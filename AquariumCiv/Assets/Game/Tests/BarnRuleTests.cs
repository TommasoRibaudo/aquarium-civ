using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain; // not UnityEngine.Terrain

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #791 (user call 2026-11-26): every village and town has at least one barn. A family's outbuilding is a barn 6 in 10 by its
    /// lot's hash, else a shed with a woodpile; the box's first outbuilding always is a barn (<see cref="Settlement.IsBarn"/>, which
    /// SimLayout draws as Pieces.Barn variant 0 and CampLife.BarnOf takes). A barn never turns into a shed as the village grows or
    /// across a save.
    /// </summary>
    public class BarnRuleTests
    {
        static void Year(SimCore sim, bool fed)
        {
            for (int m = 0; m < 12; m++)
            {
                if (fed)
                {
                    sim.State.population = Math.Max(sim.State.population, 26);
                    sim.State.wood = 2000f; sim.State.food = Math.Max(sim.State.food, 200f);
                }
                sim.Step();
                sim.State.simHours = sim.State.month * sim.Config.StepHours;
            }
        }

        /// <summary>A year grown on its own: the era moves on when it is ready.</summary>
        static void NaturalYear(SimCore sim)
        {
            for (int m = 0; m < 12; m++)
            {
                sim.Step();
                sim.State.simHours = sim.State.month * sim.Config.StepHours;
                if (sim.State.eraReady) sim.AdvanceEra();
            }
        }

        static List<Lot> Outbuildings(SimCore sim) => sim.Settlement.Lots.Where(l => l.kind == LotKind.Outbuilding && !l.retired).ToList();

        /// <summary>Asserts this year's rule (a barn once any outbuilding stands; every barn seen so far still a barn) and adds the new barns.</summary>
        static void Check(SimCore sim, int seed, string when, HashSet<int> barns)
        {
            var obs = Outbuildings(sim);
            var now = new HashSet<int>(obs.Where(l => sim.Settlement.IsBarn(l)).Select(l => l.id));
            if (obs.Count > 0) Assert.That(now.Count, Is.GreaterThan(0), $"seed {seed} {when} ({sim.State.era}): {obs.Count} outbuildings, no barn");
            foreach (int id in barns) Assert.That(now.Contains(id), $"seed {seed} {when}: the barn on lot {id} is no longer a barn");
            barns.UnionWith(now);
        }

        [Test]
        public void EveryVillageAndTownHasABarnThatStaysABarn()
        {
            int villages = 0, towns = 0, firstOnly = 0;
            for (int seed = 4230; seed < 4250; seed++)
            {
                var cfg = new SimConfig { settle = true };
                var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                var barns = new HashSet<int>();
                sim.ForceNextEra(); // the village, staged the way CampCheats.StageVillage does it
                for (int y = 0; y < 15; y++) { Year(sim, true); Check(sim, seed, $"village year {y}", barns); }
                Assert.That(sim.State.era, Is.EqualTo(Era.Village), $"seed {seed}");
                Assert.That(Outbuildings(sim).Count, Is.GreaterThan(0), $"seed {seed}: a 15-year village has an outbuilding");
                villages++;
                if (Outbuildings(sim).Count(l => sim.Settlement.IsBarn(l)) == 1 && Terrain.Hash3(seed, sim.Settlement.FirstOutbuilding(), 61) >= 0.6f) firstOnly++;

                // a save and a load keep the same barns
                var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
                Assert.That(Outbuildings(loaded).Where(l => loaded.Settlement.IsBarn(l)).Select(l => l.id), Is.EquivalentTo(barns), $"seed {seed}: after a load");
                sim = loaded;

                sim.ForceNextEra(); // the town
                for (int y = 0; y < 6; y++) { Year(sim, true); Check(sim, seed, $"town year {y}", barns); }
                Assert.That(sim.State.era, Is.GreaterThanOrEqualTo(Era.Town), $"seed {seed}");
                towns++;
            }
            Assert.That(villages, Is.EqualTo(20));
            Assert.That(towns, Is.EqualTo(20));
            Assert.That(firstOnly, Is.GreaterThan(0), "no seed needed the first-outbuilding rule: the test proves nothing");
        }

        [Test]
        public void AVillageGrownOnItsOwnHasABarnFromItsFirstYear()
        {
            int villages = 0;
            for (int seed = 4230; seed < 4250; seed++)
            {
                var cfg = new SimConfig { settle = true };
                var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                var barns = new HashSet<int>();
                int y = 0;
                while (sim.State.era < Era.Village && y < 140) { NaturalYear(sim); y++; }
                if (sim.State.era < Era.Village) continue;
                villages++;
                for (int i = 0; i < 12; i++)
                {
                    Assert.That(Outbuildings(sim).Count, Is.GreaterThan(0), $"seed {seed}: village year {i} has no outbuilding");
                    Check(sim, seed, $"natural village year {i}", barns);
                    NaturalYear(sim);
                }
            }
            Assert.That(villages, Is.EqualTo(20), "every seed reaches the village in 140 years");
        }
    }
}
