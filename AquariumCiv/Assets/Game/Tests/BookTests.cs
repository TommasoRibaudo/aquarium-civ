using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The small book's pages (#80, chronicle-and-history.md §4.1), and the debug force for each teaser kind (#77).</summary>
    public class BookTests
    {
        /// <summary>A box that lived <paramref name="years"/>, crossing each era as it comes.</summary>
        static SimCore Lived(int years, int seed = 5)
        {
            var sim = NewSim(seed);
            for (int y = 0; y < years; y++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            return sim;
        }

        static IEnumerable<BookPage> Of(List<BookPage> pages, BookPageKind kind) => pages.Where(p => p.kind == kind);

        [Test]
        public void ANewBoxHasACoverAndAFoundingAndNothingWaits()
        {
            var sim = NewSim(5);
            var pages = sim.BookPages();
            Assert.AreEqual(BookPageKind.Cover, pages[0].kind);
            Assert.AreEqual(BookPageKind.Founding, pages[1].kind);
            Assert.That(pages[1].people, Is.Not.Empty, "the founders");
            Assert.AreEqual("first chief", pages[1].people[0].note);
            Assert.IsFalse(Of(pages, BookPageKind.Relics).Any(), "the relics page waits for the first relic");
            Assert.IsFalse(Of(pages, BookPageKind.End).Any(), "the end waits for the box");
            Assert.IsFalse(pages.Where(p => p.kind == BookPageKind.Cover || p.kind == BookPageKind.Founding).Any(p => p.isNew), "no ribbon for what was always there");
        }

        [Test]
        public void AnOldBoxReadsAsAShortBookWithOneSpreadPerEra()
        {
            var sim = Lived(120);
            var pages = sim.BookPages();
            Assert.AreEqual(Era.Town, sim.State.era);
            var eras = Of(pages, BookPageKind.Era).ToList();
            Assert.AreEqual(new[] { Era.Camp, Era.Village, Era.Town }, eras.Select(p => p.era).ToArray());
            Assert.That(Of(pages, BookPageKind.Legend).Count(), Is.InRange(1, SimCore.BookLegendCap));
            Assert.That(Of(pages, BookPageKind.Family).Count(), Is.InRange(1, SimCore.BookFamilyCap));
            Assert.That(pages.Count, Is.InRange(8, 22), string.Join(" | ", pages.Select(p => p.title)));
            Assert.AreEqual(pages.Count, pages.Select(p => p.key).Distinct().Count(), "a page's key is its own");
        }

        [Test]
        public void TheFamilyTreeShowsBothParentsOfTheChildren()
        {
            var sim = Lived(120);
            var f = Of(sim.BookPages(), BookPageKind.Family).Select(p => p.family).FirstOrDefault(x => x.children.Count > 0 && x.partner != null);
            Assert.NotNull(f, "a couple with children");
            foreach (var c in f.children) Assert.That(sim.PersonCardOf(c.id).family.Where(t => t.kind == TieKind.Parent).Select(t => t.otherId), Does.Contain(f.main.id));
        }

        [Test]
        public void AClosedEraNeverChangesAndTheTownPrintsItsOwnYear()
        {
            var sim = Lived(70);
            Assert.GreaterOrEqual((int)sim.State.era, 1);
            var before = Of(sim.BookPages(), BookPageKind.Era).First();
            string town = null;
            for (int y = 0; y < 60; y++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
                town ??= Of(sim.BookPages(), BookPageKind.Era).FirstOrDefault(p => p.era == Era.Town)?.line;
            }
            var after = Of(sim.BookPages(), BookPageKind.Era).First();
            Assert.AreEqual(before.title, after.title);
            Assert.AreEqual(before.sub, after.sub);
            Assert.AreEqual(before.stories.Select(s => s.text).ToArray(), after.stories.Select(s => s.text).ToArray());
            Assert.NotNull(town, "the box reached the town");
            Assert.AreEqual(town, Of(sim.BookPages(), BookPageKind.Era).First(p => p.era == Era.Town).line, "printed once, in the year it says");
        }

        [Test]
        public void TheCapFallsOnTheLowestScoresAndAReadPageStays()
        {
            var sim = Lived(120);
            foreach (var n in sim.State.notables) sim.State.bookKept.Remove(n.id);
            var all = sim.State.notables.Where(n => sim.LegendReasons(n.id).Count > 0).OrderByDescending(n => sim.LegendScore(n.id)).ThenBy(n => n.id).ToList();
            Assume.That(all.Count, Is.GreaterThan(SimCore.BookLegendCap), "enough legends to reach the cap");
            var legends = sim.BookLegends();
            Assert.AreEqual(SimCore.BookLegendCap, legends.Count);
            CollectionAssert.AreEquivalent(all.Take(SimCore.BookLegendCap).Select(n => n.id), legends.Select(n => n.id));

            var low = all.Last();
            sim.State.bookKept.Add(low.id);
            Assert.That(sim.BookLegends().Select(n => n.id), Does.Contain(low.id), "a page once read stays in the book");
            Assert.That(sim.BookLegends().Count, Is.LessThanOrEqualTo(SimCore.BookLegendCeiling));
        }

        [Test]
        public void ReadingAPageTakesOffItsRibbon()
        {
            var sim = NewSim(5);
            Assert.IsFalse(sim.BookHasNewPages, "what the box began with isn't news");
            sim.Advance(2);
            foreach (var p in sim.BookPages()) sim.BookMarkSeen(p);
            Assert.IsFalse(sim.BookHasNewPages);
            sim.DebugGiveRelic(RelicId.SkyCrumb);
            Assert.IsTrue(sim.BookHasNewPages, "the first relic earns a page");
            var relics = sim.BookPages().Single(p => p.kind == BookPageKind.Relics);
            Assert.IsTrue(relics.isNew);
            sim.BookMarkSeen(relics);
            Assert.IsFalse(sim.BookPages().Single(p => p.kind == BookPageKind.Relics).isNew);
        }

        [Test]
        public void UnfoundRelicsAreSilhouettesWithTheirHints()
        {
            var sim = NewSim(5);
            sim.DebugGiveRelic(RelicId.SkyCrumb);
            var page = sim.BookPages().Single(p => p.kind == BookPageKind.Relics);
            Assert.AreEqual(RelicId.All.Length, page.relics.Count);
            var found = page.relics[0];
            Assert.IsTrue(found.found);
            Assert.AreEqual("Sky Crumb", found.name);
            Assert.That(found.label, Is.Not.Empty);
            foreach (var r in page.relics.Skip(1))
            {
                Assert.IsFalse(r.found);
                Assert.AreEqual("????", r.name, "the name stays hidden");
                Assert.AreEqual(sim.RelicHint(r.id), r.hint);
            }
            Assert.AreEqual("If only we could see further than the wall.", page.relics.Single(r => r.id == RelicId.Lens).hint);
        }

        [Test]
        public void EarningEveryPageReachesEveryKind()
        {
            var sim = Lived(30);
            sim.DebugEarnBook();
            var kinds = sim.BookPages().Select(p => p.kind).Distinct().ToList();
            foreach (var k in new[] { BookPageKind.Cover, BookPageKind.Founding, BookPageKind.Era, BookPageKind.Legend, BookPageKind.Family, BookPageKind.Relics, BookPageKind.End })
                Assert.That(kinds, Does.Contain(k));
            Assert.IsFalse(sim.State.boxComplete, "the end shows without finishing the box");
        }

        [Test]
        public void AFinishedBoxEndsOnWhoCarriedOn()
        {
            var sim = Lived(120);
            Assert.NotNull(sim.ForceCompleteBox());
            var end = sim.BookPages().Last();
            Assert.AreEqual(BookPageKind.End, end.kind);
            StringAssert.Contains("Years 1–", end.sub);
            StringAssert.Contains("left", end.line);
            Assert.That(end.people, Is.Not.Empty);
        }

        [Test]
        public void TheBookDrawsNoDice()
        {
            var a = Lived(40);
            var b = Lived(40);
            a.BookPages();
            a.BookMarkSeen(a.BookPages().Last());
            for (int i = 0; i < 24; i++) { a.Step(); b.Step(); }
            Assert.AreEqual(b.State.population, a.State.population);
            Assert.AreEqual(b.State.chronicle.Count, a.State.chronicle.Count);
        }

        [TestCase(ThreadSource.Era)]
        [TestCase(ThreadSource.Seat)]
        [TestCase(ThreadSource.Couple)]
        [TestCase(ThreadSource.Gift)]
        [TestCase(ThreadSource.Rivalry)]
        [TestCase(ThreadSource.Slice)]
        public void EachTeaserKindCanBeForced(string kind)
        {
            var sim = NewSim(5);
            string line = sim.DebugForceTeaser(kind);
            Assert.That(line, Is.Not.Empty);
            Assert.AreEqual(line, sim.CurrentTeaser, "the peek has it");
            Assert.AreEqual(kind, sim.State.threads.Single(t => t.id == sim.State.teaserThreadId).source);
        }
    }
}
