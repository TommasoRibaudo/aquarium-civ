using System;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #275: where a ritual's props stand (the carried Keeper Doll, its post) relative to the buildings' footprints. The doll
    /// is carried in the arms, so the person and the doll need room round them: the post must not sit inside a lot's box or
    /// under its roof overhang. Pure: lots in, numbers out.
    /// </summary>
    public static class RitualClearance
    {
        /// <summary>Room between a prop's post and any building's footprint (body + doll in the arms + the roof's overhang).</summary>
        public const float Margin = 0.3f;

        /// <summary>Signed distance from a point to a lot's oriented box (negative inside), on the ground plane.</summary>
        public static float Gap(Lot l, float x, float z)
        {
            float r = l.yaw * (MathF.PI / 180f);
            float s = MathF.Sin(r), c = MathF.Cos(r);
            float dx = x - l.x, dz = z - l.z;
            float lx = dx * c - dz * s;   // local x axis = (cos, -sin)
            float lz = dx * s + dz * c;   // local z axis = (sin, cos)
            float ox = MathF.Abs(lx) - l.w * 0.5f, oz = MathF.Abs(lz) - l.d * 0.5f;
            float outside = MathF.Sqrt(MathF.Max(ox, 0f) * MathF.Max(ox, 0f) + MathF.Max(oz, 0f) * MathF.Max(oz, 0f));
            return outside > 0f ? outside : MathF.Max(ox, oz);
        }

        /// <summary>The smallest gap to any standing, solid lot (the open hearth and well don't count: no roof). A large
        /// number when there are none.</summary>
        public static float NearestGap(Settlement st, float x, float z)
        {
            float best = float.MaxValue;
            if (st == null) return best;
            foreach (var l in st.Lots)
            {
                if (l.retired || l.open) continue;
                float g = Gap(l, x, z);
                if (g < best) best = g;
            }
            return best;
        }

        public static bool IsClear(Settlement st, float x, float z, float margin = Margin) => NearestGap(st, x, z) >= margin;

        /// <summary>The post nearest to (<paramref name="x"/>, <paramref name="z"/>) that keeps <see cref="Margin"/> from every
        /// building and that <paramref name="ok"/> accepts (walkable ground): the point itself when it is clear, else the
        /// nearest on rings round it. False when none is found within a metre or so.</summary>
        public static bool FindPost(Settlement st, float x, float z, Func<float, float, bool> ok, out float px, out float pz)
        {
            px = x; pz = z;
            if (IsClear(st, x, z) && ok(x, z)) return true;
            for (float rad = 0.1f; rad <= 1.2f; rad += 0.1f)
            {
                int n = Math.Max(8, (int)(rad * 40f));
                float bestD = float.MaxValue; bool found = false;
                for (int i = 0; i < n; i++)
                {
                    double a = i * (2 * Math.PI / n);
                    float cx = x + (float)Math.Cos(a) * rad, cz = z + (float)Math.Sin(a) * rad;
                    if (!IsClear(st, cx, cz) || !ok(cx, cz)) continue;
                    // among the ring's clear points take the one furthest from buildings (the roomiest), tie by order
                    float d = -NearestGap(st, cx, cz);
                    if (d < bestD) { bestD = d; px = cx; pz = cz; found = true; }
                }
                if (found) return true;
            }
            return false;
        }
    }
}
