using System;

namespace AquariumCiv.Audio
{
    /// <summary>One croak to play now (<see cref="FrogCroaker.Tick"/>): which clip, how loud, pitch, pan and the low-pass cutoff.</summary>
    public struct FrogCall
    {
        public int clip;        // 0 .. ClipCount-1
        public float volume;    // 0..1 of the frog layer's gain
        public float pitch;     // ~1
        public float pan;       // -1 .. 1
        public float cutoffHz;  // the source's low-pass: lower when the camera is far
    }

    /// <summary>
    /// #315: the pond's frogs as single croaks instead of a recorded chorus loop (playtest #306: "awful, especially when
    /// several are frogging at once, very loud"). One call at a time (the view says whether the last one still sounds), a long
    /// random gap, shorter at night and dusk (the ambience's frog level already carries the hour, the season and the storm),
    /// softer and duller the farther the camera is (zoom 0 = the whole box, 1 = closest), never the same clip twice in a row.
    /// Pure C#: its own seeded <see cref="Random"/>, nothing allocates per tick. The croaks are made in code (<see cref="Generate"/>),
    /// so there is no recording to license. Every number is a constant to tune.
    /// </summary>
    public sealed class FrogCroaker
    {
        public const int ClipCount = 4;
        public const int SampleRate = 22050;
        /// <summary>Seconds between two croaks, scaled by <see cref="GapScale"/>: <see cref="NightScale"/> at the busiest (night), <see cref="DayScale"/> by day.</summary>
        public const float GapMin = 16f, GapMax = 42f, NightScale = 0.6f, DayScale = 1.5f;
        /// <summary>The frog level (0..1, from <see cref="Ambience"/>) below which there are no calls (winter, no pond).</summary>
        public const float CallsFromLevel = 0.05f;
        /// <summary>A call's volume at the farthest zoom and at the closest, and its low-pass cutoff the same way (Hz): a phone speaker bites above 4 kHz.</summary>
        public const float FarVolume = 0.22f, NearVolume = 0.7f, FarCutoff = 1500f, NearCutoff = 4200f;
        /// <summary>The frog layer's gain against the layer's cue volume (the old loop played at 1.0).</summary>
        public const float Gain = 0.5f;

        readonly Random rng;
        float next;
        int last = -1;

        public FrogCroaker(int seed)
        {
            rng = new Random(seed);
            next = Range(GapMin, GapMax) * 0.5f;
        }

        public float SecondsToNext => next;

        /// <summary>0 at the whole box .. 1 at the closest, eased.</summary>
        public static float Nearness(float zoom01) { float t = Clamp01(zoom01); return t * t * (3f - 2f * t); }

        public static float VolumeAt(float zoom01) => FarVolume + (NearVolume - FarVolume) * Nearness(zoom01);
        public static float CutoffAt(float zoom01) => FarCutoff + (NearCutoff - FarCutoff) * Nearness(zoom01);

        /// <summary>The gap multiplier for a frog level: <see cref="NightScale"/> at 1, <see cref="DayScale"/> at the daytime 0.25.</summary>
        public static float GapScale(float level)
        {
            float t = Clamp01((level - 0.25f) / 0.75f);
            return DayScale + (NightScale - DayScale) * t;
        }

        /// <summary>Advance by <paramref name="dt"/> real seconds. <paramref name="busy"/>: the last croak still sounds (never two at once).</summary>
        public bool Tick(float dt, float frogLevel, float zoom01, bool busy, out FrogCall call)
        {
            call = default;
            next -= dt;
            if (next > 0f) return false;
            if (frogLevel < CallsFromLevel) { next = 5f; return false; }
            if (busy) { next = 0.5f; return false; }
            next = Range(GapMin, GapMax) * GapScale(frogLevel);
            call = Make(zoom01);
            return true;
        }

        /// <summary>A call now (the debug cheat), whatever the gap. The next one is still a long way off.</summary>
        public FrogCall Now(float zoom01)
        {
            next = Math.Max(next, Range(GapMin, GapMax) * 0.5f);
            return Make(zoom01);
        }

