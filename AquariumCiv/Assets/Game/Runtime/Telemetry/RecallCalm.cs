namespace AquariumCiv.Game
{
    /// <summary>What is on screen right now, as plain flags and timers (#328). The view fills it from the HUD (read only).</summary>
    public struct RecallCalmState
    {
        /// <summary>A sheet is open (the Kit, settings, history, shaping, offices ...).</summary>
        public bool sheetOpen;
        /// <summary>The cloth, the mister, the tweezers or the seed is in hand (or the shaping tool is armed).</summary>
        public bool toolInHand;
        /// <summary>A first-run step is still going.</summary>
        public bool firstRunStep;
        /// <summary>An era moment, a Show me tour, the return cards, a person card or a time-lapse is playing.</summary>
        public bool momentPlaying;
        /// <summary>A caption or a toast is on screen (or waiting).</summary>
        public bool captionOrToast;
        /// <summary>Another prompt (the feedback sheet, the question itself) is already up.</summary>
        public bool anotherPromptUp;
        /// <summary>Real seconds since a finger or the mouse last touched the screen.</summary>
        public double secondsSinceTouch;
        /// <summary>Real seconds since anything above was last true.</summary>
        public double secondsSinceBusy;
    }

    /// <summary>
    /// When the recall question may show (#328). <see cref="RecallPrompt.ShouldAsk"/> decides whether it is due; this decides whether
    /// the moment is calm. If not, the view keeps asking each check and the question shows at the next calm moment in the session,
    /// or at the next session start (the day stamp is only written when it actually shows).
    /// </summary>
    public static class RecallCalm
    {
        /// <summary>No touch and nothing on screen for this long (real seconds).</summary>
        public const double CalmSeconds = 2.0;

        public static bool Busy(RecallCalmState s) =>
            s.sheetOpen || s.toolInHand || s.firstRunStep || s.momentPlaying || s.captionOrToast || s.anotherPromptUp;

        public static bool CanAskNow(RecallCalmState s) =>
            !Busy(s) && s.secondsSinceTouch >= CalmSeconds && s.secondsSinceBusy >= CalmSeconds;
    }
}
