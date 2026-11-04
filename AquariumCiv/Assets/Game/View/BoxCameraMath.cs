using System.Collections.Generic;
using AquariumCiv.Lighting;
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
        public const float PitchCeiling = 68f;
        /// <summary>Past a limit a drag counts at 1 / (1 + over / SoftRange): degrees for the angles, world units for the pan.</summary>
        public const float SoftRange = 6f, PanSoftRange = 0.25f;
        /// <summary>After release, what is past a limit decays as exp(-dt / SpringTau).</summary>
        public const float SpringTau = 0.12f;
        public const float InertiaTau = 0.2f, InertiaStop = 3f, InertiaCap = 200f, VelocityWindow = 0.08f;
        /// <summary>The shown angles follow the target angles with 1 - exp(-FollowRate dt).</summary>
        public const float FollowRate = 18f;
        /// <summary>#193: how far past the box the shadows reach, the air kept in front of what's nearest, and the closest the near plane goes.</summary>
        public const float DepthBack = 8f, NearMargin = 0.5f, NearestPlane = 0.3f;
        /// <summary>The box's hull and the shelf under it (the plane everything stands on).</summary>
        public const float HullSide = 0.3f, ShelfY = -0.3f;
        /// <summary>The far plane is never closer than this past the camera's focus: the shelf view backs away as the row of boxes grows.</summary>
        public const float FarPastFocus = 80f;

        /// <summary>The view depth at which the bottom edge of the screen comes down to <paramref name="height"/>. Nothing on screen at or below that height is
        /// nearer than this, wherever it stands: the screen's lowest ray is the first to reach it. 0 when the camera is below it, or looks up.</summary>
        public static float DepthAtHeight(Vector3 pos, Vector3 forward, Vector3 up, float tanHalfFov, float height)
        {
            float drop = tanHalfFov * up.y - forward.y;
            return drop <= 1e-4f || pos.y <= height ? 0f : (pos.y - height) / drop;
        }

        /// <summary>The nearest of the box's corners along the view.</summary>
        public static float BoxDepth(Vector3 pos, Vector3 forward)
        {
            float best = float.MaxValue, hx = Land.HX + HullSide, hz = Land.HZ + HullSide;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -hx : hx, (i & 2) == 0 ? ShelfY : Land.GlassTop + 0.1f, (i & 4) == 0 ? -hz : hz);
                best = Mathf.Min(best, Vector3.Dot(c - pos, forward));
            }
            return best;
        }

        /// <summary>Tester run 10, and the user's "only a piece of the tank": the near plane comes from the geometry, not from a margin round the box. It sits just in front of
        /// the box and the shelf on screen, as tight as #193's shadows want. <paramref name="rowTop"/> &gt;= 0: other boxes stand on the shelf too, up to that height, anywhere along it.</summary>
        public static float FitNear(Vector3 pos, Vector3 forward, Vector3 up, float tanHalfFov, float rowTop = -1f)
        {
            float d = Mathf.Min(BoxDepth(pos, forward), DepthAtHeight(pos, forward, up, tanHalfFov, ShelfY));
            if (rowTop >= 0f) d = Mathf.Min(d, DepthAtHeight(pos, forward, up, tanHalfFov, rowTop));
            return Mathf.Max(NearestPlane, d - NearMargin);
        }

        /// <summary>Fly-to: default duration, and the zoom a flight to a person / a place ends at (< 1 = closer).</summary>
        public const float FlyDefaultSeconds = 0.9f, FlyPersonZoom = 0.45f, FlyPlaceZoom = 0.6f;

        /// <summary>Smoother-step ease in and out, t clamped to 0..1.</summary>
        public static float FlyEase(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6f - 15f) + 10f); }

        /// <summary>The target kept inside the pan bounds of this zoom (a flight never ends where the camera would spring back).</summary>
        public static Vector3 ClampFocus(Vector3 target, float zoom)
        {
            var b = PanBounds(zoom);
            return new Vector3(Mathf.Clamp(target.x, -b.x, b.x), target.y, Mathf.Clamp(target.z, -b.y, b.y));
        }

        /// <summary>#273: the same, against bounds the caller already drew in for its pose (<see cref="PanBoundsFor"/>).</summary>
        public static Vector3 ClampFocus(Vector3 target, Vector2 bound)
            => new Vector3(Mathf.Clamp(target.x, -bound.x, bound.x), target.y, Mathf.Clamp(target.z, -bound.y, bound.y));

        public static float YawDegrees(float pixels, float screenHeight) => pixels / Mathf.Max(1f, screenHeight) * YawPerScreen;
        public static float PitchDegrees(float pixels, float screenHeight) => pixels / Mathf.Max(1f, screenHeight) * PitchPerScreen;

        public static bool PastSlop(Vector2 movedPixels, float screenHeight) => movedPixels.magnitude >= SlopFraction * screenHeight;

        /// <summary>The lowest pitch allowed: 24 degrees at full-box zoom (zoom 1 and out), down to 14 at the closest zoom.</summary>
        public static float PitchFloor(float zoom) => CameraReach.PitchFloor(zoom);

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

        /// <summary>k: a little at full-box zoom (#146), fully open by zoom 0.5.</summary>
        public static float PanK(float zoom) => CameraReach.PanK(zoom);

        /// <summary>#273: the most the target may be off the box centre in x and z. The same at every zoom: the pivot rests up to the glass's inner face.</summary>
        public static Vector2 PanBounds(float zoom)
        {
            CameraGlass.PanBounds(Land.HX, Land.HZ, out float x, out float z);
            return new Vector2(x, z);
        }

        /// <summary>#273 (user 2026-11-04): the pan range for a pose, limited by the VIEW: the pivot may move until the visible ground's edge reaches the glass (a long way close up,
        /// nothing once the whole box fits), and drawn in where the camera would enter the box's hull. Portrait phone by default.</summary>
        public static Vector2 PanBoundsFor(float yaw, float pitch, float distance, float pivotY, float tanHalfV = 0.19438f, float aspect = 0.5625f)
        {
            CameraGlass.PanBoundsFor(yaw, pitch, distance, pivotY, tanHalfV, aspect, Land.HX, Land.HZ, Land.HX + HullSide, Land.HZ + HullSide, ShelfY, Land.GlassTop, out float x, out float z);
            return new Vector2(x, z);
        }

        /// <summary>#273: a pan drag with a soft band at the glass: full speed up to the rest limit, slower through the band, never past the hard stop.</summary>
        public static float GlassStep(float value, float delta, float lo, float hi) => CameraGlass.Step(value, delta, lo, hi);

        /// <summary>A pan's hard stop, the soft band's end: the pivot never reaches the glass.</summary>
        public static Vector2 PanHardStop
        {
            get { CameraGlass.HardStop(Land.HX, Land.HZ, out float x, out float z); return new Vector2(x, z); }
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
