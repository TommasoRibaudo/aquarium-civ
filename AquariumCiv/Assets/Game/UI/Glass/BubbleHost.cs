using System;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// One speech bubble on screen, across every panel (spec §3, §5: the captures showed two overlapping). Each view keeps its own
    /// <see cref="GlassBubble"/>, because the play tests find them by name in that view's document, and asks this host for the word
    /// before it shows one. A higher rank takes the word at once and the holder is told to go; a lower or equal rank waits, so the
    /// second bubble never appears over the first. Pure, so EditMode tests cover it.
    /// </summary>
    public sealed class BubbleHost
    {
        /// <summary>From the quietest up: the people's chatter, a line said outside a moment (a relic, the first run, the founders), a moment's own lines.</summary>
        public enum Rank { World, Line, Moment }

        public sealed class Voice
        {
            public readonly Rank rank;
            internal readonly Action yielded;
            internal Voice(Rank rank, Action yielded) { this.rank = rank; this.yielded = yielded; }
        }

        public Voice Holder { get; private set; }

        /// <summary>A speaker. <paramref name="yielded"/> runs when a higher rank takes the word from it: it hides its bubble.</summary>
        public Voice Join(Rank rank, Action yielded = null) => new(rank, yielded);

        /// <summary>Would a voice of this rank get the word now?</summary>
        public bool Free(Rank rank) => Holder == null || Holder.rank < rank;

        /// <summary>Asks for the word. False means someone of the same rank or higher is talking: wait and ask again.</summary>
        public bool Ask(Voice voice)
        {
            if (Holder == voice) return true;
            if (!Free(voice.rank)) return false;
            var old = Holder;
            Holder = voice;
            old?.yielded?.Invoke();
            return true;
        }

        /// <summary>This voice has finished. A voice that doesn't hold the word changes nothing.</summary>
        public void Done(Voice voice)
        {
            if (Holder == voice) Holder = null;
        }

        /// <summary>A new scene: whoever held the word is gone.</summary>
        public void Clear() => Holder = null;
    }
}
