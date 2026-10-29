using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// P3+: every living notable walks the village as their own character, with the same look as their portrait
    /// (KayKit model by id), a touch taller than the crowd and a small marker overhead; the pinned one shows their name.
    /// Takes over notable tagging from CrowdView (tagNotables off) and reserves their headcount in the crowd.
    /// </summary>
    public class NotableView : MonoBehaviour
    {
        public CrowdView crowd;
        [Tooltip("Notables are this much bigger than the crowd, so they read as someone.")] public float scale = 1.15f;
        [Tooltip("Children appear in the village from this age (notable-years).")] public float minAge = 6f;
        public bool showMarkers = true;
        [Tooltip("History replay: show the notables alive at this game month (-1 = live).")] public long viewMonth = -1;

        public static NotableView Instance { get; private set; }

        readonly Dictionary<int, Wanderer> agents = new();
        int generation = -1;
        GUIStyle nameStyle;
        static Texture2D dot;

        void Awake() => Instance = this;

        public static int Look(Notable n, int lookCount) => lookCount <= 0 ? 0 : n.id % lookCount;

        public Wanderer Find(int notableId) => agents.TryGetValue(notableId, out var w) ? w : null;
        public IEnumerable<Wanderer> All => agents.Values;

        /// <summary>The named person nearest the finger on screen, within a touch-sized radius (they're only ~7 pt tall).</summary>
        public bool Pick(Vector2 screenPos, float radiusPx, out Wanderer who)
        {
            who = null;
            var cam = Camera.main;
            if (cam == null) return false;
            float best = radiusPx;
            foreach (var w in agents.Values)
            {
                if (w == null) continue;
                var sp = cam.WorldToScreenPoint(w.transform.position + Vector3.up * crowd.characterScale * scale * 1.2f);
                if (sp.z <= 0) continue;
                float d = Vector2.Distance(screenPos, sp);
                if (d < best) { best = d; who = w; }
            }
            return who != null;
        }

        public void CheerAll(float seconds = 1.7f)
        {
            foreach (var a in agents.Values) a.Cheer(seconds);
        }

        /// <summary>Is this notable on stage at the viewed month (born, not yet dead, old enough to be out and about)?</summary>
        public bool Visible(Notable n, SimState s)
        {
            long m = viewMonth >= 0 ? viewMonth : s.month;
            if (n.bornMonth > m) return false;
            if (viewMonth < 0) return n.alive && n.age >= minAge;
            if (n.diedMonth >= 0 && n.diedMonth <= m) return false;
            // Age at the viewed month: notables age notableAgeFactor per game year.
            long until = n.diedMonth >= 0 ? n.diedMonth : s.month;
            float age = n.age - (until - m) / (float)SimConfig.MonthsPerYear * SimClock.Instance.config.notableAgeFactor;
            return age >= minAge;
        }

        void Update()
        {
            var clock = SimClock.Instance;
            if (clock.Generation != generation)
            {
                generation = clock.Generation;
                foreach (var a in agents.Values) if (a != null) Destroy(a.gameObject);
                agents.Clear();
            }
            var s = clock.Sim.State;
            var want = new HashSet<int>();
            foreach (var n in s.notables)
            {
                if (!Visible(n, s)) continue;
                want.Add(n.id);
                if (agents.ContainsKey(n.id)) continue;
                var prefab = crowd.characterPrefabs[Look(n, crowd.characterPrefabs.Length)];
                var w = crowd.SpawnAgent(n.id, transform, prefab, scale);
                w.name = $"Notable_{n.id}_{n.name}";
                w.SetNotable(n.id, n.FullName);
                agents[n.id] = w;
            }
            foreach (var id in agents.Keys.Where(id => !want.Contains(id)).ToList())
            {
                Destroy(agents[id].gameObject);
                agents.Remove(id);
            }
            crowd.reserved = agents.Count;
        }

        void OnGUI()
        {
            if (!showMarkers || !SurveyPanel.IsPlaying || FeedbackPanel.IsOpen) return;
            ProtoGui.Begin();
            if (dot == null) dot = MakeDot();
            nameStyle ??= new GUIStyle(ProtoGui.Box) { fontSize = 19, padding = new RectOffset(10, 10, 4, 4), wordWrap = false, alignment = TextAnchor.MiddleCenter };
            var s = SimClock.Instance.Sim.State;
            foreach (var (id, w) in agents)
            {
                var head = w.transform.position + Vector3.up * crowd.characterScale * scale * 2.9f;
                var p = ProtoGui.WorldToCanvas(head);
                if (p.x < -100) continue;
                bool pinned = id == s.pinnedNotableId;
                var c = GUI.color;
                GUI.color = pinned ? new Color(1f, 0.83f, 0.4f, 1f) : new Color(0.55f, 0.95f, 0.9f, 0.9f);
                float d = pinned ? 18 : 12;
                GUI.DrawTexture(new Rect(p.x - d / 2, p.y - d / 2, d, d), dot);
                GUI.color = c;
                if (!pinned) continue;
                var label = new GUIContent(w.DisplayName);
                var size = nameStyle.CalcSize(label);
                GUI.Box(new Rect(Mathf.Clamp(p.x - size.x / 2, 8, ProtoGui.Width - size.x - 8), p.y - d / 2 - size.y - 4, size.x, size.y), label, nameStyle);
            }
        }

        static Texture2D MakeDot()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(n / 2f - r);
                float rim = r > n / 2f - 4 ? 0.65f : 1f; // dark rim so it reads on grass and on sky
                t.SetPixel(x, y, new Color(rim, rim, rim, a));
            }
            t.Apply();
            return t;
        }
    }
}
