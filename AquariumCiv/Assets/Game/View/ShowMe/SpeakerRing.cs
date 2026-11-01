using AquariumCiv.Life;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// #204: a soft ring on the ground under whoever has the word, so a line can't be anyone's but theirs even in a crowd or with the bubble pushed
    /// off them. One mesh, made once, moved under the doll; <see cref="ShowMeDirector"/> says who.
    /// </summary>
    public sealed class SpeakerRing
    {
        const int Sides = 32;
        /// <summary>Inside clear, the bright line, outside clear (box units), how far over the ground, and how long it takes to fade (real seconds).</summary>
        const float Inner = 0.075f, Mid = 0.125f, Outer = 0.18f, Lift = 0.025f, FadeSeconds = 0.18f, Peak = 0.8f;

        GameObject go;
        Mesh mesh;
        MeshRenderer renderer;
        MaterialPropertyBlock block;
        float alpha;
        Vector3 at;

        /// <summary>The ring is under someone now, and where (tests).</summary>
        public bool Up { get; private set; }
        public Vector3 At => at;

        /// <summary>One frame: under <paramref name="who"/>'s feet, or fading out with nobody to be under.</summary>
        public void Draw(PeopleView pv, PersonRef who, float dt)
        {
            Up = !who.IsNone && pv != null && pv.TryGetDollFeet(who, out at);
            alpha = Mathf.MoveTowards(alpha, Up ? 1f : 0f, dt / FadeSeconds);
            if (alpha <= 0f) { if (go != null && go.activeSelf) go.SetActive(false); return; }
            if (go == null) Make();
            if (!go.activeSelf) go.SetActive(true);
            go.transform.position = at + Vector3.up * Lift;
            block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            renderer.SetPropertyBlock(block);
        }

        void Make()
        {
            go = new GameObject("SpeakerRing") { hideFlags = HideFlags.DontSave };
            mesh = new Mesh { name = "SpeakerRing", hideFlags = HideFlags.DontSave };
            var v = new Vector3[Sides * 3];
            var c = new Color[Sides * 3];
            var t = new int[Sides * 12];
            var col = new Color(1f, 0.95f, 0.78f, 0f);
            float[] radii = { Inner, Mid, Outer };
            float[] alphas = { 0f, Peak, 0f };
            int n = 0;
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < Sides; i++)
                {
                    float a = i * Mathf.PI * 2f / Sides;
                    v[r * Sides + i] = new Vector3(Mathf.Cos(a) * radii[r], 0f, Mathf.Sin(a) * radii[r]);
                    c[r * Sides + i] = new Color(col.r, col.g, col.b, alphas[r]);
                }
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < Sides; i++)
                {
                    int p = r * Sides + i, q = r * Sides + (i + 1) % Sides, s = (r + 1) * Sides + i, u = (r + 1) * Sides + (i + 1) % Sides;
                    t[n++] = p; t[n++] = s; t[n++] = q;
                    t[n++] = q; t[n++] = s; t[n++] = u;
                }
            mesh.vertices = v;
            mesh.colors = c;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ShapingView.LoadFx();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }

        /// <summary>The director is going: nothing outlives it.</summary>
        public void Dispose()
        {
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            go = null; mesh = null;
        }
    }
}
