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
        readonly Label word;
        GlassIcon icon;

        public GlassTag() : this("") { }

        public GlassTag(string word, TagKind kind = TagKind.Name, string mark = null)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-tag");
            if (kind == TagKind.Pin) { AddToClassList("kg-tag--gold"); mark ??= "pin"; }
            if (kind == TagKind.Tie) AddToClassList("kg-tag--tie");
            this.word = new Label(word) { pickingMode = PickingMode.Ignore };
            this.word.AddToClassList("type-note");
            this.word.AddToClassList("kg-tag-word");
            Add(this.word);
            if (mark != null) Mark = mark;
        }

        public string Word { get => word.text; set => word.text = value; }

        /// <summary>The mark from the icon set, before the word.</summary>
        public string Mark
        {
            get => icon?.Shape;
            set
            {
                if (string.IsNullOrEmpty(value)) { icon?.RemoveFromHierarchy(); icon = null; return; } // t4-fixes: no mark at all
                if (icon == null) { icon = new GlassIcon(value, "kg-icon--small"); Insert(0, icon); }
                else icon.Shape = value;
            }
        }

        /// <summary>Hangs over a point in the world: absolute, its bottom centre on <c>left</c> and <c>top</c>; a tie label is centred on them.</summary>
        public bool InWorld { set => EnableInClassList("kg-tag--world", value); }
    }

    /// <summary>The brass plate: the box's name and its lines.</summary>
    [UxmlElement]
    public partial class GlassPlate : VisualElement
    {
        readonly Label title;
        readonly System.Collections.Generic.List<Label> plateLines = new();
        public GlassPlate() : this("") { }

        /// <summary>The box's name on the plate. Empty, the plate keeps its height: blank is a state, not a missing piece.</summary>
        public string Title { get => title.text; set => title.text = string.IsNullOrEmpty(value) ? "\u00A0" : value; }

        public GlassPlate(string name, params string[] lines)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-plate");
            title = new Label(name) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("type-head");
            Add(title);
            SetLines(lines);
        }

        /// <summary>The lines under the name: the plate is engraved letter by letter, so they change while it's up.</summary>
        public void SetLines(params string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (i == plateLines.Count)
                {
                    var l = new Label { pickingMode = PickingMode.Ignore };
                    l.AddToClassList("type-note");
                    Add(l);
                    plateLines.Add(l);
                }
                plateLines[i].text = string.IsNullOrEmpty(words[i]) ? "\u00A0" : words[i];
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

    /// <summary>One bubble: who and a line, or chatter in symbols. The tail is a rotated square, or for a line in the world a wedge that reaches its
    /// speaker (<see cref="TailTo"/>, #204). <see cref="MessageQueue"/> keeps it to one at a time.</summary>
    [UxmlElement]
    public partial class GlassBubble : VisualElement
    {
        public GlassBubble() : this("", "") { }

        /// <summary>Longer lines become a caption, not a bigger bubble.</summary>
        public const int MaxChars = 48;

        readonly Label whoLabel, lineLabel;
        VisualElement tail;

        public GlassBubble(string who, string line)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-bubble");
            whoLabel = new Label() { pickingMode = PickingMode.Ignore };
            whoLabel.AddToClassList("type-head");
            whoLabel.AddToClassList("kg-bubble-who");
            Add(whoLabel);
            lineLabel = new Label() { pickingMode = PickingMode.Ignore };
            lineLabel.AddToClassList("type-sub");
            lineLabel.AddToClassList("kg-bubble-line");
            Add(lineLabel);
            Tail();
            Set(who, line);
        }

        /// <summary>New words for a bubble that stays on screen: a person's line, with or without their name.</summary>
        public void Set(string who, string line)
        {
            whoLabel.text = who ?? "";
            whoLabel.style.display = string.IsNullOrEmpty(who) ? DisplayStyle.None : DisplayStyle.Flex;
            lineLabel.text = line ?? "";
            lineLabel.style.width = StyleKeyword.Auto;
            schedule.Execute(Balance).ExecuteLater(16); // once the panel has laid the words out at their widest
        }

        /// <summary>The narrowest width that keeps the line count, so the last line isn't one word on its own. A few measures, once a line.</summary>
        void Balance()
        {
            float wide = lineLabel.layout.width;
            if (float.IsNaN(wide) || wide <= 1f || string.IsNullOrEmpty(lineLabel.text)) return;
            float Tall(float w) => lineLabel.MeasureTextSize(lineLabel.text, w, MeasureMode.AtMost, 0f, MeasureMode.Undefined).y;
            float tall = Tall(wide), one = Tall(100000f);
            if (tall < one * 1.5f) return;
            float lo = wide * 0.45f, hi = wide;
            for (int i = 0; i < BalanceSteps; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Tall(mid) > tall + 0.5f) lo = mid; else hi = mid;
            }
            lineLabel.style.width = Mathf.Ceil(hi) + 1f;
        }

        const int BalanceSteps = 7;

        /// <summary>#204: the wedge's tip, from the bubble's top left (panel px), and whether it's drawn.</summary>
        public Vector2 TailTip { get; private set; }
        public bool Reaching => reach != null && reach.style.display == DisplayStyle.Flex;

        VisualElement reach;

        /// <summary>#204: the tail as a wedge from the bubble's nearest edge out to this point, from the bubble's top left (panel px), however far that is.</summary>
        public void TailTo(Vector2 tip)
        {
            if (reach == null)
            {
                reach = new VisualElement { pickingMode = PickingMode.Ignore };
                reach.style.position = Position.Absolute;
                reach.style.left = reach.style.top = reach.style.right = reach.style.bottom = 0;
                reach.generateVisualContent += DrawReach;
                Add(reach);
                tail.style.visibility = Visibility.Hidden; // it stays for its colours
            }
            TailTip = tip;
            reach.style.display = DisplayStyle.Flex;
            reach.MarkDirtyRepaint();
        }

        /// <summary>#204: no tail at all: better none than one pointing at nobody.</summary>
        public void NoTail() { if (reach != null) reach.style.display = DisplayStyle.None; }

        void DrawReach(MeshGenerationContext c)
        {
            var box = reach.contentRect;
            float w = box.width, h = box.height, edge = resolvedStyle.borderLeftWidth;
            if (w <= 0f || h <= 0f) return;
            var t = TailTip - new Vector2(edge, edge);
            float dx = Mathf.Max(-t.x, t.x - w), dy = Mathf.Max(-t.y, t.y - h);
            if (dx <= 0f && dy <= 0f) return; // the speaker is behind the bubble: nothing to reach
            Vector2 a, b;
            if (dy >= dx)
            {
                float x = w > (WedgeCorner + WedgeHalf) * 2f ? Mathf.Clamp(t.x, WedgeCorner + WedgeHalf, w - WedgeCorner - WedgeHalf) : w * 0.5f;
                float y = t.y > h ? h - WedgeSink : WedgeSink;
                a = new Vector2(x - WedgeHalf, y); b = new Vector2(x + WedgeHalf, y);
            }
            else
            {
                float y = h > (WedgeCorner + WedgeHalf) * 2f ? Mathf.Clamp(t.y, WedgeCorner + WedgeHalf, h - WedgeCorner - WedgeHalf) : h * 0.5f;
                float x = t.x > w ? w - WedgeSink : WedgeSink;
                a = new Vector2(x, y - WedgeHalf); b = new Vector2(x, y + WedgeHalf);
            }
            var p = c.painter2D;
            p.fillColor = tail.resolvedStyle.backgroundColor;
            p.BeginPath(); p.MoveTo(a); p.LineTo(t); p.LineTo(b); p.ClosePath(); p.Fill();
            p.strokeColor = tail.resolvedStyle.borderRightColor;
            p.lineWidth = Mathf.Max(1f, edge);
            p.lineJoin = LineJoin.Round;
            p.BeginPath(); p.MoveTo(a); p.LineTo(t); p.LineTo(b); p.Stroke();
        }

        /// <summary>The wedge's half width at the bubble, the rounded corner it keeps off, and how far into the bubble it starts, over the edge line.</summary>
        const float WedgeHalf = 15f, WedgeCorner = 26f, WedgeSink = 3f;

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
            tail = new VisualElement { pickingMode = PickingMode.Ignore };
            tail.AddToClassList("kg-bubble-tail");
            Add(tail);
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

        /// <summary>New words for a caption that stays on screen.</summary>
        public void Set(string text, string by = null)
        {
            line.text = text ?? "";
            who.text = by ?? "";
            who.style.display = string.IsNullOrEmpty(by) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>The line's label, so a screen can give it the name its play tests use.</summary>
        public Label Line => line;
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
        readonly Label label;

        public GlassHint() : this("") { }

        public string Word { get => label.text; set => label.text = value; }

        public GlassHint(string word, string icon = null)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("kg-hint");
            AddToClassList(GlassMotion.FadeClass);
            if (icon != null) Add(new GlassIcon(icon));
            label = new Label(word) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("type-small-head");
            Add(label);
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
        readonly VisualElement words;
        Label title, sub;

        public GlassBanner() : this(null, null) { }

        public GlassBanner(string title, string sub)
        {
            AddToClassList("kg-banner");
            AddToClassList(GlassMotion.SlideClass);
            words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.AddToClassList("kg-banner-words");
            Add(words);
            Set(title, sub);
            End = new VisualElement();
            End.style.flexDirection = FlexDirection.Row;
            End.style.alignItems = Align.Center;
            End.style.flexShrink = 0;
            Add(End);
        }

        public bool Inline { set => EnableInClassList("kg-banner--inline", value); }

        /// <summary>The headline's label, so a screen can give it the name its play tests use.</summary>
        public Label TitleLabel => title;

        /// <summary>The words; an empty line takes no room.</summary>
        public void Set(string headline, string note)
        {
            Line(ref title, headline, "type-head", null);
            Line(ref sub, note, "type-note", "kg-banner-sub");
        }

        void Line(ref Label l, string text, string type, string extra)
        {
            bool on = !string.IsNullOrEmpty(text);
            if (l == null && on)
            {
                l = new Label { pickingMode = PickingMode.Ignore };
                l.AddToClassList(type);
                if (extra != null) l.AddToClassList(extra);
                words.Add(l);
            }
            if (l == null) return;
            l.text = text ?? "";
            l.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }
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

    /// <summary>
    /// A question and two answers on the flat tint: wipe first, the notification. It sits in the toast lane (question 23), so it keeps
    /// off the box's corner, and it holds the toasts as a prompt does. The caller picks the buttons' kinds and order.
    /// </summary>
    [UxmlElement]
    public partial class GlassAsk : VisualElement
    {
        readonly Label line;
        string holdKey;

        public GlassAsk() : this("") { }

        public GlassAsk(string question, params GlassButton[] answers)
        {
            AddToClassList("kg-ask");
            line = new Label(question) { pickingMode = PickingMode.Ignore };
            line.AddToClassList("type-body");
            line.AddToClassList("kg-ask-line");
            Add(line);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("kg-ask-row");
            for (int i = 0; i < answers.Length; i++)
            {
                if (i == answers.Length - 1) answers[i].AddToClassList("kg-ask-last");
                row.Add(answers[i]);
            }
            Add(row);
            RegisterCallback<DetachFromPanelEvent>(_ => Hold(false));
        }

        public string Question { get => line.text; set => line.text = value; }

        public bool Ready
        {
            get => style.display != DisplayStyle.None;
            set { style.display = value ? DisplayStyle.Flex : DisplayStyle.None; Hold(value); }
        }

        public bool Inline { set => EnableInClassList("kg-ask--inline", value); }

        void Hold(bool on)
        {
            holdKey ??= "ask-" + GetHashCode();
            GameUiPanel.Toasts.Hold(holdKey, on);
        }
    }
}
