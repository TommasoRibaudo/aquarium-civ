using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Look-sampler spike (docs/design/spikes/look-sampler.md): instead of one ray from the screen centre, share each sample
    /// between every tagged thing on screen, weighted by how close it is to where the player is looking (the centre,
    /// pulled toward their last touch for a few seconds) and by its size on screen. Off by default
    /// (<see cref="EventLog.weightedLook"/>): the prototypes' look-time stays the centre ray they shipped with.
    /// </summary>
    public static class LookSampler
    {
        public struct Target
        {
            public string category;
            public Vector3 viewport; // x, y in 0..1, z = depth (behind the camera if <= 0)
            public float radius;     // on-screen radius in viewport units
        }

        /// <summary>Shares per category, summing to 1 ("empty": 1 when nothing tagged is on screen).</summary>
        public static Dictionary<string, float> Weigh(IReadOnlyList<Target> targets, Vector2 focus, float sigma = 0.22f)
        {
            var shares = new Dictionary<string, float>();
            float total = 0;
            foreach (var t in targets)
            {
                if (t.viewport.z <= 0 || t.viewport.x < 0 || t.viewport.x > 1 || t.viewport.y < 0 || t.viewport.y > 1) continue;
                float d2 = ((Vector2)t.viewport - focus).sqrMagnitude;
                float w = Mathf.Exp(-d2 / (2 * sigma * sigma)) * Mathf.Clamp(t.radius, 0.005f, 0.25f);
                if (w <= 1e-6f) continue;
                shares[t.category] = shares.GetValueOrDefault(t.category) + w;
                total += w;
            }
            if (total <= 0) return new Dictionary<string, float> { ["empty"] = 1f };
            var keys = new List<string>(shares.Keys);
            foreach (var k in keys) shares[k] /= total;
            return shares;
        }

        /// <summary>The look focus: the screen centre, pulled toward the last touch, fading back over <paramref name="fade"/> s.</summary>
        public static Vector2 Focus(Vector2? lastTouchViewport, float secondsSinceTouch, float fade = 3f)
        {
            var centre = new Vector2(0.5f, 0.5f);
            if (lastTouchViewport == null || secondsSinceTouch >= fade) return centre;
            return Vector2.Lerp(lastTouchViewport.Value, centre, secondsSinceTouch / fade);
        }

        static readonly List<Target> Scratch = new();

        /// <summary>Every active <see cref="LogCategory"/> with a renderer, projected into this camera's viewport.</summary>
        public static IReadOnlyList<Target> Collect(Camera cam)
        {
            Scratch.Clear();
            foreach (var lc in Object.FindObjectsByType<LogCategory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var r = lc.GetComponentInChildren<Renderer>();
                if (r == null || !r.isVisible) continue;
                var b = r.bounds;
                var v = cam.WorldToViewportPoint(b.center);
                var edge = cam.WorldToViewportPoint(b.center + cam.transform.right * b.extents.magnitude);
                Scratch.Add(new Target { category = lc.category, viewport = v, radius = Vector2.Distance(v, edge) });
            }
            return Scratch;
        }
    }
}
