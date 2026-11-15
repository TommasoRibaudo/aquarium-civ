using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #423 the life tool on the land (placed-life spec §6, B12.0): while a species is in hand (<see cref="GameHud.KitTool.Life"/>),
    /// the ground it may live on glows softly (<see cref="SimCore.LifeGround"/>) and the rest stays as it is. The first tap on the
    /// glow shows a faint ghost of the founding group there (flat pale outlines, breathing); a tap on the ghost places it
    /// (<see cref="SimCore.PlaceLife"/>), a tap elsewhere on the glow moves the ghost, a tap on dark ground gives a small "no"
    /// wobble and nothing else, a tap off the land puts the tool back. One finger is the tool, two the camera
    /// (<see cref="EcoMistView.ClaimsPress"/> already holds one finger for any kit tool). The arrival itself is #424's.
    /// <para>#518: the tap and the ghost's flow are <see cref="KitPress"/> and <see cref="GhostTap"/>, shared with the seed
    /// (<see cref="SeedPlaceView"/>); the silhouettes are <see cref="GhostLook"/>.</para>
    /// </summary>
    [DefaultExecutionOrder(-14)]
    public class LifePlaceView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static LifePlaceView Instance { get; private set; }

        [Tooltip("A press that wanders further than this (share of screen height) is a drag, not a tap.")] public float slop = 0.02f;
        [Tooltip("A tap this near the ghost's middle (box units) places it.")] public float ghostReach = 0.32f;
        [Tooltip("Tester feedback 3: the glow's outline (alpha = its strongest). A warm honey line, darker and more saturated than the pale sand so it reads as a target at the default zoom; no green or yellow (playtest #510: it read as grass).")] public Color glow = new(1f, 0.6f, 0.26f, 0.7f);
        [Tooltip("Tester feedback 3: the valid ground inside the outline: a warm bright cream, so the ground itself lifts (alpha = its strongest).")] public Color glowFillColor = new(1f, 0.95f, 0.84f, 0.3f);
        [Tooltip("How deep the glow's slow breath goes (0 = steady): gentle, never a blink.")][Range(0f, 0.5f)] public float glowBreath = 0.28f;
        [Tooltip("The silhouettes' fill (pale) and their thin darker edge.")] public Color ghostFill = new(1f, 1f, 1f, 0.86f), ghostEdge = new(0.30f, 0.26f, 0.20f, 0.6f);
        [Tooltip("User 2026-11-08: the rest of the land dims a little while a species is in hand (0 = off).")][Range(0f, 0.6f)] public float dim = 0.22f;

        BoxView box;
        GameHud hud;
        Material fx;
        GameObject glowGo, ghostGo, noGo, restoreGo, dimGo;
        Material glowMat, dimMat, restoreMat;
        GhostTap ghost;
        GhostLook look;
        readonly KitPress press = new();
        Mesh dimMesh;
        Mesh glowMesh, rimMesh, ghostMesh, noMesh;
        MaterialPropertyBlock mpb;
        readonly List<int> cells = new();
        LifeSpecies glowFor;
        int glowStamp = -1;
        float noT = -9f;

        /// <summary>The glow is built again next frame (after its colours change: tests, the debug panel).</summary>
        public void RebuildGlow() => glowStamp = -1;

        /// <summary>Cells glowing now (0 when nothing is in hand).</summary>
        public int GlowCells { get; private set; }
        /// <summary>The land is dimmed now (a species in hand), and how strongly (user 2026-11-08).</summary>
        public bool Dimmed => dimGo != null && dimGo.activeSelf;
        /// <summary>The hint while the ghost waits for its second tap: #516 it names what is going down, once.</summary>
        public static string TapAgainHintFor(LifeSpecies s) => IsWater(s) ? $"Tap the water again to set the {GroupName(s)} down." : $"Tap again to set the {GroupName(s)} down here.";
        /// <summary>fb4 #542: the ducks and the fish live on the pond as a whole: any tap on its water counts, and the camera frames the pond.</summary>
        public static bool IsWater(LifeSpecies s) => s == LifeSpecies.Ducks || s == LifeSpecies.Fish;
        LifeSpecies framedFor = (LifeSpecies)(-1);
        /// <summary>#516 what the people call each founding group (the hint's one name for it).</summary>
        public static string GroupName(LifeSpecies s) => s switch
        {
            LifeSpecies.Thicket => "bushes",
            LifeSpecies.Rabbits => "rabbits",
            LifeSpecies.Deer => "deer",
            LifeSpecies.Ducks => "ducks",
            LifeSpecies.Fish => "fish",
            _ => "wolves",
        };
        /// <summary>How long the tap-again hint may stay up while the ghost waits (it goes with the ghost before that).</summary>
        public const float GhostHintSeconds = GhostTap.HintSeconds;
        /// <summary>The ghost is shown, at <see cref="GhostCell"/>.</summary>
        /// <summary>fb4 #542: the pond tool (ducks, fish) is in hand: the camera keeps the pond framed.</summary>
        public bool FramingWater => framedFor == (hud != null ? hud.HeldLife : framedFor) && IsWater(framedFor) && hud != null && hud.Hand == GameHud.KitTool.Life;
        public bool GhostShown => ghostGo != null && ghostGo.activeSelf;
        public int GhostCell => Ghost.Cell;
        GhostTap Ghost => ghost ??= new GhostTap(TapAgainHintFor(LifeSpecies.Thicket), ghostReach);
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
            foreach (var go in new[] { glowGo, ghostGo, noGo, restoreGo, dimGo }) if (go != null) Destroy(go);
            foreach (var m in new[] { glowMesh, rimMesh, ghostMesh, noMesh, dimMesh }) if (m != null) Destroy(m);
            foreach (var m in new[] { glowMat, dimMat, restoreMat }) if (m != null) Destroy(m);
            look?.Destroy(); look = null; ghost?.Hide(null);
            glowGo = ghostGo = noGo = restoreGo = dimGo = null; glowMesh = rimMesh = ghostMesh = noMesh = dimMesh = null;
            glowMat = dimMat = restoreMat = null;
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
                GlowCells = 0; glowStamp = -1; press.Reset(); framedFor = (LifeSpecies)(-1);
                DrawNo();
                return;
            }
            var s = hud.HeldLife;
            // the glow follows the land: rebuilt when the species changes or the land moved on (a season, a placing)
            int stamp = (int)(sim.State.month * 8 + (int)s);
            if (s != glowFor || stamp != glowStamp)
            {
                if (GhostShown && (s != ghostFor || !sim.LifeGroundOk(s, GhostCell))) HideGhost(); // #516: the ghost is always the species in hand
                glowFor = s; glowStamp = stamp; BuildGlow(sim, s);
            }
            if (glowGo != null)
            {
                // a slow soft breath (about 2.6 s), never a blink; the colours are the mesh's (outline and fill)
                float pulse = 1f - glowBreath * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * 2.4f));
                mpb.SetColor(ColorId, new Color(1f, 1f, 1f, pulse));
                glowGo.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
            }
            if (IsWater(s) && framedFor != s) { framedFor = s; FramePond(); }
            else if (!IsWater(s) && framedFor != s) { framedFor = s; FrameGround(sim, s); }
            Dim();
            DrawGhost();
            DrawNo();
            Taps(sim, s);
        }

        static readonly int ColorId = Shader.PropertyToID("_Color");

        // ---------------------------------------------------------------- input
        void Taps(SimCore sim, LifeSpecies s)
        {
            int n = KitPress.ReadPointer(out var pos);
            if (press.Tap(n, pos, slop, out var at)) TapAt(sim, s, at); // two fingers (a pan, a pinch) never place: the camera's (#518)
        }

        /// <summary>One tap of the life tool at a screen point (the touch path calls it; tests may too).</summary>
        public void TapAt(SimCore sim, LifeSpecies s, Vector2 screen)
        {
            var mist = EcoMistView.Instance;
            if (mist == null || !mist.GroundPoint(screen, out var at)) { Last = "off the land: put back"; hud?.SetHand(GameHud.KitTool.None); return; }
            int cell = SiteMap.CellAt(at.x, at.z);
            // fb4 #542: the player taps the water they SEE; the ray goes on to the pond's bed, which can sit a cell or more further on
            if (IsWater(s) && (cell < 0 || !sim.LifeGroundOk(s, cell)) && SurfaceCell(screen, out int sc) && sim.LifeGroundOk(s, sc)) { cell = sc; at = box.land.OnGround(SiteMap.CXc(sc), SiteMap.CZc(sc)); }
            Ghost.reach = ghostReach;
            // fb4 #542: on the pond any tap on its water sets the group down where the ghost stands (the first tap on the water showed it)
            bool onWater = IsWater(s) && cell >= 0 && sim.LifeGroundOk(s, cell);
            if (GhostShown && (onWater || Ghost.OnGhost(at)) && sim.CanPlaceLife(s, GhostCell))
            {
                Place(sim, s, GhostCell);
                return;
            }
            if (cell < 0 || !sim.LifeGroundOk(s, cell))
            {
                Refusals++;
                Last = $"no: cell {cell}";
                Ghost.Wobble();
                noAt = box.land.OnGround(at.x, at.z); noT = Time.unscaledTime;
                return;
            }
            bool moved = GhostShown;
            ShowGhost(s, cell); // the tap-again hint stays while the ghost waits and goes with it (GhostTap)
            LifeArrivalView.Instance?.LeanOnGhost(s, Ghost.At); // user 2026-11-08: the camera leans in so the group reads at phone size
            Last = moved ? $"ghost moved to {cell}" : $"ghost at {cell}";
        }

        /// <summary>fb4 #542: the water tool was just taken: the camera frames the whole pond (the glow's cells), so the first tap is on a big target.</summary>
        void FramePond()
        {
            if (cells.Count == 0) return;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (int c in cells)
            {
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); z0 = Mathf.Min(z0, z); z1 = Mathf.Max(z1, z);
            }
            float width = Mathf.Max(x1 - x0, (z1 - z0) * 1.3f) + 0.6f;
            LifeArrivalView.Instance?.LeanOnPond(hud.HeldLife, new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f), width);
        }

        /// <summary>#643 (playtest): a land group was just taken and too little of its glowing ground is on screen (the wolves' den spots sit
        /// deep in the wood, often out of view): the camera flies to the valid cell nearest the camp, close enough for the glow to read.
        /// Ground already in view is left alone.</summary>
        public float GroundFrameZoom = 0.5f;
        public const int SmallGround = 30;
        public int GroundFrames { get; private set; }
        void FrameGround(SimCore sim, LifeSpecies s)
        {
            var cam = Camera.main;
            var bc = cam != null ? cam.GetComponent<BoxCamera>() : null;
            if (bc == null || cells.Count == 0) return;
            int seen = 0;
            foreach (int c in cells)
            {
                var v = cam.WorldToViewportPoint(box.transform.TransformPoint(box.land.OnGround(SiteMap.CXc(c), SiteMap.CZc(c))));
                if (v.z > 0f && v.x > 0.08f && v.x < 0.92f && v.y > 0.2f && v.y < 0.85f) seen++;
            }
            bool small = cells.Count < SmallGround && bc.zoom > GroundFrameZoom + 0.15f; // a few cells, seen from far: a speck
            if (seen * 2 >= cells.Count && !small) return;
            int at = sim.LifePlaceCellNearCamp(s);
            if (at < 0) at = cells[0];
            bc.FlyTo(box.transform.TransformPoint(box.land.OnGround(SiteMap.CXc(at), SiteMap.CZc(at))), Mathf.Min(bc.zoom, GroundFrameZoom));
            GroundFrames++;
        }

        /// <summary>The cell under the water's surface at a screen point (the ray meets the water plane, not the bed).</summary>
        bool SurfaceCell(Vector2 screen, out int cell)
        {
            cell = -1;
            var cam = Camera.main;
            if (cam == null) return false;
            float w = box.layout != null && box.layout.eco != null ? box.layout.eco.waterY : Land.WaterY;
            var ray = cam.ScreenPointToRay(screen);
            var o = box.transform.InverseTransformPoint(ray.origin);
            var d = box.transform.InverseTransformDirection(ray.direction);
            if (d.y > -0.0001f) return false;
            float t = (w - o.y) / d.y;
            if (t < 0f) return false;
            var p = o + d * t;
            cell = SiteMap.CellAt(p.x, p.z);
            return cell >= 0;
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
                // #516: the glow draws the outline (rimMesh); the soft sheet (glowMesh) is the restore's
                rimMesh = new Mesh { name = "LifeGlowRim" }; rimMesh.MarkDynamic();
                glowGo.GetComponent<MeshFilter>().sharedMesh = rimMesh;
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
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var rims = new List<Color32>(); var tris = new List<int>();
            var index = new Dictionary<int, int>();
            int V(int i, int j)
            {
                int key = j * VX + i;
                if (index.TryGetValue(key, out int v)) return v;
                float x = SiteMap.X0 + i * SiteMap.Cell, z = SiteMap.Z0 + j * SiteMap.Cell;
                v = verts.Count;
                verts.Add(new Vector3(x, GroundY(Mathf.Clamp(x, -Land.HX, Land.HX), Mathf.Clamp(z, -Land.HZ, Land.HZ), s), z));
                float a = lit[key];
                float soft = a * a * (3f - 2f * a);
                cols.Add(new Color(1f, 1f, 1f, soft)); // smoothstep: a soft rim (the restore)
                // #516 the glow: a soft line on the valid ground's edge (corners shared by valid and dark cells); tester feedback 3: in a
                // warm honey that stands off pale sand, over a bright cream fill on the ground inside
                float edge = 4f * a * (1f - a);
                var rc = Color.Lerp(glowFillColor, glow, edge).linear; // picked in the eye's terms; the vertex colours are linear
                rc.a = Mathf.Max(glow.a * edge, glowFillColor.a * soft);
                rims.Add(rc);
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
            rimMesh.Clear();
            rimMesh.indexFormat = glowMesh.indexFormat;
            rimMesh.SetVertices(verts); rimMesh.SetColors(rims); rimMesh.SetTriangles(tris, 0);
            rimMesh.RecalculateBounds();
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
            if (ghostGo == null) ghostGo = Child("LifeGhost", out ghostMesh);
            ghostGo.SetActive(true);
            Ghost.SetHint(TapAgainHintFor(s), hud);
            Ghost.Show(cell, target, hud, GameHud.LifeHintFor(s));
            ghostFor = s;
        }

        LifeSpecies ghostFor;
        Vector3 noAt;

        void HideGhost()
        {
            if (ghostGo != null && ghostGo.activeSelf) ghostGo.SetActive(false);
            look?.Hide();
            Ghost.Hide(hud);
        }

        void DrawGhost()
        {
            if (!GhostShown) return;
            float breath = Ghost.Breath(), pop = Ghost.Pop();
            var c = Ghost.Centre();
            // user 2026-11-08: faint outlines of the actual group (the real bushes, rabbits, ducks; placeholder deer and wolves), not rings.
            // #516 (playtest #509/#510): the thicket's area ring went too: with it the bushes read as a blob in a ring, not as bushes
            Silhouettes(c, breath * pop);
        }

        /// <summary>The ghost's silhouettes: the group's pale shapes over a thin darker edge, side-on to the camera, breathing.</summary>
        void Silhouettes(Vector3 c, float scale)
        {
            look ??= new GhostLook(box.transform, "LifeGhost");
            bool water = ghostFor == LifeSpecies.Ducks || ghostFor == LifeSpecies.Fish;
            float y = GroundY(Mathf.Clamp(c.x, -Land.HX, Land.HX), Mathf.Clamp(c.z, -Land.HZ, Land.HZ), ghostFor) - (water ? 0.008f : 0.012f);
            look.Draw(LifeLooks.Ghost(ghostFor, box, false), LifeLooks.Ghost(ghostFor, box, true), new Vector3(c.x, y, c.z), scale, ghostFill, ghostEdge);
        }

        static Material DimMaterial(int queue) => GhostLook.DimMaterial(queue);

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
        /// <summary>Where the ghost's middle is on screen now, on the ground or the water it stands on (tests: tester feedback 3, the ghost
        /// sits in the clear middle band after the lean). Zero with no ghost.</summary>
        public Vector2 GhostScreen
        {
            get
            {
                var cam = Camera.main;
                if (cam == null || box == null || !GhostShown) return Vector2.zero;
                var c = Ghost.At;
                float y = GroundY(Mathf.Clamp(c.x, -Land.HX, Land.HX), Mathf.Clamp(c.z, -Land.HZ, Land.HZ), ghostFor);
                return cam.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(c.x, y, c.z)));
            }
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
