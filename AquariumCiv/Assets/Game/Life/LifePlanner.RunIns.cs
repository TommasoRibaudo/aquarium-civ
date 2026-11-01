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
        /// <summary>Real seconds a beat lasts: a bicker, a chat, a wave between strangers.</summary>
        public const float BickerSeconds = 6f, ChatSeconds = 5f, WaveSeconds = 1.5f;
        /// <summary>How far (world units) someone will go out of their way to meet a friend or rival on a walk, and the share of days a pair does.</summary>
        const float InterceptUnits = 4f, RivalDayChance = 0.34f, FriendDayChance = 0.6f;
        const float MinStayAfter = 0.8f, NearUnits = 0.4f, MinWalkForRunIn = 0.16f;
        const float TieChance = 0.8f, KinChance = 0.5f, RivalChance = 0.7f, StrangerChance = 0.004f;

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
            TiePass(count);
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
                    float chance, secs = ChatSeconds;
                    if (a.p.rival == b.p || b.p.rival == a.p || a.p.partner == b.p || b.p.partner == a.p || a.p.friend == b.p || b.p.friend == a.p) continue; // ties: TiePass
                    else if (a.p.mentor == b.p || b.p.mentor == a.p || a.p.parent == b.p || b.p.parent == a.p) { kind = ActivityKind.Chat; chance = KinChance; }
                    else
                    {
                        if (waved.Contains(a.p.key) || waved.Contains(b.p.key)) continue;
                        kind = ActivityKind.Wave; chance = StrangerChance; secs = WaveSeconds;
                    }
                    float lo = Math.Max(a.s.start + (a.s.end - a.s.start) * 0.2f, b.s.start + (b.s.end - b.s.start) * 0.2f);
                    float hi = Math.Min(a.s.end - (a.s.end - a.s.start) * 0.15f, b.s.end - (b.s.end - b.s.start) * 0.15f);
                    float len = secs / LifeClock.RealSecondsPerViewHour(Math.Max(5f, Math.Min(23f, lo)));
                    if (hi <= lo) continue;
                    if (R(PairKey(a.p, b.p), day, 91) >= chance) continue;
                    float t = Meet(a, b, lo, hi);
                    if (t < 0f) continue;
                    if (Overlaps(a.p, t, len) || Overlaps(b.p, t, len)) continue;
                    Add(a.p, b.p, kind, t, t + len);
                    Add(b.p, a.p, kind, t, t + len);
                    count[a.p.key] = ca + 1;
                    count[b.p.key] = cb + 1;
                    if (kind == ActivityKind.Wave) { waved.Add(a.p.key); waved.Add(b.p.key); }
                    runInCount++;
                }
            }
        }

        /// <summary>Friends, couples and rivals meet on the way (#197): on a day a pair does, one stops on a walk (to or from
        /// work, to the green) and the other, if a walk or a stay of theirs is within <see cref="InterceptUnits"/>, goes
        /// over to them and back (an intercept: a straight leg to the spot, then the way back into their day). Rivals bicker
        /// about one day in three, never on a day with the evening argument (rule 8).</summary>
        void TiePass(Dictionary<int, int> count)
        {
            long day = dayInfo.day;
            for (int i = 0; i < people.Count; i++)
            {
                var a = people[i];
                if (!a.Grown || EveningBusy(a)) continue;
                for (int k = 0; k < 3; k++)
                {
                    var b = k == 0 ? a.rival : k == 1 ? a.friend : a.partner;
                    if (b == null || !b.Grown || EveningBusy(b) || b == a) continue;
                    if (a.key > b.key && (b.rival == a || b.friend == a || b.partner == a)) continue; // once per pair
                    if (R(PairKey(a, b), day, 93 + k) >= (k == 0 ? RivalDayChance : FriendDayChance)) continue;
                    if (k == 0 && R(PairKey(a, b), day, 60) < 0.35f) continue;
                    count.TryGetValue(a.key, out int ca);
                    count.TryGetValue(b.key, out int cb);
                    if (ca >= RunInsPerDay || cb >= RunInsPerDay) continue;
                    var kind = k == 0 ? ActivityKind.Argue : ActivityKind.Chat;
                    float secs = k == 0 ? BickerSeconds : ChatSeconds;
                    if (!Intercept(a, b, kind, secs) && !Intercept(b, a, kind, secs)) continue;
                    count[a.key] = ca + 1;
                    count[b.key] = cb + 1;
                    runInCount++;
                }
            }
        }
        bool Carrying(Seg s) => ActivityInfo.IsMove(s.kind) && s.prop != Prop.None;

        /// <summary><paramref name="stopper"/> stops on one of their walks; <paramref name="goer"/> comes over from where they are.</summary>
        bool Intercept(Person stopper, Person goer, ActivityKind kind, float secs)
        {
            var gsegs = DayOf(goer);
            foreach (var w in runWalks)
            {
                if (w.p != stopper) continue;
                if (w.poly == null) continue;
                float dur = w.s.end - w.s.start;
                float len = secs / LifeClock.RealSecondsPerViewHour(Math.Max(5f, Math.Min(23f, w.s.start)));
                float hi = w.s.end - dur * 0.15f; // the beat may run past the walk's end: they arrive that much later, to a stay of at least MinStayAfter
                for (float t = w.s.start + dur * 0.2f; t <= hi; t += 0.05f)
                {
                    w.poly.At((t - w.s.start) / dur, out float mx, out float mz);
                    float t0 = t;
                    float d = 0f;
                    for (int it = 0; it < 2; it++) // where they are when they set off, which depends on how far they have to come
                    {
                        if (!PlanPos(goer, t0, out float gx, out float gz)) { d = float.MaxValue; break; }
                        d = (float)Math.Sqrt((gx - mx) * (gx - mx) + (gz - mz) * (gz - mz));
                        t0 = t - (d > NearUnits ? d / WalkSpeed : 0f);
                    }
                    if (d > InterceptUnits) continue;
                    var g0 = gsegs[Find(gsegs, t0)];
                    var g1 = gsegs[Find(gsegs, t)];
                    if (g0.src != ActivitySource.Schedule || g1.src != ActivitySource.Schedule || g0.kind == ActivityKind.Sleep || g1.kind == ActivityKind.Sleep || Carrying(g0) || Carrying(g1)) continue;
                    float tEnd = t + len;
                    if (Overlaps(goer, t0, tEnd - t0) || Overlaps(stopper, t, tEnd - t)) continue;
                    Add(stopper, goer, kind, t, tEnd + 0.02f);
                    Add(goer, stopper, kind, d > NearUnits ? t0 : t, tEnd, d > NearUnits, mx, mz);
                    return true;
                }
            }
            return false;
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

        void Add(Person p, Person with, ActivityKind kind, float t, float end, bool point = false, float mx = 0f, float mz = 0f)
        {
            if (!runInCache.TryGetValue(p.key, out var l)) runInCache[p.key] = l = new List<Seg>(2);
            l.Add(new Seg { kind = kind, start = t, end = end, hasPoint = point, mx = mx, mz = mz, place = Place.None, from = Place.None, partner = with.who, src = ActivitySource.Schedule, trip = TripType.None, chronicleId = -1 });
        }
    }
}
