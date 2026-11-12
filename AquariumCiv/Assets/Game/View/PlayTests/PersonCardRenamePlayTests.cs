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
    /// <summary>#575: rename a notable from the person card at phone size, by real touches on the button and Keep.</summary>
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
                yield return TapEl(Q("person-card-rename-keep"));
                yield return Frames(5);
                Assert.IsNotEmpty(Cards.RenameProblem, "a taken name is said, not taken");
                Assert.AreEqual(was, Sim.FindNotable(n.id).name);
                yield return Shot("rename-3-taken");
            }
            field.value = "Pip";
            yield return TapEl(Q("person-card-rename-keep"));
            yield return Frames(10);
            Assert.AreEqual("Pip", Sim.FindNotable(n.id).name, "after Keep: problem \"" + Cards.RenameProblem + "\", field \"" + Cards.RenameField?.value + "\", open " + Cards.RenameOpen);
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
    }
}
