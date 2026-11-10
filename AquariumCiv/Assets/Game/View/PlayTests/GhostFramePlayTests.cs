using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Tester feedback 3: after the first tap with a life tool the camera leans in, and the ghost sat near the top of the screen under the
    /// people's caption ("Tap again to set the ducks down here", and "here" couldn't be seen). Now the lean keeps the ghost in the clear
    /// middle band (35-65 % of the height) at both phone sizes, and while a life or seed tool is in hand the people's lines wait (a scene
    /// line queues and shows once the tool is put down). Also the glow shots (before and after its new colours, day and night).
    /// Shots: Temp/agent/tfix_*.png.
    /// </summary>
    [Category("GhostFrame")]
    public class GhostFramePlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        IEnumerator LivingLand(int w, int h)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.Table(0.3f); // a pond with open water
            yield return Frames(20);
            Sim.DebugJarsReady();
            yield return new WaitForSecondsRealtime(4f); // the first water's moment passes
        }

        static IEnumerator CameraSettled() { yield return Until(() => !Cam.Leaning && !Cam.Gliding && !Cam.GlideArmed && !Cam.IsFlying, 30f); }

        [UnityTest, Timeout(900000)]
        public IEnumerator GhostAfterLean_InTheMiddleBand_NoCaption([Values(1080, 1179)] int w)
        {
            int h = w == 1080 ? 1920 : 2556;
            yield return LivingLand(w, h);
            var lpv = LifePlaceView.Instance; var hud = GameHud.Instance; var show = ShowMeDirector.Instance;
            Assert.IsNotNull(show, "the caption's director");
            int framed = 0;
            foreach (var s in new[] { LifeSpecies.Rabbits, LifeSpecies.Ducks, LifeSpecies.Thicket })
            {
                Sim.DebugJarsReady();
                yield return CameraSettled();
                if (!Sim.JarReady(s)) { Debug.Log($"[tfix] {s}: jar not ready ({Sim.JarStateOf(s)})"); continue; }
                // a scene's line is up when the Keeper takes the tool: it comes down and waits
                show.Say($"A line before the {s}.", PersonRef.None, 8f, LineKind.Echo);
                yield return Frames(3);
                Assert.IsTrue(show.CaptionUp, "the scene's line is up first");
                int waiting0 = show.WaitingLines;
                hud.TakeLife(s);
                yield return Frames(5);
                Assert.IsFalse(show.CaptionUp, $"{s}: the caption comes down while the tool is in hand");
                Assert.Greater(show.WaitingLines, waiting0, $"{s}: and the line waits its turn (not dropped)");
                Assert.IsNotNull(show.QuietReason(), "the people's talk pauses");
                int cell = GlowingCellOnScreen(Sim, s);
                if (cell < 0) { Debug.Log($"[tfix] {s}: no glowing spot on screen"); hud.SetHand(GameHud.KitTool.None); continue; }
                yield return TapAt(lpv.ScreenOf(cell));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Until(() => !Cam.IsFlying, 8f);
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsTrue(lpv.GhostShown, $"{s}: the ghost ({lpv.Last})");
                // a line said now waits too
                Assert.IsFalse(show.Say($"A line during the {s} ghost.", PersonRef.None, 6f, LineKind.Echo), $"{s}: no line over the ghost");
                yield return new WaitForSecondsRealtime(1f);
                Assert.IsFalse(show.CaptionUp, $"{s}: no caption while the ghost is up");
                var p = lpv.GhostScreen;
                float fy = 1f - p.y / Screen.height; // from the top, as the eye reads it
                Debug.Log($"[tfix] {s} {w}x{h}: ghost at {p} = {fy:P0} from the top, zoom {Cam.zoom:0.00}");
                yield return Shot($"tfix_ghost_{s.ToString().ToLowerInvariant()}_{w}x{h}");
                Assert.That(fy, Is.InRange(0.35f, 0.65f), $"{s}: the ghost sits in the clear middle band ({fy:P0} from the top)");
                Assert.That(p.x / Screen.width, Is.InRange(0.15f, 0.85f), $"{s}: and across the middle");
                framed++;
                // the tool put away: the waiting lines get their turn
                hud.SetHand(GameHud.KitTool.None);
                yield return Until(() => show.CaptionUp, 3f);
                Assert.IsTrue(show.CaptionUp, $"{s}: the waiting line shows once the tool is down");
                yield return Until(() => !show.CaptionUp && show.WaitingLines == 0, 30f);
            }
            Assert.GreaterOrEqual(framed, 2, "at least two species framed by the lean");
            // the seed in hand keeps the people quiet too
            hud.SetHand(GameHud.KitTool.Seed);
            yield return Frames(3);
            Assert.IsNotNull(show.QuietReason(), "the seed in hand: the talk pauses");
            Assert.IsFalse(show.Say("A line with the seed in hand.", PersonRef.None, 5f, LineKind.Echo), "a scene line waits while the seed is in hand");
            hud.SetHand(GameHud.KitTool.None);
            yield return Until(() => show.CaptionUp, 3f);
            Assert.IsTrue(show.CaptionUp, "and shows after");
            AssertNoErrors("ghost frame");
        }

        /// <summary>The glow at the default zoom, before (the old pale white at 0.26) and after, by day and by night, plus one close shot.</summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator GlowShots_BeforeAfter_DayNight()
        {
            yield return LivingLand(1080, 1920);
            var lpv = LifePlaceView.Instance; var hud = GameHud.Instance;
            yield return CameraSettled();
            Color newRim = lpv.glow, newFill = lpv.glowFillColor; float newBreath = lpv.glowBreath;
            foreach (var s in new[] { LifeSpecies.Thicket, LifeSpecies.Rabbits })
            {
                Sim.DebugJarsReady();
                if (!Sim.JarReady(s)) continue;
                hud.TakeLife(s);
                foreach (float hour in new[] { 10f, 22f })
                {
                    PeopleView.DebugSetHour(hour);
                    string tag = $"{s.ToString().ToLowerInvariant()}_{(hour < 18f ? "day" : "night")}";
                    // before: the old look (#516), a pale warm white outline at 0.26 and a faint fill
                    lpv.glow = new Color(1f, 0.97f, 0.9f, 0.26f); lpv.glowFillColor = new Color(1f, 0.97f, 0.9f, 0.026f); lpv.glowBreath = 0f;
                    lpv.RebuildGlow();
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Shot($"tfix_glow_{tag}_before");
                    lpv.glow = newRim; lpv.glowFillColor = newFill; lpv.glowBreath = 0f;
                    lpv.RebuildGlow();
                    yield return new WaitForSecondsRealtime(0.5f);
                    Assert.Greater(lpv.GlowCells, 0, $"{s}: glows");
                    yield return Shot($"tfix_glow_{tag}_after");
                }
                lpv.glowBreath = newBreath;
                PeopleView.DebugSetHour(10f);
                hud.SetHand(GameHud.KitTool.None);
                yield return Frames(5);
            }
            // one close shot, by day
            Sim.DebugJarsReady();
            hud.TakeLife(Sim.JarReady(LifeSpecies.Thicket) ? LifeSpecies.Thicket : LifeSpecies.Rabbits);
            Cam.FlyTo(Cam.target, 0.4f, 0f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("tfix_glow_close_after");
            hud.SetHand(GameHud.KitTool.None);
            AssertNoErrors("glow shots");
        }
    }
}
