using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// Care moves up (#60; decisions.md 2026-10-29 "Care moves up", life-in-the-box §7, choice 6): in the town the
    /// people really trim the overgrowth, slowly (the trash heaps are gone, #313). The sweepers are the notables with the
    /// sweeper/gardener job plus one from the crowd per <see cref="SimConfig.peoplePerCrowdSweeper"/> people. Each trims
    /// <see cref="SimConfig.trimPerHour"/> overgrowth per real hour, like the mess, live and offline; together never more
    /// than <see cref="SimConfig.careMaxShare"/> of what comes in, so the mess still grows and the Keeper always has care
    /// to do. The glass is always the Keeper's: grime is never touched. Camp and village: nobody (the village tidies
    /// cosmetically, in the view only).
    /// </summary>
    public partial class SimCore
    {
        const string FlagSweepers = "sweepers";

        /// <summary>The crowd's sweepers (town only).</summary>
        public int CrowdSweepers => State.era == Era.Town && Config.peoplePerCrowdSweeper > 0 ? State.population / Config.peoplePerCrowdSweeper : 0;

        /// <summary>Notables working as sweeper/gardener (town only).</summary>
        public int NotableSweepers => State.era == Era.Town ? State.notables.Count(n => n.alive && n.job == JobType.Sweeper) : 0;

        /// <summary>Everyone sweeping and weeding now: 0 outside the town.</summary>
        public int Sweepers => State.era == Era.Town ? CrowdSweepers + NotableSweepers : 0;

        /// <summary>Overgrowth a real hour of weeding trims now: per sweeper, capped at a share of the incoming overgrowth.</summary>
        public float TrimPerHour
        {
            get
            {
                int n = Sweepers;
                if (n == 0) return 0f;
                return Math.Min(n * Config.trimPerHour, Config.careMaxShare / Config.messFullHours);
            }
        }

        /// <summary>Every month: the chronicle notes the first sweepers, once.</summary>
        void CareStep()
        {
            var s = State;
            if (s.era != Era.Town || s.storyFlags.Contains(FlagSweepers) || Sweepers == 0) return;
            s.storyFlags.Add(FlagSweepers);
            var who = s.notables.Where(n => n.alive && n.job == JobType.Sweeper).OrderBy(n => n.id).FirstOrDefault();
            Chronicle("sweepers", who != null
                ? $"{who.FullName} took up a broom and a handcart. {Place} has sweepers now: the weeds on the green get trimmed."
                : $"{Place} has sweepers now: a few folk with brooms and a handcart trim the weeds on the green.",
                0.6f, who?.id ?? -1);
        }

        // ---------- debug ----------
        /// <summary>Debug: the sweepers trim a tenth of the overgrowth now, whatever the rates say.
        /// False outside the town (no sweepers).</summary>
        public bool DebugSweep()
        {
            if (Sweepers == 0) return false;
            State.overgrowth = Math.Max(0f, State.overgrowth - 0.1f);
            return true;
        }
    }
}
