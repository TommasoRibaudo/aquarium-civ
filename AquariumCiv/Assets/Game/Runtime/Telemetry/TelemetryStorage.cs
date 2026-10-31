using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AquariumCiv.Game
{
    /// <summary>File access for the event log, behind an interface so the log is testable and a failing disk never reaches the game.</summary>
    public interface ITelemetryStorage
    {
        /// <summary>File names (not paths) that start with <paramref name="prefix"/>.</summary>
        string[] List(string prefix);
        long Length(string name);                 // 0 when missing
        bool EndsWithNewline(string name);        // true when missing or empty
        void Append(string name, string text);    // creates the file; must reach the disk before returning
        string ReadAll(string name);              // "" when missing
        void Delete(string name);
        void Move(string from, string to);        // replaces the target
    }

    /// <summary>The real thing: a folder (phase 2: persistentDataPath/telemetry).</summary>
    public class FileTelemetryStorage : ITelemetryStorage
    {
        readonly string dir;
        public FileTelemetryStorage(string directory) { dir = directory; Directory.CreateDirectory(dir); }
        string P(string n) => Path.Combine(dir, n);

        public string[] List(string prefix)
        {
            var r = new List<string>();
            foreach (var f in Directory.GetFiles(dir)) { var n = Path.GetFileName(f); if (n.StartsWith(prefix, StringComparison.Ordinal)) r.Add(n); }
            r.Sort(StringComparer.Ordinal);
            return r.ToArray();
        }

        public long Length(string name) => File.Exists(P(name)) ? new FileInfo(P(name)).Length : 0;

        public bool EndsWithNewline(string name)
        {
            if (Length(name) == 0) return true;
            using (var fs = new FileStream(P(name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(-1, SeekOrigin.End);
                return fs.ReadByte() == '\n';
            }
        }

        public void Append(string name, string text)
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var fs = new FileStream(P(name), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // to the disk, so a killed app keeps everything written so far
            }
        }

        public string ReadAll(string name)
        {
            if (!File.Exists(P(name))) return "";
            using (var fs = new FileStream(P(name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
                return sr.ReadToEnd();
        }

        public void Delete(string name) { if (File.Exists(P(name))) File.Delete(P(name)); }

        public void Move(string from, string to)
        {
            if (!File.Exists(P(from))) return;
            if (File.Exists(P(to))) File.Delete(P(to));
            File.Move(P(from), P(to));
        }
    }

    /// <summary>In-memory storage for tests (and for a build with logging off).</summary>
    public class MemoryTelemetryStorage : ITelemetryStorage
    {
        public readonly SortedDictionary<string, string> files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Set to make every write throw (full disk, revoked permission).</summary>
        public bool failWrites;

        public string[] List(string prefix)
        {
            var r = new List<string>();
            foreach (var k in files.Keys) if (k.StartsWith(prefix, StringComparison.Ordinal)) r.Add(k);
            return r.ToArray();
        }
        public long Length(string name) => files.TryGetValue(name, out var s) ? Encoding.UTF8.GetByteCount(s) : 0;
        public bool EndsWithNewline(string name) => !files.TryGetValue(name, out var s) || s.Length == 0 || s[s.Length - 1] == '\n';
        public void Append(string name, string text)
        {
            if (failWrites) throw new IOException("disk full");
            files[name] = (files.TryGetValue(name, out var s) ? s : "") + text;
        }
        public string ReadAll(string name) => files.TryGetValue(name, out var s) ? s : "";
        public void Delete(string name) => files.Remove(name);
        public void Move(string from, string to) { if (files.TryGetValue(from, out var s)) { files[to] = s; files.Remove(from); } }
    }
}
