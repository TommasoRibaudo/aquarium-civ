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
            var cfg = new SimConfig { terrainPreset = run.preset, settle = true, buildingSlots = 20, ecology = true };
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
                // #523: no vine stands on trodden ground, and VineLoad counts only what is left on open ground
                if (sim.EcologyOn)
                {
                    foreach (int vc in sim.EcoVine) Assert.IsFalse(sim.IsTroddenGround(SiteMap.CXc(vc), SiteMap.CZc(vc)), $"vine on trodden cell {vc} month {sim.State.month}");
                    Assert.AreEqual(System.Math.Min(SimCore.VineLoadMax, sim.EcoVine.Count * SimCore.VineLoadPerCell), sim.VineLoad, 1e-6f);
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
        public void AVineOnALotIsPulledAtOnceAndNeverCounts()
        {
            var cfg = new SimConfig { terrainPreset = 0, settle = true, buildingSlots = 20, ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            for (int m = 0; m < 24; m++) { sim.Step(); sim.State.simHours = sim.State.month * sim.Config.StepHours; if (sim.State.eraReady) sim.AdvanceEra(); }
            if (!sim.EcologyOn) Assert.Inconclusive("ecology off");
            int lotCell = -1;
            for (int c = 0; c < SiteMap.N && lotCell < 0; c++) if (sim.Settlement.Map.lot[c] != 0) lotCell = c;
            Assert.GreaterOrEqual(lotCell, 0);
            sim.State.eco.vine.Add(lotCell);
            sim.Step();
            CollectionAssert.DoesNotContain(sim.State.eco.vine, lotCell);
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
