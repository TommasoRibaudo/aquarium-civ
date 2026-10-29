using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Local playtest logging: one JSON object per line in persistentDataPath/logs, plus a compact per-session
    /// summary that testers can copy to the clipboard. Schema: docs/design/prototype-plan.md §3.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class EventLog : MonoBehaviour
    {
        public static EventLog Instance { get; private set; }

        [Tooltip("Seconds between 'what is the player looking at' samples.")]
        public float lookInterval = 0.5f;

        public string SessionId { get; private set; }
        public bool InSession { get; private set; }
        public string LastSummary { get; private set; } = "{}";
        /// <summary>The live session's summary so far (or the last one between sessions), for feedback reports.</summary>
        public string CurrentSummary => InSession ? BuildSummary(Time.realtimeSinceStartup - sessionStart) : LastSummary;
        /// <summary>The most recent log lines (oldest first), for feedback reports.</summary>
        public IEnumerable<string> RecentLines => recent;
        public string LogPath => Path.Combine(Application.persistentDataPath, "logs", $"{Tester.Id.Substring(0, 8)}_{DateTime.UtcNow:yyyyMMdd}.jsonl");

        // Session counters for the summary.
        float sessionStart;
        double awayHours;
        readonly Dictionary<string, float> taps = new();
        readonly Dictionary<string, float> look = new();
        readonly Dictionary<string, float> counters = new();
        readonly Dictionary<string, string> survey = new();
        readonly Queue<string> recent = new();
        const int RecentMax = 40;
        float lookTimer, zoomedInSeconds;

        void Awake() => Instance = this;

        void Update()
        {
            if (!InSession || (lookTimer += Time.unscaledDeltaTime) < lookInterval) return;
            lookTimer = 0;
            var cam = Camera.main;
            if (cam == null) return;
            string cat = "empty";
            if (Physics.Raycast(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f)), out var hit, 500f))
                cat = LogCategory.Of(hit.collider)?.category ?? "other";
            Add(look, cat, lookInterval);
            if (OrbitCamera.Instance != null && OrbitCamera.Instance.ZoomT < 0.5f) zoomedInSeconds += lookInterval;
        }

        public void StartSession(double hoursAway, float yearsElapsed, float messLevel)
        {
            if (InSession) EndSession("restart");
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            InSession = true;
            sessionStart = Time.realtimeSinceStartup;
            awayHours = hoursAway;
            taps.Clear(); look.Clear(); counters.Clear(); survey.Clear(); zoomedInSeconds = 0;
            Log("session_start", ("build", Application.version), ("platform", Application.platform.ToString()),
                ("away_h", hoursAway), ("years_elapsed", yearsElapsed), ("mess", messLevel), ("nick", Tester.Nickname));
        }

        public void EndSession(string reason)
        {
            if (!InSession) return;
            float duration = Time.realtimeSinceStartup - sessionStart;
            Log("look", ("seconds", look), ("zoomed_in_s", zoomedInSeconds));
            Log("session_end", ("duration_s", duration), ("reason", reason));
            LastSummary = BuildSummary(duration);
            InSession = false;
        }

        // ---------- typed helpers used by views ----------
        public void Tap(string category, string id = null)
        {
            Add(taps, category, 1);
            Log("tap", ("cat", category), ("id", id));
        }

        public void Clean(string kind, float amount, string gesture, float dirtBefore)
        {
            bool voluntary = dirtBefore < 0.1f;
            Add(counters, "cleans", 1);
            if (voluntary) Add(counters, "cleans_voluntary", 1);
            Log("clean", ("kind", kind), ("amount", amount), ("gesture", gesture), ("dirt_before", dirtBefore), ("voluntary", voluntary));
        }

        public void Count(string counter, float amount = 1) => Add(counters, counter, amount);

        public void Survey(IReadOnlyList<(string key, string answer)> answers)
        {
            var data = new List<(string, object)>();
            foreach (var (k, a) in answers) { survey[k] = a; data.Add((k, a)); }
            Log("survey", data.ToArray());
        }

        // ---------- core ----------
        public void Log(string ev, params (string key, object value)[] data)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            Json.Field(sb, "t", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)); sb.Append(',');
            Json.Field(sb, "tester", Tester.Id); sb.Append(',');
            Json.Field(sb, "session", SessionId); sb.Append(',');
            Json.Field(sb, "proto", PrototypeConfig.Current != null ? PrototypeConfig.Current.protoId : "-"); sb.Append(',');
            Json.Field(sb, "variant", PrototypeConfig.Current != null ? PrototypeConfig.Current.Variant : "-"); sb.Append(',');
            Json.Field(sb, "ev", ev); sb.Append(",\"data\":{");
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(',');
                Json.Field(sb, data[i].key, data[i].value);
            }
            sb.Append("}}");
            recent.Enqueue(sb.ToString());
            while (recent.Count > RecentMax) recent.Dequeue();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, sb.Append('\n').ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[EventLog] write failed: {e.Message}"); }
        }

        string BuildSummary(float duration)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            Json.Field(sb, "tester", Tester.Nickname.Length > 0 ? Tester.Nickname : Tester.Id.Substring(0, 8)); sb.Append(',');
            Json.Field(sb, "proto", PrototypeConfig.Current?.protoId); sb.Append(',');
            Json.Field(sb, "variant", PrototypeConfig.Current?.Variant); sb.Append(',');
            Json.Field(sb, "session", SessionId); sb.Append(',');
            Json.Field(sb, "date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)); sb.Append(',');
            Json.Field(sb, "duration_s", Mathf.Round(duration)); sb.Append(',');
            Json.Field(sb, "away_h", Math.Round(awayHours, 2)); sb.Append(',');
            Json.Field(sb, "taps", taps); sb.Append(',');
            Json.Field(sb, "look_s", look); sb.Append(',');
            Json.Field(sb, "counters", counters); sb.Append(',');
            Json.Field(sb, "survey", survey);
            sb.Append('}');
            return sb.ToString();
        }

        static void Add(Dictionary<string, float> d, string key, float v) => d[key] = d.TryGetValue(key, out var x) ? x + v : v;
    }

    /// <summary>Minimal JSON writer for flat log payloads (no dependency, IL2CPP-safe).</summary>
    public static class Json
    {
        public static void Field(StringBuilder sb, string key, object value)
        {
            String(sb, key);
            sb.Append(':');
            Value(sb, value);
        }

        public static void Value(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: String(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case float f: sb.Append(float.IsFinite(f) ? f.ToString("0.###", CultureInfo.InvariantCulture) : "null"); break;
                case double d: sb.Append(double.IsFinite(d) ? d.ToString("0.###", CultureInfo.InvariantCulture) : "null"); break;
                case int or long or uint or short: sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); break;
                case IDictionary<string, float> fd:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in fd) { if (!first) sb.Append(','); first = false; Field(sb, kv.Key, kv.Value); }
                    sb.Append('}');
                    break;
                case IDictionary<string, string> sd:
                    sb.Append('{');
                    bool f2 = true;
                    foreach (var kv in sd) { if (!f2) sb.Append(','); f2 = false; Field(sb, kv.Key, kv.Value); }
                    sb.Append('}');
                    break;
                default: String(sb, v.ToString()); break;
            }
        }

        public static void String(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
