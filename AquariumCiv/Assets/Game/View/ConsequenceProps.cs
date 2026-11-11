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
                if (!made.TryGetValue(p.id, out var m) || m.t == null || m.kind != p.kind)
                {
                    if (m != null) { Kill(m.mesh); if (m.t != null) Kill(m.t.gameObject); }
                    m = new Made { kind = p.kind, mesh = ConsequenceMeshes.Build(p.kind) };
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
