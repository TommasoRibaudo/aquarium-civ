using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#656/#677 (the user, 2026-11-14), the planner's half: no root dig ever (the people live off the carried basket until
    /// the land gives); before any bush there is no fire to tend; the grass-and-twig fire is fed with dry grass and twigs gathered
    /// at the meadow's edge, never with wood; logs only after the first wild wood.</summary>
    public class NewStartPlanTests
    {
        static SimCore NewBox(int seed)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static void Reach(SimCore sim, string key)
        {
            if (sim.State.eco.Reached(key)) return;
            sim.State.eco.milestoneKeys.Add(key); sim.State.eco.milestoneMonths.Add(sim.State.month);
        }

        /// <summary>Every activity of everyone over <paramref name="days"/> days spread over a year (all seasons).</summary>
        static List<PersonActivity> Plan(SimCore sim, int days = 16)
        {
            var planner = LifePlanner.Wired(sim);
            var all = new List<PersonActivity>();
            var day = new List<PersonActivity>();
            for (int i = 0; i < days; i++)
                foreach (var who in planner.People)
                {
                    planner.DaySchedule(who, i * 23, day);
                    all.AddRange(day);
                }
            return all;
        }

        static bool RootDig(PersonActivity a) => a.kind == ActivityKind.Gather && a.place.kind == PlaceKind.Stream;

        /// <summary>#656/#677 (view pass): hunger is shown, never punished: with the basket empty every walk is timed
        /// <see cref="LifePlanner.HungryPace"/> longer (the day's fixed hours absorb much of it); the view adds the looks at the sky.
        /// An old box or the old slice never slows.</summary>
        [Test]
        public void Hungry_TheirWalksAreTimedSlower()
        {
            var sim = NewBox(7);
            Assert.IsFalse(sim.Hungry, "the basket is full at the start");
            Assert.AreEqual(1f, LifePlanner.Wired(sim).WalkPace, 1e-6f);
            sim.DebugEmptyBasket();
            Assert.IsTrue(sim.Hungry, "the food is gone");
            Assert.AreEqual(LifePlanner.HungryPace, LifePlanner.Wired(sim).WalkPace, 1e-6f, "hungry: slower");
            Assert.That(LifePlanner.HungryPace, Is.InRange(1.1f, 1.5f), "a little slower, not a crawl");
            var old = new SimCore(new SimConfig(), SimCore.NewWorld(7, new SimConfig()));
            old.State.food = 0f;
            Assert.AreEqual(1f, LifePlanner.Wired(old).WalkPace, 1e-6f, "the old slice never slows");
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        public void AFreshBox_PlansNoRootDig_AndNoFireChores(int seed)
        {
            var sim = NewBox(seed);
            Assert.IsTrue(sim.BeforeCamp && !sim.BerriesGrow, "a fresh barren box");
            var plan = Plan(sim);
            Assert.IsNotEmpty(plan.Where(a => a.Group == ActivityGroup.Work || a.Group == ActivityGroup.Chore), "they still keep busy");
            Assert.IsFalse(plan.Any(RootDig), "nobody digs roots at the hollow");
            Assert.IsFalse(plan.Any(LifePlanner.IsTinderGather), "no fire yet: nothing to gather for it");
            Assert.IsFalse(plan.Any(a => a.kind == ActivityKind.ChopKindling || a.kind == ActivityKind.StackWood || a.kind == ActivityKind.Chop), "no wood work without wild wood");
            Assert.IsFalse(plan.Any(a => a.kind == ActivityKind.Cook && a.place.kind == PlaceKind.Fire && a.start >= 17f), "no evening pot over a fire that isn't lit");

            // two years on, no Keeper: still no root dig, whatever the land did
            for (int m = 0; m < 24; m++) sim.Step();
            Assert.IsFalse(Plan(sim).Any(RootDig), "no root dig two years on either");
        }

        [TestCase(1)]
        [TestCase(7)]
        public void TheGrassFire_IsFedWithGrassAndTwigs_NeverWood(int seed)
        {
            var sim = NewBox(seed);
            sim.DebugFireStage((int)CampFireStage.GrassAndTwig);
            Assert.AreEqual(CampFireStage.GrassAndTwig, sim.FireStage);
            var plan = Plan(sim, 24);
            var tinder = plan.Where(LifePlanner.IsTinderGather).ToList();
            Assert.IsNotEmpty(tinder, "someone gathers dry grass and twigs for the fire");
            Assert.IsTrue(tinder.All(a => a.place.kind == PlaceKind.Meadow), "at the meadow's edge");
            Assert.IsTrue(plan.Where(a => a.kind == ActivityKind.Carry && a.prop == Prop.Stick).All(a => a.place.kind == PlaceKind.Fire || a.place.kind == PlaceKind.Stash),
                "carried to the fire (or set down on the pile beside it at the day's end)");
            Assert.IsFalse(plan.Any(a => a.kind == ActivityKind.ChopKindling || a.kind == ActivityKind.StackWood), "never kindling, never wood");
            Assert.IsFalse(plan.Any(RootDig));
            var planner = LifePlanner.Wired(sim);
            var g = tinder[0];
            string said = planner.Describe(g);
            StringAssert.Contains("dry grass and twigs", said);
            StringAssert.DoesNotContain("wood", said);
        }

        [Test]
        public void WithBerriesInWinter_NobodyDigsRootsEither()
        {
            var sim = NewBox(7);
            sim.DebugOpenCampGate();
            Reach(sim, EcoMilestones.FirstBush); // the berries' job opens, but a bush may have no fruit today
            Reach(sim, EcoMilestones.FirstRabbit);
            sim.Step();
            sim.DebugRedrawJobs();
            Assert.IsTrue(sim.State.notables.Any(n => n.alive && n.job == JobType.Gatherer), "gatherers now");
            Assert.IsFalse(Plan(sim, 24).Any(RootDig), "a gatherer with no berries today does other work, not a root dig");
        }

        [TestCase(1)]
        [TestCase(7)]
        public void TheDeadfall_IsGatheredUnderTheTree_NeverChopped(int seed)
        {
            var sim = NewBox(seed);
            sim.DebugOpenCampGate();
            Reach(sim, EcoMilestones.FirstBush);
            StringAssert.Contains("beside the Keeper's tree", sim.DebugWildTrees(true));
            Assume.That(sim.WoodsTreeCount, Is.EqualTo(0));
            sim.Step();
            Assert.AreEqual(CampFireStage.Logs, sim.FireStage, "the deadfall is the first wild wood");
            var plan = Plan(sim, 24);
            var df = plan.Where(LifePlanner.IsDeadfallGather).ToList();
            Assert.IsNotEmpty(df, "someone gathers the fallen branches");
            Assert.IsTrue(df.All(a => sim.EcoCoverAt(a.place.index) == EcoCover.Tree && a.place.index != sim.State.eco.treeCell), "under a wild tree, never the Keeper's");
            Assert.IsFalse(plan.Any(a => a.kind == ActivityKind.Chop), "nothing is chopped near the Keeper's tree (#573)");
            Assert.IsFalse(df.Any(LifePlanner.IsTinderGather));
            Assert.IsTrue(plan.Any(a => a.kind == ActivityKind.Carry && a.prop == Prop.Log), "the branches carried home");
            var planner = LifePlanner.Wired(sim);
            StringAssert.Contains("fallen branches", planner.Describe(df[0]));
            Assert.IsTrue(planner.IsValidPlace(df[0].place));
        }

        [Test]
        public void TheFirstWildWood_BringsTheLogFire_AndItsChores()
        {
            var sim = NewBox(7);
            sim.DebugOpenCampGate();
            Reach(sim, EcoMilestones.FirstBush);
            sim.DebugFirstWildWoodNow();
            Assert.AreEqual(CampFireStage.Logs, sim.FireStage);
            sim.Step();
            sim.DebugRedrawJobs();
            var plan = Plan(sim, 24);
            Assert.IsFalse(plan.Any(LifePlanner.IsTinderGather), "a log fire is not fed by hand with grass");
            Assert.IsFalse(plan.Any(RootDig));
        }
    }
}
