using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Shows the sim's mess: grime tint on the glass, trash props, overgrowth props. Trash and vines clean on tap.
    /// P1 replaces/extends the glass with a paintable wipe mask.
    /// </summary>
    public class MessView : MonoBehaviour
    {
        public DioramaView diorama;
        public CrowdView crowd;
        public Renderer[] glassRenderers;
        public Color cleanGlass = new(0.85f, 0.95f, 1f, 0.10f);
        public Color dirtyGlass = new(0.42f, 0.47f, 0.30f, 0.78f);
        public GameObject[] trashPrefabs;
        public float trashScale = 0.55f;
        public GameObject[] vinePrefabs;
        public float vineScale = 0.6f;
        public int maxVines = 14;
        public Material puffMaterial;

        Material glassMat;
        readonly Dictionary<int, GameObject> trash = new();
        readonly List<GameObject> vines = new();
        Vector3[] vineSpots;
        int vineSeed = 99, generation;

        public float GlassGrime => SimClock.Instance.Sim.State.grime;

        void Start()
        {
            if (glassRenderers.Length > 0)
            {
                glassMat = new Material(glassRenderers[0].sharedMaterial) { name = "GlassGrime (runtime)" };
                glassMat.SetTexture("_BaseMap", MakeGrimeTexture(256));
                foreach (var r in glassRenderers) r.sharedMaterial = glassMat;
            }
            var rng = new System.Random(vineSeed);
            vineSpots = new Vector3[maxVines];
            for (int i = 0; i < maxVines; i++)
            {
                var slot = diorama.slotPositions[rng.Next(diorama.slotPositions.Length)];
                float a = (float)rng.NextDouble() * Mathf.PI * 2, d = (float)rng.NextDouble() * diorama.slotRadius;
                vineSpots[i] = slot + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            }
        }

        void Update()
        {
            var sim = SimClock.Instance.Sim;
            var s = sim.State;
            if (SimClock.Instance.Generation != generation)
            {
                generation = SimClock.Instance.Generation;
                foreach (var go in trash.Values) Destroy(go);
                trash.Clear();
            }
            if (glassMat != null) glassMat.SetColor("_BaseColor", Color.Lerp(cleanGlass, dirtyGlass, Mathf.SmoothStep(0, 1, s.grime)));

            // Trash
            var alive = new HashSet<int>();
            foreach (var t in s.trash)
            {
                alive.Add(t.id);
                if (trash.ContainsKey(t.id)) continue;
                var pos = diorama.SlotPosition(t.slot) + new Vector3(t.offsetX, 0, t.offsetZ) * diorama.slotRadius * 2f;
                var go = Instantiate(trashPrefabs[t.id % trashPrefabs.Length], pos, Quaternion.Euler(0, t.id * 47 % 360, 0), transform);
                go.transform.localScale = Vector3.one * trashScale;
                go.name = $"Trash_{t.id}";
                DioramaView.AddBoundsCollider(go);
                LogCategory.Tag(go, "trash", t.id.ToString());
                go.AddComponent<TapToClean>().Init(this, "trash", t.id);
                trash[t.id] = go;
            }
            foreach (var id in new List<int>(trash.Keys))
                if (!alive.Contains(id)) { Destroy(trash[id]); trash.Remove(id); }

            // Overgrowth
            int wantVines = Mathf.RoundToInt(s.overgrowth * maxVines);
            while (vines.Count < wantVines)
            {
                int i = vines.Count;
                var go = Instantiate(vinePrefabs[i % vinePrefabs.Length], vineSpots[i], Quaternion.Euler(0, i * 73 % 360, 0), transform);
                go.transform.localScale = Vector3.one * vineScale;
                go.name = $"Vine_{i}";
                DioramaView.AddBoundsCollider(go);
                LogCategory.Tag(go, "vine", i.ToString());
                go.AddComponent<TapToClean>().Init(this, "vine", i);
                vines.Add(go);
            }
            while (vines.Count > wantVines) { Destroy(vines[^1]); vines.RemoveAt(vines.Count - 1); }
        }

        internal void Clean(string kind, int id, GameObject piece, Vector3 at)
        {
            var sim = SimClock.Instance.Sim;
            float before = sim.MessLevel;
            if (kind == "trash") sim.RemoveTrash(id);
            else if (kind == "vine")
            {
                sim.Prune(1f / maxVines + 0.001f);
                vines.Remove(piece); // remove the tapped vine, not whichever is last
                Destroy(piece);
            }
            EventLog.Instance?.Clean(kind, 1, "tap", before);
            Puff(at);
            AfterClean(before);
        }

        /// <summary>Debug / P1 hook: wipe some grime off the glass.</summary>
        public void WipeGlass(float amount, string gesture)
        {
            var sim = SimClock.Instance.Sim;
            float before = sim.MessLevel, grimeBefore = sim.State.grime;
            sim.CleanGrime(amount);
            EventLog.Instance?.Clean("glass", grimeBefore - sim.State.grime, gesture, before);
            AfterClean(before);
        }

        public void CleanEverything(string gesture)
        {
            var sim = SimClock.Instance.Sim;
            float before = sim.MessLevel;
            sim.CleanAll();
            EventLog.Instance?.Clean("all", before, gesture, before);
            AfterClean(before);
        }

        void AfterClean(float messBefore)
        {
            if (messBefore > 0.15f && SimClock.Instance.Sim.MessLevel < 0.05f && crowd != null) crowd.CheerAll();
        }

        void Puff(Vector3 at)
        {
            var go = new GameObject("Puff");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 0.8f;
            main.startSize = 0.07f;
            main.startColor = new Color(1f, 0.97f, 0.85f, 0.9f);
            main.gravityModifier = -0.1f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            if (puffMaterial != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = puffMaterial;
            ps.Play();
        }

        static Texture2D MakeGrimeTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "Grime" };
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

    /// <summary>A piece of mess that disappears with a little hop when tapped.</summary>
    public class TapToClean : MonoBehaviour, ITappable
    {
        MessView owner;
        string kind;
        int id;
        bool done;

        public void Init(MessView view, string k, int i) { owner = view; kind = k; id = i; }

        public void OnTap(RaycastHit hit)
        {
            if (done) return;
            done = true;
            StartCoroutine(Vanish());
        }

        IEnumerator Vanish()
        {
            var t = transform;
            var start = t.localScale;
            var p0 = t.position;
            for (float k = 0; k < 1f; k += Time.deltaTime * 5f)
            {
                t.position = p0 + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.15f;
                t.localScale = start * (1f - k);
                yield return null;
            }
            owner.Clean(kind, id, gameObject, p0 + Vector3.up * 0.1f);
        }
    }
}
