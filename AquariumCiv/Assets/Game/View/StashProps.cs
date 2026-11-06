using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// The camp's stash by the fire (#150; <see cref="CampStash"/>): a log stack, baskets and folded hides that grow and shrink with
    /// the stock. Code-built in the box's palette, a set of its own like <see cref="RelicProps"/> and not a layout piece, so nothing
    /// that plans the box has to know about it. Every piece stands somewhere fixed in the pile (<see cref="LogSpots"/>,
    /// <see cref="BasketSpots"/>, <see cref="HideSpots"/>: x across, z towards the fire), all of it within
    /// <see cref="CampStash.PileExtent"/>. Gone with the camp: <see cref="LifePlanner.TryGetStash"/> says false from the village on.
    /// </summary>
    public class StashProps : MonoBehaviour
    {
        /// <summary>The sizes of a piece (box units): a log's length and radius, a basket's foot and height, half a folded hide.</summary>
        public const float LogLength = 0.1f, LogRadius = 0.0135f, BasketRadius = 0.024f, BasketHeight = 0.034f, HideHalfX = 0.024f, HideHalfZ = 0.018f, HideHeight = 0.009f;

        /// <summary>Where the logs lie, bottom row first, at the back of the pile, so a low stock is a few logs on the ground.</summary>
        public static readonly Vector3[] LogSpots = Logs();

        /// <summary>Where the baskets stand; the last sits on the others.</summary>
        public static readonly Vector3[] BasketSpots =
        {
            new(-0.056f, 0f, 0.046f), new(-0.008f, 0f, 0.054f), new(0.03f, 0f, 0.066f), new(-0.032f, BasketHeight, 0.05f),
        };

        /// <summary>Where the hides lie: one folded stack.</summary>
        public static readonly Vector3[] HideSpots =
        {
            new(0.062f, 0f, 0.026f), new(0.062f, HideHeight, 0.026f), new(0.062f, 2f * HideHeight, 0.026f),
        };

        static Vector3[] Logs()
        {
            var spots = new List<Vector3>();
            const float back = -0.072f, step = 0.027f, rise = 0.023f;
            for (int row = 0; row < 4; row++)
                for (int i = 0; i < 4 - row; i++)
                    spots.Add(new Vector3(0f, LogRadius + row * rise, back + (i + row * 0.5f) * step));
            return spots.ToArray();
        }

        BoxView box;
        Transform root;
        Mesh logMesh, basketMesh, hideMesh;
        readonly List<Transform> logs = new(), baskets = new(), hides = new();
        StashLook shown;
        bool drawn;

        /// <summary>The pieces standing now (tests): what's lit, by kind.</summary>
        public int LogsShown => CountOn(logs);
        public int BasketsShown => CountOn(baskets);
        public int HidesShown => CountOn(hides);
        public bool Visible => root != null && root.gameObject.activeSelf;
        public Transform Root => root;

        static int CountOn(List<Transform> parts)
        {
            int n = 0;
            foreach (var t in parts) if (t.gameObject.activeSelf) n++;
            return n;
        }

        // ---- #361: the pile answers a tipped basket (animation-specs B1.f: Squash 0.88 -> 1)
        /// <summary>How far the pile squashes on a landing (0.12 = to 0.88), and how long it takes to settle.</summary>
        public const float SquashDepth = 0.12f, SquashSeconds = 0.6f;
        float bumpAge = -1f, bumpDepth;
        /// <summary>Squashes the pile once (and a pile already settling is squashed again from the top). <paramref name="depth"/> 0.12 is the spec's 0.88.</summary>
        public void Bump(float depth = SquashDepth)
        {
            if (bumpAge >= 0f && bumpDepth * Mathf.Exp(-bumpAge / 0.12f) >= depth) return; // still squashed deeper than this: let it settle
            bumpAge = 0f; bumpDepth = depth;
        }
        /// <summary>The pile's current squash (1 = at rest), for tests.</summary>
        public float SquashNow => root != null ? root.localScale.y : 1f;
        /// <summary>The top of the pile in world space (the landing spot of a tipped basket). False while the stash isn't drawn.</summary>
        public bool TryGetPileTop(out Vector3 world)
        {
            world = default;
            if (root == null || !drawn || box == null || box.land == null || !root.gameObject.activeSelf) return false;
            world = root.parent.TransformPoint(box.land.OnGround(shown.x, shown.z) + Vector3.up * 0.04f);
            return true;
        }

        void LateUpdate()
        {
            if (bumpAge < 0f || root == null) return;
            bumpAge += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (bumpAge >= SquashSeconds || !drawn || box == null || box.land == null)
            {
                bumpAge = -1f;
                root.localScale = Vector3.one; root.localPosition = Vector3.zero;
                return;
            }
            float k = 1f - bumpDepth * Mathf.Exp(-bumpAge / 0.12f) * Mathf.Cos(bumpAge * Mathf.PI * 2f * 2f);
            float w = 1f + (1f - k) * 0.5f;
            var scale = new Vector3(w, k, w);
            var pivot = box.land.OnGround(shown.x, shown.z);
            root.localScale = scale;
            root.localPosition = pivot - Vector3.Scale(pivot, scale); // squashed about the pile's foot
        }

        void OnEnable() => box = GetComponent<BoxView>();

        void OnDisable()
        {
            if (root != null) Kill(root.gameObject);
            Kill(logMesh); Kill(basketMesh); Kill(hideMesh);
            root = null;
            logs.Clear(); baskets.Clear(); hides.Clear();
            drawn = false;
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        public void Sync(LifePlanner planner)
        {
            if (box == null || box.Root == null || planner == null) return;
            bool on = planner.TryGetStash(out var look);
            if (root != null && root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) return;
            if (root == null) Build();
            if (drawn && Same(look, shown)) return;
            shown = look;
            drawn = true;
            Draw(look);
        }

        static bool Same(in StashLook a, in StashLook b) =>
            a.x == b.x && a.z == b.z && a.faceX == b.faceX && a.faceZ == b.faceZ &&
            a.stock.logs == b.stock.logs && a.stock.baskets == b.stock.baskets && a.stock.hides == b.stock.hides;

        void Draw(in StashLook s)
        {
            float turn = Mathf.Atan2(s.faceX, s.faceZ) * Mathf.Rad2Deg; // the pile's z towards the fire
            var rot = Quaternion.Euler(0f, turn, 0f);
            Lay(logs, LogSpots, s, rot, s.stock.logs);
            Lay(baskets, BasketSpots, s, rot, s.stock.baskets);
            Lay(hides, HideSpots, s, rot, s.stock.hides);
        }

        void Lay(List<Transform> parts, Vector3[] spots, in StashLook s, Quaternion rot, int count)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                bool on = i < count;
                parts[i].gameObject.SetActive(on);
                if (!on) continue;
                var at = rot * spots[i];
                // each on the ground under it, so the pile sits on the land's small slopes
                parts[i].SetLocalPositionAndRotation(box.land.OnGround(s.x + at.x, s.z + at.z) + Vector3.up * spots[i].y, rot);
            }
        }

        void Build()
        {
            root = new GameObject("Stash") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(transform, false);
            logMesh = Mesh("StashLog", mb => Pieces.Log(mb, Vector3.zero, LogRadius, LogLength, 90f, Palette.Bark));
            basketMesh = Mesh("StashBasket", mb => mb.Prism(Vector3.zero, 0.018f, BasketRadius, BasketHeight, 8, Palette.Thatch, topCol: Palette.Ember));
            hideMesh = Mesh("StashHide", mb => mb.Box(new Vector3(0f, HideHeight * 0.5f, 0f), new Vector3(HideHalfX * 2f, HideHeight, HideHalfZ * 2f), Palette.Hide));
            Parts(logs, LogSpots.Length, "StashLog", logMesh);
            Parts(baskets, BasketSpots.Length, "StashBasket", basketMesh);
            Parts(hides, HideSpots.Length, "StashHide", hideMesh);
        }

        static Mesh Mesh(string name, System.Action<MeshBuilder> build)
        {
            var mb = new MeshBuilder(name.GetHashCode()) { jitter = 0.02f };
            build(mb);
            var mesh = mb.ToMesh(name);
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        void Parts(List<Transform> into, int count, string name, Mesh mesh)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(name + i) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = box.SolidMaterial;
                r.shadowCastingMode = ShadowCastingMode.On;
                go.SetActive(false);
                into.Add(go.transform);
            }
        }

        // ---------------- debug (the debug panel) ----------------
        /// <summary>Debug: the camp's food and wood at a low or a full stock (the sim's month may trim the food to its cap).</summary>
        public static string DebugStock(SimCore sim, bool high)
        {
            if (sim == null) return "no sim";
            sim.State.food = high ? CampStash.FullFood : 6f;
            sim.State.wood = high ? CampStash.FullWood : 8f;
            return high ? "stash full" : "stash low";
        }
    }
}
