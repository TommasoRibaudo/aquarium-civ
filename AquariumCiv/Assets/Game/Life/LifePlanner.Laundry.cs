using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>One line of washing at a home, for one chore of the day (B28, #705). Hours are view hours of <see cref="day"/>.</summary>
    public readonly struct LaundryLine
    {
        public readonly Place home;
        public readonly long day;
        /// <summary>The nth line at this home today (a second chore later in the day is its own line).</summary>
        public readonly int ordinal;
        public readonly float start, leave, takeAt, fadeAt;
        public readonly int cloths;
        /// <summary>Who takes the cloths in at <see cref="takeAt"/>: the doer or a housemate (their key), or <see cref="NoTaker"/> when nobody is free (they fade at <see cref="fadeAt"/>).</summary>
        public readonly int takerKey;
        /// <summary>The cheat's line: its stay is shown at 16 s real, whatever the hour.</summary>
        public readonly bool debug;

        public LaundryLine(Place home, long day, int ordinal, float start, float leave, float takeAt, float fadeAt, int cloths, int takerKey, bool debug)
        {
            this.home = home; this.day = day; this.ordinal = ordinal; this.start = start; this.leave = leave; this.takeAt = takeAt; this.fadeAt = fadeAt;
            this.cloths = cloths; this.takerKey = takerKey; this.debug = debug;
        }

        /// <summary>Crowd members have negative keys, so "nobody" is this, not -1.</summary>
        public const int NoTaker = int.MinValue;
        public bool Taken => takerKey != NoTaker;
        public float TakeEnd => takeAt + Laundry.TakeInHours;
    }

    /// <summary>#705: the line of washing. The day's plan hangs it (a <see cref="ActivityKind.HangWashing"/> chore at the home, now 0.7 view hours and only on
    /// a dry day outside winter); this finds the cloths' end: the doer or a housemate at the door takes them in (<see cref="ActivityKind.TakeInWashing"/>),
    /// else they fade. The view draws the line from <see cref="LaundryOn"/>.</summary>
    public sealed partial class LifePlanner
    {
        readonly List<LaundryLine> laundry = new List<LaundryLine>();
        long laundryDay = long.MinValue;
        int laundryPeople = -1;

        // The cheat's line: one doer, one chore from now, the stay at 16 s real, then the take-in ("Take in the washing" brings it forward).
        bool debugLaundryOn;
        int debugLaundryKey;
        long debugLaundryDay;
        float debugLaundryStart, debugLaundryLeave, debugLaundryTake;

        /// <summary>The day's lines of washing (planned chores and the cheat's), in start order. Safe to call every frame.</summary>
        public IReadOnlyList<LaundryLine> LaundryOn(long day)
        {
            Sync();
            EnsureDay(day);
            if (buildDepth > 0) return laundry; // asked from inside a day's build: leave the cache alone
            if (laundryDay == day && laundryPeople == people.Count) return laundry;
            laundryDay = day; laundryPeople = people.Count;
            laundry.Clear();
            var d = dayInfo;
            var raw = new List<(Place home, float start, float leave, int cloths, int doer)>();
            foreach (var q in people)
            {
                if (q.stage == LifeStage.Baby || q.home.IsNone || !Laundry.CanHang(d.season, d.weather)) continue;
                foreach (var s in DayOf(q))
                {
                    if (s.kind != ActivityKind.HangWashing || s.place != q.home || s.end - s.start < 0.1f || s.end > Laundry.Latest) continue;
                    raw.Add((q.home, s.start, s.end, Laundry.ClothsFor(LifeClock.RealSecondsBetween(s.start, s.end)), q.key));
                }
            }
            bool debugToday = debugLaundryOn && debugLaundryDay == day && byKey.TryGetValue(debugLaundryKey, out var dq) && !dq.home.IsNone;
            if (debugToday)
                raw.Add((byKey[debugLaundryKey].home, debugLaundryStart, debugLaundryLeave, Laundry.ClothsFor(LifeClock.RealSecondsBetween(debugLaundryStart, debugLaundryLeave), Laundry.BigCapacity), debugLaundryKey));
            raw.Sort((a, c) => a.start.CompareTo(c.start));
            // One line per chore. Two chores at one home that overlap (rare: housemates take different dawn chores) put their lines side by side
            // on the one cord: the later one's ordinal shifts it along.
            var merged = new List<(Place home, float start, float leave, int cloths, List<int> doers, int ordinal)>();
            foreach (var r in raw)
            {
                int ord = 0;
                foreach (var m in merged) if (m.home == r.home && r.start <= Laundry.TakeInAt(m.leave) + 0.001f) ord++;
                merged.Add((r.home, r.start, r.leave, Math.Min(r.cloths, Laundry.CapacityFor(GrownAt(r.home))), new List<int> { r.doer }, ord));
            }
            foreach (var m in merged)
            {
                float takeFrom = Math.Max(m.leave, Math.Min(Laundry.TakeInAt(m.leave), Laundry.Latest - Laundry.TakeInHours));
                float fade = Math.Max(takeFrom, Laundry.FadeAt(m.leave));
                bool isDebug = debugToday && m.doers.Contains(debugLaundryKey);
                int taker = LaundryLine.NoTaker; float takeAt = takeFrom;
                if (isDebug)
                {
                    taker = debugLaundryKey; takeAt = Math.Max(m.leave, debugLaundryTake);
                    fade = takeAt;
                }
                else
                {
                    var cands = TakerCandidates(m.home, m.doers, day);
                    for (float t = takeFrom; t <= Math.Max(takeFrom, fade - Laundry.TakeInHours) + 1e-4f && taker == LaundryLine.NoTaker; t += 0.1f)
                        foreach (var c in cands)
                            if (FreeAtHome(c, m.home, t, t + Laundry.TakeInHours)) { taker = c.key; takeAt = t; break; }
                }
                laundry.Add(new LaundryLine(m.home, day, m.ordinal, m.start, m.leave, takeAt, fade, m.cloths, taker, isDebug));
            }
            return laundry;
        }

        int GrownAt(Place home)
        {
            int n = 0;
            foreach (var q in people) if (q.Grown && q.home == home) n++;
            return n;
        }

        /// <summary>The doer(s) first, then the grown-ups of the home by the day's hash: whoever is idle at the door when the stay ends.</summary>
        List<Person> TakerCandidates(Place home, List<int> doers, long day)
        {
            var list = new List<Person>();
            foreach (int k in doers) if (byKey.TryGetValue(k, out var q)) list.Add(q);
            var mates = new List<Person>();
            foreach (var q in people) if (q.stage != LifeStage.Baby && q.home == home && !doers.Contains(q.key)) mates.Add(q); // a kid at home may do it too
            mates.Sort((a, c) => { uint ha = H(a.key, day, 705), hc = H(c.key, day, 705); return ha != hc ? ha.CompareTo(hc) : a.key.CompareTo(c.key); });
            list.AddRange(mates);
            return list;
        }

        /// <summary>The person is indoors or sitting out at their own door for the whole stretch: free to step out and take the washing in.</summary>
        bool FreeAtHome(Person q, Place home, float t0, float t1)
        {
            if (q.home != home || q.stage == LifeStage.Baby) return false;
            var segs = DayOf(q);
            var s = segs[Find(segs, t0)];
            return (s.kind == ActivityKind.Indoors || s.kind == ActivityKind.SitOut) && s.place == home && !s.Moving && s.start <= t0 + 1e-3f && s.end >= t1 - 1e-3f;
        }

        /// <summary>The taker's step out to the line (B28.d) as a short activity at the home; false for everyone else.</summary>
        bool LaundryActivityAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (buildDepth > 0 || p.stage == LifeStage.Baby || t.hour < 5f || t.hour > 17.6f) return false;
            var lines = LaundryOn(t.day);
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.takerKey != p.key || l.debug || t.hour < l.takeAt || t.hour >= l.TakeEnd) continue;
                a = TakeInActivity(p, l, t.hour);
                return true;
            }
            return false;
        }

        PersonActivity TakeInActivity(Person p, LaundryLine l, float hour) => new PersonActivity
        {
            who = p.who, kind = ActivityKind.TakeInWashing, place = l.home, from = l.home, prop = Prop.Basket, start = l.takeAt, end = l.TakeEnd,
            progress = Math.Min(1f, Math.Max(0f, (hour - l.takeAt) / Laundry.TakeInHours)), pace = 1f, chronicleId = -1,
        };

        // ---------------------------------------------------------------- debug (#705)

        /// <summary>Debug ("Laundry now"): <paramref name="personKey"/> hangs out the washing at their home from now (0.7 view hours), the stay is 16 s real, then
        /// they take it in. Returns false when they have no home.</summary>
        public bool DebugLaundryNow(ViewTime now, int personKey)
        {
            Sync();
            if (!byKey.TryGetValue(personKey, out var p) || p.home.IsNone) return false;
            debugLaundryOn = true; debugLaundryKey = personKey; debugLaundryDay = now.day;
            debugLaundryStart = now.hour; debugLaundryLeave = now.hour + Laundry.ChoreHours;
            debugLaundryTake = debugLaundryLeave + LifeClock.ViewHoursFor(debugLaundryLeave, 16f);
            laundryDay = long.MinValue;
            return true;
        }

        /// <summary>Debug: the adult whose home stands nearest (x, z) (box-local), or -1.</summary>
        public int DebugLaundryPick(float x, float z)
        {
            Sync();
            int best = -1; float bd = float.MaxValue;
            foreach (var q in people)
            {
                if (q.stage != LifeStage.Adult || q.home.IsNone) continue;
                if (!positions.TryGetPosition(q.home, out float hx, out float hz)) { hx = x; hz = z; } // a planner with no map (tests): any adult
                float dd = (hx - x) * (hx - x) + (hz - z) * (hz - z);
                if (dd < bd) { bd = dd; best = q.key; }
            }
            return best;
        }

        /// <summary>Debug ("Take in the washing"): ends the stay now (right after the chore when she is still hanging it).</summary>
        public bool DebugTakeInNow(ViewTime now)
        {
            if (!debugLaundryOn || debugLaundryDay != now.day) return false;
            debugLaundryTake = Math.Max(now.hour, debugLaundryLeave);
            laundryDay = long.MinValue;
            return true;
        }

        /// <summary>True while the cheat's chore, stay or take-in is running.</summary>
        public bool DebugLaundryRunning => debugLaundryOn;

        /// <summary>The cheat's doer: the chore, the sit while the washing dries, the take-in; then the day's plan again.</summary>
        bool DebugLaundryAt(Person p, ViewTime t, out PersonActivity a)
        {
            a = default;
            if (!debugLaundryOn || p.key != debugLaundryKey) return false;
            float end = debugLaundryTake + Laundry.TakeInHours;
            if (t.day != debugLaundryDay || t.hour >= end) { debugLaundryOn = false; laundryDay = long.MinValue; return false; }
            if (t.hour < debugLaundryStart) return false;
            if (t.hour < debugLaundryLeave)
                a = new PersonActivity { who = p.who, kind = ActivityKind.HangWashing, place = p.home, from = p.home, prop = Prop.Cloth, start = debugLaundryStart, end = debugLaundryLeave,
                    progress = (t.hour - debugLaundryStart) / (debugLaundryLeave - debugLaundryStart), pace = 1f, chronicleId = -1 };
            else if (t.hour < debugLaundryTake)
                a = new PersonActivity { who = p.who, kind = ActivityKind.SitOut, place = p.home, from = p.home, start = debugLaundryLeave, end = debugLaundryTake,
                    progress = (t.hour - debugLaundryLeave) / Math.Max(0.01f, debugLaundryTake - debugLaundryLeave), pace = 1f, chronicleId = -1 };
            else
                a = TakeInActivity(p, new LaundryLine(p.home, t.day, 0, debugLaundryStart, debugLaundryLeave, debugLaundryTake, debugLaundryTake, 0, p.key, true), t.hour);
            return true;
        }
    }
}
