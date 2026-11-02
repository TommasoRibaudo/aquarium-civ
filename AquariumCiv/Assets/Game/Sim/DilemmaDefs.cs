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
    /// The people's words (questions, hints, labels) never say "glass" (chronicle-and-history Choice 10). Callback texts are told after
    /// "Because the Keeper chose X, " (or "Because they chose X, "), so they read on from a comma; labels read after "chose".
    /// </summary>
    public static class DilemmaDefs
    {
        static DilemmaCallbackDef Cb(string id, string text, string fallback = "", bool aliveA = false, bool aliveB = false, string needsBuilding = null,
            int minYears = 4, int maxYears = 15) =>
            new DilemmaCallbackDef { id = id, text = text, fallback = fallback, aliveA = aliveA, aliveB = aliveB, needsBuilding = needsBuilding, minYears = minYears, maxYears = maxYears };

        // ---------- camp ----------

        /// <summary>Who: the chief hands on the fire. The chosen one takes the chief's seat (the old chief steps aside).</summary>
        static readonly DilemmaDef CampFire = new DilemmaDef
        {
            id = "camp_fire", kind = DilemmaKind.Who, minEra = Era.Camp, maxEra = Era.Camp,
            cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true,
            question = "Who keeps the fire now: {a} or {b}?",
            askedBy = new Dictionary<string, string>
            {
                ["the lazy"] = "I'd like a long sit. Who keeps the fire: {a} or {b}?",
                ["the stubborn"] = "Not tired. Resting. Who keeps the fire: {a} or {b}?",
                ["the grump"] = "My knees are done. Who keeps the fire: {a} or {b}?",
                ["the joker"] = "I'm handing on the fire. It's hot. {a} or {b}?",
                ["the dreamer"] = "The clouds need watching. The fire: {a} or {b}?",
                ["the worrier"] = "Someone must mind the fire, properly. {a} or {b}?",
            },
            traitHints = new Dictionary<string, string>
            {
                ["the stubborn"] = "{p} will keep the fire exactly where it is. Forever.",
                ["the dreamer"] = "{p} will tell stories by the fire till the stars go out.",
                ["the kind"] = "{p} will feed anyone who sits down by the fire.",
                ["the loud"] = "{p} will call everyone to supper. Everyone, everywhere.",
                ["the clever"] = "{p} will build a cleverer fire. It may need explaining.",
                ["the lazy"] = "{p} will keep the fire low and the naps long.",
                ["the brave"] = "{p} will lead the hunt farther out, to see what's there.",
                ["the gossip"] = "{p} will know everyone's business by the first night.",
                ["the gardener"] = "{p} will plant something round the fire. Probably beans.",
                ["the grump"] = "{p} will keep the fire, and keep everyone off the good log.",
                ["the singer"] = "{p} will sing by the fire every night. Every night.",
                ["the tinkerer"] = "{p} will fiddle with the fire until it does something new.",
                ["the worrier"] = "{p} will check the fire all night, so the rest can sleep.",
                ["the joker"] = "{p} will keep the fire, and keep everyone laughing round it.",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "{a}", hint = "{a} will keep the fire their own way, and {b} will watch.",
                    done = "The people asked who should keep the fire, and the Keeper chose {a}.",
                    takesRole = true, ties = DilemmaTies.RivalOrGrace,
                    callbacks = new[]
                    {
                        Cb("fire_a_kept", "the fire at {place} has not gone out once. {a} sleeps beside it, one eye open.",
                            "the fire at {place} is still laid the way {a} liked it, big logs at the back.", aliveA: true),
                        Cb("fire_a_other", "{b} started a little fire of their own at the far end of {place}. The young ones like it better.", aliveB: true, minYears: 5),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "{b}", hint = "{b} will keep the fire their own way, and {a} will watch.",
                    done = "The people asked who should keep the fire, and the Keeper chose {b}.",
                    takesRole = true, ties = DilemmaTies.RivalOrGrace,
                    callbacks = new[]
                    {
                        Cb("fire_b_kept", "the fire at {place} has not gone out once. {b} sleeps beside it, one eye open.",
                            "the fire at {place} is still laid the way {b} liked it, big logs at the back.", aliveB: true),
                        Cb("fire_b_other", "{a} started a little fire of their own at the far end of {place}. The young ones like it better.", aliveA: true, minYears: 5),
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
            id = "camp_remember", kind = DilemmaKind.Tradition, minEra = Era.Camp, maxEra = Era.Camp,
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
                    favouredBy = new[] { "the kind", "the gardener", "the dreamer", "the singer", "the worrier" },
                    callbacks = new[]
                    {
                        Cb("remember_tree_charms", "the old tree is hung with little charms, and they click together in the wind like someone laughing."),
                        Cb("remember_tree_branch", "nobody in {place} will cut a branch from the old tree. Not even the low one that hits everyone on the head.", minYears: 7),
                    },
                },
            },
        };

        /// <summary>Strangers: lost travellers. Two join (a couple), or they go on and leave their axe trick behind (a wood lean).</summary>
        static readonly DilemmaDef CampTravellers = new DilemmaDef
        {
            id = "camp_travellers", kind = DilemmaKind.Strangers, minEra = Era.Camp, maxEra = Era.Camp,
            question = "Lost travellers at the edge of the woods. Can they stay?",
            askedBy = new Dictionary<string, string>
            {
                ["the kind"] = "Two lost travellers, cold and hungry. Can they stay?",
                ["the worrier"] = "Strangers! Two of them. Probably fine? Can they stay?",
                ["the gossip"] = "Strangers in the woods! I saw them first. Can they stay?",
                ["the brave"] = "I found two travellers in the woods. Can they stay?",
                ["the grump"] = "Two more mouths at the edge of the woods. Can they stay?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "a place by the fire", hint = "Two new faces round the fire, and new ways of doing things.",
                    done = "The Keeper let the travellers stay. They slept by the fire the first night and never left.",
                    strangers = 2, custom = "travellers:stayed",
                    favouredBy = new[] { "the kind", "the singer", "the gossip", "the dreamer", "the joker", "the loud" },
                    callbacks = new[]
                    {
                        Cb("travellers_stew", "the travellers who stayed still cook their own strange stew. Half of {place} asks for it now."),
                        Cb("travellers_knots", "the travellers' way of tying a knot caught on. Everything in {place} is tied twice now.", minYears: 6),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "full packs", hint = "They'll go on with full packs, and leave us their trick with an axe.",
                    done = "The Keeper sent the travellers on with full packs. They showed everyone a thing or two with an axe first.",
                    lean = SimCore.Lean.Craft, custom = "travellers:sent",
                    favouredBy = new[] { "the grump", "the worrier", "the stubborn", "the clever", "the tinkerer", "the lazy" },
                    callbacks = new[]
                    {
                        Cb("travellers_notch", "every axe in {place} still has the travellers' little notch in the handle. Nobody knows why it helps. It helps."),
                        Cb("travellers_wave", "the travellers came back through once, years later, and waved from the edge of the woods. All of {place} waved back.", minYears: 6),
                    },
                },
            },
        };

        // ---------- village ----------

        /// <summary>Who: the village is split over its headman. The chosen one takes the seat (the headman steps aside).</summary>
        static readonly DilemmaDef VillageHeadman = new DilemmaDef
        {
            id = "village_headman", kind = DilemmaKind.Who, minEra = Era.Village, maxEra = Era.Village,
            cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true,
            question = "{place} is split down the middle. Headman: {a} or {b}?",
            askedBy = new Dictionary<string, string>
            {
                ["the lazy"] = "Headman's a lot of talking. Could {a} or {b} do it?",
                ["the kind"] = "I've had my turn. Headman next: {a} or {b}?",
                ["the grump"] = "I'm done being argued at. Headman: {a} or {b}?",
                ["the gossip"] = "Everyone's whispering. Headman: {a} or {b}?",
                ["the dreamer"] = "I'd rather be dreaming. Headman next: {a} or {b}?",
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
                    done = "{place} was split, and the Keeper chose {a} as headman.",
                    takesRole = true, ties = DilemmaTies.RivalOrGrace,
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
                    done = "{place} was split, and the Keeper chose {b} as headman.",
                    takesRole = true, ties = DilemmaTies.RivalOrGrace,
                    callbacks = new[]
                    {
                        Cb("headman_b_meetings", "the meetings by the well still go {b}'s way, and they still start late.",
                            "{place} still holds its meetings the way {b} did, and they still start late.", aliveB: true),
                        Cb("headman_b_back", "{a} sits at the back of every meeting with folded arms, and has the best ideas there.", aliveA: true, minYears: 5),
                    },
                },
            },
        };

        /// <summary>Which way: the mill wants grain, the roofs want timber (a food lean or a wood lean).</summary>
        static readonly DilemmaDef VillageSpring = new DilemmaDef
        {
            id = "village_spring", kind = DilemmaKind.WhichWay, minEra = Era.Village, maxEra = Era.Village, needs = BuildingType.Windmill,
            question = "The mill wants grain, the roofs want timber. Which?",
            askedBy = new Dictionary<string, string>
            {
                ["the gardener"] = "The fields are begging, and so are the roofs. Which?",
                ["the tinkerer"] = "Grain for the mill, or timber for my next idea?",
                ["the worrier"] = "Grain or timber this year? We can't do both. Can we?",
                ["the loud"] = "GRAIN OR TIMBER? Sorry. Grain or timber?",
                ["the lazy"] = "Grain or timber. Pick the easy one. Which is that?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "the grain", hint = "Fuller bins and busier hearths, and the timber can wait.",
                    done = "The Keeper chose the grain. The mill sang all summer.",
                    lean = SimCore.Lean.Food, custom = "spring:grain", buildingType = BuildingType.Windmill,
                    favouredBy = new[] { "the gardener", "the kind", "the singer", "the lazy", "the worrier" },
                    callbacks = new[]
                    {
                        Cb("spring_grain_mill", "the mill at {place} still turns from dawn to dark, and the miller has stopped complaining about it.",
                            "{place} still bakes the best bread anyone remembers, and says so to everyone.", needsBuilding: BuildingType.Windmill),
                        Cb("spring_grain_flour", "{place} had so much flour one autumn that the children had a flour fight. There was no winner.", minYears: 5),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "the timber", hint = "New roofs and new building, and plainer bread for a while.",
                    done = "The Keeper chose the timber. Sawdust got into everything, including the soup.",
                    lean = SimCore.Lean.Craft, custom = "spring:timber", buildingType = BuildingType.Lumbermill,
                    favouredBy = new[] { "the tinkerer", "the clever", "the stubborn", "the brave", "the loud" },
                    callbacks = new[]
                    {
                        Cb("spring_timber_birds", "there's hardly a roof in {place} without a carved bird on the ridge. It started as a joke."),
                        Cb("spring_timber_squirrels", "the sawpit is still the busiest place in {place}. The squirrels have moved out in protest.", minYears: 5),
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
                    favouredBy = new[] { "the kind", "the dreamer", "the worrier", "the gardener", "the stubborn" },
                    callbacks = new[]
                    {
                        Cb("harvest_offering_basket", "the best basket goes up the hill every autumn, and every spring it's empty. The birds aren't saying."),
                        Cb("harvest_offering_crumb", "the children of {place} leave a crumb on the sill for the Keeper after every meal. Just in case.", minYears: 5),
                    },
                },
            },
        };

        /// <summary>Strangers: a family from over the hill. Three join, or they stay neighbours and trade their spare grain (a food lean).</summary>
        static readonly DilemmaDef VillageHillFamily = new DilemmaDef
        {
            id = "village_hill_family", kind = DilemmaKind.Strangers, minEra = Era.Village, maxEra = Era.Village,
            question = "A family from over the hill asks for a home. Here, or there?",
            askedBy = new Dictionary<string, string>
            {
                ["the kind"] = "A whole family from over the hill. Where shall they live?",
                ["the stubborn"] = "A family wants a home. Here, or on their own hill?",
                ["the joker"] = "New family over the hill. Here, or near enough to wave?",
                ["the loud"] = "A FAMILY! From over the hill! Where do they live?",
                ["the gossip"] = "I've met the hill family already. Here, or there?",
            },
            options = new[]
            {
                new DilemmaOptionDef
                {
                    label = "a home here", hint = "New neighbours at the next hearth, and new names in {place}.",
                    done = "The Keeper gave the hill family a home here. They moved in with a goat.",
                    strangers = 3, custom = "hill:joined",
                    favouredBy = new[] { "the kind", "the gossip", "the singer", "the loud", "the joker" },
                    callbacks = new[]
                    {
                        Cb("hill_joined_songs", "the hill family still sing their hill songs at supper. Half of {place} knows the words now."),
                        Cb("hill_joined_goat", "the hill family's goat has eaten its way through every garden in {place}. It is much loved anyway.", minYears: 5),
                    },
                },
                new DilemmaOptionDef
                {
                    label = "a home over the hill", hint = "They'll keep their own hill, and trade us their spare grain.",
                    done = "The Keeper left the hill family on their hill. They waved, and sent eggs.",
                    lean = SimCore.Lean.Food, custom = "hill:neighbours",
                    favouredBy = new[] { "the grump", "the worrier", "the stubborn", "the gardener", "the lazy" },
                    callbacks = new[]
                    {
                        Cb("hill_neighbours_eggs", "the family over the hill still comes down every spring with eggs. Far too many eggs."),
                        Cb("hill_neighbours_path", "there's a path worn over the hill now, from {place} to the neighbours and back. Nobody built it. Everyone walked it.", minYears: 6),
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

