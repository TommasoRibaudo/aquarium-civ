using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#689: a house's card says the year it really went up. The camp's tent becomes the village's house at the era
    /// change, and a house built later keeps its own year.</summary>
    public class BuildingYearTests
    {
        static SimCore ToVillage(int seed, out long villageMonth)
        {
            var sim = NewSim(seed);
            for (int y = 0; y < 400 && !sim.State.eraReady; y++) sim.Advance(1);
            Assert.IsTrue(sim.State.eraReady, "the box reaches the village");
            Assert.IsTrue(sim.AdvanceEra());
            Assert.AreEqual(Era.Village, sim.State.era);
            villageMonth = sim.State.eraStartMonth;
            return sim;
        }

        [Test]
        public void TheCampsTentReadsAsBuiltWhenItBecameAHouse()
        {
            var sim = ToVillage(3, out long vm);
            Assert.Greater(vm, 12, "the village came after the first year");
            var tent = sim.State.buildings.First(b => b.type == BuildingType.Hut && b.foundedMonth < vm);
            var c = sim.BuildingCardOf(tent.id);
            Assert.AreEqual("A house", c.title);
            Assert.AreEqual(SimCore.YearNo(vm), c.year);
            Assert.Greater(c.year, 1);
            Assert.AreEqual($"Built in year {c.year}.", c.yearLine);
        }

        [Test]
        public void AHouseBuiltInTheVillageKeepsItsOwnYear()
        {
            var sim = ToVillage(3, out long vm);
            for (int y = 0; y < 3; y++) sim.Advance(1);
            var b = sim.DebugBuild(BuildingType.Hut);
            Assert.GreaterOrEqual(b.foundedMonth, vm);
            Assert.AreEqual(SimCore.YearNo(b.foundedMonth), sim.BuildingCardOf(b.id).year);
            Assert.Greater(sim.BuildingCardOf(b.id).year, SimCore.YearNo(vm), "built a few years after the change");
        }

        [Test]
        public void OldSaveHutWithMonthZeroReadsAsTheVillageYearNotYearOne()
        {
            var sim = ToVillage(5, out long vm);
            var b = sim.DebugBuild(BuildingType.Hut);
            b.foundedMonth = 0; b.lotId = 0; // as an old save left it
            Assert.AreEqual(SimCore.YearNo(vm), sim.BuildingCardOf(b.id).year);
        }

        [Test]
        public void TheWellAndOtherThingsKeepTheirFoundingYear()
        {
            var sim = ToVillage(3, out long vm);
            foreach (var b in sim.State.buildings.Where(x => x.type != BuildingType.Hut && x.type != BuildingType.Farm && x.type != BuildingType.Lumbermill))
                Assert.AreEqual(SimCore.YearNo(b.foundedMonth), sim.BuildingCardOf(b.id).year, b.type);
        }

        [Test]
        public void TheCardChangeDoesNotMoveHistorysBuildings()
        {
            var sim = ToVillage(3, out long vm);
            sim.Advance(2);
            var tent = sim.State.buildings.First(b => b.type == BuildingType.Hut && b.foundedMonth < vm);
            var at = sim.BoxAt(SimCore.YearNo(tent.foundedMonth));
            Assert.IsNotNull(at);
            var rec = at.buildings.FirstOrDefault(x => x.id == tent.id);
            Assert.IsNotNull(rec, "the replay still shows the tent in its own year");
            Assert.Less(rec.foundedMonth, vm, "the replay keeps the lot's own month, not the card's");
        }
    }
}
