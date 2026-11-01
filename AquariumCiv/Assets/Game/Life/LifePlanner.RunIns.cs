using System;
using System.Collections.Generic;

namespace AquariumCiv.Life
{
    /// <summary>
    /// Run-ins on the way (#197): two people whose walks pass close stop for a short beat. Friends and couples chat,
    /// rivals bicker, strangers wave. The beat is the existing Chat / Argue / Wave activity with the other as partner, laid
    /// over the walk as an interruption block (the same machinery as a Keeper reaction: the doll stops where it stands on
    /// the route, then walks on from there), so the view needs no new animation.
    ///
    /// Rare on purpose: a chance by tie per meeting, at most <see cref="RunInsPerDay"/> beats a day per person and one
    /// wave between strangers, and only on a walk with a stay of at least <see cref="MinStayAfter"/> hours after it, so a
    /// beat of a few real seconds only ever pushes an arrival a little later and never swallows a stay. Deterministic from
    /// (seed, the pair, the day) and the day's plan; nothing is saved.
    /// </summary>
    public sealed partial class LifePlanner
    {
        /// <summary>Beats a day per person at most (ties and strangers together).</summary>
        public const int RunInsPerDay = 2;
        /// <summary>Real seconds a beat lasts (a wave between strangers is half).</summary>
        public const float RunInSeconds = 3f;
        const float MinStayAfter = 0.6f, NearUnits = 0.4f, MinWalkForRunIn = 0.16f;
        const float TieChance = 0.8f, KinChance = 0.5f, RivalChance = 0.7f, StrangerChance = 0.02f;

        readonly Dictionary<int, List<Seg>> runInCache = new Dictionary<int, List<Seg>>();
        long runInDay = long.MinValue;
        bool runInReady;
        int runInCount;

        /// <summary>Beats laid over today's walks (tests, the debug readout); builds the day's run-ins.</summary>
        public int RunInsToday { get { Sync(); EnsureRunIns(); return runInCount; } }

        /// <summary>Today's beats by kind: friends and kin chatting, rivals bickering, strangers waving (each beat counted once per person).</summary>
        public void RunInKindsToday(out int chats, out int bickers, out int waves)
        {
            Sync();
            EnsureRunIns();
            chats = bickers = waves = 0;
            foreach (var kv in runInCache)
                foreach (var s in kv.Value)
                {
                    if (s.kind == ActivityKind.Chat) chats++;
                    else if (s.kind == ActivityKind.Argue) bickers++;
                    else waves++;
                }
        }

        struct WalkRef
        {
            public Person p;
            public Seg s;
            public Poly poly;
        }

        readonly List<WalkRef> runWalks = new List<WalkRef>();

        List<Seg> RunInsOf(Person p)
        {
            EnsureRunIns();
            return runInCache.TryGetValue(p.key, out var l) ? l : null;
        }

