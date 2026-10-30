using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.View
{
    /// <summary>
    /// The slice's camera on the box. One meaning per gesture: one finger orbits, two fingers pan and zoom together, a tap is
    /// left to selection (a touch that stays within a small slop never moves the camera). In the Editor, left drag orbits,
    /// right or middle drag pans, scroll zooms towards the pointer. The default distance fits the whole box across the
    /// screen and is refitted whenever the aspect changes (the Game view size lands a few frames into play mode,
    /// docs/tooling.md). Input only in play mode. The numbers live in <see cref="BoxCameraMath"/> (unit-tested).
    /// <list type="bullet">
    /// <item>Orbit speed is a fraction of the screen height (110 degrees of yaw, 50 of pitch per screen height), so it feels the same on every phone.</item>
    /// <item>The pitch floor depends on zoom (24 degrees at the full box down to 14 close in), the ceiling is 68. Limits are soft: past one the drag counts less and less, and on release the camera springs back.</item>
    /// <item>Release flings with the velocity of the last 80 ms, decaying over about 0.2 s; the shown angles follow the targets smoothly.</item>
    /// <item>Pinch zooms by ratio towards the fingers; the pan grabs the ground (the point under the fingers stays under them), bounded by zoom so the full box stays centred, with a rubber band.</item>
    /// </list>
    /// </summary>
    [ExecuteAlways]
    public class BoxCamera : MonoBehaviour
    {
        public Vector3 target = new(0, 1.0f, 0);
        public float yaw = 28f, pitch = 34f;
        [Tooltip("Orbit stays in front of the box, so the shelf and wall behind it stay in view (tester #31).")]
        public float minYaw = -30f, maxYaw = 55f;
        [Tooltip("Zoom: 1 = the whole box fits the width; < 1 is closer.")]
        public float zoom = 1f, minZoom = 0.3f, maxZoom = 1.3f;
        [Tooltip("How much of the screen width the box takes at zoom 1 (about).")]
        public float fill = 1f;

        Camera cam;

        // The angles the drag moves; yaw / pitch follow them smoothly.
        float targetYaw, targetPitch, lastYaw, lastPitch;
        bool synced;
        Vector2 velocity; // degrees per second (yaw, pitch)
        readonly List<(float t, Vector2 d)> samples = new();

        enum Mode { None, Orbit, TwoFinger, Locked }
        Mode mode;
        Vector2 pressPos, lastPos, prevMid;
        float prevDist;
        bool orbiting, moved;
        bool pressOnUi;

        /// <summary>True from the moment a press has moved past the slop (or a second finger came down) until all fingers lift:
        /// selection should ignore the release as a tap.</summary>
        public bool GestureMoved => moved;
        /// <summary>True while a finger or button is down on the camera.</summary>
        public bool Touching => mode != Mode.None;

        public float FitDistance()
        {
            if (cam == null) cam = GetComponent<Camera>();
            float aspect = cam != null ? cam.aspect : 9f / 16f;
            float fov = cam != null ? cam.fieldOfView : 22f;
            float halfH = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, aspect));
            return new Vector3(Land.HX + 0.3f, 1.7f, Land.HZ + 0.3f).magnitude / Mathf.Tan(halfH) / Mathf.Max(0.1f, fill);
        }

        void OnEnable() { synced = false; mode = Mode.None; velocity = Vector2.zero; }

        void Update()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (Application.isPlaying) Step(Mathf.Min(Time.unscaledDeltaTime, 0.1f), Time.unscaledTime);
            else { targetYaw = lastYaw = yaw; targetPitch = lastPitch = pitch; synced = true; }
            Apply();
        }

        /// <summary>Reads the input and moves the camera by one frame.</summary>
        void Step(float dt, float now)
        {
            // Something else set yaw / pitch (a debug cheat, a test): take that as the new target.
            if (!synced || Mathf.Abs(yaw - lastYaw) > 1e-3f) targetYaw = yaw;
            if (!synced || Mathf.Abs(pitch - lastPitch) > 1e-3f) targetPitch = pitch;
            synced = true;

            ReadPointers(out int n, out Vector2 a, out Vector2 b, out bool mousePan);
            // A press that starts on the HUD belongs to the HUD until every finger lifts.
            if (n == 0) pressOnUi = false;
            else if (mode == Mode.None && AquariumCiv.Game.UiInput.Over(a)) pressOnUi = true;
            if (pressOnUi) { n = 0; mode = Mode.None; orbiting = false; }
            float H = Mathf.Max(1f, Screen.height);

            if (n == 0) EndPress(now);
            else if (n >= 2 || mousePan) PanZoom(n, a, b, mousePan);
            else Orbit(a, H, now);

            if (Mouse.current != null && n == 0)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) ZoomAbout(Mathf.Clamp(zoom * (1f - scroll * 0.0008f), minZoom, maxZoom), Mouse.current.position.ReadValue());
            }

            bool dragging = mode == Mode.Orbit && orbiting;
            float lo = BoxCameraMath.PitchFloor(zoom), hi = BoxCameraMath.PitchCeiling;
            if (!dragging)
            {
                if (velocity != Vector2.zero)
                {
                    targetYaw = BoxCameraMath.SoftStep(targetYaw, velocity.x * dt, minYaw, maxYaw);
                    targetPitch = BoxCameraMath.SoftStep(targetPitch, velocity.y * dt, lo, hi);
                    velocity = BoxCameraMath.InertiaStep(velocity, dt);
                }
                targetYaw = BoxCameraMath.Spring(targetYaw, minYaw, maxYaw, dt);
                targetPitch = BoxCameraMath.Spring(targetPitch, lo, hi, dt);
            }
            if (mode != Mode.TwoFinger) SpringPan(dt);

            float f = 1f - Mathf.Exp(-BoxCameraMath.FollowRate * dt);
            yaw += (targetYaw - yaw) * f;
            pitch += (targetPitch - pitch) * f;
            lastYaw = yaw; lastPitch = pitch;
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        // ---------------- input ----------------
        /// <summary>The first two live fingers (or the mouse). <paramref name="mousePan"/>: a right or middle button is down.</summary>
        static void ReadPointers(out int n, out Vector2 a, out Vector2 b, out bool mousePan)
        {
            n = 0; a = b = default; mousePan = false;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.isInProgress) continue;
                    if (n == 0) a = t.position.ReadValue(); else if (n == 1) b = t.position.ReadValue();
                    n++;
                }
                if (n > 0) return;
            }
            var m = Mouse.current;
            if (m == null) return;
            a = m.position.ReadValue();
            if (m.rightButton.isPressed || m.middleButton.isPressed) { n = 1; mousePan = true; }
            else if (m.leftButton.isPressed) n = 1;
        }

        void EndPress(float now)
        {
            if (mode == Mode.Orbit && orbiting) velocity = BoxCameraMath.CapFling(BoxCameraMath.Velocity(samples, now));
            mode = Mode.None; orbiting = false; moved = false; samples.Clear();
        }

        void Orbit(Vector2 pos, float H, float now)
        {
            if (mode == Mode.TwoFinger || mode == Mode.Locked) { mode = Mode.Locked; return; } // one finger left of a two-finger gesture: ignore until all lift
            if (mode == Mode.None)
            {
                mode = Mode.Orbit; pressPos = lastPos = pos; orbiting = moved = false; velocity = Vector2.zero; samples.Clear();
                return;
            }
            if (!orbiting)
            {
                if (!BoxCameraMath.PastSlop(pos - pressPos, H)) return;
                orbiting = moved = true; lastPos = pos; // the slop itself is swallowed: no jump when it is crossed
                return;
            }
            var d = pos - lastPos; lastPos = pos;
            float dYaw = BoxCameraMath.YawDegrees(d.x, H), dPitch = -BoxCameraMath.PitchDegrees(d.y, H);
            targetYaw = BoxCameraMath.SoftStep(targetYaw, dYaw, minYaw, maxYaw);
            targetPitch = BoxCameraMath.SoftStep(targetPitch, dPitch, BoxCameraMath.PitchFloor(zoom), BoxCameraMath.PitchCeiling);
            samples.Add((now, new Vector2(dYaw, dPitch)));
            if (samples.Count > 32) samples.RemoveAt(0);
        }

        /// <summary>Two fingers (or a right / middle mouse drag): pinch zoom towards the fingers and pan grabbing the ground.</summary>
        void PanZoom(int n, Vector2 a, Vector2 b, bool mousePan)
        {
            Vector2 mid = n >= 2 ? (a + b) * 0.5f : a;
            float dist = n >= 2 ? Vector2.Distance(a, b) : 0f;
            if (mode != Mode.TwoFinger)
            {
                mode = Mode.TwoFinger; orbiting = false; moved = true; velocity = Vector2.zero; samples.Clear();
                prevMid = mid; prevDist = dist;
                return;
            }
            if (n >= 2 && dist > 1f && prevDist > 1f) ZoomAbout(Mathf.Clamp(zoom * prevDist / dist, minZoom, maxZoom), mid);
            if (GroundAt(prevMid, out var g0) && GroundAt(mid, out var g1))
            {
                var bound = BoxCameraMath.PanBounds(zoom);
                var d = g0 - g1;
                target.x = BoxCameraMath.SoftStep(target.x, d.x, -bound.x, bound.x, BoxCameraMath.PanSoftRange);
                target.z = BoxCameraMath.SoftStep(target.z, d.z, -bound.y, bound.y, BoxCameraMath.PanSoftRange);
                Apply();
            }
            prevMid = mid; prevDist = dist;
        }

        /// <summary>Changes the zoom and moves the target (1 - z1/z0) of the way to the ground under <paramref name="screenPoint"/>.</summary>
        void ZoomAbout(float z1, Vector2 screenPoint)
        {
            float z0 = zoom;
            if (Mathf.Approximately(z0, z1)) return;
            if (GroundAt(screenPoint, out var g)) target = BoxCameraMath.ZoomTarget(target, new Vector3(g.x, target.y, g.z), z0, z1);
            zoom = z1;
            Apply();
        }

        bool GroundAt(Vector2 screenPoint, out Vector3 p)
        {
            p = default;
            return cam != null && BoxCameraMath.GroundPoint(cam.ScreenPointToRay(screenPoint), target.y, out p);
        }

        /// <summary>Whatever the target is past the pan bounds (they shrink as you zoom out) eases back.</summary>
        void SpringPan(float dt)
        {
            var bound = BoxCameraMath.PanBounds(zoom);
            target.x = BoxCameraMath.Spring(target.x, -bound.x, bound.x, dt);
            target.z = BoxCameraMath.Spring(target.z, -bound.y, bound.y, dt);
        }

        public void Apply()
        {
            transform.position = target + Quaternion.Euler(pitch, yaw, 0) * Vector3.back * (FitDistance() * zoom);
            transform.LookAt(target);
        }
    }
}
