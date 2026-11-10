using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>One person's day as layered segments (life-in-the-box §3, §4, rules 3–9, 14, 15).</summary>
    public sealed partial class LifePlanner
    {
        struct Seg
        {
            public float start, end;
            public ActivityKind kind;
            public Place place, from;
            public Prop prop;
            /// <summary>P8 (#267): what a bucket holds (Clean or Muddy); None when the prop is no bucket.</summary>
            public WaterKind water;
            public PersonRef partner;
            public ActivitySource src;
            public TripType trip;
            public int chronicleId;
            public bool copies;
            /// <summary>#64: a move whose end a later layer cut off (the walk is redirected to the interruption's place) /
            /// whose start it cut off (the rest of the walk re-routes from wherever the interruption left them).</summary>
            public bool cut, resumed;
            /// <summary>#109: a stay doing a move's loop where it is (filling the buckets at the well), not a move from wherever
            /// they are to <see cref="place"/> (a layer's carry to the green).</summary>
            public bool still;
            /// <summary>#92: facing the front glass; walking there carrying <see cref="prop"/> (a staged beat); a count (knocks).</summary>
            public bool faces, carries;
            public bool hasPoint; public float mx, mz; // #197: an intercept
            public int count;
            public bool Moving => ActivityInfo.IsMove(kind) && from != place && !from.IsNone;
        }

        /// <summary>What the planner knows about the view day it is planning.</summary>
        sealed class DayInfo
        {
            public long day = long.MinValue;
            public Season season;
            public Weather weather;
            public bool rest, ducks, feast, announce;
            public int rainNudgeId = -1;
            public float pace = 1f;
            public readonly List<Scene> scenes = new List<Scene>();
            public Person teller;
            /// <summary>#92: who visits the glass today (<see cref="PlanGlassVisits"/>).</summary>
            public readonly Dictionary<int, List<Seg>> glass = new Dictionary<int, List<Seg>>();
            /// <summary>#200: the few who may sit out at their door today, and the day that list is for.</summary>
            public readonly HashSet<int> doorSitters = new HashSet<int>();
            public long doorSittersDay = long.MinValue;
            /// <summary>P8 #288: who walks over to look at the first bush today (<see cref="IsFirstBushLooker"/>).</summary>
            public long lookerDay = long.MinValue;
            public int lookerKey = int.MinValue;
        }

        /// <summary>#200: the most people who sit out at their doors in a day. A whole Village sat in front of its houses each morning, facing them.</summary>
        public const int DoorSitters = 3;

        /// <summary>Today's door sitters: the elders first, then by the day's hash, so the same few sit all day and it's someone else's turn tomorrow.</summary>
        bool MaySitAtDoor(Person p, DayInfo d)
        {
            if (d.doorSittersDay != d.day)
            {
                d.doorSittersDay = d.day;
                d.doorSitters.Clear();
                var order = new List<Person>(people);
                order.Sort((a, b) =>
                {
                    int old = (b.stage == LifeStage.Elder).CompareTo(a.stage == LifeStage.Elder);
                    return old != 0 ? old : H(a.key, d.day, 777).CompareTo(H(b.key, d.day, 777));
                });
                for (int i = 0; i < order.Count && d.doorSitters.Count < DoorSitters; i++)
                    if (order[i].Grown) d.doorSitters.Add(order[i].key);
            }
            return d.doorSitters.Contains(p.key);
        }

        /// <summary>#200: everyone else who would sit at their door today stays in.</summary>
        void KeepDoorsQuiet(Person p, Seg[] day)
        {
            if (MaySitAtDoor(p, dayInfo)) return;
            for (int i = 0; i < day.Length; i++)
                if (day[i].kind == ActivityKind.SitOut && day[i].place == p.home && !day[i].Moving) { day[i].kind = ActivityKind.Indoors; day[i].prop = Prop.None; }
        }

        readonly DayInfo dayInfo = new DayInfo();
        readonly Dictionary<int, Seg[]> dayCache = new Dictionary<int, Seg[]>();
        Person dayFireKeeper;
        int buildDepth;

        void ClearDays() { runInReady = false; runInCache.Clear(); dayCache.Clear(); blockCache.Clear(); carryPlans.Clear(); dayInfo.day = long.MinValue; diggersDay = long.MinValue; /* tester run 1 #3 */ }

        void EnsureDay(long day)
        {
            if (dayInfo.day == day) return;
            dayCache.Clear();
            blockCache.Clear();
            carryPlans.Clear();
            var d = dayInfo;
            var s = sim.State;
            d.day = day;
            d.season = SeasonOn(day);
            d.scenes.Clear();
            d.rainNudgeId = -1;
            d.ducks = false;
            // Weather: the month's story flags and the Keeper's rain (rule 14).
            var w = Weather.Clear;
            for (int i = s.chronicle.Count - 1; i >= 0 && s.chronicle[i].month >= day; i--)
            {
                var e = s.chronicle[i];
                if (e.month != day) continue;
                switch (e.kind)
                {
                    case "storm": w = Weather.Storm; break;
                    case "snow": if (w != Weather.Storm) w = Weather.Snow; break;
                    case "rainbow": if (w == Weather.Clear || w == Weather.Fog) w = Weather.Rainbow; break;
                    case "fog": if (w == Weather.Clear) w = Weather.Fog; break;
                    case "ducks": d.ducks = true; break;
                }
                AddChronicleScene(d, e);
            }
            // After the first snow, winters snow now and then [est].
            if (w == Weather.Clear && d.season == Season.Winter && s.storyFlags.Contains("snow") && R(0, day, 901) < 0.35f) w = Weather.Snow;
            foreach (var n in s.nudges)
                if (n.month == day && n.kind == SimCore.NudgeKind.Rain) d.rainNudgeId = n.id;
            if (w == Weather.Clear && d.rainNudgeId >= 0) w = Weather.KeeperRain;
            d.weather = forcedWeather ?? w;
            if (forcedWeather.HasValue && forcedWeather.Value == Weather.KeeperRain && d.rainNudgeId < 0 && nudgeIds.Count > 0)
                foreach (var id in nudgeIds) { d.rainNudgeId = id; break; }
            d.scenes.Reverse(); // chronicle order
            // The era festival's year (era-moment.md §6, #91): every evening is a festival evening (tables, the ring
            // dance) unless the day already has its own festival.
            if (sim.FestivalOn && !d.scenes.Exists(x => x.kind == SceneKind.Festival))
                d.scenes.Add(new Scene { kind = SceneKind.Festival, day = day, start = FestivalEveningFrom, end = FestivalEveningTo, place = gather,
                    a = -1, b = -1, chronicleId = -1, aPartner = -1, aFriend = -1, aParent = -1 });
            bool festival = false;
            foreach (var sc in d.scenes) festival |= sc.kind == SceneKind.Festival;
            d.rest = LifeClock.IsRestDay(day) && !festival;
            d.feast = !festival && box.feastChance > 0f && R(1, day, 911) < box.feastChance;
            d.announce = box.announceChance > 0f && R(1, day, 912) < box.announceChance;
            d.pace = (1f - sim.OutputPenalty) * (d.weather == Weather.Fog ? 0.8f : 1f);

            // The storyteller: in winter the eldest tells the founding story (eldestStoryAge); otherwise an elder by turns.
            d.teller = null;
            Person eldest = null;
            int elders = 0;
            foreach (var p in people)
            {
                if (p.stage != LifeStage.Elder) continue;
                elders++;
                if (p.n != null && (eldest == null || p.age > eldest.age)) eldest = p;
            }
            if (d.season == Season.Winter && eldest != null && eldest.age >= sim.Config.eldestStoryAge) d.teller = eldest;
            else if (elders > 0)
            {
                int k = (int)(H(2, day, 913) % (uint)elders);
                foreach (var p in people)
                    if (p.stage == LifeStage.Elder && k-- == 0) { d.teller = p; break; }
            }
            // The camp's night owl: someone keeps the fire.
            dayFireKeeper = null;
            if (era == Era.Camp)
            {
                int adults = 0;
                foreach (var p in people) if (p.Adult) adults++;
                if (adults > 0)
                {
                    int k = (int)(H(3, day, 914) % (uint)adults);
                    foreach (var p in people)
                        if (p.Adult && k-- == 0) { dayFireKeeper = p; break; }
                }
            }
            PlanGlassVisits(d); // #92
        }

        Seg[] DayOf(Person p)
        {
            if (dayCache.TryGetValue(p.key, out var segs)) return segs;
            if (buildDepth > 3) return new[] { new Seg { start = 0f, end = 24f, kind = ActivityKind.SitOut, place = p.home, from = p.home, chronicleId = -1 } };
            buildDepth++;
            try { segs = BuildDay(p, dayInfo); }
            finally { buildDepth--; }
            KeepDoorsQuiet(p, segs);
            dayCache[p.key] = segs;
            return segs;
        }

        // ======================================================================== the builder

        List<Seg> b = new List<Seg>(48);
        float bt;
        Place bcur;

        /// <summary>The shortest stay worth walking to, in view hours (#109).</summary>
        const float MinStay = 0.05f;

        /// <summary>The share of its route's time a re-routed walk or a carry must get, else it is dropped (#109): at dawn's
        /// fast clock a walk given its full time already shows at 1.6x the pace.</summary>
        const float WalkSlack = 0.85f;

        /// <summary>How much of the stay after it a walk short of time may eat into, arriving late (#109; it was half, and a
        /// long walk to a quarrel on the green still rushed at 2x).</summary>
        const float LateShare = 0.75f;

        void Begin(Place at) { b = new List<Seg>(48); bt = 0f; bcur = at; }

        void Push(ActivityKind k, float start, float end, Place place, Place from, Prop prop, PersonRef partner, TripType trip, ActivitySource src = ActivitySource.Schedule, int chronicle = -1, bool copies = false, bool still = false)
        {
            if (end <= start + 1e-4f) return;
            b.Add(new Seg { start = start, end = end, kind = k, place = place, from = from, prop = prop, water = prop == Prop.Buckets || prop == Prop.EmptyBucket ? WaterNow : WaterKind.None, partner = partner, trip = trip, src = src, chronicleId = chronicle, copies = copies, still = still });
        }

        /// <summary>Stay at a place doing <paramref name="k"/> until <paramref name="until"/>; walks there first if needed
        /// (every walk has a destination, rule 3).</summary>
        void At(ActivityKind k, float until, Place place, Prop prop = Prop.None, PersonRef partner = default, TripType trip = TripType.Errand)
        {
            if (until <= bt + 1e-4f) return;
            if (place != bcur)
            {
                // The walk takes its route's time. A stay too short to walk there and still be there a moment is skipped:
                // they carry on from where they are (a walk squeezed into half the stay rushed at 3-5x, #109).
                float w = WalkHours(bcur, place);
                if (w > until - bt - MinStay) return;
                Push(ActivityKind.Walk, bt, bt + w, place, bcur, Prop.None, PersonRef.None, trip);
                bt += w;
            }
            Push(k, bt, until, place, place, prop, partner.IsNone ? PersonRef.None : partner, TripType.None, still: true);
            bt = until;
            bcur = place;
        }

        /// <summary>A move with or without a load.</summary>
        void Move(ActivityKind k, Place to, float until, Prop prop, TripType trip)
        {
            if (until <= bt + 1e-4f) return;
            Push(k, bt, until, to, bcur, prop, PersonRef.None, trip);
            bt = until;
            bcur = to;
        }

        static PersonRef Ref(Person p) => p == null ? PersonRef.None : p.who;

        // ======================================================================== the day

        Seg[] BuildDay(Person p, DayInfo d)
        {
            Begin(p.home);
            switch (p.stage)
            {
                case LifeStage.Baby: BabyDay(p, d); break;
                case LifeStage.Kid: KidDay(p, d); break;
                case LifeStage.Elder: ElderDay(p, d); break;
                default: AdultDay(p, d); break;
            }
            if (bt < 24f) At(ActivityKind.Sleep, 24f, p.home);
            var list = b;

            // Layers, lowest first: relationships and owls colour the schedule; role beats beat it; weather; scenes beat all.
            if (p.Grown) Relationships(p, d, list);
            if (p.Adult) NightOwls(p, d, list);
            GlassLayer(p, d, list); // #92: the people at the glass, by belief stage
            if (p.n != null && p.role != null && p.Grown) RoleBeats(p, d, list);
            if (p.trails != null) Trail(p, list);
            if (eraReady && p == leader) Put(list, Mk(ActivityKind.LookAtGlass, 18.3f, 19.1f, gather, Prop.None, ActivitySource.RoleBeat), d);
            if (eraReady && p == leader && era == Era.Village) Put(list, Mk(ActivityKind.PacePlans, 18.3f, 19.1f, gather, Prop.Plans, ActivitySource.RoleBeat), d);
            WeatherLayer(p, d, list);
            foreach (var sc in d.scenes) SceneLayer(sc, p, d, list, false);
            return Stitch(p, list);
        }

        Seg Mk(ActivityKind k, float start, float end, Place place, Prop prop = Prop.None, ActivitySource src = ActivitySource.Schedule, PersonRef partner = default, Place from = default, int chronicle = -1)
            => new Seg { kind = k, start = start, end = end, place = place, from = from.IsNone ? place : from, prop = prop, src = src, partner = partner.IsNone ? PersonRef.None : partner, chronicleId = chronicle };

        /// <summary>Lay <paramref name="s"/> over the day, trimming what it covers.</summary>
        static void Put(List<Seg> list, Seg s, DayInfo d)
        {
            if (s.end <= s.start) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var o = list[i];
                if (o.end <= s.start || o.start >= s.end) continue;
                list.RemoveAt(i);
                bool walk = ActivityInfo.IsMove(o.kind) && o.from != o.place; // #64: an interrupted walk
                if (o.start < s.start) { var a = o; a.end = s.start; a.cut |= walk; list.Insert(i, a); i++; }
                if (o.end > s.end) { var c = o; c.start = s.end; c.resumed |= walk; list.Insert(i, c); }
            }
            int at = 0;
            while (at < list.Count && list[at].start < s.start) at++;
            list.Insert(at, s);
        }

        /// <summary>Joins the layers up: whoever must be somewhere else walks there first (rule 3); tiny slivers merge.</summary>
        Seg[] Stitch(Person p, List<Seg> list)
        {
            var outList = new List<Seg>(list.Count + 8);
            Place cur = p.home;
            float t = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s.start < t) s.start = t;
                if (s.end <= s.start + 1e-4f && i + 1 < list.Count) continue; // a walk before it ran on through all of it (#109)
                bool isMove = ActivityInfo.IsMove(s.kind) && !s.still;
                // #64: a walk a later layer interrupted never ends at its old destination. Its first part heads for the
                // interruption's place instead; its rest re-routes from wherever the interruption left them.
                if (isMove && s.resumed)
                {
                    InterruptedWalks++;
                    if (s.place == cur) continue; // already there: what came before stretches over it
                    s.from = cur;
                    if (!TakeTheWalksTime(ref s, list, i, outList, cur)) continue;
                }
                // A plain walk from a place a later layer took away (the walk home from the tavern after a courting hour
                // on the green replaced the drink): it sets off from where they are, or, already there, is dropped (#110).
                if (s.kind == ActivityKind.Walk && !s.resumed && !s.cut && !s.from.IsNone && s.from != s.place && s.from != cur)
                {
                    if (s.place == cur) continue;
                    s.from = cur;
                    if (!TakeTheWalksTime(ref s, list, i, outList, cur)) continue;
                }
                if (isMove && s.cut && i + 1 < list.Count)
                {
                    InterruptedWalks++;
                    var n = list[i + 1];
                    bool nMove = ActivityInfo.IsMove(n.kind);
                    Place to = !nMove ? n.place : !n.from.IsNone && n.from != n.place && !n.resumed ? n.from : Place.None;
                    if (to == fire && ActivityInfo.GroupOf(s.kind) == ActivityGroup.Carry) to = stash; // #150: never set down in the fire
                    if (to.IsNone || to == cur) continue; // the next thing starts from here: wait here instead
                    float w = WalkHours(cur, to);
                    if (w <= s.end - s.start) s.start = s.end - w; // set off just in time; what came before stretches
                    else
                    {
                        // Short of time: set off earlier (#109), then arrive late, eating into what comes next; when even that
                        // is over before they could get there (a quarrel on the green at 17:15 for one coming from the mill),
                        // they walk on through it to what follows.
                        s.start = SetOffEarlier(outList, cur, s.start, w - (s.end - s.start), true);
                        float stayEnd = StayEnd(list, i + 1);
                        s.end = Math.Max(s.end, s.start + w > stayEnd - MinStay && i + 2 < list.Count ? s.start + w : Math.Min(s.start + w, n.start + (stayEnd - n.start) * LateShare));
                    }
                    s.place = to;
                    s.from = cur;
                }
                if (s.end - s.start < 0.02f && outList.Count > 0) { var last = outList[outList.Count - 1]; last.end = s.end; outList[outList.Count - 1] = last; t = s.end; continue; }
                bool move = ActivityInfo.IsMove(s.kind) && !s.still;
                if (move && !s.from.IsNone && s.from != cur && s.from != s.place)
                {
                    // A carry starts where its load is: walk there first (a role beat may have moved the person away). The walk
                    // sets off early enough to take its route's time, eating into the stay before it (at most half of it);
                    // only what that can't give comes out of the carry, at most half of it (a walk to the load squeezed into
                    // half the carry rushed at 2-3x, #109).
                    float need = WalkHours(cur, s.from);
                    float start = SetOffEarlier(outList, cur, s.start, need, false);
                    // No time left for the walk there and the carry (the water after the bell rung at dawn): a plain carry is
                    // dropped, they carry on from where they are.
                    if (s.src == ActivitySource.Schedule && i + 1 < list.Count &&
                        s.end - (start + need) < Math.Min(WalkHours(s.from, s.place), s.end - s.start) * WalkSlack) continue;
                    start = SetOffEarlier(outList, cur, s.start, need, true);
                    float arrive = Math.Min(start + need, s.start + (s.end - s.start) * 0.5f);
                    outList.Add(new Seg { start = start, end = arrive, kind = ActivityKind.Walk, place = s.from, from = cur, src = s.src, trip = TripType.Errand, chronicleId = -1, partner = PersonRef.None });
                    s.start = Math.Max(s.start, arrive);
                }
                else if (move) s.from = cur;
                else if (s.place != cur)
                {
                    // The walk there takes its route's time, setting off in the time of any stay skipped before it. A plain
                    // stay a layer left too short to walk to and still be there a moment (the last minutes of chopping in the
                    // woods after a beat at the fields) is skipped: the next thing sets off from here (#109). A beat or a
                    // scene keeps its place: the walk to it takes half of it at most, and PaceNightWalks has it set off earlier.
                    float need = WalkHours(cur, s.place);
                    float start = SetOffEarlier(outList, cur, s.start, need, false, false);
                    if (s.src == ActivitySource.Schedule && need > s.end - start - MinStay && i + 1 < list.Count) continue;
                    start = SetOffEarlier(outList, cur, s.start, need, true, false);
                    float arrive = Math.Min(start + need, s.src == ActivitySource.Schedule ? s.end - MinStay : s.start + (s.end - s.start) * 0.5f);
                    outList.Add(new Seg { start = start, end = arrive, kind = ActivityKind.Walk, place = s.place, from = cur, src = s.src, trip = TripType.Errand, chronicleId = -1, partner = PersonRef.None });
                    s.start = Math.Max(s.start, arrive);
                }
                if (s.start > t && outList.Count > 0 && outList[outList.Count - 1].end < s.start) { var last = outList[outList.Count - 1]; last.end = s.start; outList[outList.Count - 1] = last; }
                if (!move) s.from = s.place;
                outList.Add(s);
                cur = s.place;
                t = s.end;
            }
            if (outList.Count == 0 || outList[0].start > 0f)
                outList.Insert(0, new Seg { start = 0f, end = outList.Count > 0 ? outList[0].start : 24f, kind = ActivityKind.Sleep, place = p.home, from = p.home, chronicleId = -1, partner = PersonRef.None });
            var lastSeg = outList[outList.Count - 1];
            if (lastSeg.end < 24f) { lastSeg.end = 24f; outList[outList.Count - 1] = lastSeg; }
            // #64: a walk that runs straight on into a walk to a beat or a scene from its end (the beat starts just as they
            // arrive) is one walk, from the first start to the last end, timed by its own route.
            for (int i = outList.Count - 1; i > 0; i--)
            {
                var a = outList[i - 1];
                var c = outList[i];
                if (a.kind != ActivityKind.Walk || c.kind != ActivityKind.Walk || !a.Moving || !c.Moving || c.from != a.place) continue;
                if (c.src == ActivitySource.Schedule && a.src == ActivitySource.Schedule) continue; // the plain day's own shape
                if (a.from == c.place)
                {
                    // There and straight back: they stay where they were.
                    if (i < 2 || outList[i - 2].Moving || outList[i - 2].place != a.from) continue;
                    var stay = outList[i - 2];
                    stay.end = c.end;
                    outList[i - 2] = stay;
                    outList.RemoveAt(i);
                    outList.RemoveAt(i - 1);
                    i--;
                    continue;
                }
                var m = c;
                m.from = a.from;
                m.start = a.start;
                m.trip = a.trip != TripType.Errand ? a.trip : c.trip;
                m.src = c.src != ActivitySource.Schedule ? c.src : a.src;
                float w = WalkHours(m.from, m.place);
                if (i >= 2 && w < m.end - m.start)
                {
                    m.start = m.end - w; // set off just in time: what came before stretches
                    var before = outList[i - 2];
                    before.end = m.start;
                    outList[i - 2] = before;
                }
                outList[i - 1] = m;
                outList.RemoveAt(i);
            }
            // Merge neighbours that are the same thing (two work blocks of the same loop read as one).
            for (int i = outList.Count - 1; i > 0; i--)
            {
                var a = outList[i - 1];
                var c = outList[i];
                if (a.kind != c.kind || a.place != c.place || a.from != c.from || a.prop != c.prop || a.partner != c.partner || a.src != c.src ||
                    a.chronicleId != c.chronicleId || a.copies != c.copies || a.faces != c.faces || a.count != c.count || ActivityInfo.IsMove(a.kind)) continue;
                a.end = c.end;
                outList[i - 1] = a;
                outList.RemoveAt(i);
            }
            PaceNightWalks(outList);
            return outList.ToArray();
        }

        /// <summary>A walk Stitch re-routed from where the person is (the rest of a walk a storm or a beat interrupted, a walk
        /// whose origin a layer took away) keeps only the leftover of the old walk's time: it sets off earlier, eating into
        /// the plain stay before it (at most half), and arrives late, eating into the stay after it (LateShare). A resumed walk
        /// of 0.02 h across the town rushed at 6x (#109). A plain walk that still can't get most of its time, or whose plain
        /// stay would be over before they got there, is dropped (Stitch then skips that stay as well and they carry on from
        /// where they are): false.</summary>
        bool TakeTheWalksTime(ref Seg s, List<Seg> list, int i, List<Seg> outList, Place cur)
        {
            float w = WalkHours(s.from, s.place);
            if (s.end - s.start >= w) return true;
            float start = SetOffEarlier(outList, cur, s.start, w - (s.end - s.start), false), end = s.end;
            bool plain = s.src == ActivitySource.Schedule && i + 1 < list.Count;
            if (i + 1 < list.Count && !list[i + 1].Moving)
            {
                var n = list[i + 1];
                float stayEnd = StayEnd(list, i + 1);
                if (plain && n.src == ActivitySource.Schedule && n.place == s.place && start + w > stayEnd - MinStay && i + 2 < list.Count) return false;
                end = Math.Max(end, Math.Min(start + w, n.start + (stayEnd - n.start) * LateShare));
            }
            if (plain && end - start < w * WalkSlack) return false;
            s.start = SetOffEarlier(outList, cur, s.start, w - (s.end - s.start), true);
            s.end = end;
            return true;
        }

        /// <summary>How much earlier than <paramref name="start"/> a walk from <paramref name="cur"/> can set off to gain up to
        /// <paramref name="gain"/> view hours: first the gap before it (the time of a stay Stitch skipped), then, with
        /// <paramref name="eat"/>, at most half of the plain stays at <paramref name="cur"/> just before it (beats and scenes
        /// keep their time). With <paramref name="commit"/> what came before ends where the walk sets off (cut short, or
        /// stretched over the rest of the gap).</summary>
        static float SetOffEarlier(List<Seg> outList, Place cur, float start, float gain, bool commit, bool eat = true)
        {
            if (outList.Count == 0) return start;
            float free = Math.Min(start, outList[outList.Count - 1].end);
            float s0 = Math.Max(start - Math.Max(0f, gain), free);
            int first = outList.Count;
            if (eat && gain > 0f && s0 <= free + 1e-5f)
            {
                while (first > 0 && !outList[first - 1].Moving && outList[first - 1].place == cur && outList[first - 1].src == ActivitySource.Schedule) first--;
                if (first < outList.Count) s0 = Math.Min(free, Math.Max(start - gain, outList[first].start + (free - outList[first].start) * 0.5f));
            }
            if (!commit) return s0;
            while (outList.Count > first + 1 && first < outList.Count && outList[outList.Count - 1].start >= s0) outList.RemoveAt(outList.Count - 1);
            var last = outList[outList.Count - 1];
            last.end = s0;
            outList[outList.Count - 1] = last;
            return s0;
        }

        /// <summary>The end of the stay at <paramref name="j"/> with the pieces after it at the same place (a stay a layer
        /// split, the storm's shelter in one piece per hour of the plain day).</summary>
        static float StayEnd(List<Seg> list, int j)
        {
            float e = list[j].end;
            for (int k = j + 1; k < list.Count && !list[k].Moving && list[k].place == list[j].place; k++) e = list[k].end;
            return e;
        }

        /// <summary>
        /// A walk shown in less than 60 % of the real seconds its route needs at <see cref="RealWalkSpeed"/> (one that falls
        /// in the fast night hours: everyone heading home at 20:00) sets off earlier instead, eating into the stays before it
        /// (at most half of them), or, right after another walk, arrives later, eating into the stays after it. Day walks
        /// are untouched: at work-hour and evening rates they already take their time (#30).
        /// </summary>
        void PaceNightWalks(List<Seg> list)
        {
            const float RushPace = 1.9f; // just under the 2x the rush tests allow
            float speed = RealWalkSpeed;
            for (int i = 1; i < list.Count; i++)
            {
                var s = list[i];
                if (!s.Moving || s.from == s.place) continue;
                var route = RouteBetween(s.from, s.place);
                if (route == null || route.len <= 1e-4f) continue;
                float need = Math.Min(route.len / speed, MaxWalk * LifeClock.RealSecondsPerViewHour(LifeClock.WorkStart));
                // #206: the floor is 60 % of the time, but a long walk's time is capped: one trimmed a little under the cap is over twice the pace while it still passes the 60 %.
                float enough = Math.Max(need * 0.6f, Math.Min(need, route.len / speed / RushPace));
                if (LifeClock.RealSecondsBetween(s.start, s.end) >= enough) continue;
                // The stays before it (the evening on the green, in slivers of chat and listening): set off within their
                // later half.
                int first = i;
                while (first > 0 && !list[first - 1].Moving) first--;
                if (first < i)
                {
                    float lo = list[first].start + (s.start - list[first].start) * 0.5f, hi = s.start;
                    if (lo < hi)
                    {
                        for (int k = 0; k < 24; k++) // the latest start that gives the walk its time
                        {
                            float mid = (lo + hi) * 0.5f;
                            if (LifeClock.RealSecondsBetween(mid, s.end) >= need) lo = mid; else hi = mid;
                        }
                        s.start = lo;
                        list[i] = s;
                        for (int m = i - 1; m >= first; m--)
                        {
                            var prev = list[m];
                            if (prev.start >= lo - 1e-4f) { list.RemoveAt(m); i--; continue; }
                            prev.end = lo;
                            list[m] = prev;
                            break;
                        }
                    }
                }
                if (LifeClock.RealSecondsBetween(s.start, s.end) >= enough) continue;
                // Nothing to set off earlier from (it follows another walk, a night round): arrive later instead, within the
                // first half of the stays after it (usually sleep).
                int last = i;
                while (last + 1 < list.Count && !list[last + 1].Moving) last++;
                if (last == i) continue;
                float lo2 = s.end, hi2 = s.end + (list[last].end - s.end) * 0.5f;
                if (LifeClock.RealSecondsBetween(s.start, hi2) < need * 0.6f) continue;
                for (int k = 0; k < 24; k++) // the earliest end that gives the walk its time
                {
                    float mid = (lo2 + hi2) * 0.5f;
                    if (LifeClock.RealSecondsBetween(s.start, mid) >= need) hi2 = mid; else lo2 = mid;
                }
                s.end = hi2;
                list[i] = s;
                for (int m = i + 1; m <= last; m++)
                {
                    var next = list[m];
                    if (next.end <= hi2 + 1e-4f) { list.RemoveAt(m); m--; last--; continue; }
                    next.start = hi2;
                    list[m] = next;
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- babies and kids

        void BabyDay(Person p, DayInfo d)
        {
            var carer = p.parent ?? (p.n == null ? null : p.partner);
            At(ActivityKind.Sleep, 6.5f, p.home);
            At(ActivityKind.Toddle, 19f, p.home, Prop.None, Ref(carer));
        }

        void KidDay(Person p, DayInfo d)
        {
            int k = p.key;
            long day = d.day;
            At(ActivityKind.Sleep, 6f + R(k, day, 1) * 0.6f, p.home);
            At(ActivityKind.Play, 7f, p.home, Prop.Ball);
            if (p.mentor != null && !d.rest)
            {
                CopyMaster(p, 7f, 12f);
                if (bt < 12f) At(ActivityKind.WatchWork, 12f, bcur, Prop.None, Ref(p.mentor)); // the master walked off: watch from here
            }
            else
            {
                At(PlayKind(p, d, 10), 10f, PlayPlace(p, d, 10, near: true), PlayProp(p, d, 10));
                if (d.teller != null && d.teller != p) At(ActivityKind.ListenStory, 11f, gather, Prop.None, Ref(d.teller));
                var snack = KidBerryPlace(p, d);
                if (!snack.IsNone) At(ActivityKind.Eat, 12f, snack, Prop.None); // P8 #288: a few berries straight from the bush
                else At(PlayKind(p, d, 11), 12f, PlayPlace(p, d, 11, near: false), PlayProp(p, d, 11));
            }
            float lunchEnd = 12.5f + R(k, day, 83) * 0.7f; // #197: kids do not all sit down at once
            // Midday (village on): carry the meal basket out to a parent in the field.
            var parentField = p.parent != null && p.parent.Adult && era != Era.Camp && !d.rest ? FieldOf(p.parent, d) : Place.None;
            if (!parentField.IsNone)
            {
                // The basket takes as long as the walk out there (from the hill it rushed at 2.6x in 0.3 h, #109).
                Move(ActivityKind.Carry, parentField, Math.Max(12.3f, bt + WalkHours(bcur, parentField)), Prop.Basket, TripType.HomeToWork);
                At(ActivityKind.Eat, lunchEnd, parentField, Prop.Basket, Ref(p.parent));
            }
            else At(ActivityKind.Eat, lunchEnd, MealPlace(p.parent ?? p, d), Prop.None, Ref(p.parent));
            float t = Math.Max(13f, lunchEnd);
            int block = 0;
            while (t < 17f)
            {
                float end = Math.Min(17f, t + 1.25f + R(PlayBlockKey, day, 20 + block) * 0.75f); // #358: one length for every kid that day: the kids at the green play together for a block (a play group)
                if (17f - end < 0.5f) end = 17f;
                At(PlayKind(p, d, 20 + block), end, PlayPlace(p, d, 20 + block, near: block % 2 == 0), PlayProp(p, d, 20 + block));
                t = end; block++;
            }
            float home = 19.3f + R(k, day, 2) * 0.3f - box.homeEarlyHours;
            // #404: some evenings the kids dare each other at the lookout tower (one roll for all of them, so they go together)
            if (TowerOpen && (DebugTowerEvenings || R(PlayBlockKey, day, 404) < TowerDareEvening) && R(k, day, 57) < 0.5f)
            {
                At(ActivityKind.Play, home, Place.Land(PlaceKind.Tower), Prop.None, default, TripType.HomeToGreen);
                At(ActivityKind.Sleep, 24f, p.home, Prop.None, default, TripType.Home);
                return;
            }
            if (R(k, day, 56) < 0.45f) { At(ActivityKind.Play, home, p.home, Prop.Ball, default, TripType.Home); At(ActivityKind.Sleep, 24f, p.home, Prop.None, default, TripType.Home); return; } // #197: some play at their own door
            At(ActivityKind.Play, 18.3f, gather, Prop.Ball, default, TripType.HomeToGreen);
            if (d.teller != null && d.teller != p) At(ActivityKind.ListenStory, Math.Min(home, 19.1f), gather, Prop.None, Ref(d.teller));
            At(ActivityKind.Play, home, gather);
            At(ActivityKind.Sleep, 24f, p.home, Prop.None, default, TripType.Home);
        }

        /// <summary>#358: the afternoon play blocks' key: the same for every kid, so their blocks start and end together.</summary>
        const int PlayBlockKey = 358358;

        ActivityKind PlayKind(Person p, DayInfo d, int salt)
        {
            if (d.weather == Weather.Snow) return R(p.key, d.day, salt + 300) < 0.6f ? ActivityKind.Snowball : ActivityKind.Play;
            if (d.season == Season.Summer && R(p.key, d.day, salt + 300) < 0.3f) return ActivityKind.Swim;
            return ActivityKind.Play;
        }

        Place PlayPlace(Person p, DayInfo d, int salt, bool near)
        {
            float r = R(p.key, d.day, salt + 400);
            if (d.ducks && r < 0.4f) return Place.Land(PlaceKind.Pond);       // chasing the ducks
            if (d.weather == Weather.KeeperRain && d.rainNudgeId >= 0 && r < 0.5f) return Place.Land(PlaceKind.Nudge, d.rainNudgeId); // puddles
            if (d.season == Season.Summer && r < 0.3f) return Place.Land(PlaceKind.Pond);
            if (era != Era.Camp && r > 0.92f) return Place.Land(PlaceKind.Hill);    // running up the Keeper's hill
            return near ? p.home : gather;
        }

        Prop PlayProp(Person p, DayInfo d, int salt) => (H(p.key, d.day, salt + 500) % 3) switch { 0 => Prop.Ball, 1 => Prop.Stick, _ => Prop.None };

        /// <summary>The apprentice works next to the master, copying each stroke a beat late (§4).</summary>
        void CopyMaster(Person p, float from, float to)
        {
            var master = DayOf(p.mentor);
            foreach (var s in master)
            {
                if (s.end <= from || s.start >= to) continue;
                float a = Math.Max(from, s.start), e = Math.Min(to, s.end);
                if (a < bt) a = bt;
                if (e <= a) continue;
                if (a > bt) At(ActivityKind.WatchWork, a, s.Moving ? s.from : s.place, Prop.None, Ref(p.mentor));
                var prop = s.prop;
                if (s.Moving)
                {
                    // The apprentice walks from where they are and takes the route's time, a beat late; a walk the master
                    // finishes after the copying ends (or one the apprentice couldn't finish by then) isn't copied: they go off
                    // to their own work from here. Clipped walks rushed at 5-9x (#109).
                    float until = Math.Max(e, bt + WalkHours(bcur, s.place));
                    if (s.end > to + 1e-4f || until > to + 1e-4f) break;
                    Move(s.kind, s.place, until, prop, s.trip);
                    continue;
                }
                Push(s.kind, a, e, s.place, s.place, prop, p.mentor.who, TripType.None, ActivitySource.Schedule, -1, true, true);
                bt = e; bcur = s.place;
            }
        }

        // ---------------------------------------------------------------- elders

        void ElderDay(Person p, DayInfo d)
        {
            int k = p.key;
            long day = d.day;
            At(ActivityKind.Sleep, 5.5f + R(k, day, 1) * 0.8f, p.home);
            At(ActivityKind.SitOut, 7f, p.home, Prop.Stool);
            bool wander = farms.Count > 0 && R(k, day, 3) < 0.4f;
            if (wander) At(ActivityKind.WatchWork, 9f, PlaceOf(farms[(int)(H(k, day, 4) % (uint)farms.Count)]), Prop.Stick);
            else At(ActivityKind.SitOut, 9f, ElderSitPlace(p, d, 0), Prop.Stick); // tester run 1 #3: ElderSitPlace is the gather outside the camp
            bool minds = R(k, day, 5) < 0.4f;
            At(minds ? ActivityKind.MindBabies : ActivityKind.SitOut, 10f, minds ? p.home : ElderSitPlace(p, d, 1));
            if (d.teller == p) At(ActivityKind.StoryTell, 11f, gather);
            else At(ActivityKind.SitOut, 11f, ElderSitPlace(p, d, 2), Prop.Stool);
            if (p.hints.sitWithOldChance > 0f && R(k, day, 6) < p.hints.sitWithOldChance) At(ActivityKind.SitWithOld, 12f, gather);
            else At(ActivityKind.SitOut, 12f, ElderSitPlace(p, d, 3));
            At(ActivityKind.Eat, 12.9f, MealPlace(p, d), Prop.None, Ref(p.partner));
            At(minds ? ActivityKind.SitOut : ActivityKind.MindBabies, 15f, p.home);
            if (farms.Count > 0 && R(k, day, 7) < 0.5f) At(ActivityKind.WatchWork, 17f, PlaceOf(farms[(int)(H(k, day, 8) % (uint)farms.Count)]), Prop.Stick);
            else At(ActivityKind.SitOut, 17f, ElderSitPlace(p, d, 4), Prop.Stool);
            float home = 19.4f + R(k, day, 2) * 0.4f - box.homeEarlyHours - p.hints.homeEarlyHours;
            if (StyleOf(p, d) != EveningStyle.Gather) { At(ActivityKind.SitOut, home, p.home, Prop.Stool, default, TripType.Home); At(ActivityKind.Sleep, 24f, p.home, Prop.None, default, TripType.Home); return; } // #197
            At(ActivityKind.SitOut, 18.3f, gather, Prop.Stool, default, TripType.HomeToGreen);
            At(d.teller == p ? ActivityKind.StoryTell : ActivityKind.SitOut, Math.Min(home, 19.1f), gather);
            At(ActivityKind.SitOut, home, gather);
            At(ActivityKind.Sleep, 24f, p.home, Prop.None, default, TripType.Home);
        }

        // ---------------------------------------------------------------- working adults

        void AdultDay(Person p, DayInfo d)
        {
            int k = p.key;
            long day = d.day;
            var work = WorkOf(p, d, morning: true);
            bool rest = d.rest && !work.keepsRestDay;
            var sh = ShiftOf(p, d);
            At(ActivityKind.Sleep, (rest ? 6f : 5f) + R(k, day, 1) * 0.6f + (rest ? 0f : Math.Max(0f, sh.start - LifeClock.WorkStart - 0.3f)), p.home);
            // #208: three of one home stretched at the door together at 05:54 (the Town's worst knot): the lane's first turn stretches
            // outside, the rest inside.
            At(DawnTurn(p, d, 3).rank == 0 ? ActivityKind.Stretch : ActivityKind.Indoors, bt + 0.2f, p.home);

            if (rest) { if (CampSpread) CampRestDay(p, d); else RestDay(p, d); } // tester run 1 #3: the camp's rest day spread out
            else
            {
                // Dawn: water from the well or the stream, a chore at the door, then off to work.
                if (!work.fetchFirst && (R(k, day, 2) < 0.3f || forcedWater.Contains(k))) // #360: the cheat sends some on top of the dice; #405: the cook draws on the way to work instead
                {
                    var water = WaterPlace; // P8 (#267): the pond once it has water, else the wet low ground
                    // #152: empty on the way out and while they draw; full only once it's drawn.
                    Move(ActivityKind.Walk, water, bt + WalkHours(p.home, water), Prop.EmptyBucket, TripType.HomeToGreen);
                    At(ActivityKind.FetchWater, bt + 0.35f, water, Prop.EmptyBucket); // #360: 3.5 s at dawn: kneel, dip, hold, lift (was 0.2 h = 2 s)
                    Move(ActivityKind.FetchWater, p.home, bt + WalkHours(water, p.home), Prop.Buckets, TripType.Home);
                }
                if (work.sweeps && box.sweeperExtraBlocks > 0)
                {
                    // A tidy holder in office (the grump, #28): the sweepers sweep the green at dawn before their round. After
                    // the water there may be no time left before the round: the walk to the green still takes its route's
                    // time and they sweep a little, so the round starts late (a squeezed walk rushed at 4x, #110).
                    float sweepUntil = Math.Max(sh.start - WalkHours(gather, work.site), bt + WalkHours(bcur, gather) + 0.1f);
                    At(ActivityKind.Sweep, sweepUntil, gather, Prop.Broom, default, TripType.HomeToGreen);
                }
                else
                {
                    // #208: the chore used to run from the stretch to its end, so everyone was out at the door together and
                    // knotted the lane between two homes at ~06:30. Each person now stays in until their own start, spread
                    // over the stretch between waking and the old end, and does a short chore (0.35-0.55 h).
                    // #208 (2026-11-07): still 4 at the camp's tent (3 chopping kindling) and 7 at two facing village homes, as housemates
                    // drew their chore and start on their own dice. Now the home's working adults take turns: each its own slot of the
                    // stretch and its own chore (DawnTurn), the chore shortened when the slots are tight, so a home has about one out at a time.
                    float choreEnd = Math.Min(6.6f + (sh.start - LifeClock.WorkStart), sh.start - 0.4f);
                    float choreUntil = HomeChoreTurn(p, d, choreEnd - 1f, choreEnd, 3); // one shared hour for the home's turns
                    float walk = work.fetchFirst ? FetchFirstHours(p.home, work) : WalkHours(p.home, work.site);
                    // #208 phase B: in the Editor the lane still knotted (worst 8 -> 10 in the Village), as the short chore was
                    // followed by sitting out at the door until work. After the chore they go back in, and step out only just before leaving.
                    float leave = sh.start - walk;
                    if (leave - 0.12f > choreUntil + 0.05f) At(ActivityKind.Indoors, leave - 0.12f, p.home);
                    At(ActivityKind.SitOut, leave, p.home);
                }
                if (work.fetchFirst) FetchFirst(work); // #405: the well, then the stash by the fire, then the pot
                // The walk to work takes as long as the route does: a late start (the water came first) arrives late.
                if (bcur != work.site || !work.fetchFirst) Move(ActivityKind.Walk, work.site, Math.Max(sh.start, bt + WalkHours(bcur, work.site)), Prop.None, TripType.HomeToWork);

                if (p.mentor != null) CopyMaster(p, 7f, 9f); // a young apprentice still learns from the master first
                WorkBlocks(p, d, work, sh.start, sh.lunch, 10, false);
                bool nap = p.hints.napChance > 0f && R(k, day, 4) < p.hints.napChance; // the lazy, after lunch
                // After lunch they walk to the afternoon's work: a role holder's morning duty (the master builder at the
                // mill) may differ from the job they go back to (sweeping the streets, #109).
                var after = WorkOf(p, d, morning: false);
                Midday(p, d, after, nap, sh);
                if (nap) At(ActivityKind.Nap, Math.Max(14f, bt + 0.5f), gather); // straight from lunch to the green (#64)
                float workEnd = sh.end;
                WorkBlocks(p, d, after, bt, workEnd, 30, true);
                if (workEnd < LifeClock.Evening - 0.01f)
                {
                    // A short winter day: home by the hearth until the evening gathering (a chore, then a sit).
                    var chore = Chore(p, d, 7);
                    At(chore, Math.Min(LifeClock.Evening - 0.4f, bt + WalkHours(bcur, p.home) + 0.6f), p.home, ChoreProp(chore), default, TripType.Home);
                    At(ActivityKind.SitOut, LifeClock.Evening, p.home);
                }
            }
            Evening(p, d);
        }

        void RestDay(Person p, DayInfo d)
        {
            int k = p.key;
            long day = d.day;
            if (HomeChoreTurn(p, d, 6.7f, 8.5f, 5) < 8.5f - 0.01f) At(ActivityKind.Indoors, 8.5f, p.home); // #208: housemates take turns
            if (churchB != null) At(ActivityKind.Listen, 10.5f, PlaceOf(churchB), Prop.None, default, TripType.Errand); // the service
            else At(ActivityKind.Chat, 10.5f, gather, Prop.None, Ref(p.friend), TripType.HomeToGreen);
            if (marketB != null && R(k, day, 6) < 0.5f) { At(ActivityKind.Shop, 11.5f, PlaceOf(marketB), Prop.Basket, default, TripType.HomeToMarket); }
            At(ActivityKind.SitOut, 12f, gather);
            At(ActivityKind.Eat, 12.9f, MealPlace(p, d), Prop.None, Ref(p.partner));
            if (p.hints.napChance > 0f && R(k, day, 7) < p.hints.napChance) At(ActivityKind.Nap, 14.5f, gather);
            At(R(k, day, 8) < 0.5f ? ActivityKind.Chat : ActivityKind.SitOut, 15.5f, gather, Prop.None, Ref(p.friend));
            if (R(k, day, 9) < 0.4f && p.hints.hillChance > 0f) At(ActivityKind.LookAtGlass, 16.5f, Place.Land(PlaceKind.Hill));
            At(ActivityKind.Chat, 17f, gather);
        }

        void Midday(Person p, DayInfo d, Work work, bool napAfter, Shift sh)
        {
            var meal = MealPlace(p, d, work.site);
            var with = p.partner != null && p.partner.Adult ? p.partner : null;
            // The walk back to work takes its route's time: from a far inn they finish eating earlier, and arrive late if
            // they got there late (a 0.2 h walk back across the town rushed at 2x, #109).
            float back = !napAfter && meal != work.site ? WalkHours(meal, work.site) : 0f;
            float arrive = bt + (meal != bcur ? WalkHours(bcur, meal) : 0f);
            At(ActivityKind.Eat, Math.Min(sh.back - 0.2f, Math.Max(sh.back - back, arrive + 0.3f)), meal, Prop.None, Ref(with));
            if (napAfter) return; // off to the green for a nap instead of back to work
            if (meal != work.site) { if (bcur != work.site) Move(ActivityKind.Walk, work.site, Math.Max(sh.back, bt + WalkHours(bcur, work.site)), Prop.None, TripType.HomeToWork); }
            else if (CampSpread && !work.chores && work.loop != ActivityKind.Inspect) At(work.loop, sh.back, meal, work.tool); // tester run 1 #3: the camp's work goes on over the meal (the cook at the pot, the picker at the bush)
            else At(ActivityKind.SitOut, sh.back, meal);
        }

        /// <summary>Where a person eats (§3 midday): the fire in the camp; the field edge or the doorstep in the village; the
        /// market and the inn's benches in the town. A couple eats where the first of them does.</summary>
        Place MealPlace(Person p, DayInfo d, Place workSite = default)
        {
            if (p.partner != null && p.partner.key < p.key && p.partner.Grown) p = p.partner;
            if (era == Era.Camp) return workSite == fire ? fire : CampMealPlace(p, d, workSite); // tester run 1 #3: meals in shifts and places
            if (era == Era.Village)
            {
                var f = p.Adult ? FieldOf(p, d) : Place.None;
                return f.IsNone ? VillageLunch(p, d, workSite) : f;
            }
            return tavernB != null && R(p.key, d.day, 40) < 0.5f ? PlaceOf(tavernB) : marketB != null ? PlaceOf(marketB) : p.home;
        }

        /// <summary>#197: a village lunch away from the field. The doorstep alone put nine of fourteen people at their own doors at
        /// midday, which read as one schedule. Now per person (a couple by the first of them) and day: about three in ten eat at
        /// their own door, two in ten by the well, one in ten on the green, one in ten at a neighbour's (a friend's) door, and the
        /// rest, if single, keep to their chore outside at the work site and eat there. Same activities as before, only the places.</summary>
        Place VillageLunch(Person p, DayInfo d, Place workSite)
        {
            float r = R(p.key, d.day, 41);
            var well = wellB != null ? PlaceOf(wellB) : gather;
            if (r < 0.30f) return p.home;
            if (r < 0.50f) return well;
            if (r < 0.60f) return gather;
            if (r < 0.72f)
            {
                var f = p.friend;
                return f != null && f.Grown && !f.home.IsNone && f.home != p.home ? f.home : well;
            }
            bool single = p.partner == null || !p.partner.Grown;
            return single && p.Adult && !workSite.IsNone ? workSite : well;
        }

        Place FieldOf(Person p, DayInfo d)
        {
            if (p.jobBuilding == null || p.jobBuilding.type != BuildingType.Farm) return Place.None;
            if (p.job != JobType.Farmer && p.job != JobType.Gatherer) return Place.None;
            if (d.season == Season.Winter) return Place.None;
            return PlaceOf(p.jobBuilding);
        }

        ActivityKind Chore(Person p, DayInfo d, int salt) => ChoreIndex(p, d, salt) switch
        {
            0 => ActivityKind.SweepStep,
            1 => ActivityKind.HangWashing,
            2 => ActivityKind.ChopKindling,
            _ => ActivityKind.SitOut,
        };

        /// <summary>The chore index (0-3). #521: a small first camp deals them out by rank, so the grown-ups never do the same chore
        /// at once (three chopping kindling at the one tent); everywhere else it stays the person's own hash.</summary>
        uint ChoreIndex(Person p, DayInfo d, int salt)
        {
            uint mine = H(p.key, d.day, salt + 600);
            if (!CampSpread || !p.Adult) return mine % 4;
            int adults = 0, rank = 0;
            foreach (var q in people)
            {
                if (!q.Adult) continue;
                adults++;
                uint h = H(q.key, d.day, salt + 600);
                if (q != p && (h < mine || (h == mine && q.key < p.key))) rank++;
            }
            return adults > 8 ? mine % 4 : (uint)(rank + H(0, d.day, salt + 601) % 4) % 4;
        }

        static readonly ActivityKind[] DawnChores = { ActivityKind.SweepStep, ActivityKind.HangWashing, ActivityKind.ChopKindling, ActivityKind.SitOut };
        readonly List<Person> dawnHouse = new();
        /// <summary>#208: homes whose doors stand this close (box units) share the dawn turns: twice the 0.25 knot the play test counts.</summary>
        const float DawnLane = 0.6f;

        /// <summary>#208: this person's turn at the home's dawn chore: their place among the home's grown-up adults (shuffled per day),
        /// how many share the home, and their chore. Housemates' chores differ (the home's first chore, then the next ones round).</summary>
        (int rank, int count, ActivityKind chore) DawnTurn(Person p, DayInfo d, int salt)
        {
            // The turns are the lane's, not only the home's: the worst knots straddled two facing homes (doors within the knot's reach).
            dawnHouse.Clear();
            bool placed = positions.TryGetPosition(p.home, out float hx, out float hz);
            foreach (var q in people)
            {
                if (q.stage != LifeStage.Adult) continue;
                if (q.home == p.home) { dawnHouse.Add(q); continue; }
                if (placed && positions.TryGetPosition(q.home, out float qx, out float qz) && (qx - hx) * (qx - hx) + (qz - hz) * (qz - hz) < DawnLane * DawnLane) dawnHouse.Add(q);
            }
            if (dawnHouse.Count <= 1) return (0, 1, Chore(p, d, salt));
            long day = d.day;
            dawnHouse.Sort((a, b) => { uint ha = H(a.key, day, 132 + salt), hb = H(b.key, day, 132 + salt); return ha != hb ? ha.CompareTo(hb) : a.key.CompareTo(b.key); });
            int rank = Math.Max(0, dawnHouse.IndexOf(p));
            int first = (int)(H(dawnHouse[0].key, day, 600 + salt) % (uint)DawnChores.Length);
            return (rank, dawnHouse.Count, DawnChores[(first + rank) % DawnChores.Length]);
        }

        /// <summary>#208: the home's morning chore by turns, between <paramref name="from"/> and <paramref name="end"/>: in until this person's
        /// slot, their own chore (shortened when the slots are tight), and back in. Returns when their chore ends (they are in after it).</summary>
        float HomeChoreTurn(Person p, DayInfo d, float from, float end, int salt)
        {
            int k = p.key;
            long day = d.day;
            var (rank, count, kind) = DawnTurn(p, d, salt);
            float slot = (end - from) / count;
            float len = Math.Min(0.35f + 0.2f * R(k, day, 130 + salt), Math.Max(0.2f, slot - 0.05f));
            float until = end;
            if (end - Math.Max(from, bt) - len > 0.05f)
            {
                // The window is the home's, shared by all (slots cut from each one's own waking did not line up); nobody starts before they're up.
                float start = Math.Max(bt + 0.05f, from + rank * slot + Math.Max(0f, slot - len) * R(k, day, 131 + salt));
                At(ActivityKind.Indoors, start, p.home);
                until = Math.Min(end, start + len);
            }
            At(kind, until, p.home, ChoreProp(kind));
            return until;
        }

        static Prop ChoreProp(ActivityKind k) => k switch
        {
            ActivityKind.SweepStep => Prop.Broom,
            ActivityKind.HangWashing => Prop.Cloth,
            ActivityKind.ChopKindling => Prop.Axe,
            _ => Prop.None,
        };

        /// <summary>#405: the view hours the trip to work takes with the fetch first: to the water, the dip, the carry to where
        /// the load is set down, and the step on to the site.</summary>
        float FetchFirstHours(Place from, Work w) =>
            WalkHours(from, w.fetchFrom) + FetchDipHours + Math.Max(0.35f, WalkHours(w.fetchFrom, w.dest)) + (w.dest != w.site ? WalkHours(w.dest, w.site) : 0f);

        /// <summary>#360: the dip at the water (3.5 s at dawn: kneel, dip, hold, lift).</summary>
        const float FetchDipHours = 0.35f;

        /// <summary>#405: on the way to work: out with the empty bucket, the dip, and the full buckets carried to <see cref="Work.dest"/>.</summary>
        void FetchFirst(Work w)
        {
            var src = w.fetchFrom;
            if (src.IsNone) return;
            if (src != bcur) Move(ActivityKind.Walk, src, bt + WalkHours(bcur, src), w.load == Prop.Buckets ? Prop.EmptyBucket : Prop.None, TripType.HomeToWork);
            At(ActivityKind.FetchWater, bt + FetchDipHours, src, w.load == Prop.Buckets ? Prop.EmptyBucket : Prop.None);
            Move(w.load == Prop.Buckets ? ActivityKind.FetchWater : w.carryKind, w.dest, bt + Math.Max(0.35f, WalkHours(src, w.dest)), w.load, TripType.Carry); // the dawn run's walk back, full
        }

        /// <summary>#361: how long the tip at the stash lasts (real seconds: the 1.4 s beat plus the moment the doll takes to stand still at the pile).</summary>
        public const float TipBlockSeconds = 1.8f;

        /// <summary>#361: the view hours a carry of a basket to the camp's stash pile spends tipping it (0 for any other load or place).</summary>
        static float TipHours(Work work, float end)
        {
            if (work.load != Prop.Basket || work.dest.kind != PlaceKind.Stash) return 0f;
            // the block's last TipBlockSeconds of real time, across a phase's change of speed (a block that ends at midday)
            float h = LifeClock.ViewHoursFor(end - 1e-3f, TipBlockSeconds);
            for (int i = 0; i < 4; i++)
            {
                float real = LifeClock.RealSecondsBetween(Math.Max(0f, end - h), end);
                if (real < 1e-3f || Math.Abs(real - TipBlockSeconds) < 0.01f) break;
                h *= TipBlockSeconds / real;
            }
            return h;
        }

        /// <summary>Work hours in blocks of 1.25–2 view hours (20–40 real s, rule 5): a work loop at the site, or a carry
        /// of the goods to where they go (Banished's busy signal). The day's last block carries the goods to the store.</summary>
        void WorkBlocks(Person p, DayInfo d, Work work, float from, float to, int salt, bool lastCarries)
        {
            int k = p.key;
            long day = d.day;
            float t = Math.Max(from, bt);
            int i = 0;
            while (t < to - 0.01f)
            {
                float len = 1.25f + (H(k, day, salt + i) % 4) * 0.25f;
                float end = Math.Min(to, t + len);
                if (to - end < 0.75f) end = to;
                bool alt = work.altShare > 0f && R(k, day, salt + 100 + i) < work.altShare;
                var site = alt ? work.altSite : work.site;
                var loop = alt ? work.altLoop : work.loop;
                var tool = alt ? work.altTool : work.tool;
                if (work.chores) { loop = Chore(p, d, salt + i); tool = ChoreProp(loop); if (loop == ActivityKind.SitOut) { loop = ActivityKind.SweepStep; tool = Prop.Broom; } }
                bool carry = work.load != Prop.None && (i > 0 || from > LifeClock.WorkStart) && R(k, day, salt + 200 + i) < work.carryShare;
                if (lastCarries && end >= to && work.load != Prop.None) carry = true;
                float tip = TipHours(work, end); // #361: a basket is tipped onto the stash pile at the end of the carry; its time comes out of the work loop, not the blocks after
                if (carry && (work.dest != site || !work.fetchFrom.IsNone))
                {
                    // #30: only when the walk back to the site and the carry both fit the block; a squeezed walk back ran
                    // at several times the walking pace (the merchant's cart loop). Else this block is the work loop.
                    var src0 = work.fetchFrom.IsNone ? site : work.fetchFrom;
                    float fits = (bcur != site ? WalkHours(bcur, site) : 0f) + 0.1f + Math.Max(0.35f, WalkHours(src0, work.dest)) + tip +
                                 (work.fetchFrom.IsNone ? 0f : WalkHours(site, src0));
                    if (end - t < fits) carry = false;
                }
                if (carry && (work.dest != site || !work.fetchFrom.IsNone))
                {
                    var src = work.fetchFrom.IsNone ? site : work.fetchFrom;
                    var dst = work.dest;
                    float cd = Math.Max(0.35f, WalkHours(src, dst));
                    float workUntil = Math.Max(t, end - cd - tip - (work.fetchFrom.IsNone ? 0f : WalkHours(site, src)));
                    if (workUntil > t + 0.1f) At(loop, workUntil, site, tool);
                    // #152: off to the water with the empty bucket; any other fetch goes empty-handed.
                    if (src != bcur) Move(ActivityKind.Walk, src, Math.Min(end - 0.2f, bt + WalkHours(bcur, src)), work.load == Prop.Buckets ? Prop.EmptyBucket : Prop.None, TripType.Errand);
                    Move(work.carryKind, dst, end - tip, work.load, TripType.Carry);
                    if (tip > 0f && bcur == dst && bt >= end - tip - 1e-3f) // #361: stand at the pile and tip the basket onto it
                    {
                        Push(ActivityKind.TipLoad, bt, end, dst, dst, work.load, PersonRef.None, TripType.None, still: true);
                        bt = end;
                    }
                }
                else At(loop, end, site, tool, Ref(work.with));
                t = end; i++;
            }
        }

        // ---------------------------------------------------------------- evening and night

        void Evening(Person p, DayInfo d)
        {
            int k = p.key;
            long day = d.day;
            float home = EndOfEvening(p, d);
            var style = StyleOf(p, d);
            // A very tidy holder in office: the sweepers sweep the green again before the evening gathering.
            if (box.sweeperExtraBlocks >= 2 && p.job == JobType.Sweeper && !d.rest) At(ActivityKind.Sweep, 17.5f, gather, Prop.Broom);
            if (style != EveningStyle.Gather) { OtherEvening(p, d, style, home); return; }
            if (!CampEveningRole(p, d)) At(ActivityKind.SitOut, 17.6f, gather, Prop.None, default, TripType.HomeToGreen); // tester run 1 #3: the fire's roles
            At(leader != null && leader != p ? ActivityKind.Listen : ActivityKind.Chat, 18.3f, gather, Prop.None, Ref(leader));
            ActivityKind free;
            var place = gather;
            float r = R(k, day, 51);
            if (p.hints.singChance > 0f && R(k, day, 52) < p.hints.singChance) free = ActivityKind.Sing;
            else if (tavernB != null && r < 0.3f) { free = ActivityKind.Drink; place = PlaceOf(tavernB); }
            else free = r < 0.7f ? ActivityKind.Chat : ActivityKind.SitOut;
            At(free, Math.Min(home, 19.1f), place, free == ActivityKind.Drink ? Prop.Mug : Prop.None);
            At(ActivityKind.Chat, home, gather);
            At(ActivityKind.Sleep, 24f, p.home, Prop.None, default, TripType.Home);
            if (d.feast) Put(b, Mk(ActivityKind.Eat, 17.6f, 19.2f, gather, Prop.None, ActivitySource.RoleBeat), d);
        }

        /// <summary>Friends meet every evening both are free; rivals argue at most once a day, not every day; reconciled
        /// pairs and couples sit together; single friends court (rules 7, 8).</summary>
        void Relationships(Person p, DayInfo d, List<Seg> list)
        {
            if (EveningBusy(p)) return;
            var f = p.friend;
            if (f != null && f.Grown && !EveningBusy(f) && (StyleOf(p, d) == EveningStyle.Gather || StyleOf(f, d) == EveningStyle.Gather))
            {
                bool court = p.Adult && f.Adult && p.n.partnerId < 0 && f.n.partnerId < 0 && p.age <= 50f && f.age <= 50f;
                Put(list, Mk(court ? ActivityKind.Court : ActivityKind.Chat, 18.3f, 19.1f, gather, court ? Prop.Flower : Prop.None, ActivitySource.Schedule, f.who), d);
            }
            var rv = p.rival;
            if (rv != null && rv.Grown && !EveningBusy(rv) && R(PairKey(p, rv), d.day, 60) < 0.35f && (StyleOf(p, d) == EveningStyle.Gather || StyleOf(rv, d) == EveningStyle.Gather))
                Put(list, Mk(ActivityKind.Argue, 17.2f, 17.55f, gather, Prop.None, ActivitySource.Schedule, rv.who), d);
            else if (p == leader)
                foreach (var q in people) // the leader steps between arguing rivals
                    if (q.rival != null && q.key < q.rival.key && q != p && q.rival != p && q.Grown && q.rival.Grown && !EveningBusy(q) && !EveningBusy(q.rival) && R(PairKey(q, q.rival), d.day, 60) < 0.35f)
                    { Put(list, Mk(ActivityKind.Mediate, 17.25f, 17.55f, gather, Prop.None, ActivitySource.RoleBeat, q.who), d); break; }
            var sit = p.reconciled != null && !EveningBusy(p.reconciled) ? p.reconciled : p.partner != null && p.partner.Grown && !EveningBusy(p.partner) ? p.partner : null;
            // Only while they're still on the green: one who heads home before 19:10 doesn't walk back for a moment's sit
            // (home, back to the green and home again in 0.05 h rushed at 7x, #109).
            float sitUntil = EndOfEvening(p, d) - 0.05f;
            if (sit != null && sitUntil >= 19.15f && StyleOf(p, d) == EveningStyle.Gather) Put(list, Mk(ActivityKind.SitTogether, 19.1f, sitUntil, gather, Prop.None, ActivitySource.Schedule, sit.who), d);
        }

        float EndOfEvening(Person p, DayInfo d)
        {
            // Bedtimes spread over more than an hour (19.2 to 20.3), so the green empties one by one (#197).
            float home = 19.2f + R(p.partner != null ? PairKey(p, p.partner) : p.key, d.day, 50) * 1.1f - box.homeEarlyHours - p.hints.homeEarlyHours;
            return Math.Max(18.4f, home);
        }

        // ---------------------------------------------------------------- minds of their own (#197)

        /// <summary>One adult's working day: when they start, break for lunch, go back and knock off. Each differs per
        /// person and day (a shift: the late starter also lunches later and works on), from the seed and the trait (the
        /// lazy start later), so the box is never all at work or all at the green at once.</summary>
        struct Shift { public float start, lunch, back, end; }

        Shift ShiftOf(Person p, DayInfo d)
        {
            float sf = R(p.key, d.day, 80) + (p.hints.napChance > 0f ? 0.15f : 0f);
            sf = Math.Min(1f, sf);
            int lk = p.partner != null && p.partner.Adult ? PairKey(p, p.partner) : p.key; // a couple eats together
            var s = new Shift { start = LifeClock.WorkStart + sf * 0.9f };
            s.lunch = 11.5f + 0.5f * sf + 0.65f * R(lk, d.day, 81);
            s.back = s.lunch + 0.6f + 0.4f * R(lk, d.day, 82);
            s.end = Math.Max(s.back + 1.2f, LifeClock.AfternoonEnd(d.season) + (sf - 0.5f) * 0.9f);
            if (CampSpread) CampShift(ref s, p, d); // tester run 1 #3
            return s;
        }

        /// <summary>Where a free adult spends the evening: the green and the fire (about four in ten), the tavern, a friend's door,
        /// or their own.</summary>
        enum EveningStyle { Gather, Tavern, Visit, Home, Tower }

        /// <summary>#404: the share of evenings (of those spent at home) someone walks out to the people apart's lookout tower once it stands.</summary>
        public const float TowerEvening = 0.08f;
        /// <summary>#404: the share of evenings the kids dare each other at the tower (about half of them go).</summary>
        public const float TowerDareEvening = 0.2f;

        bool TowerOpen => sim != null && sim.TowerStands;

        /// <summary>Debug (the panel, the tester): half the free evenings go to the tower, and every evening is a dare evening.</summary>
        public static bool DebugTowerEvenings;

        EveningStyle StyleOf(Person p, DayInfo d)
        {
            if (!p.Grown || EveningBusy(p) || p == leader || p.hints.singChance > 0f || d.feast) return EveningStyle.Gather;
            float r = R(p.partner != null ? PairKey(p, p.partner) : p.key, d.day, 54);
            if (r < 0.42f) return EveningStyle.Gather;
            float tav = tavernB != null ? 0.22f : 0f;
            if (r < 0.42f + tav) return EveningStyle.Tavern;
            bool friendDoor = p.friend != null && p.friend.Grown && p.friend.home != p.home && !p.friend.home.IsNone;
            if (friendDoor && r < 0.42f + tav + 0.12f) return EveningStyle.Visit;
            if (TowerOpen && r >= 1f - (DebugTowerEvenings ? 0.5f : TowerEvening)) return EveningStyle.Tower; // #404: a couple decides together (the pair's roll)
            return EveningStyle.Home;
        }

        void OtherEvening(Person p, DayInfo d, EveningStyle style, float home)
        {
            int k = p.key;
            var chore = Chore(p, d, 9);
            float late = Math.Min(home, 19.3f);
            if (style == EveningStyle.Tavern)
            {
                At(ActivityKind.Drink, Math.Max(late, bt + 1f), PlaceOf(tavernB), Prop.Mug);
            }
            else if (style == EveningStyle.Tower)
            {
                // #404: out to the lookout tower at dusk: up on the platform or at its foot (the view decides), home to bed
                At(chore, 17.9f, p.home, ChoreProp(chore));
                At(ActivityKind.SitOut, Math.Max(Math.Min(home, 19.6f), bt + 1.1f), Place.Land(PlaceKind.Tower), Prop.None, p.partner != null ? Ref(p.partner) : default);
            }
            else if (style == EveningStyle.Visit)
            {
                At(chore, 17.9f, p.home, ChoreProp(chore));
                At(ActivityKind.Visit, Math.Max(late, bt + 0.8f), p.friend.home, Prop.None, Ref(p.friend));
            }
            else
            {
                At(chore, 18.2f, p.home, ChoreProp(chore));
                At(R(k, d.day, 55) < 0.5f ? ActivityKind.SitOut : ActivityKind.Chat, Math.Max(late, bt + 0.5f), p.home, Prop.None, p.partner != null ? Ref(p.partner) : default);
            }
            At(ActivityKind.Sleep, 24f, p.home, Prop.None, default, TripType.Home);
        }

        static bool EveningBusy(Person p) => p.role == RoleType.Innkeeper || p.role == RoleType.Captain || p.job == CrowdServer;

        /// <summary>A few night owls so a night open still has life (Choice 2, rule 15): the camp's fire keeper, the tavern's
        /// last drinkers and its server, a fisher by the pond.</summary>
        void NightOwls(Person p, DayInfo d, List<Seg> list)
        {
            int k = p.key;
            if (p == dayFireKeeper) Put(list, Mk(ActivityKind.SitOut, 20.2f, 23f, fire, Prop.Stick), d);
            if (p.job == CrowdServer && tavernB != null) Put(list, Mk(ActivityKind.Serve, 18f, 22f, PlaceOf(tavernB), Prop.Mug), d);
            else if (p.job == JobType.Brewer && tavernB != null) Put(list, Mk(ActivityKind.Serve, 18f, 20f, PlaceOf(tavernB), Prop.Mug), d);
            else if (tavernB != null && !EveningBusy(p) && R(k, d.day, 70) < 0.15f) Put(list, Mk(ActivityKind.Drink, 18.3f, 21.8f, PlaceOf(tavernB), Prop.Mug), d);
            else if (p.job == JobType.Fisher && (sim == null || sim.FishingWater) && R(k, d.day, 71) < 0.3f) // P8: no evening fishing in a dry hollow; #421: nor before the fish
                Put(list, Mk(ActivityKind.Fish, 19.7f, 22f, Place.Land(PlaceKind.Pond), Prop.Lantern), d);
        }

        // ---------------------------------------------------------------- weather (rule 14)

        void WeatherLayer(Person p, DayInfo d, List<Seg> list)
        {
            if (p.stage == LifeStage.Baby) return;
            if (d.weather == Weather.Storm)
            {
                // Everyone outdoors shelters under the eaves for the afternoon: the building they are at or heading for when
                // it breaks (or the one they set off from), else home, and they stay there till it passes. A shelter per
                // piece of the plain afternoon sent them from eaves to eaves through the storm, in walks squeezed into the
                // pieces (#109).
                var shelters = new List<Seg>();
                var at = Place.None;
                foreach (var s in list)
                {
                    if (s.end <= 13f || s.start >= 16f || s.kind == ActivityKind.Sleep) continue;
                    if (at.IsNone) at = s.place.IsBuilding ? s.place : ActivityInfo.IsMove(s.kind) && s.from.IsBuilding ? s.from : p.home;
                    shelters.Add(Mk(ActivityKind.Shelter, Math.Max(13f, s.start), Math.Min(16f, s.end), at));
                }
                foreach (var s in shelters) Put(list, s, d);
            }
            else if (d.weather == Weather.Rainbow)
                Put(list, Mk(ActivityKind.Point, 14f, 15.5f, gather), d); // "Nobody worked that afternoon."
            else if (d.weather == Weather.KeeperRain && d.rainNudgeId >= 0 && p.Adult && (p.job == JobType.Gatherer || p.job == JobType.Farmer) && !d.rest)
                Put(list, Mk(p.job == JobType.Gatherer ? ActivityKind.Gather : ActivityKind.Weed, 14f, 15.5f, Place.Land(PlaceKind.Nudge, d.rainNudgeId), Prop.Basket), d);
            if (eraReady && p.Adult && !d.rest && p != leader)
            {
                // The era waits: the people prepare (era-moment §2): seed baskets by the fire, timber and stone on the green.
                var load = era == Era.Camp ? Prop.SeedBag : (p.key & 1) == 0 ? Prop.Log : Prop.Block;
                var pile = era == Era.Camp ? stash : gather; // #150
                Put(list, Mk(ActivityKind.Carry, 15.5f, 15.9f, pile, load, ActivitySource.Schedule, default, Place.None), d);
                Put(list, Mk(ActivityKind.Prepare, 15.9f, 16.6f, pile, load), d);
            }
        }
    }
}
