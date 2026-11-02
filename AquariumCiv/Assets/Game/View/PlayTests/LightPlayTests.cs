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
using UnityEngine.UIElements;

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

        [Category("Tester10")]
        [UnityTest, Timeout(120000)]
        public IEnumerator ACardHoldsTheViewAndTheCloseGlidesHome([Values("code", "the X", "box", "room")] string how)
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
            var cards = PersonCardView.Instance;
            if (how == "code") cards.Close();
            else if (how == "the X") yield return TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root.panel.visualTree, "person-card-close"));
            else
            {
                // Tester run 10: the press cancelled the glide before its lift closed the card, and the view stayed cropped for good.
                Assert.IsTrue(FreeSpot(how == "box", out var spot), "a clear spot on the " + how);
                yield return TapAt(spot);
                yield return Frames(2);
            }
            Assert.IsFalse(cards.AnyOpen, "closed by " + how);
            Assert.IsTrue(Cam.GlideArmed, "the close keeps the way home: " + how);
            yield return Until(() => !Cam.GlideArmed, GlideBack.Hold + GlideBack.Duration + 1f);
            Assert.AreEqual(zoom, Cam.zoom, 1e-3f, "closed: it glides back, " + how);

            // A real drag with the card up is the player taking the camera: no way home after that.
            Assert.IsTrue(cards.Open(who.notableId));
            yield return Until(() => !Cam.IsFlying, 2f);
            Assert.IsTrue(Cam.GlideArmed);
            Assert.IsTrue(FreeSpot(true, out var from), "a clear spot to drag from");
            yield return Drag(new[] { from, from + new Vector2(0f, Screen.height * 0.2f) }, 20f);
            Assert.IsFalse(Cam.GlideArmed, "a deliberate drag cancels it");
            float kept = Cam.zoom;
            cards.Close();
            yield return Wait(GlideBack.Hold + 0.5f);
            Assert.AreEqual(kept, Cam.zoom, 1e-3f, "and the view stays");
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("card glide " + how);
        }

        /// <summary>A screen point above the card, on the land or off it, with no doll, building or UI under it.</summary>
        static bool FreeSpot(bool onLand, out Vector2 spot)
        {
            var cards = PersonCardView.Instance;
            var cam = Camera.main;
            for (float y = 0.85f; y > 0.2f; y -= 0.03f)
                for (float x = 0.1f; x < 0.95f; x += 0.05f)
                {
                    spot = new Vector2(Screen.width * x, Screen.height * y);
                    if (UiInput.Over(spot) || !cards.PickDoll(spot).IsNone || !cards.PickPerson(spot).IsNone || cards.PickBuilding(spot) >= 0) continue;
                    if (!BoxCameraMath.GroundPoint(cam.ScreenPointToRay(spot), Land.G, out var g)) continue;
                    if ((Mathf.Abs(g.x) < Land.HX - 0.2f && Mathf.Abs(g.z) < Land.HZ - 0.2f) == onLand) return true;
                }
            spot = default;
            return false;
        }

        /// <summary>The user saw only a piece of the tank: at no pose the player can reach may the planes cut what's on screen.</summary>
        [Category("Tester10")]
        [UnityTest, Timeout(180000)]
        public IEnumerator NoPoseCutsTheBox()
        {
            yield return Open();
            using var watch = new DepthWatch();
            var rig = Cam;
            int poses = 0;
            foreach (float zoom in new[] { 0.2f, 0.3f, 0.45f, 0.6f, 0.8f, 1f, 1.3f })
                foreach (float yaw in new[] { rig.minYaw, 28f, rig.maxYaw })
                    foreach (float pitch in new[] { BoxCameraMath.PitchFloor(zoom), 34f, BoxCameraMath.PitchCeiling })
                    {
                        var b = BoxCameraMath.PanBounds(zoom);
                        foreach (var off in new[] { Vector2.zero, new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
                        {
                            rig.CancelFlight();
                            rig.zoom = zoom; rig.yaw = yaw; rig.pitch = pitch;
                            rig.target = new Vector3(b.x * off.x, Land.G, b.y * off.y);
                            rig.Apply();
                            poses++;
                            yield return null;
                        }
                    }
            Debug.Log($"[light] {poses} poses, {watch.Frames} frames seen, {watch.Faults} cut; closest: {watch.Worst}; first: {watch.First}");
            Assert.Greater(watch.Frames, poses / 2);
            Assert.AreEqual(0, watch.Faults, watch.First);

            // In motion: the nudges (a wish, a crumb), the glide back, a pinned overshoot by real fingers.
            rig.CancelFlight();
            rig.EaseTo(Vector3.zero, 28f, 34f, 1f, 0.01f);
            yield return Wait(0.4f);
            watch.Reset();
            Assert.IsTrue(rig.NudgeToward(new Vector3(2.6f, 1f, 1.8f), BoxCamera.NudgeZoom, 0.3f));
            yield return Until(() => !rig.IsFlying, 2f);
            yield return Until(() => !rig.GlideArmed, GlideBack.Hold + GlideBack.Duration + 1f);
            rig.GoHome(0.2f);
            yield return Wait(0.4f);
            Assert.Greater(watch.Frames, 10);
            Assert.AreEqual(0, watch.Faults, "nudge and glide: " + watch.First);
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("depth");
        }

        /// <summary>Tester run 10: at zoom 0.2 the view rested about 80 % on the wall, more in the rubber band.</summary>
        [Category("Tester10")]
        [UnityTest, Timeout(180000)]
        public IEnumerator PanStaysOnTheLand([Values(0.46f, 0.5625f, 0.75f)] float aspect)
        {
            yield return Open();
            var rig = Cam;
            var cam = Camera.main;
            cam.aspect = aspect;
            float worstShare = 1f, worstRest = 1f;
            foreach (float zoom in new[] { CameraReach.MinZoom, 0.3f, 0.5f })
                foreach (var dir in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
                {
                    rig.EaseTo(Vector3.zero, 28f, 34f, zoom, 0.01f);
                    yield return Wait(0.2f);
                    Vector2 a = new(Screen.width * 0.3f, Screen.height * 0.5f), b = new(Screen.width * 0.7f, Screen.height * 0.5f);
                    int ida = touchId++, idb = touchId++;
                    BeginTouch(ida, a, queueEventOnly: true);
                    BeginTouch(idb, b, queueEventOnly: true);
                    yield return Frames(2);
                    for (int i = 0; i < 40; i++)
                    {
                        // far past the land's edge: the drag runs well into the rubber band
                        var step = dir * Screen.height * 0.05f;
                        a += step; b += step;
                        MoveTouch(ida, a, delta: Vector2.zero, queueEventOnly: true);
                        MoveTouch(idb, b, delta: Vector2.zero, queueEventOnly: true);
                        yield return null;
                        Assert.IsTrue(CentreOnLand(cam), $"the screen's centre left the land at zoom {zoom}, frame {i}, target {rig.target}");
                        worstShare = Mathf.Min(worstShare, LandShare(cam));
                    }
                    EndTouch(ida, a, delta: Vector2.zero, queueEventOnly: true);
                    EndTouch(idb, b, delta: Vector2.zero, queueEventOnly: true);
                    yield return Wait(0.6f);
                    Assert.IsTrue(CentreOnLand(cam), $"at rest at zoom {zoom}: {rig.target}");
                    float share = LandShare(cam);
                    if (zoom <= CameraReach.MinZoom) worstRest = Mathf.Min(worstRest, share);
                    if (zoom <= CameraReach.MinZoom && dir == new Vector2(1f, 1f) && aspect == 0.5625f) yield return Shot("pan-closest-" + (int)(aspect * 100));
                }
            Debug.Log($"[light] aspect {aspect}: land share at zoom 0.2 at rest {worstRest:0.00}, in the band anywhere {worstShare:0.00}");
            Assert.Greater(worstRest, 0.35f, "enough of the land stays on screen at the closest zoom");
            cam.ResetAspect();
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("pan");
        }

        static bool CentreOnLand(Camera cam)
        {
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return BoxCameraMath.GroundPoint(ray, Land.G, out var g) && Mathf.Abs(g.x) <= Land.HX && Mathf.Abs(g.z) <= Land.HZ;
        }

        /// <summary>How much of the screen shows the land's top.</summary>
        static float LandShare(Camera cam)
        {
            int on = 0, n = 0;
            for (float y = 0.05f; y < 1f; y += 0.1f)
                for (float x = 0.05f; x < 1f; x += 0.1f)
                {
                    n++;
                    if (BoxCameraMath.GroundPoint(cam.ViewportPointToRay(new Vector3(x, y, 0f)), Land.G, out var g) && Mathf.Abs(g.x) <= Land.HX && Mathf.Abs(g.z) <= Land.HZ) on++;
                }
            return on / (float)n;
        }

        /// <summary>The moment end to end, with the depth watch on: this rendered blank with every counter green (tester run 10).</summary>
        [Category("Tester10")]
        [UnityTest, Timeout(120000)]
        public IEnumerator AMomentKeepsTheBoxInDepthAtEveryBeat()
        {
            yield return Open();
            using var watch = new DepthWatch();
            var mp = MomentPlayer.Instance;
            var sim = Sim;
            sim.State.grime = 0f;
            Assert.IsTrue(sim.ForceEraReady(), "the next era is ready");
            MomentPlayer.DebugWaited(100);
            yield return Wait(0.7f);
            var watchPill = mp.GetComponent<UIDocument>().rootVisualElement.Q<Button>("moment-watch");
            yield return TapAt(GameUiPanel.ScreenCenter(watchPill));
            Assert.IsTrue(mp.Playing, "a tap on Watch starts the moment");
            watch.Reset();
            MomentPlayer.DebugHurry(6f);
            MomentPlayer.DebugHoldHurry = true;
            yield return Until(() => !mp.Playing, 60f);
            MomentPlayer.DebugHoldHurry = false;
            Assert.IsFalse(mp.Playing, "the moment ends");
            Debug.Log($"[light] moment: {watch.Frames} frames, {watch.Faults} cut, {watch.OffScreen} off screen; closest {watch.Worst}");
            Assert.Greater(watch.Frames, 50);
            Assert.AreEqual(0, watch.Faults, watch.First);
            Assert.AreEqual(0, watch.OffScreen, "the box is on screen at every beat");
            Assert.IsNotNull(mp.StillTaken, "the still was taken");
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("moment depth");
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

        [Category("Tester10")]
        [UnityTest, Timeout(120000)]
        public IEnumerator AnEraStillTakesItsHourAndPutsTheLightBack()
        {
            yield return Open(22f);
            yield return Frames(4);
            var day = DayLightDriver.Instance;
            var sun = RenderSettings.sun;
            float inten = day.Current.SunIntensity, shown = day.Logic.ShownHour;
            var sky = RenderSettings.ambientSkyColor;
            day.BeginStill(DayLightDriver.StillHour);
            Assert.AreEqual(DayLight.Table(17.5f).SunPitch, day.Current.SunPitch, 1e-3f, "the golden hour");
            Assert.Greater(day.Current.SunIntensity, inten);
            Assert.AreEqual(day.Current.SunIntensity, sun.intensity, 0.02f, "the sun itself, though the care view owns it: the picture is rendered before it runs again");
            day.EndStill();
            Assert.AreEqual(inten, day.Current.SunIntensity, 1e-4f);
            Assert.AreEqual(inten, sun.intensity, 0.02f);
            Assert.AreEqual(shown, day.Logic.ShownHour, 1e-4f);
            Assert.AreEqual(sky, RenderSettings.ambientSkyColor);
            EraStills.DebugClear(Sim.State.seed);
            // As the game takes it (tester run 10): at the cheer the moment holds the camera, close in, with the rig off.
            Cam.enabled = false;
            Cam.target = new Vector3(0f, 1f, 0f); Cam.zoom = 0.4f; Cam.yaw = 28f; Cam.pitch = 34f;
            Cam.Apply();
            var info = EraStills.Take(Camera.main, Cam, Sim);
            Cam.enabled = true;
            Assert.IsNotNull(info, "the still was taken, and it isn't blank");
            Assert.AreEqual(shown, day.Logic.ShownHour, 1e-3f, "the still changed nothing");
            string png = EraStills.PngPath(info);
            Assert.IsTrue(File.Exists(png));
            Directory.CreateDirectory(Shots + "light-shots/"); // a fresh checkout has no Temp/agent/light-shots
            File.Copy(png, Shots + "light-shots/era-still.png", true);
            var still = new Texture2D(2, 2, TextureFormat.RGB24, false);
            Assert.IsTrue(still.LoadImage(File.ReadAllBytes(png)));
            Assert.IsFalse(EraStills.LooksBlank(still), "a real picture of the box, not the grey of a camera past it");
            var grey = new Texture2D(72, 128, TextureFormat.RGB24, false);
            for (int y = 0; y < grey.height; y++)
                for (int x = 0; x < grey.width; x++)
                {
                    float v = 0.65f - 0.2f * new Vector2(x / 72f - 0.5f, y / 128f - 0.5f).magnitude; // the failure's look: a soft vignette, nothing else
                    grey.SetPixel(x, y, new Color(v, v * 0.97f, v * 0.98f));
                }
            grey.Apply();
            Assert.IsTrue(EraStills.LooksBlank(grey), "and the failure's grey is caught");
            Object.Destroy(still); Object.Destroy(grey);
            EraStills.DebugClear(Sim.State.seed);
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("still");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WipingWorksAtCloseZoom()
        {
            yield return Open();
            Sim.State.grime = 0.7f;
            yield return Frames(6);
            Cam.EaseTo(Vector3.zero, 28f, 40f, 0.3f, 0.01f);
            yield return Wait(0.8f);
            var box = Object.FindAnyObjectByType<BoxView>();
            Assert.Less(box.PaneOpacity(0), 0.5f, "the near glass is fading at this zoom");
            GameHud.Instance.SetCleanMode(true);
            yield return Wait(0.8f);
            Assert.Greater(box.PaneOpacity(0), 0.9f, "clean mode brings it back");
            float grime = Sim.State.grime;
            var a = new Vector2(Screen.width * 0.3f, Screen.height * 0.5f);
            for (float fy = 0.35f; fy <= 0.7f && !CareView.Instance.GlassAt(a); fy += 0.05f) a = new Vector2(Screen.width * 0.3f, Screen.height * fy);
            Assume.That(CareView.Instance.GlassAt(a), "glass under the finger");
            yield return Drag(new[] { a, a + new Vector2(Screen.width * 0.4f, 0f), a + new Vector2(0f, 30f), a + new Vector2(Screen.width * 0.4f, 30f) }, 18f);
            yield return Frames(3);
            Debug.Log($"[light] wipe at zoom {Cam.zoom:0.00}: grime {grime:0.000} -> {Sim.State.grime:0.000}");
            Assert.Less(Sim.State.grime, grime - 0.002f, "it wiped");
            GameHud.Instance.SetCleanMode(false);
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("wipe close");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheTimeLapseHoldsTheFlicker()
        {
            yield return Open(22f);
            SimLayoutSource.DebugSkipYears(3f);
            Sim.CleanAll();
            yield return Frames(4);
            var day = DayLightDriver.Instance;
            Assert.IsFalse(day.FlickerHeld, "lamps flicker on a still clock");
            var tl = TimeLapseView.Ensure();
            Assume.That(tl.Open(), "a replay to open");
            yield return Frames(4);
            Assert.IsTrue(day.FlickerHeld, "steady under the time-lapse");
            Assert.Greater(RenderSettings.sun.intensity, 0.2f, "and the sun is the driver's with the care view off");
            tl.Close();
            yield return Frames(3);
            Assert.IsFalse(day.FlickerHeld);
            DayLightDriver.DebugPauseClock(false);
            AssertNoErrors("time-lapse");
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
