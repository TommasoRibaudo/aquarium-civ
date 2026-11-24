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

        GameObject go, back;
        Mesh mesh, backMesh;
        Material material, backMaterial;
        MeshRenderer renderer, backRenderer;
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
            // S-ritual (the #694 note: a ring over the people at the fire at night): the half toward the camera is never cut (tester run 9: a
            // speaker behind a tree); the far half keeps its depth test, so the speaker's own body hides it instead of the ring drawing over them
            var cam = Camera.main;
            if (cam != null)
            {
                var toCam = cam.transform.position - at; toCam.y = 0f;
                if (toCam.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
            block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            renderer.SetPropertyBlock(block);
            backRenderer.SetPropertyBlock(block);
        }

        void Make()
        {
            go = new GameObject("SpeakerRing") { hideFlags = HideFlags.DontSave };
            mesh = RingMesh("SpeakerRing", Inner, Mid, Outer, new Color(1f, 0.95f, 0.78f), Peak, 0f);
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
            back = new GameObject("SpeakerRing (far half)") { hideFlags = HideFlags.DontSave };
            back.transform.SetParent(go.transform, false);
            backMesh = RingMesh("SpeakerRingFar", Inner, Mid, Outer, new Color(1f, 0.95f, 0.78f), Peak, Mathf.PI);
            back.AddComponent<MeshFilter>().sharedMesh = backMesh;
            backRenderer = back.AddComponent<MeshRenderer>();
            backMaterial = new Material(ShapingView.LoadFx()) { name = "SpeakerRingFar", hideFlags = HideFlags.DontSave, renderQueue = RingQueue };
            backMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            backRenderer.sharedMaterial = backMaterial;
            backRenderer.shadowCastingMode = ShadowCastingMode.Off;
            backRenderer.receiveShadows = false;
        }

        /// <summary>A flat ring on the ground: clear inside, bright at <paramref name="mid"/>, clear outside.</summary>
        public static Mesh RingMesh(string name, float inner, float mid, float outer, Color col, float peak) => RingMesh(name, inner, mid, outer, col, peak, -1f);

        /// <summary>The ring, or (<paramref name="from"/> ≥ 0) the half of it from that angle on (radians; 0 = the half on local +z).</summary>
        public static Mesh RingMesh(string name, float inner, float mid, float outer, Color col, float peak, float from)
        {
            bool half = from >= 0f;
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
                    float a = half ? from + i * Mathf.PI / (Sides - 1) : i * Mathf.PI * 2f / Sides;
                    v[r * Sides + i] = new Vector3(Mathf.Cos(a) * radii[r], 0f, Mathf.Sin(a) * radii[r]);
                    c[r * Sides + i] = new Color(col.r, col.g, col.b, alphas[r]);
                }
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < (half ? Sides - 1 : Sides); i++)
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
            if (backMesh != null) Object.Destroy(backMesh);
            if (backMaterial != null) Object.Destroy(backMaterial);
            go = null; mesh = null; material = null; back = null; backMesh = null; backMaterial = null;
        }
    }
}
