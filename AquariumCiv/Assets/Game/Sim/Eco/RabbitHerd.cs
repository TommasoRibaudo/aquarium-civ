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
        public const float HomeRadius = 0.6f;
        /// <summary>#333: how many green cells nearest the warren the rabbits' first homes are spread over (a wide meadow, not one blob).</summary>
        public const int HomePool = 200;
        /// <summary>#333: a tap within this of a rabbit startles it; the ones within <see cref="NearRadius"/> of the tap follow a moment later.</summary>
        public const float TapRadius = 0.3f, NearRadius = 0.75f;
        /// <summary>#333: a startled rabbit settles this far from where it was frightened if the meadow lets it; the place stays avoided
        /// (radius, seconds of field time) so nothing steers a rabbit straight back.</summary>
        public const float AvoidRadius = 0.7f, AvoidSeconds = 180f, WantFar = 1.2f, WantApart = 0.45f;

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
            readonly List<int> meadow = new List<int>();
            readonly List<int> baseHome = new List<int>();
            readonly Dictionary<int, List<int>> local = new Dictionary<int, List<int>>();
            readonly Dictionary<int, List<HomeEvent>> rehomes = new Dictionary<int, List<HomeEvent>>();
            readonly List<Avoid> avoids = new List<Avoid>();
            int startles;
            struct HomeEvent { public double t; public int home; }
            struct Avoid { public float x, z; public double until; }
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
                pool.Clear(); local.Clear(); meadow.Clear(); baseHome.Clear();
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
                meadow.AddRange(all);
                for (int k = 0; k < all.Count && k < HomePool; k++) pool.Add(all[k]);
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

            /// <summary>The first home of rabbit <paramref name="id"/>: of a few hashed candidates in the pool, the one farthest from the
            /// homes of the rabbits before it, so the herd spreads over the meadow instead of bunching.</summary>
            int BaseHome(int id)
            {
                while (baseHome.Count <= id)
                {
                    int k = baseHome.Count, best = pool[(int)(H((uint)seed, (uint)k, 1) % (uint)pool.Count)];
                    float bs = -1f;
                    if (k > 0)
                        for (int a = 0; a < 8; a++)
                        {
                            int c = pool[(int)(H((uint)seed, (uint)k, (uint)(1 + a * 31)) % (uint)pool.Count)];
                            float nn = float.MaxValue;
                            for (int j = 0; j < k; j++) nn = Math.Min(nn, Dist2(c, SiteMap.CXc(baseHome[j]), SiteMap.CZc(baseHome[j])));
                            if (nn > bs) { bs = nn; best = c; }
                        }
                    baseHome.Add(best);
                }
                return baseHome[id];
            }

            int HomeAt(int id, double t)
            {
                if (rehomes.TryGetValue(id, out var l))
                    for (int k = l.Count - 1; k >= 0; k--) if (l[k].t <= t) return l[k].home;
                return BaseHome(id);
            }

            List<int> LocalOf(int home)
            {
                if (local.TryGetValue(home, out var l)) return l;
                l = new List<int>();
                float hx = SiteMap.CXc(home), hz = SiteMap.CZc(home);
                foreach (int c in meadow) if (Dist2(c, hx, hz) <= HomeRadius * HomeRadius) l.Add(c);
                if (l.Count == 0) l.Add(home);
                local[home] = l;
                return l;
            }

            void Spot(int id, long leg, double t, out float x, out float z)
            {
                var l = LocalOf(HomeAt(id, t));
                uint r = H((uint)seed, (uint)id, (uint)(leg * 7 + 3));
                int c = l[(int)(r % (uint)l.Count)];
                // the jitter stays inside the cell (0.1 wide)
                x = SiteMap.CXc(c) + (F(r, 1, 5) - 0.5f) * 0.07f;
                z = SiteMap.CZc(c) + (F(r, 2, 5) - 0.5f) * 0.07f;
            }

            bool PathGreen(float ax, float az, float bx, float bz)
            {
                float dx = bx - ax, dz = bz - az;
                int n = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(dx * dx + dz * dz) / 0.01f)); // fine enough not to cut a cell corner (cells are 0.1)
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
                Spot(id, leg, t, out float ax, out float az);
                Spot(id, leg + 1, t, out float bx, out float bz);
                uint r = H((uint)seed, (uint)id, (uint)(leg * 13 + 9));
                bool stays = F(r, 3, 4) < 0.2f || !PathGreen(ax, az, bx, bz);
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

            // ------------------------------------------------------------ #333: a tap startles
            /// <summary>One rabbit a tap startled: it freezes until <see cref="until"/> (field seconds), then runs to its new place.</summary>
            public struct Startled { public int id; public double until; public int refuge; public float awayX, awayZ; }

            /// <summary>A tap at (tx, tz) at time <paramref name="t"/>: when a rabbit is within <see cref="TapRadius"/> of it, that one
            /// freezes for <see cref="Scare.FreezeSeconds"/> then bolts, and those within <see cref="NearRadius"/> of the tap do the same a
            /// moment later (further, later). Each gets a new home elsewhere in the meadow from the moment it starts running: far from the
            /// tap and from where it was, clear of the places avoided, apart from the other rabbits. The places are avoided for a while.
            /// Never removes a rabbit. <paramref name="skip"/> keeps some out (a rabbit a hunter is stalking). Randomness: hashes of
            /// (seed, rabbit, startle count) only, never the sim's stream.</summary>
            public int Startle(float tx, float tz, double t, Func<int, bool> skip, List<Startled> into)
            {
                into.Clear();
                if (pool.Count == 0 || meadow.Count == 0) return 0;
                var order = new List<(float d, int id)>();
                bool hit = false;
                foreach (int id in alive)
                {
                    if (skip != null && skip(id)) continue;
                    var p = Sample(id, t);
                    float d = MathF.Sqrt((p.x - tx) * (p.x - tx) + (p.z - tz) * (p.z - tz));
                    if (d <= TapRadius) hit = true;
                    if (d <= NearRadius) order.Add((d, id));
                }
                if (!hit) return 0;
                avoids.RemoveAll(a => a.until < t);
                avoids.Add(new Avoid { x = tx, z = tz, until = t + AvoidSeconds });
                order.Sort((a, b) => { int c = a.d.CompareTo(b.d); return c != 0 ? c : a.id.CompareTo(b.id); });
                foreach (var (d, id) in order)
                {
                    double go = t + (d <= TapRadius ? 0.0 : 0.12 + d * 0.5) + Scare.FreezeSeconds;
                    var p = Sample(id, t);
                    float ax = p.x - tx, az = p.z - tz, al = MathF.Sqrt(ax * ax + az * az);
                    if (al < 0.001f) { ax = 1f; az = 0f; al = 1f; }
                    var s = new Startled { id = id, until = go, awayX = ax / al, awayZ = az / al };
                    s.refuge = PickRefuge(id, p.x, p.z, tx, tz, t, go);
                    avoids.Add(new Avoid { x = p.x, z = p.z, until = t + AvoidSeconds });
                    SetHome(id, go, s.refuge);
                    into.Add(s);
                }
                return into.Count;
            }

            /// <summary>Puts rabbit <paramref name="id"/> at home in <paramref name="cell"/> from time <paramref name="t"/> on (debug spawns).</summary>
            public void Rehome(int id, int cell, double t) { if (cell >= 0 && cell < green.Length && green[cell]) SetHome(id, t, cell); }

            void SetHome(int id, double t, int cell)
            {
                if (!rehomes.TryGetValue(id, out var l)) rehomes[id] = l = new List<HomeEvent>();
                l.RemoveAll(r => r.t >= t);
                l.Add(new HomeEvent { t = t, home = cell });
            }

            /// <summary>The best green cell to settle in: far from the tap and from where it was, clear of avoided places, apart from the
            /// other rabbits' homes, with green all the way there.</summary>
            int PickRefuge(int id, float fx, float fz, float tx, float tz, double t, double go)
            {
                startles++;
                int best = HomeAt(id, t); float bs = float.MinValue;
                const int Tries = 40;
                for (int a = 0; a < Tries; a++)
                {
                    int c = meadow[(int)(H((uint)seed, (uint)id * 977u + (uint)startles, (uint)(a + 4000)) % (uint)meadow.Count)];
                    float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
                    float dTap = MathF.Sqrt(Dist2(c, tx, tz)), dFrom = MathF.Sqrt(Dist2(c, fx, fz));
                    float nn = 9f;
                    foreach (int o in alive)
                    {
                        if (o == id) continue;
                        int oh = HomeAt(o, go);
                        nn = Math.Min(nn, MathF.Sqrt(Dist2(oh, cx, cz)));
                    }
                    float sc = Math.Min(dTap, WantFar) * 2f + Math.Min(dFrom, WantFar) + Math.Min(nn, WantApart) * 3f;
                    if (Avoided(cx, cz, t)) sc -= 4f;
                    if (PathGreen(fx, fz, cx, cz)) sc += 1.5f;
                    if (sc > bs) { bs = sc; best = c; }
                }
                return best;
            }

            /// <summary>Is a place inside an area the startled rabbits keep away from at time t.</summary>
            public bool Avoided(float x, float z, double t)
            {
                foreach (var av in avoids) if (av.until >= t && (x - av.x) * (x - av.x) + (z - av.z) * (z - av.z) < AvoidRadius * AvoidRadius) return true;
                return false;
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
            bool flee;
            public bool Fleeing => flee || holding || running;
            /// <summary>#333: frozen after a tap, waiting to bolt; and bolting to its new place.</summary>
            public bool Holding => holding;
            public bool Running => running;
            bool holding, running;
            float holdFromX, holdFromZ, holdYaw, holdLeft;
            double holdUntil;
            /// <summary>One startle bound covers this much ground; the run is faster than a flee from people.</summary>
            public const float RunSpeed = 2.2f, RunBound = 0.2f;

            /// <summary>#333: a tap startled this rabbit. It stays at (fromX, fromZ), sitting up and facing (faceX, faceZ) (the tap), until field
            /// time <paramref name="until"/> (when the field gives it its new home); then it bolts to where the schedule now has it.</summary>
            public void Hold(float fromX, float fromZ, float faceX, float faceZ, double until, float freezeSeconds)
            {
                holding = true; running = false; flee = false;
                holdFromX = fromX; holdFromZ = fromZ; holdUntil = until; holdLeft = freezeSeconds + 1f;
                holdYaw = MathF.Atan2(faceX - fromX, faceZ - fromZ);
                offX = offZ = 0f; stride = 0f;
            }

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

            /// <summary>The tap's startle: the freeze (sat up where it was), then the run, off = where it was - where the schedule has it
            /// now, shrinking at <see cref="RunSpeed"/> in long bounds until it is there.</summary>
            RabbitPose Startle(float dt, RabbitPose basePose, Field field, double now)
            {
                var p = basePose;
                if (holding)
                {
                    holdLeft -= dt;
                    bool go = double.IsNaN(now) ? holdLeft <= 1f : now >= holdUntil || holdLeft <= 0f;
                    if (!go)
                    {
                        p.x = holdFromX; p.z = holdFromZ; p.y = 0f; p.phase = 0f; p.act = RabbitAct.SitUp; p.yaw = holdYaw;
                        return p;
                    }
                    holding = false; running = true;
                    offX = holdFromX - basePose.x; offZ = holdFromZ - basePose.z; stride = 0f;
                }
                float d = MathF.Sqrt(offX * offX + offZ * offZ);
                if (d <= 0.02f) { running = false; flee = false; calm = 0f; offX = offZ = 0f; back = 0f; stride = 0f; return p; }
                float m = MathF.Min(d, RunSpeed * dt);
                lastYaw = MathF.Atan2(-offX, -offZ);
                offX -= offX / d * m; offZ -= offZ / d * m;
                stride += m / RunBound;
                p.x = basePose.x + offX; p.z = basePose.z + offZ;
                for (int k = 0; k < 4 && !field.IsGreenAt(p.x, p.z); k++) { offX *= 0.5f; offZ *= 0.5f; p.x = basePose.x + offX; p.z = basePose.z + offZ; }
                float ph = stride - MathF.Floor(stride);
                p.act = RabbitAct.Flee; p.yaw = lastYaw; p.phase = ph; p.y = MathF.Sin(MathF.PI * ph) * HopArc * 1.8f;
                return p;
            }

            static readonly float[] Turns = { 0f, 0.785f, -0.785f, 1.571f, -1.571f };

            /// <summary>Advance by <paramref name="dt"/> seconds. The scheduled pose in, the shown one out (base + offset, on the green).
            /// Startled, the rabbit freezes sitting up for <see cref="FreezeSeconds"/>, then bounds away; once calm it hops back to
            /// its schedule (it never slides).</summary>
            public RabbitPose Step(float dt, RabbitPose basePose, IReadOnlyList<float> px, IReadOnlyList<float> pz, Field field, double now = double.NaN)
            {
                if (holding || running) return Startle(dt, basePose, field, now);
                float cx = basePose.x + offX, cz = basePose.z + offZ;
                bool bolting = false, hoppingBack = false;
                if (FleeDirection(cx, cz, px, pz, AlertRadius, out float dx, out float dz))
                {
                    if (!flee) { freeze = FreezeSeconds; stride = 0f; lastYaw = MathF.Atan2(-dx, -dz); } // it turns to look at what's coming
                    calm = 0f; flee = true;
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
                    if (flee && calm < 1.5f) bolting = true; // still running out its last bounds
                    if (calm > 1.5f)
                    {
                        flee = false;
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
                if (flee && !bolting)
                {   // frozen: up on the haunches, facing the danger
                    p.act = RabbitAct.SitUp; p.yaw = lastYaw; p.y = 0f; p.phase = 0f;
                }
                else if (flee)
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
