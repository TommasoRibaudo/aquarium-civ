// Visit loop t1-huntgrass (tester finding 4): the hunt is played on open ground, not behind tall grass. Pure (System only): Tools/SimTests compile it.
using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// Where the hunt's stalk, throw and hold-up may stand. The default camera (BoxCamera yaw 28) looks at the box from the front-left, so
    /// whatever stands on the camera side of a doll, within about 0.3 u, hides it: tall-grass tufts are 0.08 to 0.13 u high, a doll about 0.2.
    /// A spot is "clear" when no hiding cell lies within that reach toward the camera (and a little to each side of it).
    /// </summary>
    public static class HuntOpen
    {
        /// <summary>BoxCamera's home yaw in degrees; the camera sits at (-sin, -cos) of it from what it looks at, in world x/z.</summary>
        public const float CameraYawDegrees = 28f;
        public static readonly float CamX = -(float)Math.Sin(CameraYawDegrees * Math.PI / 180.0), CamZ = -(float)Math.Cos(CameraYawDegrees * Math.PI / 180.0);
        /// <summary>How far toward the camera something may hide a doll, and the sideways reach at the far samples.</summary>
        public const float Reach = 0.3f, Side = 0.1f;

        /// <summary>No hiding cell at the spot or on its camera side. <paramref name="hides"/> answers for a world point.</summary>
        public static bool SpotClear(float x, float z, Func<float, float, bool> hides)
        {
            if (hides == null) return true;
            if (hides(x, z)) return false;
            for (float s = 0.1f; s <= Reach + 1e-4f; s += 0.1f)
            {
                float cx = x + CamX * s, cz = z + CamZ * s;
                if (hides(cx, cz)) return false;
                if (s > 0.15f && (hides(cx - CamZ * Side, cz + CamX * Side) || hides(cx + CamZ * Side, cz - CamX * Side))) return false;
            }
            return true;
        }

        /// <summary>Every spot of a hunt's line (stalk start, middle of the stalk, throw point, the rabbit and the stand) is clear.</summary>
        public static bool LineClear(float stalkX, float stalkZ, float throwX, float throwZ, float targetX, float targetZ, float standX, float standZ, Func<float, float, bool> hides)
            => Hidden(stalkX, stalkZ, throwX, throwZ, targetX, targetZ, standX, standZ, hides) == 0;

        /// <summary>How many of the line's five spots are hidden (0 = clear); the planner takes the least hidden line when none is clear.
        /// The throw and the stand count double: they are what the player watches.</summary>
        public static int Hidden(float stalkX, float stalkZ, float throwX, float throwZ, float targetX, float targetZ, float standX, float standZ, Func<float, float, bool> hides)
        {
            int n = 0;
            if (!SpotClear(stalkX, stalkZ, hides)) n++;
            if (!SpotClear((stalkX + throwX) * 0.5f, (stalkZ + throwZ) * 0.5f, hides)) n++;
            if (!SpotClear(throwX, throwZ, hides)) n += 2;
            if (!SpotClear(targetX, targetZ, hides)) n++;
            if (!SpotClear(standX, standZ, hides)) n += 2;
            return n;
        }
    }
}
