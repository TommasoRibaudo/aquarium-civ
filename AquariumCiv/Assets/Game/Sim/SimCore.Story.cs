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

        // #715 (research 21, user 2026-11-14 "drop them"): the trait stories, the weather (storm, rainbow, fog, drought, bumper),
        // lost in the hills, the strange light, the tavern sheep and the old ducks are gone from the stream: none of them was seen
        // in the box or remembered. Old saves keep their entries. New events that are seen replace them (research 21 §7).

        /// <summary>#715: the story kinds the stream no longer tells. An old save's entries of these stay in History, but never title an era.</summary>
        public static readonly string[] DroppedStoryKinds =
        {
            "storm", "rainbow", "fog", "drought", "bumper", "strange_light", "sheep", "ducks",
            "invention", "song", "prank", "giant_turnip", "brave", "stubborn", "worrier", "gossip", "grump", "dreamer", "lazy", "kind", "loud",
        };

        /// <summary>The Lamp's flag: its story is gone (#715, #718), the flag stays so an old save's storyFlags still read.</summary>
        public const string FlagLampUsed = "lamp-used";

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

        /// <summary>The drawn quarrel, unless these two fell out over just that last time (#61: the same pair fell out over
        /// the same song lyric twice). The draw still decides it; no dice.</summary>
        string QuarrelAgain(string drawn, int a, int b) => QuarrelGated(QuarrelAgainRaw(drawn, a, b));

        /// <summary>A quarrel can only be over what is in the box (#86): no well, no quarrel at the well; no bakery
        /// to bake in, none about baking. Same draw, a plain fallback.</summary>
        public string QuarrelGated(string q)
        {
            if (q == Quarrels[1] && Count(BuildingType.Well) == 0) return Quarrels[3];
            if (q == Quarrels[2] && Count(BuildingType.Bakery) == 0) return Quarrels[0];
            return q;
        }

        string QuarrelAgainRaw(string drawn, int a, int b)
        {
            var last = State.chronicle.LastOrDefault(e => e.kind == "rivalry" && Pair(e, a, b));
            // #715: also the last time they fell out in this order (as told, a first), and the flag when the History prune dropped the line
            var sameOrder = State.chronicle.LastOrDefault(e => e.kind == "rivalry" && e.notableId == a && e.otherNotableId == b);
            string lastText = last == null ? LastQuarrel(a, b) : last.text + " | " + (sameOrder?.text ?? "");
            if (lastText == null) return drawn;
            // #715: compare what would be said (gated, in this era's words), and step on until it differs from last time
            int at = Array.IndexOf(Quarrels, drawn);
            for (int i = 0; i < Quarrels.Length; i++)
            {
                string q = Quarrels[(at + i) % Quarrels.Length];
                if (!lastText.Contains($"over {QuarrelIn(QuarrelGated(q))}.")) return q;
            }
            return drawn;
        }

        static string QuarrelKey(int a, int b) => $"quarrel:{Math.Min(a, b)}-{Math.Max(a, b)}=";

        /// <summary>#715: the last subject this pair fell out over, kept in storyFlags (text only, no dice).</summary>
        void RememberQuarrel(int a, int b, string over)
        {
            string key = QuarrelKey(a, b);
            State.storyFlags.RemoveAll(f => f.StartsWith(key));
            State.storyFlags.Add(key + $"over {over}.");
        }

        string LastQuarrel(int a, int b)
        {
            string key = QuarrelKey(a, b);
            var f = State.storyFlags.LastOrDefault(x => x.StartsWith(key));
            return f?.Substring(key.Length);
        }

        /// <summary>A quarrel subject as said in this era: the camp has no well (#61).</summary>
        string QuarrelIn(string q) => State.era != Era.Camp ? q
            : q == "whose turn it was at the well" ? "whose turn it was to fetch water"
            : q == "where the new path should go" ? "a tent peg" : q; // #715: the camp's own words, at the source (no LandWords swap)

        // Told in turn (#61): the first wording first, then the others, so a box doesn't read the same sentence twice running.
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
            "{0} and {1} made peace while looking for the same lost basket. They found it together.", // #715 [est]: no goat
        };
        /// <summary>P8: a living-land camp has no harvest to celebrate yet; the feast is at the fire.</summary>
        public static readonly string[] CampFestivalLines =
        {
            "{0} called everyone to the fire for a feast. There was dancing until dawn.",
            "{0} stoked the fire high for a feast. The food ran out long before the singing did.",
            "{0} gathered the tribe round the fire. Someone danced with a ladle, and the ladle led.", // #715 [est]
        };
        public static readonly string[] FestivalLines =
        {
            "{0} threw a harvest festival. There was dancing until dawn.",
            "{0} threw a harvest festival. The bread ran out long before the music did.", // #715 [est]: no pies
            "{0} threw a harvest festival. Someone danced with a ladle, and the ladle led.", // #715 [est]
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
                                    b.type == BuildingType.Church || b.type == BuildingType.Guildhall || b.type == BuildingType.Workshop || b.type == BuildingType.Shop || b.type == BuildingType.Bakery || (b.type == BuildingType.Lumbermill && State.era > Era.Camp);

        /// <summary>Hook for the Lamp tool (research 10, not in the sim yet): from now on the strange light is the Keeper's lamp.</summary>
        public void KeeperLampUsed()
        {
            if (!State.storyFlags.Contains(FlagLampUsed)) State.storyFlags.Add(FlagLampUsed);
        }

        // #715 [est]: no goat and no fence post in a box that has neither (research 21): the stew, and where the path goes.
        /// <summary>The snow line's second sentence, by who is there to catch it: two kids, one kid, or only grown-ups (no dice).</summary>
        string SnowKidClause()
        {
            int kids = KidCount;
            if (kids >= 2) return "The children tried to catch it.";
            if (kids == 1) return State.notables.Any(n => n.alive && n.age < 3f) ? "The baby stared at it." : "The little one tried to catch it.";
            return "Everyone stood with their hands out to catch it.";
        }

        static readonly string[] Quarrels = { "the last of the stew", "whose turn it was at the well", "the best way to bake bread", "where the new path should go", "a song lyric" };
        static readonly string[] FriendReasons = { "a long night of storytelling", "sharing the last apple", "fixing a roof together", "a snowball fight" };
        /// <summary>#720: the living land's camp (tents, no apple trees): the same reasons, in its own words (same count: the same dice).</summary>
        static readonly string[] FriendReasonsCamp = { "a long night of storytelling", "sharing the last of the basket", "fixing a tent together", "a snowball fight" };
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
                Chronicle("snow", "Snow fell inside the glass for the first time. " + SnowKidClause(), 0.6f);
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
                Chronicle("friendship", $"{mates.a.FullName} and {mates.b.FullName} became fast friends after {Pick(r, EcologyOn && State.era == Era.Camp ? FriendReasonsCamp : FriendReasons)}.", 0.5f, mates.a.id, mates.b.id);
            });
            var foes = PickPair(r, adults, (a, b) => a.rivalId < 0 && b.rivalId < 0 && a.friendId != b.id && a.partnerId != b.id);
            if (foes.a != null) Add(0.9f, () =>
            {
                foes.a.rivalId = foes.b.id; foes.b.rivalId = foes.a.id;
                string over = QuarrelIn(QuarrelAgain(Pick(r, Quarrels), foes.a.id, foes.b.id));
                RememberQuarrel(foes.a.id, foes.b.id, over);
                Chronicle("rivalry", $"{foes.a.FullName} and {foes.b.FullName} fell out over {over}. They no longer share a fire.", 0.58f, foes.a.id, foes.b.id);
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

            // ---- the first loss: the Left Sandal (SimCore.Relics.cs, #74), found 20 years later. #715: no other "lost in the hills".
            if (adults.Count > 0 && SandalLossDue && Fresh("lost", -1, 6)) Add(0.6f, () =>
            {
                var n = Pick(r, adults);
                string sandal = SandalLossLine(n);
                if (sandal == null) return;
                var e = Chronicle("lost", sandal, 0.6f, n.id);
                if (RelicsOn) SandalLost(n, e); // the relic waits to be found 20 years later; with the relics off it is only the story
            });

            // #715: a quiet month, in place of the dropped weather and trait stories (about their old share), so the kept stories
            // don't fill the gap: fewer stories, not the same number of quarrels (research 21 §7.7). [est] weight
            Add(QuietStoryWeight + QuietPerAdult * adults.Count, () => { });

            float total = options.Sum(o => o.w);
            float pick = r.Value() * total;
            foreach (var (w, run) in options)
            {
                if ((pick -= w) > 0) continue;
                run();
                return;
            }
        }

        /// <summary>#715: the weight of a month with no story (the weather's and the trait stories' old share) [est].</summary>
        public const float QuietStoryWeight = 2.5f, QuietPerAdult = 0.25f;

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
            MarksAfterWedding(a, b); // #722 (1.2): with the Keeper's tree grown, the couple's marks on it
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
            int season = CalendarMonth;
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

        /// <summary>Debug (#708/#709: the kids' beats need kids; a new living land starts with five grown-ups): <paramref name="n"/> children of
        /// <paramref name="age"/> years join a living adult's family. How many came.</summary>
        public int DebugAddKids(int n = 2, float age = 7f)
        {
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                var parent = State.notables.Where(x => x.alive && x.age >= 18f).OrderBy(x => x.id).Skip(i).FirstOrDefault()
                             ?? State.notables.Where(x => x.alive && x.age >= 18f).OrderBy(x => x.id).FirstOrDefault();
                if (parent == null) break;
                var child = AddNotable(age + i * 0.7f, parent.id, parent.family);
                Chronicle("birth", $"{child.FullName} came to live with {parent.FullName}.", 0.2f, child.id);
                made++;
            }
            return made;
        }

        // ---------- queries for views ----------
        public Notable FindNotable(int id) => State.notables.FirstOrDefault(n => n.id == id);

        /// <summary>Chronicle entries about this person (as the subject or the other party), newest first.</summary>
        public IEnumerable<ChronicleEntry> StoriesOf(int notableId) =>
            State.chronicle.Where(e => e.notableId == notableId || e.otherNotableId == notableId).Reverse();

        public IEnumerable<Notable> ChildrenOf(int notableId) => State.notables.Where(n => n.parentId == notableId);
    }
}
