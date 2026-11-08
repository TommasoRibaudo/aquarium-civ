using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>User 2026-11-08 (#420 follow-up): the village and the town hunt the deer too, about half as often as the camp, against
    /// the real planner; the floors, the spring pause and the restraint rules still hold over 40 game years of village hunts.</summary>
    public class VillageDeerHuntTests
    {
        /// <summary>The camp's rungs, a far wood and a full herd of deer.</summary>
        static SimCore Deer(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0);
            Assert.IsTrue(sim.DebugGrowFarWood());
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Deer), 0);
            sim.DebugDeerToCap();
            Assert.Greater(sim.DeerHuntSurplus, 0);
            return sim;
        }

        static int HuntDays(SimCore sim, long from, int days)
        {
            var planner = new LifePlanner(sim);
            int n = 0;
            for (long d = from; d < from + days; d++) n += planner.DeerHuntsOn(d).Count;
            return n;
        }

        [Test]
        public void TheVillageAndTheTown_HuntTheDeer_AboutHalfAsOftenAsTheCamp()
        {
            Assert.AreEqual(LifePlanner.DeerHuntDayChance, LifePlanner.DeerHuntChanceIn(Era.Camp));
            Assert.AreEqual(LifePlanner.DeerHuntDayChance * 0.5f, LifePlanner.DeerHuntChanceIn(Era.Village), 1e-6f);
            Assert.AreEqual(LifePlanner.DeerHuntDayChance * 0.5f, LifePlanner.DeerHuntChanceIn(Era.Town), 1e-6f);
            var sim = Deer();
            const int Days = 3000;
            int camp = HuntDays(sim, 0, Days);
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(Era.Village, sim.State.era);
            Assert.Greater(sim.DeerHuntSurplus, 0);
            int village = HuntDays(sim, 0, Days);
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(Era.Town, sim.State.era);
            int town = HuntDays(sim, 0, Days);
            TestContext.Out.WriteLine($"deer hunts in {Days} view days: camp {camp}, village {village}, town {town}");
            Assert.Greater(camp, 200);
            Assert.That(village / (float)camp, Is.InRange(0.4f, 0.6f), "about half as often in the village");
            Assert.That(town / (float)camp, Is.InRange(0.4f, 0.6f), "and in the town");
        }

        [Test]
        public void FortyVillageYears_TheFloorsAndTheSpringRuleHold()
        {
            var sim = Deer();
            Assert.IsTrue(sim.ForceNextEra());
            var planner = new LifePlanner(sim);
            int caught = 0, hunts = 0, pausedDays = 0;
            long day = 0;
            for (int year = 0; year < 40; year++)
                for (int season = 0; season < 4; season++)
                {
                    for (int m = 0; m < 3; m++) sim.Step();
                    planner.Invalidate();
                    for (int k = 0; k < 40; k++, day++)
                    {
                        var plans = planner.DeerHuntsOn(day);
                        if (sim.LifeTakePaused(LifeSpecies.Deer))
                        {
                            pausedDays++;
                            Assert.IsEmpty(plans, "nobody hunts deer in spring once the people have ruled");
                        }
                        hunts += plans.Count;
                        var settled = new List<DeerHuntPlan>();
                        planner.ResolveDeerHunts(new ViewTime(day, 23.5f), settled);
                        caught += settled.Count(p => p.caught);
                        Assert.GreaterOrEqual(sim.EcoDeerCount, 2, $"the floor, year {year}");
                        Assert.GreaterOrEqual(sim.EcoDeerStags, 1, "never the last stag");
                        Assert.GreaterOrEqual(sim.EcoDeerDoes, 1, "never the last doe");
                    }
                }
            TestContext.Out.WriteLine($"40 village years: {hunts} deer hunts, {caught} deer taken, {pausedDays} spring days paused, herd {sim.EcoDeerCount}, ruled {sim.LifeRuledMonth(LifeSpecies.Deer)}");
            Assert.AreEqual(Era.Village, sim.State.era);
            Assert.Greater(hunts, 0, "the village hunts deer");
            Assert.Greater(caught, 0);
            Assert.GreaterOrEqual(sim.LifeRuledMonth(LifeSpecies.Deer), 0, "hunted to the floor, the people ruled");
            Assert.Greater(pausedDays, 0, "and the spring pause held");
        }
    }
}
