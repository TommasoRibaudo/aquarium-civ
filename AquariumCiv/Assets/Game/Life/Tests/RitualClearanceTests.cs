using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#275: the ritual's props (the carried Keeper Doll and its post) keep clear of the buildings' footprints, so
    /// the doll never clips a roof or a stall. Seeded Town boxes, the doll given every few months so it sits at different
    /// shrines / greens.</summary>
    public class RitualClearanceTests
    {
        static SimCore TownBox(int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < Era.Town; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            Assert.AreEqual(Era.Town, sim.State.era, $"seed {seed} reaches Town");
            return sim;
        }

        [Test]
        public void TheDollsPost_KeepsClearOfEveryBuilding([Values(1, 5, 7, 11, 23, 1234)] int seed)
        {
            var sim = TownBox(seed);
            var pos = new SettlePlacePositions(sim);
            var doll = LifePlanner.RelicPlace(RelicId.KeeperDoll);
            int checkedPosts = 0;
            var bad = new List<string>();
            for (int round = 0; round < 6; round++)
            {
                for (int m = 0; m < 18; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
                if (sim.FindRelic(RelicId.KeeperDoll) == null || !sim.FindRelic(RelicId.KeeperDoll).Found) sim.DebugGiveRelic(RelicId.KeeperDoll);
                Assert.IsTrue(pos.TryGetPosition(doll, out float x, out float z), "the doll has a post");
                checkedPosts++;
                float gap = RitualClearance.NearestGap(sim.Settlement, x, z);
                if (gap < RitualClearance.Margin) bad.Add($"round {round} post ({x:F2},{z:F2}) gap {gap:F2}");
            }
            Assert.GreaterOrEqual(checkedPosts, 6);
            Assert.IsEmpty(bad, $"seed {seed}: posts too close to a building (< {RitualClearance.Margin}): " + string.Join("; ", bad));
        }
    }
}
