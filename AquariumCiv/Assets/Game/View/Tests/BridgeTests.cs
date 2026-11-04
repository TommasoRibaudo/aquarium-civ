using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>#274: a bridge where a path crosses a canal, none where it doesn't; people on it walk on the deck (EditMode).</summary>
    public class BridgeTests
    {
        static SimCore Grow(int seed, float years)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            SimLayoutSource.Skip(sim, years, true);
            return sim;
        }

        /// <summary>Runs of canal cells that a path crosses, counted from the sim's map alone (8-connected).</summary>
        static int Crossings(SiteMap m)
        {
            bool Cross(int c) => m.Canal(c) && m.lot[c] == 0 && (m.Laid(c) || m.tier[c] >= PathNet.PathTier);
            var seen = new HashSet<int>(); int runs = 0;
            for (int c0 = 0; c0 < SiteMap.N; c0++)
            {
                if (seen.Contains(c0) || !Cross(c0)) continue;
                runs++; var st = new Stack<int>(); st.Push(c0); seen.Add(c0);
                while (st.Count > 0)
                {
                    int c = st.Pop(); int i = SiteMap.I(c), j = SiteMap.J(c);
                    for (int di = -1; di <= 1; di++)
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int ni = i + di, nj = j + dj;
                            if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                            int nc = SiteMap.Index(ni, nj);
                            if (Cross(nc) && seen.Add(nc)) st.Push(nc);
                        }
                }
            }
            return runs;
        }

        [TestCase(11, 60f)]
        [TestCase(20261029, 200f)]
        public void ABridgeStandsWhereAPathCrossesACanal_AndNowhereElse(int seed, float years)
        {
            var sim = Grow(seed, years);
            var before = new BoxLayout(); SimLayout.Fill(before, sim);
            int baseline = Crossings(sim.Settlement.Map);
            Assert.AreEqual(baseline, before.bridges.Count, "bridges = crossings before the dig");

            int cell = sim.Settlement.DebugCanalAcrossPath();
            Assert.GreaterOrEqual(cell, 0, "a path to dig across");
            var after = new BoxLayout(); SimLayout.Fill(after, sim);
            int crossings = Crossings(sim.Settlement.Map);
            Assert.Greater(crossings, 0);
            Assert.AreEqual(crossings, after.bridges.Count, "one bridge per crossing");
            var hit = after.bridges.OrderBy(b => (b.centre - new Vector2(SiteMap.CXc(cell), SiteMap.CZc(cell))).sqrMagnitude).First();
            Assert.Less((hit.centre - new Vector2(SiteMap.CXc(cell), SiteMap.CZc(cell))).magnitude, 0.45f, "the new bridge is at the dug crossing");
            Assert.AreEqual(sim.State.era == Era.Town, hit.Stone, "stone from the town, planks before");
            // a bridge's centre lies on a canal cell: none floats over dry ground
            foreach (var b in after.bridges)
            {
                int c = SiteMap.CellAt(b.centre.x, b.centre.y);
                bool nearCanal = false;
                for (int di = -2; di <= 2 && !nearCanal; di++) for (int dj = -2; dj <= 2; dj++)
                {
                    int ni = SiteMap.I(c) + di, nj = SiteMap.J(c) + dj;
                    if (ni >= 0 && nj >= 0 && ni < SiteMap.NX && nj < SiteMap.NZ && sim.Settlement.Map.Canal(SiteMap.Index(ni, nj))) { nearCanal = true; break; }
                }
                Assert.IsTrue(nearCanal, "a bridge only over a canal");
            }
        }

        [Test]
        public void NoCanal_NoBridge()
        {
            var sim = Grow(5, 12f);
            var L = new BoxLayout(); SimLayout.Fill(L, sim);
            Assert.AreEqual(Crossings(sim.Settlement.Map), L.bridges.Count);
        }

        [TestCase(Era.Village)]
        [TestCase(Era.Town)]
        public void ThePersonOnTheSpanStandsOnTheDeck(Era era)
        {
            var spec = TerrainSpec.Preset(0);
            float Ground(float x, float z) => Sim.Settle.Terrain.Height(spec, x, z);
            var b = new BridgeData { centre = new Vector2(0.4f, 0.3f), dir = new Vector2(1, 1).normalized, era = era };
            var mb = MeshBuilder.Rent(1);
            Bridges.Build(mb, b, Ground);
            Assert.Greater(mb.VertexCount, 40, "the bridge has a mesh");
            for (float t = -b.length / 2f; t <= b.length / 2f; t += 0.03f)
            {
                var p = b.centre + b.dir * t;
                Assert.IsTrue(Bridges.Deck(b, Ground, p, out float y), "on the span");
                Assert.GreaterOrEqual(y, Ground(p.x, p.y) + Bridges.Lift - 0.005f, "the deck is above the land (they walk on it)");
            }
            Assert.IsFalse(Bridges.Deck(b, Ground, b.centre + b.dir * (b.length / 2f + 0.1f), out _), "off the end: the land");
            Assert.IsFalse(Bridges.Deck(b, Ground, b.centre + new Vector2(-b.dir.y, b.dir.x) * (b.width / 2f + 0.1f), out _), "beside it: the land");
        }
    }
}
