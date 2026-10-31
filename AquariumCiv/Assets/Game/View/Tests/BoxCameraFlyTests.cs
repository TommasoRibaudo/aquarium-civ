using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>Camera fly-to (#38): arrives, eases, is interrupted by touch or scroll, respects the pan bounds, and gates (the wipe) claim presses.</summary>
    public class BoxCameraFlyTests
    {
        GameObject go; BoxCamera cam;

        [SetUp] public void SetUp() { go = new GameObject("cam", typeof(Camera)); cam = go.AddComponent<BoxCamera>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(go); }

        static void Run(BoxCamera c, float seconds, int touches = 0)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) c.TickFlight(1f / 60f, touches);
        }

        [Test]
        public void Ease_StartsAtZero_EndsAtOne_Monotonic()
        {
            Assert.AreEqual(0f, BoxCameraMath.FlyEase(-1f)); Assert.AreEqual(1f, BoxCameraMath.FlyEase(2f));
            Assert.AreEqual(0.5f, BoxCameraMath.FlyEase(0.5f), 1e-5f);
            float prev = 0f;
            for (int i = 1; i <= 20; i++) { float e = BoxCameraMath.FlyEase(i / 20f); Assert.GreaterOrEqual(e, prev); prev = e; }
        }

        [Test]
        public void FlyTo_Arrives_AtPointAndZoom_AndReportsIt()
        {
            bool? arrived = null; cam.FlightEnded += a => arrived = a;
            cam.FlyTo(new Vector3(1.5f, 0f, -1f), 0.5f, 0.5f);
            Assert.IsTrue(cam.IsFlying);
            Run(cam, 0.25f);
            Assert.IsTrue(cam.IsFlying); Assert.Greater(cam.zoom, 0.5f); Assert.Less(cam.zoom, 1f, "half way");
            Run(cam, 0.5f);
            Assert.IsFalse(cam.IsFlying); Assert.IsTrue(arrived);
            Assert.AreEqual(0.5f, cam.zoom, 1e-4f);
            Assert.AreEqual(1.5f, cam.target.x, 1e-4f); Assert.AreEqual(-1f, cam.target.z, 1e-4f);
            Assert.AreEqual(1f, cam.target.y, "stays on the ground plane");
        }

        [Test]
        public void FlyTo_TouchOrScroll_Interrupts_AndStaysPut()
        {
            bool? arrived = null; cam.FlightEnded += a => arrived = a;
            cam.FlyTo(new Vector3(2f, 0f, 0f), 0.4f, 1f);
            Run(cam, 0.3f);
            var at = cam.target; float z = cam.zoom;
            cam.TickFlight(1f / 60f, 1);
            Assert.IsFalse(cam.IsFlying); Assert.IsFalse(arrived.Value);
            Run(cam, 1f);
            Assert.AreEqual(at, cam.target, "no further movement after the touch"); Assert.AreEqual(z, cam.zoom);

            cam.FlyTo(new Vector3(-2f, 0f, 0f), 0.4f, 1f);
            cam.TickFlight(1f / 60f, 0, 5f);
            Assert.IsFalse(cam.IsFlying, "a scroll interrupts too");
        }

        [Test]
        public void FlyTo_RespectsPanBounds_OfTheEndZoom()
        {
            cam.FlyTo(new Vector3(50f, 0f, -50f), 0.3f, 0.2f);
            Run(cam, 0.3f);
            var b = BoxCameraMath.PanBounds(0.3f);
            Assert.AreEqual(b.x, cam.target.x, 1e-4f); Assert.AreEqual(-b.y, cam.target.z, 1e-4f);
            // Zooming out to the full box leaves no room to pan: the flight ends centred.
            cam.FlyTo(new Vector3(2f, 0f, 1f), 1f, 0.2f);
            Run(cam, 0.3f);
            Assert.AreEqual(0f, cam.target.x, 1e-4f); Assert.AreEqual(0f, cam.target.z, 1e-4f);
            // Zoom is clamped to the camera's range.
            cam.FlyTo(Vector3.zero, 0.01f, 0.1f); Run(cam, 0.2f);
            Assert.AreEqual(cam.minZoom, cam.zoom, 1e-4f);
        }

        [Test]
        public void FlyTo_NewFlightReplaces_ZeroSecondsJumps()
        {
            int ended = 0; bool? last = null; cam.FlightEnded += a => { ended++; last = a; };
            cam.FlyTo(new Vector3(1f, 0f, 0f), 0.6f, 1f);
            Run(cam, 0.2f);
            cam.FlyTo(new Vector3(-1f, 0f, 0f), 0.6f, 0.4f);
            Assert.AreEqual(1, ended); Assert.IsFalse(last.Value, "the first flight was replaced");
            Run(cam, 0.6f);
            Assert.AreEqual(-1f, cam.target.x, 1e-4f);
            cam.FlyTo(new Vector3(0.5f, 0f, 0f), 0.6f, 0f);
            Assert.IsFalse(cam.IsFlying); Assert.AreEqual(0.5f, cam.target.x, 1e-4f);
        }

        sealed class Gate : ICameraGate { public bool claim; public bool ClaimsPress(Vector2 p) => claim; }

        [Test]
        public void Gates_AreRegisteredOnce_AndCanBeRemoved()
        {
            var g = new Gate { claim = true };
            cam.AddGate(g); cam.AddGate(g); cam.RemoveGate(g);
            Assert.IsFalse(cam.PressClaimed, "no press in edit mode");
            Assert.IsTrue(g.ClaimsPress(Vector2.zero));
        }
    }
}
