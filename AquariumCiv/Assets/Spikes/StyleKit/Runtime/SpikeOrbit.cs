using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.Spikes.StyleKit
{
    /// <summary>Spike camera: drag to orbit around the box, scroll or pinch to zoom. Play mode only.</summary>
    public class SpikeOrbit : MonoBehaviour
    {
        public Vector3 target;
        public float distance = 20f, yaw = 28f, pitch = 34f, minDistance = 9f, maxDistance = 26f;
        float lastPinch;

        void Update()
        {
            if (!Application.isPlaying) return;
            var ts = Touchscreen.current;
            if (ts != null && ts.touches.Count > 1 && ts.touches[0].isInProgress && ts.touches[1].isInProgress)
            {
                float d = Vector2.Distance(ts.touches[0].position.ReadValue(), ts.touches[1].position.ReadValue());
                if (lastPinch > 0) distance *= lastPinch / Mathf.Max(1f, d);
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
            if (Mouse.current != null) distance *= 1f - Mouse.current.scroll.ReadValue().y * 0.0008f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            Apply();
        }

        public void Apply()
        {
            transform.position = target + Quaternion.Euler(pitch, yaw, 0) * Vector3.back * distance;
            transform.LookAt(target);
        }
    }
}
