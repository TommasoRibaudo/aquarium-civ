using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #709 (B71.c, B71.d): what the people do at the nest, in spring while it holds eggs. Two small things, never on the same day:
    /// a gatherer walks to the nest's bush with a basket, takes an egg (settled once through <see cref="SimCore.TakeNestEgg"/>: never the last)
    /// and carries it to the pile; or one to three kids creep up, look in and go home (they never touch it). A work use and a life use for the
    /// same thing (people-and-things). Hashed on (seed, person, day, salt) so the view and the sim agree; nothing is saved. Every number is [est].
    /// </summary>
    public sealed class NestPlan
    {
        public long day;
        /// <summary>True: a gatherer takes an egg. False: kids look in.</summary>
        public bool gather;
        /// <summary>The gatherer's key (gather), or the lookers' (kids, one to three).</summary>
        public int key;
        public readonly List<int> lookerKeys = new List<int>();
        /// <summary>A grown-up who beckons the kids away at the end of a look (the view's call; -1 = none).</summary>
        public int beckonKey = -1;
        public int bushCell;
        public Place from;
        /// <summary>View hours: they leave, they reach the bush and start (the pick or the look), the egg is taken / the look is over (settled), they are back at the pile (gather) or home (look), the beat ends.</summary>
        public float start, pickStart, resolveHour, carryEnd, end;
        public bool forced, debugStarted;
        public bool resolved, taken;
        public double ResolveAbsolute => day * 24.0 + resolveHour;
    }

    public sealed partial class LifePlanner
    {
        // ---------------------------------------------------------------- tuning [est]
        /// <summary>The share of working days a gatherer takes a nest egg ("now and then", while one is spare), the share of days the kids look in (B71.c: 1 in 2), the hour a gather leaves and its spread, the look's hour and spread, and the beats in view hours.</summary>
        public const float NestGatherDayChance = 0.2f, NestLookDayChance = 0.5f, NestGatherFrom = 9.4f, NestGatherSpread = 3f,
            NestLookFrom = 13.5f, NestLookSpread = 3f, NestPickHours = 0.35f, NestLookHours = 0.3f, NestTipHours = 0.25f;

        readonly Dictionary<long, List<NestPlan>> nestDays = new Dictionary<long, List<NestPlan>>();
        static readonly List<NestPlan> NoNest = new List<NestPlan>();

        /// <summary>The nest visits of a view day: at most one, a gather or a look. Empty unless the ecology is on and the nest holds eggs. Made once per day (while
        /// the nest is in the egg season) and kept.</summary>
        public IReadOnlyList<NestPlan> NestVisitsOn(long day)
        {
            Sync();
            return NestVisitsOf(day);
        }

        List<NestPlan> NestVisitsOf(long day)
        {
            if (sim == null || !sim.EcologyOn || !sim.NestKidsMayLook) return NestDayList(day) ?? NoNest;
            if (nestDays.TryGetValue(day, out var list)) return list;
            EnsureDay(day);
            List<long> drop = null;
            foreach (var k in nestDays.Keys) if (k < day - 2) (drop ??= new List<long>()).Add(k);
            if (drop != null) foreach (var k in drop) nestDays.Remove(k);
            list = new List<NestPlan>();
            nestDays[day] = list;
            if (dayInfo.weather == Weather.Storm) return list;
            int bush = sim.NestCell;
            if (bush < 0 || !IsValidPlace(Place.Land(PlaceKind.BushCell, bush))) return list;
            bool rest = LifeClock.IsRestDay(day);
            if (!rest && sim.NestEggsSpare > 0 && R(0, day, 450) < NestGatherDayChance)
            {
                var g = MakeNestGather(day, NestGatherFrom + NestGatherSpread * R(0, day, 451), bush);
                if (g != null) { list.Add(g); return list; }
            }
            if (R(0, day, 452) < NestLookDayChance)
            {
                var l = MakeNestLook(day, NestLookFrom + NestLookSpread * R(0, day, 453), bush);
                if (l != null) list.Add(l);
            }
            return list;
        }

        List<NestPlan> NestDayList(long day) => nestDays.TryGetValue(day, out var l) ? l : null;

        /// <summary>Who takes the egg: a gatherer first, else a grown-up who is not the toolmaker, the cook, a hunter or minding a baby; the lowest hash of the day.</summary>
        NestPlan MakeNestGather(long day, float start, int bush)
        {
            BuildCarers();
            Person best = null; int bestRank = 0; uint bestHash = 0;
            foreach (var p in people)
            {
                if (!p.Adult || p.job == JobType.Toolmaker || p.job == CrowdCook || p.job == JobType.Hunter || carerBabies.ContainsKey(p.key)) continue;
                int rank = p.job == JobType.Gatherer ? 0 : 1;
                uint h = H(p.key, day, 454);
                if (best == null || rank < bestRank || (rank == bestRank && h < bestHash)) { best = p; bestRank = rank; bestHash = h; }
            }
            if (best == null) return null;
            var at = Place.Land(PlaceKind.BushCell, bush);
            var from = BasePlace(best, day, start);
            float walkOut = Math.Max(0.1f, WalkHours(from, at));
            float walkBack = Math.Max(0.15f, WalkHours(at, stash));
            var plan = new NestPlan { day = day, gather = true, key = best.key, bushCell = bush, start = start, from = from };
            plan.pickStart = start + walkOut;
            plan.resolveHour = plan.pickStart + NestPickHours;
            plan.carryEnd = plan.resolveHour + walkBack;
            plan.end = plan.carryEnd + (stash.kind == PlaceKind.Stash ? NestTipHours : 0f);
            return plan;
        }

        /// <summary>The kids who look: one to three, the lowest hashes of the day; a grown-up who beckons them away at the end (the view's beat).</summary>
        NestPlan MakeNestLook(long day, float start, int bush)
        {
            var kids = new List<(uint h, Person p)>();
            Person adult = null; uint adultHash = 0;
            foreach (var p in people)
            {
                if (!p.Adult && p.stage == LifeStage.Kid) kids.Add((H(p.key, day, 455), p));
                else if (p.Adult && p.job != JobType.Toolmaker && p.job != CrowdCook && p.job != JobType.Hunter)
                {
                    uint h = H(p.key, day, 456);
                    if (adult == null || h < adultHash) { adult = p; adultHash = h; }
                }
            }
            if (kids.Count == 0) return null;
            kids.Sort((a, b) => a.h.CompareTo(b.h));
            int n = Math.Min(kids.Count, 1 + (int)(R(0, day, 457) * 3f));
            var at = Place.Land(PlaceKind.BushCell, bush);
            var plan = new NestPlan { day = day, gather = false, bushCell = bush, start = start, beckonKey = adult != null ? adult.key : -1 };
            for (int i = 0; i < n; i++) plan.lookerKeys.Add(kids[i].p.key);
            plan.key = plan.lookerKeys[0];
            plan.from = BasePlace(kids[0].p, day, start);
            float walkOut = Math.Max(0.1f, WalkHours(plan.from, at));
            float walkBack = Math.Max(0.15f, WalkHours(at, stash));
            plan.pickStart = start + walkOut;
            plan.resolveHour = plan.pickStart + NestLookHours;
            plan.carryEnd = plan.resolveHour + walkBack;
            plan.end = plan.carryEnd;
            return plan;
        }

        /// <summary>Settle every nest visit whose pick (or look) is over at <paramref name="now"/> (the view calls this every frame, as <see cref="ResolveEggGathers"/>):
        /// a gather through <see cref="SimCore.TakeNestEgg"/>, a look through <see cref="SimCore.NoteNestLook"/>, each once. Returns how many settled now.</summary>
        public int ResolveNestVisits(ViewTime now, List<NestPlan> settled = null)
        {
            Sync();
            if (sim == null || !sim.EcologyOn || nestDays.Count == 0) return 0;
            int count = 0;
            foreach (var kv in nestDays)
                foreach (var plan in kv.Value)
                {
                    if (plan.resolved || plan.ResolveAbsolute > now.Absolute) continue;
                    plan.resolved = true;
                    if (plan.gather) plan.taken = sim.TakeNestEgg();
                    else sim.NoteNestLook();
                    settled?.Add(plan);
                    count++;
                }
            return count;
        }

        /// <summary>Debug ("Take a nest egg" / "Kids look at the nest"): the visit starts now. False when the ecology is off, no nest stands, or nobody is free. For a gather the sim
        /// decides at the pick's end (lay eggs first, <see cref="SimCore.DebugNestLay"/>; the last egg is never taken).</summary>
        public bool DebugStartNestVisit(ViewTime now, bool gather, out NestPlan plan)
        {
            plan = null;
            Sync();
            if (sim == null || !sim.EcologyOn || sim.NestCell < 0) return false;
            EnsureDay(now.day);
            if (!nestDays.TryGetValue(now.day, out var list) || ReferenceEquals(list, NoNest)) { list = new List<NestPlan>(); nestDays[now.day] = list; }
            plan = gather ? MakeNestGather(now.day, now.hour + 0.02f, sim.NestCell) : MakeNestLook(now.day, now.hour + 0.02f, sim.NestCell);
            if (plan == null) return false;
            plan.forced = plan.debugStarted = true;
            list.Add(plan);
            return true;
        }

        /// <summary>The person's nest beat now, if they are on one (called from <see cref="PlanBase"/>). A gatherer: walk, a basket gather at the bush, carry (or walk back
        /// empty-handed when no egg was spare), tip at the stash. A looker: walk, Inspect at the bush, walk home.</summary>
        bool NestActivityAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (nestDays.Count == 0 || !nestDays.TryGetValue(t.day, out var list)) return false;
            foreach (var plan in list)
            {
                bool mine = plan.gather ? plan.key == p.key : plan.lookerKeys.Contains(p.key);
                if (!mine || t.hour < plan.start || t.hour >= plan.end) continue;
                var at = Place.Land(PlaceKind.BushCell, plan.bushCell);
                if (!IsValidPlace(at)) return false;
                if (t.hour < plan.pickStart)
                    a = NestAct(p, t, ActivityKind.Walk, at, plan.from, Prop.None, true, plan.start, plan.pickStart);
                else if (t.hour < plan.resolveHour)
                    a = plan.gather ? NestAct(p, t, ActivityKind.Gather, at, at, Prop.Basket, false, plan.pickStart, plan.resolveHour)
                                    : NestAct(p, t, ActivityKind.Inspect, at, at, Prop.None, false, plan.pickStart, plan.resolveHour);
                else if (plan.gather)
                {
                    bool empty = plan.resolved && !plan.taken;
                    if (t.hour < plan.carryEnd)
                        a = NestAct(p, t, empty ? ActivityKind.Walk : ActivityKind.Carry, stash, at, empty ? Prop.None : Prop.Basket, true, plan.resolveHour, plan.carryEnd);
                    else if (empty) return false;
                    else a = NestAct(p, t, ActivityKind.TipLoad, stash, stash, Prop.Basket, false, plan.carryEnd, plan.end);
                }
                else a = NestAct(p, t, ActivityKind.Walk, stash, at, Prop.None, true, plan.resolveHour, plan.carryEnd);
                return true;
            }
            return false;
        }

        PersonActivity NestAct(Person p, ViewTime t, ActivityKind kind, Place place, Place from, Prop prop, bool moving, float start, float end)
            => new PersonActivity
            {
                who = p.who, kind = kind, place = place, from = from, moving = moving && from != place, prop = prop, start = start, end = end,
                progress = Math.Max(0f, Math.Min(1f, (t.hour - start) / Math.Max(1e-4f, end - start))), pace = 1f, chronicleId = -1,
            };
    }
}
