using NUnit.Framework;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>#376: a pinch is never a tap or a knock.</summary>
    public class TapGestureTests
    {
        const float Slop = 20f, Max = 0.35f;
        static TapGesture.Event U(TapGesture g, int n, float t, float x = 100f) => g.Update(n, x, 100f, t, Slop, Max);

        [Test] public void OneFingerQuickIsATap()
        {
            var g = new TapGesture();
            Assert.AreEqual(TapGesture.Event.Began, U(g, 1, 0f));
            Assert.AreEqual(TapGesture.Event.Tap, U(g, 0, 0.1f));
            Assert.IsTrue(g.Quick);
        }

        [Test] public void SlowPressIsATapButNotQuick()
        {
            var g = new TapGesture();
            U(g, 1, 0f);
            Assert.AreEqual(TapGesture.Event.Tap, U(g, 0, 1f));
            Assert.IsFalse(g.Quick);
        }

        [Test] public void SecondFingerCancelsAndNoTapFollows()
        {
            var g = new TapGesture();
            U(g, 1, 0f);
            Assert.AreEqual(TapGesture.Event.Cancelled, U(g, 2, 0.05f));
            Assert.AreEqual(TapGesture.Event.None, U(g, 1, 0.1f));
            Assert.AreEqual(TapGesture.Event.None, U(g, 0, 0.15f));
        }

        // The old code's hole: both fingers land in one frame, one lifts, and the survivor started a new press that
        // ended as a knock.
        [Test] public void TwoFingersLandingTogetherThenOneLiftingIsNoTap()
        {
            var g = new TapGesture();
            Assert.AreEqual(TapGesture.Event.None, U(g, 2, 0f));
            Assert.AreEqual(TapGesture.Event.None, U(g, 1, 0.1f));
            Assert.AreEqual(TapGesture.Event.None, U(g, 0, 0.2f));
        }

        [Test] public void LiftingOneFingerOutOfAPinchThenTheOtherIsNoTap()
        {
            var g = new TapGesture();
            U(g, 1, 0f); U(g, 2, 0.02f); U(g, 2, 0.5f);
            Assert.AreEqual(TapGesture.Event.None, U(g, 1, 0.6f));
            Assert.AreEqual(TapGesture.Event.None, U(g, 0, 0.7f));
        }

        [Test] public void ANewTapAfterAPinchWorks()
        {
            var g = new TapGesture();
            U(g, 2, 0f); U(g, 1, 0.1f); U(g, 0, 0.2f);
            Assert.AreEqual(TapGesture.Event.Began, U(g, 1, 0.3f));
            Assert.AreEqual(TapGesture.Event.Tap, U(g, 0, 0.4f));
            Assert.IsTrue(g.Quick);
        }

        [Test] public void MovingPastSlopCancels()
        {
            var g = new TapGesture();
            U(g, 1, 0f);
            Assert.AreEqual(TapGesture.Event.Cancelled, U(g, 1, 0.1f, 150f));
            Assert.AreEqual(TapGesture.Event.None, U(g, 0, 0.2f));
        }
    }
}
