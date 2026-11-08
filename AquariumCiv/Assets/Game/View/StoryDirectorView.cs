// #367 Visit loop B11: the view half of the background-life director (docs/design/animation-specs.md B11; research 17 §4 principle 7,
// §6.7). Every StoryRules.CheckSeconds it looks only inside the camera's view rectangle (inset 10 %): which stories are in progress there
// (a play group's game, a pair beat, a hunt, a draw at the water, pickers at a bush, a staged moment ...), who on view is idle, and what
// is in view to play with (rabbits, the water, a bush). The pure decision (Life/StoryDirector.cs) picks a cheap story near the focus
// and this starts it: a kids' game (KidPlayDirector.Direct), a hand-over (PairStage), a water run or a pick (LifePlanner.StartStoryErrand).
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    [DefaultExecutionOrder(40)]
    public class StoryDirectorView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static StoryDirectorView Instance { get; private set; }

        /// <summary>The cheat "Story density: off / normal / high" (high = twice the rate, for the tester). Static: it outlives a box swap.</summary>
        public static StoryDensity Density = StoryDensity.Normal;

        /// <summary>The view rectangle's inset on each side (B11: inset 10 %).</summary>
        public const float Inset = 0.1f;

        /// <summary>A story this director started: who, and until when (its own clock) it counts as in view while any of them is.</summary>
        sealed class Own { public StoryKind kind; public readonly List<int> members = new List<int>(); public float until; public int id; }

        readonly List<Own> own = new List<Own>();
        readonly StoryWorld world = new StoryWorld();
        readonly Dictionary<long, StoryKind> seen = new Dictionary<long, StoryKind>();
        readonly HashSet<long> seenMoments = new HashSet<long>();
        StoryDirector director;
        SimCore boundSim;
        PeopleView pv;
        float clock, nextLook;
        int ownIds;
        StoryDensity lastDensity = StoryDensity.Normal;

        /// <summary>The decision (tests, the panel).</summary>
        public StoryDirector Director => director;
        /// <summary>The director's clock (real seconds while enabled).</summary>
        public float Clock => clock;
        /// <summary>The last story it started (tests).</summary>
        public StoryPlan LastPlan { get; private set; }
        /// <summary>Why the last look started nothing: the box is busy, or the decision's reason.</summary>
        public string LastWhy { get; private set; } = "";
        /// <summary>The stories counted in view at the last look.</summary>
        public int InViewCount { get; private set; }

        static SimCore Sim
        {
            get
            {
                var c = GameClock.Instance;
                return c != null && c.Sim != null && ReferenceEquals(c.Sim, c.ActiveSim) ? c.ActiveSim : null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<StoryDirectorView>() == null) b.gameObject.AddComponent<StoryDirectorView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            own.Clear();
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (clock < nextLook) return;
            nextLook = clock + StoryRules.CheckSeconds;
            Look();
        }

        /// <summary>One look now (tests and the cheats; <see cref="Update"/> calls it every <see cref="StoryRules.CheckSeconds"/>).
        /// Returns the story it started, or null.</summary>
        public StoryPlan Look()
        {
            var sim = Sim;
            if (!ReferenceEquals(sim, boundSim)) { boundSim = sim; director = null; own.Clear(); }
            if (sim == null) { LastWhy = "no living box on screen"; return null; }
            if (pv == null) pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || pv.Planner == null || cam == null) { LastWhy = "no people or camera"; return null; }
            director ??= new StoryDirector(unchecked(sim.State.seed * 31 + 367));
            if (Density != lastDensity) { lastDensity = Density; director.Reset(); }
            director.Density = Density;
            own.RemoveAll(o => clock > o.until);
            Fill(sim, cam);
            string busy = HeldMomentStage.BusyReason();
            if (busy != null) { LastWhy = busy; return null; }
            world.time = clock;
            director.Density = Density;
            var plan = director.Step(world, TryStart);
            LastWhy = plan == null ? director.LastWhy : "";
            if (plan != null) LastPlan = plan;
            return plan;
        }

        // ---------------------------------------------------------------- what's in view

        bool InView(Camera cam, Vector3 worldPos)
        {
            var vp = cam.WorldToViewportPoint(worldPos);
            return vp.z > 0f && vp.x >= Inset && vp.x <= 1f - Inset && vp.y >= Inset && vp.y <= 1f - Inset;
        }

        Vector3 World(float x, float z) => pv.transform.TransformPoint(new Vector3(x, pv.GroundAt(x, z), z));

        void Fill(SimCore sim, Camera cam)
        {
            world.Clear();
            seen.Clear();
            seenMoments.Clear();
            var planner = pv.Planner;
            var now = pv.Now;
            world.population = sim.State.population;
            world.part = StoryRules.PartOf(now.hour);
            world.waterHours = StoryRules.WaterHours(now.hour);
            // The focus: the ground under the middle of the screen (the box's own plane), box-local.
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var plane = new Plane(pv.transform.up, pv.transform.position);
            var focus = plane.Raycast(ray, out float dist) ? pv.transform.InverseTransformPoint(ray.GetPoint(dist)) : Vector3.zero;
            world.focusX = focus.x; world.focusZ = focus.z;

            float t = pv.PoseClock;
            var acts = pv.Activities;
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (a.hunt != HuntPhase.None) world.huntOn = true;
                if (!pv.TryGetDollFeet(a.who, out var feet) || !InView(cam, feet)) continue;
                int key = a.who.Key;
                var kind = StoryNow(planner, a, now, t, out long id, out bool held, out bool moment);
                if (moment) { seenMoments.Add(id); continue; }
                if (kind != StoryKind.None) { seen[id] = kind; continue; }
                if (held) continue;
                var stage = planner.StageOf(a.who);
                if (!StoryRules.FreeFor(a, stage) || !pv.TryGetDollPosition(a.who, out var at)) continue;
                world.free.Add(new StoryActor
                {
                    key = key, kid = stage == LifeStage.Kid, adult = stage == LifeStage.Adult, x = at.x, z = at.y,
                    freeSeconds = a.end > now.hour ? LifeClock.RealSecondsBetween(now.hour, a.end) : 0f,
                    atFire = a.place.kind == PlaceKind.Fire || a.place == planner.GatherPlace,
                });
            }
            world.storiesInView = seen.Count;
            world.momentsInView = seenMoments.Count; // batch 3: the moments count as one together (StoryWorld.Counted)
            foreach (var k in seen.Values) if (k != StoryKind.Other && !world.kindsInView.Contains(k)) world.kindsInView.Add(k);
            InViewCount = seen.Count + seenMoments.Count;

            var rv = RabbitView.Instance;
            int rid = rv != null ? rv.NearestShown(world.focusX, world.focusZ) : -1;
            if (rid >= 0 && rv.TryGetPosition(rid, out var rp) && InView(cam, World(rp.x, rp.z))) { world.rabbits = true; world.rabbitX = rp.x; world.rabbitZ = rp.z; }
            if (planner.StoryWaterSpot(out float wx, out float wz) && InView(cam, World(wx, wz))) { world.water = true; world.waterX = wx; world.waterZ = wz; }
            if (pv.KidPlay.SplashSpot(new Vector2(world.focusX, world.focusZ), out var edge) && InView(cam, World(edge.x, edge.y))) { world.splash = true; world.splashX = edge.x; world.splashZ = edge.y; }
            if (planner.StoryBushNear(world.focusX, world.focusZ, now.day, out int cell, out float bx, out float bz) && InView(cam, World(bx, bz)))
            { world.bush = true; world.bushCell = cell; world.bushX = bx; world.bushZ = bz; }
            // P9 #424 (B12.w): a ready jar's waiting hint (LifeArrivalView keeps the once-per-visit rule; never the wolves)
            var la = LifeArrivalView.Instance;
            if (la != null && la.HintWanted(sim, out int hs, out int hst, out int hc, out float hx, out float hz))
            { world.hint = true; world.hintSpecies = hs; world.hintStage = hst; world.hintCell = hc; world.hintX = hx; world.hintZ = hz; }
        }

        /// <summary>P9 #424, the cheat "Hint now": the waiting hint for <paramref name="species"/> at <paramref name="stage"/> (1–3) at once,
        /// whatever the director's floor, the visit's rule or the hour says (it still needs idle people near the spot). What happened.</summary>
        public string HintNow(Sim.Eco.LifeSpecies species, int stage)
        {
            var sim = Sim;
            if (sim == null) return "no living box on screen";
            if (pv == null) pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || pv.Planner == null || cam == null) return "no people or camera";
            director ??= new StoryDirector(unchecked(sim.State.seed * 31 + 367));
            var la = LifeArrivalView.Instance;
            if (la == null || !la.HintSpot(sim, species, out int cell, out float x, out float z)) return $"hint {species}: no spot for it";
            Fill(sim, cam);
            world.part = StoryDayPart.Day;
            world.hint = true; world.hintSpecies = (int)species; world.hintStage = Mathf.Clamp(stage, 1, 3); world.hintCell = cell; world.hintX = x; world.hintZ = z;
            var plan = new StoryPlan();
            if (!director.TryCast(world, StoryKind.Hint, plan) && !CastAnyone(plan, (int)species, world.hintStage, cell, x, z))
                return $"hint {species}: nobody awake within {StoryRules.ErrandReach} u of the spot";
            plan.force = true;
            if (!TryStart(plan)) return $"hint {species}: the planner couldn't start it ({hintWhy}; {plan.members.Count} cast)";
            LastPlan = plan;
            return $"hint {species} stage {world.hintStage}: {plan.members.Count} at cell {cell}";
        }

        /// <summary>The story this person is in now (None: none), one id per story. <paramref name="held"/>: in one of the director's own
        /// stories or a pair beat or a game that isn't a story yet (still walking to it): not free either.</summary>
        StoryKind StoryNow(LifePlanner planner, in PersonActivity a, ViewTime now, float t, out long id, out bool held, out bool moment)
        {
            held = false; moment = false;
            int key = a.who.Key;
            foreach (var o in own)
                if (o.members.Contains(key)) { id = -1000L - o.id; held = true; return o.kind; }
            var game = pv.KidPlay.GameOf(key, out var pg);
            if (game != null)
            {
                id = RuntimeHelpers.GetHashCode(game) * 4L + 1;
                return pg switch
                {
                    PlayGame.ChaseRabbits => StoryKind.ChaseRabbits, PlayGame.Ball => StoryKind.Ball, PlayGame.Tag => StoryKind.Tag,
                    PlayGame.Splash => StoryKind.Splash, _ => StoryKind.Other,
                };
            }
            if (PairStage.TryCue(key, t, out var ev, out _))
            {
                id = RuntimeHelpers.GetHashCode(ev) * 4L + 2;
                return ev.kind == PairKind.HandOver ? StoryKind.HandOver : StoryKind.Other;
            }
            if (planner.HeldMomentAt(a.who, now, out var scene, out _, out _, out _))
            {
                id = RuntimeHelpers.GetHashCode(scene) * 4L + 3;
                moment = true;
                return StoryKind.Other;
            }
            return StoryRules.StoryOf(a, out id);
        }

        // ---------------------------------------------------------------- starting one

        static readonly PropKind[] HandThings = { PropKind.Basket, PropKind.Pot, PropKind.Bundle, PropKind.Stick };

        bool TryStart(StoryPlan plan)
        {
            float until;
            switch (plan.kind)
            {
                case StoryKind.ChaseRabbits: case StoryKind.Ball: case StoryKind.Tag: case StoryKind.Splash:
                {
                    var game = plan.kind switch { StoryKind.ChaseRabbits => PlayGame.ChaseRabbits, StoryKind.Ball => PlayGame.Ball, StoryKind.Tag => PlayGame.Tag, _ => PlayGame.Splash };
                    var end = pv.KidPlay.Direct(game, plan.members, new Vector2(plan.x, plan.z), plan.seconds, plan.seed);
                    if (!end.HasValue) return false;
                    until = clock + (end.Value - pv.PoseClock);
                    break;
                }
                case StoryKind.HandOver:
                {
                    if (plan.members.Count < 2) return false;
                    PersonRef G = Ref(plan.members[0]), T = Ref(plan.members[1]);
                    if (!pv.TryGetDollPosition(G, out var gp) || !pv.TryGetDollPosition(T, out var tp)) return false;
                    float t = pv.PoseClock;
                    if (PairStage.TryCue(G.Key, t, out _, out _) || PairStage.TryCue(T.Key, t, out _, out _)) return false;
                    var thing = HandThings[(plan.seed >> 4) % HandThings.Length];
                    var ev = PairStage.HandOver(plan.seed, t, G.Key, T.Key, gp, tp, thing, 0f, StoryRules.HandReach + 0.1f);
                    if (ev == null) return false;
                    PairStage.Add(ev);
                    until = clock + (ev.End - t);
                    break;
                }
                case StoryKind.Hint:
                {
                    if (!pv.Planner.StartStoryHint(plan.hintSpecies, plan.hintStage, plan.members, plan.hintCell, pv.Now, out float secs, out hintWhy, plan.force)) return false;
                    until = clock + secs;
                    LifeArrivalView.Instance?.HintStarted((Sim.Eco.LifeSpecies)plan.hintSpecies, plan.hintStage, plan.members.Count, secs, plan.hintCell);
                    break;
                }
                case StoryKind.WaterRun: case StoryKind.Pick:
                {
                    if (plan.members.Count < 1) return false;
                    if (!pv.Planner.StartStoryErrand(plan.kind, Ref(plan.members[0]), pv.Now, plan.bushCell, out float secs, out _)) return false;
                    until = clock + secs;
                    break;
                }
                default:
                    return false;
            }
            var o = new Own { kind = plan.kind, until = until, id = ++ownIds };
            o.members.AddRange(plan.members);
            own.Add(o);
            return true;
        }

        /// <summary>The cheat's cast when nobody near is idle: the nearest grown-ups and kids awake, whatever they are doing (the planner
        /// still leaves out sleepers, hunters and people in a moment).</summary>
        bool CastAnyone(StoryPlan plan, int species, int stage, int cell, float x, float z)
        {
            StoryRules.HintCast(species, stage, out int adults, out int kids);
            var who = new List<PersonRef>(); var xs = new List<float>(); var zs = new List<float>();
            pv.StandingNow(who, xs, zs);
            var order = new List<int>();
            for (int i = 0; i < who.Count; i++) order.Add(i);
            order.Sort((a, b) => ((xs[a] - x) * (xs[a] - x) + (zs[a] - z) * (zs[a] - z)).CompareTo((xs[b] - x) * (xs[b] - x) + (zs[b] - z) * (zs[b] - z)));
            plan.members.Clear();
            int na = 0, nk = 0;
            foreach (int i in order)
            {
                var st = pv.Planner.StageOf(who[i]);
                if (st == LifeStage.Adult && na < adults + 4) { plan.members.Insert(na++, who[i].Key); } // spares: the planner takes the first free ones
                else if (st == LifeStage.Kid && nk < kids + 3) { plan.members.Add(who[i].Key); nk++; }
            }
            if (plan.members.Count == 0) return false;
            plan.kind = StoryKind.Hint; plan.seconds = StoryRules.Seconds(StoryKind.Hint);
            plan.x = x; plan.z = z; plan.hintSpecies = species; plan.hintStage = stage; plan.hintCell = cell;
            return true;
        }

        string hintWhy = "";

        static PersonRef Ref(int key) => key > 0 ? PersonRef.Notable(key) : PersonRef.Crowd(-key - 1);

        // ---------------------------------------------------------------- the panel (#367)

        /// <summary>The panel's status line: "stories in view: n, next in s" and what it started last or why not.</summary>
        public string Status()
        {
            if (director == null) return $"stories ({Density}): not looking yet ({LastWhy})";
            float next = director.NextIn - (clock - director.LastLook);
            string nextS = float.IsInfinity(next) ? "—" : $"{Mathf.Max(0f, next):0}s";
            return $"stories ({Density}): in view {InViewCount}, next in {nextS}; started {director.Started}, last {director.LastKind}" +
                   (string.IsNullOrEmpty(LastWhy) ? "" : $" ({LastWhy})");
        }

        /// <summary>The cheat: off → normal → high → off. Off ends the errands it started (they walk back into their day).</summary>
        public static string CycleDensity()
        {
            Density = Density == StoryDensity.Off ? StoryDensity.Normal : Density == StoryDensity.Normal ? StoryDensity.High : StoryDensity.Off;
            var i = Instance;
            if (Density == StoryDensity.Off && i != null && i.pv != null && i.pv.Planner != null) i.pv.Planner.EndLifeStories();
            if (i != null) i.nextLook = 0f; // look now
            return "story density: " + Density;
        }

        public static string DebugStatus() => Instance != null ? Instance.Status() : "no story director";
    }
}
