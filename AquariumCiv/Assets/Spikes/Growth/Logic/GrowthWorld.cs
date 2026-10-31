using System;
using System.Collections.Generic;
using System.Diagnostics;
using AquariumCiv.Sim;

namespace AquariumCiv.Spikes.Growth
{
    /// <summary>Organic = round 1's scored frontage and rows; Random and Hand = the controls; Cluster = round 2's family
    /// clusters round shared yards (Haufendorf). New values go at the end (the scene stores the number).</summary>
    public enum Variant { Organic, Random, Hand, Cluster }

    /// <summary>A Keeper's shaping as the spike sees it (SimCore keeps its own Nudge; this adds the spot).</summary>
    public sealed class NudgeRec
    {
        public int coreId;
        public string kind;
        public float x, z;
        public long month;
        public int siteId = -1;
        public long answerMonth = -1;
        public NudgeRec Clone() => (NudgeRec)MemberwiseClone();
    }

    /// <summary>Scripted input at a month (scripted Keeper in the time-lapse; the lab's cheats): rain, grove, channel, era.</summary>
    public sealed class ScriptEvent
    {
        public long month;
        public string kind;
        public float x, z;
        /// <summary>Like a player: shape the open ground nearest the spot (no lot within 0.45 u).</summary>
        public bool snapOpen;
        public ScriptEvent(long month, string kind, float x = 0, float z = 0, bool snapOpen = false) { this.month = month; this.kind = kind; this.x = x; this.z = z; this.snapOpen = snapOpen; }
    }

    /// <summary>One game year, everything the time-lapse needs to draw it.</summary>
    public sealed class Snapshot
    {
        public const byte FCanal = 1, FForest = 2, FLaid = 4, FGreen = 8, FPond = 16, FCommons = 32, FYard = 64;
        public int year;
        public long month;
        public Era era;
        public int population;
        public Site[] sites;
        public byte[] tier, flags;
        public float[] wear;
        public NudgeRec[] nudges;
        public Cluster[] clusters;
        public List<PlacementLog> placements = new();
        public int stalls;
        public List<string> lines = new();
    }

    /// <summary>
    /// The Growth Lab driver (research 08 §5.2): SimCore decides *what* gets built (unchanged), this decides *where*.
    /// Each month: script events, SimCore.Step, place what SimCore added, the Town-era extension, a wear pass every
    /// third month, and a snapshot every year. Pure C#; live stepping and catch-up run the same code.
    /// </summary>
    public sealed class GrowthWorld
    {
        public readonly int seed;
        public readonly Variant variant;
        public readonly SimConfig cfg;
        public readonly SimCore core;
        public readonly SiteMap map;
        public readonly PathNet net;
        public readonly IPlacer placer;
        public readonly List<Site> sites = new();
        public readonly List<NudgeRec> nudges = new();
        public readonly List<ScriptEvent> script = new();
        public readonly List<Snapshot> snapshots = new();
        public readonly List<PlacementLog> placements = new();
        public readonly List<string> lines = new();
        /// <summary>Family clusters round shared yards (Cluster variant only).</summary>
        public readonly List<Cluster> clusters = new();
        public Era era = Era.Camp;
        public long eraMonth;
        public int unplaced;
        public readonly List<(Kind kind, int year)> unplacedLog = new();
        /// <summary>Placements that found no lot, with every candidate and why it was rejected (tuning).</summary>
        public readonly List<PlacementLog> failed = new();
        public bool keepSnapshots = true;

        // Timing (criterion 6): placement and wear only (SimCore's own step is reported separately).
        public long placeTicks, wearTicks, simTicks, monthsStepped;
        public int extensionCount;
        int processed;
        long nextExtensionMonth = -1;
        int nextSiteId = 1;
        readonly List<int> tmpNet = new(), tmpRoute = new(), tmpCells = new();
        readonly bool[] tmpMark = new bool[SiteMap.N];
        readonly int[][] fields = { new int[SiteMap.N], new int[SiteMap.N], new int[SiteMap.N], new int[SiteMap.N] };
        int snapshotPlacementsFrom;

        public SimState State => core.State;
        public int Year => (int)(State.month / SimConfig.MonthsPerYear);

        // Era thresholds (spike-side; the game waits for the player at each).
        public static int VillageYear = 20, VillagePop = 9, VillageLatest = 32, TownYear = 75, TownPop = 22, TownLatest = 92;
        public static int ExtensionEveryMonths = 18, ExtensionCap = 16;
        public static readonly Kind[] ExtensionOrder =
        {
            Kind.Smithy, Kind.Cottage, Kind.Dyer, Kind.Cottage, Kind.Shop, Kind.Cottage, Kind.Smithy, Kind.Cottage,
            Kind.Dyer, Kind.Cottage, Kind.Shop, Kind.Cottage, Kind.Cottage, Kind.Smithy, Kind.Cottage, Kind.Dyer,
        };