        FrogCall Make(float zoom01)
        {
            int c = last < 0 ? rng.Next(ClipCount) : rng.Next(ClipCount - 1);
            if (last >= 0 && c >= last) c++; // never the same clip twice in a row
            last = c;
            return new FrogCall
            {
                clip = c, volume = VolumeAt(zoom01) * Range(0.7f, 1f), pitch = Range(0.88f, 1.08f),
                pan = Range(-0.7f, 0.7f), cutoffHz = CutoffAt(zoom01) * Range(0.85f, 1.1f),
            };
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        // ------------------------------------------------------------------ the croaks

        /// <summary>Peak of <see cref="Generate"/>'s samples.</summary>
        public const float Peak = 0.8f;

        /// <summary>
        /// A soft croak, 22.05 kHz mono: a train of damped pulses (the throat's flutter) over a low carrier with one soft vowel
        /// resonance, then a gentle low-pass (nothing above about 2.5 kHz, so the phone speaker does not bite). 0 a short two-pulse
        /// "rib", 1 a three-pulse "ribbit" that rises, 2 one long soft "gronk" with a slow wobble, 3 a quick four-pulse trill.
        /// Deterministic by (clip, seed), silent at both ends.
        /// </summary>
        public static float[] Generate(int clip, int seed)
        {
            if (clip < 0 || clip >= ClipCount) throw new ArgumentOutOfRangeException(nameof(clip));
            var rng = new Random(unchecked(seed * 6007 + clip * 101 + 3));
            int pulses; float pulseGap, pulseLen, f0, rise, wobble;
            switch (clip)
            {
                case 0: pulses = 2; pulseGap = 0.11f; pulseLen = 0.09f; f0 = 210f; rise = 1.05f; wobble = 0f; break;
                case 1: pulses = 3; pulseGap = 0.1f; pulseLen = 0.08f; f0 = 190f; rise = 1.25f; wobble = 0f; break;
                case 2: pulses = 1; pulseGap = 0f; pulseLen = 0.32f; f0 = 150f; rise = 0.9f; wobble = 0.06f; break;
                default: pulses = 4; pulseGap = 0.07f; pulseLen = 0.06f; f0 = 260f; rise = 1.15f; wobble = 0f; break;
            }
            f0 *= 0.95f + 0.1f * (float)rng.NextDouble();
            float total = 0.04f + (pulses - 1) * pulseGap + pulseLen + 0.06f;
            var d = new float[(int)(total * SampleRate) + 1];
            for (int p = 0; p < pulses; p++)
            {
                int at = (int)((0.02f + p * pulseGap) * SampleRate);
                int m = (int)(pulseLen * SampleRate);
                float prog = pulses <= 1 ? 0f : p / (float)(pulses - 1);
                float f = f0 * (1f + (rise - 1f) * prog);
                double ph = 0;
                float amp = 0.8f + 0.2f * (1f - prog);
                float z1 = 0f, z2 = 0f; // the vowel resonance near 650 Hz
                float r = (float)Math.Exp(-Math.PI * 160.0 / SampleRate), th = (float)(2 * Math.PI * 650.0 / SampleRate);
                float a1 = -2f * r * (float)Math.Cos(th), a2 = r * r, g = 1f - r;
                for (int i = 0; i < m && at + i < d.Length; i++)
                {
                    float t = i / (float)SampleRate, u = i / (float)m;
                    float ff = f * (1f + wobble * (float)Math.Sin(2 * Math.PI * 9.0 * t));
                    ph += 2.0 * Math.PI * ff / SampleRate;
                    float src = (float)(Math.Sin(ph) + 0.4 * Math.Sin(2 * ph) + 0.15 * Math.Sin(3 * ph));
                    float y = g * src - a1 * z1 - a2 * z2; z2 = z1; z1 = y;
                    float env = (float)Math.Sin(Math.PI * Math.Pow(u, 0.6)) * (float)Math.Exp(-2.5 * u); // soft attack, no click
                    d[at + i] += (0.5f * src + 2.2f * y) * env * amp;
                }
            }
            LowPass(d, 2500f);
            float max = 0f;
            for (int i = 0; i < d.Length; i++) max = Math.Max(max, Math.Abs(d[i]));
            if (max > 0f) { float k = Peak / max; for (int i = 0; i < d.Length; i++) d[i] *= k; }
            return d;
        }

        /// <summary>Two cascaded one-pole low-passes, in place.</summary>
        static void LowPass(float[] d, float hz)
        {
            float a = 1f - (float)Math.Exp(-2.0 * Math.PI * hz / SampleRate);
            for (int pass = 0; pass < 2; pass++)
            {
                float y = 0f;
                for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
            }
        }
    }
}
