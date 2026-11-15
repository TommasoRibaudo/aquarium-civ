using System.Collections.Generic;
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
        public void NobodyInAnAskComesFromOutsideTheBox()
        {
            // the closed world (feedback rule; writing-events.md §3): people live in the tank, nothing comes from beyond it
            foreach (var d in DilemmaDefs.All)
                foreach (var t in new[] { d.question }.Concat(d.askedBy?.Values ?? Enumerable.Empty<string>()).Concat(d.options.SelectMany(o => new[] { o.hint, o.label })))
                    foreach (var w in new[] { "past the sky wall", "behind the keeper's window", "beyond", "outside", "far-away", "the world" })
                        StringAssert.DoesNotContain(w, (t ?? "").ToLowerInvariant(), d.id);
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

        // ---------- #716: office and role lines name only things in the box ----------

        static readonly string[] Missing = { "goat", "sheep", "chimney", "hat day", "sack race", "egg-and-spoon", "turns to glass", "see over the glass", "the hill", "very large dog" };

        /// <summary>A source file of the sim, found from the Unity project root (the Editor's working folder) or the repo (dotnet test).</summary>
        static string SimSource(string file)
        {
            foreach (var root in new[] { System.IO.Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var d = new System.IO.DirectoryInfo(root); d != null; d = d.Parent)
                    foreach (var rel in new[] { "Assets/Game/Sim/" + file, "AquariumCiv/Assets/Game/Sim/" + file })
                    {
                        var p = System.IO.Path.Combine(d.FullName, rel);
                        if (System.IO.File.Exists(p)) return System.IO.File.ReadAllText(p);
                    }
            Assert.Fail("source not found: " + file);
            return null;
        }

        [Test]
        public void NoOfficeOrRoleLineNamesAThingTheBoxNeverHas()
        {
            foreach (var t in SimCore.AllTermText())
                foreach (var w in Missing) StringAssert.DoesNotContain(w, t.ToLowerInvariant(), t);
            foreach (var file in new[] { "SimCore.Office.cs", "SimCore.Roles.cs" })
            {
                // the tables and inline lines, comments left out
                var code = string.Join("\n", SimSource(file).Split('\n').Select(l => { int c = l.IndexOf("//"); return c >= 0 ? l.Substring(0, c) : l; }));
                foreach (var w in Missing) StringAssert.DoesNotContain(w, code.ToLowerInvariant(), file);
            }
        }

        static (SimCore sim, Notable chief) CampChief(int seed, string trait, bool ecology = false)
        {
            var cfg = new SimConfig { ecology = ecology };
            var sim = ecology ? new SimCore(cfg, SimCore.NewWorld(seed, cfg)) : NewSim(seed, cfg);
            var chief = sim.HolderOf(RoleType.Chief);
            Assume.That(chief, Is.Not.Null);
            chief.trait = trait; chief.age = 20f; chief.lifespan = 200f;
            return (sim, chief);
        }

        /// <summary>Every term line this chief tells over <paramref name="years"/> years, asked for every season.</summary>
        static List<string> TermLines(SimCore sim, int years)
        {
            var told = new List<string>();
            for (int m = 0; m < years * SimConfig.MonthsPerYear; m++)
            {
                sim.Step();
                if (m % 3 == 0 && sim.DebugTermStory(RoleType.Chief) is ChronicleEntry e) told.Add(e.text);
            }
            told.AddRange(sim.State.chronicle.Where(e => e.kind.StartsWith("term_")).Select(e => e.text));
            return told;
        }

        [Test]
        public void ACampTinkererHasNoDeviceLine_TheWeathervaneIsTownOnly()
        {
            var (sim, _) = CampChief(7, "the tinkerer");
            Assert.IsNull(sim.DebugTermStory(RoleType.Chief), "no device line is true of a camp");
            Assert.IsEmpty(TermLines(sim, 3).Where(t => t.Contains("weathervane") || t.Contains("cart")));
        }

        [Test]
        public void ACampGrumpPutsTheFireLowNotTheLamps()
        {
            var (sim, _) = CampChief(7, "the grump");
            var lines = TermLines(sim, 12);
            Assert.IsEmpty(lines.Where(t => t.Contains("lamps out")), "no lamps in a camp");
            Assume.That(lines.Any(t => t.Contains("made a rule")));
            Assert.IsTrue(lines.Where(t => t.Contains("made a rule")).All(t => t.Contains("the fire goes low early")));
        }

        [Test]
        public void TheDreamersWalkNeedsTheKeepersTree_TheLoudToastNeedsDucks()
        {
            var (dreamer, _) = CampChief(7, "the dreamer"); // ecology off: no Keeper's tree, no ducks
            Assert.IsEmpty(TermLines(dreamer, 12).Where(t => t.Contains("Keeper's tree") || t.Contains("over the glass")));
            var (loud, _) = CampChief(7, "the loud");
            var lines = TermLines(loud, 12);
            Assert.IsEmpty(lines.Where(t => t.Contains("ducks")), "no ducks on the pond, no toast that scares them");
            Assert.IsEmpty(lines.Where(t => t.Contains("far side of the glass")));
        }

        [Test]
        public void ACampWorrierRingsTheFireWithStones()
        {
            var (sim, _) = CampChief(7, "the worrier");
            var lines = TermLines(sim, 12);
            Assert.IsEmpty(lines.Where(t => t.Contains("lanterns hung")), "the lantern line is the village's");
        }

        // ---------- #721: the rituals replay every year ----------

        static SimCore WithChoice(string defId, int option)
        {
            var def = DilemmaDefs.All.Single(d => d.id == defId);
            var sim = Raised(def);
            Assert.IsNotNull(sim, defId);
            Assert.IsTrue(sim.AnswerDilemma(sim.State.dilemmas[0].id, option, DilemmaPath.Card));
            return sim;
        }

        [TestCase("camp_winter", 0, RitualKind.Berrying)]
        [TestCase("camp_winter", 1, RitualKind.Woodpile)]
        [TestCase("village_harvest", 0, RitualKind.Feast)]
        [TestCase("village_harvest", 1, RitualKind.Offering)]
        public void EachAnswerBringsItsRitualBackEveryYearInItsSeason_WithANamedLeader(string defId, int option, string ritual)
        {
            var sim = WithChoice(defId, option);
            CollectionAssert.AreEqual(new[] { ritual }, sim.Rituals.ToArray(), "only the chosen ritual");
            var played = new List<(string kind, Notable lead, ChronicleEntry e, int cal)>();
            sim.RitualPlayed += (k, lead, e) => played.Add((k, lead, e, sim.CalendarMonth));
            for (int m = 0; m < 6 * SimConfig.MonthsPerYear; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            Assert.That(played.Count, Is.InRange(5, 6), "once a year");
            Assert.IsTrue(played.All(p => p.kind == ritual && p.cal == RitualKind.MonthOf(ritual)), "in its season");
            Assert.IsTrue(played.All(p => p.lead != null && p.e.text.Length > 0 && !p.e.text.Contains("{")), "a named leader");
            Assert.AreEqual(SimCore.RitualFirstKind(ritual), played[0].e.kind, "a card the first time");
            Assert.IsTrue(played.Skip(1).All(p => p.e.kind == SimCore.RitualYearKind(ritual)), "History after");
            Assert.AreEqual("custom", SimCore.GroupOf(played[0].e.kind));
            Assert.AreEqual("hidden", SimCore.GroupOf(played[1].e.kind), "never a card again");
            foreach (var p in played) foreach (var w in new[] { "hill", "goat", "glass", "tank" }) StringAssert.DoesNotContain(w, p.e.text.ToLowerInvariant());
        }

        [Test]
        public void ARitualSurvivesASaveAndAnEraChange_AndALapseStartsIt()
        {
            var sim = WithChoice("camp_winter", 0);
            for (int m = 0; m < 2 * SimConfig.MonthsPerYear; m++) sim.Step();
            int before = sim.RitualTimes(RitualKind.Berrying);
            Assert.GreaterOrEqual(before, 1);
            var back = new SimCore(new SimConfig { dilemmas = true }, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.IsTrue(back.ForceNextEra());
            for (int m = 0; m < 2 * SimConfig.MonthsPerYear; m++) back.Step();
            Assert.Greater(back.RitualTimes(RitualKind.Berrying), before, "still every autumn, in the village");
            Assert.AreEqual(1, back.State.chronicle.Count(e => e.kind == SimCore.RitualFirstKind(RitualKind.Berrying)), "the card only once");

            var def = DilemmaDefs.All.Single(d => d.id == "village_harvest");
            var lapsed = Raised(def);
            Assert.IsTrue(lapsed.DebugLapseDilemma());
            Assert.AreEqual(1, lapsed.Rituals.Count(), "the people's pick starts a ritual too");
        }

        [Test]
        public void TheTownGrowsTheRituals_AndTheOfferingBasketIsEmptyInSpring()
        {
            var sim = WithChoice("village_harvest", 1);
            for (int m = 0; m < 2 * SimConfig.MonthsPerYear; m++) sim.Step();
            Assert.IsTrue(sim.State.chronicle.Any(e => e.text.Contains("Keeper's tree") && e.kind.StartsWith("ritual_offering")));
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "ritual_offering_empty"), "the basket is empty in spring");
            Assert.AreEqual("hidden", SimCore.GroupOf("ritual_offering_empty"));
        }

        [Test]
        public void TheDebugCheatPlaysEachRitualNow()
        {
            var sim = NewSim(3, new SimConfig { dilemmas = true });
            foreach (var k in RitualKind.All)
            {
                var e = sim.DebugPlayRitual(k);
                Assert.IsNotNull(e, k);
                Assert.AreEqual(SimCore.RitualFirstKind(k), e.kind);
                Assert.IsFalse(string.IsNullOrEmpty(RitualKind.Caption(k)));
            }
            Assert.IsNull(sim.DebugPlayRitual("nope"));
        }

        [Test]
        public void TheFeastCallbackCountsTheSeats()
        {
            var sim = WithChoice("village_harvest", 0);
            ChronicleEntry e;
            var told = new List<string>();
            while ((e = sim.DebugFireCallbackNow()) != null) told.Add(e.text);
            Assert.IsTrue(told.All(t => !t.Contains("{")), string.Join(" / ", told));
            Assert.IsTrue(told.Any(t => t.Contains("seats now")), string.Join(" / ", told));
        }

        // ---------- #715: the story stream ----------

        /// <summary>The story stream's kinds (SimCore.Story.cs), the kept ones and the dropped ones.</summary>
        public static readonly string[] StoryKinds =
            new[] { "wedding", "friendship", "rivalry", "reconcile", "apprentice", "elder", "naming", "anniversary", "snow", "lost" }
            .Concat(SimCore.DroppedStoryKinds).ToArray();

        /// <summary>How many entries of each story kind a box tells in <paramref name="years"/> years, the eras advanced when ready.</summary>
        public static Dictionary<string, int> StoryCounts(bool ecology, int seed, int years = 100)
        {
            var cfg = new SimConfig { ecology = ecology };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < years * SimConfig.MonthsPerYear; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            var c = StoryKinds.ToDictionary(k => k, k => 0);
            foreach (var e in sim.State.chronicle) if (c.ContainsKey(e.kind)) c[e.kind]++;
            return c;
        }

        [Test]
        public void NoSliceOfLifeTeaser_AndTheRivalryTeaserSaysAgainOnlyOnARepeat()
        {
            var sim = NewSim(5);
            for (int m = 0; m < 240; m++) { sim.Step(); sim.OpenTeaser(); if (sim.State.eraReady) sim.AdvanceEra(); }
            Assert.IsFalse(sim.State.threads.Any(t => t.source == ThreadSource.Slice), "no slice-of-life teaser is ever opened");
            foreach (var t in sim.State.threads) StringAssert.DoesNotContain("raft", t.teaser);

            var box = NewSim(5);
            var adults = box.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).ToList();
            foreach (var n in adults) { n.friendId = -1; n.rivalId = -1; n.partnerId = -1; }
            var a = adults[0]; var b = adults[1];
            a.rivalId = b.id; b.rivalId = a.id;
            box.State.chronicle.Add(new ChronicleEntry { id = 900001, month = box.State.month, kind = "rivalry", text = "x", notableId = a.id, otherNotableId = b.id });
            string first = box.OpenTeaser();
            if (first != null && first.Contains("aren't speaking"))
            {
                StringAssert.EndsWith("aren't speaking.", first, "the first quarrel");
                box.State.chronicle.Add(new ChronicleEntry { id = 900002, month = box.State.month, kind = "reconcile", text = "y", notableId = a.id, otherNotableId = b.id });
                box.State.teaserThreadId = -1;
                foreach (var t in box.State.threads) t.state = ThreadState.Died;
                string again = box.OpenTeaser();
                Assume.That(again, Does.Contain("aren't speaking"));
                StringAssert.EndsWith("aren't speaking again.", again, "after they made up once");
            }
            else Assert.Inconclusive("another teaser came first: " + first);
        }

        [Test]
        public void AnOldSaveWithDroppedStoryKindsLoadsAndKeepsThem()
        {
            var sim = NewSim(9);
            sim.State.chronicle.Add(new ChronicleEntry { id = 900010, month = 3, kind = "storm", text = "A great storm rattled the glass.", salience = 0.5f });
            sim.State.chronicle.Add(new ChronicleEntry { id = 900011, month = 4, kind = "invention", text = "Ada invented a bucket that pours itself.", salience = 0.62f });
            var back = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            var box = new SimCore(new SimConfig(), back);
            for (int m = 0; m < 24; m++) box.Step();
            Assert.IsTrue(box.State.chronicle.Any(e => e.kind == "storm" && e.text.Contains("storm")), "an old storm stays in History");
            Assert.IsTrue(box.State.chronicle.Any(e => e.kind == "invention"));
        }

        [Test]
        public void NoGoatsPiesOrSackRacesInTheStoryText()
        {
            var lines = SimCore.FestivalLines.Concat(SimCore.CampFestivalLines);
            foreach (var t in lines) foreach (var w in new[] { "goat", "pies", "sack race" }) StringAssert.DoesNotContain(w, t);
            var sim = NewSim(7);
            for (int m = 0; m < 100 * 12; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            foreach (var e in sim.State.chronicle.Where(e => StoryKinds.Contains(e.kind) || e.kind == "festival" || e.kind == "era_festival"))
                foreach (var w in new[] { "goat", "pies", "sack race", "in the hills" }) StringAssert.DoesNotContain(w, e.text, e.kind);
        }

        [Test]
        public void TheStoryStreamTellsNoneOfTheDroppedKinds_A100YearRun()
        {
            foreach (bool eco in new[] { true, false })
            {
                var counts = StoryCounts(eco, 7);
                foreach (var k in SimCore.DroppedStoryKinds) Assert.AreEqual(0, counts.TryGetValue(k, out var n) ? n : 0, $"{k} (ecology {eco})");
                Assert.Greater(counts.Values.Sum(), 10, "the stream still tells stories");
            }
        }
    }
}
