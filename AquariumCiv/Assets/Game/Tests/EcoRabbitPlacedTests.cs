using System;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#419 (placed-life spec §5.2, §3.1): rabbits are placed by the Keeper, never arriving by themselves. A placed pair grows
    /// to what the meadow holds, the warren is the placed cell, and the camp gate waits on the placing.</summary>
    public partial class EcoTests
    {
        [Test]
        public void NoRabbits_EverWithoutAPlacing_FortyYears()
        {
            // a Keeper who tends the land all along but never opens the jar
            var sim = EcoWorld(7); var keeper = new Keeper(true, places: false);
            bool tracks = false; float most = 0f;
            for (int s = 0; s < 40 * 4; s++)
            {
                keeper.Play(sim);
                sim.Advance(0.25);
                most = Math.Max(most, sim.EcoRabbits);
                tracks |= sim.EcoRabbitTracksSeen;
            }
            Assert.Greater(sim.EcoMeadowShare, 0.2f, "meadow enough for the camp");
            Assert.AreEqual(0f, most, "no rabbit, ever");
            Assert.IsFalse(tracks, "no tracks either");
            Assert.IsFalse(sim.State.eco.Reached(EcoMilestones.FirstRabbit));
            Assert.IsFalse(sim.CampGateOpen, "no camp without the Keeper's rabbits");
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Thicket), "the first jar waits, open");

            // the thicket placed but never the rabbits: their jar waits forty years too
            var half = EcoWorld(7); var k2 = new Keeper(true, places: false);
            for (int s = 0; s < 40 * 4; s++)
            {
                if (half.JarReady(LifeSpecies.Thicket)) half.PlaceLife(LifeSpecies.Thicket, half.LifePlaceCellNearCamp(LifeSpecies.Thicket));
                k2.Play(half);
                half.Advance(0.25);
                Assert.AreEqual(0f, half.EcoRabbits, "season " + s);
            }
            Assert.IsTrue(half.LifeIsPlaced(LifeSpecies.Thicket));
            Assert.AreEqual(JarState.Ready, half.JarStateOf(LifeSpecies.Rabbits));
            Assert.IsFalse(half.CampGateOpen);
        }

        [Test]
        public void APlacedPair_GrowsToWhatTheMeadowHolds()
        {
            var sim = EcoWorld(7);
            LifeJarTests.Meadow(sim, 0.3f);
            sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
            int c = sim.LifePlaceCellNearCamp(LifeSpecies.Rabbits);
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Rabbits, c));
            Assert.AreEqual(2, sim.EcoRabbitCount, "a founding pair");
            for (int y = 0; y < 15; y++) sim.Advance(1);
            var e = sim.State.eco;
            float holds = (e.cGrass + e.cTall + 0.3f * e.cBush) / sim.Config.eco.meadowPerRabbit;
            TestContext.Out.WriteLine($"rabbits {sim.EcoRabbits:0.0}, the meadow holds {holds:0.0}");
            Assert.Greater(holds, 10f);
            Assert.That(sim.EcoRabbits, Is.InRange(holds * 0.85f, holds * 1.15f), "grown to the meadow's capacity");
        }

        [Test]
        public void TheWarren_IsThePlacedCell()
        {
            var sim = EcoWorld(7);
            LifeJarTests.Meadow(sim, 0.3f);
            sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
            var g = sim.EcoGround;
            // not the nearest valid cell: one well out in the meadow
            int c = Enumerable.Range(0, EcoLand.N).Where(x => sim.CanPlaceLife(LifeSpecies.Rabbits, x))
                .OrderByDescending(x => Terrain.Dist(SiteMap.CXc(x), SiteMap.CZc(x), g.campX, g.campZ)).First();
            int arrived = -1; sim.RabbitArrived += x => arrived = x;
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Rabbits, c));
            Assert.AreEqual(c, sim.EcoRabbitWarren);
            Assert.AreEqual(c, arrived, "the first-rabbit sign plays at the placed cell");
            Assert.IsTrue(sim.EcoRabbitArrivalPending);
            var f = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(f);
            Assert.AreEqual(c, f.WarrenCell, "the herd lives round the placed cell");
            sim.DebugSkipSeasons(8);
            Assert.AreEqual(c, sim.EcoRabbitWarren, "the warren stays put");
            // a restock brings another pair to the same herd
            Assert.IsTrue(sim.DebugJarAgain(LifeSpecies.Rabbits));
            float before = sim.EcoRabbits;
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Rabbits, sim.LifePlaceCellNearCamp(LifeSpecies.Rabbits)));
            Assert.AreEqual(before + 2f, sim.EcoRabbits, 1e-4f);
            Assert.AreEqual(c, sim.EcoRabbitWarren);
        }

        [Test]
        public void TheCampGate_OpensOnlyAfterTheRabbitsArePlaced()
        {
            var sim = EcoWorld(7);
            LifeJarTests.Meadow(sim, 0.3f);
            sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
            sim.DebugSkipSeasons(8);
            Assert.GreaterOrEqual(sim.EcoMeadowShare, sim.Config.eco.campMeadow, "the meadow is there");
            Assert.IsFalse(sim.CampGateOpen, "but no rabbits placed: no camp");
            Assert.IsTrue(sim.BeforeCamp);
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Rabbits, sim.LifePlaceCellNearCamp(LifeSpecies.Rabbits)));
            long placed = sim.State.month;
            sim.DebugSkipSeasons(1);
            Assert.IsTrue(sim.CampGateOpen, "the season after the placing");
            Assert.GreaterOrEqual(sim.State.eco.campGateMonth, placed);
            Assert.IsTrue(sim.State.chronicle.Any(x => x.kind == "eco_camp"));
            // hunting is as it was: never below the floor
            for (int i = 0; i < 50; i++) sim.HuntCatch();
            Assert.AreEqual(SimCore.HuntFloor, sim.EcoRabbitCount, "the hunt floor holds");
        }
    }
}
