using System;
using System.Collections.Generic;

namespace AquariumCiv.View.Moments
{
    /// <summary>
    /// The rebuild wave (era-moment.md Choice 3, §4 0:20, §5 0:35): lots rebuild in place one after another, from the
    /// centre outward. Each gets a time inside the wave beat and a note (each knock a note higher: a rising scale).
    /// Lots never move; the view swaps each lot's look with a puff when its time comes.
    /// </summary>
    public class RebuildWave
    {
        public readonly struct Step
        {
            public readonly int lotId;    // whatever id the view uses for a lot (a sim Building.id)
            public readonly float time;   // moment time of the rebuild
            public readonly int order;    // 0 = nearest the centre
            public readonly int semitone; // the knock's pitch above the base note
            public Step(int lotId, float time, int order, int semitone) { this.lotId = lotId; this.time = time; this.order = order; this.semitone = semitone; }
        }

        /// <summary>A major pentatonic scale, so any run of knocks sounds like a tune.</summary>
        static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };
        /// <summary>The scale climbs at most this many notes, then stays on the top note [est].</summary>
        public const int MaxNotes = 12;
        /// <summary>Of the beat, the share the wave uses; the rest lets the last puff settle [est].</summary>
        public const float Spread = 0.85f;

        readonly List<Step> steps = new();
        public IReadOnlyList<Step> Steps => steps;

        /// <summary>Orders <paramref name="lots"/> by flat distance from <paramref name="centre"/> (ties by id) and
        /// spreads them over [<paramref name="start"/>, <paramref name="start"/> + <paramref name="duration"/> × Spread].</summary>
        public RebuildWave(IEnumerable<(int lotId, MVec3 pos)> lots, MVec3 centre, float start, float duration)
        {
            var list = new List<(int id, float d)>();
            foreach (var (id, pos) in lots) list.Add((id, pos.FlatDistance(centre)));
            list.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.id.CompareTo(b.id));
            float span = Math.Max(0f, duration) * Spread;
            for (int i = 0; i < list.Count; i++)
            {
                float u = list.Count == 1 ? 0f : i / (float)(list.Count - 1);
                steps.Add(new Step(list[i].id, start + span * u, i, Semitone(i)));
            }
        }

        public static int Semitone(int order)
        {
            int n = Math.Min(Math.Max(0, order), MaxNotes - 1);
            return Pentatonic[n % Pentatonic.Length] + 12 * (n / Pentatonic.Length);
        }

        /// <summary>The steps whose time falls in (<paramref name="from"/>, <paramref name="to"/>]; pass the moment time
        /// of the last frame and this one. A first call with from &lt; start catches the step at the start.</summary>
        public IEnumerable<Step> Due(float from, float to)
        {
            foreach (var s in steps) if (s.time > from && s.time <= to) yield return s;
        }

        /// <summary>How many lots have rebuilt by moment time <paramref name="t"/>.</summary>
        public int DoneBy(float t)
        {
            int n = 0;
            foreach (var s in steps) if (s.time <= t) n++;
            return n;
        }
    }
}
