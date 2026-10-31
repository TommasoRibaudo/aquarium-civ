using System;
using System.Collections.Generic;

namespace AquariumCiv.Spikes.Growth
{
    /// <summary>A family cluster (Growth Lab round 2): 2–4 homes round a shared yard. Yards never move.</summary>
    public sealed class Cluster
    {
        public int id;
        public float x, z, r;        // the shared yard
        public int cap, count;       // how many homes the family builds here, how many stand
        public long foundedMonth, lastJoinMonth;
        public bool ringHeld;        // the land round the yard still waits for the family
        public Cluster Clone() => (Cluster)MemberwiseClone();
    }

    /// <summary>
    /// Round 2's "cluster village" (Haufendorf). Homes come in family clusters of 2–4 round a shared yard: a new home
    /// either joins a family whose yard has room, or founds a new yard a little way off. Yaw comes from facing the yard,
    /// the sun, the green or downhill, with ±20° of hashed jitter, never from the street. Setbacks and sizes vary.
    /// Clusters keep a gap between them (gardens, hedges, trees and woodpiles go there) and some commons stay open for
    /// good. Paths are not laid between clusters: they wear in from walking, as in round 1. Fields, mills, workshops,
    /// the market, tavern, church and shops keep round 1's siting rules (without house rows). Hash-based, no rng draws.
    /// </summary>
    public sealed class ClusterPlacer : IPlacer
    {
        // ---- tunables (growth-lab.md, Round 2 has the values that worked) ----
        public static float YardMin = 0.15f, YardMax = 0.24f;      // yard radius
        public static float ClusterGap = 0.26f;                    // home ↔ another family's buildings
        public static float OtherGap = 0.14f;                      // home ↔ any other lot (fields, workshops)
        public static float RelaxedGap = 0.1f;                     // second pass when nothing fits
        public static float YardSpacing = 1.0f;                    // between two yards
        public static float Jitter = 20f;                          // ± degrees on every yaw
        public static float GreenNear = 0.9f, GreenFar = 1.9f;     // yard distance band from the green
        public static float SunYaw = -35f;                         // the house front faces the low sun (the lab's light)
        public static float WJoin = 1.4f, WGreen = 1.0f, WNeighbour = 0.6f, WAccess = 0.4f, WSlope = 0.8f, WContour = 0.9f,
            WWater = 0.3f, WForest = 0.6f, WYaw = 0.35f, WTaste = 0.3f, WNudge = 4f;
        public static int CommonsCount = 3;
        public static float CommonsRMin = 0.24f, CommonsRMax = 0.32f;
        public static float SizeMin = 0.8f, SizeMax = 1.08f;            // × the encyclopedia's lot size
        public static float RingExtra = 0.3f;                      // land kept for the family round its yard until it is full
        public static int RingMonths = 8 * 12;                      // ...or until this long passes without a new home

        enum Yaw { Yard, Sun, Green, Contour }
        static readonly float[] YawPref = { 1f, 0.2f, 0f, 0.6f };

        readonly Placer inner = new() { homeRows = false };
        readonly List<int> network = new();
        readonly bool[] netMark = new bool[SiteMap.N];
        readonly int[] reach = new int[SiteMap.N];
        readonly List<int> cells = new();
        // per candidate: the yard it uses (a new one when cluster == -2), which yaw rule, and a taste hash
        readonly List<(float yx, float yz, float yr, Yaw yaw)> meta = new();

        public static bool Clustered(Kind k) => k == Kind.Hut || k == Kind.Cottage;

