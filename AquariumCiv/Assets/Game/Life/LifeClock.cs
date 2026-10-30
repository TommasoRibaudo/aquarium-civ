using System;

namespace AquariumCiv.Life
{
    /// <summary>A moment on the view's day clock: one view day per game month (decisions 2026-10-29, Choice 1), so
    /// <see cref="day"/> is the sim month and <see cref="hour"/> runs 0–24.</summary>
    public readonly struct ViewTime
    {
        public readonly long day;
        public readonly float hour;

        public ViewTime(long day, float hour)
        {
            // Normalise so callers can add hours freely.
            while (hour >= 24f) { hour -= 24f; day++; }
            while (hour < 0f) { hour += 24f; day--; }
            this.day = day;
            this.hour = hour;
        }

        public ViewTime AddHours(float h) => new ViewTime(day, hour + h);
        /// <summary>Hours since the start of day 0, for comparing times across days.</summary>
        public double Absolute => day * 24.0 + hour;
        public override string ToString() => $"day {day} {hour:0.00}h";
    }

    public enum DayPhase { Night, Dawn, Morning, Midday, Afternoon, Evening }

    /// <summary>
    /// The view day (life-in-the-box §3): view hours mapped onto ~300 real seconds unevenly, so night is short
    /// (decisions: "short night with flickering lamps"). Dawn 5–7 (20 s), morning work 7–12 (80 s), midday meal 12–13 (25 s),
    /// afternoon work 13–17 (80 s), evening 17–20 (50 s), night 20–5 (45 s). All [est], to tune in play.
    /// </summary>
    public static class LifeClock
    {
        public const float Dawn = 5f, WorkStart = 7f, Midday = 12f, AfternoonStart = 13f, Evening = 17f, Night = 20f;
        public const int RestDayEvery = 6; // one view day in 6 is a rest day [est]

        static readonly float[] PhaseStart = { 0f, Dawn, WorkStart, Midday, AfternoonStart, Evening, Night };
        static readonly float[] PhaseSeconds = { 0f, 20f, 80f, 25f, 80f, 50f, 0f };
        const float NightSeconds = 45f;

        public static float RealSecondsPerDay => 20f + 80f + 25f + 80f + 50f + NightSeconds;

        public static DayPhase PhaseOf(float hour)
        {
            if (hour < Dawn || hour >= Night) return DayPhase.Night;
            if (hour < WorkStart) return DayPhase.Dawn;
            if (hour < Midday) return DayPhase.Morning;
            if (hour < AfternoonStart) return DayPhase.Midday;
            if (hour < Evening) return DayPhase.Afternoon;
            return DayPhase.Evening;
        }

        public static bool IsWorkHour(float hour) => (hour >= WorkStart && hour < Midday) || (hour >= AfternoonStart && hour < Evening);

        /// <summary>Real seconds one view hour lasts at this hour.</summary>
        public static float RealSecondsPerViewHour(float hour)
        {
            switch (PhaseOf(hour))
            {
                case DayPhase.Night: return NightSeconds / 9f;
                case DayPhase.Dawn: return 20f / 2f;
                case DayPhase.Morning: return 80f / 5f;
                case DayPhase.Midday: return 25f;
                case DayPhase.Afternoon: return 80f / 4f;
                default: return 50f / 3f;
            }
        }

        /// <summary>Real seconds between two view hours of one day (<paramref name="h0"/> ≤ <paramref name="h1"/>), across
        /// the phases' different speeds.</summary>
        public static float RealSecondsBetween(float h0, float h1)
        {
            float s = 0f, h = Math.Max(0f, h0);
            h1 = Math.Min(24f, h1);
            int guard = 0;
            while (h < h1 - 1e-6f && guard++ < 16)
            {
                float next = Math.Min(h1, NextBoundary(h));
                s += (next - h) * RealSecondsPerViewHour(h);
                h = next;
            }
            return s;
        }

        /// <summary>View hours that <paramref name="seconds"/> real seconds cover from this hour (a reaction's length).</summary>
        public static float ViewHoursFor(float hour, float seconds) => seconds / RealSecondsPerViewHour(hour);

        /// <summary>
        /// The view day after a frame (#30). A view day and a sim month are both ~5 real minutes but out of step, so the
        /// month ticks in the middle of a view day; switching the day there swapped everyone's plan and the dolls jumped.
        /// The view keeps <paramref name="day"/> until the clock passes midnight (<paramref name="midnights"/> &gt; 0), then
        /// takes the sim's month; a sim further ahead (a catch-up, a skip) or behind (another box) is taken at once.
        /// </summary>
        public static long NextViewDay(long day, long midnights, long simMonth)
        {
            if (midnights > 0 || simMonth < day || simMonth > day + 1) return simMonth;
            return day;
        }

        /// <summary>Advance the view clock by real seconds (the view calls this every frame).</summary>
        public static ViewTime Advance(ViewTime t, float realSeconds)
        {
            long day = t.day;
            float hour = t.hour;
            int guard = 0;
            while (realSeconds > 0f && guard++ < 64)
            {
                float rate = RealSecondsPerViewHour(hour);
                float toNext = NextBoundary(hour) - hour;
                float need = toNext * rate;
                if (realSeconds < need) { hour += realSeconds / rate; break; }
                realSeconds -= need;
                hour += toNext;
                if (hour >= 24f) { hour -= 24f; day++; }
            }
            return new ViewTime(day, hour);
        }

        static float NextBoundary(float hour)
        {
            for (int i = 1; i < PhaseStart.Length; i++)
                if (hour < PhaseStart[i]) return PhaseStart[i];
            return 24f;
        }

        /// <summary>The sim's season is month % 12; winter is 11, 0, 1 (SimCore). Spring 2–4, summer 5–7, autumn 8–10.</summary>
        public static Season SeasonOf(long day)
        {
            int m = (int)(((day % 12) + 12) % 12);
            return m <= 1 || m == 11 ? Season.Winter : m <= 4 ? Season.Spring : m <= 7 ? Season.Summer : Season.Autumn;
        }

        public static bool IsRestDay(long day) => ((day % RestDayEvery) + RestDayEvery) % RestDayEvery == RestDayEvery - 1;
    }

    public enum Season { Spring, Summer, Autumn, Winter }
}
