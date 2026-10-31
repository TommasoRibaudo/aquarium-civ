using System;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// How the care layer shows (Gate 1, decisions.md 2026-10-29): mess dims the sunlight and slows the work pace (the
    /// sim's <see cref="SimCore.OutputPenalty"/>), and the people's mood shows it in how they walk and what they say,
    /// never as a demand on the player. The numbers that change the world live in <see cref="SimConfig"/>.
    /// </summary>
    [Serializable]
    public class CareConfig
    {
        public float moodShowsAbove = 0.3f;           // people say wistful or comic lines above this mess level
        public float cheerBelow = 0.1f;               // someone cheers when cleaning brings mess below this
        public float grumpinessPerMess = 1.4f;        // how fast the crowd's walk slows with mess

        /// <summary>0..100: grime and junk block the people's light.</summary>
        public static int SunlightPercent(SimCore sim) => (int)Math.Round((1f - sim.MessLevel) * 100f);

        /// <summary>0..100: work follows the light.</summary>
        public static int WorkPacePercent(SimCore sim) => (int)Math.Round((1f - sim.OutputPenalty) * 100f);

        /// <summary>"Sunlight 57 % · work pace 89 %", or the full-sun line when clean.</summary>
        public static string LedgerLine(SimCore sim)
        {
            int sun = SunlightPercent(sim);
            return sun >= 99 ? "Full sunlight  ·  work pace 100%" : $"Sunlight {sun}%  ·  work pace {WorkPacePercent(sim)}%";
        }

        /// <summary>0..1: slower walks, longer idles.</summary>
        public float Grumpiness(SimCore sim) => Math.Clamp(sim.MessLevel * grumpinessPerMess, 0f, 1f);

        public bool MoodShows(SimCore sim) => sim.MessLevel > moodShowsAbove;
        public bool Cheers(SimCore sim) => sim.MessLevel < cheerBelow;

        // Wistful or comic, never a demand on the player: the people react to their world, they don't scold the Keeper.
        public static readonly string[] MoodLines =
        {
            "I miss the sun...", "Found a sock bigger than my house.", "The weeds are taller than me now!",
            "Is it evening already?", "My crops are squinting.", "Someone's crate is in my garden.",
        };
        public static readonly string[] CheerLines = { "Look at that sky!", "Sunshine!", "I can see the hills again!", "Back to work, the light's back!" };
    }
}
