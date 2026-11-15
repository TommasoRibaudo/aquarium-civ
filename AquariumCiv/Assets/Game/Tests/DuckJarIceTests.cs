using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#703: the duck jar is shut while the pond is frozen (the wild ducks are away in winter, #618), so a placing is refused and
    /// nothing is spent; it opens at the thaw, and a flock placed before the freeze stays placed through it.</summary>
    public class DuckJarIceTests
    {
        [TearDown] public void Thaw() { PondIce.Force = null; }

        static SimCore OpenDuckJar()
        {
            var sim = PlacedLifeTests.Camp(7, 0);
            PondIce.Force = false;
            sim.DebugJarsReady();
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Ducks), "the jar is open in the thaw");
            Assert.GreaterOrEqual(sim.LifePlaceCellNearCamp(LifeSpecies.Ducks), 0, "there is duck ground");
            return sim;
        }

        [Test]
        public void FrozenPond_TheDuckJarIsShut_PlacingRefused_NothingSpent_TheLineSaysWhy()
        {
            var sim = OpenDuckJar();
            int cell = sim.LifePlaceCellNearCamp(LifeSpecies.Ducks), chron = sim.State.chronicle.Count;
            PondIce.Force = true;
            Assert.AreEqual(JarState.Locked, sim.JarStateOf(LifeSpecies.Ducks), "shut like any shut jar");
            Assert.IsFalse(sim.JarReady(LifeSpecies.Ducks));
            var ready = new List<LifeSpecies>(); sim.ReadyJars(ready);
            var locked = new List<LifeSpecies>(); sim.LockedJars(locked);
            CollectionAssert.DoesNotContain(ready, LifeSpecies.Ducks);
            CollectionAssert.Contains(locked, LifeSpecies.Ducks, "the chooser lists it greyed");
            Assert.AreEqual(EcoModel.IceNeedLine, sim.JarNeed(LifeSpecies.Ducks));
            StringAssert.Contains("ice", sim.JarNeed(LifeSpecies.Ducks));
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Ducks, cell));
            Assert.IsFalse(sim.PlaceLife(LifeSpecies.Ducks, cell), "refused");
            Assert.AreEqual(0, sim.EcoDucks, "no ducks came down");
            Assert.IsFalse(sim.LifeIsPlaced(LifeSpecies.Ducks));
            Assert.AreEqual(chron, sim.State.chronicle.Count, "no history line");
            Assert.AreEqual(JarState.Ready, new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).JarStateOf(LifeSpecies.Ducks), "the jar itself stays open under the ice");
            Assert.IsNull(sim.JarNeed(LifeSpecies.Rabbits), "other jars read as before");
        }

        [Test]
        public void AfterTheThaw_TheJarOpens_APlacedFlockStays_AndAFreezeAfterPlacingKeepsIt()
        {
            var sim = OpenDuckJar();
            int cell = sim.LifePlaceCellNearCamp(LifeSpecies.Ducks);
            PondIce.Force = true;
            Assert.IsFalse(sim.PlaceLife(LifeSpecies.Ducks, cell));
            PondIce.Force = false; // the thaw
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Ducks));
            Assert.IsNull(sim.JarNeed(LifeSpecies.Ducks));
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Ducks, cell), "placed once it is open");
            Assert.AreEqual(2, sim.EcoDucks);
            // the pond freezes right after the placing: the placing is kept, the jar is placed (not shut), the flock's count stays
            PondIce.Force = true;
            Assert.AreEqual(JarState.Placed, sim.JarStateOf(LifeSpecies.Ducks));
            Assert.IsTrue(sim.LifeIsPlaced(LifeSpecies.Ducks));
            Assert.AreEqual(2, sim.EcoDucks, "away for the winter, count kept (#618)");
            Assert.IsTrue(PondIce.DucksAway(sim, sim.State.month));
            PondIce.Force = false;
            Assert.AreEqual(2, sim.EcoDucks, "back at the thaw");
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.kind == "life_ducks"), "placed once");
        }

        [Test]
        public void TheCalendarWinter_ShutsTheJar_AndSpringOpensIt()
        {
            var sim = OpenDuckJar();
            PondIce.Force = null;
            bool sawShut = false, sawOpenAfter = false;
            for (int k = 0; k < 36 && !sawOpenAfter; k++)
            {
                sim.Step();
                bool winter = PondIce.Winter(sim.State.month);
                var st = sim.JarStateOf(LifeSpecies.Ducks);
                if (winter) { sawShut = true; Assert.AreEqual(JarState.Locked, st, "month " + sim.State.month); Assert.AreEqual(EcoModel.IceNeedLine, sim.JarNeed(LifeSpecies.Ducks)); }
                else if (sawShut) { Assert.AreEqual(JarState.Ready, st, "month " + sim.State.month); sawOpenAfter = true; }
            }
            Assert.IsTrue(sawShut && sawOpenAfter, "a winter came and went");
        }
    }
}
