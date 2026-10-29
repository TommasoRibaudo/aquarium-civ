using System;
using System.Collections.Generic;

namespace AquariumCiv.Spikes.Growth
{
    public sealed class Candidate
    {
        public float x, z, yaw, w, d;
        public int door = -1;
        public bool valid;
        public string reject;
        public float score;               // Σ wᵢ·fᵢ (only meaningful when valid)
        public float[] f;                 // the terms (only when valid)
        public byte source;               // 0 frontage, 1 edge ray, 2 backland, 3 water edge, 4 nudge ring, 5 green ring, 6 scan, 7 random, 8 hand
    }

    /// <summary>One placement, kept for the debug overlay (the Emilien Fig. 5 view: every candidate and its score).</summary>
    public sealed class PlacementLog
    {
        public Kind kind;
        public long month;
        public int siteId = -1;
        public int nudgeId = -1;
        public int chosen = -1;
        public List<Candidate> cands = new();
    }

    public interface IPlacer
    {
        /// <summary>Find a lot for <paramref name="site"/> (kind, size and id already set), claim it and connect it.</summary>
        bool Place(GrowthWorld w, Site site, NudgeRec nudge, PlacementLog log);
    }

    /// <summary>
    /// Option B of research 08: "scored frontage" placement. Candidates along the worn/laid network (both sides, at a
    /// setback, facing the path), out on rays from the green (fields, mills), behind existing lots (Town backland), at
    /// the water's edge (watermill, dyer) and round a Keeper's shaping. Constraints make a candidate invalid; the rest is
    /// a weighted sum of terms per building type (Encyclopedia). Best of all wins; ties go to a seed hash.
    /// </summary>
    public sealed class Placer : IPlacer
    {
        public const float Setback = 0.12f, Gap = 0.08f;
        public const int MaxFrontageCells = 240;

        readonly List<int> cells = new();
        readonly float[] cornerX = new float[4], cornerZ = new float[4];
        readonly List<int> network = new();
        readonly List<int> route = new();
        bool[] netMark = new bool[SiteMap.N];
        int[] reach = new int[SiteMap.N];

        public bool Place(GrowthWorld w, Site site, NudgeRec nudge, PlacementLog log)
        {
            var map = w.map;
            w.net.Network(network, netMark);
            Array.Copy(w.net.Flow(network), reach, SiteMap.N);
            var spec = Encyclopedia.Of(site.kind);
            var cands = log.cands;
            cands.Clear();
            Generate(w, site, spec, nudge, cands);
            foreach (var c in cands) Evaluate(w, site, spec, nudge, c, reach);
            if (!AnyValid(cands))
            {
                // Nothing on the network fits: scan the whole box (coarse) so the people still build.
                int before = cands.Count;
                Scan(w, site, cands);
                for (int i = before; i < cands.Count; i++) Evaluate(w, site, spec, nudge, cands[i], reach);
            }
            // Best first; claim and connect; if the door turns out to be cut off, take the next one.
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
                if (w.Claim(site, c.x, c.z, c.yaw, c.w, c.d, c.door, site.kind == Kind.Shop))
                {
                    log.chosen = i;
                    return true;
                }
                c.valid = false; c.reject = "cut off";
            }
            return false;
        }

        static bool RowsWith(Kind a, Kind b)
        {
            if (a == Kind.Farm || b == Kind.Farm) return a == b;
            bool ha = Encyclopedia.IsHome(a) || a == Kind.Tavern, hb = Encyclopedia.IsHome(b) || b == Kind.Tavern;
            return ha && hb;
        }

        static bool AnyValid(List<Candidate> cands)
        {
            foreach (var c in cands) if (c.valid) return true;
            return false;
        }

