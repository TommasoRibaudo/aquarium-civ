using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#495: the pond's shore follows the drawn (eco) water as it grows, on dry ground facing it; no fishing without open water;
    /// and the pond's full future extent for #539.</summary>
    public class PondShoreTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>The main basin's lowest cells first, as the table fills them.</summary>
        static int[] BasinByHeight(SimCore sim) => sim.EcoGround.main.cells.OrderBy(c => sim.EcoGround.height[c]).ThenBy(c => c).ToArray();

        static void Fill(SimCore sim, int[] order, int n)
        {
            for (int c = 0; c < EcoLand.N; c++) if (sim.State.eco.cover[c] == (byte)EcoCover.Water) sim.State.eco.cover[c] = (byte)EcoCover.Bare;
            for (int i = 0; i < n && i < order.Length; i++) sim.State.eco.cover[order[i]] = (byte)EcoCover.Water;
        }

        static float NearestWater(SimCore sim, float x, float z)
        {
            float best = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
                if (sim.EcoCoverAt(c) == EcoCover.Water) best = System.Math.Min(best, Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z));
            return best;
        }

        [Test]
        public void NoShoreWithoutOpenWater()
        {
            var sim = World();
            Fill(sim, BasinByHeight(sim), 0);
            Assert.IsFalse(PondShore.NearestShore(sim, 0f, 0f, 0, out _, out _, out _, out _));
        }

        [Test]
        public void TheShoreFollowsTheGrowingPond_DryGround_FacingTheWater()
        {
            var sim = World();
            var order = BasinByHeight(sim);
            Assume.That(order.Length, Is.GreaterThan(30));
            foreach (int n in new[] { 1, 3, 8, 15, 30, order.Length / 2, order.Length })
            {
                Fill(sim, order, n);
                Assert.IsTrue(PondShore.NearestShore(sim, sim.EcoGround.campX, sim.EcoGround.campZ, 0, out float x, out float z, out float fx, out float fz), "n=" + n);
                Assert.IsFalse(PondShore.IsWet(sim, x, z), $"n={n}: dry ground at ({x:0.00},{z:0.00})");
                Assert.IsTrue(PondShore.IsWet(sim, fx, fz), $"n={n}: facing water");
                Assert.LessOrEqual(NearestWater(sim, x, z), 0.3f, $"n={n}: at the water's edge");
                for (int slot = 1; slot <= 3; slot++)
                {
                    Assert.IsTrue(PondShore.NearestShore(sim, sim.EcoGround.campX, sim.EcoGround.campZ, slot, out x, out z, out _, out _));
                    Assert.IsFalse(PondShore.IsWet(sim, x, z), $"n={n} slot {slot}: dry");
                    Assert.LessOrEqual(NearestWater(sim, x, z), 0.3f, $"n={n} slot {slot}: at the edge");
                }
            }
        }

        [Test]
        public void ThePondPlaceMovesWithTheWater()
        {
            var sim = World();
            var order = BasinByHeight(sim);
            var pos = new SettlePlacePositions(sim);
            Fill(sim, order, 4);
            Assert.IsTrue(pos.TryGetPosition(Place.Land(PlaceKind.Pond), out float x1, out float z1));
            Assert.IsFalse(PondShore.IsWet(sim, x1, z1));
            Assert.LessOrEqual(NearestWater(sim, x1, z1), 0.3f);
            Fill(sim, order, order.Length);
            Assert.IsTrue(pos.TryGetPosition(Place.Land(PlaceKind.Pond), out float x2, out float z2), "asked again, not cached");
            Assert.IsFalse(PondShore.IsWet(sim, x2, z2));
            Assert.LessOrEqual(NearestWater(sim, x2, z2), 0.3f);
        }

        [Test]
        public void NoFishingWithoutOpenWater_EvenWithFishPlaced()
        {
            var sim = World();
            Fill(sim, BasinByHeight(sim), 0);
            var j = sim.State.eco.jars.First(x => x.species == nameof(LifeSpecies.Fish));
            j.state = (int)JarState.Placed; j.placings = 1; j.placedMonth = sim.State.month;
            Assert.IsTrue(sim.FishingOpen);
            Assert.IsFalse(sim.FishingWater, "no water");
            Fill(sim, BasinByHeight(sim), 5);
            Assert.IsTrue(sim.FishingWater);
        }

        [Test]
        public void FutureWater_IsTheWholeBasinBelowTheFloodLine_PlusTheMargin()
        {
            var sim = World();
            var land = sim.EcoGround;
            var bare = PondShore.FutureWater(land, 0f);
            var wide = PondShore.FutureWater(land, 0.3f);
            int nb = bare.Count(b => b), nw = wide.Count(b => b);
            Assert.Greater(nb, 0);
            Assert.Greater(nw, nb, "the margin widens it");
            for (int c = 0; c < EcoLand.N; c++) if (bare[c]) Assert.IsTrue(wide[c]);
            // every cell the pond ever covers is inside
            var order = BasinByHeight(sim);
            Fill(sim, order, order.Length);
            for (int c = 0; c < EcoLand.N; c++)
                if (land.InBasin(c) && land.height[c] < land.main.spill - 0.005f) Assert.IsTrue(bare[c], "cell " + c);
            // far from the pond: clear
            int far = Enumerable.Range(0, EcoLand.N).OrderByDescending(c => NearestWaterToCell(land, bare, c)).First();
            Assert.IsFalse(wide[far]);
            Assert.IsFalse(PondShore.InFutureWater(sim, SiteMap.CXc(far), SiteMap.CZc(far), 0.3f));
            int mid = Enumerable.Range(0, EcoLand.N).First(c => bare[c]);
            Assert.IsTrue(PondShore.InFutureWater(sim, SiteMap.CXc(mid), SiteMap.CZc(mid), 0f));
        }

        static float NearestWaterToCell(EcoLand land, bool[] mask, int c)
        {
            float best = float.MaxValue;
            for (int o = 0; o < EcoLand.N; o++) if (mask[o]) best = System.Math.Min(best, Terrain.Dist(SiteMap.CXc(o), SiteMap.CZc(o), SiteMap.CXc(c), SiteMap.CZc(c)));
            return best;
        }
    }
}
