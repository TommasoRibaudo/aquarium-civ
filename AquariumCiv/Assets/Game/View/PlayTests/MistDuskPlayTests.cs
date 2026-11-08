using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#379: while the mister sprays (dusk especially) the whole land must not get lighter. Compares the screen's
    /// luminance, over the pixels that hold still, with no spray and with the finger down far from the camera's focus.</summary>
    [Category("MistDusk")]
    public class MistDuskPlayTests : PlayTestBase
    {
        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static IEnumerator Grab(System.Action<Texture2D> got)
        {
            yield return new WaitForEndOfFrame();
            got(ScreenCapture.CaptureScreenshotAsTexture());
        }

        /// <summary>Mean luminance of the lower 60 % of the screen (the land) in a, and in b over the same pixels, skipping pixels that
        /// changed between two grabs of the same state (ref1, ref2).</summary>
        static float ChangedRatio; static int ChangedPx;
        static void Ratio(Texture2D baseline, Texture2D baseline2, Texture2D other, float[] r)
        {
            var a = baseline.GetPixels(); var a2 = baseline2.GetPixels(); var b = other.GetPixels();
            int w = baseline.width, h = baseline.height;
            double la = 0, lb = 0; int n = 0; double ca = 0, cb = 0; int cn = 0;
            for (int y = 0; y < (int)(h * 0.6f); y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (Mathf.Abs(a[i].r - a2[i].r) + Mathf.Abs(a[i].g - a2[i].g) + Mathf.Abs(a[i].b - a2[i].b) > 1.5f / 255f) continue;
                    la += Lum(a[i]); lb += Lum(b[i]); n++;
                    if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 3f / 255f) { ca += Lum(a[i]); cb += Lum(b[i]); cn++; }
                }
            ChangedRatio = cn > 0 ? (float)(cb / ca) : 1f; ChangedPx = cn; r[0] = n > 0 ? (float)(lb / Mathf.Max(1e-5f, (float)la)) : 1f; r[1] = n; r[2] = n > 0 ? (float)(la / n) : 0f;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Probe_AoOffLightensLand([Values(20f)] float hour)
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            EcoDebug.Table(0.3f); EcoDebug.Humidity(0.9f); EcoDebug.MistBigCamp();
            for (int i = 0; i < 30; i++) EcoDebug.SkipSeasons(1);
            yield return Frames(30);
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = Object.FindAnyObjectByType<BoxView>();
            if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = true;
            PeopleView.DebugSetHour(hour); PeopleView.DebugSpeed(0f);
            yield return new WaitForSecondsRealtime(1.5f);
            rig.enabled = false;
            yield return Frames(10);
            Texture2D a = null, a2 = null, b = null;
            yield return Grab(t => a = t); yield return Frames(3); yield return Grab(t => a2 = t);
            yield return Shot("mistdusk_probe_ao_on");
            box.bakeAO = false; box.Refresh(); yield return Frames(30);
            yield return Grab(t => b = t);
            yield return Shot("mistdusk_probe_ao_off");
            var r = new float[3]; Ratio(a, a2, b, r);
            Debug.Log($"[mistdusk] PROBE changed {ChangedPx} px ratio {ChangedRatio:0.000}"); Debug.Log($"[mistdusk] PROBE AO off/on land lum ratio {r[0]:0.000} over {r[1]:0} px");
            box.bakeAO = true; box.Refresh();
            PeopleView.DebugSpeed(1f); if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = false; rig.enabled = true;
        }

        static string Globals()
        {
            var sun = RenderSettings.sun;
            var vol = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
            string v = "";
            if (vol != null && vol.profile != null) foreach (var o in vol.profile.components) v += $"{o.GetType().Name}:{vol.weight:0.00}/{o.active} ";
            return $"mess {GameClock.Instance.ActiveSim.MessLevel:0.000} sun {(sun != null ? sun.intensity : -1f):0.000} amb {RenderSettings.ambientSkyColor} {RenderSettings.ambientEquatorColor} fog {RenderSettings.fogColor} ambBoost {Shader.GetGlobalFloat("_AC_AmbientBoost"):0.000} vol {v}";
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Spraying_NeverLightensTheLand([Values(12f, 20f, 21f)] float hour)
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            Assert.IsNotNull(EcoMistView.Instance);
            Assert.IsNotNull(EcoSurface.Instance);
            EcoDebug.Table(0.3f); EcoDebug.Humidity(0.9f); EcoDebug.MistBigCamp();
            yield return Frames(30);
            var hud = GameHud.Instance;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            hud?.SetHand(GameHud.KitTool.Mister);
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = Object.FindAnyObjectByType<BoxView>();
            var st = GameClock.Instance.ActiveSim.Settlement;
            float x = st.Map.greenX + 0.5f, z = st.Map.greenZ;
            try
            {
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = true;
                PeopleView.DebugSetHour(hour);
                PeopleView.DebugSpeed(0f);
                yield return new WaitForSecondsRealtime(1.5f);
                rig.enabled = false;
                yield return Frames(10);
                Texture2D off1 = null, off2 = null, during = null, during2 = null, after = null;
                yield return Grab(t => off1 = t);
                Debug.Log($"[mistdusk] hour {hour:0} OFF {Globals()}");
                yield return Frames(3);
                yield return Grab(t => off2 = t);
                yield return Shot($"mistdusk_{hour:00}h_off");

                var w = box.transform.TransformPoint(box.land.OnGround(x, z));
                var sp = Camera.main.WorldToScreenPoint(w);
                var at = new Vector2(sp.x, sp.y);
                int id = touchId++;
                BeginTouch(id, at, queueEventOnly: true);
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < 1.4f) { MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
                Assert.IsTrue(EcoMistView.Instance.Misting, "held");
                yield return Grab(t => during = t);
                Debug.Log($"[mistdusk] hour {hour:0} DURING {Globals()}");
                yield return Shot($"mistdusk_{hour:00}h_during");
                MoveTouch(id, at + new Vector2(2f, 0f), delta: Vector2.zero, queueEventOnly: true);
                yield return Frames(3);
                yield return Grab(t => during2 = t);
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                yield return new WaitForSecondsRealtime(4f);
                yield return Grab(t => after = t);
                yield return Shot($"mistdusk_{hour:00}h_after");

                var r = new float[3];
                Ratio(off1, off2, during, r);
                float rDuring = r[0]; float ChangedRatio0 = ChangedRatio;
                Debug.Log($"[mistdusk] hour {hour:0}: CHANGED px {ChangedPx} ratio {ChangedRatio:0.000}"); Debug.Log($"[mistdusk] hour {hour:0}: land lum off {r[2]:0.0000}, during/off = {rDuring:0.000} over {r[1]:0} px");
                Ratio(off1, off2, after, r);
                Debug.Log($"[mistdusk] hour {hour:0}: after/off = {r[0]:0.000}");
                Assert.LessOrEqual(rDuring, 1.01f, "spraying does not lighten the land");
                Assert.LessOrEqual(ChangedRatio0, 1.08f, "what the spray changes is not lighter than it was (the droplets are lit like the land)");
            }
            finally
            {
                PeopleView.DebugSpeed(1f);
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = false;
                if (rig != null) rig.enabled = true;
            }
        }

        /// <summary>#379 / note #396 (2026-11-07, iPhone, build 21 with the PR #391 fix): the phone runs the Mobile quality level (Mobile_RPAsset:
        /// no depth or opaque texture, render scale 0.8, one cascade, no SSAO) and the Editor measures ran on PC. The tester's two cases, camp at
        /// year 0 at night and a grown village at sundown, under each level. A probe: logs and shoots, asserts nothing.</summary>
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator Probe_MobileQuality([Values(false, true)] bool mobile, [Values(false, true)] bool village)
        {
            int was = QualitySettings.GetQualityLevel();
            int want = System.Array.IndexOf(QualitySettings.names, mobile ? "Mobile" : "PC");
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            if (want >= 0) QualitySettings.SetQualityLevel(want, true); // after the load: the scene load sets the level back
            var pipeWas = QualitySettings.renderPipeline;
#if UNITY_EDITOR
            // The level alone doesn't stick in the Editor's play mode (it read PC after the switch): set its pipeline as LightShots does.
            QualitySettings.renderPipeline = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(mobile ? "Assets/Settings/Mobile_RPAsset.asset" : "Assets/Settings/PC_RPAsset.asset");
#endif
            if (village) { SimLayoutSource.DebugSkipYears(39); for (int i = 0; i < 8; i++) EcoDebug.SkipSeasons(1); }
            yield return Frames(60);
            var hud = GameHud.Instance;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            hud?.SetHand(GameHud.KitTool.Mister);
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = Object.FindAnyObjectByType<BoxView>();
            var st = GameClock.Instance.ActiveSim.Settlement;
            float x = st.Map.greenX + 0.5f, z = st.Map.greenZ;
            float hour = village ? 19.5f : 21f;
            string tag = $"mistq_{(mobile ? "mobile" : "pc")}_{(village ? "village" : "camp")}";
            try
            {
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = true;
                PeopleView.DebugSetHour(hour);
                PeopleView.DebugSpeed(0f);
                yield return new WaitForSecondsRealtime(1.5f);
                rig.enabled = false;
                yield return Frames(10);
                Texture2D off1 = null, off2 = null, during = null, after = null;
                yield return Grab(t => off1 = t);
                string gOff = Globals();
                yield return Frames(3);
                yield return Grab(t => off2 = t);
                yield return Shot(tag + "_off");
                var w = box.transform.TransformPoint(box.land.OnGround(x, z));
                var sp = Camera.main.WorldToScreenPoint(w);
                var at = new Vector2(sp.x, sp.y);
                int id = touchId++;
                BeginTouch(id, at, queueEventOnly: true);
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < 1.4f) { MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
                bool held = EcoMistView.Instance != null && EcoMistView.Instance.Misting;
                yield return Grab(t => during = t);
                string gDuring = Globals();
                yield return Shot(tag + "_during");
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                yield return new WaitForSecondsRealtime(4f);
                yield return Grab(t => after = t);
                string gAfter = Globals();
                yield return Shot(tag + "_after");
                var r = new float[3];
                Ratio(off1, off2, during, r);
                float masked = r[0], changed = ChangedRatio; int changedPx = ChangedPx;
                Ratio(off1, off2, after, r);
                float lOff = MeanLum(off1), lDur = MeanLum(during), lAfter = MeanLum(after);
                Debug.Log($"[mistq] {tag} quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} rp {QualitySettings.renderPipeline?.name} held {held} · land mean off {lOff:0.0000} during {lDur / Mathf.Max(1e-5f, lOff):0.000} after {lAfter / Mathf.Max(1e-5f, lOff):0.000} · masked during/off {masked:0.000} after/off {r[0]:0.000} · changed px {changedPx} ratio {changed:0.000}");
                Debug.Log($"[mistq] {tag} OFF {gOff}");
                Debug.Log($"[mistq] {tag} DURING {gDuring}");
                Debug.Log($"[mistq] {tag} AFTER {gAfter}");
            }
            finally
            {
                PeopleView.DebugSpeed(1f);
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = false;
                if (rig != null) rig.enabled = true;
                QualitySettings.renderPipeline = pipeWas;
                QualitySettings.SetQualityLevel(was, true);
            }
        }

        /// <summary>#396's frame: the camp at year 0 at 21 h, the camp-frame camera (zoom 0.44 in the tester's log), with the glass dry and
        /// fogged by a humid season (spraying raises the air's humidity; the droplets follow on the next season step). A probe.</summary>
        [UnityTest, Explicit, Timeout(600000)]
        public IEnumerator Probe_FoggedGlassAtNight([Values(0f, 0.9f)] float humidity)
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            if (humidity > 0f) EcoDebug.Humidity(humidity);
            Debug.Log("[mistq] " + EcoDebug.MistBigCamp()); // the first run's misting: a big damp patch round the camp
            yield return Frames(30);
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = Object.FindAnyObjectByType<BoxView>();
            var st = GameClock.Instance.ActiveSim.Settlement;
            try
            {
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = true;
                PeopleView.DebugSetHour(21f);
                PeopleView.DebugSpeed(0f);
                rig.target = new Vector3(st.Map.greenX, rig.target.y, st.Map.greenZ); rig.zoom = 0.44f; rig.Apply();
                yield return new WaitForSecondsRealtime(4f); // the glass eases to the droplets
                rig.enabled = false;
                yield return Frames(5);
                Texture2D t = null;
                yield return Grab(x => t = x);
                Debug.Log($"[mistq] fog humidity {humidity:0.0} droplets sim {GameClock.Instance.ActiveSim.EcoDroplets:0.000} glass {box.Droplets:0.000} land mean {MeanLum(t):0.0000} · {Globals()}");
                yield return Shot($"mistq_fog_{humidity:0.0}");
            }
            finally
            {
                PeopleView.DebugSpeed(1f);
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = false;
                if (rig != null) rig.enabled = true;
            }
        }

        /// <summary>#396: a phone-like stroke at the year-0 camp at 21 h (the mister in hand, 5 s dragged across the chunk border at z 0
        /// near the fire), then the finger up: is every chunk's AO back, or does a chunk stay light (hard straight edges)? A probe.</summary>
        [UnityTest, Explicit, Timeout(600000)]
        public IEnumerator Probe_StrokeAcrossChunks()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            yield return Frames(30);
            var hud = GameHud.Instance;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            hud?.SetHand(GameHud.KitTool.Mister);
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = Object.FindAnyObjectByType<BoxView>();
            var st = GameClock.Instance.ActiveSim.Settlement;
            try
            {
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = true;
                PeopleView.DebugSetHour(21f);
                PeopleView.DebugSpeed(0f);
                rig.target = new Vector3(st.Map.greenX, rig.target.y, st.Map.greenZ); rig.zoom = 0.44f; rig.Apply();
                yield return new WaitForSecondsRealtime(1f);
                rig.enabled = false;
                yield return Frames(5);
                yield return Shot("mistq_stroke_0before");
                Vector2 Scr(float x, float z) { var s = Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z))); return new Vector2(s.x, s.y); }
                Vector2 a = Scr(st.Map.greenX - 0.4f, -0.45f), b = Scr(st.Map.greenX + 0.4f, 0.45f);
                int id = touchId++;
                BeginTouch(id, a, queueEventOnly: true);
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < 5f)
                {
                    float u = Mathf.PingPong((Time.unscaledTime - t0) / 1.25f, 1f);
                    MoveTouch(id, Vector2.Lerp(a, b, u), delta: Vector2.zero, queueEventOnly: true);
                    yield return null;
                }
                Debug.Log($"[mistq] stroke held {EcoMistView.Instance.Misting} · during: deferAO {box.deferAO} aoPending {box.AoPending} waiting {box.ChunksWaiting}");
                yield return Shot("mistq_stroke_1during");
                EndTouch(id, b, delta: Vector2.zero, queueEventOnly: true);
                yield return new WaitForSecondsRealtime(1f);
                Debug.Log($"[mistq] stroke +1 s: deferAO {box.deferAO} aoPending {box.AoPending} waiting {box.ChunksWaiting}");
                yield return Shot("mistq_stroke_2after1s");
                yield return new WaitForSecondsRealtime(4f);
                Debug.Log($"[mistq] stroke +5 s: deferAO {box.deferAO} aoPending {box.AoPending} waiting {box.ChunksWaiting}");
                yield return Shot("mistq_stroke_3after5s");
                box.bakeAO = false; box.Rebuild(); yield return Frames(5);
                yield return Shot("mistq_stroke_4aooff");
                box.bakeAO = true; box.Rebuild(); yield return Frames(5);
            }
            finally
            {
                PeopleView.DebugSpeed(1f);
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = false;
                if (rig != null) rig.enabled = true;
            }
        }

        static float MeanLum(Texture2D t)
        {
            var p = t.GetPixels(); int w = t.width, h = t.height; double s = 0; int n = 0;
            for (int y = 0; y < (int)(h * 0.6f); y++) for (int x = 0; x < w; x++) { s += Lum(p[y * w + x]); n++; }
            return n > 0 ? (float)(s / n) : 0f;
        }

        /// <summary>#379 D5 re-measure: the tester's case (a grown village, years in, at sundown) with the camera orbit off (masked
        /// ratio, the #300 method) and on (whole-screen land mean, as a player sees it). A probe: logs, asserts nothing.</summary>
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator Probe_GrownVillageSundown([Values(false, true)] bool orbit)
        {
            UseLivingLand();
            yield return OpenSlice(seed: 4242);
            Debug.Log("[mistdusk-d5] " + SimLayoutSource.DebugSkipYears(39));
            for (int i = 0; i < 8; i++) EcoDebug.SkipSeasons(1);
            yield return Frames(60);
            var hud = GameHud.Instance;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            hud?.SetHand(GameHud.KitTool.Mister);
            var rig = Camera.main.GetComponent<BoxCamera>();
            var box = Object.FindAnyObjectByType<BoxView>();
            var st = GameClock.Instance.ActiveSim.Settlement;
            float x = st.Map.greenX + 0.5f, z = st.Map.greenZ;
            const float hour = 19.5f;
            try
            {
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = true;
                PeopleView.DebugSetHour(hour);
                PeopleView.DebugSpeed(0f);
                yield return new WaitForSecondsRealtime(1.5f);
                rig.enabled = orbit;
                yield return Frames(10);
                Texture2D off1 = null, off2 = null, during = null, after = null;
                yield return Grab(t => off1 = t);
                string gOff = Globals();
                yield return Frames(3);
                yield return Grab(t => off2 = t);
                yield return Shot($"mistdusk_d5_{(orbit ? "orbit" : "still")}_off");
                var w = box.transform.TransformPoint(box.land.OnGround(x, z));
                var sp = Camera.main.WorldToScreenPoint(w);
                var at = new Vector2(sp.x, sp.y);
                int id = touchId++;
                BeginTouch(id, at, queueEventOnly: true);
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < 1.4f) { MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
                bool held = EcoMistView.Instance != null && EcoMistView.Instance.Misting;
                yield return Grab(t => during = t);
                string gDuring = Globals();
                yield return Shot($"mistdusk_d5_{(orbit ? "orbit" : "still")}_during");
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                yield return new WaitForSecondsRealtime(4f);
                yield return Grab(t => after = t);
                yield return Shot($"mistdusk_d5_{(orbit ? "orbit" : "still")}_after");
                var r = new float[3];
                Ratio(off1, off2, during, r);
                float masked = r[0], changed = ChangedRatio; int changedPx = ChangedPx;
                Ratio(off1, off2, after, r);
                float lOff = MeanLum(off1), lDur = MeanLum(during), lAfter = MeanLum(after);
                var s = GameClock.Instance.ActiveSim;
                Debug.Log($"[mistdusk-d5] orbit {orbit} year {s.State.Year:0.0} era {s.State.era} pop {s.State.population} held {held} · whole land mean off {lOff:0.0000} during {lDur:0.0000} ({lDur / Mathf.Max(1e-5f, lOff):0.000}) after {lAfter:0.0000} ({lAfter / Mathf.Max(1e-5f, lOff):0.000}) · masked during/off {masked:0.000}, after/off {r[0]:0.000} · changed px {changedPx} ratio {changed:0.000}");
                Debug.Log($"[mistdusk-d5] OFF {gOff}");
                Debug.Log($"[mistdusk-d5] DURING {gDuring}");
            }
            finally
            {
                PeopleView.DebugSpeed(1f);
                if (DayLightDriver.Instance != null) DayLightDriver.Instance.HoldFlicker = false;
                if (rig != null) rig.enabled = true;
            }
        }
    }
}
