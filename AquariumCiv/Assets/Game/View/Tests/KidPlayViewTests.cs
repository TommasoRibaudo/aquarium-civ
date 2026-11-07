using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#358: the kids' ball is drawn (it fell to PropKind.None), and the play styles have their own moves.</summary>
    public class KidPlayViewTests
    {
        [Test]
        public void TheBall_IsAPropHuggedInBothArms_WithAMesh()
        {
            var (kind, slot) = PeopleView.PropOf(AquariumCiv.Life.Prop.Ball, AquariumCiv.Life.PropSlot.Hand, ActivityKind.Play);
            Assert.AreEqual(PropKind.Ball, kind);
            Assert.AreEqual(PropSlot.Arms, slot);
            var mesh = PegDoll.PropMesh(PropKind.Ball);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.bounds.extents.x, 0.03f, "oversized: bigger than a kid's head");
        }

        [Test]
        public void ToddleAndDance_NoLongerTheHopCircle()
        {
            Assert.AreEqual(DollStyle.Toddle, DollMotion.StyleOf(ActivityKind.Toddle));
            Assert.AreEqual(DollStyle.RingDance, DollMotion.StyleOf(ActivityKind.Dance));
            var toddle = DollMotion.Evaluate(DollStyle.Toddle, 0.1f, 1f, 0, 1, PropSlot.Hand, false);
            Assert.Greater(Mathf.Abs(toddle.roll), 1f, "a wobble");
            Assert.AreEqual(0f, toddle.offset.x, 1e-5f, "no circle");
        }
    }
}
