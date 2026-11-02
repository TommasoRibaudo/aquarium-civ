using System;

namespace AquariumCiv.Sim
{
    /// <summary>The four kinds of dilemma (research 14 §2.4).</summary>
    public enum DilemmaKind { Who, WhichWay, Tradition, Strangers }

    /// <summary>Who the two options point at. <see cref="TwoCandidates"/>: the two best-standing people free to take the def's role
    /// (option 0 is person A, option 1 is person B). <see cref="None"/>: the options are a way, a custom or a family, not people.</summary>
    public enum DilemmaCast { None, TwoCandidates }

    /// <summary>What an answer does to the one who was not chosen (the other option's person).</summary>
    public enum DilemmaTies { None, RivalOrGrace }

    /// <summary>
    /// One option of a dilemma: what the player reads (label, one-line hint in the people's voice, no numbers) and what choosing it does.
    /// Text may use {a} and {b} (the two people's first names) and {place} (the settlement's name).
    /// Effects (all optional, any mix): <see cref="takesRole"/>, <see cref="lean"/>, <see cref="ties"/>, <see cref="custom"/>, <see cref="strangers"/>.
    /// <see cref="callbacks"/>: what comes back years later because this option was taken (P7 rule 4; at least one per option).
    /// </summary>
    public sealed class DilemmaOptionDef
    {
        public string label = "";           // short: "Ada as chief", "the woods". Also fills "The people chose … themselves."
        public string hint = "";            // one line, the people's voice: "Ada will run it her way, and Bo won't like it."
        public string done = "";            // the Keeper's chronicle line when answered; empty = a plain line from the label
        public bool takesRole;              // this option's person takes the def's role (bypasses the succession roll); a holder steps aside if the def allows
        public SimCore.Lean? lean;          // a Lean for SimCore.NudgeMonths (36) game months
        public DilemmaTies ties;            // the unchosen person: a rival chance or grace, by trait
        public string custom = "";          // a stored custom ("burial:stone"), see SimCore.HasCustom
        public int strangers;               // a family of this many adults joins (0 = none)
        public string buildingType = "";    // the place this option refers to ("Well"): its first building is the dilemma's place for the option
        public DilemmaCallbackDef[] callbacks;  // storylets that fire 4-15 years after this option was taken (answered or lapsed); null = none
        public string[] favouredBy;         // traits (NameData.Traits) that tilt a lapse toward this option, when the role-holder has one
    }

    /// <summary>
    /// A callback (P7 rule 4, #221): a storylet gated on "this option was taken N years ago". Scheduled when the dilemma ends (the Keeper
    /// answered, or the people did after a lapse), by a hash of the seed and the dilemma, fired in the monthly step when due.
    /// Text placeholders: {a} {b} (the two people's names, dead or alive), {place}, {chosen} (the taken option's label) and
    /// {credit}: "Because the Keeper chose {chosen}" for an answer, "Because they chose {chosen}" for a lapse. A text without
    /// {credit} gets it put in front ("Because the Keeper chose the woods, the granary filled."), so write the text to read after a comma.
    /// If a condition fails the <see cref="fallback"/> is told instead (same placeholders), or nothing at all if there is none.
    /// </summary>
    public sealed class DilemmaCallbackDef
    {
        public string id = "";              // unique within the option, for tests and telemetry
        public int minYears = 4, maxYears = 15;   // the delay window in game years, after the dilemma ended
        public bool aliveA, aliveB;         // person a (b) of the dilemma must be alive
        public string needsBuilding;        // building types that must stand ("a|b" = either); null = none
        public string needsCustom;          // a custom (SimCore.HasCustom) that must be set; null = none
        public string text = "";
        public string fallback = "";        // told when a condition fails; empty = drop the callback quietly
        public float salience = 0.7f;
    }

    /// <summary>
    /// A dilemma as data, a storylet: conditions (era range, buildings, a free pair for the role, never repeated in a box) → content
    /// (question, two options with hints) → effects (on each option). The sim picks among the defs whose conditions hold.
    /// </summary>
    public sealed class DilemmaDef
    {
        public const string Leader = "leader";  // role = the era's leader role (chief, headman, lord)

        public string id = "";
        public DilemmaKind kind;
        public Era minEra = Era.Camp, maxEra = Era.Town;
        public string needs;                // building types that must stand ("a|b" = either), null = none (as the roles' table)
        public DilemmaCast cast;
        public string role;                 // RoleType (or Leader) the people are choosing for; null = an honour with no seat
        public bool replacesHolder;         // a held seat is open to the pick: the holder steps aside (false: the seat must be empty)
        public string question = "";        // the people's ask, {a}/{b}/{place}
        public DilemmaOptionDef[] options = new DilemmaOptionDef[2];
    }

    /// <summary>The defs. The 8 real dilemmas (4 camp, 4 village; who, which way, tradition, strangers) come with the content issue (#222).</summary>
    public static class DilemmaDefs
    {
        public static readonly DilemmaDef[] All =
        {
            // PLACEHOLDER (#220): replaced by the real defs in #222.
            new DilemmaDef
            {
                id = "ph_leader", kind = DilemmaKind.Who, cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true,
                question = "The people cannot agree who should lead: {a} or {b}?",
                options = new[]
                {
                    new DilemmaOptionDef { label = "{a}", hint = "{a} will lead their way, and {b} will mind it.", takesRole = true, ties = DilemmaTies.RivalOrGrace, custom = "leader:a",
                        callbacks = new[] { new DilemmaCallbackDef { id = "ph_a", aliveA = true, text = "{a} still runs {place} their way, and it shows.", fallback = "{place} still remembers when it chose {a}." } } },
                    new DilemmaOptionDef { label = "{b}", hint = "{b} will lead their way, and {a} will mind it.", takesRole = true, ties = DilemmaTies.RivalOrGrace, custom = "leader:b",
                        callbacks = new[] { new DilemmaCallbackDef { id = "ph_b", aliveB = true, text = "{b} still runs {place} their way, and it shows.", fallback = "{place} still remembers when it chose {b}." } } },
                },
            },
            new DilemmaDef
            {
                id = "ph_way", kind = DilemmaKind.WhichWay, cast = DilemmaCast.None,
                question = "Where should {place} put its hands this year: the fields, or the woods?",
                options = new[]
                {
                    new DilemmaOptionDef { label = "the fields", hint = "More to eat, and the woods will grow wild.", lean = SimCore.Lean.Food, favouredBy = new[] { "the kind", "the gardener", "the worrier" },
                        callbacks = new[] { new DilemmaCallbackDef { id = "ph_fields", text = "the fields still feed {place} well." } } },
                    new DilemmaOptionDef { label = "the woods", hint = "More to build with, and the fields will wait.", lean = SimCore.Lean.Craft, favouredBy = new[] { "the tinkerer", "the clever", "the brave" },
                        callbacks = new[] { new DilemmaCallbackDef { id = "ph_woods", text = "the woods still give {place} its timber." } } },
                },
            },
        };
    }
}
