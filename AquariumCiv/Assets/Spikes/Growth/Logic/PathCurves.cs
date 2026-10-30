using System;
using System.Collections.Generic;

namespace AquariumCiv.Spikes.Growth
{
    /// <summary>One drawn path: smooth sample points (world x, z) and a tier per point (1 trail, 2 path, 3 road).</summary>
    public sealed class PathCurve
    {
        public readonly List<float> x = new(), z = new(), tier = new();
        public bool startCap, endCap;   // a round cap where the curve ends (a dead end or a junction)
        public int Count => x.Count;
    }

    /// <summary>
    /// Growth Lab round 2: the tier grid drawn as curves instead of 0.1 u cells. The worn cells are thinned to a
    /// one-cell skeleton (Zhang–Suen), traced into chains between junctions and dead ends, simplified
    /// (Douglas–Peucker) and drawn through a centripetal Catmull–Rom spline. Pure C#; the view makes ribbons from it.
    /// </summary>
    public static class PathCurves
    {
        public static float SimplifyEps = 0.06f, SampleStep = 0.03f;
        public static int ChaikinRounds = 2, SmoothRounds = 6;

        /// <summary>Curves for a snapshot's tier grid. Cells on the green or the pond are left out (the green is drawn
        /// on its own); a chain that ends next to the green reaches into it.</summary>
        public static List<PathCurve> Build(byte[] tier, byte[] flags, float greenX, float greenZ)
        {
            const int NX = SiteMap.NX, NZ = SiteMap.NZ, N = SiteMap.N;
            var on = new bool[N];
            for (int c = 0; c < N; c++)
                on[c] = tier[c] >= PathNet.Trail && (flags[c] & (Snapshot.FGreen | Snapshot.FPond)) == 0;
            Thin(on);

            // Adjacency without the diagonal shortcuts of an L-corner (they make false junctions).
            bool Adj(int a, int b)
            {
                int ai = a % NX, aj = a / NX, bi = b % NX, bj = b / NX;
                int di = bi - ai, dj = bj - aj;
                if (Math.Abs(di) > 1 || Math.Abs(dj) > 1 || (di == 0 && dj == 0) || !on[b]) return false;
                if (di != 0 && dj != 0 && (on[SiteMap.Index(ai + di, aj)] || on[SiteMap.Index(ai, aj + dj)])) return false;
                return true;
            }
            var nbr = new List<int>[N];
            for (int c = 0; c < N; c++)
            {
                if (!on[c]) continue;
                var l = nbr[c] = new List<int>(4);
                int i = c % NX, j = c / NX;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= NX || nj >= NZ) continue;
                        int nc = SiteMap.Index(ni, nj);
                        if (Adj(c, nc)) l.Add(nc);
                    }
            }

            var chains = new List<List<int>>();
            var used = new HashSet<long>();
            long Key(int a, int b) => a < b ? (long)a * N + b : (long)b * N + a;
            void Walk(int start, int next)
            {
                var chain = new List<int> { start, next };
                used.Add(Key(start, next));
                int prev = start, cur = next;
                while (nbr[cur].Count == 2 && cur != start)
                {
                    int nx = nbr[cur][0] == prev ? nbr[cur][1] : nbr[cur][0];
                    if (!used.Add(Key(cur, nx))) break;
                    chain.Add(nx);
                    prev = cur; cur = nx;
                }
                chains.Add(chain);
            }
            for (int c = 0; c < N; c++)
                if (on[c] && nbr[c].Count != 2)
                    foreach (int n in nbr[c])
                        if (!used.Contains(Key(c, n))) Walk(c, n);
            for (int c = 0; c < N; c++) // loops with no junction on them
                if (on[c] && nbr[c].Count == 2 && !used.Contains(Key(c, nbr[c][0]))) Walk(c, nbr[c][0]);

