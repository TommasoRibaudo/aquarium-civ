using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #543: the camp's fish rack by the fire, drawn from <see cref="LifePlanner.TryGetFishRack"/>: a slim rack with the fish hung from it by
    /// their tails, fresh ones silver-blue and dry ones brown, one more every moment a hanger's beat goes on and one fewer as the dry ones come
    /// down. This view also runs the chain: it calls <see cref="LifePlanner.ResolveFishChain"/> every frame (hangs and take-downs whose time has
    /// come move their fish in the sim; catches are counted by <see cref="FishingView"/>). The rack stands once the pond can be fished (or fish
    /// are already on it) and goes with the camp. Code-built in a mesh of its own (<see cref="FishRackPieces"/>), rebuilt only when what hangs
    /// on it changes. One rack: #429's smoke goes up from this one.
    /// </summary>
    [DefaultExecutionOrder(6)]
    public sealed class FishRackView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static FishRackView Instance { get; private set; }

        BoxView box;
        PeopleView people;
        Transform holder, rootSeen;
        GameObject rack;
        int key;
        Mesh mesh;

        /// <summary>What the rack shows now (tests).</summary>
        public int FreshShown { get; private set; }
        public int DryShown { get; private set; }
        public bool Visible => rack != null && rack.activeSelf;
        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        PeopleView People => people != null ? people : people = PeopleView.Find();

        public static FishRackView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<FishRackView>();
            return v != null ? v : b.gameObject.AddComponent<FishRackView>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
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
            if (holder != null) Destroy(holder.gameObject);
            if (mesh != null) Destroy(mesh);
            holder = null; rack = null; mesh = null; key = 0;
        }

        void Update()
        {
            var sim = Sim;
            var pv = People;
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null || pv == null || pv.Planner == null) { Hide(); return; }
            if (rootSeen != box.Root) { holder = null; rack = null; key = 0; rootSeen = box.Root; }
            var planner = pv.Planner;
            planner.ResolveFishChain(pv.Now);
            planner.ResolveEggGathers(pv.Now); // #574: the egg gather's take is settled here, once per gather (same per-frame hook)
            if (!planner.TryGetFishRack(pv.Now, out var look)) { Hide(); return; }
            bool wanted = sim.FishingWater || look.Fish > 0 || sim.FishInBasket > 0 || sim.FishInPile > 0;
            if (!wanted) { Hide(); return; }
            int k = 17;
            k = k * 31 + look.fresh; k = k * 31 + look.dry;
            k = k * 31 + Mathf.RoundToInt(look.x * 200f); k = k * 31 + Mathf.RoundToInt(look.z * 200f);
            if (rack == null || k != key)
            {
                if (rack != null) Destroy(rack);
                if (mesh != null) Destroy(mesh);
                rack = Build(look);
                key = k;
            }
            rack.SetActive(true);
            FreshShown = look.fresh; DryShown = look.dry;
        }

        void Hide()
        {
            if (rack != null) rack.SetActive(false);
            FreshShown = DryShown = 0;
        }

        Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("Fish rack") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            return holder;
        }

        GameObject Build(in LifePlanner.FishRackLook look)
        {
            var mb = new MeshBuilder(box.seed + 543) { jitter = 0f };
            float yaw = Mathf.Atan2(look.dirX, look.dirZ) * Mathf.Rad2Deg; // local +z runs along the bar
            var centre = box.land.OnGround(look.x, look.z);
            mb.M = Matrix4x4.TRS(centre, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            int n = look.Fish;
            FishRackPieces.Rack(mb, n);
            for (int i = 0; i < n; i++) FishRackPieces.HangingFish(mb, FishRackPieces.HangPoint(i), i < look.dry, i + 1);
            mb.M = Matrix4x4.identity;
            mesh = mb.ToMesh("FishRack");
            mesh.hideFlags = HideFlags.DontSave;
            var go = new GameObject("FishRack") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Holder(), false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        // ---------------------------------------------------------------- debug (SliceDebugPanel's #543 block)
        /// <summary>"Fill fish basket": six fish in the basket and one grown-up walks it to the rack and hangs them up.</summary>
        public static string DebugFillBasket()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no PeopleView";
            int n = pv.Planner.DebugFillBasketNow(pv.Now);
            return n < 0 ? "no fish rack (camp on a living land only) or nobody free" : $"{n} fish in the basket: someone hangs them on the rack";
        }

        /// <summary>"Fish on the rack": six dry fish hang on the rack and one grown-up takes them down and carries them to the pile.</summary>
        public static string DebugRack()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no PeopleView";
            int n = pv.Planner.DebugRackNow(pv.Now);
            return n < 0 ? "no fish rack (camp on a living land only) or nobody free" : $"{n} dry fish on the rack: someone takes them down to the pile";
        }

        /// <summary>"Dried fish to pile": the pile holds nine dried fish at once.</summary>
        public static string DebugToPile()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null || !sim.EcologyOn) return "no living land";
            return $"{sim.DebugFishToPile(9)} dried fish on the pile";
        }
    }
}
