// #791: the run-in play tests' "never jumps" metric. A jump is a step further than the frame's own time explains: 0.1 u in a frame of
// the resting 30 fps (3 u/s), that speed over a longer frame. An Editor hitch carries a longer step of the same walk; a teleport fails.
using NUnit.Framework;

namespace AquariumCiv.View.Tests
{
    public class DollJumpMetricTests
    {
        const float Walk = 0.83f; // u/s: the walk seen in the Town run-in that tripped the old fixed per-frame limit

        [Test]
        public void AWalkThroughAnEditorHitch_IsNoJump()
        {
            float dt = 0.155f, step = Walk * dt; // 0.129 u in one 155 ms frame
            Assert.Greater(step, PeopleView.JumpStep, "the old fixed limit called this a jump");
            Assert.IsFalse(PeopleView.IsJump(step, dt));
        }

        [Test]
        public void AWalkAtTheGamesPace_IsNoJump()
        {
            Assert.IsFalse(PeopleView.IsJump(Walk / 30f, 1f / 30f));
            Assert.IsFalse(PeopleView.IsJump(Walk / 60f, 1f / 60f));
            Assert.IsFalse(PeopleView.IsJump(2.2f / 30f, 1f / 30f), "the run cap is under the jump speed");
        }

        [Test]
        public void ATeleport_IsAJump_InANormalFrameAndInAHitch()
        {
            Assert.IsTrue(PeopleView.IsJump(0.3f, 1f / 60f), "0.3 u in a 60 fps frame");
            Assert.IsTrue(PeopleView.IsJump(0.11f, 1f / 30f), "the same 0.1 u limit in a resting frame");
            Assert.IsTrue(PeopleView.IsJump(0.129f + 0.4f, 0.155f), "a hitch frame's walk plus a 0.4 u teleport");
            Assert.IsTrue(PeopleView.IsJump(0.8f, 0.25f), "the longest tick (dt is clamped at 0.25 s)");
        }
    }
}
