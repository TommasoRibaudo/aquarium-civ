using System;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>
    /// The planner's trait colour from #28 (<see cref="SimCore.TermFlavorOf"/>, notable-roles §4, life-in-the-box rule 9):
    /// a role-holder's trait colours their own day and, while they hold the role, the whole box. Each aspect of the
    /// <see cref="TermFlavor"/> counts only above an ordinary term (0.5); the leader's counts in full, every other
    /// office's at half (as in <see cref="SimCore.CurrentTermFlavor"/>). Since #64 everyone's own trait shows in their
    /// own day (<see cref="SimCore.TraitFlavorOf"/>): a notable without a role gets the same colour at
    /// <see cref="NonHolderStrength"/> (a lazy one naps now and then, a singer sings some evenings), but never the box's.
    /// Reads the sim only, draws no dice; with the flavour off (<see cref="SimCore.TermFlavourOn"/>) it is neutral.
    /// </summary>
    public sealed class TermLifeHints : ILifeHints
    {
        readonly SimCore sim;

        public TermLifeHints(SimCore sim) { this.sim = sim ?? throw new ArgumentNullException(nameof(sim)); }

        // Scales: what a full-strength aspect (1.0, i.e. +0.5 over ordinary) becomes [est].
        const float NapAtFull = 1f, SingAtFull = 1f, SitWithOldAtFull = 1f, HillAtFull = 0.5f, OwnHomeEarlyAtFull = 0.5f;
        const float BoxHomeEarlyAtFull = 1f, SweeperBlocksAtFull = 2f, FeastAtFull = 0.5f, AnnounceAtFull = 0.5f;

        /// <summary>How strongly a notable without a role shows their own trait, against a holder's 1 (#64) [est].</summary>
        public const float NonHolderStrength = 0.5f;

        /// <summary>0 for an ordinary term or less, 1 for a full-strength aspect.</summary>
        static float Over(float v) => Math.Max(0f, Math.Min(1f, (v - 0.5f) * 2f));

        public PersonHints Person(Notable n, string role)
        {
            if (n == null) return default;
            bool holder = role != null;
            var f = holder ? sim.TermFlavorOf(n) : sim.TraitFlavorOf(n);
            float w = holder ? 1f : NonHolderStrength;
            return new PersonHints
            {
                napChance = Over(f.idleness) * NapAtFull * w,
                singChance = Over(f.music) * SingAtFull * w,
                sitWithOldChance = Over(f.kindness) * SitWithOldAtFull * w,
                hillChance = Over(f.wonder) * HillAtFull * w,
                homeEarlyHours = Math.Max(Over(f.quiet), Over(f.caution)) * OwnHomeEarlyAtFull * w,
            };
        }

        public BoxHints Office(Notable holder, string role)
        {
            if (holder == null || role == null) return default;
            var f = sim.TermFlavorOf(holder);
            float w = role == SimCore.LeaderOf(sim.State.era) ? 1f : 0.5f;
            return new BoxHints
            {
                homeEarlyHours = Math.Max(Over(f.quiet), Over(f.caution)) * w * BoxHomeEarlyAtFull,
                sweeperExtraBlocks = (int)Math.Round(Over(f.tidiness) * w * SweeperBlocksAtFull, MidpointRounding.AwayFromZero),
                feastChance = Over(f.feasts) * w * FeastAtFull,
                announceChance = Over(f.gathering) * w * AnnounceAtFull,
            };
        }
    }
}
