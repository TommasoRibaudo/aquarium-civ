using System.Collections.Generic;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// The chronicle cards on screen (#75, Keeper's Glass in #166; chronicle-and-history §2, §7, spec §4): the peek in its own lane and
    /// one <see cref="GlassSheet"/> at 30 %, holding a <see cref="GlassCard"/>. All the logic is <see cref="CardStackModel"/>; this file only
    /// draws <see cref="CardStackModel.Face"/> and passes touches on. Order in the row: Back, Next, Show me, then the one primary at the
    /// right (Give, Watch, Remember; Done on Also…). A story has no filled button, its headline leads. Long cards scroll inside the sheet
    /// in whole lines. During Show me the sheet slides off so the place stays clear, and a bar in the tools' lane brings it back: at a tap,
    /// or when the echo is over (board S33Return). Stable names for tests and the tester agent: <c>cards-peek</c>, <c>cards-sheet</c>,
    /// <c>cards-next</c>, <c>cards-back</c>, <c>cards-skip</c>, <c>cards-showme</c>, <c>cards-action</c>, <c>cards-portrait-0/1</c>,
    /// <c>cards-also-0/1/2</c>, <c>cards-showme-bar</c>, <c>cards-showme-back</c>.
    /// </summary>
    public class CardStackView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static CardStackView Instance { get; private set; }

        const string ToolBlock = "cards", ToastHold = "peek";
        /// <summary>A drag at least this long (panel units) is a swipe, not a tap.</summary>
        const float SwipeMin = 90f;
        /// <summary>Line pitches (pt size, ratio): spec §1.</summary>
        const float TitleRatio = 1.15f, QuoteRatio = 1.3f, QuotePt = 14f, NotePt = 13f, TitlePt = 21f;

        UIDocument doc;
        VisualElement root, head, ports, eraMark, alsoBox, yearSide;
        GlassPeek peek;
        GlassSheet sheet;
        GlassBanner showBar;
        GlassCard card;
        GlassScroll scroll;
        GlassPortrait peekPortrait;
        GlassIcon peekStar;
        Label peekAsk, peekText, peekTeaser, teaser, headline, body, endLine, quote, note, footer, alsoTitle;
        GlassButton nextBtn, backBtn, skipBtn, showMeBtn, actionBtn;
        readonly GlassButton[] portraits = new GlassButton[2];
        readonly GlassPortrait[] faces = new GlassPortrait[2];
        readonly int[] portraitIds = { -1, -1 };
        readonly Button[] alsoLines = new Button[3];
        // The dilemma card (#223): two columns, each an answer button (face + label) over the hint in the people's voice.
        VisualElement dilBox;
        GlassButton dilLet;                 // #368: "Let them decide" under the two options (cards-dil-let)
        readonly VisualElement[] dilCols = new VisualElement[2];
        readonly GlassButton[] dilAnswers = new GlassButton[2];
        readonly GlassPortrait[] dilFaces = new GlassPortrait[2];
        readonly GlassIcon[] dilPlaces = new GlassIcon[2];
        readonly Label[] dilLabels = new Label[2], dilHints = new Label[2];
        GameClock clock;
        GameHud hud;
        SimCore bound;
        CardStackModel model;
        Rect lastSafe;
        Vector2Int lastScreen;
        bool personOpen, watchingFlight, pressedLast, toolsHidden, peekUp, tight, showingPlace, barUp, choiceUp;
        Vector2 swipeFrom;
        int swipePointer = -1;
        bool swipeInScroll;
        string debugFace;
        System.Action<CardFace> debugEdit;
        // #238: the open dilemma last seen, the card of one raised live still waiting to be peeked, the month the dot was worked out at.
        int liveSeen = -1, livePending = -1;
        long dotMonth = -1;

        public CardStackModel Model => model;
        public VisualElement Root => root;
        /// <summary>#224: the choice scene is on screen for this card (the sheet is down, the bar is up): its touches answer.</summary>
        public bool ShowingChoice => choiceUp && showingPlace;
        /// <summary>The choice scene holds the bar up while it waits or reacts (#224).</summary>
        bool ChoiceHolds => choiceUp && ChoiceSceneView.Instance != null && ChoiceSceneView.Instance.Holding;
        /// <summary>The bar's second line while the people wait on a touch (#224).</summary>
        public const string ChoiceNote = "Touch one.";
        /// <summary>The peek or the stack is up: prompts over the world wait (MomentPlayer.PromptsBlocked).</summary>
        public bool Showing => model != null && model.Mode != CardStackMode.Hidden;
        /// <summary>The stack itself (not the peek) is open over the box: nothing is staged under it (visit-loop 2.4).</summary>
        public bool StackOpen => model != null && model.Mode == CardStackMode.Open;

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            new GameObject("CardStackView").AddComponent<CardStackView>();
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 12; // over the HUD, as the person card
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
            if (hud != null && (hud.OutsideSheet == sheet || hud.OutsideSheet == showBar)) hud.SetOutsideSheet(null);
            SetPeekUp(false);
            Unhook();
            SetToolsHidden(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnApplicationPause(bool pause) { if (pause) model?.OnPause(); }

        bool IsOverUi(Vector2 p) => GameUiPanel.Over(root, p);

        // ================================================================== the world around it

        void Unhook()
        {
            if (clock != null) clock.CaughtUp -= OnCaughtUp;
            clock = null;
            if (hud != null) { hud.HistoryTapped = null; hud.StoriesTapped = null; }
            hud = null;
        }

        void Hook()
        {
            if (clock != GameClock.Instance)
            {
                if (clock != null) clock.CaughtUp -= OnCaughtUp;
                clock = GameClock.Instance;
                if (clock != null) clock.CaughtUp += OnCaughtUp;
            }
            if (hud != GameHud.Instance)
            {
                hud = GameHud.Instance;
                if (hud != null) { hud.HistoryTapped = OnHistory; hud.StoriesTapped = OnStories; }
            }
        }

        void OnCaughtUp(CatchUpResult r) => model?.Prepare(r.realHours);

        /// <summary>History is always the time-lapse (hierarchy D1; its Close is the way out). False (nothing to replay yet, a moment is
        /// playing, or the first run: first-session §2 keeps the time-lapse out of it): the HUD opens its History sheet.</summary>
        bool OnHistory()
        {
            if (bound != null && bound.State.firstRun.active) return false;
            return TimeLapseScreenView.Ensure().Open();
        }

        /// <summary>The Stories chip: what's waiting after a peek was swiped away.</summary>
        void OnStories() => model?.Reopen();

        void Bind(SimCore sim)
        {
            if (model != null) { model.Changed -= Draw; model.ShowMeEnd -= OnShowMeEnd; }
            bound = sim;
            model = null;
            liveSeen = sim?.OpenDilemma?.id ?? -1; // one open already was raised before this session: the return tells it
            livePending = -1;
            dotMonth = -1;
            if (sim != null)
            {
                model = new CardStackModel(sim, () => Time.realtimeSinceStartupAsDouble, GameClock.Tel) { DilemmaTel = GameClock.DilemmaTrackerFor(sim) };
                model.Changed += Draw;
                model.ShowMeEnd += OnShowMeEnd;
                model.DilemmaShowMeHook = v => DilemmaShowMeHook != null && DilemmaShowMeHook(v);
                var session = clock != null ? clock.Session : null;
                if (session != null && !session.newWorld && session.sim == sim && session.awayHours > 0) model.Prepare(session.awayHours);
            }
            Draw();
        }

        static void OnShowMeEnd() => ShowMeDirector.Instance?.End(true);

        void SetToolsHidden(bool on)
        {
            if (toolsHidden == on) return;
            toolsHidden = on;
            GameHud.Instance?.HideTools(ToolBlock, on);
        }

        /// <summary>The peek on screen: it holds the pin's lane, and a toast would land on it (rule 3).</summary>
        void SetPeekUp(bool on)
        {
            if (peekUp == on) return;
            peekUp = on;
            GameHud.Instance?.SetPeekUp(on);
            GameUiPanel.Toasts.Hold(ToastHold, on);
        }

        /// <summary>Something else holds the screen: the cards stay as they are, out of sight.</summary>
        bool Covered()
        {
            if (hud != null && (hud.CameraMode || hud.SheetOpen || !hud.IsVisible)) return true;
            if (clock != null && clock.Visiting) return true;
            var person = PersonCardView.Instance;
            if (person != null && (person.AnyOpen || person.BuildingOpen)) return true; // #241: a building card too, or the cards open under it
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return true;
            var mp = MomentPlayer.Instance;
            return mp != null && mp.Playing;
        }

        void Update()
        {
            if (root == null) return;
            Hook();
            var sim = Sim;
            if (sim != bound) Bind(sim);
            if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y) ApplySafeArea();
            bool dot = model != null && model.HistoryDot;
            if (hud != null) hud.SetHistoryDot(dot && model.Mode == CardStackMode.Hidden); // spec §4: after a swipe, never beside the peek
            bool covered = model == null || Covered();
            bool open = model != null && model.Mode == CardStackMode.Open, peeking = model != null && model.Mode == CardStackMode.Peek;
            if (showingPlace && (!open || (!ChoiceHolds && (ShowMeDirector.Instance == null || ShowMeDirector.Instance.Settled)))) showingPlace = false;
            if (!showingPlace) choiceUp = false;
            ShowSheet(open && !covered && !showingPlace);
            ShowBar(open && !covered && showingPlace);
            ShowPeek(peeking && !covered);
            if (model == null) return;
            WatchLive();
            SetToolsHidden(open);
            SetPeekUp(peeking && !covered);
            peek.EnableInClassList("kg-peek--choice", peeking && model.PeekIsChoice); // t3-choice: a choice waits on the Keeper
            if (hud != null)
            {
                bool mine = open && !covered;
                VisualElement up = showingPlace ? showBar : sheet;
                if (mine && hud.OutsideSheet != up) hud.SetOutsideSheet(up);
                else if (!mine && (hud.OutsideSheet == sheet || hud.OutsideSheet == showBar)) hud.SetOutsideSheet(null);
            }

            if (personOpen && (PersonCardView.Instance == null || !PersonCardView.Instance.IsOpen)) { personOpen = false; model.PersonClosed(); }
            if (watchingFlight)
            {
                var sm = ShowMeDirector.Instance;
                if (sm == null || sm.State != ShowMeDirector.Phase.Flying) { watchingFlight = false; model.ShowMeLanded(); }
            }
            bool pressed = Pressed(out var at);
            if (peeking && !covered && pressed && !pressedLast && !GameUiPanel.Over(root, at)) model.HidePeek(); // the first touch elsewhere
            pressedLast = pressed;
            if (open && !covered && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) model.Close();
        }

        /// <summary>#238: with dilemmas on, a dilemma the people raise while the player watches brings the peek up with it on top, as a
        /// return does, once nothing else holds the screen; and the history dot follows the unread stories month by month. With dilemmas
        /// off nothing here runs (variant A stays as it was).</summary>
        void WatchLive()
        {
            if (bound == null || !bound.Config.dilemmas) return;
            if (bound.State.month != dotMonth) { dotMonth = bound.State.month; model.RefreshDot(); }
            var open = bound.OpenDilemma;
            int id = open?.id ?? -1;
            if (id != liveSeen) { liveSeen = id; livePending = open != null ? open.entryId : -1; }
            if (livePending < 0) return;
            var d = bound.DilemmaOfEntry(livePending);
            // Gone, already on the peek, or open in the stack. t3-choice (tester run 2 D): a peek up with an older story switches to it.
            if (d == null || d.state != DilemmaState.Open || model.PeekIs(livePending) || model.Mode == CardStackMode.Open && model.Holds(livePending)) { livePending = -1; return; }
            if (model.Mode == CardStackMode.Open || !LiveFree()) return; // wait until the other thing closes
            if (model.ShowLive(livePending)) livePending = -1;
        }

        /// <summary>t3-choice: the choice scene staged in the box let go without an answer: the open choice comes back on the peek (when the
        /// screen is free), so it stays one touch away.</summary>
        public void PeekOpenChoice()
        {
            var open = bound?.OpenDilemma;
            if (open != null && model != null && !model.PeekIs(open.entryId)) { liveSeen = open.id; livePending = open.entryId; }
        }

        /// <summary>Nothing else holds the screen: no sheet or card, no moment, Show me, choice scene, evening talk or clean mode. Our own
        /// peek doesn't count (t3-choice: a new choice takes its place).</summary>
        bool LiveFree()
        {
            if (Covered() || (model.Mode == CardStackMode.Hidden ? MomentPlayer.PromptsBlocked() : OtherPromptsBlocked())) return false;
            if (hud != null && (hud.CleanMode || hud.Hand != GameHud.KitTool.None)) return false;
            var talk = EveningTalkView.Instance;
            if (talk != null && talk.Open) return false;
            var choice = ChoiceSceneView.Instance;
            return choice == null || choice.State == ChoiceSceneView.Phase.Idle;
        }

        /// <summary><see cref="MomentPlayer.PromptsBlocked"/> without the cards' own peek.</summary>
        static bool OtherPromptsBlocked()
        {
            var recall = RecallPromptView.Instance;
            if (recall != null && recall.IsOpen) return true;
            if (PulseQuestionView.Instance != null && PulseQuestionView.Instance.IsOpen) return true; // #636
            var tool = ShapingView.Instance;
            if (tool != null && tool.WishArmed) return true;
            var sm = ShowMeDirector.Instance;
            if (sm != null && sm.Busy) return true;
            var fr = FirstRunView.Instance;
            return fr != null && (fr.CardOpen || fr.NameSheetOpen);
        }

        /// <summary>Debug and tests (#238): the card of a dilemma raised live that waits for the screen to be free (-1 = none).</summary>
        public int LivePending => livePending;

        void ShowSheet(bool on)
        {
            if (on == sheet.IsOpen) return;
            UiSound.Play(on ? UiRole.CardIn : UiRole.CardOut);
            if (on) sheet.Open(); else sheet.Close();
        }

        void ShowBar(bool on)
        {
            if (on == barUp) return;
            barUp = on;
            showBar.pickingMode = on ? PickingMode.Position : PickingMode.Ignore; // a leaving bar takes no taps
            if (on) GlassMotion.Show(showBar, GlassMotion.SlideOff, null, GlassMotion.SlideMs);
            else GlassMotion.Hide(showBar, GlassMotion.SlideOff, GlassMotion.SlideMs);
        }

        void ShowPeek(bool on)
        {
            if (on == peek.IsOpen) return;
            if (on) peek.Open(); else peek.Close();
        }

        static bool Pressed(out Vector2 at)
        {
            at = default;
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.isInProgress) { at = t.position.ReadValue(); return true; }
            var m = Mouse.current;
            if (m == null || !m.leftButton.isPressed) return false;
            at = m.position.ReadValue();
            return true;
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            var i = GameUiPanel.SafeInsets(lastSafe, Screen.width, Screen.height);
            peek.style.bottom = i.w + GameUiPanel.Lane(GameUiPanel.PeekBottom, i.w);
            peek.style.left = GlassSheet.SidePad + i.x;
            peek.style.right = GlassSheet.SidePad + i.z;
            showBar.style.bottom = i.w + GameUiPanel.Lane(GameUiPanel.ToolsBottom, i.w);
            showBar.style.left = GlassSheet.SidePad + i.x;
            showBar.style.right = GlassSheet.SidePad + i.z;
            bool shortPhone = GlassSheet.IsShort(Screen.width, Screen.height);
            sheet.SetInsets(i, shortPhone ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
            sheet.EnableInClassList("cards--short", shortPhone);
            if (shortPhone == tight) return;
            tight = shortPhone;
            // Spec §2: the year joins the dots. There's room for little, and why a Give is locked must never scroll out of sight.
            var lines = alsoBox.parent;
            if (tight) { yearSide.Add(footer); lines.Insert(0, note); }
            else { lines.Insert(lines.IndexOf(alsoBox), note); lines.Insert(lines.IndexOf(alsoBox), footer); }
        }

        // ================================================================== building

        static Label Text(string name, VisualElement parent, params string[] classes)
        {
            var l = new Label { name = name, pickingMode = PickingMode.Ignore };
            foreach (var c in classes) l.AddToClassList(c);
            parent.Add(l);
            return l;
        }

        /// <summary>A line of the scrolling part: one row, so the view ends under the last that fits whole.</summary>
        Label Line(string name, params string[] classes)
        {
            var l = Text(name, scroll, classes);
            l.enableRichText = true;
            Row(l, QuotePt * QuoteRatio * GlassType.Pt);
            return l;
        }

        static void Row(VisualElement e, float pitch)
        {
            e.AddToClassList("cards-line");
            e.AddToClassList(GlassScroll.RowClass);
            e.userData = pitch;
        }

        static GlassButton Press(string name, string text, System.Action tapped, VisualElement parent, ButtonKind kind = ButtonKind.Secondary, UiRole role = UiRole.Tap)
        {
            var b = new GlassButton(text, tapped, kind, role) { name = name };
            parent.Add(b);
            return b;
        }

        void Build(VisualElement parent)
        {
            parent.Clear();
            tight = false;
            root = new VisualElement { name = "cards-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            parent.Add(root);

            peek = new GlassPeek { name = "cards-peek" };
            peek.style.display = DisplayStyle.None;
            peekPortrait = new GlassPortrait("") { name = "cards-peek-portrait" };
            peekStar = new GlassIcon("star", "kg-icon--light");
            peekPortrait.Add(peekStar);
            peek.Add(peekPortrait);
            var lines = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("kg-peek-lines");
            peek.Add(lines);
            peekText = Text("cards-peek-text", lines, "type-head", "kg-peek-line");
            peekTeaser = Text("cards-peek-teaser", lines, "type-quote", "kg-peek-teaser");
            peekAsk = Text("cards-peek-ask", peek, "kg-peek-ask"); // t3-choice: a choice waits on the Keeper
            peekAsk.text = "?";
            peek.RegisterCallback<PointerDownEvent>(e => SwipeDown(peek, e));
            peek.RegisterCallback<PointerUpEvent>(e => { if (SwipeUp(peek, e, out var d)) PeekReleased(d); });
            root.Add(peek);

            sheet = new GlassSheet { name = "cards-sheet" };
            sheet.style.display = DisplayStyle.None;
            // Trickle down: a ScrollView keeps its touches to itself, and a swipe that starts on the text still turns the page.
            sheet.RegisterCallback<PointerDownEvent>(SheetPressed, TrickleDown.TrickleDown);
            sheet.RegisterCallback<PointerDownEvent>(e => { if (e.pointerId == swipePointer && !swipeInScroll) sheet.CapturePointer(e.pointerId); });
            sheet.RegisterCallback<PointerUpEvent>(SheetLifted, TrickleDown.TrickleDown);
            sheet.RegisterCallback<PointerUpEvent>(SheetLifted);
            root.Add(sheet);

            showBar = new GlassBanner { name = "cards-showme-bar", pickingMode = PickingMode.Ignore };
            showBar.AddToClassList("kg-banner--wrap"); // visit loop: "The Keeper chose full…" was cut off at 1080x1920
            showBar.style.display = DisplayStyle.None;
            showBar.End.Add(new GlassButton("Back to the story", () => showingPlace = false, ButtonKind.Secondary, UiRole.Back) { name = "cards-showme-back", Compact = true });
            root.Add(showBar);

            card = new GlassCard();
            sheet.Add(card);
            BuildDots();
            BuildHead();
            BuildScroll();
            BuildDilemma();
            BuildActions();
            ApplySafeArea();
        }

        /// <summary>The dilemma's two options sit between the question (the headline) and the buttons row. Names for tests and the tester:
        /// <c>cards-dilemma</c>, <c>cards-dil-0/1</c> (the column), <c>cards-dil-answer-0/1</c> (the button), <c>cards-dil-hint-0/1</c>.</summary>
        void BuildDilemma()
        {
            dilBox = new VisualElement { name = "cards-dilemma", pickingMode = PickingMode.Ignore };
            dilBox.AddToClassList("cards-dilemma");
            card.hierarchy.Insert(card.hierarchy.IndexOf(scroll), dilBox);
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                var col = new VisualElement { name = "cards-dil-" + i, pickingMode = PickingMode.Ignore };
                col.AddToClassList("cards-dil");
                if (i == 1) col.AddToClassList("cards-dil--second");
                dilBox.Add(col);
                dilCols[i] = col;
                var b = Press("cards-dil-answer-" + i, "", () => AnswerDilemma(k), col, ButtonKind.Secondary, UiRole.Confirm);
                b.AddToClassList("cards-dil-btn");
                dilFaces[i] = new GlassPortrait("");
                dilFaces[i].AddToClassList("cards-dil-face");
                dilPlaces[i] = new GlassIcon("pin", "kg-icon--light");
                dilFaces[i].Add(dilPlaces[i]);
                b.Add(dilFaces[i]);
                dilLabels[i] = Text("cards-dil-label-" + i, b, "type-btn", "cards-dil-label");
                dilAnswers[i] = b;
                dilHints[i] = Text("cards-dil-hint-" + i, col, "type-note", "cards-dil-hint");
                dilHints[i].enableRichText = true;
            }
            dilBox.style.display = DisplayStyle.None;
            // #368: every choice can be handed back. The wording is fixed. Quiet, under the two options, only while the question is open.
            // It sits in the button row (BuildActions): on its own line between the options and the row it overflowed the sheet and the
            // row drew over it (batch 1, 1179x2556: "Show me" covered "Let them").
            dilLet = Press("cards-dil-let", "Let them decide", () => model?.LetThemDecide(), card, ButtonKind.Quiet, UiRole.Confirm);
            dilLet.style.alignSelf = Align.Center;
            dilLet.style.display = DisplayStyle.None;
        }

        /// <summary>The hook #224's choice scene implements: Show me on an open dilemma card offers the dilemma to it. Return true to take the
        /// tap (the scene flies in and the card waits behind it); false, or no hook, and Show me frames the two people or places as before.</summary>
        public static System.Func<DilemmaView, bool> DilemmaShowMeHook;

        /// <summary>#224: answered by a touch in the box: the bar says what the Keeper chose while the people react; the card comes back
        /// after, answered.</summary>
        public void ChoiceAnswered()
        {
            var f = model?.Face;
            var v = f?.dilemma;
            if (v == null || f.dilemmaChosen < 0 || f.dilemmaChosen >= v.options.Length) return;
            showBar.Set(f.dilemmaLetThem ? $"The Keeper let the people decide: {v.options[f.dilemmaChosen].label}." : $"The Keeper chose {v.options[f.dilemmaChosen].label}.", null);
        }

        void AnswerDilemma(int option)
        {
            if (model == null || !model.AnswerDilemma(option)) return; // Changed redraws: the chosen face lifts, the other dims
        }

        void BuildDots()
        {
            var row = new VisualElement { name = "cards-dots-row", pickingMode = PickingMode.Ignore };
            row.AddToClassList("cards-dots-row");
            card.hierarchy.Insert(0, row);
            yearSide = new VisualElement { pickingMode = PickingMode.Ignore };
            yearSide.AddToClassList("cards-dots-side");
            yearSide.AddToClassList("cards-dots-year");
            row.Add(yearSide);
            card.Dots.name = "cards-dots";
            row.Add(card.Dots);
            var mirror = new VisualElement { pickingMode = PickingMode.Ignore }; // keeps the dots centred under the sheet
            mirror.AddToClassList("cards-dots-side");
            row.Add(mirror);
        }

        void BuildHead()
        {
            head = card.Head;
            head.name = "cards-head";
            head.AddToClassList("cards-head");
            ports = new VisualElement { pickingMode = PickingMode.Ignore };
            ports.AddToClassList("cards-ports");
            head.Insert(0, ports);
            for (int i = 0; i < portraits.Length; i++)
            {
                int k = i;
                portraits[i] = Press("cards-portrait-" + i, "", () => OpenPerson(portraitIds[k]), ports, ButtonKind.Icon, UiRole.None);
                portraits[i].AddToClassList("cards-portrait");
                faces[i] = new GlassPortrait("");
                portraits[i].Add(faces[i]);
            }
            portraits[1].AddToClassList("cards-portrait--second");
            eraMark = new GlassPortrait("") { name = "cards-era-mark" };
            eraMark.AddToClassList("kg-portrait--era");
            eraMark.Add(new GlassIcon("star", "kg-icon--light"));
            ports.Add(eraMark);
            headline = card.Title;
            headline.name = "cards-headline";
            headline.enableRichText = true;
            alsoTitle = Text("cards-also-title", head, "type-sheet", "kg-card-title");
            alsoTitle.text = "Also…";
            skipBtn = Press("cards-skip", "Skip all", () => model?.SkipAll(), head, ButtonKind.Quiet, UiRole.Close);
        }

        void BuildScroll()
        {
            scroll = new GlassScroll { name = "cards-scroll", WholeRows = true, SplitRows = true };
            scroll.AddToClassList("cards-scroll");
            scroll.View.contentContainer.AddToClassList("cards-scroll-pad");
            scroll.View.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => FitStory());
            scroll.View.RegisterCallback<GeometryChangedEvent>(_ => FitStory());
            card.hierarchy.Insert(card.hierarchy.IndexOf(card.Actions), scroll);
            teaser = Line("cards-teaser", "type-quote");
            body = card.Body;
            body.name = "cards-body";
            Row(body, GlassType.BodyPitch);
            scroll.Add(body);
            endLine = Line("cards-endline", "type-quote");
            quote = Line("cards-quote", "type-quote");
            note = Line("cards-note", "type-note");
            note.userData = NotePt * QuoteRatio * GlassType.Pt;
            footer = card.Sub;
            footer.name = "cards-footer";
            Row(footer, QuotePt * QuoteRatio * GlassType.Pt);
            scroll.Add(footer);

            alsoBox = new VisualElement { name = "cards-also", pickingMode = PickingMode.Ignore };
            scroll.Add(alsoBox);
            for (int i = 0; i < alsoLines.Length; i++)
            {
                int k = i;
                var b = new Button(() => { UiSound.Play(UiRole.Tap); model?.OpenAlso(k); }) { name = "cards-also-" + i };
                b.RemoveFromClassList(Button.ussClassName);
                b.AddToClassList("cards-also-line");
                b.AddToClassList(GlassScroll.RowClass);
                b.userData = GameUiPanel.MinTarget; // a line is a target: never half of one
                alsoBox.Add(b);
                alsoLines[i] = b;
            }
        }

        void BuildActions()
        {
            var row = card.Actions;
            row.name = "cards-buttons";
            backBtn = Press("cards-back", "", () => model?.Back(), row, ButtonKind.Icon, UiRole.Back);
            backBtn.Icon = "back";
            nextBtn = Press("cards-next", "Next", () => model?.Next(), row);
            showMeBtn = Press("cards-showme", "Show me", ShowMe, row, ButtonKind.Secondary, UiRole.None); // the director sounds it when the flight starts
            actionBtn = Press("cards-action", "", Act, row, ButtonKind.Primary, UiRole.Confirm);
            if (dilLet != null) { dilLet.RemoveFromHierarchy(); row.Add(dilLet); } // #368: after Show me, while a choice is open
        }

        // ================================================================== touches

        void SwipeDown(VisualElement e, PointerDownEvent ev)
        {
            swipeFrom = ev.position;
            swipePointer = ev.pointerId;
            e.CapturePointer(ev.pointerId);
        }

        bool SwipeUp(VisualElement e, PointerUpEvent ev, out Vector2 delta)
        {
            delta = (Vector2)ev.position - swipeFrom;
            if (ev.pointerId != swipePointer) return false;
            swipePointer = -1;
            if (e.HasPointerCapture(ev.pointerId)) e.ReleasePointer(ev.pointerId);
            return true;
        }

        /// <summary>Where a swipe starts. Not on a button: its own tap would turn the page as well. On a card that scrolls, the
        /// pointer stays the ScrollView's, or the text could never move.</summary>
        void SheetPressed(PointerDownEvent ev)
        {
            if (ev.target is Button) return;
            swipeFrom = ev.position;
            swipePointer = ev.pointerId;
            var view = scroll.View;
            swipeInScroll = view.worldBound.Contains(ev.position) && view.contentContainer.layout.height > view.layout.height + 1f;
        }

        void SheetLifted(PointerUpEvent ev)
        {
            if (SwipeUp(sheet, ev, out var d)) SheetReleased(d);
        }

        void PeekReleased(Vector2 d)
        {
            if (model == null) return;
            if (d.y > SwipeMin) { model.HidePeek(); return; }
            if (d.magnitude < SwipeMin && model.TapPeek()) Sfx.Play(CueLibrary.UiOpen);
        }

        void SheetReleased(Vector2 d)
        {
            if (model == null) return;
            if (Mathf.Abs(d.x) >= SwipeMin && Mathf.Abs(d.x) > Mathf.Abs(d.y))
            {
                if (d.x < 0) model.Next(); else model.Back();
            }
            else if (d.y >= SwipeMin && !swipeInScroll) model.Close(); // a drag down on a long card is its scroll
        }

        void ShowMe()
        {
            if (model != null && model.DilemmaShowMe())
            {
                // The choice scene (#224) took it: the sheet slides off so the people can be touched, the bar keeps the ask.
                showBar.Set(ChoiceNote, null); // the ask itself is said in the box, by the one who asks
                showingPlace = true;
                choiceUp = true;
                return;
            }
            choiceUp = false;
            var shown = model?.ShowMe();
            var sm = ShowMeDirector.Instance;
            if (sm == null || model == null) return;
            watchingFlight = shown != null && sm.Show(shown) != null;
            if (watchingFlight)
            {
                var f = model.Face;
                showBar.Set(f?.headline, f?.footer);
                showingPlace = true;
                return;
            }
            model.ShowMeLanded();
            GameHud.Instance?.Toast(CantGoThere, 2.5f); // the card stays, one line says why (board 3.3)
        }

        const string CantGoThere = "That happened somewhere the camera can't go.";

        void OpenPerson(int id)
        {
            var cards = PersonCardView.Instance;
            if (model == null || id < 0 || cards == null) return;
            model.OpenPerson(id);
            personOpen = cards.Open(id, true, false);
            if (!personOpen) model.PersonClosed();
        }

        void Act()
        {
            var face = model?.Face;
            if (face == null) return;
            switch (face.action)
            {
                case CardAction.Give:
                    var o = model.Give();
                    if (!o.ok) { Sfx.Play(AquariumCiv.View.Shaping.ShapingShow.FailCue); break; } // the card says why
                    ShapingView.Instance?.ArmWish(o.wish.id);
                    break;
                case CardAction.Remember:
                    int id = model.Remember();
                    // Their page in the book (#80), and until they have one, their card.
                    var book = BookView.Instance;
                    if (id >= 0 && book != null && book.OpenAtLegend(id, model.PersonClosed)) break;
                    var cards = PersonCardView.Instance;
                    if (id >= 0 && cards != null) personOpen = cards.Open(id, true, false);
                    if (!personOpen) model.PersonClosed();
                    break;
                case CardAction.Watch:
                    if (model.Watch()) MomentPlayer.Instance?.Watch();
                    break;
            }
        }

        // ================================================================== drawing

        static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Words at their type's px line height (spec §1), or no line at all.</summary>
        static void Set(Label l, string text, float pt = 15f, float ratio = 1.35f)
        {
            bool on = !string.IsNullOrEmpty(text);
            l.text = on ? GlassType.Lines(text, pt, ratio) : "";
            Show(l, on);
        }

        void Paint(GlassPortrait face, int id)
        {
            var who = bound?.FindNotable(id);
            face.Person = who?.name ?? "?";
            face.EnableInClassList("kg-portrait--gone", who != null && !who.alive);
        }

        void Draw()
        {
            if (root == null) return;
            if (model == null || model.Mode == CardStackMode.Hidden) { if (wasOpen) { wasOpen = false; GameClock.Instance?.Save(); } return; }
            if (model.Mode == CardStackMode.Peek) { DrawPeek(); return; }
            wasOpen = true;
            var f = model.Face;
            if (f != null && debugEdit != null) { if (f.id == debugFace) debugEdit(f); else debugEdit = null; }
            DrawFace(f);
        }

        bool wasOpen;

        /// <summary>#240: a callback is a story that comes back years later, and it is the whole card: the sheet grows (up to
        /// <see cref="StoryMaxShare"/> of the screen) until the grey question, the headline and the story fit without a scroll.</summary>
        bool fitStory;
        const float StoryMaxShare = 0.5f, StoryMinShare = 0.3f;

        void SetFitStory(bool on)
        {
            fitStory = on;
            if (!on) sheet.style.height = StyleKeyword.Null;
            else FitStory();
        }

        void FitStory()
        {
            if (!fitStory || root == null) return;
            var view = scroll.View;
            float panelH = root.layout.height, cur = sheet.layout.height;
            if (float.IsNaN(panelH) || panelH <= 0f || float.IsNaN(cur) || cur <= 0f) return;
            float over = view.contentContainer.layout.height - view.layout.height;
            if (float.IsNaN(over) || Mathf.Abs(over) < 1f) return;
            float want = Mathf.Clamp(cur + over + 2f, panelH * StoryMinShare, panelH * StoryMaxShare);
            if (Mathf.Abs(want - cur) > 1f) sheet.style.height = want;
        }

        void DrawPeek()
        {
            var p = model.Peek;
            peekText.text = p.text ?? "";
            Set(peekTeaser, p.teaser, QuotePt, QuoteRatio);
            bool person = p.notableId >= 0;
            if (person) Paint(peekPortrait, p.notableId); else peekPortrait.Person = "";
            peekPortrait.EnableInClassList("kg-portrait--era", !person); // a story of the whole box wears the star, as on its card
            Show(peekStar, !person);
            Show(peekPortrait, !p.isTeaser);
            peek.EnableInClassList("kg-peek--teaser", p.isTeaser);
        }

        void DrawFace(CardFace f)
        {
            if (f == null) return;
            card.Dots.Set(model.PageCount, model.Index);
            scroll.View.scrollOffset = Vector2.zero;
            Show(alsoBox, f.isAlso);
            Show(headline, !f.isAlso);
            Show(alsoTitle, f.isAlso);
            Show(skipBtn, !f.isAlso);
            if (f.isAlso) DrawAlso(f); else DrawStory(f);
            Show(backBtn, model.CanGoBack);
            Show(showMeBtn, !f.isAlso);
            Show(actionBtn, f.action != CardAction.None);
            if (f.dilemma != null && f.dilemmaChosen >= 0) Show(showMeBtn, false); // answered: nothing left to look at, Next is the way on
            actionBtn.text = f.actionLabel;
            actionBtn.Available = f.actionEnabled;
            bool done = model.OnLastPage;
            nextBtn.text = done ? "Done" : "Next";
            nextBtn.Role = done ? UiRole.Confirm : UiRole.Tap;
            nextBtn.Kind = f.isAlso ? ButtonKind.Primary : ButtonKind.Secondary; // Next is never primary: only Also… has nothing else to lead
        }

        void DrawAlso(CardFace f)
        {
            SetFitStory(false);
            foreach (var l in new[] { teaser, body, endLine, quote, note, footer }) Show(l, false);
            Show(dilBox, false);
            Show(dilLet, false);
            Show(scroll, true);
            headline.RemoveFromClassList("cards-ask");
            card.RemoveFromClassList("cards-dilcard");
            head.EnableInClassList("cards-head--alone", true);
            // Every headline stays reachable by scrolling; what's past them is never counted (rule 8).
            for (int i = 0; i < alsoLines.Length; i++)
            {
                bool on = i < f.alsoLines.Count;
                Show(alsoLines[i], on);
                if (on) alsoLines[i].text = f.alsoLines[i];
            }
        }

        /// <summary>The dilemma card: the people's ask as the headline, two options under it. Answered, the chosen one lifts and the other
        /// dims, and the headline says what the Keeper chose (the people chose, on a lapse).</summary>
        void DrawDilemma(CardFace f)
        {
            var v = f.dilemma;
            SetFitStory(false);
            Show(scroll, false);
            Show(dilBox, true);
            Show(dilLet, f.dilemmaChosen < 0 && !showingPlace); // visit loop: the sheet's copy is really hidden while the box scene has its own
            Show(eraMark, false);
            head.EnableInClassList("cards-head--alone", true);
            headline.AddToClassList("cards-ask");
            card.AddToClassList("cards-dilcard");
            string words = f.dilemmaChosen < 0 ? v.question
                : f.dilemmaLetThem ? $"The Keeper let the people decide. They chose {v.options[f.dilemmaChosen].label}."
                : f.dilemmaLapsed ? $"The people chose {v.options[f.dilemmaChosen].label} themselves."
                : $"The Keeper chose {v.options[f.dilemmaChosen].label}.";
            headline.text = GlassType.Lines(words, 16f, 1.25f);
            for (int i = 0; i < 2; i++)
            {
                var o = i < v.options.Length ? v.options[i] : null;
                Show(dilCols[i], o != null);
                if (o == null) continue;
                bool person = o.personId >= 0;
                if (person) Paint(dilFaces[i], o.personId); else dilFaces[i].Person = "";
                dilFaces[i].EnableInClassList("kg-portrait--era", !person); // a place or a way wears an icon on the same disc
                Show(dilPlaces[i], !person);
                if (!person) dilPlaces[i].Shape = PlaceIcon(v, o);
                dilLabels[i].text = o.label;
                Set(dilHints[i], o.hint, NotePt, tight ? 1.1f : QuoteRatio);
                bool open = f.dilemmaChosen < 0, chosen = f.dilemmaChosen == i;
                dilAnswers[i].pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
                dilAnswers[i].Kind = chosen ? ButtonKind.Primary : ButtonKind.Secondary;
                dilCols[i].EnableInClassList("cards-dil--chosen", chosen && !f.dilemmaLapsed);
                dilCols[i].EnableInClassList("cards-dil--dim", !open && !chosen);
            }
        }

        static string PlaceIcon(DilemmaView v, DilemmaOptionView o) =>
            !string.IsNullOrEmpty(o.buildingType) ? "hill" : v.kind switch
            {
                DilemmaKind.Tradition => "scroll",
                DilemmaKind.Strangers => "herd",
                _ => "pin",
            };

        void DrawStory(CardFace f)
        {
            if (f.dilemma != null) { DrawDilemma(f); return; }
            Show(dilBox, false);
            Show(dilLet, false);
            Show(scroll, true);
            headline.RemoveFromClassList("cards-ask");
            card.RemoveFromClassList("cards-dilcard");
            Set(teaser, f.teaserLine, QuotePt, QuoteRatio);
            headline.text = GlassType.Lines(f.headline, TitlePt, TitleRatio);
            Set(body, f.body);
            Set(endLine, f.endLine, QuotePt, QuoteRatio);
            Set(quote, string.IsNullOrEmpty(f.quote) ? "" : Quoted(f), QuotePt, QuoteRatio);
            Set(note, f.actionNote, NotePt, QuoteRatio);
            string year = f.keeperMark ? f.footer + " · the Keeper's gift" : f.footer;
            footer.text = year ?? "";
            Show(footer, !string.IsNullOrEmpty(year));
            for (int i = 0; i < portraits.Length; i++)
            {
                bool on = i < f.portraits.Count;
                portraitIds[i] = on ? f.portraits[i] : -1;
                Show(portraits[i], on);
                if (on) Paint(faces[i], f.portraits[i]);
            }
            bool era = f.kind == CardKind.EraReady || (f.portraits.Count == 0 && f.card?.lead.kind == "era");
            Show(eraMark, era);
            head.EnableInClassList("cards-head--alone", !era && f.portraits.Count == 0);
            SetFitStory(f.card != null && f.card.lead.kind == SimCore.CallbackKind);
        }

        string Quoted(CardFace f)
        {
            var who = f.quoteBy >= 0 ? bound?.FindNotable(f.quoteBy)?.name : null;
            return who != null ? $"“{f.quote}” ({who})" : $"“{f.quote}”";
        }

        // ================================================================== debug (panel, `script eval`)

        /// <summary>A return of up to <paramref name="cards"/> cards, whatever the time away.</summary>
        public static string DebugReturn(int cards)
        {
            var m = Instance?.model;
            if (m == null) return "no card stack";
            return m.DebugPrepare(cards) ? Status() : "nothing to tell";
        }

        /// <summary>Every story is unread again.</summary>
        public static string DebugClearRead()
        {
            var m = Instance?.model;
            if (m == null) return "no card stack";
            Sim.DebugClearCardsRead();
            return "all cards unread";
        }

        /// <summary>The stack open on a card of this kind, made now if the box has none.</summary>
        public static string DebugCard(CardKind kind)
        {
            var v = Instance;
            var sim = Sim;
            if (v == null || v.model == null || sim == null) return "no card stack";
            var who = sim.State.notables.Find(n => n.alive);
            switch (kind)
            {
                case CardKind.Wish: sim.DebugForceWish(); break;
                case CardKind.EraReady: sim.ForceEraReady(); break;
                case CardKind.Relic: sim.DebugGiveRelic(RelicId.All[0]); break;
                case CardKind.Legend: sim.DebugKillOldest(); break;
                case CardKind.Dilemma: sim.DebugRaiseDilemma(); break;
                case CardKind.Keeper: sim.Chronicle("keeper", $"After the Keeper's rain, {who?.FullName} staked out new ground.", 0.9f, who?.id ?? -1); break;
                default: sim.Chronicle("story", $"{who?.FullName} found a way across the stream.", 0.9f, who?.id ?? -1); break;
            }
            var m = v.model;
            if (m.Mode == CardStackMode.Open) m.Close();
            if (!m.DebugPrepare(7) || !m.TapPeek()) return "nothing to tell";
            for (int guard = 0; guard < 8 && m.Face != null && (m.Face.isAlso || m.Face.kind != kind); guard++) if (!m.Next()) return "no such card came up";
            return m.Face != null && m.Face.kind == kind ? Status() : "no such card came up";
        }

        /// <summary>The stack open on the card of this chronicle entry (a raised dilemma, a callback just fired), as on a return. #225.</summary>
        public static string DebugOpenEntry(int entryId)
        {
            var v = Instance;
            if (v == null || v.model == null) return "no card stack";
            var m = v.model;
            if (m.Mode == CardStackMode.Open) m.Close();
            if (!m.DebugPrepare(16) || !m.TapPeek()) return "nothing to tell";
            for (int guard = 0; guard < 16 && (m.Shown == null || m.Shown.lead.id != entryId); guard++) if (!m.Next()) return "that card did not come up";
            return m.Shown != null && m.Shown.lead.id == entryId ? Status() : "that card did not come up";
        }

        /// <summary>Show me on the open page, as its button: the sheet leaves and the bar comes up.</summary>
        public static string DebugShowMe()
        {
            var v = Instance;
            if (v?.model == null || v.model.Mode != CardStackMode.Open || v.model.Face == null || v.model.Face.isAlso) return "open a card first";
            v.ShowMe();
            return v.showingPlace ? "show me: the bar is up" : "no place to show";
        }

        /// <summary>This page drawn as a card with a teaser it pays off (<c>payoff</c>), an end line (<c>end</c>), too much to fit
        /// (<c>long</c>) or two people (<c>two</c>).</summary>
        public static string DebugText(string what)
        {
            var v = Instance;
            var face = v?.model?.Face;
            if (face == null || face.isAlso) return "open a card first";
            var pair = Sim.State.notables.FindAll(n => n.alive);
            if (what == "two" && pair.Count < 2) return "two people needed";
            v.debugFace = face.id;
            v.debugEdit = what switch
            {
                "payoff" => f => f.teaserLine = "At the fire: The craftsfolk talk of guilds, and of a bigger market.",
                "end" => f => { f.teaserLine = "At the fire: Edmund and Hal aren't speaking again."; f.endLine = "They never did make up."; },
                "two" => f => { f.portraits.Clear(); f.portraits.Add(pair[0].id); f.portraits.Add(pair[1].id); },
                _ => f =>
                {
                    f.body = "A piece of bread the size of a hut fell from the sky the year the Keeper shaped the land. They tasted it first, and then everyone did. The village ate well all winter, and the old folk still tell it.";
                    f.quote = "If the sky drops bread, what else is up there?";
                    f.quoteBy = -1;
                },
            };
            v.Draw();
            return "card text: " + what;
        }

        public static string Status()
        {
            var m = Instance?.model;
            return m == null ? "no card stack" : $"cards {m.Mode}: page {m.Index + 1}/{m.PageCount}, {m.CardCount} cards, also {m.AlsoCount}, dot {m.HistoryDot}";
        }
    }
}
