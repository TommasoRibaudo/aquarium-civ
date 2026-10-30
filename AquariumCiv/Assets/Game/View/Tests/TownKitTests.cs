using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The town-era kit (#34): every town lot kind has a town piece within the vertex budget, the town's own
    /// looks differ from the village's, the square gets stalls, and paths can be cobbled (#90's hook).</summary>
    public class TownKitTests
    {
        static MeshBuilder Draw(PieceKind k, Era e, int seed = 3, int variant = 0, float w = 0.5f, float d = 0.45f)
        {
            var mb = new MeshBuilder();
            Pieces.Draw(mb, new PieceData { kind = k, era = e, w = w, d = d, seed = seed, variant = variant }, q => q);
            return mb;
        }

        [Test]
        public void EveryLotKind_HasATownPiece_WithinTheVertexBudget()
        {
            foreach (LotKind lk in System.Enum.GetValues(typeof(LotKind)))
            {
                if (lk == LotKind.Farm || lk == LotKind.Camp) continue; // a field; the camp's hearth is retired by the town
                var spec = Encyclopedia.Of(lk);
                var pk = SimLayout.PieceFor(lk);
                for (int seed = 1; seed < 40; seed += 7)
                {
                    var mb = Draw(pk, Era.Town, seed, 0, spec.wMax, spec.dMax);
                    Assert.Greater(mb.VertexCount, 0, $"{lk} → {pk}");
                    Assert.LessOrEqual(mb.VertexCount, Pieces.TownPieceVertexBudget, $"{lk} → {pk} (seed {seed}): {mb.VertexCount} verts");
                }
            }
            foreach (PieceKind pk in System.Enum.GetValues(typeof(PieceKind)))
                Assert.LessOrEqual(Draw(pk, Era.Town).VertexCount, Pieces.TownPieceVertexBudget, pk.ToString());
        }

        [Test]
        public void TownPieces_LookDifferent_FromTheVillages()
        {
            foreach (var k in new[] { PieceKind.Windmill, PieceKind.Market, PieceKind.Well, PieceKind.Workshop, PieceKind.Guildhall, PieceKind.Bakery,
                         PieceKind.Watermill, PieceKind.Stall, PieceKind.Lumbermill })
            {
                var town = Draw(k, Era.Town); var village = Draw(k, Era.Village);
                Assert.AreNotEqual(village.VertexCount, town.VertexCount, $"{k}: the town has its own piece");
            }
            // The tower mill is the tallest thing on its lot, taller than the village's post mill.
            Assert.Greater(Draw(PieceKind.Windmill, Era.Town).verts.Max(v => v.y), Draw(PieceKind.Windmill, Era.Village).verts.Max(v => v.y));
            Assert.Greater(Draw(PieceKind.Guildhall, Era.Town).verts.Max(v => v.y), 0.9f, "the guildhall's clock tower stands over the roofs");
        }

        [Test]
        public void Workshops_ShowThreeDifferentTrades()
        {
            var counts = Enumerable.Range(1, Pieces.TradeCount).Select(v => Draw(PieceKind.Workshop, Era.Town, 3, v).VertexCount).ToArray();
            Assert.AreEqual(Pieces.TradeCount, counts.Distinct().Count(), "weaver, tanner and cooper differ");
            var seen = Enumerable.Range(0, 60).Select(s => Pieces.TradeOf(new PieceData { seed = s * 101 + 7 })).Distinct().Count();
            Assert.AreEqual(Pieces.TradeCount, seen, "seeds spread the trades");
            Assert.AreEqual(Pieces.Trade.Cooper, Pieces.TradeOf(new PieceData { seed = 5, variant = 3 }));
        }

        [Test]
        public void TownWindowsAndLamps_LightAtNight()
        {
            foreach (var k in new[] { PieceKind.Windmill, PieceKind.Market, PieceKind.Guildhall, PieceKind.Bakery, PieceKind.Workshop, PieceKind.Watermill })
                Assert.IsTrue(Draw(k, Era.Town).lamps.Any(l => l.x > 0.5f && l.x < 1.5f), $"{k} has lamp windows");
        }

        [Test]
        public void TownWithAMarket_HasStallsOnTheSquare()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(20261029, cfg));
            SimLayoutSource.Skip(sim, 200f, true);
            Assert.AreEqual(Era.Town, sim.State.era, "200 years in: a town");
            Assert.IsTrue(sim.Settlement.Has(LotKind.Market), "with a market");
            var L = new BoxLayout();
            SimLayout.Fill(L, sim);
            var green = new Vector2(sim.Settlement.GreenX, sim.Settlement.GreenZ);
            var stalls = L.pieces.Where(p => p.kind == PieceKind.Stall).ToList();
            Assert.That(stalls.Count, Is.InRange(1, SimLayout.TownStalls));
            foreach (var s in stalls)
            {
                Assert.AreEqual(Era.Town, s.era);
                Assert.Less(Vector2.Distance(s.Pos, green), SiteMap.GreenR, "on the square");
            }
            Assert.IsTrue(L.clearings.Any(c => c.kind == ClearingKind.Cobble), "the square is cobbled");

            // #90's hook: cobble every cell, and every path run comes out cobbled; cobble none, and none does.
            var all = new BoxLayout(); SimLayout.Fill(all, sim, null, c => true);
            Assert.IsTrue(all.paths.Count > 0 && all.paths.All(p => p.cobbled));
            Assert.IsFalse(L.paths.Any(p => p.cobbled));
        }

        [Test]
        public void CobbledPath_PaintsCobbles_PlainPathStaysSandy()
        {
            var go = new GameObject("TestBox");
            go.SetActive(false);
            try
            {
                var box = go.AddComponent<BoxView>();
                box.layout.Path(PathTier.Path, new Vector2(0.2f, 0.1f), new Vector2(1.0f, 0.1f));
                box.layout.CobbledPath(PathTier.Path, new Vector2(0.2f, 0.8f), new Vector2(1.0f, 0.8f));
                box.layout.Path(PathTier.Road, new Vector2(-1.6f, 0.8f), new Vector2(-0.8f, 0.8f));
                box.layout.autoNature = false;
                box.bakeAO = false;
                go.SetActive(true);
                var plain = box.GroundColor(0.6f, 0.1f, Land.G, out _);
                var cobbled = box.GroundColor(0.6f, 0.8f, Land.G, out _);
                var road = box.GroundColor(-1.2f, 0.8f, Land.G, out _);
                float Sat(Color c) => Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);
                Assert.Less(Sat(cobbled), Sat(plain), "cobbles are grey stone, the path is sandy");
                Assert.AreEqual(Sat(road), Sat(cobbled), 0.08f, "a cobbled path looks like the road");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