        public GrowthWorld(int seed, int terrainPreset, Variant variant, IEnumerable<ScriptEvent> events = null)
        {
            this.seed = seed;
            this.variant = variant;
            cfg = new SimConfig { buildingSlots = 96, settle = false }; // the lab places with its own Placer; the slice's Settle (#32) stays off
            var start = SimCore.NewWorld(seed, cfg);
            start.era = AquariumCiv.Sim.Era.Town; // the spike runs its own eras; SimCore's era gate (#23) stays open
            core = new SimCore(cfg, start);
            map = new SiteMap(TerrainSpec.Preset(terrainPreset));
            net = new PathNet(map);
            placer = variant switch { Variant.Random => new RandomPlacer(), Variant.Hand => new HandPlacer(), Variant.Cluster => new ClusterPlacer(), _ => new Placer() };
            if (variant == Variant.Cluster) ClusterPlacer.ReserveCommons(this);
            if (events != null) script.AddRange(events);
            script.Sort((a, b) => a.month.CompareTo(b.month));
            ProcessNewBuildings();
            WearPass();
            Snap();
        }

        // ---------------- time ----------------
        /// <summary>Like SimCore.Advance: steps on whole-month boundaries of the accumulated sim hours.</summary>
        public int Advance(double simHours)
        {
            if (simHours <= 0) return 0;
            State.simHours += simHours;
            long target = (long)Math.Floor(State.simHours / cfg.StepHours + 1e-7);
            int steps = 0;
            while (State.month < target) { StepMonth(); steps++; }
            return steps;
        }

        public void RunToYear(int year)
        {
            while (Year < year) Advance(cfg.StepHours);
        }

        public void StepMonth()
        {
            foreach (var e in script)
                if (e.month == State.month) Apply(e);

            long t0 = Stopwatch.GetTimestamp();
            core.Step();
            long t1 = Stopwatch.GetTimestamp();
            simTicks += t1 - t0;

            ProcessNewBuildings();
            Extension();
            long t2 = Stopwatch.GetTimestamp();
            placeTicks += t2 - t1;
            if (State.month % 3 == 0) WearPass();
            wearTicks += Stopwatch.GetTimestamp() - t2;
            monthsStepped++;

            if (State.month % SimConfig.MonthsPerYear == 0)
            {
                Yearly();
                Snap();
            }
        }

        void Apply(ScriptEvent e)
        {
            if (e.kind == "era") { if (era < Era.Town) EnterEra(era + 1); return; }
            float x = e.x, z = e.z;
            if (e.snapOpen) OpenSpotNear(ref x, ref z, 0.45f);
            Shape(e.kind, x, z);
        }

        // ---------------- placement ----------------
        void ProcessNewBuildings()
        {
            var bs = State.buildings;
            for (; processed < bs.Count; processed++)
            {
                var b = bs[processed];
                var kind = Encyclopedia.FromSim(b.type);
                NudgeRec answer = null;
                foreach (var n in nudges)
                {
                    var cn = State.nudges.Find(x => x.id == n.coreId);
                    if (cn != null && cn.buildingId == b.id) { answer = n; break; }
                }
                if (answer != null && answer.kind == SimCore.NudgeKind.Channel && era == Era.Town && !Has(Kind.Watermill))
                    kind = Kind.Watermill;
                PlaceNew(kind, b.id, answer);
            }
        }

        public bool Has(Kind k)
        {
            foreach (var s in sites) if (s.kind == k && !s.retired) return true;
            return false;
        }

        public Site PlaceNew(Kind kind, int simId, NudgeRec answer)
        {
            var spec = Encyclopedia.Of(kind);
            int id = nextSiteId++;
            var site = new Site
            {
                id = id, simId = simId, kind = kind, foundedMonth = State.month,
                w = spec.wMin + (spec.wMax - spec.wMin) * GrowthTerrain.Hash3(seed, id, 11),
                d = spec.dMin + (spec.dMax - spec.dMin) * GrowthTerrain.Hash3(seed, id, 12),
                nudgeId = answer?.coreId ?? -1,
            };
            var log = new PlacementLog { kind = kind, month = State.month, nudgeId = site.nudgeId };
            bool ok;
            if (kind == Kind.Camp || kind == Kind.Well)
            {
                // The founding hearth is the green's centre; the first well goes beside it (lots never move).
                site.x = map.greenX + (kind == Kind.Well ? 0.17f : 0f);
                site.z = map.greenZ + (kind == Kind.Well ? -0.1f : 0f);
                site.yaw = 0f;
                site.doorCell = SiteMap.CellAt(site.x, site.z);
                sites.Add(site);
                ok = true;
            }
            else ok = placer.Place(this, site, answer, log);
            if (!ok) { unplaced++; unplacedLog.Add((kind, Year)); failed.Add(log); return null; }
            log.siteId = site.id;
            placements.Add(log);
            if (answer != null) { answer.siteId = site.id; answer.answerMonth = State.month; }
            return site;
        }

