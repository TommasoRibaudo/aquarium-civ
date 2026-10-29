using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Stories (Gate 2's chronicle cards and time-lapse) and shaping the land (Gate 3).</summary>
    public class StoryAndNudgeTests
    {
        static SimCore Grown(int seed)
        {
            var sim = NewSim(seed);
            sim.Advance(20);
            return sim;
        }

        [Test]
        public void StoriesAreDeterministicAndCatchUpEqualsLive()
        {
            var a = NewSim(1234); var b = NewSim(1234);
            a.Advance(40);
            for (int i = 0; i < 40 * 12; i++) b.Advance(1.0 / 12);
            Assert.AreEqual(string.Join("\n", a.State.chronicle.Select(e => e.text)), string.Join("\n", b.State.chronicle.Select(e => e.text)));
        }

        [Test]
        public void StoriesNeverShiftTheEconomyDice()
        {
            var a = NewSim(99); var b = NewSim(99);
            b.ForceStory(); b.ForceStory();
            a.Advance(30); b.Advance(30);
            Assert.AreEqual(a.State.econRng.state, b.State.econRng.state);
        }

        [Test]
        public void AnEightHourAbsenceHasEnoughStoriesForCards()
        {
            // The return shows 3–7 cards: after a 20-year prehistory, 8 h away should usually produce at least 3.
            int enough = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                var sim = Grown(seed);
                long before = sim.State.month;
                sim.Advance(8);
                if (sim.State.chronicle.Count(e => e.month > before) >= 3) enough++;
            }
            Assert.GreaterOrEqual(enough, 24, $"only {enough}/30 seeds had 3+ stories in 8 h");
        }

        [Test]
        public void StoriesLinkNamedPeople()
        {
            var sim = NewSim(7);
            sim.Advance(120);
            var s = sim.State;
            var kinds = s.chronicle.Select(e => e.kind).Distinct().ToList();
            TestContext.WriteLine($"{s.chronicle.Count} entries, {kinds.Count} kinds: {string.Join(", ", kinds)}");
            Assert.GreaterOrEqual(kinds.Count, 15, "a century should show most of the template variety");
            Assert.IsTrue(s.notables.Any(n => n.partnerId >= 0), "someone married");
            Assert.IsFalse(string.IsNullOrEmpty(s.settlementName), "the settlement got a name");
            foreach (var e in s.chronicle.Where(e => e.otherNotableId >= 0))
                Assert.IsNotNull(sim.FindNotable(e.otherNotableId), $"{e.kind} links a real person");
            foreach (var n in s.notables.Where(n => n.partnerId >= 0))
                Assert.AreEqual(n.id, sim.FindNotable(n.partnerId).partnerId, "marriage is mutual");
        }

        [Test]
        public void NoTwoLivingPeopleShareAName()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = NewSim(seed);
                for (int y = 0; y < 60; y++)
                {
                    sim.Advance(1);
                    var alive = sim.State.notables.Where(n => n.alive).Select(n => n.FullName).ToList();
                    Assert.AreEqual(alive.Count, alive.Distinct().Count(), $"seed {seed} year {sim.State.Year}: {string.Join(", ", alive)}");
                }
            }
        }

        [Test]
        public void YearSnapshotsCoverTheHistory()
        {
            var sim = NewSim(1234);
            sim.Advance(100);
            var years = sim.State.years;
            Assert.AreEqual(0, years[0].year);
            Assert.AreEqual(100, years[^1].year);
            Assert.AreEqual(101, years.Count);
            Assert.AreEqual(sim.State.population, years[^1].population);
            for (int i = 1; i < years.Count; i++) Assert.GreaterOrEqual(years[i].buildings, years[i - 1].buildings, "buildings are never lost");
        }

        [TestCase(SimCore.NudgeKind.Rain, BuildingType.Farm)]
        [TestCase(SimCore.NudgeKind.Grove, null)]
        [TestCase(SimCore.NudgeKind.Channel, null)]
        public void ThePeopleAnswerWithinThreeYearsAndTheKeeperIsCredited(string kind, string expectBuilding)
        {
            int answered = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = Grown(seed);
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
        public void RainLeansTheVillageTowardFields()
        {
            int farmsWith = 0, farmsWithout = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var with = Grown(seed); var without = Grown(seed);
                with.PlaceNudge(SimCore.NudgeKind.Rain, 2);
                with.Advance(3); without.Advance(3);
                farmsWith += with.Count(BuildingType.Farm);
                farmsWithout += without.Count(BuildingType.Farm);
            }
            Assert.Greater(farmsWith, farmsWithout, "rain means more fields");
        }

        [Test]
        public void CooldownIsThreeYears()
        {
            var sim = Grown(1234);
            sim.PlaceNudge(SimCore.NudgeKind.Rain, 1);
            Assert.AreEqual(SimCore.NudgeMonths, sim.NudgeCooldown(SimCore.NudgeKind.Rain));
            Assert.AreEqual(0, sim.NudgeCooldown(SimCore.NudgeKind.Grove));
            sim.Advance(3);
            Assert.AreEqual(0, sim.NudgeCooldown(SimCore.NudgeKind.Rain));
        }
    }
}
