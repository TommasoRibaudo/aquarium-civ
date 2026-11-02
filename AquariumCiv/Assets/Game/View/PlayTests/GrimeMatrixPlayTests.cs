using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>The glass at four hours and three grime levels, river and desert, with numbers beside the shots: how much of the
    /// mask is pinned at full, how far the glass moves the picture, and how much of the town's contrast comes through it.
    /// Explicit: a measure to run by hand (category GrimeMatrix, --include_explicit true).</summary>
    [Category("GrimeMatrix")]
    public class GrimeMatrixPlayTests : PlayTestBase
    {
        const string Tag = "after";
        static readonly float[] Hours = { 12f, 17.5f, 19.5f, 22f };

        [UnityTest, Explicit("a measure, not a check"), Timeout(300000)]
        public IEnumerator River()
        {
            yield return OpenSlice(1080, 1920, 20261101, skipFirstRun: false);
            ShowMeDirector.BubblesOn = false;
            Debug.Log($"[matrix] river start: grime {GameClock.Instance.ActiveSim.State.grime:0.000}");
            yield return Sweep("river", GameClock.Instance.ActiveSim.State.grime);
        }

        [UnityTest, Explicit("a measure, not a check"), Timeout(300000)]
        public IEnumerator Desert()
        {
            yield return OpenSlice(1080, 1920, 1234);
            ShowMeDirector.BubblesOn = false;
            Assert.That(BoxCompleteController.DebugComplete(BoxSetting.DesertOasis), Does.StartWith("box completed"));
            yield return Frames(30);
            var sim = GameClock.Instance.ActiveSim;
            Assert.AreEqual(1, sim.State.boxIndex);
            Debug.Log($"[matrix] desert start: grime {sim.State.grime:0.000}, first run {sim.State.firstRun.active}");
            yield return Sweep("desert", sim.State.grime);
        }

        /// <summary>The smallest phone on the real path: an era moment through its Watch button, then the shelf. Shots: small_*.</summary>
        [UnityTest, Explicit("shots to look at"), Timeout(300000)]
        public IEnumerator SmallPhone()
        {
            yield return OpenSlice(375, 667, 1234);
            ShowMeDirector.BubblesOn = false;
            var sim = GameClock.Instance.ActiveSim;
            for (int i = 0; i < 4; i++) { SimLayoutSource.DebugSkipYears(5f); yield return null; }
            sim.CleanAll();
            yield return Frames(2);
            var mp = MomentPlayer.Instance;
            Assert.IsTrue(sim.ForceEraReady(), "the next era is ready");
            MomentPlayer.DebugWaited(100);
            yield return new WaitForSecondsRealtime(0.7f);
            var watch = mp.GetComponent<UIDocument>().rootVisualElement.Q<Button>("moment-watch");
            yield return TapAt(GameUiPanel.ScreenCenter(watch));
            Assert.IsTrue(mp.Playing, "a tap on Watch starts the moment");
            MomentPlayer.DebugHurry(3f);
            MomentPlayer.DebugHoldHurry = true;
            yield return Until(() => mp.Director.Time >= 4f, 20f);
            yield return Shot("small_moment_early");
            yield return Until(() => mp.Director.Time >= 8f, 20f);
            yield return Shot("small_moment");
            MomentPlayer.DebugHurry(6f);
            yield return Until(() => !mp.Playing, 60f);
            MomentPlayer.DebugHoldHurry = false;
            yield return Frames(6);
            sim = GameClock.Instance.ActiveSim;
            Assert.That(BoxCompleteController.DebugComplete(BoxSetting.DesertOasis), Does.StartWith("box completed"));
            yield return Frames(20);
            for (int i = 0; i < 12 && FirstRunView.Instance.Active; i++) { FirstRunView.Instance.Run.SkipStep(); yield return Frames(2); }
            var c = BoxCompleteController.Instance;
            Assert.IsTrue(c.OpenShelf(), "the shelf");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("small_shelf");
            c.CloseShelf();
            yield return Frames(10);
            AssertNoErrors("small phone");
        }

        IEnumerator Sweep(string where, float start)
        {
            DayLightDriver.DebugPauseClock(true);
            foreach (var (name, level) in new[] { ("start", start), ("g08", 0.8f), ("g10", 1f) })
                foreach (float hour in Hours)
                {
                    yield return Apply(level, hour);
                    string h = hour.ToString("00.0", CultureInfo.InvariantCulture);
                    Report($"{where} {name} {level:0.00} h{h}");
                    yield return Shot($"m_{Tag}_{where}_{name}_{h}");
                }
            DayLightDriver.DebugPauseClock(false);
        }

        internal static IEnumerator Apply(float level, float hour)
        {
            var care = CareView.Instance;
            GameClock.Instance.ActiveSim.State.grime = level;
            yield return Frames(3);               // the view's own sync first, so a ResetTo isn't undone by it
            foreach (var m in care.Masks) m.ResetTo(level);
            PeopleView.DebugSetHour(hour);
            yield return Frames(6);
        }

        static void Report(string label)
        {
            var care = CareView.Instance;
            int live = 0, full = 0; double sum = 0;
            foreach (var m in care.Masks)
                for (int i = 0; i < m.grime.Length; i++)
                {
                    if (m.weight[i] <= 0.01f) continue;
                    live++; sum += m.grime[i];
                    if (m.grime[i] >= 0.98f) full++;
                }
            var panes = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.name.StartsWith("Glass") && r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var cam = Camera.main;
            var front = panes.First(r => r.name == "GlassFront");
            var b = front.bounds;
            Vector2 lo = cam.WorldToScreenPoint(new Vector3(b.min.x, 1.1f, b.center.z)), hi = cam.WorldToScreenPoint(new Vector3(b.max.x, b.max.y, b.center.z));
            var rect = new RectInt(Mathf.RoundToInt(Mathf.Min(lo.x, hi.x)), Mathf.RoundToInt(Mathf.Min(lo.y, hi.y)), Mathf.RoundToInt(Mathf.Abs(hi.x - lo.x)), Mathf.RoundToInt(Mathf.Abs(hi.y - lo.y)));
            foreach (var p in panes) p.enabled = false;
            var bare = Render();
            foreach (var p in panes) p.enabled = true;
            var shown = Render();
            float change = 0f, hidden = 0f; int n = 0;
            float bareSum = 0f, shownSum = 0f, bareSq = 0f, shownSq = 0f, shownBright = 0f, bareBright = 0f;
            var ca = bare.GetPixels32(); var cb = shown.GetPixels32();
            for (int y = rect.yMin; y < rect.yMax; y += 2)
                for (int x = rect.xMin; x < rect.xMax; x += 2)
                {
                    int i = y * bare.width + x;
                    if (i < 0 || i >= ca.Length) continue;
                    float la = (ca[i].r + ca[i].g + ca[i].b) / 3f, lb = (cb[i].r + cb[i].g + cb[i].b) / 3f;
                    float d = (Mathf.Abs(ca[i].r - cb[i].r) + Mathf.Abs(ca[i].g - cb[i].g) + Mathf.Abs(ca[i].b - cb[i].b)) / 3f;
                    change += d; if (d > 50f) hidden++;
                    bareSum += la; shownSum += lb; bareSq += la * la; shownSq += lb * lb; bareBright += la; shownBright += lb; n++;
                }
            float ma = bareSum / n, mb = shownSum / n;
            float sa = Mathf.Sqrt(Mathf.Max(0f, bareSq / n - ma * ma)), sb = Mathf.Sqrt(Mathf.Max(0f, shownSq / n - mb * mb));
            Opacity(out float opacity, out float top);
            Debug.Log($"[matrix] {label}: opacity {opacity:0.000} p95 {top:0.000} | mask mean {care.GlassMean:0.000} above ground {sum / Mathf.Max(1, live):0.000} pinned {full / (float)Mathf.Max(1, live):0.00} | moved {change / n:0.0} hidden {hidden / n:0.00} bare lum {ma:0} shown lum {mb:0} contrast kept {sb / Mathf.Max(0.1f, sa):0.00}");
            Object.Destroy(bare); Object.Destroy(shown);
        }

        /// <summary>What the front pane really lets through, from the real shader at the real hour: the pane alone over black and over
        /// white, no post, so alpha = 1 - (white - black). Over its part above the ground, in linear values.</summary>
        internal static void Opacity(out float mean, out float p95)
        {
            var cam = Camera.main;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            var panes = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.name.StartsWith("Glass") && r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var front = panes.First(r => r.name == "GlassFront");
            var b = front.bounds;
            Vector2 lo = cam.WorldToScreenPoint(new Vector3(b.min.x, 1.2f, b.center.z)), hi = cam.WorldToScreenPoint(new Vector3(b.max.x, b.max.y - 0.1f, b.center.z));
            int x0 = Mathf.RoundToInt(Mathf.Min(lo.x, hi.x)) + 8, x1 = Mathf.RoundToInt(Mathf.Max(lo.x, hi.x)) - 8;
            int y0 = Mathf.RoundToInt(Mathf.Min(lo.y, hi.y)) + 8, y1 = Mathf.RoundToInt(Mathf.Max(lo.y, hi.y)) - 8;
            var (flags, bg, mask, post) = (cam.clearFlags, cam.backgroundColor, cam.cullingMask, data.renderPostProcessing);
            foreach (var p in panes) if (p != front) p.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.cullingMask = 1 << CareView.GlassLayer;
            data.renderPostProcessing = false;
            cam.backgroundColor = Color.black;
            var dark = Render(true);
            cam.backgroundColor = Color.white;
            var light = Render(true);
            (cam.clearFlags, cam.backgroundColor, cam.cullingMask, data.renderPostProcessing) = (flags, bg, mask, post);
            foreach (var p in panes) p.enabled = true;
            var ca = dark.GetPixels32(); var cb = light.GetPixels32();
            var seen = new List<float>();
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(dark.height, y1); y += 3)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(dark.width, x1); x += 3)
                {
                    int i = y * dark.width + x;
                    float a = 1f - (cb[i].g - ca[i].g) / 255f;
                    if (a >= 0.03f) seen.Add(a); // the pane is a slanted shape in this rectangle: clean glass is 0.05, the rest isn't glass
                }
            seen.Sort();
            mean = seen.Average();
            p95 = seen[(int)(seen.Count * 0.95f)];
            Object.Destroy(dark); Object.Destroy(light);
        }

        static Texture2D Render(bool linear = false)
        {
            var cam = Camera.main;
            int w = Screen.width, h = Screen.height;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB, 1);
            var keep = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = keep;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
