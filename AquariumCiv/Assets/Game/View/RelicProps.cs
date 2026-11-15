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
    /// <item><b>Keeper Doll:</b> on a post by the shrine (or the green), facing the glass.</item>
    /// <item><b>Left Sandal:</b> on the remembering stone when an heir got it back (the owner wears it).</item>
    /// </list>
    /// The Festival Mask has no place in the world (it's worn at the festival, then kept on the shelf). #718: the Sky Crumb
    /// and the Lens are gone (an old save that holds one shows it only in the book).
    /// </summary>
    public class RelicProps : MonoBehaviour
    {
        /// <summary>How much bigger than life the relics stand, so they read at default zoom (user 2026-10-30, #92).</summary>
        public const float DollScale = 2.2f, SandalScale = 2.2f;

        BoxView box;
        Transform root;
        readonly Dictionary<string, Transform> made = new();

        /// <summary>The relic props standing now (tests).</summary>
        public IReadOnlyDictionary<string, Transform> Made => made;

        void OnEnable() => box = GetComponent<BoxView>();

        void OnDisable()
        {
            if (root != null) Kill(root.gameObject);
            root = null;
            made.Clear();
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        /// <summary>A relic's moment starts (nothing to drop since #718: the Sky Crumb is gone).</summary>
        public void MomentStarted(string id) { }

        public void Sync(SimCore sim, LifePlanner planner, float dt)
        {
            if (box == null || box.Root == null || sim == null || planner == null) return;
            if (root == null)
            {
                root = new GameObject("Relics") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            var pos = planner.Positions;

            // Keeper Doll: on its post, facing the glass.
            if (sim.HasRelic(RelicId.KeeperDoll) && InReplay(RelicId.KeeperDoll, sim.FindRelic(RelicId.KeeperDoll)) && pos.TryGetPosition(LifePlanner.RelicPlace(RelicId.KeeperDoll), out float dx, out float dz))
            {
                var doll = Ensure(RelicId.KeeperDoll, BuildDollPost);
                var n = PeopleView.GlassNormal(planner.GlassSide);
                doll.localPosition = box.land.OnGround(dx + n.x * 0.08f, dz + n.y * 0.08f);
                doll.localScale = Vector3.one * DollScale;
                doll.localRotation = Quaternion.Euler(0f, Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg + 180f, 0f); // its front (-z) faces out
            }

            // Left Sandal: an heir set it on the remembering stone.
            var sandal = sim.FindRelic(RelicId.LeftSandal);
            bool onStone = sandal != null && sandal.Found && sandal.holderId != sandal.ownerId && InReplay(RelicId.LeftSandal, sandal);
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

        /// <summary>#669: a relic stands in the box only from the month it was found, in a replay. Hides the part it had if it is too early.</summary>
        bool InReplay(string id, Relic r)
        {
            bool ok = r == null || ReplayGate.Shows(r.foundMonth);
            if (!ok && made.TryGetValue(id, out var part) && part != null && part.gameObject.activeSelf) part.gameObject.SetActive(false);
            return ok;
        }

        Transform Ensure(string id, System.Action<MeshBuilder> build)
        {
            if (made.TryGetValue(id, out var t) && t != null) { if (!t.gameObject.activeSelf) t.gameObject.SetActive(true); return t; }
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
        /// <summary>The Keeper Doll on a post, about as tall as a person, facing -z.</summary>
        static void BuildDollPost(MeshBuilder mb)
        {
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(0.022f, 0.1f, 0.022f), Palette.Timber);
            PegDoll.KeeperDoll(mb, new Vector3(0, 0.1f, 0), 1.1f);
        }

        /// <summary>The remembering stone's sandal, on a cushion.</summary>
        static void BuildSandalStone(MeshBuilder mb)
        {
            mb.Box(new Vector3(0.09f, 0.01f, 0), new Vector3(0.05f, 0.02f, 0.06f), Palette.Accents[0]);
            mb.Box(new Vector3(0.09f, 0.024f, 0), new Vector3(0.026f, 0.008f, 0.05f), Palette.Hide);
        }
    }
}
