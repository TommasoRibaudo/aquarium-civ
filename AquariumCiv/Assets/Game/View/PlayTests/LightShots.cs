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

        const int HW = 1080, HH = 1920, SW = 375, SH = 667;
        static readonly float[] Hours = { 6f, 12f, 17.5f, 19.5f, 22f };

        IEnumerator Grow(Era era)
        {
            var sim = GameClock.Instance.ActiveSim;
            for (int i = 0; i < 24 && sim.State.era < era; i++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            sim.CleanAll();
            yield return Frames(4);
            Debug.Log($"[light] era {sim.State.era}, lamps {Object.FindAnyObjectByType<BoxView>().LampCount}");
        }

        static IEnumerator Hour(float hour)
        {
            PeopleView.DebugSetHour(hour);
            yield return Wait(0.35f);
            var sh = RenderSettings.ambientProbe;
            Debug.Log($"[light] {hour} h: ambient sky {RenderSettings.ambientSkyColor}, SH L0 ({sh[0, 0]:0.000}, {sh[1, 0]:0.000}, {sh[2, 0]:0.000})");
        }

#if UNITY_EDITOR
        static void Stats(string tag) => Debug.Log($"[light] {tag}: batches {UnityEditor.UnityStats.batches}, draws {UnityEditor.UnityStats.drawCalls}, setpass {UnityEditor.UnityStats.setPassCalls}, tris {UnityEditor.UnityStats.triangles}, verts {UnityEditor.UnityStats.vertices}, shadow casters {UnityEditor.UnityStats.shadowCasters}");
#else
        static void Stats(string tag) { }
#endif

        /// <summary>The five hours in Village and Town at both phone sizes.</summary>
        [UnityTest, Timeout(300000), Category("LightHours")]
        public IEnumerator HoursInVillageAndTown([Values(Era.Village, Era.Town)] Era era)
        {
            yield return Box(HW, HH, 0f, 12f);
            yield return Grow(era);
            DayLightDriver.DebugPauseClock(true);
            foreach (float h in Hours)
            {
                yield return Hour(h);
                yield return Snap($"h-{era}-1080-{h:00.0}");
                Stats($"{era} {h}");
            }
            SetSize(SW, SH);
            yield return Wait(0.6f);
            foreach (float h in Hours)
            {
                yield return Hour(h);
                yield return Snap($"h-{era}-375-{h:00.0}");
            }
            DayLightDriver.DebugPauseClock(false);
            Done("hours");
        }

        /// <summary>The looks side by side at dusk and night, and what they cost.</summary>
        [UnityTest, Timeout(300000), Category("LightLooks")]
        public IEnumerator Looks()
        {
            yield return Box(HW, HH, 0f, 12f);
            yield return Grow(Era.Town);
            DayLightDriver.DebugPauseClock(true);
            foreach (var look in new[] { Lighting.LightLook.Today, Lighting.LightLook.SoftBlueDusk, Lighting.LightLook.SoftBlueMoon, Lighting.LightLook.Lamplight })
            {
                DayLightDriver.DebugLook(look);
                foreach (float h in new[] { 12f, 19.5f, 22f })
                {
                    yield return Hour(h);
                    yield return Snap($"look-{look}-{h:00.0}");
                }
                yield return FrameMs($"look {look} 22 h, town, 1080");
                Stats($"look {look} 22 h");
            }
            DayLightDriver.DebugLook(Lighting.LightLook.SoftBlueDusk);
            DayLightDriver.DebugNightFloor(0.45f);
            yield return Hour(22f);
            yield return Snap("look-floor-045-22.0");
            DayLightDriver.DebugNightFloor(0.9f);
            GameClock.Instance.ActiveSim.State.grime = 1f;
            yield return Wait(3f);
            yield return Snap("look-grime-22.0");
            yield return Hour(12f);
            yield return Wait(1f);
            yield return Snap("look-grime-12.0");
            DayLightDriver.DebugPauseClock(false);
            Done("looks");
        }

        /// <summary>The closest zoom at the fire and at a door: the glass, the doll, the pan, the glide back.</summary>
        [UnityTest, Timeout(240000), Category("LightClose")]
        public IEnumerator CloseIn()
        {
            yield return Box(HW, HH, 0f, 12f);
            yield return Grow(Era.Village);
            DayLightDriver.DebugPauseClock(true);
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            pv.Planner.Positions.TryGetPosition(Life.Place.Land(Life.PlaceKind.Fire), out float fx, out float fz);
            var fire = pv.transform.TransformPoint(new Vector3(fx, 0f, fz));
            int k = box.LampCount / 2;
            var door = new Vector3(box.LampX[k], 0f, box.LampZ[k]);
            foreach (var (name, at) in new[] { ("fire", fire), ("door", door) })
                foreach (float hour in new[] { 12f, 22f })
                {
                    yield return Hour(hour);
                    float yaw = name == "fire" ? 28f : -20f;
                    Cam.EaseTo(at, yaw, 34f, 0.2f, 0.01f);
                    yield return Wait(1.2f);
                    Debug.Log($"[light] {name} {hour}: zoom {Cam.zoom:0.00} target {Cam.target} panes {box.PaneOpacity(0):0.00} {box.PaneOpacity(1):0.00} {box.PaneOpacity(2):0.00} {box.PaneOpacity(3):0.00} frame {box.FrameOpacity(0):0.00} {box.FrameOpacity(2):0.00} {box.FrameOpacity(4):0.00}");
                    yield return Snap($"close-{name}-{hour:00}");
                    Cam.EaseTo(at, -yaw, 50f, 0.2f, 0.01f);
                    yield return Wait(1.2f);
                    yield return Snap($"close-{name}-{hour:00}-orbit");
                    Cam.EaseTo(Vector3.zero, 28f, 34f, 1f, 0.01f);
                    yield return Wait(0.5f);
                }
            yield return Hour(22f);
            yield return Snap("night-default");
            var who = System.Linq.Enumerable.FirstOrDefault(GameClock.Instance.ActiveSim.State.notables, n => n.alive && n.age >= 16f);
            if (who != null && PersonCardView.Instance.Open(who.id, false))
            {
                yield return Wait(1f);
                yield return Snap("night-sheet");
                Debug.Log($"[light] sheet tint alpha {FrostSwitch.TintAlpha:0.00}");
                PersonCardView.Instance.Close();
                yield return Wait(0.5f);
            }
            Cam.DebugFlyIn(door, 0.2f);
            yield return Wait(0.2f);
            GameHud.Instance.Toast("A line on the tank at night.", 3f);
            yield return Wait(0.6f);
            yield return Snap("night-toast");
            yield return Hour(12f);
            Cam.CancelFlight();
            Cam.EaseTo(Vector3.zero, 28f, 34f, 1f, 0.01f);
            yield return Wait(0.5f);
            yield return Snap("close-home-doll");
            // The glide: a fly-in at the closest zoom, timed.
            var home = Cam.target; float zoomHome = Cam.zoom;
            Cam.DebugFlyIn(fire, 0.2f);
            yield return Until(() => !Cam.IsFlying, 3f);
            float t0 = Time.realtimeSinceStartup, started = -1f;
            while (Cam.GlideArmed && Time.realtimeSinceStartup - t0 < 6f) { if (started < 0f && Cam.Gliding) started = Time.realtimeSinceStartup - t0; yield return null; }
            Debug.Log($"[light] glide: began {started:0.00} s after landing, home {Time.realtimeSinceStartup - t0:0.00} s; off by {(Cam.target - home).magnitude:0.000}, zoom {Cam.zoom:0.00} (was {zoomHome:0.00})");
            DayLightDriver.DebugPauseClock(false);
            Done("close in");
        }

        /// <summary>The sun's swap for the moon, in steps, and how long the Dusk moment's light takes to arrive.</summary>
        [UnityTest, Timeout(180000), Category("LightSwap")]
        public IEnumerator Swap()
        {
            yield return Box(SW, SH, 0f, 12f);
            yield return Grow(Era.Village);
            DayLightDriver.DebugPauseClock(true);
            foreach (float h in new[] { 19.5f, 20.2f, 20.6f, 20.8f, 21f, 21.5f, 22f })
            {
                yield return Hour(h);
                yield return Snap($"swap-{h:00.0}");
            }
            yield return Hour(12f);
            var day = DayLightDriver.Instance;
            PeopleView.SetHourGently(19.5f);
            float t0 = Time.realtimeSinceStartup;
            yield return Until(() => Mathf.Abs(day.Logic.ShownHour - 19.5f) < 0.05f, 15f);
            float arrived = Time.realtimeSinceStartup - t0;
            yield return Wait(1.2f);
            Debug.Log($"[light] the dusk moment's light arrived in {arrived:0.0} s, sun {day.Current.SunIntensity:0.00}, lamp {day.Current.Lamp:0.00}, ambient {RenderSettings.ambientSkyColor}");
            yield return Snap("swap-moment-dusk");
            DayLightDriver.DebugPauseClock(false);
            Done("swap");
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
