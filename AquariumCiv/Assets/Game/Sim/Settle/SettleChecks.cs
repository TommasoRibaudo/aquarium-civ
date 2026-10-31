using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>The measurable criteria of research 08 §5 / Growth Lab (sanity, emergence) on a settlement. Used by the
    /// tests and handy for the debug panel ("is the layout sane?").</summary>
    public static class SettleChecks
    {
        public struct Sanity
        {
            public int lots, overlaps, inMargin, unconnected, waiting, buildings, unplacedBuildings;
            public override string ToString() =>
                $"{buildings} buildings on {lots} lots, overlaps {overlaps}, in margin {inMargin}, no path to green {unconnected}, waiting for a lot {waiting}";
        }

        public static Sanity Check(Settlement w, List<Building> buildings)
        {
            var r = new Sanity { waiting = w.Waiting(), buildings = buildings.Count };
            var lots = new List<Lot>();
            foreach (var s in w.Lots) if (!s.open && !s.retired) lots.Add(s);
            r.lots = lots.Count;
            for (int a = 0; a < lots.Count; a++)
                for (int b = a + 1; b < lots.Count; b++)
                    if (SiteMap.RectGap(lots[a], lots[b]) < 0f) r.overlaps++;
            const float mx = Terrain.HX - Terrain.GlassMargin + 1e-4f, mz = Terrain.HZ - Terrain.GlassMargin + 1e-4f;
            var cx = new float[4]; var cz = new float[4];
            foreach (var s in lots)
            {
                SiteMap.Corners(s.x, s.z, s.yaw, s.w, s.d, cx, cz);
                for (int k = 0; k < 4; k++)
                    if (MathF.Abs(cx[k]) > mx || MathF.Abs(cz[k]) > mz) { r.inMargin++; break; }
            }
            var reach = ReachFromGreen(w.Map);
            foreach (var s in lots)
                if (s.kind != LotKind.Outbuilding && (s.door < 0 || !reach[s.door])) r.unconnected++;
            foreach (var b in buildings) if (b.lotId == 0) r.unplacedBuildings++;
            return r;
        }

        /// <summary>Cells reachable from the green over trails/paths/roads (8-neighbour), never through a lot.</summary>
        public static bool[] ReachFromGreen(SiteMap m)
        {
            var mark = new bool[SiteMap.N];
            var q = new Queue<int>();
            for (int c = 0; c < SiteMap.N; c++) if (m.Green(c) && m.lot[c] == 0) { mark[c] = true; q.Enqueue(c); }
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
            public int pathCells, emergentCells, largestEmergent, mainStreet, roadCells;
            public override string ToString() =>
                $"path+ cells {pathCells} (roads {roadCells}), emergent (never laid) {emergentCells}, largest emergent run {largestEmergent}, main street {mainStreet} cells";
        }

        /// <summary>Paths (tier ≥ path) that no connection step laid, and the biggest connected path component.</summary>
        public static Emergence Emerge(SiteMap m)
        {
            var e = new Emergence();
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (m.tier[c] >= PathNet.PathTier) { e.pathCells++; if (!m.Laid(c)) e.emergentCells++; }
                if (m.tier[c] >= PathNet.Road) e.roadCells++;
            }
            e.largestEmergent = Largest(c => m.tier[c] >= PathNet.PathTier && !m.Laid(c));
            e.mainStreet = Largest(c => m.tier[c] >= PathNet.PathTier);
            return e;
        }

        static int Largest(Func<int, bool> inSet)
        {
            var seen = new bool[SiteMap.N];
            int best = 0;
            var q = new Queue<int>();
            for (int s = 0; s < SiteMap.N; s++)
            {
                if (seen[s] || !inSet(s)) continue;
                int count = 0;
                seen[s] = true; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(); count++;
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
                if (count > best) best = count;
            }
            return best;
        }
    }
}
