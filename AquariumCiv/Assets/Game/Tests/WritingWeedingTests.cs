using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// Research 21's weeding (#714 to #721; docs/design/writing-events.md). #714: no choice changes the economy (the leans are gone);
    /// the asks name who they are about (the travellers, the young people, the one who saw the wolves); the travellers' camp of their
    /// own is for good; one white-stag rule; the headman's well callbacks need a well; the pond family brings a boat, not a goat (old
    /// saves migrate). Pure C#, so it also runs in Tools/SimTests.
    /// </summary>
    public class WritingWeedingTests
    {
        static SimCore Raised(DilemmaDef def)
        {
            for (int s = 1; s <= 12; s++)
            {
                var sim = DilemmaContentTests.RaisedWithin40Years(def, s);
                if (sim != null) return sim;
            }
            return null;
        }

        static int SeedOf(DilemmaDef def)
        {
            for (int s = 1; s <= 12; s++) if (DilemmaContentTests.RaisedWithin40Years(def, s) != null) return s;
            return -1;
        }

        // ---------- #714: no leans ----------

        [Test]
        public void NoOptionHasALean_EachDilemmaBothWaysMakesTheSameFoodAndWood()
        {
            foreach (var def in DilemmaDefs.All)
            {
                // strangers who join add hands and mouths: that is who they are, not a lean
                if (def.options.Any(o => o.strangers > 0 && string.IsNullOrEmpty(o.apart))) continue;
                int seed = SeedOf(def);
                Assert.Greater(seed, 0, def.id);
                var a = DilemmaContentTests.RaisedWithin40Years(def, seed);
                var b = DilemmaContentTests.RaisedWithin40Years(def, seed);
                Assert.IsTrue(a.AnswerDilemma(a.State.dilemmas[0].id, 0, DilemmaPath.Card), def.id);
                Assert.IsTrue(b.AnswerDilemma(b.State.dilemmas[0].id, 1, DilemmaPath.Card), def.id);
                for (int m = 0; m < 24; m++)
                {
                    a.Step(); b.Step();
                    Assert.AreEqual(a.State.food, b.State.food, 1e-3f, $"{def.id} month {m}: the same food either way");
                    Assert.AreEqual(a.State.wood, b.State.wood, 1e-3f, $"{def.id} month {m}: the same wood either way");
                }
            }
        }

        [Test]
        public void AnOldSaveWithALeanAGoatPenAndTheOldCallbackIdsStillLoads()
        {
            var sim = NewSim(15, new SimConfig { dilemmas = true });
            var json = JsonUtility.ToJson(sim.State);
            // a save from before #714: a lean list and the pond family's goat pen
            json = json.Substring(0, json.Length - 1)
                + ",\"dilemmaLeans\":[{\"lean\":0,\"fromMonth\":3,\"untilMonth\":39,\"dilemmaId\":1}]}";
            var old = JsonUtility.FromJson<SimState>(json);
            old.consequenceProps.Add(new ConsequenceProp { id = 1, dilemmaId = 1, option = 0, kind = ConsequenceKind.LegacyGoatFence, placed = true });
            var back = new SimCore(new SimConfig { dilemmas = true }, old);
            Assert.AreEqual(ConsequenceKind.ShoreBoat, back.ConsequenceProps.Single().kind, "the goat pen is their boat now");
            Assert.AreEqual("their boat, upside down by the fire", KeeperChoices.PropName(ConsequenceKind.ShoreBoat));
            for (int m = 0; m < 12; m++) back.Step();
            CollectionAssert.DoesNotContain(ConsequenceKind.All, ConsequenceKind.LegacyGoatFence);
        }

        [Test]
        public void TheRenamedCallbackIdsAndNoGoat()
        {
            var ids = DilemmaDefs.All.SelectMany(d => d.options).SelectMany(o => o.callbacks).Select(c => c.id).ToList();
            CollectionAssert.Contains(ids, "hill_neighbours_fish"); CollectionAssert.Contains(ids, "spring_timber_sunset");
            CollectionAssert.Contains(ids, "hill_joined_boat");
            CollectionAssert.DoesNotContain(ids, "hill_neighbours_eggs"); CollectionAssert.DoesNotContain(ids, "spring_timber_squirrels");
            foreach (var d in DilemmaDefs.All)
                foreach (var o in d.options)
                    foreach (var t in new[] { o.label, o.hint, o.done }.Concat(o.callbacks.SelectMany(c => new[] { c.text, c.fallback })))
                        StringAssert.DoesNotContain("goat", t ?? "", d.id);
        }

        // ---------- #714: name who is asked about ----------

        [Test]
        public void VillageSpringNamesTwoYoungPeopleAndTheAnswerIsAboutThem()
        {
            var def = DilemmaDefs.All.Single(d => d.id == "village_spring");
            for (int opt = 0; opt < 2; opt++)
            {
                var sim = Raised(def);
                Assert.IsNotNull(sim);
                var d = sim.State.dilemmas[0];
                var y1 = sim.State.notables.Single(n => n.id == d.youngA);
                var y2 = sim.State.notables.Single(n => n.id == d.youngB);
                Assert.AreNotEqual(y1.id, y2.id);
                Assert.That(y1.age, Is.GreaterThanOrEqualTo(SimCore.AdultAge)); Assert.That(y2.age, Is.GreaterThanOrEqualTo(SimCore.AdultAge));
                var younger = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge && n.age < 20f).ToList();
                if (younger.Count >= 2) Assert.That(y1.age < 20f && y2.age < 20f, "just came of age, when there are two such");
                string q = sim.QuestionOf(d);
                StringAssert.Contains(y1.name, q); StringAssert.Contains(y2.name, q);
                StringAssert.DoesNotContain("young ones", q);
                Assert.IsTrue(sim.AnswerDilemma(d.id, opt, DilemmaPath.Card));
                var done = sim.State.chronicle.Single(e => e.id == d.answerEntryId).text;
                StringAssert.Contains(y1.name, done); StringAssert.Contains(y2.name, done);
                Assert.IsTrue(y1.chosenByKeeper && y2.chosenByKeeper, "both are sent by the Keeper");
                var cb = sim.DebugFireCallbackNow();
                Assert.IsNotNull(cb);
                StringAssert.DoesNotContain("{", cb.text);
            }
        }

        // ---------- #714: the headman's meetings by the well ----------

        [Test]
        public void TheMeetingsByTheWellAreNeverToldWithoutAWell()
        {
            var def = DilemmaDefs.All.Single(d => d.id == "village_headman");
            foreach (var o in def.options)
                foreach (var c in o.callbacks.Where(c => c.text.Contains("well")))
                {
                    Assert.AreEqual(BuildingType.Well, c.needsBuilding, c.id);
                    StringAssert.DoesNotContain("well", c.fallback, c.id);
                }
            var sim = Raised(def);
            Assert.IsNotNull(sim);
            var d = sim.State.dilemmas[0];
            Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card));
            sim.State.buildings.RemoveAll(b => b.type == BuildingType.Well);
            ChronicleEntry e;
            while ((e = sim.DebugFireCallbackNow()) != null) StringAssert.DoesNotContain("by the well", e.text);
        }

        // ---------- #714: the woodpile it asks about exists ----------

        [Test]
        public void CampWinterWaitsForTheFirstWildWoodOnALivingLand()
        {
            var cfg = new SimConfig { ecology = true, dilemmas = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            Assume.That(sim.NewStartOn, "a living land");
            sim.DebugBuild(BuildingType.Lumbermill);
            Assert.AreEqual("no wild wood yet", sim.DilemmaWhyNot("camp_winter"));
            sim.State.newStart.firstWildWoodMonth = sim.State.month;
            Assert.AreNotEqual("no wild wood yet", sim.DilemmaWhyNot("camp_winter"));
        }

        // ---------- #714: one white-stag rule ----------

        [Test]
        public void TheWhiteStagLetThemDecideIsTheChiefsTrait()
        {
            foreach (var (trait, hunt) in new[] { ("the kind", false), ("the dreamer", false), ("the brave", true), ("the grump", true) })
            {
                var sim = PlacedLifeTests.Camp(9, 30);
                foreach (var n in sim.State.notables) n.trait = trait;
                Assert.IsTrue(sim.DebugWhiteStagNow());
                Assert.AreEqual(hunt, sim.WhiteStagLetThemDecide(), trait);
            }
        }
    }
}
