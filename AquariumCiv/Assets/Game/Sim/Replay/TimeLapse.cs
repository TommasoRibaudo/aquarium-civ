using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim.Replay
{
    // #78 phase 1: the time-lapse as pure data. No UnityEngine here, so it is tested with dotnet. Phase 2 (the Editor) maps a
    // frame's buildings and paths onto a BoxLayout (SimLayout's lot-kind dressing) and animates the diffs.

    /// <summary>What changed about a lot's building between two years: it stands in both, but looks different (a hut that
    /// became a cottage, a house that grew a storey).</summary>
    public struct BuildingChange { public BuildingAt from, to; }

    /// <summary>What changed between two years (any two, not only neighbours), so a scrub redraws only the difference.</summary>
    public class ReplayDiff
    {
        public int fromYear, toYear;
        public List<BuildingAt> appeared = new();      // in the later year only (pop in / grow)
        public List<BuildingAt> disappeared = new();   // in the earlier year only (a cleared lot)
        public List<BuildingChange> relooked = new();  // in both, new look/size/spot
        public List<PathCellAt> pathsAdded = new();    // cell is new in the later year
        public List<PathCellAt> pathsRemoved = new();  // cell has gone (the old values)
        public List<PathCellAt> pathsChanged = new();  // cell is in both with a new tier or laid flag (the new values)
        public List<ShapeRec> shapingsAdded = new();
        public List<ShapeRec> shapingsRemoved = new();
        public bool eraChanged;
        public int populationFrom, populationTo;

        public bool IsEmpty => appeared.Count == 0 && disappeared.Count == 0 && relooked.Count == 0 && pathsAdded.Count == 0
            && pathsRemoved.Count == 0 && pathsChanged.Count == 0 && shapingsAdded.Count == 0 && shapingsRemoved.Count == 0 && !eraChanged;
        public int Size => appeared.Count + disappeared.Count + relooked.Count + pathsAdded.Count + pathsRemoved.Count
            + pathsChanged.Count + shapingsAdded.Count + shapingsRemoved.Count;
    }

    /// <summary>One era on the timeline.</summary>
    public class EraBand
    {
        public Era era;
        public string title;           // "" while the era goes on
        public long startMonth, endMonth;
        public float startYear, endYear;   // fractional years; the open era ends at the sim's present
        public bool open;
    }

    /// <summary>A story on the timeline: a chronicle entry worth a pin.</summary>
    public class StoryPin
    {
        public int entryId;
        public long month;
        public float year;             // fractional year, where it sits on the timeline
        public string kind, text;
        public float salience;
        public bool hasPos;
        public float x, z;             // box units, valid with hasPos (fly-to)
        public int notableId, buildingId;
    }

    /// <summary>Where a scrub stands: the two years it sits between, how far along (0..1), and their diff.</summary>
    public class ScrubState
    {
        public float position;
        public int yearA, yearB;
        public float blend;            // 0 = fully yearA, 1 = fully yearB
        public BoxAtYear a, b;
        public ReplayDiff diff;        // a to b (empty when yearA == yearB)
        /// <summary>The year to draw as the settled box: the nearer one.</summary>
        public BoxAtYear Nearest => blend < 0.5f ? a : b;
    }

    /// <summary>
    /// The replay model over a sim's history records. Frames (the box at a year) are built once and cached; diffs between
    /// years are cached too, so dragging a scrubber back and forth costs a dictionary hit. Frames of finished years never
    /// change; the open year is rebuilt when the sim moves. Read only: nothing here touches the sim's state.
    /// </summary>
    public class TimeLapse
    {
        readonly SimCore sim;
        readonly Dictionary<int, BoxAtYear> frames = new();
        readonly Dictionary<long, ReplayDiff> diffs = new();
        readonly int maxFrames, maxDiffs;
        long cachedMonth = -1;
        List<EraBand> bands;

        /// <param name="maxFrames">Frame cache size (a whole 120-year run fits by default).</param>
        public TimeLapse(SimCore sim, int maxFrames = 256, int maxDiffs = 512)
        {
            this.sim = sim;
            this.maxFrames = maxFrames;
            this.maxDiffs = maxDiffs;
        }

        public SimCore Sim => sim;
        public int FramesCached => frames.Count;
        public int DiffsCached => diffs.Count;

        /// <summary>The latest year the box can be shown at (0-based; the plate reads year + 1).</summary>
        public int MaxYear => (int)(sim.State.month / SimConfig.MonthsPerYear);
        /// <summary>The scrubber's end in fractional years: the sim's own month, so the open year scrubs smoothly to now.</summary>
        public float EndPosition => sim.State.month / (float)SimConfig.MonthsPerYear;

        void Sync()
        {
            long m = sim.State.month;
            if (m == cachedMonth) return;
            if (cachedMonth >= 0 && m < cachedMonth) { frames.Clear(); diffs.Clear(); } // loaded an older save
            else if (cachedMonth >= 0)
            {
                // Finished years are stable. Only the newest year's frame can still change (its snapshot lands as it ends).
                int open = (int)(cachedMonth / SimConfig.MonthsPerYear);
                frames.Remove(open);
                foreach (var k in diffs.Keys.Where(k => (int)(k >> 32) == open || (int)(k & 0xffffffffL) == open).ToList()) diffs.Remove(k);
            }
            cachedMonth = m;
            bands = null;
        }

        /// <summary>Forget everything (a new game, or the records were rewritten).</summary>
        public void Invalidate() { frames.Clear(); diffs.Clear(); cachedMonth = -1; bands = null; }

        /// <summary>The box at the start of a year (cached). Null for a year that has not happened.</summary>
        public BoxAtYear Frame(int year)
        {
            Sync();
            if (year < 0 || year > MaxYear) return null;
            if (frames.TryGetValue(year, out var f)) return f;
            f = sim.BoxAt(year);
            if (f == null) return null;
            if (frames.Count >= maxFrames) frames.Remove(frames.Keys.OrderBy(k => Math.Abs(k - year)).Last()); // drop the furthest
            frames[year] = f;
            return f;
        }

        /// <summary>Builds missing frames, nearest to <paramref name="aroundYear"/> first, until the time budget runs out.
        /// Call it a little each frame so a first drag never stalls. True when every year is cached.</summary>
        public bool WarmUp(int aroundYear, double budgetMs)
        {
            Sync();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int max = MaxYear;
            for (int d = 0; d <= max; d++)
                for (int side = 0; side < (d == 0 ? 1 : 2); side++)
                {
                    int y = side == 0 ? aroundYear - d : aroundYear + d;
                    if (y < 0 || y > max || frames.ContainsKey(y)) continue;
                    if (sw.Elapsed.TotalMilliseconds >= budgetMs) return false;
                    Frame(y);
                }
            return true;
        }

        // ---------- diffs ----------
        static long Key(int a, int b) => ((long)a << 32) | (uint)b;

        /// <summary>What changes going from year <paramref name="a"/> to year <paramref name="b"/> (either order). Cached.</summary>
        public ReplayDiff Diff(int a, int b)
        {
            Sync();
            var fa = Frame(a); var fb = Frame(b);
            if (fa == null || fb == null) return null;
            long k = Key(a, b);
            if (diffs.TryGetValue(k, out var d)) return d;
            d = Compute(fa, fb);
            if (diffs.Count >= maxDiffs) diffs.Clear();
            diffs[k] = d;
            return d;
        }

        static bool SameLook(BuildingAt p, BuildingAt q) =>
            p.look == q.look && p.lotId == q.lotId && p.type == q.type && p.x == q.x && p.z == q.z && p.yaw == q.yaw && p.w == q.w && p.d == q.d;

        /// <summary>The diff of two frames. Paths come sorted by cell, so they merge in one pass.</summary>
        public static ReplayDiff Compute(BoxAtYear a, BoxAtYear b)
        {
            var d = new ReplayDiff { fromYear = a.year, toYear = b.year, eraChanged = a.era != b.era, populationFrom = a.population, populationTo = b.population };
            var before = new Dictionary<int, BuildingAt>(a.buildings.Count);
            foreach (var x in a.buildings) before[x.id] = x;
            var seen = new HashSet<int>();
            foreach (var y in b.buildings)
            {
                seen.Add(y.id);
                if (!before.TryGetValue(y.id, out var x)) d.appeared.Add(y);
                else if (!SameLook(x, y)) d.relooked.Add(new BuildingChange { from = x, to = y });
            }
            foreach (var x in a.buildings) if (!seen.Contains(x.id)) d.disappeared.Add(x);

            int i = 0, j = 0;
            var pa = a.paths; var pb = b.paths;
            while (i < pa.Count || j < pb.Count)
            {
                if (j >= pb.Count || (i < pa.Count && pa[i].cell < pb[j].cell)) d.pathsRemoved.Add(pa[i++]);
                else if (i >= pa.Count || pb[j].cell < pa[i].cell) d.pathsAdded.Add(pb[j++]);
                else
                {
                    if (pa[i].tier != pb[j].tier || pa[i].laid != pb[j].laid) d.pathsChanged.Add(pb[j]);
                    i++; j++;
                }
            }

            string SKey(ShapeRec s) => s.nudgeId + ":" + s.kind + ":" + s.month;
            var sa = new HashSet<string>(a.shapings.Select(SKey));
            var sb = new HashSet<string>(b.shapings.Select(SKey));
            foreach (var s in b.shapings) if (!sa.Contains(SKey(s))) d.shapingsAdded.Add(s);
            foreach (var s in a.shapings) if (!sb.Contains(SKey(s))) d.shapingsRemoved.Add(s);
            return d;
        }

        // ---------- scrubbing ----------
        /// <summary>Where the scrubber stands at a fractional year (0 .. <see cref="EndPosition"/>): the year before and the year
        /// after it, the blend between them and their diff. Phase 2 draws <see cref="ScrubState.a"/> and animates
        /// <see cref="ScrubState.diff"/> by <see cref="ScrubState.blend"/>.</summary>
        public ScrubState Scrub(float position)
        {
            Sync();
            int max = MaxYear;
            position = Math.Max(0f, Math.Min(position, EndPosition));
            int a = Math.Min((int)Math.Floor(position), max);
            int b = Math.Min(a + 1, max);
            var s = new ScrubState { position = position, yearA = a, yearB = b, a = Frame(a), b = Frame(b) };
            s.blend = a == b ? 0f : position - a;
            s.diff = a == b ? new ReplayDiff { fromYear = a, toYear = a } : Diff(a, b);
            return s;
        }

        // ---------- the timeline ----------
        /// <summary>The era bands, in order. The open era runs to the sim's present.</summary>
        public IReadOnlyList<EraBand> EraBands()
        {
            Sync();
            if (bands != null) return bands;
            var list = new List<EraBand>();
            long now = sim.State.month;
            for (int e = 0; e <= (int)Era.Town; e++)
            {
                long start = sim.EraStartMonth((Era)e);
                if (start < 0 || start > now) break;
                long next = e < (int)Era.Town ? sim.EraStartMonth((Era)(e + 1)) : -1;
                bool open = next < 0 || next > now;
                long end = open ? now : next;
                list.Add(new EraBand
                {
                    era = (Era)e, title = sim.EraTitle((Era)e), startMonth = start, endMonth = end, open = open,
                    startYear = start / (float)SimConfig.MonthsPerYear, endYear = end / (float)SimConfig.MonthsPerYear,
                });
            }
            return bands = list;
        }

        /// <summary>The era the timeline is in at a fractional year.</summary>
        public Era EraAt(float position)
        {
            var era = Era.Camp;
            foreach (var band in EraBands()) if (position >= band.startYear) era = band.era;
            return era;
        }

        /// <summary>Story pins: the chronicle entries worth a mark on the timeline, the strongest <paramref name="max"/> of them,
        /// in time order. Each has a spot if the sim stored or can work one out (for fly-to). Term lines are left out.</summary>
        public List<StoryPin> Pins(int max = 24, float minSalience = 0.5f)
        {
            Sync();
            var cand = sim.State.chronicle
                .Where(e => e.salience >= minSalience && e.id < SimCore.TermLineIdBase && e.kind != "era-ready")
                .OrderByDescending(e => e.salience).ThenBy(e => e.month).ThenBy(e => e.id)
                .Take(Math.Max(0, max))
                .OrderBy(e => e.month).ThenBy(e => e.id);
            var res = new List<StoryPin>();
            foreach (var e in cand)
            {
                var p = new StoryPin
                {
                    entryId = e.id, month = e.month, year = e.month / (float)SimConfig.MonthsPerYear, kind = e.kind, text = e.text,
                    salience = e.salience, notableId = e.notableId, buildingId = e.buildingId,
                };
                p.hasPos = sim.EntryPosition(e, out p.x, out p.z);
                res.Add(p);
            }
            return res;
        }

        /// <summary>The pins the playhead passes going from <paramref name="from"/> to <paramref name="to"/> (fractional years,
        /// either direction): the moments to pop a caption. In the order passed.</summary>
        public static List<StoryPin> PinsCrossed(IReadOnlyList<StoryPin> pins, float from, float to)
        {
            var res = new List<StoryPin>();
            float lo = Math.Min(from, to), hi = Math.Max(from, to);
            foreach (var p in pins) if (p.year > lo && p.year <= hi) res.Add(p);
            if (to < from) res.Reverse();
            return res;
        }
    }

    /// <summary>
    /// The playback clock: a playhead in fractional years, a play speed in years per real second, play/pause, seeking and
    /// an end that stops or loops. Pure: feed it real delta time. Dragging the scrubber (<see cref="Seek"/>) pauses it.
    /// </summary>
    public class ReplayClock
    {
        /// <summary>Speed steps, years per real second. 120 years at 4 a second is half a minute.</summary>
        public static readonly float[] Speeds = { 1f, 2f, 4f, 8f, 16f };
        public const int DefaultSpeedIndex = 2;

        public float Position { get; private set; }
        public float Start { get; set; }
        public float End { get; set; }
        public bool Playing { get; private set; }
        public bool Loop { get; set; }
        public int SpeedIndex { get; private set; } = DefaultSpeedIndex;
        public float Speed => Speeds[SpeedIndex];
        public bool AtEnd => Position >= End;

        public ReplayClock(float end = 0f) { End = end; }

        public void Play() { if (AtEnd && !Loop) Position = Start; Playing = true; }
        public void Pause() => Playing = false;
        public void Toggle() { if (Playing) Pause(); else Play(); }
        public void SetSpeed(int index) => SpeedIndex = Math.Max(0, Math.Min(Speeds.Length - 1, index));
        public void FasterStep() => SetSpeed(SpeedIndex + 1);
        public void SlowerStep() => SetSpeed(SpeedIndex - 1);

        /// <summary>Jump the playhead (a scrubber drag). Pauses unless told not to; clamped to the timeline.</summary>
        public void Seek(float position, bool pause = true)
        {
            Position = Math.Max(Start, Math.Min(End, position));
            if (pause) Playing = false;
        }

        /// <summary>Advance by real seconds. Returns the playhead movement in years (0 when paused). At the end it stops, or
        /// wraps to the start when looping.</summary>
        public float Tick(float dt)
        {
            if (!Playing || dt <= 0f) return 0f;
            float before = Position;
            Position += dt * Speed;
            if (Position >= End)
            {
                if (Loop && End > Start) Position = Start + (Position - Start) % (End - Start);
                else { Position = End; Playing = false; }
            }
            return Position - before;
        }
    }
}
