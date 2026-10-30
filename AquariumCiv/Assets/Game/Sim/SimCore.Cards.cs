using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>Where a thread came from: a teaser said at the evening talk (first-session §4), or a story link.</summary>
    public static class ThreadSource
    {
        public const string Era = "era", Seat = "seat", Couple = "couple", Gift = "gift", Rivalry = "rivalry", Office = "office",
            Slice = "slice", Link = "link";
    }

    public enum ThreadState { Open, PaidOff, Died }

    /// <summary>
    /// A thread ties chronicle entries into one story (chronicle-and-history.md §2.3, §6). Two kinds:
    /// a <b>teaser</b> the evening talk said (<see cref="teaser"/> set), paid off by a later entry (Mira and Tob marry) or
    /// ended by a death; and a <b>story link</b> (<see cref="ThreadSource.Link"/>) between entries that continue each other
    /// (a wedding and the couple's first child). Entries point at it through <see cref="ChronicleEntry.threadId"/>.
    /// </summary>
    [Serializable]
    public class StoryThread
    {
        public int id;
        public string source;           // ThreadSource
        public string teaser = "";      // the line said at the evening talk ("" for a story link)
        public long month;              // opened
        public long toldMonth;          // last said at the evening talk
        public int notableId = -1;      // the people it is about
        public int otherNotableId = -1;
        public int subjectId = -1;      // the nudge, for a gift
        public string role = "";        // the seat, for a seat
        public ThreadState state;
        public int payoffEntryId = -1;  // the entry that paid it off
        public int endEntryId = -1;     // the death that ended it (the death card mentions it)
        public string endLine = "";     // "Tob never did ask Mira."
    }

    public enum CardTone { Good, Neutral, Sad }

    /// <summary>The card looks of §2.1 ("Also…" is <see cref="ReturnStack.also"/>).</summary>
    public enum CardKind { Story, Keeper, Wish, Relic, Legend, EraReady }

    /// <summary>One card on return: one entry, or several that share a thread (merged, Choice 2). Not saved.</summary>
    public sealed class ChronicleCard
    {
        public CardKind kind;
        public CardTone tone;
        public string group;                    // the kind group (no three weddings in a row)
        public ChronicleEntry lead;             // the best entry: its text is the card's body
        public List<ChronicleEntry> entries = new(); // every new entry of the story, oldest first
        public ChronicleEntry before;           // the last entry of the same thread the player saw on an earlier return
        public float score;                     // the lead's score (salience + pinned + teaser payoff + viewed)
        public string headline;                 // short (≤ ~50 characters), also for the peek and "Also…"
        public string teaser;                   // the evening talk's line it pays off or ends (grey, above the headline)
        public string endLine;                  // a teased thread a death ended ("They never did make up.")
        public bool paysOffTeaser;
        public List<int> people = new();        // everyone in it, lead first
        public int buildingId = -1;             // fly-to place (the first entry with a building)
        public long month;                      // the lead's month
    }

    /// <summary>What <see cref="SimCore.ReturnCards"/> gives the view: the stack in order, and the overflow.</summary>
    public sealed class ReturnStack
    {
        public int cap;                                 // 3 / 5 / 7 by time away
        public List<ChronicleCard> cards = new();       // in showing order: era ready, good news, the rest, deaths, the wish
        public List<ChronicleCard> also = new();        // past the cap, best first; each opens to its full card
        public List<string> alsoHeadlines = new();      // the "Also…" card's three short lines
        public int alsoMore;                            // how many more past those three
        public int collected;                           // entries considered (after the never-shown filter)
    }

    /// <summary>
    /// The chronicle records the cards need (#59, chronicle-and-history.md §2, §5, §6):
    /// <list type="bullet">
    /// <item><b>Teaser threads</b>: <see cref="OpenTeaser"/> says one true line for the evening talk and opens a thread;
    /// the entry that pays it off links back and scores +0.2 on the next return, however late.</item>
    /// <item><b>Story links</b>: a wedding and the couple's first child, a rivalry and the vote or making-up it led to, a
    /// friendship and the wedding, lost and found (<see cref="LinkStories"/> for relics).</item>
    /// <item><b>Ranking inputs</b> per entry: tone and kind group from a table in code (<see cref="ToneOf"/>,
    /// <see cref="GroupOf"/>), people (<see cref="ChronicleEntry.notableId"/>, other), place (building).</item>
    /// <item><b><see cref="ReturnCards"/></b>: ranked, merged and grouped per §2.2, capped 3/5/7 by time away, with "Also…".</item>
    /// <item><b>The glass stage</b> (1–3, §5.2): opened by the era; its relic opens the later half (<see cref="GlassRelicFound"/>).</item>
    /// </list>
    /// Story only. No dice stream is drawn, no economy value is read for a decision that writes back, and threads take
    /// their ids from their own counter (<see cref="SimState.threadNextId"/>), so the shared ids (buildings, notables,
    /// trash, chronicle) never shift.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Debug switch: false turns the card records off (no threads, no links, no teasers) and lets the four lines
        /// that "know too much" back into the camp and village (SimCore.Story.cs). Not saved. The tests run the box both
        /// ways to show the economy is the same.</summary>
        public bool CardsOn { get; set; } = true;

        public const float TeaserPayoffBoost = 0.2f, PinnedBoost = 0.15f, ViewedBoost = 0.05f, SameGroupPenalty = 0.15f;

        public event Action<int> GlassStageChanged;

        // ---------- the kind table (tone, group) ----------
        static readonly Dictionary<string, (CardTone tone, string group)> KindTable = new()
        {
            ["founding"] = (CardTone.Good, "milestone"), ["naming"] = (CardTone.Good, "milestone"),
            ["anniversary"] = (CardTone.Good, "milestone"), ["milestone"] = (CardTone.Good, "milestone"),
            ["first_building"] = (CardTone.Good, "milestone"), ["festival"] = (CardTone.Good, "milestone"),
            ["wedding"] = (CardTone.Good, "family"), ["birth"] = (CardTone.Good, "family"),
            ["friendship"] = (CardTone.Good, "friends"), ["reconcile"] = (CardTone.Good, "friends"),
            ["rivalry"] = (CardTone.Neutral, "friends"),
            ["apprentice"] = (CardTone.Good, "people"), ["elder"] = (CardTone.Good, "people"),
            ["newcomer"] = (CardTone.Good, "people"), ["rise"] = (CardTone.Good, "people"),
            ["retired"] = (CardTone.Neutral, "people"), ["job_change"] = (CardTone.Neutral, "people"), ["job_era"] = (CardTone.Good, "people"),
            ["invention"] = (CardTone.Good, "trait"), ["song"] = (CardTone.Good, "trait"), ["prank"] = (CardTone.Good, "trait"),
            ["giant_turnip"] = (CardTone.Good, "trait"), ["brave"] = (CardTone.Good, "trait"), ["kind"] = (CardTone.Good, "trait"),
            ["dreamer"] = (CardTone.Good, "trait"), ["lazy"] = (CardTone.Good, "trait"), ["loud"] = (CardTone.Good, "trait"),
            ["stubborn"] = (CardTone.Neutral, "trait"), ["worrier"] = (CardTone.Neutral, "trait"), ["gossip"] = (CardTone.Neutral, "trait"),
            ["grump"] = (CardTone.Neutral, "trait"), ["lost"] = (CardTone.Neutral, "trait"), ["found"] = (CardTone.Good, "trait"),
            ["snow"] = (CardTone.Good, "world"), ["rainbow"] = (CardTone.Good, "world"), ["bumper"] = (CardTone.Good, "world"),
            ["ducks"] = (CardTone.Good, "world"), ["sheep"] = (CardTone.Good, "world"),
            ["storm"] = (CardTone.Neutral, "world"), ["fog"] = (CardTone.Neutral, "world"), ["drought"] = (CardTone.Neutral, "world"),
            ["lean_winter"] = (CardTone.Neutral, "world"), ["strange_light"] = (CardTone.Neutral, "world"),
            ["keeper"] = (CardTone.Good, "keeper"),
            ["era"] = (CardTone.Good, "era"), ["era-ready"] = (CardTone.Good, "era"),
            ["role_contest"] = (CardTone.Neutral, "role"), ["role_challenge"] = (CardTone.Neutral, "role"),
            ["role_stepdown"] = (CardTone.Neutral, "role"),
            ["death"] = (CardTone.Sad, "legend"), ["role_death"] = (CardTone.Sad, "legend"),
            ["term_row"] = (CardTone.Neutral, "office"), ["term_complaint"] = (CardTone.Neutral, "office"),
            ["wish"] = (CardTone.Good, "wish"), ["relic"] = (CardTone.Good, "relic"),
            ["coming_of_age"] = (CardTone.Neutral, "hidden"),
        };

        /// <summary>Good, neutral or sad, by story kind (§2.2: from the table, never the text). Unknown kinds are neutral.</summary>
        public static CardTone ToneOf(string kind) =>
            KindTable.TryGetValue(kind ?? "", out var k) ? k.tone : kind != null && (kind.StartsWith("role_") || kind.StartsWith("term_")) ? CardTone.Good : CardTone.Neutral;

        /// <summary>The kind group: cards of the same group already chosen cost −0.15 each. "hidden" is never a card.</summary>
        public static string GroupOf(string kind) =>
            KindTable.TryGetValue(kind ?? "", out var k) ? k.group : kind == null ? "other" : kind.StartsWith("role_") ? "role" : kind.StartsWith("term_") ? "office" : "other";

        /// <summary>§2.2 step 1: never a card: came of age, job changes under 0.5.</summary>
        public static bool ShownAsCard(ChronicleEntry e) =>
            GroupOf(e.kind) != "hidden" && !((e.kind == "job_change" || e.kind == "job_era" || e.kind == "retired") && e.salience < 0.5f);

        /// <summary>A card can be up to 3 after under an hour away, 5 after 1–8 hours, 7 after longer (Choice 1).</summary>
        public static int CardCap(double realHoursAway) => realHoursAway < 1 ? 3 : realHoursAway <= 8 ? 5 : 7;

        public StoryThread FindThread(int id) => id <= 0 || State.threads == null ? null : State.threads.FirstOrDefault(t => t.id == id);

        public StoryThread ThreadOf(ChronicleEntry e) => e == null ? null : FindThread(e.threadId);

        // ---------- the return ----------
        /// <summary>
        /// The cards for a return (§2.2): the entries after <paramref name="sinceMonth"/> (exclusive: that month was seen
        /// live), merged by thread, scored (salience + 0.15 pinned + 0.2 a heard teaser's payoff + 0.05 a person whose card
        /// was opened last session; −0.15 for each card of the same group already chosen), the top
        /// <see cref="CardCap"/> picked and ordered: era ready (if waiting), the best good news, the other good and neutral
        /// cards, the sad ones, the wish. The rest go to "Also…". Read-only apart from bringing the records up to date.
        /// </summary>
        public ReturnStack ReturnCards(long sinceMonth, double realHoursAway, IEnumerable<int> viewedPeople = null) =>
            ReturnCardsCapped(sinceMonth, CardCap(realHoursAway), viewedPeople);

        /// <summary><see cref="ReturnCards(long, double, IEnumerable{int})"/> with an explicit cap (tests, debug).</summary>
        public ReturnStack ReturnCardsCapped(long sinceMonth, int cap, IEnumerable<int> viewedPeople = null)
        {
            AnnotateNew();
            var viewed = viewedPeople == null ? new HashSet<int>() : new HashSet<int>(viewedPeople);
            var stack = new ReturnStack { cap = Math.Max(1, cap) };
            var fresh = State.chronicle.Where(e => e.month > sinceMonth && ShownAsCard(e)).ToList();
            stack.collected = fresh.Count;

            // Merge: one card per thread, one per lone entry, in chronicle order.
            var cards = new List<ChronicleCard>();
            var byThread = new Dictionary<int, ChronicleCard>();
            foreach (var e in fresh)
            {
                if (e.threadId > 0 && byThread.TryGetValue(e.threadId, out var c)) { c.entries.Add(e); continue; }
                c = new ChronicleCard();
                c.entries.Add(e);
                cards.Add(c);
                if (e.threadId > 0) byThread[e.threadId] = c;
            }
            foreach (var c in cards) Fill(c, viewed);

            // Pick: the waiting era and one wish always come; the rest greedily by score, −0.15 per same group chosen.
            var chosen = new List<ChronicleCard>();
            var era = cards.Where(c => c.kind == CardKind.EraReady).OrderByDescending(c => c.month).FirstOrDefault();
            if (era != null) chosen.Add(era);
            var wish = cards.Where(c => c.kind == CardKind.Wish).OrderByDescending(c => c.score).FirstOrDefault();
            if (wish != null && chosen.Count < stack.cap) chosen.Add(wish);
            var pool = cards.Where(c => !chosen.Contains(c) && c.kind != CardKind.Wish && c.kind != CardKind.EraReady).ToList();
            while (chosen.Count < stack.cap && pool.Count > 0)
            {
                ChronicleCard best = null;
                float bestScore = float.MinValue;
                foreach (var c in pool)
                {
                    float s = c.score - SameGroupPenalty * chosen.Count(x => x.group == c.group);
                    if (s > bestScore || (s == bestScore && c.month > best.month)) { best = c; bestScore = s; }
                }
                chosen.Add(best);
                pool.Remove(best);
            }

            // Good news first (decided): if any good-news card exists, one always makes the stack. It takes the place of the
            // lowest-scoring sad card, else the lowest neutral one, never era ready or the wish; if those two fill the cap,
            // it comes on top of it.
            if (!chosen.Any(c => c.tone == CardTone.Good && c.kind != CardKind.EraReady && c.kind != CardKind.Wish))
            {
                var good = pool.Where(c => c.tone == CardTone.Good).OrderByDescending(c => c.score).ThenByDescending(c => c.month).FirstOrDefault();
                if (good != null)
                {
                    var swappable = chosen.Where(c => c.kind != CardKind.EraReady && c.kind != CardKind.Wish).ToList();
                    var drop = swappable.Where(c => c.tone == CardTone.Sad).OrderBy(c => c.score).FirstOrDefault()
                               ?? swappable.OrderBy(c => c.score).FirstOrDefault();
                    if (drop != null) chosen.Remove(drop);
                    chosen.Add(good);
                }
            }

            // Order (good news first, decided).
            static IEnumerable<ChronicleCard> ByScore(IEnumerable<ChronicleCard> xs) => xs.OrderByDescending(c => c.score).ThenBy(c => c.month);
            var rest = chosen.Where(c => c.kind != CardKind.EraReady && c.kind != CardKind.Wish).ToList();
            var headline = ByScore(rest.Where(c => c.tone == CardTone.Good)).FirstOrDefault();
            stack.cards.AddRange(chosen.Where(c => c.kind == CardKind.EraReady));
            if (headline != null) stack.cards.Add(headline);
            stack.cards.AddRange(ByScore(rest.Where(c => c != headline && c.tone != CardTone.Sad)));
            stack.cards.AddRange(ByScore(rest.Where(c => c.tone == CardTone.Sad)));
            stack.cards.AddRange(chosen.Where(c => c.kind == CardKind.Wish));

            stack.also = ByScore(cards.Where(c => !chosen.Contains(c))).ToList();
            stack.alsoHeadlines = stack.also.Take(3).Select(c => c.headline).ToList();
            stack.alsoMore = Math.Max(0, stack.also.Count - 3);
            return stack;
        }

        void Fill(ChronicleCard c, HashSet<int> viewed)
        {
            float best = float.MinValue;
            foreach (var e in c.entries)
            {
                float s = EntryScore(e, viewed);
                if (s > best) { best = s; c.lead = e; }
            }
            c.score = best;
            var lead = c.lead;
            c.month = lead.month;
            c.tone = ToneOf(lead.kind);
            c.group = GroupOf(lead.kind);
            c.kind = CardKindOf(lead);
            c.headline = Headline(lead);
            c.paysOffTeaser = c.entries.Any(PaysOffATeaser);
            foreach (var e in new[] { lead }.Concat(c.entries))
            {
                if (e.notableId >= 0 && !c.people.Contains(e.notableId)) c.people.Add(e.notableId);
                if (e.otherNotableId >= 0 && !c.people.Contains(e.otherNotableId)) c.people.Add(e.otherNotableId);
            }
            c.buildingId = lead.buildingId >= 0 ? lead.buildingId : c.entries.FirstOrDefault(e => e.buildingId >= 0)?.buildingId ?? -1;
            var t = ThreadOf(lead);
            if (t != null)
            {
                if (t.source != ThreadSource.Link && t.state != ThreadState.Open) c.teaser = t.teaser;
                if (t.state == ThreadState.Died && c.entries.Any(e => e.id == t.endEntryId)) c.endLine = t.endLine;
                long first = c.entries[0].month;
                int firstId = c.entries[0].id;
                c.before = State.chronicle.LastOrDefault(e => e.threadId == t.id && e.month <= first && e.id != firstId && !c.entries.Contains(e));
            }
        }

        float EntryScore(ChronicleEntry e, HashSet<int> viewed)
        {
            float s = e.salience;
            int pin = State.pinnedNotableId;
            if (pin >= 0 && (e.notableId == pin || e.otherNotableId == pin)) s += PinnedBoost;
            if (PaysOffATeaser(e)) s += TeaserPayoffBoost;
            if ((e.notableId >= 0 && viewed.Contains(e.notableId)) || (e.otherNotableId >= 0 && viewed.Contains(e.otherNotableId))) s += ViewedBoost;
            return s;
        }

        /// <summary>This entry is the payoff of a teaser the player heard at the evening talk.</summary>
        public bool PaysOffATeaser(ChronicleEntry e)
        {
            var t = ThreadOf(e);
            return t != null && t.source != ThreadSource.Link && t.payoffEntryId == e.id;
        }

        CardKind CardKindOf(ChronicleEntry e)
        {
            if (e.kind == "era-ready" && State.eraReady) return CardKind.EraReady;
            switch (GroupOf(e.kind))
            {
                case "legend": return CardKind.Legend;
                case "wish": return CardKind.Wish;
                case "relic": return CardKind.Relic;
            }
            return e.keeper || e.kind == "keeper" ? CardKind.Keeper : CardKind.Story;
        }

        // ---------- headlines ----------
        /// <summary>A short line for the peek and "Also…" (≤ ~50 characters): a form per kind, else the first sentence.</summary>
        public string Headline(ChronicleEntry e)
        {
            string A = Who(e.notableId), B = Who(e.otherNotableId);
            string h = e.kind switch
            {
                "wedding" when e.otherNotableId >= 0 => $"{A} married {B}",
                "birth" => BirthHeadline(e),
                "death" or "role_death" => $"{A} died",
                "friendship" when e.otherNotableId >= 0 => $"{A} and {B} became friends",
                "rivalry" when e.otherNotableId >= 0 => $"{A} and {B} fell out",
                "reconcile" when e.otherNotableId >= 0 => $"{A} and {B} made up",
                "apprentice" when e.otherNotableId >= 0 => $"{A} became {B}'s apprentice",
                "keeper" => e.notableId >= 0 ? $"{A} answered the Keeper" : "The people answered the Keeper",
                "era-ready" => $"{Place} is ready for a new age",
                "era" => $"{Place} became a {EraNoun(State.era)}",
                "naming" => $"The settlement was named {Place}",
                "first_building" => FirstBuildingHeadline(e),
                "lost" => $"{A} got lost in the hills",
                _ => null,
            };
            return Clip(h ?? FirstSentence(e.text));
        }

        string Who(int id) => id < 0 ? "someone" : FindNotable(id)?.name ?? "someone";

        string BirthHeadline(ChronicleEntry e)
        {
            var child = FindNotable(e.notableId);
            var parent = child == null ? null : FindNotable(child.parentId);
            if (child == null) return null;
            var partner = parent == null ? null : FindNotable(parent.partnerId);
            return parent == null ? $"{child.name} was born"
                : partner != null ? $"{parent.name} and {partner.name} had a baby, {child.name}"
                : $"{parent.name} had a baby, {child.name}";
        }

        string FirstBuildingHeadline(ChronicleEntry e)
        {
            var b = State.buildings.FirstOrDefault(x => x.id == e.buildingId);
            return b == null ? null : $"The first {Label(b)}";
        }

        static string FirstSentence(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int i = text.IndexOfAny(new[] { '.', ':', ';', '!' });
            return i > 0 ? text.Substring(0, i) : text;
        }

        static string Clip(string s) => s.Length <= 50 ? s : s.Substring(0, 49).TrimEnd(' ', ',') + "…";

        // ---------- annotation: links and teaser resolution ----------
        /// <summary>End of Step (no dice): brings the card records up to date. Also called by every query.</summary>
        void CardsStep() => AnnotateNew();

        /// <summary>Entries with <see cref="ChronicleEntry.threadId"/> −1 haven't been looked at yet; they are always the
        /// end of the chronicle, since entries are looked at in order. Looked at: 0 = no thread, else the thread's id.</summary>
        void AnnotateNew()
        {
            if (!CardsOn) return;
            var s = State;
            s.threads ??= new List<StoryThread>();
            if (s.threadNextId < 1) s.threadNextId = s.threads.Count == 0 ? 1 : s.threads.Max(t => t.id) + 1;
            int start = s.chronicle.Count;
            while (start > 0 && s.chronicle[start - 1].threadId == -1) start--;
            for (int i = start; i < s.chronicle.Count; i++) Annotate(i);
            FadeThreads();
        }

        void Annotate(int index)
        {
            var e = State.chronicle[index];
            e.threadId = 0;
            // Teasers first: the payoff links back to the evening talk's thread.
            foreach (var t in State.threads)
            {
                if (t.state != ThreadState.Open || t.source == ThreadSource.Link || t.month >= e.month) continue;
                if (Pays(t, e))
                {
                    t.state = ThreadState.PaidOff;
                    t.payoffEntryId = e.id;
                    if (e.threadId <= 0) e.threadId = t.id;
                }
                else if (Ends(t, e, out string endLine))
                {
                    t.state = ThreadState.Died;
                    if (endLine == null) continue;
                    t.endEntryId = e.id;
                    t.endLine = endLine;
                    if (e.threadId <= 0) e.threadId = t.id;
                }
            }
            // Then story links: join the earlier entry's thread, or start a link thread for both.
            var earlier = LinkedEarlier(e, index);
            if (earlier == null) return;
            if (earlier.threadId > 0 && e.threadId <= 0) e.threadId = earlier.threadId;
            else if (earlier.threadId <= 0 && e.threadId > 0) earlier.threadId = e.threadId;
            else if (earlier.threadId <= 0)
            {
                var t = NewThread(ThreadSource.Link, "", earlier.notableId, earlier.otherNotableId);
                t.month = earlier.month;
                earlier.threadId = e.threadId = t.id;
            }
        }

        static bool Pair(ChronicleEntry e, int a, int b) =>
            a >= 0 && b >= 0 && ((e.notableId == a && e.otherNotableId == b) || (e.notableId == b && e.otherNotableId == a));

        static bool About(ChronicleEntry e, int id) => id >= 0 && (e.notableId == id || e.otherNotableId == id);

        /// <summary>Does this entry pay off the teaser?</summary>
        bool Pays(StoryThread t, ChronicleEntry e)
        {
            switch (t.source)
            {
                case ThreadSource.Couple: return e.kind == "wedding" && Pair(e, t.notableId, t.otherNotableId);
                case ThreadSource.Rivalry:
                    return (e.kind == "reconcile" || e.kind == "role_contest" || e.kind == "role_challenge") && Pair(e, t.notableId, t.otherNotableId);
                case ThreadSource.Seat: return SeatContests.Contains(e.kind) && About(e, t.notableId) && IsSeatLine(e, t.role);
                case ThreadSource.Era: return e.kind == "era-ready" || e.kind == "era";
                case ThreadSource.Gift:
                    var n = State.nudges.FirstOrDefault(x => x.id == t.subjectId);
                    return e.keeper && n != null && n.credited && n.buildingId == e.buildingId && n.answerMonth == e.month;
                case ThreadSource.Office: return e.kind.StartsWith("term_") && e.notableId == t.notableId;
                default: return false;
            }
        }

        /// <summary>The role lines that settle who holds a seat: contests, challenges, elections and successions. Not the era
        /// moment's offices line, not a death or a step-down.</summary>
        static readonly HashSet<string> SeatContests = new()
        {
            "role_contest", "role_challenge", "role_won", "role_reelected", "role_reluctant", "role_rise", "role_masterwork",
            "role_succession", "role_named", "role_inherited", "role_appointed",
        };

        /// <summary>The line is about this seat: told at its building and naming the role (seats can share a building).</summary>
        bool IsSeatLine(ChronicleEntry e, string role)
        {
            var b = SeatBuilding(role);
            return b != null && e.buildingId == b.id && !string.IsNullOrEmpty(role) && e.text.Contains(role);
        }

        /// <summary>Does this entry end the teaser without paying it off? <paramref name="endLine"/> = what the death card
        /// says about it, or null to let it fade quietly.</summary>
        bool Ends(StoryThread t, ChronicleEntry e, out string endLine)
        {
            endLine = null;
            if (e.kind == "death" && e.notableId >= 0 && (e.notableId == t.notableId || e.notableId == t.otherNotableId))
            {
                int other = e.notableId == t.notableId ? t.otherNotableId : t.notableId;
                endLine = t.source switch
                {
                    ThreadSource.Couple => $"{Who(other)} never did ask {Who(e.notableId)}.",
                    ThreadSource.Rivalry => "They never did make up.",
                    ThreadSource.Seat => $"{Who(e.notableId)} never did get to stand.",
                    _ => null,
                };
                return t.source != ThreadSource.Era && t.source != ThreadSource.Gift && t.source != ThreadSource.Slice;
            }
            // One of a teased couple married someone else: it fades.
            return t.source == ThreadSource.Couple && e.kind == "wedding" && (About(e, t.notableId) || About(e, t.otherNotableId));
        }

        /// <summary>Teasers the world moved past: an unanswered gift ran out, the teased holder left office.</summary>
        void FadeThreads()
        {
            foreach (var t in State.threads)
            {
                if (t.state != ThreadState.Open) continue;
                if (t.source == ThreadSource.Gift)
                {
                    var n = State.nudges.FirstOrDefault(x => x.id == t.subjectId);
                    if (n == null || (!n.credited && State.month - n.month >= NudgeMonths)) t.state = ThreadState.Died;
                }
                else if (t.source == ThreadSource.Office && RoleOf(FindNotable(t.notableId)) == null) t.state = ThreadState.Died;
                else if (t.source == ThreadSource.Seat && SeatOf(t.role) == null) t.state = ThreadState.Died; // the era moved on
            }
        }

        /// <summary>The earlier entry this one continues (§2 Choice 2), or null. Never a term line.</summary>
        ChronicleEntry LinkedEarlier(ChronicleEntry e, int index)
        {
            if (e.kind.StartsWith("term_")) return null;
            switch (e.kind)
            {
                case "birth": // wedding → the couple's first child
                {
                    var child = FindNotable(e.notableId);
                    var p = child == null ? null : FindNotable(child.parentId);
                    if (p == null || p.partnerId < 0) return null;
                    int q = p.partnerId;
                    for (int i = index - 1; i >= 0; i--)
                    {
                        var x = State.chronicle[i];
                        if (x.kind == "wedding" && Pair(x, p.id, q)) return x;
                        if (x.kind == "birth" && FindNotable(x.notableId) is Notable c && (c.parentId == p.id || c.parentId == q)) return null; // not the first
                    }
                    return null;
                }
                case "wedding": return Earlier(index, x => x.kind == "friendship" && Pair(x, e.notableId, e.otherNotableId));
                case "reconcile":
                case "role_contest":
                case "role_challenge":
                    return Earlier(index, x => x.kind == "rivalry" && Pair(x, e.notableId, e.otherNotableId));
                case "role_death": return Earlier(index, x => x.kind == "death" && x.notableId == e.notableId && x.month == e.month);
                case "found": return Earlier(index, x => x.kind == "lost" && x.notableId == e.notableId);
                default: return null;
            }
        }

        ChronicleEntry Earlier(int index, Func<ChronicleEntry, bool> match)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                var x = State.chronicle[i];
                if (!x.kind.StartsWith("term_") && match(x)) return x;
            }
            return null;
        }

        /// <summary>Hook (relics, the Left Sandal's finding): tie a later entry to the story it continues, so the return
        /// merges them into one card. Both must already be in the chronicle.</summary>
        public void LinkStories(ChronicleEntry earlier, ChronicleEntry later)
        {
            if (!CardsOn || earlier == null || later == null || earlier == later) return;
            AnnotateNew();
            if (earlier.threadId > 0) later.threadId = earlier.threadId;
            else if (later.threadId > 0) earlier.threadId = later.threadId;
            else
            {
                var t = NewThread(ThreadSource.Link, "", earlier.notableId, earlier.otherNotableId);
                t.month = earlier.month;
                earlier.threadId = later.threadId = t.id;
            }
        }

        StoryThread NewThread(string source, string teaser, int a, int b)
        {
            var s = State;
            var t = new StoryThread { id = s.threadNextId++, source = source, teaser = teaser ?? "", month = s.month, toldMonth = s.month, notableId = a, otherNotableId = b };
            s.threads.Add(t);
            return t;
        }

        // ---------- the evening talk's teaser ----------
        /// <summary>
        /// One true line for the evening talk (first-session §4), and a thread for it: when a later entry resolves it, that
        /// entry links back and gets +0.2 on the next return. The highest-salience source wins (era nearly ready, a seat
        /// coming up, two single friends, an unanswered gift, a rivalry, the holder's trait, a slice of life), never the
        /// same one twice in a row. Null while an era is ready (the era-ready state replaces the teaser) or with the cards
        /// off. No dice.
        /// </summary>
        public string OpenTeaser() => OpenTeaserThread()?.teaser;

        /// <summary><see cref="OpenTeaser"/>, returning the thread.</summary>
        public StoryThread OpenTeaserThread()
        {
            if (!CardsOn || State.eraReady) return null;
            AnnotateNew();
            var s = State;
            var last = FindThread(s.teaserThreadId);
            string lastKey = last == null ? null : Key(last.source, last.notableId, last.otherNotableId, last.subjectId, last.role);
            foreach (var c in TeaserCandidates())
            {
                string key = Key(c.source, c.a, c.b, c.subject, c.role);
                if (key == lastKey && c.source != ThreadSource.Slice) continue;
                if (c.source == ThreadSource.Slice && last != null && last.source == ThreadSource.Slice && last.teaser == c.line) continue;
                foreach (var old in s.threads.Where(t => t.state == ThreadState.Open && t.source == ThreadSource.Slice)) old.state = ThreadState.Died;
                var t = s.threads.FirstOrDefault(x => x.state == ThreadState.Open && x.source != ThreadSource.Link &&
                                                       Key(x.source, x.notableId, x.otherNotableId, x.subjectId, x.role) == key);
                if (t == null)
                {
                    t = NewThread(c.source, c.line, c.a, c.b);
                    t.subjectId = c.subject;
                    t.role = c.role ?? "";
                }
                t.toldMonth = s.month;
                s.teaserThreadId = t.id;
                return t;
            }
            return null;
        }

        /// <summary>The current teaser line (the peek when nothing new happened, Choice 3), or null if it was resolved.</summary>
        public string CurrentTeaser
        {
            get
            {
                if (!CardsOn) return null;
                AnnotateNew();
                var t = FindThread(State.teaserThreadId);
                return t != null && t.state == ThreadState.Open ? t.teaser : null;
            }
        }

        static string Key(string source, int a, int b, int subject, string role) => $"{source}|{Math.Min(a, b)}|{Math.Max(a, b)}|{subject}|{role}";

        /// <summary>What the evening talk could say now, best first.</summary>
        IEnumerable<(string source, string line, int a, int b, int subject, string role)> TeaserCandidates()
        {
            var s = State;
            string seat = SeatLabel(LeaderOf(s.era));
            var adults = s.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.id).ToList();

            // An era nearly ready: ≥ 80 % of every threshold [est].
            var th = NextEraThreshold;
            if (th != null && Math.Min(Math.Min(s.population / (float)th.population, s.buildings.Count / (float)th.buildings), YearsInEra / (float)th.years) >= 0.8f)
                yield return (ThreadSource.Era, NextEra == Era.Village
                    ? "The elders talk about planting seeds and staying put."
                    : "The craftsfolk talk of guilds, and of a bigger market.", -1, -1, -1, "");

            // A seat coming up: the chief's rival, or an aspirant when an elected term is nearly over (≤ 3 years).
            var chief = HolderOf(RoleType.Chief);
            var rival = chief == null ? null : Alive(chief.rivalId);
            if (rival != null && CanStand(rival))
                yield return (ThreadSource.Seat, $"{rival.FullName} has started speaking at the {seat}. The chief has noticed.", rival.id, chief.id, -1, RoleType.Chief);
            foreach (var r in s.roles ?? new List<RoleSeat>())
            {
                if (r.holderId < 0 || r.termEndsMonth < 0 || r.termEndsMonth - s.month > 3 * SimConfig.MonthsPerYear) continue;
                var who = RoleCandidates(r.role).Where(n => CanStand(n) && Aspires(n, r.role)).OrderBy(n => n.id).FirstOrDefault();
                if (who != null)
                    yield return (ThreadSource.Seat, $"{who.FullName} has been shaking a lot of hands at the {SeatLabel(r.role)}. The {r.role}'s term is nearly up.", who.id, r.holderId, -1, r.role);
            }

            // Two single adult friends.
            foreach (var a in adults)
            {
                var b = Alive(a.friendId);
                if (b == null || b.id < a.id || b.friendId != a.id || a.partnerId >= 0 || b.partnerId >= 0 || a.family == b.family ||
                    a.age > 50f || b.age > 50f || b.age < AdultAge) continue;
                yield return (ThreadSource.Couple, $"{a.FullName} and {b.FullName} keep sitting together at the {seat}.", a.id, b.id, -1, "");
                break;
            }

            // A gift not yet answered.
            var gift = s.nudges.Where(n => !n.credited && s.month - n.month < NudgeMonths).OrderBy(n => n.id).FirstOrDefault();
            if (gift != null)
                yield return (ThreadSource.Gift, gift.kind switch
                {
                    NudgeKind.Grove => "People keep walking out to look at the new trees.",
                    NudgeKind.Channel => "Everyone keeps going down to look at the new water.",
                    _ => "The gatherers keep checking the wet ground.",
                }, -1, -1, gift.id, "");

            // A rivalry.
            foreach (var a in adults)
            {
                var b = Alive(a.rivalId);
                if (b == null || b.id < a.id || b.rivalId != a.id) continue;
                yield return (ThreadSource.Rivalry, $"{a.FullName} and {b.FullName} aren't speaking again.", a.id, b.id, -1, "");
                break;
            }

            // The holder's trait (SimCore.Office.cs).
            string office = OfficeTeaser(out var holder);
            if (office != null) yield return (ThreadSource.Office, office, holder.id, -1, -1, "");

            // Nothing pending: a slice of life in the people's own words for their glass stage (§5). Takes turns.
            var lines = SliceLines[GlassStage - 1];
            int told = s.threads.Count(t => t.source == ThreadSource.Slice);
            var kid = adults.Count == 0 ? null : adults[told % adults.Count];
            string line = lines[told % lines.Length].Replace("{0}", kid?.FullName ?? "Someone").Replace("{1}", seat);
            yield return (ThreadSource.Slice, line, kid?.id ?? -1, -1, -1, "");
        }

        /// <summary>Slices of life by glass stage (decisions 2026-10-29: camp, a bit scared of the glass; village, they play
        /// with it and look for you; town, the scholars study it). Never "glass" before the town (Choice 10).</summary>
        static readonly string[][] SliceLines =
        {
            new[]
            {
                "The kids are building a raft.",
                "The children dared each other to touch the edge of the sky. Nobody did.",
                "{0} keeps looking up at the bright sky, then hurrying back to the {1}.",
                "Someone heard a hum from the sky last night. Everyone slept close to the {1}.",
            },
            new[]
            {
                "The kids keep waving at the window, in case the Keeper waves back.",
                "{0} swears the Keeper smiled at the new roof.",
                "The children are playing Keeper-says at the {1}. The Keeper always wins.",
                "The elders say the Keeper likes the new mill. Nobody knows how they know.",
            },
            new[]
            {
                "The astronomers have asked for a bigger lens.",
                "{0} measured the glass again. It is still there, and still glass.",
                "The scholars argue at the {1}: one Keeper, or many?",
                "{0} has drawn a map with four edges. Each says: here be glass.",
            },
        };

        // ---------- the glass stage ----------
        /// <summary>How the people understand the glass (§5.2, decisions 2026-10-29): 1 = the sky wall (camp: they gather
        /// at it, a bit scared), 2 = the Keeper's window (village: they play with it and look for you), 3 = a wall with a
        /// room beyond (town: the scholars study it). The era opens each stage; never back.</summary>
        public int GlassStage => Math.Clamp(Math.Max(State.glassStage, (int)State.era + 1), 1, 3);

        /// <summary>The current stage's relic has been found (the Sky Crumb in stage 1, the Keeper Doll in 2, the Lens in
        /// 3): the stage's later half is open (decisions 2026-10-29, #58). A relic found before its era counts once the era
        /// arrives.</summary>
        public bool GlassNudged => GlassRelic.OfStage(GlassStage) is string r && State.storyFlags.Contains("relic:" + r);

        public static class GlassRelic
        {
            public const string SkyCrumb = "sky_crumb", KeeperDoll = "keeper_doll", Lens = "lens";

            /// <summary>The stage a relic belongs to (0 = it says nothing about the glass).</summary>
            public static int StageOf(string relic) => relic switch { SkyCrumb => 1, KeeperDoll => 2, Lens => 3, _ => 0 };

            /// <summary>The relic that opens a stage's later half.</summary>
            public static string OfStage(int stage) => stage switch { 1 => SkyCrumb, 2 => KeeperDoll, 3 => Lens, _ => null };
        }

        /// <summary>
        /// Hook for the relic events (none exist yet): a relic was found. It is remembered in
        /// <see cref="SimState.storyFlags"/> ("relic:" + id) and opens its own stage's later half (<see cref="GlassNudged"/>).
        /// It never moves the stage: the era opens each stage (#58), so a later stage's relic found early applies when that
        /// era arrives. Returns the stage (unchanged).
        /// </summary>
        public int GlassRelicFound(string relic)
        {
            var flags = State.storyFlags;
            if (!string.IsNullOrEmpty(relic) && !flags.Contains("relic:" + relic)) flags.Add("relic:" + relic);
            return GlassStage; // only the era moves the stage (#58)
        }

        /// <summary>Called at the era moment (SimCore.Eras.cs): the era lifts the stage to its own.</summary>
        void GlassOnEraChange() => SetGlassStage(GlassStage);

        void SetGlassStage(int stage)
        {
            int before = State.glassStage;
            State.glassStage = Math.Clamp(Math.Max(before, stage), 1, 3);
            if (State.glassStage != before) GlassStageChanged?.Invoke(State.glassStage);
        }

        // ---------- debug ----------
        /// <summary>Debug: set the glass stage (1–3). Can go back here only, for testing; the era's own stage still holds.</summary>
        public void DebugSetGlassStage(int stage)
        {
            State.glassStage = Math.Clamp(stage, 1, 3);
            GlassStageChanged?.Invoke(GlassStage);
        }

        /// <summary>Debug: open a teaser now even while an era waits (the evening talk's line). Returns the line or null.</summary>
        public string DebugOpenTeaser()
        {
            bool ready = State.eraReady;
            State.eraReady = false;
            try { return OpenTeaser(); }
            finally { State.eraReady = ready; }
        }
    }
}
