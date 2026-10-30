using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// The activity planner (#29, docs/design/life-in-the-box.md §10): what every person in the box is doing at a view
    /// time. Pure C#, view-side: it reads the sim (buildings, notables, jobs, roles, links, the month's chronicle, mess),
    /// never writes it and never draws from its dice. Every choice is a hash of (seed, person, view day), so a person's
    /// day is a pure function of (seed, person, day, time) and the sim state of that month: opening the app after a
    /// catch-up costs nothing, the planner just answers "what is everyone doing now".
    ///
    /// Interrupt order (rule 10): Keeper reaction (<see cref="React"/>) &gt; story scene (this month's chronicle,
    /// <see cref="StartScene"/>, <see cref="Echo"/>) &gt; role beat &gt; the day's schedule.
    ///
    /// Places are abstract (building ids, "the fire", "field N", "home of family X"); world positions, walking costs and
    /// the homes per family come through <see cref="IPlacePositions"/> (M2's placement layer, <see cref="SettlePlacePositions"/>),
    /// the trait colour through <see cref="ILifeHints"/> (#28, <see cref="TermLifeHints"/>). <see cref="Wired"/> plugs both in;
    /// without them the planner runs on abstract places and plain days.
    /// </summary>
    public sealed partial class LifePlanner
    {
        readonly SimCore sim;
        ILifeHints hints;
        IPlacePositions positions;

        public LifePlanner(SimCore sim, ILifeHints hints = null, IPlacePositions positions = null)
        {
            this.sim = sim ?? throw new ArgumentNullException(nameof(sim));
            this.hints = hints ?? NeutralLifeHints.Instance;
            this.positions = positions ?? NoPlacePositions.Instance;
        }

        /// <summary>The planner wired to the sim: #28's trait colour (<see cref="TermLifeHints"/>) and #32's places, paths and
        /// homes (<see cref="SettlePlacePositions"/>), or abstract places when the sim doesn't settle (<see cref="SimConfig.settle"/>).</summary>
        public static LifePlanner Wired(SimCore sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            return new LifePlanner(sim, new TermLifeHints(sim), sim.Config.settle ? new SettlePlacePositions(sim) : (IPlacePositions)NoPlacePositions.Instance);
        }

        public SimCore Sim => sim;

        /// <summary>Plug in #28's trait flavour (the planner re-plans from the next query).</summary>
        public ILifeHints Hints { get => hints; set { hints = value ?? NeutralLifeHints.Instance; Invalidate(); } }

        /// <summary>Plug in M2's positions (walk lengths, routes, homes per family, who stands near a gesture).</summary>
        public IPlacePositions Positions { get => positions; set { positions = value ?? NoPlacePositions.Instance; Invalidate(); } }

        /// <summary>Debug: force a season (null = follow the sim month).</summary>
        public Season? ForcedSeason { get => forcedSeason; set { forcedSeason = value; ClearDays(); } }
        Season? forcedSeason;

        /// <summary>Debug: force the weather (null = from the month's chronicle and the Keeper's rain).</summary>
        public Weather? ForcedWeather { get => forcedWeather; set { forcedWeather = value; ClearDays(); } }
        Weather? forcedWeather;

        /// <summary>Times a planned place didn't exist and the person fell back to a chore at home (rule 2). Stays 0 unless
        /// the sim changed under a cached day without <see cref="Invalidate"/>.</summary>
        public int FallbackCount { get; private set; }

        /// <summary>Walks a role beat, scene, the weather or a relationship cut in the days planned so far (#64; tests):
        /// each was redirected or re-routed instead of jumping to its old destination.</summary>
        public int InterruptedWalks { get; private set; }

        // ======================================================================== public API

        /// <summary>Everyone in the box now: living notables by id, then the crowd by index.</summary>
        public IReadOnlyList<PersonRef> People { get { Sync(); return peopleRefs; } }

        /// <summary>Plan the whole box for one view time. Slots at each place are numbered in person order (rule 6).</summary>
        public void PlanAll(ViewTime t, List<PersonActivity> into)
        {
            into.Clear();
            Sync();
            EnsureDay(t.day);
            PruneLive(t);
            slotCounter.Clear();
            for (int i = 0; i < people.Count; i++)
            {
                var a = PlanPerson(people[i], t);
                slotCounter.TryGetValue(a.place, out int s);
                a.slot = s;
                slotCounter[a.place] = s + 1;
                into.Add(a);
            }
        }

        /// <summary>One person's activity at a view time. A person no longer in the box gets <see cref="ActivityKind.None"/>.</summary>
        public PersonActivity Plan(PersonRef who, ViewTime t)
        {
            Sync();
            EnsureDay(t.day);
            PruneLive(t);
            return byKey.TryGetValue(who.Key, out var p) ? PlanPerson(p, t) : new PersonActivity { who = who, kind = ActivityKind.None, chronicleId = -1 };
        }

        /// <summary>The next moves in this person's day after <paramref name="t"/> (from, to, when, carrying what).</summary>
        public int UpcomingTrips(PersonRef who, ViewTime t, List<Trip> into, int max = 4)
        {
            into.Clear();
            Sync();
            EnsureDay(t.day);
            if (!byKey.TryGetValue(who.Key, out var p)) return 0;
            var segs = DayOf(p);
            for (int i = 0; i < segs.Length && into.Count < max; i++)
            {
                var s = segs[i];
                if (s.end <= t.hour || !s.Moving) continue;
                into.Add(new Trip { kind = s.kind, from = s.from, to = s.place, start = s.start, end = s.end, prop = s.prop, type = s.trip });
            }
            return into.Count;
        }

        /// <summary>The walk of a trip along the path network, door to door (<see cref="IPlacePositions.TryRoute"/>). False
        /// without positions or when there is no way; the view then walks straight.</summary>
        public bool RouteOf(Trip trip, List<Waypoint> into) => RouteOf(trip.from, trip.to, into);

        /// <summary>The walk from one place to another along the path network (false without positions).</summary>
        public bool RouteOf(Place from, Place to, List<Waypoint> into) => positions.TryRoute(from, to, into);

        /// <summary>The trash heap this sweeper heads to first (<see cref="SimCore.SweeperTargets"/>: one heap per sweeper,
        /// the oldest first), or -1 (not a town sweeper, or no heap left for them: they sweep the streets).</summary>
        public int SweeperTargetOf(PersonRef who)
        {
            Sync();
            return byKey.TryGetValue(who.Key, out var p) && p.sweeper >= 0 ? SweepHeap(p, 0, 0) : -1;
        }

        /// <summary>The whole planned day of a person (debug panel, tests).</summary>
        public int DaySchedule(PersonRef who, long day, List<PersonActivity> into)
        {
            into.Clear();
            Sync();
            EnsureDay(day);
            if (!byKey.TryGetValue(who.Key, out var p)) return 0;
            foreach (var s in DayOf(p)) into.Add(ToActivity(p, s, new ViewTime(day, s.start)));
            return into.Count;
        }

        /// <summary>A person's stage of life as the planner sees it (crowd stages come from the index, rule 13).</summary>
        public LifeStage StageOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) ? p.stage : LifeStage.None; }

        /// <summary>The crowd job a crowd member draws from the buildings (§5), or the notable's JobType ("" = chores).</summary>
        public string JobOf(PersonRef who) { Sync(); return byKey.TryGetValue(who.Key, out var p) ? p.job : null; }

        /// <summary>The weather the planner uses on this view day.</summary>
        public Weather WeatherOn(long day) { Sync(); EnsureDay(day); return dayInfo.weather; }

        public Season SeasonOn(long day) => forcedSeason ?? LifeClock.SeasonOf(day);

        public bool IsRestDay(long day) { Sync(); EnsureDay(day); return dayInfo.rest; }

        /// <summary>The sim changed outside a month step (debug cheats, a load): re-read it on the next query.</summary>
        public void Invalidate() { snapSig = long.MinValue; ClearDays(); }

        /// <summary>Whether a place exists in the box now (building, trash heap, nudge; land always exists).</summary>
        public bool IsValidPlace(Place p)
        {
            Sync();
            switch (p.kind)
            {
                case PlaceKind.None: return false;
                case PlaceKind.TrashHeap: return trashIds.Contains(p.index);
                case PlaceKind.Nudge: return nudgeIds.Contains(p.index);
                case PlaceKind.Relic: return p.index >= 0 && p.index < relicHere.Length && relicHere[p.index]; // #92
            }
            return !p.IsBuilding || buildingIds.Contains(p.buildingId);
        }

        // ======================================================================== the query

        readonly Dictionary<Place, int> slotCounter = new Dictionary<Place, int>();

        PersonActivity PlanPerson(Person p, ViewTime t)
        {
            // 1+2. Keeper reactions (≤ 4 s) and live story scenes (echoes from "Show me", the debug panel, the evening talk),
            // with the walk back into the day after them (#64).
            var blocks = BlocksOf(p);
            for (int b = 0; b < blocks.Length; b++)
                if (t.hour >= blocks[b].bs && t.hour < blocks[b].be) return BlockActivity(p, blocks[b], t);
            // 3+4. The day: this month's story scenes > role beats > the schedule, already layered.
            var segs = DayOf(p);
            int i = Find(segs, t.hour);
            return ToActivity(p, segs[i], t);
        }

        static int Find(Seg[] segs, float hour)
        {
            int lo = 0, hi = segs.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (segs[mid].start <= hour) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        PersonActivity ToActivity(Person p, Seg s, ViewTime t)
        {
            var place = s.place;
            var from = s.Moving ? s.from : s.place;
            var kind = s.kind;
            if (!IsValidPlace(place) || !IsValidPlace(from))
            {
                FallbackCount++;
                place = from = ValidHome(p);
                kind = ActivityKind.SweepStep;
            }
            float len = Math.Max(1e-4f, s.end - s.start);
            float into = Math.Max(0f, t.hour - s.start);
            int steps = ActivityInfo.LoopSteps(kind);
            return new PersonActivity
            {
                who = p.who, kind = kind, place = place, from = from, moving = s.Moving && from != place, partner = s.partner,
                prop = s.prop, start = s.start, end = s.end, progress = Math.Min(1f, into / len),
                step = steps <= 1 ? 0 : (int)(into / StepHours) % steps, source = s.src, trip = s.trip, pace = dayInfo.pace,
                chronicleId = s.chronicleId, copiesPartner = s.copies, facesGlass = s.faces, count = s.count,
            };
        }

        const float StepHours = 0.25f;

        Place ValidHome(Person p)
        {
            if (IsValidPlace(p.home)) return p.home;
            return fire;
        }

        // ======================================================================== snapshot of the sim

        /// <summary>A person as the planner sees them this month.</summary>
        sealed class Person
        {
            public PersonRef who;
            public int key;
            public Notable n;           // null for the crowd
            public LifeStage stage;
            public float age;
            public string job = "";     // JobType, or a crowd slot job
            public Building jobBuilding;// the crowd slot's building, the farmer's field
            public int jobSlotRank;
            public string role;         // RoleType or null
            public Place home;
            public Person partner, friend, rival, mentor, parent, trails, coworker;
            public PersonHints hints;
            public int sweeper = -1;    // the town sweeper's number (SimCore.SweeperTarget), -1 = not a sweeper
            public bool reconciledWithSomeone;
            public Person reconciled;
            public bool Adult => stage == LifeStage.Adult;
            public bool Grown => stage == LifeStage.Adult || stage == LifeStage.Elder;
            public string Name => n != null ? n.FullName : null;
        }


        // Crowd slot jobs (life-in-the-box §5) beside the notable JobType strings.
        const string CrowdCook = "cook", CrowdSplitter = "wood splitter", CrowdSawyer = "sawyer", CrowdMillhand = "mill hand",
            CrowdServer = "server", CrowdHelper = "";

        long snapSig = long.MinValue;
        readonly List<Person> people = new List<Person>();
        readonly List<PersonRef> peopleRefs = new List<PersonRef>();
        readonly Dictionary<int, Person> byKey = new Dictionary<int, Person>();
        readonly HashSet<int> buildingIds = new HashSet<int>();
        readonly HashSet<int> trashIds = new HashSet<int>();
        readonly HashSet<int> nudgeIds = new HashSet<int>();
        readonly List<int> trashList = new List<int>();
        readonly List<Building> farms = new List<Building>(), homes = new List<Building>(), workshops = new List<Building>();
        readonly Dictionary<int, Building> buildingById = new Dictionary<int, Building>();
        readonly Dictionary<int, Notable> notableById = new Dictionary<int, Notable>();
        readonly List<int> tmpIds = new List<int>();
        // Town care (#60): the heaps in the sweepers' order (oldest first) and each sweeper's first one.
        readonly List<int> sweepOrder = new List<int>(), sweepTargets = new List<int>();
        int sweepersTotal;
        Building campB, wellB, marketB, tavernB, churchB, windmillB, lumberB, guildB, newestB;
        Place fire, gather, remembering;
        Era era;
        bool eraReady;
        Person leader;
        BoxHints box;
        int crowdCount;

        void Sync()
        {
            long sig = Signature();
            if (sig == snapSig) return;
            snapSig = sig;
            polyCache.Clear();
            Snapshot();
            ClearDays();
        }

        /// <summary>Cheap check that the sim moved: month, counts, era, role holders.</summary>
        long Signature()
        {
            var s = sim.State;
            long h = s.month;
            h = h * 31 + s.buildings.Count;
            h = h * 31 + s.notables.Count;
            h = h * 31 + s.population;
            h = h * 31 + s.chronicle.Count;
            h = h * 31 + s.trash.Count;
            h = h * 31 + s.nudges.Count;
            h = h * 31 + (int)s.era * 2 + (s.eraReady ? 1 : 0);
            h = h * 31 + sim.BeliefStage * 2 + (sim.BeliefHalfNow == BeliefHalf.Later ? 1 : 0); // #92: how they see the glass
            if (s.roles != null)
                for (int i = 0; i < s.roles.Count; i++) h = h * 31 + s.roles[i].holderId * (i + 7);
            return h;
        }

        void Snapshot()
        {
            var s = sim.State;
            era = s.era;
            eraReady = s.eraReady;

            buildingIds.Clear(); farms.Clear(); homes.Clear(); workshops.Clear(); buildingById.Clear();
            campB = wellB = marketB = tavernB = churchB = windmillB = lumberB = guildB = newestB = null;
            foreach (var b in s.buildings)
            {
                buildingIds.Add(b.id);
                buildingById[b.id] = b;
                switch (b.type)
                {
                    case BuildingType.Camp: campB ??= b; break;
                    case BuildingType.Hut: homes.Add(b); break;
                    case BuildingType.Farm: farms.Add(b); break;
                    case BuildingType.Lumbermill: lumberB ??= b; break;
                    case BuildingType.Well: wellB ??= b; break;
                    case BuildingType.Windmill: windmillB ??= b; break;
                    case BuildingType.Market: marketB ??= b; break;
                    case BuildingType.Tavern: tavernB ??= b; break;
                    case BuildingType.Church: churchB ??= b; break;
                    case BuildingType.Workshop: workshops.Add(b); break;
                    case BuildingType.Guildhall: guildB ??= b; break;
                }
                if (newestB == null || b.foundedMonth > newestB.foundedMonth || (b.foundedMonth == newestB.foundedMonth && b.id > newestB.id)) newestB = b;
            }
            // Placed homes (#32): the huts that stand on a lot, in the settlement's order.
            positions.GetHomes(tmpIds);
            if (tmpIds.Count > 0)
            {
                int before = homes.Count;
                foreach (int id in tmpIds)
                    if (buildingById.TryGetValue(id, out var hb)) homes.Add(hb);
                if (homes.Count > before) homes.RemoveRange(0, before);
            }
            var any = campB ?? (s.buildings.Count > 0 ? s.buildings[0] : null);
            if (homes.Count == 0 && any != null) homes.Add(any); // tents round the fire
            fire = campB != null ? new Place(PlaceKind.Fire, campB.id) : any != null ? PlaceOf(any) : Place.Land(PlaceKind.Hill);
            gather = era == Era.Camp ? fire
                : era == Era.Village ? (wellB != null ? PlaceOf(wellB) : fire)
                : marketB != null ? PlaceOf(marketB) : wellB != null ? PlaceOf(wellB) : fire;
            remembering = Place.Land(PlaceKind.Remembering, churchB != null ? 1 : 0); // a cairn, later the churchyard

            trashIds.Clear(); trashList.Clear();
            foreach (var t in s.trash) { trashIds.Add(t.id); trashList.Add(t.id); }
            nudgeIds.Clear();
            foreach (var n in s.nudges) nudgeIds.Add(n.id);

            // ---- notables
            people.Clear(); peopleRefs.Clear(); byKey.Clear();
            var family = new Dictionary<string, int>();
            notableById.Clear();
            foreach (var n in s.notables) // the dead stay in the list, so family order is stable
            {
                notableById[n.id] = n;
                if (n.family != null && !family.ContainsKey(n.family)) family[n.family] = family.Count;
            }
            var roleOf = new Dictionary<int, string>();
            if (s.roles != null)
                foreach (var seat in s.roles)
                    if (seat.holderId >= 0 && !roleOf.ContainsKey(seat.holderId)) roleOf[seat.holderId] = seat.role;

            int alive = 0;
            foreach (var n in s.notables)
            {
                if (!n.alive) continue;
                alive++;
                var p = new Person { who = PersonRef.Notable(n.id), key = n.id, n = n, age = n.age, job = n.job ?? "" };
                p.stage = n.age < 4f ? LifeStage.Baby : n.age < SimCore.AdultAge ? LifeStage.Kid
                    : (n.age >= sim.Config.retireAge || n.job == JobType.Elder) ? LifeStage.Elder : LifeStage.Adult;
                roleOf.TryGetValue(n.id, out p.role);
                int fam = n.family != null && family.TryGetValue(n.family, out var fi) ? fi : 0;
                if (positions.TryGetHome(n, out int homeId, out int headId) && buildingIds.Contains(homeId))
                {
                    // #32: the household's home (children with a parent, partners together), as the head's family's place.
                    int hf = notableById.TryGetValue(headId, out var head) && head.family != null && family.TryGetValue(head.family, out var hfi) ? hfi : fam;
                    p.home = new Place(PlaceKind.Home, homeId, hf);
                }
                else p.home = new Place(PlaceKind.Home, homes.Count > 0 ? homes[fam % homes.Count].id : -1, fam); // stand-in
                p.hints = hints.Person(n, p.role);
                Add(p);
            }
            foreach (var p in people)
            {
                var n = p.n;
                p.partner = Alive(n.partnerId);
                p.friend = Alive(n.friendId);
                p.rival = Alive(n.rivalId);
                p.parent = Alive(n.parentId);
                var m = Alive(n.mentorId);
                p.mentor = m != null && p.age < 20f && m.Adult ? m : null; // the sim's apprentices are 8–20
                if (p.job == JobType.Farmer && farms.Count > 0) p.jobBuilding = farms[(int)(H(p.key, 0, 71) % (uint)farms.Count)];
            }

            // ---- reconciled pairs: a make-up in the last ten years, and not rivals again
            long since = s.month - 120;
            for (int i = s.chronicle.Count - 1; i >= 0 && s.chronicle[i].month >= since; i--)
            {
                var e = s.chronicle[i];
                if (e.kind != "reconcile") continue;
                if (!byKey.TryGetValue(e.notableId, out var a) || !byKey.TryGetValue(e.otherNotableId, out var b)) continue;
                if (a.rival == b || a.reconciled != null || b.reconciled != null) continue;
                a.reconciled = b; b.reconciled = a;
            }

            // ---- roles: the leader, the heir, the box-wide trait colour (#28)
            leader = null;
            box = default;
            if (s.roles != null)
                foreach (var seat in s.roles)
                {
                    if (seat.holderId < 0 || !byKey.TryGetValue(seat.holderId, out var h)) continue;
                    if (seat.role == SimCore.LeaderOf(era)) leader = h;
                    var o = hints.Office(h.n, seat.role);
                    box.homeEarlyHours = Math.Max(box.homeEarlyHours, o.homeEarlyHours);
                    box.sweeperExtraBlocks += o.sweeperExtraBlocks;
                    box.feastChance = Math.Max(box.feastChance, o.feastChance);
                    box.announceChance = Math.Max(box.announceChance, o.announceChance);
                    if (seat.role == RoleType.Lord)
                    {
                        Person heir = null;
                        foreach (var c in people)
                            if (c.parent == h && c.stage != LifeStage.Baby && (heir == null || c.age > heir.age)) heir = c;
                        if (heir != null && heir.role == null) heir.trails = h;
                    }
                }

            // ---- the crowd: a count in the sim; ~25 % kids, a few elders, adults take building slots (§5)
            crowdCount = Math.Max(0, s.population - alive);
            var slots = CrowdSlots();
            int nextSlot = 0;
            for (int i = 0; i < crowdCount; i++)
            {
                var p = new Person { who = PersonRef.Crowd(i), key = -(i + 1) };
                if (i % 4 == 3) { p.stage = i % 8 == 7 ? LifeStage.Baby : LifeStage.Kid; p.age = i % 8 == 7 ? 2f : 5f + H(p.key, 0, 5) % 10; }
                else if (i % 12 == 5) { p.stage = LifeStage.Elder; p.age = 66f + H(p.key, 0, 5) % 12; }
                else { p.stage = LifeStage.Adult; p.age = 18f + H(p.key, 0, 5) % 40; }
                int hutCount = homes.Count;
                p.home = new Place(PlaceKind.Home, hutCount > 0 ? homes[(i + family.Count) % hutCount].id : -1, -1);
                if (p.stage == LifeStage.Adult)
                {
                    if (nextSlot < slots.Count)
                    {
                        var sl = slots[nextSlot++];
                        p.job = sl.job; p.jobBuilding = sl.building; p.jobSlotRank = sl.rank;
                    }
                    else p.job = CrowdHelper;
                }
                Add(p);
            }
            // Two-man saws: the sawyers at one mill work as a pair.
            foreach (var p in people)
                if (p.job == CrowdSawyer && p.coworker == null)
                    foreach (var q in people)
                        if (q != p && q.job == CrowdSawyer && q.coworker == null && q.jobBuilding == p.jobBuilding) { p.coworker = q; q.coworker = p; break; }

            // ---- town care (#60): sweepers are numbered notables first (by id), then the crowd's; sweeper i heads to
            // SimCore.SweeperTarget(i), then to every sweepersTotal-th heap after it in the sweeping order.
            sweepOrder.Clear(); sweepTargets.Clear(); sweepersTotal = 0;
            if (era == Era.Town)
            {
                int idx = 0;
                tmpIds.Clear();
                foreach (var p in people) if (p.n != null && p.job == JobType.Sweeper && p.Grown) tmpIds.Add(p.key);
                tmpIds.Sort();
                foreach (int key in tmpIds) byKey[key].sweeper = idx++;
                foreach (var p in people) if (p.n == null && p.job == JobType.Sweeper) p.sweeper = idx++;
                sweepTargets.AddRange(sim.SweeperTargets());
                var heaps = new List<TrashItem>(s.trash);
                heaps.Sort((a, c) => a.month != c.month ? a.month.CompareTo(c.month) : a.id.CompareTo(c.id));
                foreach (var t in heaps) sweepOrder.Add(t.id);
                sweepersTotal = Math.Max(1, Math.Max(sim.Sweepers, idx));
            }

            dayFireKeeper = null;
            SnapshotGlass(); // #92
        }

        void Add(Person p)
        {
            people.Add(p);
            peopleRefs.Add(p.who);
            byKey[p.key] = p;
        }

        Person Alive(int id) => id >= 0 && byKey.TryGetValue(id, out var p) ? p : null;

        struct CrowdSlot { public int rank, priority, order; public string job; public Building building; }

        /// <summary>Crowd slots per building, filled round-robin in priority order: every building's first slot before any
        /// second one, so a small settlement shows its most important work first (the field gets its first farmer before
        /// the mill its second sawyer).</summary>
        List<CrowdSlot> CrowdSlots()
        {
            var list = new List<CrowdSlot>();
            int order = 0;
            void Slots(Building b, int priority, int count, string job)
            {
                for (int r = 0; r < count; r++) list.Add(new CrowdSlot { rank = r, priority = priority, order = order++, job = job, building = b });
            }
            if (campB != null) Slots(campB, 0, 1, CrowdCook);
            foreach (var f in farms) Slots(f, 1, era == Era.Camp ? 2 : 3, era == Era.Camp ? JobType.Gatherer : JobType.Farmer);
            if (lumberB != null) Slots(lumberB, 2, era == Era.Camp ? 1 : 2, era == Era.Camp ? CrowdSplitter : CrowdSawyer);
            if (windmillB != null) Slots(windmillB, 3, 1, CrowdMillhand);
            if (marketB != null) Slots(marketB, 4, 2, JobType.Stallholder);
            if (tavernB != null) Slots(tavernB, 5, 1, CrowdServer);
            foreach (var w in workshops) Slots(w, 6, 2, JobType.Weaver);
            if (era == Era.Town) Slots(null, 7, sim.CrowdSweepers, JobType.Sweeper); // the sim's crowd sweepers (#60): 1 per 12 people
            // The sim's crowd sweepers come first: they are counted in the sim (its sweep rate, SweeperTargets), so a crowd
            // that runs out of adults must still show every one of them (#109). Then rank, priority, order.
            list.Sort((a, b) =>
            {
                bool sa = a.job == JobType.Sweeper, sb = b.job == JobType.Sweeper;
                if (sa != sb) return sa ? -1 : 1;
                return a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.priority != b.priority ? a.priority.CompareTo(b.priority) : a.order.CompareTo(b.order);
            });
            return list;
        }

        static Place PlaceOf(Building b)
        {
            if (b == null) return Place.None;
            var k = b.type switch
            {
                BuildingType.Camp => PlaceKind.Fire,
                BuildingType.Hut => PlaceKind.Home,
                BuildingType.Farm => PlaceKind.Field,
                BuildingType.Lumbermill => PlaceKind.LumberMill,
                BuildingType.Well => PlaceKind.Green,
                BuildingType.Windmill => PlaceKind.Windmill,
                BuildingType.Market => PlaceKind.Market,
                BuildingType.Tavern => PlaceKind.Tavern,
                BuildingType.Church => PlaceKind.Shrine,
                BuildingType.Workshop => PlaceKind.Workshop,
                BuildingType.Guildhall => PlaceKind.Guildhall,
                _ => PlaceKind.BuildingSite,
            };
            return new Place(k, b.id);
        }

        // ======================================================================== hashing (never the sim's dice)

        uint H(int key, long day, int salt)
        {
            uint h = (uint)sim.State.seed * 0x9E3779B1u ^ (uint)key * 0x85EBCA77u ^ (uint)day * 0xC2B2AE3Du ^ (uint)(day >> 32) * 0x165667B1u ^ (uint)salt * 0x27D4EB2Fu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return h;
        }

        /// <summary>0..1 from (seed, person, day, salt).</summary>
        float R(int key, long day, int salt) => (H(key, day, salt) >> 8) * (1f / 16777216f);

        static int PairKey(Person a, Person b) => Math.Min(a.key, b.key) * 7919 + Math.Max(a.key, b.key);

        /// <summary>How long a walk takes: the length of the route the view walks (rule 3: chosen by the path network's
        /// walking cost, the same cost the wear pass walks with), else the straight line, else a flat quarter hour. The
        /// time comes from the route's real length, not its cost (they differ both ways: worn paths are cheaper, rough ground dearer); a
        /// cost-timed walk along a road ran 3–4× <see cref="WalkSpeed"/> on screen (#30).</summary>
        float WalkHours(Place a, Place b)
        {
            if (a == b) return 0f;
            var route = RouteBetween(a, b);
            if (route != null && route.len > 1e-4f) return Math.Max(MinWalk, Math.Min(MaxWalk, route.len / WalkSpeed));
            if (positions.TryWalkLength(a, b, out float len)) return Math.Max(MinWalk, Math.Min(MaxWalk, len / WalkSpeed));
            if (positions.TryGetPosition(a, out float ax, out float az) && positions.TryGetPosition(b, out float bx, out float bz))
            {
                float dx = ax - bx, dz = az - bz;
                return Math.Max(MinWalk, Math.Min(MaxWalk, (float)Math.Sqrt(dx * dx + dz * dz) / WalkSpeed));
            }
            return 0.25f;
        }

        /// <summary>World units of plain ground walked per view hour [est], and the shortest and longest walk in view hours.</summary>
        public const float WalkSpeed = 5f, MinWalk = 0.08f, MaxWalk = 0.6f;

        /// <summary>The walking pace on screen, in world units per real second: <see cref="WalkSpeed"/> at the work hours'
        /// clock rate. Night hours run ~3× faster (<see cref="LifeClock"/>), so a walk timed in view hours alone rushes
        /// after nightfall; <see cref="PaceNightWalks"/> holds walks to this pace.</summary>
        public static float RealWalkSpeed => WalkSpeed / LifeClock.RealSecondsPerViewHour(LifeClock.WorkStart);
    }
}
