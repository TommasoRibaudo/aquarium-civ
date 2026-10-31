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
    /// The time-lapse screen (#79 phase 2), drawn with UI Toolkit in the plain HUD sheet style. All decisions live in
    /// <see cref="TimeLapseScreenModel"/> (pure, tested with dotnet); this class only draws the model and forwards pointer
    /// events and frame time. It drives <see cref="TimeLapseView"/> through <see cref="TimeLapseView.Show"/> only (the model
    /// ticks the clock and logs telemetry, so <see cref="TimeLapseView.externalTick"/> is on while the screen is open).
    ///
    /// Look and logic are separate on purpose: colours, sizes and fonts are in Resources/GameUI/TimeLapse.uss; the code
    /// below sets only what depends on data (x and width of bands and markers, the playhead). The UI rework restyles the USS,
    /// or replaces <c>Build*</c> and <c>Redraw*</c>, and keeps the model.
    ///
    /// Stable names for tests and the tester: tl-root, tl-panel, tl-year, tl-bar, tl-band-N, tl-pin-N, tl-playhead, tl-play,
    /// tl-speed, tl-now, tl-snapshot, tl-close, tl-caption, tl-caption-text, tl-cap-prev, tl-cap-next, tl-cap-person,
    /// tl-cap-close, tl-era-card, tl-toast, tl-empty.
    ///
    /// The one hook to add elsewhere: the HUD's History button calls <c>TimeLapseScreenView.Ensure().Open()</c>.
    /// </summary>
    [AddComponentMenu("AquariumCiv/Time-lapse Screen")]
    public class TimeLapseScreenView : MonoBehaviour
    {
        /// <summary>Panel units are 3 per dp on a 360 dp phone (1080 reference width); the model's numbers are in dp.</summary>
        const float U = 3f;
        const float BarHeight = 168f;

        public static TimeLapseScreenView Instance { get; private set; }

        /// <summary>Set by whoever can share a file (a native share plugin later). Null: the screen just says it saved.</summary>
        public static System.Action<string> ShareHandler;

        public static TimeLapseScreenView Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TimeLapseScreen");
            return go.AddComponent<TimeLapseScreenView>();
        }

        UIDocument doc;
        VisualElement root, panel, bar, bandsLayer, pinsLayer, playhead, captionCard, eraCard, emptyNote;
        Label yearLabel, captionText, eraCardText, toast, emptyText;
        Button play, speed, now, snapshot, capPrev, capNext, capPerson;
        TimeLapseView view;
        TimeLapseScreenModel model;
        int drawnVersion = -1;
        float shownPosition = float.NaN;
        float builtWidth = -1f;
        bool wasPlaying;
        Rect lastSafe;
        bool capturing;
        float toastUntil;

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
            root.style.display = DisplayStyle.Flex;
            IsOpen = true;
            drawnVersion = -1; shownPosition = float.NaN; builtWidth = -1f; wasPlaying = false;
            UiInput.Add(IsOverScreen);
            ApplySafeArea();
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
            if (root != null) root.style.display = DisplayStyle.None;
            if (view != null) { view.externalTick = false; view.Close(); }
            model = null;
        }

        bool IsOverScreen(Vector2 screenPos) => IsOpen && root != null && GameUiPanel.Over(root, screenPos);

        /// <summary>The model's numbers are in dp; the panel is in 1080-wide units (3 per dp).</summary>
        static TimelineOptions ScaledOptions()
        {
            var o = new TimelineOptions();
            o.padLeft *= U; o.padRight *= U; o.minPinGap *= U; o.pinHitRadius *= U; o.tapSlop *= U;
            o.pinSnap *= U; o.nowSnap *= U; o.eraSnap *= U; o.bandLabelMinWidth *= U;
            return o;
        }

        // ================================================================== frame
        void Update()
        {
            if (!IsOpen || model == null) return;
            if (Screen.safeArea != lastSafe) ApplySafeArea();
            RelayoutIfWidthChanged();
            bool wasPlayingNow = model.Playing;
            model.Tick(Time.unscaledDeltaTime);
            if (float.IsNaN(shownPosition) || shownPosition != model.Position)
            {
                shownPosition = model.Position;
                view.Show(shownPosition, false);
            }
            if (wasPlaying && !model.Playing) view.Settle();     // paused (or ran out): bake the AO
            wasPlaying = model.Playing;
            if (model.Version != drawnVersion) Redraw();
            UpdatePlayhead();
            if (toast != null && toast.style.display != DisplayStyle.None && Time.unscaledTime > toastUntil) toast.style.display = DisplayStyle.None;
        }

        void RelayoutIfWidthChanged()
        {
            if (bar == null) return;
            float w = bar.resolvedStyle.width;
            if (float.IsNaN(w) || w <= 1f || Mathf.Abs(w - builtWidth) < 0.5f) return;
            builtWidth = w;
            model.SetWidth(w);
            BuildBarContents();
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
            root.style.justifyContent = Justify.FlexEnd;
            root.style.display = DisplayStyle.None;
            doc.rootVisualElement.Add(root);

            // over the world (top part): the era card and the toast; nothing here takes a press, the world stays draggable
            eraCard = new VisualElement { name = "tl-era-card", pickingMode = PickingMode.Ignore };
            eraCard.AddToClassList("tl-era-card");
            eraCard.Add(eraCardText = new Label { pickingMode = PickingMode.Ignore });
            eraCard.style.display = DisplayStyle.None;
            root.Add(eraCard);

            toast = new Label { name = "tl-toast", pickingMode = PickingMode.Ignore };
            toast.AddToClassList("tl-toast");
            toast.style.display = DisplayStyle.None;
            root.Add(toast);

            panel = new VisualElement { name = "tl-panel" };
            panel.AddToClassList("sheet");          // the plain HUD sheet (Game.uss)
            panel.AddToClassList("tl-panel");
            root.Add(panel);

            var head = new VisualElement();
            head.AddToClassList("tl-head");
            yearLabel = new Label { name = "tl-year", pickingMode = PickingMode.Ignore };
            yearLabel.AddToClassList("tl-year");
            head.Add(yearLabel);
            snapshot = Btn("tl-snapshot", "Snapshot", OnSnapshot, "tl-btn", "tl-btn--small");
            head.Add(snapshot);
            head.Add(Btn("tl-close", "Close", Close, "sheet-close"));
            panel.Add(head);

            captionCard = new VisualElement { name = "tl-caption" };
            captionCard.AddToClassList("tl-caption");
            captionText = new Label { name = "tl-caption-text", pickingMode = PickingMode.Ignore };
            captionText.AddToClassList("tl-caption-text");
            captionCard.Add(captionText);
            var capRow = new VisualElement();
            capRow.AddToClassList("tl-cap-row");
            capPrev = Btn("tl-cap-prev", "Back", () => model?.NextInGroup(-1), "tl-btn", "tl-btn--small");
            capNext = Btn("tl-cap-next", "Next", () => model?.NextInGroup(1), "tl-btn", "tl-btn--small");
            capPerson = Btn("tl-cap-person", "Who", OnOpenPerson, "tl-btn", "tl-btn--small");
            capRow.Add(capPrev); capRow.Add(capNext); capRow.Add(capPerson);
            capRow.Add(Btn("tl-cap-close", "Done", () => model?.DismissCaption(), "tl-btn", "tl-btn--small"));
            captionCard.Add(capRow);
            captionCard.style.display = DisplayStyle.None;
            panel.Add(captionCard);

            bar = new VisualElement { name = "tl-bar" };
            bar.AddToClassList("tl-bar");
            bar.style.height = BarHeight;
            bandsLayer = new VisualElement { name = "tl-bands", pickingMode = PickingMode.Ignore };
            bandsLayer.AddToClassList("tl-layer");
            pinsLayer = new VisualElement { name = "tl-pins", pickingMode = PickingMode.Ignore };
            pinsLayer.AddToClassList("tl-layer");
            playhead = new VisualElement { name = "tl-playhead", pickingMode = PickingMode.Ignore };
            playhead.AddToClassList("tl-playhead");
            var handle = new VisualElement { pickingMode = PickingMode.Ignore };
            handle.AddToClassList("tl-handle");
            playhead.Add(handle);
            bar.Add(bandsLayer); bar.Add(pinsLayer); bar.Add(playhead);
            bar.RegisterCallback<PointerDownEvent>(e => { if (model == null) return; bar.CapturePointer(e.pointerId); model.PointerDown(e.localPosition.x); e.StopPropagation(); });
            bar.RegisterCallback<PointerMoveEvent>(e => { if (model != null && bar.HasPointerCapture(e.pointerId)) model.PointerMove(e.localPosition.x); });
            bar.RegisterCallback<PointerUpEvent>(e => { if (model != null && bar.HasPointerCapture(e.pointerId)) { bar.ReleasePointer(e.pointerId); model.PointerUp(e.localPosition.x); } });
            bar.RegisterCallback<PointerCancelEvent>(e => { if (model != null && bar.HasPointerCapture(e.pointerId)) { bar.ReleasePointer(e.pointerId); model.PointerCancel(); } });
            panel.Add(bar);

            emptyNote = new VisualElement { name = "tl-empty", pickingMode = PickingMode.Ignore };
            emptyNote.Add(emptyText = new Label("The first year is still going. Come back when the town has a past.") { pickingMode = PickingMode.Ignore });
            emptyNote.AddToClassList("tl-empty");
            emptyNote.style.display = DisplayStyle.None;
            panel.Add(emptyNote);

            var controls = new VisualElement();
            controls.AddToClassList("tl-controls");
            play = Btn("tl-play", "Play", () => model?.TogglePlay(), "hud-btn", "hud-btn--primary", "tl-play");
            speed = Btn("tl-speed", "4 yr/s", () => model?.CycleSpeed(), "tl-btn");
            now = Btn("tl-now", "Now", () => model?.GoNow(), "tl-btn");
            controls.Add(play); controls.Add(speed); controls.Add(now);
            panel.Add(controls);
        }

        static Button Btn(string name, string text, System.Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { name = name, text = text };
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        /// <summary>Bands, era ticks and pin markers for the current width. Positions come from the model's layout.</summary>
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
                if (b.showLabel) { var l = new Label(b.era.ToString()) { pickingMode = PickingMode.Ignore }; l.AddToClassList("tl-band-label"); e.Add(l); }
                bandsLayer.Add(e);
            }
            foreach (float mx in lay.EraMarkers)
            {
                var t = new VisualElement { pickingMode = PickingMode.Ignore };
                t.AddToClassList("tl-era-tick");
                t.style.left = mx - 1.5f;
                bandsLayer.Add(t);
            }
            i = 0;
            foreach (var g in lay.Groups)
            {
                var p = new Label(g.IsGroup ? g.pins.Count.ToString() : "") { name = "tl-pin-" + i++, pickingMode = PickingMode.Ignore };
                p.AddToClassList("tl-pin");
                if (g.IsGroup) p.AddToClassList("tl-pin--group");
                p.style.left = g.x - 30f;       // 60 units wide (tl-pin), centred on the marker
                pinsLayer.Add(p);
            }
            UpdatePlayhead();
        }

        void UpdatePlayhead()
        {
            if (model == null || playhead == null) return;
            playhead.style.left = model.PlayheadX - 3f;
        }

        // ================================================================== drawing the model
        void Redraw()
        {
            drawnVersion = model.Version;
            yearLabel.text = model.HasHistory ? model.YearLabel + " · " + model.EraName : "Year 1";
            play.text = model.PlayLabel;
            speed.text = model.SpeedLabel;
            now.SetEnabled(model.NowEnabled);
            play.SetEnabled(model.HasHistory);
            snapshot.SetEnabled(!capturing);
            emptyNote.style.display = model.HasHistory ? DisplayStyle.None : DisplayStyle.Flex;
            bar.style.display = model.HasHistory ? DisplayStyle.Flex : DisplayStyle.None;
            var cap = model.Caption;
            captionCard.style.display = cap != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (cap != null)
            {
                captionText.text = cap.Text;
                bool grouped = cap.groupCount > 1;
                capPrev.style.display = capNext.style.display = grouped ? DisplayStyle.Flex : DisplayStyle.None;
                if (grouped) capNext.text = "Next " + (cap.groupIndex + 1) + "/" + cap.groupCount;
                capPerson.style.display = cap.CanOpenPerson && PersonCardView.Instance != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var ec = model.EraCard;
            eraCard.style.display = ec != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (ec != null) eraCardText.text = string.IsNullOrEmpty(ec.title) ? ec.era.ToString() : ec.era + " · " + ec.title;
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            if (panel == null) return;
            var i = GameUiPanel.SafeInsets(lastSafe, Screen.width, Screen.height);
            panel.style.paddingBottom = 32 + i.w;
            panel.style.paddingLeft = 44 + i.x;
            panel.style.paddingRight = 44 + i.z;
        }

        // ================================================================== actions
        void OnOpenPerson()
        {
            var cap = model?.Caption;
            if (cap == null || !cap.CanOpenPerson || PersonCardView.Instance == null) return;
            PersonCardView.Instance.Open(cap.pin.notableId, false);
        }

        void OnSnapshot()
        {
            if (model == null || capturing) return;
            var data = model.TakeSnapshot();
            capturing = true;
            snapshot.SetEnabled(false);
            StartCoroutine(TimeLapseSnapshot.Take(data, path =>
            {
                capturing = false;
                if (snapshot != null) snapshot.SetEnabled(true);
                if (path == null) { Say("Could not take the picture"); return; }
                if (ShareHandler != null) ShareHandler(path); else Say("Saved: " + data.fileName);
            }));
        }

        void Say(string text)
        {
            if (toast == null) return;
            toast.text = text;
            toast.style.display = DisplayStyle.Flex;
            toastUntil = Time.unscaledTime + 3f;
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
