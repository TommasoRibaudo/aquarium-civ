using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#208: around 06:30 about 7 people clumped in the lane between two homes that face each other, because most
    /// people started their home chores together. Each person now starts a short chore at their own time, spread between
    /// waking and the old end; the door still holds two. (The planner runs on abstract places here, so the knot is counted
    /// as everyone out of doors at their homes at one moment, the lane's crowd.)</summary>
    public class DawnStaggerTests
    {
        static SimCore SimIn(Era era, int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            Assert.AreEqual(era, sim.State.era);
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        static bool OutAtHome(PersonActivity a) =>
            !a.moving && a.place.kind == PlaceKind.Home && a.kind != ActivityKind.Sleep && a.kind != ActivityKind.Indoors && a.source != ActivitySource.Scene;

        /// <summary>Over 30 days, every 5 minutes from 05:30 to 08:00: the most people out at their homes at one moment, and the
        /// mean over working days of each day's worst moment (x100). Also fills <paramref name="doorMax"/> with the most at one
        /// home's door-type spot at a moment.</summary>
        static (int worst, int meanWorst100) Measure(Era era, int seed, out int doorMax)
        {
            var sim = SimIn(era, seed);
            var planner = new LifePlanner(sim);
            var acts = new List<PersonActivity>();
            int worst = 0, sum = 0, days = 0;
            doorMax = 0;
            for (long day = sim.State.month; day < sim.State.month + 30; day++)
            {
                if (planner.IsRestDay(day)) continue;
                int dayWorst = 0;
                for (float h = 5.5f; h < 8f; h += 1f / 12f)
                {
                    planner.PlanAll(new ViewTime(day, h), acts);
                    var atDoor = new Dictionary<Place, int>();
                    int n = 0;
                    foreach (var a in acts)
                    {
                        if (!OutAtHome(a)) continue;
                        n++;
                        if (SlotSpots.SpotFor(a.kind) == SlotSpots.HomeSpot.Door) { atDoor.TryGetValue(a.place, out int c); atDoor[a.place] = ++c; }
                    }
                    foreach (var c in atDoor.Values) doorMax = Math.Max(doorMax, Math.Min(c, SlotSpots.AtDoor)); // the view seats rank >= 2 out in front
                    dayWorst = Math.Max(dayWorst, n);
                }
                worst = Math.Max(worst, dayWorst); sum += dayWorst; days++;
            }
            return (worst, sum * 100 / Math.Max(1, days));
        }

        // Before the change (seed 7, the same 30 days): Village worst 16, mean 1328; Town worst 22, mean 1828.
        [TestCase(Era.Village, 7, 16, 1328, 12, 1050)]
        [TestCase(Era.Town, 7, 22, 1828, 18, 1450)]
        public void TheDawnKnotIsSmallerThanBeforeTheStagger(Era era, int seed, int before, int beforeMean, int maxWorst, int maxMean)
        {
            var m = Measure(era, seed, out _);
            TestContext.WriteLine($"DAWN {era} {seed}: worst {m.worst} (before {before}), mean of each day's worst x100 {m.meanWorst100} (before {beforeMean})");
            Assert.LessOrEqual(m.worst, maxWorst);
            Assert.LessOrEqual(m.meanWorst100, maxMean);
        }

        [TestCase(Era.Village, 7)]
        [TestCase(Era.Town, 7)]
        public void TheDoorStillHoldsTwo(Era era, int seed)
        {
            Assert.AreEqual(2, SlotSpots.AtDoor);
            for (int rank = 0; rank < 8; rank++)
            {
                var (dx, dz) = SlotSpots.Home(SlotSpots.HomeSpot.Door, rank, 0.7f);
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (rank < 2) Assert.Less(d, SlotSpots.AtDoorReach, $"rank {rank} at the door"); else Assert.Greater(d, SlotSpots.AtDoorReach, $"rank {rank} out in front");
            }
            Measure(era, seed, out int doorMax);
            Assert.LessOrEqual(doorMax, 2);
        }
    }
}
