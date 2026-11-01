using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>How tall a sheet stands (spec §2). Only screens that don't talk about the world go past 30 %.</summary>
    public enum SheetHeight
    {
        /// <summary>30 %, the rule.</summary>
        Standard,
        /// <summary>33 %: setting picker.</summary>
        Picker,
        /// <summary>35 %: licences.</summary>
        Licences,
        /// <summary>59 %: settings, feedback.</summary>
        Tall,
        /// <summary>Its content's height, up to 80 %: the crowd card, which needs less than 30 %.</summary>
        Content,
    }

    /// <summary>
    /// The one bottom sheet (<c>GameHud.Sheet</c>, <c>cards-sheet</c>, <c>recall-sheet</c>, <c>shelf-sheet</c>, <c>fb-sheet</c> and the
    /// naming sheet are all this one). Sits on the bottom edge, slides by translate, runs under the home bar with
    /// its content kept above it. Add children to it and they land in the content box. Normally inside a <see cref="GlassScrim"/>.
    /// </summary>
    [UxmlElement]
    public partial class GlassSheet : VisualElement
    {
        /// <summary>Sides and bottom of the content box, panel px: 16 pt sides, 30 pt under it.</summary>
        public const float SidePad = 44f, BottomPad = 82.5f;
        /// <summary>Bottom padding on a short phone (spec §2): 14 pt, under 700 pt tall.</summary>
        public const float ShortBottomPad = 38.5f;
        /// <summary>Above the content: 12 pt plus the hairline.</summary>
        const float TopPad = 35.75f;
        /// <summary>700 pt tall on a 375 pt wide phone. The panel only knows the screen's shape, and the shape is what runs out of room.</summary>
        public const float ShortAspect = 700f / 375f;

        public static bool IsShort(float width, float height) => height / Mathf.Max(1f, width) < ShortAspect;

        readonly VisualElement content;
        Vector4 insets;
        readonly System.Collections.Generic.List<VisualElement> stopped = new();
        public FrostSurface Surface { get; }
        public bool IsOpen { get; private set; }
        public override VisualElement contentContainer => content ?? this;

        public GlassSheet() : this(SheetHeight.Standard) { }

        public GlassSheet(SheetHeight height)
        {
            AddToClassList("kg-sheet");
            AddToClassList(GlassMotion.SlideClass);
            AddToClassList(HeightClass(height));
            var shadow = new VisualElement { name = "kg-shadow", pickingMode = PickingMode.Ignore };
            shadow.AddToClassList("kg-sheet-shadow");
            hierarchy.Add(shadow);
            Surface = new FrostSurface();
            hierarchy.Add(Surface);
            content = new VisualElement { name = "kg-sheet-content" };
            content.AddToClassList("kg-sheet-content");
            hierarchy.Add(content);
            RegisterCallback<PointerDownEvent>(e => { tapSpot = e.position; tapAt = Time.realtimeSinceStartup; }, TrickleDown.TrickleDown); // the press: a button holds the lift for itself
        }

        /// <summary>The last press on the sheet: a Close soon after came from that tap, and its spot gets a <see cref="GlassTapGuard"/>.</summary>
        Vector2 tapSpot;
        float tapAt = -1f;
        const float ClosedByTapSeconds = 1f;

        public static string HeightClass(SheetHeight h) => h switch
        {
            SheetHeight.Picker => "kg-sheet--33",
            SheetHeight.Licences => "kg-sheet--35",
            SheetHeight.Tall => "kg-sheet--59",
            SheetHeight.Content => "kg-sheet--content",
            _ => "kg-sheet--30",
        };

        /// <summary>Safe-area insets (left, top, right, bottom, panel px): the sheet runs under them, its content doesn't.</summary>
        public void SetInsets(Vector4 i, float bottomPad = BottomPad)
        {
            insets = i;
            content.style.paddingLeft = SidePad + i.x;
            content.style.paddingRight = SidePad + i.z;
            content.style.paddingBottom = Mathf.Max(bottomPad, i.w);
        }

        public Vector4 Insets => insets;

        /// <summary>Full height over the box, its content under the safe area: the one sheet a short phone can't fit (feedback, spec §2).</summary>
        public void SetFull(bool on)
        {
            EnableInClassList("kg-sheet--full", on);
            content.style.paddingTop = on ? insets.y + TopPad : StyleKeyword.Null;
        }

        public void Open(Action done = null)
        {
            IsOpen = true;
            foreach (var e in stopped) e.pickingMode = PickingMode.Position;
            stopped.Clear();
            GlassMotion.Show(this, GlassMotion.SlideOff, done, GlassMotion.SlideMs);
            Surface.Show();
            Surface.Follow(GlassMotion.SlideMs);
        }

        public void Close(Action done = null)
        {
            IsOpen = false;
            // A leaving sheet takes no taps: what it still covers is free at once. Picking doesn't inherit, so every piece stops.
            this.Query<VisualElement>().Where(e => e.pickingMode == PickingMode.Position).ForEach(stopped.Add);
            foreach (var e in stopped) e.pickingMode = PickingMode.Ignore;
            if (tapAt >= 0f && Time.realtimeSinceStartup - tapAt < ClosedByTapSeconds) GlassTapGuard.Arm(this, tapSpot);
            tapAt = -1f;
            Surface.Follow(GlassMotion.SlideMs);
            GlassMotion.Hide(this, GlassMotion.SlideOff, GlassMotion.SlideMs, () => { Surface.Hide(); done?.Invoke(); });
        }

        /// <summary>The sheet's head: a title and an icon Close, or whatever else a screen puts there.</summary>
        public static VisualElement Head(string title, string closeName, Action close)
        {
            var head = new VisualElement();
            head.AddToClassList("kg-sheet-head");
            var t = new Label(title) { name = "kg-sheet-title" };
            t.AddToClassList("type-sheet");
            t.AddToClassList("kg-sheet-title");
            head.Add(t);
            if (close != null) head.Add(new GlassButton("", close, ButtonKind.Icon, UiRole.Close) { name = closeName, Icon = "close" });
            return head;
        }
    }

    /// <summary>
    /// The peek (spec §1, lanes): a small frosted card over the tools, in the lane the pin leaves while it's up. Its own surface, because it
    /// floats on all four sides. Slides like a sheet. Add children to it and they sit on the frost.
    /// </summary>
    [UxmlElement]
    public partial class GlassPeek : VisualElement
    {
        readonly VisualElement content;
        public FrostSurface Surface { get; }
        public bool IsOpen { get; private set; }
        public override VisualElement contentContainer => content ?? this;

        public GlassPeek()
        {
            AddToClassList("kg-peek");
            AddToClassList(GlassMotion.SlideClass);
            var shadow = new VisualElement { pickingMode = PickingMode.Ignore };
            shadow.AddToClassList("kg-sheet-shadow");
            shadow.AddToClassList("kg-peek-shadow");
            hierarchy.Add(shadow);
            Surface = new FrostSurface();
            Surface.AddToClassList("kg-surface--peek");
            hierarchy.Add(Surface);
            content = new VisualElement { pickingMode = PickingMode.Ignore };
            content.AddToClassList("kg-peek-content");
            hierarchy.Add(content);
        }

        public bool Inline { set => EnableInClassList("kg-peek--inline", value); }

        public void Open()
        {
            IsOpen = true;
            pickingMode = PickingMode.Position;
            GlassMotion.Show(this, GlassMotion.SlideOff, null, GlassMotion.SlideMs);
            Surface.Show();
            Surface.Follow(GlassMotion.SlideMs);
        }

        public void Close()
        {
            IsOpen = false;
            pickingMode = PickingMode.Ignore; // a leaving peek takes no taps
            Surface.Follow(GlassMotion.SlideMs);
            GlassMotion.Hide(this, GlassMotion.SlideOff, GlassMotion.SlideMs, Surface.Hide);
        }
    }

    /// <summary>
    /// What sits behind a sheet: a clear full-screen element that takes the tap outside it. It dims only behind a destructive
    /// confirm (<see cref="Dim"/>). Nothing else dims, because panels never hide their subject.
    /// </summary>
    public class GlassScrim : VisualElement
    {
        public GlassSheet Sheet { get; }
        public event Action TappedOutside;

        public GlassScrim(string id, GlassSheet sheet)
        {
            name = id + "-scrim";
            Sheet = sheet;
            sheet.name = id + "-sheet";
            AddToClassList("kg-scrim");
            style.display = DisplayStyle.None;
            Add(sheet);
            sheet.style.display = DisplayStyle.None;
            RegisterCallback<PointerUpEvent>(e => { if (e.target == this) TappedOutside?.Invoke(); });
        }

        public bool Dim { set => EnableInClassList("kg-scrim--dim", value); }

        public void Open()
        {
            pickingMode = PickingMode.Position;
            style.display = DisplayStyle.Flex;
            Sheet.Open();
        }

        public void Close()
        {
            pickingMode = PickingMode.Ignore; // the world is free as soon as the sheet starts to leave
            Sheet.Close(() => { if (!Sheet.IsOpen) style.display = DisplayStyle.None; });
        }
    }

    /// <summary>
    /// Scrolling inside a sheet: the view's height snaps to whole lines so no line is ever cut (questions 3), with a thin bar that
    /// shows there's more, and with <see cref="SnapToRows"/> a release settles the text on a whole line too. Set
    /// <see cref="LinePitch"/> to the content's line height in panel px.
    /// </summary>
    [UxmlElement]
    public partial class GlassScroll : VisualElement
    {
        readonly ScrollView view;
        readonly VisualElement bar;
        public float LinePitch { get; set; } = GlassType.BodyPitch;
        /// <summary>Content of mixed heights, where no pitch fits: the view ends under the last element of this class that fits whole.</summary>
        public const string RowClass = "kg-scroll-row";
        public bool SnapToRows
        {
            get => snapToRows;
            set { snapToRows = value; view.scrollDecelerationRate = value ? 0f : DefaultDeceleration; } // it stops where it's let go, or the settle would fight the glide
        }
        bool snapToRows;
        const float DefaultDeceleration = 0.135f;
        /// <summary>Rows are paragraphs: the view may end inside one, on a whole line of it, so room isn't left empty. A row's own pitch is
        /// its <c>userData</c> (a float), else <see cref="LinePitch"/>.</summary>
        public bool SplitRows { get; set; }
        public ScrollView View => view;
        public override VisualElement contentContainer => view != null ? view.contentContainer : this;

        public GlassScroll()
        {
            AddToClassList("kg-scroll");
            view = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped,
            };
            hierarchy.Add(view);
            bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("kg-scroll-bar");
            hierarchy.Add(bar);
            RegisterCallback<GeometryChangedEvent>(_ => Snap());
            view.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => Snap());
            view.RegisterCallback<GeometryChangedEvent>(_ => Bar()); // the height just set lands a layout later
            view.verticalScroller.valueChanged += _ => Bar();
            // The ScrollView holds the pointer while it drags, so it's the end of that hold that says the finger is off.
            RegisterCallback<PointerDownEvent>(_ => pressedAt = view.scrollOffset.y, TrickleDown.TrickleDown);
            // A slider takes its finger before the view can, or a drag that wanders a little moves the sheet instead of the thumb.
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target is not GlassSlider slider) return;
                slider.Grab(e.pointerId, e.position);
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(_ => Settle(), TrickleDown.TrickleDown);
            view.contentContainer.RegisterCallback<PointerCaptureOutEvent>(_ => Settle());
        }

        float pressedAt = float.NaN;

        /// <summary>The finger is off: the view moves on to the next whole line the way it was going (the stops are every row's lines, and
        /// the very end), so a short drag on a tall row isn't undone. With no drag, the nearest.</summary>
        void Settle()
        {
            if (!SnapToRows) return;
            float all = view.contentContainer.layout.height, shown = view.layout.height;
            if (float.IsNaN(all) || float.IsNaN(shown) || all <= shown + 1f) return;
            float end = all - shown, at = view.scrollOffset.y;
            int way = float.IsNaN(pressedAt) || Mathf.Abs(at - pressedAt) < 1f ? 0 : at > pressedAt ? 1 : -1;
            pressedAt = float.NaN; // the release comes by two routes
            float best = way < 0 ? 0f : end;
            void Stop(float y)
            {
                if ((way > 0 && y < at - 0.5f) || (way < 0 && y > at + 0.5f)) return;
                if (Mathf.Abs(y - at) < Mathf.Abs(best - at)) best = y;
            }
            Stop(0f);
            float top = view.contentContainer.worldBound.yMin;
            view.contentContainer.Query(className: RowClass).ForEach(r =>
            {
                if (r.resolvedStyle.display == DisplayStyle.None) return;
                float pitch = r.userData is float own && own > 0f ? own : LinePitch, bottom = r.worldBound.yMax - top;
                for (float y = r.worldBound.yMin - top; y < bottom - 1f && y < end; y += pitch) Stop(y);
            });
            view.scrollOffset = new Vector2(view.scrollOffset.x, best);
        }

        void Snap()
        {
            float room = layout.height;
            if (float.IsNaN(room) || room <= 0f || LinePitch <= 0f) return;
            float fit = SnapToRows ? LastWholeRow(room) : 0f;
            float lead = SnapToRows ? FirstRowTop() : 0f; // a first row's top margin isn't a line
            view.style.height = fit > 0f ? fit : lead + Mathf.Floor((room - lead) / LinePitch) * LinePitch;
            Bar();
        }

        float FirstRowTop()
        {
            float top = view.contentContainer.worldBound.yMin, first = float.MaxValue;
            view.contentContainer.Query(className: RowClass).ForEach(r =>
            {
                if (r.resolvedStyle.display != DisplayStyle.None) first = Mathf.Min(first, r.worldBound.yMin - top);
            });
            return first == float.MaxValue ? 0f : Mathf.Max(0f, first);
        }

        float LastWholeRow(float room)
        {
            float top = view.contentContainer.worldBound.yMin, fit = 0f, splitTop = float.MaxValue, splitPitch = LinePitch;
            view.contentContainer.Query(className: RowClass).ForEach(r =>
            {
                if (r.resolvedStyle.display == DisplayStyle.None) return;
                float bottom = r.worldBound.yMax - top;
                if (bottom <= room) { if (bottom > fit) fit = bottom; return; }
                float rowTop = r.worldBound.yMin - top;
                if (rowTop >= splitTop) return;
                splitTop = rowTop;
                splitPitch = r.userData is float own ? own : LinePitch;
            });
            float whole = Mathf.Floor((room - splitTop) / splitPitch);
            if (SplitRows && whole >= 1f && splitTop >= fit) fit = splitTop + whole * splitPitch;
            return fit;
        }

        void Bar()
        {
            float all = view.contentContainer.layout.height, shown = view.layout.height;
            bool more = !float.IsNaN(all) && !float.IsNaN(shown) && all > shown + 1f;
            bar.style.display = more ? DisplayStyle.Flex : DisplayStyle.None;
            if (!more) return;
            float share = Mathf.Clamp01(shown / all);
            bar.style.height = Length.Percent(share * 100f);
            float at = Mathf.Clamp01(view.scrollOffset.y / Mathf.Max(1f, all - shown));
            bar.style.top = Length.Percent((1f - share) * at * 100f);
        }
    }
}
