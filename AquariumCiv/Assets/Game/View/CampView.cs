using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The camp begins (#284 phase 2; keepers-hands §2 beat 2 to 3): a frame of poles stands by the fire with the hides stretched on it
    /// (one per hide drying, they pile up); when the sim raises a tent from hides (<see cref="SimCore.TentRaised"/>) the builders walk to the
    /// frame, take poles and hides, carry them to the lot and raise it, and the tent grows with <see cref="LifePlanner.TentRaised"/>. The first
    /// tent of a living land (<see cref="SimCore.CampBegan"/>) is the small moment: a short camera lean to the lot, and a builder says a line
    /// when the last pole is up (the chronicle card already fired with the gate).
    /// </summary>
    [DefaultExecutionOrder(6)]
    public class CampView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static CampView Instance { get; private set; }

        /// <summary>The line the builder says when the first tent stands (era words: the hides and poles of the camp).</summary>
        public static readonly string[] FirstLines =
        {
            "Poles and hides. A roof, our own.",
            "Look. A tent, from the rabbits' hides.",
        };

        /// <summary>A raising that cannot start (nobody free, no lot) is given up on after this many real seconds: the tent just stands.</summary>
        public const float GiveUpSeconds = 8f;
        /// <summary>The tent grows in steps of this much (each step rebuilds its chunk once).</summary>
        public const float GrowStep = 0.1f;

        BoxView box;
        PeopleView people;
        SimCore boundSim;
        Transform holder, rootSeen;
        GameObject frame;
        int frameKey;

        sealed class Pending { public int id; public bool first, placed; public float waited; }
        readonly List<Pending> pending = new();
        readonly Dictionary<int, TentPlan> active = new();
        readonly Dictionary<int, int> shownStep = new();
        readonly HashSet<int> lineSaid = new();
        readonly List<int> doneBuf = new();
        readonly List<(float x, float z)> points = new();
        double lastAbs = double.NaN;

        /// <summary>A view-clock step bigger than this in one frame (hours) is a jump (a day catching up at midnight after a season
        /// step, a debug hour, a return to the box), not time passing: raisings under way are moved with it (#294).</summary>
        public const double JumpHours = 1.5;
        /// <summary>Raisings carried over a view-clock jump (tests, debug).</summary>
        public int Carried { get; private set; }

        /// <summary>Hides drawn on the frame now, raisings under way, moments played and the last line said (tests, debug).</summary>
        public int HidesShown { get; private set; }
        public bool FrameShown => frame != null && frame.activeSelf;
        public int RaisingsActive => active.Count;
        public int RaisingsStarted { get; private set; }
        public int MomentsPlayed { get; private set; }
        public int Leans { get; private set; }
        public string LastLine { get; private set; }
        public int GaveUp { get; private set; }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        PeopleView People => people != null ? people : people = PeopleView.Find();

        public static CampView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<CampView>();
            return v != null ? v : b.gameObject.AddComponent<CampView>();
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
            SimLayout.BuildingGrow = GrowOf;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (SimLayout.BuildingGrow == (System.Func<int, float>)GrowOf) SimLayout.BuildingGrow = null;
            Bind(null);
            if (holder != null) Destroy(holder.gameObject);
            holder = null; frame = null; pending.Clear(); active.Clear(); shownStep.Clear(); lastAbs = double.NaN;
        }

        void Bind(SimCore sim)
        {
            if (sim == boundSim) return;
            if (boundSim != null) { boundSim.TentRaised -= OnTent; boundSim.CampBegan -= OnCamp; }
            boundSim = sim;
            pending.Clear(); active.Clear(); shownStep.Clear(); lineSaid.Clear();
            if (sim == null) return;
            sim.TentRaised += OnTent;
            sim.CampBegan += OnCamp;
        }

        void OnTent(Building b, bool first)
        {
            if (b == null) return;
            // Hidden from the moment it is built until its planned raising starts (the layout asks GrowOf).
            pending.Add(new Pending { id = b.id, first = first });
        }

        void OnCamp(Building b) { MomentsPlayed++; }

        /// <summary>How much of a building stands: 1, unless its tent is waiting to be raised or is being raised.</summary>
        float GrowOf(int buildingId)
        {
            foreach (var p in pending) if (p.id == buildingId) return 0.02f;
            if (active.TryGetValue(buildingId, out var plan) && People != null && People.Planner != null)
                return Quantize(People.Planner.TentRaised(buildingId, People.Now));
            return 1f;
        }

        static float Quantize(float g) => g >= 0.999f ? 1f : Mathf.Floor(g / GrowStep) * GrowStep;

        void Update()
        {
            var sim = Sim;
            Bind(sim != null && sim.EcologyOn ? sim : null);
            var pv = People;
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null || pv == null || pv.Planner == null) { HideFrame(); return; }
            if (rootSeen != box.Root) { holder = null; frame = null; frameKey = 0; rootSeen = box.Root; }
            var planner = pv.Planner;
            var now = pv.Now;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool refresh = false;

            // #294: the view clock can jump (the day catches up with the sim's month at midnight after a season step; a debug
            // hour; a return at night). A raising planned before the jump would read as long over (or not begun) and the tent
            // would just stand: it is carried over instead, so it plays from where it was, whatever the hour.
            double clockStep = double.IsNaN(lastAbs) ? 0.0 : now.Absolute - lastAbs;
            lastAbs = now.Absolute;
            if (active.Count > 0 && (clockStep > JumpHours || clockStep < -0.01))
            {
                foreach (var plan in active.Values) Shift(plan, clockStep);
                Carried++;
            }

            // raisings waiting for their lot
            for (int i = 0; i < pending.Count; i++)
            {
                var p = pending[i];
                if (!p.placed) { p.placed = true; sim.Settlement?.PlacePending(); } // the lot comes now, not at the month's end (as the shaping show does)
                if (planner.BeginTentRaising(p.id, now, p.first, out var plan))
                {
                    pending.RemoveAt(i--);
                    active[p.id] = plan; shownStep[p.id] = 0;
                    RaisingsStarted++;
                    if (p.first) Lean(sim, plan);
                    refresh = true;
                }
                else if ((p.waited += dt) > GiveUpSeconds)
                {
                    pending.RemoveAt(i--); GaveUp++; refresh = true; // nobody to raise it: it just stands
                }
            }

            // raisings under way: grow the tent, say the first one's line
            doneBuf.Clear();
            foreach (var kv in active)
            {
                var plan = kv.Value;
                float g = Quantize(planner.TentRaised(kv.Key, now));
                int step = Mathf.RoundToInt(g * 100f);
                if (!shownStep.TryGetValue(kv.Key, out int was) || was != step) { shownStep[kv.Key] = step; refresh = true; }
                if (plan.first && now.Absolute >= plan.StartAbsolute + plan.raiseEnd && lineSaid.Add(kv.Key)) SayFirst(sim, plan);
                if (now.Absolute >= plan.EndAbsolute || now.Absolute < plan.StartAbsolute - 0.01) doneBuf.Add(kv.Key);
            }
            foreach (int id in doneBuf) { active.Remove(id); shownStep.Remove(id); refresh = true; }
            if (doneBuf.Count > 0) planner.PruneTents(now);
            if (refresh) box.Refresh();

            UpdateFrame(sim, planner, now);
        }

        /// <summary>Moves a raising by <paramref name="hours"/> (a view-clock jump): its people and the tent are where they were.</summary>
        static void Shift(TentPlan plan, double hours)
        {
            double abs = plan.StartAbsolute + hours;
            long day = (long)System.Math.Floor(abs / 24.0);
            plan.startTime = new ViewTime(day, (float)(abs - day * 24.0));
        }

        // ---------------------------------------------------------------- the moment
        void Lean(SimCore sim, TentPlan plan)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam == null || !GlassVoice.CameraFree()) return;
            // between the lot and the frame, so the hides and the rising tent are both in view
            float x = (plan.lotX + plan.frameX) * 0.5f, z = (plan.lotZ + plan.frameZ) * 0.5f;
            if (cam.NudgeToward(box.transform.TransformPoint(box.land.OnGround(x, z)))) Leans++;
        }

        void SayFirst(SimCore sim, TentPlan plan)
        {
            string line = FirstLines[(sim.State.seed & int.MaxValue) % FirstLines.Length];
            var show = ShowMeDirector.Instance;
            if (show == null) return;
            foreach (var w in plan.workers)
            {
                if (w.isKid || !w.who.IsNotable) continue;
                show.SayLine(line, w.who, 4f);
                LastLine = line;
                return;
            }
            foreach (var w in plan.workers)
            {
                if (w.isKid) continue;
                show.SayLine(line, w.who, 4f);
                LastLine = line;
                return;
            }
        }

        // ---------------------------------------------------------------- the hide frame
        void HideFrame() { if (frame != null) frame.SetActive(false); HidesShown = 0; }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Hide frame") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        void UpdateFrame(SimCore sim, LifePlanner planner, ViewTime now)
        {
            int n = planner.HideFramePoints(now, points);
            bool wanted = n > 0 || sim.HidesTotal > 0 || active.Count > 0 || pending.Count > 0;
            if (!wanted || !planner.HideFrame(out float cx, out float cz, out float dx, out float dz)) { HideFrame(); return; }
            int key = 17;
            key = key * 31 + n;
            key = key * 31 + Mathf.RoundToInt(cx * 200f);
            key = key * 31 + Mathf.RoundToInt(cz * 200f);
            key = key * 31 + Mathf.RoundToInt(dx * 100f);
            if (frame == null || key != frameKey)
            {
                if (frame != null) Destroy(frame);
                frame = BuildFrame(cx, cz, dx, dz, points);
                frameKey = key;
            }
            frame.SetActive(true);
            HidesShown = points.Count;
        }

        /// <summary>The hide rack (#332, replacing #284's bar): an A-frame of crossed poles at each end with a ridge pole, and a pelt hung on
        /// it for each point (hide-shaped, dark edge, cream lacing), with a folded pile at its foot as they come off dry. Built in the box's
        /// own coordinates by <see cref="Pieces.HideRack"/>.</summary>
        GameObject BuildFrame(float cx, float cz, float dx, float dz, List<(float x, float z)> hides)
        {
            var mb = new MeshBuilder(box.seed + 284) { jitter = 0f };
            var land = box.land;
            float halfLen = Mathf.Max(0.12f, HideFrameSpan(hides.Count) * 0.5f + 0.06f);
            float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg; // local +z runs along the bar, +x away from the fire
            var centre = land.OnGround(cx, cz);
            mb.M = Matrix4x4.TRS(centre, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            var inv = mb.M.inverse;
            var zs = new List<float>(hides.Count);
            foreach (var p in hides) zs.Add(inv.MultiplyPoint3x4(land.OnGround(p.x, p.z)).z);
            Pieces.HideRack(mb, halfLen, zs, PileFor(hides.Count), box.seed + 284);
            mb.M = Matrix4x4.identity;
            var go = new GameObject("HideFrame") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Holder(), false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("HideFrame");
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = box.SolidMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        /// <summary>Folded hides at the rack's foot for the hides on it: one for every three, at most three.</summary>
        public static int PileFor(int hidesOnFrame) => Mathf.Clamp(hidesOnFrame / 3, 0, 3);

        static float HideFrameSpan(int n) => Mathf.Max(3, n) * LifePlanner.HideFrameSpacing;
    }
}
