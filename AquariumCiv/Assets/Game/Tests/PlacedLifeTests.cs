using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#420-#422 (docs/design/placed-life.md §4, §5.3-§5.5): the deer, the ducks and the fish as placed stocks, the floors
    /// every taker keeps, shy animals, the people's restraint and the restock, the village gate, the save.</summary>
    public class PlacedLifeTests
    {
        // ---------------------------------------------------------------- helpers
        internal static SimCore World(int seed = 7) => LifeJarTests.World(seed);
        static EcoModel Model(SimCore sim) => new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco);
        static float Dist(int c, float x, float z) => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);

        /// <summary>The first w x h block of dry cells (off the hollow and the yard) at least <paramref name="far"/> from the fire, and
        /// clear of any other cover but bare and meadow. Returns its corner cell.</summary>
        static int Block(SimCore sim, int w, int h, float far, int skip = 0)
        {
            var g = sim.EcoGround; var e = sim.State.eco;
            for (int j = 3; j + h < SiteMap.NZ - 3; j++)
                for (int i = 3; i + w < SiteMap.NX - 3; i++)
                {
                    bool ok = true;
                    for (int dj = -1; dj <= h && ok; dj++)
                        for (int di = -1; di <= w && ok; di++)
                        {
                            int c = SiteMap.Index(i + di, j + dj);
                            var cv = (EcoCover)e.cover[c];
                            ok = !g.InBasin(c) && !g.yard[c] && Dist(c, g.campX, g.campZ) >= far
                                && (cv == EcoCover.Bare || cv == EcoCover.Grass || cv == EcoCover.Tall);
                        }
                    if (ok && skip-- <= 0) return SiteMap.Index(i, j);
                }
            Assert.Fail("no dry block");
            return -1;
        }

        static void Fill(SimCore sim, int corner, int w, int h, EcoCover cv, int max = int.MaxValue)
        {
            int i0 = SiteMap.I(corner), j0 = SiteMap.J(corner), n = 0;
            for (int j = j0; j < j0 + h; j++) for (int i = i0; i < i0 + w && n < max; i++, n++) sim.State.eco.cover[SiteMap.Index(i, j)] = (byte)cv;
            Model(sim).Count();
        }

        /// <summary>A wood of <paramref name="trees"/> grown trees (a block, far from the fire). Returns the block's corner.</summary>
        internal static int Wood(SimCore sim, int trees)
        {
            int w = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(trees)));
            int corner = Block(sim, w, w, 1.4f);
            Fill(sim, corner, w, w, EcoCover.Tree, trees);
            return corner;
        }

        /// <summary>A pond of w x h water cells (a block). Returns the block's corner.</summary>
        internal static int Pond(SimCore sim, int w = 6, int h = 6)
        {
            int corner = Block(sim, w, h, 1.2f, 30);
            Fill(sim, corner, w, h, EcoCover.Water);
            return corner;
        }

        /// <summary>The thicket and the rabbits placed on a 30 % meadow (the camp's rungs), a wood and a pond.</summary>
        internal static SimCore Camp(int seed = 7, int trees = 18, bool pond = true)
        {
            var sim = World(seed);
            LifeJarTests.Meadow(sim, 0.3f);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0);
            if (trees > 0) Wood(sim, trees);
            if (pond) Pond(sim);
            return sim;
        }

        

        /// <summary>Steps through the next spring step (to month % 12 == 3, the step that breeds).</summary>
        /// <summary>Whole seasons, a month at a time (sim.Step: an Advance after a Step would wait for its own clock).</summary>
        static void Seasons(SimCore sim, int n) { for (int k = 0; k < n * 3; k++) sim.Step(); }
        static void Season(SimCore sim) => Seasons(sim, 1);

        static void ToSpring(SimCore sim) { do sim.Step(); while (sim.State.month % 12 != 3); }

        // ================================================================ #420 the deer
        [Test]
        public void Deer_NoneUntilPlaced_ThenAStagAndTwoDoes_AtThePlacedCell()
        {
            var sim = Camp();
            Seasons(sim, 8);
            Assert.AreEqual(0, sim.EcoDeerCount, "nothing arrives by itself");
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Deer), "5 grown trees and the camp's rabbits");
            int c = sim.LifePlaceCellNearCamp(LifeSpecies.Deer);
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Deer, c));
            Assert.AreEqual(1, sim.EcoDeerStags); Assert.AreEqual(2, sim.EcoDeerDoes);
            Assert.AreEqual(c, sim.EcoDeerHome, "the herd's home is the placed cell");
            Assert.AreEqual(3f, sim.LifePopulation(LifeSpecies.Deer));
            Assert.IsTrue(sim.State.chronicle.Any(x => x.kind == "life_deer"));
        }

        [Test]
        public void Deer_CapacityFollowsTheForest()
        {
            var sim = World(); var m = Model(sim); var e = sim.State.eco;
            foreach (var (trees, cap) in new[] { (0, 3), (5, 3), (11, 3), (12, 4), (18, 6), (24, 8), (200, 8) })
            {
                e.cTrees = trees;
                Assert.AreEqual(cap, m.DeerCap, trees + " trees");
            }
            // a placed herd grows to what the wood holds, and no further
            var small = Camp(7, 13);
            small.DebugPlaceNearCamp(LifeSpecies.Deer);
            for (int y = 0; y < 6; y++) { Seasons(small, 4); Assert.LessOrEqual(small.EcoDeerCount, Math.Max(small.EcoDeerCap, 3)); }
            Assert.AreEqual(small.EcoDeerCap, small.EcoDeerCount, "full");
            var big = Camp(7, 30);
            big.DebugPlaceNearCamp(LifeSpecies.Deer);
            Seasons(big, 16);
            Assert.AreEqual(8, big.EcoDeerCount, "a big wood holds 8 at most");
        }

        [Test]
        public void Deer_OneFawnPerDoeInSpring_UnderTheCap()
        {
            var sim = Camp(7, 30);
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            Assert.AreEqual(8, sim.EcoDeerCap);
            ToSpring(sim);
            Assert.AreEqual(5, sim.EcoDeerCount, "two does, two fawns");
            Assert.AreEqual(2, sim.EcoDeerFawns);
            while (sim.State.month % 12 != 10) sim.Step();
            Assert.AreEqual(5, sim.EcoDeerCount, "no fawns outside spring");
            Assert.AreEqual(0, sim.EcoDeerFawns, "grown by autumn");
            ToSpring(sim);
            Assert.AreEqual(8, sim.EcoDeerCount, "up to the cap only");
            Assert.GreaterOrEqual(sim.EcoDeerStags, 1); Assert.GreaterOrEqual(sim.EcoDeerDoes, 2);
        }

        [Test]
        public void Deer_TheHuntNeverTakesTheHerdBelowTwo_NorTheLastStagOrDoe()
        {
            var sim = Camp(7, 30);
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            sim.DebugDeerToCap();
            int caught = 0;
            for (int i = 0; i < 20; i++) if (sim.DeerHuntCatch(out _)) caught++;
            Assert.AreEqual(6, caught, "8 down to the floor of 2");
            Assert.AreEqual(2, sim.EcoDeerCount);
            Assert.AreEqual(1, sim.EcoDeerStags); Assert.AreEqual(1, sim.EcoDeerDoes);
            Assert.AreEqual(0, sim.DeerHuntSurplus);
            Assert.IsFalse(sim.DeerHuntCatch(out _));
            Assert.AreEqual(6, sim.EcoDeerTaken);
        }

        [Test]
        public void Deer_TheFirstStag_AntlersOverTheChiefsTent_Once_AndTwoHidesADeer()
        {
            var sim = Camp(7, 30);
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            Assert.IsFalse(sim.DeerAntlersUp);
            Assert.IsTrue(sim.DeerHuntCatch(out bool first));
            Assert.IsFalse(first, "the placed herd: a doe goes, the stag stays");
            Assert.IsFalse(sim.DeerAntlersUp, "a doe has no antlers");
            sim.DebugDeerToCap();
            int raised = 0; sim.AntlersRaised += () => raised++;
            int hides = sim.HidesDrying;
            Assert.IsTrue(sim.DeerHuntCatch(out bool stag));
            Assert.IsTrue(stag, "a stag while there are two");
            Assert.AreEqual(hides + SimCore.DeerHides, sim.HidesDrying, "two hides drying");
            Assert.IsTrue(sim.DeerAntlersUp);
            Assert.AreEqual(sim.State.month, sim.DeerAntlersMonth);
            sim.DebugDeerToCap();
            Assert.IsTrue(sim.DeerHuntCatch(out _));
            Assert.AreEqual(1, raised, "once");
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.kind == "deer_antlers"));
        }

        [Test]
        public void WhiteStag_OncePerBox_TwoAnswers()
        {
            var sim = Camp(7, 30);
            int seen = 0; sim.WhiteStagSeen += () => seen++;
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            Seasons(sim, 4);
            Assert.AreEqual(WhiteStag.None, sim.WhiteStagState, "not in the herd's first two years");
            for (int y = 0; y < 30 && sim.WhiteStagState == WhiteStag.None; y++) Seasons(sim, 4);
            Assert.AreEqual(WhiteStag.Seen, sim.WhiteStagState, "it shows on its own once the herd is settled");
            Assert.GreaterOrEqual(sim.State.eco.whiteStagMonth - sim.LifePlacedMonth(LifeSpecies.Deer), sim.Config.eco.whiteStagAfter);
            Assert.IsTrue(sim.WhiteStagChoiceDue);
            Assert.AreEqual(1, seen);
            int herd = sim.EcoDeerCount, hides = sim.HidesDrying;
            Assert.IsTrue(sim.ResolveWhiteStag(true));
            Assert.AreEqual(WhiteStag.Hunted, sim.WhiteStagState);
            Assert.IsTrue(sim.WhiteStagAntlers); Assert.IsFalse(sim.WhiteStagAtDawn);
            Assert.AreEqual(herd, sim.EcoDeerCount, "the white stag was never one of the herd");
            Assert.AreEqual(hides + SimCore.DeerHides, sim.HidesDrying);
            Assert.IsTrue(sim.State.chronicle.Any(x => x.kind == "festival" && x.text.Contains("white stag")), "a feast");
            Assert.IsFalse(sim.ResolveWhiteStag(false), "answered once");
            Seasons(sim, 40);
            Assert.AreEqual(1, seen, "once per box");
            Assert.IsFalse(sim.DebugWhiteStagNow());

            var other = Camp(7, 30);
            Assert.IsTrue(other.DebugWhiteStagNow());
            Assert.IsTrue(other.ResolveWhiteStag(false));
            Assert.IsTrue(other.WhiteStagAtDawn);
            Assert.IsTrue(other.State.chronicle.Any(x => x.kind == "white_stag"));
            var third = Camp(9, 30);
            Assert.IsTrue(third.DebugWhiteStagNow());
            bool hunted = third.WhiteStagLetThemDecide();
            Assert.AreEqual(hunted ? WhiteStag.Hunted : WhiteStag.LetBe, third.WhiteStagState, "let them decide answers it");
        }

        [Test]
        public void DeerHerd_PosesAreDeterministic_AndOnTheWoodsEdge()
        {
            var sim = Camp(7, 30, pond: false);
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            sim.DebugDeerToCap();
            DeerHerd.Field a = new DeerHerd.Field(), b = new DeerHerd.Field();
            sim.EcoDeerRefresh(a); sim.EcoDeerRefresh(b);
            a.Sync(sim.EcoDeerShown()); b.Sync(sim.EcoDeerShown());
            Assert.AreEqual(8, a.Alive.Count);
            Assert.IsTrue(a.HasRange);
            Assert.IsFalse(a.HasPond);
            Assert.IsTrue(a.IsStag(0), "id 0 is the stag");
            var acts = new HashSet<DeerAct>();
            var e = sim.State.eco;
            for (double t = 0; t < 600; t += 1.7)
                foreach (int id in a.Alive)
                {
                    var p = a.Sample(id, t, 12f); var q = b.Sample(id, t, 12f);
                    Assert.AreEqual(p.x, q.x); Assert.AreEqual(p.z, q.z); Assert.AreEqual(p.act, q.act);
                    acts.Add(p.act);
                    int c = SiteMap.CellAt(p.x, p.z);
                    Assert.GreaterOrEqual(c, 0);
                    Assert.AreNotEqual((byte)EcoCover.Water, e.cover[c]);
                }
            CollectionAssert.IsSupersetOf(acts, new[] { DeerAct.Graze, DeerAct.LookUp, DeerAct.Walk });
            Assert.IsFalse(acts.Contains(DeerAct.Drink), "no pond, no drink");
            // the range is the wood's edge: every spot within 0.3 u of a grown tree
            foreach (int c in a.Range)
                Assert.IsTrue(Enumerable.Range(0, EcoLand.N).Any(o => e.cover[o] == (byte)EcoCover.Tree
                    && Terrain.Dist(SiteMap.CXc(o), SiteMap.CZc(o), SiteMap.CXc(c), SiteMap.CZc(c)) <= DeerHerd.GrazeNear + 1e-4f));
            // another seed: other poses
            var c2 = new DeerHerd.Field();
            c2.Refresh(e.cover, sim.EcoGround, e.seed + 1, sim.EcoDeerHome, 1);
            c2.Sync(8);
            Assert.IsTrue(Enumerable.Range(0, 8).Any(id => c2.Sample(id, 50, 12f).x != a.Sample(id, 50, 12f).x));
            // the bolt runs away from the threat
            var p0 = a.Sample(0, 10, 12f);
            Assert.IsTrue(a.BoltSpot(p0.x, p0.z, p0.x + 0.1f, p0.z, out float bx, out float bz));
            Assert.Greater(Terrain.Dist(bx, bz, p0.x + 0.1f, p0.z), Terrain.Dist(p0.x, p0.z, p0.x + 0.1f, p0.z));
        }

        [Test]
        public void DeerHerd_TheDawnWalk_OnlyWithAPond()
        {
            var sim = Camp(7, 30, pond: false);
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            sim.DebugDeerToCap();
            var dry = new DeerHerd.Field(); sim.EcoDeerRefresh(dry); dry.Sync(8);
            for (double t = 0; t < 200; t += 3) foreach (int id in dry.Alive) Assert.AreNotEqual(DeerAct.Drink, dry.Sample(id, t, 6f).act, "no pond: no dawn walk");
            Pond(sim);
            var wet = new DeerHerd.Field(); sim.EcoDeerRefresh(wet); wet.Sync(8);
            Assert.IsTrue(wet.HasPond);
            var e = sim.State.eco; int drank = 0;
            for (double t = 0; t < 200; t += 3)
                foreach (int id in wet.Alive)
                {
                    var p = wet.Sample(id, t, 6f);
                    if (p.act != DeerAct.Drink) continue;
                    drank++;
                    int c = SiteMap.CellAt(p.x, p.z);
                    var nb = new int[8]; int n = EcoLand.Neighbours8(c, nb);
                    Assert.IsTrue(Enumerable.Range(0, n).Any(k => e.cover[nb[k]] == (byte)EcoCover.Water), "at the water's edge");
                }
            Assert.Greater(drank, 0, "the does drink at dawn");
            for (double t = 0; t < 200; t += 3) foreach (int id in wet.Alive) Assert.AreNotEqual(DeerAct.Drink, wet.Sample(id, t, 12f).act, "only at dawn");
        }

        // ================================================================ #421 the ducks and the fish
        [Test]
        public void Ducks_APairPlaced_ClutchesInSpring_HalfGrowUp_CapEight()
        {
            var sim = Camp(7, 0);
            Assert.AreEqual(0, sim.EcoDucks);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Ducks), 0);
            Assert.AreEqual(2, sim.EcoDucks, "a pair");
            Assert.AreEqual(sim.LifePlacedCell(LifeSpecies.Ducks), sim.EcoDuckHome);
            ToSpring(sim);
            Assert.That(sim.EcoDuckEggs, Is.InRange(3, 5), "one clutch of 3-5");
            int eggs = sim.EcoDuckEggs;
            Assert.AreEqual(0, sim.EggSurplus, "the pair is the floor: no eggs are taken");
            Assert.IsFalse(sim.TakeEgg());
            while (sim.State.month % 12 != 7) sim.Step();
            Assert.AreEqual(0, sim.EcoDuckEggs, "hatched at the summer step");
            Assert.AreEqual(eggs, sim.EcoDucklings);
            Assert.AreEqual(2 + (int)MathF.Round(eggs * 0.5f), sim.EcoDucks, "about half grow up");
            Assert.IsTrue(sim.ArrowsAreFletched, "the first summer's feathers");
            for (int y = 0; y < 8; y++) { Seasons(sim, 4); Assert.LessOrEqual(sim.EcoDucks, 8); }
            Assert.AreEqual(8, sim.EcoDucks, "the wild cap");
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.kind == "ducks_feathers"), "fletched once");
        }

        [Test]
        public void Eggs_TakenOnlyAboveTheFloor_NeverABird()
        {
            var sim = Camp(7, 0);
            sim.DebugPlaceNearCamp(LifeSpecies.Ducks);
            sim.State.eco.ducks = 3;
            ToSpring(sim);
            int eggs = sim.EcoDuckEggs, took = 0;
            Assert.Greater(eggs, 0);
            while (sim.TakeEgg()) took++;
            Assert.AreEqual(eggs, took, "every egg in the nests (the flock is above its floor)");
            Assert.AreEqual(3, sim.EcoDucks, "never a bird");
            Assert.AreEqual(took, sim.EcoEggsTaken);
            while (sim.State.month % 12 != 7) sim.Step();
            Assert.AreEqual(3, sim.EcoDucks, "no ducklings from emptied nests");
            Assert.IsFalse(sim.WolfTake(LifeSpecies.Fish), "the wolves don't fish");
            Assert.IsTrue(sim.WolfTake(LifeSpecies.Ducks), "a wolf may take a wild duck above the floor (#437)");
            Assert.IsFalse(sim.WolfTake(LifeSpecies.Ducks), "never below it");
            Assert.AreEqual(2, sim.EcoDucks);
        }

        [Test]
        public void Fish_ASchoolOfSix_GrowsToThePondsCap_FloorThree()
        {
            var sim = Camp(7, 0);
            Assert.IsFalse(sim.FishingOpen, "no fish placed");
            Assert.IsFalse(sim.FishCatch());
            Assert.AreEqual(0, sim.EcoFishShown());
            sim.DebugPlaceNearCamp(LifeSpecies.Ducks);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Fish), 0);
            Assert.IsTrue(sim.FishingOpen);
            Assert.AreEqual(6, sim.EcoFish);
            Assert.AreEqual(sim.LifePlacedCell(LifeSpecies.Fish), sim.EcoFishHome);
            int cap = Math.Max(6, sim.EcoPondCells / 3);
            Assert.AreEqual(cap, sim.EcoFishCap);
            Seasons(sim, 4);
            Assert.AreEqual(sim.EcoFishCap, sim.EcoFish, "full within a year");
            Assert.AreEqual(Math.Min(8, sim.EcoFish), sim.EcoFishShown());
            Assert.AreEqual(1f, sim.FishBiteRate, 1e-4f, "a full pond bites");
            int caught = 0;
            while (sim.FishCatch()) caught++;
            Assert.AreEqual(3, sim.EcoFish, "the floor");
            Assert.AreEqual(cap - 3, caught);
            Assert.AreEqual(0f, sim.FishBiteRate, "nothing bites at the floor");
            sim.State.eco.fish = 4;
            Assert.That(sim.FishBiteRate, Is.InRange(0.15f, 0.5f), "bites come slower near the floor");
        }

        [Test]
        public void TheReeds_ComeFirstWhereTheDucksWerePlaced()
        {
            var t = new EcoTuning();
            var sim = Camp(7, 0);
            int home = sim.DebugPlaceNearCamp(LifeSpecies.Ducks);
            var m = Model(sim);
            var boost = typeof(EcoModel).GetMethod("DuckReedBoost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.AreEqual(t.duckReedBoost, (float)boost.Invoke(m, new object[] { home }));
            int far = Enumerable.Range(0, EcoLand.N).First(c => Dist(c, SiteMap.CXc(home), SiteMap.CZc(home)) > 1.5f);
            Assert.AreEqual(1f, (float)boost.Invoke(m, new object[] { far }));
        }

        // ================================================================ #422 floors, shy, restraint, restock
        [Test]
        public void Shy_AtTheFloorAfterTakes_TheHomeMovesToTheFarSide()
        {
            var sim = Camp(7, 30);
            int placed = sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            sim.DebugDeerToCap();
            Assert.IsFalse(sim.LifeShy(LifeSpecies.Deer));
            Assert.AreEqual(placed, sim.LifeHomeCell(LifeSpecies.Deer));
            while (sim.DeerHuntCatch(out _)) { }
            Assert.IsTrue(sim.LifeShy(LifeSpecies.Deer), "at the floor after the hunt");
            var g = sim.EcoGround;
            int shy = sim.EcoDeerHome;
            Assert.AreNotEqual(placed, shy);
            Assert.Greater(Dist(shy, g.campX, g.campZ), Dist(placed, g.campX, g.campZ), "the far side, away from the camp");
            Assert.IsTrue(sim.LifeGroundOk(LifeSpecies.Deer, shy) || sim.State.eco.cover[shy] != (byte)EcoCover.Water, "still deer ground");
            // a founding pair is small, not shy
            var r = Camp(7, 0);
            Assert.AreEqual(2, r.EcoRabbitCount);
            Assert.IsFalse(r.LifeShy(LifeSpecies.Rabbits), "nothing was taken");
            Assert.IsFalse(r.LifeShy(LifeSpecies.Ducks), "none placed");
            // back above the floor: home again
            sim.DebugDeerToCap();
            Assert.AreEqual(placed, sim.EcoDeerHome);
        }

        [Test]
        public void Restraint_OncePerSpecies_ThenTheTakePausesInSpring()
        {
            var sim = Camp(7, 30);
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            var ruled = new List<LifeSpecies>(); sim.LifeRuled += ruled.Add;
            while (sim.State.month % 12 != 7) sim.Step(); // summer: no fawns to refill the herd for a while
            while (sim.DeerHuntCatch(out _)) { }
            Assert.AreEqual(2, sim.EcoDeerCount);
            Assert.AreEqual(-1, sim.LifeRuledMonth(LifeSpecies.Deer), "not yet: one season at the floor first");
            for (int k = 0; k < 2; k++) Season(sim);
            CollectionAssert.AreEqual(new[] { LifeSpecies.Deer }, ruled, "a season at the floor: a notable rules");
            Assert.GreaterOrEqual(sim.LifeRuledMonth(LifeSpecies.Deer), 0);
            var line = sim.State.chronicle.Last(x => x.kind == SimCore.RulingKind);
            StringAssert.Contains("leave the does in spring", line.text);
            for (int y = 0; y < 3; y++)
            {
                ToSpring(sim);
                Assert.IsTrue(sim.LifeTakePaused(LifeSpecies.Deer), "spring");
                Assert.AreEqual(0, sim.DeerHuntSurplus);
                Assert.IsFalse(sim.DeerHuntCatch(out _), "nobody hunts deer in spring");
                while (sim.State.month % 12 != 7) sim.Step();
                Assert.IsFalse(sim.LifeTakePaused(LifeSpecies.Deer), "summer");
                while (sim.DeerHuntCatch(out _)) { }
                Season(sim); Season(sim);
            }
            Assert.AreEqual(1, ruled.Count, "once per species");
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.kind == SimCore.RulingKind));
        }

        [Test]
        public void Restraint_TheMomentIsHeldForTheKeeper()
        {
            var cfg = new SimConfig { ecology = true, moments = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            LifeJarTests.Meadow(sim, 0.3f);
            sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
            sim.DebugPlaceNearCamp(LifeSpecies.Rabbits);
            Assert.IsTrue(sim.DebugRestraintNow(LifeSpecies.Rabbits));
            Assert.IsFalse(sim.DebugRestraintNow(LifeSpecies.Rabbits), "once");
            var e = sim.State.chronicle.Last(x => x.kind == SimCore.RulingKind);
            StringAssert.Contains("leave the young ones in spring", e.text);
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m, "the ruling is staged as a held moment");
            Assert.AreEqual(MomentKind.Ruling, m.kind);
            Assert.AreEqual(e.notableId, m.personA, "the notable who ruled");
            Assert.IsTrue(sim.MomentHidesCard(e), "seen in the box, not a card");
            // what the view's own ruling beat needs (2026-11-08): who rules, the species, the line
            Assert.IsTrue(sim.RulingOf(m, out var r));
            Assert.AreEqual(LifeSpecies.Rabbits, r.species);
            Assert.AreEqual(e.notableId, r.rulerId);
            Assert.AreEqual(e.text, r.line);
            Assert.AreEqual(e.id, r.entryId);
            Assert.AreEqual(sim.LifeRuledMonth(LifeSpecies.Rabbits), r.month);
            if (r.rulerId >= 0) StringAssert.Contains(r.rulerName, r.line);
            Assert.IsTrue(sim.RulingOn(LifeSpecies.Rabbits, out var r2));
            Assert.AreEqual(r.entryId, r2.entryId);
            Assert.IsFalse(sim.RulingOn(LifeSpecies.Deer, out _), "no ruling on the deer yet");
            Assert.IsFalse(sim.RulingOfEntry(sim.State.chronicle.First(x => x.kind != SimCore.RulingKind).id, out _), "not a ruling line");
            Assert.AreEqual(1, sim.State.moments.Count(x => x.kind == MomentKind.Ruling), "one ruling moment");
            Assert.IsFalse(sim.DebugRestraintNow(LifeSpecies.Thicket), "the thicket is never taken from");
        }

        [Test]
        public void Restraint_TheDucks_AfterASpringWhoseNestsWereEmptied()
        {
            var sim = Camp(7, 0);
            sim.DebugPlaceNearCamp(LifeSpecies.Ducks);
            sim.State.eco.ducks = 4;
            ToSpring(sim);
            while (sim.TakeEgg()) { }
            while (sim.State.month % 12 != 7) sim.Step();
            Assert.GreaterOrEqual(sim.LifeRuledMonth(LifeSpecies.Ducks), 0);
            StringAssert.Contains("leave the nests till summer", sim.State.chronicle.Last(x => x.kind == SimCore.RulingKind).text);
            ToSpring(sim);
            Assert.Greater(sim.EcoDuckEggs, 0);
            Assert.AreEqual(0, sim.EggSurplus);
            Assert.IsFalse(sim.TakeEgg(), "the nests are left in spring");
        }

        [Test]
        public void Restock_TwoYearsAtTheFloor_ReopensTheJar_ForDeerDucksAndFish()
        {
            foreach (var s in new[] { LifeSpecies.Deer, LifeSpecies.Ducks, LifeSpecies.Fish })
            {
                var sim = Camp(7, 13);
                if (s == LifeSpecies.Fish) sim.DebugPlaceNearCamp(LifeSpecies.Ducks);
                Assert.IsTrue(sim.DebugToFloor(s), s.ToString());
                Assert.IsTrue(sim.LifeShy(s));
                int restocks = sim.JarRestocks(s);
                // held at the floor two game years (taken back down every season, as a heavy taker would)
                for (int k = 0; k < 9 && sim.JarStateOf(s) == JarState.Placed; k++) { Season(sim); if (sim.JarStateOf(s) == JarState.Placed) sim.DebugToFloor(s); }
                Assert.AreEqual(JarState.Ready, sim.JarStateOf(s), s + ": the jar opens again");
                Assert.AreEqual(restocks + 1, sim.JarRestocks(s));
                float before = sim.LifePopulation(s);
                Assert.IsTrue(sim.PlaceLife(s, sim.LifePlaceCellNearCamp(s)));
                float group = s == LifeSpecies.Deer ? 3 : s == LifeSpecies.Ducks ? 2 : 6;
                Assert.AreEqual(before + group, sim.LifePopulation(s), s + ": another founding group");
            }
        }

        // ================================================================ the save
        [Test]
        public void SaveRoundTrip_KeepsThePlacedStocks_AndAnOldSaveLoadsWithNone()
        {
            var sim = Camp(7, 30);
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            sim.DebugPlaceNearCamp(LifeSpecies.Ducks);
            sim.DebugPlaceNearCamp(LifeSpecies.Fish);
            sim.DebugDeerToCap();
            sim.DebugDeerHuntNow();
            sim.DebugWhiteStagNow();
            sim.DebugEggsNow(); sim.TakeEgg();
            sim.FishCatch();
            sim.DebugRestraintNow(LifeSpecies.Fish);
            sim.DeerHuntSettled(5);
            string json = JsonUtility.ToJson(sim.State);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            Assert.AreEqual(json, JsonUtility.ToJson(loaded.State), "loading changes nothing");
            Assert.AreEqual(sim.EcoDeerCount, loaded.EcoDeerCount); Assert.AreEqual(sim.EcoDeerStags, loaded.EcoDeerStags);
            Assert.AreEqual(sim.EcoDeerHome, loaded.EcoDeerHome); Assert.AreEqual(sim.DeerAntlersUp, loaded.DeerAntlersUp);
            Assert.AreEqual(WhiteStag.Seen, loaded.WhiteStagState);
            Assert.AreEqual(sim.EcoDucks, loaded.EcoDucks); Assert.AreEqual(sim.EcoDuckEggs, loaded.EcoDuckEggs); Assert.AreEqual(sim.EcoEggsTaken, loaded.EcoEggsTaken);
            Assert.AreEqual(sim.EcoFish, loaded.EcoFish); Assert.AreEqual(sim.EcoFishCaught, loaded.EcoFishCaught);
            Assert.AreEqual(sim.LifeRuledMonth(LifeSpecies.Fish), loaded.LifeRuledMonth(LifeSpecies.Fish));
            Assert.AreEqual(1, loaded.DeerHuntsSettled(5));
            for (int k = 0; k < 8; k++) { Seasons(sim, 1); Seasons(loaded, 1); }
            Assert.AreEqual(JsonUtility.ToJson(sim.State.eco), JsonUtility.ToJson(loaded.State.eco), "and lives on the same");

            // a save from before #420-#422: none of the new fields at all
            var old = Camp(7, 30);
            string oldJson = JsonUtility.ToJson(old.State);
            foreach (var f in new[] { "deerStags", "deerDoes", "deerHome", "deerTaken", "deerFawns", "antlersMonth", "whiteStagMonth", "whiteStag",
                         "deerHuntDay", "deerHuntDone", "ducks", "duckHome", "duckEggs", "ducklings", "eggsTaken", "eggsTakenSpring", "ducksTaken",
                         "fletchedMonth", "fish", "fishHome", "fishCaught", "ruledMonth" })
                oldJson = System.Text.RegularExpressions.Regex.Replace(oldJson, "\"" + f + "\":-?\\d+,?", "");
            var back = new SimCore(old.Config, JsonUtility.FromJson<SimState>(oldJson));
            Assert.AreEqual(0, back.EcoDeerCount); Assert.AreEqual(0, back.EcoDucks); Assert.AreEqual(0, back.EcoFish);
            Assert.AreEqual(-1, back.EcoDeerHome); Assert.IsFalse(back.DeerAntlersUp); Assert.AreEqual(WhiteStag.None, back.WhiteStagState);
            Assert.AreEqual(-1, back.LifeRuledMonth(LifeSpecies.Deer));
            Seasons(back, 4);
            Assert.AreEqual(0, back.EcoDeerCount, "nothing arrives by itself");
        }

        // ================================================================ debug
        [Test]
        public void TheDebugHelpers_ReachEveryNewState()
        {
            var sim = Camp(7, 30);
            Assert.IsTrue(sim.DebugDeerHuntNow(), "places the deer, then a hit");
            Assert.Greater(sim.DebugDeerToCap(), 2);
            Assert.IsTrue(sim.DebugWhiteStagNow());
            Assert.Greater(sim.DebugEggsNow(), 0);
            Assert.Greater(sim.EggSurplus, 0, "eggs that can be taken");
            foreach (var s in new[] { LifeSpecies.Rabbits, LifeSpecies.Deer, LifeSpecies.Ducks, LifeSpecies.Fish })
            {
                Assert.IsTrue(sim.DebugToFloor(s), s.ToString());
                Assert.AreEqual(SimCore.LifeFloor(s), sim.LifePopulation(s), 1e-4f, s.ToString());
                Assert.IsTrue(sim.LifeShy(s), s + " shy");
                Assert.IsTrue(sim.DebugRestraintNow(s), s + " restraint");
            }
            Assert.IsFalse(sim.DebugToFloor(LifeSpecies.Thicket));
            Assert.IsFalse(sim.DebugToFloor(LifeSpecies.Wolves));
            var off = new SimCore(new SimConfig(), SimCore.NewWorld(3, new SimConfig()));
            Assert.IsFalse(off.DebugWhiteStagNow()); Assert.IsFalse(off.DebugDeerHuntNow()); Assert.AreEqual(0, off.DebugEggsNow());
            Assert.IsFalse(off.DebugToFloor(LifeSpecies.Deer)); Assert.IsFalse(off.DebugRestraintNow(LifeSpecies.Deer));
            Assert.IsTrue(off.FishingOpen, "the old box fishes as ever"); Assert.IsTrue(off.FishCatch());
        }
    }

    /// <summary>#420-#422 over the long run: a Keeper who places every jar, people who take all they can every month.</summary>
    public partial class EcoTests
    {
        [Test]
        public void FortyYears_HeavyTaking_NoSpeciesBelowItsFloor_CapsHold_RestraintOnce_SpringPause()
        {
            var sim = EcoWorld(7); var keeper = new Keeper(true);
            var ruled = new List<LifeSpecies>(); sim.LifeRuled += ruled.Add;
            bool fishedBeforeFish = false, villageBeforePond = false, tookInPausedSpring = false;
            var seen = new HashSet<LifeSpecies>();
            for (int month = 0; month < 40 * 12; month++)
            {
                if (month % 3 == 0) keeper.Play(sim);
                bool fish = sim.LifeIsPlaced(LifeSpecies.Fish);
                // the takers, every month, as much as they may
                for (int k = 0; k < 4; k++)
                {
                    bool r = sim.HuntCatch(), d = sim.DeerHuntCatch(out _), e = sim.TakeEgg(), f = sim.FishCatch();
                    if (f && !fish) fishedBeforeFish = true;
                    if ((r && sim.LifeTakePaused(LifeSpecies.Rabbits)) || (d && sim.LifeTakePaused(LifeSpecies.Deer))
                        || (e && sim.LifeTakePaused(LifeSpecies.Ducks)) || (f && sim.LifeTakePaused(LifeSpecies.Fish))) tookInPausedSpring = true;
                }
                if (!fish && (sim.FishingOpen || sim.JobOpen(JobType.Fisher))) fishedBeforeFish = true;
                sim.Step();
                if (sim.VillageGateOpen && !(sim.LifeIsPlaced(LifeSpecies.Ducks) && sim.LifeIsPlaced(LifeSpecies.Fish))) villageBeforePond = true;
                foreach (var s in new[] { LifeSpecies.Rabbits, LifeSpecies.Deer, LifeSpecies.Ducks, LifeSpecies.Fish })
                {
                    if (!sim.LifeIsPlaced(s)) continue;
                    seen.Add(s);
                    float pop = s == LifeSpecies.Rabbits ? sim.EcoRabbitCount : sim.LifePopulation(s);
                    Assert.GreaterOrEqual(pop, SimCore.LifeFloor(s), $"{s} below its floor in month {sim.State.month}");
                }
                if (sim.State.month % 3 == 1)
                {
                    Assert.LessOrEqual(sim.EcoDeerCount, Math.Max(3, sim.EcoDeerCap), "the wood's cap");
                    Assert.LessOrEqual(sim.EcoDeerCount, 8);
                    Assert.LessOrEqual(sim.EcoDucks, 8, "the wild cap");
                    if (sim.EcoFish > 0) Assert.LessOrEqual(sim.EcoFish, sim.EcoFishCap, "the pond's cap");
                }
            }
            TestContext.Out.WriteLine($"placed {string.Join(",", seen)}; ruled {string.Join(",", ruled)}; village {sim.State.eco.villageGateMonth / 12f:0.0}; " +
                                      $"deer {sim.EcoDeerCount} taken {sim.EcoDeerTaken}, ducks {sim.EcoDucks} eggs {sim.EcoEggsTaken}, fish {sim.EcoFish} caught {sim.EcoFishCaught}");
            CollectionAssert.IsSupersetOf(seen, new[] { LifeSpecies.Rabbits, LifeSpecies.Deer, LifeSpecies.Ducks, LifeSpecies.Fish }, "a caring Keeper places them all");
            Assert.IsFalse(fishedBeforeFish, "no fishing of any kind before the fish");
            Assert.IsFalse(villageBeforePond, "the village waits on the ducks and the fish");
            Assert.IsFalse(tookInPausedSpring, "the spring pause holds");
            Assert.AreEqual(ruled.Count, ruled.Distinct().Count(), "a ruling once per species");
            CollectionAssert.IsSupersetOf(ruled, new[] { LifeSpecies.Deer, LifeSpecies.Fish }, "heavy takes bring the rulings");
            Assert.Greater(sim.EcoDeerTaken, 10); Assert.Greater(sim.EcoFishCaught, 10);
        }

        [Test]
        public void FortyYears_NoDucksOrFishWithoutPlacing_NoFishing_NoVillage()
        {
            var sim = EcoWorld(7); var keeper = new Keeper(true, which: s => s != LifeSpecies.Ducks && s != LifeSpecies.Fish);
            for (int s = 0; s < 40 * 4; s++)
            {
                keeper.Play(sim);
                sim.Advance(0.25);
                Assert.AreEqual(0, sim.EcoDucks); Assert.AreEqual(0, sim.EcoFish);
                Assert.IsFalse(sim.FishingOpen); Assert.IsFalse(sim.FishCatch()); Assert.IsFalse(sim.JobOpen(JobType.Fisher));
            }
            Assert.GreaterOrEqual(sim.EcoPondCells, sim.Config.eco.pondGateCells, "the pond is there");
            Assert.GreaterOrEqual(sim.EcoGrownTrees, sim.Config.eco.villageTrees, "the wood is there");
            Assert.IsFalse(sim.VillageGateOpen, "but no village without the ducks and the fish");
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Ducks), "the jar waits");
            Assert.AreEqual(Era.Camp, sim.State.era);
        }
    }
}
