using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Replay;

namespace AquariumCiv.View.TimeLapseScreen
{
    public enum CaptionSource { Tap, Snap, Crossed }

    /// <summary>The story card under the timeline: what the pin says and what its buttons can do.</summary>
    public class PinCaption
    {
        public StoryPin pin;
        public CaptionSource source;
        public int groupIndex, groupCount;     // which story of a grouped marker (0 of 1 for a lone pin)
        public float secondsLeft = -1f;        // counting down (Crossed, or a Tap once play moves on); -1 = stays until dismissed
        public string Text => pin.text;
        public int EntryId => pin.entryId;
        public bool CanOpenPerson => pin.notableId >= 0;   // PersonCardView.Open(notableId)
        public bool CanFlyTo => pin.hasPos;                // pin.x, pin.z in box units
    }

    /// <summary>The era name card shown for a second when play crosses an era start.</summary>
    public class EraCard { public Era era; public string title; public float secondsLeft; }

    /// <summary>What the Snapshot button hands the capture: the label goes in the image only, never on the screen.</summary>
    public class SnapshotData
    {
        public string boxName, eraName, eraTitle, label, fileName;
        public int year;                 // as the plate reads it (position floor + 1)
        public float position;
    }

    /// <summary>
    /// #79 phase 1: everything the time-lapse screen decides, with no UI in it. Phase 2 draws it (UI Toolkit) and forwards
    /// pointer events and the frame's delta time; the rework restyles the drawing and keeps this. The screen reads
    /// <see cref="Position"/> each frame and shows the box there (TimeLapseView.Show). It must not also call
    /// TimeLapseView.Seek/Play, or the telemetry would be logged twice: this model logs it.
    /// </summary>
    public class TimeLapseScreenModel
    {
        public TimeLapse Lapse { get; }
        public ReplayClock Clock { get; }
        public TimelineLayout Layout { get; }
        public TimelineOptions Opt => Layout.Opt;
        public IReadOnlyList<StoryPin> Pins => pins;
        public string BoxName { get; set; }
        readonly TelemetryLog tel;
        readonly List<StoryPin> pins;

        public PinCaption Caption { get; private set; }
        public EraCard EraCard { get; private set; }
        /// <summary>Bumps on every change the UI has to redraw (a cheap dirty flag).</summary>
        public int Version { get; private set; }

        bool down, scrubbing; float downX; PinGroup downPin; float dragFrom;

        public TimeLapseScreenModel(TimeLapse lapse, ReplayClock clock, TelemetryLog tel = null, string boxName = "", TimelineOptions opt = null, IEnumerable<StoryPin> pinsOverride = null)
        {
            Lapse = lapse; Clock = clock; this.tel = tel; BoxName = boxName ?? "";
            Layout = new TimelineLayout(opt);
            pins = pinsOverride != null ? pinsOverride.ToList() : lapse.Pins(Layout.Opt.maxPins, Layout.Opt.minSalience);
        }

        /// <summary>Lay out for the control's width (px). Call when the screen opens and when its size changes.</summary>
        public void SetWidth(float width) { Layout.Build(width, Clock.End, Lapse.EraBands(), pins); Version++; }

        // ---------- readouts ----------
        public float Position => Clock.Position;
        public float PlayheadX => Layout.XOf(Clock.Position);
        public bool Playing => Clock.Playing;
        public bool Dragging => scrubbing;
        public Era Era => Lapse.EraAt(Clock.Position);
        public string EraName => Era.ToString();
        /// <summary>The year as the plate reads it (year 0 is "Year 1").</summary>
        public int YearNumber => (int)Math.Floor(Clock.Position) + 1;
        public string YearLabel => "Year " + YearNumber;
        public bool AtNow => Clock.Position >= Clock.End - 0.005f;
        /// <summary>The Now button is live whenever the playhead is not at the present.</summary>
        public bool NowEnabled => !AtNow;
        public string PlayLabel => Clock.Playing ? "Pause" : (Clock.AtEnd ? "Replay" : "Play");
        public int SpeedIndex => Clock.SpeedIndex;
        public int SpeedCount => ReplayClock.Speeds.Length;
        public string SpeedLabel => ReplayClock.Speeds[Clock.SpeedIndex].ToString("0.#", CultureInfo.InvariantCulture) + " yr/s";
        public bool HasHistory => Clock.End > 0f;

