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
            // A choice raised in the following years only ever names a real baby of a real couple (nothing is invented).
            for (int m = 0; m < 48; m++)
            {
                sim.Advance(1.0 / 12);
                var open = sim.OpenDilemma;
                if (open == null) continue;
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
