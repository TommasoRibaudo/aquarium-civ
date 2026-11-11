// #569 pacing lab: scripted Keepers play the GAME's real sim (SimCore + EcoModel + EcoTuning, not a copy) on the real-hour clock
// (1 real hour = 1 game year) and the year of each gate is read off. Lives here (dotnet test only), beside CameraGlassTests.
//
// The Keepers (assumptions, written in docs/design/placed-life.md "Pacing (#569)"):
//   Active     six visits a day (08:00 10:00 12:30 16:00 19:00 22:30) plus a second visit half an hour into the first session.
//   UserHabit  one visit every 4.5 h (08:00 12:30 17:00 21:30), offline overnight (the game's own catch-up: 8 h at full speed, the rest at a quarter).
//   NeverMists the Active schedule, places and plants but never mists (except the first run's one rain at the start).
// Every visit: place what is ready (by the camp), plant the seed, wipe the glass, pluck a weed, then SIX mist strokes of 3 s each
// (amount 1.5, reach 0.93; about 20 s of spray, what a finger does in a visit). The strokes go: the Keeper's tree; the wild seedlings and
// young trees (up to 3 strokes, most covered first); the open ground round the grown trees where a seed would fall (up to 3); the rest
// to the driest ground near the camp, spreading out (the lab's old rule). The player sees the saplings, so they water them.
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
    public enum Habit { Active, UserHabit, NeverMists }

    public sealed class PacingResult
    {
        public int seed; public Habit habit; public bool before;
        /// <summary>Game years of each gate (-1 = not reached in the run) and the real hour it fell on.</summary>
        public float seedUnlocked = -1, planted = -1, firstWood = -1, camp = -1, pond20 = -1, trees8 = -1, village = -1, villageHours = -1, treeGrown = -1, lastYear;
        public int visits, trees, pond;
        public override string ToString() => $"{habit,-10} seed {seed,3}: camp {F(camp)} pond20 {F(pond20)} trees8 {F(trees8)} village {F(village)} (real h {F(villageHours)})  [seed unlocked {F(seedUnlocked)} planted {F(planted)} keeper tree grown {F(treeGrown)} first wild tree {F(firstWood)}]";
        public static string F(float y) => y < 0 ? "  -  " : y.ToString("0.0").PadLeft(5);
    }

    public static class PacingLab
    {
        public static readonly int[] Seeds = { 7, 21, 42 };
        const int StrokesPerVisit = 6;
        const float StrokeSeconds = 3f;

        /// <summary>The numbers before #569 (the tuning the table's "before" rows ran on).</summary>
        public static EcoTuning Before()
        {
            var t = new EcoTuning();
            t.wateredTake = t.wateredGreen = t.wateredGrow = t.wateredKeeper = 0f;
            t.seedsPerBird = 0.3f; t.maxSeedsPerSeason = 25; t.thicketChoke = 0.9f;
            return t;
        }

        static readonly float[] ActiveDay = { 0f, 2f, 4.5f, 8f, 11f, 14.5f };       // hours after 08:00
        static readonly float[] HabitDay = { 0f, 4.5f, 9f, 13.5f };

        public static List<float> Schedule(Habit h, int days)
        {
            var v = new List<float>();
            var day = h == Habit.UserHabit ? HabitDay : ActiveDay;
            for (int d = 0; d < days; d++)
                foreach (var x in day) v.Add(d * 24f + x);
            if (h != Habit.UserHabit) v.Insert(1, 0.5f); // the first session, half an hour
            return v;
        }

        public static PacingResult Run(Habit habit, int seed, EcoTuning tuning = null, int maxYears = 60, bool before = false)
        {
            var cfg = new SimConfig { ecology = true };
            cfg.eco = tuning ?? new EcoTuning();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            var res = new PacingResult { seed = seed, habit = habit, before = before };
            var visits = Schedule(habit, maxYears / 24 + 3);
            double real = 0; // real hours since the start
            double step = cfg.StepHours;
            var covered = new bool[SiteMap.N];
            for (int i = 0; i < visits.Count && sim.State.month < maxYears * 12; i++)
            {
                real = visits[i];
                Visit(sim, habit, i == 0, covered);
                res.visits++;
                // away until the next visit: the game's own catch-up pace (full speed for 8 h, a quarter after)
                double gap = i + 1 < visits.Count ? visits[i + 1] - visits[i] : 24;
                double simH = cfg.OfflineSimHours(gap);
                int months = (int)Math.Round(simH / step);
                for (int m = 0; m < months; m++)
                {
                    sim.Advance(step);
                    var e = sim.State.eco;
                    float y = sim.State.month / 12f;
                    double rt = real + gap * (m + 1) / months;
                    if (res.camp < 0 && e.campGateMonth >= 0) res.camp = e.campGateMonth / 12f;
                    if (res.pond20 < 0 && e.cPond >= cfg.eco.pondGateCells) res.pond20 = y;
                    if (res.trees8 < 0 && e.cTrees >= cfg.eco.villageTrees) res.trees8 = y;
                    if (res.treeGrown < 0 && e.treeStage >= 4) res.treeGrown = y;
                    if (res.village < 0 && e.villageGateMonth >= 0) { res.village = e.villageGateMonth / 12f; res.villageHours = (float)rt; }
                }
                if (res.village >= 0) break;
            }
            long M(string k) { long m = sim.EcoMilestoneMonth(k); return m; }
            res.seedUnlocked = M(EcoMilestones.SeedUnlocked) / 12f; res.planted = M(EcoMilestones.TreePlanted) / 12f; res.firstWood = M(EcoMilestones.FirstWoodTree) / 12f;
            res.lastYear = sim.State.month / 12f; res.trees = sim.State.eco.cTrees; res.pond = sim.State.eco.cPond;
            return res;
        }

        // ---------------------------------------------------------------- one visit
        static void Visit(SimCore sim, Habit habit, bool first, bool[] covered)
        {
            var e = sim.State.eco;
            var land = sim.EcoGround;
            if (habit == Habit.NeverMists && first) { var p = Spots(sim, land, 1, covered); sim.Mist(p[0].x, p[0].z, 2f); } // the first run's rain
            EcoTests.PlaceReadyJars(sim);
            if (e.treeCell < 0 && sim.CanPlantTree) { var ps = PlantSpot(sim, land); sim.PlantTree(ps.x, ps.z); }
            if (habit != Habit.NeverMists)
            {
                Array.Clear(covered, 0, covered.Length);
                var strokes = new List<(float x, float z)>();
                if (e.treeCell >= 0 && e.treeStage < 4) strokes.Add((SiteMap.CXc(e.treeCell), SiteMap.CZc(e.treeCell)));
                ForestStrokes(sim, land, strokes, covered);
                var rest = StrokesPerVisit - strokes.Count;
                if (rest > 0) strokes.AddRange(Spots(sim, land, rest, covered));
                foreach (var s in strokes.Take(StrokesPerVisit)) sim.Mist(s.x, s.z, StrokeSeconds, StrokeSeconds);
            }
            sim.CleanGrime(1f);
            sim.Prune(1f);
        }

        static float Reach(SimCore sim) => sim.EcoMistRadius(StrokeSeconds);

        static void Cover(bool[] covered, float x, float z, float r)
        {
            for (int c = 0; c < SiteMap.N; c++)
                if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) <= r) covered[c] = true;
        }

        /// <summary>The strokes that grow the wood: on the wild seedlings and young trees, then on the open ground round the grown trees.</summary>
        static void ForestStrokes(SimCore sim, EcoLand land, List<(float x, float z)> strokes, bool[] covered)
        {
            var e = sim.State.eco;
            float r = Reach(sim);
            int budget = StrokesPerVisit - strokes.Count;
            foreach (var s in strokes) Cover(covered, s.x, s.z, r);
            // a) saplings: the stroke centred on the sapling that covers the most uncovered saplings
            var saps = new List<int>(); var trees = new List<int>();
            for (int c = 0; c < SiteMap.N; c++)
            {
                var cv = (EcoCover)e.cover[c];
                if (c == e.treeCell) continue;
                if (cv == EcoCover.Seedling || cv == EcoCover.Young) saps.Add(c);
                else if (cv == EcoCover.Tree) trees.Add(c);
            }
            for (int k = 0; k < 3 && budget > 0; k++)
            {
                int best = -1, bn = 0;
                foreach (var c in saps)
                {
                    if (covered[c]) continue;
                    int n = 0;
                    foreach (var o in saps) if (!covered[o] && Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(o), SiteMap.CZc(o)) <= r) n++;
                    if (n > bn) { bn = n; best = c; }
                }
                if (best < 0) break;
                float x = SiteMap.CXc(best), z = SiteMap.CZc(best);
                strokes.Add((x, z)); Cover(covered, x, z, r); budget--;
            }
            // b) round the grown trees: open ground a seed could take, not yet watered this visit
            for (int k = 0; k < 3 && budget > 0 && trees.Count > 0; k++)
            {
                float bx = 0, bz = 0; int bn = 0;
                foreach (var t in trees)
                    for (int a = -1; a < 6; a++)
                    {
                        float x = SiteMap.CXc(t) + (a < 0 ? 0f : MathF.Cos(a * MathF.PI / 3f) * 0.5f), z = SiteMap.CZc(t) + (a < 0 ? 0f : MathF.Sin(a * MathF.PI / 3f) * 0.5f);
                        if (MathF.Abs(x) > Terrain.HX - 0.2f || MathF.Abs(z) > Terrain.HZ - 0.2f) continue;
                        int n = 0;
                        for (int c = 0; c < SiteMap.N; c++)
                        {
                            if (covered[c] || land.InBasin(c) || land.yard[c]) continue;
                            var cv = (EcoCover)e.cover[c];
                            if (cv != EcoCover.Grass && cv != EcoCover.Tall && cv != EcoCover.Bush && cv != EcoCover.Bare) continue;
                            if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) <= r) n++;
                        }
                        if (n > bn) { bn = n; bx = x; bz = z; }
                    }
                if (bn < 8) break;
                strokes.Add((bx, bz)); Cover(covered, bx, bz, r); budget--;
            }
        }

        /// <summary>The lab's old rule: the driest of ten candidates round the camp, the reach growing as the player mists more.</summary>
        static int spotsMisted;
        static List<(float x, float z)> Spots(SimCore sim, EcoLand land, int count, bool[] covered)
        {
            var e = sim.State.eco; var list = new List<(float, float)>();
            if (e.season <= 1) spotsMisted = 0;
            for (int k = 0; k < count; k++)
            {
                float reach = MathF.Min(2.8f, 0.7f + spotsMisted * 0.08f);
                float best = float.MaxValue, bx = land.campX, bz = land.campZ;
                for (int tries = 0; tries < 10; tries++)
                {
                    float a = Terrain.Hash3(e.seed * 31 + 101, e.season * 16 + k, tries) * MathF.PI * 2f;
                    float rr = MathF.Sqrt(Terrain.Hash3(e.seed * 31 + 102, e.season * 16 + k, tries)) * reach;
                    float x = land.campX + MathF.Cos(a) * rr, z = land.campZ + MathF.Sin(a) * rr;
                    if (MathF.Abs(x) > Terrain.HX - 0.2f || MathF.Abs(z) > Terrain.HZ - 0.2f) continue;
                    int c = SiteMap.CellAt(x, z);
                    if (c < 0 || (EcoCover)e.cover[c] == EcoCover.Water) continue;
                    float score = e.moisture[c] + ((EcoCover)e.cover[c] == EcoCover.Bare ? 0f : 0.3f);
                    if (score < best) { best = score; bx = x; bz = z; }
                }
                list.Add((bx, bz)); spotsMisted++;
            }
            return list;
        }

        static (float x, float z) PlantSpot(SimCore sim, EcoLand land)
        {
            var e = sim.State.eco;
            float best = float.MinValue, bx = land.campX, bz = land.campZ;
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * MathF.PI * 2f, r = 0.8f + 0.2f * (k % 2);
                float x = land.campX + MathF.Cos(a) * r, z = land.campZ + MathF.Sin(a) * r;
                int c = SiteMap.CellAt(x, z);
                if (c < 0 || land.InBasin(c) || land.yard[c] || land.slope[c] > 0.3f) continue;
                float score = e.moisture[c] - 0.1f * Terrain.Dist(x, z, 0f, 0f);
                if (score > best) { best = score; bx = SiteMap.CXc(c); bz = SiteMap.CZc(c); }
            }
            return (bx, bz);
        }

        public static string Table(EcoTuning tuning, bool before)
        {
            var sb = new StringBuilder();
            foreach (Habit h in Enum.GetValues(typeof(Habit)))
                foreach (var s in Seeds)
                    sb.AppendLine(Run(h, s, tuning, 60, before).ToString());
            return sb.ToString();
        }
    }

    public class PacingLabTests
    {
        static readonly int[] Eight = { 7, 21, 42, 3, 5, 11, 13, 17 };
        static float Mean(IEnumerable<float> v) => v.Average();

        [Test]
        public void ActivePlayer_ReachesTheVillage_AtYear25PlusOrMinus3()
        {
            var runs = PacingLab.Seeds.Select(s => PacingLab.Run(Habit.Active, s)).ToList();
            foreach (var r in runs) TestContext.Out.WriteLine(r);
            foreach (var r in runs) { Assert.Greater(r.village, 0f, $"seed {r.seed} reaches the village"); Assert.That(r.village, Is.InRange(21f, 29f), $"seed {r.seed}: every box within 4 years of 25"); }
            Assert.That(Mean(runs.Select(r => r.village)), Is.InRange(22f, 28f), "the 3 lab seeds: the village at year 25 +-3");
            Assert.That(Mean(runs.Select(r => r.villageHours)), Is.InRange(22f, 29f), "about 25 real hours after starting");
        }

        [Test]
        public void ActivePlayer_EightBoxes_MeanIsYear25PlusOrMinus3_NoBoxIsFarOff()
        {
            var ys = Eight.Select(s => PacingLab.Run(Habit.Active, s).village).ToList();
            TestContext.Out.WriteLine("8 seeds: " + string.Join(" ", ys.Select(PacingResult.F)));
            Assert.That(ys.Min(), Is.GreaterThan(0f));
            Assert.That(Mean(ys), Is.InRange(22f, 28f));
            Assert.That(ys.Min(), Is.GreaterThan(19f)); Assert.That(ys.Max(), Is.LessThan(31f));
        }

        [Test]
        public void TheUsersHabit_OneVisitEvery4Point5Hours_ComesAFewYearsAfter()
        {
            var act = PacingLab.Seeds.Select(s => PacingLab.Run(Habit.Active, s).village).ToList();
            var hab = PacingLab.Seeds.Select(s => PacingLab.Run(Habit.UserHabit, s)).ToList();
            foreach (var r in hab) TestContext.Out.WriteLine(r);
            foreach (var r in hab) Assert.Greater(r.village, 0f);
            float gap = Mean(hab.Select(r => r.village)) - Mean(act);
            TestContext.Out.WriteLine($"gap {gap:0.0} years");
            Assert.That(gap, Is.InRange(2f, 8f), "later than the active player, but not by decades (the rest is the one-placement-per-visit chain, #434)");
            Assert.That(Mean(hab.Select(r => r.village)), Is.LessThan(34f));
        }

        [Test]
        public void NeverMisting_IsTheSlowestWay_AndTheVillageIsFarOff()
        {
            foreach (var s in PacingLab.Seeds)
            {
                var r = PacingLab.Run(Habit.NeverMists, s);
                TestContext.Out.WriteLine(r);
                Assert.That(r.village < 0f || r.village > 40f, $"seed {s}: without the mister the village is past year 40");
            }
        }

        [Test]
        public void Before_TheActivePlayerWasFarPast25()
        {
            // the tuning of build 25 (PacingLab.Before) on the same player: the tester's complaint, reproduced
            var ys = PacingLab.Seeds.Select(s => PacingLab.Run(Habit.Active, s, PacingLab.Before(), 60, true).village).ToList();
            TestContext.Out.WriteLine("before: " + string.Join(" ", ys.Select(PacingResult.F)));
            Assert.That(Mean(ys), Is.GreaterThan(32f));
        }

        [Test, Explicit("prints the before/after table (docs/design/placed-life.md, Pacing (#569))")]
        public void PrintTable()
        {
            TestContext.Out.WriteLine("BEFORE (tuning of build 25)\n" + PacingLab.Table(PacingLab.Before(), true));
            TestContext.Out.WriteLine("AFTER\n" + PacingLab.Table(new EcoTuning(), false));
        }
    }
}
