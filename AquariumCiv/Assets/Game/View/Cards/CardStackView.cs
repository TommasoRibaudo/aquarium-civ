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
    /// The chronicle cards on screen (#75; chronicle-and-history §2, §7): the peek and the slim stack at the bottom, in the
    /// plain #36 HUD style. All the logic is <see cref="CardStackModel"/>; this file only draws <see cref="CardStackModel.Face"/>
    /// and passes touches on, and the look is the <c>card*</c> classes in Game.uss, so the UI rework restyles it without
    /// touching the model. Stable names for tests and the tester agent: <c>cards-peek</c>, <c>cards-sheet</c>, <c>cards-next</c>,
    /// <c>cards-back</c>, <c>cards-skip</c>, <c>cards-showme</c>, <c>cards-action</c>, <c>cards-portrait-0/1</c>,
    /// <c>cards-also-0/1/2</c>.
    /// </summary>
    public class CardStackView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static CardStackView Instance { get; private set; }

        const string ToolBlock = "cards";
        /// <summary>A drag at least this long (panel units) is a swipe, not a tap.</summary>
        const float SwipeMin = 90f;

        UIDocument doc;
        VisualElement root, peek, sheet, dots, head, alsoBox;
        Label peekText, peekTeaser, peekPortrait, teaser, headline, body, endLine, quote, note, footer, alsoTitle, alsoMore;
        Button nextBtn, backBtn, skipBtn, showMeBtn, actionBtn;
        readonly Button[] portraits = new Button[2];
        readonly int[] portraitIds = { -1, -1 };
        readonly Button[] alsoLines = new Button[3];
        readonly List<VisualElement> dotEls = new();
        GameClock clock;
        GameHud hud;
        SimCore bound;
        CardStackModel model;
        Rect lastSafe;
        Vector2Int lastScreen;
        bool personOpen, watchingFlight, pressedLast, toolsHidden;
        Vector2 swipeFrom;
        int swipePointer = -1;

        public CardStackModel Model => model;
        public VisualElement Root => root;
        /// <summary>The peek or the stack is up: prompts over the world wait (MomentPlayer.PromptsBlocked).</summary>
        public bool Showing => model != null && model.Mode != CardStackMode.Hidden;

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
            if (hud != null) hud.HistoryTapped = null;
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
                if (hud != null) hud.HistoryTapped = OnHistory;
            }
        }

        void OnCaughtUp(CatchUpResult r) => model?.Prepare(r.realHours);

        /// <summary>The History button: the waiting stack if there is one. Until the time-lapse has a screen of its own (#79,
        /// nothing there lets a player back out yet) the rest stays the History sheet.</summary>
        /// <summary>Cards waiting: show them. Otherwise the time-lapse screen (#79; its Close is the way out). False (nothing to replay yet,
        /// a moment is playing, or the first run: first-session §2 keeps the time-lapse out of it): the HUD opens its History sheet.</summary>
        bool OnHistory()
        {
            if (bound != null && bound.State.firstRun.active) return false;
            if (model != null && model.HistoryDot && model.Reopen()) return true;
            return TimeLapseScreenView.Ensure().Open();
        }

        void Bind(SimCore sim)
        {
            if (model != null) { model.Changed -= Draw; model.ShowMeEnd -= OnShowMeEnd; }
            bound = sim;
            model = null;
            if (sim != null)
            {
                model = new CardStackModel(sim, () => Time.realtimeSinceStartupAsDouble, GameClock.Tel);
                model.Changed += Draw;
                model.ShowMeEnd += OnShowMeEnd;
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

        /// <summary>Something else holds the screen: the cards stay as they are, out of sight.</summary>
        bool Covered()
        {
            if (hud != null && (hud.CameraMode || hud.SheetOpen || !hud.IsVisible)) return true;
            if (clock != null && clock.Visiting) return true;
            var card = PersonCardView.Instance;
            if (card != null && card.IsOpen) return true;
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
            if (hud != null) hud.SetHistoryDot(model != null && model.HistoryDot);
            if (model == null) { peek.style.display = sheet.style.display = DisplayStyle.None; return; }

            bool covered = Covered();
            bool open = model.Mode == CardStackMode.Open, peeking = model.Mode == CardStackMode.Peek;
            sheet.style.display = open && !covered ? DisplayStyle.Flex : DisplayStyle.None;
            peek.style.display = peeking && !covered ? DisplayStyle.Flex : DisplayStyle.None;
            SetToolsHidden(open);

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
            peek.style.bottom = GameUiPanel.PromptBottom + i.w; // the prompts' lane: toasts sit under it, the HUD's row under them
            peek.style.left = 40 + i.x;
            peek.style.right = 40 + i.z;
            sheet.style.paddingBottom = 24 + i.w;
            sheet.style.paddingLeft = 40 + i.x;
            sheet.style.paddingRight = 40 + i.z;
            if (model != null && model.OnAlsoPage) Draw();
        }

        /// <summary>The "Also…" page without its lines (panel units): padding, dots, title, the buttons.</summary>
        const float AlsoChrome = 288f, SheetShare = 0.30f;

        /// <summary>The slim sheet's rule (at most 30 % of the height) on the "Also…" page: how many lines fit at a full
        /// target's height, 1 to 3. The panel matches the width, so a wider screen is a shorter panel.</summary>
        public static int AlsoLinesThatFit(float panelHeight, float bottomInset) =>
            Mathf.Clamp(Mathf.FloorToInt((panelHeight * SheetShare - AlsoChrome - bottomInset) / GameUiPanel.MinTarget), 1, 3);

        // ================================================================== building

        static Label Text(string name, string cls, VisualElement parent)
        {
            var l = new Label { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            parent.Add(l);
            return l;
        }

        static Button Btn(string name, string text, System.Action onClick, VisualElement parent, params string[] classes)
        {
            var b = new Button(() => { Sfx.Play(CueLibrary.UiTap); onClick(); }) { name = name, text = text };
            foreach (var c in classes) b.AddToClassList(c);
            parent.Add(b);
            return b;
        }

        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "cards-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            parent.Add(root);

            peek = new VisualElement { name = "cards-peek" };
            peek.AddToClassList("card-peek");
            peek.style.display = DisplayStyle.None;
            peekPortrait = Text("cards-peek-portrait", "card-portrait", peek);
            var lines = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("card-peek-lines");
            peek.Add(lines);
            peekText = Text("cards-peek-text", "card-peek-text", lines);
            peekTeaser = Text("cards-peek-teaser", "card-teaser", lines);
            peek.RegisterCallback<PointerDownEvent>(e => SwipeDown(peek, e));
            peek.RegisterCallback<PointerUpEvent>(e => { if (SwipeUp(peek, e, out var d)) PeekReleased(d); });
            root.Add(peek);

            sheet = new VisualElement { name = "cards-sheet" };
            sheet.AddToClassList("card-sheet");
            sheet.style.display = DisplayStyle.None;
            sheet.RegisterCallback<PointerDownEvent>(e => SwipeDown(sheet, e));
            sheet.RegisterCallback<PointerUpEvent>(e => { if (SwipeUp(sheet, e, out var d)) SheetReleased(d); });
            root.Add(sheet);

            dots = new VisualElement { name = "cards-dots", pickingMode = PickingMode.Ignore };
            dots.AddToClassList("card-dots");
            sheet.Add(dots);
            teaser = Text("cards-teaser", "card-teaser", sheet);

            head = new VisualElement { name = "cards-head", pickingMode = PickingMode.Ignore };
            head.AddToClassList("card-head");
            sheet.Add(head);
            for (int i = 0; i < portraits.Length; i++)
            {
                int k = i;
                portraits[i] = Btn("cards-portrait-" + i, "", () => OpenPerson(portraitIds[k]), head, "card-portrait");
            }
            headline = Text("cards-headline", "card-headline", head);
            skipBtn = Btn("cards-skip", "Skip all", () => model?.SkipAll(), head, "card-btn", "card-btn--quiet");

            body = Text("cards-body", "card-body", sheet);
            endLine = Text("cards-endline", "card-body", sheet);
            quote = Text("cards-quote", "card-quote", sheet);
            note = Text("cards-note", "card-note", sheet);
            footer = Text("cards-footer", "card-footer", sheet);

            alsoBox = new VisualElement { name = "cards-also", pickingMode = PickingMode.Ignore };
            sheet.Add(alsoBox);
            var alsoHead = new VisualElement { pickingMode = PickingMode.Ignore };
            alsoHead.AddToClassList("card-also-head");
            alsoBox.Add(alsoHead);
            alsoTitle = Text("cards-also-title", "card-headline", alsoHead);
            alsoTitle.text = "Also…";
            alsoMore = Text("cards-also-more", "card-footer", alsoHead);
            for (int i = 0; i < alsoLines.Length; i++)
            {
                int k = i;
                alsoLines[i] = Btn("cards-also-" + i, "", () => model?.OpenAlso(k), alsoBox, "card-also-line");
            }

            var row = new VisualElement { name = "cards-buttons", pickingMode = PickingMode.Ignore };
            row.AddToClassList("card-buttons");
            sheet.Add(row);
            backBtn = Btn("cards-back", "Back", () => model?.Back(), row, "card-btn", "card-btn--quiet");
            showMeBtn = Btn("cards-showme", "Show me", ShowMe, row, "card-btn");
            actionBtn = Btn("cards-action", "", Act, row, "card-btn", "card-btn--primary");
            nextBtn = Btn("cards-next", "Next", () => model?.Next(), row, "card-btn", "card-btn--primary");
            ApplySafeArea();
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
            else if (d.y >= SwipeMin) model.Close();
        }

        void ShowMe()
        {
            var card = model?.ShowMe();
            var sm = ShowMeDirector.Instance;
            if (card == null || sm == null) return;
            watchingFlight = sm.Show(card) != null;
            if (!watchingFlight) model.ShowMeLanded();
        }

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
                    var cards = PersonCardView.Instance; // the book's page, once the book exists (#80)
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

        static void Set(Label l, string text)
        {
            l.text = text ?? "";
            Show(l, !string.IsNullOrEmpty(text));
        }

        string Initial(int id)
        {
            var name = bound?.FindNotable(id)?.name;
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
        }

        void Draw()
        {
            if (root == null) return;
            if (model == null || model.Mode == CardStackMode.Hidden) { if (wasOpen) { wasOpen = false; GameClock.Instance?.Save(); } return; }
            if (model.Mode == CardStackMode.Peek) { DrawPeek(); return; }
            wasOpen = true;
            DrawFace(model.Face);
        }

        bool wasOpen;

        void DrawPeek()
        {
            var p = model.Peek;
            peekText.text = p.text ?? "";
            Set(peekTeaser, p.teaser);
            peekPortrait.text = p.notableId >= 0 ? Initial(p.notableId) : "";
            Show(peekPortrait, p.notableId >= 0);
            peek.EnableInClassList("card-peek--teaser", p.isTeaser);
        }

        void DrawFace(CardFace f)
        {
            if (f == null) return;
            while (dotEls.Count < model.PageCount)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore };
                d.AddToClassList("card-dot");
                dots.Add(d);
                dotEls.Add(d);
            }
            for (int i = 0; i < dotEls.Count; i++)
            {
                Show(dotEls[i], i < model.PageCount);
                dotEls[i].EnableInClassList("card-dot--on", i == model.Index);
            }
            foreach (CardKind k in System.Enum.GetValues(typeof(CardKind))) sheet.EnableInClassList("card--" + k.ToString().ToLowerInvariant(), !f.isAlso && f.kind == k);
            sheet.EnableInClassList("card--sad", !f.isAlso && f.tone == CardTone.Sad);

            Show(alsoBox, f.isAlso);
            Show(head, !f.isAlso);
            Show(skipBtn, !f.isAlso);
            if (f.isAlso)
            {
                foreach (var l in new[] { teaser, body, endLine, quote, note, footer }) Show(l, false);
                float panelH = Screen.height * GameUiPanel.ReferenceWidth / Mathf.Max(1f, Screen.width);
                float bottom = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height).w;
                int fit = Mathf.Min(AlsoLinesThatFit(panelH, bottom), f.alsoLines.Count);
                for (int i = 0; i < alsoLines.Length; i++)
                {
                    bool on = i < fit;
                    Show(alsoLines[i], on);
                    if (on) alsoLines[i].text = f.alsoLines[i];
                }
                int more = f.alsoMore + f.alsoLines.Count - fit; // told like the overflow past three: counted here, kept in the chronicle
                Set(alsoMore, more > 0 ? $"and {more} more" : "");
            }
            else
            {
                Set(teaser, f.teaserLine);
                headline.text = f.headline;
                Set(body, f.body);
                Set(endLine, f.endLine);
                Set(quote, string.IsNullOrEmpty(f.quote) ? "" : Quoted(f));
                Set(note, f.actionNote);
                Set(footer, f.keeperMark ? f.footer + " · the Keeper's gift" : f.footer);
                for (int i = 0; i < portraits.Length; i++)
                {
                    bool on = i < f.portraits.Count;
                    portraitIds[i] = on ? f.portraits[i] : -1;
                    Show(portraits[i], on);
                    if (on) portraits[i].text = Initial(f.portraits[i]);
                }
            }
            Show(backBtn, model.CanGoBack);
            Show(showMeBtn, f.canShowMe);
            Show(actionBtn, f.action != CardAction.None);
            actionBtn.text = f.actionLabel;
            actionBtn.SetEnabled(f.actionEnabled);
            nextBtn.text = model.OnLastPage ? "Done" : "Next";
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

        public static string Status()
        {
            var m = Instance?.model;
            return m == null ? "no card stack" : $"cards {m.Mode}: page {m.Index + 1}/{m.PageCount}, {m.CardCount} cards, also {m.AlsoCount}, dot {m.HistoryDot}";
        }
    }
}
