// #791 barn lab: how often a village has no barn. Every family yard with a home gets one outbuilding (Settlement.Outbuildings), and
// SimLayout draws it as a barn (Pieces.Barn variant 0) when Settlement.IsBarn: Terrain.Hash3(seed, lotId, 61) < 0.6 or the box's first
// outbuilding (2026-11-26: every village has a barn; before that rule 10 of 20 had none), else a shed with a woodpile (variant 1).
// The village's barn uses (BarnChores, HayJump: CampLife.BarnOf) take only variant 0. Measurement only (Explicit): dotnet test --filter BarnLab.
using System;
using System.Linq;
using System.Text;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    public class BarnLab
    {
        static bool IsBarn(SimCore sim, Lot l) => sim.Settlement.IsBarn(l); // SimLayout.Fill, LotKind.Outbuilding

        static void Year(SimCore sim)
        {
            for (int m = 0; m < 12; m++)
            {
                sim.Step();
                sim.State.simHours = sim.State.month * sim.Config.StepHours;
                if (sim.State.eraReady) sim.AdvanceEra();
            }
        }

        static string Count(SimCore sim, out int outbuildings, out int barns)
        {
            var obs = sim.Settlement.Lots.Where(l => l.kind == LotKind.Outbuilding && !l.retired).ToList();
            outbuildings = obs.Count; barns = obs.Count(l => IsBarn(sim, l));
            return $"era {sim.State.era} year {sim.State.month / 12} homes {sim.Settlement.Lots.Count(l => (l.kind == LotKind.Hut || l.kind == LotKind.Cottage) && !l.retired)} outbuildings {outbuildings} barns {barns}";
        }

        [Test, Explicit("measurement")]
        public void HowOftenAVillageHasNoBarn()
        {
            var sb = new StringBuilder();
            int stagedNone = 0, naturalNone = 0, naturalVillages = 0, n = 0;
            for (int seed = 4230; seed < 4250; seed++, n++)
            {
                // staged the way CampCheats.StageVillage does it: the era forced, then 15 fed years
                var cfg = new SimConfig { settle = true };
                var a = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                a.ForceNextEra();
                for (int i = 0; i < 15; i++)
                {
                    a.State.population = Math.Max(a.State.population, 26);
                    a.State.wood = 2000f; a.State.food = Math.Max(a.State.food, 200f);
                    Year(a);
                }
                string sa = Count(a, out _, out int ba);
                if (ba == 0) stagedNone++;
                // grown on its own: until the village era, then ten more years (or 150 years at most)
                var b = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                int y = 0;
                while (b.State.era < Era.Village && y < 140) { Year(b); y++; }
                bool village = b.State.era >= Era.Village;
                if (village) for (int i = 0; i < 10; i++) Year(b);
                string sb2 = Count(b, out _, out int bb);
                if (village) { naturalVillages++; if (bb == 0) naturalNone++; }
                sb.AppendLine($"seed {seed}: staged {sa} | natural {(village ? sb2 : "no village in 140 years")}");
            }
            sb.AppendLine($"no barn: staged {stagedNone}/{n}, natural {naturalNone}/{naturalVillages} villages");
            TestContext.Out.WriteLine(sb.ToString());
            Console.WriteLine(sb.ToString());
        }
    }
}
