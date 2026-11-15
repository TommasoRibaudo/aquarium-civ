using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Tests
{
    /// <summary>#726 (landscaping.md 8.1, decision 2026-11-14): seeds and bushes are earned as the civ grows. One of each per new era
    /// and per +5 people at the PEAK; at most 3 wait; a step is paid once; a shrinking village pays nothing; saved.</summary>
    public class LandscapePouchTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static void Month(SimCore sim) => sim.Advance(sim.Config.StepHours);

        [Test]
        public void ANewBox_HasAnEmptyPouch()
        {
            var sim = World();
            Month(sim);
            Assert.AreEqual(0, sim.SeedsInPouch);
            Assert.AreEqual(0, sim.BushesInPouch);
            Assert.AreEqual(3, sim.PouchCap);
        }

        [Test]
        public void ThePeopleStep_PaysOneOfEach_OnceEach_FromTheStart()
        {
            var sim = World();
            sim.State.population = 6; Month(sim);
            Assert.AreEqual(0, sim.SeedsInPouch, "6 people: not a step yet");
            sim.State.population = 7; Month(sim);
            Assert.AreEqual((1, 1), (sim.SeedsInPouch, sim.BushesInPouch), "+2 people over the founders: one of each");
            for (int i = 0; i < 5; i++) Month(sim);
            Assert.AreEqual((1, 1), (sim.SeedsInPouch, sim.BushesInPouch), "a step pays once");
            sim.State.population = 9; Month(sim);
            Assert.AreEqual((2, 2), (sim.SeedsInPouch, sim.BushesInPouch));
        }

        [Test]
        public void TheCampGate_ANewKindOfBuilding_AndEachEightGrownTrees_PayToo()
        {
            var m = new EcoModel(World().State.eco, World().EcoGround, new EcoTuning());
            Assert.AreEqual(0, m.LandscapeSteps(0, 5, 2, 0, false), "the start: nothing");
            Assert.AreEqual(1, m.LandscapeSteps(0, 5, 2, 0, true), "the camp gate opened");
            Assert.AreEqual(2, m.LandscapeSteps(0, 5, 4, 0, false), "two new kinds of building");
            Assert.AreEqual(0, m.LandscapeSteps(0, 5, 2, 7, false), "seven grown trees: not yet");
            Assert.AreEqual(2, m.LandscapeSteps(0, 5, 2, 16, false), "sixteen grown trees: two");
            Assert.AreEqual(1 + 1 + 1 + 1 + 1, m.LandscapeSteps(1, 7, 3, 8, true), "era + camp gate + people + kind + trees");
            var sim = World();
            Assert.IsTrue(sim.DebugOpenCampGate());
            Month(sim);
            Assert.AreEqual((1, 1), (sim.SeedsInPouch, sim.BushesInPouch), "the camp stands: the first of each");
            sim.State.eco.cTrees = 8; sim.State.eco.peakTrees = 0;
            Month(sim);
            Assert.AreEqual(8, sim.State.eco.peakTrees);
            Assert.AreEqual((2, 2), (sim.SeedsInPouch, sim.BushesInPouch), "eight grown trees: a second step");
        }

        [Test]
        public void ANewEra_PaysOneOfEach()
        {
            var sim = World();
            Assert.AreEqual(1, new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).LandscapeSteps(1, 5), "the village era is one step even with the start's people");
            Assert.IsTrue(sim.ForceNextEra());
            Month(sim);
            // the era is one step, and the village green's well is a new kind of building: a second one
            Assert.AreEqual((2, 2), (sim.SeedsInPouch, sim.BushesInPouch), "the village era (and its well)");
            Assert.IsTrue(sim.ForceNextEra());
            Month(sim);
            Assert.AreEqual((3, 3), (sim.SeedsInPouch, sim.BushesInPouch), "the town era: the cap");
        }

        [Test]
        public void AShrinkingVillage_PaysNothing_AndTheOldPeakStays()
        {
            var sim = World();
            sim.State.population = 11; Month(sim);
            Assert.AreEqual((3, 3), (sim.SeedsInPouch, sim.BushesInPouch), "the peak of 11 is three steps");
            sim.DebugTakeFromPouch(3, 3);
            sim.State.population = 6; Month(sim);
            Assert.AreEqual((0, 0), (sim.SeedsInPouch, sim.BushesInPouch), "a village that shrank pays nothing");
            Assert.AreEqual(11, sim.State.eco.peakPeople);
            sim.State.population = 11; Month(sim);
            Assert.AreEqual((0, 0), (sim.SeedsInPouch, sim.BushesInPouch), "growing back to the old peak pays nothing again");
            sim.State.population = 13; Month(sim);
            Assert.AreEqual((1, 1), (sim.SeedsInPouch, sim.BushesInPouch), "only a new peak does");
        }

        [Test]
        public void TheCapHolds_AndAnOwedStepWaitsForRoom()
        {
            var sim = World();
            sim.State.population = 40; Month(sim); // 17 steps
            Assert.AreEqual((3, 3), (sim.SeedsInPouch, sim.BushesInPouch), "at most 3 wait");
            for (int i = 0; i < 12; i++) Month(sim);
            Assert.AreEqual((3, 3), (sim.SeedsInPouch, sim.BushesInPouch), "and it stays at 3");
            Assert.AreEqual((3, 3), (sim.State.eco.seedSteps, sim.State.eco.bushSteps), "only 3 steps were paid");
            sim.DebugTakeFromPouch(1, 0); Month(sim);
            Assert.AreEqual((3, 3), (sim.SeedsInPouch, sim.BushesInPouch), "a seed planted: the owed step pays into the room");
            Assert.AreEqual(4, sim.State.eco.seedSteps);
            Assert.AreEqual(3, sim.State.eco.bushSteps, "the bushes were not taken: their pouch is still full");
        }

        [Test]
        public void TheDebugCheats_GiveAndStep()
        {
            var sim = World();
            sim.DebugGiveSeeds(5); sim.DebugGiveBushes(2);
            Assert.AreEqual((5, 2), (sim.SeedsInPouch, sim.BushesInPouch), "the cheats may pass the cap");
            var fresh = World();
            Assert.AreEqual(2, fresh.DebugNextCivStep(), "a civ step: one seed and one bush");
            Assert.AreEqual((1, 1), (fresh.SeedsInPouch, fresh.BushesInPouch));
            Assert.AreEqual(2, fresh.DebugNextCivStep());
            Assert.AreEqual((2, 2), (fresh.SeedsInPouch, fresh.BushesInPouch));
        }

        [Test]
        public void TheCountersAreSaved()
        {
            var sim = World();
            sim.State.population = 9; Month(sim);
            sim.DebugTakeFromPouch(1, 0);
            var loaded = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            Assert.AreEqual((1, 2), (core.SeedsInPouch, core.BushesInPouch));
            Assert.AreEqual(9, loaded.eco.peakPeople);
            Month(core);
            Assert.AreEqual((1, 2), (core.SeedsInPouch, core.BushesInPouch), "the paid steps are not paid again after a load");
        }

        [Test]
        public void AnOldSave_HasNoPouch_AndTheFirstMonthPaysWhatTheCivOwes_UpToTheCap()
        {
            var sim = World();
            sim.State.population = 30; // an old box that grew before the pouch existed
            Assert.IsTrue(sim.ForceNextEra());
            var e = sim.State.eco;
            e.seedsPouch = e.bushesPouch = e.seedSteps = e.bushSteps = e.peakPeople = 0; // as a save from before has them
            var loaded = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            Assert.AreEqual(0, core.SeedsInPouch);
            Month(core);
            Assert.AreEqual((3, 3), (core.SeedsInPouch, core.BushesInPouch), "era + people owe more than the cap: it pays 3");
        }

        [Test]
        public void WithTheEcologyOff_NothingIsEarned()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            sim.State.population = 40; Month(sim);
            Assert.AreEqual((0, 0), (sim.SeedsInPouch, sim.BushesInPouch));
            sim.DebugGiveSeeds(2);
            Assert.AreEqual(0, sim.SeedsInPouch);
        }
    }
}
