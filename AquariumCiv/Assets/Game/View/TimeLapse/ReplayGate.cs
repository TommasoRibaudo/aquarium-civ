using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #669: the one place that says what the History replay may draw. While a replay is on, <see cref="Month"/> is the month it shows
    /// and a view that draws something placed or built by itself asks <see cref="Shows"/> with that thing's month: a placing month
    /// (<c>placedMonth</c>, <c>sinceMonth</c>), a building's founding, or <c>SimCore.ThingSince</c> for what has no record of its own.
    /// A thing with no record (-1) is hidden in a replay, except at the end of the timeline, which is the box as it stands now.
    /// Living animals and berries have no per-year record of where they stood, so <see cref="TimeLapseView"/> turns their views off.
    /// </summary>
    public static class ReplayGate
    {
        public static bool Active { get; private set; }
        /// <summary>The sim month the replay shows (floor of the playhead).</summary>
        public static long Month { get; private set; }
        /// <summary>The playhead is at the end: the box as it is now.</summary>
        public static bool AtPresent { get; private set; }

        public static void Set(long month, bool atPresent) { Active = true; Month = month; AtPresent = atPresent; }
        public static void Clear() { Active = false; Month = 0; AtPresent = false; }

        /// <summary>True when a thing placed or built in <paramref name="month"/> (-1 = no record) is drawn now.</summary>
        public static bool Shows(long month) => !Active || AtPresent || (month >= 0 && month <= Month);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Clear();
    }
}
