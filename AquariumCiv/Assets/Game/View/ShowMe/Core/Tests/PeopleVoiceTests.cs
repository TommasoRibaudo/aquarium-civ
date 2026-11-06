using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Audio;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>#120, #181, #191, #122 phase 1: more lines in the people's voice (no repeats, not even in the next session),
    /// talk about what the town wants and lacks (true, from the sim's own choice, read-only), the wish payoff's plan, and the
    /// notification question's words.</summary>
    public class PeopleVoiceTests
    {
        static readonly string[] CampOnly = { "windmill", "market", "tavern", "church", "workshop", "bakery", "guildhall", "shop", "well" };
        static readonly string[] VillageLater = { "workshop", "bakery", "guildhall", "shop" };

        // ----------------------------------------------------------------------------------------------- #181: what the town wants

        /// <summary>The query is the choice TryBuild makes: whenever it says "this would go up" and the town didn't change size
        /// in that step, exactly that building goes up; whenever nothing is wanted, nothing does.</summary>
        [Test]
        public void TheQueryIsWhatTryBuildThenBuilds()
        {
            int checkedBuilds = 0, checkedNone = 0;
            foreach (int seed in new[] { 3, 7, 42 })
            {
                var sim = ShowMeTests.NewSim(seed);
                for (int m = 0; m < 12 * 70; m++)
                {
                    var w = sim.NextWant();
                    int pop = sim.State.population, count = sim.State.buildings.Count;
                    sim.Step();
                    if (sim.State.population == pop && sim.State.buildings.Count < sim.Config.buildingSlots)
                    {
                        if (w.building != null && w.stop == WantStop.None)
                        {
                            Assert.AreEqual(count + 1, sim.State.buildings.Count, $"seed {seed} month {m}: {w.building} goes up");
                            Assert.AreEqual(w.building, sim.State.buildings.Last().type, $"seed {seed} month {m}");
                            checkedBuilds++;
                        }
                        else if (w.building == null || w.stop == WantStop.People)
                        {
                            Assert.AreEqual(count, sim.State.buildings.Count, $"seed {seed} month {m}: nothing wanted, nothing built");
                            checkedNone++;
                        }
                    }
                    if (sim.State.eraReady) sim.AdvanceEra();
                }
            }
            Assert.Greater(checkedBuilds, 5, "the run builds");
            Assert.Greater(checkedNone, 10);
        }

        /// <summary>Asking, however often, changes nothing: the same seed with and without the questions ends in the same state.</summary>
        [Test]
        public void AskingChangesNothing()
        {
            string Run(bool ask)
            {
                var sim = ShowMeTests.NewSim(11);
                for (int m = 0; m < 12 * 40; m++)
                {
                    if (ask) { sim.NextWant(); foreach (var l in PeopleVoice.WantLines(sim, m)) { } }
                    sim.Step();
                    if (sim.State.eraReady) sim.AdvanceEra();
                }
                return JsonUtility.ToJson(sim.State);
            }
            Assert.AreEqual(Run(false), Run(true));
        }

        static SimCore Camp(int pop, float wood, float food)
        {
            var sim = ShowMeTests.NewSim(5);
            for (int m = 0; m < 24; m++) sim.Step();
            sim.State.population = pop; sim.State.wood = wood; sim.State.food = food;
            return sim;
        }

        [Test]
        public void ABuildingShortOfWoodIsAskedForByName()
        {
            var sim = Camp(14, 0f, 200f);
            var w = sim.NextWant();
            Assert.AreEqual(WantStop.Wood, w.stop);
            var line = PeopleVoice.WantLineFor(sim, WantCase.Wood) ?? PeopleVoice.WantLineFor(sim, WantCase.Crowded);
            Assert.IsNotNull(line, "someone says what's missing");
            if (line.kind == WantCase.Wood) StringAssert.Contains(PeopleVoice.Thing(sim, w.building).Replace("the ", "").Replace("new ", ""), line.text.ToLowerInvariant());
        }

        [Test]
        public void NotEnoughPeopleYetAndContentAreTheirOwnCases()
        {
            var sim = ShowMeTests.NewSim(5);
            for (int m = 0; m < 24; m++) sim.Step();
            // A camp at 5 people with the first farm up and wood to spare: nothing wanted yet, the lumbermill waits for 6.
            sim.State.population = 5; sim.State.wood = 500f; sim.State.food = 500f;
            while (sim.Count(BuildingType.Farm) < 1 || sim.Housing - 1 <= 5) sim.State.buildings.Add(new Building { id = sim.State.NextId(), type = sim.Count(BuildingType.Farm) < 1 ? BuildingType.Farm : BuildingType.Hut });
            var w = sim.NextWant();
            Assert.AreEqual(WantStop.People, w.stop, w.building);
            Assert.IsNotNull(PeopleVoice.WantLineFor(sim, WantCase.People));
            var content = PeopleVoice.WantLineFor(sim, WantCase.Content);
            Assert.IsNotNull(content, "nothing missing: a contented line");
            Assert.IsNull(PeopleVoice.WantLineFor(sim, WantCase.Wood));
            Assert.IsNull(PeopleVoice.WantLineFor(sim, WantCase.FoodLow));
        }

        [Test]
        public void CrowdedAndHungryHaveTheirLines()
        {
            var sim = Camp(12, 500f, 500f);
            sim.State.population = sim.Housing; // every bed taken
            var crowded = PeopleVoice.WantLineFor(sim, WantCase.Crowded);
            Assert.IsNotNull(crowded);
            StringAssert.Contains("hut", crowded.text);
            Assert.IsNull(PeopleVoice.WantLineFor(sim, WantCase.Content), "not contented when crowded");
            sim.State.food = 1f;
            var hungry = PeopleVoice.WantLineFor(sim, WantCase.FoodLow);
            Assert.IsNotNull(hungry);
            sim.State.food = sim.State.population * 3f;
            Assert.IsNull(PeopleVoice.WantLineFor(sim, WantCase.FoodLow), "fed again: the line is gone");
        }

        /// <summary>Over whole runs, no want line names a one-of-a-kind building that's already up or one from a later era, and
        /// every line is allowed at its belief stage.</summary>
        [Test]
        public void NoWantLineNamesABuildingThatsUpOrFromALaterEra()
        {
            var single = new[] { BuildingType.Lumbermill, BuildingType.Well, BuildingType.Windmill, BuildingType.Market, BuildingType.Tavern, BuildingType.Church, BuildingType.Workshop, BuildingType.Bakery, BuildingType.Guildhall };
            int seen = 0; var cases = new HashSet<WantCase>();
            foreach (int seed in new[] { 3, 7, 42 })
            {
                var sim = ShowMeTests.NewSim(seed);
                for (int m = 0; m < 12 * 80; m++)
                {
                    sim.Step();
                    if (sim.State.eraReady) sim.AdvanceEra();
                    for (int v = 0; v < 4; v++)
                        foreach (var l in PeopleVoice.WantLines(sim, v))
                        {
                            seen++; cases.Add(l.kind);
                            Assert.IsTrue(PeopleVoice.Allowed(l.text, sim), l.text);
                            string t = l.text.ToLowerInvariant();
                            if (l.building != null)
                            {
                                Assert.IsTrue(sim.Allowed(l.building), $"{l.building} is not from a later era: {l.text}");
                                if (Array.IndexOf(single, l.building) >= 0) Assert.AreEqual(0, sim.Count(l.building), $"{l.building} is already up: {l.text}");
                            }
                            if (sim.State.era == Era.Camp) foreach (var b in CampOnly) Assert.IsFalse(t.Contains(b), $"camp says '{b}': {l.text}");
                            if (sim.State.era != Era.Town) foreach (var b in VillageLater) Assert.IsFalse(t.Contains(b), $"{sim.State.era} says '{b}': {l.text}");
                            if (sim.State.era == Era.Camp) Assert.IsFalse(t.Contains("village") || t.Contains("town"), l.text);
                        }
                }
            }
            Assert.Greater(seen, 50);
            CollectionAssert.Contains(cases, WantCase.Wood);
            CollectionAssert.Contains(cases, WantCase.Content);
        }

        /// <summary>Only some of the talk is about the town: with wants available, a run of bubbles is mostly the rest.</summary>
        [Test]
        public void OnlySomeOfTheTalkIsAboutTheTown()
        {
            var sim = ShowMeTests.SimIn(Era.Village, 7);
            sim.DebugSetBelief(2, true);
            var wb = new WorldBubbles(sim, new BubbleRules { warmUpSeconds = 0f });
            var got = new List<WorldBubble>();
            for (int i = 0; i < 9; i++) { var b = wb.Next(1000 + i * 25); if (b != null) got.Add(b); } // as fast as the rate limit lets them
            int wants = got.Count(b => b.source == BubbleSource.Want);
            Assert.GreaterOrEqual(got.Count, 8);
            Assert.Greater(wants, 0, "the town's wants are in the mix");
            Assert.LessOrEqual(wants * 3, got.Count + 2, "about one in three at most while there is other talk");
            Assert.IsNotNull(wb.ForceWant(WantCase.Content, 1) ?? wb.ForceWant(WantCase.Wood, 1) ?? wb.ForceWant(WantCase.People, 1) ?? wb.ForceWant(WantCase.Crowded, 1) ?? wb.ForceWant(WantCase.FoodLow, 1));
        }

        [Test]
        public void ForceWantSaysEachCaseThatIsTrue()
        {
            var sim = Camp(12, 500f, 500f);
            sim.State.population = sim.Housing;
            sim.State.food = 1f;
            var wb = new WorldBubbles(sim);
            var h = wb.ForceWant(WantCase.FoodLow, 0);
            var c = wb.ForceWant(WantCase.Crowded, 0);
            Assert.IsNotNull(h); Assert.IsNotNull(c);
            Assert.AreEqual(BubbleSource.Want, h.source);
            Assert.IsNull(wb.ForceWant(WantCase.Content, 0), "not true now");
            Assert.IsEmpty(sim.State.saidBubbles, "a want line isn't kept as said: it can come round again");
        }

        // ----------------------------------------------------------------------------------------------- #120: more lines, no repeats

        [Test]
        public void WhatWasSaidIsRememberedInTheNextSession()
        {
            var sim = ShowMeTests.SimIn(Era.Village, 7);
            sim.DebugSetBelief(2, true);
            var first = new WorldBubbles(sim, new BubbleRules { warmUpSeconds = 0f });
            var said = new List<string>();
            for (int i = 0; i < 6; i++) { var b = first.Force(i); if (b != null && b.source != BubbleSource.Want) said.Add(b.key); }
            Assert.GreaterOrEqual(said.Count, 3);
            var second = new WorldBubbles(sim, new BubbleRules { warmUpSeconds = 0f }); // a new session on the same box
            var again = second.Candidates().Select(b => b.key).ToList();
            foreach (var k in said) CollectionAssert.DoesNotContain(again, k, "not said again next session");
            Assert.AreEqual(said.Count, sim.State.saidBubbles.Count(k => said.Contains(k)));
        }

        [Test]
        public void TheMemoryIsCapped()
        {
            var sim = ShowMeTests.NewSim(1);
            for (int i = 0; i < WorldBubbles.SaidKept + 50; i++) sim.State.saidBubbles.Add("old:" + i);
            var wb = new WorldBubbles(sim);
            wb.Reset();
            Assert.IsEmpty(sim.State.saidBubbles, "Reset forgets");
        }

        [Test]
        public void BrothersAndSistersGetDifferentBirthLines()
        {
            var sim = ShowMeTests.NewSim(2);
            for (int m = 0; m < 12 * 60 && sim.State.chronicle.Count(e => e.kind == "birth") < 3; m++) sim.Step();
            var births = sim.State.chronicle.Where(e => e.kind == "birth").ToList();
            Assume.That(births.Count, Is.GreaterThanOrEqualTo(2));
            var byParent = births.Select(e => (e, v: PeopleVoice.StoryLine(sim, e)))
                .Where(x => x.v != null).GroupBy(x => x.v.speakerId).FirstOrDefault(g => g.Count() > 1);
            if (byParent == null) { Assert.Pass("no parent with two living-speaker births in this run"); return; }
            var texts = byParent.Select(x => x.v.text.Replace(sim.FindNotable(x.e.notableId).name, "{c}")).ToList();
            Assert.AreEqual(texts.Count, texts.Distinct().Count(), "two babies of one parent, two different lines");
        }

        [Test]
        public void EveryKindHasSeveralWordingsAndTheNoseLineKeepsTheChildsName()
        {
            var sim = ShowMeTests.SimIn(Era.Village, 7);
            sim.DebugSetBelief(2, true);
            var kinds = new[] { "invention", "giant_turnip", "prank", "song", "brave", "kind", "dreamer", "lazy", "loud", "lost", "found", "elder", "newcomer", "keeper" };
            var who = sim.State.notables.First(n => n.alive);
            foreach (var kind in kinds)
            {
                var texts = new HashSet<string>();
                for (int id = 0; id < 6; id++)
                {
                    var q = PeopleVoice.StoryLine(sim, new ChronicleEntry { id = id, kind = kind, notableId = who.id, salience = 0.8f });
                    Assert.IsNotNull(q, kind);
                    texts.Add(q.text);
                }
                Assert.GreaterOrEqual(texts.Count, 2, $"{kind}: more than one way to say it");
            }
            var child = sim.State.notables.FirstOrDefault(n => n.alive && n.parentId >= 0 && sim.FindNotable(n.parentId)?.alive == true);
            if (child == null) return;
            var seen = new HashSet<string>();
            for (int id = 0; id < 12; id++)
            {
                var q = PeopleVoice.StoryLine(sim, new ChronicleEntry { id = id, kind = "birth", notableId = child.id, salience = 0.8f });
                StringAssert.Contains(child.name, q.text);
                seen.Add(q.text);
            }
            Assert.GreaterOrEqual(seen.Count, 1);
        }

        [Test]
        public void TraitLinesFollowTheBeliefStageAndTheRules()
        {
            Assert.AreEqual(42, PeopleVoice.TraitLines.Count);
            Assert.AreEqual(PeopleVoice.TraitLines.Count, PeopleVoice.TraitLines.Select(l => l.id).Distinct().Count());
            foreach (var t in NameData.Traits) foreach (int stage in new[] { 1, 2, 3 })
                Assert.AreEqual(1, PeopleVoice.TraitLines.Count(l => l.trait == t && l.stage == stage), $"{t} at stage {stage}");
            foreach (int stage in new[] { 1, 2, 3 })
            {
                var sim = stage == 1 ? ShowMeTests.NewSim(4) : ShowMeTests.SimIn(stage == 2 ? Era.Village : Era.Town, stage == 2 ? 7 : 1234);
                if (stage == 1) for (int m = 0; m < 24; m++) sim.Step();
                sim.DebugSetBelief(stage, false); // the early half: no "glass" yet
                var now = PeopleVoice.TraitLinesNow(sim).ToList();
                Assert.IsNotEmpty(now, $"stage {stage}");
                foreach (var (l, who) in now)
                {
                    Assert.AreEqual(stage, l.stage);
                    Assert.AreEqual(l.trait, sim.FindNotable(who).trait);
                    Assert.IsTrue(PeopleVoice.Allowed(l.text, sim), l.text);
                }
            }
            foreach (var l in PeopleVoice.TraitLines)
            {
                var low = l.text.ToLowerInvariant();
                foreach (var banned in new[] { "aquarium", "box", "player", "phone", "game", "screen", "terrarium", "glass" }) StringAssert.DoesNotContain(banned, low, l.text);
                if (l.stage == 1) { StringAssert.DoesNotContain("keeper", low, l.text); StringAssert.DoesNotContain("window", low, l.text); StringAssert.DoesNotContain("scholar", low, l.text); }
                if (l.stage == 2) StringAssert.DoesNotContain("scholar", low, l.text);
            }
        }

        // ----------------------------------------------------------------------------------------------- #191: a wish given

        static (SimCore sim, Wish wish) WithWish()
        {
            var sim = ShowMeTests.SimIn(Era.Village, 7);
            sim.DebugSetBelief(2, true);
            var w = sim.DebugForceWish(SimCore.NudgeKind.Rain);
            Assert.IsNotNull(w);
            return (sim, w);
        }

        [Test]
        public void ADayWishPlaysTheSoundAndTheWisherAnswersAtOnce()
        {
            var (sim, w) = WithWish();
            var plan = WishPayoff.Plan(sim, w, WishBeat.Given, night: false, isAwake: null, awakeIds: null, wisherOnScreen: false);
            Assert.AreEqual(CueLibrary.WishGiven, plan.cue);
            Assert.IsNotNull(CueLibrary.Get(plan.cue));
            Assert.IsTrue(plan.effectNow);
            Assert.IsTrue(plan.wisherSaysNow);
            Assert.AreEqual(w.notableId, plan.wisherLine.speakerId);
            Assert.IsTrue(plan.flyToWisher, "off screen: fly to them");
            Assert.IsFalse(WishPayoff.Plan(sim, w, WishBeat.Given, false, null, null, true).flyToWisher);
            var met = WishPayoff.Plan(sim, w, WishBeat.Met, false, null, null, true);
            Assert.AreEqual(CueLibrary.CheerSmall, met.cue, "the second beat is smaller");
        }

        [Test]
        public void AtNightSomeoneAwakeSpeaksOrTheWisherOnWaking()
        {
            var (sim, w) = WithWish();
            var other = sim.State.notables.First(n => n.alive && n.id != w.notableId).id;
            var plan = WishPayoff.Plan(sim, w, WishBeat.Given, true, id => id == other, new[] { other }, true);
            Assert.IsTrue(plan.quiet);
            Assert.IsTrue(plan.effectNow, "the gift lands either way");
            Assert.IsFalse(plan.wisherSaysNow, "the wisher is asleep");
            Assert.IsNotNull(plan.wisherLine, "...and says it on waking");
            Assert.AreEqual(other, plan.witnessLine.speakerId);
            var awake = WishPayoff.Plan(sim, w, WishBeat.Given, true, id => id == w.notableId, new[] { w.notableId }, true);
            Assert.IsTrue(awake.wisherSaysNow);
            Assert.IsNull(awake.witnessLine);
            var nobody = WishPayoff.Plan(sim, w, WishBeat.Given, true, id => false, new int[0], true);
            Assert.IsNull(nobody.witnessLine);
            Assert.IsNotNull(nobody.wisherLine);
        }

        [Test]
        public void WishReactionsAreAllowedForEveryToolAndStage()
        {
            foreach (int stage in new[] { 1, 2, 3 })
            {
                var sim = stage == 1 ? ShowMeTests.NewSim(4) : ShowMeTests.SimIn(stage == 2 ? Era.Village : Era.Town, stage == 2 ? 7 : 1234);
                if (stage == 1) for (int m = 0; m < 24; m++) sim.Step();
                sim.DebugSetBelief(stage, false);
                var who = sim.State.notables.First(n => n.alive);
                foreach (var kind in new[] { SimCore.NudgeKind.Rain, SimCore.NudgeKind.Grove, SimCore.NudgeKind.Herd, SimCore.NudgeKind.Channel })
                    for (int id = 0; id < 4; id++)
                        foreach (bool met in new[] { false, true })
                        {
                            var q = PeopleVoice.WishReaction(sim, new Wish { id = id, notableId = who.id, kind = kind, state = met ? WishState.Met : WishState.Given }, met);
                            Assert.IsNotNull(q, $"{kind} stage {stage}");
                            Assert.IsTrue(PeopleVoice.Allowed(q.text, sim), q.text);
                            if (stage == 1) StringAssert.DoesNotContain("Keeper", q.text);
                        }
            }
        }

        // ----------------------------------------------------------------------------------------------- #122

        [Test]
        public void TheNotificationQuestionSaysWhatItsAbout()
        {
            Assert.AreEqual(MomentLibrary.NotifyQuestion, MomentLibrary.CampToVillage().beats.First(b => b.actions.Contains(BeatAction.AskNotification)).line);
            StringAssert.DoesNotContain("the next one", MomentLibrary.NotifyQuestion);
            StringAssert.Contains("people", MomentLibrary.NotifyQuestion);
        }

        // ----------------------------------------------------------------------------------------------- t1-moments: a staged moment's own words

        [Test]
        public void EachStagedMomentHasItsOwnShortClosedWorldLines()
        {
            var sim = ShowMeTests.SimIn(Era.Village, 7);
            foreach (var kind in new[] { MomentKind.Festival, MomentKind.Wedding, MomentKind.Newborn, MomentKind.NewLeader })
            {
                var lines = PeopleVoice.MomentLines(kind, 1, 2);
                Assert.GreaterOrEqual(lines.Count, 3, kind + " has a few lines");
                foreach (var (text, _) in lines)
                {
                    Assert.IsTrue(PeopleVoice.Allowed(text, sim), text);
                    Assert.LessOrEqual(PeopleVoice.Wrap(text).Count, 2, "short: " + text);
                    StringAssert.DoesNotContain("getting up", text);
                }
            }
            CollectionAssert.IsEmpty(PeopleVoice.MomentLines(MomentKind.Callback, 1, 2), "a callback has its own line");
        }

        [Test]
        public void WhileAMomentPlaysTheBubblesAreItsLinesNotTheStories()
        {
            var sim = ShowMeTests.SimIn(Era.Village, 7);
            sim.Chronicle("lazy", "Somebody rested.", 0.9f, 1);
            var wb = new WorldBubbles(sim, new BubbleRules { warmUpSeconds = 0f })
            {
                HeldNow = new HeldMoment { id = 77, kind = MomentKind.Festival, personA = -1, personB = -1 },
            };
            var texts = PeopleVoice.MomentLines(MomentKind.Festival, -1, -1).Select(l => l.text).ToList();
            var got = new List<WorldBubble>();
            for (int i = 0; i < 12; i++) { var b = wb.Next(100 + i * 10); if (b != null) got.Add(b); }
            Assert.GreaterOrEqual(got.Count, 3);
            foreach (var b in got) CollectionAssert.Contains(texts, b.text);
            Assert.AreEqual(got.Count, got.Select(b => b.text).Distinct().Count(), "no line twice in a moment");
        }
    }
}
