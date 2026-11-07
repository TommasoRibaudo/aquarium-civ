using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// #276: the wordless symbols over the people of a ritual (a relic moment) or a festival evening, so the player sees that something
    /// special is happening (research 12: bold flat glyphs, no text). The schedule comes from <see cref="RitualBubbles"/> (pure); this
    /// draws it: all marks one mesh, one shared material, billboarded, rebuilt each frame like <see cref="ChatterView"/>'s, a fixed size on
    /// screen. Hidden when the doll is too small to tie a mark to, and in camera mode. Lives on the box next to <see cref="GlassVoice"/>.
    /// </summary>
    public sealed class RitualBubbleView : MonoBehaviour
    {
        public static RitualBubbleView Instance { get; private set; }

        /// <summary>The mark's diameter on screen in points, and panel units (1080 wide) to a point.</summary>
        public const float MarkPoints = 24f, PanelPerPoint = 2.75f;
        /// <summary>A doll shorter than this on screen (pixels of a 2556 tall frame) is too small to carry a mark.</summary>
        public const float TinyDollPx = 14f;
        const float PopSeconds = 0.14f;
        const int Sides = 18, MarksQueue = 3011;

        /// <summary>What was drawn last frame (tests, debug): who and which symbol.</summary>
        public readonly List<(PersonRef who, RitualSymbol symbol)> Drawn = new();
        /// <summary>Every symbol drawn so far and how many bubbles (distinct) came up, per source (tests).</summary>
        public int RitualShown, FestivalShown;
        public readonly HashSet<RitualSymbol> SeenSymbols = new();
        public int MaxAliveSeen;

        sealed class Shape { public Vector3[] v; public Color[] c; public int[] t; }

        readonly Dictionary<RitualSymbol, Shape> shapes = new();
        readonly List<Vector3> verts = new();
        readonly List<Color> colors = new();
        readonly List<int> tris = new();
        readonly List<RelicMomentInfo> moments = new();
        readonly Dictionary<string, List<RitualBubble>> schedules = new();
        readonly Dictionary<string, float> firstSeen = new();
        readonly List<PersonRef> crowd = new();
        List<RitualBubble> festival;
        long festivalDay = long.MinValue;
        GameObject go;
        Mesh mesh;
        Material material;

        void OnEnable() { if (Instance == null) Instance = this; }

        void OnDisable() { if (Instance == this) Instance = null; Dispose(); }

        void LateUpdate()
        {
            Drawn.Clear();
            verts.Clear(); colors.Clear(); tris.Clear();
            var pv = PeopleView.Find();
            var cam = Camera.main;
            var hud = GameHud.Instance;
            if (pv != null && cam != null && pv.Planner != null && (hud == null || !hud.CameraMode)) Collect(pv, cam);
            Upload();
        }

        void Collect(PeopleView pv, Camera cam)
        {
            var now = pv.Now;
            var active = new List<(RitualBubble b, bool fest)>();
            pv.Planner.ActiveRelicMoments(now, moments);
            foreach (var m in moments)
            {
                string key = m.relicId + "/" + m.day + "/" + m.start.ToString("0.000");
                if (!schedules.TryGetValue(key, out var list))
                {
                    list = RitualBubbles.For(m, RelicMoments.For(m.relicId));
                    schedules[key] = list;
                }
                for (int i = 0; i < list.Count; i++)
                    if (now.hour >= list[i].start && now.hour < list[i].end) active.Add((list[i], false));
            }
            StagedMoments.AddBubbles(now, active); // #370: the staged moments' symbols
            if (active.Count == 0) CollectFestival(pv, now, active);
            // Never more than a handful at once, and one per person, whatever is staged (two moments at once, a festival over a ritual).
            if (active.Count > RitualBubbles.MaxAlive || HasTwiceOver(active))
            {
                var seen = new HashSet<long>();
                var keep = new List<(RitualBubble b, bool fest)>();
                foreach (var a in active)
                    if (keep.Count < RitualBubbles.MaxAlive && seen.Add(a.b.who.Key)) keep.Add(a);
                active = keep;
            }
            int alive = 0;
            float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float px = MarkPoints * PanelPerPoint * Screen.width / GameUiPanel.ReferenceWidth;
            var right = cam.transform.right;
            var up = cam.transform.up;
            var forward = cam.transform.forward;
            double real = Time.unscaledTimeAsDouble;
            foreach (var (b, fest) in active)
            {
                if (!pv.TryGetDollTop(b.who, out var top) || !pv.TryGetDollFeet(b.who, out var feet)) continue;
                var s0 = cam.WorldToScreenPoint(top);
                if (s0.z <= 0f || s0.x < 0f || s0.x > Screen.width || s0.y < 0f || s0.y > Screen.height) continue;
                float dollPx = (s0 - cam.WorldToScreenPoint(feet)).magnitude * 2556f / Mathf.Max(1f, Screen.height);
                if (dollPx < TinyDollPx) continue; // zoomed far out: a mark nobody can tie to a person
                float depth = Vector3.Dot(top - cam.transform.position, forward);
                if (depth <= 0f) continue;
                string id = (fest ? "f" : "r") + b.who.Key + "/" + b.symbol + "/" + b.start.ToString("0.000");
                if (!firstSeen.TryGetValue(id, out float t0))
                {
                    firstSeen[id] = t0 = (float)real;
                    if (fest) FestivalShown++; else RitualShown++;
                }
                float pop = Mathf.Clamp01((float)(real - t0) / PopSeconds);
                pop = 1f + 0.18f * Mathf.Sin(pop * Mathf.PI) - 0.5f * (1f - pop);
                float perPx = cam.orthographic ? 2f * cam.orthographicSize / Screen.height : 2f * depth * tan / Screen.height;
                float size = px * perPx;
                var at = top + up * (size * 0.72f);
                Append(ShapeOf(b.symbol), at, right * (size * pop), up * (size * pop));
                Drawn.Add((b.who, b.symbol));
                SeenSymbols.Add(b.symbol);
                alive++;
            }
            if (alive > MaxAliveSeen) MaxAliveSeen = alive;
            if (firstSeen.Count > 400) firstSeen.Clear();
        }

        static bool HasTwiceOver(List<(RitualBubble b, bool fest)> l)
        {
            for (int i = 0; i < l.Count; i++)
                for (int j = i + 1; j < l.Count; j++)
                    if (l[i].b.who.Equals(l[j].b.who)) return true;
            return false;
        }

        void CollectFestival(PeopleView pv, ViewTime now, List<(RitualBubble b, bool fest)> into)
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null || !sim.FestivalOn || now.hour < LifePlanner.FestivalEveningFrom || now.hour >= LifePlanner.FestivalEveningTo) return;
            if (festival == null || festivalDay != now.day)
            {
                // Everyone with a doll on view: the notables first, then the crowd.
                var who = new List<PersonRef>();
                var notables = new List<int>();
                pv.NotablesShown(notables);
                notables.Sort();
                foreach (int id in notables) who.Add(PersonRef.Notable(id));
                pv.CrowdShown(crowd);
                who.AddRange(crowd);
                if (who.Count == 0) return; // try again next frame
                festival = RitualBubbles.ForFestival(LifePlanner.FestivalEveningFrom, LifePlanner.FestivalEveningTo, who);
                festivalDay = now.day;
            }
            for (int i = 0; i < festival.Count; i++)
                if (now.hour >= festival[i].start && now.hour < festival[i].end) into.Add((festival[i], true));
        }

        void Append(Shape s, Vector3 at, Vector3 right, Vector3 up)
        {
            int start = verts.Count;
            for (int i = 0; i < s.v.Length; i++)
            {
                verts.Add(at + right * s.v[i].x + up * s.v[i].y);
                colors.Add(s.c[i]);
            }
            for (int i = 0; i < s.t.Length; i++) tris.Add(start + s.t[i]);
        }

        void Upload()
        {
            if (verts.Count == 0) { if (go != null && go.activeSelf) go.SetActive(false); return; }
            if (go == null) Make();
            if (!go.activeSelf) go.SetActive(true);
            mesh.Clear(true);
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        }

        /// <summary>Whether the marks are on screen now (tests).</summary>
        public bool Showing => go != null && go.activeSelf && Drawn.Count > 0;
        public MeshRenderer Renderer { get; private set; }
        /// <summary>The one material every mark shares (tests).</summary>
        public Material SharedMaterial => material;

        void Make()
        {
            go = new GameObject("RitualBubbles") { hideFlags = HideFlags.DontSave };
            mesh = new Mesh { name = "RitualBubbles", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = go.AddComponent<MeshRenderer>();
            material = new Material(ShapingView.LoadFx()) { name = "RitualBubbles", hideFlags = HideFlags.DontSave, renderQueue = MarksQueue };
            material.SetFloat("_ZTest", (float)CompareFunction.Always); // a mark says who: no roof cuts it
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
        }

        void Dispose()
        {
            if (go != null) Destroy(go);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            go = null; mesh = null; material = null; Renderer = null;
        }

        // ================================================================== the four glyphs
        static readonly Color Ink = new(0.17f, 0.11f, 0.07f, 1f), SunCol = new(0.97f, 0.72f, 0.14f, 1f), SunRay = new(0.93f, 0.55f, 0.1f, 1f);

        Shape ShapeOf(RitualSymbol sym)
        {
            if (shapes.TryGetValue(sym, out var s)) return s;
            var mb = new MeshBuilder(276) { jitter = 0f };
            // The bubble: a white disc with a soft rim and a small tail, as the chatter marks have.
            Disc(mb, Vector2.zero, 0.54f, 0.54f, new Color(0.2f, 0.14f, 0.1f, 0.3f));
            Disc(mb, Vector2.zero, 0.5f, 0.5f, Color.white);
            mb.Tri(new Vector3(-0.13f, -0.44f), new Vector3(0.13f, -0.44f), new Vector3(0f, -0.66f), Color.white);
            switch (sym)
            {
                case RitualSymbol.Sun:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Bar(mb, d * 0.24f, d * 0.4f, 0.075f, SunRay);
                    }
                    Disc(mb, Vector2.zero, 0.19f, 0.19f, SunCol);
                    break;
                case RitualSymbol.Doll:
                    Disc(mb, new Vector2(0f, 0.2f), 0.115f, 0.115f, Ink);
                    mb.Tri(new Vector3(-0.15f, -0.3f), new Vector3(0.15f, -0.3f), new Vector3(0f, 0.08f), Ink);
                    Bar(mb, new Vector2(-0.25f, -0.02f), new Vector2(0.25f, -0.02f), 0.06f, Ink);
                    Disc(mb, new Vector2(0f, 0.2f), 0.05f, 0.05f, Color.white); // the one big open eye
                    break;
                case RitualSymbol.Eye:
                    Almond(mb, 0.36f, 0.2f, Ink);
                    Almond(mb, 0.3f, 0.15f, Color.white);
                    Disc(mb, Vector2.zero, 0.11f, 0.11f, Ink);
                    Disc(mb, new Vector2(0.035f, 0.035f), 0.035f, 0.035f, Color.white);
                    break;
                case RitualSymbol.Notes:
                    foreach (float x in new[] { -0.16f, 0.14f })
                    {
                        Disc(mb, new Vector2(x - 0.08f, -0.17f), 0.1f, 0.075f, Ink);
                        Bar(mb, new Vector2(x, -0.17f), new Vector2(x, 0.25f), 0.045f, Ink);
                    }
                    Bar(mb, new Vector2(-0.16f, 0.25f), new Vector2(0.14f, 0.25f), 0.07f, Ink);
                    break;
                case RitualSymbol.Heart: // t1-moments: the wedding's sign (and the newborn's), a fat rose heart that reads at 20 px
                    {
                        var rose = Palette.Accents[4];
                        Disc(mb, new Vector2(-0.12f, 0.1f), 0.17f, 0.17f, rose);
                        Disc(mb, new Vector2(0.12f, 0.1f), 0.17f, 0.17f, rose);
                        mb.Tri(new Vector3(-0.285f, 0.04f), new Vector3(0.285f, 0.04f), new Vector3(0f, -0.3f), rose);
                        Disc(mb, new Vector2(-0.16f, 0.16f), 0.045f, 0.035f, new Color(1f, 1f, 1f, 0.85f)); // a shine
                        break;
                    }
            }
            s = new Shape { v = mb.verts.ToArray(), c = mb.colors.ToArray(), t = mb.tris.ToArray() };
            shapes[sym] = s;
            return s;
        }

        static void Almond(MeshBuilder mb, float halfW, float halfH, Color col)
        {
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float x0 = Mathf.Lerp(-halfW, halfW, i / (float)n), x1 = Mathf.Lerp(-halfW, halfW, (i + 1) / (float)n);
                float h0 = halfH * (1f - Mathf.Pow(x0 / halfW, 2f)), h1 = halfH * (1f - Mathf.Pow(x1 / halfW, 2f));
                mb.Tri(new Vector3(x0, -h0), new Vector3(x1, -h1), new Vector3(x1, h1), col);
                mb.Tri(new Vector3(x0, -h0), new Vector3(x1, h1), new Vector3(x0, h0), col);
            }
        }

        static void Disc(MeshBuilder mb, Vector2 c, float rx, float ry, Color col)
        {
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides, b = (i + 1) * Mathf.PI * 2f / Sides;
                mb.Tri(new Vector3(c.x, c.y), new Vector3(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry), new Vector3(c.x + Mathf.Cos(b) * rx, c.y + Mathf.Sin(b) * ry), col);
            }
        }

        static void Bar(MeshBuilder mb, Vector2 a, Vector2 b, float width, Color col)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * (width * 0.5f);
            mb.Tri(a + n, b + n, b - n, col);
            mb.Tri(a + n, b - n, a - n, col);
        }
    }
}
