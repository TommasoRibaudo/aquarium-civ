using System;
using System.Collections.Generic;
using AquariumCiv.Audio;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using T = AquariumCiv.View.Shaping.ShapingTuning;

namespace AquariumCiv.View.Shaping
{
    /// <summary>
    /// The view's side of shaping the land (#85): which shapings are on screen and where each is in its life (arriving,
    /// waiting, answered, leaving), the people's answer shown one at a time, and the calls the shaping tray (#84) and the
    /// debug panel make. Pure C#: the view calls <see cref="Sync"/> every frame with its real clock and view time, then
    /// samples <see cref="ShapingLooks"/> through <see cref="Rain"/>, <see cref="Grove"/>, <see cref="Channel"/>,
    /// <see cref="Herd"/> and <see cref="Answer"/>.
    /// <para>It never draws from the sim's dice and never changes the economy. The only sim write is the view's own flag
    /// <see cref="Nudge.reported"/> ("the view has shown the answer"), set when an answer's show ends.</para>
    /// </summary>
    public sealed class ShapingShow
    {
        /// <summary>One shaping on screen.</summary>
        public sealed class Entry
        {
            public ShapingFootprint footprint;
            public int nudgeId;
            public string kind;
            public long month;
            /// <summary>Real seconds: first seen, the answer seen (NaN = not answered), its show started (NaN = not yet), taken back (−1 = no).</summary>
            public float seenAt, answeredAt = float.NaN, showAt = float.NaN, leavingAt = -1f;
            public long answerMonth = -1;
            public int buildingId = -1;
            /// <summary>Answered, waiting for its turn to be shown.</summary>
            public bool answerQueued;
            /// <summary>The people near it still have to react (placed since the last sync).</summary>
            public bool react;
            /// <summary>Its answer moment in the world (the planner's), when one was staged.</summary>
            public RelicMomentInfo moment;
            public AnswerLook answer;
        }

        readonly Dictionary<int, Entry> byId = new Dictionary<int, Entry>();
        readonly List<Entry> entries = new List<Entry>();
        readonly List<ShapingEvent> pending = new List<ShapingEvent>();
        readonly List<Entry> answerQueue = new List<Entry>();
        Entry showing;
        SimCore sim;
        float now;
        ViewTime viewNow;
        bool synced;

        /// <summary>Answered while the player was away longer ago than this (game months): reported quietly, not shown.</summary>
        public const int ShowAnswersWithinMonths = 12;
        /// <summary>At most this many answers queue up to be shown after a return; the rest are reported quietly.</summary>
        public const int MaxQueuedAnswers = 3;
        /// <summary>Answers are staged only in the day's view hours (from the work start to nightfall); the rest wait for morning.</summary>
        public static bool ShowHour(float hour) => hour >= LifeClock.WorkStart && hour < LifeClock.Night;
        /// <summary>Real seconds between two queued answer shows.</summary>
        public const float AnswerGap = 1.5f;
        /// <summary>Box units round a new shaping within which the people react.</summary>
        public const float ReactRadius = 1.2f;

        /// <summary>The shapings on screen, in placing order.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        public Entry Find(int nudgeId) => byId.TryGetValue(nudgeId, out var e) ? e : null;

        /// <summary>The answer being shown now (<see cref="AnswerLook.active"/> false when none).</summary>
        public AnswerLook Answer => showing != null ? showing.answer : default;

        // ================================================================== the calls (#84's tray, the debug panel)

        /// <summary>The Keeper puts a tool at a point (the tray, #84): the sim's <see cref="SimCore.PlaceShaping"/>, then its
        /// arrival plays from now. On a fail the view plays <see cref="FailCue"/> and says why (practices §4).</summary>
        public SimCore.ShapeResult Place(SimCore s, string kind, float x, float z, int creditNotableId = -1)
        {
            var r = s.PlaceShaping(kind, x, z, creditNotableId);
            if (r.Ok) Track(s, r.nudge, false);
            return r;
        }

