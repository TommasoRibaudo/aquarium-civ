// #783 (docs/design/783-story-camera.md, option A): the maths of the camera's lean at a held scene. Pure floats (StoryLean, CameraReach,
// GlideBack), so it runs in dotnet test (Tools/SimTests) and in Unity's EditMode.
using System;
using AquariumCiv.Lighting;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    public class StoryLeanTests
    {
        const float Pitch = 34f, Lean = 52f;
        static readonly (string name, float h)[] Screens = { ("750x1334", 1334f), ("1179x2556", 2556f), ("tablet 1640x2360", 2360f) };

        /// <summary>The doll's height at the camp frame: 55 px of a 2556-px screen, the same share on every screen.</summary>
        static float CampPx(float screenH) => screenH * CampFrame.TargetDollPx / CampFrame.ReferenceScreenH;

        /// <summary>The doll's height after the lean, from the model ZoomFor inverts: px scales with 1/zoom, the doll's growth with the zoom
        /// and the cosine of the pitch.</summary>
        static float PxAfter(float pxNow, float zoomNow, float zoomTo, float pitchNow, float pitchTo)
            => pxNow * (zoomNow / zoomTo) * CameraReach.DollScale(zoomTo) / CameraReach.DollScale(zoomNow)
               * (float)(Math.Cos(pitchTo * Math.PI / 180.0) / Math.Cos(pitchNow * Math.PI / 180.0));

        [Test]
        public void ScenePx_IsFivePercentOfTheScreensHeight()
        {
            Assert.AreEqual(66.7f, StoryLean.ScenePx(1334f), 0.1f, "67 px on 1334");
            Assert.AreEqual(127.8f, StoryLean.ScenePx(2556f), 0.1f, "128 px on 2556");
        }

        [Test]
        public void ZoomFor_GivesTheSamePixelShareOnEveryScreen()
        {
            float first = -1f;
            foreach (var (name, h) in Screens)
            {
                float zoomNow = 0.8f, px = CampPx(h); // far enough out that the target is above the player's floor
                float z = StoryLean.ZoomFor(px, h, zoomNow, 0f, Pitch, Lean);
                Assert.Less(z, zoomNow, name + ": it leans in from the camp frame");
                float after = PxAfter(px, zoomNow, z, Pitch, Lean);
                Assert.AreEqual(StoryLean.ScenePx(h), after, StoryLean.ScenePx(h) * 0.03f, name + ": a doll is 5 % of the height after the lean");
                if (first < 0f) first = z; else Assert.AreEqual(first, z, 0.01f, name + ": the same zoom, the share is what counts");
            }
        }

        [Test]
        public void ZoomFor_FromTheCampFrame_IsAboutTwoToThreeTimesCloser()
        {
            // the spec estimate: ~29 px on 1334 to ~67 px, about 2.3x closer in pixels; the raised pitch costs a little more
            float z = StoryLean.ZoomFor(29f, 1334f, 0.6f, 0f, Pitch, Lean);
            Assert.That(0.6f / z, Is.InRange(2.0f, 3.2f), "zoom " + z);
        }

        [Test]
        public void ZoomFor_NeverZoomsOut_APlayerAlreadyCloserKeepsTheirZoom()
        {
            // already 90 px on 1334 at zoom 0.3: bigger than the target, so the zoom stays
            Assert.AreEqual(0.3f, StoryLean.ZoomFor(90f, 1334f, 0.3f, 0f, Pitch, Lean), 1e-4f);
            // a cast that does not fit even now: it would need to back off, which a lean never does
            Assert.AreEqual(0.5f, StoryLean.ZoomFor(29f, 1334f, 0.5f, 3f, Pitch, Lean), 1e-4f);
            for (float zoomNow = 0.2f; zoomNow <= 1.3f; zoomNow += 0.1f)
                Assert.LessOrEqual(StoryLean.ZoomFor(25f, 1334f, zoomNow, 0.4f, Pitch, Lean), zoomNow + 1e-5f);
        }

        [Test]
        public void ZoomFor_FromAViewSoCloseTheTargetIsPastTheFloor_StopsAtTheFloor_ASmallerShare()
        {
            // a camp-frame doll (29 px on 1334) at zoom 0.55 needs a zoom of ~0.18 for 67 px at the leaning pitch: the player's floor (0.2) wins
            float z = StoryLean.ZoomFor(CampPx(1334f), 1334f, 0.55f, 0f, Pitch, Lean);
            Assert.AreEqual(CameraReach.MinZoom, z, 1e-4f);
        }

        [Test]
        public void ZoomFor_StopsAtThePlayersOwnFloor()
        {
            float z = StoryLean.ZoomFor(2f, 1334f, 1f, 0f, Pitch, Lean); // a speck: it would want to go far past the floor
            Assert.AreEqual(CameraReach.MinZoom, z, 1e-4f);
        }

        [Test]
        public void ZoomFor_AWideCastIsHeldInTheFrame_NotJustBigAsTheTarget()
        {
            float px = CampPx(1334f);
            float tight = StoryLean.ZoomFor(px, 1334f, 0.8f, 0f, Pitch, Lean);
            float wide = StoryLean.ZoomFor(px, 1334f, 0.8f, 0.5f, Pitch, Lean); // the cast fills half the allowed frame at 0.8: it needs 0.4
            Assert.AreEqual(0.4f, wide, 0.01f);
            Assert.Greater(wide, tight - 1e-4f, "a wider cast sits no closer than a tight one");
        }

        [Test]
        public void CastExtent_UsesTheMiddleEightyAcrossAndSeventyUp()
        {
            Assert.AreEqual(1f, StoryLean.CastExtent(0.8f, 0f), 1e-5f);
            Assert.AreEqual(1f, StoryLean.CastExtent(0f, 0.7f), 1e-5f);
            Assert.AreEqual(0f, StoryLean.CastExtent(-1f, -1f), 1e-5f);
            Assert.AreEqual(2f, StoryLean.CastExtent(1.6f, 0.7f), 1e-5f);
        }

        [Test]
        public void NeedsMove_ASceneAlreadyFramedAtThePlayersZoom_MovesNothing()
        {
            Assert.IsFalse(StoryLean.NeedsMove(0.3f, 0.3f, 0.5f, 0.5f), "the player is already on the scene");
            Assert.IsFalse(StoryLean.NeedsMove(0.3f, 0.29f, 0.45f, 0.55f), "within the same-zoom slack");
            Assert.IsTrue(StoryLean.NeedsMove(0.6f, 0.3f, 0.5f, 0.5f), "zoomed out: it leans in");
            Assert.IsTrue(StoryLean.NeedsMove(0.3f, 0.3f, 0.9f, 0.5f), "zoomed on somebody else: it pans over");
            Assert.IsTrue(StoryLean.NeedsMove(0.3f, 0.3f, 0.5f, 0.1f));
        }

        [Test]
        public void MayLean_TheFirstAndTheChainedOneAndOneAfterTheCooldown_ButNotInBetween()
        {
            Assert.IsTrue(StoryLean.MayLean(null), "the first");
            Assert.IsTrue(StoryLean.MayLean(0.5f), "the next of two queued, as the first ends: it chains (one way home)");
            Assert.IsFalse(StoryLean.MayLean(8f), "a busy day is not a tour");
            Assert.IsFalse(StoryLean.MayLean(19f));
            Assert.IsTrue(StoryLean.MayLean(20f));
        }

        [Test]
        public void TheFollowAndTheFrameBands_AreSane()
        {
            Assert.IsFalse(StoryLean.NeedsFollow(0.5f, 0.5f));
            Assert.IsTrue(StoryLean.NeedsFollow(0.8f, 0.5f));
            Assert.IsTrue(StoryLean.NeedsFollow(0.5f, 0.75f));
            Assert.IsFalse(StoryLean.LeftTheFrame(0.8f, 0.5f), "following is not leaving");
            Assert.IsTrue(StoryLean.LeftTheFrame(0.98f, 0.5f));
            Assert.IsTrue(StoryLean.LeftTheFrame(0.5f, 0.02f));
        }

        [Test]
        public void TheEaseBack_IsTheGlidesOwn_AndTheCooldownOutlastsALeanAndAGlide()
        {
            Assert.AreEqual(1.2f, GlideBack.Hold, 1e-4f);
            Assert.AreEqual(1.1f, GlideBack.Duration, 1e-4f);
            Assert.Greater(StoryLean.CooldownSeconds, GlideBack.Hold + GlideBack.Duration + 1.4f);
        }

        [Test]
        public void TheGlideBack_GoesToThePlayersSavedPose_AfterItsWaitAndItsEase()
        {
            var g = new GlideBack();
            float x = 1f, z = 2f, zoom = 0.3f, yaw = 40f, pitch = 52f;
            g.Remember(0.25f, -0.5f, 0.8f, 28f, 34f);
            Assert.IsTrue(g.Armed);
            float t = 0f; int steps = 0;
            while (g.Armed && steps++ < 1000) { t += 0.05f; g.Step(0.05f, ref x, ref z, ref zoom, ref yaw, ref pitch); }
            Assert.IsFalse(g.Armed, "the glide finished");
            Assert.AreEqual(0.25f, x, 1e-3f); Assert.AreEqual(-0.5f, z, 1e-3f); Assert.AreEqual(0.8f, zoom, 1e-3f);
            Assert.AreEqual(28f, yaw, 1e-2f); Assert.AreEqual(34f, pitch, 1e-2f);
            Assert.That(t, Is.InRange(GlideBack.Hold + GlideBack.Duration - 0.15f, GlideBack.Hold + GlideBack.Duration + 0.15f), "1.2 s wait, 1.1 s ease");
        }

        [Test]
        public void AtTheZoomFloor_TheLeanKeepsThePitch_AboveItItLooksDown()
        {
            Assert.AreEqual(Pitch, StoryLean.LeanPitchFor(Pitch, Lean, 0.2f, 0.2f), 1e-4f, "at the floor: no tilt, it would only shorten the dolls");
            Assert.AreEqual(Pitch, StoryLean.LeanPitchFor(Pitch, Lean, 0.15f, 0.2f), 1e-4f, "under the player's floor (the lean's own): no tilt");
            Assert.AreEqual(Lean, StoryLean.LeanPitchFor(Pitch, Lean, 0.35f, 0.2f), 1e-4f, "above the floor: it looks down");
            Assert.AreEqual(60f, StoryLean.LeanPitchFor(60f, Lean, 0.35f, 0.2f), 1e-4f, "never flatter than now");
            float px = 50f, at = PxAfter(px, 0.2f, 0.2f, Pitch, StoryLean.LeanPitchFor(Pitch, Lean, 0.2f, 0.2f));
            Assert.GreaterOrEqual(at, px - 1e-3f, "at the floor the dolls don't get smaller");
        }
    }
}
