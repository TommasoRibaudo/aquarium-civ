using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #423 the life slot (placed-life spec §6): a ready jar wiggles the Kit once and leaves a dot; the slot in the kit takes the
    /// species in hand, its ground glows; the first tap on the glow shows the ghost, a tap on the ghost places it (sim: Placed);
    /// a tap on dark ground does nothing; two ready jars open the chooser row. All by touch, on a portrait phone. Shots go to
    /// Temp/agent/p9-slot-*.png.
    /// </summary>
    [Category("LifeSlot")]
    public class LifeSlotPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        /// <summary>A living box with a meadow, after the first run, at 10 h, with nothing on screen.</summary>
        IEnumerator LivingMeadow(int w = 1179, int h = 2556)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            yield return Frames(10);
        }

        /// <summary>A screen point on the land whose cell the species may not have (dark ground), clear of the HUD.</summary>
        static Vector2 DarkSpot(LifeSpecies s, out int cell)
        {
            var lpv = LifePlaceView.Instance; var mist = EcoMistView.Instance;
            for (float fy = 0.3f; fy < 0.75f; fy += 0.05f)
                for (float fx = 0.2f; fx < 0.8f; fx += 0.05f)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    if (UiInput.Over(p) || !mist.GroundPoint(p, out var at)) continue;
                    cell = AquariumCiv.Sim.Settle.SiteMap.CellAt(at.x, at.z);
                    if (cell >= 0 && !Sim.LifeGroundOk(s, cell)) return p;
                }
            cell = -1;
            return Vector2.zero;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ReadyJar_Wiggle_Choose_Glow_Ghost_Place()
        {
            yield return LivingMeadow();
            var hud = GameHud.Instance;
            Assert.IsFalse(hud.LifeSlotShown, "no ready jar: no life slot");
            Assert.IsFalse(hud.LifeDotShown, "and no dot");
            // the land earns the thicket's jar as time passes
            int wiggles0 = hud.LifeWiggles;
            for (int i = 0; i < 16 && !Sim.JarReady(LifeSpecies.Thicket); i++) EcoDebug.SkipSeasons(1);
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "the thicket's jar opened on a meadow");
            Assert.IsFalse(Sim.JarReady(LifeSpecies.Rabbits), "the rabbits wait for the thicket (the ladder)");
            yield return Until(() => hud.LifeWiggles > wiggles0, 3f);
            Assert.AreEqual(wiggles0 + 1, hud.LifeWiggles, "the Kit wiggles once when the jar opens");
            Assert.IsFalse(Sim.JarWiggleDue(LifeSpecies.Thicket), "and the wiggle is acknowledged");
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Shot("p9-slot-1-wiggle");
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(hud.LifeDotShown, "a small dot stays on the Kit");
            yield return Shot("p9-slot-2-dot");
            yield return Frames(30);
            Assert.AreEqual(wiggles0 + 1, hud.LifeWiggles, "only once");

            // Kit: the life slot is there, with its dot; one ready jar: a tap takes it in hand
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "Kit opens the kit");
            Assert.IsTrue(hud.LifeSlotShown, "the life slot shows");
            yield return Shot("p9-slot-3-kit");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(hud.LifeRowOpen, "one ready jar: no chooser");
            Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "the thicket in hand");
            Assert.AreEqual(LifeSpecies.Thicket, hud.HeldLife);
            Assert.IsNull(hud.OpenSheet, "the kit closes with the tool in hand");
            Assert.IsFalse(hud.LifeDotShown, "no dot on Done while the jar is in hand");

            // the glow
            var lpv = LifePlaceView.Instance;
            Assert.IsNotNull(lpv, "the life tool's view");
            yield return Frames(5);
            var ground = new List<int>(); Sim.LifeGround(LifeSpecies.Thicket, ground);
            Assert.AreEqual(ground.Count, lpv.GlowCells, "the valid ground glows");
            Assert.Greater(lpv.GlowCells, 0);
            yield return new WaitForSecondsRealtime(1.5f); // the hint toast
            yield return Shot("p9-slot-4-glow");

            // a tap on dark ground: nothing but the soft no
            var dark = DarkSpot(LifeSpecies.Thicket, out int darkCell);
            Assert.GreaterOrEqual(darkCell, 0, "dark ground on screen");
            int ref0 = lpv.Refusals;
            yield return TapAt(dark);
            yield return Frames(5);
            Assert.AreEqual(ref0 + 1, lpv.Refusals, "the soft no: " + lpv.Last);
            Assert.IsFalse(lpv.GhostShown, "no ghost off the glow");
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "nothing placed");
            Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "still in hand");

            // the first tap on the glow: the ghost
            int cell = GlowingCellOnScreen(Sim, LifeSpecies.Thicket);
            Assert.GreaterOrEqual(cell, 0, "a glowing spot on screen");
            var p = lpv.ScreenOf(cell);
            yield return TapAt(p);
            yield return Frames(5);
            Assert.IsTrue(lpv.GhostShown, "the ghost: " + lpv.Last);
            Assert.AreEqual(cell, lpv.GhostCell);
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Thicket), "a ghost places nothing");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("p9-slot-5-ghost");
            // a dark tap with the ghost up: the ghost stays where it was
            yield return TapAt(dark);
            yield return Frames(3);
            Assert.AreEqual(cell, lpv.GhostCell, "a tap off the glow leaves the ghost");
            Assert.AreEqual(ref0 + 2, lpv.Refusals);

            // the tap on the ghost places it
            int placed0 = lpv.Placings;
            yield return TapAt(p);
            yield return Frames(10);
            Assert.AreEqual(JarState.Placed, Sim.JarStateOf(LifeSpecies.Thicket), "placed: " + lpv.Last);
            Assert.AreEqual(cell, Sim.LifePlacedCell(LifeSpecies.Thicket), "where the ghost stood");
            Assert.AreEqual(placed0 + 1, lpv.Placings);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "the tool is put down");
            Assert.IsFalse(lpv.GhostShown, "the ghost goes");
            yield return Frames(5);
            Assert.AreEqual(0, lpv.GlowCells, "and the glow");
            var bushes = new List<int>(); Sim.BushCells(bushes);
            Assert.Greater(bushes.Count, 0, "the thicket's founders stand");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("p9-slot-6-placed");
            if (!Sim.AnyJarReady) Assert.IsFalse(hud.LifeDotShown, "no jar ready: the dot goes");
            AssertNoErrors("life slot");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TwoReadyJars_OpenTheChooserRow()
        {
            yield return LivingMeadow();
            var hud = GameHud.Instance;
            Sim.DebugJarsReady(); // every jar open (the cheat): several are ready at once
            var ready = new List<LifeSpecies>(); Sim.ReadyJars(ready);
            Assert.GreaterOrEqual(ready.Count, 2, "two or more ready jars");
            yield return Frames(10);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(GameHud.Kit, hud.OpenSheet);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(hud.LifeRowOpen, "two or more ready jars: the chooser row opens");
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "nothing in hand yet");
            foreach (var s in ready) Assert.IsNotNull(hud.Root.Q("kit-life-" + s.ToString().ToLowerInvariant()), "an icon for " + s);
            yield return Shot("p9-slot-7-row");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life-rabbits"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GameHud.KitTool.Life, hud.Hand, "a tap on an icon takes it in hand");
            Assert.AreEqual(LifeSpecies.Rabbits, hud.HeldLife);
            yield return Frames(5);
            Assert.Greater(LifePlaceView.Instance.GlowCells, 0, "the rabbits' ground glows");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("p9-slot-8-rabbits-glow");
            // Done puts it back
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return Frames(10);
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "Done puts it back");
            Assert.IsTrue(Sim.JarReady(LifeSpecies.Rabbits), "still ready");
            Assert.AreEqual(0, LifePlaceView.Instance.GlowCells, "the glow goes");
            // and the whole real path places the pair
            yield return KeeperPlaces(LifeSpecies.Rabbits);
            Assert.IsTrue(LastPlaced, "the rabbits placed by touch");
            Assert.Greater(Sim.EcoRabbitCount, 0, "a pair in the box");
            AssertNoErrors("life row");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator SmallPhone_SlotAndRowFit()
        {
            yield return LivingMeadow(750, 1334);
            var hud = GameHud.Instance;
            Sim.DebugJarsReady();
            yield return Frames(10);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "kit-life"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(hud.LifeRowOpen);
            var panel = hud.Root.panel.visualTree.worldBound;
            foreach (var name in new[] { "kit-life" }) // the chooser stands in for the note and the other tools while it is open
            {
                var r = hud.Root.Q(name).worldBound;
                Assert.IsTrue(r.xMin >= panel.xMin - 0.5f && r.xMax <= panel.xMax + 0.5f, $"{name} fits the screen's width ({r})");
                Assert.GreaterOrEqual(r.height + 32f, GameUiPanel.MinTarget - 0.5f, $"{name} is a full touch target (with its 16-unit reach)");
            }
            var row = hud.Root.Q("kit-life-row");
            var head = hud.Root.Q("kit-scrim").Q(className: "kg-sheet-head");
            Assert.Greater(row.worldBound.yMin, head.worldBound.yMax - 0.5f, $"the chooser sits under the title ({row.worldBound} vs {head.worldBound})");
            foreach (var c in row.Children())
            {
                Assert.IsTrue(c.worldBound.xMax <= panel.xMax + 0.5f, $"{c.name} fits ({c.worldBound})");
                Assert.GreaterOrEqual(c.worldBound.height + 32f, GameUiPanel.MinTarget - 0.5f, $"{c.name} is a full touch target (with its reach)");
                Assert.Less(c.worldBound.yMax, hud.Root.Q("kit-life").worldBound.yMin, $"{c.name} sits in one row above the slot");
            }
            yield return Shot("p9-slot-9-small-phone-row");
            AssertNoErrors("small phone");
        }
    }
}
