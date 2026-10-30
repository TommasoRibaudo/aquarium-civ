using System;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The first run (#94): the state machine, its hint timing, the opt-in start, naming, the guaranteed first rain,
    /// "Their first year" and resuming after a kill. Pure C#; the UI of phase 2 only calls what is tested here.</summary>
    public class FirstRunTests
    {
        static (SimCore sim, FirstRun run) Fresh(int seed = 7)
        {
            var sim = NewSim(seed);
            return (sim, FirstRun.Begin(sim));
        }

        static void TickTo(FirstRun run, double seconds, bool touching = false, float dt = 0.25f)
        {
            for (double t = 0; t < seconds; t += dt) run.Tick(dt, touching);
        }

        /// <summary>Plays the whole run the way a player would.</summary>
        static void PlayThrough(SimCore sim, FirstRun run, string name = null)
        {
            TickTo(run, FirstRun.ArriveSeconds + 0.5);
            Assert.AreEqual(FirstRunStep.Wipe, run.Step);
            sim.CleanGrime(1f); run.Tick(0.1f, true);
            Assert.AreEqual(FirstRunStep.Patch, run.Step);
            sim.Prune(1f); run.Tick(0.1f, true);
            Assert.AreEqual(FirstRunStep.Name, run.Step);
            Assert.AreEqual(NameProblem.None, run.KeepName(name));
            Assert.AreEqual(FirstRunStep.Rain, run.Step);
            var (x, z) = run.SuggestedRainSpot;
            Assert.IsTrue(run.PlaceRain(x, z).Ok);
            Assert.AreEqual(FirstRunStep.Year, run.Step);
            for (int i = 0; i < 400 && run.Step == FirstRunStep.Year; i++) run.AdvanceYear(0.1f);
            Assert.AreEqual(FirstRunStep.Card, run.Step);
        }

        // ---------- start ----------
        [Test]
        public void NoFirstRunUnlessBegun()
        {
            var sim = NewSim(5);
            Assert.IsFalse(sim.State.firstRun.active);
            sim.Advance(5);
            Assert.IsFalse(sim.State.firstRun.active);
            Assert.IsFalse(FirstRun.Resume(sim).Active);
            Assert.AreEqual(0.0, sim.State.firstRun.playSeconds);
        }

        [Test]
        public void BeginSetsUpTheOvergrownPatchAndTheGrimyGlass()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var cfg = new SimConfig { terrainPreset = seed - 1 };
                var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                var run = FirstRun.Begin(sim);
                Assert.IsTrue(run.Active);
                Assert.AreEqual(FirstRunStep.Arrive, run.Step);
                Assert.GreaterOrEqual(sim.State.grime, FirstRun.StartGrime);
                Assert.GreaterOrEqual(sim.State.overgrowth, FirstRun.StartOvergrowth);
                Assert.AreEqual(0, sim.State.trash.Count);
                var (px, pz) = run.PatchSpot; var (rx, rz) = run.SuggestedRainSpot;
                Assert.LessOrEqual(Math.Abs(px), 3f); Assert.LessOrEqual(Math.Abs(pz), 2.2f);
                Assert.LessOrEqual(Math.Abs(rx), 3f); Assert.LessOrEqual(Math.Abs(rz), 2.2f);
                Assert.Greater(Math.Sqrt((px - rx) * (px - rx) + (pz - rz) * (pz - rz)), 1f, "the rain does not fall on the patch");
                Assert.IsTrue(SettlementNaming.IsValid(run.SuggestedName));
                Assert.AreEqual("", run.PlateName, "blank brass plate until kept");
            }
        }

        // ---------- the flow ----------
        [Test]
        public void FullRunReachesTheFirstCardAndEnds()
        {
            var (sim, run) = Fresh();
            var seen = new System.Collections.Generic.List<FirstRunStep>();
            run.StepChanged += s => seen.Add(s);
            PlayThrough(sim, run, "Mossy Hollow");
            Assert.AreEqual("Mossy Hollow", sim.PlaceName);
            var sum = run.Summary();
            Assert.IsTrue(sum.rainAnswered, "the first rain is answered inside the first year");
            Assert.IsNotNull(sum.card);
            StringAssert.Contains("rain", sum.cardText.ToLowerInvariant());
            Assert.GreaterOrEqual(sum.flyToBuildingId, 0, "Show me has somewhere to fly");
            Assert.AreEqual("Their first year", sum.caption);
            Assert.AreEqual(12, sim.State.month - sim.State.firstRun.yearStartMonth);
            run.AcknowledgeCard();
            Assert.AreEqual(FirstRunStep.Done, run.Step);
            Assert.IsFalse(run.Active);
            CollectionAssert.AreEqual(new[] { FirstRunStep.Wipe, FirstRunStep.Patch, FirstRunStep.Name, FirstRunStep.Rain, FirstRunStep.Year, FirstRunStep.Card, FirstRunStep.Done }, seen);
        }

        [Test]
        public void TheFirstRainIsAnsweredInsideTheYearForEverySeedEvenWithNoCharges()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var (sim, run) = Fresh(seed);
                sim.State.shapingCharges = 0f;
                PlayThrough(sim, run);
                var sum = run.Summary();
                Assert.IsTrue(sum.rainAnswered, $"seed {seed}");
                var n = sim.State.nudges.Single(x => x.id == sim.State.firstRun.rainNudgeId);
                Assert.IsTrue(n.credited);
                Assert.LessOrEqual(n.answerMonth - n.month, SimCore.FirstRainAnswerMonths + 1, $"seed {seed}");
                Assert.AreEqual(sim.State.firstRun.rainNotableId, sim.State.chronicle.Last(e => e.keeper).notableId, "credited to the wisher");
                Assert.AreEqual(WishState.Given, run.RainWish.state);
            }
        }

        [Test]
        public void RainStepRaisesARealRainWish()
        {
            var (sim, run) = Fresh();
            TickTo(run, 3);
            sim.CleanGrime(1f); run.Tick(0.1f, true);
            sim.Prune(1f); run.Tick(0.1f, true);
            Assert.IsNull(run.RainWish);
            run.KeepName(null);
            Assert.AreEqual(FirstRunStep.Rain, run.Step);
            var w = run.RainWish;
            Assert.IsNotNull(w);
            Assert.AreEqual(WishState.Open, w.state);
            Assert.AreEqual(SimCore.NudgeKind.Rain, w.kind);
            Assert.AreSame(w, sim.OpenWish);
        }

        [Test]
        public void NothingBlocksAndStepsMayComeInAnyOrder()
        {
            var (sim, run) = Fresh();
            // The player cleans the patch and the glass before the thump is over, and rains through the ordinary tray.
            sim.Prune(1f);
            sim.CleanGrime(1f);
            var rain = sim.PlaceShaping(SimCore.NudgeKind.Rain, 0.5f, 0.5f);
            Assert.IsTrue(rain.Ok);
            run.Tick(0.1f, true);
            Assert.AreEqual(FirstRunStep.Arrive, run.Step, "the arrival still takes its moment");
            TickTo(run, 3);
            Assert.AreEqual(FirstRunStep.Name, run.Step, "wipe and patch were already done");
            run.KeepName("Testwick");
            Assert.AreEqual(FirstRunStep.Year, run.Step, "the ordinary rain counts");
            Assert.AreEqual(rain.nudge.id, sim.State.firstRun.rainNudgeId);
            for (int i = 0; i < 400 && run.Step == FirstRunStep.Year; i++) run.AdvanceYear(0.1f);
            Assert.IsTrue(rain.nudge.credited);
        }

        [Test]
        public void SkipStepAndSkipAllNeverStrand()
        {
            var (sim, run) = Fresh(3);
            for (int i = 0; i < 12 && run.Active; i++) run.SkipStep();
            Assert.AreEqual(FirstRunStep.Done, run.Step);
            Assert.IsFalse(run.Active);
            Assert.AreEqual(run.SuggestedName, sim.PlaceName, "a skipped plate keeps the suggestion");
            Assert.IsTrue(run.Summary().rainAnswered, "skipping the rain still gives the year its story");

            var (sim2, run2) = Fresh(4);
            run2.SkipAll();
            Assert.IsFalse(run2.Active);
            Assert.IsTrue(run2.Skipped);
            Assert.AreEqual(run2.SuggestedName, sim2.PlaceName);
            run2.Tick(1f, true); // harmless
            Assert.AreEqual(FirstRunStep.Done, run2.Step);
        }

        [Test]
        public void DebugSkipToReachesAnyStep()
        {
            foreach (FirstRunStep target in new[] { FirstRunStep.Name, FirstRunStep.Rain, FirstRunStep.Year, FirstRunStep.Card })
            {
                var (sim, run) = Fresh(9);
                run.DebugSkipTo(target);
                Assert.AreEqual(target, run.Step);
            }
            var (s, r) = Fresh(9);
            r.DebugSkipTo(FirstRunStep.Card);
            Assert.LessOrEqual(s.State.grime, FirstRun.WipedGrime);
            Assert.LessOrEqual(s.State.overgrowth, FirstRun.ClearedOvergrowth);
        }

        [Test]
        public void ReplayStartsOver()
        {
            var (sim, run) = Fresh(2);
            run.DebugSkipTo(FirstRunStep.Card);
            run.AcknowledgeCard();
            Assert.IsFalse(run.Active);
            var again = FirstRun.Replay(sim);
            Assert.IsTrue(again.Active);
            Assert.AreEqual(FirstRunStep.Arrive, again.Step);
            Assert.GreaterOrEqual(sim.State.grime, FirstRun.StartGrime);
        }

        // ---------- hints ----------
        [Test]
        public void HintsAreAtMostThreeWordsAndEveryStepThatNeedsOneHasOne()
        {
            foreach (var h in FirstRun.AllHints())
            {
                Assert.IsTrue(h.visible);
                Assert.LessOrEqual(h.text.Split(' ').Length, FirstRun.HintMaxWords, h.text);
                Assert.AreNotEqual(HintGesture.None, h.gesture);
                Assert.AreNotEqual(HintAnchor.None, h.anchor);
            }
        }

        [Test]
        public void HintShowsOnceThenReturnsAfterTwentyFiveIdleSecondsAtMostThreeTimes()
        {
            var (sim, run) = Fresh();
            TickTo(run, FirstRun.ArriveSeconds + 0.3);
            Assert.AreEqual(FirstRunStep.Wipe, run.Step);
            Assert.IsFalse(run.Hint.visible, "not at the very start of the step");
            TickTo(run, FirstRun.HintDelaySeconds + 0.3);
            Assert.IsTrue(run.Hint.visible);
            Assert.AreEqual("wipe", run.Hint.id);
            run.Tick(0.1f, true); // the player starts wiping (grime untouched here)
            Assert.IsFalse(run.Hint.visible, "the finger goes once the player acts");
            TickTo(run, FirstRun.HintIdleSeconds - 1);
            Assert.IsFalse(run.Hint.visible, "not before 25 s idle");
            int comebacks = 0;
            for (int round = 0; round < 6; round++)
            {
                TickTo(run, FirstRun.HintIdleSeconds + 1);
                if (run.Hint.visible) { comebacks++; run.Tick(0.1f, true); }
            }
            Assert.AreEqual(FirstRun.HintMaxReturns, comebacks, "back at most 3 times");
            TickTo(run, 300);
            Assert.IsFalse(run.Hint.visible, "then never again");
        }

        [Test]
        public void TouchingResetsTheIdleWait()
        {
            var (sim, run) = Fresh();
            TickTo(run, 4);
            run.Tick(0.1f, true);
            for (int i = 0; i < 5; i++) { TickTo(run, 20); run.Tick(0.1f, true); }
            Assert.IsFalse(run.Hint.visible, "20 s gaps never add up to 25 idle seconds");
            Assert.AreEqual(0, sim.State.firstRun.hintReturns);
        }

        [Test]
        public void HintResetsForTheNextStepAndRainHintMovesOnWhenTheTrayOpens()
        {
            var (sim, run) = Fresh();
            TickTo(run, 4);
            sim.CleanGrime(1f); run.Tick(0.1f, true);
            Assert.AreEqual(FirstRunStep.Patch, run.Step);
            Assert.IsFalse(run.Hint.visible, "a new step starts quiet");
            TickTo(run, 1.2);
            Assert.AreEqual("patch", run.Hint.id);
            sim.Prune(1f); run.Tick(0.1f, true);
            run.KeepName(null);
            TickTo(run, 1.2);
            Assert.AreEqual("rain-tap", run.Hint.id);
            run.NoteTrayOpened();
            Assert.AreEqual("rain-rub", run.Hint.id);
            Assert.IsTrue(run.Hint.visible);
        }

        [Test]
        public void ChildDrawsASmileyAfterTwentySecondsUntouchedOnceOnly()
        {
            var (sim, run) = Fresh();
            int smileys = 0;
            run.Cue += c => { if (c == FirstRunCue.ChildSmiley) smileys++; };
            TickTo(run, FirstRun.ArriveSeconds + 0.3);
            TickTo(run, FirstRun.SmileyIdleSeconds - 1);
            Assert.AreEqual(0, smileys);
            TickTo(run, 3);
            Assert.AreEqual(1, smileys);
            TickTo(run, 60);
            Assert.AreEqual(1, smileys);
        }

        [Test]
        public void AnUntouchedPlateKeepsTheSuggestionAndTheRunGoesOn()
        {
            var (sim, run) = Fresh();
            TickTo(run, 3);
            sim.CleanGrime(1f); run.Tick(0.1f, true);
            sim.Prune(1f); run.Tick(0.1f, true);
            Assert.AreEqual(FirstRunStep.Name, run.Step);
            TickTo(run, FirstRun.NameAutoKeepSeconds + 1);
            Assert.AreEqual(FirstRunStep.Rain, run.Step);
            Assert.AreEqual(run.SuggestedName, sim.PlaceName);
        }

        // ---------- saving ----------
        [Test]
        public void ResumesAfterAKillWhereItWas()
        {
            string dir = Path.Combine(Path.GetTempPath(), "aqciv-fr-" + Guid.NewGuid().ToString("N"));
            try
            {
                var (sim, run) = Fresh(11);
                TickTo(run, 4);
                sim.CleanGrime(1f); run.Tick(0.1f, true);
                TickTo(run, 2); run.Tick(0.1f, true);
                string path = Path.Combine(dir, "box.json");
                SaveStore.Write(path, sim.State, DateTime.UtcNow);
                Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var env));
                var sim2 = new SimCore(sim.Config, env.state);
                var run2 = FirstRun.Resume(sim2);
                Assert.IsTrue(run2.Active);
                Assert.AreEqual(FirstRunStep.Patch, run2.Step);
                Assert.AreEqual(run.SuggestedName, run2.SuggestedName);
                Assert.AreEqual(run.PatchSpot, run2.PatchSpot);
                Assert.IsFalse(run2.Hint.visible, "the finger does not jump back the instant the app opens");
                sim2.Prune(1f);
                run2.Tick(0.1f, true);
                Assert.AreEqual(FirstRunStep.Name, run2.Step);
                // killed mid year: the skip resumes
                run2.DebugSkipTo(FirstRunStep.Year);
                for (int i = 0; i < 20; i++) run2.AdvanceYear(0.25f);
                SaveStore.Write(path, sim2.State, DateTime.UtcNow);
                SaveStore.Read(path, out var env2);
                var sim3 = new SimCore(sim.Config, env2.state);
                var run3 = FirstRun.Resume(sim3);
                Assert.AreEqual(FirstRunStep.Year, run3.Step);
                Assert.AreEqual(run2.YearProgress, run3.YearProgress, 1e-5);
                for (int i = 0; i < 100 && run3.Step == FirstRunStep.Year; i++) run3.AdvanceYear(0.25f);
                Assert.AreEqual(FirstRunStep.Card, run3.Step);
                Assert.AreEqual(12, sim3.State.month - sim3.State.firstRun.yearStartMonth);
                Assert.IsTrue(run3.Summary().rainAnswered);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test]
        public void AFinishedRunStaysFinishedAfterReload()
        {
            var (sim, run) = Fresh(6);
            PlayThrough(sim, run);
            run.AcknowledgeCard();
            var back = new SimCore(sim.Config, UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(sim.State)));
            Assert.IsFalse(FirstRun.Resume(back).Active);
            Assert.AreEqual(FirstRunStep.Done, FirstRun.Resume(back).Step);
        }

        // ---------- the name on the box ----------
        [Test]
        public void ANamedBoxKeepsItsNameAndThePeopleDoNotRenameIt()
        {
            var (sim, run) = Fresh(8);
            run.KeepName("Kettle Green");
            sim.Advance(40);
            Assert.AreEqual("Kettle Green", sim.PlaceName);
            Assert.IsFalse(sim.State.chronicle.Any(e => e.kind == "naming"), "the plate already has a name");
        }

        [Test]
        public void ARefusedNameChangesNothing()
        {
            var (sim, run) = Fresh(8);
            Assert.AreEqual(NameProblem.Blocked, run.KeepName("shit hole"));
            Assert.AreEqual("", sim.State.settlementName);
            Assert.IsFalse(sim.State.firstRun.nameKept);
        }

        [Test]
        public void RerollGivesAnotherValidSuggestion()
        {
            var (_, run) = Fresh(8);
            string a = run.SuggestedName, b = run.RerollName();
            Assert.AreNotEqual(a, b);
            Assert.IsTrue(SettlementNaming.IsValid(b));
        }

        // ---------- the summary ----------
        [Test]
        public void SummaryHasNumbersAndAFallbackForAQuietYear()
        {
            var (sim, run) = Fresh(12);
            Assert.AreEqual(FirstRun.FallbackLine, run.Summary().cardText, "before the year there is only the fallback");
            PlayThrough(sim, run);
            var sum = run.Summary();
            Assert.AreEqual(sim.State.population, sum.populationEnd);
            Assert.AreEqual(sim.State.buildings.Count, sum.buildingsEnd);
            Assert.GreaterOrEqual(sum.buildingsEnd, sum.buildingsStart + 1, "the rain's answer is a new building");
            Assert.IsFalse(string.IsNullOrEmpty(sum.cardText));
            Assert.IsFalse(sum.cardText.Contains("{"));
        }

        [Test]
        public void StartingOptInLeavesOtherWorldsAlone()
        {
            var a = NewSim(21); a.Advance(10);
            var b = NewSim(21); FirstRun.Begin(b); // mess and hints only; the economy's dice are never touched
            b.Advance(10);
            Assert.AreEqual(a.State.population, b.State.population);
            Assert.AreEqual(a.State.buildings.Count, b.State.buildings.Count);
            Assert.AreEqual(a.State.rng.Range(0, 1000000), b.State.rng.Range(0, 1000000));
        }
    }

    public class SettlementNamingTests
    {
        static NameProblem P(string s) => SettlementNaming.Check(s, out _);

        [TestCase("Glasswick")]
        [TestCase("Little Hollow")]
        [TestCase("St. Mary's Fold")]
        [TestCase("Ada-by-the-Sea")]
        [TestCase("Camp 7")]
        [TestCase("Scunthorpe")]
        [TestCase("Hancock")]
        [TestCase("Cumbria")]
        [TestCase("Dickens Row")]
        public void GoodNamesPass(string s) => Assert.AreEqual(NameProblem.None, P(s));

        [Test]
        public void ShapeRules()
        {
            Assert.AreEqual(NameProblem.Empty, P(null));
            Assert.AreEqual(NameProblem.Empty, P("   "));
            Assert.AreEqual(NameProblem.TooShort, P("A"));
            Assert.AreEqual(NameProblem.TooLong, P("Abcdefghijklmnopqrstu"));
            Assert.AreEqual(NameProblem.None, P("Abcdefghijklmnopqrst"));
            Assert.AreEqual(NameProblem.BadCharacters, P("Glass<wick>"));
            Assert.AreEqual(NameProblem.BadCharacters, P("Hello❤"));
            Assert.AreEqual(NameProblem.NotEnoughLetters, P("12 3"));
            Assert.AreEqual(NameProblem.NotEnoughLetters, P("A1"));
            Assert.AreEqual(NameProblem.Repeated, P("Aaaaah"));
            Assert.AreEqual(NameProblem.None, P("Hollow"), "double letters are fine");
        }

        [Test]
        public void TidyTrimsSqueezesAndCapitalises()
        {
            Assert.AreEqual("Little Hollow", SettlementNaming.Tidy("   little    hollow  "));
            Assert.AreEqual("McTavish", SettlementNaming.Tidy("McTavish"));
            Assert.AreEqual("Glasswick", SettlementNaming.Tidy("--Glasswick--"));
            SettlementNaming.Check("  dewmoor ", out var clean);
            Assert.AreEqual("Dewmoor", clean);
        }

        [TestCase("Fuck")]
        [TestCase("f u c k")]
        [TestCase("Fuuuck town")]
        [TestCase("Sh1t Creek")]
        [TestCase("Big Cunt")]
        [TestCase("B1tch")]
        [TestCase("Hitler Land")]
        [TestCase("Nazi Nest")]
        [TestCase("Fuckton")]
        [TestCase("The Porn")]
        public void TheWorstWordsAreRefused(string s) => Assert.AreEqual(NameProblem.Blocked, P(s), s);

        [Test]
        public void MessagesExistForEveryProblem()
        {
            foreach (NameProblem p in Enum.GetValues(typeof(NameProblem)))
                Assert.AreEqual(p == NameProblem.None, SettlementNaming.Message(p) == "", p.ToString());
        }

        [Test]
        public void DefaultsAreAllValidDistinctAndStable()
        {
            Assert.AreEqual(SettlementNaming.Defaults.Length, SettlementNaming.Defaults.Distinct().Count());
            foreach (var n in SettlementNaming.Defaults) Assert.AreEqual(NameProblem.None, P(n), n);
            Assert.AreEqual(SettlementNaming.Suggest(5), SettlementNaming.Suggest(5));
            Assert.GreaterOrEqual(Enumerable.Range(1, 30).Select(i => SettlementNaming.Suggest(i)).Distinct().Count(), 8, "seeds vary the suggestion");
            Assert.AreNotEqual(SettlementNaming.Suggest(5), SettlementNaming.Suggest(5, 1));
        }
    }
}
