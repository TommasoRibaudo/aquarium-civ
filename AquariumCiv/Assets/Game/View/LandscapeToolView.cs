using System.Collections.Generic;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #730 phase 2 (docs/design/landscaping.md 8.3): the plucking tool in hand (<see cref="GameHud.KitTool.Landscape"/>; its name is still the
    /// user's, <see cref="SimCore.LandscapeToolName"/>). A ring on the ground marks what it would take nearest the finger (or the mouse)
    /// (<see cref="SimCore.EcoNearestLandscape"/>): a seedling, a young tree or a bush, never the Keeper's tree. A tap lifts that one out; a held
    /// finger drags along and lifts what it passes, as the tweezers do. The plucked thing pops up and fades (<see cref="MessProps.PopSapling"/>)
    /// while the land rebuilds without it. One finger is the tool; a second finger makes the press the camera's (<see cref="KitPress"/>).
    /// A tap on the Keeper's tree or a grown tree says it stays.
    /// </summary>
    [DefaultExecutionOrder(-14)]
    public class LandscapeToolView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static LandscapeToolView Instance { get; private set; }

        public const string KeepersTreeStays = "Your tree stays. The spade lifts seedlings, trees and bushes.";
        public const string GrownTreeStays = "Grown trees stay. The spade lifts seedlings, young trees and bushes.";

        [Tooltip("How far from the finger (box units) a thing still counts as touched.")] public float reach = CareView.SaplingReach;
        [Tooltip("A held finger lifts at most one thing this often (seconds).")] public float dragEvery = 0.12f;
        [Tooltip("The target ring's colour (the life glow's warm honey).")] public Color ringColor = new(1f, 0.66f, 0.3f, 1f);
        [Tooltip("#668: the dark rim either side of the ring, so it reads on grass and sand.")] public Color ringEdge = new(0.18f, 0.11f, 0.06f, 0.9f);

        BoxView box;
        GameHud hud;
        MessProps props;
        GameObject ringGo;
        Mesh ringMesh;
        Material fx;
        bool pressing, active, multi, plucked;
        float downT, lastPluckT = -9f;
        Vector2 lastPos;

        /// <summary>The cell the ring marks now, or -1.</summary>
        public int Target { get; private set; } = -1;
        /// <summary>Things lifted by the tool this session, and what the last was (tests, debug panel).</summary>
        public int Plucks { get; private set; }
        public PluckKind LastKind { get; private set; }
        public string Last { get; private set; } = "";
        /// <summary>Tests and shots: a screen point the ring follows as if a finger hovered there (null: the real pointer).</summary>
        public Vector2? HoverOverride;

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<LandscapeToolView>() == null) b.gameObject.AddComponent<LandscapeToolView>();
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
            if (ringGo != null) Destroy(ringGo);
            if (ringMesh != null) Destroy(ringMesh);
            ringGo = null; ringMesh = null;
        }

        void Update()
        {
            var sim = Sim;
            hud ??= GameHud.Instance;
            bool holding = sim != null && sim.EcologyOn && box != null && hud != null && hud.Hand == GameHud.KitTool.Landscape;
            if (!holding) { pressing = active = false; HideRing(); Target = -1; return; }
            int n = KitPress.ReadPointer(out var pos);
            if (n > 0)
            {
                if (!pressing)
                {
                    pressing = true; multi = false; plucked = false;
                    active = n == 1 && !UiInput.Over(pos) && hud.IsVisible && !hud.SheetOpen && !hud.CameraMode;
                    downT = Time.unscaledTime; lastPluckT = -9f;
                }
                if (n > 1) { multi = true; active = false; } // two fingers are the camera's until every finger is up
                lastPos = pos;
                if (active && Time.unscaledTime - downT >= KitPress.SecondFingerGrace && Time.unscaledTime - lastPluckT >= dragEvery)
                    if (PluckAt(sim, pos)) { lastPluckT = Time.unscaledTime; plucked = true; }
            }
            else if (pressing)
            {
                pressing = false;
                // a tap quicker than the grace lifts on its lift; a tap that found nothing says why when it is a tree that stays
                if (active && !multi && !plucked && !PluckAt(sim, lastPos)) SayWhyNot(sim, lastPos);
                else if (plucked) GameClock.Instance?.Save();
                active = false;
            }
            var hover = HoverOverride ?? (n == 1 && !multi ? pos : (UnityEngine.InputSystem.Touchscreen.current == null && UnityEngine.InputSystem.Mouse.current != null ? pos : (Vector2?)null));
            DrawRing(sim, hover);
        }

        /// <summary>Lifts what the tool would take nearest a screen point (the touch path; tests may call it). True if something came out.</summary>
        public bool PluckAt(SimCore sim, Vector2 screen)
        {
            var mist = EcoMistView.Instance;
            if (sim == null || mist == null || !mist.GroundPoint(screen, out var at)) return false;
            int cell = sim.EcoNearestLandscape(at.x, at.z, reach);
            bool young = cell >= 0 && (EcoCover)sim.State.eco.cover[cell] == EcoCover.Young;
            cell = sim.EcoLandscapePluck(at.x, at.z, reach, out var kind);
            if (cell < 0) return false;
            var local = new Vector3(SiteMap.CXc(cell), 0f, SiteMap.CZc(cell));
            local.y = box.GroundHeight(local.x, local.z);
            props ??= GetComponent<MessProps>() ?? FindAnyObjectByType<MessProps>();
            props?.PopSapling(local, kind == PluckKind.Bush, kind == PluckKind.Tree ? 2.4f : young ? 1.6f : 1f);
            box.Refresh(); // the land rebuilds without it (and BoxView's tree and bush spots with it)
            if (!pressing) GameClock.Instance?.Save(); // a drag saves once, on its lift
            Plucks++;
            LastKind = kind;
            Last = $"{kind} at {cell}";
            GameClock.Tel?.Cleaned("prune", true);
            Sfx.Play(CueLibrary.Pluck, 0f, kind == PluckKind.Bush ? 0.9f : 1f);
            CareView.Instance?.Burst(box.transform.TransformPoint(local) + Vector3.up * 0.06f, 3, 0.08f, 0.1f);
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null) pv.Planner.ReactAt(AquariumCiv.Life.KeeperGesture.Prune, pv.Now, local.x, local.z, 1.4f);
            return true;
        }

        /// <summary>A tap that took nothing: on the Keeper's tree or a grown tree, say that it stays.</summary>
        void SayWhyNot(SimCore sim, Vector2 screen)
        {
            var mist = EcoMistView.Instance;
            if (mist == null || !mist.GroundPoint(screen, out var at)) return;
            var tree = sim.EcoTree;
            if (tree.cell >= 0 && Vector2.Distance(new Vector2(at.x, at.z), new Vector2(SiteMap.CXc(tree.cell), SiteMap.CZc(tree.cell))) < 0.25f)
            { Last = "the Keeper's tree stays"; hud?.Toast(KeepersTreeStays, 3f, through: true); return; }
            int c = SiteMap.CellAt(at.x, at.z);
            if (c < 0) return;
            int i0 = SiteMap.I(c), j0 = SiteMap.J(c);
            for (int j = Mathf.Max(0, j0 - 2); j <= Mathf.Min(SiteMap.NZ - 1, j0 + 2); j++)
                for (int i = Mathf.Max(0, i0 - 2); i <= Mathf.Min(SiteMap.NX - 1, i0 + 2); i++)
                    if ((EcoCover)sim.State.eco.cover[SiteMap.Index(i, j)] == EcoCover.Tree)
                    { Last = "a grown tree stays"; hud?.Toast(GrownTreeStays, 3f, through: true); return; }
            Last = "nothing to lift";
        }

        // ---------------------------------------------------------------- the target ring
        void DrawRing(SimCore sim, Vector2? screen)
        {
            Target = -1;
            var mist = EcoMistView.Instance;
            if (screen == null || mist == null || UiInput.Over(screen.Value) || !mist.GroundPoint(screen.Value, out var at)) { HideRing(); return; }
            int cell = sim.EcoNearestLandscape(at.x, at.z, reach);
            if (cell < 0) { HideRing(); return; }
            Target = cell;
            var cv = (EcoCover)sim.State.eco.cover[cell];
            float r = cv == EcoCover.Bush ? 0.09f : cv == EcoCover.Young ? 0.1f : cv == EcoCover.Tree ? 0.18f : 0.065f;
            r *= 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f); // a small breath, so it reads as "this one"
            if (ringGo == null)
            {
                ringGo = new GameObject("LandscapeRing");
                ringGo.transform.SetParent(box.transform, false);
                ringMesh = new Mesh { name = "LandscapeRing" };
                ringMesh.MarkDynamic();
                ringGo.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                var mr = ringGo.AddComponent<MeshRenderer>();
                fx ??= ShapingView.LoadFx();
                mr.sharedMaterial = fx;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            ringGo.SetActive(true);
            // #668 (the landscaping polish: the ring was hard to see on grass): a wider honey ring over a dark rim on each side, and a
            // little larger, so it reads on sand, grass and under a crown alike
            verts.Clear(); cols.Clear(); tris.Clear();
            var mid = new Vector3(SiteMap.CXc(cell), 0f, SiteMap.CZc(cell));
            r = Mathf.Max(r * 1.15f, 0.08f);
            Ring(mid, r, RingWidth + 0.02f, ringEdge, 0f);
            Ring(mid, r, RingWidth, ringColor, 0.002f);
            ringMesh.Clear();
            ringMesh.SetVertices(verts); ringMesh.SetColors(cols); ringMesh.SetTriangles(tris, 0);
            ringMesh.RecalculateBounds();
        }

        /// <summary>#668: the honey ring's width (box units); the dark rim shows 0.01 past it on each side.</summary>
        public const float RingWidth = 0.026f;

        void HideRing() { if (ringGo != null && ringGo.activeSelf) ringGo.SetActive(false); }

        readonly List<Vector3> verts = new();
        readonly List<Color32> cols = new();
        readonly List<int> tris = new();

        /// <summary>Adds one ring band to the lists (the caller fills the mesh); <paramref name="up"/> lifts it over the band under it.</summary>
        void Ring(Vector3 c, float r, float width, Color col, float up)
        {
            Color32 c32 = col.linear;
            const int seg = 32;
            int b = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                float xo = c.x + cs * (r + width * 0.5f), zo = c.z + sn * (r + width * 0.5f);
                float xi = c.x + cs * (r - width * 0.5f), zi = c.z + sn * (r - width * 0.5f);
                verts.Add(new Vector3(xo, Y(xo, zo) + up, zo));
                verts.Add(new Vector3(xi, Y(xi, zi) + up, zi));
                cols.Add(c32); cols.Add(c32);
                if (i == seg) continue;
                int o = b + i * 2;
                tris.Add(o); tris.Add(o + 1); tris.Add(o + 3); tris.Add(o); tris.Add(o + 3); tris.Add(o + 2);
            }
        }

        float Y(float x, float z) => box.land.Height(Mathf.Clamp(x, -Land.HX, Land.HX), Land.ClampZ(z)) + 0.016f;

        /// <summary>The screen point of a site cell's middle on the land (tests and the debug panel).</summary>
        public Vector2 ScreenOf(int cell)
        {
            var cam = Camera.main;
            if (cam == null || box == null) return Vector2.zero;
            return cam.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(SiteMap.CXc(cell), SiteMap.CZc(cell))));
        }
    }
}
