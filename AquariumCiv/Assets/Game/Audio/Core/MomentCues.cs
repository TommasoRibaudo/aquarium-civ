using System;
using System.Collections.Generic;
using AquariumCiv.View.Moments;

namespace AquariumCiv.Audio
{
    /// <summary>
    /// An era moment's sound as a timeline (#96, era-moment.md §4, §5, §10): each beat's <c>audio</c> id becomes one or
    /// more cues at moment times, and the rebuild wave adds a knock per lot, each a note higher. Built once at Watch
    /// (only the beats that play, as <see cref="MomentContext.Plays"/> says); <see cref="Due"/> hands the view what
    /// starts between two frames. Moment time, so hurry plays it faster; a frame that jumps far (a hitch) drops the
    /// stale one-shots instead of firing a pile at once.
    /// </summary>
    public class MomentCues
    {
        public readonly struct Entry
        {
            public readonly float time;
            public readonly string cue;
            public readonly float semitones;
            public readonly string beat;
            /// <summary>A light haptic tick goes with it (a beat with <c>haptic</c>, one per lot in the wave).</summary>
            public readonly bool tick;
            public Entry(float time, string cue, float semitones, string beat, bool tick)
            { this.time = time; this.cue = cue; this.semitones = semitones; this.beat = beat; this.tick = tick; }
            public override string ToString() => $"{time:0.00}s {cue}{(semitones != 0f ? $" +{semitones}st" : "")} ({beat})";
        }

        /// <summary>A cue whose start is more than this far behind the frame is skipped (seconds of moment time).</summary>
        public const float StaleAfter = 0.75f;

        /// <summary>
        /// What each MomentBeat.audio id plays: (cue, delay in seconds from the beat start). A "_scale" id plays nothing
        /// at the beat start: its notes come from the wave (<see cref="WaveCue"/>).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (string cue, float delay)[]> AudioIds =
            new Dictionary<string, (string, float)[]>
            {
                ["flute"] = new[] { (CueLibrary.Flute, 0f) },
                ["drum"] = new[] { (CueLibrary.Drum, 0f), (CueLibrary.Drum, 0.45f) },
                ["tune_up"] = new[] { (CueLibrary.TuneUp, 0f) },
                ["knock_scale"] = Array.Empty<(string, float)>(),
                ["hammer_scale"] = Array.Empty<(string, float)>(),
                ["chord"] = new[] { (CueLibrary.Chord, 0f), (CueLibrary.Cheer, 0.15f) },
                ["chime"] = new[] { (CueLibrary.Chime, 0f) },
                ["bell_fiddle"] = new[] { (CueLibrary.Bell, 0f), (CueLibrary.Fiddle, 1.6f) },
                ["cheer_small"] = new[] { (CueLibrary.CheerSmall, 0.3f) },
                ["cobble_taps"] = new[] { (CueLibrary.CobbleTap, 0f), (CueLibrary.CobbleTap, 0.18f), (CueLibrary.CobbleTap, 0.34f), (CueLibrary.CobbleTap, 0.52f) },
                ["pause_note"] = new[] { (CueLibrary.PauseNote, 0f) },
                ["fiddle"] = new[] { (CueLibrary.Fiddle, 0f) },
                ["bell"] = new[] { (CueLibrary.Bell, 0f) },
            };

        /// <summary>The per-lot cue of a wave beat: knocks for the village (wood), hammers for the town.</summary>
        public static string WaveCue(MomentBeat b) => b != null && b.audio == "hammer_scale" ? CueLibrary.Hammer : CueLibrary.Knock;

        readonly List<Entry> entries = new();
        public IReadOnlyList<Entry> Entries => entries;
        /// <summary>Audio ids in the moment no mapping knows (shown in the debug status so a typo is caught).</summary>
        public readonly List<string> Unknown = new();

        /// <param name="wave">The rebuild wave's steps (the view builds it at the wave beat); null leaves the knocks
        /// out, and <see cref="AddWave"/> can add them later.</param>
        public MomentCues(MomentDef def, MomentContext ctx, RebuildWave wave = null)
        {
            if (def == null) return;
            foreach (var b in def.beats)
            {
                if (ctx != null && !ctx.Plays(b)) continue;
                if (string.IsNullOrEmpty(b.audio)) continue;
                if (!AudioIds.TryGetValue(b.audio, out var cues)) { if (!Unknown.Contains(b.audio)) Unknown.Add(b.audio); continue; }
                foreach (var (cue, delay) in cues) entries.Add(new Entry(b.start + delay, cue, 0f, b.id, false));
                if (b.Has(BeatAction.RebuildWave) && wave != null) AddWave(wave, b);
            }
            Sort();
        }

        /// <summary>Adds the wave's notes: one per lot at its time, pitched by its semitone, each with a tick.</summary>
        public void AddWave(RebuildWave wave, MomentBeat beat)
        {
            if (wave == null) return;
            string cue = WaveCue(beat);
            foreach (var s in wave.Steps) entries.Add(new Entry(s.time, cue, s.semitone, beat?.id ?? "wave", beat == null || beat.haptic));
            Sort();
        }

        void Sort() => entries.Sort((a, b) => a.time.CompareTo(b.time));

        /// <summary>The cues that start in (<paramref name="from"/>, <paramref name="to"/>] of moment time (pass the last
        /// frame's time and this one's; a first call with from &lt; 0 catches cues at 0). Cues more than
        /// <see cref="StaleAfter"/> behind <paramref name="to"/> are skipped, except the moment's chord (never lost).</summary>
        public IEnumerable<Entry> Due(float from, float to)
        {
            foreach (var e in entries)
            {
                if (e.time <= from || e.time > to) continue;
                if (to - e.time > StaleAfter && e.cue != CueLibrary.Chord) continue;
                yield return e;
            }
        }
    }
}
