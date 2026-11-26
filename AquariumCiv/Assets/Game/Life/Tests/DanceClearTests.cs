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
