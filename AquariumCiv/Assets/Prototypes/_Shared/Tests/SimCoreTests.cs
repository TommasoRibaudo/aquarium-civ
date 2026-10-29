using System.Linq;
using AquariumCiv.Prototypes.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Prototypes.Tests
{
    public class SimCoreTests
    {
        static SimCore NewSim(int seed = 42, SimConfig cfg = null)
        {
            cfg ??= new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        [Test]
        public void SameSeedProducesSameWorld()
        {
            var a = NewSim(); var b = NewSim();
            a.Advance(48); b.Advance(48);
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b.State));
        }

        [Test]
        public void CatchUpEqualsLiveStepping()
        {
            var offline = NewSim(); var live = NewSim();
            offline.Advance(48);
            for (int i = 0; i < 48 * 60; i++) live.Advance(1.0 / 60); // one call per real minute
            Assert.AreEqual(offline.State.month, live.State.month);
            Assert.AreEqual(JsonUtility.ToJson(offline.State.buildings), JsonUtility.ToJson(live.State.buildings));
            Assert.AreEqual(offline.State.population, live.State.population);
            Assert.AreEqual(offline.State.chronicle.Count, live.State.chronicle.Count);
        }

        [Test]
        public void OneRealHourIsOneGameYear()
        {
            var sim = NewSim();
            sim.Advance(1);
            Assert.AreEqual(12, sim.State.month);
        }

        [Test]
        public void MessIsCappedAndNothingIsLost()
        {
            var sim = NewSim();
            sim.Advance(48);
            int buildings = sim.State.buildings.Count;
            int pop = sim.State.population;
            sim.Advance(24 * 30); // a month of neglect
            Assert.LessOrEqual(sim.State.grime, 1f);
            Assert.LessOrEqual(sim.State.overgrowth, 1f);
            Assert.LessOrEqual(sim.State.trash.Count, sim.Config.trashMax);
            Assert.GreaterOrEqual(sim.State.buildings.Count, buildings, "neglect must never remove buildings");
            Assert.GreaterOrEqual(sim.State.population, 5, "the civ never collapses");
            Assert.Greater(pop, 0);
        }

        [Test]
        public void OutputPenaltyIsCappedAndOptional()
        {
            var cfg = new SimConfig();
            var sim = NewSim(cfg: cfg);
            sim.State.grime = sim.State.overgrowth = 1f;
            for (int i = 0; i < cfg.trashMax; i++) sim.State.trash.Add(new TrashItem { id = 1000 + i });
            Assert.AreEqual(cfg.outputPenaltyMax, sim.OutputPenalty, 1e-5f);
            cfg.outputPenaltyEnabled = false;
            Assert.AreEqual(0f, sim.OutputPenalty);
        }

        [Test]
        public void NotablesDieOnlyOfOldAgeAndAgeAtHalfSpeed()
        {
            var sim = NewSim();
            var founder = sim.State.notables[0];
            float startAge = founder.age;
            sim.Advance(10); // 10 game years
            if (founder.alive) Assert.AreEqual(startAge + 5f, founder.age, 1e-3f);
            sim.Advance(24 * 14);
            foreach (var n in sim.State.notables.Where(n => !n.alive))
                Assert.GreaterOrEqual(n.age, n.lifespan, $"{n.FullName} died before their lifespan");
            Assert.GreaterOrEqual(sim.State.notables.Count(n => n.alive), sim.Config.minNotables - 1);
        }

        [Test]
        public void VillageGrowsOverAFewDays()
        {
            var sim = NewSim();
            sim.Advance(72);
            Assert.Greater(sim.State.population, 10);
            Assert.Greater(sim.State.buildings.Count, 4);
            Assert.Greater(sim.State.chronicle.Count, 5);
        }
    }
}
