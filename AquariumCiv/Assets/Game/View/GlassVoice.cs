using AquariumCiv.Audio;
using System.Collections.Generic;
using System.Globalization;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.View
{
    /// <summary>
    /// The people and the glass in the slice (#92; relics-and-glass-voice.md §1, §3): the view side of the planner's glass
    /// behaviours (#92 phase 1, <see cref="LifePlanner"/>'s glass partial). Added to the box at start-up (like the care layer),
    /// it:
    /// <list type="bullet">
    /// <item><b>Front glass.</b> Sets <see cref="LifePlanner.GlassSide"/> from the camera's yaw when a view day turns (never
    /// mid-day: it re-plans the day).</item>
    /// <item><b>Relic moments.</b> When the sim finds a relic while the box is watched (<see cref="SimCore.RelicFound"/>, not
    /// in a catch-up), its moment is staged (<see cref="LifePlanner.StartRelicMoment"/>); an era moment first plays out. The Festival Mask's is the festival's (MomentPlayer, #91). The lines said in a moment show on the
    /// one caption with the speaker's name, ring and speech mark (#327 Option B, <see cref="ShowMeDirector.SayOrWait"/>); a line that
    /// finds the word busy waits its turn.</item>
    /// <item><b>Tapping the glass.</b> A tap on the box outside clean mode and camera mode is the Keeper knocking: the people
    /// react as their stage does (<see cref="KeeperGesture.TapGlass"/>). (#718: nobody knocks back any more.)</item>
    /// <item><b>Relics in the world.</b> <see cref="RelicProps"/>: the Keeper Doll on its post, an heir's Sandal on the
    /// remembering stone.</item>
    /// </list>
    /// Debug: static <c>Debug*</c> methods (the debug panel, `script eval`).
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class GlassVoice : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static GlassVoice Instance { get; private set; }

        /// <summary>Taps closer together than this (real seconds) are one knock of several taps.</summary>
        public const float TapGap = 0.6f;
        /// <summary>Tester run 9: the answer of a tap on bare land.</summary>
        public GroundRipple Ripple { get; } = new();
        /// <summary>A press longer than this, or moving more than <see cref="TapSlop"/> of the screen height, is no tap.</summary>
        public const float TapMaxSeconds = 0.35f, TapSlop = 0.02f;
        /// <summary>A sim that moved on more months than this in a frame was catching up: its relics' moments aren't played.</summary>
        public const int CatchUpMonths = 2;

        BoxView box;
        RelicProps props;
        StashProps stash;
        StallProps stalls;
        SimCore boundSim;
        long lastDay = long.MinValue, lastMonth;
        readonly List<string> pending = new();
        readonly List<RelicLine> lines = new();
        readonly HashSet<(string, int, float)> said = new();
        // taps
        readonly TapGesture tapGesture = new();
        bool pressValid, pressWorld;
        Vector2 pressPos;
        float pressTime, lastTapTime = -10f;
        int taps;

        /// <summary>The relic moment staged last, and its cast (tests, debug).</summary>
        public RelicMomentInfo LastMoment { get; private set; }
        /// <summary>Relics whose moment waits (an era moment playing).</summary>
        public IReadOnlyList<string> Pending => pending;
        /// <summary>The last knock's taps.</summary>
        public int LastTaps { get; private set; }
        public int Knocks { get; private set; }

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : SimLayoutSource.Find()?.Sim;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<GlassVoice>() == null) b.gameObject.AddComponent<GlassVoice>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            if (!TryGetComponent(out props)) props = gameObject.AddComponent<RelicProps>();
            if (!TryGetComponent(out stash)) stash = gameObject.AddComponent<StashProps>();
            if (!TryGetComponent(out stalls)) stalls = gameObject.AddComponent<StallProps>();
            if (!TryGetComponent<RitualBubbleView>(out _)) gameObject.AddComponent<RitualBubbleView>(); // #276: the symbols over a ritual's people
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null; else return;
            Bind(null);
        }

        void Bind(SimCore sim)
        {
            if (sim == boundSim) return;
            if (boundSim != null) boundSim.RelicFound -= OnRelicFound;
            boundSim = sim;
            pending.Clear();
            said.Clear();
            lastDay = long.MinValue;
            if (sim == null) return;
            sim.RelicFound += OnRelicFound;
            lastMonth = sim.State.month;
        }

        void OnRelicFound(Relic r)
        {
            if (r == null || r.id == RelicId.FestivalMask) return; // the festival stages the mask (MomentPlayer, #91)
            if (!pending.Contains(r.id)) pending.Add(r.id);
        }

        static Camera MainCam => Camera.main;
        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();

        void OnDestroy() => Ripple.Dispose();

        void Update()
        {
            Ripple.Step(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            var sim = Sim;
            Bind(sim);
            var people = PeopleView.Find();
            var planner = people != null ? people.Planner : null;
            if (sim == null || planner == null) return;
            var now = people.Now;

            // The front glass follows the camera, at a view day's turn.
            if (now.day != lastDay)
            {
                lastDay = now.day;
                var cam = Cam;
                if (cam != null) planner.GlassSide = LifePlanner.GlassSideFacing(cam.yaw);
            }

            // A catch-up (or a debug skip) found relics nobody watched: no moment (the card's "Show me" echoes it).
            long month = sim.State.month;
            if (month - lastMonth > CatchUpMonths) pending.Clear();
            lastMonth = month;
            PlayPending(sim, planner, now);
            HandleTaps(sim, planner, now);
            SayLines(planner, now);
            if (props != null) props.Sync(sim, planner, Time.unscaledDeltaTime);
            if (stash != null) stash.Sync(planner);
            if (stalls != null) stalls.Sync(sim);
        }

        // ---------------- relic moments ----------------
        void PlayPending(SimCore sim, LifePlanner planner, ViewTime now)
        {
            if (pending.Count == 0) return;
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return; // the era moment first
            for (int i = 0; i < pending.Count; i++)
            {
                var id = pending[i];
                var def = RelicMoments.For(id);
                if (def == null || !sim.HasRelic(id)) { pending.RemoveAt(i--); continue; }
                // A moment with an hour waits for it (any time from then until night).
                if (def.preferredHour >= 0f && (now.hour < def.preferredHour - 0.25f || now.hour > LifeClock.Night + 1f)) continue;
                pending.RemoveAt(i--);
                Play(planner, id, now);
                return; // one at a time
            }
        }

        RelicMomentInfo Play(LifePlanner planner, string id, ViewTime now)
        {
            var info = planner.StartRelicMoment(id, now);
            if (info != null)
            {
                LastMoment = info;
                if (props != null) props.MomentStarted(id);
                Sfx.Play(CueLibrary.Discovery); // #96: a relic's moment begins ("look at this")
                NudgeTo(planner, info.spot);
                Debug.Log($"[GlassVoice] the {SimCore.RelicName(id)} moment: {info.cast.Count} people, {now.hour:0.00}h to {info.end:0.00}h");
            }
            return info;
        }


        /// <summary>A relic moment is happening off-screen or too small to see: a gentle nudge there (user
        /// 2026-10-30, #92). Counted for tests.</summary>
        public int Nudges { get; private set; }

        void NudgeTo(LifePlanner planner, Place place)
        {
            var cam = Cam;
            if (cam == null || !CameraFree() || planner?.Positions == null) return;
            if (!planner.Positions.TryGetPosition(place, out float x, out float z)) return;
            if (cam.NudgeToward(transform.TransformPoint(new Vector3(x, 0f, z)))) Nudges++;
        }

        /// <summary>May the world pull the camera now (a nudge, the fly to an answered gift)? Not while the player is busy with
        /// something else: a sheet, a card, a wipe, a tool, a photo, Show me, a moment, the time-lapse.</summary>
        public static bool CameraFree()
        {
            if (!WorldFree()) return false;
            var sm = ShowMeDirector.Instance;
            if (sm != null && sm.Busy) return false;
            var card = PersonCardView.Instance;
            return card == null || !card.AnyOpen;
        }

        void SayLines(LifePlanner planner, ViewTime now)
        {
            var mp = MomentPlayer.Instance;
            if (mp == null) return;
            planner.RelicLinesAt(now, lines);
            foreach (var l in lines)
            {
                if (!said.Add((l.relicId, l.lineNo, l.start))) continue;
                float secs = Mathf.Clamp(LifeClock.RealSecondsBetween(l.start, l.end), 3f, MomentPlayer.BubbleSeconds);
                // #327 Option B: on the one caption with the speaker's name, ring and mark; busy, it waits its turn (no second system).
                var sm = ShowMeDirector.Instance;
                if (sm != null) { sm.SayOrWait(l.text, l.speaker, secs, LineKind.Relic); continue; }
                string who = l.speaker.IsNotable ? Sim?.FindNotable(l.speaker.notableId)?.name : null;
                mp.Say($"“{l.text}”", secs, who != null && !l.text.Contains(who) ? who : null);
            }
        }

        // ---------------- tapping the glass ----------------
        static int ReadPointer(out Vector2 pos)
        {
            pos = default;
            int n = 0;
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.isInProgress) { if (n == 0) pos = t.position.ReadValue(); n++; }
            if (n > 0) return n;
            var m = Mouse.current;
            if (m == null) return 0;
            pos = m.position.ReadValue();
            return m.leftButton.isPressed ? 1 : (m.rightButton.isPressed || m.middleButton.isPressed) ? 2 : 0;
        }

        /// <summary>Is a press here a knock on the glass? Not on the UI, not in clean mode (that's a wipe), not in camera mode
        /// (a tap brings the UI back), not in shape mode (#85), not with a sheet open or an era moment playing, and on the glass.</summary>
        static bool KnockAllowed(Vector2 pos)
        {
            if (!WorldTapAllowed(pos)) return false;
            var care = CareView.Instance;
            return care == null || care.GlassAt(pos);
        }

        /// <summary>#39: may a tap here reach the world at all (pick a person, close the person card, knock)? The knock's rules
        /// without "on the glass": a doll seen through the open top can be picked.</summary>
        public static bool WorldTapAllowed(Vector2 pos)
        {
            return !UiInput.Over(pos) && WorldFree();
        }

        /// <summary>The world is the player's to touch: no clean or camera mode, no sheet, no shape mode, no replay, no moment.</summary>
        static bool WorldFree()
        {
            var hud = GameHud.Instance;
            if (hud != null && (hud.CleanMode || hud.Hand != GameHud.KitTool.None || hud.CameraMode || hud.SheetOpen || !hud.IsVisible)) return false; // a kit tool in hand owns the taps (build 15: the seed opened a card)
            var shaping = ShapingView.Instance; // #85: in shape mode a tap places the tool
            if (shaping != null && shaping.ShapeMode) return false;
            var tl = TimeLapseView.Find(); // the time-lapse replays the past: the live people can't hear a knock (tester batch 2)
            if (tl != null && tl.Active) return false;
            var bc = BoxCompleteController.Instance;
            if (bc != null && bc.Playing) return false;
            var mp = MomentPlayer.Instance;
            return mp == null || !mp.Playing;
        }

        /// <summary>Tester run 9: the land answers a tap with a soft ring and a small sound, and nothing more.</summary>
        void AnswerGround(Vector2 pos)
        {
            var shaping = ShapingView.Instance;
            if (shaping == null || box == null || !shaping.GroundPoint(pos, out float x, out float z)) return;
            Ripple.Start(box, x, z);
            Sfx.Play(CueLibrary.MessDrop, 7f, 0.22f);
        }

        /// <summary>Debug: a tap's answer on the land under the middle of the screen, or the box's middle.</summary>
        public static string DebugRipple()
        {
            var g = Instance;
            if (g == null || g.box == null) return "no glass voice";
            g.Ripple.Start(g.box, 0f, 0f);
            Sfx.Play(CueLibrary.MessDrop, 7f, 0.22f);
            return $"ground ripple {g.Ripple.Count}";
        }

        void HandleTaps(SimCore sim, LifePlanner planner, ViewTime now)
        {
            int n = ReadPointer(out var pos);
            float t = Time.unscaledTime;
            // #376: a second finger ever joining spoils the press (TapGesture); lifting out of a pinch is never a new press.
            var ev = tapGesture.Update(n, pos.x, pos.y, t, TapSlop * Mathf.Max(1f, Screen.height), TapMaxSeconds);
            if (ev == TapGesture.Event.Began) { pressPos = pos; pressTime = t; pressValid = KnockAllowed(pos); pressWorld = WorldTapAllowed(pos); }
            else if (ev == TapGesture.Event.Cancelled) pressValid = pressWorld = false;
            if (ev == TapGesture.Event.Tap)
            {
                // #39: a tap on a person's doll opens their card (and a tap on the world closes an open card); only a tap
                // that reaches neither is the Keeper's knock.
                bool quick = tapGesture.Quick;
                var cards = PersonCardView.Instance;
                var choice = ChoiceSceneView.Instance; // #224: a touch on one of the two choices answers the people
                if (quick && pressWorld && choice != null && choice.WorldTap(pressPos)) { }
                else if (quick && pressWorld && cards != null && cards.WorldTap(pressPos)) { }
                else
                {
                    if (pressValid && quick) Tap(planner, now);
                    else if (quick && pressWorld) AnswerGround(pressPos); // tester run 9: through the open top there's no glass to knock, and a tap on the land did nothing at all
                }
            }
            if (taps > 0 && t - lastTapTime > TapGap) FinishKnock();
        }

        /// <summary>One tap on the glass: the people look up (by stage) at once; the count goes to the sim when the taps stop.</summary>
        public void Tap(LifePlanner planner, ViewTime now)
        {
            bool first = taps == 0;
            taps++;
            lastTapTime = Time.unscaledTime;
            var sm = ShowMeDirector.Instance;
            var late = first && sm != null ? sm.KnockMarks(planner, now, pressPos) : null; // #161: a ! first, then the bodies; one set of marks a knock
            planner.ReactKnock(now, late);
            Sfx.Play(CueLibrary.TapGlass); // #96: a knuckle on glass
            RabbitView.Instance?.KnockAtScreen(pressPos); // #333: the rabbits close to the knocked spot startle and run (a tap on a rabbit does nothing)
            DeerView.Instance?.KnockAtScreen(pressPos);   // #425: and the deer look up, then bolt
        }

        void FinishKnock()
        {
            LastTaps = taps;
            taps = 0;
            Knocks++;
        }

        // ---------------- debug (the debug panel, `script eval`) ----------------
        public static string Status()
        {
            var g = Instance;
            var pv = PeopleView.Find();
            var p = pv != null ? pv.Planner : null;
            if (g == null || p == null) return "glass: not running";
            return string.Format(CultureInfo.InvariantCulture, "glass: belief {0} {1} · side {2} · waiting {3} · knocks {4} (last {5})",
                p.BeliefStageSeen, p.BeliefLaterSeen ? "later" : "early", p.GlassSide, g.pending.Count == 0 ? "none" : string.Join(",", g.pending),
                g.Knocks, g.LastTaps);
        }

        /// <summary>Debug: give the relic if it isn't found yet, then play its moment now (whatever the hour).</summary>
        public static string DebugPlayMoment(string id)
        {
            var g = Instance; var sim = Sim; var pv = PeopleView.Find();
            if (g == null || sim == null || pv == null || pv.Planner == null) return "glass: not running";
            if (!sim.HasRelic(id) && sim.DebugGiveRelic(id) == null) return "no " + id;
            g.pending.Remove(id);
            pv.Planner.Invalidate();
            var info = g.Play(pv.Planner, id, pv.Now);
            return info == null ? $"nobody can play the {SimCore.RelicName(id)}" : $"the {SimCore.RelicName(id)}: {info.cast.Count} people until {info.end:0.00}h";
        }

        /// <summary>Debug (#276): start a ritual now (the Keeper Doll's by default), whatever the hour: gives the relic if the box
        /// hasn't found it, stages its moment through <see cref="RitualBubbles.StartNow"/>, and the people show their symbols.</summary>
        public static string DebugStartRitual(string id = RelicId.KeeperDoll)
        {
            var g = Instance; var sim = Sim; var pv = PeopleView.Find();
            if (g == null || sim == null || pv == null || pv.Planner == null) return "glass: not running";
            pv.Planner.Invalidate();
            var r = RitualBubbles.StartNow(sim, pv.Planner, pv.Now, id);
            g.pending.Remove(id); // after: giving the relic queues its own moment, and this one is it
            if (r == null) return $"nobody can play the {SimCore.RelicName(id)}";
            g.LastMoment = r.moment;
            if (g.props != null) g.props.MomentStarted(id);
            g.NudgeTo(pv.Planner, r.moment.spot);
            return $"ritual: the {SimCore.RelicName(id)}, {r.moment.cast.Count} people, {r.bubbles.Count} symbol bubbles until {r.moment.end:0.00}h";
        }

        /// <summary>Debug: a gathering at the glass now (stage 1: a step back; later: the kids look for you).</summary>
        public static string DebugGather(int most = 0)
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            int n = pv.Planner.GatherAtGlass(pv.Now, true, most);
            return n == 0 ? "nobody near enough to come" : $"{n} at the glass";
        }

        /// <summary>Debug: the Keeper taps the glass <paramref name="n"/> times (the player's path: the people react).</summary>
        public static string DebugKnock(int n = 3)
        {
            var g = Instance; var sim = Sim; var pv = PeopleView.Find();
            if (g == null || sim == null || pv == null || pv.Planner == null) return "glass: not running";
            for (int i = 0; i < n; i++) g.Tap(pv.Planner, pv.Now);
            g.FinishKnock();
            return $"knocked {n} (belief {pv.Planner.BeliefStageSeen}{(pv.Planner.BeliefLaterSeen ? " later" : "")})";
        }

        /// <summary>Debug: the front glass (0 = -z, 1 = +x, 2 = +z, 3 = -x), or -1 = the camera's now.</summary>
        public static string DebugGlassSide(int side = -1)
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            var cam = Cam;
            pv.Planner.GlassSide = side >= 0 ? side : cam != null ? LifePlanner.GlassSideFacing(cam.yaw) : 0;
            return "front glass: side " + pv.Planner.GlassSide;
        }
    }
}
