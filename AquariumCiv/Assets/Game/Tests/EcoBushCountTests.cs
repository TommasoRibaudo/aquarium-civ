using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#385 (playtest #341, the user 2026-11-07): berry bushes grow in one or two thickets and settle at about 60 in a
    /// grown box, never a carpet; none on a lot, a path or the green; an old carpeted save thins over a few years.</summary>
    public partial class EcoTests
    {
        /// <summary>A scripted run, year by year: the bush count, the bushes on lots / paths / the green, and those outside the thickets.</summary>
        sealed class BushYears
        {
            public SimCore sim;
            public readonly List<int> count = new List<int>(), onSettlement = new List<int>(), outside = new List<int>();
        }

        static readonly Dictionary<string, BushYears> bushCache = new Dictionary<string, BushYears>();

        static BushYears PlayBushes(bool care, int years, int seed)
        {
            string key = $"{care}-{years}-{seed}";
            lock (bushCache)
            {
                if (bushCache.TryGetValue(key, out var hit)) return hit;
                var r = new BushYears { sim = EcoWorld(seed) };
                var sim = r.sim; var keeper = new Keeper(care);
                for (int s = 0; s < years * 4; s++)
                {
                    keeper.Play(sim);
                    sim.Advance(0.25);
                    if (s % 4 != 3) continue;
                    var e = sim.State.eco; var map = sim.Settlement.Map;
                    int n = 0, on = 0, outs = 0;
                    for (int c = 0; c < EcoLand.N; c++)
                    {
                        if ((EcoCover)e.cover[c] != EcoCover.Bush) continue;
                        n++;
                        if (map.lot[c] != 0 || map.tier[c] >= PathNet.Trail || map.Green(c)) on++;
                        if (sim.BushThicketZone(c) <= 0f) outs++;
                    }
                    r.count.Add(n); r.onSettlement.Add(on); r.outside.Add(outs);
                }
                return bushCache[key] = r;
            }
        }

        static readonly int[] BushSeeds = { 7, 3, 11 };

        [Test]
        public void Bushes_SettleNearSixty_NeverACarpet([ValueSource(nameof(BushSeeds))] int seed)
        {
            var r = PlayBushes(true, 40, seed);
            TestContext.Out.WriteLine($"seed {seed}: " + string.Join(" ", r.count.Select((n, i) => $"y{i + 1}:{n}")));
            Assert.That(r.count[15], Is.InRange(12, 50), "year 16: the thickets filling in");
            for (int y = 30; y <= 40; y++) Assert.That(r.count[y - 1], Is.InRange(40, 70), $"year {y}: settled near 60");
            Assert.LessOrEqual(r.count.Max(), 72, "never past the cap by much");
        }

        [Test]
        public void Bushes_GrowInOneOrTwoThickets([ValueSource(nameof(BushSeeds))] int seed)
        {
            var r = PlayBushes(true, 40, seed);
            Assert.GreaterOrEqual(r.sim.State.eco.thicketA, 0, "a first thicket");
            for (int y = 20; y <= 40; y++)
                Assert.LessOrEqual(r.outside[y - 1], Math.Max(3, r.count[y - 1] / 8), $"year {y}: only a few strays outside the thickets");
        }

        [Test]
        public void NoBush_OnALotAPathOrTheGreen([ValueSource(nameof(BushSeeds))] int seed)
        {
            var r = PlayBushes(true, 40, seed);
            for (int y = 1; y <= 40; y++) Assert.AreEqual(0, r.onSettlement[y - 1], $"year {y}: a bush on a lot, path or the green (#334)");
        }

        [Test]
        public void TheVillage_BuildsRoundTheThickets([ValueSource(nameof(BushSeeds))] int seed)
        {
            // #385, the user 2026-11-07: buildings go round the thickets, onto one only when short of room
            var r = PlayBushes(true, 40, seed);
            var map = r.sim.Settlement.Map;
            int heart = 0, built = 0, lots = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (map.lot[c] != 0) lots++;
                if (r.sim.BushThicketZone(c) < 0.5f) continue;
                heart++;
                if (map.lot[c] != 0) built++;
            }
            TestContext.Out.WriteLine($"seed {seed}: {built} of {heart} thicket-heart cells under a lot ({lots} lot cells in all), {r.count[39]} bushes");
            Assert.Greater(heart, 20);
            Assert.LessOrEqual(built, heart / 10, "the village went round the thickets");
        }

        [Test]
        public void TheWoodsDontTakeTheThickets()
        {
            // a thicket chokes seedlings: its heart is still meadow and bush in a grown box
            var r = PlayBushes(true, 40, 7);
            var e = r.sim.State.eco;
            int heart = 0, wood = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (r.sim.BushThicketZone(c) < 0.99f) continue;
                heart++;
                var cv = (EcoCover)e.cover[c];
                if (cv == EcoCover.Seedling || cv == EcoCover.Young || cv == EcoCover.Tree) wood++;
            }
            Assert.Greater(heart, 10);
            Assert.LessOrEqual(wood, heart / 5, $"{wood} of {heart} heart cells went to wood");
        }

        [Test]
        public void AnOldCarpet_ThinsBackOverAFewYears()
        {
            var sim = EcoWorld(7); var keeper = new Keeper(true);
            for (int s = 0; s < 30 * 4; s++) { keeper.Play(sim); sim.Advance(0.25); }
            // an old save: no thickets yet, the meadow a carpet of bushes
            sim.State.eco.thicketA = sim.State.eco.thicketB = -1;
            sim.DebugCarpetBushes();
            var cells = new List<int>();
            sim.BushCells(cells);
            int carpet = cells.Count, cap = sim.BushCap;
            Assert.Greater(carpet, 400, "the carpet");
            var years = new List<int>();
            for (int y = 1; y <= 6; y++)
            {
                for (int s = 0; s < 4; s++) { keeper.Play(sim); sim.Advance(0.25); }
                sim.BushCells(cells); years.Add(cells.Count);
            }
            TestContext.Out.WriteLine($"carpet {carpet}, cap {cap}: " + string.Join(" ", years.Select((n, i) => $"+{i + 1}y:{n}")));
            Assert.Greater(years[0], cap * 5, "not all at once: a year on, still plenty");
            Assert.Greater(years[1], cap * 2, "two years on, still thinning");
            Assert.LessOrEqual(years[5], cap * 1.5f, "six years on: back near the cap");
            Assert.GreaterOrEqual(years[5], cap / 2, "the thickets themselves stay");
        }

        [Test]
        public void TheFirstBush_IsStillASign_AndTheSeedStillComes()
        {
            var r = Play(true, Seasons30);
            float bush = r.Year(EcoMilestones.FirstBush), seed = r.Year(EcoMilestones.SeedUnlocked);
            Assert.That(bush, Is.InRange(0f, 10f), "first bush");
            Assert.That(seed, Is.InRange(bush, 14f), "the seed follows the first bushes");
            Assert.AreEqual(1, r.sim.State.chronicle.Count(c => c.kind == "eco_bush"), "told once");
        }

        [Test]
        public void DebugAddBushes_GoInTheThickets()
        {
            var r = PlayBushes(true, 20, 3);
            var sim = r.sim;
            var before = new List<int>(); sim.BushCells(before);
            int made = sim.DebugAddBushes(4);
            Assert.AreEqual(4, made);
            var after = new List<int>(); sim.BushCells(after);
            foreach (var c in after.Except(before)) Assert.Greater(sim.BushThicketZone(c), 0.5f, "a new bush in a thicket's heart");
        }

        [Test, Explicit("measurement: prints the 62-year bush curve (#385 before/after)")]
        public void BushCurve_Measure()
        {
            var sb = new StringBuilder("seed,care,year,bush\n");
            foreach (int seed in BushSeeds)
                foreach (bool care in new[] { true, false })
                {
                    var r = PlayBushes(care, 62, seed);
                    for (int y = 0; y < r.count.Count; y++) sb.AppendLine($"{seed},{(care ? 1 : 0)},{y + 1},{r.count[y]}");
                }
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bushcurve.csv"), sb.ToString());
            TestContext.Out.WriteLine(sb.ToString());
        }
    }
}
