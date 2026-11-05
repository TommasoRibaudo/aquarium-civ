using System.Collections.Generic;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The mess on the land (#37, ported from the prototypes' MessView): up to <see cref="MaxWeeds"/> patches of weeds standing for the overgrowth (each worth
    /// 1/<see cref="MaxWeeds"/>). Built in the style kit's flat colours with <see cref="MeshBuilder"/>, drawn in the box's own
    /// material and children of the box (not of its generated root, so a rebuild does not lose them). The view follows the
    /// sim: a weed patch the sweepers trimmed, or a clean all, shrinks away; a new one pops in.
    /// (The trash heaps were removed in #313.) The pieces are inert until
    /// <see cref="CareView"/> picks one with the finger.
    /// </summary>
    public class MessProps : MonoBehaviour
    {
        /// <summary>#94: while set, the weeds grow in one overgrown patch round this point (box coordinates) instead of being scattered.</summary>
        public static Vector2? PatchCentre;
        public const float PatchRadius = 0.5f;
        public const int MaxWeeds = 8;
        public const float WeedUnit = 1f / MaxWeeds;
        const float LeaveSeconds = 0.4f, AppearSeconds = 0.35f, Gravity = 2.2f;
        /// <summary>The weed patches' size on the land (user 2026-10-30: bigger, clearly visible at default zoom; was 1.5).</summary>
        public const float WeedScale = 2.4f;

        /// <summary>What a piece is (#311, #314): a weed patch (the overgrowth), a tuft of weed round the Keeper's tree, or a cell of the creeping vine.</summary>
        public enum Kind { Weed, TreeWeed, Vine }

        public class Piece
        {
            public bool weed;               // a weed patch or a tuft round the tree (not the vine)
            public Kind kind;
            public int key;                 // the weed patch's index; the tree tuft's index; the vine's site cell
            public Transform t;
            public Vector3 baseScale = Vector3.one;
            public float age, wobble, leaveT;
            public bool held, leaving;
            public Vector3 vel, spin;
            public float lift;
            public Vector3 Center => t.position + Vector3.up * 0.08f * t.lossyScale.y;
        }

        BoxView box;
        Transform holder;
        readonly Dictionary<int, Piece> weeds = new(), treeWeeds = new(), vines = new();
        readonly List<Piece> all = new(), leavers = new();
        readonly List<int> scratch = new();
        readonly List<Mesh> meshes = new();
        readonly Vector2[] spots = new Vector2[MaxWeeds];
        readonly bool[] spotKnown = new bool[MaxWeeds];
        Transform spotsRoot;
        long spotsMonth = -1;
        bool first = true;

        public IReadOnlyList<Piece> AllPieces => all;
        public int WeedCount => weeds.Count;
        /// <summary>Choose every weed spot again (the patch moved).</summary>
        public void ResetSpots() { spotsRoot = null; for (int i = 0; i < MaxWeeds; i++) spotKnown[i] = false; }

        void Awake()
        {
            box = GetComponent<BoxView>();
            holder = new GameObject("CareProps") { hideFlags = HideFlags.DontSave }.transform;
            holder.SetParent(transform, false);
        }

        void OnDestroy()
        {
            foreach (var m in meshes) if (m != null) Destroy(m);
            if (holder != null) Destroy(holder.gameObject);
        }

        // ---------------- follow the sim ----------------
        public void Sync(SimCore sim, float dt)
        {
            if (box == null || sim == null) return;
            // weeds: a tossed patch leaves its index free for the next one; fewer wanted drops the highest index
            float og = sim.State.overgrowth;
            int want = og < 0.03f ? 0 : Mathf.Clamp(Mathf.CeilToInt(og * MaxWeeds - 1e-3f), 1, MaxWeeds);
            RefreshSpots(sim);
            while (weeds.Count > want)
            {
                int hi = -1;
                foreach (var k in weeds.Keys) hi = Mathf.Max(hi, k);
                Leave(weeds[hi], Vector3.zero); weeds.Remove(hi);
            }
            for (int i = 0; i < MaxWeeds && weeds.Count < want; i++) if (!weeds.ContainsKey(i)) AddWeed(i);
            SyncTreeWeeds(sim);
            SyncVine(sim);
            first = false;
            Animate(dt);
        }

        void RefreshSpots(SimCore sim)
        {
            var root = box.Root;
            long month = sim.State.month;
            if (root == spotsRoot && month == spotsMonth) return;
            bool rebuilt = root != spotsRoot;
            spotsRoot = root; spotsMonth = month;
            if (root == null) return;
            for (int i = 0; i < MaxWeeds; i++)
            {
                if (spotKnown[i] && !rebuilt && box.FreeForProp(spots[i], 0.2f)) continue;
                spotKnown[i] = FindSpot(i, out spots[i]);
                if (weeds.TryGetValue(i, out var w) && spotKnown[i]) w.t.localPosition = Ground(spots[i]);
            }
        }

        bool FindSpot(int i, out Vector2 spot)
        {
            for (int k = 0; k < 60; k++)
            {
                var p = PatchCentre.HasValue
                    ? PatchCentre.Value + new Vector2(Land.Hash3(i * 41 + k, 3, 17) * 2f - 1f, Land.Hash3(i * 53 + k, 9, 23) * 2f - 1f) * PatchRadius
                    : new Vector2((Land.Hash3(i * 41 + k, 3, 17) * 2f - 1f) * (Land.HX - 0.5f), (Land.Hash3(i * 53 + k, 9, 23) * 2f - 1f) * (Land.HZ - 0.5f));
                if (!box.FreeForProp(p, 0.22f)) continue;
                bool clash = false;
                for (int j = 0; j < MaxWeeds; j++) if (j != i && spotKnown[j] && (spots[j] - p).sqrMagnitude < (PatchCentre.HasValue ? 0.26f : 0.38f) * (PatchCentre.HasValue ? 0.26f : 0.38f)) { clash = true; break; }
                if (!clash) { spot = p; return true; }
            }
            spot = default;
            return false;
        }

        Vector3 Ground(Vector2 p) => new(p.x, box.GroundHeight(p.x, p.y), p.y);

        void AddWeed(int i)
        {
            if (!spotKnown[i]) spotKnown[i] = FindSpot(i, out spots[i]);
            if (!spotKnown[i]) { spots[i] = new Vector2(0f, 1.6f); spotKnown[i] = true; }
            var piece = MakePiece(true, i, $"Weeds{i}", WeedMesh(i));
            piece.t.localPosition = Ground(spots[i]);
            weeds[i] = piece;
        }

        // ---------------- #311: the weeds round the Keeper's tree ----------------
        public const int TreeTufts = AquariumCiv.Sim.Eco.EcoModel.TreeWeedSlots;
        const float TreeWeedScale = 1.7f, TreeWeedRing = 0.24f;

        void SyncTreeWeeds(SimCore sim)
        {
            int want = sim.EcologyOn ? sim.EcoTreeWeedPieces : 0;
            var tree = sim.EcoTree;
            while (treeWeeds.Count > want)
            {
                int hi = -1;
                foreach (var k in treeWeeds.Keys) hi = Mathf.Max(hi, k);
                Leave(treeWeeds[hi], Vector3.zero); treeWeeds.Remove(hi);
            }
            for (int i = 0; i < TreeTufts && treeWeeds.Count < want; i++)
            {
                if (treeWeeds.ContainsKey(i)) continue;
                var piece = MakePiece(true, i, $"TreeWeeds{i}", WeedMesh(i + 40), Kind.TreeWeed, TreeWeedScale);
                treeWeeds[i] = piece;
            }
            foreach (var kv in treeWeeds)
            {
                float a = kv.Key * 2.1f + 0.6f;
                var p = new Vector2(tree.x + Mathf.Cos(a) * TreeWeedRing, tree.z + Mathf.Sin(a) * TreeWeedRing);
                kv.Value.t.localPosition = Ground(p);
            }
        }

        // ---------------- #314: the creeping vine ----------------
        public int VineCount => vines.Count;
        const float VineScale = 1f;

        void SyncVine(SimCore sim)
        {
            var list = sim.EcologyOn ? sim.EcoVine : System.Array.Empty<int>();
            scratch.Clear();
            foreach (var k in vines.Keys) if (!Contains(list, k)) scratch.Add(k);
            foreach (var k in scratch) { Leave(vines[k], Vector3.zero); vines.Remove(k); }
            for (int i = 0; i < list.Count; i++)
            {
                int c = list[i];
                if (vines.ContainsKey(c)) continue;
                // run along the strand: toward the next cell, or on from the one before it
                float cx = AquariumCiv.Sim.Settle.SiteMap.CXc(c), cz = AquariumCiv.Sim.Settle.SiteMap.CZc(c);
                float dx = 1f, dz = 0f;
                if (i + 1 < list.Count && Near(c, list[i + 1])) { dx = AquariumCiv.Sim.Settle.SiteMap.CXc(list[i + 1]) - cx; dz = AquariumCiv.Sim.Settle.SiteMap.CZc(list[i + 1]) - cz; }
                else if (i > 0 && Near(c, list[i - 1])) { dx = cx - AquariumCiv.Sim.Settle.SiteMap.CXc(list[i - 1]); dz = cz - AquariumCiv.Sim.Settle.SiteMap.CZc(list[i - 1]); }
                else { float h = Land.Hash3(c, 11, 3) * Mathf.PI * 2f; dx = Mathf.Cos(h); dz = Mathf.Sin(h); }
                var piece = MakePiece(false, c, $"Vine{c}", VineMesh(c), Kind.Vine, VineScale);
                piece.t.localPosition = Ground(new Vector2(cx, cz));
                piece.t.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-dz, dx) * Mathf.Rad2Deg, 0f);
                vines[c] = piece;
            }
        }

        static bool Contains(IReadOnlyList<int> list, int c) { for (int i = 0; i < list.Count; i++) if (list[i] == c) return true; return false; }
        static bool Near(int a, int b) => Mathf.Abs(AquariumCiv.Sim.Settle.SiteMap.I(a) - AquariumCiv.Sim.Settle.SiteMap.I(b)) <= 1 && Mathf.Abs(AquariumCiv.Sim.Settle.SiteMap.J(a) - AquariumCiv.Sim.Settle.SiteMap.J(b)) <= 1;

        /// <summary>One cell of the vine: a thin wavy stem along +X with small paired leaves, in a darker, bluer green than the weeds.</summary>
        static Mesh VineMesh(int seed)
        {
            var mb = new MeshBuilder(seed * 7 + 5) { jitter = 0.004f };
            const float L = 0.14f, W = 0.007f;
            const int Segs = 5;
            Vector3 prev = default;
            for (int s = 0; s <= Segs; s++)
            {
                float u = s / (float)Segs - 0.5f;
                var p = new Vector3(u * L, 0.010f + 0.004f * Mathf.Sin(s * 1.7f + seed), Mathf.Sin(u * 9f + seed * 0.3f) * 0.012f);
                if (s > 0)
                {
                    var side = Vector3.forward * W;
                    mb.Quad(prev - side, prev + side, p + side, p - side, Palette.VineStem);
                    mb.Quad(prev + side, prev - side, p - side, p + side, Palette.VineStem);
                    // a leaf pair at the joint, a little up and out, one longer than the other
                    float a = seed * 0.9f + s;
                    for (int k = 0; k < 2; k++)
                    {
                        float sgn = k == 0 ? 1f : -1f;
                        var lead = new Vector3(0.012f, 0.014f + 0.004f * Mathf.Sin(a + k), sgn * (0.026f + 0.01f * Mathf.Sin(a * 1.3f + k)));
                        var col = (s + k) % 2 == 0 ? Palette.VineA : Palette.VineB;
                        var w = new Vector3(0.012f, 0f, 0f);
                        mb.Tri(p - w, p + lead, p + w, col);
                        mb.Tri(p + w, p + lead, p - w, col);
                    }
                }
                prev = p;
            }
            return mb.ToMesh("CareVine");
        }

        Piece MakePiece(bool weed, int key, string name, Mesh mesh, Kind kind = Kind.Weed, float scale = WeedScale)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(holder, false);
            go.transform.localRotation = Quaternion.Euler(0f, Land.Hash3(key, weed ? 5 : 6, 2) * 360f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshes.Add(mesh);
            var p = new Piece { weed = weed, kind = kind, key = key, t = go.transform, baseScale = Vector3.one * scale, age = first ? AppearSeconds : 0f };
            go.transform.localScale = first ? p.baseScale : Vector3.zero;
            all.Add(p);
            return p;
        }

        // ---------------- looks ----------------
        static Mesh WeedMesh(int seed)
        {
            var mb = new MeshBuilder(seed * 13 + 3);
            mb.sway = 0.3f;
            mb.Ico(new Vector3(0.02f, 0.04f, 0), 0.07f, new Vector3(1.2f, 0.75f, 1.1f), Palette.Pine, 0.25f);
            mb.sway = 0f;
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7f * Mathf.PI * 2f + mb.Range(0f, 0.6f), r = mb.Range(0.02f, 0.09f);
                AquariumCiv.View.Pieces.GrassTuft(mb, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), mb.Range(0.12f, 0.24f), k % 2 == 0 ? Palette.LeafB : Palette.Pine);
            }
            for (int k = 0; k < 3; k++)   // thistle heads, so the patch reads as weeds and not as a nicer lawn
                mb.Ico(new Vector3(mb.Range(-0.06f, 0.06f), mb.Range(0.17f, 0.24f), mb.Range(-0.06f, 0.06f)), 0.014f, Vector3.one, Palette.Flowers[3], 0.1f);
            return mb.ToMesh("CareWeeds");
        }

        // ---------------- touch ----------------
        /// <summary>The mess nearest to the finger (within <paramref name="radiusPx"/> on screen), or null.</summary>
        public Piece Pick(Camera cam, Vector2 screenPos, float radiusPx)
        {
            Piece best = null;
            float bestD = radiusPx;
            foreach (var p in all)
            {
                if (p.leaving || p.t == null) continue;
                var sp = cam.WorldToScreenPoint(p.Center);
                if (sp.z <= 0f) continue;
                float d = Vector2.Distance(screenPos, sp);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>The piece really under a finger: within <paramref name="leastPx"/> of its middle, or inside its own size on screen when it's bigger than that.</summary>
        public Piece PickUnder(Camera cam, Vector2 screenPos, float leastPx)
        {
            Piece best = null;
            float bestD = float.MaxValue, tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            foreach (var p in all)
            {
                if (p.leaving || p.t == null) continue;
                var sp = cam.WorldToScreenPoint(p.Center);
                if (sp.z <= 0f) continue;
                float reach = Mathf.Max(leastPx, PieceRadius * p.t.lossyScale.y * Screen.height / (2f * sp.z * tan));
                float d = Vector2.Distance(screenPos, sp);
                if (d < reach && d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>About how far a piece of mess reaches from its middle at scale 1 (box units).</summary>
        const float PieceRadius = 0.1f;

        /// <summary>Every piece's screen point (tests).</summary>
        public void OnScreen(Camera cam, List<Vector2> into)
        {
            into.Clear();
            foreach (var p in all)
                if (!p.leaving && p.t != null) into.Add(cam.WorldToScreenPoint(p.Center));
        }

        public void Wobble(Piece p) { if (p != null) p.wobble = 1f; }
        public void SetHeld(Piece p, bool held) { if (p != null) p.held = held; }

        /// <summary>The piece is thrown out with this world velocity and leaves the dictionaries (the caller tells the sim).</summary>
        public void Throw(Piece p, Vector3 velocity)
        {
            if (p == null) return;
            Forget(p);
            Leave(p, velocity);
        }

        void Forget(Piece p)
        {
            switch (p.kind)
            {
                case Kind.TreeWeed: treeWeeds.Remove(p.key); break;
                case Kind.Vine: vines.Remove(p.key); break;
                default: weeds.Remove(p.key); break;
            }
        }

        /// <summary>#311: the tweezers lift a piece out: it pops up a little and is gone (the caller tells the sim).</summary>
        public void Pluck(Piece p)
        {
            if (p == null || p.leaving) return;
            Forget(p);
            Leave(p, Vector3.up * 0.45f * Mathf.Max(0.4f, p.baseScale.y * 0.5f));
        }

        /// <summary>#311: a wild sapling came out of the ground at a box-local point: its ghost pops up and fades while the land rebuilds without it.</summary>
        public void PopSapling(Vector3 local)
        {
            var mb = new MeshBuilder(local.GetHashCode()) { jitter = 0.01f };
            mb.Prism(Vector3.zero, 0.012f, 0.008f, 0.13f, 5, Palette.Bark);
            mb.Ico(new Vector3(0f, 0.15f, 0f), 0.06f, new Vector3(1f, 1.15f, 1f), Palette.LeafB, 0.2f);
            mb.Ico(new Vector3(0.02f, 0.2f, 0.01f), 0.04f, Vector3.one, Palette.LeafC, 0.2f);
            var p = MakePiece(true, Mathf.Abs(local.GetHashCode()), "PluckedSapling", mb.ToMesh("CareSapling"), Kind.Weed, 1f);
            p.t.localPosition = local;
            p.t.localScale = Vector3.one;
            Leave(p, Vector3.up * 0.45f);
        }

        void Leave(Piece p, Vector3 velocity)
        {
            if (p.leaving) return;
            p.leaving = true; p.held = false; p.leaveT = 0f; p.vel = velocity;
            p.spin = new Vector3(Land.Hash3(p.key, 1, 1) - 0.5f, 0.4f, Land.Hash3(p.key, 2, 2) - 0.5f) * 540f;
            leavers.Add(p);
        }

        void Animate(float dt)
        {
            foreach (var p in all)
            {
                if (p.leaving || p.t == null) continue;
                p.age += dt;
                float g = Mathf.Clamp01(p.age / AppearSeconds);
                float grow = 1f - (1f - g) * (1f - g) * (1f - 0.6f * g); // eases out with a little overshoot
                p.wobble = Mathf.Max(0f, p.wobble - dt * 1.6f);
                p.lift = Mathf.MoveTowards(p.lift, p.held ? 0.05f : 0f, dt * 0.5f);
                float sway = p.wobble * Mathf.Sin(Time.unscaledTime * 26f) * 7f;
                p.t.localScale = p.baseScale * grow * (p.held ? 1.12f : 1f);
                float yaw = p.t.localEulerAngles.y;
                p.t.localRotation = Quaternion.Euler(sway, yaw, sway * 0.6f);
                var lp = p.t.localPosition;
                lp.y = box.GroundHeight(lp.x, lp.z) + p.lift;
                p.t.localPosition = lp;
            }
            for (int i = leavers.Count - 1; i >= 0; i--)
            {
                var p = leavers[i];
                if (p.t == null) { leavers.RemoveAt(i); all.Remove(p); continue; }
                p.leaveT += dt;
                p.vel.y -= Gravity * dt;
                p.t.position += p.vel * dt;
                p.t.Rotate(p.spin * dt, Space.Self);
                float k = Mathf.Clamp01(p.leaveT / LeaveSeconds);
                p.t.localScale = p.baseScale * (1f - k * k);
                if (k >= 1f) { Destroy(p.t.gameObject); leavers.RemoveAt(i); all.Remove(p); }
            }
        }
    }
}
