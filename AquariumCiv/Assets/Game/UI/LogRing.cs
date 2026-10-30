using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The last log lines of the session (every Debug.Log, warning, error and exception, with a short stack for the bad
    /// ones), kept from before the first scene loads so a feedback note carries what the game said. Capped, so it costs
    /// nothing to keep.
    /// </summary>
    public static class LogRing
    {
        const int Capacity = 400;
        static readonly Queue<string> lines = new();
        static readonly object gate = new();
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Hook()
        {
            if (hooked) return;
            hooked = true;
            Application.logMessageReceivedThreaded += OnLog;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            // Time.realtimeSinceStartup is main-thread only; threaded logs get a wall-clock stamp instead.
            var line = $"{DateTime.Now:HH:mm:ss} [{Tag(type)}] {message}";
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !string.IsNullOrEmpty(stack))
                line += "\n" + Trim(stack, 6);
            Add(line);
        }

        static string Tag(LogType t) => t switch { LogType.Warning => "warn", LogType.Log => "info", _ => "ERROR" };

        public static string Trim(string stack, int maxLines)
        {
            var parts = stack.TrimEnd().Split('\n');
            return parts.Length <= maxLines ? stack.TrimEnd() : string.Join("\n", parts, 0, maxLines) + "\n  ...";
        }

        public static void Add(string line)
        {
            lock (gate)
            {
                lines.Enqueue(line);
                while (lines.Count > Capacity) lines.Dequeue();
            }
        }

        /// <summary>The newest <paramref name="max"/> entries, oldest first.</summary>
        public static List<string> Last(int max)
        {
            lock (gate)
            {
                var all = new List<string>(lines);
                return all.Count <= max ? all : all.GetRange(all.Count - max, max);
            }
        }

        public static void Clear() { lock (gate) lines.Clear(); }
    }
}