        void EnsureRunIns()
        {
            if (runInReady && runInDay == dayInfo.day) return;
            runInReady = true;
            runInDay = dayInfo.day;
            runInCache.Clear();
            runInCount = 0;
            long day = dayInfo.day;
            runWalks.Clear();
            for (int pi = 0; pi < people.Count; pi++)
            {
                var p = people[pi];
                if (p.stage == LifeStage.Baby) continue;
                var segs = DayOf(p);
                for (int i = 0; i < segs.Length - 1; i++)
                {
                    var s = segs[i];
                    if (s.kind != ActivityKind.Walk || !s.Moving || s.src != ActivitySource.Schedule || s.cut || s.resumed) continue;
                    if (s.end - s.start < MinWalkForRunIn || s.carries || s.prop != Prop.None) continue;
                    var next = segs[i + 1]; // the very next piece must be a stay of its own (not another move), so a beat only ever delays an arrival
                    if (next.Moving || ActivityInfo.IsMove(next.kind) || next.end - next.start < MinStayAfter || next.place != s.place) continue;
                    runWalks.Add(new WalkRef { p = p, s = s, poly = RouteBetween(s.from, s.place) });
                }
            }
            runWalks.Sort((a, b) => a.s.start.CompareTo(b.s.start));
            var count = new Dictionary<int, int>();
            var waved = new HashSet<int>();
            for (int i = 0; i < runWalks.Count; i++)
            {
                var a = runWalks[i];
                for (int j = i + 1; j < runWalks.Count && runWalks[j].s.start < a.s.end - 0.1f; j++)
                {
                    var b = runWalks[j];
                    if (a.p == b.p) continue;
                    count.TryGetValue(a.p.key, out int ca);
                    count.TryGetValue(b.p.key, out int cb);
                    if (ca >= RunInsPerDay || cb >= RunInsPerDay) continue;
                    ActivityKind kind;
                    float chance, secs = RunInSeconds;
                    if (a.p.rival == b.p || b.p.rival == a.p)
                    {
                        // Rivals argue at most once a day (rule 8): not when the evening argument is on today.
                        if (R(PairKey(a.p, b.p), day, 60) < 0.35f) continue;
                        kind = ActivityKind.Argue; chance = RivalChance;
                    }
                    else if (a.p.partner == b.p || b.p.partner == a.p || a.p.friend == b.p || b.p.friend == a.p) { kind = ActivityKind.Chat; chance = TieChance; }
                    else if (a.p.mentor == b.p || b.p.mentor == a.p || a.p.parent == b.p || b.p.parent == a.p) { kind = ActivityKind.Chat; chance = KinChance; }
                    else
                    {
                        if (waved.Contains(a.p.key) || waved.Contains(b.p.key)) continue;
                        kind = ActivityKind.Wave; chance = StrangerChance; secs *= 0.5f;
                    }
                    float lo = Math.Max(a.s.start + (a.s.end - a.s.start) * 0.2f, b.s.start + (b.s.end - b.s.start) * 0.2f);
                    float hi = Math.Min(a.s.end - (a.s.end - a.s.start) * 0.15f, b.s.end - (b.s.end - b.s.start) * 0.15f);
                    float len = secs / LifeClock.RealSecondsPerViewHour(Math.Max(5f, Math.Min(23f, lo)));
                    hi -= len;
                    if (hi <= lo) continue;
                    if (R(PairKey(a.p, b.p), day, 91) >= chance) continue;
                    float t = Meet(a, b, lo, hi);
                    if (t < 0f) continue;
                    if (Overlaps(a.p, t, len) || Overlaps(b.p, t, len)) continue;
                    Add(a.p, b.p, kind, t, len);
                    Add(b.p, a.p, kind, t, len);
                    count[a.p.key] = ca + 1;
                    count[b.p.key] = cb + 1;
                    if (kind == ActivityKind.Wave) { waved.Add(a.p.key); waved.Add(b.p.key); }
                    runInCount++;
                }
            }
        }

        /// <summary>When in [lo, hi] the two walkers pass within <see cref="NearUnits"/> of each other along their routes (the
        /// middle of the overlap if they share a corridor, when there are no positions), else -1.</summary>
        float Meet(in WalkRef a, in WalkRef b, float lo, float hi)
        {
            if (a.poly == null || b.poly == null)
            {
                bool corridor = (a.s.from == b.s.from && a.s.place == b.s.place) || (a.s.from == b.s.place && a.s.place == b.s.from);
                return corridor ? (lo + hi) * 0.5f : -1f;
            }
            for (float t = lo; t <= hi; t += 0.02f)
            {
                a.poly.At((t - a.s.start) / (a.s.end - a.s.start), out float ax, out float az);
                b.poly.At((t - b.s.start) / (b.s.end - b.s.start), out float bx, out float bz);
                float dx = ax - bx, dz = az - bz;
                if (dx * dx + dz * dz < NearUnits * NearUnits) return t;
            }
            return -1f;
        }

        bool Overlaps(Person p, float t, float len)
        {
            if (!runInCache.TryGetValue(p.key, out var l)) return false;
            foreach (var s in l) if (t < s.end + 0.1f && t + len > s.start - 0.1f) return true;
            return false;
        }

        void Add(Person p, Person with, ActivityKind kind, float t, float len)
        {
            if (!runInCache.TryGetValue(p.key, out var l)) runInCache[p.key] = l = new List<Seg>(2);
            l.Add(new Seg { kind = kind, start = t, end = t + len, place = Place.None, from = Place.None, partner = with.who, src = ActivitySource.Schedule, trip = TripType.None, chronicleId = -1 });
        }
    }
}
