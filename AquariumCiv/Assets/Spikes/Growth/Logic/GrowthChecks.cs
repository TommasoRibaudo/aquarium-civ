using System;
using System.Collections.Generic;

namespace AquariumCiv.Spikes.Growth
{
    /// <summary>The measurable pass criteria of research 08 §5 (sanity, emergence) on a world or a snapshot.</summary>
    public static class GrowthChecks
    {
        public struct Sanity
        {
            public int lots, overlaps, inMargin, unconnected, unplaced;
            public override string ToString() => $"lots {lots}, overlaps {overlaps}, in margin {inMargin}, no path to green {unconnected}, unplaced {unplaced}";
        }

        public static bool HasLot(Kind k) => k != Kind.Camp && k != Kind.Well;

        public static Sanity Check(GrowthWorld w)
        {
            var r = new Sanity { unplaced = w.unplaced };
            var lots = new List<Site>();
            foreach (var s in w.sites) if (HasLot(s.kind)) lots.Add(s);
            r.lots = lots.Count;
            for (int a = 0; a < lots.Count; a++)
                for (int b = a + 1; b < lots.Count; b++)
                    if (SiteMap.RectGap(lots[a], lots[b]) < 0f) r.overlaps++;
            const float mx = GrowthTerrain.HX - GrowthTerrain.GlassMargin + 1e-4f, mz = GrowthTerrain.HZ - GrowthTerrain.GlassMargin + 1e-4f;
            var cx = new float[4]; var cz = new float[4];
            foreach (var s in lots)
            {
                SiteMap.Corners(s.x, s.z, s.yaw, s.w, s.d, cx, cz);
                for (int k = 0; k < 4; k++)
                    if (MathF.Abs(cx[k]) > mx || MathF.Abs(cz[k]) > mz) { r.inMargin++; break; }
            }
            var reach = ReachFromGreen(w.map);
            foreach (var s in lots)
                if (s.doorCell < 0 || !reach[s.doorCell]) r.unconnected++;
            return r;
        }

        /// <summary>Cells reachable from the green over trails/paths/roads (8-neighbour), never through a lot.</summary>
        public static bool[] ReachFromGreen(SiteMap m)
        {
            var mark = new bool[SiteMap.N];
            var q = new Queue<int>();
            for (int c = 0; c < SiteMap.N; c++) if (m.green[c] && m.lot[c] == 0) { mark[c] = true; q.Enqueue(c); }
            while (q.Count > 0)
            {
                int c = q.Dequeue(), i = SiteMap.I(c), j = SiteMap.J(c);
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                        int nc = SiteMap.Index(ni, nj);
                        if (mark[nc] || m.lot[nc] != 0 || m.tier[nc] < PathNet.Trail) continue;
                        mark[nc] = true; q.Enqueue(nc);
                    }
            }
            return mark;
        }

        public struct Emergence
        {
            public int pathCells, emergentCells, largestEmergent, mainStreet, mainStreetEmergent, roadCells;
            public override string ToString() =>
                $"path+ cells {pathCells} (roads {roadCells}), emergent (never laid) {emergentCells}, largest emergent run {largestEmergent}, main street {mainStreet} cells ({mainStreetEmergent} emergent)";
        }

