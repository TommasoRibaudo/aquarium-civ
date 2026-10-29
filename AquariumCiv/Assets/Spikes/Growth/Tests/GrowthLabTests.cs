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
            var cfg = new SimConfig { buildingSlots = w.cfg.buildingSlots };
            var bare = new SimCore(cfg, SimCore.NewWorld(7, cfg));
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
    }
}