        // ---------- play, pause, speed ----------
        public void Play()
        {
            if (Clock.Playing) return;
            Clock.Play();                         // from the end it restarts at the beginning
            tel?.TimelapsePlayed();
            if (Caption != null && Caption.source != CaptionSource.Crossed) Caption.secondsLeft = Opt.captionSeconds; // a card lets go once play moves on
            Version++;
        }
        public void Pause() { if (!Clock.Playing) return; Clock.Pause(); Version++; }
        public void TogglePlay() { if (Clock.Playing) Pause(); else Play(); }
        public void SetSpeed(int index) { Clock.SetSpeed(index); Version++; }
        /// <summary>Tap on the speed chip: next step, wrapping from the fastest back to the slowest.</summary>
        public void CycleSpeed() => SetSpeed(Clock.SpeedIndex + 1 >= SpeedCount ? 0 : Clock.SpeedIndex + 1);

        // ---------- the Now button ----------
        /// <summary>Jump the playhead to the present (leaving the screen is Close). Pauses, like any seek.</summary>
        public void GoNow()
        {
            if (AtNow && !Clock.Playing) return;
            float from = Clock.Position;
            Clock.Seek(Clock.End);
            LogScrub(from, Clock.Position);
            Caption = null; EraCard = null; Version++;
        }

        // ---------- pointer: tap a pin, drag the playhead, tap the track ----------
        public void PointerDown(float x)
        {
            if (down) return;
            down = true; downX = x; scrubbing = false; dragFrom = Clock.Position;
            downPin = Layout.HitPin(x);
            if (downPin == null) BeginScrub(x);
        }

        public void PointerMove(float x)
        {
            if (!down) return;
            if (!scrubbing)
            {
                if (Math.Abs(x - downX) <= Opt.tapSlop) return;
                downPin = null; BeginScrub(downX);
            }
            Clock.Seek(Layout.YearAt(x)); Version++;
        }

        /// <summary>Finger up. A press on a marker that did not move is a tap on it; a drag (or a tap on the bare track) settles
        /// on a marker, Now or an era start if one is within reach.</summary>
        public void PointerUp(float x)
        {
            if (!down) return;
            down = false;
            if (!scrubbing && downPin != null) { var g = downPin; downPin = null; TapPin(g, 0); return; }
            if (!scrubbing) return;
            scrubbing = false;
            float? snap = Layout.SnapTarget(x, out var group);
            Clock.Seek(snap ?? Layout.YearAt(x));
            LogScrub(dragFrom, Clock.Position);
            if (group != null) ShowCaption(group, 0, CaptionSource.Snap);
            Version++;
        }

        /// <summary>The system took the touch away: end the drag where it is, no snap.</summary>
        public void PointerCancel()
        {
            if (!down) return;
            down = false; downPin = null;
            if (scrubbing) { scrubbing = false; LogScrub(dragFrom, Clock.Position); }
            Version++;
        }

        void BeginScrub(float x)
        {
            scrubbing = true; dragFrom = Clock.Position;
            Caption = null; EraCard = null;
            Clock.Seek(Layout.YearAt(x)); Version++;
        }

        // ---------- pins ----------
        /// <summary>Tap a marker: the playhead goes to its story, play pauses, the caption shows. A grouped marker opens on its
        /// first story; <see cref="NextInGroup"/> steps through the rest.</summary>
        public void TapPin(PinGroup g, int index)
        {
            if (g == null) return;
            index = Math.Max(0, Math.Min(g.pins.Count - 1, index));
            var p = g.pins[index];
            float from = Clock.Position;
            Clock.Seek(p.year);
            tel?.PinTapped(p.notableId);
            LogScrub(from, Clock.Position);
            ShowCaption(g, index, CaptionSource.Tap);
        }

