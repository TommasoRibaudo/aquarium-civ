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
    /// Orbit-the-pot camera: one-finger drag rotates, two fingers rotate + pinch zoom, tap inspects (routed to
    /// ITappable + EventLog). A drag tool (glass wiping) can take one-finger drags; two fingers always rotate.
    /// Mouse in the Editor: left-drag rotate, right-drag rotate (bypasses the drag tool), wheel zoom, click = tap.
    /// Three-finger tap / F1 / backquote toggles debug.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitCamera : MonoBehaviour
    {
        public static OrbitCamera Instance { get; private set; }

        public Vector3 pivot;
        public float minZoom = 0.35f;
        public float rotateDegreesPerPixel = 0.25f;
        public Vector2 pitchLimits = new(15f, 75f);
        [Tooltip("Aspect (w/h) the scene's camera distance was framed for; narrower screens pull back to keep the tank's width.")]
        public float designAspect = 9f / 16f;
        public bool inputEnabled = true;

        /// <summary>A tool (e.g. glass wiping) that can claim one-finger drags; the camera then doesn't rotate.</summary>
        public static IDragTool DragTool;
        /// <summary>True while a drag tool's EndDrag runs because the press was cancelled (a second finger, the OS), not lifted.</summary>
        public static bool DragCancelled { get; private set; }
        /// <summary>Gets first say on a tap (screen position, what the world raycast hit or null). True = handled.</summary>
        public static Func<Vector2, Collider, bool> TapClaim;
        public event Action ThreeFingerTap;

        public Camera Cam { get; private set; }
        /// <summary>The pivot the camera returns to on ResetView.</summary>
        public Vector3 BasePivot => basePivot;
        public float ZoomT => Mathf.InverseLerp(minZoom * baseDistance, baseDistance, distance);

        float yaw, pitch, distance, baseDistance, designDistance, fittedAspect;
        Vector3 basePivot, focusPivot;
        Vector2 pressPos;
        float pressTime, pressTravel, lastEmptyTap;
        bool pressClaimed, toolDragging, pressing;
        DepthOfField dof;

        void Awake()
        {
            Instance = this;
            Cam = GetComponent<Camera>();
            basePivot = focusPivot = pivot;
            var offset = transform.position - pivot;
            designDistance = baseDistance = distance = offset.magnitude;
            FitAspect();
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
            if (!Mathf.Approximately(Cam.aspect, fittedAspect)) FitAspect();
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
                Rotate((a.delta + b.delta) * 0.5f);
                CancelPress(); // a pinch is never a tap or a wipe
                return;
            }
            var t0 = touches[0];
            switch (t0.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began: Press(t0.screenPosition); break;
                case UnityEngine.InputSystem.TouchPhase.Moved: Drag(t0.screenPosition, t0.delta); break;
                case UnityEngine.InputSystem.TouchPhase.Ended: Release(t0.screenPosition); break;
                case UnityEngine.InputSystem.TouchPhase.Canceled: CancelPress(); break;
            }
        }

        void HandleMouse()
        {
            var m = Mouse.current;
            if (m == null) return;
            var pos = m.position.ReadValue();
            if (m.leftButton.wasPressedThisFrame) Press(pos);
            else if (m.leftButton.isPressed) Drag(pos, m.delta.ReadValue());
            else if (m.leftButton.wasReleasedThisFrame) Release(pos);
            if (m.rightButton.isPressed && !m.leftButton.isPressed) Rotate(m.delta.ReadValue());
            float scroll = m.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !GuiBlocker.Contains(pos)) Zoom(scroll > 0 ? 0.9f : 1.1f);
        }

        void Press(Vector2 pos)
        {
            pressing = true;
            pressPos = pos;
            pressTime = Time.unscaledTime;
            pressTravel = 0;
            pressClaimed = GuiBlocker.Contains(pos);
            toolDragging = !pressClaimed && DragTool != null && DragTool.BeginDrag(pos);
            pressClaimed |= toolDragging;
        }

        void Drag(Vector2 pos, Vector2 delta)
        {
            if (!pressing) return;
            pressTravel += delta.magnitude;
            if (toolDragging) DragTool.Drag(pos, delta);
            if (pressClaimed) return;
            Rotate(delta);
        }

        void Rotate(Vector2 delta)
        {
            yaw += delta.x * rotateDegreesPerPixel;
            pitch = Mathf.Clamp(pitch - delta.y * rotateDegreesPerPixel, pitchLimits.x, pitchLimits.y);
        }

        void Release(Vector2 pos)
        {
            if (!pressing) return;
            pressing = false;
            if (toolDragging) { toolDragging = false; DragTool.EndDrag(pos); }
            bool isTap = pressTravel < 15f && Time.unscaledTime - pressTime < 0.4f;
            if (isTap && !GuiBlocker.Contains(pos)) Tap(pos);
        }

        void CancelPress()
        {
            if (toolDragging)
            {
                toolDragging = false;
                DragCancelled = true;
                try { DragTool.EndDrag(pressPos); }
                finally { DragCancelled = false; }
            }
            pressing = false;
        }

        void FitAspect()
        {
            fittedAspect = Cam.aspect;
            float old = baseDistance;
            baseDistance = designDistance * Mathf.Max(1f, designAspect / Mathf.Max(fittedAspect, 0.01f));
            distance *= baseDistance / old;
        }

        void Zoom(float factor) => distance = Mathf.Clamp(distance * factor, minZoom * baseDistance, baseDistance);

        void Tap(Vector2 pos)
        {
            bool hitWorld = Physics.Raycast(Cam.ScreenPointToRay(pos), out var hit, 500f);
            if (TapClaim != null && TapClaim(pos, hitWorld ? hit.collider : null)) return;
            if (hitWorld)
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

        /// <summary>Fly the camera to look at a world point (chronicle "show me"), zoomed in to zoomT (0 = closest, 1 = default).</summary>
        public void FocusOn(Vector3 world, float zoomT = 0.55f)
        {
            focusPivot = new Vector3(world.x, basePivot.y, world.z);
            distance = Mathf.Lerp(minZoom * baseDistance, baseDistance, Mathf.Clamp01(zoomT));
        }

        /// <summary>Pull back for a wider view (P6: an annex box or the shelf), as a multiple of the default distance.</summary>
        public void SetWide(float factor, Vector3? pivotOverride = null)
        {
            if (pivotOverride.HasValue) basePivot = pivotOverride.Value;
            focusPivot = basePivot;
            baseDistance = designDistance * Mathf.Max(1f, designAspect / Mathf.Max(fittedAspect, 0.01f)) * factor;
            distance = baseDistance;
        }

        public void ResetView()
        {
            focusPivot = basePivot;
            distance = baseDistance;
        }
    }

    /// <summary>A one-finger drag tool. BeginDrag returns true to take the drag (the camera won't rotate).</summary>
    public interface IDragTool
    {
        bool BeginDrag(Vector2 screenPos);
        void Drag(Vector2 screenPos, Vector2 delta);
        void EndDrag(Vector2 screenPos);
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
