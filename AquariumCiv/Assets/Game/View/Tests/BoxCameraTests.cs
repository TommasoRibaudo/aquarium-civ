using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The camera's pure maths (#63): drag speed per screen fraction, the slop, soft limits, inertia, pan bounds by zoom.</summary>
    public class BoxCameraTests
    {
        [Test]
        public void Drag_SpeedIsAFractionOfScreenHeight_NotPixels()
        {
            Assert.AreEqual(110f, BoxCameraMath.YawDegrees(2556f, 2556f), 1e-3f, "a full-height drag is 110 degrees of yaw");
            Assert.AreEqual(50f, BoxCameraMath.PitchDegrees(1920f, 1920f), 1e-3f, "and 50 of pitch");
            // The same fraction of the screen turns the same on a small and a big phone.
            Assert.AreEqual(BoxCameraMath.YawDegrees(200f, 1000f), BoxCameraMath.YawDegrees(511.2f, 2556f), 1e-3f);
            Assert.AreEqual(BoxCameraMath.PitchDegrees(0.25f * 800f, 800f), BoxCameraMath.PitchDegrees(0.25f * 2556f, 2556f), 1e-3f);
        }

        [Test]
        public void Slop_IsAFractionOfScreenHeight()
        {
            Assert.IsFalse(BoxCameraMath.PastSlop(new Vector2(0.011f * 2556f, 0f), 2556f), "1.1% is still a tap");
            Assert.IsTrue(BoxCameraMath.PastSlop(new Vector2(0f, 0.013f * 2556f), 2556f), "1.3% starts an orbit");
            Assert.IsFalse(BoxCameraMath.PastSlop(new Vector2(0.008f * 1000f, 0.008f * 1000f), 1000f), "diagonal 0.8 + 0.8 = 1.13% is under");
            Assert.IsTrue(BoxCameraMath.PastSlop(new Vector2(0.01f * 1000f, 0.01f * 1000f), 1000f), "diagonal 1 + 1 = 1.41% is over");
        }

        [Test]
        public void PitchFloor_DependsOnZoom()
        {
            Assert.AreEqual(24f, BoxCameraMath.PitchFloor(1f), 1e-4f);
            Assert.AreEqual(24f, BoxCameraMath.PitchFloor(1.3f), 1e-4f, "zoomed out past the full box keeps the 24 degree floor");
            Assert.AreEqual(14f, BoxCameraMath.PitchFloor(0.3f), 1e-4f);
            Assert.AreEqual(19f, BoxCameraMath.PitchFloor(0.65f), 1e-3f);
            Assert.AreEqual(68f, BoxCameraMath.PitchCeiling);
        }

        [Test]
        public void SoftStep_FreeInside_SlowerPastALimit()
        {
            Assert.AreEqual(40f, BoxCameraMath.SoftStep(30f, 10f, 24f, 68f), 1e-4f);
            Assert.AreEqual(25f, BoxCameraMath.SoftStep(30f, -5f, 24f, 68f), 1e-4f);
            // 6 degrees past the limit a drag counts 1/(1 + 6/6) = 1/2; 12 past, 1/3.
            Assert.AreEqual(0.5f * 0.01f, BoxCameraMath.SoftStep(68f + 6f, 0.01f, 24f, 68f) - (68f + 6f), 1e-4f);
            Assert.AreEqual(0.5f * 0.01f, (24f - 6f) - BoxCameraMath.SoftStep(24f - 6f, -0.01f, 24f, 68f), 1e-4f);
            Assert.AreEqual(0.01f / 3f, BoxCameraMath.SoftStep(68f + 12f, 0.01f, 24f, 68f) - (68f + 12f), 1e-4f);
            // A soft wall, not a stop: it keeps giving, ever more slowly; and a big drag does not run away.
            float v = 68f;
            for (int i = 0; i < 100; i++) v = BoxCameraMath.SoftStep(v, 5f, 24f, 68f);
            Assert.Greater(v, 68f + 20f);
            Assert.Less(v, 68f + 80f, "500 degrees of drag past the limit moves it by about 70");
            // Moving back in is never slowed.
            Assert.AreEqual(70f, BoxCameraMath.SoftStep(80f, -10f, 24f, 68f), 1e-4f);
            // The result does not depend on the frame rate.
            float one = BoxCameraMath.SoftStep(68f, 12f, 24f, 68f), many = 68f;
            for (int i = 0; i < 120; i++) many = BoxCameraMath.SoftStep(many, 0.1f, 24f, 68f);
            Assert.AreEqual(one, many, 1e-3f);
        }

        [Test]
        public void SoftStep_CrossingTheLimitInOneMove_SplitsTheMove()
        {
            // 4 free degrees up to the limit, then the other 10 count at the soft rate (less than full).
            float r = BoxCameraMath.SoftStep(64f, 14f, 24f, 68f);
            Assert.Greater(r, 68f); Assert.Less(r, 78f);
            Assert.AreEqual(68f, BoxCameraMath.SoftStep(64f, 4f, 24f, 68f), 1e-4f, "exactly to the limit");
        }

        [Test]
        public void Spring_DecaysTheOvershootWithATimeConstant()
        {
            float v = BoxCameraMath.Spring(74f, 24f, 68f, BoxCameraMath.SpringTau);
            Assert.AreEqual(68f + 6f / Mathf.Exp(1f), v, 1e-3f, "after one time constant 1/e of the overshoot is left");
            Assert.AreEqual(50f, BoxCameraMath.Spring(50f, 24f, 68f, 0.5f), 1e-5f, "inside the limits it does nothing");
            Assert.AreEqual(24f, BoxCameraMath.Spring(24f, 24f, 68f, 0.1f), 1e-5f);
            Assert.AreEqual(24f - 5f / Mathf.Exp(2f), BoxCameraMath.Spring(19f, 24f, 68f, 2f * BoxCameraMath.SpringTau), 1e-3f);
        }

        [Test]
        public void Inertia_DecaysAndStops_FlingIsCapped()
        {
            var after = BoxCameraMath.InertiaStep(new Vector2(100f, 0f), BoxCameraMath.InertiaTau);
            Assert.AreEqual(100f / Mathf.Exp(1f), after.x, 1e-3f, "one time constant: 1/e");
            Assert.AreEqual(Vector2.zero, BoxCameraMath.InertiaStep(new Vector2(3.05f, 0f), 0.1f), "below 3 degrees per second it stops dead");
            Assert.AreNotEqual(Vector2.zero, BoxCameraMath.InertiaStep(new Vector2(5f, 0f), 0.01f));
            // Run it frame by frame: the total travel is about v * tau.
            float travel = 0f; var w = new Vector2(120f, 0f);
            for (int i = 0; i < 600 && w != Vector2.zero; i++) { travel += w.x / 60f; w = BoxCameraMath.InertiaStep(w, 1f / 60f); }
            Assert.AreEqual(120f * BoxCameraMath.InertiaTau, travel, 2f);
            Assert.AreEqual(200f, BoxCameraMath.CapFling(new Vector2(600f, 800f)).magnitude, 1e-3f);
            Assert.AreEqual(new Vector2(30f, 40f), BoxCameraMath.CapFling(new Vector2(30f, 40f)));
        }

        [Test]
        public void Velocity_IsTheMoveOverTheLast80Ms()
        {
            var s = new List<(float t, Vector2 d)> { (0.00f, new Vector2(100, 0)), (0.95f, new Vector2(1f, 0f)), (0.97f, new Vector2(1f, 1f)), (0.99f, new Vector2(2f, 0f)) };
            var v = BoxCameraMath.Velocity(s, 1.0f); // only the last three are inside [0.92, 1.0]
            Assert.AreEqual(4f / 0.08f, v.x, 1e-3f);
            Assert.AreEqual(1f / 0.08f, v.y, 1e-3f);
            Assert.AreEqual(Vector2.zero, BoxCameraMath.Velocity(s, 5f), "a finger that rested before release flings nothing");
        }

        [Test]
        public void PanBounds_ByZoom_FullBoxStaysCentred()
        {
            Assert.AreEqual(Vector2.zero, BoxCameraMath.PanBounds(1f), "no pan at the full box");
            Assert.AreEqual(Vector2.zero, BoxCameraMath.PanBounds(1.3f));
            var closest = BoxCameraMath.PanBounds(0.3f);
            Assert.AreEqual(Land.HX - 0.3f, closest.x, 1e-4f);
            Assert.AreEqual(Land.HZ - 0.3f, closest.y, 1e-4f);
            Assert.AreEqual(0.5f, BoxCameraMath.PanK(0.65f), 1e-4f);
            var mid = BoxCameraMath.PanBounds(0.65f);
            Assert.AreEqual((Land.HX - 0.3f) * 0.5f, mid.x, 1e-4f);
            Assert.Greater(BoxCameraMath.PanBounds(0.5f).x, BoxCameraMath.PanBounds(0.8f).x, "closer = more room to pan");
        }

        [Test]
        public void Pan_RubberBand_PastTheBound()
        {
            float b = BoxCameraMath.PanBounds(0.3f).x;
            Assert.AreEqual(1f, BoxCameraMath.SoftStep(0f, 1f, -b, b, BoxCameraMath.PanSoftRange), 1e-5f, "inside the bound it follows the fingers");
            float over = BoxCameraMath.SoftStep(b, 0.5f, -b, b, BoxCameraMath.PanSoftRange);
            Assert.Greater(over, b); Assert.Less(over, b + 0.5f, "past the bound it gives less than the fingers moved");
            // At the full box (bound 0) a long pan only stretches the band a little.
            float v = 0f;
            for (int i = 0; i < 60; i++) v = BoxCameraMath.SoftStep(v, 0.05f, 0f, 0f, BoxCameraMath.PanSoftRange);
            Assert.Less(v, 1.2f, "3 units of drag move it by about one");
            Assert.Greater(v, 0f);
        }

        [Test]
        public void ZoomTarget_MovesTowardsTheGroundPointByTheRatio()
        {
            var t = new Vector3(0, 1, 0); var g = new Vector3(2, 1, -1);
            Assert.AreEqual(t, BoxCameraMath.ZoomTarget(t, g, 1f, 1f));
            Assert.AreEqual(new Vector3(1, 1, -0.5f), BoxCameraMath.ZoomTarget(t, g, 1f, 0.5f), "halving the zoom moves half way");
            Assert.AreEqual(new Vector3(-2, 1, 1), BoxCameraMath.ZoomTarget(t, g, 0.5f, 1f), "zooming out moves away from the point by the same ratio");
        }

        [Test]
        public void GroundPoint_RayMeetsThePlane()
        {
            Assert.IsTrue(BoxCameraMath.GroundPoint(new Ray(new Vector3(1, 5, 2), new Vector3(0, -1, 0)), 1f, out var p));
            Assert.AreEqual(new Vector3(1, 1, 2), p);
            Assert.IsTrue(BoxCameraMath.GroundPoint(new Ray(new Vector3(0, 3, 0), new Vector3(1, -1, 0).normalized), 1f, out p));
            Assert.AreEqual(2f, p.x, 1e-4f);
            Assert.IsFalse(BoxCameraMath.GroundPoint(new Ray(Vector3.up * 3f, Vector3.up), 1f, out _), "pointing away");
            Assert.IsFalse(BoxCameraMath.GroundPoint(new Ray(Vector3.up * 3f, Vector3.right), 1f, out _), "parallel");
        }
    }
}