        /// <summary>Paths (tier ≥ path) that no connection step laid, and the biggest connected path component (the
        /// "main street").</summary>
        public static Emergence Emerge(byte[] tier, Func<int, bool> laid)
        {
            var e = new Emergence();
            var comp = new int[SiteMap.N];
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (tier[c] >= PathNet.PathTier) { e.pathCells++; if (!laid(c)) e.emergentCells++; }
                if (tier[c] >= PathNet.Road) e.roadCells++;
            }
            // components of emergent cells
            e.largestEmergent = Largest(c => tier[c] >= PathNet.PathTier && !laid(c), out _, null);
            e.mainStreet = Largest(c => tier[c] >= PathNet.PathTier, out var members, null);
            foreach (int c in members) if (!laid(c)) e.mainStreetEmergent++;
            return e;
        }

        static int Largest(Func<int, bool> inSet, out List<int> best, object _)
        {
            var seen = new bool[SiteMap.N];
            best = new List<int>();
            var q = new Queue<int>();
            for (int s = 0; s < SiteMap.N; s++)
            {
                if (seen[s] || !inSet(s)) continue;
                var cur = new List<int>();
                seen[s] = true; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(); cur.Add(c);
                    int i = SiteMap.I(c), j = SiteMap.J(c);
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int ni = i + di, nj = j + dj;
                            if (ni < 0 || nj < 0 || ni >= SiteMap.NX || nj >= SiteMap.NZ) continue;
                            int nc = SiteMap.Index(ni, nj);
                            if (seen[nc] || !inSet(nc)) continue;
                            seen[nc] = true; q.Enqueue(nc);
                        }
                }
                if (cur.Count > best.Count) best = cur;
            }
            return best.Count;
        }

        public static Emergence Emerge(GrowthWorld w) => Emerge(w.map.tier, c => w.map.laid[c]);

        public struct Alignment
        {
            public int pairs, parallel, inLine;
            public float ParallelShare => pairs > 0 ? parallel / (float)pairs : 0f;
            public float InLineShare => pairs > 0 ? inLine / (float)pairs : 0f;
            public override string ToString() => $"neighbouring home pairs {pairs}: parallel (yaw within 5°) {parallel} ({100f * ParallelShare:0}%), fronts in line {inLine} ({100f * InLineShare:0}%)";
        }

        /// <summary>How "row-like" the homes are: neighbouring homes (gap under 0.5 u) that are parallel (yaw within 5°,
        /// modulo 90°), and of those, how many also have their fronts on one line (a street row).</summary>
        public static Alignment Align(IList<Site> sites)
        {
            var a = new Alignment();
            var homes = new List<Site>();
            foreach (var s in sites) if (!s.retired && Encyclopedia.IsHome(s.kind)) homes.Add(s);
            for (int i = 0; i < homes.Count; i++)
                for (int j = i + 1; j < homes.Count; j++)
                {
                    Site p = homes[i], q = homes[j];
                    if (GrowthTerrain.Dist(p.x, p.z, q.x, q.z) > 1.2f || SiteMap.RectGap(p, q) > 0.5f) continue;
                    a.pairs++;
                    float dy = Math.Abs(((q.yaw - p.yaw) % 90f + 90f) % 90f);
                    if (Math.Min(dy, 90f - dy) > 5f) continue;
                    a.parallel++;
                    p.Front(out float fx, out float fz);
                    float frontP = p.d / 2f, frontQ = q.d / 2f;
                    // offset of q's front line from p's front line, along p's front direction
                    float along = (q.x - p.x) * fx + (q.z - p.z) * fz + frontQ - frontP;
                    float same = ((q.yaw - p.yaw) % 360f + 360f) % 360f; // facing the same way, not back to back
                    if (Math.Abs(along) < 0.06f && (same < 5f || same > 355f)) a.inLine++;
                }
            return a;
        }

        public struct NudgeTrial
        {
            public string kind; public int seed, preset; public long month; public float x, z;
            public bool answered; public Kind answerKind; public float dist, distToCanal;
        }

        /// <summary>Criterion 2: one shaping on open ground at a seeded time and spot; how far away the people answer.</summary>
        public static NudgeTrial RunNudgeTrial(string kind, int i, Variant variant = Variant.Organic)
        {
            int seed = 1000 + i * 7919 + kind.Length * 31, preset = i % 3;
            var w = new GrowthWorld(seed, preset, variant) { keepSnapshots = false };
            int year = 8 + (int)(GrowthTerrain.Hash3(seed, 1, 0) * 55f);
            w.RunToYear(year);
            float x = -2.2f + 4.4f * GrowthTerrain.Hash3(seed, 2, 0), z = -1.5f + 3.0f * GrowthTerrain.Hash3(seed, 3, 0);
            w.OpenSpotNear(ref x, ref z, 0.45f);
            var t = new NudgeTrial { kind = kind, seed = seed, preset = preset, month = w.State.month, x = x, z = z, dist = 99f, distToCanal = 99f };
            var n = w.Shape(kind, x, z);
            if (n == null) return t;
            for (int m = 0; m < 48 && n.siteId < 0; m++) w.StepMonth();
            var s = w.sites.Find(q => q.id == n.siteId);
            if (s == null) return t;
            t.answered = true; t.answerKind = s.kind;
            t.dist = GrowthTerrain.Dist(s.x, s.z, x, z);
            for (int c = 0; c < SiteMap.N; c++)
                if (w.map.canal[c]) t.distToCanal = Math.Min(t.distToCanal, GrowthTerrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), s.x, s.z));
            return t;
        }

        /// <summary>The scripted Keeper used in the time-lapse runs: rain, a grove, a channel, at spots set by the terrain.</summary>
        public static List<ScriptEvent> DefaultScript(int terrainPreset)
        {
            var t = TerrainSpec.Preset(terrainPreset);
            float sx = t.mirror ? -1f : 1f;
            var list = new List<ScriptEvent>
            {
                new ScriptEvent(14 * 12 + 4, "rain", t.centreX + sx * 1.4f, t.centreZ + 1.0f, true),
                new ScriptEvent(33 * 12 + 2, "grove", t.centreX - sx * 1.6f, t.centreZ - 1.1f, true),
                new ScriptEvent(58 * 12 + 7, "channel", t.centreX + sx * 0.9f, t.centreZ + 1.25f, true),
            };
            if (terrainPreset % 3 == 2)
            {
                list[0] = new ScriptEvent(14 * 12 + 4, "rain", t.centreX + 1.5f, t.centreZ - 0.6f, true);
                list[2] = new ScriptEvent(58 * 12 + 7, "channel", t.centreX + 0.8f, t.centreZ - 0.9f, true);
            }
            return list;
        }
    }
}
