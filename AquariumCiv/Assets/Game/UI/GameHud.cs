using System;
using System.Collections.Generic;
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
        VisualElement root, safe, topRow, bottomRow, pinRow, outsideSheet, ledge;
        bool outsideKeepsTop, peekUp;
        GlassScrim openSheet;
        readonly System.Collections.Generic.Dictionary<string, GlassScrim> sheets = new();
        Label nameLabel, yearLabel;
        float inkHalo; // #604 (fb7 pass): 0 .. 1, the pale halo under dark ink while the camera is off home
        float inkLightness = -1f; // #604: eased 0 (dark ink) .. 1 (light ink); -1 = not applied yet
        PinChip pinChip;
        StoriesChip storiesChip;
        SettingsSheets settings;
        GlassButton cleanButton, shapeButton, historyButton;
        GlassIcon toolMark;
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
                ShowToolWord();
                cleanButton.EnableInClassList("kg-tool--on", on); // the word, a mark and a ring: the mode is never a silent one
            }
            if (on && hand != KitTool.None) SetHand(KitTool.None);
            CleanModeChanged?.Invoke(on);
        }

        // ---------------- P8: the Keeper's kit (keepers-hands.md section 3) ----------------
        /// <summary>The kit's tools besides the cloth (the cloth is clean mode). One in hand at a time; one finger is the tool, two the camera.</summary>
        public enum KitTool { None, Mister, Seed, Tweezers, Life, Bush, Landscape } // #731 Bush: an earned bush in hand; #730 Landscape: the plucking tool
        KitTool hand;
        bool kitBox, kitSeedShown, kitBushShown;
        KitTile kitCloth, kitMister, kitTweezers, kitLandscape;
        /// <summary>#668: the kit's one note (named <c>kit-life-note</c>, the name the tests know).</summary>
        Label kitNote;
        /// <summary>The tool in hand (the mister or the seed), or None. The cloth is <see cref="CleanMode"/>.</summary>
        public KitTool Hand => hand;
        /// <summary>A living-land box: Clean and Shape become one Kit button with the cloth, the mister and (once it comes) the seed.</summary>
        public bool KitBox => kitBox;
        public event Action<KitTool> HandChanged;

        /// <summary>#516 (playtest #510: "Done" read as "confirm" while it only closed the placement): with a kit tool in hand the Kit
        /// button says what it does, puts the tool away, under a cross. Clean mode (the cloth, the first run's wipe) keeps Done and its check.</summary>
        public const string PutAway = "Put away";

        /// <summary>The Kit/Clean button's word and mark for the state now. #661: in a kit box a tool in hand (the cloth too) turns the
        /// button into the hand chip: an icon disc, the tool's name, and a Put away pill (the cloth: Done, with its check). The button's
        /// own text keeps the old word (Put away / Done / Kit / Clean); what shows is <see cref="KitWord"/>.</summary>
        void ShowToolWord()
        {
            if (cleanButton == null) return;
            bool away = hand != KitTool.None;
            cleanButton.text = away ? PutAway : cleanMode ? "Done" : kitBox ? "Kit" : "Clean";
            if (toolMark != null) toolMark.Shape = away ? "close" : "check";
            cleanButton.EnableInClassList("kg-tool--away", away);
            bool chip = kitBox && (away || cleanMode);
            cleanButton.EnableInClassList("kg-tool--hand", chip);
            historyButton?.EnableInClassList("kg-tool--slim", chip);
            if (chip)
            {
                handIcon.Shape = HandIcon;
                handName.text = HandName;
                handAwayMark.Shape = away ? "close" : "check";
                handAwayWord.text = away ? PutAway : "Done";
            }
            bool dotWord = kitBox && !chip && cleanButton.ClassListContains("kg-tool--life");
            SetWord(cleanButton, cleanButton.text + (dotWord ? " ·" : ""));
        }

        /// <summary>#661: what the Kit button says now, as the player reads it ("Mister · Put away", "Cloth · Done", "Kit ·", "Kit").</summary>
        public string KitWord
        {
            get
            {
                if (cleanButton == null) return "";
                if (cleanButton.ClassListContains("kg-tool--hand")) return handName.text + " · " + handAwayWord.text;
                return ToolWordOf(cleanButton);
            }
        }

        /// <summary>Debug (kit states, #668): the kit shows its tools only, as a box before its first seed or jar would (staged: today every
        /// tool is there from the start and the seed comes as the first run ends).</summary>
        public static bool DebugToolsOnly { get; set; }

        /// <summary>Debug (kit states, #668): a tool tile wears the New state (no tool arrives in the game yet); null clears it.</summary>
        public void DebugNewTool(string tileName)
        {
            foreach (var t in new[] { kitCloth, kitMister, kitTweezers, kitLandscape }) if (t != null) t.New = t.name == tileName;
        }

        /// <summary>Debug and shots: a shut jar picked, as a tap on its tile does (its need goes in the note).</summary>
        public void DebugPickShut(AquariumCiv.Sim.Eco.LifeSpecies s) { lockedPick = s; pickTapped = true; lifeRowKey = ""; if (OpenSheet == Kit) FillJars(); }

        /// <summary>Debug and shots: every toast, waiting or up, goes.</summary>
        public void DebugDropToasts() => DropToasts();

        /// <summary>The kit's tiles by name (tests): a tool, a ready thing or a shut jar; null when it is not in the kit now.</summary>
        public KitTile KitTileNamed(string name) => root?.Q<KitTile>(name);

        /// <summary>The name in the hand chip: the tool, or the group ("The rabbits").</summary>
        string HandName => hand switch
        {
            KitTool.Mister => "Mister",
            KitTool.Tweezers => "Tweezers",
            KitTool.Landscape => LandscapeLabel,
            KitTool.Seed => "Seed",
            KitTool.Bush => "Bushes",
            KitTool.Life => "The " + AquariumCiv.Sim.Eco.EcoModel.PlainName(heldLife).ToLowerInvariant(),
            _ => "Cloth",
        };

        string HandIcon => hand switch
        {
            KitTool.Mister => "mister",
            KitTool.Tweezers => "tweezers",
            KitTool.Landscape => "spade",
            KitTool.Seed => SeedIcon,
            KitTool.Bush => LifeIcon(AquariumCiv.Sim.Eco.LifeSpecies.Thicket),
            KitTool.Life => LifeIcon(heldLife),
            _ => "cloth",
        };

        /// <summary>Put a kit tool in hand (None puts it down). Clean mode ends; the Kit button reads Put away and stays lit while it's held.</summary>
        public void SetHand(KitTool t)
        {
            if (hand == t) return;
            if (t != KitTool.None) { SetCleanMode(false); CloseSheet(); }
            var was = hand;
            hand = t;
            if (cleanButton != null)
            {
                bool on = t != KitTool.None || cleanMode;
                ShowToolWord();
                cleanButton.EnableInClassList("kg-tool--on", on);
            }
            if (t == KitTool.Mister && MisterHintDue) { Toast(MisterHint, 4f); NoteMisterHintShown(); }
            if (t == KitTool.Seed) { seedHintShown = SeedHintNow; Toast(seedHintShown, 4f); }
            else if (was == KitTool.Seed) WithdrawToast(seedHintShown); // #558: the seed is back in the jar; its hint no longer applies (it would sit in the queue and show with nothing in hand)
            if (t == KitTool.Tweezers) Toast(TweezersHint, 4f);
            if (t == KitTool.Life) Toast(LifeHintFor(heldLife), 4f);
            if (t == KitTool.Bush) Toast(BushHint, 4f);
            else if (was == KitTool.Bush) WithdrawToast(BushHint);
            if (t == KitTool.Landscape) Toast(LandscapeHint, 4f);
            HandChanged?.Invoke(t);
        }

        // Tester run 2: the Mister's hint is for the first meeting. It shows the first time the mister is picked up, once more if the
        // player put it down without misting, then never again (PlayerPrefs: it is about the player, not the box).
        // #668 (kit-words.md §3, K4 built as proposed; K3: the tree's own words stay as the game has them): one instruction, then the one tail.
        public const string ViewTail = "Two fingers move the view.";
        public const string MisterHint = "Hold a finger on the land to mist it. " + ViewTail;
        /// <summary>#311: the tweezers pull what overgrows; the cloth keeps the glass (#730: saplings are the spade's).</summary>
        public const string TweezersHint = "Tap or drag over weeds and vines to pull them. " + ViewTail;
        /// <summary>#518: the seed places like the life slot: a tap shows where it would go, a second tap on that plants it.</summary>
        public const string SeedHint = "One seed: it grows into the box's tree, anywhere, even on bare sand. Tap where it should go. " + ViewTail; // #675
        /// <summary>#727: once the Keeper's tree stands, the seed in hand is an earned one (#726): it grows into a young wild tree.</summary>
        public const string EarnedSeedHint = "This seed grows a young tree. Tap where it should go. " + ViewTail;
        string seedHintShown = SeedHint;
        /// <summary>The seed's hint now: the Keeper's one tree, or an earned seed.</summary>
        public string SeedHintNow { get { var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null; return sim != null && sim.CanPlantSeed && !sim.CanPlantTree ? EarnedSeedHint : SeedHint; } }
        /// <summary>The seed hint up now (SeedPlaceView takes it down when its ghost shows).</summary>
        public string SeedHintShown => seedHintShown;
        /// <summary>#731: an earned bush in hand.</summary>
        public const string BushHint = "Bushes: tap the glowing ground to pick a spot. " + ViewTail;
        /// <summary>#730: the plucking tool's word on its button (the name is still the user's to pick: SimCore.LandscapeToolName as is).</summary>
        public static string LandscapeLabel
        {
            get
            {
                var n = AquariumCiv.Sim.SimCore.LandscapeToolName;
                if (n.StartsWith("the ")) n = n.Substring(4);
                return char.ToUpperInvariant(n[0]) + n.Substring(1);
            }
        }
        /// <summary>#730: what the plucking tool does, as it is picked up.</summary>
        public const string LandscapeHint = "Tap or drag over seedlings, trees and bushes to lift them out. Your tree stays. " + ViewTail;
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

        // ---------------- #668: the kit, mock-up A "the tile shelf" (kit-redesign.md §4) ----------------
        // Three small labels over rows of square tiles (four to a row): Tools; Ready (the seed, earned bushes, each open jar); Still to
        // come (#662: each shut jar with its short need, a lock, never faded). One note under them. Nothing hides behind a jar any more:
        // the Kit, then the tile, then the land.
        VisualElement toolsGrid, readyGrid, shutGrid;
        Label readySec, shutSec;
        /// <summary>The ready things the player has seen in an open kit: a tile is New until then (tests read it too).</summary>
        readonly HashSet<string> seenTiles = new();
        readonly List<KitTile> shownReady = new();

        void BuildKit()
        {
            var sheet = Sheet(Kit, "The Keeper's kit");
            sheet.AddToClassList("kit-sheet"); // tester 2026-11-08: grows past 30 % when its tiles need it (a full kit, a tablet)
            var shelf = new VisualElement { name = "kit-shelf", pickingMode = PickingMode.Ignore };
            shelf.AddToClassList("kit-shelf");
            shelf.Add(Section("Tools", "kit-sec-tools"));
            toolsGrid = Grid("kit-tools");
            toolsGrid.Add(kitCloth = new KitTile("Cloth", "cloth", "wipes the glass", () => { CloseSheet(); SetCleanMode(true); }) { name = "kit-cloth" });
            toolsGrid.Add(kitMister = new KitTile("Mister", "mister", "wets the land", () => SetHand(KitTool.Mister)) { name = "kit-mister" });
            toolsGrid.Add(kitTweezers = new KitTile("Tweezers", "tweezers", "pull weeds", () => SetHand(KitTool.Tweezers)) { name = "kit-tweezers" });
            // #730 / K2: the plucking tool is the Spade (it pulls grown trees too, K6)
            toolsGrid.Add(kitLandscape = new KitTile(LandscapeLabel, "spade", "lifts plants", () => SetHand(KitTool.Landscape)) { name = "kit-landscape" });
            kitLandscape.End = true;
            shelf.Add(toolsGrid);
            // the jars: Ready, then Still to come (kit-life-row keeps the name the tests know; it shows while either has a tile)
            kitLifeRow = new VisualElement { name = "kit-life-row", pickingMode = PickingMode.Ignore };
            kitLifeRow.AddToClassList("kit-life-row");
            kitLifeRow.Add(readySec = Section("Ready", "kit-sec-ready"));
            kitLifeRow.Add(readyGrid = Grid("kit-ready"));
            kitLifeRow.Add(shutSec = Section("Still to come", "kit-sec-shut"));
            kitLifeRow.Add(shutGrid = Grid("kit-shut"));
            shelf.Add(kitLifeRow);
            kitNote = Text("", "type-note", "kit-note");
            kitNote.name = "kit-life-note";
            kitNote.pickingMode = PickingMode.Ignore;
            shelf.Add(kitNote);
            sheet.Add(shelf);
        }

        static Label Section(string word, string name)
        {
            var l = new Label(word.ToUpperInvariant()) { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList("kit-sec");
            return l;
        }

        static VisualElement Grid(string name)
        {
            var g = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            g.AddToClassList("kit-grid");
            return g;
        }

        // ---------------- #423: the life jars (placed-life spec section 6), as tiles since #668 ----------------
        VisualElement kitLifeRow, kitDot, kitDotHalo;
        AquariumCiv.Sim.Eco.LifeSpecies heldLife;
        readonly System.Collections.Generic.List<AquariumCiv.Sim.Eco.LifeSpecies> readyJars = new();
        // #570: the shut jars stand in the kit, each with what it waits for; a tap on one says it all in the note, never nothing
        readonly System.Collections.Generic.List<AquariumCiv.Sim.Eco.LifeSpecies> lockedJars = new();
        AquariumCiv.Sim.Eco.LifeSpecies? lockedPick;
        bool pickTapped;
        string lifeRowKey = "";
        bool lifeShown;
        public const string LifeWaterHint = "Tap the water to pick a spot. " + ViewTail; // fb4 #542
        public const string LifeHint = "Tap the glowing ground to pick a spot. " + ViewTail;
        /// <summary>#643 (playtest): the hint as a group is taken names it first, so the player knows what is in hand.</summary>
        public static string LifeHintFor(AquariumCiv.Sim.Eco.LifeSpecies s) => s switch
        {
            AquariumCiv.Sim.Eco.LifeSpecies.Ducks => "The ducks: " + Lower(LifeWaterHint),
            AquariumCiv.Sim.Eco.LifeSpecies.Fish => "The fish: " + Lower(LifeWaterHint),
            AquariumCiv.Sim.Eco.LifeSpecies.Thicket => "The bushes: " + Lower(LifeHint),
            AquariumCiv.Sim.Eco.LifeSpecies.Rabbits => "The rabbits: " + Lower(LifeHint),
            AquariumCiv.Sim.Eco.LifeSpecies.Deer => "The deer: " + Lower(LifeHint),
            _ => "The wolves: tap a glowing den spot in the wood. " + ViewTail,
        };
        static string Lower(string h) => char.ToLowerInvariant(h[0]) + h.Substring(1);
        /// <summary>The species in hand while <see cref="Hand"/> is <see cref="KitTool.Life"/>.</summary>
        public AquariumCiv.Sim.Eco.LifeSpecies HeldLife => heldLife;
        /// <summary>How many times the ready wiggle played (tests).</summary>
        public int LifeWiggles { get; private set; }
        /// <summary>How many #660 "ready in your kit" toasts were posted (tests).</summary>
        public int ReadyToasts { get; private set; }
        /// <summary>The jar shelves (Ready, Still to come) show in the open kit (tests; #668: no jar to tap open any more).</summary>
        public bool LifeRowOpen => kitLifeRow != null && OpenSheet == Kit && kitLifeRow.resolvedStyle.display == DisplayStyle.Flex && kitLifeRow.style.display != DisplayStyle.None;
        /// <summary>#570 / #668 the kit's one note: what the picked shut jar waits for and what the village waits for, or what to do now (tests).</summary>
        public string LifeNoteText => kitNote != null ? kitNote.text : "";
        /// <summary>#570 the shut jar the note speaks of (tests); null when none is shut.</summary>
        public AquariumCiv.Sim.Eco.LifeSpecies? LockedPick => lockedPick;
        /// <summary>A jar is ready (its tile is in the kit's Ready shelf).</summary>
        public bool LifeSlotShown => lifeShown;
        /// <summary>The Kit button carries the small dot (a jar is ready and nothing is in hand).</summary>
        public bool LifeDotShown => cleanButton != null && cleanButton.ClassListContains("kg-tool--life");
        /// <summary>#660: the Kit button's dot is pulsing (a toggled class on a schedule; tests).</summary>
        public bool KitDotPulsing => kitPulseOn;

        /// <summary>Takes the species in hand (the life tool): its ground glows (LifePlaceView), the next taps place it.</summary>
        public void TakeLife(AquariumCiv.Sim.Eco.LifeSpecies s)
        {
            heldLife = s;
            if (hand == KitTool.Life) { ShowToolWord(); HandChanged?.Invoke(hand); return; }
            SetHand(KitTool.Life);
        }

        /// <summary>#668: fills the Ready and Still to come shelves (rebuilt when what they hold changes) and the note (every call).</summary>
        void FillJars()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim != null) { sim.ReadyJars(readyJars); sim.LockedJars(lockedJars); } else { readyJars.Clear(); lockedJars.Clear(); }
            bool firstRun = sim != null && sim.State.firstRun.active;
            if (firstRun) lockedJars.Clear(); // #570: the shut list waits for the first run to end
            if (DebugToolsOnly) { readyJars.Clear(); lockedJars.Clear(); }
            if (lockedPick.HasValue && !lockedJars.Contains(lockedPick.Value)) { lockedPick = null; pickTapped = false; }
            bool readyAny = kitSeedShown || kitBushShown || readyJars.Count > 0;
            // the note speaks of a shut jar when one was tapped, or when nothing is ready (the first shut one, as before)
            if (!lockedPick.HasValue && !readyAny && lockedJars.Count > 0) lockedPick = lockedJars[0];
            if (lockedPick.HasValue && readyAny && !pickTapped) lockedPick = null;
            int seeds = sim != null && !sim.CanPlantTree ? sim.SeedsInPouch : 0;
            int bushes = sim != null ? sim.BushesInPouch : 0;
            string key = string.Join(",", readyJars) + (kitSeedShown ? ",seed" + seeds : "") + (kitBushShown ? ",bush" + bushes : "") + "|" + string.Join(",", lockedJars) + "|" + lockedPick;
            if (key != lifeRowKey)
            {
                lifeRowKey = key;
                readyGrid.Clear(); shutGrid.Clear(); shownReady.Clear();
                if (kitSeedShown)
                {
                    // 2026-11-14: the seed is the first rung, so it comes first. #675: a sprout in a mound, named "Seed". #726: earned ones count.
                    bool keepers = sim == null || sim.CanPlantTree;
                    var t = new KitTile("Seed", SeedIcon, keepers ? "the box's tree" : "a young tree", () => SetHand(KitTool.Seed)) { name = "kit-life-seed" };
                    t.Count = keepers ? 0 : seeds;
                    Ready(t, keepers ? "seed" : "seed+");
                }
                if (kitBushShown)
                {
                    // #731: earned bushes, one at a time; the badge carries the count
                    var t = new KitTile("Bushes", LifeIcon(AquariumCiv.Sim.Eco.LifeSpecies.Thicket), "grow berries", () => SetHand(KitTool.Bush)) { name = "kit-life-bush" };
                    t.Count = bushes;
                    Ready(t, "bush+");
                }
                foreach (var sp in readyJars)
                {
                    var s = sp;
                    var t = new KitTile(AquariumCiv.Sim.Eco.EcoModel.PlainName(s), LifeIcon(s), WhereLine(s), () => TakeLife(s)) { name = "kit-life-" + s.ToString().ToLowerInvariant() };
                    Ready(t, "jar-" + s);
                }
                // #570 / #662: the shut jars, after the ready ones, under their own label: the short need shows without a tap
                int i = 0;
                foreach (var sp in lockedJars)
                {
                    var s = sp;
                    var t = new KitTile(AquariumCiv.Sim.Eco.EcoModel.PlainName(s), LifeIcon(s), ShortNeed(s, sim), () => { lockedPick = s; pickTapped = true; FillJars(); }) { name = "kit-life-locked-" + s.ToString().ToLowerInvariant() };
                    t.Shut = true;
                    t.Picked = lockedPick == s;
                    t.End = ++i % 4 == 0;
                    shutGrid.Add(t);
                }
                for (int k = 0; k < shownReady.Count; k++) shownReady[k].End = (k + 1) % 4 == 0;
                bool anyReady = readyGrid.childCount > 0, anyShut = shutGrid.childCount > 0;
                readySec.style.display = readyGrid.style.display = anyReady ? DisplayStyle.Flex : DisplayStyle.None;
                shutSec.style.display = shutGrid.style.display = anyShut ? DisplayStyle.Flex : DisplayStyle.None;
                kitLifeRow.style.display = anyReady || anyShut ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else foreach (var t in shutGrid.Children()) if (t is KitTile kt) kt.Line = ShortNeed(LockedOf(kt), sim);
            string note = KitNoteNow(sim);
            if (kitNote != null && kitNote.text != note) kitNote.text = note;
        }

        static AquariumCiv.Sim.Eco.LifeSpecies LockedOf(KitTile t)
        {
            foreach (var s in AquariumCiv.Sim.Eco.LifeJar.All) if (t.name == "kit-life-locked-" + s.ToString().ToLowerInvariant()) return s;
            return AquariumCiv.Sim.Eco.LifeSpecies.Thicket;
        }

        void Ready(KitTile t, string seenKey)
        {
            t.New = !seenTiles.Contains(seenKey);
            t.userData = seenKey;
            shownReady.Add(t);
            readyGrid.Add(t);
        }

        /// <summary>The kit closed: every ready tile it showed has been seen, so its New tag goes.</summary>
        void SeeReadyTiles()
        {
            foreach (var t in shownReady) if (t.userData is string k && seenTiles.Add(k)) t.New = false;
        }

        /// <summary>The kit's one note (kit-words.md §2): a tapped shut jar's need first, else what to do with what is ready, else the
        /// tree's line; the village's waits line under it (#570, signed off row 59).</summary>
        string KitNoteNow(AquariumCiv.Sim.SimCore sim)
        {
            if (sim == null) return "";
            string line;
            if (lockedPick.HasValue) line = sim.JarNeed(lockedPick.Value) ?? PlainReady(lockedPick.Value);
            else if (readyJars.Count > 0) line = ReadyLine(readyJars[0]);
            else if (kitSeedShown && sim.CanPlantTree) line = "The seed grows the box's one tree. Tap it, then the land.";
            else if (kitSeedShown) line = "A seed waits. Tap it, then the land.";
            else if (kitBushShown) line = "A bush waits. Tap it, then the land.";
            else if (sim.EcoTree.cell >= 0) line = "Mist the tree when the land looks dry.";
            else line = "A seed comes when the land can hold a tree.";
            string waits = sim.VillageWaitsLine ?? "";
            return line.Length > 0 && waits.Length > 0 ? line + "\n" + waits : line + waits;
        }

        /// <summary>"The rabbits are ready. Tap them, then the meadow."</summary>
        public static string ReadyLine(AquariumCiv.Sim.Eco.LifeSpecies s) =>
            "The " + AquariumCiv.Sim.Eco.EcoModel.PlainName(s).ToLowerInvariant() + " are ready. Tap them, then " + PlaceWord(s) + ".";

        static string PlaceWord(AquariumCiv.Sim.Eco.LifeSpecies s) => s switch
        {
            AquariumCiv.Sim.Eco.LifeSpecies.Rabbits => "the meadow",
            AquariumCiv.Sim.Eco.LifeSpecies.Deer => "the wood",
            AquariumCiv.Sim.Eco.LifeSpecies.Wolves => "the wood",
            AquariumCiv.Sim.Eco.LifeSpecies.Ducks => "the pond",
            AquariumCiv.Sim.Eco.LifeSpecies.Fish => "the pond",
            _ => "the land",
        };

        /// <summary>The line under a ready group's tile: where it goes.</summary>
        public static string WhereLine(AquariumCiv.Sim.Eco.LifeSpecies s) => s == AquariumCiv.Sim.Eco.LifeSpecies.Thicket ? "grow berries" : "for " + PlaceWord(s);

        /// <summary>#662: the line under a shut jar's tile, about 15 letters: its need in short (the full line goes in the note on a tap).</summary>
        public static string ShortNeed(AquariumCiv.Sim.Eco.LifeSpecies s, AquariumCiv.Sim.SimCore sim)
        {
            string need = sim?.JarNeed(s);
            if (need == null) return "comes soon";
            // read from EcoModel.JarNeed's lines, in its order: the rung below first, then the land
            string n = need.ToLowerInvariant();
            if (n.Contains(" ice")) return "pond is ice";
            if (n.Contains(" first"))
            {
                if (n.Contains("seed in the ground")) return "needs the tree";
                int a = n.IndexOf(" need the ", StringComparison.Ordinal);
                if (a >= 0) return "needs " + n.Substring(a + 10).Replace(" first", "");
            }
            if (n.Contains("bigger meadow")) return "more meadow";
            if (n.Contains("meadow")) return "needs a meadow";
            if (n.Contains("thicker wood")) return "thicker wood";
            if (n.Contains("bigger pond")) return "a bigger pond";
            if (n.Contains("pond") && s == AquariumCiv.Sim.Eco.LifeSpecies.Ducks) return "needs a pond";
            if (n.Contains("reeds")) return "needs reeds";
            if (n.Contains("ducks to settle")) return "ducks settling";
            if (n.Contains("herd of")) return "needs deer";
            if (n.Contains("deeper wood")) return "deeper wood";
            if (n.Contains("den")) return "a den spot";
            if (n.Contains("hold its numbers")) return "a steady herd";
            return "still to come";
        }

        /// <summary>#726: the seed's word for tests and the debug panel: "Seed", then the count of earned seeds ("2 seeds").</summary>
        public string SeedWord
        {
            get
            {
                var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
                if (sim == null || sim.CanPlantTree) return "Seed";
                int n = sim.SeedsInPouch;
                return n == 1 ? "1 seed" : n + " seeds";
            }
        }
        /// <summary>#726: the earned bushes' word ("1 bush", "3 bushes").</summary>
        public string BushWord
        {
            get
            {
                var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
                int n = sim != null ? sim.BushesInPouch : 0;
                return n == 1 ? "1 bush" : n + " bushes";
            }
        }
        /// <summary>#726: the kit shows an earned bush (tests).</summary>
        public bool BushChoiceShown => kitBushShown;

        /// <summary>#675: the seed's icon (Icons/life-seed.svg): a sprout in a small mound, not a grown tree.</summary>
        public const string SeedIcon = "life-seed";

        /// <summary>A shut jar whose land is ready already opens at the season's end.</summary>
        static string PlainReady(AquariumCiv.Sim.Eco.LifeSpecies s) => AquariumCiv.Sim.Eco.EcoModel.PlainName(s) + " come at the season's end";

        /// <summary>The icon of each species (Icons/life-*.svg, drawn for this project).</summary>
        public static string LifeIcon(AquariumCiv.Sim.Eco.LifeSpecies s) => s switch
        {
            AquariumCiv.Sim.Eco.LifeSpecies.Thicket => "life-thicket",
            AquariumCiv.Sim.Eco.LifeSpecies.Rabbits => "life-rabbits",
            AquariumCiv.Sim.Eco.LifeSpecies.Deer => "life-stag",
            AquariumCiv.Sim.Eco.LifeSpecies.Ducks => "life-duck",
            AquariumCiv.Sim.Eco.LifeSpecies.Fish => "life-fish",
            AquariumCiv.Sim.Eco.LifeSpecies.Wolves => "life-wolf",
            _ => "life-jar",
        };

        VisualElement photoButton, settingsButton;

        /// <summary>#647 (playtest: a first run played with no UI at all): photo mode hides every prompt, and only a short tap brings them
        /// back, which misting (a held drag) never is. While the first run is on, the Photo button steps out and photo mode ends.</summary>
        void GuardFirstRun(AquariumCiv.Sim.SimCore sim)
        {
            bool first = sim.State.firstRun.active;
            var show = first ? DisplayStyle.None : DisplayStyle.Flex;
            if (photoButton != null && photoButton.style.display != show) photoButton.style.display = show;
            if (first && cameraMode) SetCameraMode(false);
        }

        /// <summary>The visit the wiggle last played in (#660: once per visit while a jar waits, not once ever).</summary>
        int visitNo = 1, wiggledVisit;
        bool kitPulseOn;
        IVisualElementScheduledItem kitPulse;
        readonly HashSet<AquariumCiv.Sim.Eco.LifeSpecies> toastedJars = new();
        float awaySince = -1f;
        /// <summary>#660: how long the app must be away before coming back counts as a new visit (s).</summary>
        public const float NewVisitAwaySeconds = 60f;

        void OnApplicationPause(bool paused)
        {
            if (paused) awaySince = Time.realtimeSinceStartup;
            else if (awaySince >= 0f) { if (Time.realtimeSinceStartup - awaySince >= NewVisitAwaySeconds) visitNo++; awaySince = -1f; }
        }

        /// <summary>Debug and tests: the next frame counts as a new visit (the wiggle may play again while a jar waits).</summary>
        public void DebugNewVisit() => visitNo++;

        /// <summary>Each frame: the Ready shelf follows the jars; the Kit button keeps a pulsing dot ("Kit ·") while one waits; a jar that
        /// opened (here or while away) says so once in a toast (#660) and wiggles the Kit button once each visit, the next time the HUD is
        /// up to see it.</summary>
        void FollowLife(AquariumCiv.Sim.SimCore sim)
        {
            bool any = sim.AnyJarReady;
            lifeShown = any;
            bool dot = any && hand != KitTool.Life;
            if (cleanButton != null && cleanButton.ClassListContains("kg-tool--life") != dot) { cleanButton.EnableInClassList("kg-tool--life", dot); ShowToolWord(); }
            SetKitPulse(dot && !hidden && !cameraMode);
            if (hand == KitTool.Life && !sim.JarReady(heldLife)) SetHand(KitTool.None); // placed (or gone): the tool is put down
            if (OpenSheet == Kit) FillJars();
            if (!any || hidden || cameraMode || toolsWaiting || toolBlocks.Count > 0 || sim.State.firstRun.active) return;
            bool fresh = false;
            foreach (var s in AquariumCiv.Sim.Eco.LifeJar.All)
                if (sim.AckJarReady(s))
                {
                    fresh = true;
                    // #660: a toast once per opened jar, on a calm moment (no sheet up; the queue waits for any prompt)
                    if (openSheet == null && outsideSheet == null && toastedJars.Add(s)) { Toast(ReadyToast(s), 4f); ReadyToasts++; }
                }
            if (!fresh && wiggledVisit == visitNo) return;
            wiggledVisit = visitNo;
            LifeWiggles++;
            UiSound.Play(UiRole.NewTool);
            Wiggle(cleanButton);
        }

        /// <summary>#660: "The rabbits are ready in your kit."</summary>
        public static string ReadyToast(AquariumCiv.Sim.Eco.LifeSpecies s) => "The " + AquariumCiv.Sim.Eco.EcoModel.PlainName(s).ToLowerInvariant() + " are ready in your kit.";

        /// <summary>#660: the Kit's dot breathes (a halo grows and fades, about once a second) while something waits; still in tests.</summary>
        void SetKitPulse(bool on)
        {
            if (kitPulseOn == on || kitDotHalo == null) return;
            kitPulseOn = on;
            kitPulse?.Pause();
            kitDotHalo.RemoveFromClassList("kg-dot-halo--out");
            if (!on || GlassMotion.Instant) return;
            bool outNow = false;
            kitPulse = kitDotHalo.schedule.Execute(() => { outNow = !outNow; kitDotHalo.EnableInClassList("kg-dot-halo--out", outNow); }).Every(PulseMs);
        }

        /// <summary>#660: one half of the dot's breath (ms).</summary>
        public const int PulseMs = 800;

        static readonly float[] WiggleDeg = { -9f, 9f, -7f, 7f, -4f, 3f, 0f };
        /// <summary>One wiggle: a few quick turns either way, then still.</summary>
        static void Wiggle(VisualElement e)
        {
            if (e == null || GlassMotion.Instant) return;
            for (int i = 0; i < WiggleDeg.Length; i++)
            {
                float a = WiggleDeg[i];
                e.schedule.Execute(() => e.style.rotate = new Rotate(new Angle(a, AngleUnit.Degree))).ExecuteLater(i * 70);
            }
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
                ShowToolWord();
                if (shapeButton != null) shapeButton.style.display = eco || toolBlocks.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (sim != null) GuardFirstRun(sim); // #647: every box, the living land or not
            if (!eco || kitLifeRow == null) { SetKitPulse(false); return; }
            // 2026-11-14: the seed is the first rung, in the kit as the first run ends; #726/#727: then each earned seed
            bool seed = sim.CanPlantAnySeed && !sim.State.firstRun.active && !DebugToolsOnly;
            bool seedChanged = seed != kitSeedShown;
            kitSeedShown = seed;
            // #731: an earned bush, once the first thicket is down
            bool bush = sim.BushesInPouch > 0 && sim.LifeIsPlaced(AquariumCiv.Sim.Eco.LifeSpecies.Thicket) && !sim.State.firstRun.active && !DebugToolsOnly;
            bool bushChanged = bush != kitBushShown;
            kitBushShown = bush;
            if (seedChanged && seed) seenTiles.Remove(sim.CanPlantTree ? "seed" : "seed+");
            if (bushChanged && bush) seenTiles.Remove("bush+");
            FollowLife(sim);
            if (seedChanged)
            {
                if (seed)
                {
                    SetShapeNew(true);
                    bool both = bush && bushChanged;
                    Toast(sim.CanPlantTree ? "A seed is in your kit. It grows the box's one tree, even on bare sand."
                        : both ? "The village grew. A seed and a bush wait in your kit." : "The village grew. A seed waits in your kit.", 5f);
                }
                if (!seed && hand == KitTool.Seed) SetHand(KitTool.None);
            }
            if (bushChanged)
            {
                if (bush && !(seed && seedChanged)) { SetShapeNew(true); Toast("The village grew. A bush waits in your kit.", 5f); }
                if (!bush && hand == KitTool.Bush) SetHand(KitTool.None);
            }
            foreach (var s in AquariumCiv.Sim.Eco.LifeJar.All) if (!sim.JarReady(s)) { toastedJars.Remove(s); seenTiles.Remove("jar-" + s); }
        }

        /// <summary>The first run's wipe and flick: the hint says to drag, so nothing turns clean mode off under it, not Done (the tool isn't
        /// here yet) and not the idle timer. Whoever sets this owns it, and clears it before the step ends.</summary>
        public bool CleanLocked { get; set; }

        /// <summary>Camera mode (photo mode, #70): every control, toast and sheet hides (and the IMGUI debug dock, see
        /// DebugDock.Hidden), the camera stays fully usable. A short tap anywhere (not a drag, not a pinch) or the small
        /// "Tap to show the UI" pill brings it all back. Clean mode and any open sheet are closed first.</summary>
        public bool CameraMode => cameraMode;
        /// <summary>#673: how long the photo pill shows before it fades, and how long the fade takes (s).</summary>
        public const float PillSeconds = 4f, PillFadeSeconds = 1f;

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
            var simNow = AquariumCiv.Game.GameClock.Instance != null ? AquariumCiv.Game.GameClock.Instance.Sim : null;
            AquariumCiv.Game.GameClock.Tel.PhotoMode(on, simNow != null && simNow.State.firstRun.active); // #647: the next first run tells us
        }

        // Camera mode each frame: the pill fades to a whisper after a few seconds; a tap anywhere brings the UI back.
        void UpdateCameraMode()
        {
            // #673 (user 2026-11-14): photo mode leaves no button on screen: the pill says it, then fades out fully (a tap anywhere still brings the UI back)
            float since = Time.unscaledTime - exitFadeAt;
            exitButton.style.opacity = since < PillSeconds ? 1f : Mathf.Clamp01(1f - (since - PillSeconds) / PillFadeSeconds);
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
                if (!float.IsNaN(h) && h > 0f)
                {
                    float lift = up.resolvedStyle.bottom; // #563: a bar sits above the tools lane, not on the glass edge: the toast clears its top, not its height alone
                    lane = h - i.w + GameUiPanel.ToastAboveSheet + (float.IsNaN(lift) ? 0f : Mathf.Max(0f, lift - i.w));
                }
            }
            float measured = MeasuredBarClear();
            toastLabel.style.bottom = Mathf.Max(lane, toastClear > 0f ? GameUiPanel.Lane(toastClear, i.w) : 0f, float.IsNaN(measured) ? (barClear > 0f ? GameUiPanel.Lane(barClear, i.w) : 0f) : measured);
        }

        readonly Dictionary<string, Func<float>> barProbes = new();

        /// <summary>Tester run 2: a toast over a choice must clear the bar AND the "Let them decide" button above it. The probe returns the top edge
        /// (panel y) of what the choice has up, or NaN when nothing is; null removes it. The toast sits 8 pt over the highest.</summary>
        public void SetBarProbe(string key, Func<float> topY)
        {
            if (topY == null) barProbes.Remove(key); else barProbes[key] = topY;
            PlaceToast();
        }

        /// <summary>The toast lane (style.bottom) that clears every probed element, or NaN when none is measurable yet.</summary>
        float MeasuredBarClear()
        {
            if (barProbes.Count == 0 || safe == null) return float.NaN;
            float top = float.NaN;
            foreach (var p in barProbes.Values) { float t = p(); if (!float.IsNaN(t) && (float.IsNaN(top) || t < top)) top = t; }
            float bottomEdge = safe.worldBound.yMax;
            if (float.IsNaN(top) || float.IsNaN(bottomEdge) || bottomEdge <= 0f) return float.NaN;
            return Mathf.Max(0f, bottomEdge - top + 8f);
        }

        float toastClear;
        float barClear;

        /// <summary>#563: the choice's bar (the card stack's or the box's) is up in the tools lane: toasts go over it, 0 lets them back. Its own field: the moment player's clear resets every frame.</summary>
        public void SetBarClear(float bottom)
        {
            if (Mathf.Approximately(barClear, bottom)) return;
            barClear = bottom;
            PlaceToast();
        }

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
            offHome = off; // #707: no backing behind any of the top row, home or not: the ink (and its halo) carries it
        }

        /// <summary>#707: the top row's place, shared with FeedbackUI's pill (it lives in another document): 110 down, 44 from the safe edge,
        /// each round button 144 wide. The pill is left of Photo; Settings is left of the pill.</summary>
        public const float TopRowTop = 110f, TopRowSide = 44f, TopIconSlot = 144f;

        static VisualElement FeedbackSlot()
        {
            var gap = new VisualElement { name = "hud-feedback-slot", pickingMode = PickingMode.Ignore };
            gap.style.width = TopIconSlot; gap.style.height = TopIconSlot; gap.style.flexShrink = 0;
            return gap;
        }

        public bool OffHome => offHome;

        /// <summary>#604: the title's ink eases between dark and light with the daylight; no pill behind it.</summary>
        void FollowInk()
        {
            if (nameLabel == null) return;
            float target = inkLightness < 0f ? HudInk.Target(HudInk.Daylight) : HudInk.Target(HudInk.Daylight, inkLightness);
            float next = inkLightness < 0f ? target : HudInk.Step(inkLightness, target, Time.unscaledDeltaTime);
            float halo = HudInk.Step(inkHalo, offHome ? 1f : 0f, Time.unscaledDeltaTime);
            if (Mathf.Approximately(next, inkLightness) && Mathf.Approximately(halo, inkHalo))
            {
                if (FeedbackUI.Instance != null && FeedbackUI.Instance.Pill != inkedPill) ApplyIconInk();
                return;
            }
            inkLightness = next; inkHalo = halo;
            var ink = HudInk.Ink(next);
            // fb7 pass: off home the dark ink can sit on the glass rails and the room: a soft pale halo then (the light ink keeps its dark shadow)
            var shadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 6f, color = HudInk.Shadow(next, halo) };
            foreach (var l in new[] { nameLabel, yearLabel }) { l.style.color = ink; l.style.textShadow = shadow; }
            ApplyIconInk();
        }

        VisualElement inkedPill;
        /// <summary>#707: the round buttons draw in the title's ink (no disc behind them), Feedback's too.</summary>
        void ApplyIconInk()
        {
            var tint = HudInk.Ink(inkLightness < 0f ? 0f : inkLightness);
            foreach (var b in new VisualElement[] { settingsButton, photoButton, FeedbackUI.Instance != null ? FeedbackUI.Instance.Pill : null })
                if (b != null) foreach (var ic in b.Query<GlassIcon>().ToList()) ic.style.unityBackgroundImageTintColor = tint;
            inkedPill = FeedbackUI.Instance != null ? FeedbackUI.Instance.Pill : null;
        }

        bool IsOverUi(Vector2 screenPos) => !hidden && GameUiPanel.Over(root, screenPos);

        void Update()
        {
            if (root == null) return;
            if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y) ApplySafeArea();
            UpdateBlockers();
            if (cameraMode) UpdateCameraMode();
            if (toastLabel != null) PumpToast();
            if (outsideSheet != null || barProbes.Count > 0) PlaceToast(); // it has no height until its first layout; a choice's bar and button move
            FollowFirstRun();
            FollowKit();
            FollowInk();
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
            // The name chip opens the offices list (#215). #707: Settings sits with the other round buttons, in the corner.
            var title = new GlassButton("", () => Open(Offices), ButtonKind.Quiet, UiRole.Open) { name = "hud-title" };
            title.AddToClassList("hud-title");
            var words = new VisualElement { name = "hud-title-text", pickingMode = PickingMode.Ignore };
            words.AddToClassList("hud-title-text");
            words.Add(nameLabel = Text("Your box", "hud-title-name", "type-box"));
            words.Add(yearLabel = Text("Year 1", "hud-title-year", "type-sub"));
            nameLabel.pickingMode = yearLabel.pickingMode = PickingMode.Ignore;
            title.Add(words);
            topRow.Add(title);
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("hud-spacer");
            topRow.Add(spacer);
            var corner = new VisualElement { name = "hud-corner", pickingMode = PickingMode.Ignore };
            corner.AddToClassList("hud-corner");
            // #702: the Feedback pill isn't here any more: FeedbackUI draws it over every panel, in this place, left of Photo (#650: full ink)
            if (FeedbackUI.InTestBuild && FeedbackUI.AutoCreate && FeedbackUI.Instance == null) FeedbackUI.Create();
            corner.Add(settingsButton = IconButton("hud-settings", "settings", () => Open(Settings), UiRole.Open));
            if (FeedbackUI.InTestBuild) corner.Add(FeedbackSlot()); // #707: the pill (FeedbackUI's document) hangs in this gap, between Settings and Photo
            corner.Add(photoButton = IconButton("hud-photo", "photo", () => SetCameraMode(true), UiRole.Toggle));
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
            // #668 (feedback#70): the glass ledge the tools stand on: a pale band with a brass-light top line, behind them
            ledge = new VisualElement { name = "hud-ledge", pickingMode = PickingMode.Ignore };
            ledge.AddToClassList("hud-ledge");
            bottomRow.Add(ledge);
            bottomRow.Add(historyButton = Tool("hud-history", "History", OnHistory, UiRole.Open));
            bottomRow.Add(cleanButton = Tool("hud-clean", "Clean", () => { if (kitBox) OnKitButton(); else SetCleanMode(!cleanMode); }, UiRole.Toggle));
            cleanButton.Add(toolMark = new GlassIcon("check", "kg-icon--small", "kg-tool-mark"));
            cleanButton.Add(new GlassNewTag());
            BuildHandChip();
            kitDot = new VisualElement { name = "hud-kit-dot", pickingMode = PickingMode.Ignore }; // #423: a jar is ready
            kitDot.AddToClassList("kg-dot");
            kitDot.AddToClassList("kg-tool-dot");
            kitDotHalo = new VisualElement { name = "hud-kit-dot-halo", pickingMode = PickingMode.Ignore }; // #660: its soft pulse
            kitDotHalo.AddToClassList("kg-dot-halo");
            kitDot.Add(kitDotHalo);
            cleanButton.Add(kitDot);
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

        /// <summary>#572: set by the person card (View): open a notable's card and bring the camera to them.</summary>
        public static Action<int> OpenPerson;

        void OfficeHolderTapped(int notableId)
        {
            if (OpenPerson != null) OpenPerson(notableId); // the person card (View) closes this sheet
        }

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
                    VisualElement who;
                    if (holder != null)
                    {
                        // #572: the holder's name is a button: it opens their card (and the camera brings them over), as tapping the person does.
                        int hid = holder.id;
                        var btn = new GlassButton(holder.name + "  ›", () => OfficeHolderTapped(hid), ButtonKind.Quiet, UiRole.Open) { name = row.name + "-holder", Compact = true };
                        btn.AddToClassList("office-holder");
                        who = btn;
                    }
                    else
                    {
                        who = Text("Vacant", "type-body", "office-holder");
                        who.AddToClassList("office-vacant");
                    }
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
            if (id == Kit) { SetHand(KitTool.None); pickTapped = false; lockedPick = null; lifeRowKey = ""; } // #668: the kit opens with every shelf filled, no shut jar picked
            if (!sheets.TryGetValue(id, out scrim) || openSheet == scrim) return;
            if (openSheet != null) { openSheet.style.display = DisplayStyle.None; openSheet.Sheet.Close(); }
            openSheet = scrim;
            if (id == Kit && kitLifeRow != null) FillJars();
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
            if (sheets.TryGetValue(Kit, out var kitScrim) && closing == kitScrim) SeeReadyTiles();
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

        /// <summary>The pill's tap (#702): the HUD shuts its own sheet first, then the sheet opens.</summary>
        internal static void OpenFeedbackFrom(FeedbackUI fb)
        {
            if (Instance != null) Instance.CloseSheet();
            fb.Open();
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
            ledge?.EnableInClassList("hud-ledge--away", waiting); // #668: the ledge comes with the tools
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

        /// <summary>#571: the line under the box name (years count from 1, as History and the book do), built from parts: "Year 37 · 16 people". #576 fills <paramref name="season"/> ("Year 37 · Autumn · 16 people").</summary>
        public static string TitleLine(int year, string season, int population)
        {
            string line = "Year " + year.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(season)) line += " · " + season;
            return line + " · " + population.ToString(CultureInfo.InvariantCulture) + (population == 1 ? " person" : " people");
        }

        /// <summary>#576: the season slot of the title line, from the sim's month ("Spring", "Summer", "Autumn", "Winter").</summary>
        public static string SeasonWord(AquariumCiv.Sim.SimState s) => AquariumCiv.Life.LifeClock.SeasonOf(s.month).ToString();

        /// <summary>The name just changed: the title shows it at once.</summary>
        public void RefreshNow() => RefreshText();

        void RefreshText()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim == null) return;
            var s = sim.State;
            string place = string.IsNullOrEmpty(s.settlementName) ? "Your box" : s.settlementName;
            nameLabel.text = place;
            yearLabel.text = TitleLine(AquariumCiv.Sim.SimCore.YearNo(s.month), SeasonWord(s), s.population);
            if (historyBody != null)
                historyBody.text = $"{place}, year {AquariumCiv.Sim.SimCore.YearNo(s.month)}. {s.population} {(s.population == 1 ? "person lives" : "people live")} here.";
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

        /// <summary>A bottom-bar tool. #668 (feedback#70, "the bottom looks flat"): it has depth, drawn in layers because UI Toolkit has no
        /// box-shadow or gradient: a soft shadow (GlassShadow.png, 9-sliced), a darker lip under it, the face (ToolFace.png: light-to-dark
        /// paper with a light top inset and a darker bottom inset), then the word. The button's own text stays (tests and the twin read
        /// it) but is drawn clear; <see cref="SetWord"/> sets both.</summary>
        static GlassButton Tool(string name, string label, Action onClick, UiRole role)
        {
            var b = new GlassButton(label, onClick, ButtonKind.Secondary, role) { name = name };
            b.AddToClassList("kg-tool");
            foreach (var layer in new[] { "kg-tool-shade", "kg-tool-lip", "kg-tool-face" })
            {
                var e = new VisualElement { pickingMode = PickingMode.Ignore };
                e.AddToClassList(layer);
                b.Add(e);
            }
            var word = new Label(label) { name = name + "-word", pickingMode = PickingMode.Ignore };
            word.AddToClassList("kg-tool-word");
            b.Add(word);
            return b;
        }

        /// <summary>A tool's word, as drawn (the button's text too, minus the "·" the word may carry).</summary>
        static void SetWord(GlassButton tool, string s)
        {
            var w = tool.Q<Label>(tool.name + "-word");
            if (w != null && w.text != s) w.text = s;
        }

        static string ToolWordOf(GlassButton tool) => tool.Q<Label>(tool.name + "-word")?.text ?? tool.text;

        // #661: the hand chip's pieces, in the Kit button
        GlassIcon handIcon, handAwayMark;
        Label handName, handAwayWord;

        void BuildHandChip()
        {
            var disc = new VisualElement { name = "hud-hand-disc", pickingMode = PickingMode.Ignore };
            disc.AddToClassList("kg-hand-disc");
            disc.Add(handIcon = new GlassIcon("cloth", "kg-hand-icon"));
            cleanButton.Add(disc);
            handName = new Label("") { name = "hud-hand-name", pickingMode = PickingMode.Ignore };
            handName.AddToClassList("kg-hand-name");
            cleanButton.Add(handName);
            var away = new VisualElement { name = "hud-hand-away", pickingMode = PickingMode.Ignore };
            away.AddToClassList("kg-hand-away");
            away.Add(handAwayMark = new GlassIcon("close", "kg-icon--small", "kg-icon--light", "kg-hand-away-mark"));
            away.Add(handAwayWord = new Label(PutAway) { pickingMode = PickingMode.Ignore });
            handAwayWord.AddToClassList("kg-hand-away-word");
            cleanButton.Add(away);
            // History slims to its book while a tool is in hand (the chip takes the room)
            historyButton.Add(new GlassIcon("book", "kg-tool-icon"));
        }

        /// <summary>The 22 pt icon in its 44 pt disc, which is what the quiet backing fills.</summary>
        static VisualElement Disc(string icon)
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.AddToClassList("hud-disc");
            d.Add(new GlassIcon(icon));
            return d;
        }

        internal static GlassButton IconButton(string name, string icon, Action onClick, UiRole role, params string[] classes)
        {
            var b = new GlassButton("", onClick, ButtonKind.Icon, role) { name = name };
            b.AddToClassList("hud-icon");
            foreach (var c in classes) b.AddToClassList(c);
            b.Add(Disc(icon));
            return b;
        }
    }
}
