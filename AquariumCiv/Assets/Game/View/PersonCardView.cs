using AquariumCiv.Audio;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// Notables in the view (#39; game definition §4, first-session §5 "Person card", notable-roles §6). Added to the box at
    /// start-up (like <see cref="GlassVoice"/>):
    /// <list type="bullet">
    /// <item><b>Tap a person.</b> A tap on a notable's doll (<see cref="WorldTap"/>, called by <see cref="GlassVoice"/> before
    /// it hears a knock) opens their card and flies the camera so they stand above it. A tap on the world with a card open
    /// closes it. A tap that hits nobody is still the Keeper's knock.</item>
    /// <item><b>The person card</b> (<see cref="SimCore.PersonCardOf"/>): a bottom sheet over at most ~46 % of the screen, the
    /// person in view above it. Name, job and role, age, trait, their line for the belief stage, their wish; family, friends,
    /// rivals and mentors as chips (a tap opens that person's card); their stories (Recent / Best) each with "Show me"
    /// (<see cref="ShowMeDirector"/>); their relics; Pin. The dead get a memorial (when, how old, the death line, why they are
    /// a legend, who remembers them).</item>
    /// <item><b>Relationships visible.</b> While a card is open, light arcs run from the person's doll to the dolls of their
    /// ties (<see cref="SimCore.RelationshipGraph"/>), coloured by kind, with a small label at the far end: at most
    /// <see cref="MaxLines"/>, strongest first. They clear when the card closes.</item>
    /// <item><b>The pin.</b> One pinned notable (<see cref="SimCore.Pin"/>): a gold name tag follows their doll; the HUD's pin
    /// chip shows their name and opens their card. Saved with the box (<see cref="SimState.pinnedNotableId"/>).</item>
    /// </list>
    /// The card closes itself when the world isn't the player's to touch (a HUD sheet, camera mode, shape mode, a moment, the
    /// time-lapse, the last moment). Debug: static <c>Debug*</c> methods (the debug panel).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PersonCardView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static PersonCardView Instance { get; private set; }

        /// <summary>How close to a doll (panel units, 1080 wide) a tap must land to pick it: about half a 48 dp target.</summary>
        public const float PickRadius = 64f;
        /// <summary>Relationship lines drawn at most (never clutter).</summary>
        public const int MaxLines = 6;
        /// <summary>Where the person stands on screen after the fly-to (fraction of the height from the bottom): above the card.</summary>
        public const float CardAnchorY = 0.74f;
        /// <summary>The card's height at most (share of the screen).</summary>
        public const float CardMaxShare = 0.46f;

        public int OpenId { get; private set; } = -1;
        public bool IsOpen => OpenId >= 0;
        public PersonCard Card { get; private set; }
        public bool ShowingBest { get; private set; }
        /// <summary>Lines drawn last frame, and to whom (tests).</summary>
        public int LinesDrawn => drawn.Count;
        public IReadOnlyList<int> LinesTo => drawnTo;
        /// <summary>The pin tag is on screen, and where (screen pixels, y up; tests).</summary>
        public bool PinTagShown { get; private set; }
        public Vector2 PinTagScreen { get; private set; }
        public VisualElement CardRoot => cardRoot;

        GameObject overlayGo, cardGo;
        VisualElement overlayRoot, linesLayer, cardRoot, sheet, pinTag, nameTag;
        Label pinTagText, nameTagText;
        ScrollView body;
        Label title, subtitle;
        Button pinButton;
        readonly List<Label> tieLabels = new();
        readonly List<(Vector2 a, Vector2 c, Vector2 b, Color col, float w)> drawn = new();
        readonly List<int> drawnTo = new();
        readonly List<Vector4> placed = new();
        readonly List<int> shown = new();
        List<RelEdge> edges = new();
        string sig = "";
        float nextRefresh;
        bool showMeFromCard;
        Rect lastSafe;
        Vector2Int lastScreen;
        string chipText;
        int chipId = -2;

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : SimLayoutSource.Find()?.Sim;
        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<PersonCardView>() == null) b.gameObject.AddComponent<PersonCardView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            UiInput.Add(IsOverCard);
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            UiInput.Remove(IsOverCard);
            if (GameHud.Instance != null) GameHud.Instance.SetPinChip(null, null);
            if (overlayGo != null) Destroy(overlayGo);
            if (cardGo != null) Destroy(cardGo);
        }

        bool IsOverCard(Vector2 screenPos) => IsOpen && cardRoot != null && GameUiPanel.Over(cardRoot, screenPos);

        // ================================================================== the API

        /// <summary>A short tap reached the world at <paramref name="screenPos"/> (pixels, y up). A notable's doll there opens
        /// their card; otherwise an open card closes. True if the tap was used (no knock).</summary>
        public bool WorldTap(Vector2 screenPos)
        {
            int id = Pick(screenPos);
            if (id >= 0) { Open(id, true); return true; }
            if (IsOpen) { Close(); return true; }
            return false;
        }

        /// <summary>The notable whose doll is under a screen point (nearest within <see cref="PickRadius"/>), or -1.</summary>
        public int Pick(Vector2 screenPos)
        {
            var pv = PeopleView.Find();
            var cam = Camera.main;
            var sim = Sim;
            if (pv == null || cam == null || sim == null) return -1;
            pv.NotablesShown(shown);
            float r = PickRadius * Screen.width / GameUiPanel.ReferenceWidth;
            int best = -1;
            float bestD = r;
            foreach (int id in shown)
            {
                var who = PersonRef.Notable(id);
                if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) continue;
                var a = cam.WorldToScreenPoint(feet);
                var b = cam.WorldToScreenPoint(top);
                if (a.z <= 0f || b.z <= 0f) continue;
                float d = SegmentDistance(screenPos, a, b);
                if (d < bestD) { bestD = d; best = id; }
            }
            return best;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Open a notable's card (living or dead). <paramref name="fly"/>: the camera brings their doll above the card.</summary>
        public bool Open(int notableId, bool fly = true)
        {
            var sim = Sim;
            var card = sim?.PersonCardOf(notableId);
            if (card == null) return false;
            EnsureUi();
            var hud = GameHud.Instance;
            if (hud != null) { hud.CloseSheet(); hud.SetCleanMode(false); }
            if (OpenId != notableId) { ShowingBest = false; GameClock.Tel.CardOpened("person", notableId); }
            OpenId = notableId;
            Card = card;
            edges = sim.RelationshipGraph(true, notableId);
            Build(card);
            if (cardRoot.resolvedStyle.display != DisplayStyle.Flex || OpenId != notableId || Card != card) Sfx.Play(CueLibrary.UiOpen); // #96
            cardRoot.style.display = DisplayStyle.Flex;
            if (fly) FlyTo(notableId);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            Sfx.Play(CueLibrary.UiClose); // #96
            OpenId = -1;
            Card = null;
            edges.Clear();
            if (cardRoot != null) cardRoot.style.display = DisplayStyle.None;
            ClearLines();
            if (showMeFromCard && ShowMeDirector.Instance != null) ShowMeDirector.Instance.End();
            showMeFromCard = false;
        }

        /// <summary>The card's Pin button: pins this person (or unpins them if they are the pin).</summary>
        public void TogglePin()
        {
            var sim = Sim;
            if (sim == null || !IsOpen) return;
            bool on = sim.TogglePin(OpenId);
            if (on) GameClock.Tel.PinTapped(OpenId);
            Sfx.Play(CueLibrary.UiToggle); // #96
            if (on && GameHud.Instance != null) GameHud.Instance.Toast($"Following {sim.FindNotable(OpenId)?.name}. Their stories come first.", 3f);
            Refresh(true);
            UpdateChip(sim, true);
        }

        void FlyTo(int id)
        {
            var cam = Cam;
            var pv = PeopleView.Find();
            if (cam == null || pv == null || !pv.TryGetDollFeet(PersonRef.Notable(id), out var feet)) return;
            float zoom = Mathf.Min(cam.zoom, BoxCameraMath.FlyPersonZoom);
            var focus = ShowMeDirector.AboveCard(cam, feet, zoom, cam.pitch, CardAnchorY);
            cam.FlyTo(focus, zoom, BoxCameraMath.FlyDefaultSeconds);
        }

        // ================================================================== frame
        void Update()
        {
            var sim = Sim;
            if (sim == null) { Close(); SetTag(pinTag, false); return; }
            if (IsOpen && Blocked()) Close();
            if (IsOpen && (Time.unscaledTime >= nextRefresh)) { nextRefresh = Time.unscaledTime + 1f; Refresh(false); }
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y) ApplySafeArea();
            UpdateChip(sim, false);
        }

        /// <summary>The world isn't the player's to touch now: the card closes and world labels hide.</summary>
        static bool Blocked()
        {
            var hud = GameHud.Instance;
            if (hud != null && (hud.CameraMode || hud.SheetOpen || !hud.IsVisible || hud.CleanMode)) return true;
            var shaping = ShapingView.Instance;
            if (shaping != null && shaping.ShapeMode) return true;
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return true;
            var bc = BoxCompleteController.Instance;
            if (bc != null && bc.Playing) return true;
            var mp = MomentPlayer.Instance;
            return mp != null && mp.Playing;
        }

        static bool LabelsHidden()
        {
            var hud = GameHud.Instance;
            if (hud != null && (hud.CameraMode || !hud.IsVisible || hud.SheetOpen)) return true;
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return true;
            var bc = BoxCompleteController.Instance;
            return bc != null && bc.Playing;
        }

        void UpdateChip(SimCore sim, bool force)
        {
            var hud = GameHud.Instance;
            if (hud == null) return;
            var p = sim.Pinned;
            string text = p == null ? null : p.alive ? p.name : p.name + " (remembered)";
            int id = p?.id ?? -1;
            if (!force && text == chipText && id == chipId) return;
            chipText = text; chipId = id;
            hud.SetPinChip(text, id < 0 ? null : () => Open(id, true));
        }

        void LateUpdate()
        {
            var sim = Sim;
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (overlayRoot == null) EnsureUi();
            bool hide = sim == null || pv == null || cam == null || overlayRoot.panel == null || LabelsHidden();
            // the pin's gold tag over their head
            var pinned = hide ? null : sim.Pinned;
            bool pinOn = pinned != null && pinned.alive && Place(pinTag, pv, cam, pinned.id);
            if (pinOn) pinTagText.text = pinned.name;
            SetTag(pinTag, pinOn);
            PinTagShown = pinOn;
            // the open card's person (when not the pin): a paper tag
            bool nameOn = !hide && IsOpen && Card != null && Card.alive && (pinned == null || pinned.id != OpenId) && Place(nameTag, pv, cam, OpenId);
            if (nameOn) nameTagText.text = Card.name;
            SetTag(nameTag, nameOn);
            // relationship lines
            drawn.Clear(); drawnTo.Clear();
            int used = 0;
            placed.Clear();
            if (!hide && IsOpen && Card != null && Card.alive && pv.TryGetDollTop(PersonRef.Notable(OpenId), out var meW))
            {
                var me = ToPanel(cam, meW, out bool meOk);
                if (meOk)
                    foreach (var e in edges.OrderBy(x => Rank(x.kind)).ThenBy(x => x.a + x.b))
                    {
                        if (drawn.Count >= MaxLines) break;
                        int other = e.a == OpenId ? e.b : e.a;
                        if (!pv.TryGetDollTop(PersonRef.Notable(other), out var oW)) continue;
                        var o = ToPanel(cam, oW, out bool ok);
                        if (!ok || (o - me).magnitude < 20f) continue;
                        var mid = (me + o) * 0.5f;
                        var ctrl = mid + Vector2.down * Mathf.Min(160f, (o - me).magnitude * 0.3f); // an arc, up over the heads
                        var col = ColorOf(e.kind);
                        drawn.Add((me, ctrl, o, col, e.kind == TieKind.Shared ? 4f : 7f));
                        drawnTo.Add(other);
                        var label = TieLabel(used);
                        var ties = Card.family.Concat(Card.friends).Concat(Card.rivals).Concat(Card.mentors).Where(t => t.otherId == other).ToList();
                        var tie = ties.FirstOrDefault(t => t.kind == e.kind || (e.kind == TieKind.Parent && t.kind == TieKind.Child) || (e.kind == TieKind.Mentor && t.kind == TieKind.Apprentice)) ?? ties.FirstOrDefault(); // the label says what the line's colour says
                        string first = sim.FindNotable(other)?.name ?? "";
                        label.text = tie != null ? $"{first} · {tie.label}" : $"{first} · {e.label}";
                        label.style.borderBottomColor = col; label.style.borderTopColor = col; label.style.borderLeftColor = col; label.style.borderRightColor = col;
                        const float along = 0.72f; // on the arc, near the other person (their own tag sits over their head)
                        var at = (1f - along) * (1f - along) * me + 2f * (1f - along) * along * ctrl + along * along * o;
                        // labels never pile up: a label that would sit on an earlier one moves down below it
                        var size = new Vector2(label.text.Length * 15f + 40f, 46f);
                        for (int k = 0; k < placed.Count; k++)
                            if (Mathf.Abs(placed[k].x - at.x) < (placed[k].z + size.x) * 0.5f && Mathf.Abs(placed[k].y - at.y) < (placed[k].w + size.y) * 0.5f)
                            { at.y = placed[k].y + (placed[k].w + size.y) * 0.5f + 2f; k = -1; }
                        placed.Add(new Vector4(at.x, at.y, size.x, size.y));
                        label.style.left = at.x;
                        label.style.top = at.y;
                        label.style.display = DisplayStyle.Flex;
                        used++;
                    }
            }
            for (int i = used; i < tieLabels.Count; i++) tieLabels[i].style.display = DisplayStyle.None;
            linesLayer?.MarkDirtyRepaint();
        }

        static int Rank(TieKind k) => k switch
        {
            TieKind.Partner => 0, TieKind.Rival => 1, TieKind.Parent or TieKind.Child => 2, TieKind.Friend => 3,
            TieKind.Sibling => 4, TieKind.Mentor or TieKind.Apprentice => 5, TieKind.MadeUp => 6, _ => 7,
        };

        public static Color ColorOf(TieKind k) => k switch
        {
            TieKind.Partner => new Color(0.86f, 0.44f, 0.52f),
            TieKind.Parent or TieKind.Child or TieKind.Sibling => new Color(0.86f, 0.68f, 0.36f),
            TieKind.Friend => new Color(0.50f, 0.74f, 0.36f),
            TieKind.Rival => new Color(0.82f, 0.30f, 0.24f),
            TieKind.Mentor or TieKind.Apprentice => new Color(0.42f, 0.64f, 0.82f),
            TieKind.MadeUp => new Color(0.66f, 0.56f, 0.80f),
            _ => new Color(0.92f, 0.90f, 0.85f, 0.7f),
        };

        Vector2 ToPanel(Camera cam, Vector3 world, out bool ok)
        {
            var s = cam.WorldToScreenPoint(world);
            ok = s.z > 0f && s.x >= 0f && s.x <= Screen.width && s.y >= 0f && s.y <= Screen.height;
            return RuntimePanelUtils.ScreenToPanel(overlayRoot.panel, new Vector2(s.x, Screen.height - s.y));
        }

        bool Place(VisualElement tag, PeopleView pv, Camera cam, int id)
        {
            if (!pv.TryGetDollTop(PersonRef.Notable(id), out var w)) return false;
            var p = ToPanel(cam, w, out bool ok);
            if (!ok) return false;
            // keep clear of the card: a person under it has no tag
            if (IsOpen && sheet != null && sheet.worldBound.height > 0f && p.y > sheet.worldBound.yMin) return false;
            tag.style.left = p.x;
            tag.style.top = p.y - 10f;
            if (tag == pinTag) { var s = cam.WorldToScreenPoint(w); PinTagScreen = new Vector2(s.x, s.y); }
            return true;
        }

        static void SetTag(VisualElement tag, bool on) { if (tag != null) tag.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; }

        void ClearLines()
        {
            drawn.Clear(); drawnTo.Clear();
            foreach (var l in tieLabels) l.style.display = DisplayStyle.None;
            linesLayer?.MarkDirtyRepaint();
        }

        Label TieLabel(int i)
        {
            while (tieLabels.Count <= i)
            {
                var l = new Label { name = "pc-tie-" + tieLabels.Count, pickingMode = PickingMode.Ignore };
                l.AddToClassList("pc-tie");
                l.style.display = DisplayStyle.None;
                overlayRoot.Add(l);
                tieLabels.Add(l);
            }
            return tieLabels[i];
        }

        void DrawLines(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            foreach (var d in drawn)
            {
                // a soft dark under-stroke, so a line reads on grass and on paths alike
                p.strokeColor = new Color(0.12f, 0.1f, 0.08f, 0.35f);
                p.lineWidth = d.w + 5f;
                p.BeginPath(); p.MoveTo(d.a); p.QuadraticCurveTo(d.c, d.b); p.Stroke();
                p.strokeColor = new Color(d.col.r, d.col.g, d.col.b, 0.9f * d.col.a);
                p.lineWidth = d.w;
                p.BeginPath(); p.MoveTo(d.a); p.QuadraticCurveTo(d.c, d.b); p.Stroke();
                // a dot at the far end, on the other person
                p.fillColor = p.strokeColor;
                p.BeginPath(); p.Arc(d.b, d.w + 3f, 0f, 360f); p.Fill();
            }
        }

        // ================================================================== building the UI
        void EnsureUi()
        {
            if (overlayGo != null && cardGo != null) return;
            overlayGo = new GameObject("PersonOverlay") { hideFlags = HideFlags.DontSave };
            overlayGo.transform.SetParent(transform, false);
            var od = overlayGo.AddComponent<UIDocument>();
            od.panelSettings = GameUiPanel.Settings;
            od.sortingOrder = 9; // under the HUD
            overlayRoot = new VisualElement { name = "pc-overlay", pickingMode = PickingMode.Ignore };
            overlayRoot.AddToClassList("hud-fill");
            od.rootVisualElement.Add(overlayRoot);
            linesLayer = new VisualElement { name = "pc-lines", pickingMode = PickingMode.Ignore };
            linesLayer.AddToClassList("hud-fill");
            linesLayer.generateVisualContent += DrawLines;
            overlayRoot.Add(linesLayer);
            pinTag = Tag("pc-pin-tag", "pc-tag--pin", out pinTagText);
            nameTag = Tag("pc-name-tag", "pc-tag--name", out nameTagText);

            cardGo = new GameObject("PersonCard") { hideFlags = HideFlags.DontSave };
            cardGo.transform.SetParent(transform, false);
            var cd = cardGo.AddComponent<UIDocument>();
            cd.panelSettings = GameUiPanel.Settings;
            cd.sortingOrder = 12; // over the HUD's bottom row
            cardRoot = new VisualElement { name = "person-card-root", pickingMode = PickingMode.Ignore };
            cardRoot.AddToClassList("hud-fill");
            cardRoot.style.justifyContent = Justify.FlexEnd;
            cardRoot.style.display = DisplayStyle.None;
            cd.rootVisualElement.Add(cardRoot);
            sheet = new VisualElement { name = "person-card" };
            sheet.AddToClassList("sheet");
            sheet.AddToClassList("pc-sheet");
            sheet.style.maxHeight = Length.Percent(CardMaxShare * 100f);
            cardRoot.Add(sheet);

            var head = new VisualElement();
            head.AddToClassList("sheet-head");
            head.AddToClassList("pc-head");
            head.style.flexShrink = 0; // the scroll area gives way, never the name row
            var names = new VisualElement();
            names.style.flexShrink = 1; names.style.flexGrow = 1; names.style.flexBasis = 0; names.style.minWidth = 0; // wrap the line, never run under Pin
            names.Add(title = Text("", "sheet-title"));
            title.name = "person-card-name";
            names.Add(subtitle = Text("", "pc-sub"));
            subtitle.name = "person-card-sub";
            head.Add(names);
            pinButton = Btn("person-card-pin", "Pin", TogglePin, "pc-pin");
            head.Add(pinButton);
            head.Add(Btn("person-card-close", "Close", Close, "sheet-close"));
            sheet.Add(head);

            body = new ScrollView(ScrollViewMode.Vertical) { name = "person-card-body" };
            body.AddToClassList("pc-body");
            body.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            body.contentContainer.style.width = Length.Percent(100); // lines wrap at the card's width, never run off it
            sheet.Add(body);
            ApplySafeArea();
        }

        VisualElement Tag(string name, string cls, out Label text)
        {
            var t = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            t.AddToClassList("pc-tag");
            t.AddToClassList(cls);
            t.style.display = DisplayStyle.None;
            text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("pc-tag-text");
            t.Add(text);
            var point = new VisualElement { pickingMode = PickingMode.Ignore };
            point.AddToClassList("pc-tag-point");
            t.Add(point);
            overlayRoot.Add(t);
            return t;
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (sheet == null) return;
            var i = GameUiPanel.SafeInsets(lastSafe, Screen.width, Screen.height);
            sheet.style.paddingBottom = 32 + i.w;
            sheet.style.paddingLeft = 44 + i.x;
            sheet.style.paddingRight = 44 + i.z;
        }

        void Refresh(bool force)
        {
            var sim = Sim;
            if (sim == null || !IsOpen) return;
            var card = sim.PersonCardOf(OpenId);
            if (card == null) { Close(); return; }
            string s = Signature(card);
            Card = card;
            if (!force && s == sig) return;
            edges = sim.RelationshipGraph(true, OpenId);
            float scroll = body != null ? body.scrollOffset.y : 0f;
            Build(card);
            if (body != null) body.scrollOffset = new Vector2(0f, scroll);
        }

        static string Signature(PersonCard c)
        {
            var sb = new StringBuilder();
            sb.Append(c.id).Append('|').Append(c.age).Append('|').Append(c.alive).Append('|').Append(c.roleLine).Append('|').Append(c.pinned)
              .Append('|').Append(c.wish?.id ?? -1).Append('|').Append(c.relics.Count).Append('|').Append(c.quote?.text);
            foreach (var t in c.family.Concat(c.friends).Concat(c.rivals).Concat(c.mentors)) sb.Append('|').Append(t.otherId).Append(t.kind).Append(t.otherAlive);
            foreach (var st in c.recentStories) sb.Append('|').Append(st.entryId);
            return sb.ToString();
        }

        void Build(PersonCard c)
        {
            sig = Signature(c);
            title.text = c.fullName;
            var sub = new List<string>();
            if (!string.IsNullOrEmpty(c.roleLine)) sub.Add(Cap(c.roleLine));
            sub.Add(c.alive ? $"{c.age} years" : $"{c.bornYear}–{c.diedYear}");
            if (!string.IsNullOrEmpty(c.trait)) sub.Add(c.trait);
            subtitle.text = string.Join(" · ", sub);
            pinButton.text = c.pinned ? "Pinned" : "Pin";
            pinButton.EnableInClassList("pc-pin--on", c.pinned);
            pinButton.style.display = c.alive || c.pinned ? DisplayStyle.Flex : DisplayStyle.None;
            pinButton.SetEnabled(c.alive || c.pinned);

            body.Clear();
            var sim = Sim;
            if (c.memorial != null)
            {
                var m = c.memorial;
                var box = Section("person-card-memorial", "Remembered");
                box.Add(Text($"Died in year {m.diedYear}, aged {m.ageAtDeath}{(m.diedInOffice ? ", still in office" : "")}.", "sheet-text"));
                if (m.legendReasons.Count > 0) box.Add(Text("A legend of the box: " + string.Join(", ", m.legendReasons) + ".", "pc-note"));
                if (m.rememberedBy.Count > 0) // a line, not chips: the family and friends rows below hold the chips
                {
                    var names = m.rememberedBy.Select(t => t.otherName.Split(' ')[0]).ToList();
                    if (names.Distinct().Count() < names.Count) names = m.rememberedBy.Select(t => t.otherName).ToList(); // two Tildas: full names
                    string who = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
                    box.Add(Text("Remembered by " + who + ".", "pc-note"));
                }
            }
            if (c.quote != null && !string.IsNullOrEmpty(c.quote.text))
            {
                var q = Text($"“{c.quote.text}”", "pc-quote");
                q.name = "person-card-quote";
                body.Add(q);
            }
            if (c.wish != null && !string.IsNullOrEmpty(c.wishText))
            {
                var w = Section("person-card-wish", "Wishes for");
                w.Add(Text(c.wishText, "sheet-text"));
            }
            if (c.role != null || c.terms.Count > 0)
            {
                var r = Section("person-card-roles", "Roles");
                if (c.role != null)
                    r.Add(Text(Cap(c.role) + $" since year {c.roleSinceYear}" + (c.termEndsYear > 0 ? $", next vote in year {c.termEndsYear}" : ""), "sheet-text"));
                foreach (var t in c.terms.Where(x => x.toYear >= 0))
                    r.Add(Text($"{Cap(t.role)}, years {t.fromYear}–{t.toYear}", "pc-note"));
            }
            // one chip per person, strongest tie first (a partner who was once a friend shows as the partner); rivals always
            // show, even in the family (the drama)
            var listed = new HashSet<int>();
            List<PersonTie> Fresh(IEnumerable<PersonTie> ts) => ts.Where(t => listed.Add(t.otherId)).ToList();
            AddTies("person-card-family", "Family", Fresh(c.family));
            AddTies("person-card-rivals", "Rivals", c.rivals);
            foreach (var t in c.rivals) listed.Add(t.otherId);
            AddTies("person-card-friends", "Friends", Fresh(c.friends.OrderBy(t => t.kind == TieKind.Friend ? 0 : 1)));
            AddTies("person-card-mentors", "Teaching", Fresh(c.mentors));

            var stories = Section("person-card-stories", "Stories");
            var toggle = new VisualElement();
            toggle.AddToClassList("pc-toggle");
            var recent = Btn("person-card-recent", "Recent", () => { ShowingBest = false; Build(Card); }, "pc-tab");
            var best = Btn("person-card-best", "Best", () => { ShowingBest = true; Build(Card); }, "pc-tab");
            recent.EnableInClassList("pc-tab--on", !ShowingBest);
            best.EnableInClassList("pc-tab--on", ShowingBest);
            toggle.Add(recent); toggle.Add(best);
            stories.Add(toggle);
            var list = ShowingBest ? c.bestStories : c.recentStories;
            if (list.Count == 0) stories.Add(Text("Nothing told about them yet.", "pc-note"));
            int i = 0;
            foreach (var st in list)
            {
                var row = new VisualElement { name = "person-card-story-" + i++ };
                row.AddToClassList("pc-story");
                var tx = Text($"Year {st.year}. {st.text}", "pc-story-text");
                row.Add(tx);
                int entryId = st.entryId;
                var show = Btn("", "Show me", () => ShowMe(entryId), "pc-showme");
                show.name = row.name + "-show";
                row.Add(show);
                stories.Add(row);
            }
            if (c.relics.Count > 0)
            {
                var rl = Section("person-card-relics", "Relics");
                foreach (var r in c.relics) rl.Add(Text($"{r.name}. {r.label}", "sheet-text"));
            }
        }

        void AddTies(string name, string head, List<PersonTie> ties)
        {
            if (ties.Count == 0) return;
            var s = Section(name, head);
            s.Add(Chips(ties));
        }

        VisualElement Chips(List<PersonTie> ties)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("pc-chips");
            foreach (var t in ties)
            {
                int id = t.otherId;
                var b = Btn("person-card-tie-" + id, $"{t.otherName} · {t.label}", () => Open(id, true), "pc-chip");
                b.EnableInClassList("pc-chip--dead", !t.otherAlive);
                b.style.borderLeftColor = ColorOf(t.kind);
                wrap.Add(b);
            }
            return wrap;
        }

        VisualElement Section(string name, string head)
        {
            var s = new VisualElement { name = name };
            s.AddToClassList("pc-section");
            s.Add(Text(head, "pc-label"));
            body.Add(s);
            return s;
        }

        void ShowMe(int entryId)
        {
            var sim = Sim;
            var d = ShowMeDirector.Instance;
            var e = sim?.State.chronicle.FirstOrDefault(x => x.id == entryId);
            if (d == null || e == null) return;
            if (d.Show(e) != null) showMeFromCard = true;
            else GameHud.Instance?.Toast("That happened somewhere the camera can't go.", 2.5f);
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static Label Text(string s, string cls)
        {
            var l = new Label(s);
            l.AddToClassList(cls);
            return l;
        }

        static Button Btn(string name, string label, System.Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { name = name, text = label };
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        // ================================================================== debug (the debug panel, `script eval`)
        public static string DebugOpen(int notableId)
        {
            var v = Instance;
            if (v == null) return "no person card";
            return v.Open(notableId, true) ? "card: " + v.Card.fullName : "no notable #" + notableId;
        }

        public static string DebugPin(int notableId)
        {
            var sim = Sim;
            if (sim == null) return "no sim";
            if (notableId < 0) { sim.Unpin(); return "unpinned"; }
            return sim.Pin(notableId) ? "pinned " + sim.FindNotable(notableId)?.FullName : "can't pin #" + notableId;
        }

        public static string Status()
        {
            var v = Instance; var sim = Sim;
            if (v == null || sim == null) return "person card: not running";
            return $"card: {(v.IsOpen ? v.Card?.fullName : "closed")} · lines {v.LinesDrawn} · pin {(sim.Pinned != null ? sim.Pinned.FullName : "none")}{(v.PinTagShown ? " (tag shown)" : "")}";
        }
    }
}
