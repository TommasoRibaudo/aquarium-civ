using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// Tester run 9: a tap on bare land through the open top used to do nothing at all. The ground answers with one soft ring spreading from the finger and
    /// a small sound: no card, no words, and nothing for the people to react to. One mesh, made once; a new tap starts it again.
    /// </summary>
    public sealed class GroundRipple
    {
        /// <summary>The ring's inside, bright line and outside at full size (box units), how long it runs (real seconds), and its peak.</summary>
        const float Inner = 0.12f, Mid = 0.19f, Outer = 0.26f, Seconds = 0.55f, Peak = 0.8f, Lift = 0.03f, StartScale = 0.3f;

        GameObject go;
        Mesh mesh;
        Material material;
        MeshRenderer renderer;
        MaterialPropertyBlock block;
        float age = float.MaxValue;

        /// <summary>Ripples started so far (tests, the debug panel).</summary>
        public int Count { get; private set; }
        public bool Up => go != null && go.activeSelf;

        /// <summary>A ripple on the land at (x, z), box space.</summary>
        public void Start(BoxView box, float x, float z)
        {
            if (box == null) return;
            if (go == null) Make();
            go.transform.SetParent(box.transform, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(x, box.GroundHeight(x, z) + Lift, z), Quaternion.identity);
            go.SetActive(true);
            age = 0f;
            Count++;
            Step(0f);
        }

        /// <summary>One frame.</summary>
        public void Step(float dt)
        {
            if (go == null || !go.activeSelf) return;
            age += dt;
            float k = age / Seconds;
            if (k >= 1f) { go.SetActive(false); return; }
            float eased = 1f - (1f - k) * (1f - k);
            go.transform.localScale = Vector3.one * Mathf.Lerp(StartScale, 1f, eased);
            block.SetColor("_Color", new Color(1f, 1f, 1f, 1f - k));
            renderer.SetPropertyBlock(block);
        }

        void Make()
        {
            go = new GameObject("GroundRipple") { hideFlags = HideFlags.DontSave };
            mesh = SpeakerRing.RingMesh("GroundRipple", Inner, Mid, Outer, new Color(1f, 0.97f, 0.86f), Peak);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            // The land's facets stand up through a flat ring and cut it: it draws over them, and only ever lands where nothing stands.
            material = new Material(ShapingView.LoadFx()) { name = "GroundRipple", hideFlags = HideFlags.DontSave };
            material.SetFloat("_ZTest", (float)CompareFunction.Always);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }

        /// <summary>The owner is going: nothing outlives it.</summary>
        public void Dispose()
        {
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
            go = null; mesh = null; material = null;
        }
    }
}
