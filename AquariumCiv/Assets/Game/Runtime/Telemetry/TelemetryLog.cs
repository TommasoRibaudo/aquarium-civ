using System;
using System.Collections.Generic;
using System.Globalization;

namespace AquariumCiv.Game
{
    public class TelemetryOptions
    {
        /// <summary>A file is rotated when it would grow past this.</summary>
        public long maxFileBytes = 256 * 1024;
        /// <summary>Rotated files kept per box (box-X.1.jsonl is the newest). Disk cap per box = maxFileBytes * (1 + keepRotated).</summary>
        public int keepRotated = 3;
        /// <summary>Flush when this many events are waiting ...</summary>
        public int flushEvery = 16;
        /// <summary>... or when the oldest has waited this long (seconds; <see cref="TelemetryLog.Tick"/> checks).</summary>
        public double flushAfterSeconds = 10;
        /// <summary>Events held in memory while the disk fails; the oldest are dropped past this.</summary>
        public int maxPending = 500;
        /// <summary>Back from the background after at least this long counts as a new open (a session).</summary>
        public double newSessionAfterAwaySeconds = 300;
    }

    /// <summary>
    /// The slice's JSONL event log (#81): one file per box ("box-&lt;id&gt;.jsonl", rotated and size-capped), appended in small
    /// batches, flushed on pause/quit, tolerant of a kill mid-write. Never throws into the game: a failing disk just keeps events
    /// in memory (bounded) and counts the errors. Pure C#; all time comes from the injected clock.
    /// </summary>
    public class TelemetryLog
    {
        public const string InstallFile = "install.txt";
        readonly ITelemetryStorage storage;
        readonly Func<DateTime> now;
        readonly TelemetryOptions opt;
        readonly List<string> pending = new List<string>();
        DateTime oldestPending;
        bool fileChecked;

        public string BoxId { get; private set; }
        public string SessionId { get; private set; }
        /// <summary>When this install first ran (read from <see cref="InstallFile"/>, written on first use).</summary>
        public DateTime InstallUtc { get; private set; }
        public int WriteErrors { get; private set; }
        public int Dropped { get; private set; }
        public int PendingCount => pending.Count;

        public TelemetryLog(ITelemetryStorage storage, Func<DateTime> utcNow, string boxId, TelemetryOptions options = null)
        {
            this.storage = storage; now = utcNow; opt = options ?? new TelemetryOptions();
            BoxId = Safe(boxId);
            SessionId = NewId();
            InstallUtc = LoadInstall();
        }

