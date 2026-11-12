using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#575: rename a notable from the person card at phone size, by real touches on the button and Save.</summary>
    [Category("PersonCardRename")]
    public class PersonCardRenamePlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PersonCardView Cards => PersonCardView.Instance;

        static VisualElement Q(string name) => Cards.CardRoot.Q(name);

        IEnumerator TapEl(VisualElement e)
        {
            Assert.IsNotNull(e, "element exists");
            var c = e.worldBound.center;
            float k = Screen.width / GameUiPanel.ReferenceWidth;
            yield return TapAt(new Vector2(c.x * k, Screen.height - c.y * k));
        }

        [UnityEngine.TestTools.UnityTest]
        public IEnumerator TheCardRenamesSomeoneAndTheNewNameStays()
        {
            yield return OpenSlice(1179, 2556, 4242);
            var n = Sim.State.notables.First(x => x.alive);
            string was = n.name;
            var old = Sim.Chronicle("test_old", $"{n.FullName} mended the roof.", 0.5f, n.id);
            Assert.IsTrue(Cards.Open(n.id, false));
            yield return Frames(10);
            var btn = Q("person-card-rename");
            Assert.IsNotNull(btn, "a living person's About tab has the rename button");
            Assert.GreaterOrEqual(btn.worldBound.height, 44f * GlassType.Pt - 1f, "44 pt tall");
            yield return Shot("rename-1-button");
            yield return TapEl(btn);
            yield return Frames(10);
            Assert.IsTrue(Cards.RenameOpen, "the field opens");
            var field = Cards.RenameField;
            Assert.IsNotNull(field);
            yield return Shot("rename-2-field");
            // refusal first: a name a living neighbour has
            var other = Sim.State.notables.FirstOrDefault(x => x.alive && x.id != n.id);
            if (other != null)
            {
                field.value = other.name;
                yield return TapEl(Q("person-card-rename-save"));
                yield return Frames(5);
                Assert.IsNotEmpty(Cards.RenameProblem, "a taken name is said, not taken");
                Assert.AreEqual(was, Sim.FindNotable(n.id).name);
                yield return Shot("rename-3-taken");
            }
            field.value = "Pip";
            yield return TapEl(Q("person-card-rename-save"));
            yield return Frames(10);
            Assert.AreEqual("Pip", Sim.FindNotable(n.id).name, "after Save: problem \"" + Cards.RenameProblem + "\", field \"" + Cards.RenameField?.value + "\", open " + Cards.RenameOpen);
            Assert.IsFalse(Cards.RenameOpen);
            StringAssert.Contains("Pip", Cards.CardRoot.Q<Label>("person-card-name").text, "the title reads the new name");
            Assert.AreEqual($"{was} {n.family} mended the roof.", old.text, "old lines keep the old name");
            yield return Shot("rename-4-done");
            Cards.Close();
            Assert.IsTrue(Cards.Open(n.id, false));
            yield return Frames(5);
            StringAssert.Contains("Pip", Cards.CardRoot.Q<Label>("person-card-name").text, "and it holds on reopening");
            CloseSlice();
        }
        /// <summary>#584: with the phone keyboard up the card fits between the top safe area and the keyboard instead of riding up whole,
        /// and the field, Never mind and Save all show. A fake keyboard (the Editor never raises one), a tall one on purpose.</summary>
        [UnityEngine.TestTools.UnityTest]
        public IEnumerator WithTheKeyboardUpTheCardFitsUnderTheSafeAreaAndShowsItsButtons()
        {
            foreach (var size in new[] { new Vector2Int(1179, 2556), new Vector2Int(750, 1334) })
            {
                yield return OpenSlice(size.x, size.y, 4242);
                float panelH = GameUiPanel.ReferenceWidth * size.y / size.x;
                float kb = panelH * 0.5f; // far taller than a real keyboard: the old translate-only card went off the top
                GlassKeyboard.Fake = kb;
                try
                {
                    var n = Sim.State.notables.First(x => x.alive);
                    Assert.IsTrue(Cards.Open(n.id, false));
                    yield return Frames(10);
                    yield return TapEl(Q("person-card-rename"));
                    yield return Frames(10);
                    Assert.IsTrue(Cards.RenameOpen);
                    yield return new WaitForSecondsRealtime(1f);
                    string at = $" at {size.x}x{size.y}";
                    Assert.That(Cards.SheetTop, Is.GreaterThanOrEqualTo(Cards.RenameTopLimit - 1f), "the card's top stays under the safe area and HUD strip" + at);
                    var sheet = Cards.CardRoot.Q("person-card");
                    Assert.That(sheet.worldBound.yMax, Is.LessThanOrEqualTo(panelH - kb + 1f), "the card sits on the keyboard, not under it" + at);
                    var area = Cards.CardRoot.Q("person-card-body").worldBound;
                    foreach (var name in new[] { "person-card-rename-field", "person-card-rename-cancel", "person-card-rename-save" })
                    {
                        var b = Q(name).worldBound;
                        Assert.That(b.yMin, Is.GreaterThanOrEqualTo(area.yMin - 1f), name + " is not cut off at the top" + at);
                        Assert.That(b.yMax, Is.LessThanOrEqualTo(area.yMax + 1f), name + " is not cut off at the bottom" + at);
                    }
                    var cancel = Q("person-card-rename-cancel").worldBound;
                    var field = Q("person-card-rename-field").worldBound;
                    Assert.That(cancel.yMin - field.yMax, Is.GreaterThan(10f), "space between the field and the buttons" + at);
                    yield return Shot($"rename-kb-{size.x}x{size.y}");
                    GlassKeyboard.Fake = null;
                    yield return Frames(5);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Assert.That(sheet.worldBound.yMax, Is.GreaterThanOrEqualTo(panelH - 200f), "the keyboard went down: the card is back on the bottom edge" + at);
                }
                finally { GlassKeyboard.Fake = null; }
                CloseSlice();
            }
        }
    }
}
