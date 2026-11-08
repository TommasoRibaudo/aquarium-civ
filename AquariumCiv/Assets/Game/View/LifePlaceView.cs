using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.View
{
    /// <summary>
    /// #423 the life tool on the land (placed-life spec §6, B12.0): while a species is in hand (<see cref="GameHud.KitTool.Life"/>),
    /// the ground it may live on glows softly (<see cref="SimCore.LifeGround"/>) and the rest stays as it is. The first tap on the
    /// glow shows a faint ghost of the founding group there (flat pale outlines, breathing); a tap on the ghost places it
    /// (<see cref="SimCore.PlaceLife"/>), a tap elsewhere on the glow moves the ghost, a tap on dark ground gives a small "no"
    /// wobble and nothing else, a tap off the land puts the tool back. One finger is the tool, two the camera
    /// (<see cref="EcoMistView.ClaimsPress"/> already holds one finger for any kit tool). The arrival itself is #424's.
    /// </summary>
    [DefaultExecutionOrder(-14)]
    public class LifePlaceView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static LifePlaceView Instance { get; private set; }

        [Tooltip("A press that wanders further than this (share of screen height) is a drag, not a tap.")] public float slop = 0.02f;
        [Tooltip("A tap this near the ghost's middle (box units) places it.")] public float ghostReach = 0.32f;
        [Tooltip("The glow's colour (alpha = its strongest).")] public Color glow = new(1f, 0.95f, 0.55f, 0.55f);
        [Tooltip("The ghost's outline colour: pale, no see-through on the lines.")] public Color ghostColor = new(0.97f, 0.95f, 0.86f, 0.9f);
        [Tooltip("The silhouettes' fill (pale) and their thin darker edge.")] public Color ghostFill = new(0.97f, 0.95f, 0.88f, 0.8f), ghostEdge = new(0.30f, 0.26f, 0.20f, 0.55f);
        [Tooltip("User 2026-11-08: the rest of the land dims a little while a species is in hand (0 = off).")][Range(0f, 0.6f)] public float dim = 0.22f;

        BoxView box;
        GameHud hud;
        Material fx;
        GameObject glowGo, ghostGo, noGo, restoreGo, dimGo, fillGo, edgeGo;
        Material glowMat, dimMat, restoreMat, fillMat, edgeMat;
        Mesh dimMesh;
        Mesh glowMesh, ghostMesh, noMesh;
        MaterialPropertyBlock mpb;
        readonly List<int> cells = new();
        LifeSpecies glowFor;
        int glowStamp = -1;
        bool down, multi;
        Vector2 downAt, lastAt;
        Vector3 ghostAt;
        float ghostT, wobbleT = -9f, noT = -9f;

        /// <summary>Cells glowing now (0 when nothing is in hand).</summary>
        public int GlowCells { get; private set; }
        /// <summary>The land is dimmed now (a species in hand), and how strongly (user 2026-11-08).</summary>
        public bool Dimmed => dimGo != null && dimGo.activeSelf;
        /// <summary>The ghost is shown, at <see cref="GhostCell"/>.</summary>
        public bool GhostShown => ghostGo != null && ghostGo.activeSelf;
        public int GhostCell { get; private set; } = -1;
        /// <summary>Taps on dark ground (the soft "no"), placings made by tap, and the last answer (tests, debug panel).</summary>
        public int Refusals { get; private set; }
        public int Placings { get; private set; }
        public string Last { get; private set; } = "";

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<LifePlaceView>() == null) b.gameObject.AddComponent<LifePlaceView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            foreach (var go in new[] { glowGo, ghostGo, noGo, restoreGo, dimGo, fillGo, edgeGo }) if (go != null) Destroy(go);
            foreach (var m in new[] { glowMesh, ghostMesh, noMesh, dimMesh }) if (m != null) Destroy(m);
            foreach (var m in new[] { glowMat, dimMat, restoreMat, fillMat, edgeMat }) if (m != null) Destroy(m);
            glowGo = ghostGo = noGo = restoreGo = dimGo = fillGo = edgeGo = null; glowMesh = ghostMesh = noMesh = dimMesh = null;
            glowMat = dimMat = restoreMat = fillMat = edgeMat = null;
        }

        void Update()
        {
            var sim = Sim;
            hud ??= GameHud.Instance;
            bool holding = sim != null && sim.EcologyOn && box != null && hud != null && hud.Hand == GameHud.KitTool.Life;
            if (!holding)
            {
                if (glowGo != null && glowGo.activeSelf) glowGo.SetActive(false);
                if (restoreGo != null && restoreGo.activeSelf) restoreGo.SetActive(false);
                if (dimGo != null && dimGo.activeSelf) dimGo.SetActive(false); // the moment the tool is put down
                HideGhost();
                GlowCells = 0; glowStamp = -1; down = false;
                DrawNo();
                return;
            }
            var s = hud.HeldLife;
            // the glow follows the land: rebuilt when the species changes or the land moved on (a season, a placing)
            int stamp = (int)(sim.State.month * 8 + (int)s);
            if (s != glowFor || stamp != glowStamp) { glowFor = s; glowStamp = stamp; BuildGlow(sim, s); if (GhostShown && !sim.LifeGroundOk(s, GhostCell)) HideGhost(); }
            if (glowGo != null)
            {
                float pulse = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 2.4f); // a soft breath, never a blink
                var c = glow; c.a *= pulse;
                mpb.SetColor(ColorId, c);
                glowGo.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
            }
            Dim();
            DrawGhost();
            DrawNo();
            Taps(sim, s);
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");

        // ---------------------------------------------------------------- input
        void Taps(SimCore sim, LifeSpecies s)
        {
            int n = ReadPointer(out var pos);
            if (n > 0)
            {
                if (!down) { down = !UiInput.Over(pos); multi = false; downAt = pos; }
                if (n > 1) multi = true;
                lastAt = pos;
                return;
            }
            if (!down) return;
            down = false;
            if (multi) return; // two fingers: the camera's
            if ((lastAt - downAt).magnitude > slop * 2f * Mathf.Max(1, Screen.height)) return; // a drag isn't a tap
            TapAt(sim, s, lastAt);
        }

        /// <summary>One tap of the life tool at a screen point (the touch path calls it; tests may too).</summary>
        public void TapAt(SimCore sim, LifeSpecies s, Vector2 screen)
        {
            var mist = EcoMistView.Instance;
            if (mist == null || !mist.GroundPoint(screen, out var at)) { Last = "off the land: put back"; hud?.SetHand(GameHud.KitTool.None); return; }
            int cell = SiteMap.CellAt(at.x, at.z);
            if (GhostShown && new Vector2(at.x - ghostAt.x, at.z - ghostAt.z).magnitude <= ghostReach && sim.CanPlaceLife(s, GhostCell))
            {
                Place(sim, s, GhostCell);
                return;
            }
            if (cell < 0 || !sim.LifeGroundOk(s, cell))
            {
                Refusals++;
                Last = $"no: cell {cell}";
                wobbleT = Time.unscaledTime;
                noAt = box.land.OnGround(at.x, at.z); noT = Time.unscaledTime;
                return;
            }
            bool moved = GhostShown;
            ShowGhost(s, cell);
            Last = moved ? $"ghost moved to {cell}" : $"ghost at {cell}";
            if (!moved) { hud?.WithdrawToast(GameHud.LifeHint); hud?.Toast("Tap the outline again to set them down here.", 3f); }
        }

        void Place(SimCore sim, LifeSpecies s, int cell)
        {
            if (!sim.PlaceLife(s, cell)) { Last = "refused"; return; }
            Placings++;
            Last = $"placed {s} at {cell}";
            HideGhost();
            box.Refresh();
            GameClock.Instance?.Save();
            hud?.SetHand(GameHud.KitTool.None); // placed: the tool is put down, the dot goes with the jar
        }

        // ---------------------------------------------------------------- the glow
        Material Fx => fx != null ? fx : fx = ShapingView.LoadFx();

        GameObject Child(string name, out Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(box.transform, false);
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Fx;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        float GroundY(float x, float z, LifeSpecies s)
        {
            float y = box.land.Height(x, z);
            if (s == LifeSpecies.Ducks || s == LifeSpecies.Fish)
            {
                float w = box.layout != null && box.layout.eco != null ? box.layout.eco.waterY : Land.WaterY;
                y = Mathf.Max(y, w);
            }
            return y + 0.012f;
        }

        /// <summary>The valid cells as one soft sheet: each grid corner is lit by the share of its four cells that are valid, so the
        /// edge fades over a cell instead of stepping.</summary>
        void BuildGlow(SimCore sim, LifeSpecies s)
        {
            sim.LifeGround(s, cells);
            GlowCells = cells.Count;
            if (glowGo == null)
            {
                glowGo = Child("LifeGlow", out glowMesh); mpb = new MaterialPropertyBlock();
                glowMat = new Material(Fx) { hideFlags = HideFlags.DontSave, renderQueue = 3003 }; // after the dim and the restore
                glowGo.GetComponent<MeshRenderer>().sharedMaterial = glowMat;
                // the restore: the same soft sheet, undoing the dim on the valid ground (dst x (1 + k a))
                restoreGo = new GameObject("LifeGlowRestore");
                restoreGo.transform.SetParent(box.transform, false);
                restoreGo.AddComponent<MeshFilter>().sharedMesh = glowMesh;
                var rr = restoreGo.AddComponent<MeshRenderer>();
                restoreMat = DimMaterial(3002);
                restoreMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor);
                restoreMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                restoreMat.SetFloat("_Premul", 1f);
                rr.sharedMaterial = restoreMat;
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
            }
            glowGo.SetActive(cells.Count > 0);
            restoreGo.SetActive(cells.Count > 0);
            var valid = new bool[SiteMap.N];
            foreach (int c in cells) valid[c] = true;
            int VX = SiteMap.NX + 1, VZ = SiteMap.NZ + 1;
            var lit = new float[VX * VZ];
            for (int j = 0; j < VZ; j++)
                for (int i = 0; i < VX; i++)
                {
                    int k = 0;
                    for (int dj = -1; dj <= 0; dj++)
                        for (int di = -1; di <= 0; di++)
                        {
                            int ci = i + di, cj = j + dj;
                            if (ci >= 0 && cj >= 0 && ci < SiteMap.NX && cj < SiteMap.NZ && valid[SiteMap.Index(ci, cj)]) k++;
                        }
                    lit[j * VX + i] = k / 4f;
                }
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
            var index = new Dictionary<int, int>();
            int V(int i, int j)
            {
                int key = j * VX + i;
                if (index.TryGetValue(key, out int v)) return v;
                float x = SiteMap.X0 + i * SiteMap.Cell, z = SiteMap.Z0 + j * SiteMap.Cell;
                v = verts.Count;
                verts.Add(new Vector3(x, GroundY(Mathf.Clamp(x, -Land.HX, Land.HX), Mathf.Clamp(z, -Land.HZ, Land.HZ), s), z));
                float a = lit[key];
                cols.Add(new Color(1f, 1f, 1f, a * a * (3f - 2f * a))); // smoothstep: a soft rim
                index[key] = v;
                return v;
            }
            for (int j = 0; j < SiteMap.NZ; j++)
                for (int i = 0; i < SiteMap.NX; i++)
                {
                    if (lit[j * VX + i] + lit[j * VX + i + 1] + lit[(j + 1) * VX + i] + lit[(j + 1) * VX + i + 1] <= 0f) continue;
                    int a = V(i, j), b = V(i + 1, j), c = V(i + 1, j + 1), d = V(i, j + 1);
                    tris.Add(a); tris.Add(d); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(b);
                }
            glowMesh.Clear();
            glowMesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            glowMesh.SetVertices(verts); glowMesh.SetColors(cols); glowMesh.SetTriangles(tris, 0);
            glowMesh.RecalculateBounds();
        }

        // ---------------------------------------------------------------- the ghost (B12.0: flat pale outlines, breathing)
        /// <summary>The founding group's outlines round the centre: (dx, dz, radius x, radius z) per member.</summary>
        static (float x, float z, float rx, float rz)[] Group(LifeSpecies s) => s switch
        {
            LifeSpecies.Thicket => new[] { (-0.075f, -0.045f, 0.085f, 0.085f), (0.075f, -0.045f, 0.085f, 0.085f), (0f, 0.085f, 0.085f, 0.085f) },
            LifeSpecies.Rabbits => new[] { (-0.06f, 0f, 0.035f, 0.05f), (0.06f, 0.02f, 0.035f, 0.05f) },
            LifeSpecies.Deer => new[] { (-0.12f, 0f, 0.05f, 0.11f), (0.04f, -0.06f, 0.045f, 0.1f), (0.12f, 0.08f, 0.045f, 0.1f) },
            LifeSpecies.Ducks => new[] { (-0.05f, 0f, 0.035f, 0.055f), (0.06f, 0.03f, 0.035f, 0.055f) },
            LifeSpecies.Fish => new[] { (-0.07f, 0f, 0.025f, 0.06f), (0.05f, -0.05f, 0.025f, 0.06f), (0.04f, 0.07f, 0.025f, 0.06f) },
            _ => new[] { (-0.07f, 0f, 0.05f, 0.09f), (0.07f, 0f, 0.05f, 0.09f) },
        };

        void ShowGhost(LifeSpecies s, int cell)
        {
            var target = new Vector3(SiteMap.CXc(cell), 0f, SiteMap.CZc(cell));
            bool slide = GhostShown;
            if (ghostGo == null) ghostGo = Child("LifeGhost", out ghostMesh);
            ghostGo.SetActive(true);
            slideFrom = slide ? ghostAt : target; slideT = slide ? Time.unscaledTime : -9f;
            ghostAt = target; GhostCell = cell; ghostT = slide ? ghostT : Time.unscaledTime;
            ghostFor = s;
        }

        LifeSpecies ghostFor;
        Vector3 slideFrom, noAt;
        float slideT = -9f;

        void HideGhost()
        {
            if (ghostGo != null && ghostGo.activeSelf) ghostGo.SetActive(false);
            if (fillGo != null && fillGo.activeSelf) fillGo.SetActive(false);
            if (edgeGo != null && edgeGo.activeSelf) edgeGo.SetActive(false);
            GhostCell = -1;
        }

        void DrawGhost()
        {
            if (!GhostShown) return;
            float now = Time.unscaledTime;
            float pop = Mathf.Clamp01((now - ghostT) / 0.15f);
            float breath = 1f + 0.02f * (1f - Mathf.Cos((now - ghostT) * Mathf.PI * 2f / 1.2f)); // 1 -> 1.04 over 1.2 s
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - slideT) / 0.2f));
            var c = Vector3.Lerp(slideFrom, ghostAt, k);
            float wob = now - wobbleT < 0.3f ? Mathf.Sin((now - wobbleT) * 50f) * 0.025f * (1f - (now - wobbleT) / 0.3f) : 0f;
            c.x += wob;
            // user 2026-11-08: faint outlines of the actual group (the real bushes, rabbits, ducks; placeholder deer and wolves), not rings;
            // the thicket keeps its area ring (nobody builds there)
            var parts = new List<(Vector3, float, float)>();
            if (ghostFor == LifeSpecies.Thicket) parts.Add((c, ThicketArea * breath, ThicketArea * breath));
            Rings(ghostMesh, parts, ghostColor, 0.016f, ghostFor);
            Silhouettes(c, breath * pop);
        }

        /// <summary>The ghost's silhouettes: the group's pale shapes over a thin darker edge, side-on to the camera, breathing.</summary>
        void Silhouettes(Vector3 c, float scale)
        {
            if (fillGo == null)
            {
                fillGo = Child("LifeGhostFill", out _); edgeGo = Child("LifeGhostEdge", out _);
                fillMat = DimMaterial(3005); fillMat.SetColor("_Color", ghostFill);
                edgeMat = DimMaterial(3004); edgeMat.SetColor("_Color", ghostEdge);
                fillGo.GetComponent<MeshRenderer>().sharedMaterial = fillMat;
                edgeGo.GetComponent<MeshRenderer>().sharedMaterial = edgeMat;
            }
            fillGo.GetComponent<MeshFilter>().sharedMesh = LifeLooks.Ghost(ghostFor, box, false);
            edgeGo.GetComponent<MeshFilter>().sharedMesh = LifeLooks.Ghost(ghostFor, box, true);
            bool water = ghostFor == LifeSpecies.Ducks || ghostFor == LifeSpecies.Fish;
            float y = GroundY(Mathf.Clamp(c.x, -Land.HX, Land.HX), Mathf.Clamp(c.z, -Land.HZ, Land.HZ), ghostFor) - (water ? 0.008f : 0.012f);
            // side-on to the camera: the group's +x runs across the screen
            float yaw = 0f;
            var cam = Camera.main;
            if (cam != null)
            {
                var f = box.transform.InverseTransformDirection(cam.transform.forward);
                yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            }
            foreach (var go in new[] { fillGo, edgeGo })
            {
                go.SetActive(true);
                go.transform.localPosition = new Vector3(c.x, y, c.z);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
            }
        }

        Material DimMaterial(int queue)
        {
            var res = Resources.Load<Material>("LifeDim");
            var m = res != null ? new Material(res) : new Material(Shader.Find("AquariumCiv/View/LifeDim"));
            m.hideFlags = HideFlags.DontSave;
            m.renderQueue = queue;
            return m;
        }

        /// <summary>User 2026-11-08: the whole view dims a little (a quad over it, after the box) and the restore sheet undoes it on the
        /// valid ground, so the glow stays soft and the rest reads as "not here".</summary>
        void Dim()
        {
            var cam = Camera.main;
            if (cam == null || dim <= 0.001f) { if (dimGo != null) dimGo.SetActive(false); return; }
            if (dimGo == null)
            {
                dimGo = new GameObject("LifeDim") { hideFlags = HideFlags.DontSave };
                dimMesh = new Mesh { name = "LifeDim" };
                dimMesh.SetVertices(new List<Vector3> { new(-0.5f, -0.5f, 0f), new(0.5f, -0.5f, 0f), new(0.5f, 0.5f, 0f), new(-0.5f, 0.5f, 0f) });
                dimMesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
                dimMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                dimMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
                dimGo.AddComponent<MeshFilter>().sharedMesh = dimMesh;
                var r = dimGo.AddComponent<MeshRenderer>();
                dimMat = DimMaterial(3001);
                dimMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                r.sharedMaterial = dimMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            if (dimGo.transform.parent != cam.transform) dimGo.transform.SetParent(cam.transform, false);
            float d = cam.nearClipPlane + 0.02f;
            float h = cam.orthographic ? cam.orthographicSize * 2f : 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            dimGo.transform.localPosition = new Vector3(0f, 0f, d);
            dimGo.transform.localRotation = Quaternion.identity;
            dimGo.transform.localScale = new Vector3(h * cam.aspect * 1.1f, h * 1.1f, 1f);
            dimMat.SetColor("_Color", new Color(0f, 0f, 0f, dim));
            float k = dim / (1f - dim);
            restoreMat.SetColor("_Color", new Color(k, k, k, 1f));
            dimGo.SetActive(GlowCells > 0);
        }

        const float ThicketArea = 0.45f; // EcoTuning.thicketRadius
        const float GroupRing = 0.2f;

        void DrawNo()
        {
            float age = Time.unscaledTime - noT;
            if (age > 0.35f || GhostShown) { if (noGo != null && noGo.activeSelf) noGo.SetActive(false); return; }
            if (noGo == null) noGo = Child("LifeNo", out noMesh);
            noGo.SetActive(true);
            float r = 0.05f + age * 0.12f; // a tiny dust ring that spreads and fades: no sound, no text
            Rings(noMesh, new List<(Vector3, float, float)> { (noAt, r, r) }, new Color(0.85f, 0.8f, 0.72f, 0.7f * (1f - age / 0.35f)), 0.012f, glowFor);
        }

        void Rings(Mesh mesh, List<(Vector3 c, float rx, float rz)> rings, Color col, float width, LifeSpecies s)
        {
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
            Color32 c32 = col;
            foreach (var (c, rx, rz) in rings)
            {
                if (rx <= 0.001f) continue;
                int seg = rx > 0.2f ? 64 : 20, b = verts.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    float xo = c.x + cs * (rx + width * 0.5f), zo = c.z + sn * (rz + width * 0.5f);
                    float xi = c.x + cs * (rx - width * 0.5f), zi = c.z + sn * (rz - width * 0.5f);
                    verts.Add(new Vector3(xo, GroundY(Mathf.Clamp(xo, -Land.HX, Land.HX), Mathf.Clamp(zo, -Land.HZ, Land.HZ), s) + 0.004f, zo));
                    verts.Add(new Vector3(xi, GroundY(Mathf.Clamp(xi, -Land.HX, Land.HX), Mathf.Clamp(zi, -Land.HZ, Land.HZ), s) + 0.004f, zi));
                    cols.Add(c32); cols.Add(c32);
                    if (i == seg) continue;
                    int o = b + i * 2;
                    tris.Add(o); tris.Add(o + 1); tris.Add(o + 3); tris.Add(o); tris.Add(o + 3); tris.Add(o + 2);
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        // ---------------------------------------------------------------- pointer
        static int ReadPointer(out Vector2 a)
        {
            a = default;
            int n = 0;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.isInProgress) continue;
                    if (n == 0) a = t.position.ReadValue();
                    n++;
                }
                if (n > 0) return n;
            }
            var m = Mouse.current;
            if (m == null) return 0;
            a = m.position.ReadValue();
            return m.leftButton.isPressed ? 1 : (m.rightButton.isPressed || m.middleButton.isPressed) ? 2 : 0;
        }

        /// <summary>The screen point of a site cell's middle on the land (tests and the debug panel).</summary>
        public Vector2 ScreenOf(int cell)
        {
            var cam = Camera.main;
            if (cam == null || box == null) return Vector2.zero;
            float x = SiteMap.CXc(cell), z = SiteMap.CZc(cell);
            return cam.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z)));
        }
    }
}
