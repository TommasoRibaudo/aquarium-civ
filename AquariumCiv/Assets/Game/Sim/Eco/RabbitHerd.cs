using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>What a rabbit is doing at a moment (the view picks the doll's pose from this).</summary>
    public enum RabbitAct : byte { Graze, SitUp, Hop, Flee }

    /// <summary>One rabbit at one moment: where, how high off the ground (the hop's arc), which way it faces, what it does.</summary>
    public struct RabbitPose
    {
        public float x, y, z, yaw;
        public RabbitAct act;
        /// <summary>Where in the hop (0..1) while hopping; 0 otherwise.</summary>
        public float phase;
    }

    /// <summary>
    /// #282 phase 1: the pure model of the rabbits you see. No Unity, no dice but hashes of (seed, rabbit, leg), so the same
    /// seed and time give the same pose on every run. <see cref="Field"/> holds the green mask and a warren; the view calls
    /// <see cref="Field.Sync"/> when the sim count changes and <see cref="Field.Refresh"/> now and then (a season, or when the
    /// green changes), then <see cref="Field.Sample"/> per rabbit per frame. Rabbits only ever stand on and hop across green
    /// cells (grass, tall grass). <see cref="Scare"/> is the flee rule the view applies when people come close.
    /// </summary>
    public static class RabbitHerd
    {
        public const int DefaultCap = 12;
        public const float LegBase = 7f, LegSpread = 5f, HopLen = 0.12f, HopTime = 0.28f, HopArc = 0.045f;
        public const float HomeRadius = 0.4f;

        // ---------------------------------------------------------------- hashes
        public static uint H(uint a, uint b, uint c)
        {
            uint h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA6Bu ^ (c + 0x165667B1u) * 0xC2B2AE35u;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return h;
        }
        public static float F(uint a, uint b, uint c) => (H(a, b, c) & 0xFFFFFFu) / 16777216f;

        /// <summary>How many rabbits to put on screen: the sim count (rounded), capped for the phone.</summary>
        public static int Shown(float simRabbits, int cap = DefaultCap) => Math.Max(0, Math.Min(cap, (int)MathF.Round(simRabbits)));

        public static bool IsGreen(EcoCover c) => c == EcoCover.Grass || c == EcoCover.Tall;

        /// <summary>Fills <paramref name="green"/> (length EcoLand.N) from a cover array.</summary>
        public static void GreenMask(byte[] cover, bool[] green) { for (int c = 0; c < green.Length; c++) green[c] = IsGreen((EcoCover)cover[c]); }

        // ---------------------------------------------------------------- meadow edge
        /// <summary>The green cells at the meadow's edge (a bare neighbour on one side), outside the yard, ranked by a hash of
        /// (seed, cell, salt): the first <paramref name="max"/> go into <paramref name="into"/>. With no edge (a meadow all the
        /// way across) any green cell outside the yard will do. Empty when there is no green.</summary>
        public static void EdgeCells(byte[] cover, bool[] yard, int seed, int salt, int max, List<int> into)
        {
            into.Clear();
            var edge = new List<int>(); var any = new List<int>();
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (!IsGreen((EcoCover)cover[c]) || (yard != null && yard[c])) continue;
                any.Add(c);
                int i = SiteMap.I(c), j = SiteMap.J(c);
                if (Is(cover, i - 1, j, EcoCover.Bare) || Is(cover, i + 1, j, EcoCover.Bare) || Is(cover, i, j - 1, EcoCover.Bare) || Is(cover, i, j + 1, EcoCover.Bare)) edge.Add(c);
            }
            var pool = edge.Count > 0 ? edge : any;
            pool.Sort((a, b) =>
            {
                int d = H((uint)seed, (uint)a, (uint)salt).CompareTo(H((uint)seed, (uint)b, (uint)salt));
                return d != 0 ? d : a.CompareTo(b);
            });
            for (int k = 0; k < pool.Count && k < max; k++) into.Add(pool[k]);
        }

        static bool Is(byte[] cover, int i, int j, EcoCover what) => i >= 0 && j >= 0 && i < SiteMap.NX && j < SiteMap.NZ && (EcoCover)cover[SiteMap.Index(i, j)] == what;

        /// <summary>The person closest to (x, z), or -1 (the first rabbit's sign: who points at it).</summary>
        public static int Nearest(IReadOnlyList<float> px, IReadOnlyList<float> pz, float x, float z)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < px.Count && i < pz.Count; i++)
            {
                float dx = px[i] - x, dz = pz[i] - z, d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static float Dist2(int c, float x, float z)
        {
            float dx = SiteMap.CXc(c) - x, dz = SiteMap.CZc(c) - z;
            return dx * dx + dz * dz;
        }

        // ---------------------------------------------------------------- the rabbits on screen
        public sealed class Field
        {
            readonly bool[] green = new bool[EcoLand.N];
            int seed;
            int warren = -1;
            readonly List<int> pool = new List<int>();
            readonly Dictionary<int, List<int>> local = new Dictionary<int, List<int>>();
            readonly List<int> alive = new List<int>();
            int nextId;

            public int Seed => seed;
            public int WarrenCell => warren;
            /// <summary>Ids of the rabbits shown now (stable: an id is never reused, a caught rabbit's id is gone for good).</summary>
            public IReadOnlyList<int> Alive => alive;
            public bool HasGreen => pool.Count > 0;

            /// <summary>Rebuild the green mask, the warren and the homes. Call at the start, each season, and when the meadow changes
            /// a lot; poses jump only if the green under a rabbit's home list changed.</summary>
            public void Refresh(byte[] cover, bool[] yard, int seed, int warrenCell)
            {
                this.seed = seed;
                GreenMask(cover, green);
                if (warrenCell < 0 || !green[warrenCell])
                {
                    var tmp = new List<int>();
                    EdgeCells(cover, yard, seed, 91, 1, tmp);
                    warrenCell = tmp.Count > 0 ? tmp[0] : -1;
                }
                warren = warrenCell;
                pool.Clear(); local.Clear();
                if (warren < 0) return;
                float wx = SiteMap.CXc(warren), wz = SiteMap.CZc(warren);
                var all = new List<int>();
                for (int c = 0; c < green.Length; c++) if (green[c]) all.Add(c);
                // the 48 green cells nearest the warren (always including it), so a thin meadow still has a home
                all.Sort((a, b) =>
                {
                    int d = Dist2(a, wx, wz).CompareTo(Dist2(b, wx, wz));
                    return d != 0 ? d : a.CompareTo(b);
                });
                for (int k = 0; k < all.Count && k < 48; k++) pool.Add(all[k]);
                if (!pool.Contains(warren)) pool.Insert(0, warren);
            }

            /// <summary>Bring the shown rabbits to <paramref name="count"/> (cap already applied, see <see cref="Shown"/>): extras go from the
            /// back, new ones are added with fresh ids. Returns true when anything changed.</summary>
            public bool Sync(int count)
            {
                bool changed = false;
                while (alive.Count > count) { alive.RemoveAt(alive.Count - 1); changed = true; }
                while (alive.Count < count) { alive.Add(nextId++); changed = true; }
                return changed;
            }

            /// <summary>A hunt caught this rabbit: it is gone (the sim count is lowered by SimCore.CatchRabbit).</summary>
            public bool Remove(int id) => alive.Remove(id);

            public bool IsGreenAt(float x, float z) { int c = SiteMap.CellAt(x, z); return c >= 0 && green[c]; }

            List<int> Local(int id)
            {
                if (local.TryGetValue(id, out var l)) return l;
                l = new List<int>();
                if (pool.Count > 0)
                {
                    int home = pool[(int)(H((uint)seed, (uint)id, 1) % (uint)pool.Count)];
                    float hx = SiteMap.CXc(home), hz = SiteMap.CZc(home);
                    foreach (int c in pool) if (Dist2(c, hx, hz) <= HomeRadius * HomeRadius) l.Add(c);
                    if (l.Count == 0) l.Add(home);
                }
                local[id] = l;
                return l;
            }

            void Spot(int id, long leg, out float x, out float z)
            {
                var l = Local(id);
                uint r = H((uint)seed, (uint)id, (uint)(leg * 7 + 3));
                int c = l[(int)(r % (uint)l.Count)];
                // the jitter stays inside the cell (0.1 wide)
                x = SiteMap.CXc(c) + (F(r, 1, 5) - 0.5f) * 0.07f;
                z = SiteMap.CZc(c) + (F(r, 2, 5) - 0.5f) * 0.07f;
            }

            bool PathGreen(float ax, float az, float bx, float bz)
            {
                float dx = bx - ax, dz = bz - az;
                int n = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(dx * dx + dz * dz) / 0.03f)); // finer than a cell (0.1)
                for (int k = 1; k <= n; k++)
                {
                    float u = k / (float)n;
                    if (!IsGreenAt(ax + dx * u, az + dz * u)) return false;
                }
                return true;
            }

            /// <summary>Where rabbit <paramref name="id"/> is at time <paramref name="t"/> seconds. A pure function of (seed, id, t) and the
            /// green mask: grazes, hops to a new spot, sits up, repeating in legs of 7-12 s. With no green: a pose at the origin.</summary>
            public RabbitPose Sample(int id, double t)
            {
                var p = new RabbitPose { act = RabbitAct.Graze };
                if (pool.Count == 0) return p;
                float legLen = LegBase + LegSpread * F((uint)seed, (uint)id, 11);
                double tt = t + 100.0 * F((uint)seed, (uint)id, 12);
                long leg = (long)Math.Floor(tt / legLen);
                float u = (float)(tt - leg * (double)legLen);
                Spot(id, leg, out float ax, out float az);
                Spot(id, leg + 1, out float bx, out float bz);
                uint r = H((uint)seed, (uint)id, (uint)(leg * 13 + 9));
                bool stays = F(r, 3, 4) < 0.3f || !PathGreen(ax, az, bx, bz);
                if (stays) { bx = ax; bz = az; }
                float dx = bx - ax, dz = bz - az, dist = MathF.Sqrt(dx * dx + dz * dz);
                int hops = dist < 0.01f ? 0 : Math.Max(1, (int)MathF.Ceiling(dist / HopLen));
                float hopT = hops * HopTime;
                float pause = legLen - hopT;
                float pre = pause * (0.3f + 0.4f * F(r, 5, 6));
                p.yaw = hops > 0 ? MathF.Atan2(dx, dz) : F(r, 7, 8) * MathF.PI * 2f;
                if (u < pre) { p.x = ax; p.z = az; p.act = F(r, 9, 10) < 0.35f ? RabbitAct.SitUp : RabbitAct.Graze; }
                else if (u < pre + hopT)
                {
                    float h = (u - pre) / HopTime;
                    int hi = (int)h; float ph = h - hi;
                    float prog = (hi + ph * ph * (3f - 2f * ph)) / hops;
                    p.x = ax + dx * prog; p.z = az + dz * prog;
                    p.y = MathF.Sin(MathF.PI * ph) * HopArc; p.phase = ph; p.act = RabbitAct.Hop;
                }
                else { p.x = bx; p.z = bz; p.act = F(r, 11, 12) < 0.25f ? RabbitAct.SitUp : RabbitAct.Graze; }
                return p;
            }

            /// <summary>The alive rabbit nearest to (x, z) at time t (a hunter stalking), or -1. Its position goes to <paramref name="px"/>, <paramref name="pz"/>.</summary>
            public int NearestTo(float x, float z, double t, out float px, out float pz)
            {
                int best = -1; float bd = float.MaxValue; px = pz = 0f;
                foreach (int id in alive)
                {
                    var p = Sample(id, t);
                    float dx = p.x - x, dz = p.z - z, d = dx * dx + dz * dz;
                    if (d < bd) { bd = d; best = id; px = p.x; pz = p.z; }
                }
                return best;
            }

            /// <summary>Where one rabbit is now (false when it isn't alive).</summary>
            public bool PositionOf(int id, double t, out float x, out float z)
            {
                x = z = 0f;
                if (!alive.Contains(id)) return false;
                var p = Sample(id, t); x = p.x; z = p.z;
                return true;
            }
        }

        // ---------------------------------------------------------------- scattering from people
        /// <summary>One rabbit's flee state, kept by the view next to the doll. <see cref="Step"/> turns the scheduled pose into the
        /// shown one: when a person is inside <see cref="AlertRadius"/> the rabbit freezes, then bounds directly away (along the green),
        /// and when they have gone it hops back to what its schedule says. It never ends off the green.</summary>
        public sealed class Scare
        {
            public const float AlertRadius = 0.55f, FleeSpeed = 1.6f, ReturnSpeed = 0.5f;
            /// <summary>A startled rabbit freezes this long, sitting up, before it bolts: the beat that tells you it noticed.</summary>
            public const float FreezeSeconds = 0.3f;
            /// <summary>One flee bound covers this much ground (box units).</summary>
            public const float BoundLen = 0.17f;
            public float offX, offZ;
            float calm, lastYaw, freeze, stride, back;
            public bool Fleeing { get; private set; }

            /// <summary>The direction (unit) a rabbit at (rx, rz) runs from the people: away from the closest one inside
            /// <paramref name="radius"/>. False when nobody is that close.</summary>
            public static bool FleeDirection(float rx, float rz, IReadOnlyList<float> px, IReadOnlyList<float> pz, float radius, out float dx, out float dz)
            {
                dx = dz = 0f;
                int n = Nearest(px, pz, rx, rz);
                if (n < 0) return false;
                float ex = rx - px[n], ez = rz - pz[n], d = MathF.Sqrt(ex * ex + ez * ez);
                if (d >= radius) return false;
                if (d < 0.001f) { ex = 1f; ez = 0f; d = 1f; }
                dx = ex / d; dz = ez / d;
                return true;
            }

            static readonly float[] Turns = { 0f, 0.785f, -0.785f, 1.571f, -1.571f };

            /// <summary>Advance by <paramref name="dt"/> seconds. The scheduled pose in, the shown one out (base + offset, on the green).
            /// Startled, the rabbit freezes sitting up for <see cref="FreezeSeconds"/>, then bounds away; once calm it hops back to
            /// its schedule (it never slides).</summary>
            public RabbitPose Step(float dt, RabbitPose basePose, IReadOnlyList<float> px, IReadOnlyList<float> pz, Field field)
            {
                float cx = basePose.x + offX, cz = basePose.z + offZ;
                bool bolting = false, hoppingBack = false;
                if (FleeDirection(cx, cz, px, pz, AlertRadius, out float dx, out float dz))
                {
                    if (!Fleeing) { freeze = FreezeSeconds; stride = 0f; lastYaw = MathF.Atan2(-dx, -dz); } // it turns to look at what's coming
                    calm = 0f; Fleeing = true;
                    if (freeze > 0f) freeze -= dt;
                    else
                    {
                        bolting = true;
                        float step = FleeSpeed * dt;
                        // straight away first, then 45 and 90 degrees either side: the first that stays on green
                        foreach (float a in Turns)
                        {
                            float ca = MathF.Cos(a), sa = MathF.Sin(a);
                            float tx = dx * ca - dz * sa, tz = dx * sa + dz * ca;
                            if (field.IsGreenAt(cx + tx * step, cz + tz * step)) { offX += tx * step; offZ += tz * step; lastYaw = MathF.Atan2(tx, tz); break; }
                        }
                        stride += step / BoundLen;
                    }
                }
                else
                {
                    calm += dt;
                    freeze = 0f;
                    if (Fleeing && calm < 1.5f) bolting = true; // still running out its last bounds
                    if (calm > 1.5f)
                    {
                        Fleeing = false;
                        float d = MathF.Sqrt(offX * offX + offZ * offZ);
                        if (d > 0f)
                        {
                            float m = MathF.Min(d, ReturnSpeed * dt);
                            lastYaw = MathF.Atan2(-offX, -offZ);
                            if (m >= d) { offX = offZ = 0f; } else { offX -= offX / d * m; offZ -= offZ / d * m; }
                            back += m / HopLen;
                            hoppingBack = true;
                        }
                        else back = 0f;
                    }
                }
                var p = basePose;
                p.x = basePose.x + offX; p.z = basePose.z + offZ;
                // never off the green: pull the offset in until it is back on it
                for (int k = 0; k < 4 && !field.IsGreenAt(p.x, p.z); k++) { offX *= 0.5f; offZ *= 0.5f; p.x = basePose.x + offX; p.z = basePose.z + offZ; }
                if (!field.IsGreenAt(p.x, p.z)) { offX = offZ = 0f; p.x = basePose.x; p.z = basePose.z; }
                if (Fleeing && !bolting)
                {   // frozen: up on the haunches, facing the danger
                    p.act = RabbitAct.SitUp; p.yaw = lastYaw; p.y = 0f; p.phase = 0f;
                }
                else if (Fleeing)
                {
                    if (calm > 0f && stride - MathF.Floor(stride) > 0.02f) stride = MathF.Min(MathF.Ceiling(stride), stride + dt / HopTime); // lands the bound it is in
                    float ph = stride - MathF.Floor(stride);
                    if (calm > 0f && ph <= 0.02f) { p.act = RabbitAct.SitUp; p.yaw = lastYaw; p.y = 0f; p.phase = 0f; } // landed: up, checking it's safe
                    else
                    {
                        p.act = RabbitAct.Flee; p.yaw = lastYaw; p.phase = ph;
                        p.y = MathF.Sin(MathF.PI * ph) * HopArc * 1.4f;
                    }
                }
                else if (hoppingBack)
                {
                    float ph = back - MathF.Floor(back);
                    p.act = RabbitAct.Hop; p.yaw = lastYaw; p.phase = ph;
                    p.y = MathF.Sin(MathF.PI * ph) * HopArc;
                }
                return p;
            }
        }
    }
}
