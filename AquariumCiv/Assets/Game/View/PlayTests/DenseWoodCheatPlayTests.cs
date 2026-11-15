using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Tester run 2 (playtest 1 batch 2): the debug panel's "Dense wood" cheat (#690) must leave a wood you can see and open the deer jar
    /// now, not at the season's end. On a bare staged land (the thicket and the rabbits placed, so the rung below is met).
    /// </summary>
    [Category("DenseWood")]
    public class DenseWoodCheatPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        [UnityTest, Timeout(600000)]
        public IEnumerator TheDenseWoodCheatDrawsTreesAndOpensTheDeerJar()
        {
            UseLivingLand(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            var sim = Sim;
            Assert.IsTrue(sim.EcologyOn, "a living-land box");
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0, "the thicket placed (the rung below the rabbits)");
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0, "the rabbits placed (the rung below the deer)");
            yield return Frames(30);
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            Assert.AreEqual(JarState.Locked, sim.JarStateOf(LifeSpecies.Deer), "before the cheat the deer jar is shut");
            int spotsBefore = box.TreeSpots.Count;
            Assert.AreEqual(0, sim.EcoWoodTrees, "a bare land: no wood");

            Assert.IsTrue(sim.DebugGrowFarWood(), "the cheat finds room");
            Assert.GreaterOrEqual(sim.EcoWoodTrees, sim.Config.eco.jarDeerWood, "the sim has a wood");
            // the view redraws by itself (no panel button, no manual Refresh): a few frames
            yield return Frames(10);
            Assert.IsTrue(sim.JarStateOf(LifeSpecies.Deer) != JarState.Locked, "the deer jar opened now: " + sim.JarStateOf(LifeSpecies.Deer) + ", need '" + sim.JarNeed(LifeSpecies.Deer) + "'");
            Assert.IsTrue(string.IsNullOrEmpty(sim.JarNeed(LifeSpecies.Deer)), "nothing is still missing");
            Assert.GreaterOrEqual(box.TreeSpots.Count, spotsBefore + 3, $"trees are drawn: {box.TreeSpots.Count} grown-tree spots (was {spotsBefore})");
            yield return Shot("dense-wood-cheat");
            AssertNoErrors("dense wood cheat");
        }
    }
}
