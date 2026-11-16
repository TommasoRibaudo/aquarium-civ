// #692 part 1: the camp uses' timelines (pure builders, CampScenes1) against the spec rows' beats and the D rules (EditMode, no scene).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class CampScenesTests
    {
        static readonly Vector2 Fire = new Vector2(0.2f, -0.3f);

        /// <summary>Every member's parts follow one another without overlapping, and every effect falls inside the scene.</summary>
        static void Sane(CampScene s)
        {
            Assert.Greater(s.Count, 0);
            for (int m = 0; m < s.Count; m++)
            {
                var ps = s.parts[m].OrderBy(p => p.from).ToList();
                for (int i = 0; i < ps.Count; i++)
                {
                    Assert.GreaterOrEqual(ps[i].to, ps[i].from, $"{s.use} member {m} part {i}");
                    if (i > 0) Assert.GreaterOrEqual(ps[i].from, ps[i - 1].to - 1e-3f, $"{s.use} member {m}: parts {i - 1} and {i} overlap");
                }
            }
            foreach (var f in s.fx) Assert.That(f.time, Is.InRange(0f, s.Length + 1e-3f), $"{s.use}: an effect outside the scene");
        }

        static void PoseRuns(CampScene s)
        {
            foreach (var l in s.parts)
                foreach (var p in l)
                    if (p.pose != null)
                        for (float t = 0f; t <= p.Len; t += 0.05f)
                        {
                            var pose = DollPose.Rest;
                            p.pose(ref pose, t, p.Len);
                            Assert.IsFalse(float.IsNaN(pose.lean) || float.IsNaN(pose.armR.x) || float.IsNaN(pose.offset.y), $"{s.use}: NaN pose");
                        }
        }

        [Test]
        public void B29a_WarmHands_ArmsToTheFlamesTheRubAndASteamBreath()
        {
            var s = CampScenes1.WarmHands(5, 10f);
            Sane(s); PoseRuns(s);
            var p = DollPose.Rest; CampScenes1.WarmPose(ref p, 1.0f, CampScenes1.WarmSeconds);
            Assert.AreEqual(75f, p.armR.x, 0.01f, "arms out to the flames, x 75");
            Assert.AreEqual(10f, p.lean, 0.5f, "leaning in 10");
            float lo = 999f, hi = -999f;
            for (float t = 1.6f; t < 2.4f; t += 0.02f) { var q = DollPose.Rest; CampScenes1.WarmPose(ref q, t, CampScenes1.WarmSeconds); lo = Mathf.Min(lo, q.armL.x); hi = Mathf.Max(hi, q.armL.x); }
            Assert.That(hi - lo, Is.InRange(8f, 11f), "the rub swaps x 75 <-> 85");
            Assert.AreEqual(1, s.fx.Count(f => f.kind == CampFxKind.Steam && f.anchor == CampAnchor.Head), "a breath in the cold at the end");
            Assert.IsTrue(s.parts[0].All(x => x.keepSpot), "the sitter stays where they sit");
        }

        [Test]
        public void B29b_KidPoke_PokesAtTheFireThenTheGrownUpTakesTheTwig()
        {
            var adult = Fire + new Vector2(0f, -0.33f);
            foreach (int pokes in new[] { 2, 3 })
            {
                var s = CampScenes1.KidPoke(1, 2, Fire + new Vector2(1f, -1f), adult, Fire, pokes, 0f);
                Sane(s); PoseRuns(s);
                var squat = s.parts[0].First(p => p.pose != null && p.style == DollStyle.Crouch).pos;
                Assert.AreEqual(CampScenes1.KidFromFire, (squat - Fire).magnitude, 0.01f, "squats 0.2 u from the fire");
                Assert.LessOrEqual((squat - adult).magnitude, 0.6f, "D rules: a grown-up within 0.6 u for any fire use");
                Assert.AreEqual(pokes + 1, s.fx.Count(f => f.kind == CampFxKind.Sparks), "sparks at each poke and at the toss");
                Assert.AreEqual(1, s.tips.Count(t => t.kind == CampTipKind.Ember), "the glowing tip");
                Assert.IsTrue(s.parts[1].Any(p => p.prop == PropKind.Twig), "the grown-up takes the twig");
                Assert.IsTrue(s.parts[0].Last().style == DollStyle.Sit, "the kid ends sitting by them");
                foreach (var p in s.parts[0]) Assert.Greater((p.pos - Fire).magnitude, 0.15f, "a kid never touches the flames");
            }
        }

        [Test]
        public void B29c_BerryStick_SwellsDarkensIsBlownOnAndEaten()
        {
            var s = CampScenes1.BerryStick(3, Fire + new Vector2(-0.8f, 0.4f), Fire + new Vector2(0.6f, 0.5f), Fire, 0f);
            Sane(s); PoseRuns(s);
            var berry = s.tips.Where(t => t.kind == CampTipKind.Berry).ToList();
            Assert.AreEqual(1.3f, berry.Max(t => t.grow), 1e-4f, "swells 1 -> 1.3");
            Assert.Greater(berry.Max(t => t.darken), 0.3f, "darkens");
            Assert.AreEqual(2, s.fx.Count(f => f.kind == CampFxKind.Steam), "blown on twice");
            Assert.AreEqual(DollStyle.Laugh, s.parts[0].Last().style, "a grin to end");
        }

        [Test]
        public void B29d_Spit_SticksBarTurningAndTheLiftOff()
        {
            var s = CampScenes1.Spit(1, 2, 3, Fire + Vector2.left, Fire + Vector2.down * 0.3f, Fire + Vector2.right * 0.4f, Fire, Vector2.right, false, 0f, out float roundAt, out var stone);
            Sane(s); PoseRuns(s);
            Assert.AreEqual(2, s.fx.Count(f => f.kind == CampFxKind.Dust), "two forked sticks pushed in");
            Assert.Greater(s.b, s.a + CampScenes1.TurnSeconds - 0.5f, "turned for the first evening hour before the lift");
            int drips = s.fx.Count(f => f.kind == CampFxKind.Flare);
            Assert.That(drips, Is.InRange(7, 9), "a drip every 2 s");
            Assert.Greater(roundAt, s.b);
            Assert.Greater((stone - Fire).magnitude, 0.2f, "the flat stone is off the fire");
        }

        [Test]
        public void B30_PotFillAndOff_CarryBetweenTheTwoSpots()
        {
            var potOffset = new Vector2(0.10f, -0.09f);
            Vector2 hot = Fire + potOffset, cold = CampScenes1.ColdPot(Fire, potOffset);
            Assert.AreEqual(0.15f, (cold - hot).magnitude, 1e-4f, "the cold spot is 0.15 u further out");
            var f = CampScenes1.PotFill(1, 2, Fire + Vector2.left, Fire + Vector2.right, Fire + new Vector2(0.5f, 0.6f), cold, hot, false, 0f, out float c0, out float c1);
            Sane(f); PoseRuns(f);
            Assert.Less(f.a, f.b, "the water before the food");
            Assert.Less(f.b, c0, "both in before the lift");
            Assert.AreEqual(1, f.fx.Count(x => x.kind == CampFxKind.Jolt), "set down: one jolt");
            Assert.IsTrue(f.parts[1].Any(p => p.prop == PropKind.Bucket), "the helper brings a full bucket");
            var o = CampScenes1.PotOff(1, 2, 3, hot + Vector2.down * 0.2f, hot + Vector2.up * 0.2f, Fire + Vector2.left, hot, cold, 0f, out float d0, out float d1);
            Sane(o); PoseRuns(o);
            Assert.Greater(o.b, d1, "the bowls are stacked after the pot is down");
            Assert.IsTrue(o.parts[2].Any(p => p.prop == PropKind.Bowl), "a kid carries the bowls");
        }

        [Test]
        public void B30cd_PeekAndSniff()
        {
            var pot = Fire + new Vector2(0.1f, -0.09f);
            var peek = CampScenes1.PotPeek(4, 1, Fire + Vector2.left, pot + Vector2.down * 0.16f, pot, 0f);
            Sane(peek); PoseRuns(peek);
            Assert.AreEqual(1, peek.fx.Count(x => x.kind == CampFxKind.Steam), "a puff of steam in the face");
            Assert.IsTrue(peek.parts[0].Any(p => p.run), "runs off laughing");
            var sniff = CampScenes1.PotSniff(5, 1, pot + Vector2.left * 0.25f, pot, pot + Vector2.down * 0.16f, 0f);
            Sane(sniff); PoseRuns(sniff);
            Assert.AreEqual(CampScenes1.SniffSeconds, sniff.parts[0][0].Len, 1e-4f);
        }

        [Test]
        public void B30b_TheRoundDipsAtThePotBeforeEachHandOver()
        {
            var takers = new List<int> { 1, 2, 3, 4 };
            var pos = new List<Vector2> { Fire + new Vector2(0.3f, 0f), Fire + new Vector2(0f, 0.3f), Fire + new Vector2(-0.3f, 0f), Fire + new Vector2(0f, -0.3f) };
            var served = new Dictionary<int, float>();
            var dips = new List<Vector2>();
            var pot = Fire + new Vector2(0.1f, -0.09f);
            var events = MealStage.Pass(9, 1f, 9, pot + new Vector2(0.1f, -0.1f), Fire, pot, takers, pos, served, dips, PropKind.Bowl, CampScenes1.DipSeconds);
            Assert.AreEqual(events.Count, dips.Count, "one dip per bowl");
            for (int i = 0; i < events.Count; i++)
            {
                Assert.LessOrEqual(dips[i].y, events[i].start + 1e-3f, "the dip comes before its hand-over");
                Assert.GreaterOrEqual(dips[i].y - dips[i].x, CampScenes1.DipSeconds - 1e-3f);
                if (i > 0) Assert.GreaterOrEqual(dips[i].x, events[i - 1].End - 1e-3f, "after the last bowl was handed");
            }
        }

        [Test]
        public void B31_FeatherTieAndTheDare()
        {
            var pole = new Vector2(-1f, 0.5f);
            var tie = CampScenes1.FeatherTie(1, new List<int> { 2, 3 }, 4, pole + Vector2.down, new List<Vector2> { pole + Vector2.left, pole + Vector2.right }, pole + Vector2.up, pole, pole + Vector2.down, 0f);
            Sane(tie); PoseRuns(tie);
            Assert.IsTrue(tie.parts[0].Any(p => p.style == DollStyle.HoldUp && p.prop == PropKind.Feather), "the chief holds the feather up");
            Assert.AreEqual(3, tie.parts.Take(3).Count(l => l.Any(p => p.style == DollStyle.Pray)), "all pray");
            Assert.IsTrue(tie.parts[3].Any(p => p.style == DollStyle.Point), "a kid points at the new one");
            var kids = new List<int> { 5, 6, 7 };
            var at = new List<Vector2> { pole + new Vector2(0.5f, -0.5f), pole + new Vector2(0.7f, -0.4f), pole + new Vector2(0.4f, -0.8f) };
            var dare = CampScenes1.PoleDare(kids, at, 8, pole + Vector2.left * 0.8f, pole, pole + Vector2.down, 0f);
            Sane(dare); PoseRuns(dare);
            Assert.AreEqual(2, dare.fx.Count(f => f.kind == CampFxKind.Jolt), "two darers touch it");
            Assert.Greater(dare.b, dare.a);
            Assert.IsTrue(dare.parts[3].Any(p => p.style == DollStyle.Look), "a grown-up turns to look");
        }
    }
}