        /// <summary>Claim a lot: occupancy, clear its trees, lay its connection path. False (and undone) if cut off.</summary>
        public bool Claim(Site site, float x, float z, float yaw, float w, float d, int door, bool allowGreen, bool force = false)
        {
            site.x = x; site.z = z; site.yaw = yaw; site.w = w; site.d = d; site.doorCell = door;
            SiteMap.RectCells(x, z, yaw, w, d, 0f, tmpCells);
            var cells = new List<int>(tmpCells);
            var keepForest = new byte[cells.Count];
            var keepGreen = new bool[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                int c = cells[i];
                if (map.lot[c] != 0 && !force) { Undo(i); return false; }
                keepForest[i] = map.forest[c]; keepGreen[i] = map.green[c];
                if (map.lot[c] == 0) map.lot[c] = site.id;
                map.forest[c] = 0; map.tier[c] = 0; map.wear[c] = 0;
                if (allowGreen) map.green[c] = false;
            }
            bool connected = false;
            if (door >= 0 && map.lot[door] == 0 && !map.pond[door])
            {
                net.Network(tmpNet, tmpMark);
                connected = net.Connect(door, tmpNet, tmpRoute);
            }
            if (!connected && !force) { Undo(cells.Count); return false; }
            sites.Add(site);
            return true;

            void Undo(int upto)
            {
                for (int i = 0; i < upto; i++)
                {
                    int c = cells[i];
                    if (map.lot[c] == site.id) map.lot[c] = 0;
                    map.forest[c] = keepForest[i];
                    map.green[c] = keepGreen[i];
                }
            }
        }

