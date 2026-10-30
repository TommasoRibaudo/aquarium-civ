using System;
using System.Globalization;
using AquariumCiv.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The slice's HUD shell (#36, first-session-and-check-in §6): nearly empty, the world is the screen. Top: the box's
    /// name and year, small; a tap opens Settings. Bottom row, in the thumb zone: History (the chronicle, M4), the pinned
    /// person's chip (hidden until someone is pinned), Shape (the shaping tray, M5). Each opens a bottom sheet; one sheet
    /// at a time, closed by its Close button, a tap on the dimmed world, or Back. Everything sits inside the safe area.
    /// UI Toolkit built in code and styled by Resources/GameUI/Game.uss; every control has a stable name so tests and
    /// the tester agent find it (<c>hud-title</c>, <c>hud-history</c>, <c>hud-shape</c>, <c>hud-pin</c>,
    /// <c>hud-feedback</c>, <c>settings</c>/<c>history</c>/<c>shaping</c> + <c>-sheet</c>, <c>-scrim</c>, <c>-close</c>,
    /// <c>settings-sound</c>, <c>settings-haptics</c>, <c>settings-feedback</c>).
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public const string Settings = "settings", History = "history", Shaping = "shaping";

        public static GameHud Instance { get; private set; }
        /// <summary>Set false (tests) to keep the bootstrap from adding the HUD to the scene.</summary>
        public static bool AutoCreate = true;

        /// <summary>Raised with the sheet's name when one opens, and with null when it closes.</summary>
        public event Action<string> SheetChanged;
        /// <summary>Raised when clean mode (one finger wipes the glass and flicks the mess, #37) is switched on or off.</summary>
        public event Action<bool> CleanModeChanged;

        UIDocument doc;
        VisualElement root, safe, bottomRow, openSheet;
        Label nameLabel, yearLabel;
        Button pinChip, soundSwitch, hapticsSwitch, cleanButton;
        Label toastLabel;
        float toastUntil;
        bool cleanMode;
        Label historyBody;
        Rect lastSafe;
        Vector2Int lastScreen;
        float nextText;
        bool hidden;

        public VisualElement Root => root;
        public string OpenSheet => openSheet?.name.Replace("-scrim", "");
        public bool IsVisible => !hidden;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static GameHud Create() => new GameObject("GameHud").AddComponent<GameHud>();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            GameSettings.Apply();
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 10; // under the feedback sheet
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Build(doc.rootVisualElement);
            UiInput.Add(IsOverUi);
            FeedbackUI.HostedByHud = true;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            UiInput.Remove(IsOverUi);
            FeedbackUI.HostedByHud = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Picking blockers over the IMGUI debug panels (UiInput.DebugBlocks), so taps there never reach HUD buttons below.
        readonly VisualElement[] blockers = new VisualElement[2];
        public bool SheetOpen => openSheet != null;

        void UpdateBlockers()
        {
            float k = GameUiPanel.ReferenceWidth / Mathf.Max(1f, Screen.width);
            for (int i = 0; i < blockers.Length; i++)
            {
                var b = blockers[i];
                if (b == null) continue;
                if (i < UiInput.DebugBlocks.Count)
                {
                    var r = UiInput.DebugBlocks[i];
                    b.style.left = r.x * k; b.style.top = r.y * k; b.style.width = r.width * k; b.style.height = r.height * k;
                    b.style.display = DisplayStyle.Flex;
                }
                else b.style.display = DisplayStyle.None;
            }
        }

        /// <summary>Clean mode (design/one-handed-controls.md): off, one finger always moves the camera; on, one finger wipes the
        /// glass and flicks the mess. The Clean button toggles it (it reads "Done" and is lit while on).</summary>
        public bool CleanMode => cleanMode;

        public void SetCleanMode(bool on)
        {
            if (cleanMode == on) return;
            cleanMode = on;
            if (cleanButton != null)
            {
                cleanButton.text = on ? "Done" : "Clean";
                cleanButton.EnableInClassList("hud-btn--on", on);
            }
            CleanModeChanged?.Invoke(on);
        }

        /// <summary>A short line above the bottom row (hints, "all clean"); replaces the one showing.</summary>
        public void Toast(string text, float seconds = 3f)
        {
            if (toastLabel == null) return;
            toastLabel.text = text;
            toastLabel.style.display = DisplayStyle.Flex;
            toastUntil = Time.unscaledTime + seconds;
        }

        bool IsOverUi(Vector2 screenPos) => !hidden && GameUiPanel.Over(root, screenPos);

        void Update()
        {
            if (root == null) return;
            if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y) ApplySafeArea();
            UpdateBlockers();
            if (toastLabel != null && toastLabel.resolvedStyle.display == DisplayStyle.Flex && Time.unscaledTime >= toastUntil) toastLabel.style.display = DisplayStyle.None;
            if (Time.unscaledTime >= nextText) { nextText = Time.unscaledTime + 0.5f; RefreshText(); }
            // Android Back / Esc closes the open sheet.
            if (openSheet != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseSheet();
        }

        // ---------------- building ----------------
        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "hud-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            parent.Add(root);

            safe = new VisualElement { name = "hud-safe", pickingMode = PickingMode.Ignore };
            safe.AddToClassList("hud-fill");
            safe.AddToClassList("hud-safe");
            root.Add(safe);

            var top = Row("hud-top");
            var title = new Button(() => Open(Settings)) { name = "hud-title" };
            title.AddToClassList("hud-title");
            title.Add(nameLabel = Text("Your box", "hud-title-name"));
            title.Add(yearLabel = Text("Year 0", "hud-title-year"));
            nameLabel.pickingMode = yearLabel.pickingMode = PickingMode.Ignore;
            top.Add(title);
            safe.Add(top);

            if (FeedbackUI.InTestBuild)
            {
                var corner = new VisualElement { name = "hud-corner", pickingMode = PickingMode.Ignore };
                corner.AddToClassList("hud-corner");
                corner.Add(Btn("hud-feedback", "Feedback", OpenFeedback, "hud-btn", "hud-btn--small"));
                safe.Add(corner);
            }

            bottomRow = Row("hud-bottom");
            bottomRow.Add(Btn("hud-history", "History", () => Open(History), "hud-btn"));
            pinChip = Btn("hud-pin", "", null, "hud-chip");
            pinChip.style.display = DisplayStyle.None; // nobody pinned yet (person card work)
            bottomRow.Add(pinChip);
            cleanButton = Btn("hud-clean", "Clean", () => SetCleanMode(!cleanMode), "hud-btn");
            bottomRow.Add(cleanButton);
            bottomRow.Add(Btn("hud-shape", "Shape", () => Open(Shaping), "hud-btn", "hud-btn--primary"));
            safe.Add(bottomRow);

            toastLabel = Text("", "hud-toast");
            toastLabel.name = "hud-toast";
            toastLabel.pickingMode = PickingMode.Ignore;
            toastLabel.style.display = DisplayStyle.None;
            safe.Add(toastLabel);

            for (int i = 0; i < blockers.Length; i++)
            {
                blockers[i] = new VisualElement { name = "hud-debug-block" + i, pickingMode = PickingMode.Position };
                blockers[i].style.position = Position.Absolute;
                blockers[i].style.display = DisplayStyle.None;
                root.Add(blockers[i]);
            }
            BuildSettings();
            BuildHistory();
            BuildShaping();
            ApplySafeArea();
            RefreshText();
        }

        void BuildSettings()
        {
            var sheet = Sheet(Settings, "Settings");
            soundSwitch = SwitchRow(sheet, "settings-sound", "Sound", () => GameSettings.Sound, v => GameSettings.Sound = v);
            hapticsSwitch = SwitchRow(sheet, "settings-haptics", "Haptics", () => GameSettings.Haptics, v => GameSettings.Haptics = v);
            if (FeedbackUI.InTestBuild) sheet.Add(Btn("settings-feedback", "Send feedback", OpenFeedback, "sheet-btn"));
            var version = Text($"Aquarium Civ {Application.version}", "sheet-note");
            version.style.marginTop = 24;
            sheet.Add(version);
        }

        void BuildHistory()
        {
            var sheet = Sheet(History, "History");
            sheet.Add(historyBody = Text("", "sheet-text"));
            sheet.Add(Text("The chronicle and the time-lapse of the years will live here.", "sheet-note"));
        }

        void BuildShaping()
        {
            var sheet = Sheet(Shaping, "Shape the land");
            var tools = new VisualElement { name = "shaping-tools" };
            tools.AddToClassList("tools");
            var rain = Btn("shaping-rain", "Rain", null, "tool");
            rain.SetEnabled(false);
            tools.Add(rain);
            sheet.Add(tools);
            sheet.Add(Text("Gifts and landscaping arrive with the shaping tools.", "sheet-note"));
        }

        /// <summary>A bottom sheet: a dimmed full-screen scrim (tap closes) holding the sheet with a title and Close.</summary>
        VisualElement Sheet(string id, string title)
        {
            var scrim = new VisualElement { name = id + "-scrim" };
            scrim.AddToClassList("hud-fill");
            scrim.AddToClassList("sheet-scrim");
            scrim.style.display = DisplayStyle.None;
            scrim.RegisterCallback<PointerUpEvent>(e => { if (e.target == scrim) CloseSheet(); });
            root.Add(scrim);

            var sheet = new VisualElement { name = id + "-sheet" };
            sheet.AddToClassList("sheet");
            scrim.Add(sheet);

            var head = new VisualElement();
            head.AddToClassList("sheet-head");
            head.Add(Text(title, "sheet-title"));
            head.Add(Btn(id + "-close", "Close", CloseSheet, "sheet-close"));
            sheet.Add(head);
            return sheet;
        }

        Button SwitchRow(VisualElement sheet, string name, string label, Func<bool> get, Action<bool> set)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.Add(Text(label, "row-label"));
            Button sw = null;
            sw = Btn(name, "", () => { set(!get()); ShowSwitch(sw, get()); }, "switch");
            ShowSwitch(sw, get());
            row.Add(sw);
            sheet.Add(row);
            return sw;
        }

        static void ShowSwitch(Button sw, bool on)
        {
            sw.text = on ? "On" : "Off";
            sw.EnableInClassList("switch--on", on);
        }

        // ---------------- behaviour ----------------
        /// <summary>Open a sheet by name (<see cref="Settings"/>, <see cref="History"/>, <see cref="Shaping"/>); closes any other.</summary>
        public void Open(string id)
        {
            var scrim = root.Q(id + "-scrim");
            if (scrim == null) return;
            if (openSheet != null) openSheet.style.display = DisplayStyle.None;
            openSheet = scrim;
            if (id == Settings) { ShowSwitch(soundSwitch, GameSettings.Sound); ShowSwitch(hapticsSwitch, GameSettings.Haptics); }
            if (id == History) RefreshText();
            scrim.style.display = DisplayStyle.Flex;
            SheetChanged?.Invoke(id);
        }

        public void CloseSheet()
        {
            if (openSheet == null) return;
            openSheet.style.display = DisplayStyle.None;
            openSheet = null;
            SheetChanged?.Invoke(null);
        }

        /// <summary>Hide the whole HUD (feedback screenshots, a clean snapshot, the debug toggle).</summary>
        public void SetVisible(bool visible)
        {
            hidden = !visible;
            if (root != null) root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OpenFeedback()
        {
            CloseSheet();
            var fb = FeedbackUI.Instance != null ? FeedbackUI.Instance : FeedbackUI.Create();
            fb.Open();
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            var i = GameUiPanel.SafeInsets(lastSafe, Screen.width, Screen.height);
            safe.style.left = i.x; safe.style.top = i.y; safe.style.right = i.z; safe.style.bottom = i.w;
            // Sheets run to the screen's bottom edge but keep their content above the home indicator.
            foreach (var id in new[] { Settings, History, Shaping })
            {
                var sheet = root.Q(id + "-sheet");
                sheet.style.paddingBottom = 44 + i.w;
                sheet.style.paddingLeft = 44 + i.x;
                sheet.style.paddingRight = 44 + i.z;
            }
        }

        void RefreshText()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null) return;
            var s = sim.State;
            string place = string.IsNullOrEmpty(s.settlementName) ? "Your box" : s.settlementName;
            nameLabel.text = place;
            yearLabel.text = "Year " + Mathf.FloorToInt(s.Year).ToString(CultureInfo.InvariantCulture);
            if (historyBody != null)
                historyBody.text = $"{place}, year {Mathf.FloorToInt(s.Year)}. {s.population} {(s.population == 1 ? "person lives" : "people live")} here.";
        }

        // ---------------- element helpers ----------------
        VisualElement Row(string name)
        {
            var r = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            r.AddToClassList(name);
            return r;
        }

        static Label Text(string s, string cls)
        {
            var l = new Label(s);
            l.AddToClassList(cls);
            return l;
        }

        static Button Btn(string name, string label, Action onClick, params string[] classes)
        {
            var b = onClick != null ? new Button(onClick) : new Button();
            b.name = name; b.text = label;
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }
    }
}
