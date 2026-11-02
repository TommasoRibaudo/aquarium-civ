using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#223: the dilemma card by touch on the real Slice at portrait phone sizes. The card reads as the people's ask with two
    /// answer buttons (full targets, in the lower half, inside the 30 % rule); a tap answers on the Card path, the chosen option lifts and
    /// the other dims, and Next moves on. Screenshots: <c>223-*</c>.</summary>
    [Category("DilemmaCard")]
    public class DilemmaCardPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static CardStackView View => CardStackView.Instance;
        static CardStackModel Model => View.Model;

        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        IEnumerator Tap(string name)
        {
            var e = View.Root.Q(name);
            Assert.IsTrue(Shown(e), name + " is on screen");
            yield return TapAt(GameUiPanel.ScreenCenter(e));
            yield return Frames(4);
        }

        /// <summary>A grown box, a dilemma raised from this def, the stack open on its card.</summary>
        IEnumerator ToTheDilemma(string def, int w, int h)
        {
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: Seed);
            GameUiPanel.AcceptInputUnfocused(true);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            var d = Sim.DebugRaiseDilemma(def);
            Assert.IsNotNull(d, def + " can be raised in this box");
            GameClock.DebugAway(24);
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Peek, Model.Mode);
            yield return Tap("cards-peek");
            for (int guard = 0; guard < 8 && Model.Face.kind != CardKind.Dilemma; guard++) yield return Tap("cards-next");
            Assert.AreEqual(CardKind.Dilemma, Model.Face.kind);
            yield return Frames(6);
        }

        void AssertLayout(string when)
        {
            var sheet = View.Root.Q("cards-sheet");
            float panelH = View.Root.worldBound.height;
            Assert.LessOrEqual(sheet.worldBound.height / panelH, 0.33f, when + ": the slim-card rule");
            foreach (var n in new[] { "cards-dil-answer-0", "cards-dil-answer-1", "cards-next" })
            {
                var b = View.Root.Q(n);
                Assert.IsTrue(Shown(b), n);
                Assert.GreaterOrEqual(b.worldBound.height, 120f, n + " is drawn 44 pt or more (the reach takes it to a full target)");
                Assert.IsTrue(sheet.worldBound.Contains(b.worldBound.center), n + " sits on the sheet");
                Assert.Greater(b.worldBound.center.y, panelH * 0.55f, n + " is in the lower half, under a thumb");
            }
            var a0 = View.Root.Q("cards-dil-answer-0").worldBound;
            var a1 = View.Root.Q("cards-dil-answer-1").worldBound;
            Assert.LessOrEqual(a0.xMax, a1.xMin, "the two answers do not overlap");
            foreach (var n in new[] { "cards-dil-hint-0", "cards-dil-hint-1" })
                Assert.LessOrEqual(View.Root.Q(n).worldBound.yMax, sheet.worldBound.yMax + 1f, n + " is not cut off");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TappingAWayOnTheCardAnswersOnTheCardPathAndNextMovesOn()
        {
            yield return ToTheDilemma("camp_travellers", 1170, 2532);
            yield return Shot("223-way-open");
            AssertLayout("way");
            var d = Sim.OpenDilemma;
            Assert.IsNotNull(d);
            yield return Tap("cards-dil-answer-1");
            Assert.AreEqual(DilemmaState.Answered, d.state);
            Assert.AreEqual(1, d.option);
            Assert.AreEqual(DilemmaPath.Card, d.path);
            Assert.AreEqual(1, Model.Face.dilemmaChosen);
            yield return Frames(20);
            Assert.IsTrue(View.Root.Q("cards-dil-1").ClassListContains("cards-dil--chosen"), "the chosen one lifts");
            Assert.IsTrue(View.Root.Q("cards-dil-0").ClassListContains("cards-dil--dim"), "the other dims");
            yield return Shot("223-way-answered");
            // a second tap on either changes nothing
            yield return TapAt(GameUiPanel.ScreenCenter(View.Root.Q("cards-dil-answer-0")));
            yield return Frames(4);
            Assert.AreEqual(1, d.option, "no undo");
            yield return Tap("cards-next");
            Assert.AreNotEqual(CardKind.Dilemma, Model.Mode == CardStackMode.Open ? Model.Face.kind : CardKind.Story);
            AssertNoErrors("way");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheWhoCardShowsTwoPortraitsOnASmallPhoneAndAnAnswerLiftsOne()
        {
            yield return ToTheDilemma("camp_fire", 750, 1334);
            yield return Shot("223-who-open-750");
            AssertLayout("who small");
            var d = Sim.OpenDilemma;
            Assert.IsNotNull(d);
            Assert.GreaterOrEqual(d.personA, 0);
            yield return Tap("cards-dil-answer-0");
            Assert.AreEqual(DilemmaPath.Card, d.path);
            Assert.AreEqual(0, d.option);
            yield return Frames(20);
            yield return Shot("223-who-answered-750");
            AssertNoErrors("who");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheWhoCardOnATallPhone()
        {
            yield return ToTheDilemma("camp_fire", 1170, 2532);
            yield return Shot("223-who-open");
            AssertLayout("who tall");
            AssertNoErrors("who tall");
        }
    }
}
