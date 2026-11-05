using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Dilemma callbacks (P7, #221): fire inside the 4-15 year window, a failed condition tells the fallback or nothing, the
    /// entry pays off the old question as a thread (+0.2), the Keeper is credited for an answer and the people for a lapse, catch-up
    /// equals live stepping, and an old save loads with none. Pure C#, so it also runs in Tools/SimTests.</summary>
    public class DilemmaCallbackTests
    {
        static SimConfig Cfg() => new SimConfig { dilemmas = true };

        static void Years(SimCore sim, double years) { for (int m = 0; m < (int)(years * 12 + 0.5); m++) sim.Advance(1.0 / 12); }

        static DilemmaDef Def(string id, DilemmaCallbackDef cb0, DilemmaCallbackDef cb1 = null) => new DilemmaDef
        {
            id = id, kind = DilemmaKind.WhichWay, cast = DilemmaCast.None, question = "Where should {place} look: the fields, or the woods?",
            options = new[]
            {
                new DilemmaOptionDef { label = "the fields", hint = "h", callbacks = new[] { cb0 } },
                new DilemmaOptionDef { label = "the woods", hint = "h", callbacks = new[] { cb1 ?? cb0 } },
            },
        };

        static DilemmaCallbackDef Plain(string text = "the granary stands full.") => new DilemmaCallbackDef { id = "plain", text = text };

        static SimCore Raised(int seed, DilemmaDef def, SimConfig cfg = null)
        {
            var sim = NewSim(seed, cfg ?? Cfg());
            sim.DilemmaCatalog = new[] { def };
            Years(sim, 1);
            Assert.IsNotNull(sim.DebugRaiseDilemma(), "the def raises");
            return sim;
        }

        static ChronicleEntry CallbackEntry(SimCore sim) => sim.State.chronicle.FirstOrDefault(e => e.kind == SimCore.CallbackKind);

        [Test]
        public void FiresInsideItsWindowOver20Seeds()
        {
            var dues = new System.Collections.Generic.HashSet<long>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = Raised(seed, Def("w", Plain()));
                var d = sim.OpenDilemma;
                Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card));
                var pend = sim.PendingCallbacks();
                Assert.AreEqual(1, pend.Count, $"seed {seed}");
                Assert.GreaterOrEqual(pend[0].dueMonth - d.endMonth, 4 * 12, $"seed {seed}");
                Assert.LessOrEqual(pend[0].dueMonth - d.endMonth, 15 * 12, $"seed {seed}");
                dues.Add(pend[0].dueMonth - d.endMonth);
                sim.Advance(3.5);
                Assert.IsNull(CallbackEntry(sim), $"seed {seed}: not before 4 years");
                sim.Advance(12.5);
                var e = CallbackEntry(sim);
                Assert.IsNotNull(e, $"seed {seed}: by 16 years");
                Assert.AreEqual(pend[0].dueMonth, e.month, $"seed {seed}: fires on its due month");
                Assert.AreEqual(0, sim.PendingCallbacks().Count);
            }
            Assert.Greater(dues.Count, 5, "the delay varies with the seed");
        }

        [Test]
        public void ConditionFailureTellsTheFallbackOrNothing()
        {
            // person a dead: fallback told
            var cb = new DilemmaCallbackDef { id = "c", aliveA = true, text = "{a} is still here.", fallback = "{place} remembers {a}." };
            var def = new DilemmaDef
            {
                id = "who", kind = DilemmaKind.Who, cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true, question = "{a} or {b}?",
                options = new[] { new DilemmaOptionDef { label = "{a}", hint = "h", callbacks = new[] { cb } }, new DilemmaOptionDef { label = "{b}", hint = "h", callbacks = new[] { cb } } },
            };
            SimCore sim = null;
            for (int seed = 1; seed < 40 && sim == null; seed++)
            {
                var s = NewSim(seed, Cfg()); s.DilemmaCatalog = new[] { def }; Years(s, 3);
                if (s.State.era == Era.Camp && s.DebugRaiseDilemma() != null) sim = s;
            }
            Assert.IsNotNull(sim, "a camp with two candidates");
            var d = sim.OpenDilemma;
            Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Box));
            var a = sim.State.notables.First(n => n.id == d.personA);
            a.alive = false;
            Assert.IsNotNull(sim.DebugFireCallbackNow());
            var e = CallbackEntry(sim);
            StringAssert.Contains("remembers", e.text);
            Assert.IsTrue(sim.State.dilemmaCallbacks[0].usedFallback);

            // no fallback: nothing is told, and the callback is gone
            var bare = new DilemmaCallbackDef { id = "b", needsBuilding = "NoSuchBuilding", text = "never." };
            var sim2 = Raised(3, Def("bare", bare));
            sim2.AnswerDilemma(sim2.OpenDilemma.id, 1, DilemmaPath.Card);
            Assert.IsNull(sim2.DebugFireCallbackNow());
            Assert.IsNull(CallbackEntry(sim2));
            Assert.AreEqual(0, sim2.PendingCallbacks().Count);
            Assert.AreEqual(CallbackState.Dropped, sim2.State.dilemmaCallbacks[0].state);

            // a custom flag: a missing one fails, the option's own custom holds
            var flag = new DilemmaCallbackDef { id = "f", needsCustom = "never:set", text = "x", fallback = "the other way." };
            var sim3 = Raised(4, Def("flag", flag));
            sim3.AnswerDilemma(sim3.OpenDilemma.id, 0, DilemmaPath.Card);
            sim3.DebugFireCallbackNow();
            StringAssert.Contains("the other way", CallbackEntry(sim3).text);
            var okFlag = new DilemmaCallbackDef { id = "g", needsCustom = "ways:fields", text = "the fields hold." };
            var def4 = Def("flag2", okFlag);
            def4.options[0].custom = "ways:fields";
            var sim4 = Raised(4, def4);
            sim4.AnswerDilemma(sim4.OpenDilemma.id, 0, DilemmaPath.Card);
            sim4.DebugFireCallbackNow();
            StringAssert.Contains("the fields hold", CallbackEntry(sim4).text);
        }

        [Test]
        public void TheEntryPaysOffTheOldQuestionAsAThread()
        {
            var sim = Raised(5, Def("t", Plain()));
            var d = sim.OpenDilemma;
            sim.AnswerDilemma(d.id, 0, DilemmaPath.Card);
            var e = sim.DebugFireCallbackNow();
            Assert.IsNotNull(e);
            var stack = sim.ReturnCardsCapped(0, 50);
            var card = stack.cards.Concat(stack.also).FirstOrDefault(c => c.entries.Contains(e));
            Assert.IsNotNull(card, "a story card");
            Assert.Greater(e.threadId, 0);
            Assert.IsTrue(sim.PaysOffATeaser(e), "counts as a teaser payoff (+0.2)");
            Assert.GreaterOrEqual(card.score, e.salience + SimCore.TeaserPayoffBoost - 0.001f);
            StringAssert.Contains("fields, or the woods", card.teaser, "the old question is the grey line");
            Assert.AreEqual(CardKind.Story, card.kind);
        }

        [Test]
        public void TheHeadlineIsTheCreditAndTheLapseLineKeepsItsFullStopAndTheQuestion()
        {
            // #240: the callback's headline is its credit only, so the card never says the opening twice.
            var sim = Raised(5, Def("h", Plain("every roof the young ones mended has a carved bird.")));
            sim.AnswerDilemma(sim.OpenDilemma.id, 1, DilemmaPath.Card);
            var e = sim.DebugFireCallbackNow();
            Assert.AreEqual("Because the Keeper chose the woods", sim.Headline(e));
            StringAssert.StartsWith(sim.Headline(e) + ", every roof", e.text);

            // The lapse line: the whole sentence as headline, and the question grey above it like a callback.
            var lapsed = Raised(6, Def("l", Plain()));
            Assert.IsTrue(lapsed.DebugLapseDilemma());
            var d = lapsed.State.dilemmas[0];
            var line = lapsed.State.chronicle.Single(x => x.id == d.resultEntryId);
            StringAssert.EndsWith(" themselves.", lapsed.Headline(line));
            var stack = lapsed.ReturnCardsCapped(0, 50);
            var card = stack.cards.Concat(stack.also).FirstOrDefault(c => c.lead == line || c.entries.Contains(line));
            Assert.IsNotNull(card, "the lapse line is a card");
            StringAssert.Contains("fields, or the woods", card.teaser, "the question is the grey line");
        }

        [Test]
        public void TheKeeperIsCreditedForAnAnswerAndThePeopleForALapse()
        {
            var answered = Raised(6, Def("c", Plain("the fields feed us.")));
            answered.AnswerDilemma(answered.OpenDilemma.id, 0, DilemmaPath.Card);
            answered.DebugFireCallbackNow();
            Assert.AreEqual("Because the Keeper chose the fields, the fields feed us.", CallbackEntry(answered).text);
            var info = answered.CallbackInfoOf(CallbackEntry(answered));
            Assert.AreEqual(CallbackCredit.Keeper, info.credit);
            Assert.AreEqual(0, info.option);

            var lapsed = Raised(6, Def("c", new DilemmaCallbackDef { id = "x", text = "{credit} -- and so it was." }));
            Assert.IsTrue(lapsed.DebugLapseDilemma());
            int opt = lapsed.State.dilemmas[0].option;
            lapsed.DebugFireCallbackNow();
            var e = CallbackEntry(lapsed);
            StringAssert.StartsWith("Because the people chose " + (opt == 0 ? "the fields" : "the woods") + " -- and so it was", e.text);
            StringAssert.DoesNotContain("Keeper", e.text);
            Assert.AreEqual(CallbackCredit.People, lapsed.CallbackInfoOf(e).credit);
            Assert.IsNull(lapsed.CallbackInfoOf(lapsed.State.chronicle[0]));
        }

        [Test]
        public void ACallbackNeverComesBeforeTheLapseLineThatIsItsResult()
        {
            // #312: the callback outscores the lapse line, so a stack by score alone put it first.
            foreach (int cap in new[] { 2, 3, 50 })
            {
                var sim = Raised(6, Def("o", Plain("the fields feed us."), Plain("the woods are thick.")));
                Assert.IsTrue(sim.DebugLapseDilemma());
                var d = sim.State.dilemmas[0];
                sim.Advance(16);
                var cb = CallbackEntry(sim);
                Assert.IsNotNull(cb, "the callback fired");
                StringAssert.StartsWith("Because the people chose ", cb.text);
                var stack = sim.ReturnCardsCapped(0, cap);
                int line = stack.cards.FindIndex(c => c.entries.Any(e => e.id == d.resultEntryId));
                int back = stack.cards.FindIndex(c => c.entries.Any(e => e.id == cb.id));
                if (back >= 0) Assert.IsTrue(line >= 0 && line < back, $"cap {cap}: the lapse line (card {line}) comes before the callback (card {back})");
            }
        }

        [Test]
        public void YearsSinceTheAnswerAreExposed()
        {
            var sim = Raised(7, Def("y", Plain()));
            sim.AnswerDilemma(sim.OpenDilemma.id, 0, DilemmaPath.Card);
            sim.Advance(16);
            var e = CallbackEntry(sim);
            var info = sim.CallbackInfoOf(e);
            Assert.GreaterOrEqual(info.yearsSinceAnswer, 4f);
            Assert.LessOrEqual(info.yearsSinceAnswer, 15f);
            Assert.AreSame(sim.State.dilemmas[0], info.dilemma);
        }

        [Test]
        public void CatchUpEqualsLiveStepping()
        {
            var a = Raised(8, Def("u", Plain(), Plain("the woods are thick.")));
            var b = Raised(8, Def("u", Plain(), Plain("the woods are thick.")));
            a.AnswerDilemma(a.OpenDilemma.id, 1, DilemmaPath.Box);
            b.AnswerDilemma(b.OpenDilemma.id, 1, DilemmaPath.Box);
            Years(a, 16); b.Advance(16);
            Assert.AreEqual(a.State.month, b.State.month);
            Assert.IsNotNull(CallbackEntry(a));
            Assert.AreEqual(Fingerprint(a.State), Fingerprint(b.State));
            Assert.AreEqual(JsonUtility.ToJson(a.State.dilemmaCallbacks[0]), JsonUtility.ToJson(b.State.dilemmaCallbacks[0]));
            Assert.AreEqual(CallbackEntry(a).text, CallbackEntry(b).text);
        }

        [Test]
        public void EveryOptionHasACallbackAndSwitchOffSchedulesNone()
        {
            foreach (var def in DilemmaDefs.All)
                foreach (var o in def.options)
                    Assert.IsTrue(o.callbacks != null && o.callbacks.Length >= 1, def.id);
            var plain = NewSim(9, new SimConfig());
            Years(plain, 10);
            Assert.AreEqual(0, plain.State.dilemmaCallbacks.Count);
            Assert.IsNull(plain.DebugFireCallbackNow());
        }

        [Test]
        public void TheScheduleSurvivesASaveAndAnOldSaveLoadsWithNone()
        {
            var sim = Raised(10, Def("s", Plain()));
            sim.AnswerDilemma(sim.OpenDilemma.id, 0, DilemmaPath.Card);
            var json = JsonUtility.ToJson(sim.State);
            var back = JsonUtility.FromJson<SimState>(json);
            Assert.AreEqual(json, JsonUtility.ToJson(back));
            var again = new SimCore(Cfg(), back) { DilemmaCatalog = new[] { Def("s", Plain()) } };
            Assert.AreEqual(1, again.PendingCallbacks().Count);
            again.Advance(16);
            Assert.IsNotNull(CallbackEntry(again));
            var old = JsonUtility.FromJson<SimState>("{\"seed\":7,\"month\":5}");
            var oldSim = new SimCore(Cfg(), old);
            Assert.AreEqual(0, oldSim.PendingCallbacks().Count);
        }
    }
}
