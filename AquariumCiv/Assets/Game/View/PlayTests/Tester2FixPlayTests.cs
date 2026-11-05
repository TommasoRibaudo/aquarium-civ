using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Tester run 2 [real] findings. #300: the new-grass overlay is lit like the land at every hour (no glow at night).
    /// #301: in the first run's wipe step a one-finger swipe that starts off the glass wipes the panes it crosses and never turns
    /// the camera. Plus the Cloth's living-land toast and the Mister's first-meeting hint. Screenshots go to Temp/agent/t2fix_*.png.
    /// </summary>
    [Category("Tester2Fix")]
    public class Tester2FixPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView Run => FirstRunView.Instance;

        // ------------------------------------------------------------------ #301
        [UnityTest, Timeout(300000)]
        public IEnumerator FirstRunWipe_EdgeToEdgeSwipesWipe_AndNeverTurnTheCamera([Values(true, false)] bool eco, [Values(1179, 1080)] int w)
        {
            int h = w == 1179 ? 2556 : 1920;
            UseLivingLand(eco);
            yield return OpenSlice(w, h, skipFirstRun: false);
            Assert.AreEqual(eco, Sim.EcologyOn);
            var care = CareView.Instance;
            var rig = Camera.main.GetComponent<BoxCamera>();
            yield return Until(() => Run.Step == FirstRunStep.Wipe, 8f);
            Assert.AreEqual(FirstRunStep.Wipe, Run.Step);
            Assert.IsTrue(CareView.WipeAnywhere, "the wipe step: one finger wipes anywhere");
            yield return Until(() => Run.GlassMadeReachable, 4f);
            yield return new WaitForSecondsRealtime(0.5f);
            float yaw0 = rig.yaw, pitch0 = rig.pitch, zoom0 = rig.zoom;
            string tag = $"{(eco ? "eco" : "old")}_{w}x{h}";
            Debug.Log($"[t2fix-wipe {tag}] start grime {Sim.State.grime:0.000} panes {care.DebugPaneGrime()} yaw {yaw0:0.0} pitch {pitch0:0.0}");
            yield return Shot("t2fix_wipe_start_" + tag);

            // The tester's natural swipe: edge to edge (x = 20 .. W-20), rows down the screen, starting off the glass.
            float spacing = care.wipeRadius * Screen.height * 1.1f;
            bool right = true;
            int strokes = 0, offGlassStarts = 0;
            float maxTurn = 0f;
            for (int pass = 0; pass < 4 && Run.Step == FirstRunStep.Wipe; pass++)
            {
                for (float y = Screen.height * 0.9f; y > Screen.height * 0.1f && Run.Step == FirstRunStep.Wipe; y -= spacing)
                {
                    var a = new Vector2(20f, y);
                    var b = new Vector2(Screen.width - 20f, y);
                    var from = right ? a : b;
                    if (!care.GlassAt(from)) offGlassStarts++;
                    yield return Drag(right ? new[] { a, b } : new[] { b, a }, 0.03f * Screen.height);
                    right = !right;
                    strokes++;
                    maxTurn = Mathf.Max(maxTurn, Mathf.Abs(Mathf.DeltaAngle(rig.yaw, yaw0)), Mathf.Abs(rig.pitch - pitch0));
                }
                Debug.Log($"[t2fix-wipe {tag}] pass {pass}: grime {Sim.State.grime:0.000} panes {care.DebugPaneGrime()} step {Run.Step} yaw {rig.yaw:0.0} pitch {rig.pitch:0.0}");
            }
            Debug.Log($"[t2fix-wipe {tag}] {strokes} strokes, {offGlassStarts} started off the glass, max turn {maxTurn:0.00}");
            Assert.Greater(offGlassStarts, 0, "some strokes started off the glass");
            Assert.Less(maxTurn, 0.5f, "a one-finger swipe in the wipe step never turns the camera");
            Assert.AreEqual(zoom0, rig.zoom, 0.01f, "nor zooms it");
            Assert.AreNotEqual(FirstRunStep.Wipe, Run.Step, $"the natural swipes clear the glass (grime {Sim.State.grime:0.000}, panes {care.DebugPaneGrime()})");
            Assert.LessOrEqual(Sim.State.grime, FirstRun.WipedGrime + 0.005f, "it ended because the glass is clear, not on the slow fallback");
            foreach (var m in care.Masks) Assert.Less(m.Mean, 0.1f, $"every pane is clear ({care.DebugPaneGrime()})");
            Assert.IsFalse(CareView.WipeAnywhere, "after the step the normal rules are back");
            yield return Shot("t2fix_wipe_done_" + tag);
            AssertNoErrors("wipe " + tag);
        }

        // ------------------------------------------------------------------ #300
        static bool Green(SimCore s, int c)
        {
            byte cv = s.State.eco.cover[c];
            return cv != (byte)EcoCover.Bare && cv != (byte)EcoCover.Water;
        }

        static IEnumerator Grab(System.Action<Texture2D> got)
        {
            yield return new WaitForEndOfFrame();
            got(ScreenCapture.CaptureScreenshotAsTexture());
        }

        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>Over the pixels the overlay changes: the mean luminance with it and without it (the land under it), and how many.</summary>
        IEnumerator Compare(Renderer overlay, float[] result)
        {
            Texture2D on = null, off = null;
            overlay.forceRenderingOff = false;
            yield return Frames(3);
            yield return Grab(t => on = t);
            overlay.forceRenderingOff = true; // EcoSurface re-enables its renderers every frame
            yield return Frames(3);
            yield return Grab(t => off = t);
            Texture2D off2 = null;
            yield return Frames(3);
            yield return Grab(t => off2 = t); // pixels that change on their own (people, lamps) are left out
            overlay.forceRenderingOff = false;
            var a = on.GetPixels();
            var b = off.GetPixels();
            var b2 = off2.GetPixels();
            double la = 0, lb = 0;
            int n = 0;
            for (int i = 0; i < a.Length; i++)
            {
                float d = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                if (d < 3f / 255f) continue;
                float still = Mathf.Abs(b[i].r - b2[i].r) + Mathf.Abs(b[i].g - b2[i].g) + Mathf.Abs(b[i].b - b2[i].b);
                if (still > 1.5f / 255f) continue;
                la += Lum(a[i]); lb += Lum(b[i]); n++;
            }
            Object.Destroy(on); Object.Destroy(off); Object.Destroy(off2);
            result[0] = n > 0 ? (float)(la / n) : 0f;
            result[1] = n > 0 ? (float)(lb / n) : 0f;
            result[2] = n;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator NewGrass_IsLitLikeTheLand_DayAndNight()
        {
            UseLivingLand();
            float speedWas = EcoSurface.FadeSpeed;
            try
            {
                yield return OpenSlice(seed: 4242);
                var surf = EcoSurface.Instance;
                Assert.IsNotNull(surf);
                yield return Frames(20);
                // the tester's scene: a pond with a meadow round it, then new grass while away
                EcoDebug.Table(0.3f);
                EcoDebug.Humidity(0.9f);
                EcoDebug.MistBigCamp();
                int green = 0;
                for (int i = 0; i < 30; i++) { green = 0; for (int c = 0; c < EcoLand.N; c++) if (Green(Sim, c)) green++; if (green >= 200) break; EcoDebug.SkipSeasons(1); }
                Debug.Log($"[t2fix-newgrass] green cells {green}");
                Sim.MarkLandSeen();
                Sim.DebugPretendAwayWithNewGrass(60);
                Assert.GreaterOrEqual(Sim.LandNewCellCount, 6, "new grass to show");
                EcoSurface.FadeSpeed = 0f; // held fresh while we look
                surf.RestartFade();
                yield return Frames(30);
                Assert.Greater(surf.NewGrassCells, 0, "the overlay is drawn");
                Renderer overlay = surf.NewGrassRenderer;
                Assert.IsNotNull(overlay, "the EcoNewGrass renderer");

                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = true; // a steady light between the two grabs
                var rig = Camera.main.GetComponent<BoxCamera>();
                rig.enabled = false; // no auto-orbit between the grabs: the same view with and without the overlay
                var res = new float[3];
                var ratios = new Dictionary<float, float>();
                foreach (float hour in new[] { 12f, 21f, 2f })
                {
                    PeopleView.DebugSetHour(hour);
                    yield return new WaitForSecondsRealtime(1.5f);
                    yield return Compare(overlay, res);
                    float ratio = res[1] > 1e-4f ? res[0] / res[1] : 1f;
                    ratios[hour] = ratio;
                    Debug.Log($"[t2fix-newgrass] hour {hour:0}: daylight {(DayLightDriver.Driving ? DayLightDriver.Instance.Current.Daylight : -1f):0.00} overlay lum {res[0]:0.0000} land lum {res[1]:0.0000} ratio {ratio:0.000} over {res[2]:0} px");
                    yield return Shot($"t2fix_newgrass_{hour:00}h");
                    if (hour == 21f)
                    {
                        // before #300, for the record: the same overlay on the plain lit tint (mode 3)
                        var mat = overlay.sharedMaterial;
                        var old = EcoSurface.MakeMaterial("EcoTint", 3, 5, 10);
                        overlay.sharedMaterial = old;
                        var before = new float[3];
                        yield return Compare(overlay, before);
                        yield return Shot("t2fix_newgrass_21h_before");
                        overlay.sharedMaterial = mat;
                        Object.Destroy(old);
                        Debug.Log($"[t2fix-newgrass] hour 21 BEFORE (plain tint): overlay lum {before[0]:0.0000} land lum {before[1]:0.0000} ratio {(before[1] > 1e-4f ? before[0] / before[1] : 1f):0.000}");
                    }
                    if (hour == 12f) Assert.Greater(res[2], 50f, "by day the new grass shows");
                }
                Assert.LessOrEqual(ratios[21f], 1.01f, "at 21 h the new grass is never brighter than the land around it");
                Assert.LessOrEqual(ratios[2f], 1.01f, "nor at 2 h");
                Assert.LessOrEqual(ratios[12f], 1.6f, "by day it may be a lighter green, not a glow");

                // the fade at a fixed hour (noon): fresh, half, gone
                PeopleView.DebugSetHour(12f);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("t2fix_newgrass_fade0");
                EcoSurface.FadeSpeed = 45f;
                yield return Until(() => surf.LandSeconds >= EcoSeen.FadeSeconds * 0.5f, 8f);
                EcoSurface.FadeSpeed = 0f;
                yield return Frames(20);
                yield return Compare(overlay, res);
                Debug.Log($"[t2fix-newgrass] mid fade: overlay lum {res[0]:0.0000} land lum {res[1]:0.0000} over {res[2]:0} px");
                yield return Shot("t2fix_newgrass_fade_mid");
                EcoSurface.FadeSpeed = 45f;
                yield return Until(() => Sim.LandNewCellCount == 0 && surf.NewGrassCells == 0, 15f);
                Debug.Log($"[t2fix-newgrass] end: new {Sim.LandNewCellCount} overlay cells {surf.NewGrassCells} land s {surf.LandSeconds:0}");
                Assert.AreEqual(0, Sim.LandNewCellCount, "the fade ended and the mask committed");
                Assert.AreEqual(0, surf.NewGrassCells, "faded to the normal colouring");
                yield return Shot("t2fix_newgrass_fade_end");
                AssertNoErrors("new grass light");
            }
            finally { EcoSurface.FadeSpeed = speedWas; if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = false; var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null; if (cam != null) cam.enabled = true; }
        }

        // ------------------------------------------------------------------ text polish
        [UnityTest, Timeout(240000)]
        public IEnumerator Kit_ClothToastFitsTheLivingLand_MisterHintOnFirstMeetingOnly()
        {
            UseLivingLand();
            GameHud.DebugForgetMisterHint();
            try
            {
                yield return OpenSlice();
                var hud = GameHud.Instance;
                yield return Frames(20);
                Sim.State.grime = Mathf.Max(Sim.State.grime, 0.4f);
                yield return Frames(5);
                hud.SetCleanMode(true);
                yield return Until(() => GameUiPanel.Toasts.Current == "Drag across the glass to wipe it.", 6f);
                Assert.AreEqual("Drag across the glass to wipe it.", GameUiPanel.Toasts.Current, "a living land has no junk or weeds to flick");
                hud.SetCleanMode(false);
                yield return Frames(5);

                int shows = 0;
                for (int k = 0; k < 4; k++)
                {
                    hud.SetHand(GameHud.KitTool.Mister);
                    yield return Frames(3);
                    if (GameUiPanel.Toasts.Current == GameHud.MisterHint) shows++;
                    hud.WithdrawToast(GameHud.MisterHint);
                    hud.SetHand(GameHud.KitTool.None);
                    yield return Frames(3);
                }
                Assert.AreEqual(2, shows, "never misted: the hint shows on the first meeting and once more, then not again");

                GameHud.DebugForgetMisterHint();
                hud.SetHand(GameHud.KitTool.Mister);
                yield return Frames(3);
                Assert.AreEqual(GameHud.MisterHint, GameUiPanel.Toasts.Current, "first meeting");
                hud.WithdrawToast(GameHud.MisterHint);
                GameHud.NoteMisterUsed(); // the player misted with it
                hud.SetHand(GameHud.KitTool.None);
                yield return Frames(3);
                hud.SetHand(GameHud.KitTool.Mister);
                yield return Frames(3);
                Assert.AreNotEqual(GameHud.MisterHint, GameUiPanel.Toasts.Current, "misted once: no second showing");
                hud.SetHand(GameHud.KitTool.None);
                AssertNoErrors("kit text");
            }
            finally { GameHud.DebugForgetMisterHint(); }
        }
    }
}
