using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#197: run-ins on the way. Two walkers who pass close stop for a short beat (friends chat, rivals bicker,
    /// strangers wave): rare, capped per person per day, deterministic, and the same Chat / Argue / Wave activities the
    /// view already draws.</summary>
    public class LifeRunInTests
    {
        static SimCore TownWith(int seed, int minPeople, Era era = Era.Town)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 160 * 12 && (sim.State.era < era || sim.State.population < minPeople); m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            for (int m = 0; m < 6; m++) sim.Step();
            return sim;
        }

        /// <summary>Every beat of a day as (person key, start, kind, partner key), found by sampling the plan every 0.03 h.</summary>
        static List<(int who, float start, ActivityKind kind, int with)> BeatsOf(LifePlanner planner, long day, List<PersonActivity> acts)
        {
            var found = new Dictionary<(int, int), (float, ActivityKind, int)>();
            for (float h = 4f; h < 24f; h += 0.03f)
            {
                planner.PlanAll(new ViewTime(day, h), acts);
                foreach (var a in acts)
                {
                    if (!a.enRoute) continue;
                    Assert.IsFalse(a.moving, "stopped for the beat");
                    Assert.IsTrue(a.kind is ActivityKind.Chat or ActivityKind.Argue or ActivityKind.Wave, $"{a.kind} is a beat the view already draws");
                    Assert.IsFalse(a.partner.IsNone, "a beat has a partner");
                    var key = (a.who.Key, (int)Math.Round(a.start * 100f));
                    if (!found.ContainsKey(key)) found[key] = (a.start, a.kind, a.partner.Key);
                }
            }
            return found.Select(kv => (kv.Key.Item1, kv.Value.Item1, kv.Value.Item2, kv.Value.Item3)).ToList();
        }

        [TestCase(Era.Town, 1, 20)]
        [TestCase(Era.Town, 7, 20)]
        [TestCase(Era.Village, 3, 10)]
        public void RunInsAreRareCappedAndMutual(Era era, int seed, int minPeople)
        {
            var sim = TownWith(seed, minPeople, era);
            var planner = LifePlanner.Wired(sim);
            planner.ForcedWeather = Weather.Clear;
            var acts = new List<PersonActivity>();
            int beats = 0, personDays = 0, days = 0, chats = 0, bickers = 0, waves = 0, maxPer = 0;
            for (int d = 0; d < 40 && days < 14; d++)
            {
                sim.Step();
                long day = sim.State.month;
                var b = BeatsOf(planner, day, acts);
                days++;
                personDays += planner.People.Count;
                beats += b.Count;
                foreach (var g in b.GroupBy(x => x.who)) maxPer = Math.Max(maxPer, g.Count());
                chats += b.Count(x => x.kind == ActivityKind.Chat);
                bickers += b.Count(x => x.kind == ActivityKind.Argue);
                waves += b.Count(x => x.kind == ActivityKind.Wave);
                // mutual: if a is stopped with b at t, b is stopped with a at t
                foreach (var x in b)
                    Assert.IsTrue(b.Any(y => y.who == x.with && y.with == x.who && Math.Abs(y.start - x.start) < 0.01f), $"day {day}: {x.who} and {x.with} stop together");
            }
            float perPersonDay = beats / (float)personDays;
            TestContext.WriteLine($"{era} seed {seed} pop {planner.People.Count}: {days} days, {beats} person-beats = {perPersonDay:0.000} per person per day ({beats / (float)days:0.0} a day for the box): chats {chats}, bickers {bickers}, waves {waves}; most for one person in a day {maxPer}");
            Assert.LessOrEqual(maxPer, LifePlanner.RunInsPerDay, "at most two a day per person");
            Assert.LessOrEqual(perPersonDay, 0.35f, "rare enough to stay special");
        }

        [Test]
        public void RunInsAreTheSameEveryTimeFromTheSameSeed()
        {
            var sim = TownWith(7, 20);
            var a = LifePlanner.Wired(sim);
            var b = LifePlanner.Wired(sim);
            var xs = new List<PersonActivity>();
            var ys = new List<PersonActivity>();
            long day = sim.State.month + 1;
            sim.Step();
            day = sim.State.month;
            var ba = BeatsOf(a, day, xs).OrderBy(t => t.who).ThenBy(t => t.start).ToList();
            var bb = BeatsOf(b, day, ys).OrderBy(t => t.who).ThenBy(t => t.start).ToList();
            CollectionAssert.AreEqual(ba, bb);
            Assert.AreEqual(a.RunInsToday, b.RunInsToday);
        }

        [Test]
        public void ReplanningWithRunInsStaysInTheFrameBudget()
        {
            var sim = TownWith(7, 20);
            var planner = LifePlanner.Wired(sim);
            var acts = new List<PersonActivity>();
            planner.PlanAll(new ViewTime(sim.State.month, 9f), acts); // warm the day
            var sw = Stopwatch.StartNew();
            const int n = 400;
            for (int i = 0; i < n; i++) planner.PlanAll(new ViewTime(sim.State.month, 7f + i * 0.02f), acts);
            double perFrameMs = sw.Elapsed.TotalMilliseconds / n;
            sw.Restart();
            var fresh = LifePlanner.Wired(sim);
            fresh.PlanAll(new ViewTime(sim.State.month, 9f), acts); // a whole day for everyone, run-ins included
            double coldMs = sw.Elapsed.TotalMilliseconds;
            TestContext.WriteLine($"pop {acts.Count}: warm PlanAll {perFrameMs:0.000} ms a frame, cold day build with run-ins {coldMs:0.0} ms, {fresh.RunInsToday} run-ins");
            Assert.Less(perFrameMs, 2.0, "a warm replan is well under a 16 ms frame");
        }
    }
}
