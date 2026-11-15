// #724 landscaping options lab: how many EXTRA tree seedlings the Keeper could place (option B2) change the village year,
// measured on the real sim with the #569 pacing lab (Active Keeper, seeds 7/21/42). The extra seedlings are put into the ground
// once, the month the Keeper's seed is planted, on open ground round the Keeper's tree: the most a free "place a seedling" tool
// could do on day one. They are normal wild seedlings from then on (same growth, same mist). Explicit: run by name.
//   dotnet test Tools/SimTests --filter LandscapeOptionsLab
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
    public class LandscapeOptionsLab
    {
        static int PlaceExtra(SimCore sim, int count)
        {
            var e = sim.State.eco; var land = sim.EcoGround;
            float tx = SiteMap.CXc(e.treeCell), tz = SiteMap.CZc(e.treeCell);
            var cells = new List<int>();
            for (int c = 0; c < SiteMap.N; c++)
            {
                var cv = (EcoCover)e.cover[c];
                if (c == e.treeCell || land.InBasin(c) || land.yard[c]) continue;
                if (cv != EcoCover.Bare && cv != EcoCover.Grass && cv != EcoCover.Tall) continue;
                if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), land.campX, land.campZ) < 0.9f) continue; // the camp's margin
                cells.Add(c);
            }
            cells.Sort((a, b) => Terrain.Dist(SiteMap.CXc(a), SiteMap.CZc(a), tx, tz).CompareTo(Terrain.Dist(SiteMap.CXc(b), SiteMap.CZc(b), tx, tz)));
            // spread them: skip a cell next to one already chosen
            var chosen = new List<int>();
            foreach (var c in cells)
            {
                if (chosen.Count >= count) break;
                bool near = false;
                foreach (var o in chosen) if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(o), SiteMap.CZc(o)) < 0.2f) { near = true; break; }
                if (near) continue;
                chosen.Add(c); e.cover[c] = (byte)EcoCover.Seedling;
            }
            return chosen.Count;
        }

        [Test, Explicit]
        public void ExtraSeedlings_VillageYear()
        {
            foreach (int extra in new[] { 0, 5, 10, 20 })
            {
                var rows = new List<string>(); var vill = new List<float>(); var tr8 = new List<float>();
                foreach (int seed in PacingLab.Seeds)
                {
                    bool done = false; int placed = 0;
                    var r = PacingLab.Run(Habit.Active, seed, null, 40, false, sim =>
                    {
                        if (done || sim.State.eco.treeCell < 0) return;
                        done = true; placed = PlaceExtra(sim, extra);
                    });
                    vill.Add(r.village); tr8.Add(r.trees8);
                    rows.Add($"seed {seed,2}: placed {placed,2}  trees8 {PacingResult.F(r.trees8)}  pond20 {PacingResult.F(r.pond20)} camp {PacingResult.F(r.camp)}  village {PacingResult.F(r.village)}  trees at end {r.trees}");
                }
                TestContext.Out.WriteLine($"EXTRA {extra,2}: village {string.Join(" ", vill.Select(PacingResult.F))} | trees8 {string.Join(" ", tr8.Select(PacingResult.F))}");
                foreach (var l in rows) TestContext.Out.WriteLine("   " + l);
            }
        }
    }
}
