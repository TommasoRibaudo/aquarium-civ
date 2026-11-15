using System;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#700: a rack (fish, hide) is never placed on a path cell, and when a path grows over a standing rack it moves off the path
    /// at a quiet moment (night, or an empty rack), not in front of the player.</summary>
    public class RackPathTests
    {
        static readonly (float x, float z)[] Fires = { (0f, 0f), (0.8f, -0.5f), (-1.1f, 0.7f), (0.3f, 1.2f) };

        /// <summary>A wedge of path, from the fire out, centred on <paramref name="centre"/> (radians, half-width <paramref name="half"/>).</summary>
        static Func<float, float, bool> Wedge(float fx, float fz, float centre, float half) => (x, z) =>
        {
            float dx = x - fx, dz = z - fz;
            if (dx * dx + dz * dz < 0.2f * 0.2f) return false;
            return CampSpots.AngleGap((float)Math.Atan2(dz, dx), centre) <= half;
        };

        [Test]
        public void Placing_NeverPicksAPathCell_ForEitherRack_AtAnyFireOrSeed()
        {
            foreach (var f in Fires)
                for (int i = 0; i < 48; i++)
                {
                    float seed = i / 48f * 6.2831853f;
                    var path = Wedge(f.x, f.z, seed, 0.5f); // the path grows right where the seed wants the rack
                    float hide = CampSpots.RackAngle(f.x, f.z, seed, LifePlanner.HideFrameDistance, path);
                    Assert.IsFalse(CampSpots.RackOnPath(f.x, f.z, hide, LifePlanner.HideFrameDistance, CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f, path), $"hide rack on the path, fire {f}, seed {seed}");
                    float fish = CampSpots.FishRackAngle(f.x, f.z, hide, path);
                    Assert.IsFalse(CampSpots.RackOnPath(f.x, f.z, fish, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength, path), $"fish rack on the path, fire {f}, seed {seed}");
                    Assert.GreaterOrEqual(CampSpots.AngleGap(fish, hide), CampSpots.RackGap - 1e-3f, "the racks keep their room");
                }
        }

        [Test]
        public void WithNoPath_TheAnglesAreTheOldOnes()
        {
            foreach (var f in Fires)
                for (int i = 0; i < 24; i++)
                {
                    float seed = i / 24f * 6.2831853f;
                    Func<float, float, bool> none = (x, z) => false;
                    Assert.AreEqual(CampSpots.RackAngle(f.x, f.z, seed, 0.5f), CampSpots.RackAngle(f.x, f.z, seed, 0.5f, none), 1e-6f);
                    float h = CampSpots.RackAngle(f.x, f.z, seed, 0.5f);
                    Assert.AreEqual(CampSpots.FishRackAngle(f.x, f.z, h), CampSpots.FishRackAngle(f.x, f.z, h, none), 1e-6f);
                }
        }

        [Test]
        public void AKeepersProp_NeverStandsOnAPath()
        {
            var fixedDiscs = new System.Collections.Generic.List<CampSpots.Disc>(); CampSpots.FireSide(0f, 0f, fixedDiscs);
            var path = Wedge(0f, 0f, 1.0f, 0.4f);
            float wx = (float)Math.Cos(1.0) * 0.8f, wz = (float)Math.Sin(1.0) * 0.8f;
            CampSpots.Spot(0f, 0f, 3.5f, LifePlanner.HideFrameDistance, wx, wz, null, out float x, out float z, 5.6f, path);
            Assert.IsFalse(path(x, z));
        }

        // ---------------------------------------------------------------- the real planner and sim
        static SimCore Camp(int seed = 5)
        {
            var cfg = new SimConfig { ecology = true, settle = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>Lays path tier over every cell the fish rack touches.</summary>
        static int PaveUnder(SimCore sim, in LifePlanner.FishRackLook look)
        {
            var map = sim.Settlement.Map; int n = 0;
            for (float a = -0.35f; a <= 0.35f; a += 0.03f)
                for (float d = -0.12f; d <= 0.12f; d += 0.03f)
                {
                    int c = SiteMap.CellAt(look.x + look.dirX * a - look.dirZ * d, look.z + look.dirZ * a + look.dirX * d);
                    if (c >= 0 && map.tier[c] < PathNet.PathTier) { map.tier[c] = PathNet.PathTier; n++; }
                }
            return n;
        }

        static bool FootprintOnPath(SimCore sim, in LifePlanner.FishRackLook look)
        {
            for (float a = -FishRackSpot.HalfLength; a <= FishRackSpot.HalfLength + 1e-4f; a += 0.04f)
                for (float d = -FishRackSpot.HalfDepth; d <= FishRackSpot.HalfDepth + 1e-4f; d += 0.035f)
                    if (RackSites.PathAt(sim, look.x + look.dirX * a - look.dirZ * d, look.z + look.dirZ * a + look.dirX * d)) return true;
            return false;
        }

        [Test]
        public void APathGrowingOverTheFishRack_MovesItOff_AtNight()
        {
            var sim = Camp();
            var planner = LifePlanner.Wired(sim);
            Assert.IsNotNull(sim.Settlement?.Map, "a settled box");
            Assert.IsTrue(planner.TryGetFishRack(new ViewTime(2, 10f), out var before));
            Assert.AreEqual(0, sim.FishOnRack);
            Assert.Greater(PaveUnder(sim, before), 0, "the path grew over the rack's cells");
            Assert.IsTrue(FootprintOnPath(sim, before), "the rack stands on the path now");
            Assert.IsTrue(planner.TryGetFishRack(new ViewTime(2, 22f), out var after));
            Assert.IsFalse(FootprintOnPath(sim, after), "the rack moved off the path");
            Assert.Greater(Math.Abs(after.x - before.x) + Math.Abs(after.z - before.z), 0.05f, "it stands somewhere else");
            Assert.AreEqual(1, RackSites.Moves(sim));
            Assert.IsTrue(planner.TryGetFishRack(new ViewTime(3, 10f), out var later));
            Assert.AreEqual(after.x, later.x, 1e-5f, "and it stays there");
            Assert.AreEqual(after.z, later.z, 1e-5f);
        }

        [Test]
        public void ARackInUse_WaitsForNight_ButAnEmptyOneMovesAtOnce()
        {
            // in use: fish hang on it by day -> it stays until night
            var sim = Camp();
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.TryGetFishRack(new ViewTime(2, 10f), out var before));
            sim.DebugFillFishBasket(3);
            Assert.AreEqual(3, sim.FishHang(3, 2));
            PaveUnder(sim, before);
            Assert.IsTrue(planner.TryGetFishRack(new ViewTime(2, 11f), out var noon));
            Assert.AreEqual(before.x, noon.x, 1e-5f, "in use by day: it does not pop");
            Assert.AreEqual(before.z, noon.z, 1e-5f);
            Assert.AreEqual(0, RackSites.Moves(sim));
            Assert.IsTrue(planner.TryGetFishRack(new ViewTime(2, 23f), out var night));
            Assert.IsFalse(FootprintOnPath(sim, night), "at night it moves");

            // empty: a quiet moment at any hour
            var sim2 = Camp(6);
            var p2 = LifePlanner.Wired(sim2);
            Assert.IsTrue(p2.TryGetFishRack(new ViewTime(2, 10f), out var b2));
            PaveUnder(sim2, b2);
            Assert.IsTrue(p2.TryGetFishRack(new ViewTime(2, 11f), out var a2));
            Assert.IsFalse(FootprintOnPath(sim2, a2), "empty by day: it moves");
        }

        [Test]
        public void APathGrowingOverTheHideRack_MovesItOff_WhenNothingHangsOnIt()
        {
            var sim = Camp();
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.HideFrame(out float x0, out float z0, out float dx0, out float dz0));
            Assert.IsTrue(planner.FirePoint(out float fx, out float fz));
            var map = sim.Settlement.Map;
            for (float a = -0.3f; a <= 0.3f; a += 0.03f)
                for (float d = -0.22f; d <= 0.22f; d += 0.03f)
                {
                    int c = SiteMap.CellAt(x0 + dx0 * a - dz0 * d, z0 + dz0 * a + dx0 * d);
                    if (c >= 0) map.tier[c] = PathNet.PathTier;
                }
            Assert.IsTrue(planner.HideFrame(out float x1, out float z1, out float dx1, out float dz1));
            float ang = (float)Math.Atan2(z1 - fz, x1 - fx);
            Assert.IsFalse(CampSpots.RackOnPath(fx, fz, ang, LifePlanner.HideFrameDistance, CampSpots.RackHalfDepth, CampSpots.RackHalfLength + 0.035f, RackSites.PathTest(sim)), "the hide rack is off the path");
            Assert.Greater(Math.Abs(x1 - x0) + Math.Abs(z1 - z0), 0.05f);
        }
    }
}
