using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>
    /// The cluster village (Haufendorf, Growth Lab round 2; decided 2026-10-29). Homes come in families of 2–4 round a
    /// shared yard: a new home either joins a family whose yard has room, or founds a new yard a little way off. Yaw
    /// comes from facing the yard, the sun, the green or downhill, with ±20° of hashed jitter, never from a street.
    /// Families keep a gap between them, some commons stay open for good, paths between families wear in from walking.
    /// Each family gets a barn or shed across its yard from the village era on (<see cref="PlaceOutbuilding"/>). When
    /// nothing round a yard fits, a fine infill takes any gap left. Hash-based: no dice.
    /// </summary>
    public sealed class ClusterPlacer
    {
        // ---- tunables (growth-lab.md Round 2 has the values that worked) ----
        public static float YardMin = 0.15f, YardMax = 0.24f;
        public static float ClusterGap = 0.26f, OtherGap = 0.14f, RelaxedGap = 0.1f;
        public static float YardSpacing = 1.2f;
        public static float Jitter = 20f;
        public static float GreenNear = 0.9f, GreenFar = 1.9f;
        public static float SunYaw = -35f;
        public static float WJoin = 1.4f, WGreen = 1.0f, WNeighbour = 0.6f, WAccess = 0.4f, WSlope = 0.8f, WContour = 0.9f,
            WWater = 0.3f, WForest = 0.6f, WYaw = 0.35f, WTaste = 0.3f, WNudge = 4f;
        public static int CommonsCount = 3;
        public static float CommonsRMin = 0.24f, CommonsRMax = 0.32f;
        public static float SizeMin = 0.8f, SizeMax = 1.08f;
        public static float RingExtra = 0.6f;
        /// <summary>The land round a yard waits this long for the family's next home. Longer than round 2's 8 years:
        /// the slice builds a hut every ~10–15 years, so families otherwise lost their land before they grew.</summary>
        public static int RingMonths = 15 * 12;

        enum Yaw { Yard, Sun, Green, Contour }
        static readonly float[] YawPref = { 1f, 0.2f, 0f, 0.6f };

        readonly Settlement w;
        readonly Placer inner;
        readonly List<int> network = new List<int>();
        readonly bool[] netMark = new bool[SiteMap.N];
        readonly int[] reach = new int[SiteMap.N];
        readonly List<int> cells = new List<int>(), order = new List<int>();
        readonly List<Candidate> cands = new List<Candidate>();
        readonly List<(float yx, float yz, float yr, Yaw yaw)> meta = new List<(float, float, float, Yaw)>();
        readonly List<(float x, float z)> spots = new List<(float, float)>();
        readonly float[] clear = new float[SiteMap.N];

        public ClusterPlacer(Settlement settlement, Placer roundOne) { w = settlement; inner = roundOne; }

        public static bool Clustered(LotKind k) => k == LotKind.Hut || k == LotKind.Cottage;

        /// <summary>A family that stopped growing lets its neighbours have the land round its yard.</summary>
        public void ReleaseStaleRings()
        {
            foreach (var k in w.Yards)
                if (k.ringHeld && w.Month - k.lastJoinMonth > RingMonths) ReleaseRing(k);
        }

        public bool Place(Lot site, Nudge nudge)
        {
            ReleaseStaleRings();
            if (!Clustered(site.kind)) return inner.Place(site, nudge) || Infill(site, nudge);

            // A family's house is its own size; the gardens lie between the families, not behind every house.
            site.w *= SizeMin + (SizeMax - SizeMin) * Terrain.Hash3(w.Seed, site.id, 13);
            site.d *= SizeMin - 0.04f + (SizeMax - SizeMin) * Terrain.Hash3(w.Seed, site.id, 14);

            w.Net.Network(network, netMark);
            Array.Copy(w.Net.Flow(network), reach, SiteMap.N);
            var spec = Encyclopedia.Of(site.kind);
            cands.Clear(); meta.Clear();
            Generate(site, nudge);
            bool any = false;
            for (int i = 0; i < cands.Count; i++)
            {
                Evaluate(site, spec, nudge, cands[i], meta[i], i, true);
                any |= cands[i].valid;
            }
            // Nothing fits with the full gap between families: let them come closer (only the gap test changes).
            if (!any)
                for (int i = 0; i < cands.Count; i++)
                    if (cands[i].reject == "neighbours") Evaluate(site, spec, nudge, cands[i], meta[i], i, false);

            order.Clear();
            for (int i = 0; i < cands.Count; i++) if (cands[i].valid) order.Add(i);
            int seed = w.Seed, id = site.id;
            order.Sort((a, b) =>
            {
                int s = cands[b].score.CompareTo(cands[a].score);
                if (s != 0) return s;
                return Terrain.Hash3(seed, id, a).CompareTo(Terrain.Hash3(seed, id, b));
            });
            foreach (int i in order)
            {
                var c = cands[i];
                if (!w.Claim(site, c.x, c.z, c.yaw, c.w, c.d, c.door, false)) { c.valid = false; c.reject = "cut off"; continue; }
                Join(site, c, meta[i]);
                return true;
            }
            // Nothing round a yard fits (a full box): round 1's frontage rules, no family; then any gap left.
            return inner.Place(site, nudge) || Infill(site, nudge);
        }

        /// <summary>The box is nearly full: try every free cell with four yaws. Best access and nearest the green wins.</summary>
        public bool Infill(Lot site, Nudge nudge)
        {
            var map = w.Map;
            var spec = Encyclopedia.Of(site.kind);
            w.Net.Network(network, netMark);
            Array.Copy(w.Net.Flow(network), reach, SiteMap.N);
            cands.Clear();
            ClearRadius(map);
            float need = MathF.Min(site.w, site.d) / 2f - 0.06f; // the lot holds a disc this big round its centre
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (clear[c] < need) continue;
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                float baseYaw = Placer.FacingYaw(x, z, map.greenX, map.greenZ);
                for (int q = 0; q < 4; q++)
                {
                    var cand = new Candidate { x = x, z = z, yaw = baseYaw + 90f * q + Jit(site, 9000 + c * 4 + q) * 0.5f, w = site.w, d = site.d, source = 6 };
                    cand.reject = inner.Check(site, spec, cand, reach);
                    if (cand.reject != null) continue;
                    cand.valid = true;
                    float acc = Placer.TermValue(Term.Access, w, site.kind, spec, nudge, x, z, cand.yaw, cand.w, cand.d, cand.door, reach);
                    float nud = Placer.TermValue(Term.Nudge, w, site.kind, spec, nudge, x, z, cand.yaw, cand.w, cand.d, cand.door, reach);
                    cand.score = acc - 0.3f * Terrain.Dist(x, z, map.greenX, map.greenZ) + WNudge * nud;
                    cands.Add(cand);
                }
            }
            order.Clear();
            for (int i = 0; i < cands.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int s = cands[b].score.CompareTo(cands[a].score);
                return s != 0 ? s : a.CompareTo(b);
            });
            foreach (int i in order)
            {
                var cand = cands[i];
                if (w.Claim(site, cand.x, cand.z, cand.yaw, cand.w, cand.d, cand.door, false)) return true;
                cand.valid = false; cand.reject = "cut off";
            }
            return false;
        }

        void Join(Lot site, Candidate c, (float yx, float yz, float yr, Yaw yaw) m)
        {
            Yard k;
            if (c.yard == -3) return; // a lone house
            if (c.yard > 0) k = w.Yards[c.yard - 1];
            else
            {
                k = new Yard { id = w.Yards.Count + 1, x = m.yx, z = m.yz, r = m.yr, foundedMonth = w.Month, ringHeld = true };
                float h = Terrain.Hash3(w.Seed, site.id, 41);
                k.cap = h < 0.35f ? 2 : h < 0.8f ? 3 : 4;
                w.Yards.Add(k);
                // The yard stays open: no lot may take it (paths may cross it). The land round it waits for the family.
                var map = w.Map;
                for (int cell = 0; cell < SiteMap.N; cell++)
                {
                    if (map.Water(cell) || map.lot[cell] != 0) continue;
                    float d = Terrain.Dist(SiteMap.CXc(cell), SiteMap.CZc(cell), k.x, k.z);
                    if (d < k.r * 0.8f) map.Set(cell, SiteMap.FYard, true);
                    else if (d < k.r + RingExtra && map.ring[cell] == 0) map.ring[cell] = k.id;
                }
            }
            k.count++;
            k.lastJoinMonth = w.Month;
            site.yard = k.id;
            if (k.count >= Capacity(k, site.kind)) ReleaseRing(k);
        }

        void ReleaseRing(Yard k)
        {
            k.ringHeld = false;
            var ring = w.Map.ring;
            for (int cell = 0; cell < SiteMap.N; cell++) if (ring[cell] == k.id) ring[cell] = 0;
        }

        static float Extent(float yaw, float w, float d, float ux, float uz)
        {
            float r = yaw * (MathF.PI / 180f), cs = MathF.Cos(r), sn = MathF.Sin(r);
            return w / 2f * MathF.Abs(ux * cs - uz * sn) + d / 2f * MathF.Abs(ux * sn + uz * cs);
        }

        int Capacity(Yard k, LotKind kind) => k.cap + (kind == LotKind.Cottage && w.Era == Era.Town ? 1 : 0);

        // ---------------- candidates ----------------
        void Generate(Lot site, Nudge nudge)
        {
            var map = w.Map;
            float lw = site.w, ld = site.d;

            // J: join a family whose yard has room, round its edge.
            foreach (var k in w.Yards)
            {
                if (k.count >= Capacity(k, site.kind)) continue;
                const int n = 18;
                float off = Terrain.Hash3(w.Seed, k.id, 7);
                for (int a = 0; a < n; a++)
                {
                    float ang = (a + off) / n * MathF.PI * 2f, ux = MathF.Cos(ang), uz = MathF.Sin(ang);
                    // Two setbacks per direction: by the yard, or a garden's width back (more room in a tight village).
                    for (int o = 0; o < 8; o++)
                    {
                        var rule = (Yaw)(o & 3);
                        float setback = 0.02f + 0.07f * Terrain.Hash3(w.Seed, site.id, 100 + a) + (o >= 4 ? 0.14f : 0f);
                        float x = k.x + ux * (k.r + ld / 2f + setback), z = k.z + uz * (k.r + ld / 2f + setback);
                        if (!YawFor(rule, x, z, k.x, k.z, out float yaw)) continue;
                        yaw += Jit(site, a * 4 + o);
                        float ext = Extent(yaw, lw, ld, ux, uz);
                        x = k.x + ux * (k.r + ext + setback); z = k.z + uz * (k.r + ext + setback);
                        cands.Add(new Candidate { x = x, z = z, yaw = yaw, w = lw, d = ld, yard = k.id, source = 10 });
                        meta.Add((k.x, k.z, k.r, rule));
                    }
                }
            }

            // F: found a new yard, on rings round the green.
            float yr = YardMin + (YardMax - YardMin) * Terrain.Hash3(w.Seed, site.id, 31);
            spots.Clear();
            for (int ring = 0; ring < 6; ring++)
            {
                float r = 0.8f + ring * 0.28f;
                const int n = 12;
                float off = 0.5f * (ring & 1) + Terrain.Hash3(w.Seed, ring, 3);
                for (int a = 0; a < n; a++)
                {
                    float ang = (a + off) / n * MathF.PI * 2f;
                    spots.Add((map.greenX + MathF.Cos(ang) * r, map.greenZ + MathF.Sin(ang) * r));
                }
            }
            for (int s = 0; s < spots.Count; s++)
            {
                var (px, pz) = spots[s];
                if (!OpenYard(px, pz, yr, YardSpacing)) continue;
                // The house stands on the far side of its yard from the green (the yard opens towards the village),
                // turned by a family's whim.
                float gx = px - map.greenX, gz = pz - map.greenZ, gl = MathF.Sqrt(gx * gx + gz * gz) + 1e-6f;
                float baseAng = MathF.Atan2(gz / gl, gx / gl);
                for (int h = 0; h < 2; h++)
                {
                    float turn = (h == 0 ? -1f : 1f) * (0.3f + 0.8f * Terrain.Hash3(w.Seed, site.id, 500 + s * 2 + h));
                    float ang = baseAng + turn, ux = MathF.Cos(ang), uz = MathF.Sin(ang);
                    var alt = (Yaw)(1 + (int)(Terrain.Hash3(w.Seed, site.id, 700 + s) * 3f) % 3);
                    for (int pass = 0; pass < 2; pass++)
                    {
                        var rule = pass == 0 ? Yaw.Yard : alt;
                        float setback = 0.02f + 0.07f * Terrain.Hash3(w.Seed, site.id, 900 + s * 2 + h);
                        float x = px + ux * (yr + ld / 2f + setback), z = pz + uz * (yr + ld / 2f + setback);
                        if (!YawFor(rule, x, z, px, pz, out float yaw)) continue;
                        yaw += Jit(site, 2000 + s * 4 + h * 2 + (int)rule);
                        float ext = Extent(yaw, lw, ld, ux, uz);
                        x = px + ux * (yr + ext + setback); z = pz + uz * (yr + ext + setback);
                        cands.Add(new Candidate { x = x, z = z, yaw = yaw, w = lw, d = ld, yard = -2, source = 11 });
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
                        float face = Placer.FacingYaw(x, z, map.greenX, map.greenZ);
                        for (int q = 0; q < 4; q++)
                        {
                            float yaw = face + 90f * q + Jit(site, 6000 + ring * 48 + a * 4 + q);
                            float rr = yaw * (MathF.PI / 180f), fx = -MathF.Sin(rr), fz = -MathF.Cos(rr);
                            float yx = x + fx * (ld / 2f + yr + 0.03f), yz = z + fz * (ld / 2f + yr + 0.03f);
                            // No room for a yard (by the pond, by the glass): a lone house still answers.
                            bool yard = OpenYard(yx, yz, yr, YardSpacing * 0.5f);
                            cands.Add(new Candidate { x = x, z = z, yaw = yaw, w = lw, d = ld, yard = yard ? -2 : -3, source = 4 });
                            meta.Add((yx, yz, yard ? yr : 0f, Yaw.Yard));
                        }
                    }
        }

        /// <summary>Chamfer distance from every cell to the nearest cell no lot may cover (or the glass margin).</summary>
        void ClearRadius(SiteMap map)
        {
            const float mx = Terrain.HX - Terrain.GlassMargin, mz = Terrain.HZ - Terrain.GlassMargin;
            const byte blockFlags = SiteMap.FCanal | SiteMap.FGreen | SiteMap.FCommons | SiteMap.FYard | SiteMap.FLaid | SiteMap.FRock;
            for (int c = 0; c < SiteMap.N; c++)
            {
                bool blocked = map.lot[c] != 0 || map.pond[c] || (map.flags[c] & blockFlags) != 0 || map.ring[c] != 0
                               || map.tier[c] >= PathNet.PathTier
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

        float Jit(Lot site, int k) => (Terrain.Hash3(w.Seed, site.id, 3000 + k) - 0.5f) * 2f * Jitter;

        /// <summary>The yaw a rule gives a house at (x, z) beside the yard at (yx, yz). False if the rule doesn't apply.</summary>
        bool YawFor(Yaw rule, float x, float z, float yx, float yz, out float yaw)
        {
            var map = w.Map;
            yaw = 0f;
            switch (rule)
            {
                case Yaw.Yard: yaw = Placer.FacingYaw(x, z, yx, yz); return true;
                case Yaw.Sun: yaw = SunYaw; return true;
                case Yaw.Green: yaw = Placer.FacingYaw(x, z, map.greenX, map.greenZ); return true;
                case Yaw.Contour:
                {
                    Gradient(map, x, z, out float gx, out float gz);
                    float s = MathF.Sqrt(gx * gx + gz * gz);
                    if (s < 0.12f) return false;
                    yaw = MathF.Atan2(gx / s, gz / s) * (180f / MathF.PI); // front faces downhill
                    return true;
                }
            }
            return false;
        }

        static void Gradient(SiteMap map, float x, float z, out float gx, out float gz)
        {
            const float e = 0.08f;
            gx = (Terrain.Height(map.spec, x + e, z) - Terrain.Height(map.spec, x - e, z)) / (2 * e);
            gz = (Terrain.Height(map.spec, x, z + e) - Terrain.Height(map.spec, x, z - e)) / (2 * e);
        }

        /// <summary>A new yard may go here: dry, inside the glass margin, off the green and the commons, no lot on it,
        /// far enough from the other yards.</summary>
        bool OpenYard(float x, float z, float r, float spacing)
        {
            var map = w.Map;
            const float mx = Terrain.HX - Terrain.GlassMargin - 0.15f, mz = Terrain.HZ - Terrain.GlassMargin - 0.15f;
            if (MathF.Abs(x) > mx || MathF.Abs(z) > mz) return false;
            foreach (var k in w.Yards)
                if (Terrain.Dist(k.x, k.z, x, z) < spacing) return false;
            SiteMap.RectCells(x, z, 0f, r * 1.6f, r * 1.6f, 0f, cells);
            const byte blockFlags = SiteMap.FCanal | SiteMap.FGreen | SiteMap.FCommons | SiteMap.FYard | SiteMap.FRock;
            foreach (int c in cells)
                if (map.pond[c] || (map.flags[c] & blockFlags) != 0 || map.ring[c] != 0 || map.lot[c] != 0 || map.slope[c] > 0.5f) return false;
            return true;
        }

        // ---------------- constraints + score ----------------
        void Evaluate(Lot site, KindSpec spec, Nudge nudge, Candidate c, (float yx, float yz, float yr, Yaw yaw) m, int idx, bool strict)
        {
            c.valid = false;
            c.reject = ClusterCheck(c, m, strict) ?? inner.Check(site, spec, c, reach);
            if (c.reject != null) return;
            c.valid = true;
            var map = w.Map;
            float sum = 0f;

            // The family's yard sits in a band round the green (a nucleated village of hamlets, not scattered farms).
            float dg = Terrain.Dist(m.yx, m.yz, map.greenX, map.greenZ);
            float green = dg < GreenNear ? 1f - 2f * (GreenNear - dg) / (GreenNear - SiteMap.GreenR + 0.01f)
                : dg > GreenFar ? 1f - 2f * (dg - GreenFar) / 1.2f : 1f;
            sum += WGreen * Terrain.Clamp(green, -1f, 1f);

            // (an answer to the Keeper goes where the land was shaped, family or not)
            if (c.yard > 0) { if (nudge == null) sum += WJoin; }
            else
            {
                float best = 99f;
                foreach (var k in w.Yards) best = MathF.Min(best, Terrain.Dist(k.x, k.z, m.yx, m.yz));
                float nb = best > 50f ? 0f : best <= 1.6f ? 1f : Terrain.Clamp(1f - (best - 1.6f) / 0.8f, -1f, 1f);
                sum += WNeighbour * nb;
            }

            float acc = Placer.TermValue(Term.Access, w, site.kind, spec, nudge, c.x, c.z, c.yaw, c.w, c.d, c.door, reach);
            SiteMap.RectCells(c.x, c.z, c.yaw, c.w, c.d, 0f, cells);
            float sl = 0f, wd = 99f; int trees = 0;
            foreach (int k in cells) { sl += map.slope[k]; wd = MathF.Min(wd, map.waterDist[k]); if (map.Forest(k)) trees++; }
            int nc = Math.Max(1, cells.Count);
            float slope = Terrain.Clamp(1f - 2f * (sl / nc) / Math.Max(0.05f, spec.maxSlope), -1f, 1f);
            float water = Encyclopedia.WaterTerm(site.kind, wd);
            float forest = -2f * trees / nc;
            float nud = Placer.TermValue(Term.Nudge, w, site.kind, spec, nudge, c.x, c.z, c.yaw, c.w, c.d, c.door, reach);
            sum += WAccess * acc + WSlope * slope + WWater * water + WForest * forest + WNudge * nud;

            // On a slope the long side follows the contour (the front faces up or down the hill).
            Gradient(map, c.x, c.z, out float gx, out float gz);
            float s = MathF.Sqrt(gx * gx + gz * gz);
            if (s > 0.12f)
            {
                float r = c.yaw * (MathF.PI / 180f), fx = -MathF.Sin(r), fz = -MathF.Cos(r);
                float align = MathF.Abs(fx * gx + fz * gz) / s;
                sum += WContour * (2f * align - 1f) * MathF.Min(1f, s / 0.4f);
            }

            sum += WYaw * YawPref[(int)m.yaw];
            sum += WTaste * (Terrain.Hash3(w.Seed, site.id, 5000 + idx) - 0.5f) * 2f;
            if (nudge == null) sum -= Placer.KeepClear(w, c.x, c.z);
            c.score = sum;
        }

        /// <summary>The family rules: the yard stays open, other families keep their distance.</summary>
        string ClusterCheck(Candidate c, (float yx, float yz, float yr, Yaw yaw) m, bool strict)
        {
            if (m.yr > 0f && SiteMap.PointRectDist(m.yx, m.yz, c.x, c.z, c.yaw, c.w, c.d) < m.yr * 0.85f) return "yard";
            foreach (var k in w.Yards)
                if (SiteMap.PointRectDist(k.x, k.z, c.x, c.z, c.yaw, c.w, c.d) < k.r * 0.85f) return "yard";
            if (c.yard == -2)
                foreach (var o in w.Lots)
                {
                    if (o.open || o.retired) continue;
                    if (SiteMap.PointRectDist(m.yx, m.yz, o.x, o.z, o.yaw, o.w, o.d) < m.yr) return "yard";
                }
            float gapCluster = strict ? ClusterGap : RelaxedGap, gapOther = strict ? OtherGap : RelaxedGap;
            foreach (var o in w.Lots)
            {
                if (o.open || o.retired) continue;
                if (o.yard > 0 && o.yard == c.yard) continue;
                float need = o.yard > 0 ? gapCluster : gapOther;
                float dx = o.x - c.x, dz = o.z - c.z, rr = 0.5f * (MathF.Sqrt(o.w * o.w + o.d * o.d) + MathF.Sqrt(c.w * c.w + c.d * c.d)) + need;
                if (dx * dx + dz * dz > rr * rr) continue;
                if (SiteMap.RectGap(o.x, o.z, o.yaw, o.w, o.d, c.x, c.z, c.yaw, c.w, c.d) < need) return "neighbours";
            }
            return null;
        }

        // ---------------- outbuildings ----------------
        /// <summary>A family's barn, byre or shed across the yard from its homes (the open point "families stop at
        /// ~2 homes": with the outbuilding each family reads as a farmstead of 3–5). No path: it opens onto the yard.
        /// False when nothing fits (the family just has no barn).</summary>
        public bool PlaceOutbuilding(Yard k, Lot site)
        {
            var map = w.Map;
            // Away from the homes: the mean direction from the yard to its homes, flipped.
            float hx = 0f, hz = 0f;
            foreach (var o in w.Lots)
                if (o.yard == k.id && !o.retired) { hx += o.x - k.x; hz += o.z - k.z; }
            float baseAng = hx * hx + hz * hz > 1e-6f ? MathF.Atan2(-hz, -hx) : Terrain.Hash3(w.Seed, k.id, 61) * MathF.PI * 2f;
            var spec = Encyclopedia.Of(LotKind.Outbuilding);
            cands.Clear();
            for (int a = 0; a < 16; a++)
            {
                // Fan out from straight across the yard, alternating sides.
                float turn = (a + 1) / 2 * (MathF.PI / 9f) * (a % 2 == 0 ? 1f : -1f);
                float ang = baseAng + turn, ux = MathF.Cos(ang), uz = MathF.Sin(ang);
                float setback = 0.02f + 0.05f * Terrain.Hash3(w.Seed, site.id, 200 + a);
                float yaw = Placer.FacingYaw(k.x + ux, k.z + uz, k.x, k.z) + Jit(site, 7000 + a) * 0.5f;
                float ext = Extent(yaw, site.w, site.d, ux, uz);
                float x = k.x + ux * (k.r + ext + setback), z = k.z + uz * (k.r + ext + setback);
                var c = new Candidate { x = x, z = z, yaw = yaw, w = site.w, d = site.d, yard = k.id, source = 12 };
                c.reject = OutbuildingCheck(k, c, spec);
                if (c.reject != null) continue;
                float sl = 0f;
                SiteMap.RectCells(c.x, c.z, c.yaw, c.w, c.d, 0f, cells);
                foreach (int q in cells) sl += map.slope[q];
                c.score = -Math.Abs(turn) - sl / Math.Max(1, cells.Count) - Placer.KeepClear(w, c.x, c.z);
                c.valid = true;
                cands.Add(c);
            }
            Candidate best = null;
            foreach (var c in cands) if (best == null || c.score > best.score) best = c;
            if (best == null) return false;
            site.yard = k.id;
            return w.Claim(site, best.x, best.z, best.yaw, best.w, best.d, -1, false, needsPath: false);
        }

        string OutbuildingCheck(Yard k, Candidate c, KindSpec spec)
        {
            var map = w.Map;
            const float mx = Terrain.HX - Terrain.GlassMargin, mz = Terrain.HZ - Terrain.GlassMargin;
            var cx = new float[4]; var cz = new float[4];
            SiteMap.Corners(c.x, c.z, c.yaw, c.w, c.d, cx, cz);
            for (int q = 0; q < 4; q++) if (MathF.Abs(cx[q]) > mx || MathF.Abs(cz[q]) > mz) return "glass";
            SiteMap.RectCells(c.x, c.z, c.yaw, c.w, c.d, 0f, cells);
            if (cells.Count == 0) return "tiny";
            float slope = 0f;
            foreach (int q in cells)
            {
                if (map.Water(q)) return "water";
                if (map.CommonsAt(q) || map.YardAt(q) || map.Green(q) || map.Rock(q)) return "commons";
                if (map.ring[q] != 0 && map.ring[q] != k.id) return "family";
                if (map.lot[q] != 0) return "lot";
                if (map.Laid(q) || map.tier[q] >= PathNet.PathTier) return "path";
                if (map.waterDist[q] < spec.minWaterDist) return "wet";
                slope += map.slope[q];
            }
            if (slope / cells.Count > spec.maxSlope) return "slope";
            if (SiteMap.PointRectDist(k.x, k.z, c.x, c.z, c.yaw, c.w, c.d) < k.r * 0.85f) return "yard";
            foreach (var o in w.Lots)
            {
                if (o.open || o.retired) continue;
                float need = o.yard == k.id ? Placer.Gap : o.yard > 0 ? RelaxedGap : Placer.Gap;
                float dx = o.x - c.x, dz = o.z - c.z, rr = 0.5f * (MathF.Sqrt(o.w * o.w + o.d * o.d) + MathF.Sqrt(c.w * c.w + c.d * c.d)) + need;
                if (dx * dx + dz * dz > rr * rr) continue;
                if (SiteMap.RectGap(o.x, o.z, o.yaw, o.w, o.d, c.x, c.z, c.yaw, c.w, c.d) < need) return "overlap";
            }
            return null;
        }

        // ---------------- commons ----------------
        /// <summary>Some land stays common for good: a meadow or orchard near the village and a couple further out.</summary>
        public static void ReserveCommons(SiteMap map, SettleState st, int seed)
        {
            for (int tries = 0; st.commons.Count < CommonsCount && tries < 300; tries++)
            {
                float ang = Terrain.Hash3(seed, 900, tries) * MathF.PI * 2f;
                float r = (st.commons.Count == 0 ? 0.95f : 1.25f) + 0.9f * Terrain.Hash3(seed, 901, tries);
                float rad = CommonsRMin + (CommonsRMax - CommonsRMin) * Terrain.Hash3(seed, 902, tries);
                float x = map.greenX + MathF.Cos(ang) * r, z = map.greenZ + MathF.Sin(ang) * r;
                const float mx = Terrain.HX - Terrain.GlassMargin, mz = Terrain.HZ - Terrain.GlassMargin;
                if (MathF.Abs(x) > mx - rad * 0.5f || MathF.Abs(z) > mz - rad * 0.5f) continue;
                int c0 = SiteMap.CellAt(x, z);
                if (c0 < 0 || map.waterDist[c0] < rad + 0.1f || map.slope[c0] > 0.35f || map.Forest(c0)) continue;
                if (Terrain.Dist(x, z, map.greenX, map.greenZ) < SiteMap.GreenR + rad + 0.35f) continue;
                bool far = true;
                foreach (var p in st.commons) if (Terrain.Dist(p.x, p.z, x, z) < 1.3f) far = false;
                if (!far) continue;
                st.commons.Add(new Commons { x = x, z = z, r = rad });
                for (int c = 0; c < SiteMap.N; c++)
                    if (!map.Water(c) && Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) < rad) map.Set(c, SiteMap.FCommons, true);
            }
        }
    }
}
