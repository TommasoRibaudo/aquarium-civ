using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>P8 #288 phase 1: berries by season, gatherers pick at the bush cells that grew, picking lowers the berries
    /// and they recover, the first bush is a sign (once), no woods trip without trees, the ecology off changes nothing.</summary>
    public class EcoBushesTests
    {
        static SimCore Land(int seed = 3, bool ecology = true, int bushes = 6)
        {
            var cfg = new SimConfig { ecology = ecology };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            if (ecology)
            {
                sim.DebugOpenCampGate();
                if (bushes > 0) sim.DebugAddBushes(bushes);
            }
            return sim;
        }

        static void AllGatherers(SimCore sim)
        {
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge && x.job != JobType.Toolmaker))
                sim.DebugSetJob(n, JobType.Gatherer);
        }

        static List<PersonActivity> Gathering(SimCore sim, Season season, int days = 6)
        {
            var planner = LifePlanner.Wired(sim);
            planner.ForcedSeason = season;
            var day = new List<PersonActivity>();
            var all = new List<PersonActivity>();
            for (int i = 0; i < days; i++)
                foreach (var who in planner.People) { planner.DaySchedule(who, i * 7 + 1, day); all.AddRange(day); }
            return all;
        }

        [Test]
        public void BerriesFollowTheSeason()
        {
            Assert.AreEqual(BerryState.Flowers, EcoBushes.StateIn(0));
            Assert.AreEqual(BerryState.Berries, EcoBushes.StateIn(1));
            Assert.AreEqual(BerryState.Berries, EcoBushes.StateIn(2));
            Assert.AreEqual(BerryState.Bare, EcoBushes.StateIn(3));
            var sim = Land();
            var cells = new List<int>(); sim.BushCells(cells);
            Assert.IsNotEmpty(cells);
            Assert.AreEqual(0f, sim.BerriesAt(cells[0], 10, 0), "flowers, no fruit");
            Assert.AreEqual(1f, sim.BerriesAt(cells[0], 10, 1), 1e-4f);
            Assert.AreEqual(1f, sim.BerriesAt(cells[0], 10, 2), 1e-4f);
            Assert.AreEqual(0f, sim.BerriesAt(cells[0], 10, 3), "bare in winter");
            int grass = Enumerable.Range(0, EcoLand.N).First(c => sim.EcoCoverAt(c) != EcoCover.Bush);
            Assert.AreEqual(0f, sim.BerriesAt(grass, 10, 1), "not a bush");
        }

        [Test]
        public void PickingLowersTheBerriesThenTheyRecover()
        {
            var sim = Land();
            var cells = new List<int>(); sim.BushCells(cells);
            int c = cells[0];
            float took = sim.PickBush(c, 10);
            Assert.AreEqual(EcoBushes.PickAmount, took, 1e-4f);
            Assert.Less(sim.BerriesAt(c, 10, 1), 1f);
            sim.PickBush(c, 10); sim.PickBush(c, 10);
            Assert.AreEqual(0f, sim.BerriesAt(c, 10, 1), 1e-4f, "stripped");
            float later = sim.BerriesAt(c, 13, 1);
            Assert.Greater(later, 0f); Assert.Less(later, 1f);
            Assert.AreEqual(1f, sim.BerriesAt(c, 10 + 8, 1), 1e-4f, "recovered");
            Assert.AreEqual(1, sim.State.eco.bushPickCell.Count, "kept in the saved state");
        }

        [Test]
        public void GatherersGoToBushCellsThatGrew()
        {
            var sim = Land();
            AllGatherers(sim);
            var cells = new List<int>(); sim.BushCells(cells);
            var gather = Gathering(sim, Season.Summer).Where(a => a.kind == ActivityKind.Gather && !a.moving).ToList();
            Assert.IsNotEmpty(gather);
            Assert.IsTrue(gather.All(a => a.place.kind == PlaceKind.BushCell), "all at bush cells: " + string.Join(",", gather.Select(a => a.place.ToString()).Distinct()));
            Assert.IsTrue(gather.All(a => cells.Contains(a.place.index)), "real grown cells");
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.IsValidPlace(gather[0].place));
        }

        [Test]
        public void WithNoBerriesTheyDoOtherWork_NeverARootDig()
        {
            // #656 (the user, 2026-11-14): this pinned the root dig in the hollow when no bush has fruit; the root dig is gone, so a
            // gatherer with nothing to pick does the camp's other work, never a basket at the hollow.
            var sim = Land();
            AllGatherers(sim);
            foreach (var s in new[] { Season.Spring, Season.Winter })
            {
                var all = Gathering(sim, s);
                Assert.IsNotEmpty(all.Where(a => !a.moving && (a.Group == ActivityGroup.Work || a.Group == ActivityGroup.Chore)), s + ": they keep busy");
                var gather = all.Where(a => a.kind == ActivityKind.Gather && !a.moving && a.prop == Prop.Basket).ToList();
                Assert.IsTrue(gather.All(a => a.place.kind == PlaceKind.BushCell), s + ": a basket only at a bush with fruit");
                Assert.IsFalse(all.Any(a => a.kind == ActivityKind.Gather && a.place.kind == PlaceKind.Stream), s + ": no root dig");
            }
        }

        [Test]
        public void ABushPickedBareIsPassedByForOthers()
        {
            var sim = Land(bushes: 12);
            AllGatherers(sim);
            var g = sim.EcoGround; var near = new List<int>();
            sim.NearestBushes(g.campX, g.campZ, 4, 1, 1, true, near);
            Assert.IsNotEmpty(near);
            foreach (var c in near) { sim.PickBush(c, 1); sim.PickBush(c, 1); sim.PickBush(c, 1); }
            var planner = LifePlanner.Wired(sim);
            planner.ForcedSeason = Season.Summer;
            var day = new List<PersonActivity>();
            var gather = new List<PersonActivity>();
            foreach (var who in planner.People) { planner.DaySchedule(who, 1, day); gather.AddRange(day.Where(a => a.kind == ActivityKind.Gather && !a.moving)); }
            Assert.IsNotEmpty(gather);
            Assert.IsFalse(gather.Any(a => near.Contains(a.place.index)), "the stripped ones are left alone");
        }

        [Test]
        public void NoWoodsTripWithoutTrees()
        {
            var sim = Land();
            AllGatherers(sim);
            Assert.IsFalse(sim.TreesGrow);
            foreach (var s in new[] { Season.Autumn, Season.Winter })
                Assert.IsFalse(Gathering(sim, s).Any(a => a.place.kind == PlaceKind.Woods || a.from.kind == PlaceKind.Woods), s + ": no woods on a young land");
        }

        [Test]
        public void TheFirstBushIsASignThatFiresOnce()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            int seen = 0, cell = -1;
            sim.FirstBushSeen += c => { seen++; cell = c; };
            Assert.AreEqual(-1, sim.FirstBushCell);
            Assert.IsFalse(sim.FirstBushSignActive);
            int first = sim.DebugFirstBushNow();
            Assert.GreaterOrEqual(first, 0);
            Assert.AreEqual(1, seen);
            Assert.AreEqual(first, cell);
            Assert.AreEqual(first, sim.FirstBushCell);
            Assert.IsTrue(sim.FirstBushSignActive);
            Assert.AreEqual(1, sim.State.chronicle.Count(e => e.kind == "eco_bush" || e.kind == "life_thicket"), "one chronicle line (#418: the thicket's placing tells it)");
            sim.DebugFirstBushNow(); sim.DebugAddBushes(5);
            Assert.AreEqual(1, seen, "once");
            Assert.AreEqual(1, sim.State.chronicle.Count(e => e.kind == "eco_bush" || e.kind == "life_thicket"));
        }

        [Test]
        public void OnePersonWalksOverToLookAtTheFirstBush()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            sim.DebugOpenCampGate();
            sim.DebugFirstBushNow();
            var spot = Place.Land(PlaceKind.BushCell, sim.FirstBushCell);
            var looks = Gathering(sim, Season.Summer, days: 1).Where(a => a.kind == ActivityKind.Inspect && a.place == spot).ToList();
            Assert.IsNotEmpty(looks, "someone goes to look");
            Assert.AreEqual(1, looks.Select(a => a.who.Key).Distinct().Count(), "one person");
        }

        [Test]
        public void ChildrenSometimesEatFromABush()
        {
            var sim = Land();
            foreach (var n in sim.State.notables.Where(x => x.alive).Take(3)) n.age = 8f; // some children
            var eat = Gathering(sim, Season.Summer, days: 14).Where(a => a.kind == ActivityKind.Eat && a.place.kind == PlaceKind.BushCell).ToList();
            Assert.IsNotEmpty(eat);
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(eat.All(a => planner.StageOf(a.who) == LifeStage.Kid));
            Assert.IsEmpty(Gathering(sim, Season.Winter, days: 14).Where(a => a.place.kind == PlaceKind.BushCell && a.kind == ActivityKind.Eat), "no fruit in winter");
        }

        [Test]
        public void WithoutTheEcologyNothingChanges()
        {
            var sim = Land(ecology: false);
            Assert.AreEqual(-1, sim.DebugFirstBushNow());
            Assert.AreEqual(0, sim.DebugAddBushes(3));
            Assert.AreEqual(0f, sim.PickBush(5, 1));
            Assert.AreEqual(-1, sim.FirstBushCell);
            AllGatherers(sim);
            var all = Gathering(sim, Season.Summer);
            Assert.IsFalse(all.Any(a => a.place.kind == PlaceKind.BushCell));
            Assert.IsTrue(all.Any(a => a.kind == ActivityKind.Gather && a.place.kind == PlaceKind.Bushes), "the old fixed spot");
        }
    }
}
