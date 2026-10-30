using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Life
{
    /// <summary>
    /// The people at the glass (#92; relics-and-glass-voice.md §1, §3, §5). Belief (the sim's <see cref="SimCore.BeliefStage"/>
    /// and <see cref="SimCore.BeliefHalfNow"/>, #74) decides how they behave at the front glass:
    /// <list type="bullet">
    /// <item><b>Stage 1 (camp, a bit scared):</b> after a wipe 3–6 people stand a step back from the glass and look up, at
    /// most once a view day; once the Sky Crumb fell, a few come most afternoons to look up where the bread came from.</item>
    /// <item><b>Stage 2 (village, play):</b> 1–3 kids most afternoons pressing their hands to the glass (after the Keeper
    /// Doll they wave at the doll first), a few adults sitting facing it at dusk; when you return the kids run to the glass to
    /// look for you.</item>
    /// <item><b>Stage 3 (town, study):</b> the clever argue about "the wall" at it; after the Lens a scholar measures along it
    /// most mornings, the Lens's scholar looks through it some evenings, and a scholar taps back when you tap.</item>
    /// </list>
    /// The relic moments (<see cref="RelicMoments"/>) are live scenes the view starts when the sim finds a relic
    /// (<see cref="SimCore.RelicFound"/>). Everything is a hash of (seed, person, day): no sim dice, no sim writes. Walks to
    /// the glass are planned like any other (a plain stay too far to reach is skipped; a live one only casts people who can
    /// walk there at the walking pace).
    /// </summary>
    public sealed partial class LifePlanner
    {
        // ======================================================================== public API

        /// <summary>The wall the people treat as the front glass (0 = -z, 1 = +x, 2 = +z, 3 = -x). The view sets it from the
        /// camera (<see cref="GlassSideFacing"/>), at most at a view day's turn: changing it re-plans the day.</summary>
        public int GlassSide
        {
            get => glassSide;
            set { int v = ((value % 4) + 4) % 4; if (v == glassSide) return; glassSide = v; Invalidate(); }
        }
        int glassSide;

        /// <summary>The wall a camera at this orbit yaw (BoxCamera's degrees) looks in through: the camera sits at
        /// (-sin yaw, -cos yaw) from the centre, so yaw 0 looks in through the -z wall.</summary>
        public static int GlassSideFacing(float yawDegrees)
        {
            double a = yawDegrees * Math.PI / 180.0;
            double dx = -Math.Sin(a), dz = -Math.Cos(a);
            if (Math.Abs(dz) / Terrain.HZ >= Math.Abs(dx) / Terrain.HX) return dz < 0 ? 0 : 2;
            return dx > 0 ? 1 : 3;
        }

        /// <summary>The front glass as a place.</summary>
        public Place GlassPlace => Place.Land(PlaceKind.Glass, glassSide);

        /// <summary>A relic's spot as a place (None for an unknown id).</summary>
        public static Place RelicPlace(string relicId)
        {
            int i = Array.IndexOf(RelicId.All, relicId);
            return i < 0 ? Place.None : Place.Land(PlaceKind.Relic, i);
        }

        /// <summary>The belief stage (1–3) and half the planner plans with (the sim's, re-read when it changes).</summary>
        public int BeliefStageSeen { get { Sync(); return belief; } }
        public bool BeliefLaterSeen { get { Sync(); return beliefLater; } }

        /// <summary>
        /// A gathering at the glass (§3 "Gathering"): stage 1, 3–6 people a step back looking up (after a wipe or the Sky
        /// Crumb); stage 2 and 3, the kids run to it to look for you (after a return). Only people who can walk there at the
        /// walking pace come, at most 6. <see cref="React"/> starts one by itself (a wipe in stage 1, a return later), at most
        /// once a view day; <paramref name="force"/> (debug) ignores that. Returns how many come.
        /// </summary>
        public int GatherAtGlass(ViewTime at, bool force = false)
        {
            Sync();
            EnsureDay(at.day);
            if (!force && gatheredDay == at.day) return 0;
            float end = EndAfter(at, GatheringSeconds);
            var st = new Staging { glass = GlassPlace };
            var glass = GlassPlace;
            var cands = new List<(Person p, float len)>();
            foreach (var p in people)
            {
                if (p.stage == LifeStage.Baby) continue;
                if (belief >= 2 && p.stage != LifeStage.Kid) continue; // later stages: the kids run to look for you
                if (DayOf(p)[Find(DayOf(p), at.hour)].kind == ActivityKind.Sleep) continue;
                var from = StopPlace(p, at.hour);
                float len = WalkLen(from, glass);
                if (WalkSecs(len) > 0.5f * LifeClock.RealSecondsBetween(at.hour, end)) continue;
                cands.Add((p, len));
            }
            cands.Sort((a, b) => a.len != b.len ? a.len.CompareTo(b.len) : a.p.key.CompareTo(b.p.key));
            int max = belief >= 2 ? 3 : MaxAtGlass;
            for (int i = 0; i < cands.Count && st.segs.Count < max; i++)
            {
                var p = cands[i].p;
                var s = belief >= 2
                    ? Mk(ActivityKind.PressHands, at.hour, end, glass, Prop.None, ActivitySource.Scene)
                    : Mk(ActivityKind.GazeWary, at.hour, end, glass, Prop.None, ActivitySource.Scene);
                s.faces = true;
                st.segs[p.key] = new List<Seg> { s };
                st.order.Add(p.key);
            }
            if (st.segs.Count == 0) return 0;
            gatheredDay = at.day;
            AddLive(SceneKind.GlassGathering, at, end, st);
            return st.segs.Count;
        }

        /// <summary>
        /// Stage 3's tap-back (Choice 9): the Keeper tapped <paramref name="taps"/> times and the sim named the scholar who
        /// taps back (<see cref="SimCore.KeeperTapped"/>, or <see cref="SimCore.DebugTapBack"/> from the panel; the view calls
        /// the sim, then this). The scholar walks to the glass at the walking pace, knocks the same number of times
        /// (<see cref="ActivityKind.TapBack"/>, <see cref="PersonActivity.count"/>), then writes it on a slate. False if the
        /// scholar isn't in the box.
        /// </summary>
        public bool TapBack(ViewTime at, int scholarId, int taps)
        {
            Sync();
            EnsureDay(at.day);
            var p = Alive(scholarId);
            if (p == null || taps <= 0) return false;
            var glass = GlassPlace;
            float walk = WalkSecs(WalkLen(StopPlace(p, at.hour), glass));
            var t = at;
            var st = new Staging { glass = glass };
            var list = new List<Seg>(3);
            if (walk > 0f)
            {
                var arrive = LifeClock.Advance(t, walk + 0.5f); // hurrying over at the walking pace, a look up, then the knocks
                var s0 = Mk(ActivityKind.Gaze, t.hour, HourOf(at, arrive), glass, Prop.Slate, ActivitySource.Scene);
                s0.faces = true;
                list.Add(s0);
                t = arrive;
            }
            var knocked = LifeClock.Advance(t, 1f + 0.5f * taps);
            var s1 = Mk(ActivityKind.TapBack, HourOf(at, t), HourOf(at, knocked), glass, Prop.Slate, ActivitySource.Scene);
            s1.faces = true; s1.count = taps;
            list.Add(s1);
            var written = LifeClock.Advance(knocked, 3f);
            var s2 = Mk(ActivityKind.WriteDown, HourOf(at, knocked), HourOf(at, written), glass, Prop.Slate, ActivitySource.Scene);
            s2.count = taps;
            list.Add(s2);
            st.segs[p.key] = list;
            st.order.Add(p.key);
            AddLive(SceneKind.TapBack, at, HourOf(at, written), st, p.key);
            return true;
        }

        /// <summary>
        /// Stage a relic's moment now (§1; <see cref="RelicMoments"/>): the view calls it when <see cref="SimCore.RelicFound"/>
        /// fires while it watches (the Lens best at dusk: <see cref="RelicMomentDef.preferredHour"/>), the debug panel any
        /// time. The principals come from the relic (owner, holder), the rest are whoever can walk there at the walking pace.
        /// Null when the relic isn't found or nobody can play it.
        /// </summary>
        public RelicMomentInfo StartRelicMoment(string relicId, ViewTime at)
        {
            Sync();
            EnsureDay(at.day);
            var r = sim.FindRelic(relicId);
            var def = RelicMoments.For(relicId);
            if (r == null || !r.Found || def == null) return null;
            float end = EndAfter(at, def.seconds);
            var st = new Staging { relicId = relicId, def = def, glass = GlassPlace, spot = relicId == RelicId.FestivalMask ? gather : RelicPlace(relicId), start = at.hour, end = end };
            var used = new HashSet<int>();

            // The principals.
            var owner = Alive(r.ownerId);
            Person second = null, holder = null;
            bool heir = false;
            if (relicId == RelicId.LeftSandal)
            {
                holder = Alive(r.holderId);
                heir = holder != null && r.holderId != r.ownerId;
                owner = null; // the owner plays the holder (or is dead)
            }
            Cast(st, RelicCast.Owner, owner, at, used);
            if (relicId == RelicId.LeftSandal) Cast(st, heir ? RelicCast.Heir : RelicCast.Holder, holder, at, used);
            if (relicId == RelicId.SkyCrumb)
            {
                foreach (var p in people)
                    if (p.n != null && p.Grown && p.n.trait == "the brave" && !used.Contains(p.key)) { second = p; break; }
                second ??= Nearest(st.spot, at, used, q => q.Grown);
                Cast(st, RelicCast.Second, second, at, used);
            }
            if (relicId == RelicId.LeftSandal)
            {
                second = Nearest(st.spot, at, used, q => q.Grown);
                Cast(st, RelicCast.Second, second, at, used);
            }
            // The crowd: nearest first.
            CastCrowd(st, RelicCast.Kids, at, used, def.maxKids, q => q.stage == LifeStage.Kid);
            CastCrowd(st, RelicCast.Grown, at, used, def.maxGrown, q => q.Grown);
            CastCrowd(st, RelicCast.Queue, at, used, def.maxQueue, q => q.stage != LifeStage.Baby);
            if (st.segs.Count == 0) return null;
            AddLive(SceneKind.Relic, at, end, st);
            return st.Info(at.day);
        }

        /// <summary>The relic moments playing at <paramref name="t"/> (the view's bubbles, the debug readout).</summary>
        public int ActiveRelicMoments(ViewTime t, List<RelicMomentInfo> into)
        {
            into.Clear();
            foreach (var sc in live)
                if (sc.kind == SceneKind.Relic && !sc.echo && sc.staging != null && sc.day == t.day && t.hour >= sc.start && t.hour < sc.end)
                    into.Add(sc.staging.Info(sc.day));
            return into.Count;
        }

        /// <summary>The lines said now in the relic moments playing (a bubble over the speaker for the beat's length): the
        /// bank's line (<see cref="SimCore.LineBank"/>) with the speaker's name filled in.</summary>
        public int RelicLinesAt(ViewTime t, List<RelicLine> into)
        {
            into.Clear();
            foreach (var sc in live)
            {
                var st = sc.staging;
                if (sc.kind != SceneKind.Relic || sc.echo || st?.def == null || sc.day != t.day) continue;
                foreach (var beat in st.def.beats)
                {
                    if (beat.lineNo <= 0) continue;
                    float b0 = st.T(beat.from), b1 = st.T(beat.to);
                    if (t.hour < b0 || t.hour >= b1) continue;
                    int speakerKey = 0;
                    foreach (var k in st.order) if (st.roles[k] == beat.cast) { speakerKey = k; break; }
                    if (speakerKey == 0) continue; // nobody of that group came
                    var line = Array.Find(SimCore.LineBank, l => l.no == beat.lineNo);
                    if (line == null) continue;
                    byKey.TryGetValue(speakerKey, out var sp);
                    string text = line.text.Replace("{0}", sp?.Name ?? "Someone");
                    if (era == Era.Camp) text = text.Replace("the window", "the sky"); // the camp has no window yet (as the sim's card)
                    into.Add(new RelicLine { relicId = st.relicId, lineNo = line.no, text = text, speaker = sp?.who ?? PersonRef.None, start = b0, end = b1 });
                }
            }
            return into.Count;
        }

        /// <summary>Debug panel / tests: the day's planned visits to the glass (who, what, when), in person order.</summary>
        public int GlassVisits(long day, List<PersonActivity> into)
        {
            into.Clear();
            Sync();
            EnsureDay(day);
            foreach (var p in people)
                if (dayInfo.glass.TryGetValue(p.key, out var list))
                    foreach (var s in list) into.Add(ToActivity(p, s, new ViewTime(day, s.start)));
            return into.Count;
        }

        // ======================================================================== the belief snapshot

        /// <summary>Never more than this many at the glass, so work goes on (§3).</summary>
        public const int MaxAtGlass = 6;
        const float GatheringSeconds = 18f;

        int belief = 1;
        bool beliefLater;
        readonly bool[] relicHere = new bool[5];
        readonly List<Person> scholars = new List<Person>();
        Person lensKeeper;
        long gatheredDay = long.MinValue;

        void SnapshotGlass()
        {
            belief = sim.BeliefStage;
            beliefLater = sim.BeliefHalfNow == BeliefHalf.Later;
            for (int i = 0; i < RelicId.All.Length; i++) relicHere[i] = sim.FindRelic(RelicId.All[i]) != null;
            scholars.Clear();
            foreach (var n in sim.Scholars) if (byKey.TryGetValue(n.id, out var p)) scholars.Add(p);
            var lens = sim.FindRelic(RelicId.Lens);
            lensKeeper = lens != null && lens.Found ? Alive(lens.holderId) : null;
        }

        bool RelicFoundHere(string id) { int i = Array.IndexOf(RelicId.All, id); return i >= 0 && relicHere[i] && sim.HasRelic(id); }

        // ======================================================================== the day's visits (§3 "Gathering")

        /// <summary>Who goes to the glass on this view day, by stage and half. A hash of (seed, day), so every person's day
        /// stays a pure function of it. At most <see cref="MaxAtGlass"/> at any time.</summary>
        void PlanGlassVisits(DayInfo d)
        {
            d.glass.Clear();
            long day = d.day;
            var glass = GlassPlace;
            void Visit(Person p, ActivityKind k, float s, float e, Prop prop = Prop.None, Person with = null, bool faces = true, Place? at = null)
            {
                var seg = Mk(k, s, e, at ?? glass, prop, ActivitySource.Schedule, Ref(with));
                seg.faces = faces;
                if (!d.glass.TryGetValue(p.key, out var list)) d.glass[p.key] = list = new List<Seg>(2);
                list.Add(seg);
            }
            // The kids who may come today, in a hashed order (apprentices work).
            var kids = new List<Person>();
            foreach (var p in people) if (p.stage == LifeStage.Kid && p.mentor == null) kids.Add(p);
            kids.Sort((a, b) => H(a.key, day, 960).CompareTo(H(b.key, day, 960)));
            int Count(int max, int salt) => 1 + (int)(H(5, day, salt) % (uint)max);

            if (belief == 1)
            {
                // Early: they only come after a wipe (GatherAtGlass). Later: "Maybe it'll drop another one."
                if (!beliefLater || R(5, day, 950) >= 0.5f) return;
                int n = Count(3, 970);
                for (int i = 0; i < kids.Count && i < n; i++) Visit(kids[i], ActivityKind.GazeWary, 15f, 15.8f);
                foreach (var p in people)
                    if (p.stage == LifeStage.Elder && R(p.key, day, 971) < 0.3f) { Visit(p, ActivityKind.GazeWary, 15f, 15.8f); break; }
                return;
            }
            // Stage 2 and 3: the kids most afternoons (stage 3 less: "kids still play").
            if (R(5, day, 951) < (belief == 2 ? 0.7f : 0.4f))
            {
                int n = Count(belief == 2 ? 3 : 2, 972);
                bool doll = belief == 2 && beliefLater && RelicFoundHere(RelicId.KeeperDoll);
                for (int i = 0; i < kids.Count && i < n; i++)
                {
                    if (doll)
                    {
                        // "Kids wave at the doll, then at the glass."
                        Visit(kids[i], ActivityKind.Wave, 14f, 14.35f, Prop.None, null, false, RelicPlace(RelicId.KeeperDoll));
                        Visit(kids[i], ActivityKind.PressHands, 14.35f, 15.1f);
                    }
                    else Visit(kids[i], ActivityKind.PressHands, 14f, 15f);
                }
            }
            if (belief == 2)
            {
                // At dusk a few adults sit facing it.
                if (R(5, day, 952) >= 0.5f) return;
                int n = 1 + Count(2, 973), taken = 0;
                foreach (var p in people)
                {
                    if (taken >= n) break;
                    // Not one whose friend meets them on the green tonight (rule 7: friends meet every evening both are free).
                    if (!p.Adult || p == leader || EveningBusy(p) || (p.friend != null && p.friend.Grown) || R(p.key, day, 974) >= 0.25f) continue;
                    Visit(p, ActivityKind.SitOut, 18.4f, 19.05f);
                    taken++;
                }
                return;
            }
            // Stage 3: the scholars, on work days.
            if (d.rest || scholars.Count == 0) return;
            if (!beliefLater)
            {
                // "The wall is not sky. It is a thing." The clever argue about it at the glass.
                if (R(5, day, 953) >= 0.4f) return;
                var a = scholars[(int)(H(5, day, 975) % (uint)scholars.Count)];
                Person b = null;
                foreach (var s in scholars) if (s != a && s.Grown) { b = s; break; }
                Visit(a, b != null ? ActivityKind.Argue : ActivityKind.Study, 9f, 10f, Prop.Notebook, b);
                if (b != null) Visit(b, ActivityKind.Argue, 9f, 10f, Prop.Notebook, a);
                return;
            }
            // The Lens is up: a scholar measures along the glass most mornings; the Lens's keeper looks through it some evenings.
            if (R(5, day, 954) < 0.7f)
            {
                var m = scholars[(int)(H(5, day, 976) % (uint)scholars.Count)];
                if (m.Grown) Visit(m, ActivityKind.Measure, 8f, 10f, Prop.MeasuringRod);
            }
            if (lensKeeper != null && lensKeeper.Grown && !EveningBusy(lensKeeper) && R(5, day, 955) < 0.4f && !d.glass.ContainsKey(lensKeeper.key))
                Visit(lensKeeper, ActivityKind.LookThroughLens, 18.4f, 19.2f, Prop.Lens, null, false, RelicPlace(RelicId.Lens));
        }

        /// <summary>Lays this person's glass visits over their day (a plain stay: Stitch walks them there at the walking pace,
        /// or skips it when it can't).</summary>
        void GlassLayer(Person p, DayInfo d, List<Seg> list)
        {
            if (!d.glass.TryGetValue(p.key, out var visits)) return;
            foreach (var s in visits) Put(list, s, d);
        }

        // ======================================================================== live staging

        /// <summary>A live scene staged in full when it starts: each cast member's segments, absolute view hours.</summary>
        sealed class Staging
        {
            public string relicId;
            public RelicMomentDef def;
            public Place spot, glass;
            public float start, end;
            public readonly Dictionary<int, List<Seg>> segs = new Dictionary<int, List<Seg>>();
            public readonly Dictionary<int, RelicCast> roles = new Dictionary<int, RelicCast>();
            public readonly List<int> order = new List<int>();

            public float T(float f) => start + f * (end - start);

            public RelicMomentInfo Info(long day)
            {
                var info = new RelicMomentInfo { relicId = relicId, day = day, start = start, end = end, spot = spot };
                foreach (int k in order)
                    info.cast.Add((k > 0 ? PersonRef.Notable(k) : PersonRef.Crowd(-k - 1), roles.TryGetValue(k, out var r) ? r : RelicCast.Grown));
                return info;
            }
        }

        void AddLive(SceneKind kind, ViewTime at, float end, Staging st, int a = -1)
        {
            live.Add(new Scene { kind = kind, day = at.day, start = at.hour, end = end, a = a, b = -1, chronicleId = -1, place = st.spot.IsNone ? st.glass : st.spot, aPartner = -1, aFriend = -1, aParent = -1, staging = st });
            LiveChanged();
        }

        /// <summary>A staged scene's part for <paramref name="p"/>.</summary>
        void StagedLayer(Scene sc, Person p, DayInfo d, List<Seg> list)
        {
            if (sc.staging == null || !sc.staging.segs.TryGetValue(p.key, out var segs)) return;
            foreach (var s in segs) Put(list, s, d);
        }

        /// <summary>The view hour <paramref name="seconds"/> real seconds after <paramref name="at"/> (the end of the day at most).</summary>
        static float EndAfter(ViewTime at, float seconds) => HourOf(at, LifeClock.Advance(at, seconds));

        static float HourOf(ViewTime day, ViewTime t) => t.day > day.day ? 24f : t.hour;

        /// <summary>Real seconds to walk a route of this length at the walking pace. Uncapped: the day's MaxWalk makes a
        /// cross-box walk brisk, but a live walk (into a moment, back into the day) would then cross the box at 3-4x.</summary>
        static float WalkSecs(float len) => len <= 1e-4f ? 0f : len / RealWalkSpeed;

        /// <summary>View hours from <paramref name="hour"/> to walk a route of this length at the walking pace (#92: the pace is
        /// in real seconds, so a live walk at dawn or after nightfall, when view hours run faster, takes more of them).</summary>
        static float LiveWalkHours(long day, float hour, float len)
        {
            float secs = WalkSecs(len);
            if (secs <= 0f) return 0f;
            var t = LifeClock.Advance(new ViewTime(day, hour), secs);
            return (t.day - day) * 24f + t.hour - hour;
        }

        /// <summary>The route's length between two places (the view's line: spot, route, spot), straight without a route, a
        /// quarter hour's walk without positions.</summary>
        float WalkLen(Place a, Place b)
        {
            if (a == b) return 0f;
            var r = RouteBetween(a, b);
            if (r != null) return r.len;
            if (positions.TryGetPosition(a, out float ax, out float az) && positions.TryGetPosition(b, out float bx, out float bz))
                return (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
            return a.IsNone || b.IsNone ? 0f : WalkSpeed * 0.25f;
        }

        /// <summary>Where the day has them at this hour, as a place (a walk's start for its first half, its end after).</summary>
        Place StopPlace(Person p, float hour)
        {
            var segs = DayOf(p);
            var s = segs[Find(segs, hour)];
            var at = s.Moving && (hour - s.start) < (s.end - s.start) * 0.5f ? s.from : s.place;
            return IsValidPlace(at) ? at : ValidHome(p);
        }

        Place Resolve(Staging st, RelicWhere w) => w switch
        {
            RelicWhere.Spot => st.spot,
            RelicWhere.Fire => fire,
            RelicWhere.Gather => gather,
            RelicWhere.Glass => st.glass,
            RelicWhere.Remembering => remembering,
            _ => Place.None,
        };

        /// <summary>
        /// One cast member's part of a relic moment: the group's beats as segments. A walk to a new place must fit in half its
        /// beat at the walking pace (the block walks it in at most half, AddPiece); if the first such walk can't, they don't
        /// join (null); a later one that can't is skipped (they do that beat where they are). <paramref name="here"/> plays
        /// every beat where they stand (a principal nobody can stand in for).
        /// </summary>
        List<Seg> Part(Staging st, Person p, IEnumerable<RelicBeat> beats, ViewTime at, bool here, int turn = 0, int turns = 1)
        {
            var list = new List<Seg>(8);
            var cur = StopPlace(p, at.hour);
            bool moved = false, any = false;
            foreach (var beat in beats)
            {
                any = true;
                float b0 = st.T(beat.from), b1 = st.T(beat.to);
                var target = here ? Place.None : Resolve(st, beat.where);
                if (!target.IsNone && target != (moved ? cur : StopPlace(p, at.hour)))
                {
                    float need = WalkSecs(WalkLen(cur, target));
                    if (need > 0.5f * LifeClock.RealSecondsBetween(b0, b1))
                    {
                        if (!moved) return null;
                        target = Place.None; // skipped: done where they are
                    }
                }
                if (!target.IsNone) { cur = target; moved = true; }
                var place = target.IsNone ? (moved ? cur : Place.None) : target;
                if (beat.cast == RelicCast.Queue && beat.kind == ActivityKind.LookThroughLens && turns > 0)
                {
                    // One turn each: wait, look (lean back, laugh: the loop's steps), then watch the next ones.
                    float len = (b1 - b0) / turns, t0 = b0 + turn * len, t1 = t0 + len;
                    if (t0 > b0) list.Add(StagedSeg(ActivityKind.Queue, b0, t0, place, Prop.None, false, false));
                    list.Add(StagedSeg(beat.kind, t0, t1, place, beat.prop, beat.facesGlass, false));
                    if (t1 < b1) list.Add(StagedSeg(ActivityKind.Watch, t1, b1, place, Prop.None, false, false));
                    continue;
                }
                list.Add(StagedSeg(beat.kind, b0, b1, place, beat.prop, beat.facesGlass, beat.carry));
            }
            return any ? list : null;
        }

        Seg StagedSeg(ActivityKind k, float s, float e, Place place, Prop prop, bool faces, bool carry)
        {
            var seg = Mk(k, s, e, place, prop, ActivitySource.Scene);
            seg.faces = faces; seg.carries = carry;
            return seg;
        }

        void Cast(Staging st, RelicCast role, Person p, ViewTime at, HashSet<int> used)
        {
            var beats = new List<RelicBeat>(st.def.For(role));
            if (beats.Count == 0) return;
            if (p == null || used.Contains(p.key) || p.stage == LifeStage.Baby)
            {
                if (!st.def.standIns || role == RelicCast.Holder || role == RelicCast.Heir) return;
                p = null;
            }
            var part = p != null ? Part(st, p, beats, at, false) : null;
            if (part == null && p != null && !st.def.standIns) part = Part(st, p, beats, at, true); // plays it where they stand
            if (part == null && st.def.standIns)
            {
                // The nearest grown-up who can get there stands in.
                var cands = new List<(Person q, float len)>();
                foreach (var q in people)
                    if (q.Grown && !used.Contains(q.key) && q != p) cands.Add((q, WalkLen(StopPlace(q, at.hour), st.spot)));
                cands.Sort((a, b) => a.len != b.len ? a.len.CompareTo(b.len) : a.q.key.CompareTo(b.q.key));
                foreach (var c in cands)
                {
                    part = Part(st, c.q, beats, at, false);
                    if (part != null) { p = c.q; break; }
                }
            }
            if (part == null || p == null) return;
            Assign(st, p, role, part, used);
        }

        void CastCrowd(Staging st, RelicCast role, ViewTime at, HashSet<int> used, int max, Func<Person, bool> who)
        {
            var beats = new List<RelicBeat>(st.def.For(role));
            if (beats.Count == 0 || max <= 0) return;
            var cands = new List<(Person q, float len)>();
            foreach (var q in people)
            {
                if (used.Contains(q.key) || !who(q)) continue;
                if (DayOf(q)[Find(DayOf(q), at.hour)].kind == ActivityKind.Sleep) continue; // nobody is woken for it
                cands.Add((q, WalkLen(StopPlace(q, at.hour), st.spot)));
            }
            cands.Sort((a, b) => a.len != b.len ? a.len.CompareTo(b.len) : a.q.key.CompareTo(b.q.key));
            var chosen = new List<Person>();
            foreach (var c in cands)
            {
                if (chosen.Count >= max) break;
                if (Part(st, c.q, beats, at, false, 0, 1) != null) chosen.Add(c.q);
            }
            for (int i = 0; i < chosen.Count; i++)
                Assign(st, chosen[i], role, Part(st, chosen[i], beats, at, false, i, chosen.Count), used);
        }

        void Assign(Staging st, Person p, RelicCast role, List<Seg> part, HashSet<int> used)
        {
            used.Add(p.key);
            st.segs[p.key] = part;
            st.roles[p.key] = role;
            st.order.Add(p.key);
        }

        Person Nearest(Place to, ViewTime at, HashSet<int> used, Func<Person, bool> who)
        {
            Person best = null;
            float bl = float.MaxValue;
            foreach (var q in people)
            {
                if (used.Contains(q.key) || !who(q)) continue;
                float l = WalkLen(StopPlace(q, at.hour), to);
                if (l < bl) { bl = l; best = q; }
            }
            return best;
        }

        // ======================================================================== echo of a relic card ("Show me")

        /// <summary>A relic card's echo: its owner at the relic doing its gesture (tasting, dancing in the mask, by the doll, the
        /// lopsided dance, at the lens), where they stand if they can't get there at the walking pace in time.</summary>
        bool EchoRelic(ChronicleEntry e, ViewTime at, float seconds)
        {
            var r = sim.FindRelic(e.relicId);
            if (r == null || !r.Found) return false;
            var p = Alive(e.relicId == RelicId.LeftSandal ? r.holderId : r.ownerId) ?? Alive(e.notableId);
            if (p == null) return false;
            var (kind, prop, faces) = e.relicId switch
            {
                RelicId.SkyCrumb => (ActivityKind.Taste, Prop.Crumb, false),
                RelicId.FestivalMask => (ActivityKind.Dance, Prop.Mask, true),
                RelicId.KeeperDoll => (ActivityKind.Point, Prop.None, false),
                RelicId.LeftSandal => (ActivityKind.LopsidedDance, Prop.Sandal, false),
                RelicId.Lens => (ActivityKind.LookThroughLens, Prop.Lens, false),
                _ => (ActivityKind.Point, Prop.None, false),
            };
            float end = EndAfter(at, Math.Max(3f, Math.Min(5f, seconds)));
            var st = new Staging { relicId = e.relicId, glass = GlassPlace, spot = RelicPlace(e.relicId), start = at.hour, end = end };
            var spot = st.spot;
            bool reach = WalkSecs(WalkLen(StopPlace(p, at.hour), spot)) <= 0.5f * LifeClock.RealSecondsBetween(at.hour, end);
            st.segs[p.key] = new List<Seg> { StagedSeg(kind, at.hour, end, reach ? spot : Place.None, prop, faces, false) };
            st.roles[p.key] = RelicCast.Owner;
            st.order.Add(p.key);
            live.Add(new Scene { kind = SceneKind.Relic, day = at.day, start = at.hour, end = end, a = p.key, b = -1, chronicleId = e.id, echo = true, place = spot, aPartner = -1, aFriend = -1, aParent = -1, staging = st });
            LiveChanged();
            return true;
        }

        // ======================================================================== how a reaction looks by stage (§3)

        /// <summary>The reaction to a Keeper gesture in this belief stage: stage 1 wary (look up, flinch, a step back, peek;
        /// kids creep in under a finger), stage 2 play (kids laugh and point, race to the finger), stage 3 study (a scholar
        /// measures the finger). <paramref name="f"/> is how far into the reaction (0..1).</summary>
        ActivityKind StagedReaction(KeeperGesture g, Person p, float f, ActivityKind plain, out Prop prop)
        {
            prop = Prop.None;
            bool kid = p.stage == LifeStage.Kid;
            switch (g)
            {
                case KeeperGesture.TapGlass:
                    if (belief == 1) return f < 0.3f ? ActivityKind.LookUpWary : f < 0.55f ? ActivityKind.Flinch : ActivityKind.Peek;
                    if (belief == 2 && kid && f >= 0.3f) return ActivityKind.LaughPoint;
                    return ActivityKind.LookUp;
                case KeeperGesture.Wipe:
                    return belief == 1 ? ActivityKind.LookUpWary : ActivityKind.LookUp;
                case KeeperGesture.Return:
                    return belief == 1 ? ActivityKind.WaveWary : plain;
                case KeeperGesture.HoldFinger:
                    if (belief == 1)
                    {
                        if (!kid) return ActivityKind.LookUpWary;
                        return f < 0.4f ? ActivityKind.CreepUp : f < 0.55f ? ActivityKind.HopBack : ActivityKind.JumpAndPoint;
                    }
                    if (belief == 3 && !kid && scholars.Contains(p))
                    {
                        if (beliefLater) { prop = Prop.MeasuringRod; return ActivityKind.Measure; }
                        prop = Prop.Notebook; return ActivityKind.Study;
                    }
                    return plain;
            }
            return plain;
        }
    }

    /// <summary>A relic moment being played (the view's camera hint and bubbles, the debug readout).</summary>
    public sealed class RelicMomentInfo
    {
        public string relicId;
        public long day;
        public float start, end;
        public Place spot;
        public readonly List<(PersonRef who, RelicCast role)> cast = new List<(PersonRef, RelicCast)>();
    }

    /// <summary>A line said in a relic moment: who, the text, and the beat it's said in (view hours).</summary>
    public struct RelicLine
    {
        public string relicId;
        public int lineNo;
        public string text;
        public PersonRef speaker;
        public float start, end;
    }
}
