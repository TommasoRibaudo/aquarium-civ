using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Life
{
    /// <summary>
    /// The planner's places in the world, from M2's settlement layer (#32, <see cref="SimCore.Settlement"/>; growth-lab.md
    /// "M2"): building lots and their doors, the fire and the market's centre, the land features (pond, forest, meadow,
    /// rock, the Keeper's hill and shapings), trash heaps and nudges, the walking cost and routes along the path network
    /// (the same cost the wear pass walks with, so the paths worn are the paths walked, rule 3), and the homes per family
    /// (<see cref="Settlement.HomeOf"/>: children with a parent, partners together).
    ///
    /// Read-only and dice-free: it never writes the save (the path net's flow buffers are runtime scratch). Walking costs
    /// are cached per destination as whole flow fields and dropped when the settlement changes (a month, a lot, a
    /// shaping), so a planned day costs a few lookups. With settling off (<see cref="SimConfig.settle"/> false) every
    /// query answers false and the planner falls back to its abstract places.
    /// </summary>
    public sealed class SettlePlacePositions : IPlacePositions
    {
        readonly SimCore sim;

        public SettlePlacePositions(SimCore sim) { this.sim = sim ?? throw new ArgumentNullException(nameof(sim)); }

        /// <summary>World units of plain ground per unit of the path net's cost (a step into a flat, unworn cell costs 10).</summary>
        public const float LengthPerCost = SiteMap.Cell / 10f;

        // ------------------------------------------------------------------ cache, dropped when the settlement changes
        Settlement st;
        long sig = long.MinValue;
        readonly Dictionary<int, Lot> lotOfBuilding = new Dictionary<int, Lot>();
        readonly Dictionary<Place, int> walkCell = new Dictionary<Place, int>();
        readonly Dictionary<int, int[]> fieldTo = new Dictionary<int, int[]>();
        readonly Dictionary<long, int> routeCost = new Dictionary<long, int>();
        readonly Dictionary<int, Spot> land = new Dictionary<int, Spot>();
        readonly List<int> sources = new List<int>(1), cells = new List<int>(64);
        bool landReady;

        struct Spot { public float x, z; public Spot(float x, float z) { this.x = x; this.z = z; } }

        /// <summary>Flow fields computed since the last change (tests and the cost log).</summary>
        public int FieldsBuilt { get; private set; }

        bool Ready()
        {
            var s = sim.Settlement;
            if (s == null) { st = null; return false; }
            var S = sim.State;
            long h = S.month;
            h = h * 31 + s.Lots.Count;
            h = h * 31 + S.settle.nextLotId;
            h = h * 31 + S.nudges.Count;
            h = h * 31 + S.trash.Count;
            h = h * 31 + S.buildings.Count;
            h = h * 31 + (int)S.era;
            if (h == sig && s == st) return true;
            bool newSettlement = s != st;
            sig = h; st = s;
            lotOfBuilding.Clear(); walkCell.Clear(); land.Clear();
            landReady = false; FieldsBuilt = 0;
            // Walking costs only change with the ground (a lot, a shaping, the wear pass every third month): keep the flow
            // fields while every cell's step cost is the same.
            long costs = CostSignature();
            if (newSettlement || costs != costSig) { costSig = costs; fieldTo.Clear(); routeCost.Clear(); }
            foreach (var l in s.Lots)
            {
                if (l.buildingId == 0) continue;
                if (!lotOfBuilding.TryGetValue(l.buildingId, out var old) || (old.retired && !l.retired)) lotOfBuilding[l.buildingId] = l;
            }
            return true;
        }

        long costSig = long.MinValue;

        /// <summary>A hash of every cell's walking cost (0 = can't), as the path net's flow fields see it.</summary>
        long CostSignature()
        {
            unchecked
            {
                long h = 17;
                for (int c = 0; c < SiteMap.N; c++) h = h * 1000003 + st.WalkCost(c);
                return h;
            }
        }

        // ------------------------------------------------------------------ IPlacePositions

        public bool TryGetPosition(Place place, out float x, out float z)
        {
            x = z = 0f;
            return Ready() && Position(place, false, out x, out z);
        }

        public bool TryWalkLength(Place from, Place to, out float length)
        {
            length = 0f;
            if (!Ready()) return false;
            int a = WalkCell(from), b = WalkCell(to);
            if (a < 0 || b < 0) return false;
            if (a == b) return true;
            int d = RouteCost(a, b);
            if (d < 0) return false;
            length = d * LengthPerCost;
            return true;
        }

        public bool TryRoute(Place from, Place to, List<Waypoint> into)
        {
            into.Clear();
            if (!Ready()) return false;
            int a = WalkCell(from), b = WalkCell(to);
            if (a < 0 || b < 0) return false;
            if (!st.Net.Trace(FieldTo(b), a, cells)) return false;
            foreach (int c in cells) into.Add(new Waypoint(SiteMap.CXc(c), SiteMap.CZc(c)));
            return true;
        }

        public bool TryGetHome(Notable n, out int buildingId, out int householdId)
        {
            buildingId = householdId = -1;
            if (n == null || !Ready()) return false;
            var lot = st.HomeOf(n);
            if (lot == null || lot.buildingId == 0) return false;
            buildingId = lot.buildingId;
            householdId = st.HouseholdHead(n).id;
            return true;
        }

        public void GetHomes(List<int> buildingIds)
        {
            buildingIds.Clear();
            if (!Ready()) return;
            foreach (var l in st.Homes()) if (l.buildingId != 0) buildingIds.Add(l.buildingId);
        }

        // ------------------------------------------------------------------ walking

        /// <summary>The path-net cell a walk to or from the place starts or ends on: the door (or the spot), moved to the
        /// nearest walkable cell. −1 if the place has no spot.</summary>
        public int WalkCell(Place p)
        {
            if (!Ready()) return -1;
            if (walkCell.TryGetValue(p, out int c)) return c;
            c = Position(p, true, out float x, out float z) ? Walkable(x, z) : -1;
            walkCell[p] = c;
            return c;
        }

        /// <summary>The walking cost of the route from cell a to cell b (−1 = no way): the steps into each cell after the
        /// first, diagonals ×1.4, as the path net counts them. The route is <see cref="Settlement.Route"/>'s: the same
        /// descent of the same flow field (cached per destination here, so it costs a trace, not a search).</summary>
        int RouteCost(int a, int b)
        {
            long key = (long)a * SiteMap.N + b;
            if (routeCost.TryGetValue(key, out int cost)) return cost;
            cost = -1;
            if (st.Net.Trace(FieldTo(b), a, cells))
            {
                cost = 0;
                for (int i = 1; i < cells.Count; i++)
                {
                    int c = cells[i], p = cells[i - 1];
                    int step = st.WalkCost(c);
                    if (step == 0) step = 10; // the destination's own cell when it is the spot itself
                    cost += SiteMap.I(c) != SiteMap.I(p) && SiteMap.J(c) != SiteMap.J(p) ? step * 14 / 10 : step;
                }
            }
            routeCost[key] = cost;
            return cost;
        }

        int[] FieldTo(int cell)
        {
            if (fieldTo.TryGetValue(cell, out var f)) return f;
            sources.Clear(); sources.Add(cell);
            f = (int[])st.Net.Flow(sources, true).Clone();
            fieldTo[cell] = f;
            FieldsBuilt++;
            return f;
        }

        int Walkable(float x, float z)
        {
            x = Math.Max(-Terrain.HX + 0.05f, Math.Min(Terrain.HX - 0.05f, x));
            z = Math.Max(-Terrain.HZ + 0.05f, Math.Min(Terrain.HZ - 0.05f, z));
            int c = SiteMap.CellAt(x, z);
            if (c < 0) return -1;
            if (st.WalkCost(c) > 0) return c;
            int ci = SiteMap.I(c), cj = SiteMap.J(c), best = -1, bestD = int.MaxValue;
            for (int r = 1; r <= 10 && best < 0; r++)
                for (int dj = -r; dj <= r; dj++)
                    for (int di = -r; di <= r; di++)
                    {
                        if (Math.Max(Math.Abs(di), Math.Abs(dj)) != r) continue;
                        int i = ci + di, j = cj + dj;
                        if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) continue;
                        int nc = SiteMap.Index(i, j);
                        if (st.WalkCost(nc) == 0) continue;
                        int d = di * di + dj * dj;
                        if (d < bestD || (d == bestD && nc < best)) { bestD = d; best = nc; }
                    }
            return best;
        }

        // ------------------------------------------------------------------ positions

        /// <summary>Where a place is. <paramref name="door"/>: where a walk to it ends (a lot's door) rather than where the
        /// activity happens (a field's middle).</summary>
        bool Position(Place p, bool door, out float x, out float z)
        {
            x = z = 0f;
            switch (p.kind)
            {
                case PlaceKind.None: return false;
                case PlaceKind.TrashHeap:
                    foreach (var t in sim.State.trash) if (t.id == p.index) { x = t.x; z = t.z; return true; }
                    return false;
                case PlaceKind.Nudge:
                    foreach (var n in sim.State.nudges) if (n.id == p.index) { x = n.x; z = n.z; return true; }
                    return false;
                case PlaceKind.Fire:
                    st.Fire(out x, out z, out _);
                    return true;
                case PlaceKind.Market:
                    st.MarketCentre(out x, out z);
                    return true;
            }
            if (p.IsBuilding)
            {
                if (!lotOfBuilding.TryGetValue(p.buildingId, out var lot))
                {
                    if (p.kind == PlaceKind.Green) { x = st.GreenX; z = st.GreenZ; return true; }
                    return false;
                }
                bool middle = lot.open || (!door && (lot.kind == LotKind.Farm || lot.kind == LotKind.Pasture || lot.kind == LotKind.Orchard));
                if (middle) { x = lot.x; z = lot.z; }
                else { x = lot.doorX; z = lot.doorZ; }
                return true;
            }
            if (p.kind == PlaceKind.Green) { x = st.GreenX; z = st.GreenZ; return true; }
            if (!landReady) BuildLand();
            int key = (int)p.kind * 4 + (p.kind == PlaceKind.Remembering && p.index == 1 ? 1 : 0);
            if (!land.TryGetValue(key, out var s)) return false;
            x = s.x; z = s.z;
            return true;
        }

        /// <summary>The land places, once per change: the nearest of each feature to the green (the people's land is what's
        /// close), the Keeper's shapings and the answers' lots first.</summary>
        void BuildLand()
        {
            landReady = true;
            var map = st.Map;
            float gx = st.GreenX, gz = st.GreenZ;
            const float mx = Terrain.HX - Terrain.GlassMargin, mz = Terrain.HZ - Terrain.GlassMargin;
            int edge = -1, woods = -1, bushes = -1, meadow = -1, rock = -1, road = -1, hill = -1;
            float dEdge = float.MaxValue, dWoods = float.MaxValue, dBushes = float.MaxValue, dMeadow = float.MaxValue, dRock = float.MaxValue;
            float dRoad = -1f, hHill = float.MinValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                if (Math.Abs(x) > mx || Math.Abs(z) > mz) continue;
                float d = (x - gx) * (x - gx) + (z - gz) * (z - gz);
                bool water = map.Water(c);
                if (map.Forest(c))
                {
                    if (d < dEdge) { dEdge = d; edge = c; }
                    if (d < dWoods && ForestAround(map, c) >= 8) { dWoods = d; woods = c; }
                }
                else if (!water && st.WalkCost(c) > 0 && d < dBushes && ForestAround(map, c) > 0) { dBushes = d; bushes = c; }
                if (map.Has(c, SiteMap.FMeadow) && d < dMeadow) { dMeadow = d; meadow = c; }
                if (map.Rock(c) && d < dRock) { dRock = d; rock = c; }
                if (map.tier[c] >= PathNet.Trail && d > dRoad) { dRoad = d; road = c; }
                if (!water)
                {
                    float h = st.HeightAt(x, z);
                    if (h > hHill) { hHill = h; hill = c; }
                }
            }
            if (woods < 0) woods = edge;
            if (bushes < 0) bushes = edge;

            // The pond's shore nearest the green.
            float px = map.spec.pondX, pz = map.spec.pondZ;
            float sx = px, sz = pz;
            {
                float dx = gx - px, dz = gz - pz, len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len > 1e-4f)
                    for (float t = 0f; t <= len; t += 0.05f)
                    {
                        sx = px + dx / len * t; sz = pz + dz / len * t;
                        int c = SiteMap.CellAt(sx, sz);
                        if (c >= 0 && !map.Water(c)) break;
                    }
            }
            var shore = new Spot(sx, sz);

            // The midden: out past the green, away from the pond.
            Spot midden;
            {
                float dx = gx - px, dz = gz - pz, len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-4f) { dx = 1f; dz = 0f; len = 1f; }
                midden = new Spot(Math.Max(-mx, Math.Min(mx, gx + dx / len * 1.4f)), Math.Max(-mz, Math.Min(mz, gz + dz / len * 1.4f)));
            }

            Spot? commons = null;
            if (st.State.commons.Count > 0) commons = new Spot(st.State.commons[0].x, st.State.commons[0].z);

            Spot? Cell(int c) => c < 0 ? (Spot?)null : new Spot(SiteMap.CXc(c), SiteMap.CZc(c));
            Spot? LotOfKind(LotKind k, bool door)
            {
                Lot best = null;
                foreach (var l in st.Lots) if (l.kind == k && !l.retired && (best == null || l.id < best.id)) best = l;
                return best == null ? (Spot?)null : door ? new Spot(best.doorX, best.doorZ) : new Spot(best.x, best.z);
            }
            Spot? NudgeOf(string kind, string deposit = null)
            {
                Nudge best = null; float bd = float.MaxValue;
                foreach (var n in sim.State.nudges)
                {
                    if (n.kind != kind) continue;
                    if (deposit != null && SimCore.Deposit(map.spec, n.x, n.z) != deposit) continue;
                    float d = (n.x - gx) * (n.x - gx) + (n.z - gz) * (n.z - gz);
                    if (d < bd || (d == bd && best != null && n.id < best.id)) { bd = d; best = n; }
                }
                return best == null ? (Spot?)null : new Spot(best.x, best.z);
            }
            void Set(PlaceKind k, params Spot?[] options)
            {
                foreach (var o in options)
                    if (o.HasValue) { land[(int)k * 4] = o.Value; return; }
            }

            var outcrop = LotOfKind(LotKind.Quarry, false) ?? NudgeOf(SimCore.NudgeKind.Outcrop, "stone") ?? Cell(rock) ?? Cell(hill);
            var clay = NudgeOf(SimCore.NudgeKind.Outcrop, "clay") ?? LotOfKind(LotKind.Kiln, false) ?? shore;
            Set(PlaceKind.Pond, shore);
            Set(PlaceKind.Stream, NudgeOf(SimCore.NudgeKind.Channel), shore);
            Set(PlaceKind.Woods, Cell(woods));
            Set(PlaceKind.ForestEdge, Cell(edge));
            Set(PlaceKind.Bushes, Cell(bushes));
            Set(PlaceKind.Meadow, NudgeOf(SimCore.NudgeKind.DyeMeadow), Cell(meadow), commons, Cell(bushes));
            Set(PlaceKind.Pasture, LotOfKind(LotKind.Pasture, false), NudgeOf(SimCore.NudgeKind.Herd), commons, Cell(meadow), Cell(bushes));
            Set(PlaceKind.ClayBank, clay);
            Set(PlaceKind.Outcrop, outcrop);
            Set(PlaceKind.Smithy, LotOfKind(LotKind.Smithy, true), NudgeOf(SimCore.NudgeKind.Outcrop, "ore"), outcrop);
            Set(PlaceKind.Kiln, LotOfKind(LotKind.Kiln, true), clay);
            Set(PlaceKind.Hill, Cell(hill));
            Set(PlaceKind.Midden, midden);
            Set(PlaceKind.Road, Cell(road), midden);
            Set(PlaceKind.Remembering, new Spot(gx + st.GreenRadius * 0.9f, gz)); // the cairn at the green's rim
            var church = LotOfKind(LotKind.Church, true);
            land[(int)PlaceKind.Remembering * 4 + 1] = church ?? land[(int)PlaceKind.Remembering * 4]; // the churchyard
        }

        static int ForestAround(SiteMap map, int c)
        {
            int ci = SiteMap.I(c), cj = SiteMap.J(c), n = 0;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) continue;
                    if (map.Forest(SiteMap.Index(i, j))) n++;
                }
            return n;
        }
    }
}
