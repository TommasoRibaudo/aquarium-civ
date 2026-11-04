using AquariumCiv.Audio;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using T = AquariumCiv.View.Shaping.ShapingTuning;

namespace AquariumCiv.View
{
    /// <summary>
    /// The Keeper's shapings in the world (#85; game-definition §6, research 10 "Decision"): the rain, the grove, the
    /// channel and the herd drawn in the style kit from <see cref="ShapingShow"/>'s looks every frame, the people's answer
    /// shown (the planner stages it, a glow ring on the new building, a world label and the chronicle line as a bubble),
    /// and a take-back that plays each in reverse.
    /// <para>Pick a tool (the tray, a wish, the debug panel) and shape mode is on. A tap on the ground places it; a one-finger drag moves
    /// the camera and places nothing (#145), except the two that are the tool's own: a wish's spot and the channel's drag
    /// (<see cref="ICameraGate"/>). A herd goes in with a real flick. After a placing the gift has a "Take back" chip for a while (#149).</para>
    /// Added to the box at start-up like <see cref="CareView"/>.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public class ShapingView : MonoBehaviour, ICameraGate
    {
        public static bool AutoCreate = true;
        public static ShapingView Instance { get; private set; }

        /// <summary>A press that moves less than this share of the screen height and lifts within <see cref="TapMaxSeconds"/> is a tap.</summary>
        public const float TapSlop = 0.025f, TapMaxSeconds = 0.6f;
        public const float CloudHeight = 0.85f;
        /// <summary>Animals a little larger than life next to the dolls (Choice 5's big props): a herd reads at phone size.</summary>
        public const float AnimalScale = 1.35f, FishScale = 1.7f;
        const int MaxChips = 4;
        /// <summary>A herd's flick lifts within this many seconds, and the camera waits that long before it orbits with the herd in hand.</summary>
        public const float FlickSeconds = 0.25f;
        /// <summary>...over at least this share of the screen height, and at least this fast (screen heights a second).</summary>
        public const float FlickMinTravel = 0.04f, FlickMinSpeed = 0.4f;

        ShapingShow show = new();
        readonly Dictionary<int, Vis> vis = new();
        readonly List<ShapingEvent> events = new();
        SimCore boundSim;
        BoxView box;
        BoxCamera cam;
        GameHud hud;
        Transform root;
        Material fxMat;
        bool overlaysRefreshed;

        /// <summary>The tracker (tests, the debug panel, #84's tray).</summary>
        public ShapingShow Show => show;
        /// <summary>#147: how long the newest shaping's arrival plays, real seconds (0 = none).</summary>
        public float LastArrivalSeconds => show.Entries.Count > 0 ? ShapingShow.ArrivalSeconds(show.Entries[show.Entries.Count - 1].footprint) : 0f;
        /// <summary>The tray's model. Until #84 gives it a screen only a wish arms it (<see cref="ArmWish"/>).</summary>
        public ShapingTray Tray { get; } = new();
        /// <summary>A wish card's Give put the tool in hand at the wish's spot.</summary>
        public bool WishArmed => Tray.HasPending;
        /// <summary>The tool in the Keeper's hand (a <see cref="SimCore.NudgeKind"/>), or null: shape mode off.</summary>
        public string Tool { get; private set; }
        /// <summary>The tool in hand came from the tray (or a wish): it spends a charge and shows the banner and Cancel. Off for
        /// the debug panel's free shape mode and the first run's rain button.</summary>
        public bool RealTool { get; private set; }
        public bool ShapeMode => Tool != null;
        /// <summary>Debug: place without spending a charge or minding the era lock and caps.</summary>
        public bool Free = true;
        public string LastResult { get; private set; } = "";
        /// <summary>Everything that happened (tests), oldest first, at most 64.</summary>
        public readonly List<ShapingEvent> EventLog = new();
        public Transform Root => root;

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        Camera Cam => cam != null ? cam.GetComponent<Camera>() : Camera.main;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<ShapingView>() == null) b.gameObject.AddComponent<ShapingView>();
        }

        // ================================================================== lifecycle

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            SimLayout.KeeperOverlays = true; // the layout leaves the Keeper's groves and herds to us
            overlaysRefreshed = false;
            Hook();
            BuildUi();
            UiInput.Add(IsOverUi);
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            cam?.RemoveGate(this);
            if (hud != null) { hud.CleanModeChanged -= OnCleanMode; hud.SheetChanged -= OnSheet; }
            UiInput.Remove(IsOverUi);
            SimLayout.KeeperOverlays = false;
            // Not while the box itself is being deactivated or its scene unloads: BoxView can't reparent then, and it goes anyway.
            if (box != null && Application.isPlaying && box.isActiveAndEnabled && gameObject.activeInHierarchy && gameObject.scene.isLoaded) box.Refresh();
            ClearVisuals();
            if (root != null) Destroy(root.gameObject);
            if (uiGo != null) Destroy(uiGo);
        }

        void Hook()
        {
            cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();
            cam?.AddGate(this);
            var h = GameHud.Instance;
            if (h != hud)
            {
                if (hud != null) { hud.CleanModeChanged -= OnCleanMode; hud.SheetChanged -= OnSheet; }
                hud = h;
                if (hud != null) { hud.CleanModeChanged += OnCleanMode; hud.SheetChanged += OnSheet; }
            }
        }

        void OnCleanMode(bool on) { if (on) SetTool(null); }

        // ================================================================== the calls

        /// <summary>Pick a tool (shape mode on) or null (off). Clean mode goes off; a hint says what to do, unless <paramref name="quiet"/>:
        /// the first rain has its own hint, and one message at a time (question 27).</summary>
        public void SetTool(string kind, bool quiet = false)
        {
            if (kind != null && System.Array.IndexOf(SimCore.SliceTools, kind) < 0) kind = null;
            if (toolHint != null) { hud?.WithdrawToast(toolHint); toolHint = null; } // waiting or on screen: it no longer applies
            Tray.Close();
            Tool = kind;
            RealTool = false;
            restKnown = false;
            if (kind == null) return;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            var sim = Sim;
            string name = sim != null ? sim.ShapingToolName(kind) : kind;
            if (quiet) return;
            toolHint = $"Tap the ground to place the {name}.";
            hud?.Toast(toolHint, 4f);
        }

        string toolHint;

        /// <summary>A wish card's Give (#75): shape mode with the wish's tool, pre-armed at the wished spot, with a banner and a
        /// Cancel. A tap on the ground gives it, a drag moves the spot first. False, with the reason as a toast, if it can't be.</summary>
        public bool ArmWish(int wishId)
        {
            var sim = Sim;
            if (sim == null) return false;
            SetTool(null);
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            if (!Tray.ArmForWish(sim, wishId))
            {
                var f = Tray.Feedback;
                LastResult = f.text ?? "";
                if (!string.IsNullOrEmpty(f.cue)) Sfx.Play(f.cue);
                hud?.Toast(LastResult, 3f);
                return false;
            }
            Tool = Tray.Armed;
            RealTool = true;
            return true;
        }

        /// <summary>The tray's pick (#84): the tool in hand with shaping mode on, spending a charge when it lands. A tool that
        /// can't be used says why (<see cref="ShapingTray.ReasonText"/>) and isn't picked.</summary>
        public bool ArmTool(string kind)
        {
            var sim = Sim;
            if (sim == null) return false;
            SetTool(null);
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            if (!Tray.Select(sim, kind))
            {
                Say(Tray.Feedback);
                return false;
            }
            Tool = kind;
            RealTool = true;
            return true;
        }

        void Say(TrayFeedback f)
        {
            LastResult = f.text ?? "";
            if (f.kind == TrayFeedbackKind.Failed && !string.IsNullOrEmpty(f.cue)) Sfx.Play(f.cue);
            hud?.Toast(LastResult, 3f);
        }

        /// <summary>Put the tool in hand down (the banner's Cancel). Nothing is spent; a wish stays open.</summary>
        public void CancelTool() => SetTool(null);

        /// <summary>Put the wish's tool down. Nothing is spent; the wish stays open.</summary>
        public void CancelWish() => CancelTool();

        void ConfirmWish()
        {
            var sim = Sim;
            if (sim == null) return;
            var r = Tray.Confirm(sim, show);
            var f = Tray.Feedback;
            LastResult = f.text ?? "";
            if (!r.Ok && !string.IsNullOrEmpty(f.cue)) Sfx.Play(f.cue); // on success the placing's own cue plays from the show
            hud?.Toast(LastResult, 3f);
            if (Tray.Mode == TrayMode.Armed) return; // the ground refused it: still in hand
            SetTool(null);
            Changed();
        }

        /// <summary>Place the tool in hand (or <paramref name="kind"/>) at a box point. Free in debug; else it spends a charge
        /// and says why when it can't.</summary>
        public string PlaceAt(float x, float z, string kind = null, bool? free = null)
        {
            var sim = Sim;
            kind ??= Tool;
            if (sim == null || kind == null) return LastResult = "no sim or no tool";
            bool isFree = free ?? (Free && !RealTool);
            if (isFree)
            {
                var n = show.DebugPlace(sim, kind, x, z);
                LastResult = n == null ? "no such tool" : $"{kind} #{n.id} at ({x:0.00}, {z:0.00})";
            }
            else if (RealTool && kind == Tool && Tray.Mode == TrayMode.Armed)
            {
                // The tray's tool: the tray places it, says what happened, and the tool leaves the hand.
                var r = Tray.PlaceAt(sim, show, x, z);
                Say(Tray.Feedback);
                restX = x; restZ = z; restKnown = true;
                if (Tray.Mode == TrayMode.Armed) return LastResult; // the ground refused it: still in hand, with the mark on the spot
                SetTool(null);
                if (r.Ok) Changed();
                return LastResult;
            }
            else
            {
                var r = show.Place(sim, kind, x, z);
                if (!r.Ok)
                {
                    LastResult = ShapingTray.ReasonText(kind, r.fail, sim.ShapingStanding(kind));
                    Sfx.Play(ShapingShow.FailCue); // soft: "not now", the text says why
                    hud?.Toast(LastResult, 3f);
                    return LastResult;
                }
                LastResult = $"{kind} #{r.nudge.id} at ({x:0.00}, {z:0.00})";
            }
            Changed();
            return LastResult;
        }


        /// <summary>Take a shaping back (the chip). False once the people have used it.</summary>
        public bool TakeBack(int nudgeId)
        {
            var sim = Sim;
            if (sim == null) return false;
            bool ok = Tray.TakeBack(sim, show, nudgeId);
            if (!ok) { Say(Tray.Feedback); return false; }
            LastResult = $"took back #{nudgeId}";
            hud?.Toast(Tray.Feedback.text, 3f);
            Changed();
            return true;
        }

        /// <summary>Debug: the people answer this shaping now (−1 = the newest one waiting).</summary>
        public bool SkipToAnswer(int nudgeId = -1)
        {
            var sim = Sim;
            if (sim == null) return false;
            if (nudgeId < 0)
            {
                var n = sim.State.nudges.FindLast(x => !x.credited && System.Array.IndexOf(SimCore.SliceTools, x.kind) >= 0);
                if (n == null) { LastResult = "nothing waiting for an answer"; return false; }
                nudgeId = n.id;
            }
            if (!show.SkipToAnswer(sim, nudgeId)) { LastResult = "already answered"; return false; }
            LastResult = $"people answered #{nudgeId}";
            Changed();
            return true;
        }

        void Changed()
        {
            if (box != null) box.Refresh();
            if (GameClock.Instance != null && GameClock.Instance.Sim != null) GameClock.Instance.Save();
        }

        // ---------- `script eval` / tests ----------
        public static string DebugPlace(string kind, float x, float z, bool free = true) => Instance == null ? "no ShapingView" : Instance.PlaceAt(x, z, kind, free);
        public static string DebugTakeBack(int id = -1)
        {
            var v = Instance; if (v == null) return "no ShapingView";
            if (id < 0) { for (int i = v.show.Entries.Count - 1; i >= 0; i--) if (v.Sim != null && v.Sim.CanTakeBack(v.show.Entries[i].nudgeId)) { id = v.show.Entries[i].nudgeId; break; } }
            return v.TakeBack(id) ? v.LastResult : "nothing to take back";
        }
        public static string DebugSkip(int id = -1) { var v = Instance; if (v == null) return "no ShapingView"; v.SkipToAnswer(id); return v.LastResult; }

        /// <summary>Debug: the newest gift's Take back chip has this many seconds left (0 = gone now).</summary>
        public static string DebugChipLeft(float seconds)
        {
            var v = Instance; if (v == null) return "no ShapingView";
            var es = v.show.Entries;
            return es.Count > 0 && v.show.DebugChipLeft(es[es.Count - 1].nudgeId, seconds) ? $"chip: {seconds:0} s left" : "no gift out there";
        }

        /// <summary>Debug: a real tool in hand, with the strip, Cancel and the mark on the ground.</summary>
        public static string DebugHand(string kind)
        {
            var v = Instance; if (v == null) return "no ShapingView";
            v.Sim?.DebugRefillShaping();
            return v.ArmTool(kind) ? $"{kind} in hand" : v.LastResult;
        }

        /// <summary>Debug: the refused state: a grove in hand, tapped on the pond (the mark, the cross, the ground's reason).</summary>
        public static string DebugRefused()
        {
            var v = Instance; var sim = v?.Sim; var map = sim?.Settlement?.Map;
            if (map == null) return "no map";
            float sx = 0f, sz = 0f;
            int n = 0;
            for (int c = 0; c < SiteMap.N; c++) if (map.Water(c)) { sx += SiteMap.CXc(c); sz += SiteMap.CZc(c); n++; }
            if (n == 0) return "no water in this box";
            DebugHand(SimCore.NudgeKind.Grove);
            return v.PlaceAt(sx / n, sz / n); // the middle of the pond
        }

        /// <summary>Debug: the people react to the last gift not answered yet, now (#142): the same staging a landing gift gets.
        /// While one plays this says so; with everyone asleep it says that, and nobody is woken.</summary>
        public static string DebugReact()
        {
            var sim = Instance?.Sim;
            var pv = PeopleView.Find();
            if (sim == null || pv == null || pv.Planner == null) return "no ShapingView";
            for (int i = sim.State.nudges.Count - 1; i >= 0; i--)
            {
                var n = sim.State.nudges[i];
                if (n.credited || System.Array.IndexOf(SimCore.SliceTools, n.kind) < 0) continue;
                var info = pv.Planner.StartShapingReaction(n.id, pv.Now);
                return info != null ? $"people react to the {n.kind}" : "nobody is up to see it: the reaction waits";
            }
            return "no gift waiting: place one first";
        }

        // ================================================================== the camera gate and the tap

        bool Usable() => ShapeMode && UiUsable();

        /// <summary>The world is free for the shaping UI (the Take back chips, the tool): no sheet, clean mode, photo mode, moment or replay.</summary>
        bool UiUsable()
        {
            if (box == null || Sim == null) return false;
            if (hud != null && (hud.CleanMode || hud.SheetOpen || hud.ToolsHidden || hud.CameraMode)) return false;
            if (BoxCompleteController.ShelfIsOpen) return false; // tester run 7: over the shelf they pointed at nothing
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return false;
            var tl = TimeLapseView.Find();
            return tl == null || !tl.Active;
        }

        /// <summary>The tool keeps the drag from the camera only where the drag is what it does: a wish's spot and the channel. Every other
        /// tool leaves it to the camera, so moving around never places anything (#145).</summary>
        public bool ClaimsPress(Vector2 screenPos) => Usable() && RealTool && (WishArmed || StrokeTool) && !UiInput.Over(screenPos) && GroundPoint(screenPos, out _, out _);

        /// <summary>A herd's flick comes in from the box's edge, so it may start off the land; everything else starts on the ground.</summary>
        bool ToolPress(Vector2 screenPos) => Usable() && !UiInput.Over(screenPos) && (Flicked || GroundPoint(screenPos, out _, out _));

        bool pressDown, pressOk, pressDrag, pressStroke;
        Vector2 pressPos, lastPos;
        float pressTime;

        static int ReadPointers(out Vector2 a)
        {
            a = default;
            int n = 0;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.isInProgress) continue;
                    if (n == 0) a = t.position.ReadValue();
                    n++;
                }
                if (n > 0) return n;
            }
            var m = Mouse.current;
            if (m == null) return 0;
            a = m.position.ReadValue();
            return m.leftButton.isPressed ? 1 : (m.rightButton.isPressed || m.middleButton.isPressed) ? 2 : 0;
        }

        /// <summary>The tray's channel: a drag ends in one point, where the finger lifts.</summary>
        bool StrokeTool => RealTool && !WishArmed && Tool != null && ShapingTray.GestureOf(Tool) == ToolGesture.Drag;

        /// <summary>The tray's herd: only a real flick places it (<see cref="IsFlick"/>).</summary>
        bool Flicked => RealTool && !WishArmed && Tool != null && ShapingTray.GestureOf(Tool) == ToolGesture.FlickFromEdge;

        /// <summary>Two seconds of dragging is still one gesture.</summary>
        const float StrokeMaxSeconds = 6f;

        /// <summary>Quick, short and fast enough to be a throw and not the start of a look around.</summary>
        public static bool IsFlick(float seconds, float travelShare) =>
            seconds <= FlickSeconds && travelShare >= FlickMinTravel && travelShare >= FlickMinSpeed * Mathf.Max(seconds, 1e-3f);

        void HandleInput()
        {
            int n = ReadPointers(out var a);
            float now = Time.unscaledTime;
            if (RealTool) Tray.RouteFingers(n); // two fingers hand the gesture to the camera
            if (n == 0)
            {
                bool tool = !RealTool || Tray.EndGesture();
                if (pressDown && pressOk && tool) Release(now - pressTime);
                pressDown = pressDrag = pressStroke = false;
                return;
            }
            if (!pressDown)
            {
                pressDown = true; pressPos = lastPos = a; pressTime = now;
                pressOk = n == 1 && ToolPress(a);
                if (pressOk && RealTool) Tray.BeginGesture();
                if (pressOk && Flicked && cam != null) cam.HoldOrbitUntil = now + FlickSeconds;
                return;
            }
            if (n == 1) lastPos = a;
            bool moved = (a - pressPos).magnitude > TapSlop * Mathf.Max(1f, Screen.height);
            if (n >= 2) pressOk = pressDrag = false;
            else if (cam != null && cam.GestureMoved) pressOk = false; // the camera has it: this press is a look around
            else if (moved && pressOk)
            {
                if (WishArmed) pressDrag = true; // a wish's spot follows the finger
                else if (StrokeTool) pressStroke = true;
                else if (!Flicked) pressOk = false;
            }
            if (pressDrag && GroundPoint(a, out float dx, out float dz)) Tray.MovePending(dx, dz);
        }

        /// <summary>The finger lifted on a press that's still the tool's: what it was decides what happens.</summary>
        void Release(float held)
        {
            float travel = (lastPos - pressPos).magnitude / Mathf.Max(1f, Screen.height);
            if (WishArmed) { if (held <= TapMaxSeconds && !pressDrag) ConfirmWish(); return; }
            bool place = pressStroke ? held <= StrokeMaxSeconds : Flicked ? IsFlick(held, travel) : held <= TapMaxSeconds;
            if (place && GroundPoint(pressStroke || Flicked ? lastPos : pressPos, out float x, out float z)) PlaceAt(x, z);
        }

        /// <summary>The ground under a screen point, in box coordinates (a march along the ray over the land's height).</summary>
        public bool GroundPoint(Vector2 screenPos, out float x, out float z)
        {
            x = z = 0f;
            var c = Cam;
            if (c == null || box == null) return false;
            var ray = c.ScreenPointToRay(screenPos);
            var o = box.transform.InverseTransformPoint(ray.origin);
            var d = box.transform.InverseTransformDirection(ray.direction).normalized;
            const float step = 0.04f; // 1500 steps reach 60 units: the camera may be 30+ away at the default framing (#94)
            float prev = 0f;
            bool wasAbove = true;
            for (int i = 1; i < 1500; i++)
            {
                float t = i * step;
                var p = o + d * t;
                bool inside = Mathf.Abs(p.x) <= Land.HX && Mathf.Abs(p.z) <= Land.HZ;
                if (inside && p.y <= box.GroundHeight(p.x, p.z))
                {
                    if (!wasAbove) return false;
                    float lo = prev, hi = t;
                    for (int k = 0; k < 12; k++)
                    {
                        float m = (lo + hi) * 0.5f;
                        var q = o + d * m;
                        if (q.y <= box.GroundHeight(Mathf.Clamp(q.x, -Land.HX, Land.HX), Mathf.Clamp(q.z, -Land.HZ, Land.HZ))) hi = m; else lo = m;
                    }
                    var h = o + d * hi;
                    x = h.x; z = h.z;
                    return true;
                }
                if (p.y < -1f) return false;
                prev = t;
            }
            return false;
        }

        /// <summary>The screen point over a box point at a height above the ground (tests and the tester agent touch here).</summary>
        public Vector2 ScreenOf(float x, float z, float up = 0f)
        {
            var c = Cam;
            if (c == null || box == null) return default;
            var w = box.transform.TransformPoint(new Vector3(x, box.GroundHeight(x, z) + up, z));
            var s = c.WorldToScreenPoint(w);
            return new Vector2(s.x, s.y);
        }

        // ================================================================== the frame

        void Update()
        {
            var sim = Sim;
            if (sim == null || box == null) return;
            if (hud == null || cam == null) Hook();
            if (!overlaysRefreshed && box.Root != null) { overlaysRefreshed = true; box.Refresh(); }
            if (sim != boundSim)
            {
                boundSim = sim;
                ClearVisuals();
                show = new ShapingShow();
            }
            var pv = PeopleView.Find();
            var planner = pv != null && pv.Planner != null && pv.Planner.Sim == sim ? pv.Planner : null;
            var now = pv != null ? pv.Now : new ViewTime(sim.State.month, 10f);
            show.Sync(sim, Time.unscaledTime, now, planner, events);
            foreach (var e in events)
            {
                EventLog.Add(e);
                if (!string.IsNullOrEmpty(e.cue)) Sfx.Play(e.cue); // #96: a tool landing, the people answering, one taken back
                if (e.kind == ShapingEventKind.Gone || e.kind == ShapingEventKind.TakenBack) box.Refresh();
                if (e.kind == ShapingEventKind.Answered) FlyToAnswer();
            }
            while (EventLog.Count > 64) EventLog.RemoveAt(0);
            if (Usable()) HandleInput(); else pressDown = false;
        }

        void LateUpdate()
        {
            var sim = Sim;
            if (sim == null || box == null || box.Root == null) return;
            if (root == null)
            {
                root = new GameObject("Shapings") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            var tl = TimeLapseView.Find();
            var src = SimLayoutSource.Find();
            bool hidden = (tl != null && tl.Active) || (src != null && src.Sim != sim); // the replay or another box on show
            root.gameObject.SetActive(!hidden);
            if (hidden) { HideUi(); return; }
            fxMat ??= LoadFx();
            float t = Time.unscaledTime;
            var seen = new HashSet<int>();
            foreach (var e in show.Entries)
            {
                seen.Add(e.nudgeId);
                if (!vis.TryGetValue(e.nudgeId, out var v)) vis[e.nudgeId] = v = new Vis(this, e);
                switch (e.kind)
                {
                    case SimCore.NudgeKind.Rain: v.Rain(show.Rain(e), t); break;
                    case SimCore.NudgeKind.Grove: v.Grove(show.Grove(e)); break;
                    case SimCore.NudgeKind.Channel: v.Channel(show.Channel(e), t); break;
                    case SimCore.NudgeKind.Herd: v.Herd(show.Herd(e), show.ClockOf(e), PastureFade(sim, e), t); break;
                }
            }
            var gone = new List<int>();
            foreach (var kv in vis) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { vis[id].Destroy(); vis.Remove(id); }
            DrawAnswer(show.Answer, t);
            DrawFootprint(sim);
            UpdateUi(sim);
        }

        /// <summary>Flights to an answer so far (tests).</summary>
        public int AnswerFlights { get; private set; }

        /// <summary>The people answer a gift: the camera flies there (user 2026-10-30), unless the player is busy elsewhere.
        /// A touch stops the flight like any other.</summary>
        void FlyToAnswer()
        {
            var a = show.Answer;
            if (cam == null || !a.active || !GlassVoice.CameraFree() || cam.Touching) return;
            cam.EaseTo(box.transform.TransformPoint(new Vector3(a.x, 0f, a.z)), cam.yaw, Mathf.Max(cam.pitch, BoxCamera.NudgePitch), Mathf.Min(cam.zoom, BoxCameraMath.FlyPlaceZoom), AnswerFlySeconds);
            AnswerFlights++;
        }

        public const float AnswerFlySeconds = 1.6f;

        /// <summary>An answered herd that went to a pasture joins the pasture's own flock: it fades once it gets there.</summary>
        float PastureFade(SimCore sim, ShapingShow.Entry e)
        {
            if (e.buildingId < 0 || sim.Settlement == null) return 1f;
            var b = sim.State.buildings.Find(q => q.id == e.buildingId);
            if (b == null || b.lotId == 0) return 1f;
            var lot = sim.Settlement.Lots.Find(l => l.id == b.lotId);
            if (lot == null || lot.kind != LotKind.Pasture) return 1f;
            var c = show.ClockOf(e);
            return 1f - Mathf.Clamp01((c.sinceAnswered - T.HerdMoveToLotSeconds) / 1.5f);
        }

        public static Material LoadFx()
        {
            var m = Resources.Load<Material>("ShapingFx");
            if (m != null) return m;
            var sh = Shader.Find("AquariumCiv/View/ShapingFx");
            return sh != null ? new Material(sh) { hideFlags = HideFlags.DontSave } : null;
        }

        void ClearVisuals()
        {
            foreach (var v in vis.Values) v.Destroy();
            vis.Clear();
            if (answerGo != null) { Destroy(answerGo); answerGo = null; }
            if (footGo != null) { Destroy(footGo); footGo = null; }
        }

        Vector3 Ground(float x, float z) => new(x, box.GroundHeight(Mathf.Clamp(x, -Land.HX, Land.HX), Mathf.Clamp(z, -Land.HZ, Land.HZ)), z);

        // ================================================================== the answer: ring, beam

        GameObject answerGo;
        ShapingMeshes.FxMesh answerFx;
        MeshRenderer answerR;
        MaterialPropertyBlock answerMpb;

        void DrawAnswer(AnswerLook a, float t)
        {
            if (!a.active || a.highlight <= 0.001f || fxMat == null)
            {
                if (answerGo != null) answerGo.SetActive(false);
                return;
            }
            if (answerGo == null)
            {
                answerFx = new ShapingMeshes.FxMesh("Shaping_AnswerGlow");
                answerR = ShapingMeshes.FxRenderer(root, "AnswerGlow", answerFx, fxMat);
                answerGo = answerR.gameObject;
                answerMpb = new MaterialPropertyBlock();
            }
            answerGo.SetActive(true);
            var g = Ground(a.x, a.z) + Vector3.up * 0.02f;
            float pulse = 1f + 0.06f * Mathf.Sin(t * 3f);
            answerFx.Clear();
            var glow = Palette.Glow;
            // Brighter (user 2026-10-30): a hot core line, the wide warm ring, a stronger beam.
            var hot = Palette.FireCore;
            answerFx.Ring(g, 0.2f * pulse, 0.27f * pulse, 32, new Color(hot.r, hot.g, hot.b, 0f), new Color(hot.r, hot.g, hot.b, 1f));
            answerFx.Ring(g, 0.27f * pulse, 0.5f * pulse, 32, new Color(hot.r, hot.g, hot.b, 1f), new Color(glow.r, glow.g, glow.b, 0f));
            answerFx.Beam(g, 0.27f, 1.1f, 20, new Color(1f, 0.93f, 0.68f, 0.65f), new Color(1f, 0.93f, 0.68f, 0f));
            answerFx.Apply();
            answerMpb.SetColor("_Color", new Color(1f, 1f, 1f, a.highlight));
            answerR.SetPropertyBlock(answerMpb);
        }

        // ================================================================== the mark on the ground

        GameObject footGo;
        ShapingMeshes.FxMesh footFx;
        MeshRenderer footR;
        float footX = float.NaN, footZ, restX, restZ;
        string footKind;
        bool restKnown, footValid;

        /// <summary>The tool's mark is on the ground (tests).</summary>
        public bool FootprintShown => footGo != null && footGo.activeSelf;
        /// <summary>The ground under the mark takes the tool (tests).</summary>
        public bool FootprintValid => footValid;

        /// <summary>Where the mark sits: a wish's spot, under the finger, or where it last was; a tool just picked starts in the middle of
        /// the view. The first run's rain has its own hint, so its mark only follows the finger.</summary>
        bool FootprintSpot(out float x, out float z)
        {
            x = z = 0f;
            if (!Usable()) return false;
            if (WishArmed) { x = Tray.PendingX; z = Tray.PendingZ; return true; }
            if (pressDown && pressOk && GroundPoint(lastPos, out x, out z)) { restX = x; restZ = z; restKnown = true; return true; }
            if (!RealTool) return false;
            restKnown = restKnown || GroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out restX, out restZ);
            x = restX; z = restZ;
            return restKnown;
        }

        /// <summary>The ground, or the water lying over it, so a mark on the pond shows.</summary>
        Vector3 OnTop(float x, float z)
        {
            var p = Ground(x, z);
            p.y = Mathf.Max(p.y, Land.WaterY);
            return p;
        }

        void DrawFootprint(SimCore sim)
        {
            bool on = FootprintSpot(out float x, out float z) && fxMat != null;
            if (!on)
            {
                if (footGo != null) footGo.SetActive(false);
                return;
            }
            if (footGo == null)
            {
                footFx = new ShapingMeshes.FxMesh("Shaping_Footprint");
                footR = ShapingMeshes.FxRenderer(root, "Footprint", footFx, fxMat);
                footGo = footR.gameObject;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", Color.white);
                footR.SetPropertyBlock(mpb);
                footX = float.NaN;
            }
            footGo.SetActive(true);
            if (footKind == Tool && Mathf.Abs(x - footX) < 0.01f && Mathf.Abs(z - footZ) < 0.01f) return;
            footKind = Tool; footX = x; footZ = z;
            footValid = ShapingFootprint.GroundReason(sim.Settlement?.Map, Tool, x, z).Length == 0;
            footFx.Clear();
            ShapingMeshes.Footprint(footFx, OnTop, x, z, ShapingFootprint.PreviewRadius(Tool), footValid);
            footFx.Apply();
        }

        // ================================================================== the UI: the world label, the bubble, the chips

        GameObject uiGo;
        UIDocument doc;
        VisualElement uiRoot;
        Label labelEl, bubbleEl;
        readonly List<GlassButton> chipEls = new();
        readonly int[] chipIds = new int[MaxChips];

        /// <summary>The world label and bubble showing now (tests).</summary>
        public string ShownLabel => labelEl != null && labelEl.style.display == DisplayStyle.Flex ? labelEl.text : null;
        public string ShownBubble => bubbleEl != null && bubbleEl.style.display == DisplayStyle.Flex ? bubbleEl.text : null;
        /// <summary>The take-back chips showing now: nudge id and the chip's screen centre (tests, the tester agent).</summary>
        public List<(int id, Vector2 screen)> Chips()
        {
            var l = new List<(int, Vector2)>();
            for (int i = 0; i < chipEls.Count; i++)
                if (chipEls[i].style.display == DisplayStyle.Flex && chipEls[i].panel != null) l.Add((chipIds[i], GameUiPanel.ScreenCenter(chipEls[i])));
            return l;
        }

        void BuildUi()
        {
            uiGo = new GameObject("ShapingUI") { hideFlags = HideFlags.DontSave };
            uiGo.transform.SetParent(transform, false);
            doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 11; // over the HUD's world, under the moment player
            uiRoot = doc.rootVisualElement;
            uiRoot.pickingMode = PickingMode.Ignore;
            uiRoot.style.position = Position.Absolute;
            uiRoot.style.left = uiRoot.style.right = uiRoot.style.top = uiRoot.style.bottom = 0;
            BuildWishBar();
            labelEl = Pill("shaping-label", "type-small-head");
            bubbleEl = Pill("shaping-bubble", "type-sub");
            for (int i = 0; i < MaxChips; i++)
            {
                int k = i;
                var b = new GlassButton("Take back", () => { if (chipIds[k] > 0) TakeBack(chipIds[k]); }, ButtonKind.Secondary, UiRole.None) { name = "shaping-takeback-" + i, Compact = true };
                b.Marked("undo", true);
                b.AddToClassList("shaping-chip");
                b.style.display = DisplayStyle.None;
                uiRoot.Add(b);
                chipEls.Add(b);
            }
        }

        VisualElement wishBar;
        GlassBanner toolBanner;
        GlassPips toolPips;
        GlassTag wishSpot;

        /// <summary>The tool in hand: the strip (name, what the finger does, charges) and Cancel in the tools' lane, spec §7 option A. The strip
        /// takes no taps, so the ground under it stays the tool's; only Cancel is a target. A wish adds a tag on the spot.</summary>
        void BuildWishBar()
        {
            wishBar = new VisualElement { name = "shaping-wish-bar", pickingMode = PickingMode.Ignore };
            wishBar.AddToClassList("tray-bar");
            wishBar.style.display = DisplayStyle.None;
            toolBanner = new GlassBanner("Tool", "Hint") { Inline = true, pickingMode = PickingMode.Ignore };
            toolBanner.AddToClassList("tray-bar-strip");
            toolBanner.TitleLabel.name = "shaping-banner";
            toolBanner.End.pickingMode = PickingMode.Ignore;
            toolPips = new GlassPips(0, 0) { name = "shaping-banner-pips" };
            toolBanner.End.Add(toolPips);
            wishBar.Add(toolBanner);
            var cancel = new GlassButton("Cancel", CancelWish, ButtonKind.Secondary, UiRole.Close) { name = "shaping-cancel" };
            cancel.AddToClassList("tray-cancel");
            wishBar.Add(cancel);
            uiRoot.Add(wishBar);
            wishSpot = new GlassTag("Here") { name = "shaping-wish-spot", InWorld = true };
            wishSpot.style.display = DisplayStyle.None;
            uiRoot.Add(wishSpot);
        }

        Label Pill(string name, string type)
        {
            var l = new Label { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList("shaping-pill");
            l.AddToClassList(type);
            l.style.display = DisplayStyle.None;
            uiRoot.Add(l);
            return l;
        }

        bool IsOverUi(Vector2 screenPos) => uiRoot != null && GameUiPanel.Over(uiRoot, screenPos);

        /// <summary>The answer's label and line on screen now (panel units), so other bubbles keep off them.</summary>
        public IEnumerable<Rect> ShownLabelRects()
        {
            if (labelEl != null && labelEl.style.display == DisplayStyle.Flex) yield return labelEl.worldBound;
            if (bubbleEl != null && bubbleEl.style.display == DisplayStyle.Flex) yield return bubbleEl.worldBound;
        }

        BubbleHost.Voice voice;

        void HideUi()
        {
            if (labelEl == null) return;
            labelEl.style.display = DisplayStyle.None;
            bubbleEl.style.display = DisplayStyle.None;
            if (voice != null) GameUiPanel.Bubbles.Done(voice);
            wishBar.style.display = DisplayStyle.None;
            wishSpot.style.display = DisplayStyle.None;
            foreach (var c in chipEls) c.style.display = DisplayStyle.None;
            hud?.SetToolInHand(false);
        }

        /// <summary>Puts an element centred over a box point (panel units); false when the point is behind the camera.</summary>
        bool Anchor(VisualElement e, float x, float z, float up, float dyPanel = 0f, bool below = false, bool inFrame = false)
        {
            var c = Cam;
            if (c == null || e.panel == null) return false;
            var w = box.transform.TransformPoint(Ground(x, z) + Vector3.up * up);
            var s = c.WorldToScreenPoint(w);
            if (s.z <= 0f) return false;
            var p = RuntimePanelUtils.ScreenToPanel(e.panel, new Vector2(s.x, Screen.height - s.y));
            if (inFrame) p.x = KeepInFrame(e, p.x); // tester run 10: close in, a caption ran off the left edge
            e.style.left = p.x;
            e.style.top = p.y + dyPanel;
            e.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(below ? 0 : -100, LengthUnit.Percent));
            return true;
        }

        /// <summary>The middle of a label moved just far enough for all of it to be on screen.</summary>
        static float KeepInFrame(VisualElement e, float x)
        {
            float half = e.resolvedStyle.width * 0.5f, wide = e.panel.visualTree.layout.width, side = half + 24f;
            return float.IsNaN(half) || float.IsNaN(wide) ? x : Mathf.Clamp(x, side, Mathf.Max(side, wide - side));
        }

        /// <summary>A chip under its gift, never over it, and under the chips already placed this frame, so two gifts side by side keep two targets (tester run 7).</summary>
        bool AnchorChip(int k, float x, float z)
        {
            var el = chipEls[k];
            float w = float.IsNaN(el.resolvedStyle.width) || el.resolvedStyle.width < 1f ? ChipW : el.resolvedStyle.width;
            float h = float.IsNaN(el.resolvedStyle.height) || el.resolvedStyle.height < 1f ? ChipH : el.resolvedStyle.height;
            float dy = 40f;
            for (int tries = 0; tries <= k; tries++)
            {
                if (!Anchor(el, x, z, 0f, dy, true)) return false;
                float at = el.style.left.value.value, top = el.style.top.value.value;
                bool clash = false;
                for (int i = 0; i < k; i++)
                {
                    float ox = chipEls[i].style.left.value.value, oy = chipEls[i].style.top.value.value;
                    if (Mathf.Abs(ox - at) < w + 8f && Mathf.Abs(oy - top) < h + 8f) { dy = oy + h + 8f - (top - dy); clash = true; break; }
                }
                if (!clash) break;
            }
            return true;
        }

        const float ChipW = 300f, ChipH = 96f;

        string bannerHint;
        int pipsOn = -1, pipsAll = -1;

        void UpdateUi(SimCore sim)
        {
            if (uiRoot == null) return;
            bool noUi = BoxCompleteController.ShelfIsOpen || (hud != null && (hud.CameraMode || hud.SheetOpen || !hud.IsVisible));
            bool armed = !noUi && RealTool && Tool != null;
            wishBar.style.display = armed ? DisplayStyle.Flex : DisplayStyle.None;
            hud?.SetToolInHand(armed);
            bool spot = armed && WishArmed && Anchor(wishSpot, Tray.PendingX, Tray.PendingZ, ShapingFootprint.PreviewRadius(Tool) * 0.8f); // over the mark
            wishSpot.style.display = spot ? DisplayStyle.Flex : DisplayStyle.None;
            if (armed)
            {
                string title = Tray.BannerTitle(sim), hint = Tray.BannerHint(sim);
                if (toolBanner.TitleLabel.text != title || bannerHint != hint) toolBanner.Set(title, bannerHint = hint);
                var c = Tray.Charges(sim);
                if (c.whole != pipsOn || c.max != pipsAll) toolPips.Set(pipsAll = c.max, pipsOn = c.whole);
                wishBar.style.bottom = Mathf.Max(GameUiPanel.ToolsBottom, GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height).w);
            }
            RefreshTray(sim, false);
            var a = show.Answer;
            bool label = !noUi && a.active && a.labelAlpha > 0.01f && Anchor(labelEl, a.x, a.z, 0.35f, inFrame: true);
            labelEl.style.display = label ? DisplayStyle.Flex : DisplayStyle.None;
            if (label) { labelEl.text = a.label; labelEl.style.opacity = a.labelAlpha; }
            bool bubble = !noUi && a.active && a.bubbleAlpha > 0.01f && !string.IsNullOrEmpty(a.bubble) && Anchor(bubbleEl, a.x, a.z, 0.35f, -80f, inFrame: true);
            voice ??= GameUiPanel.Bubbles.Join(BubbleHost.Rank.Line);
            if (bubble) bubble = GameUiPanel.Bubbles.Ask(voice); // asked every frame: it waits for whoever has the word
            else GameUiPanel.Bubbles.Done(voice);
            bubbleEl.style.display = bubble ? DisplayStyle.Flex : DisplayStyle.None;
            if (bubble) { bubbleEl.text = a.bubble; bubbleEl.style.opacity = a.bubbleAlpha; }

            int k = 0;
            if (!noUi && UiUsable())
                for (int i = show.Entries.Count - 1; i >= 0 && k < MaxChips; i--)
                {
                    var e = show.Entries[i];
                    var chip = show.ChipFor(sim, e.nudgeId);
                    if (!chip.visible || show.StageOf(e) == ShapingStage.Arriving) continue; // let the arrival be seen first
                    if (!AnchorChip(k, chip.x, chip.z)) continue;
                    chipIds[k] = e.nudgeId;
                    chipEls[k].style.opacity = chip.alpha;
                    chipEls[k].style.display = DisplayStyle.Flex;
                    k++;
                }
            for (; k < MaxChips; k++) { chipEls[k].style.display = DisplayStyle.None; chipIds[k] = 0; }
        }

        // ================================================================== the tray's sheet (#84, #167)
        // Board 3.4: a sheet of 30 %, the charges in its head, one row of four tiles, a note with the reason under them, and the Take back
        // list in the row's place. Every element has the name the tests and the tester find. The model is ShapingTray; this only draws it.

        string traySig = "";
        /// <summary>The Take back list is showing instead of the tiles.</summary>
        bool trayList;
        /// <summary>The unavailable tile tapped last: the note gives its reason.</summary>
        string trayAsked;
        VisualElement trayRefill;

        void OnSheet(string id)
        {
            var sim = Sim;
            if (id == GameHud.Shaping && sim != null) { trayList = false; trayAsked = null; Tray.Open(); RefreshTray(sim, true); }
        }

        void RefreshTray(SimCore sim, bool force)
        {
            if (hud == null || hud.ShapingBody == null || hud.OpenSheet != GameHud.Shaping) return;
            var charges = Tray.Charges(sim);
            float refill = Mathf.Clamp01(charges.refill) * 100f;
            if (trayRefill != null) trayRefill.style.width = Length.Percent(refill); // every frame, without pulling the tiles from under a finger
            var tools = Tray.Tools(sim);
            var list = Tray.TakeBackList(sim);
            var sig = new System.Text.StringBuilder();
            sig.Append(charges.text).Append(sim.State.era).Append(trayList).Append(trayAsked);
            foreach (var t in tools) sig.Append(t.kind).Append(t.fail).Append(t.standing).Append(t.name);
            foreach (var i in list) sig.Append('#').Append(i.nudgeId);
            if (!force && sig.ToString() == traySig) return;
            traySig = sig.ToString();

            hud.ShapingHead.Clear();
            var row = new VisualElement { name = "shaping-charges", pickingMode = PickingMode.Ignore };
            row.AddToClassList("tray-charges");
            var pips = new GlassPips(charges.max, charges.whole) { name = "shaping-pips" };
            trayRefill = null;
            if (!charges.full)
            {
                trayRefill = new VisualElement { name = "shaping-refill", pickingMode = PickingMode.Ignore };
                trayRefill.AddToClassList("tray-pip-fill");
                trayRefill.style.width = Length.Percent(refill);
                pips[charges.whole].Add(trayRefill);
            }
            row.Add(pips);
            var ct = TrayLabel(charges.text, "type-sub", "tray-charges-text");
            ct.name = "shaping-charges-text";
            row.Add(ct);
            hud.ShapingHead.Add(row);

            var body = hud.ShapingBody;
            body.Clear();
            if (trayList) body.Add(TakeBackList(list));
            else body.Add(Tiles(tools));

            var foot = new VisualElement { pickingMode = PickingMode.Ignore };
            foot.AddToClassList("tray-foot");
            var note = TrayLabel(trayList ? (list.Count == 0 ? "Nothing waiting. A gift can be taken back until the people use it." : "A gift can be taken back until the people use it.") : TrayNote(tools, charges),
                "type-note", "tray-note");
            note.name = "shaping-tray-note";
            foot.Add(note);
            if (trayList) foot.Add(new GlassButton("Back", () => { trayList = false; }, ButtonKind.Secondary, UiRole.Back) { name = "shaping-takeback-back", Compact = true });
            else if (charges.whole > 0 || list.Count > 0) foot.Add(new GlassButton("Take back", () => { trayList = true; }, ButtonKind.Secondary, UiRole.Open) { name = "shaping-takeback-open", Compact = true });
            body.Add(foot);
        }

        /// <summary>A name this long ("Wool flock", "Blossoming") only fits a tile a size down.</summary>
        const int LongToolName = 9, WrapToolName = 14;

        VisualElement Tiles(IReadOnlyList<TrayTool> list)
        {
            var tools = new VisualElement { name = "shaping-tools", pickingMode = PickingMode.Ignore };
            tools.AddToClassList("tray-tiles");
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                string kind = t.kind;
                var b = new Button(() => OnToolButton(kind)) { name = "shaping-tool-" + kind };
                b.RemoveFromClassList(Button.ussClassName); // every state comes from Glass.uss
                b.AddToClassList("kg-tile");
                b.EnableInClassList("kg-tile--off", !t.enabled);
                b.EnableInClassList("kg-tile--armed", t.armed);
                b.EnableInClassList("kg-tile--last", i == list.Count - 1);
                b.EnableInClassList("kg-tile--wrap", t.name.Length >= WrapToolName);
                b.Add(new GlassIcon(t.enabled ? IconOf(kind) : "lock", "kg-icon--small"));
                var name = TrayLabel(Cap(t.name), "type-small-head", "kg-tile-name");
                name.EnableInClassList("kg-tile-name--long", t.name.Length >= LongToolName);
                name.EnableInClassList("kg-tile-name--wrap", t.name.Length >= WrapToolName); // "Blossoming grove": no size of type fits it on one line
                b.Add(name);
                b.Add(TrayLabel($"Level {t.level}", "type-note"));
                b.Add(TrayLabel($"{t.standing} of {t.cap} out", "type-note"));
                tools.Add(b);
            }
            return tools;
        }

        VisualElement TakeBackList(IReadOnlyList<TakeBackItem> list)
        {
            var scroll = new GlassScroll { WholeRows = true };
            scroll.AddToClassList("tray-list");
            foreach (var item in list)
            {
                int id = item.nudgeId;
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("tray-row");
                row.AddToClassList(GlassScroll.RowClass);
                row.Add(TrayLabel(item.label, "type-body", "tray-row-label"));
                var b = new GlassButton("Take back", () => { TakeBack(id); traySig = ""; }, ButtonKind.Secondary, UiRole.None) { name = "shaping-tray-takeback-" + id, Compact = true };
                b.AddToClassList("tray-takeback");
                row.Add(b);
                scroll.Add(row);
            }
            return scroll;
        }

        /// <summary>The one line under the tiles: why the tile just tapped can't be used, else why the tools are down, a capped one before a
        /// locked one. The later tools get the last word when nothing's wrong.</summary>
        string TrayNote(IReadOnlyList<TrayTool> tools, ChargeDisplay charges)
        {
            foreach (var t in tools) if (t.kind == trayAsked && !t.enabled) return ToolNote(t);
            if (charges.whole < 1) return "The next one comes back as the people are happy, or when you clean the glass.";
            foreach (var t in tools) if (t.fail == SimCore.ShapeFail.AtCap) return ToolNote(t);
            foreach (var t in tools) if (t.fail == SimCore.ShapeFail.Locked) return ToolNote(t);
            return "Hill and hollow come later.";
        }

        static string ToolNote(TrayTool t) => t.fail == SimCore.ShapeFail.NoCharge ? t.reason + "."
            : t.fail == SimCore.ShapeFail.Locked ? $"{Cap(t.name)} {char.ToLowerInvariant(t.reason[0])}{t.reason.Substring(1)}."
            : $"{Cap(t.name)}: {t.reason}.";

        static string IconOf(string kind) => kind switch
        {
            SimCore.NudgeKind.Rain => "drop",
            SimCore.NudgeKind.Grove => "tree",
            SimCore.NudgeKind.Herd => "herd",
            SimCore.NudgeKind.Channel => "channel",
            _ => "gift",
        };

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static Label TrayLabel(string text, params string[] classes)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        /// <summary>A tile: picks the tool and closes the sheet so the world is free for the finger. An unavailable one stays down and the note
        /// says why.</summary>
        void OnToolButton(string kind)
        {
            var sim = Sim;
            if (sim == null) return;
            var t = Tray.ToolOf(sim, kind);
            if (!t.enabled)
            {
                UiSound.Play(UiRole.Refused);
                LastResult = t.reason;
                trayAsked = kind;
                return;
            }
            UiSound.Play(UiRole.Tap);
            if (ArmTool(kind)) hud?.CloseSheet();
            else traySig = "";
        }

        // ================================================================== one shaping's objects

        sealed class Vis
        {
            readonly ShapingView owner;
            readonly ShapingShow.Entry entry;
            readonly Transform root;
            readonly MaterialPropertyBlock mpb = new();

            public Vis(ShapingView owner, ShapingShow.Entry e)
            {
                this.owner = owner;
                entry = e;
                root = new GameObject($"Shaping_{e.kind}_{e.nudgeId}") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(owner.root, false);
            }

            public void Destroy() { if (root != null) Object.Destroy(root.gameObject); }

            Material Solid => owner.box.SolidMaterial;
            Vector3 Ground(float x, float z) => owner.Ground(x, z);

            void Tint(Renderer r, float alpha)
            {
                mpb.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
                r.SetPropertyBlock(mpb);
            }

            // ---------------------------------------------------------- rain
            Transform cloud;
            bool cloudStorm;
            Light flash;
            ShapingMeshes.FxMesh streaks, wet;
            MeshRenderer streakR, wetR;
            readonly List<Transform> sprouts = new();
            int streakCount;

            public void Rain(RainLook r, float t)
            {
                if (owner.fxMat == null) return;
                var g = Ground(r.x, r.z);
                if (cloud == null || cloudStorm != r.storm)
                {
                    if (cloud != null) Object.Destroy(cloud.gameObject);
                    cloudStorm = r.storm;
                    cloud = ShapingMeshes.Solid(root, "Cloud", ShapingMeshes.Cloud(r.storm), Solid, false);
                }
                float cs = ShapingLooks.Smooth(r.cloud) * (r.storm ? 1.3f : 1f);
                cloud.gameObject.SetActive(cs > 0.01f);
                cloud.localPosition = g + Vector3.up * CloudHeight + new Vector3(Mathf.Sin(t * 0.4f) * 0.03f, Mathf.Sin(t * 0.7f) * 0.01f, 0f);
                cloud.localScale = Vector3.one * Mathf.Max(0.001f, cs);

                // the streaks, falling from the cloud to the ground
                if (streaks == null)
                {
                    streaks = new ShapingMeshes.FxMesh("Shaping_Rain");
                    streakR = ShapingMeshes.FxRenderer(root, "Rain", streaks, owner.fxMat);
                    streakCount = r.storm ? 220 : entry.footprint.level == Era.Village ? 150 : 100;
                }
                streakR.gameObject.SetActive(r.rain > 0.01f);
                if (r.rain > 0.01f)
                {
                    streaks.Clear();
                    var col = new Color(0.82f, 0.9f, 1f, 0.75f);
                    float top = g.y + CloudHeight - 0.08f;
                    for (int i = 0; i < streakCount; i++)
                    {
                        float a = Land.Hash3(entry.footprint.seed, i, 1) * Mathf.PI * 2f;
                        float rad = Mathf.Sqrt(Land.Hash3(entry.footprint.seed, i, 2)) * 0.32f * cs;
                        float x = r.x + Mathf.Cos(a) * rad, z = r.z + Mathf.Sin(a) * rad;
                        float gy = owner.box.GroundHeight(Mathf.Clamp(x, -Land.HX, Land.HX), Mathf.Clamp(z, -Land.HZ, Land.HZ));
                        float u = Mathf.Repeat(t * (r.storm ? 2.2f : 1.6f) + Land.Hash3(entry.footprint.seed, i, 3), 1f);
                        float y = Mathf.Lerp(top, gy, u);
                        float len = r.storm ? 0.09f : 0.065f;
                        var p0 = new Vector3(x, y, z);
                        var p1 = new Vector3(x - (r.storm ? 0.02f : 0.005f), y + len, z);
                        const float w = 0.004f;
                        streaks.Quad(p0 + new Vector3(-w, 0, 0), p1 + new Vector3(-w, 0, 0), p1 + new Vector3(w, 0, 0), p0 + new Vector3(w, 0, 0), col);
                        streaks.Quad(p0 + new Vector3(0, 0, -w), p1 + new Vector3(0, 0, -w), p1 + new Vector3(0, 0, w), p0 + new Vector3(0, 0, w), col);
                    }
                    streaks.Apply();
                    Tint(streakR, r.rain);
                }

                // the darkened soil
                if (wet == null)
                {
                    wet = new ShapingMeshes.FxMesh("Shaping_Wet");
                    wetR = ShapingMeshes.FxRenderer(root, "WetSoil", wet, owner.fxMat);
                    var col = Palette.SoilDark;
                    const float h = SiteMap.Cell * 0.5f;
                    foreach (int c in entry.footprint.cells)
                    {
                        float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                        float fall = 1f - Mathf.Clamp01((Vector2.Distance(new Vector2(x, z), new Vector2(r.x, r.z)) - 0.3f) / 0.25f); // soft edge
                        var cc = new Color(col.r, col.g, col.b, 0.5f * Mathf.Max(0.25f, fall));
                        Vector3 P(float px, float pz) => Ground(px, pz) + Vector3.up * 0.006f;
                        wet.Quad(P(x - h, z - h), P(x - h, z + h), P(x + h, z + h), P(x + h, z - h), cc);
                    }
                    wet.Apply();
                }
                wetR.gameObject.SetActive(r.wet > 0.01f);
                if (r.wet > 0.01f) Tint(wetR, r.wet);

                // sprouts on the wet ground
                if (sprouts.Count == 0)
                {
                    int k = 0;
                    foreach (int c in entry.footprint.cells)
                    {
                        if (Land.Hash3(entry.footprint.seed, c, 9) > 0.45f) continue;
                        float x = SiteMap.CXc(c) + (Land.Hash3(entry.footprint.seed, c, 10) - 0.5f) * 0.06f;
                        float z = SiteMap.CZc(c) + (Land.Hash3(entry.footprint.seed, c, 11) - 0.5f) * 0.06f;
                        var s = ShapingMeshes.Solid(root, "Sprout", ShapingMeshes.Sprouts(k++ % 3), Solid, false);
                        s.localPosition = Ground(x, z);
                        s.localRotation = Quaternion.Euler(0f, Land.Hash3(entry.footprint.seed, c, 12) * 360f, 0f);
                        sprouts.Add(s);
                    }
                }
                float sp = r.sprouts;
                foreach (var s in sprouts)
                {
                    s.gameObject.SetActive(sp > 0.02f);
                    s.localScale = new Vector3(0.6f + 0.4f * sp, Mathf.Max(0.001f, sp), 0.6f + 0.4f * sp);
                }

                // a storm's lightning
                if (r.storm)
                {
                    if (flash == null)
                    {
                        var go = new GameObject("Lightning") { hideFlags = HideFlags.DontSave };
                        go.transform.SetParent(root, false);
                        flash = go.AddComponent<Light>();
                        flash.type = LightType.Point;
                        flash.range = 2.5f;
                        flash.color = new Color(0.85f, 0.9f, 1f);
                        flash.shadows = LightShadows.None;
                    }
                    flash.transform.localPosition = g + Vector3.up * (CloudHeight - 0.15f);
                    flash.intensity = r.flash * 6f;
                    flash.enabled = r.flash > 0.01f;
                }
            }

            // ---------------------------------------------------------- grove
            readonly Dictionary<int, (Transform t, string key)> trees = new();

            public void Grove(GroveLook g)
            {
                var seen = new HashSet<int>();
                foreach (var tr in g.trees)
                {
                    seen.Add(tr.seed);
                    bool blossom = tr.blossom > 0.5f, fruit = tr.fruit > 0.5f;
                    int variant = (tr.seed & 0x7fffffff) % 4;
                    string key = $"{g.style}{blossom}{fruit}{variant}";
                    if (!trees.TryGetValue(tr.seed, out var it) || it.t == null)
                    {
                        var t = ShapingMeshes.Solid(root, "Tree", ShapingMeshes.Tree(g.style, blossom, fruit, variant), Solid);
                        t.localRotation = Quaternion.Euler(0f, (tr.seed & 0xffff) % 360, 0f);
                        trees[tr.seed] = it = (t, key);
                    }
                    else if (it.key != key)
                    {
                        it.t.GetComponent<MeshFilter>().sharedMesh = ShapingMeshes.Tree(g.style, blossom, fruit, variant);
                        trees[tr.seed] = it = (it.t, key);
                    }
                    it.t.gameObject.SetActive(tr.growth > 0.005f);
                    it.t.localPosition = Ground(tr.x, tr.z);
                    it.t.localScale = Vector3.one * Mathf.Max(0.001f, tr.growth);
                }
                var drop = new List<int>();
                foreach (var kv in trees) if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
                foreach (int k in drop) { if (trees[k].t != null) Object.Destroy(trees[k].t.gameObject); trees.Remove(k); }
            }

            // ---------------------------------------------------------- channel
            ShapingMeshes.FxMesh water, trench;
            MeshRenderer waterR, trenchR;
            MeshFilter capsF;
            Mesh capsMesh;
            string capsSig = "";
            Transform wheel;
            readonly List<Transform> sluices = new();

            public void Channel(ChannelLook ch, float t)
            {
                if (owner.fxMat == null || ch.cells.Count == 0) return;
                var land = owner.box.land;
                const float h = SiteMap.Cell * 0.5f;
                bool leaving = entry.leavingAt >= 0f;

                // Turf caps over the cells not yet carved (the layout has already cut the trench): they shrink as the cut runs.
                var sig = new System.Text.StringBuilder();
                foreach (var c in ch.cells) sig.Append(c.carved >= 1f ? '1' : c.carved <= 0f ? '0' : (char)('a' + (int)(c.carved * 20f)));
                string s = leaving ? "" : sig.ToString(); // after a take-back the land is whole again: no caps
                if (s != capsSig)
                {
                    capsSig = s;
                    var mb = new MeshBuilder(entry.footprint.seed) { jitter = 0f };
                    if (!leaving)
                        foreach (var c in ch.cells)
                        {
                            if (c.carved >= 1f) continue;
                            float k = h * (1.05f - c.carved);
                            float y = land.Height(c.x, c.z) + 0.004f;
                            var col = Color.Lerp(Palette.GrassA, Palette.GrassB, Land.Hash3(entry.footprint.seed, c.cell, 21));
                            mb.Quad(new Vector3(c.x - k, y, c.z - k), new Vector3(c.x - k, y, c.z + k), new Vector3(c.x + k, y, c.z + k), new Vector3(c.x + k, y, c.z - k), col);
                        }
                    if (capsF == null)
                    {
                        var go = new GameObject("TurfCaps") { hideFlags = HideFlags.DontSave };
                        go.transform.SetParent(root, false);
                        capsF = go.AddComponent<MeshFilter>();
                        go.AddComponent<MeshRenderer>().sharedMaterial = Solid;
                    }
                    if (capsMesh != null) Object.Destroy(capsMesh);
                    capsMesh = mb.ToMesh("Shaping_TurfCaps");
                    capsMesh.hideFlags = HideFlags.DontSave;
                    capsF.sharedMesh = capsMesh;
                }

                // The water, with a shimmer running down the channel; on a take-back the dark trench closes after it drains.
                if (water == null)
                {
                    water = new ShapingMeshes.FxMesh("Shaping_ChannelWater");
                    waterR = ShapingMeshes.FxRenderer(root, "Water", water, owner.fxMat);
                    trench = new ShapingMeshes.FxMesh("Shaping_Trench");
                    trenchR = ShapingMeshes.FxRenderer(root, "Trench", trench, owner.fxMat);
                }
                water.Clear(); trench.Clear();
                for (int i = 0; i < ch.cells.Count; i++)
                {
                    var c = ch.cells[i];
                    float y = (leaving ? land.Height(c.x, c.z) : owner.box.GroundHeight(c.x, c.z)) + 0.012f;
                    if (c.water > 0.01f)
                    {
                        float shimmer = 0.5f + 0.5f * Mathf.Sin(t * 4f - i * 0.7f);
                        var col = Color.Lerp(Palette.Water, Palette.Plaster, 0.25f * shimmer);
                        col.a = 0.75f * c.water;
                        float k = h * 0.8f;
                        water.Quad(new Vector3(c.x - k, y, c.z - k), new Vector3(c.x - k, y, c.z + k), new Vector3(c.x + k, y, c.z + k), new Vector3(c.x + k, y, c.z - k), col);
                    }
                    if (leaving && c.carved > 0.01f)
                    {
                        var col = Palette.SoilDark; col.a = 0.8f * c.carved;
                        float k = h * 0.75f * c.carved;
                        trench.Quad(new Vector3(c.x - k, y - 0.004f, c.z - k), new Vector3(c.x - k, y - 0.004f, c.z + k), new Vector3(c.x + k, y - 0.004f, c.z + k), new Vector3(c.x + k, y - 0.004f, c.z - k), col);
                    }
                }
                water.Apply(); trench.Apply();
                Tint(waterR, 1f); Tint(trenchR, 1f);

                // the town's mill race: a wheel turning at the little fall
                bool race = ch.style == ChannelStyle.MillRace && ch.fallAt > 0 && ch.fallAt < ch.cells.Count - 1;
                if (race && wheel == null) wheel = ShapingMeshes.Solid(root, "WaterWheel", ShapingMeshes.WaterWheel(), Solid);
                if (wheel != null)
                {
                    var f = ch.cells[Mathf.Clamp(ch.fallAt, 0, ch.cells.Count - 1)];
                    bool on = race && f.carved >= 1f;
                    wheel.gameObject.SetActive(on);
                    if (on)
                    {
                        var a = ch.cells[ch.fallAt - 1]; var b = ch.cells[ch.fallAt + 1];
                        var dir = new Vector3(b.x - a.x, 0f, b.z - a.z);
                        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
                        wheel.localPosition = Ground(f.x, f.z) + Vector3.up * 0.05f;
                        wheel.localRotation = Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(-t * 90f * f.water, 0f, 0f);
                    }
                }
                // the village's irrigation: little sluice boards along it once the farmers line it with fields
                bool irrigate = ch.style == ChannelStyle.Irrigation && !leaving;
                if (irrigate && sluices.Count == 0)
                    for (int i = 2; i < ch.cells.Count - 1; i += 4)
                    {
                        var a = ch.cells[i - 1]; var b = ch.cells[i + 1]; var c = ch.cells[i];
                        var dir = new Vector3(b.x - a.x, 0f, b.z - a.z);
                        var sl = ShapingMeshes.Solid(root, "Sluice", ShapingMeshes.Sluice(), Solid);
                        sl.localPosition = Ground(c.x, c.z);
                        sl.localRotation = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
                        sluices.Add(sl);
                    }
                foreach (var sl in sluices) sl.gameObject.SetActive(irrigate);
            }

            // ---------------------------------------------------------- herd
            readonly List<Transform> animals = new();
            ShapingMeshes.FxMesh splash;
            MeshRenderer splashR;
            HerdSpecies species;

            public void Herd(HerdLook h, ShapingClock c, float fade, float t)
            {
                if (animals.Count != h.animals.Count || species != h.species)
                {
                    foreach (var a in animals) if (a != null) Object.Destroy(a.gameObject);
                    animals.Clear();
                    species = h.species;
                    for (int i = 0; i < h.animals.Count; i++)
                        animals.Add(ShapingMeshes.Solid(root, h.species.ToString(), ShapingMeshes.Animal(h.species), Solid, h.species != HerdSpecies.Fish));
                }
                bool fish = h.species == HerdSpecies.Fish;
                for (int i = 0; i < h.animals.Count; i++)
                {
                    var a = h.animals[i];
                    var tr = animals[i];
                    float alpha = a.alpha * fade;
                    bool show = a.pose != AnimalPose.Hidden && alpha > 0.01f;
                    tr.gameObject.SetActive(show);
                    if (!show) continue;
                    float ph = (a.seed & 0xffff) * 0.001f;
                    float yaw = a.heading * Mathf.Rad2Deg, pitch = 0f, bob = 0f;
                    switch (a.pose)
                    {
                        case AnimalPose.Run: bob = Mathf.Abs(Mathf.Sin(t * 14f + ph)) * 0.014f; break;
                        case AnimalPose.Walk: bob = Mathf.Abs(Mathf.Sin(t * 8f + ph)) * 0.006f; break;
                        case AnimalPose.Graze: pitch = 14f + 4f * Mathf.Sin(t * 2.3f + ph); break;
                        case AnimalPose.Swim: yaw += 10f * Mathf.Sin(t * 9f + ph); break;
                        case AnimalPose.Dive: pitch = 40f; break;
                    }
                    // just at the surface, so the shoal reads through the water at phone size
                    var p = fish ? new Vector3(a.x, Land.WaterY + 0.002f - (a.pose == AnimalPose.Dive ? (1f - alpha) * 0.05f : 0f), a.z) : Ground(a.x, a.z);
                    tr.localPosition = p + Vector3.up * bob;
                    tr.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                    tr.localScale = Vector3.one * (fish ? FishScale * Mathf.Max(0.001f, alpha) : AnimalScale * Mathf.Max(0.001f, fade));
                }
                if (fish && owner.fxMat != null)
                {
                    if (splash == null)
                    {
                        splash = new ShapingMeshes.FxMesh("Shaping_Splash");
                        splashR = ShapingMeshes.FxRenderer(root, "Splash", splash, owner.fxMat);
                    }
                    splashR.gameObject.SetActive(h.splash > 0.01f);
                    if (h.splash > 0.01f)
                    {
                        float grow = 1f - h.splash;
                        splash.Clear();
                        var at = new Vector3(entry.footprint.entryX, Land.WaterY + 0.004f, entry.footprint.entryZ);
                        splash.Ring(at, 0.05f + 0.2f * grow, 0.09f + 0.24f * grow, 24, new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0f));
                        splash.Apply();
                        Tint(splashR, h.splash);
                    }
                }
            }
        }
    }
}
