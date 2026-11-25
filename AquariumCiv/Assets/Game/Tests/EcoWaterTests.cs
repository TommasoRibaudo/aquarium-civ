using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #687, the water rework (decisions.md 2026-11-15, "the pond answers the stroke"): the soak lands at the next game month
    /// instead of the next season, and a stroke that fed the pond raises its edge a little at once. Same total water: the yearly
    /// totals are set beside the old seasonal soak (<see cref="EcoTuning.soakMonthly"/> and <see cref="EcoTuning.pondAnswers"/> off)
    /// with the same strokes.
    /// </summary>
    public partial class EcoTests
    {
        static SimConfig WaterCfg(bool rework)
        {
            var cfg = new SimConfig { eco = new EcoTuning() };
            if (!rework) { cfg.eco.soakMonthly = false; cfg.eco.pondAnswers = false; }
            return cfg;
        }

        static void Months(SimCore sim, int n) { for (int i = 0; i < n; i++) sim.Advance(sim.Config.StepHours); }

        /// <summary>Months until a season step has just happened (the next month is not one).</summary>
        static void ToSeasonStart(SimCore sim)
        {
            int s0 = sim.State.eco.season;
            for (int i = 0; i < 4 && sim.State.eco.season == s0; i++) Months(sim, 1);
            Assert.AreNotEqual(s0, sim.State.eco.season, "a season step came");
        }

        /// <summary>A box with a young pond: the table set under the hollow's floor-plus, one season, the table back at its peak.</summary>
        static SimCore PondWorld(bool rework, int seed = 7)
        {
            var sim = EcoWorld(seed, WaterCfg(rework));
            sim.DebugSetTable(sim.EcoGround.main.bottom + 0.05f);
            sim.DebugSkipSeasons(1);
            Assert.Greater(sim.EcoPondCells, 0, "a pond");
            return sim;
        }

        /// <summary>A shore cell of the pond (in the hollow, dry, next to the water), as a stroke spot.</summary>
        static (float x, float z) ShoreSpot(SimCore sim)
        {
            var e = sim.State.eco; var land = sim.EcoGround; var nb = new int[8];
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (!land.InBasin(c) || (EcoCover)e.cover[c] == EcoCover.Water) continue;
                int n = EcoLand.Neighbours8(c, nb);
                for (int k = 0; k < n; k++)
                    if ((EcoCover)e.cover[nb[k]] == EcoCover.Water) return (SiteMap.CXc(c), SiteMap.CZc(c));
            }
            Assert.Fail("no shore");
            return default;
        }

        static SimCore Twin(SimCore sim) => new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));

        // ---------------------------------------------------------------- the soak lands next month
        [Test]
        public void TheSoak_ReachesTheTable_WithinOneGameMonth()
        {
            foreach (bool rework in new[] { true, false })
            {
                var with = EcoWorld(7, WaterCfg(rework));
                ToSeasonStart(with);
                var without = Twin(with);
                var land = with.EcoGround;
                for (int k = 0; k < 6; k++) with.Mist(land.campX + 0.5f * MathF.Cos(k), land.campZ + 0.5f * MathF.Sin(k), 3f);
                Months(with, 1); Months(without, 1);
                float rose = with.State.eco.table - without.State.eco.table;
                TestContext.Out.WriteLine($"{(rework ? "monthly" : "seasonal")}: the table one month after six strokes is {rose:0.000000} above the twin's");
                if (rework) Assert.Greater(rose, 0f, "the strokes' water reached the table at the next month");
                else Assert.AreEqual(0f, rose, "before #687 it waited for the season step");
                Months(with, 2); Months(without, 2);
                Assert.Greater(with.State.eco.table, without.State.eco.table, "by the season step it has, either way");
            }
        }

        // ---------------------------------------------------------------- the pond answers the stroke
        [Test]
        public void AStrokeThatFedThePond_RaisesItsLevel_AtOnce()
        {
            var with = PondWorld(true);
            ToSeasonStart(with);
            var without = Twin(with);
            var e = with.State.eco;
            with.DebugSetTable(e.tablePeak); without.DebugSetTable(without.State.eco.tablePeak); // the table at its peak, so a rise shows in the level
            var fed = new List<float>(); var rose = new List<(float a, float b)>();
            with.EcoPondFed += (x, z, r) => fed.Add(r);
            with.EcoPondRose += (a, b) => rose.Add((a, b));
            float level0 = e.pondLevel; int cells0 = with.EcoPondCells;
            var (sx, sz) = ShoreSpot(with);
            with.Mist(sx, sz, 2f);
            Assert.AreEqual(1, fed.Count, "the stroke fed the pond");
            Assert.Greater(fed[0], 0f);
            Assert.Greater(e.pondLevel, level0, "the edge rose at the stroke");
            Assert.AreEqual(1, rose.Count);
            float booked = e.soakTaken;
            Assert.Greater(booked, 0f, "booked against the coming month");
            TestContext.Out.WriteLine($"one 2 s stroke on the shore: level +{e.pondLevel - level0:0.000000} at once (a year's soak: {12 * with.Config.eco.tablePerSoak * 4 / (1 + e.cPond / with.Config.eco.tablePondDamp):0.0000}), pond {cells0} -> {with.EcoPondCells} cells");
            // the next step: still above the twin that had no stroke
            Months(with, 1); Months(without, 1);
            TestContext.Out.WriteLine($"a month on: {with.State.eco.pondLevel - without.State.eco.pondLevel:0.000000} above the twin");
            Assert.Greater(with.State.eco.pondLevel, without.State.eco.pondLevel, "the pond level is higher at the next step than without the stroke");
            Assert.LessOrEqual(e.soakTaken, with.Config.eco.soakCapPerSeason * 2 / 3f + 1e-4f, "within the season's cap, a third a month");
        }

        [Test]
        public void AStrokeFarFromThePond_DoesNotAnswer()
        {
            var sim = PondWorld(true);
            var fed = 0; sim.EcoPondFed += (x, z, r) => fed++;
            float level = sim.State.eco.pondLevel;
            var land = sim.EcoGround;
            var e = sim.State.eco;
            var water = Enumerable.Range(0, EcoLand.N).Where(c => (EcoCover)e.cover[c] == EcoCover.Water || land.InBasin(c)).ToList(); // the pond and its hollow
            int far = -1;
            for (int c = 0; c < EcoLand.N && far < 0; c++)
                if (!land.InBasin(c) && AquariumCiv.Sim.Settle.Terrain.InZ(SiteMap.CZc(c), 0.3f) && MathF.Abs(SiteMap.CXc(c)) < AquariumCiv.Sim.Settle.Terrain.HX - 0.3f
                    && water.All(w => AquariumCiv.Sim.Settle.Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(w), SiteMap.CZc(w)) > 1.6f)) far = c;
            Assert.GreaterOrEqual(far, 0);
            sim.Mist(SiteMap.CXc(far), SiteMap.CZc(far), 2f);
            Assert.AreEqual(0, fed);
            Assert.AreEqual(level, sim.State.eco.pondLevel, "it waits for the month");
        }

        // ---------------------------------------------------------------- same water
        /// <summary>A fixed Keeper: every season, <paramref name="strokes"/> 2 s strokes on fixed spots round the camp (and one on the
        /// pond's shore once there is one), in the season's first, second or third month by turn. Returns the water the table took each year.</summary>
        static double[] YearlyWater(bool rework, int years, bool pond, int seed = 7, int strokes = 4)
        {
            var sim = pond ? PondWorld(rework, seed) : EcoWorld(seed, WaterCfg(rework));
            var land = sim.EcoGround; var e = sim.State.eco;
            var perYear = new double[years];
            for (int y = 0; y < years; y++)
            {
                double start = e.soakedIn;
                for (int m = 0; m < 12; m++)
                {
                    if (m % 3 == (y + m / 3) % 3)
                    {
                        for (int k = 0; k < strokes; k++)
                        {
                            float a = k * 1.7f + m;
                            sim.Mist(land.campX + 0.6f * MathF.Cos(a), land.campZ + 0.6f * MathF.Sin(a), 2f);
                        }
                        if (pond && sim.EcoPondCells > 0) { var (sx, sz) = ShoreSpot(sim); sim.Mist(sx, sz, 2f); }
                    }
                    Months(sim, 1);
                }
                perYear[y] = e.soakedIn - start;
            }
            return perYear;
        }

        [Test]
        public void TheYearlyWater_IsTheSame_SoonerNotMore()
        {
            foreach (bool pond in new[] { false, true })
            {
                var old = YearlyWater(false, 6, pond); var now = YearlyWater(true, 6, pond);
                TestContext.Out.WriteLine($"{(pond ? "with a pond" : "from bare ground")}: old {string.Join(" ", old.Select(v => v.ToString("0.0")))} = {old.Sum():0.0}; " +
                    $"monthly {string.Join(" ", now.Select(v => v.ToString("0.0")))} = {now.Sum():0.0} ({(now.Sum() / old.Sum() - 1) * 100:+0.0;-0.0}%)");
                Assert.That(now.Sum(), Is.EqualTo(old.Sum()).Within(4).Percent, "six years' water");
                for (int y = 1; y < old.Length; y++) Assert.That(now[y], Is.EqualTo(old[y]).Within(10).Percent, $"year {y + 1}");
            }
        }

        // ---------------------------------------------------------------- the save
        [Test]
        public void AnOldSave_WithoutTheMonthlySoak_Loads_AndPlaysOn()
        {
            var sim = PondWorld(true);
            ToSeasonStart(sim);
            var (sx, sz) = ShoreSpot(sim);
            sim.Mist(sx, sz, 2f);
            Months(sim, 1);
            string json = JsonUtility.ToJson(sim.State);
            StringAssert.Contains("\"soakTaken\"", json);
            string old = System.Text.RegularExpressions.Regex.Replace(json, "\"(soakTaken|soakMonths)\":[^,}]*,?", "");
            Assert.AreNotEqual(json, old, "the new fields were cut out");
            var loaded = JsonUtility.FromJson<SimState>(old);
            Assert.AreEqual(0f, loaded.eco.soakTaken); Assert.AreEqual(0, loaded.eco.soakMonths);
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            float table = core.State.eco.table;
            core.Mist(sx, sz, 2f);
            Months(core, 6);
            Assert.GreaterOrEqual(core.State.eco.table, table, "it plays on");
            Assert.Greater(core.EcoPondCells, 0);
            // and a save that has them round-trips
            var again = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(core.State));
            Assert.AreEqual(core.State.eco.soakTaken, again.eco.soakTaken);
            Assert.AreEqual(core.State.eco.soakMonths, again.eco.soakMonths);
            Assert.AreEqual(core.EcoFingerprint, new SimCore(core.Config, again).EcoFingerprint);
        }
    }
}
