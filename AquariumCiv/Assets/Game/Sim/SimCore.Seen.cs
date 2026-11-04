using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// P8 #281: the view's door to "new grass is fresher green" (see <see cref="EcoSeen"/>). The view calls
    /// <see cref="MarkLandSeen"/> on pause, quit, leave, on a fresh box at start, and on an old save that has no mask yet;
    /// on return it asks for <see cref="LandFreshness"/> each frame (real seconds since the return) and calls
    /// <see cref="CommitLandSeenIfFaded"/> when it has finished. Catch-up needs nothing: grass grown while away is simply
    /// green and not in the mask. Off the ecology, and with no mask, nothing is fresh.
    /// </summary>
    public partial class SimCore
    {
        public bool HasLandSeenMask => EcologyOn && EcoSeen.HasMask(State.eco);

        /// <summary>The player has looked at the land: all green cells count as seen from now on.</summary>
        public void MarkLandSeen() { if (EcologyOn) EcoSeen.Mark(State.eco); }

        /// <summary>Freshness 0..1 of one site cell, <paramref name="secondsSinceReturn"/> real seconds after the return.</summary>
        public float LandFreshness(int cell, float secondsSinceReturn) => EcologyOn ? EcoSeen.Freshness(State.eco, cell, secondsSinceReturn) : 0f;

        /// <summary>Fills a per-cell freshness array (length at least EcoLand.N); returns how many cells are fresh (0 off the ecology).</summary>
        public int FillLandFreshness(float secondsSinceReturn, float[] into)
        {
            if (!EcologyOn) { if (into != null) System.Array.Clear(into, 0, into.Length); return 0; }
            return EcoSeen.Fill(State.eco, secondsSinceReturn, into);
        }

        /// <summary>Cells that are new since the player last looked (green, unseen), whatever the fade.</summary>
        public int LandNewCellCount => EcologyOn ? EcoSeen.NewCount(State.eco) : 0;

        /// <summary>Once the fade is over, record everything as seen. Returns true when it committed.</summary>
        public bool CommitLandSeenIfFaded(float secondsSinceReturn)
        {
            if (!EcologyOn || secondsSinceReturn < EcoSeen.FadeSeconds) return false;
            EcoSeen.Mark(State.eco);
            return true;
        }

        /// <summary>Debug ("pretend I was away, with new grass"): up to <paramref name="count"/> green cells become unseen. Returns the new-cell count.</summary>
        public int DebugPretendAwayWithNewGrass(int count = 12) => EcologyOn ? EcoSeen.PretendAway(State.eco, count) : 0;
    }
}
