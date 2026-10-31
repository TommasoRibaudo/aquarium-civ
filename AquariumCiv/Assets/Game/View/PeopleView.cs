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
        public float replanSeconds = 0.2f;
        [Tooltip("Animate in edit mode (otherwise edit mode only updates when the Editor repaints).")]
        public bool animateInEditMode;

        public ViewTime Now => now;
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
            public Vector2 lastSpot;
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
        readonly List<PersonActivity> acts = new();
        readonly Dictionary<int, Doll> dolls = new();
        readonly Dictionary<Place, int> countAt = new();
        readonly HashSet<(Place, int)> taken = new();
        /// <summary>Where the dolls placed so far this frame stand (rule 6: one doll per spot, also after a slot moved off a lot).</summary>
        readonly List<Vector2> claimed = new();
        /// <summary>Pieces standing on open ground (a well by the fire): rings and spreads keep off them.</summary>
        readonly List<(Vector2 c, float r)> blockers = new();
        const float Personal = 0.075f; // closest two standing dolls get, centre to centre
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
            if (peopleRoot != null) Kill(peopleRoot.gameObject);
            peopleRoot = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void Update()
        {
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
            bool newSim = planner == null || plannerSim != sim;
            if (newSim) { planner = LifePlanner.Wired(sim); plannerSim = sim; replanIn = 0f; snapAll = clockStarted; }

            // The view day turns at midnight, not when the sim's month ticks mid-day (that swapped everyone's plan and the
            // dolls jumped, #30); a sim that jumped ahead (a catch-up, a skip) or another box is taken at once, in place.
            float hour = clockStarted ? now.hour : startHour;
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
            notables.Clear();
            foreach (var n in sim.State.notables) if (n.alive) notables[n.id] = n;
            blockers.Clear();
            foreach (var pc in Box.layout.pieces)
                if (pc.kind != PieceKind.Fire && pc.kind != PieceKind.LogSeat)
                    blockers.Add((pc.Pos, Mathf.Min(pc.w, pc.d) * 0.5f + 0.05f));
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
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            seen.Clear();
            claimed.Clear();
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
            gone.Clear();
            foreach (var kv in dolls) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int k in gone) { if (dolls[k].view != null) Kill(dolls[k].view.gameObject); dolls.Remove(k); }
        }

        void Drive(Doll d, PersonActivity a, LifeStage stage, float dt, float time)
        {
            var v = d.view;
            var land = Box.land;
            bool hidden = stage == LifeStage.Baby || a.kind == ActivityKind.None; // babies stay indoors (§4 "Everyone")
            if (!planner.PositionAt(a.who, now, out float x, out float z) && !planner.Positions.TryGetPosition(a.place, out x, out z)) hidden = true;
            var spot = new Vector2(x, z);
            var target = spot;
            Vector2? face = null;
            if (!a.moving && !hidden)
            {
                target = SlotSpot(a, spot, out face);
                target = Standable(target, spot, !InField(a.place));
                claimed.Add(target);
            }
            if (a.kind != ActivityKind.Swim && a.kind != ActivityKind.Splash && land.IsWater(target.x, target.y))
            {
                var wet = target;
                target = Ashore(land, target);
                if (!a.moving) face = wet; // the fisher faces the water
            }
            if (!a.moving && a.place.kind is PlaceKind.Pond or PlaceKind.Stream) face = land.pond;
            if (!a.moving && a.facesGlass) face = target + GlassNormal(planner.GlassSide); // #92: a dance for the sky, a wave, sitting at the window
            float rim = !a.moving && a.place.kind == PlaceKind.Glass ? 0.05f : 0.12f; // #92: right up at the glass
            target.x = Mathf.Clamp(target.x, -Land.HX + rim, Land.HX - rim);
            target.y = Mathf.Clamp(target.y, -Land.HZ + rim, Land.HZ - rim);

            // Chase the target: walks are continuous already (the planner's route); taking a slot on arrival glides at a
            // walking pace, never faster than the walk itself. Unseen (indoors) or a clock jump: just be there. A plan that
            // changed under them (the month ticked: a heap swept away, a new home) is walked to at the walking pace, never
            // jumped (#30): the planner's point moving faster than any walk is such a change, not a walk to keep up with.
            var prev = d.pos;
            // How fast the planner's own point moves (the walk), not the slot target, which steps on arrival.
            float pace = LifePlanner.RealWalkSpeed * Mathf.Max(1f, timeScale);
            float targetSpeed = dt > 0f && d.placed ? (spot - d.lastSpot).magnitude / dt : 0f;
            if (targetSpeed > pace * 3f) targetSpeed = pace;
            d.lastSpot = spot;
            if (!d.placed || d.wasHidden || dt <= 0f || snapAll) d.pos = target;
            else
            {
                // Into a slot: straight if the way is clear, else by the place's middle (no cutting across a house corner).
                var goal = a.moving || Clear(d.pos, target) ? target : spot;
                d.pos = Vector2.MoveTowards(d.pos, goal, (targetSpeed + 0.35f) * dt);
            }
            d.wasHidden = hidden;
            float inst = dt > 0f && d.placed ? (d.pos - prev).magnitude / dt : 0f;
            d.speed = Mathf.Lerp(d.speed, inst, dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 1f);
            d.placed = true;

            bool walking = d.speed > 0.04f;
            var style = DollMotion.StyleOf(a.kind);
            if (walking && style != DollStyle.Hop) style = a.kind == ActivityKind.RunOver || (stage == LifeStage.Kid && a.kind == ActivityKind.Play) ? DollStyle.Run : DollStyle.Walk;
            if (hidden) style = DollStyle.Hidden;

            // Props.
            var (kind, slot) = PropOf(a.prop, a.PropSlot, a.kind);
            if (v.propKind != kind || v.propSlot != slot || (kind != PropKind.None && v.prop == null)) v.SetProp(kind, slot);

            // Facing: along the walk, else the partner, the ring's centre, the work or the water.
            float want = d.yaw;
            if (walking && d.pos != prev) want = Yaw(prev, d.pos);
            else if (!walking)
            {
                if (!a.partner.IsNone && dolls.TryGetValue(a.partner.Key, out var p) && p.placed && (p.pos - d.pos).sqrMagnitude < 0.6f * 0.6f && p.pos != d.pos)
                    want = Yaw(d.pos, p.pos);
                else if (face.HasValue && (face.Value - d.pos).sqrMagnitude > 1e-5f) want = Yaw(d.pos, face.Value);
            }
            d.yaw = dt > 0f ? Mathf.MoveTowardsAngle(d.yaw, want, 300f * dt) : want;

            // Pose: the apprentice copies the master a beat late (§4).
            float phase = time + d.phase - (a.copiesPartner ? 0.35f : 0f);
            if (!a.partner.IsNone && !a.copiesPartner && a.kind is ActivityKind.Chat or ActivityKind.Argue) phase += a.who.Key < a.partner.Key ? 0f : 0.65f;
            int steps = ActivityInfo.LoopSteps(a.kind);
            // #92: a story told once (the lens look, the tap-back's knocks) runs on real seconds into the activity.
            float since = a.end > a.start ? LifeClock.RealSecondsBetween(a.start, Mathf.Clamp(now.hour, a.start, a.end)) : 0f;
            bool holds = kind != PropKind.None && kind != PropKind.Mask; // a mask is worn, the hands stay free
            var pose = DollMotion.Evaluate(style, phase, a.pace, a.step, steps, slot, holds, d.speed, a.progress, since, a.count);

            v.gameObject.SetActive(!pose.hidden);
            if (pose.hidden) return;
            float s = v.scale;
            var yawQ = Quaternion.Euler(0, d.yaw + pose.yaw, 0);
            var ground = land.OnGround(d.pos);
            var tilt = pose.lying ? Quaternion.Euler(90f, 0, 0) : Quaternion.Euler(-pose.lean, 0, pose.roll);
            v.transform.SetLocalPositionAndRotation(ground + yawQ * (pose.offset * s), yawQ * tilt);
            v.transform.localScale = new Vector3(1f, pose.squash, 1f);
            v.ApplyArms(pose);
        }

        /// <summary>Where in the place this person stands (rule 6: one doll per slot) and what they face there.</summary>
        Vector2 SlotSpot(PersonActivity a, Vector2 spot, out Vector2? face)
        {
            face = null;
            countAt.TryGetValue(a.place, out int count);
            count = Mathf.Max(1, count);
            float h = Hash01(a.place.GetHashCode(), 11) * Mathf.PI * 2f;
            switch (a.place.kind)
            {
                case PlaceKind.Glass:
                {
                    // #92: a row along the glass, facing it; the wary (stage 1) keep a step back, the kids press right up.
                    var n = GlassNormal(planner.GlassSide);
                    var along = new Vector2(-n.y, n.x);
                    int k = a.slot;
                    float off = k == 0 ? 0f : ((k & 1) == 1 ? 1f : -1f) * ((k + 1) / 2) * 0.13f;
                    float back = a.kind is ActivityKind.GazeWary or ActivityKind.LookUpWary or ActivityKind.WaveWary ? 0.16f
                        : a.kind is ActivityKind.SitOut ? 0.06f : -0.09f; // the kids' noses on the glass
                    var p = spot + along * off - n * back;
                    face = p + n;
                    return p;
                }
                case PlaceKind.Relic:
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
            if (a.place.IsBuilding && lotOfBuilding.TryGetValue(a.place.buildingId, out var lot) && !lot.open &&
                lot.kind != LotKind.Farm && lot.kind != LotKind.Pasture && lot.kind != LotKind.Orchard)
            {
                // At a door: a half-ring outside it, facing the building (the sawyer at the mill, the server at the tavern).
                var centre = new Vector2(lot.x, lot.z);
                var outDir = (spot - centre).sqrMagnitude > 1e-6f ? (spot - centre).normalized : Vector2.down;
                float baseAng = Mathf.Atan2(outDir.y, outDir.x);
                var (dx, dz) = SlotSpots.Door(a.slot, baseAng); // a fan outside the door, no two closer than SlotSpots.Gap
                face = centre;
                return spot + new Vector2(dx, dz);
            }
            // Open ground (a field, the woods, the pond, a heap): a loose spiral round the spot, facing it.
            float spread = a.place.kind is PlaceKind.Field or PlaceKind.Pasture or PlaceKind.Meadow or PlaceKind.Woods or PlaceKind.ForestEdge or PlaceKind.Bushes ? 0.2f : 0.12f;
            if (spread <= SlotSpots.Gap)
            {
                // A heap, a bare doorstep: rings round the spot, nobody closer than the gap.
                var (ox, oz) = SlotSpots.Open(a.slot, h);
                face = a.slot == 0 ? spot + new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * 0.1f : spot;
                return spot + new Vector2(ox, oz);
            }
            if (a.slot == 0) { face = spot + new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * 0.1f; return spot; }
            float ga = h + a.slot * 2.39996f;
            face = spot;
            return spot + new Vector2(Mathf.Cos(ga), Mathf.Sin(ga)) * spread * Mathf.Sqrt(a.slot);
        }

        static bool InField(Place p) => p.kind is PlaceKind.Field or PlaceKind.Pasture;

        /// <summary>#92: out through the front glass (box-local x, z) for a <see cref="LifePlanner.GlassSide"/>.</summary>
        public static Vector2 GlassNormal(int side) => (side & 3) switch { 0 => Vector2.down, 1 => Vector2.right, 2 => Vector2.up, _ => Vector2.left };

        /// <summary>A slot that fell on a building's lot (a ring round the market running into a house) moves to the nearest
        /// walkable ground: round the same ring first, then closer to the middle. The spot itself if nothing is free.</summary>
        static readonly float[] Rings = { 1f, 1.4f, 0.75f, 1.8f, 0.5f };

        Vector2 Standable(Vector2 p, Vector2 centre, bool offLots)
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
                for (int k = 0; k <= 12; k++)
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        float a = a0 + sgn * k * 15f * Mathf.Deg2Rad;
                        var q = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * shrink;
                        if (Free(q)) return q;
                    }
            return centre;
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
        public static (PropKind kind, PropSlot slot) PropOf(LifeProp prop, LifeSlot lifeSlot, ActivityKind activity)
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
                LifeProp.Buckets => PropKind.Bucket,
                LifeProp.Sack or LifeProp.SeedBag => PropKind.Sack,
                LifeProp.Block => PropKind.Block,
                LifeProp.Barrel => PropKind.Barrel,
                LifeProp.Axe => PropKind.Axe,
                LifeProp.Spear => PropKind.Spear,
                LifeProp.Pots or LifeProp.Honey => PropKind.Pot,
                LifeProp.Hoe or LifeProp.Scythe => PropKind.Hoe,
                LifeProp.Hammer or LifeProp.Hammerstone or LifeProp.Pick => PropKind.Hammer,
                LifeProp.FishStick => PropKind.Fish,
                LifeProp.Rod => PropKind.Rod,
                _ => PropKind.None,
            };
            // Fit the slot to the mesh: a bundle rides on the back, buckets hang from the hand, a block is hugged.
            if (kind == PropKind.Bundle && slot != PropSlot.Shoulder) slot = PropSlot.Back;
            else if (kind == PropKind.Bucket) slot = PropSlot.Hand;
            else if (kind == PropKind.Block && slot == PropSlot.Hand) slot = PropSlot.Arms;
            else if (kind == PropKind.Barrel) slot = PropSlot.Front;
            else if (kind == PropKind.Slate && activity == ActivityKind.WriteDown) slot = PropSlot.Arms; // hugged, the hand writes
            return (kind, slot);
        }

        /// <summary>Is this person's doll walking (on a route, or gliding into its slot)?</summary>
        public bool IsWalking(PersonRef who) => dolls.TryGetValue(who.Key, out var d) && d.speed > 0.04f;

        /// <summary>The activity a doll is showing (tests, reports).</summary>
        public bool TryGetActivity(PegDollView view, out PersonActivity act)
        {
            foreach (var d in dolls.Values) if (d.view == view) { act = d.act; return true; }
            act = default;
            return false;
        }

        /// <summary>Where a person's doll stands now (box-local x, z), false if they have none.</summary>
        public bool TryGetDollPosition(PersonRef who, out Vector2 pos)
        {
            pos = default;
            if (!dolls.TryGetValue(who.Key, out var d) || !d.placed) return false;
            pos = d.pos;
            return true;
        }

        /// <summary>#39: the top of a person's doll (just over the head) in world space, false if they have no doll or it is
        /// hidden (indoors, a baby). For world labels (the pin marker) and the person card's relationship lines.</summary>
        public bool TryGetDollTop(PersonRef who, out Vector3 world)
        {
            world = default;
            if (!dolls.TryGetValue(who.Key, out var d) || !d.placed || d.view == null || !d.view.gameObject.activeInHierarchy) return false;
            var t = d.view.transform;
            float top = (PegDoll.HeadY + PegDoll.HeadR) * d.view.scale;
            world = t.parent != null ? t.parent.TransformPoint(t.localPosition + Vector3.up * top) : t.position + Vector3.up * top;
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
            if (n == null) return "Crowd " + who.crowdIndex;
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
            sb.Append(n != null ? n.FullName : "crowd " + a.who.crowdIndex);
            var tags = new List<string>();
            if (role != null) tags.Add(role);
            if (!string.IsNullOrEmpty(job)) tags.Add(job);
            if (planner != null) tags.Add(planner.StageOf(a.who).ToString().ToLowerInvariant());
            sb.Append(" (").Append(string.Join(", ", tags)).Append("): ");
            sb.Append(a.kind);
            sb.Append(a.moving ? " to " : " at ").Append(a.place);
            if (a.moving && a.from != a.place) sb.Append(" from ").Append(a.from);
            var (pk, ps) = PropOf(a.prop, a.PropSlot, a.kind);
            if (pk != PropKind.None) sb.Append(" with ").Append(pk).Append(" (").Append(ps).Append(')');
            if (!a.partner.IsNone) sb.Append(" with ").Append(a.partner);
            if (dolls.TryGetValue(a.who.Key, out var d) && d.view != null)
                sb.Append(string.Format(CultureInfo.InvariantCulture, " · doll {0} at ({1:0.00}, {2:0.00}){3}", d.view.gameObject.activeSelf ? "shown" : "hidden", d.pos.x, d.pos.y, d.speed > 0.04f ? " walking" : ""));
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
            p.now = new ViewTime(p.plannerSim != null ? p.plannerSim.State.month : p.now.day, Mathf.Repeat(hour, 24f)); // a jump: today's month
            p.clockStarted = true;
            p.replanIn = 0f;
            p.Tick(0f);
            return p.Status();
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
