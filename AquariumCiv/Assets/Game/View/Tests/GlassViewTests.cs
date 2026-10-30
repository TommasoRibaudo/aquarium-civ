using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using LifeProp = AquariumCiv.Life.Prop;

namespace AquariumCiv.View.Tests
{
    /// <summary>#92's pure view parts (EditMode): every glass and relic activity has its own move, the relic props are
    /// drawn, the tap-back knocks exactly as often as the Keeper tapped, the front glass faces out of its wall.</summary>
    public class GlassViewTests
    {
        static readonly ActivityKind[] GlassKinds =
        {
            ActivityKind.GazeWary, ActivityKind.PressHands, ActivityKind.WaveWary, ActivityKind.Flinch, ActivityKind.Peek, ActivityKind.CreepUp,
            ActivityKind.HopBack, ActivityKind.LaughPoint, ActivityKind.Measure, ActivityKind.TapBack, ActivityKind.WriteDown, ActivityKind.TurnLens,
            ActivityKind.LookThroughLens, ActivityKind.Freeze, ActivityKind.StepBack, ActivityKind.Poke, ActivityKind.Taste, ActivityKind.Deliver,
            ActivityKind.SetUp, ActivityKind.Bow, ActivityKind.CopyPose, ActivityKind.PickUp, ActivityKind.HoldUp, ActivityKind.Give, ActivityKind.PutOn,
            ActivityKind.LopsidedDance, ActivityKind.Gaze,
        };

        [Test]
        public void EveryGlassAndRelicActivityHasAMove()
        {
            foreach (var k in GlassKinds)
                Assert.AreNotEqual(DollStyle.Stand, DollMotion.StyleOf(k), $"{k} is drawn as more than standing");
            Assert.AreEqual(DollStyle.Stand, DollMotion.StyleOf(ActivityKind.Queue), "waiting in the queue is standing");
            Assert.AreEqual(DollStyle.LookUpWary, DollMotion.StyleOf(ActivityKind.LookUpWary), "the camp's wary look");
        }

        [Test]
        public void TheRelicPropsAreDrawn()
        {
            foreach (var (p, want) in new[] { (LifeProp.Crumb, PropKind.Crumb), (LifeProp.Mask, PropKind.Mask), (LifeProp.Doll, PropKind.Doll),
                         (LifeProp.Sandal, PropKind.Sandal), (LifeProp.Tripod, PropKind.Tripod), (LifeProp.Lens, PropKind.Lens),
                         (LifeProp.Slate, PropKind.Slate), (LifeProp.MeasuringRod, PropKind.MeasuringRod) })
            {
                var (kind, _) = PeopleView.PropOf(p, ActivityInfo.SlotOf(p, ActivityKind.Watch), ActivityKind.Watch);
                Assert.AreEqual(want, kind, $"{p}");
                var mesh = PegDoll.PropMesh(kind);
                Assert.IsNotNull(mesh, $"{kind} has a mesh");
                Assert.Greater(mesh.vertexCount, 8, $"{kind} is more than a speck");
            }
            Assert.AreEqual(PropSlot.Head, PeopleView.PropOf(LifeProp.Mask, ActivityInfo.SlotOf(LifeProp.Mask, ActivityKind.Dance), ActivityKind.Dance).slot, "the mask is worn");
            Assert.AreEqual(PropSlot.Arms, PeopleView.PropOf(LifeProp.Slate, LifeSlotHand, ActivityKind.WriteDown).slot, "the slate is hugged while writing");
        }

        static AquariumCiv.Life.PropSlot LifeSlotHand => AquariumCiv.Life.PropSlot.Hand;

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void TheTapBackKnocksExactlyAsOften(int count)
        {
            int hits = 0;
            bool was = false;
            for (float s = 0f; s < 6f; s += 0.01f)
            {
                var p = DollMotion.Evaluate(DollStyle.Knock, s, 1f, 0, 1, PropSlot.Hand, false, 0f, 0f, s, count);
                bool hit = p.hit > 0f;
                if (hit && !was) hits++;
                was = hit;
            }
            Assert.AreEqual(count, hits, "one knock per tap, then still");
        }

        [Test]
        public void TheGlassNormalPointsOutOfItsWall()
        {
            Assert.AreEqual(Vector2.down, PeopleView.GlassNormal(0), "-z");
            Assert.AreEqual(Vector2.right, PeopleView.GlassNormal(1), "+x");
            Assert.AreEqual(Vector2.up, PeopleView.GlassNormal(2), "+z");
            Assert.AreEqual(Vector2.left, PeopleView.GlassNormal(3), "-x");
            Assert.AreEqual(0, LifePlanner.GlassSideFacing(28f), "BoxCamera's default looks in through side 0");
        }
    }
}