        // ---------------- eras and the Town extension ----------------
        void Yearly()
        {
            int y = Year, pop = State.population;
            if (era == Era.Camp && ((y >= VillageYear && pop >= VillagePop) || y >= VillageLatest)) EnterEra(Era.Village);
            else if (era == Era.Village && ((y >= TownYear && pop >= TownPop) || y >= TownLatest)) EnterEra(Era.Town);
            // Lumber mills cut back the nearest woods a little every year.
            foreach (var s in sites)
            {
                if (s.kind != Kind.Lumbermill) continue;
                for (int k = 0; k < 2; k++)
                {
                    int best = -1; float bd = 1.6f;
                    for (int c = 0; c < SiteMap.N; c++)
                    {
                        if (map.forest[c] == 0) continue;
                        float d = GrowthTerrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), s.x, s.z);
                        if (d < bd) { bd = d; best = c; }
                    }
                    if (best >= 0) map.forest[best] = 0;
                }
            }
        }

        public void EnterEra(Era e)
        {
            era = e;
            eraMonth = State.month;
            if (e == Era.Village)
            {
                foreach (var s in sites) if (s.kind == Kind.Camp) s.retired = true;
                lines.Add($"Year {Year}: the camp became a village. The old fire ring was cleared for a green.");
            }
            else if (e == Era.Town)
            {
                lines.Add($"Year {Year}: the village became a town of crafts.");
                if (!Has(Kind.Watermill)) PlaceNew(Kind.Watermill, -1, null);
                nextExtensionMonth = State.month + 6;
            }
        }

        void Extension()
        {
            if (era != Era.Town || nextExtensionMonth < 0 || State.month < nextExtensionMonth || extensionCount >= ExtensionCap) return;
            nextExtensionMonth = State.month + ExtensionEveryMonths;
            var kind = ExtensionOrder[extensionCount % ExtensionOrder.Length];
            extensionCount++;
            PlaceNew(kind, -1, null);
        }

        /// <summary>Market stalls on the green in the Town era (render-only, grows with the town's age).</summary>
        public int Stalls => era == Era.Town && Has(Kind.Market) ? Math.Min(5, 2 + (int)((State.month - eraMonth) / 60)) : 0;

        // ---------------- paths ----------------
        void WearPass()
        {
            var groups = new List<int>[4];
            for (int g = 0; g < 4; g++) groups[g] = new List<int>();
            for (int c = 0; c < SiteMap.N; c++) if (map.green[c] && map.lot[c] == 0) groups[0].Add(c);
            var homes = new List<Site>();
            foreach (var s in sites)
            {
                if (s.retired || s.doorCell < 0) continue;
                if (Encyclopedia.IsHome(s.kind)) homes.Add(s);
                switch (s.kind)
                {
                    case Kind.Farm: groups[1].Add(s.doorCell); break;
                    case Kind.Lumbermill: case Kind.Windmill: case Kind.Watermill: case Kind.Smithy: case Kind.Dyer: groups[2].Add(s.doorCell); break;
                    case Kind.Market: case Kind.Tavern: case Kind.Church: groups[3].Add(s.doorCell); break;
                }
            }
            var doors = new List<int>(homes.Count);
            foreach (var h in homes) doors.Add(h.doorCell);
            net.PrepareCosts();
            for (int g = 0; g < 4; g++)
                if (groups[g].Count > 0) Array.Copy(net.Flow(groups[g], false, doors), fields[g], SiteMap.N);
            for (int k = 0; k < homes.Count; k++)
            {
                int door = homes[k].doorCell;
                if (net.Trace(fields[0], door, tmpRoute)) net.Wear(tmpRoute, 1f);
                int work = (k % 3 != 2 && groups[1].Count > 0) || groups[2].Count == 0 ? 1 : 2;
                if (groups[work].Count > 0 && net.Trace(fields[work], door, tmpRoute)) net.Wear(tmpRoute, 0.8f);
                if (groups[3].Count > 0 && net.Trace(fields[3], door, tmpRoute)) net.Wear(tmpRoute, 0.5f);
            }
            net.UpdateTiers(era == Era.Town);
        }

        // ---------------- the Keeper's shaping ----------------
        /// <summary>Rain, grove or channel at a spot: edit the layers now, and tell SimCore (it decides the answer).</summary>
        public NudgeRec Shape(string kind, float x, float z)
        {
            x = GrowthTerrain.Clamp(x, -GrowthTerrain.HX + 0.1f, GrowthTerrain.HX - 0.1f);
            z = GrowthTerrain.Clamp(z, -GrowthTerrain.HZ + 0.1f, GrowthTerrain.HZ - 0.1f);
            if (kind == SimCore.NudgeKind.Rain) map.rains.Add(new Rain { x = x, z = z, month = State.month });
            else if (kind == SimCore.NudgeKind.Grove) PlantGrove(x, z);
            else if (kind == SimCore.NudgeKind.Channel) { if (!DigCanal(x, z)) return null; }
            else return null;
            // The Keeper's will beats the village's custom: commons under a shaping open up for the answer (Cluster only).
            for (int c = 0; c < SiteMap.N; c++)
                if (map.commons[c] && GrowthTerrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) < 0.7f) map.commons[c] = false;
            var n = core.PlaceNudge(kind, 0);
            var rec = new NudgeRec { coreId = n.id, kind = kind, x = x, z = z, month = State.month };
            nudges.Add(rec);
            lines.Add($"Year {Year}: the Keeper's {kind} at ({x:0.0}, {z:0.0}).");
            return rec;
        }

        /// <summary>The nearest point to (x, z) on dry land, inside the glass margin, with no lot or green within r.</summary>
        public bool OpenSpotNear(ref float x, ref float z, float r)
        {
            int best = -1; float bd = float.MaxValue;
            const float mx = GrowthTerrain.HX - GrowthTerrain.GlassMargin - 0.1f, mz = GrowthTerrain.HZ - GrowthTerrain.GlassMargin - 0.1f;
            for (int c = 0; c < SiteMap.N; c++)
            {
                float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
                if (MathF.Abs(cx) > mx || MathF.Abs(cz) > mz || map.Water(c) || map.slope[c] > 0.4f) continue;
                float d = GrowthTerrain.Dist(cx, cz, x, z);
                if (d >= bd) continue;
                bool clear = true;
                int rr = (int)MathF.Ceiling(r / SiteMap.Cell), i = SiteMap.I(c), j = SiteMap.J(c);
                for (int dj = -rr; dj <= rr && clear; dj++)
                    for (int di = -rr; di <= rr && clear; di++)
                    {
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                        if (di * di + dj * dj > rr * rr) continue;
                        int nc = SiteMap.Index(ni, nj);
                        if (map.lot[nc] != 0 || map.green[nc]) clear = false;
                    }
                if (clear) { bd = d; best = c; }
            }
            if (best < 0) return false;
            x = SiteMap.CXc(best); z = SiteMap.CZc(best);
            return true;
        }

        void PlantGrove(float x, float z)
        {
            for (int c = 0; c < SiteMap.N; c++)
            {
                float d = GrowthTerrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                if (d > 0.38f) continue;
                if (map.lot[c] != 0 || map.Water(c) || map.green[c] || map.laid[c] || map.tier[c] >= PathNet.PathTier) continue;
                map.forest[c] = 1;
            }
        }

        /// <summary>A channel from the pond to the spot, routed round lots and the green; bridges where it crosses a path.</summary>
        bool DigCanal(float x, float z)
        {
            int target = SiteMap.CellAt(x, z);
            if (target < 0 || map.Water(target)) return false;
            if (map.lot[target] != 0 || map.green[target])
            {
                // The spot is built on: dig to the nearest open ground instead.
                int best = -1; float bd = 9f;
                for (int c = 0; c < SiteMap.N; c++)
                {
                    if (map.lot[c] != 0 || map.green[c] || map.Water(c)) continue;
                    float d = GrowthTerrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                    if (d < bd) { bd = d; best = c; }
                }
                if (best < 0) return false;
                target = best;
            }
            var dist = new int[SiteMap.N];
            for (int c = 0; c < SiteMap.N; c++) dist[c] = PathNet.Inf;
            var open = new SortedSet<(int d, int c)>();
            for (int c = 0; c < SiteMap.N; c++) if (map.pond[c]) { dist[c] = 0; open.Add((0, c)); }
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
                    if (map.lot[nc] != 0 || map.green[nc]) continue;
                    int cost = 10 + (int)(map.slope[nc] * 60f) + (map.laid[nc] ? 25 : 0);
                    if (d + cost < dist[nc]) { dist[nc] = d + cost; open.Add((dist[nc], nc)); }
                }
            }
            if (dist[target] >= PathNet.Inf) return false;
            int cur = target, guard = 0;
            while (dist[cur] > 0 && guard++ < SiteMap.N)
            {
                map.canal[cur] = true;
                map.forest[cur] = 0;
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
            map.RecomputeWaterDist();
            return true;
        }

        // ---------------- snapshots ----------------
        void Snap()
        {
            if (!keepSnapshots) return;
            var s = new Snapshot
            {
                year = Year, month = State.month, era = era, population = State.population,
                tier = (byte[])map.tier.Clone(), flags = new byte[SiteMap.N], wear = (float[])map.wear.Clone(),
                stalls = Stalls,
            };
            for (int c = 0; c < SiteMap.N; c++)
            {
                byte f = 0;
                if (map.canal[c]) f |= Snapshot.FCanal;
                if (map.forest[c] != 0) f |= Snapshot.FForest;
                if (map.laid[c]) f |= Snapshot.FLaid;
                if (map.green[c]) f |= Snapshot.FGreen;
                if (map.pond[c]) f |= Snapshot.FPond;
                if (map.commons[c]) f |= Snapshot.FCommons;
                if (map.yard[c]) f |= Snapshot.FYard;
                s.flags[c] = f;
            }
            s.sites = new Site[sites.Count];
            for (int i = 0; i < sites.Count; i++) s.sites[i] = sites[i].Clone();
            s.nudges = new NudgeRec[nudges.Count];
            for (int i = 0; i < nudges.Count; i++) s.nudges[i] = nudges[i].Clone();
            s.clusters = new Cluster[clusters.Count];
            for (int i = 0; i < clusters.Count; i++) s.clusters[i] = clusters[i].Clone();
            for (int i = snapshotPlacementsFrom; i < placements.Count; i++) s.placements.Add(placements[i]);
            snapshotPlacementsFrom = placements.Count;
            s.lines.AddRange(lines);
            snapshots.Add(s);
        }

        /// <summary>A compact text fingerprint of everything placed (positions, paths, canals): live = catch-up check.</summary>
        public string Fingerprint()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(State.month).Append('|').Append(era).Append('|');
            foreach (var s in sites) sb.Append($"{s.id}:{s.kind}:{s.x:R},{s.z:R},{s.yaw:R},{s.w:R},{s.d:R},{s.cluster};");
            sb.Append('|');
            uint h = 2166136261;
            for (int c = 0; c < SiteMap.N; c++)
            {
                unchecked
                {
                    h = (h ^ map.tier[c]) * 16777619;
                    h = (h ^ (uint)(map.canal[c] ? 1 : 0)) * 16777619;
                    h = (h ^ (uint)BitConverter.SingleToInt32Bits(map.wear[c])) * 16777619;
                }
            }
            sb.Append(h);
            return sb.ToString();
        }
    }
}
