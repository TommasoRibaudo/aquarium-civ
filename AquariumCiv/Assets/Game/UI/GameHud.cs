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
    /// the box's name and year as quiet text with a settings mark beside it (the name opens the Offices list, the mark opens Settings), Photo and a quieter Feedback as small
    /// icons, Feedback in test builds only. Bottom: three tools of one calm look, History (always the time-lapse), Clean, Shape.
    /// Above them the pin chip (a portrait and the name) and the Stories chip (unread stories). One sheet at a time, closed by
    /// its Close, a tap on the world, or Back. Everything sits inside the safe area, in the lanes of <see cref="GameUiPanel"/>.
    /// Built in code from the components in UI/Glass; every control has a stable name so tests and the tester agent find it
    /// (<c>hud-title</c>, <c>hud-history</c>, <c>hud-clean</c>, <c>hud-shape</c>, <c>hud-pin</c>, <c>hud-history-dot</c> (the Stories chip),
    /// <c>hud-feedback</c>, <c>hud-photo</c>, <c>settings</c>/<c>history</c>/<c>shaping</c> + <c>-sheet</c>, <c>-scrim</c>, <c>-close</c>,
    /// and what <see cref="SettingsSheets"/> builds).
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public const string Settings = "settings", History = "history", Shaping = "shaping", Offices = "offices", Kit = "kit";

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
        bool outsideKeepsTop, peekUp;
        GlassScrim openSheet;
        readonly System.Collections.Generic.Dictionary<string, GlassScrim> sheets = new();
        Label nameLabel, yearLabel;
        PinChip pinChip;
        StoriesChip storiesChip;
        SettingsSheets settings;
        GlassButton cleanButton, shapeButton, historyButton;
        readonly System.Collections.Generic.HashSet<string> toolBlocks = new();
        GlassToast toastLabel;
        bool cleanMode, shapeNew, offHome, toolsWaiting;
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
        const string WaitingClass = "kg-tool--waiting";
        const string ArriveClass = "kg-tool--arrive";
        const int ArriveStepMs = 220;

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
        }

        void OnDisable()
        {
            if (Instance != this) return;
            UiInput.Remove(IsOverUi);
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
            if (cleanMode == on || (CleanLocked && !on)) return;
            cleanMode = on;
            if (cleanButton != null)
            {
                cleanButton.text = on ? "Done" : KitBox ? "Kit" : "Clean";
                cleanButton.EnableInClassList("kg-tool--on", on); // the word, a check and a ring: the mode is never a silent one
            }
            if (on && hand != KitTool.None) SetHand(KitTool.None);
            CleanModeChanged?.Invoke(on);
        }

        // ---------------- P8: the Keeper's kit (keepers-hands.md section 3) ----------------
        /// <summary>The kit's tools besides the cloth (the cloth is clean mode). One in hand at a time; one finger is the tool, two the camera.</summary>
        public enum KitTool { None, Mister, Seed, Tweezers }
        KitTool hand;
        bool kitBox, kitSeedShown;
        GlassButton kitCloth, kitMister, kitSeed, kitTweezers;
        Label kitNote;
        /// <summary>The tool in hand (the mister or the seed), or None. The cloth is <see cref="CleanMode"/>.</summary>
        public KitTool Hand => hand;
        /// <summary>A living-land box: Clean and Shape become one Kit button with the cloth, the mister and (once it comes) the seed.</summary>
        public bool KitBox => kitBox;
        public event Action<KitTool> HandChanged;

        /// <summary>Put a kit tool in hand (None puts it down). Clean mode ends; the Kit button reads Done and stays lit while it's held.</summary>
        public void SetHand(KitTool t)
        {
            if (hand == t) return;
            if (t != KitTool.None) { SetCleanMode(false); CloseSheet(); }
            hand = t;
            if (cleanButton != null)
            {
                bool on = t != KitTool.None || cleanMode;
                cleanButton.text = on ? "Done" : KitBox ? "Kit" : "Clean";
                cleanButton.EnableInClassList("kg-tool--on", on);
            }
            if (t == KitTool.Mister && MisterHintDue) { Toast(MisterHint, 4f); NoteMisterHintShown(); }
            if (t == KitTool.Seed) Toast("Tap a spot on the land to plant the seed.", 4f);
            if (t == KitTool.Tweezers) Toast(TweezersHint, 4f);
            HandChanged?.Invoke(t);
        }

        // Tester run 2: the Mister's hint is for the first meeting. It shows the first time the mister is picked up, once more if the
        // player put it down without misting, then never again (PlayerPrefs: it is about the player, not the box).
        public const string MisterHint = "Hold a finger over the land to mist it. Two fingers move the view.";
        /// <summary>#311: the tweezers pluck what overgrows; the cloth keeps the glass.</summary>
        public const string TweezersHint = "Tap or drag over weeds, vines and saplings to pluck them. Two fingers move the view.";
        const string MisterShownKey = "p8.misterHint.shown", MisterUsedKey = "p8.misterHint.used";
        public static bool MisterHintDue
        {
            get
            {
                int shown = PlayerPrefs.GetInt(MisterShownKey, 0);
                return shown == 0 || (shown == 1 && PlayerPrefs.GetInt(MisterUsedKey, 0) == 0);
            }
        }
        static void NoteMisterHintShown() { PlayerPrefs.SetInt(MisterShownKey, PlayerPrefs.GetInt(MisterShownKey, 0) + 1); PlayerPrefs.Save(); }
        /// <summary>The mister in hand misted the land: the second showing of its hint is not needed.</summary>
        public static void NoteMisterUsed()
        {
            if (PlayerPrefs.GetInt(MisterUsedKey, 0) == 1) return;
            PlayerPrefs.SetInt(MisterUsedKey, 1); PlayerPrefs.Save();
        }
        /// <summary>Debug panel and tests: forget the Mister's hint, so the next pick-up shows it.</summary>
        public static string DebugForgetMisterHint()
        {
            PlayerPrefs.DeleteKey(MisterShownKey); PlayerPrefs.DeleteKey(MisterUsedKey); PlayerPrefs.Save();
            return "Mister hint forgotten: pick up the Mister";
        }

        void OnKitButton()
        {
            if (cleanMode || hand != KitTool.None) { SetCleanMode(false); SetHand(KitTool.None); return; }
            SetShapeNew(false);
            Open(Kit);
        }

        void BuildKit()
        {
            var sheet = Sheet(Kit, "The Keeper's kit");
            kitNote = Text("", "type-note", "kg-sheet-note");
            var acts = new VisualElement();
            acts.AddToClassList("kg-card-acts");
            acts.Add(kitCloth = new GlassButton("Cloth", () => { CloseSheet(); SetCleanMode(true); }, ButtonKind.Secondary, UiRole.Toggle) { name = "kit-cloth" });
            acts.Add(kitMister = new GlassButton("Mister", () => SetHand(KitTool.Mister), ButtonKind.Secondary, UiRole.Toggle) { name = "kit-mister" });
            acts.Add(kitTweezers = new GlassButton("Tweezers", () => SetHand(KitTool.Tweezers), ButtonKind.Secondary, UiRole.Toggle) { name = "kit-tweezers" });
            acts.Add(kitSeed = new GlassButton("Seed", () => SetHand(KitTool.Seed), ButtonKind.Secondary, UiRole.Toggle) { name = "kit-seed" });
            kitNote.style.whiteSpace = WhiteSpace.Normal; // the phone's width: the note wraps, never runs off the sheet
            kitNote.style.marginBottom = 12;
            sheet.Add(kitNote);
            sheet.Add(acts);
            kitSeed.style.display = DisplayStyle.None;
        }

        /// <summary>Each frame: a living-land box shows Kit (not Clean and Shape); the seed joins the kit when the land is ready for it
        /// and leaves once it's planted.</summary>
        void FollowKit()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            bool eco = sim != null && sim.EcologyOn;
            if (eco != kitBox)
            {
                kitBox = eco;
                if (!eco) SetHand(KitTool.None);
                if (cleanButton != null) cleanButton.text = cleanMode || hand != KitTool.None ? "Done" : eco ? "Kit" : "Clean";
                if (shapeButton != null) shapeButton.style.display = eco || toolBlocks.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (!eco || kitSeed == null) return;
            bool seed = sim.CanPlantTree;
            if (seed != kitSeedShown)
            {
                kitSeedShown = seed;
                kitSeed.style.display = seed ? DisplayStyle.Flex : DisplayStyle.None;
                if (seed && !sim.State.firstRun.active) { SetShapeNew(true); Toast("A seed has come. It's in your kit.", 4f); }
                if (!seed && hand == KitTool.Seed) SetHand(KitTool.None);
            }
            if (kitNote != null)
                kitNote.text = seed ? "Cloth wipes the glass. Mister wets the land. Tweezers pluck what overgrows. Seed: plant the one tree."
                    : sim.EcoTree.cell >= 0 ? "Cloth wipes the glass. Mister wets the land and the tree. Tweezers pluck what overgrows."
                    : "Cloth wipes the glass. Mister wets the land. Tweezers pluck what overgrows. A seed comes when the land can hold a tree.";
        }

        /// <summary>The first run's wipe and flick: the hint says to drag, so nothing turns clean mode off under it, not Done (the tool isn't
        /// here yet) and not the idle timer. Whoever sets this owns it, and clears it before the step ends.</summary>
        public bool CleanLocked { get; set; }

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
            if (h) { SetCleanMode(false); SetHand(KitTool.None); DropToasts(); }
            if (cleanButton != null) cleanButton.style.display = h ? DisplayStyle.None : DisplayStyle.Flex;
            if (shapeButton != null) shapeButton.style.display = h || kitBox ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public bool ToolsHidden => toolBlocks.Count > 0;

        readonly System.Collections.Generic.HashSet<string> laneBlocks = new();

        /// <summary>Batch 7: a bar of someone else's sits in the tools' lane (the box-staged choice's bar), so History steps out of
        /// sight and taps under it too (HideTools leaves History, which showed through the bar).</summary>
        public void TakeToolsLane(string reason, bool on)
        {
            if (!(on ? laneBlocks.Add(reason) : laneBlocks.Remove(reason))) return;
            ToolsBehindSheet();
        }

        public bool ToolsLaneTaken => laneBlocks.Count > 0;

        /// <summary>History always opens the time-lapse (D1). This does it: false means there is nothing to replay yet, so the
        /// History sheet opens instead.</summary>
        public Func<bool> HistoryTapped;

        /// <summary>The Stories chip: opens what's waiting.</summary>
        public Action StoriesTapped;

        /// <summary>The Stories chip is up, so it has the slot the Shelf chip shares (question 5).</summary>
        public bool StoriesUp => storiesChip != null && storiesChip.style.display == DisplayStyle.Flex;

        /// <summary>The peek has the pin's lane now.</summary>
        public bool PeekUp => peekUp;

        /// <summary>A Shelf row in Settings, while Stories has the chip's slot. A tap closes the sheet and calls <see cref="ShelfTapped"/>.</summary>
        public void SetShelfRow(bool on)
        {
            var d = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (settings != null && settings.ShelfRow.style.display != d) settings.ShelfRow.style.display = d;
        }

        public Action ShelfTapped;

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
        public void Toast(string text, float seconds = 3f, bool through = false)
        {
            if (toastLabel == null || cameraMode) return;
            if (through) GameUiPanel.Toasts.PostThrough(text, seconds); else GameUiPanel.Toasts.Post(text, seconds);
            PumpToast();
        }

        /// <summary>Takes back a toast, waiting or on screen: a hint that no longer applies.</summary>
        public void WithdrawToast(string text) { GameUiPanel.Toasts.Withdraw(text); PumpToast(); }

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
            float lane = GameUiPanel.Lane(toolInHand || bannerUp ? GameUiPanel.ToastOverStrip : toolsWaiting ? GameUiPanel.ToolsBottom : GameUiPanel.ToastBottom, i.w);
            VisualElement up = openSheet != null ? openSheet.Sheet : outsideSheet;
            if (up != null)
            {
                float h = up.layout.height;
                if (!float.IsNaN(h) && h > 0f) lane = h - i.w + GameUiPanel.ToastAboveSheet;
            }
            toastLabel.style.bottom = Mathf.Max(lane, toastClear > 0f ? GameUiPanel.Lane(toastClear, i.w) : 0f);
        }

        float toastClear;

        /// <summary>Tester run 8: a prompt in the toast's own lane (short screens) hid the words under it: toasts go over <paramref name="bottom"/> while it's up, 0 lets them back.</summary>
        public void SetToastClear(float bottom)
        {
            if (Mathf.Approximately(toastClear, bottom)) return;
            toastClear = bottom;
            PlaceToast();
        }

        /// <summary>A new tool waits in Shape: one soft glow, then the New tag stays until the tray is opened.</summary>
        public void SetShapeNew(bool on)
        {
            if (shapeNew == on || shapeButton == null) return;
            shapeNew = on;
            var b = kitBox ? cleanButton : shapeButton;
            shapeButton.EnableInClassList("kg-tool--new", on && !kitBox);
            cleanButton?.EnableInClassList("kg-tool--new", on && kitBox);
            if (!on) return;
            UiSound.Play(UiRole.NewTool);
            GlassMotion.Glow(b, "kg-tool--glow");
        }

        public bool ShapeNew => shapeNew;

        /// <summary>#160: the Clean button draws the eye, the way the new tool's glow does, and asks for nothing.</summary>
        public void PulseClean()
        {
            if (cleanButton == null) return;
            CleanPulses++;
            GlassMotion.Glow(cleanButton, "kg-tool--glow");
        }

        /// <summary>How many times <see cref="PulseClean"/> ran (tests).</summary>
        public int CleanPulses { get; private set; }

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
            FollowKit();
            if (Time.unscaledTime >= nextText) { nextText = Time.unscaledTime + 0.5f; RefreshText(); }
            settings.Tick(SafeNow, BottomPadNow, OpenSheet == Settings);
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
            safe.AddToClassList(GlassMotion.AwayClass);
            root.Add(safe);

            topRow = Row("hud-top");
            // The name chip opens the offices list (#215); the settings mark to its right opens Settings.
            var title = new GlassButton("", () => Open(Offices), ButtonKind.Quiet, UiRole.Open) { name = "hud-title" };
            title.AddToClassList("hud-title");
            var words = new VisualElement { name = "hud-title-text", pickingMode = PickingMode.Ignore };
            words.AddToClassList("hud-title-text");
            words.Add(nameLabel = Text("Your box", "hud-title-name", "type-box"));
            words.Add(yearLabel = Text("Year 0", "hud-title-year", "type-sub"));
            nameLabel.pickingMode = yearLabel.pickingMode = PickingMode.Ignore;
            title.Add(words);
            topRow.Add(title);
            topRow.Add(IconButton("hud-settings", "settings", () => Open(Settings), UiRole.Open));
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
            bottomRow.Add(historyButton = Tool("hud-history", "History", OnHistory, UiRole.Open));
            bottomRow.Add(cleanButton = Tool("hud-clean", "Clean", () => { if (kitBox) OnKitButton(); else SetCleanMode(!cleanMode); }, UiRole.Toggle));
            cleanButton.Add(new GlassIcon("check", "kg-icon--small", "kg-tool-mark"));
            cleanButton.Add(new GlassNewTag());
            bottomRow.Add(shapeButton = Tool("hud-shape", "Shape", () => { SetShapeNew(false); Open(Shaping); }, UiRole.Open));
            shapeButton.Add(new GlassNewTag());
            safe.Add(bottomRow);

            GameUiPanel.Toasts.Clear();
            GameUiPanel.Bubbles.Clear();
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
            settings = new SettingsSheets(this, tightSheets.Add);
            BuildHistory();
            BuildOffices();
            BuildShaping();
            BuildKit();
            ApplySafeArea();
            RefreshText();
            FollowFirstRun();
        }

        void BuildHistory()
        {
            var sheet = Sheet(History, "History");
            sheet.Add(historyBody = Text("", "type-body"));
            sheet.Add(Text("The chronicle and the time-lapse of the years will live here.", "type-note", "kg-sheet-note"));
        }

        /// <summary>#215: who holds each office of this era. Rebuilt every time the sheet opens, so it is never stale. #272: in a
        /// scroll, so a long town's list scrolls and the view ends on a whole row.</summary>
        void BuildOffices()
        {
            var sheet = Sheet(Offices, "Offices");
            OfficesScroll = new GlassScroll { name = "offices-scroll", WholeRows = true };
            officesList = new VisualElement { name = "offices-list", pickingMode = PickingMode.Ignore };
            OfficesScroll.Add(officesList);
            sheet.Add(OfficesScroll);
        }

        VisualElement officesList;
        /// <summary>The Offices sheet's scroll (tests).</summary>
        public GlassScroll OfficesScroll { get; private set; }

        void RefreshOffices()
        {
            officesList.Clear();
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            int rows = 0;
            if (sim != null)
                foreach (var role in AquariumCiv.Sim.SimCore.RolesOf(sim.State.era))
                {
                    if (!sim.RoleOpen(role)) continue;
                    var holder = sim.HolderOf(role);
                    var row = new VisualElement { name = "office-" + role.Replace(' ', '-'), pickingMode = PickingMode.Ignore };
                    row.AddToClassList("kg-row");
                    row.AddToClassList(GlassScroll.RowClass);
                    var label = Text(char.ToUpperInvariant(role[0]) + role.Substring(1), "type-body", "kg-row-label");
                    var who = Text(holder != null ? holder.name : "Vacant", "type-body", "office-holder");
                    if (holder == null) who.AddToClassList("office-vacant");
                    row.Add(label);
                    row.Add(who);
                    officesList.Add(row);
                    rows++;
                }
            if (rows == 0) officesList.Add(Text("No offices yet.", "type-note", "kg-sheet-note"));
        }

        /// <summary>The shaping sheet's body (#84): empty here; the shaping view fills it with the tray when the sheet opens
        /// (<see cref="SheetChanged"/>), so the tray's logic and look live with the shaping, not in the HUD shell.</summary>
        public VisualElement ShapingBody { get; private set; }
        /// <summary>The slot in the tray's head, between the title and Close, where the charges sit (spec §4).</summary>
        public VisualElement ShapingHead { get; private set; }

        GlassSheet shapingSheet;
        readonly System.Collections.Generic.List<GlassSheet> tightSheets = new();
        Vector4 SafeNow => GameUiPanel.SafeInsets(lastSafe, lastScreen.x, lastScreen.y);
        float BottomPadNow => GlassSheet.IsShort(lastScreen.x, lastScreen.y) ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad;

        /// <summary>Settings and what's behind it.</summary>
        public SettingsSheets SettingsView => settings;

        void BuildShaping()
        {
            shapingSheet = Sheet(Shaping, "Shape the land");
            shapingSheet.AddToClassList("tray-sheet");
            ShapingHead = new VisualElement { name = "shaping-head", pickingMode = PickingMode.Ignore };
            shapingSheet.Q(className: "kg-sheet-head").Insert(1, ShapingHead);
            ShapingBody = new VisualElement { name = "shaping-tray", pickingMode = PickingMode.Ignore };
            ShapingBody.AddToClassList("tray");
            shapingSheet.Add(ShapingBody);
        }

        /// <summary>A bottom sheet in its clear scrim (a tap outside closes it), with a title and Close.</summary>
        internal GlassSheet Sheet(string id, string title, SheetHeight height = SheetHeight.Standard, bool close = true)
        {
            var sheet = new GlassSheet(height);
            var scrim = new GlassScrim(id, sheet);
            scrim.TappedOutside += () => { UiSound.Play(UiRole.Close); CloseSheet(); };
            sheet.Add(GlassSheet.Head(title, close ? id + "-close" : null, close ? CloseSheet : null));
            AddSheet(id, scrim);
            return sheet;
        }

        /// <summary>A sheet built elsewhere, opened and closed like the HUD's own.</summary>
        internal void AddSheet(string id, GlassScrim scrim)
        {
            root.Add(scrim);
            scrim.Sheet.RegisterCallback<GeometryChangedEvent>(_ => PlaceToast());
            sheets[id] = scrim;
        }

        internal GlassScrim ScrimOf(string id) => sheets[id];

        // ---------------- behaviour ----------------
        /// <summary>Open a sheet by name (<see cref="Settings"/>, <see cref="History"/>, <see cref="Shaping"/>, <see cref="Offices"/>); closes any other.</summary>
        public void Open(string id)
        {
            if (!sheets.TryGetValue(id, out var scrim) || openSheet == scrim) return;
            if (id == Shaping && kitBox) id = Kit; // a living-land box has no Shape tray: the kit is the Keeper's hands
            if (id == Shaping || id == Kit) SetCleanMode(false); // one mode at a time, and Done would sit hidden behind the tray
            if (id == Kit) SetHand(KitTool.None);
            if (!sheets.TryGetValue(id, out scrim) || openSheet == scrim) return;
            if (openSheet != null) { openSheet.style.display = DisplayStyle.None; openSheet.Sheet.Close(); }
            openSheet = scrim;
            settings.Opening(id);
            if (id == History) RefreshText();
            if (id == Offices) RefreshOffices();
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
            settings.Closing(SafeNow, BottomPadNow);
            closing.Close();
            ToolsBehindSheet();
            PlaceToast();
            SheetChanged?.Invoke(null);
        }

        /// <summary>The tools step back behind a sheet: a flat sheet is 96 %, so they would show through it as ghosts.</summary>
        void ToolsBehindSheet()
        {
            var v = openSheet != null || outsideSheet != null || toolInHand || bannerUp ? Visibility.Hidden : Visibility.Visible;
            bottomRow.style.visibility = laneBlocks.Count > 0 ? Visibility.Hidden : v;
            pinRow.style.visibility = peekUp ? Visibility.Hidden : v;
        }

        /// <summary>The peek takes the pin's lane while it's up (spec §1), so the pin and Stories chips step aside.</summary>
        public void SetPeekUp(bool up)
        {
            if (peekUp == up) return;
            peekUp = up;
            ToolsBehindSheet();
        }

        /// <summary>A sheet the HUD doesn't own is up (the person card; null when it goes): the tools and the top row step back, so the
        /// person has the world to themselves (board 3.4), and a toast rides above it as it does above the HUD's own.
        /// <paramref name="keepTop"/>: the top row stays, as it does behind the recall question (board 3.3).</summary>
        public void SetOutsideSheet(VisualElement sheet, bool keepTop = false)
        {
            outsideSheet = sheet;
            outsideKeepsTop = keepTop;
            ToolsBehindSheet();
            PlaceToast();
        }

        bool toolInHand;

        /// <summary>A tool is in hand: its strip takes the tools' lane, the rest of the HUD leaves the ground to the finger (board 3.4), and a
        /// refusal's toast drops into the free pin lane, right over the strip (<see cref="GameUiPanel.ToastOverStrip"/>).</summary>
        /// <summary>The strip over the tools is taken, by a tool in hand or a banner: the toast drops into the chips' lane, so the Shelf chip steps aside with them (tester run 10).</summary>
        public bool ChipLaneTaken => toolInHand || bannerUp;

        public void SetToolInHand(bool on)
        {
            if (toolInHand == on) return;
            toolInHand = on;
            ToolsBehindSheet();
            PlaceToast();
        }

        bool bannerUp;

        /// <summary>The visiting banner has the tools' lane (spec §1, §4): the tools and the top row leave, and the toast rides over it as it
        /// does over a tool's strip. Out of the layout too, so nothing can be tapped behind the banner.</summary>
        public void SetBannerUp(bool on)
        {
            if (bannerUp == on) return;
            bannerUp = on;
            bottomRow.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
            ToolsBehindSheet();
            PlaceToast();
        }

        /// <summary>The sheet set by <see cref="SetOutsideSheet"/>, so two owners can't take it from each other.</summary>
        public VisualElement OutsideSheet => outsideSheet;
        public bool OutsideKeepsTop => outsideKeepsTop;

        /// <summary>Hide the whole HUD (feedback screenshots, a clean snapshot, the debug toggle).</summary>
        public void SetVisible(bool visible)
        {
            hidden = !visible;
            if (root == null) return;
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible && slidAway) { slidAway = false; GlassMotion.Show(safe, GlassMotion.AwayOff); }
        }

        bool slidAway;

        /// <summary>A moment starts: the HUD slides away (spec's <c>ui-away</c>) rather than vanishing. It counts as hidden at once.</summary>
        public void SlideAway()
        {
            if (root == null || hidden) return;
            hidden = true;
            slidAway = true;
            GlassMotion.Hide(safe, GlassMotion.AwayOff, GlassMotion.AwayMs, () => { if (hidden) root.style.display = DisplayStyle.None; });
        }

        internal void OpenFeedback()
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
            bool tight = GlassSheet.IsShort(Screen.width, Screen.height);
            shapingSheet.SetInsets(i, tight ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
            shapingSheet.EnableInClassList("tray-sheet--short", tight);
            foreach (var s in tightSheets) s.SetInsets(i, BottomPadNow);
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
            bool firstRun = sim != null && sim.State.firstRun.active;
            topRow.EnableInClassList("hud-top--away", (outsideSheet != null && !outsideKeepsTop) || toolInHand || bannerUp || firstRun);
            if (firstRun != toolsWaiting) SetToolsWaiting(firstRun);
        }

        /// <summary>Each step of the first run has one primary and no other tools (hierarchy "First run", board 3.7), so History, Clean and
        /// Shape aren't there to tap. When it ends they arrive one at a time, in the order the run taught them. They take taps at once;
        /// only the look is staggered, so a tap never waits for the arrival.</summary>
        void SetToolsWaiting(bool waiting)
        {
            toolsWaiting = waiting;
            var tools = new[] { cleanButton, shapeButton, historyButton };
            for (int i = 0; i < tools.Length; i++)
            {
                var tool = tools[i];
                tool.pickingMode = waiting ? PickingMode.Ignore : PickingMode.Position;
                if (waiting || GlassMotion.Instant) tool.EnableInClassList(WaitingClass, waiting);
                else
                {
                    tool.AddToClassList(ArriveClass); // a transition of its own: elsewhere a tool's place is exact at once
                    tool.schedule.Execute(() => { if (!toolsWaiting) tool.RemoveFromClassList(WaitingClass); }).ExecuteLater(i * ArriveStepMs);
                }
            }
            PlaceToast();
        }

        /// <summary>The name just changed: the title shows it at once.</summary>
        internal void RefreshNow() => RefreshText();

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
