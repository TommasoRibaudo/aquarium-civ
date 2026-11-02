using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #178: frame stats of the real scene, the same way before and after the frost test exists, so "off changes nothing"
    /// is a measurement. Run by hand; each run appends to Temp/agent/frost-probe.tsv. VSync off, so the frame time means
    /// something, with the medians of SetPass, draws and render textures over a fixed frame count.
    /// </summary>
    [Category("FrostProbe"), Explicit("Measures for a minute; run by hand, #178")]
    public class FrostCostProbe : PlayTestBase
    {
        const int Window = 300;

        static float Median(List<float> v) { var s = v.OrderBy(x => x).ToList(); return s.Count == 0 ? 0f : s[s.Count / 2]; }

        IEnumerator Measure(string label)
        {
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var rtCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Count");
            using var rtBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes");
            var sp = new List<float>(); var dc = new List<float>(); var rc = new List<float>(); var rb = new List<float>(); var ms = new List<float>();
            yield return Frames(5);
            for (int i = 0; i < Window; i++)
            {
                yield return null;
                sp.Add(setPass.LastValue); dc.Add(draws.LastValue); rc.Add(rtCount.LastValue); rb.Add(rtBytes.LastValue);
                ms.Add(Time.unscaledDeltaTime * 1000f);
            }
            var line = $"{label}\tsetpass {Median(sp):0}\tdraws {Median(dc):0}\trt {Median(rc):0}\trtMB {Median(rb) / (1024f * 1024f):0.0}\tframeMs {Median(ms):0.00}\tobjects {Resources.FindObjectsOfTypeAll<RenderTexture>().Length}";
            Debug.Log("[FrostProbe] " + line);
            Debug.Log("[FrostProbe] " + label + " textures: " + string.Join(", ", Resources.FindObjectsOfTypeAll<RenderTexture>().Select(r => $"{r.name} {r.width}x{r.height}").OrderBy(n => n)));
            Directory.CreateDirectory(Shots);
            File.AppendAllText(Shots + "frost-probe.tsv", line + "\n");
        }

        [UnityTest, Timeout(170000)]
        public IEnumerator FrameStats()
        {
            yield return OpenSlice(seed: 4242);
            int vs = QualitySettings.vSyncCount, fps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            try
            {
                yield return Frames(60);
                yield return Measure("before");
                yield return Measure("before-again");
                AquariumCiv.GameUI.FrostTestPanel.DebugToggle();
                yield return Frames(10);
                yield return Measure("panel-open-off");
                var panel = AquariumCiv.GameUI.FrostTestPanel.Instance;
                foreach (AquariumCiv.GameUI.FrostMode m in System.Enum.GetValues(typeof(AquariumCiv.GameUI.FrostMode)))
                {
                    if (m == AquariumCiv.GameUI.FrostMode.Off) continue;
                    panel.SetMode(m);
                    yield return Frames(10);
                    yield return Measure("mode-" + m);
                }
                AquariumCiv.GameUI.FrostTestPanel.DebugToggle();
                yield return Frames(10);
                yield return Measure("after-closed");
            }
            finally
            {
                QualitySettings.vSyncCount = vs;
                Application.targetFrameRate = fps;
            }
            AssertNoErrors("probe");
        }
    }
}
