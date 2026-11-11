using System;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#578 (the travellers): the words match the land (woods / meadow / pond, and the camp sits where they said), the wood they carry
    /// reaches the camp's pile (the first load told once, the pile quicker while the lean lasts), and they walk in and join when the village is
    /// made (or at the next new house in a village).</summary>
    public class TravellersTests
    {
        static SimCore Eco(int seed)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static SimCore Plain(int seed, int months = 0)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < months; m++) sim.Step();
            return sim;
        }

        /// <summary>Raises camp_travellers (stepping a year at a time while it does not hold).</summary>
        static Dilemma Raise(SimCore sim)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                var d = sim.DebugRaiseDilemma("camp_travellers");
                if (d != null) return d;
                for (int m = 0; m < 12; m++) sim.Step();
            }
            return null;
        }

        static float Nearest(bool[] mask, float x, float z)
        {
            float best = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++) if (mask[c]) best = Math.Min(best, Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z));
            return best;
        }

        static int ForestAround(SimCore sim, float x, float z)
        {
            var map = sim.Settlement.Map;
            int c = SiteMap.CellAt(x, z), n = 0;
            for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++) if (map.Forest(c + dj * SiteMap.NX + di)) n++;
            return n;
        }

        static string AllWords(SimCore sim, Dilemma d)
        {
            var v = sim.ViewOfDilemma(d);
            return v.question + "|" + string.Join("|", v.options.Select(o => o.label + "/" + o.hint));
        }

        // ---- 1. the words match the land

        [Test]
        public void WithAGrownWoodsTheWordsSayTheWoodsAndTheCampIsInTheFarWoods()
        {
            var sim = Eco(7);
            Assume.That(sim.Settlement?.Map, Is.Not.Null);
            sim.DebugWildTrees(false);
            Assert.IsTrue(sim.WoodsExist, "a grown wild stand");
            var d = Raise(sim);
            Assert.IsNotNull(d, "camp_travellers held");
            Assert.AreEqual("woods", d.land);
            StringAssert.Contains("woods", sim.QuestionOf(d));
            StringAssert.Contains("far woods", AllWords(sim, d));
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            var g = sim.ApartOf(d);
            Assert.AreEqual(ApartKind.WoodsCamp, g.kind);
            StringAssert.Contains("far woods", sim.State.chronicle.Last(e => e.kind == "newcomer").text);
        }

        [Test]
        public void WithNoWoodsTheyAreNeverFoundInTheWoods()
        {
            int seen = 0;
            foreach (int seed in new[] { 7, 42, 4242, 5 })
            {
                var sim = Eco(seed);
                if (sim.Settlement?.Map == null) continue;
                Assert.IsFalse(sim.WoodsExist, "a young box has no grown wild tree");
                var d = Raise(sim);
                if (d == null) continue;
                Assert.IsFalse(sim.WoodsExist);
                CollectionAssert.Contains(new[] { "meadow", "pond" }, d.land, $"seed {seed}");
                StringAssert.DoesNotContain("woods", AllWords(sim, d), $"seed {seed}: no woods in the words");
                Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
                StringAssert.DoesNotContain("woods", sim.State.chronicle.Last(e => e.kind == "newcomer").text);
                seen++;
            }
            Assert.Greater(seen, 0);
        }

        [Test]
        public void ByTheMeadowTheWordsSayTheMeadowAndTheCampStandsOnOpenGrass()
        {
            int seen = 0;
            foreach (int seed in new[] { 7, 42, 4242, 5 })
            {
                var sim = Eco(seed);
                if (sim.Settlement?.Map == null) continue;
                sim.DebugLand = "meadow";
                var woodsAt = sim.ApartSpotPreview(ApartKind.WoodsCamp);
                var d = Raise(sim);
                if (d == null) continue;
                StringAssert.Contains("the meadow", sim.QuestionOf(d));
                StringAssert.Contains("out in the meadow", AllWords(sim, d));
                Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
                var g = sim.ApartOf(d);
                Assert.AreEqual(ApartKind.MeadowCamp, g.kind);
                Assert.LessOrEqual(ForestAround(sim, g.x, g.z), ForestAround(sim, woodsAt.x, woodsAt.z), $"seed {seed}: no more forest round a meadow camp than a woods camp");
                var drawn = sim.ApartCampSpot(g.x, g.z, g.kind, out _);
                Assert.IsFalse(PondShore.InFutureWater(sim, drawn.x, drawn.z, 0.05f), "dry for good");
                seen++;
            }
            Assert.Greater(seen, 0);
        }

        [Test]
        public void ByThePondTheWordsSayThePondAndTheCampStandsDryByTheWater()
        {
            int seen = 0;
            foreach (int seed in new[] { 7, 42, 4242, 5 })
            {
                var sim = Eco(seed);
                if (sim.Settlement?.Map == null) continue;
                sim.DebugLand = "pond";
                var bare = PondShore.FutureWater(sim.EcoGround, 0f);
                var woodsAt = sim.ApartSpotPreview(ApartKind.WoodsCamp);
                var d = Raise(sim);
                if (d == null) continue;
                StringAssert.Contains("the pond", sim.QuestionOf(d));
                StringAssert.Contains("by the pond", AllWords(sim, d));
                Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
                var g = sim.ApartOf(d);
                Assert.AreEqual(ApartKind.PondCamp, g.kind);
                var drawn = sim.ApartCampSpot(g.x, g.z, g.kind, out _);
                Assert.IsFalse(PondShore.InFutureWater(sim, drawn.x, drawn.z, 0.05f), $"seed {seed}: on dry ground that stays dry");
                float d1 = Nearest(bare, drawn.x, drawn.z), d2 = Nearest(bare, woodsAt.x, woodsAt.z);
                Assert.LessOrEqual(d1, SimCore.WoodsPondMargin + ApartSpots.Footprint + SimCore.ShoreHutReach + 0.5f, $"seed {seed}: by the water ({d1:0.00})");
                Assert.LessOrEqual(d1, d2 + 1e-3f, $"seed {seed}: nearer the water than a woods camp ({d1:0.00} vs {d2:0.00})");
                seen++;
            }
            Assert.Greater(seen, 0);
        }

        [Test]
        public void ThePickIsFixedWhenItIsRaisedAndTheWoodsComingLaterDoesNotChangeTheWords()
        {
            var sim = Eco(7);
            Assume.That(sim.Settlement?.Map, Is.Not.Null);
            sim.DebugLand = "meadow";
            var d = Raise(sim);
            Assert.IsNotNull(d);
            string before = sim.QuestionOf(d);
            sim.DebugLand = "";
            sim.DebugWildTrees(false);
            Assert.AreEqual(before, sim.QuestionOf(d));
        }

        // ---- 2. the wood

        [Test]
        public void AFirstLoadReachesThePileAndRaisesItsLineOnce()
        {
            var sim = Plain(7531, 72);
            var d = Raise(sim);
            Assume.That(d, Is.Not.Null);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            var g = sim.ApartOf(d);
            Assert.IsTrue(g.joins);
            int lines(string t) => sim.State.chronicle.Count(e => e.text.Contains(t));
            Assert.AreEqual(0, lines("first full load"));
            Assert.IsTrue(sim.ApartBringsWood(g.id));
            Assert.AreEqual(1, g.woodLoads);
            Assert.AreEqual(1, lines("first full load"), "the first load is told");
            StringAssert.Contains("notch", sim.State.chronicle.Last(e => e.text.Contains("first full load")).text);
            sim.ApartBringsWood(g.id); sim.ApartBringsWood(g.id);
            Assert.AreEqual(3, g.woodLoads);
            Assert.AreEqual(1, lines("first full load"), "only once");
            Assert.IsFalse(sim.ApartBringsWood(9999), "no such camp");
        }

        [Test]
        public void ThePileGrowsQuickerWhileTheWoodLeanLastsAndTheCarryIsPacedByIt()
        {
            var sim = Plain(7531, 72);
            var d = Raise(sim);
            Assume.That(d, Is.Not.Null);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            var g = sim.ApartOf(d);
            float leaning = sim.ApartWoodRate(g);
            Assert.AreEqual(1.5f, leaning, 1e-4f, "wood x1.5 while the lean lasts");
            for (int m = 0; m < SimCore.NudgeMonths + 2; m++) sim.Step();
            float after = sim.ApartWoodRate(g);
            Assert.AreEqual(1f, after, 1e-4f, "back to the usual pace");
            // the view's carry cycle: loads set down in ten minutes of real time
            int Loads(float rate)
            {
                float period = ApartLife.CarryPeriod(rate), clock = 0f; int n = 0; bool dropped = false;
                for (int i = 0; i < 6000; i++)
                {
                    clock += 0.1f;
                    var ph = ApartLife.CarryAt(clock / period, out _);
                    if (ph == CarryPhase.Chop) dropped = false;
                    if (ph == CarryPhase.Drop && !dropped) { dropped = true; n++; }
                }
                return n;
            }
            int slow = Loads(after), fast = Loads(leaning);
            Assert.AreEqual(13, slow, 1, "one a 45 s cycle");
            Assert.Greater(fast, slow, "quicker");
            Assert.AreEqual(1.5f, (float)fast / slow, 0.1f);
        }

        [Test]
        public void TheCarryWalksFromTheCuttingSpotToThePileAndSetsTheLogDownThere()
        {
            var order = new System.Collections.Generic.List<CarryPhase>();
            float lastWalk = -1f;
            for (float c = 0f; c < 1f; c += 0.001f)
            {
                var ph = ApartLife.CarryAt(c, out float walk);
                if (order.Count == 0 || order[order.Count - 1] != ph) order.Add(ph);
                if (ph == CarryPhase.Walk) { Assert.GreaterOrEqual(walk, lastWalk, "the walk only goes on"); lastWalk = walk; }
                if (ph == CarryPhase.Drop) Assert.AreEqual(1f, walk, 1e-4f, "set down at the pile");
            }
            CollectionAssert.AreEqual(new[] { CarryPhase.Chop, CarryPhase.Lift, CarryPhase.Walk, CarryPhase.Drop }, order);
            Assert.AreEqual(ApartLife.MaxPileLogs, ApartLife.PileLogs(500));
            Assert.AreEqual(0, ApartLife.PileLogs(-3));
            Assert.AreEqual(5, ApartLife.PileLogs(5));
        }

        // ---- 3. they join

        [Test]
        public void WhenTheVillageIsMadeTheyWalkInAndJoin()
        {
            var sim = Plain(7531, 72);
            var d = Raise(sim);
            Assume.That(d, Is.Not.Null);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            var g = sim.ApartOf(d);
            Assert.AreEqual(0, g.joinState, "camping in the camp era");
            var people = g.members.ToList();
            int pop = sim.State.population;
            Assert.IsTrue(people.All(n => !sim.State.notables.Contains(n)), "apart: not the settlement's");
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(Era.Village, sim.State.era);
            Assert.AreEqual(2, g.joinState, "the village is made: they pack up");
            CollectionAssert.Contains(sim.State.apartGroups, g, "still there until they have walked in");
            Assert.IsTrue(sim.FinishApartJoin(g.id), "the view walks them in");
            CollectionAssert.DoesNotContain(sim.State.apartGroups, g, "the apart group is gone");
            Assert.IsNull(sim.ApartOf(d));
            Assert.IsTrue(people.All(n => sim.State.notables.Contains(n)), "they are villagers' notables now");
            Assert.AreEqual(pop + people.Count(n => n.alive), sim.State.population, "and counted");
            StringAssert.Contains("walked in to the green", sim.State.chronicle.Last(e => e.kind == "newcomer").text);
            Assert.IsFalse(sim.FinishApartJoin(g.id), "once");
        }

        [Test]
        public void IfTheViewNeverWalksThemInTheSimFinishesTheJoinAfterAWhile()
        {
            var sim = Plain(7531, 72);
            var g = sim.DebugSettlersArrive(ApartKind.WoodsCamp, true);
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(2, g.joinState);
            for (int m = 0; m < SimCore.JoinFallbackMonths + 1; m++) sim.Step();
            CollectionAssert.DoesNotContain(sim.State.apartGroups, g);
            Assert.IsTrue(g.members.All(n => sim.State.notables.Contains(n)));
        }

        [Test]
        public void AnsweredInAVillageTheyJoinAtTheNextNewHouse()
        {
            var sim = Plain(7531, 72);
            Assert.IsTrue(sim.ForceNextEra());
            var g = sim.DebugSettlersArrive(ApartKind.WoodsCamp, true);
            Assert.AreEqual(1, g.joinState, "waits for a house");
            for (int m = 0; m < 3; m++) sim.Step();
            Assert.AreEqual(1, g.joinState, "no new house yet");
            sim.DebugBuild(BuildingType.Hut);
            sim.Step();
            Assert.AreEqual(2, g.joinState, "a new house: they pack up");
            int pop = sim.State.population;
            Assert.IsTrue(sim.FinishApartJoin(g.id));
            Assert.AreEqual(pop + 2, sim.State.population);
            CollectionAssert.DoesNotContain(sim.State.apartGroups, g);
        }

        [Test]
        public void OtherApartFolkAreNotTheTravellersAndKeepToThemselves()
        {
            var sim = Plain(7531, 72);
            var shore = sim.DebugSettlersArrive(ApartKind.ShoreHut);
            var woods = sim.DebugSettlersArrive(ApartKind.WoodsCamp);
            Assert.IsFalse(shore.joins || woods.joins);
            Assert.AreNotEqual(shore.id, woods.id);
            Assert.IsTrue(sim.ForceNextEra());
            Assert.AreEqual(0, shore.joinState); Assert.AreEqual(0, woods.joinState);
            for (int m = 0; m < 12; m++) sim.Step();
            Assert.AreEqual(2, sim.State.apartGroups.Count(g => g == shore || g == woods));
        }

        [Test]
        public void GroupIdsStayUniqueAfterOneJoinsAndGoes()
        {
            var sim = Plain(7531, 72);
            var a = sim.DebugSettlersArrive(ApartKind.ShoreHut);
            var b = sim.DebugSettlersArrive(ApartKind.WoodsCamp, true);
            b.joinState = 2;
            Assert.IsTrue(sim.FinishApartJoin(b.id));
            var c = sim.DebugSettlersArrive(ApartKind.WoodsCamp);
            Assert.AreNotEqual(a.id, c.id, "a new group does not take a live one's id");
        }

        [Test]
        public void TheTravellersCheatsMakeTheCampIfNoneStandsAndJoinNowStartsTheWalk()
        {
            var sim = Plain(7531, 72);
            var g = sim.DebugTravellersBringWood();
            Assert.IsTrue(g.joins);
            Assert.AreSame(g, sim.DebugTravellersBringWood(), "the same camp the second time");
            Assert.AreEqual(0, g.joinState);
            sim.DebugTravellersJoin();
            Assert.AreEqual(2, g.joinState);
        }
    }
}
