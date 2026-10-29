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

        /// <summary>Trait incidents: {0} = the person, {1} = a building. Two lines each so a long life doesn't repeat.</summary>
        static readonly Dictionary<string, (string kind, float salience, string[] lines)> TraitStories = new()
        {
            ["the tinkerer"] = ("invention", 0.62f, new[] { "{0} invented a bucket that pours itself.", "{0} built a wheelbarrow with five wheels. It mostly works." }),
            ["the clever"] = ("invention", 0.62f, new[] { "{0} invented a mirror for signalling the Keeper.", "{0} worked out how to tell the time from the glass's shadow." }),
            ["the singer"] = ("song", 0.5f, new[] { "{0} wrote a song about the Keeper's giant thumb. Everyone hums it now.", "{0} sang at the {1} until the rafters shook." }),
            ["the joker"] = ("prank", 0.52f, new[] { "{0} painted the {1} roof bright pink overnight.", "{0} hid every spoon in the village inside the {1}." }),
            ["the gardener"] = ("giant_turnip", 0.55f, new[] { "{0} grew a turnip the size of a sheep. It fed the street for a week.", "{0} planted flowers around the {1}. The bees approve." }),
            ["the brave"] = ("brave", 0.5f, new[] { "{0} chased a fox away from the hens with a frying pan.", "{0} climbed the {1} roof in a storm to save a kitten." }),
            ["the stubborn"] = ("stubborn", 0.48f, new[] { "{0} refused to move for the new path. The path now goes around them.", "{0} insisted the {1} was built backwards. It wasn't." }),
            ["the worrier"] = ("worrier", 0.45f, new[] { "{0} built a little fence around the well, just in case.", "{0} counted the people of the village twice a day for a month." }),
            ["the gossip"] = ("gossip", 0.5f, new[] { "{0} spread a rumour that the Keeper has a cat. It caused quite a stir.", "{0} knew about the wedding before the couple did." }),
            ["the grump"] = ("grump", 0.45f, new[] { "{0} complained about the noise from the {1} for a whole season.", "{0} said the harvest festival was too loud, then danced all night." }),
            ["the dreamer"] = ("dreamer", 0.55f, new[] { "{0} dreamt of a city beyond the glass and drew a map of it.", "{0} spent a summer trying to talk to the clouds." }),
            ["the lazy"] = ("lazy", 0.45f, new[] { "{0} napped through the entire harvest. Nobody had the heart to wake them.", "{0} invented a hammock. It was their only invention." }),
            ["the kind"] = ("kind", 0.5f, new[] { "{0} took in a stray goat. It follows them everywhere now.", "{0} baked bread for every house after the storm." }),
            ["the loud"] = ("loud", 0.45f, new[] { "{0} sneezed so hard the weathervane spun round.", "{0} called the goats home from the far side of the glass." }),
        };

        static readonly string[] FoundThings =
        {
            "a shiny pebble nobody can explain", "a very old boot", "a jar of honey", "a crown made of twigs",
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
            var single = adults.Where(n => n.partnerId < 0 && n.age <= 50f).ToList();
            var couple = PickPair(r, single, (a, b) => a.family != b.family);
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
                Chronicle("rivalry", $"{foes.a.FullName} and {foes.b.FullName} fell out over {Pick(r, Quarrels)}. They no longer share a bench.", 0.58f, foes.a.id, foes.b.id);
            });
            var feuding = adults.FirstOrDefault(n => n.rivalId >= 0 && Alive(n.rivalId) != null);
            if (feuding != null) Add(0.5f, () =>
            {
                var other = Alive(feuding.rivalId);
                feuding.rivalId = other.rivalId = -1;
                Chronicle("reconcile", $"{feuding.FullName} and {other.FullName} made up over a pot of stew. The whole street sighed with relief.", 0.55f, feuding.id, other.id);
            });
            var elders = adults.Where(n => n.age >= Config.elderAge).ToList();
            var youngs = s.notables.Where(n => n.alive && n.age >= 8f && n.age < 20f && n.mentorId < 0).ToList();
            if (elders.Count > 0 && youngs.Count > 0) Add(0.8f, () =>
            {
                var m = Pick(r, elders); var y = Pick(r, youngs);
                y.mentorId = m.id;
                Chronicle("apprentice", $"{m.FullName} took young {y.FullName} as an apprentice.", 0.5f, y.id, m.id);
            });
            var eldest = adults.OrderByDescending(n => n.age).FirstOrDefault();
            if (eldest != null && eldest.age >= Config.eldestStoryAge && winter) Add(0.5f, () =>
                Chronicle("elder", $"{eldest.FullName}, now the eldest, told the founding story to the children all winter.", 0.55f, eldest.id));

            // ---- one person, by trait (each person tells each kind of story at most once in 15 years) ----
            foreach (var n in adults)
            {
                var who = n;
                if (!TraitStories.TryGetValue(n.trait, out var t) || !Fresh(t.kind, who.id, 15)) continue;
                Add(0.5f, () =>
                {
                    var b = RandomBuilding(r);
                    var e = Chronicle(t.kind, string.Format(Pick(r, t.lines), who.FullName, Label(b)), t.salience, who.id);
                    if (t.lines.Any(l => l.Contains("{1}"))) e.buildingId = b?.id ?? -1;
                });
            }
            if (adults.Count > 0 && Fresh("lost", -1, 6)) Add(0.6f, () =>
            {
                var n = Pick(r, adults);
                Chronicle("lost", $"{n.FullName} got lost in the hills for three days and came back with {Pick(r, FoundThings)}.", 0.6f, n.id);
            });

            // ---- the world ----
            if (Fresh("storm", -1, 6)) Add(0.6f, () =>
            {
                var b = RandomBuilding(r);
                s.mood = Math.Max(0f, s.mood - 0.05f);
                Chronicle("storm", b != null ? $"A great storm rattled the glass. The people huddled together in the {Label(b)}." : "A great storm rattled the glass.", 0.5f).buildingId = b?.id ?? -1;
            });
            if (!winter && Fresh("rainbow", -1, 8)) Add(0.5f, () => { s.mood = Math.Min(1f, s.mood + 0.05f); Chronicle("rainbow", "A rainbow arched over the pond. Nobody worked that afternoon.", 0.42f); });
            if (Fresh("fog", -1, 8)) Add(0.4f, () =>
            {
                var n = adults.Count > 0 ? Pick(r, adults) : null;
                Chronicle("fog", n != null ? $"Fog filled the glass for a week. {n.FullName} walked into the same tree four times." : "Fog filled the glass for a week.", 0.42f, n?.id ?? -1);
            });
            if (season >= 5 && season <= 7 && Fresh("drought", -1, 6)) Add(0.4f, () => { s.food *= 0.85f; Chronicle("drought", "A dry summer: the stream ran low and everyone shared their water.", 0.48f); });
            if (season >= 8 && season <= 9 && Fresh("bumper", -1, 6)) Add(0.5f, () => { s.food = Math.Min(FoodCap, s.food * 1.2f + 5f); Chronicle("bumper", "The best harvest anyone can remember. The granary door wouldn't close.", 0.55f); });
            if (Fresh("strange_light", -1, 20)) Add(0.35f, () => Chronicle("strange_light", "A strange warm light crossed the sky. Some say it was the Keeper's lamp.", 0.5f));
            if (Fresh("ducks", -1, 30)) Add(0.35f, () => Chronicle("ducks", "A family of ducks moved into the pond and took charge of it.", 0.45f));
            if (Count(BuildingType.Tavern) > 0 && Fresh("sheep", -1, 30)) Add(0.4f, () =>
            {
                var b = State.buildings.First(x => x.type == BuildingType.Tavern);
                Chronicle("sheep", "A sheep wandered into the tavern and refused to leave. It has a stool now.", 0.5f).buildingId = b.id;
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
        ChronicleEntry Chronicle(string kind, string text, float salience, int notableId, int otherId)
        {
            var e = Chronicle(kind, text, salience, notableId);
            e.otherNotableId = otherId;
            return e;
        }

        string Place => string.IsNullOrEmpty(State.settlementName) ? "The settlement" : State.settlementName;

        Notable Alive(int id) => id < 0 ? null : State.notables.FirstOrDefault(n => n.id == id && n.alive);

        Notable StoryAdult(SimRandom r)
        {
            var adults = State.notables.Where(n => n.alive && n.age >= 16f).ToList();
            return adults.Count == 0 ? null : Pick(r, adults);
        }

        Building RandomBuilding(SimRandom r) => State.buildings.Count == 0 ? null : Pick(r, State.buildings);

        static string Label(Building b) => b == null ? "camp" : b.type == BuildingType.Lumbermill ? "lumber mill" : b.type.ToLowerInvariant();

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
