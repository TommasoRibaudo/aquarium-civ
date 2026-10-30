using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// Care moves up (#60; decisions.md 2026-10-29 "Care moves up", life-in-the-box §7, choice 6 ★): in the town the
    /// people really clear their own trash heaps and trim the overgrowth, slowly. The sweepers are the notables with the
    /// sweeper/gardener job plus one from the crowd per <see cref="SimConfig.peoplePerCrowdSweeper"/> people. Each clears
    /// <see cref="SimConfig.sweepHeapsPerHour"/> heaps and trims <see cref="SimConfig.trimPerHour"/> overgrowth per real hour,
    /// like the mess, live and offline; together never more than <see cref="SimConfig.careMaxShare"/> of what comes in, so
    /// the mess still grows and the Keeper always has care to do. The glass is always the Keeper's: grime is never touched.
    /// Camp and village: nobody (the village tidies cosmetically, in the view only). No dice: the oldest heap goes first.
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

        /// <summary>Trash heaps a real hour of sweeping clears now: per sweeper, capped at a share of the incoming trash.</summary>
        public float SweepHeapsPerHour
        {
            get
            {
                int n = Sweepers;
                if (n == 0) return 0f;
                float incoming = State.population * Config.trashPerPersonHour;
                return Math.Min(n * Config.sweepHeapsPerHour, Config.careMaxShare * incoming);
            }
        }

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

        /// <summary>0..1: how far the sweepers are toward clearing the next heap (for the view's timing).</summary>
        public float SweepProgress => State.sweepAccum;

        /// <summary>The heaps in the order the sweepers take them: oldest first (then by id).</summary>
        IEnumerable<TrashItem> SweepOrder => State.trash.OrderBy(t => t.month).ThenBy(t => t.id);

        /// <summary>The id of the heap sweeper number <paramref name="index"/> is heading to (0 = the next one to go), or -1
        /// when there is no such sweeper or heap. Sweepers take the oldest heaps, one each.</summary>
        public int SweeperTarget(int index = 0)
        {
            if (index < 0 || index >= Sweepers || index >= State.trash.Count) return -1;
            return SweepOrder.ElementAt(index).id;
        }

        /// <summary>The heaps the sweepers are heading to, one per sweeper, the next to go first.</summary>
        public List<int> SweeperTargets() => SweepOrder.Take(Sweepers).Select(t => t.id).ToList();

        /// <summary><paramref name="h"/> real hours of sweeping (called from AccrueMess, so live, catch-up and the unlived
        /// top-up all sweep alike). With nothing to sweep, the sweepers don't bank work for later.</summary>
        void Sweep(float h)
        {
            var s = State;
            if (s.trash.Count == 0) return;
            s.sweepAccum += SweepHeapsPerHour * h;
            while (s.sweepAccum >= 1f && s.trash.Count > 0)
            {
                s.sweepAccum -= 1f;
                s.trash.Remove(SweepOrder.First());
            }
            if (s.trash.Count == 0) s.sweepAccum = Math.Min(s.sweepAccum, 0.99f);
        }

        /// <summary>Every month: the chronicle notes the first sweepers, once.</summary>
        void CareStep()
        {
            var s = State;
            if (s.era != Era.Town || s.storyFlags.Contains(FlagSweepers) || Sweepers == 0) return;
            s.storyFlags.Add(FlagSweepers);
            var who = s.notables.Where(n => n.alive && n.job == JobType.Sweeper).OrderBy(n => n.id).FirstOrDefault();
            Chronicle("sweepers", who != null
                ? $"{who.FullName} took up a broom and a handcart. {Place} has sweepers now: the heaps go to the midden one by one, and the weeds on the green get trimmed."
                : $"{Place} has sweepers now: a few folk with brooms and a handcart take the heaps to the midden one by one, and trim the weeds on the green.",
                0.6f, who?.id ?? -1);
        }

        // ---------- debug ----------
        /// <summary>Debug: the sweepers clear the next heap and trim a tenth of the overgrowth now, whatever the rates say.
        /// False outside the town (no sweepers).</summary>
        public bool DebugSweep()
        {
            if (Sweepers == 0) return false;
            if (State.trash.Count > 0) State.trash.Remove(SweepOrder.First());
            State.overgrowth = Math.Max(0f, State.overgrowth - 0.1f);
            return true;
        }
    }
}