        /// <summary>The cue for a tool that can't be used now.</summary>
        public const string FailCue = CueLibrary.ShapeFail;

        /// <summary>Debug panel: a tool at a point, free of charge, lock and cap (<see cref="SimCore.DebugShape"/>).</summary>
        public Nudge DebugPlace(SimCore s, string kind, float x, float z)
        {
            var n = s.DebugShape(kind, x, z);
            if (n != null) Track(s, n, false);
            return n;
        }

        /// <summary>The Keeper takes a shaping back (the "Take back" chip): the sim refunds it and the gift leaves on screen.
        /// False once the people have used it.</summary>
        public bool TakeBack(SimCore s, int nudgeId)
        {
            if (!s.CanTakeBack(nudgeId)) return false;
            var n = s.State.nudges.Find(x => x.id == nudgeId);
            var e = Find(nudgeId) ?? Track(s, n, true); // snapshot before the sim forgets its cells
            if (!s.TakeBack(nudgeId)) return false;
            Leave(e, ShapingEventKind.TakenBack);
            return true;
        }

        /// <summary>Debug panel: the people answer now (<see cref="SimCore.DebugSkipToAnswer"/>) and the building is placed
        /// at once; the next <see cref="Sync"/> shows it.</summary>
        public bool SkipToAnswer(SimCore s, int nudgeId)
        {
            if (!s.DebugSkipToAnswer(nudgeId)) return false;
            s.Settlement?.PlacePending(); // the answer's building gets its lot now, not at the month's end
            return true;
        }

        /// <summary>The "Take back" chip for a shaping (research 10 "Undo"): shown while the people haven't used it.</summary>
        public TakeBackChip ChipFor(SimCore s, int nudgeId)
        {
            var e = Find(nudgeId);
            if (e == null || e.leavingAt >= 0f || !s.CanTakeBack(nudgeId)) return new TakeBackChip { nudgeId = nudgeId };
            return new TakeBackChip { visible = true, nudgeId = nudgeId, x = e.footprint.x, z = e.footprint.z };
        }

        // ================================================================== every frame

        /// <summary>
        /// Catch up with the sim. <paramref name="realNow"/> is the view's real clock in seconds; <paramref name="view"/> the
        /// view time (its day is the sim month). With a <paramref name="planner"/>, a new shaping makes the people near it
        /// react (<see cref="LifePlanner.ReactAt"/>) and an answer is staged in the world (<see cref="LifePlanner.StartShapingAnswer"/>).
        /// Fills <paramref name="events"/> (cleared first) with what happened: the view plays each event's cue.
        /// </summary>
        public void Sync(SimCore s, float realNow, ViewTime view, LifePlanner planner = null, List<ShapingEvent> events = null)
        {
            sim = s;
            now = realNow;
            viewNow = view;
            bool first = !synced;
            synced = true;
            var alive = new HashSet<int>();
            foreach (var n in s.State.nudges)
            {
                if (Array.IndexOf(SimCore.SliceTools, n.kind) < 0) continue;
                alive.Add(n.id);
                var e = Find(n.id);
                if (e == null) e = Track(s, n, first);
                if (e.react)
                {
                    // The people near it look (a rain: hands out, kids splash; a shaping: they point).
                    e.react = false;
                    planner?.ReactAt(n.kind == SimCore.NudgeKind.Rain ? KeeperGesture.Rain : KeeperGesture.Shape, view, n.x, n.z, ReactRadius);
                }
                if (n.credited && e.answerMonth < 0)
                {
                    e.answerMonth = n.answerMonth;
                    e.buildingId = n.buildingId;
                    bool quiet = n.reported || (first && s.State.month - n.answerMonth > ShowAnswersWithinMonths) || answerQueue.Count >= MaxQueuedAnswers;
                    e.answeredAt = quiet ? now - 1e4f : now;
                    if (quiet) n.reported = true;
                    else { e.answerQueued = true; answerQueue.Add(e); }
                }
            }
            // Gone from the sim without our take-back (another box loaded, a debug reset): they leave too.
            foreach (var e in entries)
                if (e.leavingAt < 0f && !alive.Contains(e.nudgeId)) Leave(e, ShapingEventKind.TakenBack);
            // Left: dropped.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.leavingAt >= 0f && now - e.leavingAt >= LeaveSecondsOf(e.kind))
                {
                    pending.Add(new ShapingEvent { kind = ShapingEventKind.Gone, nudgeId = e.nudgeId, tool = e.kind });
                    entries.RemoveAt(i);
                    byId.Remove(e.nudgeId);
                    answerQueue.Remove(e);
                    if (showing == e) showing = null;
                }
            }
            StepAnswers(s, planner);
            if (events != null) { events.Clear(); events.AddRange(pending); }
            pending.Clear();
        }

