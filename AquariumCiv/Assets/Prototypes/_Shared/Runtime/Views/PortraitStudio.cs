using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Head-and-shoulders portraits of the KayKit characters, rendered once per look into a small texture by an
    /// off-screen camera, so chronicle cards and the Book of Ages show the same face that walks the village.
    /// Draw() puts it on a disc tinted by family, so people from one family read as one.
    /// </summary>
    public class PortraitStudio : MonoBehaviour
    {
        public GameObject[] characterPrefabs;
        public AnimationClip idleClip;
        public int size = 256;
        [Tooltip("Far from the tank so neither camera sees the other's subject.")] public Vector3 stage = new(400, -400, 0);

        public static PortraitStudio Instance { get; private set; }

        readonly Dictionary<int, RenderTexture> portraits = new();
        readonly Queue<int> pending = new();
        Camera cam;
        static Texture2D disc;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            foreach (var rt in portraits.Values) if (rt != null) rt.Release();
        }

        /// <summary>The portrait for a look index (renders within a frame or two the first time).</summary>
        public Texture Get(int look)
        {
            if (characterPrefabs == null || characterPrefabs.Length == 0) return null;
            look = Mathf.Abs(look) % characterPrefabs.Length;
            if (portraits.TryGetValue(look, out var rt)) return rt;
            rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { name = $"Portrait_{look}", antiAliasing = 4 };
            rt.Create();
            portraits[look] = rt;
            pending.Enqueue(look);
            if (pending.Count == 1) StartCoroutine(RenderQueue());
            return rt;
        }

        public Texture Get(Notable n) => n == null ? null : Get(NotableView.Look(n, characterPrefabs.Length));

        IEnumerator RenderQueue()
        {
            if (cam == null) cam = MakeCamera();
            while (pending.Count > 0)
            {
                int look = pending.Peek();
                var go = Instantiate(characterPrefabs[look], stage, Quaternion.Euler(0, 180f, 0));
                go.name = "PortraitSitter";
                if (idleClip != null) idleClip.SampleAnimation(go, idleClip.length * 0.3f);
                var b = new Bounds(stage, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                float h = Mathf.Max(b.size.y, 0.01f);
                var focus = new Vector3(b.center.x, b.min.y + h * 0.74f, b.center.z);
                cam.transform.position = focus + new Vector3(0.18f * h, 0.06f * h, -1.9f * h); // a little off-axis, like a painted portrait
                cam.transform.LookAt(focus);
                cam.farClipPlane = 6f * h;
                cam.targetTexture = portraits[look];
                cam.enabled = true;
                yield return null; // the camera renders this frame
                yield return new WaitForEndOfFrame();
                cam.enabled = false;
                Destroy(go);
                pending.Dequeue();
            }
        }

        Camera MakeCamera()
        {
            var go = new GameObject("PortraitCamera");
            go.transform.SetParent(transform);
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0, 0, 0, 0);
            c.fieldOfView = 24f;
            c.nearClipPlane = 0.01f;
            c.enabled = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            return c;
        }

        /// <summary>A family-tinted disc with the portrait on it. Safe to call from any OnGUI.</summary>
        public static void Draw(Rect r, Notable n, bool dimmed = false)
        {
            if (disc == null) disc = MakeDisc(128);
            var c = GUI.color;
            var tint = FamilyColor(n?.family);
            if (dimmed) tint = Color.Lerp(tint, new Color(0.55f, 0.53f, 0.5f), 0.8f);
            GUI.color = tint;
            GUI.DrawTexture(r, disc);
            GUI.color = dimmed ? new Color(0.5f, 0.48f, 0.45f, 0.75f) : Color.white; // the dead read as faded at a glance
            var tex = Instance != null && n != null ? Instance.Get(n) : null;
            if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            GUI.color = c;
        }

        /// <summary>A soft pastel per family name (stable across runs).</summary>
        public static Color FamilyColor(string family)
        {
            if (string.IsNullOrEmpty(family)) return new Color(0.6f, 0.75f, 0.78f);
            uint h = 2166136261;
            foreach (char ch in family) { h ^= ch; h *= 16777619; }
            return Color.HSVToRGB((h % 360) / 360f, 0.35f, 0.92f);
        }

        static Texture2D MakeDisc(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(n / 2f - r);
                float shade = Mathf.Lerp(1f, 0.82f, Mathf.Clamp01((y / (float)n))); // lighter at the bottom, like a lit backdrop
                float rim = r > n / 2f - 5 ? 0.7f : shade;
                t.SetPixel(x, y, new Color(rim, rim, rim, a));
            }
            t.Apply();
            return t;
        }
    }
}
