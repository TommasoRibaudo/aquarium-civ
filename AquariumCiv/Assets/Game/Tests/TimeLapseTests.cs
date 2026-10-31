using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Replay;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The time-lapse replay model (#78 phase 1): frames, diffs that rebuild the next year, scrubbing, era bands,
    /// story pins, the playback clock, and that scrubbing a whole run is cheap.</summary>
    public class TimeLapseTests
    {
        const int Y = SimConfig.MonthsPerYear;
        SimCore sim;

        static void Run(SimCore s, int years)
        {
            for (int m = 0; m < years * Y; m++)
            {
                s.Step();
                if (s.State.eraReady) s.AdvanceEra();
            }
        }

        [OneTimeSetUp]
        public void Setup()
        {
            sim = NewSim(3);
            Run(sim, 120);
        }

        [Test]
        public void FramesMatchTheSimsBoxAtAndAreCached()
        {
            var t = new TimeLapse(sim);
            Assert.AreEqual(120, t.MaxYear);
            Assert.IsNull(t.Frame(-1));
            Assert.IsNull(t.Frame(121));
            var f = t.Frame(40);
            Assert.AreSame(f, t.Frame(40), "cached");
            Assert.AreEqual(sim.BoxAt(40).buildings.Count, f.buildings.Count);
            Assert.AreEqual(1, t.FramesCached);
        }

        [Test]
        public void ADiffRebuildsTheNextYearFromThisOne()
        {
            var t = new TimeLapse(sim);
            int appeared = 0, disappeared = 0, relooked = 0, pathChanges = 0, eraChanges = 0;
            for (int y = 0; y < t.MaxYear; y++)
            {
                var a = t.Frame(y); var b = t.Frame(y + 1);
                var d = t.Diff(y, y + 1);
                // buildings: apply the diff to a
                var ids = new HashSet<int>(a.buildings.Select(x => x.id));
                foreach (var x in d.disappeared) Assert.IsTrue(ids.Remove(x.id), $"year {y}: a disappearing building stood");
                foreach (var x in d.appeared) Assert.IsTrue(ids.Add(x.id), $"year {y}: an appearing building was not there");
                CollectionAssert.AreEquivalent(b.buildings.Select(x => x.id), ids, $"year {y}");
                foreach (var c in d.relooked) Assert.AreEqual(c.from.id, c.to.id);
                // paths: apply the diff to a
                var cells = a.paths.ToDictionary(p => p.cell);
                foreach (var p in d.pathsRemoved) Assert.IsTrue(cells.Remove(p.cell));
                foreach (var p in d.pathsAdded) cells.Add(p.cell, p);
                foreach (var p in d.pathsChanged) cells[p.cell] = p;
                Assert.AreEqual(b.paths.Count, cells.Count, $"year {y} path cells");
                foreach (var p in b.paths) { Assert.IsTrue(cells.TryGetValue(p.cell, out var q)); Assert.AreEqual(p.tier, q.tier); Assert.AreEqual(p.laid, q.laid); }
                appeared += d.appeared.Count; disappeared += d.disappeared.Count; relooked += d.relooked.Count;
                pathChanges += d.pathsAdded.Count + d.pathsChanged.Count + d.pathsRemoved.Count;
                if (d.eraChanged) eraChanges++;
            }
            Assert.Greater(appeared, 10, "a town grows");
            Assert.GreaterOrEqual(disappeared, 1, "the camp hearth is cleared");
            Assert.Greater(relooked, 0, "huts become houses");
            Assert.Greater(pathChanges, 10, "paths wear in");
            Assert.AreEqual(2, eraChanges, "camp to village to town");
        }

        [Test]
        public void ADiffAcrossManyYearsWorksToo()
        {
            var t = new TimeLapse(sim);
            var d = t.Diff(0, 100);
            var back = t.Diff(100, 0);
            Assert.AreEqual(d.appeared.Count, back.disappeared.Count);
            Assert.AreEqual(d.disappeared.Count, back.appeared.Count);
            Assert.AreEqual(d.pathsAdded.Count, back.pathsRemoved.Count);
            Assert.IsTrue(t.Diff(50, 50).IsEmpty);
            Assert.AreSame(d, t.Diff(0, 100), "cached");
        }

        [Test]
        public void ScrubbingGivesTheTwoYearsAndTheBlend()
        {
            var t = new TimeLapse(sim);
            var s = t.Scrub(12.25f);
            Assert.AreEqual(12, s.yearA); Assert.AreEqual(13, s.yearB);
            Assert.AreEqual(0.25f, s.blend, 1e-4f);
            Assert.AreSame(s.a, t.Frame(12));
            Assert.AreSame(s.a, s.Nearest);
            Assert.AreSame(s.b, t.Scrub(12.75f).Nearest);
            var start = t.Scrub(-5f);
            Assert.AreEqual(0, start.yearA); Assert.AreEqual(0f, start.position);
            var end = t.Scrub(9999f);
            Assert.AreEqual(120, end.yearA); Assert.AreEqual(120, end.yearB); Assert.IsTrue(end.diff.IsEmpty);
            Assert.AreEqual(0f, end.blend);
        }

        [Test]
        public void EraBandsFollowTheEraStarts()
        {
            var t = new TimeLapse(sim);
            var bands = t.EraBands();
            Assert.AreEqual(3, bands.Count);
            Assert.AreEqual(0f, bands[0].startYear);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(bands[i].endYear, bands[i + 1].startYear, "bands touch");
                Assert.IsFalse(bands[i].open);
                Assert.AreEqual(sim.EraStartMonth(bands[i + 1].era), bands[i].endMonth);
                Assert.IsNotEmpty(bands[i].title);
            }
            Assert.IsTrue(bands[2].open);
            Assert.AreEqual(t.EndPosition, bands[2].endYear, 1e-4f);
            Assert.AreEqual(Era.Camp, t.EraAt(0.5f));
            Assert.AreEqual(Era.Town, t.EraAt(t.EndPosition));
            Assert.AreEqual(Era.Village, t.EraAt(bands[1].startYear + 0.1f));
        }

        [Test]
        public void StoryPinsAreTheStrongestEntriesInTimeOrder()
        {
            var t = new TimeLapse(sim);
            var pins = t.Pins(12);
            Assert.That(pins.Count, Is.InRange(3, 12));
            for (int i = 1; i < pins.Count; i++) Assert.GreaterOrEqual(pins[i].month, pins[i - 1].month);
            Assert.IsTrue(pins.All(p => p.salience >= 0.5f && p.entryId < SimCore.TermLineIdBase));
            Assert.IsTrue(pins.Any(p => p.hasPos), "some stories happened somewhere");
            foreach (var p in pins.Where(p => p.hasPos)) Assert.That(p.x, Is.InRange(-3f, 3f));
            Assert.AreEqual(pins.Count, t.Pins(12).Select(p => p.entryId).Distinct().Count());
            // crossing
            var first = pins[0];
            var hit = TimeLapse.PinsCrossed(pins, first.year - 0.01f, first.year + 0.01f);
            Assert.AreEqual(first.entryId, hit[0].entryId);
            Assert.IsEmpty(TimeLapse.PinsCrossed(pins, first.year, first.year), "standing still crosses nothing");
            var back = TimeLapse.PinsCrossed(pins, pins.Last().year + 0.01f, 0f);
            Assert.AreEqual(pins.Last().entryId, back[0].entryId, "backwards runs newest first");
        }

        [Test]
        public void TheClockPlaysPausesSeeksAndStopsAtTheEnd()
        {
            var c = new ReplayClock(100f);
            Assert.AreEqual(0f, c.Tick(1f), "paused");
            c.Play();
            Assert.AreEqual(4f, c.Tick(1f), 1e-4f, "default 4 years a second");
            c.FasterStep();
            Assert.AreEqual(8f, c.Speed);
            c.SetSpeed(99); Assert.AreEqual(ReplayClock.Speeds.Last(), c.Speed);
            c.Seek(50f);
            Assert.IsFalse(c.Playing, "a drag pauses");
            c.Seek(-3f); Assert.AreEqual(0f, c.Position);
            c.Seek(98f, pause: false); c.Play();
            c.Tick(1f);
            Assert.AreEqual(100f, c.Position); Assert.IsFalse(c.Playing); Assert.IsTrue(c.AtEnd);
            c.Play();
            Assert.AreEqual(0f, c.Position, "play at the end starts over");
            c.Loop = true; c.Seek(99f, false); c.Play();
            c.Tick(0.25f); // 99 + 4 = 103 wraps to 3
            Assert.AreEqual(3f, c.Position, 1e-3f); Assert.IsTrue(c.Playing);
        }

        [Test]
        public void TheOpenYearIsRebuiltWhenTheSimMovesOn()
        {
            var s = NewSim(5);
            Run(s, 10);
            var t = new TimeLapse(s);
            var f10 = t.Frame(10);
            var f5 = t.Frame(5);
            Run(s, 3);
            Assert.AreEqual(13, t.MaxYear);
            Assert.AreSame(f5, t.Frame(5), "finished years stay cached");
            Assert.AreNotSame(f10, t.Frame(10), "the year that was open is rebuilt");
            Assert.IsNotNull(t.Frame(13));
            Assert.AreEqual(s.BoxAt(10).buildings.Count, t.Frame(10).buildings.Count);
        }

        [Test]
        public void WarmUpFillsTheCacheWithinABudget()
        {
            var t = new TimeLapse(sim);
            Assert.IsFalse(t.WarmUp(60, 0.0), "no budget does nothing");
            Assert.AreEqual(0, t.FramesCached);
            Assert.IsTrue(t.WarmUp(60, 60000));
            Assert.AreEqual(121, t.FramesCached);
        }

        [Test]
        public void ScrubbingAWholeRunIsCheapOnceWarm()
        {
            var t = new TimeLapse(sim);
            var sw = Stopwatch.StartNew();
            for (int y = 0; y <= t.MaxYear; y++) t.Frame(y);
            double cold = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            for (int y = 0; y < t.MaxYear; y++) t.Diff(y, y + 1);
            double diffs = sw.Elapsed.TotalMilliseconds;
            // three drags across the whole run, at 10 steps a year, forwards and back
            sw.Restart();
            int n = 0;
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i <= t.MaxYear * 10; i++)
                {
                    float pos = pass % 2 == 0 ? i / 10f : t.MaxYear - i / 10f;
                    var s = t.Scrub(pos);
                    n += s.diff.Size;
                }
            double drag = sw.Elapsed.TotalMilliseconds;
            int steps = 3 * (t.MaxYear * 10 + 1);
            TestContext.Out.WriteLine($"time-lapse over {t.MaxYear} years: frames cold {cold:F1} ms ({cold / (t.MaxYear + 1):F2} ms each), consecutive diffs {diffs:F1} ms, " +
                $"{steps} scrub steps {drag:F1} ms ({drag * 1000 / steps:F2} us each), diff entries touched {n}, cache {t.FramesCached} frames / {t.DiffsCached} diffs");
            Assert.Less(drag / steps, 0.2, "a warm scrub step is well under a frame");
            Assert.Less(cold, 5000, "building every frame cold is a few seconds at worst (warm it in slices)");
        }
    }
}
