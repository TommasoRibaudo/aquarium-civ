// #355 Beat kit: pure tests (NUnit 3 classic asserts; UnityEngine maths only), so Tools/AnimBench can compile and run them too.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class BeatKitEasingTests
    {
        static readonly Ease[] All = (Ease[])System.Enum.GetValues(typeof(Ease));

        [Test]
        public void EveryEase_StartsAt0_EndsAt1_AndClampsOutside()
        {
            foreach (var e in All)
            {
                Assert.AreEqual(0f, Easing.Apply(e, 0f), 1e-4f, $"{e} at 0");
                Assert.AreEqual(1f, Easing.Apply(e, 1f), 1e-4f, $"{e} at 1");
                Assert.AreEqual(Easing.Apply(e, 0f), Easing.Apply(e, -2f), 1e-6f, $"{e} below 0");
                Assert.AreEqual(Easing.Apply(e, 1f), Easing.Apply(e, 3f), 1e-6f, $"{e} past 1");
                for (float u = 0f; u <= 1f; u += 0.01f) Assert.IsFalse(float.IsNaN(Easing.Apply(e, u)), $"{e} at {u}");
            }
        }

        [Test]
        public void PlainEases_AreMonotone_InIsSlowFirst_OutIsFastFirst()
        {
            foreach (var e in new[] { Ease.Linear, Ease.In, Ease.Out, Ease.Smooth, Ease.Snap })
            {
                float last = 0f;
                for (float u = 0.01f; u <= 1f; u += 0.01f) { float v = Easing.Apply(e, u); Assert.GreaterOrEqual(v, last - 1e-6f, $"{e} at {u}"); last = v; }
            }
            Assert.Less(Easing.Apply(Ease.In, 0.5f), 0.5f);
            Assert.Greater(Easing.Apply(Ease.Out, 0.5f), 0.5f);
            Assert.Less(Easing.Apply(Ease.Snap, 0.5f), Easing.Apply(Ease.In, 0.5f), "a strike is slower to start than a plain ease in");
            Assert.AreEqual(Mathf.SmoothStep(0f, 1f, 0.3f), Easing.Apply(Ease.Smooth, 0.3f), 1e-5f, "Smooth is today's smoothstep");
        }

        [Test]
        public void Back_Overshoots_Pop_PeaksAt1Point2_Bounce_HopsTwice()
        {
            float most = 0f;
            for (float u = 0f; u <= 1f; u += 0.005f) most = Mathf.Max(most, Easing.Apply(Ease.Back, u));
            Assert.Greater(most, 1.05f, "out-back goes past the key");
            Assert.Less(most, 1.15f);

            Assert.AreEqual(Easing.PopPeak, Easing.Pop(Easing.PopPeakAt), 1e-4f);
            Assert.AreEqual(1f, Easing.Pop(1f), 1e-5f);

            // The ball: down, up to 35 %, down, up to 12 %, down, still.
            var peaks = new List<float>();
            float a = Easing.BounceHeight(0f), b = Easing.BounceHeight(0.001f);
            for (float u = 0.002f; u <= 1f; u += 0.001f)
            {
                float c = Easing.BounceHeight(u);
                if (b > a && b >= c) peaks.Add(b);
                a = b; b = c;
            }
            Assert.AreEqual(2, peaks.Count, "two bounces");
            Assert.AreEqual(Easing.Bounce1, peaks[0], 0.01f);
            Assert.AreEqual(Easing.Bounce2, peaks[1], 0.01f);
            Assert.AreEqual(0f, Easing.BounceHeight(1f), 1e-5f);
        }

        [Test]
        public void Wobble_IsZeroAtContact_AndDiesAway()
        {
            Assert.AreEqual(0f, Easing.Wobble(0f, 7f, 6f, 0.18f), 1e-6f);
            Assert.AreEqual(0f, Easing.Wobble(-1f, 7f, 6f, 0.18f), 1e-6f);
            float early = 0f, late = 0f;
            for (float t = 0f; t < 0.2f; t += 0.005f) early = Mathf.Max(early, Mathf.Abs(Easing.Wobble(t, 7f, 6f, 0.18f)));
            for (float t = Easing.WobbleSeconds(0.18f); t < 2f; t += 0.005f) late = Mathf.Max(late, Mathf.Abs(Easing.Wobble(t, 7f, 6f, 0.18f)));
            Assert.Greater(early, 3f, "a bush sways visibly (§0.5: 7°)");
            Assert.Less(late, 7f * 0.07f, "and is still after WobbleSeconds");
        }
    }

    public class BeatKitBeatTests
    {
        /// <summary>A test strike: arm back, snap down, hit, hold, overshoot, back to the loop.</summary>
        internal static Beat Strike() => new Beat(
            Beat.Step(BeatPhase.Antic, 0.3f, Ease.Out, PoseKey.Hold.ArmR(150f, 0f, 8f).Lean(-6f)),
            Beat.Step(BeatPhase.Act, 0.15f, Ease.Snap, PoseKey.Hold.ArmR(50f, 0f, 8f).Lean(20f).Extend(true)),
            Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.3f, Ease.Back, PoseKey.Hold.ArmR(70f, 0f, 8f).Lean(10f)),
            Beat.Step(BeatPhase.Settle, 0.3f, Ease.Smooth, PoseKey.Base));

        static DollPose Loop => DollMotion.Evaluate(DollStyle.Stand, 1.3f, 1f, 0, 1, PropSlot.Hand, false);

        [Test]
        public void Phases_PlayInOrder_WithTheirDurations()
        {
            var b = Strike();
            Assert.AreEqual(1.31f, b.Duration, 1e-4f);
            Assert.AreEqual(0.45f, b.ContactAt, 1e-4f);
            Assert.AreEqual(0.51f, b.StartOf(BeatPhase.Hold), 1e-4f);
            Assert.AreEqual(-1f, b.StartOf(BeatPhase.EndBeat));

            Assert.IsFalse(b.Sample(-0.1f).started);
            Assert.AreEqual(BeatPhase.Antic, b.Sample(0f).phase);
            Assert.AreEqual(BeatPhase.Act, b.Sample(0.35f).phase);
            Assert.AreEqual(BeatPhase.Contact, b.Sample(0.47f).phase);
            Assert.IsTrue(b.Sample(0.47f).Hit);
            Assert.AreEqual(BeatPhase.Hold, b.Sample(0.6f).phase);
            Assert.IsFalse(b.Sample(0.6f).Hit);
            Assert.AreEqual(BeatPhase.Follow, b.Sample(0.8f).phase);
            Assert.AreEqual(BeatPhase.Settle, b.Sample(1.2f).phase);
            Assert.IsTrue(b.Sample(b.Duration).done);
            Assert.AreEqual(BeatPhase.Done, b.Sample(5f).phase);

            var last = BeatPhase.Approach;
            for (float t = 0f; t < b.Duration; t += 0.01f)
            {
                var p = b.Sample(t).phase;
                Assert.GreaterOrEqual((int)p, (int)last, $"no phase plays twice or backwards, at {t}");
                last = p;
            }
        }

        [Test]
        public void AContactShorterThanAFrame_IsWidened()
        {
            var b = new Beat(Beat.Step(BeatPhase.Act, 0.1f, Ease.Snap, PoseKey.Hold), Beat.Step(BeatPhase.Contact, 0f, Ease.Linear, PoseKey.Hold));
            Assert.AreEqual(Beat.MinContact, b[1].seconds, 1e-6f);
        }

        [Test]
        public void TheHitEdge_FiresExactlyOncePerContact_AtAnyFrameRate()
        {
            var b = Strike();
            foreach (float fps in new[] { 20f, 30f, 60f, 144f })
                for (float start = 0f; start < 0.05f; start += 0.0071f)
                {
                    var edge = new HitEdge();
                    int fires = 0;
                    for (float t = -0.2f + start; t < b.Duration + 0.5f; t += 1f / fps)
                        if (edge.Step(b.Pose(Loop, t).hit)) fires++;
                    Assert.AreEqual(1, fires, $"{fps} fps, offset {start}");
                }
        }

        [Test]
        public void ContactBetween_CountsOnce_EvenAtCoarseSteps()
        {
            var b = Strike();
            foreach (float step in new[] { 0.016f, 0.1f, 0.5f, 2f })
            {
                int n = 0;
                for (float t = -1f; t < 3f; t += step) if (b.ContactBetween(t, t + step)) n++;
                Assert.AreEqual(1, n, $"step {step}");
            }
        }

        [Test]
        public void HitEdge_OnALoop_FiresOncePerStroke()
        {
            // A chop loop (1.15 s a stroke at pace 1) sampled at 30 fps for 10 s: one edge per stroke, never two.
            var edge = new HitEdge();
            int fires = 0;
            const float period = 1.15f;
            for (int f = 0; f < 300; f++)
                if (edge.Step(DollMotion.Evaluate(DollStyle.Chop, f / 30f, 1f, 0, 2, PropSlot.Hand, true).hit)) fires++;
            int strokes = 0;
            for (int k = 0; k < 20; k++) { float hitStart = (k + 0.74f) * period; if (hitStart < 10f) strokes++; }
            Assert.AreEqual(strokes, fires);
        }

        [Test]
        public void TheBeat_RidesOnTheLoop_AndSettlesBackOntoIt()
        {
            var b = Strike();
            var loop = Loop;
            loop.armL = new Vector3(33f, 0f, -12f);
            Assert.AreEqual(loop.armR, b.Pose(loop, -0.5f).armR, "before the start: the loop");
            var mid = b.Pose(loop, 0.6f);
            Assert.AreEqual(50f, mid.armR.x, 1e-3f, "held at the act's key");
            Assert.AreEqual(20f, mid.lean, 1e-3f);
            Assert.IsTrue(mid.extend);
            Assert.AreEqual(loop.armL, mid.armL, "parts no key sets stay the loop's");
            Assert.AreEqual(loop.squash, mid.squash, 1e-6f);
            var end = b.Pose(loop, b.Duration + 0.1f);
            Assert.AreEqual(loop.armR.x, end.armR.x, 1e-3f, "settled back onto the loop");
            Assert.AreEqual(loop.lean, end.lean, 1e-3f);
            Assert.IsFalse(end.extend);
            Assert.AreEqual(0f, end.hit);
        }

        [Test]
        public void Back_OvershootsTheKey_InTheFollow()
        {
            var b = Strike();
            float least = float.MaxValue; // Follow goes from 50 to 70: out-back passes 70 on the way
            float most = float.MinValue;
            for (float t = 0.71f; t < 1.01f; t += 0.005f) { float x = b.Pose(Loop, t).armR.x; least = Mathf.Min(least, x); most = Mathf.Max(most, x); }
            Assert.Greater(most, 70.5f, "the recoil overshoots");
        }

        [Test]
        public void Amp_ScalesHowFarTheBeatMovesTheDoll()
        {
            var b = Strike();
            var loop = Loop;
            var full = b.Pose(loop, 0.47f); // in the Contact
            var half = b.Pose(loop, 0.47f, 0.5f);
            Assert.AreEqual((full.armR.x + loop.armR.x) * 0.5f, half.armR.x, 1e-3f);
            Assert.AreEqual((full.lean + loop.lean) * 0.5f, half.lean, 1e-3f);
            Assert.AreEqual(1f, half.hit, "a smaller move still hits");
        }

        [Test]
        public void Scaled_StretchesEveryStep()
        {
            var b = Strike().Scaled(1.1f);
            Assert.AreEqual(1.31f * 1.1f, b.Duration, 1e-4f);
            Assert.AreEqual(0.45f * 1.1f, b.ContactAt, 1e-4f);
        }

        [Test]
        public void HitStop_FreezesAtTheEndOfTheAct_ThenCatchesUpInTheFollow()
        {
            var b = Strike();
            float c = b.ContactAt, h = BeatFlags.HitStopSeconds;
            var actEnd = b.Pose(Loop, c - 1e-4f);
            for (float t = c; t < c + h - 1e-3f; t += 0.01f)
            {
                var s = b.Sample(t, true);
                Assert.IsTrue(s.frozen, $"frozen at {t}");
                var p = b.Pose(Loop, t, 1f, true);
                Assert.AreEqual(actEnd.armR.x, p.armR.x, 1e-3f, "the strike's end holds");
                Assert.AreEqual(0f, p.hit, "the hit (the puff) comes as the freeze ends");
            }
            Assert.IsTrue(b.Sample(c + h + 0.01f, true).Hit);
            Assert.IsTrue(b.ContactBetween(c + h - 0.01f, c + h + 0.01f, true));
            // The beat still ends on time: same phase at the end, the Follow absorbed the freeze.
            Assert.AreEqual(BeatPhase.Settle, b.Sample(b.StartOf(BeatPhase.Settle) + 0.01f, true).phase);
            Assert.IsTrue(b.Sample(b.Duration, true).done);
            var edge = new HitEdge(); int fires = 0;
            for (float t = 0f; t < b.Duration + 0.2f; t += 1f / 30f) if (edge.Step(b.Pose(Loop, t, 1f, true).hit)) fires++;
            Assert.AreEqual(1, fires);
            // Continuous through the catch-up (no jump bigger than a normal frame's move).
            float lastClock = b.Clock(0f, true, out _);
            for (float t = 0.005f; t < b.Duration; t += 0.005f)
            {
                float k = b.Clock(t, true, out _);
                Assert.GreaterOrEqual(k, lastClock - 1e-4f, $"the clock never runs back, at {t}");
                Assert.Less(k - lastClock, 0.02f, $"nor jumps, at {t}");
                lastClock = k;
            }
        }
    }

    public class BeatKitDesyncTests
    {
        [Test]
        public void Of_IsDeterministic_InTheSpecsRanges()
        {
            var seen = new HashSet<float>();
            for (int key = 0; key < 200; key++)
            {
                var d = Desync.Of(key, 358);
                Assert.AreEqual(d.delay, Desync.Of(key, 358).delay);
                Assert.That(d.delay, Is.InRange(0f, Desync.MaxDelay));
                Assert.That(d.period, Is.InRange(Desync.PeriodLo, Desync.PeriodHi));
                Assert.That(d.amp, Is.InRange(Desync.AmpLo, Desync.AmpHi));
                seen.Add(d.delay);
            }
            Assert.Greater(seen.Count, 190, "keys spread");
            Assert.AreNotEqual(Desync.Of(5, 1).delay, Desync.Of(5, 2).delay, "another kind of beat, another delay");
        }

        [Test]
        public void InGroup_NoThreeMembersStartTogether()
        {
            for (int seed = 0; seed < 300; seed++)
                for (int n = 2; n <= 10; n++)
                {
                    var delays = new float[n];
                    for (int i = 0; i < n; i++) delays[i] = Desync.InGroup(seed, i, n).delay;
                    System.Array.Sort(delays);
                    Assert.GreaterOrEqual(delays[0], 0f);
                    Assert.LessOrEqual(delays[n - 1], Mathf.Max(Desync.MaxDelay, n * 0.1f) + 1e-4f);
                    for (int i = 0; i + 2 < n; i++)
                        Assert.Greater(delays[i + 2] - delays[i], Desync.InStepWindow, $"seed {seed}, n {n}: P6");
                }
        }

        [Test]
        public void BeatHash_IsItsOwnStream()
        {
            Assert.AreEqual(BeatHash.U01(1, 2, 3), BeatHash.U01(1, 2, 3));
            Assert.AreNotEqual(BeatHash.U01(1, 2, 3), BeatHash.U01(1, 2, 4));
            float sum = 0f; for (int i = 0; i < 1000; i++) sum += BeatHash.U01(i, 9);
            Assert.AreEqual(0.5f, sum / 1000f, 0.04f, "about even");
            for (int i = 0; i < 100; i++) Assert.That(BeatHash.Pick(5, i, 1), Is.InRange(0, 4));
        }
    }

    public class BeatKitArcTests
    {
        /// <summary>Degrees between two directions (AnimBench's shim has no Vector3.Angle).</summary>
        static float AngleOf(Vector3 a, Vector3 b) => Mathf.Atan2(Vector3.Cross(a, b).magnitude, Vector3.Dot(a, b)) * Mathf.Rad2Deg;

        static readonly Vector3 Hand = new Vector3(0.2f, 1.12f, -0.3f), Chest = new Vector3(0.62f, 1.07f, -0.1f);

        [Test]
        public void AThrow_StartsAtTheHand_PeaksAtItsApex_LandsOnTarget_OnTime()
        {
            var s = ArcShot.Throw(Hand, Chest, 0.25f, 0.75f, Landing.Catch, launchAt: 2f);
            Assert.AreEqual(ArcStage.Waiting, Arc.Sample(s, 1.9f).stage);
            Assert.IsFalse(Arc.Sample(s, 1.9f).Visible);
            Assert.Less(Vector3.Distance(Arc.Sample(s, 2f).position, Hand), 0.02f, "Arc test: starts within 0.02 u of the hand");
            var mid = Arc.Sample(s, 2.375f);
            Assert.AreEqual(ArcStage.Flying, mid.stage);
            Assert.AreEqual((Hand.y + Chest.y) * 0.5f + 0.25f, mid.position.y, 1e-3f, "h is the apex over the straight line");
            Assert.AreEqual(ArcStage.Flying, Arc.Sample(s, 2.749f).stage);
            Assert.Less(Vector3.Distance(Arc.Sample(s, 2.7499f).position, Chest), 0.03f, "Arc test: ends within 0.03 u of its target");
            var caught = Arc.Sample(s, 3f);
            Assert.AreEqual(ArcStage.Resting, caught.stage);
            Assert.Less(Vector3.Distance(caught.position, Chest), 1e-4f);
            Assert.AreEqual(2.75f, s.LandAt, 1e-5f);
        }

        [Test]
        public void InFlight_ItPointsAlongItsWay()
        {
            var s = ArcShot.Throw(Hand, Chest + new Vector3(0.5f, 0f, 0.4f), 0.2f, 0.9f, Landing.Stick);
            for (float t = 0.05f; t < 0.85f; t += 0.1f)
            {
                var o = Arc.Sample(s, t);
                var fwd = o.rotation * Vector3.forward;
                Assert.Greater(Vector3.Dot(fwd, o.velocity.normalized), 0.999f, $"at {t}");
            }
        }

        [Test]
        public void ASpear_SticksBetween30And50OffVertical_AndQuivers()
        {
            foreach (float apex in new[] { 0.02f, 0.2f, 1.5f })
            {
                var s = ArcShot.Throw(Hand, new Vector3(1.3f, 1.0f, 0.2f), apex, 0.8f, Landing.Stick);
                var o = Arc.Sample(s, s.LandAt + 2f);
                Assert.AreEqual(ArcStage.Stuck, o.stage);
                Assert.Less(Vector3.Distance(o.position, s.end), 1e-4f);
                float offVertical = AngleOf(o.rotation * Vector3.forward, Vector3.down);
                Assert.That(offVertical, Is.InRange(29.5f, 50.5f), $"apex {apex}");
            }
            var q = ArcShot.Throw(Hand, new Vector3(1.3f, 1.0f, 0.2f), 0.2f, 0.8f, Landing.Stick);
            float a = AngleOf(Arc.Sample(q, q.LandAt + 0.025f).rotation * Vector3.forward, Vector3.down);
            float b = AngleOf(Arc.Sample(q, q.LandAt + 2f).rotation * Vector3.forward, Vector3.down);
            Assert.Greater(Mathf.Abs(a - b), 2f, "a quiver right after it lands");
        }

        [Test]
        public void AMissedBall_BouncesTwice_RollsOn_AndStops()
        {
            var s = ArcShot.Throw(Hand, new Vector3(0.7f, 1f, -0.3f), 0.25f, 0.75f, Landing.BounceRoll);
            float settle = Arc.SettleSeconds(s);
            Assert.Greater(settle, s.rollSeconds);
            Assert.Less(Vector3.Distance(Arc.Sample(s, s.LandAt - 1e-4f).position, Arc.Sample(s, s.LandAt + 1e-4f).position), 0.01f, "no jump at the landing");
            float top = 0f, lowest = float.MaxValue;
            for (float t = s.LandAt; t < s.LandAt + settle; t += 0.005f)
            {
                var o = Arc.Sample(s, t);
                top = Mathf.Max(top, o.position.y - s.end.y);
                lowest = Mathf.Min(lowest, o.position.y - s.end.y);
            }
            Assert.AreEqual(s.bounce1, top, 0.005f, "the first bounce is the highest");
            Assert.GreaterOrEqual(lowest, -1e-4f, "never through the ground");
            var rest = Arc.Sample(s, s.LandAt + settle + 0.5f);
            Assert.AreEqual(ArcStage.Resting, rest.stage);
            Assert.AreEqual(rest.position, Arc.Sample(s, s.LandAt + settle + 3f).position);
            float went = Vector3.Distance(new Vector3(rest.position.x, 0f, rest.position.z), new Vector3(s.end.x, 0f, s.end.z));
            Assert.Greater(went, s.roll, "it ran on past where it landed");
            Assert.Greater(Vector3.Dot(rest.position - s.end, s.Heading), 0f, "the way it was going");
        }

        [Test]
        public void IntoABasket_ItIsGoneAtTheEnd_AndLandsOnce()
        {
            var s = ArcShot.Throw(Hand, Chest, 0.1f, 0.4f, Landing.DropIn, launchAt: 1f);
            Assert.IsFalse(Arc.Sample(s, 1.41f).Visible);
            Assert.AreEqual(ArcStage.Gone, Arc.Sample(s, 1.41f).stage);
            int n = 0;
            for (float t = 0f; t < 3f; t += 1f / 30f) if (Arc.LandedBetween(s, t, t + 1f / 30f)) n++;
            Assert.AreEqual(1, n);
        }

        [Test]
        public void ThePool_ReusesSlots_TakesOverTheOldestWhenFull_AndSweeps()
        {
            var pool = new ArcPool(3);
            var a = pool.Launch(ArcShot.Throw(Hand, Chest, 0.2f, 0.5f, Landing.DropIn, launchAt: 0f));
            var b = pool.Launch(ArcShot.Throw(Hand, Chest, 0.2f, 0.5f, Landing.Catch, launchAt: 0.1f));
            var c = pool.Launch(ArcShot.Throw(Hand, Chest, 0.2f, 0.5f, Landing.Stick, launchAt: 0.2f));
            Assert.AreEqual(3, pool.Live);
            var d = pool.Launch(ArcShot.Throw(Hand, Chest, 0.2f, 0.5f, Landing.Catch, launchAt: 0.3f));
            Assert.IsFalse(pool.IsValid(a), "the oldest was taken over");
            Assert.IsTrue(pool.IsValid(d));
            Assert.IsFalse(pool.Sample(a, 0.4f, out var gone));
            Assert.AreEqual(ArcStage.Gone, gone.stage);
            Assert.IsTrue(pool.Sample(b, 0.4f, out var flying));
            Assert.AreEqual(ArcStage.Flying, flying.stage);

            pool.Release(b);
            Assert.AreEqual(2, pool.Live);
            Assert.IsFalse(pool.IsValid(b));
            Assert.AreEqual(0, pool.Sweep(0.5f, 1f), "nothing has settled yet");
            Assert.AreEqual(2, pool.Sweep(10f, 1f), "the spear and the caught ball, once they've lain a second");
            Assert.AreEqual(0, pool.Live);
            var e = pool.Launch(ArcShot.Throw(Hand, Chest, 0.2f, 0.5f, Landing.Catch));
            Assert.IsTrue(pool.IsValid(e));
            Assert.IsFalse(pool.IsValid(c), "a stale handle stays stale when its slot is reused");
        }
    }

    public class BeatKitGroupTests
    {
        static readonly int[] Kids = { 11, 23, 5, 42, 7 };

        [Test]
        public void ARing_SpreadsOnItsRadius_FacesTheCentre_AndTurnsBackAndForth()
        {
            var centre = new Vector2(0.4f, -0.2f);
            var ring = new RingGroup(99, 10f, Kids, centre, RingGroup.RadiusFor(5), leader: 2);
            Assert.AreEqual(0.3f, RingGroup.RadiusFor(5), 1e-6f);
            Assert.AreEqual(0.36f, RingGroup.RadiusFor(8), 1e-5f);
            var spots = new List<Vector2>();
            for (int i = 0; i < Kids.Length; i++)
            {
                var c = ring.Cue(i, 12.3f);
                if (i == 2) { Assert.AreEqual(GroupRole.Centre, c.role); Assert.AreEqual(centre, c.target); continue; }
                Assert.AreEqual(GroupRole.Dancer, c.role);
                Assert.AreEqual(0.3f, (c.target - centre).magnitude, 1e-4f);
                Assert.AreEqual(centre, c.face);
                Assert.AreEqual(0f, c.desync.delay, "a ring dance is in step on purpose (P6 exempt)");
                spots.Add(c.target);
            }
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                    Assert.Greater((spots[i] - spots[j]).magnitude, 0.3f, "evenly spread, nobody on top of another");

            // Turns 25°/s, reverses every 4 beats (2 s), continuous at the turn.
            Assert.AreEqual(25f, ring.TurnAt(1f), 1e-4f);
            Assert.AreEqual(50f, ring.TurnAt(2f), 1e-3f);
            Assert.AreEqual(25f, ring.TurnAt(3f), 1e-3f);
            Assert.AreEqual(ring.TurnAt(1.999f), ring.TurnAt(2.001f), 0.1f);
            Assert.AreEqual(ring.Cue(0, 12.3f).target, new RingGroup(99, 10f, Kids, centre, 0.3f, leader: 2).Cue(0, 12.3f).target, "deterministic");
        }

        [Test]
        public void ATalk_TakesTurns_FaceToFace()
        {
            var pair = new PairGroup(7, 0f, 101, 202, PairKind.Talk, new Vector2(0f, 0f), new Vector2(1f, 0f), gap: 0.16f, turnSeconds: 2f);
            for (float t = 0.1f; t < 10f; t += 0.5f)
            {
                var a = pair.Cue(0, t); var b = pair.Cue(1, t);
                Assert.AreNotEqual(a.role, b.role, "one talks, one listens");
                CollectionAssert.Contains(new[] { GroupRole.Talker, GroupRole.Listener }, a.role);
                Assert.AreEqual(b.target, a.face); Assert.AreEqual(a.target, b.face);
                Assert.AreEqual(0.16f, (a.target - b.target).magnitude, 1e-4f);
            }
            Assert.AreNotEqual(pair.Cue(0, 0.5f).role, pair.Cue(0, 2.5f).role, "the turn passes");
            Assert.AreEqual(pair.Cue(0, 0.5f).role, pair.Cue(0, 4.5f).role);
        }

        [Test]
        public void AHandOver_HasAGiverAndATaker_AndAHugWaitsForItsFlag()
        {
            var give = new PairGroup(3, 0f, 1, 2, PairKind.HandOver, Vector2.zero, new Vector2(0f, 1f));
            Assert.AreEqual(GroupRole.Giver, give.Cue(0, 1f).role);
            Assert.AreEqual(GroupRole.Taker, give.Cue(1, 1f).role);
            Assert.Greater(give.Cue(0, 1f).since, give.Cue(1, 1f).since, "the taker answers a moment later");

            var hug = new PairGroup(3, 0f, 1, 2, PairKind.Hug, Vector2.zero, new Vector2(0f, 1f), gap: 0.2f);
            bool was = BeatFlags.Hugs;
            try
            {
                BeatFlags.Hugs = false;
                Assert.AreEqual(GroupRole.Giver, hug.Cue(0, 1f).role, "with hugs off, a hug stance is a hand-over's (§0.8)");
                BeatFlags.Hugs = true;
                Assert.AreEqual(GroupRole.HugA, hug.Cue(0, 1f).role);
                Assert.AreEqual(PairGroup.HugGap, (hug.Cue(0, 1f).target - hug.Cue(1, 1f).target).magnitude, 1e-4f, "close");
                Assert.AreEqual(0.2f, (hug.Cue(0, 0f).target - hug.Cue(1, 0f).target).magnitude, 1e-4f, "from apart");
            }
            finally { BeatFlags.Hugs = was; }
        }

        [Test]
        public void WalkingTogether_TheSideKeepsAShoulderToTheRight()
        {
            var walk = new PairGroup(3, 0f, 1, 2, PairKind.WalkTogether, Vector2.zero, new Vector2(0f, 1f), speed: 0.12f);
            var lead = walk.Cue(0, 5f); var side = walk.Cue(1, 5f);
            Assert.AreEqual(0.6f, lead.target.y, 1e-4f);
            Assert.AreEqual(PairGroup.SideBySide, (side.target - lead.target).magnitude, 1e-4f);
            Assert.Greater(side.target.x, lead.target.x, "to the right of the way they go");
            Assert.IsTrue(lead.moving);
        }

        [Test]
        public void Tag_TheChaserTouchesItsTarget_WhoChasesNext_AndNoTagBacks()
        {
            var centre = new Vector2(-0.5f, 0.3f);
            var tag = new ChaseGroup(2026, 4f, Kids, centre, 0.6f, 5f);
            var again = new ChaseGroup(2026, 4f, Kids, centre, 0.6f, 5f);
            for (int r = 0; r < 8; r++)
            {
                int c = tag.ChaserOf(r), g = tag.TargetOf(r);
                Assert.AreNotEqual(c, g);
                if (r > 0) { Assert.AreEqual(tag.TargetOf(r - 1), c, "the touched one is it"); Assert.AreNotEqual(tag.ChaserOf(r - 1), g, "no tag-backs"); }
                float at = tag.ContactAt(r);
                var chaser = tag.Cue(c, at); var runner = tag.Cue(g, at);
                Assert.AreEqual(GroupRole.Chaser, chaser.role);
                Assert.AreEqual(GroupRole.Runner, runner.role);
                Assert.AreEqual(ChaseGroup.TouchDistance, (chaser.target - runner.target).magnitude, 0.005f, $"round {r}: P1, the touch within 0.09 u");
                Assert.AreEqual(0f, chaser.since, 1e-4f, "the chaser's beat is at its contact");
                // After the touch both hold, looking at each other.
                var hold = tag.Cue(g, at + 0.3f);
                Assert.AreEqual(runner.target, hold.target);
                Assert.Less((hold.face - tag.Cue(c, at + 0.3f).target).magnitude, 1e-4f);
                for (int i = 0; i < Kids.Length; i++)
                    for (float t = tag.t0 + r * 5f; t < tag.t0 + (r + 1) * 5f; t += 0.25f)
                    {
                        var cue = tag.Cue(i, t);
                        Assert.LessOrEqual((cue.target - centre).magnitude, 0.6f + ChaseGroup.TouchDistance + 1e-3f, "kept to the green");
                        Assert.AreEqual(cue.target, again.Cue(i, t).target, "deterministic");
                    }
            }
            // Positions are continuous across a round's turn.
            for (int i = 0; i < Kids.Length; i++)
                Assert.Less((tag.Cue(i, tag.t0 + 5f - 1e-3f).target - tag.Cue(i, tag.t0 + 5f + 1e-3f).target).magnitude, 0.01f);
            var other = new ChaseGroup(2027, 4f, Kids, centre, 0.6f, 5f);
            Assert.AreNotEqual(tag.Cue(0, 6f).target, other.Cue(0, 6f).target, "another seed, another game");
        }

        [Test]
        public void Runners_AreOutOfStep_AndTheChaserRunsAtOnce()
        {
            var tag = new ChaseGroup(17, 0f, Kids, Vector2.zero);
            var delays = new List<float>();
            for (int i = 0; i < Kids.Length; i++) delays.Add(tag.DesyncOf(i).delay);
            delays.Sort();
            for (int i = 0; i + 2 < delays.Count; i++) Assert.Greater(delays[i + 2] - delays[i], Desync.InStepWindow, "P6");
            int c = tag.ChaserOf(0);
            Assert.IsTrue(tag.Cue(c, 0.5f).moving);
        }
    }
}
