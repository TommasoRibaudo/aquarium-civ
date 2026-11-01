using System;
using System.Collections.Generic;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>
    /// The babble (#144 research 12 §5), synthesized as samples: 4 voices x 6 clips, 22.05 kHz mono. Pure and deterministic
    /// by (voice, clip, seed); the view wraps the floats in an AudioClip the way <c>CareSound.MakeProc</c> does. A syllable is
    /// 60 to 110 ms (a soft triangle plus a quieter second harmonic through two vowel-ish resonances, a quick pitch glide, a
    /// fast attack and exponential decay); a word is 2 to 5 syllables, a clip 1 to 3 words. Contour follows the mark.
    /// </summary>
    public static class ChatterBabble
    {
        public const int SampleRate = 22050, Voices = 4, Clips = 6;
        /// <summary>Peak of the output (headroom: the cue sits at 0.2 to 0.3 volume on the World bus).</summary>
        public const float Peak = 0.7f;
        static readonly float[] VoicePitch = { 150f, 210f, 290f, 400f }; // low adult, adult, young, kid
        static readonly float[][] Vowels =
        {
            new[] { 700f, 1100f }, new[] { 400f, 2000f }, new[] { 500f, 900f }, new[] { 300f, 800f }, new[] { 550f, 1600f },
        };

        /// <summary>The clip for a mark: 0 and 5 murmur (variety for …), 1 ♪ held notes, 2 ♥ soft falling pair, 3 ! short and sharp, 4 ? rising end.</summary>
        public static int ClipFor(ChatterMark m, float roll)
        {
            switch (m)
            {
                case ChatterMark.Note: return 1;
                case ChatterMark.Heart: return 2;
                case ChatterMark.Bang: return 3;
                case ChatterMark.Question: return 4;
                default: return roll < 0.5f ? 0 : 5;
            }
        }

        /// <summary>Kids high, adults low (adults pick voice 0 or 1 by id).</summary>
        public static int VoiceFor(float ageYears, int personId, float adultAge = 16f)
        {
            if (ageYears < adultAge * 0.6f) return 3;
            if (ageYears < adultAge) return 2;
            return Math.Abs(personId) % 2;
        }

        /// <summary>The samples, at most +-<see cref="Peak"/>, silent at both ends (no click). Throws for a voice or clip out of range.</summary>
        public static float[] Generate(int voice, int clip, int seed)
        {
            if (voice < 0 || voice >= Voices) throw new ArgumentOutOfRangeException(nameof(voice));
            if (clip < 0 || clip >= Clips) throw new ArgumentOutOfRangeException(nameof(clip));
            var rng = new Random(unchecked(seed * 7919 + voice * 131 + clip * 17 + 1));
            float basePitch = VoicePitch[voice] * (0.97f + 0.06f * (float)rng.NextDouble());
            int words = clip == 3 ? 1 : 1 + rng.Next(3);
            var syl = new List<(float ms, int vowel, bool wordEnd)>();
            for (int w = 0; w < words; w++)
            {
                int count = clip == 3 ? 1 + rng.Next(2) : clip == 1 ? 2 + rng.Next(2) : 2 + rng.Next(4);
                for (int i = 0; i < count; i++)
                {
                    float ms = clip == 1 ? 130f + 40f * (float)rng.NextDouble() : clip == 3 ? 60f + 20f * (float)rng.NextDouble() : 60f + 50f * (float)rng.NextDouble();
                    syl.Add((ms, rng.Next(Vowels.Length), i == count - 1));
                }
            }
            const float WordGap = 0.07f, SylGap = 0.012f;
            double total = 0.02;
            foreach (var s in syl) total += s.ms / 1000.0 + (s.wordEnd ? WordGap : SylGap);
            var d = new float[(int)(total * SampleRate) + 1];
            int pos = (int)(0.01 * SampleRate);
            for (int i = 0; i < syl.Count; i++)
            {
                float len = syl[i].ms / 1000f;
                float prog = syl.Count <= 1 ? 1f : i / (float)(syl.Count - 1);
                float step, glide;
                switch (clip)
                {
                    case 0: step = 1f + 0.05f * (float)Math.Sin(i * 1.7); glide = 1.08f; break;
                    case 1: step = new[] { 1f, 1.26f, 1.5f, 1.26f }[i % 4]; glide = 1f; break; // held notes, a little tune
                    case 2: step = prog < 0.5f ? 1.2f : 1f; glide = 0.92f; break; // soft falling pair
                    case 3: step = 1.35f; glide = 0.85f; break; // short and sharp
                    case 4: step = 1f + 0.35f * prog * prog; glide = 1f + 0.2f * prog; break; // rising end
                    default: step = 1f + 0.08f * (float)Math.Cos(i * 2.3); glide = 0.95f; break;
                }
                Syllable(d, pos, len, basePitch * step, glide, Vowels[syl[i].vowel], clip == 1 ? 1.5f : 4f, clip == 3 ? 0.9f : 0.75f + 0.25f * (1f - 0.4f * prog));
                pos += (int)((len + (syl[i].wordEnd ? WordGap : SylGap)) * SampleRate);
            }
            float max = 0f;
            for (int i = 0; i < d.Length; i++) max = Math.Max(max, Math.Abs(d[i]));
            if (max > 0f) { float k = Peak / max; for (int i = 0; i < d.Length; i++) d[i] *= k; }
            return d;
        }

        static void Syllable(float[] d, int at, float len, float f0, float glide, float[] formants, float decay, float amp)
        {
            int m = (int)(len * SampleRate);
            double ph = 0;
            var r1 = new Reson(formants[0], 90f); var r2 = new Reson(formants[1], 130f);
            for (int i = 0; i < m && at + i < d.Length; i++)
            {
                float t = i / (float)SampleRate, u = i / (float)m;
                float f = f0 * (1f + (glide - 1f) * (1f - (1f - u) * (1f - u))); // a quick glide toward the target
                ph += 2.0 * Math.PI * f / SampleRate;
                double tri = 2.0 / Math.PI * Math.Asin(Math.Sin(ph));
                float src = (float)(tri + 0.35 * Math.Sin(2 * ph));
                float y = 0.55f * src + 0.9f * r1.Run(src) + 0.6f * r2.Run(src);
                float env = Math.Min(1f, t / 0.006f) * (float)Math.Exp(-t * decay / len * 1.5f) * Math.Min(1f, (len - t) / 0.008f);
                d[at + i] += y * env * amp;
            }
        }

        /// <summary>A 2-pole band-pass.</summary>
        sealed class Reson
        {
            readonly float a1, a2, g; float z1, z2;
            public Reson(float f, float bw)
            {
                float r = (float)Math.Exp(-Math.PI * bw / SampleRate), th = (float)(2 * Math.PI * f / SampleRate);
                a1 = -2f * r * (float)Math.Cos(th); a2 = r * r; g = 1f - r;
            }
            public float Run(float x)
            {
                float y = g * x - a1 * z1 - a2 * z2;
                z2 = z1; z1 = y;
                return y;
            }
        }
    }
}
