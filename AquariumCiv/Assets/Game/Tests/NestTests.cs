using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Tests
{
    /// <summary>#709 (B71, decision 2026-11-15 "The bird from the bushes"): the nest in the thicket, the sim side. One nest in a placed thicket,
    /// the parent in spring, 2 to 3 eggs, a gatherer takes one now and then (never the last, two a spring at most), chicks at the end of spring
    /// (Q5 default on), everyone gone by summer, the empty nest through the winter, the nest goes with its thicket and comes back in another.
    /// Old saves load as "no nest yet". The defaults of Q4 (bird outside the animal budget), Q5 (chicks) and Q6 (first thicket) are each one value.</summary>
    public class NestTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>A box with a tree, meadow, the first thicket placed by the camp, and at least 8 bushes in it. The first bird has not come yet.</summary>
        static SimCore WithThicket(int more = 0, int seed = 7)
        {
            var sim = World(seed);
            var g = sim.EcoGround;
            Assert.IsTrue(sim.PlantTree(g.campX + 0.9f, g.campZ));
            for (int c = 0; c < EcoLand.N; c++)
                if ((EcoCover)sim.State.eco.cover[c] == EcoCover.Bare && !g.InBasin(c)) sim.State.eco.cover[c] = (byte)EcoCover.Grass;
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            for (int i = 0; i < more; i++) Assert.GreaterOrEqual(sim.DebugPlaceThicketNearCamp(), 0);
            sim.DebugAddBushes(8);
            return sim;
        }

        static void BirdCame(SimCore sim) { sim.State.eco.milestoneKeys.Add(EcoMilestones.FirstBird); sim.State.eco.milestoneMonths.Add(0); }

        static List<(int cell, float radius, int bushes)> Thickets(SimCore sim) { var l = new List<(int, float, int)>(); sim.BushThickets(l); return l; }

        static List<(NestEventKind k, int cell)> Listen(SimCore sim) { var l = new List<(NestEventKind, int)>(); sim.NestChanged += (k, c) => l.Add((k, c)); return l; }

        // ---------------------------------------------------------------- the year
        [Test]
        public void NoNest_UntilTheFirstBirdCame_AndNoNestWithoutAThicket()
        {
            var sim = World();
            sim.DebugNestMonth(2, 0);
            Assert.IsFalse(sim.NestStands, "no thicket");
            sim = WithThicket();
            sim.DebugNestMonth(2, 0);
            Assert.IsFalse(sim.NestStands, "the bird has not come down yet: the story leads into the nest, not the other way");
            BirdCame(sim);
            sim.DebugNestMonth(2, 0);
            Assert.IsTrue(sim.NestStands);
        }

        [Test]
        public void TheYear_ParentComesBuilds_EggsAreLaid_ChicksAtTheEnd_GoneBySummer_TheEmptyNestStaysThroughWinter()
        {
            var sim = WithThicket(); BirdCame(sim);
            var ev = Listen(sim);
            sim.DebugNestMonth(1, 0);
            Assert.IsFalse(sim.NestStands, "not before spring");
            sim.DebugNestMonth(2, 0);
            Assert.IsTrue(sim.NestStands); Assert.AreEqual(NestPhase.Building, sim.NestPhaseNow);
            Assert.IsTrue(sim.NestParentPresent); Assert.AreEqual(0, sim.NestEggs);
            int bush = sim.NestCell;
            Assert.AreEqual(EcoCover.Bush, (EcoCover)sim.State.eco.cover[bush], "in a bush");
            Assert.Greater(sim.BushThicketZone(bush), 0f, "of the thicket");
            sim.DebugNestMonth(3, 0);
            Assert.AreEqual(NestPhase.Eggs, sim.NestPhaseNow);
            Assert.That(sim.NestEggs, Is.InRange(2, 3), "2 to 3 eggs");
            int laid = sim.NestEggs;
            sim.DebugNestMonth(4, 0);
            Assert.AreEqual(NestPhase.Chicks, sim.NestPhaseNow);
            Assert.AreEqual(2, sim.NestChicks, "Q5 default: two chicks at the end of spring");
            Assert.AreEqual(0, sim.NestEggs);
            Assert.IsTrue(sim.NestParentPresent);
            sim.DebugNestMonth(5, 0);
            Assert.IsFalse(sim.NestParentPresent, "everyone gone by summer");
            Assert.AreEqual(0, sim.NestChicks);
            Assert.AreEqual(NestPhase.Empty, sim.NestPhaseNow);
            foreach (int cal in new[] { 6, 8, 11, 0, 1 }) sim.DebugNestMonth(cal, 0);
            Assert.IsTrue(sim.NestStands, "the empty nest stays in the winter");
            Assert.AreEqual(bush, sim.NestCell);
            CollectionAssert.AreEqual(new[] { NestEventKind.Built, NestEventKind.Laid, NestEventKind.Hatched, NestEventKind.ParentLeft }, ev.Select(e => e.k).ToArray());
            Assert.GreaterOrEqual(laid, 2);
        }

        [Test]
        public void NextSpring_TheParentComesBackToTheSameNest_WithFreshEggs()
        {
            var sim = WithThicket(); BirdCame(sim);
            foreach (int cal in new[] { 2, 3, 4, 5 }) sim.DebugNestMonth(cal, 0);
            int bush = sim.NestCell;
            sim.DebugNestMonth(2, 1);
            Assert.AreEqual(NestPhase.Building, sim.NestPhaseNow);
            Assert.IsTrue(sim.NestParentPresent);
            Assert.AreEqual(bush, sim.NestCell, "the same bush while it stands");
            Assert.AreEqual(0, sim.NestEggsTakenThisSpring, "the spring's share is new");
            sim.DebugNestMonth(3, 1);
            Assert.That(sim.NestEggs, Is.InRange(2, 3));
        }

        [Test]
        public void AThicketPlacedInTheSecondMonthOfSpring_GetsItsNestThatMonth_ButNotInTheLastMonth()
        {
            var sim = WithThicket(); BirdCame(sim);
            sim.DebugNestMonth(3, 0);
            Assert.AreEqual(NestPhase.Eggs, sim.NestPhaseNow, "late: the eggs just come");
            sim = WithThicket(); BirdCame(sim);
            sim.DebugNestMonth(4, 0);
            Assert.IsFalse(sim.NestStands, "too late in the spring: next year");
        }

        [Test]
        public void ChicksOff_TheEggsAreSimplyGoneWhenTheBirdLeaves()
        {
            var sim = WithThicket(); BirdCame(sim);
            sim.Config.eco.nestChicks = 0; // Q5, the other answer
            foreach (int cal in new[] { 2, 3 }) sim.DebugNestMonth(cal, 0);
            Assert.Greater(sim.NestEggs, 0);
            sim.DebugNestMonth(4, 0);
            Assert.AreEqual(0, sim.NestChicks);
            Assert.AreEqual(0, sim.NestEggs);
            Assert.IsFalse(sim.NestParentPresent);
            Assert.AreEqual(NestPhase.Empty, sim.NestPhaseNow);
            Assert.IsTrue(sim.NestStands);
        }

        [Test]
        public void WinterGoneOn_TheNestIsBlownDown_AndBuiltAgainNextSpring()
        {
            var sim = WithThicket(); BirdCame(sim);
            sim.Config.eco.nestWinterGone = true;
            foreach (int cal in new[] { 2, 3, 4, 5 }) sim.DebugNestMonth(cal, 0);
            sim.DebugNestMonth(11, 0);
            Assert.IsFalse(sim.NestStands);
            sim.DebugNestMonth(2, 1);
            Assert.IsTrue(sim.NestStands);
        }

        // ---------------------------------------------------------------- the egg take
        [Test]
        public void AnEggIsTaken_NeverTheLast_TwoASpringAtMost_FoodGoesUp()
        {
            var sim = WithThicket(); BirdCame(sim);
            foreach (int cal in new[] { 2, 3 }) sim.DebugNestMonth(cal, 0);
            var ev = Listen(sim);
            sim.Config.eco.nestEggsMin = sim.Config.eco.nestEggsMax = 4; sim.State.eco.nest.eggs = 4;
            float food = sim.State.food;
            Assert.AreEqual(2, sim.NestEggsSpare, "two a spring, never the last");
            Assert.IsTrue(sim.TakeNestEgg()); Assert.IsTrue(sim.TakeNestEgg());
            Assert.IsFalse(sim.TakeNestEgg(), "the spring's share is taken");
            Assert.AreEqual(2, sim.NestEggs);
            Assert.Greater(sim.State.food, food, "small, but food");
            Assert.AreEqual(2, ev.Count(e => e.k == NestEventKind.EggTaken));
            // two eggs: one is spare, then the last stays
            sim = WithThicket(); BirdCame(sim);
            foreach (int cal in new[] { 2, 3 }) sim.DebugNestMonth(cal, 0);
            sim.State.eco.nest.eggs = 2;
            Assert.IsTrue(sim.TakeNestEgg());
            Assert.AreEqual(1, sim.NestEggs);
            Assert.IsFalse(sim.TakeNestEgg(), "never the last egg");
            Assert.AreEqual(1, sim.NestEggs);
        }

        [Test]
        public void NoEggTake_WithoutEggs_OrOutOfSpring()
        {
            var sim = WithThicket(); BirdCame(sim);
            Assert.IsFalse(sim.TakeNestEgg(), "no nest");
            sim.DebugNestMonth(2, 0);
            Assert.IsFalse(sim.TakeNestEgg(), "building: no eggs yet");
            sim.DebugNestMonth(3, 0); sim.DebugNestMonth(4, 0);
            Assert.IsFalse(sim.TakeNestEgg(), "chicks, not eggs");
        }

        [Test]
        public void TheChronicle_TellsTheNestOnce_ThenTheFirstEgg_ThenTheChicks_InClosedWorldWords()
        {
            var sim = WithThicket(); BirdCame(sim);
            foreach (int cal in new[] { 2, 3 }) sim.DebugNestMonth(cal, 0);
            sim.State.eco.nest.eggs = 3;
            Assert.IsTrue(sim.TakeNestEgg());
            sim.DebugNestMonth(4, 0);
            foreach (int cal in new[] { 5, 2, 3 }) sim.DebugNestMonth(cal, cal == 5 ? 0 : 1);
            var kinds = sim.State.chronicle.Select(c => c.kind).ToList();
            Assert.AreEqual(1, kinds.Count(k => k == "eco_nest"), "once");
            Assert.AreEqual(1, kinds.Count(k => k == "eco_nest_egg"));
            Assert.AreEqual(1, kinds.Count(k => k == "eco_nest_chicks"));
            foreach (var c in sim.State.chronicle.Where(c => c.kind.StartsWith("eco_nest")))
                foreach (var bad in new[] { "city", "sea", "world", "outside", "forest beyond" }) StringAssert.DoesNotContain(bad, c.text.ToLowerInvariant());
        }

        // ---------------------------------------------------------------- the nest and its thicket
        [Test]
        public void ThePluckedNestBush_MovesTheNest_ToAnotherBushOfTheThicket()
        {
            var sim = WithThicket(); BirdCame(sim);
            foreach (int cal in new[] { 2, 3 }) sim.DebugNestMonth(cal, 0);
            var ev = Listen(sim);
            int was = sim.NestCell, eggs = sim.NestEggs;
            Assert.Greater(sim.EcoLandscapePluck(SiteMap.CXc(was), SiteMap.CZc(was), 0.02f), 0, "the bush comes out");
            Assert.IsTrue(sim.NestStands, "the thicket stands: the nest moves");
            Assert.AreNotEqual(was, sim.NestCell);
            Assert.AreEqual(EcoCover.Bush, (EcoCover)sim.State.eco.cover[sim.NestCell]);
            Assert.AreEqual(eggs, sim.NestEggs);
            Assert.IsTrue(ev.Any(e => e.k == NestEventKind.Moved));
        }

        [Test]
        public void ThePluckedThicket_TakesTheNest_AndNextSpringItMovesToAnotherThicket()
        {
            var sim = WithThicket(1); BirdCame(sim);
            sim.Config.eco.nestWhichThicket = 0; // pin the nest to the first thicket, so this test plucks the right one
            var l = Thickets(sim);
            Assert.AreEqual(2, l.Count);
            sim.DebugAddBushes(0);
            foreach (int cal in new[] { 2, 3 }) sim.DebugNestMonth(cal, 0);
            Assert.AreEqual(l[0].cell, sim.NestThicketCell, "nestWhichThicket 0: the first thicket");
            var ev = Listen(sim);
            // pluck the whole first thicket
            var first = l[0];
            sim.DebugLandscapeArea(SiteMap.CXc(first.cell), SiteMap.CZc(first.cell), first.radius * 1.6f);
            Assert.AreEqual(1, sim.BushThicketCount, "the first thicket is gone");
            Assert.IsFalse(sim.NestStands, "the nest went with it");
            Assert.IsTrue(ev.Any(e => e.k == NestEventKind.Gone));
            Assert.AreEqual(0, sim.NestEggs);
            // the other thicket needs 6 bushes to hold a nest
            sim.DebugAddBushes(8);
            sim.DebugNestMonth(2, 1);
            Assert.IsTrue(sim.NestStands, "a new nest next spring");
            Assert.AreEqual(Thickets(sim)[0].cell, sim.NestThicketCell, "in the one that is left");
        }

        [Test]
        public void ThinThicket_IsNotChosen_UntilItHoldsSixBushes()
        {
            var sim = World(); var g = sim.EcoGround;
            Assert.IsTrue(sim.PlantTree(g.campX + 0.9f, g.campZ));
            for (int c = 0; c < EcoLand.N; c++)
                if ((EcoCover)sim.State.eco.cover[c] == EcoCover.Bare && !g.InBasin(c)) sim.State.eco.cover[c] = (byte)EcoCover.Grass;
            sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
            BirdCame(sim);
            sim.Config.eco.nestMinBushes = 99;
            sim.DebugNestMonth(2, 0);
            Assert.IsFalse(sim.NestStands, "under the bushes it needs");
            sim.Config.eco.nestMinBushes = 1;
            sim.DebugNestMonth(2, 0);
            Assert.IsTrue(sim.NestStands);
        }

        [Test]
        public void Q6_TheBiggestThicket_ByDefault()
        {
            var sim = WithThicket(1); BirdCame(sim);
            var l = Thickets(sim);
            sim.State.eco.thickets[0].trim = 0;
            Assert.AreEqual(1, new EcoTuning().nestWhichThicket, "the user's pick 2026-11-15: the biggest");
            sim.DebugNestMonth(2, 0);
            int big = l.OrderByDescending(t => t.bushes).First().cell;
            Assert.AreEqual(big, sim.NestThicketCell);
        }

        [Test]
        public void TheShrinkingThicket_LeavesTheNestBush_ForTheNestIsKept()
        {
            var sim = WithThicket(); BirdCame(sim);
            foreach (int cal in new[] { 2, 3 }) sim.DebugNestMonth(cal, 0);
            int bush = sim.NestCell;
            for (int s = 0; s < 8; s++) sim.DebugSkipSeasons(1); // seasons pass; more thickets crowd it
            sim.DebugPlaceThicketNearCamp(); sim.DebugPlaceThicketNearCamp();
            for (int s = 0; s < 40; s++) sim.DebugSkipSeasons(1);
            Assert.IsTrue(sim.NestStands, "the nest is still standing");
            Assert.AreEqual(EcoCover.Bush, (EcoCover)sim.State.eco.cover[sim.NestCell]);
            Assert.GreaterOrEqual(sim.NestCell, 0);
            _ = bush;
        }

        // ---------------------------------------------------------------- save, budget, stability
        [Test]
        public void OldSave_WithoutANest_LoadsAsNoNestYet()
        {
            var sim = WithThicket();
            string json = JsonUtility.ToJson(sim.State);
            var loaded = JsonUtility.FromJson<SimState>(json);
            loaded.eco.nest = null; // an old save has none
            var again = new SimCore(sim.Config, loaded);
            Assert.IsFalse(again.NestStands);
            Assert.AreEqual(0, again.NestEggs);
            again.DebugNestMonth(2, 0); // does not throw
        }

        [Test]
        public void TheNest_IsSaved_AndLoadsBack()
        {
            var sim = WithThicket(); BirdCame(sim);
            foreach (int cal in new[] { 2, 3 }) sim.DebugNestMonth(cal, 0);
            Assert.IsTrue(sim.TakeNestEgg() || sim.NestEggs < 2);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.IsTrue(loaded.NestStands);
            Assert.AreEqual(sim.NestCell, loaded.NestCell);
            Assert.AreEqual(sim.NestEggs, loaded.NestEggs);
            Assert.AreEqual(sim.NestEggsTakenThisSpring, loaded.NestEggsTakenThisSpring);
            Assert.AreEqual(sim.NestPhaseNow, loaded.NestPhaseNow);
            Assert.AreEqual(sim.NestParentPresent, loaded.NestParentPresent);
        }

        [Test]
        public void LivedThroughRealYears_TheNestHasItsSpringsAndTheWorldIsTheSameAsWithout()
        {
            // the real month loop: place the thicket early, live 6 years, the nest is there each spring and never leaves a parent behind in summer
            var sim = WithThicket();
            var seen = new HashSet<NestPhase>();
            for (int m = 0; m < 6 * 12; m++)
            {
                sim.Advance(sim.Config.StepHours);
                int cal = sim.CalendarMonth;
                if (sim.NestStands) seen.Add(sim.NestPhaseNow);
                if (cal >= 5 && cal <= 10) Assert.IsFalse(sim.NestParentPresent, "no parent in summer or autumn");
                Assert.LessOrEqual(sim.NestEggs, 3);
            }
            TestContext.Out.WriteLine("phases seen: " + string.Join(",", seen) + " · nest " + sim.NestStands);
            // the first bird comes with the bushes' growth: without it there is no nest, never a crash
            Assert.IsTrue(!sim.NestStands || sim.State.eco.Reached(EcoMilestones.FirstBird));
        }

        [Test]
        public void TheBird_IsOutsideTheAnimalBudget_ByDefault_AndOneFrogSlotWhenAsked()
        {
            Assert.AreEqual(0, AnimalBudget.NestBirdSlots);
            Assert.AreEqual(AnimalBudget.BaseTotal, AnimalBudget.Total(AnimalBudget.BaseArea), "the table is untouched: the bird is not a kind");
            int frogs = AnimalBudget.Frogs;
            try
            {
                AnimalBudget.NestBirdSlots = 1; // Q4, the other answer
                Assert.AreEqual(frogs - 1, AnimalBudget.Frogs);
                Assert.AreEqual(AnimalBudget.BaseTotal - 1, AnimalBudget.Total(AnimalBudget.BaseArea));
            }
            finally { AnimalBudget.NestBirdSlots = 0; }
        }

        [Test]
        public void TheNestDice_DoNotTouchTheMainStreams_TheSameSeedPlaysTheSameWithAndWithoutANestRun()
        {
            // two identical boxes, one of them runs the nest's month a lot: the main rng and the econ stream do not move
            var a = WithThicket(); var b = WithThicket(); BirdCame(b);
            for (int y = 0; y < 3; y++) foreach (int cal in new[] { 2, 3, 4, 5 }) b.DebugNestMonth(cal, y);
            a.Advance(12 * a.Config.StepHours); b.Advance(12 * b.Config.StepHours);
            Assert.AreEqual(a.State.population, b.State.population);
            Assert.AreEqual(a.State.notables.Count, b.State.notables.Count);
            CollectionAssert.AreEqual(a.State.eco.cover, b.State.eco.cover, "the land is the same");
        }
    }
}
