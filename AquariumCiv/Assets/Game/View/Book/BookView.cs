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
    /// book-person-&lt;id&gt;, book-story-&lt;entry&gt;, book-relic-&lt;id&gt;, book-tree, book-tree-line, book-tree-view, book-tree-map,
    /// book-tree-knot-&lt;id&gt;, book-tree-fold-&lt;id&gt;, book-tree-gen-&lt;gen&gt;.
    /// <para>#774 (tree A, the user's calls 2026-11-26): the Family pages are each founding family's whole tree, top-down, the dead kept
    /// for everyone (<see cref="SimCore.FamilyTreeOf"/>). It scrolls sideways and down (a drag on it never turns the page), a gutter names
    /// each generation and its year, a corner map shows where you are, a branch folds to a chip ("11 more") at the knot under its couple,
    /// and a tap on a face opens that person's card and rings them.</para>
    /// </summary>
    public class BookView : MonoBehaviour
    {
        public static BookView Instance { get; private set; }
        public static bool AutoCreate = true;

        /// <summary>A drag this share of the width, more sideways than up, turns the page.</summary>
        public const float SwipeShare = 0.14f;
        public const string PageClass = "book-page", RightOff = "book-page--right";

        /// <summary>#774: the tree's measures (panel units): a face's cell, a generation's row, the doll, the gutter, the corner map.</summary>
        public const float CellW = 200f, RowH = 300f, Doll = 110f, NameH = 52f, TopPad = 30f, SidePad = 24f, Gutter = 150f, BusUp = 44f, LineW = 5.5f,
            MapW = 210f, MapH = 270f, KnotHit = 99f;

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
        /// <summary>#774: the face ringed on a tree page (the card it was opened from, or the last face tapped); -1 = nobody.</summary>
        public int TreeFocus { get; private set; } = -1;
        /// <summary>#774: the households folded to a chip on the tree pages, until the book closes.</summary>
        public IReadOnlyCollection<int> Folded => folded;
        /// <summary>#774: the tree on screen, as drawn (folds applied), and its scroll (tests).</summary>
        public FamilyTree Tree { get; private set; }
        public ScrollView TreeScroll => treeScroll;
        readonly HashSet<int> folded = new();
        readonly Dictionary<int, VisualElement> treeFaces = new();
        ScrollView treeScroll;
        VisualElement treeView, treeGutter, mapView;
        Vector2 restoreScroll = new(-1f, -1f); // a fold or an unfold redraws the page where it was
        int focusNext = -1;
        bool pressOnTree;
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
            TreeFocus = focusNext; // #774
            focusNext = -1;
            folded.Clear();
            var person = PersonCardView.Instance;
            if (person != null) { person.Close(); person.CloseCrowd(); person.CloseBuilding(); } // #766: one sheet at a time
            var hud = GameHud.Instance;
            if (hud != null) hud.CloseSheet();
            IsOpen = true;
            pressing = false;
            ApplySafe();
            root.style.display = DisplayStyle.Flex;
            UiSound.Play(UiRole.BookOpen);
            ShowPage(start, 0);
            return true;
        }

        /// <summary>#774: the card's "See the whole line": the family's tree page, <paramref name="focusId"/> ringed and in view.</summary>
        public bool OpenAtTree(int rootId, int focusId, Action whenClosed = null)
        {
            focusNext = focusId;
            bool ok = Open(null, SimCore.BookTreeKey(rootId), whenClosed);
            focusNext = -1;
            return ok;
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
            treeView = null;
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
            if (down && !pressing) { pressing = true; pressAt = pos; pressOnTree = OverTree(pos); }
            else if (!down && pressing)
            {
                pressing = false;
                int dir = pressOnTree ? 0 : SwipeTurn(lastPos - pressAt, Screen.width); // #774: a drag on the tree scrolls it
                if (dir != 0) Turn(dir);
            }
            if (down) lastPos = pos;
        }

        Vector2 lastPos;

        /// <summary>#774: a screen point (pixels, y up) on the tree's scrolling view.</summary>
        bool OverTree(Vector2 screen)
        {
            if (treeView == null || treeView.panel == null || current == null || !current.Contains(treeView)) return false;
            var at = RuntimePanelUtils.ScreenToPanel(treeView.panel, new Vector2(screen.x, Screen.height - screen.y));
            return treeView.worldBound.Contains(at);
        }

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
            if (p.kind == BookPageKind.Family) // #774: the tree scrolls both ways on its own
            {
                TreePage(page, p);
                if (p.isNew) { var rb = new GlassRibbon { name = "book-ribbon" }; rb.AddToClassList("book-ribbon"); page.Add(rb); }
                return page;
            }
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
            RelicId.FestivalMask => "person", RelicId.KeeperDoll => "hand", RelicId.LeftSandal => "pin", _ => "star", // #718: an old save's Sky Crumb or Lens: a star
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

        // ================================================================== the family tree (#774, tree A)
        /// <summary>A family's page: its name, then the tree in a view that scrolls both ways, the gutter of generations on its left edge and
        /// the corner map over its bottom right. Cells, lines, knots and chips are placed here because USS can't draw a connector (limits.md).</summary>
        void TreePage(VisualElement page, BookPage p)
        {
            var t = folded.Count > 0 ? sim.FamilyTreeOf(p.tree.rootId, folded) ?? p.tree : p.tree;
            Tree = t;
            treeFaces.Clear();
            var keep = restoreScroll;
            Text(page, p.title, "type-title", "book-title");
            treeView = new VisualElement { name = "book-tree-view" };
            treeView.AddToClassList("book-tree-view");
            page.Add(treeView);
            treeScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                name = "book-tree",
                verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped,
            };
            treeScroll.AddToClassList("book-tree-scroll");
            treeView.Add(treeScroll);
            float w = Gutter + 2f * SidePad + t.width * CellW, h = TopPad + t.rows * RowH;
            var canvas = new VisualElement { name = "book-tree-canvas", pickingMode = PickingMode.Ignore };
            canvas.AddToClassList("book-tree-canvas");
            canvas.style.width = w;
            canvas.style.height = h;
            treeScroll.Add(canvas);

            // the lines first, so the faces sit on them
            foreach (var c in t.couples)
            {
                var a = t.Node(c.a); var b = t.Node(c.b);
                if (a == null || b == null) continue;
                float y = RowTop(a.row) + Doll / 2f;
                Line(canvas, Mathf.Min(X(a.x), X(b.x)), y, Mathf.Abs(X(b.x) - X(a.x)), LineW, "book-tree-couple");
            }
            foreach (var br in t.branches)
            {
                var owner = t.Node(br.ownerId);
                bool couple = t.couples.Any(c => c.a == br.ownerId);
                float from = couple ? RowTop(br.fromRow) + Doll / 2f : RowTop(br.fromRow) + Doll + NameH;
                float bus = RowTop(br.fromRow + 1) - BusUp, x0 = X(br.fromX);
                Line(canvas, x0, from, LineW, bus - from);
                float lo = Mathf.Min(x0, br.childX.Min(X)), hi = Mathf.Max(x0, br.childX.Max(X));
                if (hi - lo > 0.5f) Line(canvas, lo, bus, hi - lo + LineW, LineW);
                foreach (float cx in br.childX) Line(canvas, X(cx), bus, LineW, BusUp);
                int id = br.ownerId;
                if (br.folded) Fold(canvas, id, br.hidden, X(br.childX[0]), RowTop(br.fromRow + 1));
                else Knot(canvas, id, x0, bus);
            }
            foreach (var n in t.nodes) Face(canvas, n);

            // the gutter: on the left edge whatever the sideways scroll
            treeGutter = new VisualElement { name = "book-tree-gutter", pickingMode = PickingMode.Ignore };
            treeGutter.AddToClassList("book-tree-gutter");
            treeGutter.style.height = h;
            foreach (var g in t.gens)
            {
                var cell = new VisualElement { name = "book-tree-gen-" + g.gen, pickingMode = PickingMode.Ignore };
                cell.AddToClassList("book-tree-gen");
                cell.style.top = RowTop(g.row) + Doll / 2f - 50f;
                Text(cell, "G" + g.gen, "type-small-head", "book-tree-gen-no");
                Text(cell, "Yr " + g.year, "type-note", "book-tree-gen-year");
                treeGutter.Add(cell);
            }
            canvas.Add(treeGutter);

            // the corner map: every face a dot (the dead in stone), the view a frame; only when the tree is bigger than its view
            var map = new VisualElement { name = "book-tree-map", pickingMode = PickingMode.Ignore };
            map.AddToClassList("book-tree-map");
            float k = Mathf.Min((MapW - 16f) / w, (MapH - 16f) / h);
            map.style.width = w * k + 16f;
            map.style.height = h * k + 16f;
            foreach (var n in t.nodes)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore };
                d.AddToClassList("book-tree-map-dot");
                if (!n.alive) d.AddToClassList("book-tree-map-dot--gone");
                d.style.left = 8f + X(n.x) * k - 5f;
                d.style.top = 8f + (RowTop(n.row) + Doll / 2f) * k - 5f;
                map.Add(d);
            }
            mapView = new VisualElement { name = "book-tree-map-view", pickingMode = PickingMode.Ignore };
            mapView.AddToClassList("book-tree-map-view");
            map.Add(mapView);
            treeView.Add(map);

            void Follow()
            {
                var off = treeScroll.scrollOffset;
                treeGutter.style.left = off.x;
                var vp = treeScroll.contentViewport.layout;
                bool bigger = vp.width > 1f && (w > vp.width + 1f || h > vp.height + 1f);
                map.style.display = bigger ? DisplayStyle.Flex : DisplayStyle.None;
                mapView.style.left = 8f + off.x * k;
                mapView.style.top = 8f + off.y * k;
                mapView.style.width = Mathf.Min(w, vp.width) * k;
                mapView.style.height = Mathf.Min(h, vp.height) * k;
            }
            treeScroll.horizontalScroller.valueChanged += _ => Follow();
            treeScroll.verticalScroller.valueChanged += _ => Follow();
            bool placed = false;
            treeScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (!placed)
                {
                    placed = true;
                    var vp = treeScroll.contentViewport.layout;
                    if (keep.x >= 0f) treeScroll.scrollOffset = keep; // a fold or an unfold keeps the view
                    else
                    {
                        var at = t.Node(TreeFocus) ?? t.Node(t.rootId);
                        float fx = at != null ? X(at.x) : w / 2f, fy = at != null ? RowTop(at.row) : 0f;
                        treeScroll.scrollOffset = new Vector2(Mathf.Clamp(fx - vp.width / 2f, 0f, Mathf.Max(0f, w - vp.width)),
                            Mathf.Clamp(fy - vp.height / 3f, 0f, Mathf.Max(0f, h - vp.height)));
                    }
                }
                Follow();
            });
        }

        static float X(float cells) => Gutter + SidePad + cells * CellW;
        static float RowTop(int row) => TopPad + row * RowH;

        /// <summary>A face: the doll (in colour with a moss dot, or greyed with a stone), the first name under it. A tap opens their card
        /// over the page and rings them.</summary>
        void Face(VisualElement canvas, TreeNode n)
        {
            var cell = Tap(new VisualElement { name = "book-person-" + n.id }, () => { Ring(n.id); OpenPerson(n.id); });
            cell.AddToClassList("book-tree-face");
            cell.style.left = X(n.x) - CellW / 2f;
            cell.style.top = RowTop(n.row);
            var doll = PersonCardView.Doll(n.fullName, n.alive);
            doll.AddToClassList("book-tree-doll");
            if (n.id == TreeFocus) doll.AddToClassList("book-tree-here");
            cell.Add(doll);
            Text(cell, n.name, "type-note", "book-tree-name");
            canvas.Add(cell);
            treeFaces[n.id] = doll;
        }

        void Ring(int id)
        {
            if (treeFaces.TryGetValue(TreeFocus, out var was)) was.RemoveFromClassList("book-tree-here");
            TreeFocus = id;
            if (treeFaces.TryGetValue(id, out var now)) now.AddToClassList("book-tree-here");
        }

        /// <summary>The knot where a household's drop meets its children's bus: a tap folds the branch to a chip.</summary>
        void Knot(VisualElement canvas, int ownerId, float x, float y)
        {
            var knot = Tap(new VisualElement { name = "book-tree-knot-" + ownerId }, () => FoldBranch(ownerId, true));
            knot.AddToClassList("book-tree-knot");
            knot.style.left = x - KnotHit / 2f;
            knot.style.top = y - KnotHit / 2f;
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("book-tree-knot-dot");
            knot.Add(dot);
            canvas.Add(knot);
        }

        /// <summary>A folded branch: "11 more" where its children would hang; a tap opens it again.</summary>
        void Fold(VisualElement canvas, int ownerId, int hidden, float x, float y)
        {
            var chip = Tap(new VisualElement { name = "book-tree-fold-" + ownerId }, () => FoldBranch(ownerId, false));
            chip.AddToClassList("book-tree-fold");
            chip.style.left = x - CellW / 2f;
            chip.style.top = y + Doll / 2f - 40f;
            Text(chip, hidden + " more", "type-note", "book-tree-fold-words");
            chip.Add(new GlassIcon("next", "kg-icon--small", "book-tree-fold-mark"));
            canvas.Add(chip);
        }

        /// <summary>#774: fold a household's children to a chip, or open it again; the page redraws where it was.</summary>
        public bool FoldBranch(int ownerId, bool fold)
        {
            if (!IsOpen || Page == null || Page.kind != BookPageKind.Family) return false;
            bool changed = fold ? folded.Add(ownerId) : folded.Remove(ownerId);
            if (!changed) return false;
            restoreScroll = treeScroll != null ? treeScroll.scrollOffset : new Vector2(-1f, -1f);
            ShowPage(At, 0);
            restoreScroll = new Vector2(-1f, -1f);
            return true;
        }

        static void Line(VisualElement t, float x, float y, float w, float h, string extra = null)
        {
            var l = new VisualElement { pickingMode = PickingMode.Ignore };
            l.AddToClassList("book-tree-line");
            if (extra != null) l.AddToClassList(extra);
            l.name = "book-tree-line";
            l.style.left = x - (w <= LineW ? LineW / 2f : 0f);
            l.style.top = y - (h <= LineW ? LineW / 2f : 0f);
            l.style.width = w;
            l.style.height = h;
            t.Add(l);
        }

        /// <summary>Debug (#774): the book at the biggest family's tree, the pin (or the first living face in it) ringed.</summary>
        public static string DebugOpenTree()
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null || Instance == null) return "no book";
            var roots = sim.FamilyTreeRoots();
            if (roots.Count == 0) return "no family to draw yet (try Family: deep)";
            var t = sim.FamilyTreeOf(roots[0]);
            int focus = sim.Pinned != null && t.Node(sim.Pinned.id) != null ? sim.Pinned.id : t.nodes.Where(n => n.alive).Select(n => n.id).DefaultIfEmpty(roots[0]).First();
            return Instance.OpenAtTree(roots[0], focus) ? $"tree of the {t.familyName} family: {t.people} people, {t.rows} rows" : "the tree page didn't open";
        }

        /// <summary>Debug (#774): on a tree page, fold the first open branch with children under it (or open them all again).</summary>
        public static string DebugFoldNext()
        {
            var v = Instance;
            if (v == null || !v.IsOpen || v.Tree == null || v.Page?.kind != BookPageKind.Family) return "open a tree page first";
            var b = v.Tree.branches.FirstOrDefault(x => !x.folded && x.ownerId != v.Tree.rootId);
            if (b == null) { foreach (int id in v.folded.ToList()) v.FoldBranch(id, false); return "every branch open"; }
            v.FoldBranch(b.ownerId, true);
            return "folded under " + v.sim.FindNotable(b.ownerId)?.name;
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