        void StepAnswers(SimCore s, LifePlanner planner)
        {
            if (showing != null)
            {
                var a = AnswerOf(s, showing, now - showing.showAt);
                showing.answer = a;
                if (a.progress < 1f) return;
                var n = s.State.nudges.Find(x => x.id == showing.nudgeId);
                if (n != null) n.reported = true;
                showing.answer = default;
                showing = null;
            }
            if (answerQueue.Count == 0) return;
            // After dark nobody can walk over to it (user 2026-10-30): the answer is held, queue and all, until the next
            // morning's view hours. The sim's answer isn't delayed, only its staging.
            if (!ShowHour(viewNow.hour)) return;
            var next = answerQueue[0];
            float lastEnd = lastAnswerEnd;
            if (now - lastEnd < AnswerGap) return;
            answerQueue.RemoveAt(0);
            next.answerQueued = false;
            next.showAt = now;
            showing = next;
            next.moment = planner?.StartShapingAnswer(next.nudgeId, viewNow);
            next.answer = AnswerOf(s, next, 0f);
            pending.Add(new ShapingEvent { kind = ShapingEventKind.Answered, nudgeId = next.nudgeId, tool = next.kind, cue = CueLibrary.Chime });
            lastAnswerEnd = now + ShapingLooks.AnswerSeconds(next.answer.bubble);
        }

        float lastAnswerEnd = float.MinValue;

        AnswerLook AnswerOf(SimCore s, Entry e, float since)
        {
            var n = s.State.nudges.Find(x => x.id == e.nudgeId);
            if (n == null) return new AnswerLook { nudgeId = e.nudgeId, progress = 1f };
            var line = ShapingAnswers.EntryOf(s, n);
            float x = n.x, z = n.z;
            var b = n.buildingId >= 0 ? s.State.buildings.Find(q => q.id == n.buildingId) : null;
            if (b != null && b.lotId != 0) { x = b.x; z = b.z; }
            return ShapingLooks.Answer(n.id, b != null ? b.id : -1, line?.id ?? -1, ShapingAnswers.CreditedNotableId(s, n), x, z,
                ShapingLooks.LabelOf(n.kind, n.variant, (Era)n.eraAtPlace), line?.text, since);
        }

        Entry Track(SimCore s, Nudge n, bool loaded)
        {
            var e = new Entry
            {
                footprint = ShapingFootprint.Of(n, s.Settlement?.Map, s.State.seed), nudgeId = n.id, kind = n.kind, month = n.month,
                seenAt = loaded ? now - 1e4f : now,
            };
            byId[n.id] = e;
            entries.Add(e);
            e.react = !loaded;
            if (!loaded) pending.Add(new ShapingEvent { kind = ShapingEventKind.Placed, nudgeId = n.id, tool = n.kind, cue = PlaceCue(n.kind) });
            return e;
        }

        void Leave(Entry e, ShapingEventKind why)
        {
            if (e.leavingAt >= 0f) return;
            e.leavingAt = now;
            answerQueue.Remove(e);
            pending.Add(new ShapingEvent { kind = why, nudgeId = e.nudgeId, tool = e.kind, cue = CueLibrary.UiBack });
        }

