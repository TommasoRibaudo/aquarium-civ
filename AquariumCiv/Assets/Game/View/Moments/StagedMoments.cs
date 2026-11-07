using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #370: the staged moments in the box (visit-loop.md §2.1, §4.3; animation-specs C3): the wedding the people organise
    /// because the Keeper showed up, the newborn held up to the glass, the new chief taking the seat, the festival. Registered
    /// with C2's director (<see cref="HeldMomentStage.Register"/>) for those four kinds (a callback keeps C2's placeholder).
    /// <list type="bullet">
    /// <item>A handed-over moment is cast and planned by the planner (<see cref="LifePlanner.StageHeldMoment"/>): real people,
    /// the real fire and glass, a beginning, a middle and an end beat, no camera move. At night it waits for the morning
    /// in the queue (t4-fixes: SimCore.NightWaitMoment, Waiting, never "playing" with nothing shown); one that can't be cast yet waits
    /// again (SimCore.DeferMoment; a card only after several tries).</item>
    /// <item>Each frame the people view asks <see cref="PeopleView.StagedSpot"/> and <see cref="PeopleView.StagedPose"/>: the
    /// plan's spot (the aisle, the ring, the glass) and its beat (<see cref="MomentBeats"/>), the same steps the planner plays.</item>
    /// <item><b>Done</b> once the scene is over (everyone walks back into their day); <b>Stop</b> ends it at once and releases
    /// everyone (the box changed, the app left).</item>
    /// </list>
    /// </summary>
    public static class StagedMoments
    {
        /// <summary>The kinds staged here.</summary>
        public static readonly MomentKind[] Kinds = { MomentKind.Wedding, MomentKind.Newborn, MomentKind.NewLeader, MomentKind.Festival };
        /// <summary>A moment waiting for the morning gives up after this long (real seconds) and waits again (the director's own
        /// limit is 3 watched minutes).</summary>
        public const float MaxWaitSeconds = 150f;

        sealed class Live
        {
            public HeldMoment moment;
            public SimCore sim;
            public PeopleView pv;
            public LifePlanner planner;
            public HeldMomentScene scene;
            public float since;
            public bool over;
            public readonly HashSet<int> marked = new HashSet<int>();
            public List<RitualBubble> bubbles;
            public StagedSign sign; // t1-moments: the wedding's arch and petals
        }

        static readonly List<Live> lives = new List<Live>();

        /// <summary>Counters (tests, the panel): scenes started, finished on their own, stopped early, given up (made a card).</summary>
        public static int Started, Finished, Stopped, GaveUp;
        /// <summary>t4-fixes: moments handed over at night and put back to wait for the morning (tests, the panel).</summary>
        public static int NightWaits;
        /// <summary>The last scene started (tests, the panel).</summary>
        public static HeldMomentScene LastScene { get; private set; }
        public static string LastWhy { get; private set; } = "";
        /// <summary>The held moment whose scene is on stage now (null = none). The people's bubbles say its own lines.</summary>
        public static HeldMoment PlayingMoment(SimCore sim)
        {
            if (sim == null) return null;
            for (int i = lives.Count - 1; i >= 0; i--)
            {
                var l = lives[i];
                if (l.scene == null || l.over || !ReferenceEquals(l.sim, sim)) continue;
                var m = sim.FindMoment(l.moment.id);
                if (m != null && m.state == HeldMomentState.Playing) return m;
            }
            return null;
        }

        /// <summary>The scenes playing now.</summary>
        public static int Playing { get { int n = 0; foreach (var l in lives) if (!l.over && l.scene != null) n++; return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { lives.Clear(); Started = Finished = Stopped = GaveUp = NightWaits = 0; LastScene = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            foreach (var k in Kinds) HeldMomentStage.Register(k, Stage);
            PeopleView.StagedSpot = SpotHook;
            PeopleView.StagedPose = PoseHook;
        }

        /// <summary>Tests: the placeholder back for these kinds and the hooks off.</summary>
        public static void Uninstall()
        {
            foreach (var k in Kinds) HeldMomentStage.Register(k, null);
            PeopleView.StagedSpot = null;
            PeopleView.StagedPose = null;
            StopAll();
        }

        // ---------------------------------------------------------------- the play contract (#369)

        static MomentStaging Stage(HeldMoment m, SimCore sim)
        {
            var pv = PeopleView.Find();
            var planner = pv != null ? pv.Planner : null;
            if (planner == null || m == null) { LastWhy = "no people view"; return null; }
            var live = new Live { moment = m, sim = sim, pv = pv, planner = planner };
            if (!LifePlanner.HeldMomentFits(pv.Now, LifePlanner.HeldMomentSeconds(m.kind)))
            {
                // t4-fixes (tester run 3): night. It waits for the morning in the queue (Waiting, not a try), never "playing" with nothing on
                // screen: the stage sees it is no longer playing and lets it be.
                sim.NightWaitMoment(m.id);
                NightWaits++;
                LastWhy = "night: it waits for the morning";
                return null;
            }
            if (!TryStart(live)) return null; // can't be cast: it waits (a card only after several tries)
            lives.Add(live);
            return new MomentStaging { Done = () => Done(live), Stop = () => End(live, true) };
        }

        static bool TryStart(Live live)
        {
            live.scene = live.planner.StageHeldMoment(live.moment, live.pv.Now);
            if (live.scene == null) { LastWhy = live.planner.LastHeldWhyNot; return false; }
            LastScene = live.scene;
            if (live.moment.kind == MomentKind.Wedding)
            {
                var sc = live.scene;
                var pv = live.pv;
                live.sign = StagedSign.Wedding(UnityEngine.Object.FindAnyObjectByType<BoxView>(), sc.plan,
                    () => pv == null ? 0f : pv.Now.day != sc.day ? 999f : LifeClock.RealSecondsBetween(sc.start, pv.Now.hour)); // t1-moments: a wedding looks like one
            }
            LastWhy = null;
            Started++;
            return true;
        }

        static bool Done(Live live)
        {
            if (live.over) return true;
            var hm = live.sim.FindMoment(live.moment.id);
            if (live.pv == null || live.pv.Planner != live.planner || hm == null || hm.state != HeldMomentState.Playing) { End(live, true); return true; }
            if (live.scene == null)
            {
                // Night: the people hold it in the morning.
                live.since += HeldMomentStage.CheckSeconds;
                if (LifePlanner.HeldMomentFits(live.pv.Now, LifePlanner.HeldMomentSeconds(live.moment.kind)))
                {
                    if (TryStart(live)) return false;
                }
                else if (live.since < MaxWaitSeconds) return false;
                live.sim.DeferMoment(live.moment.id); // t2-polish: not staged yet: it waits for the Keeper again (a card only after several tries)
                GaveUp++;
                End(live, false);
                return true;
            }
            if (live.planner.HeldMomentPlaying(live.scene, live.pv.Now)) return false;
            Finished++;
            End(live, false);
            return true;
        }

        static void End(Live live, bool early)
        {
            if (live.over) return;
            live.over = true;
            if (live.sign != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(live.sign.gameObject); else UnityEngine.Object.DestroyImmediate(live.sign.gameObject);
                live.sign = null;
            }
            if (live.scene != null)
            {
                live.planner?.EndHeldMoment(live.scene);
                if (early) Stopped++;
            }
            lives.Remove(live);
        }

        /// <summary>End every staged moment now (the panel; the stage's own Stop does it per moment).</summary>
        public static int StopAll()
        {
            int n = lives.Count;
            foreach (var l in lives.ToArray()) End(l, true);
            return n;
        }

        // ---------------------------------------------------------------- the people view's hooks

        static bool Find(in PersonActivity a, ViewTime now, out Live live, out HeldMomentScene sc, out MomentMember m, out int index, out float secs)
        {
            live = null; sc = null; m = null; index = -1; secs = 0f;
            for (int i = lives.Count - 1; i >= 0; i--)
            {
                var l = lives[i];
                if (l.scene == null || l.over || l.planner == null) continue;
                if (l.planner.HeldMomentAt(a.who, now, out sc, out m, out index, out secs) && ReferenceEquals(sc, l.scene)) { live = l; return true; }
            }
            return false;
        }

        static bool SpotHook(in PersonActivity a, ViewTime now, out float x, out float z, out Vector2? face)
        {
            x = z = 0f; face = null;
            if (lives.Count == 0) return false;
            if (!Find(a, now, out _, out var sc, out var m, out int i, out float secs)) return false;
            var st = m.steps[i];
            if (st.travel && (a.moving || a.enRoute)) return false; // the planner walks them there along the paths
            var at = MomentBeats.Spot(sc.plan, m, i, secs, out face);
            x = at.x; z = at.y;
            return true;
        }

        static void PoseHook(in PersonActivity a, PegDollView view, ref DollPose pose)
        {
            if (lives.Count == 0 || view == null) { if (view != null) view.LiftBaby(0f); return; }
            Live live = null;
            foreach (var l in lives) if (l.pv != null) { live = l; break; }
            if (live == null || !Find(a, live.pv.Now, out live, out var sc, out var m, out int i, out float secs)) { view.LiftBaby(0f); return; }
            var st = m.steps[i];
            if (st.travel && a.moving) { view.LiftBaby(0f); return; }
            MomentBeats.Apply(ref pose, sc.plan, m, i, secs, out float lift);
            view.LiftBaby(m.key == sc.plan.carrierKey ? lift : 0f);
            if (st.beat == StageBeat.Toss && pose.hit > 0f) HitFx.MarkStagedToss(a.who); // the log lands: HitFx flares the fire for this stroke only
            // The spotter: a "!" over the kid who saw the Keeper, as it points.
            if (st.beat == StageBeat.Point && live.marked.Add(m.key)) ShowMeDirector.Instance?.KnockHeard(a.who);
        }

        /// <summary>#370 for <see cref="RitualBubbleView"/>: the symbols over the staged moments playing now.</summary>
        public static void AddBubbles(ViewTime now, List<(RitualBubble b, bool fest)> into)
        {
            foreach (var l in lives)
            {
                if (l.scene == null || l.over || now.day != l.scene.day) continue;
                l.bubbles ??= RitualBubbles.ForHeldMoment(l.scene);
                foreach (var b in l.bubbles) if (now.hour >= b.start && now.hour < b.end) into.Add((b, false));
            }
        }

        // ---------------------------------------------------------------- cheats (#370)

        static SimCore Sim
        {
            get
            {
                var c = GameClock.Instance;
                return c != null && c.Sim != null && ReferenceEquals(c.Sim, c.ActiveSim) ? c.ActiveSim : null;
            }
        }

        /// <summary>Cheat: a moment of <paramref name="kind"/> plays now (queued at the top, the director's budget lifted). At night the
        /// view clock skips to the morning first, so it is seen.</summary>
        public static string DebugPlayNow(MomentKind kind)
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.MomentsOn && GameClock.Instance != null) GameClock.Instance.config.moments = true;
            if (!sim.MomentsOn) return "moments off";
            if (sim.PlayingMoments().Count > 0) return "a moment is playing: wait, or End staged moments";
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            string skipped = "";
            if (!LifePlanner.HeldMomentFits(pv.Now, LifePlanner.HeldMomentSeconds(kind))) { PeopleView.DebugSetHour(9.5f); skipped = " (skipped to the morning)"; }
            var m = sim.DebugQueueMoment(kind);
            if (m == null) return kind == MomentKind.NewLeader ? "nobody holds the leader's seat" : kind == MomentKind.Wedding ? "no couple to marry" : "nobody to hold it";
            pv.Planner.Invalidate();
            sim.DebugMomentDueNow();
            var stage = HeldMomentStage.Instance;
            if (stage == null) return "no moment stage in the scene";
            var started = stage.Check();
            if (started.Count == 0) return $"nothing started: {stage.LastWhy}";
            var sc = LastScene;
            return sc != null && sc.momentId == started[0].id
                ? $"{kind}: {sc.CastCount} people, {sc.plan.Duration:0} s{skipped}"
                : $"{kind}: started{skipped}; {(LastWhy ?? "waiting for the morning")}";
        }

        /// <summary>Cheat: the Keeper arrives now: a new visit, so the top waiting moment plays as the welcome (a wedding is queued
        /// when nothing waits).</summary>
        public static string DebugKeeperArrives()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.MomentsOn && GameClock.Instance != null) GameClock.Instance.config.moments = true;
            var pv = PeopleView.Find();
            if (pv != null && !LifePlanner.HeldMomentFits(pv.Now, LifePlanner.HeldMomentSeconds(MomentKind.Wedding))) PeopleView.DebugSetHour(9.5f);
            string what = sim.DebugKeeperArrives();
            pv?.Planner?.Invalidate();
            var stage = HeldMomentStage.Instance;
            if (stage == null) return what + "; no moment stage";
            var started = stage.Check();
            return started.Count == 0 ? $"{what}; nothing started: {stage.LastWhy}" : $"{what}; welcome: {started[0].kind}" + (started[0].welcome ? "" : " (not as the welcome)");
        }

        public static string DebugStatus()
        {
            string s = $"staged: {Playing} playing; started {Started}, finished {Finished}, stopped {Stopped}, gave up {GaveUp}; hugs {(BeatFlags.Hugs ? "on" : "off")}";
            if (LastScene != null) s += $"\nlast: {LastScene.kind} #{LastScene.momentId}, {LastScene.CastCount} people, {LastScene.start:0.00}h-{LastScene.end:0.00}h";
            if (!string.IsNullOrEmpty(LastWhy)) s += "\nlast refusal: " + LastWhy;
            return s;
        }

        /// <summary>The debug panel's block (#370).</summary>
        public static string Gui()
        {
            string last = null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wedding now (the Keeper arrived)")) last = DebugPlayNow(MomentKind.Wedding);
            if (GUILayout.Button("Newborn to the glass now")) last = DebugPlayNow(MomentKind.Newborn);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New chief now")) last = DebugPlayNow(MomentKind.NewLeader);
            if (GUILayout.Button("Festival now")) last = DebugPlayNow(MomentKind.Festival);
            if (GUILayout.Button("Keeper arrives now")) last = DebugKeeperArrives();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Hugs " + (BeatFlags.Hugs ? "on" : "off"))) { BeatFlags.Hugs = !BeatFlags.Hugs; last = "hugs " + (BeatFlags.Hugs ? "on" : "off"); }
            if (GUILayout.Button("End staged moments")) last = $"ended {StopAll()}";
            if (GUILayout.Button("Staged: status")) last = DebugStatus();
            GUILayout.EndHorizontal();
            return last;
        }
    }
}
