using System;

namespace AquariumCiv.Audio
{
    /// <summary>One bird call to play now (<see cref="BirdSong.Tick"/>): which clip, how loud, its pitch and its pan.</summary>
    public struct BirdCall
    {
        public int clip;        // 0 .. clipCount-1
        public float volume;    // 0..1 of the layer's cue volume
        public float pitch;     // ~1
        public float pan;       // -1 (left) .. 1 (right)
    }

    /// <summary>
    /// #195: keeps the daytime birds fresh over a long session. The single <c>birds_day_loop</c> repeats audibly after a few
    /// minutes, so the loop becomes a quiet bed that swells and thins on a slow, irregular schedule (<see cref="Swell"/>:
    /// bouts of song and gaps of near quiet, none the same length), and separate calls (a pool of short clips) come at random
    /// times, never the same clip twice in a row, with a little pitch, volume and pan jitter. Pure C#: its own seeded
    /// <see cref="System.Random"/> (never the sim's dice), nothing allocates per tick. The view multiplies the loop's level
    /// by <see cref="Swell"/> and plays what <see cref="Tick"/> returns, scaled by the same birds level (no calls at night).
    /// </summary>
    public sealed class BirdSong
    {
        /// <summary>The loop's lowest level in a gap (quiet, not silent) and the lengths of a bout and a gap, in seconds.</summary>
        public const float GapLevel = 0.25f, BoutMin = 25f, BoutMax = 70f, GapMin = 12f, GapMax = 40f, EaseSeconds = 4f;
        /// <summary>Seconds between two calls, and the birds level below which no call is made (dawn fading, night).</summary>
        public const float CallMin = 6f, CallMax = 28f, CallsFromLevel = 0.15f;

        readonly Random rng;
        readonly int clips;
        float swell = 1f;
        bool inBout = true;
        float phaseLeft;
        float nextCall;
        int lastClip = -1;

        public BirdSong(int seed, int clipCount)
        {
            rng = new Random(seed);
            clips = Math.Max(0, clipCount);
            phaseLeft = Range(BoutMin, BoutMax);
            nextCall = Range(CallMin, CallMax);
        }

        /// <summary>The loop's multiplier now, <see cref="GapLevel"/>..1.</summary>
        public float Swell => swell;
        public bool InBout => inBout;

        /// <summary>Advance by <paramref name="dt"/> real seconds. <paramref name="birdsLevel"/>: the day's birds level (0..1).
        /// True and <paramref name="call"/> filled when a call should play now.</summary>
        public bool Tick(float dt, float birdsLevel, out BirdCall call)
        {
            call = default;
            phaseLeft -= dt;
            if (phaseLeft <= 0f)
            {
                inBout = !inBout;
                phaseLeft = inBout ? Range(BoutMin, BoutMax) : Range(GapMin, GapMax);
            }
            float target = inBout ? 1f : GapLevel;
            float step = dt * (1f - GapLevel) / EaseSeconds;
            swell = swell < target ? Math.Min(target, swell + step) : Math.Max(target, swell - step);

            nextCall -= dt;
            if (nextCall > 0f) return false;
            if (birdsLevel < CallsFromLevel || clips == 0) { nextCall = Range(CallMin, CallMax) * 0.5f; return false; } // look again soon, no call now
            // A gap is the quiet between songs; a call may still land in it (that is what makes a gap feel like a forest), but rarer.
            nextCall = Range(CallMin, CallMax) * (inBout ? 1f : 1.6f);
            int c = lastClip < 0 || clips == 1 ? rng.Next(clips) : rng.Next(clips - 1);
            if (lastClip >= 0 && clips > 1 && c >= lastClip) c++; // never the same clip twice in a row
            lastClip = c;
            call = new BirdCall
            {
                clip = c, volume = Range(0.45f, 1f), pitch = Range(0.92f, 1.1f), pan = Range(-0.8f, 0.8f),
            };
            return true;
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    }
}
