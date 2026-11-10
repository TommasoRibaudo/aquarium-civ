using System;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#539: the settlers' camp keeps clear of where the eco pond WILL be (its full basin plus a margin), a shore hut stays near
    /// the water on ground that stays dry, a camp the water would reach moves back to dry ground, and a woods camp isn't by the lake.</summary>
    public class ApartPondTests
    {
        static SimCore World(int seed)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static float Nearest(bool[] mask, float x, float z)
        {
            float best = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++) if (mask[c]) best = Math.Min(best, Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z));
            return best;
        }

        [Test]
        public void ASiteChosenWithASmallPondIsOutsideTheFullBasinPlusMargin()
        {
            int seen = 0;
            foreach (int seed in new[] { 7, 42, 4242, 5 })
            {
                var sim = World(seed);
                if (sim.Settlement?.Map == null) continue;
                var bare = PondShore.FutureWater(sim.EcoGround, 0f);
                foreach (var kind in new[] { ApartKind.WoodsCamp, ApartKind.ShoreHut })
                {
                    var site = sim.ApartSpotPreview(kind);
                    var drawn = sim.ApartCampSpot(site.x, site.z, kind, out _);
                    Assert.IsFalse(PondShore.InFutureWater(sim, drawn.x, drawn.z, 0.05f), $"{kind} seed {seed}: dry for good at ({drawn.x:0.00},{drawn.z:0.00})");
                    float d = Nearest(bare, drawn.x, drawn.z);
                    if (kind == ApartKind.WoodsCamp) Assert.GreaterOrEqual(d, ApartSpots.Footprint + 0.05f, $"seed {seed}: the woods camp is well clear of the basin ({d:0.00})");
                    else Assert.LessOrEqual(d, SimCore.ShoreHutReach + 0.5f, $"seed {seed}: the shore hut is still near the water ({d:0.00})");
                }
                seen++;
            }
            Assert.Greater(seen, 0, "a living-land box with a site map");
        }

        [Test]
        public void ACampWhoseSiteIsInTheFutureWaterMovesToDryGround()
        {
            var sim = World(7);
            Assume.That(sim.Settlement?.Map, Is.Not.Null);
            var bare = PondShore.FutureWater(sim.EcoGround, 0f);
            int wet = Enumerable.Range(0, EcoLand.N).First(c => bare[c] && SiteMap.I(c) > 4 && SiteMap.J(c) > 4 && SiteMap.I(c) < SiteMap.NX - 4 && SiteMap.J(c) < SiteMap.NZ - 4);
            float x = SiteMap.CXc(wet), z = SiteMap.CZc(wet);
            foreach (var kind in new[] { ApartKind.WoodsCamp, ApartKind.ShoreHut })
            {
                var at = sim.ApartCampSpot(x, z, kind, out bool moved);
                Assert.IsTrue(moved, kind + ": an old save's camp on the basin moves");
                Assert.IsFalse(PondShore.InFutureWater(sim, at.x, at.z, 0f), kind + ": to ground the pond never covers");
            }
        }

        [Test]
        public void AWoodsCampKeepsAwayFromTheMainCampsGround()
        {
            foreach (int seed in new[] { 7, 42, 5 })
            {
                var sim = World(seed);
                var map = sim.Settlement?.Map;
                if (map == null) continue;
                var site = sim.ApartSpotPreview(ApartKind.WoodsCamp);
                float fromGreen = Terrain.Dist(site.x, site.z, map.greenX, map.greenZ);
                Assert.GreaterOrEqual(fromGreen, 1.0f, $"seed {seed}: apart from the green ({fromGreen:0.00})");
                var bare = PondShore.FutureWater(sim.EcoGround, 0f);
                Assert.GreaterOrEqual(Nearest(bare, site.x, site.z), 0.5f, $"seed {seed}: not on the lake shore");
            }
        }
    }
}
