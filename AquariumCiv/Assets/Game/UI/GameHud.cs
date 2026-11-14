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
                ShowToolWord();
                cleanButton.EnableInClassList("kg-tool--on", on); // the word, a mark and a ring: the mode is never a silent one
            }
            if (on && hand != KitTool.None) SetHand(KitTool.None);
            CleanModeChanged?.Invoke(on);
        }

        // ---------------- P8: the Keeper's kit (keepers-hands.md section 3) ----------------
        /// <summary>The kit's tools besides the cloth (the cloth is clean mode). One in hand at a time; one finger is the tool, two the camera.</summary>
        public enum KitTool { None, Mister, Seed, Tweezers, Life }
        KitTool hand;
        bool kitBox, kitSeedShown;
        GlassButton kitCloth, kitMister, kitTweezers;
        Label kitNote;
        /// <summary>The tool in hand (the mister or the seed), or None. The cloth is <see cref="CleanMode"/>.</summary>
        public KitTool Hand => hand;
        /// <summary>A living-land box: Clean and Shape become one Kit button with the cloth, the mister and (once it comes) the seed.</summary>
        public bool KitBox => kitBox;
        public event Action<KitTool> HandChanged;

        /// <summary>#516 (playtest #510: "Done" read as "confirm" while it only closed the placement): with a kit tool in hand the Kit
        /// button says what it does, puts the tool away, under a cross. Clean mode (the cloth, the first run's wipe) keeps Done and its check.</summary>
        public const string PutAway = "Put away";

        /// <summary>The Kit/Clean button's word and mark for the state now.</summary>
        void ShowToolWord()
        {
            if (cleanButton == null) return;
            bool away = hand != KitTool.None;
            cleanButton.text = away ? PutAway : cleanMode ? "Done" : kitBox ? "Kit" : "Clean";
            if (toolMark != null) toolMark.Shape = away ? "close" : "check";
            cleanButton.EnableInClassList("kg-tool--away", away);
        }

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
            if (t == KitTool.Seed) Toast(SeedHint, 4f);
            else if (was == KitTool.Seed) WithdrawToast(SeedHint); // #558: the seed is back in the jar; its hint no longer applies (it would sit in the queue and show with nothing in hand)
            if (t == KitTool.Tweezers) Toast(TweezersHint, 4f);
            if (t == KitTool.Life) Toast(LifeHintFor(heldLife), 4f);
            if (kitLife != null) kitLife.EnableInClassList("kg-tool--on", t == KitTool.Life || t == KitTool.Seed);
            HandChanged?.Invoke(t);
        }

        // Tester run 2: the Mister's hint is for the first meeting. It shows the first time the mister is picked up, once more if the
        // player put it down without misting, then never again (PlayerPrefs: it is about the player, not the box).
        public const string MisterHint = "Hold a finger over the land to mist it. Two fingers move, turn and zoom the view.";
        /// <summary>#311: the tweezers pluck what overgrows; the cloth keeps the glass.</summary>
        public const string TweezersHint = "Tap or drag over weeds, vines and saplings to pluck them. Two fingers move, turn and zoom the view.";
        /// <summary>#518: the seed places like the life slot: a tap shows where it would go, a second tap on that plants it.</summary>
        public const string SeedHint = "Tap the land to choose where the seed goes. Two fingers move, turn and zoom the view.";
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
            sheet.AddToClassList("kit-sheet"); // tester 2026-11-08: grows past 30 % when its tools need it (the chooser on a tablet)
            kitNote = Text("", "type-note", "kg-sheet-note");
            var acts = new VisualElement();
            acts.AddToClassList("kg-card-acts");
            acts.Add(kitCloth = new GlassButton("Cloth", () => { CloseSheet(); SetCleanMode(true); }, ButtonKind.Secondary, UiRole.Toggle) { name = "kit-cloth" });
            acts.Add(kitMister = new GlassButton("Mister", () => SetHand(KitTool.Mister), ButtonKind.Secondary, UiRole.Toggle) { name = "kit-mister" });
            acts.Add(kitTweezers = new GlassButton("Tweezers", () => SetHand(KitTool.Tweezers), ButtonKind.Secondary, UiRole.Toggle) { name = "kit-tweezers" });
            acts.AddToClassList("kit-acts");
            // #423: the life slot; shown only while a jar is ready or the seed has come (#558: the seed is one of its choices)
            kitLife = new GlassButton("Life", OnLifeSlot, ButtonKind.Secondary, UiRole.Toggle) { name = "kit-life" };
            kitLife.AddToClassList("kit-life");
            kitLife.Insert(0, new GlassIcon("life-jar", "kit-life-icon"));
            var slotDot = new VisualElement { name = "kit-life-dot", pickingMode = PickingMode.Ignore };
            slotDot.AddToClassList("kg-dot");
            slotDot.AddToClassList("kit-life-dot");
            kitLife.Add(slotDot);
            kitLife.style.display = DisplayStyle.None;
            acts.Add(kitLife);
            kitLifeRow = new VisualElement { name = "kit-life-row", pickingMode = PickingMode.Ignore };
            kitLifeRow.AddToClassList("kit-life-row");
            kitNote.style.whiteSpace = WhiteSpace.Normal; // the phone's width: the note wraps, never runs off the sheet
            kitNote.style.marginBottom = 12;
            sheet.Add(kitNote);
            sheet.Add(kitLifeRow); // the chooser opens above the slot (spec section 6)
            // #570: under the chooser's icons, what the picked shut jar waits for, and what the village waits for (one label, the note's place)
            kitLifeNote = Text("", "type-note", "kg-sheet-note");
            kitLifeNote.name = "kit-life-note";
            kitLifeNote.style.whiteSpace = WhiteSpace.Normal;
            kitLifeNote.style.marginBottom = 12;
            kitLifeNote.style.display = DisplayStyle.None;
            sheet.Add(kitLifeNote);
            sheet.Add(acts);
        }

        // ---------------- #423: the life slot (placed-life spec section 6) ----------------
        GlassButton kitLife;
        VisualElement kitLifeRow, kitDot;
        AquariumCiv.Sim.Eco.LifeSpecies heldLife;
        readonly System.Collections.Generic.List<AquariumCiv.Sim.Eco.LifeSpecies> readyJars = new();
        // #570: the shut jars stand in the chooser greyed, each with what it waits for; a tap on one says it (kitLifeNote), never nothing
        readonly System.Collections.Generic.List<AquariumCiv.Sim.Eco.LifeSpecies> lockedJars = new();
        Label kitLifeNote;
        AquariumCiv.Sim.Eco.LifeSpecies? lockedPick;
        string lifeRowKey = "";
        bool lifeShown;
        public const string LifeWaterHint = "Tap the water to choose a spot. Two fingers move, turn and zoom the view."; // fb4 #542
        public const string LifeHint = "Tap the glowing ground to choose a spot. Two fingers move, turn and zoom the view.";
        /// <summary>#643 (playtest): the hint as a group is taken names it first, so the player knows what is in hand (the chooser's pick).</summary>
        public static string LifeHintFor(AquariumCiv.Sim.Eco.LifeSpecies s) => s switch
        {
            AquariumCiv.Sim.Eco.LifeSpecies.Ducks => "The ducks: " + Lower(LifeWaterHint),
            AquariumCiv.Sim.Eco.LifeSpecies.Fish => "The fish: " + Lower(LifeWaterHint),
            AquariumCiv.Sim.Eco.LifeSpecies.Thicket => "The bushes: " + Lower(LifeHint),
            AquariumCiv.Sim.Eco.LifeSpecies.Rabbits => "The rabbits: " + Lower(LifeHint),
            AquariumCiv.Sim.Eco.LifeSpecies.Deer => "The deer: " + Lower(LifeHint),
            _ => "The wolves: tap a glowing den spot in the wood. Two fingers move, turn and zoom the view.",
        };
        static string Lower(string h) => char.ToLowerInvariant(h[0]) + h.Substring(1);
        /// <summary>The species in hand while <see cref="Hand"/> is <see cref="KitTool.Life"/>.</summary>
        public AquariumCiv.Sim.Eco.LifeSpecies HeldLife => heldLife;
        /// <summary>How many times the ready wiggle played (tests).</summary>
        public int LifeWiggles { get; private set; }
        /// <summary>The chooser row is open (two or more ready jars; tests).</summary>
        public bool LifeRowOpen => kitLifeRow != null && kitLifeRow.ClassListContains("kit-life-row--open");
        /// <summary>The chooser opens in the note's place (the kit sheet keeps its height).</summary>
        void SetLifeRow(bool open)
        {
            kitLifeRow.EnableInClassList("kit-life-row--open", open);
            if (kitNote != null) kitNote.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            if (kitLifeNote != null) kitLifeNote.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            // the other tools step aside too: the sheet keeps its height, so on a short phone the icons would run into the title
            var others = open ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (var b in new[] { kitCloth, kitMister, kitTweezers }) if (b != null) b.style.display = others;
        }
        /// <summary>#570 the chooser's note: what the picked shut jar waits for, then what the village waits for (tests).</summary>
        public string LifeNoteText => kitLifeNote != null ? kitLifeNote.text : "";
        /// <summary>#570 the shut jar the note speaks of (tests); null when none is shut.</summary>
        public AquariumCiv.Sim.Eco.LifeSpecies? LockedPick => lockedPick;
        /// <summary>The slot shows in the kit (a jar is ready).</summary>
        public bool LifeSlotShown => lifeShown;
        /// <summary>The Kit button carries the small dot (a jar is ready and nothing is in hand).</summary>
        public bool LifeDotShown => cleanButton != null && cleanButton.ClassListContains("kg-tool--life");

        /// <summary>Takes the species in hand (the life tool): its ground glows (LifePlaceView), the next taps place it.</summary>
        public void TakeLife(AquariumCiv.Sim.Eco.LifeSpecies s)
        {
            heldLife = s;
            if (hand == KitTool.Life) { HandChanged?.Invoke(hand); return; }
            SetHand(KitTool.Life);
        }

        void OnLifeSlot()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (hand == KitTool.Life || hand == KitTool.Seed) { SetHand(KitTool.None); return; } // tapping the slot puts it back
            if (sim == null) return;
            sim.ReadyJars(readyJars);
            sim.LockedJars(lockedJars);
            int choices = readyJars.Count + (kitSeedShown ? 1 : 0);
            if (choices + lockedJars.Count == 0) return;
            // #570: with shut jars to show the chooser opens even for one choice (or none: the list of what is missing).
            // #643 (playtest, the user 2026-11-13): always the chooser, a single ready jar too: the player picks the group, never finds it in hand.
            bool open = !LifeRowOpen;
            if (open) FillLifeRow();
            SetLifeRow(open);
        }

        void FillLifeRow()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim != null) sim.LockedJars(lockedJars); else lockedJars.Clear();
            if (lockedPick.HasValue && !lockedJars.Contains(lockedPick.Value)) lockedPick = null;
            if (!lockedPick.HasValue && lockedJars.Count > 0) lockedPick = lockedJars[0];
            string key = string.Join(",", readyJars) + (kitSeedShown ? ",seed" : "") + "|" + string.Join(",", lockedJars) + "|" + lockedPick;
            if (key != lifeRowKey)
            {
                lifeRowKey = key;
                kitLifeRow.Clear();
                foreach (var sp in readyJars)
                {
                    var s = sp;
                    var b = new GlassButton("", () => { SetLifeRow(false); TakeLife(s); }, ButtonKind.Secondary, UiRole.Toggle) { name = "kit-life-" + s.ToString().ToLowerInvariant() };
                    b.AddToClassList("kit-life-choice");
                    b.Add(new GlassIcon(LifeIcon(s)));
                    kitLifeRow.Add(b);
                }
                if (kitSeedShown)
                {
                    // #558: the seed is a choice in the jar beside the species (the tree icon: it becomes the one tree)
                    var b = new GlassButton("", () => { SetLifeRow(false); SetHand(KitTool.Seed); }, ButtonKind.Secondary, UiRole.Toggle) { name = "kit-life-seed" };
                    b.AddToClassList("kit-life-choice");
                    b.Add(new GlassIcon("tree"));
                    kitLifeRow.Add(b);
                }
                // #570: the shut jars, greyed, after the ready ones; a tap picks one and its need shows in the note
                foreach (var sp in lockedJars)
                {
                    var s = sp;
                    var b = new GlassButton("", () => { lockedPick = s; }, ButtonKind.Secondary, UiRole.Toggle) { name = "kit-life-locked-" + s.ToString().ToLowerInvariant() };
                    b.AddToClassList("kit-life-choice");
                    b.AddToClassList("kit-life-choice--locked");
                    b.EnableInClassList("kit-life-choice--picked", lockedPick == s);
                    b.Add(new GlassIcon(LifeIcon(s)));
                    kitLifeRow.Add(b);
                }
            }
            // the note follows the sim every time (a count moves: "more trees (2/5)")
            string need = lockedPick.HasValue && sim != null ? sim.JarNeed(lockedPick.Value) ?? PlainReady(lockedPick.Value) : "";
            string waits = sim != null ? sim.VillageWaitsLine : "";
            string note = need.Length > 0 && waits.Length > 0 ? need + "\n" + waits : need + waits;
            if (kitLifeNote != null && kitLifeNote.text != note) kitLifeNote.text = note;
        }

        /// <summary>A shut jar whose land is ready already opens at the season's end.</summary>
        static string PlainReady(AquariumCiv.Sim.Eco.LifeSpecies s) => AquariumCiv.Sim.Eco.EcoModel.PlainName(s) + " are nearly here";

        /// <summary>The icon of each species in the chooser (Icons/life-*.svg, drawn for this project).</summary>
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

        /// <summary>Each frame: the slot shows while a jar is ready, the Kit button keeps a small dot until it is placed, and a jar
        /// that opened (here or while away) wiggles the Kit button once, the next time the HUD is up to see it.</summary>
        VisualElement photoButton;

        /// <summary>#647 (playtest: a first run played with no UI at all): photo mode hides every prompt, and only a short tap brings them
        /// back, which misting (a held drag) never is. While the first run is on, the Photo button steps out and photo mode ends.</summary>
        void GuardFirstRun(AquariumCiv.Sim.SimCore sim)
        {
            bool first = sim.State.firstRun.active;
            var show = first ? DisplayStyle.None : DisplayStyle.Flex;
            if (photoButton != null && photoButton.style.display != show) photoButton.style.display = show;
            if (first && cameraMode) SetCameraMode(false);
        }

        void FollowLife(AquariumCiv.Sim.SimCore sim)
        {
            bool any = sim.AnyJarReady;
            sim.LockedJars(lockedJars);
            bool shut = lockedJars.Count > 0 && !sim.State.firstRun.active; // #570: the slot also lists what is still to come
            if (any != lifeShown)
            {
                lifeShown = any;
                if (!any && !kitSeedShown && !shut) SetLifeRow(false);
            }
            var show = any || kitSeedShown || shut || hand == KitTool.Life || hand == KitTool.Seed ? DisplayStyle.Flex : DisplayStyle.None;
            if (kitLife.style.display != show) kitLife.style.display = show;
            kitLife.EnableInClassList("kit-life--ready", any);
            cleanButton?.EnableInClassList("kg-tool--life", any && hand != KitTool.Life);
            if (hand == KitTool.Life && !sim.JarReady(heldLife)) SetHand(KitTool.None); // placed (or gone): the tool is put down
            if (LifeRowOpen) { sim.ReadyJars(readyJars); FillLifeRow(); if (readyJars.Count == 0 && !kitSeedShown && !shut) SetLifeRow(false); }
            if (!any || hidden || cameraMode || toolsWaiting || toolBlocks.Count > 0 || sim.State.firstRun.active) return;
            bool due = false;
            foreach (var s in AquariumCiv.Sim.Eco.LifeJar.All) if (sim.AckJarReady(s)) due = true;
            if (!due) return;
            LifeWiggles++;
            UiSound.Play(UiRole.NewTool);
            Wiggle(cleanButton);
            if (OpenSheet == Kit) Wiggle(kitLife);
        }

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
            if (!eco || kitLife == null) return;
            bool seed = sim.CanPlantTree;
            bool seedChanged = seed != kitSeedShown;
            kitSeedShown = seed;
            FollowLife(sim);
            if (seedChanged)
            {
                if (seed && !sim.State.firstRun.active) { SetShapeNew(true); Toast("A seed has come. It's in the life jar, in your kit.", 4f); }
                if (!seed && hand == KitTool.Seed) SetHand(KitTool.None);
            }
            if (kitNote != null)
                kitNote.text = (seed ? "Cloth wipes the glass. Mister wets the land. Tweezers pluck what overgrows. The life jar holds the seed: plant the one tree."
                    : sim.EcoTree.cell >= 0 ? "Cloth wipes the glass. Mister wets the land and the tree. Tweezers pluck what overgrows."
                    : "Cloth wipes the glass. Mister wets the land. Tweezers pluck what overgrows. A seed comes when the land can hold a tree.")
                    + (sim.VillageWaitsLine is { Length: > 0 } waits ? "\n" + waits : ""); // #570
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

        /// <summary>#604: the title's ink eases between dark and light with the daylight; no pill behind it.</summary>
        void FollowInk()
        {
            if (nameLabel == null) return;
            float target = inkLightness < 0f ? HudInk.Target(HudInk.Daylight) : HudInk.Target(HudInk.Daylight, inkLightness);
            float next = inkLightness < 0f ? target : HudInk.Step(inkLightness, target, Time.unscaledDeltaTime);
            float halo = HudInk.Step(inkHalo, offHome ? 1f : 0f, Time.unscaledDeltaTime);
            if (Mathf.Approximately(next, inkLightness) && Mathf.Approximately(halo, inkHalo)) return;
            inkLightness = next; inkHalo = halo;
            var ink = HudInk.Ink(next);
            // fb7 pass: off home the dark ink can sit on the glass rails and the room: a soft pale halo then (the light ink keeps its dark shadow)
            var shadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 6f, color = HudInk.Shadow(next, halo) };
            foreach (var l in new[] { nameLabel, yearLabel }) { l.style.color = ink; l.style.textShadow = shadow; }
        }

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
            // The name chip opens the offices list (#215); the settings mark to its right opens Settings.
            var title = new GlassButton("", () => Open(Offices), ButtonKind.Quiet, UiRole.Open) { name = "hud-title" };
            title.AddToClassList("hud-title");
            var words = new VisualElement { name = "hud-title-text", pickingMode = PickingMode.Ignore };
            words.AddToClassList("hud-title-text");
            words.Add(nameLabel = Text("Your box", "hud-title-name", "type-box"));
            words.Add(yearLabel = Text("Year 1", "hud-title-year", "type-sub"));
            nameLabel.pickingMode = yearLabel.pickingMode = PickingMode.Ignore;
            title.Add(words);
            topRow.Add(title);
            topRow.Add(IconButton("hud-settings", "settings", () => Open(Settings), UiRole.Open));
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("hud-spacer");
            topRow.Add(spacer);
            var corner = new VisualElement { name = "hud-corner", pickingMode = PickingMode.Ignore };
            corner.AddToClassList("hud-corner");
            if (FeedbackUI.InTestBuild) corner.Add(IconButton("hud-feedback", "feedback", OpenFeedback, UiRole.Tap)); // #650: full ink like its neighbours (the quiet fade read as disabled)
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
            bottomRow.Add(historyButton = Tool("hud-history", "History", OnHistory, UiRole.Open));
            bottomRow.Add(cleanButton = Tool("hud-clean", "Clean", () => { if (kitBox) OnKitButton(); else SetCleanMode(!cleanMode); }, UiRole.Toggle));
            cleanButton.Add(toolMark = new GlassIcon("check", "kg-icon--small", "kg-tool-mark"));
            cleanButton.Add(new GlassNewTag());
            kitDot = new VisualElement { name = "hud-kit-dot", pickingMode = PickingMode.Ignore }; // #423: a jar is ready
            kitDot.AddToClassList("kg-dot");
            kitDot.AddToClassList("kg-tool-dot");
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
            if (id == Kit) { SetHand(KitTool.None); if (kitLifeRow != null) SetLifeRow(false); } // the kit opens on its tools, the chooser shut
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
