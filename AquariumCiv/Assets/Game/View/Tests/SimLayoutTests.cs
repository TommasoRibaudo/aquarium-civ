using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.Demo;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The sim-to-view mapping (#33) and the box's chunked refresh (EditMode).</summary>
    public class SimLayoutTests
    {
        static SimCore Grow(int seed, float years)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            SimLayoutSource.Skip(sim, years, true);
            return sim;
        }

        static BoxLayout Fill(SimCore sim, System.Func<Lot, Era> eraOf = null)
        {
            var L = new BoxLayout();
            SimLayout.Fill(L, sim, eraOf);
            return L;
        }

        [Test]
        public void EveryStandingLot_HasItsPieceOrField_OnTheLot()
        {
            var sim = Grow(11, 60f);
            var L = Fill(sim);
            var st = sim.Settlement;
            var era = sim.State.era;
            Assert.AreNotEqual(Era.Camp, era, "60 years in: past the camp");
            foreach (var l in st.Standing())
            {
                if (l.kind == LotKind.Camp) continue;
                int seed = SimLayout.SeedOf(l);
                if (l.kind == LotKind.Farm)
                {
                    Assert.IsTrue(L.fields.Any(f => f.seed == seed && f.crop != CropKind.Gathering && f.fence), $"farm lot {l.id} is a fenced field in the village");
                    continue;
                }
                var p = L.pieces.FirstOrDefault(q => q.seed == seed);
                Assert.IsNotNull(p, $"lot {l.id} ({l.kind}) has a piece");
                Assert.AreEqual(l.x, p.x, 1e-5f); Assert.AreEqual(l.z, p.z, 1e-5f); Assert.AreEqual(l.yaw, p.yaw, 1e-5f);
                Assert.AreEqual(era, p.era);
            }
            Assert.IsFalse(L.pieces.Any(p => p.kind == PieceKind.Fire), "the camp's fire gave way to the green");
            Assert.IsTrue(L.clearings.Any(c => c.kind == ClearingKind.Green || (era == Era.Town && c.kind == ClearingKind.Cobble)), "a green (a square in a town with a market)");
        }

        [Test]
        public void Camp_HasTheFire_Tents_AndGatheringGrounds()
        {
            var sim = Grow(3, 4f);
            Assert.AreEqual(Era.Camp, sim.State.era);
            sim.DebugBuild(BuildingType.Farm);
            sim.DebugBuild(BuildingType.Lumbermill);
            var L = Fill(sim);
            Assert.AreEqual(1, L.pieces.Count(p => p.kind == PieceKind.Fire));
            Assert.IsTrue(L.pieces.Where(p => p.kind == PieceKind.Home).All(p => p.era == Era.Camp));
            Assert.IsTrue(L.pieces.Any(p => p.kind == PieceKind.Gathering), "a camp farm is gathering grounds");
            Assert.IsTrue(L.fields.All(f => f.crop == CropKind.Gathering && !f.fence));
            Assert.IsTrue(L.pieces.Any(p => p.kind == PieceKind.Lumbermill && p.era == Era.Camp), "the camp's mill (drawn as a woodpile)");
        }

        [Test]
        public void Trees_ComeFromTheForest_AndStayOffLotsAndPaths()
        {
            var sim = Grow(7, 40f);
            var L = Fill(sim);
            var map = sim.Settlement.Map;
            Assert.IsFalse(L.autoTrees, "the forest layer gives the trees");
            Assert.Greater(L.trees.Count, 20);
            foreach (var t in L.trees)
            {
                int c = SiteMap.CellAt(t.pos.x, t.pos.y);
                Assert.AreEqual(0, map.lot[c], $"tree at {t.pos} on a lot");
                Assert.Less(map.tier[c], PathNet.Trail, $"tree at {t.pos} on a path");
                Assert.IsFalse(map.Water(c));
            }
        }

        [Test]
        public void Paths_FollowTheCurves_ByTier_AndPeople_FollowThePopulation()
        {
            var sim = Grow(7, 110f);
            for (int i = 0; i < 4 && sim.State.era != Era.Town; i++) SimLayoutSource.Skip(sim, 10f, true);
            while (sim.State.era != Era.Town) sim.ForceNextEra();
            SimLayoutSource.Skip(sim, 3f, true); // a wear pass in the town era lays roads
            var L = Fill(sim);
            int samples = sim.Settlement.Paths().Where(c => c.Count >= 2).Sum(c => c.Count);
            Assert.Greater(L.paths.Count, 0);
            Assert.GreaterOrEqual(L.paths.Sum(p => p.points.Length), samples, "every curve sample is in a run");
            Assert.IsTrue(L.paths.Any(p => p.tier == PathTier.Road), "a town has roads");
            Assert.AreEqual(Mathf.Clamp(sim.State.population, 3, SimLayout.MaxPeople), L.villagers.Count);
            Assert.AreEqual(L.villagers.Count, L.villagers.Select(v => v.seed).Distinct().Count(), "stable, unique doll seeds");
        }

        [Test]
        public void Fill_IsDeterministic_AndTheWaveDrawsTheOldEra()
        {
            var sim = Grow(5, 60f);
            var a = Fill(sim); var b = Fill(sim);
            Assert.AreEqual(a.pieces.Count, b.pieces.Count);
            for (int i = 0; i < a.pieces.Count; i++)
            {
                Assert.AreEqual(a.pieces[i].seed, b.pieces[i].seed);
                Assert.AreEqual(a.pieces[i].x, b.pieces[i].x);
            }
            var old = Fill(sim, _ => Era.Camp);
            Assert.IsTrue(old.pieces.Any(p => p.kind == PieceKind.Fire), "before the wave reaches the green, the fire burns");
            Assert.IsTrue(old.pieces.Where(p => p.kind == PieceKind.Home).All(p => p.era == Era.Camp));
        }

        [Test]
        public void Refresh_RebuildsOnlyTheChunkThatChanged_AndKeepsThePeople()
        {
            var go = new GameObject("TestBox");
            go.SetActive(false);
            try
            {
                var box = go.AddComponent<BoxView>();
                box.logRebuilds = false;
                go.AddComponent<TemporaryDemoLayout>().FillLayout(box.layout);
                Object.DestroyImmediate(go.GetComponent<TemporaryDemoLayout>()); // keep this layout (no refill on Refresh)
                go.SetActive(true);
                var before = box.Dolls.ToArray();

                box.Refresh();
                Assert.AreEqual(0, box.lastChunks.Count, "nothing changed");

                box.layout.House(2.2f, 1.5f, 180f, 0.5f, 0.45f, Era.Village, seed: 999); // the back-right quarter
                box.Refresh();
                CollectionAssert.AreEqual(new[] { BoxView.ChunkOf(2.2f, 1.5f) }, box.lastChunks);
                Assert.AreEqual(3, BoxView.ChunkOf(2.2f, 1.5f));
                CollectionAssert.AreEqual(before, box.Dolls.ToArray(), "the same dolls, not new ones");

                box.layout.villagers[0].pos += new Vector2(0.05f, 0f);
                box.Refresh();
                Assert.AreEqual(0, box.lastChunks.Count, "people move without a chunk rebuild");
                Assert.AreSame(before[0], box.Dolls[0]);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
