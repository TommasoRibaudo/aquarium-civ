using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Replay;
using NUnit.Framework;

namespace AquariumCiv.View.TimeLapseScreen.Tests
{
    /// <summary>#79 phase 1: the time-lapse screen's model. Position and x, bands, pins that never overlap, grouping, tap and
    /// drag and snap, play/pause/speed, captions while pins are crossed, the Now button, the snapshot data, the telemetry.</summary>
    public class TimeLapseScreenTests
    {
        const int Y = SimConfig.MonthsPerYear;
        const float W = 360f;                       // a small portrait phone
        static SimCore sim;
        DateTime utc = new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc);
        MemoryTelemetryStorage disk;

        [OneTimeSetUp]
        public void Once()
        {
            var cfg = new SimConfig();
            sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            for (int m = 0; m < 120 * Y; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
        }

        [SetUp] public void SetUp() { disk = new MemoryTelemetryStorage(); }

        static StoryPin Pin(int id, float year, float salience = 0.6f, int notable = 0) =>
            new StoryPin { entryId = id, year = year, month = (long)(year * Y), salience = salience, notableId = notable, text = "story " + id, kind = "k" };

        static EraBand Band(Era e, float start, float end, bool open = false) =>
            new EraBand { era = e, title = e + " title", startYear = start, endYear = end, open = open };

        static TimelineLayout Lay(float width, float end, IEnumerable<StoryPin> pins, params EraBand[] bands)
        {
            var l = new TimelineLayout();
            l.Build(width, end, bands, pins.ToList());
            return l;
        }

        TelemetryLog NewLog() => new TelemetryLog(disk, () => utc, "t");
        List<TelemetryEvent> Events(TelemetryLog log)
        {
            log.Flush();
            return disk.ReadAll("box-" + log.BoxId + ".jsonl").Split('\n').Where(l => l.Length > 0).Select(TelemetryEvent.Parse).ToList();
        }

        TimeLapseScreenModel Real(TelemetryLog log = null, float width = W, string name = "Reef")
        {
            var lapse = new TimeLapse(sim);
            var m = new TimeLapseScreenModel(lapse, new ReplayClock(lapse.EndPosition), log, name);
            m.Clock.Seek(lapse.EndPosition);
            m.SetWidth(width);
            return m;
        }

        // ---------- position and x ----------
        [Test]
        public void PositionAndXAreInverses()
        {
            var l = Lay(W, 100f, new StoryPin[0], Band(Era.Camp, 0, 100, true));
            Assert.AreEqual(l.TrackLeft, l.XOf(0f), 1e-3);
            Assert.AreEqual(l.TrackRight, l.XOf(100f), 1e-3);
            foreach (float y in new[] { 0f, 12.5f, 50f, 99.9f, 100f })
                Assert.AreEqual(y, l.YearAt(l.XOf(y)), 1e-3);
            Assert.AreEqual(0f, l.YearAt(-50f), 1e-4);          // off the ends clamps
            Assert.AreEqual(100f, l.YearAt(W + 50f), 1e-3);
        }

        [Test]
        public void ANewBoxWithNoHistoryDoesNotDivideByZero()
        {
            var l = Lay(W, 0f, new StoryPin[0], Band(Era.Camp, 0, 0, true));
            Assert.AreEqual(l.TrackLeft, l.XOf(5f));
            Assert.AreEqual(0f, l.YearAt(200f));
        }

        // ---------- era bands ----------
        [Test]
        public void BandsTileTheTrackAndOnlyWideOnesCarryAName()
        {
            var l = Lay(W, 100f, new StoryPin[0], Band(Era.Camp, 0, 40), Band(Era.Village, 40, 95), Band(Era.Town, 95, 100, true));
            Assert.AreEqual(3, l.Bands.Count);
            Assert.AreEqual(l.TrackLeft, l.Bands[0].x0, 1e-3);
            Assert.AreEqual(l.Bands[0].x1, l.Bands[1].x0, 1e-3);
            Assert.AreEqual(l.Bands[1].x1, l.Bands[2].x0, 1e-3);
            Assert.AreEqual(l.TrackRight, l.Bands[2].x1, 1e-3);
            Assert.IsTrue(l.Bands[0].showLabel);
            Assert.IsFalse(l.Bands[2].showLabel, "a 5-year sliver is too narrow for its name");
            Assert.AreEqual(2, l.EraMarkers.Count);
        }

        [Test]
        public void RealBandsRunToTheEndOfTheTimeline()
        {
            var m = Real();
            var last = m.Layout.Bands.Last();
            Assert.AreEqual(m.Layout.TrackRight, last.x1, 1e-2);
            Assert.AreEqual(m.Layout.TrackLeft, m.Layout.Bands[0].x0, 1e-2);
        }

        // ---------- pin layout ----------
        [Test]
        public void MarkersNeverOverlapAtPhoneWidth()
        {
            var rnd = new Random(7);
            for (int trial = 0; trial < 50; trial++)
            {
                var pins = Enumerable.Range(1, 24).Select(i => Pin(i, (float)rnd.NextDouble() * 120f)).ToList();
                var l = Lay(W, 120f, pins, Band(Era.Camp, 0, 120, true));
                for (int i = 1; i < l.Groups.Count; i++)
                    Assert.GreaterOrEqual(l.Groups[i].x - l.Groups[i - 1].x, l.Opt.minPinGap - 1e-3f);
                Assert.AreEqual(24, l.Groups.Sum(g => g.pins.Count), "no pin is lost in grouping");
                Assert.LessOrEqual(l.Groups.Count, (int)(l.TrackWidth / l.Opt.minPinGap) + 1);
            }
        }

        [Test]
        public void SparsePinsStayApartAndDensePinsGroup()
        {
            var sparse = Lay(W, 120f, new[] { Pin(1, 10), Pin(2, 60), Pin(3, 110) }, Band(Era.Camp, 0, 120, true));
            Assert.AreEqual(3, sparse.Groups.Count);
            Assert.IsFalse(sparse.Groups.Any(g => g.IsGroup));
            var dense = Lay(W, 120f, new[] { Pin(1, 50f), Pin(2, 50.5f, 0.9f), Pin(3, 51f) }, Band(Era.Camp, 0, 120, true));
            Assert.AreEqual(1, dense.Groups.Count);
            Assert.IsTrue(dense.Groups[0].IsGroup);
            Assert.AreEqual(2, dense.Groups[0].Lead.entryId, "the strongest story draws the marker");
            Assert.AreEqual(1, dense.Groups[0].pins[0].entryId, "stories stay in time order");
        }

        [Test]
        public void AWiderScreenSplitsWhatANarrowOneGroups()
        {
            var pins = new[] { Pin(1, 50f), Pin(2, 56f) };
            Assert.AreEqual(1, Lay(300f, 120f, pins, Band(Era.Camp, 0, 120, true)).Groups.Count);
            Assert.AreEqual(2, Lay(900f, 120f, pins, Band(Era.Camp, 0, 120, true)).Groups.Count);
        }

        [Test]
        public void RealPinsFitPhoneWidth()
        {
            var m = Real();
            Assert.Greater(m.Pins.Count, 0);
            for (int i = 1; i < m.Layout.Groups.Count; i++)
                Assert.GreaterOrEqual(m.Layout.Groups[i].x - m.Layout.Groups[i - 1].x, m.Opt.minPinGap - 1e-3f);
            Assert.AreEqual(m.Pins.Count, m.Layout.Groups.Sum(g => g.pins.Count));
        }

        // ---------- tap a pin ----------
        [Test]
        public void TappingAPinSeeksThereAndShowsItsStory()
        {
            var log = NewLog();
            var m = Real(log);
            var g = m.Layout.Groups[0];
            m.PointerDown(g.x + 3f);
            m.PointerUp(g.x + 3f);
            Assert.AreEqual(g.Year, m.Position, 1e-4);
            Assert.IsNotNull(m.Caption);
            Assert.AreEqual(g.pins[0].entryId, m.Caption.EntryId);
            Assert.AreEqual(CaptionSource.Tap, m.Caption.source);
            Assert.IsFalse(m.Playing);
            var ev = Events(log);
            Assert.AreEqual(1, ev.Count(e => e.name == Ev.PinTap));
        }

        [Test]
        public void TappingAPinWhilePlayingPausesIt()
        {
            var m = Real();
            m.Clock.Seek(0f, false); m.Play();
            Assert.IsTrue(m.Playing);
            var g = m.Layout.Groups[0];
            m.PointerDown(g.x); m.PointerUp(g.x);
            Assert.IsFalse(m.Playing);
        }

        [Test]
        public void AGroupedMarkerStepsThroughItsStories()
        {
            var lapse = new TimeLapse(sim);
            var m = Real();
            var g = m.Layout.Groups.FirstOrDefault(x => x.IsGroup);
            if (g == null)
            {   // build one on purpose: a narrow control forces grouping
                m.SetWidth(120f);
                g = m.Layout.Groups.First(x => x.IsGroup);
            }
            m.TapPin(g, 0);
            Assert.AreEqual(0, m.Caption.groupIndex);
            Assert.AreEqual(g.pins.Count, m.Caption.groupCount);
            m.NextInGroup();
            Assert.AreEqual(1, m.Caption.groupIndex);
            Assert.AreEqual(g.pins[1].year, m.Position, 1e-4);
            m.NextInGroup(-1); m.NextInGroup(-1);
            Assert.AreEqual(g.pins.Count - 1, m.Caption.groupIndex, "previous wraps round");
        }

        [Test]
        public void ACaptionCanBeDismissedAndADragClearsIt()
        {
            var m = Real();
            m.TapPin(m.Layout.Groups[0], 0);
            m.DismissCaption();
            Assert.IsNull(m.Caption);
            m.TapPin(m.Layout.Groups[0], 0);
            float far = m.Layout.XOf(m.Layout.End * 0.5f);
            m.PointerDown(far); m.PointerMove(far + 40f);
            Assert.IsNull(m.Caption);
        }

        // ---------- drag and snap ----------
        [Test]
        public void DraggingFollowsTheFingerAndPauses()
        {
            var m = Real();
            m.Layout.Build(W, m.Clock.End, new EraBand[] { Band(Era.Camp, 0, m.Clock.End, true) }, new StoryPin[0]);   // bare track: a press starts a drag at once
            m.Play();
            float x0 = m.Layout.XOf(m.Layout.End * 0.5f);
            m.PointerDown(x0);
            Assert.IsTrue(m.Dragging);
            Assert.IsFalse(m.Playing);
            Assert.AreEqual(m.Layout.YearAt(x0), m.Position, 1e-3);
            m.PointerMove(x0 + 50f);
            Assert.AreEqual(m.Layout.YearAt(x0 + 50f), m.Position, 1e-3);
            m.PointerMove(-100f);
            Assert.AreEqual(0f, m.Position, 1e-4, "dragging past the left edge clamps");
            m.PointerMove(W + 100f);
            Assert.AreEqual(m.Clock.End, m.Position, 1e-3);
        }

        [Test]
        public void ReleasingNearNowSnapsToNow()
        {
            var m = Real();
            m.Clock.Seek(10f);
            float x = m.Layout.TrackRight - 8f;
            m.PointerDown(m.Layout.XOf(10f)); m.PointerMove(x); m.PointerUp(x);
            Assert.AreEqual(m.Clock.End, m.Position, 1e-4);
            Assert.IsTrue(m.AtNow);
            Assert.IsFalse(m.NowEnabled);
        }

        [Test]
        public void ReleasingNearAPinSnapsToItAndShowsItsStory()
        {
            var m = Real();
            var g = m.Layout.Groups[0];
            float start = Math.Min(m.Layout.TrackRight - 100f, g.x + 120f);
            m.PointerDown(start);
            m.PointerMove(g.x + 9f);
            m.PointerUp(g.x + 9f);
            Assert.AreEqual(g.Year, m.Position, 1e-4);
            Assert.AreEqual(CaptionSource.Snap, m.Caption.source);
        }

        [Test]
        public void ReleasingNearAnEraStartSnapsToIt()
        {
            var lapse = new TimeLapse(sim);
            var bands = lapse.EraBands();
            if (bands.Count < 2) Assert.Ignore("the test run reached one era only");
            var m = Real();
            m.Layout.Build(W, m.Clock.End, bands, new StoryPin[0]);   // no pins to take the snap first
            float mx = m.Layout.EraMarkers[0];
            m.PointerDown(mx + 40f); m.PointerMove(mx + 6f); m.PointerUp(mx + 6f);
            Assert.AreEqual(bands[1].startYear, m.Position, 1e-3);
        }

        [Test]
        public void ADragFarFromAnythingKeepsItsExactSpot()
        {
            var m = Real();
            m.Layout.Build(W, m.Clock.End, new EraBand[] { Band(Era.Camp, 0, m.Clock.End, true) }, new StoryPin[0]);
            float x = m.Layout.XOf(m.Clock.End * 0.37f);
            m.PointerDown(x); m.PointerMove(x + 1f); m.PointerUp(x + 1f);
            Assert.AreEqual(m.Layout.YearAt(x + 1f), m.Position, 1e-3);
            Assert.IsNull(m.Caption);
        }

        [Test]
        public void ASmallWobbleOnTheBareTrackStillJumpsThere()
        {
            var m = Real();
            m.Layout.Build(W, m.Clock.End, new EraBand[] { Band(Era.Camp, 0, m.Clock.End, true) }, new StoryPin[0]);
            float x = m.Layout.XOf(m.Clock.End * 0.5f);
            m.PointerDown(x); m.PointerUp(x);
            Assert.AreEqual(m.Layout.YearAt(x), m.Position, 1e-3);
        }

        [Test]
        public void ACancelledTouchLeavesNoDragBehind()
        {
            var m = Real();
            m.PointerDown(100f); m.PointerMove(150f); m.PointerCancel();
            Assert.IsFalse(m.Dragging);
            float p = m.Position;
            m.PointerMove(300f);                                   // stray move after the cancel
            Assert.AreEqual(p, m.Position, 1e-6);
        }

        // ---------- the Now button ----------
        [Test]
        public void NowJumpsToThePresentAndIsDisabledThere()
        {
            var m = Real();
            Assert.IsFalse(m.NowEnabled);
            m.Clock.Seek(20f);
            Assert.IsTrue(m.NowEnabled);
            m.GoNow();
            Assert.AreEqual(m.Clock.End, m.Position, 1e-4);
            Assert.IsFalse(m.NowEnabled);
            Assert.IsFalse(m.Playing);
        }

        [Test]
        public void NowWhilePlayingStopsAtThePresent()
        {
            var m = Real();
            m.Clock.Seek(30f, false); m.Play();
            m.GoNow();
            Assert.IsFalse(m.Playing);
            Assert.IsNull(m.Caption);
        }

        // ---------- play, pause, speed ----------
        [Test]
        public void PlayPauseAndTheLabel()
        {
            var m = Real();
            Assert.AreEqual("Replay", m.PlayLabel, "at Now, play starts again from the beginning");
            m.TogglePlay();
            Assert.IsTrue(m.Playing);
            Assert.AreEqual(0f, m.Position, 1e-4);
            Assert.AreEqual("Pause", m.PlayLabel);
            m.TogglePlay();
            Assert.AreEqual("Play", m.PlayLabel);
        }

        [Test]
        public void SpeedStepsCycleAndReadOut()
        {
            var m = Real();
            Assert.AreEqual(ReplayClock.DefaultSpeedIndex, m.SpeedIndex);
            Assert.AreEqual("4 yr/s", m.SpeedLabel);
            for (int i = 0; i < m.SpeedCount; i++) m.CycleSpeed();
            Assert.AreEqual(ReplayClock.DefaultSpeedIndex, m.SpeedIndex, "a full lap returns");
            m.SetSpeed(99); Assert.AreEqual(m.SpeedCount - 1, m.SpeedIndex);
            m.CycleSpeed(); Assert.AreEqual(0, m.SpeedIndex, "after the fastest comes the slowest");
        }

        [Test]
        public void PlayingAdvancesAtTheChosenSpeedAndStopsAtNow()
        {
            var m = Real();
            m.Clock.Seek(0f); m.SetSpeed(1); m.Play();                // 2 yr/s
            Assert.IsTrue(m.Tick(1f));
            Assert.AreEqual(2f, m.Position, 1e-4);
            for (int i = 0; i < 100 && m.Playing; i++) m.Tick(1f);
            Assert.IsFalse(m.Playing);
            Assert.IsTrue(m.AtNow);
        }

        [Test]
        public void ReadoutsFollowThePlayhead()
        {
            var m = Real();
            m.Clock.Seek(0f);
            Assert.AreEqual("Year 1", m.YearLabel);
            Assert.AreEqual(Era.Camp, m.Era);
            m.Clock.Seek(m.Clock.End);
            Assert.AreEqual(m.Lapse.EraAt(m.Clock.End), m.Era);
            Assert.AreEqual((int)m.Clock.End + 1, m.YearNumber);
        }

        // ---------- caption while pins are crossed ----------
        TimeLapseScreenModel Synthetic(params StoryPin[] pins)
        {
            var lapse = new TimeLapse(sim);
            var m = new TimeLapseScreenModel(lapse, new ReplayClock(100f), null, "Reef", null, pins);
            m.Layout.Build(W, 100f, new[] { Band(Era.Camp, 0, 100, true) }, pins);
            return m;
        }

        [Test]
        public void CrossingAPinPopsItsCaptionThenLetsGo()
        {
            var m = Synthetic(Pin(1, 10f), Pin(2, 50f));
            m.Clock.Seek(0f); m.SetSpeed(2); m.Play();                // 4 yr/s
            m.Tick(1f); Assert.IsNull(m.Caption);                      // year 4
            m.Tick(2f);                                                // year 12: crossed pin 1
            Assert.AreEqual(1, m.Caption.EntryId);
            Assert.AreEqual(CaptionSource.Crossed, m.Caption.source);
            m.Tick(1f); Assert.IsNotNull(m.Caption);                   // still inside its 3 s
            m.Tick(3f); Assert.IsNull(m.Caption);
        }

        [Test]
        public void SeveralPinsInOneFrameShowTheStrongest()
        {
            var m = Synthetic(Pin(1, 10f, 0.55f), Pin(2, 11f, 0.9f), Pin(3, 12f, 0.7f));
            m.Clock.Seek(0f); m.SetSpeed(4); m.Play();                // 16 yr/s
            m.Tick(1f);
            Assert.AreEqual(2, m.Caption.EntryId);
        }

        [Test]
        public void ATappedCaptionStaysWhilePausedAndFadesOncePlayMovesOn()
        {
            var m = Synthetic(Pin(1, 10f));
            m.TapPin(m.Layout.Groups[0], 0);
            m.Tick(30f);
            Assert.IsNotNull(m.Caption, "paused: the card stays");
            m.Play();
            m.Tick(0.1f); Assert.IsNotNull(m.Caption);
            m.Tick(3.5f); Assert.IsNull(m.Caption);
        }

        [Test]
        public void ScrubbingBackwardsDoesNotPopCaptions()
        {
            var m = Synthetic(Pin(1, 10f), Pin(2, 50f));
            m.Clock.Seek(80f);
            m.PointerDown(m.Layout.XOf(80f)); m.PointerMove(m.Layout.XOf(5f)); m.PointerUp(m.Layout.XOf(5f));
            m.Tick(1f);
            Assert.IsNull(m.Caption);
        }

        [Test]
        public void CrossingAnEraStartShowsTheEraCardForAboutASecond()
        {
            var lapse = new TimeLapse(sim);
            var bands = lapse.EraBands();
            if (bands.Count < 2) Assert.Ignore("the test run reached one era only");
            var m = Real();
            float start = bands[1].startYear;
            m.Clock.Seek(Math.Max(0f, start - 0.5f)); m.SetSpeed(2); m.Play();
            m.Tick(0.25f);                                              // +1 year
            Assert.IsNotNull(m.EraCard);
            Assert.AreEqual(bands[1].era, m.EraCard.era);
            m.Tick(1.1f);
            Assert.IsNull(m.EraCard);
        }

        // ---------- snapshot ----------
        [Test]
        public void SnapshotCarriesNameYearAndEra()
        {
            var log = NewLog();
            var m = Real(log, name: "  Reef  ");
            m.Clock.Seek(0f);
            var s = m.TakeSnapshot();
            Assert.AreEqual("Reef", s.boxName);
            Assert.AreEqual(1, s.year);
            Assert.AreEqual("Camp", s.eraName);
            Assert.AreEqual("Reef · Year 1 · Camp", s.label);
            Assert.AreEqual("aquarium-year-1-camp.png", s.fileName);
            Assert.AreEqual(1, Events(log).Count(e => e.name == Ev.Snapshot));
        }

        [Test]
        public void SnapshotWithoutAName()
        {
            var m = Real(name: "");
            m.Clock.Seek(0f);
            Assert.AreEqual("Year 1 · Camp", m.TakeSnapshot().label);
        }

        // ---------- telemetry ----------
        [Test]
        public void OneScrubEventPerDragAndOnePlayPerStart()
        {
            var log = NewLog();
            var m = Real(log);
            m.Layout.Build(W, m.Clock.End, new EraBand[] { Band(Era.Camp, 0, m.Clock.End, true) }, new StoryPin[0]);
            float a = m.Layout.XOf(30f);
            m.PointerDown(a);
            for (int i = 1; i <= 20; i++) m.PointerMove(a - i * 3f);
            m.PointerUp(a - 60f);
            m.Play(); m.Play();                                        // a second Play while playing is not a second event
            m.Pause(); m.Play();
            var ev = Events(log);
            Assert.AreEqual(1, ev.Count(e => e.name == Ev.TimelapseScrub));
            Assert.AreEqual(2, ev.Count(e => e.name == Ev.TimelapsePlay));
            var scrub = ev.First(e => e.name == Ev.TimelapseScrub);
            Assert.AreEqual(m.Layout.YearAt(a - 60f), scrub.Num("to_year"), 0.01);
        }

        [Test]
        public void ATapOnTheSpotLogsNoScrub()
        {
            var log = NewLog();
            var m = Real(log);
            m.PointerDown(m.PlayheadX); m.PointerUp(m.PlayheadX);
            Assert.AreEqual(0, Events(log).Count(e => e.name == Ev.TimelapseScrub));
        }

        [Test]
        public void NullTelemetryIsFine()
        {
            var m = Real(null);
            m.Play(); m.Pause(); m.GoNow(); m.TakeSnapshot();
            m.TapPin(m.Layout.Groups[0], 0);
        }

        [Test]
        public void VersionBumpsOnlyOnChange()
        {
            var m = Real();
            int v = m.Version;
            m.Tick(0.5f);                                              // paused, no card: nothing to redraw
            Assert.AreEqual(v, m.Version);
            m.Clock.Seek(0f); m.Play(); m.Tick(0.5f);
            Assert.Greater(m.Version, v);
        }
    }
}
