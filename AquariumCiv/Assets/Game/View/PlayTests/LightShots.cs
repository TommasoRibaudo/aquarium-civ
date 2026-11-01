using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#113, #175, #146, #162, #193: the light and camera pass's pictures and numbers, on the phone's render pipeline asset
    /// (the Editor's default quality level is PC). Not checks: what a person judges. Explicit; into Temp/agent/light-shots.</summary>
    [Category("LightShots"), Explicit("Writes images; run by hand")]
    public class LightShots : PlayTestBase
    {
        const string Out = Shots + "light-shots/";
        /// <summary>Temp/agent/light-tag.txt names a run (before, after...).</summary>
        static string Tag => File.Exists(Shots + "light-tag.txt") ? File.ReadAllText(Shots + "light-tag.txt").Trim() : "x";
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        UnityEngine.Rendering.RenderPipelineAsset pipelineWas;

        IEnumerator Box(int w, int h, float years, float hour, int seed = 1234)
        {
            yield return OpenSlice(w, h, seed);
            // The Mobile quality level excludes Standalone, so in play mode the only level is PC: put the phone's asset on it for the run.
            pipelineWas = QualitySettings.renderPipeline;
#if UNITY_EDITOR
            QualitySettings.renderPipeline = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
#endif
            yield return null;
            Debug.Log($"[light] pipeline {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name}");
            if (years > 0f) SimLayoutSource.DebugSkipYears(years);
            yield return null;
            GameClock.Instance.ActiveSim.CleanAll();
            PeopleView.DebugSetHour(hour);
            yield return Wait(0.3f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            ShowMeDirector.BubblesOn = false;
        }

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        /// <summary>Saves the frame; logs its mean luminance (Rec. 709 over the stored values) so hours can be compared.</summary>
        static IEnumerator Snap(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Out + name + ".png", tex.EncodeToPNG());
            Debug.Log($"[light] {name} {tex.width}x{tex.height} luma {MeanLuma(tex):0.000}");
            Object.Destroy(tex);
        }

        static float MeanLuma(Texture2D tex)
        {
            var px = tex.GetPixels32();
            double sum = 0;
            for (int i = 0; i < px.Length; i += 7) sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
            return (float)(sum / (px.Length / 7.0) / 255.0);
        }

        /// <summary>Mean frame time (ms) over <paramref name="frames"/> frames: an Editor number, it only ranks things.</summary>
        static IEnumerator FrameMs(string tag, int frames = 240)
        {
            int vs = QualitySettings.vSyncCount, fps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            yield return null;
            float sum = 0f, worst = 0f;
            for (int i = 0; i < frames; i++) { yield return null; sum += Time.unscaledDeltaTime; worst = Mathf.Max(worst, Time.unscaledDeltaTime); }
            QualitySettings.vSyncCount = vs; Application.targetFrameRate = fps;
            Debug.Log($"[light] {tag}: {sum / frames * 1000f:0.00} ms avg, {worst * 1000f:0.0} ms worst over {frames} frames");
        }

        void Done(string when)
        {
            QualitySettings.renderPipeline = pipelineWas;
            AssertNoErrors(when);
        }

        /// <summary>#193: the wall shadow at phone size, at three zooms, plus frame time.</summary>
        [UnityTest, Timeout(240000), Category("LightShadows")]
        public IEnumerator Shadows()
        {
            string tag = Tag;
            yield return Box(375, 667, 14f, 12f);
            Debug.Log($"[light] asset {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name} near {Camera.main.nearClipPlane:0.0} d {Vector3.Distance(Camera.main.transform.position, Cam.target):0.0}");
            yield return Snap($"193-{tag}-375-default");
            Cam.EaseTo(Vector3.zero, Cam.yaw, Cam.pitch, 1.3f, 0.01f);
            yield return Wait(0.3f);
            yield return Snap($"193-{tag}-375-out");
            Cam.EaseTo(new Vector3(-2f, 0f, 1.6f), Cam.yaw, 38f, 0.45f, 0.01f);
            yield return Wait(0.3f);
            yield return Snap($"193-{tag}-375-close");
            SetSize(1080, 1920);
            yield return Wait(0.5f);
            yield return Snap($"193-{tag}-1080-close");
            Cam.EaseTo(Vector3.zero, Cam.yaw, 34f, 1f, 0.01f);
            yield return Wait(0.3f);
            yield return Snap($"193-{tag}-1080-default");
            yield return FrameMs($"193-{tag} 1080 default");
            Done("shadow shots");
        }
    }
}
