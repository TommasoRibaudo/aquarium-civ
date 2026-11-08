using System;
using System.Linq;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #404 (playtest note #386; the user 2026-11-07): the people who live apart give the Keeper a lookout tower in the town. A few months into
    /// the town era the group (the shore family first, else the oldest group with someone alive) begins it by their camp; it goes up in
    /// three stages the player can watch over a few visits (a frame, the platform and ladder, then the roof and lamp), once per box. When it
    /// stands, the chronicle says so. People use it (the view and the planner): the lamp is lit at dusk, someone keeps a lookout by day, and
    /// townsfolk walk out to it in the evening. No economy.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Months into the town era before the tower is begun.</summary>
        public const int TowerAfterTownMonths = 6;
        /// <summary>Months each stage takes: a frame, then the platform and ladder, then the roof and lamp (done).</summary>
        public const int TowerStageMonths = 2;
        /// <summary>The tower's stage when done.</summary>
        public const int TowerDone = 3;

        /// <summary>The group building or keeping the tower, or null (once per box).</summary>
        public ApartGroup TowerGroup => State.apartGroups?.FirstOrDefault(g => g.tower);

        /// <summary>0 none, 1 the frame, 2 the platform and ladder, 3 done (roof and lamp).</summary>
        public int TowerStage(ApartGroup g)
        {
            if (g == null || !g.tower) return 0;
            return Math.Min(TowerDone, 1 + (int)Math.Max(0, (State.month - g.towerFrom) / TowerStageMonths));
        }

        /// <summary>The tower stands (stage 3): people may use it.</summary>
        public bool TowerStands => TowerStage(TowerGroup) >= TowerDone;

        /// <summary>Monthly, from <see cref="ApartStep"/>. No dice.</summary>
        void TowerStep()
        {
            var gs = State.apartGroups;
            if (gs == null || gs.Count == 0 || State.era < Era.Town) return;
            var t = TowerGroup;
            if (t != null)
            {
                if (!t.towerDone && TowerStage(t) >= TowerDone)
                {
                    t.towerDone = true;
                    string where = t.kind == ApartKind.ShoreHut ? "on the far shore" : "at the edge of the far woods";
                    Chronicle("gift", $"The {t.family} family finished a lookout tower {where}, a gift for the Keeper. Its lamp is lit at dusk.", 0.75f);
                }
                return;
            }
            if (State.month - State.eraStartMonth < TowerAfterTownMonths) return;
            var g = gs.Where(x => x.AnyAlive).OrderBy(x => x.kind == ApartKind.ShoreHut ? 0 : 1).ThenBy(x => x.sinceMonth).FirstOrDefault();
            if (g == null) return;
            BeginTower(g);
        }

        void BeginTower(ApartGroup g)
        {
            TowerSpot(g);
            g.tower = true;
            g.towerFrom = State.month;
            g.towerDone = false;
        }

        /// <summary>A free cell 0.3-0.65 from the group's home spot: no pond, lot, lot ring, canal, green, commons, yard or rock, not hard
        /// against the glass. A shore family's stands by the water; a woods camp's away from the settlement. Read-only on the map.</summary>
        void TowerSpot(ApartGroup g)
        {
            var map = Settlement?.Map;
            if (map == null) { g.towerX = g.x + 0.35f; g.towerZ = g.z; return; }
            const byte block = SiteMap.FCanal | SiteMap.FGreen | SiteMap.FCommons | SiteMap.FYard | SiteMap.FRock | SiteMap.FLaid;
            int best = -1; float bestScore = float.MinValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                int i = SiteMap.I(c), j = SiteMap.J(c);
                if (i < 3 || j < 3 || i >= SiteMap.NX - 3 || j >= SiteMap.NZ - 3) continue;
                float dx = SiteMap.CXc(c) - g.x, dz = SiteMap.CZc(c) - g.z, d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < 0.3f || d > 0.65f) continue;
                if (map.pond[c] || map.lot[c] != 0 || (map.ring != null && map.ring[c] != 0) || (map.flags[c] & block) != 0) continue;
                if (map.wear != null && map.wear[c] >= PathNet.TTrail) continue;
                float score = -Math.Abs(d - 0.4f);
                if (g.kind == ApartKind.ShoreHut)
                {
                    bool shore = map.pond[c - 1] || map.pond[c + 1] || map.pond[c - SiteMap.NX] || map.pond[c + SiteMap.NX];
                    if (shore) score += 1f;
                }
                else
                {
                    float gx = SiteMap.CXc(c) - map.greenX, gz = SiteMap.CZc(c) - map.greenZ;
                    score += 0.3f * (float)Math.Sqrt(gx * gx + gz * gz);
                }
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best < 0) { g.towerX = g.x + 0.35f; g.towerZ = g.z; return; }
            g.towerX = SiteMap.CXc(best);
            g.towerZ = SiteMap.CZc(best);
        }

        // ---------------------------------------------------------------- debug (the debug panel)

        /// <summary>Debug: begin the tower now (the first group with someone alive), whatever the era.</summary>
        public string DebugTowerBegin()
        {
            var g = TowerGroup ?? State.apartGroups?.Where(x => x.AnyAlive).OrderBy(x => x.kind == ApartKind.ShoreHut ? 0 : 1).FirstOrDefault();
            if (g == null) return "no group lives apart";
            if (!g.tower) BeginTower(g);
            return $"tower begun by the {g.family} family at ({g.towerX:0.00}, {g.towerZ:0.00})";
        }

        /// <summary>Debug: move the tower on a stage (and finish it at the last).</summary>
        public string DebugTowerNextStage()
        {
            var g = TowerGroup;
            if (g == null) return DebugTowerBegin();
            g.towerFrom -= TowerStageMonths;
            if (TowerStage(g) >= TowerDone && !g.towerDone)
            {
                g.towerDone = true;
                Chronicle("gift", $"The {g.family} family finished a lookout tower, a gift for the Keeper. Its lamp is lit at dusk.", 0.75f);
            }
            return $"tower stage {TowerStage(g)}";
        }
    }
}
