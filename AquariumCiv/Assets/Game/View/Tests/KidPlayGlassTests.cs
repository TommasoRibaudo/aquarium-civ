// #772: the glass stops a thrown ball. Pure (UnityEngine maths only), so Tools/AnimBench.Tests runs it too.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class KidPlayGlassTests
    {
        const float Eps = 1e-4f;

        static void AssertInside(Vector3 p, float margin, string what)
        {
            Assert.GreaterOrEqual(p.x, -GlassBox.HalfX + margin - Eps, what + " x"); Assert.LessOrEqual(p.x, GlassBox.HalfX - margin + Eps, what + " x");
            Assert.GreaterOrEqual(p.z, GlassBox.ZMin + margin - Eps, what + " z"); Assert.LessOrEqual(p.z, GlassBox.ZMax - margin + Eps, what + " z");
        }

        [Test]
        public void AThrowAimedAtAndPastEveryPane_BouncesBack_NeverOutsideTheGlass()
        {
            const float r = 0.045f;
            int bounced = 0, shots = 0;
            // from a few spots near each pane and the corners, in every direction, short and long throws, bounce + roll included
            var spots = new List<Vector2>();
            foreach (float x in new[] { -3f, -2.8f, 0f, 2.8f, 3f }) foreach (float z in new[] { -2.2f, -2f, 0.8f, 3.6f, 3.8f }) spots.Add(new Vector2(x, z));
            foreach (var sp in spots)
                for (int a = 0; a < 360; a += 15)
                    foreach (float len in new[] { 0.2f, 0.5f, 0.9f, 1.6f })
                    {
                        var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                        var from = new Vector3(Mathf.Clamp(sp.x, -3f, 3f), 0.12f, sp.y);
                        var to = new Vector3(from.x + dir.x * len, r, from.z + dir.y * len);
                        var shot = ArcShot.Throw(from, to, 0.2f, Arc.FlightFor(len, 0.6f), Landing.BounceRoll, 1f, 0f, r).InsideGlass(r);
                        shots++;
                        bool hit = false;
                        for (float t = 0f; t < 1f + shot.flight + Arc.SettleSeconds(shot) + 1f; t += 0.02f)
                        {
                            var s = Arc.Sample(shot, t);
                            AssertInside(s.position, r, $"from {from} {a} deg {len}: t {t:0.00}");
                            hit |= s.beyondGlass;
                        }
                        if (hit) bounced++;
                    }
            Assert.Greater(bounced, 100, $"{bounced} of {shots} throws met a pane (the test is not vacuous)");
        }

        [Test]
        public void ABounceLosesSpeed_AndComesBackTheWayItWent()
        {
            const float r = 0.045f;
            // straight at the right pane along the row z = 0.8
            var shot = ArcShot.Throw(new Vector3(2.8f, 0.12f, 0.8f), new Vector3(3.4f, r, 0.8f), 0.15f, 1f, Landing.BounceRoll, 0f, 0f, r).InsideGlass(r);
            var end = Arc.Sample(shot, shot.LandAt).position;
            Assert.Less(end.x, GlassBox.HalfX - r + Eps, "stopped at the pane, not past it");
            Assert.Greater(end.x, 2.8f, "it went out toward the glass first");
            float leave = 0f, back = 0f;
            for (float t = 0f; t < shot.flight; t += 0.01f)
            {
                float x0 = Arc.Sample(shot, t).position.x, x1 = Arc.Sample(shot, t + 0.01f).position.x;
                if (x1 > x0) leave = Mathf.Max(leave, (x1 - x0) / 0.01f);
                if (x1 < x0) back = Mathf.Max(back, (x0 - x1) / 0.01f);
            }
            Assert.Greater(back, 0f, "it came back off the glass");
            Assert.Less(back, leave * 0.7f, "slower on the way back");
            Assert.IsTrue(Arc.HitGlassBetween(shot, 0f, shot.LandAt), "the thunk fires once on the way");
            Assert.IsFalse(Arc.HitGlassBetween(shot, 0f, 0.05f));
        }

        [Test]
        public void TheGlassCheatsGame_FirstThrowsMeetTheRightPane_AndStayInside()
        {
            for (int n = 1; n <= 3; n++)
                for (int seed = 1; seed <= 24; seed++)
                {
                    var members = new List<int>(); for (int i = 0; i < n; i++) members.Add(100 + i);
                    var game = new BallGame(seed, 0f, members, new Vector2(GlassBox.HalfX - 0.3f, 0.8f)) { GlassAim = true };
                    bool met = false;
                    for (int k = 0; k < 2; k++)
                    {
                        var th = game.ThrowAt(k);
                        Assert.IsTrue(th.miss, $"{n} kids seed {seed}: throw {k} is a miss");
                        for (float t = th.launch; t < th.next; t += 0.02f)
                        {
                            var p = Arc.Sample(th.shot, t).position;
                            AssertInside(p, BallGame.BallR, $"{n} kids seed {seed} throw {k} t {t:0.00}");
                            met |= p.x > GlassBox.HalfX - BallGame.BallR - 0.1f;
                        }
                    }
                    Assert.IsTrue(met, $"{n} kids seed {seed}: the ball met the pane");
                }
        }

        static IEnumerable<TestCaseData> Games()
        {
            foreach (int n in new[] { 1, 2, 3 })
                for (int seed = 1; seed <= 24; seed++)
                    foreach (var c in new[] { new Vector2(-2.55f, 0.8f), new Vector2(2.55f, 0.8f), new Vector2(0f, -1.75f), new Vector2(0f, 3.35f), new Vector2(2.55f, 3.35f), new Vector2(-2.55f, -1.75f) }) // the nearest a play group is put to a pane (KidPlayDirector.Open keeps 0.45 u off)
                        yield return new TestCaseData(n, seed, c).SetName($"NoBallLeavesTheGlass_{n}kids_seed{seed}_at{c.x:0.0}_{c.y:0.0}");
        }

        [TestCaseSource(nameof(Games))]
        public void TheBallGame_NeverPutsABallOutsideThePanes(int n, int seed, Vector2 centre)
        {
            var members = new List<int>(); for (int i = 0; i < n; i++) members.Add(100 + i);
            var game = new BallGame(seed, 0f, members, centre);
            float r = BallGame.BallR;
            for (int k = 0; k < 40; k++)
            {
                var th = game.ThrowAt(k);
                for (float t = th.launch; t < th.next; t += 0.03f)
                {
                    var s = Arc.Sample(th.shot, t);
                    AssertInside(s.position, r, $"throw {k} t {t:0.00}");
                    var b = game.Ball(t);
                    if (b.Free) AssertInside(b.position, r, $"ball {k} t {t:0.00}");
                }
                if (th.miss)
                {
                    AssertInside(new Vector3(th.restPos.x, 0f, th.restPos.y), r, $"rest {k}");
                    AssertInside(new Vector3(th.fetchTo.x, 0f, th.fetchTo.y), 0f, $"the fetcher's spot {k}");
                    if (th.rolls) AssertInside(new Vector3(th.rollFrom.x, 0f, th.rollFrom.y), r, $"roll from {k}");
                }
            }
        }

        [Test]
        public void ARollDownIntoAHollow_PastTheGlass_StaysInside()
        {
            BallGame.RollsTo = p => p + new Vector2(5f, -5f); // a hollow far past the glass
            try
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    var game = new BallGame(seed, 0f, new[] { 1, 2, 3 }, new Vector2(2.55f, 3.35f));
                    for (int k = 0; k < 30; k++)
                    {
                        var th = game.ThrowAt(k);
                        for (float t = th.launch; t < th.next; t += 0.03f)
                        {
                            var b = game.Ball(t);
                            if (b.Free) AssertInside(b.position, BallGame.BallR, $"seed {seed} throw {k} t {t:0.00}");
                        }
                    }
                }
            }
            finally { BallGame.RollsTo = null; }
        }
    }
}
