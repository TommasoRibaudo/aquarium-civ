using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#588 the village keeps a stone fire pit on the green with the well a short walk away, and everything that gathers
    /// (dance, story, supper) targets the pit; #589 the supper pick is played out that evening: the pot gathers people at the fire,
    /// the baskets send families home.</summary>
    public class FireAndSupperTests
    {
        static SimCore Make(int seed, Era era)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            if (era == Era.Camp) for (int m = 0; m < 36 && !sim.State.eraReady; m++) sim.Step();
            else
                for (int m = 0; m < 120 * 12 && sim.State.era < era; m++)
                {
                    sim.Step();
                    if (sim.State.eraReady) sim.AdvanceEra();
                }
            Assert.AreEqual(era, sim.State.era);
            for (int m = 0; m < 24 && era == Era.Village; m++) sim.Step();
            return sim;
        }

        [TestCase(3)]
        [TestCase(7)]
        public void TheVillageHasAFirePit_TheWellStandsWithinSixCells_AndTheGatheringIsThePit(int seed)
        {
            var sim = Make(seed, Era.Village);
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.Positions.TryGetPosition(new Place(PlaceKind.Fire), out float fx, out float fz), "the village has a fire place");
            Assert.That(planner.GatherPlace.kind, Is.EqualTo(PlaceKind.Fire), "the dance, the story and supper gather at the pit");
            var well = sim.Settlement.Lots.First(l => l.kind == AquariumCiv.Sim.Settle.LotKind.Well);
            float d = (float)System.Math.Sqrt((well.x - fx) * (well.x - fx) + (well.z - fz) * (well.z - fz));
            Assert.That(d, Is.LessThan(6f * AquariumCiv.Sim.Settle.SiteMap.Cell), "the well is within six cells of the pit");
            Assert.That(d, Is.GreaterThan(0.40f), "but clear of the dance ring round the pit");
        }

        [TestCase(3)]
        public void TheVillagesEveningCookAndGatheringTargetThePit(int seed)
        {
            var sim = Make(seed, Era.Village);
            var planner = LifePlanner.Wired(sim);
            var acts = new List<PersonActivity>();
            int atPit = 0, atWell = 0;
            long day0 = sim.State.month;
            for (int i = 0; i < 12; i++)
            {
                planner.PlanAll(new ViewTime(day0 + i, 18.6f), acts);
                foreach (var a in acts) { if (a.place.kind == PlaceKind.Fire) atPit++; if (a.place.kind == PlaceKind.Green) atWell++; }
            }
            Assert.That(atPit, Is.GreaterThan(0), "evenings gather at the pit");
            Assert.That(atWell, Is.EqualTo(0), "nobody gathers at the well any more");
        }

        static void Count(SimCore sim, int pick, out int people, out int atFire, out int eatAtFire, out int eatHome)
        {
            var planner = LifePlanner.Wired(sim);
            sim.DebugSupper(pick);
            var acts = new List<PersonActivity>();
            planner.PlanAll(new ViewTime(sim.State.month, 18.4f), acts);
            people = 0; atFire = 0; eatAtFire = 0; eatHome = 0;
            foreach (var a in acts)
            {
                if (a.kind == ActivityKind.Sleep) continue;
                people++;
                if (a.place.kind == PlaceKind.Fire) { atFire++; if (a.kind == ActivityKind.Eat) eatAtFire++; }
                if (a.place.kind == PlaceKind.Home && a.kind == ActivityKind.Eat) eatHome++;
            }
        }

        /// <summary>#589 tester: on the village's first day the era festival made every evening a feast, so both picks looked the same. The supper
        /// isn't asked in a festival year, and the cheat ends the festival so a village supper can be seen.</summary>
        [Test]
        public void NoSupperChoiceInTheFestivalYear_AndTheCheatEndsItSoThePickShows()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < Era.Village; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            Assert.AreEqual(Era.Village, sim.State.era);
            Assert.IsTrue(sim.FestivalOn, "the village's first year is a festival year");
            for (int m = 0; m < 6; m++) sim.Step();
            Assert.IsTrue(sim.FestivalOn);
            Assert.IsFalse(sim.State.dilemmas.Exists(d => d.defId == "small_supper" && d.month >= sim.State.month - 6), "no supper choice raised in the festival year");
            Count(sim, SupperPick.Pot, out int n1, out int fire1, out _, out _);
            Assert.IsFalse(sim.FestivalOn, "the supper cheat ended the festival");
            Count(sim, SupperPick.Baskets, out int n2, out int fire2, out _, out int home2);
            Assert.That(fire2, Is.LessThan(fire1), $"baskets empty the fire ({fire2} vs {fire1})");
            Assert.That(home2, Is.GreaterThan(0), "families eat at home");
        }

        [TestCase(3, Era.Camp)]
        [TestCase(7, Era.Camp)]
        [TestCase(3, Era.Village)]
        [TestCase(7, Era.Village)]
        public void EachSupperPickChangesWhereEveryoneEatsThatEvening(int seed, Era era)
        {
            var sim = Make(seed, era);
            Count(sim, SupperPick.None, out int n0, out int fire0, out _, out int home0);
            Count(sim, SupperPick.Pot, out int n1, out int fire1, out int eatFire1, out int home1);
            Count(sim, SupperPick.Baskets, out int n2, out int fire2, out _, out int home2);
            Assert.That(n1, Is.GreaterThan(4), "enough people to tell");
            Assert.That(fire1, Is.GreaterThan(fire0), $"the pot gathers more at the fire ({fire1} vs {fire0})");
            Assert.That(eatFire1 * 100, Is.GreaterThanOrEqualTo(n1 * 60), $"with the pot most eat at the fire ({eatFire1}/{n1})");
            Assert.That(home1, Is.EqualTo(0), "no basket suppers at home with the pot");
            Assert.That(fire2, Is.LessThan(fire1), $"baskets empty the fire ({fire2} vs {fire1})");
            Assert.That(fire2 * 100, Is.LessThanOrEqualTo(n2 * 40), $"only a few linger at the fire ({fire2}/{n2})");
            Assert.That(home2, Is.GreaterThan(home1), $"families eat at home with baskets ({home2})");
            Assert.That(home2, Is.GreaterThan(home0));
        }

        [Test]
        public void TheSupperPickOnlyLastsTonightAndTomorrow()
        {
            var sim = Make(3, Era.Camp);
            sim.DebugSupper(SupperPick.Pot);
            long m = sim.State.month;
            Assert.AreEqual(SupperPick.Pot, sim.SupperOn(m));
            Assert.AreEqual(SupperPick.Pot, sim.SupperOn(m + 1));
            Assert.AreEqual(SupperPick.None, sim.SupperOn(m + 2));
            Assert.AreEqual(SupperPick.None, sim.SupperOn(m - 1));
        }
    }
}
