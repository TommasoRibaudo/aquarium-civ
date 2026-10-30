using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// Story layer ("Narrative Legos", prototype-plan.md P3): about 30 templates filled from sim state, each with a
    /// salience for the chronicle cards (Gate 2). Weddings, friendships and feuds link notables; trait incidents give
    /// people a voice; weather and oddities give the world one. Draws only from <see cref="SimState.storyRng"/>, so it
    /// never shifts the economy's dice.
    /// </summary>
    public partial class SimCore
    {
        // One-off firsts, kept in storyFlags.
        const string FlagWedding = "wedding", FlagSnow = "snow", FlagNamed = "named";

        /// <summary>Trait incidents: {0} = the person, {1} = a building with a roof, {2} = the era noun (camp, village, town),
        /// {3} = what the people call the Keeper ("the Bright One" in the camp). Two lines each, told in turn (#61), so a
        /// long life doesn't repeat itself.</summary>
        static readonly Dictionary<string, (string kind, float salience, string[] lines)> TraitStories = new()
        {
            ["the tinkerer"] = ("invention", 0.62f, new[] { "{0} invented a bucket that pours itself.", "{0} built a wheelbarrow with five wheels. It mostly works." }),
            ["the clever"] = ("invention", 0.62f, new[] { "{0} invented a mirror for signalling the Keeper.", "{0} worked out how to tell the time from the glass's shadow." }),
            ["the singer"] = ("song", 0.5f, new[] { "{0} wrote a song about the Keeper's giant thumb. Everyone hums it now.", "{0} sang at the {1} until the rafters shook." }),
            ["the joker"] = ("prank", 0.52f, new[] { "{0} painted the {1} roof bright pink overnight.", "{0} hid every spoon in the {2} inside the {1}." }),
            ["the gardener"] = ("giant_turnip", 0.55f, new[] { "{0} grew a turnip the size of a sheep. It fed the whole {2} for a week.", "{0} planted flowers around the {1}. The bees approve." }),
            ["the brave"] = ("brave", 0.5f, new[] { "{0} chased a fox away from the hens with a frying pan.", "{0} climbed the {1} roof in a storm to save a kitten." }),
            ["the stubborn"] = ("stubborn", 0.48f, new[] { "{0} refused to move for the new path. The path now goes around them.", "{0} insisted the {1} was built backwards. It wasn't." }),
            ["the worrier"] = ("worrier", 0.45f, new[] { "{0} built a little fence around the pond, just in case.", "{0} counted the people of the {2} twice a day for a month." }),
            ["the gossip"] = ("gossip", 0.5f, new[] { "{0} spread a rumour that the Keeper has a cat. It caused quite a stir.", "{0} knew about the wedding before the couple did." }),
            ["the grump"] = ("grump", 0.45f, new[] { "{0} complained about the noise from the {1} for a whole season.", "{0} said the harvest festival was too loud, then danced all night." }),
            ["the dreamer"] = ("dreamer", 0.55f, new[] { "{0} dreamt of a city beyond the glass and drew a map of it.", "{0} spent a summer trying to talk to the clouds." }),
            ["the lazy"] = ("lazy", 0.45f, new[] { "{0} napped through the entire harvest. Nobody had the heart to wake them.", "{0} invented a hammock. It was their only invention." }),
            ["the kind"] = ("kind", 0.5f, new[] { "{0} took in a stray goat. It follows them everywhere now.", "{0} baked bread for every hearth after the storm." }),
            ["the loud"] = ("loud", 0.45f, new[] { "{0} sneezed so hard the birds left every tree in the {2}.", "{0} called the goats home from the far side of the glass." }),
        };

        /// <summary>
        /// Lines that know too much for the camp and village (chronicle-and-history.md §5.1, relics-and-glass-voice.md §4):
        /// town-only (stage 3). Before the town each is told as its stand-in, so every trait keeps two lines. The swap
        /// happens after the dice: the story stream draws exactly as before (same options, weights and picks), so storms,
        /// droughts and harvests land where they did and the economy can't move. Wording waits on the user: plain strings.
        /// </summary>
        static readonly Dictionary<string, string> TownOnlyLines = new()
        {
            ["{0} invented a mirror for signalling the Keeper."] =
                "{0} made a stick that throws a shadow, and now everyone knows when lunch is.",
            ["{0} worked out how to tell the time from the glass's shadow."] =
                "{0} invented a better basket. It holds slightly more berries and everyone is very impressed.",
            ["{0} spread a rumour that the Keeper has a cat. It caused quite a stir."] =
                "{0} swears {3} sneezed last spring. Half the {2} believes it.",
            ["{0} dreamt of a city beyond the glass and drew a map of it."] =
                "{0} dreamt they climbed to the edge of the sky and knocked. Something knocked back.",
        };

        /// <summary>The strange light, before and after the Keeper's Lamp has been used (relics-and-glass-voice.md §4).</summary>
        static readonly string[] StrangeLightLamp =
        {
            "A strange warm light crossed the sky. Some say it was the Keeper's lamp.",
            "The Keeper's lamp swung across the sky again. The children ran along underneath it.",
        };
        /// <summary>Before the Lamp: {0} = what the people call the Keeper (#61: "the Bright One" only in the camp).</summary>
        static readonly string[] StrangeLightEarly =
        {
            "A strange warm light crossed the sky. Some say {0} was looking for something.",
            "The strange warm light came back and lingered over the pond. Some say {0} had dropped something small.",
        };
        public const string FlagLampUsed = "lamp-used";

        /// <summary>A trait line as told in this era: the town-only ones become their stand-ins before the town.</summary>
        string EraLine(string line) =>
            CardsOn && State.era < Era.Town && TownOnlyLines.TryGetValue(line, out var early) ? early : line;

        /// <summary>What the people call the Keeper in this era (relics-and-glass-voice.md §2): the Bright One in the camp.</summary>
        string KeeperWord => State.era == Era.Camp ? "the Bright One" : "the Keeper";

        /// <summary>How many entries of this kind (about this person, if given) the chronicle already holds: picks the next
        /// wording of a repeating event (#61), so the same sentence doesn't come round again. No dice.</summary>
        int Told(string kind, int notableId = -1)
        {
            int n = 0;
            foreach (var e in State.chronicle) if (e.kind == kind && (notableId < 0 || e.notableId == notableId)) n++;
            return n + PrunedTold(kind, notableId); // lines dropped from an old chronicle still count as told (SimCore.History.cs)
        }

        static string Nth(string[] lines, int n) => lines[n % lines.Length];

        /// <summary>The thing found, unless this person found just that last time (#61: the same pebble came back twice). The
        /// drawn thing still decides it, so the dice are untouched.</summary>
        string FoundNext(string drawn, int who)
        {
            var last = State.chronicle.LastOrDefault(e => e.kind == "lost" && e.notableId == who);
            return last != null && last.text.Contains(drawn) ? FoundThings[(Array.IndexOf(FoundThings, drawn) + 1) % FoundThings.Length] : drawn;
        }

        /// <summary>The drawn quarrel, unless these two fell out over just that last time (#61: the same pair fell out over
        /// the same song lyric twice). The draw still decides it; no dice.</summary>
        string QuarrelAgain(string drawn, int a, int b) => QuarrelGated(QuarrelAgainRaw(drawn, a, b));

        /// <summary>A quarrel can only be over what is in the box (#86): no well, no quarrel at the well; no windmill
        /// to grind the grain, none about baking. Same draw, a plain fallback.</summary>
        public string QuarrelGated(string q)
        {
            if (q == Quarrels[1] && Count(BuildingType.Well) == 0) return Quarrels[3];
            if (q == Quarrels[2] && Count(BuildingType.Windmill) == 0) return Quarrels[0];
            return q;
        }

        string QuarrelAgainRaw(string drawn, int a, int b)
        {
            var last = State.chronicle.LastOrDefault(e => e.kind == "rivalry" && Pair(e, a, b));
            return last != null && last.text.Contains($"over {QuarrelIn(drawn)}.") ? Quarrels[(Array.IndexOf(Quarrels, drawn) + 1) % Quarrels.Length] : drawn;
        }

        /// <summary>A quarrel subject as said in this era: the camp has no well (#61).</summary>
        string QuarrelIn(string q) => State.era == Era.Camp && q == "whose turn it was at the well" ? "whose turn it was to fetch water" : q;

        // World events told in turn (#61): the first wording first, then the others, so a box doesn't read the same sentence
        // every time the ducks come back. {0} = a person or a building, {1} = the era noun. The dice are drawn as before.
        static readonly string[] StormLines =
        {
            "A great storm rattled the glass. The people huddled together in the {0}.",
            "Thunder rolled round the glass all night. Everyone squeezed into the {0} and told stories till it passed.",
            "A storm shook the glass. By morning the {0} smelled of wet dog, and everyone was fine.",
        };
        static readonly string[] RainbowLines =
        {
            "A rainbow arched over the pond. Nobody worked that afternoon.",
            "A double rainbow hung inside the glass. The children argued all afternoon about where it ended.",
            "A rainbow came down right by the pond. Everyone went to look for the end of it and found mud.",
        };
        static readonly string[] FogLines =
        {
            "Fog filled the glass for a week. {0} walked into the same tree four times.",
            "A thick fog sat in the glass for days. {0} said good morning to a bush.",
            "Fog rolled in and stayed. {0} found the way home by following the smell of supper.",
        };
        static readonly string[] DroughtLines =
        {
            "A dry summer: the stream ran low and everyone shared their water.",
            "The pond shrank all summer. The whole {1} learned to wash in one bucket.",
            "A long dry spell. The people carried water up from the pond in a line, singing to keep time.",
        };
        static readonly string[] BumperLines =
        {
            "The best harvest anyone can remember. The granary door wouldn't close.",
            "Another fine harvest: there were turnips in places turnips have no business being.",
            "A harvest so big the {1} ran out of baskets and started using hats.",
        };
        static readonly string[] CampBumperLines =
        {
            "The best gathering anyone can remember. The baskets ran out before the berries did.",
            "The bushes bent double with berries. Everyone had purple fingers for a month.",
        };
        static readonly string[] DuckLines =
        {
            "A family of ducks moved into the pond and took charge of it.",
            "The ducks had ducklings. The pond is theirs now; the people are allowed to visit.",
            "A second family of ducks arrived and fell out with the first. The pond is split down the middle.",
        };
        static readonly string[] SheepLines =
        {
            "A sheep wandered into the tavern and refused to leave. It has a stool now.",
            "The tavern sheep had a lamb. The lamb has a stool too.",
            "The tavern sheep is old now and gets the warm seat by the fire. Nobody argues with it.",
        };
        static readonly string[] ElderLines =
        {
            "{0}, now the eldest, told the founding story to the children all winter.",
            "{0} told the children how the first wanderers found the glass. The story gets longer every winter.",
            "All winter the children sat round {0}, the eldest, asking for the one about the first fire.",
        };
        static readonly string[] ReconcileLines =
        {
            "{0} and {1} made up over a pot of stew. The whole {2} sighed with relief.",
            "{0} and {1} are friends again. Nobody is sure who said sorry first.",
            "{0} and {1} made peace while looking for the same lost goat. They found it together.",
        };
        internal static readonly string[] FestivalLines =
        {
            "{0} threw a harvest festival. There was dancing until dawn.",
            "{0} threw a harvest festival. The pies ran out long before the music did.",
            "{0} threw a harvest festival. Someone danced with a goat, and the goat led.",
        };

        /// <summary>The building a story happens in or on: one with a roof (#61: nobody huddles in a well or climbs a field's
        /// roof). The drawn building if it has one, else the next roofed one in the list. No dice.</summary>
        Building Roofed(Building b)
        {
            if (b == null || HasRoof(b)) return b;
            var list = State.buildings;
            int at = list.IndexOf(b);
            for (int i = 1; i < list.Count; i++)
            {
                var x = list[(at + i) % list.Count];
                if (HasRoof(x)) return x;
            }
            return b;
        }

        bool HasRoof(Building b) => b.type == BuildingType.Hut || b.type == BuildingType.Windmill || b.type == BuildingType.Tavern ||
                                    b.type == BuildingType.Church || b.type == BuildingType.Guildhall || b.type == BuildingType.Workshop || b.type == BuildingType.Shop || (b.type == BuildingType.Lumbermill && State.era > Era.Camp);

        /// <summary>Hook for the Lamp tool (research 10, not in the sim yet): from now on the strange light is the Keeper's lamp.</summary>
        public void KeeperLampUsed()
        {
            if (!State.storyFlags.Contains(FlagLampUsed)) State.storyFlags.Add(FlagLampUsed);
        }

        static readonly string[] FoundThings =
        {
            "a shiny pebble nobody can explain", "a very old boot", "a jar of honey", "a crown made of twigs",
            "a feather as long as an arm", "a song nobody had heard before",
        };
        static readonly string[] Quarrels = { "a goat", "whose turn it was at the well", "the best way to bake bread", "a fence post", "a song lyric" };
        static readonly string[] FriendReasons = { "a long night of storytelling", "sharing the last apple", "fixing a roof together", "a snowball fight" };
        static readonly string[] PlaceNames = { "Glasswick", "Brightmere", "Little Hollow", "Keepersend", "Pebbleford", "Dewmoor", "Lanternfold" };

        void StoryStep(int season, bool winter)
        {
            var s = State;
            var r = s.storyRng;
            long year = s.month / SimConfig.MonthsPerYear;

            // Milestones that always happen (not dice): naming the place, round anniversaries.
            if (!s.storyFlags.Contains(FlagNamed) && s.population >= 12)
            {
                s.storyFlags.Add(FlagNamed);
                s.settlementName = PlaceNames[r.Range(0, PlaceNames.Length)];
                var namer = StoryAdult(r);
                Chronicle("naming", namer != null
                    ? $"{namer.FullName} suggested a name for the settlement: {s.settlementName}. It stuck."
                    : $"The people named their settlement {s.settlementName}.", 0.8f, namer?.id ?? -1);
            }
            if (season == 0 && year > 0 && year % 25 == 0)
                Chronicle("anniversary", $"{Place} celebrated {year} years in the glass with a feast.", 0.65f);
            if (winter && !s.storyFlags.Contains(FlagSnow) && year >= 2 && r.Chance(0.25f))
            {
                s.storyFlags.Add(FlagSnow);
                Chronicle("snow", "Snow fell inside the glass for the first time. The children tried to catch it.", 0.6f);
            }

            if (!r.Chance(Config.storyChancePerMonth)) return;

            var options = new List<(float w, Action run)>();
            var adults = s.notables.Where(n => n.alive && n.age >= 16f).ToList();
            void Add(float w, Action a) => options.Add((w, a));

            // ---- people together ----
            // Couples of an age (#61: a founder of 35 married a girl of 17 he had watched grow up): both 18 to 50, single or
            // widowed, different families, not parent and child, at most 12 years apart.
            var single = adults.Where(n => Alive(n.partnerId) == null && n.age >= MarryAge && n.age <= 50f).ToList();
            var couple = PickPair(r, single, CanCourt);
            if (couple.a != null) Add(1.4f, () => Wedding(couple.a, couple.b, season));
            var mates = PickPair(r, adults, (a, b) => a.friendId < 0 && b.friendId < 0 && a.rivalId != b.id);
            if (mates.a != null) Add(1f, () =>
            {
                mates.a.friendId = mates.b.id; mates.b.friendId = mates.a.id;
                Chronicle("friendship", $"{mates.a.FullName} and {mates.b.FullName} became fast friends after {Pick(r, FriendReasons)}.", 0.5f, mates.a.id, mates.b.id);
            });
            var foes = PickPair(r, adults, (a, b) => a.rivalId < 0 && b.rivalId < 0 && a.friendId != b.id && a.partnerId != b.id);
            if (foes.a != null) Add(0.9f, () =>
            {
                foes.a.rivalId = foes.b.id; foes.b.rivalId = foes.a.id;
                Chronicle("rivalry", $"{foes.a.FullName} and {foes.b.FullName} fell out over {QuarrelIn(QuarrelAgain(Pick(r, Quarrels), foes.a.id, foes.b.id))}. They no longer share a bench.", 0.58f, foes.a.id, foes.b.id);
            });
            var feuding = adults.FirstOrDefault(n => n.rivalId >= 0 && Alive(n.rivalId) != null);
            if (feuding != null) Add(0.5f, () =>
            {
                var other = Alive(feuding.rivalId);
                feuding.rivalId = other.rivalId = -1;
                Chronicle("reconcile", string.Format(Nth(ReconcileLines, Told("reconcile", feuding.id) + feuding.id), feuding.FullName, other.FullName, EraNoun(s.era)), 0.55f, feuding.id, other.id);
            });
            var masters = adults.Where(n => n.age >= Config.masterAge).ToList();
            var youngs = s.notables.Where(n => n.alive && n.age >= 8f && n.age < 20f && n.mentorId < 0).ToList();
            if (masters.Count > 0 && youngs.Count > 0) Add(0.8f, () =>
            {
                var m = Pick(r, masters); var y = Pick(r, youngs);
                y.mentorId = m.id;
                Chronicle("apprentice", $"{m.FullName} took young {y.FullName} as an apprentice.", 0.5f, y.id, m.id);
            });
            var eldest = adults.OrderByDescending(n => n.age).FirstOrDefault();
            if (eldest != null && eldest.age >= Config.eldestStoryAge && winter) Add(0.5f, () =>
                Chronicle("elder", string.Format(Nth(ElderLines, Told("elder", eldest.id) + eldest.id), eldest.FullName), 0.55f, eldest.id));

            // ---- one person, by trait (each person tells each kind of story at most once in 15 years) ----
            foreach (var n in adults)
            {
                var who = n;
                if (!TraitStories.TryGetValue(n.trait, out var t) || !Fresh(t.kind, who.id, 15)) continue;
                Add(0.5f, () =>
                {
                    var b = Roofed(RandomBuilding(r));
                    _ = Pick(r, t.lines); // drawn as before, so the story stream never moves; the lines are told in turn (#61)
                    string line = t.lines[(Told(t.kind, who.id) + who.id) % t.lines.Length];
                    var e = Chronicle(t.kind, string.Format(EraLine(line), who.FullName, Label(b), EraNoun(s.era), KeeperWord), t.salience, who.id);
                    if (t.lines.Any(l => l.Contains("{1}"))) e.buildingId = b?.id ?? -1;
                });
            }
            if (adults.Count > 0 && Fresh("lost", -1, 6)) Add(0.6f, () =>
            {
                var n = Pick(r, adults);
                string thing = FoundNext(Pick(r, FoundThings), n.id); // drawn as before
                string sandal = SandalLossLine(n); // the box's first "lost" story is the Left Sandal's (SimCore.Relics.cs, #74)
                var lost = Chronicle("lost", sandal ?? $"{n.FullName} got lost in the hills for three days and came back with {thing}.", 0.6f, n.id);
                if (sandal != null) SandalLost(n, lost);
            });

            // ---- the world ----
            if (Fresh("storm", -1, 6)) Add(0.6f, () =>
            {
                var b = Roofed(RandomBuilding(r));
                s.mood = Math.Max(0f, s.mood - 0.05f);
                Chronicle("storm", b != null ? string.Format(Nth(StormLines, Told("storm")), Label(b)) : "A great storm rattled the glass.", 0.5f).buildingId = b?.id ?? -1;
            });
            if (!winter && Fresh("rainbow", -1, 8)) Add(0.5f, () => { s.mood = Math.Min(1f, s.mood + 0.05f); Chronicle("rainbow", Nth(RainbowLines, Told("rainbow")), 0.42f); });
            if (Fresh("fog", -1, 8)) Add(0.4f, () =>
            {
                var n = adults.Count > 0 ? Pick(r, adults) : null;
                Chronicle("fog", n != null ? string.Format(Nth(FogLines, n == null ? 0 : Told("fog", n.id) + n.id), n.FullName) : "Fog filled the glass for a week.", 0.42f, n?.id ?? -1);
            });
            if (season >= 5 && season <= 7 && Fresh("drought", -1, 6)) Add(0.4f, () => { s.food *= 0.85f; Chronicle("drought", string.Format(Nth(DroughtLines, Told("drought")), "", EraNoun(s.era)), 0.48f); });
            if (season >= 8 && season <= 9 && Fresh("bumper", -1, 6)) Add(0.5f, () => { s.food = Math.Min(FoodCap, s.food * 1.2f + 5f); Chronicle("bumper", string.Format(Nth(s.era == Era.Camp ? CampBumperLines : BumperLines, Told("bumper")), "", EraNoun(s.era)), 0.55f); });
            if (Fresh("strange_light", -1, 20)) Add(0.35f, () => Chronicle("strange_light", string.Format(Nth(!CardsOn || s.storyFlags.Contains(FlagLampUsed) ? StrangeLightLamp : StrangeLightEarly, Told("strange_light")), KeeperWord), 0.5f));
            if (Fresh("ducks", -1, 30)) Add(0.35f, () => Chronicle("ducks", Nth(DuckLines, Told("ducks")), 0.45f));
            if (Count(BuildingType.Tavern) > 0 && Fresh("sheep", -1, 30)) Add(0.4f, () =>
            {
                var b = State.buildings.First(x => x.type == BuildingType.Tavern);
                Chronicle("sheep", Nth(SheepLines, Told("sheep")), 0.5f).buildingId = b.id;
            });

            float total = options.Sum(o => o.w);
            float pick = r.Value() * total;
            foreach (var (w, run) in options)
            {
                if ((pick -= w) > 0) continue;
                run();
                return;
            }
        }

        /// <summary>No entry of this kind (about this person, if given) in the last <paramref name="years"/> years.</summary>
        bool Fresh(string kind, int notableId, int years)
        {
            long since = State.month - years * SimConfig.MonthsPerYear;
            for (int i = State.chronicle.Count - 1; i >= 0; i--)
            {
                var e = State.chronicle[i];
                if (e.month < since) return true;
                if (e.kind == kind && (notableId < 0 || e.notableId == notableId)) return false;
            }
            return true;
        }

        public const float MarryAge = 18f;

        void Wedding(Notable a, Notable b, int season)
        {
            a.partnerId = b.id; b.partnerId = a.id;
            bool first = !State.storyFlags.Contains(FlagWedding);
            if (first) State.storyFlags.Add(FlagWedding);
            string when = season <= 1 || season == 11 ? "by the winter fire" : season <= 4 ? "among the spring blossoms" : season <= 7 ? "under the summer sky" : "at the harvest dance";
            Chronicle("wedding", first
                ? $"The first wedding in the glass: {a.FullName} married {b.FullName} {when}."
                : $"{a.FullName} married {b.FullName} {when}.", first ? 0.8f : 0.7f, a.id, b.id);
        }

        /// <summary>Record this game year (the time-lapse history). Also called at founding, so year 0 exists.</summary>
        public void RecordYear()
        {
            var s = State;
            int year = (int)(s.month / SimConfig.MonthsPerYear);
            if (s.years.Count > 0 && s.years[^1].year == year) return;
            s.years.Add(new YearSnapshot { year = year, population = s.population, buildings = s.buildings.Count, notables = s.notables.Count(n => n.alive), mood = s.mood, era = s.era });
        }

        /// <summary>Chronicle line linking two people (wedding, feud...).</summary>
        ChronicleEntry Chronicle(string kind, string text, float salience, int notableId, int otherId) => Record(kind, text, salience, notableId, otherId);

        string Place => string.IsNullOrEmpty(State.settlementName) ? "The settlement" : State.settlementName;

        Notable Alive(int id) => id < 0 ? null : State.notables.FirstOrDefault(n => n.id == id && n.alive);

        Notable StoryAdult(SimRandom r)
        {
            var adults = State.notables.Where(n => n.alive && n.age >= 16f).ToList();
            return adults.Count == 0 ? null : Pick(r, adults);
        }

        Building RandomBuilding(SimRandom r) => State.buildings.Count == 0 ? null : Pick(r, State.buildings);

        /// <summary>A building as the people say it in this era (#61): the camp's farm is its gathering grounds and its lumber
        /// mill a woodpile (decisions 2026-10-29, #23); a hut is a cottage in the village and a house in the town.</summary>
        string Label(Building b) => b?.type switch
        {
            null => "camp",
            BuildingType.Camp => "camp fire",
            BuildingType.Hut => State.era == Era.Camp ? "hut" : State.era == Era.Village ? "cottage" : "house",
            BuildingType.Farm => State.era == Era.Camp ? "gathering grounds" : "farm",
            BuildingType.Lumbermill => State.era == Era.Camp ? "woodpile" : "lumber mill",
            BuildingType.Church => "chapel",
            BuildingType.Shop => "shop",
            _ => b.type.ToLowerInvariant(),
        };

        static T Pick<T>(SimRandom r, IReadOnlyList<T> list) => list[r.Range(0, list.Count)];

        static (Notable a, Notable b) PickPair(SimRandom r, List<Notable> pool, Func<Notable, Notable, bool> ok)
        {
            if (pool.Count < 2) return (null, null);
            for (int tries = 0; tries < 6; tries++)
            {
                var a = Pick(r, pool); var b = Pick(r, pool);
                if (a != b && ok(a, b)) return (a, b);
            }
            return (null, null);
        }

        // ---------- debug / lab cheats ----------
        /// <summary>Debug: one story event now (dice still from the story stream).</summary>
        public void ForceStory()
        {
            float chance = Config.storyChancePerMonth;
            Config.storyChancePerMonth = 1f;
            int season = (int)(State.month % SimConfig.MonthsPerYear);
            try { StoryStep(season, season == 11 || season == 0 || season == 1); }
            finally { Config.storyChancePerMonth = chance; }
        }

        /// <summary>Debug: the oldest living notable dies (of old age, as always).</summary>
        public Notable DebugKillOldest()
        {
            var n = State.notables.Where(x => x.alive).OrderByDescending(x => x.age).FirstOrDefault();
            if (n == null) return null;
            n.alive = false;
            n.diedMonth = State.month;
            Chronicle("death", $"{n.FullName}, {n.trait}, died of old age at {Math.Floor(n.age)}.", 0.85f, n.id);
            return n;
        }

        /// <summary>Debug: a child is born to a living adult notable.</summary>
        public Notable DebugBirth()
        {
            var parent = State.notables.Where(x => x.alive && x.age >= 18f).OrderBy(x => x.id).FirstOrDefault();
            if (parent == null) return null;
            var child = AddNotable(0f, parent.id, parent.family);
            Chronicle("birth", $"{child.FullName} was born to {parent.FullName}.", 0.6f, child.id);
            return child;
        }

        // ---------- queries for views ----------
        public Notable FindNotable(int id) => State.notables.FirstOrDefault(n => n.id == id);

        /// <summary>Chronicle entries about this person (as the subject or the other party), newest first.</summary>
        public IEnumerable<ChronicleEntry> StoriesOf(int notableId) =>
            State.chronicle.Where(e => e.notableId == notableId || e.otherNotableId == notableId).Reverse();

        public IEnumerable<Notable> ChildrenOf(int notableId) => State.notables.Where(n => n.parentId == notableId);
    }
}
