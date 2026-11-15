namespace AquariumCiv.View.Moments
{
    /// <summary>Where the close is (board Flow: the close): the HUD steps away, the people gather, one speaks, the camera eases to the
    /// shelf, and the last line stays until the player touches or goes.</summary>
    public enum CloseStep { None, Leaving, Gather, Speak, Shelf, Rest }

    /// <summary>What the view knows when it asks whether a close may start.</summary>
    public struct CloseAsk
    {
        public double sessionSeconds;   // this sitting
        public double sinceLastClose;   // since the last close of this sitting (a large number if there was none)
        public float idleSeconds;       // since the last touch, or the last thing up on screen
        public float zoom;              // the camera's, 1 = the box fills the width
        public bool touching;
        public bool busy;               // a beat isn't spent: a sheet, a card, the peek, a moment, Show me, a tool in hand, a visit
        public bool firstSitting;       // the first run ended in this sitting and the box has never had a close
    }

    /// <summary>
    /// When the evening talk starts and how long each step takes (first-session-and-check-in.md §4, Choice 6 ★, and decisions.md
    /// "Session close"). Pure, tested without Unity.
    /// <list type="bullet">
    /// <item><b>Only after a long sitting</b> (about 15 minutes): short check-ins end quietly, with no forced beat. The first sitting
    /// counts as long, because the first run's table ends on this scene (§2, 3:10).</item>
    /// <item><b>Then one of two things starts it:</b> stepping back (a pinch out past the fitted view, finger lifted), or about 20 s
    /// without a touch once the check-in's beats are spent.</item>
    /// <item><b>Once a sitting's stretch:</b> another long stretch earns another close, so it never nags.</item>
    /// <item><b>At most 8 s</b> to the last line, and a touch at any step leaves it: it never blocks.</item>
    /// </list>
    /// </summary>
    public static class CloseRules
    {
        public const double LongSittingSeconds = 15.0 * 60.0;
        public const float IdleSeconds = 20f;
        /// <summary>The fitted view is 1 and the camera stops at 1.3: past this, the player has stepped back.</summary>
        public const float StepBackZoom = 1.15f;
        /// <summary>#672: closer in than this (BoxCamera zoom, smaller = closer) the player is watching up close: idle never starts the close.</summary>
        public const float WatchingZoom = 0.7f;

        public const float LeavingSeconds = 0.4f, GatherSeconds = 2.6f, SpeakSeconds = 2.2f, ShelfSeconds = 2.8f;
        public const float TotalSeconds = LeavingSeconds + GatherSeconds + SpeakSeconds + ShelfSeconds;

        public static bool LongEnough(in CloseAsk a) => a.firstSitting || (a.sessionSeconds >= LongSittingSeconds && a.sinceLastClose >= LongSittingSeconds);

        public static bool ShouldStart(in CloseAsk a)
        {
            if (a.busy || a.touching || !LongEnough(a)) return false;
            // #672 (playtest): a player who zoomed in and set the phone down is watching the people; idle doesn't end that
            return a.zoom >= StepBackZoom || (a.idleSeconds >= IdleSeconds && a.zoom >= WatchingZoom);
        }

        /// <summary>The step at this many seconds into the close.</summary>
        public static CloseStep StepAt(float t)
        {
            if (t < LeavingSeconds) return CloseStep.Leaving;
            if (t < LeavingSeconds + GatherSeconds) return CloseStep.Gather;
            if (t < LeavingSeconds + GatherSeconds + SpeakSeconds) return CloseStep.Speak;
            return t < TotalSeconds ? CloseStep.Shelf : CloseStep.Rest;
        }

        /// <summary>The caption's line: the era waiting replaces the teaser (§4 C); with neither, the tuck-in says nothing.</summary>
        public static string LastLine(bool eraWaits, string eraLine, string teaser) => eraWaits ? eraLine : teaser;

        /// <summary>Said once, the first time the box closes: leaving is fine and part of the game (§2, Neko Atsume's lesson).</summary>
        public const string FirstCloseLine = "They'll keep living while you're away. Come back whenever you like.";
        public const string FlagFirstClose = "close:told";
    }
}
