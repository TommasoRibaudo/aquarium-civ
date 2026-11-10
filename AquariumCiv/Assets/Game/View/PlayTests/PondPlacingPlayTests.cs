using System.Collections;
using System.Collections.Generic;
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
    /// fb4 #542 "ducks are hard to place" (playtest #530): the Keeper places the ducks and then the fish by real taps on a phone
    /// (1179x2556) at the default zoom. The taps land where the player SEES things: on the water's surface (the ghost and the glow
    /// are drawn there), not on the pond's bed. Shots: Temp/agent/fb4/542/*.png.
    /// </summary>
    [Category("PondPlacing")]
    public class PondPlacingPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        static string Dir => "fb4/542/";

        /// <summary>The land point (box-local) a tap at the screen point lands on, as the life tool reads it.</summary>
        static Vector3 LandAt(Vector2 p) { EcoMistView.Instance.GroundPoint(p, out var at); return at; }

        /// <summary>Where the player sees the water's surface above a cell's middle.</summary>
        static Vector2 SurfaceOf(int cell)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            float w = box.layout != null && box.layout.eco != null ? box.layout.eco.waterY : Land.WaterY;
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(SiteMap.CXc(cell), w, SiteMap.CZc(cell))));
        }

        IEnumerator Pond()
        {
            System.IO.Directory.CreateDirectory(Shots + Dir);
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.Table(0.3f);
            Sim.DebugGrowFarWood();
            yield return Frames(20);
            Sim.DebugJarsReady();
            yield return new WaitForSecondsRealtime(4f);
        }

        /// <summary>The cell the species may have whose water-surface point is on screen, clear of the HUD, and whose tap lands on it.</summary>
        static int WaterCell(LifeSpecies s)
        {
            var ground = new List<int>();
            Sim.LifeGround(s, ground);
            int best = -1; float bestD = float.MaxValue;
            foreach (int c in ground)
            {
                if (!Sim.CanPlaceLife(s, c)) continue;
                var p = SurfaceOf(c);
                if (p.x < Screen.width * 0.12f || p.x > Screen.width * 0.88f || p.y < Screen.height * 0.2f || p.y > Screen.height * 0.78f) continue;
                if (UiInput.Over(p)) continue;
                var d = Vector2.Distance(p, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        IEnumerator Settled() { yield return Until(() => !Cam.Leaning && !Cam.Gliding && !Cam.GlideArmed && !Cam.IsFlying, 30f); }

        IEnumerator PlaceByTouch(LifeSpecies s)
        {
            var hud = GameHud.Instance; var lpv = LifePlaceView.Instance;
            string tag = s.ToString().ToLowerInvariant();
            yield return Settled();
            Assert.IsTrue(Sim.JarReady(s), $"{s}: jar ready");
            hud.TakeLife(s);
            yield return Frames(6);
            Assert.Greater(lpv.GlowCells, 0, $"{s}: the water glows");
            // #542: taking the water tool frames the whole pond at once (before any tap)
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Until(() => !Cam.IsFlying, 10f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Less(Cam.zoom, 0.65f, $"{s}: the camera came in on the pond (zoom {Cam.zoom:0.00})");
            Assert.GreaterOrEqual(Cam.zoom, 0.29f, $"{s}: and shows the whole pond, not a speck (zoom {Cam.zoom:0.00})");
            Assert.IsTrue(Cam.Leaning, $"{s}: the frame holds while the tool is in hand");
            int cell = WaterCell(s);
            Assert.GreaterOrEqual(cell, 0, $"{s}: a glowing water cell on screen at the default zoom");
            yield return Shot(Dir + tag + "-1-glow");
            // tap 1: the player's finger on the water they see (not the bed under it)
            var p1 = SurfaceOf(cell);
            yield return TapAt(p1);
            yield return Frames(4);
            Assert.IsTrue(lpv.GhostShown, $"{s}: a tap on the water shows the ghost ({lpv.Last})");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Until(() => !Cam.IsFlying, 10f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot(Dir + tag + "-2-ghost");
            // tap 2: on the ghost as it is drawn (its silhouettes stand on the water)
            var g = lpv.GhostScreen;
            Assert.Greater(g.x, 0f, $"{s}: the ghost is on screen"); Assert.Less(g.x, Screen.width);
            Assert.Greater(g.y, Screen.height * 0.15f, $"{s}: the ghost is clear of the bottom HUD"); Assert.Less(g.y, Screen.height * 0.85f);
            // any tap on the water counts: a different bit of water than the ghost's, as far from it as the screen shows
            int ghostCell = lpv.GhostCell, far = -1; float farD = 0f;
            var ground = new List<int>(); Sim.LifeGround(s, ground);
            foreach (int c in ground)
            {
                var q = SurfaceOf(c);
                if (q.x < Screen.width * 0.1f || q.x > Screen.width * 0.9f || q.y < Screen.height * 0.2f || q.y > Screen.height * 0.8f || UiInput.Over(q)) continue;
                float d = Vector2.Distance(q, g);
                if (d > farD) { farD = d; far = c; }
            }
            Assert.GreaterOrEqual(far, 0, $"{s}: more water on screen to tap");
            Debug.Log($"[pond542] {s}: the confirming tap is {farD:0} px from the ghost");
            yield return TapAt(SurfaceOf(far));
            yield return Frames(8);
            Assert.IsTrue(Sim.LifeIsPlaced(s), $"{s}: a tap on the ghost places it ({lpv.Last})");
            Assert.AreEqual(GameHud.KitTool.None, hud.Hand, $"{s}: the tool is put down");
            int pc = Sim.LifePlacedCell(s);
            Assert.AreEqual(ghostCell, pc, $"{s}: set down where the ghost stood, on its water");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot(Dir + tag + "-3-placed");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Ducks_Then_Fish_ByTouchOnTheWater()
        {
            yield return Pond();
            foreach (var s in new[] { LifeSpecies.Ducks, LifeSpecies.Fish }) yield return PlaceByTouch(s);
            AssertNoErrors("pond placing");
        }

        /// <summary>Any tap on the water the player sees must count: the glow's cells, tapped on the water's surface, give a cell that
        /// is placeable, and a tap on the ghost (as drawn) lands within its reach.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator BigPond_TapsOnTheVisibleWaterCount()
        {
            yield return Pond();
            EcoDebug.PondToFull();
            yield return Frames(20);
            yield return Settled();
            var hud = GameHud.Instance; var lpv = LifePlaceView.Instance;
            hud.TakeLife(LifeSpecies.Ducks);
            yield return Frames(6);
            yield return Shot(Dir + "big-1-glow");
            var ground = new List<int>(); Sim.LifeGround(LifeSpecies.Ducks, ground);
            int on = 0, ok = 0, same = 0; float worst = 0f;
            foreach (int c in ground)
            {
                var p = SurfaceOf(c);
                if (p.x < 0 || p.x > Screen.width || p.y < Screen.height * 0.1f || p.y > Screen.height * 0.9f) continue;
                on++;
                if (!EcoMistView.Instance.GroundPoint(p, out var at)) continue;
                int hit = SiteMap.CellAt(at.x, at.z);
                if (hit >= 0 && Sim.LifeGroundOk(LifeSpecies.Ducks, hit)) ok++;
                if (hit == c) same++;
                worst = Mathf.Max(worst, Vector2.Distance(new Vector2(at.x, at.z), new Vector2(SiteMap.CXc(c), SiteMap.CZc(c))));
            }
            Debug.Log($"[pond542] glow cells {ground.Count}, on screen {on}, tap on the water gives valid ground {ok}, the same cell {same}, worst offset {worst:0.00} u");
            Assert.Greater(on, 0, "the glow shows at the default zoom");
            Assert.AreEqual(on, ok, "every tap on the visible water lands on water that may be had");
        }
    }
}
