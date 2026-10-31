using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>A land feature for the view and the planner: the pond, the commons, and each of the Keeper's shapings
    /// (with the lot that answered it, if any).</summary>
    public struct Feature
    {
        public string kind;     // "pond", "commons", or a SimCore.NudgeKind
        public float x, z, r;
        public int nudgeId;     // 0 for the pond and the commons
        public int lotId;       // the answer's lot (0 = not answered yet)
        public string deposit;  // outcrops: "stone", "clay" or "ore"
    }

    /// <summary>
    /// Where things go (research 08 §3, Growth Lab → M2): the economy (SimCore) decides <i>what</i> gets built, this
    /// decides <i>where</i>, at the end of every month: each new building gets a lot (homes by the cluster village
    /// rules, everything else by round 1's), a connection path at once, and every third month the people's trips wear
    /// the paths. All results live in <see cref="SettleState"/> (the save). No dice: ties and jitter come from hashes,
    /// so placement can never shift the economy (SettleTests checks it). Runtime objects (the static land, the flow
    /// field buffers) are rebuilt from the save on load; a save from before placement is migrated from slots once.
    /// </summary>
    public sealed class Settlement
    {
        readonly SimCore core;
        public SiteMap Map { get; private set; }
        public PathNet Net { get; private set; }
        readonly Placer placer;
        readonly ClusterPlacer cluster;

        /// <summary>Timing (criterion 6): placement and wear, not the economy. Not saved.</summary>
        public long placeTicks, wearTicks, monthsStepped;

        readonly List<int> tmpNet = new List<int>(), tmpRoute = new List<int>(), tmpCells = new List<int>();
        readonly List<int> undoCells = new List<int>();
        readonly List<byte> undoFlags = new List<byte>();
        readonly bool[] tmpMark = new bool[SiteMap.N];
        readonly int[][] fields = { new int[SiteMap.N], new int[SiteMap.N], new int[SiteMap.N], new int[SiteMap.N] };
        readonly List<int>[] groups = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        readonly List<Lot> homes = new List<Lot>();
        readonly List<int> doors = new List<int>();
        List<PathCurve> curves;
        long curvesMonth = -1;
        int curvesHash;

        SimState S => core.State;
        SettleState St => core.State.settle;
        public int Seed => S.seed;
        public Era Era => S.era;
        public long Month => S.month;
        public List<Lot> Lots => St.lots;
        public List<Yard> Yards => St.yards;
        public List<Nudge> Nudges => S.nudges;
        public SettleState State => St;

        public Settlement(SimCore core)
        {
            this.core = core;
            bool fresh = St.version == 0;
            if (fresh) St.terrain = core.Config.terrainPreset;
            Map = new SiteMap(St, fresh);
            Net = new PathNet(Map);
            placer = new Placer(this);
            cluster = new ClusterPlacer(this, placer);
            if (fresh) Found();
            AssignHomes();
        }

        /// <summary>A new box, or a save from before placement (slots only): reserve the commons, find a spot for every
        /// shaping and piece of trash from its old slot, then place every building in the order it was built.</summary>
        void Found()
        {
            ClusterPlacer.ReserveCommons(Map, St, Seed);
            St.version = SettleState.Version;
            foreach (var n in S.nudges)
            {
                if (n.x == 0f && n.z == 0f) SlotPosition(n.slot, out n.x, out n.z);
                ApplyShaping(n);
            }
            ProcessNewBuildings(migrating: S.buildings.Count > 2);
            foreach (var t in S.trash) TrashSpot(t);
            if (S.era >= Era.Village) Outbuildings();
            WearPass();
        }

        // ---------------- the month ----------------
        /// <summary>Called by SimCore at the end of every month, after the economy (and only when settling is on).</summary>
        public void Step()
        {
            long t0 = Stopwatch.GetTimestamp();
            AssignHomes();
            ProcessNewBuildings(false);
            AssignHomes();
            if (S.month % SimConfig.MonthsPerYear == 0) Yearly();
            if (Waiting() > 0) { St.noLotMonths++; if (St.noLotSince < 0) St.noLotSince = S.month; }
            else St.noLotSince = -1;
            long t1 = Stopwatch.GetTimestamp();
            placeTicks += t1 - t0;
            if (S.month % 3 == 0) WearPass();
            wearTicks += Stopwatch.GetTimestamp() - t1;
            monthsStepped++;
        }

        /// <summary>Offer a lot to buildings added since the last month (debug builds) without stepping time.</summary>
        public void PlacePending() => ProcessNewBuildings(false);

        /// <summary>The era moment: lots never move; the camp's hearth gives way to the green, families get barns.</summary>
        public void OnEraChange()
        {
            if (S.era >= Era.Village)
                foreach (var l in Lots) if (l.kind == LotKind.Camp) l.retired = true;
            Outbuildings();
        }

        void Yearly()
        {
            // Builds that found no lot try again once a year (the land changes: rings released, woods cut back).
            if (Waiting() > 0)
                foreach (var b in S.buildings)
                    if (b.lotId == 0 && Processed(b)) TryPlace(b, null);
            if (S.era >= Era.Village) Outbuildings();
            // Lumber mills cut back the nearest woods a little every year.
            foreach (var s in Lots)
            {
                if (s.retired || (s.kind != LotKind.Lumbermill && s.kind != LotKind.TimberYard)) continue;
                for (int k = 0; k < 2; k++)
                {
                    int best = -1; float bd = 1.6f;
                    for (int c = 0; c < SiteMap.N; c++)
                    {
                        if (!Map.Forest(c)) continue;
                        float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), s.x, s.z);
                        if (d < bd) { bd = d; best = c; }
                    }
                    if (best >= 0) Map.Set(best, SiteMap.FForest, false);
                }
            }
        }

        bool Processed(Building b)
        {
            int i = S.buildings.IndexOf(b);
            return i >= 0 && i < St.processed;
        }

        /// <summary>Buildings still waiting for a lot.</summary>
        public int Waiting()
        {
            int n = 0;
            for (int i = 0; i < St.processed && i < S.buildings.Count; i++) if (S.buildings[i].lotId == 0) n++;
            return n;
        }

        /// <summary>Game months the current wait for a lot has lasted (0 = nothing waits). The town's peak asks for 36.</summary>
        public long MonthsWithoutLot => St.noLotSince < 0 ? 0 : S.month - St.noLotSince;

        // ---------------- placement ----------------
        void ProcessNewBuildings(bool migrating)
        {
            var bs = S.buildings;
            for (; St.processed < bs.Count; St.processed++)
            {
                var b = bs[St.processed];
                Nudge answer = null;
                foreach (var n in S.nudges) if (n.buildingId == b.id && b.id != 0) { answer = n; break; }
                if (!TryPlace(b, answer))
                {
                    St.noLotBuilds++;
                    if (St.noLotSince < 0) St.noLotSince = S.month;
                }
                // Migrating an old save: paths grow as they would have between builds.
                if (migrating && St.processed % 2 == 1) WearPass();
            }
        }

        bool TryPlace(Building b, Nudge answer)
        {
            var kind = KindFor(b, answer);
            var lot = PlaceNew(kind, b.id, answer);
            var plain = Encyclopedia.FromType(b.type);
            if (lot == null && kind != plain)
            {
                lot = PlaceNew(plain, b.id, answer); // the special kind found no spot
                // The chronicle must name what stands there (#82: "built a watermill" over a plain workshop).
                if (lot != null && answer != null)
                {
                    var e = S.chronicle.LastOrDefault(x => x.keeper && x.kind == "keeper" && x.buildingId == b.id);
                    if (e != null)
                    {
                        var who = S.notables.FirstOrDefault(x => x.id == e.notableId);
                        e.text = SimCore.CreditLine(answer.kind, b.type, who, S.era, true, answer.variant);
                    }
                }
            }
            if (lot == null) return false;
            b.lotId = lot.id; b.yardId = lot.yard;
            b.x = lot.x; b.z = lot.z; b.yaw = lot.yaw; b.w = lot.w; b.d = lot.d; b.doorX = lot.doorX; b.doorZ = lot.doorZ;
            if (answer != null) answer.lotId = lot.id;
            return true;
        }

        /// <summary>What a building stands on: its type's kind, unless it answers a shaping, which the era and the land
        /// at the shaped spot turn into the fitting kind (a channel's workshop is a watermill, an outcrop's a quarry,
        /// a kiln or a smithy by the rock). Reads only the terrain and the shaping, never placement results.</summary>
        public LotKind KindFor(Building b, Nudge n)
        {
            var plain = Encyclopedia.FromType(b.type);
            if (n == null) return b.type == BuildingType.Hut && S.era == Era.Town ? LotKind.Cottage : plain; // the town's huts are cottages: smaller, clustered, one more to a yard (#114)
            bool wet = SimCore.WetSpot(Map.spec, n.x, n.z);
            switch (b.type)
            {
                case BuildingType.Farm:
                    if (n.kind == SimCore.NudgeKind.Grove && S.era >= Era.Village) return LotKind.Orchard;
                    if (n.kind == SimCore.NudgeKind.Herd) return wet ? LotKind.Jetty : LotKind.Pasture;
                    return LotKind.Farm;
                case BuildingType.Workshop:
                    switch (n.kind)
                    {
                        case SimCore.NudgeKind.Channel: return LotKind.Watermill;
                        case SimCore.NudgeKind.Grove: return LotKind.TimberYard;
                        case SimCore.NudgeKind.Herd: return wet ? LotKind.Jetty : LotKind.Workshop;
                        case SimCore.NudgeKind.DyeMeadow: return LotKind.Dyer;
                        case SimCore.NudgeKind.Outcrop:
                            string dep = SimCore.Deposit(Map.spec, n.x, n.z);
                            return dep == "clay" ? LotKind.Kiln : dep == "ore" ? LotKind.Smithy : LotKind.Quarry;
                    }
                    return LotKind.Workshop;
            }
            return plain;
        }

        Lot PlaceNew(LotKind kind, int buildingId, Nudge answer)
        {
            var spec = Encyclopedia.Of(kind);
            int id = St.nextLotId++;
            var site = new Lot
            {
                id = id, buildingId = buildingId, kind = kind, foundedMonth = S.month,
                w = spec.wMin + (spec.wMax - spec.wMin) * Terrain.Hash3(Seed, id, 11),
                d = spec.dMin + (spec.dMax - spec.dMin) * Terrain.Hash3(Seed, id, 12),
                nudgeId = answer?.id ?? 0,
            };
            // (a well that answers a spring goes to the spring)
            bool firstOnGreen = (kind == LotKind.Camp && !Has(LotKind.Camp)) || (kind == LotKind.Well && answer == null && !HasOpenWell());
            if (firstOnGreen)
            {
                // The founding hearth is the green's centre; the first well goes beside it (lots never move).
                site.open = true;
                site.x = Map.greenX + (kind == LotKind.Well ? 0.17f : 0f);
                site.z = Map.greenZ + (kind == LotKind.Well ? -0.1f : 0f);
                site.door = SiteMap.CellAt(site.x, site.z);
                site.doorX = site.x; site.doorZ = site.z;
                Lots.Add(site);
                return site;
            }
            bool ok = cluster.Place(site, answer);
            return ok ? site : null;
        }

        public bool Has(LotKind k)
        {
            foreach (var s in Lots) if (s.kind == k && !s.retired) return true;
            return false;
        }

        bool HasOpenWell()
        {
            foreach (var s in Lots) if (s.kind == LotKind.Well && s.open) return true;
            return false;
        }

        /// <summary>Claim a lot: occupancy, clear its trees, lay its connection path. False (and undone) if cut off.</summary>
        public bool Claim(Lot site, float x, float z, float yaw, float w, float d, int door, bool allowGreen, bool needsPath = true)
        {
            site.x = x; site.z = z; site.yaw = yaw; site.w = w; site.d = d; site.door = door;
            SiteMap.RectCells(x, z, yaw, w, d, 0f, tmpCells);
            undoCells.Clear(); undoFlags.Clear();
            foreach (int c in tmpCells)
            {
                if (Map.lot[c] != 0) { Undo(site.id); return false; }
                undoCells.Add(c); undoFlags.Add(Map.flags[c]);
                Map.lot[c] = site.id;
                Map.Set(c, SiteMap.FForest, false);
                if (allowGreen) Map.Set(c, SiteMap.FGreen, false);
            }
            bool connected = !needsPath;
            if (needsPath && door >= 0 && Map.lot[door] == 0 && !Map.pond[door])
            {
                Net.Network(tmpNet, tmpMark, laidOnly: true);
                connected = Net.Connect(door, tmpNet, tmpRoute, S.month);
            }
            if (!connected) { Undo(site.id); return false; }
            foreach (int c in undoCells) { Map.tier[c] = 0; Map.wear[c] = 0f; St.pathMonth[c] = 0; }
            site.Front(out float fx, out float fz);
            site.doorX = x + fx * (d / 2f + 0.07f);
            site.doorZ = z + fz * (d / 2f + 0.07f);
            Lots.Add(site);
            return true;
        }

        void Undo(int id)
        {
            for (int i = 0; i < undoCells.Count; i++)
            {
                int c = undoCells[i];
                if (Map.lot[c] == id) Map.lot[c] = 0;
                Map.flags[c] = undoFlags[i];
            }
            undoCells.Clear(); undoFlags.Clear();
        }

        /// <summary>Every family yard with a home gets a barn, byre or shed across it (village era on).</summary>
        void Outbuildings()
        {
            foreach (var k in Yards)
            {
                // From the second home on (or once the family stops growing), so the barn rarely takes a home's place.
                if (k.outbuildingLot != 0 || k.count < 1 || (k.ringHeld && k.count < 2)) continue;
                var spec = Encyclopedia.Of(LotKind.Outbuilding);
                int id = St.nextLotId++;
                var site = new Lot
                {
                    id = id, kind = LotKind.Outbuilding, foundedMonth = S.month,
                    w = spec.wMin + (spec.wMax - spec.wMin) * Terrain.Hash3(Seed, id, 11),
                    d = spec.dMin + (spec.dMax - spec.dMin) * Terrain.Hash3(Seed, id, 12),
                };
                // No room for a barn: the yard tries again next year (the land round it changes).
                if (cluster.PlaceOutbuilding(k, site)) k.outbuildingLot = site.id;
            }
        }

        // ---------------- paths ----------------
        /// <summary>One season of trips: every home walks to the green, to a field or a workplace, and to a gathering
        /// place (market, tavern, church); the wear decays and the tiers update (research 08 §3.4).</summary>
        void WearPass()
        {
            for (int g = 0; g < 4; g++) groups[g].Clear();
            for (int c = 0; c < SiteMap.N; c++) if (Map.Green(c) && Map.lot[c] == 0) groups[0].Add(c);
            homes.Clear();
            foreach (var s in Lots)
            {
                if (s.retired || s.door < 0 || s.open) continue;
                var spec = Encyclopedia.Of(s.kind);
                if (spec.home) homes.Add(s);
                switch (spec.trips)
                {
                    case TripGroup.Field: groups[1].Add(s.door); break;
                    case TripGroup.Work: groups[2].Add(s.door); break;
                    case TripGroup.Gathering: groups[3].Add(s.door); break;
                }
            }
            doors.Clear();
            foreach (var h in homes) doors.Add(h.door);
            Net.PrepareCosts();
            for (int g = 0; g < 4; g++)
                if (groups[g].Count > 0) Array.Copy(Net.Flow(groups[g], false, doors), fields[g], SiteMap.N);
            for (int k = 0; k < homes.Count; k++)
            {
                int door = homes[k].door;
                if (Net.Trace(fields[0], door, tmpRoute)) Net.Wear(tmpRoute, 1f);
                int work = (k % 3 != 2 && groups[1].Count > 0) || groups[2].Count == 0 ? 1 : 2;
                if (groups[work].Count > 0 && Net.Trace(fields[work], door, tmpRoute)) Net.Wear(tmpRoute, 0.8f);
                if (groups[3].Count > 0 && Net.Trace(fields[3], door, tmpRoute)) Net.Wear(tmpRoute, 0.5f);
            }
            Net.UpdateTiers(S.era == Era.Town, S.month);
        }

        // ---------------- the Keeper's shaping ----------------
        /// <summary>The land changes where the Keeper shaped it (called by SimCore.PlaceNudge; the economy already
        /// took its share). Rain and the lamp change fertility (read live from the nudges); a grove plants woods; a
        /// channel or a spring digs water to the pond; an outcrop pushes up rock; a dye meadow blooms; a herd needs no
        /// layer (the animals are the view's). Commons under a shaping open up so the answer can land there.</summary>
        public void ApplyShaping(Nudge n)
        {
            n.x = Terrain.Clamp(n.x, -Terrain.HX + 0.1f, Terrain.HX - 0.1f);
            n.z = Terrain.Clamp(n.z, -Terrain.HZ + 0.1f, Terrain.HZ - 0.1f);
            byte[] before = n.shapeCells == null ? (byte[])Map.flags.Clone() : null; // #82: what to put back on a take-back
            ApplyShapingToLand(n);
            if (before == null) return;
            var cells = new List<int>(); var was = new List<byte>();
            for (int c = 0; c < SiteMap.N; c++)
                if (Map.flags[c] != before[c]) { cells.Add(c); was.Add(before[c]); }
            n.shapeCells = cells.ToArray(); n.shapeBefore = was.ToArray();
        }

        /// <summary>Take a shaping back (#82): the cells it changed go back to what they were, unless a lot has since
        /// claimed them. Only for shapings the people have not used.</summary>
        public void RevertShaping(Nudge n)
        {
            if (n.shapeCells == null) return;
            bool water = false;
            for (int k = 0; k < n.shapeCells.Length; k++)
            {
                int c = n.shapeCells[k];
                if (Map.lot[c] != 0) continue;
                if (((Map.flags[c] ^ n.shapeBefore[k]) & SiteMap.FCanal) != 0) water = true;
                Map.flags[c] = n.shapeBefore[k];
            }
            if (water) Map.RecomputeWaterDist();
            n.shapeCells = null; n.shapeBefore = null;
        }

        void ApplyShapingToLand(Nudge n)
        {
            switch (n.kind)
            {
                case SimCore.NudgeKind.Grove: Paint(n.x, n.z, 0.38f, SiteMap.FForest, true); break;
                case SimCore.NudgeKind.Channel:
                case SimCore.NudgeKind.Spring: DigCanal(n.x, n.z); break;
                case SimCore.NudgeKind.Outcrop: Paint(n.x, n.z, 0.16f, SiteMap.FRock, false); break;
                case SimCore.NudgeKind.DyeMeadow: Paint(n.x, n.z, 0.4f, SiteMap.FMeadow, true); break;
            }
            for (int c = 0; c < SiteMap.N; c++)
                if (Map.CommonsAt(c) && Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), n.x, n.z) < 0.7f) Map.Set(c, SiteMap.FCommons, false);
        }

        /// <summary>Is this shaping still waiting for the people's answer (others keep clear of it)?</summary>
        public bool Waiting(Nudge n) => !n.credited && n.lotId == 0 && S.month - n.month < SimCore.NudgeMonths;

        void Paint(float x, float z, float r, byte flag, bool skipPaths)
        {
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) > r) continue;
                if (Map.lot[c] != 0 || Map.Water(c) || Map.Green(c)) continue;
                if (skipPaths && (Map.Laid(c) || Map.tier[c] >= PathNet.PathTier)) continue;
                Map.Set(c, flag, true);
            }
        }

        /// <summary>A channel from the pond to the spot, routed round lots and the green; bridges where it crosses a path.</summary>
        bool DigCanal(float x, float z)
        {
            int target = SiteMap.CellAt(x, z);
            if (target < 0 || Map.Water(target)) return false;
            if (Map.lot[target] != 0 || Map.Green(target))
            {
                int best = -1; float bd = 9f;
                for (int c = 0; c < SiteMap.N; c++)
                {
                    if (Map.lot[c] != 0 || Map.Green(c) || Map.Water(c)) continue;
                    float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                    if (d < bd) { bd = d; best = c; }
                }
                if (best < 0) return false;
                target = best;
            }
            var dist = new int[SiteMap.N];
            for (int c = 0; c < SiteMap.N; c++) dist[c] = PathNet.Inf;
            var open = new SortedSet<(int d, int c)>();
            for (int c = 0; c < SiteMap.N; c++) if (Map.pond[c]) { dist[c] = 0; open.Add((0, c)); }
            while (open.Count > 0)
            {
                var (d, c) = open.Min; open.Remove(open.Min);
                if (d > dist[c]) continue;
                int i = SiteMap.I(c), j = SiteMap.J(c);
                for (int k = 0; k < 4; k++)
                {
                    int ni = i + (k == 0 ? 1 : k == 1 ? -1 : 0), nj = j + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (ni < 1 || nj < 1 || ni >= SiteMap.NX - 1 || nj >= SiteMap.NZ - 1) continue;
                    int nc = SiteMap.Index(ni, nj);
                    if (Map.lot[nc] != 0 || Map.Green(nc)) continue;
                    int cost = 10 + (int)(Map.slope[nc] * 60f) + (Map.Laid(nc) ? 25 : 0);
                    if (d + cost < dist[nc]) { dist[nc] = d + cost; open.Add((dist[nc], nc)); }
                }
            }
            if (dist[target] >= PathNet.Inf) return false;
            int cur = target, guard = 0;
            while (dist[cur] > 0 && guard++ < SiteMap.N)
            {
                Map.Set(cur, SiteMap.FCanal, true);
                Map.Set(cur, SiteMap.FForest, false);
                int i = SiteMap.I(cur), j = SiteMap.J(cur), best = cur;
                for (int k = 0; k < 4; k++)
                {
                    int ni = i + (k == 0 ? 1 : k == 1 ? -1 : 0), nj = j + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                    int nc = SiteMap.Index(ni, nj);
                    if (dist[nc] < dist[best]) best = nc;
                }
                if (best == cur) break;
                cur = best;
            }
            Map.RecomputeWaterDist();
            return true;
        }

        /// <summary>The nearest point to (x, z) on dry land, inside the glass margin, with no lot or green within r
        /// (what a player's finger lands on when they shape open ground).</summary>
        public bool OpenSpotNear(ref float x, ref float z, float r)
        {
            int best = -1; float bd = float.MaxValue;
            const float mx = Terrain.HX - Terrain.GlassMargin - 0.1f, mz = Terrain.HZ - Terrain.GlassMargin - 0.1f;
            int rr = (int)MathF.Ceiling(r / SiteMap.Cell);
            for (int c = 0; c < SiteMap.N; c++)
            {
                float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
                if (MathF.Abs(cx) > mx || MathF.Abs(cz) > mz || Map.Water(c) || Map.slope[c] > 0.4f) continue;
                float d = Terrain.Dist(cx, cz, x, z);
                if (d >= bd) continue;
                bool clear = true;
                int i = SiteMap.I(c), j = SiteMap.J(c);
                for (int dj = -rr; dj <= rr && clear; dj++)
                    for (int di = -rr; di <= rr && clear; di++)
                    {
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                        if (di * di + dj * dj > rr * rr) continue;
                        int nc = SiteMap.Index(ni, nj);
                        if (Map.lot[nc] != 0 || Map.Green(nc)) clear = false;
                    }
                if (clear) { bd = d; best = c; }
            }
            if (best < 0) return false;
            x = SiteMap.CXc(best); z = SiteMap.CZc(best);
            return true;
        }

        // ---------------- trash and old slots ----------------
        /// <summary>Where a piece of trash lies: by the door of the lot its (economy-drawn) slot picks, offset by its
        /// (economy-drawn) offsets. Draws nothing.</summary>
        public void TrashSpot(TrashItem t)
        {
            Lot at = null;
            int count = 0;
            foreach (var l in Lots) if (!l.retired) count++;
            if (count > 0)
            {
                int pick = ((t.slot % count) + count) % count;
                foreach (var l in Lots)
                {
                    if (l.retired) continue;
                    if (pick-- == 0) { at = l; break; }
                }
            }
            float bx = at?.doorX ?? Map.greenX, bz = at?.doorZ ?? Map.greenZ;
            float x = bx + t.offsetX * 0.6f, z = bz + t.offsetZ * 0.6f;
            int c = SiteMap.CellAt(x, z);
            if (c < 0 || Map.Water(c) || Map.lot[c] != 0) { x = bx; z = bz; }
            t.x = Terrain.Clamp(x, -Terrain.HX + 0.1f, Terrain.HX - 0.1f);
            t.z = Terrain.Clamp(z, -Terrain.HZ + 0.1f, Terrain.HZ - 0.1f);
        }

        /// <summary>The position of an old building slot (the prototypes' hex slots, centre-out round the green), used
        /// to migrate saves and for callers that still pass a slot.</summary>
        public void SlotPosition(int slot, out float x, out float z)
        {
            const float spacing = 0.62f;
            var list = new List<(float x, float z, float d)>();
            float rowH = spacing * 0.8660254f;
            const float mx = Terrain.HX - Terrain.GlassMargin - 0.2f, mz = Terrain.HZ - Terrain.GlassMargin - 0.2f;
            for (int row = -10; row <= 10; row++)
                for (int col = -12; col <= 12; col++)
                {
                    float px = Map.greenX + col * spacing + (row & 1) * spacing * 0.5f, pz = Map.greenZ + row * rowH;
                    if (MathF.Abs(px) > mx || MathF.Abs(pz) > mz) continue;
                    int c = SiteMap.CellAt(px, pz);
                    if (c < 0 || Map.waterDist[c] < 0.25f || Map.slope[c] > 0.5f) continue;
                    list.Add((px, pz, Terrain.Dist(px, pz, Map.greenX, Map.greenZ)));
                }
            list.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            if (list.Count == 0) { x = Map.greenX; z = Map.greenZ; return; }
            var p = list[Math.Max(0, Math.Min(list.Count - 1, slot))];
            x = p.x; z = p.z;
        }

        // ================= queries for the view and the planner =================
        public float GreenX => Map.greenX;
        public float GreenZ => Map.greenZ;
        public float GreenRadius => SiteMap.GreenR;
        public TerrainSpec Terrain0 => Map.spec;

        public Lot LotById(int id)
        {
            if (id == 0) return null;
            foreach (var l in Lots) if (l.id == id) return l;
            return null;
        }

        public Lot LotOf(Building b) => b == null ? null : LotById(b.lotId);

        /// <summary>Lots standing now (not retired), outbuildings included.</summary>
        public IEnumerable<Lot> Standing()
        {
            foreach (var l in Lots) if (!l.retired) yield return l;
        }

        /// <summary>The era moment's rebuild wave: standing lots from the centre outward (ties by id). Lots never move.</summary>
        public List<Lot> RebuildOrder()
        {
            var r = new List<Lot>(Standing());
            float gx = Map.greenX, gz = Map.greenZ;
            r.Sort((a, b) =>
            {
                int s = Terrain.Dist(a.x, a.z, gx, gz).CompareTo(Terrain.Dist(b.x, b.z, gx, gz));
                return s != 0 ? s : a.id.CompareTo(b.id);
            });
            return r;
        }

        /// <summary>The fire: the camp's hearth on the green (it stays the green's centre after the camp; <paramref name="lit"/>
        /// says whether the hearth still burns, i.e. the box is still a camp).</summary>
        public void Fire(out float x, out float z, out bool lit)
        {
            x = Map.greenX; z = Map.greenZ; lit = false;
            foreach (var l in Lots)
                if (l.kind == LotKind.Camp) { x = l.x; z = l.z; lit = !l.retired; return; }
        }

        /// <summary>The village's centre for gatherings: the market's door once there is a market, else the green.</summary>
        public void MarketCentre(out float x, out float z)
        {
            foreach (var l in Lots)
                if (l.kind == LotKind.Market && !l.retired) { x = l.doorX; z = l.doorZ; return; }
            x = Map.greenX; z = Map.greenZ;
        }

        /// <summary>The homes (people live there) standing now.</summary>
        public List<Lot> Homes()
        {
            var r = new List<Lot>();
            foreach (var l in Lots) if (!l.retired && Encyclopedia.IsHome(l.kind)) r.Add(l);
            return r;
        }

        /// <summary>The homes round one family yard.</summary>
        public List<Lot> HomesOfYard(int yardId)
        {
            var r = new List<Lot>();
            foreach (var l in Lots) if (!l.retired && l.yard == yardId && Encyclopedia.IsHome(l.kind)) r.Add(l);
            return r;
        }

        /// <summary>The yard a notable family (surname) lives round: families in the order they first appeared among the
        /// notables, yards in the order they were founded, shared round-robin when there are more families than yards.
        /// 0 when the box has no family yard yet. Once a family has a yard it keeps it (<see cref="SettleState.familyYards"/>:
        /// a yard founded later must not shift the round-robin and put two families in one home).</summary>
        public int YardOfFamily(string family)
        {
            if (string.IsNullOrEmpty(family)) return 0;
            foreach (var f in St.familyYards) if (f.family == family) return f.yard;
            return DeriveYardOfFamily(family);
        }

        int DeriveYardOfFamily(string family)
        {
            var yards = new List<Yard>();
            foreach (var k in Yards) if (k.count > 0) yards.Add(k);
            if (yards.Count == 0 || string.IsNullOrEmpty(family)) return 0;
            var seen = new List<string>();
            foreach (var n in S.notables)
                if (!string.IsNullOrEmpty(n.family) && !seen.Contains(n.family)) seen.Add(n.family);
            int idx = seen.IndexOf(family);
            if (idx < 0) idx = seen.Count;
            return yards[idx % yards.Count].id;
        }

        /// <summary>The homes of a notable family (its yard's homes; all homes if it has no yard).</summary>
        public List<Lot> HomesOf(string family)
        {
            int y = YardOfFamily(family);
            var r = y > 0 ? HomesOfYard(y) : new List<Lot>();
            return r.Count > 0 ? r : Homes();
        }

        /// <summary>The home a notable sleeps in: children with their parent, partners together (the elder id's home),
        /// otherwise a home of the family's yard. The household keeps the home it was given (<see cref="SettleState.homes"/>)
        /// while that lot stands: a new home is filled by households that appear later and never moves one that is settled.
        /// First choice is a hash of the household head over the family's homes. A pure query: it stores nothing (the month
        /// step does, <see cref="AssignHomes"/>). Null if the box has no home.</summary>
        public Lot HomeOf(Notable n)
        {
            var head = HouseholdHead(n);
            var kept = KeptHome(head.id);
            return kept ?? PickHome(head);
        }

        Lot KeptHome(int headId)
        {
            var hs = St.homes;
            if (hs == null) return null;
            for (int i = 0; i < hs.Count; i++)
                if (hs[i].head == headId)
                {
                    foreach (var l in St.lots) if (l.id == hs[i].lot) return !l.retired && Encyclopedia.IsHome(l.kind) ? l : null;
                    return null;
                }
            return null;
        }

        Lot PickHome(Notable head)
        {
            var list = HomesOf(head.family);
            if (list.Count == 0) return null;
            return list[(int)(Terrain.Hash3(Seed, head.id, 77) * list.Count) % list.Count];
        }

        /// <summary>Give every living household head without a standing home one (the hash pick over the homes standing
        /// now) and store it. Runs each month before and after new buildings are placed, and once when a box is loaded,
        /// so an old save keeps the homes its hash gives today.</summary>
        void AssignHomes()
        {
            if (St.homes == null) St.homes = new List<HomeAssign>();
            if (St.familyYards == null) St.familyYards = new List<FamilyYard>();
            foreach (var n in S.notables)
            {
                if (string.IsNullOrEmpty(n.family) || St.familyYards.Exists(f => f.family == n.family)) continue;
                int y = DeriveYardOfFamily(n.family);
                if (y > 0) St.familyYards.Add(new FamilyYard { family = n.family, yard = y });
            }
            foreach (var n in S.notables)
            {
                if (!n.alive) continue;
                var head = HouseholdHead(n);
                if (!head.alive || KeptHome(head.id) != null) continue;
                var lot = PickHome(head);
                if (lot == null) continue;
                int i = St.homes.FindIndex(a => a.head == head.id);
                if (i >= 0) St.homes[i].lot = lot.id; else St.homes.Add(new HomeAssign { head = head.id, lot = lot.id });
            }
        }

        /// <summary>Whose home a notable lives in (<see cref="HomeOf"/>): a child's parent (up to the first grown-up), then
        /// of a couple the partner with the lower id; the notable itself otherwise.</summary>
        public Notable HouseholdHead(Notable n)
        {
            var head = n;
            for (int guard = 0; guard < 4 && head.age < SimCore.AdultAge && head.parentId >= 0; guard++)
            {
                var p = S.notables.Find(q => q.id == head.parentId);
                if (p == null || !p.alive) break;
                head = p;
            }
            if (head.partnerId >= 0 && head.partnerId < head.id)
            {
                var p = S.notables.Find(q => q.id == head.partnerId);
                if (p != null && p.alive) head = p;
            }
            return head;
        }

        /// <summary>Tier at a cell (0 none, 1 trail, 2 path, 3 road) and the walking cost into it (×10; 0 = can't).</summary>
        public byte Tier(int cell) => cell < 0 ? (byte)0 : Map.tier[cell];
        public byte TierAt(float x, float z) => Tier(SiteMap.CellAt(x, z));
        public int WalkCost(int cell) => cell < 0 || !Net.Passable(cell) ? 0 : Net.StepCost(cell);
        public float HeightAt(float x, float z) => Terrain.Height(Map.spec, x, z);
        public bool WaterAt(float x, float z) { int c = SiteMap.CellAt(x, z); return c >= 0 && Map.Water(c); }
        public bool ForestAt(float x, float z) { int c = SiteMap.CellAt(x, z); return c >= 0 && Map.Forest(c); }
        public bool CanalAt(float x, float z) { int c = SiteMap.CellAt(x, z); return c >= 0 && Map.Canal(c); }
        public bool MeadowAt(float x, float z) { int c = SiteMap.CellAt(x, z); return c >= 0 && Map.Has(c, SiteMap.FMeadow); }
        public bool RockAt(float x, float z) { int c = SiteMap.CellAt(x, z); return c >= 0 && Map.Rock(c); }

        /// <summary>The path network as smooth curves (polyline cells, samples, tier, founded month). Cached until the
        /// tiers change.</summary>
        public List<PathCurve> Paths()
        {
            int h = TierHash();
            if (curves == null || h != curvesHash || curvesMonth < 0)
            {
                curves = PathCurves.Build(Map);
                curvesHash = h; curvesMonth = S.month;
            }
            return curves;
        }

        int TierHash()
        {
            unchecked
            {
                int h = 17;
                for (int c = 0; c < SiteMap.N; c++) h = h * 31 + Map.tier[c];
                return h;
            }
        }

        /// <summary>A walk from one point to another along the people's walking cost (paths are cheaper, lots and the
        /// pond can't be crossed). Fills <paramref name="cells"/> from start to end; false if there is no way.</summary>
        public bool Route(float fromX, float fromZ, float toX, float toZ, List<int> cells)
        {
            cells.Clear();
            int a = SiteMap.CellAt(fromX, fromZ), b = SiteMap.CellAt(toX, toZ);
            if (a < 0 || b < 0) return false;
            tmpNet.Clear(); tmpNet.Add(b);
            var field = Net.Flow(tmpNet, true, new List<int> { a });
            return Net.Trace(field, a, cells);
        }

        /// <summary>The land features: the pond, the commons, and every shaping with its spot and answer.</summary>
        public List<Feature> Features()
        {
            var r = new List<Feature> { new Feature { kind = "pond", x = Map.spec.pondX, z = Map.spec.pondZ, r = 0.6f } };
            foreach (var k in St.commons) r.Add(new Feature { kind = "commons", x = k.x, z = k.z, r = k.r });
            foreach (var n in S.nudges)
            {
                float rad = n.kind == SimCore.NudgeKind.Grove ? 0.38f : n.kind == SimCore.NudgeKind.DyeMeadow ? 0.4f
                    : n.kind == SimCore.NudgeKind.Outcrop ? 0.16f : n.kind == SimCore.NudgeKind.Rain ? 0.8f : 0.3f;
                r.Add(new Feature
                {
                    kind = n.kind, x = n.x, z = n.z, r = rad, nudgeId = n.id, lotId = n.lotId,
                    deposit = n.kind == SimCore.NudgeKind.Outcrop ? SimCore.Deposit(Map.spec, n.x, n.z) : null,
                });
            }
            return r;
        }
    }
}
