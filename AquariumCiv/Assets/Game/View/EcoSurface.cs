using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using UnityEngine;
using Site = AquariumCiv.Sim.Settle.SiteMap;

namespace AquariumCiv.View
{
    /// <summary>
    /// What the mister leaves on the ground (#266, keepers-hands §2.2): the <b>wet area</b> (soil darkened under a soft edge, a
    /// slight sheen, drying back), <b>shoots</b> on bare ground, the <b>green that shows at once</b> (a soft green patch on the
    /// cells that just turned to grass, until the box's chunk has caught up with the mesh) and the <b>puddles</b> (small, dark,
    /// glossy, a soft rim, a sky reflection, the odd ripple). All of it is a few small overlay meshes on the ground, rebuilt
    /// only when something moved: the chunk meshes are never touched, so a stroke costs a fraction of a millisecond here and
    /// the chunks can catch up a frame at a time (#277). The sim steps the wet area, shoots and puddles per game month; this
    /// eases what is drawn between the steps (fast up, slow down).
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class EcoSurface : MonoBehaviour
    {
        public static EcoSurface Instance { get; private set; }

        const int N = Site.N, NX = Site.NX, NZ = Site.NZ;
        const float Lift = 0.006f;
        /// <summary>The weakest puddle drawn, and how far apart two are (box units).</summary>
        public const float MinPuddle = 0.12f, Spacing = 0.26f;

        BoxView box;
        float[] wetT = new float[N], wetS = new float[N], wetB = new float[N];
        float[] pudT = new float[N], pudS = new float[N], pudB = new float[N];
        float[] spT = new float[N], spS = new float[N], spB = new float[N];
        float[] frT = new float[N], frS = new float[N], frB = new float[N];
        float[] ngS = new float[N], ngB = new float[N];  // #281: new grass since the last visit (0..1 freshness, from the sim)
        float[] cornerY = new float[(NX + 1) * (NZ + 1)], cellY = new float[N];
        int landVersion = -1;
        bool snap = true;
        object simSeen;
        float buildT;
        Transform rootSeen;

        Mesh wetMesh, pudMesh, spMesh, frMesh, ngMesh;
        MeshFilter wetF, sheenF, pudF, spF, frF, ngF;
        MeshRenderer wetR, sheenR, pudR, spR, frR, ngR;
        Material wetMat, sheenMat, pudMat, tintMat;
        bool pudTuned;

        // reusable builders
        readonly List<Vector3> pos = new(4096);
        readonly List<Color32> col = new(4096);
        readonly List<Vector2> uv0 = new(4096), uv1 = new(4096);
        readonly List<int> tri = new(8192);
        readonly List<int> pudCells = new(128);

        /// <summary>Counts for tests and the debug panel.</summary>
        public int WetCells { get; private set; }
        public int PuddleCount { get; private set; }
        public int ShootCells { get; private set; }
        public int FreshCells { get; private set; }
        /// <summary>#281: cells drawn in the fresher spring green, and the real seconds (times <see cref="FadeSpeed"/>) since the player came back.</summary>
        public int NewGrassCells { get; private set; }
        public float LandSeconds { get; private set; }
        /// <summary>Tests: how much faster than real time the new-grass fade runs.</summary>
        public static float FadeSpeed = 1f;
        public float NewGrassShown(int c) => c >= 0 && c < N ? ngS[c] : 0f;
        float ngPoll;
        bool landMarkChecked;
        /// <summary>The player has just come back (or the debug cheat pretended so): the fade starts over from now.</summary>
        public void RestartFade() { LandSeconds = 0f; ngPoll = 1f; }
        public int Builds { get; private set; }
        /// <summary>How long the last overlay rebuild took (ms), for the frame-budget test.</summary>
        public double LastBuildMs { get; private set; }
        /// <summary>The wet look the ground has now at a cell (eased), 0..1 (tests).</summary>
        public float WetShown(int c) => c >= 0 && c < N ? wetS[c] : 0f;
        public float PuddleShown(int c) => c >= 0 && c < N ? pudS[c] : 0f;
        public float FreshShown(int c) => c >= 0 && c < N ? frS[c] : 0f;
        public float ShootShown(int c) => c >= 0 && c < N ? spS[c] : 0f;
        /// <summary>The drawn puddles' cells (strength at least the drawing threshold), strongest first (tests, the people's visits).</summary>
        public IReadOnlyList<int> PuddleCellsShown => pudCells;

        public static EcoSurface Ensure(BoxView b)
        {
            if (b == null) return null;
            var s = b.GetComponent<EcoSurface>();
            return s != null ? s : b.gameObject.AddComponent<EcoSurface>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            GameClock.LandReturned += RestartFade;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            GameClock.LandReturned -= RestartFade;
            if (Instance == this) Instance = null;
            Clear();
        }

        void Clear()
        {
            foreach (var go in new[] { wetF, sheenF, pudF, spF, frF, ngF }) if (go != null) Destroy(go.gameObject);
            wetF = sheenF = pudF = spF = frF = ngF = null;
            foreach (var m in new[] { wetMesh, pudMesh, spMesh, frMesh, ngMesh }) if (m != null) Destroy(m);
            wetMesh = pudMesh = spMesh = frMesh = ngMesh = null;
        }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        /// <summary>One of the ground-look materials: the asset under Resources/EcoGround (so the shader is in the build), else a
        /// runtime copy of the shader (edit mode, tests before the assets exist). <paramref name="src"/>/<paramref name="dst"/> are
        /// UnityEngine.Rendering.BlendMode values.</summary>
        public static Material MakeMaterial(string name, float mode, int src, int dst)
        {
            var m = Resources.Load<Material>("EcoGround/" + name);
            if (m == null)
            {
                var sh = Shader.Find("AquariumCiv/View/EcoGround");
                if (sh == null) return null;
                m = new Material(sh) { name = name };
                m.renderQueue = mode < 0.5f ? 2940 : mode < 1.5f ? 2944 : mode < 2.5f ? 2946 : mode < 3.5f ? 2942 : 3005;
            }
            else m = new Material(m);
            m.SetFloat("_Mode", mode);
            m.SetFloat("_SrcBlend", src);
            m.SetFloat("_DstBlend", dst);
            return m;
        }

        bool EnsureMaterials()
        {
            const int Zero = 0, One = 1, DstColor = 2, SrcAlpha = 5, OneMinusSrcAlpha = 10;
            wetMat ??= MakeMaterial("EcoWet", 0, DstColor, Zero);
            sheenMat ??= MakeMaterial("EcoSheen", 1, One, One);
            pudMat ??= MakeMaterial("EcoPuddle", 2, SrcAlpha, OneMinusSrcAlpha);
            tintMat ??= MakeMaterial("EcoTint", 3, SrcAlpha, OneMinusSrcAlpha);
            if (pudMat != null && !pudTuned)
            {
                // water, not slate (tester run 1): a dark wet-blue/brown bed under the sky's reflection
                pudTuned = true;
                pudMat.SetColor("_PuddleDeep", new Color(0.10f, 0.09f, 0.08f)); // a brown-dark bed
                pudMat.SetFloat("_Gloss", 0.9f);
                if (wetMat != null) { wetMat.SetFloat("_WetBoost", 1.9f); wetMat.SetColor("_WetColor", new Color(0.80f, 0.72f, 0.62f)); } // damp sand, not soot
                pudMat.SetColor("_WetColor", new Color(0.46f, 0.37f, 0.31f)); // the puddle's mud rim
                if (sheenMat != null) sheenMat.SetFloat("_WetBoost", 1.9f);
            }
            return wetMat != null && sheenMat != null && pudMat != null && tintMat != null;
        }

        MeshFilter Part(string name, ref MeshRenderer r, Material mat, Mesh mesh)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(box.Root != null ? box.Root : transform, false);
            var f = go.AddComponent<MeshFilter>();
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            f.sharedMesh = mesh;
            return f;
        }

