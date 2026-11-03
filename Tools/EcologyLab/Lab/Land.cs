using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace EcologyLab
{
    /// <summary>
    /// The static land of one box on the game's 60 × 44 site grid: height and slope from the game's own
    /// <see cref="Terrain"/>, a base soil, and the basins from a priority flood. A basin is a closed hollow; its spill
    /// level is the people's flood line (keepers-hands §5.3: they never build below it).
    /// </summary>
    public sealed class Land
    {
        public const int NX = 60, NZ = 44, N = NX * NZ;
        public const float Cell = 0.1f, X0 = -Terrain.HX, Z0 = -Terrain.HZ;
        /// <summary>The camp's trampled yard round the cold fire (SiteMap.GreenR).</summary>
        public const float YardR = 0.42f;

        public readonly int preset;
        public readonly TerrainSpec spec;
        public readonly float[] height = new float[N], slope = new float[N], baseSoil = new float[N];
        /// <summary>Priority-flood fill level: the height water would stand at before spilling out of the hollow.</summary>
        public readonly float[] fill = new float[N];
        /// <summary>Basin id per cell (-1 = not in a closed hollow).</summary>
        public readonly int[] basin = new int[N];
        public readonly List<Basin> basins = new List<Basin>();
        public readonly bool[] yard = new bool[N];
        public readonly float campX, campZ;
        public float minHeight, maxHeight;
        /// <summary>The largest basin: the one the water table surfaces in first.</summary>
        public Basin main;

        public sealed class Basin
        {
            public int id;
            public float spill, bottom;
            public readonly List<int> cells = new List<int>();
        }

        public Land(int preset)
        {
            this.preset = preset;
            spec = TerrainSpec.Preset(preset);
            campX = spec.centreX; campZ = spec.centreZ;
            minHeight = float.MaxValue; maxHeight = float.MinValue;
            for (int j = 0; j < NZ; j++)
                for (int i = 0; i < NX; i++)
                {
                    int c = Index(i, j);
                    float x = CX(i), z = CZ(j);
                    height[c] = Terrain.Height(spec, x, z);
                    const float e = 0.05f;
                    float gx = (Terrain.Height(spec, x + e, z) - Terrain.Height(spec, x - e, z)) / (2 * e);
                    float gz = (Terrain.Height(spec, x, z + e) - Terrain.Height(spec, x, z - e)) / (2 * e);
                    slope[c] = MathF.Sqrt(gx * gx + gz * gz);
                    // SiteMap's base fertility, read as how good the dirt is under the dust.
                    float n = Terrain.Fbm(x * 1.3f + spec.noiseOff + 7f, z * 1.3f + 3f);
                    baseSoil[c] = Terrain.Clamp01(0.25f + 0.6f * n - slope[c] * 0.8f);
                    yard[c] = Terrain.Dist(x, z, campX, campZ) < YardR;
                    minHeight = MathF.Min(minHeight, height[c]);
                    maxHeight = MathF.Max(maxHeight, height[c]);
                }
            PriorityFlood();
        }

        /// <summary>Barnes et al. priority flood from the box's edge (the glass is where a hollow would overflow along
        /// the ground; conservative for the flood line). Then basins = connected cells whose fill is above the ground.</summary>
        void PriorityFlood()
        {
            const float eps = 0.002f;
            var done = new bool[N];
            var pq = new PriorityQueue<int, float>();
            for (int c = 0; c < N; c++)
            {
                int i = I(c), j = J(c);
                if (i == 0 || j == 0 || i == NX - 1 || j == NZ - 1) { fill[c] = height[c]; done[c] = true; pq.Enqueue(c, height[c]); }
            }
            while (pq.Count > 0)
            {
                int c = pq.Dequeue();
                ForNeighbours4(c, nb =>
                {
                    if (done[nb]) return;
                    done[nb] = true;
                    fill[nb] = MathF.Max(height[nb], fill[c]);
                    pq.Enqueue(nb, fill[nb]);
                });
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
                    ForNeighbours4(k, nb =>
                    {
                        if (basin[nb] >= 0 || fill[nb] - height[nb] < eps) return;
                        basin[nb] = b.id; stack.Push(nb);
                    });
                }
                basins.Add(b);
            }
            foreach (var b in basins) if (main == null || b.cells.Count > main.cells.Count) main = b;
        }

        public bool InBasin(int c) => basin[c] >= 0;

        public static int Index(int i, int j) => j * NX + i;
        public static int I(int c) => c % NX;
        public static int J(int c) => c / NX;
        public static float CX(int i) => X0 + (i + 0.5f) * Cell;
        public static float CZ(int j) => Z0 + (j + 0.5f) * Cell;
        public static float CXc(int c) => CX(c % NX);
        public static float CZc(int c) => CZ(c / NX);

        public static int CellAt(float x, float z)
        {
            int i = (int)MathF.Floor((x - X0) / Cell), j = (int)MathF.Floor((z - Z0) / Cell);
            if (i < 0 || j < 0 || i >= NX || j >= NZ) return -1;
            return Index(i, j);
        }

        public static void ForNeighbours4(int c, Action<int> f)
        {
            int i = I(c), j = J(c);
            if (i > 0) f(c - 1);
            if (i < NX - 1) f(c + 1);
            if (j > 0) f(c - NX);
            if (j < NZ - 1) f(c + NX);
        }

        static readonly int[] DI = { -1, 0, 1, -1, 1, -1, 0, 1 }, DJ = { -1, -1, -1, 0, 0, 1, 1, 1 };

        /// <summary>The 8 neighbours into <paramref name="into"/>; returns how many.</summary>
        public static int Neighbours8(int c, int[] into)
        {
            int i = I(c), j = J(c), n = 0;
            for (int k = 0; k < 8; k++)
            {
                int a = i + DI[k], b = j + DJ[k];
                if (a < 0 || b < 0 || a >= NX || b >= NZ) continue;
                into[n++] = Index(a, b);
            }
            return n;
        }
    }
}
