using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #202: the box's wind, pure: a slow seeded value that holds a spell (still, a gentle breeze, a brisker one) and eases into the next. The view
    /// turns the mill's sails by it. No sim state: it is weather for the eye, the same box at the same second blows the same.
    /// </summary>
    public static class WindDrift
    {
        /// <summary>How long a spell lasts (real seconds), and the share of it spent easing into the next.</summary>
        public const float SpellSeconds = 16f, EaseShare = 0.4f;
        /// <summary>The share of spells that are still, and that are gentle; the rest are brisk.</summary>
        public const float StillShare = 0.3f, GentleShare = 0.45f;
        /// <summary>A gentle breeze and a brisk one, on the 0 to 1 of <see cref="At"/>.</summary>
        public const float Gentle = 0.32f, Brisk = 1f;
        /// <summary>How fast sails turn in the briskest wind (degrees a second): a round in about four seconds.</summary>
        public const float MostDegrees = 90f;

        /// <summary>The wind (0 still, 1 brisk) at <paramref name="seconds"/> for a box's seed.</summary>
        public static float At(int seed, double seconds)
        {
            double s = seconds / SpellSeconds;
            long k = (long)Math.Floor(s);
            float f = (float)(s - k);
            float a = Level(seed, k), b = Level(seed, k + 1);
            float u = Math.Max(0f, Math.Min(1f, (f - (1f - EaseShare)) / EaseShare));
            u = u * u * (3f - 2f * u);
            return a + (b - a) * u;
        }

        /// <summary>Degrees a second for a wind.</summary>
        public static float Degrees(float wind) => wind * MostDegrees;

        static float Level(int seed, long spell)
        {
            float h = Hash01(seed, spell, 1), v = Hash01(seed, spell, 2);
            if (h < StillShare) return 0f;
            if (h < StillShare + GentleShare) return Gentle * (0.8f + 0.4f * v);
            return Brisk * (0.7f + 0.3f * v);
        }

        static float Hash01(int seed, long spell, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)spell * 374761393u ^ (uint)(spell >> 32) * 1274126177u ^ (uint)salt * 668265263u;
                h = (h ^ (h >> 15)) * 2246822519u;
                h = (h ^ (h >> 13)) * 3266489917u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
