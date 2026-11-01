using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.View.Shaping
{
    /// <summary>
    /// #151: where each grazer stands round the herd's focus, pure. A sunflower spiral (a golden-angle spread), turned by the
    /// herd's seed and nudged a little per animal, scaled so the nearest two are at least <c>spacing</c> apart. It depends only
    /// on (seed, index, count, spacing): the herd keeps its shape as it moves, so it stays one herd and nobody overlaps.
    /// </summary>
    public static class HerdLayout
    {
        // The closest pair of a golden-angle spiral (unit scale) is 1.546 apart, whatever the count (5 to 8, checked in tests).
        const float UnitMin = 1.546f, Roomy = 1.3f, Nudge = 0.07f, Golden = 2.3999632f;

        /// <summary>The farthest any of <paramref name="count"/> animals stands from the focus.</summary>
        public static float Reach(int count, float spacing) => spacing * Roomy / UnitMin * (float)Math.Sqrt(Math.Max(1, count) - 0.5f) + spacing * Nudge * 1.5f;

        public static void Offset(int seed, int i, int count, float spacing, out float dx, out float dz)
        {
            float c = spacing * Roomy / UnitMin;
            float turn = Terrain.Hash3(seed, 0, 31) * 6.2831853f;
            float r = c * (float)Math.Sqrt(i + 0.5f), a = turn + i * Golden;
            float jx = (Terrain.Hash3(seed, i, 32) * 2f - 1f) * spacing * Nudge;
            float jz = (Terrain.Hash3(seed, i, 33) * 2f - 1f) * spacing * Nudge;
            dx = (float)Math.Cos(a) * r + jx;
            dz = (float)Math.Sin(a) * r + jz;
        }
    }
}