        public bool Place(GrowthWorld w, Site site, NudgeRec nudge, PlacementLog log)
        {
            // A family that stopped growing lets its neighbours have the land round its yard.
            foreach (var k in w.clusters)
                if (k.ringHeld && w.State.month - k.lastJoinMonth > RingMonths) ReleaseRing(w.map, k);
            if (!Clustered(site.kind)) return inner.Place(w, site, nudge, log) || Infill(w, site, nudge, log);

            // A family's house is its own size; the lot is the house and its doorstep (the gardens lie between the
            // clusters, not behind every house as in round 1's crofts).
            site.w *= SizeMin + (SizeMax - SizeMin) * GrowthTerrain.Hash3(w.seed, site.id, 13);
            site.d *= SizeMin - 0.04f + (SizeMax - SizeMin) * GrowthTerrain.Hash3(w.seed, site.id, 14);

            w.net.Network(network, netMark);
            Array.Copy(w.net.Flow(network), reach, SiteMap.N);
            var spec = Encyclopedia.Of(site.kind);
            var cands = log.cands;
            cands.Clear(); meta.Clear();
            Generate(w, site, nudge, cands);
            bool any = false;
            for (int i = 0; i < cands.Count; i++)
            {
                Evaluate(w, site, spec, nudge, cands[i], meta[i], i, true);
                any |= cands[i].valid;
            }
            // Nothing fits with the full gap between families: let them come closer (only the gap test changes).
            if (!any)
                for (int i = 0; i < cands.Count; i++)
                    if (cands[i].reject == "neighbours") Evaluate(w, site, spec, nudge, cands[i], meta[i], i, false);

            var order = new List<int>();
            for (int i = 0; i < cands.Count; i++) if (cands[i].valid) order.Add(i);
            order.Sort((a, b) =>
            {
                int s = cands[b].score.CompareTo(cands[a].score);
                if (s != 0) return s;
                return GrowthTerrain.Hash3(w.seed, site.id, a).CompareTo(GrowthTerrain.Hash3(w.seed, site.id, b));
            });
            foreach (int i in order)
            {
                var c = cands[i];
                if (!w.Claim(site, c.x, c.z, c.yaw, c.w, c.d, c.door, false)) { c.valid = false; c.reject = "cut off"; continue; }
                log.chosen = i;
                Join(w, site, c, meta[i]);
                return true;
            }
            // Nothing round a yard fits (a full box): round 1's frontage rules, no family; then any gap left.
            return inner.Place(w, site, nudge, log) || Infill(w, site, nudge, log);
        }

