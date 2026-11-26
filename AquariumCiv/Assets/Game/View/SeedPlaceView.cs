using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #518 (playtest #513) the seed in hand (<see cref="GameHud.KitTool.Seed"/>) places the way the life slot does (#423): the first tap
    /// on the land shows a pale ghost of the seed's mound where the tree would go (the Keeper's tree at stage 0, <see cref="KeeperTreeKit"/>;
    /// a press by the glass shows it a little way in, where the tree has room), a tap on the ghost plants it, a tap elsewhere moves the ghost,
    /// a tap where no tree can grow says so and leaves the ghost, a tap off the land puts the seed back. One finger is the tool; a press
    /// that becomes a two-finger pan or pinch never plants (<see cref="KitPress"/>). The flow and the hint are <see cref="GhostTap"/>, shared
    /// with <see cref="LifePlaceView"/>; the look is <see cref="GhostLook"/>.
    /// </summary>
    [DefaultExecutionOrder(-14)]
    public class SeedPlaceView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static SeedPlaceView Instance { get; private set; }

        /// <summary>The hint while the ghost waits for its second tap (#516: the life slot's wording, naming what goes down).</summary>
        public const string TapAgainHint = "Tap again to plant the seed here.";
        public const string NotThere = "Not there. A seed can't grow in the hollow or in water."; // #668 kit-words §5
        /// <summary>#727: an earned seed needs open ground: not the camp, the water, a thicket or the Keeper's tree.</summary>
        public const string NotThereEarned = "Not there: a seed needs open ground, away from the camp, the water and the bushes.";

        [Tooltip("A press that wanders further than this (share of screen height) is a drag, not a tap.")] public float slop = 0.02f;
        [Tooltip("A tap this near the ghost's middle (box units) plants it.")] public float ghostReach = 0.28f;
        [Tooltip("The ghost's size against the real mound.")] public float ghostScale = 1.25f;
        [Tooltip("The silhouette's fill (pale) and its thin darker edge.")] public Color ghostFill = new(1f, 1f, 1f, 0.86f), ghostEdge = new(0.30f, 0.26f, 0.20f, 0.6f);

        BoxView box;
        GameHud hud;
        GhostTap ghost;
        GhostLook look;
        readonly KitPress press = new();
        int tappedCell = -1;
        static readonly Mesh[] ghostMeshes = new Mesh[4];

        /// <summary>The ghost is up, at <see cref="GhostCell"/> (the cell the tree would take).</summary>
        public bool GhostShown => ghost != null && ghost.Shown;
        public int GhostCell => ghost != null ? ghost.Cell : -1;
        /// <summary>Taps where no tree can grow, plantings made by tap, and the last answer (tests, debug panel).</summary>
        public int Refusals { get; private set; }
        public int Plantings { get; private set; }
        public string Last { get; private set; } = "";

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        GhostTap Ghost => ghost ??= new GhostTap(TapAgainHint, ghostReach);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<SeedPlaceView>() == null) b.gameObject.AddComponent<SeedPlaceView>();
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
            look?.Destroy(); look = null;
            ghost?.Hide(null);
        }

        void Update()
        {
            var sim = Sim;
            hud ??= GameHud.Instance;
            bool holding = sim != null && sim.EcologyOn && box != null && hud != null && hud.Hand == GameHud.KitTool.Seed;
            if (!holding) { HideGhost(); press.Reset(); return; }
            // tester batch 3 (the first-tap freeze): the ghost's meshes are built when the seed is taken, not on the tap that shows it
            if (!GhostShown) { bool earned = !sim.CanPlantTree; GhostMesh(false, earned); GhostMesh(true, earned); }
            DrawGhost();
            int n = KitPress.ReadPointer(out var pos);
            if (press.Tap(n, pos, slop, out var at)) TapAt(sim, at); // two fingers (a pan, a pinch) never plant: the camera's
        }

        /// <summary>One tap of the seed at a screen point (the touch path calls it; tests may too).</summary>
        public void TapAt(SimCore sim, Vector2 screen)
        {
            var mist = EcoMistView.Instance;
            if (mist == null || !mist.GroundPoint(screen, out var at)) { Last = "off the land: put back"; hud?.SetHand(GameHud.KitTool.None); return; }
            if (!sim.CanPlantAnySeed) { Last = "no seed"; hud?.SetHand(GameHud.KitTool.None); return; } // #727: the Keeper's tree, or an earned seed
            Ghost.reach = ghostReach;
            if (Ghost.OnGhost(at) && sim.TreeCellFor(Ghost.At.x, Ghost.At.z) == Ghost.Cell) { Plant(sim); return; }
            int cell = sim.TreeCellFor(at.x, at.z);
            if (cell < 0)
            {
                Refusals++;
                Last = "refused";
                Ghost.Wobble();
                hud?.Toast(sim.CanPlantTree ? NotThere : NotThereEarned, 3f);
                return;
            }
            bool moved = GhostShown;
            tappedCell = SiteMap.CellAt(at.x, at.z);
            Ghost.Show(cell, new Vector3(SiteMap.CXc(cell), 0f, SiteMap.CZc(cell)), hud, hud != null ? hud.SeedHintShown : GameHud.SeedHint);
            Last = moved ? $"ghost moved to {cell}" : $"ghost at {cell}";
        }

        void Plant(SimCore sim)
        {
            int cell = Ghost.Cell;
            float x = SiteMap.CXc(cell), z = SiteMap.CZc(cell);
            // #378: a press by the glass lands a little way in from it (the ghost showed where); say so, the seed never jumps silently
            bool shifted = tappedCell >= 0 && tappedCell != cell;
            bool keepers = sim.CanPlantTree; // the first seed is the Keeper's tree; after it, an earned seed (#727)
            if (!sim.PlantTree(x, z)) { Last = "refused"; hud?.Toast(keepers ? NotThere : NotThereEarned, 3f); return; }
            Plantings++;
            Last = $"planted at ({x:0.00}, {z:0.00})";
            HideGhost();
            box.Refresh();
            GameClock.Instance?.Save();
            hud?.SetHand(GameHud.KitTool.None);
            if (!keepers) hud?.Toast(sim.SeedsInPouch > 0 ? $"The seed is in the ground. It will grow into a tree. {sim.SeedsInPouch} more in the life jar." : "The seed is in the ground. It will grow into a tree.", 4f);
            else hud?.Toast(shifted ? "The seed went in a little way from the glass, where a tree has room. Mist it, and keep its weeds back." : "The seed is in the ground. Mist it, and keep its weeds back.", 4f);
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null) pv.Planner.ReactAt(AquariumCiv.Life.KeeperGesture.Rain, pv.Now, x, z, 1.4f);
        }

        void HideGhost()
        {
            look?.Hide();
            if (ghost != null && ghost.Shown) ghost.Hide(hud);
            tappedCell = -1;
        }

        // ---------------------------------------------------------------- the ghost: the seed's mound, pale, breathing
        void DrawGhost()
        {
            if (!GhostShown) { look?.Hide(); return; }
            var c = Ghost.Centre();
            float y = box.land.Height(Mathf.Clamp(c.x, -Land.HX, Land.HX), Land.ClampZ(c.z)) + 0.002f;
            look ??= new GhostLook(box.transform, "SeedGhost");
            // #609: the ghost's mound lies on the slope like the planted one
            KeeperTreeKit.PadFit(box.land, Mathf.Clamp(c.x, -Land.HX, Land.HX), Land.ClampZ(c.z), out var tilt, out var lift);
            var simNow = Sim; bool earned = simNow != null && !simNow.CanPlantTree;
            look.Draw(GhostMesh(false, earned), GhostMesh(true, earned), new Vector3(c.x, y + lift, c.z), ghostScale * Ghost.Breath() * Ghost.Pop(), ghostFill, ghostEdge, tilt);
        }

        /// <summary>The Keeper's tree at stage 0 (the seed in its mound, its stones and moss), its own colours washed pale (#516,
        /// <see cref="GhostLook.Pale"/>); <paramref name="edge"/>: a little larger, in white, for the darker outline behind.
        /// #668 (the landscaping polish): an earned seed (#727) grows an ordinary young tree, so its ghost is a small sapling, never the
        /// Keeper's mound.</summary>
        static Mesh GhostMesh(bool edge, bool earned)
        {
            int k = (edge ? 1 : 0) + (earned ? 2 : 0);
            if (ghostMeshes[k] != null) return ghostMeshes[k];
            var mb = new MeshBuilder(518) { jitter = 0f };
            if (edge) mb.M = Matrix4x4.Scale(new Vector3(1.12f, 1.25f, 1.12f));
            if (earned) EarnedSapling(mb);
            else KeeperTreeKit.Build(mb, Vector3.zero, 0, 0, LandColors.River);
            mb.M = Matrix4x4.identity;
            if (edge) GhostLook.Flat(mb); else GhostLook.Pale(mb);
            var m = mb.ToMesh(edge ? "SeedGhost_edge" : "SeedGhost");
            m.hideFlags = HideFlags.DontSave;
            return ghostMeshes[k] = m;
        }

        /// <summary>The earned seed's ghost: a young sapling on a little ring of turned earth (the shape the tweezers' pop and the spade use).</summary>
        static void EarnedSapling(MeshBuilder mb)
        {
            var at = mb.M;
            mb.Prism(Vector3.zero, 0.05f, 0.042f, 0.012f, 9, Palette.Bark);                       // the turned earth
            mb.M = at * Matrix4x4.Scale(Vector3.one * 1.5f);
            mb.Prism(Vector3.zero, 0.012f, 0.008f, 0.13f, 5, Palette.Bark);
            mb.Ico(new Vector3(0f, 0.15f, 0f), 0.06f, new Vector3(1f, 1.15f, 1f), Palette.LeafB, 0.2f);
            mb.Ico(new Vector3(0.02f, 0.2f, 0.01f), 0.04f, Vector3.one, Palette.LeafC, 0.2f);
            mb.M = at;
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
