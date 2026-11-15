using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#710: one visible-animal budget; every drawn cap is read from <see cref="AnimalBudget"/>.</summary>
    public class AnimalBudgetTests
    {
        [Test]
        public void TodaysLand_DrawsAtMost24()
        {
            Assert.AreEqual(26.4f, AnimalBudget.LandArea, 1e-3f);
            Assert.LessOrEqual(AnimalBudget.Total(), 24);
            Assert.AreEqual(24, AnimalBudget.Total());
        }

        [Test]
        public void DeeperBox_KeepsTheDensity()
        {
            Assert.AreEqual(33, AnimalBudget.Total(36f), "6 x 6 draws about 34");
            Assert.AreEqual(AnimalBudget.BaseTotal, AnimalBudget.Total(AnimalBudget.BaseArea));
            Assert.AreEqual(8, AnimalBudget.Cap(AnimalBudget.Kind.Rabbits, 36f));
        }

        [Test]
        public void EveryDrawnCap_IsReadFromTheTable()
        {
            Assert.AreEqual(AnimalBudget.Rabbits, RabbitHerd.DefaultCap);
            Assert.AreEqual(AnimalBudget.Deer, DeerHerd.DefaultCap);
            Assert.AreEqual(AnimalBudget.Ducks, SimCore.DucksShownCap);
            Assert.AreEqual(AnimalBudget.Fish, SimCore.FishShownCap);
            Assert.AreEqual(AnimalBudget.Cap(AnimalBudget.Kind.Rabbits), AnimalBudget.Rabbits);
            Assert.AreEqual(6, AnimalBudget.Rabbits); Assert.AreEqual(4, AnimalBudget.Deer); Assert.AreEqual(4, AnimalBudget.Ducks);
            Assert.AreEqual(4, AnimalBudget.Fish); Assert.AreEqual(3, AnimalBudget.Frogs); Assert.AreEqual(3, AnimalBudget.Wolves);
            Assert.AreEqual(AnimalBudget.Rabbits, RabbitHerd.Shown(50f));
            Assert.AreEqual(AnimalBudget.Deer, DeerHerd.Shown(50));
            Assert.AreEqual(3, RabbitHerd.Shown(50f, 3), "an explicit cap still wins");
        }
    }
}