        static Mesh NewMesh(string n) { var m = new Mesh { name = n }; m.MarkDynamic(); return m; }

        /// <summary>#296: an overlay with nothing to show still draws one invisible, zero-area triangle in the middle of the land,
        /// so each ground-look material's first real use (the first spray, the first puddle) costs no first-draw work.</summary>
        void Warm(Mesh m)
        {
            float y = box != null ? box.land.Height(0f, 0f) + Lift : 0f;
            var p = new Vector3(0f, y, 0f);
            m.SetVertices(new List<Vector3> { p, p, p });
            m.SetColors(new List<Color32> { default, default, default });
            m.SetUVs(0, new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero });
            m.SetUVs(1, new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero });
            m.SetTriangles(new[] { 0, 1, 2 }, 0, false);
            m.bounds = new Bounds(p, Vector3.one * 0.2f);
        }

        void Update()
        {
            var sim = Sim;
            if (box == null || box.Root == null) return;
            if (sim == null || !sim.EcologyOn) { SetVisible(false); return; }
            if (!EnsureMaterials()) return;
            if (wetF != null && rootSeen != box.Root) { Clear(); snap = true; } // the box was rebuilt: its children went with the old root
            if (wetF == null)
            {
                rootSeen = box.Root;
                wetMesh = NewMesh("EcoWet"); pudMesh = NewMesh("EcoPuddles"); spMesh = NewMesh("EcoShoots"); frMesh = NewMesh("EcoFresh"); ngMesh = NewMesh("EcoNewGrass");
                Warm(wetMesh); Warm(pudMesh); Warm(frMesh); Warm(ngMesh);
                ngF = Part("EcoNewGrass", ref ngR, tintMat, ngMesh);
                frF = Part("EcoFresh", ref frR, tintMat, frMesh);
                wetF = Part("EcoWet", ref wetR, wetMat, wetMesh);
                sheenF = Part("EcoSheen", ref sheenR, sheenMat, wetMesh);
                pudF = Part("EcoPuddles", ref pudR, pudMat, pudMesh);
                spF = Part("EcoShoots", ref spR, box.SolidMaterial, spMesh);
            }
            SetVisible(true);
            if (!ReferenceEquals(simSeen, sim)) { simSeen = sim; snap = true; landMarkChecked = false; LandSeconds = 0f; ngPoll = 1f; }
            if (landVersion != box.LandVersion) { landVersion = box.LandVersion; System.Array.Fill(cornerY, float.NaN); System.Array.Fill(cellY, float.NaN); wetB[0] = -1; pudB[0] = -1; spB[0] = -1; frB[0] = -1; ngB[0] = -1; }
            Read(sim);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Ease(wetS, wetT, snap ? 1e9f : 4f, snap ? 1e9f : 0.22f, dt);
            Ease(pudS, pudT, snap ? 1e9f : 1.2f, snap ? 1e9f : 0.07f, dt);
            Ease(spS, spT, snap ? 1e9f : 1.5f, snap ? 1e9f : 0.4f, dt);
            Ease(frS, frT, snap ? 1e9f : 3f, snap ? 1e9f : 1.6f, dt);
            snap = false;
            UpdateNewGrass(sim, dt);
            buildT += dt;
            if (buildT < 0.06f) return; // at most ~16 rebuilds a second, and none unless something moved
            bool built = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (Changed(wetS, wetB, 0.012f)) { BuildWet(); built = true; }
            if (Changed(pudS, pudB, 0.012f)) { BuildPuddles(); built = true; }
            if (Changed(spS, spB, 0.02f)) { BuildShoots(); built = true; }
            if (Changed(frS, frB, 0.02f)) { BuildFresh(); built = true; }
            if (Changed(ngS, ngB, 0.02f)) { BuildNewGrass(); built = true; }
            if (built) { buildT = 0f; Builds++; LastBuildMs = sw.Elapsed.TotalMilliseconds; }
        }

        void SetVisible(bool on)
        {
            foreach (var r in new[] { wetR, sheenR, pudR, spR, frR, ngR }) if (r != null && r.enabled != on) r.enabled = on;
        }

        void Read(SimCore sim)
        {
            var wet = sim.EcoWet;
            var st = sim.State.eco;
            var baked = box.BakedCover;
            for (int c = 0; c < N; c++)
            {
                wetT[c] = wet != null && c < wet.Count ? wet[c] : 0f;
                pudT[c] = sim.EcoPuddleAt(c);
                var cv = st.cover != null ? st.cover[c] : (byte)0;
                bool green = cv != (byte)AquariumCiv.Sim.Eco.EcoCover.Bare && cv != (byte)AquariumCiv.Sim.Eco.EcoCover.Water;
                spT[c] = green ? 0f : sim.EcoSproutAt(c);
                // green now, but the chunk has not drawn it yet: a soft green patch stands in
                frT[c] = green && box.BakedValid && baked[c] != cv && baked[c] == (byte)AquariumCiv.Sim.Eco.EcoCover.Bare ? 1f : 0f;
            }
        }

        /// <summary>#281: grass new since the player last looked is a light spring green that fades to the normal colouring over
        /// <see cref="AquariumCiv.Sim.Eco.EcoSeen.FadeSeconds"/>. Polled five times a second; only the small overlay is rebuilt (the chunks never are).</summary>
        void UpdateNewGrass(SimCore sim, float dt)
        {
            if (!landMarkChecked)
            {
                landMarkChecked = true;
                if (!sim.HasLandSeenMask) sim.MarkLandSeen(); // a fresh box, or an old save: nothing is new yet
            }
            LandSeconds += dt * FadeSpeed;
            ngPoll += dt;
            if (ngPoll < 0.2f) return;
            ngPoll = 0f;
            if (sim.CommitLandSeenIfFaded(LandSeconds)) System.Array.Clear(ngS, 0, N);
            else sim.FillLandFreshness(LandSeconds, ngS);
        }

        static void Ease(float[] shown, float[] target, float up, float down, float dt)
        {
            for (int c = 0; c < shown.Length; c++)
            {
                float t = target[c], s = shown[c];
                if (t == s) continue;
                shown[c] = t > s ? Mathf.MoveTowards(s, t, up * dt) : Mathf.MoveTowards(s, t, down * dt);
            }
        }

        static bool Changed(float[] now, float[] built, float eps)
        {
            for (int c = 0; c < now.Length; c++) if (Mathf.Abs(now[c] - built[c]) > eps) return true;
            return false;
        }

        // ---------- the ground under the overlay ----------
        float CornerY(int i, int j)
        {
            int k = j * (NX + 1) + i;
            float y = cornerY[k];
            if (float.IsNaN(y)) cornerY[k] = y = box.land.Height(Site.X0 + i * Site.Cell, Site.Z0 + j * Site.Cell);
            return y;
        }

        float CenterY(int c)
        {
            float y = cellY[c];
            if (float.IsNaN(y)) cellY[c] = y = box.land.Height(Site.CXc(c), Site.CZc(c));
            return y;
        }

        static float Hash(int a, int b) => Land.Hash(a, b);

        float At(float[] f, int i, int j) => i < 0 || j < 0 || i >= NX || j >= NZ ? 0f : f[Site.Index(i, j)];

        /// <summary>Soft patches from a per-cell field: every cell with something in it or next to it, five points a cell (the
        /// middle and the four corners, each corner the mean of the four cells round it), so the edge fades out over a cell.</summary>
        int SoftPatch(float[] f, Color32 colour, float noise, float scale, int cellCap)
        {
            pos.Clear(); col.Clear(); uv0.Clear(); uv1.Clear(); tri.Clear();
            int used = 0;
            for (int j = 0; j < NZ; j++)
                for (int i = 0; i < NX; i++)
                {
                    int c = Site.Index(i, j);
                    bool any = f[c] > 0.001f;
                    if (!any)
                    {
                        for (int dj = -1; dj <= 1 && !any; dj++)
                            for (int di = -1; di <= 1; di++) if (At(f, i + di, j + dj) > 0.001f) { any = true; break; }
                    }
                    if (!any) continue;
                    if (++used > cellCap) break;
                    int b = pos.Count;
                    float cx = Site.CXc(c), cz = Site.CZc(c);
                    float x0 = Site.X0 + i * Site.Cell, z0 = Site.Z0 + j * Site.Cell, x1 = x0 + Site.Cell, z1 = z0 + Site.Cell;
                    Add(cx, CenterY(c) + Lift, cz, f[c] * scale, colour);
                    for (int q = 0; q < 4; q++)
                    {
                        int ci = i + (q & 1), cj = j + (q >> 1);
                        float a = (At(f, ci - 1, cj - 1) + At(f, ci, cj - 1) + At(f, ci - 1, cj) + At(f, ci, cj)) * 0.25f;
                        a *= 1f - noise + noise * 2f * Hash(ci * 31 + 7, cj * 17 + 3); // a ragged edge, not the grid's
                        Add(ci * Site.Cell + Site.X0, CornerY(ci, cj) + Lift, cj * Site.Cell + Site.Z0, Mathf.Clamp01(a * scale), colour);
                    }
                    // corners: 1 = (i,j), 2 = (i+1,j), 3 = (i,j+1), 4 = (i+1,j+1); fan round the middle
                    tri.Add(b); tri.Add(b + 3); tri.Add(b + 4);
                    tri.Add(b); tri.Add(b + 4); tri.Add(b + 2);
                    tri.Add(b); tri.Add(b + 2); tri.Add(b + 1);
                    tri.Add(b); tri.Add(b + 1); tri.Add(b + 3);
                }
            return used;
        }

        void Add(float x, float y, float z, float a, Color32 c)
        {
            pos.Add(new Vector3(x, y, z));
            c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
            col.Add(c);
            uv0.Add(Vector2.zero); uv1.Add(Vector2.zero);
        }

        void Upload(Mesh m)
        {
            m.Clear();
            if (pos.Count == 0) { Warm(m); return; }
            m.SetVertices(pos); m.SetColors(col); m.SetUVs(0, uv0); m.SetUVs(1, uv1); m.SetTriangles(tri, 0, true);
        }

        void BuildWet()
        {
            System.Array.Copy(wetS, wetB, N);
            WetCells = SoftPatch(wetS, new Color32(255, 255, 255, 255), 0.35f, 1f, N);
            Upload(wetMesh);
        }

        void BuildFresh()
        {
            System.Array.Copy(frS, frB, N);
            var g = box.land.Colors.grassC;
            FreshCells = SoftPatch(frS, (Color32)Color.Lerp(g, box.land.Colors.grassA, 0.25f), 0.2f, 0.92f, N);
            Upload(frMesh);
        }

        void BuildNewGrass()
        {
            System.Array.Copy(ngS, ngB, N);
            // a light, fresh spring green over the normal grass: the more it is new, the more of it shows
            NewGrassCells = SoftPatch(ngS, new Color32(175, 245, 85, 255), 0.15f, 0.9f, N);
            Upload(ngMesh);
        }

        void BuildShoots()
        {
            System.Array.Copy(spS, spB, N);
            ShootCells = 0;
            var mb = new MeshBuilder(box.seed + 11) { jitter = 0f };
            var co = box.land.Colors;
            for (int c = 0; c < N; c++)
            {
                float s = spS[c];
                if (s < 0.06f) continue;
                ShootCells++;
                int n = s > 0.5f ? 3 : 2;
                for (int t = 0; t < n; t++)
                {
                    float x = Site.CXc(c) + (Land.Hash3(box.seed * 31 + 5, c, 40 + t * 2) - 0.5f) * 0.08f;
                    float z = Site.CZc(c) + (Land.Hash3(box.seed * 31 + 5, c, 41 + t * 2) - 0.5f) * 0.08f;
                    float h = 0.014f + 0.05f * s;
                    Pieces.GrassTuft(mb, box.land.OnGround(x, z), h, t == 0 ? co.sprout : co.tuftA);
                }
            }
            var old = spMesh;
            spMesh = ShootCells > 0 ? mb.ToMesh("EcoShoots") : null;
            if (spF != null) spF.sharedMesh = spMesh;
            if (old != null) Destroy(old);
            mb.Free();
        }

        void BuildPuddles()
        {
            System.Array.Copy(pudS, pudB, N);
            pos.Clear(); col.Clear(); uv0.Clear(); uv1.Clear(); tri.Clear();
            pudCells.Clear();
            const int Seg = 10;
            // A few small puddles, not a lake: the soaked cells are thinned (strongest first, none within two and a half cells of
            // one already kept) and each kept puddle takes the water of the cells it absorbed as its size.
            var cand = new List<int>();
            for (int c = 0; c < N; c++) if (pudS[c] >= MinPuddle) cand.Add(c);
            cand.Sort((a, b) => pudS[b].CompareTo(pudS[a]));
            var mass = new Dictionary<int, float>();
            foreach (int c in cand)
            {
                int keep = -1;
                foreach (int k in pudCells)
                {
                    float dx = Site.CXc(k) - Site.CXc(c), dz = Site.CZc(k) - Site.CZc(c);
                    if (dx * dx + dz * dz < Spacing * Spacing) { keep = k; break; }
                }
                if (keep >= 0) mass[keep] += pudS[c] * 0.35f; else { pudCells.Add(c); mass[c] = pudS[c]; }
            }
            PuddleCount = pudCells.Count;
            foreach (int c in pudCells)
            {
                float s = Mathf.Min(1.6f, mass[c]);
                float R = 0.03f + 0.044f * Mathf.Sqrt(s); // small: about a cell across, up to a little over
                float cx = Site.CXc(c) + (Hash(c, 3) - 0.5f) * 0.03f, cz = Site.CZc(c) + (Hash(c, 4) - 0.5f) * 0.03f;
                float y = CenterY(c);
                for (int k = 0; k < Seg; k++)
                {
                    float a = k / (float)Seg * Mathf.PI * 2f;
                    y = Mathf.Max(y, box.land.Height(cx + Mathf.Cos(a) * R, cz + Mathf.Sin(a) * R));
                }
                y += 0.004f;
                float phase = Hash(c, 9), strength = Hash(c, 10) < 0.55f ? 1f : 0.35f;
                int b = pos.Count;
                var white = new Color32(128, 128, 255, 255); // rg: the middle
                pos.Add(new Vector3(cx, y, cz)); col.Add(white); uv0.Add(new Vector2(0f, 0f)); uv1.Add(new Vector2(phase, strength));
                for (int ring = 0; ring < 2; ring++)
                    for (int k = 0; k < Seg; k++)
                    {
                        float a = k / (float)Seg * Mathf.PI * 2f;
                        float rr = R * (0.8f + 0.4f * Hash(c * 13 + k, 5)) * (ring == 0 ? 0.72f : 1f);
                        pos.Add(new Vector3(cx + Mathf.Cos(a) * rr, y, cz + Mathf.Sin(a) * rr));
                        var cc = white; cc.a = (byte)(ring == 0 ? 255 : 0);
                        float u = rr / (R * 1.2f); // where in the puddle, for the shader's far-side sky and glint
                        cc.r = (byte)Mathf.Clamp(Mathf.RoundToInt(128f + Mathf.Cos(a) * u * 127f), 0, 255);
                        cc.g = (byte)Mathf.Clamp(Mathf.RoundToInt(128f + Mathf.Sin(a) * u * 127f), 0, 255);
                        col.Add(cc);
                        uv0.Add(new Vector2(ring == 0 ? 0.72f : 1f, 0f)); uv1.Add(new Vector2(phase, strength));
                    }
                for (int k = 0; k < Seg; k++)
                {
                    int k1 = (k + 1) % Seg;
                    int i0 = b + 1 + k, i1 = b + 1 + k1, o0 = b + 1 + Seg + k, o1 = b + 1 + Seg + k1;
                    tri.Add(b); tri.Add(i1); tri.Add(i0);
                    tri.Add(i0); tri.Add(i1); tri.Add(o1);
                    tri.Add(i0); tri.Add(o1); tri.Add(o0);
                }
            }
            Upload(pudMesh);
        }

        /// <summary>Puddle opacity scale is the vertex alpha: the middle full, the rim gone; a thin puddle is mostly rim.</summary>
        public Vector3 PuddlePoint(int cell) => new(Site.CXc(cell), CenterY(cell), Site.CZc(cell));
    }
}
