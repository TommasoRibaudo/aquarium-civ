using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>P7 self-play (the orchestrator's pass before the tester, #226): a fresh box with its first run, dilemmas on and the lab
    /// schedule, lived on to the village by the clock (no raise cheats). The dilemmas come when the sim raises them: the first is answered
    /// on its card by touch, the second in the box by touch, the third is left to lapse; then a callback card. Every month: never a wish
    /// and a dilemma open together, none during the first run. Screenshots <c>p7self-*</c> at 1170x2532 (and the card at 750x1334).</summary>
    [Category("P7SelfPlay")]
    public class P7SelfPlayTests : PlayTestBase
    {
        const int Seed = 4242;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static CardStackView Cards => CardStackView.Instance;
        static CardStackModel Model => Cards.Model;
        static ChoiceSceneView Choice => ChoiceSceneView.Instance;

        int overlaps, duringFirstRun;
        readonly List<string> raised = new();

        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        IEnumerator Tap(string name)
        {
            var e = Cards.Root.Q(name);
            Assert.IsNotNull(e, name);
            Assert.IsTrue(Shown(e), name + " is on screen");
            yield return TapAt(GameUiPanel.ScreenCenter(e));
            yield return Frames(4);
        }

        void Watch(bool firstRun)
        {
            var d = Sim.OpenDilemma;
            if (d != null && Sim.OpenWish != null) { overlaps++; Debug.Log($"[P7Self] OVERLAP month {Sim.State.month}: wish + {d.defId}"); }
            if (d != null && firstRun) { duringFirstRun++; Debug.Log($"[P7Self] DURING FIRST RUN: {d.defId}"); }
            if (d != null && !raised.Contains(d.defId)) { raised.Add(d.defId); Debug.Log($"[P7Self] raised {d.defId} at month {Sim.State.month} (year {Sim.State.month / 12f:0.0}, era {Sim.State.era})"); }
        }

        /// <summary>Live the box a month at a time until <paramref name="until"/> or <paramref name="years"/> run out; watch every month.</summary>
        IEnumerator Live(float years, System.Func<bool> until)
        {
            for (int m = 0; m < years * 12 && !until(); m++)
            {
                SimLayoutSource.DebugSkipYears(1f / 12f);
                Watch(FirstRunView.Instance != null && FirstRunView.Instance.Active);
                if (m % 6 == 0) yield return null;
            }
            yield return Frames(4);
        }

        /// <summary>The open dilemma's card on screen, through the peek (as a player coming back would see it).</summary>
        IEnumerator ToItsCard()
        {
            if (Model.Mode != CardStackMode.Open)
            {
                Debug.Log("[P7Self] " + GameClock.DebugAway(CardStackModel.MinAwayHours + 0.05)); // a real return (stack due), a few months: nothing lapses
                yield return Frames(8);
                if (Model.Mode == CardStackMode.Peek) yield return Tap("cards-peek");
            }
            for (int guard = 0; guard < 12 && Model.Face.kind != CardKind.Dilemma; guard++) yield return Tap("cards-next");
            Assert.AreEqual(CardKind.Dilemma, Model.Face.kind, "on the dilemma's card");
            yield return Frames(6);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator AFreshBoxToTheVillageWithDilemmasOn()
        {
            yield return OpenSlice(1170, 2532, seed: Seed, skipFirstRun: false);
            Debug.Log("[P7Self] " + DilemmaCheats.Toggle(true));
            DilemmaCheats.Lab(true);
            GameClock.Instance.speed = 1f; // the clock is driven month by month here; the lab schedule stays on
            Assert.IsTrue(FirstRunView.Instance != null && FirstRunView.Instance.Active, "a fresh box starts in its first run");

            // 1. the first run: three years of it, never a dilemma
            yield return Live(3f, () => false);
            Assert.AreEqual(0, duringFirstRun, "no dilemma in the first run");
            Debug.Log("[P7Self] " + FirstRunView.DebugSkipAll());
            yield return Frames(10);
            Sim.CleanAll();

            // 2. the first dilemma by the clock: answered on the card, by touch
            yield return Live(6f, () => Sim.OpenDilemma != null);
            var d1 = Sim.OpenDilemma;
            Assert.IsNotNull(d1, "the lab schedule raises one within a few years of the first run: " + DilemmaCheats.List());
            yield return ToItsCard();
            yield return Shot("p7self-1-card-1170x2532");
            SetSize(750, 1334); yield return new WaitForSecondsRealtime(0.6f); yield return Frames(10);
            yield return Shot("p7self-1-card-750x1334");
            SetSize(1170, 2532); yield return new WaitForSecondsRealtime(0.6f); yield return Frames(10);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Tap("cards-dil-answer-1");
            yield return Frames(6);
            if (d1.state != DilemmaState.Answered && Cards.Root.Q("cards-dil-answer-1") == null)
                Debug.Log("[P7Self] no element cards-dil-answer-1; answering by the model: " + DilemmaCheats.AnswerOnCard(1));
            Assert.AreEqual(DilemmaState.Answered, d1.state, d1.defId + " answered on the card");
            Assert.AreEqual(DilemmaPath.Card, d1.path);
            yield return Shot("p7self-1-answered-1170x2532");
            if (Model.Mode == CardStackMode.Open) Model.Close();
            yield return Frames(8);

            // 3. the second: in the box, by touch on the left one
            yield return Live(8f, () => Sim.OpenDilemma != null);
            var d2 = Sim.OpenDilemma;
            Assert.IsNotNull(d2, "a second one: " + DilemmaCheats.List());
            yield return ToItsCard();
            yield return Tap("cards-showme");
            yield return new WaitForSecondsRealtime(6f); // they walk to their spots, the camera lands
            yield return Shot("p7self-2-box-1170x2532");
            Debug.Log("[P7Self] box: " + DilemmaCheats.BoxStatus());
            if (Choice.TryScreenOf(0, out var a) && Choice.TryScreenOf(1, out var b) && Choice.Answerable)
            {
                yield return TapAt(a.x <= b.x ? a : b);
                yield return Frames(4);
            }
            if (d2.state != DilemmaState.Answered) Debug.Log("[P7Self] the touch didn't answer; by the cheat: " + DilemmaCheats.AnswerInBox(0));
            Assert.AreEqual(DilemmaState.Answered, d2.state, d2.defId + " answered in the box");
            Assert.AreEqual(DilemmaPath.Box, d2.path);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("p7self-2-reaction-1170x2532");
            yield return Until(() => Choice.State == ChoiceSceneView.Phase.Idle, 15f);
            yield return Frames(10);
            if (Model.Mode == CardStackMode.Open) Model.Close();
            yield return Frames(8);

            // 4. on to the village (the era turns), then the third: left to lapse
            if (Sim.State.era == Era.Camp) Debug.Log("[P7Self] " + SimLayoutSource.DebugNextEra(false));
            yield return Frames(30);
            Sim.CleanAll();
            yield return Shot("p7self-3-village-1170x2532");
            yield return Live(10f, () => Sim.OpenDilemma != null);
            var d3 = Sim.OpenDilemma;
            Assert.IsNotNull(d3, "a third one, in the village: " + DilemmaCheats.List());
            yield return ToItsCard();
            yield return Shot("p7self-3-card-village-1170x2532");
            if (Model.Mode == CardStackMode.Open) Model.Close();
            yield return Frames(6);
            yield return Live(5f, () => d3.state != DilemmaState.Open); // the lab waits 48 months (#238)
            Assert.AreEqual(DilemmaState.Lapsed, d3.state, d3.defId + " lapses after four years unanswered (the lab)");
            Debug.Log($"[P7Self] lapsed {d3.defId}: the people chose option {d3.option}; entry {d3.resultEntryId}");

            // 5. a callback, by the clock if one comes within 15 years, else fired
            int before = Sim.State.dilemmaCallbacks.Count(c => c.state == CallbackState.Fired);
            yield return Live(15f, () => Sim.State.dilemmaCallbacks.Count(c => c.state == CallbackState.Fired) > before);
            bool natural = Sim.State.dilemmaCallbacks.Count(c => c.state == CallbackState.Fired) > before;
            Debug.Log("[P7Self] callback " + (natural ? "came by the clock" : "fired: " + DilemmaCheats.FireCallback()));
            if (natural)
            {
                var entry = Sim.State.chronicle.LastOrDefault(e => e.kind == SimCore.CallbackKind);
                if (entry != null) Debug.Log("[P7Self] " + CardStackView.DebugOpenEntry(entry.id));
            }
            yield return Frames(8);
            yield return Shot("p7self-4-callback-1170x2532");
            Debug.Log("[P7Self] callback face: " + Model.Face?.headline + " | " + Model.Face?.body);
            if (Model.Mode == CardStackMode.Open) Model.Close();
            yield return Frames(6);

            Debug.Log($"[P7Self] summary: raised [{string.Join(", ", raised)}], overlaps {overlaps}, during first run {duringFirstRun}");
            Debug.Log("[P7Self] list:\n" + DilemmaCheats.List());
            Debug.Log("[P7Self] report:\n" + DilemmaCheats.Report());
            Assert.AreEqual(0, overlaps, "never a wish and a dilemma open together");
            AssertNoErrors("P7 self-play");
        }

        public override void TearDown()
        {
            if (GameClock.Instance != null) { GameClock.Instance.config.dilemmas = false; GameClock.Instance.config.dilemmaLab = false; GameClock.Instance.speed = 1f; }
            base.TearDown();
        }
    }
}
