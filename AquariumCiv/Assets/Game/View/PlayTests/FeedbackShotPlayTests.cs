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

        static Color MeanOf(Texture2D t, Rect r)
        {
            var c = t.GetPixels32();
            long a = 0, b = 0, d = 0, n = 0;
            for (int y = (int)r.yMin; y < (int)r.yMax; y++)
                for (int x = (int)r.xMin; x < (int)r.xMax; x++)
                {
                    var p = c[y * t.width + x];
                    a += p.r; b += p.g; d += p.b; n++;
                }
            return new Color(a / (255f * n), b / (255f * n), d / (255f * n));
        }

        static float Mean(Color c) => (c.r + c.g + c.b) / 3f;

        /// <summary>Tester run 7: the JPG read 0.881 where the screen read 0.766, and caught the icon pressed.</summary>
        [UnityTest, Timeout(60000), Category("Bundle3"), Category("ShotBright")]
        public IEnumerator TheShotHasTheScreensBrightnessAndNoPressedIcon()
        {
            yield return OpenSlice(1179, 2556, 1234);
            ShowMeDirector.BubblesOn = false;
            GameClock.Instance.ActiveSim.CleanAll();
            yield return new WaitForSecondsRealtime(0.6f);
            var hud = GameHud.Instance;
            var fb = FeedbackUI.Instance;
            var icon = GameUiPanel.ScreenCenter(hud.Root, "hud-feedback");
            yield return new WaitForEndOfFrame();
            var screen = ScreenCapture.CaptureScreenshotAsTexture();
            float k = 1f / screen.height;
            var iconAt = new Vector2(icon.x * k, icon.y * k); // in shares of the height
            var whole = MeanOf(screen, new Rect(0f, 0f, screen.width, screen.height));
            var ring = MeanOf(screen, new Rect(icon.x + 44f, icon.y + 40f, 12f, 12f)); // a corner of the pressed disc, clear of the glyph
            Object.Destroy(screen);
            yield return TapAt(icon);
            yield return Until(() => fb.IsOpen, 3f);
            Assert.IsTrue(fb.IsOpen);
            var jpg = new Texture2D(2, 2);
            Assert.IsTrue(jpg.LoadImage(fb.LastShot));
            var wholeJ = MeanOf(jpg, new Rect(0f, 0f, jpg.width, jpg.height));
            float s = jpg.height;
            var ringJ = MeanOf(jpg, new Rect((iconAt.x + 44f * k) * s, (iconAt.y + 40f * k) * s, Mathf.Max(2f, 12f * k * s), Mathf.Max(2f, 12f * k * s)));
            Object.Destroy(jpg);
            File.WriteAllBytes(Shots + "bundle3-fb-shot.jpg", fb.LastShot);
            Debug.Log($"[feedback] screen {Mean(whole):0.000} jpg {Mean(wholeJ):0.000}; over the icon: {Mean(ring):0.000} jpg {Mean(ringJ):0.000}");
            Assert.AreEqual(Mean(whole), Mean(wholeJ), 0.02f, "as bright as the screen");
            Assert.AreEqual(Mean(ring), Mean(ringJ), 0.03f, "the icon at rest");
            AssertNoErrors("shot brightness");
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
