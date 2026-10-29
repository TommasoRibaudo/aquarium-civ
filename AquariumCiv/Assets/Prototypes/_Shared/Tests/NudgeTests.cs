using System.Linq;
using AquariumCiv.Prototypes.Sim;
using NUnit.Framework;

namespace AquariumCiv.Prototypes.Tests
{
    /// <summary>P5 nudges: the people answer within 1–3 game years, the chronicle credits the Keeper, nothing changes without one.</summary>
    public class NudgeTests
    {
        static SimCore World(int seed = 1234)
        {
            var cfg = new SimConfig { outputPenaltyEnabled = false, messAffectsMood = false, storyEvents = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            sim.Advance(20);
            return sim;
        }

        [TestCase("harvest_totem", "Farm")]
        [TestCase("rain", "Farm")]
        [TestCase("grove", null)]
        [TestCase("craft_totem", null)]
        [TestCase("faith_shrine", null)]
        [TestCase("channel", null)]
        public void ThePeopleAnswerWithinThreeYearsAndTheKeeperIsCredited(string kind, string expectBuilding)
        {
            int answered = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = World(seed);
                var n = sim.PlaceNudge(kind, 3);
                sim.Advance(3);
                if (!n.credited) continue;
                answered++;
                Assert.GreaterOrEqual(n.answerMonth - n.month, 8, "not instantly: the people take it in first");
                Assert.LessOrEqual(n.answerMonth - n.month, SimCore.NudgeMonths);
                var b = sim.State.buildings.First(x => x.id == n.buildingId);
                if (expectBuilding != null) Assert.AreEqual(expectBuilding, b.type);
                var credit = sim.State.chronicle.Last(e => e.keeper && e.buildingId == b.id);
                StringAssert.Contains("Keeper", credit.text);
            }
            Assert.GreaterOrEqual(answered, 18, $"{kind}: answered in {answered}/20 seeds");
        }

        [Test]
        public void NoNudgeNoChange()
        {
            // Two worlds, one with an expired-free empty nudge list: identical economies.
            var a = World(5); var b = World(5);
            b.State.nudges.Clear();
            a.Advance(10); b.Advance(10);
            Assert.AreEqual(a.State.population, b.State.population);
            Assert.AreEqual(a.State.buildings.Count, b.State.buildings.Count);
        }

        [Test]
        public void ANudgeLeansTheVillage()
        {
            int farmsWith = 0, farmsWithout = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var with = World(seed); var without = World(seed);
                with.PlaceNudge("harvest_totem", 2);
                with.Advance(3); without.Advance(3);
                farmsWith += with.Count(BuildingType.Farm);
                farmsWithout += without.Count(BuildingType.Farm);
            }
            Assert.Greater(farmsWith, farmsWithout, "a harvest totem means more fields");
        }

        [Test]
        public void CooldownIsThreeYears()
        {
            var sim = World();
            sim.PlaceNudge("rain", 1);
            Assert.AreEqual(SimCore.NudgeMonths, sim.NudgeCooldown("rain"));
            Assert.AreEqual(0, sim.NudgeCooldown("grove"));
            sim.Advance(3);
            Assert.AreEqual(0, sim.NudgeCooldown("rain"));
        }
    }
}
