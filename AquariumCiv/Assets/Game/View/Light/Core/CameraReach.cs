using System;

namespace AquariumCiv.Light
{
    /// <summary>
    /// #146 camera reach, pure floats so it runs outside Unity. The BoxCamera wiring swaps its limits for these
    /// (<c>BoxCameraMath</c> itself keeps its old behaviour and its Unity tests until then).
    /// Zoom: smaller = closer. Land half-sizes are passed in (Land.HX = 3, Land.HZ = 2.2).
    /// </summary>
    public static class CameraReach
    {
        /// <summary>Closest zoom (was 0.3). Tune by playing.</summary>
        public const float MinZoom = 0.2f, MaxZoom = 1.3f;
        /// <summary>Share of the full pan range open at zoom 1 (was 0): a little sideways movement at full-box view.</summary>
        public const float FullBoxPan = 0.12f;
        /// <summary>Zoom at which the pan range is fully open (was 0.3).</summary>
        public const float FullPanZoom = 0.5f;
        /// <summary>Margin kept from the box edge (was 0.3) and how far past the edge the focus may go at the closest zoom.</summary>
        public const float Margin = 0.1f, CloseOvershoot = 0.35f;
        /// <summary>The pitch floor is 24 at zoom 1 and out, down to 14 at <see cref="MinZoom"/>.</summary>
        public const float PitchFloorFull = 24f, PitchFloorClose = 14f;

        static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
        static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>0..1 share of the pan range open at this zoom. Continuous: FullBoxPan at 1, 1 at FullPanZoom, a little less past 1.</summary>
        public static float PanK(float zoom)
        {
            if (zoom >= 1f) return FullBoxPan * (1f - 0.5f * Clamp01((zoom - 1f) / (MaxZoom - 1f)));
            return FullBoxPan + (1f - FullBoxPan) * Smooth((1f - zoom) / (1f - FullPanZoom));
        }

        /// <summary>Most the focus may be off the box centre in x and z.</summary>
        public static void PanBounds(float zoom, float halfX, float halfZ, out float boundX, out float boundZ)
        {
            float k = PanK(zoom);
            float over = CloseOvershoot * Smooth((FullPanZoom - zoom) / (FullPanZoom - MinZoom));
            boundX = (halfX - Margin) * k + over * k;
            boundZ = (halfZ - Margin) * k + over * k;
        }

        public static float PitchFloor(float zoom)
        {
            float t = Clamp01((zoom - MinZoom) / (1f - MinZoom));
            return PitchFloorClose + (PitchFloorFull - PitchFloorClose) * t;
        }
    }

    /// <summary>
    /// Glide back after a fly-in (#146). The camera remembers where it was when the flight started; after the flight it
    /// waits <see cref="Hold"/> seconds, then eases back over <see cref="Duration"/>. Any touch cancels it.
    /// </summary>
    public sealed class GlideBack
    {
        public const float Hold = 1.2f, Duration = 1.1f;

        public bool Armed { get; private set; }
        public bool Gliding { get; private set; }
        float _x, _z, _zoom, _t, _sx, _sz, _sz0;

        /// <summary>Call as a fly-in starts, with the focus (x, z) and zoom to return to.</summary>
        public void Remember(float x, float z, float zoom) { _x = x; _z = z; _zoom = zoom; Armed = true; Gliding = false; _t = 0f; }

        /// <summary>Call on any touch or a new flight: forget the return.</summary>
        public void Cancel() { Armed = false; Gliding = false; _t = 0f; }

        /// <summary>
        /// Call every frame once the flight has ended and the camera is free. Moves x, z and zoom; returns true while it
        /// owns the camera. Eases with a smoother-step from where the glide began, so there is no jerk at either end.
        /// </summary>
        public bool Step(float dt, ref float x, ref float z, ref float zoom)
        {
            if (!Armed) return false;
            _t += dt;
            if (!Gliding)
            {
                if (_t < Hold) return false;
                Gliding = true; _sx = x; _sz = z; _sz0 = zoom; _t = 0f;
            }
            float u = Clamp(_t / Duration);
            float e = u * u * u * (u * (u * 6f - 15f) + 10f);
            x = _sx + (_x - _sx) * e;
            z = _sz + (_z - _sz) * e;
            zoom = _sz0 + (_zoom - _sz0) * e;
            if (u >= 1f) Cancel();
            return true;
        }

        static float Clamp(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }
}
