using System;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>
    /// #770 (playtest 1): in the later eras the dance ring stood in the way of the buildings. The ring is held on open ground: the
    /// pure search (<see cref="OpenGround.RingCentre"/>) and the real festival and wedding plans on a grown village and a grown town,
    /// several seeds: no ring sample (the middle, the step-in, the edge) is inside a building's footprint.
    /// </summary>
    public class DanceClearTests
    {
        // ---------------------------------------------------------------- the pure search
        static bool Houses(float x, float z) => Math.Abs(x - 0.3f) < 0.4f && Math.Abs(z) < 0.4f; // a square lot just off the middle

        [Test]
        public void ARingThatMeetsALot_MovesToTheNearestClearCentre_AndAClearOneStays()
        {
            Assert.AreEqual((2f, 2f), OpenGround.RingCentre(2f, 2f, 0.5f, Houses));
            Assert.Greater(OpenGround.RingHits(0f, 0f, 0.5f, Houses), 0, "the ring at the origin meets the lot");
            var (x, z) = OpenGround.RingCentre(0f, 0f, 0.5f, Houses);
            Assert.AreEqual(0, OpenGround.RingHits(x, z, 0.5f, Houses), $"({x:0.00},{z:0.00}) is clear");
            Assert.Less(Math.Sqrt(x * x + z * z), 1.6f);
        }

        [Test]
        public void TheSearchRespectsTheBox_AndGivesTheLeastBlockedWhenNothingIsClear()
        {
            Func<float, float, bool> all = (x, z) => true;
            var (cx, cz) = OpenGround.RingCentre(0f, 0f, 0.5f, all);
            Assert.IsTrue(float.IsFinite(cx) && float.IsFinite(cz));
            var (bx, bz) = OpenGround.RingCentre(0f, 0f, 0.5f, Houses, (x, z) => x < 0.1f); // only the left side is allowed
            Assert.Less(bx, 0.1f);
            Assert.AreEqual(0, OpenGround.RingHits(bx, bz, 0.5f, Houses));
        }

        // ---------------------------------------------------------------- the real plans
        static SimCore Grown(int seed, Era era, int extraYears)
        {
            var cfg = new SimConfig { settle = true, moments = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            long endExtra = -1;
            for (int guard = 0; guard < 12 * 140; guard++)
            {
                sim.Step();
                sim.State.simHours = sim.State.month * sim.Config.StepHours;
                if (sim.State.eraReady) sim.AdvanceEra();
                if (sim.State.era == era)
                {
                    if (endExtra < 0) endExtra = sim.State.month + 12L * extraYears;
                    if (sim.State.month >= endExtra) break;
                }
            }
            Assert.AreEqual(era, sim.State.era, "grown to the era");
            return sim;
        }

        static void AssertRingClear(SimCore sim, MomentKind kind, string what)
        {
            var m = sim.DebugQueueMoment(kind);
            if (m == null) { Assert.Fail($"{what}: no {kind} could be queued"); return; }
            var planner = LifePlanner.Wired(sim);
            var at = new ViewTime(sim.State.month, 10f);
            var sc = planner.StageHeldMoment(m, at);
            Assert.IsNotNull(sc, $"{what}: {planner.LastHeldWhyNot}");
            var plan = sc.plan;
            var blocked = LifePlanner.FootprintBlocked(sim);
            AssertNoLotInRing(sim, plan.centreX, plan.centreZ, plan.ringRadius, $"{what} {kind}");
            Assert.IsNotNull(blocked, "a settled box");
            Assert.Greater(plan.ringKeys.Count, 0, $"{what}: a ring dances");
            // Every dancer's place on the ring, turning, stepped in and out: the ring's samples, never inside a footprint.
            int hits = OpenGround.RingHits(plan.centreX, plan.centreZ, plan.ringRadius, blocked);
            Assert.AreEqual(0, hits, $"{what}: the {kind} ring at ({plan.centreX:0.00},{plan.centreZ:0.00}) r {plan.ringRadius:0.00} meets {hits} footprint points");
            for (int i = 0; i < plan.ringKeys.Count; i++)
                for (float turn = 0f; turn < 360f; turn += 15f)
                {
                    double a = (i * 360.0 / plan.ringKeys.Count + turn) * Math.PI / 180.0;
                    foreach (float k in new[] { 0.6f, 1f })
                    {
                        float x = plan.centreX + (float)Math.Cos(a) * plan.ringRadius * k, z = plan.centreZ + (float)Math.Sin(a) * plan.ringRadius * k;
                        Assert.IsFalse(blocked(x, z), $"{what}: a dancer at ({x:0.00},{z:0.00}) is inside a footprint");
                    }
                }
        }

        /// <summary>#770 round 2: no building's lot rectangle (well, market, houses, church) has a corner or its middle inside the ring, or the ring on it.</summary>
        static void AssertNoLotInRing(SimCore sim, float cx, float cz, float r, string what)
        {
            foreach (var b in sim.State.buildings)
            {
                if (b.lotId == 0) continue;
                float yr = b.yaw * (float)Math.PI / 180f, co = (float)Math.Cos(yr), si = (float)Math.Sin(yr);
                float hw = 0.5f * b.w, hd = 0.5f * b.d;
                // the nearest point of the (turned) rectangle to the ring's centre must be farther than the ring
                float dx = cx - b.x, dz = cz - b.z;
                float lx = dx * co + dz * si, lz = -dx * si + dz * co;
                float nx = Math.Max(-hw, Math.Min(hw, lx)), nz = Math.Max(-hd, Math.Min(hd, lz));
                float dist = (float)Math.Sqrt((lx - nx) * (lx - nx) + (lz - nz) * (lz - nz));
                // yaw sign is read both ways: the nearer reading must clear the ring
                float lx2 = dx * co - dz * si, lz2 = dx * si + dz * co;
                float nx2 = Math.Max(-hw, Math.Min(hw, lx2)), nz2 = Math.Max(-hd, Math.Min(hd, lz2));
                float dist2 = (float)Math.Sqrt((lx2 - nx2) * (lx2 - nx2) + (lz2 - nz2) * (lz2 - nz2));
                Assert.GreaterOrEqual(Math.Min(dist, dist2), r, $"{what}: the {b.type} lot at ({b.x:0.00},{b.z:0.00}) {b.w:0.00}x{b.d:0.00} is inside the ring at ({cx:0.00},{cz:0.00}) r {r:0.00}");
            }
        }

        /// <summary>The kids' ring dance: the view picks its centre with the same search (OpenGround.RingFit) on the same footprint test.</summary>
        [TestCaseSource(nameof(KidCases))]
        public void TheKidsRingDance_KeepsOffEveryLot(int seed, Era era)
        {
            var sim = Grown(seed, era, 6);
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.Positions.TryGetPosition(planner.GatherPlace, out float gx, out float gz), "a gathering place");
            var blocked = LifePlanner.FootprintBlocked(sim);
            foreach (int n in new[] { 4, 8, 14 })
            {
                float R = 0.3f + 0.03f * Math.Max(0, n - 6);
                var c = OpenGround.RingFit(gx, gz, R, Math.Max(0.3f, 0.7f * R), blocked, null, 2.2f);
                AssertNoLotInRing(sim, c.x, c.z, c.r, $"{era} seed {seed} kids x{n}");
                Assert.AreEqual(0, OpenGround.RingHits(c.x, c.z, c.r, blocked));
            }
        }

        /// <summary>#770 round 3: the festival dressing is laid round the ring: poles outside it, tables clear of the dancers, none on a lot.</summary>
        [TestCaseSource(nameof(KidCases))]
        public void TheFestivalDressing_StandsOutsideTheRing_AndOffEveryLot(int seed, Era era)
        {
            var sim = Grown(seed, era, 6);
            var blocked = LifePlanner.FootprintBlocked(sim);
            foreach (bool staged in new[] { false, true })
            {
                FestivalLayout.Ring = null;
                try
                {
                    if (staged)
                    {
                        var m = sim.DebugQueueMoment(MomentKind.Festival);
                        if (m == null) continue;
                        var sc = LifePlanner.Wired(sim).StageHeldMoment(m, new ViewTime(sim.State.month, 10f));
                        Assert.IsNotNull(sc);
                        Assert.IsTrue(FestivalLayout.Ring.HasValue, "staging sets the ring");
                        Assert.AreEqual(sc.plan.centreX, FestivalLayout.Ring.Value.x, 1e-4f);
                    }
                    var lay = FestivalLayout.Compute(sim);
                    string w = $"{era} seed {seed} staged {staged}";
                    Assert.Greater(lay.poles.Count, 3, w + ": poles stand");
                    Assert.AreEqual(2, lay.tables.Count, w + ": two tables");
                    foreach (var (x, z) in lay.poles)
                    {
                        Assert.IsFalse(blocked(x, z), $"{w}: a pole at ({x:0.00},{z:0.00}) is on a lot");
                        Assert.GreaterOrEqual(Math.Sqrt((x - lay.cx) * (x - lay.cx) + (z - lay.cz) * (z - lay.cz)), lay.ringR, w + ": a pole is inside the ring");
                    }
                    foreach (var (x, z) in lay.tables)
                    {
                        for (int i = -2; i <= 2; i++)
                            for (int j = -1; j <= 1; j++)
                                Assert.IsFalse(blocked(x + FestivalLayout.TableLen * 0.5f * i / 2f, z + FestivalLayout.TableW * 0.5f * j), $"{w}: a table at ({x:0.00},{z:0.00}) is on a lot");
                        float nearest = (float)Math.Sqrt((x - lay.cx) * (x - lay.cx) + (z - lay.cz) * (z - lay.cz)) - FestivalLayout.TableLen * 0.5f;
                        Assert.GreaterOrEqual(nearest, lay.ringR, w + ": a table is among the dancers");
                    }
                    if (staged) { Assert.AreEqual(FestivalLayout.Ring.Value.x, lay.cx, 1e-4f, "dressed round the dancers' ring"); }
                }
                finally { FestivalLayout.Ring = null; }
            }
        }

        static System.Collections.Generic.IEnumerable<TestCaseData> KidCases()
        {
            foreach (var era in new[] { Era.Village, Era.Town })
                foreach (int seed in new[] { 3, 7, 11, 19 })
                    yield return new TestCaseData(seed, era).SetName($"KidsRing_{era}_seed{seed}");
        }

        static System.Collections.Generic.IEnumerable<TestCaseData> Cases()
        {
            foreach (var era in new[] { Era.Village, Era.Town })
                foreach (int seed in new[] { 3, 7, 11, 19 })
                    foreach (var kind in new[] { MomentKind.Festival, MomentKind.Wedding })
                        yield return new TestCaseData(seed, era, kind).SetName($"NoDancerInsideABuilding_{era}_seed{seed}_{kind}");
        }

        [TestCaseSource(nameof(Cases))]
        public void NoDancerStandsInsideABuildingFootprint(int seed, Era era, MomentKind kind)
        {
            var sim = Grown(seed, era, 6);
            AssertRingClear(sim, kind, $"{era} seed {seed}");
        }
    }
}
