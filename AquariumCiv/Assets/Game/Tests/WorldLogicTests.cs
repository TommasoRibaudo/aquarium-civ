using System.Linq;
using System.Text.RegularExpressions;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Phase 1 of #188 (building cards), #147 (the crumb's label), #154 (stall stock) and #160 (the Clean hint).</summary>
    public class WorldLogicTests
    {
        static SimCore Aged(int seed, int years, bool toTown = true)
        {
            var sim = NewSim(seed);
            for (int y = 0; y < years; y++)
            {
                sim.Advance(1);
                if (toTown && sim.State.eraReady) sim.AdvanceEra();
            }
            return sim;
        }

        // ---------- #188 ----------
        [Test]
        public void EveryBuildingAWorldMakesHasACardWithItsYear()
        {
            var sim = Aged(7, 60);
            Assert.Greater(sim.State.buildings.Count, 4);
            foreach (var b in sim.State.buildings)
            {
                var c = sim.BuildingCardOf(b.id);
                Assert.IsNotNull(c);
                Assert.AreEqual((int)(b.foundedMonth / SimConfig.MonthsPerYear), c.year);
                Assert.IsNotEmpty(c.title);
                StringAssert.StartsWith("Built by", c.builderLine);
                if (c.builderId >= 0) Assert.IsNotNull(sim.FindNotable(c.builderId), "a named builder is a real notable");
            }
            Assert.IsNull(sim.BuildingCardOf(-5));
        }

        [Test]
        public void BuildersAreRecordedAndTheRunIsTheSameWhoeverBuilt()
        {
            int named = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                var a = Aged(seed, 70); var b = Aged(seed, 70);
                Assert.AreEqual(Fingerprint(a.State), Fingerprint(b.State), "same seed, same run");
                Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b.State));
                named += a.State.buildings.Count(x => x.builderId > 0);
            }
            Assert.Greater(named, 0, "somebody is named as a builder");
        }

        [Test]
        public void TheMasterBuilderBuildsWhenThereIsOne()
        {
            var sim = Aged(3, 150);
            var mb = sim.HolderOf(RoleType.MasterBuilder);
            if (mb == null || !mb.alive) Assert.Ignore("no master builder in this world");
            var b = sim.DebugBuild(BuildingType.Hut);
            Assert.AreEqual(mb.id, b.builderId);
            var c = sim.BuildingCardOf(b.id);
            Assert.AreEqual(mb.id, c.builderId);
            StringAssert.Contains("master builder", c.builderLine);
        }

        [Test]
        public void ABuilderWhoHasDiedStillShowsAndIsFlaggedDead()
        {
            var sim = Aged(5, 40);
            var n = sim.State.notables.First();
            var b = sim.State.buildings.First();
            Assert.IsTrue(sim.DebugSetBuilder(b.id, n.id));
            n.alive = false;
            var c = sim.BuildingCardOf(b.id);
            Assert.AreEqual(n.id, c.builderId);
            Assert.IsFalse(c.builderAlive);
            StringAssert.Contains(n.FullName, c.builderLine);
        }

        [Test]
        public void OldSavesLoadAndTheirBuildingsSayTogether()
        {
            var sim = Aged(4, 30);
            string json = Regex.Replace(JsonUtility.ToJson(sim.State), "\"builderId\"\\s*:\\s*-?\\d+,?", "");
            Assert.IsFalse(json.Contains("builderId"));
            var old = JsonUtility.FromJson<SimState>(json);
            var loaded = new SimCore(new SimConfig(), old);
            Assert.Greater(old.buildings.Count, 0);
            foreach (var b in old.buildings)
            {
                Assert.AreEqual(0, b.builderId);
                var c = loaded.BuildingCardOf(b.id);
                Assert.IsTrue(c.Together);
                Assert.AreEqual("Built by the people, together.", c.builderLine);
            }
            loaded.Advance(5); // and it keeps playing
        }

        [Test]
        public void EveryBuildingTypeHasATitleAndABlurb()
        {
            foreach (var t in new[] { BuildingType.Camp, BuildingType.Hut, BuildingType.Farm, BuildingType.Lumbermill, BuildingType.Well, BuildingType.Windmill,
                BuildingType.Market, BuildingType.Tavern, BuildingType.Church, BuildingType.Workshop, BuildingType.Guildhall, BuildingType.Shop, BuildingType.Bakery })
                foreach (Era e in new[] { Era.Camp, Era.Village, Era.Town })
                {
                    Assert.IsNotEmpty(SimCore.BuildingTitle(t, e), t);
                    Assert.IsNotEmpty(SimCore.BuildingBlurb(t, e), t);
                }
        }

        // ---------- #147: the label ----------
        [Test]
        public void TheCrumbsShelfTextNamesTheRealShaping()
        {
            var sim = Aged(9, 3, false);
            var r = new Relic { id = RelicId.SkyCrumb, foundMonth = 1, cause = SimCore.NudgeKind.Herd };
            sim.State.relics.Add(r);
            string herd = sim.RelicLabel(RelicId.SkyCrumb);
            StringAssert.Contains("sent the herd", herd);
            StringAssert.DoesNotContain("made the trees", herd);
            r.cause = SimCore.NudgeKind.Rain;
            StringAssert.Contains("made rain", sim.RelicLabel(RelicId.SkyCrumb));
            r.cause = "";
            StringAssert.Contains("shaped the land", sim.RelicLabel(RelicId.SkyCrumb)); // old saves and carried crumbs
            StringAssert.Contains("fed us", sim.RelicLabel(RelicId.SkyCrumb));           // what it did for the people
        }

        // ---------- #154 ----------
        [Test]
        public void StallsFillWithStockAndAnEmptyStockIsBare()
        {
            for (int stalls = 1; stalls <= 5; stalls++)
            {
                for (int i = 0; i < stalls; i++) Assert.IsTrue(TownStalls.StockFor(0f, 0f, 20, true, i, stalls).Empty);
                int last = -1;
                foreach (float f in new[] { 1f, 10f, 30f, 60f, 80f, 200f })
                {
                    int total = Enumerable.Range(0, stalls).Sum(i => TownStalls.StockFor(f, 0f, 20, false, i, stalls).produce);
                    Assert.GreaterOrEqual(total, last, "more food never empties a stall");
                    last = total;
                }
                for (int i = 0; i < stalls; i++)
                {
                    var full = TownStalls.StockFor(1000f, 1000f, 20, true, i, stalls);
                    Assert.AreEqual(TownStalls.MaxLevel, full.produce);
                    Assert.AreEqual(TownStalls.MaxLevel, full.goods);
                    Assert.AreEqual(TownStalls.MaxLevel, full.bread);
                }
            }
            Assert.AreEqual(0, TownStalls.StockFor(1000f, 1000f, 20, false, 0, 3).bread, "no bakery, no bread");
            Assert.IsTrue(TownStalls.StockFor(5f, 0f, 20, false, 0, 3).produce > 0, "any food shows");
            Assert.IsTrue(TownStalls.StockFor(5f, 0f, 20, false, 2, 3).produce <= TownStalls.StockFor(5f, 0f, 20, false, 0, 3).produce, "the first stall fills first");
        }

        [Test]
        public void StallStockReadsTheSimAndTheDebugCheatsReachTheEnds()
        {
            var sim = Aged(2, 80);
            int stalls = 3;
            sim.State.food = 0f; sim.State.wood = 0f;
            Assert.IsTrue(Enumerable.Range(0, stalls).All(i => TownStalls.StockFor(sim, i, stalls).Empty));
            TownStalls.DebugStock("full", sim.State.population, out float food, out float wood);
            sim.State.food = food; sim.State.wood = wood;
            Assert.IsTrue(Enumerable.Range(0, stalls).All(i => TownStalls.StockFor(sim, i, stalls).produce == TownStalls.MaxLevel));
            TownStalls.DebugStock("half", sim.State.population, out food, out wood);
            sim.State.food = food; sim.State.wood = wood;
            int mid = TownStalls.StockFor(sim, 0, stalls).produce;
            Assert.That(mid, Is.InRange(1, TownStalls.MaxLevel - 1));
        }

        // ---------- #160 ----------
        [Test]
        public void ATouchOnMessOutsideCleanModePulsesAndHintsOnce()
        {
            var h = new CleanHint();
            var r = h.Touch(false, true, 10f);
            Assert.IsTrue(r.pulse); Assert.IsTrue(r.hint);
            Assert.IsFalse(h.Touch(true, true, 100f).pulse, "in clean mode: nothing");
            Assert.IsFalse(h.Touch(false, false, 100f).pulse, "not on mess: nothing");
        }

        [Test]
        public void TheCleanHintIsRateLimitedAndStopsOnceTheyHaveCleaned()
        {
            var h = new CleanHint();
            Assert.IsTrue(h.Touch(false, true, 0f).hint);
            var again = h.Touch(false, true, 2f);
            Assert.IsFalse(again.pulse, "a second touch within the pulse gap is quiet");
            var later = h.Touch(false, true, CleanHint.PulseGap + 0.1f);
            Assert.IsTrue(later.pulse); Assert.IsFalse(later.hint, "the words wait for the longer gap");
            Assert.IsTrue(h.Touch(false, true, CleanHint.HintGap + 1f).hint);
            for (float t = 100f; t < 2000f; t += 50f) h.Touch(false, true, t);
            Assert.AreEqual(CleanHint.MaxHints, h.HintsShown, "never more than the cap");
            var fresh = new CleanHint();
            fresh.Cleaned();
            Assert.IsFalse(fresh.Touch(false, true, 0f).pulse, "they know how to clean");
            fresh.Reset();
            Assert.IsTrue(fresh.Touch(false, true, 0f).hint);
        }
    }
}
