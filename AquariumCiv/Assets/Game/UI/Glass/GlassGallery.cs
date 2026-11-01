using System;
using System.Collections.Generic;
using AquariumCiv.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// Every component in every state, for the tester (#108). Open it from the debug panel (UI gallery). Each state has a name,
    /// <c>gal-&lt;component&gt;-&lt;state&gt;</c>, listed in <see cref="States"/>. The bar's Frost button flips the one switch, Tank
    /// clears the background so translucent pieces sit over the live box, and the two Sheet buttons open real sheets. It exists only
    /// while open: closing destroys it. Not a game screen, and nothing here ships to players (the debug panel is dev only).
    /// </summary>
    public class GlassGallery : MonoBehaviour
    {
        public static GlassGallery Instance { get; private set; }
        public static string DebugToggle()
        {
            if (Instance != null) { Instance.Close(); return "gallery closed"; }
            new GameObject("GlassGallery").AddComponent<GlassGallery>();
            return "gallery open";
        }

        /// <summary>Names of every state shown, so a test can hold the gallery to the spec's list.</summary>
        public readonly List<string> States = new();

        UIDocument doc;
        VisualElement root, body;
        ScrollView scroll;
        GlassScrim sheet30, sheet59;
        GlassButton frostButton, tankButton;
        readonly Dictionary<string, VisualElement> sections = new();
        Func<Vector2, bool> over;
        public VisualElement Root => root;
        public ScrollView Scroll => scroll;
        public GlassSheet LiveSheet => sheet30.Sheet;

        /// <summary>Brings a section to the top of the view.</summary>
        public void ScrollTo(string section)
        {
            if (sections.TryGetValue(section, out var e)) scroll.scrollOffset = new Vector2(0f, e.layout.y);
        }

        public IEnumerable<string> Sections => sections.Keys;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            over = p => GameUiPanel.Over(root, p);
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 80; // over the game, under the feedback sheet
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Build(doc.rootVisualElement);
            UiInput.Add(over);
        }

        void OnDisable()
        {
            if (Instance != this) return;
            UiInput.Remove(over);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Close()
        {
            enabled = false;
            Destroy(gameObject);
        }

        // ---------- helpers ----------

        T State<T>(VisualElement parent, string component, string state, T e) where T : VisualElement
        {
            string id = $"gal-{component}-{state}";
            e.name = id;
            States.Add(id);
            parent.Add(e);
            return e;
        }

        VisualElement Section(string title)
        {
            var s = new VisualElement { name = "gal-" + title.ToLowerInvariant().Replace(' ', '-') };
            s.AddToClassList("gal-section");
            s.Add(Text(title, "type-sheet", "gal-head"));
            body.Add(s);
            sections[title] = s;
            return s;
        }

        static Label Text(string s, params string[] classes)
        {
            var l = new Label(s) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        static VisualElement Row(VisualElement parent, string caption = null)
        {
            if (caption != null) parent.Add(Text(caption, "type-note", "gal-state"));
            var r = new VisualElement();
            r.AddToClassList("gal-row");
            parent.Add(r);
            return r;
        }

        // ---------- the page ----------

        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "gal-root" };
            root.AddToClassList("gal-root");
            parent.Add(root);

            var bar = new VisualElement { name = "gal-bar" };
            bar.AddToClassList("gal-bar");
            bar.Add(Text("Keeper's Glass", "type-sheet", "gal-bar-title"));
            frostButton = new GlassButton(FrostLabel, ToggleFrost, ButtonKind.Secondary, UiRole.Toggle) { name = "gal-frost" };
            bar.Add(frostButton);
            tankButton = new GlassButton("Tank", ToggleTank, ButtonKind.Secondary, UiRole.Toggle) { name = "gal-tank" };
            bar.Add(tankButton);
            bar.Add(new GlassButton("Close", Close, ButtonKind.Primary, UiRole.Close) { name = "gal-close" });
            root.Add(bar);

            scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "gal-scroll",
                touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped,
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            };
            scroll.AddToClassList("gal-scroll");
            root.Add(scroll);
            body = scroll.contentContainer;
            body.style.width = Length.Percent(100f);

            Tokens();
            Buttons();
            Chips();
            Sheets();
            Cards();
            TabsToggles();
            Bubbles();
            Labels();
            Badges();
            Plates();
            Motion();

            sheet30 = LiveSheetOf("gal-live-30", SheetHeight.Standard, "A frosted sheet", "30 % of the screen, over the live tank, one at a time. Close it and the copy of the frame stops.");
            sheet59 = LiveSheetOf("gal-live-59", SheetHeight.Tall, "Settings height", "59 %, for sheets that don't talk about the world.");
        }

        GlassScrim LiveSheetOf(string id, SheetHeight height, string title, string text)
        {
            var sheet = new GlassSheet(height);
            var scrim = new GlassScrim(id, sheet);
            scrim.TappedOutside += () => scrim.Close();
            sheet.Add(GlassSheet.Head(title, id + "-close", () => scrim.Close()));
            sheet.Add(Text(text, "type-body"));
            sheet.Q<Label>(className: "type-body").style.whiteSpace = WhiteSpace.Normal;
            root.Add(scrim);
            return scrim;
        }

        string FrostLabel => FrostSwitch.On ? "Frost" : "Flat";

        void ToggleFrost()
        {
            FrostSwitch.On = !FrostSwitch.On;
            frostButton.text = FrostLabel;
        }

        void ToggleTank() => root.ToggleInClassList("gal-root--tank");

        /// <summary>Clear background, so pieces sit over the live box.</summary>
        public void ShowTank(bool on) => root.EnableInClassList("gal-root--tank", on);

        public void OpenLive(bool tall = false)
        {
            ShowTank(true);
            (tall ? sheet59 : sheet30).Open();
        }

        public void CloseLive()
        {
            sheet30.Close();
            sheet59.Close();
        }

        void Tokens()
        {
            var s = Section("Tokens");
            var colours = Row(s, "Colour");
            (string, string)[] swatches =
            {
                ("ink", "#3D2A1E"), ("ink-soft", "#4A3324"), ("timber", "#6A4B36"), ("paper", "#F3E9D6"), ("brass", "#B88A3E"), ("edge", "#D3B684"),
                ("brass-light", "#D9B873"), ("primary", "#9E5033"), ("on-primary", "#EFE3CC"), ("moss", "#526E3A"),
            };
            foreach (var (word, hex) in swatches)
            {
                var w = new VisualElement();
                w.AddToClassList("gal-swatch");
                var c = new VisualElement();
                c.AddToClassList("gal-chip");
                ColorUtility.TryParseHtmlString(hex, out var col);
                c.style.backgroundColor = col;
                w.Add(c);
                w.Add(Text($"{word} {hex}", "type-note", "gal-swatch-word"));
                colours.Add(w);
            }
            var translucent = Row(s, "Translucent (pick Tank to see them over the box)");
            foreach (var (word, cls) in new[] { ("frost tint 72 %", "frost"), ("flat tint 92 %", "flat"), ("flat sheet 96 %", "sheet"), ("quiet backing 72 %", "quiet"), ("dot off 52 %", "dot"), ("dark plate 96 %", "plate") })
            {
                var w = new VisualElement();
                w.AddToClassList("gal-swatch");
                var c = new VisualElement();
                c.AddToClassList("gal-chip");
                c.AddToClassList("gal-t-" + cls);
                w.Add(c);
                w.Add(Text(word, "type-note", "gal-swatch-word"));
                translucent.Add(w);
            }
            var type = Row(s, "Type");
            type.style.flexDirection = FlexDirection.Column;
            type.style.alignItems = Align.FlexStart;
            foreach (var (cls, word) in new[]
            {
                ("type-title", "Book title, Lora 700, 26"), ("type-sheet", "Sheet heading, Lora 700, 21"), ("type-box", "Box name, Lora 700, 19"), ("type-big", "Tools, big prompt, caption, Lora 700, 17"),
                ("type-head", "Peek headline, banner, Lora 700, 16"), ("type-small-head", "Tile, tab on, hint, Lora 700, 15"), ("type-btn", "Button, PT Serif 700, 16"), ("type-body", "Body, PT Serif 400, 15"),
                ("type-sub", "Sub, year, toast, PT Serif 400, 14"), ("type-quote", "Quote, teaser line, PT Serif italic, 14"), ("type-note", "Notes, chips, smallest, PT Serif 400, 13"),
            }) State(type, "type", cls, Text(word, cls));
            var spaces = Row(s, "Spacing 4 to 24");
            foreach (var n in new[] { 4, 6, 8, 10, 12, 16, 24 })
            {
                var e = new VisualElement();
                e.AddToClassList("gal-space");
                e.style.width = e.style.height = n * GlassType.Pt;
                spaces.Add(e);
            }
            var radii = Row(s, "Radii: chip 14, tile 16, toast 18, pin 22, button 23, peek 24, sheet 26, tool 28");
            foreach (var n in new[] { 14, 16, 18, 22, 23, 24, 26, 28 })
            {
                var e = new VisualElement();
                e.AddToClassList("gal-radius");
                float r = n * GlassType.Pt;
                e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
                e.Add(Text(n.ToString(), "type-note", "gal-ink"));
                radii.Add(e);
            }
            var icons = Row(s, "Icons, 24 grid, tinted");
            foreach (var name in GlassIcon.All)
            {
                var c = new VisualElement();
                c.AddToClassList("gal-icon");
                c.Add(new GlassIcon(name));
                State(icons, "icon", name, c);
            }
        }

        void Buttons()
        {
            var s = Section("Button");
            var r = Row(s, "Primary: default, pressed, unavailable");
            State(r, "button", "primary", new GlassButton("Give", null, ButtonKind.Primary, UiRole.Confirm));
            State(r, "button", "primary-pressed", new GlassButton("Give", null, ButtonKind.Primary) { ShowPressed = true });
            State(r, "button", "primary-off", new GlassButton("Give", null, ButtonKind.Primary) { Available = false });
            r = Row(s, "Secondary: default, pressed, unavailable");
            State(r, "button", "secondary", new GlassButton("Show me"));
            State(r, "button", "secondary-pressed", new GlassButton("Show me") { ShowPressed = true });
            State(r, "button", "secondary-off", new GlassButton("Now") { Available = false });
            r = Row(s, "Quiet: default, pressed, unavailable");
            State(r, "button", "quiet", new GlassButton("Skip all", null, ButtonKind.Quiet));
            State(r, "button", "quiet-pressed", new GlassButton("Skip all", null, ButtonKind.Quiet) { ShowPressed = true });
            State(r, "button", "quiet-off", new GlassButton("Skip all", null, ButtonKind.Quiet) { Available = false });
            r = Row(s, "Icon, 44 pt, reaching 144 px: default, pressed");
            State(r, "button", "icon", new GlassButton("", null, ButtonKind.Icon) { Icon = "close" });
            State(r, "button", "icon-pressed", new GlassButton("", null, ButtonKind.Icon) { Icon = "back", ShowPressed = true });
            r = Row(s, "Compact, 44 pt, in a row with other things: default, with a mark");
            State(r, "button", "compact", new GlassButton("Pin") { Compact = true });
            State(r, "button", "compact-marked", new GlassButton("Pinned") { Compact = true }).Marked("pin", true);
        }

        void Chips()
        {
            var s = Section("Chip");
            var r = Row(s, "Label, 30 pt inside a 144 px slot: default, pressed");
            State(r, "chip", "label", new GlassChip("Dorrit · child"));
            State(r, "chip", "label-pressed", new GlassChip("Hilda · friend") { ShowPressed = true });
            r = Row(s, "Someone who has died: italic, never faded");
            State(r, "chip", "label-gone", new GlassChip("Aldo · parent") { Gone = true });
            r = Row(s, "Choice: off, on");
            State(r, "chip", "choice-off", new GlassChip("Confusing", null, true));
            State(r, "chip", "choice-on", new GlassChip("Something broke", null, true) { On = true });
            r = Row(s, "Pin chip: pinned, long label, hidden (nothing shows)");
            State(r, "pin", "pinned", new PinChip(null) { Person = "Piers" });
            r = Row(s);
            State(r, "pin", "long", new PinChip(null) { Person = "Bartholomew the Younger (remembered)" });
            State(r, "pin", "hidden", new PinChip(null) { Person = null });
            r = Row(s, "Stories chip: shown; hidden (nothing shows)");
            State(r, "stories", "shown", new StoriesChip(null));
            var off = State(r, "stories", "hidden", new StoriesChip(null));
            off.style.display = DisplayStyle.None;
        }

        VisualElement SheetBox(VisualElement s, string id, bool shortBox, GlassSheet sheet)
        {
            var box = new VisualElement();
            box.AddToClassList("gal-sheet-box");
            if (shortBox) box.AddToClassList("gal-sheet-box--short");
            sheet.AddToClassList("kg-sheet--inline");
            sheet.RemoveFromClassList(GlassSheet.HeightClass(SheetHeight.Standard));
            sheet.style.height = Length.Percent(100f);
            box.Add(sheet);
            State(s, "sheet", id, box);
            return box;
        }

        void Sheets()
        {
            var s = Section("Sheet");
            var live = Row(s, "Live, over the tank: open, then Close or tap outside. Frost goes by the one switch");
            State(live, "sheet", "open-30", new GlassButton("Open 30 %", () => OpenLive(), ButtonKind.Secondary, UiRole.Open));
            State(live, "sheet", "open-59", new GlassButton("Open 59 %", () => OpenLive(true), ButtonKind.Secondary, UiRole.Open));
            s.Add(Text("Inline, flat: head, short content", "type-note", "gal-state"));
            var a = new GlassSheet();
            a.Add(GlassSheet.Head("Shape the land", "gal-sheet-close", () => { }));
            var charges = Row(a.contentContainer);
            charges.Add(new GlassPips(3, 2));
            charges.Add(Text("2 of 3", "type-sub"));
            SheetBox(s, "open", true, a);
            s.Add(Text("Scrolled: the view snaps to whole lines, with a thin bar", "type-note", "gal-state"));
            var b = new GlassSheet();
            b.Add(GlassSheet.Head("A long story", "gal-sheet-close2", () => { }));
            var scrolled = new GlassScroll();
            for (int i = 1; i <= 12; i++)
            {
                var l = Text(GlassType.Lines($"Line {i} of a story that is longer than its sheet."), "type-body");
                l.enableRichText = true;
                scrolled.Add(l);
            }
            b.Add(scrolled);
            SheetBox(s, "scrolled", false, b);
        }

        void Cards()
        {
            var s = Section("Card");
            s.Add(Text("Content of a sheet, never its own surface: dots, head, body, sub, actions with the primary at the right", "type-note", "gal-state"));
            var sheet = new GlassSheet();
            var card = new GlassCard();
            card.Dots.Set(5, 2);
            card.Set("Mabel has a wish", "Mabel the woodcutter would love rain on the west fields.", "Year 70", "Mabel");
            card.Head.Add(new GlassButton("Skip all", null, ButtonKind.Quiet));
            card.Actions.Add(new GlassButton("Next"));
            card.Actions.Add(new GlassButton("Show me", null, ButtonKind.Secondary, UiRole.ShowMe));
            card.Actions.Add(new GlassButton("Give", null, ButtonKind.Primary, UiRole.Confirm));
            sheet.Add(card);
            var box = SheetBox(s, "open-card", false, sheet);
            box.style.height = 704f; // 30 % of the 852 pt phone
            States.Add("gal-card-wish");
            card.name = "gal-card-wish";
            s.Add(Text("A story: no portrait, no filled button, the headline leads", "type-note", "gal-state"));
            var sheet2 = new GlassSheet();
            var story = new GlassCard();
            story.Dots.Set(3, 0);
            story.Set("The first well", "They dug it where the ground ran soft, and it held.");
            story.Actions.Add(new GlassButton("Next"));
            sheet2.Add(story);
            var box2 = SheetBox(s, "open-story", true, sheet2);
            box2.style.height = 500f;
            States.Add("gal-card-story");
            story.name = "gal-card-story";
            s.Add(Text("The peek: over the tools, a tap opens the stack, a swipe puts it away", "type-note", "gal-state"));
            var peek = new GlassPeek { Inline = true };
            var lines = new VisualElement { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("kg-peek-lines");
            lines.Add(Text("Mabel has a wish", "type-head", "kg-peek-line"));
            lines.Add(Text("At the fire: rain on the west fields.", "type-quote", "kg-peek-teaser"));
            peek.Add(new GlassPortrait("Mabel"));
            peek.Add(lines);
            State(s, "peek", "story", peek); // never opened: a gallery piece holds no frost, as the sheets above
        }

        void TabsToggles()
        {
            var s = Section("Tab and toggle");
            var r = Row(s, "Tabs: the selected one is bold and underlined");
            State(r, "tab", "tabs", new GlassTabs("About", "Recent", "Best")).Select(1, false);
            r = Row(s, "The same control as a segment: Music plays");
            State(r, "tab", "segment", new GlassTabs("Off", "Rare", "Normal")).Select(2, false);
            r = Row(s, "Toggle: on, off");
            State(r, "toggle", "on", new GlassToggle(true));
            State(r, "toggle", "off", new GlassToggle(false));
            r = Row(s, "Slider: 70 %, 0 %. 144 px hit strip");
            var a = State(r, "slider", "value", new GlassSlider(0.7f));
            a.style.width = Length.Percent(100f);
            r = Row(s);
            var b = State(r, "slider", "zero", new GlassSlider(0f));
            b.style.width = Length.Percent(100f);
        }

        void Bubbles()
        {
            var s = Section("Bubble");
            s.Add(Text("Who and a line, then chatter in symbols. One at a time; a longer line becomes a caption", "type-note", "gal-state"));
            State(s, "bubble", "line", new GlassBubble("Piers", "Piers hopes for rain."));
            var c = State(s, "bubble", "chatter", new GlassBubble(new[] { "drop", "star" }));
            c.style.marginTop = 44f;
        }

        void Labels()
        {
            var s = Section("Label");
            var r = Row(s, "Name tag, pin tag, tie label with its mark");
            State(r, "label", "name", new GlassTag("Greta"));
            State(r, "label", "pin", new GlassTag("Piers", TagKind.Pin));
            State(r, "label", "tie", new GlassTag("Hilda · friend", TagKind.Tie, "star"));
            State(r, "label", "tie-rival", new GlassTag("Dorrit · rival", TagKind.Tie, "alert"));
            State(r, "label", "tie-family", new GlassTag("Eorl · child", TagKind.Tie, "person"));
            s.Add(Text("Brass plate", "type-note", "gal-state"));
            State(s, "label", "plate", new GlassPlate("Mossbarrow", "years 1–61", "camp, village, town"));
        }

        void Badges()
        {
            var s = Section("Badge and dot");
            var r = Row(s, "Dot, page dots, charge pips, New tag, ribbon");
            State(r, "badge", "dot", new GlassDot()).style.marginRight = 44f;
            State(r, "badge", "page-dots", new GlassPageDots(5, 1)).style.marginRight = 44f;
            State(r, "badge", "pips", new GlassPips(3, 2)).style.marginRight = 44f;
            State(r, "badge", "new", new GlassNewTag()).style.marginRight = 44f;
            State(r, "badge", "ribbon", new GlassRibbon());
        }

        void Plates()
        {
            var s = Section("Toast caption hint banner prompt");
            s.Add(Text("Toast: one at a time, queued", "type-note", "gal-state"));
            State(s, "toast", "short", new GlassToast("All tidy. Look at that sky!") { Inline = true });
            var t2 = State(s, "toast", "long", new GlassToast("Thanks! Saved, it will send when you're online.") { Inline = true });
            t2.style.marginTop = 22f;
            s.Add(Text("Caption: line, line and who, in the time-lapse panel", "type-note", "gal-state"));
            State(s, "caption", "line", new GlassCaption("Mossbarrow · a farming village · year 6") { Inline = true });
            var c2 = State(s, "caption", "who", new GlassCaption("Mira and Tob keep sitting together at the fire.", "Greta, at the fire") { Inline = true });
            c2.style.marginTop = 22f;
            var c3 = State(s, "caption", "panel", new GlassCaption("The fire that took the granary, year 41.", "Greta") { InPanel = true });
            c3.style.marginTop = 22f;
            s.Add(Text("Hint: a word, with a finger, and the ghost finger that sits on the thing", "type-note", "gal-state"));
            State(s, "hint", "word", new GlassHint("Hold to hurry"));
            var h = State(s, "hint", "finger", new GlassHint("Wipe the glass", "hand"));
            h.style.marginTop = 22f;
            var g = State(s, "hint", "ghost", GlassHint.Ghost());
            g.style.marginTop = 22f;
            s.Add(Text("Banner: tool in hand, visiting. The exit never moves", "type-note", "gal-state"));
            var b1 = State(s, "banner", "tool", new GlassBanner("Grove", "Tap the ground where it should grow") { Inline = true });
            b1.End.Add(new GlassPips(3, 2));
            var b2 = State(s, "banner", "visiting", new GlassBanner(null, "River valley · Mossbarrow · years 1–61") { Inline = true });
            b2.End.Add(new GlassButton("Back to your box", null, ButtonKind.Primary));
            b2.style.marginTop = 22f;
            s.Add(Text("Big prompt: ready, pressed; hidden (nothing shows). No timer, no counter, no dim", "type-note", "gal-state"));
            State(s, "prompt", "ready", new GlassBigPrompt("Watch them become a village") { Inline = true });
            var p2 = State(s, "prompt", "pressed", new GlassBigPrompt("Finish Mossbarrow") { Inline = true, ShowPressed = true });
            p2.style.marginTop = 22f;
            var p3 = State(s, "prompt", "hidden", new GlassBigPrompt("Hidden") { Inline = true });
            p3.style.display = DisplayStyle.None;
        }

        void Motion()
        {
            var s = Section("Motion and sound");
            s.Add(Text("Tap to play a token on the box beneath. Sounds go by role, the Sound setting applies", "type-note", "gal-state"));
            var target = new GlassTag("Target");
            target.style.alignSelf = Align.Center;
            target.style.marginBottom = 33f;
            var targetRow = Row(s);
            targetRow.style.height = 132f;
            targetRow.Add(target);
            var r = Row(s);
            void Play(string word, string on, string off, int ms)
            {
                target.AddToClassList(on);
                var b = new GlassButton(word, () =>
                {
                    GlassMotion.Hide(target, off, ms, () => GlassMotion.Show(target, off));
                });
                State(r, "motion", word.ToLowerInvariant(), b);
            }
            Play("Fade", GlassMotion.FadeClass, GlassMotion.FadeOff, GlassMotion.FadeMs);
            Play("Away", GlassMotion.AwayClass, GlassMotion.AwayOff, GlassMotion.AwayMs);
            Play("Turn", GlassMotion.TurnClass, GlassMotion.TurnOff, GlassMotion.TurnMs);
            State(r, "motion", "glow", new GlassButton("Glow", () => GlassMotion.Glow(target, "kg-tag--gold")));
            var sounds = Row(s, "Roles");
            foreach (UiRole role in Enum.GetValues(typeof(UiRole)))
                if (role != UiRole.None) State(sounds, "sound", role.ToString().ToLowerInvariant(), new GlassButton(role.ToString(), null, ButtonKind.Secondary, role));
        }
    }
}
