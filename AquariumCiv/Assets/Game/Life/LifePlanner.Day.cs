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
        }

        readonly DayInfo dayInfo = new DayInfo();
        readonly Dictionary<int, Seg[]> dayCache = new Dictionary<int, Seg[]>();
        Person dayFireKeeper;
        int buildDepth;

        void ClearDays() { dayCache.Clear(); dayInfo.day = long.MinValue; }

        void EnsureDay(long day)
        {
            if (dayInfo.day == day) return;
            dayCache.Clear();
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

        void Begin(Place at) { b = new List<Seg>(48); bt = 0f; bcur = at; }

        void Push(ActivityKind k, float start, float end, Place place, Place from, Prop prop, PersonRef partner, TripType trip, ActivitySource src = ActivitySource.Schedule, int chronicle = -1, bool copies = false)
        {
            if (end <= start + 1e-4f) return;
            b.Add(new Seg { start = start, end = end, kind = k, place = place, from = from, prop = prop, partner = partner, trip = trip, src = src, chronicleId = chronicle, copies = copies });
        }

        /// <summary>Stay at a place doing <paramref name="k"/> until <paramref name="until"/>; walks there first if needed
        /// (every walk has a destination, rule 3).</summary>
        void At(ActivityKind k, float until, Place place, Prop prop = Prop.None, PersonRef partner = default, TripType trip = TripType.Errand)
        {
            if (until <= bt + 1e-4f) return;
            if (place != bcur)
            {
                float w = Math.Min(WalkHours(bcur, place), (until - bt) * 0.5f);
                Push(ActivityKind.Walk, bt, bt + w, place, bcur, Prop.None, PersonRef.None, trip);
                bt += w;
            }
            Push(k, bt, until, place, place, prop, partner.IsNone ? PersonRef.None : partner, TripType.None);
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
                if (o.start < s.start) { var a = o; a.end = s.start; list.Insert(i, a); i++; }
                if (o.end > s.end) { var c = o; c.start = s.end; list.Insert(i, c); }
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
            foreach (var s0 in list)
            {
                var s = s0;
                if (s.start < t) s.start = t;
                if (s.end - s.start < 0.02f && outList.Count > 0) { var last = outList[outList.Count - 1]; last.end = s.end; outList[outList.Count - 1] = last; t = s.end; continue; }
                if (s.start > t && outList.Count > 0) { var last = outList[outList.Count - 1]; last.end = s.start; outList[outList.Count - 1] = last; }
                bool move = ActivityInfo.IsMove(s.kind);
                if (move && !s.from.IsNone && s.from != cur && s.from != s.place)
                {
                    // A carry starts where its load is: walk there first (a role beat may have moved the person away).
                    float w = Math.Min(WalkHours(cur, s.from), (s.end - s.start) * 0.5f);
                    outList.Add(new Seg { start = s.start, end = s.start + w, kind = ActivityKind.Walk, place = s.from, from = cur, src = s.src, trip = TripType.Errand, chronicleId = -1, partner = PersonRef.None });
                    s.start += w;
                }
                else if (move) s.from = cur;
                else if (s.place != cur)
                {
                    float w = Math.Min(WalkHours(cur, s.place), (s.end - s.start) * 0.5f);
                    outList.Add(new Seg { start = s.start, end = s.start + w, kind = ActivityKind.Walk, place = s.place, from = cur, src = s.src, trip = TripType.Errand, chronicleId = -1, partner = PersonRef.None });
                    s.start += w;
                }
                if (!move) s.from = s.place;
                outList.Add(s);
                cur = s.place;
                t = s.end;
            }
            if (outList.Count == 0 || outList[0].start > 0f)
                outList.Insert(0, new Seg { start = 0f, end = outList.Count > 0 ? outList[0].start : 24f, kind = ActivityKind.Sleep, place = p.home, from = p.home, chronicleId = -1, partner = PersonRef.None });
            var lastSeg = outList[outList.Count - 1];
            if (lastSeg.end < 24f) { lastSeg.end = 24f; outList[outList.Count - 1] = lastSeg; }
            // Merge neighbours that are the same thing (two work blocks of the same loop read as one).
            for (int i = outList.Count - 1; i > 0; i--)
            {
                var a = outList[i - 1];
                var c = outList[i];
                if (a.kind != c.kind || a.place != c.place || a.from != c.from || a.prop != c.prop || a.partner != c.partner || a.src != c.src ||
                    a.chronicleId != c.chronicleId || a.copies != c.copies || ActivityInfo.IsMove(a.kind)) continue;
                a.end = c.end;
                outList[i - 1] = a;
                outList.RemoveAt(i);
            }
            return outList.ToArray();
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
            if (p.mentor != null && !d.rest) CopyMaster(p, 7f, 12f);
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
                Move(ActivityKind.Carry, parentField, 12.3f, Prop.Basket, TripType.HomeToWork);
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
                if (s.Moving) { Move(s.kind, s.place, e, prop, s.trip); continue; }
                Push(s.kind, a, e, s.place, s.place, prop, p.mentor.who, TripType.None, ActivitySource.Schedule, -1, true);
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
                At(Chore(p, d, 3), 6.6f, p.home, ChoreProp(Chore(p, d, 3)));
                float walk = WalkHours(p.home, work.site);
                At(ActivityKind.SitOut, LifeClock.WorkStart - walk, p.home);
                Move(ActivityKind.Walk, work.site, LifeClock.WorkStart, Prop.None, TripType.HomeToWork);

                if (p.mentor != null) CopyMaster(p, 7f, 9f); // a young apprentice still learns from the master first
                WorkBlocks(p, d, work, LifeClock.WorkStart, LifeClock.Midday, 10, false);
                Midday(p, d, work);
                var after = WorkOf(p, d, morning: false);
                if (p.hints.napChance > 0f && R(k, day, 4) < p.hints.napChance) At(ActivityKind.Nap, 14f, gather); // the lazy, after lunch
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

        void Midday(Person p, DayInfo d, Work work)
        {
            var meal = MealPlace(p, d);
            var with = p.partner != null && p.partner.Adult ? p.partner : null;
            At(ActivityKind.Eat, 12.8f, meal, Prop.None, Ref(with));
            if (meal != work.site) Move(ActivityKind.Walk, work.site, 13f, Prop.None, TripType.HomeToWork);
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
            int heap = 0;
            while (t < to - 0.01f)
            {
                float len = 1.25f + (H(k, day, salt + i) % 4) * 0.25f;
                float end = Math.Min(to, t + len);
                if (to - end < 0.75f) end = to;
                bool alt = work.altShare > 0f && R(k, day, salt + 100 + i) < work.altShare;
                var site = alt ? work.altSite : work.site;
                var loop = alt ? work.altLoop : work.loop;
                var tool = alt ? work.altTool : work.tool;
                if (work.sweeps)
                {
                    // The sweeper stops at each heap in turn (§7); with no heap, the streets.
                    if (trashList.Count > 0) site = Place.Land(PlaceKind.TrashHeap, trashList[(int)((H(k, day, 77) + heap++) % (uint)trashList.Count)]);
                }
                if (work.chores) { loop = Chore(p, d, salt + i); tool = ChoreProp(loop); if (loop == ActivityKind.SitOut) { loop = ActivityKind.SweepStep; tool = Prop.Broom; } }
                bool carry = work.load != Prop.None && (i > 0 || from > LifeClock.WorkStart) && R(k, day, salt + 200 + i) < work.carryShare;
                if (lastCarries && end >= to && work.load != Prop.None) carry = true;
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
            if (sit != null) Put(list, Mk(ActivityKind.SitTogether, 19.1f, Math.Max(19.15f, EndOfEvening(p, d) - 0.05f), gather, Prop.None, ActivitySource.Schedule, sit.who), d);
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
                // Everyone outdoors shelters under the eaves for the afternoon: the nearest building, else home.
                var shelters = new List<Seg>();
                foreach (var s in list)
                {
                    if (s.end <= 13f || s.start >= 16f || s.kind == ActivityKind.Sleep) continue;
                    var shelter = s.place.IsBuilding ? s.place : p.home;
                    shelters.Add(Mk(ActivityKind.Shelter, Math.Max(13f, s.start), Math.Min(16f, s.end), shelter));
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