        /// <summary>The cue a tool makes when it lands (#96's ids; grove and herd have none yet).</summary>
        public static string PlaceCue(string kind) => kind switch
        {
            SimCore.NudgeKind.Rain => CueLibrary.Rain,
            SimCore.NudgeKind.Channel => CueLibrary.ShapeChannel,
            _ => null,
        };

        static float LeaveSecondsOf(string kind) => kind == SimCore.NudgeKind.Herd ? T.HerdLeaveSeconds : T.LeaveSeconds;

        // ================================================================== sampling

        /// <summary>Where a shaping is in its life now.</summary>
        public ShapingStage StageOf(Entry e)
        {
            if (e.leavingAt >= 0f) return now - e.leavingAt >= LeaveSecondsOf(e.kind) ? ShapingStage.Gone : ShapingStage.Leaving;
            if (e.answerMonth >= 0) return ShapingStage.Answered;
            return now - e.seenAt < ArrivalSeconds(e.footprint) ? ShapingStage.Arriving : ShapingStage.Waiting;
        }

        /// <summary>Real seconds a tool's arrival plays (the cloud's whole shower, the saplings popping, the carving, the run-in).</summary>
        public static float ArrivalSeconds(ShapingFootprint f) => f.kind switch
        {
            SimCore.NudgeKind.Rain => T.RainDelaySeconds + T.RainSeconds(f.level) + T.CloudFadeSeconds,
            SimCore.NudgeKind.Grove => f.cells.Count * T.SaplingStagger + T.SaplingPopSeconds,
            SimCore.NudgeKind.Channel => ShapingLooks.CarveSeconds(f.cells.Count) + T.WaterLagSeconds,
            SimCore.NudgeKind.Herd => f.variant == "fish" ? T.SplashSeconds * 2f : T.RunInSeconds + T.RunInStagger * T.HerdCount(f.Species),
            _ => 0f,
        };

        /// <summary>The clocks for an entry at the last <see cref="Sync"/>.</summary>
        public ShapingClock ClockOf(Entry e)
        {
            float age = (float)(viewNow.day - e.month) + viewNow.hour / 24f;
            float sinceAnswerMonths = e.answerMonth >= 0 ? Math.Max(0f, (float)(viewNow.day - e.answerMonth) + viewNow.hour / 24f) : -1f;
            return new ShapingClock
            {
                sinceSeen = now - e.seenAt,
                sinceAnswered = float.IsNaN(e.answeredAt) ? -1f : Math.Max(0f, now - e.answeredAt),
                sinceLeaving = e.leavingAt >= 0f ? now - e.leavingAt : -1f,
                ageMonths = Math.Max(0f, age),
                monthsSinceAnswer = sinceAnswerMonths,
                era = sim?.State.era ?? Era.Camp,
                day = viewNow.day,
            };
        }

        public RainLook Rain(Entry e) => ShapingLooks.Rain(e.footprint, ClockOf(e));
        /// <summary>The grove (its trees leave with the woods the people cut; a taken-back grove sinks whole, the land is
        /// already put back).</summary>
        public GroveLook Grove(Entry e) => ShapingLooks.Grove(e.footprint, ClockOf(e), e.leavingAt >= 0f ? null : sim?.Settlement?.Map);
        public ChannelLook Channel(Entry e) => ShapingLooks.Channel(e.footprint, ClockOf(e));

        public HerdLook Herd(Entry e)
        {
            float lx = float.NaN, lz = float.NaN;
            if (e.buildingId >= 0 && sim != null)
            {
                var b = sim.State.buildings.Find(q => q.id == e.buildingId);
                if (b != null && b.lotId != 0) { lx = b.x; lz = b.z; }
            }
            return ShapingLooks.Herd(e.footprint, ClockOf(e), lx, lz);
        }
    }
}
