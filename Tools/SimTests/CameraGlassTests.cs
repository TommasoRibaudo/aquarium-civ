// #273: the camera reaches the glass on every side and never goes past it. Pure floats (CameraGlass), so it runs in dotnet test;
// the Unity half (BoxCameraMath wrappers, near plane) is covered in BoxCameraTests and runs in phase B.
using System;
using AquariumCiv.Lighting;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    public class CameraGlassTests
    {
        const float HX = 3f, HZ = 2.2f, HullX = 3.3f, HullZ = 2.5f, Bottom = -0.3f, Top = 2.95f;

        static float Dist(float zoom, float baseDist = 41f) => baseDist * zoom;

        [Test]
        public void Pan_RestLimitReachesTheGlass_OnEverySide_AtEveryZoom_AndTheHardStopStaysInside()
        {
            CameraGlass.PanBounds(HX, HZ, out float bx, out float bz);
            CameraGlass.HardStop(HX, HZ, out float sx, out float sz);
            Assert.Greater(bx, HX - 0.25f); Assert.Greater(bz, HZ - 0.25f); // within a quarter unit of the inner face
            Assert.Less(sx, HX); Assert.Less(sz, HZ);
            Assert.Greater(sx, bx); Assert.Greater(sz, bz);
            // Left and right are symmetric: a drag to either side meets the same limit.
            Assert.AreEqual(CameraGlass.Step(bx, 5f, -bx, bx), -CameraGlass.Step(-bx, -5f, -bx, bx), 1e-5f);
            for (float zoom = 0.2f; zoom <= 1.3f; zoom += 0.05f)
                for (float yaw = -30f; yaw <= 55f; yaw += 5f)
                    for (float pitch = CameraReach.PitchFloor(zoom); pitch <= 68f; pitch += 6f)
                    {
                        CameraGlass.PanBoundsFor(yaw, pitch, Dist(zoom), 0f, HX, HZ, HullX, HullZ, Bottom, Top, out float px, out float pz);
                        Assert.LessOrEqual(px, bx + 1e-5f); Assert.LessOrEqual(pz, bz + 1e-5f);
                        Assert.GreaterOrEqual(px, 0f); Assert.GreaterOrEqual(pz, 0f);
                    }
        }

        [Test]
        public void Pan_FarAway_KeepsTheWholeRange()
        {
            CameraGlass.PanBounds(HX, HZ, out float bx, out float bz);
            // A camera 30+ units away, at any yaw, is outside the box wherever the pivot goes.
            for (float zoom = 0.8f; zoom <= 1.3f; zoom += 0.1f)
                for (float yaw = -30f; yaw <= 55f; yaw += 5f)
                {
                    CameraGlass.PanBoundsFor(yaw, 30f, Dist(zoom), 0f, HX, HZ, HullX, HullZ, Bottom, Top, out float px, out float pz);
                    Assert.AreEqual(bx, px, 1e-4f, $"zoom {zoom} yaw {yaw}"); Assert.AreEqual(bz, pz, 1e-4f);
                }
        }

        [Test]
        public void SoftStep_FollowsInside_IsContinuousAtTheLimit_AndNeverReachesTheGlass()
        {
            Assert.AreEqual(1.5f, CameraGlass.Step(0.5f, 1f, -2f, 2f), 1e-6f);
            // No jump at the limit: a tiny move past it is the same tiny move (full speed in, slows with depth).
            float eps = 1e-3f, hi = 2f;
            Assert.AreEqual(hi + eps, CameraGlass.Step(hi, eps, -2f, hi), 1e-5f);
            Assert.AreEqual(hi, CameraGlass.Step(hi - eps, eps, -2f, hi), 1e-6f, "arrives exactly at the limit");
            // Monotone, slower with depth, and bounded by the gap however long the drag.
            float v = hi, prevStep = float.MaxValue;
            for (int i = 0; i < 400; i++)
            {
                float next = CameraGlass.Step(v, 0.02f, -2f, hi);
                float step = next - v;
                Assert.GreaterOrEqual(step, 0f); Assert.LessOrEqual(step, prevStep + 1e-6f);
                Assert.Less(next, hi + CameraGlass.Gap, "never reaches the glass");
                prevStep = step; v = next;
            }
            Assert.Greater(v, hi + CameraGlass.Gap * 0.95f, "a long drag leans nearly all the way into the band");
            // The low end mirrors the high end.
            Assert.AreEqual(-hi - (v - hi), CameraGlass.Step(-hi, -8f, -hi, hi), 0.01f);
        }

        [Test]
        public void SoftStep_DoesNotDependOnTheFrameRate()
        {
            float one = CameraGlass.Step(1.9f, 0.6f, -2f, 2f);
            float many = 1.9f;
            for (int i = 0; i < 60; i++) many = CameraGlass.Step(many, 0.01f, -2f, 2f);
            Assert.AreEqual(one, many, 1e-4f);
            // Pulling back out is free: from inside the band a move towards the pan range is 1:1 until the limit.
            Assert.AreEqual(2f, CameraGlass.Step(2.05f, -0.05f, -2f, 2f), 1e-5f);
        }

        [Test]
        public void Camera_NeverEntersTheBox_AnyYawPitchZoomAndPivot_AtTheLimits()
        {
            foreach (float baseDist in new[] { 30f, 41f, 55f })           // other aspects change the fit distance
                for (float zoom = 0.2f; zoom <= 1.3f + 1e-4f; zoom += 0.025f)
                    for (float yaw = -30f; yaw <= 55f + 1e-4f; yaw += 2.5f)
                        for (float pitch = CameraReach.PitchFloor(zoom); pitch <= 68f + 1e-4f; pitch += 2f)
                            foreach (float pivotY in new[] { 0f, 0.6f, 1.5f })
                            {
                                float d = Dist(zoom, baseDist);
                                CameraGlass.PanBoundsFor(yaw, pitch, d, pivotY, HX, HZ, HullX, HullZ, Bottom, Top, out float bx, out float bz);
                                CameraGlass.CameraOffset(yaw, pitch, d, out float ox, out float oy, out float oz);
                                foreach (float sx in new[] { -1f, 0f, 1f })
                                    foreach (float sz in new[] { -1f, 0f, 1f })
                                        Assert.IsFalse(CameraGlass.CameraInsideBox(sx * bx + ox, pivotY + oy, sz * bz + oz, HullX, HullZ, Bottom, Top, 0.3f),
                                            $"inside at zoom {zoom} yaw {yaw} pitch {pitch} pivot ({sx * bx},{sz * bz}) y {pivotY}");
                            }
        }

        [Test]
        public void Camera_AtTheCentre_IsOutsideTheBox_AtEveryPose()
        {
            for (float zoom = 0.2f; zoom <= 1.3f; zoom += 0.05f)
                for (float yaw = -30f; yaw <= 55f; yaw += 5f)
                    for (float pitch = CameraReach.PitchFloor(zoom); pitch <= 68f; pitch += 4f)
                    {
                        CameraGlass.CameraOffset(yaw, pitch, Dist(zoom, 41f), out float ox, out float oy, out float oz);
                        Assert.IsFalse(CameraGlass.CameraInsideBox(ox, oy, oz, HullX, HullZ, Bottom, Top), $"zoom {zoom} yaw {yaw} pitch {pitch}");
                    }
        }

        [Test]
        public void PanScale_IsContinuous_InYawAndZoom()
        {
            // The pivot spring chases this each frame, so a jump would be a lurch.
            CameraGlass.PanBounds(HX, HZ, out float bx, out float bz);
            float prev = -1f;
            for (float yaw = -30f; yaw <= 55f; yaw += 0.1f)
            {
                float s = CameraGlass.PanScale(yaw, 14f, Dist(0.2f), 0f, bx, bz, HullX, HullZ, Bottom, Top);
                if (prev >= 0f) Assert.Less(Math.Abs(s - prev), 0.05f, "yaw " + yaw);
                prev = s;
            }
            prev = -1f;
            for (float zoom = 0.2f; zoom <= 1.3f; zoom += 0.002f)
            {
                float s = CameraGlass.PanScale(55f, 14f, Dist(zoom), 0f, bx, bz, HullX, HullZ, Bottom, Top);
                if (prev >= 0f) Assert.Less(Math.Abs(s - prev), 0.05f, "zoom " + zoom);
                prev = s;
            }
        }

        // ---- #273 (user 2026-11-04): the limit is on the view ----
        const float TanV = 0.19438f;

        static float FitDist(float aspect) => (float)Math.Sqrt((HX + 0.3f) * (HX + 0.3f) + 1.7f * 1.7f + (HZ + 0.3f) * (HZ + 0.3f)) / (TanV * Math.Min(1f, aspect));

        /// <summary>Screen position (NDC, -1..1 is on screen) of a world point, the camera orbiting the pivot like BoxCamera.</summary>
        static void Project(float yaw, float pitch, float dist, float aspect, float pvx, float pvy, float pvz, float x, float y, float z, out float nx, out float ny)
        {
            float yr = yaw * (float)Math.PI / 180f, pr = pitch * (float)Math.PI / 180f;
            float sy = (float)Math.Sin(yr), cy = (float)Math.Cos(yr), sp = (float)Math.Sin(pr), cp = (float)Math.Cos(pr);
            float cx = pvx - cp * sy * dist, cyy = pvy + sp * dist, cz = pvz - cp * cy * dist;
            float dx = x - cx, dy = y - cyy, dz = z - cz;
            float depth = dx * cp * sy + dy * -sp + dz * cp * cy;
            float right = dx * cy + dz * -sy, up = dx * sp * sy + dy * cp + dz * sp * cy;
            nx = right / (depth * TanV * aspect); ny = up / (depth * TanV);
        }

        /// <summary>#538: the view no longer limits the pan. At close, mid and far zoom the pivot rests at the glass's full range
        /// (as far as the hull allows); the old view limit (#273) left almost nothing at mid and far zoom.</summary>
        [Test]
        public void ViewPan_IsFree_AtCloseMidAndFarZoom_AsFarAsTheGlassRange()
        {
            CameraGlass.PanBounds(HX, HZ, out float maxX, out float maxZ);
            foreach (float aspect in new[] { 1179f / 2556f, 1080f / 1920f })
                foreach (float zoom in new[] { 0.2f, 0.5f, 1f })
                    foreach (float pitch in new[] { 26f, 34f, 50f })
                    {
                        float dist = FitDist(aspect) * zoom;
                        CameraGlass.PanBoundsFor(0f, pitch, dist, 1f, TanV, aspect, HX, HZ, HullX, HullZ, Bottom, Top, out float bx, out float bz);
                        CameraGlass.PanBoundsFor(0f, pitch, dist, 1f, HX, HZ, HullX, HullZ, Bottom, Top, out float gx, out float gz);
                        Assert.AreEqual(gx, bx, 1e-5f, $"x is the glass's range: zoom {zoom} pitch {pitch} aspect {aspect}");
                        Assert.AreEqual(gz, bz, 1e-5f, $"z: zoom {zoom} pitch {pitch} aspect {aspect}");
                        if (zoom >= 0.5f) Assert.AreEqual(maxX, bx, 1e-4f, $"mid / far zoom, camera clear of the box: the pivot reaches each side wall");
                    }
        }

        /// <summary>#538: wherever the pivot rests (up to the hard stop with the soft band), the camera is never inside the box's hull.</summary>
        [Test]
        public void FreePan_TheCameraNeverEntersTheBox_AtAnyHeldLimit()
        {
            CameraGlass.HardStop(HX, HZ, out float stopX, out float stopZ);
            foreach (float aspect in new[] { 1179f / 2556f })
                foreach (float zoom in new[] { 0.2f, 0.35f, 0.5f, 1f })
                    foreach (float yaw in new[] { -30f, 0f, 28f, 55f })
                        foreach (float pitch in new[] { 14f, 24f, 34f, 50f })
                        {
                            float dist = FitDist(aspect) * zoom;
                            CameraGlass.PanBoundsFor(yaw, pitch, dist, 1f, TanV, aspect, HX, HZ, HullX, HullZ, Bottom, Top, out float bx, out float bz);
                            CameraGlass.CameraOffset(yaw, pitch, dist, out float ox, out float oy, out float oz);
                            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                            {
                                float px = sx * Math.Min(stopX, bx + CameraGlass.Gap), pz = sz * Math.Min(stopZ, bz + CameraGlass.Gap);
                                Assert.IsFalse(CameraGlass.CameraInsideBox(px + ox, 1f + oy, pz + oz, HullX, HullZ, Bottom, Top, 0.3f),
                                    $"camera in the box: zoom {zoom} yaw {yaw} pitch {pitch} pivot {px},{pz}");
                            }
                        }
        }
    }
}
