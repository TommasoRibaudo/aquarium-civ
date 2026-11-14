using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #656/#677 (the user, 2026-11-14): a new living-land box starts with the carried stock (a basket of food, a bundle of sticks that
    /// is not wood), set down by the arrival; wood comes only from grown wild trees (never the Keeper's tree); the fire goes none →
    /// grass and twigs → logs; hunger is shown, never punished; the gatherer's job (and no root dig) waits for food on the land.
    /// </summary>
    public class NewStartTests
    {
        static SimCore NewBox(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static void Reach(SimCore sim, string key)
        {
            if (sim.State.eco.Reached(key)) return;
            sim.State.eco.milestoneKeys.Add(key); sim.State.eco.milestoneMonths.Add(sim.State.month);
        }

        [Test]
        public void ANewBox_CarriesItsBasketIn_AndHasNoUsableWood()
        {
            var sim = NewBox();
            Assert.IsTrue(sim.NewStartOn);
            Assert.AreEqual(SimCore.CarriedFood, sim.State.food, 1e-4f, "the carried basket is the food stock");
            Assert.AreEqual(0f, sim.State.wood, "the carried sticks are a bundle, not wood");
            Assert.Greater(SimCore.CarriedSticks, 0, "the bundle has sticks to show");
            Assert.AreEqual(CampFireStage.None, sim.FireStage, "nothing to burn on a barren box: no fire");

            // the old slice (living land off) keeps its old stock and its fire
            var old = new SimCore(new SimConfig(), SimCore.NewWorld(7, new SimConfig()));
            Assert.IsFalse(old.NewStartOn);
            Assert.AreEqual(20f, old.State.food); Assert.AreEqual(4f, old.State.wood);
            Assert.IsTrue(old.StockSetDown);
            Assert.AreEqual(CampFireStage.Logs, old.FireStage);
        }

        [Test]
        public void TheMuseumsNextBox_CarriesItsBasketInToo()
        {
            var cfg = new SimConfig { ecology = true };
            var first = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            var next = SimCore.SeedNextBox(cfg, first, 99, 1, null, out _);
            Assert.IsTrue(next.NewStartOn);
            Assert.AreEqual(SimCore.CarriedFood, next.State.food, 1e-4f);
            Assert.AreEqual(0f, next.State.wood);
            Assert.IsFalse(next.StockSetDown);
        }

        [Test]
        public void TheStockShows_OnlyAfterTheArrivalSetsItDown()
        {
            var sim = NewBox();
            Assert.IsFalse(sim.StockSetDown, "the HUD hides the numbers while the basket is still carried");
            Assert.AreEqual(-1, sim.StockSetDownMonth);
            sim.Step();
            Assert.IsFalse(sim.StockSetDown, "the months alone don't set it down: the arrival does");
            Assert.IsTrue(sim.SetDownStock());
            Assert.IsTrue(sim.StockSetDown);
            Assert.AreEqual(sim.State.month, sim.StockSetDownMonth);
            Assert.IsFalse(sim.SetDownStock(), "once");

            sim.DebugArrivalAgain();
            Assert.IsFalse(sim.StockSetDown, "the cheat carries it in again");
            sim.DebugOpenCampGate();
            sim.Step();
            Assert.IsTrue(sim.StockSetDown, "a camp that stands has its stock down (the safety net)");
        }

        [Test]
        public void NoWoodComes_BeforeTheFirstWildTree()
        {
            var sim = NewBox();
            for (int m = 0; m < 36; m++)
            {
                sim.Step();
                if (sim.WildWoodTreeCount > 0) break;
                Assert.AreEqual(0f, sim.State.wood, $"month {sim.State.month}: no wood without a grown wild tree");
            }
            Assert.AreEqual(-1, sim.FirstWildWoodMonth);
            Assert.AreNotEqual(CampFireStage.Logs, sim.FireStage);
        }

        [Test]
        public void TheKeepersTree_NeverGivesWood()
        {
            var sim = NewBox();
            Assert.IsTrue(sim.DebugTreeStage(4), "the Keeper's tree, grown");
            Assert.AreEqual(0, sim.WoodsTreeCount, "the Keeper's tree is not the woods");
            Assert.AreEqual(0, sim.DeadfallTreeCount, "nor deadfall");
            for (int m = 0; m < 24; m++)
            {
                sim.Step();
                if (sim.WildWoodTreeCount > 0) break; // a wild tree grew by itself: past what this test is about
                Assert.AreEqual(0f, sim.State.wood, $"month {sim.State.month}: the Keeper's grown tree gives no wood, not even dropped branches");
            }
            Assert.IsFalse(sim.FirstWildWoodReached);
        }

        [Test]
        public void AGrownWildTree_GivesWood_AndTheFirstLogsAreAMomentOnce()
        {
            var sim = NewBox();
            int raised = 0;
            sim.FirstWildWood += () => raised++;
            StringAssert.Contains("grown wild tree", sim.DebugWildTrees(false, 2));
            Assert.Greater(sim.WoodsTreeCount, 0);
            sim.Step();
            Assert.Greater(sim.State.wood, 0f, "wood from the wild trees");
            Assert.IsTrue(sim.FirstWildWoodReached);
            Assert.IsTrue(sim.FirstWildWoodPending, "the view plays the first logs");
            Assert.AreEqual(CampFireStage.Logs, sim.FireStage, "the fire is rebuilt with logs");
            Assert.AreEqual(1, raised);
            Assert.IsTrue(sim.State.chronicle.Any(c => c.kind == "first_wild_wood"));
            sim.MarkFirstWildWoodSeen();
            Assert.IsFalse(sim.FirstWildWoodPending);
            float before = sim.State.wood;
            sim.Step();
            Assert.Greater(sim.State.wood, before, "the wood keeps coming");
            Assert.AreEqual(1, raised, "the first logs come once");
        }

        [Test]
        public void AWildTreeBesideTheKeepersTree_GivesDeadfall_TheFirstWoodToo()
        {
            var sim = NewBox();
            StringAssert.Contains("beside the Keeper's tree", sim.DebugWildTrees(true));
            Assert.AreEqual(0, sim.WoodsTreeCount, "too near the Keeper's tree to be the woods (#573: never chopped)");
            Assert.Greater(sim.DeadfallTreeCount, 0, "its fallen branches can be gathered");
            int cell = sim.DeadfallTreeCell(0f, 0f);
            Assert.AreNotEqual(sim.State.eco.treeCell, cell, "never the Keeper's own tree");
            sim.Step();
            Assert.Greater(sim.State.wood, 0f, "deadfall wood");
            Assert.LessOrEqual(sim.State.wood, sim.DeadfallTreeCount * SimCore.DeadfallPerTree * 1.2f * 1.5f + 1e-3f, "at the deadfall rate");
            Assert.IsTrue(sim.FirstWildWoodReached, "the first wild wood comes from either source");
            Assert.AreEqual(CampFireStage.Logs, sim.FireStage);
        }

        [Test]
        public void TheWildWood_IsCappedByThePeoplesHands()
        {
            var sim = NewBox();
            sim.DebugWildTrees(false, 12);
            int trees = sim.WoodsTreeCount;
            Assume.That(trees, Is.GreaterThan(4), "room for a wood");
            float mult = sim.OutputMultiplier;
            float before = sim.State.wood;
            sim.Step();
            float hands = 1f + sim.State.population * 0.15f; // no lumber mill yet
            Assert.LessOrEqual(sim.State.wood - before, hands * 1.2f * 1.5f + 1e-3f, "no more than the hands can cut (mood pace and the lean at most)");
            Assert.LessOrEqual(sim.State.wood - before, trees * SimCore.WildWoodPerTree * 1.2f * 1.5f + 1e-3f);
            TestContext.WriteLine($"{trees} trees, mult {mult:0.00}: +{sim.State.wood - before:0.00} wood");
        }

        [Test]
        public void TheBasketRunsDown_ButNobodyStarves()
        {
            var sim = NewBox();
            int startPop = sim.State.population;
            float last = sim.State.food;
            bool hungrySeen = false;
            for (int m = 0; m < 48 && !sim.LandGivesFood; m++)
            {
                sim.Step();
                if (sim.LandGivesFood) break;
                Assert.GreaterOrEqual(sim.State.food, 0f, "food never below 0");
                Assert.LessOrEqual(sim.State.food, last + 1e-4f, "the basket only runs down until the land gives");
                last = sim.State.food;
                hungrySeen |= sim.Hungry;
            }
            Assert.GreaterOrEqual(sim.State.population, startPop, "nobody dies of hunger");

            // an empty basket over a long while (the cheat): shown, never punished
            sim.DebugEmptyBasket();
            Assert.IsTrue(sim.Hungry);
            int alive = sim.State.notables.Count(n => n.alive);
            for (int m = 0; m < 24; m++)
            {
                sim.DebugEmptyBasket();
                sim.Step();
                Assert.GreaterOrEqual(sim.State.food, 0f);
                Assert.GreaterOrEqual(sim.State.population, 5, "the box never fails");
            }
            Assert.That(sim.State.notables.Count(n => n.alive), Is.GreaterThanOrEqualTo(alive - 1), "at most old age in two years, no hunger deaths");
            TestContext.WriteLine($"hungry seen before the land gave: {hungrySeen}");
        }

        [Test]
        public void TheLandsFirstFood_IsTheBasketsFirstRefill()
        {
            var sim = NewBox();
            Assert.IsFalse(sim.LandGivesFood);
            sim.Step();
            float down = sim.State.food;
            Assert.Less(down, SimCore.CarriedFood, "the basket ran down a little");
            Reach(sim, EcoMilestones.FirstBush);
            Assert.IsTrue(sim.LandGivesFood);
            sim.Step();
            Assert.AreEqual(sim.State.month, sim.LandFoodMonth, "the month the land first gave");
            Assert.Greater(sim.State.food, down, "the first refill");
        }

        [Test]
        public void TheFire_GoesFromNone_ToGrassAndTwigs_ToLogs()
        {
            var sim = NewBox();
            Assert.AreEqual(CampFireStage.None, sim.FireStage, "no bush: nothing to burn, they huddle");
            Reach(sim, EcoMilestones.FirstBush);
            Assert.AreEqual(CampFireStage.GrassAndTwig, sim.FireStage, "the first bush: a grass and twig fire");
            sim.DebugWildTrees(false, 1);
            sim.Step();
            Assert.AreEqual(CampFireStage.Logs, sim.FireStage, "the first wild wood: a log fire");

            sim.DebugFireStage(1);
            Assert.AreEqual(CampFireStage.GrassAndTwig, sim.FireStage, "the cheat holds a stage");
            sim.DebugFireStage(0);
            Assert.AreEqual(CampFireStage.None, sim.FireStage);
            sim.DebugFireStage(-1);
            Assert.AreEqual(CampFireStage.Logs, sim.FireStage, "the land decides again");
        }

        [Test]
        public void TheFirstWildWoodCheat_GrowsATreeAndBringsTheLogsNow()
        {
            var sim = NewBox();
            string said = sim.DebugFirstWildWoodNow();
            TestContext.WriteLine(said);
            Assert.IsTrue(sim.FirstWildWoodReached, said);
            Assert.IsTrue(sim.FirstWildWoodPending);
            Assert.Greater(sim.State.wood, 0f);
            Assert.AreEqual(CampFireStage.Logs, sim.FireStage);
            StringAssert.Contains("already", sim.DebugFirstWildWoodNow(), "once");
        }

        [Test]
        public void OnBareLand_TheGatherersJobWaits_OneMakesTools_NobodyDigsRoots()
        {
            var sim = NewBox();
            Assert.IsFalse(sim.JobOpen(JobType.Gatherer), "no food on the land: no gatherer");
            Assert.IsTrue(sim.NoWorkYet);
            var adults = sim.State.notables.Where(n => n.alive && n.age >= 16f && n.age < sim.Config.retireAge).ToList();
            Assert.AreEqual(1, adults.Count(n => n.job == JobType.Toolmaker), "one founder makes tools");
            Assert.IsTrue(adults.Where(n => n.job != JobType.Toolmaker).All(n => n.job == JobType.None), "the others wait for the land");
            Assert.IsFalse(sim.State.notables.Any(n => sim.JobLabel(n.job ?? "") == "root digger"));

            Reach(sim, EcoMilestones.FirstBush);
            sim.Step();
            Assert.IsTrue(sim.JobOpen(JobType.Gatherer), "berries on the land: the gatherer's job opens");
            Assert.IsFalse(sim.NoWorkYet);
            Assert.IsTrue(sim.State.notables.Any(n => n.alive && n.job == JobType.Gatherer), "and is taken");
            Assert.IsFalse(sim.State.notables.Any(n => n.alive && n.age >= 16f && n.age < sim.Config.retireAge && string.IsNullOrEmpty(n.job)), "everyone has work now");
        }

        [Test]
        public void AnOldLivingLandSave_PlaysAsBefore()
        {
            var sim = NewBox();
            sim.State.newStart = new NewStartState(); // what JsonUtility gives a save from before #656: off
            Assert.IsFalse(sim.NewStartOn);
            Assert.IsTrue(sim.StockSetDown);
            Assert.AreEqual(CampFireStage.Logs, sim.FireStage);
            float before = sim.State.wood;
            sim.Step();
            Assert.Greater(sim.State.wood, before, "the old wood income");
        }
            // ---------- #656/#677 (tester 2026-11-14): the chronicle's stories fit the living land ----------
        static readonly string[] NeverOnALivingLand = { "stream", "goat", "hens", "fox", "kitten", "wet dog", "the hut", "fence post", "apple", "lumber mill" };

        /// <summary>A living-land camp with no pond, no wood and no animals placed, told stories for decades: never a stream, goats, hens or a
        /// hut; the pond only once there is one; firewood or the woodpile only once a wild tree gives wood. The dice are untouched (the
        /// swap is on the finished line), so a second box with the ecology off tells the old words.</summary>
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(1656)]
        public void ALivingLandCamp_TellsStoriesThatFitItsLand(int seed)
        {
            var sim = NewBox(seed);
            int seen = 0, told = 0;
            for (int m = 0; m < 12 * 40; m++)
            {
                if (m % 3 == 0) sim.ForceStory(); // many more stories than a box tells
                sim.Step();
                bool water = sim.OpenWater, wood = sim.WildWoodTreeCount > 0 || sim.FirstWildWoodReached;
                for (; seen < sim.State.chronicle.Count; seen++)
                {
                    string t = sim.State.chronicle[seen].text; told++;
                    foreach (var w in NeverOnALivingLand) StringAssert.DoesNotContain(w, t, $"month {sim.State.month}: \"{t}\"");
                    if (!water) StringAssert.DoesNotContain("pond", t, $"no pond yet, month {sim.State.month}: \"{t}\"");
                    if (!wood) { StringAssert.DoesNotContain("firewood", t, $"no wood yet: \"{t}\""); StringAssert.DoesNotContain("woodpile", t, $"no wood yet: \"{t}\""); }
                    if (sim.State.era == Era.Camp) StringAssert.DoesNotContain(" hut", t, $"a camp has tents: \"{t}\"");
                }
            }
            Assert.That(told, Is.GreaterThan(60), "plenty of stories were told");
        }

        [Test]
        public void TheLandWords_SwapOnlyOnALivingLand()
        {
            var sim = NewBox();
            Assert.AreEqual("A rainbow arched over the glass. Nobody worked that afternoon.", sim.LandWords("A rainbow arched over the pond. Nobody worked that afternoon."));
            Assert.AreEqual("A and B fell out over the warmest spot in the tent. They no longer share a bench.", sim.LandWords("A and B fell out over a goat. They no longer share a bench."));
            StringAssert.Contains("sulks by the tents", sim.LandWords("B sulks by the woodpile."));
            var old = new SimCore(new SimConfig(), SimCore.NewWorld(7, new SimConfig()));
            Assert.AreEqual("A rainbow arched over the pond.", old.LandWords("A rainbow arched over the pond."), "the old slice keeps its words");
        }
}
}
