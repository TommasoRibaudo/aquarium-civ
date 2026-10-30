using System;

namespace AquariumCiv.Game
{
    /// <summary>What the recall question needs to remember between sessions (phase 2 saves it as JSON next to the telemetry files).</summary>
    [Serializable]
    public class RecallState
    {
        public int shown;            // times the question appeared
        public int dismissed;        // times the tester closed it without answering
        public bool answered;
        public int lastShownDay = -1; // day index (0 = install day)
    }

    /// <summary>What the UI knows right now.</summary>
    public struct RecallContext
    {
        /// <summary>Days since the install day, in the phone's own calendar (see <see cref="TelemetryAnalyzer.DayIndex"/>).</summary>
        public int dayIndex;
        /// <summary>Seconds since this session started (open or return).</summary>
        public double sessionSeconds;
        /// <summary>An era moment, the return cards, a person card, a fly-to or a modal is on screen.</summary>
        public bool busy;
        /// <summary>Stories the tester has had a chance to recall: chronicle cards shown so far.</summary>
        public int cardsEverShown;
    }

    /// <summary>
    /// When to ask "which stories do you remember?" (vertical-slice-plan §5.4: one question, on day 3). The decision is pure;
    /// the UI is #100 phase 2. Ask once, at a calm moment, on or after day 3; if the tester closes it, ask again at most
    /// <see cref="MaxDismissals"/> more times, on a later day, never in the same day.
    /// </summary>
    public static class RecallPrompt
    {
        public const int FirstDay = 3;
        /// <summary>After this day the question no longer measures "day 3" recall, so it is not asked.</summary>
        public const int LastDay = 7;
        public const int MaxDismissals = 2;
        /// <summary>Let the return cards and the first look at the box finish first.</summary>
        public const double MinSessionSeconds = 20;
        public const int MinCardsShown = 1;

        public static bool ShouldAsk(RecallState s, RecallContext c)
        {
            if (s == null || s.answered) return false;
            if (c.dayIndex < FirstDay || c.dayIndex > LastDay) return false;
            if (s.dismissed > MaxDismissals) return false;
            if (s.lastShownDay == c.dayIndex) return false;
            if (c.busy || c.sessionSeconds < MinSessionSeconds) return false;
            if (c.cardsEverShown < MinCardsShown) return false; // nothing to recall yet
            return true;
        }

        /// <summary>Call when the question appears (also log <see cref="TelemetryHooks.RecallShown"/>).</summary>
        public static void Shown(RecallState s, int dayIndex) { s.shown++; s.lastShownDay = dayIndex; }
        public static void Dismissed(RecallState s) => s.dismissed++;
        public static void Answered(RecallState s) => s.answered = true;
    }
}
