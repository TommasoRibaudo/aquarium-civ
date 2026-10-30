using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// How a term in office feels (#28): 0..1 per aspect, 0.5 = an ordinary term. The view and the activity planner read
    /// it (life-in-the-box §10 rule 9: the grump sends people home early, the singer sings at the fire, the lazy naps).
    /// A readout, never saved and never read by the economy.
    /// </summary>
    [Serializable]
    public class TermFlavor
    {
        public float feasts;      // gatherings with food and dancing at the seat (view-and-story only, no mood)
        public float music;       // singing at the fire or green in the evening
        public float tidiness;    // sweepers busier, the green swept every morning
        public float quiet;       // people go home early, lamps out early
        public float quarrels;    // rows, rivals argue more often
        public float kindness;    // shared meals, visiting the old and sick, making up
        public float invention;   // odd devices by the seat
        public float adventure;   // expeditions and hunts; kids play chase near the seat
        public float caution;     // fences, extra lanterns, home before dark
        public float idleness;    // naps on the green, long lunches (output unchanged)
        public float gardening;   // flowers round the seat, the green planted
        public float mirth;       // jokes, games, speeches ending in laughter
        public float gathering;   // the crowd gathers to listen (announcements, news)
        public float wonder;      // grand plans, staring at the sky, walks up the Keeper's hill

        public static TermFlavor Neutral() => new TermFlavor().Fill(0.5f);

        TermFlavor Fill(float v)
        {
            feasts = music = tidiness = quiet = quarrels = kindness = invention = adventure = caution = idleness = gardening =
                mirth = gathering = wonder = v;
            return this;
        }

        /// <summary>this += d * w.</summary>
        internal void Add(TermFlavor d, float w)
        {
            feasts += d.feasts * w; music += d.music * w; tidiness += d.tidiness * w; quiet += d.quiet * w;
            quarrels += d.quarrels * w; kindness += d.kindness * w; invention += d.invention * w; adventure += d.adventure * w;
            caution += d.caution * w; idleness += d.idleness * w; gardening += d.gardening * w; mirth += d.mirth * w;
            gathering += d.gathering * w; wonder += d.wonder * w;
        }

        internal TermFlavor Clamp()
        {
            static float C(float v) => Math.Clamp(v, 0f, 1f);
            feasts = C(feasts); music = C(music); tidiness = C(tidiness); quiet = C(quiet); quarrels = C(quarrels);
            kindness = C(kindness); invention = C(invention); adventure = C(adventure); caution = C(caution);
            idleness = C(idleness); gardening = C(gardening); mirth = C(mirth); gathering = C(gathering); wonder = C(wonder);
            return this;
        }
    }

    /// <summary>
    /// The role-holder's trait colours their term in office (#28, notable-roles.md §4; decisions.md 2026-10-29: story and
    /// behaviour only, never the economy). Three things:
    /// <list type="bullet">
    /// <item>Term lines (kinds <c>term_*</c>): each month a role-holder may add a line in their trait's style (a feast, a
    /// song, a swept green, a row at the meeting). The leader twice as often as the other offices.</item>
    /// <item>Codas: a few existing story lines get a clause about the holder (the worrier counts everyone in before the
    /// storm, the brave leads the search for the lost).</item>
    /// <item><see cref="TermFlavor"/>: a 0..1 readout for the view and the activity planner, and a teaser line for the
    /// evening talk.</item>
    /// </list>
    /// Why the economy can't move: no dice stream is drawn (rolls come from <see cref="RoleRoll"/>, a hash of the seed, the
    /// people and the month), no notable or story link is changed, and term lines take their ids from their own counter
    /// (<see cref="SimState.termLineNextId"/>), so the ids of buildings, notables and trash (which jobs, roles and the
    /// crowd hash read) are the same with or without the flavour. Story events that touch the economy (storms, droughts,
    /// bumper harvests, the harvest festival) are never added, removed or re-weighted; at most their text gets a coda.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Debug switch: false turns the whole flavour off (term lines, codas, the readout and the teaser). Not
        /// saved. The tests run the box both ways to show the economy is the same.</summary>
        public bool TermFlavourOn { get; set; } = true;

        const float LeaderTermChance = 0.025f;  // per month: about one line every 3 years from the leader [est]
        const float OfficeTermChance = 0.012f;  // per month for every other office [est]
        const int TermFreshYears = 4;           // the same holder tells the same kind of line at most this often [est]
        const float TermSalience = 0.5f;        // notable-roles §5 term_colour
        public const int TermLineIdBase = 1_000_000_000; // term lines' own ids, far above the shared counter

        /// <summary>A term line: {0} = the holder, {1} = their role, {2} = the seat ("camp fire", "green"), {3} and {4} =
        /// two other adult notables, {5} = the era noun, {6} = a quarrel subject, {7} = late evening ("the fire burned low" in
        /// the camp, which has no lamps).</summary>
        sealed class TermStory
        {
            public string kind;
            public float weight;
            public string[] lines;
            public TermStory(string kind, float weight, params string[] lines) { this.kind = kind; this.weight = weight; this.lines = lines; }
            public int Others => lines.Max(l => l.Contains("{4}") ? 2 : l.Contains("{3}") ? 1 : 0);
            public bool couple;             // {3} and {4} are a couple-to-be: two single, unrelated adults of an age (#61)
            public TermStory Couple() { couple = true; return this; }
        }

        sealed class TraitTerm
        {
            public TermFlavor flavor;       // deltas from 0.5
            public TermStory[] stories;
            public string teaser;           // present tense, for the evening talk
        }

        static readonly Dictionary<string, TraitTerm> TraitTerms = new()
        {
            ["the singer"] = new TraitTerm
            {
                flavor = new TermFlavor { music = 0.45f, feasts = 0.3f, mirth = 0.15f, quiet = -0.2f },
                stories = new[]
                {
                    new TermStory("term_song", 2f, "With {0} as {1} there was singing at the {2} every night. The owls moved out.",
                        "{0}, the {5}'s {1}, taught everyone a new song. It has eleven verses and no ending."),
                    new TermStory("term_feast", 1.2f, "{0}, the {5}'s {1}, called a feast at the {2} for no reason but a fine evening. Nobody minded.",
                        "{0} the {1} sang {3} and {4} through supper at the {2}, and then everyone else too."),
                },
                teaser = "{0} has been humming a new song at the {2} all week.",
            },
            ["the joker"] = new TraitTerm
            {
                flavor = new TermFlavor { mirth = 0.45f, feasts = 0.25f, quiet = -0.15f, tidiness = -0.1f },
                stories = new[]
                {
                    new TermStory("term_prank", 2f, "{0}, the {5}'s {1}, ended a speech at the {2} with a joke about the goats. Even the goats laughed.",
                        "On the orders of {0}, the first day of spring is now Hat Day. {3} wore three."),
                    new TermStory("term_feast", 1f, "{0} the {1} held a feast day with games at the {2}. {3} won the sack race by falling over the line.",
                        "{0}, the {5}'s {1}, declared a day of games at the {2}. {3} is still champion of the egg-and-spoon."),
                },
                teaser = "{0} is planning something for Hat Day. Nobody knows what.",
            },
            ["the grump"] = new TraitTerm
            {
                flavor = new TermFlavor { tidiness = 0.45f, quiet = 0.45f, feasts = -0.3f, mirth = -0.3f, music = -0.2f, quarrels = 0.1f },
                stories = new[]
                {
                    new TermStory("term_tidy", 2f, "Under {1} {0}, someone sweeps the {2} every morning and nobody laughs after dark.",
                        "{0}, the {5}'s {1}, made a rule: lamps out early. The {5} has never slept so well, or so grumpily."),
                    new TermStory("term_complaint", 1.2f, "{0} the {1} complained about the noise from {3}'s house for a whole season.",
                        "{0}, the {5}'s {1}, sent {3} home from the {2} for whistling."),
                    new TermStory("term_row", 0.6f, "{0} the {1} and {3} argued about {6} at the {2} until {7}.",
                        "{0}, the {5}'s {1}, told {3} exactly what they thought about {6}. It took most of an afternoon."),
                },
                teaser = "{0} has been glaring at the untidy end of the {5}.",
            },
            ["the stubborn"] = new TraitTerm
            {
                flavor = new TermFlavor { gathering = 0.2f, quarrels = 0.25f, mirth = -0.1f },
                stories = new[]
                {
                    new TermStory("term_meeting", 2f, "{0} the {1} kept the meeting at the {2} going until everyone agreed with them. It took two days.",
                        "{0}, the {5}'s {1}, refused to move the woodpile. The path now goes around it."),
                    new TermStory("term_row", 1f, "{0} the {1} and {3} argued about {6} at the {2} until {7}.",
                        "{3} and {4} shouted each other hoarse over {6} at the meeting. {0} the {1} would not let anyone go home."),
                },
                teaser = "{0} has made up their mind about something, and nothing will change it.", // #61: the payoff can't promise the subject
            },
            ["the kind"] = new TraitTerm
            {
                flavor = new TermFlavor { kindness = 0.45f, feasts = 0.15f, quarrels = -0.25f },
                stories = new[]
                {
                    new TermStory("term_meal", 2f, "{0}, the {5}'s {1}, set long tables at the {2}, and everyone ate together.",
                        "{0} the {1} visited every old and sick person in the {5} before the winter."),
                    new TermStory("term_mend", 1.2f, "{0} the {1} sat {3} and {4} down with a pot of stew until they stopped arguing about {6}.",
                        "{0}, the {5}'s {1}, walked {3} and {4} round the pond until they forgot what the row was about."),
                },
                teaser = "{0} has been cooking enough stew for the whole {5}.",
            },
            ["the loud"] = new TraitTerm
            {
                flavor = new TermFlavor { gathering = 0.45f, quiet = -0.3f, mirth = 0.05f },
                stories = new[]
                {
                    new TermStory("term_announce", 2f, "{0}, the {5}'s {1}, made an announcement at the {2} every morning. Most of them were about the weather.",
                        "{0} the {1} called the whole {5} to the {2} to say a roof was fixed. It was {3}'s roof."),
                    new TermStory("term_feast", 0.5f, "{0} the {1} held a feast at the {2}. They could be heard on the far side of the glass.",
                        "{0}, the {5}'s {1}, gave a toast at the {2} so loud the ducks took off."),
                },
                teaser = "{0} has an announcement for tomorrow. Everyone will hear it.",
            },
            ["the clever"] = new TraitTerm
            {
                flavor = new TermFlavor { invention = 0.45f, wonder = 0.1f },
                stories = new[]
                {
                    new TermStory("term_device", 2f, "A strange device appeared by the {2}. {0} the {1} says it counts the days. Nobody has checked.",
                        "{0}, the {5}'s {1}, worked out a faster way to carry water. Now everyone does it their way."),
                },
                teaser = "{0} keeps measuring the shadows by the {2}.",
            },
            ["the tinkerer"] = new TraitTerm
            {
                flavor = new TermFlavor { invention = 0.45f, tidiness = -0.15f },
                stories = new[]
                {
                    new TermStory("term_device", 2f, "The {2} sprouted a weathervane, a bell and a little crank. {0} the {1} isn't finished.",
                        "{0}, the {5}'s {1}, built a cart that steers itself, mostly into the pond."),
                },
                teaser = "Something by the {2} has started ticking. {0} says it's fine.",
            },
            ["the dreamer"] = new TraitTerm
            {
                flavor = new TermFlavor { wonder = 0.45f, invention = 0.1f, tidiness = -0.1f },
                stories = new[]
                {
                    new TermStory("term_plan", 1.5f, "{0}, the {5}'s {1}, announced a tower tall enough to see over the glass. They built the first step.",
                        "{0} the {1} drew a map of the {5} as it will be in a hundred years. It has a lot of bridges."),
                    new TermStory("term_pilgrim", 1.5f, "{0} the {1} led a walk up the hill to wave at the sky. {3} swears something waved back.",
                        "{0}, the {5}'s {1}, spent a night on the hill watching the sky, with {3} holding the blanket."),
                },
                teaser = "{0} has been staring up at the sky again, making plans.",
            },
            ["the brave"] = new TraitTerm
            {
                flavor = new TermFlavor { adventure = 0.45f, caution = -0.2f, gathering = 0.05f },
                stories = new[]
                {
                    new TermStory("term_expedition", 2f, "{0}, the {5}'s {1}, led an expedition to the far edge, where the world turns to glass. They came back with stories.",
                        "{0} the {1} took {3} and {4} on a long hunt. They came back muddy and very proud."),
                },
                teaser = "{0} wants to see what's past the far edge of the {5}.",
            },
            ["the gossip"] = new TraitTerm
            {
                flavor = new TermFlavor { gathering = 0.3f, quarrels = 0.2f, kindness = 0.05f, mirth = 0.1f },
                stories = new[]
                {
                    new TermStory("term_news", 2f, "With {0} as {1}, the whole {5} knew about {3} and {4} before {3} and {4} did.",
                        "{0}, the {5}'s {1}, held every meeting at the {2}, the better to hear everything. They heard everything.").Couple(),
                    new TermStory("term_matchmaking", 1f, "{0} the {1} introduced {3} to {4} at the {2}. They talked until {7}.",
                        "{0}, the {5}'s {1}, made sure {3} and {4} sat next to each other at supper. Twice.").Couple(),
                    new TermStory("term_row", 1f, "{3} and {4} fell out over {6} after {0} the {1} told each what the other had said.",
                        "{0} the {1} let slip what {3} said about {4}'s cooking. The {5} took sides."),
                },
                teaser = "{0} knows something, and is dying to tell.", // #61: the payoff is about whoever it's about
            },
            ["the worrier"] = new TraitTerm
            {
                flavor = new TermFlavor { caution = 0.45f, quiet = 0.3f, adventure = -0.25f, tidiness = 0.1f },
                stories = new[]
                {
                    new TermStory("term_lanterns", 2f, "{0}, the {5}'s {1}, had extra lanterns hung round the {2}, and a fence put round the fence.",
                        "{0} the {1} had everyone home before dark, just in case."),
                },
                teaser = "{0} has been counting the lanterns. And then counting them again.",
            },
            ["the gardener"] = new TraitTerm
            {
                flavor = new TermFlavor { gardening = 0.45f, tidiness = 0.15f, kindness = 0.05f },
                stories = new[]
                {
                    new TermStory("term_garden", 2f, "{0}, the {5}'s {1}, planted flowers all round the {2}. The bees approve.",
                        "{0} the {1} blessed the fields one by one. It took all autumn."),
                },
                teaser = "{0} has been eyeing the bare ground by the {2}.",
            },
            ["the lazy"] = new TraitTerm
            {
                flavor = new TermFlavor { idleness = 0.45f, quiet = 0.1f, gathering = -0.2f, tidiness = -0.15f },
                stories = new[]
                {
                    new TermStory("term_nap", 2f, "{0}, the {5}'s {1}, declared a long lunch. It became a long nap on the green.",
                        "{0} the {1} has not called a meeting in two years. Nobody has complained."),
                },
                teaser = "{0} has found a very comfortable spot by the {2}.",
            },
        };

        /// <summary>Clauses added to existing story lines while a holder of this trait is in office: (story kind, trait) →
        /// " {0} the {1} ...". Text only: the story still happened by the story stream's own dice.</summary>
        static readonly Dictionary<(string kind, string trait), string> TermCodas = new()
        {
            [("storm", "the worrier")] = " {0} the {1} had them all counted in before the first gust.",
            [("lost", "the brave")] = " {0} the {1} led the search party, which came home with firewood and a goat as well.",
            [("festival", "the singer")] = " {0} the {1} sang until the stars came out.",
            [("festival", "the joker")] = " {0} the {1} organised the sack race.",
            [("festival", "the grump")] = " {0} the {1} went to bed early.",
            [("rivalry", "the kind")] = " {0} the {1} has promised to sort it out.",
            [("rivalry", "the gossip")] = " {0} the {1} told everyone by supper.",
            [("wedding", "the singer")] = " {0} the {1} sang them down the path.",
            [("rainbow", "the lazy")] = " {0} the {1} made the afternoon off official.",
        };

        // ---------- queries for views and the planner ----------
        /// <summary>The trait's own colour for one holder (deltas around 0.5, unclamped by others). Neutral if the flavour
        /// is off or they hold no role.</summary>
        public TermFlavor TermFlavorOf(Notable holder)
        {
            var f = TermFlavor.Neutral();
            if (!TermFlavourOn || holder == null || !holder.alive || RoleOf(holder) == null) return f;
            if (TraitTerms.TryGetValue(holder.trait ?? "", out var t)) f.Add(t.flavor, 1f);
            return f.Clamp();
        }

        /// <summary>Anyone's own trait colour (#64, life-in-the-box rule 9): the same deltas as <see cref="TermFlavorOf"/>,
        /// whether or not they hold a role, so a lazy notable without a role naps a little too. A readout: no economy, no
        /// dice, no state change. Neutral if the flavour is off, the notable is gone or the trait has no colour.</summary>
        public TermFlavor TraitFlavorOf(Notable n)
        {
            var f = TermFlavor.Neutral();
            if (!TermFlavourOn || n == null || !n.alive) return f;
            if (TraitTerms.TryGetValue(n.trait ?? "", out var t)) f.Add(t.flavor, 1f);
            return f.Clamp();
        }

        /// <summary>How the box feels now: the leader's trait counts in full, every other office's at half.</summary>
        public TermFlavor CurrentTermFlavor()
        {
            var f = TermFlavor.Neutral();
            if (!TermFlavourOn || State.roles == null) return f;
            string leader = LeaderOf(State.era);
            foreach (var seat in State.roles)
            {
                var h = seat.holderId >= 0 ? FindNotable(seat.holderId) : null;
                if (h == null || !h.alive || !TraitTerms.TryGetValue(h.trait ?? "", out var t)) continue;
                f.Add(t.flavor, seat.role == leader ? 1f : 0.5f);
            }
            return f.Clamp();
        }

        /// <summary>A true line for the evening talk from a role-holder (the leader first), or null. No dice.</summary>
        public string OfficeTeaser() => OfficeTeaser(out _);

        /// <summary><see cref="OfficeTeaser()"/>, with the holder who says it (the teaser thread follows them, SimCore.Cards.cs).</summary>
        internal string OfficeTeaser(out Notable holder)
        {
            holder = null;
            if (!TermFlavourOn || State.roles == null) return null;
            foreach (var seat in State.roles)
            {
                var h = seat.holderId >= 0 ? FindNotable(seat.holderId) : null;
                if (h == null || !h.alive || !TraitTerms.TryGetValue(h.trait ?? "", out var t)) continue;
                var others = TermOthers(h, 1, 21);
                if (t.teaser.Contains("{3}") && others == null) continue;
                holder = h;
                return FillTerm(t.teaser, h, seat, others, 22);
            }
            return null;
        }

        // ---------- the monthly step ----------
        /// <summary>End of Step: each role-holder may add a term line in their trait's style. No dice, no links changed.</summary>
        void OfficeStep()
        {
            if (!TermFlavourOn || State.roles == null) return;
            string leader = LeaderOf(State.era);
            foreach (var seat in State.roles)
            {
                var h = seat.holderId >= 0 ? FindNotable(seat.holderId) : null;
                if (h == null || !h.alive) continue;
                float chance = seat.role == leader ? LeaderTermChance : OfficeTermChance;
                if (RoleRoll(23, h.id, seat.role.Length) >= chance) continue;
                TermStoryFor(seat, h);
            }
        }

        /// <summary>One term line for this holder now, if their trait has a fresh one with enough people. Returns it or null.</summary>
        ChronicleEntry TermStoryFor(RoleSeat seat, Notable h)
        {
            if (!TraitTerms.TryGetValue(h.trait ?? "", out var t)) return null;
            bool rival = Alive(h.rivalId) != null;
            // A holder tells a kind's lines in turn; once they've told them all, the kind rests longer each round (#61: a
            // 40-year term had "made an announcement every morning" three times).
            int Rest(TermStory st) => TermFreshYears * (1 + 2 * ((State.chronicle.Count(e => e.kind == st.kind && e.notableId == h.id) + PrunedTold(st.kind, h.id)) / st.lines.Length));
            var options = t.stories.Where(st => Fresh(st.kind, h.id, Rest(st)) && (st.Others == 0 || TermOthers(h, st.Others, 24) != null)).ToList();
            if (options.Count == 0) return null;
            // A quarrelsome holder with a living rival rows more (notable-roles §4: rivalries ×1.5 [est]).
            float W(TermStory st) => st.weight * (st.kind == "term_row" && rival ? 1.5f : 1f);
            float pick = RoleRoll(25, h.id, seat.role.Length) * options.Sum(W);
            var chosen = options[^1];
            foreach (var st in options) if ((pick -= W(st)) < 0) { chosen = st; break; }
            var others = chosen.Others == 0 ? null : TermOthers(h, chosen.Others, 24);
            // The holder's lines of a kind take turns, so a long term doesn't repeat itself.
            int told = State.chronicle.Count(e => e.kind == chosen.kind && e.notableId == h.id) + PrunedTold(chosen.kind, h.id);
            string line = chosen.lines[told % chosen.lines.Length];
            if (chosen.couple && line.Contains("{4}"))
            {
                others = TermCouple(h, 26); // never a parent and child, the married, or the holder's own spouse (#61)
                if (others == null) return null;
            }
            return TermLine(chosen.kind, FillTerm(line, h, seat, others, 27), h.id, line.Contains("{3}") ? others[0].id : -1, SeatBuilding(seat.role)?.id ?? -1);
        }

        /// <summary>A chronicle line with an id from the term lines' own counter (so the shared ids don't shift).</summary>
        ChronicleEntry TermLine(string kind, string text, int notableId, int otherId, int buildingId)
        {
            var s = State;
            if (s.termLineNextId < TermLineIdBase) // a save that lost the counter: carry on above every id in use
                s.termLineNextId = Math.Max(TermLineIdBase, s.chronicle.Count == 0 ? 0 : s.chronicle.Max(x => x.id) + 1);
            var e = new ChronicleEntry
            {
                id = s.termLineNextId++, month = s.month, kind = kind, text = text, salience = TermSalience,
                notableId = notableId, otherNotableId = otherId, buildingId = buildingId,
            };
            s.chronicle.Add(e);
            Chronicled?.Invoke(e);
            return e;
        }

        /// <summary>The coda for a story line of this kind from the first office (leader first) whose holder's trait has one.
        /// Not when the story is about the holder themself, as either party (#61: a headman doesn't promise to sort out their
        /// own feud).</summary>
        string TermCoda(string kind, string text, int notableId, int otherId = -1)
        {
            if (!TermFlavourOn || State.roles == null || State.roles.Count == 0) return text;
            foreach (var seat in State.roles)
            {
                if (seat.holderId < 0 || seat.holderId == notableId || seat.holderId == otherId) continue;
                var h = FindNotable(seat.holderId);
                if (h == null || !h.alive || !TermCodas.TryGetValue((kind, h.trait ?? ""), out var coda)) continue;
                return text + FillTerm(coda, h, seat, null, 28);
            }
            return text;
        }

        /// <summary>Living adults other than the holder (and their rival first, for a row), picked by a hash; null if too few.</summary>
        List<Notable> TermOthers(Notable h, int count, int salt)
        {
            var pool = State.notables.Where(n => n.alive && n.age >= AdultAge && n.id != h.id).ToList();
            if (pool.Count < count) return null;
            var picked = new List<Notable>();
            var rival = Alive(h.rivalId);
            if (rival != null && rival.age >= AdultAge) { picked.Add(rival); pool.Remove(rival); }
            for (int i = 0; picked.Count < count; i++)
            {
                var n = pool[(int)(RoleRoll(salt + i * 7, h.id, pool.Count) * pool.Count) % pool.Count];
                picked.Add(n);
                pool.Remove(n);
            }
            return picked;
        }

        /// <summary>Two living, single adults (18+), not of one family, not parent and child, within 12 years of each other,
        /// and not the holder: the pair a gossip matches or talks about. Picked by a hash; null if there is none.</summary>
        List<Notable> TermCouple(Notable h, int salt)
        {
            var single = State.notables.Where(n => n.alive && n.age >= 18f && n.age <= 50f && n.id != h.id && Alive(n.partnerId) == null)
                .OrderBy(n => n.id).ToList();
            var pairs = new List<(Notable a, Notable b)>();
            for (int i = 0; i < single.Count; i++)
                for (int j = i + 1; j < single.Count; j++)
                    if (CanCourt(single[i], single[j])) pairs.Add((single[i], single[j]));
            if (pairs.Count == 0) return null;
            var p = pairs[(int)(RoleRoll(salt, h.id, pairs.Count) * pairs.Count) % pairs.Count];
            return new List<Notable> { p.a, p.b };
        }

        /// <summary>Could these two court? Different family names, not kin (no parent, grandparent or sibling in common, as
        /// far as two generations show) and within 12 years of each other.</summary>
        public bool CanCourt(Notable a, Notable b) => a != b && a.family != b.family && Math.Abs(a.age - b.age) <= 12f && !Kin(a, b);

        /// <summary>Close family: one is in the other's line (parents, their partners, grandparents), or the lines meet.</summary>
        public bool Kin(Notable a, Notable b)
        {
            var la = Line(a); var lb = Line(b);
            return la.Contains(b.id) || lb.Contains(a.id) || la.Overlaps(lb);
        }

        /// <summary>A notable's parents and grandparents, with their partners (the sim keeps one parent per child).</summary>
        HashSet<int> Line(Notable n)
        {
            var line = new HashSet<int>();
            var gen = new List<Notable> { n };
            for (int depth = 0; depth < 2; depth++)
            {
                var next = new List<Notable>();
                foreach (var x in gen)
                {
                    var p = FindNotable(x.parentId);
                    if (p == null) continue;
                    line.Add(p.id); next.Add(p);
                    var q = FindNotable(p.partnerId);
                    if (q != null) { line.Add(q.id); next.Add(q); }
                }
                gen = next;
            }
            return line;
        }

        string FillTerm(string line, Notable h, RoleSeat seat, List<Notable> others, int salt)
        {
            string a = others != null && others.Count > 0 ? others[0].FullName : "someone";
            string b = others != null && others.Count > 1 ? others[1].FullName : "someone else";
            string quarrel = QuarrelIn(Quarrels[(int)(RoleRoll(salt, h.id, 0) * Quarrels.Length) % Quarrels.Length]);
            string late = State.era == Era.Camp ? "the fire burned low" : "the lamps went out";
            return string.Format(line, h.FullName, seat.role, SeatLabel(seat.role), a, b, EraNoun(State.era), quarrel, late);
        }

        /// <summary>Where a role works from, as people would say it.</summary>
        string SeatLabel(string role)
        {
            var b = SeatBuilding(role);
            return b?.type switch
            {
                BuildingType.Camp => "camp fire",
                BuildingType.Well => "green",
                BuildingType.Market => "market steps",
                BuildingType.Windmill => "mill",
                BuildingType.Church => State.era == Era.Village ? "shrine" : "church",
                BuildingType.Tavern => "inn",
                BuildingType.Lumbermill => "timber yard",
                BuildingType.Guildhall => "guildhall",
                null => "fire",
                _ => Label(b),
            };
        }

        /// <summary>The term-line kinds a trait can tell (empty for an unknown trait).</summary>
        public static IEnumerable<string> TermKindsOf(string trait) =>
            TraitTerms.TryGetValue(trait ?? "", out var t) ? t.stories.Select(s => s.kind).Distinct() : Enumerable.Empty<string>();

        // ---------- debug ----------
        /// <summary>Debug: this role's holder tells a term line now (if their trait has a fresh one). Returns it or null.</summary>
        public ChronicleEntry DebugTermStory(string role)
        {
            var seat = SeatOf(role);
            var h = seat == null || seat.holderId < 0 ? null : FindNotable(seat.holderId);
            return h == null || !TermFlavourOn ? null : TermStoryFor(seat, h);
        }
    }
}
