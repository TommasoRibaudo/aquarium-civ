using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// The small book (#80, chronicle-and-history.md §4, board 3.5): full-screen paper, one page at a time, swipe or Back and Next to turn,
    /// Close at the bottom. What's on a page is <see cref="SimCore.BookPages"/>; this class only draws it. The era look is paper colour and
    /// type (spec §4), because the stone and bark art doesn't exist: the camp is bark paper, a picture and a caption; the village is the
    /// priest's italic hand; the town is printed, centred between rules.
    ///
    /// Opens from the time-lapse's Book button, or from Remember on a legend's card. A portrait opens the person's card over the page; a
    /// story's row is Show me, which leaves the book for the live box. New pages wear a ribbon here, and the Book button until they're read.
    /// Stable names: book-root, book-page-&lt;kind&gt;, book-prev, book-next, book-close, book-page-label, book-ribbon, book-picture,
    /// book-person-&lt;id&gt;, book-story-&lt;entry&gt;, book-relic-&lt;id&gt;, book-tree, book-tree-line.
    /// </summary>
    public class BookView : MonoBehaviour
    {
        public static BookView Instance { get; private set; }
        public static bool AutoCreate = true;

        /// <summary>A drag this share of the width, more sideways than up, turns the page.</summary>
        public const float SwipeShare = 0.14f;
        public const string PageClass = "book-page", RightOff = "book-page--right";

        const float TreeW = 948f, CellW = 237f, CellH = 232f, RowGap = 74f, Portrait = 121f, CoupleOff = 130f, BusUp = 37f, LineW = 5.5f;

        UIDocument doc;
        VisualElement root, host, current;
        GlassButton prev, next;
        Label pageLabel;
        readonly Dictionary<string, Texture2D> pictures = new();
        readonly HashSet<string> read = new();
        Texture2D live;
        SimCore sim;
        Action closed;
        Vector2 pressAt;
        bool pressing;

        public bool IsOpen { get; private set; }
        public List<BookPage> Pages { get; private set; } = new();
        public int At { get; private set; }
        public BookPage Page => At >= 0 && At < Pages.Count ? Pages[At] : null;
        public VisualElement Root => root;
        /// <summary>The page on screen (tests).</summary>
        public VisualElement PageElement => current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static BookView Create() => new GameObject("Book").AddComponent<BookView>();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 11.5f; // over the time-lapse (11), under the person card (12): a portrait opens the card over the page
            TimeLapseScreenView.BookHandler = s => { if (Instance != null) Instance.Open(s); };
            TimeLapseScreenView.BookHasNew = s => s.BookHasNewPages;
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Build();
            UiInput.Add(IsOver);
        }

        void OnDisable()
        {
            if (Instance == this) UiInput.Remove(IsOver);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            FreePictures();
        }

        void OnApplicationPause(bool pause) { if (pause && IsOpen) GameClock.Instance?.Save(); }

        bool IsOver(Vector2 p) => IsOpen;

        // ================================================================== building
        void Build()
        {
            var uss = Resources.Load<StyleSheet>("GameUI/Book");
            if (uss != null) doc.rootVisualElement.styleSheets.Add(uss);
            else Debug.LogWarning("[BookView] Resources/GameUI/Book.uss is missing: the book draws unstyled");
            root = new VisualElement { name = "book-root" };
            root.AddToClassList("hud-fill");
            root.AddToClassList("book-root");
            root.style.display = DisplayStyle.None;
            doc.rootVisualElement.Add(root);

            host = new VisualElement { name = "book-pages", pickingMode = PickingMode.Ignore };
            host.AddToClassList("hud-fill");
            root.Add(host);

            var nav = new VisualElement { name = "book-nav" };
            nav.AddToClassList("book-nav");
            prev = Icon("book-prev", "back", () => Turn(-1), nav, UiRole.None);
            pageLabel = new Label { name = "book-page-label", pickingMode = PickingMode.Ignore };
            pageLabel.AddToClassList("type-sub");
            pageLabel.AddToClassList("book-page-label");
            nav.Add(pageLabel);
            next = Icon("book-next", "next", () => Turn(1), nav, UiRole.None);
            var close = Icon("book-close", "close", () => { GlassTapGuard.Arm(root.Q("book-close")); Close(); }, nav, UiRole.BookClose); // the second tap of a double tap stays here: hud-shape sits under it (tester run 6)
            close.AddToClassList("book-nav-close");
            root.Add(nav);
        }

        static GlassButton Icon(string name, string icon, Action tapped, VisualElement row, UiRole role)
        {
            var b = new GlassButton("", tapped, ButtonKind.Secondary, role) { name = name, Icon = icon };
            b.AddToClassList("book-nav-btn");
            row.Add(b);
            return b;
        }

        // ================================================================== open, close, turn
        /// <summary>Opens the book of <paramref name="box"/> (null = the box on screen), at <paramref name="key"/> or the cover.</summary>
        public bool Open(SimCore box = null, string key = null, Action whenClosed = null)
        {
            sim = box ?? GameClock.Instance?.Sim;
            if (sim == null || IsOpen) return false;
            Pages = sim.BookPages();
            read.Clear();
            int start = key == null ? 0 : Pages.FindIndex(p => p.key == key);
            if (start < 0) return false;
            closed = whenClosed;
            IsOpen = true;
            pressing = false;
            ApplySafe();
            root.style.display = DisplayStyle.Flex;
            UiSound.Play(UiRole.BookOpen);
            ShowPage(start, 0);
            return true;
        }

        /// <summary>Remember on a legend's card: their page, if they have one.</summary>
        public bool OpenAtLegend(int notableId, Action whenClosed = null) => Open(null, SimCore.BookLegendKey(notableId), whenClosed);

        public void Close() => Shut(true);

        void Shut(bool sound)
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.style.display = DisplayStyle.None;
            current?.RemoveFromHierarchy();
            current = null;
            host.Clear();
            FreePictures();
            TimeLapseScreenView.BookRibbon = Pages.Any(p => p.isNew && !read.Contains(p.key));
            GameClock.Instance?.Save();
            var done = closed;
            closed = null;
            done?.Invoke();
        }

        void FreePictures()
        {
            foreach (var t in pictures.Values) if (t != null) Destroy(t);
            pictures.Clear();
            if (live != null) Destroy(live);
            live = null;
        }

        /// <summary>Back or Next: one page, if there is one.</summary>
        public bool Turn(int dir)
        {
            int to = At + Math.Sign(dir);
            if (!IsOpen || to < 0 || to >= Pages.Count) return false;
            UiSound.Play(UiRole.BookPage);
            ShowPage(to, dir);
            return true;
        }

        /// <summary>Jumps straight to a page (tests, debug).</summary>
        public void GoTo(int index) { if (IsOpen && index >= 0 && index < Pages.Count) ShowPage(index, 0); }

        void ShowPage(int index, int dir)
        {
            var old = current;
            At = index;
            var page = Pages[index];
            current = Draw(page);
            host.Add(current);
            sim.BookMarkSeen(page);
            read.Add(page.key);
            if (dir != 0 && old != null)
            {
                string outClass = dir > 0 ? GlassMotion.TurnOff : RightOff, inClass = dir > 0 ? RightOff : GlassMotion.TurnOff;
                GlassMotion.Show(current, inClass);
                old.pickingMode = PickingMode.Ignore;
                GlassMotion.Hide(old, outClass, GlassMotion.TurnMs, old.RemoveFromHierarchy);
            }
            else old?.RemoveFromHierarchy();
            prev.style.visibility = index > 0 ? Visibility.Visible : Visibility.Hidden;
            next.style.visibility = index < Pages.Count - 1 ? Visibility.Visible : Visibility.Hidden;
            pageLabel.text = page.kind == BookPageKind.Cover ? "Cover" : $"{index + 1} of {Pages.Count}";
        }

        void ApplySafe()
        {
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            root.EnableInClassList("book--short", GlassSheet.IsShort(Screen.width, Screen.height));
            root.style.paddingTop = 0f;
            root.Q("book-nav").style.bottom = Mathf.Max(GlassSheet.BottomPad, i.w);
            topInset = Mathf.Max(MinTop, i.y + 16f);
        }

        const float MinTop = 121f; // the board's 44 pt
        float topInset = MinTop;

        // ================================================================== swipe
        void Update()
        {
            if (!IsOpen) return;
            var card = PersonCardView.Instance;
            if (card != null && card.AnyOpen) { pressing = false; return; }
            bool down = ReadPointer(out var pos);
            if (down && !pressing) { pressing = true; pressAt = pos; }
            else if (!down && pressing)
            {
                pressing = false;
                int dir = SwipeTurn(lastPos - pressAt, Screen.width);
                if (dir != 0) Turn(dir);
            }
            if (down) lastPos = pos;
        }

        Vector2 lastPos;

        /// <summary>+1 next (a drag to the left), -1 back, 0 not a swipe: a scroll, a tap.</summary>
        public static int SwipeTurn(Vector2 drag, float width)
        {
            if (Mathf.Abs(drag.x) < width * SwipeShare || Mathf.Abs(drag.x) < Mathf.Abs(drag.y) * 1.5f) return 0;
            return drag.x < 0f ? 1 : -1;
        }

        static bool ReadPointer(out Vector2 pos)
        {
            pos = default;
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.press.isPressed) { pos = t.position.ReadValue(); return true; }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) { pos = Mouse.current.position.ReadValue(); return true; }
            return false;
        }

        // ================================================================== pages
        VisualElement Draw(BookPage p)
        {
            var page = new VisualElement { name = "book-page-" + p.kind.ToString().ToLowerInvariant(), pickingMode = PickingMode.Ignore };
            page.AddToClassList(PageClass);
            page.AddToClassList(GlassMotion.TurnClass);
            string look = p.kind == BookPageKind.Era ? p.era switch { Era.Camp => "book-page--bark", Era.Village => "book-page--hand", _ => "book-page--print" } : null;
            if (look != null) page.AddToClassList(look);
            page.style.paddingTop = topInset;
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped,
            };
            scroll.AddToClassList("book-scroll");
            page.Add(scroll);
            var body = scroll.contentContainer;
            switch (p.kind)
            {
                case BookPageKind.Cover: Cover(body, p); break;
                case BookPageKind.Founding: Founding(body, p); break;
                case BookPageKind.Era: EraSpread(body, p); break;
                case BookPageKind.Legend: Legend(body, p); break;
                case BookPageKind.Family: Family(body, p); break;
                case BookPageKind.Relics: Relics(body, p); break;
                case BookPageKind.End: End(body, p); break;
                case BookPageKind.Choices: Choices(body, p); break;
            }
            if (p.isNew)
            {
                var ribbon = new GlassRibbon { name = "book-ribbon" };
                ribbon.AddToClassList("book-ribbon");
                page.Add(ribbon);
            }
            return page;
        }

        static Label Text(VisualElement to, string words, params string[] classes)
        {
            var l = new Label(words) { pickingMode = PickingMode.Ignore, enableRichText = true };
            foreach (var c in classes) l.AddToClassList(c);
            to.Add(l);
            return l;
        }

        static Label Para(VisualElement to, string words, string extra = null)
        {
            var l = Text(to, GlassType.Lines(words), "type-body", "book-line");
            if (extra != null) l.AddToClassList(extra);
            return l;
        }

        void Cover(VisualElement to, BookPage p)
        {
            Text(to, p.title, "type-title", "book-title");
            Text(to, Sentence(SettingLabel(), p.sub), "type-sub", "book-sub");
            Picture(to, p, true);
        }

        /// <summary>"River valley · years 1–61".</summary>
        static string Sentence(string lead, string rest) =>
            string.IsNullOrEmpty(lead) ? rest : lead + " · " + (string.IsNullOrEmpty(rest) ? "" : char.ToLowerInvariant(rest[0]) + rest.Substring(1));

        string SettingLabel()
        {
            var shelf = GameClock.Instance?.Session?.shelf;
            var box = shelf?.boxes?.Find(b => b.state == sim.State);
            return box != null ? BoxCompleteController.SettingLabel(box.setting) : "";
        }

        void Founding(VisualElement to, BookPage p)
        {
            Text(to, p.title, "type-title", "book-title");
            Para(to, p.line);
            Text(to, "Founders", "type-big", "book-head");
            foreach (var who in p.people) PersonRow(to, who);
        }

        void EraSpread(VisualElement to, BookPage p)
        {
            if (p.era == Era.Camp)
            {
                Picture(to, p, true);
                Para(to, p.title, "book-caption");
            }
            else
            {
                if (p.era == Era.Town) Rule(to);
                Text(to, p.title, "type-title", "book-title");
                if (p.era == Era.Town) Rule(to);
                Text(to, p.sub, "type-sub", "book-sub");
                Picture(to, p, false);
                if (!string.IsNullOrEmpty(p.line)) Para(to, p.line, "book-caption");
            }
            foreach (var s in p.stories) StoryRow(to, s);
            foreach (var who in p.people) PersonRow(to, who);
        }

        static void Rule(VisualElement to)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.AddToClassList("book-rule");
            to.Add(r);
        }

        void Legend(VisualElement to, BookPage p)
        {
            var head = Tap(new VisualElement { name = "book-person-" + p.person.id }, () => OpenPerson(p.person.id));
            head.AddToClassList("book-head-row");
            head.Add(PortraitOf(p.person.fullName, p.person.alive));
            Text(head, p.title, "type-sheet", "book-head-name");
            to.Add(head);
            Para(to, p.sub);
            Para(to, p.line);
            Para(to, p.line2);
            if (!string.IsNullOrEmpty(p.note)) Para(to, p.note);
            foreach (var s in p.stories) StoryRow(to, s);
            foreach (var who in p.people) PersonRow(to, who);
        }

        /// <summary>#373: "The Keeper's choices" of an era: per choice the ask, what was chosen, who it touched, what it left. The Keeper's
        /// own are tappable (Show me), the people's after a lapse are plain lines.</summary>
        void Choices(VisualElement to, BookPage p)
        {
            Text(to, p.title, "type-title", "book-title");
            Text(to, p.sub, "type-sub", "book-sub");
            bool canShow = !(GameClock.Instance?.Visiting ?? true);
            foreach (var c in p.choices)
            {
                var block = new VisualElement { name = "book-choice-" + c.dilemmaId, pickingMode = PickingMode.Ignore };
                block.AddToClassList("book-choice");
                Text(block, c.ask + " · year " + c.year, "type-small-head", "book-choice-ask");
                Text(block, c.chosenLine, "type-body", "book-choice-line");
                if (c.touchedLine != "") Text(block, c.touchedLine, "type-body", "book-choice-line", "book-choice-people");
                if (c.propLine != "") Text(block, c.propLine, "type-body", "book-choice-line", "book-choice-people");
                if (canShow && c.answerEntryId >= 0) { int entry = c.answerEntryId; Tap(block, () => ShowMe(entry)); }
                to.Add(block);
            }
        }

        void Family(VisualElement to, BookPage p)
        {
            Text(to, p.title, "type-title", "book-title");
            to.Add(Tree(p.family));
        }

        void Relics(VisualElement to, BookPage p)
        {
            Text(to, p.title, "type-title", "book-title");
            foreach (var r in p.relics)
            {
                var row = new VisualElement { name = "book-relic-" + r.id, pickingMode = PickingMode.Ignore };
                row.AddToClassList("book-relic");
                if (r.found && r.ownerId >= 0) Tap(row, () => OpenPerson(r.ownerId));
                var mark = new GlassPortrait(r.found ? "" : "?");
                mark.AddToClassList("kg-portrait--era");
                if (r.found) mark.Add(new GlassIcon(RelicIcon(r.id), "book-relic-icon"));
                row.Add(mark);
                var words = new VisualElement { pickingMode = PickingMode.Ignore };
                words.AddToClassList("book-relic-words");
                Text(words, r.name, "type-small-head", "book-line");
                Para(words, r.found ? r.label : r.hint);
                if (r.found && r.ownerName != "") Text(words, r.ownerName, "type-note", "book-relic-owner");
                row.Add(words);
                to.Add(row);
            }
        }

        static string RelicIcon(string id) => id switch
        {
            RelicId.SkyCrumb => "gift", RelicId.FestivalMask => "person", RelicId.KeeperDoll => "hand", RelicId.LeftSandal => "pin", RelicId.Lens => "eye", _ => "star",
        };

        void End(VisualElement to, BookPage p)
        {
            Text(to, p.title, "type-title", "book-title");
            Text(to, p.sub, "type-sub", "book-sub");
            Picture(to, p, false);
            Para(to, p.line);
            Para(to, p.line2);
            foreach (var who in p.people) PersonRow(to, who);
        }

        // ================================================================== rows
        VisualElement Tap(VisualElement e, Action action)
        {
            e.pickingMode = PickingMode.Position;
            e.AddManipulator(new Clickable(() => { UiSound.Play(UiRole.Tap); action(); }));
            return e;
        }

        static GlassPortrait PortraitOf(string name, bool alive)
        {
            var p = new GlassPortrait(name) { pickingMode = PickingMode.Ignore };
            if (!alive) p.AddToClassList("kg-portrait--gone");
            return p;
        }

        void PersonRow(VisualElement to, BookPerson who)
        {
            if (who == null) return;
            var row = Tap(new VisualElement { name = "book-person-" + who.id }, () => OpenPerson(who.id));
            row.AddToClassList("book-row");
            row.Add(PortraitOf(who.name, who.alive));
            Text(row, string.IsNullOrEmpty(who.note) ? who.name : who.name + ", " + who.note, "type-body", "book-row-text");
            if (who.legend) row.Add(new GlassIcon("star", "kg-icon--small"));
            to.Add(row);
        }

        void StoryRow(VisualElement to, BookStory s)
        {
            bool canShow = !(GameClock.Instance?.Visiting ?? true);
            var row = new VisualElement { name = "book-story-" + s.entryId, pickingMode = PickingMode.Ignore };
            row.AddToClassList("book-row");
            if (canShow) Tap(row, () => ShowMe(s.entryId));
            Text(row, s.text + " · year " + s.year, "type-body", "book-row-text");
            if (canShow) row.Add(new GlassIcon("eye", "kg-icon--small"));
            to.Add(row);
        }

        void OpenPerson(int id)
        {
            var cards = PersonCardView.Instance;
            if (cards != null) cards.Open(id, false);
        }

        /// <summary>A story's row: out of the book and the time-lapse, to the live box. An old story is a fly-to only.</summary>
        void ShowMe(int entryId)
        {
            var e = sim.State.chronicle.Find(x => x.id == entryId);
            var director = ShowMeDirector.Instance;
            if (e == null || director == null) return;
            Shut(false);
            if (TimeLapseScreenView.Instance != null) TimeLapseScreenView.Instance.Close();
            UiSound.Play(UiRole.ShowMe);
            director.Show(e, true);
        }

        // ================================================================== pictures
        void Picture(VisualElement to, BookPage p, bool tall)
        {
            var tex = PictureOf(p);
            if (tex == null) return;
            var frame = new VisualElement { name = "book-picture", pickingMode = PickingMode.Ignore };
            frame.AddToClassList("book-picture");
            if (tall) frame.AddToClassList("book-picture--tall");
            frame.style.backgroundImage = new StyleBackground(tex);
            to.Add(frame);
        }

        /// <summary>A closed era: the still from its end (the cheer of the next). A running era, the cover, the end: the newest still, and
        /// with none yet the box as it is now, drawn once per opening. The still never changes, so neither does a closed era's page.</summary>
        Texture2D PictureOf(BookPage p)
        {
            int seed = sim.State.seed;
            var info = p.stillEra >= 0 ? EraStills.Find(seed, (Era)p.stillEra) : EraStills.All(seed).LastOrDefault();
            if (info == null && (p.stillEra < 0 || p.stillEra == (int)sim.State.era)) info = EraStills.All(seed).LastOrDefault();
            if (info == null) return p.stillEra < 0 || p.stillEra == (int)sim.State.era ? Live() : null;
            if (pictures.TryGetValue(info.png, out var tex)) return tex;
            string path = EraStills.PngPath(info);
            if (!System.IO.File.Exists(path)) return null;
            tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) { Destroy(tex); return null; }
            if (EraStills.LooksBlank(tex)) { Destroy(tex); EraStills.Forget(info); return PictureOf(p); } // a grey still from before the depth fix
            pictures[info.png] = tex;
            return tex;
        }

        Texture2D Live()
        {
            if (live != null) return live;
            var cam = Camera.main;
            if (cam == null) return null;
            live = EraStills.Render(cam, EraStills.Width, Mathf.RoundToInt(EraStills.Width * Screen.height / (float)Mathf.Max(1, Screen.width)));
            return live;
        }

        // ================================================================== the family tree
        /// <summary>The tree: parents over the main person, the couple joined, their children hanging from a bus. Up to four children a row,
        /// so with a second row the trunk runs down the gap between the second and third of the first. Cells and lines are placed here
        /// because USS can't draw a connector (limits.md).</summary>
        VisualElement Tree(BookFamily f)
        {
            var t = new VisualElement { name = "book-tree", pickingMode = PickingMode.Ignore };
            t.AddToClassList("book-tree");
            float mid = TreeW / 2f, mainX = f.partner != null ? mid - CoupleOff : mid, y = 0f, portraitMid = Portrait / 2f;
            if (f.parents.Count > 0)
            {
                bool two = f.parents.Count == 2;
                Cell(t, f.parents[0], two ? mainX - CoupleOff : mainX, y);
                if (two)
                {
                    Cell(t, f.parents[1], mainX + CoupleOff, y);
                    Line(t, mainX - CoupleOff + portraitMid, y + portraitMid, 2f * CoupleOff - Portrait, LineW);
                    Line(t, mainX, y + portraitMid, LineW, CellH + RowGap - portraitMid);
                }
                else Line(t, mainX, y + CellH, LineW, RowGap);
                y += CellH + RowGap;
            }
            Cell(t, f.main, mainX, y);
            float trunk = y + CellH;
            if (f.partner != null)
            {
                Cell(t, f.partner, mid + CoupleOff, y);
                Line(t, mainX + portraitMid, y + portraitMid, 2f * CoupleOff - Portrait, LineW);
                trunk = y + portraitMid;
            }
            float rowX = f.partner != null ? mid : mainX;
            int done = 0;
            while (done < f.children.Count)
            {
                int n = Mathf.Min(4, f.children.Count - done);
                y += CellH + RowGap;
                float bus = y - BusUp;
                Line(t, rowX, trunk, LineW, bus - trunk);
                float x0 = rowX - (n - 1) * CellW / 2f;
                if (n > 1) Line(t, x0, bus, (n - 1) * CellW + LineW, LineW);
                for (int i = 0; i < n; i++)
                {
                    float x = x0 + i * CellW;
                    Line(t, x, bus, LineW, BusUp);
                    Cell(t, f.children[done + i], x, y);
                }
                trunk = bus;
                done += n;
            }
            t.style.height = y + CellH;
            return t;
        }

        void Cell(VisualElement t, BookPerson who, float centre, float y)
        {
            var c = Tap(new VisualElement { name = "book-person-" + who.id }, () => OpenPerson(who.id));
            c.AddToClassList("book-who");
            c.style.left = centre - CellW / 2f;
            c.style.top = y;
            c.Add(PortraitOf(who.name, who.alive));
            if (who.legend)
            {
                var star = new GlassIcon("star", "kg-icon--small", "book-who-star");
                c.Add(star);
            }
            Text(c, who.name, "type-note", "book-who-name");
            t.Add(c);
        }

        static void Line(VisualElement t, float x, float y, float w, float h)
        {
            var l = new VisualElement { pickingMode = PickingMode.Ignore };
            l.AddToClassList("book-tree-line");
            l.name = "book-tree-line";
            l.style.left = x - (w <= LineW ? LineW / 2f : 0f);
            l.style.top = y - (h <= LineW ? LineW / 2f : 0f);
            l.style.width = w;
            l.style.height = h;
            t.Add(l);
        }

        // ================================================================== debug (debug panel, `script eval`)
        public static string DebugOpen() => Instance != null && Instance.Open() ? "book open" : "no book";

        /// <summary>Debug: earn every page: some years, the three eras with their stills, relics (the rest stay silhouettes), the end.</summary>
        public static string DebugEarnAll()
        {
            var clock = GameClock.Instance;
            var sim = clock != null ? clock.Sim : null;
            if (sim == null) return "no sim";
            var cam = Camera.main;
            var rig = cam != null ? cam.GetComponent<BoxCamera>() : null;
            for (int i = 0; i < 2 && !sim.IsFinalEra; i++)
            {
                SimLayoutSource.DebugSkipYears(12);
                SimLayoutSource.DebugNextEra(false);
                if (cam != null) EraStills.Take(cam, rig, sim);
            }
            SimLayoutSource.DebugSkipYears(12);
            sim.DebugEarnBook();
            clock.Save();
            return $"{sim.BookPages().Count} book pages";
        }

        /// <summary>Debug: forget what's been read, so every page wears its ribbon.</summary>
        public static string DebugUnread()
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null) return "no sim";
            sim.State.bookSeen.Clear();
            sim.State.bookKept.Clear();
            return "book unread";
        }
    }
}
