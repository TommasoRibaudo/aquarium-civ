using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Eras are plateaus, crossed live (game definition §8, #23): a threshold only makes the next era ready; it
    /// waits, live and in catch-up, until the view calls AdvanceEra with the player watching.</summary>
    public class EraTests
    {
        static bool OnlyEraTypes(SimCore sim) => sim.State.buildings.All(b => BuildingType.EraOf(b.type) <= sim.State.era);

        /// <summary>Advance hour by hour, crossing each era the moment it is ready (a player who is always watching).</summary>
        static void Watched(SimCore sim, int hours)
        {
            for (int h = 0; h < hours; h++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
            }
        }

        [Test]
        public void ANewBoxIsACamp()
        {
            var s = NewSim().State;
            Assert.AreEqual(Era.Camp, s.era);
            Assert.IsFalse(s.eraReady);
            Assert.AreEqual(-1, s.eraReadyMonth);
            Assert.AreEqual(Era.Camp, s.years[0].era);
        }

        [Test]
        public void EraAdvancesOnlyThroughAdvanceEra()
        {
            int ready = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = NewSim(seed);
                sim.Advance(120); // five days unwatched
                Assert.AreEqual(Era.Camp, sim.State.era, $"seed {seed}: the era moved on its own");
                Assert.IsTrue(OnlyEraTypes(sim), $"seed {seed}: a waiting camp built a village building");
                if (!sim.State.eraReady) continue;
                ready++;
                Assert.IsTrue(sim.AdvanceEra());
                Assert.AreEqual(Era.Village, sim.State.era);
                Assert.IsFalse(sim.State.eraReady);
                Assert.IsFalse(sim.AdvanceEra(), "one threshold, one era");
            }
            Assert.GreaterOrEqual(ready, 18, $"only {ready}/20 camps reached the village threshold in 120 years");
        }

        [Test]
        public void CatchUpOfTwoDaysStopsAtThePlateau()
        {
            int waited = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                var sim = NewSim(seed);
                var r = sim.CatchUp(48);
                Assert.AreEqual(18 * 12, r.months, "the people keep living");
                Assert.AreEqual(Era.Camp, sim.State.era, $"seed {seed}: catch-up crossed an era");
                Assert.IsTrue(OnlyEraTypes(sim), $"seed {seed}");
                if (!sim.State.eraReady) continue;
                waited++;
                Assert.Less(sim.State.eraReadyMonth, sim.State.month, $"seed {seed}: the threshold was met on the way and it waited");
                Assert.GreaterOrEqual(sim.State.eraReadyMonth, sim.Config.villageAt.years * 12L);

                // A second absence still waits, and the ready month doesn't move.
                long readyAt = sim.State.eraReadyMonth;
                sim.CatchUp(48);
                Assert.AreEqual(Era.Camp, sim.State.era);
                Assert.AreEqual(readyAt, sim.State.eraReadyMonth);
            }
            TestContext.WriteLine($"{waited}/30 camps were waiting after 48 h away");
            Assert.GreaterOrEqual(waited, 8, "the plateau should be common after two days away");
        }

        static string World(SimState s) => Fingerprint(s) + "|" + s.era + "|" + s.eraReady + "|" + s.eraReadyMonth + "|" + s.eraStartMonth + "|" +
                                           s.years.Count + ":" + string.Join(",", s.years.Select(y => y.era));

        [Test]
        public void CatchUpEqualsLiveSteppingThroughTheThreshold()
        {
            var offline = NewSim(1234); var live = NewSim(1234);
            offline.Advance(48);
            for (int i = 0; i < 48 * 60; i++) live.Advance(1.0 / 60);
            Assert.IsTrue(offline.State.eraReady);
            Assert.AreEqual(World(offline.State), World(live.State));

            // ...and crossing it later gives the same world either way.
            offline.AdvanceEra(); live.AdvanceEra();
            offline.Advance(10);
            for (int i = 0; i < 10 * 12; i++) live.Advance(1.0 / 12);
            Assert.AreEqual(Era.Village, live.State.era);
            Assert.AreEqual(World(offline.State), World(live.State));
        }

        [Test]
        public void ReadyNeedsAllThreeNumbers()
        {
            var high = new SimConfig { villageAt = new EraThreshold { population = 0, buildings = 0, years = 1000 } };
            var sim = NewSim(5, high);
            sim.Advance(48);
            Assert.IsFalse(sim.State.eraReady, "years not reached");

            var none = new SimConfig { villageAt = new EraThreshold { population = 0, buildings = 0, years = 0 } };
            sim = NewSim(5, none);
            int raised = 0;
            sim.EraReadied += e => { raised++; Assert.AreEqual(Era.Village, e); };
            sim.Step();
            Assert.IsTrue(sim.State.eraReady);
            Assert.AreEqual(1, sim.State.eraReadyMonth);
            sim.Advance(5);
            Assert.AreEqual(1, raised, "raised once per threshold");
        }

        [Test]
        public void TheYearsCountFromTheStartOfTheEra()
        {
            var cfg = new SimConfig { townAt = new EraThreshold { population = 0, buildings = 0, years = 5 } };
            var sim = NewSim(3, cfg);
            sim.Advance(10);
            sim.ForceNextEra();
            Assert.AreEqual(Era.Village, sim.State.era);
            Assert.AreEqual(0, sim.YearsInEra);
            sim.Advance(4);
            Assert.IsFalse(sim.State.eraReady, "a new era needs its own years");
            sim.Advance(1.01);
            Assert.IsTrue(sim.State.eraReady);
        }

        [Test]
        public void TheVillageBuildsVillageThings()
        {
            int villageBuilt = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = NewSim(seed);
                Watched(sim, 72);
                if (sim.State.era < Era.Village) continue;
                Assert.IsTrue(OnlyEraTypes(sim));
                if (sim.State.buildings.Any(b => BuildingType.EraOf(b.type) == Era.Village)) villageBuilt++;
                var line = sim.State.chronicle.First(e => e.kind == "era");
                StringAssert.Contains("farming village", line.text);
            }
            Assert.GreaterOrEqual(villageBuilt, 8, $"only {villageBuilt}/10 villages built a well, mill, market...");
        }

        [Test]
        public void AWatchedBoxReachesTheTownAndStops()
        {
            var sim = NewSim(7);
            Watched(sim, 150);
            Assert.AreEqual(Era.Town, sim.State.era);
            Assert.IsTrue(sim.IsFinalEra);
            Assert.IsNull(sim.NextEraThreshold);
            Assert.IsFalse(sim.State.eraReady, "no era after the town");
            Assert.AreEqual(2, sim.State.chronicle.Count(e => e.kind == "era"));
            var eras = sim.State.years.Select(y => y.era).ToList();
            for (int i = 1; i < eras.Count; i++) Assert.GreaterOrEqual(eras[i], eras[i - 1], "the time-lapse sees eras in order");
            Assert.AreEqual(Era.Town, eras[^1]);
        }

        [Test]
        public void ErasNeverShiftTheEconomyDice()
        {
            var camp = NewSim(99); var town = NewSim(99);
            town.ForceNextEra(); town.ForceNextEra();
            camp.Advance(30); town.Advance(30);
            Assert.AreEqual(camp.State.econRng.state, town.State.econRng.state);
        }

        [Test]
        public void TheTownEraBuildsLikeTheSimBeforeEras()
        {
            // Every type is allowed in the town, so seed 1234 builds like the sim before eras (#23); its notables follow the
            // town band (#26), so the number is its own now.
            var sim = NewSim(1234);
            sim.State.era = Era.Town;
            sim.Advance(48);
            Assert.AreEqual(2393058935u, Fingerprint(sim.State)); // #26: the town band (8–12 adults, +2 children) and 55–80 lifespans; #27: role lines in the chronicle (economy unchanged); #28: term lines and codas in the chronicle (economy unchanged); #59: the strange light is the Bright One's until the Lamp is used (text only); #60: town sweepers; #61: read-through text fixes (text and roles only)
            var before = NewSim(1234);
            before.CardsOn = false; // #59 off: the text as it was, so that is all that changed
            before.State.era = Era.Town;
            before.Advance(48);
            Assert.AreEqual(2652515594u, Fingerprint(before.State));
        }

        [Test]
        public void ACampAnswersTheKeeperWithCampThings()
        {
            int answered = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = NewSim(seed);
                sim.Advance(6);
                var n = sim.PlaceNudge(SimCore.NudgeKind.Channel, 3);
                sim.Advance(3);
                Assert.AreEqual(Era.Camp, sim.State.era);
                if (!n.credited) continue;
                answered++;
                Assert.AreEqual(BuildingType.Farm, sim.State.buildings.First(b => b.id == n.buildingId).type, "a field, not a windmill");
            }
            Assert.GreaterOrEqual(answered, 18);
        }

        [Test]
        public void DebugCheats()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.ForceEraReady());
            Assert.AreEqual(Era.Camp, sim.State.era, "ready, still waiting");
            Assert.IsTrue(sim.State.eraReady);
            Assert.IsFalse(sim.ForceEraReady(), "already ready");
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(Era.Village, sim.State.era);
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(Era.Town, sim.State.era);
            Assert.IsFalse(sim.ForceNextEra());
            Assert.IsFalse(sim.ForceEraReady());
            Assert.IsTrue(sim.Allowed(BuildingType.Guildhall));
        }

        [Test]
        public void TheEraIsSaved()
        {
            var sim = NewSim();
            sim.Advance(5);
            sim.ForceNextEra();
            sim.Advance(2);
            sim.ForceEraReady();
            var loaded = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            Assert.AreEqual(Era.Village, loaded.era);
            Assert.IsTrue(loaded.eraReady);
            Assert.AreEqual(sim.State.eraStartMonth, loaded.eraStartMonth);
            Assert.AreEqual(sim.State.eraReadyMonth, loaded.eraReadyMonth);
        }

        [TestCase(BuildingType.Camp, Era.Camp)]
        [TestCase(BuildingType.Hut, Era.Camp)]
        [TestCase(BuildingType.Farm, Era.Camp)]
        [TestCase(BuildingType.Lumbermill, Era.Camp)]
        [TestCase(BuildingType.Well, Era.Village)]
        [TestCase(BuildingType.Windmill, Era.Village)]
        [TestCase(BuildingType.Market, Era.Village)]
        [TestCase(BuildingType.Tavern, Era.Village)]
        [TestCase(BuildingType.Church, Era.Village)]
        [TestCase(BuildingType.Workshop, Era.Town)]
        [TestCase(BuildingType.Guildhall, Era.Town)]
        public void BuildingEras(string type, Era era) => Assert.AreEqual(era, BuildingType.EraOf(type));
    }
}
