using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.View
{
    /// <summary>
    /// The slice's camera on the box (ported from the spike's SpikeOrbit): drag to orbit, scroll or pinch to zoom. The
    /// default distance fits the whole box across the screen and is refitted whenever the aspect changes (the Game view
    /// size lands a few frames into play mode, docs/tooling.md). Input only in play mode.
    /// </summary>
    [ExecuteAlways]
    public class BoxCamera : MonoBehaviour
    {
        public Vector3 target = new(0, 1.0f, 0);
        public float yaw = 28f, pitch = 34f;
        [Tooltip("Zoom: 1 = the whole box fits the width; < 1 is closer.")]
        public float zoom = 1f, minZoom = 0.3f, maxZoom = 1.3f;
        [Tooltip("How much of the screen width the box takes at zoom 1.")]
        public float fill = 0.88f;

        Camera cam;
        float lastPinch;

        public float FitDistance()
        {
            if (cam == null) cam = GetComponent<Camera>();
            float aspect = cam != null ? cam.aspect : 9f / 16f;
            float fov = cam != null ? cam.fieldOfView : 22f;
            float halfH = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, aspect));
            return new Vector3(Land.HX + 0.3f, 1.7f, Land.HZ + 0.3f).magnitude / Mathf.Tan(halfH) * fill;
        }

        void Update()
        {
            if (Application.isPlaying) ReadInput();
            Apply();
        }

        void ReadInput()
        {
            var ts = Touchscreen.current;
            if (ts != null && ts.touches.Count > 1 && ts.touches[0].isInProgress && ts.touches[1].isInProgress)
            {
                float d = Vector2.Distance(ts.touches[0].position.ReadValue(), ts.touches[1].position.ReadValue());
                if (lastPinch > 0) zoom *= lastPinch / Mathf.Max(1f, d);
                lastPinch = d;
            }
            else
            {
                lastPinch = 0;
                var p = Pointer.current;
                if (p != null && p.press.isPressed)
                {
                    var delta = p.delta.ReadValue();
                    yaw += delta.x * 0.25f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.15f, 12f, 70f);
                }
            }
            if (Mouse.current != null) zoom *= 1f - Mouse.current.scroll.ReadValue().y * 0.0008f;
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        public void Apply()
        {
            transform.position = target + Quaternion.Euler(pitch, yaw, 0) * Vector3.back * (FitDistance() * zoom);
            transform.LookAt(target);
        }
    }
}
