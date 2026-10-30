using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.GameUI.Tests
{
    public class FeedbackReportTests
    {
        [Serializable] class Issue { public string title; public string body; public string[] labels; }

        static readonly List<(string, string)> Ctx = new() { ("Box", "Village · year 12.5"), ("Device", "Pixel 8 | Android 15") };

        [Test]
        public void LabelsMatchThePrototypeScheme()
        {
            CollectionAssert.AreEqual(new[] { "feedback", "fb:bug", "proto:slice" }, FeedbackReport.Labels(FeedbackKind.Bug));
            CollectionAssert.AreEqual(new[] { "feedback", "fb:praise", "proto:slice" }, FeedbackReport.Labels(FeedbackKind.Praise));
        }

        [Test]
        public void TitleUsesTheFirstLineAndCutsLongOnes()
        {
            Assert.AreEqual("[Slice] Idea: more fire (Camp, y3)", FeedbackReport.Title(FeedbackKind.Idea, "more fire\nand more logs", "Camp, y3"));
            var t = FeedbackReport.Title(FeedbackKind.Bug, new string('x', 200), "");
            Assert.That(t.Length, Is.LessThan(80));
            StringAssert.EndsWith("...", t);
            StringAssert.Contains("(no text)", FeedbackReport.Title(FeedbackKind.Bug, "  ", ""));
        }

        [Test]
        public void BodyCarriesTextContextLogsAndScreenshotMarker()
        {
            var body = FeedbackReport.Body(FeedbackKind.Confusing, "what is this?\nsecond line", true, "ab12cd34", Ctx, new[] { "12:00:01 [info] hi", "12:00:02 [ERROR] boom" });
            StringAssert.Contains("> what is this?\n> second line", body);
            StringAssert.Contains(FeedbackReport.ScreenshotMarker, body);
            StringAssert.Contains("| Device | Pixel 8 / Android 15 |", body, "a pipe in a value must not break the table");
            StringAssert.Contains("Game log (last 2 lines)", body);
            StringAssert.Contains("[ERROR] boom", body);
            StringAssert.Contains("kind=confusing proto=slice", body);
        }

        [Test]
        public void NoScreenshotMeansNoMarkerAndNoLogsNoDetails()
        {
            var body = FeedbackReport.Body(FeedbackKind.Feel, "nice", false, "x", Ctx, new List<string>());
            StringAssert.DoesNotContain(FeedbackReport.ScreenshotMarker, body);
            StringAssert.DoesNotContain("<details>", body);
        }

        [Test]
        public void LogsCannotCloseTheCodeFence()
        {
            var body = FeedbackReport.Body(FeedbackKind.Bug, "x", false, "x", Ctx, new[] { "```evil```" });
            StringAssert.DoesNotContain("```evil", body);
        }

        [Test]
        public void PayloadIsValidJsonThatRoundTrips()
        {
            string text = "quote \" backslash \\ tab \t newline\nunicode é 😀";
            var body = FeedbackReport.Body(FeedbackKind.Idea, text, false, "x", Ctx, new[] { "a \"b\"" });
            var json = FeedbackReport.PayloadJson("T \"1\"", body, FeedbackReport.Labels(FeedbackKind.Idea));
            var parsed = JsonUtility.FromJson<Issue>(json);
            Assert.AreEqual("T \"1\"", parsed.title);
            Assert.AreEqual(body.Replace("\r", ""), parsed.body);
            Assert.AreEqual(3, parsed.labels.Length);
        }

        [Test]
        public void ScreenshotMarkerSurvivesEscapingSoItCanBeSwappedAtSendTime()
        {
            var json = FeedbackReport.PayloadJson("t", "a " + FeedbackReport.ScreenshotMarker + " b", new[] { "feedback" });
            StringAssert.Contains(FeedbackReport.ScreenshotMarker, json);
            var swapped = json.Replace(FeedbackReport.ScreenshotMarker, FeedbackReport.Escape("![s](u)\nnext \"q\""));
            Assert.AreEqual("a ![s](u)\nnext \"q\" b", JsonUtility.FromJson<Issue>(swapped).body);
        }

        [Test]
        public void LogRingKeepsTheNewestLinesOldestFirst()
        {
            LogRing.Clear();
            for (int i = 0; i < 500; i++) LogRing.Add("line " + i);
            var last = LogRing.Last(3);
            CollectionAssert.AreEqual(new[] { "line 497", "line 498", "line 499" }, last);
            Assert.LessOrEqual(LogRing.Last(10000).Count, 400);
            LogRing.Clear();
        }

        [Test]
        public void LogRingTrimsLongStacks()
        {
            var trimmed = LogRing.Trim("a\nb\nc\nd\ne\nf\ng\nh", 3);
            Assert.AreEqual("a\nb\nc\n  ...", trimmed);
        }

        [Test]
        public void DeviceInfoHasTheRowsAReaderNeeds()
        {
            var keys = new List<string>();
            foreach (var (k, v) in DeviceInfo.Rows("local")) { keys.Add(k); Assert.IsNotEmpty(v, k); }
            foreach (var want in new[] { "Build", "Device", "GPU", "Memory", "Screen" }) CollectionAssert.Contains(keys, want);
        }
    }
}
