using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#188 by touch: a tap opens a building's card; a drag, clean mode and shape mode don't; people win; the builder's name goes to their card.</summary>
    [Category("BuildingCard")]
    public class BuildingCardPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static GameHud Hud => GameHud.Instance;
        static PersonCardView Cards => PersonCardView.Instance;
        static VisualElement Panel => Hud.Root.panel.visualTree;

        IEnumerator Grown()
        {
            yield return OpenSlice(seed: Seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(14f);
            yield return Frames(3);
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
        }

        /// <summary>The screen point of a building no doll stands by, over its roof, in the clear of the HUD.</summary>
        static bool FindTarget(out int id, out Vector2 at)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            var cam = Camera.main;
            foreach (var l in Sim.Settlement.Lots.Where(x => !x.retired && x.buildingId > 0 && x.kind != LotKind.Farm))
            {
                at = cam.WorldToScreenPoint(box.transform.TransformPoint(new Vector3(l.x, box.GroundHeight(l.x, l.z) + 0.2f, l.z)));
                if (at.x < 40f || at.x > Screen.width - 40f || at.y < Screen.height * 0.25f || at.y > Screen.height * 0.8f) continue;
                id = Cards.PickBuilding(at); // the nearest roof along the ray may be the next lot's: that's the one a finger gets
                if (id > 0 && Cards.PickPerson(at).IsNone) return true;
            }
            id = -1; at = default;
            return false;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ATapOpensTheCardAndADragDoesNot()
        {
            yield return Grown();
            PeopleView.DebugSetHour(23.5f); // indoors: nobody between the finger and the roof
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(FindTarget(out int id, out var at), $"a building in the clear, of {Sim.Settlement.Lots.Count} lots");
            int knocks = GlassVoice.Instance.Knocks;
            yield return Drag(new[] { at, at + new Vector2(240f, 0f) }, 40f);
            yield return Frames(3);
            Assert.IsFalse(Cards.BuildingOpen, "a drag moves the camera");
            Hud.SetCleanMode(false);
            Camera.main.GetComponent<BoxCamera>().GoHome(0.05f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(FindTarget(out id, out at), "a building in the clear again");
            yield return TapAt(at);
            yield return Frames(3);
            Assert.AreEqual(id, Cards.BuildingId, "a tap opens its card");
            yield return new WaitForSecondsRealtime(GlassVoice.TapGap + 0.2f);
            Assert.AreEqual(knocks, GlassVoice.Instance.Knocks, "and it's no knock");
            var title = Panel.Q<Label>("building-card-title");
            Assert.AreEqual(Sim.BuildingCardOf(id).title, title.text);
            yield return Shot("bundle2-188-village");
            yield return TapAt(GameUiPanel.ScreenCenter(Panel, "building-card-close"));
            yield return Frames(3);
            Assert.IsFalse(Cards.BuildingOpen, "Close closes it");
            yield return TapAt(at);
            yield return Frames(2);
            Assert.IsTrue(Cards.BuildingOpen);
            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.93f)); // the world, off the box
            yield return Frames(3);
            Assert.IsFalse(Cards.BuildingOpen, "a tap on the world closes it, as a person's card");
            AssertNoErrors("building card");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CleanAndShapeModeKeepTheirTouch()
        {
            yield return Grown();
            PeopleView.DebugSetHour(23.5f); // indoors: nobody between the finger and the roof
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(FindTarget(out int id, out var at), $"a building in the clear, of {Sim.Settlement.Lots.Count} lots");
            Hud.SetCleanMode(true);
            yield return Frames(2);
            yield return TapAt(at);
            yield return Frames(3);
            Assert.IsFalse(Cards.BuildingOpen, "clean mode");
            Hud.SetCleanMode(false);
            ShapingView.Instance.SetTool(SimCore.NudgeKind.Rain);
            yield return Frames(2);
            yield return TapAt(at);
            yield return Frames(3);
            Assert.IsFalse(Cards.BuildingOpen, "shape mode");
            ShapingView.Instance.SetTool(null);
            AssertNoErrors("modes");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator PeopleWinOverTheBuildingBehindThem()
        {
            yield return Grown();
            var pv = PeopleView.Find();
            var cam = Camera.main;
            foreach (var a in pv.Activities.Where(x => x.who.IsNotable))
            {
                if (!pv.TryGetDollTop(a.who, out var top) || !pv.TryGetDollFeet(a.who, out var feet)) continue;
                Vector2 s = cam.WorldToScreenPoint((top + feet) * 0.5f);
                if (Cards.PickPerson(s) != a.who || Cards.PickBuilding(s) < 0) continue;
                Assert.IsTrue(Cards.WorldTap(s));
                yield return Frames(2);
                Assert.IsTrue(Cards.IsOpen, "the person's card");
                Assert.IsFalse(Cards.BuildingOpen, "not the building's");
                AssertNoErrors("people first");
                yield break;
            }
            Assert.Inconclusive("nobody stood in front of a building in this run");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheBuilderLinkAndBothWordings()
        {
            yield return Grown();
            var b = Sim.State.buildings[0];
            var living = Sim.State.notables.First(n => n.alive && n.age >= 16f);
            var dead = Sim.State.notables.FirstOrDefault(n => !n.alive);
            if (dead == null) { dead = Sim.State.notables.Last(n => n.alive && n != living); dead.alive = false; } // a young box: nobody has died yet

            Sim.DebugSetBuilder(b.id, 0);
            Assert.IsTrue(Cards.OpenBuilding(b.id));
            yield return Frames(3);
            StringAssert.Contains("together", Panel.Q<Label>("building-card-by").text);
            Assert.AreEqual(DisplayStyle.None, Panel.Q("building-card-builder").resolvedStyle.display, "no link for the people together");
            yield return Shot("bundle2-188-together");

            Sim.DebugSetBuilder(b.id, living.id);
            Cards.OpenBuilding(b.id);
            yield return Frames(3);
            yield return TapAt(GameUiPanel.ScreenCenter(Panel, "building-card-builder"));
            yield return Frames(3);
            Assert.AreEqual(living.id, Cards.OpenId, "the builder's card");
            Assert.IsFalse(Cards.BuildingOpen);
            Cards.Close();
            yield return new WaitForSecondsRealtime(0.4f);

            Sim.DebugSetBuilder(b.id, dead.id);
            Cards.OpenBuilding(b.id);
            yield return Frames(3);
            yield return Shot("bundle2-188-dead-builder");
            yield return TapAt(GameUiPanel.ScreenCenter(Panel, "building-card-builder"));
            yield return Frames(3);
            Assert.AreEqual(dead.id, Cards.OpenId, "also when they've died");
            Cards.Close();
            AssertNoErrors("builder link");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CampAndTownShots()
        {
            yield return OpenSlice(seed: Seed);
            ShowMeDirector.BubblesOn = false;
            SimLayoutSource.DebugSkipYears(6f);
            yield return null;
            Sim.CleanAll();
            Assert.That(PersonCardView.DebugBuildingCard(), Does.StartWith("building"));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("bundle2-188-camp");
            Cards.CloseBuilding();
            MomentPlayer.DebugGrowVillage(20f);
            yield return Frames(3);
            Sim.ForceNextEra();
            yield return Frames(3);
            SimLayoutSource.DebugSkipYears(10f);
            yield return Frames(3);
            Sim.CleanAll();
            var tavern = Sim.State.buildings.FirstOrDefault(b => b.type == BuildingType.Tavern) ?? Sim.State.buildings.Last();
            Assert.IsTrue(Cards.OpenBuilding(tavern.id));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("bundle2-188-town");
            AssertNoErrors("shots");
        }
    }
}
