using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>
    /// Paths that come from walking (research 08 §3.4): seasonal flow fields from each destination group, trips from
    /// every home add wear, worn cells become trail, then path, then road, and every new lot gets a connection path at
    /// once (Emilien et al.), so nothing is ever cut off. Cheaper cost on worn ground makes routes merge (Helbing).
    /// Ported from Growth Lab (Dial's bucket queue, early-stopping flow fields, the walked direction per cell).
    /// </summary>
    public sealed class PathNet
    {
        public const byte Trail = 1, PathTier = 2, Road = 3;
        public const int Inf = int.MaxValue / 4;

        // Tunables (growth-lab.md has the values that worked).
        public static float WearPerTrip = 1f, Decay = 0.85f;
        public static float TTrail = 2.5f, TPath = 8f, TRoad = 20f;
        public static float WornDiscount = 0.55f, NetworkDiscount = 0.5f, SlopeCost = 6f;
        /// <summary>Wear and walked direction below this are dropped to 0 (keeps the save small; never visible).</summary>
        public const float Floor = 0.01f;

        readonly SiteMap m;
        readonly int[] dist = new int[SiteMap.N];
        readonly int[] cost = new int[SiteMap.N]; // 0 = impassable
        const int Buckets = 1024, MaxStep = Buckets - 1;
        readonly int[] bucketHead = new int[Buckets];
        readonly int[] nodeNext = new int[SiteMap.N * 9 + 16], nodeCell = new int[SiteMap.N * 9 + 16], nodeKey = new int[SiteMap.N * 9 + 16];
        readonly bool[] isTarget = new bool[SiteMap.N];
        readonly Queue<int> queue = new Queue<int>();
        static readonly int[] DI = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DJ = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly int[] OFF = { 1, -1, SiteMap.NX, -SiteMap.NX, 1 + SiteMap.NX, 1 - SiteMap.NX, -1 + SiteMap.NX, -1 - SiteMap.NX };
        static readonly bool[] Interior = BuildInterior();

        static bool[] BuildInterior()
        {
            var a = new bool[SiteMap.N];
            for (int c = 0; c < SiteMap.N; c++)
            {
                int i = SiteMap.I(c), j = SiteMap.J(c);
                a[c] = i > 0 && j > 0 && i < SiteMap.NX - 1 && j < SiteMap.NZ - 1;
            }
            return a;
        }

        public PathNet(SiteMap map) { m = map; }

        public bool Passable(int c) => m.lot[c] == 0 && !m.pond[c];

        /// <summary>Cost to step into cell c (×10, orthogonal). The planner walks with the same cost.</summary>
        public int StepCost(int c)
        {
            float k = 1f + SlopeCost * m.slope[c];
            if (m.Canal(c) && !m.Laid(c)) k *= 5f;       // fording a canal; laid crossings are bridges
            if (m.Green(c)) k *= 0.6f;
            float wn = Math.Min(1f, m.wear[c] / TPath);
            k *= 1f - WornDiscount * wn;
            if (m.tier[c] >= PathTier) k *= NetworkDiscount; // walked paths draw walkers; a laid but unwalked trail doesn't
            int i = SiteMap.I(c), j = SiteMap.J(c);
            if (i < 2 || j < 2 || i >= SiteMap.NX - 2 || j >= SiteMap.NZ - 2) k *= 1.5f; // people don't hug the glass
            return Math.Min(MaxStep * 10 / 14, Math.Max(1, (int)(k * 10f)));
        }

        /// <summary>Cache every cell's step cost (once per wear pass / placement, not once per edge).</summary>
        public void PrepareCosts()
        {
            for (int c = 0; c < SiteMap.N; c++) cost[c] = Passable(c) ? StepCost(c) : 0;
        }

        /// <summary>Multi-source Dijkstra; the result is valid until the next call. With <paramref name="targets"/>, it
        /// stops once all of them are settled (every cell nearer than the last target is final).</summary>
        public int[] Flow(List<int> sources, bool prepare = true, List<int> targets = null)
        {
            if (prepare) PrepareCosts();
            for (int c = 0; c < SiteMap.N; c++) dist[c] = Inf;
            for (int b = 0; b < Buckets; b++) bucketHead[b] = -1;
            int nodes = 0, live = 0, left = 0;
            if (targets != null)
            {
                Array.Clear(isTarget, 0, SiteMap.N);
                foreach (int t in targets) if (t >= 0 && !isTarget[t]) { isTarget[t] = true; left++; }
            }
            foreach (int s in sources)
            {
                if (s < 0 || dist[s] == 0) continue;
                dist[s] = 0;
                Push(ref nodes, ref live, s, 0);
            }
            for (int cur = 0; live > 0; cur++)
            {
                int b = cur & (Buckets - 1);
                while (bucketHead[b] >= 0)
                {
                    int node = bucketHead[b];
                    bucketHead[b] = nodeNext[node];
                    live--;
                    int c = nodeCell[node], key = nodeKey[node];
                    if (key > dist[c]) continue;
                    if (targets != null && isTarget[c]) { isTarget[c] = false; if (--left == 0) return dist; }
                    Relax(c, key, ref nodes, ref live);
                }
            }
            return dist;
        }

        void Relax(int c, int key, ref int nodes, ref int live)
        {
            if (Interior[c])
            {
                for (int k = 0; k < 8; k++)
                {
                    int nc = c + OFF[k];
                    int step = cost[nc];
                    if (step == 0) continue;
                    if (k >= 4)
                    {
                        if (cost[c + DI[k]] == 0 || cost[c + OFF[2] * DJ[k]] == 0) continue; // no corner cutting
                        step = step * 14 / 10;
                    }
                    int nd = key + step;
                    if (nd < dist[nc]) { dist[nc] = nd; Push(ref nodes, ref live, nc, nd); }
                }
                return;
            }
            int i = SiteMap.I(c), j = SiteMap.J(c);
            for (int k = 0; k < 8; k++)
            {
                int ni = i + DI[k], nj = j + DJ[k];
                if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                int nc = SiteMap.Index(ni, nj);
                int step = cost[nc];
                if (step == 0) continue;
                if (k >= 4)
                {
                    if (cost[SiteMap.Index(i + DI[k], j)] == 0 || cost[SiteMap.Index(i, j + DJ[k])] == 0) continue;
                    step = step * 14 / 10;
                }
                int nd = key + step;
                if (nd < dist[nc]) { dist[nc] = nd; Push(ref nodes, ref live, nc, nd); }
            }
        }

        /// <summary>Walk down a flow field from <paramref name="from"/> to its nearest source. False if unreachable.</summary>
        public bool Trace(int[] field, int from, List<int> route)
        {
            route.Clear();
            if (from < 0 || field[from] >= Inf) return false;
            int c = from, guard = 0;
            route.Add(c);
            while (field[c] > 0 && guard++ < SiteMap.N)
            {
                int i = SiteMap.I(c), j = SiteMap.J(c), best = -1, bestD = field[c];
                for (int k = 0; k < 8; k++)
                {
                    int ni = i + DI[k], nj = j + DJ[k];
                    if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                    int nc = SiteMap.Index(ni, nj);
                    if (field[nc] < bestD) { bestD = field[nc]; best = nc; }
                }
                if (best < 0) return false;
                c = best;
                route.Add(c);
            }
            return true;
        }

        /// <summary>Cells joined to the green through worn or laid ground (what new lots may connect to).</summary>
        public void Network(List<int> into, bool[] mark) => Network(into, mark, false);

        /// <summary>With <paramref name="laidOnly"/>: the green and the laid connection paths only. Laid cells never go
        /// (no lot may cover one, they never wear away), so a door connected to them stays connected for good; a worn
        /// trail can fade, which once left a door cut off.</summary>
        public void Network(List<int> into, bool[] mark, bool laidOnly)
        {
            into.Clear();
            Array.Clear(mark, 0, mark.Length);
            queue.Clear();
            for (int c = 0; c < SiteMap.N; c++)
                if (m.Green(c) && m.lot[c] == 0) { mark[c] = true; queue.Enqueue(c); }
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                into.Add(c);
                int i = SiteMap.I(c), j = SiteMap.J(c);
                for (int k = 0; k < 8; k++)
                {
                    int ni = i + DI[k], nj = j + DJ[k];
                    if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                    int nc = SiteMap.Index(ni, nj);
                    if (mark[nc] || m.lot[nc] != 0) continue;
                    if (laidOnly ? !m.Laid(nc) && !m.Green(nc) : m.tier[nc] < Trail && !m.Green(nc)) continue;
                    mark[nc] = true;
                    queue.Enqueue(nc);
                }
            }
            into.Sort();
        }

        /// <summary>Lay a path from <paramref name="door"/> to the network (shortest, reusing what exists). False if cut off.</summary>
        public bool Connect(int door, List<int> network, List<int> route, long month)
        {
            var field = Flow(network);
            if (!Trace(field, door, route)) return false;
            foreach (int c in route)
            {
                m.Set(c, SiteMap.FLaid, true);
                if (m.tier[c] < Trail) { m.tier[c] = Trail; if (m.st.pathMonth[c] == 0) m.st.pathMonth[c] = (int)month + 1; }
                m.wear[c] = Math.Max(m.wear[c], TTrail);
            }
            AddDirection(route, 1f);
            return true;
        }

        /// <summary>Add wear along one trip (weight = how many people, how often).</summary>
        public void Wear(List<int> route, float weight)
        {
            float add = WearPerTrip * weight;
            foreach (int c in route) m.wear[c] += add;
            AddDirection(route, weight);
        }

        /// <summary>Accumulate the route's local direction (over ±2 cells) as a doubled angle, so A-to-B and B-to-A agree.</summary>
        void AddDirection(List<int> route, float weight)
        {
            int n = route.Count;
            if (n < 2) return;
            for (int k = 0; k < n; k++)
            {
                int a = route[Math.Max(0, k - 2)], b = route[Math.Min(n - 1, k + 2)];
                float dx = SiteMap.I(b) - SiteMap.I(a), dz = SiteMap.J(b) - SiteMap.J(a);
                float l2 = dx * dx + dz * dz;
                if (l2 < 0.5f) continue;
                int c = route[k];
                m.dirC[c] += weight * (dx * dx - dz * dz) / l2;
                m.dirS[c] += weight * (2f * dx * dz) / l2;
            }
        }

        /// <summary>Decay wear and update tiers, with hysteresis so paths don't flicker; laid cells stay at least trails.</summary>
        public void UpdateTiers(bool roadsAllowed, long month)
        {
            var pm = m.st.pathMonth;
            for (int c = 0; c < SiteMap.N; c++)
            {
                float w = m.wear[c] * Decay;
                if (w < Floor) w = 0f;
                m.wear[c] = w;
                float dc = m.dirC[c] * Decay, ds = m.dirS[c] * Decay;
                m.dirC[c] = dc > -Floor && dc < Floor ? 0f : dc;
                m.dirS[c] = ds > -Floor && ds < Floor ? 0f : ds;
                byte t = w >= TRoad && roadsAllowed ? Road : w >= TPath ? PathTier : w >= TTrail ? Trail : (byte)0;
                byte cur = m.tier[c];
                if (t < cur)
                {
                    float keep = cur == Road ? TRoad : cur == PathTier ? TPath : TTrail;
                    if (w >= keep * 0.6f && (cur != Road || roadsAllowed)) t = cur;
                }
                if (m.Laid(c) && t < Trail) t = Trail;
                if (m.lot[c] != 0 || m.pond[c]) t = 0;
                m.tier[c] = t;
                if (t == 0) pm[c] = 0;
                else if (pm[c] == 0) pm[c] = (int)month + 1;
            }
        }

        void Push(ref int nodes, ref int live, int c, int key)
        {
            int b = key & (Buckets - 1);
            nodeCell[nodes] = c; nodeKey[nodes] = key; nodeNext[nodes] = bucketHead[b];
            bucketHead[b] = nodes;
            nodes++; live++;
        }
    }
}
