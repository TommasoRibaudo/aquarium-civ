using System.Collections.Generic;
using NUnit.Framework;
using AquariumCiv.Lighting;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The camera's pure maths (#63): drag speed per screen fraction, the slop, soft limits, inertia, pan bounds by zoom.</summary>
    public class BoxCameraTests
    {
        [Test]
        public void PinTagOpacity_FullClose_GoneZoomedOut_SmoothBetween()
        {
            Assert.AreEqual(1f, BoxCameraMath.PinTagOpacity(0.3f), 1e-6f);
            Assert.AreEqual(1f, BoxCameraMath.PinTagOpacity(BoxCameraMath.PinTagFadeStart), 1e-6f);
            Assert.AreEqual(0f, BoxCameraMath.PinTagOpacity(BoxCameraMath.PinTagFadeEnd), 1e-6f);
            Assert.AreEqual(0f, BoxCameraMath.PinTagOpacity(1.3f), 1e-6f);
            float prev = 1f;
            for (float z = 0.5f; z <= 1.3f; z += 0.002f)
            {
                float o = BoxCameraMath.PinTagOpacity(z);
                Assert.LessOrEqual(o, prev + 1e-6f, "zooming out never brings it back, at " + z);
                Assert.Less(prev - o, 0.03f, "no pop, at " + z);
                prev = o;
            }
        }

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
            Assert.AreEqual(14f, BoxCameraMath.PitchFloor(AquariumCiv.Lighting.CameraReach.MinZoom), 1e-4f);
            Assert.AreEqual(19f, BoxCameraMath.PitchFloor(0.6f), 1e-3f);
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
        public void PanBounds_Glass_ReachTheGlassAtEveryZoom_NeverPastIt()
        {
            // #273: the pivot rests up to the glass's inner face, close or far, and the hard stop stays inside it.
            for (float z = AquariumCiv.Lighting.CameraReach.MinZoom; z <= 1.3f; z += 0.02f)
            {
                var b = BoxCameraMath.PanBounds(z);
                Assert.Greater(b.x, Land.HX - 0.3f, "x reaches the glass at zoom " + z);
                Assert.Greater(b.y, Land.HZ - 0.3f, "z reaches the glass at zoom " + z);
                Assert.Less(b.x, BoxCameraMath.PanHardStop.x); Assert.Less(b.y, BoxCameraMath.PanHardStop.y);
            }
            Assert.Less(BoxCameraMath.PanHardStop.x, Land.HX, "the soft band ends inside the glass: this is what BoxCamera clamps to");
            Assert.Less(BoxCameraMath.PanHardStop.y, Land.HZ);
        }

        [Test]
        public void PanBoundsFor_DrawsInOnlyWhereTheCameraWouldEnterTheBox()
        {
            // #538: the view no longer limits the pan: at close, mid and far zoom (the camp's default pose) the pivot reaches each side wall's inner face.
            var open = BoxCameraMath.PanBounds(1f);
            foreach (float dist in new[] { 8.2f, 20f, 41f, 53f })
            {
                var b = BoxCameraMath.PanBoundsFor(0f, 26f, dist, 1f);
                Assert.AreEqual(open.x, b.x, 1e-4f, "x reaches the glass at distance " + dist);
                Assert.AreEqual(open.y, b.y, 1e-4f, "z reaches the glass at distance " + dist);
                Assert.Greater(b.x, Land.HX - 0.3f);
                Assert.Less(b.x, BoxCameraMath.PanHardStop.x);
            }
            var far = BoxCameraMath.PanBoundsFor(28f, 34f, 41f, 0f);
            Assert.Greater(far.x, Land.HX - 0.3f, "far out, free pan (#538; was 0.4 under #273's view limit)");
            var corner = BoxCameraMath.PanBoundsFor(55f, 14f, 8.2f, 0f);
            Assert.LessOrEqual(corner.x, open.x + 1e-4f, "the camera low and close at a corner still draws the range in");
            // the camera never enters the box's hull at the held end of the pan, any of those poses
            float hx = Land.HX + BoxCameraMath.HullSide, hz = Land.HZ + BoxCameraMath.HullSide;
            foreach (var pose in new[] { (28f, 34f, 41f), (55f, 14f, 8.2f), (0f, 24f, 8.2f), (-30f, 20f, 15f) })
            {
                var b = BoxCameraMath.PanBoundsFor(pose.Item1, pose.Item2, pose.Item3, 0f);
                var stop = BoxCameraMath.PanHardStop;
                CameraGlass.CameraOffset(pose.Item1, pose.Item2, pose.Item3, out float ox, out float oy, out float oz);
                foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                {
                    float px = sx * Mathf.Min(stop.x, b.x + CameraGlass.Gap), pz = sz * Mathf.Min(stop.y, b.y + CameraGlass.Gap);
                    Assert.IsFalse(CameraGlass.CameraInsideBox(px + ox, oy, pz + oz, hx, hz, BoxCameraMath.ShelfY, Land.GlassTop, 0.3f), "pose " + pose);
                }
            }
        }

        /// <summary>The user saw "only a piece of the tank": for every pose, the moments' included, nothing of the box or the shelf row on screen is in front of the near plane.</summary>
        [Test]
        public void Near_NeverCutsTheBoxOrTheShelf_AtAnyPose()
        {
            float tan = Mathf.Tan(11f * Mathf.Deg2Rad);
            foreach (float dist in new[] { 8f, 12f, 16.5f, 24f, 41f, 53f, 90f })
                foreach (float yaw in new[] { -55f, -30f, 0f, 28f, 55f })
                    foreach (float pitch in new[] { 5f, 14f, 24f, 34f, 50f, 68f })
                        foreach (var at in new[] { new Vector3(0f, 1f, 0f), new Vector3(2.9f, 1f, 2.1f), new Vector3(-2.9f, 1f, -2.1f), new Vector3(-7.6f, 1f, 0.6f), new Vector3(7.6f, 1f, 0f) })
                        {
                            var rot = Quaternion.Euler(pitch, yaw, 0f);
                            Vector3 fwd = rot * Vector3.forward, up = rot * Vector3.up, pos = at - fwd * dist;
                            float near = BoxCameraMath.FitNear(pos, fwd, up, tan, 3f);
                            Assert.GreaterOrEqual(near, BoxCameraMath.NearestPlane);
                            for (float x = -16f; x <= 16f; x += 0.8f)
                                for (float y = -0.3f; y <= 3f; y += 0.55f)
                                    for (float z = -4.2f; z <= 3.6f; z += 0.65f)
                                    {
                                        var p = new Vector3(x, y, z) - pos;
                                        float depth = Vector3.Dot(p, fwd);
                                        if (depth <= BoxCameraMath.NearestPlane) continue; // the lens itself among the boxes: no plane helps there
                                        float vy = Vector3.Dot(p, up) / depth;
                                        if (Mathf.Abs(vy) > tan) continue; // above or below the screen
                                        Assert.GreaterOrEqual(depth, near - 1e-3f, $"({x}, {y}, {z}) from dist {dist} yaw {yaw} pitch {pitch} focus {at}");
                                    }
                        }
        }

        /// <summary>#273: with the pivot at each wall (the hard stop, every corner too) the near plane never cuts a pane, at close, mid and far distance and the whole yaw / pitch range.</summary>
        [Test]
        public void Near_NeverCutsAPane_WithThePivotAtEachWall()
        {
            float tan = Mathf.Tan(11f * Mathf.Deg2Rad), hx = Land.HX + BoxCameraMath.HullSide, hz = Land.HZ + BoxCameraMath.HullSide;
            var stop = BoxCameraMath.PanHardStop;
            int checkedPoints = 0;
            foreach (float dist in new[] { 8.2f, 12f, 20f, 41f })
                foreach (float yaw in new[] { -30f, -10f, 0f, 28f, 55f })
                    foreach (float pitch in new[] { 14f, 24f, 34f, 60f })
                        foreach (var pivot in new[] { new Vector3(stop.x, 0f, 0f), new Vector3(-stop.x, 0f, 0f), new Vector3(0f, 0f, stop.y), new Vector3(0f, 0f, -stop.y),
                                                      new Vector3(stop.x, 0f, stop.y), new Vector3(-stop.x, 0f, -stop.y), new Vector3(stop.x, 0f, -stop.y), new Vector3(-stop.x, 0f, stop.y) })
                        {
                            // the camera's own range: the pivot is drawn in to where the pose allows
                            CameraGlass.PanBoundsFor(yaw, pitch, dist, 0f, Land.HX, Land.HZ, Land.HX + BoxCameraMath.HullSide, Land.HZ + BoxCameraMath.HullSide, BoxCameraMath.ShelfY, Land.GlassTop, out float bbx, out float bbz); // the glass-only range: the worst case
                            var b = new Vector2(bbx, bbz);
                            var at = new Vector3(Mathf.Clamp(pivot.x, -b.x, b.x), 0f, Mathf.Clamp(pivot.z, -b.y, b.y));
                            var rot = Quaternion.Euler(pitch, yaw, 0f);
                            Vector3 fwd = rot * Vector3.forward, up = rot * Vector3.up, pos = at - fwd * dist;
                            float near = BoxCameraMath.FitNear(pos, fwd, up, tan);
                            // every point of the four panes, sampled
                            for (float u = -1f; u <= 1.001f; u += 0.1f)
                                for (float y = BoxCameraMath.ShelfY; y <= Land.GlassTop + 0.1f; y += 0.4f)
                                    foreach (var w in new[] { new Vector3(u * hx, y, -hz), new Vector3(u * hx, y, hz), new Vector3(-hx, y, u * hz), new Vector3(hx, y, u * hz) })
                                    {
                                        var d = w - pos;
                                        float depth = Vector3.Dot(d, fwd);
                                        if (depth <= BoxCameraMath.NearestPlane) continue;
                                        if (Mathf.Abs(Vector3.Dot(d, up) / depth) > tan) continue;
                                        checkedPoints++;
                                        Assert.GreaterOrEqual(depth, near - 1e-3f, $"pane point {w} dist {dist} yaw {yaw} pitch {pitch} pivot {at}");
                                    }
                        }
            Assert.Greater(checkedPoints, 1000);
        }

        [Test]
        public void Near_StaysTightAroundTheBoxAtTheHomeView()
        {
            var rot = Quaternion.Euler(34f, 28f, 0f);
            Vector3 fwd = rot * Vector3.forward, pos = new Vector3(0f, 1f, 0f) - fwd * 41f;
            float near = BoxCameraMath.FitNear(pos, fwd, rot * Vector3.up, Mathf.Tan(11f * Mathf.Deg2Rad));
            Assert.Greater(near, 32f, "#193: the shadow map isn't spread over the air in front of the box");
        }

        [Test]
        public void Pan_RubberBand_PastTheBound()
        {
            float b = BoxCameraMath.PanBounds(0.3f).x, stop = BoxCameraMath.PanHardStop.x;
            Assert.AreEqual(1f, BoxCameraMath.GlassStep(0f, 1f, -b, b), 1e-5f, "inside the bound it follows the fingers");
            float over = BoxCameraMath.GlassStep(b, 0.05f, -b, b);
            Assert.Greater(over, b); Assert.Less(over, b + 0.05f, "past the bound it gives less than the fingers moved");
            float v = b;
            for (int i = 0; i < 200; i++) v = BoxCameraMath.GlassStep(v, 0.05f, -b, b);
            Assert.Less(v, stop, "a long drag never reaches the glass");
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

        /// <summary>#377: along a zoom sweep with the pitch at its floor (what a pinch does), the camera's height, pitch and distance change one way and in small steps.</summary>
        [Test]
        public void ZoomSweep_PitchHeightDistance_MonotoneAndJumpFree()
        {
            float fit = 20f; var target = new Vector3(0f, 1f, 0f);
            float prevP = -1f, prevH = -1f, prevD = -1f, maxP = 0f, maxH = 0f;
            for (float z = AquariumCiv.Lighting.CameraReach.MinZoom; z <= AquariumCiv.Lighting.CameraReach.MaxZoom + 1e-4f; z += 0.005f)
            {
                float pitch = BoxCameraMath.PitchFloor(z), d = fit * z;
                float h = target.y + Mathf.Sin(pitch * Mathf.Deg2Rad) * d;
                if (prevP >= 0f)
                {
                    Assert.GreaterOrEqual(pitch, prevP - 1e-4f, "pitch never falls as the zoom opens"); Assert.GreaterOrEqual(h, prevH - 1e-4f, "height never falls"); Assert.GreaterOrEqual(d, prevD);
                    maxP = Mathf.Max(maxP, pitch - prevP); maxH = Mathf.Max(maxH, h - prevH);
                }
                prevP = pitch; prevH = h; prevD = d;
            }
            Assert.Less(maxP, 0.1f, "no pitch jump per 0.005 zoom"); Assert.Less(maxH, 0.15f, "no height jump per 0.005 zoom");
        }
    }
}
