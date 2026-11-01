using System;

namespace AquariumCiv.Lighting
{
    /// <summary>The picked lamps, reused every call so picking allocates nothing.</summary>
    public sealed class LampPick
    {
        public readonly int[] Indices = new int[Lamps.MaxLamps];
        public int Count;
        internal readonly float[] Dist = new float[Lamps.MaxLamps];
        internal readonly int[] Prev = new int[Lamps.MaxLamps];
    }

    /// <summary>
    /// Lamp rules: when each lamp comes on (staggered by a seed, so they flicker on one by one as the lamp level rises
    /// through the dusk), the flicker curve, and the choice of at most <see cref="MaxLamps"/> lamps nearest the camera focus.
    /// </summary>
    public static class Lamps
    {
        public const int MaxLamps = 8;
        /// <summary>How far above its threshold the lamp level must climb before a lamp stops stuttering and burns steadily.</summary>
        public const float IgniteWindow = 0.10f;
        /// <summary>A lamp already picked counts this much closer (squared 0.85) so the set does not swap while the camera drifts.</summary>
        const float KeepBias = 0.7225f;

        static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
            return x;
        }

        static float Unit(uint h) => (h & 0xFFFFFF) / 16777216f;

        /// <summary>The lamp level at which this lamp turns on: 0.05..0.95, fixed by seed and lamp id.</summary>
        public static float Threshold(int seed, int lampId) => 0.05f + 0.9f * Unit(Hash(unchecked((uint)seed * 2654435761U ^ (uint)(lampId + 1) * 40503U)));

        /// <summary>
        /// Brightness 0..1 of one lamp. Below its threshold: 0. In the ignite window just above it: a stutter (hashed on/off
        /// pulses that get longer). After that a faint steady shimmer. <paramref name="steady"/> (time-lapse) drops the
        /// stutter and the shimmer: the lamp just fades on.
        /// </summary>
        public static float Brightness(float level, int seed, int lampId, float time, bool steady = false)
        {
            float th = Threshold(seed, lampId);
            if (level <= th) return 0f;
            float over = level - th;
            if (steady) return Smooth01(over / IgniteWindow);
            float phase = Unit(Hash(unchecked((uint)lampId * 9781U + (uint)seed))) * 6.2831853f;
            float shimmer = 1f - 0.05f * (0.5f + 0.5f * (float)Math.Sin(time * 7.3f + phase)) - 0.03f * (0.5f + 0.5f * (float)Math.Sin(time * 17.1f + phase * 2f));
            if (over >= IgniteWindow) return shimmer;
            float u = over / IgniteWindow;
            float slot = 0.05f + 0.15f * u;                 // pulses get longer as it catches
            uint bucket = (uint)Math.Max(0f, time / slot);
            bool on = Unit(Hash(unchecked(bucket * 31U + (uint)lampId * 7U + (uint)seed))) < 0.25f + 0.75f * u;
            return on ? Smooth01(u * 2f + 0.3f) * shimmer : 0f;
        }

        static float Smooth01(float t) { t = t < 0f ? 0f : t > 1f ? 1f : t; return t * t * (3f - 2f * t); }

        /// <summary>
        /// Chooses up to <see cref="MaxLamps"/> lamps nearest the focus (<paramref name="fx"/>, <paramref name="fz"/>), nearest
        /// first, into <paramref name="pick"/> (no allocation). Lamps from the previous call count as 15 % closer.
        /// </summary>
        public static void PickNearest(float[] xs, float[] zs, int count, float fx, float fz, LampPick pick)
        {
            int prevCount = pick.Count;
            Array.Copy(pick.Indices, pick.Prev, prevCount);
            int m = 0;
            for (int i = 0; i < count; i++)
            {
                float dx = xs[i] - fx, dz = zs[i] - fz;
                float d = dx * dx + dz * dz;
                for (int p = 0; p < prevCount; p++) if (pick.Prev[p] == i) { d *= KeepBias; break; }
                if (m == MaxLamps && d >= pick.Dist[m - 1]) continue;
                int at = m < MaxLamps ? m++ : m - 1;
                while (at > 0 && pick.Dist[at - 1] > d)
                {
                    pick.Dist[at] = pick.Dist[at - 1];
                    pick.Indices[at] = pick.Indices[at - 1];
                    at--;
                }
                pick.Dist[at] = d;
                pick.Indices[at] = i;
            }
            pick.Count = m;
        }
    }
}
