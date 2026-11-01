using System;
using System.Globalization;
using AquariumCiv.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The HUD shell (#36, reworked to Keeper's Glass in #108, hierarchy.md D1 to D6): nearly empty, the world is the screen. Top:
    /// the box's name and year as quiet text with a settings mark (a tap opens Settings), Photo and a quieter Feedback as small
    /// icons, Feedback in test builds only. Bottom: three tools of one calm look, History (always the time-lapse), Clean, Shape.
    /// Above them the pin chip (a portrait and the name) and the Stories chip (unread stories). One sheet at a time, closed by
    /// its Close, a tap on the world, or Back. Everything sits inside the safe area, in the lanes of <see cref="GameUiPanel"/>.
    /// Built in code from the components in UI/Glass; every control has a stable name so tests and the tester agent find it
    /// (<c>hud-title</c>, <c>hud-history</c>, <c>hud-clean</c>, <c>hud-shape</c>, <c>hud-pin</c>, <c>hud-history-dot</c> (the Stories chip),
    /// <c>hud-feedback</c>, <c>hud-photo</c>, <c>settings</c>/<c>history</c>/<c>shaping</c> + <c>-sheet</c>, <c>-scrim</c>, <c>-close</c>,
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

        /// <summary>Raised when camera mode (photo mode, #70: all UI hidden, only the box) is switched on or off.</summary>
        public event Action<bool> CameraModeChanged;

        UIDocument doc;
        VisualElement root, safe, topRow, bottomRow, pinRow, outsideSheet;
        GlassScrim openSheet;
        readonly System.Collections.Generic.Dictionary<string, GlassScrim> sheets = new();
        Label nameLabel, yearLabel;
        PinChip pinChip;
        StoriesChip storiesChip;
        GlassToggle soundSwitch, hapticsSwitch;
        GlassButton cleanButton, shapeButton;
        readonly System.Collections.Generic.HashSet<string> toolBlocks = new();
        GlassToast toastLabel;
        bool cleanMode, shapeNew, offHome;
        Label historyBody;
        Rect lastSafe;
        Vector2Int lastScreen;
        float nextText;
        bool hidden;
        bool cameraMode;
        GlassButton exitButton;
        float cameraModeSince, exitFadeAt;
        // The tap that brings the UI back: a short press that stays put, never a drag, never two fingers.
        bool tapDown, tapMoved, tapMulti;
        Vector2 tapStart;
        float tapTime;
        public const float TapSlopFraction = 0.02f, TapMaxSeconds = 0.4f;
        const float TopRowTop = 110f; // 40 pt

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
                cleanButton.EnableInClassList("kg-tool--on", on); // the word, a check and a ring: the mode is never a silent one
            }
            CleanModeChanged?.Invoke(on);
        }

        /// <summary>Camera mode (photo mode, #70): every control, toast and sheet hides (and the IMGUI debug dock, see
        /// DebugDock.Hidden), the camera stays fully usable. A short tap anywhere (not a drag, not a pinch) or the small
        /// "Tap to show the UI" pill brings it all back. Clean mode and any open sheet are closed first.</summary>
        public bool CameraMode => cameraMode;

        public void SetCameraMode(bool on)
        {
            if (cameraMode == on || root == null) return;
            if (on)
            {
                if (FeedbackUI.Instance != null && FeedbackUI.Instance.IsOpen) return;
                CloseSheet();
                SetCleanMode(false);
                DropToasts();
            }
            cameraMode = on;
            safe.EnableInClassList("hud-photo", on);
            exitButton.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            exitButton.style.opacity = 1f;
            cameraModeSince = exitFadeAt = Time.unscaledTime;
            tapDown = false;
            CameraModeChanged?.Invoke(on);
        }

        // Camera mode each frame: the pill fades to a whisper after a few seconds; a tap anywhere brings the UI back.
        void UpdateCameraMode()
        {
            exitButton.style.opacity = Time.unscaledTime - exitFadeAt < 4f ? 1f : 0.45f;
            ReadPointer(out bool down, out int count, out Vector2 pos);
            float slop = Screen.height * TapSlopFraction;
            if (!tapDown)
            {
                // Only a press that begins in this mode counts (not the one that pressed Photo).
                if (down && count == 1 && Time.unscaledTime - cameraModeSince > 0.15f)
                {
                    tapDown = true; tapMoved = false; tapMulti = false; tapStart = pos; tapTime = Time.unscaledTime;
                }
                return;
            }
            if (down)
            {
                if (count > 1) tapMulti = true;
                if ((pos - tapStart).sqrMagnitude > slop * slop) tapMoved = true;
                return;
            }
            tapDown = false;
            if (!tapMoved && !tapMulti && Time.unscaledTime - tapTime <= TapMaxSeconds) SetCameraMode(false);
        }

        static void ReadPointer(out bool down, out int count, out Vector2 pos)
        {
            count = 0; pos = default;
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.press.isPressed) { count++; if (count == 1) pos = t.position.ReadValue(); }
            if (count == 0 && Mouse.current != null && Mouse.current.leftButton.isPressed) { count = 1; pos = Mouse.current.position.ReadValue(); }
            down = count > 0;
        }

        /// <summary>Camera mode: the small pill says who the camera follows (null = nobody), so a follow shows even at full view.</summary>
        public void SetCameraNote(string note)
        {
            exitButton?.Say(string.IsNullOrEmpty(note) ? "Tap to show the UI" : $"{note} · tap to show the UI");
        }

        /// <summary>Clean and Shape don't apply (a museum visit, the time-lapse): hidden while any <paramref name="reason"/> holds
        /// them, clean mode ends and the toast showing goes (tester batch 2).</summary>
        public void HideTools(string reason, bool hide)
        {
            if (!(hide ? toolBlocks.Add(reason) : toolBlocks.Remove(reason))) return;
            bool h = toolBlocks.Count > 0;
            if (h) { SetCleanMode(false); DropToasts(); }
            if (cleanButton != null) cleanButton.style.display = h ? DisplayStyle.None : DisplayStyle.Flex;
            if (shapeButton != null) shapeButton.style.display = h ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public bool ToolsHidden => toolBlocks.Count > 0;

        /// <summary>History always opens the time-lapse (D1). This does it: false means there is nothing to replay yet, so the
        /// History sheet opens instead.</summary>
        public Func<bool> HistoryTapped;

        /// <summary>The Stories chip: opens what's waiting.</summary>
        public Action StoriesTapped;

        /// <summary>The Stories chip: unread stories wait after a peek was swiped away. A dot and a word, never a number.</summary>
        public void SetHistoryDot(bool on)
        {
            if (storiesChip != null) storiesChip.style.display = on || ForceStories ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Debug: holds the Stories chip up, whatever the stack says.</summary>
        public bool ForceStories { get; set; }

        void OnHistory()
        {
            if (HistoryTapped == null || !HistoryTapped()) Open(History);
        }

        Action pinTap;

        /// <summary>#39, D2: the pinned person's chip, a portrait and the name, apart from the tools: <paramref name="text"/> null
        /// hides it; a tap calls <paramref name="onTap"/> (the first flies to them, a second opens their card).</summary>
        public void SetPinChip(string text, Action onTap)
        {
            pinTap = onTap;
            if (pinChip != null) pinChip.Person = text;
        }

        /// <summary>The pin chip's text, or null while it is hidden (tests).</summary>
        public string PinChipText => pinChip != null && pinChip.style.display != DisplayStyle.None ? pinChip.Person : null;

        /// <summary>A short line in the toast lane (hints, "all clean"). One at a time: it waits for the one showing, and for any prompt.</summary>
        public void Toast(string text, float seconds = 3f)
        {
            if (toastLabel == null || cameraMode) return;
            GameUiPanel.Toasts.Post(text, seconds);
            PumpToast();
        }

        /// <summary>Takes back a toast still waiting its turn, a hint that no longer applies.</summary>
        public void WithdrawToast(string text) => GameUiPanel.Toasts.Withdraw(text);

        void PumpToast()
        {
            var q = GameUiPanel.Toasts;
            if (!q.Tick(Time.unscaledTime)) return;
            if (q.Current == null) { GlassMotion.Hide(toastLabel, GlassMotion.FadeOff, GlassMotion.FadeMs); return; }
            toastLabel.text = q.Current;
            GlassMotion.Show(toastLabel, GlassMotion.FadeOff);
        }

        void DropToasts()
        {
            GameUiPanel.Toasts.Clear();
            if (toastLabel != null) toastLabel.style.display = DisplayStyle.None;
        }

        /// <summary>The toast's lane: its own, or 8 pt above a sheet that's up.</summary>
        void PlaceToast()
        {
            if (toastLabel == null) return;
            var i = GameUiPanel.SafeInsets(lastSafe, lastScreen.x, lastScreen.y);
            float lane = GameUiPanel.Lane(GameUiPanel.ToastBottom, i.w);
            VisualElement up = openSheet != null ? openSheet.Sheet : outsideSheet;
            if (up != null)
            {
                float h = up.layout.height;
                if (!float.IsNaN(h) && h > 0f) lane = h - i.w + GameUiPanel.ToastAboveSheet;
            }
            toastLabel.style.bottom = lane;
        }

        /// <summary>A new tool waits in Shape: one soft glow, then the New tag stays until the tray is opened.</summary>
        public void SetShapeNew(bool on)
        {
            if (shapeNew == on || shapeButton == null) return;
            shapeNew = on;
            shapeButton.EnableInClassList("kg-tool--new", on);
            if (!on) return;
            UiSound.Play(UiRole.NewTool);
            GlassMotion.Glow(shapeButton, "kg-tool--glow");
        }

        public bool ShapeNew => shapeNew;

        /// <summary>The camera left its home view: ink on the world drops to 1.0:1 over the glass frame, so the name and top icons
        /// get a quiet backing (spec §4, question 2). On the home view there's none.</summary>
        public void SetOffHome(bool off)
        {
            if (offHome == off) return;
            offHome = off;
            topRow?.EnableInClassList("hud-top--backed", off);
        }

        public bool OffHome => offHome;

        bool IsOverUi(Vector2 screenPos) => !hidden && GameUiPanel.Over(root, screenPos);

        void Update()
        {
            if (root == null) return;
            if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y) ApplySafeArea();
            UpdateBlockers();
            if (cameraMode) UpdateCameraMode();
            if (toastLabel != null) PumpToast();
            if (outsideSheet != null) PlaceToast(); // it has no height until its first layout
            FollowFirstRun();
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

            topRow = Row("hud-top");
            var title = new GlassButton("", () => Open(Settings), ButtonKind.Quiet, UiRole.Open) { name = "hud-title" };
            title.AddToClassList("hud-title");
            var words = new VisualElement { name = "hud-title-text", pickingMode = PickingMode.Ignore };
            words.AddToClassList("hud-title-text");
            words.Add(nameLabel = Text("Your box", "hud-title-name", "type-box"));
            words.Add(yearLabel = Text("Year 0", "hud-title-year", "type-sub"));
            nameLabel.pickingMode = yearLabel.pickingMode = PickingMode.Ignore;
            title.Add(words);
            title.Add(Disc("settings"));
            topRow.Add(title);
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("hud-spacer");
            topRow.Add(spacer);
            var corner = new VisualElement { name = "hud-corner", pickingMode = PickingMode.Ignore };
            corner.AddToClassList("hud-corner");
            if (FeedbackUI.InTestBuild) corner.Add(IconButton("hud-feedback", "feedback", OpenFeedback, UiRole.Tap, "hud-icon--quiet"));
            corner.Add(IconButton("hud-photo", "photo", () => SetCameraMode(true), UiRole.Toggle));
            topRow.Add(corner);
            safe.Add(topRow);

            pinRow = Row("hud-pins");
            pinChip = new PinChip(() => pinTap?.Invoke()) { name = "hud-pin" };
            pinChip.style.display = DisplayStyle.None; // nobody pinned yet
            pinRow.Add(pinChip);
            storiesChip = new StoriesChip(() => StoriesTapped?.Invoke()) { name = "hud-history-dot" };
            storiesChip.style.display = DisplayStyle.None;
            pinRow.Add(storiesChip);
            safe.Add(pinRow);

            bottomRow = Row("hud-bottom");
            bottomRow.Add(Tool("hud-history", "History", OnHistory, UiRole.Open));
            bottomRow.Add(cleanButton = Tool("hud-clean", "Clean", () => SetCleanMode(!cleanMode), UiRole.Toggle));
            cleanButton.Add(new GlassIcon("check", "kg-icon--small", "kg-tool-mark"));
            bottomRow.Add(shapeButton = Tool("hud-shape", "Shape", () => { SetShapeNew(false); Open(Shaping); }, UiRole.Open));
            shapeButton.Add(new GlassNewTag());
            safe.Add(bottomRow);

            GameUiPanel.Toasts.Clear();
            toastLabel = new GlassToast { name = "hud-toast" };
            toastLabel.style.display = DisplayStyle.None;
            safe.Add(toastLabel);

            for (int i = 0; i < blockers.Length; i++)
            {
                blockers[i] = new VisualElement { name = "hud-debug-block" + i, pickingMode = PickingMode.Position };
                blockers[i].style.position = Position.Absolute;
                blockers[i].style.display = DisplayStyle.None;
                root.Add(blockers[i]);
            }
            // The way back from camera mode: a small pill at the bottom, inside the safe area, shown only in that mode.
            exitButton = new GlassButton("Tap to show the UI", () => SetCameraMode(false), ButtonKind.Quiet, UiRole.Toggle) { name = "hud-photo-exit" };
            exitButton.AddToClassList("hud-photo-exit");
            exitButton.AddToClassList("kg-way-back");
            exitButton.Mark(new GlassIcon("close", "kg-icon--small", "kg-way-back-mark"));
            exitButton.style.display = DisplayStyle.None;
            safe.Add(exitButton);
            BuildSettings();
            BuildHistory();
            BuildShaping();
            ApplySafeArea();
            RefreshText();
            FollowFirstRun();
        }

        void BuildSettings()
        {
            var sheet = Sheet(Settings, "Settings", SheetHeight.Tall);
            soundSwitch = SwitchRow(sheet, "settings-sound", "Sound", () => GameSettings.Sound, v => GameSettings.Sound = v);
            hapticsSwitch = SwitchRow(sheet, "settings-haptics", "Haptics", () => GameSettings.Haptics, v => GameSettings.Haptics = v);
            if (FeedbackUI.InTestBuild)
            {
                var send = new GlassButton("Send feedback", OpenFeedback) { name = "settings-feedback" };
                send.AddToClassList("kg-sheet-action");
                sheet.Add(send);
            }
            sheet.Add(Text($"Aquarium Civ {Application.version}", "type-note", "kg-sheet-note"));
        }

        void BuildHistory()
        {
            var sheet = Sheet(History, "History");
            sheet.Add(historyBody = Text("", "type-body"));
            sheet.Add(Text("The chronicle and the time-lapse of the years will live here.", "type-note", "kg-sheet-note"));
        }

        /// <summary>The shaping sheet's body (#84): empty here; the shaping view fills it with the tray when the sheet opens
        /// (<see cref="SheetChanged"/>), so the tray's logic and look live with the shaping, not in the HUD shell.</summary>
        public VisualElement ShapingBody { get; private set; }

        void BuildShaping()
        {
            // Content height until #167 gives the tray its low tile row at 30 %.
            var sheet = Sheet(Shaping, "Shape the land", SheetHeight.Content);
            ShapingBody = new VisualElement { name = "shaping-tray" };
            ShapingBody.AddToClassList("tray");
            sheet.Add(ShapingBody);
        }

        /// <summary>A bottom sheet in its clear scrim (a tap outside closes it), with a title and Close.</summary>
        GlassSheet Sheet(string id, string title, SheetHeight height = SheetHeight.Standard)
        {
            var sheet = new GlassSheet(height);
            var scrim = new GlassScrim(id, sheet);
            scrim.TappedOutside += () => { UiSound.Play(UiRole.Close); CloseSheet(); };
            root.Add(scrim);
            sheet.Add(GlassSheet.Head(title, id + "-close", CloseSheet));
            sheet.RegisterCallback<GeometryChangedEvent>(_ => PlaceToast());
            sheets[id] = scrim;
            return sheet;
        }

        GlassToggle SwitchRow(VisualElement sheet, string name, string label, Func<bool> get, Action<bool> set)
        {
            var row = new VisualElement();
            row.AddToClassList("kg-row");
            row.Add(Text(label, "type-body", "kg-row-label"));
            var sw = new GlassToggle(get(), set) { name = name };
            row.Add(sw);
            sheet.Add(row);
            return sw;
        }

        // ---------------- behaviour ----------------
        /// <summary>Open a sheet by name (<see cref="Settings"/>, <see cref="History"/>, <see cref="Shaping"/>); closes any other.</summary>
        public void Open(string id)
        {
            if (!sheets.TryGetValue(id, out var scrim) || openSheet == scrim) return;
            if (id == Shaping) SetCleanMode(false); // one mode at a time, and Done would sit hidden behind the tray
            if (openSheet != null) { openSheet.style.display = DisplayStyle.None; openSheet.Sheet.Close(); }
            openSheet = scrim;
            if (id == Settings) { soundSwitch.On = GameSettings.Sound; hapticsSwitch.On = GameSettings.Haptics; }
            if (id == History) RefreshText();
            scrim.Open();
            ToolsBehindSheet();
            PlaceToast();
            SheetChanged?.Invoke(id);
        }

        public void CloseSheet()
        {
            if (openSheet == null) return;
            var closing = openSheet;
            openSheet = null;
            closing.Close();
            ToolsBehindSheet();
            PlaceToast();
            SheetChanged?.Invoke(null);
        }

        /// <summary>The tools step back behind a sheet: a flat sheet is 96 %, so they would show through it as ghosts.</summary>
        void ToolsBehindSheet()
        {
            var v = openSheet != null || outsideSheet != null ? Visibility.Hidden : Visibility.Visible;
            bottomRow.style.visibility = v;
            pinRow.style.visibility = v;
        }

        /// <summary>A sheet the HUD doesn't own is up (the person card; null when it goes): the tools and the top row step back, so the
        /// person has the world to themselves (board 3.4), and a toast rides above it as it does above the HUD's own.</summary>
        public void SetOutsideSheet(VisualElement sheet)
        {
            outsideSheet = sheet;
            ToolsBehindSheet();
            PlaceToast();
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
            foreach (var scrim in sheets.Values) scrim.Sheet.SetInsets(i);
            // The lanes count from the screen's edge; a home bar taller than 30 pt lifts them.
            bottomRow.style.bottom = GameUiPanel.Lane(GameUiPanel.ToolsBottom, i.w);
            pinRow.style.bottom = GameUiPanel.Lane(GameUiPanel.PinBottom, i.w);
            exitButton.style.bottom = GameUiPanel.Lane(GameUiPanel.ToolsBottom, i.w);
            topRow.style.top = Mathf.Max(0f, TopRowTop - i.y); // 40 pt down on the boards, or just under the notch
            PlaceToast();
        }

        /// <summary>Question 26: the top row arrives when the first run is done. Every frame, not with the half-second text: while it's
        /// away Settings, Photo and Feedback can't be tapped, so they come back the moment the first run ends.</summary>
        void FollowFirstRun()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            topRow.EnableInClassList("hud-top--away", outsideSheet != null || (sim != null && sim.State.firstRun.active));
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

        static Label Text(string s, params string[] classes)
        {
            var l = new Label(s);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        static GlassButton Tool(string name, string label, Action onClick, UiRole role)
        {
            var b = new GlassButton(label, onClick, ButtonKind.Secondary, role) { name = name };
            b.AddToClassList("kg-tool");
            return b;
        }

        /// <summary>The 22 pt icon in its 44 pt disc, which is what the quiet backing fills.</summary>
        static VisualElement Disc(string icon)
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.AddToClassList("hud-disc");
            d.Add(new GlassIcon(icon));
            return d;
        }

        static GlassButton IconButton(string name, string icon, Action onClick, UiRole role, params string[] classes)
        {
            var b = new GlassButton("", onClick, ButtonKind.Icon, role) { name = name };
            b.AddToClassList("hud-icon");
            foreach (var c in classes) b.AddToClassList(c);
            b.Add(Disc(icon));
            return b;
        }
    }
}
