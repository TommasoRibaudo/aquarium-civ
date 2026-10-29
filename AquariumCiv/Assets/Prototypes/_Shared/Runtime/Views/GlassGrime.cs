using System;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Paintable grime on the terrarium glass: one CPU mask per pane (0 = clean, 1 = filthy), uploaded to the
    /// GlassGrime shader. The sim's grime value stays the source of truth over time: when it rises (time passing)
    /// the masks grow, unevenly; when it drops outside a wipe (debug clean) they fade. Wipes paint the masks and
    /// write the new mean back to the sim.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GlassGrime : MonoBehaviour
    {
        public const int GlassLayer = 2; // Ignore Raycast: world taps and look sampling see through the glass

        public class Pane
        {
            public Renderer renderer;
            public Material material;
            public Texture2D texture;
            public float[] grime, weight;
            public byte[] bytes;
            public int width, height;
            public Vector2 size; // world units (u, v)
            public bool zAxis, dirty;
            public double sum;
            public float glint, peakMean;
            public float Mean => (float)(sum / grime.Length);
        }

        public Renderer[] glassRenderers;
        public Material grimeMaterial;
        public float texelsPerUnit = 32f;

        public Pane[] Panes { get; private set; }
        public static GlassGrime Instance { get; private set; }
        /// <summary>A pane went from dirty to sparkling (pane, world center).</summary>
        public event Action<Pane, Vector3> PaneCleared;

        float synced;
        int generation;
        static Texture2D noise;

        public float Mean
        {
            get
            {
                double s = 0; long n = 0;
                foreach (var p in Panes) { s += p.sum; n += p.grime.Length; }
                return n == 0 ? 0f : (float)(s / n);
            }
        }

        void Awake()
        {
            Instance = this;
            noise ??= MakeNoiseTexture(256);
            Panes = new Pane[glassRenderers.Length];
            for (int i = 0; i < glassRenderers.Length; i++) Panes[i] = CreatePane(glassRenderers[i], i);
        }

        void Start()
        {
            generation = SimClock.Instance.Generation;
            ResetTo(SimClock.Instance.Sim.State.grime);
        }

        Pane CreatePane(Renderer r, int index)
        {
            var scale = r.transform.lossyScale;
            bool zAxis = scale.x < scale.z; // thin along x: an east/west pane, mapped on (z, y)
            var size = new Vector2(zAxis ? scale.z : scale.x, scale.y);
            int w = Mathf.Clamp(Mathf.RoundToInt(size.x * texelsPerUnit), 8, 512);
            int h = Mathf.Clamp(Mathf.RoundToInt(size.y * texelsPerUnit), 8, 512);
            var p = new Pane
            {
                renderer = r, width = w, height = h, size = size, zAxis = zAxis,
                grime = new float[w * h], weight = new float[w * h], bytes = new byte[w * h],
                texture = new Texture2D(w, h, TextureFormat.R8, false, true)
                    { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = $"GrimeMask_{r.name}" },
            };
            // Growth weights: some spots fog up faster (noise), and the bottom of the glass gets dirtier first.
            float ox = index * 13.7f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w * size.x, v = y / (float)h;
                float n = Mathf.PerlinNoise(ox + u * 1.3f, v * size.y * 1.3f);
                p.weight[y * w + x] = Mathf.Lerp(0.55f, 1.45f, n) * (1.2f - 0.4f * v);
            }
            p.material = new Material(grimeMaterial) { name = $"GlassGrime_{r.name}" };
            p.material.SetTexture("_MaskTex", p.texture);
            p.material.SetTexture("_NoiseTex", noise);
            p.material.SetFloat("_Axis", zAxis ? 1f : 0f);
            p.material.SetVector("_PaneSize", size);
            r.sharedMaterial = p.material;

            r.gameObject.layer = GlassLayer;
            if (!r.TryGetComponent<BoxCollider>(out _)) r.gameObject.AddComponent<BoxCollider>();
            return p;
        }

        void Update()
        {
            var clock = SimClock.Instance;
            if (clock.Generation != generation) { generation = clock.Generation; ResetTo(clock.Sim.State.grime); }
            float g = clock.Sim.State.grime;
            if (g > synced + 0.002f) Grow(g - synced);
            else if (g < synced - 0.002f) Fade(g);

            foreach (var p in Panes)
            {
                if (p.glint > 0) { p.glint = Mathf.Max(0, p.glint - Time.unscaledDeltaTime * 0.8f); p.material.SetFloat("_Glint", p.glint); }
            }
        }

        void LateUpdate()
        {
            foreach (var p in Panes)
            {
                if (!p.dirty) continue;
                p.dirty = false;
                for (int i = 0; i < p.grime.Length; i++) p.bytes[i] = (byte)(p.grime[i] * 255f + 0.5f);
                p.texture.SetPixelData(p.bytes, 0);
                p.texture.Apply(false);
            }
        }

        // ---------- sync with the sim ----------

        /// <summary>Fresh, even grime at this level (new world, lab start).</summary>
        public void ResetTo(float level)
        {
            foreach (var p in Panes)
            {
                p.sum = 0;
                for (int i = 0; i < p.grime.Length; i++) p.sum += p.grime[i] = Mathf.Clamp01(level * p.weight[i]);
                p.dirty = true;
                p.peakMean = p.Mean;
            }
            WriteBack();
        }

        void Grow(float delta)
        {
            foreach (var p in Panes)
            {
                p.sum = 0;
                for (int i = 0; i < p.grime.Length; i++) p.sum += p.grime[i] = Mathf.Min(1f, p.grime[i] + delta * p.weight[i]);
                p.dirty = true;
                p.peakMean = Mathf.Max(p.peakMean, p.Mean);
            }
            WriteBack();
        }

        void Fade(float target)
        {
            float k = synced > 0.0001f ? target / synced : 0f;
            foreach (var p in Panes)
            {
                p.sum = 0;
                for (int i = 0; i < p.grime.Length; i++) p.sum += p.grime[i] = target <= 0.001f ? 0f : p.grime[i] * k;
                p.dirty = true;
            }
            synced = target;
        }

        /// <summary>Push the masks' mean into the sim (after painting), so mess level and logs match what you see.</summary>
        public void WriteBack()
        {
            var sim = SimClock.Instance.Sim;
            synced = Mean;
            sim.State.grime = synced;
        }

        // ---------- painting ----------

        /// <summary>Raycast the glass only. uv is the pane's mask coordinate (0..1).</summary>
        public bool Raycast(Ray ray, out Pane pane, out Vector2 uv, out Vector3 point)
        {
            pane = null; uv = default; point = default;
            if (!Physics.Raycast(ray, out var hit, 500f, 1 << GlassLayer, QueryTriggerInteraction.Collide)) return false;
            foreach (var p in Panes)
            {
                if (p.renderer.transform != hit.collider.transform) continue;
                pane = p;
                point = hit.point;
                var os = p.renderer.transform.InverseTransformPoint(hit.point);
                uv = new Vector2((p.zAxis ? os.z : os.x) + 0.5f, os.y + 0.5f);
                return true;
            }
            return false;
        }

        /// <summary>Average grime in a disc (world radius) around uv.</summary>
        public float GrimeAt(Pane p, Vector2 uv, float radius)
        {
            float sum = 0; int n = 0;
            ForDisc(p, uv, radius, (i, f) => { sum += p.grime[i]; n++; });
            return n == 0 ? 0 : sum / n;
        }

        /// <summary>Wipe a disc. strength = fraction removed at the center. Returns grime removed, as a share of all glass.</summary>
        public float Brush(Pane p, Vector2 uv, float radius, float strength)
        {
            double removed = 0;
            ForDisc(p, uv, radius, (i, f) =>
            {
                float before = p.grime[i];
                float after = before * (1f - strength * f);
                if (after < 0.02f) after = 0f;
                p.grime[i] = after;
                removed += before - after;
            });
            if (removed <= 0) return 0;
            p.sum -= removed;
            p.dirty = true;
            float share = (float)(removed / TotalTexels);
            WriteBack();
            CheckCleared(p);
            return share;
        }

        /// <summary>Wipe along a segment on one pane (stamps spaced by a third of the radius).</summary>
        public float BrushLine(Pane p, Vector2 fromUv, Vector2 toUv, float radius, float strength)
        {
            var worldDelta = Vector2.Scale(toUv - fromUv, p.size);
            int steps = Mathf.Max(1, Mathf.CeilToInt(worldDelta.magnitude / (radius * 0.33f)));
            float total = 0;
            for (int s = 1; s <= steps; s++) total += Brush(p, Vector2.Lerp(fromUv, toUv, s / (float)steps), radius, strength);
            return total;
        }

        public Vector3 WorldPoint(Pane p, Vector2 uv)
        {
            var os = p.zAxis ? new Vector3(0, uv.y - 0.5f, uv.x - 0.5f) : new Vector3(uv.x - 0.5f, uv.y - 0.5f, 0);
            return p.renderer.transform.TransformPoint(os);
        }

        int TotalTexels
        {
            get { int n = 0; foreach (var p in Panes) n += p.grime.Length; return n; }
        }

        void CheckCleared(Pane p)
        {
            float mean = p.Mean;
            if (p.peakMean > 0.2f && mean < 0.05f)
            {
                p.peakMean = mean;
                p.glint = 1f;
                PaneCleared?.Invoke(p, p.renderer.bounds.center);
            }
        }

        void ForDisc(Pane p, Vector2 uv, float radius, Action<int, float> visit)
        {
            float cx = uv.x * p.width, cy = uv.y * p.height;
            float rx = radius / p.size.x * p.width, ry = radius / p.size.y * p.height;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx)), x1 = Mathf.Min(p.width - 1, Mathf.CeilToInt(cx + rx));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry)), y1 = Mathf.Min(p.height - 1, Mathf.CeilToInt(cy + ry));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float d2 = dx * dx + dy * dy;
                if (d2 >= 1f) continue;
                float f = 1f - d2; // soft edge: the rim of a stroke leaves streaks for a second pass
                visit(y * p.width + x, f * f * (3f - 2f * f));
            }
        }

        static Texture2D MakeNoiseTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "GrimeDetail" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = 0.55f * Mathf.PerlinNoise(u * 6f, v * 6f) + 0.3f * Mathf.PerlinNoise(u * 17f + 3, v * 17f + 7) + 0.15f * Mathf.PerlinNoise(u * 41f, v * 41f);
                float streak = Mathf.PerlinNoise(u * 30f, v * 2f) * 0.25f; // drip streaks
                float a = Mathf.Clamp01(0.35f + (n + streak - 0.45f) * 1.6f);
                byte c = (byte)(200 + 55 * n);
                px[y * size + x] = new Color32(c, c, c, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
