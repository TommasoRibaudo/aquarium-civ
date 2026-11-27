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
    /// <summary>#774: the card's Family tab links to the family tree (tree A) in the book: every face drawn, the dead kept, the person
    /// ringed and in view, a drag scrolls the tree and never turns the page, a knot folds a branch to a chip and the chip opens it, a
    /// face opens that card. Shots at both phone sizes: Temp/agent/tree774-*.png and pc774-*.png.</summary>
    public class FamilyTreePlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BookView Book => BookView.Instance;

        IEnumerator Family(int w, int h)
        {
            yield return OpenSlice(w, h, seed: Seed);
            GameUiPanel.AcceptInputUnfocused(true);
            SimLayoutSource.DebugSkipYears(30f);
            yield return Frames(4);
            Assert.GreaterOrEqual(Sim.DebugDeepFamily(3), 0, "a founder to grow a family from");
            Sim.CleanAll();
            Sim.CardsSeenToNow();
            CardStackView.Instance.Model?.HidePeek();
            yield return Frames(10);
        }

        IEnumerator TreeFromTheCard(int w, int h)
        {
            yield return Family(w, h);
            try
            {
                var sim = Sim;
                int root = sim.FamilyTreeRoots().First();
                var full = sim.FamilyTreeOf(root);
                Assert.GreaterOrEqual(full.rows, 4, "three more generations under the founders");
                Assert.IsTrue(full.nodes.Any(n => !n.alive), "someone in it has died");
                int who = full.nodes.Where(n => n.alive && n.blood && n.gen >= 2).OrderByDescending(n => n.gen).First().id;

                // the card: Family first, the link at its end
                var cards = PersonCardView.Instance;
                Assert.IsTrue(cards.Open(who, false));
                cards.SetExpanded(true);
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.AreEqual(CardTab.Family, cards.Tab);
                Assert.IsNotNull(cards.CardRoot.Q("person-card-parents"), "their parents as faces");
                yield return Shot($"pc774-family-{w}");
                var link = cards.CardRoot.Q<Button>("person-card-tree");
                Assert.NotNull(link, "See the whole line");
                cards.CardRoot.Q<ScrollView>("person-card-body").ScrollTo(link);
                yield return Frames(3);
                yield return TapAt(GameUiPanel.ScreenCenter(link));
                yield return Frames(8);
                Assert.IsTrue(Book.IsOpen && Book.Page.kind == BookPageKind.Family, "the link opens the tree page");
                Assert.IsFalse(cards.IsOpen, "one sheet at a time");
                Assert.AreEqual(who, Book.TreeFocus);
                yield return new WaitForSecondsRealtime(0.5f);

                // every face, the dead greyed with a stone, the person ringed and on screen
                var page = Book.PageElement;
                foreach (var n in full.nodes) Assert.NotNull(page.Q("book-person-" + n.id), "a face for " + n.fullName);
                Assert.AreEqual(full.nodes.Count(n => !n.alive), page.Query(className: "pc-stone").ToList().Count, "a stone for each of the dead");
                var ring = page.Q(className: "book-tree-here");
                Assert.NotNull(ring);
                var view = page.Q("book-tree-view").worldBound;
                Assert.IsTrue(view.Contains(ring.worldBound.center), "the person is scrolled into view");
                Assert.AreEqual(full.gens.Count, page.Query(className: "book-tree-gen").ToList().Count, "the gutter names each generation");
                yield return Shot($"tree774-open-{w}");

                // a drag on the tree scrolls it, the page stays
                int at = Book.At;
                var before = Book.TreeScroll.scrollOffset;
                yield return Drag(new[] { new Vector2(view.center.x / page.panel.visualTree.layout.width * Screen.width, Screen.height * 0.5f), new Vector2(Screen.width * 0.15f, Screen.height * 0.5f) }, Screen.width * 0.05f);
                yield return Frames(6);
                Assert.AreEqual(at, Book.At, "a drag on the tree never turns the page");
                Debug.Log($"[Tree774] scroll {before} -> {Book.TreeScroll.scrollOffset}");

                // fold a branch at its knot, open it again from the chip
                var branch = full.branches.First(b => b.ownerId != root && b.childX.Count > 0);
                var knot = page.Q("book-tree-knot-" + branch.ownerId);
                Assert.NotNull(knot);
                Book.TreeScroll.ScrollTo(knot);
                yield return Frames(3);
                yield return TapAt(GameUiPanel.ScreenCenter(knot));
                yield return Frames(6);
                Assert.IsTrue(Book.Folded.Contains(branch.ownerId), "the knot folds the branch");
                Assert.Less(Book.Tree.nodes.Count, full.nodes.Count);
                var chip = Book.PageElement.Q("book-tree-fold-" + branch.ownerId);
                Assert.NotNull(chip, "a chip where the branch was");
                StringAssert.Contains(" more", chip.Q<Label>().text);
                yield return Shot($"tree774-folded-{w}");
                Book.TreeScroll.ScrollTo(chip);
                yield return Frames(3);
                yield return TapAt(GameUiPanel.ScreenCenter(chip));
                yield return Frames(6);
                Assert.IsFalse(Book.Folded.Contains(branch.ownerId), "the chip opens it");
                Assert.AreEqual(full.nodes.Count, Book.Tree.nodes.Count);

                // a face opens that person's card over the page, and rings them
                var other = full.nodes.First(n => n.id != who && n.alive);
                var face = Book.PageElement.Q("book-person-" + other.id);
                Book.TreeScroll.ScrollTo(face);
                yield return Frames(3);
                yield return TapAt(GameUiPanel.ScreenCenter(face));
                yield return Frames(6);
                Assert.AreEqual(other.id, cards.OpenId, "a face opens their card");
                Assert.AreEqual(other.id, Book.TreeFocus);
                cards.Close();
                yield return Frames(4);

                // #804: the book's X goes back to the card the link was on, on its Family tab
                var x = Book.PageElement.panel.visualTree.Q("book-close");
                Assert.NotNull(x, "the book's X");
                yield return TapAt(GameUiPanel.ScreenCenter(x));
                yield return Frames(8);
                Assert.IsFalse(Book.IsOpen, "the X closes the book");
                Assert.AreEqual(who, cards.OpenId, "back to the card it came from");
                Assert.AreEqual(CardTab.Family, cards.Tab);
                yield return Shot($"pc804-back-{w}");
                cards.Close();
                yield return Frames(4);
                Assert.IsTrue(Book.OpenAtTree(root, who), "the tree again, opened without a card");
                yield return Frames(4);
                Book.Close();
                yield return Frames(4);
                Assert.IsFalse(cards.IsOpen, "with no card to go back to, the X lands on the box");
                AssertNoErrors("the family tree");
            }
            finally { GameUiPanel.AcceptInputUnfocused(false); }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TheTreeFromTheCardOnATallPhone() => TreeFromTheCard(1179, 2556);

        [UnityTest, Timeout(240000)]
        public IEnumerator TheTreeFromTheCardOnAShortPhone() => TreeFromTheCard(750, 1334);

        /// <summary>The card's three tabs at one size, for eyes (Temp/agent/pc774-*.png).</summary>
        IEnumerator CardTabs(int w, int h)
        {
            yield return Family(w, h);
            var sim = Sim;
            var full = sim.FamilyTreeOf(sim.FamilyTreeRoots().First());
            int who = full.nodes.Where(n => n.alive && n.blood && n.gen == 2).Select(n => n.id).DefaultIfEmpty(full.rootId).First();
            var cards = PersonCardView.Instance;
            Assert.IsTrue(cards.Open(who, true));
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot($"pc774-short-family-{w}");
            foreach (var tab in new[] { CardTab.Family, CardTab.Friends, CardTab.Story })
            {
                cards.ShowTab(tab);
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.AreEqual(tab, cards.Tab);
                Assert.AreEqual(DisplayStyle.Flex, cards.CardRoot.Q("person-card-tabs").resolvedStyle.display, "the tall card shows the tabs");
                yield return Shot($"pc774-{tab.ToString().ToLowerInvariant()}-{w}");
            }
            var dead = full.nodes.FirstOrDefault(n => !n.alive);
            Assert.NotNull(dead, "the deep family has a stone");
            var scroller = cards.CardRoot.Q<ScrollView>("person-card-body");
            scroller.scrollOffset = new Vector2(0f, 145f); // tester 11-27: the last card was scrolled down to the tree link
            yield return Frames(2);
            cards.Open(dead.id, false);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0f, cards.CardRoot.Q<ScrollView>("person-card-body").scrollOffset.y, 0.5f, "another person's card opens at the top: the died line shows");
            Assert.IsNotNull(cards.CardRoot.Q("person-card-memorial"));
            yield return Shot($"pc774-dead-{w}");
            cards.Close();
            yield return Frames(4);
            AssertNoErrors("the card's tabs");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator CardTabsOnATallPhone() => CardTabs(1179, 2556);

        [UnityTest, Timeout(240000)]
        public IEnumerator CardTabsOnAShortPhone() => CardTabs(750, 1334);
    }
}
