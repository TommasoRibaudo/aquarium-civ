using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The tester's batch 3 play-through (1080x1920, real touch): the four [real] findings. Shots: Temp/agent/b3fix/.
    /// 1. "Skip step" sat on the first green's line ("Green, where the mist fell...") at the arrival.
    /// 2. After a placing's lean the camera stayed close for the rest of the run: the next tool's framing cancelled the way home, and a
    ///    return during a lean (or a lean during the return's ease) lost the camp frame.
    /// 3. A ~1 s frame on the first tap with a seed in hand.
    /// 4. Wiping heavy grime at the default (camp) view barely cleaned it: the glass was not on screen.
    /// </summary>
    [Category("Batch3TesterFixes")]
    public class Batch3TesterFixesPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        const string Dir = "b3fix/";

        IEnumerator LivingBox(int w = 1080, int h = 1920, bool skipFirstRun = true)
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: 4242, skipFirstRun: skipFirstRun);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(10f);
        }

        IEnumerator Settled() { yield return Until(() => !Cam.Leaning && !Cam.Gliding && !Cam.GlideArmed && !Cam.IsFlying, 40f); }

        IEnumerator Returned(CampFrameView view, int framedBefore)
        {
            yield return Until(() => view.Framed > framedBefore, 10f);
            Assert.Greater(view.Framed, framedBefore, "the return framed the camp: " + view.LastWhy);
        }

        // ---------------------------------------------------------------- 1
        [UnityTest, Timeout(180000)]
        public IEnumerator SkipStepStaysOffTheFirstGreensLine([Values("1080x1920", "1080x2400", "1640x2360")] string size)
        {
            var wh = size.Split('x');
            yield return LivingBox(int.Parse(wh[0]), int.Parse(wh[1]), skipFirstRun: false);
            var fr = FirstRunView.Instance;
            Assert.IsTrue(fr.Active, "the first run");
            yield return Until(() => fr.Step == FirstRunStep.Wipe, 6f);
            bool bubblesWere = ShowMeDirector.BubblesOn;
            ShowMeDirector.BubblesOn = false; // the people's own lines wait: the first green's line has the word
            fr.Run.DebugSkipTo(FirstRunStep.Rain); // the P8 order: wipe, patch, rain (the mist), then the name with the first green's line
            yield return Frames(4);
            fr.Run.SkipStep();
            Assert.AreEqual(FirstRunStep.Name, fr.Step, "at the name step");
            yield return Until(() => fr.ShownReward.Length > 0, 4f);
            var bubble = fr.Root.Q("firstrun-bubble");
            var skip = fr.Root.Q("firstrun-skip-row");
            yield return Until(() => bubble.resolvedStyle.display == DisplayStyle.Flex, 6f); // it waits for whoever has the word
            var hud0 = GameHud.Instance; var holder = GameUiPanel.Bubbles.Holder;
            Debug.Log($"[b3fix] {size}: step {fr.Step}, reward '{fr.ShownReward}', hud visible {hud0.IsVisible}, camera mode {hud0.CameraMode}, sheet {hud0.SheetOpen}, bubble holder {(holder != null ? holder.rank.ToString() : "none")}, bubble {bubble.resolvedStyle.display}");
            yield return Frames(6);
            Assert.AreEqual(DisplayStyle.Flex, bubble.resolvedStyle.display, "the first green's line is up");
            Assert.AreEqual(DisplayStyle.Flex, skip.resolvedStyle.display, "and the test row");
            Debug.Log($"[b3fix] {size}: bubble {bubble.worldBound}, skip {skip.worldBound}, steps {fr.SkipSteps}");
            yield return Shot($"{Dir}1_arrival_{size}");
            Assert.IsFalse(bubble.worldBound.Overlaps(skip.worldBound), $"Skip sits clear of the line at {size}");
            var safe = fr.Root.Q("firstrun-safe");
            Assert.LessOrEqual(skip.worldBound.yMax, safe.worldBound.yMax, "and stays on screen");
            ShowMeDirector.BubblesOn = bubblesWere;
            AssertNoErrors("skip row");
        }

        // ---------------------------------------------------------------- 2
        IEnumerator PlaceByTaps(LifeSpecies s)
        {
            var hud = GameHud.Instance; var lp = LifePlaceView.Instance;
            hud.TakeLife(s);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Until(() => !Cam.IsFlying, 6f);
            yield return new WaitForSecondsRealtime(0.3f);
            int cell = GlowingCellOnScreen(Sim, s);
            Assert.GreaterOrEqual(cell, 0, $"{s}: a glowing spot on screen");
            lp.TapAt(Sim, s, lp.ScreenOf(cell));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Until(() => !Cam.IsFlying, 6f);
            lp.TapAt(Sim, s, lp.ScreenOf(lp.GhostCell));
            yield return Frames(5);
            Assert.IsTrue(Sim.LifeIsPlaced(s), $"{s} placed: {lp.Last}");
        }

        void AssertAtTheCampFrame(CampFrameView view, string when, bool spot = true)
        {
            var home = Cam.Home;
            Debug.Log($"[b3fix] {when}: zoom {Cam.zoom:0.00} pitch {Cam.pitch:0.0} yaw {Cam.yaw:0.0}; home zoom {home.zoom:0.00} pitch {home.pitch:0.0} yaw {home.yaw:0.0}; {CampFrameView.Status()}");
            Assert.AreEqual(view.LastZoom, Cam.zoom, 0.03f, $"{when}: the camp frame's zoom");
            Assert.AreEqual(home.pitch, Cam.pitch, 2f, $"{when}: the camp frame's angle, not the lean's");
            if (spot) Assert.Less(new Vector2(Cam.target.x - home.target.x, Cam.target.z - home.target.z).magnitude, 0.15f, $"{when}: over the camp");
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator ALeanAlwaysFindsItsWayHome()
        {
            yield return LivingBox();
            var view = CampFrameView.Instance;
            CampFrameView.Auto = true;
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            Debug.Log("[b3fix] " + GameClock.DebugAway(8));
            yield return Returned(view, 0);
            yield return Settled();
            AssertAtTheCampFrame(view, "the return");
            yield return Shot(Dir + "2a_camp_frame");

            // (a) the tester's run: a group placed (the lean), then an earned bush taken straight away (the tool frames its ground)
            Sim.DebugJarReady(LifeSpecies.Rabbits);
            Sim.DebugGiveBushes(1);
            yield return Frames(5);
            yield return PlaceByTaps(LifeSpecies.Rabbits);
            Assert.IsTrue(Cam.GlideArmed, "the lean will glide home");
            yield return new WaitForSecondsRealtime(0.5f);
            GameHud.Instance.SetHand(GameHud.KitTool.Bush);
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log($"[b3fix] bush in hand: ground frames {LifePlaceView.Instance.GroundFrames}, armed {Cam.GlideArmed}, zoom {Cam.zoom:0.00}");
            Assert.IsTrue(Cam.GlideArmed, "taking the bush keeps the way home");
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsFalse(Cam.Gliding, "and the view stays while the bush is in hand");
            GameHud.Instance.SetHand(GameHud.KitTool.None);
            yield return Settled();
            AssertAtTheCampFrame(view, "bush put back", spot: false); // the rabbits' tool framed its ground first (#643): the way home is that view
            yield return Shot(Dir + "2b_after_lean_and_bush");

            // (b) a return while a lean still holds the camera: the lean goes home to the new camp frame
            bool hold = true;
            var spot = Object.FindAnyObjectByType<BoxView>().transform.TransformPoint(new Vector3(1.2f, 1f, 1.5f));
            Assert.IsTrue(Cam.LeanIn(spot, 0.2f, () => hold), "a lean");
            yield return Until(() => !Cam.IsFlying, 4f);
            int framed = view.Framed;
            Debug.Log("[b3fix] " + GameClock.DebugAway(8));
            yield return Returned(view, framed);
            Assert.Greater(view.FramedViaGlide, 0, "framed through the lean's way home");
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(Cam.Leaning, "the lean still holds");
            hold = false;
            yield return Settled();
            AssertAtTheCampFrame(view, "lean let go after a return");

            // (c) a lean that starts while the return's ease is still on its way: home is where the ease was going
            Cam.EaseTo(Cam.target + new Vector3(0.6f, 0f, 0.4f), Cam.yaw, 55f, 0.2f, 0.01f);
            yield return Frames(10);
            yield return Settled();
            framed = view.Framed;
            Debug.Log("[b3fix] " + GameClock.DebugAway(8));
            yield return Until(() => view.Framed > framed, 10f);
            Assert.IsTrue(Cam.IsFlying, "the camp frame is easing in");
            hold = true;
            Assert.IsTrue(Cam.LeanIn(spot, 0.2f, () => hold), "a lean mid-ease");
            yield return new WaitForSecondsRealtime(2f);
            hold = false;
            yield return Settled();
            AssertAtTheCampFrame(view, "lean during the return's ease");
            yield return Shot(Dir + "2c_return_after_leans");
            AssertNoErrors("way home");
        }

        // ---------------------------------------------------------------- 3
        [UnityTest, Timeout(240000)]
        public IEnumerator TheFirstSeedTapHasNoLongFrame()
        {
            yield return LivingBox();
            var hud = GameHud.Instance; var spv = SeedPlaceView.Instance;
            for (int round = 0; round < 2; round++)
            {
                if (round > 0) { Sim.DebugGiveSeeds(1); GameClock.DebugAway(8); yield return new WaitForSecondsRealtime(5f); }
                hud.CloseSheet();
                yield return Frames(10);
                yield return TakeFromKit(hud, "kit-life-seed");
                Assert.AreEqual(GameHud.KitTool.Seed, hud.Hand, "the seed in hand");
                yield return new WaitForSecondsRealtime(0.5f);
                var p = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var sw = Stopwatch.StartNew();
                long worst = 0;
                BeginTouch(touchId, p, queueEventOnly: true);
                for (int i = 0; i < 3; i++) { long a = sw.ElapsedMilliseconds; yield return null; worst = System.Math.Max(worst, sw.ElapsedMilliseconds - a); }
                EndTouch(touchId, p, delta: Vector2.zero, queueEventOnly: true);
                touchId++;
                for (int i = 0; i < 60; i++) { long a = sw.ElapsedMilliseconds; yield return null; worst = System.Math.Max(worst, sw.ElapsedMilliseconds - a); }
                Debug.Log($"[b3fix] seed round {round}: {spv.Last}, worst frame {worst} ms");
                Assert.IsTrue(spv.GhostShown, "the first tap shows the ghost: " + spv.Last);
                Assert.Less(worst, 250, "no long frame on the first tap");
                hud.SetHand(GameHud.KitTool.None);
                yield return Frames(10);
            }
            // what a search for a seed spot over the whole screen costs (the tester's script did one between its shot and its tap)
            var mist = EcoMistView.Instance;
            var sw2 = Stopwatch.StartNew();
            int points = 0;
            for (float y = 20f; y < Screen.height; y += 40f)
                for (float x = 20f; x < Screen.width; x += 40f)
                    if (mist.GroundPoint(new Vector2(x, y), out var at)) { Sim.TreeCellFor(at.x, at.z); points++; }
            Debug.Log($"[b3fix] a 40 px screen scan for a seed spot: {sw2.ElapsedMilliseconds} ms ({points} land points)");
            AssertNoErrors("seed tap");
        }

        // ---------------------------------------------------------------- 4
        [UnityTest, Timeout(300000)]
        public IEnumerator TheClothFindsTheGlass_AndFastStrokesCleanHeavyGrime()
        {
            yield return LivingBox();
            var view = CampFrameView.Instance;
            CampFrameView.Auto = true;
            Debug.Log("[b3fix] " + GameClock.DebugAway(8));
            yield return Returned(view, 0);
            yield return Settled();
            var care = CareView.Instance;
            Sim.AddMess(Mathf.Max(0f, 0.72f - Sim.State.grime), 0f);
            yield return new WaitForSecondsRealtime(2f);
            float g0 = Sim.State.grime;
            float inViewAtFrame = care.GlassInView();
            Debug.Log($"[b3fix] camp frame: grime {g0:0.00}, glass mean {care.GlassMean:0.00}, glass in view {inViewAtFrame:0.00}");
            yield return Shot(Dir + "4a_camp_frame_grimy");

            var hud = GameHud.Instance;
            hud.SetCleanMode(true);
            yield return new WaitForSecondsRealtime(1f);
            float inView = care.GlassInView();
            Debug.Log($"[b3fix] cloth taken at the camp frame: zoom {Cam.zoom:0.00}, glass in view {inView:0.00}, grime {Sim.State.grime:0.00}");
            Assert.Greater(inView, 0.4f, "the glass is on screen at the camp frame");
            Assert.AreEqual(view.LastZoom, Cam.zoom, 0.03f, "the camera stays where the player had it");
            yield return Shot(Dir + "4b_cloth_taken");
            float gc = Sim.State.grime;

            // seven fast strokes edge to edge, top to bottom (what the tester did)
            int strokes0 = care.StrokeCount;
            for (int k = 0; k < 7; k++)
            {
                float y = Screen.height * (0.78f - k * 0.085f);
                float x0 = Screen.width * 0.06f, x1 = Screen.width * 0.94f;
                for (float x = x0; x < x1; x += 12f) if (care.GlassAt(new Vector2(x, y))) { x0 = x; break; }
                var path = k % 2 == 0 ? new[] { new Vector2(x0, y), new Vector2(x1, y) } : new[] { new Vector2(x1, y), new Vector2(x0, y) };
                yield return Drag(path, Screen.width * 0.06f);
                yield return Frames(2);
            }
            yield return Frames(10);
            float g1 = Sim.State.grime;
            Debug.Log($"[b3fix] 7 fast strokes: grime {g0:0.00} (cloth {gc:0.00}) -> {g1:0.00} ({care.StrokeCount - strokes0} strokes), glass mean {care.GlassMean:0.00}");
            yield return Shot(Dir + "4c_after_seven_strokes");
            Debug.Log($"[b3fix] glass out of reach from here: {care.LastUnreachableShare:0.00} of the grime");
            Assert.AreEqual(1, care.ReachClears, "the first stroke measured the glass in reach");
            Assert.GreaterOrEqual(care.StrokeCount - strokes0, 4, "the strokes over the glass wiped it");
            Assert.Less(g1, g0 * 0.75f, "heavy grime comes off visibly: a quarter or more in seven strokes (it was 0.01)");

            hud.SetCleanMode(false);
            yield return Settled();
            AssertAtTheCampFrame(view, "cloth put away", spot: false); // the rows that start above the glass pan the view (a press off the glass is the camera's)
            AssertNoErrors("cloth");
        }
    }
}
