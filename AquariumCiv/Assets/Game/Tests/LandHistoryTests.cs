using System;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Replay;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#287 phase 1: the history keeps the living land per year (cover, pond, tree, rabbits), the replay hands it out and
    /// blends it, the save stays small, old saves and ecology-off boxes are untouched.</summary>
    public class LandHistoryTests
    {
        const int Y = SimConfig.MonthsPerYear;
        static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        string dir, path;

        [SetUp] public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "aqciv-land-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(dir, "box.json");
        }
        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        static SimCore EcoBox(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>A light-handed Keeper: three misted spots each season, from a fixed rotation over the box.</summary>
        static void Play(SimCore s, int years)
        {
            for (int m = 0; m < years * Y; m++)
            {
                if (m % 3 == 0 && s.EcologyOn)
                    for (int k = 0; k < 3; k++)
                    {
                        int cell = (m / 3 * 211 + k * 877 + 301) % 2640;
                        s.Mist(SiteMap.CXc(cell), SiteMap.CZc(cell), 2f);
                    }
                s.Step();
                if (s.State.eraReady) s.AdvanceEra();
            }
        }

        [Test]
        public void ScrubbingAGrowingBoxShowsTheGreenRise()
        {
            var sim = EcoBox(); Play(sim, 40);
            var t = new TimeLapse(sim);
            var first = t.Frame(0);
            Assert.IsNotNull(first.land, "year 0 has a land record");
            Assert.AreEqual(2640, first.land.cover.Length);
            float prev = first.land.GreenShare;
            Assert.Less(prev, 0.02f, "a barren start");
            for (int y = 0; y <= t.MaxYear; y++) Assert.IsNotNull(t.Frame(y).land, "year " + y);
            float lastShare = prev;
            for (float p = 0; p <= t.EndPosition; p += 0.5f)
            {
                var land = t.Scrub(p).Land;
                Assert.IsNotNull(land);
                Assert.GreaterOrEqual(land.GreenShare, lastShare - 0.15f, "no wild swings at " + p);
                lastShare = land.GreenShare;
            }
            float end = t.Scrub(t.EndPosition).Land.GreenShare;
            Console.WriteLine($"green share y0 {prev:F3} y10 {t.Frame(10).land.GreenShare:F3} y20 {t.Frame(20).land.GreenShare:F3} now {end:F3}");
            Assert.Greater(end, prev + 0.2f, "the green rises from year 0 to now");
            Assert.Greater(t.Frame(20).land.GreenShare, t.Frame(1).land.GreenShare);
        }

        [Test]
        public void RecordsMatchTheLiveLandAndNowIsTheLiveCover()
        {
            var sim = EcoBox(); Play(sim, 25);
            var t = new TimeLapse(sim);
            var now = t.Now().land;
            CollectionAssert.AreEqual(sim.State.eco.cover, now.cover);
            Assert.AreEqual(sim.State.eco.pondLevel, now.pondLevel);
            Assert.AreEqual(sim.State.eco.treeStage, now.treeStage);
            var rep = EcoBox(); Play(rep, 20);
            CollectionAssert.AreEqual(rep.State.eco.cover, t.Frame(20).land.cover, "year 20 record == a fresh run stopped at year 20");
            Assert.AreEqual((int)Math.Round(rep.State.eco.rabbits), t.Frame(20).land.rabbits);
        }

        [Test]
        public void ABlendBetweenTwoYearsLerpsPondAndShare()
        {
            var sim = EcoBox(); Play(sim, 30);
            var t = new TimeLapse(sim);
            var s = t.Scrub(12.25f);
            Assert.AreEqual(12, s.yearA); Assert.AreEqual(13, s.yearB);
            var l = s.Land;
            Assert.AreEqual(0.25f, l.t, 1e-4f);
            float ga = t.Frame(12).land.GreenShare, gb = t.Frame(13).land.GreenShare;
            Assert.AreEqual(ga + (gb - ga) * 0.25f, l.GreenShare, 1e-4f);
            for (int c = 0; c < l.CellCount; c++)
            {
                l.Cell(c, out var from, out var to, out _);
                Assert.AreEqual(l.Changes(c), from != to);
                Assert.AreEqual(from, l.CoverAt(c)); // under half way: the earlier year's
            }
            Assert.AreEqual(t.Frame(30).land.pondLevel, t.Scrub(30f).Land.PondLevel, 1e-4f);
        }

        [Test]
        public void TheRecordsRoundTripThroughTheSave()
        {
            var sim = EcoBox(); Play(sim, 35);
            SaveStore.Write(path, sim.State, Now);
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(e.state));
            var loaded = new SimCore(new SimConfig { ecology = true }, e.state);
            var a = new TimeLapse(sim); var b = new TimeLapse(loaded);
            Assert.AreEqual(a.MaxYear, b.MaxYear);
            for (int y = 0; y <= a.MaxYear; y += 3)
            {
                CollectionAssert.AreEqual(a.Frame(y).land.cover, b.Frame(y).land.cover, "year " + y);
                Assert.AreEqual(a.Frame(y).land.rabbits, b.Frame(y).land.rabbits);
            }
            Play(loaded, 3); // the loaded box keeps recording where it left off
            Assert.IsNotNull(new TimeLapse(loaded).Frame(37).land);
        }

        [Test]
        public void AnOldSaveWithoutLandRecordsLoadsAndPlaysAsBefore()
        {
            var sim = EcoBox(); Play(sim, 15);
            sim.State.history.land.Clear(); sim.State.history.lastLandYear = -1; // the format before #287
            SaveStore.Write(path, sim.State, Now);
            SaveStore.Read(path, out var e);
            var loaded = new SimCore(new SimConfig { ecology = true }, e.state);
            var t = new TimeLapse(loaded);
            for (int y = 0; y <= t.MaxYear; y++) { Assert.IsNotNull(t.Frame(y)); Assert.IsNull(t.Frame(y).land); }
            Assert.IsNull(t.Scrub(5.5f).Land);
            Assert.IsNull(t.Now().land);
            Play(loaded, 5); // plays on; from the load on, the year starts are recorded
            var t2 = new TimeLapse(loaded);
            Assert.IsNull(t2.Frame(10).land, "the years before the load stay without land");
            Assert.IsNotNull(t2.Frame(18).land);
            Assert.IsNotNull(t2.Scrub(18.5f).Land);
        }

        [Test]
        public void WithTheEcologyOffNothingIsRecorded()
        {
            var sim = NewSim(3);
            for (int m = 0; m < 30 * Y; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            Assert.AreEqual(0, sim.State.history.land.Count);
            var t = new TimeLapse(sim);
            for (int y = 0; y <= t.MaxYear; y++) Assert.IsNull(t.Frame(y).land);
            Assert.IsNull(t.Now().land);
            Assert.IsNull(t.Scrub(7.3f).Land);
        }

        [Test]
        public void TheSaveStaysSmall()
        {
            foreach (int years in new[] { 50, 200 })
            {
                var sim = EcoBox(); Play(sim, years);
                int landChars = sim.LandRecordBytes();
                SaveStore.Write(path, sim.State, Now);
                long file = new FileInfo(path).Length;
                int steps = sim.State.history.land.Count;
                int keys = sim.State.history.land.Count(x => x.keyframe);
                Console.WriteLine($"LANDSIZE {years} years: {steps} records ({keys} keyframes), land JSON ~{landChars} B = {landChars / (float)steps:F0} B/year; whole save {file} B ({landChars * 100f / file:F1}% land)");
                Assert.Less(landChars / (float)steps, 1500f, "bytes per year");
                Assert.Less(landChars, years == 50 ? 60000 : 220000, "land records of a " + years + "-year box");
            }
        }
    }
}
