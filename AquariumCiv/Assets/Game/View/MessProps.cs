using System.Collections.Generic;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The mess on the land (#37, ported from the prototypes' MessView): a heap of junk per <see cref="TrashItem"/> where the
    /// sim says it lies, and up to <see cref="MaxWeeds"/> patches of weeds standing for the overgrowth (each worth
    /// 1/<see cref="MaxWeeds"/>). Built in the style kit's flat colours with <see cref="MeshBuilder"/>, drawn in the box's own
    /// material and children of the box (not of its generated root, so a rebuild does not lose them). The view follows the
    /// sim: a heap the sweepers took, or a clean all, shrinks away; a new heap pops in. The pieces are inert until
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
        /// <summary>The heaps' and weed patches' size on the land (user 2026-10-30: bigger, clearly visible at default zoom;
        /// was 1.7 and 1.5).</summary>
        public const float HeapScale = 2.6f, WeedScale = 2.4f;

        public class Piece
        {
            public bool weed;
            public int key;                 // the heap's sim id, or the weed patch's index
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
        readonly Dictionary<int, Piece> heaps = new(), weeds = new();
        readonly List<Piece> all = new(), leavers = new();
        readonly List<Mesh> meshes = new();
        readonly Vector2[] spots = new Vector2[MaxWeeds];
        readonly bool[] spotKnown = new bool[MaxWeeds];
        Transform spotsRoot;
        long spotsMonth = -1;
        bool first = true;
        readonly HashSet<int> seen = new();

        public IReadOnlyList<Piece> AllPieces => all;
        public int HeapCount => heaps.Count;
        public int WeedCount => weeds.Count;
        /// <summary>Choose every weed spot again (the patch moved).</summary>
        public void ResetSpots() { spotsRoot = null; for (int i = 0; i < MaxWeeds; i++) spotKnown[i] = false; }

        public bool TryGetHeap(int id, out Piece p) => heaps.TryGetValue(id, out p);

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
            // heaps
            seen.Clear();
            foreach (var t in sim.State.trash)
            {
                seen.Add(t.id);
                if (!heaps.ContainsKey(t.id)) AddHeap(t);
            }
            List<int> gone = null;
            foreach (var kv in heaps) if (!seen.Contains(kv.Key)) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null) foreach (int id in gone) { Leave(heaps[id], Vector3.zero); heaps.Remove(id); }

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

        void AddHeap(TrashItem t)
        {
            var piece = MakePiece(false, t.id, $"Heap{t.id}", HeapMesh(t.id));
            piece.t.localPosition = Ground(new Vector2(t.x, t.z));
            heaps[t.id] = piece;
        }

        void AddWeed(int i)
        {
            if (!spotKnown[i]) spotKnown[i] = FindSpot(i, out spots[i]);
            if (!spotKnown[i]) { spots[i] = new Vector2(0f, 1.6f); spotKnown[i] = true; }
            var piece = MakePiece(true, i, $"Weeds{i}", WeedMesh(i));
            piece.t.localPosition = Ground(spots[i]);
            weeds[i] = piece;
        }

        Piece MakePiece(bool weed, int key, string name, Mesh mesh)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(holder, false);
            go.transform.localRotation = Quaternion.Euler(0f, Land.Hash3(key, weed ? 5 : 6, 2) * 360f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshes.Add(mesh);
            var p = new Piece { weed = weed, key = key, t = go.transform, baseScale = Vector3.one * (weed ? WeedScale : HeapScale), age = first ? AppearSeconds : 0f };
            go.transform.localScale = first ? p.baseScale : Vector3.zero;
            all.Add(p);
            return p;
        }

        // ---------------- looks ----------------
        static Mesh HeapMesh(int seed)
        {
            var mb = new MeshBuilder(seed * 7 + 1) { jitter = 0.02f };
            // a mud patch, a broken crate, a sack, a pot, bones: reads as somebody's rubbish at a glance
            mb.Ico(new Vector3(0, 0.012f, 0), 0.1f, new Vector3(1.1f, 0.14f, 1f), Palette.Mud, 0.1f);
            mb.Box(new Vector3(-0.035f, 0.032f, 0.0f), new Vector3(0.085f, 0.06f, 0.07f), Palette.WoodLight, Quaternion.Euler(0, 24, 0), true);
            mb.Box(new Vector3(-0.01f, 0.085f, 0.012f), new Vector3(0.06f, 0.012f, 0.05f), Palette.Bark, Quaternion.Euler(0, -32, 14), true);
            mb.Ico(new Vector3(0.055f, 0.04f, 0.02f), 0.045f, new Vector3(1f, 1.15f, 0.9f), Palette.Hide, 0.18f);
            mb.Prism(new Vector3(0.0f, 0.0f, -0.07f), 0.03f, 0.018f, 0.045f, 6, Palette.Terracotta);
            mb.Box(new Vector3(0.07f, 0.01f, -0.05f), new Vector3(0.05f, 0.012f, 0.012f), Palette.Canvas, Quaternion.Euler(0, 40, 0), true);
            mb.Box(new Vector3(0.05f, 0.01f, -0.075f), new Vector3(0.035f, 0.012f, 0.012f), Palette.Canvas, Quaternion.Euler(0, -20, 0), true);
            return mb.ToMesh("CareHeap");
        }

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

        public void Wobble(Piece p) { if (p != null) p.wobble = 1f; }
        public void SetHeld(Piece p, bool held) { if (p != null) p.held = held; }

        /// <summary>The piece is thrown out with this world velocity and leaves the dictionaries (the caller tells the sim).</summary>
        public void Throw(Piece p, Vector3 velocity)
        {
            if (p == null) return;
            if (p.weed) weeds.Remove(p.key); else heaps.Remove(p.key);
            Leave(p, velocity);
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