            var curves = new List<PathCurve>();
            var px = new List<float>(); var pz = new List<float>(); var pt = new List<float>();
            foreach (var ch in chains)
            {
                px.Clear(); pz.Clear(); pt.Clear();
                foreach (int c in ch) { px.Add(SiteMap.CXc(c)); pz.Add(SiteMap.CZc(c)); pt.Add(tier[c]); }
                // Reach into the green where a chain ends beside it.
                for (int end = 0; end < 2; end++)
                {
                    int c = end == 0 ? ch[0] : ch[ch.Count - 1];
                    if (!NextToGreen(flags, c)) continue;
                    float x = SiteMap.CXc(c), z = SiteMap.CZc(c), dx = greenX - x, dz = greenZ - z, l = MathF.Sqrt(dx * dx + dz * dz) + 1e-6f;
                    float ex = x + dx / l * 0.09f, ez = z + dz / l * 0.09f;
                    if (end == 0) { px.Insert(0, ex); pz.Insert(0, ez); pt.Insert(0, tier[c]); }
                    else { px.Add(ex); pz.Add(ez); pt.Add(tier[c]); }
                }
                if (px.Count < 2) continue;
                SmoothTier(pt);
                var keep = Simplify(px, pz, SimplifyEps);
                // control points: the kept points, corners cut twice (Chaikin), each with its place along the chain
                var cx = new List<float>(); var cz = new List<float>(); var ci = new List<float>();
                foreach (int k in keep) { cx.Add(px[k]); cz.Add(pz[k]); ci.Add(k); }
                for (int it = 0; it < ChaikinRounds; it++) Chaikin(cx, cz, ci);
                var dense = new PathCurve();
                Spline(cx, cz, ci, pt, dense);
                var curve = Resample(dense, SampleStep);
                curve.startCap = curve.endCap = true;
                Relax(curve, SmoothRounds);
                if (curve.Count >= 2) curves.Add(curve);
            }
            return curves;
        }

        static bool NextToGreen(byte[] flags, int c)
        {
            int i = SiteMap.I(c), j = SiteMap.J(c);
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int ni = i + di, nj = j + dj;
                    if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                    if ((flags[SiteMap.Index(ni, nj)] & Snapshot.FGreen) != 0) return true;
                }
            return false;
        }

        /// <summary>A path doesn't jump from trail to road in one cell: average the tier over ±2 cells.</summary>
        static void SmoothTier(List<float> t)
        {
            var o = t.ToArray();
            for (int k = 0; k < t.Count; k++)
            {
                float s = 0; int n = 0;
                for (int q = Math.Max(0, k - 2); q <= Math.Min(t.Count - 1, k + 2); q++) { s += o[q]; n++; }
                t[k] = s / n;
            }
        }

        /// <summary>Zhang–Suen thinning in place (8-neighbourhood).</summary>
        public static void Thin(bool[] on)
        {
            const int NX = SiteMap.NX, NZ = SiteMap.NZ;
            var del = new List<int>();
            bool changed = true;
            bool P(int i, int j) => i >= 0 && j >= 0 && i < NX && j < NZ && on[SiteMap.Index(i, j)];
            while (changed)
            {
                changed = false;
                for (int step = 0; step < 2; step++)
                {
                    del.Clear();
                    for (int j = 0; j < NZ; j++)
                        for (int i = 0; i < NX; i++)
                        {
                            if (!on[SiteMap.Index(i, j)]) continue;
                            // p2..p9 clockwise from "north" (+j)
                            bool p2 = P(i, j + 1), p3 = P(i + 1, j + 1), p4 = P(i + 1, j), p5 = P(i + 1, j - 1),
                                 p6 = P(i, j - 1), p7 = P(i - 1, j - 1), p8 = P(i - 1, j), p9 = P(i - 1, j + 1);
                            int b = (p2 ? 1 : 0) + (p3 ? 1 : 0) + (p4 ? 1 : 0) + (p5 ? 1 : 0) + (p6 ? 1 : 0) + (p7 ? 1 : 0) + (p8 ? 1 : 0) + (p9 ? 1 : 0);
                            if (b < 2 || b > 6) continue;
                            int a = (!p2 && p3 ? 1 : 0) + (!p3 && p4 ? 1 : 0) + (!p4 && p5 ? 1 : 0) + (!p5 && p6 ? 1 : 0) +
                                    (!p6 && p7 ? 1 : 0) + (!p7 && p8 ? 1 : 0) + (!p8 && p9 ? 1 : 0) + (!p9 && p2 ? 1 : 0);
                            if (a != 1) continue;
                            if (step == 0 ? (p2 && p4 && p6) || (p4 && p6 && p8) : (p2 && p4 && p8) || (p2 && p6 && p8)) continue;
                            del.Add(SiteMap.Index(i, j));
                        }
                    foreach (int c in del) on[c] = false;
                    changed |= del.Count > 0;
                }
            }
        }

        /// <summary>Douglas–Peucker: indices of the points to keep (always the two ends).</summary>
        public static List<int> Simplify(List<float> x, List<float> z, float eps)
        {
            var keep = new bool[x.Count];
            keep[0] = keep[x.Count - 1] = true;
            var stack = new Stack<(int a, int b)>();
            stack.Push((0, x.Count - 1));
            while (stack.Count > 0)
            {
                var (a, b) = stack.Pop();
                float best = -1f; int bi = -1;
                float dx = x[b] - x[a], dz = z[b] - z[a], l2 = dx * dx + dz * dz;
                for (int k = a + 1; k < b; k++)
                {
                    float d;
                    if (l2 < 1e-9f) d = GrowthTerrain.Dist(x[k], z[k], x[a], z[a]);
                    else d = MathF.Abs((x[k] - x[a]) * dz - (z[k] - z[a]) * dx) / MathF.Sqrt(l2);
                    if (d > best) { best = d; bi = k; }
                }
                if (bi >= 0 && best > eps) { keep[bi] = true; stack.Push((a, bi)); stack.Push((bi, b)); }
            }
            var r = new List<int>();
            for (int k = 0; k < keep.Length; k++) if (keep[k]) r.Add(k);
            return r;
        }

        /// <summary>Even spacing along the curve (so a tight bend can't hide between two far-apart samples).</summary>
        static PathCurve Resample(PathCurve d, float step)
        {
            var r = new PathCurve();
            int n = d.Count;
            r.x.Add(d.x[0]); r.z.Add(d.z[0]); r.tier.Add(d.tier[0]);
            float carry = 0f;
            for (int k = 0; k + 1 < n; k++)
            {
                float seg = GrowthTerrain.Dist(d.x[k], d.z[k], d.x[k + 1], d.z[k + 1]);
                float pos = step - carry;
                while (pos <= seg)
                {
                    float u = seg > 1e-6f ? pos / seg : 0f;
                    r.x.Add(d.x[k] + (d.x[k + 1] - d.x[k]) * u); r.z.Add(d.z[k] + (d.z[k + 1] - d.z[k]) * u); r.tier.Add(d.tier[k] + (d.tier[k + 1] - d.tier[k]) * u);
                    pos += step;
                }
                carry = seg - (pos - step);
            }
            if (carry > step * 0.35f || r.Count < 2) { r.x.Add(d.x[n - 1]); r.z.Add(d.z[n - 1]); r.tier.Add(d.tier[n - 1]); }
            else { r.x[r.Count - 1] = d.x[n - 1]; r.z[r.Count - 1] = d.z[n - 1]; }
            return r;
        }

        /// <summary>Laplacian smoothing of the samples, the two ends fixed (they meet junctions and doors).</summary>
        static void Relax(PathCurve c, int rounds)
        {
            int n = c.Count;
            if (n < 3) return;
            var x = new float[n]; var z = new float[n];
            for (int it = 0; it < rounds; it++)
            {
                for (int k = 0; k < n; k++) { x[k] = c.x[k]; z[k] = c.z[k]; }
                for (int k = 1; k + 1 < n; k++) { c.x[k] = 0.5f * x[k] + 0.25f * (x[k - 1] + x[k + 1]); c.z[k] = 0.5f * z[k] + 0.25f * (z[k - 1] + z[k + 1]); }
            }
        }

        /// <summary>One round of Chaikin corner cutting; the two ends stay put.</summary>
        static void Chaikin(List<float> x, List<float> z, List<float> fi)
        {
            int n = x.Count;
            if (n < 3) return;
            var nx = new List<float> { x[0] }; var nz = new List<float> { z[0] }; var nf = new List<float> { fi[0] };
            for (int k = 0; k + 1 < n; k++)
            {
                if (k > 0) { nx.Add(0.75f * x[k] + 0.25f * x[k + 1]); nz.Add(0.75f * z[k] + 0.25f * z[k + 1]); nf.Add(0.75f * fi[k] + 0.25f * fi[k + 1]); }
                if (k + 1 < n - 1) { nx.Add(0.25f * x[k] + 0.75f * x[k + 1]); nz.Add(0.25f * z[k] + 0.75f * z[k + 1]); nf.Add(0.25f * fi[k] + 0.75f * fi[k + 1]); }
            }
            nx.Add(x[n - 1]); nz.Add(z[n - 1]); nf.Add(fi[n - 1]);
            x.Clear(); x.AddRange(nx); z.Clear(); z.AddRange(nz); fi.Clear(); fi.AddRange(nf);
        }

        /// <summary>Centripetal Catmull–Rom through the control points, sampled about every <see cref="SampleStep"/>.
        /// Tier is interpolated along the original chain (each control point knows where it came from).</summary>
        static void Spline(List<float> x, List<float> z, List<float> ci, List<float> t, PathCurve into)
        {
            int n = x.Count;
            float TierAt(float fi)
            {
                int lo = Math.Max(0, Math.Min(t.Count - 1, (int)fi)); float fr = fi - lo;
                return lo + 1 < t.Count ? t[lo] + (t[lo + 1] - t[lo]) * fr : t[lo];
            }
            (float x, float z) Pt(int k)
            {
                if (k < 0) // mirror the first segment
                {
                    var p0 = Pt(0); var p1 = Pt(1);
                    return (2 * p0.x - p1.x, 2 * p0.z - p1.z);
                }
                if (k >= n)
                {
                    var p0 = Pt(n - 1); var p1 = Pt(n - 2);
                    return (2 * p0.x - p1.x, 2 * p0.z - p1.z);
                }
                return (x[k], z[k]);
            }
            for (int s = 0; s < n - 1; s++)
            {
                var p0 = Pt(s - 1); var p1 = Pt(s); var p2 = Pt(s + 1); var p3 = Pt(s + 2);
                float t0 = 0f, t1 = t0 + Knot(p0, p1), t2 = t1 + Knot(p1, p2), t3 = t2 + Knot(p2, p3);
                float len = GrowthTerrain.Dist(p1.x, p1.z, p2.x, p2.z);
                int steps = Math.Max(4, (int)MathF.Ceiling(3f * len / SampleStep)); // dense; resampled evenly after
                for (int q = 0; q < steps; q++)
                {
                    float u = q / (float)steps, tt = t1 + (t2 - t1) * u;
                    var p = CR(p0, p1, p2, p3, t0, t1, t2, t3, tt);
                    into.x.Add(p.x); into.z.Add(p.z); into.tier.Add(TierAt(ci[s] + (ci[s + 1] - ci[s]) * u));
                }
            }
            var last = Pt(n - 1);
            into.x.Add(last.x); into.z.Add(last.z); into.tier.Add(TierAt(ci[n - 1]));
        }

        static float Knot((float x, float z) a, (float x, float z) b) => MathF.Max(1e-4f, MathF.Sqrt(GrowthTerrain.Dist(a.x, a.z, b.x, b.z)));

        static (float x, float z) CR((float x, float z) p0, (float x, float z) p1, (float x, float z) p2, (float x, float z) p3,
            float t0, float t1, float t2, float t3, float t)
        {
            (float, float) L((float x, float z) a, (float x, float z) b, float ta, float tb)
            {
                float w = (t - ta) / (tb - ta);
                return (a.x + (b.x - a.x) * w, a.z + (b.z - a.z) * w);
            }
            var a1 = L(p0, p1, t0, t1); var a2 = L(p1, p2, t1, t2); var a3 = L(p2, p3, t2, t3);
            var b1 = L(a1, a2, t0, t2); var b2 = L(a2, a3, t1, t3);
            return L(b1, b2, t1, t2);
        }
    }
}
