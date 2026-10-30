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
        }

        readonly DayInfo dayInfo = new DayInfo();
        readonly Dictionary<int, Seg[]> dayCache = new Dictionary<int, Seg[]>();
        Person dayFireKeeper;
        int buildDepth;

        void ClearDays() { dayCache.Clear(); blockCache.Clear(); dayInfo.day = long.MinValue; }

        void EnsureDay(long day)
        {
            if (dayInfo.day == day) return;
            dayCache.Clear();
            blockCache.Clear();
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
            dayCache[p.key] = segs;
            return segs;
        }

        // ======================================================================== the builder

        List<Seg> b = new List<Seg>(48);
        float bt;
        Place bcur;
        int sweepBlock; // the sweeper's heaps so far today

        /// <summary>The shortest stay worth walking to, in view hours (#109).</summary>
        const float MinStay = 0.05f;

        /// <summary>The share of its route's time a re-routed walk or a carry must get, else it is dropped (#109): at dawn's
        /// fast clock a walk given its full time already shows at 1.6x the pace.</summary>
        const float WalkSlack = 0.85f;

        /// <summary>How much of the stay after it a walk short of time may eat into, arriving late (#109; it was half, and a
        /// long walk to a quarrel on the green still rushed at 2x).</summary>
        const float LateShare = 0.75f;

        void Begin(Place at) { b = new List<Seg>(48); bt = 0f; bcur = at; sweepBlock = 0; }

        void Push(ActivityKind k, float start, float end, Place place, Place from, Prop prop, PersonRef partner, TripType trip, ActivitySource src = ActivitySource.Schedule, int chronicle = -1, bool copies = false, bool still = false)
        {
            if (end <= start + 1e-4f) return;
            b.Add(new Seg { start = start, end = end, kind = k, place = place, from = from, prop = prop, partner = partner, trip = trip, src = src, chronicleId = chronicle, copies = copies, still = still });
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
            float speed = RealWalkSpeed;
            for (int i = 1; i < list.Count; i++)
            {
                var s = list[i];
                if (!s.Moving || s.from == s.place) continue;
                var route = RouteBetween(s.from, s.place);
                if (route == null || route.len <= 1e-4f) continue;
                float need = Math.Min(route.len / speed, MaxWalk * LifeClock.RealSecondsPerViewHour(LifeClock.WorkStart));
                if (LifeClock.RealSecondsBetween(s.start, s.end) >= need * 0.6f) continue;
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
                if (LifeClock.RealSecondsBetween(s.start, s.end) >= need * 0.6f) continue;
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
                At(PlayKind(p, d, 11), 12f, PlayPlace(p, d, 11, near: false), PlayProp(p, d, 11));
            }
            // Midday (village on): carry the meal basket out to a parent in the field.
            var parentField = p.parent != null && p.parent.Adult && era != Era.Camp && !d.rest ? FieldOf(p.parent, d) : Place.None;
            if (!parentField.IsNone)
            {
                // The basket takes as long as the walk out there (from the hill it rushed at 2.6x in 0.3 h, #109).
                Move(ActivityKind.Carry, parentField, Math.Max(12.3f, bt + WalkHours(bcur, parentField)), Prop.Basket, TripType.HomeToWork);
                At(ActivityKind.Eat, 12.8f, parentField, Prop.Basket, Ref(p.parent));
            }
            else At(ActivityKind.Eat, 12.8f, MealPlace(p.parent ?? p, d), Prop.None, Ref(p.parent));
            float t = 13f;
            int block = 0;
            while (t < 17f)
            {
                float end = Math.Min(17f, t + 1.25f + R(k, day, 20 + block) * 0.75f);
                if (17f - end < 0.5f) end = 17f;
                At(PlayKind(p, d, 20 + block), end, PlayPlace(p, d, 20 + block, near: block % 2 == 0), PlayProp(p, d, 20 + block));
                t = end; block++;
            }
            float home = 19.3f + R(k, day, 2) * 0.3f - box.homeEarlyHours;
            At(ActivityKind.Play, 18.3f, gather, Prop.Ball, default, TripType.HomeToGreen);
            if (d.teller != null && d.teller != p) At(ActivityKind.ListenStory, Math.Min(home, 19.1f), gather, Prop.None, Ref(d.teller));
            At(ActivityKind.Play, home, gather);
            At(ActivityKind.Sleep, 24f, p.home, Prop.None, default, TripType.Home);
        }

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
            else At(ActivityKind.SitOut, 9f, gather, Prop.Stick);
            bool minds = R(k, day, 5) < 0.4f;
            At(minds ? ActivityKind.MindBabies : ActivityKind.SitOut, 10f, minds ? p.home : gather);
            if (d.teller == p) At(ActivityKind.StoryTell, 11f, gather);
            else At(ActivityKind.SitOut, 11f, gather, Prop.Stool);
            if (p.hints.sitWithOldChance > 0f && R(k, day, 6) < p.hints.sitWithOldChance) At(ActivityKind.SitWithOld, 12f, gather);
            else At(ActivityKind.SitOut, 12f, gather);
            At(ActivityKind.Eat, 12.9f, MealPlace(p, d), Prop.None, Ref(p.partner));
            At(minds ? ActivityKind.SitOut : ActivityKind.MindBabies, 15f, p.home);
            if (farms.Count > 0 && R(k, day, 7) < 0.5f) At(ActivityKind.WatchWork, 17f, PlaceOf(farms[(int)(H(k, day, 8) % (uint)farms.Count)]), Prop.Stick);
            else At(ActivityKind.SitOut, 17f, gather, Prop.Stool);
            float home = 19.4f + R(k, day, 2) * 0.4f - box.homeEarlyHours - p.hints.homeEarlyHours;
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
            At(ActivityKind.Sleep, (rest ? 6f : 5f) + R(k, day, 1) * 0.6f, p.home);
            At(ActivityKind.Stretch, bt + 0.2f, p.home);

            if (rest) { RestDay(p, d); }
            else
            {
                // Dawn: water from the well or the stream, a chore at the door, then off to work.
                if (R(k, day, 2) < 0.3f)
                {
                    var water = wellB != null ? PlaceOf(wellB) : Place.Land(PlaceKind.Stream);
                    Move(ActivityKind.Walk, water, bt + WalkHours(p.home, water), Prop.Buckets, TripType.HomeToGreen);
                    At(ActivityKind.FetchWater, bt + 0.2f, water, Prop.Buckets);
                    Move(ActivityKind.FetchWater, p.home, bt + WalkHours(water, p.home), Prop.Buckets, TripType.Home);
                }
                if (work.sweeps && box.sweeperExtraBlocks > 0)
                {
                    // A tidy holder in office (the grump, #28): the sweepers sweep the green at dawn before their round. After
                    // the water there may be no time left before the round: the walk to the green still takes its route's
                    // time and they sweep a little, so the round starts late (a squeezed walk rushed at 4x, #110).
                    float sweepUntil = Math.Max(LifeClock.WorkStart - WalkHours(gather, work.site), bt + WalkHours(bcur, gather) + 0.1f);
                    At(ActivityKind.Sweep, sweepUntil, gather, Prop.Broom, default, TripType.HomeToGreen);
                }
                else
                {
                    At(Chore(p, d, 3), 6.6f, p.home, ChoreProp(Chore(p, d, 3)));
                    float walk = WalkHours(p.home, work.site);
                    At(ActivityKind.SitOut, LifeClock.WorkStart - walk, p.home);
                }
                // The walk to work takes as long as the route does: a late start (the water came first) arrives late.
                Move(ActivityKind.Walk, work.site, Math.Max(LifeClock.WorkStart, bt + WalkHours(bcur, work.site)), Prop.None, TripType.HomeToWork);

                if (p.mentor != null) CopyMaster(p, 7f, 9f); // a young apprentice still learns from the master first
                WorkBlocks(p, d, work, LifeClock.WorkStart, LifeClock.Midday, 10, false);
                bool nap = p.hints.napChance > 0f && R(k, day, 4) < p.hints.napChance; // the lazy, after lunch
                // After lunch they walk to the afternoon's work: a role holder's morning duty (the master builder at the
                // mill) may differ from the job they go back to (sweeping their heap, #109).
                var after = WorkOf(p, d, morning: false);
                Midday(p, d, after, nap);
                if (nap) At(ActivityKind.Nap, 14f, gather); // straight from lunch to the green (#64)
                WorkBlocks(p, d, after, bt, LifeClock.Evening, 30, true);
            }
            Evening(p, d);
        }

        void RestDay(Person p, DayInfo d)
        {
            int k = p.key;
            long day = d.day;
            At(Chore(p, d, 5), 8.5f, p.home, ChoreProp(Chore(p, d, 5)));
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

        void Midday(Person p, DayInfo d, Work work, bool napAfter = false)
        {
            var meal = MealPlace(p, d);
            var with = p.partner != null && p.partner.Adult ? p.partner : null;
            // The walk back to work takes its route's time: from a far inn they finish eating earlier, and arrive late if
            // they got there late (a 0.2 h walk back across the town rushed at 2x, #109).
            float back = !napAfter && meal != work.site ? WalkHours(meal, work.site) : 0f;
            float arrive = bt + (meal != bcur ? WalkHours(bcur, meal) : 0f);
            At(ActivityKind.Eat, Math.Min(12.8f, Math.Max(13f - back, arrive + 0.3f)), meal, Prop.None, Ref(with));
            if (napAfter) return; // off to the green for a nap instead of back to work
            if (meal != work.site) { if (bcur != work.site) Move(ActivityKind.Walk, work.site, Math.Max(13f, bt + WalkHours(bcur, work.site)), Prop.None, TripType.HomeToWork); }
            else At(ActivityKind.SitOut, 13f, meal);
        }

        /// <summary>Where a person eats (§3 midday): the fire in the camp; the field edge or the doorstep in the village; the
        /// market and the inn's benches in the town. A couple eats where the first of them does.</summary>
        Place MealPlace(Person p, DayInfo d)
        {
            if (p.partner != null && p.partner.key < p.key && p.partner.Grown) p = p.partner;
            if (era == Era.Camp) return fire;
            if (era == Era.Village)
            {
                var f = p.Adult ? FieldOf(p, d) : Place.None;
                return f.IsNone ? p.home : f;
            }
            return tavernB != null && R(p.key, d.day, 40) < 0.5f ? PlaceOf(tavernB) : marketB != null ? PlaceOf(marketB) : p.home;
        }

        Place FieldOf(Person p, DayInfo d)
        {
            if (p.jobBuilding == null || p.jobBuilding.type != BuildingType.Farm) return Place.None;
            if (p.job != JobType.Farmer && p.job != JobType.Gatherer) return Place.None;
            if (d.season == Season.Winter) return Place.None;
            return PlaceOf(p.jobBuilding);
        }

        ActivityKind Chore(Person p, DayInfo d, int salt) => (H(p.key, d.day, salt + 600) % 4) switch
        {
            0 => ActivityKind.SweepStep,
            1 => ActivityKind.HangWashing,
            2 => ActivityKind.ChopKindling,
            _ => ActivityKind.SitOut,
        };

        static Prop ChoreProp(ActivityKind k) => k switch
        {
            ActivityKind.SweepStep => Prop.Broom,
            ActivityKind.HangWashing => Prop.Cloth,
            ActivityKind.ChopKindling => Prop.Axe,
            _ => Prop.None,
        };

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
                if (work.sweeps && !alt)
                {
                    // The sweeper stops at their heaps in turn (§7, #60: the one SimCore.SweeperTargets names first); with no
                    // heap left for them, the streets.
                    int heapId = SweepHeap(p, day, sweepBlock++);
                    site = heapId >= 0 ? Place.Land(PlaceKind.TrashHeap, heapId) : gather;
                }
                if (work.chores) { loop = Chore(p, d, salt + i); tool = ChoreProp(loop); if (loop == ActivityKind.SitOut) { loop = ActivityKind.SweepStep; tool = Prop.Broom; } }
                bool carry = work.load != Prop.None && (i > 0 || from > LifeClock.WorkStart) && R(k, day, salt + 200 + i) < work.carryShare;
                if (lastCarries && end >= to && work.load != Prop.None) carry = true;
                if (carry && (work.dest != site || !work.fetchFrom.IsNone))
                {
                    // #30: only when the walk back to the site and the carry both fit the block; a squeezed walk back ran
                    // at several times the walking pace (the merchant's cart loop). Else this block is the work loop.
                    var src0 = work.fetchFrom.IsNone ? site : work.fetchFrom;
                    float fits = (bcur != site ? WalkHours(bcur, site) : 0f) + 0.1f + Math.Max(0.35f, WalkHours(src0, work.fetchFrom.IsNone ? work.dest : site)) +
                                 (work.fetchFrom.IsNone ? 0f : WalkHours(site, src0));
                    if (end - t < fits) carry = false;
                }
                if (carry && (work.dest != site || !work.fetchFrom.IsNone))
                {
                    var src = work.fetchFrom.IsNone ? site : work.fetchFrom;
                    var dst = work.fetchFrom.IsNone ? work.dest : site;
                    float cd = Math.Max(0.35f, WalkHours(src, dst));
                    float workUntil = Math.Max(t, end - cd - (work.fetchFrom.IsNone ? 0f : WalkHours(site, src)));
                    if (workUntil > t + 0.1f) At(loop, workUntil, site, tool);
                    if (src != bcur) Move(ActivityKind.Walk, src, Math.Min(end - 0.2f, bt + WalkHours(bcur, src)), Prop.None, TripType.Errand);
                    Move(work.carryKind, dst, end, work.load, TripType.Carry);
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
            float home = 19.6f + R(p.partner != null ? PairKey(p, p.partner) : k, day, 50) * 0.5f - box.homeEarlyHours - p.hints.homeEarlyHours;
            home = Math.Max(18.4f, home);
            // A very tidy holder in office: the sweepers sweep the green again before the evening gathering.
            if (box.sweeperExtraBlocks >= 2 && p.job == JobType.Sweeper && !d.rest) At(ActivityKind.Sweep, 17.5f, gather, Prop.Broom);
            At(ActivityKind.SitOut, 17.6f, gather, Prop.None, default, TripType.HomeToGreen);
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
            if (f != null && f.Grown && !EveningBusy(f))
            {
                bool court = p.Adult && f.Adult && p.n.partnerId < 0 && f.n.partnerId < 0 && p.age <= 50f && f.age <= 50f;
                Put(list, Mk(court ? ActivityKind.Court : ActivityKind.Chat, 18.3f, 19.1f, gather, court ? Prop.Flower : Prop.None, ActivitySource.Schedule, f.who), d);
            }
            var rv = p.rival;
            if (rv != null && rv.Grown && !EveningBusy(rv) && R(PairKey(p, rv), d.day, 60) < 0.35f)
                Put(list, Mk(ActivityKind.Argue, 17.2f, 17.55f, gather, Prop.None, ActivitySource.Schedule, rv.who), d);
            else if (p == leader)
                foreach (var q in people) // the leader steps between arguing rivals
                    if (q.rival != null && q.key < q.rival.key && q != p && q.rival != p && q.Grown && q.rival.Grown && !EveningBusy(q) && !EveningBusy(q.rival) && R(PairKey(q, q.rival), d.day, 60) < 0.35f)
                    { Put(list, Mk(ActivityKind.Mediate, 17.25f, 17.55f, gather, Prop.None, ActivitySource.RoleBeat, q.who), d); break; }
            var sit = p.reconciled != null && !EveningBusy(p.reconciled) ? p.reconciled : p.partner != null && p.partner.Grown && !EveningBusy(p.partner) ? p.partner : null;
            // Only while they're still on the green: one who heads home before 19:10 doesn't walk back for a moment's sit
            // (home, back to the green and home again in 0.05 h rushed at 7x, #109).
            float sitUntil = EndOfEvening(p, d) - 0.05f;
            if (sit != null && sitUntil >= 19.15f) Put(list, Mk(ActivityKind.SitTogether, 19.1f, sitUntil, gather, Prop.None, ActivitySource.Schedule, sit.who), d);
        }

        float EndOfEvening(Person p, DayInfo d)
        {
            float home = 19.6f + R(p.partner != null ? PairKey(p, p.partner) : p.key, d.day, 50) * 0.5f - box.homeEarlyHours - p.hints.homeEarlyHours;
            return Math.Max(18.4f, home);
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
            else if (p.job == JobType.Fisher && R(k, d.day, 71) < 0.3f) Put(list, Mk(ActivityKind.Fish, 19.7f, 22f, Place.Land(PlaceKind.Pond), Prop.Lantern), d);
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
                Put(list, Mk(ActivityKind.Carry, 15.5f, 15.9f, gather, load, ActivitySource.Schedule, default, Place.None), d);
                Put(list, Mk(ActivityKind.Prepare, 15.9f, 16.6f, gather, load), d);
            }
        }
    }
}
