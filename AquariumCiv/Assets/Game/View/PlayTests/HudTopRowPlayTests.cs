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
    /// <summary>#707: the top row is one look (no backing anywhere, icons in the title's ink) and Settings sits with Feedback and Photo,
    /// left of them, not beside the name. Shots (1179x2556) go to Temp/agent/707/. Run alone.</summary>
    [Category("HudTopRowPlay")]
    public class HudTopRowPlayTests : PlayTestBase
    {
        const string Out = "F:/GameDev-wt/pt1b/AquariumCiv/Temp/agent/707/";
        static SimCore Sim => GameClock.Instance.ActiveSim;

        IEnumerator Snap(string name)
        {
            Directory.CreateDirectory(Out);
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Out + name + ".png", t.EncodeToPNG());
            Object.Destroy(t);
        }

        void AssertOneLook(GameHud hud, string when)
        {
            foreach (var d in hud.Root.Query(className: "hud-disc").ToList())
                Assert.AreEqual(0f, d.resolvedStyle.backgroundColor.a, 0.001f, $"{when}: no backing behind a round button");
            var fbDisc = FeedbackUI.Instance.PillRoot.Query(className: "hud-disc").First();
            Assert.AreEqual(0f, fbDisc.resolvedStyle.backgroundColor.a, 0.001f, $"{when}: none behind the Feedback pill either");
            Assert.AreEqual(0f, hud.Root.Q("hud-title").resolvedStyle.backgroundColor.a, 0.001f, $"{when}: none behind the name");
            var tints = new[] { hud.Root.Q("hud-settings"), hud.Root.Q("hud-photo"), FeedbackUI.Instance.Pill }
                .Select(b => b.Q<GlassIcon>().resolvedStyle.unityBackgroundImageTintColor).ToList();
            Assert.AreEqual(tints[0], tints[1], $"{when}: Settings and Photo share one ink");
            Assert.AreEqual(tints[0], tints[2], $"{when}: and so does Feedback");
            Assert.AreEqual(hud.Root.Q(className: "hud-title-name").resolvedStyle.color.r, tints[0].r, 0.02f, $"{when}: the buttons' ink is the title's ink");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheTopRowIsOneLookAndSettingsIsWithTheButtons()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            var hud = GameHud.Instance;
            yield return Frames(10);
            PeopleView.DebugSetHour(12f);
            yield return new WaitForSecondsRealtime(2f);

            // the order: name on the left; Settings, Feedback, Photo on the right, in one row, none under another
            var title = hud.Root.Q("hud-title").worldBound;
            var set = hud.Root.Q("hud-settings").worldBound;
            var photo = hud.Root.Q("hud-photo").worldBound;
            var fb = FeedbackUI.Instance.Pill.worldBound;
            Assert.Less(title.xMax, set.xMin, "Settings is right of the name, not beside it");
            Assert.Greater(set.xMin - title.xMax, 100f, "with real distance between them (it is with the other buttons)");
            Assert.Less(set.xMax, fb.xMin + 2f, "Settings, then Feedback");
            Assert.Less(fb.xMax, photo.xMin + 2f, "then Photo");
            Assert.AreEqual(set.center.y, photo.center.y, 2f, "Settings is on Photo's line");
            Assert.AreEqual(fb.center.y, photo.center.y, 2f, "and so is Feedback");
            Assert.Less(Mathf.Abs((photo.xMin - fb.xMax) - (fb.xMin - set.xMax)), 3f, "evenly spaced");
            AssertOneLook(hud, "day");
            yield return Snap("day");

            PeopleView.DebugSetHour(22f);
            yield return new WaitForSecondsRealtime(2f);
            AssertOneLook(hud, "night");
            yield return Snap("night");

            PeopleView.DebugSetHour(12f);
            yield return new WaitForSecondsRealtime(1.6f);
            var who = Sim.State.notables.First(n => n.alive).id;
            Assert.IsTrue(PersonCardView.Instance.Open(who, false));
            yield return new WaitForSecondsRealtime(0.8f);
            AssertOneLook(hud, "person card");
            yield return Snap("card");
            PersonCardView.Instance.Close();
            yield return Frames(6);

            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-settings"));
            yield return Until(() => hud.OpenSheet == GameHud.Settings, 4f);
            Assert.AreEqual(GameHud.Settings, hud.OpenSheet, "tapping the gear opens Settings");
            yield return new WaitForSecondsRealtime(0.8f);
            AssertOneLook(hud, "settings sheet");
            yield return Snap("sheet");
            hud.CloseSheet();
            yield return Frames(8);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-title"));
            yield return Until(() => hud.OpenSheet == GameHud.Offices, 4f);
            Assert.AreEqual(GameHud.Offices, hud.OpenSheet, "the name still opens the offices");
            hud.CloseSheet();
            CloseSlice();
        }
    }
}
