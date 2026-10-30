using System.Collections.Generic;
using UnityEngine;
using AquariumCiv.Sim;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>Anything that can fill a <see cref="BoxLayout"/> before the box builds (the sim view in #33; until then
    /// the temporary demo layout). Put it on the same GameObject as the <see cref="BoxView"/>.</summary>
    public interface IBoxLayoutSource
    {
        void FillLayout(BoxLayout layout);
    }

    /// <summary>
    /// The terrarium, drawn with the style kit (docs/design/spikes/style-kit.md): terrain with the pond, the soil
    /// cross-section behind the glass, the plinth, frame, shelf and wall, nature, and whatever the <see cref="layout"/>
    /// holds (pieces, fields, paths, clearings, people). Static parts merge into one mesh with ambient occlusion baked
    /// into vertex alpha; water, glows, people and the four glass panes are separate. Rebuilt on enable (edit and play
    /// mode) and on <see cref="Rebuild"/>; nothing generated is saved with the scene. No layout logic lives here: where
    /// things go comes from the layout (#32/#33).
    /// </summary>
    [ExecuteAlways]
    public class BoxView : MonoBehaviour
    {
        public Land land = new();
        /// <summary>Filled by an <see cref="IBoxLayoutSource"/> on enable (not saved with the scene: the sim or the demo
        /// owns it).</summary>
        [System.NonSerialized] public BoxLayout layout = new();
        public int seed = 7;
        [Tooltip("Terrain grid; finer grids keep painted paths and the shore smooth.")]
        public int terrainX = 96, terrainZ = 70;
        public bool bakeAO = true;
        public int aoRays = 10;
        public float aoDistance = 0.45f;
        public Material solid, water, glass, glow;
        public string lastBuildInfo;

        public const float GlassMargin = 0.1f;
        public static float PathWidth(PathTier t) => t switch { PathTier.Trail => 0.055f, PathTier.Road => 0.13f, _ => 0.085f };

        Transform root;
        readonly List<Material> panes = new();
        Vector2[] shore;
        readonly List<(Vector2 p, float r)> taken = new();
        public Transform Root => root;
        public IReadOnlyList<PegDollView> Dolls => dolls;
        readonly List<PegDollView> dolls = new();

        void OnEnable()
        {
            var src = GetComponent<IBoxLayoutSource>();
            if (src != null) src.FillLayout(layout);
            Rebuild();
        }

        void OnDisable() => Clear();

        void Clear()
        {
            if (root != null) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); }
            foreach (var m in panes) if (m != null) DestroyImmediate(m);
            panes.Clear();
            dolls.Clear();
            root = null;
        }

        Material Solid => solid != null ? solid : solid = new Material(Shader.Find("AquariumCiv/View/Stylized")) { hideFlags = HideFlags.DontSave };

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            root = new GameObject("Generated") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(transform, false);
            shore = land.Shoreline(64);
            Reserve();

            var mb = new MeshBuilder(seed);
            Terrain(mb);
            PondRim(mb);
            Substrate(mb);
            BoxAndRoom(mb);
            Fields(mb);
            PiecesMesh(mb);
            Nature(mb);
            var staticMesh = mb.ToMesh("BoxStatic");
            float aoMs = 0;
            if (bakeAO) { var t = System.Diagnostics.Stopwatch.StartNew(); BakeAO(staticMesh, mb); aoMs = t.ElapsedMilliseconds; }
            Spawn("Static", staticMesh, Solid, true);
            Spawn("Water", WaterMesh(), water != null ? water : Solid, false);
            Glows();
            People();
            Glass();
            lastBuildInfo = $"{staticMesh.vertexCount} verts, {staticMesh.triangles.Length / 3} tris, {layout.pieces.Count} pieces, {dolls.Count} people, AO {aoMs} ms, total {sw.ElapsedMilliseconds} ms";
        }

        /// <summary>Debug hook: rebuild every box in the scene (after editing a layout from `script eval`).</summary>
        public static string DebugRebuild()
        {
            var s = "";
            foreach (var b in FindObjectsByType<BoxView>(FindObjectsSortMode.None)) { b.Rebuild(); s += b.lastBuildInfo + "\n"; }
            return s;
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

        // ---------------- queries ----------------
        public static float DistToPolyline(Vector2[] pts, Vector2 p)
        {
            float best = 99f;
            if (pts == null) return best;
            if (pts.Length == 1) return Vector2.Distance(p, pts[0]);
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var ab = b - a;
                float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        public static bool InPolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static float DistToRing(Vector2[] ring, Vector2 p)
        {
            float best = 99f;
            for (int i = 0; i < ring.Length; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % ring.Length];
                var ab = b - a;
                float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        public bool InPond(Vector2 p) => shore != null && InPolygon(shore, p);

        /// <summary>Signed distance to the shoreline: negative in the pond.</summary>
        public float ShoreDistance(Vector2 p)
        {
            if (shore == null) shore = land.Shoreline(64);
            float d = DistToRing(shore, p);
            return InPolygon(shore, p) ? -d : d;
        }

        static float PieceRadius(PieceData p) => p.kind switch
        {
            PieceKind.Lantern => 0.08f,
            PieceKind.Crates or PieceKind.Bench or PieceKind.LogSeat => 0.14f,
            PieceKind.Fire => 0.48f,
            PieceKind.Woodpile or PieceKind.DryingRack or PieceKind.Well => 0.2f,
            _ => 0.5f * Mathf.Sqrt(p.w * p.w + p.d * p.d),
        };

        void Reserve()
        {
            taken.Clear();
            foreach (var p in layout.pieces) taken.Add((p.Pos, PieceRadius(p)));
            foreach (var t in layout.trees) taken.Add((t.pos, 0.2f * t.scale));
        }

        bool Free(Vector2 p, float r)
        {
            if (!land.Inside(p, r + GlassMargin)) return false;
            if (ShoreDistance(p) < 0.12f + r) return false;
            foreach (var (q, qr) in taken) if (Vector2.Distance(p, q) < qr + r) return false;
            foreach (var c in layout.clearings) if (Vector2.Distance(p, c.centre) < c.radius + r) return false;
            foreach (var f in layout.fields) if (InPolygon(f.polygon, p) || DistToRing(f.polygon, p) < r + 0.06f) return false;
            foreach (var path in layout.paths) if (DistToPolyline(path.points, p) < PathWidth(path.tier) + 0.05f + r) return false;
            return true;
        }

        // ---------------- ground ----------------
        static Color Cobble(float x, float z, float n)
        {
            float s = Land.Noise(x * 22f, z * 22f);
            return Color.Lerp(Color.Lerp(Palette.Stone, Palette.Path, 0.35f), Palette.RockDark, s * 0.45f + (n - 0.5f) * 0.2f);
        }

        static Color Grass(float n, float y)
        {
            var g = n < 0.42f ? Palette.GrassB : n < 0.66f ? Palette.GrassA : Palette.GrassC;
            return Color.Lerp(g, Palette.Moss, Land.Smooth(Land.G + 0.35f, Land.G + 0.7f, y) * 0.5f);
        }

        /// <summary>The ground's colour at a point (pond bed, shore, fields, paths, clearings, grass) and how much it glows
        /// (the warm pool round a fire).</summary>
        public Color GroundColor(float x, float z, float y, out float glowAmount)
        {
            glowAmount = 0f;
            var p = new Vector2(x, z);
            float sd = ShoreDistance(p);
            if (sd < 0f) return y < Land.WaterY - 0.06f ? Palette.PondBed : Color.Lerp(Palette.SandWet, Palette.PondBed, Mathf.Clamp01(-sd / 0.12f));
            if (sd < 0.05f) return Palette.Sand;
            float n = Land.Fbm(x * 1.4f, z * 1.4f + 5f);
            var col = Grass(n, y);
            bool painted = false;

            foreach (var c in layout.clearings)
            {
                float d = Vector2.Distance(p, c.centre), r = c.radius + 0.08f * (n - 0.5f);
                if (d > r + 0.12f) continue;
                if (d > r) { col = Color.Lerp(col, c.kind == ClearingKind.Green ? Palette.GrassC : Palette.Dirt, 0.35f); continue; }
                col = c.kind switch
                {
                    ClearingKind.Green => Color.Lerp(Palette.GrassC, Palette.GrassA, n),
                    ClearingKind.Cobble => Cobble(x, z, n),
                    _ => d < r * 0.72f ? Palette.Dirt : Color.Lerp(Palette.Dirt, Palette.GrassB, 0.45f),
                };
                painted = true;
            }
            if (!painted)
                foreach (var f in layout.fields)
                {
                    if (!InPolygon(f.polygon, p)) continue;
                    if (f.crop == CropKind.Gathering) { col = n < 0.5f ? Color.Lerp(Palette.Dirt, col, 0.55f) : col; break; }
                    float a = f.rowYaw * Mathf.Deg2Rad;
                    float across = x * Mathf.Cos(a) - z * Mathf.Sin(a);
                    col = (Mathf.FloorToInt(across / 0.08f) & 1) == 0 ? Palette.FieldSoil : Palette.FieldSoilLight;
                    painted = true;
                    break;
                }
            float thrN = (n - 0.5f) * 0.03f;
            foreach (var path in layout.paths)
            {
                float d = DistToPolyline(path.points, p), w = PathWidth(path.tier) + thrN;
                if (d < w)
                    col = path.tier switch
                    {
                        PathTier.Trail => Color.Lerp(Grass(n, y), Palette.Dirt, 0.6f),
                        PathTier.Road => Cobble(x, z, n),
                        _ => Color.Lerp(Palette.Path, Palette.Dirt, (n - 0.4f) * 0.5f),
                    };
                else if (d < w + 0.035f && !painted) col = Color.Lerp(col, Palette.Dirt, 0.25f);
            }
            foreach (var pc in layout.pieces)
            {
                if (pc.kind != PieceKind.Fire) continue;
                float d = Vector2.Distance(p, pc.Pos);
                if (d > 0.6f) continue;
                float k = 1f - d / 0.6f;
                k *= k;
                col = Color.Lerp(col, Color.Lerp(col, Palette.Glow, 0.6f), k * 0.55f);
                glowAmount = Mathf.Max(glowAmount, k * 0.07f);
            }
            return col;
        }

        void Terrain(MeshBuilder mb)
        {
            int nx = Mathf.Max(8, terrainX), nz = Mathf.Max(8, terrainZ);
            float HX = Land.HX, HZ = Land.HZ;
            var h = new float[(nx + 1) * (nz + 1)];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                    h[j * (nx + 1) + i] = land.Height(-HX + 2 * HX * i / nx, -HZ + 2 * HZ * j / nz);
            Vector3 P(int i, int j) => new(-HX + 2 * HX * i / nx, h[j * (nx + 1) + i], -HZ + 2 * HZ * j / nz);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    Vector3 a = P(i, j), b = P(i, j + 1), c = P(i + 1, j + 1), d = P(i + 1, j);
                    if (((i + j) & 1) == 0) { GroundTri(mb, a, b, c); GroundTri(mb, a, c, d); }
                    else { GroundTri(mb, a, b, d); GroundTri(mb, b, c, d); }
                }
        }

        void GroundTri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c)
        {
            var m = (a + b + c) / 3f;
            var col = GroundColor(m.x, m.z, m.y, out float g);
            mb.emission = g;
            mb.Tri(a, b, c, col);
            mb.emission = 0f;
        }

        static void QuadUp(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            if (Vector3.Cross(b - a, c - a).y > 0f) mb.Quad(a, b, c, d, col); // front faces have cross.y > 0 (see Box's top)
            else mb.Quad(d, c, b, a, col);
        }

        /// <summary>
        /// A smooth sand rim draped over the shoreline (spike polish note: the pond's sandy rim was jagged, because it was
        /// only the terrain grid's triangles below a height). Wet sand just over the water's edge, dry sand above it.
        /// </summary>
        void PondRim(MeshBuilder mb)
        {
            int n = shore.Length;
            var inner = new Vector3[n]; var mid = new Vector3[n]; var outer = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var s = shore[i];
                var prev = shore[(i - 1 + n) % n]; var next = shore[(i + 1) % n];
                var tangent = (next - prev).normalized;
                var nrm = new Vector2(tangent.y, -tangent.x);
                if (Vector2.Dot(nrm, s - land.pond) < 0) nrm = -nrm;
                float wob = 0.012f * (Land.Noise(i * 0.7f, 3.3f) - 0.5f);
                Vector3 At(Vector2 q, float lift) => new(q.x, Mathf.Max(land.Height(q.x, q.y), Land.WaterY) + lift, q.y);
                inner[i] = At(s - nrm * 0.035f, 0.004f);
                mid[i] = At(s + nrm * (0.03f + wob), 0.005f);
                outer[i] = At(s + nrm * (0.085f + wob * 2f), 0.004f);
            }
            var keep = mb.jitter; mb.jitter = 0.02f;
            for (int i = 0; i < n; i++)
            {
                int k = (i + 1) % n;
                QuadUp(mb, inner[i], mid[i], mid[k], inner[k], Palette.SandWet);
                QuadUp(mb, mid[i], outer[i], outer[k], mid[k], Palette.Sand);
            }
            mb.jitter = keep;
        }

        /// <summary>The cut through the soil you see behind the glass: gravel, loam, soil, a moss lip, pebbles.</summary>
        void Substrate(MeshBuilder mb)
        {
            float HX = Land.HX, HZ = Land.HZ;
            var corners = new[] { new Vector2(-HX, -HZ), new Vector2(HX, -HZ), new Vector2(HX, HZ), new Vector2(-HX, HZ) };
            Color[] cols = { Palette.Gravel, Palette.Loam, Palette.SoilLight, Palette.Moss };
            for (int side = 0; side < 4; side++)
            {
                var p0 = corners[side]; var p1 = corners[(side + 1) % 4];
                float len = Vector2.Distance(p0, p1);
                int segs = Mathf.RoundToInt(len / 0.0625f);
                var outward = new Vector3(p1.y - p0.y, 0, -(p1.x - p0.x)).normalized;
                for (int s = 0; s < segs; s++)
                {
                    var a = Vector2.Lerp(p0, p1, s / (float)segs); var b = Vector2.Lerp(p0, p1, (s + 1) / (float)segs);
                    float[] la = Layers(a), lb = Layers(b);
                    for (int k = 0; k < 4; k++)
                        mb.Quad(new Vector3(a.x, la[k], a.y), new Vector3(a.x, la[k + 1], a.y), new Vector3(b.x, lb[k + 1], b.y), new Vector3(b.x, lb[k], b.y), cols[k]);
                    if (mb.Range(0f, 1f) < 0.4f)
                    {
                        var at = Vector2.Lerp(a, b, mb.Range(0f, 1f));
                        float r = mb.Range(0.035f, 0.075f);
                        var pos = new Vector3(at.x, mb.Range(0.05f, Layers(at)[1] - 0.02f), at.y) + outward * (-r * 0.45f);
                        var col = mb.Range(0, 3) switch { 0 => Palette.Rock, 1 => Palette.RockLight, _ => Palette.RockDark };
                        mb.Ico(pos, r, new Vector3(1f, 0.75f, 1f), col, 0.2f);
                    }
                    if (mb.Range(0f, 1f) < 0.12f)
                    {
                        var at = Vector2.Lerp(a, b, mb.Range(0f, 1f));
                        var l = Layers(at);
                        mb.Ico(new Vector3(at.x, mb.Range(l[1] + 0.04f, l[2] - 0.04f), at.y) + outward * -0.015f, mb.Range(0.02f, 0.035f), Vector3.one, Palette.RockLight, 0.2f);
                    }
                }
            }
        }

        float[] Layers(Vector2 p)
        {
            float top = land.Height(p.x, p.y);
            float n1 = Land.Noise(p.x * 2.1f + p.y * 1.3f, 3.7f), n2 = Land.Noise(p.x * 1.7f - p.y * 2.2f, 8.1f), n3 = Land.Noise(p.x * 3.1f + p.y, 1.9f);
            return new[] { 0f, 0.18f + 0.06f * n1, 0.46f + 0.08f * n2, Mathf.Max(0.5f, top - 0.08f - 0.04f * n3), Mathf.Max(top, Land.WaterY) };
        }

        void BoxAndRoom(MeshBuilder mb)
        {
            float HX = Land.HX, HZ = Land.HZ, GlassTop = Land.GlassTop;
            const float lip = 0.2f, plinthH = 0.3f;
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
            // The glass's cut edge along the top of the frame: a pale, faintly glowing line (polish note: the glass read
            // only by its dark frame). Together with the shader's rim highlight it says "glass" at full view.
            float ey = GlassTop + t / 2f + 0.004f, ew = 0.022f;
            mb.emission = 0.12f;
            mb.Box(new Vector3(0, ey, -gz), new Vector3(2 * gx + t, 0.008f, ew), Palette.GlassEdge);
            mb.Box(new Vector3(0, ey, gz), new Vector3(2 * gx + t, 0.008f, ew), Palette.GlassEdge);
            mb.Box(new Vector3(-gx, ey, 0), new Vector3(ew, 0.008f, 2 * gz - t), Palette.GlassEdge);
            mb.Box(new Vector3(gx, ey, 0), new Vector3(ew, 0.008f, 2 * gz - t), Palette.GlassEdge);
            mb.emission = 0f;
            // The shelf: a grid top so the baked AO can draw the box's contact shadow on it.
            float sy = -plinthH, sx0 = -16f, sx1 = 16f, sz0 = -4.2f, sz1 = 3.6f, cell = 0.3f;
            var keep = mb.jitter; mb.jitter = 0f;
            for (float x = sx0; x < sx1 - 1e-3f; x += cell)
                for (float z = sz0; z < sz1 - 1e-3f; z += cell)
                {
                    int plank = Mathf.FloorToInt((z - sz0) / 0.9f);
                    var c = Color.Lerp(Palette.Shelf, Palette.ShelfEdge, Land.Hash(plank, 3) * 0.25f);
                    mb.Quad(new Vector3(x, sy, z), new Vector3(x, sy, z + cell), new Vector3(x + cell, sy, z + cell), new Vector3(x + cell, sy, z), c);
                }
            mb.Box(new Vector3(0, sy - 0.2f, sz0), new Vector3(sx1 - sx0, 0.4f, 0.02f), Palette.ShelfEdge);
            float wz = sz1, wy0 = sy, wy1 = 12f;
            for (int i = 0; i < 8; i++)
            {
                float y0 = Mathf.Lerp(wy0, wy1, i / 8f), y1 = Mathf.Lerp(wy0, wy1, (i + 1) / 8f);
                var c0 = Color.Lerp(Palette.WallBottom, Palette.WallTop, i / 8f); var c1 = Color.Lerp(Palette.WallBottom, Palette.WallTop, (i + 1) / 8f);
                Vector3 a = new(sx0 * 2f, y0, wz), b = new(sx0 * 2f, y1, wz), c = new(sx1 * 2f, y1, wz), d = new(sx1 * 2f, y0, wz);
                mb.TriC(a, b, c, c0, c1, c1); mb.TriC(a, c, d, c0, c1, c0);
            }
            mb.jitter = keep;
        }

        // ---------------- layout ----------------
        Pieces.Ground LotGround(MeshBuilder mb)
        {
            return local =>
            {
                var wp = mb.M.MultiplyPoint3x4(local);
                var g = land.OnGround(wp.x, wp.z);
                return mb.M.inverse.MultiplyPoint3x4(g);
            };
        }

        void PiecesMesh(MeshBuilder mb)
        {
            foreach (var p in layout.pieces)
            {
                // Each piece gets its own builder seeded by its seed, so its look doesn't change when others are added.
                var lb = new MeshBuilder(p.seed * 7919 + (int)p.era * 31 + seed) { jitter = 0.035f };
                float sink = p.kind == PieceKind.Home && p.era != Era.Camp ? 0.04f : 0f;
                lb.M = Matrix4x4.TRS(land.OnGround(p.x, p.z) - Vector3.up * sink, Quaternion.Euler(0, p.yaw, 0), Vector3.one);
                Pieces.Draw(lb, p, LotGround(lb));
                mb.Append(lb);
            }
        }

        void Fields(MeshBuilder mb)
        {
            foreach (var f in layout.fields)
            {
                if (f.polygon == null || f.polygon.Length < 3) continue;
                var fb = new MeshBuilder(f.seed * 131 + seed);
                Pieces.Ground ground = q => land.OnGround(q.x, q.z);
                if (f.crop == CropKind.Wheat || f.crop == CropKind.Sprout)
                {
                    var col = f.crop == CropKind.Wheat ? Palette.Wheat : Palette.Sprout;
                    float a = f.rowYaw * Mathf.Deg2Rad;
                    var across = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)); var along = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                    Vector2 min = f.polygon[0], max = f.polygon[0];
                    foreach (var q in f.polygon) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
                    var c = (min + max) / 2f;
                    float ext = (max - min).magnitude / 2f;
                    for (float u = -ext; u <= ext; u += 0.08f)
                        for (float v = -ext; v <= ext; v += 0.07f)
                        {
                            var q = c + across * (u + 0.04f) + along * v + new Vector2(fb.Range(-0.012f, 0.012f), fb.Range(-0.012f, 0.012f));
                            if (!InPolygon(f.polygon, q) || DistToRing(f.polygon, q) < 0.05f) continue;
                            Pieces.Crop(fb, land.OnGround(q), col);
                        }
                }
                if (f.fence)
                {
                    int n = f.polygon.Length;
                    for (int e = 0; e < n; e++)
                    {
                        Vector3 A = new(f.polygon[e].x, 0, f.polygon[e].y), B = new(f.polygon[(e + 1) % n].x, 0, f.polygon[(e + 1) % n].y);
                        if (e == f.gateEdge)
                        {
                            // Leave a gate in the middle of this edge.
                            float len = Vector3.Distance(A, B), gap = Mathf.Min(0.24f, len * 0.5f) / len;
                            Pieces.Fence(fb, ground, new[] { A, Vector3.Lerp(A, B, 0.5f - gap / 2f) });
                            Pieces.Fence(fb, ground, new[] { Vector3.Lerp(A, B, 0.5f + gap / 2f), B });
                        }
                        else Pieces.Fence(fb, ground, new[] { A, B });
                    }
                }
                mb.Append(fb);
            }
        }

        // ---------------- nature ----------------
        void Nature(MeshBuilder mb)
        {
            var nb = new MeshBuilder(seed * 3 + 1);
            float HX = Land.HX, HZ = Land.HZ;
            foreach (var t in layout.trees) Pieces.Tree(nb, land.OnGround(t.pos), t.scale, t.pine);
            if (layout.reeds)
            {
                int count = 0;
                for (int i = 0; i < 120 && count < 34; i++)
                {
                    var s = shore[nb.Range(0, shore.Length)];
                    var dir = (s - land.pond).normalized;
                    var at = s + dir * nb.Range(-0.03f, 0.1f) + new Vector2(nb.Range(-0.03f, 0.03f), nb.Range(-0.03f, 0.03f));
                    if (!land.Inside(at, GlassMargin)) continue;
                    bool clash = false;
                    foreach (var (q, qr) in taken) if (Vector2.Distance(at, q) < qr) { clash = true; break; }
                    if (clash) continue;
                    Pieces.Reed(nb, new Vector3(at.x, Mathf.Max(land.Height(at.x, at.y), Land.WaterY), at.y), count % 3 == 0 ? Palette.Sprout : Palette.LeafB);
                    count++;
                }
            }
            if (layout.autoNature)
            {
                float k = Mathf.Max(0f, layout.natureDensity);
                int trees = Mathf.RoundToInt(24 * k), placed = 0;
                for (int tries = 0; tries < 1200 && placed < trees; tries++)
                {
                    var p = new Vector2(nb.Range(-HX, HX), nb.Range(-HZ, HZ));
                    float xm = land.mirror ? -p.x : p.x;
                    float bias = Land.Smooth(-0.6f, 1.8f, p.y) * 0.6f + Land.Smooth(1.5f, -2.5f, xm) * 0.5f + 0.1f;
                    if (nb.Range(0f, 1f) > bias) continue;
                    if (!Free(p, 0.22f)) continue;
                    Pieces.Tree(nb, land.OnGround(p), nb.Range(0.75f, 1.2f), nb.Range(0f, 1f) < 0.35f);
                    taken.Add((p, 0.2f));
                    placed++;
                }
                for (int i = 0, n = 0; i < 500 && n < Mathf.RoundToInt(16 * k); i++)
                {
                    var p = new Vector2(nb.Range(-HX, HX), nb.Range(-HZ, HZ));
                    if (!Free(p, 0.1f)) continue;
                    Pieces.Bush(nb, land.OnGround(p), nb.Range(0.07f, 0.12f), n % 3 == 0);
                    taken.Add((p, 0.1f));
                    n++;
                }
                for (int i = 0, n = 0; i < 400 && n < Mathf.RoundToInt(14 * k); i++)
                {
                    var p = new Vector2(nb.Range(-HX, HX), nb.Range(-HZ, HZ));
                    if (!Free(p, 0.08f)) continue;
                    float r = nb.Range(0.05f, 0.14f);
                    Pieces.Rock(nb, land.OnGround(p), r, n % 3 == 0);
                    taken.Add((p, r));
                    n++;
                }
                int tufts = Mathf.RoundToInt(340 * k);
                for (int i = 0, n = 0; i < tufts * 4 && n < tufts; i++)
                {
                    var p = new Vector2(nb.Range(-HX, HX), nb.Range(-HZ, HZ));
                    if (!Free(p, 0.02f)) continue;
                    var g = land.OnGround(p);
                    float h = nb.Range(0.05f, 0.1f);
                    var col = nb.Range(0, 3) switch { 0 => Palette.GrassC, 1 => Palette.LeafC, _ => Palette.GrassA };
                    Pieces.GrassTuft(nb, g, h, col);
                    if (n % 5 == 0) nb.Ico(g + Vector3.up * h * 0.8f, 0.014f, Vector3.one, Palette.Flowers[nb.Range(0, Palette.Flowers.Length)], 0.1f);
                    n++;
                }
            }
            mb.Append(nb);
        }

        Mesh WaterMesh()
        {
            var mb = new MeshBuilder(seed + 3) { jitter = 0f };
            var c = new Vector3(land.pond.x, Land.WaterY, land.pond.y);
            int n = shore.Length;
            for (int i = 0; i < n; i++)
            {
                // A little past the shoreline: the ground and the sand rim cover the edge.
                Vector2 s0 = shore[i] + (shore[i] - land.pond).normalized * 0.05f, s1 = shore[(i + 1) % n] + (shore[(i + 1) % n] - land.pond).normalized * 0.05f;
                Vector3 p0 = new(s0.x, Land.WaterY, s0.y), p1 = new(s1.x, Land.WaterY, s1.y);
                if (Vector3.Cross(p1 - c, p0 - c).y > 0) mb.TriC(c, p1, p0, Palette.WaterDeep, Palette.Water, Palette.Water);
                else mb.TriC(c, p0, p1, Palette.WaterDeep, Palette.Water, Palette.Water);
            }
            return mb.ToMesh("Water");
        }

        // ---------------- glows ----------------
        /// <summary>Soft additive glows: a big breathing one over every fire, a small one on every lantern that follows
        /// the night level. One mesh, one draw.</summary>
        void Glows()
        {
            var verts = new List<Vector3>(); var cols = new List<Color>(); var corner = new List<Vector2>(); var data = new List<Vector2>(); var tris = new List<int>();
            void Add(Vector3 c, float radius, float kind, Color col)
            {
                int o = verts.Count;
                foreach (var k in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) })
                { verts.Add(c); cols.Add(col); corner.Add(k); data.Add(new Vector2(radius, kind)); }
                tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
            foreach (var p in layout.pieces)
            {
                var frame = Matrix4x4.TRS(land.OnGround(p.x, p.z), Quaternion.Euler(0, p.yaw, 0), Vector3.one);
                if (p.kind == PieceKind.Fire || (p.kind == PieceKind.Tavern && p.era == Era.Camp))
                    Add(frame.MultiplyPoint3x4(new Vector3(0, 0.13f, 0)), 0.42f, 0f, Palette.Glow);
                else if (p.kind == PieceKind.Lantern)
                    Add(frame.MultiplyPoint3x4(new Vector3(0.065f, 0.275f, 0)), 0.2f, 1f + Pieces.Phase(p.seed), Palette.Lamp);
            }
            if (verts.Count == 0) return;
            var m = new Mesh { name = "Glows" };
            m.SetVertices(verts); m.SetColors(cols); m.SetUVs(0, corner); m.SetUVs(1, data); m.SetTriangles(tris, 0);
            m.bounds = new Bounds(new Vector3(0, 1.5f, 0), new Vector3(2 * Land.HX + 1f, 3f, 2 * Land.HZ + 1f));
            var mat = glow != null ? glow : new Material(Shader.Find("AquariumCiv/View/Glow")) { hideFlags = HideFlags.DontSave };
            var go = Spawn("Glows", m, mat, false);
            go.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        // ---------------- people ----------------
        void People()
        {
            var parent = new GameObject("People") { hideFlags = HideFlags.DontSave }.transform;
            parent.SetParent(root, false);
            foreach (var v in layout.villagers)
            {
                var look = PegDoll.PickLook(v.seed, v.era == Era.Camp, v.kid, v.accent);
                var doll = PegDollView.Create(parent, Solid, look, v.kid, v.seed);
                doll.transform.SetLocalPositionAndRotation(land.OnGround(v.pos), Quaternion.Euler(0, v.yaw, 0));
                if (v.prop != PropKind.None) doll.SetProp(v.prop, v.slot);
                dolls.Add(doll);
            }
        }

        // ---------------- glass ----------------
        void Glass()
        {
            var shader = Shader.Find("AquariumCiv/View/Glass");
            if (glass == null && shader == null) return;
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            float gx = Land.HX + 0.075f, gz = Land.HZ + 0.075f, h = Land.GlassTop - 0.03f, t = 0.02f;
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
                bool facing = Vector3.Dot(transform.TransformDirection(outward), cam.transform.position - t.position) > 0;
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
                    var wn = transform.TransformDirection(n);
                    var rot = Quaternion.FromToRotation(Vector3.forward, wn);
                    float occ = 0f;
                    var origin = p + wn * 0.004f;
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
    }
}
