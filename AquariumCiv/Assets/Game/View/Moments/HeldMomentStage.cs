using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>#369: one held moment being staged in the box. <see cref="Done"/> is polled; <see cref="Stop"/> ends it early (the box
    /// changed, the app left).</summary>
    public sealed class MomentStaging
    {
        /// <summary>True once the scene is over (it was seen).</summary>
        public Func<bool> Done;
        /// <summary>Optional: end it now (nothing else is promised).</summary>
        public Action Stop;
    }

    /// <summary>#369: stages one moment of a kind now, or returns null when it can't (nobody to play it, the place is gone): the
    /// moment then becomes a card. C3 (#370) registers the real scenes with <see cref="HeldMomentStage.Register"/>.</summary>
    public delegate MomentStaging MomentStager(HeldMoment moment, SimCore sim);

    /// <summary>
    /// #369: the view side of the moment director (visit-loop.md 2.1-2.2): the play contract between the sim's queue and whatever
    /// stages a ceremony in the box.
    /// <list type="number">
    /// <item>Every <see cref="CheckSeconds"/>, while the box on screen is the living one and calm (no sheet, Show me, choice scene,
    /// evening talk, era moment, time-lapse, first-run word, shaping or wipe), it asks <see cref="SimCore.StartDueMoments"/>: what
    /// plays now (the welcome on an open, then the budget per watched minute; a busy day hands over 2 or 3).</item>
    /// <item>Each is handed to the stager registered for its kind (<see cref="Register"/>), else the placeholder: the "Show me"
    /// echo the box already has for that line (<see cref="EchoScenes"/>, played where the people are, no camera grab).</item>
    /// <item>When the staging says <see cref="MomentStaging.Done"/>, <see cref="SimCore.FinishMoment"/>(played). A stager that
    /// returns null: FinishMoment(not played), and the moment is told as a card instead, so nothing is lost.</item>
    /// </list>
    /// The sim never plays anything while away; this only ever runs while watched.
    /// </summary>
    [DefaultExecutionOrder(30)]
    public class HeldMomentStage : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static HeldMomentStage Instance { get; private set; }
        /// <summary>Real seconds between two looks at the queue.</summary>
        public const float CheckSeconds = 1f;

        static readonly Dictionary<MomentKind, MomentStager> stagers = new();

        /// <summary>C3: the scene for a kind (null puts the placeholder back).</summary>
        public static void Register(MomentKind kind, MomentStager stager)
        {
            if (stager == null) stagers.Remove(kind); else stagers[kind] = stager;
        }

        /// <summary>The moments staged now, by id.</summary>
        readonly Dictionary<int, MomentStaging> playing = new();
        float sinceCheck;
        SimCore boundSim;

        /// <summary>The last moments handed over (tests, the panel).</summary>
        public IReadOnlyList<HeldMoment> LastStarted => lastStarted;
        readonly List<HeldMoment> lastStarted = new();
        /// <summary>Why the last look started nothing (the view's reason, or the director's; null = it started some).</summary>
        public string LastWhy { get; private set; } = "";

        static SimCore Sim
        {
            get
            {
                var c = GameClock.Instance;
                // Only the living box: a visited museum's people don't hold ceremonies.
                return c != null && c.Sim != null && ReferenceEquals(c.Sim, c.ActiveSim) ? c.ActiveSim : null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<HeldMomentStage>() == null) b.gameObject.AddComponent<HeldMomentStage>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            StopAll();
        }

        void StopAll()
        {
            foreach (var s in playing.Values) { try { s?.Stop?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); } }
            playing.Clear();
        }

        void Update()
        {
            sinceCheck += Time.unscaledDeltaTime;
            if (sinceCheck < CheckSeconds) return;
            sinceCheck = 0f;
            Check();
        }

        /// <summary>One look: finish what is over, then start what is due if the box is calm. Returns what started (tests, cheats).</summary>
        public List<HeldMoment> Check()
        {
            lastStarted.Clear();
            var sim = Sim;
            if (!ReferenceEquals(sim, boundSim)) { StopAll(); boundSim = sim; } // another box: what played there is over (the sim gives it back on its next visit)
            if (sim == null || !sim.MomentsOn)
            {
                LastWhy = sim == null ? "no living box on screen" : "moments off";
                if (sim != null && BusyReason() == null) TryStageChoice(sim);
                return lastStarted;
            }

            foreach (var m in sim.PlayingMoments())
            {
                if (!playing.TryGetValue(m.id, out var st) || st == null) { sim.FinishMoment(m.id, true); continue; } // lost track of it: it was handed over while watched
                bool done;
                try { done = st.Done == null || st.Done(); }
                catch (Exception ex) { Debug.LogException(ex); done = true; }
                if (done) { playing.Remove(m.id); sim.FinishMoment(m.id, true); }
            }
            if (sim.PlayingMoments().Count > 0) { LastWhy = "a moment is playing"; return lastStarted; }

            string busy = BusyReason();
            if (busy != null) { LastWhy = busy; return lastStarted; }
            var started = StartNow(sim);
            if (started.Count == 0) TryStageChoice(sim); // visit-loop 2.4: after the welcome moment, the held choice is staged in the box
            return started;
        }

        int choiceTries;

        /// <summary>Visit-loop 2.4 (the fail item of tester run 1): a choice waiting when the app opens is staged in the box first, the
        /// choice scene with its cast, as Show me on its card does, after the welcome moment (never over a moment playing or due, never
        /// over an open card stack). The card stays available. Three failed tries and it is left to the card.</summary>
        void TryStageChoice(SimCore sim)
        {
            if (!sim.ChoiceStageDue) { choiceTries = 0; return; }
            var cv = ChoiceSceneView.Instance;
            if (cv == null || cv.State != ChoiceSceneView.Phase.Idle) return;
            if (sim.PlayingMoments().Count > 0 || sim.MomentBeforeChoice()) { LastWhy = "the welcome moment first"; return; }
            var cards = CardStackView.Instance;
            if (cards != null && cards.StackOpen) { LastWhy = "the card stack is open"; return; }
            var v = sim.OpenDilemmaView;
            if (v != null && cv.Offer(v)) { sim.ChoiceStaged(); choiceTries = 0; LastWhy = "the held choice is staged"; return; }
            if (++choiceTries >= 3) { sim.ChoiceStaged(); choiceTries = 0; LastWhy = "the choice could not be staged: its card stays"; }
        }

        List<HeldMoment> StartNow(SimCore sim)
        {
            var due = sim.StartDueMoments();
            LastWhy = due.Count == 0 ? sim.MomentWaitReason() : null;
            foreach (var m in due)
            {
                MomentStaging st = null;
                try { st = (stagers.TryGetValue(m.kind, out var f) && f != null ? f : Placeholder)(m, sim); }
                catch (Exception ex) { Debug.LogException(ex); }
                if (st == null) { sim.FinishMoment(m.id, false); continue; } // can't be staged now: told as a card
                playing[m.id] = st;
                lastStarted.Add(m);
            }
            return lastStarted;
        }

        /// <summary>The box is busy with something the player is looking at: the director waits for a natural break (research 16
        /// §4.2, "Peak Fade"). Null = calm.</summary>
        public static string BusyReason()
        {
            var c = GameClock.Instance;
            if (c != null && c.Held) return "the era moment";
            var hud = GameHud.Instance;
            if (hud != null && hud.SheetOpen) return "a sheet is open";
            var sm = ShowMeDirector.Instance;
            if (sm != null && sm.Busy) return "show me";
            if (ChoiceSceneView.Instance != null && ChoiceSceneView.Instance.Holding) return "the people ask the Keeper";
            if (FirstRunView.Instance != null && FirstRunView.Instance.HasTheWord) return "the first run";
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return "the era moment";
            var et = EveningTalkView.Instance;
            if (et != null && et.Open) return "the evening talk";
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return "the time-lapse";
            var bc = BoxCompleteController.Instance;
            if (bc != null && (bc.Playing || bc.ShelfOpen)) return "the last moment";
            var sv = ShapingView.Instance;
            if (sv != null && sv.ShapeMode) return "shape mode";
            var care = CareView.Instance;
            if (care != null && care.IsWiping) return "a wipe";
            return null;
        }

        // ---------- the placeholder: what the box shows today for that line ----------

        /// <summary>The "Show me" echo of the moment's line, staged where the people are (no camera move): a wedding's couple side
        /// by side with a flower, a parent holding the baby up, the new leader speaking at the seat, the people at a callback's
        /// thing. A line with no echo (a festival is box-wide, a line older than the echo's limit) can't be staged: null (a card).
        /// C3 (#370) replaces these with the staged scenes.</summary>
        internal static MomentStaging Placeholder(HeldMoment m, SimCore sim)
        {
            var pv = PeopleView.Find();
            var planner = pv != null ? pv.Planner : null;
            var entry = sim.State.chronicle.FirstOrDefault(e => e.id == m.entryId);
            if (planner == null || entry == null) return null;
            var echo = EchoScenes.For(sim, entry);
            if (echo == null || !echo.Plays) return null;
            var info = planner.StageEcho(echo, pv.Now);
            if (info == null) return null;
            var list = new List<RelicMomentInfo>();
            return new MomentStaging
            {
                Done = () =>
                {
                    var now = pv != null ? pv.Now : default;
                    return pv == null || pv.Planner != planner || now.day != info.day || now.hour >= info.end || planner.ActiveEchoes(now, list) == 0;
                },
            };
        }

        // ---------- debug (the #369 cheats) ----------

        /// <summary>Cheat: one moment of each kind waits now.</summary>
        public static string DebugQueueOneOfEach()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.MomentsOn) return "moments off";
            var made = sim.DebugQueueOneOfEach();
            return made.Count == 0 ? "nobody to hold a moment for" : $"queued {made.Count}: {string.Join(", ", made.Select(x => x.kind))}";
        }

        /// <summary>Cheat: the next waiting moment plays now (the budget, the rest and the visit's cap are lifted; the box must be calm).</summary>
        public static string DebugPlayNextNow(bool busyDay = false)
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (!sim.MomentsOn) return "moments off";
            if (busyDay) sim.DebugBusyDay(); else sim.DebugMomentDueNow();
            var stage = Instance;
            if (stage == null) return "no moment stage in the scene";
            var started = stage.Check();
            return started.Count == 0 ? $"nothing started: {stage.LastWhy}" : $"playing {string.Join(", ", started.Select(x => $"#{x.id} {x.kind}"))}";
        }

        /// <summary>Cheat (visit loop 2.4): the open choice counts as held on an open, and is staged in the box now if the box is calm.</summary>
        public static string DebugStageHeldChoice()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            if (sim.OpenDilemma == null) return "no choice is open";
            sim.DebugHoldChoiceForOpen();
            var stage = Instance;
            if (stage == null) return "no moment stage in the scene";
            stage.Check();
            return sim.ChoiceStageDue ? "waiting: " + stage.LastWhy : "staged (or left to its card)";
        }

        /// <summary>Cheat: the queue and the next due time.</summary>
        public static string DebugStatus()
        {
            var sim = Sim;
            if (sim == null) return "no living box";
            string view = Instance == null ? "no moment stage" : BusyReason() is string b ? "the view waits: " + b : "the view is calm";
            return sim.MomentQueueStatus() + "\n" + view;
        }

        /// <summary>Cheat: the moment queue on or off for this box (off: nothing queues; what waits stays).</summary>
        public static string DebugToggle()
        {
            var c = GameClock.Instance;
            if (c == null) return "no clock";
            c.config.moments = !c.config.moments;
            return "moments " + (c.config.moments ? "on" : "off");
        }
    }
}
