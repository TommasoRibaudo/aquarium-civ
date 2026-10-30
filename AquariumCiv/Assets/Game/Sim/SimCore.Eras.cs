using System;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// Eras are plateaus, crossed live (game definition §8): camp → farming village → industrious town. When a box meets
    /// the next era's threshold (<see cref="SimConfig.villageAt"/>, <see cref="SimConfig.townAt"/>) it only becomes
    /// <see cref="SimState.eraReady"/>; the people keep living, live and in catch-up, but the era waits until the view
    /// calls <see cref="AdvanceEra"/> with the player watching. No dice here, so eras never shift the economy's rolls.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Raised once when the next era's threshold is reached (live or during catch-up).</summary>
        public event Action<Era> EraReadied;

        public Era NextEra => State.era + 1;
        public bool IsFinalEra => State.era >= Era.Town;

        /// <summary>The threshold for the next era (null in the final era).</summary>
        public EraThreshold NextEraThreshold => State.era switch
        {
            Era.Camp => Config.villageAt,
            Era.Village => Config.townAt,
            _ => null,
        };

        /// <summary>Game years spent in the current era.</summary>
        public int YearsInEra => (int)((State.month - State.eraStartMonth) / SimConfig.MonthsPerYear);

        /// <summary>Whether the box builds this type in its current era.</summary>
        public bool Allowed(string type) => BuildingType.EraOf(type) <= State.era;

        bool Wants(string type, bool need) => need && Allowed(type);

        public bool MeetsNextEraThreshold
        {
            get
            {
                var t = NextEraThreshold;
                return t != null && State.population >= t.population && State.buildings.Count >= t.buildings && YearsInEra >= t.years;
            }
        }

        /// <summary>Called at the end of every month: marks the next era ready, once. Never advances it.</summary>
        void EraStep()
        {
            if (State.eraReady || !MeetsNextEraThreshold) return;
            MarkEraReady();
        }

        void MarkEraReady()
        {
            State.eraReady = true;
            State.eraReadyMonth = State.month;
            Chronicle("era-ready", ReadyLine(NextEra), 0.8f); // user, 2026-10-29: the wait is part of the story
            EraReadied?.Invoke(NextEra);
        }

        /// <summary>The era moment: the only way the era moves. The view calls it while the player watches.
        /// Returns false when no era is ready.</summary>
        public bool AdvanceEra()
        {
            var s = State;
            if (!s.eraReady || IsFinalEra) return false;
            s.era++;
            s.eraReady = false;
            s.eraReadyMonth = -1;
            s.eraStartMonth = s.month;
            Chronicle("era", EraLine(s.era), 0.95f);
            JobsOnEraChange(); // jobs the new era doesn't have change (SimCore.Jobs.cs)
            RolesOnEraChange(); // the leader carries over, new offices fill (SimCore.Roles.cs)
            Settlement?.OnEraChange(); // lots never move: they rebuild in place; families get barns (Sim/Settle)
            return true;
        }

        string ReadyLine(Era era) => era switch
        {
            Era.Village => $"{Place} is restless. The elders talk of staying put and sowing seed; something is about to change.",
            Era.Town => $"{Place} hums with new trades. Everyone feels a bigger change coming.",
            _ => $"{Place} feels a new age coming.",
        };

        string EraLine(Era era) => era switch
        {
            Era.Village => $"{Place} became a farming village. Everyone remembers where they stood that day.",
            Era.Town => $"{Place} became a town of crafts and guilds. The old folk said they'd seen it coming.",
            _ => $"{Place} entered a new age.",
        };

        // ---------- debug ----------
        /// <summary>Debug: skip to the threshold (the era is ready and waits), whatever the numbers say.</summary>
        public bool ForceEraReady()
        {
            if (State.eraReady || IsFinalEra) return false;
            MarkEraReady();
            return true;
        }

        /// <summary>Debug: cross into the next era now.</summary>
        public bool ForceNextEra()
        {
            if (IsFinalEra) return false;
            if (!State.eraReady) MarkEraReady();
            return AdvanceEra();
        }
    }
}
