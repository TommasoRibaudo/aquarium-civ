using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;
using LifeProp = AquariumCiv.Life.Prop;
using LifeSlot = AquariumCiv.Life.PropSlot;

namespace AquariumCiv.View
{
    /// <summary>
    /// The people in the box doing things (#30, life-in-the-box §9): one peg doll per person the activity planner (#29)
    /// knows, notables first, then the crowd, up to <see cref="maxPeople"/>. Every frame each doll walks the planner's
    /// route (<see cref="LifePlanner.PositionAt"/>: the worn paths), takes its slot at the place (a ring round the fire
    /// or the green, a spread in a field, a half-ring at a door), faces its partner, its work or the fire, and plays its
    /// activity as a procedural pose (<see cref="DollMotion"/>) with its prop in the right slot. Role holders wear their
    /// role's colour (a wide sash and a headband, <see cref="Palette.RoleAccent"/>); notables a thin sash.
    /// <para>
    /// The view day: one view day per sim month (its number is the month, taken at midnight: <see cref="LifeClock.NextViewDay"/>),
    /// the hour runs on <see cref="LifeClock"/>'s uneven ~5-minute day from <see cref="startHour"/>. The planner is asked for everyone every
    /// <see cref="replanSeconds"/>; positions and poses every frame. With this component on the box, the
    /// <see cref="BoxView"/> drops the layout's standing people (#33's placeholders).
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxView))]
    public class PeopleView : MonoBehaviour
    {
        [Tooltip("Dolls drawn at most (life-in-the-box §9 budget: ~60).")]
        public int maxPeople = SimLayout.MaxPeople;
        [Tooltip("The view hour a session starts at (the planner's clock, 0–24).")]
        public float startHour = 9f;
        [Tooltip("View-clock speed (1 = the ~5-minute day).")]
        public float timeScale = 1f;

        /// <summary>#146: how much bigger people are at full view (debug panel; 0 = their real size everywhere).</summary>
        public static float DollScaleAmount = AquariumCiv.Lighting.CameraReach.DollScaleUp;
        BoxCamera rig;
        float dollZoomScale = 1f;
        /// <summary>#361: the dolls' zoom factor, which the berry and root arcs share (animation-specs §0.1).</summary>
        public float DollZoom => dollZoomScale;
        public float replanSeconds = 0.2f;
        [Tooltip("Animate in edit mode (otherwise edit mode only updates when the Editor repaints).")]
        public bool animateInEditMode;
        /// <summary>A test steps <see cref="Tick"/> itself on a fixed frame time, so <c>Update</c> keeps out of it.</summary>
        [System.NonSerialized] public bool HandDriven;

        public ViewTime Now => now;
        /// <summary>The hour the box shows, also before the first tick (<see cref="Now"/> is midnight then).</summary>
        public float ViewHour => clockStarted ? now.hour : startHour;
        public LifePlanner Planner => planner;
        public IReadOnlyList<PersonActivity> Activities => acts;
        /// <summary>Average milliseconds of this component's frame work (planning + all dolls), for the perf budget (≤ 0.5 ms).</summary>
        public double FrameMs { get; private set; }

        sealed class Doll
        {
            public PegDollView view;
            public uint sig;
            public Vector2 pos;
            public float yaw, speed, phase;
            public bool placed, wasHidden;
            public PersonActivity act;
            /// <summary>The place this person holds a slot at, and which (stable while they stay, rule 6).</summary>
            public Place slotPlace;
            public int slot = -1;
            /// <summary>Their place in line at this kind of spot at a home (<see cref="SlotSpots.Home"/>).</summary>
            public int homeRank;
            public Vector2 lastSpot;
            /// <summary>#189: the planned point (<see cref="pos"/>) stays the plan's; the doll is drawn at <see cref="drawn"/>, eased aside
            /// by <see cref="offset"/> and kept off the solids. <see cref="heading"/> is the way the plan walks, its index this frame's in the crowd.</summary>
            public Vector2 drawn, offset, heading;
            public int crowdIndex = -1;
            /// <summary>The pose drawn last frame and the move it was (#203: a change of move blends over <see cref="BlendSeconds"/>, from <see cref="blendFrom"/>).</summary>
            public DollPose pose, blendFrom;
            public DollStyle drew;
            public float blendT = BlendSeconds;
            /// <summary>#203: the walk's phase, added up frame by frame; the way the doll goes, smoothed, which it faces; and whether it counts as walking.</summary>
            public float stride;
            public Vector2 velocity;
            public bool walking;
            /// <summary>#355: the pose's hit, as a once-per-contact edge for <see cref="WorkHit"/>.</summary>
            public HitEdge hitEdge;
            /// <summary>#361: the pose clock the berry beats loop on (<see cref="TryGetBerryDoll"/>).</summary>
            public float poseClock;
            /// <summary>#363: who they last stood talking with, and when (a couple leaving together walks together, see <see cref="Mates"/>).</summary>
            public int mateKey;
            public float mateTime = -999f;
            /// <summary>#361: when the tip at the stash began (the first frame the doll stood still at the pile): the beat runs from there.</summary>
            public float tipStart;
        }

        BoxView box;
        SimLayoutSource src;
        LifePlanner planner;
        SimCore plannerSim;
        Transform peopleRoot;
        ViewTime now;
        bool clockStarted;
        /// <summary>This frame every doll is put straight where its plan says (a clock or box jump), else they walk there.</summary>
        bool snapAll;
        /// <summary>A sim running ahead of the view (the first year's fast clock): the dolls walk to the new day's plans instead
        /// of snapping there at every skip.</summary>
        public bool SoftDayTurns;
        float replanIn, lastTime = -1f;
        /// <summary>The poses' clock: seconds this view has run, so a pose moves with the frames it is stepped by.</summary>
        float poseClock;
        readonly List<PersonActivity> acts = new();
        /// <summary>#362: the thrown spear, and the hunt step the doll being driven is in (set by <see cref="HuntSpot"/>).</summary>
        readonly HuntStage huntStage = new();
        HuntStep huntStep; HuntPlan huntPlan;
        /// <summary>#363: the hunt plan each hunter was last handed a spear for (once per plan).</summary>
        readonly Dictionary<int, HuntPlan> spearHanded = new Dictionary<int, HuntPlan>();
        public HuntStage HuntStage => huntStage;
        readonly Dictionary<int, Doll> dolls = new();
        readonly Dictionary<Place, int> countAt = new();
        readonly HashSet<(Place, int)> taken = new();
        readonly Dictionary<(Place, SlotSpots.HomeSpot), int> homeRanks = new();
        /// <summary>Where the dolls placed so far this frame stand (rule 6: one doll per spot, also after a slot moved off a lot).</summary>
        readonly List<Vector2> claimed = new();
        /// <summary>Pieces standing on open ground (a well by the fire): rings and spreads keep off them.</summary>
        readonly List<(Vector2 c, float r)> blockers = new();
        /// <summary>#189: who stands where this frame, and what nobody walks through (the fire, the stash pile).</summary>
        readonly CrowdAvoid crowd = new();
        readonly List<Solid> solids = new();
        /// <summary>#189: how much faster than the plan the drawn doll may catch up with where it should be.</summary>
        const float DrawnSlack = 0.35f;
        /// <summary>Tester run 9: catching up with the plan: the least extra speed, and how it grows with the gap (per second) on the way and into a slot.</summary>
        const float CatchLeast = 0.03f, CatchWalking = 1f, CatchArriving = 3f;
        /// <summary>Tester run 9: under this speed a doll hasn't really got a way to face, and turns at <see cref="SlowTurn"/> at the most (degrees a second).</summary>
        const float FaceFrom = 0.2f, SlowTurn = 110f;
        /// <summary>#203: how fast the way a walker goes settles (per second), how fast a doll turns to face it, and the fastest it turns (degrees a second).</summary>
        const float HeadingRate = 8f, VelocityRate = 10f, TurnRate = 9f, MostTurn = 300f;
        /// <summary>#203: slower than this a doll isn't walking, faster than that it is: a doll easing onto its spot doesn't flick between the two.</summary>
        const float WalkStops = 0.03f, WalkStarts = 0.05f;
        /// <summary>#203: how long one move takes to become the next.</summary>
        const float BlendSeconds = 0.16f;
        /// <summary>#189: off, walkers go where the plan says (debug panel; the perf test compares the two).</summary>
        public static bool AvoidCrowds = true;
        const float Personal = 0.075f; // closest two standing dolls get, centre to centre
        /// <summary>#150: past the pile's edge, what rings keep off it; and past that edge, where someone stands to set a load down.</summary>
        const float StashBlockMargin = 0.02f, StashStandOff = 0.04f;
        readonly Dictionary<int, Lot> lotOfBuilding = new();
        readonly Dictionary<int, Notable> notables = new();
        readonly HashSet<int> seen = new();
        readonly List<int> gone = new();
        long lotsMonth = long.MinValue;
        int lotsCount = -1;
        double msAvg;

        BoxView Box => box != null ? box : box = GetComponent<BoxView>();
        SimLayoutSource Source => src != null ? src : src = GetComponent<SimLayoutSource>();

        public static PeopleView Find() => FindAnyObjectByType<PeopleView>();

        void OnEnable()
        {
            if (Box != null && Source != null) Box.PlannedPeople = true; // no sim (a demo layout): its people stay
            replanIn = 0f;
            lastTime = -1f;
        }

        void OnDisable()
        {
            if (box != null) box.PlannedPeople = false;
            foreach (var d in dolls.Values) if (d.view != null) Kill(d.view.gameObject);
            dolls.Clear();
            kidPlay?.Clear(); // #358
            // DontSave: like BoxView's root, a deferred Destroy can be lost on the way into play mode (BoxView.OrphanRoots).
            if (peopleRoot != null) DestroyImmediate(peopleRoot.gameObject);
            peopleRoot = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void Update()
        {
            if (HandDriven) return;
            float t = Time.realtimeSinceStartup;
            float dt = lastTime < 0f ? 0f : Mathf.Clamp(t - lastTime, 0f, 0.25f);
            lastTime = t;
            if (!Application.isPlaying && !animateInEditMode && clockStarted) dt = 0f;
            Tick(dt);
#if UNITY_EDITOR
            if (!Application.isPlaying && animateInEditMode) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        /// <summary>One frame: advance the view clock by <paramref name="dt"/> real seconds, replan if due, move and pose
        /// every doll. Public for tests and edit-mode renders.</summary>
        public void Tick(float dt)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sim = Source != null ? Source.Sim : null;
            if (sim == null || Box == null || Box.Root == null) return;
            Box.PlannedPeople = true;
            if (rig == null && Camera.main != null) rig = Camera.main.GetComponent<BoxCamera>();
            dollZoomScale = rig != null ? AquariumCiv.Lighting.CameraReach.DollScale(rig.zoom, DollScaleAmount) : 1f;
            bool newSim = planner == null || plannerSim != sim;
            if (newSim) { planner = LifePlanner.Wired(sim); plannerSim = sim; replanIn = 0f; snapAll = clockStarted; }

            // The view day turns at midnight, not when the sim's month ticks mid-day (that swapped everyone's plan and the
            // dolls jumped, #30); a sim that jumped ahead (a catch-up, a skip) or another box is taken at once, in place.
            float hour = clockStarted ? now.hour : startHour;
            HuntSlowMo.Tick(this, now); // #362: the "Slow-mo next hunt" cheat
            var next = LifeClock.Advance(new ViewTime(0, hour), dt * Mathf.Max(0f, timeScale));
            long day = clockStarted && !newSim ? LifeClock.NextViewDay(now.day, next.day, sim.State.month) : sim.State.month;
            if (clockStarted && day != now.day && next.day == 0 && !SoftDayTurns) snapAll = true;
            now = new ViewTime(day, next.hour);
            clockStarted = true;

            replanIn -= dt;
            if (replanIn <= 0f || acts.Count == 0)
            {
                replanIn = replanSeconds;
                Replan(sim);
            }
            Animate(sim, dt);
            snapAll = false;
            msAvg = msAvg <= 0 ? sw.Elapsed.TotalMilliseconds : msAvg * 0.95 + sw.Elapsed.TotalMilliseconds * 0.05;
            FrameMs = msAvg;
        }

        void Replan(SimCore sim)
        {
            planner.PlanAll(now, acts);
            if (acts.Count > maxPeople) acts.RemoveRange(maxPeople, acts.Count - maxPeople);
            // Slots that stay put: the planner numbers a place's slots in person order, so someone leaving would shift
            // everyone after them round the ring. Keep each person's slot while they stay; a newcomer takes the lowest
            // free one. countAt holds the slots in use + 1 (the ring's spacing).
            countAt.Clear();
            taken.Clear();
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (a.moving || !dolls.TryGetValue(a.who.Key, out var d) || d.slotPlace != a.place || d.slot < 0) continue;
                taken.Add((a.place, d.slot));
            }
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (!dolls.TryGetValue(a.who.Key, out var d)) { d = new Doll { phase = Hash01(a.who.Key, 5) * 37f }; dolls[a.who.Key] = d; }
                if (a.moving) { d.slotPlace = Place.None; d.slot = -1; continue; }
                if (d.slotPlace != a.place || d.slot < 0)
                {
                    int s = 0;
                    while (taken.Contains((a.place, s))) s++;
                    taken.Add((a.place, s));
                    d.slotPlace = a.place; d.slot = s;
                }
                a.slot = d.slot;
                acts[i] = a;
                countAt.TryGetValue(a.place, out int c);
                countAt[a.place] = Mathf.Max(c, d.slot + 1);
            }
            // #200: who's where round each home. Only those on view count: someone indoors holds no spot at the door.
            homeRanks.Clear();
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                if (a.moving || a.place.kind != PlaceKind.Home || DollMotion.StyleOf(a.kind) == DollStyle.Hidden || !dolls.TryGetValue(a.who.Key, out var d)) continue;
                var at = (a.place, SlotSpots.SpotFor(a.kind));
                homeRanks.TryGetValue(at, out int rank);
                homeRanks[at] = rank + 1;
                d.homeRank = rank;
            }
            notables.Clear();
            foreach (var n in sim.State.notables) if (n.alive) notables[n.id] = n;
            blockers.Clear();
            solids.Clear();
            foreach (var pc in Box.layout.pieces)
            {
                if (pc.kind == PieceKind.Fire) solids.Add(new Solid(pc.Pos.x, pc.Pos.y, CrowdAvoid.FireRadius));
                if (pc.kind != PieceKind.Fire && pc.kind != PieceKind.LogSeat)
                    blockers.Add((pc.Pos, Mathf.Min(pc.w, pc.d) * 0.5f + 0.05f));
            }
            if (planner.TryGetStash(out var pile))
            {
                blockers.Add((new Vector2(pile.x, pile.z), CampStash.PileExtent + StashBlockMargin)); // #150
                solids.Add(new Solid(pile.x, pile.z, CampStash.PileExtent));
            }
            var st = sim.Settlement;
            int lots = st != null ? st.Lots.Count : 0;
            if (sim.State.month != lotsMonth || lots != lotsCount)
            {
                lotsMonth = sim.State.month; lotsCount = lots;
                lotOfBuilding.Clear();
                if (st != null)
                    foreach (var l in st.Lots)
                        if (l.buildingId > 0 && (!lotOfBuilding.TryGetValue(l.buildingId, out var old) || (old.retired && !l.retired))) lotOfBuilding[l.buildingId] = l;
            }
        }

        // ---------------- dolls ----------------
        void Animate(SimCore sim, float dt)
        {
            if (peopleRoot == null)
            {
                peopleRoot = new GameObject("Planned People") { hideFlags = HideFlags.DontSave }.transform;
                peopleRoot.SetParent(transform, false);
            }
            bool camp = sim.State.era == Era.Camp;
            poseClock += dt;
            float time = poseClock;
            KidPlay.Step(planner, acts, now, time, peopleRoot); // #358: the play groups
            seen.Clear();
            claimed.Clear();
            // #189: everyone on view where they were drawn last frame, for the walkers to keep clear of (nobody goes round a sleeper they can't see, #203).
            crowd.Clear();
            for (int i = 0; i < acts.Count; i++)
                if (dolls.TryGetValue(acts[i].who.Key, out var c))
                    c.crowdIndex = AvoidCrowds && c.placed && !c.wasHidden && !c.pose.hidden ? crowd.Add(acts[i].who.Key, c.drawn.x, c.drawn.y, acts[i].moving, c.heading.x, c.heading.y) : -1;
            crowd.Build();
            for (int i = 0; i < acts.Count; i++)
            {
                var a = acts[i];
                int key = a.who.Key;
                seen.Add(key);
                var stage = planner.StageOf(a.who);
                Notable n = null;
                if (a.who.IsNotable) notables.TryGetValue(a.who.notableId, out n);
                string role = n != null ? sim.RoleOf(n) : null;
                bool kid = stage == LifeStage.Kid || stage == LifeStage.Baby;
                var look = LookOf(a.who, n, role, camp, kid);
                uint sig = LookSig(look, kid);
                if (!dolls.TryGetValue(key, out var d)) { d = new Doll { phase = Hash01(key, 5) * 37f }; dolls[key] = d; }
                if (d.view == null || d.sig != sig)
                {
                    if (d.view != null) Kill(d.view.gameObject);
                    d.view = PegDollView.Create(peopleRoot, Box.SolidMaterial, look, kid, Seed(a.who));
                    d.view.name = Label(a.who, n, role);
                    d.sig = sig;
                }
                d.act = a;
                Drive(d, a, stage, dt, time);
            }
            // #362: the thrown spear flies and stands in the ground
            huntStage.Tick(planner, now, peopleRoot, Box.SolidMaterial,
                who => dolls.TryGetValue(who.Key, out var hd) && hd.view != null ? hd.view.prop : null, (hx, hz) => Box.SurfaceHeight(hx, hz));
            KidPlay.DrawBalls(peopleRoot, Box.SolidMaterial, dollZoomScale); // #358
            gone.Clear();
            foreach (var kv in dolls) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int k in gone) { if (dolls[k].view != null) Kill(dolls[k].view.gameObject); dolls.Remove(k); }
        }

        void Drive(Doll d, PersonActivity a, LifeStage stage, float dt, float time)
        {
            var v = d.view;
            var land = Box.land;
            bool hidden = stage == LifeStage.Baby || a.kind == ActivityKind.None; // babies are never walkers: a carer's doll shows them (#192, §4 "Everyone")
            float x = 0f, z = 0f;
            Vector2? huntFace = null;
            bool hunting = a.hunt != HuntPhase.None && HuntSpot(a, out x, out z, out huntFace);
            bool tenting = !hunting && ((a.tent != TentPhase.None && TentSpot(a, out x, out z, out huntFace)) // #284: the planned walk to the frame and the lot
                || (StagedSpot != null && StagedSpot(a, now, out x, out z, out huntFace))); // #370: a staged moment's own spots (the aisle, the ring, the glass) ride the same path
            if (hunting || tenting) hidden = false;
            else if (!planner.PositionAt(a.who, now, out x, out z) && !planner.Positions.TryGetPosition(a.place, out x, out z)) hidden = true;
            var spot = new Vector2(x, z);
            var target = spot;
            Vector2? face = null;
            if (hunting || tenting) { face = huntFace; claimed.Add(target); }
            else if (!a.moving && !a.enRoute && !hidden && pins.TryGetValue(a.who.Key, out var pin)) // #224: the choice scene stands them on their own spot
            {
                target = pin; // the scene chose it clear of the others
                face = target + GlassNormal(planner.GlassSide);
                claimed.Add(target);
            }
            else if (!a.moving && !a.enRoute && !hidden) // #197: a run-in stops on the path, it does not take a slot at a place
            {
                target = SlotSpot(d, a, spot, out face);
                bool yard = a.place.kind == PlaceKind.Home && (target - spot).sqrMagnitude > SlotSpots.AtDoorReach * SlotSpots.AtDoorReach;
                target = Standable(target, spot, !InField(a.place), yard);
                claimed.Add(target);
                if (yard && lotOfBuilding.TryGetValue(a.place.buildingId, out var yardLot))
                {
                    var from = new Vector2(yardLot.x, yardLot.z);
                    face = target + (target - from).normalized; // moved round a corner, still looking away from the house
                }
            }
            // #358: a play group has them: its game says where they stand, what they face and the move (KidPlayDirector).
            PlayCue playCue = default; KidGame playGame = null; int playIx = -1;
            bool playing = !hidden && !hunting && !tenting && KidPlay.TryCue(a.who.Key, time, out playCue, out playGame, out playIx);
            if (playing) { target = playCue.pos; face = playCue.face; }
            // #363 B7: a pair moment has them (hand over, walk together, hug): the pair's shared clock says where they stand and what they face.
            if (hunting && a.hunt == HuntPhase.TakeSpear) HandSpear(d, a, time);
            PairCue pairCue = default; PairEvent pairEv = null;
            bool paired = !hidden && !playing && !tenting && PairStage.TryCue(a.who.Key, time, out pairEv, out pairCue);
            if (paired) { target = pairCue.pos; face = pairCue.face; }
            if (a.kind != ActivityKind.Swim && a.kind != ActivityKind.Splash && !(playing && playCue.sink > 0f) /* #359: wading in on purpose */ && land.IsWater(target.x, target.y))
            {
                var wet = target;
                target = Ashore(land, target);
                if (!a.moving) face = wet; // the fisher faces the water
            }
            // #360: drawing water: kneel at the real water's edge, facing the water (the slot's side of it), not in the middle of the pond.
            bool drawing = a.kind == ActivityKind.FetchWater && !a.moving && !a.enRoute && !hidden && !hunting && !tenting && a.place.kind is PlaceKind.Pond or PlaceKind.Stream or PlaceKind.Puddle;
            if (drawing) target = DrawStand(land, a.place.kind, spot, target, a.slot, out face);
            // #364: fishing: the fisher stands 0.06 u back from the real water edge facing the water (the water run's spot), so the line's end is on the water
            bool fishing = a.kind == ActivityKind.Fish && !a.moving && !a.enRoute && !hidden && !hunting && !tenting && !playing && a.place.kind == PlaceKind.Pond;
            if (fishing) target = DrawStand(land, PlaceKind.Pond, spot, target, a.slot, out face);
            if (!hunting && !tenting && !a.moving && !drawing && !fishing && !playing /* #359 */ && a.place.kind is PlaceKind.Pond or PlaceKind.Stream) face = land.pond;
            if (!hunting && !tenting && !a.moving && lookAt.Count > 0 && lookAt.TryGetValue(a.who.Key, out var lookTo)) face = lookTo; // #282: turned to the first rabbit
            if (!hunting && !tenting && !a.moving && a.facesGlass && a.place.kind != PlaceKind.Glass) face = target + GlassNormal(planner.GlassSide); // #92: a dance for the sky, a wave; at the glass itself SlotSpot turns each one a little (#190)
            float rim = !a.moving && a.place.kind == PlaceKind.Glass ? 0.05f : 0.12f; // #92: right up at the glass
            target.x = Mathf.Clamp(target.x, -Land.HX + rim, Land.HX - rim);
            target.y = Mathf.Clamp(target.y, -Land.HZ + rim, Land.HZ - rim);

            // Chase the target: walks are continuous already (the planner's route); taking a slot on arrival glides at a
            // walking pace, never faster than the walk itself. Unseen (indoors) or a clock jump: just be there. A plan that
            // changed under them (the month ticked: a new home) is walked to at the walking pace, never
            // jumped (#30): the planner's point moving faster than any walk is such a change, not a walk to keep up with.
            var prev = d.pos;
            // How fast the planner's own point moves (the walk), not the slot target, which steps on arrival.
            float pace = LifePlanner.RealWalkSpeed * Mathf.Max(1f, timeScale);
            float targetSpeed = dt > 0f && d.placed ? (spot - d.lastSpot).magnitude / dt : 0f;
            if (targetSpeed > pace * 3f) targetSpeed = pace;
            d.lastSpot = spot;
            bool jump = !d.placed || d.wasHidden || dt <= 0f || snapAll;
            if (jump) d.pos = target;
            else if (playing) d.pos = Vector2.MoveTowards(d.pos, target, Mathf.Min(0.75f, playCue.speed * 1.15f + 0.15f + (target - d.pos).magnitude * 3f) * dt); // #358: the game's pace (a run in tag), catching up fast
            else if (paired) d.pos = Vector2.MoveTowards(d.pos, target, Mathf.Min(0.75f, pairCue.speed * 1.1f + (target - d.pos).magnitude * 1.5f) * dt); // #363: the pair's pace
            else
            {
                // Into a slot: straight if the way is clear, else by the place's middle (no cutting across a house corner).
                var goal = a.moving || Clear(d.pos, target) ? target : spot;
                float gap = (goal - d.pos).magnitude;
                // Tester run 9: most of a town's hard brakes were this, not the crowd. Off a slot a walker ran at twice the walk to reach the plan's point, then dropped to the walk
                // on reaching it. Now the catching up fades as they near it, and they settle into the walk.
                d.pos = Vector2.MoveTowards(d.pos, goal, (targetSpeed + Mathf.Min(DrawnSlack, CatchLeast + gap * (a.moving ? CatchWalking : CatchArriving))) * dt);
            }
            d.wasHidden = hidden;
            float inst = dt > 0f && d.placed ? (d.pos - prev).magnitude / dt : 0f;
            var drawnWas = d.drawn;
            StepDrawn(d, a, jump, hidden, prev, inst, dt);
            if (!jump) inst = (d.drawn - drawnWas).magnitude / dt; // the legs follow the doll drawn: one still easing back to its plan walks there
            d.speed = Mathf.Lerp(d.speed, inst, dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 1f);
            d.placed = true;

            if (d.speed > WalkStarts) d.walking = true; else if (d.speed < WalkStops) d.walking = false;
            bool walking = d.walking;
            var style = DollMotion.StyleOf(a.kind);
            // #362: a crouched creep (0.1 u/s) is faster than the walk threshold, but it is a stalk, not a walk
            bool keepsHunchStyle = hunting && a.hunt is HuntPhase.Stalk or HuntPhase.Strike or HuntPhase.Bolt or HuntPhase.PullSpear or HuntPhase.PickUp or HuntPhase.HoldUp;
            if (walking && style != DollStyle.Hop && !keepsHunchStyle) style = a.kind == ActivityKind.RunOver || (stage == LifeStage.Kid && a.kind == ActivityKind.Play) ? DollStyle.Run : DollStyle.Walk;
            if (hidden) style = DollStyle.Hidden;
            if (!walking && !hidden) style = BerryMotion.Retarget(style, a.kind, a.place.kind); // #361: berries and roots you can see
            if (playing) style = playCue.style; // #358
            if (paired && !walking && pairEv.kind != PairKind.WalkTogether) style = DollStyle.Stand; // #363: the beat is the whole pose (no seated knapper under a hand-over)
            if (!a.moving && !a.partner.IsNone && a.kind is ActivityKind.Chat or ActivityKind.Court or ActivityKind.SitTogether or ActivityKind.Eat or ActivityKind.Visit)
            { d.mateKey = a.partner.Key; d.mateTime = time; } // #363: who they last stood with

            // Props.
            var (kind, slot) = PropOf(a.prop, a.PropSlot, a.kind, a.water);
            // #360: the bucket comes up full at the start of the lift (the plan's block keeps the empty one until it walks home).
            if (kind == PropKind.BucketEmpty && a.kind == ActivityKind.FetchWater && !a.moving && a.end > a.start &&
                WaterBeats.Full(LifeClock.RealSecondsBetween(a.start, Mathf.Clamp(now.hour, a.start, a.end)), a.who.Key, a.slot))
                kind = a.water == WaterKind.Muddy ? PropKind.BucketMuddy : PropKind.Bucket;
            // #364: a fisher's block runs on its own seconds (the cheat's shift on top) and what is left of it: the bites, the yank, the end beat (FishBeats);
            // in the end beat the line is in and the last catch is held up on the stick.
            float fishS = 0f, fishLeft = 99f;
            bool fishNow = style == DollStyle.Fish && a.kind == ActivityKind.Fish && !a.moving && !hidden;
            if (fishNow)
            {
                float at = a.end > a.start ? Mathf.Clamp(now.hour, a.start, a.end) : a.start;
                fishS = LifeClock.RealSecondsBetween(a.start, at) + FishingView.ShiftOf(a.who.Key);
                fishLeft = a.end > a.start ? LifeClock.RealSecondsBetween(at, a.end) : 99f;
                if (FishBeats.HoldsFish(a.who.Key, fishS, fishLeft)) (kind, slot) = (PropKind.Fish, PropSlot.Hand);
            }
            if (hunting) (kind, slot) = HuntProp(a, kind, slot);   // #362: the spear leaves the hand; the catch is lifted up
            if (paired && pairCue.propMode == 1) (kind, slot) = (pairCue.prop, pairCue.slot); // #363: the handed thing is in exactly one pair of hands
            if (playing && !(kind == PropKind.Stick && playGame is TagGame)) kind = PropKind.None; // #358: the group's ball is the director's; a stick is kept for tag
            if (playing && playCue.stick) { kind = PropKind.Stick; slot = PropSlot.Hand; } // #359: the stick duel
            if (v.propKind != kind || v.propSlot != slot || (kind != PropKind.None && v.prop == null)) v.SetProp(kind, slot);
            v.HoldInLeft(BerryMotion.BasketInLeft(style)); // #361: the right hand picks, the basket rides in the left

            // Facing: along the walk, else the partner, the ring's centre, the work or the water.
            float want = d.yaw;
            if (jump || dt <= 0f) d.velocity = Vector2.zero;
            else d.velocity = Vector2.Lerp(d.velocity, (d.drawn - drawnWas) / dt, 1f - Mathf.Exp(-dt * VelocityRate));
            if (playing) { if ((face.Value - d.pos).sqrMagnitude > 1e-5f) want = Yaw(d.pos, face.Value); } // #358: the game says what they face, running or not
            else if (paired && !walking && face.HasValue) { if ((face.Value - d.pos).sqrMagnitude > 1e-5f) want = Yaw(d.pos, face.Value); } // #363
            else if (walking && !((hunting || tenting) && huntFace.HasValue)) { if (d.velocity.sqrMagnitude > 0.05f * 0.05f) want = Yaw(Vector2.zero, d.velocity); }
            else
            {
                if (!a.partner.IsNone && dolls.TryGetValue(a.partner.Key, out var p) && p.placed && (p.pos - d.pos).sqrMagnitude < 0.6f * 0.6f && p.pos != d.pos)
                    want = Yaw(d.pos, p.pos);
                else if (face.HasValue && (face.Value - d.pos).sqrMagnitude > 1e-5f) want = Yaw(d.pos, face.Value);
            }
            float most = walking || a.moving ? Mathf.Lerp(SlowTurn, MostTurn, d.speed / FaceFrom) : MostTurn; // nearly stopped in a crowd, a body doesn't spin
            d.yaw = dt > 0f ? d.yaw + Mathf.Clamp(Mathf.DeltaAngle(d.yaw, want) * (1f - Mathf.Exp(-dt * TurnRate)), -most * dt, most * dt) : want;

            // Pose: the apprentice copies the master a beat late (§4).
            float phase = time + d.phase - (a.copiesPartner ? 0.35f : 0f);
            d.poseClock = phase; // #361
            if (!a.partner.IsNone && !a.copiesPartner && a.kind is ActivityKind.Chat or ActivityKind.Argue) phase += a.who.Key < a.partner.Key ? 0f : 0.65f;
            int steps = ActivityInfo.LoopSteps(a.kind);
            // #92: a story told once (the lens look, the tap-back's knocks) runs on real seconds into the activity.
            float since = a.end > a.start ? LifeClock.RealSecondsBetween(a.start, Mathf.Clamp(now.hour, a.start, a.end)) : 0f;
            bool holds = kind != PropKind.None && kind != PropKind.Mask; // a mask is worn, the hands stay free
            // #192: a baby in the arms holds them the way a load does; on the back it rides, and the hands stay on the work.
            bool babyArms = !a.carrying.IsNone && !a.carryOnBack && !holds;
            d.stride = Mathf.Repeat(d.stride + DollMotion.Cadence(playing ? playCue.under : style, d.speed) * Mathf.PI * dt, Mathf.PI * 2f);
            if (paired && pairEv.kind == PairKind.WalkTogether && pairCue.index == 1 && dolls.TryGetValue(pairEv.group.KeyOf(0), out var strideLead)) d.stride = strideLead.stride; // #363: strides in step
            float tipSince = 0f;
            if (style == DollStyle.TipBasket) // #361: the tip plays from the moment the doll stands still at the pile, not from the plan's start
            {
                if (d.drew != DollStyle.TipBasket) d.tipStart = time;
                tipSince = since = time - d.tipStart;
            }
            var pose = playing ? playGame.PoseOf(playIx, time, phase, d.stride, d.speed, out _) : DollMotion.Evaluate(style, phase, a.pace, a.step, steps, babyArms ? PropSlot.Arms : slot, holds || babyArms, d.speed, a.progress, since, a.count, d.stride); // #358
            if (style != d.drew)
            {
                d.blendFrom = d.pose;
                d.blendT = pose.hidden || pose.lying || d.pose.hidden || d.pose.lying || jump ? BlendSeconds : 0f;
                d.drew = style;
            }
            if (d.blendT < BlendSeconds)
            {
                d.blendT += dt;
                pose = DollMotion.Blend(d.blendFrom, pose, Mathf.SmoothStep(0f, 1f, d.blendT / BlendSeconds));
            }
            if (hunting)
            {
                var stalkNow = default(HuntStalk.Now);
                if (a.hunt == HuntPhase.Stalk && huntPlan != null) stalkNow = HuntStalk.Sample(since, huntPlan.freezeA, huntPlan.freezeB);
                DollMotion.Hunt(ref pose, a.hunt, a.progress, since, stalkNow);
            }
            StagedPose?.Invoke(a, v, ref pose); // #370: a staged moment's one-shot beats (a cheer, a bow, the lift, the ring)
            if (a.kind == ActivityKind.FetchWater) // #360: the draw (kneel, dip, lift) at the water; the heavy walk home with the full bucket
            {
                if (style == DollStyle.DrawWater) WaterBeats.Apply(ref pose, since, a.who.Key, a.slot);
                else if (style == DollStyle.Walk && a.moving) WaterBeats.Heavy(ref pose, d.stride);
            }
            if (fishNow) FishBeats.Apply(ref pose, fishS, a.who.Key, fishLeft); // #364
            // #363 B7: the pair beats lie over the pose the loop drew.
            if (paired) PairStage.Apply(pairEv, pairCue.index, time, ref pose, d.stride);
            else if (style == DollStyle.Talk && !a.partner.IsNone && a.kind is ActivityKind.Chat or ActivityKind.Court or ActivityKind.Visit
                     && dolls.TryGetValue(a.partner.Key, out var chatMate) && chatMate.placed && (chatMate.pos - d.pos).sqrMagnitude < 0.6f * 0.6f)
            {
                // The listener nods on the talker's beats, the talker turns an open hand, the last 1.2 s are a shared laugh; one clock for the pair.
                float left = LifeClock.RealSecondsBetween(Mathf.Clamp(now.hour, a.start, a.end), a.end);
                PairBeats.Talk(ref pose, PairBeats.TalkAt(a.who.Key, a.partner.Key, time, left), Desync.Of(a.who.Key, 363).amp, holds);
            }
            else if (style == DollStyle.Walk && Mates(d, a, time, out bool mateLead, out var mate))
            {
                if (a.who.Key > d.mateKey) d.stride = mate.stride;   // strides in step (a pair is exempt from the neighbours rule)
                PairBeats.WalkTogether(ref pose, mateLead, time, d.stride, PairBeats.PairSeed(a.who.Key, d.mateKey));
            }
            d.pose = pose;
            v.gameObject.SetActive(!pose.hidden);
            bool struck = d.hitEdge.Step(pose.hidden ? 0f : pose.hit); // #355
            if (pose.hidden) return;
            v.SetBaby(!a.carrying.IsNone, a.carryOnBack);
            float s = v.scale;
            var yawQ = Quaternion.Euler(0, d.yaw + pose.yaw, 0);
            var ground = new Vector3(d.drawn.x, Box != null ? Box.SurfaceHeight(d.drawn.x, d.drawn.y) : land.Height(d.drawn.x, d.drawn.y), d.drawn.y);
            if (playing && playCue.sink > 0f && KidPlay.PondLevel(out float pondY)) ground.y = pondY - playCue.sink; // #359: wading, jumped in
            var tilt = pose.lying ? Quaternion.Euler(90f, 0, 0) : Quaternion.Euler(-pose.lean, 0, pose.roll);
            v.transform.SetLocalPositionAndRotation(ground + yawQ * (pose.offset * s), yawQ * tilt);
            v.transform.localScale = new Vector3(dollZoomScale, pose.squash * dollZoomScale, dollZoomScale);
            v.ApplyArms(pose);
            if (style == DollStyle.TipBasket) BasketTip?.Invoke(a.who, v, tipSince); // #361: every frame of the tip (BerryPickView pours the berries)
            Vector3 fishLine = default;
            if (fishNow)
            {   // #364: the line's end, on the water, a rod's reach out along the way the fisher faces: the splash is there and the fish leaves from there
                float level = KidPlay.PondLevel(out float pondLevel) ? pondLevel : Land.WaterY;
                var fwd = Quaternion.Euler(0, d.yaw, 0) * Vector3.back;
                fishLine = peopleRoot.TransformPoint(new Vector3(ground.x, level, ground.z) + fwd * (FishBeats.LineReach * s * dollZoomScale));
                Fishing?.Invoke(a.who, v, fishS, fishLeft, fishLine);
            }
            if (struck)
            {   // #355; #362: a throw's hit is the spear coming down, so the puff is where it lands, not at the hand; #364: a yank's is the line's end
                var hitAt = hunting && a.hunt == HuntPhase.Strike && huntPlan != null
                    ? peopleRoot.TransformPoint(HuntStage.LandPoint(huntPlan, (hx, hz) => Box.SurfaceHeight(hx, hz)))
                    : fishNow ? fishLine : WorkHitPoint.HitPoint(v);
                WorkHit?.Invoke(a.who, a.place, style, hitAt);
            }
        }

        // ---------------------------------------------------------------- #363: pairs

        /// <summary>True when this walker and the one they last stood talking with leave together: both walking, near, the same way. <paramref name="lead"/>
        /// is the one whose right side faces the other (the other walks at their right hand).</summary>
        bool Mates(Doll d, in PersonActivity a, float time, out bool lead, out Doll mate)
        {
            lead = false; mate = null;
            if (time - d.mateTime > 120f || !dolls.TryGetValue(d.mateKey, out mate) || mate.mateKey != a.who.Key || time - mate.mateTime > 120f) return false;
            if (!mate.placed || !mate.walking || (mate.pos - d.pos).sqrMagnitude > 0.45f * 0.45f) return false;
            if (d.velocity.sqrMagnitude < 0.0025f || mate.velocity.sqrMagnitude < 0.0025f) return false;
            var h = d.velocity.normalized;
            if (Vector2.Dot(h, mate.velocity.normalized) < 0.8f) return false;
            lead = Vector2.Dot(mate.pos - d.pos, new Vector2(h.y, -h.x)) > 0f;
            return true;
        }

        /// <summary>A hunter takes the spear (the hunt's TakeSpear step): if a toolmaker is knapping near, they walk it over and hand it across.
        /// Once per hunt plan; squeezed into what is left of the step.</summary>
        void HandSpear(Doll hunter, in PersonActivity a, float time)
        {
            int key = a.who.Key;
            if (huntPlan == null || (spearHanded.TryGetValue(key, out var done) && done == huntPlan)) return;
            spearHanded[key] = huntPlan;
            int best = -1; float bestD = PairStage.MaxHandOverReach * PairStage.MaxHandOverReach;
            foreach (var kv in dolls)
            {
                var o = kv.Value;
                if (kv.Key == key || o.act.kind != ActivityKind.Knap || !o.placed || o.pose.hidden || PairStage.TryCue(kv.Key, time, out _, out _)) continue;
                float dd = (o.pos - hunter.pos).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = kv.Key; }
            }
            if (best < 0) return;
            float window = LifeClock.RealSecondsBetween(now.hour, Mathf.Max(now.hour, huntStep.end));
            var ev = PairStage.HandOver(PairBeats.PairSeed(best, key) ^ (int)now.day, time, best, key, dolls[best].pos, hunter.pos, PropKind.Spear, window);
            if (ev == null) return;
            ev.cutAt = time + window + 0.2f;
            PairStage.Add(ev);
        }

        /// <summary>Debug (#363): "Hand over now" / "Walk together now" / "Hug now": the two nearest people on view play the pair beat.</summary>
        public static string DebugPair(PairKind kind)
        {
            var pv = Find();
            return pv == null ? "no people" : pv.StagePair(kind);
        }

        string StagePair(PairKind kind)
        {
            float time = poseClock;
            int ka = 0, kb = 0; float best = float.MaxValue;
            foreach (var x in dolls)
                foreach (var y in dolls)
                {
                    if (x.Key >= y.Key) continue;
                    Doll p = x.Value, q = y.Value;
                    if (!p.placed || !q.placed || p.pose.hidden || q.pose.hidden || p.view == null || q.view == null) continue;
                    if (PairStage.TryCue(x.Key, time, out _, out _) || PairStage.TryCue(y.Key, time, out _, out _)) continue;
                    float dd = (p.pos - q.pos).sqrMagnitude;
                    if (dd < best) { best = dd; ka = x.Key; kb = y.Key; }
                }
            if (best == float.MaxValue) return "pairs: need two people on view";
            Doll a = dolls[ka], b = dolls[kb];
            int seed = PairBeats.PairSeed(ka, kb) ^ (int)now.day;
            PairEvent ev;
            switch (kind)
            {
                case PairKind.HandOver:
                    ev = PairStage.HandOver(seed, time, ka, kb, a.pos, b.pos, PropKind.Spear, 0f, 3f);
                    break;
                case PairKind.WalkTogether:
                {
                    var mid = (a.pos + b.pos) * 0.5f;
                    var toMid = -mid; // toward the middle of the box, so they stay on the ground
                    if (toMid.sqrMagnitude < 0.09f) toMid = new Vector2(1f, 0f);
                    var heading = toMid.normalized;
                    var right = new Vector2(heading.y, -heading.x);
                    bool aLead = Vector2.Dot(b.pos - a.pos, right) > 0f; // whoever has the other on the right leads
                    ev = PairStage.WalkTogether(seed, time, aLead ? ka : kb, aLead ? kb : ka, aLead ? a.pos : b.pos, heading, 7f);
                    break;
                }
                default:
                    ev = PairStage.Hug(seed, time, ka, kb, a.pos, b.pos, true, b.view.scale < a.view.scale * 0.9f);
                    break;
            }
            if (ev == null) return "pairs: too far apart";
            PairStage.Add(ev);
            return $"pairs: {kind} with {ka} and {kb}";
        }

        // ---------------------------------------------------------------- #283: the hunt

        /// <summary>Where a hunter (or the kid who follows) is now, from the hunt plan's own coordinates (box units), and what they face
        /// (the rabbit while they stalk). False when the plan has no step for them now.</summary>
        bool HuntSpot(in PersonActivity a, out float x, out float z, out Vector2? face)
        {
            x = z = 0f; face = null;
            if (!planner.HuntStepAt(a.who, now, out var st, out var plan)) return false;
            huntStep = st; huntPlan = plan;
            float x0 = st.x0, z0 = st.z0, x1 = st.x1, z1 = st.z1;
            bool startOpen = st.phase is HuntPhase.ToFire or HuntPhase.KidFollow, endOpen = st.phase is HuntPhase.Return or HuntPhase.KidBack;
            if (startOpen && !planner.Positions.TryGetPosition(st.from, out x0, out z0)) { x0 = plan.fireX; z0 = plan.fireZ; }
            if (endOpen && !planner.Positions.TryGetPosition(st.to, out x1, out z1)) { x1 = plan.fireX; z1 = plan.fireZ; }
            float f = Mathf.Clamp01((now.hour - st.start) / Mathf.Max(1e-4f, st.end - st.start));
            if (st.phase == HuntPhase.Stalk)   // #362: seven creeping steps and the freezes, the same clock the rabbit sits up on
                f = HuntStalk.Sample(LifeClock.RealSecondsBetween(st.start, Mathf.Max(st.start, now.hour)), plan.freezeA, plan.freezeB).fraction;
            var p = Vector2.Lerp(AtFire(plan, x0, z0, a.who.Key), AtFire(plan, x1, z1, a.who.Key), f);
            x = p.x; z = p.y;
            if (st.faceRabbit) face = new Vector2(plan.targetX, plan.targetZ);
            else if (st.faceSpear) face = new Vector2(plan.landX, plan.landZ);
            else if (st.phase is HuntPhase.TakeSpear or HuntPhase.Skin or HuntPhase.Cook) face = new Vector2(plan.fireX, plan.fireZ);
            return true;
        }

        /// <summary>#284: where a tent builder (or the kid who watches) is now, from the raising plan's coordinates, and what they face
        /// (the frame while they take a load, the lot while they raise it).</summary>
        bool TentSpot(in PersonActivity a, out float x, out float z, out Vector2? face)
        {
            x = z = 0f; face = null;
            if (!planner.TentStepAt(a.who, now, out var st, out var plan)) return false;
            float x0 = st.x0, z0 = st.z0, x1 = st.x1, z1 = st.z1;
            if (st.phase == TentPhase.ToFrame && !planner.Positions.TryGetPosition(st.from, out x0, out z0)) { x0 = x1; z0 = z1; }
            if (st.phase == TentPhase.Return && !planner.Positions.TryGetPosition(st.to, out x1, out z1)) { x1 = x0; z1 = z0; }
            float f = Mathf.Clamp01((float)((now.Absolute - plan.StartAbsolute - st.start) / Mathf.Max(1e-4f, st.end - st.start)));
            var p = Vector2.Lerp(new Vector2(x0, z0), new Vector2(x1, z1), f);
            x = p.x; z = p.y;
            if (st.phase is TentPhase.TakeLoad) face = new Vector2(plan.frameX, plan.frameZ);
            else if (st.phase is TentPhase.Raise or TentPhase.Stand or TentPhase.KidWatch or TentPhase.KidCheer) face = new Vector2(plan.lotX, plan.lotZ);
            return true;
        }

        /// <summary>#362: what the hunter holds now. The spear is in the hand until the throw releases it, and again once it is pulled out
        /// of the ground; a catch is held up in the hand (not over the shoulder) from the moment it is lifted.</summary>
        (PropKind, PropSlot) HuntProp(in PersonActivity a, PropKind kind, PropSlot slot)
        {
            float since = a.end > a.start ? LifeClock.RealSecondsBetween(a.start, Mathf.Clamp(now.hour, a.start, a.end)) : 0f;
            switch (a.hunt)
            {
                case HuntPhase.Strike: if (!HuntBeats.SpearInHand(a.hunt, since)) return (PropKind.None, PropSlot.Hand); break;
                case HuntPhase.PullSpear: if (huntPlan != null && now.hour < huntPlan.takeHour) return (PropKind.None, PropSlot.Hand); break;
                case HuntPhase.PickUp: if (huntPlan != null && now.hour >= huntPlan.takeHour) return (PropKind.Rabbit, PropSlot.Hand); break;
                case HuntPhase.HoldUp: return (PropKind.Rabbit, PropSlot.Hand);
            }
            return (kind, slot);
        }

        /// <summary>A point on the fire itself becomes a spot on the ring round it, so nobody stands in the flames.</summary>
        static Vector2 AtFire(HuntPlan plan, float x, float z, int key)
        {
            float dx = x - plan.fireX, dz = z - plan.fireZ;
            if (dx * dx + dz * dz > 0.05f * 0.05f) return new Vector2(x, z);
            float ang = Hash01(key, 283) * Mathf.PI * 2f;
            return new Vector2(plan.fireX + Mathf.Cos(ang) * 0.34f, plan.fireZ + Mathf.Sin(ang) * 0.34f);
        }

        /// <summary>#189: from the planned point to the drawn one: the walker's offset eases toward what the crowd presses (it fades when
        /// nothing does), a point in a solid slides out to its rim, and the doll follows a moment behind at no more than a walking pace, so a
        /// change of side is a walk and a corner a curve (#203).</summary>
        void StepDrawn(Doll d, in PersonActivity a, bool jump, bool hidden, Vector2 prev, float inst, float dt)
        {
            // The way the plan walks, settling over a moment: a corner of the route turns the sidestep with it, not in one frame.
            if (!a.moving || jump || hidden || dt <= 0f) d.heading = Vector2.zero;
            else d.heading = Vector2.Lerp(d.heading, (d.pos - prev) / dt, 1f - Mathf.Exp(-dt * HeadingRate));
            float px = 0f, pz = 0f;
            if (d.crowdIndex >= 0 && !jump && !hidden) crowd.Push(d.crowdIndex, out px, out pz);
            if (jump || hidden) d.offset = Vector2.zero;
            else if (a.moving) CrowdAvoid.Ease(ref d.offset.x, ref d.offset.y, px, pz, dt);
            else d.offset = Vector2.MoveTowards(d.offset, Vector2.zero, DrawnSlack * dt); // arrived: back onto their spot at a stroll, off whatever the walk brushed
            var wanted = d.pos + d.offset;
            if (Box.land.IsWater(wanted.x, wanted.y)) wanted = d.pos; // eased aside, never into the pond
            if (hidden) { d.drawn = wanted; return; }
            CrowdAvoid.KeepOut(ref wanted.x, ref wanted.y, solids, d.heading.x, d.heading.y);
            if (jump) { d.drawn = wanted; return; }
            CrowdAvoid.Follow(ref d.drawn.x, ref d.drawn.y, wanted.x, wanted.y, (inst + DrawnSlack) * dt, dt);
            CrowdAvoid.KeepOut(ref d.drawn.x, ref d.drawn.y, solids, d.heading.x, d.heading.y);
        }

        /// <summary>Where in the place this person stands (rule 6: one doll per slot) and what they face there.</summary>
        Vector2 SlotSpot(Doll d, PersonActivity a, Vector2 spot, out Vector2? face)
        {
            face = null;
            countAt.TryGetValue(a.place, out int count);
            count = Mathf.Max(1, count);
            float h = Hash01(a.place.GetHashCode(), 11) * Mathf.PI * 2f;
            switch (a.place.kind)
            {
                case PlaceKind.Glass:
                {
                    // #190: a loose group, some closer, some behind, a few turned to each other; the wary (stage 1) keep a step
                    // back, the kids press right up. The group's seed lasts the day, so the spots stay put while they stand there.
                    var n = GlassNormal(planner.GlassSide);
                    var along = new Vector2(-n.y, n.x);
                    float back = a.kind is ActivityKind.GazeWary or ActivityKind.LookUpWary or ActivityKind.WaveWary ? 0.16f
                        : a.kind is ActivityKind.SitOut ? 0.06f : -0.09f; // the kids' noses on the glass
                    var stand = GlassSpots.Spot(unchecked(a.place.GetHashCode() * 31 + (int)now.day), a.slot, a.who.Key);
                    var p = spot + along * stand.along - n * (back + stand.depth);
                    float cos = Mathf.Cos(stand.turn), sin = Mathf.Sin(stand.turn);
                    face = p + n * cos + along * sin;
                    return p;
                }
                case PlaceKind.Relic:
                case PlaceKind.Prop: // #371: round the thing a choice left, the same ring
                {
                    // #92: round the relic (the crumb, the doll's post, the tripod, the vines), a little back from it.
                    float r = 0.2f + Mathf.Max(0, count - 6) * 0.02f;
                    float ang = h + a.slot * Mathf.PI * 2f / Mathf.Max(count, 6);
                    face = spot;
                    return spot + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                }
                case PlaceKind.Fire:
                case PlaceKind.Green:
                case PlaceKind.Remembering:
                case PlaceKind.Market:
                {
                    // A ring facing the middle; more people widen it a little.
                    float r = a.place.kind switch { PlaceKind.Fire => 0.3f, PlaceKind.Remembering => 0.2f, _ => 0.36f } + Mathf.Max(0, count - 8) * 0.015f;
                    float ang = h + a.slot * Mathf.PI * 2f / Mathf.Max(count, 8); // fixed spacing up to 8, so arrivals don't shift the ring
                    face = spot;
                    return spot + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                }
            }
            if (a.place.kind == PlaceKind.Stash && planner.Positions.TryGetPosition(CampStash.Pile, out float pileX, out float pileZ))
            {
                // #150: a fan on the fire's side of the pile, turning round it as more come, facing it: a load is set down, not walked over.
                var pile = new Vector2(pileX, pileZ);
                var toFire = spot - pile;
                float start = toFire.sqrMagnitude > 1e-6f ? Mathf.Atan2(toFire.y, toFire.x) : 0f;
                int row = a.slot / StashFan, k = a.slot % StashFan;
                float r = CampStash.PileExtent + StashStandOff + row * SlotSpots.Gap;
                float ang = start + ((k + 1) / 2) * ((k & 1) == 1 ? 1f : -1f) * SlotSpots.Gap / r;
                face = pile;
                return pile + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            }
            if (a.place.IsBuilding && lotOfBuilding.TryGetValue(a.place.buildingId, out var lot) && !lot.open &&
                lot.kind != LotKind.Farm && lot.kind != LotKind.Pasture && lot.kind != LotKind.Orchard)
            {
                // At a door: a half-ring outside it, facing the building (the sawyer at the mill, the server at the tavern).
                var centre = new Vector2(lot.x, lot.z);
                var outDir = (spot - centre).sqrMagnitude > 1e-6f ? (spot - centre).normalized : Vector2.down;
                float baseAng = Mathf.Atan2(outDir.y, outDir.x);
                var (dx, dz) = a.place.kind == PlaceKind.Home ? SlotSpots.Home(SlotSpots.SpotFor(a.kind), d.homeRank, baseAng) // #200: at most two at the door, the rest in the yard
                    : SlotSpots.Door(a.slot, baseAng); // a fan outside the door, no two closer than SlotSpots.Gap
                var at = spot + new Vector2(dx, dz);
                // #200: at home they look out over the town. Facing their own wall, a sitter or a chopper looks as if they're building it.
                face = a.place.kind == PlaceKind.Home ? at + (at - centre).normalized : centre;
                return at;
            }
            if (a.place.kind == PlaceKind.BushCell)
            {
                // P8 #288: at a real bush (a berry bush, the cell's centre): each picker at their own side of it, an arm's reach off, facing it.
                float ba = h + a.slot * 2.39996f;
                face = spot;
                return spot + new Vector2(Mathf.Cos(ba), Mathf.Sin(ba)) * (0.17f + 0.03f * (a.slot / 3));
            }
            // Open ground (a field, the woods, the pond): a loose spiral round the spot, facing it.
            float spread = a.place.kind is PlaceKind.Field or PlaceKind.Pasture or PlaceKind.Meadow or PlaceKind.Woods or PlaceKind.ForestEdge or PlaceKind.Bushes ? 0.2f : 0.12f;
            if (spread <= SlotSpots.Gap)
            {
                // A bare doorstep: rings round the spot, nobody closer than the gap.
                var (ox, oz) = SlotSpots.Open(a.slot, h);
                face = a.slot == 0 ? spot + new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * 0.1f : spot;
                return spot + new Vector2(ox, oz);
            }
            if (a.slot == 0) { face = spot + new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * 0.1f; return spot; }
            float ga = h + a.slot * 2.39996f;
            face = spot;
            return spot + new Vector2(Mathf.Cos(ga), Mathf.Sin(ga)) * spread * Mathf.Sqrt(a.slot);
        }

        /// <summary>#150: carriers in one row round the pile before the next stands behind it.</summary>
        const int StashFan = 7;

        static bool InField(Place p) => p.kind is PlaceKind.Field or PlaceKind.Pasture;

        /// <summary>#92: out through the front glass (box-local x, z) for a <see cref="LifePlanner.GlassSide"/>.</summary>
        public static Vector2 GlassNormal(int side) => (side & 3) switch { 0 => Vector2.down, 1 => Vector2.right, 2 => Vector2.up, _ => Vector2.left };

        /// <summary>A slot that fell on a building's lot (a ring round the market running into a house) moves to the nearest
        /// walkable ground: round the same ring first, then closer to the middle. The spot itself if nothing is free.</summary>
        static readonly float[] Rings = { 1f, 1.4f, 0.75f, 1.8f, 0.5f };

        Vector2 Standable(Vector2 p, Vector2 centre, bool offLots, bool noCloser = false)
        {
            var st = offLots && plannerSim != null ? plannerSim.Settlement : null;
            bool Free(Vector2 q)
            {
                foreach (var o in claimed) if ((o - q).sqrMagnitude < Personal * Personal) return false;
                if (!offLots) return true;
                foreach (var b in blockers) if ((b.c - q).sqrMagnitude < b.r * b.r) return false;
                if (st == null) return true;
                int c = SiteMap.CellAt(q.x, q.y);
                return (c < 0 || st.WalkCost(c) > 0) && Clear(centre, q);
            }
            if (Free(p)) return p;
            var off = p - centre;
            float r = Mathf.Max(off.magnitude, Personal), a0 = Mathf.Atan2(off.y, off.x);
            foreach (float shrink in Rings)
                for (int k = 0; k <= 12 && (!noCloser || shrink >= 1f); k++) // #200: a yard spot never slides back to the door
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        float a = a0 + sgn * k * 15f * Mathf.Deg2Rad;
                        var q = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * shrink;
                        if (Free(q)) return q;
                    }
            return noCloser ? p : centre;
        }

        /// <summary>Is the straight line between two points on walkable ground (no lot, no pond)? A line that starts on a
        /// lot (a market's middle) is checked from where it leaves it. True without a settlement.</summary>
        bool Clear(Vector2 a, Vector2 b)
        {
            var st = plannerSim != null ? plannerSim.Settlement : null;
            if (st == null) return true;
            bool Walkable(Vector2 q) { int c = SiteMap.CellAt(q.x, q.y); return c < 0 || st.WalkCost(c) > 0; }
            bool outside = Walkable(a);
            int n = Mathf.CeilToInt((b - a).magnitude / 0.04f);
            for (int i = 1; i < n; i++)
            {
                bool w = Walkable(Vector2.Lerp(a, b, i / (float)n));
                if (!outside) { outside = w; continue; }
                if (!w) return false;
            }
            return true;
        }

        /// <summary>#360: where someone kneels to draw water, and what they face: 0.06 u back from the real edge of the pond (the first dry point
        /// out from its middle, turned by the slot so two drawers stand apart), or 0.1 u off a puddle or the wet bed.</summary>
        static Vector2 DrawStand(Land land, PlaceKind kind, Vector2 spot, Vector2 slotSpot, int slot, out Vector2? face)
        {
            var water = kind == PlaceKind.Pond ? land.pond : spot;
            var dir = kind == PlaceKind.Pond ? land.centre - water : slotSpot - water;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.down;
            dir.Normalize();
            float turn = ((slot + 1) / 2) * (slot % 2 == 1 ? 1f : -1f) * 0.45f; // radians
            float c = Mathf.Cos(turn), sn = Mathf.Sin(turn);
            dir = new Vector2(dir.x * c - dir.y * sn, dir.x * sn + dir.y * c);
            var edge = water;
            if (kind == PlaceKind.Pond) for (int i = 0; i < 200 && land.IsWater(edge.x, edge.y); i++) edge += dir * 0.02f;
            var stand = edge + dir * (kind == PlaceKind.Pond ? 0.06f : 0.1f);
            face = stand - dir; // one step towards the water
            return stand;
        }

        static Vector2 Ashore(Land land, Vector2 p)
        {
            var dir = land.centre - p;
            if (dir.sqrMagnitude < 1e-6f) return p;
            dir.Normalize();
            for (int i = 0; i < 40 && land.IsWater(p.x, p.y); i++) p += dir * 0.03f;
            return p + dir * 0.03f;
        }

        /// <summary>The yaw that turns the doll's front (local −z) from <paramref name="from"/> towards <paramref name="to"/>.</summary>
        static float Yaw(Vector2 from, Vector2 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 180f;
        }

        // ---------------- looks ----------------
        static int Seed(PersonRef who) => who.IsNotable ? 40000 + who.notableId * 13 : 9000 + who.crowdIndex;

        static PegDoll.Look LookOf(PersonRef who, Notable n, string role, bool camp, bool kid)
        {
            var look = PegDoll.PickLook(Seed(who), camp, kid, -1);
            if (role != null)
            {
                look.role = true;
                look.accent = Palette.RoleAccent(role);
            }
            else if (n != null && !kid)
                look.accent = Palette.Accents[(n.id * 7 + 3) % Palette.Accents.Length]; // a notable's thin sash (camp: headband)
            // Gold is the leader's (chief, headman, lord): nobody else wears it, so the leader is the one gold dot in the box.
            if (!look.role && look.accent == Palette.Accents[1]) look.accent = Palette.Accents[(Seed(who) & 1) == 0 ? 0 : 3];
            return look;
        }

        static uint LookSig(PegDoll.Look l, bool kid)
        {
            unchecked
            {
                uint h = 2166136261u;
                void Add(uint v) { h = (h ^ v) * 16777619u; }
                Add(kid ? 1u : 0u); Add(l.camp ? 1u : 0u); Add(l.role ? 1u : 0u);
                Add((uint)Mathf.RoundToInt(l.accent.r * 255f)); Add((uint)Mathf.RoundToInt(l.accent.g * 255f)); Add((uint)Mathf.RoundToInt(l.accent.b * 255f)); Add((uint)Mathf.RoundToInt(l.accent.a * 255f));
                return h;
            }
        }

        static float Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>The planner's prop (one of ~40) as one of the view's code-built props, and where it rides. Small things
        /// that wouldn't read at phone size (a mug, a flower, a bell) aren't drawn.</summary>
        public static (PropKind kind, PropSlot slot) PropOf(LifeProp prop, LifeSlot lifeSlot, ActivityKind activity, WaterKind water = WaterKind.None)
        {
            var slot = lifeSlot switch
            {
                LifeSlot.Head => PropSlot.Head,
                LifeSlot.Shoulder => PropSlot.Shoulder,
                LifeSlot.Back or LifeSlot.Pulled => PropSlot.Back,
                LifeSlot.Arms => PropSlot.Arms,
                LifeSlot.Rolled => PropSlot.Front,
                _ => PropSlot.Hand,
            };
            var kind = prop switch
            {
                LifeProp.Basket or LifeProp.Loaves => PropKind.Basket,
                LifeProp.Log => PropKind.Log,
                LifeProp.Goods or LifeProp.Cloth or LifeProp.Wool or LifeProp.Handcart => PropKind.Bundle,
                LifeProp.Hide => PropKind.Bundle,
                LifeProp.Broom => PropKind.Broom,
                LifeProp.Stick or LifeProp.Crook or LifeProp.Saw => PropKind.Stick,
                LifeProp.MeasuringRod => PropKind.MeasuringRod,
                // #92: the relics and the scholars' things
                LifeProp.Crumb => PropKind.Crumb,
                LifeProp.Mask => PropKind.Mask,
                LifeProp.Doll => PropKind.Doll,
                LifeProp.Sandal => PropKind.Sandal,
                LifeProp.Tripod => PropKind.Tripod,
                LifeProp.Lens => PropKind.Lens,
                LifeProp.Slate or LifeProp.Notebook => PropKind.Slate,
                LifeProp.Sheaf => PropKind.Sheaf,
                LifeProp.Lantern => PropKind.Lantern,
                LifeProp.Buckets => water == WaterKind.Muddy ? PropKind.BucketMuddy : PropKind.Bucket,
                LifeProp.EmptyBucket => PropKind.BucketEmpty,
                LifeProp.Sack or LifeProp.SeedBag => PropKind.Sack,
                LifeProp.Block => PropKind.Block,
                LifeProp.Barrel => PropKind.Barrel,
                LifeProp.Axe => PropKind.Axe,
                LifeProp.Spear => PropKind.Spear,
                LifeProp.Rabbit => PropKind.Rabbit, // #283
                LifeProp.Pots or LifeProp.Honey => PropKind.Pot,
                LifeProp.Hoe or LifeProp.Scythe => PropKind.Hoe,
                LifeProp.Hammer or LifeProp.Hammerstone or LifeProp.Pick => PropKind.Hammer,
                LifeProp.FishStick => PropKind.Fish,
                LifeProp.Rod => PropKind.Rod,
                LifeProp.Ball => PropKind.Ball, // #358: drawn (it fell to None)
                _ => PropKind.None,
            };
            // Fit the slot to the mesh: a bundle rides on the back, buckets hang from the hand, a block is hugged.
            if (kind == PropKind.Basket) slot = PropSlot.Hand; // #361: a basket is held in a hand (the left one while the right picks, PegDollView.HoldInLeft), never hugged: SlotOf says Arms
            else if (kind == PropKind.Bundle && slot != PropSlot.Shoulder) slot = PropSlot.Back;
            else if (kind is PropKind.Bucket or PropKind.BucketMuddy or PropKind.BucketEmpty) slot = PropSlot.Hand;
            else if (kind == PropKind.Block && slot == PropSlot.Hand) slot = PropSlot.Arms;
            else if (kind == PropKind.Barrel) slot = PropSlot.Front;
            else if (kind == PropKind.Ball) slot = PropSlot.Arms; // #358: hugged
            else if (kind == PropKind.Slate && activity == ActivityKind.WriteDown) slot = PropSlot.Arms; // hugged, the hand writes
            return (kind, slot);
        }

        /// <summary>Is this person's doll walking (on a route, or gliding into its slot)?</summary>
        readonly Dictionary<int, Vector2> pins = new();
        readonly Dictionary<int, Vector2> lookAt = new();

        /// <summary>#282: turn this person (while they stand) to face a spot (box units); the first rabbit's sign sets it for whoever points.</summary>
        public void LookAt(PersonRef who, Vector2 at) => lookAt[who.Key] = at;
        public void ClearLookAt(PersonRef who) => lookAt.Remove(who.Key);

        // ---------------------------------------------------------------- #358: kids' play (KidPlayDirector)
        KidPlayDirector kidPlay;
        /// <summary>#358: the play groups (tag, ball, the rabbit chase, the ring dance) and the panel's play cheats.</summary>
        public KidPlayDirector KidPlay => kidPlay ??= new KidPlayDirector(this);
        internal Land LandForPlay => Box != null ? Box.land : null;
        internal BoxView BoxForPlay => Box; // #359
        internal PegDollView ViewOf(int key) => dolls.TryGetValue(key, out var d) ? d.view : null;
        internal float GroundAt(float x, float z) => Box != null ? Box.SurfaceHeight(x, z) : 0f;
        /// <summary>How big a person's doll is drawn (doll scale × zoom), for where its hands are.</summary>
        internal bool ScaleOf(int key, out float s)
        {
            s = 0f;
            if (!dolls.TryGetValue(key, out var d) || d.view == null) return false;
            s = d.view.scale * dollZoomScale;
            return true;
        }
        /// <summary>The person on view nearest a spot who passes <paramref name="ok"/> and isn't in <paramref name="not"/> (a key, or −1).</summary>
        internal int NearestShown(Vector2 at, System.Func<PersonRef, bool> ok, List<int> not)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (var a in acts)
            {
                int k = a.who.Key;
                if (not.Contains(k) || !dolls.TryGetValue(k, out var d) || !d.placed || d.pose.hidden || d.view == null || !d.view.gameObject.activeInHierarchy) continue;
                if (a.hunt != HuntPhase.None || a.tent != TentPhase.None || !ok(a.who)) continue;
                float dd = (d.drawn - at).sqrMagnitude;
                if (dd < bd) { bd = dd; best = k; }
            }
            return best;
        }
        /// <summary>Where a few people are drawn, on average (a home's kids in its yard).</summary>
        internal bool MeanDrawn(int[] keys, out Vector2 mean)
        {
            mean = Vector2.zero; int n = 0;
            foreach (int k in keys) if (dolls.TryGetValue(k, out var d) && d.placed) { mean += d.drawn; n++; }
            if (n == 0) return false;
            mean /= n;
            return true;
        }

        /// <summary>#355: a stroke landed: a doll's <see cref="DollPose.hit"/> went 0 → 1 (a loop's chop, hoe or knock, a beat's
        /// <see cref="BeatPhase.Contact"/>), once per contact. Who, where they work, the move, and the tool's end in world space
        /// (<see cref="WorkHitPoint.HitPoint"/>). Puffs (HitFx) and the targets' answers subscribe here.</summary>
        public event System.Action<PersonRef, Place, DollStyle, Vector3> WorkHit;

        /// <summary>#364: every frame a fisher stands fishing: who, the doll, the block's seconds so far (with the cheat's shift), the seconds left, and the line's end (world, on the water).</summary>
        public event System.Action<PersonRef, PegDollView, float, float, Vector3> Fishing;
        /// <summary>#361: every frame a doll tips its basket onto the stash pile: who, their view, seconds into the tip.</summary>
        public event System.Action<PersonRef, PegDollView, float> BasketTip;

        /// <summary>#370: a staged moment (<c>StagedMoments</c>) places its cast on the plan's own spots and lays its beats over their pose.</summary>
        public delegate bool StagedSpotHook(in PersonActivity a, ViewTime now, out float x, out float z, out Vector2? face);
        public delegate void StagedPoseHook(in PersonActivity a, PegDollView view, ref DollPose pose);
        public static StagedSpotHook StagedSpot;
        public static StagedPoseHook StagedPose;

        /// <summary>#282: everyone standing out in the box now (placed, shown), with their box-local spots: what rabbits keep away from
        /// and who is nearest to a sign.</summary>
        public void StandingNow(List<PersonRef> who, List<float> xs, List<float> zs)
        {
            who.Clear(); xs.Clear(); zs.Clear();
            foreach (var a in acts)
                if (dolls.TryGetValue(a.who.Key, out var d) && d.placed && !d.pose.hidden && d.view != null && d.view.gameObject.activeInHierarchy)
                { who.Add(a.who); xs.Add(d.drawn.x); zs.Add(d.drawn.y); }
        }

        /// <summary>#224: stand this person on their own spot (box units) while they are at a place, instead of a slot there: the choice
        /// scene puts its two candidates either side of the fire. A walk is still the planner's; the pin holds once they arrive.</summary>
        public void Pin(PersonRef who, Vector2 at) => pins[who.Key] = at;

        /// <summary>#224: everyone back to their slots.</summary>
        public void ClearPins() => pins.Clear();

        public int PinCount => pins.Count;

        public bool IsWalking(PersonRef who) => dolls.TryGetValue(who.Key, out var d) && d.walking;

        /// <summary>The activity a doll is showing (tests, reports).</summary>
        public bool TryGetActivity(PegDollView view, out PersonActivity act)
        {
            foreach (var d in dolls.Values) if (d.view == view) { act = d.act; return true; }
            act = default;
            return false;
        }

        /// <summary>Debug (#192): a baby for the first grown notable, and who has them today. Their hours are in the planner's Describe.</summary>
        public static string DebugBaby()
        {
            var pv = Find();
            var sim = pv != null && pv.Source != null ? pv.Source.Sim : null;
            var baby = sim?.DebugBirth();
            if (baby == null) return "no adult to bear";
            pv.planner?.Invalidate();
            return $"born: {baby.FullName}";
        }

        /// <summary>Debug (#197): a rival bicker or a friend chat now, on the people's own walks and spots.</summary>
        public static string DebugRunIn(bool bicker)
        {
            var pv = Find();
            if (pv == null || pv.planner == null) return "no people";
            return pv.planner.ForceRunIn(bicker ? ActivityKind.Argue : ActivityKind.Chat, pv.now);
        }

        /// <summary>Debug (#360): "Water run now": three adults draw water at dawn, and the clock goes to the walk out.</summary>
        public static string DebugWaterRun()
        {
            var pv = Find();
            if (pv == null || pv.planner == null) return "no people";
            string who = pv.planner.ForceWaterRun(3);
            DebugSetHour(5.7f);
            return who;
        }

        /// <summary>The way a doll faces (box-local, on the ground), for tests.</summary>
        public bool TryGetDollFacing(PersonRef who, out Vector2 forward)
        {
            forward = default;
            if (!dolls.TryGetValue(who.Key, out var d) || !d.placed) return false;
            var f = Quaternion.Euler(0f, d.yaw, 0f) * Vector3.back; // a doll faces local -z
            forward = new Vector2(f.x, f.z);
            return true;
        }

        /// <summary>The pose a person's doll was drawn in last (tests).</summary>
        public bool TryGetDollPose(PersonRef who, out DollPose pose)
        {
            pose = default;
            if (!dolls.TryGetValue(who.Key, out var d) || !d.placed) return false;
            pose = d.pose;
            return true;
        }

        /// <summary>Where a person's doll stands now (box-local x, z), false if they have none.</summary>
        public bool TryGetDollPosition(PersonRef who, out Vector2 pos)
        {
            pos = default;
            if (!dolls.TryGetValue(who.Key, out var d) || !d.placed) return false;
            pos = d.drawn;
            return true;
        }

        /// <summary>#39: the top of a person's doll (just over the head) in world space, false if they have no doll or it is
        /// hidden (indoors, a baby). For world labels (the pin marker) and the person card's relationship lines.</summary>
        public bool TryGetDollTop(PersonRef who, out Vector3 world)
        {
            world = default;
            if (!dolls.TryGetValue(who.Key, out var d) || !d.placed || d.view == null || !d.view.gameObject.activeInHierarchy) return false;
            var t = d.view.transform;
            float top = (PegDoll.HeadY + PegDoll.HeadR) * d.view.scale * dollZoomScale;
            world = t.parent != null ? t.parent.TransformPoint(t.localPosition + Vector3.up * top) : t.position + Vector3.up * top;
            return true;
        }

        /// <summary>#361: a doll's view and the clock its berry beat loops on, for <see cref="BerryPickView"/>.</summary>
        public bool TryGetBerryDoll(PersonRef who, out PegDollView view, out float poseClock)
        {
            view = null; poseClock = 0f;
            if (!dolls.TryGetValue(who.Key, out var d) || !d.placed || d.view == null || !d.view.gameObject.activeInHierarchy) return false;
            view = d.view; poseClock = d.poseClock;
            return true;
        }

        /// <summary>#39: the feet of a person's doll in world space (see <see cref="TryGetDollTop"/>).</summary>
        public bool TryGetDollFeet(PersonRef who, out Vector3 world)
        {
            world = default;
            if (!dolls.TryGetValue(who.Key, out var d) || !d.placed || d.view == null || !d.view.gameObject.activeInHierarchy) return false;
            world = d.view.transform.position;
            return true;
        }

        /// <summary>#148: everyone in the crowd who has a doll in the box now.</summary>
        public void CrowdShown(List<PersonRef> into)
        {
            into.Clear();
            foreach (var a in acts)
                if (a.who.IsCrowd && dolls.TryGetValue(a.who.Key, out var d) && d.placed && d.view != null && d.view.gameObject.activeInHierarchy) into.Add(a.who);
        }

        /// <summary>#39: the notables who have a doll in the box now (ids).</summary>
        public void NotablesShown(List<int> into)
        {
            into.Clear();
            foreach (var kv in dolls)
                if (kv.Key > 0 && kv.Value.placed && kv.Value.view != null && kv.Value.view.gameObject.activeInHierarchy) into.Add(kv.Key);
        }

        // ---------------- reports and debug ----------------
        string Label(PersonRef who, Notable n, string role)
        {
            if (n == null) return plannerSim?.CrowdName(who.crowdIndex) ?? "Crowd " + who.crowdIndex; // #148
            return n.FullName + (role != null ? " (" + role + ")" : "");
        }

        /// <summary>"Ivo Lund (chief, hunter): Chop at Woods with Axe" — what a watcher should be able to say.</summary>
        public string Describe(PersonActivity a)
        {
            var sim = plannerSim;
            Notable n = null;
            if (a.who.IsNotable && sim != null) n = sim.FindNotable(a.who.notableId);
            string role = n != null && sim != null ? sim.RoleOf(n) : null;
            string job = planner != null ? planner.JobOf(a.who) : null;
            var sb = new StringBuilder();
            sb.Append(n != null ? n.FullName : sim?.CrowdName(a.who.crowdIndex) ?? "crowd " + a.who.crowdIndex);
            var tags = new List<string>();
            if (role != null) tags.Add(role);
            if (!string.IsNullOrEmpty(job)) tags.Add(job);
            if (planner != null) tags.Add(planner.StageOf(a.who).ToString().ToLowerInvariant());
            sb.Append(" (").Append(string.Join(", ", tags)).Append("): ");
            sb.Append(a.kind);
            sb.Append(a.moving ? " to " : " at ").Append(a.place);
            if (a.moving && a.from != a.place) sb.Append(" from ").Append(a.from);
            var (pk, ps) = PropOf(a.prop, a.PropSlot, a.kind, a.water);
            if (pk != PropKind.None) sb.Append(" with ").Append(pk).Append(" (").Append(ps).Append(')');
            if (!a.partner.IsNone) sb.Append(" with ").Append(a.partner);
            if (dolls.TryGetValue(a.who.Key, out var d) && d.view != null)
                sb.Append(string.Format(CultureInfo.InvariantCulture, " · doll {0} at ({1:0.00}, {2:0.00}){3}", d.view.gameObject.activeSelf ? "shown" : "hidden", d.pos.x, d.pos.y, d.walking ? " walking" : ""));
            return sb.ToString();
        }

        public string Status() => planner == null ? "people: not started" : string.Format(CultureInfo.InvariantCulture,
            "people: {0} · {1} {2:00}:{3:00} ({4}{5}) · {6:0.00} ms/frame", dolls.Count, LifeClock.PhaseOf(now.hour), Mathf.Floor(now.hour),
            Mathf.Floor((now.hour % 1f) * 60f), planner.SeasonOn(now.day), planner.IsRestDay(now.day) ? ", rest day" : "", FrameMs);

        /// <summary>Debug: set the view hour (0–24) on the sim's current month.</summary>
        public static string DebugSetHour(float hour)
        {
            var p = Find();
            if (p == null) return "no PeopleView";
            string s = p.SetHour(hour);
            DayLightDriver.Instance?.Snap(p.now.hour); // the light is there at once: a night can be judged straight away
            return s;
        }

        /// <summary>Sets this view's hour (0–24) on the sim's current month.</summary>
        public string SetHour(float hour)
        {
            now = new ViewTime(plannerSim != null ? plannerSim.State.month : now.day, Mathf.Repeat(hour, 24f)); // a jump: today's month
            clockStarted = true;
            replanIn = 0f;
            Tick(0f);
            return Status();
        }

        /// <summary>A moment's light (an era moment's beat, the last moment): the view hour moves to <paramref name="hour"/> on
        /// the sim's current month, but nobody is put anywhere: on the next frames every doll walks from where it stands to
        /// where the new hour has it, at the walking pace (tester batch 2: <see cref="DebugSetHour"/>'s Tick(0) snapped all
        /// of them 0.7-2.6 u in one frame at the bell, the cheer and the last moment's evening).</summary>
        public static void SetHourGently(float hour)
        {
            var p = Find();
            if (p == null) return;
            p.now = new ViewTime(p.plannerSim != null ? p.plannerSim.State.month : p.now.day, Mathf.Repeat(hour, 24f));
            p.clockStarted = true;
            p.replanIn = 0f;
        }

        /// <summary>Debug: jump the view clock on by <paramref name="hours"/> view hours.</summary>
        public static string DebugSkipHours(float hours) { var p = Find(); return p == null ? "no PeopleView" : DebugSetHour(p.now.hour + hours); }

        /// <summary>Debug: the view clock's speed (1 = normal, 10 = a day in 30 s).</summary>
        public static string DebugSpeed(float scale) { var p = Find(); if (p == null) return "no PeopleView"; p.timeScale = scale; return $"view clock ×{scale}"; }

        /// <summary>Debug / tester: what <paramref name="count"/> people are doing now (notables first, then a spread of
        /// the crowd), one line each; <paramref name="count"/> ≤ 0 lists everyone.</summary>
        public static string DebugReport(int count = 8)
        {
            var p = Find();
            if (p == null) return "no PeopleView";
            if (p.acts.Count == 0) p.Tick(0f);
            var sb = new StringBuilder(p.Status());
            int n = p.acts.Count, take = count <= 0 ? n : Mathf.Min(count, n);
            for (int i = 0; i < take; i++)
            {
                int k = count <= 0 || take == n ? i : (int)((long)i * n / take);
                sb.Append('\n').Append(p.Describe(p.acts[k]));
            }
            return sb.ToString();
        }
    }
}
