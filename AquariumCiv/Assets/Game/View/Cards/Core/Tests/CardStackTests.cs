using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.View.Cards.Tests
{
    /// <summary>#75 phase 1: when a stack is due, its pages, what's read and what waits (across a kill), the "Also…" card,
    /// the peek, Give and why it can fail, Show me, the telemetry, and the first run (no stack during it).</summary>
    public class CardStackTests
    {
        const double Day = 24, FewHours = 8;
        double clock;
        DateTime utc = new DateTime(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc);
        MemoryTelemetryStorage disk;

        [SetUp] public void SetUp() { clock = 100; disk = new MemoryTelemetryStorage(); }

        static SimCore Quiet(int seed = 5)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            sim.TermFlavourOn = false;
            sim.CardsSeenToNow(); // the founding lines are not the story of these tests
            return sim;
        }

        static int nextId = 500_000;

        static ChronicleEntry Add(SimCore sim, string kind, float salience = 0.6f, int a = -1, int b = -1)
        {
            sim.State.month++;
            var e = new ChronicleEntry { id = nextId++, month = sim.State.month, kind = kind, text = $"A {kind} happened.", salience = salience, notableId = a, otherNotableId = b };
            sim.State.chronicle.Add(e);
            return e;
        }

        static void AddMany(SimCore sim, int n)
        {
            string[] kinds = { "wedding", "friendship", "storm", "keeper", "invention", "naming", "apprentice", "snow", "festival", "song" };
            for (int i = 0; i < n; i++) Add(sim, kinds[i % kinds.Length], 0.5f + i * 0.01f);
        }

        CardStackModel Model(SimCore sim, TelemetryLog log = null) => new CardStackModel(sim, () => clock, log);

        /// <summary>Opened on a return of exactly <paramref name="cap"/> cards (the cap by time away is 1 / 2 / 3 since #368).</summary>
        static CardStackModel OpenedCap(CardStackModel m, int cap)
        {
            Assert.IsTrue(m.DebugPrepare(cap));
            Assert.IsTrue(m.TapPeek());
            return m;
        }

        static CardStackModel Opened(CardStackModel m, double hours = Day)
        {
            Assert.IsTrue(m.Prepare(hours));
            Assert.IsTrue(m.TapPeek());
            return m;
        }

        // ---------- when a stack is due ----------
        [Test]
        public void NoPeekOrCardsUnderFifteenMinutesAndTheCardsWait()
        {
            var sim = Quiet();
            AddMany(sim, 2);
            var m = Model(sim);
            Assert.IsFalse(m.Prepare(0.2));
            Assert.AreEqual(CardStackMode.Hidden, m.Mode);
            Assert.IsFalse(m.Peek.visible);
            Assert.IsTrue(m.Prepare(CardStackModel.MinAwayHours));
            Assert.AreEqual(CardStackMode.Peek, m.Mode);
        }

        [Test]
        public void TheCapFollowsTimeAwayAndTheRestGoToAlso()
        {
            foreach (var (hours, cap) in new[] { (0.5, 1), (FewHours, 2), (Day, 3) })
            {
                var sim = Quiet();
                AddMany(sim, 10);
                var m = Opened(Model(sim), hours);
                Assert.AreEqual(cap, m.CardCount, $"{hours} h");
                Assert.AreEqual(10 - cap, m.AlsoCount);
                Assert.AreEqual(cap + 1, m.PageCount, "the page dots count the Also… page");
            }
        }

        [Test]
        public void AReturnAfterEightHoursLeadsWithGoodNewsAndEndsOnTheWish()
        {
            var sim = Quiet();
            var dead = sim.State.notables[0];
            Add(sim, "death", 0.95f, dead.id);
            Add(sim, "wedding", 0.6f);
            Add(sim, "storm", 0.7f);
            sim.DebugForceWish(SimCore.NudgeKind.Rain);
            var m = OpenedCap(Model(sim), 5);
            var kinds = new List<CardKind>();
            var tones = new List<CardTone>();
            for (bool more = true; more; more = m.Next()) { kinds.Add(m.Face.kind); tones.Add(m.Face.tone); }
            Assert.AreEqual(CardTone.Good, tones[0]);
            Assert.AreEqual(CardKind.Legend, kinds[kinds.Count - 2], "the death comes after the good news");
            Assert.AreEqual(CardKind.Wish, kinds[kinds.Count - 1]);
        }

        // ---------- reading ----------
        [Test]
        public void NextAndBackPageAndPastTheLastPageTheStackCloses()
        {
            var sim = Quiet();
            AddMany(sim, 3);
            var m = Opened(Model(sim));
            Assert.IsFalse(m.CanGoBack);
            Assert.IsFalse(m.Back());
            var first = m.Face.id;
            Assert.IsTrue(m.Next());
            Assert.AreEqual(1, m.Index);
            Assert.IsTrue(m.Back());
            Assert.AreEqual(first, m.Face.id);
            m.Next(); m.Next();
            Assert.IsTrue(m.OnLastPage);
            Assert.IsFalse(m.Next());
            Assert.AreEqual(CardStackMode.Hidden, m.Mode);
            Assert.IsNull(m.Face);
            Assert.IsFalse(m.HistoryDot);
            Assert.IsFalse(m.Prepare(Day), "everything read: nothing is due");
        }

        [Test]
        public void SkipAllLeavesTheUnreadForTheNextOpenAndReadCardsDontComeBack()
        {
            var sim = Quiet();
            AddMany(sim, 4);
            var m = Opened(Model(sim));
            string first = m.Face.id;
            m.Next();
            m.SkipAll();
            Assert.AreEqual(CardStackMode.Hidden, m.Mode);
            Assert.IsTrue(m.HistoryDot, "a dot, the cards wait");

            Opened(m);
            Assert.AreEqual(3, m.CardCount, "the skipped ones are all back");
            var ids = new List<string>();
            for (bool more = true; more; more = m.Next()) ids.Add(m.Face.id);
            Assert.IsFalse(ids.Contains(first));
        }

        [Test]
        public void ACardLookedAtForAMomentCountsOnlyAfterALook()
        {
            var sim = Quiet();
            AddMany(sim, 2);
            var m = Opened(Model(sim));
            clock += CardStackModel.MinReadSeconds / 2;
            m.SkipAll();
            Assert.AreEqual(2, sim.UnreadCards(Day).cards.Count, "a flick past reads nothing");
            Opened(m);
            clock += CardStackModel.MinReadSeconds;
            m.Close();
            Assert.AreEqual(1, sim.UnreadCards(Day).cards.Count, "a real look does");
        }

        [Test]
        public void AlsoIsTheLastPageItsLinesOpenFullCardsAndPassingItRetiresTheOverflow()
        {
            var sim = Quiet();
            AddMany(sim, 9);
            var m = OpenedCap(Model(sim), 3); // 3 cards, 6 past the cap
            m.Next(); m.Next(); m.Next();
            Assert.IsTrue(m.OnAlsoPage);
            var face = m.Face;
            Assert.IsTrue(face.isAlso);
            Assert.AreEqual(3, face.alsoLines.Count);
            Assert.AreEqual(3, face.alsoMore);
            Assert.IsFalse(face.canShowMe);

            Assert.IsTrue(m.OpenAlso(1));
            Assert.AreEqual(face.alsoLines[1], m.Face.headline);
            Assert.IsNotNull(m.Detail);
            Assert.IsTrue(m.Back());
            Assert.IsTrue(m.OnAlsoPage);
            Assert.IsFalse(m.OpenAlso(3), "only the three lines are tappable");

            Assert.IsFalse(m.Next());
            Assert.IsFalse(sim.HasUnreadCards);
        }

        [Test]
        public void ANewEntryOfARememberedStoryComesBackWithTheOldOneBehindIt()
        {
            var sim = Quiet();
            var a = sim.State.notables[0]; var b = sim.State.notables[1];
            var wedding = Add(sim, "wedding", 0.7f, a.id, b.id);
            var t = new StoryThread { id = 77, source = ThreadSource.Link, month = wedding.month };
            sim.State.threads.Add(t);
            wedding.threadId = t.id;
            var m = Opened(Model(sim));
            m.Next();
            var birth = Add(sim, "birth", 0.7f, a.id);
            birth.threadId = t.id;

            Opened(m);
            Assert.AreEqual(1, m.CardCount);
            Assert.AreEqual(1, m.Shown.entries.Count);
            Assert.AreEqual(birth.id, m.Shown.lead.id);
            Assert.AreEqual(wedding.id, m.Shown.before?.id);
        }

        // ---------- across a kill ----------
        [Test]
        public void WhatsReadSurvivesAKillMidStack()
        {
            var sim = Quiet();
            AddMany(sim, 4);
            var m = Opened(Model(sim));
            string first = m.Face.id;
            m.Next();
            // No close: the app dies with the stack open.
            string path = Path.Combine(Path.GetTempPath(), "cards-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                SaveStore.Write(path, sim.State, utc);
                Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
                var back = new SimCore(sim.Config, e.state);
                var after = Opened(Model(back));
                Assert.AreEqual(3, after.CardCount);
                for (bool more = true; more; more = after.Next()) Assert.AreNotEqual(first, after.Face.id);
            }
            finally { File.Delete(path); File.Delete(path + ".bak"); }
        }

        [Test]
        public void ASaveFromBeforeTheLedgerStartsNowNotAtTheFounding()
        {
            var sim = Quiet();
            AddMany(sim, 6);
            sim.State.cards = new CardLedger(); // what an old save loads as
            var back = new SimCore(sim.Config, sim.State);
            Assert.AreEqual(sim.State.month, back.State.cards.since);
            Assert.IsFalse(back.HasUnreadCards);
            Add(back, "wedding");
            Assert.IsTrue(back.HasUnreadCards, "the time away is still told");
        }

        [Test]
        public void TheLedgerStaysAsShortAsWhatsWaiting()
        {
            var sim = Quiet();
            for (int round = 0; round < 8; round++)
            {
                AddMany(sim, 5);
                var m = Opened(Model(sim), Day);
                for (bool more = true; more; more = m.Next()) { }
                sim.State.month++;
                sim.CompactCardLedger();
                Assert.LessOrEqual(sim.State.cards.read.Count, 1, $"round {round}");
            }
            // Something skipped holds the ledger back at that card, and nothing is lost.
            Add(sim, "wedding");
            var last = Add(sim, "song");
            sim.State.month += 3;
            var s = Opened(Model(sim));
            s.Next();
            s.SkipAll();
            Assert.Less(sim.State.cards.since, last.month);
            Assert.IsTrue(sim.HasUnreadCards);
        }

        // ---------- the peek ----------
        [Test]
        public void ThePeekIsTheTopCardAndTappingItOpensTheStack()
        {
            var sim = Quiet();
            var who = sim.State.notables[0];
            Add(sim, "wedding", 0.7f, who.id, sim.State.notables[1].id);
            var m = Model(sim);
            m.Prepare(Day);
            Assert.IsTrue(m.Peek.visible);
            Assert.IsFalse(m.Peek.isTeaser);
            Assert.AreEqual(who.id, m.Peek.notableId);
            Assert.That(m.Peek.text, Does.Contain("married"));
            Assert.IsTrue(m.TapPeek());
            Assert.AreEqual(CardStackMode.Open, m.Mode);
            Assert.AreEqual(m.Peek.visible, false);
        }

        [Test]
        public void HidingThePeekLeavesADotAndTheCardsWait()
        {
            var sim = Quiet();
            AddMany(sim, 2);
            var m = Model(sim);
            m.Prepare(Day);
            m.HidePeek();
            Assert.AreEqual(CardStackMode.Hidden, m.Mode);
            Assert.IsFalse(m.Peek.visible);
            Assert.IsTrue(m.HistoryDot);
            Assert.IsFalse(m.TapPeek());
            Assert.IsTrue(m.Reopen());
            Assert.AreEqual(2, m.CardCount);
        }

        [Test]
        public void WithNothingNewThePeekIsTheTeaserAndOpensNothing()
        {
            var sim = Quiet();
            string line = sim.DebugOpenTeaser();
            Assume.That(line, Is.Not.Null.And.Not.Empty, "this box has something to tease");
            var m = Model(sim);
            Assert.IsFalse(m.Prepare(Day));
            Assert.AreEqual(CardStackMode.Peek, m.Mode);
            Assert.IsTrue(m.Peek.isTeaser);
            Assert.AreEqual(sim.CurrentTeaser, m.Peek.text);
            Assert.IsFalse(m.TapPeek());
        }

        [Test]
        public void WithNothingAtAllThereIsNoPeek()
        {
            var sim = Quiet();
            var m = Model(sim);
            Assert.IsFalse(m.Prepare(Day));
            Assert.AreEqual(CardStackMode.Hidden, m.Mode);
        }

        // ---------- faces ----------
        [Test]
        public void AFaceCarriesTheTeaserItPaysOffAndTheKindsButton()
        {
            var sim = Quiet();
            var a = sim.State.notables[0];
            var e = Add(sim, "wedding", 0.7f, a.id);
            sim.State.threads.Add(new StoryThread { id = 88, source = ThreadSource.Couple, teaser = "Mira and Tob keep sitting together.", month = 0, state = ThreadState.PaidOff, payoffEntryId = e.id });
            e.threadId = 88;
            var d = Add(sim, "death", 0.9f, sim.State.notables[1].id);
            var m = Opened(Model(sim));
            Assert.AreEqual("At the fire: Mira and Tob keep sitting together.", m.Face.teaserLine);
            Assert.AreEqual(CardAction.None, m.Face.action);
            Assert.That(m.Face.footer, Does.StartWith("Year "));
            Assert.AreEqual(a.id, m.Face.portraits[0]);
            m.Next();
            Assert.AreEqual(d.id, m.Face.card.lead.id);
            Assert.AreEqual(CardAction.Remember, m.Face.action);
            Assert.AreEqual(d.notableId, m.Remember());
        }

        [Test]
        public void TheBodyDoesntRepeatTheHeadline()
        {
            var sim = Quiet();
            var e = Add(sim, "song", 0.7f);
            e.text = "The miller wrote a song. Nobody asked him to.";
            var m = Opened(Model(sim));
            Assert.AreEqual("The miller wrote a song", m.Face.headline);
            Assert.AreEqual("Nobody asked him to.", m.Face.body);
        }

        [Test]
        public void AWishHasAShortHeadlineAndItsWholeLineAsTheBody()
        {
            var sim = WithWish(out var w);
            var m = Opened(Model(sim));
            Assert.AreEqual(sim.FindNotable(w.notableId).name + " has a wish", m.Face.headline);
            Assert.That(m.Face.body, Does.Contain("rain"));
        }

        [Test]
        public void WatchClosesTheStackAndReopenBringsTheRestBack()
        {
            var sim = Quiet();
            AddMany(sim, 2);
            sim.ForceEraReady();
            var m = Opened(Model(sim));
            Assert.AreEqual(CardKind.EraReady, m.Face.kind);
            Assert.AreEqual(CardAction.Watch, m.Face.action);
            Assert.IsTrue(m.Watch());
            Assert.AreEqual(CardStackMode.Hidden, m.Mode);
            Assert.IsTrue(m.Reopen());
            Assert.AreEqual(3, m.CardCount, "the era still waits, the two stories are unread");
        }

        [Test]
        public void TheEraWaitsAcrossReturnsUntilItComesThenItsCardIsGone()
        {
            var sim = Quiet();
            sim.ForceEraReady();
            var m = Opened(Model(sim));
            Assert.AreEqual(CardKind.EraReady, m.Face.kind);
            Assert.IsFalse(m.Next());
            Assert.IsFalse(sim.HasUnreadCards, "read: no dot");
            Assert.IsTrue(m.Prepare(Day), "read, but the era still waits");
            m.HidePeek();
            sim.AdvanceEra();
            sim.State.cards.since = -1;
            Assert.IsFalse(sim.UnreadCards(Day).cards.Any(c => c.lead.kind == "era-ready"), "a past era's waiting line is stale");
        }

        // ---------- the wish ----------
        static SimCore WithWish(out Wish w, string kind = SimCore.NudgeKind.Rain)
        {
            var sim = Quiet();
            sim.State.shapingCharges = sim.ShapingMaxCharges;
            w = sim.DebugForceWish(kind);
            Assert.IsNotNull(w);
            return sim;
        }

        [Test]
        public void GiveClosesTheStackForTheToolAndTheWishStaysOpenUntilPlaced()
        {
            var sim = WithWish(out var w);
            var m = Opened(Model(sim));
            Assert.AreEqual(CardAction.Give, m.Face.action);
            Assert.IsTrue(m.Face.actionEnabled);
            var o = m.Give();
            Assert.IsTrue(o.ok);
            Assert.AreEqual("", o.line);
            Assert.AreSame(w, o.wish);
            Assert.AreEqual(CardStackMode.Hidden, m.Mode, "Give leads into the shaping");
            Assert.AreEqual(WishState.Open, w.state, "cancelling the tool costs nothing");
            Assert.IsTrue(m.Prepare(Day), "still open: it comes back");
            m.HidePeek();
            Assert.IsTrue(sim.GiveWish(w.id).Ok);
            Assert.IsFalse(m.Prepare(Day), "given: the card stops");
        }

        [Test]
        public void GiveWithNoChargeSaysWhyOnTheCardAndTheWishStaysOpen()
        {
            var sim = WithWish(out var w);
            sim.State.shapingCharges = 0;
            var m = Opened(Model(sim));
            Assert.IsFalse(m.Face.actionEnabled);
            Assert.That(m.Face.actionNote, Does.Contain("cleaning the glass"));
            var o = m.Give();
            Assert.IsFalse(o.ok);
            Assert.AreEqual(SimCore.ShapeFail.NoCharge, o.fail);
            Assert.That(o.line, Does.Contain("cleaning the glass"));
            Assert.AreEqual(WishState.Open, w.state);
        }

        [Test]
        public void GiveAtTheCapOrWithALockedToolSaysSo()
        {
            var sim = WithWish(out var w, SimCore.NudgeKind.Channel);
            var m = Opened(Model(sim));
            var why = sim.CanGiveWish(w.id);
            Assert.AreEqual(SimCore.ShapeFail.Locked, why, "the channel comes with the village");
            Assert.That(m.Face.actionNote, Does.Contain("comes with the village"));
            Assert.AreEqual(sim.ShapeFailLine(w.kind, SimCore.ShapeFail.AtCap), $"That's as many as the box holds ({SimCore.ShapingCap(w.kind)} at once).");
        }

        [Test]
        public void AWishThatLapsedWhileTheStackWasUpSaysItHasPassed()
        {
            var sim = WithWish(out var w);
            var m = Opened(Model(sim));
            w.state = WishState.Lapsed;
            Assert.IsFalse(m.Face.actionEnabled);
            var o = m.Give();
            Assert.IsFalse(o.ok);
            Assert.AreEqual("That wish has passed.", o.line);
        }

        [Test]
        public void AnUngivenWishComesBackOnTheNextReturnUntilItLapses()
        {
            var sim = WithWish(out var w);
            var m = Opened(Model(sim));
            Assert.IsFalse(m.Next());
            Assert.IsFalse(m.HistoryDot, "read: no dot for it");
            Assert.IsTrue(m.Prepare(Day), "no timer: it just stops once given");
            m.HidePeek();
            w.state = WishState.Lapsed;
            Assert.IsFalse(m.Prepare(Day));
        }

        [Test]
        public void GiveOnAnotherKindOfCardDoesNothing()
        {
            var sim = Quiet();
            AddMany(sim, 1);
            var m = Opened(Model(sim));
            Assert.IsFalse(m.Give().ok);
            Assert.AreEqual(-1, m.Remember());
            Assert.IsFalse(m.Watch());
        }

        // ---------- Show me ----------
        [Test]
        public void ShowMeWorksPerCardTheStackStaysOpenAndClosingEndsIt()
        {
            var sim = Quiet();
            Add(sim, "wedding", 0.7f, sim.State.notables[0].id, sim.State.notables[1].id);
            Add(sim, "storm", 0.6f);
            var m = Opened(Model(sim));
            int ended = 0;
            m.ShowMeEnd += () => ended++;
            Assert.IsTrue(m.Face.canShowMe);
            var card = m.ShowMe();
            Assert.AreSame(m.Shown, card);
            Assert.AreEqual(CardStackMode.Open, m.Mode);
            Assert.IsTrue(sim.IsCardRead(card), "Show me reads the card");
            m.ShowMeLanded();
            Assert.IsTrue(m.Next());
            Assert.AreEqual(0, ended, "the camera goes back when the stack closes");
            m.SkipAll();
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void WithoutShowMeClosingAsksNothingOfTheCamera()
        {
            var sim = Quiet();
            AddMany(sim, 1);
            var m = Opened(Model(sim));
            int ended = 0;
            m.ShowMeEnd += () => ended++;
            m.Next();
            Assert.AreEqual(0, ended);
        }

        // ---------- telemetry ----------
        List<TelemetryEvent> Events(TelemetryLog log)
        {
            log.Flush();
            return disk.ReadAll($"box-{log.BoxId}.jsonl").Split('\n').Where(l => l.Length > 0).Select(TelemetryEvent.Parse).ToList();
        }

        [Test]
        public void TheStackTellsTelemetryWhatHappened()
        {
            var sim = Quiet();
            Add(sim, "wedding", 0.7f, sim.State.notables[0].id, sim.State.notables[1].id);
            Add(sim, "song", 0.6f);
            Add(sim, "snow", 0.55f);
            var log = new TelemetryLog(disk, () => utc, "t");
            var m = Model(sim, log);
            m.Prepare(Day);
            m.TapPeek();
            clock += 4;
            m.OpenPerson(sim.State.notables[0].id);
            clock += 100;                          // the person card covers it: not read time
            m.PersonClosed();
            m.ShowMe();
            clock += 1;
            m.ShowMeLanded();
            clock += 2;
            m.Next();
            m.Back();                              // shown once, however often it's paged to
            m.Next();
            m.SkipAll();
            var ev = Events(log);
            int N(string name) => ev.Count(e => e.name == name);
            Assert.AreEqual(1, N(Ev.PeekTap));
            Assert.AreEqual(2, N(Ev.CardShown));
            Assert.AreEqual(1, N(Ev.CardOpened));
            Assert.AreEqual(0, N(Ev.ShowMe), "ShowMeDirector logs it, once");
            Assert.AreEqual(1, N(Ev.SkipAll));
            Assert.AreEqual(2, ev.First(e => e.name == Ev.SkipAll).Num("left"), "the skipped one and the one never reached");
            var read = ev.First(e => e.name == Ev.CardRead);
            Assert.AreEqual(6000, read.Num("dwell_ms"), 1, "the covers don't count");
        }

        [Test]
        public void ThePauseStopsTheReadTimeAndAShortAbsenceResumesTheStack()
        {
            var sim = Quiet();
            AddMany(sim, 2);
            var log = new TelemetryLog(disk, () => utc, "t");
            var m = Opened(Model(sim, log));
            clock += 2;
            m.OnPause();
            clock += 500;
            Assert.IsFalse(m.Prepare(0.1));
            Assert.AreEqual(CardStackMode.Open, m.Mode);
            clock += 1;
            m.Next();
            var reads = Events(log).Where(e => e.name == Ev.CardRead).Select(e => e.Num("dwell_ms")).ToList();
            CollectionAssert.AreEqual(new double[] { 2000, 1000 }, reads);
        }

        [Test]
        public void ALongAbsenceWithTheStackUpStartsAFreshReturn()
        {
            var sim = Quiet();
            AddMany(sim, 2);
            var m = Opened(Model(sim));
            m.Next();
            m.OnPause();
            AddMany(sim, 1);
            Assert.IsTrue(m.Prepare(Day));
            Assert.AreEqual(CardStackMode.Peek, m.Mode);
            Assert.AreEqual(2, m.CardCount, "the one read is gone, the new one came");
        }

        // ---------- the first run ----------
        [Test]
        public void NoStackDuringTheFirstRunAndItsStoriesAreNotToldTwice()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(9, cfg));
            var run = FirstRun.Begin(sim);
            var m = Model(sim);
            Assert.IsFalse(m.Prepare(Day));
            Assert.IsFalse(m.Peek.visible);
            Assert.IsFalse(m.Reopen());

            while (run.Active) run.SkipStep();
            long end = sim.State.month;
            Assert.Greater(end, 0, "the first year played");
            Assert.IsFalse(sim.HasUnreadCards, "the first run told them");

            sim.Advance(3);
            var cards = sim.UnreadCards(Day).cards;
            Assert.Greater(cards.Count, 0);
            Assert.IsTrue(cards.All(c => c.entries.All(e => e.month > end)), "only what came after the run");
        }

        [Test]
        public void TheFirstRunShowsNoDotAndItsEndLeavesNone()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(9, cfg));
            var run = FirstRun.Begin(sim);
            var m = Model(sim);
            Assert.IsTrue(sim.HasUnreadCards, "the founding stories count");
            Assert.IsFalse(m.HistoryDot, "not while the first run lasts");
            Assert.IsFalse(m.Reopen());
            while (run.Active) run.SkipStep();
            Assert.IsFalse(m.HistoryDot, "the run told them");
            sim.Advance(3);
            m.Prepare(Day);
            m.HidePeek();
            Assert.IsTrue(m.HistoryDot, "real cards after the run: the dot as usual");
        }

        [Test]
        public void SkippingTheFirstRunAlsoStartsTheLedgerNow()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(9, cfg));
            var run = FirstRun.Begin(sim);
            run.SkipAll();
            Assert.IsFalse(sim.HasUnreadCards);
        }

        [Test]
        public void AFinishedBoxHasNoStack()
        {
            var sim = Quiet();
            AddMany(sim, 2);
            sim.State.boxComplete = true;
            Assert.IsFalse(Model(sim).Prepare(Day));
        }

        // ---------- story only ----------
        [Test]
        public void ReadingCardsNeverTouchesTheEconomy()
        {
            var cfg = new SimConfig();
            var a = new SimCore(cfg, SimCore.NewWorld(11, cfg));
            var b = new SimCore(cfg, SimCore.NewWorld(11, cfg));
            var m = Model(b);
            for (int y = 0; y < 12; y++)
            {
                a.Advance(1); b.Advance(1);
                if (m.Prepare(Day) && m.TapPeek()) { m.ShowMe(); m.Next(); m.SkipAll(); }
                else m.HidePeek();
            }
            Assert.AreEqual(a.State.population, b.State.population);
            Assert.AreEqual(a.State.food, b.State.food);
            Assert.AreEqual(a.State.buildings.Count, b.State.buildings.Count);
            Assert.AreEqual(a.State.chronicle.Count, b.State.chronicle.Count);
        }

        [Test]
        public void TheDebugClearMakesEveryStoryUnreadAgain()
        {
            var sim = Quiet();
            AddMany(sim, 2);
            var m = Opened(Model(sim));
            m.Next(); m.Next();
            Assert.IsFalse(sim.HasUnreadCards);
            sim.DebugClearCardsRead();
            Assert.IsTrue(sim.HasUnreadCards);
            Assert.IsTrue(m.DebugPrepare(1));
            Assert.AreEqual(1, m.CardCount);
        }

        // ---------- the dilemma card (#223) ----------
        static SimCore WithDilemma(string defId, out Dilemma d)
        {
            var sim = Quiet();
            d = sim.DebugRaiseDilemma(defId);
            Assert.IsNotNull(d, defId);
            return sim;
        }

        [Test]
        public void ADilemmaCardComesRightAfterTheEraCardBeforeTheStories()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            AddMany(sim, 3);
            sim.ForceEraReady();
            var m = Opened(Model(sim));
            var kinds = new List<CardKind>();
            do kinds.Add(m.Face.kind); while (m.Next());
            Assert.AreEqual(CardKind.EraReady, kinds[0]);
            Assert.AreEqual(CardKind.Dilemma, kinds[1]);
            Assert.AreEqual(1, kinds.Count(k => k == CardKind.Dilemma));
        }

        [Test]
        public void ADilemmaCardReadsTheQuestionAndBothOptionsAndHasNoPrimaryAction()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            var m = Opened(Model(sim));
            var f = m.Face;
            Assert.AreEqual(CardKind.Dilemma, f.kind);
            Assert.AreEqual(sim.ViewOfDilemma(d).question, f.headline);
            Assert.AreEqual(2, f.dilemma.options.Length);
            Assert.AreEqual("a place by the fire", f.dilemma.options[0].label);
            Assert.That(f.dilemma.options[1].hint, Is.Not.Empty);
            Assert.AreEqual(-1, f.dilemmaChosen);
            Assert.AreEqual(CardAction.None, f.action);
        }

        [Test]
        public void AnsweringByButtonCallsAnswerDilemmaOnTheCardPathAndTheCardSaysWhatWasChosen()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            var m = Opened(Model(sim));
            Assert.IsTrue(m.AnswerDilemma(1));
            Assert.AreEqual(DilemmaState.Answered, d.state);
            Assert.AreEqual(1, d.option);
            Assert.AreEqual(DilemmaPath.Card, d.path);
            Assert.AreEqual(1, m.Face.dilemmaChosen, "the card stays up and says so");
            Assert.IsFalse(m.Face.dilemmaLapsed);
            Assert.IsNotNull(sim.State.chronicle.Find(e => e.id == d.answerEntryId), "the Keeper's line is written");
            Assert.AreEqual(CardKind.Dilemma, m.Face.kind, "still this card until Next");
        }

        [Test]
        public void ADilemmaAnswerIsFinalNoSecondAnswerNoOtherOption()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            var m = Opened(Model(sim));
            Assert.IsFalse(m.AnswerDilemma(2), "only options 0 and 1");
            Assert.IsTrue(m.AnswerDilemma(0));
            Assert.IsFalse(m.AnswerDilemma(1), "no undo");
            Assert.AreEqual(0, d.option);
        }

        [Test]
        public void AnsweringReadsTheCardNextMovesOnAndTheDilemmaCardIsGoneNextTime()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            AddMany(sim, 2);
            var m = Opened(Model(sim));
            Assert.AreEqual(CardKind.Dilemma, m.Face.kind);
            m.AnswerDilemma(0);
            Assert.IsTrue(m.Next());
            Assert.AreNotEqual(CardKind.Dilemma, m.Face.kind);
            m.SkipAll();
            AddMany(sim, 2); // t4-fixes: something new happened while away
            Assert.IsTrue(m.Prepare(Day));
            m.TapPeek();
            var kinds = new List<CardKind>();
            var leads = new List<int>();
            do { kinds.Add(m.Face.kind); if (m.Shown != null) leads.Add(m.Shown.lead.id); } while (m.Next());
            Assert.IsFalse(kinds.Contains(CardKind.Dilemma), "an answered dilemma's question is gone");
            // t4-fixes (tester run 3): the Keeper gave the answer and saw it said: it is read, so a later return never leads with it
            CollectionAssert.DoesNotContain(leads, d.answerEntryId, "the Keeper's own answer is not news on the next return (it stays in History)");
        }

        [Test]
        public void ALapsedDilemmaOnAnOpenCardShowsThePeoplesChoiceAndAnswerIsRefused()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            var m = Opened(Model(sim));
            Assert.IsTrue(sim.DebugLapseDilemma());
            Assert.IsFalse(m.AnswerDilemma(0));
            Assert.IsTrue(m.Face.dilemmaLapsed);
            Assert.AreEqual(d.option, m.Face.dilemmaChosen);
        }

        [Test]
        public void ADilemmaStaysUntilAnsweredEvenWhenTheStackIsSkipped()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            var m = Opened(Model(sim));
            m.Next();
            Assert.IsTrue(m.Prepare(Day), "unanswered: it keeps coming back");
            m.TapPeek();
            Assert.AreEqual(CardKind.Dilemma, m.Face.kind);
        }

        [Test]
        public void ShowMeOnADilemmaCardGoesToTheHookAndOnlyWhenOneIsSet()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            var m = Opened(Model(sim));
            Assert.IsFalse(m.DilemmaShowMe(), "no choice scene yet: the normal Show me stays");
            DilemmaView got = null;
            m.DilemmaShowMeHook = v => { got = v; return true; };
            Assert.IsTrue(m.Face.canShowMe);
            Assert.IsTrue(m.DilemmaShowMe());
            Assert.AreSame(d, got.dilemma);
            m.AnswerDilemma(0);
            Assert.IsFalse(m.DilemmaShowMe(), "answered: nothing to show");
        }

        // ---------- #238: a dilemma raised while the player watches ----------
        [Test]
        public void ALiveDilemmaPeeksOnTopAndATapOpensOnItsCardOnly()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            AddMany(sim, 4); // other unread stories wait for the return
            var m = Model(sim);
            Assert.IsTrue(m.ShowLive(d.entryId));
            Assert.AreEqual(CardStackMode.Peek, m.Mode);
            Assert.AreEqual(sim.Headline(sim.State.chronicle.First(e => e.id == d.entryId)), m.Peek.text);
            Assert.IsTrue(m.Holds(d.entryId));
            Assert.IsFalse(m.ShowLive(d.entryId), "not twice while it is up");
            Assert.IsTrue(m.TapPeek());
            Assert.AreEqual(CardKind.Dilemma, m.Face.kind);
            Assert.AreEqual(1, m.PageCount, "only the dilemma: the stories wait for the return");
            m.Close();
            Assert.IsTrue(m.HistoryDot, "the rest wait: the dot");
        }

        /// <summary>t3-choice (tester run 2 D): a return's peek is up with an older story when the people raise a choice: the peek
        /// switches to the choice and wears the choice mark.</summary>
        [Test]
        public void ALiveDilemmaReplacesAnOlderStoryOnThePeek()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            AddMany(sim, 4);
            var m = Model(sim);
            Assert.IsTrue(m.DebugPrepare(3));
            Assert.AreEqual(CardStackMode.Peek, m.Mode);
            if (m.PeekIs(d.entryId)) Assert.IsFalse(m.ShowLive(d.entryId), "already on top");
            else Assert.IsTrue(m.ShowLive(d.entryId), "the peek switches to the new choice");
            Assert.IsTrue(m.PeekIs(d.entryId));
            Assert.IsTrue(m.PeekIsChoice, "the peek says a choice waits");
            Assert.AreEqual(sim.Headline(sim.State.chronicle.First(e => e.id == d.entryId)), m.Peek.text);
            Assert.IsFalse(m.ShowLive(d.entryId), "not twice");
            sim.AnswerDilemma(d.id, 0, DilemmaPath.Box);
            Assert.IsFalse(m.PeekIsChoice, "answered in the box: the mark goes");
            Assert.IsTrue(m.TapPeek());
            Assert.AreEqual(CardKind.Dilemma, m.Face.kind);
            Assert.IsFalse(m.ShowLive(d.entryId), "never over the open stack");
        }

        [Test]
        public void NoLivePeekInTheFirstRunOrForAnAnsweredDilemma()
        {
            var sim = WithDilemma("camp_travellers", out var d);
            var m = Model(sim);
            sim.State.firstRun.active = true;
            Assert.IsFalse(m.ShowLive(d.entryId), "the first run tells its own cards");
            sim.State.firstRun.active = false;
            sim.AnswerDilemma(d.id, 0, DilemmaPath.Card);
            Assert.IsFalse(m.ShowLive(d.entryId), "answered: nothing to ask");
            Assert.AreEqual(CardStackMode.Hidden, m.Mode);
        }

        [Test]
        public void TheDotIsWorkedOutAgainLive()
        {
            var sim = Quiet();
            var m = Model(sim);
            Assert.IsFalse(m.HistoryDot);
            Add(sim, "wedding", 0.8f);
            Assert.IsFalse(m.HistoryDot, "not until it is worked out");
            m.RefreshDot();
            Assert.IsTrue(m.HistoryDot);
        }

        // ---------- #240: the callback card ----------
        [Test]
        public void ACallbacksHeadlineIsItsCreditAndTheBodyGoesOnWithoutRepeatingIt()
        {
            var sim = Quiet();
            sim.State.month += 3; // past the founding months the test box counts as seen
            var d = sim.DebugRaiseDilemma("camp_remember");
            Assert.IsNotNull(d);
            sim.AnswerDilemma(d.id, 1, DilemmaPath.Card);
            var e = sim.DebugFireCallbackNow();
            Assert.IsNotNull(e);
            var m = Model(sim);
            Assert.IsTrue(m.DebugPrepare(16) && m.TapPeek());
            for (int i = 0; i < 16 && m.Shown?.lead.id != e.id; i++) m.Next();
            var f = m.Face;
            Assert.AreEqual(e.id, f.card.lead.id);
            Assert.AreEqual("Because the Keeper chose the tree", f.headline);
            Assert.IsFalse(f.body.StartsWith("Because"), f.body);
            Assert.IsTrue(char.IsUpper(f.body[0]), "the body reads as a sentence: " + f.body);
            StringAssert.StartsWith("At the fire: ", f.teaserLine, "the question stays as the grey line");
        }

        /// <summary>t2-polish (the peek said "Esk married Aveline" for a wedding never shown): boxes lived the way the game lives them
        /// (watched minutes with the director handing moments over, the view staging some and failing to cast others, then an away
        /// through CatchUp + BeginVisit and the peek's Prepare). After every away, the peek and the whole unread stack never hold a line
        /// whose ceremony waits or plays: no wedding, birth or new chief is read before it is watched.</summary>
        [Test]
        public void AfterAnyAwayThePeekNeverHoldsACeremonyThatWaitsOrPlays()
        {
            int checkedStacks = 0, deferred = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var cfg = new SimConfig { moments = true, marriedBirths = true };
                var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                var m = Model(sim);
                for (int visit = 0; visit < 5; visit++)
                {
                    for (int min = 0; min < 6; min++)
                    {
                        sim.Advance(1 / 60.0);
                        foreach (var h in sim.StartDueMoments())
                        {
                            // the view: a couple out hunting can't be cast the first times (it waits again), the rest play
                            if (h.deferrals < 2 && (h.id + visit) % 2 == 0) { sim.DeferMoment(h.id); deferred++; }
                            else sim.FinishMoment(h.id, true);
                        }
                    }
                    double away = visit % 2 == 0 ? 1 : 8;
                    sim.CatchUp(away);
                    sim.BeginVisit();
                    m.Prepare(away);
                    var all = sim.UnreadCardsCapped(64);
                    foreach (var c in all.cards.Concat(all.also))
                        foreach (var e in c.entries)
                            Assert.IsFalse(sim.MomentStillHeld(e), $"seed {seed} visit {visit}: '{sim.Headline(e)}' is a card while its ceremony waits");
                    if (m.Mode == CardStackMode.Peek && !m.Peek.isTeaser)
                    {
                        var held = sim.State.moments.Where(x => x.state == HeldMomentState.Waiting || x.state == HeldMomentState.Playing)
                            .Select(x => sim.State.chronicle.FirstOrDefault(e => e.id == x.entryId)).Where(e => e != null).Select(sim.Headline);
                        CollectionAssert.DoesNotContain(held.ToList(), m.Peek.text, $"seed {seed} visit {visit}: the peek reads a held ceremony");
                    }
                    checkedStacks++;
                    m.HidePeek();
                }
            }
            Assert.AreEqual(60, checkedStacks);
            Assert.Greater(deferred, 0, "some moments could not be staged at first");
        }
    }
}
