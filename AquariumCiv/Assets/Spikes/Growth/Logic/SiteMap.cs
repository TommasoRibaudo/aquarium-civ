using System;
using System.Collections.Generic;

namespace AquariumCiv.Spikes.Growth
{
    /// <summary>A claimed lot: an oriented rectangle whose front (local -z) faces its path. Lots never move.</summary>
    public sealed class Site
    {
        public int id;            // spike id (> 0)
        public int simId = -1;    // SimCore building id, or -1 for spike-side buildings (Town extension, era landmarks)
        public Kind kind;
        public float x, z, yaw, w, d;
        public long foundedMonth;
        public int nudgeId = -1;  // the Keeper's shaping this answered
        public bool retired;      // cleared at an era moment (the camp hearth)
        public int doorCell = -1;
        public int cluster = -1;  // the family cluster (Cluster variant), else -1

        /// <summary>World direction of the lot's front (local -z after yaw).</summary>
        public void Front(out float fx, out float fz)
        {
            float r = yaw * (MathF.PI / 180f);
            fx = -MathF.Sin(r); fz = -MathF.Cos(r);
        }

        public Site Clone() => (Site)MemberwiseClone();
    }

    public struct Rain { public float x, z; public long month; }

    /// <summary>
    /// The 0.1-unit site grid (60 × 44 cells) of research 08 §3.2: static terrain layers and live layers (water,
    /// fertility, forest, wear, occupancy). Every score term is a lookup here.
    /// </summary>
    public sealed class SiteMap
    {
        public const int NX = 60, NZ = 44, N = NX * NZ;
        public const float Cell = 0.1f, X0 = -GrowthTerrain.HX, Z0 = -GrowthTerrain.HZ;
        public const float GreenR = 0.42f;

        public readonly TerrainSpec spec;
        public readonly float[] height = new float[N], slope = new float[N], baseFert = new float[N];
        public readonly bool[] pond = new bool[N], canal = new bool[N], green = new bool[N], laid = new bool[N];
        public readonly float[] waterDist = new float[N], wear = new float[N];
        /// <summary>Path direction per cell as a doubled-angle vector (axis, no sign), accumulated from traced routes.</summary>
        public readonly float[] dirC = new float[N], dirS = new float[N];
        public readonly byte[] forest = new byte[N], tier = new byte[N];
        public readonly int[] lot = new int[N];
        /// <summary>Cluster variant: commons kept open (meadow, orchard) and shared family yards. Never set by the other variants.</summary>
        public readonly bool[] commons = new bool[N], yard = new bool[N];
        /// <summary>Cluster variant: the land round a family's yard, kept for that family (cluster id) until it is full.</summary>
        public readonly int[] ring = new int[N];
        public readonly List<Rain> rains = new();
        public float greenX, greenZ;

        public SiteMap(TerrainSpec spec)
        {
            this.spec = spec;
            greenX = spec.centreX; greenZ = spec.centreZ;
            for (int j = 0; j < NZ; j++)
                for (int i = 0; i < NX; i++)
                {
                    int c = Index(i, j);
                    float x = CX(i), z = CZ(j);
                    height[c] = GrowthTerrain.Height(spec, x, z);
                    const float e = 0.05f;
                    float gx = (GrowthTerrain.Height(spec, x + e, z) - GrowthTerrain.Height(spec, x - e, z)) / (2 * e);
                    float gz = (GrowthTerrain.Height(spec, x, z + e) - GrowthTerrain.Height(spec, x, z - e)) / (2 * e);
                    slope[c] = MathF.Sqrt(gx * gx + gz * gz);
                    pond[c] = height[c] < GrowthTerrain.WaterY;
                    green[c] = GrowthTerrain.Dist(x, z, greenX, greenZ) < GreenR;
                    float n = GrowthTerrain.Fbm(x * 1.3f + spec.noiseOff + 7f, z * 1.3f + 3f);
                    baseFert[c] = GrowthTerrain.Clamp01(0.25f + 0.6f * n - slope[c] * 0.8f);
                    // Starting woods: on the hills, in patches, never on the founding flat.
                    float f = GrowthTerrain.ForestBias(spec, x, z) + (GrowthTerrain.Fbm(x * 1.8f + spec.noiseOff, z * 1.8f + 40f) - 0.5f) * 1.1f;
                    float dc = GrowthTerrain.Dist(x, z, greenX, greenZ);
                    if (!pond[c] && f > 0.62f && dc > 1.3f) forest[c] = 1;
                }
            RecomputeWaterDist();
        }

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

        public bool Water(int c) => pond[c] || canal[c];

        /// <summary>Fertility now: base + near water + the Keeper's rain (+0.4 in 0.8 u, full for 3 years, gone by 5).</summary>
        public float Fertility(int c, long month)
        {
            float f = baseFert[c] + 0.3f * MathF.Max(0f, 1f - waterDist[c] / 0.9f);
            float x = CXc(c), z = CZc(c);
            foreach (var r in rains)
            {
                long age = month - r.month;
                if (age < 0) continue;
                float fade = age <= 36 ? 1f : 1f - GrowthTerrain.Clamp01((age - 36) / 24f);
                if (fade <= 0f) continue;
                float d = GrowthTerrain.Dist(x, z, r.x, r.z);
                if (d < 0.8f) f += 0.4f * fade * (1f - d / 0.8f);
            }
            return GrowthTerrain.Clamp01(f);
        }

