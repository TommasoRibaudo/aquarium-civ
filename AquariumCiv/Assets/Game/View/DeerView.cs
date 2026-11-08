using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #425: keeps placed animals out from under the tree crowns, so they stay seen (the #424 pass had deer and wolves half under the
    /// canopies). A point inside a grown tree's crown (BoxView.TreeSpots, still a tree in the sim) is pushed straight out past the crown's
    /// edge plus <paramref name="pad"/>; a few passes for crowns that touch. Deterministic: a function of the point and the trees.
    /// </summary>
    public static class TreeCrowns
    {
        /// <summary>The crown's reach as drawn is about this share of the spot's radius (Pieces' canopies overhang their trunk spot).</summary>
        public const float Reach = 1.15f;

        public static bool Push(BoxView box, SimCore sim, ref float x, ref float z, float pad)
        {
            if (box == null) return false;
            bool moved = false;
            for (int pass = 0; pass < 4; pass++)
            {
                bool any = false;
                lock (box.TreeSpots)
                    foreach (var kv in box.TreeSpots)
                    {
                        if (sim != null && sim.EcologyOn && sim.EcoCoverAt(kv.Key) != EcoCover.Tree) continue; // felled since
                        float need = kv.Value.r * Reach + pad;
                        float dx = x - kv.Value.pos.x, dz = z - kv.Value.pos.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                        if (d >= need) continue;
                        if (d < 1e-4f) { dx = 1f; dz = 0f; d = 1f; }
                        x = kv.Value.pos.x + dx / d * need; z = kv.Value.pos.z + dz / d * need;
                        any = moved = true;
                    }
                if (!any) break;
            }
            x = Mathf.Clamp(x, -Land.HX + 0.1f, Land.HX - 0.1f);
            z = Mathf.Clamp(z, -Land.HZ + 0.1f, Land.HZ - 0.1f);
            return moved;
        }

        /// <summary>Is (x, z) under a grown tree's crown (tests)?</summary>
        public static bool Under(BoxView box, SimCore sim, float x, float z, float pad = 0f)
        {
            float px = x, pz = z;
            return Push(box, sim, ref px, ref pz, pad) && (Mathf.Abs(px - x) > 1e-3f || Mathf.Abs(pz - z) > 1e-3f);
        }
    }

    /// <summary>
    /// #425 (placed-life §5.3, animation-specs P0 / B12.c): the deer you see, every day, near the home the Keeper placed them on. Pooled
    /// <see cref="QuadDoll"/>s (a stag with antlers, does, the spring's fawns at their mothers' heels), as many as the sim counts (capped,
    /// <see cref="DeerHerd.DefaultCap"/>), moved by <see cref="DeerHerd.Field"/>: they graze, look up, walk between spots, drink at
    /// dawn, and bed down at night. Kept out from under the tree crowns (<see cref="TreeCrowns"/>).
    /// <list type="bullet">
    /// <item>People coming near: heads up (<see cref="QuadMotion.Alert"/>), then they walk off a little and come back when it is clear.</item>
    /// <item>A knock on the glass near them: heads up, then they bolt (the bound, the white rump up) into the range away from it.</item>
    /// <item>The arrival (LifeArrivalView, B12.c) draws its own dolls; when it ends they are handed over here where they stand.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(6)]
    public class DeerView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static DeerView Instance { get; private set; }

        /// <summary>A person this near makes a deer look up; still this near after the look, it walks off; it comes back when nobody is
        /// within <see cref="ClearRadius"/> for <see cref="ClearSeconds"/>.</summary>
        public const float AlertRadius = 0.6f, ShyRadius = 0.5f, ClearRadius = 0.8f, ClearSeconds = 3f, AlertSeconds = 1.0f, WalkOff = 0.4f;
        /// <summary>Speeds (box units a second): the catch-up walk, the bolt; a knock's look before the bolt.</summary>
        public const float WalkSpeed = 0.12f, BoltSpeed = QuadMotion.BoltSpeedDeer, KnockLook = 0.45f, BoltSeconds = 3f;
        /// <summary>The deer bed down from this hour to this one.</summary>
        public const float BedFrom = 21.5f, BedTo = 4.5f;
        /// <summary>Fawns shown at most (one per doe, the sim's spring count).</summary>
        public const int MaxFawns = 3;
        /// <summary>A knock this near on screen (share of the screen height) reaches a deer.</summary>
        public const float KnockScreenReach = 0.12f;

        public DeerHerd.Field Field { get; } = new DeerHerd.Field();

        sealed class Slot
        {
            public QuadDoll doll; public int id; public bool stag;
            public Vector3 cur; public float yaw; public bool placed;
            public float alertT = -1f; public Vector2 cause;
            public bool off; public Vector2 offTo; public float clearT;
            public float boltT = -1f, knockT = -1f; public Vector2 boltTo;
            public DeerAct lastAct; public float actSince;
            public float grow;
        }

        BoxView box;
        Transform holder, rootSeen;
        readonly Dictionary<int, Slot> slots = new();
        readonly List<QuadDoll> fawns = new();
        readonly List<int> gone = new();
        readonly List<PersonRef> whoBuf = new();
        readonly List<float> px = new(), pz = new();
        float refreshT; int fieldKey = int.MinValue;
        double ownClock;

        /// <summary>LifeArrivalView's hand-over: where its dolls stand when the arrival ends (box-local), in the founding group's order.</summary>
        public static readonly List<(Vector3 at, float yaw)> HandOff = new();

        // ---------------------------------------------------------------- for tests and the panel
        public int Shown { get { int n = 0; foreach (var s in slots.Values) if (s.doll != null && s.doll.go.activeSelf) n++; return n; } }
        public int FawnsShown { get { int n = 0; foreach (var f in fawns) if (f.go.activeSelf) n++; return n; } }
        public int Alerted { get { int n = 0; foreach (var s in slots.Values) if (s.alertT >= 0f) n++; return n; } }
        public int Bolting { get { int n = 0; foreach (var s in slots.Values) if (s.boltT >= 0f || s.knockT >= 0f) n++; return n; } }
        public int WalkedOff { get { int n = 0; foreach (var s in slots.Values) if (s.off) n++; return n; } }
        public int Knocks { get; private set; }
        /// <summary>Frames a shown deer stood under a crown (should stay 0).</summary>
        public int UnderCrownFrames { get; private set; }
        /// <summary>Extra spots (box-local x, z) that count as people near: tests and the debug panel.</summary>
        public readonly List<Vector2> Scarers = new();

        public bool TryGetDeer(int i, out Vector3 at, out bool stag)
        {
            at = default; stag = false;
            int k = 0;
            foreach (var s in slots.Values) { if (!s.doll.go.activeSelf) continue; if (k++ == i) { at = s.cur; stag = s.stag; return true; } }
            return false;
        }

        /// <summary>World positions of the deer shown now (shots, tests).</summary>
        public void WorldPositions(List<Vector3> into)
        {
            into.Clear();
            foreach (var s in slots.Values) if (s.doll.go.activeSelf) into.Add(s.doll.go.transform.position);
        }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static DeerView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<DeerView>();
            return v != null ? v : b.gameObject.AddComponent<DeerView>();
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
            holder = null; slots.Clear(); fawns.Clear();
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Deer") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        void HideAll()
        {
            foreach (var s in slots.Values) s.doll?.SetActive(false);
            foreach (var f in fawns) f.SetActive(false);
        }

        public double FieldNow
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
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null) { HideAll(); return; }
            if (rootSeen != box.Root) { holder = null; slots.Clear(); fawns.Clear(); rootSeen = box.Root; } // rebuilt: the dolls went with the old root
            int count = sim.EcoDeerShown();
            if (count == 0) { HideAll(); Field.Sync(0); return; }
            EnsureField(sim, dt);
            // the arrival draws its own dolls until its end beat
            var la = LifeArrivalView.Instance;
            if (la != null && la.Playing(LifeSpecies.Deer)) { HideAll(); return; }
            Field.Sync(count);
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null) pv.StandingNow(whoBuf, px, pz); else { whoBuf.Clear(); px.Clear(); pz.Clear(); }
            foreach (var sc in Scarers) { px.Add(sc.x); pz.Add(sc.y); }
            double t = FieldNow;
            float hour = pv != null ? pv.ViewHour : 12f;

            gone.Clear();
            foreach (var kv in slots) if (!Contains(Field.Alive, kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { slots[id].doll?.Destroy(); slots.Remove(id); }

            int hand = 0;
            foreach (int id in Field.Alive)
            {
                if (!slots.TryGetValue(id, out var s))
                {
                    bool stag = Field.IsStag(id);
                    s = new Slot { id = id, stag = stag, doll = new QuadDoll(QuadLook.Of(stag ? QuadKind.Stag : QuadKind.Doe, id), Holder(), box.SolidMaterial) };
                    s.doll.go.name = (stag ? "Stag " : "Doe ") + id;
                    slots[id] = s;
                }
                if (!s.placed)
                {
                    s.placed = true; s.grow = 1f;
                    if (hand < HandOff.Count) { s.cur = HandOff[hand].at; s.yaw = HandOff[hand].yaw; hand++; }
                    else
                    {
                        var p0 = Field.Sample(id, t, hour);
                        float x0 = p0.x, z0 = p0.z; TreeCrowns.Push(box, sim, ref x0, ref z0, Pad);
                        s.cur = box.land.OnGround(x0, z0); s.yaw = p0.yaw * Mathf.Rad2Deg; s.grow = 0f; // a new fawn grown up, a restock: fades in
                    }
                }
                Step(sim, s, Field.Sample(id, t, hour), dt, (float)t, hour);
            }
            if (hand > 0) HandOff.Clear();
            Fawns(sim, (float)t);
        }

        const float Pad = 0.07f;

        static bool Contains(IReadOnlyList<int> l, int id) { for (int i = 0; i < l.Count; i++) if (l[i] == id) return true; return false; }

        void EnsureField(SimCore sim, float dt)
        {
            refreshT -= dt;
            if (refreshT > 0f && fieldKey != int.MinValue) return;
            refreshT = 0.8f;
            int key = (int)(sim.State.month / 3) * 7919 + sim.EcoDeerHome * 31 + sim.EcoDeerStags * 7 + box.TreeSpots.Count;
            if (key == fieldKey) return;
            fieldKey = key;
            sim.EcoDeerRefresh(Field);
        }

        void Step(SimCore sim, Slot s, DeerPose day, float dt, float t, float hour)
        {
            // where its day has it, out from under the crowns
            float dx = day.x, dz = day.z;
            TreeCrowns.Push(box, sim, ref dx, ref dz, Pad);
            var at2 = new Vector2(s.cur.x, s.cur.z);
            if (s.knockT >= 0f)
            {   // the knock: heads up toward it, still, then the bolt
                s.knockT += dt;
                if (s.knockT >= KnockLook) { s.knockT = -1f; s.boltT = 0f; }
                else
                {
                    var kp = QuadPose.Stand(s.cur, s.yaw);
                    QuadMotion.Alert(ref kp, s.knockT, Mathf.DeltaAngle(s.yaw, Mathf.Atan2(s.cause.x - at2.x, s.cause.y - at2.y) * Mathf.Rad2Deg), true);
                    s.doll.Apply(kp); s.doll.SetActive(true);
                    return;
                }
            }
            // people near: the nearest one
            float near = float.MaxValue; Vector2 who = default;
            for (int i = 0; i < px.Count; i++)
            {
                float d = (new Vector2(px[i], pz[i]) - at2).magnitude;
                if (d < near) { near = d; who = new Vector2(px[i], pz[i]); }
            }
            if (s.boltT < 0f && s.alertT < 0f && near < AlertRadius && !s.off) { s.alertT = 0f; s.cause = who; }
            Vector2 goal = new(dx, dz);
            float speed = WalkSpeed;
            bool bolting = false;
            if (s.boltT >= 0f)
            {
                s.boltT += dt;
                goal = s.boltTo; speed = BoltSpeed; bolting = true;
                if (s.boltT > BoltSeconds || (goal - at2).magnitude < 0.02f) { s.boltT = -1f; s.off = true; s.offTo = new Vector2(s.cur.x, s.cur.z); s.clearT = 0f; }
            }
            else if (s.alertT >= 0f)
            {
                s.alertT += dt;
                goal = at2; speed = 0f;
                if (s.alertT >= AlertSeconds)
                {
                    s.alertT = -1f;
                    if (near < ShyRadius)
                    {   // walk off a little, away from them
                        var away = (at2 - who); if (away.sqrMagnitude < 1e-4f) away = Vector2.right;
                        var to = at2 + away.normalized * WalkOff;
                        float tx = to.x, tz = to.y; TreeCrowns.Push(box, sim, ref tx, ref tz, Pad);
                        s.off = true; s.offTo = new Vector2(tx, tz); s.clearT = 0f;
                    }
                }
            }
            if (s.off && s.boltT < 0f)
            {
                goal = s.offTo;
                // back to its day once nobody is near its day's spot for a while
                bool clear = true;
                for (int i = 0; i < px.Count; i++) if ((new Vector2(px[i], pz[i]) - new Vector2(dx, dz)).magnitude < ClearRadius) { clear = false; break; }
                s.clearT = clear ? s.clearT + dt : 0f;
                if (s.clearT > ClearSeconds) s.off = false;
            }
            // move toward the goal: the sim's own walk is the leg itself; a catch-up walks at WalkSpeed, a bolt bounds
            var dv = goal - at2;
            float dist = dv.magnitude;
            bool moving = speed > 0f && dist > 0.012f;
            if (moving)
            {
                float step = Mathf.Min(dist, Mathf.Max(speed, day.act == DeerAct.Walk ? DeerHerd.WalkSpeed * 1.2f : speed) * dt);
                at2 += dv / dist * step;
                float want = Mathf.Atan2(dv.x, dv.y) * Mathf.Rad2Deg;
                s.yaw = Mathf.MoveTowardsAngle(s.yaw, want, (bolting ? 540f : 200f) * dt);
            }
            else if (s.alertT < 0f && !s.off && day.act != DeerAct.Walk) s.yaw = Mathf.MoveTowardsAngle(s.yaw, day.yaw * Mathf.Rad2Deg, 40f * dt);
            s.cur = box.land.OnGround(at2.x, at2.y);
            if (TreeCrowns.Under(box, sim, s.cur.x, s.cur.z, 0f)) UnderCrownFrames++;
            s.grow = Mathf.MoveTowards(s.grow, 1f, dt / 0.8f);
            // the pose
            var act = moving ? (bolting ? DeerAct.Bolt : DeerAct.Walk) : s.alertT >= 0f ? DeerAct.LookUp : day.act;
            if (act != s.lastAct) { s.lastAct = act; s.actSince = 0f; } else s.actSince += dt;
            var p = QuadPose.Stand(s.cur, s.yaw);
            p.scale = Mathf.SmoothStep(0f, 1f, s.grow);
            bool bed = hour >= BedFrom || hour < BedTo;
            switch (act)
            {
                case DeerAct.Bolt: QuadMotion.Bolt(ref p, t + s.id * 0.37f, true); break;
                case DeerAct.Walk: QuadMotion.Walk(ref p, t + s.id * 0.31f); break;
                case DeerAct.Drink: QuadMotion.Drink(ref p, t + s.id); break;
                case DeerAct.LookUp:
                    if (s.alertT >= 0f) QuadMotion.Alert(ref p, s.alertT, Mathf.DeltaAngle(s.yaw, Mathf.Atan2(s.cause.x - at2.x, s.cause.y - at2.y) * Mathf.Rad2Deg), true);
                    else QuadMotion.Alert(ref p, s.actSince, Mathf.Sin(t * 0.7f + s.id) * 50f, false); // looks round, ears turning
                    break;
                default:
                    if (bed && !s.off) QuadMotion.LieDown(ref p, s.actSince + 1f, 0.3f);
                    else QuadMotion.Graze(ref p, t, s.actSince, s.id);
                    break;
            }
            s.doll.Apply(p);
            s.doll.SetActive(true);
        }

        /// <summary>The spring's fawns (the sim's count, at most one per doe, <see cref="MaxFawns"/>): at their mothers' heels, doing as she does.</summary>
        void Fawns(SimCore sim, float t)
        {
            int want = Mathf.Min(MaxFawns, sim.EcoDeerFawns);
            var mothers = new List<Slot>();
            foreach (var s in slots.Values) if (!s.stag && s.doll.go.activeSelf) mothers.Add(s);
            want = Mathf.Min(want, mothers.Count);
            while (fawns.Count < want) { var f = new QuadDoll(QuadLook.Of(QuadKind.Fawn, fawns.Count), Holder(), box.SolidMaterial); f.go.name = "Fawn " + fawns.Count; fawns.Add(f); }
            for (int i = 0; i < fawns.Count; i++)
            {
                if (i >= want) { fawns[i].SetActive(false); continue; }
                var m = mothers[i];
                var back = Quaternion.Euler(0f, m.yaw, 0f) * new Vector3(0.07f, 0f, -0.16f);
                float fx = m.cur.x + back.x, fz = m.cur.z + back.z;
                var p = QuadPose.Stand(box.land.OnGround(fx, fz), m.yaw + 15f);
                if (m.lastAct == DeerAct.Walk) QuadMotion.Walk(ref p, t * 1.3f);
                else if (m.lastAct == DeerAct.Bolt) QuadMotion.Bolt(ref p, t, true);
                else QuadMotion.Alert(ref p, 1f, Mathf.Sin(t * 0.9f + i) * 40f, false);
                fawns[i].Apply(p);
                fawns[i].SetActive(true);
            }
        }

        // ---------------------------------------------------------------- a knock on the glass (like the rabbits, #333)

        /// <summary>A knock on the glass close to deer on screen: they look up at once, then bolt away from the land the finger points at.
        /// How many.</summary>
        public int KnockAtScreen(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null || box == null) return 0;
            float reach = KnockScreenReach * Screen.height;
            var hit = new List<Slot>();
            foreach (var s in slots.Values)
            {
                if (!s.doll.go.activeInHierarchy) continue;
                var sp = cam.WorldToScreenPoint(s.doll.go.transform.position);
                if (sp.z > 0f && ((Vector2)sp - screenPos).magnitude <= reach) hit.Add(s);
            }
            if (hit.Count == 0) return 0;
            float fx, fz;
            var shaping = ShapingView.Instance;
            if (shaping == null || !shaping.GroundPoint(screenPos, out fx, out fz)) { var c = hit[0].cur; fx = c.x; fz = c.z - 0.3f; }
            return Startle(fx, fz, hit);
        }

        /// <summary>A knock above box-local (x, z): the deer within 1 u look up, then bolt (tests, the debug panel).</summary>
        public int KnockAtGround(float x, float z)
        {
            var hit = new List<Slot>();
            foreach (var s in slots.Values) if (s.doll.go.activeSelf && (new Vector2(s.cur.x - x, s.cur.z - z)).magnitude < 1f) hit.Add(s);
            return Startle(x, z, hit);
        }

        int Startle(float x, float z, List<Slot> hit)
        {
            var sim = Sim;
            int n = 0;
            foreach (var s in hit)
            {
                if (!Field.BoltSpot(s.cur.x, s.cur.z, x, z, out float bx, out float bz)) continue;
                TreeCrowns.Push(box, sim, ref bx, ref bz, Pad);
                s.alertT = -1f; s.boltT = -1f;
                s.knockT = 0f; // looks up first, then bounds
                s.boltTo = new Vector2(bx, bz);
                s.cause = new Vector2(x, z);
                n++;
            }
            if (n > 0) Knocks++;
            return n;
        }

        public string Status()
        {
            var sim = Sim;
            return $"deer: shown {Shown} (+{FawnsShown} fawns) of sim {(sim != null ? sim.EcoDeerCount : 0)}, home {(sim != null ? sim.EcoDeerHome : -1)}, " +
                   $"range {Field.Range.Count}; alert {Alerted}, off {WalkedOff}, bolting {Bolting}; knocks {Knocks}; under crowns {UnderCrownFrames}";
        }
    }
}
