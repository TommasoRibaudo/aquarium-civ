using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using Terrain = AquariumCiv.Sim.Settle.Terrain;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #371 (visit loop C4; visit-loop.md 2.5): every option leaves a thing in the box within a game year, the thing is saved, a
    /// callback is a live moment (a card only if the moment becomes one), and the person card says "Chosen by the Keeper". Pure C#,
    /// so it also runs in Tools/SimTests.
    /// </summary>
    public class ConsequencePropTests
    {
        static SimConfig Cfg() => new SimConfig { dilemmas = true, moments = true };

        static DilemmaDef Def(string id, string prop, PropAnchor at, string building = "") => new DilemmaDef
        {
            id = id, kind = DilemmaKind.WhichWay, cast = DilemmaCast.None, question = "The fields, or the woods?",
            options = new[]
            {
                new DilemmaOptionDef { label = "the fields", hint = "h", prop = prop, propAt = at, propBuilding = building, callbacks = new[] { new DilemmaCallbackDef { id = "c", text = "the granary stands full." } } },
                new DilemmaOptionDef { label = "the woods", hint = "h", prop = prop, propAt = at, propBuilding = building },
            },
        };

        static SimCore Raised(int seed, DilemmaDef def)
        {
            var sim = NewSim(seed, Cfg());
            sim.DilemmaCatalog = new[] { def };
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Assert.IsNotNull(sim.DebugRaiseDilemma(def.id), "the def raises");
            return sim;
        }

        [Test]
        public void EveryOptionOfEveryChoiceHasAProp()
        {
            var all = DilemmaDefs.All.Concat(SmallChoices.All).ToList();
            Assert.AreEqual(11, all.Count); // t4-fixes: + the story and supper small choices
            foreach (var def in all)
                foreach (var o in def.options)
                    CollectionAssert.Contains(ConsequenceKind.All, o.prop, $"{def.id}: option '{o.label}' leaves a thing");
            var kinds = DilemmaDefs.All.SelectMany(d => d.options).Select(o => o.prop).Distinct().ToList();
            Assert.GreaterOrEqual(kinds.Count, 14, "the big choices' options each leave their own thing");
            Assert.AreEqual(16, DilemmaDefs.All.Sum(d => d.options.Length));
        }

        [Test]
        public void AnAnsweredChoiceLeavesItsThingWithinAYearAtAnOpenSpot()
        {
            foreach (var at in new[] { PropAnchor.Home, PropAnchor.Building, PropAnchor.Fire, PropAnchor.Edge, PropAnchor.Pond })
                for (int seed = 1; seed <= 5; seed++)
                {
                    var sim = Raised(seed, Def("p", ConsequenceKind.Feather, at, BuildingType.Hut));
                    var d = sim.OpenDilemma;
                    Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card));
                    for (int m = 0; m < SimCore.PropWithinMonths; m++) sim.Advance(1.0 / 12);
                    var p = sim.ConsequencePropFor(d.id, 0);
                    Assert.IsNotNull(p, $"{at} seed {seed}");
                    Assert.IsTrue(p.placed, $"{at} seed {seed}: within a game year");
                    Assert.LessOrEqual(p.placedMonth - d.endMonth, SimCore.PropWithinMonths, $"{at} seed {seed}");
                    Assert.LessOrEqual(System.Math.Abs(p.x), Terrain.HX - Terrain.GlassMargin + 0.001f);
                    Assert.LessOrEqual(System.Math.Abs(p.z), Terrain.HZ - Terrain.GlassMargin + 0.001f);
                    Assert.IsNull(sim.ConsequencePropFor(d.id, 1), "only the option taken leaves its thing");
                    Assert.IsTrue(p.keeper);
                }
        }

        [Test]
        public void ALapsedChoiceLeavesItsThingToo()
        {
            var sim = Raised(3, Def("p", ConsequenceKind.Baskets, PropAnchor.Fire));
            var d = sim.OpenDilemma;
            Assert.IsTrue(sim.DebugLapseDilemma());
            var p = sim.ConsequencePropFor(d.id, d.option);
            Assert.IsNotNull(p);
            Assert.IsFalse(p.keeper, "the people's choice, not the Keeper's");
        }

        [Test]
        public void LetThemDecideLeavesTheDefaultOptionsThing()
        {
            var sim = Raised(4, Def("p", ConsequenceKind.Staff, PropAnchor.Fire));
            var d = sim.OpenDilemma;
            Assert.IsTrue(sim.LetThemDecide(d.id));
            var p = sim.ConsequencePropFor(d.id, d.option);
            Assert.IsNotNull(p);
            Assert.IsTrue(p.keeper, "handed back by the Keeper: credited");
        }

        [Test]
        public void EveryRealOptionsPropCanBePlacedInOneBox()
        {
            var sim = NewSim(9, Cfg());
            for (int m = 0; m < 24; m++) sim.Advance(1.0 / 12);
            int n = sim.DebugPlaceEveryProp();
            Assert.AreEqual(22, n, "16 big options and 2 options each of naming, story and supper (t4-fixes)");
            Assert.AreEqual(n, sim.ConsequenceProps.Count(p => p.placed));
            foreach (var p in sim.ConsequenceProps)
            {
                Assert.LessOrEqual(System.Math.Abs(p.x), Terrain.HX - Terrain.GlassMargin + 0.001f, p.kind);
                Assert.LessOrEqual(System.Math.Abs(p.z), Terrain.HZ - Terrain.GlassMargin + 0.001f, p.kind);
            }
        }

        [Test]
        public void ThePropsPersistInTheSave()
        {
            var sim = Raised(5, Def("p", ConsequenceKind.WoodpileRoof, PropAnchor.Building, BuildingType.Hut));
            var d = sim.OpenDilemma;
            sim.AnswerDilemma(d.id, 1, DilemmaPath.Card);
            for (int m = 0; m < 6; m++) sim.Advance(1.0 / 12);
            var json = JsonUtility.ToJson(sim.State);
            var back = JsonUtility.FromJson<SimState>(json);
            Assert.AreEqual(json, JsonUtility.ToJson(back));
            Assert.AreEqual(1, back.consequenceProps.Count);
            Assert.AreEqual(ConsequenceKind.WoodpileRoof, back.consequenceProps[0].kind);
            Assert.AreEqual(sim.State.consequenceProps[0].x, back.consequenceProps[0].x);
            var old = JsonUtility.FromJson<SimState>("{\"seed\":7,\"month\":5}");
            Assert.IsTrue(old.consequenceProps == null || old.consequenceProps.Count == 0, "an old save has none");
        }

        [Test]
        public void ThePlacementIsDeterministic()
        {
            var a = Raised(6, Def("p", ConsequenceKind.Feather, PropAnchor.Fire));
            var b = Raised(6, Def("p", ConsequenceKind.Feather, PropAnchor.Fire));
            a.AnswerDilemma(a.OpenDilemma.id, 0, DilemmaPath.Card);
            b.AnswerDilemma(b.OpenDilemma.id, 0, DilemmaPath.Card);
            Assert.AreEqual(JsonUtility.ToJson(a.State.consequenceProps[0]), JsonUtility.ToJson(b.State.consequenceProps[0]));
        }

        // ---------- the callback as a live moment ----------

        [Test]
        public void ACallbackIsAMomentNotACardUntilItBecomesOne()
        {
            Assert.IsFalse(MomentRules.AlsoACard(MomentKind.Callback));
            var sim = Raised(7, Def("p", ConsequenceKind.Feather, PropAnchor.Fire));
            sim.AnswerDilemma(sim.OpenDilemma.id, 0, DilemmaPath.Card);
            var e = sim.DebugFireCallbackNow();
            Assert.IsNotNull(e);
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m, "the callback queued a moment");
            Assert.AreEqual(MomentKind.Callback, m.kind);
            StringAssert.StartsWith("Because the Keeper chose", e.text, "the moment's caption is the credit");
            Assert.IsTrue(sim.MomentHidesCard(e), "waiting as a moment: not a card");
            Assert.IsTrue(sim.MomentStillHeld(e));
            for (int i = 0; i < 8 * 12 + 2; i++)
            {
                sim.State.population = 30;
                sim.Advance(1.0 / 12);
                sim.State.moments.RemoveAll(x => x != m);
            }
            Assert.AreEqual(HeldMomentState.Carded, m.state);
            Assert.IsFalse(sim.MomentHidesCard(e), "now it is a card, with the credit on it");
        }

        // ---------- the person card ----------

        [Test]
        public void ThePersonCardSaysChosenByTheKeeperOnlyForTheKeepersPick()
        {
            var cb = new DilemmaCallbackDef { id = "c", text = "x." };
            var def = new DilemmaDef
            {
                id = "who", kind = DilemmaKind.Who, cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true, question = "{a} or {b}?",
                options = new[]
                {
                    new DilemmaOptionDef { label = "{a}", hint = "h", takesRole = true, prop = ConsequenceKind.Feather, propAt = PropAnchor.Home, callbacks = new[] { cb } },
                    new DilemmaOptionDef { label = "{b}", hint = "h", takesRole = true, prop = ConsequenceKind.Feather, propAt = PropAnchor.Home, callbacks = new[] { cb } },
                },
            };
            SimCore Make(int seed)
            {
                var s = NewSim(seed, Cfg()); s.DilemmaCatalog = new[] { def };
                for (int m = 0; m < 36; m++) s.Advance(1.0 / 12);
                return s.State.era == Era.Camp && s.DebugRaiseDilemma() != null ? s : null;
            }
            SimCore answered = null, lapsed = null, letGo = null;
            for (int seed = 1; seed < 80 && letGo == null; seed++)
            {
                var s = Make(seed);
                if (s == null) continue;
                if (answered == null) answered = s;
                else if (lapsed == null) lapsed = s;
                else letGo = s;
            }
            Assert.IsNotNull(letGo, "three camps with two candidates");

            var d = answered.OpenDilemma;
            answered.AnswerDilemma(d.id, 0, DilemmaPath.Box);
            Assert.AreEqual("Chosen by the Keeper.", answered.PersonCardOf(d.personA).keeperLine);
            Assert.AreEqual("", answered.PersonCardOf(d.personB).keeperLine, "the unchosen one is not marked");

            var dl = lapsed.OpenDilemma;
            lapsed.DebugLapseDilemma();
            Assert.AreEqual("", lapsed.PersonCardOf(dl.PersonOf(dl.option)).keeperLine, "the people chose: no mark");

            var dg = letGo.OpenDilemma;
            letGo.LetThemDecide(dg.id);
            Assert.AreEqual("Chosen by the Keeper.", letGo.PersonCardOf(dg.PersonOf(dg.option)).keeperLine, "handed back by the Keeper: still the Keeper's");
        }

        [Test]
        public void TheCardAlsoSaysNamedByTheKeeper()
        {
            var sim = NewSim(3, Cfg());
            var n = sim.State.notables.First(x => x.alive);
            Assert.AreEqual("", sim.KeeperHandLine(n));
            n.namedByKeeper = true;
            Assert.AreEqual("Named by the Keeper.", sim.KeeperHandLine(n));
            n.chosenByKeeper = true;
            Assert.AreEqual("Chosen and named by the Keeper.", sim.KeeperHandLine(n));
            Assert.AreEqual("Chosen and named by the Keeper.", sim.PersonCardOf(n.id).keeperLine);
        }
    }
}
