using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #178: the frosted sheet test, by touch. Off means no copy: the pass count stands still
    /// and there is no texture. Each mode shows the sheet the way it should, and closing leaves nothing behind.
    /// </summary>
    [Category("FrostTest")]
    public class FrostTestPlayTests : PlayTestBase
    {
        float scaleWas;

        IEnumerator Open()
        {
            yield return OpenSlice(seed: 4242);
            // Play mode stays on PC_RPAsset whatever the quality level (tooling.md), so take the setting that differs and matters: phones draw at 0.8.
            var asset = UniversalRenderPipeline.asset;
            scaleWas = asset.renderScale;
            asset.renderScale = 0.8f;
            yield return Frames(3);
            Debug.Log($"[FrostTest] pipeline {asset.name} scale {asset.renderScale}");
        }

        public override void TearDown()
        {
            FrostTestPanel.SendOverride = null;
            if (FrostTestPanel.Instance != null) FrostTestPanel.DebugToggle();
            FrostBackdrop.Stop();
            if (scaleWas > 0f) UniversalRenderPipeline.asset.renderScale = scaleWas;
            base.TearDown();
        }

        IEnumerator Tap(string name)
        {
            var e = FrostTestPanel.Instance.Root.Q(name);
            Assert.IsNotNull(e, name);
            yield return Frames(3);
            yield return TapAt(GameUiPanel.ScreenCenter(e));
            yield return Frames(4);
        }

        IEnumerator AssertNoCopy(string when)
        {
            long before = FrostBackdrop.CopiesRecorded;
            yield return Frames(30);
            Assert.AreEqual(before, FrostBackdrop.CopiesRecorded, $"{when}: no pass recorded");
            Assert.IsFalse(FrostBackdrop.Active, $"{when}: no callback");
            Assert.IsNull(FrostBackdrop.Target, $"{when}: no texture");
        }

        static float Brightness(RenderTexture rt)
        {
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = was;
            float sum = t.GetPixels32().Average(c => (c.r + c.g + c.b) / 3f);
            Object.Destroy(t);
            return sum;
        }

        [UnityTest, Timeout(170000)]
        public IEnumerator OffAddsNoPassAndEachModeShowsTheSheet()
        {
            yield return Open();
            Assert.IsNull(FrostTestPanel.Instance, "nothing exists until the debug panel asks");
            yield return AssertNoCopy("never opened");

            FrostTestPanel.DebugToggle();
            yield return Frames(6);
            var panel = FrostTestPanel.Instance;
            var sheet = panel.Root.Q("frost-sheet");
            var inner = panel.Root.Q("frost-inner");
            var image = panel.Root.Q("frost-image");
            var tint = panel.Root.Q("frost-tint");
            Assert.AreEqual(FrostMode.Off, panel.Mode);
            Assert.AreEqual(DisplayStyle.None, sheet.resolvedStyle.display, "off shows no sheet");
            yield return AssertNoCopy("open, off");
            yield return Shot("frost-0-off");

            yield return Tap("frost-mode-1");
            Assert.AreEqual(FrostMode.Flat, panel.Mode);
            Assert.AreEqual(DisplayStyle.Flex, sheet.resolvedStyle.display, "flat shows the sheet");
            Assert.AreEqual(0.30f, sheet.worldBound.height / panel.Root.worldBound.height, 0.005f, "30 % of the height");
            Assert.AreEqual(0.92f, tint.resolvedStyle.backgroundColor.a, 0.01f);
            Assert.AreEqual(DisplayStyle.None, inner.resolvedStyle.display, "flat draws no backdrop");
            yield return AssertNoCopy("flat");
            yield return Shot("frost-1-flat");

            var rig = Camera.main.GetComponent<BoxCamera>();
            rig.target += Vector3.up * 1.6f; // the tank behind the sheet: its dark glass edges are what the blur has to soften
            rig.Apply();
            yield return Frames(3);
            foreach (var (name, mode, divisor) in new[] { ("frost-mode-2", FrostMode.Eighth, 8), ("frost-mode-3", FrostMode.Quarter, 4) })
            {
                yield return Tap(name);
                Assert.AreEqual(mode, panel.Mode);
                long before = FrostBackdrop.CopiesRecorded;
                yield return Frames(20);
                Assert.Greater(FrostBackdrop.CopiesRecorded, before, $"{mode}: the copy runs");
                var rt = FrostBackdrop.Target;
                Assert.IsNotNull(rt);
                Assert.AreEqual(Camera.main.pixelWidth / divisor, rt.width, $"{mode}: copy size");
                Assert.AreEqual(Camera.main.pixelHeight / divisor, rt.height, $"{mode}: copy size");
                Assert.Greater(Brightness(rt), 5f, $"{mode}: the copy holds a picture");
                Assert.AreEqual(rt, image.resolvedStyle.backgroundImage.renderTexture, $"{mode}: the sheet shows the copy");
                Assert.AreEqual(1, inner.resolvedStyle.filter.Count(), $"{mode}: one blur");
                Assert.AreEqual(FrostModes.Blur(mode), inner.resolvedStyle.filter.First().GetParameter(0).floatValue, 0.01f, $"{mode}: blur radius");
                Assert.AreEqual(0.72f, tint.resolvedStyle.backgroundColor.a, 0.01f, $"{mode}: frost alpha");
                Assert.AreEqual(0f, image.worldBound.x, 1f, $"{mode}: the copy lines up with the screen");
                Assert.AreEqual(0f, image.worldBound.y, 1f, $"{mode}: the copy lines up with the screen");
                Assert.AreEqual(panel.Root.worldBound.height, image.worldBound.height, 1f, $"{mode}: the copy lines up with the screen");
                yield return Shot("frost-" + (int)mode + "-" + divisor);
            }

            var onSheet = GameUiPanel.ScreenCenter(sheet);
            Assert.IsTrue(UiInput.Over(onSheet), "the sheet keeps its taps from the camera");
            Assert.IsFalse(UiInput.Over(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f)), "the tank is still free");

            yield return Tap("frost-mode-0");
            Assert.AreEqual(FrostMode.Off, panel.Mode);
            Assert.AreEqual(DisplayStyle.None, sheet.resolvedStyle.display);
            yield return AssertNoCopy("back to off");

            yield return Tap("frost-close");
            yield return Frames(3);
            Assert.IsNull(FrostTestPanel.Instance, "closing removes it");
            Assert.IsFalse(Resources.FindObjectsOfTypeAll<RenderTexture>().Any(r => r.name == "FrostBackdrop"), "no copy texture is left");
            yield return AssertNoCopy("closed");
            AssertNoErrors("modes");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheAutomaticRunMeasuresEveryModeAndTheNumbersGoInANote()
        {
            yield return Open();
            int capWas = Application.targetFrameRate;
            FrostTestPanel.DebugToggle();
            yield return Frames(6);
            var panel = FrostTestPanel.Instance;
            panel.autoSeconds = 1.5f;
            panel.settleSeconds = 0.3f;
            string sent = null;
            FrostTestPanel.SendOverride = t => { sent = t; return true; };

            yield return Tap("frost-auto");
            Assert.IsTrue(panel.AutoRunning);
            yield return Until(() => !panel.AutoRunning, 20f);
            Assert.IsFalse(panel.AutoRunning, "the run ends by itself");
            Assert.AreEqual(FrostMode.Off, panel.Mode, "and ends on off");
            Assert.IsFalse(FrostBackdrop.Active);
            for (int m = 0; m < FrostModes.Count; m++)
                Assert.Greater(panel.Window((FrostMode)m).Count, 10, $"{(FrostMode)m} has numbers");

            yield return Tap("frost-send");
            Assert.IsNotNull(sent, "Send hands over the numbers");
            Assert.LessOrEqual(sent.Length, FeedbackReport.MaxTextChars, sent);
            foreach (var label in new[] { "Off:", "Flat:", "1/8 + 6:", "1/4 + 14:" }) StringAssert.Contains(label, sent);
            yield return Shot("frost-summary");
            Debug.Log("[FrostTest] note text:\n" + sent);

            yield return Tap("frost-cap");
            Assert.AreEqual(60, Application.targetFrameRate, "Cap reaches 60");
            yield return Tap("frost-close");
            yield return Frames(3);
            Assert.AreEqual(capWas, Application.targetFrameRate, "closing restores the frame cap");
            AssertNoErrors("auto");
        }
    }
}
