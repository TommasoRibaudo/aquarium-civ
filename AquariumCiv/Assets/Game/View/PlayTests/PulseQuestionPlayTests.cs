using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #636: the playtest's quick questions by touch on the real Slice scene. An answer lands in the Feedback outbox as an fb:pulse issue
    /// with the whole package (the answer, the screenshot taken when the question opened, the context, the session stats, the recent
    /// events, the game log, and the box's people and stories). The sender's token is blanked: nothing reaches GitHub.
    /// </summary>
    public class PulseQuestionPlayTests : PlayTestBase
    {
        string outboxBackup;

        IEnumerator Open(int w = 1179, int h = 2556)
        {
            outboxBackup = FeedbackSender.OutboxDir + "~pulsetest";
            if (Directory.Exists(outboxBackup)) Directory.Delete(outboxBackup, true);
            if (Directory.Exists(FeedbackSender.OutboxDir)) Directory.Move(FeedbackSender.OutboxDir, outboxBackup);
            Directory.CreateDirectory(Shots + "636");
            yield return OpenSlice(w, h, seed: 5151);
            var sender = FeedbackSender.Ensure();
            typeof(FeedbackSender).GetField("token", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sender, ""); // never reaches GitHub
            Assert.IsNotNull(PulseQuestionView.Instance, "the bootstrap made the view");
            PulseQuestionView.DebugReset();
        }

        void Restore()
        {
            if (Directory.Exists(FeedbackSender.OutboxDir)) Directory.Delete(FeedbackSender.OutboxDir, true);
            if (Directory.Exists(outboxBackup)) Directory.Move(outboxBackup, FeedbackSender.OutboxDir);
        }

        IEnumerator Tap(string name)
        {
            var v = PulseQuestionView.Instance;
            var e = v.Root.Q(name);
            Assert.IsNotNull(e, name);
            yield return TapAt(GameUiPanel.ScreenCenter(e));
        }

        IEnumerator AskAndWait(string id)
        {
            var v = PulseQuestionView.Instance;
            StringAssert.StartsWith("asking", PulseQuestionView.DebugAsk(id));
            float t = 0f;
            while (!v.IsOpen && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(v.IsOpen, $"'{id}' is on screen");
            yield return new WaitForSecondsRealtime(0.6f); // the sheet slides in
        }

        static string NewestPulse()
        {
            var files = Directory.Exists(FeedbackSender.OutboxDir) ? Directory.GetFiles(FeedbackSender.OutboxDir, "*-pulse.json") : new string[0];
            return files.OrderBy(f => f).LastOrDefault();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator AnAnswerByTouchGoesOutWithTheWholeLog()
        {
            yield return Open();
            try
            {
                var v = PulseQuestionView.Instance;
                Assert.IsFalse(v.Context().firstRunDone, "tester: a minute to themselves after the first run before any question");
                yield return AskAndWait("know");
                Assert.AreEqual("know", v.Asking.id);
                yield return Shot("636/know_open");
                yield return Tap("pulse-answer-1"); // "Sort of"
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsFalse(v.IsOpen, "answering closes it");
                var json = NewestPulse();
                Assert.IsNotNull(json, "the answer waits in the outbox");
                var payload = File.ReadAllText(json);
                StringAssert.Contains("Pulse know (day ", payload, "the title says the question");
                StringAssert.Contains("Sort of", payload);
                StringAssert.Contains("fb:pulse", payload);
                StringAssert.Contains("Right now, do you know what to do next?", payload);
                StringAssert.Contains("### Context", payload);
                StringAssert.Contains("| Box | ", payload, "the box's key numbers");
                StringAssert.Contains("Session stats", payload, "the event log summary");
                StringAssert.Contains("Recent events", payload, "the last telemetry lines");
                StringAssert.Contains("Game log", payload, "the game log");
                StringAssert.Contains("**Living:**", payload, "the people, to check a recall answer");
                StringAssert.Contains("Latest stories", payload);
                StringAssert.Contains(FeedbackReport.ScreenshotMarker, payload, "a screenshot rides along");
                Assert.IsTrue(File.Exists(Path.ChangeExtension(json, ".jpg")), "the screenshot taken when the question opened");
                Assert.AreEqual(1, v.State.Of("know").roundsDone, "the first round is done");
                Assert.IsNull(PulseQuestions.Next(v.State, v.Context()), "one question per session");
            }
            finally { Restore(); }
        }

        /// <summary>Tester (1640x2360): a second row of answers covered the question. At a phone and a tablet size the question sits
        /// above every answer, and every answer is on screen.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator TheQuestionNeverHidesBehindItsAnswers()
        {
            foreach (var (w, h) in new[] { (1640, 2360), (750, 1334), (1179, 2556) })
            {
                yield return Open(w, h);
                try
                {
                    var v = PulseQuestionView.Instance;
                    foreach (var id in new[] { "mine", "back", "know" })
                    {
                        yield return AskAndWait(id);
                        yield return Shot($"636/layout_{id}_{w}x{h}");
                        var body = v.Root.Q<GlassCard>("pulse-card").Body.worldBound;
                        var panel = v.Root.panel.visualTree.worldBound;
                        for (int i = 0; i < PulseQuestions.Find(id).answers.Length; i++)
                        {
                            var b = v.Root.Q("pulse-answer-" + i).worldBound;
                            Assert.GreaterOrEqual(b.yMin + 0.5f, body.yMax, $"{id} {w}x{h}: answer {i} starts below the question");
                            Assert.LessOrEqual(b.yMax, panel.yMax + 0.5f, $"{id} {w}x{h}: answer {i} on screen");
                            Assert.Greater(b.width, 60f, $"{id} {w}x{h}: answer {i} is a real button");
                        }
                        yield return Tap("pulse-dismiss");
                        yield return new WaitForSecondsRealtime(0.5f);
                    }
                }
                finally { Restore(); }
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ANameIsTypedAndSent()
        {
            yield return Open();
            try
            {
                var v = PulseQuestionView.Instance;
                yield return AskAndWait("name");
                yield return Tap("pulse-answer-0"); // "Yes"
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsTrue(v.IsOpen, "Yes opens the text step");
                var field = v.Root.Q<TextField>("pulse-text");
                Assert.AreEqual(DisplayStyle.Flex, v.Root.Q("pulse-text-step").resolvedStyle.display);
                field.value = "Aveline, the singer";
                yield return Shot("636/name_text");
                yield return Tap("pulse-send");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsFalse(v.IsOpen);
                var payload = File.ReadAllText(NewestPulse());
                StringAssert.Contains("| Typed | Aveline, the singer |", payload);
                StringAssert.Contains("Pulse name (day ", payload);
                var log = GameClock.Tel.RecentLines(10);
                Assert.IsTrue(log.Any(l => l.Contains("pulse_answer") && l.Contains("\"q\":\"name\"")), "logged");
                Assert.IsFalse(log.Any(l => l.Contains("Aveline, the singer")), "never the typed words in the telemetry log");
            }
            finally { Restore(); }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ConfusedOpensTheFeedbackSheetAsConfusing()
        {
            yield return Open();
            try
            {
                yield return AskAndWait("lost");
                yield return Tap("pulse-answer-1"); // "Yes"
                var fb = FeedbackUI.Instance;
                float t = 0f;
                while ((fb == null || !fb.IsOpen) && t < 4f) { t += Time.unscaledDeltaTime; yield return null; fb = FeedbackUI.Instance; }
                Assert.IsTrue(fb != null && fb.IsOpen, "the Feedback sheet opens");
                var chip = fb.Root.Q<GlassChip>("fb-kind-confusing");
                Assert.IsNotNull(chip);
                Assert.IsTrue(chip.On, "Confusing is picked");
                Assert.IsNotNull(NewestPulse(), "the answer itself went out too");
                fb.Close();
            }
            finally { Restore(); }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator NotNowAsksAgainInALaterSession()
        {
            yield return Open();
            try
            {
                var v = PulseQuestionView.Instance;
                yield return AskAndWait("tidy");
                yield return Tap("pulse-dismiss");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsFalse(v.IsOpen);
                Assert.IsNull(NewestPulse(), "Not now sends nothing");
                var r = v.State.Of("tidy");
                Assert.AreEqual(1, r.dismissed);
                Assert.AreEqual(0, r.roundsDone, "the round stays open");
            }
            finally { Restore(); }
        }
    }
}
