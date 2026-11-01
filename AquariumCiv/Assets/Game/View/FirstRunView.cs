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
    /// held; then the first card. Gesture hints are an animated ghost finger with at most three words. It never blocks: the
    /// step's own gesture is always there, test builds have Skip, and an untouched plate keeps its suggested name.
    /// Keeper's Glass (#169, board 3.7): one primary per step and no other tools, so the HUD's tools and top row wait for Done
    /// (<see cref="GameHud"/> reads the run's state), and clean mode holds through the wipe and the flick
    /// (<see cref="GameHud.CleanLocked"/>). One message at a time: the hint rests while a reward line shows.
    /// Built in code from the components in UI/Glass, styled in Glass.uss; the code sets only what depends on the world or the
    /// clock (where a hint or a bubble sits, the ghost's motion, the year's bar, the keyboard's height). Every control has a stable
    /// <c>firstrun-*</c> name.
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
        VisualElement root, safe, ghost, ghostRing, skipRow, yearFill;
        GlassScrim cardScrim;
        GlassSheet cardSheet;
        GlassNameSheet nameSheet;
        Button plate;
        GlassPlate plateFace;
        GlassBigPrompt rainBtn;
        GlassBubble bubble;
        GlassHint hint;
        GlassCaption yearBox;
        GlassCard card;
        GlassBanner showBar;
        Label cardAlso;
        GlassButton showMeBtn;
        FirstYearSummary summary;
        bool heldClock, toolsHidden, cardShown, showingMe, rainArmed, runEnded, yearUp;
        float plateNamedUntil, rewardUntil;
        string rewardLine;
        float hintShownAt = -1f;
        string lastHintId = "";
        bool prevHintVisible;
        FirstRunStep shownStep = (FirstRunStep)(-1);

        public FirstRun Run => run;
        public bool Active => run != null && run.Active;
        public FirstRunStep Step => run != null ? run.Step : FirstRunStep.Done;
        public VisualElement Root => root;
        public string ShownHintId => root != null && ghost != null && ghost.resolvedStyle.display == DisplayStyle.Flex ? lastHintId : "";
        public string ShownHintText => ShownHintId.Length > 0 ? hint.Word : "";
        public bool NameSheetOpen => nameSheet != null && nameSheet.IsOpen;
        public bool CardOpen => cardSheet != null && cardSheet.IsOpen;
        /// <summary>One bubble at a time: the run's own, the "Their first year" caption or the first card, and the people wait.</summary>
        public bool HasTheWord => Active && (CardOpen || (bubble != null && bubble.resolvedStyle.display == DisplayStyle.Flex) || Step == FirstRunStep.Rain || Step == FirstRunStep.Year);
        /// <summary>The reward line showing now (the hint rests under it), or "".</summary>
        public string ShownReward => bubble != null && Time.unscaledTime < rewardUntil ? rewardLine : "";
        public string NameMessage => nameSheet?.Problem ?? "";
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
            if (voice != null) GameUiPanel.Bubbles.Done(voice);
            if (heldClock && GameClock.Instance != null) GameClock.Instance.Held = false;
            heldClock = false;
            if (toolsHidden && GameHud.Instance != null) GameHud.Instance.HideTools("firstrun", false);
            toolsHidden = false;
            if (GameHud.Instance != null) GameHud.Instance.CleanLocked = false;
            SetSoftDays(false);
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
            else if (never && clock.Session != null && (clock.Session.newWorld || (sim.State.boxIndex > 0 && sim.State.nudges.Count == 0 && !clock.Visiting))) run = FirstRun.Begin(sim); // a new box too: its short run (#198)
            else run = null;
            if (run != null) Hook();
        }

        void Hook()
        {
            runEnded = false;
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

        /// <summary>Debug: holds the naming sheet as it rides the keyboard, which an Editor never raises.</summary>
        public static bool DebugKeyboard { get => GlassKeyboard.Debug; set => GlassKeyboard.Debug = value; }

        /// <summary>Debug: the reward line that rests the hint.</summary>
        public static string DebugReward()
        {
            var v = Instance;
            if (v == null || v.run == null || !v.run.Active) return "no first run active";
            v.OnCue(FirstRunCue.ChildSmiley);
            return "reward line up";
        }

        /// <summary>Debug: the naming sheet in its refused state, with Keep unavailable until the name is edited.</summary>
        public static string DebugRefuseName()
        {
            var v = Instance;
            if (v == null || v.run == null || !v.run.Active) return "no first run active";
            v.OpenName();
            v.nameSheet.Field.SetValueWithoutNotify("shit hole");
            v.OnKeep();
            return "name refused: " + v.NameMessage;
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
            SetSoftDays(year);
            SetToolsHidden(year || run.Step == FirstRunStep.Card);
            if (year) run.AdvanceYear(dt);
            ReadPointer(out bool touching);
            run.Tick(dt, touching && !year);
            if (!run.Active) { EndRun(); HideAll(); return; }
            hud.CleanLocked = run.Step == FirstRunStep.Wipe || run.Step == FirstRunStep.Patch; // before EnterStep, which turns it off for the next step
            if (run.Step != shownStep) EnterStep();
            UpdateUi();
        }

        // "Their first year" runs a month every 1.25 s, far ahead of the view clock: the people walk to their new plans
        // instead of jumping there at each skip (tester batch 3).
        static void SetSoftDays(bool on)
        {
            var pv = PeopleView.Find();
            if (pv != null) pv.SoftDayTurns = on;
        }

        void SetToolsHidden(bool hide)
        {
            if (hide == toolsHidden) return;
            toolsHidden = hide;
            hud.HideTools("firstrun", hide);
            if (hide) { hud.CloseSheet(); ShapingView.Instance?.SetTool(null); }
        }

        /// <summary>Once per run: Update reaches here every frame after the run is over, and the shaping tool the player
        /// picks later must not be cleared (tester batch 3).</summary>
        void EndRun()
        {
            if (runEnded) return;
            runEnded = true;
            SetSoftDays(false);
            if (heldClock && GameClock.Instance != null) GameClock.Instance.Held = false;
            heldClock = false;
            SetToolsHidden(false);
            if (hud != null) hud.CleanLocked = false;
            MessProps.PatchCentre = null;
            CloseSheets();
            if (ShapingView.Instance != null) { ShapingView.Instance.SetTool(null); ShapingView.Instance.Free = true; }
            if (cardShown) { cardShown = false; if (ShowMeDirector.Instance != null) ShowMeDirector.Instance.End(); }
            var cam = FindAnyObjectByType<BoxCamera>();
            if (cam != null && cam.OffHome) cam.GoHome(ShowMeDirector.ReturnSeconds); // Done: the camera goes home and the HUD arrives (board 3.7)
        }

        void OnStep(FirstRunStep s)
        {
            GameClock.Tel.FirstRunStepStarted(s.ToString());
            if (GameClock.Instance != null) GameClock.Instance.Save(); // a kill resumes at this step
            rainArmed = false;
            if (s != FirstRunStep.Rain && ShapingView.Instance != null) { ShapingView.Instance.SetTool(null); ShapingView.Instance.Free = true; }
            if (s != FirstRunStep.Name) CloseName();
            if (box != null && s == FirstRunStep.Year) box.Refresh();
        }

        void OnCue(FirstRunCue c)
        {
            // The child's smiley in the mist is a view staging beat that is not built yet (see the issue's open questions);
            // for now the line says what the child does, in a bubble over the glass, and the hint rests under it.
            if (c != FirstRunCue.ChildSmiley) return;
            rewardLine = "A child draws a smiley in the mist.";
            rewardUntil = Time.unscaledTime + RewardSeconds;
            Sfx.Play(CueLibrary.Discovery);
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
                    hud.SetCleanMode(false); // the hint says it: one message, no toast (question 27)
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
        const float RewardSeconds = 3.5f, ToastSeconds = 1.6f, NamedPlateSeconds = 1.7f;
        /// <summary>Half the ghost, and where the skip row starts under the notch, panel px (96 pt).</summary>
        const float GhostHalf = 60.5f, SkipTop = 264f;
        /// <summary>The panel's top limit for a word over the world.</summary>
        const float TopLimit = 190f;
        /// <summary>The plate on the plinth's front face, box units.</summary>
        const float PlateY = -0.12f, PlateZ = -Land.HZ - 0.2f;

        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "firstrun-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            parent.Add(root);
            safe = new VisualElement { name = "firstrun-safe", pickingMode = PickingMode.Ignore };
            safe.AddToClassList("hud-fill");
            root.Add(safe);

            // the brass plate: tap to name the box. A clear slot takes the tap; what shows is the plate
            plate = new Button(OpenName) { name = "firstrun-plate" };
            plate.RemoveFromClassList(Button.ussClassName);
            plate.AddToClassList("fr-plate-slot");
            plateFace = new GlassPlate();
            plateFace.Title = "";
            plate.Add(plateFace);
            plate.style.display = DisplayStyle.None;
            safe.Add(plate);

            // the rain: the step's one primary, in the prompt lane
            rainBtn = new GlassBigPrompt("Give them rain", OnRainButton) { name = "firstrun-rain", Role = UiRole.Toggle };
            rainBtn.Ready = false;
            safe.Add(rainBtn);

            // one bubble: the wish over the wisher, or the reward line over the glass
            bubble = new GlassBubble("", "") { name = "firstrun-bubble" };
            bubble.AddToClassList("kg-bubble--world");
            bubble.style.display = DisplayStyle.None;
            safe.Add(bubble);

            // skip row: test builds only, the quietest thing on screen
            skipRow = new VisualElement { name = "firstrun-skip-row", pickingMode = PickingMode.Ignore };
            skipRow.AddToClassList("fr-skip");
            skipRow.Add(new GlassButton("Skip step", () => run?.SkipStep(), ButtonKind.Quiet) { name = "firstrun-skip-step" });
            skipRow.Add(new GlassButton("Skip all", () => run?.SkipAll(), ButtonKind.Quiet) { name = "firstrun-skip-all" });
            skipRow.style.display = DisplayStyle.None;
            safe.Add(skipRow);

            // "Their first year": the caption and a thin bar in it
            yearBox = new GlassCaption("Their first year") { name = "firstrun-year" };
            yearBox.AddToClassList("fr-year");
            yearBox.Line.name = "firstrun-year-caption";
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("fr-year-track");
            yearFill = new VisualElement { name = "firstrun-year-fill", pickingMode = PickingMode.Ignore };
            yearFill.AddToClassList("fr-year-fill");
            track.Add(yearFill);
            yearBox.Add(track);
            yearBox.style.display = DisplayStyle.None;
            safe.Add(yearBox);

            // the ghost finger and its words
            ghost = GlassHint.Ghost();
            ghost.name = "firstrun-ghost";
            ghost.AddToClassList("fr-ghost");
            ghost.style.display = DisplayStyle.None;
            ghostRing = new VisualElement { name = "firstrun-ghost-ring", pickingMode = PickingMode.Ignore };
            ghostRing.AddToClassList("fr-ghost-ring");
            ghost.Add(ghostRing);
            safe.Add(ghost);
            hint = new GlassHint { name = "firstrun-hint" };
            hint.AddToClassList("fr-hint");
            hint.style.display = DisplayStyle.None;
            safe.Add(hint);

            BuildNameSheet();
            BuildCard();
            ApplySafe();
        }

        void ApplySafe()
        {
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            safe.style.left = i.x; safe.style.top = i.y; safe.style.right = i.z; safe.style.bottom = i.w;
            float pad = GlassSheet.IsShort(Screen.width, Screen.height) ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad;
            cardSheet.SetInsets(i, pad);
            if (!nameSheet.Riding) nameSheet.Sheet.SetInsets(i, pad);
            rainBtn.style.bottom = GameUiPanel.Lane(GameUiPanel.PromptBottom, i.w);
            skipRow.style.top = Mathf.Max(0f, SkipTop - i.y);
            showBar.style.bottom = GameUiPanel.Lane(GameUiPanel.ToolsBottom, i.w);
        }

        void BuildNameSheet()
        {
            nameSheet = new GlassNameSheet("firstrun-name", "Name your box", "Later", CloseName, OnAnother, OnKeep);
            root.Add(nameSheet.Scrim);
        }

        void BuildCard()
        {
            cardSheet = new GlassSheet();
            cardScrim = new GlassScrim("firstrun-card", cardSheet);
            root.Add(cardScrim);
            card = new GlassCard();
            card.Dots.style.display = DisplayStyle.None;
            cardSheet.Add(card);
            // head, then the sub line, then the words: the year's numbers go with the name (board 3.7)
            card.Remove(card.Sub);
            card.Insert(card.IndexOf(card.Head) + 1, card.Sub);
            var scroll = new GlassScroll { SnapToRows = true, SplitRows = true };
            scroll.AddToClassList("fr-card-scroll");
            scroll.View.contentContainer.AddToClassList("fr-card-scroll-pad");
            card.Insert(card.IndexOf(card.Actions), scroll);
            card.Body.name = "firstrun-card-text";
            card.Body.AddToClassList(GlassScroll.RowClass);
            card.Body.userData = GlassType.BodyPitch;
            scroll.Add(card.Body);
            cardAlso = (Label)Text("firstrun-card-also", "type-note", "");
            cardAlso.enableRichText = true;
            cardAlso.AddToClassList("fr-card-also");
            cardAlso.AddToClassList(GlassScroll.RowClass);
            cardAlso.userData = NotePitch;
            scroll.Add(cardAlso);
            showMeBtn = new GlassButton("Show me", OnShowMe, ButtonKind.Secondary, UiRole.ShowMe) { name = "firstrun-card-showme" };
            card.Actions.Add(showMeBtn);
            card.Actions.Add(new GlassButton("Done", OnDone, ButtonKind.Primary, UiRole.None) { name = "firstrun-card-done" });

            // Show me leaves the land clear; the way back is fixed, in the tools' lane
            showBar = new GlassBanner { name = "firstrun-showme-bar", pickingMode = PickingMode.Ignore };
            showBar.style.display = DisplayStyle.None;
            showBar.End.Add(new GlassButton("Back to the story", BackToCard, ButtonKind.Secondary, UiRole.Back) { name = "firstrun-showme-back", Compact = true });
            safe.Add(showBar);
        }

        const float NotePitch = 13f * 1.3f * GlassType.Pt;

        static TextElement Text(string name, string cls, string words)
        {
            var l = new Label(words) { pickingMode = PickingMode.Ignore };
            if (name != null) l.name = name;
            l.AddToClassList(cls);
            return l;
        }

        // ---------------- the plate and the name ----------------
        void SetFieldFromRun() { if (nameSheet != null && run != null) nameSheet.Field.value = run.SuggestedName; }

        void OpenName()
        {
            if (run == null || !run.Active) return;
            hud?.CloseSheet();
            if (string.IsNullOrEmpty(nameSheet.Field.value)) nameSheet.Field.value = run.SuggestedName;
            nameSheet.Say("");
            if (!nameSheet.IsOpen) { UiSound.Play(UiRole.Open); nameSheet.Scrim.Open(); }
            hud?.SetOutsideSheet(nameSheet.Sheet);
        }

        void CloseName()
        {
            if (nameSheet == null || !nameSheet.IsOpen) return;
            var (i, pad) = NameInsets();
            nameSheet.Land(i, pad);
            nameSheet.Scrim.Close();
            if (hud != null && hud.OutsideSheet == nameSheet.Sheet) hud.SetOutsideSheet(null);
        }

        static (Vector4, float) NameInsets() => (GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height),
            GlassSheet.IsShort(Screen.width, Screen.height) ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);

        /// <summary>Only a corner of the box stays in view over the keyboard, which question 33 accepts.</summary>
        void RideKeyboard()
        {
            var (i, pad) = NameInsets();
            nameSheet.Ride(i, pad);
        }

        void OnAnother()
        {
            if (run != null) nameSheet.Field.value = run.RerollName();
        }

        void OnKeep()
        {
            if (run == null) return;
            var p = run.CheckName(nameSheet.Field.value, out string clean);
            if (p != NameProblem.None) { nameSheet.Refuse(p); return; }
            var problem = run.KeepName(clean);
            if (problem != NameProblem.None) { nameSheet.Refuse(problem); return; }
            CloseName();
            UiSound.Play(UiRole.Confirm);
            plateFace.Title = clean;
            plateNamedUntil = Time.unscaledTime + NamedPlateSeconds;
            hud?.Toast($"Welcome to {clean}.", ToastSeconds);
            GameClock.Instance?.Save();
        }

        // ---------------- the rain ----------------
        void OnRainButton()
        {
            if (run == null || run.Step != FirstRunStep.Rain) return;
            var sv = ShapingView.Instance;
            if (sv == null) { var (x, z) = run.SuggestedRainSpot; run.PlaceRain(x, z); return; }
            rainArmed = true;
            sv.Free = false;
            sv.SetTool(SimCore.NudgeKind.Rain, true);
            run.NoteTrayOpened();
        }

        // ---------------- the card ----------------
        void OpenCard()
        {
            summary = run.Summary();
            int dp = summary.populationEnd - summary.populationStart, db = summary.buildingsEnd - summary.buildingsStart;
            var sb = new StringBuilder(summary.caption).Append(" · ").Append(Plural(summary.populationEnd, "person", "people"));
            if (dp > 0) sb.Append(" (+").Append(dp.ToString(CultureInfo.InvariantCulture)).Append(')');
            if (db > 0) sb.Append(" · ").Append(Plural(db, "new building", "new buildings"));
            card.Set(summary.place, summary.cardText, sb.ToString());
            bool also = summary.alsoHeadlines.Count > 0;
            cardAlso.text = also ? GlassType.Lines("Also: " + string.Join(" · ", summary.alsoHeadlines), 13f, 1.3f) : "";
            cardAlso.style.display = also ? DisplayStyle.Flex : DisplayStyle.None;
            showMeBtn.style.display = summary.card != null && summary.flyToBuildingId >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            cardScrim.Open();
            Sfx.Play(CueLibrary.CardArrive); // #96: "Their first year" arrives
            cardShown = true; showingMe = false;
        }

        static string Plural(int n, string one, string many) => n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);

        void OnShowMe()
        {
            var d = ShowMeDirector.Instance;
            if (d == null || summary?.card == null) return;
            d.Show(summary.card, true);
            cardScrim.Close();
            showBar.Set(summary.caption, summary.place);
            GlassMotion.Show(showBar, GlassMotion.SlideOff);
            showingMe = true;
        }

        /// <summary>The land was looked at: the card comes back, at a tap or when the echo is over.</summary>
        void BackToCard()
        {
            if (!showingMe) return;
            showingMe = false;
            GlassMotion.Hide(showBar, GlassMotion.SlideOff, GlassMotion.SlideMs);
            if (run != null && run.Step == FirstRunStep.Card) cardScrim.Open();
        }

        void OnDone()
        {
            if (run == null) return;
            ShowMeDirector.Instance?.End();
            cardScrim.Close();
            Sfx.Play(CueLibrary.CardDismiss); // #96
            showingMe = false;
            run.AcknowledgeCard();
            GameClock.Instance?.Save();
        }

        void CloseSheets()
        {
            CloseName();
            if (cardSheet != null && cardSheet.IsOpen) cardScrim.Close();
            if (showBar != null) showBar.style.display = DisplayStyle.None;
            showingMe = false;
        }

        // ================================================================== per frame UI
        void HideAll()
        {
            if (root == null) return;
            foreach (var e in new VisualElement[] { plate, bubble, skipRow, ghost, hint }) if (e != null) e.style.display = DisplayStyle.None;
            if (voice != null) GameUiPanel.Bubbles.Done(voice);
            rainBtn.Ready = false;
            Fade(yearBox, ref yearUp, false, GlassMotion.CaptionOff, GlassMotion.CaptionOutMs);
            if (shownStep != (FirstRunStep)(-1) && (run == null || !run.Active)) { shownStep = (FirstRunStep)(-1); }
        }

        static void Fade(VisualElement e, ref bool up, bool on, string off, int ms)
        {
            if (on == up) return;
            up = on;
            if (on) GlassMotion.Show(e, off);
            else GlassMotion.Hide(e, off, ms);
        }

        void UpdateUi()
        {
            var step = run.Step;
            bool quiet = hud.CameraMode || !hud.IsVisible || (hud.SheetOpen && !NameSheetOpen);
            ApplySafe();
            RideKeyboard();
            // The card comes back after a Show me.
            if (showingMe)
            {
                var d = ShowMeDirector.Instance;
                if (d == null || d.Settled) BackToCard();
            }
            bool named = Time.unscaledTime < plateNamedUntil;
            bool plateUp = !quiet && (step == FirstRunStep.Name && !NameSheetOpen || named);
            plate.style.display = plateUp ? DisplayStyle.Flex : DisplayStyle.None;
            if (plateUp) PlacePlate();
            plate.pickingMode = named ? PickingMode.Ignore : PickingMode.Position;
            // The prompt waits for the welcome: it would take the toast down (question 22), and the name deserves its moment.
            bool rainUp = !quiet && step == FirstRunStep.Rain && !rainArmed && !named;
            if (rainBtn.Ready != rainUp) rainBtn.Ready = rainUp;
            bool skips = FeedbackUI.InTestBuild && !quiet && step != FirstRunStep.Card && step != FirstRunStep.Year && step != FirstRunStep.Arrive && !NameSheetOpen;
            skipRow.style.display = skips ? DisplayStyle.Flex : DisplayStyle.None;
            Fade(yearBox, ref yearUp, !quiet && step == FirstRunStep.Year, GlassMotion.CaptionOff, GlassMotion.CaptionOutMs);
            if (step == FirstRunStep.Year) yearFill.style.width = Length.Percent(100f * run.YearProgress);
            bool reward = !quiet && Time.unscaledTime < rewardUntil;
            UpdateBubble(step, quiet, reward);
            UpdateHint(quiet || NameSheetOpen || reward);
        }

        /// <summary>The plate sits on the plinth, under the glass, where it stays in the world.</summary>
        void PlacePlate()
        {
            if (!ToPanel(box.transform.TransformPoint(new Vector3(0f, PlateY, PlateZ)), out var p)) return;
            float w = plate.resolvedStyle.width, h = plate.resolvedStyle.height;
            var q = GameUiPanel.KeepOnScreen(plate, new Vector2(p.x - (float.IsNaN(w) ? 0f : w) * 0.5f, p.y - (float.IsNaN(h) ? 0f : h) * 0.5f), TopLimit);
            plate.style.left = q.x; plate.style.top = q.y;
        }

        void UpdateBubble(FirstRunStep step, bool quiet, bool reward)
        {
            bool show = false;
            if (reward)
            {
                show = ToPanel(box.transform.TransformPoint(new Vector3(0f, 1.5f, Land.HZ)), out var p);
                if (show) { bubble.Set("", rewardLine); PlaceBubble(p); }
            }
            else
            {
                var w = step == FirstRunStep.Rain ? run.RainWish : null;
                if (!quiet && w != null && w.state == WishState.Open)
                {
                    var pv = PeopleView.Find();
                    var n = Sim?.FindNotable(w.notableId);
                    if (pv != null && n != null && pv.TryGetDollTop(PersonRef.Notable(w.notableId), out var top) && ToPanel(top, out var p))
                    {
                        bubble.Set("", $"{n.name} hopes for rain.");
                        PlaceBubble(p);
                        show = true;
                    }
                }
            }
            voice ??= GameUiPanel.Bubbles.Join(BubbleHost.Rank.Line);
            if (show) show = GameUiPanel.Bubbles.Ask(voice); // asked every frame, so it waits for whoever has the word
            else GameUiPanel.Bubbles.Done(voice);
            bubble.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        BubbleHost.Voice voice;

        void PlaceBubble(Vector2 p)
        {
            PlaceOver(bubble, p, 24f);
            bubble.TailTo(p - new Vector2(bubble.style.left.value.value, bubble.style.top.value.value)); // #204: on them, even pushed off by the edge
        }

        /// <summary>Centres a label over a panel point, on screen whatever its size (one frame late on new text).</summary>
        static void PlaceOver(VisualElement e, Vector2 p, float gap)
        {
            float w = e.resolvedStyle.width, h = e.resolvedStyle.height;
            var q = GameUiPanel.KeepOnScreen(e, new Vector2(p.x - (float.IsNaN(w) ? 0f : w) * 0.5f, p.y - gap - (float.IsNaN(h) ? 0f : h)), TopLimit);
            e.style.left = q.x; e.style.top = q.y;
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
            if (visible && !prevHintVisible) hintShownAt = Time.unscaledTime;
            prevHintVisible = visible;
            if (!visible)
            {
                ghost.style.display = DisplayStyle.None;
                hint.style.display = DisplayStyle.None;
                return;
            }
            lastHintId = h.id;
            if (!Anchor(h, out var at, out var labelBelow)) { ghost.style.display = DisplayStyle.None; hint.style.display = DisplayStyle.None; return; }
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
            // The finger taps the rain button's middle (tester run 7), and the button says the words; on the blank plate it sits in the middle too.
            bool onRain = h.anchor == HintAnchor.ShapingButton;
            ghost.style.left = at.x + off.x - GhostHalf; ghost.style.top = at.y + off.y - GhostHalf;
            ghost.style.scale = new Scale(new Vector3(scale, scale, 1f));
            ghost.style.opacity = fade * alpha;
            hint.Word = h.text;
            hint.style.display = onRain ? DisplayStyle.None : DisplayStyle.Flex;
            hint.style.opacity = fade;
            var hl = hint.resolvedStyle;
            float hw = float.IsNaN(hl.width) ? 0f : hl.width, hh = float.IsNaN(hl.height) ? 0f : hl.height;
            var spot = GameUiPanel.KeepOnScreen(hint, new Vector2(at.x - hw * 0.5f, labelBelow ? at.y + 96f : at.y - 90f - hh), TopLimit);
            hint.style.left = spot.x; hint.style.top = spot.y;
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

    }
}