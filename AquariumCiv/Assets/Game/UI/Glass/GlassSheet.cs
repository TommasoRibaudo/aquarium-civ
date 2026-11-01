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
        /// <summary>Its content's height, up to 80 %. Only for a screen not yet retrofitted (the tray until #167).</summary>
        Content,
    }

    /// <summary>
    /// The one bottom sheet (<c>GameHud.Sheet</c>, <c>cards-sheet</c>, <c>recall-sheet</c>, <c>shelf-sheet</c>, <c>fb-sheet</c> and the
    /// naming sheet all become this as their tickets land). Sits on the bottom edge, slides by translate, runs under the home bar with
    /// its content kept above it. Add children to it and they land in the content box. Normally inside a <see cref="GlassScrim"/>.
    /// </summary>
    [UxmlElement]
    public partial class GlassSheet : VisualElement
    {
        /// <summary>Sides and bottom of the content box, panel px: 16 pt sides, 30 pt under it.</summary>
        public const float SidePad = 44f, BottomPad = 82.5f;

        readonly VisualElement content;
        Vector4 insets;
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
        }

        public static string HeightClass(SheetHeight h) => h switch
        {
            SheetHeight.Picker => "kg-sheet--33",
            SheetHeight.Licences => "kg-sheet--35",
            SheetHeight.Tall => "kg-sheet--59",
            SheetHeight.Content => "kg-sheet--content",
            _ => "kg-sheet--30",
        };

        /// <summary>Safe-area insets (left, top, right, bottom, panel px): the sheet runs under them, its content doesn't.</summary>
        public void SetInsets(Vector4 i)
        {
            insets = i;
            content.style.paddingLeft = SidePad + i.x;
            content.style.paddingRight = SidePad + i.z;
            content.style.paddingBottom = Mathf.Max(BottomPad, i.w);
        }

        public Vector4 Insets => insets;

        public void Open(Action done = null)
        {
            IsOpen = true;
            GlassMotion.Show(this, GlassMotion.SlideOff, done, GlassMotion.SlideMs);
            Surface.Show();
            Surface.Follow(GlassMotion.SlideMs);
        }

        public void Close(Action done = null)
        {
            IsOpen = false;
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
    /// shows there's more. Set <see cref="LinePitch"/> to the content's line height in panel px.
    /// </summary>
    [UxmlElement]
    public partial class GlassScroll : VisualElement
    {
        readonly ScrollView view;
        readonly VisualElement bar;
        public float LinePitch { get; set; } = GlassType.BodyPitch;
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
            view.verticalScroller.valueChanged += _ => Bar();
        }

        void Snap()
        {
            float room = layout.height;
            if (float.IsNaN(room) || room <= 0f || LinePitch <= 0f) return;
            view.style.height = Mathf.Floor(room / LinePitch) * LinePitch;
            Bar();
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
