using System;
using System.Collections.Generic;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>Who says a line in the box, by where it comes from (#327, docs/design/people-talk.md §1).</summary>
    public enum LineKind { Ambient, Want, Tree, Echo, Death, Ask, Relic, Crumb, Wish, FirstRunWish }

    /// <summary>Where a spoken line shows: the top caption (with the name, the ring and the speech mark) or a bubble over the head.</summary>
    public enum LineChannel { Caption, Bubble }

    /// <summary>
    /// #327 Option B (the user, 2026-11-08): every spoken line, at any zoom, goes to the one caption at the top with the speaker's
    /// name (or nothing), the ring under the speaker and a speech mark over their head. The over-head bubble is kept only for
    /// the first run's wish, which points at a person to tap. A line from a scene (a relic, the crumb, a wish's thanks, Show me)
    /// that finds the word busy waits its turn for a while instead of falling to a second system or vanishing; the people's
    /// ambient talk doesn't wait (there is always more of it). Pure, so SimTests run it too.
    /// </summary>
    public static class LineRoute
    {
        /// <summary>How long a scene's line waits for the word before it is let go (real seconds) [est].</summary>
        public const float WaitSeconds = 10f;
        /// <summary>The most lines waiting at once: a relic moment says several, and the oldest goes first.</summary>
        public const int MaxWaiting = 4;

        public static LineChannel ChannelFor(LineKind kind) => kind == LineKind.FirstRunWish ? LineChannel.Bubble : LineChannel.Caption;

        /// <summary>Does a line of this kind wait for the word when it's busy? The scene's lines do; the ambient talk is just skipped.</summary>
        public static bool Waits(LineKind kind) => kind switch
        {
            LineKind.Ambient or LineKind.Want or LineKind.Tree => false,
            LineKind.FirstRunWish => false, // asked every frame by its own view
            _ => true,
        };

        /// <summary>The line under the caption's words: the speaker's first name (a notable's, else the crowd person's), or null
        /// when nobody can be named. Never a filler like "someone in the camp" (#522): no name, no line.</summary>
        public static string Who(string notableName, string crowdName = null)
        {
            if (!string.IsNullOrWhiteSpace(notableName)) return notableName;
            return string.IsNullOrWhiteSpace(crowdName) ? null : crowdName;
        }
    }

    /// <summary>
    /// #327: lines waiting for the word, oldest first. Each waits up to its own deadline; past it, it is let go (counted).
    /// </summary>
    public sealed class LineQueue<T>
    {
        readonly List<(T line, double until)> waiting = new();
        readonly int max;

        public LineQueue(int max = LineRoute.MaxWaiting) { this.max = Math.Max(1, max); }

        public int Count => waiting.Count;
        /// <summary>Lines let go unsaid: waited too long or pushed out by newer ones (tests, the debug panel).</summary>
        public int Dropped { get; private set; }

        public void Add(T line, double now, float waitSeconds = LineRoute.WaitSeconds)
        {
            waiting.Add((line, now + waitSeconds));
            while (waiting.Count > max) { waiting.RemoveAt(0); Dropped++; }
        }

        /// <summary>The oldest line still in time when <paramref name="free"/> (the word can be had now), else nothing. Lines past
        /// their time go first, either way.</summary>
        public bool TryTake(double now, bool free, out T line)
        {
            for (int i = waiting.Count - 1; i >= 0; i--)
                if (now > waiting[i].until) { waiting.RemoveAt(i); Dropped++; }
            line = default;
            if (!free || waiting.Count == 0) return false;
            line = waiting[0].line;
            waiting.RemoveAt(0);
            return true;
        }

        public void Clear() => waiting.Clear();
    }
}
