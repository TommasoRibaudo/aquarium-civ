using System.Linq;
using AquariumCiv.Game;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#636: when the playtest's quick questions are asked (PulseQuestions). Pure C#, so it also runs in Tools/SimTests.</summary>
    public class PulseQuestionsTests
    {
        static PulseContext Ctx(int day, int session, double secs = 60, double away = 2, bool firstRunDone = true, bool choice = false, bool busy = false) =>
            new PulseContext { dayIndex = day, session = session, sessionSeconds = secs, awayHours = away, firstRunDone = firstRunDone, choiceThisVisit = choice, busy = busy };

        static PulseDef Ask(PulseState s, PulseContext c)
        {
            var d = PulseQuestions.Next(s, c);
            if (d != null) PulseQuestions.Shown(s, d, c);
            return d;
        }

        [Test]
        public void TheFirstQuestionIsWhetherTheyKnowWhatToDo()
        {
            var s = new PulseState();
            Assert.IsNull(PulseQuestions.Next(s, Ctx(0, 1, firstRunDone: false)), "never during the first run");
            Assert.IsNull(PulseQuestions.Next(s, Ctx(0, 1, secs: 5)), "not in the first seconds");
            Assert.IsNull(PulseQuestions.Next(s, Ctx(0, 1, busy: true)), "not over something");
            Assert.IsNull(PulseQuestions.Next(s, Ctx(0, 1, choice: true)), "not in a visit that had a choice");
            Assert.AreEqual("know", PulseQuestions.Next(s, Ctx(0, 1)).id);
        }

        [Test]
        public void OnePerSessionAndAtMostThreeADay()
        {
            var s = new PulseState();
            var d = Ask(s, Ctx(2, 1));
            Assert.IsNotNull(d);
            PulseQuestions.Answered(s, d, 2);
            Assert.IsNull(PulseQuestions.Next(s, Ctx(2, 1)), "one per session");
            for (int session = 2; session <= 3; session++)
            {
                var q = Ask(s, Ctx(2, session));
                Assert.IsNotNull(q, $"session {session}");
                PulseQuestions.Answered(s, q, 2);
            }
            Assert.IsNull(PulseQuestions.Next(s, Ctx(2, 4)), "three a day at most");
            Assert.IsNotNull(PulseQuestions.Next(s, Ctx(3, 5)), "the next day, again");
        }

        [Test]
        public void EveryQuestionIsAskedOverAWeekAndTheRepeatsComeBack()
        {
            var s = new PulseState();
            var asked = new System.Collections.Generic.List<(int day, string id)>();
            int session = 0;
            for (int day = 0; day <= 9; day++)
                for (int k = 0; k < 4; k++)
                {
                    var c = Ctx(day, ++session, secs: k == 0 ? 30 : 120);
                    var d = Ask(s, c);
                    if (d == null) continue;
                    asked.Add((day, d.id));
                    PulseQuestions.Answered(s, d, day);
                }
            foreach (var def in PulseQuestions.All)
                Assert.AreEqual(def.rounds.Length, asked.Count(a => a.id == def.id), $"{def.id}: every round asked once");
            foreach (var (day, id) in asked)
                Assert.GreaterOrEqual(day, PulseQuestions.Find(id).rounds[0], $"{id} not before its day");
            Assert.AreEqual(0, asked.First(a => a.id == "know").day);
            Assert.GreaterOrEqual(asked.Last(a => a.id == "know").day, 3, "know comes back on day 3");
            Assert.GreaterOrEqual(asked.Last(a => a.id == "mine").day, 6, "mine comes back on day 6");
            Assert.GreaterOrEqual(asked.Single(a => a.id == "keep").day, 7);
            Assert.LessOrEqual(asked.GroupBy(a => a.day).Max(g => g.Count()), PulseQuestions.MaxPerDay);
        }

        [Test]
        public void WhatBroughtYouBackIsAskedEarlyAndOnlyAfterAnAbsence()
        {
            var s = new PulseState();
            PulseQuestions.Answered(s, PulseQuestions.Find("know"), 0);
            var back = new[] { PulseQuestions.Find("back") };
            Assert.IsNull(PulseQuestions.Next(s, Ctx(1, 2, secs: 200), back), "late in a session: no");
            Assert.IsNull(PulseQuestions.Next(s, Ctx(1, 2, secs: 30, away: 0.1), back), "no real absence: no");
            Assert.AreEqual("back", PulseQuestions.Next(s, Ctx(1, 2, secs: 30), back)?.id);
            Assert.AreEqual("back", PulseQuestions.Next(s, Ctx(1, 2, secs: 5), back)?.id, "it may come in the first seconds");
        }

        [Test]
        public void NotNowAsksAgainLaterAndTwiceGivesTheRoundUp()
        {
            var s = new PulseState();
            var d = Ask(s, Ctx(0, 1));
            Assert.AreEqual("know", d.id);
            PulseQuestions.Dismissed(s, d);
            Assert.IsNull(PulseQuestions.Next(s, Ctx(0, 1)), "not again this session");
            Assert.AreEqual("know", Ask(s, Ctx(0, 2)).id, "again in the next session");
            PulseQuestions.Dismissed(s, d);
            Assert.AreEqual(1, s.Of("know").roundsDone, "two Not nows give the round up");
            Assert.IsNull(PulseQuestions.Next(s, Ctx(0, 3)), "nothing else is due on day 0");
        }

        [Test]
        public void ARepeatWaitsForAnotherDay()
        {
            var s = new PulseState();
            var know = PulseQuestions.Find("know");
            PulseQuestions.Answered(s, know, 3); // round 1 answered late, on day 3
            var only = new[] { know };
            Assert.IsNull(PulseQuestions.Next(s, Ctx(3, 9), only), "round 2 opens on day 3, but not the same day as round 1's answer");
            Assert.AreEqual("know", PulseQuestions.Next(s, Ctx(4, 10), only)?.id);
        }

        [Test]
        public void SessionsCountOncePerId()
        {
            var s = new PulseState();
            Assert.AreEqual(1, s.See("a"));
            Assert.AreEqual(1, s.See("a"));
            Assert.AreEqual(2, s.See("b"));
        }

        [Test]
        public void TheTitleSaysQuestionDayAndAnswer()
        {
            var d = PulseQuestions.Find("name");
            Assert.AreEqual("[Slice] Pulse name (day 2, session 5): Yes - Aveline", PulseQuestions.Title(d, "Yes", 2, 5, "Aveline"));
            Assert.AreEqual("[Slice] Pulse name (day 2, session 5): Not really", PulseQuestions.Title(d, "Not really", 2, 5, ""));
            CollectionAssert.Contains(PulseQuestions.Labels, "fb:pulse");
        }

        [Test]
        public void TheWordsFitAPhone()
        {
            foreach (var d in PulseQuestions.All)
            {
                Assert.LessOrEqual(d.question.Length, 64, d.id);
                Assert.That(d.answers.Length, Is.InRange(2, 4), d.id);
                foreach (var a in d.answers) Assert.LessOrEqual(a.Length, 14, $"{d.id}: {a}");
                Assert.AreEqual(d.rounds.OrderBy(x => x).ToArray(), d.rounds, d.id);
            }
            Assert.AreEqual(PulseQuestions.All.Length, PulseQuestions.All.Select(d => d.id).Distinct().Count());
        }
    }
}
