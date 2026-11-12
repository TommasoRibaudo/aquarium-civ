using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #573: chopping reads as wood. Every axe hit at the woods throws chips (HitFx); every <c>perLog</c> hits make one log lie on the
    /// ground by the chopper; when the chopper leaves with the load (the planner's Chop work carries <see cref="Prop.Log"/> to the stash)
    /// the logs on the ground go with them, and the stash pile (StashProps) grows with the stock. Pure counting; the view
    /// (ChopLogsView) draws it. Nothing here touches the sim and nothing is felled: the tree stays (#366 stays open), the logs come from it.
    /// </summary>
    public sealed class ChopLogTally
    {
        /// <summary>Most logs lying by one chopper (the rest of the work is carried in turns).</summary>
        public const int MaxPerChopper = 4;
        readonly Dictionary<int, int> hits = new(), logs = new();

        public int Logs(int who) => logs.TryGetValue(who, out int n) ? n : 0;
        public int Hits(int who) => hits.TryGetValue(who, out int n) ? n : 0;
        public int TotalLogs { get { int t = 0; foreach (var n in logs.Values) t += n; return t; } }

        /// <summary>One axe hit by <paramref name="who"/>; true when it made a log (every <paramref name="perLog"/>th hit, while there is room).</summary>
        public bool Hit(int who, int perLog)
        {
            if (perLog < 1) perLog = 1;
            int h = Hits(who) + 1;
            if (h < perLog) { hits[who] = h; return false; }
            hits[who] = 0;
            if (Logs(who) >= MaxPerChopper) return false;
            logs[who] = Logs(who) + 1;
            return true;
        }

        /// <summary>The chopper picked the logs up and left: how many they carried off.</summary>
        public int Leave(int who)
        {
            int n = Logs(who);
            logs.Remove(who); hits.Remove(who);
            return n;
        }

        public void Clear() { hits.Clear(); logs.Clear(); }
    }
}
