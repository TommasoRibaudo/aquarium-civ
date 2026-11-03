using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#225 phase 2: every P7 cheat on the real Slice, and the telemetry they leave. The A/B switch and the lab preset; then
    /// raise → answer on the card → fire its callback (card) → Show me on it; raise → stage in the box → answer in the box; raise → lapse.
    /// The log of this session must hold every dilemma event with the right fields, and <see cref="TelemetryAnalyzer"/> must summarise it.</summary>
    [Category("DilemmaCheats")]
    public class DilemmaCheatsPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static CardStackModel Model => CardStackView.Instance.Model;

        IEnumerator Raise(string def)
        {
            string line = null;
            for (int i = 0; i < 5; i++)
            {
                line = DilemmaCheats.Raise(def);
                if (Sim.OpenDilemma != null && Sim.OpenDilemma.defId == def) break;
                SimLayoutSource.DebugSkipYears(1f);
                yield return null;
            }
            Debug.Log("[DilemmaCheats] " + line);
            Assert.IsNotNull(Sim.OpenDilemma, def + ": " + line);
            Assert.AreEqual(def, Sim.OpenDilemma.defId, line);
            yield return Frames(4);
        }

        List<TelemetryEvent> SessionEvents(out List<string> lines)
        {
            var log = GameClock.Tel;
            Assert.IsNotNull(log, "telemetry runs");
            string session = log.SessionId;
            var all = log.ReadAllLines();
            lines = new List<string>();
            var evs = new List<TelemetryEvent>();
            foreach (var l in all)
            {
                var e = TelemetryEvent.Parse(l);
                if (e == null || e.session != session) continue;
                lines.Add(l);
                evs.Add(e);
            }
            return evs;
        }

        [UnityTest, Timeout(420000)]
        public IEnumerator EveryCheatWorksAndLeavesItsTelemetry()
        {
            yield return OpenSlice(1170, 2532, seed: Seed);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            var clock = GameClock.Instance;

            // the A/B switch and the lab preset
            Debug.Log("[DilemmaCheats] " + DilemmaCheats.Toggle(true));
            Assert.IsTrue(clock.config.dilemmas, "dilemmas on");
            Assert.AreEqual(DilemmaSurvey.VariantOn, GameClock.Tel.Variant, "variant B");
            Assert.IsTrue(RecallPromptView.AsksAboutChoices, "the recall question is P7's");
            Debug.Log("[DilemmaCheats] " + DilemmaCheats.Lab(true));
            Assert.IsTrue(clock.config.dilemmaLab);
            Assert.AreEqual(GameClock.LabSpeed, clock.speed, "the lab runs at x60");
            long before = Sim.State.month;
            yield return new WaitForSecondsRealtime(6.5f); // a month is 5 real seconds at x60
            Assert.Greater(Sim.State.month - before, 0, "at x60 the months run (a year a real minute)");
            DilemmaCheats.Lab(false);
            Assert.AreEqual(1f, clock.speed);
            Assert.IsFalse(clock.config.dilemmaLab);
            foreach (var id in DilemmaCheats.Ids) Debug.Log($"[DilemmaCheats] {id}: {DilemmaCheats.WhyNot(id) ?? "can be raised"}");

            // 1. raise on the card, answer on the card, the callback, Show me on it
            yield return Raise("camp_travellers");
            var d1 = Sim.OpenDilemma;
            Assert.AreEqual(CardStackMode.Open, Model.Mode, "the stack opens on it");
            Assert.AreEqual(CardKind.Dilemma, Model.Face.kind, "on its card");
            yield return new WaitForSecondsRealtime(1.5f); // some reading time
            Debug.Log("[DilemmaCheats] " + DilemmaCheats.AnswerOnCard(1));
            Assert.AreEqual(DilemmaState.Answered, d1.state);
            Assert.AreEqual(DilemmaPath.Card, d1.path);
            Assert.AreEqual(1, d1.option);
            Assert.Greater(Sim.PendingCallbacks().Count, 0, "the answer has callbacks to come");
            string cb = DilemmaCheats.FireCallback();
            Debug.Log("[DilemmaCheats] " + cb);
            yield return Frames(4);
            Assert.IsNotNull(Model.Shown, cb);
            Assert.AreEqual(SimCore.CallbackKind, Model.Shown.lead.kind, "the stack is on the callback card: " + cb);
            bool showMe = Model.Face.canShowMe;
            if (showMe)
            {
                Debug.Log("[DilemmaCheats] " + CardStackView.DebugShowMe());
                yield return Frames(6);
            }
            Model.Close();
            yield return Frames(6);

            // 2. raise, stage the box scene, answer in the box
            yield return Raise("camp_fire");
            var d2 = Sim.OpenDilemma;
            Debug.Log("[DilemmaCheats] " + DilemmaCheats.StageInBox());
            Assert.AreEqual(ChoiceSceneView.Phase.Waiting, ChoiceSceneView.Instance.State, "staged");
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("[DilemmaCheats] " + DilemmaCheats.AnswerInBox(0));
            Assert.AreEqual(DilemmaState.Answered, d2.state);
            Assert.AreEqual(DilemmaPath.Box, d2.path);
            Debug.Log("[DilemmaCheats] " + DilemmaCheats.LeaveBox());
            yield return Until(() => ChoiceSceneView.Instance.State == ChoiceSceneView.Phase.Idle, 12f);
            if (Model.Mode == CardStackMode.Open) Model.Close();
            yield return Frames(4);

            // 3. raise, lapse
            yield return Raise("camp_remember");
            var d3 = Sim.OpenDilemma;
            Debug.Log("[DilemmaCheats] " + DilemmaCheats.Lapse());
            Assert.AreEqual(DilemmaState.Lapsed, d3.state);
            if (Model.Mode == CardStackMode.Open) Model.Close();
            yield return Frames(4);

            string list = DilemmaCheats.List();
            Debug.Log("[DilemmaCheats] list:\n" + list);
            StringAssert.Contains("camp_travellers", list);
            StringAssert.Contains("answered 2 (card)", list);
            StringAssert.Contains("answered 1 (box)", list);
            StringAssert.Contains("lapsed to", list);
            StringAssert.Contains("apart: the", list, "the travellers' camp is listed");

            // the telemetry of this session
            var evs = SessionEvents(out var lines);
            IEnumerable<TelemetryEvent> Named(string n) => evs.Where(e => e.name == n);
            int[] ids = { d1.id, d2.id, d3.id };
            foreach (int id in ids) Assert.AreEqual(1, Named(Ev.DilemmaRaised).Count(e => (int)e.Num("id", -1) == id), $"dilemma_raised once for #{id}");
            Assert.AreEqual(1, Named(Ev.DilemmaShown).Count(e => (int)e.Num("id", -1) == d1.id), "dilemma_shown once for the card one");
            Assert.AreEqual(1, Named(Ev.DilemmaShown).Count(e => (int)e.Num("id", -1) == d2.id), "dilemma_shown once for the box one (card, then the box: still once)");
            Assert.AreEqual(1, Named(Ev.DilemmaShown).Count(e => (int)e.Num("id", -1) == d3.id), "dilemma_shown once for the lapsed one");
            var a1 = Named(Ev.DilemmaAnswered).Single(e => (int)e.Num("id", -1) == d1.id);
            Assert.AreEqual("card", a1.Str("path")); Assert.AreEqual(1, (int)a1.Num("option", -1));
            Assert.IsTrue(a1.TryGet("secs", out _), "seconds from shown on the card answer");
            Assert.GreaterOrEqual(a1.Num("secs"), 1.0, "it was read for a moment");
            var a2 = Named(Ev.DilemmaAnswered).Single(e => (int)e.Num("id", -1) == d2.id);
            Assert.AreEqual("box", a2.Str("path")); Assert.AreEqual(0, (int)a2.Num("option", -1));
            Assert.IsTrue(a2.TryGet("years_open", out _));
            Assert.AreEqual(1, Named(Ev.DilemmaLapsed).Count(e => (int)e.Num("id", -1) == d3.id), "dilemma_lapsed");
            Assert.AreEqual(1, Named(Ev.CallbackShown).Count(e => (int)e.Num("id", -1) == d1.id), "callback_shown");
            if (showMe) Assert.AreEqual(1, Named(Ev.CallbackShowMe).Count(e => (int)e.Num("id", -1) == d1.id), "callback_showme");
            Assert.IsTrue(Named(Ev.Variant).Any(e => e.Str("v") == "B"), "the switch to B is in the record");
            Debug.Log("[DilemmaCheats] events: " + string.Join(", ", evs.Where(e => e.name.StartsWith("dilemma") || e.name.StartsWith("callback") || e.name == Ev.Variant).Select(e => e.name)));

            // the analyzer reads a real play log
            var r = TelemetryAnalyzer.Analyze(lines, GameClock.TelemetryNow);
            Assert.AreEqual(0, r.badLines, "every line parses");
            Assert.IsTrue(r.dilemmasByVariant.TryGetValue("B", out var b), "a B summary");
            Debug.Log("[DilemmaCheats] " + b.ToText());
            // #239: every dilemma here was raised by a cheat, so its events carry debug=1 and stay out of the lab numbers
            foreach (int id in ids)
                foreach (var ev in evs.Where(e => e.name.StartsWith("dilemma_") && (int)e.Num("id", -1) == id))
                    Assert.AreEqual(1, (int)ev.Num("debug"), $"{ev.name} #{id} is a cheat event");
            Assert.AreEqual(0, b.raised); Assert.AreEqual(0, b.shown); Assert.AreEqual(0, b.answered); Assert.AreEqual(0, b.lapsed);
            Assert.AreEqual(0, b.callbacksShown, "the callback is the cheat-raised dilemma's");
            Assert.GreaterOrEqual(b.cheatEvents, 3 + 3 + 2 + 1 + 1, "raised, shown, answered, lapsed and callback events counted as cheat events");
            StringAssert.Contains("not counted", b.ToText());
            Assert.IsNotEmpty(DilemmaCheats.Report());

            // and back to A
            DilemmaCheats.Toggle(false);
            Assert.AreEqual(DilemmaSurvey.VariantOff, GameClock.Tel.Variant);
            AssertNoErrors("dilemma cheats");
        }

        public override void TearDown()
        {
            ChoiceSceneView.DebugWatch = false;
            if (GameClock.Instance != null) { GameClock.Instance.config.dilemmas = false; GameClock.Instance.config.dilemmaLab = false; GameClock.Instance.speed = 1f; }
            base.TearDown();
        }
    }
}
