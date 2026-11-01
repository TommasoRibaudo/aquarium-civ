using System.Collections.Generic;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// #154: what's on the town's stalls: produce, goods and loaves, from <see cref="TownStalls"/> (what the sim already has, no new
    /// economy). A set of its own like <see cref="StashProps"/>: the stall in the layout is the bare counter, and this draws the goods on
    /// it, one shared mesh per stock, so a change of stock doesn't rebuild the land. Stalls are taken in a stable order (their seeds).
    /// </summary>
    public class StallProps : MonoBehaviour
    {
        /// <summary>Real seconds between looks at the stock: it changes by the game month.</summary>
        const float CheckSeconds = 0.5f;
        /// <summary>The counter's top on <see cref="Pieces.MarketStall"/>.</summary>
        const float Top = 0.1f;

        BoxView box;
        Transform root;
        float nextCheck;
        readonly List<PieceData> stalls = new();
        readonly List<(MeshFilter f, MeshRenderer r)> shown = new();
        readonly Dictionary<int, Mesh> meshes = new();

        /// <summary>How many stalls stand, how many carry anything, and the fullest one's level (tests).</summary>
        public int StallCount => stalls.Count;
        public int Stocked { get; private set; }
        public int FullestLevel { get; private set; }
        /// <summary>The level of each stall, in order (tests, the debug panel).</summary>
        public readonly List<int> Levels = new();

        void OnEnable() => box = GetComponent<BoxView>();

        void OnDisable()
        {
            if (root != null) Kill(root.gameObject);
            foreach (var m in meshes.Values) Kill(m);
            meshes.Clear();
            shown.Clear();
            root = null;
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        public void Sync(SimCore sim)
        {
            if (box == null || box.Root == null || sim == null || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckSeconds;
            Refresh(sim);
        }

        /// <summary>Reads the stock and the stalls now.</summary>
        public void Refresh(SimCore sim)
        {
            if (box == null || box.layout == null) return;
            stalls.Clear();
            foreach (var p in box.layout.pieces) if (p.kind == PieceKind.Stall && p.era >= Era.Town) stalls.Add(p);
            stalls.Sort((a, b) => a.seed.CompareTo(b.seed));
            if (root == null)
            {
                root = new GameObject("Stalls") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            while (shown.Count < stalls.Count)
            {
                var go = new GameObject("StallGoods") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root, false);
                var f = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = box.SolidMaterial;
                r.shadowCastingMode = ShadowCastingMode.On;
                shown.Add((f, r));
            }
            Stocked = FullestLevel = 0;
            Levels.Clear();
            for (int i = 0; i < shown.Count; i++)
            {
                var (f, r) = shown[i];
                bool on = i < stalls.Count;
                var stock = on ? TownStalls.StockFor(sim, i, stalls.Count) : default;
                on = on && !stock.Empty;
                if (i < stalls.Count) Levels.Add(stock.Level);
                if (f.gameObject.activeSelf != on) f.gameObject.SetActive(on);
                if (!on) continue;
                Stocked++;
                FullestLevel = Mathf.Max(FullestLevel, stock.Level);
                var p = stalls[i];
                f.transform.SetLocalPositionAndRotation(box.land.OnGround(p.x, p.z), Quaternion.Euler(0f, p.yaw, 0f));
                f.transform.localScale = Vector3.one * Mathf.Max(0.02f, p.grow);
                f.sharedMesh = MeshFor(stock);
            }
        }

        Mesh MeshFor(StallStock s)
        {
            int key = s.produce + s.goods * 4 + s.bread * 16;
            if (meshes.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder(154 + key) { jitter = 0.01f };
            Goods(mb, s);
            m = mb.ToMesh("StallGoods");
            m.hideFlags = HideFlags.DontSave;
            return meshes[key] = m;
        }

        /// <summary>Produce heaps on the left, pots on the right, loaves along the front edge; a full stall's sacks and baskets spill onto the ground in front.</summary>
        static void Goods(MeshBuilder mb, StallStock s)
        {
            for (int i = 0; i < s.produce * 3; i++)
            {
                int row = i / 3, n = i % 3;
                float y = Top + 0.022f + (row == 2 ? 0.034f : row == 1 ? 0.01f : 0f);
                float z = row == 1 ? 0.026f : row == 2 ? 0.006f : -0.02f;
                mb.Ico(new Vector3(-0.1f + n * 0.042f, y, z), 0.024f, Vector3.one, n == 1 ? Palette.Wheat : Palette.Ember, 0.1f);
            }
            for (int i = 0; i < s.goods; i++)
                mb.Prism(new Vector3(0.03f + i * 0.04f, Top, 0.016f), 0.024f, 0.017f, 0.05f, 6, Color.Lerp(Palette.Terracotta, Palette.SoilLight, i * 0.25f), topCol: Palette.Door);
            for (int i = 0; i < s.bread; i++)
                mb.Ico(new Vector3(0.03f + i * 0.042f, Top + 0.016f, -0.034f), 0.02f, new Vector3(1.3f, 0.75f, 0.9f), new Color(0.73f, 0.45f, 0.2f), 0.08f);
            if (s.Level < TownStalls.MaxLevel) return;
            mb.Ico(new Vector3(-0.08f, 0.042f, -0.11f), 0.042f, new Vector3(1f, 1.1f, 0.85f), Color.Lerp(Palette.Canvas, Palette.Hide, 0.35f), 0.08f); // a sack
            mb.Prism(new Vector3(0.07f, 0f, -0.11f), 0.03f, 0.04f, 0.04f, 8, Palette.Thatch, topCol: Palette.Ember); // a basket
        }

        static int debugStep;

        /// <summary>Debug (#154): the stalls round empty, half and full, by setting food and wood.</summary>
        public static string DebugStock(string how = null)
        {
            var sim = Game.GameClock.Instance != null ? Game.GameClock.Instance.ActiveSim : null;
            var v = FindAnyObjectByType<StallProps>();
            if (sim == null || v == null) return "no stalls";
            how ??= new[] { "empty", "half", "full" }[debugStep++ % 3];
            TownStalls.DebugStock(how, sim.State.population, out float food, out float wood);
            sim.State.food = food;
            sim.State.wood = wood;
            v.Refresh(sim);
            return $"stalls {how}: {string.Join(" ", v.Levels)}";
        }
    }
}
