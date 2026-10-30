using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The pure maths of <see cref="BoxCamera"/> (no input, no Unity objects), so the feel is unit-tested: drag speed as a
    /// fraction of the screen height, the touch slop, zoom-dependent pitch limits with soft edges, fling inertia, and the
    /// pan bounds by zoom.
    /// </summary>
    public static class BoxCameraMath
    {
        /// <summary>Degrees of yaw / pitch for a drag of one full screen height (resolution independent).</summary>
        public const float YawPerScreen = 110f, PitchPerScreen = 50f;
        /// <summary>A touch that moved less than this fraction of the screen height has not started an orbit (so a tap never nudges).</summary>
        public const float SlopFraction = 0.012f;
        public const float PitchCeiling = 68f, PitchFloorFull = 24f, PitchFloorClose = 14f, CloseZoom = 0.3f;
        /// <summary>Past a limit a drag counts at 1 / (1 + over / SoftRange): degrees for the angles, world units for the pan.</summary>
        public const float SoftRange = 6f, PanSoftRange = 0.25f;
        /// <summary>After release, what is past a limit decays as exp(-dt / SpringTau).</summary>
        public const float SpringTau = 0.12f;
        public const float InertiaTau = 0.2f, InertiaStop = 3f, InertiaCap = 200f, VelocityWindow = 0.08f;
        /// <summary>The shown angles follow the target angles with 1 - exp(-FollowRate dt).</summary>
        public const float FollowRate = 18f;
        /// <summary>Margin the pan keeps from the box's edge (world units).</summary>
        public const float PanMargin = 0.3f;

        public static float YawDegrees(float pixels, float screenHeight) => pixels / Mathf.Max(1f, screenHeight) * YawPerScreen;
        public static float PitchDegrees(float pixels, float screenHeight) => pixels / Mathf.Max(1f, screenHeight) * PitchPerScreen;

        public static bool PastSlop(Vector2 movedPixels, float screenHeight) => movedPixels.magnitude >= SlopFraction * screenHeight;

        /// <summary>The lowest pitch allowed: 24 degrees at full-box zoom (zoom 1 and out), down to 14 at zoom 0.3.</summary>
        public static float PitchFloor(float zoom) => Mathf.Lerp(PitchFloorClose, PitchFloorFull, Mathf.InverseLerp(CloseZoom, 1f, zoom));

        /// <summary>
        /// Moves <paramref name="value"/> by <paramref name="delta"/> inside [lo, hi]; the part of the move beyond a limit
        /// counts at 1 / (1 + over / range), where over is how far past the limit it already is: a soft wall, not a stop.
        /// Solved exactly (over + overÂ² / 2range = the drag beyond the limit), so the result does not depend on the frame rate.
        /// </summary>
        public static float SoftStep(float value, float delta, float lo, float hi, float range = SoftRange)
        {
            if (delta > 0f)
            {
                float free = Mathf.Min(delta, Mathf.Max(0f, hi - value));
                float start = value + free, rest = delta - free;
                if (rest <= 0f) return start;
                return hi + Beyond(Mathf.Max(0f, start - hi), rest, range);
            }
            if (delta < 0f)
            {
                float free = Mathf.Min(-delta, Mathf.Max(0f, value - lo));
                float start = value - free, rest = -delta - free;
                if (rest <= 0f) return start;
                return lo - Beyond(Mathf.Max(0f, lo - start), rest, range);
            }
            return value;
        }

        static float Beyond(float over, float drag, float range)
        {
            float s = over + over * over / (2f * range) + drag;
            return range * (Mathf.Sqrt(1f + 2f * s / range) - 1f);
        }

        /// <summary>After release: whatever is past [lo, hi] decays as exp(-dt / SpringTau).</summary>
        public static float Spring(float value, float lo, float hi, float dt)
        {
            float c = Mathf.Clamp(value, lo, hi);
            return c + (value - c) * Mathf.Exp(-dt / SpringTau);
        }

        /// <summary>Velocity (units per second) from the moves in the last <see cref="VelocityWindow"/> seconds before <paramref name="now"/>.</summary>
        public static Vector2 Velocity(IReadOnlyList<(float t, Vector2 d)> samples, float now)
        {
            var sum = Vector2.zero;
            for (int i = 0; i < samples.Count; i++) if (samples[i].t >= now - VelocityWindow) sum += samples[i].d;
            return sum / VelocityWindow;
        }

        /// <summary>A fling is capped at <see cref="InertiaCap"/> degrees per second.</summary>
        public static Vector2 CapFling(Vector2 v) => v.magnitude > InertiaCap ? v.normalized * InertiaCap : v;

        /// <summary>One frame of inertia: v · exp(-dt / InertiaTau), and zero once it is below <see cref="InertiaStop"/> degrees per second.</summary>
        public static Vector2 InertiaStep(Vector2 v, float dt)
        {
            v *= Mathf.Exp(-dt / InertiaTau);
            return v.magnitude < InertiaStop ? Vector2.zero : v;
        }

        /// <summary>k: 0 at full-box zoom (no pan, the box stays centred) up to 1 at zoom 0.3.</summary>
        public static float PanK(float zoom) => Mathf.Clamp01((1f - zoom) / 0.7f);

        /// <summary>The most the target may be off the box centre in x and z at this zoom.</summary>
        public static Vector2 PanBounds(float zoom)
        {
            float k = PanK(zoom);
            return new Vector2((Land.HX - PanMargin) * k, (Land.HZ - PanMargin) * k);
        }

        /// <summary>Zoom towards a point: the target moves (1 - z1/z0) of the way to the ground point under the fingers.</summary>
        public static Vector3 ZoomTarget(Vector3 target, Vector3 ground, float z0, float z1) => target + (ground - target) * (1f - z1 / z0);

        /// <summary>Where a ray meets the horizontal plane at height <paramref name="y"/> (false when it points away or parallel).</summary>
        public static bool GroundPoint(Ray ray, float y, out Vector3 point)
        {
            point = default;
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t <= 0f) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }
    }
}
