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

        /// <summary>#727: the same measure at the EARNED pace (#726: a seed per era and per +5 people at the peak, at most 3 wait). The Active
        /// Keeper plants each earned seed the month it is earned, on open ground round the Keeper's tree (an upper bound: a real Keeper waits
        /// for the next visit). Reports when each seed was earned, the 10-tree gate and the village against the no-seed run.
        /// dotnet test Tools/SimTests --filter EarnedSeeds_VillageYear</summary>
        [Test, Explicit]
        public void EarnedSeeds_VillageYear()
        {
            // the third column group is a what-if: the pouch pays a seed in years 4, 8 and 12 whatever the people do (the earned pace itself
            // pays nothing before the village era in this lab: the Active Keeper's camp stays at 8 people)
            foreach (int mode in new[] { 0, 1, 2 })
            {
                bool plant = mode > 0;
                int[] gift = mode == 2 ? new[] { 4, 8, 12 } : new int[0];
                var vill = new List<float>(); var tr8 = new List<float>(); var rows = new List<string>();
                foreach (int seed in PacingLab.Seeds)
                {
                    int planted = 0, earned = 0, earnedB = 0, maxPop = 0, buildings = 0; string era = ""; var when = new List<string>(); var whenB = new List<string>();
                    var r = PacingLab.Run(Habit.Active, seed, null, 40, false, sim =>
                    {
                        var e = sim.State.eco;
                        maxPop = Math.Max(maxPop, sim.State.population); buildings = sim.State.buildings.Count; era = sim.State.era.ToString();
                        if (e.seedSteps > earned) { for (int k = earned; k < e.seedSteps; k++) when.Add((sim.State.month / 12f).ToString("0.0")); earned = e.seedSteps; }
                        if (e.bushSteps > earnedB) { for (int k = earnedB; k < e.bushSteps; k++) whenB.Add((sim.State.month / 12f).ToString("0.0")); earnedB = e.bushSteps; }
                        foreach (var gy in gift) if (sim.State.month == gy * 12) sim.DebugGiveSeeds(1);
                        if (!plant || e.treeCell < 0) return;
                        while (sim.SeedsInPouch > 0)
                        {
                            bool ok = false;
                            for (int k = 0; k < 40 && !ok; k++)
                            {
                                float a = Terrain.Hash3(seed * 17 + 5, planted, k) * MathF.PI * 2f, rr = 0.35f + 0.8f * Terrain.Hash3(seed * 17 + 6, planted, k);
                                ok = sim.PlantTree(SiteMap.CXc(e.treeCell) + MathF.Cos(a) * rr, SiteMap.CZc(e.treeCell) + MathF.Sin(a) * rr);
                            }
                            if (!ok) break;
                            planted++;
                        }
                    });
                    vill.Add(r.village); tr8.Add(r.trees8);
                    rows.Add($"seed {seed,2}: seeds earned at years [{string.Join(" ", when.Take(8))}], bushes [{string.Join(" ", whenB)}] (3 wait, none placed), planted {planted}  trees8 {PacingResult.F(r.trees8)}  village {PacingResult.F(r.village)}  era ready {PacingResult.F(r.eraReady)}  trees at end {r.trees}  [peak people {maxPop}, buildings {buildings}, era {era}, last year {r.lastYear:0.0}]");
                }
                TestContext.Out.WriteLine($"{(mode == 0 ? "NO earned seeds planted" : mode == 1 ? "EARNED seeds planted (the earned pace, round 2)" : "WHAT-IF a seed at years 4, 8, 12")}: village {string.Join(" ", vill.Select(PacingResult.F))} | trees8 {string.Join(" ", tr8.Select(PacingResult.F))}");
                foreach (var l in rows) TestContext.Out.WriteLine("   " + l);
            }
        }

        /// <summary>#726 round 2: what actually grows in the camp years, per game year: people (peak), buildings, building kinds, grown trees.
        /// dotnet test Tools/SimTests --filter CampTimeline</summary>
        [Test, Explicit]
        public void CampTimeline()
        {
            foreach (int seed in PacingLab.Seeds)
            {
                var rows = new List<string>(); int lastYear = -1, peak = 0;
                PacingLab.Run(Habit.Active, seed, null, 30, false, sim =>
                {
                    peak = Math.Max(peak, sim.State.population);
                    int y = (int)(sim.State.month / 12);
                    if (y == lastYear) return; lastYear = y;
                    if (y % 2 != 0 && y > 12) return;
                    var kinds = sim.State.buildings.Select(b => b.type).Distinct().Count();
                    rows.Add($"y{y}: peak {peak} pop {sim.State.population} bld {sim.State.buildings.Count} kinds {kinds} trees {sim.State.eco.cTrees}");
                });
                TestContext.Out.WriteLine($"seed {seed}: " + string.Join(" | ", rows));
            }
        }
    }
}
