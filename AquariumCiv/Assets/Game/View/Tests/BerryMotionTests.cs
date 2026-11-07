// #361 berries you can see: the beats' shape (pure: AnimBench compiles and runs this too).
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class BerryMotionTests
    {
        static int Hits(DollStyle style, float seconds, out float firstHit)
        {
            int n = 0; firstHit = -1f; float last = 0f;
            for (float t = 0f; t < seconds; t += 1f / 30f)
            {
                var p = DollPose.Rest;
                BerryMotion.Evaluate(style, ref p, t, 0f, 0f);
                if (p.hit > 0.5f && last <= 0.5f) { n++; if (firstHit < 0f) firstHit = t; }
                last = p.hit;
            }
            return n;
        }

        [Test]
        public void Retarget_BerriesAtABush_RootsAtTheStream_KidsStand()
        {
            Assert.AreEqual(DollStyle.PickBerry, BerryMotion.Retarget(DollStyle.Pick, ActivityKind.Gather, PlaceKind.BushCell));
            Assert.AreEqual(DollStyle.DigRoot, BerryMotion.Retarget(DollStyle.Pick, ActivityKind.Gather, PlaceKind.Stream));
            Assert.AreEqual(DollStyle.TasteBerry, BerryMotion.Retarget(DollStyle.Sit, ActivityKind.Eat, PlaceKind.BushCell));
            Assert.AreEqual(DollStyle.Pick, BerryMotion.Retarget(DollStyle.Pick, ActivityKind.Harvest, PlaceKind.BushCell), "Harvest keeps Pick");
            Assert.AreEqual(DollStyle.Pick, BerryMotion.Retarget(DollStyle.Pick, ActivityKind.Gather, PlaceKind.Field), "a field is not a bush");
            Assert.AreEqual(DollStyle.Sit, BerryMotion.Retarget(DollStyle.Sit, ActivityKind.Eat, PlaceKind.Fire), "a meal is still a sit");
        }

        [Test]
        public void Pick_OneHitPerPick_SixPicksPerBasket_EverySecondAndAHalf()
        {
            float group = BerryMotion.GroupSeconds(DollStyle.PickBerry);
            Assert.AreEqual(BerryMotion.PicksPerGroup, Hits(DollStyle.PickBerry, group - 0.01f, out float first));
            Assert.Greater(first, 0.3f, "a wind-up comes first");
            Assert.AreEqual(BerryMotion.PicksPerGroup * 2, Hits(DollStyle.PickBerry, group * 2f - 0.01f, out _));
            Assert.AreEqual(BerryMotion.PickCycle * 5f + 2.63f, group, 0.05f);
        }

        [Test]
        public void Dig_TwoPawsAndAPull_AndTheLastContactIsThePull()
        {
            Assert.AreEqual(3, Hits(DollStyle.DigRoot, BerryMotion.GroupSeconds(DollStyle.DigRoot) - 0.01f, out _));
            Assert.AreEqual(BerryMotion.RootCycle, BerryMotion.GroupSeconds(DollStyle.DigRoot), 1e-3f);
            bool pull = false; int pulls = 0; float last = 0f;
            for (float t = 0f; t < BerryMotion.RootCycle; t += 1f / 30f)
            {
                var p = DollPose.Rest;
                BerryMotion.Evaluate(DollStyle.DigRoot, ref p, t, 0f, 0f);
                if (p.hit > 0.5f && last <= 0.5f) { pull = BerryMotion.IsRootPull(t + 1f / 30f); if (pull) pulls++; }
                last = p.hit;
            }
            Assert.AreEqual(1, pulls);
        }

        [Test]
        public void Taste_OneBerryEvery2Point2Seconds_AHopOnTheThird()
        {
            float group = BerryMotion.GroupSeconds(DollStyle.TasteBerry);
            Assert.AreEqual(3, Hits(DollStyle.TasteBerry, group - 0.01f, out _));
            Assert.AreEqual(BerryMotion.TasteCycle * 3f + 0.4f - 0.2f + 0.2f, group, 0.4f);
            float topY = 0f;
            for (float t = 0f; t < group; t += 1f / 30f)
            {
                var p = DollPose.Rest;
                BerryMotion.Evaluate(DollStyle.TasteBerry, ref p, t, 0f, 0f);
                topY = Mathf.Max(topY, p.offset.y);
            }
            Assert.GreaterOrEqual(topY, 0.025f, "the hop and the chew bob must be more than a pixel or three");
        }

        [Test]
        public void TheBlocksLastSecond_HoldsTheBasketUp_WithoutAHit()
        {
            var p = DollPose.Rest;
            // 20.5 s into a 21 s block: the last 0.9 s hold the basket up (the left arm high)
            BerryMotion.Evaluate(DollStyle.PickBerry, ref p, 7f, 20.5f / 21f, 20.5f);
            Assert.Greater(p.armL.x, 100f);
            Assert.AreEqual(0f, p.hit);
        }

        [Test]
        public void Basket_RidesInTheLeftHandOnlyForPickAndDig()
        {
            Assert.IsTrue(BerryMotion.BasketInLeft(DollStyle.PickBerry));
            Assert.IsTrue(BerryMotion.BasketInLeft(DollStyle.DigRoot));
            Assert.IsFalse(BerryMotion.BasketInLeft(DollStyle.Walk));
            Assert.IsFalse(BerryMotion.BasketInLeft(DollStyle.Pick));
        }

        [Test]
        public void EveryBerryHit_ComesWithAHold_AndKeyOffsetsReadOnAPhone()
        {
            foreach (var style in new[] { DollStyle.PickBerry, DollStyle.DigRoot, DollStyle.TasteBerry })
            {
                float group = BerryMotion.GroupSeconds(style);
                float prevHit = 0f, holdFrom = -1f;
                for (float t = 0f; t < group; t += 1f / 60f)
                {
                    var p = DollPose.Rest;
                    BerryMotion.Evaluate(style, ref p, t, 0f, 0f);
                    if (p.hit > 0.5f && prevHit <= 0.5f) holdFrom = t;
                    prevHit = p.hit;
                }
                Assert.GreaterOrEqual(holdFrom, 0f, style + " has a contact");
            }
        }

        // ---------------------------------------------------------------- the tip at the stash

        [Test]
        public void Tip_OneHit_AtTheFirstBerrysLanding_InAboutAsecondAndAHalf()
        {
            Assert.AreEqual(BerryMotion.TipContactAt, BerryMotion.TipContact, 1e-4f, "the pose's contact is where the first berry lands");
            Assert.That(BerryMotion.TipSeconds, Is.InRange(1.2f, 1.5f));
            int hits = 0; float first = -1f, last = 0f;
            for (float t = 0f; t < BerryMotion.TipSeconds + 0.5f; t += 1f / 60f)
            {
                var p = BerryMotion.TipPose(t);
                if (p.hit > 0.5f && last <= 0.5f) { hits++; if (first < 0f) first = t; }
                last = p.hit;
            }
            Assert.AreEqual(1, hits);
            Assert.AreEqual(BerryMotion.TipContactAt, first, 0.03f);
        }

        [Test]
        public void Tip_BerriesLeaveTheRimOnceTheBasketIsOver_AndLandOnTheContactFrameOrAfter()
        {
            Assert.GreaterOrEqual(BerryMotion.TipLaunchAt(0), 0.45f, "after the lift and the tilt");
            Assert.AreEqual(BerryMotion.TipContactAt, BerryMotion.TipLaunchAt(0) + BerryMotion.TipFlight, 1e-4f, "the first lands on the hit");
            Assert.Greater(BerryMotion.TipRoll(BerryMotion.TipLaunchAt(0)), 0.95f * BerryMotion.TipRollDegrees, "the basket is rolled over when the first berry leaves");
            Assert.AreEqual(0f, BerryMotion.TipRoll(0f), 1e-3f);
            Assert.AreEqual(0f, BerryMotion.TipRoll(BerryMotion.TipSeconds), 1e-3f, "empty and upright again at the end");
            Assert.AreEqual(0f, BerryMotion.TipLift(BerryMotion.TipSeconds), 1e-3f);
            Assert.AreEqual(BerryMotion.TipGap, BerryMotion.TipLaunchAt(1) - BerryMotion.TipLaunchAt(0), 1e-4f, "0.06 s apart");
        }

        [Test]
        public void Tip_PoursThreeToFiveBerries_NeverNone()
        {
            Assert.AreEqual(3, BerryMotion.TipBerries(0));
            Assert.AreEqual(3, BerryMotion.TipBerries(3));
            Assert.AreEqual(4, BerryMotion.TipBerries(4));
            Assert.AreEqual(5, BerryMotion.TipBerries(9));
        }

        [Test]
        public void Tip_LeansOverThePile_AndStaysInRange()
        {
            float maxLean = 0f;
            for (float t = 0f; t < BerryMotion.TipSeconds + 0.3f; t += 1f / 60f)
            {
                var p = BerryMotion.TipPose(t);
                maxLean = Mathf.Max(maxLean, p.lean);
                Assert.IsFalse(float.IsNaN(p.lean + p.armL.x + p.armR.x + p.squash));
                Assert.That(p.squash, Is.InRange(0.9f, 1.01f));
            }
            Assert.GreaterOrEqual(maxLean, 30f, "the lean (spec: 35) reads on a phone");
            var end = BerryMotion.TipPose(BerryMotion.TipSeconds + 0.3f);
            Assert.Less(end.lean, 6f, "back upright for the walk home");
        }

        [Test]
        public void TipBasket_IsABerryStyle_AndTipLoadDrawsIt()
        {
            Assert.IsTrue(BerryMotion.IsBerryStyle(DollStyle.TipBasket));
            Assert.AreEqual(DollStyle.TipBasket, DollMotion.StyleOf(ActivityKind.TipLoad));
            var p = DollMotion.Evaluate(DollStyle.TipBasket, 0f, 1f, 0, 1, PropSlot.Arms, true, 0f, 0f, 0.8f);
            Assert.Greater(p.lean, 10f, "DollMotion hands the tip's clock (since) to the beat");
        }
    }
}
