using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#699: the deer walk round the camp's fire and its tents, and keep a shy distance from the camp.</summary>
    public class DeerCampTests
    {
        static (SimCore sim, DeerHerd.Field f, float fx, float fz) Herd()
        {
            var sim = PlacedLifeTests.Camp(7, 30, pond: false);
            sim.DebugPlaceNearCamp(LifeSpecies.Deer);
            sim.DebugDeerToCap();
            var f = new DeerHerd.Field(); sim.EcoDeerRefresh(f); f.Sync(sim.EcoDeerShown());
            var g = sim.EcoGround;
            return (sim, f, g.campX, g.campZ);
        }

        /// <summary>Walks like the view: toward the goal at 0.12 u/s in 1/30 s steps, each step through Steer.</summary>
        static float WalkTo(DeerHerd.Field f, ref float x, ref float z, float gx, float gz, int salt, float fx, float fz, out float closest, int maxSteps = 3000)
            => WalkTo(f, ref x, ref z, gx, gz, salt, fx, fz, out closest, out _, maxSteps);

        static float WalkTo(DeerHerd.Field f, ref float x, ref float z, float gx, float gz, int salt, float fx, float fz, out float closest, out int entered, int maxSteps)
        {
            closest = float.MaxValue; entered = 0;
            for (int i = 0; i < maxSteps; i++)
            {
                float dx = gx - x, dz = gz - z, d = MathF.Sqrt(dx * dx + dz * dz);
                if (d < 0.02f) break;
                float st = Math.Min(d, 0.12f / 30f);
                float nx = x + dx / d * st, nz = z + dz / d * st;
                f.Steer(x, z, ref nx, ref nz, salt);
                bool was = f.KeptOff(x, z);
                x = nx; z = nz;
                if (!was && f.KeptOff(x, z)) entered++;
                closest = Math.Min(closest, Terrain.Dist(x, z, fx, fz));
            }
            return Terrain.Dist(x, z, gx, gz);
        }

        [Test]
        public void Deer_OnAPathThroughTheFire_GoRoundIt()
        {
            var (sim, f, fx, fz) = Herd();
            Assert.Greater(f.CampRadius, 0.8f, "the camp is known to the herd");
            int n = 0;
            for (int salt = 0; salt < 6; salt++)
                foreach (var (ox, oz) in new[] { (-1.3f, 0f), (1.3f, 0f), (0f, 1.3f), (0f, -1.3f), (-1.1f, 0.05f), (0.9f, -0.9f) })
                {
                    float x = fx + ox, z = fz + oz, gx = fx - ox, gz = fz - oz;
                    if (f.KeptOff(x, z) && Terrain.Dist(x, z, fx, fz) < f.CampRadius) continue;
                    WalkTo(f, ref x, ref z, gx, gz, salt, fx, fz, out float closest);
                    Assert.GreaterOrEqual(closest, f.CampRadius - 0.01f, $"salt {salt} from ({ox},{oz}) walked over the fire (closest {closest})");
                    if (Terrain.Dist(x, z, gx, gz) >= 0.1f) TestContext.WriteLine($"end ({x - fx},{z - fz}) goal ({gx - fx},{gz - fz}) goalKept {f.KeptOff(gx, gz)} camp ({fx},{fz})");
                    Assert.Less(Terrain.Dist(x, z, gx, gz), 0.1f, $"salt {salt} from ({ox},{oz}) got round to the far side");
                    n++;
                }
            Assert.Greater(n, 20, "paths walked");
        }

        [Test]
        public void Deer_WalkRoundTheTents_AndOtherLots()
        {
            var (sim, f, fx, fz) = Herd();
            var blocked = sim.EcoRabbitBlocked();
            Assert.IsNotNull(blocked, "the camp is settled");
            // a blocked lot cell (a tent or such) outside the fire's ring, with free cells either side of it along x
            int found = 0;
            for (int c = 0; c < EcoLand.N && found < 5; c++)
            {
                int i = SiteMap.I(c), j = SiteMap.J(c);
                if (i < 3 || i > SiteMap.NX - 4 || !blocked[c]) continue;
                if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), fx, fz) < f.CampRadius + 0.4f) continue;
                int l = SiteMap.Index(i - 2, j), r = SiteMap.Index(i + 2, j);
                if (f.KeptOff(SiteMap.CXc(l), SiteMap.CZc(l)) || f.KeptOff(SiteMap.CXc(r), SiteMap.CZc(r))) continue;
                float x = SiteMap.CXc(l), z = SiteMap.CZc(l);
                WalkTo(f, ref x, ref z, SiteMap.CXc(r), SiteMap.CZc(r), 0, fx, fz, out _, out int entered, 300);
                Assert.AreEqual(0, entered, $"cell {c}: a step went into the lot");
                // never stepped into the cell: check the final point is not kept off, and the walk moved at most along the edge
                Assert.IsFalse(f.KeptOff(x, z), $"cell {c}: the deer ended inside a lot");
                found++;
            }
            Assert.Greater(found, 0, "found a lot cell to test against");
        }

        [Test]
        public void Deer_KeepAShyDistanceFromTheCamp()
        {
            var (sim, f, fx, fz) = Herd();
            // every place the herd grazes is outside the shy ring
            foreach (int c in f.Range)
                Assert.GreaterOrEqual(Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), fx, fz), SimCore.DeerCampMargin - 1e-4f, "a range cell inside the ring");
            // and following the day's pose through Steer for 20 minutes, none comes nearer than the ring
            float nearest = float.MaxValue;
            foreach (int id in f.Alive)
            {
                var p0 = f.Sample(id, 0, 12f); float x = p0.x, z = p0.z;
                for (double t = 0; t < 1200; t += 1.0 / 30)
                {
                    var p = f.Sample(id, t, 12f);
                    float dx = p.x - x, dz = p.z - z, d = MathF.Sqrt(dx * dx + dz * dz);
                    if (d > 0.012f)
                    {
                        float st = Math.Min(d, 0.12f / 30f), nx = x + dx / d * st, nz = z + dz / d * st;
                        f.Steer(x, z, ref nx, ref nz, id); x = nx; z = nz;
                    }
                    nearest = Math.Min(nearest, Terrain.Dist(x, z, fx, fz));
                }
            }
            TestContext.WriteLine($"nearest any deer came to the fire in 20 min: {nearest:F3} u (ring {f.CampRadius})");
            Assert.GreaterOrEqual(nearest, f.CampRadius - 0.01f);
            Assert.Greater(nearest, SimCore.RabbitFireMargin, "shyer than the rabbits");
        }
    }
}
