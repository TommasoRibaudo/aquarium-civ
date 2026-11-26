using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#762: no field (or any standing lot) covers a drying rack. A grown village keeps its racks clear of its lots, and a lot laid over
    /// a standing rack makes the rack the one that moves (at night, as for a path, #700).</summary>
    public class RackLotTests
    {
        static readonly (int seed, int preset)[] Runs = { (7, 0), (11, 1), (1, 2), (3, 0) };

        static SimCore Box(int seed, int preset)
        {
            var cfg = new SimConfig { terrainPreset = preset, settle = true, buildingSlots = 20 };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static void Month(SimCore sim)
        {
            sim.Step();
            sim.State.simHours = sim.State.month * sim.Config.StepHours;
            if (sim.State.eraReady) sim.AdvanceEra();
        }

        /// <summary>The first standing lot that covers any sampled point of either rack's footprint, or null.</summary>
        static Lot Covering(SimCore sim, out string rack)
        {
            sim.Settlement.Fire(out float fx, out float fz, out _);
            float hide = RackSites.Hide(sim, fx, fz), fish = RackSites.Fish(sim, fx, fz);
            var w = sim.Settlement;
            foreach (var l in w.Lots)
            {
                if (l.retired || l.open || !RackSites.IsField(l.kind)) continue;
                if (Hits(l, fx, fz, hide, LifePlanner.HideFrameDistance, CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f)) { rack = "hide"; return l; }
                if (Hits(l, fx, fz, fish, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength)) { rack = "fish"; return l; }
            }
            rack = null; return null;
        }

        /// <summary>Does the lot's polygon (its oriented rect) contain a point of the rack's footprint (sampled every 0.02)?</summary>
        static bool Hits(Lot l, float fx, float fz, float angle, float dist, float halfDepth, float halfLength)
        {
            float ux = (float)Math.Cos(angle), uz = (float)Math.Sin(angle);
            float cx = fx + ux * dist, cz = fz + uz * dist;
            float r = l.yaw * (float)(Math.PI / 180.0), cs = (float)Math.Cos(r), sn = (float)Math.Sin(r);
            for (float a = -halfDepth; a <= halfDepth + 1e-4f; a += 0.02f)
                for (float b = -halfLength; b <= halfLength + 1e-4f; b += 0.02f)
                {
                    float x = cx + ux * a - uz * b, z = cz + uz * a + ux * b;
                    float dx = x - l.x, dz = z - l.z;
                    float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs; // as SiteMap.RectCells
                    if (Math.Abs(lx) <= l.w / 2f && Math.Abs(lz) <= l.d / 2f) return true;
                }
            return false;
        }

        [Test]
        public void AGrownVillage_HasNoLotOverItsRacks()
        {
            foreach (var (seed, preset) in Runs)
            {
                var sim = Box(seed, preset);
                int fields = 0;
                while (sim.State.month < 60 * 12L)
                {
                    // the view asks where the racks stand every frame; at night a covered rack is taken down
                    RackSites.Observe(sim, new ViewTime(1, 23f));
                    sim.Settlement.Fire(out float fx0, out float fz0, out _);
                    RackSites.Fish(sim, fx0, fz0);
                    Month(sim);
                    if (sim.State.month % 12 != 0) continue;
                    var l = Covering(sim, out var rack);
                    Assert.IsNull(l, $"seed {seed}, year {sim.State.month / 12}: a {l?.kind} lot covers the {rack} rack, lot at {l?.x},{l?.z} {l?.w}x{l?.d}, fire {sim.Settlement.GreenX},{sim.Settlement.GreenZ}, era {sim.State.era}, eco {sim.EcologyOn}");
                }
                foreach (var l in sim.Settlement.Lots) if (!l.retired && l.kind == LotKind.Farm) fields++;
                Assert.GreaterOrEqual(fields, 1, $"seed {seed}: the village has a field to test against");
            }
        }

        [Test]
        public void AFieldLaidOverAStandingRack_MakesTheRackMove_AtNight()
        {
            var sim = Box(7, 0);
            while (sim.State.month < 12L) Month(sim);
            sim.Settlement.Fire(out float fx, out float fz, out _);
            RackSites.Observe(sim, new ViewTime(1, 23f));
            float hide0 = RackSites.Hide(sim, fx, fz);
            float ux = (float)Math.Cos(hide0), uz = (float)Math.Sin(hide0);
            float cx = fx + ux * LifePlanner.HideFrameDistance, cz = fz + uz * LifePlanner.HideFrameDistance;

            // a field right over the rack (lot cells claimed as the placer does)
            var w = sim.Settlement; var map = w.Map;
            var field = new Lot { id = 9000, kind = LotKind.Farm, x = cx, z = cz, yaw = hide0 * 57.29578f + 180f, w = 0.5f, d = 0.5f };
            var cells = new List<int>();
            SiteMap.RectCells(field.x, field.z, field.yaw, field.w, field.d, 0f, cells);
            foreach (int c in cells) map.lot[c] = field.id;
            w.Lots.Add(field);
            Assert.IsTrue(Hits(field, fx, fz, hide0, LifePlanner.HideFrameDistance, CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f), "the field covers the rack before it moves");

            // at night a rack moves whatever hangs on it
            RackSites.Observe(sim, new ViewTime(1, 23f));
            float hide1 = RackSites.Hide(sim, fx, fz);
            Assert.Greater(Math.Abs(hide0 - hide1), 1e-4f, "the rack moved off the field");
            Assert.IsFalse(Hits(field, fx, fz, hide1, LifePlanner.HideFrameDistance, CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f), "and now stands clear of it");
            float fish1 = RackSites.Fish(sim, fx, fz);
            Assert.IsFalse(Hits(field, fx, fz, fish1, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength), "the fish rack too");
        }
    }
}
