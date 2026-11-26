using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#676: the camp pot's stew follows what the camp has: fresh rabbit, then dried fish, then berries and roots, else a thin broth.</summary>
    public class StewKindTests
    {
        static SimCore Eco(int seed = 5)
        {
            var cfg = new SimConfig { ecology = true, settle = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        [Test] public void Nothing_IsAThinBroth() => Assert.AreEqual(StewKind.Thin, StewKinds.Decide(10, -1, 0, 0f));
        [Test] public void OnlyBerriesInTheStores_IsABerryStew() => Assert.AreEqual(StewKind.Berries, StewKinds.Decide(10, -1, 0, 12f));
        [Test] public void FishOnThePile_IsAFishStew_EvenWithBerries() => Assert.AreEqual(StewKind.Fish, StewKinds.Decide(10, -1, 3, 40f));
        [Test] public void ARabbitHuntToday_IsARabbitStew() => Assert.AreEqual(StewKind.Rabbit, StewKinds.Decide(10, 10, 0, 40f));
        [Test] public void RabbitBeatsFish_WhileTheMeatIsFresh_ThenFishReturns()
        {
            Assert.AreEqual(StewKind.Rabbit, StewKinds.Decide(11, 10, 5, 40f), "the day after the hunt");
            Assert.AreEqual(StewKind.Fish, StewKinds.Decide(12, 10, 5, 40f), "two days on the meat is eaten");
            Assert.AreEqual(StewKind.Berries, StewKinds.Decide(12, 10, 0, 40f), "then berries");
        }
        [Test] public void ACatchInTheFuture_OrNever_IsNotRabbit()
        {
            Assert.AreEqual(StewKind.Thin, StewKinds.Decide(5, 9, 0, 0f), "a view day earlier than the catch");
            Assert.AreEqual(StewKind.Thin, StewKinds.Decide(0, -1, 0, 0.5f), "under a unit of food is nothing");
        }

        [Test]
        public void TheSimReadsItsOwnState_AHuntCatchMakesRabbit_TheCheatsReachEachKind()
        {
            var sim = Eco();
            sim.State.food = 20f;
            Assert.AreEqual(StewKind.Berries, sim.StewOn(sim.State.month));
            sim.DebugFillFishBasket(4); sim.FishHang(4, 1); sim.FishToPile(9, 3);
            Assert.AreEqual(StewKind.Fish, sim.StewOn(sim.State.month));
            sim.State.eco.rabbitCatchDay = sim.State.month;
            Assert.AreEqual(StewKind.Rabbit, sim.StewOn(sim.State.month));
            foreach (var k in new[] { StewKind.Fish, StewKind.Rabbit, StewKind.Berries, StewKind.Thin })
                Assert.AreEqual(k, sim.DebugStew(k), "cheat reaches " + k);
        }

        [Test]
        public void AFreshStateHasNoRabbitDay_AndAHuntCatchSetsIt()
        {
            var sim = Eco();
            Assert.AreEqual(-1L, sim.State.eco.rabbitCatchDay);
            sim.DebugAddRabbits(4);
            if (sim.HuntSurplus > 0 && sim.HuntCatch()) Assert.AreEqual(sim.State.month, sim.State.eco.rabbitCatchDay);
            else Assert.Inconclusive("the herd was at its floor in this world");
        }
    }
}
