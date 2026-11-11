using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// fb4 #566: the storyteller's feather is ONE pole for the whole settlement, however many answers asked for a feather, standing a
    /// little further out than the tents. The spot is picked once by <see cref="FeatherPoleSpots.Pick"/> (pure, no dice), saved
    /// in the box, and every placed feather prop is moved onto it, so the view draws one pole (the first, <see cref="FeatherPoleProp"/>) and
    /// the life planner sends people to that same prop (<c>PlaceKind.Prop</c>). An old save with several stakes by the tents shows the one pole.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The pole: the lowest-id placed feather prop, standing on the pole's spot (null with no placed feather). Safe to ask every frame.</summary>
        public ConsequenceProp FeatherPoleProp
        {
            get
            {
                ConsequenceProp first = null;
                var list = State.consequenceProps;
                if (list == null) return null;
                foreach (var p in list) if (p.placed && p.kind == ConsequenceKind.Feather && (first == null || p.id < first.id)) first = p;
                if (first == null) return null;
                if (!State.featherPoleSet || !FeatherPoleStillFree()) PlaceFeatherPole();
                else if (first.x != State.featherPoleX || first.z != State.featherPoleZ || FeatherStakesApart()) MoveFeathersToPole();
                return first;
            }
        }

        /// <summary>The pole's spot (false while no feather stands).</summary>
        public bool FeatherPoleSpot(out float x, out float z)
        {
            var p = FeatherPoleProp;
            x = p?.x ?? 0f; z = p?.z ?? 0f;
            return p != null;
        }

        /// <summary>Is the saved spot still clear of the buildings (a tent raised since)?</summary>
        bool FeatherPoleStillFree()
        {
            foreach (var b in State.buildings)
                if (b.lotId != 0 && Terrain.Dist(b.x, b.z, State.featherPoleX, State.featherPoleZ) < FeatherPoleSpots.BuildingGap * 0.6f) return false;
            return true;
        }

        void PlaceFeatherPole()
        {
            var st = Settlement;
            float fx = 0f, fz = 0f;
            if (st != null) st.Fire(out fx, out fz, out _);
            var bpts = new List<(float x, float z)>();
            foreach (var b in State.buildings) if (b.lotId != 0) bpts.Add((b.x, b.z));
            var (x, z) = FeatherPoleSpots.Pick(st?.Map, fx, fz, State.seed, bpts, FutureWaterMask(FeatherPoleSpots.WaterMargin));
            State.featherPoleSet = true; State.featherPoleX = x; State.featherPoleZ = z;
            MoveFeathersToPole();
        }

        bool FeatherStakesApart()
        {
            foreach (var p in State.consequenceProps)
                if (p.kind == ConsequenceKind.Feather && p.placed && (p.x != State.featherPoleX || p.z != State.featherPoleZ)) return true;
            return false;
        }

        void MoveFeathersToPole()
        {
            float x = State.featherPoleX, z = State.featherPoleZ;
            foreach (var p in State.consequenceProps)
                if (p.kind == ConsequenceKind.Feather && p.placed) { p.x = x; p.z = z; }
        }

        /// <summary>Debug: a feather pole stands now (a feather prop is left and placed when none stands). Returns true.</summary>
        public bool DebugEnsureFeatherPole()
        {
            if (FeatherPoleProp == null)
            {
                var list = (State.consequenceProps ??= new List<ConsequenceProp>());
                var p = new ConsequenceProp { id = list.Count == 0 ? 1 : list.Max(x => x.id) + 1, dilemmaId = 990000 + list.Count, option = 0, kind = ConsequenceKind.Feather, anchor = PropAnchor.Fire, dueMonth = State.month };
                list.Add(p);
                TryPlaceProp(p, true);
            }
            return FeatherPoleProp != null;
        }
    }

    /// <summary>Where the one feather pole stands. Pure: the same inputs give the same spot.</summary>
    public static class FeatherPoleSpots
    {
        /// <summary>Nearest to the fire (clear of the rack, stash, pot, seats, hide frame), the room the pole and the people round it keep
        /// clear of paths, fields, yards and trees, and from a tent, in box units.</summary>
        public const float MinFire = 1.3f, Room = 0.22f, BuildingGap = 0.5f, TreeGap = 0.25f;
        /// <summary>The pond's future water is kept this far away (the people stand round the pole).</summary>
        public const float WaterMargin = 0.25f;
        /// <summary>How far past the farthest settled thing the pole stands, and the bounds on the ring it aims for.</summary>
        public const float Beyond = 0.35f, MinTarget = 1.5f, MaxTarget = 2.3f;

        static bool Open(SiteMap map, int c, bool[] avoid)
        {
            if (c < 0) return false;
            if (!ApartSpots.CellFree(map, c)) return false;
            if (avoid != null && avoid[c]) return false;
            return true;
        }

        static (float x, float z) Fixed(float fx, float fz, int seed)
        {
            float a0 = Terrain.Hash3(seed, 566, 1) * (float)Math.PI * 2f;
            return (Terrain.Clamp(fx + (float)Math.Cos(a0) * MinTarget, -2.2f, 2.2f), Terrain.Clamp(fz + (float)Math.Sin(a0) * MinTarget, -1.4f, 1.4f));
        }

        /// <summary>The pole's spot. With no map (settling off) a fixed point out from the fire.</summary>
        public static (float x, float z) Pick(SiteMap map, float fx, float fz, int seed, IList<(float x, float z)> buildings, bool[] avoid)
        {
            if (map == null) return Fixed(fx, fz, seed);
            // how far the settled ground reaches from the fire
            float reach = 0f;
            for (int c = 0; c < SiteMap.N; c++)
                if (map.lot[c] != 0) reach = Math.Max(reach, Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), fx, fz));
            if (buildings != null) foreach (var b in buildings) reach = Math.Max(reach, Terrain.Dist(b.x, b.z, fx, fz));
            float target = Terrain.Clamp(reach + Beyond, MinTarget, MaxTarget);
            for (int pass = 0; pass < 3; pass++)
            {
                int best = -1; float bestScore = float.MaxValue;
                for (int c = 0; c < SiteMap.N; c++)
                {
                    float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                    if (Math.Abs(x) > Terrain.HX - ApartSpots.GlassMargin || Math.Abs(z) > Terrain.HZ - ApartSpots.GlassMargin) continue;
                    float d = Terrain.Dist(x, z, fx, fz);
                    if (d < MinFire) continue;
                    if (!FreeAround(map, x, z, pass == 0 ? Room : Room * 0.6f, avoid, pass < 2)) continue;
                    if (buildings != null && TooNear(buildings, x, z, pass == 0 ? BuildingGap : BuildingGap * 0.7f)) continue;
                    // the nearest to the ring at the target distance; a front-glass lean (the Keeper looks from -z) and a little jitter pick between equals
                    float score = Math.Abs(d - target) + 0.08f * (z - fz) + Terrain.Hash3(seed, SiteMap.I(c), SiteMap.J(c)) * 0.06f;
                    if (score < bestScore) { bestScore = score; best = c; }
                }
                if (best >= 0) return (SiteMap.CXc(best), SiteMap.CZc(best));
            }
            return Fixed(fx, fz, seed);
        }

        static bool TooNear(IList<(float x, float z)> pts, float x, float z, float gap)
        {
            foreach (var b in pts) if (Terrain.Dist(b.x, b.z, x, z) < gap) return true;
            return false;
        }

        /// <summary>Every cell within <paramref name="r"/> of (x, z) is open ground (<see cref="ApartSpots.CellFree"/>, outside the future water) and
        /// no tree stands within <see cref="TreeGap"/> (when <paramref name="trees"/>).</summary>
        public static bool FreeAround(SiteMap map, float x, float z, float r, bool[] avoid, bool trees)
        {
            int n = (int)Math.Ceiling(Math.Max(r, TreeGap) / SiteMap.Cell);
            for (int dj = -n; dj <= n; dj++)
                for (int di = -n; di <= n; di++)
                {
                    float px = x + di * SiteMap.Cell, pz = z + dj * SiteMap.Cell;
                    float d2 = (px - x) * (px - x) + (pz - z) * (pz - z);
                    int c = SiteMap.CellAt(px, pz);
                    if (d2 <= r * r + 1e-6f && !Open(map, c, avoid)) return false;
                    if (trees && d2 <= TreeGap * TreeGap + 1e-6f && c >= 0 && map.Forest(c)) return false;
                }
            return true;
        }
    }
}
