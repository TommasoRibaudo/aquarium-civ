using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #702: the Feedback pill is above every panel (a modal sheet aside) and always takes a tap. Each panel is opened, and the pill is
    /// tapped through the input system at its screen position (a fixture touchscreen: UI Toolkit pointer events, no method call);
    /// the feedback sheet must open every time. Shots (1179x2556) go to Temp/agent/702/. Run alone.
    /// </summary>
    [Category("FeedbackPillPlay")]
    public class FeedbackPillPlayTests : PlayTestBase
    {
        const string Out = "F:/GameDev-wt/pt1b/AquariumCiv/Temp/agent/702/";
        static SimCore Sim => GameClock.Instance.ActiveSim;

        IEnumerator Snap(string name)
        {
            Directory.CreateDirectory(Out);
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Out + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        IEnumerator Open()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box (the kit's life chooser)");
            Assert.IsNotNull(FeedbackUI.Instance?.Pill, "the Feedback pill is there (test build)");
            yield return Frames(10);
        }

        /// <summary>The pill is shown, nothing else is on top of it, and a touch at its centre opens the sheet.</summary>
        IEnumerator TapPillOpensSheet(string what, string shot)
        {
            var fb = FeedbackUI.Instance;
            var pill = fb.Pill;
            yield return Frames(4);
            Assert.AreEqual(DisplayStyle.Flex, pill.resolvedStyle.display, what);
            var at = GameUiPanel.ScreenCenter(pill);
            var panelPt = RuntimePanelUtils.ScreenToPanel(pill.panel, new Vector2(at.x, Screen.height - at.y));
            Assert.AreEqual(pill, FindPill(pill.panel.Pick(panelPt)), $"{what}: the pill is what a touch at its place reaches");
            if (shot != null) yield return Snap(shot);
            yield return TapAt(at);
            yield return Until(() => fb.IsOpen, 4f);
            Assert.IsTrue(fb.IsOpen, $"{what}: tapping Feedback opens the sheet");
            fb.Close();
            yield return Frames(4);
            Assert.IsFalse(fb.IsOpen);
        }

        static VisualElement FindPill(VisualElement hit)
        {
            for (var e = hit; e != null; e = e.parent) if (e.name == "hud-feedback") return e;
            return hit;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator FeedbackTakesATapOverEveryPanel()
        {
            yield return Open();
            var hud = GameHud.Instance;
            var fb = FeedbackUI.Instance;
            try
            {
                yield return TapPillOpensSheet("nothing open", null);

                // the kit
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.AreEqual(GameHud.Kit, hud.OpenSheet, "the kit is open");
                yield return TapPillOpensSheet("the kit", "kit");
                Assert.IsNull(hud.OpenSheet, "the HUD's sheet shut when Feedback opened");

                // the life chooser
                Sim.DebugJarsReady();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                yield return new WaitForSecondsRealtime(0.6f);
                // #668: no jar to open first: the tiles are on the kit's shelves
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsTrue(hud.LifeRowOpen, "the life chooser is open");
                yield return TapPillOpensSheet("the life chooser", "chooser");

                // the person card
                var who = Sim.State.notables.First(n => n.alive).id;
                Assert.IsTrue(PersonCardView.Instance.Open(who, false), "the card opens");
                yield return new WaitForSecondsRealtime(0.8f);
                yield return TapPillOpensSheet("the person card", "card");
                PersonCardView.Instance.Close();
                yield return Frames(6);

                // a moment
                var mp = MomentPlayer.Instance;
                Assert.IsNotNull(mp);
                Sim.State.grime = 0f;
                Assert.IsTrue(Sim.ForceEraReady());
                yield return null;
                MomentPlayer.DebugHurry(4f);
                MomentPlayer.DebugHoldHurry = true;
                Assert.IsTrue(mp.Watch(true), "the moment starts");
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.IsTrue(mp.Playing, "the moment is playing");
                yield return TapPillOpensSheet("a moment", "moment");
                MomentPlayer.DebugHoldHurry = false;
                if (mp.Playing) mp.Leave();
                yield return Frames(10);

                // photo mode
                hud.SetCameraMode(true);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(hud.CameraMode, "photo mode is on");
                yield return TapPillOpensSheet("photo mode", "photo");
                hud.SetCameraMode(false);
                yield return Frames(6);

                // the quick question
                StringAssert.StartsWith("asking", PulseQuestionView.DebugAsk("know"));
                yield return Until(() => PulseQuestionView.Instance.IsOpen, 5f);
                Assert.IsTrue(PulseQuestionView.Instance.IsOpen, "the quick question is up");
                yield return new WaitForSecondsRealtime(0.8f);
                yield return TapPillOpensSheet("the quick-question sheet", "question");
            }
            finally
            {
                MomentPlayer.DebugHoldHurry = false;
                if (fb != null && fb.IsOpen) fb.Close();
                CloseSlice();
            }
        }
    }
}
