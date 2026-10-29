using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Sends playtest feedback and survey answers as GitHub issues in the game's repo. Issues labelled "feedback" are
    /// auto-added to the "Aquarium Civ Playtest Feedback" project (built-in project workflow), so the app only needs
    /// a fine-grained token limited to this repo's issues. CI bakes it into Resources/FeedbackToken.txt (gitignored).
    /// Without a token, or offline, reports wait in persistentDataPath/feedback_outbox and are retried on launch.
    /// Issue format (labels + body) is documented in docs/feedback.md.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public class FeedbackSender : MonoBehaviour
    {
        public enum Kind { Bug, Feel, Confusing, Idea, Praise, Survey }

        public string repo = "TommasoRibaudo/aquarium-civ";

        public static FeedbackSender Instance { get; private set; }
        /// <summary>Raised after each attempt: (sent now, message for the player).</summary>
        public event Action<bool, string> Result;

        string token, buildInfo;
        string OutboxDir => Path.Combine(Application.persistentDataPath, "feedback_outbox");

        void Awake()
        {
            Instance = this;
            token = Resources.Load<TextAsset>("FeedbackToken")?.text.Trim() ?? "";
            buildInfo = Resources.Load<TextAsset>("BuildInfo")?.text.Trim() ?? "local";
#if UNITY_EDITOR
            // Editor testing: a token in UserSettings/ (gitignored, never built into the app).
            var local = Path.Combine(Application.dataPath, "..", "UserSettings", "FeedbackToken.txt");
            if (token.Length == 0 && File.Exists(local)) token = File.ReadAllText(local).Trim();
#endif
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3f); // let the session start first
            yield return FlushOutbox();
        }

        public static string KindLabel(Kind k) => k switch
        {
            Kind.Bug => "Something broke", Kind.Feel => "Look / sound / feel", Kind.Confusing => "Confusing",
            Kind.Idea => "Idea", Kind.Praise => "Loved it", _ => "Survey",
        };

        // ---------- reports ----------

        /// <summary>Free-form feedback from the in-game Feedback button.</summary>
        public void SendFeedback(Kind kind, string text)
        {
            var first = text.Trim().Split('\n')[0];
            if (first.Length > 60) first = first.Substring(0, 57) + "...";
            var body = new StringBuilder();
            body.Append("### What the tester said\n\n").Append(Quote(text)).Append("\n\n");
            AppendContext(body, kind, EventLog.Instance != null ? EventLog.Instance.CurrentSummary : "{}");
            Post($"{Prefix()} {kind}: {first}", body.ToString(), kind);
        }

        /// <summary>Post-session survey (prototype-plan.md §4), sent automatically on "Send".</summary>
        public void SendSurvey(IReadOnlyList<(string key, string question, string answer)> answers, string sessionSummary)
        {
            var body = new StringBuilder("### Survey answers\n\n| # | Question | Answer |\n|---|---|---|\n");
            string q1 = "-", q2 = "-", q4 = "-";
            for (int i = 0; i < answers.Count; i++)
            {
                var (key, question, answer) = answers[i];
                body.Append($"| {key} | {Cell(question)} | {Cell(answer)} |\n");
                if (key == "q1_feel") q1 = answer;
                if (key == "q2_return") q2 = answer;
                if (key == "q4") q4 = answer;
            }
            body.Append('\n');
            AppendContext(body, Kind.Survey, sessionSummary);
            Post($"{Prefix()} Survey from {Nick()}: feel {q1} · return {q2} · Q4 {q4}", body.ToString(), Kind.Survey);
        }

        // ---------- formatting ----------

        static string Prefix()
        {
            var cfg = PrototypeConfig.Current;
            return cfg != null ? $"[{cfg.protoId}·{cfg.Variant}]" : "[-]";
        }

        static string Nick() => Tester.Nickname.Length > 0 ? Tester.Nickname : Tester.Id.Substring(0, 8);

        void AppendContext(StringBuilder b, Kind kind, string summaryJson)
        {
            var cfg = PrototypeConfig.Current;
            var clock = SimClock.Instance;
            var cam = OrbitCamera.Instance;
            b.Append("### Context\n\n| | |\n|---|---|\n");
            b.Append($"| Kind | {KindLabel(kind)} |\n");
            b.Append($"| Tester | {Cell(Nick())} (`{Tester.Id.Substring(0, 8)}`) |\n");
            if (cfg != null) b.Append($"| Prototype / variant | {cfg.protoId} / {cfg.Variant} ({cfg.testMode}) |\n");
            b.Append($"| Build | {Application.version} · {buildInfo} · {Application.platform} |\n");
            b.Append($"| Device | {Cell(SystemInfo.deviceModel)} · {Cell(SystemInfo.operatingSystem)} · {Screen.width}x{Screen.height} |\n");
            if (EventLog.Instance != null) b.Append($"| Session | `{EventLog.Instance.SessionId}` |\n");
            if (clock != null)
            {
                var sim = clock.Sim;
                b.Append(string.Format(CultureInfo.InvariantCulture, "| World | year {0:0.0}, pop {1}, mess {2:0.00} (glass {3:0.00}, weeds {4:0.00}, trash {5}) |\n",
                    sim.State.Year, sim.State.population, sim.MessLevel, sim.State.grime, sim.State.overgrowth, sim.State.trash.Count));
            }
            if (cam != null) b.Append(string.Format(CultureInfo.InvariantCulture, "| Camera | zoom {0:0.00} |\n", cam.ZoomT));
            b.Append("\n### Session stats\n\n```json\n").Append(summaryJson).Append("\n```\n");
            if (EventLog.Instance != null)
            {
                b.Append("\n<details><summary>Recent log events</summary>\n\n```\n");
                foreach (var line in EventLog.Instance.RecentLines) b.Append(line.TrimEnd('\n')).Append('\n');
                b.Append("```\n</details>\n");
            }
            // Machine-readable footer for agents triaging the board.
            b.Append($"\n<!-- aquarium-civ-feedback v1 kind={kind.ToString().ToLowerInvariant()} proto={cfg?.protoId} variant={cfg?.Variant} tester={Tester.Id.Substring(0, 8)} build={Application.version}/{buildInfo} -->\n");
        }

        static string Quote(string text)
        {
            var sb = new StringBuilder();
            foreach (var line in text.Trim().Split('\n')) sb.Append("> ").Append(line.TrimEnd('\r')).Append('\n');
            return sb.ToString();
        }

        static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\r", "").Replace("\n", " ");

        // ---------- transport ----------

        void Post(string title, string body, Kind kind)
        {
            var cfg = PrototypeConfig.Current;
            var labels = new List<string> { "feedback", "fb:" + kind.ToString().ToLowerInvariant() };
            if (cfg != null) { labels.Add("proto:" + cfg.protoId); labels.Add("variant:" + cfg.Variant); }
            var json = new StringBuilder("{");
            Json.Field(json, "title", title); json.Append(',');
            Json.Field(json, "body", body); json.Append(",\"labels\":[");
            for (int i = 0; i < labels.Count; i++) { if (i > 0) json.Append(','); Json.String(json, labels[i]); }
            json.Append("]}");
            EventLog.Instance?.Log("feedback", ("kind", kind.ToString()), ("chars", body.Length));
            StartCoroutine(SendOrQueue(json.ToString(), true));
        }

        IEnumerator SendOrQueue(string payload, bool report)
        {
            bool ok = false;
            if (token.Length > 0)
            {
                using var req = new UnityWebRequest($"https://api.github.com/repos/{repo}/issues", "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = 20,
                };
                req.SetRequestHeader("Authorization", "Bearer " + token);
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                ok = req.result == UnityWebRequest.Result.Success;
                if (!ok) Debug.LogWarning($"[Feedback] send failed: {req.responseCode} {req.error}");
            }
            if (!ok) Queue(payload);
            if (report) Result?.Invoke(ok, ok ? "Thanks! Sent to the team." : "Thanks! Saved; it will send next time you're online.");
        }

        void Queue(string payload)
        {
            try
            {
                Directory.CreateDirectory(OutboxDir);
                File.WriteAllText(Path.Combine(OutboxDir, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}.json"), payload);
            }
            catch (Exception e) { Debug.LogWarning($"[Feedback] could not save to outbox: {e.Message}"); }
        }

        IEnumerator FlushOutbox()
        {
            if (token.Length == 0 || !Directory.Exists(OutboxDir)) yield break;
            foreach (var file in Directory.GetFiles(OutboxDir, "*.json"))
            {
                string payload;
                try { payload = File.ReadAllText(file); File.Delete(file); }
                catch { continue; }
                yield return SendOrQueue(payload, false); // re-queued under a new name if it fails again
            }
        }
    }
}
