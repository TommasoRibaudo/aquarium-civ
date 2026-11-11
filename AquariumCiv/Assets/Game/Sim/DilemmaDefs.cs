using System;
using System.Collections.Generic;

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
        public bool joins;                  // #578: with apart: when the village is made (or at the next new house in a village) they walk in and join
        public string apart = "";           // with strangers: they live apart instead of joining (ApartKind: "woods_camp", "shore_hut"; #231)
        public string buildingType = "";    // the place this option refers to ("Well"): its first building is the dilemma's place for the option
        public DilemmaCallbackDef[] callbacks;  // storylets that fire 4-15 years after this option was taken (answered or lapsed); null = none
        public string prop = "";            // #371: the thing this option leaves in the box (ConsequenceKind), within a game year; "" = none
        public PropAnchor propAt;           // where it goes (the person's home, the option's building, the fire, the edge, the pond)
        public string propBuilding = "";    // PropAnchor.Building: the building type to stand by when the option has no building of its own (else buildingType)
        public string[] favouredBy;         // traits (NameData.Traits) that tilt a lapse toward this option, when the role-holder has one
        public string icon = "";            // t4-fixes: the option's own mark on its tag in the box (a GlassIcon name); "" = none (a person option shows a person)
    }

    /// <summary>
    /// A callback (P7 rule 4, #221): a storylet gated on "this option was taken N years ago". Scheduled when the dilemma ends (the Keeper
    /// answered, or the people did after a lapse), by a hash of the seed and the dilemma, fired in the monthly step when due.
    /// Text placeholders: {a} {b} (the two people's names, dead or alive), {place}, {chosen} (the taken option's label) and
    /// {credit}: "Because the Keeper chose {chosen}" for an answer, "Because the people chose {chosen}" for a lapse. A text without
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
        public bool needsApart;             // the dilemma's apart group (#231) must have someone alive
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
        public bool landWords;              // #578: the words say where (the woods, the meadow or the pond: {land}, {camp}), set when it is raised from the land
        public bool needsDeath;             // #380: the def's words assume a loss ("the ones we lose"): only once someone in this box has died
        public string needs;                // building types that must stand ("a|b" = either), null = none (as the roles' table)
        public DilemmaCast cast;
        public string role;                 // RoleType (or Leader) the people are choosing for; null = an honour with no seat
        public bool replacesHolder;         // a held seat is open to the pick: the holder steps aside (false: the seat must be empty)
        public bool leaderElder;            // the era's leader must be an elder (SimConfig.elderAge): the people are thinking about who comes next
        public int termEndingMonths;        // > 0: the leader's term must end within this many months (an election is coming)
        public bool askerNotLeader;         // the eldest other adult asks, not the leader (a leader doesn't ask who replaces them)
        public DilemmaSize size = DilemmaSize.Big; // #368: a big choice (every ~6-10 h) or a small one between (C5)
        public string smallKind;            // #372: set on a small choice (SmallChoices.Naming...): it is raised from a moment of the box, not the era
        public string letDone = "";         // small choices: the line when the people take the pick ({n} = the name); empty = the plain line
        public string question = "";        // the people's ask, {a}/{b}/{place}
        public string shortAsk = "";        // #373: the ask in a few words for the Keeper's choices page; empty = cut from the question
        /// <summary>The question in the asker's voice, by their trait (the asker is the era's leader when it is raised, if they are
        /// not one of the two). A trait not listed, or no asker, gets <see cref="question"/>. Optional.</summary>
        public Dictionary<string, string> askedBy;
        /// <summary>For <see cref="DilemmaCast.TwoCandidates"/>: an option's hint by its person's trait, {p} = that person, {q} = the
        /// other one. A trait not listed gets the option's own <see cref="DilemmaOptionDef.hint"/>. Optional.</summary>
        public Dictionary<string, string> traitHints;
        public DilemmaOptionDef[] options = new DilemmaOptionDef[2];
    }

    /// <summary>
    /// The eight dilemmas of P7 (#222): four in the camp, four in the village, two of each kind (who, which way, tradition, strangers).
    /// The full text, for review in one place: docs/design/results/P7.md, "Dilemmas". Both options are good: a different history,
    /// never a better economy (a lean builds a different thing, food or wood, not more). Hints say what changes, never how much.
    /// The people's words (questions, hints, labels) never say "glass", "tank" or "box" (chronicle-and-history Choice 10, §5.1): they
    /// say what their era believes (camp: the sky wall; village: the Keeper's window). The narrator may say "a far-away tank". Callback texts are told after
    /// "Because the Keeper chose X, " (or "Because the people chose X, "), so they read on from a comma; labels read after "chose".
    /// </summary>
    public static class DilemmaDefs
    {
        static DilemmaCallbackDef Cb(string id, string text, string fallback = "", bool aliveA = false, bool aliveB = false, string needsBuilding = null,
            int minYears = 4, int maxYears = 15, bool needsApart = false) =>
            new DilemmaCallbackDef { id = id, text = text, fallback = fallback, aliveA = aliveA, aliveB = aliveB, needsBuilding = needsBuilding, minYears = minYears, maxYears = maxYears, needsApart = needsApart };

        // ---------- camp ----------

        /// <summary>Who: the chief is an elder, and the people are thinking about who leads next. An elder asks (never the chief).
        /// The chosen one becomes chief now (the old chief steps down).</summary>
        static readonly DilemmaDef CampFire = new DilemmaDef
        {
            id = "camp_fire", kind = DilemmaKind.Who, minEra = Era.Camp, maxEra = Era.Camp,
            cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true, leaderElder = true, askerNotLeader = true,
            question = "The chief is getting old. Who leads next: {a} or {b}?",
            askedBy = new Dictionary<string, string>
            {
                ["the gossip"] = "Chief's slowing down. Everyone says {a} or {b}. Which?",
                ["the worrier"] = "The chief is old. Who will lead us next: {a} or {b}?",
                ["the kind"] = "Let the chief rest at last. Who leads next: {a} or {b}?",
                ["the grump"] = "Chief's past it. Don't tell them I said so. {a} or {b}?",
                ["the joker"] = "The chief fell asleep mid-speech again. {a} or {b}?",
                ["the stubborn"] = "The chief won't say it, so I will. Next: {a} or {b}?",
            },
            traitHints = new Dictionary<string, string>
            {
                ["the stubborn"] = "{p} will lead one way, and never turn back.",
                ["the dreamer"] = "{p} will lead with big ideas, and some of them will work.",
                ["the kind"] = "{p} will lead gently, and nobody will go hungry.",
                ["the loud"] = "{p} will lead from the front, and everyone will hear it.",
                ["the clever"] = "{p} will lead with a plan for everything. Twice.",
                ["the lazy"] = "{p} will lead calmly. Very calmly. Mostly sitting down.",
                ["the brave"] = "{p} will lead the hunts farther out, and come back first.",
                ["the gossip"] = "{p} will lead by knowing everything about everyone.",
                ["the gardener"] = "{p} will lead {place} toward planting and tending.",
                ["the grump"] = "{p} will lead fairly, and complain the whole way.",
                ["the singer"] = "{p} will lead with a song for every job.",
                ["the tinkerer"] = "{p} will lead by fixing things nobody knew were broken.",
                ["the worrier"] = "{p} will lead carefully, with a plan for every storm.",
                ["the joker"] = "{p} will lead with a laugh, and {place} will follow.",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "{a}", hint = "{a} will lead {place} their own way, and {b} will watch.",
                    done = "The old chief stepped down, and the Keeper chose {a} to lead.",
                    takesRole = true, ties = DilemmaTies.RivalOrGrace,
                    prop = ConsequenceKind.Feather, propAt = PropAnchor.Home,
                    callbacks = new[]
                    {
                        Cb("fire_a_kept", "{place} still does things {a}'s way, down to who sits where at supper.",
                            "{place} still does things the way {a} did, down to who sits where at supper.", aliveA: true),
                        Cb("fire_a_other", "{b} never got to lead, but has given {a} advice every day since. Some of it was good.", aliveA: true, aliveB: true, minYears: 5),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "{b}", hint = "{b} will lead {place} their own way, and {a} will watch.",
                    done = "The old chief stepped down, and the Keeper chose {b} to lead.",
                    takesRole = true, ties = DilemmaTies.RivalOrGrace,
                    prop = ConsequenceKind.Feather, propAt = PropAnchor.Home,
                    callbacks = new[]
                    {
                        Cb("fire_b_kept", "{place} still does things {b}'s way, down to who sits where at supper.",
                            "{place} still does things the way {b} did, down to who sits where at supper.", aliveB: true),
                        Cb("fire_b_other", "{a} never got to lead, but has given {b} advice every day since. Some of it was good.", aliveA: true, aliveB: true, minYears: 5),
                    },
                },
            },
        };

        /// <summary>Which way: before the snow, the baskets (a food lean) or the woodpile (a wood lean).</summary>
        static readonly DilemmaDef CampWinter = new DilemmaDef
        {
            id = "camp_winter", kind = DilemmaKind.WhichWay, minEra = Era.Camp, maxEra = Era.Camp, needs = BuildingType.Lumbermill,
            question = "Before the snow: fill the baskets, or the woodpile?",
            askedBy = new Dictionary<string, string>
            {
                ["the worrier"] = "Winter's coming. It always is. Baskets or woodpile?",
                ["the lazy"] = "We can only do one before winter. Baskets or wood?",
                ["the gardener"] = "Bushes heavy, nights cold. Baskets or woodpile?",
                ["the grump"] = "Cold soon. Hungry soon. Pick one: baskets or wood?",
                ["the tinkerer"] = "I have a basket idea and a log idea. Which first?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "the baskets", hint = "Full bellies all winter, and a thinner woodpile.",
                    done = "The Keeper chose the baskets, and everyone went berrying.",
                    lean = SimCore.Lean.Food, custom = "winter:baskets", buildingType = BuildingType.Farm,
                    prop = ConsequenceKind.Baskets, propAt = PropAnchor.Building,
                    favouredBy = new[] { "the kind", "the gardener", "the lazy", "the dreamer", "the singer" },
                    callbacks = new[]
                    {
                        Cb("winter_baskets_berrying", "{place} still goes berrying every autumn, all together, and nobody comes home with clean fingers."),
                        Cb("winter_baskets_names", "the children of {place} know every berry bush by name. Some of the names are rude.", minYears: 6),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "the woodpile", hint = "Warm huts and new building, and a plainer winter table.",
                    done = "The Keeper chose the woodpile. The axes rang till the first snow.",
                    lean = SimCore.Lean.Craft, custom = "winter:wood", buildingType = BuildingType.Lumbermill,
                    prop = ConsequenceKind.WoodpileRoof, propAt = PropAnchor.Building,
                    favouredBy = new[] { "the tinkerer", "the brave", "the stubborn", "the clever", "the worrier" },
                    callbacks = new[]
                    {
                        Cb("winter_wood_roof", "{place}'s woodpile grew so tall it got a little roof of its own."),
                        Cb("winter_wood_leaks", "the roofs of {place} have never once leaked. People knock on them to show visitors.", minYears: 6),
                    },
                },
            },
        };

        /// <summary>Tradition: where the people remember their dead. A custom only: the stone by the pond, or the old tree.</summary>
        static readonly DilemmaDef CampRemember = new DilemmaDef
        {
            id = "camp_remember", kind = DilemmaKind.Tradition, minEra = Era.Camp, maxEra = Era.Camp, needsDeath = true, // #346: nobody to remember before the first death
            question = "Where do we remember the ones we lose: stone or tree?",
            askedBy = new Dictionary<string, string>
            {
                ["the kind"] = "Nobody should be forgotten. The stone, or the tree?",
                ["the dreamer"] = "I dreamt of the ones before us. Stone or tree?",
                ["the singer"] = "Where do I sing for the ones we lose: stone or tree?",
                ["the worrier"] = "What if we forget someone? Stone or tree, to remember?",
                ["the stubborn"] = "Stone. Obviously. But some say the tree. Well?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "the stone", hint = "Names on the big stone by the pond, there for good.",
                    done = "The Keeper chose the stone. The first name went on it that spring.",
                    custom = "remember:stone",
                    prop = ConsequenceKind.NameStone, propAt = PropAnchor.Pond,
                    favouredBy = new[] { "the stubborn", "the clever", "the grump", "the brave", "the tinkerer" },
                    callbacks = new[]
                    {
                        Cb("remember_stone_polish", "the big stone by the pond is covered in names now, and the children polish it with their sleeves."),
                        Cb("remember_stone_stories", "anyone in {place} can tell you who every name on the stone was. Some of the stories are even true.", minYears: 7),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "the tree", hint = "A charm on the old tree for each of them, growing with it.",
                    done = "The Keeper chose the old tree. The first charm went up that spring.",
                    custom = "remember:tree",
                    prop = ConsequenceKind.CharmPole, propAt = PropAnchor.Edge,
                    favouredBy = new[] { "the kind", "the gardener", "the dreamer", "the singer", "the worrier" },
                    callbacks = new[]
                    {
                        Cb("remember_tree_charms", "the old tree is hung with little charms, and they click together in the wind like someone laughing."),
                        Cb("remember_tree_branch", "nobody in {place} will cut a branch from the old tree. Not even the low one that hits everyone on the head.", minYears: 7),
                    },
                },
            },
        };

        /// <summary>Strangers: lost travellers who wandered in from a far-away tank. Two join (a couple), or they camp apart in the far
        /// woods (#231: an apart group the player can see) and leave their axe trick behind (a wood lean).</summary>
        static readonly DilemmaDef CampTravellers = new DilemmaDef
        {
            id = "camp_travellers", kind = DilemmaKind.Strangers, minEra = Era.Camp, maxEra = Era.Camp,
            landWords = true,
            question = "Lost travellers at the edge of {land}. Can they stay?",
            askedBy = new Dictionary<string, string>
            {
                ["the kind"] = "Two lost travellers, cold and hungry. Can they stay?",
                ["the worrier"] = "Strangers! Two of them. Probably fine? Can they stay?",
                ["the gossip"] = "Travellers! From past the sky wall, they say. Can they stay?",
                ["the brave"] = "I found two travellers by {land}. Can they stay?",
                ["the grump"] = "Two more mouths at the edge of {land}. Can they stay?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "a place by the fire", hint = "Two new faces round the fire, and new ways of doing things.",
                    done = "The Keeper let the travellers from a far-away tank stay. They slept by the fire and never left.",
                    strangers = 2, custom = "travellers:stayed",
                    prop = ConsequenceKind.StewPot, propAt = PropAnchor.Fire,
                    favouredBy = new[] { "the kind", "the singer", "the gossip", "the dreamer", "the joker", "the loud" },
                    callbacks = new[]
                    {
                        Cb("travellers_stew", "the travellers who stayed still cook their own strange stew. Half of {place} asks for it now."),
                        Cb("travellers_knots", "the travellers' way of tying a knot caught on. Everything in {place} is tied twice now.", minYears: 6),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "full packs", hint = "They camp {camp} with their axe trick, and join us later.",
                    done = "The Keeper sent the travellers from a far-away tank to camp {camp}, with full packs.",
                    lean = SimCore.Lean.Craft, custom = "travellers:sent", strangers = 2, apart = ApartKind.WoodsCamp, joins = true,
                    prop = ConsequenceKind.AxeStump, propAt = PropAnchor.Edge,
                    favouredBy = new[] { "the grump", "the worrier", "the stubborn", "the clever", "the tinkerer", "the lazy" },
                    callbacks = new[]
                    {
                        Cb("travellers_notch", "every axe in {place} still has the travellers' little notch in the handle. Nobody knows why it helps. It helps."),
                        Cb("travellers_wave", "the travellers still live wild {camp}. Some evenings you can see their fire, and all of {place} waves at it.",
                            "the travellers' old camp {camp} is grass again, but {place} still waves at the spot some evenings.", needsApart: true, minYears: 6),
                    },
                },
            },
        };

        // ---------- village ----------

        /// <summary>Who: the headman's term is nearly up and the village is split over the next one. An elder asks (never the
        /// headman). The chosen one takes the seat now (the headman hands over early).</summary>
        static readonly DilemmaDef VillageHeadman = new DilemmaDef
        {
            id = "village_headman", kind = DilemmaKind.Who, minEra = Era.Village, maxEra = Era.Village,
            cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true, termEndingMonths = 24, askerNotLeader = true,
            question = "A vote soon, and {place} is split. Headman: {a} or {b}?",
            askedBy = new Dictionary<string, string>
            {
                ["the gossip"] = "Everyone's whispering about the vote. {a} or {b}?",
                ["the grump"] = "Another vote, another row. Headman: {a} or {b}?",
                ["the kind"] = "Let's not fall out over the vote. {a} or {b}?",
                ["the lazy"] = "Counting votes is hard. Just pick: {a} or {b}?",
                ["the worrier"] = "What if the vote comes out even? {a} or {b}?",
            },
            traitHints = new Dictionary<string, string>
            {
                ["the stubborn"] = "{p} will run {place} one way only. A straight way.",
                ["the dreamer"] = "{p} will have big plans for {place}, and some will happen.",
                ["the kind"] = "{p} will see that nobody goes without, and talk to everyone.",
                ["the loud"] = "{p} will hold meetings you can hear from the pond.",
                ["the clever"] = "{p} will settle every quarrel with a clever new rule.",
                ["the lazy"] = "{p} will keep the meetings short and the benches comfy.",
                ["the brave"] = "{p} will stand up for {place} first and ask after.",
                ["the gossip"] = "{p} will know what everyone wants before they say it.",
                ["the gardener"] = "{p} will put flowers by every bench and tidy the fields.",
                ["the grump"] = "{p} will grumble at everyone fairly, and get things done.",
                ["the singer"] = "{p} will open every meeting with a song. A long one.",
                ["the tinkerer"] = "{p} will mend everything in {place}, starting with the well.",
                ["the worrier"] = "{p} will check on every hearth, twice, and keep {place} careful.",
                ["the joker"] = "{p} will make the meetings fun, and nobody will skip them.",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "{a}", hint = "{a} will run {place} their way, and {b} will watch.",
                    done = "The vote was split, and the Keeper chose {a} as headman.",
                    takesRole = true, ties = DilemmaTies.RivalOrGrace,
                    prop = ConsequenceKind.Staff, propAt = PropAnchor.Home,
                    callbacks = new[]
                    {
                        Cb("headman_a_meetings", "the meetings by the well still go {a}'s way, and they still start late.",
                            "{place} still holds its meetings the way {a} did, and they still start late.", aliveA: true),
                        Cb("headman_a_back", "{b} sits at the back of every meeting with folded arms, and has the best ideas there.", aliveB: true, minYears: 5),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "{b}", hint = "{b} will run {place} their way, and {a} will watch.",
                    done = "The vote was split, and the Keeper chose {b} as headman.",
                    takesRole = true, ties = DilemmaTies.RivalOrGrace,
                    prop = ConsequenceKind.Staff, propAt = PropAnchor.Home,
                    callbacks = new[]
                    {
                        Cb("headman_b_meetings", "the meetings by the well still go {b}'s way, and they still start late.",
                            "{place} still holds its meetings the way {b} did, and they still start late.", aliveB: true),
                        Cb("headman_b_back", "{a} sits at the back of every meeting with folded arms, and has the best ideas there.", aliveA: true, minYears: 5),
                    },
                },
            },
        };

        /// <summary>Which way: the miller and the roofers both want the young ones' hands this year (a food lean or a wood lean).</summary>
        static readonly DilemmaDef VillageSpring = new DilemmaDef
        {
            id = "village_spring", kind = DilemmaKind.WhichWay, minEra = Era.Village, maxEra = Era.Village, needs = BuildingType.Windmill,
            question = "Miller or roofers: who gets the young ones this year?",
            askedBy = new Dictionary<string, string>
            {
                ["the gardener"] = "The miller and the roofers both want the young ones. Who?",
                ["the tinkerer"] = "Miller or roofers? I made a ladder for either. Who?",
                ["the worrier"] = "Roofs leaking, grain ripe. Who gets the young ones?",
                ["the loud"] = "MILLER OR ROOFERS? Sorry. Who gets the young ones?",
                ["the lazy"] = "Everyone wants the young ones but me. Miller or roofers?",
                ["the grump"] = "The miller's shouting and the roofers are shouting. Who?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "the miller", hint = "Full sacks and fresh bread, and the roofs drip a while longer.",
                    done = "The Keeper sent the young ones to the miller. The mill sang all summer.",
                    lean = SimCore.Lean.Food, custom = "spring:miller", buildingType = BuildingType.Windmill,
                    prop = ConsequenceKind.FlourSacks, propAt = PropAnchor.Building,
                    favouredBy = new[] { "the gardener", "the kind", "the singer", "the lazy", "the worrier" },
                    callbacks = new[]
                    {
                        Cb("spring_grain_mill", "the miller still calls them \"my young ones\", though they're all grown and one has a beard.",
                            "{place} still bakes the best bread anyone remembers, and says so to everyone.", needsBuilding: BuildingType.Windmill),
                        Cb("spring_grain_flour", "{place} had so much flour one autumn that the children had a flour fight. There was no winner.", minYears: 5),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "the roofers", hint = "Dry roofs and new rafters, and the bread a little thin.",
                    done = "The Keeper sent the young ones up the ladders. Sawdust got into everything, including the soup.",
                    lean = SimCore.Lean.Craft, custom = "spring:roofers", buildingType = BuildingType.Hut,
                    prop = ConsequenceKind.RoofBird, propAt = PropAnchor.Building,
                    favouredBy = new[] { "the tinkerer", "the clever", "the stubborn", "the brave", "the loud" },
                    callbacks = new[]
                    {
                        Cb("spring_timber_birds", "every roof the young ones mended that year has a little carved bird on the ridge. Now every new roof gets one."),
                        Cb("spring_timber_squirrels", "the young roofers of that year still climb up to watch the sunset. Nobody can get them down for supper.", minYears: 5),
                    },
                },
            },
        };

        /// <summary>Tradition: how the harvest is marked. A custom only: a feast, or an offering for the Keeper.</summary>
        static readonly DilemmaDef VillageHarvest = new DilemmaDef
        {
            id = "village_harvest", kind = DilemmaKind.Tradition, minEra = Era.Village, maxEra = Era.Village,
            question = "How do we mark the harvest: a feast or an offering?",
            askedBy = new Dictionary<string, string>
            {
                ["the singer"] = "The harvest needs a song. A feast song, or an offering one?",
                ["the joker"] = "Harvest's in! Do we eat it, or give it to the Keeper?",
                ["the kind"] = "Share the harvest round our tables, or with the Keeper?",
                ["the dreamer"] = "Maybe the Keeper is hungry too. Feast or offering?",
                ["the grump"] = "Either way I carry the tables. Feast or offering?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "a feast", hint = "Long tables with everyone at them, and the Keeper watching.",
                    done = "The Keeper chose a feast. The tables ran the length of {place}.",
                    custom = "harvest:feast",
                    prop = ConsequenceKind.LongTable, propAt = PropAnchor.Fire,
                    favouredBy = new[] { "the loud", "the joker", "the singer", "the gossip", "the lazy" },
                    callbacks = new[]
                    {
                        Cb("harvest_feast_days", "the harvest feast at {place} lasts three days now. Nobody remembers agreeing to the third."),
                        Cb("harvest_feast_table", "the tavern keeps one long table just for the harvest feast, and nobody may eat at it the rest of the year.",
                            needsBuilding: BuildingType.Tavern, minYears: 5),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "an offering", hint = "The best basket left on the hill for the Keeper, and quiet thanks.",
                    done = "The Keeper chose an offering. The best basket went up the hill, very carefully.",
                    custom = "harvest:offering",
                    prop = ConsequenceKind.OfferingBasket, propAt = PropAnchor.Edge,
                    favouredBy = new[] { "the kind", "the dreamer", "the worrier", "the gardener", "the stubborn" },
                    callbacks = new[]
                    {
                        Cb("harvest_offering_basket", "the best basket goes up the hill every autumn, and every spring it's empty. The birds aren't saying."),
                        Cb("harvest_offering_crumb", "the children of {place} leave a crumb on the sill for the Keeper after every meal. Just in case.", minYears: 5),
                    },
                },
            },
        };

        /// <summary>Strangers: a family who wandered in from a far-away tank, camped across the pond. Three join, or they keep a hut on
        /// the far shore (#231: an apart group) and trade their catch (a food lean).</summary>
        static readonly DilemmaDef VillageHillFamily = new DilemmaDef
        {
            id = "village_hill_family", kind = DilemmaKind.Strangers, minEra = Era.Village, maxEra = Era.Village,
            question = "The family across the pond asks for a home. Here, or there?",
            askedBy = new Dictionary<string, string>
            {
                ["the kind"] = "The family across the pond wants a home. Where?",
                ["the stubborn"] = "A family wants a home. Here, or on their own shore?",
                ["the joker"] = "A family across the pond. Here, or near enough to wave?",
                ["the loud"] = "A FAMILY! Across the pond! Where do they live?",
                ["the gossip"] = "I've met the pond family already. Here, or there?",
                ["the dreamer"] = "They came from behind the Keeper's window! Here, or there?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "a home here", hint = "New neighbours at the next hearth, and new names in {place}.",
                    done = "The Keeper gave the family from a far-away tank a home here. They moved in with a goat.",
                    strangers = 3, custom = "pond:joined",
                    prop = ConsequenceKind.GoatFence, propAt = PropAnchor.Fire,
                    favouredBy = new[] { "the kind", "the gossip", "the singer", "the loud", "the joker" },
                    callbacks = new[]
                    {
                        Cb("hill_joined_songs", "the pond family still sing their shore songs at supper. Half of {place} knows the words now."),
                        Cb("hill_joined_goat", "the pond family's goat has eaten its way through every garden in {place}. It is much loved anyway.", minYears: 5),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "a home across the pond", hint = "They'll keep their own shore, and trade us their catch.",
                    done = "The Keeper left the family from a far-away tank on their shore. They waved, and sent fish.",
                    lean = SimCore.Lean.Food, custom = "pond:neighbours", strangers = 3, apart = ApartKind.ShoreHut,
                    prop = ConsequenceKind.FishCrate, propAt = PropAnchor.Pond,
                    favouredBy = new[] { "the grump", "the worrier", "the stubborn", "the gardener", "the lazy" },
                    callbacks = new[]
                    {
                        Cb("hill_neighbours_eggs", "the family across the pond still rows over every spring with fish. Far too many fish.",
                            "the hut across the pond stands empty now, but {place} still leaves a fish on its step each spring.", needsApart: true),
                        Cb("hill_neighbours_path", "there's a path worn round the pond now, from {place} to the neighbours and back. Nobody built it. Everyone walked it.", minYears: 6),
                    },
                },
            },
        };

        public static readonly DilemmaDef[] All =
        {
            CampFire, CampWinter, CampRemember, CampTravellers,
            VillageHeadman, VillageSpring, VillageHarvest, VillageHillFamily,
        };
    }
}

