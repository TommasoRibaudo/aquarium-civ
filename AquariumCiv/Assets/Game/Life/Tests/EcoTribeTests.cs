using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>P8, the tribe before the camp (keepers-hands §2.1, #246): on a barren box nobody hunts, fishes or chops; no new tent
    /// goes up; once the camp gate opens the camp's own jobs come back. #656 (the user, 2026-11-14): this used to pin the root dig in
    /// the dry hollow; the root dig is gone (they live off the carried basket), so it now pins that nobody digs.</summary>
    public class EcoTribeTests
    {
        static SimCore Barren(int seed)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static List<PersonActivity> Work(SimCore sim, int days = 8)
        {
            var planner = LifePlanner.Wired(sim);
            var all = new List<PersonActivity>();
            var day = new List<PersonActivity>();
            for (int i = 0; i < days; i++)
                foreach (var who in planner.People)
                {
                    planner.DaySchedule(who, i * 7, day);
                    all.AddRange(day.Where(a => a.Group == ActivityGroup.Work));
                }
            return all;
        }

        [TestCase(1)]
        [TestCase(7)]
        public void BeforeTheCampNobodyDigsRoots(int seed)
        {
            var sim = Barren(seed);
            for (int m = 0; m < 24; m++) sim.Step(); // two dead years, no Keeper
            Assert.IsTrue(sim.BeforeCamp, "still before the camp");
            int lots = sim.State.buildings.Count;
            var work = Work(sim);
            Assert.IsNotEmpty(work, "they still work");
            var kinds = work.Select(a => a.kind).Distinct().ToList();
            TestContext.WriteLine("work: " + string.Join(", ", kinds));
            Assert.IsFalse(work.Any(a => a.kind == ActivityKind.Gather && a.place.kind == PlaceKind.Stream), "#656: no root dig in the stream bed");
            Assert.IsFalse(work.Any(a => a.kind == ActivityKind.Stalk || a.kind == ActivityKind.Fish || a.kind == ActivityKind.Chop),
                "no hunting, fishing or chopping on a dead land");
            Assert.AreEqual(lots, sim.State.buildings.Count);

            sim.DebugOpenCampGate();
            Assert.IsFalse(sim.BeforeCamp);
            // #285: the camp's own jobs come back as the land offers them, not with the gate alone
            foreach (var key in new[] { EcoMilestones.FirstBush, EcoMilestones.WaterSurfaced, EcoMilestones.FirstWoodTree })
            { sim.State.eco.milestoneKeys.Add(key); sim.State.eco.milestoneMonths.Add(sim.State.month); }
            sim.DebugWildTrees(false); // #573: the wood job needs a real grown tree away from the Keeper's tree, not only the milestone
            sim.DebugRedrawJobs();
            Assert.IsFalse(Work(sim).All(a => a.place.kind == PlaceKind.Stream || a.place.kind == PlaceKind.Fire || a.place == CampStash.Stand),
                "once the land offers it the camp's own jobs come back");
        }
    }
}
