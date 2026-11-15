using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#710: one visible-animal budget; every drawn cap is read from <see cref="AnimalBudget"/>.</summary>
    public class AnimalBudgetTests
    {
        [Test]
        public void TheDeeperLand_DrawsAbout33()
        {
            // #658: every box is 6 x 6 now (was 6 x 4.4 = 26.4 u2, 24 animals); the budget follows the land's area.
            Assert.AreEqual(36f, AnimalBudget.LandArea, 1e-3f);
            Assert.AreEqual(33, AnimalBudget.Total());
            Assert.AreEqual(AnimalBudget.BaseTotal, AnimalBudget.Total());
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
            Assert.AreEqual(8, AnimalBudget.Rabbits); Assert.AreEqual(6, AnimalBudget.Deer); Assert.AreEqual(6, AnimalBudget.Ducks);
            Assert.AreEqual(5, AnimalBudget.Fish); Assert.AreEqual(4, AnimalBudget.Frogs); Assert.AreEqual(4, AnimalBudget.Wolves);
            Assert.AreEqual(AnimalBudget.Rabbits, RabbitHerd.Shown(50f));
            Assert.AreEqual(AnimalBudget.Deer, DeerHerd.Shown(50));
            Assert.AreEqual(3, RabbitHerd.Shown(50f, 3), "an explicit cap still wins");
        }
    }
}
