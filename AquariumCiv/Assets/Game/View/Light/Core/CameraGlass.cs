using System;

namespace AquariumCiv.Lighting
{
    /// <summary>
    /// #273 (pure floats, runs in dotnet test): the camera reaches the glass on every side at every zoom and never goes past it.
    /// The pivot may travel to the glass's inner face (less a hair, <see cref="Margin"/>) whatever the zoom; past the rest limit a
    /// drag meets a soft band of <see cref="Gap"/> that it can only approach (<see cref="Step"/>), never cross, and that settles
    /// back on release. The camera itself sits outside the box: <see cref="PanScale"/> draws the pan in whenever a pose
    /// (yaw, pitch, distance) would put it inside the box's hull, so no yaw / pitch / zoom combination puts it in a pane.
    /// </summary>
    public static class CameraGlass
    {
        /// <summary>How close the pivot comes to the inner face at the hard end, and the width of the soft band in front of it.</summary>
        public const float Margin = 0.04f, Gap = 0.1f;
        /// <summary>The camera stays this far outside the box's hull (its near plane is at least 0.3 on top of that).</summary>
        public const float CameraClear = 0.5f;

        /// <summary>Rest limits of the pivot (where it settles) in x and z. Same at every zoom; the soft band lies beyond them.</summary>
        public static void PanBounds(float halfX, float halfZ, out float boundX, out float boundZ)
        {
            boundX = Math.Max(0f, halfX - Margin - Gap);
            boundZ = Math.Max(0f, halfZ - Margin - Gap);
        }

        /// <summary>The pivot never gets past this (the soft band's asymptote).</summary>
        public static void HardStop(float halfX, float halfZ, out float stopX, out float stopZ)
        {
            stopX = Math.Max(0f, halfX - Margin);
            stopZ = Math.Max(0f, halfZ - Margin);
        }

        /// <summary>
        /// Moves <paramref name="value"/> by <paramref name="delta"/> inside [lo, hi]. Beyond a limit the move counts at
        /// (1 - over / gap): the same speed at the limit (no jump), slower the further in, and over tends to gap but never reaches it.
        /// Exact in closed form, so it does not depend on the frame rate.
        /// </summary>
        public static float Step(float value, float delta, float lo, float hi, float gap = Gap)
        {
            if (delta > 0f)
            {
                float free = Math.Min(delta, Math.Max(0f, hi - value));
                float over = Math.Max(0f, value + free - hi), rest = delta - free;
                return rest <= 0f ? value + free : hi + Beyond(over, rest, gap);
            }
            if (delta < 0f)
            {
                float free = Math.Min(-delta, Math.Max(0f, value - lo));
                float over = Math.Max(0f, lo - (value - free)), rest = -delta - free;
                return rest <= 0f ? value - free : lo - Beyond(over, rest, gap);
            }
            return value;
        }

        static float Beyond(float over, float drag, float gap)
        {
            over = Math.Min(over, gap);
            return gap - (gap - over) * (float)Math.Exp(-drag / gap);
        }

        /// <summary>The camera's offset from its pivot: distance away, pitch up, yaw round (Unity's Euler(pitch, yaw, 0) * back).</summary>
        public static void CameraOffset(float yawDeg, float pitchDeg, float distance, out float ox, out float oy, out float oz)
        {
            float y = yawDeg * (float)Math.PI / 180f, p = pitchDeg * (float)Math.PI / 180f;
            ox = -(float)(Math.Cos(p) * Math.Sin(y)) * distance;
            oy = (float)Math.Sin(p) * distance;
            oz = -(float)(Math.Cos(p) * Math.Cos(y)) * distance;
        }

        /// <summary>Is the camera at (x, y, z) inside the box's hull grown by <paramref name="clear"/>? Hull half sizes and glass top come in.</summary>
        public static bool CameraInsideBox(float x, float y, float z, float hullX, float hullZ, float bottom, float top, float clear = CameraClear)
            => Math.Abs(x) < hullX + clear && Math.Abs(z) < hullZ + clear && y > bottom - clear && y < top + clear;

        /// <summary>
        /// 0..1: how much of the pan range the pose keeps. 1 when the camera is clear of the box wherever the pivot goes within
        /// its bounds (above the glass, or beside it); smaller when the far side of the pan would put it into a pane. Continuous in the pose.
        /// </summary>
        public static float PanScale(float yawDeg, float pitchDeg, float distance, float pivotY, float boundX, float boundZ,
            float hullX, float hullZ, float bottom, float top, float clear = CameraClear)
        {
            CameraOffset(yawDeg, pitchDeg, distance, out float ox, out float oy, out float oz);
            float camY = pivotY + oy;
            if (camY >= top + clear || camY <= bottom - clear) return 1f;
            return Math.Max(AxisScale(ox, boundX, hullX + clear), AxisScale(oz, boundZ, hullZ + clear));
        }

        // The largest s with |px + o| >= half for every |px| <= bound * s: the camera is clear on this axis.
        static float AxisScale(float o, float bound, float half)
        {
            float room = Math.Abs(o) - half;
            if (room < 0f) return 0f;
            if (bound <= 1e-6f) return 1f;
            return Math.Min(1f, room / bound);
        }

        /// <summary>Rest limits for a pose: <see cref="PanBounds"/> drawn in by <see cref="PanScale"/>.</summary>
        public static void PanBoundsFor(float yawDeg, float pitchDeg, float distance, float pivotY, float halfX, float halfZ,
            float hullX, float hullZ, float bottom, float top, out float boundX, out float boundZ)
        {
            PanBounds(halfX, halfZ, out boundX, out boundZ);
            float s = PanScale(yawDeg, pitchDeg, distance, pivotY, boundX, boundZ, hullX, hullZ, bottom, top);
            boundX *= s; boundZ *= s;
        }
    }
}
