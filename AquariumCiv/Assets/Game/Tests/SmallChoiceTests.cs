using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#372: small choices (naming a notable couple's newborn) between the big dilemmas. Pure C#, so it also runs in Tools/SimTests.</summary>
    public class SmallChoiceTests
    {
        static SimConfig Cfg() => new SimConfig { dilemmas = true };

        static DilemmaDef Way(string id) => new DilemmaDef
        {
            id = id, kind = DilemmaKind.WhichWay, cast = DilemmaCast.None, question = "The fields, or the woods?",
            options = new[]
            {
                new DilemmaOptionDef { label = "the fields", hint = "h", lean = SimCore.Lean.Food },
                new DilemmaOptionDef { label = "the woods", hint = "h", lean = SimCore.Lean.Craft },
            },
        };

        /// <summary>A box a few years old with a big choice raised and answered, plus a newborn of a wed notable couple.</summary>
        static SimCore AfterABig(int seed = 5, bool newborn = true)
        {
            var sim = NewSim(seed, Cfg());
            sim.WishesOn = false; // an open wish holds the shared slot for up to 24 months: this test is about the choices
            sim.DilemmaCatalog = Enumerable.Range(0, 20).Select(i => Way("w" + i)).ToArray();
            for (int m = 0; m < 36; m++) sim.Advance(1.0 / 12);
            var big = sim.DebugRaiseDilemma("w0");
            Assert.IsNotNull(big);
            Assert.IsTrue(sim.AnswerDilemma(big.id, 0, DilemmaPath.Card));
            if (newborn) Assert.IsNotNull(sim.DebugMakeNotableNewborn());
            return sim;
        }

        /// <summary>Watch month by month (live steps) until a choice opens or <paramref name="years"/> pass.</summary>
        static Dilemma WatchForOne(SimCore sim, double years)
        {
            for (int m = 0; m < (int)(years * 12); m++)
            {
                sim.Advance(1.0 / 12);
                if (sim.OpenDilemma != null) return sim.OpenDilemma;
            }
            return null;
        }

        static Dilemma RaiseSmall(SimCore sim)
        {
            var d = WatchForOne(sim, 3.6);
            Assert.IsNotNull(d, "a small choice is raised once the 3 h gap has passed");
            return d;
        }

        [Test]
        public void SmallAndBigAlternate()
        {
            var sim = AfterABig();
            var small = RaiseSmall(sim);
            Assert.AreEqual(DilemmaSize.Small, small.size);
            Assert.AreEqual("small_naming", small.defId);
            Assert.IsTrue(sim.AnswerDilemma(small.id, 0, DilemmaPath.Card));
            var next = WatchForOne(sim, 14);
            Assert.IsNotNull(next, "the next big one comes when due");
            Assert.AreEqual(DilemmaSize.Big, next.size, "a big follows the small one");
        }

        [Test]
        public void NamingAppliesThePickedNameAndRecordsTheKeeper()
        {
            foreach (int option in new[] { 0, 1 })
            {
                var sim = AfterABig(5);
                var d = RaiseSmall(sim);
                var child = sim.FindNotable(d.subjectId);
                string offered = option == 0 ? d.nameA : d.nameB;
                Assert.IsTrue(sim.AnswerDilemma(d.id, option, DilemmaPath.Card));
                Assert.AreEqual(offered, child.name);
                Assert.IsTrue(child.namedByKeeper);
                Assert.IsTrue(sim.NamedByKeeper(child.id));
                var birth = sim.State.chronicle.Last(e => e.kind == "birth" && e.notableId == child.id);
                StringAssert.Contains(child.FullName, birth.text);
                Assert.IsTrue(sim.State.chronicle.Any(e => e.id == d.answerEntryId && e.keeper));
            }
        }

        [Test]
        public void LetThemDecideTakesOneOfTheTwoNames()
        {
            var sim = AfterABig(6);
            var d = RaiseSmall(sim);
            var child = sim.FindNotable(d.subjectId);
            string a = d.nameA, b = d.nameB;
            Assert.IsTrue(sim.LetThemDecide(d.id));
            CollectionAssert.Contains(new[] { a, b }, child.name);
            Assert.IsTrue(child.namedByKeeper, "left to the parents by the Keeper is still the Keeper's naming");
            StringAssert.Contains(child.name, sim.State.chronicle.Single(e => e.id == d.answerEntryId).text);
        }

        [Test]
        public void TheTwoNamesAreDistinctValidAndFree()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = AfterABig(seed);
                var d = RaiseSmall(sim);
                Assert.AreNotEqual(d.nameA, d.nameB, $"seed {seed}");
                foreach (var n in new[] { d.nameA, d.nameB })
                {
                    CollectionAssert.Contains(NameData.Given, n);
                    Assert.IsFalse(sim.State.notables.Any(o => o.alive && o.name == n), $"{n} is taken (seed {seed})");
                }
                var v = sim.OpenDilemmaView;
                StringAssert.Contains(d.nameA, v.question);
                Assert.AreEqual(d.nameA, v.options[0].label);
                Assert.AreEqual(d.nameB, v.options[1].label);
                Assert.AreEqual(d.personA, v.options[0].personId);
            }
        }

        [Test]
        public void NoSmallChoiceWithoutANotableNewborn()
        {
            var sim = AfterABig(5, newborn: false);
            foreach (var n in sim.State.notables) n.partnerId = -1; // no couples, so no newborn of a couple
            Assert.IsNull(sim.NamingNewborn());
            // A naming raised in the following years only ever names a real baby of a real couple (nothing is invented). t4-fixes: the
            // other choices that fill the slots meanwhile (the everyday small kinds, a big in the small slot) are answered and passed.
            for (int m = 0; m < 48; m++)
            {
                sim.Advance(1.0 / 12);
                var open = sim.OpenDilemma;
                if (open == null) continue;
                if (open.defId != "small_naming") { Assert.IsTrue(sim.AnswerDilemma(open.id, 0, DilemmaPath.Card)); continue; }
                Assert.AreEqual(DilemmaSize.Small, open.size);
                var child = sim.FindNotable(open.subjectId);
                Assert.Less(child.age, SmallChoices.NewbornNameAge);
                Assert.IsNotNull(sim.FindNotable(child.parentId));
                Assert.IsNotNull(sim.FindNotable(open.personA).partnerId >= 0 ? sim.FindNotable(open.personB) : null, "a couple");
                break;
            }
        }

        [Test]
        public void ItWaitsForTheGapAndNeverRaisesWhileCatchingUp()
        {
            var sim = AfterABig();
            for (int m = 0; m < 30; m++) { sim.Advance(1.0 / 12); Assert.IsNull(sim.OpenDilemma, "inside the 3 h gap"); }
            var sim2 = AfterABig();
            sim2.CatchUp(3.2); // away: held, never raised unseen
            Assert.IsNull(sim2.OpenDilemma);
            Assert.IsTrue(sim2.BeginVisit(), "raised on the next open");
            Assert.AreEqual(DilemmaSize.Small, sim2.OpenDilemma.size);
        }

        [Test]
        public void ItClosesAfterThreeVisitsLikeABigOne()
        {
            var sim = AfterABig();
            var d = RaiseSmall(sim);
            for (int v = 2; v <= SimCore.DilemmaMaxVisits; v++) { sim.CatchUp(1); sim.BeginVisit(); Assert.AreEqual(DilemmaState.Open, d.state); }
            sim.CatchUp(1);
            sim.BeginVisit();
            Assert.AreEqual(DilemmaState.Lapsed, d.state);
            var child = sim.FindNotable(d.subjectId);
            CollectionAssert.Contains(new[] { d.nameA, d.nameB }, child.name);
            Assert.IsFalse(child.namedByKeeper, "the parents named the baby, not the Keeper");
        }

        [Test]
        public void SurvivesASave()
        {
            var sim = AfterABig();
            var d = RaiseSmall(sim);
            var back = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            var again = new SimCore(Cfg(), back);
            var d2 = again.OpenDilemma;
            Assert.AreEqual(d.nameA, d2.nameA);
            Assert.AreEqual(d.nameB, d2.nameB);
            Assert.AreEqual(d.subjectId, d2.subjectId);
            Assert.AreEqual(d.nameB, again.OpenDilemmaView.options[1].label);
            again.AnswerDilemma(d2.id, 1, DilemmaPath.Card);
            var back2 = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(again.State));
            Assert.IsTrue(new SimCore(Cfg(), back2).NamedByKeeper(d.subjectId));
        }

        // ---------- t4-fixes (tester run 3): two returns, 7 h, and no choice ----------

        /// <summary>A small kind that is never there (no moment for it): the small slot has nothing to offer.</summary>
        static DilemmaDef NeverSmall() => new DilemmaDef
        {
            id = "small_never", kind = DilemmaKind.WhichWay, size = DilemmaSize.Small, smallKind = "never", question = "?",
            options = new[] { new DilemmaOptionDef { label = "a", hint = "h" }, new DilemmaOptionDef { label = "b", hint = "h" } },
        };

        /// <summary>Opens every 1 to 3 real hours over <paramref name="hours"/> (watched 15 minutes each, the open choice answered while
        /// watched): checks the rules on the way and returns the choices raised after the start.</summary>
        static System.Collections.Generic.List<Dilemma> PlayOpens(SimCore sim, double hours)
        {
            double start = sim.State.clockHours;
            int before = sim.State.dilemmas.Count;
            double[] gaps = { 1.0, 2.0, 3.0, 1.5, 2.5, 1.0, 3.0, 2.0 };
            for (int k = 0; sim.State.clockHours - start < hours; k++)
            {
                for (int m = 0; m < 3; m++)
                {
                    sim.Advance(1.0 / 12);
                    Assert.LessOrEqual(sim.State.dilemmas.Count(d => d.state == DilemmaState.Open), 1, "never two open at once");
                    var open = sim.OpenDilemma;
                    if (open != null && m == 2) Assert.IsTrue(sim.AnswerDilemma(open.id, 0, DilemmaPath.Box));
                }
                double from = sim.State.clockHours;
                sim.CatchUp(gaps[k % gaps.Length]);
                double to = sim.State.clockHours;
                Assert.IsNull(sim.State.dilemmas.Skip(before).FirstOrDefault(d => d.raisedHours > from + 1e-6 && d.raisedHours < to - 1e-6), "never raised during an away");
                sim.BeginVisit();
                Assert.LessOrEqual(sim.State.dilemmas.Count(d => d.state == DilemmaState.Open), 1, "never two open at once");
            }
            var raised = sim.State.dilemmas.Skip(before).ToList();
            for (int i = 1; i < raised.Count; i++)
                Assert.GreaterOrEqual(raised[i].raisedHours - raised[i - 1].raisedHours, SimCore.DilemmaGapHours - 1e-6, "the 3 h gap");
            if (raised.Count > 0 && before > 0) Assert.GreaterOrEqual(raised[0].raisedHours - sim.State.dilemmas[before - 1].raisedHours, SimCore.DilemmaGapHours - 1e-6);
            return raised;
        }

        [Test]
        public void NoSmallKindThereTheNextBigFillsTheSlotAfterTheGap()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var sim = AfterABig(seed, newborn: false);
                sim.SmallCatalog = new[] { NeverSmall() };
                var raised = PlayOpens(sim, 10);
                Assert.GreaterOrEqual(raised.Count, 2, $"seed {seed}: a choice every few hours, not only when the next big's 6-10 h window comes");
                Assert.IsTrue(raised.All(d => d.size == DilemmaSize.Big), $"seed {seed}: nothing is invented: the bigs fill in");
            }
        }

        [Test]
        public void WithoutANewbornTheEverydayKindsComeEveryFewHours()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var sim = AfterABig(seed, newborn: false);
                sim.SmallCatalog = SmallChoices.All.Where(d => d.smallKind != SmallChoices.Naming).ToArray(); // no notable newborn in this box
                var raised = PlayOpens(sim, 10);
                Assert.GreaterOrEqual(raised.Count, 2, $"seed {seed}");
                Assert.IsTrue(raised.Any(d => d.size == DilemmaSize.Small), $"seed {seed}: a small one between the bigs");
                var smalls = raised.Where(d => d.size == DilemmaSize.Small).ToList();
                for (int i = 1; i < smalls.Count; i++) Assert.AreNotEqual(smalls[i - 1].defId, smalls[i].defId, $"seed {seed}: never the same kind twice in a row");
            }
        }

        [Test]
        public void TheEverydayKindsAreThereInACampAndLeaveTheirThing()
        {
            foreach (string kind in new[] { SmallChoices.Story, SmallChoices.Supper })
                for (int option = 0; option < 2; option++)
                {
                    var sim = AfterABig(3, newborn: false);
                    sim.SmallCatalog = SmallChoices.All.Where(d => d.smallKind == kind).ToArray();
                    var d = RaiseSmall(sim);
                    Assert.AreEqual(DilemmaSize.Small, d.size);
                    var v = sim.OpenDilemmaView;
                    Assert.IsFalse(v.question.Contains("{"), v.question);
                    if (kind == SmallChoices.Story)
                    {
                        Assert.AreNotEqual(d.personA, d.personB);
                        Assert.GreaterOrEqual(sim.FindNotable(d.personA).age, SimCore.AdultAge);
                        Assert.AreEqual(sim.FindNotable(d.personA).name, v.options[0].label);
                        Assert.AreEqual(d.personB, v.options[1].personId);
                    }
                    Assert.IsTrue(sim.AnswerDilemma(d.id, option, DilemmaPath.Box));
                    for (int m = 0; m < SimCore.PropWithinMonths; m++) sim.Advance(1.0 / 12);
                    var p = sim.ConsequencePropFor(d.id, option);
                    Assert.IsNotNull(p, $"{kind} {option}");
                    Assert.IsTrue(p.placed, $"{kind} {option}");
                    if (kind == SmallChoices.Story) Assert.AreEqual(d.PersonOf(option), p.personId, "the teller's feather is by their home");
                }
        }

        [Test]
        public void TheEverydayKindsAreAskedAtMostTwiceEach()
        {
            var sim = AfterABig(2, newborn: false);
            sim.SmallCatalog = SmallChoices.All.Where(d => d.smallKind != SmallChoices.Naming).ToArray();
            PlayOpens(sim, 40);
            foreach (var id in new[] { "small_story", "small_supper" })
                Assert.LessOrEqual(sim.State.dilemmas.Count(d => d.defId == id), SmallChoices.RepeatMax, id);
        }

        [Test]
        public void TheKeepersOwnAnswerIsReadAndNeverLeadsTheNextReturn()
        {
            var sim = AfterABig(4, newborn: false);
            var keeperLine = sim.State.chronicle.Single(e => e.id == sim.State.dilemmas[0].answerEntryId);
            Assert.IsTrue(sim.State.cards.read.Contains(keeperLine.id), "the Keeper's answer is read");
            var stack = sim.UnreadCardsCapped(3);
            Assert.IsFalse(stack.cards.Concat(stack.also).Any(c => c.entries.Any(e => e.id == keeperLine.id)), "not a card on the next return");
        }

        [Test]
        public void TheCheatMakesANewbornAndRaisesTheNaming()
        {
            var sim = NewSim(5, Cfg());
            sim.WishesOn = false;
            for (int m = 0; m < 36; m++) sim.Advance(1.0 / 12);
            var d = sim.DebugRaiseNaming();
            Assert.IsNotNull(d);
            Assert.AreEqual(DilemmaSize.Small, d.size);
            Assert.AreEqual(d, sim.OpenDilemma);
        }
    }
}
