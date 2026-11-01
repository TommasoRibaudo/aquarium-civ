using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>The kinds of page in the small book (chronicle-and-history.md §4.1).</summary>
    public enum BookPageKind { Cover, Founding, Era, Legend, Family, Relics, End }

    /// <summary>Someone named on a page: a tap opens their card.</summary>
    public sealed class BookPerson
    {
        public int id = -1;
        public string name = "";
        public string note = "";        // "first chief", "partner"
        public bool legend;             // has a page of their own
        public bool alive;
    }

    /// <summary>A story on a page: a tap is Show me.</summary>
    public sealed class BookStory
    {
        public int entryId;
        public int year;
        public string text = "";
    }

    /// <summary>A relic on the relics page: found, with the people's label; or a silhouette with a hint in their words (C8).</summary>
    public sealed class BookRelic
    {
        public string id, name, label, hint;
        public bool found;
        public int ownerId = -1;
        public string ownerName = "";
    }

    /// <summary>One family: both parents always shown (P4's polish), so nobody reads as a half-sibling.</summary>
    public sealed class BookFamily
    {
        public List<BookPerson> parents = new();  // the main person's own parents: none, one or both
        public BookPerson main, partner;
        public List<BookPerson> children = new(); // legends first, then by age, at most SimCore.BookTreeChildren
    }

    /// <summary>One page, ready to draw. Not saved: built from the sim on demand. <see cref="key"/> is stable, for the ribbon.</summary>
    public sealed class BookPage
    {
        public BookPageKind kind;
        public string key;
        public string title = "", sub = "", line = "", line2 = "", note = "";
        public Era era;
        public int stillEra = -1;       // the era whose still shows this page (-1 = the newest still)
        public bool isNew;
        public List<BookPerson> people = new();
        public List<BookStory> stories = new();
        public List<BookRelic> relics = new();
        public PersonCard person;       // a legend's page
        public BookFamily family;
    }

    /// <summary>
    /// The small book's pages (#80, chronicle-and-history.md §4.1): the short rule of what earns a page, and what each one says. Pure
    /// reads, except <see cref="BookMarkSeen"/>, which remembers what the player has opened. No dice.
    /// <list type="bullet">
    /// <item><b>Pages never change after the fact:</b> a closed era keeps its stored title and its events (book-worthy entries are never
    /// pruned), the town's printed line says the year the era ended or began, never today's, and a legend's page once seen stays
    /// (<see cref="SimState.bookKept"/>), past the cap if it must.</item>
    /// <item><b>The cap:</b> about ten legends, by legend score. Pages the player has read stay; a new legend joins past ten only if they
    /// outscore the lowest of those, up to <see cref="BookLegendCeiling"/>.</item>
    /// </list>
    /// </summary>
    public partial class SimCore
    {
        public const int BookLegendCap = 10, BookLegendCeiling = 14, BookFamilyCap = 4, BookTreeChildren = 8, BookStoriesOnALegend = 3;
        /// <summary>The painted camp's page keeps to its picture, its caption and one story.</summary>
        public const int BookCampStories = 1;

        /// <summary>Debug (`earn every book page`): the end page shows without finishing the box. Not saved.</summary>
        public bool DebugBookEnd { get; set; }

        /// <summary>The whole book as it stands, in reading order: cover, founding, each era followed by the legends who rose in it, the
        /// family trees, the relics, the end. About 12–20 pages a box.</summary>
        public List<BookPage> BookPages()
        {
            var s = State;
            var legends = BookLegends();
            var ids = new HashSet<int>(legends.Select(n => n.id));
            var pages = new List<BookPage> { BookCover(), BookFounding(ids) };
            var chapters = BookChapters();
            foreach (var c in chapters)
            {
                pages.Add(BookEra(c));
                foreach (var n in legends.Where(n => LegendEra(n, chapters) == c.era).OrderBy(n => n.bornMonth).ThenBy(n => n.id))
                    pages.Add(BookLegend(n, ids));
            }
            pages.AddRange(BookFamilies(legends, ids));
            if (FoundRelics.Any()) pages.Add(BookRelics());
            if (s.boxComplete || DebugBookEnd) pages.Add(BookEnd());
            var seen = s.bookSeen ?? new List<string>();
            // What the box began with isn't news: nothing wears the ribbon in the first year, and the cover and the founding never do.
            bool settled = s.month >= SimConfig.MonthsPerYear;
            foreach (var p in pages) p.isNew = settled && p.kind != BookPageKind.Cover && p.kind != BookPageKind.Founding && !seen.Contains(p.key);
            return pages;
        }

        /// <summary>The ribbon: a page earned that the player hasn't opened.</summary>
        public bool BookHasNewPages => BookPages().Any(p => p.isNew);

        /// <summary>The player opened this page: it loses its ribbon, and a legend's page stays in the book from now on.</summary>
        public void BookMarkSeen(BookPage page)
        {
            if (page == null) return;
            var s = State;
            s.bookSeen ??= new List<string>();
            s.bookKept ??= new List<int>();
            if (!s.bookSeen.Contains(page.key)) s.bookSeen.Add(page.key);
            if (page.kind == BookPageKind.Legend && page.person != null && !s.bookKept.Contains(page.person.id)) s.bookKept.Add(page.person.id);
        }

        /// <summary>A person's page, or null if they have none.</summary>
        public static string BookLegendKey(int notableId) => "legend:" + notableId;

        /// <summary>The sum of their top five saliences (§4.1).</summary>
        public float LegendScore(int notableId) => State.chronicle.Where(e => About(e, notableId)).Select(e => e.salience).OrderByDescending(x => x).Take(5).Sum();

        /// <summary>Who has a page: the kept ones, then the best of the rest up to the cap; past it, only someone better than the lowest kept.</summary>
        public List<Notable> BookLegends()
        {
            var s = State;
            var kept = s.bookKept ?? new List<int>();
            var scored = s.notables.Where(n => kept.Contains(n.id) || LegendReasons(n.id).Count > 0)
                .Select(n => (n, score: LegendScore(n.id))).OrderByDescending(x => x.score).ThenBy(x => x.n.id).ToList();
            var chosen = scored.Where(x => kept.Contains(x.n.id)).ToList();
            float lowest = chosen.Count > 0 ? chosen.Min(x => x.score) : float.MaxValue;
            foreach (var x in scored.Where(x => !kept.Contains(x.n.id)))
            {
                if (chosen.Count >= BookLegendCeiling) break;
                if (chosen.Count < BookLegendCap || x.score > lowest) chosen.Add(x);
            }
            return chosen.Select(x => x.n).ToList();
        }

        Era LegendEra(Notable n, List<BookChapter> chapters)
        {
            long rose = n.bornMonth;
            var first = TermsOf(n.id).OrderBy(t => t.fromMonth).FirstOrDefault();
            if (first != null) rose = Math.Max(rose, first.fromMonth);
            var c = chapters.LastOrDefault(x => x.fromMonth <= rose) ?? chapters[0];
            return c.era;
        }

        BookPerson BookPersonOf(int id, string note, HashSet<int> legends)
        {
            var n = FindNotable(id);
            return n == null ? null : new BookPerson { id = id, name = n.FullName, note = note ?? "", legend = legends != null && legends.Contains(id), alive = n.alive };
        }

        BookStory BookStoryOf(ChronicleEntry e) => new() { entryId = e.id, year = YearNo(e.month), text = BookLine(e) };

        /// <summary>The headline, or the whole line where the headline had to be cut: a book has room, and a cut sentence reads badly on paper.</summary>
        string BookLine(ChronicleEntry e)
        {
            string h = Headline(e);
            return h.EndsWith("…") || h.EndsWith("...") ? e.text : h;
        }

        /// <summary>A role-holder's death is two entries with one headline: one line in the book.</summary>
        static List<BookStory> Once(IEnumerable<BookStory> stories) => stories.GroupBy(x => x.text).Select(g => g.First()).ToList();

        static string Capital(string word) => string.IsNullOrEmpty(word) ? "" : char.ToUpperInvariant(word[0]) + word.Substring(1);

        static string Listed(IList<string> names) =>
            names.Count == 0 ? "" : names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];

        BookPage BookCover() => new()
        {
            kind = BookPageKind.Cover, key = "cover", title = Place, sub = $"Years 1–{YearNo(State.month)}", era = State.era,
        };

        BookPage BookFounding(HashSet<int> legends)
        {
            var s = State;
            var first = s.chronicle.OrderBy(e => e.month).ThenBy(e => e.id).FirstOrDefault(e => e.kind == "founding" || e.kind == "founders");
            var p = new BookPage
            {
                kind = BookPageKind.Founding, key = "founding", title = "The founding",
                line = first != null ? first.text : "A few wanderers settled in the glass and made camp.",
            };
            int chief = s.leaders.Count > 0 ? s.leaders[0].notableId : HolderOf(RoleType.Chief)?.id ?? -1;
            foreach (var n in s.notables.Where(n => n.parentId < 0 && n.bornMonth <= 0).OrderBy(n => n.id == chief ? 0 : 1).ThenBy(n => n.id))
                p.people.Add(BookPersonOf(n.id, n.id == chief ? "first chief" : "", legends));
            return p;
        }

        BookPage BookEra(BookChapter c)
        {
            string noun = EraNoun(c.era);
            bool closed = c.toYear >= 0;
            var leader = c.leaders.FirstOrDefault();
            string role = LeaderOf(c.era);
            var p = new BookPage
            {
                kind = BookPageKind.Era, key = "era:" + (int)c.era, era = c.era,
                title = !string.IsNullOrEmpty(c.eraTitle) ? c.eraTitle : $"The {noun} years",
                sub = closed ? $"Years {c.fromYear}–{c.toYear}" : $"From year {c.fromYear}",
                stillEra = closed ? (int)c.era + 1 : (int)c.era,
            };
            // The look of the era is words too (§5.3): the painted camp has one caption, the priest writes, the town prints.
            p.line = c.era switch
            {
                Era.Camp => "",
                Era.Village => leader != null ? $"In the time of {role} {leader.name}." : "",
                _ => $"Chronicle of {Place}, printed in the year {(closed ? c.toYear : c.fromYear)}, by order of the {role}.",
            };
            p.stories = Once(c.events.Select(BookStoryOf)).Take(c.era == Era.Camp ? BookCampStories : c.events.Count).ToList();
            if (c.era != Era.Camp) p.people = c.leaders.Select(n => BookPersonOf(n.id, role, null)).Where(x => x != null).ToList();
            return p;
        }

        BookPage BookLegend(Notable n, HashSet<int> legends)
        {
            var c = PersonCardOf(n.id);
            var work = new List<string>();
            if (!string.IsNullOrEmpty(c.job)) work.Add(Capital(c.job));
            foreach (var r in c.terms.Select(t => t.role).Distinct()) work.Add(work.Count == 0 ? Capital(r) : r);
            string span = n.alive ? $"born {c.bornYear}" : $"{c.bornYear}–{c.diedYear}";
            var parts = new List<string>();
            if (work.Count > 0) parts.Add(string.Join(", ", work));
            parts.Add(span);
            if (!string.IsNullOrEmpty(c.trait)) parts.Add(c.trait); // the board's epithet, "the joker": the traits are worded so
            var p = new BookPage
            {
                kind = BookPageKind.Legend, key = BookLegendKey(n.id), title = c.fullName, sub = string.Join(" · ", parts), person = c,
            };
            var reasons = n.alive ? LegendReasons(n.id) : c.memorial.legendReasons;
            string why = reasons.Count > 0 ? $"A legend of the box: {string.Join(", ", reasons)}." : "A legend of the box.";
            if (n.alive) p.line = $"Aged {c.age}, still among them.";
            else
            {
                p.line = $"Died in year {c.diedYear}, aged {c.age}{(c.memorial.diedInOffice ? ", still in office" : "")}.";
                var by = c.memorial.rememberedBy.Select(t => FirstName(t.otherName)).Take(3).ToList();
                if (by.Count > 0) why += $" Remembered by {Listed(by)}.";
            }
            p.line2 = why;
            // The page already says how it ended.
            p.stories = Once(c.bestStories.Where(x => x.kind != "death" && x.kind != "role_death").Select(x =>
            {
                var e = State.chronicle.Find(y => y.id == x.entryId);
                return new BookStory { entryId = x.entryId, year = x.year, text = e != null ? BookLine(e) : x.headline };
            })).Take(BookStoriesOnALegend).ToList();
            // Partner first; someone who's both a child and a friend is listed once.
            foreach (var t in c.family.Where(t => t.kind == TieKind.Partner).Concat(c.family.Where(t => t.kind == TieKind.Child))
                         .Concat(c.friends.Where(t => t.kind == TieKind.Friend)).Concat(c.rivals).GroupBy(t => t.otherId).Select(g => g.First()))
                if (BookPersonOf(t.otherId, t.label.Replace("late ", ""), legends) is BookPerson who) p.people.Add(who); // the portrait's colour says who's gone
            if (c.relics.Count > 0) p.note = "Keeper of the " + Listed(c.relics.Select(r => r.name).ToList()) + ".";
            return p;
        }

        static string FirstName(string full) => string.IsNullOrEmpty(full) ? "" : full.Split(' ')[0];

        /// <summary>A family with a legend in it, the family's best legend at the middle of the tree. The families with the most legends
        /// first, at most <see cref="BookFamilyCap"/>; someone with no family to draw earns no tree.</summary>
        IEnumerable<BookPage> BookFamilies(List<Notable> legends, HashSet<int> ids)
        {
            var groups = legends.GroupBy(n => n.family).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Max(n => LegendScore(n.id))).ThenBy(g => g.Key);
            int made = 0;
            foreach (var g in groups)
            {
                if (made >= BookFamilyCap) yield break;
                foreach (var n in g.OrderByDescending(x => LegendScore(x.id)).ThenBy(x => x.id))
                {
                    var f = BookFamilyOf(n, ids);
                    if (f == null) continue;
                    yield return new BookPage { kind = BookPageKind.Family, key = "family:" + g.Key, title = $"The {g.Key} family", family = f };
                    made++;
                    break;
                }
            }
        }

        BookFamily BookFamilyOf(Notable n, HashSet<int> legends)
        {
            var c = PersonCardOf(n.id);
            var f = new BookFamily { main = BookPersonOf(n.id, "", legends) };
            var partner = c.family.FirstOrDefault(t => t.kind == TieKind.Partner);
            if (partner != null) f.partner = BookPersonOf(partner.otherId, "", legends);
            f.parents = c.family.Where(t => t.kind == TieKind.Parent).OrderBy(t => t.otherId).Take(2).Select(t => BookPersonOf(t.otherId, "", legends)).Where(x => x != null).ToList();
            f.children = c.family.Where(t => t.kind == TieKind.Child).Select(t => BookPersonOf(t.otherId, "", legends)).Where(x => x != null)
                .OrderBy(x => x.legend ? 0 : 1).ThenBy(x => FindNotable(x.id).bornMonth).ThenBy(x => x.id).Take(BookTreeChildren).ToList();
            return f.partner == null && f.parents.Count == 0 && f.children.Count == 0 ? null : f;
        }

        BookPage BookRelics()
        {
            var p = new BookPage { kind = BookPageKind.Relics, key = "relics", title = "Relics" };
            var found = FoundRelics.ToList();
            foreach (var r in found)
                p.relics.Add(new BookRelic { id = r.id, name = RelicName(r.id), label = RelicLabel(r.id), found = true, ownerId = r.ownerId, ownerName = FindNotable(r.ownerId)?.FullName ?? "" });
            foreach (var id in RelicId.All.Where(id => !found.Any(r => r.id == id)))
                p.relics.Add(new BookRelic { id = id, name = "????", hint = RelicHint(id) });
            return p;
        }

        BookPage BookEnd()
        {
            var s = State;
            int year = s.boxComplete ? YearNo(s.boxCompleteMonth) : YearNo(s.month);
            var left = s.chronicle.LastOrDefault(e => e.kind == "founders_left");
            var carried = FoundRelics.Where(r => r.takenMonth >= 0).Select(r => r.id).Select(RelicName).ToList();
            var p = new BookPage
            {
                kind = BookPageKind.End, key = "end", title = "The end",
                sub = $"Years 1–{year}",
                line = left != null ? left.text : $"{Place} began with a fire and a hut. We remember.",
                line2 = carried.Count > 0 ? $"In the time capsule: the {Listed(carried)}." : "It was a good glass to live in.",
            };
            foreach (var id in s.founderIds ?? new List<int>())
                if (BookPersonOf(id, "carried on", null) is BookPerson who) p.people.Add(who);
            return p;
        }

        // ---------- debug ----------
        /// <summary>Debug (`earn every book page`): the three eras, a found relic or two (the rest stay silhouettes), the end page.</summary>
        public void DebugEarnBook()
        {
            while (!IsFinalEra) ForceNextEra();
            DebugGiveRelic(RelicId.SkyCrumb);
            DebugGiveRelic(RelicId.KeeperDoll);
            DebugBookEnd = true;
        }

        /// <summary>Debug (force each teaser kind): opens a thread of this <see cref="ThreadSource"/> kind with the kind's line, whether or not
        /// the sim would say it now, and makes it the latest teaser. It changes nothing else, so it needn't be true of the box. Null if
        /// the kind has nobody to name, say an office with no holder whose trait has a line.</summary>
        public string DebugForceTeaser(string source)
        {
            var s = State;
            var adults = s.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.id).ToList();
            switch (source)
            {
                case ThreadSource.Era:
                {
                    var t = NewThread(ThreadSource.Era, s.era == Era.Camp ? "The elders talk about planting seeds and staying put." : "The craftsfolk talk of guilds, and of a bigger market.", -1, -1);
                    s.teaserThreadId = t.id;
                    return t.teaser;
                }
                case ThreadSource.Seat:
                {
                    var chief = HolderOf(RoleType.Chief) ?? HolderOf(LeaderOf(s.era));
                    var rival = adults.FirstOrDefault(n => n != chief && CanStand(n)) ?? adults.FirstOrDefault(n => n != chief);
                    if (chief == null || rival == null) return null;
                    var t = NewThread(ThreadSource.Seat, $"{rival.FullName} has started speaking at the {SeatLabel(LeaderOf(s.era))}. The chief has noticed.", rival.id, chief.id);
                    t.role = RoleType.Chief;
                    s.teaserThreadId = t.id;
                    return t.teaser;
                }
                case ThreadSource.Couple:
                {
                    if (adults.Count < 2) return null;
                    var t = NewThread(ThreadSource.Couple, $"{adults[0].FullName} and {adults[1].FullName} keep sitting together at the {SeatLabel(LeaderOf(s.era))}.", adults[0].id, adults[1].id);
                    s.teaserThreadId = t.id;
                    return t.teaser;
                }
                case ThreadSource.Gift:
                {
                    var n = PlaceNudge(NudgeKind.Rain, 0f, 0f); // a gift's thread ends with its gift: a real one, unanswered
                    if (n == null) return null;
                    var t = NewThread(ThreadSource.Gift, s.era == Era.Camp ? "The gatherers keep checking the wet ground." : "The farmers keep checking the wet ground.", -1, -1);
                    t.subjectId = n.id;
                    s.teaserThreadId = t.id;
                    return t.teaser;
                }
                case ThreadSource.Rivalry:
                {
                    if (adults.Count < 2) return null;
                    var t = NewThread(ThreadSource.Rivalry, $"{adults[0].FullName} and {adults[1].FullName} aren't speaking again.", adults[0].id, adults[1].id);
                    s.teaserThreadId = t.id;
                    return t.teaser;
                }
                case ThreadSource.Office:
                {
                    string line = OfficeTeaser(out var holder);
                    if (line == null) return null;
                    var t = NewThread(ThreadSource.Office, line, holder.id, -1);
                    s.teaserThreadId = t.id;
                    return t.teaser;
                }
                case ThreadSource.Slice:
                {
                    var t = NewThread(ThreadSource.Slice, "The kids are building a raft.", -1, -1);
                    s.teaserThreadId = t.id;
                    return t.teaser;
                }
            }
            return null;
        }
    }
}
