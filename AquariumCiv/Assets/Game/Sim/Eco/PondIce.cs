using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #618: winter on the pond. Pure and read-only (no save, no dice), like <see cref="PondShore"/>.
    /// <list type="bullet">
    /// <item>The pond <b>freezes</b> in the calendar's winter (months 11, 0, 1 of the box's calendar, as <c>LifeClock.SeasonOf</c>), any pond with water.</item>
    /// <item>People go <b>on the ice</b> (fishing through a hole, kids sliding) only on a pond of at least <see cref="EcoTuning.iceActivityCells"/>
    /// open-water cells (the user's rule, 2026-11-12: "make sure actions happen to a pond that is big enough"). A smaller pond ices over as a look only,
    /// and nobody fishes it in winter. No pond: nothing freezes.</item>
    /// <item>The wild <b>ducks are away</b> through the winter (they leave at the first freeze and are back in spring); nothing takes them or their eggs
    /// while they are gone. Their count is kept.</item>
    /// <item>Spots on the ice keep off the shore: only water cells whose eight neighbours are water too (<see cref="Inner"/>).</item>
    /// </list>
    /// </summary>
    public static class PondIce
    {
        /// <summary>Is a box calendar month winter (11, 0, 1)? The same split as LifeClock.SeasonOf.</summary>
        public static bool WinterMonth(int calendarMonth) => calendarMonth <= 1 || calendarMonth == 11;
        /// <summary>Is the sim month / view day in winter on the box's calendar?</summary>
        public static bool Winter(long monthOrDay) => WinterMonth(SeasonCalendar.OfMonth(monthOrDay));

        /// <summary>Debug ("Ice: on / off / as the season"): null follows the calendar. Not saved.</summary>
        public static bool? Force;

        /// <summary>The pond is frozen on that view day (a sim month): winter and some open water. False with the ecology off.</summary>
        public static bool Frozen(SimCore sim, long day) => sim != null && sim.EcologyOn && sim.EcoPondCells > 0 && (Force ?? Winter(day));

        /// <summary>The pond is big enough for people on the ice (any season; see <see cref="EcoTuning.iceActivityCells"/>).</summary>
        public static bool BigEnough(SimCore sim) => sim != null && sim.EcologyOn && sim.EcoPondCells >= sim.Config.eco.iceActivityCells && InnerCount(sim) > 0;

        /// <summary>People may be on the ice that day: frozen, and big enough.</summary>
        public static bool OnIce(SimCore sim, long day) => Frozen(sim, day) && BigEnough(sim);

        /// <summary>No fishing that day: the pond is frozen and too small to fish through the ice.</summary>
        public static bool FishingStopped(SimCore sim, long day) => Frozen(sim, day) && !BigEnough(sim);

        /// <summary>The wild ducks are away (winter), whatever the pond's size. False with no ducks.</summary>
        public static bool DucksAway(SimCore sim, long day) => sim != null && sim.EcologyOn && sim.EcoDucks > 0 && (Force ?? Winter(day));

        // ---------------------------------------------------------------- where on the ice

        static readonly int[] D8I = { 1, -1, 0, 0, 1, 1, -1, -1 }, D8J = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>A water cell all of whose eight neighbours are water too: ice that is well off the shore.</summary>
        public static bool Inner(SimCore sim, int c)
        {
            if (c < 0 || c >= SiteMap.N || sim.EcoCoverAt(c) != EcoCover.Water) return false;
            int ci = c % SiteMap.NX, cj = c / SiteMap.NX;
            for (int k = 0; k < 8; k++)
            {
                int i = ci + D8I[k], j = cj + D8J[k];
                if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) return false;
                if (sim.EcoCoverAt(j * SiteMap.NX + i) != EcoCover.Water) return false;
            }
            return true;
        }

        public static int InnerCount(SimCore sim)
        {
            if (sim == null || !sim.EcologyOn) return 0;
            int n = 0;
            for (int c = 0; c < SiteMap.N; c++) if (Inner(sim, c)) n++;
            return n;
        }

        /// <summary>Is the point on inner ice (<see cref="Inner"/>)?</summary>
        public static bool OnInner(SimCore sim, float x, float z) => Inner(sim, SiteMap.CellAt(x, z));

        /// <summary>The inner cells, nearest to (fromX, fromZ) first.</summary>
        public static List<int> InnerByDistance(SimCore sim, float fromX, float fromZ)
        {
            var list = new List<int>();
            if (sim == null || !sim.EcologyOn) return list;
            for (int c = 0; c < SiteMap.N; c++) if (Inner(sim, c)) list.Add(c);
            list.Sort((a, b) => Terrain.Dist(SiteMap.CXc(a), SiteMap.CZc(a), fromX, fromZ).CompareTo(Terrain.Dist(SiteMap.CXc(b), SiteMap.CZc(b), fromX, fromZ)));
            return list;
        }

        /// <summary>Steps (8-neighbour cells) from each water cell to the nearest non-water cell; -1 off the water.</summary>
        public static int[] ShoreSteps(SimCore sim, out int max)
        {
            var d = new int[SiteMap.N];
            var q = new Queue<int>();
            max = 0;
            for (int c = 0; c < SiteMap.N; c++)
            {
                d[c] = -1;
                if (sim.EcoCoverAt(c) == EcoCover.Water)
                {   // the glass is a shore too: a pond that runs to the pane has its middle away from it
                    int bi = c % SiteMap.NX, bj = c / SiteMap.NX;
                    if (bi == 0 || bj == 0 || bi == SiteMap.NX - 1 || bj == SiteMap.NZ - 1) { d[c] = 1; q.Enqueue(c); }
                    continue;
                }
                d[c] = 0; q.Enqueue(c);
            }
            while (q.Count > 0)
            {
                int c = q.Dequeue(), ci = c % SiteMap.NX, cj = c / SiteMap.NX;
                for (int k = 0; k < 8; k++)
                {
                    int i = ci + D8I[k], j = cj + D8J[k];
                    if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) continue;
                    int o = j * SiteMap.NX + i;
                    if (d[o] >= 0) continue;
                    d[o] = d[c] + 1; if (d[o] > max) max = d[o];
                    q.Enqueue(o);
                }
            }
            for (int c = 0; c < SiteMap.N; c++) if (sim.EcoCoverAt(c) != EcoCover.Water) d[c] = -1;
            return d;
        }

        /// <summary>The share of the deepest shore distance a spot needs to count as "the middle of the pond".</summary>
        public const float MiddleShare = 0.6f;

        /// <summary>#618 (user, 2026-11-12: "more towards the centre of the lake"): the inner cells in the middle of the pond (at least
        /// <see cref="MiddleShare"/> of the deepest shore distance, and never under 2 steps from the shore) nearest (fromX, fromZ) first, so
        /// what happens on the ice stays in view from the camp; then the rest of the inner ice, deepest first.</summary>
        public static List<int> InnerByMiddle(SimCore sim, float fromX, float fromZ)
        {
            var list = new List<int>();
            if (sim == null || !sim.EcologyOn) return list;
            var steps = ShoreSteps(sim, out int max);
            int need = Math.Max(2, (int)Math.Ceiling(max * MiddleShare));
            var mid = new List<int>(); var rest = new List<int>();
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (!Inner(sim, c)) continue;
                (steps[c] >= need ? mid : rest).Add(c);
            }
            mid.Sort((a, b) => Terrain.Dist(SiteMap.CXc(a), SiteMap.CZc(a), fromX, fromZ).CompareTo(Terrain.Dist(SiteMap.CXc(b), SiteMap.CZc(b), fromX, fromZ)));
            rest.Sort((a, b) => steps[b].CompareTo(steps[a]));
            list.AddRange(mid); list.AddRange(rest);
            return list;
        }

        /// <summary>Holes keep this far apart (u).</summary>
        public const float HoleGap = 0.2f;
        /// <summary>The fisher sits this far from the hole (u), side-on to the default camera (yaw 28), so neither hides the other.</summary>
        public const float HoleReach = 0.08f;
        /// <summary>The default camera's direction from what it looks at, in world x/z (BoxCamera's home yaw 28, as HuntOpen).</summary>
        public static readonly float CamX = -(float)Math.Sin(28.0 * Math.PI / 180.0), CamZ = -(float)Math.Cos(28.0 * Math.PI / 180.0);

        /// <summary>
        /// Fisher <paramref name="slot"/>'s hole on the ice: the inner cells nearest (fromX, fromZ), the k-th that is <see cref="HoleGap"/> from the
        /// holes before it; the fisher sits <see cref="HoleReach"/> beside it, side-on to the camera, on inner ice. False when the ice has no room.
        /// </summary>
        public static bool FishingSpot(SimCore sim, float fromX, float fromZ, int slot, out float sitX, out float sitZ, out float holeX, out float holeZ)
        {
            sitX = sitZ = holeX = holeZ = 0f;
            var cells = InnerByMiddle(sim, fromX, fromZ);
            var holes = new List<(float x, float z)>();
            foreach (int c in cells)
            {
                float hx = SiteMap.CXc(c), hz = SiteMap.CZc(c);
                bool apart = true;
                foreach (var h in holes) if (Terrain.Dist(h.x, h.z, hx, hz) < HoleGap) { apart = false; break; }
                if (!apart) continue;
                // side-on to the camera: the sitter is beside the hole across the view, on whichever side is inner ice
                float px = -CamZ, pz = CamX;
                float sx = hx + px * HoleReach, sz = hz + pz * HoleReach;
                if (!OnInner(sim, sx, sz)) { sx = hx - px * HoleReach; sz = hz - pz * HoleReach; }
                if (!OnInner(sim, sx, sz)) continue;
                if (holes.Count == slot % 8) { sitX = sx; sitZ = sz; holeX = hx; holeZ = hz; return true; }
                holes.Add((hx, hz));
            }
            return false;
        }

        /// <summary>The kids' slide: about <see cref="SlideLength"/> long (at least <see cref="SlideMin"/>).</summary>
        public const float SlideLength = 0.55f, SlideMin = 0.3f;

        /// <summary>The kids' slide line: every point on inner ice, nearest (fromX, fromZ) first, across the camera's view so it
        /// reads side-on (then the diagonals). False: the ice has no room for one.</summary>
        /// <summary>The slide keeps this far from the fishers' holes (u), and has this much inner ice behind its start for the queue and the run-up.</summary>
        public const float SlideFromHoles = 0.35f, RunUpRoom = 0.3f;

        public static bool SlideLine(SimCore sim, float fromX, float fromZ, out float ax, out float az, out float bx, out float bz)
        {
            ax = az = bx = bz = 0f;
            var cells = InnerByMiddle(sim, fromX, fromZ);
            // the holes the fishers would cut (FishingSpot from the camp's fire, as PeopleView asks), so the slide never runs through them
            var holes = new List<(float x, float z)>();
            var g = sim.EcoGround;
            for (int k = 0; k < 5; k++) if (FishingSpot(sim, g.campX, g.campZ, k, out _, out _, out float hx, out float hz)) holes.Add((hx, hz));
            bool NearHole(float x, float z) { foreach (var h in holes) if (Terrain.Dist(h.x, h.z, x, z) < SlideFromHoles) return true; return false; }
            float bestLen = 0f;
            int tried = 0;
            double across = Math.Atan2(CamX, -CamZ);
            foreach (int c in cells)
            {
                if (tried > 80) break;
                float sx = SiteMap.CXc(c), sz = SiteMap.CZc(c);
                if (NearHole(sx, sz)) continue;
                tried++;
                for (int k = 0; k < 6; k++)
                {
                    double a = across + (k / 2) * 0.5 * (k % 2 == 0 ? 1 : -1) + (k % 2 == 1 && k < 2 ? Math.PI : 0);
                    if (k == 1) a = across + Math.PI;
                    float dx = (float)Math.Cos(a), dz = (float)Math.Sin(a);
                    bool room = true;
                    for (float s = 0.05f; s <= RunUpRoom + 1e-4f && room; s += 0.05f) room = OnInner(sim, sx - dx * s, sz - dz * s) && !NearHole(sx - dx * s, sz - dz * s);
                    if (!room) continue;
                    float len = 0f;
                    for (float s = 0.03f; s <= SlideLength + 1e-4f; s += 0.03f) { if (!OnInner(sim, sx + dx * s, sz + dz * s) || NearHole(sx + dx * s, sz + dz * s)) break; len = s; }
                    if (len > bestLen + 0.05f) { bestLen = len; ax = sx; az = sz; bx = sx + dx * len; bz = sz + dz * len; }
                    if (bestLen >= SlideLength - 0.03f) return true;
                }
            }
            return bestLen >= SlideMin;
        }
    }
}
