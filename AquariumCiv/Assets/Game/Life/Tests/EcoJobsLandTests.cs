using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>P8 #285 and #267: on a living land the jobs open with the land (and never close), the group is "the tribe"
    /// before the camp gate, and the dawn water is muddy from the wet low ground while the pond is dry and clean from the
    /// pond once it has water.</summary>
    public class EcoJobsLandTests
    {
        static SimCore Barren(int seed, bool ecology = true)
        {
            var cfg = new SimConfig { ecology = ecology };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static void Reach(SimCore sim, string key)
        {
            if (!sim.State.eco.Reached(key)) { sim.State.eco.milestoneKeys.Add(key); sim.State.eco.milestoneMonths.Add(sim.State.month); }
        }

        static void MakePond(SimCore sim, int cells = 6)
        {
            for (int c = 0, n = 0; c < EcoLand.N && n < cells; c++)
                if ((EcoCover)sim.State.eco.cover[c] == EcoCover.Bare) { sim.State.eco.cover[c] = (byte)EcoCover.Water; n++; }
        }

        /// <summary>#421: the fish placed (as the Keeper's jar would): the fisher's job waits for them.</summary>
        static void PlaceFish(SimCore sim)
        {
            var j = sim.State.eco.jars.First(x => x.species == nameof(LifeSpecies.Fish));
            j.state = (int)JarState.Placed; j.placings = 1; j.placedMonth = sim.State.month;
            sim.State.eco.fish = 6;
        }

        static void DryLand(SimCore sim)
        {
            for (int c = 0; c < EcoLand.N; c++) sim.State.eco.cover[c] = (byte)EcoCover.Bare;
        }

        [Test]
        public void OnABarrenLandOnlyTheTribesJobsAreOpen()
        {
            var sim = Barren(3);
            var open = sim.OpenJobs();
            TestContext.WriteLine(sim.DebugJobsReadout());
            Assert.IsTrue(open.Contains(JobType.Gatherer), "roots and berries");
            foreach (var j in new[] { JobType.Woodcutter, JobType.Fisher, JobType.Hunter })
                Assert.IsFalse(open.Contains(j), j + " has no land yet");
            Assert.IsTrue(sim.State.notables.Where(n => n.alive && n.age >= 16f).All(n => sim.JobOpen(n.job) || n.job == JobType.Elder), "nobody holds a closed job");
        }

        [Test]
        public void JobsOpenWithTheLand()
        {
            var sim = Barren(3);
            Reach(sim, EcoMilestones.FirstWoodTree);
            Assert.IsTrue(sim.JobOpen(JobType.Woodcutter), "trees");
            Assert.IsFalse(sim.JobOpen(JobType.Fisher));
            Assert.IsFalse(sim.JobOpen(JobType.Hunter));
            Reach(sim, EcoMilestones.FirstRabbit);
            Assert.IsTrue(sim.JobOpen(JobType.Hunter), "rabbits");
            Assert.IsFalse(sim.JobOpen(JobType.Fisher), "no pond yet");
            Reach(sim, EcoMilestones.WaterSurfaced);
            Assert.IsFalse(sim.JobOpen(JobType.Fisher), "#421: a pond, but no fish placed yet");
            PlaceFish(sim);
            Assert.IsTrue(sim.JobOpen(JobType.Fisher), "the fish the Keeper placed");
            StringAssert.Contains("fisher: open", sim.DebugJobsReadout());
        }

        // ---- #493: the Keeper's tree is never cut
        static SimCore KeeperOnly(out int keeperCell)
        {
            var cfg = new SimConfig { ecology = true, settle = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.IsNotNull(sim.Settlement);
            Assert.IsTrue(sim.DebugTreeStage(4));
            keeperCell = sim.State.eco.treeCell;
            sim.Step();
            return sim;
        }

        static void AllWoodcutters(SimCore sim)
        {
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge))
                sim.DebugSetJob(n, JobType.Woodcutter);
        }

        [Test]
        public void TheKeepersGrownTreeAloneOpensNoWoodWork()
        {
            var sim = KeeperOnly(out int cell);
            Assert.AreEqual(EcoCover.Tree, sim.EcoCoverAt(cell));
            Assert.IsFalse(sim.TreesGrow);
            Assert.IsFalse(sim.JobOpen(JobType.Woodcutter));
            AllWoodcutters(sim);
            var planner = LifePlanner.Wired(sim);
            var day = new List<PersonActivity>();
            foreach (var season in new[] { Season.Autumn, Season.Winter })
            {
                planner.ForcedSeason = season;
                foreach (var who in planner.People)
                {
                    planner.DaySchedule(who, 1, day);
                    Assert.IsFalse(day.Any(a => a.kind == ActivityKind.Chop || a.kind == ActivityKind.ChopKindling && a.place.kind is PlaceKind.Woods or PlaceKind.ForestEdge),
                        season + ": nobody chops with only the Keeper's tree standing");
                }
            }
            var pos = new SettlePlacePositions(sim);
            foreach (var kind in new[] { PlaceKind.Woods, PlaceKind.ForestEdge })
                if (pos.TryGetPosition(Place.Land(kind), out float x, out float z))
                    Assert.AreNotEqual(cell, AquariumCiv.Sim.Settle.SiteMap.CellAt(x, z), kind + " is never the Keeper's tree");
        }

        [Test]
        public void OnceAWildTreeGrowsChoppingGoesThereNeverToTheKeepersTree()
        {
            var sim = KeeperOnly(out int cell);
            var eco = sim.State.eco;
            int wild = -1;
            for (int c = 0; c < EcoLand.N && wild < 0; c++)
                if (c != cell && (EcoCover)eco.cover[c] == EcoCover.Bare && Math.Abs(AquariumCiv.Sim.Settle.SiteMap.CXc(c)) < 2.5f && Math.Abs(AquariumCiv.Sim.Settle.SiteMap.CZc(c)) < 1.7f) wild = c; // inside the glass margin
            Assert.GreaterOrEqual(wild, 0);
            eco.cover[wild] = (byte)EcoCover.Tree;
            for (int m = 0; m < 12 && !sim.TreesGrow; m++) sim.Step(); // the ecology steps once a season
            Assert.AreEqual(EcoCover.Tree, sim.EcoCoverAt(wild));
            Assert.IsTrue(sim.TreesGrow, "a wild tree stands");
            Assert.IsTrue(sim.JobOpen(JobType.Woodcutter));
            var pos = new SettlePlacePositions(sim);
            Assert.IsTrue(pos.TryGetPosition(Place.Land(PlaceKind.ForestEdge), out float x, out float z));
            Assert.AreEqual(wild, AquariumCiv.Sim.Settle.SiteMap.CellAt(x, z), "the edge is the wild tree");
        }

        [Test]
        public void AJobThatOpenedNeverCloses()
        {
            var sim = Barren(3);
            Reach(sim, EcoMilestones.FirstWoodTree); Reach(sim, EcoMilestones.WaterSurfaced); Reach(sim, EcoMilestones.FirstRabbit); PlaceFish(sim);
            Assert.IsTrue(sim.JobOpen(JobType.Woodcutter) && sim.JobOpen(JobType.Fisher) && sim.JobOpen(JobType.Hunter));
            DryLand(sim);
            for (int m = 0; m < 6; m++) sim.Step();
            Assert.IsTrue(sim.JobOpen(JobType.Woodcutter), "the trees are not taken back");
            Assert.IsTrue(sim.JobOpen(JobType.Fisher));
            Assert.IsTrue(sim.JobOpen(JobType.Hunter));
        }

        [Test]
        public void TheCardsSayTheRootDiggerUntilABushComesUp()
        {
            var sim = Barren(3);
            Assert.AreEqual("root digger", sim.JobLabel(JobType.Gatherer));
            Reach(sim, EcoMilestones.FirstBush);
            Assert.AreEqual("gatherer", sim.JobLabel(JobType.Gatherer));
            Assert.AreEqual("fisher", sim.JobLabel(JobType.Fisher));
        }

        [Test]
        public void BeforeTheCampTheGroupIsTheTribe()
        {
            var sim = Barren(3);
            var n = sim.State.notables.First(x => x.alive && x.age >= 16f);
            Assert.IsTrue(sim.BeforeCamp);
            StringAssert.EndsWith("chief of the tribe", sim.CardRoleLine(n, RoleType.Chief));
            Assert.IsFalse(sim.State.chronicle.Any(e => e.text.Contains("of the camp")), "the chronicle says tribe: " +
                string.Join(" | ", sim.State.chronicle.Where(e => e.text.Contains("camp")).Select(e => e.text)));
            sim.DebugOpenCampGate();
            StringAssert.EndsWith("chief of the camp", sim.CardRoleLine(n, RoleType.Chief));
        }

        [Test]
        public void WithoutTheEcologyNothingChanges()
        {
            var sim = Barren(3, ecology: false);
            Assert.IsTrue(sim.JobOpen(JobType.Fisher) && sim.JobOpen(JobType.Woodcutter) && sim.JobOpen(JobType.Hunter));
            Assert.AreEqual("gatherer", sim.JobLabel(JobType.Gatherer));
            Assert.IsTrue(sim.OpenWater);
            var n = sim.State.notables.First(x => x.alive && x.age >= 16f);
            StringAssert.EndsWith("chief of the camp", sim.CardRoleLine(n, RoleType.Chief));
        }

        // ---------------------------------------------------------------- the planner

        static List<PersonActivity> Water(SimCore sim, int days = 12)
        {
            var planner = LifePlanner.Wired(sim);
            var all = new List<PersonActivity>();
            var day = new List<PersonActivity>();
            for (int i = 0; i < days; i++)
                foreach (var who in planner.People)
                {
                    planner.DaySchedule(who, i * 7, day);
                    all.AddRange(day.Where(a => a.prop == Prop.Buckets || a.prop == Prop.EmptyBucket));
                }
            return all;
        }

        [Test]
        public void WhileThePondIsDryTheBucketsComeBackMuddyFromTheLowGround()
        {
            var sim = Barren(5);
            Assert.AreEqual(0, sim.EcoPondCells);
            var buckets = Water(sim);
            Assert.IsNotEmpty(buckets, "they fetch water");
            Assert.IsTrue(buckets.All(a => a.water == WaterKind.Muddy), "muddy");
            Assert.IsTrue(buckets.Where(a => a.prop == Prop.Buckets && a.kind == ActivityKind.FetchWater).All(a => a.from.kind == PlaceKind.Stream),
                "from the wet low ground");
        }

        [Test]
        public void OnceThePondHasWaterTheTripGoesToThePondAndTheWaterIsClean()
        {
            var sim = Barren(5);
            MakePond(sim);
            Assert.Greater(sim.EcoPondCells, 0);
            var buckets = Water(sim);
            Assert.IsNotEmpty(buckets);
            Assert.IsTrue(buckets.All(a => a.water == WaterKind.Clean), "clean");
            var full = buckets.Where(a => a.prop == Prop.Buckets && a.kind == ActivityKind.FetchWater && a.moving).ToList();
            Assert.IsNotEmpty(full);
            Assert.IsTrue(full.All(a => a.from.kind == PlaceKind.Pond), "from the pond, not the dry stream bed");
        }

        [Test]
        public void WithoutTheEcologyTheWaterStaysClean()
        {
            var sim = Barren(5, ecology: false);
            var buckets = Water(sim);
            Assert.IsNotEmpty(buckets);
            Assert.IsTrue(buckets.All(a => a.water == WaterKind.Clean));
        }

        static List<PersonActivity> WorkOf(SimCore sim)
        {
            var planner = LifePlanner.Wired(sim);
            var day = new List<PersonActivity>();
            var work = new List<PersonActivity>();
            for (int i = 0; i < 6; i++)
                foreach (var who in planner.People) { planner.DaySchedule(who, i * 7, day); work.AddRange(day.Where(a => a.Group == ActivityGroup.Work)); }
            return work;
        }

        [Test]
        public void AHunterHasTribeWorkUntilTheHuntIsBuilt()
        {
            var sim = Barren(3);
            Reach(sim, EcoMilestones.FirstRabbit);
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge)) sim.DebugSetJob(n, JobType.Hunter);
            sim.DebugOpenCampGate();
            var work = WorkOf(sim);
            Assert.IsNotEmpty(work);
            Assert.IsFalse(work.Any(a => a.kind == ActivityKind.Stalk), "no hunt yet (#283)");
        }

        [Test]
        public void ANoTreeLandDoesNotChop()
        {
            var sim = Barren(3);
            sim.DebugOpenCampGate();
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge)) sim.DebugSetJob(n, JobType.Woodcutter);
            Assert.IsFalse(WorkOf(sim).Any(a => a.kind == ActivityKind.Chop), "no trees, no chopping");
        }
    }
}
