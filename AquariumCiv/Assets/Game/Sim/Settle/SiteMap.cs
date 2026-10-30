using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>
    /// The 0.1-unit site grid (60 × 44 cells) of research 08 §3.2. Static land layers (height, slope, pond, base
    /// fertility, water distance) are derived from the terrain; the live layers are the saved arrays of
    /// <see cref="SettleState"/>, shared by reference, so placement writes straight into the save.
    /// Ported from Growth Lab's SiteMap; the spike's bool layers are packed into <see cref="SettleState.flags"/>.
    /// </summary>
    public sealed class SiteMap
    {
        public const int NX = 60, NZ = 44, N = NX * NZ;
        public const float Cell = 0.1f, X0 = -Terrain.HX, Z0 = -Terrain.HZ;
        public const float GreenR = 0.42f;

        // SettleState.flags bits
        public const byte FCanal = 1, FForest = 2, FLaid = 4, FGreen = 8, FCommons = 16, FYard = 32, FMeadow = 64, FRock = 128;

        public readonly TerrainSpec spec;
        public readonly float[] height = new float[N], slope = new float[N], baseFert = new float[N], waterDist = new float[N];
        public readonly bool[] pond = new bool[N];
        public readonly SettleState st;
        public float greenX, greenZ;

        public byte[] flags => st.flags;
        public byte[] tier => st.tier;
        public float[] wear => st.wear;
        public float[] dirC => st.dirC;
        public float[] dirS => st.dirS;
        public int[] lot => st.lot;
        public int[] ring => st.ring;

        /// <summary>Builds the static layers; with <paramref name="fresh"/>, also the starting live layers (green,
        /// starting woods) into <paramref name="state"/>'s arrays.</summary>
        public SiteMap(SettleState state, bool fresh)
        {
            st = state;
            spec = TerrainSpec.Preset(state.terrain);
            greenX = spec.centreX; greenZ = spec.centreZ;
            if (fresh)
            {
                st.flags = new byte[N]; st.tier = new byte[N]; st.wear = new float[N]; st.dirC = new float[N]; st.dirS = new float[N];
                st.lot = new int[N]; st.ring = new int[N]; st.pathMonth = new int[N];
            }
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
                    pond[c] = height[c] < Terrain.WaterY;
                    float n = Terrain.Fbm(x * 1.3f + spec.noiseOff + 7f, z * 1.3f + 3f);
                    baseFert[c] = Terrain.Clamp01(0.25f + 0.6f * n - slope[c] * 0.8f);
                    if (!fresh) continue;
                    if (Terrain.Dist(x, z, greenX, greenZ) < GreenR) Set(c, FGreen, true);
                    // Starting woods: on the hills, in patches, never on the founding flat.
                    float f = Terrain.ForestBias(spec, x, z) + (Terrain.Fbm(x * 1.8f + spec.noiseOff, z * 1.8f + 40f) - 0.5f) * 1.1f;
                    float dc = Terrain.Dist(x, z, greenX, greenZ);
                    if (!pond[c] && f > 0.62f && dc > 1.3f) Set(c, FForest, true);
                }
            RecomputeWaterDist();
        }

        public bool Has(int c, byte f) => (st.flags[c] & f) != 0;
        public void Set(int c, byte f, bool on)
        {
            if (on) st.flags[c] |= f;
            else st.flags[c] &= (byte)~f;
        }

        public bool Canal(int c) => (st.flags[c] & FCanal) != 0;
        public bool Forest(int c) => (st.flags[c] & FForest) != 0;
        public bool Laid(int c) => (st.flags[c] & FLaid) != 0;
        public bool Green(int c) => (st.flags[c] & FGreen) != 0;
        public bool CommonsAt(int c) => (st.flags[c] & FCommons) != 0;
        public bool YardAt(int c) => (st.flags[c] & FYard) != 0;
        public bool Rock(int c) => (st.flags[c] & FRock) != 0;
        public bool Water(int c) => pond[c] || (st.flags[c] & FCanal) != 0;

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

        /// <summary>Fertility now: base + near water + the Keeper's rain (+0.4 in 0.8 u, full for 3 years, gone by 5)
        /// and the Keeper's lamp (+0.3 in 0.6 u for 2 years, a sunbeam on a field).</summary>
        public float Fertility(int c, long month, List<Nudge> nudges)
        {
            float f = baseFert[c] + 0.3f * MathF.Max(0f, 1f - waterDist[c] / 0.9f);
            float x = CXc(c), z = CZc(c);
            for (int k = 0; k < nudges.Count; k++)
            {
                var r = nudges[k];
                bool rain = r.kind == SimCore.NudgeKind.Rain, lamp = r.kind == SimCore.NudgeKind.Lamp;
                if (!rain && !lamp) continue;
                long age = month - r.month;
                if (age < 0) continue;
                float fade = rain ? (age <= 36 ? 1f : 1f - Terrain.Clamp01((age - 36) / 24f)) : (age <= 24 ? 1f : 0f);
                if (fade <= 0f) continue;
                float reach = rain ? 0.8f : 0.6f, add = rain ? 0.4f : 0.3f;
                float d = Terrain.Dist(x, z, r.x, r.z);
                if (d < reach) f += add * fade * (1f - d / reach);
            }
            return Terrain.Clamp01(f);
        }

        /// <summary>Chamfer distance (in units) to the nearest pond or canal cell.</summary>
        public void RecomputeWaterDist()
        {
            const float big = 99f;
            for (int c = 0; c < N; c++) waterDist[c] = Water(c) ? 0f : big;
            const float o = Cell, dg = Cell * 1.41421356f;
            for (int j = 0; j < NZ; j++)
                for (int i = 0; i < NX; i++) Relax(i, j, -1, 0, o, -1, -1, dg, 0, -1, o, 1, -1, dg);
            for (int j = NZ - 1; j >= 0; j--)
                for (int i = NX - 1; i >= 0; i--) Relax(i, j, 1, 0, o, 1, 1, dg, 0, 1, o, -1, 1, dg);
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

        /// <summary>The cells whose centres lie inside the oriented rect (optionally grown by <paramref name="pad"/>).</summary>
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
                    cx[k] = x + lx * cs + lz * sn;
                    cz[k] = z - lx * sn + lz * cs;
                    k++;
                }
        }

        public static float RectGap(Lot a, Lot b) => RectGap(a.x, a.z, a.yaw, a.w, a.d, b.x, b.z, b.yaw, b.w, b.d);

        static readonly float[] acx = new float[4], acz = new float[4], bcx = new float[4], bcz = new float[4];

        /// <summary>Separating-axis distance between two oriented rects: &lt; 0 overlap, otherwise a lower bound of the
        /// gap. Not thread-safe (shared scratch arrays): the sim runs on one thread.</summary>
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
                for (int axis = 0; axis < 2; axis++)
                {
                    float ux = axis == 0 ? cs : sn, uz = axis == 0 ? -sn : cs;
                    float amin = float.MaxValue, amax = float.MinValue, bmin = float.MaxValue, bmax = float.MinValue;
                    for (int k = 0; k < 4; k++)
                    {
                        float pa = acx[k] * ux + acz[k] * uz, pb = bcx[k] * ux + bcz[k] * uz;
                        if (pa < amin) amin = pa;
                        if (pa > amax) amax = pa;
                        if (pb < bmin) bmin = pb;
                        if (pb > bmax) bmax = pb;
                    }
                    float gap = MathF.Max(bmin - amax, amin - bmax);
                    if (gap > best) best = gap;
                }
            }
            return best;
        }

        /// <summary>Distance from a point to an oriented rect (0 inside).</summary>
        public static float PointRectDist(float px, float pz, float x, float z, float yaw, float w, float d)
        {
            float r = yaw * (MathF.PI / 180f), cs = MathF.Cos(r), sn = MathF.Sin(r);
            float dx = px - x, dz = pz - z;
            float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
            float ox = MathF.Max(0f, MathF.Abs(lx) - w / 2f), oz = MathF.Max(0f, MathF.Abs(lz) - d / 2f);
            return MathF.Sqrt(ox * ox + oz * oz);
        }
    }
}
