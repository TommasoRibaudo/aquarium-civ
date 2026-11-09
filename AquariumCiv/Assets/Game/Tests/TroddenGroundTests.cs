using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#523 (playtest #500): no weed, vine or sapling on the camp's trodden ground. The weed amount is the sim's overgrowth (untouched
    /// here); where a patch stands is the view's pick, which asks <see cref="SimCore.IsTroddenGround"/>. So the sim test checks that
    /// question over two sim years and that plenty of open ground is left, so the patches (same count as before) land elsewhere.</summary>
    public class TroddenGroundTests
    {
        static readonly (int seed, int preset)[] Runs = { (7, 0), (11, 1), (1, 2) };

        [Test]
        public void TheFireRingAndEveryLotAreTroddenForTwoYearsAndOpenGroundIsLeft([ValueSource(nameof(Runs))] (int seed, int preset) run)
        {
            var cfg = new SimConfig { terrainPreset = run.preset, settle = true, buildingSlots = 20 };
            var sim = new SimCore(cfg, SimCore.NewWorld(run.seed, cfg));
            int checkedLots = 0;
            while (sim.State.month < 24)
            {
                sim.Step();
                sim.State.simHours = sim.State.month * sim.Config.StepHours;
                if (sim.State.eraReady) sim.AdvanceEra();
                var map = sim.Settlement.Map;
                sim.Settlement.Fire(out float fx, out float fz, out bool lit);
                // the fire's ring: every point inside it, while the fire burns
                if (lit)
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * 0.3927f;
                        foreach (float r in new[] { 0f, 0.5f, 1.0f, SimCore.FireClearRadius - 0.02f })
                            Assert.IsTrue(sim.IsTroddenGround(fx + r * (float)System.Math.Cos(a), fz + r * (float)System.Math.Sin(a)), $"fire ring r={r} month {sim.State.month}");
                    }
                // every lot cell
                for (int c = 0; c < SiteMap.N; c++)
                    if (map.lot[c] != 0) { Assert.IsTrue(sim.IsTroddenGround(SiteMap.CXc(c), SiteMap.CZc(c)), $"lot cell {c}"); checkedLots++; }
            }
            Assert.Greater(checkedLots, 0, "the settlement had lots");
            // open ground is left: at least half the box (the weeds' share goes elsewhere, the count is the overgrowth's and unchanged)
            int open = 0;
            for (int c = 0; c < SiteMap.N; c++) if (!sim.IsTroddenGround(SiteMap.CXc(c), SiteMap.CZc(c))) open++;
            Assert.Greater(open, SiteMap.N / 2, $"open cells {open}/{SiteMap.N}");
        }

        [Test]
        public void NothingIsTroddenOutsideASettledBox()
        {
            var cfg = new SimConfig { settle = false };
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.IsFalse(sim.IsTroddenGround(0f, 0f));
        }
    }
}
