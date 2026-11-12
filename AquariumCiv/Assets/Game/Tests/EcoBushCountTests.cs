using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#385 (playtest #341, the user 2026-11-07), #418 (placed-life spec §5.1): the one berry thicket is placed by the Keeper;
    /// no bush before it, none outside its area, and it fills to its cap of about 22; none on a lot, a path or the green; the settlement
    /// never builds on it; an old carpeted save thins back over a few years.</summary>
    public partial class EcoTests
    {
        /// <summary>A scripted run, year by year: the bush count, the bushes on lots / paths / the green, and those outside the thickets.</summary>
        sealed class BushYears
        {
            public SimCore sim;
            public readonly List<int> count = new List<int>(), onSettlement = new List<int>(), besideLot = new List<int>(), outside = new List<int>(), nearFire = new List<int>();
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
                    int n = 0, on = 0, outs = 0, fire = 0, beside = 0;
                    var g = sim.EcoGround;
                    for (int c = 0; c < EcoLand.N; c++)
                    {
                        if ((EcoCover)e.cover[c] != EcoCover.Bush) continue;
                        n++;
                        if (map.lot[c] != 0 || map.tier[c] >= PathNet.Trail || map.Green(c)) on++;
                        else if (NextToALot(map, c)) beside++;
                        if (sim.BushThicketZone(c) <= 0f) outs++;
                        if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), g.campX, g.campZ) < 0.9f) fire++;
                    }
                    r.count.Add(n); r.onSettlement.Add(on); r.besideLot.Add(beside); r.outside.Add(outs); r.nearFire.Add(fire);
                }
                return bushCache[key] = r;
            }
        }

        static readonly int[] BushSeeds = { 7, 3, 11 };

        [Test]
        public void Bushes_FillTheThicketToItsCap_AndStop([ValueSource(nameof(BushSeeds))] int seed)
        {
            // #418: 3 founders, a bush a season, up to the cap (20-25), then it stops
            var r = PlayBushes(true, 40, seed);
            TestContext.Out.WriteLine($"seed {seed}: " + string.Join(" ", r.count.Select((n, i) => $"y{i + 1}:{n}")));
            int cap = r.sim.BushCap;
            Assert.That(cap, Is.InRange(20, 25), "the spec's 20-25 bushes");
            Assert.AreEqual(cap, r.count[39], "year 40: the thicket holds its cap");
            int full = r.count.FindIndex(n => n >= cap);
            Assert.That(full, Is.InRange(2, 12), "it fills over a few years, not at once");
            for (int y = full; y < 40; y++) Assert.AreEqual(cap, r.count[y], $"year {y + 1}: stays at the cap");
            for (int y = 1; y < full; y++) if (r.count[y - 1] > 0) Assert.LessOrEqual(r.count[y] - r.count[y - 1], 6, $"year {y + 1}: about a bush a season, a few more where the mister watered it (#605)");
        }

        [Test]
        public void Bushes_OnlyInTheThicketsArea_Over40Years([ValueSource(nameof(BushSeeds))] int seed)
        {
            var r = PlayBushes(true, 40, seed);
            var e = r.sim.State.eco;
            Assert.GreaterOrEqual(e.thicketA, 0, "the Keeper placed the thicket");
            Assert.AreEqual(-1, e.thicketB, "exactly one thicket, ever");
            Assert.AreEqual(e.thicketA, r.sim.LifePlacedCell(LifeSpecies.Thicket));
            for (int y = 1; y <= 40; y++) Assert.AreEqual(0, r.outside[y - 1], $"year {y}: a bush outside the thicket's area");
        }

        [Test]
        public void NoBush_BeforeTheThicketIsPlaced([ValueSource(nameof(BushSeeds))] int seed)
        {
            // a box nobody tends: no thicket is ever placed, so no bush ever grows (40 years)
            var none = PlayBushes(false, 40, seed);
            Assert.IsTrue(none.count.All(n => n == 0), "no bush without the Keeper's thicket: " + string.Join(" ", none.count));
            Assert.IsFalse(none.sim.LifeIsPlaced(LifeSpecies.Thicket));
            Assert.IsFalse(none.sim.State.eco.Reached(EcoMilestones.FirstBush));
            // and a tended box has none until the placing
            var care = PlayBushes(true, 40, seed);
            long placed = care.sim.LifePlacedMonth(LifeSpecies.Thicket);
            Assert.GreaterOrEqual(placed, 0);
            Assert.AreEqual(placed, care.sim.EcoMilestoneMonth(EcoMilestones.FirstBush), "the first bushes are the founders");
            for (int y = 1; y <= 40 && y * 12 <= placed; y++) Assert.AreEqual(0, care.count[y - 1], $"year {y}: a bush before the thicket");
        }

        [Test]
        public void NoBush_OnALotAPathOrTheGreen([ValueSource(nameof(BushSeeds))] int seed)
        {
            var r = PlayBushes(true, 40, seed);
            for (int y = 1; y <= 40; y++) Assert.AreEqual(0, r.onSettlement[y - 1], $"year {y}: a bush on a lot, path or the green (#334)");
        }

        static bool NextToALot(SiteMap map, int c)
        {
            int x = c % SiteMap.NX, z = c / SiteMap.NX;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if ((dx != 0 || dz != 0) && nx >= 0 && nz >= 0 && nx < SiteMap.NX && nz < SiteMap.NZ && map.lot[nz * SiteMap.NX + nx] != 0) return true;
                }
            return false;
        }

        [Test]
        public void NoBush_BesideALot([ValueSource(nameof(BushSeeds))] int seed)
        {
            // verify run 2026-11-07: two bushes in the cells beside a house's lot reached under its walls (a bush's crown is wider
            // than its cell). A bush keeps one cell of room from every lot.
            var r = PlayBushes(true, 40, seed);
            TestContext.Out.WriteLine($"seed {seed}: beside a lot by year: " + string.Join(" ", r.besideLot.Select((n, i) => $"y{i + 1}:{n}")));
            for (int y = 1; y <= 40; y++) Assert.AreEqual(0, r.besideLot[y - 1], $"year {y}: a bush in the cell beside a lot");
        }

        [Test]
        public void Rabbits_KeepOffTheRingRoundTheFire()
        {
            // verify run 2026-11-07: rabbits grazed right at the camp's fire ring, past the people sitting there; they keep
            // SimCore.RabbitFireMargin from the fire.
            var sim = EcoWorld(7);
            sim.Advance(1.0);
            var blocked = sim.EcoRabbitBlocked();
            Assert.IsNotNull(blocked, "a settled box");
            var g = sim.EcoGround;
            int ring = 0, open = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), g.campX, g.campZ);
                if (d < SimCore.RabbitFireMargin - 0.05f) { ring++; Assert.IsTrue(blocked[c], $"cell {c} at {d:0.00} from the fire is open to the rabbits"); }
                else if (d > 1.2f && !blocked[c]) open++;
            }
            Assert.Greater(ring, 40);
            Assert.Greater(open, 500, "the rest of the land stays theirs");
        }

        [Test]
        public void TheSettlement_NeverBuildsOnTheThicket([ValueSource(nameof(BushSeeds))] int seed)
        {
            // #418, placed-life spec §5.1: the thicket's whole area is reserved, like water: no tent, hut, field, pen, path or yard, ever
            var r = PlayBushes(true, 40, seed);
            var sim = r.sim; var map = sim.Settlement.Map;
            foreach (var t in new[] { BuildingType.Farm, BuildingType.Hut, BuildingType.Hut, BuildingType.Farm, BuildingType.Hut, BuildingType.Lumbermill })
                sim.DebugBuild(t); // crowd the box: every kind of lot has to find room
            sim.Advance(2);
            sim.Settlement.SyncEco();
            int area = 0;
            const byte held = SiteMap.FLaid | SiteMap.FYard | SiteMap.FCommons | SiteMap.FGreen;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (sim.BushThicketZone(c) <= 0f) continue;
                area++;
                Assert.IsTrue(map.Reserved(c), $"cell {c} in the thicket is reserved");
                Assert.AreEqual(0, map.lot[c], $"cell {c} in the thicket under a lot");
                Assert.Less(map.tier[c], PathNet.Trail, $"cell {c} in the thicket under a path");
                Assert.AreEqual(0, map.flags[c] & held, $"cell {c} in the thicket is a yard, commons, green or laid path");
                Assert.AreEqual(0, map.ring[c], $"cell {c} in the thicket held for a family");
            }
            TestContext.Out.WriteLine($"seed {seed}: {area} thicket cells, {sim.State.settle.lots.Count} lots, era {sim.State.era}");
            Assert.That(area, Is.InRange(30, 120), "an area about three tents wide");
            Assert.Greater(sim.State.settle.lots.Count, 6, "the box did build, round it");
        }

        [Test]
        public void NoBush_CloseToTheFire([ValueSource(nameof(BushSeeds))] int seed)
        {
            // the user 2026-11-07: the thickets a little away from the camp's centre
            var r = PlayBushes(true, 40, seed);
            for (int y = 1; y <= 40; y++) Assert.AreEqual(0, r.nearFire[y - 1], $"year {y}: a bush within 0.9 of the fire");
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
        public void AnOldCarpet_BecomesOneThicket_AndTheRestThinsAway()
        {
            // #418, spec §9: an old save from before the jar, its meadow a carpet of wild bushes. It loads with the thicket placed at
            // the old thicket's centre; the bushes outside it never spread and thin back over the years; the thicket's own stay.
            var sim = EcoWorld(7); var keeper = new Keeper(true);
            for (int s = 0; s < 30 * 4; s++) { keeper.Play(sim); sim.Advance(0.25); }
            var e = sim.State.eco;
            // as saved before the jar: the meadow a carpet of bushes, no jar, #385's thickets unset
            sim.DebugCarpetBushes();
            e.jarOn = false; e.jars = null; e.thicketA = e.thicketB = -1; e.thicketRadius = 0f;
            int carpet = e.cover.Count(v => v == (byte)EcoCover.Bush);
            Assert.Greater(carpet, 300, "the carpet");
            var loaded = new SimCore(sim.Config, UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(sim.State)));
            Assert.IsTrue(loaded.LifeIsPlaced(LifeSpecies.Thicket), "the wild bushes count as the placed thicket");
            Assert.GreaterOrEqual(loaded.State.eco.thicketA, 0);
            Assert.Greater(loaded.State.eco.thicketRadius, 0f);
            Assert.AreEqual(-1, loaded.State.eco.thicketB);
            Assert.AreEqual(loaded.State.eco.thicketA, loaded.LifePlacedCell(LifeSpecies.Thicket));
            var cells = new List<int>();
            int cap = loaded.BushCap, outside0 = Outside(loaded);
            var years = new List<int>(); var outs = new List<int>();
            for (int y = 1; y <= 8; y++)
            {
                for (int s = 0; s < 4; s++) loaded.Advance(0.25);
                loaded.BushCells(cells); years.Add(cells.Count); outs.Add(Outside(loaded));
            }
            TestContext.Out.WriteLine($"carpet {carpet} ({outside0} outside), cap {cap}: " + string.Join(" ", years.Select((n, i) => $"+{i + 1}y:{n}/{outs[i]}")));
            for (int y = 1; y < 8; y++) Assert.LessOrEqual(outs[y], outs[y - 1], "the bushes outside never spread");
            Assert.Greater(years[0], cap * 4, "not all at once: a year on, still plenty");
            Assert.LessOrEqual(years[7], cap * 2.5f, "eight years on: thinned back toward the thicket");
            Assert.GreaterOrEqual(years[7] - outs[7], cap / 2, "the thicket itself stays");
        }

        static int Outside(SimCore sim)
        {
            var e = sim.State.eco; int n = 0;
            for (int c = 0; c < EcoLand.N; c++) if (e.cover[c] == (byte)EcoCover.Bush && sim.BushThicketZone(c) <= 0f) n++;
            return n;
        }

        [Test]
        public void TheFirstBush_IsStillASign_AndTheSeedStillComes()
        {
            var r = Play(true, Seasons30);
            float bush = r.Year(EcoMilestones.FirstBush), seed = r.Year(EcoMilestones.SeedUnlocked);
            Assert.That(bush, Is.InRange(0f, 10f), "first bush");
            Assert.That(seed, Is.InRange(bush, 14f), "the seed follows the thicket (#418)");
            Assert.GreaterOrEqual(seed, r.CampYear, "and the camp");
            Assert.AreEqual(1, r.sim.State.chronicle.Count(c => c.kind == "life_thicket"), "the placing is told once");
            Assert.AreEqual(0, r.sim.State.chronicle.Count(c => c.kind == "eco_bush"), "not twice: the bushes came with the placing");
        }

        [Test]
        public void DebugAddBushes_GoInTheThicket()
        {
            var r = PlayBushes(true, 20, 3);
            var sim = r.sim;
            var before = new List<int>(); sim.BushCells(before);
            int made = sim.DebugAddBushes(4);
            Assert.AreEqual(4, made);
            var after = new List<int>(); sim.BushCells(after);
            foreach (var c in after.Except(before)) Assert.Greater(sim.BushThicketZone(c), 0f, "a new bush inside the thicket");
            // on a box with no thicket yet, the cheat places it by the camp first
            var fresh = EcoWorld(7);
            LifeJarTests.Meadow(fresh, 0.3f);
            Assert.Greater(fresh.DebugAddBushes(2), 0);
            Assert.IsTrue(fresh.LifeIsPlaced(LifeSpecies.Thicket));
            fresh.BushCells(after);
            foreach (var c in after) Assert.Greater(fresh.BushThicketZone(c), 0f);
        }

        [Test, Explicit("measurement: prints the 62-year bush curve (#385 before/after)"), Category("OnDemand")]
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
