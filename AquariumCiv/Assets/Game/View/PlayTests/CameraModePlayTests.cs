using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Camera mode in the real Slice scene (#70, user 2026-10-30): the player zooms and moves freely (#587: one finger pans, two
    /// twist, slide and pinch, by touch), the camera keeps following the person they were following (a person card, a pin), a real
    /// pan lets go, and the auto-orbit waits 10 s. The HUD side is CameraModeTests (UI). It backs up and restores the save.
    /// </summary>
    [Category("CameraModePlay")]
    public class CameraModePlayTests : PlayTestBase
    {
        IEnumerator Open(int w = 1179, int h = 2556)
        {
            ShowMeDirector.BubblesOn = false;
            yield return OpenSlice(w, h, seed: 1234);
        }

        void Close()
        {
            ShowMeDirector.BubblesOn = true;
            CloseSlice();
        }

        IEnumerator Drag(Vector2 a, Vector2 b, int steps = 20)
        {
            int id = touchId++;
            BeginTouch(id, a, queueEventOnly: true);
            yield return null; yield return null;
            for (int i = 1; i <= steps; i++)
            {
                MoveTouch(id, Vector2.Lerp(a, b, i / (float)steps), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(id, b, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
        }

        /// <summary>Two fingers from (a0, b0) to (a1, b1).</summary>
        IEnumerator TwoFingers(Vector2 a0, Vector2 b0, Vector2 a1, Vector2 b1, int steps = 20)
        {
            int ia = touchId++, ib = touchId++;
            BeginTouch(ia, a0, queueEventOnly: true);
            BeginTouch(ib, b0, queueEventOnly: true);
            yield return null; yield return null; yield return null;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                MoveTouch(ia, Vector2.Lerp(a0, a1, t), delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(ib, Vector2.Lerp(b0, b1, t), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(ia, a1, delta: Vector2.zero, queueEventOnly: true);
            EndTouch(ib, b1, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null; yield return null;
        }

        /// <summary>#587: two fingers turning about the screen's middle: the line between them goes from <paramref name="deg0"/> to <paramref name="deg1"/> (math degrees, y up),
        /// the midpoint slides by <paramref name="slide"/> and the half span goes from <paramref name="r0"/> to <paramref name="r1"/> pixels.</summary>
        IEnumerator Twist(float deg0, float deg1, Vector2 slide = default, float r0 = -1f, float r1 = -1f, int steps = 30)
        {
            if (r0 < 0f) r0 = Screen.width * 0.2f;
            if (r1 < 0f) r1 = r0;
            Vector2 P(float t, float sign)
            {
                float d = Mathf.Lerp(deg0, deg1, t) * Mathf.Deg2Rad, r = Mathf.Lerp(r0, r1, t);
                return Mid + slide * t + new Vector2(Mathf.Cos(d), Mathf.Sin(d)) * r * sign;
            }
            int ia = touchId++, ib = touchId++;
            BeginTouch(ia, P(0f, 1f), queueEventOnly: true); BeginTouch(ib, P(0f, -1f), queueEventOnly: true);
            yield return null; yield return null; yield return null;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                MoveTouch(ia, P(t, 1f), delta: Vector2.zero, queueEventOnly: true); MoveTouch(ib, P(t, -1f), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(ia, P(1f, 1f), delta: Vector2.zero, queueEventOnly: true); EndTouch(ib, P(1f, -1f), delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null; yield return null;
        }

        static Vector2 Mid => new(Screen.width * 0.5f, Screen.height * 0.55f);
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        static bool DollWorld(PersonRef who, out Vector3 w)
        {
            w = default;
            var pv = PeopleView.Find();
            if (pv == null || !pv.TryGetDollPosition(who, out var p)) return false;
            w = pv.transform.TransformPoint(new Vector3(p.x, 0f, p.y));
            return true;
        }

        /// <summary>A living notable with a doll on the land now.</summary>
        static IEnumerator FindDoll(Action<int> found)
        {
            var sim = GameClock.Instance.Sim;
            PeopleView.SetHourGently(10.5f);
            for (int i = 0; i < 300; i++)
            {
                var n = sim.State.notables.FirstOrDefault(x => x.alive && DollWorld(PersonRef.Notable(x.id), out _));
                if (n != null) { found(n.id); yield break; }
                yield return null;
            }
            Assert.Fail("no notable has a doll");
        }

        [UnityTest]
        public IEnumerator OrbitPinchAndPanAllWorkInCameraMode()
        {
            yield return Open();
            try
            {
                var hud = GameHud.Instance;
                var rig = Rig;
                hud.SetCameraMode(true);
                yield return new WaitForSecondsRealtime(0.3f);

                // #587: one finger pans (the ground follows the finger), and turns nothing
                float yaw0 = rig.yaw, pitch0 = rig.pitch;
                var t0 = rig.target;
                yield return Drag(Mid, Mid + new Vector2(-0.2f * Screen.width, 0.06f * Screen.height));
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(hud.CameraMode, "a drag stays in camera mode");
                Assert.Greater(new Vector2(rig.target.x - t0.x, rig.target.z - t0.z).magnitude, 0.15f, "one finger pans");
                Assert.Less(Mathf.Abs(rig.yaw - yaw0), 0.5f, "and does not turn");
                Assert.Less(Mathf.Abs(rig.pitch - pitch0), 0.5f, "or tilt");

                float zoom0 = rig.zoom;
                var side = new Vector2(0.15f * Screen.width, 0f);
                yield return TwoFingers(Mid - side, Mid + side, Mid - side * 2f, Mid + side * 2f);
                Assert.IsTrue(hud.CameraMode, "a pinch stays in camera mode");
                Assert.Less(rig.zoom, zoom0 * 0.8f, "a pinch out zooms in");

                // two fingers twisting turn the camera
                yaw0 = rig.yaw;
                yield return Twist(0f, 25f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsTrue(hud.CameraMode, "a twist stays in camera mode");
                Assert.Greater(Mathf.Abs(rig.yaw - yaw0), 5f, "two fingers twisting turn the camera");
                yield return Shot("camera-mode-free");
                Assert.IsEmpty(errors, string.Join("\n", errors));
                hud.SetCameraMode(false);
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TheCameraKeepsFollowingTheOpenCardsPersonUntilAPan()
        {
            yield return Open();
            try
            {
                var hud = GameHud.Instance;
                var rig = Rig;
                var cards = PersonCardView.Instance;
                int id = -1;
                yield return FindDoll(x => id = x);
                var who = PersonRef.Notable(id);
                Assert.IsTrue(cards.Open(id, true), "the card opens");
                float t = 0f;
                while (rig.IsFlying && t < 4f) { t += Time.unscaledDeltaTime; yield return null; }

                hud.SetCameraMode(true);
                Assert.AreEqual(who, rig.Following, "camera mode takes over the card's person");
                StringAssert.StartsWith("Following ", hud.Root.Q<UnityEngine.UIElements.Button>("hud-photo-exit").text, "the pill says who is followed");
                yield return null; yield return null;
                Assert.IsFalse(cards.IsOpen, "the card closes in camera mode");

                // It eases onto the doll and stays on them while they walk.
                t = 0f; float off = 1f;
                while (t < 4f)
                {
                    t += Time.unscaledDeltaTime;
                    Assert.IsTrue(rig.IsFollowing, "still following");
                    if (DollWorld(who, out var d)) off = new Vector2(rig.target.x - d.x, rig.target.z - d.z).magnitude;
                    yield return null;
                }
                var b = BoxCameraMath.PanBounds(rig.zoom);
                if (DollWorld(who, out var w) && Mathf.Abs(w.x) < b.x && Mathf.Abs(w.z) < b.y)
                    Assert.Less(off, 0.3f, "the camera is on the doll");
                yield return Shot("camera-mode-follow");

                // A twist and a pinch on the spot keep them.
                yield return Twist(0f, 15f);
                Assert.AreEqual(who, rig.Following, "a turn keeps following");
                var side = new Vector2(0.12f * Screen.width, 0f);
                float z0 = rig.zoom;
                yield return TwoFingers(Mid - side, Mid + side, Mid - side * 0.7f, Mid + side * 0.7f);
                Assert.AreEqual(who, rig.Following, "a pinch keeps following");
                Assert.Greater(rig.zoom, z0, "and zooms");
                Assert.IsTrue(hud.CameraMode);

                // A real pan lets go.
                var move = new Vector2(0f, 0.2f * Screen.height);
                yield return Drag(Mid, Mid + move);
                Assert.IsFalse(rig.IsFollowing, "a one-finger pan lets them go");

                hud.SetCameraMode(false);
                Assert.IsFalse(rig.IsFollowing);
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }

        // ---- #587 touch swap: one finger pans, two fingers twist (yaw), slide (pitch) and pinch (zoom) ----

        [UnityTest]
        public IEnumerator OneFingerPan_GrabsTheGround_AndFlingsOnRelease()
        {
            yield return Open();
            try
            {
                var rig = Rig;
                rig.EaseTo(new Vector3(0f, 1f, 0f), 28f, 34f, 0.6f, 0f);
                for (int i = 0; i < 40; i++) yield return null;
                float yaw0 = rig.yaw, pitch0 = rig.pitch, zoom0 = rig.zoom;
                var t0 = rig.target;
                yield return Drag(Mid, Mid + new Vector2(-0.12f * Screen.width, 0f), steps: 30);
                var moved = rig.target - t0;
                Assert.Greater(new Vector2(moved.x, moved.z).magnitude, 0.2f, "one finger pans");
                Assert.Less(Mathf.Abs(rig.yaw - yaw0), 0.5f, "no turn");
                Assert.Less(Mathf.Abs(rig.pitch - pitch0), 0.5f, "no tilt");
                Assert.AreEqual(zoom0, rig.zoom, 1e-4f, "no zoom");
                // a quick flick keeps moving after the lift
                int id = touchId++;
                BeginTouch(id, Mid, queueEventOnly: true); yield return null; yield return null;
                for (int i = 1; i <= 6; i++) { MoveTouch(id, Mid + new Vector2(0.05f * Screen.width * i, 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
                EndTouch(id, Mid + new Vector2(0.3f * Screen.width, 0f), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
                var atLift = rig.target;
                for (int i = 0; i < 20; i++) yield return null;
                Assert.Greater(new Vector2(rig.target.x - atLift.x, rig.target.z - atLift.z).magnitude, 0.05f, "a flick carries the pan after the lift");
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator OneFingerTap_MovesNothing()
        {
            yield return Open();
            try
            {
                var rig = Rig;
                var t0 = rig.target; float yaw0 = rig.yaw;
                yield return Drag(Mid, Mid + new Vector2(2f, 2f), steps: 3);
                Assert.AreEqual(t0, rig.target, "a tap inside the slop pans nothing");
                Assert.AreEqual(yaw0, rig.yaw, 1e-3f);
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TwoFingerTwist_TurnsYaw_WithoutPanning()
        {
            yield return Open();
            try
            {
                var rig = Rig;
                rig.EaseTo(new Vector3(0f, 1f, 0f), 20f, 34f, 0.8f, 0f);
                for (int i = 0; i < 40; i++) yield return null;
                float yaw0 = rig.yaw, zoom0 = rig.zoom; var t0 = rig.target;
                yield return Twist(0f, -30f); // clockwise
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.Less(rig.yaw - yaw0, -8f, "a clockwise twist turns yaw down (the world follows the fingers)");
                Assert.AreEqual(zoom0, rig.zoom, 0.02f, "a twist alone does not zoom");
                Assert.Less(new Vector2(rig.target.x - t0.x, rig.target.z - t0.z).magnitude, 0.1f, "and does not pan");
                float yaw1 = rig.yaw;
                yield return Twist(0f, 30f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.Greater(rig.yaw - yaw1, 8f, "counter-clockwise turns it back up");
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TwoFingerSlide_ChangesPitch_AndAPinchZooms_InTheSameGesture()
        {
            yield return Open();
            try
            {
                var rig = Rig;
                rig.EaseTo(new Vector3(0f, 1f, 0f), 28f, 40f, 0.8f, 0f);
                for (int i = 0; i < 40; i++) yield return null;
                float pitch0 = rig.pitch, zoom0 = rig.zoom, yaw0 = rig.yaw; var t0 = rig.target;
                yield return Twist(0f, 0f, slide: new Vector2(0f, 0.12f * Screen.height)); // both fingers move up the screen
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.Greater(Mathf.Abs(rig.pitch - pitch0), 3f, "moving both fingers up or down changes pitch");
                Assert.Less(Mathf.Abs(rig.yaw - yaw0), 1f, "and not yaw");
                Assert.Less(new Vector2(rig.target.x - t0.x, rig.target.z - t0.z).magnitude, 0.1f, "and does not pan");

                // pinch + twist in one gesture
                zoom0 = rig.zoom; yaw0 = rig.yaw;
                yield return Twist(0f, 20f, r0: Screen.width * 0.12f, r1: Screen.width * 0.24f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.Less(rig.zoom, zoom0 * 0.85f, "the pinch zooms in");
                Assert.Greater(Mathf.Abs(rig.yaw - yaw0), 3f, "while the twist turns");
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator Pinch_Zooms_AndNeverTurns()
        {
            yield return Open();
            try
            {
                var rig = Rig;
                float yaw0 = rig.yaw, pitch0 = rig.pitch, zoom0 = rig.zoom;
                var side = new Vector2(0.1f * Screen.width, 0f);
                yield return TwoFingers(Mid - side, Mid + side, Mid - side * 2.2f, Mid + side * 2.2f);
                Assert.Less(rig.zoom, zoom0 * 0.85f, "a pinch out zooms in");
                Assert.Less(Mathf.Abs(rig.yaw - yaw0), 1f, "a straight pinch is not a twist");
                Assert.Less(Mathf.Abs(rig.pitch - pitch0), 1.5f, "or a tilt");
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator ThePinnedPersonIsAlwaysFlownTo_AndAnUnpinnedBoxIsNot()
        {
            yield return Open();
            try
            {
                var hud = GameHud.Instance;
                var rig = Rig;
                var sim = GameClock.Instance.Sim;
                hud.SetCameraMode(true);
                Assert.IsFalse(rig.IsFollowing, "nobody to follow");
                hud.SetCameraMode(false);
                yield return null;

                int id = -1;
                yield return FindDoll(x => id = x);
                Assert.IsTrue(sim.Pin(id));
                Assert.IsTrue(DollWorld(PersonRef.Notable(id), out var w));
                rig.FlyTo(w, 0.7f, 0.01f);
                yield return null; yield return null;
                hud.SetCameraMode(true);
                Assert.AreEqual(PersonRef.Notable(id), rig.Following, "the pinned person in view is followed");
                hud.SetCameraMode(false);
                yield return null;

                // Off screen (user 2026-10-30): the far corner of the box, zoomed in.
                rig.FlyTo(new Vector3(w.x > 0f ? -2.4f : 2.4f, w.y, w.z > 0f ? -1.4f : 1.4f), 0.3f, 0.01f);
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.IsTrue(DollWorld(PersonRef.Notable(id), out w));
                var seen = Camera.main.WorldToViewportPoint(w);
                Assert.IsFalse(seen.z > 0f && seen.x > 0.05f && seen.x < 0.95f && seen.y > 0.05f && seen.y < 0.95f, "they start off screen");
                hud.SetCameraMode(true);
                Assert.AreEqual(PersonRef.Notable(id), rig.Following, "off screen, they are still followed");
                Assert.IsTrue(rig.IsFlying, "and the camera flies to them");
                yield return new WaitForSecondsRealtime(BoxCameraMath.FlyDefaultSeconds + 0.8f);
                Assert.IsTrue(DollWorld(PersonRef.Notable(id), out w));
                seen = Camera.main.WorldToViewportPoint(w);
                Assert.IsTrue(seen.z > 0f && seen.x > 0.1f && seen.x < 0.9f && seen.y > 0.1f && seen.y < 0.9f, $"they are on screen after the flight ({seen})");
                hud.SetCameraMode(false);
                sim.Unpin();
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TheAutoOrbitStartsAfterTenSecondsIdle()
        {
            yield return Open();
            try
            {
                var hud = GameHud.Instance;
                var rig = Rig;
                Assert.AreEqual(10f, BoxCamera.AutoOrbitIdleSeconds, "user 2026-10-30");
                Assert.IsTrue(BoxCamera.AutoOrbitInCameraMode);
                yield return Drag(Mid, Mid + new Vector2(0.03f * Screen.width, 0f)); // leave the yaw limits
                yield return new WaitForSecondsRealtime(1f);
                hud.SetCameraMode(true);
                float yaw0 = rig.yaw;
                yield return new WaitForSecondsRealtime(BoxCamera.AutoOrbitIdleSeconds - 2f);
                Assert.AreEqual(yaw0, rig.yaw, 0.05f, "still at 8 s");
                yield return new WaitForSecondsRealtime(4.5f);
                Assert.Greater(Mathf.Abs(rig.yaw - yaw0), 1f, "drifting by 12.5 s");
                hud.SetCameraMode(false);
            }
            finally { Close(); }
        }
    }
}
