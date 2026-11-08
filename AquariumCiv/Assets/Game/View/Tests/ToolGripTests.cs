using AquariumCiv.Life;
using AquariumCiv.View;
using NUnit.Framework;
using UnityEngine;
using PropSlot = AquariumCiv.View.PropSlot;

namespace AquariumCiv.View.Tests
{
    /// <summary>#401 (playtest: "hoes held the other way round", chopping kindling held wrong): a swung tool leads with its working
    /// side and lands on the ground in front, and a tool shows only in the move that uses it.</summary>
    public class ToolGripTests
    {
        [TestCase(PropKind.Hoe)]
        [TestCase(PropKind.Axe)]
        [TestCase(PropKind.Hammer)]
        public void ASwungTool_LeadsWithItsWorkingSide(PropKind kind)
        {
            // The mesh's working side is -z (the axe's edge, the hoe's blade); in the arm's frame +z leads the downstroke
            // (forward with the arm overhead, down with it level).
            var side = DollMotion.ToolGrip(kind) * Vector3.back;
            Assert.Greater(side.z, 0.5f, $"{kind}: the working side faces the way the stroke goes, not back up the swing");
            var handle = DollMotion.ToolGrip(kind) * Vector3.up;
            Assert.Less(handle.y, -0.5f, $"{kind}: the handle runs on from the fist, down the arm");
        }

        [TestCase(PropKind.Hoe, DollStyle.Hoe)]
        [TestCase(PropKind.Axe, DollStyle.Chop)]
        [TestCase(PropKind.Hammer, DollStyle.Hammer)]
        public void TheStroke_LandsAtTheGround_NotUnderIt(PropKind kind, DollStyle style)
        {
            var root = new GameObject("ToolGripTest") { hideFlags = HideFlags.DontSave };
            try
            {
                var doll = PegDollView.Create(root.transform, null, PegDoll.PickLook(7, true, false, -1), false, 7);
                doll.SetProp(kind, PropSlot.Hand);
                float lowest = float.MaxValue, highest = float.MinValue;
                int hits = 0;
                for (float t = 0f; t < 3f; t += 1f / 60f)
                {
                    var p = DollMotion.Evaluate(style, t, 1f, 0, 1, PropSlot.Hand, true);
                    if (p.hit < 0.5f) continue;
                    doll.ApplyArms(p);
                    doll.transform.SetLocalPositionAndRotation(p.offset, Quaternion.Euler(-p.lean, 0f, p.roll));
                    float y = WorkHitPoint.HitPoint(doll).y - root.transform.position.y;
                    lowest = Mathf.Min(lowest, y); highest = Mathf.Max(highest, y);
                    hits++;
                }
                Assert.Greater(hits, 0, "the loop strikes");
                // Before #401 the hoe's blade went 0.1 u into the soil and the axe's head 0.05 u: the stroke vanished on the hit.
                Assert.Greater(lowest, -0.03f, $"{kind}: the working end stays at the ground on the hit (lowest {lowest:0.000})");
                Assert.Less(highest, 0.07f, $"{kind}: and reaches down to it, not chopping the air (highest {highest:0.000})");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void WeedingWithAHoe_IsTheHoesSwing_ButWithABasket_StaysAPick()
        {
            var pick = DollMotion.StyleOf(ActivityKind.Weed);
            Assert.AreEqual(DollStyle.Hoe, DollMotion.ToolStyle(pick, ActivityKind.Weed, PropKind.Hoe));
            Assert.AreEqual(DollStyle.Pick, DollMotion.ToolStyle(pick, ActivityKind.Weed, PropKind.Basket));
            Assert.AreEqual(DollStyle.Walk, DollMotion.ToolStyle(DollStyle.Walk, ActivityKind.Weed, PropKind.Hoe), "walking is walking");
            Assert.AreEqual(DollStyle.Pick, DollMotion.ToolStyle(pick, ActivityKind.Gather, PropKind.Hoe), "only weeding");
        }

        [Test]
        public void TheHarvest_IsPickedByHand_NoToolFlailing()
        {
            var (kind, _) = PeopleView.PropOf(Prop.Scythe, ActivityInfo.SlotOf(Prop.Scythe, ActivityKind.Harvest), ActivityKind.Harvest);
            Assert.AreEqual(PropKind.None, kind, "the crouched pick holds no blade");
            var (hoe, _) = PeopleView.PropOf(Prop.Hoe, ActivityInfo.SlotOf(Prop.Hoe, ActivityKind.Hoe), ActivityKind.Hoe);
            Assert.AreEqual(PropKind.Hoe, hoe, "the hoer keeps the hoe");
        }
    }
}
