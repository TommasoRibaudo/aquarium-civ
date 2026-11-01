// #148: the sim's crowd card fed by the real planner. Lives here, not in Assets/Game/Tests: AquariumCiv.Sim.Tests can't see
// the Life assembly, and this checks the two ends phase 2 will join (PersonCardView passes the planner's strings to the sim).
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    public class CrowdCardLifeTests
    {
        [Test]
        public void EveryCrowdDollGetsACardFromThePlanner()
        {
            var sim = SimCoreTests.NewSim(4);
            for (int y = 0; y < 40; y++) { sim.Advance(1); if (sim.State.eraReady) sim.AdvanceEra(); }
            var planner = new LifePlanner(sim);
            var crowd = planner.People.Where(p => p.IsCrowd).ToList();
            Assert.AreEqual(sim.CrowdCount, crowd.Count, "the sim and the planner count the same crowd");
            Assert.Greater(crowd.Count, 2);

            foreach (float hour in new[] { 3f, 10f, 15f, 20f })
            {
                var at = new ViewTime(7, hour);
                foreach (var who in crowd)
                {
                    var c = sim.CrowdCardOf(who.crowdIndex, planner.JobOf(who), planner.StageOf(who).ToString(), planner.Describe(planner.Plan(who, at)));
                    Assert.IsNotNull(c, who.ToString());
                    StringAssert.StartsWith(c.name + " is ", c.doing);
                    Assert.IsFalse(string.IsNullOrEmpty(c.jobLine), who.ToString());
                    StringAssert.DoesNotContain("One of the people", c.doing);
                    StringAssert.DoesNotContain("An old one", c.doing);
                    StringAssert.DoesNotContain("A child", c.doing);
                    if (planner.StageOf(who) == LifeStage.Kid) Assert.AreEqual("child", c.jobLine);
                }
            }
        }
    }
}
