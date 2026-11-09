using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// P9 #426 (animation-specs B13): what the deer hunt leaves on the land besides the people (their beats are the planner's,
    /// LifePlanner.DeerScene.cs, posed by <see cref="DeerBeats"/>):
    /// <list type="bullet">
    /// <item>The <b>arrow</b>: from the shooter's bow at <see cref="DeerHuntPlan.releaseHour"/> on a low arc (<see cref="DeerTiming.FlightSeconds"/>)
    /// to the deer (a hit: it vanishes at the contact, no arrow shows in it, no blood) or past it into the ground at the wood's edge (a miss:
    /// it sticks and quivers, two chips, until the shooter pulls it).</item>
    /// <item>The <b>deer the hunt took</b>: it jumps, stumbles three steps and lies down on its side; it hangs from the pole between the
    /// two carriers, legs up, swaying; it is set down by the fire (dust) and stays there a while.</item>
    /// <item>The <b>feast</b> (B13.c, the first deer and the white stag only): that night the fire burns bigger with a spit over it, and a
    /// dance ring round it.</item>
    /// <item>The <b>antlers</b> over the chief's door (B13.d) from the next morning on, for good (the white stag's bigger, white-tipped
    /// ones replace them).</item>
    /// </list>
    /// The deer herd itself (holding the target, the herd's bolt, taking the deer out) is <see cref="DeerView"/>'s.
    /// </summary>
    [DefaultExecutionOrder(8)]
    public class DeerHuntStage : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static DeerHuntStage Instance { get; private set; }

        sealed class Live
        {
            public DeerHuntPlan plan;
            public Transform arrow;
            public ArcShot shot; public bool shotMade, landed;
            public QuadDoll deer; public Transform pole;
            public bool setDown;
        }

        BoxView box;
        Transform holder, rootSeen;
        readonly Dictionary<DeerHuntPlan, Live> lives = new();
        readonly List<DeerHuntPlan> plans = new(), drop = new();
        Mesh arrowMesh, poleMesh, antlerMesh, whiteAntlerMesh, spitMesh, flameMesh;
        Transform antlers, feast, roast; readonly List<Transform> flames = new();

        // ---------------------------------------------------------------- tests and the panel
        public int ArrowsShot { get; private set; }
        public int ArrowsLanded { get; private set; }
        public int SetDowns { get; private set; }
        public int Feasts { get; private set; }
        public bool AntlersShown => antlers != null && antlers.gameObject.activeSelf;
        public bool FeastShown => feast != null && feast.gameObject.activeSelf;
        /// <summary>The arrow now (box-local tip), if one flies or stands.</summary>
        public bool TryGetArrow(out Vector3 tip, out bool flying)
        {
            tip = default; flying = false;
            foreach (var l in lives.Values) if (l.arrow != null && l.arrow.gameObject.activeSelf) { tip = l.arrow.localPosition; flying = !l.landed; return true; }
            return false;
        }
        /// <summary>The deer the hunt took, as drawn now (box-local), if any: lying, on the pole or set down.</summary>
        public bool TryGetCarcass(out Vector3 at, out bool onPole)
        {
            at = default; onPole = false;
            foreach (var l in lives.Values)
                if (l.deer != null && l.deer.go.activeSelf) { at = l.deer.go.transform.localPosition; onPole = l.pole != null && l.pole.gameObject.activeSelf; return true; }
            return false;
        }
        /// <summary>The pole's ends (box-local) while carried; false otherwise.</summary>
        public bool TryGetPole(out Vector3 a, out Vector3 b)
        {
            a = b = default;
            foreach (var l in lives.Values)
                if (l.pole != null && l.pole.gameObject.activeSelf) { var half = l.pole.localRotation * new Vector3(0f, 0f, l.pole.localScale.z * 0.5f); a = l.pole.localPosition - half; b = l.pole.localPosition + half; return true; }
            return false;
        }
        /// <summary>The longest gap seen between a pole end and its carrier's shoulder while carried (B13.b test: within 0.02 u).</summary>
        public float PoleGapMax { get; private set; }
        public void ResetPoleGap() => PoleGapMax = 0f;

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static DeerHuntStage Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<DeerHuntStage>();
            return v != null ? v : b.gameObject.AddComponent<DeerHuntStage>();
        }

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
            Clear();
            if (holder != null) Destroy(holder.gameObject);
            holder = null;
        }

        void OnDestroy()
        {
            foreach (var m in new[] { arrowMesh, poleMesh, antlerMesh, whiteAntlerMesh, spitMesh, flameMesh }) if (m != null) Destroy(m);
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Deer hunt") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        void Clear()
        {
            foreach (var l in lives.Values) Kill(l);
            lives.Clear();
        }

        void Kill(Live l)
        {
            if (l.arrow != null) Destroy(l.arrow.gameObject);
            if (l.pole != null) Destroy(l.pole.gameObject);
            l.deer?.Destroy();
        }

        void Update()
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null) { Clear(); HideDressing(); return; }
            if (rootSeen != box.Root) { lives.Clear(); holder = null; antlers = feast = roast = null; flames.Clear(); rootSeen = box.Root; }
            var pv = PeopleView.Find();
            var planner = pv != null ? pv.Planner : null;
            if (planner == null) { Clear(); HideDressing(); return; }
            var now = pv.Now;
            settledBuf.Clear();
            planner.ResolveWhiteHunts(now, settledBuf);
            planner.DeerScenesOn(now.day, plans);
            drop.Clear();
            foreach (var kv in lives) if (!plans.Contains(kv.Key) || now.hour > kv.Key.end + 1.5f) drop.Add(kv.Key);
            foreach (var p in drop) { Kill(lives[p]); lives.Remove(p); }
            foreach (var p in plans)
            {
                if (now.hour < p.releaseHour - 0.01f) continue;
                // the first deer (and the white stag) earn the feast that night (B13.c)
                if (p.playsHit && now.hour >= p.landHour && !feastDays.Contains(p.day) && (p.white || sim.EcoDeerTaken <= 1)) { feastDays.Add(p.day); }
                if (p.playsHit && now.hour >= p.landHour && (p.targetStag || p.white)) antlerDay = Mathf.Max(antlerDay, (int)p.day);
                // a finished hunt is over for good: the plan stays listed for the rest of the day, but it must never be rebuilt (it re-shot
                // its arrow and re-laid its carcass every frame: tester 2026-11-08, ArrowsShot 2 -> 81 with no new hunt)
                if (now.hour > p.end + 1.5f) continue;
                if (!lives.TryGetValue(p, out var l)) { l = new Live { plan = p }; lives[p] = l; }
                Arrow(l, pv, now);
                if (p.playsHit) Carcass(l, pv, now, sim);
            }
            Feast(pv, now);
            Antlers(sim, now);
        }

        readonly List<DeerHuntPlan> settledBuf = new();

        // ---------------------------------------------------------------- the arrow
        void Arrow(Live l, PeopleView pv, ViewTime now)
        {
            var p = l.plan;
            float gone = p.playsHit ? p.landHour : p.arrowTakeHour;
            if (now.hour >= gone + (p.playsHit ? 0f : 0f) && l.landed && p.playsHit) { if (l.arrow != null) l.arrow.gameObject.SetActive(false); return; }
            if (!p.playsHit && now.hour >= p.arrowTakeHour) { if (l.arrow != null) l.arrow.gameObject.SetActive(false); return; }
            float rel = LifeClock.RealSecondsBetween(p.releaseHour, Mathf.Max(p.releaseHour, now.hour));
            if (!l.shotMade)
            {
                Vector3 start;
                if (pv.TryGetDollTransform(p.hunters[0], out var t)) start = Holder().InverseTransformPoint(t.TransformPoint(new Vector3(0f, PegDoll.BodyH * 0.8f, -0.05f)));
                else start = box.land.OnGround(p.shotX, p.shotZ) + Vector3.up * 0.1f;
                var end = box.land.OnGround(p.landX, p.landZ) + Vector3.up * (p.playsHit ? 0.09f : 0.004f);
                l.shot = ArcShot.Throw(start, end, DeerTiming.FlightApex, DeerTiming.FlightSeconds, p.playsHit ? Landing.DropIn : Landing.Stick, 0f);
                l.shotMade = true;
                ArrowsShot++;
                l.arrow = RentArrow();
            }
            var s = Arc.Sample(l.shot, rel);
            bool vis = s.Visible && !(p.playsHit && rel >= l.shot.flight);
            l.arrow.gameObject.SetActive(vis);
            if (s.Visible) l.arrow.SetLocalPositionAndRotation(s.position, s.rotation);
            if (!l.landed && rel >= l.shot.flight)
            {
                l.landed = true; ArrowsLanded++;
                var at = Holder().TransformPoint(l.shot.end);
                if (HitFx.Enabled) HitFx.Puff(p.playsHit ? PuffKind.Dust : PuffKind.Chips, at, Vector3.up, p.playsHit ? 0.6f : 0.8f);
            }
            if (!p.playsHit && l.landed && now.hour >= p.arrowTakeHour - 0.002f && HitFx.Enabled && l.arrow.gameObject.activeSelf)
                HitFx.Puff(PuffKind.Dust, Holder().TransformPoint(l.shot.end), Vector3.up, 0.6f);
        }

        Transform RentArrow()
        {
            var go = new GameObject("Arrow") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Holder(), false);
            var body = new GameObject("Mesh") { hideFlags = HideFlags.DontSave };
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = arrowMesh ??= BuildArrow();
            var mr = body.AddComponent<MeshRenderer>(); mr.sharedMaterial = box.SolidMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A thin 0.12 u arrow along +z, the tip at the origin, a pale fletch at the back (drawn a little over life to read).</summary>
        static Mesh BuildArrow()
        {
            var mb = new MeshBuilder(4260) { jitter = 0f };
            const float s = 1.3f, L = 0.12f * s;
            mb.Box(new Vector3(0f, 0f, -L * 0.5f), new Vector3(0.006f, 0.006f, L), Palette.WoodLight);
            mb.Box(new Vector3(0f, 0f, -0.006f), new Vector3(0.012f, 0.012f, 0.014f), Palette.RockLight);
            mb.Box(new Vector3(0f, 0f, -L + 0.012f), new Vector3(0.002f, 0.022f, 0.026f), Palette.Plaster);
            mb.Box(new Vector3(0f, 0f, -L + 0.012f), new Vector3(0.022f, 0.002f, 0.026f), Palette.Plaster);
            return mb.ToMesh("DeerArrow");
        }

        // ---------------------------------------------------------------- the deer they took
        void Carcass(Live l, PeopleView pv, ViewTime now, SimCore sim)
        {
            var p = l.plan;
            if (now.hour < p.landHour) return;
            float keep = p.setDownHour + 1.6f;
            if (now.hour >= keep || now.hour >= 23.5f) { l.deer?.SetActive(false); if (l.pole != null) l.pole.gameObject.SetActive(false); return; }
            if (l.deer == null)
            {
                var kind = p.white ? QuadKind.WhiteStag : p.targetStag ? QuadKind.Stag : QuadKind.Doe;
                l.deer = new QuadDoll(QuadLook.Of(kind, p.targetId < 0 ? 0 : p.targetId), Holder(), box.SolidMaterial);
                l.deer.go.name = "Hunted " + kind;
            }
            float yaw = Mathf.Atan2(p.fireX - p.targetX, p.fireZ - p.targetZ) * Mathf.Rad2Deg + 75f;
            var at = box.land.OnGround(p.targetX, p.targetZ);
            QuadPose q;
            bool carried = now.hour >= p.takeHour && now.hour < p.setDownHour;
            if (!carried && now.hour < p.takeHour)
            {
                // B13.a: it jumps 0.03 u, stumbles three steps, lies down on its side and is still
                float s = LifeClock.RealSecondsBetween(p.landHour, now.hour);
                var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                if (s < 0.35f) { q = QuadPose.Stand(at, yaw); QuadMotion.Alert(ref q, s, 0f, true); q.lift = Mathf.Sin(s / 0.35f * Mathf.PI) * 0.03f; }
                else if (s < 1.2f) { q = QuadPose.Stand(at + fwd * (0.07f * (s - 0.35f) / 0.85f), yaw); QuadMotion.Walk(ref q, s * 1.4f); q.roll = Mathf.Sin(s * 9f) * 6f; }
                else
                {
                    q = QuadPose.Stand(at + fwd * 0.07f, yaw);
                    QuadMotion.LieDown(ref q, s - 1.2f, 0.4f);
                    q.roll = Mathf.Lerp(0f, 90f, Mathf.SmoothStep(0f, 1f, (s - 1.5f) / 0.3f));
                }
                if (l.pole != null) l.pole.gameObject.SetActive(false);
                l.deer.Apply(q); l.deer.SetActive(true);
                return;
            }
            if (carried)
            {
                // B13.b: under the pole between the carriers' shoulders, legs tied up, swaying 6 degrees at 1 Hz
                int run = Mathf.Min(p.poleRunner, p.hunters.Count - 1);
                if (Shoulder(pv, p.hunters[0], out var a) && Shoulder(pv, p.hunters[run], out var b))
                {
                    var mid = (a + b) * 0.5f; var axis = b - a;
                    float len = axis.magnitude;
                    PoleGapMax = Mathf.Max(PoleGapMax, Mathf.Abs(len - DeerTiming.PoleHalf * 2f) * 0.5f);
                    float pyaw = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;
                    var pole = Pole();
                    pole.SetLocalPositionAndRotation(mid, Quaternion.LookRotation(axis.sqrMagnitude > 1e-6f ? axis : Vector3.forward));
                    pole.localScale = new Vector3(1f, 1f, Mathf.Max(len + 0.04f, 0.2f));
                    pole.gameObject.SetActive(true);
                    float sway = Mathf.Sin(Time.time * Mathf.PI * 2f) * 6f;
                    q = QuadPose.Stand(mid - Vector3.up * 0.1f, pyaw);
                    q.roll = 180f + sway; q.lie = 0.6f; q.neck = 30f;
                    l.deer.Apply(q); l.deer.SetActive(true);
                    return;
                }
                l.deer.SetActive(false);
                return;
            }
            // set down by the fire: dust, then it lies there a while
            if (!l.setDown)
            {
                l.setDown = true; SetDowns++;
                if (HitFx.Enabled) HitFx.Puff(PuffKind.Dust, Holder().TransformPoint(box.land.OnGround(p.setX, p.setZ)), Vector3.up, 1f);
                LookAtTheDeer(pv, p);
            }
            if (l.pole != null) l.pole.gameObject.SetActive(false);
            q = QuadPose.Stand(box.land.OnGround(p.setX, p.setZ), Mathf.Atan2(p.dirX, p.dirZ) * Mathf.Rad2Deg + 90f);
            q.lie = 1f; q.roll = 90f; q.neck = 10f;
            l.deer.Apply(q); l.deer.SetActive(true);
        }

        /// <summary>B13.b end beat: everyone near the fire turns to the deer the hunters set down.</summary>
        static void LookAtTheDeer(PeopleView pv, DeerHuntPlan p)
        {
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            for (int i = 0; i < who.Count; i++)
                if (!p.hunters.Contains(who[i]) && (new Vector2(xs[i] - p.setX, zs[i] - p.setZ)).magnitude < 1.6f) pv.LookAt(who[i], new Vector2(p.setX, p.setZ));
        }

        bool Shoulder(PeopleView pv, PersonRef who, out Vector3 at)
        {
            at = default;
            if (!pv.TryGetDollTransform(who, out var t)) return false;
            at = Holder().InverseTransformPoint(t.TransformPoint(new Vector3(0f, PegDoll.BodyH + 0.012f, 0f)));
            return true;
        }

        Transform Pole()
        {
            foreach (var l in lives.Values) if (l.pole != null) return l.pole;
            var go = new GameObject("Pole") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Holder(), false);
            go.AddComponent<MeshFilter>().sharedMesh = poleMesh ??= BuildPole();
            go.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
            foreach (var l in lives.Values) if (l.plan.playsHit && l.pole == null) { l.pole = go.transform; break; }
            return go.transform;
        }

        static Mesh BuildPole()
        {
            var mb = new MeshBuilder(4261) { jitter = 0f };
            mb.Box(Vector3.zero, new Vector3(0.012f, 0.012f, 1f), Palette.WoodLight); // scaled along z to the carriers' gap
            return mb.ToMesh("DeerPole");
        }

        // ---------------------------------------------------------------- B13.c the feast
        readonly HashSet<long> feastDays = new();
        long feastShownDay = -1, ringDay = -1;
        public const float FeastFrom = 18.5f, FeastTo = 23.5f, RingAt = 19.2f;

        /// <summary>The debug panel's "deer feast now": tonight's feast (the hour jumps to dusk if it is earlier).</summary>
        public string DebugFeastNow()
        {
            var pv = PeopleView.Find();
            if (pv == null) return "no people view";
            feastDays.Add(pv.Now.day);
            ringDay = -1;
            if (pv.Now.hour < FeastFrom || pv.Now.hour > FeastTo) PeopleView.DebugSetHour(FeastFrom + 0.1f);
            return "the feast tonight: the big fire, the spit, the dance ring";
        }

        void Feast(PeopleView pv, ViewTime now)
        {
            bool on = feastDays.Contains(now.day) && now.hour >= FeastFrom && now.hour < FeastTo;
            if (!on) { if (feast != null) feast.gameObject.SetActive(false); return; }
            var planner = pv.Planner;
            if (!planner.Positions.TryGetPosition(Place.Land(PlaceKind.Fire), out float fx, out float fz)) { fx = Sim.EcoGround.campX; fz = Sim.EcoGround.campZ; }
            if (feast == null) BuildFeast();
            feast.localPosition = box.land.OnGround(fx, fz);
            feast.gameObject.SetActive(true);
            if (feastShownDay != now.day) { feastShownDay = now.day; Feasts++; }
            // the fire burns bigger (scale 1.3), the roast turns slowly, sparks go up now and then
            float t = Time.time;
            for (int i = 0; i < flames.Count; i++)
            {
                float k = 1f + Mathf.Sin(t * (6f + i * 1.7f) + i) * 0.12f;
                flames[i].localScale = new Vector3(1.3f, 1.3f * k, 1.3f);
            }
            if (roast != null) roast.localRotation = Quaternion.Euler(t * 25f, 0f, 0f);
            if (HitFx.Enabled && Random.value < Time.deltaTime * 1.5f) HitFx.Puff(PuffKind.Sparks, feast.TransformPoint(new Vector3(0f, 0.2f, 0f)), Vector3.up, 0.7f);
            // the dance ring round the fire (B2.e)
            if (now.hour >= RingAt && ringDay != now.day) { ringDay = now.day; pv.KidPlay.Force(PlayGame.DanceRing); }
        }

        void BuildFeast()
        {
            feast = new GameObject("Feast") { hideFlags = HideFlags.DontSave }.transform;
            feast.SetParent(Holder(), false);
            // the spit: two forked sticks and a bar over the fire
            var spit = new GameObject("Spit") { hideFlags = HideFlags.DontSave };
            spit.transform.SetParent(feast, false);
            spit.AddComponent<MeshFilter>().sharedMesh = spitMesh ??= BuildSpit();
            spit.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
            roast = new GameObject("Roast") { hideFlags = HideFlags.DontSave }.transform;
            roast.SetParent(feast, false);
            roast.localPosition = new Vector3(0f, 0.27f, 0f);
            var rmb = new MeshBuilder(4263) { jitter = 0.03f };
            rmb.Ico(Vector3.zero, 0.032f, new Vector3(2.2f, 1f, 1f), new Color(0.5f, 0.27f, 0.15f), 0.15f);
            var rgo = new GameObject("Meat") { hideFlags = HideFlags.DontSave };
            rgo.transform.SetParent(roast, false);
            rgo.AddComponent<MeshFilter>().sharedMesh = rmb.ToMesh("DeerRoast");
            rgo.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
            // bigger flames: three tongues
            flames.Clear();
            for (int i = 0; i < 3; i++)
            {
                var f = new GameObject("Flame " + i) { hideFlags = HideFlags.DontSave };
                f.transform.SetParent(feast, false);
                f.transform.localPosition = new Vector3((i - 1) * 0.025f, 0.02f, (i % 2) * 0.02f - 0.01f);
                f.AddComponent<MeshFilter>().sharedMesh = flameMesh ??= BuildFlame();
                var mr = f.AddComponent<MeshRenderer>(); mr.sharedMaterial = box.SolidMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flames.Add(f.transform);
            }
        }

        static Mesh BuildSpit()
        {
            var mb = new MeshBuilder(4262) { jitter = 0.02f };
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                mb.Box(new Vector3(x, 0.135f, 0f), new Vector3(0.012f, 0.27f, 0.012f), Palette.Bark);
                mb.Box(new Vector3(x + 0.01f, 0.285f, 0f), new Vector3(0.008f, 0.03f, 0.008f), Palette.Bark, Quaternion.Euler(0f, 0f, -30f));
                mb.Box(new Vector3(x - 0.01f, 0.285f, 0f), new Vector3(0.008f, 0.03f, 0.008f), Palette.Bark, Quaternion.Euler(0f, 0f, 30f));
            }
            mb.Box(new Vector3(0f, 0.27f, 0f), new Vector3(0.32f, 0.008f, 0.008f), Palette.WoodLight);
            return mb.ToMesh("DeerSpit");
        }

        static Mesh BuildFlame()
        {
            var mb = new MeshBuilder(4264) { jitter = 0.02f };
            mb.Prism(Vector3.zero, 0.022f, 0f, 0.13f, 5, new Color(1f, 0.6f, 0.15f));
            mb.Prism(new Vector3(0.008f, 0.005f, 0f), 0.014f, 0f, 0.09f, 5, new Color(1f, 0.9f, 0.4f));
            return mb.ToMesh("FeastFlame");
        }

        void HideDressing()
        {
            if (feast != null) feast.gameObject.SetActive(false);
            if (antlers != null) antlers.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- B13.d the antlers over the chief's door
        int antlerDay = -1;
        bool antlersWhite;

        /// <summary>The debug panel's "antlers up now": the first stag's antlers go up at once (sim and view).</summary>
        public string DebugAntlersNow()
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn) return "no living box";
            if (sim.State.eco.antlersMonth < 0) sim.State.eco.antlersMonth = sim.State.month;
            antlerDay = -1;
            return "the antlers hang over the chief's door";
        }

        void Antlers(SimCore sim, ViewTime now)
        {
            bool up = sim.DeerAntlersUp || sim.WhiteStagAntlers;
            // they go up the morning after the hunt that brought them (from 7 h), and stay
            if (up && antlerDay >= 0 && (now.day < antlerDay + 1 || (now.day == antlerDay + 1 && now.hour < 7f))) up = false;
            if (!up) { if (antlers != null) antlers.gameObject.SetActive(false); return; }
            bool white = sim.WhiteStagAntlers;
            if (antlers == null || antlersWhite != white)
            {
                if (antlers != null) Destroy(antlers.gameObject);
                var go = new GameObject(white ? "White antlers" : "Antlers") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(Holder(), false);
                go.AddComponent<MeshFilter>().sharedMesh = white ? (whiteAntlerMesh ??= BuildAntlers(true)) : (antlerMesh ??= BuildAntlers(false));
                go.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
                antlers = go.transform; antlersWhite = white;
            }
            if (!ChiefDoor(sim, out var at, out float yaw)) { antlers.gameObject.SetActive(false); return; }
            antlers.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            antlers.gameObject.SetActive(true);
        }

        /// <summary>Over the chief's door (the leader's home's front, 0.17 u up), facing out; the fire's edge when the chief has no home.</summary>
        bool ChiefDoor(SimCore sim, out Vector3 at, out float yaw)
        {
            at = default; yaw = 0f;
            var chief = sim.HolderOf(SimCore.LeaderOf(sim.State.era)) ?? sim.HolderOf(RoleType.Chief);
            var lot = chief != null && sim.Settlement != null ? sim.Settlement.HomeOf(chief) : null;
            if (lot == null)
            {
                float fx = sim.EcoGround.campX, fz = sim.EcoGround.campZ;
                at = box.land.OnGround(fx + 0.3f, fz);
                return true;
            }
            lot.Front(out float frx, out float frz);
            // beside the door, on the side the camera sees
            float out1 = lot.d * 0.5f + 0.05f, side = lot.w * 0.5f * 0.6f + 0.03f;
            float sx = -frz, sz = frx;
            var cam = Camera.main;
            if (cam != null)
            {
                var c = box.Root.InverseTransformPoint(cam.transform.position);
                if ((c.x - lot.x) * sx + (c.z - lot.z) * sz < 0f) { sx = -sx; sz = -sz; }
            }
            float x = lot.x + frx * out1 + sx * side, z = lot.z + frz * out1 + sz * side;
            at = box.land.OnGround(x, z);
            yaw = Mathf.Atan2(frx, frz) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>The post the antlers are lashed to, by the chief's door.</summary>
        const float Post = 0.2f;

        /// <summary>A stag's antlers (0.12 u wide, the white stag's 0.16 with pale tips) on a little hide-bound mount, standing on its base.</summary>
        static Mesh BuildAntlers(bool white)
        {
            var mb = new MeshBuilder(white ? 4266 : 4265) { jitter = 0.01f };
            float s = white ? 1.35f : 1f;
            var bone = white ? new Color(0.96f, 0.95f, 0.9f) : new Color(0.82f, 0.74f, 0.6f);
            var tip = white ? new Color(1f, 0.98f, 0.92f) : new Color(0.9f, 0.85f, 0.74f);
            // a post by the door (a canvas tent narrows to its poles: antlers on the canvas sank into it), the antlers lashed on top
            mb.Prism(Vector3.zero, 0.011f, 0.009f, Post, 6, Palette.Bark, topCol: Palette.WoodLight);
            mb.M = Matrix4x4.Translate(new Vector3(0f, Post, 0f));
            mb.Box(new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.022f, 0.012f), Palette.Bark);
            foreach (int side in new[] { -1, 1 })
            {
                // the main beam out and up, two tines
                mb.Box(new Vector3(side * 0.03f * s, 0.03f * s, 0f), new Vector3(0.008f, 0.07f * s, 0.008f), bone, Quaternion.Euler(0f, 0f, -side * 40f));
                mb.Box(new Vector3(side * 0.055f * s, 0.07f * s, 0f), new Vector3(0.007f, 0.05f * s, 0.007f), tip, Quaternion.Euler(0f, 0f, -side * 12f));
                mb.Box(new Vector3(side * 0.03f * s, 0.06f * s, 0f), new Vector3(0.006f, 0.035f * s, 0.006f), tip, Quaternion.Euler(0f, 0f, side * 18f));
                if (white) mb.Box(new Vector3(side * 0.07f * s, 0.045f * s, 0f), new Vector3(0.006f, 0.03f * s, 0.006f), tip, Quaternion.Euler(0f, 0f, -side * 60f));
            }
            return mb.ToMesh(white ? "WhiteAntlers" : "Antlers");
        }

        /// <summary>The debug panel's "deer hunt now (hit)" / "(miss)": two or three hunters go after the deer from now (deer placed first if
        /// there are none; the hour moves to the morning at night). The planner's own plan, so it plays as in normal play.</summary>
        public static string DebugHuntNow(bool hit)
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            var pv = PeopleView.Find();
            if (sim == null || !sim.EcologyOn || pv == null || pv.Planner == null) return "no living box";
            if (sim.EcoDeerCount == 0 && sim.DebugPlaceNearCamp(AquariumCiv.Sim.Eco.LifeSpecies.Deer) < 0) return "no ground for deer";
            if (pv.Now.hour < 7f || pv.Now.hour > 15.5f) PeopleView.DebugSetHour(8.5f);
            if (!pv.Planner.DebugStartDeerHunt(pv.Now, hit, out var plan)) return "fewer than two can go";
            if (!plan.HasScene) return "no deer to stalk (all at the water, or the day ends first)";
            return $"{plan.hunters.Count} hunters go after the {(plan.targetStag ? "stag" : "doe")} ({(plan.playsHit ? "a hit" : "a miss")}); the shot at {plan.shootStart:0.00} h";
        }

        public string Status()
        {
            string live = lives.Count == 0 ? "no hunt on stage" : string.Join(", ", new List<DeerHuntPlan>(lives.Keys).ConvertAll(p => $"{(p.white ? "white stag" : p.targetStag ? "stag" : "doe")} {(p.playsHit ? "hit" : "miss")}"));
            return $"deer hunt: {live}; arrows {ArrowsShot}/{ArrowsLanded}, set down {SetDowns}, feasts {Feasts}, antlers {(AntlersShown ? (antlersWhite ? "white" : "up") : "no")}";
        }
    }
}
