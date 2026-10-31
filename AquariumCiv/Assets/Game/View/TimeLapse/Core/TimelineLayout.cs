using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Replay;

namespace AquariumCiv.View.TimeLapseScreen
{
    /// <summary>Sizes and thresholds of the timeline, in dp/px of the track. The UI rework may change the numbers, not the logic.</summary>
    public class TimelineOptions
    {
        public float padLeft = 16f, padRight = 16f;
        /// <summary>Pins closer than this (centre to centre) are grouped into one marker, so every marker is tappable.</summary>
        public float minPinGap = 36f;
        /// <summary>A finger within this of a marker taps it (half the usual 44 dp touch target).</summary>
        public float pinHitRadius = 22f;
        /// <summary>A press that moves more than this is a drag, not a tap.</summary>
        public float tapSlop = 8f;
        /// <summary>On release the playhead snaps to a marker / Now / an era start within this many px.</summary>
        public float pinSnap = 14f, nowSnap = 14f, eraSnap = 10f;
        /// <summary>An era band shows its name only if it is at least this wide.</summary>
        public float bandLabelMinWidth = 56f;
        public float captionSeconds = 3f, eraCardSeconds = 1f;
        public int maxPins = 24;
        public float minSalience = 0.5f;
    }

    public class BandRect
    {
        public Era era; public string title;
        public float x0, x1;          // px on the control
        public float startYear, endYear;
        public bool open, showLabel;
        public float Width => x1 - x0;
    }

    /// <summary>One marker on the timeline: a story pin, or several pins that are too close to tell apart at this width.</summary>
    public class PinGroup
    {
        public float x;                                  // where the marker is drawn (the earliest pin's x, so markers keep their gap)
        public List<StoryPin> pins = new List<StoryPin>();   // in time order
        public StoryPin Lead;                            // the strongest story: its kind draws the icon
        public bool IsGroup => pins.Count > 1;
        public float Year => pins[0].year;
    }

    /// <summary>Timeline position and x are two views of one thing. Pure: no UnityEngine. Width in px of the whole control.</summary>
    public class TimelineLayout
    {
        public TimelineOptions Opt { get; }
        public float Width { get; private set; }
        public float End { get; private set; }
        public float TrackLeft => Opt.padLeft;
        public float TrackRight => Width - Opt.padRight;
        public float TrackWidth => Math.Max(0f, TrackRight - TrackLeft);
        public IReadOnlyList<BandRect> Bands => bands;
        public IReadOnlyList<PinGroup> Groups => groups;
        /// <summary>Era starts after the first (the ticks between bands), as x.</summary>
        public IReadOnlyList<float> EraMarkers => markers;
        readonly List<BandRect> bands = new List<BandRect>();
        readonly List<PinGroup> groups = new List<PinGroup>();
        readonly List<float> markers = new List<float>();

        public TimelineLayout(TimelineOptions opt = null) { Opt = opt ?? new TimelineOptions(); }

        public float XOf(float year) => End <= 0f ? TrackLeft : TrackLeft + Clamp(year / End, 0f, 1f) * TrackWidth;
        public float YearAt(float x) => TrackWidth <= 0f ? 0f : Clamp((x - TrackLeft) / TrackWidth, 0f, 1f) * End;

        /// <summary>(Re)lay out for a control width and a timeline. Call on open and on a size change.</summary>
        public void Build(float width, float end, IReadOnlyList<EraBand> eraBands, IReadOnlyList<StoryPin> pins)
        {
            Width = width; End = Math.Max(0f, end);
            bands.Clear(); markers.Clear(); groups.Clear();
            int i = 0;
            foreach (var b in eraBands)
            {
                float endYear = b.open ? End : b.endYear;
                float x0 = XOf(b.startYear), x1 = XOf(endYear);
                bands.Add(new BandRect { era = b.era, title = b.title, startYear = b.startYear, endYear = endYear, x0 = x0, x1 = x1, open = b.open, showLabel = x1 - x0 >= Opt.bandLabelMinWidth });
                if (i++ > 0) markers.Add(x0);
            }
            PinGroup cur = null;
            foreach (var p in pins.OrderBy(p => p.year).ThenBy(p => p.entryId))
            {
                float x = XOf(p.year);
                if (cur == null || x - cur.x >= Opt.minPinGap) { cur = new PinGroup { x = x }; groups.Add(cur); }
                cur.pins.Add(p);
            }
            foreach (var g in groups) g.Lead = g.pins.OrderByDescending(p => p.salience).First();
        }

        /// <summary>The marker under a finger at x, if any (nearest within the hit radius).</summary>
        public PinGroup HitPin(float x) => Nearest(x, Opt.pinHitRadius);

        PinGroup Nearest(float x, float r)
        {
            PinGroup best = null; float bd = r;
            foreach (var g in groups) { float d = Math.Abs(g.x - x); if (d <= bd) { bd = d; best = g; } }
            return best;
        }

        /// <summary>The group a pin belongs to.</summary>
        public PinGroup GroupOf(int entryId) => groups.FirstOrDefault(g => g.pins.Any(p => p.entryId == entryId));

        /// <summary>Where a released drag settles: a marker within reach, else Now, else an era start. Null = stay where it is.</summary>
        public float? SnapTarget(float x, out PinGroup group)
        {
            group = Nearest(x, Opt.pinSnap);
            if (group != null) return group.Year;
            if (End > 0f && TrackRight - x <= Opt.nowSnap) return End;
            foreach (var m in markers) if (Math.Abs(m - x) <= Opt.eraSnap) return YearAt(m);
            return null;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
