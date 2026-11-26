// #358 Kids' play: pure tests of the play group's games (NUnit 3 classic asserts; UnityEngine maths only), so Tools/AnimBench compiles and
// runs them too (Tools/AnimBench.Tests), and Unity runs them in the View EditMode tests.
using System.Collections.Generic;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class KidPlayTests
    {
        static readonly int[] Four = { 11, 22, 33, 44 }, Three = { 11, 22, 33 };

        [Test]
        public void Tag_TheTouchLandsAtTouchDistance_AndTheTouchedOneChasesNext()
        {
            var tag = new TagGame(358, 2f, Four, Vector2.zero);
            for (int r = 0; r < 12; r++)
            {
                if (r % tag.TagsPerSet == tag.TagsPerSet - 1) continue; // the set's last tag: the next round starts after the rest
                int c = tag.ChaserOf(r), g = tag.TargetOf(r);
                Assert.AreNotEqual(c, g, $"round {r}");
                float at = tag.ContactAt(r) + 0.01f;
                var cc = tag.Cue(c, at); var gc = tag.Cue(g, at);
                Assert.AreEqual(ChaseGroup.TouchDistance, (cc.pos - gc.pos).magnitude, 0.01f, $"round {r}: P1, the touch within 0.09 u");
                Assert.AreEqual(g, cc.hitOn, $"round {r}: the chaser's contact lands on its runner");
                Assert.AreEqual(DollStyle.Flinch, gc.style, $"round {r}: the touched runner answers on the contact");
                Assert.AreEqual(g, tag.ChaserOf(r + 1), $"round {r}: the touch swaps who's it");
            }
        }

        [Test]
        public void Tag_TheChaserHitsOnceARound_WithAHoldAfter()
        {
            var tag = new TagGame(7, 0f, Four, Vector2.zero);
            int c = tag.ChaserOf(0);
            float t0 = tag.ContactAt(0);
            int hitFrames = 0; float lastHit = -1f;
            for (float t = 0.5f; t < t0 + 1f; t += 1f / 60f)
            {
                var p = tag.PoseOf(c, t, t, 0f, 0f, out _);
                if (p.hit > 0.5f) { hitFrames++; lastHit = t; }
            }
            Assert.Greater(hitFrames, 0, "a hit on the touch");
            Assert.LessOrEqual(hitFrames, 5, "one short contact window");
            var held = tag.PoseOf(c, lastHit + 0.15f, lastHit + 0.15f, 0f, 0f, out _);
            Assert.Greater(held.armR.x, 85f, "P8: the arm stays out (held) after the touch");
        }

        [Test]
        public void Tag_NeverThreeKidsInOneMove()
        {
            var tag = new TagGame(91, 0f, Four, Vector2.zero);
            for (float t = 0f; t < 40f; t += 0.1f)
            {
                var count = new Dictionary<DollStyle, int>();
                for (int i = 0; i < 4; i++)
                {
                    var s = tag.Cue(i, t).style;
                    if (s is DollStyle.Sit or DollStyle.Stand or DollStyle.Run) continue;
                    count[s] = count.TryGetValue(s, out int k) ? k + 1 : 1;
                }
                foreach (var kv in count) Assert.Less(kv.Value, 3, $"t {t:0.0}: {kv.Value} kids {kv.Key}");
            }
        }

        [Test]
        public void Ball_IsNeverInTwoPlaces_AndEveryCatchLandsInTheCatchersArms()
        {
            var ball = new BallGame(4, 1f, Three, Vector2.zero);
            for (float t = 0f; t < 60f; t += 0.05f)
            {
                var b = ball.Ball(t);
                int holding = 0;
                for (int i = 0; i < 3; i++)
                {
                    var c = ball.Cue(i, t);
                    if (c.holdsBall) { holding++; Assert.AreEqual(i, b.holder, $"t {t:0.00}: {i} hugs a ball it doesn't hold"); }
                }
                Assert.AreEqual(b.Free ? 0 : 1, holding, $"t {t:0.00}: the ball is in {holding} pairs of arms (free: {b.Free})");
            }
            int catches = 0, misses = 0;
            for (int k = 0; k < 30; k++)
            {
                var th = ball.ThrowAt(k);
                var start = Arc.Sample(th.shot, th.launch).position;
                Assert.Less(Vector2.Distance(new Vector2(start.x, start.z), th.fromPos), 0.15f, $"throw {k}: leaves from the thrower");
                if (th.miss) { misses++; Assert.Greater(th.pickEnd, th.land, $"throw {k}: picked up after it lands"); continue; }
                catches++;
                var end = Arc.Sample(th.shot, th.land).position;
                Assert.Less(Vector2.Distance(new Vector2(end.x, end.z), th.aim), 0.08f, $"throw {k}: lands in the catcher's arms");
                Assert.AreEqual(th.to, ball.Ball(ball.t0 + th.land + 0.05f).holder, $"throw {k}: the catcher has it");
                float gap = ball.ThrowAt(k + 1).ready - th.ready;
                Assert.GreaterOrEqual(gap, 2.2f - 1e-3f, $"throw {k}: a throw every 2.2-3 s");
            }
            Assert.Greater(catches, misses, "most throws are caught");
            Assert.Greater(misses, 0, "now and then it bounces away");
        }

        /// <summary>#744 (the user, 2026-11-24: options 3 and 4): a missed ball that comes to rest on the pond floats there; the fetcher wades
        /// in (ankle-deep only while on the water), shakes it, then runs back; and no ball ever jumps from the water into the hands.</summary>
        [Test]
        public void Ball_MissIntoThePond_TheFetcherWadesIn_AndShakesIt()
        {
            try
            {
                BallGame.PondAt = p => p.x > 0.05f; // the pond: everything east of x = 0.05
                BallGame.RollsTo = null;
                var ball = new BallGame(4, 0f, Three, Vector2.zero);
                int wet = 0;
                for (int k = 0; k < 40; k++)
                {
                    var th = ball.ThrowAt(k);
                    if (!th.miss || !th.wet) continue;
                    wet++;
                    Assert.IsTrue(BallGame.PondAt(th.restPos), $"throw {k}: it rests on the water");
                    var floating = ball.Ball(ball.t0 + th.land + 0.3f);
                    Assert.IsTrue(floating.Free && floating.placed && floating.floating, $"throw {k}: it floats where it came down");
                    Assert.Less(Vector2.Distance(th.fetchTo, th.restPos), 0.06f, $"throw {k}: the fetcher wades right up to it");
                    var at = ball.Cue(th.fetcher, ball.t0 + th.pickStart + 0.05f);
                    if (BallGame.PondAt(at.pos)) Assert.AreEqual(BallGame.WadeSink, at.sink, 1e-4f, $"throw {k}: ankle-deep in the water");
                    var shake = ball.Cue(th.fetcher, ball.t0 + th.pickEnd - BallGame.Shake * 0.5f);
                    Assert.AreEqual(DollStyle.PlayShake, shake.style, $"throw {k}: it shakes the wet ball");
                    Assert.AreEqual(th.fetcher, ball.Ball(ball.t0 + th.pickEnd).holder, $"throw {k}: then holds it");
                }
                Assert.Greater(wet, 0, "some misses landed in the pond");
            }
            finally { BallGame.PondAt = null; BallGame.RollsTo = null; }
        }

        /// <summary>#744: on dry ground, a missed ball that stops on the side of a hollow rolls on down to its bottom; the fetcher goes there.</summary>
        [Test]
        public void Ball_MissIntoADryHollow_RollsToTheBottom()
        {
            try
            {
                var bottom = new Vector2(0.6f, 0f);
                BallGame.PondAt = null;
                BallGame.RollsTo = p => (p - bottom).magnitude < 0.9f ? bottom : p;
                var ball = new BallGame(4, 0f, Three, Vector2.zero);
                int rolled = 0;
                for (int k = 0; k < 40; k++)
                {
                    var th = ball.ThrowAt(k);
                    if (!th.miss || !th.rolls) continue;
                    rolled++;
                    Assert.AreEqual(bottom, th.restPos, $"throw {k}: it ends at the bottom");
                    var mid = ball.Ball(ball.t0 + (th.rollStart + th.rollEnd) * 0.5f);
                    var midP = new Vector2(mid.position.x, mid.position.z);
                    Assert.IsTrue(mid.placed, $"throw {k}: drawn where the roll says");
                    Assert.Greater(Vector2.Distance(midP, th.rollFrom), 0.01f, $"throw {k}: under way");
                    Assert.Less(Vector2.Distance(midP, bottom), Vector2.Distance(th.rollFrom, bottom), $"throw {k}: toward the bottom");
                    Assert.Less(Vector2.Distance(th.fetchTo, bottom), 0.08f, $"throw {k}: the fetcher picks it up there");
                    Assert.GreaterOrEqual(th.pickStart, th.rollEnd - 0.1f, $"throw {k}: once it has stopped");
                }
                Assert.Greater(rolled, 0, "some misses rolled into the hollow");
            }
            finally { BallGame.PondAt = null; BallGame.RollsTo = null; }
        }

        [Test]
        public void Ball_Alone_TossesAheadAndFetches()
        {
            var ball = new BallGame(2, 0f, new[] { 5 }, Vector2.zero);
            for (int k = 0; k < 6; k++)
            {
                var th = ball.ThrowAt(k);
                Assert.IsTrue(th.miss);
                Assert.AreEqual(0, th.fetcher);
                Assert.Less(th.fromPos.magnitude, 1.2f, $"throw {k}: stays near its spot");
            }
        }

        [Test]
        public void RabbitChase_ContactInsideTheAlertRadius_ThenALaugh_AndNoHerdSaysSo()
        {
            var herd = new Vector2(1f, 0.3f);
            var chase = new RabbitChaseGame(9, 0f, Three, Vector2.zero) { HerdAt = _ => herd };
            var ch = chase.ChaseOf(0);
            Assert.IsTrue(ch.herd);
            var at = chase.Cue(ch.contactBy, ch.contactAt + 0.01f);
            Assert.AreEqual(KidGame.Herd, at.hitOn);
            Assert.AreEqual(RabbitChaseGame.AlertRadius, (at.pos - herd).magnitude, 0.03f, "the contact is the alert radius");
            for (int i = 0; i < 3; i++)
            {
                if (i == ch.contactBy) continue;
                Assert.Greater((chase.Cue(i, ch.contactAt - 0.05f).pos - herd).magnitude, RabbitChaseGame.AlertRadius, $"kid {i} is not in first");
            }
            for (float t = 0f; t < ch.contactAt - 0.05f; t += 0.05f)
                for (int i = 0; i < 3; i++)
                    Assert.Greater((chase.Cue(i, t).pos - herd).magnitude, RabbitChaseGame.AlertRadius, $"t {t:0.00}: kid {i} scares them before the charge");
            int laughing = 0;
            for (int i = 0; i < 3; i++) if (chase.Cue(i, ch.laughAt[i] + 0.1f).style == DollStyle.Laugh) laughing++;
            Assert.AreEqual(3, laughing, "everyone laughs (the end beat)");
            var none = new RabbitChaseGame(9, 0f, Three, Vector2.zero) { HerdAt = _ => null };
            Assert.IsTrue(none.NoHerd(1f));
        }

        [Test]
        public void Toddle_FollowsBehind_AndPlops()
        {
            var t = new ToddleGame(12, 0f, new[] { 1, 2 }, Vector2.zero);
            bool plopped = false;
            for (float s = 0f; s < 20f; s += 0.1f)
            {
                var carer = t.Cue(0, s); var baby = t.Cue(1, s);
                Assert.AreEqual(ToddleGame.Behind, (carer.pos - baby.pos).magnitude, 0.03f, $"t {s:0.0}: 0.15 u behind");
                if (baby.endBeat) plopped = true;
            }
            Assert.IsTrue(plopped);
        }

        [Test]
        public void DanceRing_MovesInStep_OnTheRing()
        {
            var ring = new DanceRingGame(21, 0f, new[] { 1, 2, 3, 4, 5, 6, 7 }, Vector2.zero);
            for (float s = 0.1f; s < 13f; s += 0.37f)
            {
                var a = ring.Cue(0, s);
                Assert.IsTrue(a.inStep);
                for (int i = 1; i < 7; i++)
                {
                    var c = ring.Cue(i, s);
                    Assert.AreEqual(a.style, c.style);
                    Assert.AreEqual(a.since, c.since, 1e-4f);
                    Assert.AreEqual(a.pos.magnitude, c.pos.magnitude, 1e-3f, "all on one ring");
                }
            }
            Assert.AreEqual(DollStyle.Cheer, ring.Cue(3, DanceRingGame.DanceSeconds + 0.5f).style, "the end beat");
        }

        [Test]
        public void HandsMid_MatchesTheArmsSlotForAHug()
        {
            var p = DollPose.Rest; p.armL = new Vector3(62f, 0f, -4f); p.armR = new Vector3(62f, 0f, 4f);
            var h = KidGame.HandsMid(Vector2.zero, 180f, p, 1f); // yaw 180: facing +z
            Assert.Greater(h.z, 0.04f, "in front");
            Assert.AreEqual(0.084f, h.y, 0.01f, "chest high");
        }
    }
}