        // ---------------- candidates ----------------
        void Generate(GrowthWorld w, Site site, KindSpec spec, NudgeRec nudge, List<Candidate> into)
        {
            var map = w.map;
            float lw = site.w, ld = site.d;
            if (site.kind == Kind.Shop)
            {
                // Market stalls harden into shops: on the green's rim, facing in (encroachment, research 08 §1.2).
                for (int a = 0; a < 32; a++)
                {
                    float ang = a / 32f * MathF.PI * 2f, r = SiteMap.GreenR - ld / 2f + 0.06f;
                    float x = map.greenX + MathF.Cos(ang) * r, z = map.greenZ + MathF.Sin(ang) * r;
                    into.Add(Facing(x, z, map.greenX, map.greenZ, lw, ld, 5));
                }
                return;
            }

            // A: frontage along the network (both sides of every edge cell, at a setback, facing it).
            var edge = new List<int>();
            foreach (int c in network)
            {
                int i = SiteMap.I(c), j = SiteMap.J(c);
                bool rim = false;
                for (int dj = -1; dj <= 1 && !rim; dj++)
                    for (int di = -1; di <= 1 && !rim; di++)
                    {
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                        int nc = SiteMap.Index(ni, nj);
                        if (!netMark[nc] && map.lot[nc] == 0 && !map.Water(nc)) rim = true;
                    }
                if (rim) edge.Add(c);
            }
            int stride = Math.Max(1, (edge.Count + MaxFrontageCells - 1) / MaxFrontageCells);
            for (int k = 0; k < edge.Count; k += stride)
            {
                int c = edge[k];
                Tangent(map, c, out float tx, out float tz);
                float px = SiteMap.CXc(c), pz = SiteMap.CZc(c);
                for (int s = -1; s <= 1; s += 2)
                {
                    float nx = -tz * s, nz = tx * s;
                    float off = Setback + ld / 2f;
                    float cx = px + nx * off, cz = pz + nz * off;
                    float yaw = MathF.Atan2(nx, nz) * (180f / MathF.PI) + (GrowthTerrain.Hash3(w.seed, site.id, c * 2 + s) - 0.5f) * 6f;
                    into.Add(new Candidate { x = cx, z = cz, yaw = yaw, w = lw, d = ld, source = 0 });
                }
            }

            // B: out on rays from the green (fields, mills, landmarks), facing the green.
            if (spec.edgeCandidates)
                for (int a = 0; a < 32; a++)
                    for (float r = 1.0f; r <= 2.9f; r += 0.3f)
                    {
                        float ang = (a + 0.5f * ((int)(r * 10) % 2)) / 32f * MathF.PI * 2f;
                        float x = map.greenX + MathF.Cos(ang) * r, z = map.greenZ + MathF.Sin(ang) * r;
                        into.Add(Facing(x, z, map.greenX, map.greenZ, lw, ld, 1));
                    }

            // R: in a row beside a lot of the same family (house rows along a street, strip fields side by side).
            foreach (var o in w.sites)
            {
                if (o.retired || !RowsWith(site.kind, o.kind)) continue;
                o.Front(out float fx, out float fz);
                float rx = -fz, rz = fx;
                for (int s = -1; s <= 1; s += 2)
                {
                    float side = o.w / 2f + Gap + 0.02f + lw / 2f;
                    float back = (ld - o.d) / 2f; // fronts in line
                    float yaw = o.yaw + (GrowthTerrain.Hash3(w.seed, site.id, o.id * 2 + s) - 0.5f) * 8f;
                    into.Add(new Candidate { x = o.x + rx * side * s - fx * back, z = o.z + rz * side * s - fz * back, yaw = yaw, w = lw, d = ld, source = 9 });
                }
            }

            // C: backland behind existing homes (Town infill).
            if (spec.backland && w.era == Era.Town)
                foreach (var o in w.sites)
                {
                    if (o.retired || !Encyclopedia.IsHome(o.kind)) continue;
                    o.Front(out float fx, out float fz);
                    float off = o.d / 2f + Gap + 0.04f + ld / 2f;
                    into.Add(new Candidate { x = o.x - fx * off, z = o.z - fz * off, yaw = o.yaw, w = lw, d = ld, source = 2 });
                    float side = o.w / 2f + Gap + 0.04f + lw / 2f;
                    float rx = -fz, rz = fx;
                    into.Add(new Candidate { x = o.x - fx * off * 0.6f + rx * side, z = o.z - fz * off * 0.6f + rz * side, yaw = o.yaw, w = lw, d = ld, source = 2 });
                }

            // D: the water's edge, back to the water.
            if (spec.needsWater || site.kind == Kind.Dyer)
                for (int c = 0; c < SiteMap.N; c += 2)
                {
                    if (map.Water(c) || map.lot[c] != 0) continue;
                    int i = SiteMap.I(c), j = SiteMap.J(c);
                    float ax = 0, az = 0;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int ni = i + di, nj = j + dj;
                            if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                            if (map.Water(SiteMap.Index(ni, nj))) { ax -= di; az -= dj; }
                        }
                    float len = MathF.Sqrt(ax * ax + az * az);
                    if (len < 0.5f) continue;
                    ax /= len; az /= len; // away from the water
                    float cx = SiteMap.CXc(c) + ax * (ld / 2f + 0.01f), cz = SiteMap.CZc(c) + az * (ld / 2f + 0.01f);
                    into.Add(new Candidate { x = cx, z = cz, yaw = MathF.Atan2(-ax, -az) * (180f / MathF.PI), w = lw, d = ld, source = 3 });
                }

