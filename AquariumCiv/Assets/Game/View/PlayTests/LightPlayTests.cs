using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Lighting;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#113, #146, #162: the day's light, the mess dimming on top of it, the near glass and the glide back, in the real scene.</summary>
    [Category("LightPlay")]
    public class LightPlayTests : PlayTestBase
    {
        const int W = 540, H = 960;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        static SimCore Sim => GameClock.Instance.ActiveSim;

        IEnumerator Open(float hour = 12f)
        {
            yield return OpenSlice(W, H, 1234);
            Sim.CleanAll();
            PeopleView.DebugSetHour(hour);
            DayLightDriver.DebugPauseClock(true);
            yield return Frames(4);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            Assert.IsNotNull(DayLightDriver.Instance, "the bootstrap made the driver");
        }

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        /// <summary>Mean luminance of the frame, in the middle of the screen: the box, not the HUD.</summary>
        static IEnumerator Luma(System.Action<float> got)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            int x0 = tex.width / 8, x1 = tex.width * 7 / 8, y0 = tex.height / 4, y1 = tex.height * 3 / 4;
            double sum = 0; int n = 0;
            for (int y = y0; y < y1; y += 5)
                for (int x = x0; x < x1; x += 5) { var c = tex.GetPixel(x, y); sum += 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b; n++; }
            Object.Destroy(tex);
            got((float)(sum / n));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheDayDrivesTheLightAndTodayGivesItBack()
        {
            yield return Open(12f);
            var day = DayLightDriver.Instance;
            var sun = RenderSettings.sun;
            float dayRot = sun.transform.eulerAngles.y;
            var sky = RenderSettings.ambientSkyColor;
            float luma12 = 0f;
            yield return Luma(v => luma12 = v);
            Assert.AreEqual(0f, NightLamps.Level, 1e-3f, "no lamps at noon");

            PeopleView.DebugSetHour(22f);
            yield return Frames(3);
            Assert.AreEqual(1f, NightLamps.Level, 1e-3f, "the hour drives the lamps");
            Assert.Less(sun.intensity, 0.4f, "the sun is the moon now (the care view composes on the day's value)");
            Assert.Less(RenderSettings.ambientSkyColor.grayscale, sky.grayscale, "the ambient follows");
            Assert.Greater(sun.shadowStrength, 0.4f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(dayRot, sun.transform.eulerAngles.y)), 1f, "from another side");

            float luma22 = 0f;
            yield return Luma(v => luma22 = v);
            Debug.Log($"[light] frame luma 12 h {luma12:0.000}, 22 h {luma22:0.000}, ratio {luma22 / luma12:0.00}");
            Assert.Less(luma22, luma12 * 0.8f, "night is darker on screen: the ambient reached the shaders");
            Assert.Greater(luma22, luma12 * 0.3f, "and never pitch black (research 13 section 5)");

            var level = Object.FindAnyObjectByType<NightLampDriver>();
            if (level != null) { level.enabled = false; level.enabled = true; yield return null; Assert.AreEqual(1f, NightLamps.Level, 1e-3f, "the inspector value stands back"); }

            DayLightDriver.DebugLook(LightLook.Today);
            yield return Frames(3);
            Assert.IsFalse(DayLightDriver.Driving);
            Assert.AreEqual(1.3f, sun.intensity, 0.05f, "the scene's sun");
            Assert.AreEqual(sky, RenderSettings.ambientSkyColor, "the scene's ambient");
            Assert.AreEqual(0f, NightLamps.Level, 1e-3f);
            DayLightDriver.DebugLook(LightLook.SoftBlueDusk);
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("the day");
        }

        /// <summary>Tester's rule for #113: the mess must still show, by day and at night. The panes are off for the measure: this is the light, not the grime.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator TheMessDimsTheBoxAtNoonAndAtNight([Values(12f, 22f)] float hour)
        {
            yield return Open(hour);
            var box = Object.FindAnyObjectByType<BoxView>();
            var panes = box.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Glass")).ToArray();
            foreach (var p in panes) p.enabled = false;
            yield return Wait(0.3f);
            float clean = 0f, messy = 0f;
            yield return Luma(v => clean = v);
            Sim.State.grime = 1f;
            var care = CareView.Instance;
            yield return Until(() => care.SunFactor < 0.85f, 8f);
            Assert.Less(care.SunFactor, 0.9f, "the sun has dimmed");
            Assert.Less(DayLightDriver.Instance.MessAmbient, 0.97f, "and so has the ambient");
            yield return Wait(0.3f);
            yield return Luma(v => messy = v);
            Debug.Log($"[light] mess at {hour} h: clean {clean:0.000}, messy {messy:0.000}, sun x{care.SunFactor:0.00}, ambient x{DayLightDriver.Instance.MessAmbient:0.00}, drop {(1f - messy / clean) * 100f:0.0} %");
            Assert.Less(messy, clean * 0.985f, "a messy box is visibly gloomier");
            foreach (var p in panes) p.enabled = true;
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("mess " + hour);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ANudgeGlidesBackAndATouchStopsIt()
        {
            yield return Open();
            Cam.EaseTo(Vector3.zero, 28f, 34f, 1f, 0.01f);
            yield return Wait(0.4f);
            var home = Cam.target; float zoom = Cam.zoom, pitch = Cam.pitch;
            Assert.IsTrue(Cam.NudgeToward(new Vector3(2.6f, 1f, 1.8f), BoxCamera.NudgeZoom, 0.3f), "the nudge moves the camera");
            yield return Until(() => !Cam.IsFlying, 2f);
            Assert.Less(Cam.zoom, zoom - 0.1f, "it went in");
            Assert.IsTrue(Cam.GlideArmed);
            yield return Wait(GlideBack.Hold * 0.5f);
            Assert.IsFalse(Cam.Gliding, "it waits");
            yield return Until(() => !Cam.GlideArmed, GlideBack.Hold + GlideBack.Duration + 1f);
            Assert.IsFalse(Cam.GlideArmed, "it's back");
            Assert.AreEqual(zoom, Cam.zoom, 1e-3f);
            Assert.AreEqual(0f, (Cam.target - home).magnitude, 1e-3f);
            Assert.AreEqual(pitch, Cam.pitch, 0.1f, "a nudge's pitch comes back too");

            // A touch keeps the camera where it is.
            Assert.IsTrue(Cam.NudgeToward(new Vector3(2.6f, 1f, 1.8f), BoxCamera.NudgeZoom, 0.2f));
            yield return Until(() => !Cam.IsFlying, 2f);
            var there = new Vector2(Screen.width * 0.9f, Screen.height * 0.45f);
            BeginTouch(touchId, there, queueEventOnly: true);
            yield return Frames(3);
            Assert.IsFalse(Cam.GlideArmed, "a touch forgets the return");
            EndTouch(touchId, there, delta: Vector2.zero, queueEventOnly: true);
            touchId++;
            yield return Wait(0.2f);
            float z = Cam.zoom;
            yield return Wait(GlideBack.Hold + 0.5f);
            Assert.AreEqual(z, Cam.zoom, 1e-3f, "and it stays");
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("glide");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ACardHoldsTheViewAndTheCloseGlidesHome()
        {
            yield return Open();
            SimLayoutSource.DebugSkipYears(12f);
            Sim.CleanAll();
            yield return Frames(4);
            Cam.EaseTo(Vector3.zero, 28f, 34f, 1f, 0.01f);
            yield return Wait(0.4f);
            float zoom = Cam.zoom;
            var pv = PeopleView.Find();
            var who = pv.Activities.Where(a => a.who.IsNotable && pv.TryGetDollPosition(a.who, out _)).Select(a => a.who).FirstOrDefault();
            Assume.That(who.IsNotable, "a notable has a doll");
            Assert.IsTrue(PersonCardView.Instance.Open(who.notableId));
            yield return Until(() => !Cam.IsFlying, 2f);
            Assert.Less(Cam.zoom, zoom - 0.1f, "the card flew in");
            yield return Wait(GlideBack.Hold + 0.8f);
            Assert.Less(Cam.zoom, zoom - 0.1f, "still there while the card is up");
            PersonCardView.Instance.Close();
            yield return Until(() => !Cam.GlideArmed, GlideBack.Hold + GlideBack.Duration + 1f);
            Assert.AreEqual(zoom, Cam.zoom, 1e-3f, "closed: it glides back");
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("card glide");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NearGlassFadesWithoutAPopAndCleanModeBringsItBack()
        {
            yield return Open();
            var box = Object.FindAnyObjectByType<BoxView>();
            Cam.EaseTo(Vector3.zero, 28f, 34f, 1f, 0.01f);
            yield return Wait(0.5f);
            for (int i = 0; i < 4; i++) Assert.AreEqual(1f, box.PaneOpacity(i), 1e-3f, "all there at the full box");

            // Zooming in frame by frame: no step in what a pane shows.
            float prev = box.PaneOpacity(0), biggest = 0f;
            for (float z = 1f; z > 0.25f; z -= 0.01f)
            {
                Cam.zoom = z; Cam.Apply();
                yield return null;
                float now = box.PaneOpacity(0);
                biggest = Mathf.Max(biggest, Mathf.Abs(now - prev));
                prev = now;
            }
            yield return Wait(0.8f);
            Debug.Log($"[light] near glass: biggest step a frame {biggest:0.000}; front {box.PaneOpacity(0):0.00}, back {box.PaneOpacity(1):0.00}, left {box.PaneOpacity(2):0.00}, right {box.PaneOpacity(3):0.00}; near post {box.FrameOpacity(4):0.00}, far post {box.FrameOpacity(6):0.00}");
            Assert.Less(biggest, 0.12f, "no pop");
            Assert.Less(box.PaneOpacity(0), 0.2f, "the front pane is gone");
            Assert.AreEqual(1f, box.PaneOpacity(1), 1e-3f, "the back of the box stays");
            Assert.AreEqual(1f, box.PaneOpacity(3), 1e-3f, "and so does the far side");
            Assert.Less(box.FrameOpacity(4), 0.5f, "the near post fades");
            Assert.AreEqual(1f, box.FrameOpacity(6), 1e-3f, "the far post doesn't");

            GameHud.Instance.SetCleanMode(true);
            yield return Wait(1f);
            Assert.Greater(box.PaneOpacity(0), 0.97f, "clean mode brings it back: the grime is there to wipe");
            GameHud.Instance.SetCleanMode(false);
            yield return Wait(1f);
            Assert.Less(box.PaneOpacity(0), 0.2f);

            // Orbit: which panes fade follows, smoothly.
            Cam.yaw = -30f; Cam.Apply();
            yield return Wait(1f);
            Debug.Log($"[light] at yaw -30: front {box.PaneOpacity(0):0.00}, left {box.PaneOpacity(2):0.00}, right {box.PaneOpacity(3):0.00}");
            Assert.Less(box.PaneOpacity(3), 0.5f, "the right pane is the near one now");
            Assert.AreEqual(1f, box.PaneOpacity(2), 0.01f, "the left one came back");

            BoxView.NearGlassForce = 0f;
            yield return Wait(1f);
            Assert.AreEqual(1f, box.PaneOpacity(0), 0.02f, "the debug force overrides the zoom");
            BoxView.NearGlassForce = -1f;
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("near glass");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AnEraStillTakesItsHourAndPutsTheLightBack()
        {
            yield return Open(22f);
            var day = DayLightDriver.Instance;
            var sun = RenderSettings.sun;
            float inten = day.Current.SunIntensity, shown = day.Logic.ShownHour;
            var sky = RenderSettings.ambientSkyColor;
            day.BeginStill(DayLightDriver.StillHour);
            Assert.AreEqual(DayLight.Table(17.5f).SunPitch, day.Current.SunPitch, 1e-3f, "the golden hour");
            Assert.Greater(day.Current.SunIntensity, inten);
            day.EndStill();
            Assert.AreEqual(inten, day.Current.SunIntensity, 1e-4f);
            Assert.AreEqual(shown, day.Logic.ShownHour, 1e-4f);
            Assert.AreEqual(sky, RenderSettings.ambientSkyColor);
            EraStills.DebugClear(Sim.State.seed);
            var info = EraStills.Take(Camera.main, Cam, Sim);
            Assert.IsNotNull(info);
            Assert.AreEqual(shown, day.Logic.ShownHour, 1e-3f, "the still changed nothing");
            string png = EraStills.PngPath(info);
            Assert.IsTrue(File.Exists(png));
            File.Copy(png, Shots + "light-shots/era-still.png", true);
            EraStills.DebugClear(Sim.State.seed);
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("still");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheDriverAllocatesNothingAFrame()
        {
            yield return Open(22f);

            yield return Frames(10);
            long before = System.GC.GetTotalMemory(false);
            for (int i = 0; i < 2000; i++) DayLightDriver.Instance.Snap(19f + i * 0.001f);
            long grew = System.GC.GetTotalMemory(false) - before;
            Debug.Log($"[light] 2000 applies: {grew} bytes");
            Assert.Less(grew, 16 * 1024, "no allocation in the driver's frame (a few KB of noise from the runtime)");
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("alloc");
        }
    }
}
