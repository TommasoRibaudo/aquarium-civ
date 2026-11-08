using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #420: one deer hunt at the plan level (placed-life spec §5.3; research 18 §5.3): which view day, who goes (two or three, the
    /// hunters first), when they leave, when the shot is and whether it hits. The scene itself (the line of hunters, the crouch at the
    /// wood's edge, the freeze, the arrow, the pole carry, the bigger fire) is #426, which lays its steps over this plan. The sim is
    /// settled once, at <see cref="resolveHour"/>, by <see cref="LifePlanner.ResolveDeerHunts"/>: a hit takes one deer above the floor
    /// (<see cref="SimCore.DeerHuntCatch"/>), else it is a miss. Hashed, so the view and the sim agree.
    /// </summary>
    public sealed class DeerHuntPlan
    {
        public long day;
        /// <summary>Position in the day's list, in the order the hunts settle.</summary>
        public int index;
        /// <summary>The hunters, the leader first (two or three).</summary>
        public readonly List<PersonRef> hunters = new List<PersonRef>();
        /// <summary>The arrow finds its mark (planned; a hit is refused at the floor or in a spring the people have ruled on).</summary>
        public bool hits;
        /// <summary>Settled in the sim; and whether a deer really was taken, and whether it was a stag (the antlers).</summary>
        public bool resolved, caught, stag;
        public bool forced, debugStarted;
        /// <summary>View hours: they leave the fire, the shot, they are home with the carry.</summary>
        public float start, resolveHour, end;
        public double ResolveAbsolute => day * 24.0 + resolveHour;
    }

    /// <summary>#420: the deer hunt as planner data (see <see cref="DeerHuntPlan"/>).</summary>
    public sealed partial class LifePlanner
    {
        // ---------------------------------------------------------------- tuning [est, #434]
        /// <summary>The share of working days the hunters go after the deer (a deer feeds the camp for days: it is not a daily job).</summary>
        public const float DeerHuntDayChance = 0.2f;
        /// <summary>How often the arrow finds its mark.</summary>
        public const float DeerHitChance = 0.5f;
        /// <summary>Two or three go; a hunt needs two.</summary>
        public const int DeerHuntersMin = 2, DeerHuntersMax = 3;
        /// <summary>They leave between these view hours (after the dawn drink: nobody hunts at the water, #427).</summary>
        public const float DeerHuntFrom = 7.2f, DeerHuntSpread = 1.6f;
        /// <summary>View hours from leaving to the shot, and to being home with the carry.</summary>
        public const float DeerShotAfter = 2.2f, DeerHuntHours = 4.5f;

        readonly Dictionary<long, List<DeerHuntPlan>> deerDays = new Dictionary<long, List<DeerHuntPlan>>();
        static readonly List<DeerHuntPlan> NoDeerHunts = new List<DeerHuntPlan>();
        bool? deerForceNext;

        /// <summary>The deer hunts of a view day (at most one), in the order they settle. Empty unless the ecology is on, the era is the
        /// camp's, there are deer and the herd has one above its floor (and it isn't a spring the people have ruled on). Made once per
        /// day and kept.</summary>
        public IReadOnlyList<DeerHuntPlan> DeerHuntsOn(long day)
        {
            Sync();
            return DeerHuntsOf(day);
        }

        List<DeerHuntPlan> DeerHuntsOf(long day)
        {
            if (sim == null || !sim.EcologyOn || sim.State.era != Era.Camp || sim.EcoDeerCount == 0) return NoDeerHunts;
            if (deerDays.TryGetValue(day, out var list)) return list;
            EnsureDay(day);
            List<long> drop = null;
            foreach (var k in deerDays.Keys) if (k < day - 2) (drop ??= new List<long>()).Add(k);
            if (drop != null) foreach (var k in drop) deerDays.Remove(k);
            list = new List<DeerHuntPlan>();
            deerDays[day] = list;
            if (LifeClock.IsRestDay(day) || dayInfo.weather == Weather.Storm || sim.DeerHuntSurplus <= 0) return list;
            if (R(0, day, 400) >= DeerHuntDayChance) return list;
            var plan = MakeDeerHunt(day, DeerHuntFrom + DeerHuntSpread * R(0, day, 401), R(0, day, 402) < DeerHitChance);
            if (plan != null) { list.Add(plan); ReindexDeer(list); }
            return list;
        }

        /// <summary>The hunters (those with the hunter's job first, then any grown-up who is not the toolmaker or minding a baby,
        /// ranked by a hash of the day) and the plan's hours. Null when fewer than two can go.</summary>
        DeerHuntPlan MakeDeerHunt(long day, float start, bool hits)
        {
            BuildCarers();
            var pool = new List<(int rank, float roll, Person p)>();
            foreach (var p in people)
            {
                if (!p.Adult || p.job == JobType.Toolmaker || carerBabies.ContainsKey(p.key)) continue;
                pool.Add((p.job == JobType.Hunter ? 0 : 1, R(p.key, day, 410), p));
            }
            pool.Sort((a, b) => { int c = a.rank.CompareTo(b.rank); if (c != 0) return c; c = a.roll.CompareTo(b.roll); return c != 0 ? c : a.p.key.CompareTo(b.p.key); });
            int want = R(0, day, 411) < 0.5f ? DeerHuntersMin : DeerHuntersMax;
            if (pool.Count < DeerHuntersMin) return null;
            var plan = new DeerHuntPlan { day = day, start = start, resolveHour = start + DeerShotAfter, end = start + DeerHuntHours, hits = hits };
            for (int i = 0; i < pool.Count && plan.hunters.Count < want; i++) plan.hunters.Add(pool[i].p.who);
            if (deerForceNext.HasValue) { plan.hits = deerForceNext.Value; plan.forced = true; deerForceNext = null; }
            return plan;
        }

        static void ReindexDeer(List<DeerHuntPlan> list)
        {
            list.Sort((a, b) => a.ResolveAbsolute.CompareTo(b.ResolveAbsolute));
            for (int i = 0; i < list.Count; i++) list[i].index = i;
        }

        /// <summary>Settle every deer hunt whose shot is over at <paramref name="now"/> (the view calls this every frame, as
        /// <see cref="ResolveHunts"/>): each once, and not twice across a reload (the sim's mark is saved). A planned hit takes one deer
        /// (<see cref="SimCore.DeerHuntCatch"/>): two hides dry by the fire, the first stag's antlers go up; at the floor, or in a spring
        /// the people have ruled on, it turns into a miss. Returns how many settled now.</summary>
        public int ResolveDeerHunts(ViewTime now, List<DeerHuntPlan> settled = null)
        {
            Sync();
            if (sim == null || !sim.EcologyOn) return 0;
            int count = 0;
            var days = new List<long>(deerDays.Keys);
            days.Sort();
            foreach (long day in days)
            {
                var list = deerDays[day];
                for (int i = 0; i < list.Count; i++)
                {
                    var plan = list[i];
                    if (plan.resolved) continue;
                    if (plan.ResolveAbsolute > now.Absolute) break;
                    plan.resolved = true;
                    if (plan.index < sim.DeerHuntsSettled(day)) continue; // settled before a reload
                    if (plan.hits && sim.DeerHuntCatch(out bool stag)) { plan.caught = true; plan.stag = stag; }
                    sim.DeerHuntSettled(day);
                    settled?.Add(plan);
                    count++;
                }
            }
            return count;
        }

        /// <summary>Debug ("deer hunt now (hit)" / "(miss)", #426): two or three go after the deer from now; <paramref name="hits"/>
        /// fixes the ending (null: hashed). A hit is still refused at the floor. False when the ecology is off, there are no deer, or
        /// fewer than two can go.</summary>
        public bool DebugStartDeerHunt(ViewTime now, bool? hits, out DeerHuntPlan plan)
        {
            plan = null;
            Sync();
            if (sim == null || !sim.EcologyOn || sim.EcoDeerCount == 0) return false;
            EnsureDay(now.day);
            var list = DeerHuntsOf(now.day);
            if (ReferenceEquals(list, NoDeerHunts)) { list = new List<DeerHuntPlan>(); deerDays[now.day] = list; }
            deerForceNext = hits;
            plan = MakeDeerHunt(now.day, now.hour + 0.02f, R(0, now.day, 412) < DeerHitChance);
            deerForceNext = null;
            if (plan == null) return false;
            plan.debugStarted = true;
            list.Add(plan);
            ReindexDeer(list);
            return true;
        }
    }
}