        static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>Box ids go into file names: keep letters, digits, - and _ only.</summary>
        static string Safe(string id)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in id ?? "") if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
            return sb.Length == 0 ? "main" : sb.ToString();
        }

        string FileName => $"box-{BoxId}.jsonl";
        string Rotated(int n) => $"box-{BoxId}.{n}.jsonl";

        DateTime LoadInstall()
        {
            try
            {
                var s = storage.ReadAll(InstallFile).Trim();
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)) return t;
                var n = now().ToUniversalTime();
                storage.Append(InstallFile, n.ToString("o", CultureInfo.InvariantCulture) + "\n");
                return n;
            }
            catch (Exception) { WriteErrors++; return now().ToUniversalTime(); }
        }

        // ---- sessions -------------------------------------------------------------------------------------------

        /// <summary>The app opened or came back after a real absence: a new session id and a session_start line (flushed at once).</summary>
        public void StartSession(string reason, double awayHours, string build, string platform, int tzMinutes)
        {
            SessionId = NewId();
            Log(Ev.SessionStart, "reason", reason, "build", build, "platform", platform, "tz_min", tzMinutes,
                "away_h", awayHours, "install_utc", InstallUtc.ToString("o", CultureInfo.InvariantCulture));
            Flush();
        }

        /// <summary>Back from the background (GameClock.Return): a new session only after a real absence.</summary>
        public bool ShouldStartNewSession(double awaySeconds) => awaySeconds >= opt.newSessionAfterAwaySeconds;

        /// <summary>App paused or quitting (OnApplicationPause(true) / OnApplicationQuit): write what we have and close the session line.</summary>
        public void EndSession(string reason, double sessionSeconds)
        {
            Log(Ev.SessionEnd, "reason", reason, "secs", sessionSeconds);
            Flush();
        }

        /// <summary>The shelf switched boxes (#87): what is waiting goes to the old box's file, new events to the new one.</summary>
        public void SwitchBox(string boxId)
        {
            Flush();
            BoxId = Safe(boxId);
            fileChecked = false;
        }

        // ---- writing --------------------------------------------------------------------------------------------

        /// <summary>Adds an event; <paramref name="kv"/> is key, value, key, value ... (null values are skipped).</summary>
        public void Log(string name, params object[] kv)
        {
            var ev = new TelemetryEvent { utc = now().ToUniversalTime(), session = SessionId, name = name };
            for (int i = 0; kv != null && i + 1 < kv.Length; i += 2) ev.Set(Convert.ToString(kv[i], CultureInfo.InvariantCulture), kv[i + 1]);
            if (pending.Count == 0) oldestPending = ev.utc;
            pending.Add(ev.ToLine());
            if (pending.Count > opt.maxPending) { pending.RemoveAt(0); Dropped++; }
            if (pending.Count >= opt.flushEvery) Flush();
        }

        /// <summary>Call every frame or so; flushes when events have waited too long.</summary>
        public void Tick()
        {
            if (pending.Count > 0 && (now().ToUniversalTime() - oldestPending).TotalSeconds >= opt.flushAfterSeconds) Flush();
        }

        /// <summary>Writes the waiting events as whole lines. Returns false (and keeps them) when the disk failed.</summary>
        public bool Flush()
        {
            if (pending.Count == 0) return true;
            try
            {
                var sb = new System.Text.StringBuilder();
                // a kill mid-write can leave a torn last line: start a fresh line so only that one line is lost
                if (!fileChecked) { if (!storage.EndsWithNewline(FileName)) sb.Append('\n'); fileChecked = true; }
                foreach (var l in pending) sb.Append(l).Append('\n');
                string text = sb.ToString();
                if (storage.Length(FileName) + text.Length > opt.maxFileBytes && storage.Length(FileName) > 0) Rotate();
                storage.Append(FileName, text);
                pending.Clear();
                return true;
            }
            catch (Exception)
            {
                WriteErrors++;
                fileChecked = false;
                return false;
            }
        }

        void Rotate()
        {
            if (opt.keepRotated <= 0) { storage.Delete(FileName); return; }
            storage.Delete(Rotated(opt.keepRotated));
            for (int n = opt.keepRotated - 1; n >= 1; n--) storage.Move(Rotated(n), Rotated(n + 1));
            storage.Move(FileName, Rotated(1));
        }

        // ---- reading --------------------------------------------------------------------------------------------

        /// <summary>Every line of every box's files, oldest file first (for the analyzer, feedback reports and the debug panel).</summary>
        public List<string> ReadAllLines()
        {
            Flush();
            var lines = new List<string>();
            var names = new List<string>(storage.List("box-"));
            // rotated files hold older events: box-X.3 before box-X.2 before box-X.1 before box-X
            names.Sort((a, b) => string.CompareOrdinal(SortKey(a), SortKey(b)));
            foreach (var n in names)
                foreach (var l in storage.ReadAll(n).Split('\n'))
                    if (l.Length > 0) lines.Add(l);
            return lines;
        }

        static string SortKey(string name)
        {
            // "box-ID.N.jsonl" -> "ID" + (9 - N); "box-ID.jsonl" -> "ID" + "9"
            string core = name.Substring(4, name.Length - 4 - ".jsonl".Length);
            int dot = core.LastIndexOf('.');
            if (dot > 0 && int.TryParse(core.Substring(dot + 1), out var n)) return core.Substring(0, dot) + "|" + (9 - Math.Min(n, 8));
            return core + "|9";
        }

        /// <summary>The last <paramref name="count"/> lines (for a feedback report).</summary>
        public List<string> RecentLines(int count)
        {
            var all = ReadAllLines();
            return all.Count <= count ? all : all.GetRange(all.Count - count, count);
        }
    }
}
