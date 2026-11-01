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
        const int Sides = 32, RingQueue = 3005;
        /// <summary>Inside clear, the bright line, outside clear (box units), how far over the ground, and how long it takes to fade (real seconds).</summary>
        const float Inner = 0.075f, Mid = 0.125f, Outer = 0.18f, Lift = 0.025f, FadeSeconds = 0.18f, Peak = 0.8f;

        GameObject go;
        Mesh mesh;
        Material material;
        MeshRenderer renderer;
        MaterialPropertyBlock block;
        float alpha;
        Vector3 at;

        /// <summary>The ring is under someone now, and where (tests).</summary>
        public bool Up { get; private set; }
        public Vector3 At => at;
        public MeshRenderer Renderer => renderer;

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
            mesh = RingMesh("SpeakerRing", Inner, Mid, Outer, new Color(1f, 0.95f, 0.78f), Peak);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            // Tester run 9: a speaker walks behind trees for seconds at a time. The ring says who has the word, so nothing cuts it, like the marks. Its own copy of
            // the material: everything else in the box keeps its depth test.
            material = new Material(ShapingView.LoadFx()) { name = "SpeakerRing", hideFlags = HideFlags.DontSave, renderQueue = RingQueue };
            material.SetFloat("_ZTest", (float)CompareFunction.Always);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }

        /// <summary>A flat ring on the ground: clear inside, bright at <paramref name="mid"/>, clear outside.</summary>
        public static Mesh RingMesh(string name, float inner, float mid, float outer, Color col, float peak)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            var v = new Vector3[Sides * 3];
            var c = new Color[Sides * 3];
            var t = new int[Sides * 12];
            float[] radii = { inner, mid, outer };
            float[] alphas = { 0f, peak, 0f };
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
            return mesh;
        }

        /// <summary>The director is going: nothing outlives it.</summary>
        public void Dispose()
        {
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
            go = null; mesh = null; material = null;
        }
    }
}
