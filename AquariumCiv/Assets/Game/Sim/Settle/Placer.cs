using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>One spot a lot could take, with why it can't (reject) or how good it is (score).</summary>
    public sealed class Candidate
    {
        public float x, z, yaw, w, d;
        public int door = -1;
        public bool valid;
        public string reject;
        public float score;               // Σ wᵢ·fᵢ (only meaningful when valid)
        public int yard;                  // ClusterPlacer: joins this yard (> 0), founds one (-2), a lone house (-3), none (0)
        public byte source;               // 0 frontage, 1 edge ray, 2 backland, 3 water edge, 4 nudge ring, 5 green ring, 6 infill, 9 row, 10 join, 11 found
    }

    /// <summary>
    /// Round 1's "scored frontage" placement (research 08 option B), kept for fields, mills, workshops, the market,
    /// tavern, church and shops (decisions.md 2026-10-29). Candidates along the worn/laid network (both sides, at a
    /// setback, facing the path), out on rays from the green, strip-field rows, behind homes (Town backland), at the
    /// water's edge, and round a Keeper's shaping. Constraints make a candidate invalid; the rest is a weighted sum of
    /// terms per kind (Encyclopedia). Best of all wins; ties go to a seed hash. No dice. Ported from Growth Lab.
    /// </summary>
    public sealed class Placer
    {
        public const float Setback = 0.12f, Gap = 0.08f;
        public const int MaxFrontageCells = 240;
        public static float KeepClearRadius = 0.9f, KeepClearWeight = 4f;

        readonly Settlement w;
        readonly List<int> cells = new List<int>(), edge = new List<int>(), order = new List<int>();
        readonly float[] cornerX = new float[4], cornerZ = new float[4];
        readonly List<int> network = new List<int>();
        readonly bool[] netMark = new bool[SiteMap.N];
        readonly int[] reach = new int[SiteMap.N];
        readonly List<Candidate> cands = new List<Candidate>();
        static readonly List<int> Scratch = new List<int>();

        public Placer(Settlement settlement) { w = settlement; }

        public bool Place(Lot site, Nudge nudge)
        {
            w.Net.Network(network, netMark);
            Array.Copy(w.Net.Flow(network), reach, SiteMap.N);
            var spec = Encyclopedia.Of(site.kind);
            cands.Clear();
            Generate(site, spec, nudge, cands);
            foreach (var c in cands) Evaluate(site, spec, nudge, c, reach);
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
                if (w.Claim(site, c.x, c.z, c.yaw, c.w, c.d, c.door, site.kind == LotKind.Shop)) return true;
                c.valid = false; c.reject = "cut off";
            }
            return false;
        }

        // ---------------- candidates ----------------
        void Generate(Lot site, KindSpec spec, Nudge nudge, List<Candidate> into)
        {
            var map = w.Map;
            float lw = site.w, ld = site.d;
            if (site.kind == LotKind.Shop)
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
            edge.Clear();
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
                    float yaw = MathF.Atan2(nx, nz) * (180f / MathF.PI) + (Terrain.Hash3(w.Seed, site.id, c * 2 + s) - 0.5f) * 6f;
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

            // R: strip fields side by side, fronts in line.
            if (spec.rows)
                foreach (var o in w.Lots)
                {
                    if (o.retired || o.kind != site.kind) continue;
                    o.Front(out float fx, out float fz);
                    float rx = -fz, rz = fx;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float side = o.w / 2f + Gap + 0.02f + lw / 2f;
                        float back = (ld - o.d) / 2f;
                        float yaw = o.yaw + (Terrain.Hash3(w.Seed, site.id, o.id * 2 + s) - 0.5f) * 8f;
                        into.Add(new Candidate { x = o.x + rx * side * s - fx * back, z = o.z + rz * side * s - fz * back, yaw = yaw, w = lw, d = ld, source = 9 });
                    }
                }

            // C: backland behind existing homes (Town infill).
            if (spec.backland && w.Era == Era.Town)
                foreach (var o in w.Lots)
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
            if (spec.needsWater || site.kind == LotKind.Dyer || site.kind == LotKind.Kiln)
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

            // E: round the Keeper's shaping, facing the nearest network cell (an answer may take a smaller lot so it
            // fits where the land was shaped).
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

        /// <summary>A candidate at (x, z) whose front faces (tx, tz).</summary>
        public static Candidate Facing(float x, float z, float tx, float tz, float w, float d, byte source)
        {
            float fx = tx - x, fz = tz - z;
            float yaw = MathF.Atan2(-fx, -fz) * (180f / MathF.PI);
            return new Candidate { x = x, z = z, yaw = yaw, w = w, d = d, source = source };
        }

        /// <summary>The yaw whose front faces (tx, tz) from (x, z).</summary>
        public static float FacingYaw(float x, float z, float tx, float tz) => MathF.Atan2(-(tx - x), -(tz - z)) * (180f / MathF.PI);

        /// <summary>Path direction at a network cell: the walked direction, else the principal axis of nearby network
        /// cells; on the green, along its rim.</summary>
        void Tangent(SiteMap map, int c, out float tx, out float tz)
        {
            float px = SiteMap.CXc(c), pz = SiteMap.CZc(c);
            if (map.Green(c))
            {
                float rx = px - map.greenX, rz = pz - map.greenZ, l = MathF.Sqrt(rx * rx + rz * rz) + 1e-6f;
                tx = -rz / l; tz = rx / l;
                return;
            }
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
                    if (!netMark[nc] || map.Green(nc)) continue;
                    sxx += di * di; sxz += di * dj; szz += dj * dj; n++;
                }
            if (n < 2)
            {
                float rx = px - map.greenX, rz = pz - map.greenZ, l = MathF.Sqrt(rx * rx + rz * rz) + 1e-6f;
                tx = -rz / l; tz = rx / l;
                return;
            }
            float ang = 0.5f * MathF.Atan2(2f * sxz, sxx - szz);
            tx = MathF.Cos(ang); tz = MathF.Sin(ang);
        }

        // ---------------- constraints + score ----------------
        void Evaluate(Lot site, KindSpec spec, Nudge nudge, Candidate c, int[] reachField)
        {
            c.valid = false;
            c.reject = Check(site, spec, c, reachField);
            if (c.reject != null) return;
            c.valid = true;
            float sum = 0f;
            for (int t = 0; t < spec.w.Length; t++)
            {
                if (spec.w[t] == 0f) continue;
                sum += spec.w[t] * TermValue((Term)t, w, site.kind, spec, nudge, c.x, c.z, c.yaw, c.w, c.d, c.door, reachField);
            }
            if (nudge == null) sum -= KeepClear(w, c.x, c.z);
            c.score = sum;
        }

        /// <summary>While a shaping waits for the people's answer, other buildings leave room round it.</summary>
        public static float KeepClear(Settlement w, float x, float z)
        {
            float pen = 0f;
            foreach (var n in w.Nudges)
            {
                if (!w.Waiting(n)) continue;
                float dn = Terrain.Dist(x, z, n.x, n.z);
                if (dn < KeepClearRadius) pen += KeepClearWeight * (1f - dn / KeepClearRadius);
            }
            return pen;
        }

        /// <summary>Null if the lot may go here, else why not. Sets the candidate's door.</summary>
        public string Check(Lot site, KindSpec spec, Candidate c, int[] reachField)
        {
            var map = w.Map;
            const float mx = Terrain.HX - Terrain.GlassMargin, mz = Terrain.HZ - Terrain.GlassMargin;
            var cx = cornerX; var cz = cornerZ;
            SiteMap.Corners(c.x, c.z, c.yaw, c.w, c.d, cx, cz);
            for (int k = 0; k < 4; k++) if (MathF.Abs(cx[k]) > mx || MathF.Abs(cz[k]) > mz) return "glass";
            SiteMap.RectCells(c.x, c.z, c.yaw, c.w, c.d, 0f, cells);
            if (cells.Count == 0) return "tiny";
            float slope = 0f, minWater = 99f;
            foreach (int k in cells)
            {
                if (map.Water(k)) return "water";
                if (map.CommonsAt(k) || map.YardAt(k)) return "commons";
                if (map.ring[k] != 0 && map.ring[k] != c.yard) return "family";
                if (map.lot[k] != 0) return "lot";
                if (map.Green(k) && site.kind != LotKind.Shop) return "green";
                if (map.Laid(k) || map.tier[k] >= PathNet.PathTier) return "path";
                if (map.Rock(k)) return "rock";
                slope += map.slope[k];
                if (map.waterDist[k] < minWater) minWater = map.waterDist[k];
            }
            if (slope / cells.Count > spec.maxSlope) return "slope";
            if (spec.needsWater)
            {
                if (minWater > 0.16f) return "dry";
            }
            else if (minWater < spec.minWaterDist) return "wet";
            foreach (var o in w.Lots)
            {
                if (o.open || o.retired) continue;
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

        public static float TermValue(Term t, Settlement w, LotKind kind, KindSpec spec, Nudge nudge, float x, float z, float yaw, float lw, float ld, int door, int[] reachField)
        {
            var map = w.Map;
            switch (t)
            {
                case Term.Green:
                {
                    float dg = Terrain.Dist(x, z, map.greenX, map.greenZ);
                    if (dg < spec.greenNear) return 1f - 2f * (spec.greenNear - dg) / Math.Max(0.05f, spec.greenNear);
                    if (dg > spec.greenFar) return Math.Max(-1f, 1f - 2f * (dg - spec.greenFar) / 1.5f);
                    return 1f;
                }
                case Term.Edge:
                {
                    float dg = Terrain.Dist(x, z, map.greenX, map.greenZ);
                    return Terrain.Clamp((dg - 1.1f) / 0.8f, -1f, 1f);
                }
                case Term.Access:
                    if (door < 0 || reachField == null || reachField[door] >= PathNet.Inf) return -1f;
                    return Terrain.Clamp(1f - Math.Max(0, reachField[door] - 10) / 50f, -1f, 1f);
                case Term.Social:
                {
                    float best = 99f;
                    foreach (var o in w.Lots)
                    {
                        if (o.retired || o.open || o.kind == LotKind.Farm) continue;
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
                    foreach (var o in w.Lots)
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
                    foreach (var o in w.Lots)
                        if (Array.IndexOf(spec.nearKinds, o.kind) >= 0)
                            best = Math.Min(best, Terrain.Dist(o.x, o.z, x, z));
                    if (best > 50f) return 0f;
                    return 1f - Terrain.Clamp01(best / 1.5f) * 2f;
                }
                case Term.Slope:
                {
                    float s = MeanOver(map.slope, x, z, yaw, lw, ld);
                    return Terrain.Clamp(1f - 2f * s / Math.Max(0.05f, spec.maxSlope), -1f, 1f);
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
                    float s = 0; foreach (int k in tmp) s += map.Fertility(k, w.Month, w.Nudges);
                    return Terrain.Clamp((s / tmp.Count - 0.5f) * 2.5f, -1f, 1f);
                }
                case Term.Forest:
                {
                    var tmp = Scratch;
                    SiteMap.RectCells(x, z, yaw, lw, ld, 0f, tmp);
                    int inside = 0; foreach (int k in tmp) if (map.Forest(k)) inside++;
                    float frac = tmp.Count > 0 ? inside / (float)tmp.Count : 0f;
                    if (kind == LotKind.Lumbermill || kind == LotKind.TimberYard)
                    {
                        int near = CountForest(map, x, z, 0.7f) - inside;
                        return Terrain.Clamp(Math.Min(1f, near / 10f) * 2f - 1f - frac, -1f, 1f);
                    }
                    if (kind == LotKind.Windmill) return 1f - Math.Min(1f, CountForest(map, x, z, 0.5f) / 4f) * 2f;
                    return -frac * 2f;
                }
                case Term.Height:
                {
                    float h = Terrain.Height(map.spec, x, z), ring = 0f;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * MathF.PI / 4f;
                        ring += Terrain.Height(map.spec, x + MathF.Cos(a) * 0.8f, z + MathF.Sin(a) * 0.8f);
                    }
                    ring /= 8f;
                    return Terrain.Clamp((h - ring) / 0.08f + (h - Terrain.G - 0.05f) / 0.25f, -1f, 1f);
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
                    return Terrain.Clamp(best / PathNet.TPath, 0f, 1f) * 2f - 1f;
                }
                case Term.Quiet:
                {
                    int n = 0;
                    foreach (var o in w.Lots)
                        if (!o.retired && Encyclopedia.IsHome(o.kind) && Terrain.Dist(o.x, o.z, x, z) < 0.8f) n++;
                    return 1f - Math.Min(1f, n / 3f) * 2f;
                }
                case Term.FrontTall:
                    // Tall buildings in the front third of the box hide the people (research 08 §3.3).
                    return Terrain.Clamp((z + 0.73f) / 0.8f, -1f, 1f);
                case Term.Nudge:
                    if (nudge == null) return 0f;
                    return Terrain.Clamp(1f - Terrain.Dist(x, z, nudge.x, nudge.z) / 0.6f, -1f, 1f);
            }
            return 0f;
        }

        static int CountForest(SiteMap map, float x, float z, float r)
        {
            int n = 0;
            int i0 = (int)MathF.Floor((x - r - SiteMap.X0) / SiteMap.Cell), i1 = (int)MathF.Floor((x + r - SiteMap.X0) / SiteMap.Cell);
            int j0 = (int)MathF.Floor((z - r - SiteMap.Z0) / SiteMap.Cell), j1 = (int)MathF.Floor((z + r - SiteMap.Z0) / SiteMap.Cell);
            for (int j = Math.Max(0, j0); j <= Math.Min(SiteMap.NZ - 1, j1); j++)
                for (int i = Math.Max(0, i0); i <= Math.Min(SiteMap.NX - 1, i1); i++)
                {
                    int c = SiteMap.Index(i, j);
                    if (map.Forest(c) && Terrain.Dist(SiteMap.CX(i), SiteMap.CZ(j), x, z) <= r) n++;
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
    }
}
