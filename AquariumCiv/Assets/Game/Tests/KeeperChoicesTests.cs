using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Replay;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #373 (visit loop C6; visit-loop.md 2.5): "The Keeper's choices" page of the book, one section per era, and the hand pins on the
    /// time-lapse. The shaping is pure (<see cref="KeeperChoices"/>); the sim tests check it reads the real dilemmas. Pure C#, so it
    /// also runs in Tools/SimTests.
    /// </summary>
    public class KeeperChoicesTests
    {
        static KeeperChoiceInput In(int id, Era era, long month, DilemmaEnd end, string chosen = "the woods", string question = "The fields, or the woods?", params string[] touched)
        {
            var i = new KeeperChoiceInput { dilemmaId = id, era = era, endMonth = month, raisedHours = month, question = question, chosen = chosen, end = end, answerEntryId = 1000 + id };
            i.touched.AddRange(touched);
            return i;
        }

        [Test]
        public void TheWordsSayWhoChose()
        {
            Assert.AreEqual("The Keeper chose the woods.", KeeperChoices.ChosenLine(DilemmaEnd.Answered, "the woods"));
            Assert.AreEqual("The Keeper let the people decide: the woods.", KeeperChoices.ChosenLine(DilemmaEnd.LetThemDecide, "the woods"));
            Assert.AreEqual("The people chose the woods.", KeeperChoices.ChosenLine(DilemmaEnd.Visits, "the woods"));
            Assert.AreEqual("The people chose the woods.", KeeperChoices.ChosenLine(DilemmaEnd.Gone, "the woods"));
        }

        [Test]
        public void AnsweredAndLetThemDecideAreCreditedALapseIsNot()
        {
            var a = KeeperChoices.LineOf(In(1, Era.Camp, 20, DilemmaEnd.Answered));
            var l = KeeperChoices.LineOf(In(2, Era.Camp, 30, DilemmaEnd.LetThemDecide));
            var p = KeeperChoices.LineOf(In(3, Era.Camp, 40, DilemmaEnd.Visits));
            Assert.IsTrue(a.credited); Assert.IsTrue(l.credited); Assert.IsFalse(p.credited);
            Assert.AreEqual(1001, a.answerEntryId);
            Assert.AreEqual(1002, l.answerEntryId);
            Assert.AreEqual(-1, p.answerEntryId, "a lapse has no Keeper line, so no hand pin");
            Assert.AreEqual("The fields, or the woods? The Keeper chose the woods.", a.pinText);
            Assert.AreEqual("", p.pinText);
        }

        [Test]
        public void TouchedAndPropLinesAreShortAndClosedWorld()
        {
            var l = KeeperChoices.LineOf(In(1, Era.Camp, 20, DilemmaEnd.Answered, "Ada as chief", "Who leads next: Ada or Bo?", "Ada Stone", "Bo Reed"));
            l.propLine = KeeperChoices.PropLine(ConsequenceKind.Feather, true);
            Assert.AreEqual("It touched Ada Stone and Bo Reed.", l.touchedLine);
            Assert.AreEqual("It left a feather on a post in the box.", l.propLine);
            Assert.AreEqual("It touched Ada Stone.", KeeperChoices.TouchedLine(new[] { "Ada Stone", "Ada Stone", "" }));
            Assert.AreEqual("It touched A, B and C.", KeeperChoices.TouchedLine(new[] { "A", "B", "C" }));
            Assert.AreEqual("", KeeperChoices.TouchedLine(new string[0]));
            Assert.AreEqual("", KeeperChoices.PropLine("", true), "a choice that left nothing says nothing");
            Assert.AreEqual("It will leave a stew pot on stones in the box.", KeeperChoices.PropLine(ConsequenceKind.StewPot, false));
            foreach (var line in new[] { l.chosenLine, l.touchedLine, l.propLine })
                foreach (var word in new[] { "glass", "tank", "game", "outside" })
                    StringAssert.DoesNotContain(word, line.ToLowerInvariant());
        }

        [Test]
        public void EveryPropKindHasAName()
        {
            foreach (var k in ConsequenceKind.All)
            {
                string n = KeeperChoices.PropName(k);
                Assert.IsNotEmpty(n, k);
                StringAssert.DoesNotContain("_", n, k + " reads as words, not an id");
            }
            Assert.AreEqual(ConsequenceKind.All.Length, ConsequenceKind.All.Select(KeeperChoices.PropName).Distinct().Count(), "no two things share a name");
        }

        [Test]
        public void TheAskIsCutShort()
        {
            Assert.AreEqual("Who leads next: Ada or Bo?", KeeperChoices.ShortAsk("The chief is getting old. Who leads next: Ada or Bo?"));
            Assert.AreEqual("The fields, or the woods?", KeeperChoices.ShortAsk("The fields, or the woods?"));
            Assert.AreEqual("Ada and Bo have a baby.", KeeperChoices.ShortAsk("Ada and Bo have a baby. Names follow."), "no question: the first sentence");
            string longAsk = KeeperChoices.ShortAsk("Strangers came to the edge of the camp with their children and their goats, asking to stay with us through the cold season?");
            Assert.LessOrEqual(longAsk.Length, KeeperChoices.AskMax);
            StringAssert.EndsWith("…", longAsk);
            Assert.AreEqual("", KeeperChoices.ShortAsk(""));
        }

        [Test]
        public void SectionsGoByEraInOrderAndChoicesByTimeInside()
        {
            var secs = KeeperChoices.Build(new[]
            {
                In(5, Era.Town, 900, DilemmaEnd.Answered),
                In(2, Era.Camp, 80, DilemmaEnd.LetThemDecide),
                In(4, Era.Village, 400, DilemmaEnd.Visits),
                In(1, Era.Camp, 30, DilemmaEnd.Answered),
                In(3, Era.Village, 400, DilemmaEnd.Answered), // same month: the lower id first
            });
            CollectionAssert.AreEqual(new[] { Era.Camp, Era.Village, Era.Town }, secs.Select(s => s.era).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, secs[0].lines.Select(l => l.dilemmaId).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 4 }, secs[1].lines.Select(l => l.dilemmaId).ToArray());
            CollectionAssert.AreEqual(new[] { 5 }, secs[2].lines.Select(l => l.dilemmaId).ToArray());
            Assert.AreEqual(3, secs[0].lines[0].year, "month 30 is year 3 on the plate");
            Assert.IsEmpty(KeeperChoices.Build(null));
            Assert.IsEmpty(KeeperChoices.Build(new KeeperChoiceInput[0]));
        }

        // ---------- the sim ----------
        static SimConfig Cfg() => new SimConfig { dilemmas = true, moments = true };

        static DilemmaDef Def(string id) => new DilemmaDef
        {
            id = id, kind = DilemmaKind.WhichWay, cast = DilemmaCast.None, question = "The elders ask. The fields, or the woods?", shortAsk = "",
            options = new[]
            {
                new DilemmaOptionDef { label = "the fields", hint = "h", prop = ConsequenceKind.Baskets, propAt = PropAnchor.Fire },
                new DilemmaOptionDef { label = "the woods", hint = "h", prop = ConsequenceKind.CharmPole, propAt = PropAnchor.Edge },
            },
        };

        static SimCore Raised(int seed)
        {
            var def = Def("c6");
            var sim = NewSim(seed, Cfg());
            sim.DilemmaCatalog = new[] { def };
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Assert.IsNotNull(sim.DebugRaiseDilemma(def.id));
            return sim;
        }

        [Test]
        public void AnAnsweredChoiceIsListedWithItsPropAndGetsAHandPin()
        {
            var sim = Raised(3);
            var d = sim.OpenDilemma;
            Assert.IsEmpty(sim.KeeperChoicesByEra(), "an open choice is not listed yet");
            Assert.IsEmpty(sim.KeeperChoicePins());
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            var secs = sim.KeeperChoicesByEra();
            Assert.AreEqual(1, secs.Count);
            Assert.AreEqual(Era.Camp, secs[0].era);
            var l = secs[0].lines.Single();
            Assert.AreEqual("The fields, or the woods?", l.ask);
            Assert.AreEqual("The Keeper chose the woods.", l.chosenLine);
            StringAssert.Contains("pole hung with charms", l.propLine);
            Assert.IsTrue(l.credited);
            var pin = sim.KeeperChoicePins().Single();
            Assert.AreEqual(SimCore.KeeperPinKind, pin.kind);
            Assert.AreEqual(d.answerEntryId, pin.entryId);
            Assert.AreEqual(d.endMonth, pin.month);
            Assert.AreEqual("The fields, or the woods? The Keeper chose the woods.", pin.text);
        }

        [Test]
        public void LetThemDecideIsCreditedAndALapseIsThePeoplesWithNoPin()
        {
            var sim = Raised(4);
            Assert.IsTrue(sim.LetThemDecide(sim.OpenDilemma.id));
            var l = sim.KeeperChoicesByEra().Single().lines.Single();
            StringAssert.StartsWith("The Keeper let the people decide: ", l.chosenLine);
            Assert.AreEqual(1, sim.KeeperChoicePins().Count);

            var lapsed = Raised(5);
            Assert.IsTrue(lapsed.DebugLapseDilemma());
            var pl = lapsed.KeeperChoicesByEra().Single().lines.Single();
            StringAssert.StartsWith("The people chose ", pl.chosenLine);
            Assert.IsFalse(pl.credited);
            Assert.IsEmpty(lapsed.KeeperChoicePins(), "only the Keeper's own choices get a hand");
        }

        [Test]
        public void TheBookGetsOnePagePerEraWithChoicesRightAfterTheEraPage()
        {
            var sim = Raised(6);
            Assert.IsFalse(sim.BookPages().Any(p => p.kind == BookPageKind.Choices), "no choices, no page");
            sim.AnswerDilemma(sim.OpenDilemma.id, 0, DilemmaPath.Card);
            var pages = sim.BookPages();
            int at = pages.FindIndex(p => p.kind == BookPageKind.Choices);
            Assert.Greater(at, 0);
            Assert.AreEqual(BookPageKind.Era, pages[at - 1].kind);
            Assert.AreEqual("The Keeper's choices", pages[at].title);
            Assert.AreEqual(1, pages[at].choices.Count);
            Assert.AreEqual("choices:0", pages[at].key);
            Assert.AreEqual(1, pages.Count(p => p.kind == BookPageKind.Choices));
        }

        [Test]
        public void TheCheatFillsSampleChoicesAndOnlyOnce()
        {
            var sim = NewSim(7, Cfg());
            for (int m = 0; m < 24; m++) sim.Advance(1.0 / 12);
            int n = sim.DebugFillKeeperChoices();
            Assert.AreEqual(3, n, "three in the one era reached");
            Assert.AreEqual(0, sim.DebugFillKeeperChoices());
            var lines = sim.KeeperChoicesByEra().Single().lines;
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual(2, lines.Count(l => l.credited));
            Assert.AreEqual(2, sim.KeeperChoicePins().Count);
            Assert.IsTrue(lines.All(l => l.chosenLine.Length > 0 && l.ask.Length > 0));
        }
    }
}