            // E: round the Keeper's shaping, facing the nearest network cell.
            // (an answer may take a smaller lot so it fits where the land was shaped)
            if (nudge != null)
                for (int size = 0; size < 2; size++)
                {
                    float sw = size == 0 ? lw : lw * 0.85f, sd = size == 0 ? ld : ld * 0.7f;
                    for (int a = 0; a < 16; a++)
                        for (float r = 0.15f; r <= 1.06f; r += 0.15f)
                        {
                            float ang = (a + (size * 0.5f)) / 16f * MathF.PI * 2f;
                            float x = nudge.x + MathF.Cos(ang) * r, z = nudge.z + MathF.Sin(ang) * r;
                            int near = NearestNetwork(x, z);
                            float tx = near >= 0 ? SiteMap.CXc(near) : map.greenX, tz = near >= 0 ? SiteMap.CZc(near) : map.greenZ;
                            var cand = Facing(x, z, tx, tz, sw, sd, 4);
                            into.Add(cand);
                            for (int q = 1; q < 4; q++)
                                into.Add(new Candidate { x = x, z = z, yaw = cand.yaw + 90f * q, w = sw, d = sd, source = 4 });
                        }
                }
        }

        void Scan(GrowthWorld w, Site site, List<Candidate> into)
        {
            for (int c = 0; c < SiteMap.N; c += 3)
            {
                if (w.map.lot[c] != 0 || w.map.Water(c)) continue;
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                int near = NearestNetwork(x, z);
                if (near < 0) continue;
                into.Add(Facing(x, z, SiteMap.CXc(near), SiteMap.CZc(near), site.w, site.d, 6));
            }
        }

        int NearestNetwork(float x, float z)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (int c in network)
            {
                float dx = SiteMap.CXc(c) - x, dz = SiteMap.CZc(c) - z, d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        public static Candidate Facing(float x, float z, float tx, float tz, float w, float d, byte source)
        {
            float fx = tx - x, fz = tz - z;
            float yaw = MathF.Atan2(-fx, -fz) * (180f / MathF.PI);
            return new Candidate { x = x, z = z, yaw = yaw, w = w, d = d, source = source };
        }

        /// <summary>Path direction at a network cell: principal axis of nearby network cells; on the green, along its rim.</summary>
        void Tangent(SiteMap map, int c, out float tx, out float tz)
        {
            float px = SiteMap.CXc(c), pz = SiteMap.CZc(c);
            if (map.green[c])
            {
                float rx = px - map.greenX, rz = pz - map.greenZ, l = MathF.Sqrt(rx * rx + rz * rz) + 1e-6f;
                tx = -rz / l; tz = rx / l;
                return;
            }
            // Prefer the direction people actually walk here (accumulated from traced routes).
            float dc = map.dirC[c], ds = map.dirS[c];
            if (dc * dc + ds * ds > 0.04f)
            {
                float a2 = 0.5f * MathF.Atan2(ds, dc);
                tx = MathF.Cos(a2); tz = MathF.Sin(a2);
                return;
            }
            int i = SiteMap.I(c), j = SiteMap.J(c);
            float sxx = 0, sxz = 0, szz = 0; int n = 0;
            for (int dj = -2; dj <= 2; dj++)
                for (int di = -2; di <= 2; di++)
                {
                    int ni = i + di, nj = j + dj;
                    if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                    int nc = SiteMap.Index(ni, nj);
                    if (!netMark[nc] || map.green[nc]) continue;
                    sxx += di * di; sxz += di * dj; szz += dj * dj; n++;
                }
            if (n < 2)
            {
                float rx = px - map.greenX, rz = pz - map.greenZ, l = MathF.Sqrt(rx * rx + rz * rz) + 1e-6f;
                tx = -rz / l; tz = rx / l;
                return;
            }
            // principal eigenvector of [[sxx, sxz], [sxz, szz]]
            float ang = 0.5f * MathF.Atan2(2f * sxz, sxx - szz);
            tx = MathF.Cos(ang); tz = MathF.Sin(ang);
        }

        // ---------------- constraints + score ----------------
        public void Evaluate(GrowthWorld w, Site site, KindSpec spec, NudgeRec nudge, Candidate c, int[] reachField)
        {
            c.valid = false; c.f = null;
            c.reject = Check(w, site, spec, c, reachField);
            if (c.reject != null) return;
            c.valid = true;
            c.f = new float[spec.w.Length];
            float sum = 0f;
            for (int t = 0; t < spec.w.Length; t++)
            {
                if (spec.w[t] == 0f) continue;
                float f = TermValue((Term)t, w, site.kind, spec, nudge, c.x, c.z, c.yaw, c.w, c.d, c.door, reachField);
                c.f[t] = f;
                sum += spec.w[t] * f;
            }
            // Keep clear: while a shaping waits for the people's answer, other buildings leave room round it.
            if (nudge == null)
                foreach (var n in w.nudges)
                {
                    if (n.siteId >= 0 || w.State.month - n.month >= AquariumCiv.Sim.SimCore.NudgeMonths) continue;
                    float dn = GrowthTerrain.Dist(c.x, c.z, n.x, n.z);
                    if (dn < KeepClearRadius) sum -= KeepClearWeight * (1f - dn / KeepClearRadius);
                }
            c.score = sum;
        }

        public static float KeepClearRadius = 0.9f, KeepClearWeight = 4f;

        /// <summary>Null if the lot may go here, else why not.</summary>
        public string Check(GrowthWorld w, Site site, KindSpec spec, Candidate c, int[] reachField)
        {
            var map = w.map;
            const float mx = GrowthTerrain.HX - GrowthTerrain.GlassMargin, mz = GrowthTerrain.HZ - GrowthTerrain.GlassMargin;
            var cx = cornerX; var cz = cornerZ;
            SiteMap.Corners(c.x, c.z, c.yaw, c.w, c.d, cx, cz);
            for (int k = 0; k < 4; k++) if (MathF.Abs(cx[k]) > mx || MathF.Abs(cz[k]) > mz) return "glass";
            SiteMap.RectCells(c.x, c.z, c.yaw, c.w, c.d, 0f, cells);
            if (cells.Count == 0) return "tiny";
            float slope = 0f, minWater = 99f;
            foreach (int k in cells)
            {
                if (map.Water(k)) return "water";
                if (map.lot[k] != 0) return "lot";
                if (map.green[k] && site.kind != Kind.Shop) return "green";
                if (map.laid[k] || map.tier[k] >= PathNet.PathTier) return "path";
                slope += map.slope[k];
                if (map.waterDist[k] < minWater) minWater = map.waterDist[k];
            }
            if (slope / cells.Count > spec.maxSlope) return "slope";
            if (spec.needsWater)
            {
                if (minWater > 0.16f) return "dry";
            }
            else if (minWater < spec.minWaterDist) return "wet";
            foreach (var o in w.sites)
            {
                if (o.kind == Kind.Camp || o.kind == Kind.Well) continue;
                float dx = o.x - c.x, dz = o.z - c.z, rr = 0.5f * (MathF.Sqrt(o.w * o.w + o.d * o.d) + MathF.Sqrt(c.w * c.w + c.d * c.d)) + Gap;
                if (dx * dx + dz * dz > rr * rr) continue;
                if (SiteMap.RectGap(o.x, o.z, o.yaw, o.w, o.d, c.x, c.z, c.yaw, c.w, c.d) < Gap) return "overlap";
            }
            // The door: just outside the front edge, reachable from the network.
            float r = c.yaw * (MathF.PI / 180f);
            float fx = -MathF.Sin(r), fz = -MathF.Cos(r);
            int door = SiteMap.CellAt(c.x + fx * (c.d / 2f + 0.07f), c.z + fz * (c.d / 2f + 0.07f));
            if (door < 0 || map.pond[door] || map.lot[door] != 0) return "door";
            if (reachField[door] >= PathNet.Inf) return "no way";
            c.door = door;
            return null;
        }

        public static float TermValue(Term t, GrowthWorld w, Kind kind, KindSpec spec, NudgeRec nudge, float x, float z, float yaw, float lw, float ld, int door, int[] reachField)
        {
            var map = w.map;
            switch (t)
            {
                case Term.Green:
                {
                    float dg = GrowthTerrain.Dist(x, z, map.greenX, map.greenZ);
                    if (dg < spec.greenNear) return 1f - 2f * (spec.greenNear - dg) / Math.Max(0.05f, spec.greenNear);
                    if (dg > spec.greenFar) return Math.Max(-1f, 1f - 2f * (dg - spec.greenFar) / 1.5f);
                    return 1f;
                }
                case Term.Edge:
                {
                    float dg = GrowthTerrain.Dist(x, z, map.greenX, map.greenZ);
                    return GrowthTerrain.Clamp((dg - 1.1f) / 0.8f, -1f, 1f);
                }
                case Term.Access:
                    if (door < 0 || reachField == null || reachField[door] >= PathNet.Inf) return -1f;
                    return GrowthTerrain.Clamp(1f - Math.Max(0, reachField[door] - 10) / 50f, -1f, 1f);
                case Term.Social:
                {
                    float best = 99f;
                    foreach (var o in w.sites)
                    {
                        if (o.retired || o.kind == Kind.Farm || o.kind == Kind.Camp || o.kind == Kind.Well) continue;
                        float g = SiteMap.RectGap(o.x, o.z, o.yaw, o.w, o.d, x, z, yaw, lw, ld);
                        if (g < best) best = g;
                    }
                    if (best > 50f) return 0f;
                    if (best <= 0.35f) return 1f;
                    return Math.Max(-0.5f, 1f - 1.5f * (best - 0.35f) / 0.85f);
                }
                case Term.SameKind:
                {
                    float best = 99f;
                    foreach (var o in w.sites)
                    {
                        if (o.kind != kind) continue;
                        float g = SiteMap.RectGap(o.x, o.z, o.yaw, o.w, o.d, x, z, yaw, lw, ld);
                        if (g < best) best = g;
                    }
                    if (best > 50f) return 0f;
                    return best < 0.3f ? 1f : Math.Max(-0.5f, 1f - (best - 0.3f) / 0.6f);
                }
                case Term.NearKind:
                {
                    float best = 99f;
                    foreach (var o in w.sites)
                        if (Array.IndexOf(spec.nearKinds, o.kind) >= 0)
                            best = Math.Min(best, GrowthTerrain.Dist(o.x, o.z, x, z));
                    if (best > 50f) return 0f;
                    return 1f - GrowthTerrain.Clamp01(best / 1.5f) * 2f;
                }
                case Term.Slope:
                {
                    float s = MeanOver(map.slope, x, z, yaw, lw, ld);
                    return GrowthTerrain.Clamp(1f - 2f * s / Math.Max(0.05f, spec.maxSlope), -1f, 1f);
                }
                case Term.Water:
                {
                    float best = 99f;
                    var tmp = Scratch;
                    SiteMap.RectCells(x, z, yaw, lw, ld, 0.05f, tmp);
                    foreach (int k in tmp) best = Math.Min(best, map.waterDist[k]);
                    if (best > 50f) { int c = SiteMap.CellAt(x, z); best = c >= 0 ? map.waterDist[c] : 9f; }
                    return Encyclopedia.WaterTerm(kind, best);
                }
                case Term.Fertility:
                {
                    var tmp = Scratch;
                    SiteMap.RectCells(x, z, yaw, lw, ld, 0f, tmp);
                    if (tmp.Count == 0) return 0f;
                    float s = 0; foreach (int k in tmp) s += map.Fertility(k, w.State.month);
                    return GrowthTerrain.Clamp((s / tmp.Count - 0.5f) * 2.5f, -1f, 1f);
                }
                case Term.Forest:
                {
                    var tmp = Scratch;
                    SiteMap.RectCells(x, z, yaw, lw, ld, 0f, tmp);
                    int inside = 0; foreach (int k in tmp) if (map.forest[k] != 0) inside++;
                    float frac = tmp.Count > 0 ? inside / (float)tmp.Count : 0f;
                    if (kind == Kind.Lumbermill)
                    {
                        int near = CountForest(map, x, z, 0.7f) - inside;
                        return GrowthTerrain.Clamp(Math.Min(1f, near / 10f) * 2f - 1f - frac, -1f, 1f);
                    }
                    if (kind == Kind.Windmill) return 1f - Math.Min(1f, CountForest(map, x, z, 0.5f) / 4f) * 2f;
                    return -frac * 2f;
                }
                case Term.Height:
                {
                    float h = GrowthTerrain.Height(map.spec, x, z), ring = 0f;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * MathF.PI / 4f;
                        ring += GrowthTerrain.Height(map.spec, x + MathF.Cos(a) * 0.8f, z + MathF.Sin(a) * 0.8f);
                    }
                    ring /= 8f;
                    return GrowthTerrain.Clamp((h - ring) / 0.08f + (h - GrowthTerrain.G - 0.05f) / 0.25f, -1f, 1f);
                }
                case Term.Busy:
                {
                    float best = 0f;
                    if (door < 0) return -1f;
                    int i0 = SiteMap.I(door), j0 = SiteMap.J(door);
                    for (int dj = -2; dj <= 2; dj++)
                        for (int di = -2; di <= 2; di++)
                        {
                            int ni = i0 + di, nj = j0 + dj;
                            if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                            best = Math.Max(best, map.wear[SiteMap.Index(ni, nj)]);
                        }
                    return GrowthTerrain.Clamp(best / PathNet.TPath, 0f, 1f) * 2f - 1f;
                }
                case Term.Quiet:
                {
                    int n = 0;
                    foreach (var o in w.sites)
                        if (!o.retired && Encyclopedia.IsHome(o.kind) && GrowthTerrain.Dist(o.x, o.z, x, z) < 0.8f) n++;
                    return 1f - Math.Min(1f, n / 3f) * 2f;
                }
                case Term.FrontTall:
                    // Tall buildings in the front third of the box hide the people (research 08 §3.3).
                    return GrowthTerrain.Clamp((z + 0.73f) / 0.8f, -1f, 1f);
                case Term.Nudge:
                    if (nudge == null) return 0f;
                    return GrowthTerrain.Clamp(1f - GrowthTerrain.Dist(x, z, nudge.x, nudge.z) / 0.6f, -1f, 1f);
            }
            return 0f;
        }

        static readonly List<int> Scratch = new();

        static int CountForest(SiteMap map, float x, float z, float r)
        {
            int n = 0;
            int i0 = (int)MathF.Floor((x - r - SiteMap.X0) / SiteMap.Cell), i1 = (int)MathF.Floor((x + r - SiteMap.X0) / SiteMap.Cell);
            int j0 = (int)MathF.Floor((z - r - SiteMap.Z0) / SiteMap.Cell), j1 = (int)MathF.Floor((z + r - SiteMap.Z0) / SiteMap.Cell);
            for (int j = Math.Max(0, j0); j <= Math.Min(SiteMap.NZ - 1, j1); j++)
                for (int i = Math.Max(0, i0); i <= Math.Min(SiteMap.NX - 1, i1); i++)
                {
                    int c = SiteMap.Index(i, j);
                    if (map.forest[c] != 0 && GrowthTerrain.Dist(SiteMap.CX(i), SiteMap.CZ(j), x, z) <= r) n++;
                }
            return n;
        }

        static float MeanOver(float[] layer, float x, float z, float yaw, float w, float d)
        {
            var tmp = Scratch;
            SiteMap.RectCells(x, z, yaw, w, d, 0f, tmp);
            if (tmp.Count == 0) { int c = SiteMap.CellAt(x, z); return c >= 0 ? layer[c] : 0f; }
            float s = 0; foreach (int k in tmp) s += layer[k];
            return s / tmp.Count;
        }

        /// <summary>One term (or the total) for a kind at every cell, the lot facing the nearest network cell: the overlay.</summary>
        public float[] TermGrid(GrowthWorld w, Kind kind, Term? term)
        {
            var spec = Encyclopedia.Of(kind);
            w.net.Network(network, netMark);
            Array.Copy(w.net.Flow(network), reach, SiteMap.N);
            var grid = new float[SiteMap.N];
            var site = new Site { id = 999999, kind = kind, w = (spec.wMin + spec.wMax) / 2f, d = (spec.dMin + spec.dMax) / 2f };
            for (int c = 0; c < SiteMap.N; c++)
            {
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                int near = NearestNetwork(x, z);
                var cand = Facing(x, z, near >= 0 ? SiteMap.CXc(near) : w.map.greenX, near >= 0 ? SiteMap.CZc(near) : w.map.greenZ, site.w, site.d, 6);
                if (term.HasValue)
                {
                    float r = cand.yaw * (MathF.PI / 180f);
                    int door = SiteMap.CellAt(x - MathF.Sin(r) * (site.d / 2f + 0.07f), z - MathF.Cos(r) * (site.d / 2f + 0.07f));
                    grid[c] = TermValue(term.Value, w, kind, spec, null, x, z, cand.yaw, site.w, site.d, door, reach);
                }
                else
                {
                    Evaluate(w, site, spec, null, cand, reach);
                    grid[c] = cand.valid ? cand.score : float.NaN;
                }
            }
            return grid;
        }
    }

    /// <summary>Control (a): the same build order, lots scattered at random (no overlap, same constraints), same paths.</summary>
    public sealed class RandomPlacer : IPlacer
    {
        readonly Placer checker = new();
        readonly List<int> network = new();
        bool[] mark = new bool[SiteMap.N];
        int[] reach = new int[SiteMap.N];

        public bool Place(GrowthWorld w, Site site, NudgeRec nudge, PlacementLog log)
        {
            w.net.Network(network, mark);
            Array.Copy(w.net.Flow(network), reach, SiteMap.N);
            var spec = Encyclopedia.Of(site.kind);
            const float mx = GrowthTerrain.HX - GrowthTerrain.GlassMargin, mz = GrowthTerrain.HZ - GrowthTerrain.GlassMargin;
            for (int a = 0; a < 600; a++)
            {
                float x = -mx + 2 * mx * GrowthTerrain.Hash3(w.seed, site.id, 3 * a);
                float z = -mz + 2 * mz * GrowthTerrain.Hash3(w.seed, site.id, 3 * a + 1);
                float yaw = 360f * GrowthTerrain.Hash3(w.seed, site.id, 3 * a + 2);
                var c = new Candidate { x = x, z = z, yaw = yaw, w = site.w, d = site.d, source = 7 };
                c.reject = checker.Check(w, site, spec, c, reach);
                if (c.reject != null) continue;
                c.valid = true;
                if (w.Claim(site, c.x, c.z, c.yaw, c.w, c.d, c.door, false)) { log.cands.Add(c); log.chosen = 0; return true; }
            }
            return false;
        }
    }

    /// <summary>Control (b): today's way. Hex slots centre-out (as the prototypes' PrototypeSceneBuilder), the building
    /// takes SimCore's slot index, yaw a random multiple of 60°. Lots are shrunk to fit a slot; paths still connect.</summary>
    public sealed class HandPlacer : IPlacer
    {
        public const float Spacing = 0.62f;
        List<(float x, float z)> slots;
        readonly HashSet<int> used = new();

        public static List<(float x, float z)> Slots(SiteMap map)
        {
            var list = new List<(float x, float z, float d)>();
            float rowH = Spacing * 0.8660254f;
            const float mx = GrowthTerrain.HX - GrowthTerrain.GlassMargin - 0.2f, mz = GrowthTerrain.HZ - GrowthTerrain.GlassMargin - 0.2f;
            for (int row = -10; row <= 10; row++)
                for (int col = -12; col <= 12; col++)
                {
                    float x = map.greenX + col * Spacing + (row & 1) * Spacing * 0.5f, z = map.greenZ + row * rowH;
                    if (MathF.Abs(x) > mx || MathF.Abs(z) > mz) continue;
                    int c = SiteMap.CellAt(x, z);
                    if (c < 0 || map.waterDist[c] < 0.25f || map.slope[c] > 0.5f) continue;
                    float d = GrowthTerrain.Dist(x, z, map.greenX, map.greenZ);
                    if (d < 0.3f) continue; // the centre tile is the camp
                    list.Add((x, z, d));
                }
            list.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            var r = new List<(float, float)>();
            foreach (var p in list) r.Add((p.x, p.z));
            return r;
        }

        public bool Place(GrowthWorld w, Site site, NudgeRec nudge, PlacementLog log)
        {
            slots ??= Slots(w.map);
            int slot = -1;
            if (site.simId >= 0)
            {
                var b = w.State.buildings.Find(x => x.id == site.simId);
                if (b != null) slot = b.slot - 1; // slot 0 is the camp
            }
            if (slot < 0 || used.Contains(slot)) { slot = 0; while (used.Contains(slot)) slot++; }
            if (slot >= slots.Count) return false;
            used.Add(slot);
            site.w = Math.Min(site.w, 0.5f); site.d = Math.Min(site.d, 0.5f);
            float yaw = 60f * (int)(GrowthTerrain.Hash3(w.seed, site.id, 77) * 6f);
            var (x, z) = slots[slot];
            float r = yaw * (MathF.PI / 180f);
            int door = SiteMap.CellAt(x - MathF.Sin(r) * (site.d / 2f + 0.07f), z - MathF.Cos(r) * (site.d / 2f + 0.07f));
            log.cands.Add(new Candidate { x = x, z = z, yaw = yaw, w = site.w, d = site.d, valid = true, source = 8, door = door });
            log.chosen = 0;
            return w.Claim(site, x, z, yaw, site.w, site.d, door, false, force: true);
        }
    }
}
