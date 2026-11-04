using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>The tribe's fire carried to the first green (#246; <see cref="SimCore.EcoFireMove"/>).</summary>
    public struct EcoFireMoveInfo
    {
        /// <summary>The fire has been carried (at least once).</summary>
        public bool moved;
        /// <summary>The game month it was carried (-1 = not yet).</summary>
        public long month;
        /// <summary>How many times (the debug panel may carry it again): a new number is a new carry to play.</summary>
        public int count;
        /// <summary>Where it stood, and where it burns now (box units).</summary>
        public float fromX, fromZ, toX, toZ;

        public float Distance => (float)Math.Sqrt((toX - fromX) * (toX - fromX) + (toZ - fromZ) * (toZ - fromZ));
    }

    /// <summary>
    /// The tribe moves its fire next to the first green (keepers-hands §2.1, #246). When the Keeper's mist first greens the
    /// dead land, the tribe carries its embers and hearth stones to the green's edge, once: from then the camp's fire, its
    /// gathering, its stash and the fire-side work follow the fire (they all read the hearth's lot). The sim moves the hearth
    /// at once; the people's carrying is the Life planner's scene (<c>LifePlanner.StartFireMove</c>), which the view plays.
    ///
    /// Where: a dry, flat cell at the green's edge (0.22 to 0.45 from the green, so its log seats touch the grass), out of the
    /// hollow (above the flood line), off the water and the rock, clear of the tent (its seats and stash too), inside the
    /// glass, as near the old fire as that allows. No dice: ties go to the lower cell.
    /// Only a box started with this (<see cref="EcoState.fireMoveArmed"/>) moves its fire; an older save and the ecology off
    /// keep theirs. A first green that came up on its own (an odd sprout) doesn't move it: the Keeper's first green does.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The fire was carried to the green (the view starts the people's scene, <c>LifePlanner.StartFireMove</c>).</summary>
        public event Action<EcoFireMoveInfo> EcoFireMoved;

        /// <summary>#246: when and where the tribe carried its fire (moved false until then, and always with the ecology off).</summary>
        public EcoFireMoveInfo EcoFireMove
        {
            get
            {
                var e = State.eco;
                if (!EcologyOn || e.fireMoves <= 0) return new EcoFireMoveInfo { month = -1 };
                return new EcoFireMoveInfo
                {
                    moved = true, month = e.fireMovedMonth, count = e.fireMoves,
                    fromX = e.fireFromX, fromZ = e.fireFromZ, toX = e.fireToX, toZ = e.fireToZ,
                };
            }
        }

        /// <summary>Nearer the old fire than this, the fire stays where it is: it is next to the green already.</summary>
        public const float FireStaysWithin = 0.15f;
        /// <summary>The band round the green the fire goes to: its log seats (0.4 out) reach the grass, the fire stays off it.</summary>
        public const float FireEdgeMin = 0.22f, FireEdgeMax = 0.45f, FireEdgeBest = 0.3f;
        /// <summary>Kept clear of the tent: the seats and the stash (Life/CampStash, 0.46 out) don't touch it.</summary>
        public const float FireTentClearance = 0.5f;
        /// <summary>Kept inside the glass: the seats and the stash stay off the glass margin.</summary>
        const float FireGlassClearance = 0.5f;
        /// <summary>The ground round the fire that must be dry and buildable (no hollow, water or rock).</summary>
        const float FireGroundRadius = 0.3f;

        /// <summary>The Keeper's mist greened <paramref name="greened"/>: on the first such mist the tribe carries its fire
        /// there. <paramref name="force"/> (debug) moves it again, on any box with the living land.</summary>
        bool EcoFireToTheGreen(IReadOnlyList<int> greened, bool force)
        {
            var e = State.eco;
            if (!EcologyOn || !Config.settle || greened == null || greened.Count == 0) return false;
            if (!force && (!e.fireMoveArmed || e.fireMoves > 0 || !BeforeCamp)) return false;
            if (State.era != Era.Camp) return false;
            var st = Settlement;
            st.Fire(out float fx, out float fz, out bool lit);
            if (!lit) return false;
            // No dry ground at the green's edge: it stays, and the next mist that greens tries again (before the camp).
            if (!FireSpotNear(greened, out float x, out float z)) return false;
            if (Terrain.Dist(x, z, fx, fz) < FireStaysWithin) { e.fireMoveArmed = false; return false; } // by the green already: once is spent
            if (!st.MoveFire(x, z)) return false;
            e.fireMoveArmed = false; // once
            e.fireMoves++;
            e.fireMovedMonth = State.month;
            e.fireFromX = fx; e.fireFromZ = fz; e.fireToX = x; e.fireToZ = z;
            EcoFireMoved?.Invoke(EcoFireMove);
            return true;
        }

        /// <summary>
        /// Where the fire goes for the green round <paramref name="greened"/>: the green patch those cells belong to (their
        /// 8-neighbour green cover), then the best dry spot at its edge (see the class summary). False when no spot fits.
        /// Pure: reads the land, the lots and the hearth, writes nothing.
        /// </summary>
        public bool FireSpotNear(IReadOnlyList<int> greened, out float x, out float z)
        {
            x = z = 0f;
            if (!EcologyOn || !Config.settle || greened == null || greened.Count == 0) return false;
            var st = Settlement;
            var cover = State.eco.cover;
            st.Fire(out float fx, out float fz, out _);

            // The patch: the greened cells and the green cover joined to them.
            var patch = new List<int>();
            var seen = new bool[SiteMap.N];
            var queue = new Queue<int>();
            foreach (int c in greened)
                if (c >= 0 && c < SiteMap.N && !seen[c]) { seen[c] = true; queue.Enqueue(c); }
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                patch.Add(c);
                int ci = SiteMap.I(c), cj = SiteMap.J(c);
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int i = ci + di, j = cj + dj;
                        if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) continue;
                        int n = SiteMap.Index(i, j);
                        if (seen[n] || !Green(cover[n])) continue;
                        seen[n] = true; queue.Enqueue(n);
                    }
            }

            var tents = new List<Lot>();
            foreach (var l in st.Lots) if (!l.open && !l.retired) tents.Add(l);
            // Where it burns already fits (the debug panel's second carry, a first green round the camp): it stays.
            if (FireSpotFits(fx, fz, patch, tents, out _)) { x = fx; z = fz; return true; }
            int best = -1;
            float bestScore = float.MaxValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                float cx = SiteMap.CXc(c), cz = SiteMap.CZc(c);
                if (!FireSpotFits(cx, cz, patch, tents, out float edge)) continue;
                float score = Terrain.Dist(cx, cz, fx, fz) + 1.5f * Math.Abs(edge - FireEdgeBest);
                if (score < bestScore) { bestScore = score; best = c; }
            }
            if (best < 0) return false;
            x = SiteMap.CXc(best); z = SiteMap.CZc(best);
            return true;
        }

        /// <summary>A fire at (x, z) would be next to the <paramref name="patch"/> (<paramref name="edge"/> from it) on legal ground.</summary>
        bool FireSpotFits(float x, float z, List<int> patch, List<Lot> tents, out float edge)
        {
            edge = float.MaxValue;
            const float mx = Terrain.HX - Terrain.GlassMargin - FireGlassClearance, mz = Terrain.HZ - Terrain.GlassMargin - FireGlassClearance;
            if (Math.Abs(x) > mx || Math.Abs(z) > mz) return false;
            var st = Settlement;
            var cover = State.eco.cover;
            int c = SiteMap.CellAt(x, z);
            if (c < 0 || Green(cover[c])) return false;
            foreach (int g in patch) edge = Math.Min(edge, Terrain.Dist(x, z, SiteMap.CXc(g), SiteMap.CZc(g)));
            if (edge < FireEdgeMin || edge > FireEdgeMax) return false;
            if (st.WalkCost(c) <= 0 || !DryGround(st.Map, cover, x, z, FireGroundRadius)) return false;
            // the stash pile (Life/CampStash: 3.55 rad, 0.46 out) must stand on dry ground too
            if (!DryGround(st.Map, cover, x + (float)Math.Cos(3.55) * 0.46f, z + (float)Math.Sin(3.55) * 0.46f, 0.05f)) return false;
            foreach (var t in tents)
                if (PointToLot(x, z, t) < FireTentClearance) return false;
            return true;
        }

        static bool Green(byte cover) => cover != (byte)EcoCover.Bare && cover != (byte)EcoCover.Water;

        /// <summary>Every cell within <paramref name="r"/> of (x, z) is dry, buildable, flat enough and free of lots.</summary>
        static bool DryGround(SiteMap map, byte[] cover, float x, float z, float r)
        {
            int c0 = SiteMap.CellAt(x, z);
            if (c0 < 0) return false;
            int reach = (int)Math.Ceiling(r / SiteMap.Cell);
            int ci = SiteMap.I(c0), cj = SiteMap.J(c0);
            for (int dj = -reach; dj <= reach; dj++)
                for (int di = -reach; di <= reach; di++)
                {
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) return false;
                    int c = SiteMap.Index(i, j);
                    if (c != c0 && Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) > r) continue;
                    if (map.Water(c) || map.NoBuild(c) || map.Rock(c) || map.lot[c] != 0 || map.slope[c] > 0.5f || cover[c] == (byte)EcoCover.Water) return false;
                }
            return true;
        }

        /// <summary>Distance from a point to a lot's rectangle (0 inside).</summary>
        static float PointToLot(float x, float z, Lot l)
        {
            float r = l.yaw * (MathF.PI / 180f);
            float cs = MathF.Cos(r), sn = MathF.Sin(r);
            float dx = x - l.x, dz = z - l.z;
            float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs; // as SiteMap.RectCells
            float ox = Math.Max(0f, Math.Abs(lx) - l.w / 2f), oz = Math.Max(0f, Math.Abs(lz) - l.d / 2f);
            return (float)Math.Sqrt(ox * ox + oz * oz);
        }

        /// <summary>
        /// Debug ("move the fire to the green now"): carry the fire to the nearest green's edge now, as the first mist does, on
        /// any box with the living land (an old save too) and again after a carry. With no green yet, the Keeper mists the
        /// first run's spot first. False when the ecology is off, the camp is over, or no spot fits (or the fire is there).
        /// </summary>
        public bool DebugMoveFireNow()
        {
            if (!EcologyOn || !Config.settle || State.era != Era.Camp) return false;
            var cells = new List<int>();
            var cover = State.eco.cover;
            for (int c = 0; c < SiteMap.N; c++) if (Green(cover[c])) cells.Add(c);
            if (cells.Count == 0)
            {
                var spot = State.firstRun != null && (State.firstRun.rainX != 0f || State.firstRun.rainZ != 0f)
                    ? (State.firstRun.rainX, State.firstRun.rainZ) : FirstGreenSpot();
                int before = State.eco.fireMoves;
                cells.AddRange(Mist(spot.Item1, spot.Item2, 2f));
                if (State.eco.fireMoves > before) return true; // that first green carried it, as in play
                if (cells.Count == 0) return false;
            }
            else
            {
                // the green nearest the fire: its patch
                Settlement.Fire(out float fx, out float fz, out _);
                int near = cells[0];
                foreach (int c in cells)
                    if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), fx, fz) < Terrain.Dist(SiteMap.CXc(near), SiteMap.CZc(near), fx, fz)) near = c;
                cells.Clear(); cells.Add(near);
            }
            return EcoFireToTheGreen(cells, true);
        }

        /// <summary>A spot out on the dry ground past the camp, away from the pond (as the first run's first mist).</summary>
        (float, float) FirstGreenSpot()
        {
            var spec = TerrainSpec.Preset(TerrainPreset);
            float dx = spec.centreX - spec.pondX, dz = spec.centreZ - spec.pondZ;
            float len = Math.Max(0.001f, (float)Math.Sqrt(dx * dx + dz * dz));
            dx /= len; dz /= len;
            return (spec.centreX - dx * 1.15f + dz * 0.25f, spec.centreZ - dz * 1.15f - dx * 0.25f);
        }
    }
}
