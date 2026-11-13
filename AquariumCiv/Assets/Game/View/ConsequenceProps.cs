using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// #371 (visit-loop.md 2.5): the small things a choice left in the box, code-built from the style kit's pieces in the box's palette
    /// (no new art; the real look is the user's call): a feather staked by the chief's door, a woodpile under its own little roof, the
    /// remembering stone's names, a charm pole, a stew pot, a long table, a carved bird on a ridge pole... Box units, standing on their
    /// origin, bigger than life a little (<see cref="Scale"/>) so they read at the default zoom.
    /// </summary>
    public static class ConsequenceMeshes
    {
        /// <summary>How much bigger than life the things stand, so they read at default zoom (as the relics do).</summary>
        public const float Scale = 1.5f;

        /// <summary>The mesh of a <see cref="ConsequenceKind"/> (an unknown kind: a small cairn, so nothing is ever missing).</summary>
        public static Mesh Build(string kind)
        {
            var mb = new MeshBuilder(3710 + System.Array.IndexOf(ConsequenceKind.All, kind)) { jitter = 0.025f };
            switch (kind)
            {
                case ConsequenceKind.Feather: // fb4 #526: a storyteller's stake: a pale stake in a ring of stones, a red cord, three big feathers fanned on top
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f;
                        mb.Ico(new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.05f, 0.022f, new Vector3(1f, 0.7f, 1f), Palette.Rock, 0.2f);
                    }
                    mb.Prism(Vector3.zero, 0.018f, 0.014f, 0.2f, 6, Palette.WoodLight, topCol: Palette.Plaster);
                    mb.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.048f, 0.03f, 0.048f), Palette.Accents[0]);
                    for (int i = -1; i <= 1; i++)
                    {
                        var rot = Quaternion.Euler(0f, 0f, -i * 24f);
                        var col = i == 0 ? Palette.Plaster : i < 0 ? Palette.Accents[1] : Palette.Accents[0];
                        var at = new Vector3(i * 0.04f, 0.31f - Mathf.Abs(i) * 0.015f, 0f);
                        mb.Box(at, new Vector3(0.05f, 0.2f, 0.008f), col, rot);
                        mb.Box(at, new Vector3(0.006f, 0.21f, 0.011f), Palette.Timber, rot);
                    }
                    break;
                case ConsequenceKind.Baskets: // three berry baskets, stacked
                    for (int i = 0; i < 3; i++)
                    {
                        var at = new Vector3((i - 1) * 0.1f, 0f, i == 1 ? 0.05f : 0f);
                        mb.Prism(at, 0.05f, 0.065f, 0.06f, 8, Palette.Thatch, topCol: Palette.Straw);
                        mb.Ico(at + new Vector3(0f, 0.065f, 0f), 0.04f, new Vector3(1f, 0.55f, 1f), i == 1 ? Palette.Accents[0] : Palette.Accents[4], 0.1f);
                    }
                    break;
                case ConsequenceKind.WoodpileRoof: // a tall woodpile under its own little roof
                    Pieces.Woodpile(mb, Vector3.zero, 0f);
                    foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.15f, 0.12f, 0f), new Vector3(0.02f, 0.24f, 0.02f), Palette.Timber);
                    foreach (int s in new[] { -1, 1 })
                        mb.Box(new Vector3(0f, 0.26f, s * 0.05f), new Vector3(0.36f, 0.015f, 0.12f), Palette.Thatch, Quaternion.Euler(s * 28f, 0f, 0f), true);
                    break;
                case ConsequenceKind.NameStone: // a standing stone cut with rows of names
                    mb.Prism(Vector3.zero, 0.11f, 0.08f, 0.1f, 7, Palette.RockDark, topCol: Palette.Rock);
                    mb.Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.13f, 0.22f, 0.07f), Palette.Rock);
                    mb.Box(new Vector3(0f, 0.325f, 0f), new Vector3(0.1f, 0.03f, 0.055f), Palette.RockLight);
                    for (int i = 0; i < 4; i++) mb.Box(new Vector3(0f, 0.13f + i * 0.045f, -0.036f), new Vector3(0.08f - (i % 2) * 0.02f, 0.01f, 0.004f), Palette.RockDark);
                    break;
                case ConsequenceKind.CharmPole: // a bough on a post hung with charms
                    mb.Prism(Vector3.zero, 0.02f, 0.014f, 0.36f, 6, Palette.Bark, topCol: Palette.WoodLight);
                    mb.Box(new Vector3(0.06f, 0.32f, 0f), new Vector3(0.14f, 0.014f, 0.014f), Palette.Bark);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -0.0f + i * 0.045f - 0.02f;
                        mb.Box(new Vector3(x + 0.04f, 0.29f, 0f), new Vector3(0.003f, 0.05f, 0.003f), Palette.Plaster);
                        mb.Ico(new Vector3(x + 0.04f, 0.26f, 0f), 0.014f, Vector3.one, Palette.Accents[i % Palette.Accents.Length], 0.05f);
                    }
                    mb.Ico(new Vector3(0f, 0.38f, 0f), 0.07f, new Vector3(1f, 0.7f, 1f), Palette.LeafB, 0.15f);
                    break;
                case ConsequenceKind.StewPot: // a pot on a tripod over a few stones
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        mb.Ico(new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.1f, 0.03f, new Vector3(1f, 0.7f, 1f), Palette.Rock, 0.2f);
                    }
                    for (int i = 0; i < 3; i++)
                        mb.Prism(new Vector3(Mathf.Cos(i * 2.094f), 0f, Mathf.Sin(i * 2.094f)) * 0.1f, 0.008f, 0.006f, 0.2f, 5, Palette.Bark,
                            Quaternion.AngleAxis(14f, new Vector3(-Mathf.Sin(i * 2.094f), 0f, Mathf.Cos(i * 2.094f))));
                    // #492: a clay pot of stew like the cook's camp pot (MealDirector); the green top read as a cauldron
                    mb.Prism(new Vector3(0f, 0.08f, 0f), 0.07f, 0.09f, 0.09f, 9, Palette.Terracotta, topCol: Palette.Terracotta);
                    mb.Prism(new Vector3(0f, 0.168f, 0f), 0.075f, 0.075f, 0.004f, 9, new Color(0.80f, 0.52f, 0.20f)); // the stew
                    for (int i = 0; i < 3; i++) // a few bits in the stew, so it reads as food at the default zoom
                    {
                        float a = i * 2.3f + 0.4f;
                        mb.Ico(new Vector3(Mathf.Cos(a) * 0.035f, 0.172f, Mathf.Sin(a) * 0.035f), 0.012f, new Vector3(1f, 0.5f, 1f),
                            i == 1 ? Palette.Sprout : Palette.WoodLight, 0.2f);
                    }
                    break;
                case ConsequenceKind.AxeStump: // a stump with an axe stuck in it, a notch in the handle
                    mb.Prism(Vector3.zero, 0.075f, 0.065f, 0.1f, 8, Palette.Bark, topCol: Palette.WoodLight);
                    mb.Box(new Vector3(0.0f, 0.2f, 0f), new Vector3(0.016f, 0.22f, 0.016f), Palette.WoodLight, Quaternion.Euler(0f, 0f, -12f));
                    mb.Box(new Vector3(-0.03f, 0.13f, 0f), new Vector3(0.05f, 0.03f, 0.01f), Palette.Iron, Quaternion.Euler(0f, 0f, -12f));
                    break;
                case ConsequenceKind.Staff: // a headman's staff with a ribbon, on a flat stone
                    mb.Prism(Vector3.zero, 0.06f, 0.05f, 0.02f, 7, Palette.Rock, topCol: Palette.RockLight);
                    mb.Prism(new Vector3(0f, 0.02f, 0f), 0.012f, 0.01f, 0.42f, 6, Palette.WoodLight);
                    mb.Ico(new Vector3(0f, 0.45f, 0f), 0.028f, Vector3.one, Palette.Accents[1], 0.05f);
                    mb.sway = 0.4f;
                    mb.Box(new Vector3(0.03f, 0.38f, 0f), new Vector3(0.06f, 0.05f, 0.003f), Palette.Accents[0]);
                    mb.sway = 0f;
                    break;
                case ConsequenceKind.FlourSacks: // sacks heaped by the mill, one split open
                    for (int i = 0; i < 4; i++)
                    {
                        var at = new Vector3((i % 2 - 0.5f) * 0.1f, (i / 2) * 0.07f, (i / 2) * 0.02f);
                        mb.Ico(at + new Vector3(0f, 0.04f, 0f), 0.055f, new Vector3(1f, 0.75f, 0.8f), Palette.Canvas, 0.1f);
                    }
                    mb.Ico(new Vector3(0.12f, 0.025f, -0.05f), 0.04f, new Vector3(1.2f, 0.4f, 1f), Palette.Plaster, 0.1f);
                    break;
                case ConsequenceKind.RoofBird: // a carved bird on a ridge pole
                    mb.Prism(Vector3.zero, 0.014f, 0.012f, 0.24f, 6, Palette.Timber);
                    mb.Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.2f, 0.02f, 0.02f), Palette.Thatch);
                    mb.Ico(new Vector3(0f, 0.3f, 0f), 0.04f, new Vector3(1.4f, 0.9f, 0.8f), Palette.Accents[2], 0.06f);
                    mb.Ico(new Vector3(-0.05f, 0.335f, 0f), 0.022f, Vector3.one, Palette.Accents[2], 0.05f);
                    mb.Prism(new Vector3(-0.07f, 0.335f, 0f), 0.008f, 0f, 0.03f, 4, Palette.Accents[1], Quaternion.Euler(0f, 0f, 90f));
                    mb.Box(new Vector3(0.06f, 0.31f, 0f), new Vector3(0.05f, 0.012f, 0.03f), Palette.Accents[2]);
                    break;
                case ConsequenceKind.LongTable: // a long table with benches either side
                    mb.Box(new Vector3(0f, 0.07f, 0f), new Vector3(0.44f, 0.014f, 0.1f), Palette.WoodLight);
                    foreach (int s in new[] { -1, 1 })
                    {
                        mb.Box(new Vector3(s * 0.19f, 0.035f, 0f), new Vector3(0.015f, 0.07f, 0.09f), Palette.Timber);
                        mb.Box(new Vector3(0f, 0.04f, s * 0.085f), new Vector3(0.4f, 0.012f, 0.04f), Palette.Timber);
                    }
                    for (int i = 0; i < 3; i++) mb.Ico(new Vector3(-0.12f + i * 0.12f, 0.085f, 0f), 0.02f, new Vector3(1f, 0.6f, 1f), Palette.Accents[i * 2 % Palette.Accents.Length], 0.05f);
                    break;
                case ConsequenceKind.OfferingBasket: // the best basket on a flat stone, a cloth over it
                    mb.Prism(Vector3.zero, 0.09f, 0.075f, 0.04f, 7, Palette.Rock, topCol: Palette.RockLight);
                    mb.Prism(new Vector3(0f, 0.04f, 0f), 0.055f, 0.07f, 0.07f, 8, Palette.Thatch, topCol: Palette.Straw);
                    mb.Ico(new Vector3(0f, 0.12f, 0f), 0.05f, new Vector3(1f, 0.5f, 1f), Palette.Accents[3], 0.08f);
                    break;
                case ConsequenceKind.GoatFence: // a little pen of rails, gnawed at one corner
                    foreach (int s in new[] { -1, 1 })
                    {
                        mb.Box(new Vector3(0f, 0.05f, s * 0.12f), new Vector3(0.26f, 0.012f, 0.012f), Palette.Bark);
                        mb.Box(new Vector3(0f, 0.09f, s * 0.12f), new Vector3(0.26f, 0.012f, 0.012f), Palette.Bark);
                        mb.Box(new Vector3(s * 0.13f, 0.05f, 0f), new Vector3(0.012f, 0.012f, 0.24f), Palette.WoodLight);
                        foreach (int t in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.13f, 0.05f, t * 0.12f), new Vector3(0.02f, 0.11f, 0.02f), Palette.Timber);
                    }
                    mb.Ico(new Vector3(0f, 0.04f, 0f), 0.05f, new Vector3(1.3f, 0.9f, 0.8f), Palette.Plaster, 0.06f);
                    mb.Ico(new Vector3(0.07f, 0.08f, 0f), 0.025f, Vector3.one, Palette.Plaster, 0.05f);
                    break;
                case ConsequenceKind.FishCrate: // a crate with fish on the near shore
                    mb.Box(new Vector3(0f, 0.035f, 0f), new Vector3(0.16f, 0.07f, 0.1f), Palette.WoodLight);
                    for (int i = 0; i < 3; i++)
                        mb.Ico(new Vector3(-0.04f + i * 0.04f, 0.08f, 0f), 0.025f, new Vector3(1.6f, 0.5f, 0.6f), i % 2 == 0 ? Palette.Water : Palette.WaterDeep, 0.05f);
                    break;
                case ConsequenceKind.NamePeg: // a peg with a small wooden tag
                    mb.Prism(Vector3.zero, 0.012f, 0.01f, 0.2f, 5, Palette.Bark);
                    mb.Box(new Vector3(0f, 0.17f, 0f), new Vector3(0.09f, 0.05f, 0.012f), Palette.WoodLight);
                    for (int i = 0; i < 3; i++) mb.Box(new Vector3(-0.025f + i * 0.025f, 0.17f, -0.008f), new Vector3(0.012f, 0.025f, 0.003f), Palette.Timber);
                    break;
                default:
                    for (int i = 0; i < 4; i++) mb.Ico(new Vector3(0f, 0.02f + i * 0.04f, 0f) + new Vector3((i % 2) * 0.02f, 0f, 0f), 0.06f - i * 0.01f, new Vector3(1f, 0.7f, 1f), Palette.Rock, 0.2f);
                    break;
            }
            var mesh = mb.ToMesh("Consequence_" + kind);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>#635: <paramref name="count"/> charms hanging from a tree's lower crown: each a pale cord and a little coloured charm,
        /// spread round the trunk. Origin = the tree's ground point; box units, not scaled. <paramref name="attachY"/> / <paramref name="ringR"/>:
        /// where the cords tie on (just under the leaves).</summary>
        public static Mesh Charms(int count, float attachY, float ringR)
        {
            var mb = new MeshBuilder(3790 + count) { jitter = 0.01f };
            for (int i = 0; i < count; i++)
            {
                float a = i * 2.39996f + 0.4f;                       // the golden angle: never two on one side
                float r = ringR * (0.8f + 0.2f * ((i * 7) % 3) / 2f);
                float len = 0.055f + 0.025f * (i % 3);
                var top = new Vector3(Mathf.Cos(a) * r, attachY, Mathf.Sin(a) * r);
                var col = Palette.Accents[i % Palette.Accents.Length];
                mb.Box(top - Vector3.up * len * 0.5f, new Vector3(0.006f, len, 0.006f), Palette.Plaster);
                var at = top - Vector3.up * (len + 0.026f);
                mb.Box(at, new Vector3(0.05f, 0.05f, 0.012f), col, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 45f)); // tester: 0.034 was a speck at home zoom
                mb.Ico(at - Vector3.up * 0.04f, 0.015f, Vector3.one, Palette.Plaster, 0.05f);
            }
            var mesh = mb.ToMesh("Consequence_charms_" + count);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>#635: the stand-in pole with <paramref name="count"/> charms on its bough (the sim's count, not always four).</summary>
        public static Mesh PoleWithCharms(int count)
        {
            var mb = new MeshBuilder(3780 + count) { jitter = 0.025f };
            mb.Prism(Vector3.zero, 0.02f, 0.014f, 0.36f, 6, Palette.Bark, topCol: Palette.WoodLight);
            mb.Box(new Vector3(0.06f, 0.32f, 0f), new Vector3(0.14f, 0.014f, 0.014f), Palette.Bark);
            int n = Mathf.Clamp(count, 1, 6);
            float step = n <= 4 ? 0.045f : 0.12f / (n - 1);
            for (int i = 0; i < n; i++)
            {
                float x = 0.02f + i * step;
                mb.Box(new Vector3(x, 0.29f, 0f), new Vector3(0.003f, 0.05f, 0.003f), Palette.Plaster);
                mb.Ico(new Vector3(x, 0.26f, 0f), 0.014f, Vector3.one, Palette.Accents[i % Palette.Accents.Length], 0.05f);
            }
            mb.Ico(new Vector3(0f, 0.38f, 0f), 0.07f, new Vector3(1f, 0.7f, 1f), Palette.LeafB, 0.15f);
            var mesh = mb.ToMesh("Consequence_pole_charms_" + count);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        /// <summary>Where the cords tie on for a drawn tree of play radius <paramref name="spotR"/> (BoxView.TreeSpots): the Keeper's grown tree
        /// (KeeperTreeKit stage 4) or a wild one (Pieces.Tree, radius 0.16 x its scale).</summary>
        public static void CharmTie(bool keeperTree, float spotR, out float attachY, out float ringR)
        {
            if (keeperTree) { attachY = 0.4f; ringR = 0.11f; return; }
            float s = spotR / 0.16f;
            attachY = 0.27f * s; ringR = 0.12f * s;
        }
    }

    /// <summary>
    /// #371: draws the things the Keeper's choices left in the box (<see cref="SimCore.ConsequenceProps"/>, placed by the sim at a real
    /// spot, saved with the box). Each stands on the ground at its spot, a little bigger than life. <see cref="Pulse"/> (a callback moment
    /// starting) makes one bob once, so the eye finds it. Everything hangs under the box and goes with it. Auto-created next to the box.
    /// </summary>
    public class ConsequencePropsView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static ConsequencePropsView Instance { get; private set; }
        /// <summary>Real seconds a pulse lasts.</summary>
        public const float PulseSeconds = 1.6f;

        sealed class Made { public Transform t; public Mesh mesh; public string kind; public float yaw; public Vector3 at; }

        /// <summary>#635: the mesh key of a thing: its kind, or for charms on a tree the tree and the count (a new death adds a charm).</summary>
        static string KeyOf(ConsequenceProp p, int charms) => p.anchor != PropAnchor.Tree ? p.kind : p.treeCell >= 0 ? $"charms:{p.treeCell}:{charms}" : $"pole:{charms}";

        /// <summary>#635: the drawn tree a tree thing hangs on: its cell's spot, else the nearest drawn grown tree (a wild crown is drawn on
        /// some of its cells only). False when none is drawn near.</summary>
        bool TreeSpotOf(int cell, float x, float z, out Vector3 at, out float r)
        {
            at = default; r = 0f;
            lock (box.TreeSpots)
            {
                if (box.TreeSpots.TryGetValue(cell, out var s)) { at = s.pos; r = s.r; return true; }
                float best = 0.35f * 0.35f; bool found = false;
                foreach (var kv in box.TreeSpots)
                {
                    float dx = kv.Value.pos.x - x, dz = kv.Value.pos.z - z, d = dx * dx + dz * dz;
                    if (d < best) { best = d; at = kv.Value.pos; r = kv.Value.r; found = true; }
                }
                return found;
            }
        }

        BoxView box;
        Transform root;
        SimCore drawn;
        float fx, fz, rackAngle, fishAngle = float.NaN;
        PeopleView people;
        readonly List<CampSpots.Disc> spots = new();
        readonly Dictionary<int, Made> made = new();
        readonly Dictionary<int, float> pulses = new();

        /// <summary>The things standing now, by prop id (tests).</summary>
        public IReadOnlyDictionary<int, Transform> Standing
        {
            get { var d = new Dictionary<int, Transform>(); foreach (var kv in made) d[kv.Key] = kv.Value.t; return d; }
        }

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<ConsequencePropsView>() == null) b.gameObject.AddComponent<ConsequencePropsView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            ClearAll();
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        void ClearAll()
        {
            foreach (var m in made.Values) { Kill(m.mesh); if (m.t != null) Kill(m.t.gameObject); }
            made.Clear();
            pulses.Clear();
            if (root != null) Kill(root.gameObject);
            root = null;
            drawn = null;
        }

        /// <summary>A callback moment starts at this thing: it bobs once.</summary>
        public static void Pulse(int propId)
        {
            if (Instance == null) return;
            var sim = Sim; // fb4 #566: every feather answer is the one pole
            var pole = sim?.FeatherPoleProp;
            if (pole != null && sim.FindConsequenceProp(propId)?.kind == ConsequenceKind.Feather) propId = pole.id;
            Instance.pulses[propId] = Time.unscaledTime;
        }

        void LateUpdate()
        {
            var sim = Sim;
            var list = sim?.State.consequenceProps;
            if (box == null || sim == null || list == null || list.Count == 0)
            {
                if (made.Count > 0 || root != null) ClearAll();
                return;
            }
            if (sim != drawn) { ClearAll(); drawn = sim; }
            if (root == null)
            {
                root = new GameObject("ConsequenceProps") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            if (people == null) people = PeopleView.Find(); var planner = people != null ? people.Planner : null;
            bool haveFire = planner != null && planner.FirePoint(out fx, out fz) && planner.HideFrame(out _, out _, out _, out _);
            spots.Clear();
            if (haveFire)
            {
                // fb4 #540: the buildings and the things already placed are in the way
                foreach (var b in sim.State.buildings) if (b.lotId != 0) spots.Add(new CampSpots.Disc(b.x, b.z, 0.22f));
                planner.HideFrame(out float hx, out float hz, out _, out _);
                rackAngle = Mathf.Atan2(hz - fz, hx - fx);
                fishAngle = FishRackSpot.AngleFor(fx, fz, sim.State.seed); // #543
            }
            var pole = sim.FeatherPoleProp; // fb4 #566: one feather pole, however many answers asked for a feather (old saves too)
            foreach (var p in list)
            {
                if (!p.placed) continue;
                if (p.kind == ConsequenceKind.Feather && p != pole)
                {
                    if (made.TryGetValue(p.id, out var gone)) { Kill(gone.mesh); if (gone.t != null) Kill(gone.t.gameObject); made.Remove(p.id); }
                    continue;
                }
                float px = p.x, pz = p.z;
                if (haveFire && p.anchor == PropAnchor.Fire)
                {
                    CampSpots.Spot(fx, fz, rackAngle, LifePlanner.HideFrameDistance, p.x, p.z, spots, out px, out pz, fishAngle);
                    spots.Add(new CampSpots.Disc(px, pz, CampSpots.PropRadius));
                }
                else if (haveFire) spots.Add(new CampSpots.Disc(p.x, p.z, CampSpots.PropRadius));
                // #635: charms on a tree hang from the drawn crown (no post, not scaled up); a tree not drawn yet: wait a frame
                bool onTree = p.anchor == PropAnchor.Tree && p.treeCell >= 0;
                Vector3 treeAt = default; float treeR = 0f;
                if (onTree && !TreeSpotOf(p.treeCell, p.x, p.z, out treeAt, out treeR))
                {
                    if (made.TryGetValue(p.id, out var hide) && hide.t != null) hide.t.gameObject.SetActive(false);
                    continue;
                }
                int charms = p.anchor == PropAnchor.Tree ? sim.CharmsOn(p) : 0;
                string key = KeyOf(p, charms);
                if (!made.TryGetValue(p.id, out var m) || m.t == null || m.kind != key)
                {
                    if (m != null) { Kill(m.mesh); if (m.t != null) Kill(m.t.gameObject); }
                    Mesh mesh;
                    if (onTree)
                    {
                        ConsequenceMeshes.CharmTie(p.treeCell == sim.State.eco.treeCell, treeR, out float ty, out float tr);
                        mesh = ConsequenceMeshes.Charms(charms, ty, tr);
                    }
                    else if (p.anchor == PropAnchor.Tree) mesh = ConsequenceMeshes.PoleWithCharms(charms); // tester: the pole always drew four
                    else mesh = ConsequenceMeshes.Build(p.kind);
                    m = new Made { kind = key, mesh = mesh };
                    var go = new GameObject("Consequence_" + p.kind + "_" + p.id) { hideFlags = HideFlags.DontSave };
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = m.mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = box.SolidMaterial;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    m.t = go.transform;
                    made[p.id] = m;
                }
                float s = ConsequenceMeshes.Scale;
                float lift = 0f;
                if (pulses.TryGetValue(p.id, out float t0))
                {
                    float k = (Time.unscaledTime - t0) / PulseSeconds;
                    if (k >= 1f) pulses.Remove(p.id);
                    else { float w = Mathf.Sin(k * Mathf.PI); s *= 1f + 0.25f * w; lift = 0.04f * w; }
                }
                if (!m.t.gameObject.activeSelf) m.t.gameObject.SetActive(true);
                if (onTree)
                {
                    m.t.localScale = Vector3.one * (s / ConsequenceMeshes.Scale);
                    m.t.SetLocalPositionAndRotation(treeAt + Vector3.up * lift, Quaternion.Euler(0f, p.yaw, 0f));
                    continue;
                }
                m.t.localScale = Vector3.one * s;
                m.t.SetLocalPositionAndRotation(box.land.OnGround(px, pz) + Vector3.up * lift, Quaternion.Euler(0f, p.yaw, 0f));
            }
        }
    }
}

