using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AquariumCiv.Game
{
    /// <summary>
    /// The slice's event names (#81, #100). One JSON object per line:
    /// <c>{"t":"2026-10-30T09:00:00.000Z","s":"a1b2c3d4","e":"card_shown",...props}</c>.
    /// Privacy (docs/feedback.md): no nickname, device id, free text or location. Ids are random per install/session;
    /// people and cards are referred to by their sim ids only.
    /// </summary>
    public static class Ev
    {
        // sessions and time away
        public const string SessionStart = "session_start";   // reason(open|return), build, platform, tz_min, away_h, install_utc
        public const string SessionEnd = "session_end";       // reason(pause|quit), secs
        public const string CatchUp = "catch_up";             // away_h, lived_h, months, ms
        // play
        public const string FirstRunStep = "first_run_step";  // step (the first-run step now starting)
        public const string Clean = "clean";                  // kind (wipe|sweep|...), voluntary
        public const string EraMoment = "era_moment";         // era, mode(live|skipped), secs   (#88: watched live or hurried/skipped)
        // M4: chronicle cards (#75)
        public const string CardShown = "card_shown";         // card, kind, i, n
        public const string CardRead = "card_read";           // card, dwell_ms (paused while a person card / fly-to covers it)
        public const string CardOpened = "card_opened";       // card, person (person card opened from it)
        public const string CardFlownTo = "card_flown_to";    // card
        public const string ShowMe = "show_me";               // card
        public const string SkipAll = "skip_all";             // left (cards not yet read)
        public const string PeekTap = "peek_tap";             // card
        // M4: history (#79/#80)
        public const string HistoryOpen = "history_open";     // via (book|timelapse|shelf), prompted
        public const string TimelapseOpen = "timelapse_open"; // prompted
        public const string TimelapseScrub = "timelapse_scrub"; // from_year, to_year
        public const string TimelapsePlay = "timelapse_play";
        public const string PinTap = "pin_tap";               // person
        public const string Snapshot = "snapshot";
        public const string BookPage = "book_page";           // page
        public const string RememberTap = "remember_tap";     // person
        // #100: the day-3 recall question
        public const string RecallShown = "recall_shown";     // day
        public const string RecallAnswered = "recall_answered"; // count (stories recalled, a number; never free text)
        public const string RecallDismissed = "recall_dismissed";
    }

    /// <summary>One logged event: when, which session, what, plus flat properties (string / number / bool).</summary>
    public class TelemetryEvent
    {
        public DateTime utc;
        public string session;
        public string name;
        public readonly List<KeyValuePair<string, object>> props = new List<KeyValuePair<string, object>>();

        public TelemetryEvent Set(string key, object value)
        {
            if (value != null) props.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public bool TryGet(string key, out object v)
        {
            foreach (var p in props) if (p.Key == key) { v = p.Value; return true; }
            v = null; return false;
        }

        public string Str(string key, string def = null) => TryGet(key, out var v) && v is string s ? s : def;
        public double Num(string key, double def = 0) => TryGet(key, out var v) && v is double d ? d : def;
        public bool Flag(string key, bool def = false) => TryGet(key, out var v) && v is bool b ? b : def;

        const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public string ToLine()
        {
            var sb = new StringBuilder(96);
            sb.Append("{\"t\":\"").Append(utc.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture)).Append('"');
            sb.Append(",\"s\":"); Json.Str(sb, session ?? "");
            sb.Append(",\"e\":"); Json.Str(sb, name);
            foreach (var p in props)
            {
                sb.Append(','); Json.Str(sb, p.Key); sb.Append(':');
                switch (p.Value)
                {
                    case string s: Json.Str(sb, s); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case float f: Json.Num(sb, f); break;
                    case double d: Json.Num(sb, d); break;
                    case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                    case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                    default: Json.Str(sb, Convert.ToString(p.Value, CultureInfo.InvariantCulture)); break;
                }
            }
            return sb.Append('}').ToString();
        }

        /// <summary>Parses one line; null when it is torn, empty or not an event (a crash can cut the last line).</summary>
        public static TelemetryEvent Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !Json.TryParseFlat(line, out var map)) return null;
            if (!map.TryGetValue("e", out var e) || !(e is string name) || name.Length == 0) return null;
            if (!map.TryGetValue("t", out var t) || !(t is string ts) ||
                !DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)) return null;
            var ev = new TelemetryEvent { utc = utc, name = name, session = map.TryGetValue("s", out var s) ? s as string : "" };
            foreach (var kv in map) if (kv.Key != "t" && kv.Key != "s" && kv.Key != "e") ev.props.Add(kv);
            return ev;
        }
    }

    /// <summary>A tiny JSON writer/reader for flat objects: enough for the log, no dependency on Unity's JsonUtility.</summary>
    static class Json
    {
        public static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        public static void Num(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) d = 0;
            sb.Append(Math.Round(d, 3).ToString("0.###", CultureInfo.InvariantCulture));
        }

        public static bool TryParseFlat(string s, out Dictionary<string, object> map)
        {
            map = new Dictionary<string, object>();
            int i = 0;
            Ws(s, ref i);
            if (i >= s.Length || s[i++] != '{') return false;
            Ws(s, ref i);
            if (i < s.Length && s[i] == '}') return true;
            while (true)
            {
                Ws(s, ref i);
                if (!ReadStr(s, ref i, out var key)) return false;
                Ws(s, ref i);
                if (i >= s.Length || s[i++] != ':') return false;
                Ws(s, ref i);
                if (i >= s.Length) return false;
                object val;
                char c = s[i];
                if (c == '"') { if (!ReadStr(s, ref i, out var str)) return false; val = str; }
                else if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { val = true; i += 4; }
                else if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { val = false; i += 5; }
                else if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { val = null; i += 4; }
                else
                {
                    int st = i;
                    while (i < s.Length && "+-.eE0123456789".IndexOf(s[i]) >= 0) i++;
                    if (i == st || !double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return false;
                    val = d;
                }
                if (val != null) map[key] = val;
                Ws(s, ref i);
                if (i >= s.Length) return false;
                if (s[i] == ',') { i++; continue; }
                return s[i] == '}';
            }
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static bool ReadStr(string s, ref int i, out string result)
        {
            result = null;
            if (i >= s.Length || s[i] != '"') return false;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') { result = sb.ToString(); return true; }
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) return false;
                char n = s[i++];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp)) return false;
                        sb.Append((char)cp); i += 4; break;
                    default: sb.Append(n); break;
                }
            }
            return false;
        }
    }
}
