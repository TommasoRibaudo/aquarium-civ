using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#573: chopping reads as wood: logs come over the hits, and a log chopped is carried to the stash.</summary>
    public class ChopLogsTests
    {
        [Test]
        public void ChoppingMakesALogEveryFewHitsAndTheChopperLeavesWithThem()
        {
            var t = new ChopLogTally();
            int made = 0;
            for (int i = 1; i <= 12; i++)
            {
                if (t.Hit(7, 4)) made++;
                Assert.AreEqual(i / 4, t.Logs(7), "after " + i + " hits");
            }
            Assert.AreEqual(3, made);
            Assert.AreEqual(0, t.Logs(8), "somebody else's hits are their own");
            for (int i = 0; i < 40; i++) t.Hit(7, 4);
            Assert.AreEqual(ChopLogTally.MaxPerChopper, t.Logs(7), "no more than the chopper can carry in a trip");
            Assert.AreEqual(ChopLogTally.MaxPerChopper, t.Leave(7), "they carry the logs off");
            Assert.AreEqual(0, t.Logs(7));
            Assert.AreEqual(0, t.TotalLogs);
        }

        [Test]
        public void AChoppedLogIsCarriedToTheStash()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(2, cfg));
            for (int m = 0; m < 60 && !sim.State.eraReady; m++) sim.Step();
            Assert.AreEqual(Era.Camp, sim.State.era);
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge)) sim.DebugSetJob(n, JobType.Woodcutter);
            var planner = LifePlanner.Wired(sim);
            var all = new List<PersonActivity>();
            var day = new List<PersonActivity>();
            for (int d = 0; d < 20; d++)
                foreach (var who in planner.People) { planner.DaySchedule(who, d * 9, day); all.AddRange(day); }
            Assert.IsTrue(all.Any(a => a.kind == ActivityKind.Chop && a.place.kind == PlaceKind.Woods), "they chop at the woods");
            Assert.IsTrue(all.Any(a => a.moving && a.Group == ActivityGroup.Carry && a.prop == Prop.Log && a.place == CampStash.Stand), "and a log is carried to the stash");
        }
    }
}
