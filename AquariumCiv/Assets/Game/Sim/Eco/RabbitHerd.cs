using System;
using System.Linq;
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
        /// <summary>A knock on the glass startles the rabbits this close to the knocked spot (box units, on the ground).</summary>
        public const float KnockRadius = 1.0f;
        /// <summary>#333: a startled rabbit settles this far from where it was frightened if the meadow lets it; the place stays avoided
        /// (radius, seconds of field time) so nothing steers a rabbit straight back.</summary>
        public const float AvoidRadius = 0.7f, AvoidSeconds = 180f, WantFar = 1.2f, WantApart = 0.45f;
        /// <summary>A startled rabbit's new place is at least this far from where it was, when the meadow has such a place.</summary>
        public const float MinRun = 0.8f;

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
            /// <summary>#400: where a rabbit may set foot at all (not a lot, a field or the camp's green).</summary>
            readonly bool[] open = new bool[EcoLand.N];
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
            public void Refresh(byte[] cover, bool[] yard, int seed, int warrenCell) => Refresh(cover, yard, seed, warrenCell, null);

            /// <summary>#400: as <see cref="Refresh(byte[], bool[], int, int)"/>, with the cells the rabbits keep off whatever grows there
            /// (<paramref name="blocked"/>: the lots, buildings and fields, and the camp's green round the fire). Null: none.</summary>
            public void Refresh(byte[] cover, bool[] yard, int seed, int warrenCell, bool[] blocked)
            {
                this.seed = seed;
                GreenMask(cover, green);
                for (int c = 0; c < open.Length; c++) open[c] = blocked == null || c >= blocked.Length || !blocked[c];
                for (int c = 0; c < green.Length; c++) if (!open[c]) green[c] = false;
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
                if (green[warren] && !pool.Contains(warren)) pool.Insert(0, warren);
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
                if (l.Count == 0)
                {   // #400: the green round this home went (built on): the nearest green cell, never a spot on the lot
                    int best = home; float bd = float.MaxValue;
                    foreach (int c in meadow) { float d = Dist2(c, hx, hz); if (d < bd) { bd = d; best = c; } }
                    l.Add(best);
                }
                local[home] = l;
                return l;
            }

            void Spot(int id, long leg, double t, out float x, out float z)
            {
                int home = HomeAt(id, t);
                var l = LocalOf(home);
                uint r = H((uint)seed, (uint)id, (uint)(leg * 7 + 3));
                // each green cell near home gets a hashed weight for this leg and the heaviest is the spot: when the green round the home
                // changes, the spot only moves if its own cell went or a new one outweighs it (picking by index into the list reshuffled
                // every spot, and the rabbit jumped across the meadow); and it is even over the cells, with no pull to the patch's edge
                int c = l[0]; uint bw = 0;
                foreach (int k in l) { uint w = H(r, (uint)k, 77u); if (w >= bw) { bw = w; c = k; } }
                // the jitter stays inside the cell (0.1 wide)
                x = SiteMap.CXc(c) + (F(r, 1, 5) - 0.5f) * 0.07f;
                z = SiteMap.CZc(c) + (F(r, 2, 5) - 0.5f) * 0.07f;
            }

            bool PathGreen(float ax, float az, float bx, float bz) => PathOn(green, ax, az, bx, bz);

            bool PathOn(bool[] mask, float ax, float az, float bx, float bz)
            {
                float dx = bx - ax, dz = bz - az;
                int n = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(dx * dx + dz * dz) / 0.01f)); // fine enough not to cut a cell corner (cells are 0.1)
                for (int k = 1; k <= n; k++)
                {
                    float u = k / (float)n;
                    int c = SiteMap.CellAt(ax + dx * u, az + dz * u);
                    if (c < 0 || !mask[c]) return false;
                }
                return true;
            }

            /// <summary>#400: green all the way along the straight line from (ax, az) to (bx, bz).</summary>
            public bool PathClear(float ax, float az, float bx, float bz) => PathGreen(ax, az, bx, bz);

            /// <summary>#400: a rabbit may cross here (bare earth too), not a lot, a field or the camp's green.</summary>
            public bool IsOpenAt(float x, float z) { int c = SiteMap.CellAt(x, z); return c >= 0 && open[c]; }

            int[] routeFrom; readonly Queue<int> routeQ = new Queue<int>();
            static readonly int[] DI = { 1, -1, 0, 0, 1, 1, -1, -1 }, DJ = { 0, 0, 1, -1, 1, -1, 1, -1 };

            /// <summary>#400: a way from (ax, az) to (bx, bz) round lots, fields and the camp's green: over the green when the green joins them,
            /// else over open ground (a strip of bare earth between two patches is crossed; a lot never is). A breadth-first search on the
            /// cells (diagonals only where both sides are clear, so it never cuts a corner), pulled straight wherever the ground allows.
            /// <paramref name="into"/> gets the turning points and the goal. False when nothing joins them.</summary>
            public bool Route(float ax, float az, float bx, float bz, List<(float x, float z)> into) =>
                RouteOn(green, ax, az, bx, bz, into) || RouteOn(open, ax, az, bx, bz, into);

            bool RouteOn(bool[] mask, float ax, float az, float bx, float bz, List<(float x, float z)> into)
            {
                into.Clear();
                int a = SiteMap.CellAt(ax, az), b = SiteMap.CellAt(bx, bz);
                if (a < 0 || b < 0 || !mask[b]) return false;
                if (routeFrom == null) routeFrom = new int[mask.Length];
                for (int c = 0; c < routeFrom.Length; c++) routeFrom[c] = -2;
                routeQ.Clear(); routeQ.Enqueue(a); routeFrom[a] = -1;
                bool found = a == b;
                while (routeQ.Count > 0 && !found)
                {
                    int c = routeQ.Dequeue(), ci = SiteMap.I(c), cj = SiteMap.J(c);
                    for (int k = 0; k < 8; k++)
                    {
                        int ni = ci + DI[k], nj = cj + DJ[k];
                        if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                        int n = SiteMap.Index(ni, nj);
                        if (routeFrom[n] != -2 || !mask[n]) continue;
                        if (k >= 4 && (!mask[SiteMap.Index(ni, cj)] || !mask[SiteMap.Index(ci, nj)])) continue;
                        routeFrom[n] = c;
                        if (n == b) { found = true; break; }
                        routeQ.Enqueue(n);
                    }
                }
                if (!found) return false;
                var cells = new List<int>();
                for (int c = b; c >= 0; c = routeFrom[c]) cells.Add(c);
                cells.Reverse();
                // pull it straight: from each point, the farthest cell centre still in a clear line of sight
                float px = ax, pz = az; int at = 0;
                while (true)
                {
                    if (PathOn(mask, px, pz, bx, bz)) { into.Add((bx, bz)); return true; }
                    int far = at + 1;
                    for (int k = cells.Count - 1; k > at; k--)
                        if (PathOn(mask, px, pz, SiteMap.CXc(cells[k]), SiteMap.CZc(cells[k]))) { far = k; break; }
                    if (far >= cells.Count) { into.Add((bx, bz)); return true; }
                    px = SiteMap.CXc(cells[far]); pz = SiteMap.CZc(cells[far]); at = far;
                    into.Add((px, pz));
                }
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
            public int Startle(float tx, float tz, double t, Func<int, bool> skip, List<Startled> into) =>
                Scatter(tx, tz, t, skip, into, NearRadius, TapRadius, false);

            /// <summary>The Keeper knocks on the glass at a point whose ground spot is (kx, kz) (at the pane): every rabbit within
            /// <see cref="KnockRadius"/> of it startles (the nearest at once, the others a moment later) and runs to another part of the
            /// meadow, as for <see cref="Startle"/>. No rabbit has to be at the spot. A tap on a rabbit or on the land does nothing to them
            /// (the user, 2026-11-05: only the glass).</summary>
            public int Knock(float kx, float kz, double t, Func<int, bool> skip, List<Startled> into) =>
                Scatter(kx, kz, t, skip, into, KnockRadius, KnockRadius, true);

            int Scatter(float tx, float tz, double t, Func<int, bool> skip, List<Startled> into, float reach, float hitRadius, bool nearestFirst)
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
                    if (d <= hitRadius) hit = true;
                    if (d <= reach) order.Add((d, id));
                }
                if (!hit) return 0;
                return Send(order, tx, tz, t, into, nearestFirst);
            }

            /// <summary>The Keeper knocked on the glass beside these rabbits (the view picks them by what the player sees: close to the
            /// finger on screen, not under it). Each freezes, then runs to another part of the meadow, away from (fx, fz), the land the
            /// finger points at; the nearest goes at once, the others a moment later. Skipped ones (the hunted) stay.</summary>
            public int StartleThese(IReadOnlyList<int> ids, float fx, float fz, double t, Func<int, bool> skip, List<Startled> into)
            {
                into.Clear();
                if (pool.Count == 0 || meadow.Count == 0 || ids == null) return 0;
                var order = new List<(float d, int id)>();
                foreach (int id in ids)
                {
                    if (!alive.Contains(id) || (skip != null && skip(id))) continue;
                    var p = Sample(id, t);
                    order.Add((MathF.Sqrt((p.x - fx) * (p.x - fx) + (p.z - fz) * (p.z - fz)), id));
                }
                if (order.Count == 0) return 0;
                return Send(order, fx, fz, t, into, true);
            }

            int Send(List<(float d, int id)> order, float tx, float tz, double t, List<Startled> into, bool nearestFirst)
            {
                float first = order.Count > 0 ? order.Min(o => o.d) : 0f;
                avoids.RemoveAll(a => a.until < t);
                avoids.Add(new Avoid { x = tx, z = tz, until = t + AvoidSeconds });
                order.Sort((a, b) => { int c = a.d.CompareTo(b.d); return c != 0 ? c : a.id.CompareTo(b.id); });
                foreach (var (d, id) in order)
                {
                    double go = t + (nearestFirst ? (d <= first + 0.001f ? 0.0 : 0.12 + (d - first) * 0.5) : (d <= TapRadius ? 0.0 : 0.12 + d * 0.5)) + (nearestFirst ? Scare.NoticeSeconds : Scare.FreezeSeconds); // knock: the nearest at once, the rest a moment later
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
                    // another part of the meadow first (the user: "run to a different place"), then away from the others' homes
                    float sc = Math.Min(dTap, WantFar) * 2f + Math.Min(dFrom, WantFar) * 2f + Math.Min(nn, WantApart) * 2f;
                    if (dFrom < MinRun) sc -= 3f;
                    if (Avoided(cx, cz, t)) sc -= 4f;
                    if (PathGreen(fx, fz, cx, cz)) sc += 0.5f;
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
        /// shown one. #400 (playtest 2026-11-07: "agitate on the spot going nowhere", "jump up and down when hitting buildings and
        /// fields", "moves with no animation"): a state machine on absolute positions, not an offset added to the schedule.
        /// <list type="bullet">
        /// <item>On its day: shown = the schedule.</item>
        /// <item>A person inside <see cref="AlertRadius"/>: it freezes sitting up, facing them, then picks a real spot on the green away
        /// from everyone (straight line over green) and bounds there; it sits up and looks back. With nowhere to go it stays up,
        /// watching: it never bounds on the spot.</item>
        /// <item>Away from its day: it grazes where it is until nobody is near where its day has it, then hops back in whole hops,
        /// round anything in the way (<see cref="Field.Route"/>). With no way back, or after <see cref="StrandSeconds"/>, it asks for a
        /// new home where it is (<see cref="TakeStranded"/>).</item>
        /// </list>
        /// Every move is a hop or a bound whose phase follows the ground covered: standing still is never animated as a hop.</summary>
        public sealed class Scare
        {
            public const float AlertRadius = 0.55f;
            /// <summary>A startled rabbit freezes this long, sitting up, before it bolts: the beat that tells you it noticed.</summary>
            public const float FreezeSeconds = 0.3f;
            /// <summary>A knocked rabbit's notice: the flinch, then up on its haunches staring at the knock, before it runs (longer than the
            /// freeze when people come near: the knock is a surprise).</summary>
            public const float NoticeSeconds = 0.45f;
            /// <summary>The first part of the freeze: a flinch, a little jump in place (seconds, height).</summary>
            public const float FlinchSeconds = 0.14f, FlinchHeight = 0.022f;
            /// <summary>After the run it sits up and looks back toward what scared it this long, then grazes again.</summary>
            public const float SettleSeconds = 1.1f;
            /// <summary>How far a rabbit runs from a person (it tries shorter runs when the green is narrow), and the least run worth making.</summary>
            public const float FleeRun = 0.65f, FleeRunMin = 0.3f;
            /// <summary>It goes back to its day only when nobody is within this of where its day has it (more than the alert: no ping-pong).</summary>
            public const float ClearRadius = 0.8f;
            /// <summary>Grazing where it ran to at least this long before it hops back.</summary>
            public const float WaitSeconds = 1.5f;
            /// <summary>Kept away from its day this long (people stay there): it makes its home where it is instead.</summary>
            public const float StrandSeconds = 20f;
            /// <summary>The hop back: one hop of up to <see cref="HopLen"/> in this many seconds (a little brisker than a grazing hop, so it
            /// catches up with its day).</summary>
            public const float BackHopTime = 0.22f;
            /// <summary>The run: one bound covers about <see cref="RunBound"/>; the speed builds from <see cref="RunStart"/> to <see cref="RunSpeed"/>
            /// over the first bounds and eases to <see cref="RunEnd"/> over the last <see cref="SlowDown"/> (box units, per second). About four
            /// bounds a second at full speed: a rabbit's gait, not a blur.</summary>
            public const float RunSpeed = 1.15f, RunStart = 0.55f, RunEnd = 0.45f, RunBound = 0.27f, SlowDown = 0.4f, RunRamp = 0.35f;
            /// <summary>A slight zigzag across the line of the run (box units, and the distance of one swing).</summary>
            public const float ZigZag = 0.06f, ZigLen = 0.55f; // #362: 0.06 (was 0.035), so a bolting rabbit zigzags visibly
            /// <summary>A scheduled pose that moves farther than this in one step jumped (a new home, or the green round it changed and the
            /// spot with it): the doll keeps its place and hops over instead (a real hop is 0.12).</summary>
            public const float JumpGuard = 0.15f; // verify run 2026-11-07: 0.3 let a 0.2-0.29 snap of the schedule through; a real hop is 0.12 and per frame far less

            /// <summary>Where it is shown minus where its day has it (0 on its day): for tests and the debug line.</summary>
            public float offX, offZ;
            /// <summary>Scared by a person (frozen, or running from them).</summary>
            bool flee;
            /// <summary>#333: frozen after a knock, waiting to bolt; and bolting (from a knock or a person).</summary>
            bool holding, running;
            /// <summary>Off its day, at (curX, curZ).</summary>
            bool away, returning;
            float curX, curZ, lastYaw, freeze, retry, wait, awayFor, settleLeft, settleYaw;
            float holdFromX, holdFromZ, holdYaw, holdLeft, holdTime, faceX, faceZ;
            float runTime, runDone, runTotal, boundLen, toX, toZ;
            float hopT = -1f, hopFromX, hopFromZ, hopToX, hopToZ;
            double holdUntil;
            float targetX = float.NaN, targetZ = float.NaN; // where the knock's run ends (the refuge), if given
            float lastBaseX, lastBaseZ; bool hasLastBase; double lastNow = double.NaN;
            readonly List<(float x, float z)> route = new List<(float x, float z)>();
            float routeGoalX, routeGoalZ;
            bool stranded, routed;

            public bool Fleeing => flee || holding || running;
            /// <summary>Sitting up after the run, looking back (counts as calm).</summary>
            public bool Settling => settleLeft > 0f;
            /// <summary>Off its day (frozen, running, sitting after the run, waiting, or hopping back): where it stands is not its schedule.</summary>
            public bool InTransit => holding || running || settleLeft > 0f || away;
            public bool Holding => holding;
            public bool Running => running;
            /// <summary>Hopping back into its day.</summary>
            public bool Returning => returning;
            /// <summary>Debug: the scare's state in one line.</summary>
            public string Debug => $"hold {holding} run {running} flee {flee} away {away} back {returning} settle {settleLeft:0.00} wait {wait:0.00} off ({offX:0.00},{offZ:0.00})";

            /// <summary>The rabbit can't get back to its day (no green way, or people kept it off for <see cref="StrandSeconds"/>): true once,
            /// with where it stands. The owner gives it a home there (<see cref="Field.Rehome"/>); it then hops the last bit into its new day.</summary>
            public bool TakeStranded(out float x, out float z)
            {
                x = curX; z = curZ;
                if (!stranded) return false;
                stranded = false;
                return true;
            }

            /// <summary>#333: a knock startled this rabbit. It stays at (fromX, fromZ), sitting up and facing (faceX, faceZ) (the knock), until
            /// field time <paramref name="until"/> (when the field gives it its new home); then it bolts to the refuge (or where the schedule
            /// now has it).</summary>
            public void Hold(float fromX, float fromZ, float faceX_, float faceZ_, double until, float freezeSeconds, float runToX = float.NaN, float runToZ = float.NaN)
            {
                holding = true; running = false; flee = false; settleLeft = 0f; returning = false; hopT = -1f; route.Clear();
                away = true; curX = fromX; curZ = fromZ; awayFor = 0f;
                holdFromX = fromX; holdFromZ = fromZ; holdUntil = until; holdLeft = freezeSeconds + 1f; holdTime = 0f;
                faceX = faceX_; faceZ = faceZ_; targetX = runToX; targetZ = runToZ;
                holdYaw = MathF.Atan2(faceX_ - fromX, faceZ_ - fromZ);
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

            static float NearestDist(IReadOnlyList<float> px, IReadOnlyList<float> pz, float x, float z)
            {
                float best = float.MaxValue;
                for (int i = 0; i < px.Count && i < pz.Count; i++) { float dx = px[i] - x, dz = pz[i] - z; best = MathF.Min(best, dx * dx + dz * dz); }
                return MathF.Sqrt(best);
            }

            static readonly float[] FleeTurns = { 0f, 0.52f, -0.52f, 1.05f, -1.05f, 1.57f, -1.57f };
            static readonly float[] FleeLens = { FleeRun, 0.48f, FleeRunMin };

            /// <summary>A spot to run to from (x, z), away along (dx, dz) or turned up to 90 degrees (never back past the person): on green with green all the way, and
            /// clear of everyone by more than the alert. Failing that, the spot that gains the most room from the nearest person (at least
            /// 0.15). False: nowhere worth running to.</summary>
            public static bool PickFlee(Field field, float x, float z, float dx, float dz, IReadOnlyList<float> px, IReadOnlyList<float> pz, out float tx, out float tz)
            {
                tx = x; tz = z;
                float now = NearestDist(px, pz, x, z), bestGain = 0.15f; bool found = false;
                foreach (float len in FleeLens)
                    foreach (float a in FleeTurns)
                    {
                        float ca = MathF.Cos(a), sa = MathF.Sin(a);
                        float ux = dx * ca - dz * sa, uz = dx * sa + dz * ca;
                        float cx = x + ux * len, cz = z + uz * len;
                        if (!field.IsGreenAt(cx, cz) || !field.PathClear(x, z, cx, cz)) continue;
                        float room = NearestDist(px, pz, cx, cz);
                        if (room > AlertRadius + 0.05f) { tx = cx; tz = cz; return true; }
                        if (room - now > bestGain) { bestGain = room - now; tx = cx; tz = cz; found = true; }
                    }
                return found;
            }

            /// <summary>The bounds of a run from where it is to (x, z): the bound length fits the run so the last bound lands on the spot.</summary>
            void StartRun(Field field, float x, float z, float faceAtX, float faceAtZ)
            {
                holding = false; running = true; settleLeft = 0f; returning = false; hopT = -1f;
                runTime = 0f; runDone = 0f; toX = x; toZ = z; faceX = faceAtX; faceZ = faceAtZ;
                route.Clear(); routed = false;
                // a knock's refuge can be across a lot or bare earth: the run goes round it, over the green
                if (field.IsOpenAt(curX, curZ) && field.IsOpenAt(x, z) && !field.PathClear(curX, curZ, x, z) && field.Route(curX, curZ, x, z, route))
                { route.RemoveAt(route.Count - 1); routed = true; } // the last point is (toX, toZ) itself
                runTotal = RouteLeft(curX, curZ);
                boundLen = runTotal / MathF.Max(1f, MathF.Round(runTotal / RunBound));
                if (boundLen < 0.05f) boundLen = RunBound;
            }

            /// <summary>The ground left to run from (x, z): through the route's turning points to (toX, toZ).</summary>
            float RouteLeft(float x, float z)
            {
                float sum = 0f;
                foreach (var w in route) { sum += MathF.Sqrt((w.x - x) * (w.x - x) + (w.z - z) * (w.z - z)); x = w.x; z = w.z; }
                return sum + MathF.Sqrt((toX - x) * (toX - x) + (toZ - z) * (toZ - z));
            }

            RabbitPose Sit(RabbitPose basePose, float x, float z, float yaw, RabbitAct act = RabbitAct.SitUp)
            {
                var p = basePose; p.x = x; p.z = z; p.y = 0f; p.phase = 0f; p.act = act; p.yaw = yaw;
                return p;
            }

            /// <summary>The knock's freeze, then the run (a knock's or a person's): from where it froze to a fixed spot, so its home's own
            /// wandering during the run doesn't add to it.</summary>
            RabbitPose Run(float dt, RabbitPose basePose, Field field, double now)
            {
                var p = basePose;
                if (holding)
                {
                    holdLeft -= dt; holdTime += dt;
                    bool go = double.IsNaN(now) ? holdLeft <= 1f : (now >= holdUntil && holdTime >= NoticeSeconds) || holdLeft <= 0f;
                    if (!go)
                    {   // it noticed: a flinch (a little jump in place), then up on its haunches, ears up, staring at the knock
                        p = Sit(basePose, holdFromX, holdFromZ, holdYaw);
                        p.y = holdTime < FlinchSeconds ? MathF.Sin(MathF.PI * holdTime / FlinchSeconds) * FlinchHeight : 0f;
                        return p;
                    }
                    curX = holdFromX; curZ = holdFromZ;
                    if (float.IsNaN(targetX)) StartRun(field, basePose.x, basePose.z, faceX, faceZ); else StartRun(field, targetX, targetZ, faceX, faceZ); // the refuge, far from the knock
                }
                while (route.Count > 0 && Near(route[0].x, route[0].z, curX, curZ, 0.02f)) route.RemoveAt(0);
                float wx = route.Count > 0 ? route[0].x : toX, wz = route.Count > 0 ? route[0].z : toZ;
                float rx = wx - curX, rz = wz - curZ, leg = MathF.Sqrt(rx * rx + rz * rz);
                float d = RouteLeft(curX, curZ);
                if (d > 0.02f && leg > 1e-5f)
                {
                    runTime += dt;
                    float v = RunSpeed;
                    if (runTime < RunRamp) v = RunStart + (RunSpeed - RunStart) * (runTime / RunRamp);
                    if (d < SlowDown) v = MathF.Min(v, RunEnd + (RunSpeed - RunEnd) * (d / SlowDown));
                    float m = MathF.Min(leg, v * dt);
                    float ux = rx / leg, uz = rz / leg; // the way it runs
                    // on a planned route the legs are clear already: it keeps to them (turning off one onto grass led it into a lot)
                    if (routed ? field.IsOpenAt(curX + ux * m, curZ + uz * m) : GreenStep(field, curX, curZ, ref ux, ref uz, m))
                    {
                        lastYaw = MathF.Atan2(ux, uz);
                        curX += ux * m; curZ += uz * m;
                        runDone += m;
                    }
                    else { toX = curX; toZ = curZ; route.Clear(); d = 0f; } // something in the way: it stops here (never bounds on the spot, never crosses it)
                }
                if (d <= 0.02f || leg <= 1e-5f)
                {   // there: sit up and look back toward what scared it; then it grazes and hops back into its day when it's safe
                    if (d > 0f) { curX = toX; curZ = toZ; }
                    route.Clear(); running = false; away = true; wait = WaitSeconds; awayFor = 0f;
                    settleLeft = SettleSeconds; settleYaw = MathF.Atan2(faceX - curX, faceZ - curZ);
                    return Sit(basePose, curX, curZ, settleYaw);
                }
                // a slight zigzag across the line, fading out as it arrives (never a jump: the offset itself stays on the line)
                float left = RouteLeft(curX, curZ);
                float fade = MathF.Min(1f, left / SlowDown) * MathF.Min(1f, runDone / 0.1f);
                float lx = MathF.Sin(lastYaw), lz = MathF.Cos(lastYaw);
                float zig = MathF.Sin(runDone / ZigLen * MathF.PI) * ZigZag * fade;
                if (!field.IsGreenAt(curX - lz * zig, curZ + lx * zig)) zig = 0f;
                p.x = curX - lz * zig; p.z = curZ + lx * zig;
                float stride = runDone / boundLen, ph = stride - MathF.Floor(stride);
                p.act = RabbitAct.Flee; p.phase = ph;
                p.yaw = lastYaw + MathF.Cos(runDone / ZigLen * MathF.PI) * 0.25f * fade;
                p.y = MathF.Sin(MathF.PI * ph) * HopArc * 1.6f;
                return p;
            }

            /// <summary>A step of <paramref name="m"/> from (x, z) along (ux, uz) that stays on green: straight, else turned 45 degrees either
            /// side (still closing in; the direction is updated), else straight on over bare earth. Never onto a lot, a field or the camp's
            /// green. False: no such step (the caller stops there; it never bounds on the spot).</summary>
            static bool GreenStep(Field field, float x, float z, ref float ux, ref float uz, float m)
            {
                if (m <= 0f || field.IsGreenAt(x + ux * m, z + uz * m)) return true;
                foreach (float a in Ahead)
                {
                    float ca = MathF.Cos(a), sa = MathF.Sin(a);
                    float tx = ux * ca - uz * sa, tz = ux * sa + uz * ca;
                    if (field.IsGreenAt(x + tx * m, z + tz * m)) { ux = tx; uz = tz; return true; }
                }
                return field.IsOpenAt(x + ux * m, z + uz * m);
            }
            static readonly float[] Ahead = { 0.785f, -0.785f };

            /// <summary>Advance by <paramref name="dt"/> seconds. The scheduled pose in, the shown one out.</summary>
            public RabbitPose Step(float dt, RabbitPose basePose, IReadOnlyList<float> px, IReadOnlyList<float> pz, Field field, double now = double.NaN)
            {
                var p = StepInner(dt, basePose, px, pz, field, now);
                offX = p.x - basePose.x; offZ = p.z - basePose.z;
                if (!away) offX = offZ = 0f;
                return p;
            }

            RabbitPose StepInner(float dt, RabbitPose basePose, IReadOnlyList<float> px, IReadOnlyList<float> pz, Field field, double now)
            {
                bool jumped = false;
                if (hasLastBase)
                {   // farther than the schedule can hop in the field time that passed (fast-forward hops are not jumps)
                    double fdt = double.IsNaN(now) || double.IsNaN(lastNow) ? 0.0 : Math.Max(0.0, now - lastNow);
                    float guard = MathF.Max(JumpGuard, (float)fdt * (HopLen / HopTime) * 1.5f + 0.05f);
                    float jx = lastBaseX - basePose.x, jz = lastBaseZ - basePose.z; jumped = jx * jx + jz * jz > guard * guard;
                }
                float wasX = lastBaseX, wasZ = lastBaseZ;
                lastBaseX = basePose.x; lastBaseZ = basePose.z; hasLastBase = true; lastNow = now;
                if (holding || running) return Run(dt, basePose, field, now);
                if (!away && jumped)
                {   // its day jumped (a new home, or the green round it changed): it hops over from where it was, it doesn't pop
                    away = true; curX = wasX; curZ = wasZ; wait = 0f; awayFor = 0f; returning = false; hopT = -1f;
                }
                if (hopT >= 0f) return Hop(dt, basePose, field); // a hop it started lands first
                float sx = away ? curX : basePose.x, sz = away ? curZ : basePose.z;
                if (FleeDirection(sx, sz, px, pz, AlertRadius, out float dx, out float dz))
                {
                    if (!away) { away = true; curX = sx; curZ = sz; awayFor = 0f; }
                    returning = false; route.Clear(); settleLeft = 0f;
                    if (!flee) { flee = true; freeze = FreezeSeconds; retry = 0f; lastYaw = MathF.Atan2(-dx, -dz); } // it turns to look at what's coming
                    wait = WaitSeconds;
                    int n = Nearest(px, pz, curX, curZ);
                    float yaw = MathF.Atan2(px[n] - curX, pz[n] - curZ);
                    if (freeze > 0f) { freeze -= dt; return Sit(basePose, curX, curZ, yaw); }
                    retry -= dt;
                    if (retry <= 0f)
                    {
                        if (PickFlee(field, curX, curZ, dx, dz, px, pz, out float tx, out float tz))
                        {
                            StartRun(field, tx, tz, px[n], pz[n]);
                            return Run(dt, basePose, field, now);
                        }
                        retry = 0.4f;
                    }
                    lastYaw = yaw;
                    return Sit(basePose, curX, curZ, yaw); // nowhere to go: up on its haunches, watching (it never bounds on the spot)
                }
                flee = false;
                if (settleLeft > 0f)
                {   // after the run: up on its haunches where it stopped, looking back where the scare came from
                    settleLeft -= dt;
                    return Sit(basePose, curX, curZ, settleYaw);
                }
                if (!away) return basePose;
                awayFor += dt;
                if (!returning)
                {
                    wait -= dt;
                    bool clear = NearestDist(px, pz, basePose.x, basePose.z) > ClearRadius;
                    if (wait <= 0f && clear) { returning = true; route.Clear(); }
                    else
                    {
                        if (awayFor > StrandSeconds) { stranded = true; awayFor = 0f; } // people keep it off its day: it makes a home here
                        return Sit(basePose, curX, curZ, lastYaw, basePose.act == RabbitAct.SitUp ? RabbitAct.SitUp : RabbitAct.Graze);
                    }
                }
                return Hop(dt, basePose, field);
            }

            /// <summary>Hopping back into its day: whole hops (like the day's own) toward the schedule, straight when the green allows,
            /// else along a route round what is in the way. Back on its day once there and the day is not mid-hop.</summary>
            RabbitPose Hop(float dt, RabbitPose basePose, Field field)
            {
                if (hopT < 0f)
                {
                    float gx = basePose.x - curX, gz = basePose.z - curZ, gd = MathF.Sqrt(gx * gx + gz * gz);
                    if (gd <= 0.03f && basePose.act != RabbitAct.Hop) { away = false; returning = false; route.Clear(); return basePose; }
                    if (gd <= 0.03f) return Sit(basePose, curX, curZ, lastYaw, RabbitAct.Graze); // its day is mid-hop: it joins on the landing
                    float tx = basePose.x, tz = basePose.z;
                    if (field.IsOpenAt(curX, curZ) && field.IsOpenAt(tx, tz) && !field.PathClear(curX, curZ, tx, tz))
                    {   // not green all the way: over the green round it if it can, else over open ground, never across a lot
                        float rgx = routeGoalX - tx, rgz = routeGoalZ - tz;
                        if (route.Count == 0 || rgx * rgx + rgz * rgz > 0.25f * 0.25f)
                        {
                            route.Clear();
                            if (!field.Route(curX, curZ, tx, tz, route))
                            {   // no way back over the green: it stays, and makes its home here
                                stranded = true; returning = false; wait = WaitSeconds;
                                return Sit(basePose, curX, curZ, lastYaw, RabbitAct.Graze);
                            }
                            routeGoalX = tx; routeGoalZ = tz;
                        }
                        while (route.Count > 0 && Near(route[0].x, route[0].z, curX, curZ, 0.02f)) route.RemoveAt(0);
                        if (route.Count > 0) { tx = route[0].x; tz = route[0].z; }
                    }
                    else route.Clear();
                    float hx = tx - curX, hz = tz - curZ, hd = MathF.Sqrt(hx * hx + hz * hz);
                    if (hd < 0.001f) return Sit(basePose, curX, curZ, lastYaw, RabbitAct.Graze);
                    float len = MathF.Min(HopLen, hd);
                    hopFromX = curX; hopFromZ = curZ; hopToX = curX + hx / hd * len; hopToZ = curZ + hz / hd * len;
                    lastYaw = MathF.Atan2(hx, hz);
                    hopT = 0f;
                }
                hopT += dt / BackHopTime;
                float ph = MathF.Min(1f, hopT), prog = ph * ph * (3f - 2f * ph);
                var p = basePose;
                p.x = hopFromX + (hopToX - hopFromX) * prog; p.z = hopFromZ + (hopToZ - hopFromZ) * prog;
                p.y = MathF.Sin(MathF.PI * ph) * HopArc; p.phase = ph; p.act = RabbitAct.Hop; p.yaw = lastYaw;
                curX = p.x; curZ = p.z;
                if (hopT >= 1f) { hopT = -1f; curX = hopToX; curZ = hopToZ; p.y = 0f; p.phase = 0f; p.act = RabbitAct.Graze; }
                return p;
            }

            static bool Near(float ax, float az, float bx, float bz, float r) => (ax - bx) * (ax - bx) + (az - bz) * (az - bz) <= r * r;
        }
    }
}
