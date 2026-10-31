using System;

namespace AquariumCiv.Sim
{
    /// <summary>The first run's beats in order (docs/design/first-session-and-check-in.md §2, decisions.md "First session").
    /// Wipe first, then the overgrown patch, then the name, the first rain, "Their first year" and its card.</summary>
    public enum FirstRunStep { Arrive = 0, Wipe = 1, Patch = 2, Name = 3, Rain = 4, Year = 5, Card = 6, Done = 7 }

    /// <summary>
    /// The first run's progress, saved inside <see cref="SimState"/> so a killed app resumes where it was (#94). Opt-in:
    /// <see cref="active"/> is false in every world that never called <c>FirstRun.Begin</c>, so existing worlds and the
    /// economy's dice are untouched. Times are play seconds (never the wall clock), so a resume never reads a gap as idleness.
    /// </summary>
    [Serializable]
    public class FirstRunState
    {
        public bool active;
        public bool skipped;               // the player skipped the whole run (never asked twice)
        public int step;                   // FirstRunStep
        public double playSeconds;         // total play time in the run
        public double stepSeconds;         // time in the current step
        // Gesture hint of the current step (see FirstRun.Hint): phase 0 not shown yet, 1 showing, 2 hidden (waiting for idle), 3 spent
        public int hintPhase;
        public int hintReturns;            // times it came back after 25 s idle (max FirstRun.HintMaxReturns)
        public double hintIdle;            // seconds idle since it was hidden
        public bool hintSubstep;           // Rain: the tray is open, so the hint now says "rub over a spot"
        public bool smileyDone;            // the child's smiley in the mist was cued
        public double untouchedSeconds;    // seconds since the player last touched in this step
        // The set-up
        public float patchX, patchZ;       // the overgrown patch (box coordinates)
        public float rainX, rainZ;         // the suggested spot for the first rain
        public string suggestedName = "";
        public bool nameKept;              // the player kept the suggestion or typed their own (false = auto-kept on a skip)
        // Rain and the year
        public int rainWishId = -1;
        public int rainNotableId = -1;     // the wisher; the answer is credited to them
        public int rainNudgeId = -1;
        public long yearStartMonth = -1;   // "Their first year" began at this month (-1 = not started)
        public bool yearDone;
        public double yearSeconds;         // seconds of the live skip played so far (it resumes after a kill)
        public bool cardAcknowledged;      // the first card was seen (Show me or dismissed)
        public int popAtStart, buildingsAtStart;
        public long beganMonth;            // sim month the run began
    }
}
