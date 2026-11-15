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
            sim.State.population = 9; Month(sim);
            Assert.AreEqual(0, sim.SeedsInPouch, "9 people: not a step yet");
            sim.State.population = 10; Month(sim);
            Assert.AreEqual((1, 1), (sim.SeedsInPouch, sim.BushesInPouch), "+5 people: one of each");
            for (int i = 0; i < 5; i++) Month(sim);
            Assert.AreEqual((1, 1), (sim.SeedsInPouch, sim.BushesInPouch), "a step pays once");
            sim.State.population = 15; Month(sim);
            Assert.AreEqual((2, 2), (sim.SeedsInPouch, sim.BushesInPouch));
        }

        [Test]
        public void ANewEra_PaysOneOfEach()
        {
            var sim = World();
            Assert.AreEqual(1, new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).LandscapeSteps(1, 5), "the village era is one step even with the start's people");
            Assert.IsTrue(sim.ForceNextEra());
            Month(sim);
            Assert.AreEqual((1, 1), (sim.SeedsInPouch, sim.BushesInPouch), "the village era");
            Assert.IsTrue(sim.ForceNextEra());
            Month(sim);
            Assert.AreEqual((2, 2), (sim.SeedsInPouch, sim.BushesInPouch), "the town era");
        }

        [Test]
        public void AShrinkingVillage_PaysNothing_AndTheOldPeakStays()
        {
            var sim = World();
            sim.State.population = 20; Month(sim);
            Assert.AreEqual((3, 3), (sim.SeedsInPouch, sim.BushesInPouch), "the peak of 20 is three steps");
            sim.DebugTakeFromPouch(3, 3);
            sim.State.population = 8; Month(sim);
            Assert.AreEqual((0, 0), (sim.SeedsInPouch, sim.BushesInPouch), "a village that shrank pays nothing");
            Assert.AreEqual(20, sim.State.eco.peakPeople);
            sim.State.population = 20; Month(sim);
            Assert.AreEqual((0, 0), (sim.SeedsInPouch, sim.BushesInPouch), "growing back to the old peak pays nothing again");
            sim.State.population = 25; Month(sim);
            Assert.AreEqual((1, 1), (sim.SeedsInPouch, sim.BushesInPouch), "only a new peak does");
        }

        [Test]
        public void TheCapHolds_AndAnOwedStepWaitsForRoom()
        {
            var sim = World();
            sim.State.population = 40; Month(sim); // 7 steps
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
            sim.State.population = 15; Month(sim);
            sim.DebugTakeFromPouch(1, 0);
            var loaded = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            Assert.AreEqual((1, 2), (core.SeedsInPouch, core.BushesInPouch));
            Assert.AreEqual(15, loaded.eco.peakPeople);
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
