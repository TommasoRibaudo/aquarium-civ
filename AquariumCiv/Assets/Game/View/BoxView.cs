using System.Collections.Generic;
using AquariumCiv.Sim;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>Anything that can fill a <see cref="BoxLayout"/> before the box builds or refreshes (the live sim,
    /// <see cref="SimLayoutSource"/>; the demo layout in tests). Put it on the same GameObject as the <see cref="BoxView"/>.</summary>
    public interface IBoxLayoutSource
    {
        void FillLayout(BoxLayout layout);
    }

    /// <summary>
    /// The terrarium, drawn with the style kit (docs/design/spikes/style-kit.md): terrain with the pond, the soil
    /// cross-section behind the glass, the plinth, frame, shelf and wall, nature, and whatever the <see cref="layout"/>
    /// holds (pieces, fields, paths, clearings, canals, people). No layout logic lives here: where things go comes from
    /// the layout (<see cref="SimLayout"/>, #33).
    /// <para>
    /// Chunked (research 08 §3.8): the parts that never change (soil cut, box, shelf, wall, pond rim) are one Base mesh;
    /// the land inside the glass is four chunks (the box's quarters), each with its terrain, fields, pieces and nature,
    /// ambient occlusion baked into vertex alpha per chunk. <see cref="Refresh"/> refills the layout, hashes what touches
    /// each chunk and rebuilds only the chunks whose hash changed (a new building or a path-tier change rebuilds its
    /// quarter, not the box). People are kept and moved, made only when new. Water, glows and the four glass panes are
    /// separate. Nothing generated is saved with the scene.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    public class BoxView : MonoBehaviour
    {
        public Land land = new();
        /// <summary>Filled by an <see cref="IBoxLayoutSource"/> on enable and on <see cref="Refresh"/> (not saved with the
        /// scene: the sim owns it).</summary>
        [System.NonSerialized] public BoxLayout layout = new();
        public int seed = 7;
        [Tooltip("Terrain grid (made even); finer grids keep painted paths and the shore smooth.")]
        public int terrainX = 96, terrainZ = 70;
        public bool bakeAO = true;
        public int aoRays = 10;
        public float aoDistance = 0.45f;
        [Tooltip("Log every chunk rebuild with its timing.")]
        public bool logRebuilds = true;
        public Material solid, water, glass, glow;
        [Tooltip("Alpha-blended smoke for the camp fires (FireSmoke.mat); a runtime material is made when empty.")]
        public Material smoke;
        public string lastBuildInfo, lastRefreshInfo;
        /// <summary>The last <see cref="Refresh"/>: chunks rebuilt, total and AO milliseconds.</summary>
        [System.NonSerialized] public List<int> lastChunks = new();
        [System.NonSerialized] public double lastRefreshMs, lastAoMs;

        public const float GlassMargin = 0.1f;
        public const int ChunkCount = 4;
        public static float PathWidth(PathTier t) => t switch { PathTier.Trail => 0.055f, PathTier.Road => 0.13f, _ => 0.085f };
        const int AoLayer = 2; // Ignore Raycast: the AO colliders stay out of everyone else's raycasts

        Transform root, peopleRoot;
        readonly List<Material> panes = new();
        Vector2[] shore;
        readonly List<(Vector2 p, float r)> taken = new();
        readonly List<(Vector2 p, bool sprout)> reedSpots = new();
        readonly MeshFilter[] chunkFilters = new MeshFilter[ChunkCount];
        readonly MeshCollider[] chunkColliders = new MeshCollider[ChunkCount];
        readonly uint[] chunkHash = new uint[ChunkCount];
        uint landHash;
        GameObject glowGo, smokeGo;
        Material glowMat, smokeMat;
        readonly Dictionary<int, (PegDollView doll, uint sig)> dollByKey = new();
        public Transform Root => root;
        public IReadOnlyList<PegDollView> Dolls => dolls;
        readonly List<PegDollView> dolls = new();
        static readonly List<VillagerData> NoVillagers = new();
        bool plannedPeople;

        /// <summary>A <see cref="PeopleView"/> draws the people from the activity planner (#30): the layout's standing
        /// people (#33's placeholders) are dropped, and come back when it lets go.</summary>
        public bool PlannedPeople
        {
            get => plannedPeople;
            set { if (plannedPeople == value) return; plannedPeople = value; if (root != null) SyncPeople(); }
        }

        /// <summary>The style kit's shared solid material (the one every generated mesh and doll uses).</summary>
        public Material SolidMaterial => Solid;

        void OnEnable()
        {
            var src = GetComponent<IBoxLayoutSource>();
            if (src != null) src.FillLayout(layout);
            Rebuild();
        }

        void OnDisable() => Clear();

        void Clear()
        {
            if (root != null)
            {
                // The box's own generated meshes (not the built-in cube of the panes, nor the dolls' shared prop meshes).
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh;
                    if (m != null && (m.name.StartsWith("Box") || m.name == "Water" || m.name == "Glows" || m.name == "Smoke")) Kill(m);
                }
                Kill(root.gameObject);
            }
            foreach (var m in panes) if (m != null) DestroyImmediate(m);
            panes.Clear();
            dolls.Clear();
            dollByKey.Clear();
            root = peopleRoot = null;
            glowGo = smokeGo = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        Material Solid => solid != null ? solid : solid = new Material(Shader.Find("AquariumCiv/View/Stylized")) { hideFlags = HideFlags.DontSave };

        int NX => Mathf.Max(8, terrainX + (terrainX & 1));
        int NZ => Mathf.Max(8, terrainZ + (terrainZ & 1));

        // ---------------- build ----------------
        /// <summary>Builds everything from the current <see cref="layout"/> (no refill).</summary>
        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            root = new GameObject("Generated") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(transform, false);
            shore = land.Shoreline(64);
            ShoreField();
            ReedSpots();
            PrepareFields();
            Reserve();

            var bm = new MeshBuilder(seed);
            PondRim(bm);
            Substrate(bm);
            BoxAndRoom(bm);
            var baseMesh = bm.ToMesh("BoxBase");
            Spawn("Base", baseMesh, Solid, true);
            AoCollider("AO Base", baseMesh);

            var builders = new MeshBuilder[ChunkCount];
            var terrainVerts = new int[ChunkCount];
            var meshes = new Mesh[ChunkCount];
            int verts = baseMesh.vertexCount, tris = baseMesh.triangles.Length / 3;
            for (int k = 0; k < ChunkCount; k++)
            {
                builders[k] = BuildChunk(k, out terrainVerts[k]);
                meshes[k] = builders[k].ToMesh("BoxChunk" + k);
                chunkFilters[k] = Spawn("Chunk " + k, meshes[k], Solid, true).GetComponent<MeshFilter>();
                chunkColliders[k] = AoCollider("AO " + k, meshes[k]);
                verts += meshes[k].vertexCount; tris += builders[k].TriangleCount;
            }
            double aoMs = 0;
            if (bakeAO)
            {
                var t = System.Diagnostics.Stopwatch.StartNew();
                Physics.SyncTransforms();
                var all = new List<(MeshBuilder, int, Mesh)> { (bm, 0, baseMesh) };
                for (int k = 0; k < ChunkCount; k++) all.Add((builders[k], terrainVerts[k], meshes[k]));
                BakeAO(all);
                aoMs = t.Elapsed.TotalMilliseconds;
            }
            Spawn("Water", WaterMesh(), water != null ? water : Solid, false);
            Glows();
            SyncPeople();
            Glass();
            for (int k = 0; k < ChunkCount; k++) chunkHash[k] = 0;
            ChunkHashes(chunkHash);
            landHash = LandHash();
            lastBuildInfo = $"{verts} verts, {tris} tris, {layout.pieces.Count} pieces, {dolls.Count} people, AO {aoMs:0} ms, total {sw.ElapsedMilliseconds} ms";
        }

        /// <summary>
        /// Refills the layout from the source and rebuilds only what changed: the chunks whose contents hash differently,
        /// the glows, and the people (kept, moved, made or removed). A changed land rebuilds everything. Returns the timing.
        /// </summary>
        public string Refresh()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var src = GetComponent<IBoxLayoutSource>();
            if (src != null) src.FillLayout(layout);
            double fillMs = sw.Elapsed.TotalMilliseconds;
            if (root == null || LandHash() != landHash)
            {
                Rebuild();
                lastChunks.Clear(); for (int k = 0; k < ChunkCount; k++) lastChunks.Add(k);
                lastRefreshInfo = "full rebuild: " + lastBuildInfo;
                return lastRefreshInfo;
            }
            PrepareFields();
            Reserve();
            var h = new uint[ChunkCount];
            ChunkHashes(h);
            lastChunks.Clear();
            for (int k = 0; k < ChunkCount; k++) if (h[k] != chunkHash[k]) lastChunks.Add(k);
            double prepMs = sw.Elapsed.TotalMilliseconds;
            var builders = new List<(int k, MeshBuilder mb, int tv, Mesh mesh)>();
            var built = new MeshBuilder[lastChunks.Count];
            var tvs = new int[lastChunks.Count];
            // The dirty chunks build side by side on worker threads (pure maths into their own builders).
            System.Threading.Tasks.Parallel.For(0, lastChunks.Count, i => built[i] = BuildChunk(lastChunks[i], out tvs[i]));
            double buildMs = sw.Elapsed.TotalMilliseconds;
            for (int i = 0; i < lastChunks.Count; i++)
            {
                int k = lastChunks[i], tv = tvs[i];
                var mb = built[i];
                var mesh = mb.ToMesh("BoxChunk" + k);
                var old = chunkFilters[k].sharedMesh;
                chunkFilters[k].sharedMesh = mesh;
                chunkColliders[k].sharedMesh = mesh;
                Kill(old);
                builders.Add((k, mb, tv, mesh));
                chunkHash[k] = h[k];
            }
            double meshMs = sw.Elapsed.TotalMilliseconds;
            double aoMs = 0;
            if (bakeAO && builders.Count > 0)
            {
                var t = System.Diagnostics.Stopwatch.StartNew();
                Physics.SyncTransforms();
                var all = new List<(MeshBuilder, int, Mesh)>();
                foreach (var (_, mb, tv, mesh) in builders) all.Add((mb, tv, mesh));
                BakeAO(all);
                aoMs = t.Elapsed.TotalMilliseconds;
            }
            Glows();
            int made = SyncPeople();
            lastRefreshMs = sw.Elapsed.TotalMilliseconds;
            lastAoMs = aoMs;
            lastRefreshInfo = lastChunks.Count == 0
                ? $"no chunk changed, {made} new people, {lastRefreshMs:0.0} ms"
                : $"rebuilt chunk(s) {string.Join(",", lastChunks)} in {lastRefreshMs:0.0} ms (AO {aoMs:0.0} ms), {made} new people, {layout.pieces.Count} pieces " +
                  $"[fill {fillMs:0.0}, fields+hash {prepMs - fillMs:0.0}, build {buildMs - prepMs:0.0}, mesh+collider {meshMs - buildMs:0.0}, AO {aoMs:0.0}, rest {lastRefreshMs - meshMs - aoMs:0.0}]";
            if (logRebuilds && lastChunks.Count > 0) Debug.Log("[BoxView] " + lastRefreshInfo);
            return lastRefreshInfo;
        }

        /// <summary>Debug hook: rebuild every box in the scene (after editing a layout from `script eval`).</summary>
        public static string DebugRebuild()
        {
            var s = "";
            foreach (var b in FindObjectsByType<BoxView>(FindObjectsSortMode.None)) { b.Rebuild(); s += b.lastBuildInfo + "\n"; }
            return s;
        }

        /// <summary>Debug hook: refresh every box in the scene from its source (only changed chunks rebuild).</summary>
        public static string DebugRefresh()
        {
            var s = "";
            foreach (var b in FindObjectsByType<BoxView>(FindObjectsSortMode.None)) s += b.Refresh() + "\n";
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

        MeshCollider AoCollider(string name, Mesh mesh)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = AoLayer };
            go.transform.SetParent(root, false);
            var c = go.AddComponent<MeshCollider>();
            c.cookingOptions = MeshColliderCookingOptions.UseFastMidphase;
            c.sharedMesh = mesh;
            return c;
        }

        // ---------------- chunks ----------------
        /// <summary>Which quarter of the box a ground point is in: bit 0 = right (x ≥ 0), bit 1 = back (z ≥ 0).</summary>
        public static int ChunkOf(float x, float z) => (x < 0f ? 0 : 1) + (z < 0f ? 0 : 2);
        public static int ChunkOf(Vector2 p) => ChunkOf(p.x, p.y);

        static void ChunkBounds(int k, out float x0, out float x1, out float z0, out float z1)
        {
            bool right = (k & 1) != 0, back = (k & 2) != 0;
            x0 = right ? 0f : -Land.HX; x1 = right ? Land.HX : 0f;
            z0 = back ? 0f : -Land.HZ; z1 = back ? Land.HZ : 0f;
        }

        MeshBuilder BuildChunk(int k, out int terrainVerts)
        {
            var mb = new MeshBuilder(seed * 31 + k * 7);
            Terrain(mb, k);
            terrainVerts = mb.VertexCount;
            foreach (var f in layout.fields)
                if (f.polygon != null && f.polygon.Length >= 3 && ChunkOf(Centroid(f.polygon)) == k) FieldMesh(mb, f);
            foreach (var p in layout.pieces)
                if (ChunkOf(p.x, p.z) == k) PieceMesh(mb, p);
            Nature(mb, k);
            return mb;
        }

        static Rect FieldBox(FieldData f)
        {
            if (f.polygon == null || f.polygon.Length == 0) return Rect.zero;
            Vector2 min = f.polygon[0], max = f.polygon[0];
            foreach (var q in f.polygon) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static Vector2 Centroid(Vector2[] poly)
        {
            var c = Vector2.zero;
            foreach (var q in poly) c += q;
            return c / poly.Length;
        }

        // FNV-1a over the bits of whatever touches a chunk.
        struct Hash
        {
            public uint h;
            public static Hash New() => new() { h = 2166136261u };
            public void Add(uint v) { unchecked { for (int i = 0; i < 4; i++) { h ^= (v >> (i * 8)) & 0xFF; h *= 16777619u; } } }
            public void Add(int v) => Add((uint)v);
            public void Add(bool v) => Add(v ? 1u : 0u);
            public void Add(float v) => Add((uint)System.BitConverter.SingleToInt32Bits(v));
            public void Add(Vector2 v) { Add(v.x); Add(v.y); }
        }

        /// <summary>Mixes an item's hash into every chunk its box (grown by the reach of nature checks, the path fringe
        /// and the fire's warm pool) overlaps.</summary>
        static void Into(uint[] h, float minX, float minZ, float maxX, float maxZ, uint v)
        {
            const float M = 0.22f;
            for (int k = 0; k < ChunkCount; k++)
            {
                ChunkBounds(k, out float x0, out float x1, out float z0, out float z1);
                if (k % 2 == 0) x0 -= 9f; else x1 += 9f; // outer edges reach anything outside the land
                if (k < 2) z0 -= 9f; else z1 += 9f;
                if (maxX < x0 - M || minX > x1 + M || maxZ < z0 - M || minZ > z1 + M) continue;
                unchecked { h[k] = (h[k] ^ v) * 16777619u + 0x9E3779B9u; }
            }
        }

        void ChunkHashes(uint[] h)
        {
            var g = Hash.New();
            g.Add(seed); g.Add(NX); g.Add(NZ); g.Add(layout.autoNature); g.Add(layout.autoTrees); g.Add(layout.natureDensity);
            g.Add(layout.reeds); g.Add(bakeAO); g.Add(aoRays); g.Add(aoDistance);
            for (int k = 0; k < ChunkCount; k++) h[k] = g.h + (uint)k;
            foreach (var p in layout.pieces)
            {
                var e = Hash.New();
                e.Add((int)p.kind); e.Add(p.x); e.Add(p.z); e.Add(p.yaw); e.Add(p.w); e.Add(p.d); e.Add((int)p.era); e.Add((int)p.roof);
                e.Add(p.storeys); e.Add(p.chimney); e.Add(p.seed); e.Add(p.variant); e.Add(p.grow);
                float r = PieceRadius(p) + (p.kind == PieceKind.Fire ? 0.6f : 0f);
                Into(h, p.x - r, p.z - r, p.x + r, p.z + r, e.h);
            }
            foreach (var f in layout.fields)
            {
                if (f.polygon == null || f.polygon.Length == 0) continue;
                var e = Hash.New();
                Vector2 min = f.polygon[0], max = f.polygon[0];
                foreach (var q in f.polygon) { e.Add(q); min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
                e.Add((int)f.crop); e.Add(f.rowYaw); e.Add(f.fence); e.Add(f.gateEdge); e.Add(f.seed);
                Into(h, min.x - 0.1f, min.y - 0.1f, max.x + 0.1f, max.y + 0.1f, e.h);
            }
            foreach (var path in layout.paths)
            {
                if (path.points == null) continue;
                for (int i = 0; i + 1 < path.points.Length; i++)
                {
                    Vector2 a = path.points[i], b = path.points[i + 1];
                    var e = Hash.New(); e.Add(a); e.Add(b); e.Add((int)path.tier); e.Add(path.cobbled);
                    Into(h, Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), e.h);
                }
            }
            foreach (var c in layout.clearings)
            {
                var e = Hash.New(); e.Add(c.centre); e.Add(c.radius); e.Add((int)c.kind);
                float r = c.radius + 0.12f;
                Into(h, c.centre.x - r, c.centre.y - r, c.centre.x + r, c.centre.y + r, e.h);
            }
            foreach (var t in layout.trees)
            {
                var e = Hash.New(); e.Add(t.pos); e.Add(t.scale); e.Add(t.pine);
                float r = 0.2f * t.scale;
                Into(h, t.pos.x - r, t.pos.y - r, t.pos.x + r, t.pos.y + r, e.h);
            }
            foreach (var cn in layout.canals)
            {
                if (cn == null || cn.Length == 0) continue;
                var e = Hash.New();
                Vector2 min = cn[0], max = cn[0];
                foreach (var q in cn) { e.Add(q); min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
                Into(h, min.x - 0.1f, min.y - 0.1f, max.x + 0.1f, max.y + 0.1f, e.h);
            }
        }

        uint LandHash()
        {
            var e = Hash.New();
            e.Add(land.mirror); e.Add(land.noiseOffset); e.Add(land.pond); e.Add(land.centre); e.Add(seed); e.Add(NX); e.Add(NZ);
            return e.h;
        }

        // ---------------- distance fields (paths per tier, canals) ----------------
        const float FieldRes = 0.025f, PathRange = 0.3f, CanalRange = 0.12f;
        static readonly int FX = Mathf.CeilToInt(2f * Land.HX / FieldRes) + 1, FZ = Mathf.CeilToInt(2f * Land.HZ / FieldRes) + 1;
        readonly float[][] pathField = new float[3][];
        /// <summary>Cobbled paths below road tier (#34/#90), per tier; only filled while there are any.</summary>
        readonly float[][] cobbleField = new float[3][];
        bool anyCobbled;
        float[] canalField, shoreField;
        const float ShoreRange = 0.45f;

        /// <summary>Signed distance to the shoreline (negative in the pond), capped at ±<see cref="ShoreRange"/>, on the
        /// field grid: the ground colour and the free-spot checks read it instead of walking the shore per triangle.</summary>
        void ShoreField()
        {
            Fill(ref shoreField, ShoreRange);
            for (int i = 0; i < shore.Length; i++) Stamp(shoreField, shore[i], shore[(i + 1) % shore.Length], ShoreRange);
            Vector2 min = shore[0], max = shore[0];
            foreach (var q in shore) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
            for (int j = 0; j < FZ; j++)
                for (int i = 0; i < FX; i++)
                {
                    var p = new Vector2(-Land.HX + i * FieldRes, -Land.HZ + j * FieldRes);
                    if (p.x < min.x || p.x > max.x || p.y < min.y || p.y > max.y || !InPolygon(shore, p)) continue;
                    shoreField[j * FX + i] = -shoreField[j * FX + i];
                }
        }

        float ShoreSD(float x, float z) => Sample(shoreField, x, z, ShoreRange);

        /// <summary>Stamps every path (by tier) and canal into distance grids, so painting the ground and finding free
        /// spots don't walk every polyline for every triangle.</summary>
        void PrepareFields()
        {
            for (int t = 0; t < 3; t++) Fill(ref pathField[t], PathRange);
            anyCobbled = false;
            foreach (var path in layout.paths)
                if (path.cobbled && path.tier != PathTier.Road && path.points != null && path.points.Length > 0) { anyCobbled = true; break; }
            if (anyCobbled) for (int t = 0; t < 3; t++) Fill(ref cobbleField[t], PathRange);
            Fill(ref canalField, CanalRange);
            foreach (var path in layout.paths)
            {
                var pts = path.points;
                if (pts == null || pts.Length == 0) continue;
                var f = pathField[(int)path.tier];
                var cf = anyCobbled && path.cobbled && path.tier != PathTier.Road ? cobbleField[(int)path.tier] : null;
                if (pts.Length == 1) { Stamp(f, pts[0], pts[0], PathRange); if (cf != null) Stamp(cf, pts[0], pts[0], PathRange); continue; }
                var last = pts[0];
                for (int i = 1; i < pts.Length; i++)
                {
                    if (i < pts.Length - 1 && (pts[i] - last).sqrMagnitude < 0.05f * 0.05f) continue; // coarser for the field
                    Stamp(f, last, pts[i], PathRange);
                    if (cf != null) Stamp(cf, last, pts[i], PathRange);
                    last = pts[i];
                }
            }
            foreach (var cn in layout.canals)
            {
                if (cn == null || cn.Length == 0) continue;
                if (cn.Length == 1) Stamp(canalField, cn[0], cn[0], CanalRange);
                for (int i = 0; i + 1 < cn.Length; i++) Stamp(canalField, cn[i], cn[i + 1], CanalRange);
            }
        }

        static void Fill(ref float[] f, float v)
        {
            f ??= new float[FX * FZ];
            for (int i = 0; i < f.Length; i++) f[i] = v;
        }

        static void Stamp(float[] f, Vector2 a, Vector2 b, float range)
        {
            int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - range + Land.HX) / FieldRes));
            int i1 = Mathf.Min(FX - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + range + Land.HX) / FieldRes));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - range + Land.HZ) / FieldRes));
            int j1 = Mathf.Min(FZ - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + range + Land.HZ) / FieldRes));
            float abx = b.x - a.x, abz = b.y - a.y, len2 = abx * abx + abz * abz;
            for (int j = j0; j <= j1; j++)
            {
                float pz = -Land.HZ + j * FieldRes;
                for (int i = i0; i <= i1; i++)
                {
                    float px = -Land.HX + i * FieldRes;
                    float t = len2 < 1e-10f ? 0f : Mathf.Clamp01(((px - a.x) * abx + (pz - a.y) * abz) / len2);
                    float dx = px - (a.x + abx * t), dz = pz - (a.y + abz * t);
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    int idx = j * FX + i;
                    if (d < f[idx]) f[idx] = d;
                }
            }
        }

        static float Sample(float[] f, float x, float z, float far)
        {
            if (f == null) return far;
            float fx = (x + Land.HX) / FieldRes, fz = (z + Land.HZ) / FieldRes;
            int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
            if (i < 0 || j < 0 || i >= FX - 1 || j >= FZ - 1) return far;
            float u = fx - i, v = fz - j;
            int o = j * FX + i;
            return (f[o] * (1 - u) + f[o + 1] * u) * (1 - v) + (f[o + FX] * (1 - u) + f[o + FX + 1] * u) * v;
        }

        float CanalDist(float x, float z) => Sample(canalField, x, z, CanalRange);

        /// <summary>The terrain's height with canals carved in (the dug channels sit a little below the land).</summary>
        public float GroundHeight(float x, float z)
        {
            float h = land.Height(x, z);
            float d = CanalDist(x, z);
            return d >= CanalRange ? h : h - 0.045f * (1f - Land.Smooth(0.035f, 0.085f, d));
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

        /// <summary>Open ground for a small prop here (not water, a path, a field, a building or a tree)? For the care layer's weeds (#37).</summary>
        public bool FreeForProp(Vector2 p, float r) => Free(p, r, taken);

        public bool InPond(Vector2 p) => shore != null && InPolygon(shore, p);

        /// <summary>Signed distance to the shoreline: negative in the pond.</summary>
        public float ShoreDistance(Vector2 p)
        {
            if (shore == null) shore = land.Shoreline(64);
            float d = DistToRing(shore, p);
            return InPolygon(shore, p) ? -d : d;
        }

        public static float PieceRadius(PieceData p) => p.kind switch
        {
            PieceKind.Lantern => 0.08f,
            PieceKind.Crates or PieceKind.Bench or PieceKind.LogSeat => 0.14f,
            PieceKind.Fire => 0.48f,
            PieceKind.Woodpile or PieceKind.DryingRack or PieceKind.Well => 0.2f,
            PieceKind.Jetty => 0.5f * Mathf.Sqrt(p.w * p.w + p.d * p.d) + 0.3f,
            _ => 0.5f * Mathf.Sqrt(p.w * p.w + p.d * p.d),
        };

        void Reserve()
        {
            taken.Clear();
            foreach (var p in layout.pieces) taken.Add((p.Pos, PieceRadius(p)));
            foreach (var t in layout.trees) taken.Add((t.pos, 0.2f * t.scale));
        }

        static bool Grassy(ClearingKind k) => k == ClearingKind.Meadow || k == ClearingKind.Flowers;

        bool Free(Vector2 p, float r, List<(Vector2 p, float r)> near)
        {
            if (!land.Inside(p, r + GlassMargin)) return false;
            if (ShoreSD(p.x, p.y) < Mathf.Min(0.12f + r, ShoreRange * 0.999f)) return false;
            foreach (var (q, qr) in near) if ((p - q).sqrMagnitude < (qr + r) * (qr + r)) return false;
            foreach (var c in layout.clearings) if (!Grassy(c.kind) && Vector2.Distance(p, c.centre) < c.radius + r) return false;
            foreach (var f in layout.fields)
            {
                var b = FieldBox(f);
                if (p.x < b.xMin - r - 0.06f || p.x > b.xMax + r + 0.06f || p.y < b.yMin - r - 0.06f || p.y > b.yMax + r + 0.06f) continue;
                if (InPolygon(f.polygon, p) || DistToRing(f.polygon, p) < r + 0.06f) return false;
            }
            for (int t = 0; t < 3; t++)
                if (Sample(pathField[t], p.x, p.y, PathRange) < Mathf.Min(PathWidth((PathTier)t) + 0.05f + r, PathRange * 0.999f)) return false;
            if (CanalDist(p.x, p.y) < Mathf.Min(0.1f + r, CanalRange * 0.999f)) return false;
            return true;
        }

        // ---------------- ground ----------------
        static Color Cobble(float x, float z, float n)
        {
            float s = Land.Noise(x * 22f, z * 22f);
            // Stones and darker grout in a staggered grid, so the square reads as cobbles at default zoom (#91).
            float gx = x * 9f, gz = z * 9f + (Mathf.FloorToInt(x * 9f) & 1) * 0.5f;
            float fx = gx - Mathf.Floor(gx), fz = gz - Mathf.Floor(gz);
            bool grout = fx < 0.14f || fz < 0.14f;
            var stone = Color.Lerp(Color.Lerp(Palette.Stone, Palette.Path, 0.2f), Palette.RockLight, s * 0.5f + (n - 0.5f) * 0.3f);
            return grout ? Color.Lerp(stone, Palette.RockDark, 0.55f) : stone;
        }

        static Color Grass(float n, float y)
        {
            var g = n < 0.42f ? Palette.GrassB : n < 0.66f ? Palette.GrassA : Palette.GrassC;
            return Color.Lerp(g, Palette.Moss, Land.Smooth(Land.G + 0.35f, Land.G + 0.7f, y) * 0.5f);
        }

        static Color Meadow(float n) => Color.Lerp(Color.Lerp(Palette.GrassC, Palette.Sprout, 0.35f), Palette.GrassA, n * 0.5f);

        /// <summary>The ground's colour at a point (pond bed, shore, clearings, fields, canals, paths, grass) and how much
        /// it glows (the warm pool round a fire).</summary>
        public Color GroundColor(float x, float z, float y, out float glowAmount)
        {
            glowAmount = 0f;
            var p = new Vector2(x, z);
            float sd = ShoreSD(x, z);
            if (sd < 0f) return y < Land.WaterY - 0.06f ? Palette.PondBed : Color.Lerp(Palette.SandWet, Palette.PondBed, Mathf.Clamp01(-sd / 0.12f));
            if (sd < 0.05f) return Palette.Sand;
            float n = Land.Fbm(x * 1.4f, z * 1.4f + 5f);
            var col = Grass(n, y);
            bool painted = false;

            foreach (var c in layout.clearings)
            {
                float dx = x - c.centre.x, dz = z - c.centre.y, reach = c.radius + 0.2f;
                if (dx * dx + dz * dz > reach * reach) continue;
                float d = Vector2.Distance(p, c.centre), r = c.radius + 0.08f * (n - 0.5f);
                if (d > r + 0.12f) continue;
                if (d > r)
                {
                    if (c.kind == ClearingKind.Dirt) col = Color.Lerp(col, Palette.Dirt, 0.35f);
                    else if (c.kind == ClearingKind.Cobble) col = Color.Lerp(col, Palette.Path, 0.6f); // a worn sandy edge round the square
                    else if (c.kind == ClearingKind.Green) col = Color.Lerp(col, Palette.Path, 0.45f); // a trodden ring round the green
                    continue;
                }
                col = c.kind switch
                {
                    // The village green: a mown lawn, brighter than the grass round it, so it reads at default zoom (#91).
                    ClearingKind.Green => Color.Lerp(Color.Lerp(Palette.GrassC, Palette.Wheat, 0.3f), Palette.GrassC, n * 0.35f),
                    ClearingKind.Cobble => Cobble(x, z, n),
                    ClearingKind.Yard => Color.Lerp(col, Palette.Dirt, d < r * 0.7f ? 0.55f : 0.3f),
                    ClearingKind.Meadow => Meadow(n),
                    ClearingKind.Flowers => Color.Lerp(Meadow(n), Palette.Flowers[3], 0.15f),
                    _ => d < r * 0.72f ? Palette.Dirt : Color.Lerp(Palette.Dirt, Palette.GrassB, 0.45f),
                };
                painted = !Grassy(c.kind);
            }
            if (!painted)
                foreach (var f in layout.fields)
                {
                    var b = FieldBox(f);
                    if (x < b.xMin || x > b.xMax || z < b.yMin || z > b.yMax || !InPolygon(f.polygon, p)) continue;
                    if (f.crop == CropKind.Gathering) { col = n < 0.5f ? Color.Lerp(Palette.Dirt, col, 0.55f) : col; break; }
                    if (f.crop == CropKind.Pasture) { col = Color.Lerp(col, Palette.GrassC, 0.45f); painted = true; break; }
                    float a = f.rowYaw * Mathf.Deg2Rad;
                    float across = x * Mathf.Cos(a) - z * Mathf.Sin(a);
                    col = (Mathf.FloorToInt(across / 0.08f) & 1) == 0 ? Palette.FieldSoil : Palette.FieldSoilLight;
                    painted = true;
                    break;
                }
            float dc = CanalDist(x, z);
            if (dc < 0.05f) { col = Color.Lerp(Palette.WaterDeep, Palette.Water, dc / 0.05f); painted = true; }
            else if (dc < 0.075f) { col = Color.Lerp(col, Palette.Mud, 0.55f); painted = true; }
            float thrN = (n - 0.5f) * 0.03f;
            bool onPath = false;
            for (int t = 0; t < 3; t++)
            {
                float d = Sample(pathField[t], x, z, PathRange), w = PathWidth((PathTier)t) + thrN;
                if (d >= w) continue;
                col = (PathTier)t switch
                {
                    PathTier.Trail => Color.Lerp(Grass(n, y), Palette.Dirt, 0.6f),
                    PathTier.Road => Cobble(x, z, n),
                    _ => Color.Lerp(Palette.Path, Palette.Dirt, (n - 0.4f) * 0.5f),
                };
                if (anyCobbled && (PathTier)t != PathTier.Road && Sample(cobbleField[t], x, z, PathRange) < w) col = Cobble(x, z, n);
                onPath = true;
            }
            if (!onPath && !painted)
                for (int t = 0; t < 3; t++)
                    if (Sample(pathField[t], x, z, PathRange) < PathWidth((PathTier)t) + thrN + 0.035f) { col = Color.Lerp(col, Palette.Dirt, 0.25f); break; }
            foreach (var pc in layout.pieces)
            {
                if (pc.kind != PieceKind.Fire || Mathf.Abs(pc.x - x) > 0.6f || Mathf.Abs(pc.z - z) > 0.6f) continue;
                float d = Vector2.Distance(p, pc.Pos);
                if (d > 0.6f) continue;
                float k = 1f - d / 0.6f;
                k *= k;
                col = Color.Lerp(col, Color.Lerp(col, Palette.Glow, 0.6f), k * 0.55f);
                glowAmount = Mathf.Max(glowAmount, k * 0.07f);
            }
            return col;
        }

        /// <summary>One quarter of the terrain grid (the grid is even, so the quarters meet on grid lines), in row bands
        /// built in parallel (the ground colour only reads the layout and the fields) and appended in order.</summary>
        void Terrain(MeshBuilder mb, int k)
        {
            int nz = NZ;
            int j0 = (k & 2) != 0 ? nz / 2 : 0, j1 = (k & 2) != 0 ? nz : nz / 2;
            const int Bands = 6;
            var parts = new MeshBuilder[Bands];
            System.Threading.Tasks.Parallel.For(0, Bands, b =>
            {
                var pb = new MeshBuilder(seed * 31 + k * 7 + b * 101);
                TerrainRows(pb, k, j0 + (j1 - j0) * b / Bands, j0 + (j1 - j0) * (b + 1) / Bands);
                parts[b] = pb;
            });
            foreach (var pb in parts) mb.Append(pb);
        }

        void TerrainRows(MeshBuilder mb, int k, int j0, int j1)
        {
            int nx = NX, nz = NZ;
            float HX = Land.HX, HZ = Land.HZ;
            int i0 = (k & 1) != 0 ? nx / 2 : 0, i1 = (k & 1) != 0 ? nx : nx / 2;
            int w = i1 - i0 + 1;
            var h = new float[w * (j1 - j0 + 1)];
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    h[(j - j0) * w + (i - i0)] = GroundHeight(-HX + 2 * HX * i / nx, -HZ + 2 * HZ * j / nz);
            Vector3 P(int i, int j) => new(-HX + 2 * HX * i / nx, h[(j - j0) * w + (i - i0)], -HZ + 2 * HZ * j / nz);
            for (int i = i0; i < i1; i++)
                for (int j = j0; j < j1; j++)
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

        void PieceMesh(MeshBuilder mb, PieceData p)
        {
            // Each piece gets its own builder seeded by its seed, so its look doesn't change when others are added.
            var lb = new MeshBuilder(p.seed * 7919 + (int)p.era * 31 + seed) { jitter = 0.035f };
            float sink = p.kind == PieceKind.Home && p.era != Era.Camp ? 0.04f : 0f;
            lb.M = Matrix4x4.TRS(land.OnGround(p.x, p.z) - Vector3.up * sink, Quaternion.Euler(0, p.yaw, 0), Vector3.one * Mathf.Max(0.02f, p.grow));
            Pieces.Draw(lb, p, LotGround(lb));
            mb.Append(lb);
        }

        void FieldMesh(MeshBuilder mb, FieldData f)
        {
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

        // ---------------- nature ----------------
        /// <summary>Reed spots along the shore: fixed by the seed and the land, so a chunk's reeds don't move when the
        /// layout changes (a spot is skipped while something stands on it).</summary>
        void ReedSpots()
        {
            reedSpots.Clear();
            var r = new System.Random(seed * 3 + 1);
            float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
            for (int i = 0; i < 120 && reedSpots.Count < 34; i++)
            {
                var s = shore[r.Next(0, shore.Length)];
                var dir = (s - land.pond).normalized;
                var at = s + dir * F(-0.03f, 0.1f) + new Vector2(F(-0.03f, 0.03f), F(-0.03f, 0.03f));
                if (!land.Inside(at, GlassMargin)) continue;
                reedSpots.Add((at, reedSpots.Count % 3 == 0));
            }
        }

        /// <summary>
        /// A chunk's nature. Every item comes from a fixed candidate (its spot, size and look hashed from the seed, the
        /// chunk and its index) and draws into its own builder, so a candidate that a new building or path now blocks
        /// simply goes, and nothing else in the chunk moves (tester #33: a new hut made bushes and rocks jump).
        /// </summary>
        void Nature(MeshBuilder mb, int k)
        {
            var near = new List<(Vector2 p, float r)>(taken);
            ChunkBounds(k, out float x0, out float x1, out float z0, out float z1);
            bool In(Vector2 p) => ChunkOf(p) == k;
            float H(int cat, int i, int c) => Land.Hash3(seed * 977 + k * 131 + cat, i, c);
            MeshBuilder Item(int cat, int i) => new(seed * 7919 + k * 104729 + cat * 15485863 + i * 31);
            foreach (var t in layout.trees)
                if (In(t.pos))
                {
                    var tb = new MeshBuilder(Mathf.RoundToInt(t.pos.x * 1000f) * 7919 + Mathf.RoundToInt(t.pos.y * 1000f) + seed);
                    Pieces.Tree(tb, land.OnGround(t.pos), t.scale, t.pine);
                    mb.Append(tb);
                }
            if (layout.reeds)
                for (int i = 0; i < reedSpots.Count; i++)
                {
                    var (at, sprout) = reedSpots[i];
                    if (!In(at)) continue;
                    bool clash = false;
                    foreach (var (q, qr) in taken) if (Vector2.Distance(at, q) < qr) { clash = true; break; }
                    if (clash || CanalDist(at.x, at.y) < 0.06f) continue;
                    var rb = Item(0, i);
                    Pieces.Reed(rb, new Vector3(at.x, Mathf.Max(land.Height(at.x, at.y), Land.WaterY), at.y), sprout ? Palette.Sprout : Palette.LeafB);
                    mb.Append(rb);
                }
            if (!layout.autoNature) return;
            // Candidates per chunk: about a quarter of the old whole-box counts get through the free-spot checks.
            float kd = Mathf.Max(0f, layout.natureDensity);
            Vector2 Spot(int cat, int i) => new(Mathf.Lerp(x0, x1, H(cat, i, 1)), Mathf.Lerp(z0, z1, H(cat, i, 2)));
            if (layout.autoTrees)
                for (int i = 0, n = Mathf.RoundToInt(30 * kd); i < n; i++)
                {
                    var p = Spot(1, i);
                    float xm = land.mirror ? -p.x : p.x;
                    float bias = Land.Smooth(-0.6f, 1.8f, p.y) * 0.6f + Land.Smooth(1.5f, -2.5f, xm) * 0.5f + 0.1f;
                    if (H(1, i, 3) > bias || !Free(p, 0.22f, near)) continue;
                    var ib = Item(1, i);
                    Pieces.Tree(ib, land.OnGround(p), Mathf.Lerp(0.75f, 1.2f, H(1, i, 4)), H(1, i, 5) < 0.35f);
                    mb.Append(ib);
                    near.Add((p, 0.2f));
                }
            for (int i = 0, n = Mathf.RoundToInt(9 * kd); i < n; i++)
            {
                var p = Spot(2, i);
                if (!Free(p, 0.1f, near)) continue;
                var ib = Item(2, i);
                Pieces.Bush(ib, land.OnGround(p), Mathf.Lerp(0.07f, 0.12f, H(2, i, 3)), H(2, i, 4) < 0.33f);
                mb.Append(ib);
                near.Add((p, 0.1f));
            }
            for (int i = 0, n = Mathf.RoundToInt(8 * kd); i < n; i++)
            {
                var p = Spot(3, i);
                if (!Free(p, 0.08f, near)) continue;
                float r = Mathf.Lerp(0.05f, 0.14f, H(3, i, 3));
                var ib = Item(3, i);
                Pieces.Rock(ib, land.OnGround(p), r, H(3, i, 4) < 0.33f);
                mb.Append(ib);
                near.Add((p, r));
            }
            for (int i = 0, n = Mathf.RoundToInt(150 * kd); i < n; i++)
            {
                var p = Spot(4, i);
                if (!Free(p, 0.02f, near)) continue;
                var ib = Item(4, i);
                var g = land.OnGround(p);
                float h = Mathf.Lerp(0.05f, 0.1f, H(4, i, 3));
                float c3 = H(4, i, 4);
                Pieces.GrassTuft(ib, g, h, c3 < 0.33f ? Palette.GrassC : c3 < 0.66f ? Palette.LeafC : Palette.GrassA);
                bool flowers = false;
                foreach (var c in layout.clearings)
                    if (c.kind == ClearingKind.Flowers && Vector2.Distance(p, c.centre) < c.radius) { flowers = true; break; }
                if (flowers || H(4, i, 5) < 0.2f)
                    ib.Ico(g + Vector3.up * h * 0.8f, flowers ? 0.018f : 0.014f, Vector3.one, Palette.Flowers[Mathf.Min(Palette.Flowers.Length - 1, (int)(H(4, i, 6) * Palette.Flowers.Length))], 0.1f);
                mb.Append(ib);
            }
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
        /// <summary>
        /// Soft additive glows in one mesh (one draw, rebuilt on every refresh: it is tiny): per fire a halo behind the flame,
        /// a flat warm pool on the ground, 8 embers; per lantern a small light that follows the night level. The motion of
        /// all of it is in the Glow shader. The fires' smoke is a second tiny mesh with its own alpha-blended material
        /// (one more draw for every fire in the box).
        /// </summary>
        void Glows()
        {
            if (glowGo != null) { Kill(glowGo.GetComponent<MeshFilter>().sharedMesh); Kill(glowGo); glowGo = null; }
            if (smokeGo != null) { Kill(smokeGo.GetComponent<MeshFilter>().sharedMesh); Kill(smokeGo); smokeGo = null; }
            var verts = new List<Vector3>(); var cols = new List<Color>(); var corner = new List<Vector2>(); var data = new List<Vector2>(); var extra = new List<Vector2>(); var tris = new List<int>();
            var sVerts = new List<Vector3>(); var sCols = new List<Color>(); var sCorner = new List<Vector2>(); var sData = new List<Vector2>(); var sTris = new List<int>();
            var corners = new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) };
            void Add(Vector3 c, float radius, float kind, Color col, float x = 0f, float y = 0f)
            {
                int o = verts.Count;
                foreach (var k in corners) { verts.Add(c); cols.Add(col); corner.Add(k); data.Add(new Vector2(radius, kind)); extra.Add(new Vector2(x, y)); }
                tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
            void AddSmoke(Vector3 c, float phase, float period)
            {
                int o = sVerts.Count;
                foreach (var k in corners) { sVerts.Add(c); sCols.Add(Palette.FireSmoke); sCorner.Add(k); sData.Add(new Vector2(phase, period)); }
                sTris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
            foreach (var p in layout.pieces)
            {
                var frame = Matrix4x4.TRS(land.OnGround(p.x, p.z), Quaternion.Euler(0, p.yaw, 0), Vector3.one);
                if (p.kind == PieceKind.Fire || (p.kind == PieceKind.Tavern && p.era == Era.Camp))
                {
                    var foot = frame.MultiplyPoint3x4(Vector3.zero);
                    float phase = Pieces.Phase(p.seed);
                    Add(frame.MultiplyPoint3x4(new Vector3(0, 0.13f, 0)), 0.42f, 0f, Palette.FireHalo, phase, 0.13f);   // halo, behind the flame (y: its height above the ground, so the shader can fade it there)
                    Add(foot + Vector3.up * 0.02f, 0.55f, 4f, Palette.FirePool, phase);                            // warm pool on the ground
                    for (int e = 0; e < 8; e++)                                                                     // embers
                        Add(foot + Vector3.up * 0.14f, 0.012f, 3f, Palette.FireSpark, (e + Land.Hash3(p.seed, e, 11)) / 8f, 1.2f + 0.8f * Land.Hash3(p.seed, e, 12));
                    for (int s = 0; s < 4; s++)                                                                     // smoke
                        AddSmoke(foot + Vector3.up * 0.3f, (s + Land.Hash3(p.seed, s, 13) * 0.4f) / 4f, 4f);
                }
                else if (p.kind == PieceKind.Lantern)
                    Add(frame.MultiplyPoint3x4(new Vector3(0.065f, 0.275f, 0)), 0.2f, 1f + Pieces.Phase(p.seed), Palette.Lamp);
            }
            var bounds = new Bounds(new Vector3(0, 1.5f, 0), new Vector3(2 * Land.HX + 1f, 3f, 2 * Land.HZ + 1f));
            if (verts.Count > 0)
            {
                var m = new Mesh { name = "Glows" };
                m.SetVertices(verts); m.SetColors(cols); m.SetUVs(0, corner); m.SetUVs(1, data); m.SetUVs(2, extra); m.SetTriangles(tris, 0);
                m.bounds = bounds;
                var mat = glow != null ? glow : glowMat != null ? glowMat : glowMat = new Material(Shader.Find("AquariumCiv/View/Glow")) { hideFlags = HideFlags.DontSave };
                glowGo = Spawn("Glows", m, mat, false);
                glowGo.GetComponent<MeshRenderer>().receiveShadows = false;
            }
            if (sVerts.Count > 0)
            {
                var m = new Mesh { name = "Smoke" };
                m.SetVertices(sVerts); m.SetColors(sCols); m.SetUVs(0, sCorner); m.SetUVs(1, sData); m.SetTriangles(sTris, 0);
                m.bounds = bounds;
                var mat = smoke != null ? smoke : smokeMat != null ? smokeMat : smokeMat = new Material(Shader.Find("AquariumCiv/View/FireSmoke")) { hideFlags = HideFlags.DontSave };
                smokeGo = Spawn("Smoke", m, mat, false);
                smokeGo.GetComponent<MeshRenderer>().receiveShadows = false;
            }
        }

        // ---------------- people ----------------
        static uint LookSig(VillagerData v)
        {
            var e = Hash.New(); e.Add(v.seed); e.Add((int)v.era == (int)Era.Camp); e.Add(v.kid); e.Add(v.accent);
            return e.h;
        }

        /// <summary>Keeps the people in step with the layout: a doll whose look is unchanged is moved (and its prop
        /// swapped), a new one is made, a gone one removed. Returns how many were made.</summary>
        int SyncPeople()
        {
            if (peopleRoot == null)
            {
                peopleRoot = new GameObject("People") { hideFlags = HideFlags.DontSave }.transform;
                peopleRoot.SetParent(root, false);
            }
            var seen = new HashSet<int>();
            dolls.Clear();
            int made = 0;
            foreach (var v in plannedPeople ? NoVillagers : layout.villagers)
            {
                int key = v.seed;
                while (!seen.Add(key)) key += 1 << 24;
                uint sig = LookSig(v);
                PegDollView doll;
                if (dollByKey.TryGetValue(key, out var have) && have.doll != null && have.sig == sig) doll = have.doll;
                else
                {
                    if (have.doll != null) Kill(have.doll.gameObject);
                    var look = PegDoll.PickLook(v.seed, v.era == Era.Camp, v.kid, v.accent);
                    doll = PegDollView.Create(peopleRoot, Solid, look, v.kid, v.seed);
                    dollByKey[key] = (doll, sig);
                    made++;
                }
                doll.transform.SetLocalPositionAndRotation(land.OnGround(v.pos), Quaternion.Euler(0, v.yaw, 0));
                if (doll.propKind != v.prop || doll.propSlot != v.slot || (v.prop != PropKind.None && doll.prop == null)) doll.SetProp(v.prop, v.slot);
                dolls.Add(doll);
            }
            var gone = new List<int>();
            foreach (var kv in dollByKey) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int key in gone) { if (dollByKey[key].doll != null) Kill(dollByKey[key].doll.gameObject); dollByKey.Remove(key); }
            return made;
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
        /// <summary>The last AO bake's breakdown (keys, timings), for profiling.</summary>
        [System.NonSerialized] public string aoStats;

        /// <summary>Builds every ray of every sample point (worker threads; plain jobs, no Burst needed).</summary>
        struct AoCommands : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> origins, normals, dirs;
            public PhysicsScene scene;
            public QueryParameters query;
            public Matrix4x4 toWorld;
            public float distance;
            public int rays;
            [NativeDisableParallelForRestriction] public NativeArray<RaycastCommand> commands;

            public void Execute(int k)
            {
                var n = toWorld.MultiplyVector(normals[k]).normalized;
                var o = toWorld.MultiplyPoint3x4(origins[k]) + n * 0.004f;
                var f = Quaternion.FromToRotation(Vector3.forward, n);
                for (int d = 0; d < rays; d++) commands[k * rays + d] = new RaycastCommand(scene, o, f * dirs[d], query, distance);
            }
        }

        /// <summary>Turns each sample point's hits into its AO (a miss leaves a zero normal).</summary>
        struct AoSum : IJobParallelFor
        {
            [ReadOnly] public NativeArray<RaycastHit> hits;
            public NativeArray<float> ao;
            public float distance;
            public int rays;

            public void Execute(int k)
            {
                float occ = 0f;
                for (int d = 0; d < rays; d++)
                {
                    var h = hits[k * rays + d];
                    if (h.normal.sqrMagnitude > 0.5f) occ += 1f - h.distance / distance * 0.6f;
                }
                ao[k] = 1f - 0.85f * occ / rays;
            }
        }

        static long PackKey(Vector3 p, Vector3 n, bool withNormal)
        {
            long x = Mathf.RoundToInt(p.x * 500f) & 0xFFFF, y = Mathf.RoundToInt(p.y * 500f) & 0xFFFF, z = Mathf.RoundToInt(p.z * 500f) & 0xFFFF;
            long k = x | (y << 16) | (z << 32);
            if (withNormal) k |= ((long)(Mathf.RoundToInt(n.x * 7f) + 8) << 48) | ((long)(Mathf.RoundToInt(n.y * 7f) + 8) << 53) | ((long)(Mathf.RoundToInt(n.z * 7f) + 8) << 58);
            return k;
        }

        /// <summary>
        /// Bakes AO into vertex alpha with batched raycasts against the AO colliders of the base and all four chunks: the
        /// rays are built, cast (RaycastCommand) and summed on worker threads. Terrain vertices (the first
        /// <paramref name="terrainVerts"/>) are sampled once per grid point; big faces per corner (contact shadows on
        /// walls); small faces and swaying foliage (grass, leaves, crops) once per face.
        /// </summary>
        sealed class AoWork
        {
            public int nv, nk;
            public int[] keyOf;
            public Mesh mesh;
            public Color[] colors;
            public NativeArray<Vector3> origins, normals, dirs;
            public NativeArray<RaycastCommand> cmds;
            public NativeArray<RaycastHit> hits;
            public NativeArray<float> ao;
            public JobHandle handle;

            public void Dispose()
            {
                handle.Complete();
                if (origins.IsCreated) origins.Dispose();
                if (normals.IsCreated) normals.Dispose();
                if (dirs.IsCreated) dirs.Dispose();
                if (cmds.IsCreated) cmds.Dispose();
                if (hits.IsCreated) hits.Dispose();
                if (ao.IsCreated) ao.Dispose();
            }
        }

        void BakeAO(MeshBuilder mb, int terrainVerts, Mesh mesh) => BakeAO(new List<(MeshBuilder, int, Mesh)> { (mb, terrainVerts, mesh) });

        /// <summary>Bakes several meshes: the rays of each start as soon as its sample points are keyed, so keying the
        /// next mesh overlaps the raycasts of the one before.</summary>
        void BakeAO(List<(MeshBuilder mb, int terrainVerts, Mesh mesh)> items)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var works = new List<AoWork>();
            int keys = 0;
            try
            {
                foreach (var (mb, tv, mesh) in items)
                {
                    var w = AoBegin(mb, tv, mesh);
                    if (w == null) continue;
                    works.Add(w);
                    keys += w.nk;
                    JobHandle.ScheduleBatchedJobs();
                }
                double keyMs = sw.Elapsed.TotalMilliseconds;
                foreach (var w in works)
                {
                    w.handle.Complete();
                    for (int i = 0; i < w.nv; i++) w.colors[i].a = w.ao[w.keyOf[i]];
                    w.mesh.SetColors(w.colors);
                }
                aoStats = $"{works.Count} mesh(es), {keys} keys: keyed {keyMs:0.0} ms, total {sw.Elapsed.TotalMilliseconds:0.0} ms";
            }
            finally { foreach (var w in works) w.Dispose(); }
        }

        AoWork AoBegin(MeshBuilder mb, int terrainVerts, Mesh mesh)
        {
            int nv = mb.verts.Count;
            if (nv == 0) return null;
            var verts = mb.verts.ToArray();
            var norms = mb.normals.ToArray();
            var uvs = mb.uvs.ToArray();
            var w = new AoWork { nv = nv, mesh = mesh, keyOf = new int[nv], colors = mb.colors.ToArray() };
            var keyOf = w.keyOf;
            var origins = w.origins = new NativeArray<Vector3>(nv, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var normals = w.normals = new NativeArray<Vector3>(nv, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            int nk = 0;
            var byKey = new Dictionary<long, int>(nv / 2);
            for (int i = 0; i < terrainVerts; i++)
            {
                long key = PackKey(verts[i], default, false);
                if (!byKey.TryGetValue(key, out int id)) { id = nk++; byKey[key] = id; origins[id] = verts[i]; normals[id] = norms[i]; }
                keyOf[i] = id;
            }
            for (int i = terrainVerts; i + 2 < nv; i += 3)
            {
                Vector3 a = verts[i], b = verts[i + 1], c = verts[i + 2], n = norms[i];
                float area = Vector3.Cross(b - a, c - a).sqrMagnitude; // (2 x area) squared
                if (area < 0.000025f || uvs[i].x > 0.5f) // small faces and swaying foliage: once per face
                {
                    origins[nk] = (a + b + c) * (1f / 3f); normals[nk] = n;
                    keyOf[i] = keyOf[i + 1] = keyOf[i + 2] = nk++;
                    continue;
                }
                for (int q = 0; q < 3; q++)
                {
                    long key = PackKey(verts[i + q], n, true);
                    if (!byKey.TryGetValue(key, out int id)) { id = nk++; byKey[key] = id; origins[id] = verts[i + q]; normals[id] = n; }
                    keyOf[i + q] = id;
                }
            }
            w.nk = nk;
            int nr = aoRays;
            var dirs = w.dirs = new NativeArray<Vector3>(nr, Allocator.TempJob);
            var r = new System.Random(5);
            for (int i = 0; i < nr; i++)
            {
                float u = (i + 0.5f) / nr, v = (float)r.NextDouble();
                float rad = Mathf.Sqrt(u), phi = v * Mathf.PI * 2f + i * 2.4f;
                dirs[i] = new Vector3(rad * Mathf.Cos(phi), rad * Mathf.Sin(phi), Mathf.Sqrt(1 - u));
            }
            w.cmds = new NativeArray<RaycastCommand>(nk * nr, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            w.hits = new NativeArray<RaycastHit>(nk * nr, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            w.ao = new NativeArray<float>(nk, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var build = new AoCommands
            {
                origins = origins, normals = normals, dirs = dirs, scene = gameObject.scene.GetPhysicsScene(),
                query = new QueryParameters(1 << AoLayer, false, QueryTriggerInteraction.Ignore, false),
                toWorld = transform.localToWorldMatrix, distance = aoDistance, rays = nr, commands = w.cmds,
            }.Schedule(nk, 64);
            var cast = RaycastCommand.ScheduleBatch(w.cmds, w.hits, 128, 1, build);
            w.handle = new AoSum { hits = w.hits, ao = w.ao, distance = aoDistance, rays = nr }.Schedule(nk, 64, cast);
            return w;
        }
    }
}
