using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #543: where the camp's fish rack stands, pure geometry (x/z in world units, angle as <see cref="CampStash"/> uses it: x = cos, z =
    /// sin from the fire). One rack, shared by the fishers' catch and the smoking rack of #429. It stands in one of the two gaps between
    /// the fire's log seats that the stash does not use (the seats are at 0.4, 2.5 and 4.6 radians, the stash at 3.55, the crumb's stone
    /// at 0.29): about 83 or 323 degrees, whichever is farther from the hide rack's side (the seed's, <see cref="HideAngle"/>), so the two
    /// racks never meet. Its bar runs across the line from the fire; people stand on the fire's side of it.
    /// </summary>
    public static class FishRackSpot
    {
        /// <summary>Distances from the fire: the rack's middle, and where people stand to hang and take down (clear of the ring of seats at 0.3).</summary>
        public const float Distance = 0.62f, StandRadius = 0.46f;
        /// <summary>Half the bar's length, and the poles' reach across it (the rack is slim: a ridge pole on two A-frames).</summary>
        public const float HalfLength = 0.2f, HalfDepth = 0.07f;
        /// <summary>The two free gaps (radians), and the room a rack needs from the hide rack's angle.</summary>
        public static readonly float[] Gaps = { 1.449f, 5.637f };

        /// <summary>The hide rack's angle for a seed (the same hash as <c>LifePlanner.HideFrame</c>; a test pins it).</summary>
        public static float HideAngle(int seed) => (LifePlanner.Mix(seed, 0, 0, 410) >> 8) * (1f / 16777216f) * 6.2831853f;

        /// <summary>The rack's angle: the gap farther from the hide rack.</summary>
        public static float AngleFor(int seed)
        {
            float h = HideAngle(seed);
            return Gap(Gaps[0], h) >= Gap(Gaps[1], h) ? Gaps[0] : Gaps[1];
        }

        static float Gap(float a, float b)
        {
            float d = Math.Abs(a - b) % 6.2831853f;
            return d > 3.1415927f ? 6.2831853f - d : d;
        }

        /// <summary>A point <paramref name="radius"/> from the fire towards the rack.</summary>
        public static void At(float fx, float fz, int seed, float radius, out float x, out float z)
        {
            float a = AngleFor(seed);
            x = fx + (float)Math.Cos(a) * radius;
            z = fz + (float)Math.Sin(a) * radius;
        }

        /// <summary>The rack's centre and the unit direction along its bar.</summary>
        public static void Frame(float fx, float fz, int seed, out float x, out float z, out float dirX, out float dirZ)
        {
            float a = AngleFor(seed);
            float ux = (float)Math.Cos(a), uz = (float)Math.Sin(a);
            x = fx + ux * Distance; z = fz + uz * Distance;
            dirX = -uz; dirZ = ux;
        }

        // ---------------------------------------------------------------- the beats' timing (fractions of the hanger's beat)
        /// <summary>When fish <paramref name="i"/> of <paramref name="n"/> goes onto the rack, as a fraction of the hang beat: the hanger reaches up first
        /// (0.15), then one fish every 0.8 / n.</summary>
        public static float HangFraction(int i, int n) => 0.15f + 0.8f * (i + 1f) / Math.Max(1, n);

        /// <summary>When fish <paramref name="i"/> of <paramref name="n"/> comes off the rack in the take-down beat (0.1 in, then one every 0.85 / n).</summary>
        public static float TakeFraction(int i, int n) => 0.1f + 0.85f * (i + 1f) / Math.Max(1, n);

        /// <summary>How many of <paramref name="n"/> fish are on the rack <paramref name="k"/> (0 to 1) of the way through the hang beat.</summary>
        public static int HungBy(int n, float k)
        {
            int c = 0;
            for (int i = 0; i < n; i++) if (HangFraction(i, n) <= k + 1e-4f) c++;
            return c;
        }

        /// <summary>How many of <paramref name="n"/> fish have come off <paramref name="k"/> of the way through the take-down beat.</summary>
        public static int TakenBy(int n, float k)
        {
            int c = 0;
            for (int i = 0; i < n; i++) if (TakeFraction(i, n) <= k + 1e-4f) c++;
            return c;
        }
    }
}
