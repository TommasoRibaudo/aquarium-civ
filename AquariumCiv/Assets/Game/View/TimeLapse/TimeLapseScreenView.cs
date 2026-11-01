using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.TimeLapseScreen;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// The time-lapse screen (#79 phase 2, Keeper's Glass in #168, board 3.5). All decisions live in <see cref="TimeLapseScreenModel"/>
    /// (pure, tested with dotnet); this class only draws the model and forwards pointer events and frame time. It drives
    /// <see cref="TimeLapseView"/> through <see cref="TimeLapseView.Show"/> only (the model ticks the clock and logs telemetry, so
    /// <see cref="TimeLapseView.externalTick"/> is on while the screen is open).
    ///
    /// A low <see cref="GlassSheet"/> on the bottom edge, its height the content's. The box at that year leads, so the HUD leaves
    /// (<see cref="GameHud.SetOutsideSheet"/>) and the only year on screen is the tag on the playhead. Under the timeline, a story's
    /// caption sits in the panel, and its controls take the play row's place when the player stopped on it; otherwise the row stays,
    /// so a caption the playhead crossed never takes Pause away. Words come in the shared toast, which rides above the panel.
    /// Colours, sizes and fonts are in Resources/GameUI/TimeLapse.uss; the code sets only what depends on data (x and width of bands and
    /// markers, the playhead, the caption's lines).
    ///
    /// Stable names for tests and the tester: tl-root, tl-panel, tl-year (the tag), tl-bar, tl-band-N, tl-pin-N, tl-playhead, tl-play,
    /// tl-speed, tl-now, tl-book, tl-snapshot, tl-close, tl-caption, tl-caption-text, tl-cap-portrait, tl-cap-prev, tl-cap-next,
    /// tl-cap-dots, tl-cap-person, tl-cap-close, tl-era-card, tl-empty.
    ///
    /// The one hook to add elsewhere: the HUD's History button calls <c>TimeLapseScreenView.Ensure().Open()</c>.
    /// </summary>
    [AddComponentMenu("AquariumCiv/Time-lapse Screen")]
    public class TimeLapseScreenView : MonoBehaviour
    {
        /// <summary>Panel units are 3 per dp on a 360 dp phone (1080 reference width); the model's numbers are in dp.</summary>
        const float U = 3f;
        /// <summary>A caption's line pitch (14 pt at 1.3, spec §1). It stops at 3 lines, or 2 on a short phone, then scrolls: 27 % of the height.</summary>
        const float CaptionPt = 14f, CaptionRatio = 1.3f, CaptionPitch = CaptionPt * CaptionRatio * GlassType.Pt;
        const int CaptionLines = 3, ShortCaptionLines = 2, MaxDots = 8;
        /// <summary>The portrait and its margin, which take that much of the caption's width.</summary>
        const float PortraitRoom = 148.5f, ScrollRoom = 24.75f;
        const float EraCardTop = 143f, EraCardUnderNotch = 22f;

        public static TimeLapseScreenView Instance { get; private set; }

        /// <summary>Set by whoever can share a file (a native share plugin later). Null: the screen just says it saved.</summary>
        public static System.Action<string> ShareHandler;

        /// <summary>#80's hook: the book opens from here, on the box on screen.</summary>
        public static System.Action<SimCore> BookHandler;

        /// <summary>#80's hook: new pages wait, so the Book button wears its ribbon.</summary>
        public static bool BookRibbon { get; set; }

        /// <summary>#80's hook: asked each time the screen opens, for the ribbon.</summary>
        public static System.Func<SimCore, bool> BookHasNew;

        public static TimeLapseScreenView Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TimeLapseScreen");
            return go.AddComponent<TimeLapseScreenView>();
        }

        UIDocument doc;
        VisualElement root, bar, bandsLayer, pinsLayer, playhead, captionCard, emptyNote, controls, capRow, ribbon;
        GlassSheet panel;
        GlassCaption eraCard, captionWords;
        GlassScroll captionScroll;
        GlassPortrait capPortrait;
        GlassPageDots capDots;
        Label yearLabel, captionText;
        GlassButton play, speed, now, capPrev, capNext, capPerson;
        TimeLapseView view;
        TimeLapseScreenModel model;
        int drawnVersion = -1;
        float shownPosition = float.NaN;
        float builtWidth = -1f;
        bool wasPlaying, tight;
        Rect lastSafe;
        Vector2Int lastScreen;
        bool capturing;

        public bool IsOpen { get; private set; }
        public TimeLapseScreenModel Model => model;
        public VisualElement Root => root;
        public VisualElement Bar => bar;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) CloseInternal(true);
        }

        // ================================================================== open / close
        /// <summary>Opens the screen at the present. False if there is nothing to replay yet or a moment is playing.</summary>
        public bool Open()
        {
            if (IsOpen) return true;
            view = TimeLapseView.Ensure();
            if (view == null || !view.Open()) return false;
            var sim = view.Lapse.Sim;
            view.externalTick = true;
            model = new TimeLapseScreenModel(view.Lapse, view.Clock, GameClock.Tel, sim.PlaceName, ScaledOptions());
            EnsureUi();
            if (BookHasNew != null) BookRibbon = BookHasNew(sim);
            root.style.display = DisplayStyle.Flex;
            IsOpen = true;
            drawnVersion = -1; shownPosition = float.NaN; builtWidth = -1f; wasPlaying = false;
            UiInput.Add(IsOverScreen);
            ApplySafeArea();
            panel.Open();
            GameHud.Instance?.SetOutsideSheet(panel);
            RelayoutIfWidthChanged();
            Redraw();
            return true;
        }

        /// <summary>Back to the live box.</summary>
        public void Close() => CloseInternal(false);

        void CloseInternal(bool destroying)
        {
            if (!IsOpen) return;
            IsOpen = false;
            UiInput.Remove(IsOverScreen);
            var hud = GameHud.Instance;
            if (hud != null && hud.OutsideSheet == panel) hud.SetOutsideSheet(null);
            if (root != null)
            {
                if (destroying) root.style.display = DisplayStyle.None;
                else panel.Close(() => { if (!IsOpen) root.style.display = DisplayStyle.None; });
            }
            if (view != null) { view.externalTick = false; view.Close(); }
            model = null;
        }

        bool IsOverScreen(Vector2 screenPos) => IsOpen && root != null && GameUiPanel.Over(root, screenPos);

        /// <summary>The model's numbers are in dp; the panel is in 1080-wide units (3 per dp). The track runs the panel's full width.</summary>
        static TimelineOptions ScaledOptions()
        {
            var o = new TimelineOptions();
            o.padLeft = o.padRight = 0f;
            o.minPinGap *= U; o.pinHitRadius *= U; o.tapSlop *= U;
            o.pinSnap *= U; o.nowSnap *= U; o.eraSnap *= U; o.bandLabelMinWidth *= U;
            return o;
        }

        // ================================================================== frame
        void Update()
        {
            if (!IsOpen || model == null) return;
            if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y) ApplySafeArea();
            // Who opens the person card over the panel, and closing it hands the HUD back: the HUD stays away until this closes.
            var hud = GameHud.Instance;
            if (hud != null && hud.OutsideSheet == null) hud.SetOutsideSheet(panel);
            RelayoutIfWidthChanged();
            model.Tick(Time.unscaledDeltaTime);
            if (float.IsNaN(shownPosition) || shownPosition != model.Position)
            {
                shownPosition = model.Position;
                view.Show(shownPosition, false);
            }
            ribbon.style.display = BookRibbon ? DisplayStyle.Flex : DisplayStyle.None; // the book can take it off at any time
            if (wasPlaying && !model.Playing) view.Settle();     // paused (or ran out): bake the AO
            wasPlaying = model.Playing;
            if (model.Version != drawnVersion) Redraw();
            UpdatePlayhead();
        }

        void RelayoutIfWidthChanged()
        {
            if (bar == null) return;
            float w = bar.resolvedStyle.width;
            if (float.IsNaN(w) || w <= 1f || Mathf.Abs(w - builtWidth) < 0.5f) return;
            builtWidth = w;
            model.SetWidth(w);
            BuildBarContents();
            FitCaption();
        }

        // ================================================================== building (structure only; look is in the USS)
        void EnsureUi()
        {
            if (root != null) return;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 11; // over the HUD (10) and under the person card and the moments (12): Who opens the card over this panel
            var uss = Resources.Load<StyleSheet>("GameUI/TimeLapse");
            if (uss != null) doc.rootVisualElement.styleSheets.Add(uss);
            else Debug.LogWarning("[TimeLapseScreenView] Resources/GameUI/TimeLapse.uss is missing: the screen draws unstyled");

            root = new VisualElement { name = "tl-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            root.AddToClassList("tl-root");
            root.style.display = DisplayStyle.None;
            doc.rootVisualElement.Add(root);

            // over the world: the era card; it takes no press, the world stays draggable
            eraCard = new GlassCaption { name = "tl-era-card" };
            eraCard.style.display = DisplayStyle.None;
            root.Add(eraCard);

            panel = new GlassSheet(SheetHeight.Content) { name = "tl-panel" };
            root.Add(panel);
            BuildBar();
            BuildCaption();
            BuildControls();
        }

        void BuildBar()
        {
            bar = new VisualElement { name = "tl-bar" };
            bar.AddToClassList("tl-bar");
            bandsLayer = new VisualElement { name = "tl-bands", pickingMode = PickingMode.Ignore };
            bandsLayer.AddToClassList("tl-layer");
            pinsLayer = new VisualElement { name = "tl-pins", pickingMode = PickingMode.Ignore };
            pinsLayer.AddToClassList("tl-layer");
            playhead = new VisualElement { name = "tl-playhead", pickingMode = PickingMode.Ignore };
            playhead.AddToClassList("tl-playhead");
            var handle = new VisualElement { pickingMode = PickingMode.Ignore };
            handle.AddToClassList("tl-handle");
            playhead.Add(handle);
            yearLabel = new Label { name = "tl-year", pickingMode = PickingMode.Ignore };
            yearLabel.AddToClassList("tl-year");
            bar.Add(bandsLayer); bar.Add(pinsLayer); bar.Add(playhead); bar.Add(yearLabel);
            bar.RegisterCallback<PointerDownEvent>(e => { if (model == null) return; bar.CapturePointer(e.pointerId); model.PointerDown(e.localPosition.x); e.StopPropagation(); });
            bar.RegisterCallback<PointerMoveEvent>(e => { if (model != null && bar.HasPointerCapture(e.pointerId)) model.PointerMove(e.localPosition.x); });
            bar.RegisterCallback<PointerUpEvent>(e => { if (model != null && bar.HasPointerCapture(e.pointerId)) { bar.ReleasePointer(e.pointerId); model.PointerUp(e.localPosition.x); } });
            bar.RegisterCallback<PointerCancelEvent>(e => { if (model != null && bar.HasPointerCapture(e.pointerId)) { bar.ReleasePointer(e.pointerId); model.PointerCancel(); } });
            panel.Add(bar);

            emptyNote = new VisualElement { name = "tl-empty", pickingMode = PickingMode.Ignore };
            var empty = new Label("The first year is still going. Come back when the town has a past.") { pickingMode = PickingMode.Ignore };
            empty.AddToClassList("type-body");
            emptyNote.Add(empty);
            emptyNote.AddToClassList("tl-empty");
            emptyNote.style.display = DisplayStyle.None;
            panel.Add(emptyNote);
        }

        void BuildCaption()
        {
            captionCard = new VisualElement { name = "tl-caption" };
            captionCard.AddToClassList("tl-caption");
            capPortrait = new GlassPortrait("") { name = "tl-cap-portrait" };
            capPortrait.AddToClassList("tl-cap-portrait");
            captionCard.Add(capPortrait);
            captionScroll = new GlassScroll { LinePitch = CaptionPitch };
            captionScroll.AddToClassList("tl-caption-scroll");
            captionWords = new GlassCaption { InPanel = true };
            captionText = captionWords.Line;
            captionText.name = "tl-caption-text";
            captionText.enableRichText = true;
            captionText.AddToClassList("tl-caption-text");
            captionScroll.Add(captionWords);
            captionCard.Add(captionScroll);
            captionCard.style.display = DisplayStyle.None;
            panel.Add(captionCard);
        }

        void BuildControls()
        {
            controls = Row("tl-controls");
            play = Icon("tl-play", "play", () => model?.TogglePlay(), controls, UiRole.Tap);
            speed = Compact("tl-speed", "4 yr/s", () => model?.CycleSpeed(), controls);
            now = Compact("tl-now", "Now", () => model?.GoNow(), controls);
            Spacer(controls);
            var book = Icon("tl-book", "book", OnBook, controls, UiRole.Open);
            book.Add(ribbon = new GlassRibbon { name = "tl-book-ribbon" });
            ribbon.AddToClassList("tl-book-ribbon");
            Icon("tl-snapshot", "photo", OnSnapshot, controls, UiRole.Tap);
            Icon("tl-close", "close", Close, controls, UiRole.Close).AddToClassList("tl-last");
            panel.Add(controls);

            capRow = Row("tl-cap-row");
            capRow.style.display = DisplayStyle.None;
            capPrev = Icon("tl-cap-prev", "back", () => model?.NextInGroup(-1), capRow, UiRole.Tap);
            capNext = Compact("tl-cap-next", "Next", () => model?.NextInGroup(1), capRow);
            capPerson = Compact("tl-cap-person", "Who", OnOpenPerson, capRow);
            capDots = new GlassPageDots { name = "tl-cap-dots" };
            capRow.Add(capDots);
            Spacer(capRow);
            Icon("tl-cap-close", "close", () => model?.DismissCaption(), capRow, UiRole.Close).AddToClassList("tl-last");
            panel.Add(capRow);
        }

        static VisualElement Row(string cls)
        {
            var r = new VisualElement();
            r.AddToClassList("tl-row");
            r.AddToClassList(cls);
            return r;
        }

        static void Spacer(VisualElement row)
        {
            var s = new VisualElement { pickingMode = PickingMode.Ignore };
            s.AddToClassList("tl-spacer");
            row.Add(s);
        }

        static GlassButton Icon(string name, string icon, System.Action tapped, VisualElement row, UiRole role)
        {
            var b = new GlassButton("", tapped, ButtonKind.Secondary, role) { name = name, Icon = icon };
            b.AddToClassList("tl-icon");
            row.Add(b);
            return b;
        }

        static GlassButton Compact(string name, string text, System.Action tapped, VisualElement row)
        {
            var b = new GlassButton(text, tapped) { name = name, Compact = true };
            row.Add(b);
            return b;
        }

        /// <summary>Bands, era names and pin marks for the current width. Positions come from the model's layout.</summary>
        void BuildBarContents()
        {
            bandsLayer.Clear(); pinsLayer.Clear();
            var lay = model.Layout;
            int i = 0;
            foreach (var b in lay.Bands)
            {
                var e = new VisualElement { name = "tl-band-" + i++, pickingMode = PickingMode.Ignore };
                e.AddToClassList("tl-band");
                e.AddToClassList("tl-band--" + b.era.ToString().ToLowerInvariant());
                e.style.left = b.x0; e.style.width = Mathf.Max(0f, b.x1 - b.x0);
                if (b.showLabel)
                {
                    var l = new Label(b.era.ToString()) { pickingMode = PickingMode.Ignore };
                    l.AddToClassList("type-note");
                    l.AddToClassList("tl-band-label");
                    e.Add(l);
                }
                bandsLayer.Add(e);
            }
            i = 0;
            foreach (var g in lay.Groups)
            {
                var p = new VisualElement { name = "tl-pin-" + i++, pickingMode = PickingMode.Ignore };
                p.AddToClassList("tl-pin");
                p.EnableInClassList("tl-pin--group", g.IsGroup);
                p.style.left = g.x;
                pinsLayer.Add(p);
            }
            UpdatePlayhead();
        }

        void UpdatePlayhead()
        {
            if (model == null || playhead == null) return;
            float x = model.PlayheadX;
            playhead.style.left = x;
            float w = yearLabel.resolvedStyle.width, room = bar.resolvedStyle.width;
            if (float.IsNaN(w) || float.IsNaN(room)) return;
            yearLabel.style.left = Mathf.Clamp(x, 0f, Mathf.Max(0f, room - w)); // the tag starts at the playhead and stops at the edge
        }

        // ================================================================== drawing the model
        void Redraw()
        {
            drawnVersion = model.Version;
            yearLabel.text = model.YearLabel + " · " + model.EraName;
            play.Icon = model.Playing ? "pause" : "play";
            speed.Say(model.SpeedLabel);
            now.Available = model.NowEnabled;
            ribbon.style.display = BookRibbon ? DisplayStyle.Flex : DisplayStyle.None;
            emptyNote.style.display = model.HasHistory ? DisplayStyle.None : DisplayStyle.Flex;
            bar.style.display = model.HasHistory ? DisplayStyle.Flex : DisplayStyle.None;
            var cap = model.Caption;
            captionCard.style.display = cap != null ? DisplayStyle.Flex : DisplayStyle.None;
            bool capControls = cap != null && cap.secondsLeft < 0f; // a story the player stopped on; one the playhead crossed goes by itself
            capRow.style.display = capControls ? DisplayStyle.Flex : DisplayStyle.None;
            controls.style.display = capControls ? DisplayStyle.None : DisplayStyle.Flex;
            if (cap != null)
            {
                captionText.text = GlassType.Lines(cap.Text, CaptionPt, CaptionRatio);
                bool grouped = cap.groupCount > 1;
                capPrev.style.display = capNext.style.display = grouped ? DisplayStyle.Flex : DisplayStyle.None;
                capDots.style.display = grouped && cap.groupCount <= MaxDots ? DisplayStyle.Flex : DisplayStyle.None;
                if (grouped) capDots.Set(cap.groupCount, cap.groupIndex);
                bool person = cap.CanOpenPerson && PersonCardView.Instance != null;
                capPerson.style.display = person ? DisplayStyle.Flex : DisplayStyle.None;
                capPortrait.style.display = person ? DisplayStyle.Flex : DisplayStyle.None;
                if (person) capPortrait.Person = view.Lapse.Sim.FindNotable(cap.pin.notableId)?.name;
                FitCaption();
            }
            var ec = model.EraCard;
            eraCard.style.display = ec != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (ec != null) eraCard.Set(ec.era.ToString(), ec.title);
        }

        /// <summary>The panel takes its height from the caption, and a sheet can't measure text for a scroll inside it, so the lines are
        /// counted here: as many as the story has, up to the limit, then it scrolls.</summary>
        void FitCaption()
        {
            var cap = model?.Caption;
            if (cap == null) return;
            float w = bar.resolvedStyle.width;
            if (float.IsNaN(w) || w <= 1f) w = GameUiPanel.ReferenceWidth - 2f * GlassSheet.SidePad;
            if (capPortrait.style.display != DisplayStyle.None) w -= PortraitRoom;
            w -= ScrollRoom;
            float h = captionText.MeasureTextSize(captionText.text, w, VisualElement.MeasureMode.AtMost, 0f, VisualElement.MeasureMode.Undefined).y;
            float most = (tight ? ShortCaptionLines : CaptionLines) * CaptionPitch;
            bool fits = h <= most + CaptionPitch * 0.4f; // a text's last line runs a little past its pitch
            float room = fits ? h + 2f : most; // past the text: layout rounds this, and a pitch over the room would snap the view to nothing
            captionScroll.LinePitch = fits ? h : CaptionPitch; // the text whole, or snapped to whole lines
            captionScroll.style.height = room;
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (panel == null) return;
            var i = GameUiPanel.SafeInsets(lastSafe, Screen.width, Screen.height);
            tight = GlassSheet.IsShort(Screen.width, Screen.height);
            panel.SetInsets(i, tight ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
            root.EnableInClassList("tl--short", tight);
            eraCard.style.top = Mathf.Max(EraCardTop, i.y + EraCardUnderNotch);
        }

        // ================================================================== actions
        void OnOpenPerson()
        {
            var cap = model?.Caption;
            if (cap == null || !cap.CanOpenPerson || PersonCardView.Instance == null) return;
            PersonCardView.Instance.Open(cap.pin.notableId, false);
        }

        void OnBook()
        {
            if (BookHandler != null) BookHandler(view.Lapse.Sim);
            else GameHud.Instance?.Toast("The book isn't written yet.", 3f);
        }

        void OnSnapshot()
        {
            if (model == null || capturing) return;
            var data = model.TakeSnapshot();
            capturing = true;
            StartCoroutine(TimeLapseSnapshot.Take(data, path =>
            {
                capturing = false;
                if (path == null) { GameHud.Instance?.Toast("Could not take the picture", 3f); return; }
                if (ShareHandler != null) ShareHandler(path); else GameHud.Instance?.Toast("Saved: " + data.fileName, 3f);
            }));
        }

        // ================================================================== for tests and the tester
        /// <summary>The screen point (pixels, y up) of an x in the bar's own coordinates (what the model's layout uses), at the
        /// bar's vertical middle. Touch here to tap a marker or drag the playhead.</summary>
        public Vector2 ScreenPointAtBarX(float x)
        {
            var wb = bar.worldBound;                          // panel space, y down
            float scale = Screen.width / bar.panel.visualTree.worldBound.width;
            return new Vector2((wb.x + x) * scale, Screen.height - wb.center.y * scale);
        }
    }
}
