// t1-camera (decisions.md 2026-11-06, "The default camera"): the camp frame. Pure floats (CampFrame, NearGlassFade), so it runs
// in dotnet test (Tools/SimTests) and in Unity's EditMode (AquariumCiv.Sim.Tests).
using System;
using System.Collections.Generic;
using AquariumCiv.Lighting;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    public class CampFrameTests
    {
        const float HX = 3f, HZ = 2.2f, G = 1f;
        static readonly float TanV = (float)Math.Tan(11.0 * Math.PI / 180.0); // the slice camera's 22 degree lens
        /// <summary>Phones: 1179x2556 (the user's), 1080x1920 (the tester's second size).</summary>
        static readonly float[] Aspects = { 1179f / 2556f, 1080f / 1920f };

        /// <summary>BoxCamera.FitDistance: the whole box across the screen's width.</summary>
        static float Fit(float aspect)
        {
            float diag = (float)Math.Sqrt(3.3 * 3.3 + 1.7 * 1.7 + 2.5 * 2.5);
            return diag / (TanV * Math.Min(1f, aspect));
        }

        static CampFrame.Point P(float x, float z, float w = 1f) => new CampFrame.Point(x, z, w);

        // ---------------------------------------------------------------- the frame

        [Test]
        public void Frame_ACampWithItsPeople_IsCentredOnTheCampAndHoldsTheTents()
        {
            var camp = new List<CampFrame.Point> { P(0.5f, -1.2f, 3f), P(0.85f, -1.0f), P(0.2f, -0.8f), P(0.9f, -1.6f) };
            var dolls = new List<CampFrame.Point> { P(0.6f, -1.1f), P(0.4f, -1.3f), P(0.7f, -0.9f), P(0.3f, -1.0f), P(2.6f, 1.8f) };
            var f = CampFrame.Frame(camp, dolls, HX, HZ);
            Assert.AreEqual(CampFrame.Source.Camp, f.source, f.ToString());
            Assert.AreEqual(0.55f, f.x, 0.25f); Assert.AreEqual(-1.2f, f.z, 0.25f);
            foreach (var c in camp) Assert.LessOrEqual(Dist(c, f), f.radius, "every tent is in the frame: " + f);
            Assert.Greater(Dist(dolls[4], f), f.radius, "the one far away isn't");
            Assert.That(f.radius, Is.InRange(CampFrame.MinRadius, CampFrame.MaxRadius));
        }

        [Test]
        public void Frame_MostPeopleTogetherAwayFromTheCamp_FramesThem()
        {
            var camp = new List<CampFrame.Point> { P(-2f, -1.5f, 3f), P(-1.7f, -1.3f) };
            var dolls = new List<CampFrame.Point> { P(1.8f, 1.2f), P(2.0f, 1.4f), P(1.9f, 1.0f), P(2.2f, 1.3f), P(-2f, -1.4f) };
            var f = CampFrame.Frame(camp, dolls, HX, HZ);
            Assert.AreEqual(CampFrame.Source.Group, f.source, "fire and group don't fit one frame: the group wins: " + f);
            Assert.AreEqual(2.0f, f.x, 0.3f); Assert.AreEqual(1.2f, f.z, 0.3f);
        }

        [Test]
        public void Frame_NoCamp_FramesTheGroup_AndNobody_TheBox()
        {
            var dolls = new List<CampFrame.Point> { P(-1f, 0.5f), P(-1.2f, 0.7f), P(-0.9f, 0.3f), P(2.5f, -1.9f) };
            var f = CampFrame.Frame(new List<CampFrame.Point>(), dolls, HX, HZ);
            Assert.AreEqual(CampFrame.Source.Group, f.source);
            Assert.AreEqual(-1.05f, f.x, 0.2f); Assert.AreEqual(0.5f, f.z, 0.2f);
            var none = CampFrame.Frame(null, null, HX, HZ);
            Assert.AreEqual(CampFrame.Source.Box, none.source);
            Assert.AreEqual(0f, none.x); Assert.AreEqual(0f, none.z);
        }

        [Test]
        public void Frame_IsStable_ASmallShuffleOfFeetBarelyMovesIt_AndItStaysInsideTheLand()
        {
            var camp = new List<CampFrame.Point> { P(2.9f, 2.1f, 3f), P(2.6f, 1.9f) }; // in the far corner
            var dolls = new List<CampFrame.Point>();
            for (int i = 0; i < 8; i++) dolls.Add(P(2.7f + 0.1f * (i % 3), 1.8f + 0.1f * (i / 3)));
            var a = CampFrame.Frame(camp, dolls, HX, HZ);
            var moved = new List<CampFrame.Point>();
            for (int i = 0; i < dolls.Count; i++) moved.Add(P(dolls[i].x + (i % 2 == 0 ? 0.03f : -0.03f), dolls[i].z + (i % 3 == 0 ? 0.03f : -0.02f)));
            var b = CampFrame.Frame(camp, moved, HX, HZ);
            Assert.Less(Math.Abs(a.x - b.x) + Math.Abs(a.z - b.z), 0.1f, $"{a} vs {b}");
            Assert.AreEqual(a.ToString(), CampFrame.Frame(camp, dolls, HX, HZ).ToString(), "the same box gives the same frame");
            Assert.LessOrEqual(Math.Abs(a.x), HX - CampFrame.EdgeInset + 1e-4f);
            Assert.LessOrEqual(Math.Abs(a.z), HZ - CampFrame.EdgeInset + 1e-4f);
        }

        // ---------------------------------------------------------------- the zoom

        [Test]
        public void Zoom_ADollIs46To64PxOfA2556PxScreen_AtEveryFrameSize_InsideTheCameraLimits()
        {
            foreach (float aspect in Aspects)
            {
                float fit = Fit(aspect);
                for (float r = CampFrame.MinRadius; r <= CampFrame.MaxRadius + 1e-3f; r += 0.2f)
                {
                    float zoom = CampFrame.Zoom(r, fit, TanV, aspect);
                    float d = zoom * fit;
                    float px = CampFrame.DollPx(d, CampFrame.Pitch, 2556f, TanV, CameraReach.DollScale(zoom));
                    Assert.That(px, Is.InRange(CampFrame.MinDollPx - 0.5f, CampFrame.MaxDollPx + 0.5f), $"aspect {aspect:0.000} r {r:0.0}: zoom {zoom:0.00}");
                    Assert.That(zoom, Is.InRange(CameraReach.MinZoom, 1f), "inside the limits, closer than the whole box");
                    Assert.GreaterOrEqual(CampFrame.Pitch, CameraReach.PitchFloor(zoom), "the pitch floor allows the frame's angle");
                    // The frame fits across the screen.
                    Assert.LessOrEqual(r, d * TanV * aspect + 1e-3f, $"r {r} fits at aspect {aspect}");
                }
                // The default (a small camp): right on the target.
                float z0 = CampFrame.Zoom(CampFrame.MinRadius, fit, TanV, aspect);
                Assert.AreEqual(CampFrame.TargetDollPx, CampFrame.DollPx(z0 * fit, CampFrame.Pitch, 2556f, TanV, CameraReach.DollScale(z0)), 0.5f);
                // The old default (zoom 1, the whole box) was well under it: the tester's 15-25 px.
                Assert.Less(CampFrame.DollPx(fit, CampFrame.Pitch, 2556f, TanV, CameraReach.DollScale(1f)), CampFrame.MinDollPx);
            }
            Assert.GreaterOrEqual(CameraReach.MaxZoom, 1f, "a pinch out still reaches the whole box");
        }

        // ---------------------------------------------------------------- the near glass is gone

        // BoxView.PartOut: front, back, left, right panes / frame sides, then the posts (-x-z, +x-z, +x+z, -x+z).
        static readonly float[,] PartOut = { { 0, -1 }, { 0, 1 }, { -1, 0 }, { 1, 0 }, { -0.7071f, -0.7071f }, { 0.7071f, -0.7071f }, { 0.7071f, 0.7071f }, { -0.7071f, 0.7071f } };

        [Test]
        public void NearGlass_AtTheCampFrame_TheCameraSidePanesAndFrameAreGone_TheBackStays_ForCampsFrontMiddleAndBack()
        {
            var camps = new[] { (0f, -1.8f), (0.6f, -1.2f), (0f, 0f), (-2.5f, 0f), (2.5f, 0.3f), (0f, 1.8f), (-2.6f, 1.9f) };
            foreach (float aspect in Aspects)
            {
                float fit = Fit(aspect);
                foreach (var (cx, cz) in camps)
                {
                    float zoom = CampFrame.Zoom(1f, fit, TanV, aspect), d = zoom * fit;
                    CameraGlass.CameraOffset(CampFrame.Yaw, CampFrame.Pitch, d, out float ox, out _, out float oz);
                    float camX = cx + ox, camZ = cz + oz, len = (float)Math.Sqrt(camX * camX + camZ * camZ);
                    float tx = camX / len, tz = camZ / len; // BoxView: from the box to the camera, on the ground
                    float camp = NearGlassFade.CampCloseness(d, fit);
                    Assert.AreEqual(1f, camp, 1e-4f, "fully camp-close at the frame");
                    string at = $"camp ({cx}, {cz}) aspect {aspect:0.000}";
                    for (int i = 0; i < 8; i++)
                    {
                        float facing = PartOut[i, 0] * tx + PartOut[i, 1] * tz;
                        float frame = NearGlassFade.Opacity(zoom, facing, true, campCloseness: camp);
                        if (i < 4)
                        {
                            float pane = NearGlassFade.Opacity(zoom, facing, false, campCloseness: camp);
                            if (i == 0 || i == 2) { Assert.LessOrEqual(pane, 0.05f, $"near pane {i}, {at}"); Assert.LessOrEqual(frame, 0.05f, $"near frame side {i}, {at}"); }
                            else { Assert.AreEqual(1f, pane, 1e-4f, $"far pane {i} stays, {at}"); Assert.AreEqual(1f, frame, 1e-4f, $"far frame side {i} stays, {at}"); }
                        }
                        else if (i == 4 || i == 5) Assert.LessOrEqual(frame, 0.05f, $"near post {i} (facing {facing:0.00}), {at}");
                        else Assert.AreEqual(1f, frame, 1e-4f, $"far post {i}, {at}");
                    }
                    // Clean mode brings everything back (#162).
                    Assert.AreEqual(1f, NearGlassFade.Opacity(zoom, 1f, true, cleanMode: true, campCloseness: camp));
                }
            }
        }

        [Test]
        public void NearGlass_TheWholeBoxNeverFades_AndTheFadeComesInSmoothly()
        {
            foreach (float aspect in new[] { 1179f / 2556f, 1080f / 1920f, 0.75f })
            {
                float fit = Fit(aspect);
                Assert.AreEqual(0f, NearGlassFade.CampCloseness(fit, fit), 1e-6f, $"zoom 1 at aspect {aspect}");
                Assert.AreEqual(1f, NearGlassFade.Opacity(1f, 1f, true, campCloseness: NearGlassFade.CampCloseness(fit, fit)), 1e-6f);
                float prev = 0f;
                for (float z = 1f; z >= CameraReach.MinZoom; z -= 0.01f)
                {
                    float c = NearGlassFade.CampCloseness(z * fit, fit);
                    Assert.GreaterOrEqual(c, prev - 1e-6f, "closer never shows more glass");
                    if (aspect < 0.5f) Assert.Less(c - prev, 0.12f, "no pop on a pinch (LightPlayTests' bar)");
                    prev = c;
                }
            }
            Assert.AreEqual(NearGlassFade.MaxFrameFade, NearGlassFade.Amount(0.2f, 1f, isFrame: true), 1e-6f, "without the camp closeness the frame keeps #319's trace");
        }

        // ---------------------------------------------------------------- 2026-11-07: closer (55 px), trimmed, resized

        [Test]
        public void Zoom_TheDefaultIs55PxOf2556_AndTheSameShareOf1920_AtBothSizes_WithTheNearGlassFullyGone()
        {
            Assert.AreEqual(55f, CampFrame.TargetDollPx);
            foreach (var (aspect, h) in new[] { (1179f / 2556f, 2556f), (1080f / 1920f, 1920f) })
            {
                float fit = Fit(aspect);
                float zoom = CampFrame.Zoom(CampFrame.MinRadius, fit, TanV, aspect), d = zoom * fit;
                float px = CampFrame.DollPx(d, CampFrame.Pitch, h, TanV, CameraReach.DollScale(zoom));
                Assert.AreEqual(55f * h / 2556f, px, 0.6f, $"aspect {aspect:0.000}: zoom {zoom:0.00}, {px:0.0} px of {h}");
                Assert.GreaterOrEqual(zoom, CameraReach.MinZoom + 0.05f, "well inside the closest zoom");
                Assert.AreEqual(1f, NearGlassFade.CampCloseness(d, fit), 1e-4f, $"near glass 0 at aspect {aspect:0.000}");
                Assert.GreaterOrEqual(CampFrame.Pitch, CameraReach.PitchFloor(zoom));
                Console.WriteLine($"camp frame aspect {aspect:0.000}: zoom {zoom:0.000}, distance {d:0.0}, doll {px:0.0} px of {h}");
            }
        }

        [Test]
        public void Frame_ASpreadCamp_KeepsTheFireAndTheBusiestPeople_WithinTheMaxRadius_AndNothingNearTheFireIsCut()
        {
            var camp = new List<CampFrame.Point> { P(0f, 0f, 3f), P(1.0f, 0.3f), P(-1.0f, -0.4f), P(0.2f, 1.1f) };
            var dolls = new List<CampFrame.Point> { P(0.1f, 0.1f), P(0.3f, -0.2f), P(-0.2f, 0.3f), P(0.5f, 0.5f), P(-0.6f, -0.1f), P(1.9f, 1.0f), P(-1.8f, -1.2f) };
            var f = CampFrame.Frame(camp, dolls, HX, HZ);
            Assert.LessOrEqual(f.radius, CampFrame.MaxRadius + 1e-4f, f.ToString());
            Assert.LessOrEqual(Dist(camp[0], f), f.radius, "the fire is in");
            for (int i = 0; i < 5; i++) Assert.LessOrEqual(Dist(dolls[i], f), f.radius, $"doll {i} at the fire is in: {f}");
            Assert.Greater(Dist(dolls[5], f), f.radius - CampFrame.Pad, "the stragglers at the edge are let go of");
        }

        [Test]
        public void Resize_TheFrameIsFittedAgainWhenTheAspectMoves()
        {
            Assert.IsTrue(CampFrame.AspectChanged(0f, 0.46f), "never framed");
            Assert.IsTrue(CampFrame.AspectChanged(1179f / 2556f, 1080f / 1920f), "1179x2556 to 1080x1920");
            Assert.IsFalse(CampFrame.AspectChanged(1179f / 2556f, 1179.5f / 2556f), "a pixel of jitter isn't a resize");
            // The old zoom at the new size is not the frame: the zoom is a share of the fit, which moves with the aspect.
            float a0 = 1179f / 2556f, a1 = 1080f / 1920f;
            float z0 = CampFrame.Zoom(1f, Fit(a0), TanV, a0), z1 = CampFrame.Zoom(1f, Fit(a1), TanV, a1);
            float pxStale = CampFrame.DollPx(z0 * Fit(a1), CampFrame.Pitch, 1920f, TanV, CameraReach.DollScale(z0));
            float pxFresh = CampFrame.DollPx(z1 * Fit(a1), CampFrame.Pitch, 1920f, TanV, CameraReach.DollScale(z1));
            Assert.AreEqual(55f * 1920f / 2556f, pxFresh, 0.6f);
            Assert.Greater(Math.Abs(pxFresh - pxStale), 1.0f, "so the frame must be recomputed");
        }

        static float Dist(CampFrame.Point p, CampFrame.Result f) => (float)Math.Sqrt((p.x - f.x) * (p.x - f.x) + (p.z - f.z) * (p.z - f.z));
    }
}
