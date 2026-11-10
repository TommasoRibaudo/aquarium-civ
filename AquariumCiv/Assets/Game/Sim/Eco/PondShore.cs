using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #495: "where is the water now, and where will it be" for everyone who stands by the eco pond. Pure and read-only (never
    /// writes the save, never rolls dice). The water meant here is the drawn water: the eco cover cells that are
    /// <see cref="EcoCover.Water"/> (<see cref="SimCore.EcoCoverAt"/>), nothing from the settle map's fixed pond.
    ///
    /// API (all x/z in box units, site cells <see cref="SiteMap.Cell"/> = 0.1 u):
    /// <list type="bullet">
    /// <item><see cref="IsWet"/>(sim, x, z): is the point over open water now.</item>
    /// <item><see cref="NearestShore"/>(sim, fromX, fromZ, slot, out x, out z, out faceX, out faceZ): the dry point at the water's edge
    /// nearest (fromX, fromZ), <see cref="StandBack"/> back from the water, and the point one step into the water to face. Re-ask
    /// each day: it follows the pond as it grows. False while there is no open water. <paramref name="slot"/> 0 is the edge's spot,
    /// slots 1, 2, 3... stand beside it (alternating sides) while the shore allows. Use this for fishers and anyone drawing water.</item>
    /// <item><see cref="FutureWater"/>(land, margin): a mask over site cells (<see cref="EcoLand.N"/>) of every cell the pond can ever
    /// cover (the main basin below the flood line, which is the table's cap; EcoModel.Water) plus every cell within
    /// <paramref name="margin"/> units of one. Independent of the pond's size today. Use this to keep a camp clear of where the
    /// pond WILL be (#539); <see cref="InFutureWater"/>(sim, x, z, margin) is the point query on the sim's land.</item>
    /// </list>
    /// </summary>
    public static class PondShore
    {
        /// <summary>How far back from the water's edge someone stands (u).</summary>
        public const float StandBack = 0.06f;
        /// <summary>Spacing of fishers along the shore (u).</summary>
        public const float SlotSpacing = 0.14f;

        public static bool IsWet(SimCore sim, float x, float z)
        {
            int c = SiteMap.CellAt(x, z);
            return c >= 0 && sim.EcoCoverAt(c) == EcoCover.Water;
        }

        static readonly int[] DI = { 1, -1, 0, 0 }, DJ = { 0, 0, 1, -1 };

        static bool Cell(int i, int j, out int c)
        {
            c = -1;
            if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) return false;
            c = j * SiteMap.NX + i;
            return true;
        }

        /// <summary>The dry shore point nearest (fromX, fromZ); false when the pond has no open water (or the ecology is off).</summary>
        public static bool NearestShore(SimCore sim, float fromX, float fromZ, int slot, out float x, out float z, out float faceX, out float faceZ)
        {
            x = z = faceX = faceZ = 0f;
            if (sim == null || !sim.EcologyOn || sim.EcoPondCells <= 0) return false;
            int best = -1, bestDry = -1; float bd = float.MaxValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (sim.EcoCoverAt(c) != EcoCover.Water) continue;
                int ci = c % SiteMap.NX, cj = c / SiteMap.NX, dry = -1; float dd = float.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    if (!Cell(ci + DI[k], cj + DJ[k], out int o) || sim.EcoCoverAt(o) == EcoCover.Water) continue;
                    // a bank cell: nearest to the asker among this water cell's dry sides
                    float d2 = Terrain.Dist(SiteMap.CXc(o), SiteMap.CZc(o), fromX, fromZ);
                    if (d2 < dd) { dd = d2; dry = o; }
                }
                if (dry < 0) continue;
                if (dd < bd) { bd = dd; best = c; bestDry = dry; }
            }
            if (best < 0) return false;
            float wx = SiteMap.CXc(best), wz = SiteMap.CZc(best), bx = SiteMap.CXc(bestDry), bz = SiteMap.CZc(bestDry);
            float ox = bx - wx, oz = bz - wz, len = (float)Math.Sqrt(ox * ox + oz * oz);
            ox /= len; oz /= len; // the unit step from the water out to the bank (an axis)
            float ex = wx + ox * SiteMap.Cell * 0.5f, ez = wz + oz * SiteMap.Cell * 0.5f; // the water's edge
            float sx = ex + ox * StandBack, sz = ez + oz * StandBack;
            if (slot > 0)
            {
                float side = ((slot + 1) / 2) * (slot % 2 == 1 ? 1f : -1f) * SlotSpacing;
                float tx = sx - oz * side, tz = sz + ox * side;
                // only while the neighbour is still on the shore: dry here, water one step in
                if (!IsWet(sim, tx, tz) && IsWet(sim, tx - ox * (StandBack + 0.04f), tz - oz * (StandBack + 0.04f))) { sx = tx; sz = tz; ex = tx - ox * StandBack; ez = tz - oz * StandBack; }
            }
            x = sx; z = sz;
            faceX = ex - ox * 0.04f; faceZ = ez - oz * 0.04f; // a little way into the water
            return true;
        }

        /// <summary>The cells the pond can ever cover, plus everything within <paramref name="margin"/> u of them (true = keep clear).</summary>
        public static bool[] FutureWater(EcoLand land, float margin = 0f)
        {
            var wet = new bool[EcoLand.N];
            var mask = new bool[EcoLand.N];
            if (land == null || land.main == null) return mask;
            float cap = land.main.spill - 0.005f; // EcoModel.Water: the table is capped just under the flood line
            for (int c = 0; c < EcoLand.N; c++)
                wet[c] = land.InBasin(c) && land.height[c] < cap && land.height[c] < land.fill[c];
            int r = Math.Max(0, (int)Math.Ceiling(margin / SiteMap.Cell));
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (!wet[c]) continue;
                int ci = c % SiteMap.NX, cj = c / SiteMap.NX;
                for (int dj = -r; dj <= r; dj++)
                    for (int di = -r; di <= r; di++)
                        if (Cell(ci + di, cj + dj, out int o) && (di * di + dj * dj) * SiteMap.Cell * SiteMap.Cell <= (margin + SiteMap.Cell * 0.5f) * (margin + SiteMap.Cell * 0.5f) + 1e-6f) mask[o] = true;
                mask[c] = true;
            }
            return mask;
        }

        /// <summary>Is the point inside (or within <paramref name="margin"/> u of) the pond's full future extent? False with the ecology off.</summary>
        public static bool InFutureWater(SimCore sim, float x, float z, float margin = 0f)
        {
            if (sim == null || !sim.EcologyOn) return false;
            int c = SiteMap.CellAt(x, z);
            if (c < 0) return false;
            var mask = FutureWater(sim.EcoGround, margin);
            return mask[c];
        }
    }
}
