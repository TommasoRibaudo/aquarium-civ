using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    public enum ButtonKind { Primary, Secondary, Quiet, Icon, Ghost }

    /// <summary>
    /// A hit area that reaches <see cref="GameUiPanel.MinTarget"/> around a piece drawn smaller. UI Toolkit picks an element only inside
    /// its bounding box, and a Button fires only if the finger lifts inside <c>ContainsPoint</c>, so it takes both: a clear child past
    /// the edges widens the box (<c>.kg-reach</c>), and the element's <c>ContainsPoint</c> calls <see cref="Contains"/>. Don't give it to
    /// pieces that touch their neighbours: those get a clear box of the full size instead, as chips, tabs, toggles and sliders do.
    /// </summary>
    public static class GlassReach
    {
        public const string Class = "kg-reach";

        public static void AddTo(VisualElement e)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.AddToClassList(Class);
            e.hierarchy.Add(r);
        }

        /// <summary>Is <paramref name="local"/> within the drawn box, or the square of <see cref="GameUiPanel.MinTarget"/> around its centre?</summary>
        public static bool Contains(VisualElement e, Vector2 local)
        {
            float w = e.layout.width, h = e.layout.height;
            float dx = Mathf.Max(0f, (GameUiPanel.MinTarget - w) * 0.5f), dy = Mathf.Max(0f, (GameUiPanel.MinTarget - h) * 0.5f);
            return local.x >= -dx && local.x <= w + dx && local.y >= -dy && local.y <= h + dy;
        }
    }

    /// <summary>An icon from the set: 24 grid, one colour, drawn white and tinted in USS. <c>kg-i-*</c> in Glass.uss.</summary>
    [UxmlElement]
    public partial class GlassIcon : VisualElement
    {
        /// <summary>Every icon on the Design System board.</summary>
        public static readonly string[] All =
        {
            "alert", "back", "bell", "book", "channel", "check", "clock", "close", "drop", "eye", "feedback", "gift", "hand", "haptic", "herd", "hill", "lock", "next",
            "now", "pause", "pencil", "person", "photo", "pin", "play", "reset", "ribbon", "scroll", "settings", "share", "shelf", "sound", "star", "tree", "undo",
        };

        string shape;

        [UxmlAttribute]
        public string Shape
        {
            get => shape;
            set
            {
                if (shape != null) RemoveFromClassList("kg-i-" + shape);
                shape = value;
                if (shape != null) AddToClassList("kg-i-" + shape);
            }
        }

        public GlassIcon() { pickingMode = PickingMode.Ignore; AddToClassList("kg-icon"); }
        public GlassIcon(string shape, params string[] classes) : this()
        {
            Shape = shape;
            foreach (var c in classes) AddToClassList(c);
        }
    }

    /// <summary>A person's face, always the same: a moss disc with their initial and a brass ring, until real portraits exist.</summary>
    [UxmlElement]
    public partial class GlassPortrait : Label
    {
        public GlassPortrait() : this("") { }
        public GlassPortrait(string name)
        {
            pickingMode = PickingMode.Ignore;
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-portrait");
            Person = name;
        }

        /// <summary>The person's name; the portrait shows its first letter.</summary>
        public string Person { set => text = string.IsNullOrEmpty(value) ? "" : value.Substring(0, 1).ToUpperInvariant(); }
    }

    /// <summary>
    /// The button: Primary (one per moment), Secondary, Quiet, Icon, each with default, pressed and unavailable. It sounds by role.
    /// Unavailable keeps its text at full strength, with a thin solid edge, a paler fill and a lock, and a tap on it is refused.
    /// Drawn 44 to 46 pt, it takes taps out to <see cref="GameUiPanel.MinTarget"/> (<see cref="GlassReach"/>).
    /// </summary>
    [UxmlElement]
    public partial class GlassButton : Button
    {
        public const string PressedClass = "kg-btn--pressed", OffClass = "kg-btn--off";
        ButtonKind kind;
        bool available = true;
        GlassIcon icon, mark;
        Label measure;

        public UiRole Role { get; set; } = UiRole.Tap;
        /// <summary>What a tap does while available.</summary>
        public event Action Tapped;

        /// <summary>An element with children no longer measures its own text, and every button has its reach, so an invisible twin of
        /// the text holds the width. It's made with the first words: an icon button has none, and an empty twin would still take a line.</summary>
        public override string text
        {
            get => base.text;
            set
            {
                base.text = value;
                if (measure == null && !string.IsNullOrEmpty(value)) Twin();
                else if (measure != null) measure.text = value;
            }
        }

        void Twin()
        {
            measure = new Label(text) { pickingMode = PickingMode.Ignore };
            measure.RemoveFromClassList(Label.ussClassName);
            measure.AddToClassList("kg-btn-measure");
            Add(measure);
        }

        public GlassButton() : this("", null) { }

        public GlassButton(string text, Action tapped = null, ButtonKind kind = ButtonKind.Secondary, UiRole role = UiRole.Tap)
        {
            RemoveFromClassList(ussClassName); // the theme's button look: every state here comes from Glass.uss
            AddToClassList("kg-btn");
            AddToClassList("type-btn");
            this.text = text;
            Role = role;
            Kind = kind;
            if (tapped != null) Tapped += tapped;
            clicked += OnClicked;
            GlassReach.AddTo(this);
        }

        public override bool ContainsPoint(Vector2 local) => GlassReach.Contains(this, local);

        void OnClicked()
        {
            if (!available) { UiSound.Play(UiRole.Refused); return; }
            UiSound.Play(Role);
            Tapped?.Invoke();
        }

        public ButtonKind Kind
        {
            get => kind;
            set
            {
                kind = value;
                EnableInClassList("kg-btn--primary", value == ButtonKind.Primary);
                EnableInClassList("kg-btn--quiet", value == ButtonKind.Quiet);
                EnableInClassList("kg-btn--icon", value == ButtonKind.Icon);
                EnableInClassList("kg-btn--ghost", value == ButtonKind.Ghost);
            }
        }

        /// <summary>Icon-kind buttons show this icon from the set.</summary>
        public string Icon
        {
            get => icon?.Shape;
            set
            {
                if (icon == null) { icon = new GlassIcon(value); Add(icon); }
                else icon.Shape = value;
            }
        }

        public bool Available
        {
            get => available;
            set
            {
                available = value;
                EnableInClassList(OffClass, !value);
                if (!value && this.Q(className: "kg-btn-lock") == null) Mark(new GlassIcon("lock", "kg-icon--small", "kg-btn-lock"));
            }
        }

        /// <summary>Adds a small mark beside the text.</summary>
        public void Mark(VisualElement mark) => Add(mark);

        /// <summary>44 pt, for a button in a row with other things (Pin, Show me).</summary>
        public bool Compact { set => EnableInClassList("kg-btn--compact", value); }

        /// <summary>A small icon before the words, on or off: Pinned has one, Pin hasn't.</summary>
        public void Marked(string shape, bool on)
        {
            EnableInClassList("kg-btn--marked", on);
            if (mark == null) { mark = new GlassIcon(shape, "kg-icon--small", "kg-btn-mark"); Add(mark); }
        }

        /// <summary>Sets the words.</summary>
        public void Say(string words) => text = words;

        /// <summary>Holds the pressed look (the gallery shows the state without a finger).</summary>
        public bool ShowPressed { set => EnableInClassList(PressedClass, value); }
    }

    /// <summary>
    /// A chip: 30 pt tall to the eye, inside a clear parent of <see cref="GameUiPanel.MinTarget"/> that takes the tap, the only route that
    /// keeps a small thing tappable. Slots touch, with no gap between rows. As a choice it holds on or off, with a check mark.
    /// </summary>
    [UxmlElement]
    public partial class GlassChip : Button
    {
        public const string SlotClass = "kg-chip-slot";
        readonly VisualElement face;
        readonly Label word;
        bool on;
        public UiRole Role { get; set; } = UiRole.Tap;
        public event Action Tapped;
        public bool IsChoice { get; }

        public GlassChip() : this("", null) { }

        public GlassChip(string label, Action tapped = null, bool choice = false)
        {
            RemoveFromClassList(ussClassName);
            AddToClassList(SlotClass);
            IsChoice = choice;
            face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("kg-chip");
            if (choice) face.AddToClassList("kg-chip--choice");
            face.Add(new GlassIcon("check", "kg-icon--small", "kg-chip-mark"));
            word = new Label(label) { pickingMode = PickingMode.Ignore };
            word.AddToClassList("type-note");
            face.Add(word);
            Add(face);
            if (choice) Role = UiRole.Toggle;
            if (tapped != null) Tapped += tapped;
            clicked += () =>
            {
                UiSound.Play(Role);
                if (IsChoice) On = !on;
                Tapped?.Invoke();
            };
        }

        public VisualElement Face => face;
        public string Label { get => word.text; set => word.text = value; }

        public bool On
        {
            get => on;
            set { on = value; face.EnableInClassList("kg-chip--on", value); }
        }

        /// <summary>The person it names has died: the words go italic, never faded.</summary>
        public bool Gone { set => face.EnableInClassList("kg-chip--gone", value); }

        public bool ShowPressed { set => EnableInClassList("kg-chip-slot--pressed", value); }
    }

    /// <summary>The pinned person, apart from the tools (D2): a portrait and the name, ellipsis only past 220 pt.</summary>
    [UxmlElement]
    public partial class PinChip : Button
    {
        readonly GlassPortrait portrait;
        readonly Label word;
        public PinChip() : this(null) { }
        public PinChip(Action tapped)
        {
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-pin");
            portrait = new GlassPortrait("") { };
            portrait.AddToClassList("kg-portrait--chip");
            Add(portrait);
            word = new Label("") { pickingMode = PickingMode.Ignore };
            word.AddToClassList("kg-pin-name");
            Add(word);
            clicked += () => { UiSound.Play(UiRole.Tap); tapped?.Invoke(); };
            GlassReach.AddTo(this);
        }

        public override bool ContainsPoint(Vector2 local) => GlassReach.Contains(this, local);

        public string Person
        {
            get => word.text;
            set { word.text = value ?? ""; portrait.Person = value; style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex; }
        }
    }

    /// <summary>Stories wait (D1): a dot and the word, never a number. Moved here from the History button, which only ever opens the time-lapse.</summary>
    [UxmlElement]
    public partial class StoriesChip : Button
    {
        public StoriesChip() : this(null) { }
        public StoriesChip(Action tapped)
        {
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-stories");
            Add(new GlassDot());
            var word = new Label("Stories") { pickingMode = PickingMode.Ignore };
            word.AddToClassList("kg-stories-word");
            Add(word);
            clicked += () => { UiSound.Play(UiRole.Open); tapped?.Invoke(); };
            GlassReach.AddTo(this);
        }

        public override bool ContainsPoint(Vector2 local) => GlassReach.Contains(this, local);
    }

    /// <summary>A finished box waits on the shelf: an icon and the word, in the Stories chip's place and look. Stories wins the slot (question 5).</summary>
    [UxmlElement]
    public partial class ShelfChip : Button
    {
        public ShelfChip() : this(null) { }
        public ShelfChip(Action tapped)
        {
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-stories");
            AddToClassList("shelf-chip");
            Add(new GlassIcon("shelf", "kg-icon--small"));
            var word = new Label("Shelf") { pickingMode = PickingMode.Ignore };
            word.AddToClassList("kg-stories-word");
            Add(word);
            clicked += () => tapped?.Invoke();
            GlassReach.AddTo(this);
        }

        public override bool ContainsPoint(Vector2 local) => GlassReach.Contains(this, local);
    }

    /// <summary>One of a few choices on a sheet (the next box's setting): a title, a line, and a ring and a check when it's the one.</summary>
    [UxmlElement]
    public partial class GlassOption : Button
    {
        bool on;

        public GlassOption() : this("", null) { }

        public GlassOption(string title, string note, Action tapped = null)
        {
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-option");
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.AddToClassList("kg-option-words");
            var head = new Label(title) { pickingMode = PickingMode.Ignore };
            head.AddToClassList("type-head");
            words.Add(head);
            var line = new Label(note ?? "") { pickingMode = PickingMode.Ignore };
            line.AddToClassList("type-sub");
            line.style.display = string.IsNullOrEmpty(note) ? DisplayStyle.None : DisplayStyle.Flex;
            words.Add(line);
            Add(words);
            Add(new GlassIcon("check", "kg-option-mark"));
            clicked += () => { UiSound.Play(UiRole.Toggle); tapped?.Invoke(); };
        }

        public bool On
        {
            get => on;
            set { on = value; EnableInClassList("kg-option--on", value); }
        }
    }

    /// <summary>Tabs; the same control serves as a segment (Music plays). Selected is bold and underlined, not colour alone.</summary>
    [UxmlElement]
    public partial class GlassTabs : VisualElement
    {
        readonly List<Button> tabs = new();
        public int Selected { get; private set; }
        public event Action<int> Changed;
        public GlassTabs() : this(Array.Empty<string>()) { }

        public GlassTabs(params string[] labels)
        {
            AddToClassList("kg-tabs");
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var b = new Button(() => { UiSound.Play(UiRole.Tap); Select(index); }) { text = labels[i], name = "tab-" + labels[i].ToLowerInvariant() };
                b.RemoveFromClassList(Button.ussClassName);
                b.AddToClassList("kg-tab");
                tabs.Add(b);
                Add(b);
            }
            Select(0, false);
        }

        /// <summary>One tab's button, so a screen can give it the name its play tests use.</summary>
        public Button Tab(int index) => tabs[index];

        public void Select(int index, bool tell = true)
        {
            Selected = index;
            for (int i = 0; i < tabs.Count; i++) tabs[i].EnableInClassList("kg-tab--on", i == index);
            if (tell) Changed?.Invoke(index);
        }
    }

    /// <summary>On or off: the word inside, the knob moves. <c>text</c> holds the word for play tests; a label draws it over the track.</summary>
    [UxmlElement]
    public partial class GlassToggle : Button
    {
        bool on;
        readonly Label word;
        public event Action<bool> Changed;
        public GlassToggle() : this(false) { }

        public GlassToggle(bool value, Action<bool> changed = null)
        {
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-toggle");
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("kg-toggle-track");
            Insert(0, track);
            var knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.AddToClassList("kg-toggle-knob");
            Add(knob);
            word = new Label("") { pickingMode = PickingMode.Ignore };
            word.RemoveFromClassList(Label.ussClassName);
            word.AddToClassList("kg-toggle-word");
            Add(word);
            if (changed != null) Changed += changed;
            Show(value);
            clicked += () =>
            {
                UiSound.Play(UiRole.Toggle);
                Show(!on);
                Changed?.Invoke(on);
            };
        }

        public bool On { get => on; set => Show(value); }

        void Show(bool value)
        {
            on = value;
            text = word.text = value ? "On" : "Off";
            EnableInClassList("kg-toggle--on", value);
        }
    }

    /// <summary>A settings row that goes somewhere: an icon, the words, a chevron, all of it the target.</summary>
    [UxmlElement]
    public partial class GlassRowButton : Button
    {
        readonly Label word;
        public GlassRowButton() : this("next", "", null) { }

        public GlassRowButton(string icon, string label, Action tapped, UiRole role = UiRole.Open)
        {
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-row");
            AddToClassList("kg-row--link");
            Add(new GlassIcon(icon, "kg-row-icon"));
            word = new Label(label) { pickingMode = PickingMode.Ignore };
            word.AddToClassList("type-body");
            word.AddToClassList("kg-row-label");
            Add(word);
            Add(new GlassIcon("next", "kg-icon--small"));
            clicked += () => { UiSound.Play(role); tapped?.Invoke(); };
        }

        public string Label { get => word.text; set => word.text = value; }
    }

    /// <summary>A hit strip of <see cref="GameUiPanel.MinTarget"/>, a 4 pt line, a 22 pt ring. Value 0 to 1. A finger anywhere on the strip takes it.</summary>
    [UxmlElement]
    public partial class GlassSlider : VisualElement
    {
        readonly VisualElement fill, thumb;
        float value;
        int pointer = -1;
        public event Action<float> Changed;
        /// <summary>The finger is off. With nobody listening the slider ticks by itself; a listener plays its own sample.</summary>
        public event Action Released;
        public GlassSlider() : this(0f) { }

        public GlassSlider(float value, Action<float> changed = null)
        {
            AddToClassList("kg-slider");
            var line = Part("kg-slider-line");
            fill = Part("kg-slider-fill");
            thumb = Part("kg-slider-thumb");
            if (changed != null) Changed += changed;
            Value = value;
            RegisterCallback<PointerDownEvent>(e => { Grab(e.pointerId, e.position); e.StopPropagation(); });
            // #654: a held slider keeps its moves to itself, so a scroll view round it never reads them as a drag of the sheet
            RegisterCallback<PointerMoveEvent>(e => { if (pointer == e.pointerId) { Drag(e.localPosition.x); e.StopPropagation(); } });
            RegisterCallback<PointerUpEvent>(e => { if (pointer == e.pointerId) { Let(); e.StopPropagation(); } });
        }

        /// <summary>A finger came down on the strip, at <paramref name="at"/> in panel space. <see cref="GlassScroll"/> calls this too, ahead of its view.</summary>
        public void Grab(int id, Vector2 at)
        {
            pointer = id;
            this.CapturePointer(id);
            Drag(this.WorldToLocal(at).x);
        }

        /// <summary>The finger lifted (the slider's own up, or <see cref="GlassScroll"/> handing it on).</summary>
        public void Let()
        {
            this.ReleasePointer(pointer);
            pointer = -1;
            if (Released != null) Released();
            else UiSound.Play(UiRole.ScrubTick);
        }

        VisualElement Part(string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            Add(e);
            return e;
        }

        /// <summary>#654: the finger at <paramref name="at"/> (panel space) while this slider holds it; <see cref="GlassScroll"/> feeds it so its scroll view never sees the move.</summary>
        public void DragAt(Vector2 at) => Drag(this.WorldToLocal(at).x);
        /// <summary>The pointer this slider holds, or -1.</summary>
        public int Holding => pointer;

        void Drag(float x)
        {
            float w = layout.width;
            if (float.IsNaN(w) || w <= 0f) return;
            Value = x / w;
            Changed?.Invoke(value);
        }

        public float Value
        {
            get => value;
            set
            {
                this.value = Mathf.Clamp01(value);
                fill.style.width = Length.Percent(this.value * 100f);
                thumb.style.left = Length.Percent(this.value * 100f);
            }
        }
    }
        }
