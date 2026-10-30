using System.Collections.Generic;
using System.Text;

namespace AquariumCiv.GameUI
{
    public enum FeedbackKind { Bug, Confusing, Idea, Feel, Praise }

    /// <summary>
    /// What a feedback note becomes on GitHub: title, labels, body, JSON payload. Pure text, so it is unit-tested. Same
    /// labels and layout as the prototypes' issues (docs/feedback.md): `feedback`, `fb:&lt;kind&gt;`, `proto:slice`.
    /// </summary>
    public static class FeedbackReport
    {
        /// <summary>In the body where the uploaded screenshot goes; the sender swaps it at send time (so a queued note can still upload later).</summary>
        public const string ScreenshotMarker = "@@SCREENSHOT@@";
        public const int MaxLogLines = 150;
        public const int MaxTextChars = 600;

        public static string KindLabel(FeedbackKind k) => k switch
        {
            FeedbackKind.Bug => "Bug", FeedbackKind.Confusing => "Confusing", FeedbackKind.Idea => "Idea",
            FeedbackKind.Feel => "Feel", _ => "Praise",
        };

        public static string[] Labels(FeedbackKind k) => new[] { "feedback", "fb:" + k.ToString().ToLowerInvariant(), "proto:slice" };

        public static string Title(FeedbackKind k, string text, string eraAndYear)
        {
            var first = (text ?? "").Trim().Split('\n')[0].Trim();
            if (first.Length > 60) first = first.Substring(0, 57) + "...";
            if (first.Length == 0) first = "(no text)";
            return $"[Slice] {KindLabel(k)}: {first}" + (eraAndYear.Length > 0 ? $" ({eraAndYear})" : "");
        }

        public static string Body(FeedbackKind k, string text, bool hasScreenshot, string nick,
            IReadOnlyList<(string key, string value)> context, IReadOnlyList<string> logLines,
            string telemetryJson = null, IReadOnlyList<string> telemetryLines = null)
        {
            var b = new StringBuilder("### What the tester said\n\n");
            foreach (var line in (text ?? "").Trim().Split('\n')) b.Append("> ").Append(line.TrimEnd('\r')).Append('\n');
            if (hasScreenshot) b.Append("\n### Screenshot\n\n").Append(ScreenshotMarker).Append('\n');
            b.Append("\n### Context\n\n| | |\n|---|---|\n");
            b.Append("| Kind | ").Append(KindLabel(k)).Append(" |\n");
            b.Append("| Tester | ").Append(Cell(nick)).Append(" |\n");
            foreach (var (key, value) in context) b.Append("| ").Append(Cell(key)).Append(" | ").Append(Cell(value)).Append(" |\n");
            if (logLines != null && logLines.Count > 0)
            {
                b.Append("\n<details><summary>Game log (last ").Append(logLines.Count).Append(" lines)</summary>\n\n```\n");
                foreach (var line in logLines) b.Append(line.Replace("```", "'''").TrimEnd('\n')).Append('\n');
                b.Append("```\n</details>\n");
            }
            if (!string.IsNullOrEmpty(telemetryJson))
                b.Append("\n### Session stats (event log summary)\n\n```json\n").Append(telemetryJson).Append("\n```\n");
            if (telemetryLines != null && telemetryLines.Count > 0)
            {
                b.Append("\n<details><summary>Recent events (last ").Append(telemetryLines.Count).Append(")</summary>\n\n```\n");
                foreach (var line in telemetryLines) b.Append(line.Replace("```", "'''").TrimEnd('\n')).Append('\n');
                b.Append("```\n</details>\n");
            }
            b.Append("\n<!-- aquarium-civ-feedback v1 kind=").Append(k.ToString().ToLowerInvariant()).Append(" proto=slice -->\n");
            return b.ToString();
        }

        /// <summary>The GitHub create-issue request body.</summary>
        public static string PayloadJson(string title, string body, IReadOnlyList<string> labels)
        {
            var j = new StringBuilder("{\"title\":\"").Append(Escape(title)).Append("\",\"body\":\"").Append(Escape(body)).Append("\",\"labels\":[");
            for (int i = 0; i < labels.Count; i++) j.Append(i > 0 ? "," : "").Append('"').Append(Escape(labels[i])).Append('"');
            return j.Append("]}").ToString();
        }

        /// <summary>JSON string contents (no surrounding quotes).</summary>
        public static string Escape(string s)
        {
            var b = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4"));
                        else b.Append(c);
                        break;
                }
            }
            return b.ToString();
        }

        static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\r", "").Replace("\n", " ");
    }
}
