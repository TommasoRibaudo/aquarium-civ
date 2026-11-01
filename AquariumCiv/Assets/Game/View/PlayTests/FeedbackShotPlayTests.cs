using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#153: a feedback shot keeps the HUD, fits the issue in the busiest scene, and a note isn't filed twice.</summary>
    [Category("FeedbackShot")]
    public class FeedbackShotPlayTests : PlayTestBase
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator TheShotKeepsTheHudAndFitsTheIssue()
        {
            yield return OpenSlice(1179, 2556, 1234);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            GameClock.Instance.ActiveSim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(30f);
            yield return Frames(4);
            GameClock.Instance.ActiveSim.CleanAll();
            var cam = Camera.main.GetComponent<BoxCamera>();
            cam.EaseTo(Vector3.zero, cam.yaw, 50f, 0.4f, 0.1f); // the town close up: the most detail a JPG has to carry
            yield return new WaitForSecondsRealtime(0.5f);
            var hud = GameHud.Instance;
            var fb = FeedbackUI.Instance;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-feedback"));
            yield return Until(() => fb.IsOpen, 3f);
            Assert.IsTrue(fb.IsOpen);
            Assert.IsTrue(hud.IsVisible, "the HUD never left");
            var jpg = fb.LastShot;
            Assert.IsNotNull(jpg);
            Debug.Log($"[feedback] shot {jpg.Length} bytes");
            Assert.LessOrEqual(ScreenShot.Base64Chars(jpg.Length), ScreenShot.MaxBase64Chars, "it fits");
            File.WriteAllBytes(Shots + "bundle2-153-shot.jpg", jpg);
            AssertNoErrors("feedback shot");
        }

        [Test]
        public void ANoteAtTheLimitStaysUnderIt()
        {
            var lines = Enumerable.Repeat(new string('x', 200), 150).ToList();
            var body = FeedbackReport.Body(FeedbackKind.Bug, new string('y', FeedbackReport.MaxTextChars), true, "tester", new List<(string, string)> { ("a", "b") }, lines.Take(40).ToList());
            Assert.LessOrEqual(body.Length + ScreenShot.MaxBase64Chars + FeedbackReport.ScreenshotWrapChars, FeedbackReport.MaxIssueChars);
            StringAssert.Contains("base64", FeedbackReport.ScreenshotBlock(new string('A', 10)));
            Assert.LessOrEqual(FeedbackReport.ScreenshotBlock("").Length, FeedbackReport.ScreenshotWrapChars);
        }

        [Test]
        public void ANoteAlreadyOnGitHubIsFoundByItsId()
        {
            string id = "20261101120000000-abcd1234";
            string json = "[{\"body\":\"text " + FeedbackReport.NoteIdLine(id).Replace("\n", "\\n") + "\"}]";
            Assert.IsTrue(FeedbackReport.AlreadyFiled(json, id));
            Assert.IsFalse(FeedbackReport.AlreadyFiled(json, "20261101120000000-other"));
            Assert.IsFalse(FeedbackReport.AlreadyFiled(null, id));
        }
    }
}
