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
    /// <summary>#417 the life jar (docs/design/placed-life.md §2, §3, §4.3, §9): readiness per species and the ladder, valid
    /// ground, placing once, the restock, the save and the old-save migration, the debug hooks.</summary>
    public class LifeJarTests
    {
        internal static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>A second model over the sim's own state, to drive readiness from the counts directly.</summary>
        static EcoModel Model(SimCore sim) => new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco);

        static int Cells(float frac) => (int)MathF.Ceiling(frac * EcoLand.N);

        /// <summary>Greens the dry cells nearest the camp until <paramref name="frac"/> of the land is meadow, then lives a season
        /// (the jars open at a season's end).</summary>
        internal static void Meadow(SimCore sim, float frac, bool step = true)
        {
            var e = sim.State.eco; var g = sim.EcoGround;
            var order = Enumerable.Range(0, EcoLand.N).Where(c => !g.InBasin(c))
                .OrderBy(c => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), g.campX, g.campZ)).ThenBy(c => c).ToList();
            int want = Cells(frac), have = e.cover.Count(v => v == (byte)EcoCover.Grass || v == (byte)EcoCover.Tall);
            foreach (int c in order)
            {
                if (have >= want) break;
                if (e.cover[c] != (byte)EcoCover.Bare) continue;
                e.cover[c] = (byte)EcoCover.Grass; have++;
                e.moisture[c] = Math.Max(e.moisture[c], 0.4f);
            }
            if (step) sim.DebugSkipSeasons(1);
        }

        static void SetPlaced(EcoModel m, LifeSpecies s, long month = 0)
        {
            var j = m.Jar(s);
            j.state = (int)JarState.Placed; j.placings = 1; j.placedMonth = month;
        }

        // ---------------------------------------------------------------- the start
        [Test]
        public void ANewBox_EveryJarIsShut()
        {
            var sim = World();
            foreach (var s in LifeJar.All)
            {
                Assert.AreEqual(JarState.Locked, sim.JarStateOf(s), s.ToString());
                Assert.IsFalse(sim.LifeIsPlaced(s));
                Assert.IsFalse(sim.JarWiggleDue(s));
                Assert.AreEqual(-1, sim.JarReadyMonth(s));
            }
            Assert.IsFalse(sim.AnyJarReady);
            Assert.AreEqual(LifeJar.All.Length, sim.State.eco.jars.Count, "one entry per species, Wolves included");
            Assert.IsTrue(sim.State.eco.jarOn);
        }

        [Test]
        public void EcologyOff_NoJars()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.AreEqual(JarState.Locked, sim.JarStateOf(LifeSpecies.Thicket));
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Thicket, 100));
            Assert.IsFalse(sim.PlaceLife(LifeSpecies.Thicket, 100));
            Assert.AreEqual(-1, sim.DebugPlaceNearCamp(LifeSpecies.Rabbits));
            Assert.AreEqual(0, sim.DebugJarsReady());
            var list = new List<LifeSpecies> { LifeSpecies.Fish }; sim.ReadyJars(list);
            Assert.AreEqual(0, list.Count);
        }

        // ---------------------------------------------------------------- readiness (spec §3) and the ladder (§2.3)
        [Test]
        public void Thicket_OpensAtTenPercentMeadow()
        {
            var sim = World(); var m = Model(sim); var e = sim.State.eco;
            e.cGrass = Cells(0.10f) - 2; m.UpdateJars(5);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Thicket), "under 10 %");
            e.cGrass = Cells(0.06f); e.cTall = Cells(0.04f); m.UpdateJars(6);
            Assert.AreEqual(JarState.Ready, m.JarStateOf(LifeSpecies.Thicket), "grass and tall grass count");
            Assert.AreEqual(6, m.Jar(LifeSpecies.Thicket).readyMonth);
            Assert.IsFalse(m.Jar(LifeSpecies.Thicket).readySeen, "the wiggle waits");
            CollectionAssert.AreEqual(new[] { LifeSpecies.Thicket }, m.newJars);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Rabbits), "the rabbits wait on the thicket");
        }

        [Test]
        public void Rabbits_NeedTheThicketPlaced_AndFifteenPercent()
        {
            var sim = World(); var m = Model(sim); var e = sim.State.eco;
            e.cGrass = Cells(0.30f); m.UpdateJars(1);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Rabbits), "no thicket placed yet");
            SetPlaced(m, LifeSpecies.Thicket);
            e.cGrass = Cells(0.15f) - 2; m.UpdateJars(2);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Rabbits), "under 15 %");
            e.cGrass = Cells(0.15f); m.UpdateJars(3);
            Assert.AreEqual(JarState.Ready, m.JarStateOf(LifeSpecies.Rabbits));
        }

        [Test]
        public void Deer_NeedRabbitsPlaced_AndFiveGrownTrees()
        {
            var sim = World(); var m = Model(sim); var e = sim.State.eco;
            e.cTrees = 20; m.UpdateJars(1);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Deer), "the camp's rabbits first");
            SetPlaced(m, LifeSpecies.Thicket); SetPlaced(m, LifeSpecies.Rabbits);
            e.cTrees = 4; m.UpdateJars(2);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Deer));
            e.cTrees = 5; m.UpdateJars(3);
            Assert.AreEqual(JarState.Ready, m.JarStateOf(LifeSpecies.Deer), "5 grown trees, earlier than the village's 8");
        }

        [Test]
        public void Ducks_NeedRabbitsPlaced_AndAPondOfTwelve()
        {
            var sim = World(); var m = Model(sim); var e = sim.State.eco;
            e.cPond = 40; m.UpdateJars(1);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Ducks));
            SetPlaced(m, LifeSpecies.Thicket); SetPlaced(m, LifeSpecies.Rabbits);
            e.cPond = 11; m.UpdateJars(2);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Ducks));
            e.cPond = 12; m.UpdateJars(3);
            Assert.AreEqual(JarState.Ready, m.JarStateOf(LifeSpecies.Ducks));
        }

        [Test]
        public void Fish_NeedDucksPlacedSixMonths_AndSixReedCells()
        {
            var sim = World(); var m = Model(sim); var e = sim.State.eco;
            e.cReeds = 30; m.UpdateJars(1);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Fish));
            SetPlaced(m, LifeSpecies.Thicket); SetPlaced(m, LifeSpecies.Rabbits); SetPlaced(m, LifeSpecies.Ducks, 10);
            m.UpdateJars(15);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Fish), "5 months after the ducks");
            e.cReeds = 5; m.UpdateJars(16);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Fish), "5 reed cells");
            e.cReeds = 6; m.UpdateJars(16);
            Assert.AreEqual(JarState.Ready, m.JarStateOf(LifeSpecies.Fish));
        }

        [Test]
        public void Wolves_StayShut_Until437()
        {
            var sim = World(); var m = Model(sim); var e = sim.State.eco;
            foreach (var s in LifeJar.All) if (s != LifeSpecies.Wolves) SetPlaced(m, s);
            e.cTrees = 200; e.cGrass = 1000; e.cPond = 100; e.cReeds = 50;
            for (int month = 0; month < 120; month += 3) m.UpdateJars(month);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Wolves));
            Assert.IsFalse(m.GroundOk(LifeSpecies.Wolves, SiteMap.Index(30, 22)));
        }

        [Test]
        public void ReadyWhileAway_TheWiggleWaitsForTheNextOpen()
        {
            var sim = World();
            var opened = new List<LifeSpecies>();
            sim.LifeJarOpened += opened.Add;
            Meadow(sim, 0.12f, false);
            sim.CatchUp(2); // two real hours away: the season steps happen in catch-up
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Thicket));
            CollectionAssert.Contains(opened, LifeSpecies.Thicket);
            Assert.IsTrue(sim.JarWiggleDue(LifeSpecies.Thicket), "nothing was shown while away");
            Assert.IsTrue(sim.AnyJarReady);
            var ready = new List<LifeSpecies>(); sim.ReadyJars(ready);
            CollectionAssert.AreEqual(new[] { LifeSpecies.Thicket }, ready);
            Assert.IsTrue(sim.AckJarReady(LifeSpecies.Thicket));
            Assert.IsFalse(sim.AckJarReady(LifeSpecies.Thicket), "once");
            Assert.IsFalse(sim.JarWiggleDue(LifeSpecies.Thicket));
            Assert.IsTrue(sim.JarReady(LifeSpecies.Thicket), "the dot stays until it is placed");
            sim.CatchUp(6);
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Thicket), "a ready jar waits for the Keeper: nothing arrives by itself");
            Assert.AreEqual(1, opened.Count(s => s == LifeSpecies.Thicket), "opened once");
        }

        // ---------------------------------------------------------------- valid ground (spec §5)
        [Test]
        public void CanPlace_Thicket_OnMeadowAwayFromTheFire_AndClearOfTheSettlement()
        {
            var sim = World(); Meadow(sim, 0.3f);
            var g = sim.EcoGround; var e = sim.State.eco; var map = sim.Settlement.Map;
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Thicket));
            int near = sim.LifePlaceCellNearCamp(LifeSpecies.Thicket);
            Assert.GreaterOrEqual(near, 0, "valid ground exists");
            Assert.IsTrue(sim.CanPlaceLife(LifeSpecies.Thicket, near));
            Assert.GreaterOrEqual(Dist(near, g.campX, g.campZ), sim.Config.eco.bushCampMargin, "the camp margin");
            for (int c = 0; c < EcoLand.N; c++)
            {
                bool ok = sim.CanPlaceLife(LifeSpecies.Thicket, c);
                if (!ok) continue;
                Assert.IsTrue(e.cover[c] == (byte)EcoCover.Grass || e.cover[c] == (byte)EcoCover.Tall, "meadow only");
                Assert.GreaterOrEqual(Dist(c, g.campX, g.campZ), sim.Config.eco.bushCampMargin);
                Assert.AreEqual(0, map.lot[c]); Assert.IsFalse(map.Green(c));
                Assert.IsFalse(g.InBasin(c));
            }
            // too near the fire, on bare ground, on water, on a lot: no
            int fire = SiteMap.CellAt(g.campX + 0.3f, g.campZ);
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Thicket, fire));
            int lotCell = Enumerable.Range(0, EcoLand.N).First(c => map.lot[c] != 0);
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Thicket, lotCell));
            e.cover[near] = (byte)EcoCover.Bare;
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Thicket, near), "bare ground");
            e.cover[near] = (byte)EcoCover.Grass;
            int basin = g.main.cells[g.main.cells.Count / 2];
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Thicket, basin), "the hollow");
            // a cell whose disc reaches the hollow is refused too
            int edge = Enumerable.Range(0, EcoLand.N).FirstOrDefault(c => !g.InBasin(c) && e.cover[c] == (byte)EcoCover.Grass
                && g.main.cells.Any(b => Dist(c, SiteMap.CXc(b), SiteMap.CZc(b)) < 0.2f));
            if (edge > 0) Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Thicket, edge), "the whole disc on dry land");
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Rabbits, near), "its jar is shut");
        }

        [Test]
        public void CanPlace_Rabbits_Deer_Ducks_Fish()
        {
            var sim = World(); Meadow(sim, 0.3f);
            var g = sim.EcoGround; var e = sim.State.eco;
            sim.DebugJarsReady();
            // rabbits: meadow outside the camp margin
            int r = sim.LifePlaceCellNearCamp(LifeSpecies.Rabbits);
            Assert.GreaterOrEqual(r, 0);
            Assert.IsTrue(sim.CanPlaceLife(LifeSpecies.Rabbits, r));
            Assert.GreaterOrEqual(Dist(r, g.campX, g.campZ), sim.Config.eco.bushCampMargin);
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Rabbits, SiteMap.CellAt(g.campX + 0.5f, g.campZ)), "inside the camp margin");
            // deer: within 0.3 u of a grown tree, outside the margin
            bool DryBlock(int c)
            {
                int ci = SiteMap.I(c), cj = SiteMap.J(c);
                if (ci < 5 || cj < 5 || ci > 52 || cj > 34) return false;
                for (int j = cj - 1; j <= cj + 8; j++) for (int i = ci - 4; i <= ci + 6; i++) if (g.InBasin(SiteMap.Index(i, j))) return false;
                return true;
            }
            int tree = Enumerable.Range(0, EcoLand.N).First(c => Dist(c, g.campX, g.campZ) > 1.6f && DryBlock(c));
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Deer, tree), "no tree yet");
            e.cover[tree] = (byte)EcoCover.Tree;
            int beside = tree + 2, far = tree + 5;
            Assert.IsTrue(sim.CanPlaceLife(LifeSpecies.Deer, beside), "0.2 u from a grown tree");
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Deer, far), "0.5 u away");
            // ducks at the pond's edge, fish away from it
            int i0 = SiteMap.I(tree) - 2, j0 = SiteMap.J(tree) + 2;
            e.cover[tree] = (byte)EcoCover.Grass;
            for (int j = j0; j < j0 + 5; j++) for (int i = i0; i < i0 + 5; i++) e.cover[SiteMap.Index(i, j)] = (byte)EcoCover.Water;
            int corner = SiteMap.Index(i0, j0), mid = SiteMap.Index(i0 + 2, j0 + 2);
            Assert.IsTrue(sim.CanPlaceLife(LifeSpecies.Ducks, corner), "an edge water cell");
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Ducks, mid), "not the middle");
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Ducks, SiteMap.Index(i0 - 1, j0)), "not dry land");
            Assert.IsTrue(sim.CanPlaceLife(LifeSpecies.Fish, mid), "the deeper part");
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Fish, corner), "not at the edge");
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Wolves, mid));
            Assert.AreEqual(-1, sim.LifePlaceCellNearCamp(LifeSpecies.Wolves), "the den: #437");
            // out of range
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Rabbits, -1));
            Assert.IsFalse(sim.CanPlaceLife(LifeSpecies.Rabbits, EcoLand.N));
            var ground = new List<int>(); sim.LifeGround(LifeSpecies.Fish, ground);
            CollectionAssert.AreEqual(new[] { SiteMap.Index(i0 + 1, j0 + 1), SiteMap.Index(i0 + 2, j0 + 1), SiteMap.Index(i0 + 3, j0 + 1),
                SiteMap.Index(i0 + 1, j0 + 2), mid, SiteMap.Index(i0 + 3, j0 + 2), SiteMap.Index(i0 + 1, j0 + 3), SiteMap.Index(i0 + 2, j0 + 3), SiteMap.Index(i0 + 3, j0 + 3) }, ground);
        }

        static float Dist(int c, float x, float z) => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);

        // ---------------------------------------------------------------- placing (spec §6)
        [Test]
        public void Place_OncePerOpenJar_RecordsAndTells()
        {
            var sim = World(); Meadow(sim, 0.3f);
            var placed = new List<LifePlacedInfo>();
            sim.LifePlaced += placed.Add;
            int c = sim.LifePlaceCellNearCamp(LifeSpecies.Thicket);
            Assert.IsFalse(sim.PlaceLife(LifeSpecies.Thicket, SiteMap.CellAt(sim.Settlement.Map.greenX, sim.Settlement.Map.greenZ)), "not on the green");
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Thicket, c));
            Assert.AreEqual(JarState.Placed, sim.JarStateOf(LifeSpecies.Thicket));
            Assert.IsTrue(sim.LifeIsPlaced(LifeSpecies.Thicket));
            Assert.AreEqual(c, sim.LifePlacedCell(LifeSpecies.Thicket));
            Assert.AreEqual(sim.State.month, sim.LifePlacedMonth(LifeSpecies.Thicket));
            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(LifeSpecies.Thicket, placed[0].species); Assert.AreEqual(c, placed[0].cell); Assert.IsFalse(placed[0].restock);
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.kind == "life_thicket"), "a history line");
            Assert.IsTrue(sim.LifeArrivalPending(LifeSpecies.Thicket));
            Assert.IsTrue(sim.AckLifeArrival(LifeSpecies.Thicket));
            Assert.IsFalse(sim.AckLifeArrival(LifeSpecies.Thicket));
            // once only: the jar is placed now
            int other = sim.LifePlaceCellNearCamp(LifeSpecies.Thicket);
            Assert.IsFalse(sim.PlaceLife(LifeSpecies.Thicket, other >= 0 ? other : c));
            Assert.AreEqual(1, placed.Count);
            Assert.IsFalse(sim.DebugJarAgain(LifeSpecies.Thicket), "the thicket never restocks");
            // the next rung opens on the same placing when the land is already ready (30 % meadow)
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Rabbits));
        }

        // ---------------------------------------------------------------- restock (spec §4.3)
        [Test]
        public void Restock_AtTheFloorForTwoYears_OpensTheJarAgain_Once()
        {
            var sim = World(); var m = Model(sim); var e = sim.State.eco;
            SetPlaced(m, LifeSpecies.Thicket); SetPlaced(m, LifeSpecies.Rabbits);
            e.rabbits = 6f; m.UpdateJars(0);
            Assert.AreEqual(-1, m.Jar(LifeSpecies.Rabbits).atFloorSince);
            e.rabbits = 2f; m.UpdateJars(3);
            Assert.AreEqual(3, m.Jar(LifeSpecies.Rabbits).atFloorSince);
            e.rabbits = 4f; m.UpdateJars(12);
            Assert.AreEqual(-1, m.Jar(LifeSpecies.Rabbits).atFloorSince, "back above the floor: the spell ends");
            e.rabbits = 2f;
            for (long month = 15; month < 39; month += 3) { m.UpdateJars(month); Assert.AreEqual(JarState.Placed, m.JarStateOf(LifeSpecies.Rabbits), "month " + month); }
            m.UpdateJars(39);
            Assert.AreEqual(JarState.Ready, m.JarStateOf(LifeSpecies.Rabbits), "24 months at the floor");
            Assert.AreEqual(1, m.Jar(LifeSpecies.Rabbits).restocks);
            Assert.IsFalse(m.Jar(LifeSpecies.Rabbits).readySeen, "it wiggles again");
            Assert.IsTrue(m.IsPlaced(LifeSpecies.Rabbits), "still counts as placed for the ladder");
            for (long month = 42; month < 120; month += 3) m.UpdateJars(month);
            Assert.AreEqual(1, m.Jar(LifeSpecies.Rabbits).restocks, "once per floor spell: an open jar waits");
            // the thicket never restocks
            Assert.AreEqual(JarState.Placed, m.JarStateOf(LifeSpecies.Thicket));
        }

        [Test]
        public void Restock_ThroughTheSim_PlacesAnotherGroup()
        {
            var sim = World(); Meadow(sim, 0.3f);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0);
            Assert.IsTrue(sim.DebugJarAgain(LifeSpecies.Rabbits));
            Assert.AreEqual(JarState.Ready, sim.JarStateOf(LifeSpecies.Rabbits));
            Assert.AreEqual(1, sim.JarRestocks(LifeSpecies.Rabbits));
            var placed = new List<LifePlacedInfo>(); sim.LifePlaced += placed.Add;
            int c = sim.LifePlaceCellNearCamp(LifeSpecies.Rabbits);
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Rabbits, c));
            Assert.IsTrue(placed[0].restock);
            Assert.AreEqual(1, sim.State.chronicle.Count(x => x.kind == "life_rabbits" && x.text.Contains("grown few")));
        }

        // ---------------------------------------------------------------- save (spec §9)
        [Test]
        public void SaveRoundTrip_KeepsEveryJar()
        {
            var sim = World(); Meadow(sim, 0.3f);
            sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
            sim.DebugJarsReady();
            sim.AckJarReady(LifeSpecies.Ducks);
            string json = JsonUtility.ToJson(sim.State);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            foreach (var s in LifeJar.All)
            {
                Assert.AreEqual(sim.JarStateOf(s), loaded.JarStateOf(s), s.ToString());
                Assert.AreEqual(sim.JarWiggleDue(s), loaded.JarWiggleDue(s));
                Assert.AreEqual(sim.LifePlacedCell(s), loaded.LifePlacedCell(s));
                Assert.AreEqual(sim.JarReadyMonth(s), loaded.JarReadyMonth(s));
            }
            Assert.AreEqual(json, JsonUtility.ToJson(loaded.State), "loading changes nothing");
            for (int k = 0; k < 8; k++) { sim.DebugSkipSeasons(1); loaded.DebugSkipSeasons(1); }
            Assert.AreEqual(JsonUtility.ToJson(sim.State.eco), JsonUtility.ToJson(loaded.State.eco), "and lives on the same");
        }

        [Test]
        public void ASaveFromBeforeASpecies_LoadsWithItShut()
        {
            var sim = World(); Meadow(sim, 0.3f);
            sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
            var st = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            st.eco.jars.RemoveAll(j => j.species == nameof(LifeSpecies.Wolves) || j.species == nameof(LifeSpecies.Fish));
            st.eco.jars.Add(new LifeJarEntry { species = "Unicorns", state = 2 }); // a species this build does not know: ignored, kept
            var loaded = new SimCore(sim.Config, st);
            Assert.AreEqual(JarState.Placed, loaded.JarStateOf(LifeSpecies.Thicket));
            Assert.AreEqual(JarState.Locked, loaded.JarStateOf(LifeSpecies.Wolves));
            Assert.AreEqual(JarState.Locked, loaded.JarStateOf(LifeSpecies.Fish));
            loaded.DebugSkipSeasons(2);
            Assert.AreEqual(LifeJar.All.Length + 1, loaded.State.eco.jars.Count);
        }

        [Test]
        public void AnOldSave_RabbitsAndBushesCountAsPlaced()
        {
            var sim = World(); Meadow(sim, 0.3f);
            var e = sim.State.eco; var g = sim.EcoGround;
            int warren = Enumerable.Range(0, EcoLand.N).First(c => e.cover[c] == (byte)EcoCover.Grass && Dist(c, g.campX, g.campZ) > 1.2f);
            e.rabbits = 5f; e.rabbitArrivalCell = warren;
            e.milestoneKeys.Add(EcoMilestones.FirstRabbit); e.milestoneMonths.Add(4);
            int bush = Enumerable.Range(0, EcoLand.N).Where(c => e.cover[c] == (byte)EcoCover.Grass && Dist(c, g.campX, g.campZ) > 1.6f).First();
            e.cover[bush] = (byte)EcoCover.Bush;
            // as saved before the jar: no jar fields at all
            e.jarOn = false; e.jars = null;
            var old = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            var loaded = new SimCore(sim.Config, old);
            Assert.AreEqual(JarState.Placed, loaded.JarStateOf(LifeSpecies.Rabbits));
            Assert.AreEqual(warren, loaded.LifePlacedCell(LifeSpecies.Rabbits));
            Assert.AreEqual(4, loaded.LifePlacedMonth(LifeSpecies.Rabbits));
            Assert.IsFalse(loaded.LifeArrivalPending(LifeSpecies.Rabbits), "no arrival replays for an old herd");
            Assert.AreEqual(JarState.Placed, loaded.JarStateOf(LifeSpecies.Thicket));
            Assert.GreaterOrEqual(loaded.LifePlacedCell(LifeSpecies.Thicket), 0);
            Assert.AreEqual(loaded.LifePlacedCell(LifeSpecies.Thicket), loaded.State.eco.thicketA, "#418: the one thicket stands there");
            Assert.Greater(loaded.BushThicketZone(loaded.State.eco.thicketA), 0f);
            Assert.AreEqual(JarState.Locked, loaded.JarStateOf(LifeSpecies.Deer), "jars open for the rest");
            Assert.IsTrue(loaded.State.eco.jarOn);
            // a box with nothing in it migrates to every jar shut
            var bare = World(5);
            bare.State.eco.jarOn = false; bare.State.eco.jars = null;
            var b2 = new SimCore(bare.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(bare.State)));
            foreach (var s in LifeJar.All) Assert.AreEqual(JarState.Locked, b2.JarStateOf(s));
        }

        // ---------------------------------------------------------------- #418 the thicket (spec §5.1)
        [Test]
        public void PlacingTheThicket_ThreeFounders_TheAreaReservedAtOnce_TheSeedAfter()
        {
            var sim = World(); Meadow(sim, 0.3f);
            var e = sim.State.eco; var map = sim.Settlement.Map;
            Assert.AreEqual(0, e.cover.Count(v => v == (byte)EcoCover.Bush), "no bush before the thicket");
            Assert.IsNull(map.reserved, "nothing reserved yet");
            int c = sim.LifePlaceCellNearCamp(LifeSpecies.Thicket);
            Assert.IsTrue(sim.PlaceLife(LifeSpecies.Thicket, c));
            Assert.AreEqual(c, e.thicketA); Assert.AreEqual(sim.Config.eco.thicketRadius, e.thicketRadius); Assert.AreEqual(-1, e.thicketB);
            var bushes = Enumerable.Range(0, EcoLand.N).Where(x => e.cover[x] == (byte)EcoCover.Bush).ToList();
            Assert.AreEqual(sim.Config.eco.thicketFounders, bushes.Count, "the founding group: 3 bushes");
            Assert.Contains(c, bushes, "one on the placed spot");
            foreach (int b in bushes) Assert.Greater(sim.BushThicketZone(b), 0f);
            Assert.IsTrue(e.Reached(EcoMilestones.FirstBush));
            Assert.AreEqual(c, sim.FirstBushCell);
            Assert.IsTrue(sim.BerriesGrow, "the gatherers' berry job can start");
            int area = 0;
            for (int x = 0; x < EcoLand.N; x++)
            {
                if (sim.BushThicketZone(x) <= 0f) continue;
                area++;
                Assert.IsTrue(map.Reserved(x) && map.NoBuild(x), "reserved like water, at once");
            }
            Assert.Greater(area, 30);
            // the seed waits on the camp as before, and now on the thicket (not on wild bushes)
            Assert.IsFalse(e.Reached(EcoMilestones.SeedUnlocked));
            var noThicket = World(); Meadow(noThicket, 0.3f);
            noThicket.DebugOpenCampGate(); noThicket.DebugSkipSeasons(1);
            Assert.IsFalse(noThicket.State.eco.Reached(EcoMilestones.SeedUnlocked), "camp but no thicket: no seed");
            sim.DebugOpenCampGate(); sim.DebugSkipSeasons(1);
            Assert.IsTrue(e.Reached(EcoMilestones.SeedUnlocked), "camp and thicket: the seed");
            Assert.IsTrue(sim.CanPlantTree);
            // the area keeps its reservation across a save
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.IsTrue(loaded.Settlement.Map.Reserved(c));
        }

        // ---------------------------------------------------------------- debug (spec §10)
        [Test]
        public void Debug_ReadyEverySpecies_PlaceNearCamp_JarAgain()
        {
            var sim = World(); Meadow(sim, 0.3f);
            int opened = sim.DebugJarsReady();
            Assert.AreEqual(LifeJar.All.Length - 1, opened, "the thicket was already open");
            foreach (var s in LifeJar.All) Assert.AreEqual(JarState.Ready, sim.JarStateOf(s));
            Assert.AreEqual(0, sim.DebugJarsReady(), "nothing left shut");
            int c = sim.DebugPlaceNearCamp(LifeSpecies.Rabbits);
            Assert.GreaterOrEqual(c, 0);
            Assert.AreEqual(JarState.Placed, sim.JarStateOf(LifeSpecies.Rabbits));
            Assert.AreEqual(-1, sim.DebugPlaceNearCamp(LifeSpecies.Wolves), "no den ground before #437");
            // place a shut jar's species: it opens first
            var s2 = World(); Meadow(s2, 0.3f);
            Assert.GreaterOrEqual(s2.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0, "the cheat skips the ladder");
            Assert.IsTrue(s2.DebugJarAgain(LifeSpecies.Rabbits));
            Assert.IsFalse(s2.DebugJarAgain(LifeSpecies.Deer), "not placed");
        }
    }
}