        /// <summary>The box is nearly full: try every free cell with four yaws (round 1's scan takes every third cell
        /// and one yaw, which misses the slivers a cluster village leaves). Best access and nearest the green wins.</summary>
        bool Infill(GrowthWorld w, Site site, NudgeRec nudge, PlacementLog log)
        {
            var map = w.map;
            var spec = Encyclopedia.Of(site.kind);
            w.net.Network(network, netMark);
            Array.Copy(w.net.Flow(network), reach, SiteMap.N);
            var cands = log.cands;
            cands.Clear();
            ClearRadius(map);
            float need = MathF.Min(site.w, site.d) / 2f - 0.06f; // the lot holds a disc this big round its centre
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (clear[c] < need) continue;
                if (map.lot[c] != 0 || map.Water(c) || map.green[c] || map.commons[c] || map.yard[c] || map.ring[c] != 0 || map.tier[c] >= PathNet.PathTier) continue;
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                float baseYaw = Placer.Facing(x, z, map.greenX, map.greenZ, 0, 0, 0).yaw;
                for (int q = 0; q < 4; q++)
                {
                    var cand = new Candidate { x = x, z = z, yaw = baseYaw + 90f * q + Jit(w, site, 9000 + c * 4 + q) * 0.5f, w = site.w, d = site.d, source = 6 };
                    cand.reject = inner.Check(w, site, spec, cand, reach);
                    if (cand.reject != null) continue;
                    cand.valid = true;
                    float acc = Placer.TermValue(Term.Access, w, site.kind, spec, nudge, x, z, cand.yaw, cand.w, cand.d, cand.door, reach);
                    float nud = Placer.TermValue(Term.Nudge, w, site.kind, spec, nudge, x, z, cand.yaw, cand.w, cand.d, cand.door, reach);
                    cand.score = acc - 0.3f * GrowthTerrain.Dist(x, z, map.greenX, map.greenZ) + WNudge * nud;
                    cands.Add(cand);
                }
            }
            // Best first; on "cut off" try the next (rare).
            var order = new List<int>();
            for (int i = 0; i < cands.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int s = cands[b].score.CompareTo(cands[a].score);
                return s != 0 ? s : a.CompareTo(b);
            });
            foreach (int i in order)
            {
                var cand = cands[i];
                if (w.Claim(site, cand.x, cand.z, cand.yaw, cand.w, cand.d, cand.door, false)) { log.chosen = i; return true; }
                cand.valid = false; cand.reject = "cut off";
            }
            return false;
        }

        void Join(GrowthWorld w, Site site, Candidate c, (float yx, float yz, float yr, Yaw yaw) m)
        {
            Cluster k;
            if (c.cluster == -3) return; // a lone house
            if (c.cluster >= 0) k = w.clusters[c.cluster - 1];
            else
            {
                k = new Cluster
                {
                    id = w.clusters.Count + 1, x = m.yx, z = m.yz, r = m.yr, foundedMonth = w.State.month,
                    ringHeld = true,
                };
                float h = GrowthTerrain.Hash3(w.seed, site.id, 41);
                k.cap = h < 0.4f ? 2 : h < 0.85f ? 3 : 4;
                w.clusters.Add(k);
                // The yard stays open: no lot may take it (paths may cross it). The land round it waits for the family.
                var map = w.map;
                for (int cell = 0; cell < SiteMap.N; cell++)
                {
                    if (map.Water(cell) || map.lot[cell] != 0) continue;
                    float d = GrowthTerrain.Dist(SiteMap.CXc(cell), SiteMap.CZc(cell), k.x, k.z);
                    if (d < k.r * 0.8f) map.yard[cell] = true;
                    else if (d < k.r + RingExtra && map.ring[cell] == 0) map.ring[cell] = k.id;
                }
            }
            k.count++;
            k.lastJoinMonth = w.State.month;
            site.cluster = k.id;
            if (k.count >= k.cap) ReleaseRing(w.map, k);
        }

        static void ReleaseRing(SiteMap map, Cluster k)
        {
            k.ringHeld = false;
            for (int cell = 0; cell < SiteMap.N; cell++) if (map.ring[cell] == k.id) map.ring[cell] = 0;
        }

        /// <summary>Half the rect's extent along the unit direction (ux, uz).</summary>
        static float Extent(float yaw, float w, float d, float ux, float uz)
        {
            float r = yaw * (MathF.PI / 180f), cs = MathF.Cos(r), sn = MathF.Sin(r);
            return w / 2f * MathF.Abs(ux * cs - uz * sn) + d / 2f * MathF.Abs(ux * sn + uz * cs);
        }

        int Capacity(GrowthWorld w, Cluster k, Kind kind) => k.cap + (kind == Kind.Cottage && w.era == Era.Town ? 1 : 0);

        // ---------------- candidates ----------------
        void Generate(GrowthWorld w, Site site, NudgeRec nudge, List<Candidate> into)
        {
            var map = w.map;
            float lw = site.w, ld = site.d;

            // J: join a family whose yard has room, round its edge.
            foreach (var k in w.clusters)
            {
                if (k.count >= Capacity(w, k, site.kind)) continue;
                const int n = 18;
                float off = GrowthTerrain.Hash3(w.seed, k.id, 7);
                for (int a = 0; a < n; a++)
                {
                    float ang = (a + off) / n * MathF.PI * 2f, ux = MathF.Cos(ang), uz = MathF.Sin(ang);
                    float setback = 0.02f + 0.07f * GrowthTerrain.Hash3(w.seed, site.id, 100 + a);
                    for (int o = 0; o < 4; o++)
                    {
                        var rule = (Yaw)o;
                        float x = k.x + ux * (k.r + ld / 2f + setback), z = k.z + uz * (k.r + ld / 2f + setback);
                        if (!YawFor(w, rule, x, z, k.x, k.z, out float yaw)) continue;
                        yaw += Jit(w, site, a * 4 + o);
                        float ext = Extent(yaw, lw, ld, ux, uz);
                        x = k.x + ux * (k.r + ext + setback); z = k.z + uz * (k.r + ext + setback);
                        into.Add(new Candidate { x = x, z = z, yaw = yaw, w = lw, d = ld, cluster = k.id, source = 10 });
                        meta.Add((k.x, k.z, k.r, rule));
                    }
                }
            }

            // F: found a new yard, on rings round the green (and round the Keeper's shaping while it waits).
            float yr = YardMin + (YardMax - YardMin) * GrowthTerrain.Hash3(w.seed, site.id, 31);
            var spots = new List<(float x, float z, float spacing)>();
            for (int ring = 0; ring < 6; ring++)
            {
                float r = 0.8f + ring * 0.28f;
                const int n = 12;
                float off = 0.5f * (ring & 1) + GrowthTerrain.Hash3(w.seed, ring, 3);
                for (int a = 0; a < n; a++)
                {
                    float ang = (a + off) / n * MathF.PI * 2f;
                    spots.Add((map.greenX + MathF.Cos(ang) * r, map.greenZ + MathF.Sin(ang) * r, YardSpacing));
                }
            }
            for (int s = 0; s < spots.Count; s++)
            {
                var (px, pz, spacing) = spots[s];
                if (!OpenYard(w, px, pz, yr, spacing)) continue;
                // The house stands on the far side of its yard from the green (the yard opens towards the village),
                // turned by a family's whim.
                float gx = px - map.greenX, gz = pz - map.greenZ, gl = MathF.Sqrt(gx * gx + gz * gz) + 1e-6f;
                float baseAng = MathF.Atan2(gz / gl, gx / gl);
                for (int h = 0; h < 2; h++)
                {
                    float turn = (h == 0 ? -1f : 1f) * (0.3f + 0.8f * GrowthTerrain.Hash3(w.seed, site.id, 500 + s * 2 + h));
                    float ang = baseAng + turn, ux = MathF.Cos(ang), uz = MathF.Sin(ang);
                    var alt = (Yaw)(1 + (int)(GrowthTerrain.Hash3(w.seed, site.id, 700 + s) * 3f) % 3);
                    foreach (var rule in new[] { Yaw.Yard, alt })
                    {
                        float setback = 0.02f + 0.07f * GrowthTerrain.Hash3(w.seed, site.id, 900 + s * 2 + h);
                        float x = px + ux * (yr + ld / 2f + setback), z = pz + uz * (yr + ld / 2f + setback);
                        if (!YawFor(w, rule, x, z, px, pz, out float yaw)) continue;
                        yaw += Jit(w, site, 2000 + s * 4 + h * 2 + (int)rule);
                        float ext = Extent(yaw, lw, ld, ux, uz);
                        x = px + ux * (yr + ext + setback); z = pz + uz * (yr + ext + setback);
                        into.Add(new Candidate { x = x, z = z, yaw = yaw, w = lw, d = ld, cluster = -2, source = 11 });
                        meta.Add((px, pz, yr, rule));
                    }
                }
            }

            // E: answering the Keeper: the house goes right by the shaped spot, its new yard in front of it.
            if (nudge != null)
                for (int ring = 0; ring < 4; ring++)
                    for (int a = 0; a < 12; a++)
                    {
                        float ang = (a + 0.5f * (ring & 1)) / 12f * MathF.PI * 2f, r = 0.1f + 0.15f * ring;
                        float x = nudge.x + MathF.Cos(ang) * r, z = nudge.z + MathF.Sin(ang) * r;
                        float face = Placer.Facing(x, z, map.greenX, map.greenZ, 0, 0, 0).yaw;
                        for (int q = 0; q < 4; q++)
                        {
                            float yaw = face + 90f * q + Jit(w, site, 6000 + ring * 48 + a * 4 + q);
                            float rr = yaw * (MathF.PI / 180f), fx = -MathF.Sin(rr), fz = -MathF.Cos(rr);
                            float yx = x + fx * (ld / 2f + yr + 0.03f), yz = z + fz * (ld / 2f + yr + 0.03f);
                            // No room for a yard (by the pond, by the glass): a lone house still answers.
                            bool yard = OpenYard(w, yx, yz, yr, YardSpacing * 0.5f);
                            into.Add(new Candidate { x = x, z = z, yaw = yaw, w = lw, d = ld, cluster = yard ? -2 : -3, source = 4 });
                            meta.Add((yx, yz, yard ? yr : 0f, Yaw.Yard));
                        }
                    }
        }

        readonly float[] clear = new float[SiteMap.N];

        /// <summary>Chamfer distance from every cell to the nearest cell no lot may cover (or the glass margin).</summary>
        void ClearRadius(SiteMap map)
        {
            const float mx = GrowthTerrain.HX - GrowthTerrain.GlassMargin, mz = GrowthTerrain.HZ - GrowthTerrain.GlassMargin;
            for (int c = 0; c < SiteMap.N; c++)
            {
                bool blocked = map.lot[c] != 0 || map.Water(c) || map.green[c] || map.commons[c] || map.yard[c] || map.ring[c] != 0
                               || map.laid[c] || map.tier[c] >= PathNet.PathTier
                               || MathF.Abs(SiteMap.CXc(c)) > mx || MathF.Abs(SiteMap.CZc(c)) > mz;
                clear[c] = blocked ? 0f : 99f;
            }
            const float o = SiteMap.Cell, dg = SiteMap.Cell * 1.41421356f;
            for (int j = 0; j < SiteMap.NZ; j++)
                for (int i = 0; i < SiteMap.NX; i++)
                {
                    int c = SiteMap.Index(i, j); float v = clear[c];
                    if (i > 0) v = MathF.Min(v, clear[c - 1] + o);
                    if (j > 0) v = MathF.Min(v, clear[c - SiteMap.NX] + o);
                    if (i > 0 && j > 0) v = MathF.Min(v, clear[c - SiteMap.NX - 1] + dg);
                    if (i < SiteMap.NX - 1 && j > 0) v = MathF.Min(v, clear[c - SiteMap.NX + 1] + dg);
                    clear[c] = v;
                }
            for (int j = SiteMap.NZ - 1; j >= 0; j--)
                for (int i = SiteMap.NX - 1; i >= 0; i--)
                {
                    int c = SiteMap.Index(i, j); float v = clear[c];
                    if (i < SiteMap.NX - 1) v = MathF.Min(v, clear[c + 1] + o);
                    if (j < SiteMap.NZ - 1) v = MathF.Min(v, clear[c + SiteMap.NX] + o);
                    if (i < SiteMap.NX - 1 && j < SiteMap.NZ - 1) v = MathF.Min(v, clear[c + SiteMap.NX + 1] + dg);
                    if (i > 0 && j < SiteMap.NZ - 1) v = MathF.Min(v, clear[c + SiteMap.NX - 1] + dg);
                    clear[c] = v;
                }
        }

        static float Jit(GrowthWorld w, Site site, int k) => (GrowthTerrain.Hash3(w.seed, site.id, 3000 + k) - 0.5f) * 2f * Jitter;

        /// <summary>The yaw a rule gives a house at (x, z) beside the yard at (yx, yz). False if the rule doesn't apply here.</summary>
        static bool YawFor(GrowthWorld w, Yaw rule, float x, float z, float yx, float yz, out float yaw)
        {
            var map = w.map;
            yaw = 0f;
            switch (rule)
            {
                case Yaw.Yard: yaw = Placer.Facing(x, z, yx, yz, 0, 0, 0).yaw; return true;
                case Yaw.Sun: yaw = SunYaw; return true;
                case Yaw.Green: yaw = Placer.Facing(x, z, map.greenX, map.greenZ, 0, 0, 0).yaw; return true;
                case Yaw.Contour:
                {
                    Gradient(map, x, z, out float gx, out float gz);
                    float s = MathF.Sqrt(gx * gx + gz * gz);
                    if (s < 0.12f) return false;
                    // Front faces downhill: the long side runs along the contour.
                    yaw = MathF.Atan2(gx / s, gz / s) * (180f / MathF.PI);
                    return true;
                }
            }
            return false;
        }

        static void Gradient(SiteMap map, float x, float z, out float gx, out float gz)
        {
            const float e = 0.08f;
            gx = (GrowthTerrain.Height(map.spec, x + e, z) - GrowthTerrain.Height(map.spec, x - e, z)) / (2 * e);
            gz = (GrowthTerrain.Height(map.spec, x, z + e) - GrowthTerrain.Height(map.spec, x, z - e)) / (2 * e);
        }

        /// <summary>A new yard may go here: dry, inside the glass margin, off the green and the commons, no lot on it,
        /// and far enough from the other yards.</summary>
        bool OpenYard(GrowthWorld w, float x, float z, float r, float spacing)
        {
            var map = w.map;
            const float mx = GrowthTerrain.HX - GrowthTerrain.GlassMargin - 0.15f, mz = GrowthTerrain.HZ - GrowthTerrain.GlassMargin - 0.15f;
            if (MathF.Abs(x) > mx || MathF.Abs(z) > mz) return false;
            foreach (var k in w.clusters)
                if (GrowthTerrain.Dist(k.x, k.z, x, z) < spacing) return false;
            SiteMap.RectCells(x, z, 0f, r * 1.6f, r * 1.6f, 0f, cells);
            foreach (int c in cells)
                if (map.Water(c) || map.green[c] || map.commons[c] || map.yard[c] || map.ring[c] != 0 || map.lot[c] != 0 || map.slope[c] > 0.5f) return false;
            return true;
        }

        // ---------------- constraints + score ----------------
        void Evaluate(GrowthWorld w, Site site, KindSpec spec, NudgeRec nudge, Candidate c, (float yx, float yz, float yr, Yaw yaw) m, int idx, bool strict)
        {
            c.valid = false; c.f = null;
            c.reject = ClusterCheck(w, c, m, strict) ?? inner.Check(w, site, spec, c, reach);
            if (c.reject != null) return;
            c.valid = true;
            var map = w.map;
            var f = c.f = new float[Enum.GetValues(typeof(Term)).Length];
            float sum = 0f;

            // The family's yard sits in a band round the green (a nucleated village of hamlets, not scattered farms).
            float dg = GrowthTerrain.Dist(m.yx, m.yz, map.greenX, map.greenZ);
            float green = dg < GreenNear ? 1f - 2f * (GreenNear - dg) / (GreenNear - SiteMap.GreenR + 0.01f)
                : dg > GreenFar ? 1f - 2f * (dg - GreenFar) / 1.2f : 1f;
            f[(int)Term.Green] = green = GrowthTerrain.Clamp(green, -1f, 1f);
            sum += WGreen * green;

            // (an answer to the Keeper goes where the land was shaped, family or not)
            if (c.cluster >= 0) { if (nudge == null) sum += WJoin; }
            else
            {
                // A new yard keeps near the others (but never on top: OpenYard checks the spacing).
                float best = 99f;
                foreach (var k in w.clusters) best = MathF.Min(best, GrowthTerrain.Dist(k.x, k.z, m.yx, m.yz));
                float nb = best > 50f ? 0f : best <= 1.6f ? 1f : GrowthTerrain.Clamp(1f - (best - 1.6f) / 0.8f, -1f, 1f);
                f[(int)Term.Social] = nb;
                sum += WNeighbour * nb;
            }

            float acc = Placer.TermValue(Term.Access, w, site.kind, spec, nudge, c.x, c.z, c.yaw, c.w, c.d, c.door, reach);
            // Slope, water and woods from one pass over the lot's cells (round 1's terms, same shapes).
            SiteMap.RectCells(c.x, c.z, c.yaw, c.w, c.d, 0f, cells);
            float sl = 0f, wd = 99f; int trees = 0;
            foreach (int k in cells) { sl += map.slope[k]; wd = MathF.Min(wd, map.waterDist[k]); if (map.forest[k] != 0) trees++; }
            int nc = Math.Max(1, cells.Count);
            float slope = GrowthTerrain.Clamp(1f - 2f * (sl / nc) / Math.Max(0.05f, spec.maxSlope), -1f, 1f);
            float water = Encyclopedia.WaterTerm(site.kind, wd);
            float forest = -2f * trees / nc;
            float nud = Placer.TermValue(Term.Nudge, w, site.kind, spec, nudge, c.x, c.z, c.yaw, c.w, c.d, c.door, reach);
            f[(int)Term.Access] = acc; f[(int)Term.Slope] = slope; f[(int)Term.Water] = water; f[(int)Term.Forest] = forest; f[(int)Term.Nudge] = nud;
            sum += WAccess * acc + WSlope * slope + WWater * water + WForest * forest + WNudge * nud;

            // On a slope the long side follows the contour (the front faces up or down the hill).
            Gradient(map, c.x, c.z, out float gx, out float gz);
            float s = MathF.Sqrt(gx * gx + gz * gz);
            if (s > 0.12f)
            {
                float r = c.yaw * (MathF.PI / 180f), fx = -MathF.Sin(r), fz = -MathF.Cos(r);
                float align = MathF.Abs(fx * gx + fz * gz) / s;
                float contour = (2f * align - 1f) * MathF.Min(1f, s / 0.4f);
                f[(int)Term.Height] = contour; // shown as "Height" in the overlay's term list
                sum += WContour * contour;
            }

            sum += WYaw * YawPref[(int)m.yaw];
            sum += WTaste * (GrowthTerrain.Hash3(w.seed, site.id, 5000 + idx) - 0.5f) * 2f;

            // Keep clear of a shaping that waits for its answer (round 1's term).
            if (nudge == null)
                foreach (var n in w.nudges)
                {
                    if (n.siteId >= 0 || w.State.month - n.month >= AquariumCiv.Sim.SimCore.NudgeMonths) continue;
                    float dn = GrowthTerrain.Dist(c.x, c.z, n.x, n.z);
                    if (dn < Placer.KeepClearRadius) sum -= Placer.KeepClearWeight * (1f - dn / Placer.KeepClearRadius);
                }
            c.score = sum;
        }

        /// <summary>The family rules: the yard stays open, other families keep their distance.</summary>
        static string ClusterCheck(GrowthWorld w, Candidate c, (float yx, float yz, float yr, Yaw yaw) m, bool strict)
        {
            // No house on a yard (its own included; a new yard isn't marked on the map yet).
            if (m.yr > 0f && PointRectDist(m.yx, m.yz, c) < m.yr * 0.85f) return "yard";
            foreach (var k in w.clusters)
                if (PointRectDist(k.x, k.z, c) < k.r * 0.85f) return "yard";
            if (c.cluster == -2)
                foreach (var o in w.sites)
                {
                    if (!GrowthChecks.HasLot(o.kind) || o.retired) continue;
                    if (PointRectDist(m.yx, m.yz, o.x, o.z, o.yaw, o.w, o.d) < m.yr) return "yard";
                }
            float gapCluster = strict ? ClusterGap : RelaxedGap, gapOther = strict ? OtherGap : RelaxedGap;
            foreach (var o in w.sites)
            {
                if (!GrowthChecks.HasLot(o.kind) || o.retired) continue;
                if (o.cluster >= 0 && o.cluster == c.cluster) continue;
                float need = o.cluster >= 0 ? gapCluster : gapOther;
                float dx = o.x - c.x, dz = o.z - c.z, rr = 0.5f * (MathF.Sqrt(o.w * o.w + o.d * o.d) + MathF.Sqrt(c.w * c.w + c.d * c.d)) + need;
                if (dx * dx + dz * dz > rr * rr) continue;
                if (SiteMap.RectGap(o.x, o.z, o.yaw, o.w, o.d, c.x, c.z, c.yaw, c.w, c.d) < need) return "neighbours";
            }
            return null;
        }

        static float PointRectDist(float px, float pz, Candidate c) => PointRectDist(px, pz, c.x, c.z, c.yaw, c.w, c.d);

        public static float PointRectDist(float px, float pz, float x, float z, float yaw, float w, float d)
        {
            float r = yaw * (MathF.PI / 180f), cs = MathF.Cos(r), sn = MathF.Sin(r);
            float dx = px - x, dz = pz - z;
            float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
            float ox = MathF.Max(0f, MathF.Abs(lx) - w / 2f), oz = MathF.Max(0f, MathF.Abs(lz) - d / 2f);
            return MathF.Sqrt(ox * ox + oz * oz);
        }

        // ---------------- commons ----------------
        /// <summary>Some land stays common for good: a meadow or orchard near the village and a couple further out.</summary>
        public static void ReserveCommons(GrowthWorld w)
        {
            var map = w.map;
            var placed = new List<(float x, float z)>();
            for (int tries = 0; placed.Count < CommonsCount && tries < 300; tries++)
            {
                float ang = GrowthTerrain.Hash3(w.seed, 900, tries) * MathF.PI * 2f;
                float r = (placed.Count == 0 ? 0.95f : 1.25f) + 0.9f * GrowthTerrain.Hash3(w.seed, 901, tries);
                float rad = CommonsRMin + (CommonsRMax - CommonsRMin) * GrowthTerrain.Hash3(w.seed, 902, tries);
                float x = map.greenX + MathF.Cos(ang) * r, z = map.greenZ + MathF.Sin(ang) * r;
                const float mx = GrowthTerrain.HX - GrowthTerrain.GlassMargin, mz = GrowthTerrain.HZ - GrowthTerrain.GlassMargin;
                if (MathF.Abs(x) > mx - rad * 0.5f || MathF.Abs(z) > mz - rad * 0.5f) continue;
                int c0 = SiteMap.CellAt(x, z);
                if (c0 < 0 || map.waterDist[c0] < rad + 0.1f || map.slope[c0] > 0.35f || map.forest[c0] != 0) continue;
                if (GrowthTerrain.Dist(x, z, map.greenX, map.greenZ) < SiteMap.GreenR + rad + 0.35f) continue;
                bool far = true;
                foreach (var p in placed) if (GrowthTerrain.Dist(p.x, p.z, x, z) < 1.3f) far = false;
                if (!far) continue;
                placed.Add((x, z));
                for (int c = 0; c < SiteMap.N; c++)
                    if (!map.Water(c) && GrowthTerrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) < rad) map.commons[c] = true;
            }
        }
    }
}
