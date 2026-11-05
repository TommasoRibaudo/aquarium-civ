using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// P8 #281 (keepers-hands 2.1, 5.4): grass that is new since the player last looked shows a lighter, fresher green; nothing
    /// is listed. Pure helpers over <see cref="EcoState.seen"/>, a bit per site cell: set = green when the player last looked.
    /// "Green" is any cover that is neither bare ground nor water. No dice, no rng; the mask is only ever written by
    /// <see cref="Mark"/> and the debug hook, never by the model.
    /// </summary>
    public static class EcoSeen
    {
        /// <summary>Real seconds, from the return, over which fresh green fades to the normal palette.</summary>
        public const float FadeSeconds = 100f;

        public static bool IsGreen(EcoState e, int cell)
        {
            if (e == null || e.cover == null || cell < 0 || cell >= e.cover.Length) return false;
            var c = (EcoCover)e.cover[cell];
            return c != EcoCover.Bare && c != EcoCover.Water;
        }

        public static bool HasMask(EcoState e) => e != null && e.on && e.seen != null && e.seen.Length >= (EcoLand.N + 7) / 8;

        public static bool IsSeen(EcoState e, int cell) => HasMask(e) && cell >= 0 && cell < EcoLand.N && (e.seen[cell >> 3] & (1 << (cell & 7))) != 0;

        static void Set(EcoState e, int cell, bool v)
        {
            if (v) e.seen[cell >> 3] |= (byte)(1 << (cell & 7));
            else e.seen[cell >> 3] &= (byte)~(1 << (cell & 7));
        }

        /// <summary>The player has looked: every green cell now counts as seen. Creates the mask if there is none.</summary>
        public static void Mark(EcoState e)
        {
            if (e == null || !e.on || e.cover == null) return;
            if (!HasMask(e)) e.seen = new byte[(EcoLand.N + 7) / 8];
            else Array.Clear(e.seen, 0, e.seen.Length);
            for (int c = 0; c < EcoLand.N; c++) if (IsGreen(e, c)) Set(e, c, true);
        }

        /// <summary>1 at the return down to 0 after <see cref="FadeSeconds"/> (smooth).</summary>
        public static float Fade(float secondsSinceReturn)
        {
            float t = Math.Clamp(secondsSinceReturn / FadeSeconds, 0f, 1f);
            float k = 1f - t;
            return k * k * (3f - 2f * k);
        }

        /// <summary>Is this cell green now but not in the seen mask (ignoring the fade)? False with no mask.</summary>
        public static bool IsNew(EcoState e, int cell) => HasMask(e) && IsGreen(e, cell) && !IsSeen(e, cell);

        /// <summary>Freshness 0..1 of one cell.</summary>
        public static float Freshness(EcoState e, int cell, float secondsSinceReturn) => IsNew(e, cell) ? Fade(secondsSinceReturn) : 0f;

        /// <summary>Fills <paramref name="into"/> (length at least N) with every cell's freshness; returns how many cells are above 0.</summary>
        public static int Fill(EcoState e, float secondsSinceReturn, float[] into)
        {
            int n = 0;
            float f = Fade(secondsSinceReturn);
            for (int c = 0; c < EcoLand.N && c < into.Length; c++)
            {
                float v = f > 0f && IsNew(e, c) ? f : 0f;
                into[c] = v;
                if (v > 0f) n++;
            }
            return n;
        }

        /// <summary>How many cells are new (green, unseen).</summary>
        public static int NewCount(EcoState e)
        {
            int n = 0;
            if (!HasMask(e)) return 0;
            for (int c = 0; c < EcoLand.N; c++) if (IsNew(e, c)) n++;
            return n;
        }

        /// <summary>Debug: act as if the player left before up to <paramref name="count"/> of the green cells grew: they become
        /// unseen. Picks by a hash of the cell so the same call always picks the same cells. Makes the mask first if none.
        /// Returns how many cells are new afterwards.</summary>
        public static int PretendAway(EcoState e, int count)
        {
            if (e == null || !e.on || e.cover == null) return 0;
            if (!HasMask(e)) Mark(e);
            var order = new List<int>();
            for (int c = 0; c < EcoLand.N; c++) if (IsGreen(e, c) && IsSeen(e, c)) order.Add(c);
            order.Sort((a, b) => Hash(e.seed, a).CompareTo(Hash(e.seed, b)));
            for (int i = 0; i < count && i < order.Count; i++) Set(e, order[i], false);
            return NewCount(e);
        }

        static uint Hash(int seed, int cell)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)(cell + 1) * 40503u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return h;
            }
        }
    }
}
