using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// Tester run 1 #4 (follow-up): a safety net so a baked tall-grass clump (or reed) never stands between the camera and a hunter.
    /// While a hunter stalks, strikes or holds up the catch, <see cref="Add"/> is called once per frame with his feet and how far the
    /// grass has gone (eased by the caller); <see cref="Flush"/> uploads up to <see cref="Max"/> points to the Stylized shader's globals
    /// (<c>_AC_HidePos/_AC_HideDir/_AC_HideCount</c>). Tall-grass and reed vertices carry the marker <see cref="TallSway"/> (1 + the wind weight)
    /// in UV0.x, so short grass, bushes and trees never move. All the maths is in the vertex shader (<c>HideNear</c>): grass within
    /// <see cref="Radius"/> on the camera's side of a point flattens to the ground.
    /// </summary>
    public static class GrassHide
    {
        public const int Max = 4;
        /// <summary>Tall-grass tufts' UV0.x: 1 (wind weight) + 1 (the marker). Reeds use 0.8 + 1.</summary>
        public const float TallSway = 2f;
        public const float Radius = 0.25f;

        static readonly int PosId = Shader.PropertyToID("_AC_HidePos"), DirId = Shader.PropertyToID("_AC_HideDir"), CountId = Shader.PropertyToID("_AC_HideCount");
        static readonly Vector4[] pos = new Vector4[Max], dir = new Vector4[Max];
        static int count;

        public static int Count => count;
        public static Vector3 First => new Vector3(pos[0].x, pos[0].y, pos[0].z);
        public static string Describe() => count == 0 ? "none" : $"{pos[0]} dir {dir[0]}";

        /// <summary>Debug / shots: the net off (the count is still tracked, nothing reaches the shader).</summary>
        public static bool Off;

        public static void Begin() => count = 0;

        /// <summary>A world-space foot point and how hidden the grass near it is (0..1).</summary>
        public static void Add(Vector3 feet, float amount)
        {
            if (count >= Max || amount <= 0.001f) return;
            var cam = Camera.main;
            var toCam = cam != null ? cam.transform.position - feet : Vector3.back;
            var h = new Vector2(toCam.x, toCam.z);
            h = h.sqrMagnitude > 1e-6f ? h.normalized : Vector2.up;
            pos[count] = new Vector4(feet.x, feet.y, feet.z, Mathf.Clamp01(amount));
            dir[count] = new Vector4(h.x, h.y, Radius, 0f);
            count++;
        }

        public static void Flush()
        {
            if (Off) return; // shots set the globals by hand
            Shader.SetGlobalFloat(CountId, count);
            if (count > 0)
            {
                Shader.SetGlobalVectorArray(PosId, pos);
                Shader.SetGlobalVectorArray(DirId, dir);
            }
        }
    }
}
