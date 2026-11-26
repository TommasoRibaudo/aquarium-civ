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
    /// <summary>#766: one sheet at a time. Remember on a legend's card opens their page in the book; the legend's note sheet must
    /// not stay up over it, and a person tapped in the book opens their card (taps through the real panel).</summary>
    public class OneSheetPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BookView Book => BookView.Instance;
        static CardStackView Cards => CardStackView.Instance;

        static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None && e.worldBound.height > 1f;

        int LegendId;

        IEnumerator Remembered(int w, int h)
        {
            yield return OpenSlice(w, h, seed: Seed);
            GameUiPanel.AcceptInputUnfocused(true);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            GameClock.DebugAway(24);
            yield return Frames(6);
            Assert.That(BookView.DebugEarnAll(), Does.Contain("book pages"));
            yield return Frames(4);
            Assert.That(CardStackView.DebugCard(CardKind.Legend), Does.StartWith("cards"));
            yield return Frames(6);
            Assert.AreEqual(CardKind.Legend, Cards.Model.Face.kind);
            Assert.IsTrue(Shown(Cards.Root.Q("cards-sheet")), "the legend's note is up");
            LegendId = Cards.Model.Shown.people[0];
            yield return TapAt(GameUiPanel.ScreenCenter(Cards.Root.Q("cards-action")));
            yield return Frames(8);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RememberThenAnotherPersonsCardOpensAndTheNoteIsClosed([Values(0, 1)] int size)
        {
            int w = size == 0 ? 750 : 1179, h = size == 0 ? 1334 : 2556;
            yield return Remembered(w, h);
            Assert.IsTrue(Book.IsOpen, "Remember opens the book");
            Assert.IsFalse(Shown(Cards.Root.Q("cards-sheet")), "one sheet at a time: the note is closed under the book");
            yield return Shot($"766/{w}_1_book_after_remember");
            // a person in the book other than the legend
            int other = -1;
            for (int i = 0; i < Book.Pages.Count && other < 0; i++)
            {
                Book.GoTo(i);
                yield return Frames(4);
                var p = Book.PageElement.Query<VisualElement>().ToList().FirstOrDefault(e => e.name != null && e.name.StartsWith("book-person-") && e.name != "book-person-" + LegendId && Shown(e));
                if (p != null) other = int.Parse(p.name.Substring("book-person-".Length));
            }
            Assert.GreaterOrEqual(other, 0, "a person to tap in the book");
            var el = Book.PageElement.Q("book-person-" + other);
            yield return TapAt(GameUiPanel.ScreenCenter(el));
            yield return Frames(10);
            var person = PersonCardView.Instance;
            Assert.IsTrue(person.IsOpen, "the other person's card shows");
            Assert.AreEqual(other, person.OpenId);
            Assert.AreNotEqual(LegendId, person.OpenId, "a different person than the one remembered");
            Assert.IsFalse(Shown(Cards.Root.Q("cards-sheet")), "the note stays closed");
            yield return Shot($"766/{w}_2_other_card");
            AssertNoErrors("one sheet");
        }
    }
}
