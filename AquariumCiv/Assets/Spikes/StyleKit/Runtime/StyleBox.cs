using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.Spikes.StyleKit
{
    public enum Era { Camp, Village }

    /// <summary>
    /// Style-kit spike (docs/research/07-art-direction.md §7.3, no KayKit): a whole terrarium built in code from one
    /// palette and one stylised material. Terrain with a soil cross-section behind the glass, nature, an era's buildings,
    /// peg-doll villagers, glass v2, the plinth and a shelf against a wall. Static parts merge into one mesh with ambient
    /// occlusion baked into vertex alpha. Rebuilt on enable (edit and play mode); nothing generated is saved.
    /// </summary>
    [ExecuteAlways]
    public class StyleBox : MonoBehaviour
    {
        public Era era = Era.Village;
        public int seed = 7;
        public bool bakeAO = true;
        public int aoRays = 10;
        public float aoDistance = 0.45f;
        public Material solid, water, glass;

        // Box interior half-size and the ground's base height.
        public const float HX = 3f, HZ = 2.2f, G = 1f, GlassTop = 2.95f, WaterY = G - 0.08f;
        static readonly Vector2 Pond = new(1.75f, -1.15f), Centre = new(-0.35f, -0.25f);
        static readonly Rect FieldRect = new(1.0f, 0.45f, 1.6f, 1.25f);

        readonly List<(Vector2 p, float r)> taken = new();
        readonly List<Vector2[]> paths = new();
        readonly List<Material> panes = new();
        Transform root;
        public string lastBuildInfo;

        void OnEnable() => Build();
        void OnDisable() => Clear();

        void Clear()
        {
            if (root != null) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); }
            foreach (var m in panes) if (m != null) DestroyImmediate(m);
            panes.Clear();
            root = null;
        }

        [ContextMenu("Rebuild")]
        public void Build()
        {
            Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            taken.Clear(); paths.Clear();
            root = new GameObject("Generated").transform;
            root.SetParent(transform, false);
            root.gameObject.hideFlags = HideFlags.DontSave;

            solid ??= new Material(Shader.Find("AquariumCiv/Spike/Stylized"));
            water ??= solid;

            var mb = new MeshBuilder(seed);
            Layout();
            Terrain(mb);
            Substrate(mb);
            BoxAndRoom(mb);
            if (era == Era.Village) Village(mb); else Camp(mb);
            Nature(mb);
            var staticMesh = mb.ToMesh($"Box_{era}");
            float aoMs = 0;
            if (bakeAO) { var t = System.Diagnostics.Stopwatch.StartNew(); BakeAO(staticMesh, mb); aoMs = t.ElapsedMilliseconds; }
            Spawn("Static", staticMesh, solid, true);
            Spawn("Water", WaterMesh(), water, false);
            People();
            Glass();
            lastBuildInfo = $"{era}: {staticMesh.vertexCount} verts, {staticMesh.triangles.Length / 3} tris, AO {aoMs} ms, total {sw.ElapsedMilliseconds} ms";
        }

        GameObject Spawn(string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ---------------- ground ----------------
        public static float Height(float x, float z)
        {
            float h = G + 0.07f * Fbm(x * 0.9f, z * 0.9f);
            float back = Smooth(0.1f, 2.1f, z), left = Smooth(2.6f, -2.8f, x);
            h += 0.62f * back * (0.3f + 0.7f * left) * (0.7f + 0.6f * Fbm(x * 0.6f, z * 0.6f + 11f));
            float dp = Vector2.Distance(new Vector2(x, z), Pond);
            h -= 0.24f * (1f - Smooth(0.45f, 0.98f, dp));
            float dc = Vector2.Distance(new Vector2(x, z), Centre);
            h = Mathf.Lerp(h, G + 0.03f, (1f - Smooth(0.9f, 1.75f, dc)) * 0.92f);
            return h;
        }

        Vector3 OnGround(float x, float z) => new(x, Height(x, z), z);

        void Layout()
        {
            if (era == Era.Village)
            {
                paths.Add(new[] { Centre, new Vector2(0.4f, 0.1f), new Vector2(1.05f, 0.85f) });                 // to the field gate
                paths.Add(new[] { Centre, new Vector2(-0.5f, -1.2f), new Vector2(-0.55f, -2.3f) });              // to the front glass
                paths.Add(new[] { Centre, new Vector2(-1.2f, 0.2f), new Vector2(-2.2f, 0.9f), new Vector2(-3.1f, 1.2f) }); // up the hill
                paths.Add(new[] { new Vector2(0.4f, 0.1f), new Vector2(1.1f, -0.35f), new Vector2(3.1f, -0.2f) });
            }
        }

        static float DistToPaths(List<Vector2[]> paths, Vector2 p)
        {
            float best = 99f;
            foreach (var path in paths)
                for (int i = 0; i + 1 < path.Length; i++)
                {
                    var a = path[i]; var b = path[i + 1];
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                    best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
                }
            return best;
        }

        Color GroundColor(float x, float z, float y)
        {
            var p = new Vector2(x, z);
            if (y < WaterY - 0.01f) return Palette.PondBed;
            if (y < WaterY + 0.035f) return Palette.Sand;
            float n = Fbm(x * 1.4f, z * 1.4f + 5f);
            if (era == Era.Village)
            {
                if (Vector2.Distance(p, Centre) < 0.5f + 0.06f * n) return Palette.Path;
                if (DistToPaths(paths, p) < 0.09f + 0.05f * n) return Palette.Path;
                if (FieldRect.Contains(p)) return ((int)((z - FieldRect.yMin) / 0.16f) % 2 == 0) ? Palette.FieldSoil : Palette.FieldSoilLight;
            }
            else
            {
                float d = Vector2.Distance(p, Centre);
                if (d < 0.62f + 0.25f * n) return d < 0.45f ? Palette.Dirt : Color.Lerp(Palette.Dirt, Palette.GrassB, 0.45f);
            }
            var g = n < 0.42f ? Palette.GrassB : n < 0.66f ? Palette.GrassA : Palette.GrassC;
            return Color.Lerp(g, Palette.Moss, Smooth(G + 0.35f, G + 0.7f, y) * 0.5f);
        }

        void Terrain(MeshBuilder mb)
        {
            const int nx = 48, nz = 36;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x0 = -HX + 2 * HX * i / nx, x1 = -HX + 2 * HX * (i + 1) / nx;
                    float z0 = -HZ + 2 * HZ * j / nz, z1 = -HZ + 2 * HZ * (j + 1) / nz;
                    Vector3 a = OnGround(x0, z0), b = OnGround(x0, z1), c = OnGround(x1, z1), d = OnGround(x1, z0);
                    if (((i + j) & 1) == 0)
                    {
                        Tri(mb, a, b, c); Tri(mb, a, c, d);
                    }
                    else
                    {
                        Tri(mb, a, b, d); Tri(mb, b, c, d);
                    }
                }
        }

        void Tri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c)
        {
            var m = (a + b + c) / 3f;
            mb.Tri(a, b, c, GroundColor(m.x, m.z, m.y));
        }

        /// <summary>The cut through the soil you see behind the glass: gravel, loam, soil, a moss lip, pebbles.</summary>
        void Substrate(MeshBuilder mb)
        {
            var corners = new[] { new Vector2(-HX, -HZ), new Vector2(HX, -HZ), new Vector2(HX, HZ), new Vector2(-HX, HZ) };
            for (int side = 0; side < 4; side++)
            {
                var p0 = corners[side]; var p1 = corners[(side + 1) % 4];
                float len = Vector2.Distance(p0, p1);
                int segs = Mathf.RoundToInt(len / 0.125f);
                var outward = new Vector3(p1.y - p0.y, 0, -(p1.x - p0.x)).normalized;
                for (int s = 0; s < segs; s++)
                {
                    var a = Vector2.Lerp(p0, p1, s / (float)segs); var b = Vector2.Lerp(p0, p1, (s + 1) / (float)segs);
                    float[] la = Layers(a), lb = Layers(b);
                    Color[] cols = { Palette.Gravel, Palette.Loam, Palette.SoilLight, Palette.Moss };
                    for (int k = 0; k < 4; k++)
                        mb.Quad(new Vector3(a.x, la[k], a.y), new Vector3(a.x, la[k + 1], a.y), new Vector3(b.x, lb[k + 1], b.y), new Vector3(b.x, lb[k], b.y), cols[k]);
                    // Pebbles pressed against the glass, mostly in the gravel.
                    if (mb.Range(0f, 1f) < 0.8f)
                    {
                        var at = Vector2.Lerp(a, b, mb.Range(0f, 1f));
                        float r = mb.Range(0.035f, 0.075f);
                        var pos = new Vector3(at.x, mb.Range(0.05f, Layers(at)[1] - 0.02f), at.y) + outward * (-r * 0.45f);
                        var col = mb.Range(0, 3) switch { 0 => Palette.Rock, 1 => Palette.RockLight, _ => Palette.RockDark };
                        mb.Ico(pos, r, new Vector3(1f, 0.75f, 1f), col, 0.2f);
                    }
                    if (mb.Range(0f, 1f) < 0.25f)
                    {
                        var at = Vector2.Lerp(a, b, mb.Range(0f, 1f));
                        var l = Layers(at);
                        mb.Ico(new Vector3(at.x, mb.Range(l[1] + 0.04f, l[2] - 0.04f), at.y) + outward * -0.015f, mb.Range(0.02f, 0.035f), Vector3.one, Palette.RockLight, 0.2f);
                    }
                }
            }
        }

        static float[] Layers(Vector2 p)
        {
            float top = Height(p.x, p.y);
            float n1 = Noise(p.x * 2.1f + p.y * 1.3f, 3.7f), n2 = Noise(p.x * 1.7f - p.y * 2.2f, 8.1f), n3 = Noise(p.x * 3.1f + p.y, 1.9f);
            return new[] { 0f, 0.18f + 0.06f * n1, 0.46f + 0.08f * n2, top - 0.08f - 0.04f * n3, top };
        }

        void BoxAndRoom(MeshBuilder mb)
        {
            const float lip = 0.2f, plinthH = 0.3f;
            // Plinth (with a slightly lighter top board) and a thin dark frame.
            mb.Box(new Vector3(0, -plinthH / 2f, 0), new Vector3(2 * HX + 2 * lip, plinthH, 2 * HZ + 2 * lip), Palette.Plinth);
            mb.Box(new Vector3(0, 0.012f, 0), new Vector3(2 * HX + 2 * lip - 0.04f, 0.024f, 2 * HZ + 2 * lip - 0.04f), Palette.PlinthTop);
            float gx = HX + 0.075f, gz = HZ + 0.075f, t = 0.07f;
            foreach (var (x, z) in new[] { (-gx, -gz), (gx, -gz), (gx, gz), (-gx, gz) })
                mb.Box(new Vector3(x, GlassTop / 2f, z), new Vector3(t, GlassTop, t), Palette.Frame);
            foreach (float y in new[] { 0.05f, GlassTop })
            {
                mb.Box(new Vector3(0, y, -gz), new Vector3(2 * gx + t, t, t), Palette.Frame);
                mb.Box(new Vector3(0, y, gz), new Vector3(2 * gx + t, t, t), Palette.Frame);
                mb.Box(new Vector3(-gx, y, 0), new Vector3(t, t, 2 * gz), Palette.Frame);
                mb.Box(new Vector3(gx, y, 0), new Vector3(t, t, 2 * gz), Palette.Frame);
            }
            // The shelf: a grid top so the baked AO can draw the box's contact shadow on it.
            float sy = -plinthH, sx0 = -9f, sx1 = 9f, sz0 = -4.2f, sz1 = 3.6f, cell = 0.3f;
            var keep = mb.jitter; mb.jitter = 0f;
            for (float x = sx0; x < sx1 - 1e-3f; x += cell)
                for (float z = sz0; z < sz1 - 1e-3f; z += cell)
                {
                    // Planks run along x; each plank keeps one tone so the grid needed for AO doesn't show.
                    int plank = Mathf.FloorToInt((z - sz0) / 0.9f);
                    var c = Color.Lerp(Palette.Shelf, Palette.ShelfEdge, Hash(plank, 3) * 0.25f);
                    mb.Quad(new Vector3(x, sy, z), new Vector3(x, sy, z + cell), new Vector3(x + cell, sy, z + cell), new Vector3(x + cell, sy, z), c);
                }
            mb.Box(new Vector3(0, sy - 0.2f, sz0 + 0.0f), new Vector3(sx1 - sx0, 0.4f, 0.02f), Palette.ShelfEdge);
            // The wall behind: a soft vertical gradient.
            float wz = sz1, wy0 = sy, wy1 = 12f;
            mb.jitter = 0f;
            for (int i = 0; i < 8; i++)
            {
                float y0 = Mathf.Lerp(wy0, wy1, i / 8f), y1 = Mathf.Lerp(wy0, wy1, (i + 1) / 8f);
                var c0 = Color.Lerp(Palette.WallBottom, Palette.WallTop, i / 8f); var c1 = Color.Lerp(Palette.WallBottom, Palette.WallTop, (i + 1) / 8f);
                Vector3 a = new(sx0 * 2f, y0, wz), b = new(sx0 * 2f, y1, wz), c = new(sx1 * 2f, y1, wz), d = new(sx1 * 2f, y0, wz);
                mb.TriC(a, b, c, c0, c1, c1); mb.TriC(a, c, d, c0, c1, c0);
            }
            mb.jitter = keep;
        }

        // ---------------- eras ----------------
        static float FaceYaw(Vector2 from, Vector2 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 180f; // local -z (the door side) faces the target
        }

        void Village(MeshBuilder mb)
        {
            Well(mb, Centre);
            taken.Add((Centre, 0.3f));
            var houses = new[] { new Vector2(-1.3f, -0.95f), new Vector2(-1.5f, 0.15f), new Vector2(-0.55f, 0.8f), new Vector2(0.55f, 0.55f), new Vector2(0.45f, -1.05f), new Vector2(-2.35f, -0.35f) };
            Color[] roofs = { Palette.Thatch, Palette.Terracotta, Palette.Thatch, Palette.Slate, Palette.Terracotta, Palette.Thatch };
            for (int i = 0; i < houses.Length; i++)
            {
                float w = mb.Range(0.5f, 0.66f), d = mb.Range(0.4f, 0.48f);
                House(mb, houses[i], FaceYaw(houses[i], Centre) + mb.Range(-12f, 12f), w, d, mb.Range(0.3f, 0.38f), mb.Range(0.24f, 0.3f),
                    i % 3 == 1 ? Palette.PlasterWarm : Palette.Plaster, roofs[i], i % 2 == 0);
                taken.Add((houses[i], 0.45f));
            }
            Woodpile(mb, new Vector2(-0.95f, 0.75f), 30f);
            Bench(mb, Centre + new Vector2(0.38f, -0.3f), 60f);
            // Fenced field with wheat rows.
            var f = FieldRect;
            Fence(mb, new[] { new Vector2(f.xMin, f.yMin + 0.35f), new Vector2(f.xMin, f.yMax), new Vector2(f.xMax, f.yMax), new Vector2(f.xMax, f.yMin), new Vector2(f.xMin + 0.25f, f.yMin) });
            for (float z = f.yMin + 0.08f; z < f.yMax - 0.05f; z += 0.32f)
                for (float x = f.xMin + 0.08f; x < f.xMax - 0.05f; x += 0.07f)
                {
                    var p = OnGround(x + mb.Range(-0.015f, 0.015f), z + mb.Range(-0.02f, 0.02f));
                    mb.sway = 0.7f;
                    bool ripe = z > f.center.y;
                    mb.Prism(p, 0.022f, 0f, mb.Range(0.09f, 0.13f), 4, ripe ? Palette.Wheat : Palette.Sprout, Quaternion.Euler(0, mb.Range(0, 90f), 0));
                    mb.sway = 0;
                }
            taken.Add((f.center, 0.95f));
            // Reeds and a little jetty by the pond.
            Reeds(mb, 26);
            Jetty(mb);
        }

        void Camp(MeshBuilder mb)
        {
            Firepit(mb, Centre);
            taken.Add((Centre, 0.35f));
            Teepee(mb, new Vector2(-1.25f, -0.65f), 0.36f, 0.62f, Palette.Hide);
            Teepee(mb, new Vector2(0.45f, 0.6f), 0.32f, 0.56f, Palette.Canvas);
            LeanTo(mb, new Vector2(-1.05f, 0.65f), FaceYaw(new Vector2(-1.05f, 0.65f), Centre));
            taken.Add((new Vector2(-1.25f, -0.65f), 0.45f)); taken.Add((new Vector2(0.45f, 0.6f), 0.42f)); taken.Add((new Vector2(-1.05f, 0.65f), 0.45f));
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.4f;
                var p = Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.42f;
                Log(mb, OnGround(p.x, p.y) + Vector3.up * 0.035f, 0.035f, 0.32f, -a * Mathf.Rad2Deg + 90f, Palette.Bark);
            }
            DryingRack(mb, new Vector2(0.7f, -0.6f));
            taken.Add((new Vector2(0.7f, -0.6f), 0.3f));
            Woodpile(mb, new Vector2(-1.75f, 0.0f), 80f);
            taken.Add((new Vector2(-1.75f, 0.0f), 0.25f));
            Reeds(mb, 40);
        }

        void House(MeshBuilder mb, Vector2 at, float yaw, float w, float d, float wallH, float roofH, Color wall, Color roof, bool chimney)
        {
            var keep = mb.M;
            mb.M = Matrix4x4.TRS(OnGround(at.x, at.y) - Vector3.up * 0.04f, Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(w + 0.05f, 0.1f, d + 0.05f), Palette.Stone);
            mb.Box(new Vector3(0, 0.1f + wallH / 2f, 0), new Vector3(w, wallH, d), wall);
            float top = 0.1f + wallH;
            // Half-timbering: corner posts and a top beam.
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                mb.Box(new Vector3(x * (w / 2f - 0.01f), 0.1f + wallH / 2f, z * (d / 2f - 0.01f)), new Vector3(0.035f, wallH, 0.035f), Palette.Timber);
            mb.Box(new Vector3(0, top - 0.015f, -d / 2f - 0.003f), new Vector3(w, 0.03f, 0.012f), Palette.Timber);
            // Gable roof along x with an overhang.
            float oh = 0.07f, half = d / 2f + oh;
            float slope = Mathf.Sqrt(half * half + roofH * roofH), ang = Mathf.Atan2(roofH, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(new Vector3(0, top + roofH / 2f, s * half / 2f), new Vector3(w + 2 * oh, 0.035f, slope), roof, Quaternion.Euler(s * ang, 0, 0), true);
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * w / 2f;
                var a = new Vector3(x, top, -d / 2f); var b = new Vector3(x, top + roofH * (d / 2f) / half, 0); var c = new Vector3(x, top, d / 2f);
                if (s > 0) mb.Tri(a, b, c, wall); else mb.Tri(c, b, a, wall);
            }
            // Door, windows (warm, slightly emissive), chimney.
            mb.Box(new Vector3(w * 0.18f, 0.1f + 0.09f, -d / 2f - 0.006f), new Vector3(0.1f, 0.18f, 0.012f), Palette.Door);
            mb.emission = 0.35f;
            mb.Box(new Vector3(-w * 0.2f, 0.1f + wallH * 0.6f, -d / 2f - 0.006f), new Vector3(0.075f, 0.07f, 0.012f), Palette.WindowLit);
            mb.Box(new Vector3(w / 2f + 0.006f, 0.1f + wallH * 0.6f, 0), new Vector3(0.012f, 0.07f, 0.075f), Palette.WindowLit);
            mb.Box(new Vector3(-w / 2f - 0.006f, 0.1f + wallH * 0.6f, 0), new Vector3(0.012f, 0.07f, 0.075f), Palette.WindowLit);
            mb.emission = 0f;
            mb.Box(new Vector3(-w * 0.2f, 0.1f + wallH * 0.6f - 0.045f, -d / 2f - 0.012f), new Vector3(0.1f, 0.015f, 0.02f), Palette.Timber);
            if (chimney) mb.Box(new Vector3(w * 0.28f, top + roofH * 0.75f, d * 0.12f), new Vector3(0.07f, roofH * 0.9f, 0.07f), Palette.Stone);
            mb.M = keep;
        }

        void Well(MeshBuilder mb, Vector2 at)
        {
            var p = OnGround(at.x, at.y);
            mb.Prism(p, 0.13f, 0.13f, 0.12f, 9, Palette.Stone, topCol: Palette.RockDark);
            mb.Prism(p + Vector3.up * 0.121f, 0.09f, 0.09f, 0.001f, 9, Palette.WaterDeep);
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.11f, 0.2f, 0), new Vector3(0.025f, 0.28f, 0.025f), Palette.Timber);
            mb.Box(p + new Vector3(0, 0.32f, 0), new Vector3(0.26f, 0.02f, 0.02f), Palette.Timber);
            foreach (int s in new[] { -1, 1 })
                mb.Box(p + new Vector3(0, 0.37f, s * 0.07f), new Vector3(0.3f, 0.02f, 0.17f), Palette.Thatch, Quaternion.Euler(s * 35f, 0, 0), true);
        }

        void Bench(MeshBuilder mb, Vector2 at, float yaw)
        {
            var keep = mb.M;
            mb.M = Matrix4x4.TRS(OnGround(at.x, at.y), Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.24f, 0.018f, 0.06f), Palette.WoodLight);
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.09f, 0.022f, 0), new Vector3(0.02f, 0.045f, 0.05f), Palette.Timber);
            mb.M = keep;
        }

        void Fence(MeshBuilder mb, Vector2[] pts)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / 0.2f));
                for (int k = 0; k <= n; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)n);
                    mb.Box(OnGround(p.x, p.y) + Vector3.up * 0.07f, new Vector3(0.025f, 0.15f, 0.025f), Palette.Timber, Quaternion.Euler(0, mb.Range(-8f, 8f), mb.Range(-4f, 4f)));
                    if (k == n) continue;
                    var q = Vector2.Lerp(a, b, (k + 1) / (float)n);
                    Vector3 pa = OnGround(p.x, p.y), pb = OnGround(q.x, q.y);
                    var dir = pb - pa;
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    foreach (float y in new[] { 0.06f, 0.11f })
                        mb.Box((pa + pb) / 2f + Vector3.up * y, new Vector3(0.014f, 0.016f, dir.magnitude), Palette.WoodLight, rot);
                }
            }
        }

        void Woodpile(MeshBuilder mb, Vector2 at, float yaw)
        {
            var p = OnGround(at.x, at.y);
            var r = Quaternion.Euler(0, yaw, 0);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                {
                    var c = p + r * new Vector3((i - (3 - row) / 2f) * 0.055f, 0.028f + row * 0.048f, 0);
                    Log(mb, c, 0.026f, 0.26f, yaw, i % 2 == 0 ? Palette.Bark : Palette.WoodLight);
                }
        }

        void Log(MeshBuilder mb, Vector3 centre, float r, float len, float yaw, Color col)
        {
            var rot = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90, 0, 0);
            mb.Prism(centre - rot * Vector3.up * (len / 2f), r, r, len, 6, col, rot, true, true, Palette.WoodLight);
        }

        void Firepit(MeshBuilder mb, Vector2 at)
        {
            var p = OnGround(at.x, at.y);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                mb.Ico(p + new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.13f, 0.04f, new Vector3(1, 0.7f, 1), i % 2 == 0 ? Palette.Rock : Palette.RockDark, 0.25f);
            }
            for (int i = 0; i < 3; i++) Log(mb, p + Vector3.up * 0.025f, 0.018f, 0.2f, i * 60f, Palette.Bark);
            mb.Prism(p, 0.06f, 0.06f, 0.012f, 7, Palette.Ember);
            mb.emission = 1f; mb.sway = 1f;
            mb.Prism(p + Vector3.up * 0.01f, 0.06f, 0f, 0.17f, 5, Palette.Fire, Quaternion.Euler(0, 20f, 0));
            mb.Prism(p + new Vector3(0.015f, 0.01f, 0.01f), 0.035f, 0f, 0.12f, 5, Palette.FireCore, Quaternion.Euler(0, 50f, 0));
            mb.emission = 0f; mb.sway = 0f;
        }

        void Teepee(MeshBuilder mb, Vector2 at, float r, float h, Color cover)
        {
            var p = OnGround(at.x, at.y) - Vector3.up * 0.02f;
            mb.Prism(p, r, 0.03f, h, 7, cover, Quaternion.Euler(0, mb.Range(0, 50f), 0), true, false, Palette.Bark);
            // Door flap: a darker triangle facing the fire.
            float yaw = FaceYaw(at, Centre) + 180f;
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var basePt = p + fwd * (r * 0.93f);
            mb.Tri(basePt - right * 0.09f + Vector3.up * 0.01f, basePt + Vector3.up * 0.22f - fwd * 0.07f + fwd * 0.005f, basePt + right * 0.09f + Vector3.up * 0.01f, Palette.Door);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                var tip = p + Vector3.up * (h + 0.09f) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.05f;
                var mid = p + Vector3.up * h * 0.85f;
                var dir = (tip - mid);
                mb.Prism(mid, 0.008f, 0.006f, dir.magnitude, 4, Palette.Bark, Quaternion.FromToRotation(Vector3.up, dir.normalized));
            }
        }

        void LeanTo(MeshBuilder mb, Vector2 at, float yaw)
        {
            var keep = mb.M;
            mb.M = Matrix4x4.TRS(OnGround(at.x, at.y), Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.Box(new Vector3(0, 0.2f, 0.05f), new Vector3(0.55f, 0.02f, 0.46f), Palette.Hide, Quaternion.Euler(-48f, 0, 0), true);
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.25f, 0.17f, -0.11f), new Vector3(0.025f, 0.34f, 0.025f), Palette.Bark);
            mb.Box(new Vector3(0, 0.34f, -0.11f), new Vector3(0.6f, 0.025f, 0.025f), Palette.Bark);
            mb.Box(new Vector3(0, 0.012f, 0.08f), new Vector3(0.4f, 0.02f, 0.26f), Palette.Canvas);
            mb.M = keep;
        }

        void DryingRack(MeshBuilder mb, Vector2 at)
        {
            var keep = mb.M;
            mb.M = Matrix4x4.TRS(OnGround(at.x, at.y), Quaternion.Euler(0, 25f, 0), Vector3.one);
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.15f, 0.14f, 0), new Vector3(0.02f, 0.28f, 0.02f), Palette.Bark);
            mb.Box(new Vector3(0, 0.27f, 0), new Vector3(0.34f, 0.018f, 0.018f), Palette.Bark);
            mb.sway = 0.25f;
            mb.Box(new Vector3(-0.06f, 0.19f, 0), new Vector3(0.1f, 0.15f, 0.008f), Palette.Hide);
            mb.Box(new Vector3(0.07f, 0.2f, 0), new Vector3(0.08f, 0.12f, 0.008f), Palette.Canvas);
            mb.sway = 0;
            mb.M = keep;
        }

        void Reeds(MeshBuilder mb, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = mb.Range(0f, Mathf.PI * 2f), d = mb.Range(0.82f, 1.0f);
                var at = Pond + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.9f) * d;
                if (Mathf.Abs(at.x) > HX - 0.05f || Mathf.Abs(at.y) > HZ - 0.05f) continue;
                var p = OnGround(at.x, at.y);
                mb.sway = 0.8f;
                mb.Prism(p, 0.012f, 0f, mb.Range(0.14f, 0.24f), 3, i % 3 == 0 ? Palette.Sprout : Palette.LeafB, Quaternion.Euler(mb.Range(-8f, 8f), 0, mb.Range(-8f, 8f)));
                mb.sway = 0;
            }
        }

        void Jetty(MeshBuilder mb)
        {
            var start = new Vector2(1.0f, -0.85f); var dir = (Pond - start).normalized;
            var rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y));
            var c = new Vector3(start.x + dir.x * 0.3f, WaterY + 0.05f, start.y + dir.y * 0.3f);
            mb.Box(c, new Vector3(0.18f, 0.025f, 0.55f), Palette.WoodLight, rot);
            foreach (var (x, z) in new[] { (-0.08f, 0.2f), (0.08f, 0.2f), (-0.08f, -0.05f), (0.08f, -0.05f) })
                mb.Box(c + rot * new Vector3(x, -0.06f, z), new Vector3(0.025f, 0.13f, 0.025f), Palette.Timber);
        }

        // ---------------- nature ----------------
        bool Free(Vector2 p, float r)
        {
            if (Mathf.Abs(p.x) > HX - r || Mathf.Abs(p.y) > HZ - r) return false;
            if (Vector2.Distance(p, Pond) < 1.0f + r) return false;
            foreach (var (q, qr) in taken) if (Vector2.Distance(p, q) < qr + r) return false;
            if (era == Era.Village && DistToPaths(paths, p) < 0.12f + r) return false;
            return true;
        }

        void Nature(MeshBuilder mb)
        {
            bool camp = era == Era.Camp;
            // Trees, mostly on the back and left hills.
            int trees = camp ? 30 : 17, placed = 0;
            for (int tries = 0; tries < 900 && placed < trees; tries++)
            {
                var p = new Vector2(mb.Range(-HX, HX), mb.Range(-HZ, HZ));
                float bias = Smooth(-0.6f, 1.8f, p.y) * 0.6f + Smooth(1.5f, -2.5f, p.x) * 0.5f + (camp ? 0.25f : 0f);
                if (mb.Range(0f, 1f) > bias) continue;
                if (!Free(p, 0.22f)) continue;
                Tree(mb, p, mb.Range(0.75f, 1.2f) * (camp ? 1.1f : 1f), mb.Range(0f, 1f) < (camp ? 0.45f : 0.3f));
                taken.Add((p, 0.2f));
                placed++;
            }
            for (int i = 0, n = 0; i < 400 && n < (camp ? 22 : 12); i++)
            {
                var p = new Vector2(mb.Range(-HX, HX), mb.Range(-HZ, HZ));
                if (!Free(p, 0.1f)) continue;
                var g = OnGround(p.x, p.y);
                mb.sway = 0.3f;
                mb.Ico(g + Vector3.up * 0.05f, mb.Range(0.07f, 0.12f), new Vector3(1.2f, 0.8f, 1.1f), n % 2 == 0 ? Palette.LeafB : Palette.LeafC, 0.25f);
                mb.sway = 0;
                if (camp && n % 3 == 0)
                    for (int k = 0; k < 4; k++) mb.Ico(g + new Vector3(mb.Range(-0.07f, 0.07f), mb.Range(0.06f, 0.11f), mb.Range(-0.07f, 0.07f)), 0.016f, Vector3.one, Palette.Ember, 0.1f);
                taken.Add((p, 0.1f));
                n++;
            }
            for (int i = 0, n = 0; i < 400 && n < 14; i++)
            {
                var p = new Vector2(mb.Range(-HX, HX), mb.Range(-HZ, HZ));
                if (!Free(p, 0.08f)) continue;
                float r = mb.Range(0.05f, 0.14f);
                mb.Ico(OnGround(p.x, p.y) + Vector3.up * r * 0.2f, r, new Vector3(1.2f, 0.6f, 1f), n % 3 == 0 ? Palette.RockLight : Palette.Rock, 0.22f, Palette.RockDark);
                taken.Add((p, r));
                n++;
            }
            // Grass tufts and flowers between everything.
            int tufts = camp ? 420 : 260;
            for (int i = 0, n = 0; i < tufts * 4 && n < tufts; i++)
            {
                var p = new Vector2(mb.Range(-HX, HX), mb.Range(-HZ, HZ));
                if (!Free(p, 0.02f)) continue;
                var g = OnGround(p.x, p.y);
                if (era == Era.Village && (FieldRect.Contains(p) || Vector2.Distance(p, Centre) < 0.6f)) continue;
                if (camp && Vector2.Distance(p, Centre) < 0.5f) continue;
                float h = mb.Range(0.05f, 0.1f) * (camp ? 1.35f : 1f);
                var col = mb.Range(0, 3) switch { 0 => Palette.GrassC, 1 => Palette.LeafC, _ => Palette.GrassA };
                mb.sway = 1f;
                for (int b = 0; b < 3; b++)
                {
                    float a = b * 2.1f + mb.Range(0f, 1f);
                    var lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * h * 0.35f;
                    var side = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.012f;
                    mb.Tri(g - side, g + lean + Vector3.up * h, g + side, col);
                    mb.Tri(g + side, g + lean + Vector3.up * h, g - side, col);
                }
                mb.sway = 0;
                if (n % 5 == 0)
                    mb.Ico(g + Vector3.up * h * 0.8f, 0.014f, Vector3.one, Palette.Flowers[mb.Range(0, Palette.Flowers.Length)], 0.1f);
                n++;
            }
        }

        void Tree(MeshBuilder mb, Vector2 at, float s, bool pine)
        {
            var g = OnGround(at.x, at.y) - Vector3.up * 0.02f;
            mb.Prism(g, 0.045f * s, 0.032f * s, 0.3f * s, 6, Palette.Bark);
            mb.sway = 1f;
            if (pine)
            {
                for (int i = 0; i < 3; i++)
                    mb.Prism(g + Vector3.up * (0.2f + i * 0.17f) * s, (0.24f - i * 0.06f) * s, 0f, 0.3f * s, 7, Palette.Pine, Quaternion.Euler(0, i * 20f, 0), false, true);
            }
            else
            {
                var col = mb.Range(0, 3) switch { 0 => Palette.LeafA, 1 => Palette.LeafB, _ => Palette.LeafC };
                mb.Ico(g + Vector3.up * 0.42f * s, 0.2f * s, new Vector3(1.1f, 0.95f, 1.1f), col, 0.18f, Palette.LeafB);
                mb.Ico(g + new Vector3(0.1f, 0.55f, 0.04f) * s, 0.14f * s, Vector3.one, col, 0.18f);
                mb.Ico(g + new Vector3(-0.09f, 0.5f, -0.06f) * s, 0.13f * s, Vector3.one, Color.Lerp(col, Palette.LeafC, 0.4f), 0.18f);
            }
            mb.sway = 0f;
        }

        Mesh WaterMesh()
        {
            var mb = new MeshBuilder(seed + 3) { jitter = 0f };
            const int segs = 24;
            var c = new Vector3(Pond.x, WaterY, Pond.y);
            for (int i = 0; i < segs; i++)
            {
                float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0) * 0.9f) * 1.0f, p1 = c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1) * 0.9f) * 1.0f;
                mb.TriC(c, p1, p0, Palette.WaterDeep, Palette.Water, Palette.Water);
            }
            return mb.ToMesh("Water");
        }

        // ---------------- people ----------------
        void People()
        {
            var parent = new GameObject("People") { hideFlags = HideFlags.DontSave }.transform;
            parent.SetParent(root, false);
            int adults = era == Era.Village ? 12 : 7, kids = era == Era.Village ? 3 : 2;
            var rng = new System.Random(seed * 31 + (int)era);
            for (int i = 0; i < adults + kids; i++)
            {
                bool kid = i >= adults;
                var mb = new MeshBuilder(seed * 100 + i) { jitter = 0.02f };
                Villager.BuildMesh(mb, rng, kid, era == Era.Camp);
                var go = Spawn(kid ? "Kid" : "Villager", mb.ToMesh("Villager"), solid, true);
                go.transform.SetParent(parent, false);
                Vector2 p;
                int guard = 0;
                do
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = 0.3f + (float)rng.NextDouble() * 1.4f;
                    p = Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                } while ((Height(p.x, p.y) < WaterY + 0.05f || Mathf.Abs(p.x) > HX - 0.2f || Mathf.Abs(p.y) > HZ - 0.2f || Blocked(p)) && ++guard < 50);
                go.transform.localPosition = OnGround(p.x, p.y);
                go.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                var v = go.AddComponent<Villager>();
                v.home = Centre; v.range = 1.5f; v.seed = seed * 7 + i;
                v.isBlocked = Blocked;
            }
        }

        bool Blocked(Vector2 p)
        {
            foreach (var (q, qr) in taken) if (qr > 0.21f && Vector2.Distance(p, q) < qr * 0.8f) return true;
            return Height(p.x, p.y) < WaterY + 0.04f || (era == Era.Village && FieldRect.Contains(p));
        }

        // ---------------- glass ----------------
        void Glass()
        {
            var shader = Shader.Find("AquariumCiv/Spike/GlassV2");
            if (shader == null) return;
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            float gx = HX + 0.075f, gz = HZ + 0.075f, h = GlassTop - 0.03f, t = 0.02f;
            var specs = new[]
            {
                ("Front", new Vector3(0, h / 2f, -gz), new Vector3(2 * gx, h, t), 0f, new Vector2(2 * gx, h)),
                ("Back", new Vector3(0, h / 2f, gz), new Vector3(2 * gx, h, t), 0f, new Vector2(2 * gx, h)),
                ("Left", new Vector3(-gx, h / 2f, 0), new Vector3(t, h, 2 * gz), 1f, new Vector2(2 * gz, h)),
                ("Right", new Vector3(gx, h / 2f, 0), new Vector3(t, h, 2 * gz), 1f, new Vector2(2 * gz, h)),
            };
            foreach (var (name, pos, size, axis, pane) in specs)
            {
                var mat = new Material(glass != null ? glass : new Material(shader)) { hideFlags = HideFlags.DontSave };
                mat.SetFloat("_Axis", axis);
                mat.SetVector("_PaneSize", new Vector4(pane.x, pane.y, 0, 0));
                mat.SetFloat("_StreakOffset", name.Length * 0.37f);
                panes.Add(mat);
                var go = Spawn("Glass" + name, cube, mat, false);
                go.transform.localPosition = pos;
                go.transform.localScale = size;
                go.GetComponent<MeshRenderer>().receiveShadows = false;
            }
            SortPanes();
        }

        /// <summary>Back panes draw first, then the front ones (fixed queues, no per-frame distance sort).</summary>
        void SortPanes()
        {
            var cam = Camera.main;
            if (cam == null || root == null) return;
            foreach (Transform t in root)
            {
                if (!t.name.StartsWith("Glass")) continue;
                var r = t.GetComponent<MeshRenderer>();
                var outward = new Vector3(Mathf.Sign(t.localPosition.x) * (t.localScale.x < 0.1f ? 1 : 0), 0, Mathf.Sign(t.localPosition.z) * (t.localScale.z < 0.1f ? 1 : 0));
                bool facing = Vector3.Dot(outward, cam.transform.position - t.position) > 0;
                r.sharedMaterial.renderQueue = facing ? 3010 : 3000;
            }
        }

        void LateUpdate() => SortPanes();

        // ---------------- ambient occlusion ----------------
        void BakeAO(Mesh mesh, MeshBuilder mb)
        {
            var go = new GameObject("AO collider") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            Physics.SyncTransforms();
            var dirs = new Vector3[aoRays];
            var r = new System.Random(5);
            for (int i = 0; i < aoRays; i++)
            {
                // Cosine-weighted hemisphere around +z.
                float u = (i + 0.5f) / aoRays, v = (float)r.NextDouble();
                float rad = Mathf.Sqrt(u), phi = v * Mathf.PI * 2f + i * 2.4f;
                dirs[i] = new Vector3(rad * Mathf.Cos(phi), rad * Mathf.Sin(phi), Mathf.Sqrt(1 - u));
            }
            var cache = new Dictionary<(Vector3Int, Vector3Int), float>();
            var colors = mb.colors;
            var scene = gameObject.scene.GetPhysicsScene();
            for (int i = 0; i < mb.verts.Count; i++)
            {
                var n = mb.normals[i];
                var p = transform.TransformPoint(mb.verts[i]);
                var key = (Vector3Int.RoundToInt(p * 500f), Vector3Int.RoundToInt(n * 50f));
                if (!cache.TryGetValue(key, out float ao))
                {
                    var rot = Quaternion.FromToRotation(Vector3.forward, transform.TransformDirection(n));
                    float occ = 0f;
                    var origin = p + transform.TransformDirection(n) * 0.004f;
                    for (int k = 0; k < aoRays; k++)
                        if (scene.Raycast(origin, rot * dirs[k], out var hit, aoDistance))
                            occ += 1f - hit.distance / aoDistance * 0.6f;
                    ao = 1f - 0.85f * occ / aoRays;
                    cache[key] = ao;
                }
                var c = colors[i]; c.a = ao; colors[i] = c;
            }
            mesh.SetColors(colors);
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        // ---------------- noise ----------------
        static float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        public static float Noise(float x, float z)
        {
            int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
            float xf = x - xi, zf = z - zi;
            float u = xf * xf * (3 - 2 * xf), v = zf * zf * (3 - 2 * zf);
            float a = Hash(xi, zi), b = Hash(xi + 1, zi), c = Hash(xi, zi + 1), d = Hash(xi + 1, zi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float z) => (Noise(x, z) * 0.57f + Noise(x * 2.03f + 17f, z * 2.03f) * 0.29f + Noise(x * 4.1f, z * 4.1f + 31f) * 0.14f);

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