        /// <summary>Chamfer distance (in units) to the nearest pond or canal cell.</summary>
        public void RecomputeWaterDist()
        {
            const float big = 99f;
            for (int c = 0; c < N; c++) waterDist[c] = Water(c) ? 0f : big;
            const float o = Cell, dg = Cell * 1.41421356f;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0)
                {
                    for (int j = 0; j < NZ; j++)
                        for (int i = 0; i < NX; i++) Relax(i, j, -1, 0, o, -1, -1, dg, 0, -1, o, 1, -1, dg);
                }
                else
                {
                    for (int j = NZ - 1; j >= 0; j--)
                        for (int i = NX - 1; i >= 0; i--) Relax(i, j, 1, 0, o, 1, 1, dg, 0, 1, o, -1, 1, dg);
                }
            }

            void Relax(int i, int j, int ax, int az, float ac, int bx, int bz, float bc, int cx, int cz, float cc, int dx, int dz, float dc)
            {
                int c = Index(i, j);
                float v = waterDist[c];
                v = Try(v, i + ax, j + az, ac); v = Try(v, i + bx, j + bz, bc); v = Try(v, i + cx, j + cz, cc); v = Try(v, i + dx, j + dz, dc);
                waterDist[c] = v;
            }

            float Try(float v, int i, int j, float add)
            {
                if (i < 0 || j < 0 || i >= NX || j >= NZ) return v;
                float w = waterDist[Index(i, j)] + add;
                return w < v ? w : v;
            }
        }

        /// <summary>The cells whose centres lie inside the oriented rect (optionally shrunk/grown by <paramref name="pad"/>).</summary>
        public static void RectCells(float x, float z, float yaw, float w, float d, float pad, List<int> into)
        {
            into.Clear();
            float r = yaw * (MathF.PI / 180f);
            float cs = MathF.Cos(r), sn = MathF.Sin(r);
            float hw = w / 2f + pad, hd = d / 2f + pad;
            float ext = MathF.Abs(cs) * hw + MathF.Abs(sn) * hd, extZ = MathF.Abs(sn) * hw + MathF.Abs(cs) * hd;
            int i0 = Math.Max(0, (int)MathF.Floor((x - ext - X0) / Cell)), i1 = Math.Min(NX - 1, (int)MathF.Floor((x + ext - X0) / Cell));
            int j0 = Math.Max(0, (int)MathF.Floor((z - extZ - Z0) / Cell)), j1 = Math.Min(NZ - 1, (int)MathF.Floor((z + extZ - Z0) / Cell));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    float dx = CX(i) - x, dz = CZ(j) - z;
                    // local x = right, local z = back (yaw rotates local (x, z) by r around up).
                    float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
                    if (MathF.Abs(lx) <= hw && MathF.Abs(lz) <= hd) into.Add(Index(i, j));
                }
        }

        /// <summary>The four corners of an oriented rect in world (x, z).</summary>
        public static void Corners(float x, float z, float yaw, float w, float d, float[] cx, float[] cz)
        {
            float r = yaw * (MathF.PI / 180f);
            float cs = MathF.Cos(r), sn = MathF.Sin(r);
            float hw = w / 2f, hd = d / 2f;
            int k = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float lx = sx * hw, lz = sz * hd;
                    // inverse of the RectCells transform: world = R^T local
                    cx[k] = x + lx * cs + lz * sn;
                    cz[k] = z - lx * sn + lz * cs;
                    k++;
                }
        }

        /// <summary>Separating-axis distance between two oriented rects: &lt; 0 overlap, otherwise a lower bound of the gap.</summary>
        public static float RectGap(Site a, Site b) => RectGap(a.x, a.z, a.yaw, a.w, a.d, b.x, b.z, b.yaw, b.w, b.d);

        static readonly float[] acx = new float[4], acz = new float[4], bcx = new float[4], bcz = new float[4];

        /// <summary>Not thread-safe (shared scratch arrays): the sim runs on one thread.</summary>
        public static float RectGap(float ax, float az, float ayaw, float aw, float ad, float bx, float bz, float byaw, float bw, float bd)
        {
            Corners(ax, az, ayaw, aw, ad, acx, acz);
            Corners(bx, bz, byaw, bw, bd, bcx, bcz);
            float best = float.MinValue;
            for (int which = 0; which < 2; which++)
            {
                float yaw = which == 0 ? ayaw : byaw;
                float r = yaw * (MathF.PI / 180f);
                float cs = MathF.Cos(r), sn = MathF.Sin(r);
                // the rect's two local axes in world
                for (int axis = 0; axis < 2; axis++)
                {
                    float ux = axis == 0 ? cs : sn, uz = axis == 0 ? -sn : cs;
                    float amin = float.MaxValue, amax = float.MinValue, bmin = float.MaxValue, bmax = float.MinValue;
                    for (int k = 0; k < 4; k++)
                    {
                        float pa = acx[k] * ux + acz[k] * uz, pb = bcx[k] * ux + bcz[k] * uz;
                        if (pa < amin) amin = pa; if (pa > amax) amax = pa;
                        if (pb < bmin) bmin = pb; if (pb > bmax) bmax = pb;
                    }
                    float gap = MathF.Max(bmin - amax, amin - bmax);
                    if (gap > best) best = gap;
                }
            }
            return best;
        }
    }
}
