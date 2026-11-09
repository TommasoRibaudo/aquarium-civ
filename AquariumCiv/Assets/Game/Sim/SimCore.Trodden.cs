using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>#523 (playtest #500): where nothing overgrows. Weed patches, the creeping vine and the wild saplings the tweezers take are
    /// not drawn on the camp's trodden ground: a clear ring round the fire, the lots, the laid paths and trails, the green, the yards and
    /// the families' rings (what the settlement holds, <see cref="SettledCell"/>). The overgrowth <i>amount</i> is untouched: the view
    /// finds the patches another spot on open ground, so the tweezers have as much to do as before.</summary>
    public partial class SimCore
    {
        /// <summary>The clear ring round a lit fire, in box units: the fire pit (0.48) and the log seats and sitters round it (the crowd keeps
        /// ~1.0 from the flames, ChatterView.FireRadius), plus a margin for a tuft's own width.</summary>
        public const float FireClearRadius = 1.15f;

        /// <summary>True at a point of the box no weed, vine or sapling may take: the fire's ring or settled ground. False when the box
        /// isn't settled (the first run, the plain prototypes), where nothing is trodden yet.</summary>
        public bool IsTroddenGround(float x, float z)
        {
            if (!Config.settle || Settlement?.Map == null) return false;
            Settlement.Fire(out float fx, out float fz, out bool lit);
            if (lit && (x - fx) * (x - fx) + (z - fz) * (z - fz) < FireClearRadius * FireClearRadius) return true;
            int c = SiteMap.CellAt(x, z);
            return c >= 0 && SettledCell(c);
        }
    }
}
