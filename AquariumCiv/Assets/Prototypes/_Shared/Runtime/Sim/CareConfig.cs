using System;

namespace AquariumCiv.Prototypes.Sim
{
    /// <summary>
    /// The care layer as Gate 1 decided it (P2 variant B, decisions.md 2026-10-29): mess dims the sunlight and slows the
    /// work pace (linear, at most −25 % at the 48 h cap), and the people's mood shows it in how they walk and what they
    /// say, never as a demand on the player. The sim's own mood ignores mess, so the stated pace is the whole cost.
    /// Off by default: P2–P6 keep setting their own switches as tested. The vertical slice turns it on with
    /// <see cref="Gate1"/> and calls <see cref="ApplyTo"/> before any catch-up.
    /// </summary>
    [Serializable]
    public class CareConfig
    {
        public bool enabled;
        public float workPaceDropAtFullMess = 0.25f;  // the slow-down at full mess (P2 B's outputPenaltyMax)
        public float messFullHours = 48f;             // after this many real hours away, nothing gets worse
        public float moodShowsAbove = 0.3f;           // people say wistful or comic lines above this mess level
        public float cheerBelow = 0.1f;               // someone cheers when cleaning brings mess below this
        public float grumpinessPerMess = 1.4f;        // how fast the crowd's walk slows with mess (CrowdView.grumpiness)

        /// <summary>Gate 1's decision, the vertical slice's default care layer.</summary>
        public static CareConfig Gate1 => new() { enabled = true };

        /// <summary>Set the sim switches this care layer implies. Does nothing when off.</summary>
        public void ApplyTo(SimConfig cfg)
        {
            if (!enabled) return;
            cfg.outputPenaltyEnabled = true;
            cfg.outputPenaltyMax = workPaceDropAtFullMess;
            cfg.messAffectsMood = false; // mood x mess would stack on the pace drop (about −36 % in P2's tester run 1)
            cfg.messFullHours = messFullHours;
        }

        // ---------- what the player sees (framed as the world's state, not a loss) ----------

        /// <summary>0..100: grime and junk block the people's light.</summary>
        public static int SunlightPercent(SimCore sim) => (int)Math.Round((1f - sim.MessLevel) * 100f);

        /// <summary>0..100: work follows the light.</summary>
        public static int WorkPacePercent(SimCore sim) => (int)Math.Round((1f - sim.OutputPenalty) * 100f);

        /// <summary>The ledger line P2 B shipped: "Sunlight 57 % · work pace 89 %", or the full-sun line when clean.</summary>
        public static string LedgerLine(SimCore sim)
        {
            int sun = SunlightPercent(sim);
            return sun >= 99 ? "Full sunlight  ·  work pace 100%" : $"Sunlight {sun}%  ·  work pace {WorkPacePercent(sim)}%";
        }

        /// <summary>0..1 target for <c>CrowdView.grumpiness</c> (slower walks, longer idles).</summary>
        public float Grumpiness(SimCore sim) => enabled ? Math.Clamp(sim.MessLevel * grumpinessPerMess, 0f, 1f) : 0f;

        public bool MoodShows(SimCore sim) => enabled && sim.MessLevel > moodShowsAbove;
        public bool Cheers(SimCore sim) => enabled && sim.MessLevel < cheerBelow;

        // Wistful or comic, never a demand on the player: the people react to their world, they don't scold the Keeper.
        public static readonly string[] MoodLines =
        {
            "I miss the sun...", "Found a sock bigger than my house.", "The weeds are taller than me now!",
            "Is it evening already?", "My crops are squinting.", "Someone's crate is in my garden.",
        };
        public static readonly string[] CheerLines = { "Look at that sky!", "Sunshine!", "I can see the hills again!", "Back to work, the light's back!" };
    }
}
