using System;
using System.Collections.Generic;

namespace AquariumCiv.View.Moments
{
    /// <summary>
    /// The guided camera of a moment (era-moment.md §3–§5) as pure maths: the beats' camera keys flattened onto one time
    /// line, eased between keys, from the pose the player left the camera in. One finger can still orbit while it plays;
    /// let go and the offset eases back to the rail (<see cref="GuidedOffset"/>). Poses stay inside BoxCamera's ranges
    /// (<see cref="Limits"/>; the same numbers as BoxCamera's fields and BoxCameraMath, tester #31: the orbit stays in
    /// front of the box).
    /// </summary>
    public class CameraRail
    {
        /// <summary>One key at an absolute moment time.</summary>
        /// <summary>A key at an absolute moment time; the move to it runs from <see cref="from"/> to <see cref="time"/>
        /// (the first key of a beat starts moving when its beat starts, so the camera holds between beats).</summary>
        public readonly struct Key
        {
            public readonly float from, time;
            public readonly CameraKey key;
            public Key(float from, float time, CameraKey key) { this.from = from; this.time = time; this.key = key; }
        }

        readonly List<Key> keys = new();
        public IReadOnlyList<Key> Keys => keys;

        /// <summary>When the camera is released (the first beat with <see cref="MomentBeat.freeCamera"/>); +inf if never.</summary>
        public float FreeAt { get; private set; } = float.PositiveInfinity;

        /// <summary>The rail of the beats that play (<paramref name="beats"/> already filtered, in time order).</summary>
        public CameraRail(IReadOnlyList<MomentBeat> beats)
        {
            float last = 0f;
            foreach (var b in beats)
            {
                if (b.freeCamera && float.IsPositiveInfinity(FreeAt)) FreeAt = b.start;
                var own = new List<CameraKey>(b.camera);
                own.Sort((x, y) => x.at.CompareTo(y.at));
                float from = Math.Max(last, b.start);
                foreach (var k in own)
                {
                    float t = Math.Max(from, b.start + k.at);
                    keys.Add(new Key(from, t, k));
                    from = last = t;
                }
            }
        }

        /// <summary>The guided pose at moment time <paramref name="t"/>. Before the first key it glides from
        /// <paramref name="from"/> (where the player left the camera); after the last key it holds.</summary>
        public CameraPose Evaluate(float t, CameraPose from, IMomentAnchors anchors)
        {
            if (keys.Count == 0) return from;
            var prev = from;
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                var pose = PoseOf(k.key, anchors);
                if (t < k.time)
                {
                    if (t <= k.from) return prev; // holding before this key's move starts
                    float u = Ease(k.key.ease, (t - k.from) / (k.time - k.from));
                    return Blend(prev, pose, u);
                }
                prev = pose;
            }
            return prev;
        }

        /// <summary>A key's pose: its anchor (Box if the view can't place it) plus offset, clamped to BoxCamera's ranges.</summary>
        public static CameraPose PoseOf(CameraKey k, IMomentAnchors anchors)
        {
            var p = AnchorPoint(k.anchor, anchors) + k.offset;
            return Limits.Clamp(new CameraPose(p, k.yaw, k.pitch, k.zoom));
        }

        public static MVec3 AnchorPoint(MomentAnchor a, IMomentAnchors anchors)
        {
            if (a != MomentAnchor.Box && anchors != null && anchors.TryGet(a, out var p)) return p;
            if (anchors != null && anchors.TryGet(MomentAnchor.Box, out var box)) return box;
            return CameraPose.Default.target;
        }

        public static CameraPose Blend(CameraPose a, CameraPose b, float u) => new(
            MVec3.Lerp(a.target, b.target, u), Lerp(a.yaw, b.yaw, u), Lerp(a.pitch, b.pitch, u), Lerp(a.zoom, b.zoom, u));

        static float Lerp(float a, float b, float u) => a + (b - a) * u;

        public static float Ease(MomentEase e, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return e switch
            {
                MomentEase.Linear => t,
                MomentEase.EaseOut => 1f - (1f - t) * (1f - t),
                MomentEase.EaseIn => t * t,
                _ => t * t * t * (t * (t * 6f - 15f) + 10f), // smootherstep, as BoxCameraMath.FlyEase
            };
        }

