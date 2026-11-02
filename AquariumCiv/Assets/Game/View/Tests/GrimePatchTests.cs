using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.View;
using AquariumCiv.View.Grime;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#121: the grime's clean patches vary by seed and do not repeat. The measure is
    /// <see cref="GrimePatches.Repetition"/>: the best self-correlation of the pane's detail (the field minus its 0.5 u local average) at any shift of 0.8 to 3.5 u (the old texture
    /// repeated every 1.67 u, which scores ~1 here).</summary>
    public class GrimePatchTests
    {
        const float W = 6f, H = 2.95f;
        const int PX = 20;            // texels per unit for the test
        static int Wd => (int)(W * PX);
        static int Ht => (int)(H * PX);

        /// <summary>The old pattern in the same terms: one fixed tile of this period repeated across the pane (what a tiled detail texture does).</summary>
        static float[] Tiled(float period)
        {
            int tw = (int)(period * PX);
            var tile = GrimePatches.Weights(11, tw, Ht, tw / (float)PX, H);
            var f = new float[Wd * Ht];
            for (int y = 0; y < Ht; y++) for (int x = 0; x < Wd; x++) f[y * Wd + x] = tile[y * tw + x % tw];
            return f;
        }

        static float[] Detail(float[] f) => GrimePatches.Detail(f, Wd, Ht, W, H);

        [Test]
        public void TheMeasureSeesATiledPatternAndSeparatesItFromTheNewOne()
        {
            float tiled = GrimePatches.Repetition(Detail(Tiled(1.65f)), Wd, Ht, W, H, 0.8f, 3.5f);
            Assert.Greater(tiled, 0.9f, "a tile repeated every 1.65 u scores as a repeat");
            var scores = Enumerable.Range(0, 12).Select(s =>
                GrimePatches.Repetition(Detail(GrimePatches.Weights(s, Wd, Ht, W, H)), Wd, Ht, W, H, 0.8f, 3.5f)).ToList();
            TestContext.Progress.WriteLine("[grime] repetition: tiled " + tiled + ", new panes max " + scores.Max() + " mean " + scores.Average());
            Assert.Less(scores.Max(), 0.45f, "no pane repeats itself at any shift of 0.8 u or more");
            Assert.Less(scores.Max(), tiled - 0.45f);
        }

        [Test]
        public void PanesDoNotShareAPattern()
        {
            var panes = Enumerable.Range(0, 8).Select(s => Detail(GrimePatches.Weights(s, Wd, Ht, W, H))).ToList();
            float worst = 0f;
            for (int a = 0; a < panes.Count; a++)
                for (int b = a + 1; b < panes.Count; b++) worst = Math.Max(worst, Math.Abs(GrimePatches.Similarity(panes[a], panes[b])));
            TestContext.Progress.WriteLine("[grime] worst pane-to-pane similarity " + worst);
            Assert.Less(worst, 0.3f);
            Assert.AreEqual(1f, GrimePatches.Similarity(panes[0], panes[0]), 1e-3f);
        }

        [Test]
        public void PatchSizeAspectAndCountVary()
        {
            var counts = new List<int>(); var radii = new List<float>(); var aspects = new List<float>(); var tilts = new List<float>();
            for (int s = 0; s < 30; s++)
            {
                var p = GrimePatches.Place(s, W, H);
                counts.Add(p.Count);
                foreach (var q in p) { radii.Add(MathF.Sqrt(q.rx * q.ry)); aspects.Add(q.ry / q.rx); tilts.Add(q.tilt); }
            }
            float cv = Sd(radii) / radii.Average();
            TestContext.Progress.WriteLine($"[grime] patches/pane {counts.Min()}..{counts.Max()}, size CV {cv}, aspect sd {Sd(aspects)}");
            Assert.GreaterOrEqual(counts.Max() - counts.Min(), 3, "the number of patches varies by pane");
            Assert.Greater(cv, 0.4f, "sizes spread (coefficient of variation)");
            Assert.Greater(Sd(aspects), 0.15f, "shapes: round to long");
            Assert.Greater(Sd(tilts), 0.6f, "tilts are all over");
            Assert.Greater(radii.Max() / radii.Min(), 5f, "small specks and big smears");
        }

        [Test]
        public void PlacementIsNotAGridAndStaysInsideThePane()
        {
            for (int s = 0; s < 10; s++)
            {
                var p = GrimePatches.Place(s, W, H);
                Assert.GreaterOrEqual(p.Count, 2);
                foreach (var q in p) { Assert.That(q.cx, Is.InRange(0f, W)); Assert.That(q.cy, Is.InRange(0f, H)); }
                // Nearest-neighbour distances are not all alike (a grid would have one value).
                var nn = new List<float>();
                for (int a = 0; a < p.Count; a++)
                {
                    float m = float.MaxValue;
                    for (int b = 0; b < p.Count; b++)
                        if (a != b) m = Math.Min(m, MathF.Sqrt((p[a].cx - p[b].cx) * (p[a].cx - p[b].cx) + (p[a].cy - p[b].cy) * (p[a].cy - p[b].cy)));
                    nn.Add(m);
                }
                if (nn.Count > 4) Assert.Greater(Sd(nn) / nn.Average(), 0.15f, "pane " + s);
            }
        }

        [Test]
        public void WeightsAreInRangeDeterministicAndHaveCleanSpaces()
        {
            var a = GrimePatches.Weights(3, Wd, Ht, W, H);
            var b = GrimePatches.Weights(3, Wd, Ht, W, H);
            CollectionAssert.AreEqual(a, b);
            Assert.GreaterOrEqual(a.Min(), 0f);
            Assert.LessOrEqual(a.Max(), 1.9f);
            float clean = a.Count(v => v < 0.2f) / (float)a.Length;
            TestContext.Progress.WriteLine("[grime] clean share pane 3: " + clean);
            Assert.That(clean, Is.InRange(0.03f, 0.5f), "open spaces exist but do not take over");
            Assert.Greater(a.Average(), 0.5f, "grime still grows over most of the glass");
        }

#if UNITY_5_3_OR_NEWER // Tools/SimTests compiles this file without Unity, and GlassMask is a Unity type
        /// <summary>The cow-hide bug: with the soil's texels counted in the level, 0.8 on the sim was 0.9 above the ground and 73% of the glass sat pinned at 1.</summary>
        [Test]
        public void TheLevelIsTheGrimeOnTheGlassAboveTheGroundAndOnlyTheWorstOfItPins()
        {
            foreach (int seed in new[] { 1, 7, 2026 })
            {
                var m = new GlassMask(new UnityEngine.Vector2(W, H), PX, seed);
                m.NoGrimeBelow(u => 1f);
                m.ResetTo(0.8f);
                Assert.AreEqual(0.8f, m.Mean, 0.1f, "the mean over the glass that can hold grime");
                int live = 0, pinned = 0;
                for (int i = 0; i < m.grime.Length; i++)
                {
                    if (m.weight[i] <= 0.01f) continue;
                    live++;
                    if (m.grime[i] >= 0.98f) pinned++;
                }
                Assert.AreEqual(live, m.LiveTexels);
                TestContext.Progress.WriteLine($"[grime] seed {seed}: mean {m.Mean:0.00}, pinned {pinned / (float)live:0.00}");
                Assert.Less(pinned / (float)live, 0.35f, "most of a dirty pane keeps its gradient");
                m.ResetTo(0.45f);
                Assert.Less(m.grime.Count(g => g >= 0.98f) / (float)live, 0.02f, "a new box's light smudges pin nothing");
            }
        }
#endif

        static float Sd(IEnumerable<float> v)
        {
            var l = v.ToList(); float m = l.Average();
            return MathF.Sqrt(l.Sum(x => (x - m) * (x - m)) / l.Count);
        }
    }
}
