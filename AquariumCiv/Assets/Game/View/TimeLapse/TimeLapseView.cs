using System.Globalization;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Replay;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The world replay (#78): the box at any year of the history records. Open it and the live world steps aside (the sim
    /// clock holds, the people and the care view are switched off), the box redraws from <see cref="TimeLapse"/>'s frames
    /// and scrubs by position (fractional years): the settled year is drawn through <see cref="SimLayoutSource.Replay"/>
    /// and <see cref="BoxView.Refresh"/> rebuilds only the chunks that differ; lots that appear grow in, cleared ones
    /// shrink away, a look change dips as it changes. <see cref="Close"/> puts the live box back.
    /// The time-lapse screen (#79) drives this: <see cref="Open"/>, <see cref="Clock"/>, <see cref="Seek"/>,
    /// <see cref="Lapse"/> (era bands, story pins), <see cref="Close"/>. Debug hooks: <see cref="DebugOpen"/> and friends.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxView))]
    [AddComponentMenu("AquariumCiv/Time-lapse View")]
    public class TimeLapseView : MonoBehaviour
    {
        /// <summary>Animation steps between two years (a still piece grows in this many sizes).</summary>
        public int growSteps = 6;
        [Tooltip("Scrubbing draws without the AO bake; the chunks bake once the playhead has been still this long, or on pause or close.")]
        public float settleSeconds = 0.25f;
        float lastShowTime;

        BoxView box;
        SimLayoutSource src;
        PeopleView people;
        CareView care;
        bool hadPeople, hadCare, heldBefore;
        float shownPos = -1f;
        int shownKey = -1;

        public TimeLapse Lapse { get; private set; }
        public ReplayClock Clock { get; private set; }
        public bool Active { get; private set; }
        /// <summary>The playhead now, in fractional years (0 = the first year; the plate reads year + 1).</summary>
        public float Position => Clock != null ? Clock.Position : 0f;
        public string LastInfo { get; private set; } = "";
        /// <summary>Last scrub step: ms for the refresh, and chunks rebuilt (tests and the debug panel log them).</summary>
        public double LastStepMs { get; private set; }
        public int LastChunks { get; private set; }
        public int Steps { get; private set; }
        public double TotalStepMs { get; private set; }

        BoxView Box => box != null ? box : box = GetComponent<BoxView>();
        SimLayoutSource Src => src != null ? src : src = GetComponent<SimLayoutSource>();

        public static TimeLapseView Find() => FindAnyObjectByType<TimeLapseView>();

        /// <summary>The scene's replay view, put on the box's object if it has none (the #79 screen calls this).</summary>
        public static TimeLapseView Ensure()
        {
            var v = Find();
            if (v != null) return v;
            var s = SimLayoutSource.Find();
            return s != null ? s.gameObject.AddComponent<TimeLapseView>() : null;
        }

        /// <summary>Opens the replay at the present (or <paramref name="atYear"/>). False if there is nothing to replay or
        /// an era moment is playing.</summary>
        public bool Open(float atYear = -1f)
        {
            if (Active) return true;
            if (Src == null || Box == null) return false;
            var sim = Src.Sim;
            if (sim == null || sim.Settlement == null) return false;
            if (Application.isPlaying && MomentPlayer.Instance != null && MomentPlayer.Instance.Playing) return false;
            Lapse = new TimeLapse(sim);
            Clock = new ReplayClock(Lapse.EndPosition);
            var clock = GameClock.Instance;
            if (clock != null) { heldBefore = clock.Held; clock.Held = true; }
            people = GetComponent<PeopleView>();
            hadPeople = people != null && people.enabled;
            if (hadPeople) people.enabled = false;
            care = GetComponent<CareView>();
            hadCare = care != null && care.enabled;
            if (hadCare) care.enabled = false;
            Active = true;
            GameClock.Tel.TimelapseOpened(false);
            scrubFrom = float.NaN;
            if (Application.isPlaying && AquariumCiv.GameUI.GameHud.Instance != null) AquariumCiv.GameUI.GameHud.Instance.HideTools("timelapse", true); // no Clean / Shape on the past (tester batch 2)
            Steps = 0; TotalStepMs = 0;
            Clock.Seek(atYear < 0f ? Lapse.EndPosition : atYear);
            Show(Clock.Position, true);
            return true;
        }

        /// <summary>Back to the live box: the sim clock runs again, people and care return, the layout is the live one.</summary>
        public void Close() => Close(true);

        float scrubFrom = float.NaN, scrubTo;

        /// <summary>A scrub is a drag: one event per drag (start year to where it stopped), logged when the drag ends.</summary>
        void FlushScrub()
        {
            if (!float.IsNaN(scrubFrom)) GameClock.Tel.TimelapseScrubbed(scrubFrom, scrubTo);
            scrubFrom = float.NaN;
        }

        void Close(bool refresh)
        {
            if (!Active) return;
            FlushScrub();
            Active = false;
            Src.Replay = null;
            Box.deferAO = false;
            Box.FlushAo();
            shownKey = -1;
            var clock = GameClock.Instance;
            if (clock != null) clock.Held = heldBefore;
            if (hadPeople && people != null) people.enabled = true;
            if (hadCare && care != null) care.enabled = true;
            if (AquariumCiv.GameUI.GameHud.Instance != null) AquariumCiv.GameUI.GameHud.Instance.HideTools("timelapse", false);
            if (refresh) Box.Refresh();
            Lapse = null; Clock = null;
        }

        /// <summary>Jump to a position (fractional years); pauses playback (a scrubber drag).</summary>
        public void Seek(float position)
        {
            if (!Active) return;
            if (float.IsNaN(scrubFrom)) scrubFrom = Clock.Position;
            scrubTo = position;
            Clock.Seek(position);
            Show(Clock.Position, false);
        }

        public void Play() { if (Active) { FlushScrub(); GameClock.Tel.TimelapsePlayed(); Clock.Play(); } }
        public void Pause() { if (Active) { FlushScrub(); Clock.Pause(); Settle(); } }

        /// <summary>Bakes the AO of the chunks the scrub rebuilt without it (the box looks as it does live).</summary>
        public void Settle()
        {
            if (!Active || Box.AoPending == 0) return;
            Box.deferAO = false;
            Box.FlushAo();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Box.Refresh();
            LastInfo += string.Format(CultureInfo.InvariantCulture, " · AO baked in {0:0.0} ms", sw.Elapsed.TotalMilliseconds);
        }
        public void SetSpeed(int index) { if (Active) Clock.SetSpeed(index); }

        void Update()
        {
            if (!Active) return;
            if (Application.isPlaying && Clock.Playing) { Clock.Tick(Time.unscaledDeltaTime); Show(Clock.Position, false); }
            else if (Box.AoPending > 0 && Time.unscaledTime - lastShowTime >= settleSeconds) Settle();
        }

        void OnDisable() { if (Active) Close(false); }

        /// <summary>Draws the box at a position now (refreshing only what changed). Skips when nothing visible differs.</summary>
        public void Show(float position, bool force)
        {
            lastShowTime = Time.unscaledTime;
            Box.deferAO = true;
            var s = Lapse.Scrub(position);
            var near = s.Nearest;
            var other = near == s.a ? s.b : s.a;
            float q = Mathf.Round(s.blend * growSteps) / growSteps;
            int key = s.yearA * 1000 + Mathf.RoundToInt(q * growSteps);
            if (!force && key == shownKey) { LastStepMs = 0; LastChunks = 0; return; }
            shownKey = key;
            shownPos = position;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int cells = Src.Sim.Settlement.Map.tier.Length;
            Src.Replay = new ReplayFrame(near, cells, s.diff.IsEmpty ? null : other, s.blend, growSteps);
            Box.Refresh();
            LastStepMs = sw.Elapsed.TotalMilliseconds;
            LastChunks = Box.lastChunks.Count;
            Steps++; TotalStepMs += LastStepMs;
            LastInfo = string.Format(CultureInfo.InvariantCulture, "year {0:0.00} ({1}) · {2} buildings · {3:0.0} ms, {4} chunk(s)",
                position + 1f, near.era, near.buildings.Count, LastStepMs, LastChunks);
        }

        // ---------------- debug (SimDebugPanel, `script eval`) ----------------
        public static string DebugOpen()
        {
            var v = Ensure(); if (v == null) return "no box in the scene";
            return v.Open() ? "time-lapse open · " + v.LastInfo : "cannot open (no sim, or an era moment is playing)";
        }
        public static string DebugClose() { var v = Find(); if (v == null) return "no TimeLapseView"; v.Close(); return "live box restored"; }
        public static string DebugSeek(float year) { var v = Find(); if (v == null || !v.Active) return "not open"; v.Seek(year); return v.LastInfo; }
    }
}
