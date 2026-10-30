using AquariumCiv.Audio;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// The first run's view (#94, phase 2; docs/design/first-session-and-check-in.md §2): drives the pure
    /// <see cref="FirstRun"/> director in a brand-new box (no save) or resumes it from the save. Wipe and the overgrown patch
    /// use the care layer (clean mode is switched on for the player); the plate is a brass pill that opens the naming sheet;
    /// the rain is the ordinary shape mode (the director adopts it); "Their first year" is the 15 s live skip with the clock
    /// held; then the first card. Gesture hints are an animated ghost finger with at most three words. It never blocks:
    /// everything else in the game keeps working, every step has Skip, and an untouched plate keeps its suggested name.
    /// UI Toolkit built in code (styled by Game.uss, the #36 sheets); every control has a stable <c>firstrun-*</c> name.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public class FirstRunView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static FirstRunView Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static FirstRunView Create() => new GameObject("FirstRunView").AddComponent<FirstRunView>();

        // ---------------- state ----------------
        FirstRun run;
        SimCore boundSim;
        GameHud hud;
        BoxView box;
        UIDocument doc;
        VisualElement root, safe, ghost, ghostRing, nameScrim, cardScrim, skipRow, yearBox, plate;
        Button rainBtn;
        Label hintLabel, bubble, yearBar, nameMsg, cardPlace, cardCounts, cardBody, cardAlso, cardCaption;
        VisualElement yearFill;
        TextField nameField;
        Button showMeBtn;
        FirstYearSummary summary;
        bool heldClock, toolsHidden, cardShown, showingMe, rainArmed;
        float hintShownAt = -1f;
        string lastHintId = "";
        bool prevHintVisible;
        FirstRunStep shownStep = (FirstRunStep)(-1);

        public FirstRun Run => run;
        public bool Active => run != null && run.Active;
        public FirstRunStep Step => run != null ? run.Step : FirstRunStep.Done;
        public VisualElement Root => root;
        public string ShownHintId => root != null && ghost != null && ghost.resolvedStyle.display == DisplayStyle.Flex ? lastHintId : "";
        public string ShownHintText => ShownHintId.Length > 0 ? hintLabel.text : "";
        public bool NameSheetOpen => nameScrim != null && nameScrim.resolvedStyle.display == DisplayStyle.Flex;
        public bool CardOpen => cardScrim != null && cardScrim.resolvedStyle.display == DisplayStyle.Flex;
        public string NameMessage => nameMsg?.text ?? "";
        public FirstYearSummary Summary => summary;

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;
        Camera Cam => Camera.main;

        // ================================================================== lifecycle
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            if (Instance != this) return;
            doc = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 12; // over the HUD and the shaping labels, under the moment player
            Build(doc.rootVisualElement);
            UiInput.Add(IsOverUi);
        }

        void OnDisable()
        {
            if (Instance != this) return;
            UiInput.Remove(IsOverUi);
            Release();
        }

        void OnDestroy()
        {
            if (Instance == this) { Release(); Instance = null; }
        }

        void Release()
        {
            if (heldClock && GameClock.Instance != null) GameClock.Instance.Held = false;
            heldClock = false;
            if (toolsHidden && GameHud.Instance != null) GameHud.Instance.HideTools("firstrun", false);
            toolsHidden = false;
            MessProps.PatchCentre = null;
        }

        bool IsOverUi(Vector2 p) => root != null && GameUiPanel.Over(root, p);

        // ================================================================== binding
        void Bind()
        {
            var clock = GameClock.Instance;
            var sim = Sim;
            if (sim == null || clock == null) return;
            if (sim == boundSim && run != null) return;
            if (run != null) Unhook();
            boundSim = sim;
            var f = sim.State.firstRun;
            bool never = !f.active && f.step == 0 && f.playSeconds <= 0 && !f.skipped;
            if (f.active) run = FirstRun.Resume(sim);
            else if (never && clock.Session != null && clock.Session.newWorld) run = FirstRun.Begin(sim);
            else run = null;
            if (run != null) Hook();
        }

        void Hook()
        {
            run.StepChanged += OnStep;
            run.Cue += OnCue;
            shownStep = (FirstRunStep)(-1);
            var (px, pz) = run.PatchSpot;
            MessProps.PatchCentre = new Vector2(px, pz);
            FindAnyObjectByType<MessProps>()?.ResetSpots();
            SetFieldFromRun();
        }

        void Unhook()
        {
            run.StepChanged -= OnStep;
            run.Cue -= OnCue;
        }

        // ---------------- debug (the sim debug panel) ----------------
        /// <summary>Debug: start the first run again on this box.</summary>
        public static string DebugReplay()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;
            if (sim == null) return "no box";
            var v = Instance != null ? Instance : Create();
            if (v.run != null) v.Unhook();
            v.CloseSheets();
            v.run = FirstRun.Replay(sim);
            v.boundSim = sim;
            v.Hook();
            GameClock.Instance.Save();
            return "first run restarted";
        }

        /// <summary>Debug: fast-forward to a step (doing the steps before it).</summary>
        public static string DebugSkipTo(FirstRunStep step)
        {
            var v = Instance;
            if (v == null || v.run == null || !v.run.Active) return "no first run active (replay it first)";
            v.run.DebugSkipTo(step);
            GameClock.Instance.Save();
            return "first run at " + v.run.Step;
        }

        public static string DebugSkipAll()
        {
            var v = Instance;
            if (v == null || v.run == null || !v.run.Active) return "no first run active";
            v.run.SkipAll();
            GameClock.Instance.Save();
            return "first run skipped";
        }

        public static string DebugStatus()
        {
            var v = Instance;
            if (v == null || v.run == null) return "no first run";
            var f = GameClock.Instance.ActiveSim.State.firstRun;
            return $"first run {(v.run.Active ? "active" : "over")} · step {v.run.Step} · {f.stepSeconds:0}s in step · hint returns {f.hintReturns}";
        }

        // ================================================================== frame
        void Update()
        {
            Bind();
            if (hud == null) hud = GameHud.Instance;
            if (box == null) box = FindAnyObjectByType<BoxView>();
            if (run == null || !run.Active || hud == null || box == null)
            {
                if (run != null && !run.Active) EndRun();
                HideAll();
                return;
            }
            var clock = GameClock.Instance;
            if (clock.Visiting) { HideAll(); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool year = run.Step == FirstRunStep.Year;
            if (year != heldClock) { heldClock = year; clock.Held = year; }
            SetToolsHidden(year || run.Step == FirstRunStep.Card);
            if (year) run.AdvanceYear(dt);
            ReadPointer(out bool touching);
            run.Tick(dt, touching && !year);
            if (!run.Active) { EndRun(); HideAll(); return; }
            if (run.Step != shownStep) EnterStep();
            UpdateUi();
        }

        void SetToolsHidden(bool hide)
        {
            if (hide == toolsHidden) return;
            toolsHidden = hide;
            hud.HideTools("firstrun", hide);
            if (hide) { hud.CloseSheet(); ShapingView.Instance?.SetTool(null); }
        }

        void EndRun()
        {
            if (heldClock && GameClock.Instance != null) GameClock.Instance.Held = false;
            heldClock = false;
            SetToolsHidden(false);
            MessProps.PatchCentre = null;
            CloseSheets();
            if (ShapingView.Instance != null) { ShapingView.Instance.SetTool(null); ShapingView.Instance.Free = true; }
            if (cardShown) { cardShown = false; if (ShowMeDirector.Instance != null) ShowMeDirector.Instance.End(); }
        }

        void OnStep(FirstRunStep s)
        {
            if (GameClock.Instance != null) GameClock.Instance.Save(); // a kill resumes at this step
            rainArmed = false;
            if (s != FirstRunStep.Rain && ShapingView.Instance != null) { ShapingView.Instance.SetTool(null); ShapingView.Instance.Free = true; }
            if (s != FirstRunStep.Name) { if (nameScrim != null) nameScrim.style.display = DisplayStyle.None; }
            if (box != null && s == FirstRunStep.Year) box.Refresh();
        }

        void OnCue(FirstRunCue c)
        {
            // The child's smiley in the mist is a view staging beat that is not built yet (see the issue's open questions);
            // for now the line says what the child does.
            if (c == FirstRunCue.ChildSmiley && hud != null) { hud.Toast("A child draws a smiley in the mist.", 3.5f); Sfx.Play(CueLibrary.Discovery); }
        }

        void EnterStep()
        {
            shownStep = run.Step;
            prevHintVisible = false;
            cardShown = false;
            switch (shownStep)
            {
                case FirstRunStep.Wipe:
                case FirstRunStep.Patch:
                    if (!hud.CleanMode && !hud.SheetOpen) hud.SetCleanMode(true); // one finger wipes and flicks
                    break;
                case FirstRunStep.Name:
                    hud.SetCleanMode(false);
                    hud.Toast("The glass is clear. Give the box a name.", 4f);
                    break;
                case FirstRunStep.Rain:
                    hud.SetCleanMode(false);
                    break;
                case FirstRunStep.Year:
                    hud.SetCleanMode(false);
                    CloseSheets();
                    break;
                case FirstRunStep.Card:
                    OpenCard();
                    break;
            }
        }

        // ================================================================== input
        static void ReadPointer(out bool down)
        {
            down = false;
            var ts = Touchscreen.current;
            if (ts != null) foreach (var t in ts.touches) if (t.press.isPressed) { down = true; return; }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) down = true;
        }

        // ================================================================== UI
        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "firstrun-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            parent.Add(root);
            safe = new VisualElement { name = "firstrun-safe", pickingMode = PickingMode.Ignore };
            safe.AddToClassList("hud-fill");
            root.Add(safe);

            // the brass plate: tap to name the box
            plate = Btn("firstrun-plate", "Name your box", OpenName);
            plate.style.position = Position.Absolute;
            plate.style.top = 190;
            plate.style.alignSelf = Align.Center;
            plate.style.width = 560; plate.style.height = 132;
            plate.style.backgroundColor = new Color(0.81f, 0.65f, 0.37f);
            plate.style.color = new Color(0.18f, 0.16f, 0.15f);
            plate.style.fontSize = 40; plate.style.unityFontStyleAndWeight = FontStyle.Bold;
            plate.style.borderTopWidth = plate.style.borderBottomWidth = plate.style.borderLeftWidth = plate.style.borderRightWidth = 6;
            var rim = new Color(0.42f, 0.29f, 0.21f);
            plate.style.borderTopColor = plate.style.borderBottomColor = plate.style.borderLeftColor = plate.style.borderRightColor = rim;
            plate.style.borderTopLeftRadius = plate.style.borderTopRightRadius = plate.style.borderBottomLeftRadius = plate.style.borderBottomRightRadius = 30;
            plate.style.display = DisplayStyle.None;
            safe.Add(plate);

            // the rain button, in the thumb zone
            rainBtn = Btn("firstrun-rain", "Give them rain", OnRainButton, "hud-btn", "hud-btn--primary");
            rainBtn.style.position = Position.Absolute;
            rainBtn.style.bottom = 330;
            rainBtn.style.alignSelf = Align.Center;
            rainBtn.style.width = 560;
            rainBtn.style.display = DisplayStyle.None;
            safe.Add(rainBtn);

            // the wish bubble over the wisher
            bubble = new Label { name = "firstrun-bubble", pickingMode = PickingMode.Ignore };
            bubble.AddToClassList("hud-toast");
            bubble.style.position = Position.Absolute;
            bubble.style.right = StyleKeyword.Auto; bubble.style.bottom = StyleKeyword.Auto;
            bubble.style.fontSize = 34; bubble.style.maxWidth = 640;
            bubble.style.unityTextAlign = TextAnchor.MiddleCenter;
            bubble.style.backgroundColor = new Color(0.94f, 0.89f, 0.8f, 0.96f);
            bubble.style.color = new Color(0.42f, 0.29f, 0.21f);
            bubble.style.display = DisplayStyle.None;
            safe.Add(bubble);

            // skip row: small, quiet, never in the way
            skipRow = new VisualElement { name = "firstrun-skip-row", pickingMode = PickingMode.Ignore };
            skipRow.style.position = Position.Absolute;
            skipRow.style.left = 24; skipRow.style.top = 190;
            skipRow.Add(SmallBtn("firstrun-skip-step", "Skip step", () => { run?.SkipStep(); }));
            var all = SmallBtn("firstrun-skip-all", "Skip all", () => { run?.SkipAll(); });
            all.style.marginTop = 12;
            skipRow.Add(all);
            safe.Add(skipRow);

            // "Their first year": a caption and a thin bar
            yearBox = new VisualElement { name = "firstrun-year", pickingMode = PickingMode.Ignore };
            yearBox.style.position = Position.Absolute;
            yearBox.style.left = 0; yearBox.style.right = 0; yearBox.style.top = 330;
            yearBox.style.alignItems = Align.Center;
            var cap = new Label("Their first year") { name = "firstrun-year-caption", pickingMode = PickingMode.Ignore };
            cap.style.fontSize = 72; cap.style.unityFontStyleAndWeight = FontStyle.Bold;
            cap.style.color = new Color(0.94f, 0.89f, 0.8f);
            cap.style.backgroundColor = new Color(0.18f, 0.16f, 0.15f, 0.62f);
            cap.style.paddingLeft = cap.style.paddingRight = 56; cap.style.paddingTop = cap.style.paddingBottom = 24;
            cap.style.borderTopLeftRadius = cap.style.borderTopRightRadius = cap.style.borderBottomLeftRadius = cap.style.borderBottomRightRadius = 60;
            yearBar = cap;
            yearBox.Add(cap);
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.width = 520; track.style.height = 14; track.style.marginTop = 24;
            track.style.backgroundColor = new Color(0.18f, 0.16f, 0.15f, 0.5f);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 7;
            yearFill = new VisualElement { name = "firstrun-year-fill", pickingMode = PickingMode.Ignore };
            yearFill.style.height = 14; yearFill.style.width = 0;
            yearFill.style.backgroundColor = new Color(0.81f, 0.65f, 0.37f);
            yearFill.style.borderTopLeftRadius = yearFill.style.borderBottomLeftRadius = 7;
            track.Add(yearFill);
            yearBox.Add(track);
            yearBox.style.display = DisplayStyle.None;
            safe.Add(yearBox);

            // the ghost finger and its words
            ghost = new VisualElement { name = "firstrun-ghost", pickingMode = PickingMode.Ignore };
            ghost.style.position = Position.Absolute;
            ghost.style.width = 96; ghost.style.height = 96;
            ghost.style.backgroundColor = new Color(1f, 1f, 1f, 0.55f);
            ghost.style.borderTopLeftRadius = ghost.style.borderTopRightRadius = ghost.style.borderBottomLeftRadius = ghost.style.borderBottomRightRadius = 48;
            ghost.style.borderTopWidth = ghost.style.borderBottomWidth = ghost.style.borderLeftWidth = ghost.style.borderRightWidth = 5;
            ghost.style.borderTopColor = ghost.style.borderBottomColor = ghost.style.borderLeftColor = ghost.style.borderRightColor = new Color(0.42f, 0.29f, 0.21f, 0.8f);
            ghost.style.display = DisplayStyle.None;
            ghostRing = new VisualElement { pickingMode = PickingMode.Ignore };
            ghostRing.style.position = Position.Absolute;
            ghostRing.style.left = -26; ghostRing.style.top = -26; ghostRing.style.right = -26; ghostRing.style.bottom = -26;
            ghostRing.style.borderTopWidth = ghostRing.style.borderBottomWidth = ghostRing.style.borderLeftWidth = ghostRing.style.borderRightWidth = 4;
            ghostRing.style.borderTopColor = ghostRing.style.borderBottomColor = ghostRing.style.borderLeftColor = ghostRing.style.borderRightColor = new Color(1f, 1f, 1f, 0.5f);
            ghostRing.style.borderTopLeftRadius = ghostRing.style.borderTopRightRadius = ghostRing.style.borderBottomLeftRadius = ghostRing.style.borderBottomRightRadius = 74;
            ghost.Add(ghostRing);
            safe.Add(ghost);
            hintLabel = new Label { name = "firstrun-hint", pickingMode = PickingMode.Ignore };
            hintLabel.AddToClassList("hud-toast");
            hintLabel.style.position = Position.Absolute;
            hintLabel.style.right = StyleKeyword.Auto; hintLabel.style.bottom = StyleKeyword.Auto;
            hintLabel.style.fontSize = 42; hintLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            hintLabel.style.display = DisplayStyle.None;
            safe.Add(hintLabel);

            BuildNameSheet();
            BuildCard();
            ApplySafe();
        }

        void ApplySafe()
        {
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            safe.style.left = i.x; safe.style.top = i.y; safe.style.right = i.z; safe.style.bottom = i.w;
        }

        VisualElement MakeSheet(string name, out VisualElement scrim)
        {
            scrim = new VisualElement { name = name + "-scrim" };
            scrim.AddToClassList("hud-fill");
            scrim.AddToClassList("sheet-scrim");
            scrim.style.display = DisplayStyle.None;
            root.Add(scrim);
            var sheet = new VisualElement { name = name + "-sheet" };
            sheet.AddToClassList("sheet");
            scrim.Add(sheet);
            return sheet;
        }

        void BuildNameSheet()
        {
            var sheet = MakeSheet("firstrun-name", out nameScrim);
            var scrim = nameScrim;
            scrim.RegisterCallback<PointerUpEvent>(e => { if (e.target == scrim) scrim.style.display = DisplayStyle.None; });
            var head = new VisualElement();
            head.AddToClassList("sheet-head");
            head.Add(Lbl("Name your box", "sheet-title"));
            head.Add(Btn("firstrun-name-close", "Later", () => scrim.style.display = DisplayStyle.None, "sheet-close"));
            sheet.Add(head);
            sheet.Add(Lbl("It goes on the brass plate.", "sheet-note"));
            nameField = new TextField { name = "firstrun-name-field", maxLength = SettlementNaming.MaxLength + 4 };
            nameField.style.marginTop = 24; nameField.style.height = 136;
            nameField.RegisterValueChangedCallback(e => { if (nameMsg != null) nameMsg.text = ""; });
            nameField.RegisterCallback<GeometryChangedEvent>(_ => StyleInput());
            sheet.Add(nameField);
            nameMsg = Lbl("", "sheet-note");
            nameMsg.name = "firstrun-name-msg";
            nameMsg.style.color = new Color(0.65f, 0.3f, 0.2f);
            nameMsg.style.minHeight = 44;
            sheet.Add(nameMsg);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 12 } };
            var again = Btn("firstrun-name-reroll", "Another", () => { if (run != null) { nameField.value = run.RerollName(); nameMsg.text = ""; } }, "sheet-btn");
            again.style.flexGrow = 1; again.style.marginRight = 24;
            var keep = Btn("firstrun-name-keep", "Keep", OnKeep, "sheet-btn");
            keep.style.flexGrow = 1.4f;
            keep.style.backgroundColor = new Color(0.78f, 0.44f, 0.31f);
            keep.style.color = new Color(0.94f, 0.89f, 0.8f);
            row.Add(again); row.Add(keep);
            sheet.Add(row);
        }

        void StyleInput()
        {
            var input = nameField.Q(className: "unity-text-field__input") ?? nameField.Q(className: "unity-base-text-field__input");
            if (input == null) return;
            input.style.fontSize = 52; input.style.height = 136;
            input.style.color = new Color(0.42f, 0.29f, 0.21f);
            input.style.backgroundColor = new Color(0.98f, 0.95f, 0.88f);
            input.style.borderTopLeftRadius = input.style.borderTopRightRadius = input.style.borderBottomLeftRadius = input.style.borderBottomRightRadius = 40;
            input.style.paddingLeft = input.style.paddingRight = 36;
            input.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        void BuildCard()
        {
            var sheet = MakeSheet("firstrun-card", out cardScrim);
            cardCaption = Lbl("Their first year", "pc-label");
            sheet.Add(cardCaption);
            cardPlace = Lbl("", "sheet-title");
            cardPlace.style.marginBottom = 8;
            sheet.Add(cardPlace);
            cardCounts = Lbl("", "sheet-note");
            cardCounts.style.marginBottom = 24;
            sheet.Add(cardCounts);
            cardBody = Lbl("", "sheet-text");
            cardBody.name = "firstrun-card-text";
            cardBody.style.fontSize = 44;
            cardBody.style.whiteSpace = WhiteSpace.Normal; cardBody.style.flexShrink = 1; cardBody.style.maxWidth = Length.Percent(100);
            sheet.Add(cardBody);
            cardAlso = Lbl("", "sheet-note");
            cardAlso.name = "firstrun-card-also";
            cardAlso.style.whiteSpace = WhiteSpace.Normal; cardAlso.style.maxWidth = Length.Percent(100);
            sheet.Add(cardAlso);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 12 } };
            showMeBtn = Btn("firstrun-card-showme", "Show me", OnShowMe, "sheet-btn");
            showMeBtn.style.flexGrow = 1; showMeBtn.style.marginRight = 24;
            var done = Btn("firstrun-card-done", "Done", OnDone, "sheet-btn");
            done.style.flexGrow = 1;
            done.style.backgroundColor = new Color(0.78f, 0.44f, 0.31f);
            done.style.color = new Color(0.94f, 0.89f, 0.8f);
            row.Add(showMeBtn); row.Add(done);
            sheet.Add(row);
        }

        // ---------------- the plate and the name ----------------
        void SetFieldFromRun() { if (nameField != null && run != null) nameField.value = run.SuggestedName; }

        void OpenName()
        {
            if (run == null || !run.Active) return;
            hud?.CloseSheet();
            if (string.IsNullOrEmpty(nameField.value)) nameField.value = run.SuggestedName;
            nameMsg.text = "";
            if (nameScrim.style.display != DisplayStyle.Flex) Sfx.Play(CueLibrary.UiOpen); // #96
            nameScrim.style.display = DisplayStyle.Flex;
        }

        void OnKeep()
        {
            if (run == null) return;
            var p = run.CheckName(nameField.value, out string clean);
            if (p != NameProblem.None) { nameMsg.text = SettlementNaming.Message(p); return; }
            var problem = run.KeepName(clean);
            if (problem != NameProblem.None) { nameMsg.text = SettlementNaming.Message(problem); return; }
            nameScrim.style.display = DisplayStyle.None;
            Sfx.Play(CueLibrary.UiConfirm); // #96: the name is kept
            hud?.Toast($"Welcome to {clean}.", 3.5f);
            GameClock.Instance?.Save();
        }

        // ---------------- the rain ----------------
        void OnRainButton()
        {
            if (run == null || run.Step != FirstRunStep.Rain) return;
            var sv = ShapingView.Instance;
            if (sv == null) { var (x, z) = run.SuggestedRainSpot; run.PlaceRain(x, z); return; }
            rainArmed = !rainArmed;
            Sfx.Play(CueLibrary.UiToggle); // #96
            if (rainArmed)
            {
                sv.Free = false;
                sv.SetTool(SimCore.NudgeKind.Rain);
                run.NoteTrayOpened();
            }
            else sv.SetTool(null);
        }

        // ---------------- the card ----------------
        void OpenCard()
        {
            summary = run.Summary();
            cardPlace.text = summary.place;
            int dp = summary.populationEnd - summary.populationStart, db = summary.buildingsEnd - summary.buildingsStart;
            var sb = new StringBuilder();
            sb.Append(Plural(summary.populationEnd, "person", "people")).Append(dp > 0 ? $" (+{dp})" : "");
            if (db > 0) sb.Append("  ·  ").Append(Plural(db, "new building", "new buildings"));
            cardCounts.text = sb.ToString();
            cardBody.text = summary.cardText;
            cardAlso.text = summary.alsoHeadlines.Count > 0 ? "Also: " + string.Join("  ·  ", summary.alsoHeadlines) : "";
            cardAlso.style.display = summary.alsoHeadlines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            showMeBtn.style.display = summary.card != null && summary.flyToBuildingId >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            cardScrim.style.display = DisplayStyle.Flex;
            Sfx.Play(CueLibrary.CardArrive); // #96: "Their first year" arrives
            cardShown = true; showingMe = false;
        }

        static string Plural(int n, string one, string many) => n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);

        void OnShowMe()
        {
            var d = ShowMeDirector.Instance;
            if (d == null || summary?.card == null) return;
            d.Show(summary.card, true);
            cardScrim.style.display = DisplayStyle.None;
            showingMe = true;
        }

        void OnDone()
        {
            if (run == null) return;
            ShowMeDirector.Instance?.End();
            cardScrim.style.display = DisplayStyle.None;
            Sfx.Play(CueLibrary.CardDismiss); // #96
            showingMe = false;
            run.AcknowledgeCard();
            GameClock.Instance?.Save();
        }

        void CloseSheets()
        {
            if (nameScrim != null) nameScrim.style.display = DisplayStyle.None;
            if (cardScrim != null) cardScrim.style.display = DisplayStyle.None;
        }

        // ================================================================== per frame UI
        void HideAll()
        {
            if (root == null) return;
            foreach (var e in new VisualElement[] { plate, rainBtn, bubble, skipRow, yearBox, ghost, hintLabel }) if (e != null) e.style.display = DisplayStyle.None;
            if (shownStep != (FirstRunStep)(-1) && (run == null || !run.Active)) { shownStep = (FirstRunStep)(-1); }
        }

        void UpdateUi()
        {
            var step = run.Step;
            bool quiet = hud.CameraMode || !hud.IsVisible || (hud.SheetOpen && !NameSheetOpen);
            ApplySafe();
            // The card comes back after a Show me.
            if (showingMe)
            {
                var d = ShowMeDirector.Instance;
                if (d == null || !d.Busy) { showingMe = false; if (step == FirstRunStep.Card) cardScrim.style.display = DisplayStyle.Flex; }
            }
            plate.style.display = !quiet && step == FirstRunStep.Name && !NameSheetOpen ? DisplayStyle.Flex : DisplayStyle.None;
            rainBtn.style.display = !quiet && step == FirstRunStep.Rain ? DisplayStyle.Flex : DisplayStyle.None;
            if (rainBtn.resolvedStyle.display == DisplayStyle.Flex) rainBtn.text = rainArmed ? "Tap the ground" : "Give them rain";
            skipRow.style.display = !quiet && step != FirstRunStep.Card && step != FirstRunStep.Year && step != FirstRunStep.Arrive && !NameSheetOpen ? DisplayStyle.Flex : DisplayStyle.None;
            yearBox.style.display = !quiet && step == FirstRunStep.Year ? DisplayStyle.Flex : DisplayStyle.None;
            if (step == FirstRunStep.Year) yearFill.style.width = 520f * run.YearProgress;
            UpdateBubble(step, quiet);
            UpdateHint(quiet || NameSheetOpen);
        }

        void UpdateBubble(FirstRunStep step, bool quiet)
        {
            var w = step == FirstRunStep.Rain ? run.RainWish : null;
            bool show = !quiet && w != null && w.state == WishState.Open;
            if (show)
            {
                var pv = PeopleView.Find();
                var sim = Sim;
                var n = sim?.FindNotable(w.notableId);
                if (pv != null && n != null && pv.TryGetDollTop(PersonRef.Notable(w.notableId), out var top) && ToPanel(top, out var p))
                {
                    bubble.text = $"{n.name} hopes for rain.";
                    bubble.style.left = p.x; bubble.style.top = p.y - 24;
                    bubble.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-100, LengthUnit.Percent));
                }
                else show = false;
            }
            bubble.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        bool ToPanel(Vector3 world, out Vector2 panelPos)
        {
            panelPos = default;
            var c = Cam;
            if (c == null || root.panel == null) return false;
            var s = c.WorldToScreenPoint(world);
            if (s.z <= 0f) return false;
            panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(s.x, Screen.height - s.y));
            return true;
        }

        Vector3 BoxPoint(float x, float z, float up) => box.transform.TransformPoint(new Vector3(x, box.GroundHeight(x, z) + up, z));

        // The ghost finger: where it points and how it moves, by the hint's gesture and anchor.
        void UpdateHint(bool suppress)
        {
            var h = run.Hint;
            bool visible = h.visible && !suppress;
            if (visible && !prevHintVisible)
            {
                hintShownAt = Time.unscaledTime;
                // The finger is about to show a wipe or a flick: make sure one finger does that.
                if ((h.id == "wipe" || h.id == "patch") && !hud.CleanMode && !hud.SheetOpen) hud.SetCleanMode(true);
            }
            prevHintVisible = visible;
            if (!visible)
            {
                ghost.style.display = DisplayStyle.None;
                hintLabel.style.display = DisplayStyle.None;
                return;
            }
            lastHintId = h.id;
            if (!Anchor(h, out var at, out var labelBelow)) { ghost.style.display = DisplayStyle.None; hintLabel.style.display = DisplayStyle.None; return; }
            float t = Time.unscaledTime - hintShownAt;
            float k = Mathf.Repeat(t, 2.2f) / 2.2f;
            float fade = Mathf.Clamp01(t / 0.35f);
            float W = GameUiPanel.ReferenceWidth;
            Vector2 off = default;
            float scale = 1f, alpha = 1f;
            switch (h.gesture)
            {
                case HintGesture.Wipe:
                    off = new Vector2(Mathf.Sin(k * Mathf.PI * 2f) * W * 0.2f, Mathf.Sin(k * Mathf.PI * 4f) * 26f);
                    break;
                case HintGesture.Flick:
                {
                    float m = Mathf.Clamp01(k / 0.45f);
                    off = new Vector2(m * m * W * 0.17f, -m * m * W * 0.1f);
                    alpha = 1f - Mathf.Clamp01((k - 0.35f) / 0.15f);
                    if (k > 0.5f) alpha = 0f;
                    break;
                }
                case HintGesture.Rub:
                    off = new Vector2(Mathf.Cos(k * Mathf.PI * 4f), Mathf.Sin(k * Mathf.PI * 4f)) * 40f;
                    break;
                default: // tap: press, release, ring
                {
                    float press = Mathf.Clamp01(k / 0.2f) * (1f - Mathf.Clamp01((k - 0.35f) / 0.15f));
                    scale = 1f - 0.18f * press;
                    ghostRing.style.opacity = Mathf.Clamp01((k - 0.35f) / 0.1f) * (1f - Mathf.Clamp01((k - 0.45f) / 0.5f));
                    break;
                }
            }
            if (h.gesture != HintGesture.Tap) ghostRing.style.opacity = 0f;
            ghost.style.display = DisplayStyle.Flex;
            ghost.style.left = at.x + off.x - 48; ghost.style.top = at.y + off.y - 48;
            ghost.style.scale = new Scale(new Vector3(scale, scale, 1f));
            ghost.style.opacity = fade * alpha;
            hintLabel.text = h.text;
            hintLabel.style.display = DisplayStyle.Flex;
            hintLabel.style.opacity = fade;
            hintLabel.style.left = at.x;
            hintLabel.style.top = at.y + (labelBelow ? 96f : -90f);
            hintLabel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(labelBelow ? 0 : -100, LengthUnit.Percent));
        }

        bool Anchor(FirstRunHint h, out Vector2 at, out bool labelBelow)
        {
            at = default; labelBelow = true;
            switch (h.anchor)
            {
                case HintAnchor.Glass:
                    labelBelow = false;
                    return ToPanel(box.transform.TransformPoint(new Vector3(0f, 1.5f, Land.HZ)), out at);
                case HintAnchor.Patch:
                {
                    var (x, z) = run.PatchSpot;
                    return ToPanel(BoxPoint(x, z, 0.15f), out at);
                }
                case HintAnchor.RainSpot:
                {
                    var (x, z) = run.SuggestedRainSpot;
                    return ToPanel(BoxPoint(x, z, 0.05f), out at);
                }
                case HintAnchor.Plate: return Centre(plate, out at);
                case HintAnchor.ShapingButton: labelBelow = false; return Centre(rainBtn, out at);
            }
            return false;
        }

        bool Centre(VisualElement e, out Vector2 at)
        {
            at = default;
            if (e.resolvedStyle.display == DisplayStyle.None || float.IsNaN(e.worldBound.center.x)) return false;
            var wb = root.WorldToLocal(e.worldBound.center);
            at = wb;
            return true;
        }

        // ---------------- element helpers ----------------
        static Label Lbl(string s, string cls) { var l = new Label(s); l.AddToClassList(cls); return l; }

        static Button Btn(string name, string text, System.Action click, params string[] classes)
        {
            var b = new Button(click) { name = name, text = text };
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        static Button SmallBtn(string name, string text, System.Action click)
        {
            var b = Btn(name, text, click, "hud-btn", "hud-btn--small");
            b.style.height = 100; b.style.fontSize = 28;
            return b;
        }
    }
}
