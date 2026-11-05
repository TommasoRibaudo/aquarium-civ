using System;

namespace AquariumCiv.Audio
{
    /// <summary>One gust of sand-hiss to play now (<see cref="SandGusts.Tick"/>).</summary>
    public struct SandGust
    {
        public int clip;         // 0 .. ClipCount-1
        public float startFrac;  // 0..1: where in the room the clip leaves for the gust (the player maps it to a start offset)
        public float duration;   // seconds, DurMin..DurMax
        public float gain;       // 0..1 of the layer's level
        public float pitch;      // ~1
        public float pan;        // -1 .. 1
        public float fadeIn, fadeOut; // seconds
    }

    /// <summary>
    /// #331: the sand's hiss as occasional gusts instead of one 40 s loop that came round all through the early camp
    /// (playtest #322: "weird repetitive noise"). A gust is a 2-6 s cut of a longer recording at a random offset, with its own pitch,
    /// pan and soft fades, then a random gap of GapMin..GapMax seconds (shorter the bigger the bare share). One at a time: the gap
    /// starts when a gust ends. The level comes from <see cref="Ambience"/> (<c>sandHiss</c>, the bare share, already gone well before
    /// the green takes over); below <see cref="GustsFromLevel"/> there are none. Pure C#, its own seeded <see cref="Random"/>.
    /// </summary>
    public sealed class SandGusts
    {
        public const int ClipCount = 4;
        public const float GapMin = 8f, GapMax = 25f, DurMin = 2f, DurMax = 6f;
        /// <summary>The hiss level (0..1, from <see cref="Ambience"/>) below which there are no gusts.</summary>
        public const float GustsFromLevel = 0.04f;
        /// <summary>The gap multiplier when the level is faint and when it is 0.5 or more: a barer box gusts more often.</summary>
        public const float SparseScale = 1.6f, BusyScale = 0.8f;

        readonly Random rng;
        float next;
        int last = -1;

        public SandGusts(int seed)
        {
            rng = new Random(seed);
            next = Range(GapMin, GapMax) * 0.4f;
        }

        public float SecondsToNext => next;

        /// <summary>The gap multiplier for a hiss level: <see cref="SparseScale"/> when faint, <see cref="BusyScale"/> from 0.5 up.</summary>
        public static float GapScale(float level)
        {
            float t = Clamp01((level - GustsFromLevel) / (0.5f - GustsFromLevel));
            return SparseScale + (BusyScale - SparseScale) * t;
        }

        /// <summary>Advance by <paramref name="dt"/> real seconds. The next gap counts from the end of a gust, so two never overlap.</summary>
        public bool Tick(float dt, float level, out SandGust gust)
        {
            gust = default;
            next -= dt;
            if (next > 0f) return false;
            if (level < GustsFromLevel) { next = 3f; return false; }
            gust = Make();
            next = gust.duration + Range(GapMin, GapMax) * GapScale(level);
            return true;
        }

        /// <summary>A gust now (the debug cheat), whatever the gap. The next one is still a gap away.</summary>
        public SandGust Now()
        {
            var g = Make();
            next = Math.Max(next, g.duration + GapMin);
            return g;
        }

        SandGust Make()
        {
            int c = last < 0 ? rng.Next(ClipCount) : rng.Next(ClipCount - 1);
            if (last >= 0 && c >= last) c++; // a different cut than the last gust
            last = c;
            float dur = Range(DurMin, DurMax);
            return new SandGust
            {
                clip = c, startFrac = Range(0f, 1f), duration = dur, gain = Range(0.65f, 1f),
                pitch = Range(0.92f, 1.08f), pan = Range(-0.6f, 0.6f),
                fadeIn = dur * Range(0.3f, 0.45f), fadeOut = dur * Range(0.35f, 0.5f),
            };
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
