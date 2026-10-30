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
    /// to tap. A small sheet in the #36 style (Plaster card, Timber text, Terracotta action). When to ask is
    /// <see cref="RecallPrompt.ShouldAsk"/>; this class only gathers the context, shows the sheet, and saves the
    /// <see cref="RecallState"/> next to the telemetry files. The answer is logged as a number only (never free text).
    /// Stable element names for tests: <c>recall-sheet</c>, <c>recall-0</c> .. <c>recall-4</c>, <c>recall-dismiss</c>.
    /// </summary>
    public class RecallPromptView : MonoBehaviour
    {
        static readonly Color Plaster = Hex("#EFE3CC"), Timber = Hex("#6A4B36"), Terracotta = Hex("#C66F4E"), Frame = Hex("#2E2A27"), Canvas = Hex("#E2D3B3");
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static RecallPromptView Instance { get; private set; }
        /// <summary>Tests: set false to keep the bootstrap from adding the view to the scene.</summary>
        public static bool AutoCreate = true;
        /// <summary>How often the conditions are checked (real seconds).</summary>
        public float checkSeconds = 2f;

        public RecallState State { get; private set; } = new RecallState();
        public bool IsOpen => sheet != null && sheet.style.display == DisplayStyle.Flex;
        public VisualElement Root => root;

        UIDocument doc;
        VisualElement root, sheet;
        float nextCheck;
        bool loaded;

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
            if (FeedbackUI.Instance != null && FeedbackUI.Instance.IsOpen) return true;
            if (MomentPlayer.Instance != null && (MomentPlayer.Instance.Playing || GameClock.Instance != null && GameClock.Instance.Held)) return true;
            if (PersonCardView.Instance != null && PersonCardView.Instance.IsOpen) return true;
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return true;
            var sm = ShowMeDirector.Instance;
            if (sm != null && sm.State != ShowMeDirector.Phase.Idle) return true;
            var fr = FindAnyObjectByType<FirstRunView>();
            if (fr != null && fr.Step != AquariumCiv.Sim.FirstRunStep.Done) return true;
            return false;
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
            if (Time.unscaledTime < nextCheck || GameClock.Tel == null) return;
            nextCheck = Time.unscaledTime + checkSeconds;
            if (!loaded) LoadState();
            if (IsOpen) return;
            // cheap checks first; reading the log (stories seen) only when everything else says yes
            if (!RecallPrompt.ShouldAsk(State, Context(false))) return;
            if (!RecallPrompt.ShouldAsk(State, Context(true))) return;
            Show();
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
            sheet.style.display = DisplayStyle.Flex;
        }

        void Answer(int count)
        {
            sheet.style.display = DisplayStyle.None;
            RecallPrompt.Answered(State);
            SaveState();
            GameClock.Tel.RecallAnswered(count);
            GameClock.Tel?.Flush();
            GameHud.Instance?.Toast("Thank you. That helps a lot.", 3f);
        }

        void Dismiss()
        {
            sheet.style.display = DisplayStyle.None;
            RecallPrompt.Dismissed(State);
            SaveState();
            GameClock.Tel.RecallDismissed();
        }

        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "recall-root", pickingMode = PickingMode.Ignore };
            Fill(root);
            parent.Add(root);

            sheet = new VisualElement { name = "recall-sheet", pickingMode = PickingMode.Position };
            Fill(sheet);
            sheet.style.backgroundColor = new Color(0.12f, 0.1f, 0.09f, 0.55f);
            sheet.style.display = DisplayStyle.None;
            root.Add(sheet);

            float scale = 1080f / Mathf.Max(1, Screen.width);
            float topInset = (Screen.height - Screen.safeArea.yMax) * scale;
            var card = new VisualElement { name = "recall-card" };
            card.style.position = Position.Absolute;
            card.style.left = 40; card.style.right = 40; card.style.top = 300 + topInset;
            Pad(card, 36); Round(card, 36);
            card.style.backgroundColor = Plaster;
            sheet.Add(card);

            card.Add(Label("A quick question", 52, Timber, FontStyle.Bold));
            var hint = Label("Without looking: how many stories from your box can you remember? Tap a number.", 34, Timber, FontStyle.Normal);
            hint.style.marginTop = 8; hint.style.marginBottom = 26; hint.style.whiteSpace = WhiteSpace.Normal;
            card.Add(hint);

            var row = new VisualElement { name = "recall-answers" };
            row.style.flexDirection = FlexDirection.Row;
            for (int i = 0; i <= 4; i++)
            {
                int n = i;
                var b = Pill("recall-" + i, i == 4 ? "4+" : i.ToString(), Terracotta, Plaster, 48);
                b.style.flexGrow = 1; b.style.height = 124; b.style.marginRight = i < 4 ? 14 : 0;
                b.clicked += () => Answer(n);
                row.Add(b);
            }
            card.Add(row);

            var no = Pill("recall-dismiss", "Not now", Canvas, Timber, 40);
            no.style.height = 112; no.style.marginTop = 22;
            no.clicked += Dismiss;
            card.Add(no);
        }

        // ---------------- tiny UI helpers (this sheet's own; the HUD's are private to its assembly) ----------------
        static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute; e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }
        static void Pad(VisualElement e, float v) { e.style.paddingLeft = e.style.paddingRight = e.style.paddingTop = e.style.paddingBottom = v; }
        static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }
        static Label Label(string t, float size, Color c, FontStyle style)
        {
            var l = new Label(t);
            l.style.fontSize = size; l.style.color = c; l.style.unityFontStyleAndWeight = style;
            return l;
        }
        static Button Pill(string name, string text, Color bg, Color fg, float size)
        {
            var b = new Button { name = name, text = text };
            b.style.backgroundColor = bg; b.style.color = fg; b.style.fontSize = size;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            Round(b, 40);
            return b;
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

        public static string DebugReset()
        {
            var v = Instance;
            if (v == null) return "no RecallPromptView";
            v.State = new RecallState(); v.loaded = true; v.SaveState();
            if (v.sheet != null) v.sheet.style.display = DisplayStyle.None;
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
            return string.Format(CultureInfo.InvariantCulture, "recall: day {0} · {1} · shown {2} dismissed {3} answered {4}",
                DayIndex(), v != null && v.IsOpen ? "OPEN" : "closed", s?.shown, s?.dismissed, s?.answered);
        }
    }
}
