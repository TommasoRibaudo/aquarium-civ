using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#587: the touch swap's pure maths. One finger pans, two fingers twist (yaw), slide (pitch) and pinch.</summary>
    public class BoxCameraTouchSwapTests
    {
        [Test]
        public void Twist_Clockwise_TurnsYawDown_CounterClockwiseUp()
        {
            var from = new Vector2(100f, 0f);
            Assert.That(BoxCameraMath.TwistDegrees(from, new Vector2(0f, -100f)), Is.EqualTo(-90f).Within(1e-3f), "a quarter turn clockwise");
            Assert.That(BoxCameraMath.TwistDegrees(from, new Vector2(0f, 100f)), Is.EqualTo(90f).Within(1e-3f), "a quarter turn counter-clockwise");
            Assert.That(BoxCameraMath.TwistDegrees(from, from * 3f), Is.EqualTo(0f).Within(1e-3f), "a pinch alone does not twist");
        }

        [Test]
        public void Twist_NeedsTwoFingersApart()
        {
            Assert.That(BoxCameraMath.TwistDegrees(Vector2.zero, new Vector2(10f, 3f)), Is.EqualTo(0f));
            Assert.That(BoxCameraMath.TwistDegrees(new Vector2(10f, 3f), Vector2.zero), Is.EqualTo(0f));
        }

        [Test]
        public void PanFling_IsCapped_AndDiesOut()
        {
            Assert.That(BoxCameraMath.CapPanFling(new Vector2(30f, 40f)).magnitude, Is.EqualTo(BoxCameraMath.PanFlingCap).Within(1e-3f));
            Assert.That(BoxCameraMath.CapPanFling(new Vector2(1f, 2f)), Is.EqualTo(new Vector2(1f, 2f)));
            var v = new Vector2(8f, 0f);
            float last = v.magnitude; int frames = 0;
            while (v != Vector2.zero && frames++ < 600) { v = BoxCameraMath.PanInertiaStep(v, 1f / 60f); Assert.That(v.magnitude, Is.LessThan(last + 1e-6f)); last = v.magnitude; }
            Assert.That(v, Is.EqualTo(Vector2.zero), "stopped");
            Assert.That(frames, Is.LessThan(120), "within about two seconds");
        }

        [Test]
        public void PanFling_FromSamples_IsWorldUnitsPerSecond()
        {
            var s = new System.Collections.Generic.List<(float t, Vector2 d)> { (0.95f, new Vector2(0.04f, 0f)), (0.98f, new Vector2(0.04f, 0f)) };
            Assert.That(BoxCameraMath.Velocity(s, 1f).x, Is.EqualTo(1f).Within(1e-3f), "0.08 units over 80 ms");
        }

        [Test]
        public void TwoFingerTurn_IsTwistForYaw_AndMidpointSlideForPitch()
        {
            var t = BoxCameraMath.TwoFingerTurn(new Vector2(200f, 0f), new Vector2(0f, 200f), new Vector2(0f, -25f), 2000f);
            Assert.That(t.x, Is.EqualTo(90f).Within(1e-3f), "yaw from the twist");
            Assert.That(t.y, Is.EqualTo(BoxCameraMath.PitchDegrees(25f, 2000f)).Within(1e-4f), "pitch from the slide (up the screen lowers the camera)");
            var close = BoxCameraMath.TwoFingerTurn(new Vector2(20f, 0f), new Vector2(0f, 20f), Vector2.zero, 2000f);
            Assert.That(close.x, Is.EqualTo(0f), "fingers too close have no direction");
        }
    }
}
