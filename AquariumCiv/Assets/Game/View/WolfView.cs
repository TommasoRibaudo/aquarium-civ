using System.Collections.Generic;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #425 (placed-life §5.6, animation-specs P0, B12.f): the wolves you see, every day, at their den in the far wood. Pooled
    /// <see cref="QuadDoll"/>s (grey, low, tail down; the grey wolf with its pale face; this spring's pups as grey fluff), as many as the
    /// sim's pack, moved by <see cref="WolfPack.Field"/>: by day they lie at the den or trot between spots at the wood's edge and the pups
    /// play; at dusk they sit and howl; on a night the eyes show, one or two stand at the firelight's edge and watch.
    /// <para>The hard rules hold for every point drawn (<see cref="WolfRules"/>): never inside the tent ring (every tent + 0.25 u), never
    /// within 0.3 u of a person. Every frame's point is checked; a move whose next step would break a rule is not walked, the wolf goes
    /// round through the trees (it is shown at its target). Kept out from under the tree crowns too (<see cref="TreeCrowns"/>), as long as
    /// that point still keeps the rules.</para>
    /// <para>The arrival (LifeArrivalView, B12.f) draws its own dolls; when it ends they are handed over here. The first dusk's howl
    /// (<see cref="ForceHowl"/>) makes the pack sit and howl now.</para>
    /// </summary>
    [DefaultExecutionOrder(6)]
    public class WolfView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static WolfView Instance { get; private set; }

        /// <summary>Speeds (box units a second): the trot (the catch-up), a little over the sim's leg trot.</summary>
        public const float TrotSpeed = 0.2f;
        /// <summary>The most wolves drawn (the sim's cap).</summary>
        public const int Cap = 8;

        public WolfPack.Field Field { get; } = new WolfPack.Field();

        sealed class Slot
        {
            public QuadDoll doll; public bool pup, grey;
            public Vector3 cur; public float yaw; public bool placed;
            public WolfAct lastAct; public float actSince; public float grow;
        }

        BoxView box;
        Transform holder, rootSeen;
        readonly List<Slot> slots = new();
        readonly List<PersonRef> whoBuf = new();
        readonly List<float> px = new(), pz = new(), people = new();
        float refreshT; int fieldKey = int.MinValue;
        float howlUntil = -1f, howlStart;
        double ownClock;

        /// <summary>LifeArrivalView's hand-over: where its dolls stand when the arrival ends (box-local).</summary>
        public static readonly List<(Vector3 at, float yaw)> HandOff = new();

        // ---------------------------------------------------------------- for tests and the panel
        public int Shown { get { int n = 0; foreach (var s in slots) if (s.doll.go.activeSelf) n++; return n; } }
        public int PupsShown { get { int n = 0; foreach (var s in slots) if (s.pup && s.doll.go.activeSelf) n++; return n; } }
        /// <summary>Frames a shown wolf stood where the rules forbid (inside the tent ring, near a person, too near the fire). Must stay 0.</summary>
        public int RuleBreaks { get; private set; }
        /// <summary>Frames a shown wolf stood under a crown (should stay 0 unless the rules left no other spot).</summary>
        public int UnderCrownFrames { get; private set; }
        public int Detours { get; private set; }
        public bool Howling => Time.unscaledTime < howlUntil;
        public string LastActs { get; private set; } = "";
        /// <summary>Extra spots (box-local x, z) that count as people: tests and the debug panel.</summary>
        public readonly List<Vector2> Scarers = new();

        public void WorldPositions(List<Vector3> into)
        {
            into.Clear();
            foreach (var s in slots) if (s.doll.go.activeSelf) into.Add(s.doll.go.transform.position);
        }

        public void LocalPositions(List<Vector3> into)
        {
            into.Clear();
            foreach (var s in slots) if (s.doll.go.activeSelf) into.Add(s.cur);
        }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static WolfView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<WolfView>();
            return v != null ? v : b.gameObject.AddComponent<WolfView>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => HandOff.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (holder != null) Destroy(holder.gameObject);
            holder = null; slots.Clear();
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Wolves") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        void HideAll() { foreach (var s in slots) s.doll.SetActive(false); }

        /// <summary>The pack sits and howls now for <paramref name="seconds"/> (the first dusk's howl, the debug panel; B21.a is #441's).</summary>
        public void ForceHowl(float seconds = 5f) { howlStart = Time.unscaledTime; howlUntil = Time.unscaledTime + seconds; }

        double FieldNow
        {
            get
            {
                var pv = PeopleView.Find();
                if (pv != null && pv.Planner != null) return HuntClock.FieldSeconds(pv.Now.day, pv.Now.hour);
                return ownClock;
            }
        }

        void Update()
        {
            var sim = Sim;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            ownClock += dt;
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null || !sim.WolvesPlaced) { HideAll(); return; }
            if (rootSeen != box.Root) { holder = null; slots.Clear(); rootSeen = box.Root; }
            EnsureField(sim, dt);
            var la = LifeArrivalView.Instance;
            if (la != null && la.Playing(LifeSpecies.Wolves)) { HideAll(); return; }
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null) pv.StandingNow(whoBuf, px, pz); else { whoBuf.Clear(); px.Clear(); pz.Clear(); }
            people.Clear();
            for (int i = 0; i < px.Count; i++) { people.Add(px[i]); people.Add(pz[i]); }
            foreach (var sc in Scarers) { people.Add(sc.x); people.Add(sc.y); }
            int count = Mathf.Min(Cap, Field.Count);
            while (slots.Count > count) { slots[slots.Count - 1].doll.Destroy(); slots.RemoveAt(slots.Count - 1); }
            double t = FieldNow;
            long day = pv != null ? pv.Now.day : 0;
            float hour = pv != null ? pv.ViewHour : 12f;
            int hand = 0;
            var acts = new System.Text.StringBuilder();
            for (int id = 0; id < count; id++)
            {
                bool pup = Field.IsPup(id), grey = id == WolfPack.GreyWolf;
                if (id >= slots.Count || slots[id].pup != pup)
                {
                    var look = QuadLook.Of(pup ? QuadKind.Pup : QuadKind.Wolf, id);
                    look.paleFace = grey && !pup; // the grey wolf's pale silver face (B22)
                    var s0 = new Slot { pup = pup, grey = grey, doll = new QuadDoll(look, Holder(), box.SolidMaterial) };
                    s0.doll.go.name = (pup ? "Pup " : grey ? "Grey wolf " : "Wolf ") + id;
                    if (id < slots.Count) { slots[id].doll.Destroy(); slots[id] = s0; } else slots.Add(s0);
                }
                var s = slots[id];
                var pose = Field.Sample(id, t, day, hour, people);
                if (Howling && !pup) { pose.act = WolfAct.Howl; pose.visible = true; }
                if (!s.placed)
                {
                    s.placed = true; s.grow = 1f;
                    if (hand < HandOff.Count) { s.cur = HandOff[hand].at; s.yaw = HandOff[hand].yaw; hand++; }
                    else { s.cur = box.land.OnGround(pose.x, pose.z); s.yaw = pose.yaw * Mathf.Rad2Deg; s.grow = 0f; }
                }
                Step(sim, s, pose, dt, (float)t);
                acts.Append(pose.act.ToString()[0]);
            }
            if (hand > 0) HandOff.Clear();
            LastActs = acts.ToString();
        }

        void EnsureField(SimCore sim, float dt)
        {
            refreshT -= dt;
            if (refreshT > 0f && fieldKey != int.MinValue) return;
            refreshT = 0.8f;
            int key = (int)(sim.State.month / 3) * 7919 + sim.WolfDen * 31 + sim.EcoWolfCount * 7 + sim.EcoWolfPups * 3 + sim.State.buildings.Count * 131
                      + Mathf.RoundToInt(sim.WolfBoldness * 20f) * 17 + Mathf.RoundToInt(sim.WolfEyesChance * 20f) * 1009;
            if (key == fieldKey) return;
            fieldKey = key;
            sim.EcoWolfRefresh(Field);
        }

        bool Ok(float x, float z, float minFire) => WolfRules.Allowed(x, z, Field.Ground, people, minFire);

        void Step(SimCore sim, Slot s, WolfPose pose, float dt, float t)
        {
            if (!pose.visible) { s.doll.SetActive(false); s.placed = false; return; } // in the trees: it comes back out at its spot
            float minFire = WolfRules.MinFromFire(Field.Boldness);
            // the watch at the firelight's edge may come nearer the fire than the day's range (the sim's EyesSpot rule)
            float lim = pose.act == WolfAct.Watch ? WolfRules.FirelightEdge : minFire;
            float gx = pose.x, gz = pose.z;
            float cx = gx, cz = gz;
            if (TreeCrowns.Push(box, sim, ref cx, ref cz, s.pup ? 0.04f : 0.06f) && Ok(cx, cz, lim)) { gx = cx; gz = cz; }
            var at2 = new Vector2(s.cur.x, s.cur.z);
            var dv = new Vector2(gx, gz) - at2;
            float dist = dv.magnitude;
            bool moving = dist > 0.012f;
            if (moving)
            {
                float step = Mathf.Min(dist, TrotSpeed * dt);
                var next = at2 + dv / dist * step;
                if (!Ok(next.x, next.y, lim)) { next = new Vector2(gx, gz); Detours++; s.grow = 0f; moving = false; } // round through the trees
                else s.yaw = Mathf.MoveTowardsAngle(s.yaw, Mathf.Atan2(dv.x, dv.y) * Mathf.Rad2Deg, 300f * dt);
                at2 = next;
            }
            else s.yaw = Mathf.MoveTowardsAngle(s.yaw, pose.yaw * Mathf.Rad2Deg, 60f * dt);
            s.cur = box.land.OnGround(at2.x, at2.y);
            if (!Ok(s.cur.x, s.cur.z, Mathf.Min(lim, WolfRules.FirelightEdge))) RuleBreaks++;
            if (TreeCrowns.Under(box, sim, s.cur.x, s.cur.z)) UnderCrownFrames++;
            s.grow = Mathf.MoveTowards(s.grow, 1f, dt / 0.6f);
            var act = moving ? WolfAct.Trot : pose.act;
            if (act != s.lastAct) { s.lastAct = act; s.actSince = 0f; } else s.actSince += dt;
            var p = QuadPose.Stand(s.cur, s.yaw);
            p.scale = Mathf.SmoothStep(0f, 1f, s.grow);
            switch (act)
            {
                case WolfAct.Trot: QuadMotion.Trot(ref p, t + s.cur.x * 3f); p.tail = -6f; break;
                case WolfAct.Play: QuadMotion.PlayBow(ref p, t + s.cur.z * 5f); break;
                case WolfAct.Howl:
                    {
                        float since = Howling ? Time.unscaledTime - howlStart : s.actSince;
                        float k = Mathf.Repeat(since + (s.grey ? 0f : 0.6f), 6f); // a howl, a rest, again; the others join a beat after
                        QuadMotion.SitHowl(ref p, k, 2.6f);
                        if (k > 3.4f) { p.sit = 1f; p.neck = 0f; p.head = 0f; } // sits on between howls
                        break;
                    }
                case WolfAct.Watch:
                    QuadMotion.Alert(ref p, s.actSince, 0f, false);
                    p.neck = -4f; p.tail = -8f;
                    break;
                default:
                    if (s.pup) QuadMotion.SleepCurl(ref p, t);
                    else QuadMotion.LieDown(ref p, s.actSince + 0.6f);
                    break;
            }
            s.doll.Apply(p);
            s.doll.SetActive(true);
        }

        public string Status()
        {
            var sim = Sim;
            return $"wolves: shown {Shown} ({PupsShown} pups) of sim {(sim != null ? sim.EcoWolfCount : 0)}, den {(sim != null ? sim.WolfDen : -1)}, range {Field.Range.Count}, " +
                   $"acts {LastActs}; rule breaks {RuleBreaks}, detours {Detours}, under crowns {UnderCrownFrames}{(Howling ? ", howling" : "")}";
        }
    }
}
