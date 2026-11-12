using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #574 (playtest #551: "I haven't seen anyone hunt ducks"; nothing ever called <see cref="SimCore.TakeEgg"/>): the minimal egg
    /// gather. On some days, while the nests hold eggs and the flock is above its floor, one grown-up walks to the pond edge with
    /// a basket, kneels at the reeds, takes an egg and carries it to the camp pile. The sim is settled once, at <see cref="EggPlan.resolveHour"/>,
    /// by <see cref="LifePlanner.ResolveEggGathers"/> through <see cref="SimCore.TakeEgg"/> (the floor, the spring rule and the season marks all hold
    /// there); a refused take is a short walk back with an empty basket. A small activity, not a system: #428 (the egg scene) lays its beats
    /// over this plan. Hashed, so the view and the sim agree.
    /// </summary>
    public sealed class EggPlan
    {
        public long day;
        public PersonRef who;
        public int key;
        /// <summary>Where they set out from (their place at <see cref="start"/>).</summary>
        public Place from;
        /// <summary>View hours: they leave, kneel at the reeds, the egg is taken (settled), they reach the pile (and tip the basket there), the beat ends.</summary>
        public float start, pickStart, resolveHour, carryEnd, end;
        public bool forced, debugStarted;
        /// <summary>Settled in the sim, and whether an egg really was taken.</summary>
        public bool resolved, taken;
        public double ResolveAbsolute => day * 24.0 + resolveHour;
    }

    public sealed partial class LifePlanner
    {
        // ---------------------------------------------------------------- tuning [est]
        /// <summary>The share of working days with an egg gather (while there are eggs to take), the hour they leave (after the dawn drink) and its spread, and the beats in view hours.</summary>
        public const float EggDayChance = 0.55f, EggFrom = 9.2f, EggSpread = 3f, EggPickHours = 0.35f, EggTipHours = 0.25f;

        readonly Dictionary<long, List<EggPlan>> eggDays = new Dictionary<long, List<EggPlan>>();
        static readonly List<EggPlan> NoEggs = new List<EggPlan>();

        /// <summary>The egg gathers of a view day (at most one), in the order they settle. Empty unless the ecology is on, ducks are placed,
        /// the nests hold an egg to spare (<see cref="SimCore.EggSurplus"/>), and there is water to walk to. Made once per day and kept.</summary>
        public IReadOnlyList<EggPlan> EggGathersOn(long day)
        {
            Sync();
            return EggGathersOf(day);
        }

        List<EggPlan> EggGathersOf(long day)
        {
            if (sim == null || !sim.EcologyOn || sim.EcoDucks == 0) return NoEggs;
            if (eggDays.TryGetValue(day, out var list)) return list;
            EnsureDay(day);
            List<long> drop = null;
            foreach (var k in eggDays.Keys) if (k < day - 2) (drop ??= new List<long>()).Add(k);
            if (drop != null) foreach (var k in drop) eggDays.Remove(k);
            list = new List<EggPlan>();
            eggDays[day] = list;
            if (LifeClock.IsRestDay(day) || dayInfo.weather == Weather.Storm || sim.EggSurplus <= 0 || !sim.OpenWater) return list;
            if (R(0, day, 440) >= EggDayChance) return list;
            var plan = MakeEggGather(day, EggFrom + EggSpread * R(0, day, 441));
            if (plan != null) list.Add(plan);
            return list;
        }

        /// <summary>Who goes: a gatherer first, else any grown-up who is not the toolmaker, the cook, a hunter or minding a baby; the lowest hash of the day.</summary>
        EggPlan MakeEggGather(long day, float start)
        {
            BuildCarers();
            Person best = null; int bestRank = 0; uint bestHash = 0;
            foreach (var p in people)
            {
                if (!p.Adult || p.job == JobType.Toolmaker || p.job == CrowdCook || p.job == JobType.Hunter || carerBabies.ContainsKey(p.key)) continue;
                int rank = p.job == JobType.Gatherer ? 0 : 1;
                uint h = H(p.key, day, 442);
                if (best == null || rank < bestRank || (rank == bestRank && h < bestHash)) { best = p; bestRank = rank; bestHash = h; }
            }
            if (best == null) return null;
            var pond = Place.Land(PlaceKind.Pond);
            var from = BasePlace(best, day, start);
            float walkOut = Math.Max(0.1f, WalkHours(from, pond));
            float walkBack = Math.Max(0.15f, WalkHours(pond, stash));
            var plan = new EggPlan { day = day, who = best.who, key = best.key, start = start, from = from };
            plan.pickStart = start + walkOut;
            plan.resolveHour = plan.pickStart + EggPickHours;
            plan.carryEnd = plan.resolveHour + walkBack;
            plan.end = plan.carryEnd + (stash.kind == PlaceKind.Stash ? EggTipHours : 0f);
            return plan;
        }

        /// <summary>Settle every egg gather whose pick is over at <paramref name="now"/> (the view calls this every frame, as
        /// <see cref="ResolveHunts"/>): each once, through <see cref="SimCore.TakeEgg"/>. Returns how many settled now.</summary>
        public int ResolveEggGathers(ViewTime now, List<EggPlan> settled = null)
        {
            Sync();
            if (sim == null || !sim.EcologyOn) return 0;
            int count = 0;
            foreach (var kv in eggDays)
                foreach (var plan in kv.Value)
                {
                    if (plan.resolved || plan.ResolveAbsolute > now.Absolute) continue;
                    plan.resolved = true;
                    plan.taken = sim.TakeEgg();
                    settled?.Add(plan);
                    count++;
                }
            return count;
        }

        /// <summary>Debug ("Gather an egg now"): one grown-up goes to the reeds from now. False when the ecology is off, no ducks are placed,
        /// there is no water, or nobody is free. Whether an egg is taken is the sim's call at the pick's end (put eggs in the nests first,
        /// <see cref="SimCore.DebugEggsToNests"/>).</summary>
        public bool DebugStartEggGather(ViewTime now, out EggPlan plan)
        {
            plan = null;
            Sync();
            if (sim == null || !sim.EcologyOn || sim.EcoDucks == 0 || !sim.OpenWater) return false;
            EnsureDay(now.day);
            var list = EggGathersOf(now.day);
            if (ReferenceEquals(list, NoEggs)) { list = new List<EggPlan>(); eggDays[now.day] = list; }
            plan = MakeEggGather(now.day, now.hour + 0.02f);
            if (plan == null) return false;
            plan.forced = plan.debugStarted = true;
            list.Add(plan);
            return true;
        }

        /// <summary>The person's egg gather beat now, if they are on one (called from <see cref="PlanBase"/>).</summary>
        bool EggActivityAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (eggDays.Count == 0 || !eggDays.TryGetValue(t.day, out var list)) return false;
            foreach (var plan in list)
            {
                if (plan.key != p.key || t.hour < plan.start || t.hour >= plan.end) continue;
                var pond = Place.Land(PlaceKind.Pond);
                bool empty = plan.resolved && !plan.taken;
                if (t.hour < plan.pickStart)
                    a = EggAct(p, t, ActivityKind.Walk, pond, plan.from, Prop.None, true, plan.start, plan.pickStart);
                else if (t.hour < plan.resolveHour)
                    a = EggAct(p, t, ActivityKind.Gather, pond, pond, Prop.Basket, false, plan.pickStart, plan.resolveHour);
                else if (t.hour < plan.carryEnd)
                    a = EggAct(p, t, empty ? ActivityKind.Walk : ActivityKind.Carry, stash, pond, empty ? Prop.None : Prop.Basket, true, plan.resolveHour, plan.carryEnd);
                else if (empty) return false;
                else
                    a = EggAct(p, t, ActivityKind.TipLoad, stash, stash, Prop.Basket, false, plan.carryEnd, plan.end);
                return true;
            }
            return false;
        }

        PersonActivity EggAct(Person p, ViewTime t, ActivityKind kind, Place place, Place from, Prop prop, bool moving, float start, float end)
            => new PersonActivity
            {
                who = p.who, kind = kind, place = place, from = from, moving = moving && from != place, prop = prop, start = start, end = end,
                progress = Math.Max(0f, Math.Min(1f, (t.hour - start) / Math.Max(1e-4f, end - start))), pace = 1f, chronicleId = -1,
            };
    }
}
