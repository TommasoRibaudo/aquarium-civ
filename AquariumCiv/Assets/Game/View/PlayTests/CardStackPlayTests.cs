using System.Collections;
using System.Linq;
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
    /// <summary>#75: the card stack by touch on the real Slice scene at a portrait phone size. A return shows the peek, paging
    /// marks read and survives a kill, Show me keeps the stack, Give arms the tool at the wish's spot, the history dot opens
    /// what's waiting, and nothing overlaps.</summary>
    public class CardStackPlayTests : PlayTestBase
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

        /// <summary>A return with this wish in it: the stack open on its card (last, and an era may wait before it).</summary>
        IEnumerator ToTheWish()
        {
            Assert.IsTrue(CardStackView.DebugReturn(2).StartsWith("cards"));
            yield return Frames(3);
            yield return Tap("cards-peek");
            for (int guard = 0; guard < 3 && Model.Face.kind != CardKind.Wish; guard++) yield return Tap("cards-next");
            Assert.AreEqual(CardKind.Wish, Model.Face.kind);
        }

        /// <summary>A grown box with stories, nothing read, away for a day: the peek is up.</summary>
        IEnumerator Returned(double hours = 24)
        {
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            GameClock.DebugAway(hours);
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Peek, Model.Mode, "a return shows the peek");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AReturnShowsThePeekAndATapOpensASlimStack()
        {
            yield return Returned();
            var peek = View.Root.Q("cards-peek");
            Assert.IsTrue(Shown(peek));
            yield return Shot("cards_peek");
            var pb = peek.worldBound;
            float panelH = View.Root.worldBound.height;
            var row = GameHud.Instance.Root.Q("hud-bottom").worldBound;
            Assert.Less(pb.yMax, row.yMin, "the peek sits above the HUD's bottom row");
            Assert.GreaterOrEqual(pb.height, GameUiPanel.MinTarget, "a thumb can hit it");

            yield return TapAt(GameUiPanel.ScreenCenter(peek));
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Open, Model.Mode);
            var sheet = View.Root.Q("cards-sheet");
            Assert.IsTrue(Shown(sheet));
            yield return Shot("cards_stack");
            yield return Frames(2);
            Assert.LessOrEqual(sheet.worldBound.height / panelH, 0.33f, "at most about 30 % of the screen");
            Assert.IsTrue(GameHud.Instance.ToolsHidden, "the tools wait while the stack is open");
            foreach (var n in new[] { "cards-next", "cards-skip", "cards-showme" })
                Assert.GreaterOrEqual(View.Root.Q(n).worldBound.height, GameUiPanel.MinTarget - 1, n);
            Assert.IsTrue(MomentPlayer.PromptsBlocked(), "prompts wait for the cards");
            AssertNoErrors("open");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PagingMarksReadAndSurvivesAKill()
        {
            yield return Returned();
            yield return Tap("cards-peek");
            int pages = Model.PageCount;
            Assert.Greater(pages, 2, "enough stories to page");
            string first = Model.Face.id;
            yield return Tap("cards-next");
            Assert.AreEqual(1, Model.Index);
            Assert.IsFalse(Sim.IsCardRead(Model.Shown), "the page in front is not read yet");
            yield return Tap("cards-back");
            Assert.AreEqual(0, Model.Index);
            Assert.AreEqual(first, Model.Face.id);
            yield return Tap("cards-next");
            string read = Model.Face.id; // the era that may lead the stack waits by design: this is a plain story
            Assert.AreNotEqual(CardKind.EraReady, Model.Face.kind);
            yield return Tap("cards-next");
            int unreadBefore = Sim.UnreadCardsCapped(100).cards.Count;

            // The phone kills the app with the stack up: what the pause saved is what comes back.
            GameClock.Instance.Save();
            yield return OpenSlice(seed: Seed, freshSave: false);
            Assert.AreEqual(unreadBefore, Sim.UnreadCardsCapped(100).cards.Count, "what's read stays read");
            GameClock.DebugAway(24);
            yield return Frames(6);
            yield return Tap("cards-peek");
            for (int i = 0; i < Model.PageCount; i++)
            {
                Assert.AreNotEqual(read, Model.Face.id, "a read card doesn't come back");
                if (i < Model.PageCount - 1) yield return Tap("cards-next");
            }
            AssertNoErrors("after the kill");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SkipAllLeavesADotThatOpensTheStack()
        {
            yield return Returned();
            yield return Tap("cards-peek");
            yield return Tap("cards-skip");
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode);
            Assert.IsFalse(GameHud.Instance.ToolsHidden);
            yield return Frames(3);
            Assert.IsTrue(Shown(GameHud.Instance.Root.Q("hud-history-dot")), "a dot, never a number");
            yield return Shot("cards_dot");
            yield return TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root, "hud-history"));
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Open, Model.Mode, "the dot opens what's waiting");
            Assert.IsNull(GameHud.Instance.OpenSheet);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SwipesPageAndATouchElsewhereHidesThePeek()
        {
            yield return Returned();
            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f));
            yield return Frames(4);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode, "the first touch elsewhere hides the peek");
            Assert.IsTrue(Model.HistoryDot);

            Assert.IsTrue(CardStackView.DebugReturn(5).StartsWith("cards"));
            yield return Frames(3);
            yield return Tap("cards-peek");
            var at = GameUiPanel.ScreenCenter(View.Root.Q("cards-body"));
            yield return Drag(new[] { at + Vector2.right * 300f, at + Vector2.left * 300f }, 40f);
            yield return Frames(4);
            Assert.AreEqual(1, Model.Index, "a swipe left is Next");
            AssertNoErrors("swipes");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ShowMeFliesAndComesBackToTheStack()
        {
            yield return Returned();
            yield return Tap("cards-peek");
            var cam = Camera.main.GetComponent<BoxCamera>();
            var before = cam.target;
            yield return Tap("cards-showme");
            Assert.IsTrue(ShowMeDirector.Instance.Busy, "the camera flies");
            Assert.AreEqual(CardStackMode.Open, Model.Mode, "the stack stays");
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot("cards_showme");
            Assert.IsTrue(Shown(View.Root.Q("cards-sheet")));
            Assert.IsTrue(Sim.IsCardRead(Model.Shown));
            yield return Tap("cards-next");
            yield return Tap("cards-skip");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.Less(Vector3.Distance(before, cam.target), 0.05f, "closing the stack takes the camera back");
            AssertNoErrors("show me");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator APortraitOpensThePersonCardAndClosingItBringsTheStackBack()
        {
            yield return Returned();
            var w = Sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Assert.IsNotNull(w);
            yield return ToTheWish();
            yield return Tap("cards-portrait-0");
            var cards = PersonCardView.Instance;
            Assert.IsTrue(cards.IsOpen);
            Assert.IsFalse(Shown(View.Root.Q("cards-sheet")), "one card at a time");
            yield return Frames(4);
            yield return TapAt(GameUiPanel.ScreenCenter(cards.CardRoot.Q("person-card-close")));
            yield return Frames(6);
            Assert.IsTrue(Shown(View.Root.Q("cards-sheet")));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator GiveArmsTheToolAtTheWishesSpotAndCancelKeepsTheWish()
        {
            yield return Returned();
            Sim.DebugRefillShaping();
            var w = Sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Assert.IsNotNull(w);
            yield return ToTheWish();
            Assert.AreEqual(CardAction.Give, Model.Face.action);
            yield return Shot("cards_wish");
            yield return Tap("cards-action");
            var sv = ShapingView.Instance;
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode, "Give leads into the shaping");
            Assert.IsTrue(sv.WishArmed, "the tool is in hand");
            Assert.AreEqual(w.kind, sv.Tool);
            Assert.AreEqual(w.x, sv.Tray.PendingX, 0.001f);
            Assert.AreEqual(WishState.Open, w.state, "nothing is spent yet");
            yield return Frames(3);
            yield return Shot("cards_armed");
            var ui = sv.GetComponentInChildren<UIDocument>().rootVisualElement;
            Assert.IsTrue(Shown(ui.Q("shaping-wish-bar")));
            Assert.IsTrue(Shown(ui.Q("shaping-wish-spot")));
            Assert.IsTrue(MomentPlayer.PromptsBlocked(), "the banner holds the prompts' lane");

            yield return TapAt(GameUiPanel.ScreenCenter(ui.Q("shaping-cancel")));
            yield return Frames(4);
            Assert.IsFalse(sv.ShapeMode, "Cancel puts the tool down");
            Assert.AreEqual(WishState.Open, w.state, "the wish stays");

            // Again, and a tap on the ground gives it where the wish points.
            yield return ToTheWish();
            yield return Tap("cards-action");
            yield return Frames(3);
            float charges = Sim.State.shapingCharges;
            int nudges = Sim.State.nudges.Count;
            yield return TapAt(sv.ScreenOf(w.x, w.z));
            yield return Frames(6);
            Assert.AreEqual(WishState.Given, w.state, sv.LastResult);
            Assert.AreEqual(nudges + 1, Sim.State.nudges.Count);
            Assert.Less(Sim.State.shapingCharges, charges, "a real gift: it spends a charge");
            Assert.IsFalse(sv.ShapeMode);
            AssertNoErrors("give");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator GiveWithNoChargeSaysWhyOnTheCard()
        {
            yield return Returned();
            var w = Sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Assert.IsNotNull(w);
            Sim.State.shapingCharges = 0;
            yield return ToTheWish();
            var n = View.Root.Q<Label>("cards-note");
            Assert.IsTrue(Shown(n));
            Assert.That(n.text, Does.Contain("cleaning the glass"));
            Assert.IsFalse(View.Root.Q("cards-action").enabledSelf);
            yield return Shot("cards_nocharge");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheAlsoPageOpensAFullCard()
        {
            yield return Returned(0.5);
            yield return Tap("cards-peek");
            Assert.Greater(Model.AlsoCount, 0, "a short return with a lot to tell overflows");
            for (int guard = 0; guard < 8 && !Model.OnAlsoPage; guard++) yield return Tap("cards-next");
            Assert.IsTrue(Model.OnAlsoPage);
            yield return Frames(2);
            yield return Shot("cards_also");
            Assert.LessOrEqual(View.Root.Q("cards-sheet").worldBound.height / View.Root.worldBound.height, 0.34f);
            string line = View.Root.Q<Button>("cards-also-0").text;
            yield return Tap("cards-also-0");
            Assert.AreEqual(line, Model.Face.headline);
            yield return Tap("cards-back");
            Assert.IsTrue(Model.OnAlsoPage);
            yield return Tap("cards-next");
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode);
            Assert.IsFalse(Sim.HasUnreadCards);
            AssertNoErrors("also");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NoStackDuringTheFirstRun()
        {
            yield return OpenSlice(seed: Seed, skipFirstRun: false);
            Assert.IsTrue(FirstRunView.Instance.Active);
            GameClock.DebugAway(24);
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode);
        }
    }
}
