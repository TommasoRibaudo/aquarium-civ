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

        // ---------------------------------------------------------------- #619: long notes
        static string BuildFor(FeedbackReport.Fit f, List<string> log, string stats, List<string> events) =>
            FeedbackReport.Body(FeedbackKind.Idea, f.text, f.screenshot, "ab12cd34", Ctx, log.GetRange(log.Count - f.logLines, f.logLines), f.stats ? stats : null, f.events ? events : null);

        static List<string> Lines(int n, int width) { var l = new List<string>(); for (int i = 0; i < n; i++) l.Add(i + " " + new string('l', width)); return l; }

        [Test]
        public void ALong3000CharNote_ArrivesWhole_WithItsScreenshot()
        {
            string text = string.Join(" ", System.Linq.Enumerable.Repeat("winter freezes the pond and", 112)); // ~3,000 chars, as a dictation
            Assert.That(text.Length, Is.InRange(2900, FeedbackReport.MaxTextChars));
            var log = Lines(FeedbackReport.MaxLogLines, 200); var events = Lines(40, 150); string stats = "{" + new string('s', 1500) + "}";
            var f = FeedbackReport.FitNote(text, log.Count, true, 40000, x => BuildFor(x, log, stats, events));
            Assert.AreEqual(text, f.text, "the words arrive whole");
            Assert.IsFalse(f.textCut);
            Assert.IsTrue(f.screenshot, "the picture stays");
            Assert.That(f.screenshotRoom, Is.GreaterThanOrEqualTo(FeedbackReport.MinScreenshotChars));
            Assert.That(BuildFor(f, log, stats, events).Length + f.screenshotRoom + FeedbackReport.ScreenshotWrapChars, Is.LessThanOrEqualTo(FeedbackReport.MaxIssueChars));
            Assert.That(f.logLines, Is.LessThan(log.Count), "the log gave way first");
        }

        [Test]
        public void TheLogGivesWayBeforeTheEventsTheStatsAndThePicture()
        {
            var log = Lines(150, 300); var events = Lines(40, 600); string stats = "{" + new string('s', 20000) + "}";
            var f = FeedbackReport.FitNote("short", log.Count, true, 40000, x => BuildFor(x, log, stats, events));
            Assert.AreEqual(0, f.logLines, "no log left");
            Assert.IsTrue(f.screenshot);
            Assert.AreEqual("short", f.text);
        }

        [Test]
        public void OnlyAsALastResort_TheTextIsCut_WithAVisibleMark()
        {
            var log = Lines(10, 10); var events = Lines(2, 10);
            string huge = new string('w', FeedbackReport.MaxTextChars);
            // a context so long nothing else can fit: the words are cut, never silently
            var big = new List<(string, string)> { ("Box", new string('b', FeedbackReport.MaxIssueChars - 2000)) };
            string Build(FeedbackReport.Fit x) => FeedbackReport.Body(FeedbackKind.Idea, x.text, x.screenshot, "t", big, log.GetRange(log.Count - x.logLines, x.logLines), x.stats ? "{}" : null, x.events ? events : null);
            var f = FeedbackReport.FitNote(huge, log.Count, true, 40000, Build);
            Assert.IsTrue(f.textCut);
            Assert.IsFalse(f.screenshot, "the picture went before the words");
            StringAssert.EndsWith("characters)", f.text);
            Assert.That(Build(f).Length, Is.LessThanOrEqualTo(FeedbackReport.MaxIssueChars));
        }

        [Test]
        public void TheCounterShowsOnlyNearTheCap()
        {
            Assert.AreEqual("", FeedbackReport.CounterText(100));
            Assert.AreEqual("", FeedbackReport.CounterText(FeedbackReport.CounterFrom - 1));
            Assert.AreEqual((FeedbackReport.MaxTextChars - FeedbackReport.CounterFrom) + " left", FeedbackReport.CounterText(FeedbackReport.CounterFrom));
            Assert.AreEqual("0 left", FeedbackReport.CounterText(FeedbackReport.MaxTextChars));
            Assert.That(FeedbackReport.MaxTextChars, Is.GreaterThanOrEqualTo(3000), "#619: a dictated note fits");
        }
    }
}
