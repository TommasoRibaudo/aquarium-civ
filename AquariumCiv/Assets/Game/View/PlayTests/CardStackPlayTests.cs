using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
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
        IEnumerator Returned(double hours = 24, int w = 1179, int h = 2556)
        {
            yield return OpenSlice(w, h, seed: Seed);
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
            {
                var b = View.Root.Q(n); // drawn 44 to 46 pt: the reach takes it to a full target (spec §1)
                Assert.IsTrue(b.ContainsPoint(new Vector2(b.layout.width * 0.5f, b.layout.height * 0.5f - GameUiPanel.MinTarget * 0.5f + 1f)), n);
            }
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
            yield return TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root, "hud-history-dot")); // hierarchy D1: the Stories chip, not History
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Open, Model.Mode, "the Stories chip opens what's waiting");
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
            Assert.IsFalse(View.Root.Q<GlassButton>("cards-action").Available, "unavailable, never faded");
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
            Assert.AreEqual(0.30f, View.Root.Q("cards-sheet").worldBound.height / View.Root.worldBound.height, 0.002f);
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

        /// <summary>The slim sheet's 30 % on a short phone, a tall one and a tablet: a wider screen is a shorter panel, so the lines
        /// scroll, every one stays a target and nothing is counted (rule 8).</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator TheAlsoPageStaysSlimOnEveryScreen([Values(0, 1, 2)] int size)
        {
            int w = new[] { 1080, 1080, 1640 }[size], h = new[] { 1920, 2400, 2360 }[size];
            yield return Returned(0.5, w, h);
            yield return Tap("cards-peek");
            for (int guard = 0; guard < 8 && !Model.OnAlsoPage; guard++) yield return Tap("cards-next");
            Assert.IsTrue(Model.OnAlsoPage);
            yield return Frames(4);
            yield return Shot($"cards_also_{w}x{h}");
            var sheet = View.Root.Q("cards-sheet").worldBound;
            float panelH = View.Root.worldBound.height;
            Assert.AreEqual(0.30f, sheet.height / panelH, 0.002f, $"{w}x{h}");
            Assert.LessOrEqual(sheet.yMax, panelH + 0.5f, "nothing falls off the bottom");
            int lines = 0;
            for (int i = 0; i < 3; i++)
            {
                var l = View.Root.Q("cards-also-" + i);
                if (!Shown(l)) continue;
                lines++;
                Assert.GreaterOrEqual(l.worldBound.height, GameUiPanel.MinTarget - 2f, "a line is still a target");
                Assert.GreaterOrEqual(l.worldBound.yMin, sheet.yMin);
            }
            Assert.AreEqual(System.Math.Min(3, Model.AlsoCount), lines, "every headline is there, in the view or a scroll away");
            Assert.IsNull(View.Root.Q("cards-also-more"), "no count of what was left");
            var view = View.Root.Q<GlassScroll>("cards-scroll").View;
            if (view.contentContainer.layout.height > view.layout.height + 1f)
            {
                var from = GameUiPanel.ScreenCenter(view);
                yield return Drag(new[] { from, from + Vector2.up * 100f }, 10f); // a finger on a line scrolls, and doesn't open it
                yield return Frames(6);
                Assert.Greater(view.scrollOffset.y, 5f, "the lines past the view are a scroll away");
                Assert.IsTrue(Model.OnAlsoPage);
            }
            foreach (var t in View.Root.Query<TextElement>().ToList().Where(Shown)) Assert.That(t.text, Does.Not.Contain(" more"), t.name);
            AssertNoErrors("also, " + size);
        }

        // ---------------- Keeper's Glass (#166) ----------------

        [UnityTest, Timeout(120000)]
        public IEnumerator ThePeekHasItsLaneAndStoriesWaitForTheSwipe()
        {
            yield return Returned();
            var hud = GameHud.Instance;
            hud.SetPinChip("Piers", null);
            yield return Frames(4);
            var peek = View.Root.Q("cards-peek");
            float panelH = View.Root.worldBound.height;
            Assert.AreEqual(GameUiPanel.PeekBottom, panelH - peek.worldBound.yMax, 1f, "the pin's lane");
            Assert.AreEqual(View.Root.Q("cards-peek-text").resolvedStyle.whiteSpace, WhiteSpace.NoWrap, "one line");
            Assert.AreEqual(Visibility.Hidden, hud.Root.Q("hud-pins").resolvedStyle.visibility, "it replaces the pin while it's up");
            Assert.IsTrue(Shown(View.Root.Q("cards-peek-portrait")), "a portrait");
            Assert.IsFalse(string.IsNullOrEmpty(Model.Peek.teaser) && Shown(View.Root.Q("cards-peek-teaser")), "a teaser only when there is one");
            Assert.IsFalse(Shown(hud.Root.Q("hud-history-dot")), "spec §4: no Stories chip beside a peek");
            Assert.IsTrue(GameUiPanel.Toasts.Held, "a toast would land on it");

            var at = GameUiPanel.ScreenCenter(peek);
            yield return Drag(new[] { at, at + Vector2.down * 220f }, 30f);
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode, "a swipe down puts it away");
            Assert.IsTrue(Shown(hud.Root.Q("hud-history-dot")), "and the chip comes up");
            Assert.AreEqual(Visibility.Visible, hud.Root.Q("hud-pins").resolvedStyle.visibility);
            Assert.IsFalse(GameUiPanel.Toasts.Held);
            hud.SetPinChip(null, null);
            AssertNoErrors("peek lane");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EachCardHasOnePrimaryAtMostAndABackOnlyPastTheFirst()
        {
            yield return Returned();
            Sim.DebugRefillShaping();
            Sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Sim.ForceEraReady();
            Assert.IsTrue(CardStackView.DebugReturn(7).StartsWith("cards"));
            yield return Frames(3);
            yield return Tap("cards-peek");
            Assert.IsFalse(Shown(View.Root.Q("cards-back")), "question 14: no Back on the first card");
            var sheet = View.Root.Q("cards-sheet");
            var kinds = new System.Collections.Generic.HashSet<CardKind>();
            for (int page = 0; page < Model.PageCount; page++)
            {
                yield return Frames(3);
                var f = Model.Face;
                var filled = sheet.Query<GlassButton>().ToList().Where(b => Shown(b) && b.Kind == ButtonKind.Primary).ToList();
                if (f.isAlso)
                {
                    Assert.AreEqual("cards-next", filled.Single().name, "Done closes the last page");
                    continue;
                }
                kinds.Add(f.kind);
                Assert.AreEqual(f.action == CardAction.None ? 0 : 1, filled.Count, f.kind + " " + f.headline);
                var next = View.Root.Q<GlassButton>("cards-next");
                Assert.AreEqual(ButtonKind.Secondary, next.Kind, "Next is never primary");
                Assert.That(next.text, Is.EqualTo("Next").Or.EqualTo("Done"), "question 19: plain words, the dots say where");
                Assert.AreEqual(page > 0, Shown(View.Root.Q("cards-back")), "Back from the second card on");
                if (filled.Count == 1) Assert.That(filled[0].text, Is.EqualTo("Give").Or.EqualTo("Watch").Or.EqualTo("Remember"));
                var row = View.Root.Q("cards-buttons").worldBound;
                foreach (var b in View.Root.Q("cards-buttons").Query<GlassButton>().ToList().Where(Shown))
                    Assert.LessOrEqual(b.worldBound.xMax, row.xMax + 0.5f, b.name + " stays in the row");
                if (page < Model.PageCount - 1) yield return Tap("cards-next");
            }
            Assert.Contains(CardKind.Wish, kinds.ToList());
            AssertNoErrors("primaries");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LongCardsScrollInWholeLinesInsideTheSheet([Values(0, 1)] int size)
        {
            int w = new[] { 1179, 750 }[size], h = new[] { 2556, 1334 }[size];
            yield return Returned(24, w, h);
            Assert.IsTrue(CardStackView.DebugCard(CardKind.Relic).StartsWith("cards"));
            yield return Frames(4);
            CardStackView.DebugText("long");
            yield return Frames(6);
            var sheet = View.Root.Q("cards-sheet");
            var panelH = View.Root.worldBound.height;
            Assert.AreEqual(0.30f, sheet.worldBound.height / panelH, 0.002f, "the sheet stays 30 %");
            var scroll = View.Root.Q<GlassScroll>("cards-scroll");
            var view = scroll.View;
            Assert.Greater(view.contentContainer.layout.height, view.layout.height + 1f, "it really overflows");
            float lines = (view.layout.height - 16.5f) / GlassType.BodyPitch;
            Assert.AreEqual(Mathf.Round(lines), lines, 0.08f, "the view ends on a whole line");
            Assert.Greater(view.layout.height, GlassType.BodyPitch * 2f, "the room is used, not left empty");
            var row = View.Root.Q("cards-buttons").worldBound;
            Assert.LessOrEqual(view.worldBound.yMax, row.yMin + 0.5f, "the text stops above the buttons");
            Assert.LessOrEqual(row.yMax, panelH, "and they stay on screen");

            float k = Screen.width / View.Root.worldBound.width;
            var at = GameUiPanel.ScreenCenter(view);
            int page = Model.Index;
            yield return Drag(new[] { at, at + Vector2.up * 120f * k }, 12f);
            yield return Frames(8);
            Assert.Greater(view.scrollOffset.y, 10f, "a finger moves it");
            Assert.AreEqual(page, Model.Index, "and it's no swipe");
            Assert.AreEqual(CardStackMode.Open, Model.Mode);

            at = GameUiPanel.ScreenCenter(view);
            yield return Drag(new[] { at + Vector2.right * 300f * k, at + Vector2.left * 300f * k }, 40f);
            yield return Frames(4);
            Assert.AreNotEqual(CardStackMode.Peek, Model.Mode);
            Assert.IsTrue(page + 1 == Model.Index || Model.Mode == CardStackMode.Hidden, "a swipe still turns a long card");
            AssertNoErrors("long card");
        }

        /// <summary>Spec §2, 375 x 667: 30 %, the year in the dots row, 14 pt under the buttons, and the head and the text clear of each other.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator TheShortPhoneKeepsTheCardAtThirtyPercent()
        {
            yield return Returned(24, 750, 1334);
            var w = Sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Assert.IsNotNull(w);
            yield return ToTheWish();
            yield return Frames(4);
            var sheet = View.Root.Q("cards-sheet").worldBound;
            float panelH = View.Root.worldBound.height;
            Assert.AreEqual(0.30f, sheet.height / panelH, 0.002f);
            var footer = View.Root.Q("cards-footer");
            Assert.IsTrue(Shown(footer));
            Assert.AreEqual(View.Root.Q("cards-dots").parent, footer.parent.parent, "the year joins the dots");
            Assert.LessOrEqual(footer.worldBound.xMax, View.Root.Q("cards-dots").Children().First().worldBound.xMin, "the year stays off the dots");
            var row = View.Root.Q("cards-buttons").worldBound;
            Assert.AreEqual(GlassSheet.ShortBottomPad, sheet.yMax - row.yMax, 1.5f, "14 pt under the buttons");
            Assert.LessOrEqual(View.Root.Q("cards-head").worldBound.yMax, View.Root.Q("cards-body").worldBound.yMin + 0.5f);
            Assert.LessOrEqual(View.Root.Q("cards-body").worldBound.yMax, row.yMin + 0.5f, "the body clears the row");

            Model.Close();
            yield return Frames(4);
            Sim.State.shapingCharges = 0;
            yield return ToTheWish();
            yield return Frames(6);
            var note = View.Root.Q("cards-note");
            Assert.IsTrue(Shown(note));
            Assert.LessOrEqual(note.worldBound.yMax, View.Root.Q<GlassScroll>("cards-scroll").View.worldBound.yMax + 0.5f, "why it's locked is in view");
            AssertNoErrors("short");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheRecallQuestionSitsInTheThumbZoneWithNoScrim()
        {
            yield return OpenSlice(seed: Seed);
            var v = RecallPromptView.Instance;
            var hud = GameHud.Instance;
            RecallPromptView.DebugReset();
            Assert.That(RecallPromptView.DebugForce(), Does.Contain("shown"));
            yield return Frames(6);
            float panelH = v.Root.worldBound.height;
            var sheet = v.Root.Q("recall-sheet");
            Assert.AreEqual(panelH, sheet.worldBound.yMax, 1f, "on the bottom edge");
            Assert.AreEqual(0.30f, sheet.worldBound.height / panelH, 0.002f);
            Assert.AreEqual(PickingMode.Ignore, v.Root.pickingMode, "no scrim");
            Assert.AreEqual(Visibility.Hidden, hud.Root.Q("hud-bottom").resolvedStyle.visibility, "the tools step back");
            Assert.AreEqual(0, sheet.Query<GlassButton>().ToList().Count(b => b.Kind == ButtonKind.Primary), "rule 4: quieter than any player control");
            Assert.IsTrue(MomentPlayer.PromptsBlocked(), "a prompt waits for it");
            var row = v.Root.Q("recall-answers").worldBound;
            Assert.LessOrEqual(v.Root.Q("recall-4").worldBound.xMax, row.xMax + 0.5f);
            yield return TapAt(GameUiPanel.ScreenCenter(v.Root.Q("recall-dismiss")));
            yield return Frames(4);
            Assert.IsFalse(v.IsOpen, "Not now");
            Assert.AreEqual(Visibility.Visible, hud.Root.Q("hud-bottom").resolvedStyle.visibility);
            RecallPromptView.DebugReset();
            AssertNoErrors("recall");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ShowMeWordsSitOnTheBubbleAndTheCaption()
        {
            yield return Returned();
            var d = ShowMeDirector.Instance;
            int id = -1;
            var pv = PeopleView.Find();
            foreach (var n in Sim.State.notables) if (n.alive && pv.TryGetDollTop(PersonRef.Notable(n.id), out _)) { id = n.id; break; }
            Assert.GreaterOrEqual(id, 0, "someone to say it");
            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.6f)); // the peek goes
            yield return Frames(4);
            Assert.IsTrue(d.SayOverHead("Rain on the east field.", PersonRef.Notable(id), 5f));
            yield return Frames(4);
            var ui = GameObject.Find("ShowMeUI").GetComponent<UIDocument>().rootVisualElement;
            Assert.IsInstanceOf<GlassBubble>(ui.Q("showme-bubble"));
            Assert.IsNull(d.ShownCaption);
            string line = new string('a', GlassBubble.MaxChars + 1);
            Assert.IsTrue(d.SayOverHead(line, PersonRef.Notable(id), 5f));
            yield return Frames(4);
            Assert.AreEqual(line, d.ShownCaption, "longer lines become a caption");
            Assert.IsFalse(d.EchoBubbleShown, "one message at a time");
            var cap = ui.Q("showme-caption");
            Assert.IsInstanceOf<GlassCaption>(cap);
            Assert.AreEqual(DisplayStyle.Flex, cap.resolvedStyle.display);
            AssertNoErrors("words");
        }

        /// <summary>Every card kind and the peek at the two sizes, for the eye and for the PR. Run by hand:
        /// <c>--filter CardShots --filter_type category --include_explicit true --async_tests true</c>.</summary>
        [UnityTest, Timeout(300000), Explicit("Writes screenshots; run by hand, #166"), Category("CardShots")]
        public IEnumerator EveryCardKindForTheEye([Values(0, 1)] int size)
        {
            int w = new[] { 1179, 750 }[size], h = new[] { 2556, 1334 }[size];
            string dir = $"166/{w}x{h}/";
            Directory.CreateDirectory(Shots + dir);
            yield return Returned(24, w, h);
            yield return Frames(20);
            yield return Shot(dir + "peek");
            yield return Tap("cards-peek");
            yield return Frames(8);
            yield return Shot(dir + "story-first");
            yield return Tap("cards-skip");
            yield return Frames(8);
            yield return Shot(dir + "stories-chip");

            Sim.DebugRefillShaping();
            foreach (CardKind kind in System.Enum.GetValues(typeof(CardKind)))
            {
                string r = CardStackView.DebugCard(kind);
                Assert.That(r, Does.StartWith("cards"), kind + ": " + r);
                yield return Frames(8);
                yield return Shot(dir + kind.ToString().ToLowerInvariant());
                if (kind == CardKind.Story)
                {
                    foreach (var what in new[] { "payoff", "end", "long" })
                    {
                        CardStackView.DebugText(what);
                        yield return Frames(8);
                        yield return Shot(dir + what);
                    }
                }
                Model.Close();
                yield return Frames(4);
            }

            Sim.State.shapingCharges = 0;
            CardStackView.DebugCard(CardKind.Wish);
            yield return Frames(8);
            yield return Shot(dir + "wish-nocharge");
            Model.Close();
            yield return Frames(4);

            Assert.That(CardStackView.DebugReturn(1), Does.StartWith("cards"));
            yield return Frames(4);
            Model.TapPeek();
            for (int guard = 0; guard < 8 && !Model.OnAlsoPage && Model.Mode == CardStackMode.Open; guard++) { Model.Next(); yield return Frames(3); }
            if (Model.OnAlsoPage) { yield return Frames(8); yield return Shot(dir + "also"); }
            if (Model.Mode == CardStackMode.Open) Model.Close();
            yield return Frames(4);

            RecallPromptView.DebugReset();
            RecallPromptView.DebugForce();
            yield return Frames(8);
            yield return Shot(dir + "recall");
            RecallPromptView.DebugReset();
            yield return Frames(6);

            var d = ShowMeDirector.Instance;
            var pv = PeopleView.Find();
            var who = Sim.State.notables.First(n => n.alive && pv.TryGetDollTop(PersonRef.Notable(n.id), out _));
            d.SayOverHead("Rain on the east field. Told you.", PersonRef.Notable(who.id), 6f);
            yield return Frames(8);
            yield return Shot(dir + "showme-bubble");
            d.SayOverHead("Mira and Tob keep sitting together at the fire, and everyone can see why.", PersonRef.Notable(who.id), 6f);
            yield return Frames(30);
            yield return Shot(dir + "showme-caption");
            AssertNoErrors("shots");
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
