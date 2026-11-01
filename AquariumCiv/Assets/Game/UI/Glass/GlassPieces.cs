using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>A dot means "something good to read". It never counts.</summary>
    [UxmlElement]
    public partial class GlassDot : VisualElement
    {
        public GlassDot() { pickingMode = PickingMode.Ignore; AddToClassList("kg-dot"); }
    }

    /// <summary>Page dots: where you are in a stack, a position and never a count of what's missed.</summary>
    [UxmlElement]
    public partial class GlassPageDots : VisualElement
    {
        public GlassPageDots() : this(0, 0) { }
        public GlassPageDots(int count, int at)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-dots");
            Set(count, at);
        }

        public void Set(int count, int at)
        {
            Clear();
            for (int i = 0; i < count; i++)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore };
                d.AddToClassList("kg-page-dot");
                d.EnableInClassList("kg-page-dot--on", i == at);
                Add(d);
            }
        }
    }

    /// <summary>Charge pips: filled and open rings.</summary>
    [UxmlElement]
    public partial class GlassPips : VisualElement
    {
        public GlassPips() : this(0, 0) { }
        public GlassPips(int total, int on)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-pips");
            Set(total, on);
        }

        public void Set(int total, int on)
        {
            Clear();
            for (int i = 0; i < total; i++)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore };
                d.AddToClassList("kg-pip");
                d.EnableInClassList("kg-pip--on", i < on);
                Add(d);
            }
        }
    }

    /// <summary>The label on a new thing: 13 pt, the floor (question 10).</summary>
    [UxmlElement]
    public partial class GlassNewTag : Label
    {
        public GlassNewTag() : base("New")
        {
            pickingMode = PickingMode.Ignore;
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-new-tag");
        }
    }

    /// <summary>The book's ribbon: new pages wait.</summary>
    [UxmlElement]
    public partial class GlassRibbon : VisualElement
    {
        public GlassRibbon() { pickingMode = PickingMode.Ignore; AddToClassList("kg-ribbon"); }
    }

    public enum TagKind { Name, Pin, Tie }

    /// <summary>A label in the world: the name tag, the gold pin tag with its mark, a tie with its mark.</summary>
    [UxmlElement]
    public partial class GlassTag : VisualElement
    {
        public GlassTag() : this("") { }

        public GlassTag(string word, TagKind kind = TagKind.Name, string mark = null)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-tag");
            if (kind == TagKind.Pin) { AddToClassList("kg-tag--gold"); mark ??= "pin"; }
            if (kind == TagKind.Tie) AddToClassList("kg-tag--tie");
            if (mark != null) Add(new GlassIcon(mark, "kg-icon--small"));
            var l = new Label(word) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("type-note");
            l.AddToClassList("kg-tag-word");
            Add(l);
        }
    }

    /// <summary>The brass plate: the box's name and its lines.</summary>
    [UxmlElement]
    public partial class GlassPlate : VisualElement
    {
        public GlassPlate() : this("") { }

        public GlassPlate(string name, params string[] lines)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-plate");
            var n = new Label(name) { pickingMode = PickingMode.Ignore };
            n.AddToClassList("type-head");
            Add(n);
            foreach (var line in lines)
            {
                var l = new Label(line) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("type-note");
                Add(l);
            }
        }
    }

    /// <summary>
    /// A card: the content of a sheet, never its own surface. Dots, a head (portrait, headline, a quiet action), body, a sub line
    /// and the actions row. The order of actions is the caller's: Back, Next, Show me, then the one primary at the right.
    /// </summary>
    [UxmlElement]
    public partial class GlassCard : VisualElement
    {
        public GlassPageDots Dots { get; }
        public GlassPortrait Portrait { get; }
        public Label Title { get; }
        public Label Body { get; }
        public Label Sub { get; }
        public VisualElement Head { get; }
        public VisualElement Actions { get; }

        public GlassCard()
        {
            AddToClassList("kg-card");
            Dots = new GlassPageDots();
            hierarchy.Add(Dots);
            Head = Box("kg-card-head");
            Portrait = new GlassPortrait("");
            Head.Add(Portrait);
            Title = Text("kg-card-title", "type-sheet");
            Head.Add(Title);
            hierarchy.Add(Head);
            Body = Text("kg-card-body", "type-body");
            Body.enableRichText = true;
            hierarchy.Add(Body);
            Sub = Text("kg-card-sub", "type-sub");
            hierarchy.Add(Sub);
            Actions = Box("kg-card-acts");
            hierarchy.Add(Actions);
        }

        static VisualElement Box(string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            return e;
        }

        static Label Text(params string[] classes)
        {
            var l = new Label("") { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        /// <summary>Fills the words. A null person hides the portrait; a story's headline leads alone.</summary>
        public GlassCard Set(string headline, string body, string sub = null, string person = null)
        {
            Title.text = headline;
            Body.text = GlassType.Lines(body);
            Sub.text = sub ?? "";
            Sub.style.display = string.IsNullOrEmpty(sub) ? DisplayStyle.None : DisplayStyle.Flex;
            Portrait.Person = person;
            Portrait.style.display = person == null ? DisplayStyle.None : DisplayStyle.Flex;
            Title.style.marginLeft = person == null ? new StyleLength(0f) : new StyleLength(StyleKeyword.Null);
            return this;
        }
    }

    /// <summary>One bubble: who and a line, or chatter in symbols. The tail is a rotated square. <see cref="MessageQueue"/> keeps it to one at a time.</summary>
    [UxmlElement]
    public partial class GlassBubble : VisualElement
    {
        public GlassBubble() : this("", "") { }

        /// <summary>Longer lines become a caption, not a bigger bubble.</summary>
        public const int MaxChars = 48;

        public GlassBubble(string who, string line)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-bubble");
            if (!string.IsNullOrEmpty(who))
            {
                var w = new Label(who) { pickingMode = PickingMode.Ignore };
                w.AddToClassList("type-head");
                w.AddToClassList("kg-bubble-who");
                Add(w);
            }
            var l = new Label(line) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("type-sub");
            l.AddToClassList("kg-bubble-line");
            Add(l);
            Tail();
        }

        /// <summary>Chatter: symbols, no words.</summary>
        public GlassBubble(params string[] symbols)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-bubble");
            AddToClassList("kg-bubble--chatter");
            foreach (var s in symbols) Add(new GlassIcon(s, "kg-icon--small"));
            Tail();
        }

        void Tail()
        {
            var t = new VisualElement { pickingMode = PickingMode.Ignore };
            t.AddToClassList("kg-bubble-tail");
            Add(t);
        }

        public static bool TooLong(string line) => line != null && line.Length > MaxChars;
    }

    /// <summary>The toast plate. Lives in the toast lane; <see cref="GameUiPanel.Toasts"/> decides what it says and when.</summary>
    [UxmlElement]
    public partial class GlassToast : Label
    {
        public GlassToast() : this("") { }
        public GlassToast(string text) : base(text)
        {
            pickingMode = PickingMode.Ignore;
            RemoveFromClassList(ussClassName);
            AddToClassList("type-sub");
            AddToClassList("kg-toast");
            AddToClassList(GlassMotion.FadeClass);
        }

        public bool Inline { set => EnableInClassList("kg-toast--inline", value); }
    }

    /// <summary>The caption plate (and the era card): one line, or a line and who. In panel form it's plain text inside the time-lapse panel.</summary>
    [UxmlElement]
    public partial class GlassCaption : VisualElement
    {
        readonly Label line, who;

        public GlassCaption() : this("") { }

        public GlassCaption(string line, string who = null)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-caption");
            AddToClassList(GlassMotion.CaptionClass);
            this.line = new Label(line) { pickingMode = PickingMode.Ignore };
            this.line.AddToClassList("type-big");
            this.line.AddToClassList("kg-caption-line");
            Add(this.line);
            this.who = new Label(who ?? "") { pickingMode = PickingMode.Ignore };
            this.who.AddToClassList("type-quote");
            this.who.AddToClassList("kg-caption-who");
            this.who.style.display = string.IsNullOrEmpty(who) ? DisplayStyle.None : DisplayStyle.Flex;
            Add(this.who);
        }

        public bool Inline { set => EnableInClassList("kg-caption--inline", value); }
        /// <summary>Plain text, for the time-lapse panel.</summary>
        public bool InPanel { set => EnableInClassList("kg-caption--panel", value); }
        /// <summary>Under the HUD's top row (100 pt) rather than the HUD-less 52.</summary>
        public bool UnderHud { set => EnableInClassList("kg-caption--up", value); }
    }

    /// <summary>A light pill of three words at most. Rests while a reward line shows (the bug in captures/README.md).</summary>
    [UxmlElement]
    public partial class GlassHint : VisualElement
    {
        public const int MaxWords = 3;

        public GlassHint() : this("") { }

        public GlassHint(string word, string icon = null)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-hint");
            AddToClassList(GlassMotion.FadeClass);
            if (icon != null) Add(new GlassIcon(icon));
            var l = new Label(word) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("type-small-head");
            Add(l);
        }

        /// <summary>The ghost finger that sits on the thing itself.</summary>
        public static VisualElement Ghost()
        {
            var g = new VisualElement { pickingMode = PickingMode.Ignore };
            g.AddToClassList("kg-ghost");
            return g;
        }
    }

    /// <summary>The mode's state and its one way out, in the tools' lane: the tool in hand, or a visit.</summary>
    [UxmlElement]
    public partial class GlassBanner : VisualElement
    {
        public VisualElement End { get; }

        public GlassBanner() : this(null, null) { }

        public GlassBanner(string title, string sub)
        {
            AddToClassList("kg-banner");
            AddToClassList(GlassMotion.SlideClass);
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.AddToClassList("kg-banner-words");
            if (!string.IsNullOrEmpty(title))
            {
                var t = new Label(title) { pickingMode = PickingMode.Ignore };
                t.AddToClassList("type-head");
                words.Add(t);
            }
            if (!string.IsNullOrEmpty(sub))
            {
                var s = new Label(sub) { pickingMode = PickingMode.Ignore };
                s.AddToClassList("type-note");
                s.AddToClassList("kg-banner-sub");
                words.Add(s);
            }
            Add(words);
            End = new VisualElement();
            End.style.flexDirection = FlexDirection.Row;
            End.style.alignItems = Align.Center;
            Add(End);
        }

        public bool Inline { set => EnableInClassList("kg-banner--inline", value); }
    }

    /// <summary>
    /// The moment's one primary, in the prompt lane: no timer, no counter, no dim. While it's up toasts wait (rule 3, question 22), so a
    /// prompt and a toast can't talk over each other. The hold lifts when it's hidden or removed.
    /// </summary>
    [UxmlElement]
    public partial class GlassBigPrompt : Button
    {
        public UiRole Role { get; set; } = UiRole.Confirm;
        public event Action Tapped;
        string holdKey;

        public GlassBigPrompt() : this("") { }

        public GlassBigPrompt(string text, Action tapped = null)
        {
            RemoveFromClassList(ussClassName);
            AddToClassList("kg-big-prompt");
            AddToClassList("type-big");
            this.text = text;
            if (tapped != null) Tapped += tapped;
            clicked += () => { UiSound.Play(Role); Tapped?.Invoke(); };
            RegisterCallback<DetachFromPanelEvent>(_ => Hold(false));
        }

        public bool Ready
        {
            get => style.display != DisplayStyle.None;
            set { style.display = value ? DisplayStyle.Flex : DisplayStyle.None; Hold(value); }
        }

        public bool Inline { set => EnableInClassList("kg-big-prompt--inline", value); }
        public bool ShowPressed { set => EnableInClassList("kg-big-prompt--pressed", value); }

        void Hold(bool on)
        {
            holdKey ??= "prompt-" + GetHashCode();
            GameUiPanel.Toasts.Hold(holdKey, on);
        }
    }
}
