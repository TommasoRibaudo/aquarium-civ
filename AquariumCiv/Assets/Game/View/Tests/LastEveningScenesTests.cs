using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>
    /// #789 (animation-specs B82.a): the last evening's timeline, pure (EditMode): the beats in order (gather at least 8 s, the hand, the stars with
    /// the caption, rising, home), the kin close round the elder on the camera's side (the partner beside them), the walk home at the elder's own slow
    /// pace with a helper each side, the elder in at their door at the end and everyone else back at the fire sitting, no gap in anyone's parts, the
    /// dog at the feet and then at the door, at most four kin and the kid only with a grown-up.
    /// </summary>
    public class LastEveningScenesTests
    {
        static readonly Vector2 Fire = new Vector2(0.1f, 0.8f), ToCam = new Vector2(-0.47f, -0.88f), Door = new Vector2(1.1f, 0.3f);

        static LastEveningCast Cast(int grown, int kids, bool partner = true, bool dog = false)
        {
            var c = new LastEveningCast { elder = 1, elderAt = Fire + new Vector2(0.6f, -0.2f), fire = Fire, toCam = ToCam, door = Door };
            for (int i = 0; i < grown + kids; i++) { c.kin.Add(10 + i); c.kinAt.Add(Fire + new Vector2(-1f + 0.25f * i, -0.6f)); c.kinKid.Add(i >= grown); }
            if (partner && grown > 0) c.partner = 0;
            if (dog) { c.dog = 5; c.dogAt = Fire + new Vector2(0.3f, 0.3f); }
            return c;
        }

        static CampPart PartAt(CampScene s, int key, float u) { s.TryPart(key, s.t0 + u, out var p, out _); return p; }

        [Test]
        public void TheBeats_InOrder_TheCaptionAtTheStars()
        {
            var s = LastEveningScenes.Evening(Cast(2, 1), 0f);
            Assert.AreEqual(CampUse.LastEvening, s.use);
            Assert.GreaterOrEqual(s.a, LastEveningScenes.GatherMin, "the gather takes at least 8 s");
            Assert.AreEqual(s.a + LastEveningScenes.HandFor, s.b, 1e-4f, "the stars 8 s after the hand");
            Assert.Greater(s.d, s.b + LastEveningScenes.LookFor, "the rising after the stars' hold");
            Assert.Greater(s.c, s.d, "in at the door after the walk home");
            Assert.Greater(s.Length, s.c, "the kin come back to the fire after");
            Assert.AreEqual(4, s.Count, "the elder and three kin");
        }

        [Test]
        public void TheKin_SitInClose_OnTheCamerasSide_ThePartnerBesideThem()
        {
            var s = LastEveningScenes.Evening(Cast(3, 1), 0f);
            var seat = LastEveningScenes.SeatOf(Fire, ToCam);
            var elder = PartAt(s, 1, s.a + 1f);
            Assert.AreEqual(DollStyle.Sit, elder.style);
            Assert.Less((elder.pos - seat).magnitude, 1e-3f, "the elder on the seat by the fire");
            Assert.Less((seat - Fire).magnitude, LastEveningScenes.SeatR + 1e-3f);
            for (int i = 0; i < 4; i++)
            {
                var p = PartAt(s, 10 + i, s.a + 1f);
                Assert.AreEqual(DollStyle.Sit, p.style, $"kin {i} sits");
                float d = (p.pos - seat).magnitude;
                if (i == 0) Assert.AreEqual(LastEveningScenes.PartnerR, d, 1e-3f, "the partner right beside them");
                else if (i == 3) Assert.Less(d, 0.1f, "the kid at the knee");
                else { Assert.GreaterOrEqual(d, 0.12f - 1e-3f); Assert.LessOrEqual(d, 0.2f + 1e-3f, "within 0.12 to 0.2 u"); Assert.Greater(Vector2.Dot(p.pos - seat, ToCam.normalized), -0.05f, "on the camera's half"); }
            }
        }

        [Test]
        public void Home_AtTheEldersOwnPace_AHelperEachSide_InAtTheDoor_TheOthersBackAtTheFire()
        {
            var s = LastEveningScenes.Evening(Cast(2, 1), 0f);
            float mid = (s.d + s.c) * 0.5f - 1.5f;
            var e = PartAt(s, 1, mid);
            Assert.AreEqual(LastEveningScenes.Walk * LastEveningScenes.FrailPace, e.speed, 1e-4f, "the frail walk, about 0.4 of the usual");
            Assert.IsTrue(e.poseWalking, "the stoop while walking");
            for (int i = 0; i < 2; i++)
            {
                var h = PartAt(s, 10 + i, mid);
                Assert.IsTrue(h.poseWalking && h.pose != null, $"helper {i} at the elbow while walking");
                Assert.AreEqual(e.to - e.from, h.to - h.from, 1e-3f, "the same stretch of time as the elder's");
                Assert.AreEqual(LastEveningScenes.Beside, (h.pos - e.pos).magnitude, 0.01f, "at the elder's side at the door");
            }
            var inDoor = PartAt(s, 1, s.c + 0.1f);
            Assert.AreEqual(DollStyle.Hidden, inDoor.style, "in at their door");
            Assert.Less((inDoor.pos - Door).magnitude, 1e-3f);
            for (int i = 0; i < 3; i++)
            {
                var p = s.parts[s.IndexOf(10 + i)].OrderBy(x => x.to).Last();
                Assert.Greater(p.from, s.c, $"kin {i}'s last part comes after the elder went in");
                Assert.AreEqual(DollStyle.Sit, p.style, $"kin {i} sits at the fire again");
                Assert.Less((p.pos - Fire).magnitude, 0.6f, $"kin {i} back by the fire");
            }
        }

        [Test]
        public void NoGaps_InAnyonesParts()
        {
            foreach (var c in new[] { Cast(1, 0), Cast(2, 2, dog: true), Cast(4, 0, partner: false), Cast(3, 1, dog: true) })
            {
                var s = LastEveningScenes.Evening(c, 0f);
                for (int m = 0; m < s.Count; m++)
                {
                    var parts = s.parts[m].Where(p => p.to > p.from + 1e-4f).OrderBy(p => p.from).ToList();
                    Assert.AreEqual(0f, parts[0].from, 1e-3f, $"member {m} from the start");
                    for (int i = 1; i < parts.Count; i++) Assert.LessOrEqual(parts[i].from, parts[i - 1].to + 1e-3f, $"member {m}: a gap at {parts[i - 1].to:0.00}");
                }
            }
        }

        [Test]
        public void AtMostFourKin_AndAKidNeverWithoutAGrownUp()
        {
            var s = LastEveningScenes.Evening(Cast(4, 2), 0f);
            Assert.AreEqual(5, s.Count, "the elder and four");
            var onlyKids = Cast(0, 2);
            var k = LastEveningScenes.Evening(onlyKids, 0f);
            Assert.IsNull(PartAt(k, 10, k.a + 2f) is CampPart p && p.pose != null && p.style == DollStyle.Sit && (p.pos - LastEveningScenes.SeatOf(Fire, ToCam)).magnitude < 0.1f ? p : null,
                "no kid leans on the knee without a grown kin there (CampLife drops the kids then; the builder never seats one at the knee)");
        }

        [Test]
        public void TheDog_LiesAcrossTheFeet_ThumpsItsTail_AndLiesAtTheDoorAfter()
        {
            var s = LastEveningScenes.Evening(Cast(2, 0, dog: true), 0f);
            Assert.AreEqual(5, s.dog);
            var atHand = s.QuadAt(0, s.a + 1f);
            Assert.AreEqual(QuadAct.Lie, atHand.act);
            Assert.Less((atHand.pos - LastEveningScenes.SeatOf(Fire, ToCam)).magnitude, 0.1f, "at the elder's feet");
            Assert.AreEqual(QuadAct.Wag, s.QuadAt(0, s.a + 3.5f).act, "the tail thumps");
            var after = s.QuadAt(0, s.c + 0.5f);
            Assert.AreEqual(QuadAct.Lie, after.act);
            Assert.Less((after.pos - Door).magnitude, 0.15f, "lies down at the door");
        }

        [Test]
        public void TheNewPoses_Move()
        {
            foreach (var name in new[] { "HandOnShoulderL", "HandOnShoulderR", "LeanOnKneeL", "LeanOnKneeR", "HelpUpL", "HelpUpR", "HelpedUp", "PairWalkL", "PairWalkR", "HoldHandL", "HoldHandR", "FrailWalk", "PatDog", "ElderAtTheFire" })
            {
                var f = LastEveningScenes.PoseOf(name);
                Assert.IsNotNull(f, name);
                var a = DollPose.Rest; var b = DollPose.Rest;
                f(ref a, 0f, 8f); f(ref b, 3f, 8f);
                if (name == "PatDog") { a = DollPose.Rest; f(ref a, 0f, 0.6f); b = DollPose.Rest; f(ref b, 0.3f, 0.6f); }
                float moved = (a.armL - b.armL).magnitude + (a.armR - b.armR).magnitude + Mathf.Abs(a.lean - b.lean) + Mathf.Abs(a.roll - b.roll) + Mathf.Abs(a.squash - b.squash) * 100f + Mathf.Abs(a.yaw - b.yaw);
                if (name == "FrailWalk") moved += Mathf.Abs(a.lean) > 1f ? 10f : 0f; // a stoop held the whole walk, a sway on top
                Assert.Greater(moved, 5f, name + " moves");
            }
            Assert.IsNull(LastEveningScenes.PoseOf("nope"));
        }
    }
}
