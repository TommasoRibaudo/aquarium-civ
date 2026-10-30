using System.Collections.Generic;
using System.Globalization;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
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
    /// in a catch-up), its moment is staged (<see cref="LifePlanner.StartRelicMoment"/>); the Lens waits for dusk, an era moment
    /// first plays out. The Festival Mask's is the festival's (MomentPlayer, #91). The lines said in a moment show in the era
    /// moment's speech bubble (<see cref="MomentPlayer.Say"/>).</item>
    /// <item><b>Tapping the glass.</b> A tap on the box outside clean mode and camera mode is the Keeper knocking: the people
    /// react as their stage does (<see cref="KeeperGesture.TapGlass"/>), and once the taps stop the sim is told how many
    /// (<see cref="SimCore.KeeperTapped"/>); in stage 3 with the Lens a scholar walks over and knocks back as often
    /// (<see cref="LifePlanner.TapBack"/>).</item>
    /// <item><b>Relics in the world.</b> <see cref="RelicProps"/>: the Sky Crumb, the Keeper Doll on its post, the Lens on its
    /// tripod, an heir's Sandal on the remembering stone.</item>
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
        /// <summary>A press longer than this, or moving more than <see cref="TapSlop"/> of the screen height, is no tap.</summary>
        public const float TapMaxSeconds = 0.35f, TapSlop = 0.02f;
        /// <summary>A sim that moved on more months than this in a frame was catching up: its relics' moments aren't played.</summary>
        public const int CatchUpMonths = 2;

        BoxView box;
        RelicProps props;
        SimCore boundSim;
        long lastDay = long.MinValue, lastMonth;
        readonly List<string> pending = new();
        readonly List<RelicLine> lines = new();
        readonly HashSet<(string, int, float)> said = new();
        // taps
        bool pressDown, pressValid;
        Vector2 pressPos;
        float pressTime, lastTapTime = -10f;
        int taps;

        /// <summary>The relic moment staged last, and its cast (tests, debug).</summary>
        public RelicMomentInfo LastMoment { get; private set; }
        /// <summary>Relics whose moment waits (the Lens for dusk, an era moment playing).</summary>
        public IReadOnlyList<string> Pending => pending;
        /// <summary>The last knock the sim was told of (taps) and who tapped back (-1 = nobody).</summary>
        public int LastTaps { get; private set; }
        public int LastTapBack { get; private set; } = -1;
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

        void Update()
        {
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
                // The Lens at dusk (§1.5): it waits for its hour (any time from then until night).
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
                Debug.Log($"[GlassVoice] the {SimCore.RelicName(id)} moment: {info.cast.Count} people, {now.hour:0.00}h to {info.end:0.00}h");
            }
            return info;
        }

        void SayLines(LifePlanner planner, ViewTime now)
        {
            var mp = MomentPlayer.Instance;
            if (mp == null) return;
            planner.RelicLinesAt(now, lines);
            foreach (var l in lines)
            {
                if (!said.Add((l.relicId, l.lineNo, l.start))) continue;
                string who = l.speaker.IsNotable ? Sim?.FindNotable(l.speaker.notableId)?.name : null;
                string text = who != null && !l.text.Contains(who) ? $"{who}: “{l.text}”" : $"“{l.text}”";
                float secs = Mathf.Clamp(LifeClock.RealSecondsBetween(l.start, l.end), 3f, MomentPlayer.BubbleSeconds);
                mp.Say(text, secs);
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
        /// (a tap brings the UI back), not with a sheet open or an era moment playing, and on the glass.</summary>
        static bool KnockAllowed(Vector2 pos)
        {
            if (UiInput.Over(pos)) return false;
            var hud = GameHud.Instance;
            if (hud != null && (hud.CleanMode || hud.CameraMode || hud.SheetOpen || !hud.IsVisible)) return false;
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return false;
            var care = CareView.Instance;
            return care == null || care.GlassAt(pos);
        }

        void HandleTaps(SimCore sim, LifePlanner planner, ViewTime now)
        {
            int n = ReadPointer(out var pos);
            float t = Time.unscaledTime;
            if (n == 1 && !pressDown) { pressDown = true; pressPos = pos; pressTime = t; pressValid = KnockAllowed(pos); }
            else if (n >= 2) pressValid = false;
            else if (n == 1 && (pos - pressPos).magnitude > TapSlop * Mathf.Max(1f, Screen.height)) pressValid = false;
            if (n == 0 && pressDown)
            {
                pressDown = false;
                if (pressValid && t - pressTime <= TapMaxSeconds) Tap(planner, now);
            }
            if (taps > 0 && t - lastTapTime > TapGap) FinishKnock(sim, planner, now);
        }

        /// <summary>One tap on the glass: the people look up (by stage) at once; the count goes to the sim when the taps stop.</summary>
        public void Tap(LifePlanner planner, ViewTime now)
        {
            taps++;
            lastTapTime = Time.unscaledTime;
            planner.React(KeeperGesture.TapGlass, now);
            if (TryGetComponent(out CareSound s)) s.Chime(0.15f); // a soft knock (placeholder until the glass's own sound)
        }

        void FinishKnock(SimCore sim, LifePlanner planner, ViewTime now)
        {
            int n = taps;
            taps = 0;
            Knocks++;
            LastTaps = n;
            int id = sim.KeeperTapped(n); // stage 3 with the Lens: a scholar taps back (and the first one makes a card)
            LastTapBack = id;
            if (id >= 0) planner.TapBack(now, id, n);
        }

        // ---------------- debug (the debug panel, `script eval`) ----------------
        public static string Status()
        {
            var g = Instance;
            var pv = PeopleView.Find();
            var p = pv != null ? pv.Planner : null;
            if (g == null || p == null) return "glass: not running";
            return string.Format(CultureInfo.InvariantCulture, "glass: belief {0} {1} · side {2} · waiting {3} · knocks {4} (last {5}, back {6})",
                p.BeliefStageSeen, p.BeliefLaterSeen ? "later" : "early", p.GlassSide, g.pending.Count == 0 ? "none" : string.Join(",", g.pending),
                g.Knocks, g.LastTaps, g.LastTapBack);
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

        /// <summary>Debug: a gathering at the glass now (stage 1: a step back; later: the kids look for you).</summary>
        public static string DebugGather()
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return "no people";
            int n = pv.Planner.GatherAtGlass(pv.Now, true);
            return n == 0 ? "nobody near enough to come" : $"{n} at the glass";
        }

        /// <summary>Debug: the Keeper taps the glass <paramref name="n"/> times (the player's path: reactions, then the sim and
        /// the tap-back if the stage has one).</summary>
        public static string DebugKnock(int n = 3)
        {
            var g = Instance; var sim = Sim; var pv = PeopleView.Find();
            if (g == null || sim == null || pv == null || pv.Planner == null) return "glass: not running";
            for (int i = 0; i < n; i++) g.Tap(pv.Planner, pv.Now);
            g.FinishKnock(sim, pv.Planner, pv.Now);
            return g.LastTapBack >= 0 ? $"{sim.FindNotable(g.LastTapBack)?.FullName} taps back {n}" : $"knocked {n}: nobody taps back (belief {pv.Planner.BeliefStageSeen}{(pv.Planner.BeliefLaterSeen ? " later" : "")})";
        }

        /// <summary>Debug: a scholar (or any adult) taps back now, whatever the stage.</summary>
        public static string DebugTapBack(int n = 3)
        {
            var sim = Sim; var pv = PeopleView.Find();
            if (sim == null || pv == null || pv.Planner == null) return "no people";
            int id = sim.DebugTapBack(n);
            if (id < 0 || !pv.Planner.TapBack(pv.Now, id, n)) return "nobody to tap back";
            return $"{sim.FindNotable(id)?.FullName} taps back {n}";
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