namespace AquariumCiv.View
{
    /// <summary>#371 cheats (debug panel "Choices leave things (#371)").</summary>
    public static class ConsequenceCheats
    {
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;

        /// <summary>Every option that leaves a thing leaves it now, placed (one of each kind stands in the box).</summary>
        public static string PlaceEvery()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            int n = sim.DebugPlaceEveryProp();
            return $"{n} things stand in the box";
        }

        /// <summary>#635: raises "where do we remember the ones we lose" (a death first if nobody has died) and answers "the tree": the
        /// charms hang on the Keeper's tree if grown, else the nearest grown tree, else a pole stands in.</summary>
        public static string RememberOnTree()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            var d = sim.DebugRaiseDilemma("camp_remember");
            if (d == null) return "camp_remember can't be raised here (Camp only): " + sim.DilemmaWhyNot("camp_remember");
            if (!sim.AnswerDilemma(d.id, 1, DilemmaPath.Card)) return "could not answer it";
            var p = sim.ConsequencePropFor(d.id, 1);
            return p == null ? "answered, but no thing was left"
                : p.treeCell < 0 ? $"answered: no grown tree, a pole stands in ({sim.CharmsOn(p)} charm)"
                : $"answered: {sim.CharmsOn(p)} charm(s) on {(p.treeCell == sim.State.eco.treeCell ? "the Keeper's tree" : "a wild tree")}";
        }

        /// <summary>#635: the Keeper's tree grown now (planted first if need be); charms on a pole or a wild tree move to it next month.</summary>
        public static string GrowKeeperTree()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            return sim.DebugTreeStage(4) ? "the Keeper's tree is grown" : "no Keeper's tree can grow here";
        }

        /// <summary>#635: the oldest dies: one more charm on the tree.</summary>
        public static string OneMoreDeath()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            sim.DebugKillOldest();
            return "the oldest died";
        }

        /// <summary>fb4 #566: "Feather pole: pray now": the one pole stands (made if need be) and two grown-ups and a kid walk to it and pray.</summary>
        public static string PrayNow()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no PeopleView";
            int n = pv.Planner.DebugPrayNow(pv.Now);
            return n < 0 ? "nobody free to pray (or no box)" : $"{n} walk to the feather pole and pray";
        }

        /// <summary>fb4 #540: every fire-side thing at once: the drying rack with 6 hides, every Keeper's prop (the stew pot, the feathers...),
        /// so the overlaps can be looked at.</summary>
        public static string ShowCampProps()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            string a = EcoDebug.ShowRack(6);
            return a + "; " + PlaceEvery();
        }

        /// <summary>Fires the next callback (a choice is raised and answered first when none is waiting) and plays its moment now.</summary>
        public static string PlayCallbackNow()
        {
            var sim = Sim;
            if (sim == null) return "no box";
            if (sim.PendingCallbacks().Count == 0)
            {
                string id = System.Array.Find(DilemmaCheats.Ids, x => sim.DilemmaWhyNot(x) == null);
                if (id == null) return "no callback waits and no choice can be raised here";
                var d = sim.DebugRaiseDilemma(id);
                if (d == null || !sim.AnswerDilemma(d.id, 0, DilemmaPath.Card)) return "could not answer a choice for its callback";
            }
            var e = sim.DebugFireCallbackNow();
            if (e == null) return "no callback had anything to tell";
            return "callback told: " + HeldMomentStage.DebugPlayNextNow();
        }
    }
}
