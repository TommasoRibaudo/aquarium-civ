using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Orbit-the-pot camera: one-finger drag rotates, pinch zooms, tap inspects (routed to ITappable + EventLog).
    /// Mouse in the Editor: left-drag rotate, wheel zoom, click = tap. Three-finger tap / F1 / backquote toggles debug.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitCamera : MonoBehaviour
    {
        public static OrbitCamera Instance { get; private set; }

        public Vector3 pivot;
        public float minZoom = 0.35f;
        public float rotateDegreesPerPixel = 0.25f;
        public Vector2 pitchLimits = new(15f, 75f);
        public bool inputEnabled = true;

        /// <summary>Tools (e.g. glass wiping) can claim a drag that starts at a screen position; the camera then ignores it.</summary>
        public static Func<Vector2, bool> DragClaim;
        public event Action ThreeFingerTap;

        public Camera Cam { get; private set; }
        public float ZoomT => Mathf.InverseLerp(minZoom * baseDistance, baseDistance, distance);

        float yaw, pitch, distance, baseDistance;
        Vector3 basePivot, focusPivot;
        Vector2 pressPos;
        float pressTime, pressTravel, lastEmptyTap;
        bool pressClaimed, pressing;
        DepthOfField dof;

        void Awake()
        {
            Instance = this;
            Cam = GetComponent<Camera>();
            basePivot = focusPivot = pivot;
            var offset = transform.position - pivot;
            baseDistance = distance = offset.magnitude;
            var e = Quaternion.LookRotation(-offset).eulerAngles;
            pitch = e.x; yaw = e.y;
            var volume = FindAnyObjectByType<Volume>();
            if (volume != null && volume.profile.TryGet(out dof)) dof.focusDistance.overrideState = true; // instance profile, keep focus on the pivot
        }

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            if (inputEnabled)
            {
                if (ETouch.activeTouches.Count > 0) HandleTouches();
                else HandleMouse();
                var kb = Keyboard.current;
                if (kb != null && (kb.f1Key.wasPressedThisFrame || kb.backquoteKey.wasPressedThisFrame)) ThreeFingerTap?.Invoke();
            }
            pivot = Vector3.Lerp(pivot, focusPivot, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            var rot = Quaternion.Euler(pitch, yaw, 0);
            transform.SetPositionAndRotation(pivot - rot * Vector3.forward * distance, rot);
            if (dof != null) dof.focusDistance.value = distance;
        }

        void HandleTouches()
        {
            var touches = ETouch.activeTouches;
            if (touches.Count >= 3)
            {
                foreach (var t in touches)
                    if (t.phase == UnityEngine.InputSystem.TouchPhase.Began) { ThreeFingerTap?.Invoke(); break; }
                pressing = false;
                return;
            }
            if (touches.Count == 2)
            {
                var a = touches[0]; var b = touches[1];
                float cur = Vector2.Distance(a.screenPosition, b.screenPosition);
                float prev = Vector2.Distance(a.screenPosition - a.delta, b.screenPosition - b.delta);
                if (cur > 1f && prev > 1f) Zoom(prev / cur);
                pressing = false; // a pinch is never a tap
                return;
            }
            var t0 = touches[0];
            switch (t0.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began: Press(t0.screenPosition); break;
                case UnityEngine.InputSystem.TouchPhase.Moved: Drag(t0.delta); break;
                case UnityEngine.InputSystem.TouchPhase.Ended: Release(t0.screenPosition); break;
                case UnityEngine.InputSystem.TouchPhase.Canceled: pressing = false; break;
            }
        }

        void HandleMouse()
        {
            var m = Mouse.current;
            if (m == null) return;
            var pos = m.position.ReadValue();
            if (m.leftButton.wasPressedThisFrame) Press(pos);
            else if (m.leftButton.isPressed) Drag(m.delta.ReadValue());
            else if (m.leftButton.wasReleasedThisFrame) Release(pos);
            float scroll = m.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !GuiBlocker.Contains(pos)) Zoom(scroll > 0 ? 0.9f : 1.1f);
        }

        void Press(Vector2 pos)
        {
            pressing = true;
            pressPos = pos;
            pressTime = Time.unscaledTime;
            pressTravel = 0;
            pressClaimed = GuiBlocker.Contains(pos) || (DragClaim?.Invoke(pos) ?? false);
        }

        void Drag(Vector2 delta)
        {
            if (!pressing) return;
            pressTravel += delta.magnitude;
            if (pressClaimed) return;
            yaw += delta.x * rotateDegreesPerPixel;
            pitch = Mathf.Clamp(pitch - delta.y * rotateDegreesPerPixel, pitchLimits.x, pitchLimits.y);
        }

        void Release(Vector2 pos)
        {
            if (!pressing) return;
            pressing = false;
            bool isTap = pressTravel < 15f && Time.unscaledTime - pressTime < 0.4f;
            if (isTap && !GuiBlocker.Contains(pos)) Tap(pos);
        }

        void Zoom(float factor) => distance = Mathf.Clamp(distance * factor, minZoom * baseDistance, baseDistance);

        void Tap(Vector2 pos)
        {
            if (Physics.Raycast(Cam.ScreenPointToRay(pos), out var hit, 500f))
            {
                var lc = LogCategory.Of(hit.collider);
                EventLog.Instance?.Tap(lc?.category ?? "other", lc?.id);
                var tappable = hit.collider.GetComponentInParent<ITappable>();
                tappable?.OnTap(hit);
                if (lc != null && (lc.category == "building" || lc.category == "citizen" || lc.category == "notable"))
                    focusPivot = new Vector3(hit.point.x, basePivot.y, hit.point.z);
                if (lc == null || lc.category != "ground") return;
            }
            else EventLog.Instance?.Tap("empty");
            // Double-tap on bare ground or outside the box resets the view.
            if (Time.unscaledTime - lastEmptyTap < 0.4f) ResetView();
            lastEmptyTap = Time.unscaledTime;
        }

        public void ResetView()
        {
            focusPivot = basePivot;
            distance = baseDistance;
        }
    }

    /// <summary>IMGUI panels register their screen rects each frame so world input ignores touches on them.</summary>
    public static class GuiBlocker
    {
        static readonly List<Rect> rects = new();
        static int frame = -1;

        /// <summary>Register a rect in IMGUI coordinates (top-left origin, after any GUI.matrix scaling has been applied by the caller).</summary>
        public static void Add(Rect screenRect)
        {
            if (frame != Time.frameCount) { rects.Clear(); frame = Time.frameCount; }
            rects.Add(screenRect);
        }

        /// <summary>Input System position (bottom-left origin).</summary>
        public static bool Contains(Vector2 pointerPos)
        {
            if (frame < Time.frameCount - 1) return false; // stale: nothing drawn recently
            var p = new Vector2(pointerPos.x, Screen.height - pointerPos.y);
            foreach (var r in rects) if (r.Contains(p)) return true;
            return false;
        }
    }
}
