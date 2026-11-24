using System;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;

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

        /// <summary>2026-11-16 Q7: the goat in the pond family's pen is outside the budget and every herd, cap, food and hunting list:
        /// a box with the pen counts and grows exactly as the same box without it.</summary>
        [Test]
        public void ThePensGoatTakesNoSlotAndChangesNoHerd()
        {
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(AnimalBudget.Kind)), "Goat", "no budget row for the goat");
            Assert.AreEqual(33, AnimalBudget.Total());
            Assert.AreEqual(AnimalBudget.BaseTotal, AnimalBudget.Total());
            var cfg = new SimConfig { ecology = true, dilemmas = true };
            var with = new SimCore(cfg, SimCore.NewWorld(5, cfg));
            var without = new SimCore(cfg, SimCore.NewWorld(5, cfg));
            for (int m = 0; m < 12; m++) { with.Step(); without.Step(); }
            with.State.consequenceProps.Add(new ConsequenceProp { id = 1, dilemmaId = 1, option = 0, kind = ConsequenceKind.GoatPen, anchor = PropAnchor.Fire, dueMonth = with.State.month });
            with.Step(); without.Step();
            Assert.IsTrue(with.PenGoat, "the pen stands with its goat");
            Assert.AreEqual(1, with.PenGoats);
            Assert.AreEqual(0, without.PenGoats);
            for (int m = 0; m < 36; m++)
            {
                with.Step(); without.Step();
                Assert.AreEqual(JsonUtility.ToJson(without.State.eco), JsonUtility.ToJson(with.State.eco), $"month {m}: the same land, herds and catches");
                Assert.AreEqual(without.State.food, with.State.food, 1e-4f, $"month {m}: the same food");
                Assert.AreEqual(without.State.notables.Count(n => n.alive), with.State.notables.Count(n => n.alive), $"month {m}: the same people");
            }
            Assert.AreEqual(33, AnimalBudget.Total(), "the budget is unchanged with the pen");
        }
    }
}
