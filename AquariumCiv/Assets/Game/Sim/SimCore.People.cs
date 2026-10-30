using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>How two notables are tied (the person card's family/friends/rivals rows and the lines between dolls).
    /// Saved nowhere: worked out from the sim's links and the chronicle each time it is asked.</summary>
    public enum TieKind
    {
        Partner,    // partnerId (a dead partner stays: "late partner")
        Parent,     // parentId, or the parent's partner at the birth (the other parent; the sim keeps one parentId)
        Child,
        Sibling,    // shares a parent
        Friend,     // friendId
        Rival,      // rivalId
        Mentor,     // mentorId: the master
        Apprentice, // the other way round
        MadeUp,     // ex-rivals: a "reconcile" entry, and not rivals again
        Shared,     // no tie above, but in at least SharedStoriesMin stories together (derived, no dice)
    }

    /// <summary>One tie as one person sees it ("Bo's friend Ada").</summary>
    public sealed class PersonTie
    {
        public int otherId;
        public string otherName;        // full name
        public TieKind kind;
        public string label;            // "partner", "late friend", "mentor", "made up", "often in stories together" ...
        public bool otherAlive;
        public int sharedStories;       // chronicle entries naming both
    }

    /// <summary>A chronicle line on the person card.</summary>
    public sealed class PersonStory
    {
        public int entryId;
        public long month;
        public int year;                // 1-based, as the plate reads
        public string kind, text, headline;
        public float salience;
        public int otherId = -1;        // the other person in it (-1 = none)
        public string quote = "";
        public int speakerId = -1;
    }

    /// <summary>One term in an important role.</summary>
    public sealed class PersonTerm
    {
        public string role;
        public int fromYear;
        public int toYear = -1;         // -1 = still holds it
        public int years;               // whole years held so far
        public string how = "";         // RoleRule name (Contest, Election...)
    }

    /// <summary>A relic the person owns or keeps.</summary>
    public sealed class PersonRelic
    {
        public string id, name, label;
        public bool owner, holder;
    }

    /// <summary>The card of a notable who has died (game definition §4: "dead notables go to a legends roster").</summary>
    public sealed class Memorial
    {
        public int diedYear;
        public int ageAtDeath;
        public string deathLine = "";           // the death entry's text, if it is still kept
        public bool diedInOffice;
        public List<string> legendReasons = new(); // chronicle-and-history.md §4.1 rules this person meets ("founder", "leader", ...)
        public List<PersonTie> rememberedBy = new(); // living family and friends
        public bool IsLegend => legendReasons.Count > 0;
    }

    /// <summary>
    /// The person card's view-model (#39; game definition §4, first-session §5 "Person card", notable-roles §6): everything
    /// the card shows, built from the sim on demand. Pure: reads only, draws no dice, writes nothing.
    /// </summary>
    public sealed class PersonCard
    {
        public int id;
        public string name, familyName, fullName, trait;
        public bool alive;
        public int age;                 // whole years (at death, for the dead)
        public int bornYear, diedYear = -1;
        public string stage;            // "baby", "child", "adult", "elder"
        public string job = "";         // JobType ("" = a child)
        public string role;             // RoleType held now, or null
        public int roleSinceYear = -1;
        public int termEndsYear = -1;   // elected roles: the year of the next vote (-1 = no term)
        public string roleLine = "";    // "gatherer, chief of the camp"; "child"; "elder, priest"
        public List<PersonTerm> terms = new();          // every term, oldest first (the book's "roles with years")
        public List<PersonTie> family = new();          // partner, parents, children, siblings (living first)
        public List<PersonTie> friends = new();         // friend, made up, shared stories
        public List<PersonTie> rivals = new();
        public List<PersonTie> mentors = new();         // mentor and apprentices
        public List<PersonStory> recentStories = new(); // newest first, at most SimCore.CardStories
        public List<PersonStory> bestStories = new();   // by salience, at most SimCore.CardStories
        public Wish wish;                               // their open wish, or null
        public string wishText = "";
        public List<PersonRelic> relics = new();
        public PeopleQuote quote;                       // a line in their voice for the belief stage now (the dead: their last words kept)
        public bool pinned, canPin;
        public float pinnedHours;
        public Memorial memorial;                       // null while alive
    }

    /// <summary>One line between two dolls (relationships visible in the world, Gate 2 note). One edge per pair: the
    /// strongest tie is <see cref="kind"/>; <see cref="kinds"/> lists them all. For directed kinds <see cref="a"/> is the
    /// parent / mentor.</summary>
    public sealed class RelEdge
    {
        public int a, b;
        public TieKind kind;
        public List<TieKind> kinds = new();
        public string label;        // "partners", "parent and child", "siblings", "friends", "rivals", "mentor and apprentice", "made up", "shared stories"
        public float strength;      // 0–1: how strongly to draw it (shared stories scale it; formal ties are 1)
        public int sharedStories;
        public bool Involves(int id) => a == id || b == id;
    }

    /// <summary>
    /// Notables in the view (#39): the person card, the relationships graph and the pin rules. Pure and deterministic:
    /// no dice, nothing in the economy reads any of it. Friends, rivals, partners, mentors and parents are the sim's own
    /// links; the other parent, siblings, ex-rivals and "shared stories" ties are derived from the links and the
    /// chronicle, so they never need a new save field.
    /// <para><b>Pin rules.</b> One pin at a time (<see cref="SimState.pinnedNotableId"/>); pinning another moves it. Only a
    /// living notable can be pinned. The pinned person's stories rank +0.15 on return (SimCore.Cards.cs), and their pin
    /// time counts toward the legend rule while they live (SimCore.History.cs). When they die the pin stays on them (their
    /// death card ranks first among the sad ones, their card turns to a memorial) until the player pins someone else or
    /// unpins.</para>
    /// </summary>
    public partial class SimCore
    {
        public const int CardStories = 3;       // stories on the card (first-session §5: "last 3 stories")
        public const int SharedStoriesMin = 2;  // co-mentions for a "shared stories" tie [est]
        public const int LegendRoleYears = 10;  // "held any role ≥ 10 years" (chronicle-and-history §4.1)
        public const float LegendStorySalience = 0.7f;

        // ---------- pin ----------
        /// <summary>The pinned notable (alive or remembered), or null.</summary>
        public Notable Pinned => State.pinnedNotableId < 0 ? null : FindNotable(State.pinnedNotableId);

        /// <summary>Only a living notable can be pinned.</summary>
        public bool CanPin(int notableId) => Alive(notableId) != null;

        /// <summary>Pin this notable (the old pin, if any, is replaced). False if they can't be pinned; the pin stays as it was.</summary>
        public bool Pin(int notableId)
        {
            if (!CanPin(notableId)) return false;
            State.pinnedNotableId = notableId;
            return true;
        }

        public void Unpin() => State.pinnedNotableId = -1;

        /// <summary>Tap the card's Pin button: pins, or unpins if this person is already the pin. Returns whether they are
        /// pinned afterwards.</summary>
        public bool TogglePin(int notableId)
        {
            if (State.pinnedNotableId == notableId) { Unpin(); return false; }
            return Pin(notableId);
        }

        // ---------- an index of the chronicle (rebuilt when the chronicle, the people or the month change) ----------
        sealed class PeopleIndex
        {
            public long month; public int entries, people;
            public readonly Dictionary<int, int> otherParent = new();
            public readonly Dictionary<(int, int), int> shared = new();
            public readonly Dictionary<(int, int), (long made, long fell)> feud = new();
        }
        PeopleIndex peopleIdx;

        static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

        PeopleIndex Index()
        {
            var s = State;
            var x = peopleIdx;
            if (x != null && x.month == s.month && x.entries == s.chronicle.Count && x.people == s.notables.Count) return x;
            x = new PeopleIndex { month = s.month, entries = s.chronicle.Count, people = s.notables.Count };
            var weddings = new Dictionary<int, List<(long month, int other)>>();
            foreach (var e in s.chronicle)
            {
                if (e.notableId < 0 || e.otherNotableId < 0 || e.notableId == e.otherNotableId) continue;
                var k = Key(e.notableId, e.otherNotableId);
                x.shared[k] = x.shared.TryGetValue(k, out int c) ? c + 1 : 1;
                if (e.kind == "reconcile" || e.kind == "rivalry")
                {
                    x.feud.TryGetValue(k, out var f);
                    if (f == default) f = (-1, -1);
                    if (e.kind == "reconcile") f.made = e.month; else f.fell = e.month;
                    x.feud[k] = f;
                }
                if (e.kind == "wedding")
                {
                    foreach (var (me, other) in new[] { (e.notableId, e.otherNotableId), (e.otherNotableId, e.notableId) })
                    {
                        if (!weddings.TryGetValue(me, out var l)) weddings[me] = l = new List<(long, int)>();
                        l.Add((e.month, other));
                    }
                }
            }
            foreach (var child in s.notables)
            {
                var p = FindNotable(child.parentId);
                if (p == null) continue;
                int other = -1;
                bool wed = false;
                if (weddings.TryGetValue(p.id, out var l))
                    foreach (var w in l) if (w.month <= child.bornMonth) { other = w.other; wed = true; }
                var q = FindNotable(other);
                if (q == null && !wed && p.partnerId >= 0)
                {
                    // the wedding line was pruned: the parent's partner now, if they were already grown and alive at the birth
                    var cand = FindNotable(p.partnerId);
                    if (cand != null && cand.bornMonth + (long)(AdultAge * SimConfig.MonthsPerYear) <= child.bornMonth && cand.parentId != p.id) q = cand;
                }
                if (q == null || q.id == child.id || (q.diedMonth >= 0 && q.diedMonth < child.bornMonth)) continue;
                x.otherParent[child.id] = q.id;
            }
            return peopleIdx = x;
        }

        // ---------- family, derived ----------
        /// <summary>A child's other parent: the sim keeps one parentId, so this is the parent's partner at the birth (the last
        /// wedding of that parent before the child was born, whose partner was still alive then). -1 = unknown or none.</summary>
        public int OtherParentOf(Notable child) => child != null && Index().otherParent.TryGetValue(child.id, out int o) ? o : -1;

        /// <summary>Both known parents (one or two ids).</summary>
        public List<int> ParentsOf(Notable n)
        {
            var list = new List<int>();
            if (n == null) return list;
            if (FindNotable(n.parentId) != null) list.Add(n.parentId);
            int o = OtherParentOf(n);
            if (o >= 0 && !list.Contains(o)) list.Add(o);
            return list;
        }

        /// <summary>Their children, including those born to their partner (the sim records one parent), oldest first.</summary>
        public List<Notable> AllChildrenOf(int notableId) =>
            State.notables.Where(c => c.id != notableId && (c.parentId == notableId || OtherParentOf(c) == notableId)).OrderBy(c => c.bornMonth).ThenBy(c => c.id).ToList();

        /// <summary>Brothers and sisters: anyone sharing a known parent.</summary>
        public List<Notable> SiblingsOf(Notable n)
        {
            var mine = ParentsOf(n);
            if (mine.Count == 0) return new List<Notable>();
            return State.notables.Where(o => o.id != n.id && ParentsOf(o).Any(mine.Contains)).OrderBy(o => o.bornMonth).ThenBy(o => o.id).ToList();
        }

        /// <summary>Chronicle entries naming both people.</summary>
        public int SharedStories(int a, int b) => a != b && Index().shared.TryGetValue(Key(a, b), out int c) ? c : 0;

        /// <summary>Ex-rivals: their last "reconcile" came after their last "rivalry" and they aren't rivals now.</summary>
        public bool MadeUp(Notable x, Notable y)
        {
            if (x == null || y == null || x == y || x.rivalId == y.id || y.rivalId == x.id) return false;
            return Index().feud.TryGetValue(Key(x.id, y.id), out var f) && f.made >= 0 && f.made >= f.fell;
        }

        // ---------- ties ----------
        /// <summary>Every tie this person has, to living and dead notables, strongest kind per kind (a sibling who is also a
        /// rival shows in both rows).</summary>
        public List<PersonTie> TiesOf(int notableId)
        {
            var n = FindNotable(notableId);
            var ties = new List<PersonTie>();
            if (n == null) return ties;
            var seen = new HashSet<(int, TieKind)>();
            void Add(Notable o, TieKind k)
            {
                if (o == null || o.id == n.id || !seen.Add((o.id, k))) return;
                ties.Add(new PersonTie { otherId = o.id, otherName = o.FullName, kind = k, otherAlive = o.alive, sharedStories = SharedStories(n.id, o.id), label = TieLabel(k, o.alive) });
            }
            Add(FindNotable(n.partnerId), TieKind.Partner);
            foreach (int p in ParentsOf(n)) Add(FindNotable(p), TieKind.Parent);
            foreach (var c in AllChildrenOf(n.id)) Add(c, TieKind.Child);
            foreach (var sib in SiblingsOf(n)) Add(sib, TieKind.Sibling);
            Add(FindNotable(n.friendId), TieKind.Friend);
            foreach (var o in State.notables) if (o.friendId == n.id) Add(o, TieKind.Friend);
            Add(FindNotable(n.rivalId), TieKind.Rival);
            foreach (var o in State.notables) if (o.rivalId == n.id) Add(o, TieKind.Rival);
            Add(FindNotable(n.mentorId), TieKind.Mentor);
            foreach (var o in State.notables) if (o.mentorId == n.id) Add(o, TieKind.Apprentice);
            foreach (var o in State.notables) if (MadeUp(n, o)) Add(o, TieKind.MadeUp);
            // Shared stories: people they keep turning up with, no tie above. The chronicle's order, so it never changes after the fact.
            var tied = new HashSet<int>(ties.Select(t => t.otherId));
            foreach (var o in State.notables)
                if (!tied.Contains(o.id) && o.id != n.id && SharedStories(n.id, o.id) >= SharedStoriesMin) Add(o, TieKind.Shared);
            return ties;
        }

        static string TieLabel(TieKind k, bool alive)
        {
            string s = k switch
            {
                TieKind.Partner => "partner",
                TieKind.Parent => "parent",
                TieKind.Child => "child",
                TieKind.Sibling => "sibling",
                TieKind.Friend => "friend",
                TieKind.Rival => "rival",
                TieKind.Mentor => "mentor",
                TieKind.Apprentice => "apprentice",
                TieKind.MadeUp => "made up after a feud",
                _ => "often in stories together",
            };
            return alive || k == TieKind.MadeUp || k == TieKind.Shared ? s : "late " + s;
        }

        static string PairLabel(TieKind k) => k switch
        {
            TieKind.Partner => "partners",
            TieKind.Parent or TieKind.Child => "parent and child",
            TieKind.Sibling => "siblings",
            TieKind.Friend => "friends",
            TieKind.Rival => "rivals",
            TieKind.Mentor or TieKind.Apprentice => "mentor and apprentice",
            TieKind.MadeUp => "made up",
            _ => "shared stories",
        };

        /// <summary>Which tie wins when a pair has several (the line drawn between them): rivals are the drama, so they win
        /// over everything but a partner.</summary>
        static int TieRank(TieKind k) => k switch
        {
            TieKind.Partner => 0, TieKind.Rival => 1, TieKind.Parent or TieKind.Child => 2, TieKind.Friend => 3,
            TieKind.Sibling => 4, TieKind.Mentor or TieKind.Apprentice => 5, TieKind.MadeUp => 6, _ => 7,
        };

        /// <summary>
        /// The relationships between notables, one edge per pair (for drawing lines between dolls, "relationships visible").
        /// <paramref name="livingOnly"/>: both ends alive (the world view). <paramref name="focusId"/> ≥ 0: only that
        /// person's edges (the card's "show their people"), dead ones included unless livingOnly. Ordered by a, then b.
        /// </summary>
        public List<RelEdge> RelationshipGraph(bool livingOnly = true, int focusId = -1)
        {
            var edges = new Dictionary<(int, int), RelEdge>();
            IEnumerable<Notable> from = focusId >= 0 ? new[] { FindNotable(focusId) }.Where(x => x != null) : State.notables;
            foreach (var n in from)
            {
                if (livingOnly && !n.alive) continue;
                foreach (var t in TiesOf(n.id))
                {
                    if (livingOnly && !t.otherAlive) continue;
                    // the edge's direction: the parent / mentor first; otherwise the lower id first
                    TieKind k = t.kind;
                    int a = Math.Min(n.id, t.otherId), b = Math.Max(n.id, t.otherId);
                    switch (k)
                    {
                        case TieKind.Child: k = TieKind.Parent; a = n.id; b = t.otherId; break;     // n is the parent
                        case TieKind.Parent: a = t.otherId; b = n.id; break;
                        case TieKind.Apprentice: k = TieKind.Mentor; a = n.id; b = t.otherId; break; // n is the master
                        case TieKind.Mentor: a = t.otherId; b = n.id; break;
                    }
                    var key = (Math.Min(a, b), Math.Max(a, b));
                    if (!edges.TryGetValue(key, out var e)) edges[key] = e = new RelEdge { a = a, b = b, kind = k, sharedStories = t.sharedStories };
                    if (!e.kinds.Contains(k)) e.kinds.Add(k);
                    if (TieRank(k) < TieRank(e.kind)) { e.kind = k; e.a = a; e.b = b; }
                }
            }
            foreach (var e in edges.Values)
            {
                e.kinds.Sort((x, y) => TieRank(x).CompareTo(TieRank(y)));
                e.label = PairLabel(e.kind);
                e.strength = e.kind == TieKind.Shared ? Math.Min(1f, e.sharedStories / 4f) : 1f;
            }
            return edges.Values.OrderBy(e => Math.Min(e.a, e.b)).ThenBy(e => Math.Max(e.a, e.b)).ToList();
        }

        // ---------- the card ----------
        /// <summary>The person card for a notable, or null if there is no such notable.</summary>
        public PersonCard PersonCardOf(int notableId)
        {
            var n = FindNotable(notableId);
            if (n == null) return null;
            var c = new PersonCard
            {
                id = n.id, name = n.name, familyName = n.family, fullName = n.FullName, trait = n.trait, alive = n.alive,
                age = (int)Math.Floor(n.age), bornYear = YearNo(n.bornMonth), diedYear = n.alive || n.diedMonth < 0 ? -1 : YearNo(n.diedMonth),
                job = n.job ?? "",
            };
            c.stage = n.age < 4f ? "baby" : n.age < AdultAge ? "child" : (n.age >= Config.retireAge || n.job == JobType.Elder) ? "elder" : "adult";

            // job and role
            if (n.alive)
            {
                var seat = State.roles?.FirstOrDefault(r => r.holderId == n.id);
                if (seat != null)
                {
                    c.role = seat.role;
                    c.roleSinceYear = YearNo(seat.sinceMonth);
                    c.termEndsYear = seat.termEndsMonth >= 0 ? YearNo(seat.termEndsMonth) : -1;
                }
            }
            c.roleLine = CardRoleLine(n, c.role);
            foreach (var t in TermsOf(n.id).OrderBy(t => t.fromMonth))
            {
                long end = t.toMonth >= 0 ? t.toMonth : (n.alive ? State.month : n.diedMonth);
                c.terms.Add(new PersonTerm
                {
                    role = t.role, fromYear = YearNo(t.fromMonth), toYear = t.toMonth >= 0 ? YearNo(t.toMonth) : -1,
                    years = (int)(Math.Max(0, end - t.fromMonth) / SimConfig.MonthsPerYear), how = t.how ?? "",
                });
            }

            // people
            foreach (var t in TiesOf(n.id))
            {
                switch (t.kind)
                {
                    case TieKind.Partner: case TieKind.Parent: case TieKind.Child: case TieKind.Sibling: c.family.Add(t); break;
                    case TieKind.Rival: c.rivals.Add(t); break;
                    case TieKind.Mentor: case TieKind.Apprentice: c.mentors.Add(t); break;
                    default: c.friends.Add(t); break;
                }
            }
            c.family = c.family.OrderByDescending(t => t.otherAlive).ThenBy(t => (int)t.kind).ToList();
            c.friends = c.friends.OrderByDescending(t => t.otherAlive).ThenBy(t => (int)t.kind).ThenByDescending(t => t.sharedStories).ToList();

            // stories
            var mine = State.chronicle.Where(e => About(e, n.id) && ShownAsCard(e) && e.kind != "wish").ToList();
            var forBest = n.alive ? mine : mine.Where(e => !(e.kind == "death" && e.notableId == n.id)).ToList();
            c.recentStories = mine.AsEnumerable().Reverse().Take(CardStories).Select(e => StoryOf(e, n.id)).ToList();
            c.bestStories = forBest.OrderByDescending(e => e.salience).ThenByDescending(e => e.month).ThenByDescending(e => e.id)
                .Take(CardStories).Select(e => StoryOf(e, n.id)).ToList();

            // wish
            c.wish = n.alive ? State.wishes?.FirstOrDefault(w => w.state == WishState.Open && w.notableId == n.id) : null;
            if (c.wish != null)
                c.wishText = State.chronicle.FirstOrDefault(e => e.id == c.wish.entryId)?.text ?? $"{n.name} hopes for a {ShapingToolName(c.wish.kind)}.";

            // relics
            foreach (var r in FoundRelics)
            {
                bool own = r.ownerId == n.id, hold = r.holderId == n.id;
                if (!own && !hold) continue;
                c.relics.Add(new PersonRelic { id = r.id, name = RelicName(r.id), label = RelicLabel(r.id), owner = own, holder = hold });
            }

            c.quote = QuoteOf(n);
            c.pinned = State.pinnedNotableId == n.id;
            c.canPin = CanPin(n.id);
            c.pinnedHours = PinnedHours(n.id);
            if (!n.alive) c.memorial = MemorialOf(n, c);
            return c;
        }

        /// <summary>"gatherer, chief of the camp"; "child"; "elder, priest". The dead: their last job and their last role.</summary>
        public string CardRoleLine(Notable n, string role = null)
        {
            if (n == null) return "";
            if (role == null && !n.alive) role = TermsOf(n.id).OrderBy(t => t.fromMonth).LastOrDefault()?.role;
            string job = n.age < AdultAge ? (n.age < 4f ? "baby" : "child") : string.IsNullOrEmpty(n.job) ? "" : n.job;
            if (string.IsNullOrEmpty(role)) return job;
            string r = role == RoleType.Chief || role == RoleType.Headman || role == RoleType.Lord ? $"{role} of the {EraNoun(State.era)}" : role;
            return string.IsNullOrEmpty(job) ? r : $"{job}, {r}";
        }

        PersonStory StoryOf(ChronicleEntry e, int me) => new PersonStory
        {
            entryId = e.id, month = e.month, year = YearNo(e.month), kind = e.kind, text = e.text, headline = Headline(e), salience = e.salience,
            otherId = e.notableId == me ? e.otherNotableId : e.notableId, quote = e.quote ?? "", speakerId = e.speakerId,
        };

        // ---------- their voice ----------
        /// <summary>
        /// A line in this person's voice: for the living, a quote from the bank for the belief stage and half now whose speaker
        /// is them (their role, job, age, trait, a scholar, the Lens's maker, the Sandal's owner), else the last quote they said
        /// in the chronicle, else a crowd line (the person as one of the crowd). For the dead: the last quote they said, or
        /// null. Changes once a year (never flickers while the card is open). No dice; null when relics are off.
        /// </summary>
        public PeopleQuote QuoteOf(Notable n)
        {
            if (n == null || !RelicsOn) return null;
            var said = State.chronicle.LastOrDefault(e => e.speakerId == n.id && !string.IsNullOrEmpty(e.quote));
            PeopleQuote Said() => said == null ? null : new PeopleQuote { lineNo = 0, text = said.quote, speakerId = n.id };
            if (!n.alive) return Said();
            var own = new List<BankLine>();
            var crowd = new List<BankLine>();
            foreach (var l in LinesNow())
            {
                if (l.speaker == "crowd") { if (n.age >= 4f) crowd.Add(l); }
                else if (SpeaksAs(n, l.speaker)) own.Add(l);
            }
            int turn = (int)(State.month / SimConfig.MonthsPerYear) + n.id;
            if (own.Count > 0) return Say(own[turn % own.Count], n.id);
            return Said() ?? (crowd.Count > 0 ? Say(crowd[turn % crowd.Count], n.id) : null);
        }

        /// <summary>Could this person say a bank line written for <paramref name="speaker"/>? (Like <see cref="Speaker"/>, but
        /// "is it them?" rather than "who is it?".)</summary>
        bool SpeaksAs(Notable n, string speaker)
        {
            string role = RoleOf(n);
            switch (speaker)
            {
                case "kid": return n.age >= 4f && n.age < AdultAge;
                case "chief": case "headman": case "lord": case "priest": case "merchant": return role == speaker;
                case "captain": return role == RoleType.Captain;
                case "leader": return role != null && role == LeaderOf(State.era);
                case "farmer": return n.job == JobType.Farmer;
                case "elder": return n.age >= Config.elderAge;
                case "scholar": return Scholars.Any(x => x.id == n.id);
                case "lens maker": return FindRelic(RelicId.Lens)?.ownerId == n.id;
                case "owner":
                    var s = FindRelic(RelicId.LeftSandal);
                    return s != null && s.Found && (s.ownerId == n.id || s.holderId == n.id);
                default: return speaker.StartsWith("the ") && n.age >= AdultAge && n.trait == speaker;
            }
        }

        // ---------- the dead ----------
        Memorial MemorialOf(Notable n, PersonCard c)
        {
            var m = new Memorial
            {
                diedYear = c.diedYear, ageAtDeath = (int)Math.Floor(n.age),
                deathLine = State.chronicle.LastOrDefault(e => (e.kind == "death" || e.kind == "role_death") && e.notableId == n.id)?.text ?? "",
                diedInOffice = Hist.roleTerms.Any(t => t.holderId == n.id && n.diedMonth >= 0 && (t.toMonth < 0 || t.toMonth >= n.diedMonth)),
                legendReasons = LegendReasons(n.id),
            };
            m.rememberedBy = c.family.Concat(c.friends).Concat(c.mentors).Where(t => t.otherAlive && t.kind != TieKind.Shared)
                .GroupBy(t => t.otherId).Select(g => g.First()).ToList();
            return m;
        }

        /// <summary>Which of the legend rules (chronicle-and-history.md §4.1) this person meets: "founder", "leader" (a
        /// leader-line role), "long service" (any role ≥ 10 years), "great stories" (three at ≥ 0.7), "relic", "pinned" (the
        /// player's pin for a day), "eldest". Empty = not a legend. The book's cap of ~10 a box is the book's to apply.</summary>
        public List<string> LegendReasons(int notableId)
        {
            var list = new List<string>();
            var n = FindNotable(notableId);
            if (n == null) return list;
            if (n.parentId < 0 && n.bornMonth <= 0) list.Add("founder");
            var terms = TermsOf(n.id).ToList();
            if (terms.Any(t => t.role == RoleType.Chief || t.role == RoleType.Headman || t.role == RoleType.Lord)) list.Add("leader");
            if (terms.Any(t => ((t.toMonth >= 0 ? t.toMonth : (n.alive ? State.month : n.diedMonth)) - t.fromMonth) >= LegendRoleYears * SimConfig.MonthsPerYear))
                list.Add("long service");
            if (State.chronicle.Count(e => About(e, n.id) && e.salience >= LegendStorySalience && e.kind != "death") >= 3) list.Add("great stories");
            if (FoundRelics.Any(r => r.ownerId == n.id)) list.Add("relic");
            if (PinnedLongEnough(n.id)) list.Add("pinned");
            var oldest = State.notables.OrderByDescending(x => x.age).ThenBy(x => x.id).FirstOrDefault();
            if (oldest == n && n.age >= Config.eldestStoryAge) list.Add("eldest");
            return list;
        }
    }
}
