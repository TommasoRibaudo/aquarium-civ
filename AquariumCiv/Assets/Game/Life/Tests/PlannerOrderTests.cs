using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#117: the planner's and settlement's answers are a pure function of the sim state. The same state gives the same
    /// plan whichever person, day, weather or place is asked first, whether the planner is fresh or well used, and whether the
    /// settlement was built as the box lived or rebuilt from the save.</summary>
    public class PlannerOrderTests
    {
        static readonly Weather[] Weathers = { Weather.Storm, Weather.Snow, Weather.Rainbow, Weather.KeeperRain, Weather.Fog };

        static void Month(SimCore sim)
        {
            sim.Step();
            if (sim.State.eraReady) sim.AdvanceEra();
        }

        /// <summary>The whole plan of a week plus one day per weather: per person and day, every segment and the walk's length.</summary>
        static Dictionary<string, string> FullPlan(SimCore sim, bool reverse, bool freshPerQuery, LifePlanner shared = null)
        {
            var result = new Dictionary<string, string>();
            long d0 = sim.State.month;
            var days = new List<(long day, Weather? w)>();
            for (long d = d0; d < d0 + 7; d++) days.Add((d, null));
            for (int w = 0; w < Weathers.Length; w++) days.Add((d0 + 7 + w, Weathers[w]));
            if (reverse) days.Reverse();
            var planner = shared ?? LifePlanner.Wired(sim);
            var segs = new List<PersonActivity>();
            var route = new List<Waypoint>();
            var keys = planner.People.ToList();
            if (reverse) keys.Reverse();
            foreach (var (day, wx) in days)
                foreach (var who in keys)
                {
                    var p = freshPerQuery ? LifePlanner.Wired(sim) : planner;
                    p.ForcedWeather = wx;
                    p.DaySchedule(who, day, segs);
                    var sb = new StringBuilder();
                    foreach (var a in segs)
                    {
                        sb.Append(a.kind).Append('@').Append(a.place).Append('<').Append(a.from).Append('/')
                          .Append(a.start.ToString("0.000", CultureInfo.InvariantCulture)).Append('-').Append(a.end.ToString("0.000", CultureInfo.InvariantCulture));
                        if (a.moving && a.from != a.place && p.RouteOf(a.from, a.place, route)) sb.Append('#').Append(route.Count);
                        sb.Append(';');
                    }
                    result[$"{who.Key} day {day - d0}"] = sb.ToString();
                }
            return result;
        }

        static string Diff(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            var bad = a.Keys.Where(k => !b.TryGetValue(k, out var v) || v != a[k]).Take(3).ToList();
            return bad.Count == 0 ? null : string.Join("\n", bad.Select(k => $"{k}:\n  {a[k]}\n  {(b.TryGetValue(k, out var v) ? v : "<none>")}"));
        }

        /// <summary>A box saved and reloaded mid-life (the settlement rebuilt from the save, its lazy caches cold) lives on exactly
        /// like one that never stopped: same state, same plans.</summary>
        [TestCase(1, 12)]
        public void AReloadedBoxLivesAndPlansLikeOneThatNeverStopped(int from, int count)
        {
            foreach (int seed in Enumerable.Range(from, count))
            {
                var a = new SimCore(new SimConfig(), SimCore.NewWorld(seed, new SimConfig()));
                var b = new SimCore(new SimConfig(), SimCore.NewWorld(seed, new SimConfig()));
                for (int m = 0; m < 40 * 12; m++) { Month(a); Month(b); }
                _ = b.Settlement.Lots.Count; // grown as it lived
                b = new SimCore(b.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(b.State)));
                for (int m = 0; m < 60 * 12; m++) { Month(a); Month(b); }
                Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b.State), $"seed {seed}: state after a reload");
                for (int m = 0; m < 12; m++) { a.Step(); b.Step(); }
                Assert.IsNull(Diff(FullPlan(a, false, false), FullPlan(b, true, true)), $"seed {seed}: plans after a reload");
            }
        }

        /// <summary>A planner that has planned and routed through earlier months (its caches warm) answers like a fresh one.</summary>
        [TestCase(1, 10)]
        public void AUsedPlannerAnswersLikeAFreshOne(int from, int count)
        {
            foreach (int seed in Enumerable.Range(from, count))
            {
                var sim = new SimCore(new SimConfig(), SimCore.NewWorld(seed, new SimConfig()));
                LifePlanner used = null;
                foreach (var era in new[] { Era.Camp, Era.Village, Era.Town })
                {
                    for (int m = 0; m < 120 * 12 && sim.State.era < era; m++)
                    {
                        Month(sim);
                        if (m % 7 == 0) { used ??= LifePlanner.Wired(sim); FullPlan(sim, m % 2 == 0, false, used); }
                    }
                    if (sim.State.era != era) continue;
                    for (int round = 0; round < 3; round++)
                    {
                        for (int m = 0; m < 5; m++) sim.Step();
                        used ??= LifePlanner.Wired(sim);
                        Assert.IsNull(Diff(FullPlan(sim, false, false), FullPlan(sim, true, false, used)), $"seed {seed} {era} round {round}");
                    }
                }
            }
        }

        [TestCase(1, 25)]
        [TestCase(1, 200, Explicit = true, Reason = "#117: the 200-seed order sweep, about 3 minutes")]
        public void TheSamePlanWhateverIsAskedFirst(int from, int count)
        {
            int diffs = 0;
            foreach (int seed in Enumerable.Range(from, count))
            {
                var sim = new SimCore(new SimConfig(), SimCore.NewWorld(seed, new SimConfig()));
                foreach (var era in new[] { Era.Camp, Era.Village, Era.Town })
                {
                    for (int m = 0; m < 120 * 12 && sim.State.era < era; m++) Month(sim);
                    if (sim.State.era != era) continue;
                    for (int m = 0; m < 24; m++) sim.Step();
                    var basePlan = FullPlan(sim, false, false);
                    string d1 = Diff(basePlan, FullPlan(sim, true, false));
                    string d2 = Diff(basePlan, FullPlan(sim, false, true));
                    string d3 = Diff(basePlan, FullPlan(sim, false, false));
                    var copy = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
                    string d4 = Diff(basePlan, FullPlan(copy, false, false));
                    foreach (var (n, d) in new[] { ("reverse", d1), ("fresh", d2), ("again", d3), ("reloaded", d4) })
                        if (d != null) { diffs++; TestContext.WriteLine($"seed {seed} {era} {n}:\n{d}"); }
                }
            }
            Assert.AreEqual(0, diffs);
        }
    }
}
