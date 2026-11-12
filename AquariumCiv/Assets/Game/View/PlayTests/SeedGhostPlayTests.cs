using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #518 (playtest #513): the seed places like the life slot (a tap shows a ghost of the seed's mound, a tap on the ghost plants it, a tap
    /// elsewhere moves it), and a press that becomes a two-finger pan never plants, places or mists: the seed, the life slot and the mister,
    /// with the fingers landing together or one after the other (the finger left after the other lifts never acts either). A fresh single
    /// tap still acts. All by touch, on a portrait phone. Shots go to Temp/agent/518-*.png.
    /// </summary>
    [Category("SeedGhost")]
    public class SeedGhostPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        /// <summary>A living box after the first run, at 10 h, the HUD up and nothing open.</summary>
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

        /// <summary>The seed comes (the debug cheat reaches the state; the tests pick it up by touch or hand it over), the HUD settles.</summary>
        IEnumerator ReadySeed()
        {
            var hud = GameHud.Instance;
            string r = EcoDebug.SeedInHand();
            Assert.IsTrue(Sim.CanPlantTree, "the seed is ready: " + r);
            hud.SetHand(GameHud.KitTool.None);
            yield return Until(() => hud.IsVisible && !(PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)), 30f);
            if (hud.SheetOpen) hud.CloseSheet();
            yield return Frames(10);
        }

        /// <summary>A cell the seed would take as it is (its tree cell is itself), its middle on screen clear of the HUD, and a tap there lands
        /// on it; the nearest the screen's middle, at least <paramref name="awayFrom"/> + 0.6 u from another cell.</summary>
        static int TreeCellOnScreen(int awayFrom = -1)
        {
            var sp = SeedPlaceView.Instance; var mist = EcoMistView.Instance;
            var mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            int best = -1; float bestD = float.MaxValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                if (Sim.TreeCellFor(x, z) != c) continue;
                if (awayFrom >= 0 && new Vector2(x - SiteMap.CXc(awayFrom), z - SiteMap.CZc(awayFrom)).magnitude < 0.6f) continue;
                var p = sp.ScreenOf(c);
                if (p.x < Screen.width * 0.15f || p.x > Screen.width * 0.7f || p.y < Screen.height * 0.25f || p.y > Screen.height * 0.75f) continue;
                if (UiInput.Over(p) || UiInput.Over(p + new Vector2(Screen.width * 0.18f, 0f))) continue;
                if (mist == null || !mist.GroundPoint(p, out var at) || SiteMap.CellAt(at.x, at.z) != c) continue;
                float d = (p - mid).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>Two fingers pan the view: landing together, or the second a few frames after the first (<paramref name="stagger"/>
        /// frames); with a stagger the second lifts first and the first stays still a moment before it lifts where it is (the tap the old
        /// seed tool saw, playtest #513).</summary>
        IEnumerator TwoFingerPan(Vector2 a, int stagger)
        {
            var b = a + new Vector2(Screen.width * 0.18f, 0f);
            var move = new Vector2(0f, Screen.height * 0.08f);
            int ia = touchId++, ib = touchId++;
            BeginTouch(ia, a, queueEventOnly: true);
            if (stagger > 0) yield return Frames(stagger);
            BeginTouch(ib, b, queueEventOnly: true);
            yield return null;
            for (int i = 1; i <= 12; i++)
            {
                var d = move * (i / 12f);
                MoveTouch(ia, a + d, delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(ib, b + d, delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            if (stagger > 0)
            {
                EndTouch(ib, b + move, delta: Vector2.zero, queueEventOnly: true);
                yield return Frames(4);
                EndTouch(ia, a + move, delta: Vector2.zero, queueEventOnly: true);
            }
            else
            {
                EndTouch(ia, a + move, delta: Vector2.zero, queueEventOnly: true);
                EndTouch(ib, b + move, delta: Vector2.zero, queueEventOnly: true);
            }
            yield return Frames(6);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Seed_TapShowsGhost_TapElsewhereMovesIt_TapOnGhostPlants()
        {
            yield return Living();
            var hud = GameHud.Instance; var sp = SeedPlaceView.Instance;
            Assert.IsNotNull(sp, "the seed's view (its bootstrap)");
            yield return ReadySeed();
            // the Kit, then the Seed: by touch
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens the kit");
            Assert.IsNull(UnityEngine.UIElements.UQueryExtensions.Q(hud.Root, "kit-seed"), "#558: no separate Seed tool in the kit");
            Assert.IsTrue(UnityEngine.UIElements.UQueryExtensions.Q(hud.Root, "kit-life").resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex, "the life jar holds the seed");
            yield return PickSeedFromLifeJar(hud, "558-seed-chooser");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.KitTool.Seed, hud.Hand, "the seed in hand");
            yield return Until(() => GameUiPanel.Toasts.Current == GameHud.SeedHint, 6f);
            Assert.AreEqual(GameHud.SeedHint, GameUiPanel.Toasts.Current, "its hint says to choose a spot");

            int cell = TreeCellOnScreen();
            Assert.GreaterOrEqual(cell, 0, "open ground for the tree on screen");
            yield return TapAt(sp.ScreenOf(cell));
            yield return Frames(5);
            Assert.IsTrue(sp.GhostShown, "the first tap shows the ghost: " + sp.Last);
            Assert.AreEqual(cell, sp.GhostCell);
            Assert.AreEqual(-1, Sim.State.eco.treeCell, "a ghost plants nothing");
            Assert.IsTrue(Sim.CanPlantTree, "the seed is still in hand");
            Assert.AreEqual(GameHud.KitTool.Seed, hud.Hand);
            yield return Until(() => GameUiPanel.Toasts.Current == SeedPlaceView.TapAgainHint, 3f);
            Assert.AreEqual(SeedPlaceView.TapAgainHint, GameUiPanel.Toasts.Current, $"the tap-again hint is up (held by: {GameUiPanel.Toasts.Holders})");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("518-seed-1-ghost");

            // a tap elsewhere on open ground moves the ghost
            int other = TreeCellOnScreen(cell);
            if (other >= 0)
            {
                yield return TapAt(sp.ScreenOf(other));
                yield return Frames(5);
                Assert.AreEqual(other, sp.GhostCell, "the ghost moved: " + sp.Last);
                Assert.AreEqual(-1, Sim.State.eco.treeCell, "moving it plants nothing");
                cell = other;
            }

            // the tap on the ghost plants it
            int plants0 = sp.Plantings;
            yield return TapAt(sp.ScreenOf(sp.GhostCell));
            yield return Frames(10);
            Assert.AreEqual(cell, Sim.State.eco.treeCell, "planted where the ghost stood: " + sp.Last);
            Assert.AreEqual(plants0 + 1, sp.Plantings);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "the seed is put down");
            Assert.IsFalse(sp.GhostShown, "the ghost goes");
            Assert.AreNotEqual(SeedPlaceView.TapAgainHint, GameUiPanel.Toasts.Current, "and its hint with it");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("518-seed-2-planted");
            AssertNoErrors("seed ghost");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Seed_TwoFingerPanOverTheLand_PlantsNothing()
        {
            yield return Living();
            var hud = GameHud.Instance; var sp = SeedPlaceView.Instance;
            yield return ReadySeed();
            hud.SetHand(GameHud.KitTool.Seed);
            yield return Frames(3);
            foreach (int stagger in new[] { 0, 3 })
            {
                int cell = TreeCellOnScreen();
                Assert.GreaterOrEqual(cell, 0, "open ground on screen");
                yield return TwoFingerPan(sp.ScreenOf(cell), stagger);
                Assert.IsFalse(sp.GhostShown, $"a pan (stagger {stagger}) shows no ghost: {sp.Last}");
                Assert.AreEqual(-1, Sim.State.eco.treeCell, $"a pan (stagger {stagger}) plants nothing");
                Assert.AreEqual(GameHud.KitTool.Seed, hud.Hand, "the seed stays in hand");
            }
            // with the ghost up, a pan that starts on it plants nothing either
            int g = TreeCellOnScreen();
            yield return TapAt(sp.ScreenOf(g));
            yield return Frames(5);
            Assert.IsTrue(sp.GhostShown, "a single tap still shows the ghost at once: " + sp.Last);
            yield return TwoFingerPan(sp.ScreenOf(sp.GhostCell), 3);
            Assert.AreEqual(-1, Sim.State.eco.treeCell, "a pan from the ghost plants nothing");
            Assert.IsTrue(sp.GhostShown, "and leaves the ghost");
            // and a fresh single tap on the ghost still plants
            var p = sp.ScreenOf(sp.GhostCell);
            Assume.That(p.y > Screen.height * 0.2f && p.y < Screen.height * 0.8f && !UiInput.Over(p), "the ghost still on screen after the pans");
            yield return TapAt(p);
            yield return Frames(10);
            Assert.AreEqual(g, Sim.State.eco.treeCell, "a tap on the ghost plants: " + sp.Last);
            AssertNoErrors("seed pan");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator LifeSlot_TwoFingerPanOverTheGlow_PlacesNothing()
        {
            yield return Living();
            var hud = GameHud.Instance; var lpv = LifePlaceView.Instance;
            Assert.Greater(Sim.DebugJarsReady(), 0, "jars ready");
            yield return Until(() => hud.IsVisible && !(PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)), 30f);
            hud.TakeLife(LifeSpecies.Thicket);
            yield return Frames(5);
            Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "the thicket in hand");
            int placings0 = lpv.Placings;
            foreach (int stagger in new[] { 0, 3 })
            {
                int cell = GlowingCellOnScreen(Sim, LifeSpecies.Thicket);
                Assert.GreaterOrEqual(cell, 0, "a glowing spot on screen");
                yield return TwoFingerPan(lpv.ScreenOf(cell), stagger);
                Assert.IsFalse(lpv.GhostShown, $"a pan (stagger {stagger}) shows no ghost: {lpv.Last}");
                Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), $"a pan (stagger {stagger}) places nothing");
                Assert.AreEqual(placings0, lpv.Placings);
                Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "still in hand");
            }
            // a single tap still shows the ghost; a pan from the ghost places nothing
            int g = GlowingCellOnScreen(Sim, LifeSpecies.Thicket);
            yield return TapAt(lpv.ScreenOf(g));
            yield return Frames(5);
            Assert.IsTrue(lpv.GhostShown, "a single tap shows the ghost: " + lpv.Last);
            var bc = Camera.main.GetComponent<BoxCamera>();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Until(() => !bc.IsFlying, 8f); // the lean on the ghost
            yield return TwoFingerPan(lpv.ScreenOf(lpv.GhostCell), 3);
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "a pan from the ghost places nothing");
            Assert.AreEqual(placings0, lpv.Placings);
            Assert.IsTrue(lpv.GhostShown, "and leaves the ghost");
            AssertNoErrors("life pan");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Mister_TwoFingerPan_MistsNothing_ATapStillMists()
        {
            yield return Living();
            var hud = GameHud.Instance; var mist = EcoMistView.Instance; var sp = SeedPlaceView.Instance;
            yield return Until(() => hud.IsVisible && !(PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)), 30f);
            hud.SetHand(GameHud.KitTool.Mister);
            yield return Frames(3);
            int pulses0 = mist.MistPulses;
            foreach (int stagger in new[] { 0, 1 }) // one frame apart: inside KitPress.SecondFingerGrace at any sane frame time
            {
                int cell = TreeCellOnScreen();
                Assert.GreaterOrEqual(cell, 0, "land on screen");
                yield return TwoFingerPan(sp.ScreenOf(cell), stagger);
                Assert.AreEqual(pulses0, mist.MistPulses, $"a pan (stagger {stagger}) mists nothing");
                Assert.IsFalse(mist.Misting, "no spray left running");
            }
            // a quick single tap still mists, at once
            int c2 = TreeCellOnScreen();
            yield return TapAt(sp.ScreenOf(c2));
            yield return Until(() => mist.MistPulses > pulses0, 2f);
            Assert.Greater(mist.MistPulses, pulses0, "a tap mists");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(mist.Misting, "the tap's spray ends");
            hud.SetHand(GameHud.KitTool.None);
            AssertNoErrors("mister pan");
        }
    }
}
