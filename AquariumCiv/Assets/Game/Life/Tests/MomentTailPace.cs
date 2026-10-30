using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;

namespace AquariumCiv.Life.Tests
{
    /// <summary>Samples a staged moment (a relic moment, a shaping answer, an echo) from its start until everyone has fully
    /// rejoined their day, and reports the fastest walk that is not the plain day's own (those are the NoWalkRushes_* sweeps').
    /// The tail (the walk back into the day) can run well past the moment's end, also past 20h when view hours run faster.</summary>
    public static class MomentTailPace
    {
        /// <summary>View hours after the moment's end to keep sampling: long enough for the longest walk back.</summary>
        public const float Rejoin = 2f;

        public static (float worst, string what, int steps) Worst(LifePlanner planner, long day, float from, float end, string tag)
        {
            float worst = 0f;
            string what = "";
            int steps = 0;
            var who = planner.People.ToList();
            var plain = new Dictionary<int, HashSet<(float, float)>>();
            var daySegs = new List<PersonActivity>();
            foreach (var p in who)
            {
                planner.DaySchedule(p, day, daySegs);
                plain[p.Key] = new HashSet<(float, float)>(daySegs.Select(a => (a.start, a.end)));
            }
            var last = new Dictionary<int, (float x, float z, float h)>();
            for (float h = from; h < Math.Min(24f, end + Rejoin); h += 0.004f)
                foreach (var p in who)
                {
                    if (!planner.PositionAt(p, new ViewTime(day, h), out float x, out float z)) continue;
                    if (last.TryGetValue(p.Key, out var l))
                    {
                        float d = (float)Math.Sqrt((x - l.x) * (x - l.x) + (z - l.z) * (z - l.z));
                        float secs = LifeClock.RealSecondsBetween(l.h, h);
                        var now = planner.Plan(p, new ViewTime(day, h));
                        if (d > 1e-4f && secs > 0f && !plain[p.Key].Contains((now.start, now.end)))
                        {
                            steps++;
                            float pace = d / secs / LifePlanner.RealWalkSpeed;
                            if (pace > worst) { worst = pace; what = $"{tag} {p} at {h:0.000}h (moment {from:0.00}-{end:0.00}): {now.kind} {now.from}->{now.place} {now.start:0.000}-{now.end:0.000} {now.source}"; }
                        }
                    }
                    last[p.Key] = (x, z, h);
                }
            return (worst, what, steps);
        }
    }
}
