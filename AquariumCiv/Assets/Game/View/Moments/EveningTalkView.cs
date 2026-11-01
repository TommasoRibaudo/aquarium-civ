using System;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// The evening talk: how a long sitting ends (#77, first-session-and-check-in.md §4, board Flow: the close). When
    /// <see cref="CloseRules"/> says so, the HUD steps away, the people gather at the fire or green, the role-holder stands with their name
    /// over them and the teaser (<see cref="SimCore.OpenTeaser"/>) takes the caption, a few lamps light and the camera eases to the box on
    /// its shelf. The last line stays; nothing competes with it (hierarchy.md). An era that waits replaces the teaser with "They'll wait
    /// for you", and the first time it also asks about notifications.
    ///
    /// It never blocks: any touch leaves it, and leaving the app ends it. A teaser opens its thread only when it's said, so what the player
    /// walked away from early was never promised. The sim keeps running: this is a view beat, not the sim's night.
    ///
    /// Stable names: moment-caption (the line), close-speaker (the name over the speaker). Debug: static <c>Debug*</c> (also `script eval`).
    /// </summary>
    public class EveningTalkView : MonoBehaviour
    {
        public static EveningTalkView Instance { get; private set; }
        public static bool AutoCreate = true;
        /// <summary>False in play tests: a test that stands still for 20 s mustn't find its HUD gone. <see cref="DebugStart"/> still works.</summary>
        public static bool AutoStart = true;

        /// <summary>The evening light (a view hour), lamps at this level by the end. No real night exists yet.</summary>
        const float EveningHour = 18.6f, LampLevel = 0.7f;
        const float GatherZoom = 0.62f, GatherPitch = 44f, ShelfPitch = 30f, ShelfYaw = 20f;
        /// <summary>The name tag's height over the speaker's feet, in box units.</summary>
        const float TagHeight = 0.42f;

        public CloseStep Step { get; private set; }
        public bool Open => Step != CloseStep.None;
        public string Line { get; private set; }
        public string Speaker { get; private set; } = "";
        /// <summary>The line wasn't said because the player left first (tests).</summary>
        public bool LeftEarly { get; private set; }

        /// <summary>Debug: added to this sitting, to reach the long-sitting rule without waiting.</summary>
        public double SessionBonus { get; set; }

        UIDocument doc;
        VisualElement root;
        GlassTag speakerTag;
        BoxCamera cam;
        Vector3 fire;
        int speakerId = -1;
        float t, lastActive, lastClose = float.NegativeInfinity;
        bool hudWas = true, said, sawFirstRun, pressed, homeOnLift, asked, swallowing;
        string forcedLine;

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static EveningTalkView Create() => new GameObject("EveningTalk").AddComponent<EveningTalkView>();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 12;
            lastActive = Time.unscaledTime;
        }

        /// <summary>The close owns the screen: the touch that leaves it is for leaving, and doesn't reach a person or the glass.</summary>
        bool OwnsPress(Vector2 p) => (Open || swallowing) && !(MomentPlayer.Instance != null && MomentPlayer.Instance.NotifyAskUp);

        void OnDisable() { if (Instance == this) UiInput.Remove(OwnsPress); }

        void OnEnable()
        {
            if (Instance != this) return;
            UiInput.Add(OwnsPress);
            root = new VisualElement { name = "close-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            doc.rootVisualElement.Add(root);
            speakerTag = new GlassTag("") { name = "close-speaker", InWorld = true };
            speakerTag.AddToClassList(GlassMotion.FadeClass);
            speakerTag.style.display = DisplayStyle.None;
            root.Add(speakerTag);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (Open) Finish();
            Instance = null;
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && Open) Finish();
            if (!pause) lastActive = Time.unscaledTime;
        }

        // ---------------- starting ----------------
        /// <summary>Starts the close now. False while something else has the screen. <paramref name="line"/> forces the words (debug).</summary>
        public bool Begin(string line = null)
        {
            var sim = Sim;
            if (Open || sim == null || MomentPlayer.Instance == null) return false;
            var clock = GameClock.Instance;
            if (clock.Visiting) return false;
            cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();
            forcedLine = line;
            var hud = GameHud.Instance;
            if (hud != null)
            {
                hud.CloseSheet();
                hud.SetCleanMode(false);
                hud.SetCameraMode(false);
                hudWas = hud.IsVisible;
                hud.SlideAway();
            }
            GameUiPanel.Toasts.Hold("close", true);
            MomentPlayer.Instance.HideSaid();

            var lead = sim.HolderOf(SimCore.LeaderOf(sim.State.era)) ?? sim.State.notables.FindLast(n => n.alive && n.age >= 16f);
            speakerId = lead != null ? lead.id : -1;
            Speaker = lead != null ? lead.name : "";
            FindFire(sim);
            if (cam != null) { cam.CancelFlight(); cam.EaseTo(fire, cam.yaw, GatherPitch, GatherZoom, CloseRules.LeavingSeconds + CloseRules.GatherSeconds); }

            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null)
            {
                PeopleView.SetHourGently(EveningHour);
                var now = pv.Now;
                pv.Planner.ClearLive();
                pv.Planner.StartScene(SceneKind.EveningTalk, now, speakerId, hours: LifeClock.ViewHoursFor(now.hour, CloseRules.TotalSeconds + 30f));
            }
            t = 0f; said = false; asked = false; Line = null; LeftEarly = false; homeOnLift = false;
            pressed = ReadPress();
            Step = CloseStep.Leaving;
            Debug.Log("[Close] the evening talk starts");
            return true;
        }

        void FindFire(SimCore sim)
        {
            fire = new Vector3(0f, 1f, 0f);
            var st = sim.Settlement;
            var box = FindAnyObjectByType<BoxView>();
            if (st != null && box != null) fire = box.transform.TransformPoint(new Vector3(st.Map.greenX, 0f, st.Map.greenZ));
        }

        /// <summary>Is anything else up? The beats of a check-in aren't spent, and the debug start would talk over it.</summary>
        public static bool Busy()
        {
            var hud = GameHud.Instance;
            if (hud == null || !hud.IsVisible || hud.SheetOpen || hud.CleanMode || hud.CameraMode || hud.PeekUp || hud.StoriesUp) return true;
            var moment = MomentPlayer.Instance;
            if (moment == null || moment.Playing || MomentPlayer.PromptsBlocked()) return true;
            var done = BoxCompleteController.Instance;
            if (done != null && (done.Playing || done.ShelfOpen || done.PickerOpen)) return true;
            var book = BookView.Instance;
            if (book != null && book.IsOpen) return true;
            var clock = GameClock.Instance;
            return clock == null || clock.Visiting || FirstRunView.Instance != null && FirstRunView.Instance.Active;
        }

        void Watch()
        {
            var sim = Sim;
            if (sim == null) return;
            var fr = FirstRunView.Instance;
            if (fr != null && fr.Active) sawFirstRun = true;
            bool down = ReadPress();
            if (down || Busy()) lastActive = Time.unscaledTime;
            if (!AutoStart) return;
            var c = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            var ask = new CloseAsk
            {
                sessionSeconds = GameClock.Instance.SessionSeconds + SessionBonus,
                sinceLastClose = Time.unscaledTime - lastClose,
                idleSeconds = Time.unscaledTime - lastActive,
                zoom = c != null ? c.zoom : 1f,
                touching = down || (c != null && c.Touching),
                busy = Busy(),
                firstSitting = sawFirstRun && !sim.State.storyFlags.Contains(CloseRules.FlagFirstClose),
            };
            if (CloseRules.ShouldStart(ask)) Begin();
        }

        // ---------------- each frame ----------------
        void Update()
        {
            if (!Open)
            {
                if (!ReadPress()) { swallowing = false; if (homeOnLift) GoHome(); }
                Watch();
                return;
            }
            t += Time.unscaledDeltaTime;
            bool down = ReadPress();
            bool press = down && !pressed;
            pressed = down;
            var moment = MomentPlayer.Instance;
            if (press && !(moment != null && moment.NotifyAskUp)) { Leave(); return; }

            var next = CloseRules.StepAt(t);
            if (next != Step) Enter(next);
            if (Step >= CloseStep.Shelf && Step <= CloseStep.Rest && !DayLightDriver.Driving) // the hour has them on already
                NightLamps.SetLevel(Mathf.Clamp01((t - CloseRules.TotalSeconds + CloseRules.ShelfSeconds) / CloseRules.ShelfSeconds) * LampLevel);
            PlaceTag();
            if (asked && !(moment != null && moment.NotifyAskUp)) Leave(); // answered: the HUD comes back, Watch with it
        }

        void Enter(CloseStep step)
        {
            Step = step;
            Debug.Log("[Close] " + step);
            if (step == CloseStep.Speak) Speak();
            if (step == CloseStep.Shelf)
            {
                GlassMotion.Hide(speakerTag, GlassMotion.FadeOff, GlassMotion.FadeMs);
                if (cam != null) cam.EaseTo(new Vector3(0f, 1f, 0f), ShelfYaw, ShelfPitch, cam.maxZoom, CloseRules.ShelfSeconds);
                AskIfFirst();
            }
        }

        void Speak()
        {
            var sim = Sim;
            if (sim == null || said) return;
            said = true;
            bool waits = sim.State.eraReady && !sim.IsFinalEra;
            string teaser = forcedLine ?? (waits ? null : sim.OpenTeaser() ?? sim.CurrentTeaser);
            Line = forcedLine ?? CloseRules.LastLine(waits, EraWaiting.CloseLine, teaser);
            bool first = !sim.State.storyFlags.Contains(CloseRules.FlagFirstClose);
            if (first) sim.State.storyFlags.Add(CloseRules.FlagFirstClose);
            if (!string.IsNullOrEmpty(Line)) MomentPlayer.Instance.Overlay.SetCaption(Line, first ? CloseRules.FirstCloseLine : null);
            if (!string.IsNullOrEmpty(Speaker))
            {
                speakerTag.Word = Speaker;
                GlassMotion.Show(speakerTag, GlassMotion.FadeOff);
            }
            GameClock.Instance.Save();
        }

        void AskIfFirst()
        {
            var sim = Sim;
            if (sim == null || !sim.State.eraReady || sim.IsFinalEra || sim.State.storyFlags.Contains(MomentContext.FlagNotifyAsked)) return;
            asked = MomentPlayer.Instance.AskAtClose();
        }

        void PlaceTag()
        {
            if (speakerTag.style.display == DisplayStyle.None || speakerId < 0 || root.panel == null) return;
            var pv = PeopleView.Find();
            var c = Camera.main;
            if (pv == null || c == null || !pv.TryGetDollPosition(PersonRef.Notable(speakerId), out var p)) return;
            var world = pv.transform.TransformPoint(new Vector3(p.x, TagHeight, p.y));
            var box = FindAnyObjectByType<BoxView>();
            if (box != null) world.y += box.GroundHeight(p.x, p.y);
            var s = c.WorldToScreenPoint(world);
            var panel = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(s.x, Screen.height - s.y));
            speakerTag.style.left = panel.x;
            speakerTag.style.top = panel.y;
        }

        // ---------------- leaving ----------------
        /// <summary>The player touched: everything comes back. Before the line it was never said, and nothing was promised.</summary>
        public void Leave()
        {
            if (!Open) return;
            LeftEarly = !said;
            homeOnLift = cam != null;
            swallowing = ReadPress();
            if (cam != null) cam.CancelFlight(); // the touch is the HUD's, so it wouldn't stop the talk's own flight: it stops here and goes home from where it got to
            Finish();
        }

        void GoHome()
        {
            if (cam == null || !cam.isActiveAndEnabled) { homeOnLift = false; return; }
            if (cam.IsFlying) return; // someone else's flight: home waits for it to stop or land (tester run 6)
            homeOnLift = false;
            cam.GoHome(firm: true);
        }

        void Finish()
        {
            Step = CloseStep.None;
            lastClose = Time.unscaledTime;
            lastActive = Time.unscaledTime;
            forcedLine = null;
            if (!DayLightDriver.Driving) NightLamps.SetLevel(0f);
            GameUiPanel.Toasts.Hold("close", false);
            speakerTag.style.display = DisplayStyle.None;
            if (MomentPlayer.Instance != null) MomentPlayer.Instance.Overlay?.HideCaption();
            var hud = GameHud.Instance;
            if (hud != null) hud.SetVisible(hudWas);
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null) pv.Planner.ClearLive();
            Debug.Log("[Close] over");
        }

        // ---------------- input ----------------
        static bool ReadPress()
        {
            var ts = Touchscreen.current;
            if (ts != null) foreach (var touch in ts.touches) if (touch.press.isPressed) return true;
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
        }

        // ---------------- debug (debug panel, `script eval`) ----------------
        public static string Status()
        {
            var v = Instance;
            if (v == null) return "no EveningTalk";
            return v.Open
                ? $"close: {v.Step} · {v.t:0.0}/{CloseRules.TotalSeconds:0.0} s · {v.Speaker} · {v.Line}"
                : $"close: rests · sitting {(GameClock.Instance != null ? GameClock.Instance.SessionSeconds + v.SessionBonus : 0):0} s · {(Busy() ? "beats not spent" : "beats spent")}";
        }

        /// <summary>Debug: start the close with the sim's own line.</summary>
        public static string DebugStart() => Instance != null && Instance.Begin() ? "the close starts" : "cannot start the close";

        /// <summary>Debug: force one teaser kind (<see cref="ThreadSource"/>) and start the close on it.</summary>
        public static string DebugTeaser(string source)
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null || Instance == null) return "no sim";
            string line = sim.DebugForceTeaser(source);
            if (line == null) return "no " + source + " teaser here";
            return Instance.Begin(line) ? line : "cannot start the close";
        }

        /// <summary>Debug: count this sitting as long, so stepping back or 20 s idle starts the close.</summary>
        public static string DebugLongSitting()
        {
            if (Instance == null) return "no EveningTalk";
            Instance.SessionBonus = CloseRules.LongSittingSeconds + 1.0;
            AutoStart = true;
            return "this sitting counts as long";
        }

        public static string DebugLeave()
        {
            if (Instance == null) return "no EveningTalk";
            Instance.Leave();
            return "the close left";
        }
    }
}
