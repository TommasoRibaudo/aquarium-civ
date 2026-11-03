using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// The static land under the ecology: SiteMap's own height, slope and base fertility on the 60 x 44 site grid, plus the
    /// basins from a priority flood (Ecology Lab's Land.cs). A basin is a closed hollow; the main one's spill level is the
    /// people's flood line (keepers-hands 5.3: they never build below it). Immutable after construction, so one instance
    /// per terrain preset is shared (<see cref="For"/>).
    /// </summary>
    public sealed class EcoLand
    {
        public const int NX = SiteMap.NX, NZ = SiteMap.NZ, N = SiteMap.N;

        public readonly float[] height, slope, baseSoil;
        /// <summary>Priority-flood fill level: the height water would stand at before spilling out of the hollow.</summary>
        public readonly float[] fill = new float[N];
        /// <summary>Basin id per cell (-1 = not in a closed hollow).</summary>
        public readonly int[] basin = new int[N];
        public readonly List<Basin> basins = new List<Basin>();
        public readonly bool[] yard = new bool[N];
        /// <summary>Cells no lot may take: the hollows the water can reach (below the main hollow's spill level).</summary>
        public readonly bool[] noBuild = new bool[N];
        public readonly float campX, campZ;
        public float minHeight = float.MaxValue, maxHeight = float.MinValue;
        /// <summary>The largest basin: the one the water table surfaces in first.</summary>
        public Basin main;

        public sealed class Basin
        {
            public int id;
            public float spill, bottom;
            public readonly List<int> cells = new List<int>();
        }

        /// <summary>The flood line: the main hollow's spill level.</summary>
        public float FloodLine => main != null ? main.spill : -1000f;

        public EcoLand(SiteMap map)
        {
            height = map.height; slope = map.slope; baseSoil = map.baseFert;
            campX = map.greenX; campZ = map.greenZ;
            for (int c = 0; c < N; c++)
            {
                yard[c] = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), campX, campZ) < SiteMap.GreenR;
                minHeight = MathF.Min(minHeight, height[c]);
                maxHeight = MathF.Max(maxHeight, height[c]);
            }
            PriorityFlood();
            if (main != null)
                for (int c = 0; c < N; c++) noBuild[c] = basin[c] >= 0 && height[c] < main.spill;
        }

        static readonly Dictionary<int, EcoLand> cache = new Dictionary<int, EcoLand>();

        /// <summary>The shared land of a terrain preset (thread-safe: the run ahead lives a copy of the box off the main thread).</summary>
        public static EcoLand For(int preset)
        {
            lock (cache)
            {
                if (!cache.TryGetValue(preset, out var l))
                {
                    // Static layers only: the live arrays and the flag for the eco pond don't matter to them.
                    cache[preset] = l = new EcoLand(new SiteMap(new SettleState { terrain = preset, flags = new byte[N] }, false, true));
                }
                return l;
            }
        }

        public bool InBasin(int c) => basin[c] >= 0;

        // A tiny binary min-heap (System.Collections.Generic.PriorityQueue is not in Unity's runtime).
        sealed class Heap
        {
            readonly List<(float key, int cell)> a = new List<(float, int)>();
            public int Count => a.Count;
            public void Push(int cell, float key)
            {
                a.Add((key, cell));
                int i = a.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (a[p].key <= a[i].key) break;
                    (a[p], a[i]) = (a[i], a[p]); i = p;
                }
            }
            public int Pop()
            {
                var top = a[0];
                var last = a[a.Count - 1];
                a.RemoveAt(a.Count - 1);
                if (a.Count > 0)
                {
                    a[0] = last;
                    int i = 0;
                    while (true)
                    {
                        int l = 2 * i + 1, r = l + 1, m = i;
                        if (l < a.Count && a[l].key < a[m].key) m = l;
                        if (r < a.Count && a[r].key < a[m].key) m = r;
                        if (m == i) break;
                        (a[m], a[i]) = (a[i], a[m]); i = m;
                    }
                }
                return top.cell;
            }
        }

        /// <summary>Barnes et al. priority flood from the box's edge (the glass is where a hollow would overflow along the
        /// ground; conservative for the flood line). Then basins = connected cells whose fill is above the ground.</summary>
        void PriorityFlood()
        {
            const float eps = 0.002f;
            var done = new bool[N];
            var pq = new Heap();
            for (int c = 0; c < N; c++)
            {
                int i = SiteMap.I(c), j = SiteMap.J(c);
                if (i == 0 || j == 0 || i == NX - 1 || j == NZ - 1) { fill[c] = height[c]; done[c] = true; pq.Push(c, height[c]); }
            }
            var nb = new int[4];
            while (pq.Count > 0)
            {
                int c = pq.Pop();
                int n = Neighbours4(c, nb);
                for (int k = 0; k < n; k++)
                {
                    int o = nb[k];
                    if (done[o]) continue;
                    done[o] = true;
                    fill[o] = MathF.Max(height[o], fill[c]);
                    pq.Push(o, fill[o]);
                }
            }
            for (int c = 0; c < N; c++) basin[c] = -1;
            for (int c = 0; c < N; c++)
            {
                if (basin[c] >= 0 || fill[c] - height[c] < eps) continue;
                var b = new Basin { id = basins.Count, spill = fill[c], bottom = float.MaxValue };
                var stack = new Stack<int>();
                stack.Push(c); basin[c] = b.id;
                while (stack.Count > 0)
                {
                    int k = stack.Pop();
                    b.cells.Add(k);
                    b.spill = MathF.Max(b.spill, fill[k]);
                    b.bottom = MathF.Min(b.bottom, height[k]);
                    int n = Neighbours4(k, nb);
                    for (int q = 0; q < n; q++)
                    {
                        int o = nb[q];
                        if (basin[o] >= 0 || fill[o] - height[o] < eps) continue;
                        basin[o] = b.id; stack.Push(o);
                    }
                }
                basins.Add(b);
            }
            foreach (var b in basins) if (main == null || b.cells.Count > main.cells.Count) main = b;
        }

        public static int Neighbours4(int c, int[] into)
        {
            int i = SiteMap.I(c), j = SiteMap.J(c), n = 0;
            if (i > 0) into[n++] = c - 1;
            if (i < NX - 1) into[n++] = c + 1;
            if (j > 0) into[n++] = c - NX;
            if (j < NZ - 1) into[n++] = c + NX;
            return n;
        }

        static readonly int[] DI = { -1, 0, 1, -1, 1, -1, 0, 1 }, DJ = { -1, -1, -1, 0, 0, 1, 1, 1 };

        /// <summary>The 8 neighbours into <paramref name="into"/>; returns how many.</summary>
        public static int Neighbours8(int c, int[] into)
        {
            int i = SiteMap.I(c), j = SiteMap.J(c), n = 0;
            for (int k = 0; k < 8; k++)
            {
                int a = i + DI[k], b = j + DJ[k];
                if (a < 0 || b < 0 || a >= NX || b >= NZ) continue;
                into[n++] = SiteMap.Index(a, b);
            }
            return n;
        }
    }
}
