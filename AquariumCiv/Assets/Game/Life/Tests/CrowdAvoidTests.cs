using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#189: people walk round the fire and don't walk through each other (the pure rules; the view draws them).</summary>
    public class CrowdAvoidTests
    {
        static float Hyp(float a, float b) => (float)Math.Sqrt(a * a + b * b);
        static float Hyp(double a, double b) => (float)Math.Sqrt(a * a + b * b);

        static readonly Solid Fire = new Solid(0f, 0f, CrowdAvoid.FireRadius);
        static readonly Solid[] Solids = { Fire };

        sealed class Walker
        {
            public int key;
            public float sx, sz, vx, vz;      // planned start and velocity (units per second)
            public float ox, oz;               // the eased offset
            public float x, z;                 // where the doll is drawn
            public float px, pz;               // the last drawn point, for the step size
            public float maxStep;
        }

        /// <summary>Walk a crowd for <paramref name="seconds"/> at 30 fps with the view's loop, calling <paramref name="each"/>
        /// every frame.</summary>
        static void Run(CrowdAvoid av, List<Walker> ws, float seconds, Action<float> each, IReadOnlyList<Solid> solids = null)
        {
            const float dt = 1f / 30f;
            for (float t = 0f; t < seconds; t += dt)
            {
                av.Clear();
                foreach (var w in ws) av.Add(w.key, w.x, w.z, true, w.vx, w.vz);
                av.Build();
                for (int i = 0; i < ws.Count; i++)
                {
                    var w = ws[i];
                    av.Push(i, out float pushX, out float pushZ);
                    CrowdAvoid.Ease(ref w.ox, ref w.oz, pushX, pushZ, dt);
                    float x = w.sx + w.vx * t + w.ox, z = w.sz + w.vz * t + w.oz;
                    if (solids != null) CrowdAvoid.KeepOut(ref x, ref z, solids, w.vx, w.vz);
                    if (t > 0f) w.maxStep = Math.Max(w.maxStep, Hyp(x - w.x, z - w.z));
                    w.x = x; w.z = z;
                }
                each(t);
            }
        }

        static float MinPair(List<Walker> ws)
        {
            float best = float.MaxValue;
            for (int i = 0; i < ws.Count; i++)
                for (int j = i + 1; j < ws.Count; j++) best = Math.Min(best, Hyp(ws[i].x - ws[j].x, ws[i].z - ws[j].z));
            return best;
        }

        [Test]
        public void APointInTheFireIsPushedToItsRimAndOneOutsideIsLeftAlone()
        {
            float x = 0.05f, z = 0.02f;
            CrowdAvoid.KeepOut(ref x, ref z, Solids);
            Assert.AreEqual(CrowdAvoid.FireRadius, Hyp(x, z), 1e-4);
            float ax = 0.5f, az = 0.1f;
            CrowdAvoid.KeepOut(ref ax, ref az, Solids);
            Assert.AreEqual((0.5f, 0.1f), (ax, az));
            float cx = 0f, cz = 0f; // the very middle: still goes somewhere
            CrowdAvoid.KeepOut(ref cx, ref cz, Solids, 1f, 0f);
            Assert.AreEqual(CrowdAvoid.FireRadius, Hyp(cx, cz), 1e-4);
        }

        [Test]
        public void AWholeCampWalksPastTheFireAndNobodyIsOnIt()
        {
            // 12 people crossing the camp from the west to the east on lines 0..0.22 either side of the fire's middle.
            var ws = new List<Walker>();
            for (int i = 0; i < 12; i++)
                ws.Add(new Walker { key = 100 + i, sx = -1.2f - 0.03f * i, sz = (i - 5.5f) * 0.04f, vx = 0.5f, vz = 0f });
            foreach (var w in ws) { w.x = w.sx; w.z = w.sz; }
            var av = new CrowdAvoid();
            float closest = float.MaxValue;
            Run(av, ws, 7f, _ => { foreach (var w in ws) closest = Math.Min(closest, Hyp(w.x, w.z)); }, Solids);
            Assert.GreaterOrEqual(closest, CrowdAvoid.FireRadius - 1e-3f, "nobody gets onto the fire");
            foreach (var w in ws) Assert.Greater(w.x, 1f, "and they all got across");
        }

        [Test]
        public void ADrawnWalkNeverJumpsExceptAtTheVeryMiddle()
        {
            // Off the middle line the push out is continuous: no frame moves more than a walk's step plus a little.
            for (float z0 = 0.05f; z0 < 0.2f; z0 += 0.015f) // the rim speeds up by r / z0 near the middle
            {
                var w = new Walker { key = 1, sx = -1f, sz = z0, vx = 0.5f };
                w.x = w.sx; w.z = w.sz;
                Run(new CrowdAvoid(), new List<Walker> { w }, 4f, _ => { }, Solids);
                Assert.Less(w.maxStep, 0.5f / 30f * (CrowdAvoid.FireRadius / z0) * 1.2f + 0.01f, $"walk at z {z0}");
            }
        }

        [Test]
        public void AGroupWalkingToTheSameSpotSpreadsOut()
        {
            // 8 people leave one doorstep for one spot on almost the same point.
            var ws = new List<Walker>();
            for (int i = 0; i < 8; i++)
                ws.Add(new Walker { key = 40 + i, sx = -1f + 0.004f * i, sz = 0.003f * (i % 3), vx = 0.5f, vz = 0.02f * (i % 2) });
            foreach (var w in ws) { w.x = w.sx; w.z = w.sz; }
            var av = new CrowdAvoid();
            float startMin = MinPair(ws);
            Assert.Less(startMin, 0.02f);
            float latestBelow = 0f;
            Run(av, ws, 3.0f, t => { if (MinPair(ws) < CrowdAvoid.Personal * 0.8f) latestBelow = t; });
            Assert.GreaterOrEqual(MinPair(ws), CrowdAvoid.Personal * 0.9f, "they end up a body apart");
            Assert.Less(latestBelow, 1.5f, "and it takes a second or so, not the whole walk");
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var w in ws) { lo = Math.Min(lo, w.z); hi = Math.Max(hi, w.z); }
            Assert.Greater(hi - lo, 0.25f, "they fan out across the way");
            foreach (var w in ws) Assert.LessOrEqual(Hyp(w.ox, w.oz), CrowdAvoid.MaxOffset + 1e-4);
        }

        [Test]
        public void AWalkerGoesRoundSomeoneStanding()
        {
            var av = new CrowdAvoid();
            var walker = new Walker { key = 1, sx = -1f, sz = 0.01f, vx = 0.5f };
            walker.x = walker.sx; walker.z = walker.sz;
            float standX = 0f, standZ = 0f, closest = float.MaxValue;
            const float dt = 1f / 30f;
            for (float t = 0f; t < 4f; t += dt)
            {
                av.Clear();
                av.Add(2, standX, standZ, false);
                av.Add(1, walker.x, walker.z, true, 1f, 0f);
                av.Build();
                av.Push(1, out float px, out float pz);
                CrowdAvoid.Ease(ref walker.ox, ref walker.oz, px, pz, dt);
                walker.x = walker.sx + walker.vx * t + walker.ox; walker.z = walker.sz + walker.oz;
                closest = Math.Min(closest, Hyp(walker.x - standX, walker.z - standZ));
            }
            Assert.GreaterOrEqual(closest, CrowdAvoid.Personal * 0.6f, "the one standing is not walked through");
            av.Clear(); av.Add(2, 0f, 0f, false); av.Build();
            av.Push(0, out float sx, out float sz);
            Assert.AreEqual((0f, 0f), (sx, sz), "and a standing person is never pushed");
        }

        /// <summary>#203: walkers on the view's loop, offset eased, doll following, through the frame-to-frame turn of their way. A real mid-walk frame
        /// on the phone (30 fps) and at 60.</summary>
        static float WorstTurn(List<Walker> ws, float dt, float seconds, out float gap)
        {
            var av = new CrowdAvoid();
            float worst = 0f, end = 0f;
            var was = new Dictionary<int, (float x, float z)>();
            foreach (var w in ws) { w.x = w.sx; w.z = w.sz; }
            for (float t = 0f; t < seconds; t += dt)
            {
                av.Clear();
                foreach (var w in ws) av.Add(w.key, w.x, w.z, true, w.vx, w.vz);
                av.Build();
                for (int i = 0; i < ws.Count; i++)
                {
                    var w = ws[i];
                    av.Push(i, out float px, out float pz);
                    CrowdAvoid.Ease(ref w.ox, ref w.oz, px, pz, dt);
                    float ax = w.x, az = w.z;
                    CrowdAvoid.Follow(ref w.x, ref w.z, w.sx + w.vx * t + w.ox, w.sz + w.vz * t + w.oz, 1f * dt, dt);
                    float sx = w.x - ax, sz = w.z - az;
                    if (t > 1f && was.TryGetValue(w.key, out var s))
                    {
                        float cross = s.x * sz - s.z * sx, dot = s.x * sx + s.z * sz;
                        worst = Math.Max(worst, Math.Abs((float)Math.Atan2(cross, dot)) * 57.3f / dt);
                    }
                    was[w.key] = (sx, sz);
                }
                end = MinPair(ws);
            }
            gap = end;
            return worst;
        }

        [TestCase(1f / 30f), TestCase(1f / 60f)]
        public void ThreeWalkingSideBySideGoStraight(float dt)
        {
            var ws = new List<Walker>();
            for (int i = 0; i < 3; i++) ws.Add(new Walker { key = 10 + i, sx = -1f, sz = (i - 1) * 0.07f, vx = 0.31f });
            float turn = WorstTurn(ws, dt, 6f, out float gap);
            Assert.Less(turn, 60f, "degrees a second off their way, once they've settled");
            Assert.GreaterOrEqual(gap, CrowdAvoid.Personal * 0.8f, "and they do keep apart");
        }

        [TestCase(1f / 30f), TestCase(1f / 60f)]
        public void ANeighbourDriftingAcrossTheMiddleDoesNotFlipTheSidestep(float dt)
        {
            // One overtaking another on almost the same line: the old side flipped at 0.15 off the middle.
            var ws = new List<Walker>
            {
                new Walker { key = 1, sx = -1.0f, sz = 0.02f, vx = 0.42f },
                new Walker { key = 2, sx = -0.7f, sz = 0f, vx = 0.31f },
            };
            Assert.Less(WorstTurn(ws, dt, 6f, out _), 200f);
        }

        [Test]
        public void TwoMeetingHeadOnPassOnOppositeSides()
        {
            var ws = new List<Walker>
            {
                new Walker { key = 1, sx = -1f, sz = 0f, vx = 0.31f },
                new Walker { key = 2, sx = 1f, sz = 0f, vx = -0.31f },
            };
            foreach (var w in ws) { w.x = w.sx; w.z = w.sz; }
            float closest = float.MaxValue;
            Run(new CrowdAvoid(), ws, 6f, _ => closest = Math.Min(closest, MinPair(ws)));
            Assert.Greater(closest, CrowdAvoid.Personal * 0.5f, "nobody walks through anybody");
            Assert.Greater(ws[0].x, 0.5f, "and both got past");
            Assert.Less(ws[1].x, -0.5f);
        }

        [Test]
        public void ANeighbourCountsOnceWhateverCellsTheyShare()
        {
            // Two of the nine cells round someone can land in one bucket of the grid: the push must not count anyone twice.
            for (int k = 0; k < 400; k++)
            {
                float x = k * 0.137f, z = k * 0.091f;
                var av = new CrowdAvoid();
                av.Add(1, x, z, true, 1f, 0f);
                av.Add(2, x + 0.05f, z + 0.03f, false);
                av.Build();
                av.Push(0, out float px, out float pz);
                float over = CrowdAvoid.Personal - Hyp(0.05f, 0.03f);
                Assert.LessOrEqual(Hyp(px, pz), over * 1.01f, $"at {x}, {z}");
            }
        }

        [Test]
        public void TheOffsetSettlesTheSameAtAnyFrameRate()
        {
            // Closed like the view's loop: the push shrinks as the offset clears the overlap.
            float At(int fps)
            {
                float ox = 0f, oz = 0f, dt = 1f / fps;
                for (int i = 0; i < fps / 4; i++) CrowdAvoid.Ease(ref ox, ref oz, 0.05f - ox, 0f, dt);
                return ox;
            }
            Assert.AreEqual(At(30), At(60), 0.003f);
        }

        [Test]
        public void OffsetsFadeBackWhenNobodyIsNear()
        {
            float ox = 0.2f, oz = 0.1f;
            for (int i = 0; i < 300; i++) CrowdAvoid.Ease(ref ox, ref oz, 0f, 0f, 1f / 30f);
            Assert.Less(Hyp(ox, oz), 0.01);
        }

        [Test]
        public void ATownsWorthOfPeopleCostsNothingToCheckAndAllocatesNothing()
        {
            var av = new CrowdAvoid();
            var rng = new Random(7);
            var xs = new float[300]; var zs = new float[300];
            for (int i = 0; i < 300; i++) { xs[i] = (float)rng.NextDouble() * 5f; zs[i] = (float)rng.NextDouble() * 4f; }
            void Frame()
            {
                av.Clear();
                for (int i = 0; i < 300; i++) av.Add(i, xs[i], zs[i], i % 2 == 0, 1f, 0f);
                av.Build();
                for (int i = 0; i < 300; i++) av.Push(i, out _, out _);
            }
            Frame(); Frame();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int f = 0; f < 200; f++) Frame();
            long after = GC.GetAllocatedBytesForCurrentThread();
            sw.Stop();
            Assert.AreEqual(before, after, "no allocation per frame");
            Assert.Less(sw.Elapsed.TotalMilliseconds / 200.0, 1.0, "under a millisecond a frame for 300 people");
        }

        [Test]
        public void ItIsTheSameEveryTime()
        {
            (float, float) Once()
            {
                var ws = new List<Walker>();
                for (int i = 0; i < 6; i++) ws.Add(new Walker { key = i, sx = -1f + 0.003f * i, sz = 0f, vx = 0.5f });
                foreach (var w in ws) { w.x = w.sx; w.z = w.sz; }
                Run(new CrowdAvoid(), ws, 2f, _ => { }, Solids);
                return (ws[3].x, ws[3].z);
            }
            Assert.AreEqual(Once(), Once());
        }
    }
}
