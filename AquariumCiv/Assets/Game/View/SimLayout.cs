using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using UnityEngine;
using STerrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.View
{
    /// <summary>
    /// The sim-to-view mapping (#33): fills a <see cref="BoxLayout"/> from a live <see cref="SimCore"/>'s settlement
    /// (Sim/Settle, #32). Pure and deterministic: the same sim state gives the same layout, so the box's chunk hashes only
    /// change where the sim changed.
    /// <list type="bullet">
    /// <item>Buildings stand on their lots, dressed by kind and era: homes are tents, huts or houses (big ones on busy
    /// paths, two storeys in the town by the square); a camp's farm is gathering grounds and its lumber mill a woodpile
    /// (decisions 2026-10-29), from the village on fenced strip fields and a mill shed; yards get barns or sheds.</item>
    /// <item>The green: the camp's trodden circle round the fire, then grass, then a cobbled square once a town has a
    /// market. Family yards, commons, dye meadows, outcrops and an unanswered herd are ground or pieces at their spot.</item>
    /// <item>Paths are the settlement's curves, split where the tier changes; dug channels become carved water strips;
    /// trees come from the sim's forest layer (one per 2 × 2 cells), so groves grow and mills cut the woods back.</item>
    /// <item>People stand where no planner is needed yet (#30 moves them): round the fire or the well, at doors, in
    /// fields, at the mill and the market, kids in the yards. As many as the population, up to <see cref="MaxPeople"/>.</item>
    /// </list>
    /// An era rebuild passes <c>eraOf</c>: each lot (and the green, as <c>null</c>) is drawn in the era it has reached in
    /// the wave, so the box rebuilds in place from the centre outward (<see cref="Settlement.RebuildOrder"/>).
    /// </summary>
    public static class SimLayout
    {
        public const int MaxPeople = 60;

        /// <summary>The view's land for a sim terrain preset (the same height function, so lots sit on the drawn ground).</summary>
        public static Land LandFor(int preset)
        {
            var t = TerrainSpec.Preset(preset);
            return new Land { mirror = t.mirror, noiseOffset = t.noiseOff, pond = new Vector2(t.pondX, t.pondZ), centre = new Vector2(t.centreX, t.centreZ) };
        }

        /// <summary>The piece kind a lot kind stands as (before era dressing). Farms, orchards and pastures are fields
        /// (with a piece on top); the hearth is the fire.</summary>
        public static PieceKind PieceFor(LotKind k) => k switch
        {
            LotKind.Camp => PieceKind.Fire,
            LotKind.Hut or LotKind.Cottage => PieceKind.Home,
            LotKind.Farm => PieceKind.Gathering,
            LotKind.Lumbermill or LotKind.TimberYard => PieceKind.Lumbermill,
            LotKind.Well => PieceKind.Well,
            LotKind.Windmill => PieceKind.Windmill,
            LotKind.Market => PieceKind.Market,
            LotKind.Tavern => PieceKind.Tavern,
            LotKind.Church => PieceKind.Church,
            LotKind.Watermill => PieceKind.Watermill,
            LotKind.Smithy => PieceKind.Smithy,
            LotKind.Dyer => PieceKind.Dyer,
            LotKind.Shop => PieceKind.Shop,
            LotKind.Workshop => PieceKind.Workshop,
            LotKind.Guildhall => PieceKind.Guildhall,
            LotKind.Outbuilding => PieceKind.Barn,
            LotKind.Pasture => PieceKind.Herd,
            LotKind.Orchard => PieceKind.Orchard,
            LotKind.Jetty => PieceKind.Jetty,
            LotKind.Quarry => PieceKind.Quarry,
            LotKind.Kiln => PieceKind.Kiln,
            _ => PieceKind.Home,
        };

        /// <summary>A stable seed for a lot's piece (its look survives rebuilds).</summary>
        public static int SeedOf(Lot l) => SeedOfLot(l.id);
        public static int SeedOfLot(int lotId) => lotId * 101 + 7;

        /// <summary>A lot's rectangle in world space, front edge first (the gate edge of a field).</summary>
        public static Vector2[] Corners(Lot l, float inset = 0f)
        {
            float r = l.yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            float hw = l.w / 2f - inset, hd = l.d / 2f - inset;
            Vector2 W(float lx, float lz) => new(l.x + lx * c + lz * s, l.z - lx * s + lz * c);
            return new[] { W(-hw, -hd), W(hw, -hd), W(hw, hd), W(-hw, hd) };
        }

        static Vector2 Local(Lot l, float lx, float lz)
        {
            float r = l.yaw * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(l.x + lx * c + lz * s, l.z - lx * s + lz * c);
        }

        static float Face(Vector2 from, Vector2 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 180f; // local -z (the front) faces the target
        }

        /// <summary>Where the sim puts the first well, from the green's centre (Settlement.TryPlace, "first on green").</summary>
        public const float FirstWellDX = 0.17f, FirstWellDZ = -0.1f;

        static bool HasOpenWell(Settlement st)
        {
            foreach (var l in st.Lots) if (l.kind == LotKind.Well && l.open && !l.retired) return true;
            return false;
        }

        static PathTier TierOf(float t) => (PathTier)(Mathf.Clamp(Mathf.RoundToInt(t), 1, 3) - 1);

        /// <summary>Fills <paramref name="L"/> from the sim. <paramref name="cobbled"/> (site cell index → laid with
        /// cobbles) cobbles those cells of any path tier (#34's hook for #90's cobbles spreading along the main path);
        /// roads are cobbled anyway.</summary>
        /// <summary>#85: <see cref="ShapingView"/> draws the Keeper's groves (saplings growing into trees) and the herds waiting
        /// for an answer, animated; while it does, the live layout leaves them out (the time-lapse keeps them).</summary>
        public static bool KeeperOverlays;

        public static void Fill(BoxLayout L, SimCore sim, Func<Lot, Era> eraOf = null, Func<int, bool> cobbled = null, ReplayFrame replay = null)
        {
            L.Clear();
            var st = sim?.Settlement;
            if (st == null) return;
            L.autoTrees = false; // the forest layer gives the trees
            var S = sim.State;
            int seed = S.seed;
            Era now = replay != null ? replay.era : S.era;
            Era EraOf(Lot l) => eraOf != null ? eraOf(l) : now;
            int year = replay != null ? replay.year : (int)(S.month / SimConfig.MonthsPerYear);
            var map = st.Map;
            var green = new Vector2(st.GreenX, st.GreenZ);
            Lot camp = null;
            foreach (var l in st.Lots) if (l.kind == LotKind.Camp) { camp = l; break; }
            Era greenEra = EraOf(null);
            bool campFire = camp != null && greenEra == Era.Camp;

            // ---- the green ----
            if (campFire)
            {
                L.Clearing(new Vector2(camp.x, camp.z), SiteMap.GreenR + 0.12f, ClearingKind.Dirt);
                L.Add(PieceKind.Fire, camp.x, camp.z, 0f, 0.4f, 0.4f, Era.Camp, SeedOf(camp));
            }
            else L.Clearing(green, SiteMap.GreenR + 0.04f, greenEra == Era.Town && st.Has(LotKind.Market) ? ClearingKind.Cobble : ClearingKind.Green);
            // The village green has its well from the era moment on (era-moment.md §4, #89). Until the sim builds its
            // first well, the view stands one exactly where the sim's first well will go (Settlement: green + (0.17, -0.1)).
            if (greenEra >= Era.Village && !HasOpenWell(st))
                L.Add(PieceKind.Well, green.x + FirstWellDX, green.y + FirstWellDZ, 0f, 0.3f, 0.3f, greenEra, 19000);

            // ---- ground: yards, commons, the Keeper's shapings ----
            foreach (var y in replay != null ? NoYards : st.Yards)
                if (y.count > 0) L.Clearing(new Vector2(y.x, y.z), Mathf.Clamp(y.r * 0.85f, 0.16f, 0.35f), ClearingKind.Yard);
            foreach (var f in replay != null ? NoFeatures : st.Features())
            {
                var at = new Vector2(f.x, f.z);
                switch (f.kind)
                {
                    case "commons": L.Clearing(at, f.r, ClearingKind.Meadow); break;
                    case SimCore.NudgeKind.DyeMeadow: L.Clearing(at, f.r, ClearingKind.Flowers); break;
                    case SimCore.NudgeKind.Outcrop:
                    {
                        var p = L.Add(PieceKind.Outcrop, f.x, f.z, 0f, f.r * 2f, f.r * 2f, S.era, 30000 + f.nudgeId);
                        p.variant = f.deposit == "clay" ? 1 : f.deposit == "ore" ? 2 : 0;
                        break;
                    }
                    case SimCore.NudgeKind.Herd:
                        // An answered herd lives on its pasture or jetty; until then the animals graze at the spot.
                        if (f.lotId == 0 && !(KeeperOverlays && replay == null)) L.Add(PieceKind.Herd, f.x, f.z, 0f, 0.45f, 0.45f, S.era, 31000 + f.nudgeId).variant = 4;
                        break;
                }
            }

            // ---- lots ----
            foreach (var l in st.Lots)
            {
                if (l.kind == LotKind.Camp || (replay != null ? !replay.Shows(l) : l.retired)) continue;
                var e = EraOf(l);
                int s = SeedOf(l);
                float h = STerrain.Hash3(seed, l.id, 61);
                PieceData P(PieceKind k, float w, float d) => L.Add(k, l.x, l.z, l.yaw, w, d, e, s);
                switch (l.kind)
                {
                    case LotKind.Hut:
                    case LotKind.Cottage:
                    {
                        bool cottage = l.kind == LotKind.Cottage;
                        if (e == Era.Camp) { P(PieceKind.Home, l.w, l.d); break; }
                        int tier = st.Tier(l.door);
                        float dg = Vector2.Distance(new Vector2(l.x, l.z), green);
                        bool big = !cottage && (tier >= PathNet.PathTier || e == Era.Town);
                        bool two = e == Era.Town && !cottage && (tier >= PathNet.Road || dg < 1.1f);
                        var p = P(PieceKind.Home, big ? l.w : l.w * 0.82f, big ? l.d : l.d * 0.85f);
                        p.roof = big || e == Era.Town ? RoofStyle.Auto : RoofStyle.Thatch;
                        p.storeys = two ? 2 : 1;
                        p.chimney = big && h < 0.5f;
                        break;
                    }
                    case LotKind.Farm:
                        if (e == Era.Camp)
                        {
                            L.Field(Corners(l, 0.02f), CropKind.Gathering, l.yaw, false, -1);
                            P(PieceKind.Gathering, l.w, l.d);
                        }
                        else
                        {
                            // Crops rotate every few years: ripe wheat, green shoots or fallow (Growth Lab).
                            float c = STerrain.Hash3(l.id, year / 3, 4);
                            var f = L.Field(Corners(l, 0.03f), c < 0.5f ? CropKind.Wheat : c < 0.85f ? CropKind.Sprout : CropKind.Fallow, l.yaw, true, 0);
                            f.seed = s;
                        }
                        break;
                    case LotKind.Pasture:
                        L.Field(Corners(l, 0.03f), CropKind.Pasture, l.yaw, true, 0).seed = s;
                        P(PieceKind.Herd, l.w * 0.8f, l.d * 0.8f).variant = 3 + (int)(h * 3f);
                        break;
                    case LotKind.Orchard:
                        L.Field(Corners(l, 0.03f), CropKind.Pasture, l.yaw, false, -1).seed = s;
                        P(PieceKind.Orchard, l.w, l.d);
                        break;
                    case LotKind.TimberYard: P(PieceKind.Lumbermill, l.w, l.d).variant = 1; break;
                    case LotKind.Well: P(PieceKind.Well, 0.3f, 0.3f); break;
                    case LotKind.Outbuilding: P(PieceKind.Barn, l.w, l.d).variant = h < 0.6f ? 0 : 1; break;
                    default: P(PieceFor(l.kind), l.w, l.d); break;
                }
            }

            // ---- paths: the settlement's curves, one run per tier (and per cobbled stretch) ----
            bool Cob(PathCurve c, int k)
            {
                if (cobbled == null) return false;
                int cell = SiteMap.CellAt(c.x[k], c.z[k]);
                return cell >= 0 && cobbled(cell);
            }
            foreach (var c in replay != null ? PathCurves.Build(map, replay.tiers) : st.Paths())
            {
                int n = c.Count;
                if (n < 2) continue;
                int start = 0;
                var t0 = TierOf(c.tier[0]);
                bool c0 = Cob(c, 0);
                for (int i = 1; i <= n; i++)
                {
                    var ti = i < n ? TierOf(c.tier[i]) : (PathTier)(-1);
                    bool ci = i < n && Cob(c, i);
                    if (i < n && ti == t0 && ci == c0) continue;
                    int end = Mathf.Min(i, n - 1);
                    if (end > start)
                    {
                        var pts = new Vector2[end - start + 1];
                        for (int k = start; k <= end; k++) pts[k - start] = new Vector2(c.x[k], c.z[k]);
                        L.Path(t0, pts).cobbled = c0;
                    }
                    start = i; t0 = ti; c0 = ci;
                }
            }

            // ---- market stalls (town): an arc of awnings on the cobbled square, on the market's side ----
            if (greenEra == Era.Town) MarketStalls(L, st, green, seed);

            // ---- dug channels: canal cells joined to their canal or pond neighbours ----
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (!map.Canal(c)) continue;
                int i = SiteMap.I(c), j = SiteMap.J(c);
                var a = new Vector2(SiteMap.CX(i), SiteMap.CZ(j));
                bool joined = false;
                for (int k = 0; k < 4; k++)
                {
                    int ni = i + (k == 0 ? 1 : k == 1 ? -1 : 0), nj = j + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                    int nc = SiteMap.Index(ni, nj);
                    if (map.pond[nc] || (map.Canal(nc) && nc > c)) { L.canals.Add(new[] { a, new Vector2(SiteMap.CX(ni), SiteMap.CZ(nj)) }); joined = true; }
                    else if (map.Canal(nc)) joined = true;
                }
                if (!joined) L.canals.Add(new[] { a });
            }

            // ---- trees from the forest layer ----
            // (#85: the cells of the Keeper's groves are ShapingView's, which grows them from saplings)
            HashSet<int> keeperGrove = null;
            if (KeeperOverlays && replay == null)
                foreach (var n in S.nudges)
                    if (n.kind == SimCore.NudgeKind.Grove)
                    {
                        keeperGrove ??= new HashSet<int>();
                        foreach (int c in AquariumCiv.View.Shaping.ShapingFootprint.Of(n, map, seed).cells) keeperGrove.Add(c);
                    }
            bool Woods(int c) => map.Forest(c) && (keeperGrove == null || !keeperGrove.Contains(c));
            for (int j = 0; j + 1 < SiteMap.NZ; j += 2)
                for (int i = 0; i + 1 < SiteMap.NX; i += 2)
                {
                    int cnt = 0;
                    for (int dj = 0; dj < 2; dj++) for (int di = 0; di < 2; di++) if (Woods(SiteMap.Index(i + di, j + dj))) cnt++;
                    if (cnt == 0 || (cnt == 1 && STerrain.Hash3(seed, i * 7 + 1, j) > 0.4f)) continue;
                    var pos = new Vector2(SiteMap.CX(i) + 0.05f + (STerrain.Hash3(seed, i, j * 3 + 1) - 0.5f) * 0.08f,
                                          SiteMap.CZ(j) + 0.05f + (STerrain.Hash3(seed, i, j * 3 + 2) - 0.5f) * 0.08f);
                    if (!TreeSpot(map, pos)) continue;
                    int c0 = SiteMap.CellAt(pos.x, pos.y);
                    float high = map.height[c0] - STerrain.G;
                    L.trees.Add(new TreeData
                    {
                        pos = pos,
                        scale = 0.75f + 0.45f * STerrain.Hash3(seed, i, j * 3 + 3),
                        pine = STerrain.Hash3(seed, i, j * 3 + 4) < (high > 0.35f ? 0.6f : 0.3f),
                    });
                }

            // ---- lanterns (village on): round the green, and along the town's roads ----
            if (greenEra >= Era.Village)
            {
                int count = greenEra == Era.Town ? 4 : 2;
                for (int k = 0; k < count; k++)
                {
                    float a = (k + 0.3f + 0.4f * STerrain.Hash3(seed, k, 71)) / count * Mathf.PI * 2f;
                    var p = green + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (SiteMap.GreenR + 0.06f);
                    if (OpenSpot(map, p) && !StallNear(L, p)) L.Add(PieceKind.Lantern, p.x, p.y, Face(p, green) + 90f, 0.1f, 0.1f, greenEra, 20000 + k);
                }
            }
            if (now == Era.Town)
            {
                int k = 0;
                foreach (var path in L.paths.ToArray())
                {
                    if (path.tier != PathTier.Road) continue;
                    float run = 0.4f;
                    for (int i = 0; i + 1 < path.points.Length; i++)
                    {
                        var a = path.points[i]; var b = path.points[i + 1];
                        run += Vector2.Distance(a, b);
                        if (run < 0.8f) continue;
                        run = 0f;
                        var dir = (b - a).normalized;
                        var side = new Vector2(-dir.y, dir.x) * ((k & 1) == 0 ? 0.19f : -0.19f);
                        var p = b + side;
                        if (OpenSpot(map, p)) L.Add(PieceKind.Lantern, p.x, p.y, Face(p, b) + 90f, 0.1f, 0.1f, Era.Town, 21000 + k);
                        k++;
                    }
                }
            }

            if (replay == null) People(L, sim, st, EraOf, green, camp, campFire);
            else if (replay.grow.Count > 0)
            {
                foreach (var p in L.pieces) if (replay.grow.TryGetValue(p.seed, out var g)) p.grow = g;
                L.fields.RemoveAll(f => replay.grow.TryGetValue(f.seed, out var g) && g < 0.6f); // a field comes with its farm, once it is mostly there
            }
        }

        static readonly List<Yard> NoYards = new();
        static readonly List<Feature> NoFeatures = new();

        /// <summary>Stalls the town's square holds (#34).</summary>
        public const int TownStalls = 3;

        /// <summary>
        /// The town's busiest main path (user 2026-10-30, #90: cobbles only on the busiest path, not every main path): of
        /// the path curves that reach path tier, the one whose path-tier cells carry the most wear (each counts at least
        /// <see cref="PathNet.TPath"/>, so a replay's paths that have since faded still count). Returns its path-tier
        /// skeleton cells and their eight neighbours (the smoothed curve's samples land within a cell of the skeleton), or
        /// null when there is no main path. <paramref name="prefer"/> (the last choice) wins ties up to
        /// <see cref="BusiestStickiness"/>, so the cobbles don't hop between two paths of about the same traffic.
        /// </summary>
        public static HashSet<int> BusiestPathCells(SiteMap map, List<PathCurve> curves, byte[] tiers, ICollection<int> prefer = null)
        {
            if (map == null || curves == null || tiers == null) return null;
            var wear = map.wear;
            PathCurve best = null;
            float bestScore = 0f;
            foreach (var c in curves)
            {
                float score = 0f;
                int mine = 0, kept = 0;
                foreach (int cell in c.cells)
                {
                    if (cell < 0 || cell >= tiers.Length || tiers[cell] < PathNet.PathTier) continue;
                    score += Mathf.Max(wear != null ? wear[cell] : 0f, PathNet.TPath);
                    mine++;
                    if (prefer != null && prefer.Contains(cell)) kept++;
                }
                if (mine == 0) continue;
                if (prefer != null && kept * 2 > mine) score *= BusiestStickiness;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best == null) return null;
            var set = new HashSet<int>();
            foreach (int cell in best.cells)
            {
                if (cell < 0 || cell >= tiers.Length || tiers[cell] < PathNet.PathTier) continue;
                int i = cell % SiteMap.NX, j = cell / SiteMap.NX;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int ii = i + di, jj = j + dj;
                        if (ii >= 0 && jj >= 0 && ii < SiteMap.NX && jj < SiteMap.NZ) set.Add(jj * SiteMap.NX + ii);
                    }
            }
            return set;
        }

        /// <summary>The last busiest path keeps its cobbles until another carries this much more traffic.</summary>
        public const float BusiestStickiness = 1.3f;

        /// <summary>The cobble hook (<see cref="Fill"/>'s <c>cobbled</c>) for the busiest path: its path-tier cells.</summary>
        public static Func<int, bool> BusiestCobbles(byte[] tiers, HashSet<int> busiest)
        {
            if (tiers == null || busiest == null) return null;
            return c => c >= 0 && c < tiers.Length && tiers[c] >= PathNet.PathTier && busiest.Contains(c);
        }

        /// <summary>The town's market stalls: an arc on the cobbled square on the market's side, facing the middle, kept
        /// off lots, water and the open well.</summary>
        static void MarketStalls(BoxLayout L, Settlement st, Vector2 green, int seed)
        {
            Lot market = null;
            foreach (var l in st.Lots) if (l.kind == LotKind.Market && !l.retired) { market = l; break; }
            if (market == null) return;
            var toMarket = new Vector2(market.x, market.z) - green;
            float a0 = toMarket.sqrMagnitude > 1e-6f ? Mathf.Atan2(toMarket.y, toMarket.x) : 0f;
            var map = st.Map;
            for (int k = 0, placed = 0; k < 7 && placed < TownStalls; k++)
            {
                // 0, +1, -1, +2, -2...: the arc grows from the market's side both ways.
                int step = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                float a = a0 + step * 1.0f; // ~0.3 u apart on the arc: stalls and their crates never touch
                var p = green + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (SiteMap.GreenR * 0.74f);
                if (!StallSpot(st, map, p)) continue;
                L.Add(PieceKind.Stall, p.x, p.y, Face(p, green), 0.3f, 0.22f, Era.Town, 22000 + placed * 13 + (seed & 0xFF));
                placed++;
            }
        }

        static bool StallNear(BoxLayout L, Vector2 p)
        {
            foreach (var q in L.pieces) if (q.kind == PieceKind.Stall && Vector2.Distance(q.Pos, p) < 0.24f) return true;
            return false;
        }

        static bool StallSpot(Settlement st, SiteMap map, Vector2 p)
        {
            int c = SiteMap.CellAt(p.x, p.y);
            if (c < 0 || map.Water(c) || map.lot[c] != 0) return false;
            foreach (var l in st.Lots)
                if (!l.retired && (l.open || l.kind == LotKind.Well) && Vector2.Distance(p, new Vector2(l.x, l.z)) < 0.3f) return false;
            return true;
        }

        static bool TreeSpot(SiteMap map, Vector2 p)
        {
            if (Mathf.Abs(p.x) > Land.HX - 0.14f || Mathf.Abs(p.y) > Land.HZ - 0.14f) return false;
            int c = SiteMap.CellAt(p.x, p.y);
            if (c < 0 || map.Water(c) || map.Green(c)) return false;
            // A crown is ~0.2 u across: no lot within two cells (tester #33: a hut squeezed into the woods), no path or
            // channel in the cells round it.
            int i = SiteMap.I(c), j = SiteMap.J(c);
            for (int dj = -2; dj <= 2; dj++)
                for (int di = -2; di <= 2; di++)
                {
                    int ni = i + di, nj = j + dj;
                    if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                    int nc = SiteMap.Index(ni, nj);
                    if (map.lot[nc] != 0 && di * di + dj * dj <= 5) return false;
                    if (Mathf.Abs(di) <= 1 && Mathf.Abs(dj) <= 1 && (map.Canal(nc) || map.tier[nc] >= PathNet.Trail)) return false;
                }
            return true;
        }

        static bool OpenSpot(SiteMap map, Vector2 p)
        {
            int c = SiteMap.CellAt(p.x, p.y);
            if (c < 0 || map.Water(c) || map.tier[c] >= PathNet.PathTier) return false;
            int i = SiteMap.I(c), j = SiteMap.J(c);
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int ni = i + di, nj = j + dj;
                    if (ni >= 0 && nj >= 0 && ni < SiteMap.NX && nj < SiteMap.NZ && map.lot[SiteMap.Index(ni, nj)] != 0) return false; // not against a wall
                }
            return true;
        }

        // ---------------- people ----------------
        static void People(BoxLayout L, SimCore sim, Settlement st, Func<Lot, Era> eraOf, Vector2 green, Lot camp, bool campFire)
        {
            int budget = Mathf.Clamp(sim.State.population, 3, MaxPeople);
            void V(Vector2 p, float yaw, Era e, int seed, bool kid = false, PropKind prop = PropKind.None, PropSlot slot = PropSlot.Hand)
            {
                if (L.villagers.Count >= budget) return;
                if (Mathf.Abs(p.x) > Land.HX - 0.12f || Mathf.Abs(p.y) > Land.HZ - 0.12f || st.WaterAt(p.x, p.y)) return;
                L.Villager(p, yaw, e, seed, kid, -1, prop, slot);
            }
            Era greenEra = eraOf(null);

            // Round the fire (camp) or the well / the green.
            if (campFire)
            {
                var fire = new Vector2(camp.x, camp.z);
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f + 0.9f;
                    var p = fire + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f;
                    V(p, Face(p, fire), Era.Camp, 100 + i, kid: i == 3);
                }
            }
            else
            {
                var centre = green;
                foreach (var l in st.Lots) if (l.kind == LotKind.Well && l.open && !l.retired) { centre = new Vector2(l.x, l.z); break; }
                for (int i = 0; i < 3; i++)
                {
                    float a = i / 3f * Mathf.PI * 2f + 0.5f;
                    var p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f;
                    V(p, Face(p, centre), greenEra, 100 + i, prop: i == 0 ? PropKind.Pot : PropKind.None, slot: PropSlot.Head);
                }
            }

            // At work: fields, the mill, the market, the workshops.
            foreach (var l in st.Lots)
            {
                if (l.retired) continue;
                var e = eraOf(l);
                int s = 1000 + l.id * 8;
                switch (l.kind)
                {
                    case LotKind.Farm:
                        V(Local(l, l.w * 0.15f, l.d * 0.05f), l.yaw + 90f, e, s, prop: e == Era.Camp ? PropKind.Basket : PropKind.Hoe);
                        break;
                    case LotKind.Pasture:
                    case LotKind.Orchard:
                        V(Local(l, 0f, -l.d / 2f - 0.08f), l.yaw + 180f, e, s, prop: PropKind.Basket);
                        break;
                    case LotKind.Lumbermill:
                    case LotKind.TimberYard:
                        V(Local(l, l.w * 0.3f, -l.d / 2f - 0.09f), l.yaw + 150f, e, s, prop: PropKind.Axe);
                        break;
                    case LotKind.Market:
                        V(Local(l, -0.1f, -l.d / 2f - 0.1f), l.yaw + 180f, e, s, prop: PropKind.Basket);
                        V(Local(l, 0.12f, -l.d / 2f - 0.12f), l.yaw, e, s + 1);
                        break;
                    case LotKind.Tavern:
                    case LotKind.Smithy:
                    case LotKind.Watermill:
                    case LotKind.Dyer:
                    case LotKind.Workshop:
                    case LotKind.Kiln:
                    case LotKind.Quarry:
                    case LotKind.Jetty:
                        V(new Vector2(l.doorX, l.doorZ), l.yaw, e, s, prop: l.kind == LotKind.Quarry ? PropKind.Log : PropKind.None, slot: PropSlot.Shoulder);
                        break;
                }
            }

            // At home: one at every door, now and then a kid beside them.
            int n = 0;
            foreach (var l in st.Lots)
            {
                if (l.retired || !Encyclopedia.IsHome(l.kind) || l.kind == LotKind.Smithy || l.kind == LotKind.Workshop || l.kind == LotKind.Dyer || l.kind == LotKind.Kiln) continue;
                var e = eraOf(l);
                var door = new Vector2(l.doorX, l.doorZ);
                V(door, l.yaw, e, 1000 + l.id * 8 + 2, prop: n % 4 == 1 ? PropKind.Bundle : PropKind.None, slot: PropSlot.Back);
                if (n % 3 == 0) V(Local(l, l.w * 0.35f, -l.d / 2f - 0.1f), l.yaw + 30f, e, 1000 + l.id * 8 + 3, kid: true);
                n++;
            }

            // Kids in the family yards, then the rest out on the green.
            foreach (var y in st.Yards)
                if (y.count > 0) V(new Vector2(y.x, y.z), y.id * 47f, sim.State.era, 5000 + y.id * 4, kid: true);
            for (int i = 0; L.villagers.Count < budget && i < MaxPeople * 2; i++)
            {
                float a = STerrain.Hash3(sim.State.seed, i, 81) * Mathf.PI * 2f, r = SiteMap.GreenR * (0.45f + 0.5f * STerrain.Hash3(sim.State.seed, i, 82));
                var p = green + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (campFire && Vector2.Distance(p, new Vector2(camp.x, camp.z)) < 0.24f) continue;
                bool byWell = false; // keep clear of the well and the other wells' footprints
                foreach (var l in st.Lots)
                    if (l.kind == LotKind.Well && !l.retired && Vector2.Distance(p, new Vector2(l.x, l.z)) < 0.24f) { byWell = true; break; }
                if (byWell) continue;
                V(p, STerrain.Hash3(sim.State.seed, i, 83) * 360f, greenEra, 9000 + i, kid: i % 3 == 1, prop: i % 5 == 2 ? PropKind.Bundle : PropKind.None, slot: PropSlot.Back);
            }
        }
    }
}
