using System.Globalization;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// The day-3 recall question (#100, vertical-slice-plan §5.4): "Which stories from your box do you remember?" with a number
    /// to tap. A <see cref="GlassSheet"/> on the bottom edge, in the thumb zone, with no scrim (#166, board 3.3). When to ask is
    /// <see cref="RecallPrompt.ShouldAsk"/>; this class only gathers the context, shows the sheet, and saves the
    /// <see cref="RecallState"/> next to the telemetry files. The answer is logged as a number only (never free text).
    /// Stable element names for tests: <c>recall-sheet</c>, <c>recall-0</c> .. <c>recall-4</c>, <c>recall-dismiss</c>.
    /// </summary>
    public class RecallPromptView : MonoBehaviour
    {
        public static RecallPromptView Instance { get; private set; }
        /// <summary>Tests: set false to keep the bootstrap from adding the view to the scene.</summary>
        public static bool AutoCreate = true;
        /// <summary>How often the conditions are checked (real seconds).</summary>
        public float checkSeconds = 2f;

        public RecallState State { get; private set; } = new RecallState();
        public bool IsOpen => sheet != null && sheet.IsOpen;
        public VisualElement Root => root;

        UIDocument doc;
        VisualElement root;
        GlassSheet sheet;
        GlassCard card;
        VisualElement storyAnswers, choiceAnswers;

        /// <summary>P7 (#225): with dilemmas on (variant B) the question is the dilemma diary's, asked on day 2 (window 2 to 4):
        /// <see cref="DilemmaSurvey.RecallQuestion"/>, answered "I can" (1) or "Not really" (0). Off, the day-3 story count.</summary>
        public static bool AsksAboutChoices => GameClock.Instance != null && GameClock.Instance.config.dilemmas;
        static int FirstDay => AsksAboutChoices ? RecallPrompt.DilemmaFirstDay : RecallPrompt.FirstDay;
        static int LastDay => AsksAboutChoices ? RecallPrompt.DilemmaLastDay : RecallPrompt.LastDay;
        Rect lastSafe;
        Vector2Int lastScreen;
        float nextCheck;
        bool loaded;
        // #328: the calm-moment tracking (per frame, so a short tap between two checks still counts)
        float lastTouchTime, lastBusyTime;
        bool askWhenCalm; // debug: show at the next calm moment, whatever the day and story count say
        /// <summary>Real seconds with nothing on screen and no touch before the question may show (tests shorten it).</summary>
        public float calmSeconds = (float)RecallCalm.CalmSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static RecallPromptView Create() => new GameObject("RecallPromptView").AddComponent<RecallPromptView>();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 90; // over the HUD shell, under the Feedback sheet
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Build(doc.rootVisualElement);
            UiInput.Add(IsOverUi);
        }

        void OnDisable() { if (Instance == this) UiInput.Remove(IsOverUi); }
        void OnDestroy() { if (Instance == this) Instance = null; }
        bool IsOverUi(Vector2 p) => IsOpen && GameUiPanel.Over(root, p);

        // ---------------- when ----------------
        string StatePath => GameClock.Instance?.TelemetryDir == null ? null : Path.Combine(GameClock.Instance.TelemetryDir, "recall.json");

        void LoadState()
        {
            loaded = true;
            try { var p = StatePath; if (p != null && File.Exists(p)) State = JsonUtility.FromJson<RecallState>(File.ReadAllText(p)) ?? new RecallState(); }
            catch (System.Exception) { State = new RecallState(); }
        }

        void SaveState()
        {
            try { var p = StatePath; if (p != null) File.WriteAllText(p, JsonUtility.ToJson(State)); }
            catch (System.Exception e) { Debug.LogWarning("[Recall] could not save state: " + e.Message); }
        }

        /// <summary>The day since install in the phone's own calendar, on the telemetry clock (debug can move it).</summary>
        public static int DayIndex()
        {
            var log = GameClock.Tel;
            if (log == null) return 0;
            int tz = (int)System.TimeZoneInfo.Local.GetUtcOffset(GameClock.TelemetryNow).TotalMinutes;
            return TelemetryAnalyzer.DayIndex(log.InstallUtc, GameClock.TelemetryNow, tz);
        }

        /// <summary>Something that should not be covered by a question is on screen.</summary>
        public static bool Busy()
        {
            var hud = GameHud.Instance;
            if (hud != null && !string.IsNullOrEmpty(hud.OpenSheet)) return true;
            if (CardStackView.Instance != null && CardStackView.Instance.Showing) return true; // same place on the screen, and the story comes first
            if (FeedbackUI.Instance != null && FeedbackUI.Instance.IsOpen) return true;
            if (MomentPlayer.Instance != null && (MomentPlayer.Instance.Playing || GameClock.Instance != null && GameClock.Instance.Held)) return true;
            if (PersonCardView.Instance != null && PersonCardView.Instance.AnyOpen) return true;
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return true;
            var sm = ShowMeDirector.Instance;
            if (sm != null && sm.State != ShowMeDirector.Phase.Idle) return true;
            var fr = FindAnyObjectByType<FirstRunView>();
            if (fr != null && fr.Step != AquariumCiv.Sim.FirstRunStep.Done) return true;
            return false;
        }

        /// <summary>#328: the HUD's state as the pure rule wants it (read only). Timers come from the per-frame tracking.</summary>
        public RecallCalmState CalmState()
        {
            var hud = GameHud.Instance;
            var sm = ShowMeDirector.Instance;
            var fr = FindAnyObjectByType<FirstRunView>();
            var tl = TimeLapseView.Find();
            float now = Time.unscaledTime;
            return new RecallCalmState
            {
                sheetOpen = hud != null && (!string.IsNullOrEmpty(hud.OpenSheet) || hud.OutsideSheet != null && hud.OutsideSheet != sheet),
                toolInHand = hud != null && (hud.Hand != GameHud.KitTool.None || hud.CleanMode || hud.ChipLaneTaken)
                    || CareView.Instance != null && (CareView.Instance.IsWiping || CareView.Instance.IsGrabbing),
                firstRunStep = fr != null && fr.Step != AquariumCiv.Sim.FirstRunStep.Done,
                momentPlaying = MomentPlayer.Instance != null && (MomentPlayer.Instance.Playing || GameClock.Instance != null && GameClock.Instance.Held)
                    || sm != null && (sm.State != ShowMeDirector.Phase.Idle || sm.CaptionUp)
                    || CardStackView.Instance != null && CardStackView.Instance.Showing
                    || PersonCardView.Instance != null && PersonCardView.Instance.AnyOpen
                    || tl != null && tl.Active,
                captionOrToast = GameUiPanel.Toasts.Current != null || GameUiPanel.Toasts.Waiting > 0,
                anotherPromptUp = IsOpen || FeedbackUI.Instance != null && FeedbackUI.Instance.IsOpen,
                secondsSinceTouch = now - lastTouchTime,
                secondsSinceBusy = now - lastBusyTime,
            };
        }

        /// <summary>Per frame: note a finger on the screen, and the last frame anything was in the way.</summary>
        void TrackCalm()
        {
            float now = Time.unscaledTime;
            bool touching = false;
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null) foreach (var t in ts.touches) if (t.isInProgress) { touching = true; break; }
            var m = UnityEngine.InputSystem.Mouse.current;
            if (!touching && m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed)) touching = true;
            if (touching) lastTouchTime = now;
            if (RecallCalm.Busy(CalmState())) lastBusyTime = now;
        }

        /// <summary>Stories the tester has had a chance to recall: cards, Show me, person cards, history opened.</summary>
        static int StoryTouches(TelemetryLog log)
        {
            var r = TelemetryAnalyzer.Analyze(log.ReadAllLines(), GameClock.TelemetryNow);
            return r.cardsShown + r.showMe + r.cardsOpened + r.historyOpens + r.timelapseOpens;
        }

        public RecallContext Context(bool withStories = true)
        {
            var log = GameClock.Tel;
            return new RecallContext
            {
                dayIndex = DayIndex(),
                sessionSeconds = GameClock.Instance != null ? GameClock.Instance.SessionSeconds : 0,
                busy = Busy() || IsOpen,
                cardsEverShown = log == null ? 0 : withStories ? StoryTouches(log) : RecallPrompt.MinCardsShown,
            };
        }

        void Update()
        {
            if (Instance == this && GameClock.Tel != null) TrackCalm();
            if (sheet != null && (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y)) ApplySafeArea();
            if (Time.unscaledTime < nextCheck || GameClock.Tel == null) return;
            nextCheck = Time.unscaledTime + checkSeconds;
            if (!loaded) LoadState();
            if (IsOpen) return;
            if (askWhenCalm) { if (CalmFor()) { askWhenCalm = false; Show(); } return; }
            // cheap checks first; reading the log (stories seen) only when everything else says yes
            if (!RecallPrompt.ShouldAsk(State, Context(false), FirstDay, LastDay)) return;
            if (!RecallPrompt.ShouldAsk(State, Context(true), FirstDay, LastDay)) return;
            if (!CalmFor()) return; // #328: wait for a calm moment, ask again at the next check
            Show();
        }

        bool CalmFor()
        {
            var s = CalmState();
            return !RecallCalm.Busy(s) && s.secondsSinceTouch >= calmSeconds && s.secondsSinceBusy >= calmSeconds;
        }

        // ---------------- the sheet ----------------
        /// <summary>Shows the question now (the decision was made, or a debug force).</summary>
        public void Show()
        {
            if (sheet == null || IsOpen) return;
            if (!loaded) LoadState();
            int day = DayIndex();
            RecallPrompt.Shown(State, day);
            SaveState();
            GameClock.Tel.RecallShown(day);
            Fill(AsksAboutChoices);
            root.style.display = DisplayStyle.Flex;
            sheet.Open();
            GameHud.Instance?.SetOutsideSheet(sheet, true); // the tools step back and a toast rides above it; the name stays
        }

        void Close()
        {
            var hud = GameHud.Instance;
            if (hud != null && hud.OutsideSheet == sheet) hud.SetOutsideSheet(null);
            sheet.Close(() => { if (!sheet.IsOpen) root.style.display = DisplayStyle.None; });
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            var i = GameUiPanel.SafeInsets(lastSafe, Screen.width, Screen.height);
            sheet.SetInsets(i, GlassSheet.IsShort(Screen.width, Screen.height) ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
        }

        void Answer(int count)
        {
            Close();
            RecallPrompt.Answered(State);
            SaveState();
            GameClock.Tel.RecallAnswered(count);
            GameClock.Tel?.Flush();
            GameHud.Instance?.Toast(choiceAnswers != null && choiceAnswers.style.display == DisplayStyle.Flex && count > 0
                ? "Thank you. Jot it down for us, and what came of it." : "Thank you. That helps a lot.", 3f);
        }

        void Dismiss()
        {
            Close();
            RecallPrompt.Dismissed(State);
            SaveState();
            GameClock.Tel.RecallDismissed();
        }

        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "recall-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            root.style.display = DisplayStyle.None;
            parent.Add(root);

            sheet = new GlassSheet { name = "recall-sheet" };
            sheet.style.display = DisplayStyle.None;
            root.Add(sheet);

            card = new GlassCard { name = "recall-card" };
            card.Dots.style.display = DisplayStyle.None;
            card.Head.Add(new GlassButton("Not now", Dismiss, ButtonKind.Quiet, UiRole.Close) { name = "recall-dismiss" });
            card.Actions.name = "recall-answers";
            storyAnswers = new VisualElement { name = "recall-stories", pickingMode = PickingMode.Ignore };
            storyAnswers.style.flexDirection = FlexDirection.Row;
            storyAnswers.style.flexGrow = 1;
            for (int i = 0; i <= 4; i++)
            {
                int n = i;
                var b = new GlassButton(i == 4 ? "4+" : i.ToString(), () => Answer(n), ButtonKind.Secondary, UiRole.Confirm) { name = "recall-" + i };
                b.AddToClassList("recall-answer");
                if (i == 4) b.AddToClassList("recall-answer--last");
                storyAnswers.Add(b);
            }
            card.Actions.Add(storyAnswers);
            // P7's diary question (#225): a yes or no in the game, the words themselves on the tester's sheet (never in the log)
            choiceAnswers = new VisualElement { name = "recall-choices", pickingMode = PickingMode.Ignore };
            choiceAnswers.style.flexDirection = FlexDirection.Row;
            choiceAnswers.style.flexGrow = 1;
            var yes = new GlassButton("I can", () => Answer(1), ButtonKind.Secondary, UiRole.Confirm) { name = "recall-choice-yes" };
            var no = new GlassButton("Not really", () => Answer(0), ButtonKind.Secondary, UiRole.Confirm) { name = "recall-choice-no" };
            yes.AddToClassList("recall-answer");
            no.AddToClassList("recall-answer"); no.AddToClassList("recall-answer--last");
            choiceAnswers.Add(yes); choiceAnswers.Add(no);
            card.Actions.Add(choiceAnswers);
            Fill(false);
            sheet.Add(card);
            ApplySafeArea();
        }

        /// <summary>The words and answers: the day-3 story count, or P7's choice recall (variant B).</summary>
        void Fill(bool choices)
        {
            if (card == null) return;
            card.Set("A quick question", choices
                ? "Without looking: " + char.ToLowerInvariant(DilemmaSurvey.RecallQuestion[0]) + DilemmaSurvey.RecallQuestion.Substring(1) + " Can you?"
                : "Without looking: how many stories from your box can you remember? Tap a number.");
            storyAnswers.style.display = choices ? DisplayStyle.None : DisplayStyle.Flex;
            choiceAnswers.style.display = choices ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------- debug (the dbg panel, `script eval`) ----------------
        /// <summary>Moves the telemetry clock so today is day <paramref name="day"/> since install.</summary>
        public static string DebugSetDay(int day)
        {
            if (GameClock.Tel == null) return "no telemetry running";
            GameClock.DebugDayOffset += day - DayIndex();
            return $"telemetry day is now {DayIndex()} (clock offset {GameClock.DebugDayOffset:0} d)";
        }

        public static string DebugAddDays(int days)
        {
            GameClock.DebugDayOffset += days;
            return $"telemetry day is now {DayIndex()} (clock offset {GameClock.DebugDayOffset:0} d)";
        }

        /// <summary>Shows the question now, ignoring the day and the story count (it still logs and saves as a real ask).</summary>
        public static string DebugForce()
        {
            var v = Instance;
            if (v == null) return "no RecallPromptView";
            v.Show();
            return v.IsOpen ? "recall question shown" : "could not show";
        }

        /// <summary>#328: the question at the next calm moment (not over the Kit, a tool, a step, a toast), ignoring the day and story count.</summary>
        public static string DebugAskWhenCalm()
        {
            var v = Instance;
            if (v == null) return "no RecallPromptView";
            v.askWhenCalm = true;
            return "recall question waits for a calm moment";
        }

        public static string DebugReset()
        {
            var v = Instance;
            if (v == null) return "no RecallPromptView";
            v.State = new RecallState(); v.loaded = true; v.SaveState();
            if (v.IsOpen) v.Close();
            return "recall state cleared";
        }

        /// <summary>The week report from the log (text), logged to the console too.</summary>
        public static string DebugReport()
        {
            var log = GameClock.Tel;
            if (log == null) return "no telemetry running";
            var text = TelemetryAnalyzer.Analyze(log.ReadAllLines(), GameClock.TelemetryNow).ToText();
            Debug.Log("[Telemetry] " + text);
            return text.Split('\n')[0] + " (full report in the console)";
        }

        public static string Status()
        {
            var v = Instance;
            var s = v?.State;
            return string.Format(CultureInfo.InvariantCulture, "recall: day {0} · {1} · shown {2} dismissed {3} answered {4} · asks {5} (days {6}-{7})",
                DayIndex(), v != null && v.IsOpen ? "OPEN" : "closed", s?.shown, s?.dismissed, s?.answered, AsksAboutChoices ? "P7 choice" : "stories", FirstDay, LastDay);
        }
    }
}
