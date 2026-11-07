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

        // ================================================================== #238: a dilemma raised while the player watches

        /// <summary>A grown box at the P7 lab preset (dilemmas on, x60), nothing on screen.</summary>
        IEnumerator Watching(int w, int h, bool dilemmas = true)
        {
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(w, h, seed: Seed);
            GameUiPanel.AcceptInputUnfocused(true);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            DilemmaCheats.Toggle(dilemmas);
            DilemmaCheats.Lab(dilemmas);
            yield return Frames(4);
            if (Model.Mode == CardStackMode.Open) Model.Close();
            if (Model.Mode == CardStackMode.Peek) Model.HidePeek();
            yield return Frames(4);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode, "nothing on screen to start");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ALiveRaiseAtX60PeeksWithinASecondAndOpensOnTheDilemma()
        {
            yield return Watching(1170, 2532);
            Assert.AreEqual(GameClock.LabSpeed, GameClock.Instance.speed, "the lab runs at x60");
            var d = Sim.DebugRaiseDilemma();
            Assert.IsNotNull(d, "a dilemma holds in this box");
            float t0 = Time.realtimeSinceStartup;
            yield return Until(() => Model.Mode == CardStackMode.Peek, 1f);
            float took = Time.realtimeSinceStartup - t0;
            Assert.AreEqual(CardStackMode.Peek, Model.Mode, $"the peek is up {took:0.00} s after a live raise");
            Assert.LessOrEqual(took, 1.05f, "within a second");
            var entry = Sim.State.chronicle.First(e => e.id == d.entryId);
            Assert.AreEqual(Sim.Headline(entry), Model.Peek.text, "the dilemma is on top");
            yield return Frames(10);
            yield return Shot("238-live-peek-1170x2532");
            yield return Tap("cards-peek");
            Assert.AreEqual(CardStackMode.Open, Model.Mode, "a tap opens the stack");
            Assert.AreEqual(CardKind.Dilemma, Model.Face.kind, "on the dilemma card");
            Assert.AreEqual(d.entryId, Model.Shown.lead.id, "this dilemma");
            yield return Frames(10);
            yield return Shot("238-live-card-1170x2532");
            Model.Close();
            yield return Frames(4);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode);
            Assert.IsTrue(Model.HistoryDot, "closed unanswered, it waits: the dot says so");
            Assert.AreEqual(-1, View.LivePending, "shown once: it does not pop up again");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode, "no second peek for the same dilemma");
            DilemmaCheats.Lab(false);
            Assert.AreEqual(DilemmaState.Open, d.state, "#368: closed unanswered, it stays open: nothing lapses by the clock");
            AssertNoErrors("live raise");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheDotFollowsTheStoriesInLivePlay()
        {
            // Dilemmas on (with them off the dot is not worked out live: WithDilemmasOffALiveRaiseChangesNothing), but the lab
            // schedule off so no choice is raised inside the 12 s windows below; the clock still at x60 so a month turns in 5 s.
            yield return Watching(750, 1334);
            DilemmaCheats.Lab(false);
            GameClock.Instance.speed = GameClock.LabSpeed;
            void ReadAll()
            {
                var all = Sim.UnreadCardsCapped(999);
                foreach (var c in all.cards.Concat(all.also)) Sim.MarkCardRead(c);
            }
            // worked out again at the next month (5 s at x60); a story the box writes meanwhile is read too
            yield return Until(() => { ReadAll(); return !Model.HistoryDot; }, 12f);
            Assert.IsFalse(Model.HistoryDot, "all read: no dot");
            var who = Sim.State.notables.Find(n => n.alive);
            Sim.Chronicle("story", who.FullName + " found a way across the stream.", 0.9f, who.id);
            yield return Until(() => Model.HistoryDot, 12f);
            Assert.IsTrue(Model.HistoryDot, "a new story lights the dot without a return");
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode, "a story raised live gets no peek: only a dilemma does");
            AssertNoErrors("dot");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ALiveRaiseWaitsForAnOpenBuildingCard()
        {
            yield return Watching(750, 1334);
            var cards = PersonCardView.Instance;
            int building = Sim.State.buildings.Select(b => b.id).FirstOrDefault(id => Sim.BuildingCardOf(id) != null);
            Assert.IsTrue(cards.OpenBuilding(building), "a building card is up");
            yield return Frames(10);
            var d = Sim.DebugRaiseDilemma();
            Assert.IsNotNull(d);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode, "no peek over the building card");
            Assert.AreEqual(d.entryId, View.LivePending, "it waits");
            cards.CloseBuilding();
            yield return Until(() => Model.Mode == CardStackMode.Peek, 2f);
            Assert.AreEqual(CardStackMode.Peek, Model.Mode, "the peek comes once the card closes");
            yield return Frames(10);
            yield return Shot("238-live-peek-after-card-750x1334");
            AssertNoErrors("wait");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator WithDilemmasOffALiveRaiseChangesNothing()
        {
            yield return Watching(750, 1334, dilemmas: false);
            bool dot = Model.HistoryDot;
            var d = Sim.DebugRaiseDilemma(); // the cheat ignores the switch: the view must still stay as it was
            Assert.IsNotNull(d);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(CardStackMode.Hidden, Model.Mode, "no peek with dilemmas off");
            Assert.AreEqual(-1, View.LivePending);
            Assert.AreEqual(dot, Model.HistoryDot, "the dot is not worked out live either");
            AssertNoErrors("off");
        }

        // ================================================================== #240: the callback and lapse cards

        /// <summary>The story on screen whole: the text needs no scroll and the body ends above the buttons.</summary>
        void AssertWhole(string when)
        {
            var scroll = View.Root.Q<GlassScroll>("cards-scroll");
            var view = scroll.View;
            var body = View.Root.Q("cards-body");
            var buttons = View.Root.Q("cards-buttons");
            Assert.IsTrue(Shown(body), when + ": the body is shown");
            Assert.LessOrEqual(view.contentContainer.layout.height, view.layout.height + 1f, when + ": the story fits without a scroll");
            Assert.LessOrEqual(body.worldBound.yMax, view.worldBound.yMax + 1f, when + ": the body is not cut off");
            Assert.LessOrEqual(body.worldBound.yMax, buttons.worldBound.yMin + 1f, when + ": the body is above the buttons");
            Assert.IsTrue(Shown(View.Root.Q("cards-teaser")), when + ": the grey question line");
        }

        static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<[^>]+>", "").Replace("\n", " ").Trim();

        IEnumerator CallbackCards(int w, int h)
        {
            yield return Watching(w, h);
            DilemmaCheats.Lab(false); // the clock at real time: nothing raised or lapsed in the way of this check
            int seen = 0;
            foreach (var (def, option) in new[] { ("camp_winter", 0), ("camp_fire", 0), ("camp_remember", 1) } /* a def is raised once per box: one option each. camp_remember last: its cheat kills the oldest (the chief), and camp_fire needs a living elder chief */)
            {
                if (Model.Mode == CardStackMode.Open) Model.Close();
                var d = Sim.DebugRaiseDilemma(def);
                if (d == null) { Debug.Log("[240] " + def + " does not hold in this box"); continue; }
                Assert.IsTrue(Sim.AnswerDilemma(d.id, option, DilemmaPath.Card));
                var e = Sim.DebugFireCallbackNow();
                Assert.IsNotNull(e, def + " fires a callback");
                // #371: a callback is a held moment, watched in the box, not a card; it is a card only once held too long. This
                // check is about that card, so it takes the cheat's path (the panel's "fire callback"): the moment becomes the card.
                var held = Sim.MomentOfEntry(e.id);
                Assert.IsNotNull(held, def + ": the callback queued its moment");
                Assert.AreEqual(HeldMomentState.Waiting, held.state, def + ": the moment waits to be watched");
                Assert.IsTrue(Sim.DebugCardMoment(e.id), def + ": the cheat turns it into a card");
                yield return Frames(2);
                Debug.Log("[240] " + CardStackView.DebugOpenEntry(e.id));
                Assert.AreEqual(e.id, Model.Shown?.lead.id, def + ": its card is up");
                yield return Frames(12);
                string head = Plain(View.Root.Q<Label>("cards-headline").text), body = Plain(View.Root.Q<Label>("cards-body").text);
                Debug.Log($"[240] {def}/{option} {w}x{h}: [{head}] / [{body}]");
                StringAssert.StartsWith("Because the Keeper chose", head, def);
                Assert.IsFalse(body.StartsWith("Because"), def + ": the opening is not said twice: " + body);
                Assert.IsFalse(head.EndsWith("…"), def + ": the headline is not clipped");
                AssertWhole($"{def}/{option} {w}x{h}");
                yield return Shot($"240-callback-{def}-{option}-{w}x{h}");
                seen++;
            }
            Assert.GreaterOrEqual(seen, 3, "enough callbacks were checked");

            // The lapse card: its whole line with the full stop, and the question grey above it.
            if (Model.Mode == CardStackMode.Open) Model.Close();
            var l = Sim.DebugRaiseDilemma("camp_travellers");
            Assert.IsNotNull(l, "camp_travellers holds for the lapse");
            Assert.IsTrue(Sim.DebugLapseDilemma());
            yield return Frames(2);
            Debug.Log("[240] " + CardStackView.DebugOpenEntry(l.resultEntryId));
            Assert.AreEqual(l.resultEntryId, Model.Shown?.lead.id, "the lapse card is up");
            yield return Frames(12);
            string lh = Plain(View.Root.Q<Label>("cards-headline").text);
            StringAssert.EndsWith("themselves.", lh, "with its full stop");
            Assert.IsTrue(Shown(View.Root.Q("cards-teaser")), "the grey question line");
            StringAssert.StartsWith("At the fire:", Plain(View.Root.Q<Label>("cards-teaser").text));
            yield return Shot($"240-lapse-{w}x{h}");
            AssertNoErrors($"callbacks {w}x{h}");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheCallbackStoryIsWholeOnASmallPhone() => CallbackCards(750, 1334);

        [UnityTest, Timeout(300000)]
        public IEnumerator TheCallbackStoryIsWholeOnATallPhone() => CallbackCards(1170, 2532);

        public override void TearDown()
        {
            if (GameClock.Instance != null) { GameClock.Instance.config.dilemmas = false; GameClock.Instance.config.dilemmaLab = false; GameClock.Instance.speed = 1f; }
            base.TearDown();
        }
    }
}
