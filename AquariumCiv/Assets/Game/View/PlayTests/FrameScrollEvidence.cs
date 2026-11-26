// #655 / #667 evidence for the user (Explicit, run by hand): the frame rate the FrameRateDirector asks for at rest, with a sheet open, and
// after it closes; and a flick through Settings recorded frame by frame (scroll offset, the view's height, the frame time), with a frame grab
// every other frame for a GIF. In the Editor the director doesn't boot by itself (device only), so the test adds it.
using System.Collections;
using System.IO;
using System.Text;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    [Category("FrameEvidence")]
    public class FrameScrollEvidence : PlayTestBase
    {
        static string Dir => Path.Combine(Application.dataPath, "..", "Temp", "agent", "frame-evidence");

        [UnityTest, Explicit("Evidence for #655/#667; run by hand"), Timeout(300000)]
        public IEnumerator RestSheetFlick_FrameRateAndScroll()
        {
            Directory.CreateDirectory(Dir);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(750, 1334); // a small phone: Settings is longer than the screen, so it scrolls
            int vsyncWas = QualitySettings.vSyncCount, rateWas = Application.targetFrameRate;
            var director = new GameObject("FrameRateDirector (test)").AddComponent<FrameRateDirector>();
            var log = new StringBuilder("phase\tt\tdt_ms\ttarget\toffset\theight\tmoving\n");
            var hud = GameHud.Instance;
            float t0 = Time.realtimeSinceStartup;
            IEnumerator Phase(string name, float seconds, bool grab = false)
            {
                float start = Time.realtimeSinceStartup; int f = 0;
                while (Time.realtimeSinceStartup - start < seconds)
                {
                    yield return null;
                    var sc = hud.SettingsView != null ? hud.SettingsView.Scroll : null;
                    float off = sc != null ? sc.View.scrollOffset.y : 0f, h = sc != null ? sc.View.resolvedStyle.height : 0f;
                    log.Append($"{name}\t{Time.realtimeSinceStartup - t0:0.000}\t{Time.unscaledDeltaTime * 1000f:0.0}\t{Application.targetFrameRate}\t{off:0.0}\t{h:0.0}\t{(sc != null && sc.Moving ? 1 : 0)}\n");
                    if (grab && (f++ % 3) == 0) ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"flick_{f:000}.png"));
                }
            }
            yield return Phase("rest", 3f);
            int restTarget = Application.targetFrameRate;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root.Q("hud-settings")));
            yield return Until(() => hud.OpenSheet == GameHud.Settings, 2f);
            yield return Phase("sheet", 2f);
            int sheetTarget = Application.targetFrameRate;
            // the flick: a quick finger-up drag through the list, then the glide, grabbed
            var view = hud.SettingsView.Scroll.View;
            // on the labels' side of the list's middle (panel y down, screen y up), as SettingsPlayTests: not on a slider, which keeps the finger
            float midY = Screen.height - view.worldBound.center.y * Screen.height / hud.Root.worldBound.height;
            var mid = new Vector2(Screen.width * 0.2f, midY - Screen.height * 0.08f);
            var flick = Drag(new[] { mid, mid + new Vector2(0f, Screen.height * 0.08f), mid + new Vector2(0f, Screen.height * 0.16f) }, Screen.width * 0.06f);
            yield return flick;
            yield return Phase("glide", 2.5f, true);
            hud.CloseSheet();
            yield return Until(() => hud.OpenSheet == null, 2f);
            yield return Phase("closed", 3f);
            int afterTarget = Application.targetFrameRate;
            File.WriteAllText(Path.Combine(Dir, "frames.tsv"), log.ToString());
            Debug.Log($"[frames] targets: rest {restTarget}, sheet {sheetTarget}, after close {afterTarget}; log at {Dir}");
            Object.Destroy(director.gameObject);
            QualitySettings.vSyncCount = vsyncWas; Application.targetFrameRate = rateWas; // the Editor's own pace back
            CloseSlice();
            Assert.AreEqual(FrameRateDirector.Rest, restTarget, "30 at rest");
            Assert.AreEqual(FrameRateDirector.Active, sheetTarget, "60 with a sheet open");
            Assert.AreEqual(FrameRateDirector.Rest, afterTarget, "back to 30 after");
        }
    }
}
