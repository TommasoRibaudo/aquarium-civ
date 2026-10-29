using System;
using System.Collections.Generic;
using AquariumCiv.Spikes.StyleKit;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.Spikes.Growth.View
{
    public enum Overlay { None, Wear, Tiers, Score, Term }

    /// <summary>A lab cheat, kept on the component so a rerun replays it (rain/grove/channel at a spot, or "era").</summary>
    [Serializable]
    public struct Cheat
    {
        public int year;
        public string kind;
        public float x, z;
    }

    /// <summary>
    /// Growth Lab time-lapse (research 08 §5.3): runs the sim + placement for 120 years once, then draws yearly
    /// snapshot <see cref="year"/> with the style kit (same palette, shader, box, shelf as StyleBox): lots and
    /// buildings from positions, paths from the tier grid, fields from lots, canals as water strips. Debug overlays
    /// (score terms, wear, tiers) tint the ground; candidate bars show the Emilien Fig. 5 view. Edit mode and play mode.
    /// </summary>
    [ExecuteAlways]
    public class GrowthBox : MonoBehaviour
    {
        [Header("Run")]
        public int seed = 7;
        [Range(0, 2)] public int terrain;
        public Variant variant = Variant.Organic;
        public bool scriptedKeeper = true;
        public List<Cheat> cheats = new();
        [Range(0, 120)] public int year = 120;

        [Header("Debug overlay")]
        public Overlay overlay;
        public Kind overlayKind = Kind.Hut;
        public Term overlayTerm = Term.Green;
        public bool showCandidates;

        [Header("Look")]
        public bool bakeAO = true;
        public bool people = true;
        public bool glassPanes = true;
        public bool labels = true;
        public Material solid, water, glass;

        public const float HX = GrowthTerrain.HX, HZ = GrowthTerrain.HZ, G = GrowthTerrain.G, WaterY = GrowthTerrain.WaterY, GlassTop = GrowthTerrain.GlassTop;
        public const int MaxYear = 120;

        [NonSerialized] public GrowthWorld world;
        [NonSerialized] public string lastBuildInfo;
        string worldKey;
        Transform root;
        readonly List<Material> panes = new();
        Snapshot snap;
        TerrainSpec spec;
        float[] canalF, f1, f2, f3, overlayF, commonsF;
        bool clusterLook;              // round 2's Cluster variant: curved paths, yards, outbuildings, commons
        List<PathCurve> curves;
        float overlayMin, overlayMax;
        readonly List<(Vector3 p, string label)> labelList = new();

        void OnEnable() => Build();
        void OnDisable() => Clear();

        void Clear()
        {
            if (root != null) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); }
            foreach (var m in panes) if (m != null) DestroyImmediate(m);
            panes.Clear();
            root = null;
        }

        // ---------------- the run ----------------
        string Key()
        {
            var k = $"{seed}|{terrain}|{variant}|{scriptedKeeper}";
            foreach (var c in cheats) k += $"|{c.year}:{c.kind}:{c.x:0.###}:{c.z:0.###}";
            return k;
        }

        public List<ScriptEvent> Script()
        {
            var list = scriptedKeeper ? GrowthChecks.DefaultScript(terrain) : new List<ScriptEvent>();
            foreach (var c in cheats) list.Add(new ScriptEvent(c.year * 12L, c.kind, c.x, c.z));
            return list;
        }

        public GrowthWorld EnsureWorld()
        {
            var key = Key();
            if (world != null && worldKey == key) return world;
            world = new GrowthWorld(seed, terrain, variant, Script());
            world.RunToYear(MaxYear);
            worldKey = key;
            return world;
        }

        /// <summary>The live world state at the shown year (overlays need layers the snapshot doesn't keep).</summary>
        public GrowthWorld WorldAtYear(int y)
        {
            var w = new GrowthWorld(seed, terrain, variant, Script()) { keepSnapshots = false };
            w.RunToYear(y);
            return w;
        }

        // ---------------- cheats (debug panel) ----------------
        public void AddCheat(string kind, float x, float z)
        {
            cheats.Add(new Cheat { year = year, kind = kind, x = x, z = z });
            Build();
        }

        public void SkipYears(int n) { year = Mathf.Clamp(year + n, 0, MaxYear); Build(); }

        public void ForceNextEra()
        {
            cheats.Add(new Cheat { year = year, kind = "era" });
            Build();
        }

        public void ClearCheats() { cheats.Clear(); Build(); }

        // ---------------- build ----------------
        [ContextMenu("Rebuild")]
        public void Build()
        {
            Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            EnsureWorld();
            long simMs = sw.ElapsedMilliseconds;
            year = Mathf.Clamp(year, 0, world.snapshots.Count - 1);
            snap = world.snapshots[year];
            spec = world.map.spec;
            clusterLook = world.variant == Variant.Cluster;
            PrepareFields();
            PrepareOverlay();

            root = new GameObject("Generated").transform;
            root.SetParent(transform, false);
            root.gameObject.hideFlags = HideFlags.DontSave;
            solid ??= new Material(Shader.Find("AquariumCiv/Spike/Stylized"));
            water ??= solid;

            var mb = new MeshBuilder(seed);
            Terrain(mb);
            if (clusterLook && overlay == Overlay.None) PathRibbons(mb);
            Substrate(mb);
            BoxAndRoom(mb);
            Buildings(mb);
            if (clusterLook) ClusterDressing(mb);
            Nature(mb);
            if (people) People(mb);
            if (showCandidates) Candidates(mb);
            var mesh = mb.ToMesh($"Growth_{year}");
            long aoMs = 0;
            if (bakeAO) { var t = System.Diagnostics.Stopwatch.StartNew(); BakeAO(mesh, mb); aoMs = t.ElapsedMilliseconds; }
            Spawn("Static", mesh, solid, true);
            Spawn("Water", WaterMesh(), water, false);
            if (glassPanes) Glass();
            lastBuildInfo = $"seed {seed} terrain {spec.name} {variant} year {snap.year} {snap.era}: pop {snap.population}, {CountSites()} buildings | " +
                            $"{mesh.vertexCount} verts, AO {aoMs} ms, total {sw.ElapsedMilliseconds} ms (sim {simMs} ms)";
        }

        int CountSites()
        {
            int n = 0;
            foreach (var s in snap.sites) if (!s.retired) n++;
            return n;
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

        // ---------------- fields from the snapshot ----------------
        void PrepareFields()
        {
            int n = SiteMap.N;
            canalF = new float[n]; f1 = new float[n]; f2 = new float[n]; f3 = new float[n];
            for (int c = 0; c < n; c++)
            {
                canalF[c] = (snap.flags[c] & Snapshot.FCanal) != 0 ? 1f : 0f;
                byte t = snap.tier[c];
                f1[c] = t >= 1 ? 1f : 0f; f2[c] = t >= 2 ? 1f : 0f; f3[c] = t >= 3 ? 1f : 0f;
            }
            // Roads read wider than paths; paths a little wider than trails.
            f3 = Dilate(f3, 0.75f);
            f2 = Dilate(f2, 0.45f);
            commonsF = new float[n];
            for (int c = 0; c < n; c++) commonsF[c] = (snap.flags[c] & Snapshot.FCommons) != 0 ? 1f : 0f;
            curves = clusterLook ? PathCurves.Build(snap.tier, snap.flags, world.map.greenX, world.map.greenZ) : null;
        }

        static float[] Dilate(float[] f, float k)
        {
            var o = (float[])f.Clone();
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (f[c] > 0f) continue;
                int i = SiteMap.I(c), j = SiteMap.J(c);
                float best = 0f;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                        float v = f[SiteMap.Index(ni, nj)] * (di != 0 && dj != 0 ? k * 0.7f : k);
                        if (v > best) best = v;
                    }
                o[c] = best;
            }
            return o;
        }

        static float Bilerp(float[] f, float x, float z)
        {
            float fx = (x - SiteMap.X0) / SiteMap.Cell - 0.5f, fz = (z - SiteMap.Z0) / SiteMap.Cell - 0.5f;
            int i0 = Mathf.FloorToInt(fx), j0 = Mathf.FloorToInt(fz);
            float tx = fx - i0, tz = fz - j0;
            float V(int i, int j) => f[SiteMap.Index(Mathf.Clamp(i, 0, SiteMap.NX - 1), Mathf.Clamp(j, 0, SiteMap.NZ - 1))];
            return Mathf.Lerp(Mathf.Lerp(V(i0, j0), V(i0 + 1, j0), tx), Mathf.Lerp(V(i0, j0 + 1), V(i0 + 1, j0 + 1), tx), tz);
        }

        public float Height(float x, float z)
        {
            float h = GrowthTerrain.Height(spec, x, z);
            float cf = Bilerp(canalF, x, z);
            if (cf > 0.02f) h = Mathf.Lerp(h, Mathf.Min(h, WaterY - 0.07f), GrowthTerrain.Smooth(0.05f, 0.55f, cf));
            return h;
        }

        Vector3 OnGround(float x, float z) => new(x, Height(x, z), z);

        Site LotAt(float x, float z, out float lx, out float lz)
        {
            foreach (var s in snap.sites)
            {
                if (!GrowthChecks.HasLot(s.kind)) continue;
                float r = s.yaw * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
                float dx = x - s.x, dz = z - s.z;
                lx = dx * cs - dz * sn; lz = dx * sn + dz * cs;
                if (Mathf.Abs(lx) <= s.w / 2f && Mathf.Abs(lz) <= s.d / 2f) return s;
            }
            lx = lz = 0;
            return null;
        }

        bool HasMarket()
        {
            foreach (var s in snap.sites) if (s.kind == Kind.Market) return true;
            return false;
        }

        // ---------------- ground ----------------
        Color GroundColor(float x, float z, float y)
        {
            if (y < WaterY - 0.01f) return Palette.PondBed;
            if (y < WaterY + 0.03f) return Palette.Sand;
            float n = GrowthTerrain.Fbm(x * 1.4f, z * 1.4f + 5f);
            var era = snap.era;
            var baseCol = Grass(n, y);

            var lot = LotAt(x, z, out float lx, out float lz);
            if (lot != null)
            {
                if (lot.kind == Kind.Farm)
                {
                    if (era == Era.Camp) return n < 0.5f ? Color.Lerp(Palette.Dirt, baseCol, 0.55f) : baseCol; // a gathering patch
                    bool light = ((int)((lx + lot.w / 2f) / 0.08f) & 1) == 0;
                    return light ? Palette.FieldSoilLight : Palette.FieldSoil; // strips run back from the path
                }
                if (Encyclopedia.IsHome(lot.kind) && era != Era.Camp && lz > lot.d * 0.12f)
                {
                    // the croft: a vegetable garden behind the house
                    bool row = ((int)((lx + lot.w / 2f) / 0.06f) & 1) == 0;
                    return row ? Color.Lerp(Palette.FieldSoil, Palette.LeafB, 0.35f) : Palette.SoilLight;
                }
                if (lot.kind != Kind.Farm) return Color.Lerp(baseCol, Palette.Dirt, era == Era.Camp ? 0.35f : 0.2f); // the yard
            }

            float dg = GrowthTerrain.Dist(x, z, world.map.greenX, world.map.greenZ);
            float thr = 0.34f + (n - 0.5f) * 0.25f;
            float t3 = Bilerp(f3, x, z), t2 = Bilerp(f2, x, z), t1 = Bilerp(f1, x, z);
            if (dg < SiteMap.GreenR + 0.02f * n && (snap.flags[Math.Max(0, SiteMap.CellAt(x, z))] & Snapshot.FGreen) != 0)
            {
                if (era == Era.Camp) return dg < 0.3f ? Palette.Dirt : Color.Lerp(Palette.Dirt, Palette.GrassB, 0.45f);
                if (era == Era.Town && HasMarket()) return Cobble(x, z, n); // the green became a market square
                return Color.Lerp(Palette.GrassC, Palette.GrassA, n);
            }
            if (clusterLook)
            {
                // Paths are drawn as curves on top (PathRibbons); the ground shows yards and the commons.
                if (snap.clusters != null)
                    foreach (var k in snap.clusters)
                        if (GrowthTerrain.Dist(x, z, k.x, k.z) < k.r * (0.95f + 0.3f * (n - 0.5f))) return Color.Lerp(baseCol, Palette.Dirt, era == Era.Camp ? 0.5f : 0.4f);
                if (Bilerp(commonsF, x, z) > 0.5f + (n - 0.5f) * 0.3f) return Meadow(n);
                return baseCol;
            }
            if (t3 > thr) return Cobble(x, z, n);
            if (t2 > thr) return Color.Lerp(Palette.Path, Palette.Dirt, (n - 0.4f) * 0.5f);
            if (t1 > thr + 0.06f) return Color.Lerp(baseCol, Palette.Dirt, 0.6f);
            if (t1 > thr - 0.12f) return Color.Lerp(baseCol, Palette.Dirt, 0.25f);
            return baseCol;
        }

        static Color Meadow(float n) => Color.Lerp(Color.Lerp(Palette.GrassC, Palette.Sprout, 0.35f), Palette.GrassA, n * 0.5f);

        static Color Cobble(float x, float z, float n)
        {
            float s = GrowthTerrain.Noise(x * 22f, z * 22f);
            return Color.Lerp(Color.Lerp(Palette.Stone, Palette.Path, 0.35f), Palette.RockDark, s * 0.45f + (n - 0.5f) * 0.2f);
        }

        Color Grass(float n, float y)
        {
            var g = n < 0.42f ? Palette.GrassB : n < 0.66f ? Palette.GrassA : Palette.GrassC;
            return Color.Lerp(g, Palette.Moss, GrowthTerrain.Smooth(G + 0.35f, G + 0.7f, y) * 0.5f);
        }

        void Terrain(MeshBuilder mb)
        {
            const int nx = 120, nz = 88;
            var h = new float[(nx + 1) * (nz + 1)];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                    h[j * (nx + 1) + i] = Height(-HX + 2 * HX * i / nx, -HZ + 2 * HZ * j / nz);
            Vector3 P(int i, int j) => new(-HX + 2 * HX * i / nx, h[j * (nx + 1) + i], -HZ + 2 * HZ * j / nz);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    Vector3 a = P(i, j), b = P(i, j + 1), c = P(i + 1, j + 1), d = P(i + 1, j);
                    if (((i + j) & 1) == 0) { Tri(mb, a, b, c); Tri(mb, a, c, d); }
                    else { Tri(mb, a, b, d); Tri(mb, b, c, d); }
                }
        }

        void Tri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c)
        {
            var m = (a + b + c) / 3f;
            var col = GroundColor(m.x, m.z, m.y);
            if (overlayF != null)
            {
                int cell = SiteMap.CellAt(m.x, m.z);
                if (cell >= 0 && !float.IsNaN(overlayF[cell])) col = Color.Lerp(col, OverlayColor(overlayF[cell]), 0.78f);
            }
            mb.Tri(a, b, c, col);
        }

        // ---------------- round 2: paths as curves ----------------
        static float HalfWidth(float tier) => tier <= 1f ? 0.03f : tier <= 2f ? Mathf.Lerp(0.03f, 0.048f, tier - 1f) : Mathf.Lerp(0.048f, 0.07f, Mathf.Min(1f, tier - 2f));

        Color PathColor(float tier, float x, float z)
        {
            float n = GrowthTerrain.Fbm(x * 1.4f, z * 1.4f + 5f);
            var grass = Grass(n, Height(x, z));
            var trail = Color.Lerp(Palette.Dirt, grass, 0.3f);
            var path = Color.Lerp(Palette.Path, Palette.Dirt, 0.25f + (n - 0.5f) * 0.4f);
            if (tier <= 1f) return trail;
            if (tier <= 2f) return Color.Lerp(trail, path, tier - 1f);
            return Color.Lerp(path, Cobble(x, z, n), Mathf.Min(1f, tier - 2f));
        }

        /// <summary>Each curve as a ribbon draped on the ground, with a soft verge under it and round caps.</summary>
        void PathRibbons(MeshBuilder mb)
        {
            var rb = new MeshBuilder(seed + 11) { jitter = 0.012f };
            foreach (var cv in curves)
            {
                int n = cv.Count;
                var left = new Vector3[n]; var right = new Vector3[n]; var cols = new Color[n]; var hw = new float[n];
                for (int pass = 0; pass < 2; pass++)
                {
                    bool verge = pass == 0;
                    for (int k = 0; k < n; k++)
                    {
                        int a = Mathf.Max(0, k - 1), b = Mathf.Min(n - 1, k + 1);
                        var t = new Vector2(cv.x[b] - cv.x[a], cv.z[b] - cv.z[a]);
                        if (t.sqrMagnitude < 1e-10f) t = Vector2.up;
                        t.Normalize();
                        var nrm = new Vector2(-t.y, t.x);
                        hw[k] = HalfWidth(cv.tier[k]) * (verge ? 1.6f : 1f);
                        float lift = verge ? 0.007f : 0.009f + 0.002f * cv.tier[k];
                        float lx = cv.x[k] + nrm.x * hw[k], lz = cv.z[k] + nrm.y * hw[k], rx = cv.x[k] - nrm.x * hw[k], rz = cv.z[k] - nrm.y * hw[k];
                        left[k] = new Vector3(lx, Height(lx, lz) + lift, lz);
                        right[k] = new Vector3(rx, Height(rx, rz) + lift, rz);
                        var c = PathColor(cv.tier[k], cv.x[k], cv.z[k]);
                        if (verge) c = Color.Lerp(c, Grass(GrowthTerrain.Fbm(cv.x[k] * 1.4f, cv.z[k] * 1.4f + 5f), Height(cv.x[k], cv.z[k])), 0.55f);
                        cols[k] = c;
                    }
                    for (int k = 0; k + 1 < n; k++)
                    {
                        if (Bilerp(canalF, cv.x[k], cv.z[k]) > 0.3f || Bilerp(canalF, cv.x[k + 1], cv.z[k + 1]) > 0.3f) continue; // the bridge
                        rb.TriC(left[k], left[k + 1], right[k + 1], cols[k], cols[k + 1], cols[k + 1]);
                        rb.TriC(left[k], right[k + 1], right[k], cols[k], cols[k + 1], cols[k]);
                    }
                    // round caps: the ends meet other curves at junctions and doors
                    foreach (int k in new[] { 0, n - 1 })
                    {
                        if (Bilerp(canalF, cv.x[k], cv.z[k]) > 0.3f) continue;
                        float lift = verge ? 0.007f : 0.009f + 0.002f * cv.tier[k];
                        var centre = new Vector3(cv.x[k], Height(cv.x[k], cv.z[k]) + lift, cv.z[k]);
                        const int segs = 10;
                        for (int s = 0; s < segs; s++)
                        {
                            float a0 = s / (float)segs * Mathf.PI * 2f, a1 = (s + 1) / (float)segs * Mathf.PI * 2f;
                            float x0 = cv.x[k] + Mathf.Cos(a0) * hw[k], z0 = cv.z[k] + Mathf.Sin(a0) * hw[k];
                            float x1 = cv.x[k] + Mathf.Cos(a1) * hw[k], z1 = cv.z[k] + Mathf.Sin(a1) * hw[k];
                            rb.TriC(centre, new Vector3(x1, Height(x1, z1) + lift, z1), new Vector3(x0, Height(x0, z0) + lift, z0), cols[k], cols[k], cols[k]);
                        }
                    }
                }
            }
            Append(mb, rb);
        }

        // ---------------- round 2: farmsteads round their yards, and what lies between them ----------------
        Func<Vector3, Vector3> GroundFn(MeshBuilder mb) => local =>
        {
            var wp = mb.M.MultiplyPoint3x4(local);
            var g = OnGround(wp.x, wp.z);
            return mb.M.inverse.MultiplyPoint3x4(g);
        };

        /// <summary>Inside a yard and off its paths and lots (for the outbuilding and the woodpile).</summary>
        bool YardSpotFree(float x, float z, float r)
        {
            for (int s = 0; s < 5; s++)
            {
                float px = x + (s == 1 ? r : s == 2 ? -r : 0f), pz = z + (s == 3 ? r : s == 4 ? -r : 0f);
                int c = SiteMap.CellAt(px, pz);
                if (c < 0 || snap.tier[c] >= 1 || (snap.flags[c] & (Snapshot.FCanal | Snapshot.FGreen)) != 0) return false;
                if (LotAt(px, pz, out _, out _) != null) return false;
            }
            return true;
        }

        void ClusterDressing(MeshBuilder mb)
        {
            if (snap.clusters == null) return;
            bool camp = snap.era == Era.Camp;
            foreach (var k in snap.clusters)
            {
                float mx = 0, mz = 0, reach = k.r; int m = 0; int roofId = 0;
                foreach (var s in snap.sites)
                {
                    if (s.cluster != k.id || s.retired) continue;
                    mx += s.x; mz += s.z; m++; roofId = s.id;
                    reach = Mathf.Max(reach, GrowthTerrain.Dist(s.x, s.z, k.x, k.z) + 0.5f * Mathf.Max(s.w, s.d));
                }
                if (m == 0) continue;
                mx /= m; mz /= m;
                var away = new Vector2(k.x - mx, k.z - mz);
                if (away.sqrMagnitude < 0.0004f) { float a = GrowthTerrain.Hash3(k.id, 1, 3) * Mathf.PI * 2f; away = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
                away.Normalize();

                // The outbuilding (a barn, byre or open shed) across the yard from the houses, its front to the yard.
                var ob = new MeshBuilder(k.id * 977 + 13) { jitter = 0.035f };
                foreach (float turn in new[] { 0f, 55f, -55f, 110f, -110f })
                {
                    var v = Rotate(away, turn);
                    float px = k.x + v.x * k.r * 0.55f, pz = k.z + v.y * k.r * 0.55f;
                    if (!YardSpotFree(px, pz, 0.1f)) continue;
                    float yaw = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg + (GrowthTerrain.Hash3(k.id, 2, 5) - 0.5f) * 30f;
                    ob.M = Matrix4x4.TRS(OnGround(px, pz), Quaternion.Euler(0, yaw, 0), Vector3.one);
                    if (camp) GrowthArt.DryingRack(ob, Vector3.zero);
                    else if (GrowthTerrain.Hash3(k.id, 3, 5) < 0.6f) GrowthArt.Barn(ob, Vector3.zero, 0.2f, 0.15f, 0.12f, GrowthArt.Roof(roofId, snap.era));
                    else { GrowthArt.Shed(ob, Vector3.zero, 0.2f, 0.14f, 0.12f, Palette.Thatch, Palette.Timber); GrowthArt.Woodpile(ob, new Vector3(0, 0, 0.01f), 90f); }
                    break;
                }
                // A woodpile or crates by the yard's edge.
                foreach (float turn in new[] { 90f, -90f, 150f, -150f })
                {
                    var v = Rotate(away, turn);
                    float px = k.x + v.x * k.r * 0.8f, pz = k.z + v.y * k.r * 0.8f;
                    if (!YardSpotFree(px, pz, 0.06f)) continue;
                    ob.M = Matrix4x4.TRS(OnGround(px, pz), Quaternion.Euler(0, Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg + 90f, 0), Vector3.one);
                    if (GrowthTerrain.Hash3(k.id, 4, 5) < 0.6f) GrowthArt.Woodpile(ob, Vector3.zero, 0f); else GrowthArt.Crates(ob, Vector3.zero);
                    break;
                }
                Append(mb, ob);

                // Between the clusters: hedges, fruit trees, kitchen gardens and woodpiles (only where the land is open).
                for (int q = 0; q < 9; q++)
                {
                    float h = GrowthTerrain.Hash3(k.id, q, 71);
                    float ang = (q + GrowthTerrain.Hash3(k.id, q, 72)) / 9f * Mathf.PI * 2f;
                    float rr = reach + 0.12f + 0.14f * GrowthTerrain.Hash3(k.id, q, 73);
                    float px = k.x + Mathf.Cos(ang) * rr, pz = k.z + Mathf.Sin(ang) * rr;
                    var ib = new MeshBuilder(k.id * 131 + q) { jitter = 0.035f };
                    float faceYard = Mathf.Atan2(k.x - px, k.z - pz) * Mathf.Rad2Deg;
                    if (h < 0.32f)
                    {
                        // a hedge along the cluster's edge
                        var tg = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang));
                        for (int b = -2; b <= 2; b++)
                        {
                            float bx = px + tg.x * b * 0.065f, bz = pz + tg.y * b * 0.065f;
                            if (!Open(bx, bz, 0.04f)) continue;
                            GrowthArt.Bush(ib, OnGround(bx, bz) - Vector3.up * 0.012f, ib.Range(0.042f, 0.055f), false);
                        }
                    }
                    else if (h < 0.56f)
                    {
                        if (Open(px, pz, 0.11f)) FruitTree(ib, px, pz, 0.5f + 0.15f * GrowthTerrain.Hash3(k.id, q, 74));
                    }
                    else if (h < 0.8f && !camp)
                    {
                        if (Open(px, pz, 0.13f))
                        {
                            ib.M = Matrix4x4.TRS(OnGround(px, pz), Quaternion.Euler(0, faceYard, 0), Vector3.one);
                            Garden(ib, 0.22f, 0.17f);
                        }
                    }
                    else if (h < 0.9f)
                    {
                        if (Open(px, pz, 0.1f))
                        {
                            ib.M = Matrix4x4.TRS(OnGround(px, pz), Quaternion.Euler(0, faceYard + 90f, 0), Vector3.one);
                            GrowthArt.Woodpile(ib, Vector3.zero, 0f);
                        }
                    }
                    Append(mb, ib);
                }
            }
        }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>A kitchen garden: dug soil, rows of greens, a wattle fence (local space of mb.M).</summary>
        void Garden(MeshBuilder mb, float w, float d)
        {
            var ground = GroundFn(mb);
            var a = ground(new Vector3(-w / 2f, 0, -d / 2f)) + Vector3.up * 0.006f; var b = ground(new Vector3(-w / 2f, 0, d / 2f)) + Vector3.up * 0.006f;
            var c = ground(new Vector3(w / 2f, 0, d / 2f)) + Vector3.up * 0.006f; var e = ground(new Vector3(w / 2f, 0, -d / 2f)) + Vector3.up * 0.006f;
            mb.Quad(a, b, c, e, Palette.FieldSoil);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 4; col++)
                {
                    var p = ground(new Vector3(-w / 2f + (col + 0.5f) * w / 4f, 0, -d / 2f + (row + 0.5f) * d / 3f));
                    mb.Ico(p + Vector3.up * 0.016f, 0.02f, new Vector3(1f, 0.7f, 1f), row % 2 == 0 ? Palette.LeafB : Palette.Sprout, 0.15f);
                }
            GrowthArt.Fence(mb, ground, new[] { new Vector3(-w / 2f + 0.06f, 0, -d / 2f), new Vector3(-w / 2f, 0, -d / 2f), new Vector3(-w / 2f, 0, d / 2f), new Vector3(w / 2f, 0, d / 2f), new Vector3(w / 2f, 0, -d / 2f), new Vector3(w / 2f - 0.06f, 0, -d / 2f) });
        }

        void FruitTree(MeshBuilder mb, float x, float z, float s)
        {
            Tree(mb, new Vector2(x, z), s, false);
            var g = OnGround(x, z);
            for (int f = 0; f < 5; f++)
            {
                float a = f * 1.3f + mb.Range(0f, 1f);
                mb.Ico(g + new Vector3(Mathf.Cos(a) * 0.15f * s, (0.38f + mb.Range(0f, 0.2f)) * s, Mathf.Sin(a) * 0.15f * s), 0.016f, Vector3.one, f % 2 == 0 ? Palette.Ember : Palette.Wheat, 0.1f);
            }
        }

        // ---------------- overlay ----------------
        void PrepareOverlay()
        {
            overlayF = null;
            labelList.Clear();
            switch (overlay)
            {
                case Overlay.None: return;
                case Overlay.Wear:
                    overlayF = new float[SiteMap.N];
                    for (int c = 0; c < SiteMap.N; c++) overlayF[c] = snap.wear[c] < 0.3f ? float.NaN : Mathf.Clamp01(snap.wear[c] / PathNet.TRoad);
                    overlayMin = 0; overlayMax = 1;
                    return;
                case Overlay.Tiers:
                    overlayF = new float[SiteMap.N];
                    for (int c = 0; c < SiteMap.N; c++)
                    {
                        bool laid = (snap.flags[c] & Snapshot.FLaid) != 0;
                        // -1 = emergent path (never laid), else tier / 3
                        overlayF[c] = snap.tier[c] >= 2 && !laid ? -1f : snap.tier[c] / 3f;
                    }
                    return;
                default:
                {
                    var w = WorldAtYear(year);
                    var placer = new Placer();
                    overlayF = placer.TermGrid(w, overlayKind, overlay == Overlay.Term ? overlayTerm : (Term?)null);
                    overlayMin = float.MaxValue; overlayMax = float.MinValue;
                    foreach (float v in overlayF)
                        if (!float.IsNaN(v)) { overlayMin = Mathf.Min(overlayMin, v); overlayMax = Mathf.Max(overlayMax, v); }
                    if (overlay == Overlay.Term) { overlayMin = -1; overlayMax = 1; }
                    return;
                }
            }
        }

        Color OverlayColor(float v)
        {
            if (overlay == Overlay.Tiers)
            {
                if (v < 0f) return new Color(0.85f, 0.2f, 0.85f);                // emergent path
                if (v <= 0f) return new Color(0.35f, 0.4f, 0.35f);
                return v < 0.4f ? new Color(0.95f, 0.85f, 0.3f) : v < 0.7f ? new Color(0.95f, 0.55f, 0.15f) : new Color(0.85f, 0.15f, 0.1f);
            }
            if (float.IsNaN(v)) return new Color(0.2f, 0.2f, 0.22f);           // invalid spot
            float t = Mathf.InverseLerp(overlayMin, overlayMax, v);
            return Ramp(t);
        }

        /// <summary>Blue (low) → white → red (high).</summary>
        public static Color Ramp(float t) => t < 0.5f
            ? Color.Lerp(new Color(0.15f, 0.3f, 0.85f), Color.white, t * 2f)
            : Color.Lerp(Color.white, new Color(0.9f, 0.15f, 0.1f), (t - 0.5f) * 2f);

        void Candidates(MeshBuilder mb)
        {
            if (snap.placements.Count == 0) return;
            var keep = mb.jitter; mb.jitter = 0f;
            foreach (var pl in snap.placements)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var c in pl.cands) if (c.valid) { lo = Mathf.Min(lo, c.score); hi = Mathf.Max(hi, c.score); }
                for (int i = 0; i < pl.cands.Count; i++)
                {
                    var c = pl.cands[i];
                    var g = OnGround(c.x, c.z);
                    if (!c.valid) { mb.Prism(g, 0.016f, 0.016f, 0.02f, 4, new Color(0.25f, 0.25f, 0.28f)); continue; }
                    float t = hi > lo ? (c.score - lo) / (hi - lo) : 1f;
                    mb.Prism(g, 0.032f, 0.032f, 0.04f + 0.45f * t, 6, Ramp(t));
                    if (i == pl.chosen)
                    {
                        mb.emission = 0.8f;
                        mb.Prism(g, 0.06f, 0.0f, 0.9f, 6, new Color(1f, 0.95f, 0.3f));
                        mb.emission = 0f;
                    }
                }
                if (labels && pl.chosen >= 0)
                    labelList.Add((OnGround(pl.cands[pl.chosen].x, pl.cands[pl.chosen].z) + Vector3.up * 0.8f, $"{pl.kind} {pl.cands[pl.chosen].score:0.00} ({pl.cands.Count} cands)"));
            }
            mb.jitter = keep;
        }

        // ---------------- buildings ----------------
        void Buildings(MeshBuilder mb)
        {
            var era = snap.era;
            foreach (var s in snap.sites)
            {
                if (s.retired) continue;
                // Each lot gets its own builder seeded by its id, so its look doesn't change when others are added.
                var lb = new MeshBuilder(s.id * 7919 + (int)era * 31 + seed) { jitter = 0.035f };
                lb.M = Matrix4x4.TRS(OnGround(s.x, s.z), Quaternion.Euler(0, s.yaw, 0), Vector3.one);
                DrawSite(lb, s, era);
                Append(mb, lb);
            }
            // Market stalls on the green in the Town era.
            if (snap.stalls > 0)
            {
                var lb = new MeshBuilder(seed + 5);
                for (int k = 0; k < snap.stalls; k++)
                {
                    float a = (k / (float)snap.stalls) * Mathf.PI * 2f + 0.6f;
                    float x = world.map.greenX + Mathf.Cos(a) * 0.27f, z = world.map.greenZ + Mathf.Sin(a) * 0.27f;
                    var cols = new[] { Palette.Terracotta, Palette.Tunics[1], Palette.Tunics[2], Palette.Canvas, Palette.Tunics[3] };
                    lb.M = Matrix4x4.identity;
                    GrowthArt.Stall(lb, OnGround(x, z), -a * Mathf.Rad2Deg - 90f, cols[k % cols.Length]);
                }
                Append(mb, lb);
            }
            // Bridges where a worn path crosses a canal.
            var bb = new MeshBuilder(seed + 9);
            for (int c = 0; c < SiteMap.N; c++)
            {
                if ((snap.flags[c] & Snapshot.FCanal) == 0 || snap.tier[c] < 1) continue;
                int i = SiteMap.I(c), j = SiteMap.J(c);
                bool alongX = (i > 0 && (snap.flags[c - 1] & Snapshot.FCanal) != 0) || (i < SiteMap.NX - 1 && (snap.flags[c + 1] & Snapshot.FCanal) != 0);
                var p = new Vector3(SiteMap.CX(i), WaterY + 0.04f, SiteMap.CZ(j));
                bb.Box(p, alongX ? new Vector3(0.11f, 0.02f, 0.2f) : new Vector3(0.2f, 0.02f, 0.11f), Palette.WoodLight);
                foreach (int s in new[] { -1, 1 })
                    bb.Box(p + (alongX ? new Vector3(s * 0.05f, 0.04f, 0) : new Vector3(0, 0.04f, s * 0.05f)), alongX ? new Vector3(0.012f, 0.012f, 0.2f) : new Vector3(0.2f, 0.012f, 0.012f), Palette.Timber);
            }
            Append(mb, bb);
        }

        int DoorTier(Site s) => s.doorCell >= 0 && s.doorCell < SiteMap.N ? snap.tier[s.doorCell] : 0;

        void DrawSite(MeshBuilder mb, Site s, Era era)
        {
            float w = s.w, d = s.d;
            Vector3 Front(float depth) => new(0, 0, -d / 2f + 0.02f + depth / 2f);
            Func<Vector3, Vector3> ground = local =>
            {
                var wp = mb.M.MultiplyPoint3x4(local);
                var g = OnGround(wp.x, wp.z);
                return mb.M.inverse.MultiplyPoint3x4(g);
            };
            bool camp = era == Era.Camp, town = era == Era.Town;
            float dg = GrowthTerrain.Dist(s.x, s.z, world.map.greenX, world.map.greenZ);
            switch (s.kind)
            {
                case Kind.Camp:
                    if (camp) GrowthArt.Firepit(mb, Vector3.zero);
                    break;
                case Kind.Well:
                    GrowthArt.Well(mb, Vector3.zero, !camp);
                    break;
                case Kind.Hut:
                case Kind.Cottage:
                case Kind.Shop:
                {
                    if (camp)
                    {
                        GrowthArt.Teepee(mb, new Vector3(0, 0, -d * 0.12f), mb.Range(0.14f, 0.17f), mb.Range(0.42f, 0.5f), s.id % 2 == 0 ? Palette.Hide : Palette.Canvas, 0f);
                        if (s.id % 3 == 0) GrowthArt.DryingRack(mb, new Vector3(w * 0.25f, 0, d * 0.25f));
                        break;
                    }
                    int tier = DoorTier(s);
                    bool big = s.kind == Kind.Hut && (tier >= 2 || town);
                    bool townhouse = town && s.kind != Kind.Cottage && (tier >= 3 || dg < 1.1f || s.kind == Kind.Shop);
                    float hw = big ? w * 0.86f : w * 0.72f, hd = townhouse ? 0.3f : big ? 0.28f : 0.25f;
                    int storeys = townhouse ? 2 : 1;
                    float wallH = townhouse ? 0.2f : big ? 0.27f : 0.22f;
                    var roof = !big && !townhouse ? Palette.Thatch : GrowthArt.Roof(s.id, era);
                    GrowthArt.House(mb, Front(hd), hw, hd, wallH, big ? 0.24f : 0.2f, GrowthArt.Wall(s.id), roof, big && s.id % 2 == 0, true, storeys);
                    if (s.kind == Kind.Shop)
                    {
                        var aw = Front(hd) + new Vector3(0, 0.27f, -hd / 2f - 0.06f);
                        mb.Box(aw, new Vector3(hw * 0.9f, 0.012f, 0.12f), Palette.Terracotta, Quaternion.Euler(-18, 0, 0), true);
                        GrowthArt.Crates(mb, Front(hd) + new Vector3(-hw * 0.3f, 0, -hd / 2f - 0.08f));
                    }
                    break;
                }
                case Kind.Farm:
                {
                    if (camp)
                    {
                        for (int k = 0; k < 4; k++)
                            GrowthArt.Bush(mb, ground(new Vector3(mb.Range(-w, w) * 0.35f, 0, mb.Range(-d, d) * 0.35f)), mb.Range(0.06f, 0.09f), true);
                        GrowthArt.DryingRack(mb, ground(new Vector3(0, 0, -d * 0.3f)));
                        break;
                    }
                    // Crops rotate every couple of years: ripe wheat, green shoots, or fallow.
                    float h = GrowthTerrain.Hash3(s.id, snap.year / 3, 4);
                    var crop = h < 0.5f ? Palette.Wheat : h < 0.85f ? Palette.Sprout : Palette.FieldSoil;
                    GrowthArt.Field(mb, ground, w, d, crop, true);
                    break;
                }
                case Kind.Lumbermill:
                    if (camp) { GrowthArt.Woodpile(mb, new Vector3(0, 0, -d * 0.1f), 20f); GrowthArt.Log(mb, new Vector3(w * 0.2f, 0.03f, d * 0.2f), 0.035f, 0.3f, 70f, Palette.Bark); }
                    else GrowthArt.Lumbermill(mb, Vector3.zero, w, d);
                    break;
                case Kind.Windmill:
                    if (camp) GrowthArt.Granary(mb, Vector3.zero);
                    else GrowthArt.Windmill(mb, Vector3.zero, s.yaw, town ? Palette.Stone : Palette.Plaster, town);
                    break;
                case Kind.Market:
                    if (camp)
                    {
                        mb.Box(new Vector3(0, 0.005f, 0), new Vector3(0.3f, 0.01f, 0.2f), Palette.Hide);
                        GrowthArt.Crates(mb, new Vector3(-0.05f, 0, 0));
                        GrowthArt.DryingRack(mb, new Vector3(0.1f, 0, d * 0.3f));
                    }
                    else GrowthArt.MarketHall(mb, Vector3.zero, w, d, town);
                    break;
                case Kind.Tavern:
                    if (camp) GrowthArt.Firepit(mb, new Vector3(0, 0, 0));
                    else
                    {
                        GrowthArt.House(mb, Front(0.34f), w * 0.9f, 0.34f, town ? 0.2f : 0.28f, 0.26f, Palette.PlasterWarm, town ? Palette.Terracotta : Palette.Thatch, true, true, town ? 2 : 1);
                        var sign = new Vector3(w * 0.5f, 0, -d / 2f + 0.02f);
                        mb.Box(sign + new Vector3(0, 0.16f, 0), new Vector3(0.02f, 0.32f, 0.02f), Palette.Timber);
                        mb.Box(sign + new Vector3(-0.05f, 0.27f, 0), new Vector3(0.08f, 0.06f, 0.01f), Palette.Ember);
                        mb.Box(new Vector3(-w * 0.25f, 0.04f, -d / 2f + 0.01f), new Vector3(0.18f, 0.016f, 0.05f), Palette.WoodLight);
                    }
                    break;
                case Kind.Church:
                    if (camp) GrowthArt.StandingStones(mb, Vector3.zero);
                    else GrowthArt.Church(mb, Vector3.zero, w, d);
                    break;
                case Kind.Watermill: GrowthArt.Watermill(mb, Vector3.zero, w, d); break;
                case Kind.Smithy: GrowthArt.Smithy(mb, Vector3.zero, w, d); break;
                case Kind.Dyer: GrowthArt.Dyer(mb, Vector3.zero, w, d); break;
            }
        }

        static void Append(MeshBuilder dst, MeshBuilder src)
        {
            int o = dst.verts.Count;
            dst.verts.AddRange(src.verts); dst.normals.AddRange(src.normals); dst.colors.AddRange(src.colors); dst.uvs.AddRange(src.uvs);
            foreach (int t in src.tris) dst.tris.Add(t + o);
        }

        // ---------------- nature ----------------
        bool Open(float x, float z, float r, bool allowTrail = false)
        {
            if (Mathf.Abs(x) > HX - r || Mathf.Abs(z) > HZ - r) return false;
            if (Height(x, z) < WaterY + 0.04f) return false;
            int c = SiteMap.CellAt(x, z);
            if (c < 0) return false;
            if ((snap.flags[c] & (Snapshot.FCanal | Snapshot.FGreen)) != 0) return false;
            if (Bilerp(f1, x, z) > (allowTrail ? 0.7f : 0.25f)) return false;
            foreach (var s in snap.sites)
            {
                if (s.retired && s.kind != Kind.Camp) continue;
                float rr = GrowthChecks.HasLot(s.kind) ? 0.5f * Mathf.Sqrt(s.w * s.w + s.d * s.d) : 0.25f;
                if (GrowthTerrain.Dist(x, z, s.x, s.z) > rr + r + 0.02f) continue;
                if (!GrowthChecks.HasLot(s.kind)) return false;
                if (SiteMap.RectGap(s.x, s.z, s.yaw, s.w, s.d, x, z, 0, r * 1.6f, r * 1.6f) < 0.01f) return false;
            }
            return true;
        }

        void Nature(MeshBuilder mb)
        {
            var nb = new MeshBuilder(seed * 3 + 1);
            // Woods: one tree per 2×2 block of forest cells (the forest layer: hills, groves; cut back by mills).
            for (int j = 0; j < SiteMap.NZ; j += 2)
                for (int i = 0; i < SiteMap.NX; i += 2)
                {
                    int n = 0;
                    for (int dj = 0; dj < 2; dj++)
                        for (int di = 0; di < 2; di++)
                            if (i + di < SiteMap.NX && j + dj < SiteMap.NZ && (snap.flags[SiteMap.Index(i + di, j + dj)] & Snapshot.FForest) != 0) n++;
                    float hsh = GrowthTerrain.Hash3(i, j, seed);
                    if (n < 2 || hsh > 0.8f) continue;
                    float x = SiteMap.X0 + (i + 1f) * SiteMap.Cell + (GrowthTerrain.Hash3(i, j, 1) - 0.5f) * 0.12f;
                    float z = SiteMap.Z0 + (j + 1f) * SiteMap.Cell + (GrowthTerrain.Hash3(i, j, 2) - 0.5f) * 0.12f;
                    if (!Open(x, z, 0.12f)) continue;
                    var tb = new MeshBuilder(i * 131 + j * 17);
                    float y = Height(x, z);
                    Tree(tb, new Vector2(x, z), 0.7f + 0.45f * GrowthTerrain.Hash3(i, j, 3), GrowthTerrain.Hash3(i, j, 4) < 0.25f + (y - G) * 0.6f);
                    Append(nb, tb);
                }
            // Bushes, rocks, grass and flowers on fixed spots; whatever gets built over simply isn't drawn.
            for (int k = 0; k < 900; k++)
            {
                float x = -HX + 2 * HX * GrowthTerrain.Hash3(seed, k, 21), z = -HZ + 2 * HZ * GrowthTerrain.Hash3(seed, k, 22);
                float kind = GrowthTerrain.Hash3(seed, k, 23);
                var tb = new MeshBuilder(k + 77);
                if (kind < 0.03f)
                {
                    if (!Open(x, z, 0.1f)) continue;
                    GrowthArt.Bush(tb, OnGround(x, z) - Vector3.up * 0.02f, tb.Range(0.07f, 0.11f), snap.era == Era.Camp && k % 3 == 0);
                }
                else if (kind < 0.05f)
                {
                    if (!Open(x, z, 0.08f)) continue;
                    float r = tb.Range(0.05f, 0.12f);
                    tb.Ico(OnGround(x, z) + Vector3.up * r * 0.2f, r, new Vector3(1.2f, 0.6f, 1f), k % 3 == 0 ? Palette.RockLight : Palette.Rock, 0.22f, Palette.RockDark);
                }
                else
                {
                    if (!Open(x, z, 0.02f)) continue;
                    var g = OnGround(x, z);
                    float h = tb.Range(0.05f, 0.09f) * (snap.era == Era.Camp ? 1.3f : 1f);
                    var col = tb.Range(0, 3) switch { 0 => Palette.GrassC, 1 => Palette.LeafC, _ => Palette.GrassA };
                    tb.sway = 1f;
                    for (int b = 0; b < 3; b++)
                    {
                        float a = b * 2.1f + tb.Range(0f, 1f);
                        var lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * h * 0.35f;
                        var side = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.012f;
                        tb.Tri(g - side, g + lean + Vector3.up * h, g + side, col);
                        tb.Tri(g + side, g + lean + Vector3.up * h, g - side, col);
                    }
                    tb.sway = 0f;
                    if (k % 5 == 0) tb.Ico(g + Vector3.up * h * 0.8f, 0.013f, Vector3.one, Palette.Flowers[k % Palette.Flowers.Length], 0.1f);
                }
                Append(nb, tb);
            }
            // The commons: a meadow with flowers, some of it an old orchard (Cluster variant).
            if (clusterLook)
                for (int c = 0; c < SiteMap.N; c++)
                {
                    if ((snap.flags[c] & Snapshot.FCommons) == 0) continue;
                    int i = SiteMap.I(c), j = SiteMap.J(c);
                    float hh = GrowthTerrain.Hash3(i, j, seed + 5);
                    float x = SiteMap.CX(i) + (GrowthTerrain.Hash3(i, j, 6) - 0.5f) * 0.07f, z = SiteMap.CZ(j) + (GrowthTerrain.Hash3(i, j, 7) - 0.5f) * 0.07f;
                    var tb = new MeshBuilder(c + 9001);
                    if ((i + 2 * j) % 4 == 0 && hh < 0.5f && snap.era != Era.Camp)
                    {
                        if (Open(x, z, 0.1f)) FruitTree(tb, x, z, 0.55f + 0.2f * hh);
                    }
                    else if (hh < 0.75f && Open(x, z, 0.02f))
                    {
                        var g = OnGround(x, z);
                        for (int f = 0; f < 3; f++)
                            tb.Ico(g + new Vector3(tb.Range(-0.03f, 0.03f), tb.Range(0.03f, 0.06f), tb.Range(-0.03f, 0.03f)), 0.012f, Vector3.one, Palette.Flowers[(c + f) % Palette.Flowers.Length], 0.1f);
                    }
                    Append(nb, tb);
                }
            // Reeds round the pond.
            for (int k = 0; k < 30; k++)
            {
                float a = GrowthTerrain.Hash3(seed, k, 31) * Mathf.PI * 2f, dd = 0.82f + 0.18f * GrowthTerrain.Hash3(seed, k, 32);
                float x = spec.pondX + Mathf.Cos(a) * dd, z = spec.pondZ + Mathf.Sin(a) * dd * 0.9f;
                if (Mathf.Abs(x) > HX - 0.05f || Mathf.Abs(z) > HZ - 0.05f || !Open(x, z, 0.02f, true)) continue;
                var tb = new MeshBuilder(k + 501);
                tb.sway = 0.8f;
                tb.Prism(OnGround(x, z), 0.012f, 0f, tb.Range(0.14f, 0.24f), 3, k % 3 == 0 ? Palette.Sprout : Palette.LeafB, Quaternion.Euler(tb.Range(-8f, 8f), 0, tb.Range(-8f, 8f)));
                Append(nb, tb);
            }
            Append(mb, nb);
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

        // ---------------- people ----------------
        void People(MeshBuilder mb)
        {
            int count = Mathf.Clamp(snap.population / 2 + 2, 3, 26);
            var spots = new List<int>();
            for (int c = 0; c < SiteMap.N; c++)
                if (snap.tier[c] >= 1 || (snap.flags[c] & Snapshot.FGreen) != 0) spots.Add(c);
            if (spots.Count == 0) return;
            var rng = new System.Random(seed * 31 + snap.year);
            for (int k = 0; k < count; k++)
            {
                int c = spots[(int)(GrowthTerrain.Hash3(seed, snap.year, k) * spots.Count)];
                float x = SiteMap.CXc(c) + (GrowthTerrain.Hash3(k, snap.year, 1) - 0.5f) * 0.08f, z = SiteMap.CZc(c) + (GrowthTerrain.Hash3(k, snap.year, 2) - 0.5f) * 0.08f;
                if (Height(x, z) < WaterY + 0.03f) continue;
                var pb = new MeshBuilder(seed * 100 + k) { jitter = 0.02f };
                pb.M = Matrix4x4.TRS(OnGround(x, z), Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Vector3.one);
                Villager.BuildMesh(pb, rng, k % 5 == 4, snap.era == Era.Camp);
                Append(mb, pb);
            }
        }

        // ---------------- the box, shelf and wall (as StyleBox) ----------------
        void Substrate(MeshBuilder mb)
        {
            var corners = new[] { new Vector2(-HX, -HZ), new Vector2(HX, -HZ), new Vector2(HX, HZ), new Vector2(-HX, HZ) };
            var sb = new MeshBuilder(seed + 2);
            for (int side = 0; side < 4; side++)
            {
                var p0 = corners[side]; var p1 = corners[(side + 1) % 4];
                int segs = Mathf.RoundToInt(Vector2.Distance(p0, p1) / 0.125f);
                var outward = new Vector3(p1.y - p0.y, 0, -(p1.x - p0.x)).normalized;
                for (int s = 0; s < segs; s++)
                {
                    var a = Vector2.Lerp(p0, p1, s / (float)segs); var b = Vector2.Lerp(p0, p1, (s + 1) / (float)segs);
                    float[] la = Layers(a), lb = Layers(b);
                    Color[] cols = { Palette.Gravel, Palette.Loam, Palette.SoilLight, Palette.Moss };
                    for (int k = 0; k < 4; k++)
                        sb.Quad(new Vector3(a.x, la[k], a.y), new Vector3(a.x, la[k + 1], a.y), new Vector3(b.x, lb[k + 1], b.y), new Vector3(b.x, lb[k], b.y), cols[k]);
                    if (sb.Range(0f, 1f) < 0.8f)
                    {
                        var at = Vector2.Lerp(a, b, sb.Range(0f, 1f));
                        float r = sb.Range(0.035f, 0.075f);
                        var pos = new Vector3(at.x, sb.Range(0.05f, Layers(at)[1] - 0.02f), at.y) + outward * (-r * 0.45f);
                        var col = sb.Range(0, 3) switch { 0 => Palette.Rock, 1 => Palette.RockLight, _ => Palette.RockDark };
                        sb.Ico(pos, r, new Vector3(1f, 0.75f, 1f), col, 0.2f);
                    }
                }
            }
            Append(mb, sb);
        }

        float[] Layers(Vector2 p)
        {
            float top = Height(p.x, p.y);
            float n1 = GrowthTerrain.Noise(p.x * 2.1f + p.y * 1.3f, 3.7f), n2 = GrowthTerrain.Noise(p.x * 1.7f - p.y * 2.2f, 8.1f), n3 = GrowthTerrain.Noise(p.x * 3.1f + p.y, 1.9f);
            return new[] { 0f, 0.18f + 0.06f * n1, 0.46f + 0.08f * n2, top - 0.08f - 0.04f * n3, top };
        }

        void BoxAndRoom(MeshBuilder mb)
        {
            var bb = new MeshBuilder(seed + 4);
            const float lip = 0.2f, plinthH = 0.3f;
            bb.Box(new Vector3(0, -plinthH / 2f, 0), new Vector3(2 * HX + 2 * lip, plinthH, 2 * HZ + 2 * lip), Palette.Plinth);
            bb.Box(new Vector3(0, 0.012f, 0), new Vector3(2 * HX + 2 * lip - 0.04f, 0.024f, 2 * HZ + 2 * lip - 0.04f), Palette.PlinthTop);
            float gx = HX + 0.075f, gz = HZ + 0.075f, t = 0.07f;
            foreach (var (x, z) in new[] { (-gx, -gz), (gx, -gz), (gx, gz), (-gx, gz) })
                bb.Box(new Vector3(x, GlassTop / 2f, z), new Vector3(t, GlassTop, t), Palette.Frame);
            foreach (float y in new[] { 0.05f, GlassTop })
            {
                bb.Box(new Vector3(0, y, -gz), new Vector3(2 * gx + t, t, t), Palette.Frame);
                bb.Box(new Vector3(0, y, gz), new Vector3(2 * gx + t, t, t), Palette.Frame);
                bb.Box(new Vector3(-gx, y, 0), new Vector3(t, t, 2 * gz), Palette.Frame);
                bb.Box(new Vector3(gx, y, 0), new Vector3(t, t, 2 * gz), Palette.Frame);
            }
            float sy = -plinthH, sx0 = -9f, sx1 = 9f, sz0 = -4.2f, sz1 = 3.6f, cell = 0.3f;
            bb.jitter = 0f;
            for (float x = sx0; x < sx1 - 1e-3f; x += cell)
                for (float z = sz0; z < sz1 - 1e-3f; z += cell)
                {
                    int plank = Mathf.FloorToInt((z - sz0) / 0.9f);
                    var c = Color.Lerp(Palette.Shelf, Palette.ShelfEdge, GrowthTerrain.Hash(plank, 3) * 0.25f);
                    bb.Quad(new Vector3(x, sy, z), new Vector3(x, sy, z + cell), new Vector3(x + cell, sy, z + cell), new Vector3(x + cell, sy, z), c);
                }
            bb.Box(new Vector3(0, sy - 0.2f, sz0), new Vector3(sx1 - sx0, 0.4f, 0.02f), Palette.ShelfEdge);
            float wz = sz1, wy0 = sy, wy1 = 12f;
            for (int i = 0; i < 8; i++)
            {
                float y0 = Mathf.Lerp(wy0, wy1, i / 8f), y1 = Mathf.Lerp(wy0, wy1, (i + 1) / 8f);
                var c0 = Color.Lerp(Palette.WallBottom, Palette.WallTop, i / 8f); var c1 = Color.Lerp(Palette.WallBottom, Palette.WallTop, (i + 1) / 8f);
                Vector3 a = new(sx0 * 2f, y0, wz), b = new(sx0 * 2f, y1, wz), c = new(sx1 * 2f, y1, wz), d = new(sx1 * 2f, y0, wz);
                bb.TriC(a, b, c, c0, c1, c1); bb.TriC(a, c, d, c0, c1, c0);
            }
            Append(mb, bb);
        }

        Mesh WaterMesh()
        {
            var mb = new MeshBuilder(seed + 3) { jitter = 0f };
            const int segs = 24;
            var c = new Vector3(spec.pondX, WaterY, spec.pondZ);
            for (int i = 0; i < segs; i++)
            {
                float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0) * 0.9f) * 1.0f, p1 = c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1) * 0.9f) * 1.0f;
                p0.x = Mathf.Clamp(p0.x, -HX, HX); p0.z = Mathf.Clamp(p0.z, -HZ, HZ);
                p1.x = Mathf.Clamp(p1.x, -HX, HX); p1.z = Mathf.Clamp(p1.z, -HZ, HZ);
                mb.TriC(c, p1, p0, Palette.WaterDeep, Palette.Water, Palette.Water);
            }
            // Canals: a water strip over every canal cell, a little wider than the cell.
            for (int k = 0; k < SiteMap.N; k++)
            {
                if ((snap.flags[k] & Snapshot.FCanal) == 0) continue;
                float x = SiteMap.CXc(k), z = SiteMap.CZc(k), h = 0.065f;
                mb.Quad(new Vector3(x - h, WaterY, z - h), new Vector3(x - h, WaterY, z + h), new Vector3(x + h, WaterY, z + h), new Vector3(x + h, WaterY, z - h), Palette.Water);
            }
            return mb.ToMesh("Water");
        }

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

        // ---------------- ambient occlusion (StyleBox.BakeAO) ----------------
        void BakeAO(Mesh mesh, MeshBuilder mb)
        {
            const int aoRays = 10; const float aoDistance = 0.45f;
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

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!labels || labelList.Count == 0) return;
            foreach (var (p, l) in labelList) UnityEditor.Handles.Label(transform.TransformPoint(p), l);
        }
#endif
    }
}
