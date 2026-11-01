using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#80: the small book by touch on the real scene: the ribbon, swipe and buttons, each page kind, the person link, Show me.</summary>
    public class BookPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BookView Book => BookView.Instance;

        IEnumerator Earned()
        {
            yield return OpenSlice(seed: Seed);
            Assert.That(BookView.DebugEarnAll(), Does.Contain("book pages"));
            Sim.CleanAll();
            Sim.CardsSeenToNow();
            CardStackView.Instance.Model?.HidePeek();
            yield return Frames(10);
        }

        IEnumerator Tap(string name)
        {
            yield return TapAt(GameUiPanel.ScreenCenter(Book.Root.Q(name)));
            yield return Frames(6);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator EveryPageKindDrawsAndTheButtonsTurn()
        {
            yield return Earned();
            Assert.IsTrue(Book.Open());
            yield return Frames(6);
            var kinds = Book.Pages.Select(p => p.kind).Distinct().ToList();
            Assert.AreEqual(7, kinds.Count, string.Join(",", kinds));
            for (int i = 0; i < Book.Pages.Count; i++)
            {
                Book.GoTo(i);
                yield return Frames(3);
                var e = Book.PageElement;
                Assert.NotNull(e);
                Assert.AreEqual("book-page-" + Book.Page.kind.ToString().ToLowerInvariant(), e.name);
                Assert.Greater(e.Query<Label>().ToList().Count(l => !string.IsNullOrEmpty(l.text)), 0, "words on " + Book.Page.key);
                if (Book.Page.kind == BookPageKind.Family) Assert.Greater(e.Query(className: "book-tree-line").ToList().Count, 0, "the tree has lines (Q21)");
                if (Book.Page.kind == BookPageKind.Relics)
                {
                    var q = e.Query<GlassPortrait>().ToList().FirstOrDefault(p => p.text == "?");
                    Assert.NotNull(q, "an unfound relic's ?");
                    Assert.AreNotEqual(q.resolvedStyle.backgroundColor, q.resolvedStyle.color, "paper on dark: the ? shows (Q21)");
                }
            }
            Book.GoTo(0);
            yield return Frames(3);
            Assert.AreEqual(Visibility.Hidden, Book.Root.Q("book-prev").resolvedStyle.visibility);
            yield return Tap("book-next");
            Assert.AreEqual(1, Book.At);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Tap("book-prev");
            Assert.AreEqual(0, Book.At);
            yield return Tap("book-close");
            Assert.IsFalse(Book.IsOpen);
            AssertNoErrors("the book's pages");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ASwipeTurnsAndATapDoesNot()
        {
            yield return Earned();
            Assert.IsTrue(Book.Open());
            yield return Frames(6);
            float w = Screen.width, h = Screen.height;
            yield return Drag(new[] { new Vector2(w * 0.8f, h * 0.5f), new Vector2(w * 0.5f, h * 0.5f), new Vector2(w * 0.2f, h * 0.5f) }, 0.05f);
            yield return Frames(4);
            Assert.AreEqual(1, Book.At, "a swipe left is the next page");
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Drag(new[] { new Vector2(w * 0.2f, h * 0.5f), new Vector2(w * 0.5f, h * 0.5f), new Vector2(w * 0.8f, h * 0.5f) }, 0.05f);
            yield return Frames(4);
            Assert.AreEqual(0, Book.At, "a swipe right goes back");
            Assert.AreEqual(0, BookView.SwipeTurn(new Vector2(40f, 400f), w), "a scroll isn't a swipe");
            Assert.AreEqual(0, BookView.SwipeTurn(new Vector2(4f, 0f), w), "nor is a tap");
            Book.Close();
            AssertNoErrors("swiping");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheRibbonComesWithANewPageAndGoesWhenRead()
        {
            yield return Earned();
            var screen = TimeLapseScreenView.Ensure();
            Assert.IsTrue(screen.Open());
            yield return Frames(10);
            Assert.IsTrue(TimeLapseScreenView.BookRibbon, "new pages wait");
            Assert.AreEqual(DisplayStyle.Flex, screen.Root.Q("tl-book-ribbon").resolvedStyle.display);
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root.Q("tl-book")));
            yield return Frames(6);
            Assert.IsTrue(Book.IsOpen, "the Book button opens it");
            int first = Book.Pages.FindIndex(p => p.isNew);
            for (int i = 0; i < Book.Pages.Count; i++)
            {
                Book.GoTo(i);
                yield return null;
                if (i == first) Assert.NotNull(Book.PageElement.Q("book-ribbon"), "the page wears it too");
            }
            Book.Close();
            yield return Frames(4);
            Assert.IsFalse(TimeLapseScreenView.BookRibbon, "read");
            Assert.AreEqual(DisplayStyle.None, screen.Root.Q("tl-book-ribbon").resolvedStyle.display);
            screen.Close();
            Assert.IsFalse(Sim.BookHasNewPages);
            yield return Frames(6);
            AssertNoErrors("the ribbon");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator APortraitOpensTheCardAndAStoryIsShowMe()
        {
            yield return Earned();
            Assert.IsTrue(Book.Open());
            int legend = Book.Pages.FindIndex(p => p.kind == BookPageKind.Legend);
            Book.GoTo(legend);
            yield return Frames(6);
            int who = Book.Page.person.id;
            yield return TapAt(GameUiPanel.ScreenCenter(Book.PageElement.Q("book-person-" + who)));
            yield return Frames(8);
            Assert.AreEqual(who, PersonCardView.Instance.OpenId, "the person's card, over the page");
            Assert.IsTrue(Book.IsOpen);
            PersonCardView.Instance.Close();
            yield return Frames(6);

            int era = Book.Pages.FindIndex(p => p.kind == BookPageKind.Era && p.stories.Count > 0);
            Assert.GreaterOrEqual(era, 0);
            Book.GoTo(era);
            yield return Frames(6);
            var story = Book.Page.stories[0];
            yield return TapAt(GameUiPanel.ScreenCenter(Book.PageElement.Q("book-story-" + story.entryId)));
            yield return Frames(8);
            Assert.IsFalse(Book.IsOpen, "out of the book");
            var show = ShowMeDirector.Instance;
            Assert.IsTrue(show.Busy, "Show me runs on the live box");
            show.End();
            yield return Frames(6);
            AssertNoErrors("the links");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RememberOpensTheLegendsPage()
        {
            yield return Earned();
            var legend = Sim.BookLegends().First(n => n.alive);
            Assert.IsTrue(Book.OpenAtLegend(legend.id));
            Assert.AreEqual(SimCore.BookLegendKey(legend.id), Book.Page.key);
            Book.Close();
            Assert.IsFalse(Book.OpenAtLegend(int.MaxValue), "someone with no page falls back to their card");
            yield return Frames(4);
            AssertNoErrors("remember");
        }
    }
}