        /// <summary>BoxCamera's ranges, mirrored here for the pure code (BoxCamera.minYaw/maxYaw/minZoom/maxZoom,
        /// BoxCameraMath.PitchCeiling/PitchFloor). Keep in step with those.</summary>
        public static class Limits
        {
            public const float MinYaw = -30f, MaxYaw = 55f, MinZoom = 0.3f, MaxZoom = 1.3f;
            public const float PitchCeiling = 68f, PitchFloorFull = 24f, PitchFloorClose = 14f, CloseZoom = 0.3f;
            /// <summary>Box interior half-size and the pan margin (Land.HX/HZ, BoxCameraMath.PanMargin).</summary>
            public const float HX = 3f, HZ = 2.2f, PanMargin = 0.3f;

            public static float PitchFloor(float zoom)
            {
                float u = (zoom - CloseZoom) / (1f - CloseZoom);
                u = u < 0f ? 0f : u > 1f ? 1f : u;
                return PitchFloorClose + (PitchFloorFull - PitchFloorClose) * u;
            }

            /// <summary>The pan allowed at a zoom (BoxCameraMath.PanBounds): none at zoom 1 and out.</summary>
            public static (float x, float z) PanBounds(float zoom)
            {
                float k = (1f - zoom) / 0.7f; k = k < 0f ? 0f : k > 1f ? 1f : k;
                return ((HX - PanMargin) * k, (HZ - PanMargin) * k);
            }

            public static CameraPose Clamp(CameraPose p)
            {
                p.zoom = Clamp(p.zoom, MinZoom, MaxZoom);
                p.yaw = Clamp(p.yaw, MinYaw, MaxYaw);
                p.pitch = Clamp(p.pitch, PitchFloor(p.zoom), PitchCeiling);
                var (bx, bz) = PanBounds(p.zoom);
                p.target.x = Clamp(p.target.x, -bx, bx);
                p.target.z = Clamp(p.target.z, -bz, bz);
                return p;
            }

            public static bool Inside(CameraKey k) =>
                k.yaw >= MinYaw && k.yaw <= MaxYaw && k.zoom >= MinZoom && k.zoom <= MaxZoom &&
                k.pitch >= PitchFloor(k.zoom) && k.pitch <= PitchCeiling;

            static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        }
    }

    /// <summary>
    /// The player's own orbit on top of the rail (§3: "one finger can still orbit; let go and it eases back"). While a
    /// finger is down the drag adds to the offset; released, the offset decays as exp(-dt / <see cref="ReturnTau"/>).
    /// </summary>
    public class GuidedOffset
    {
        /// <summary>Seconds for the offset to fall to 1/e after release [est].</summary>
        public const float ReturnTau = 0.6f;
        /// <summary>The most the player can pull off the rail, in degrees [est].</summary>
        public const float MaxYaw = 40f, MaxPitch = 20f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Held { get; private set; }

        /// <summary>One frame: <paramref name="dragYaw"/>/<paramref name="dragPitch"/> are this frame's drag in degrees
        /// (BoxCameraMath.YawDegrees / PitchDegrees), <paramref name="touching"/> whether a finger is down.</summary>
        public void Step(float dt, bool touching, float dragYaw = 0f, float dragPitch = 0f)
        {
            Held = touching;
            if (touching)
            {
                Yaw = Clamp(Yaw + dragYaw, -MaxYaw, MaxYaw);
                Pitch = Clamp(Pitch + dragPitch, -MaxPitch, MaxPitch);
                return;
            }
            float k = (float)Math.Exp(-Math.Max(0f, dt) / ReturnTau);
            Yaw *= k; Pitch *= k;
            if (Math.Abs(Yaw) < 0.01f) Yaw = 0f;
            if (Math.Abs(Pitch) < 0.01f) Pitch = 0f;
        }

        public void Reset() { Yaw = Pitch = 0f; Held = false; }

        /// <summary>The rail pose with the player's offset on top, kept inside BoxCamera's ranges.</summary>
        public CameraPose Apply(CameraPose rail)
        {
            rail.yaw += Yaw; rail.pitch += Pitch;
            return CameraRail.Limits.Clamp(rail);
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
