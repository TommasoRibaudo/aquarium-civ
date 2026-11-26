using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #726 / #730 / #731 (landscaping.md 8, phase 2: the view) on a portrait phone, by touch through the real HUD: the life row shows the earned
    /// seeds and bushes with their counts; an earned seed is planted with the ghost and a second tap; an earned bush is refused inside a thicket
    /// (the toast says why, #664), then placed with the ghost and a second tap (a second small thicket, the count goes down); the plucking tool
    /// in the kit's rail lifts a seedling and a bush. The cheats only reach the state (the Keeper's tree, the first thicket, a civ step).
    /// Shots go to Temp/agent/731-*.png.
    /// </summary>
    [Category("Landscaping")]
    public class LandscapingPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        IEnumerator Living()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (1.6f, 0.9f) }) EcoDebug.MistBig(x, z);
            yield return Frames(10);
        }

        /// <summary>The state the path starts from: the Keeper's tree stands, the first thicket is down, and one civ step paid a seed and a bush.</summary>
        IEnumerator Earned()
        {
            var hud = GameHud.Instance;
            Assert.IsTrue(Sim.DebugTreeStage(2), "the Keeper's tree");
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0, "the first thicket");
            Sim.DebugTakeFromPouch(Sim.SeedsInPouch, Sim.BushesInPouch);
            Assert.Greater(Sim.DebugNextCivStep(), 0, "a civ step pays");
            Assert.AreEqual((1, 1), (Sim.SeedsInPouch, Sim.BushesInPouch), "one seed and one bush earned");
            UnityEngine.Object.FindAnyObjectByType<BoxView>().Refresh();
            hud.SetHand(GameHud.KitTool.None);
            yield return Until(() => hud.IsVisible && !(PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)), 30f);
            if (hud.SheetOpen) hud.CloseSheet();
            yield return Frames(20);
        }

        IEnumerator OpenRow(GameHud hud)
        {
            if (hud.Hand != GameHud.KitTool.None) { yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean")); yield return Frames(10); }
            if (hud.OpenSheet != GameHud.Kit)
            {
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens the kit");
            // #668: no jar to open first: the tiles are on the kit's shelves
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(hud.LifeRowOpen, "the life jar opens its row");
        }

        /// <summary>#668: a ready tile's name and its count badge ("Seed ×1").</summary>
        static string ChoiceWord(GameHud hud, string name)
        {
            var t = hud.KitTileNamed(name);
            return t != null ? (t.Name + " " + t.CountText).Trim() : null;
        }

        /// <summary>A cell the earned seed would take as it is, its middle on screen clear of the HUD.</summary>
        static int SeedCellOnScreen()
        {
            var sp = SeedPlaceView.Instance; var mist = EcoMistView.Instance;
            var mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            int best = -1; float bestD = float.MaxValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (Sim.TreeCellFor(SiteMap.CXc(c), SiteMap.CZc(c)) != c) continue;
                var p = sp.ScreenOf(c);
                if (!Clear(p) || mist == null || !mist.GroundPoint(p, out var at) || SiteMap.CellAt(at.x, at.z) != c) continue;
                float d = (p - mid).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        static bool Clear(Vector2 p) => p.x > Screen.width * 0.12f && p.x < Screen.width * 0.88f && p.y > Screen.height * 0.22f && p.y < Screen.height * 0.8f && !UiInput.Over(p);

        /// <summary>A cell where the earned bush may go, nearest the camp, on screen.</summary>
        static int BushCellOnScreen()
        {
            var lpv = LifePlaceView.Instance; var mist = EcoMistView.Instance;
            var ground = new List<int>();
            Sim.LifeGround(LifeSpecies.Thicket, ground);
            var g = Sim.EcoGround;
            foreach (int c in ground.OrderBy(c => { float dx = SiteMap.CXc(c) - g.campX, dz = SiteMap.CZc(c) - g.campZ; return dx * dx + dz * dz; }))
            {
                if (!Sim.CanPlaceBush(c)) continue;
                var p = lpv.ScreenOf(c);
                if (!Clear(p) || mist == null || !mist.GroundPoint(p, out var at) || SiteMap.CellAt(at.x, at.z) != c) continue;
                return c;
            }
            return -1;
        }

        /// <summary>A cell where the tool would take <paramref name="kind"/>, the nearest at its own point, on screen.</summary>
        static int PluckCellOnScreen(PluckKind kind, int prefer = -1)
        {
            var tv = LandscapeToolView.Instance; var mist = EcoMistView.Instance;
            IEnumerable<int> cells = Enumerable.Range(0, SiteMap.N);
            if (prefer >= 0) cells = new[] { prefer }.Concat(cells);
            foreach (int c in cells)
            {
                if (Sim.EcoLandscapeKind(c) != kind) continue;
                var p = tv.ScreenOf(c);
                if (!Clear(p) || mist == null || !mist.GroundPoint(p, out var at)) continue;
                if (Sim.EcoNearestLandscape(at.x, at.z, tv.reach) != c) continue;
                return c;
            }
            return -1;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Landscaping_PlantASeed_PlaceABush_SeeARefusal_PluckWithTheTool_ByTouch()
        {
            yield return Living();
            yield return Earned();
            var hud = GameHud.Instance; var sp = SeedPlaceView.Instance; var lpv = LifePlaceView.Instance; var tv = LandscapeToolView.Instance;
            Assert.IsNotNull(sp); Assert.IsNotNull(lpv); Assert.IsNotNull(tv, "the plucking tool's view (its bootstrap)");

            // ---- #726: the life row shows the earned seed and bush, with their counts
            yield return OpenRow(hud);
            Assert.AreEqual("Seed ×1", ChoiceWord(hud, "kit-life-seed"), "the seed's count (#668: a badge on its tile)");
            Assert.AreEqual("Bushes ×1", ChoiceWord(hud, "kit-life-bush"), "the bush's count");
            yield return Shot("731-1-life-row-counts");

            // ---- #727: the earned seed: a tap shows the ghost, a tap on it plants a seedling
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-seed"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.KitTool.Seed, hud.Hand, "the earned seed in hand");
            int cell = SeedCellOnScreen();
            Assert.GreaterOrEqual(cell, 0, "open ground for the seed on screen");
            yield return TapAt(sp.ScreenOf(cell));
            yield return Frames(5);
            Assert.IsTrue(sp.GhostShown, "the ghost: " + sp.Last);
            Assert.AreEqual(1, Sim.SeedsInPouch, "a ghost spends nothing");
            yield return TapAt(sp.ScreenOf(sp.GhostCell));
            yield return Frames(10);
            Assert.AreEqual(0, Sim.SeedsInPouch, "the seed went in: " + sp.Last);
            Assert.AreEqual((byte)EcoCover.Seedling, Sim.State.eco.cover[cell], "a seedling where the ghost stood");
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "the seed is put down");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("731-2-seed-planted");

            // ---- #731: the earned bush: a tap inside the thicket is refused with its reason; then the ghost and a second tap
            yield return OpenRow(hud);
            Assert.IsNull(hud.Root.Q("kit-life-seed"), "no seed left in the row");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-bush"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.KitTool.Bush, hud.Hand, "the bush in hand");
            yield return Frames(5);
            Assert.Greater(lpv.GlowCells, 0, "its ground glows");
            var ths = new List<(int cell, float radius, int bushes)>(); Sim.BushThickets(ths);
            var bc = Camera.main.GetComponent<BoxCamera>();
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Until(() => !bc.IsFlying, 8f);
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            // the bush's camera frames free ground near the camp; the player pans to the old thicket to try there (a fly stands in for the pan)
            bc.FlyTo(box.transform.TransformPoint(box.land.OnGround(SiteMap.CXc(ths[0].cell), SiteMap.CZc(ths[0].cell))), Mathf.Min(bc.zoom, 0.6f));
            yield return Until(() => !bc.IsFlying, 8f);
            yield return Frames(5);
            var inThicket = lpv.ScreenOf(ths[0].cell);
            if (Clear(inThicket))
            {
                int refused0 = lpv.Refusals;
                yield return TapAt(inThicket);
                yield return Frames(5);
                Assert.AreEqual(refused0 + 1, lpv.Refusals, "a tap inside the thicket is refused: " + lpv.Last);
                Assert.AreEqual("Already a thicket here", lpv.LastRefusal);
                yield return Until(() => GameUiPanel.Toasts.Current == "Already a thicket here.", 3f);
                Assert.AreEqual("Already a thicket here.", GameUiPanel.Toasts.Current, "the refused tap says why");
                Assert.AreEqual(1, Sim.BushesInPouch, "a refusal spends nothing");
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Shot("731-3-bush-refused");
            }
            else Debug.LogWarning("[731] the first thicket is off screen: no refusal tap");
            int bushCell = BushCellOnScreen();
            if (bushCell < 0)
            {
                // pan back to the glow (a fly stands in for the pan): the valid ground nearest the camp
                int near = Sim.LifePlaceCellNearCamp(LifeSpecies.Thicket);
                Assert.GreaterOrEqual(near, 0, "valid ground for a bush somewhere");
                bc.FlyTo(box.transform.TransformPoint(box.land.OnGround(SiteMap.CXc(near), SiteMap.CZc(near))), Mathf.Min(bc.zoom, 0.6f));
                yield return Until(() => !bc.IsFlying, 8f);
                yield return Frames(5);
                bushCell = BushCellOnScreen();
            }
            Assert.GreaterOrEqual(bushCell, 0, "glowing ground for the bush on screen");
            yield return TapAt(lpv.ScreenOf(bushCell));
            yield return Frames(5);
            Assert.IsTrue(lpv.GhostShown, "the bush's ghost: " + lpv.Last);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Until(() => !bc.IsFlying, 8f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("731-4-bush-ghost");
            yield return TapAt(lpv.ScreenOf(lpv.GhostCell));
            yield return Frames(10);
            Assert.AreEqual(2, Sim.BushThicketCount, "a second small thicket: " + lpv.Last);
            Assert.AreEqual(0, Sim.BushesInPouch, "the count went down");
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "the bush is put down");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("731-5-bush-placed");

            // ---- #730: the plucking tool in the rail: a seedling, then a bush
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet);
            Assert.AreEqual(GameHud.LandscapeLabel, hud.KitTileNamed("kit-landscape").Name, "the tool's word: Spade (K2)");
            Assert.AreEqual("Spade", GameHud.LandscapeLabel);
            float toolsY = hud.Root.Q("kit-cloth").worldBound.yMin; // the landscaping polish: the tools never wrap to a second row
            foreach (var n in new[] { "kit-mister", "kit-tweezers", "kit-landscape" }) Assert.AreEqual(toolsY, hud.Root.Q(n).worldBound.yMin, 0.5f, n + " in the tools' one row");
            yield return Shot("731-6-kit-rail");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-landscape"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.KitTool.Landscape, hud.Hand, "the tool in hand");
            // the camera leaned on the new thicket; pan back to the seedling (a fly stands in for the pan)
            bc.FlyTo(box.transform.TransformPoint(box.land.OnGround(SiteMap.CXc(cell), SiteMap.CZc(cell))), Mathf.Min(bc.zoom, 0.6f));
            yield return Until(() => !bc.IsFlying, 8f);
            yield return Frames(5);
            int sc = PluckCellOnScreen(PluckKind.Sapling, cell);
            Assert.GreaterOrEqual(sc, 0, "a seedling on screen");
            tv.HoverOverride = tv.ScreenOf(sc);
            yield return Frames(3);
            Assert.AreEqual(sc, tv.Target, "the ring marks the seedling");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("731-7-tool-in-hand");
            tv.HoverOverride = null;
            int plucks0 = tv.Plucks;
            yield return TapAt(tv.ScreenOf(sc));
            yield return Frames(5);
            Assert.AreEqual(plucks0 + 1, tv.Plucks, "the tap lifted it: " + tv.Last);
            Assert.AreNotEqual((byte)EcoCover.Seedling, Sim.State.eco.cover[sc], "the seedling is gone");
            int bushes0 = Sim.State.eco.cBush;
            int bc0 = PluckCellOnScreen(PluckKind.Bush);
            Assert.GreaterOrEqual(bc0, 0, "a bush on screen");
            yield return TapAt(tv.ScreenOf(bc0));
            yield return Frames(5);
            Assert.AreEqual(PluckKind.Bush, tv.LastKind, "a bush came out: " + tv.Last);
            Assert.AreNotEqual((byte)EcoCover.Bush, Sim.State.eco.cover[bc0]);
            // the Keeper's tree stays
            int keeper = Sim.EcoTree.cell;
            yield return TapAt(tv.ScreenOf(keeper));
            yield return Frames(5);
            Assert.AreEqual(keeper, Sim.State.eco.treeCell, "the Keeper's tree stays");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("731-8-plucked");
            Assert.AreEqual(GameHud.KitTool.Landscape, hud.Hand);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return Frames(5);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "Put away puts the tool down");
            AssertNoErrors("landscaping");
        }

        /// <summary>#729 look: the thickets at n = 1, 2 and 4 after they settle (each sheds to its target a bush a season). Shots only.</summary>
        [UnityTest, Timeout(900000), Explicit("shots")]
        public IEnumerator Thickets_AtOneTwoAndFour_Shots()
        {
            yield return Living();
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0, "the first thicket");
            Sim.DebugSkipSeasons(16); // the first fills (22)
            box.Refresh();
            yield return Frames(20);
            var cam = Camera.main.GetComponent<BoxCamera>();
            var g = Sim.EcoGround;
            cam.FlyTo(box.transform.TransformPoint(box.land.OnGround(g.campX, g.campZ)), 0.7f);
            yield return Until(() => !cam.IsFlying, 8f);
            yield return Shot($"731-th-n1-{Sim.State.eco.cBush}");
            Sim.DebugCrowdThickets(1);
            Sim.DebugSkipSeasons(16);
            box.Refresh();
            yield return Frames(20);
            yield return Shot($"731-th-n2-{Sim.State.eco.cBush}");
            Sim.DebugCrowdThickets(2);
            Sim.DebugSkipSeasons(20);
            box.Refresh();
            yield return Frames(20);
            yield return Shot($"731-th-n{Sim.BushThicketCount}-{Sim.State.eco.cBush}");
            var ths = new List<(int cell, float radius, int bushes)>(); Sim.BushThickets(ths);
            Debug.Log("[731] thickets: " + string.Join(" · ", ths.Select(t => $"{t.bushes} r {t.radius:0.00}")));
        }
    }
}
