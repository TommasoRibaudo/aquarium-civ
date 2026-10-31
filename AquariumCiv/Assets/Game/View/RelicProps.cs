using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// The relics in the world (#92; relics-and-glass-voice.md §1), code-built in the box's palette:
    /// <list type="bullet">
    /// <item><b>Sky Crumb:</b> a lump the size of a hut where it fell (it drops in with a thud when its moment starts), eaten
    /// away over the next game year; the last piece rests on a flat stone by the fire.</item>
    /// <item><b>Keeper Doll:</b> on a post by the shrine (or the green), facing the glass.</item>
    /// <item><b>Lens:</b> a brass tube on a tripod; it slowly turns to follow the camera.</item>
    /// <item><b>Left Sandal:</b> on the remembering stone when an heir got it back (the owner wears it).</item>
    /// </list>
    /// The Festival Mask has no place in the world (it's worn at the festival, then kept on the shelf).
    /// </summary>
    public class RelicProps : MonoBehaviour
    {
        /// <summary>Game months the crumb takes to be eaten down to its last piece.</summary>
        public const int CrumbMonths = 12;
        public const float DropSeconds = 0.6f;
        /// <summary>How much bigger than life the relics stand, so they read at default zoom (user 2026-10-30, #92).</summary>
        public const float CrumbScale = 1.5f, DollScale = 2.2f, LensScale = 1.9f, SandalScale = 2.2f;

        BoxView box;
        Transform root;
        readonly Dictionary<string, Transform> made = new();
        Transform crumbPiece;
        float crumbDrop = -1f, lensYaw;

        /// <summary>The relic props standing now (tests).</summary>
        public IReadOnlyDictionary<string, Transform> Made => made;

        void OnEnable() => box = GetComponent<BoxView>();

        void OnDisable()
        {
            if (root != null) Kill(root.gameObject);
            root = null;
            made.Clear();
            crumbPiece = null;
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        /// <summary>A relic's moment starts: the crumb drops in from the top of the box.</summary>
        public void MomentStarted(string id)
        {
            if (id == RelicId.SkyCrumb) crumbDrop = 0f;
        }

        public void Sync(SimCore sim, LifePlanner planner, float dt)
        {
            if (box == null || box.Root == null || sim == null || planner == null) return;
            if (root == null)
            {
                root = new GameObject("Relics") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            var pos = planner.Positions;

            // Sky Crumb: the big lump where it fell, eaten away; the last piece by the fire.
            var crumb = sim.FindRelic(RelicId.SkyCrumb);
            if (crumb != null && crumb.Found && pos.TryGetPosition(LifePlanner.RelicPlace(RelicId.SkyCrumb), out float cx, out float cz))
            {
                float left = 1f - Mathf.Clamp01((sim.State.month - crumb.foundMonth) / (float)CrumbMonths);
                var big = Ensure(RelicId.SkyCrumb, BuildCrumb);
                big.gameObject.SetActive(left > 0.02f);
                float s = Mathf.Lerp(0.3f, 1f, left);
                float drop = 0f;
                if (crumbDrop >= 0f)
                {
                    crumbDrop += dt;
                    float u = Mathf.Clamp01(crumbDrop / DropSeconds);
                    drop = (1f - u * u) * 1.4f; // falls from the top of the box, faster and faster
                    if (u >= 1f) crumbDrop = -1f;
                }
                big.localPosition = box.land.OnGround(cx, cz) + Vector3.up * drop;
                big.localScale = new Vector3(s, s * (drop > 0f ? 1f : 0.92f), s) * CrumbScale;
                if (crumbPiece == null) crumbPiece = Part("SkyCrumb_LastPiece", BuildCrumbPiece);
                var st = sim.Settlement;
                if (st != null)
                {
                    st.Fire(out float fx, out float fz, out _);
                    crumbPiece.localPosition = box.land.OnGround(fx + 0.34f, fz + 0.1f);
                    crumbPiece.localScale = Vector3.one * CrumbScale;
                }
            }

            // Keeper Doll: on its post, facing the glass.
            if (sim.HasRelic(RelicId.KeeperDoll) && pos.TryGetPosition(LifePlanner.RelicPlace(RelicId.KeeperDoll), out float dx, out float dz))
            {
                var doll = Ensure(RelicId.KeeperDoll, BuildDollPost);
                var n = PeopleView.GlassNormal(planner.GlassSide);
                doll.localPosition = box.land.OnGround(dx + n.x * 0.08f, dz + n.y * 0.08f);
                doll.localScale = Vector3.one * DollScale;
                doll.localRotation = Quaternion.Euler(0f, Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg + 180f, 0f); // its front (-z) faces out
            }

            // Lens: on its tripod, slowly following the camera.
            if (sim.HasRelic(RelicId.Lens) && pos.TryGetPosition(LifePlanner.RelicPlace(RelicId.Lens), out float lx, out float lz))
            {
                var lens = Ensure(RelicId.Lens, BuildLens);
                lens.localPosition = box.land.OnGround(lx + 0.12f, lz);
                lens.localScale = Vector3.one * LensScale;
                var cam = Camera.main;
                if (cam != null)
                {
                    var c = transform.InverseTransformPoint(cam.transform.position);
                    float want = Mathf.Atan2(c.x - lens.localPosition.x, c.z - lens.localPosition.z) * Mathf.Rad2Deg + 180f;
                    lensYaw = Mathf.MoveTowardsAngle(lensYaw, want, 20f * dt);
                }
                lens.localRotation = Quaternion.Euler(0f, lensYaw, 0f);
            }

            // Left Sandal: an heir set it on the remembering stone.
            var sandal = sim.FindRelic(RelicId.LeftSandal);
            bool onStone = sandal != null && sandal.Found && sandal.holderId != sandal.ownerId;
            if (onStone)
            {
                bool church = sim.State.buildings.Exists(b => b.type == BuildingType.Church);
                if (pos.TryGetPosition(Place.Land(PlaceKind.Remembering, church ? 1 : 0), out float rx, out float rz))
                {
                    var stone = Ensure(RelicId.LeftSandal, BuildSandalStone);
                    stone.localPosition = box.land.OnGround(rx, rz);
                    stone.localScale = Vector3.one * SandalScale;
                }
            }
            else if (made.TryGetValue(RelicId.LeftSandal, out var gone)) { Kill(gone.gameObject); made.Remove(RelicId.LeftSandal); }
        }

        Transform Ensure(string id, System.Action<MeshBuilder> build)
        {
            if (made.TryGetValue(id, out var t) && t != null) return t;
            t = Part("Relic_" + id, build);
            made[id] = t;
            return t;
        }

        Transform Part(string name, System.Action<MeshBuilder> build)
        {
            var mb = new MeshBuilder(name.GetHashCode()) { jitter = 0.02f };
            build(mb);
            var mesh = mb.ToMesh(name);
            mesh.hideFlags = HideFlags.DontSave;
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go.transform;
        }

        // ---------------- meshes ----------------
        /// <summary>A crumb the size of a hut: a golden lump with a darker crust and a few loose crumbs round it.</summary>
        static void BuildCrumb(MeshBuilder mb)
        {
            mb.Ico(new Vector3(0, 0.1f, 0), 0.16f, new Vector3(1.25f, 0.8f, 1f), Palette.Wheat, 0.22f, Palette.Thatch);
            mb.Ico(new Vector3(0.05f, 0.19f, -0.03f), 0.07f, new Vector3(1f, 0.7f, 1f), Palette.Straw, 0.25f);
            mb.Ico(new Vector3(0.24f, 0.015f, 0.06f), 0.025f, Vector3.one, Palette.Wheat, 0.3f);
            mb.Ico(new Vector3(-0.2f, 0.012f, -0.12f), 0.02f, Vector3.one, Palette.Thatch, 0.3f);
        }

        /// <summary>The last piece of sky bread on a flat stone by the fire.</summary>
        static void BuildCrumbPiece(MeshBuilder mb)
        {
            mb.Prism(Vector3.zero, 0.06f, 0.055f, 0.02f, 7, Palette.Stone, topCol: Palette.RockLight);
            mb.Ico(new Vector3(0, 0.038f, 0), 0.026f, new Vector3(1.2f, 0.8f, 1f), Palette.Wheat, 0.25f, Palette.Thatch);
        }

        /// <summary>The Keeper Doll on a post, about as tall as a person, facing -z.</summary>
        static void BuildDollPost(MeshBuilder mb)
        {
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.022f, 0.1f, 0.022f), Palette.Timber);
            PegDoll.KeeperDoll(mb, new Vector3(0, 0.1f, 0), 1.1f);
        }

        /// <summary>The Lens: three tripod legs and a brass tube on top, pointing -z.</summary>
        static void BuildLens(MeshBuilder mb)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f;
                var foot = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 0.06f);
                var top = new Vector3(0, 0.16f, 0);
                var mid = (foot + top) * 0.5f;
                var dir = (top - foot).normalized;
                mb.Box(mid, new Vector3(0.008f, (top - foot).magnitude, 0.008f), Palette.WoodLight, Quaternion.FromToRotation(Vector3.up, dir));
            }
            mb.Prism(new Vector3(0, 0.17f, 0.05f), 0.02f, 0.016f, 0.15f, 8, PegDoll.Brass, Quaternion.Euler(-80f, 0, 0), true, true, Palette.GlassEdge);
        }

        /// <summary>The remembering stone's sandal, on a cushion.</summary>
        static void BuildSandalStone(MeshBuilder mb)
        {
            mb.Box(new Vector3(0.09f, 0.01f, 0), new Vector3(0.05f, 0.02f, 0.06f), Palette.Accents[0]);
            mb.Box(new Vector3(0.09f, 0.024f, 0), new Vector3(0.026f, 0.008f, 0.05f), Palette.Hide);
        }
    }
}