        public void TapPin(int entryId) { var g = Layout.GroupOf(entryId); if (g != null) TapPin(g, g.pins.FindIndex(p => p.entryId == entryId)); }

        public bool HasMoreInGroup => Caption != null && Caption.groupCount > 1;

        /// <summary>The caption's next story in the grouped marker (wraps), seeking there. Previous with step -1.</summary>
        public void NextInGroup(int step = 1)
        {
            if (!HasMoreInGroup) return;
            var g = Layout.GroupOf(Caption.pin.entryId);
            if (g == null) return;
            int n = g.pins.Count;
            TapPin(g, (((Caption.groupIndex + step) % n) + n) % n);
        }

        public void DismissCaption() { if (Caption != null) { Caption = null; Version++; } }

        void ShowCaption(PinGroup g, int index, CaptionSource src)
        {
            Caption = new PinCaption { pin = g.pins[index], source = src, groupIndex = index, groupCount = g.pins.Count, secondsLeft = -1f };
            Version++;
        }

        // ---------- time passes ----------
        /// <summary>Feed it the real (unscaled) frame time. Moves the playhead while playing, pops a caption for the strongest
        /// pin crossed, an era card at an era start, and lets old captions go. True when the playhead moved.</summary>
        public bool Tick(float dt)
        {
            bool changed = false;
            if (Caption != null && Caption.secondsLeft >= 0f && (Caption.secondsLeft -= dt) <= 0f) { Caption = null; changed = true; }
            if (EraCard != null && (EraCard.secondsLeft -= dt) <= 0f) { EraCard = null; changed = true; }
            if (!Clock.Playing) { if (changed) Version++; return false; }

            float before = Clock.Position;
            Clock.Tick(dt);
            float after = Clock.Position;
            if (after < before) before = Clock.Start - 0.0001f;     // looped round: judge the new lap from its start
            if (after != before)
            {
                var crossed = TimeLapse.PinsCrossed(pins, before, after);
                if (crossed.Count > 0)
                {
                    var p = crossed.OrderByDescending(c => c.salience).First();
                    var g = Layout.GroupOf(p.entryId);
                    Caption = new PinCaption { pin = p, source = CaptionSource.Crossed, groupIndex = g != null ? g.pins.IndexOf(p) : 0, groupCount = g != null ? g.pins.Count : 1, secondsLeft = Opt.captionSeconds };
                }
                foreach (var b in Lapse.EraBands().Skip(1))
                    if (b.startYear > before && b.startYear <= after) EraCard = new EraCard { era = b.era, title = b.title, secondsLeft = Opt.eraCardSeconds };
                changed = true;
            }
            if (changed) Version++;
            return after != before;
        }

        // ---------- snapshot ----------
        /// <summary>What to write on the image. The screen takes the picture (EraStills) with this label and no UI in it, then
        /// offers the share sheet with <see cref="SnapshotData.fileName"/>.</summary>
        public SnapshotData TakeSnapshot()
        {
            var era = Era;
            var band = Lapse.EraBands().LastOrDefault(b => Clock.Position >= b.startYear);
            string name = string.IsNullOrWhiteSpace(BoxName) ? "" : BoxName.Trim();
            var d = new SnapshotData { boxName = name, year = YearNumber, eraName = era.ToString(), eraTitle = band != null ? band.title : "", position = Clock.Position };
            d.label = (name.Length > 0 ? name + " · " : "") + "Year " + d.year + " · " + d.eraName;
            d.fileName = "aquarium-year-" + d.year + "-" + d.eraName.ToLowerInvariant() + ".png";
            tel?.SnapshotTaken();
            return d;
        }

        void LogScrub(float from, float to)
        {
            if (Math.Abs(to - from) > 0.01f) tel?.TimelapseScrubbed(from, to);
        }
    }
}
