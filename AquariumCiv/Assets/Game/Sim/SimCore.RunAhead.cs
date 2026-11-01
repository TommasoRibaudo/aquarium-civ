using System;

namespace AquariumCiv.Sim
{
    /// <summary>What a run ahead looks for.</summary>
    public enum RunAheadTarget { None, NextEra, Peak }

    /// <summary>Why a run ahead has nothing to tell.</summary>
    public enum RunAheadMiss
    {
        Found,
        /// <summary>The era or the peak is ready already: it waits, and nothing can follow until the player watches.</summary>
        AlreadyWaiting,
        /// <summary>The box is a museum.</summary>
        BoxComplete,
        /// <summary>Nothing becomes ready within <see cref="SimCore.RunAheadMaxAwayHours"/>.</summary>
        NotInTime,
    }

    /// <summary>When the next era or the peak becomes ready if the player stays away.</summary>
    public struct RunAheadResult
    {
        public RunAheadTarget target;
        public RunAheadMiss miss;
        public Era era;             // the era that becomes ready (NextEra only)
        public long month;          // the month it happens, as eraReadyMonth or peakReadyMonth will read
        public double simHours;     // sim hours from close until then
        public double realHours;    // real hours from close until then: the notification goes at close + this
        public int monthsRun;       // months the copy lived: the run's cost
        public bool Found => target != RunAheadTarget.None;
    }

    /// <summary>
    /// The notification's time (era-moment.md §10): iOS can't run the sim in the background, so at close a copy of the state
    /// lives ahead until the next era or the peak becomes ready. It goes through the same <see cref="Step"/> as
    /// <see cref="CatchUp"/>, with <see cref="CatchingUp"/> set, and any catch-up lives the same months up to the point it
    /// stops (its mess top-up comes after the last month), so the first ready month doesn't depend on how long the player
    /// really stays away. That month turns back into real hours with <see cref="SimConfig.RealHoursFor"/>: full speed for
    /// 8 h, a quarter after.
    /// <para><b>Cost bound.</b> One deep copy plus at most <see cref="RunAheadMaxMonths"/> months (2,232 at the default config:
    /// 30 days away is 186 sim hours). That is the most a catch-up can ever live, since the save caps an absence at 30 days.</para>
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The longest absence a catch-up honours. Equals SaveStore.MaxAwayHours (Sim can't see Runtime: a test holds them together).</summary>
        public const double RunAheadMaxAwayHours = 30 * 24;

        /// <summary>The most months a run ahead can live.</summary>
        public int RunAheadMaxMonths => (int)Math.Floor(Config.OfflineSimHours(RunAheadMaxAwayHours) / Config.StepHours + 1e-7);

        /// <summary>
        /// Runs a copy of this box ahead and says when it becomes ready. <paramref name="deepCopy"/> makes the copy (Sim has no
        /// serialiser: Runtime gives a JsonUtility round trip of the state, the save's own path). This sim and its state
        /// aren't touched and none of its events fire; the copy is dropped after.
        /// </summary>
        public RunAheadResult RunAhead(Func<SimState, SimState> deepCopy)
        {
            if (deepCopy == null) throw new ArgumentNullException(nameof(deepCopy));
            if (State.boxComplete) return new RunAheadResult { miss = RunAheadMiss.BoxComplete };
            if (State.eraReady || State.peakReady) return new RunAheadResult { miss = RunAheadMiss.AlreadyWaiting };
            var copy = deepCopy(State);
            if (copy == null || ReferenceEquals(copy, State)) throw new ArgumentException("needs a separate copy of the state", nameof(deepCopy));
            var ahead = new SimCore(Config, copy) { CatchingUp = true };
            var s = ahead.State;
            bool Ready() => s.eraReady || s.peakReady;
            // Like Advance: a month steps when simHours reaches it, and a save can stand part way through a month.
            long last = (long)Math.Floor((s.simHours + Config.OfflineSimHours(RunAheadMaxAwayHours)) / Config.StepHours + 1e-7);
            int months = 0;
            while (s.month < last && !Ready()) { ahead.Step(); months++; }
            if (!Ready()) return new RunAheadResult { miss = RunAheadMiss.NotInTime, monthsRun = months };
            double sim = Math.Max(0, s.month * Config.StepHours - s.simHours);
            return new RunAheadResult
            {
                target = s.eraReady ? RunAheadTarget.NextEra : RunAheadTarget.Peak, era = s.eraReady ? s.era + 1 : s.era,
                month = s.month, simHours = sim, realHours = Config.RealHoursFor(sim), monthsRun = months,
            };
        }
    }
}
