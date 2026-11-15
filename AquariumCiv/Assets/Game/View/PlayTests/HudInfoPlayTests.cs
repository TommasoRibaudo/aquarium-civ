using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#571 the population on the title line; #572 the Offices sheet's chief opens the chief's card, by a real touch.</summary>
    [Category("HudInfo")]
    public class HudInfoPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static GameHud Hud => GameHud.Instance;
        static VisualElement Panel => Hud.Root.panel.visualTree;

        [Test]
        public void TheTitleLineIsBuiltFromParts()
        {
            Assert.AreEqual("Year 37 · 16 people", GameHud.TitleLine(37, null, 16));
            Assert.AreEqual("Year 3 · 1 person", GameHud.TitleLine(3, "", 1));
            Assert.AreEqual("Year 37 · Autumn · 16 people", GameHud.TitleLine(37, "Autumn", 16));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThePopulationShowsAndFollowsBirthsAndDeaths()
        {
            yield return OpenSlice(seed: 1234);
            var year = Panel.Q<Label>(className: "hud-title-year");
            Assert.IsNotNull(year);
            yield return Until(() => year.text.Contains("people") || year.text.Contains("person"), 4f);
            Assert.AreEqual(GameHud.TitleLine(AquariumCiv.Sim.SimCore.YearNo(Sim.State.month), GameHud.SeasonWord(Sim.State), Sim.State.population), year.text);
            yield return Shot("fb5-571-title-1179");
            int pop = Sim.State.population;
            Sim.DebugBirth();      // a newborn notable...
            Sim.State.population++; // ...and the town counts them (the month step does this on a real birth)
            yield return Until(() => year.text.Contains((pop + 1) + " "), 3f);
            StringAssert.Contains($"{pop + 1} people", year.text, "a birth shows");
            Sim.State.population -= 2;
            yield return Until(() => year.text.Contains((pop - 1) + " "), 3f);
            StringAssert.Contains($"{pop - 1} people", year.text, "a death shows");
            Sim.State.population = 1;
            yield return Until(() => year.text.EndsWith("1 person"), 3f);
            StringAssert.EndsWith("1 person", year.text);
            Sim.State.population = 1288;
            yield return Until(() => year.text.Contains("1288"), 3f);
            Sim.State.population = pop;
        }

        /// <summary>The longest plausible line (a season word, three-digit year, big town) beside the round buttons, small and tall phones.</summary>
        [UnityTest, Timeout(90000)]
        public IEnumerator TheLongestLineFitsBesideTheButtons([Values(750, 1179)] int w)
        {
            int h = w == 750 ? 1334 : 2556;
            yield return OpenSlice(w, h, seed: 1234);
            Sim.State.settlementName = "Brightwater Hollow";
            Sim.State.population = 1288;
            Hud.RefreshNow();
            var year = Panel.Q<Label>(className: "hud-title-year");
            year.text = GameHud.TitleLine(137, "Midsummer", 1288); // what #576 will add
            yield return Frames(6);
            var settings = Panel.Q("hud-settings");
            Assert.LessOrEqual(year.worldBound.xMax, settings.worldBound.xMin + 1f, "the line ends before the settings mark");
            Assert.Greater(year.resolvedStyle.width, 50f);
            yield return Shot($"fb5-571-long-{w}");
            Assert.AreEqual(w, Screen.width);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TappingTheChiefsOfficeOpensTheChief()
        {
            yield return OpenSlice(seed: 1234);
            var chief = Sim.HolderOf(RoleType.Chief);
            Assert.IsNotNull(chief, "a camp has a chief");
            Hud.Open(GameHud.Offices);
            yield return Frames(8);
            yield return Shot("fb5-572-offices");
            var holder = Panel.Q("office-chief-holder");
            Assert.IsNotNull(holder, "the chief's office has a tappable name");
            Assert.IsFalse(PersonCardView.Instance.IsOpen);
            yield return TapAt(GameUiPanel.ScreenCenter(Panel, "office-chief-holder"));
            yield return Frames(6);
            Assert.IsTrue(PersonCardView.Instance.IsOpen, "the card opens");
            Assert.AreEqual(chief.id, PersonCardView.Instance.OpenId, "and it is the chief's");
            Assert.IsNull(Hud.OpenSheet, "the Offices sheet gave way");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("fb5-572-chief-card");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AVacantOfficeDoesWhatItDid()
        {
            yield return OpenSlice(seed: 1234);
            Hud.Open(GameHud.Offices);
            yield return Frames(6);
            foreach (var l in Panel.Query<Label>(className: "office-vacant").ToList())
                Assert.IsNull(l.Q<GlassButton>(), "a vacant row is a plain label");
            Assert.IsFalse(PersonCardView.Instance.IsOpen);
        }
    }
}
