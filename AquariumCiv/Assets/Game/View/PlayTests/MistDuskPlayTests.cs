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
