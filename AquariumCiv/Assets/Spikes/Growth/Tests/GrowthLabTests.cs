using System.Collections.Generic;
using System.Diagnostics;
using AquariumCiv.Sim;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace AquariumCiv.Spikes.Growth.Tests
{
    /// <summary>
    /// The Growth Lab spike's pass criteria 2–6 (research 08 §5), plus the geometry it relies on. Numbers go to the
    /// console with a [GrowthLab] tag so they can be copied into growth-lab.md.
    /// </summary>
    public class GrowthLabTests
    {
        static readonly (int seed, int preset)[] Runs = { (7, 0), (11, 1), (1, 2) };

        static readonly Dictionary<(int, int, Variant), GrowthWorld> cache = new();

        static GrowthWorld Run(int seed, int preset, Variant v = Variant.Organic)
        {
            if (cache.TryGetValue((seed, preset, v), out var w)) return w;
            w = new GrowthWorld(seed, preset, v, GrowthChecks.DefaultScript(preset));
            w.RunToYear(120);
            cache[(seed, preset, v)] = w;
            return w;
        }

        // ---------- geometry ----------
        [Test]
        public void RectGap_DetectsOverlapAndGap()
        {
            Assert.Less(SiteMap.RectGap(0, 0, 0, 1, 1, 0.5f, 0, 30, 1, 1), 0f);
            Assert.AreEqual(0.5f, SiteMap.RectGap(0, 0, 0, 1, 1, 1.5f, 0, 0, 1, 1), 1e-4f);
            Assert.Greater(SiteMap.RectGap(0, 0, 45, 1, 1, 1.3f, 0, 0, 1, 1), 0f);
        }

        [Test]
        public void RectCells_MatchesRotatedArea()
        {
            var cells = new List<int>();
            SiteMap.RectCells(0.05f, 0.05f, 0f, 0.5f, 0.3f, 0f, cells);
            Assert.AreEqual(15, cells.Count, "0.5 × 0.3 at 0.1 cells");
            SiteMap.RectCells(0.05f, 0.05f, 37f, 0.5f, 0.3f, 0f, cells);
            Assert.That(cells.Count, Is.InRange(12, 18));
        }

        [Test]
        public void Terrain_Preset0_IsTheStyleKitBox()
        {
            var t = TerrainSpec.Preset(0);
            for (float x = -2.9f; x < 3f; x += 0.37f)
                for (float z = -2.1f; z < 2.2f; z += 0.41f)
                    Assert.AreEqual(StyleKit.StyleBox.Height(x, z), GrowthTerrain.Height(t, x, z), 1e-4f, $"at {x}, {z}");
        }

        [Test]
        public void SimCore_IsUnchanged_ByThePlacementLayer()
        {
            // No nudges: the economy under the spike is the same economy run on its own.
            var w = new GrowthWorld(7, 0, Variant.Organic) { keepSnapshots = false };
            w.RunToYear(60);
            var cfg = new SimConfig { buildingSlots = w.cfg.buildingSlots, settle = false };
            var bareState = SimCore.NewWorld(7, cfg);
            bareState.era = AquariumCiv.Sim.Era.Town; // as GrowthWorld: SimCore's era gate open
            var bare = new SimCore(cfg, bareState);
            for (int m = 0; m < 60 * 12; m++) bare.Step();
            Assert.AreEqual(bare.State.population, w.State.population);
            Assert.AreEqual(bare.State.buildings.Count, w.State.buildings.Count);
            Assert.AreEqual(bare.State.wood, w.State.wood);
            Assert.AreEqual(bare.State.chronicle.Count, w.State.chronicle.Count);
        }

        // ---------- criterion 3: live = catch-up ----------
        [Test]
        public void Criterion3_LiveStepping_EqualsCatchUp()
        {
            var script = GrowthChecks.DefaultScript(0);
            var a = new GrowthWorld(7, 0, Variant.Organic, script) { keepSnapshots = false };
            var b = new GrowthWorld(7, 0, Variant.Organic, script) { keepSnapshots = false };
            var c = new GrowthWorld(7, 0, Variant.Organic, script) { keepSnapshots = false };
            a.RunToYear(10); b.RunToYear(10); c.RunToYear(10);
            for (int m = 0; m < 576; m++) a.Advance(a.cfg.StepHours);      // live: month by month
            b.Advance(48.0);                                                // catch-up: one call
            for (int k = 0; k < 48 * 7; k++) c.Advance(1.0 / 7.0);          // live: odd-sized frames
            Assert.AreEqual(a.State.month, b.State.month);
            Assert.AreEqual(a.Fingerprint(), b.Fingerprint(), "month by month vs Advance(48 h)");
            Assert.AreEqual(a.Fingerprint(), c.Fingerprint(), "small frames vs month by month");
            Debug.Log($"[GrowthLab] C3 live = catch-up over 576 months: identical ({a.sites.Count} sites, era {a.era})");
        }

        // ---------- criterion 4: sanity over 3 × 120 years ----------
        [Test]
        public void Criterion4_Sanity_3x120Years()
        {
            foreach (var (seed, preset) in Runs)
            {
                var w = Run(seed, preset);
                var s = GrowthChecks.Check(w);
                Debug.Log($"[GrowthLab] C4 seed {seed}/preset {preset}: {w.sites.Count} buildings at year 120 (era {w.era}, pop {w.State.population}); {s}");
                Assert.AreEqual(0, s.overlaps, "overlapping lots");
                Assert.AreEqual(0, s.inMargin, "lots in the glass margin");
                Assert.AreEqual(0, s.unconnected, "lots without a path to the green");
                Assert.GreaterOrEqual(w.sites.Count, 35, "buildings that fit");
            }
        }

        [Test]
        public void Criterion4_LotsNeverMove()
        {
            var w = Run(7, 0);
            var first = new Dictionary<int, Site>();
            foreach (var snap in w.snapshots)
                foreach (var s in snap.sites)
                {
                    if (!first.TryGetValue(s.id, out var f)) { first[s.id] = s; continue; }
                    Assert.AreEqual(f.x, s.x); Assert.AreEqual(f.z, s.z); Assert.AreEqual(f.yaw, s.yaw);
                    Assert.AreEqual(f.w, s.w); Assert.AreEqual(f.d, s.d);
                }
        }

        // ---------- criterion 5: emergence ----------
        [Test]
        public void Criterion5_PathsEmerge_ThatNoConnectionLaid()
        {
            foreach (var (seed, preset) in Runs)
            {
                var w = Run(seed, preset);
                var e = GrowthChecks.Emerge(w);
                Debug.Log($"[GrowthLab] C5 seed {seed}/preset {preset}: {e}");
                Assert.GreaterOrEqual(e.largestEmergent, 5, "a path at least 0.5 u long that no connection step laid");
                Assert.Greater(e.roadCells, 0, "a main street worn into a road by the Town era");
            }
        }

        // ---------- criterion 2: the nudge answer is visible ----------
        [Test]
        public void Criterion2_NudgeAnswers_Within1Unit()
        {
            const int n = 30;
            foreach (var kind in SimCore.NudgeKind.All)
            {
                int ok = 0;
                var sum = 0f;
                for (int i = 0; i < n; i++)
                {
                    var t = GrowthChecks.RunNudgeTrial(kind, i);
                    if (t.answered && t.dist <= 1f) ok++;
                    if (t.answered) sum += t.dist;
                }
                Debug.Log($"[GrowthLab] C2 {kind}: {ok}/{n} answers within 1.0 u of the shaped spot ({100f * ok / n:0}%), mean distance {sum / n:0.00} u");
                Assert.GreaterOrEqual(ok, (int)System.Math.Ceiling(0.8 * n), kind);
            }
        }

        // ---------- criterion 6: cost ----------
        [Test]
        public void Criterion6_Cost()
        {
            // Warm up (JIT), then measure a full 120-year run and a 576-month catch-up from year 60.
            new GrowthWorld(3, 0, Variant.Organic) { keepSnapshots = false }.RunToYear(5);
            var w = new GrowthWorld(7, 0, Variant.Organic, GrowthChecks.DefaultScript(0)) { keepSnapshots = false };
            w.RunToYear(120);
            double f = 1000.0 / Stopwatch.Frequency;
            double perMonth = (w.placeTicks + w.wearTicks) * f / w.monthsStepped;
            var c = new GrowthWorld(11, 1, Variant.Organic, GrowthChecks.DefaultScript(1)) { keepSnapshots = false };
            c.RunToYear(60);
            var sw = Stopwatch.StartNew();
            c.Advance(48.0);
            double catchUp = sw.Elapsed.TotalMilliseconds;
            Debug.Log($"[GrowthLab] C6 placement {w.placeTicks * f / w.monthsStepped:0.000} + wear {w.wearTicks * f / w.monthsStepped:0.000} = {perMonth:0.000} ms per month (SimCore itself {w.simTicks * f / w.monthsStepped:0.000}); 576-month catch-up from year 60: {catchUp:0} ms");
            Assert.Less(perMonth, 1.0, "placement + wear per month");
            Assert.Less(catchUp, 300.0, "576-month catch-up");
        }

        // ================= Round 2: the Cluster variant (family clusters round shared yards) =================

        [Test]
        public void Cluster_Criterion2_NudgeAnswers_Within1Unit()
        {
            const int n = 30;
            foreach (var kind in SimCore.NudgeKind.All)
            {
                int ok = 0;
                var sum = 0f;
                for (int i = 0; i < n; i++)
                {
                    var t = GrowthChecks.RunNudgeTrial(kind, i, Variant.Cluster);
                    if (t.answered && t.dist <= 1f) ok++;
                    if (t.answered) sum += t.dist;
                }
                Debug.Log($"[GrowthLab] Cluster C2 {kind}: {ok}/{n} answers within 1.0 u of the shaped spot ({100f * ok / n:0}%), mean distance {sum / n:0.00} u");
                Assert.GreaterOrEqual(ok, (int)System.Math.Ceiling(0.8 * n), kind);
            }
        }

        [Test]
        public void Cluster_Criterion3_LiveStepping_EqualsCatchUp()
        {
            var script = GrowthChecks.DefaultScript(0);
            var a = new GrowthWorld(7, 0, Variant.Cluster, script) { keepSnapshots = false };
            var b = new GrowthWorld(7, 0, Variant.Cluster, script) { keepSnapshots = false };
            var c = new GrowthWorld(7, 0, Variant.Cluster, script) { keepSnapshots = false };
            a.RunToYear(10); b.RunToYear(10); c.RunToYear(10);
            for (int m = 0; m < 576; m++) a.Advance(a.cfg.StepHours);
            b.Advance(48.0);
            for (int k = 0; k < 48 * 7; k++) c.Advance(1.0 / 7.0);
            Assert.AreEqual(a.State.month, b.State.month);
            Assert.AreEqual(a.Fingerprint(), b.Fingerprint(), "month by month vs Advance(48 h)");
            Assert.AreEqual(a.Fingerprint(), c.Fingerprint(), "small frames vs month by month");
            Assert.AreEqual(a.clusters.Count, b.clusters.Count, "the same families");
            Debug.Log($"[GrowthLab] Cluster C3 live = catch-up over 576 months: identical ({a.sites.Count} sites, {a.clusters.Count} clusters, era {a.era})");
        }

        [Test]
        public void Cluster_Criterion4_Sanity_3x120Years()
        {
            foreach (var (seed, preset) in Runs)
            {
                var w = Run(seed, preset, Variant.Cluster);
                var s = GrowthChecks.Check(w);
                int familyHomes = 0, homes = 0;
                foreach (var site in w.sites)
                    if (ClusterPlacer.Clustered(site.kind)) { homes++; if (site.cluster > 0 && w.clusters[site.cluster - 1].count >= 2) familyHomes++; }
                Debug.Log($"[GrowthLab] Cluster C4 seed {seed}/preset {preset}: {w.sites.Count} buildings at year 120 (era {w.era}, pop {w.State.population}); {s}; " +
                          $"{w.clusters.Count} clusters, {familyHomes}/{homes} huts+cottages in a family of 2+");
                Assert.AreEqual(0, s.overlaps, "overlapping lots");
                Assert.AreEqual(0, s.inMargin, "lots in the glass margin");
                Assert.AreEqual(0, s.unconnected, "lots without a path to the green");
                Assert.GreaterOrEqual(w.sites.Count, 35, "buildings that fit");
                Assert.Greater(w.clusters.Count, 0, "family clusters");
                // Commons and yards stay open for good.
                for (int c = 0; c < SiteMap.N; c++)
                {
                    if (w.map.commons[c]) Assert.AreEqual(0, w.map.lot[c], "a lot on the commons");
                    if (w.map.yard[c]) Assert.AreEqual(0, w.map.lot[c], "a lot on a yard");
                }
            }
        }

        [Test]
        public void Cluster_Criterion4_LotsAndYardsNeverMove()
        {
            var w = Run(7, 0, Variant.Cluster);
            var first = new Dictionary<int, Site>();
            var yards = new Dictionary<int, Cluster>();
            foreach (var snap in w.snapshots)
            {
                foreach (var s in snap.sites)
                {
                    if (!first.TryGetValue(s.id, out var f)) { first[s.id] = s; continue; }
                    Assert.AreEqual(f.x, s.x); Assert.AreEqual(f.z, s.z); Assert.AreEqual(f.yaw, s.yaw);
                    Assert.AreEqual(f.w, s.w); Assert.AreEqual(f.d, s.d); Assert.AreEqual(f.cluster, s.cluster);
                }
                foreach (var k in snap.clusters)
                {
                    if (!yards.TryGetValue(k.id, out var y)) { yards[k.id] = k; continue; }
                    Assert.AreEqual(y.x, k.x); Assert.AreEqual(y.z, k.z); Assert.AreEqual(y.r, k.r);
                }
            }
        }

        [Test]
        public void Cluster_Criterion5_PathsEmerge_ThatNoConnectionLaid()
        {
            foreach (var (seed, preset) in Runs)
            {
                var w = Run(seed, preset, Variant.Cluster);
                var e = GrowthChecks.Emerge(w);
                Debug.Log($"[GrowthLab] Cluster C5 seed {seed}/preset {preset}: {e}");
                Assert.GreaterOrEqual(e.largestEmergent, 5, "a path at least 0.5 u long that no connection step laid");
                Assert.Greater(e.roadCells, 0, "a main street worn into a road by the Town era");
            }
        }

        [Test]
        public void Cluster_Criterion6_Cost()
        {
            new GrowthWorld(3, 0, Variant.Cluster) { keepSnapshots = false }.RunToYear(5);
            var w = new GrowthWorld(7, 0, Variant.Cluster, GrowthChecks.DefaultScript(0)) { keepSnapshots = false };
            w.RunToYear(120);
            double f = 1000.0 / Stopwatch.Frequency;
            double perMonth = (w.placeTicks + w.wearTicks) * f / w.monthsStepped;
            var c = new GrowthWorld(11, 1, Variant.Cluster, GrowthChecks.DefaultScript(1)) { keepSnapshots = false };
            c.RunToYear(60);
            var sw = Stopwatch.StartNew();
            c.Advance(48.0);
            double catchUp = sw.Elapsed.TotalMilliseconds;
            Debug.Log($"[GrowthLab] Cluster C6 placement {w.placeTicks * f / w.monthsStepped:0.000} + wear {w.wearTicks * f / w.monthsStepped:0.000} = {perMonth:0.000} ms per month (SimCore itself {w.simTicks * f / w.monthsStepped:0.000}); 576-month catch-up from year 60: {catchUp:0} ms");
            Assert.Less(perMonth, 1.0, "placement + wear per month");
            Assert.Less(catchUp, 300.0, "576-month catch-up");
        }

        [Test]
        public void Cluster_HomesDontLineUp()
        {
            foreach (var (seed, preset) in Runs)
            {
                var o = GrowthChecks.Align(Run(seed, preset, Variant.Organic).sites);
                var k = GrowthChecks.Align(Run(seed, preset, Variant.Cluster).sites);
                var r = GrowthChecks.Align(Run(seed, preset, Variant.Random).sites);
                Debug.Log($"[GrowthLab] Rows seed {seed}: Organic {o} | Cluster {k} | Random {r}");
                Assert.Less(k.ParallelShare, o.ParallelShare * 0.6f, "cluster homes are far less often parallel to a neighbour than round 1's rows");
                Assert.LessOrEqual(k.InLineShare, 0.05f, "no street rows (fronts in line)");
            }
        }

        [Test]
        public void PathCurves_FollowThePaths_AndBendSmoothly()
        {
            var w = Run(7, 0, Variant.Cluster);
            var snap = w.snapshots[110];
            var curves = PathCurves.Build(snap.tier, snap.flags, w.map.greenX, w.map.greenZ);
            Assert.Greater(curves.Count, 0);
            int covered = 0, cells = 0;
            float worst = 0f;
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (snap.tier[c] < PathNet.PathTier || (snap.flags[c] & Snapshot.FGreen) != 0) continue;
                cells++;
                float best = 9f;
                foreach (var cv in curves)
                    for (int k = 0; k < cv.Count; k++)
                        best = System.Math.Min(best, GrowthTerrain.Dist(cv.x[k], cv.z[k], SiteMap.CXc(c), SiteMap.CZc(c)));
                if (best <= 0.15f) covered++;
            }
            foreach (var cv in curves)
                for (int k = 1; k + 1 < cv.Count; k++)
                {
                    float ax = cv.x[k] - cv.x[k - 1], az = cv.z[k] - cv.z[k - 1], bx = cv.x[k + 1] - cv.x[k], bz = cv.z[k + 1] - cv.z[k];
                    float la = (float)System.Math.Sqrt(ax * ax + az * az), lb = (float)System.Math.Sqrt(bx * bx + bz * bz);
                    if (la < 0.008f || lb < 0.008f) continue;
                    float cos = (ax * bx + az * bz) / (la * lb);
                    worst = System.Math.Max(worst, (float)(System.Math.Acos(System.Math.Min(1.0, System.Math.Max(-1.0, cos))) * 180.0 / System.Math.PI));
                }
            Debug.Log($"[GrowthLab] Curves seed 7 year 110: {curves.Count} curves cover {covered}/{cells} path cells within 0.15 u; sharpest bend between samples {worst:0} deg");
            Assert.GreaterOrEqual(covered, (int)(0.9f * cells), "the curves follow the worn paths");
            Assert.Less(worst, 45f, "no kinks between samples");
        }
    }
}
