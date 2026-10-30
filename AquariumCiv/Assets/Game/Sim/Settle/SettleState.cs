using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>What stands on a lot: the look and siting rules, not the economy's role. Saved as an int, so the order
    /// is fixed: new kinds go at the end. One economy type can stand on several kinds (a Workshop is a watermill, a
    /// smithy, a dyer's yard...), and the view dresses each kind by era (a camp's Farm is gathering grounds).</summary>
    public enum LotKind
    {
        Camp, Hut, Farm, Lumbermill, Well, Windmill, Market, Tavern, Church,
        Watermill, Smithy, Dyer, Cottage, Shop, Workshop, Guildhall,
        /// <summary>A family's barn, byre or shed across its yard (no economy building; see Settlement.Outbuildings).</summary>
        Outbuilding,
        Pasture, Orchard, Jetty, Quarry, Kiln, TimberYard,
        Bakery,
    }

    /// <summary>A claimed lot: an oriented rectangle whose front (local −z) faces its door. Lots never move: an era
    /// change rebuilds on the same lot.</summary>
    [Serializable]
    public class Lot
    {
        public int id;               // ≥ 1 (0 = none everywhere)
        public int buildingId;       // the economy's building on it; 0 for yard outbuildings
        public LotKind kind;
        public float x, z, yaw, w, d;
        public long foundedMonth;
        public int nudgeId;          // the Keeper's shaping this answered (0 = none)
        public bool retired;         // cleared at an era moment (the camp hearth)
        public bool open;            // stands on the green without claiming cells (the hearth, the first well)
        public int door = -1;        // the cell just outside the front, joined to the path network
        public float doorX, doorZ;
        public int yard;             // the family yard it belongs to (0 = none)

        /// <summary>World direction of the lot's front (local −z after yaw).</summary>
        public void Front(out float fx, out float fz)
        {
            float r = yaw * (MathF.PI / 180f);
            fx = -MathF.Sin(r); fz = -MathF.Cos(r);
        }
    }

    /// <summary>A family's shared yard (the cluster village, Growth Lab round 2): 2–4 homes round it, a barn across it.
    /// Yards never move.</summary>
    [Serializable]
    public class Yard
    {
        public int id;
        public float x, z, r;
        public int cap, count;       // homes the family builds here, homes standing
        public long foundedMonth, lastJoinMonth;
        public bool ringHeld;        // the land round the yard still waits for the family
        public int outbuildingLot;   // the barn's lot (0 = none yet)
    }

    /// <summary>Land kept open for good (a meadow or orchard): never built on, except where the Keeper shapes it.</summary>
    [Serializable]
    public class Commons
    {
        public float x, z, r;
    }

    /// <summary>
    /// The settlement's placement results, stored in the save (decisions.md 2026-10-29: layouts differ slightly between
    /// Mono and .NET, so results are never re-derived). Grids are the 60 × 44 site grid of 0.1 u (research 08 §3.2);
    /// the static land (height, slope, pond, base fertility) comes from <see cref="terrain"/> and is not stored.
    /// </summary>
    [Serializable]
    public class SettleState
    {
        public const int Version = 1;
        /// <summary>0 = not settled yet: a save from before placement (migrated from slots once) or settling is off.</summary>
        public int version;
        public int terrain;              // TerrainSpec preset
        /// <summary>Per cell: SiteMap.F* bits (canal, forest, laid, green, commons, yard, meadow, rock).</summary>
        public byte[] flags;
        /// <summary>Per cell: 0 none, 1 trail, 2 path, 3 road (PathNet).</summary>
        public byte[] tier;
        public float[] wear;
        /// <summary>Walked direction per cell as a doubled angle (cos 2θ, sin 2θ): gives frontage its yaw.</summary>
        public float[] dirC, dirS;
        /// <summary>Per cell: the lot id covering it (0 = open).</summary>
        public int[] lot;
        /// <summary>Per cell: the yard whose family still holds this land (0 = none).</summary>
        public int[] ring;
        /// <summary>Per cell: month + 1 when it last became a path of any tier (0 = no path now).</summary>
        public int[] pathMonth;
        public List<Lot> lots = new List<Lot>();
        public List<Yard> yards = new List<Yard>();
        public List<Commons> commons = new List<Commons>();
        public int nextLotId = 1;
        /// <summary>Buildings (in SimState.buildings order) already offered a lot.</summary>
        public int processed;
        /// <summary>Months in which some building waited for a lot (the box is full; feeds the town's peak).</summary>
        public int noLotMonths;
        /// <summary>The month a build first found no good lot, while it still waits (-1 = nothing waits).</summary>
        public long noLotSince = -1;
        /// <summary>Lots ever refused (a build that found nothing on its first try).</summary>
        public int noLotBuilds;
    }
}
