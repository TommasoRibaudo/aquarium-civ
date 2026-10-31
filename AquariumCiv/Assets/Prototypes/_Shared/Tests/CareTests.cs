using System.Linq;
using AquariumCiv.Prototypes.Sim;
using NUnit.Framework;

namespace AquariumCiv.Prototypes.Tests
{
    /// <summary>Gate 1's care layer (CareConfig): it reproduces P2 variant B exactly, and it changes nothing when off.</summary>
    public class CareTests
    {
        static string Fingerprint(SimState s) =>
            s.population + "|" + s.month + "|" + s.rng.state + "|" + s.econRng.state + "|" +
            string.Join(",", s.buildings.Select(b => b.type + b.slot)) + "|" + string.Join("/", s.chronicle.Select(c => c.text));

        [Test]
        public void Gate1CareIsP2VariantB()
        {
            var p2b = new SimConfig { outputPenaltyEnabled = true, messAffectsMood = false }; // ReturnToMess.ApplyVariant, B
            var care = new SimConfig();
            CareConfig.Gate1.ApplyTo(care);
            var a = new SimCore(p2b, SimCore.NewWorld(1234, p2b));
            var b = new SimCore(care, SimCore.NewWorld(1234, care));
            a.Advance(48); b.Advance(48);
            Assert.AreEqual(Fingerprint(a.State), Fingerprint(b.State));
            Assert.AreEqual(0.25f, care.outputPenaltyMax);
            Assert.AreEqual(48f, care.messFullHours);
            Assert.IsFalse(care.messAffectsMood);
        }

        [Test]
        public void CareOffLeavesTheConfigAlone()
        {
            var cfg = new SimConfig { outputPenaltyEnabled = false, messAffectsMood = false, outputPenaltyMax = 0.1f, messFullHours = 30f };
            new CareConfig().ApplyTo(cfg);
            Assert.IsFalse(cfg.outputPenaltyEnabled);
            Assert.AreEqual(0.1f, cfg.outputPenaltyMax);
            Assert.AreEqual(30f, cfg.messFullHours);
            Assert.AreEqual(0f, new CareConfig().Grumpiness(new SimCore(cfg, SimCore.NewWorld(1, cfg))));
        }

        [Test]
        public void MessCapsAt48HoursAndThePaceDropAtAQuarter()
        {
            var cfg = new SimConfig();
            var care = CareConfig.Gate1;
            care.ApplyTo(cfg);
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            sim.Advance(24);
            Assert.Less(sim.MessLevel, 1f);
            Assert.Greater(CareConfig.WorkPacePercent(sim), 75);
            sim.Advance(24 * 6); // a week away: nothing gets worse than the cap
            Assert.AreEqual(1f, sim.State.grime, 1e-5f);
            Assert.AreEqual(1f, sim.State.overgrowth, 1e-5f);
            Assert.AreEqual(cfg.trashMax, sim.State.trash.Count);
            Assert.AreEqual(1f, sim.MessLevel, 1e-5f);
            Assert.AreEqual(0, CareConfig.SunlightPercent(sim));
            Assert.AreEqual(75, CareConfig.WorkPacePercent(sim));
            Assert.AreEqual(1f, care.Grumpiness(sim));
            Assert.IsTrue(care.MoodShows(sim));
            Assert.IsFalse(care.Cheers(sim));
        }

        [Test]
        public void ACleanTankReadsAsFullSunAndCheers()
        {
            var cfg = new SimConfig();
            var care = CareConfig.Gate1;
            care.ApplyTo(cfg);
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            sim.Advance(30);
            Assert.That(CareConfig.LedgerLine(sim), Does.StartWith("Sunlight "));
            sim.CleanAll();
            Assert.AreEqual(100, CareConfig.SunlightPercent(sim));
            Assert.AreEqual("Full sunlight  ·  work pace 100%", CareConfig.LedgerLine(sim));
            Assert.IsTrue(care.Cheers(sim));
            Assert.IsFalse(care.MoodShows(sim));
        }

        [Test]
        public void MessSlowsGrowthButNeverShrinksTheVillage()
        {
            // Over 50 seeds, 48 h untouched: the cared-for world is never behind, and neglect never loses people or buildings.
            int behind = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                var off = new SimConfig { outputPenaltyEnabled = false, messAffectsMood = false };
                var on = new SimConfig();
                CareConfig.Gate1.ApplyTo(on);
                var clean = new SimCore(off, SimCore.NewWorld(seed, off));
                var messy = new SimCore(on, SimCore.NewWorld(seed, on));
                int startPop = messy.State.population, startBuildings = messy.State.buildings.Count;
                clean.Advance(48); messy.Advance(48);
                if (messy.State.population > clean.State.population) behind++;
                Assert.GreaterOrEqual(messy.State.buildings.Count, startBuildings);
                Assert.GreaterOrEqual(messy.State.population, startPop);
            }
            Assert.LessOrEqual(behind, 2, "mess should not make the village bigger");
        }
    }
}
