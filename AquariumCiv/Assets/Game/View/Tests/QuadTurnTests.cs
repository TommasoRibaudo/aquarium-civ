// #771: a standing wolf's turn eases: no snap between headings, no twitch from a tiny move or a flickering look target.
// Pure (System + NUnit only), so it also runs offline.
using System;
using NUnit.Framework;

namespace AquariumCiv.View.Tests
{
    public class QuadTurnTests
    {
        const float Dt = 1f / 60f;

        static QuadTurn At(float yaw) => new QuadTurn { yaw = yaw, target = yaw, held = true };

        /// <summary>A steady new look target: held for longer than the hold time.</summary>
        static void Hold(ref QuadTurn t, float to) { for (int i = 0; i < 12; i++) t.Look(to, Dt); }

        /// <summary>Largest per-frame heading change and the number of times the heading's direction of travel reversed.</summary>
        static (float maxStep, int reversals, float maxRateJump) Run(ref QuadTurn t, int frames, Action<int> feed, float maxRate = QuadTurn.MaxRate)
        {
            float maxStep = 0f, maxJump = 0f, prevStep = 0f, prevRate = t.rate; int rev = 0;
            for (int i = 0; i < frames; i++)
            {
                float before = t.yaw;
                feed(i);
                t.Step(Dt, maxRate);
                float step = QuadTurn.Delta(before, t.yaw);
                maxStep = Math.Max(maxStep, Math.Abs(step));
                maxJump = Math.Max(maxJump, Math.Abs(t.rate - prevRate)); prevRate = t.rate;
                if (Math.Abs(step) > 1e-4f) { if (prevStep != 0f && Math.Sign(step) != Math.Sign(prevStep)) rev++; prevStep = step; }
            }
            return (maxStep, rev, maxJump);
        }

        [TestCase(30f)] [TestCase(90f)] [TestCase(180f)] [TestCase(-170f)]
        public void AStandingTurn_EasesInAndOut_NoSnap_NoOvershoot(float by)
        {
            var t = At(0f); Hold(ref t, by);
            var (maxStep, rev, jump) = Run(ref t, 240, _ => { });
            Assert.LessOrEqual(maxStep, QuadTurn.MaxRate * Dt + 0.01f, "no frame turns faster than the turn rate");
            Assert.LessOrEqual(jump, QuadTurn.Accel * Dt * 2.5f, "the rate eases: no frame changes it more than the acceleration");
            Assert.AreEqual(0, rev, "no back and forth");
            Assert.AreEqual(0f, Math.Abs(QuadTurn.Delta(t.yaw, by)), 0.01f, "it arrives");
            Assert.AreEqual(0f, t.rate, 1e-3f, "and stops, no rate left");
        }

        [Test]
        public void ALookTargetFlickingInsideTheDeadZone_DoesNotMoveTheWolf()
        {
            var t = At(10f);
            var (maxStep, rev, _) = Run(ref t, 300, i => t.Look(10f + ((i & 1) == 0 ? 5f : -5f), Dt));
            Assert.AreEqual(10f, t.yaw, 1e-3f); Assert.AreEqual(0f, maxStep, 1e-4f); Assert.AreEqual(0, rev);
        }

        [Test]
        public void ALookTargetFlippingBetweenTwoFarHeadingsEveryFrame_StaysSmooth()
        {
            var t = At(0f);
            var (maxStep, rev, jump) = Run(ref t, 600, i => t.Look((i & 1) == 0 ? 60f : -60f, Dt));
            Assert.LessOrEqual(maxStep, QuadTurn.MaxRate * Dt + 0.01f);
            Assert.LessOrEqual(jump, QuadTurn.Accel * Dt * 2.5f);
            Assert.AreEqual(0f, t.yaw, 1e-3f, "a flicker that never settles never steers: the wolf stays put");
            Assert.AreEqual(0, rev);
        }

        [Test]
        public void ATinyMove_NearTheGoal_GivesNoHeadingFromNoise()
        {
            // what WolfView does: heading from the direction to the goal, only above MoveOn; the wolf hovers 0.005..0.03 from it, direction random
            var t = At(40f); var rng = new Random(7);
            int moving = 0;
            var (maxStep, rev, _) = Run(ref t, 600, i =>
            {
                float dist = 0.005f + 0.025f * (float)rng.NextDouble();
                bool m = t.Moving(dist);
                if (m) moving++;
                if (m && dist > QuadTurn.MoveOn) t.Look((float)rng.NextDouble() * 360f - 180f, Dt);
            }, QuadTurn.TrotMaxRate);
            Assert.Greater(moving, 0);
            Assert.LessOrEqual(maxStep, QuadTurn.TrotMaxRate * Dt + 0.01f);
            // the old code snapped to a random direction at 300 deg/s and flipped each frame; here every frame still eases
            Assert.LessOrEqual(rev, 40, "reversals only when a real new heading arrives, not each frame");
        }

        [Test]
        public void TheMovingFlag_HasHysteresis()
        {
            var t = At(0f);
            Assert.IsFalse(t.Moving(0.015f), "below MoveOn it does not start");
            Assert.IsTrue(t.Moving(0.03f));
            Assert.IsTrue(t.Moving(0.012f), "once moving it keeps going above MoveOff");
            Assert.IsFalse(t.Moving(0.005f));
        }

        [Test]
        public void ATurnReversedMidWay_BrakesFirst_ThenComesBack_SmoothlyWithoutAJump()
        {
            var t = At(0f); Hold(ref t, 120f);
            var (maxStep, _, jump) = Run(ref t, 20, _ => { });
            Hold(ref t, -120f);
            var r = Run(ref t, 300, _ => { });
            Assert.LessOrEqual(Math.Max(maxStep, r.maxStep), QuadTurn.MaxRate * Dt + 0.01f);
            Assert.LessOrEqual(Math.Max(jump, r.maxRateJump), QuadTurn.Accel * Dt * 2.5f);
            Assert.AreEqual(-120f, t.yaw, 0.01f);
        }
    }
}
