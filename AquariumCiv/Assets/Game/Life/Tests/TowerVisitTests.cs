using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#404 (the user 2026-11-07): people use the lookout tower the people apart gave the Keeper. Once it stands, a few townsfolk
    /// walk out to it some evenings (a couple together), and on some evenings the kids go there to dare each other; before it stands,
    /// nobody does.</summary>
    public class TowerVisitTests
    {
        static SimCore Town(int seed, bool tower)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < Era.Town; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            Assert.AreEqual(Era.Town, sim.State.era);
            for (int m = 0; m < 24; m++) sim.Step(); // past the town's founding festival evenings
            var someone = new Notable { id = 990001, name = "Ash", family = "Reed", alive = true, age = 30f, lifespan = 80f };
            var g = new ApartGroup { id = 1, kind = ApartKind.ShoreHut, family = "Reed", x = 1.5f, z = -1f, sinceMonth = sim.State.month, members = new List<Notable> { someone } };
            if (tower) { g.tower = true; g.towerDone = true; g.towerFrom = sim.State.month - 3 * SimCore.TowerStageMonths; g.towerX = 1.8f; g.towerZ = -1f; }
            sim.State.apartGroups.Add(g);
            Assert.AreEqual(tower, sim.TowerStands);
            return sim;
        }

        /// <summary>Over 40 view days at 18:45: the grown-ups and the kids at the tower, and how many days had any.</summary>
        static (int adults, int kids, int adultDays, int kidDays, bool coupleTogether) Count(SimCore sim)
        {
            var planner = new LifePlanner(sim);
            var acts = new List<PersonActivity>();
            int adults = 0, kids = 0, adultDays = 0, kidDays = 0;
            bool couple = false;
            for (long day = sim.State.month; day < sim.State.month + 40; day++)
            {
                planner.PlanAll(new ViewTime(day, 18.75f), acts);
                var at = acts.Where(a => a.place.kind == PlaceKind.Tower && !a.moving).ToList();
                int ad = at.Count(a => a.kind == ActivityKind.SitOut), kd = at.Count(a => a.kind == ActivityKind.Play);
                adults += ad; kids += kd;
                if (ad > 0) adultDays++;
                if (kd > 0) kidDays++;
                foreach (var a in at) if (!a.partner.IsNone && at.Any(b => b.who.Equals(a.partner))) couple = true;
            }
            return (adults, kids, adultDays, kidDays, couple);
        }

        [Test]
        public void OnceItStands_SomeTownsfolkWalkOutToItInTheEvening_AndTheKidsDareThere()
        {
            int adults = 0, kids = 0, adultDays = 0, kidDays = 0; bool couple = false;
            foreach (int seed in new[] { 7, 11 })
            {
                var c = Count(Town(seed, true));
                TestContext.WriteLine($"seed {seed}: {c.adults} grown-up evenings at the tower over {c.adultDays} days, {c.kids} kids over {c.kidDays} days, a couple together {c.coupleTogether}");
                adults += c.adults; kids += c.kids; adultDays += c.adultDays; kidDays += c.kidDays; couple |= c.coupleTogether;
            }
            Assert.Greater(adultDays, 10, "most weeks someone walks out to it");
            Assert.Less(adults, 80 * 6, "a few, not the town");
            Assert.Greater(kidDays, 4, "some evenings the kids dare each other there");
            Assert.Less(kidDays, 40, "not every evening");
            Assert.IsTrue(couple, "a couple goes up together");
        }

        [Test]
        public void BeforeItStands_NobodyGoes()
        {
            var c = Count(Town(7, false));
            Assert.AreEqual(0, c.adults + c.kids, "no tower, no visits");
        }
    }
}
