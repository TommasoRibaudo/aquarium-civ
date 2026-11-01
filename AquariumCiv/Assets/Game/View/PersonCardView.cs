using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    public enum CardTab { About, Recent, Best }

    /// <summary>
    /// Notables in the view (#39; game definition §4, first-session §5 "Person card", notable-roles §6). Added to the box at
    /// start-up (like <see cref="GlassVoice"/>):
    /// <list type="bullet">
    /// <item><b>Tap a person.</b> A tap on a notable's doll (<see cref="WorldTap"/>, called by <see cref="GlassVoice"/> before
    /// it hears a knock) opens their card and flies the camera so they stand above it. A tap on the world with a card open
    /// closes it. A tap that hits nobody is still the Keeper's knock.</item>
    /// <item><b>The person card</b> (<see cref="SimCore.PersonCardOf"/>, spec §4, board 3.4): a <see cref="GlassSheet"/> at 30 % of the
    /// screen, the person in view above it and kept there while they walk. Portrait, name, Pin and Close in one row, the subtitle
    /// on its own row, then About, Recent, Best. About holds their line for the belief stage, their wish, family, friends, rivals
    /// and mentors as chips (a tap opens that person's card), their roles and relics, and a story (question 35: never half empty).
    /// Recent and Best hold stories, each with "Show me" (<see cref="ShowMeDirector"/>). The dead get a memorial (when, how old,
    /// why they are a legend, who remembers them) and no Pin.</item>
    /// <item><b>Relationships visible.</b> While a card is open, light arcs run from the person's doll to the dolls of their
    /// ties (<see cref="SimCore.RelationshipGraph"/>), each kind with its own line and mark, its label over the far end and off
    /// the name tag: at most <see cref="MaxLines"/>, strongest first. They clear when the card closes.</item>
    /// <item><b>The pin.</b> One pinned notable (<see cref="SimCore.Pin"/>): a gold name tag follows their doll; the HUD's pin
    /// chip shows their name and opens their card. Saved with the box (<see cref="SimState.pinnedNotableId"/>).</item>
    /// </list>
    /// The card closes itself when the world isn't the player's to touch (a HUD sheet, camera mode, shape mode, a moment, the
    /// time-lapse, the last moment). Debug: static <c>Debug*</c> methods (the debug panel).
    /// <para><b>The crowd (#148, board S34Acting).</b> A tap on a crowd doll opens a small sheet of its own: first name, job and what
    /// they're doing now (<see cref="SimCore.CrowdCardOf"/>), no stories, family or pin. <see cref="IsOpen"/> stays the notables' card;
    /// <see cref="AnyOpen"/> is either.</para>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public partial class PersonCardView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static PersonCardView Instance { get; private set; }

        /// <summary>How close to a doll (panel units, 1080 wide) a tap must land to pick it: about half a 48 dp target.</summary>
        public const float PickRadius = 64f;
        /// <summary>#188: on a doll means within this share of its height on screen, and at least <see cref="TightMin"/> panel units.</summary>
        public const float TightShare = 0.33f, TightMin = 14f;
        /// <summary>Relationship lines drawn at most (never clutter).</summary>
        public const int MaxLines = 6;
        /// <summary>Where the person stands on screen after the fly-to (fraction of the height from the bottom): above the card.</summary>
        public const float CardAnchorY = 0.6f;
        /// <summary>The card's height (share of the screen): the sheet's 30 %, so the box stays in view (spec §2).</summary>
        public const float CardMaxShare = 0.3f;
        /// <summary>A person this close to the card's top edge (panel px) has walked under it, and the camera moves.</summary>
        public const float KeepClear = 40f;

        public int OpenId { get; private set; } = -1;
        public bool IsOpen => OpenId >= 0;
        public PersonCard Card { get; private set; }
        public CardTab Tab { get; private set; }
        public bool ShowingBest => Tab == CardTab.Best;
        /// <summary>Lines drawn last frame, and to whom (tests).</summary>
        public int LinesDrawn => drawn.Count;
        public IReadOnlyList<int> LinesTo => drawnTo;
        /// <summary>The pin tag is on screen, and where (screen pixels, y up; tests).</summary>
        public bool PinTagShown { get; private set; }
        public Vector2 PinTagScreen { get; private set; }
        public VisualElement CardRoot => cardRoot;
        /// <summary>#148: the crowd person whose small card is open (-1 = none), and that card.</summary>
        public int CrowdIndex { get; private set; } = -1;
        public bool CrowdOpen => CrowdIndex >= 0;
        public CrowdCard Crowd { get; private set; }
        /// <summary>A card is up, of either kind.</summary>
        public bool AnyOpen => IsOpen || CrowdOpen;

        PersonRef Who => IsOpen ? PersonRef.Notable(OpenId) : CrowdOpen ? PersonRef.Crowd(CrowdIndex) : PersonRef.None;
        GlassSheet ShownSheet => CrowdOpen ? crowdSheet : sheet;

        GameObject overlayGo, cardGo;
        VisualElement overlayRoot, linesLayer, cardRoot;
        GlassSheet sheet, crowdSheet;
        GlassPortrait crowdPortrait;
        Label crowdName, crowdJob, crowdDoing;
        readonly List<PersonRef> shownCrowd = new();
        GlassTag pinTag, nameTag;
        ScrollView body;
        GlassPortrait portrait;
        GlassTabs tabs;
        Label title, subtitle;
        GlassButton pinButton;
        readonly List<GlassTag> tieLabels = new();
        readonly List<(Vector2 a, Vector2 c, Vector2 b, TieKind kind)> drawn = new();
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
        int chipFlownId = -1;
        Vector3 chipFlownTo, lastFlight;
        const float ChipStillThere = 0.1f;
        bool flownForCard;
        float nextKeep;
        static readonly CustomStyleProperty<Color> LineProp = new("--tie-line"), InnerProp = new("--tie-inner"), UnderProp = new("--tie-under");
        Color lineCol = Color.white, innerCol = Color.green, underCol = new(0f, 0f, 0f, 0.35f);

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
            if (GameHud.Instance != null) { GameHud.Instance.SetPinChip(null, null); GameHud.Instance.SetOutsideSheet(null); }
            if (overlayGo != null) Destroy(overlayGo);
            if (cardGo != null) Destroy(cardGo);
            if (markGo != null) Destroy(markGo);
            if (markMesh != null) Destroy(markMesh);
        }

        bool IsOverCard(Vector2 screenPos) => (AnyOpen || BuildingOpen) && cardRoot != null && GameUiPanel.Over(cardRoot, screenPos);

        // ================================================================== the API

        /// <summary>A short tap reached the world at <paramref name="screenPos"/> (pixels, y up). One thing wins (#188, #160): a doll the finger
        /// is really on; then, with no card up, mess (the hint is what that tap says, see <see cref="CareView"/>); then the building under the
        /// finger; then a doll near it on open ground (the generous <see cref="PickRadius"/>). An open card closes rather than opens a building.
        /// True if the tap was used (no knock).</summary>
        public bool WorldTap(Vector2 screenPos)
        {
            if (OpenWho(PickDoll(screenPos))) return true;
            bool cardUp = AnyOpen || BuildingOpen;
            bool onMess = CareView.Instance != null && CareView.Instance.MessSaysSo(screenPos);
            if (!cardUp && onMess) { CareView.Instance.AnswerMessTap(); return true; }
            int building = PickBuilding(screenPos);
            if (building < 0 && !onMess && OpenWho(PickPerson(screenPos))) return true; // tester run 9: mess under the finger beats a doll merely near it
            if (IsOpen) { Close(); return true; }
            if (CrowdOpen) { CloseCrowd(); return true; }
            if (BuildingOpen) { CloseBuilding(); return true; }
            return building >= 0 && OpenBuilding(building); // the rest of the world is a knock
        }

        bool OpenWho(PersonRef who)
        {
            if (who.IsNotable) { Open(who.notableId, true); return true; }
            if (who.IsCrowd) { OpenCrowd(who.crowdIndex, true); return true; }
            return false;
        }

        /// <summary>The person whose doll is under a screen point (nearest within <see cref="PickRadius"/>, a notable on a tie),
        /// or <see cref="PersonRef.None"/>.</summary>
        public PersonRef PickPerson(Vector2 screenPos)
        {
            var who = PersonRef.None;
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || cam == null || Sim == null) return who;
            float bestD = PickRadius * Screen.width / GameUiPanel.ReferenceWidth;
            pv.NotablesShown(shown);
            foreach (int id in shown)
            {
                float d = DollDistance(pv, cam, PersonRef.Notable(id), screenPos);
                if (d < bestD && !Unseen(pv, cam, PersonRef.Notable(id))) { bestD = d; who = PersonRef.Notable(id); } // tester run 8: past the lot's box, the eaves still hide them
            }
            pv.CrowdShown(shownCrowd);
            foreach (var c in shownCrowd)
            {
                float d = DollDistance(pv, cam, c, screenPos);
                if (d < bestD && !Unseen(pv, cam, c)) { bestD = d; who = c; }
            }
            return who;
        }

        /// <summary>#188: the person whose doll the finger is really on or right next to (tighter than <see cref="PickRadius"/>, which covers
        /// most of a small town at default zoom), or <see cref="PersonRef.None"/>. The reach follows the doll's size on screen.</summary>
        public PersonRef PickDoll(Vector2 screenPos)
        {
            var who = PersonRef.None;
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || cam == null || Sim == null) return who;
            float best = float.MaxValue;
            void Try(PersonRef p)
            {
                float d = DollDistance(pv, cam, p, screenPos);
                if (d >= TightReach(pv, cam, p) || d >= best || Unseen(pv, cam, p)) return;
                best = d;
                who = p;
            }
            pv.NotablesShown(shown);
            foreach (int id in shown) Try(PersonRef.Notable(id));
            pv.CrowdShown(shownCrowd);
            foreach (var c in shownCrowd) Try(c);
            return who;
        }

        /// <summary>How near a doll counts as on it, in pixels: <see cref="TightShare"/> of its height on screen, at least <see cref="TightMin"/>
        /// panel units so a far doll can still be hit, and never past <see cref="PickRadius"/>.</summary>
        static float TightReach(PeopleView pv, Camera cam, PersonRef who)
        {
            float scale = Screen.width / GameUiPanel.ReferenceWidth;
            if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) return 0f;
            float tall = (((Vector2)cam.WorldToScreenPoint(top)) - (Vector2)cam.WorldToScreenPoint(feet)).magnitude;
            return Mathf.Clamp(tall * TightShare, TightMin * scale, PickRadius * scale);
        }

        /// <summary>Debug: how far a point is from a doll, against its reach.</summary>
        public string DebugDollReach(PersonRef who, Vector2 screenPos)
        {
            var pv = PeopleView.Find();
            return pv == null ? "" : $"d {DollDistance(pv, Camera.main, who, screenPos):0} reach {TightReach(pv, Camera.main, who):0}";
        }

        /// <summary>How far a screen point is from a doll, feet to head, in pixels.</summary>
        static float DollDistance(PeopleView pv, Camera cam, PersonRef who, Vector2 screenPos)
        {
            if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) return float.MaxValue;
            var a = cam.WorldToScreenPoint(feet);
            var b = cam.WorldToScreenPoint(top);
            return a.z <= 0f || b.z <= 0f ? float.MaxValue : SegmentDistance(screenPos, a, b);
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
                float d = DollDistance(pv, cam, PersonRef.Notable(id), screenPos);
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

        /// <summary>Open a notable's card (living or dead). <paramref name="fly"/>: the camera brings their doll above the card.
        /// <paramref name="logOpen"/> false: the caller logs it (the chronicle card it was opened from).</summary>
        public bool Open(int notableId, bool fly = true, bool logOpen = true)
        {
            var sim = Sim;
            var card = sim?.PersonCardOf(notableId);
            if (card == null) return false;
            EnsureUi();
            CloseCrowd(false);
            var hud = GameHud.Instance;
            if (hud != null) { hud.CloseSheet(); hud.SetCleanMode(false); }
            if (OpenId != notableId) { Tab = CardTab.About; if (logOpen) GameClock.Tel.CardOpened("person", notableId); }
            OpenId = notableId;
            Card = card;
            edges = sim.RelationshipGraph(true, notableId);
            Build(card);
            if (!sheet.IsOpen)
            {
                UiSound.Play(UiRole.Open); // #96
                cardRoot.style.display = DisplayStyle.Flex;
                sheet.Open();
            }
            if (hud != null) hud.SetOutsideSheet(sheet);
            flownForCard = fly && FlyTo(Who);
            return true;
        }

        /// <summary>#148: open a crowd person's small card. <paramref name="fly"/>: the camera brings their doll above it, as for a notable.</summary>
        public bool OpenCrowd(int index, bool fly = true)
        {
            var card = CrowdCardFor(index);
            if (card == null) return false;
            EnsureUi();
            if (IsOpen) Close();
            var hud = GameHud.Instance;
            if (hud != null) { hud.CloseSheet(); hud.SetCleanMode(false); }
            if (CrowdIndex != index) GameClock.Tel.CardOpened("crowd", index);
            CrowdIndex = index;
            Crowd = card;
            BuildCrowd(card);
            if (!crowdSheet.IsOpen)
            {
                UiSound.Play(UiRole.Open);
                cardRoot.style.display = DisplayStyle.Flex;
                crowdSheet.Open();
            }
            if (hud != null) hud.SetOutsideSheet(crowdSheet);
            flownForCard = fly && FlyTo(Who);
            return true;
        }

        public void CloseCrowd() => CloseCrowd(true);

        void CloseCrowd(bool sound)
        {
            if (!CrowdOpen) return;
            if (sound) UiSound.Play(UiRole.Close);
            CrowdIndex = -1;
            Crowd = null;
            flownForCard = false;
            if (GameHud.Instance != null) GameHud.Instance.SetOutsideSheet(null);
            crowdSheet.Close(() => { if (!AnyOpen && !BuildingOpen) cardRoot.style.display = DisplayStyle.None; });
        }

        /// <summary>The small card of a crowd person, as the planner has them now; null if nobody stands at that index.</summary>
        static CrowdCard CrowdCardFor(int index)
        {
            var sim = Sim;
            var pv = PeopleView.Find();
            var planner = pv != null ? pv.Planner : null;
            if (sim == null || planner == null) return null;
            var r = PersonRef.Crowd(index);
            return sim.CrowdCardOf(r.crowdIndex, planner.JobOf(r), planner.StageOf(r).ToString(), planner.Describe(planner.Plan(r, pv.Now)));
        }

        void BuildCrowd(CrowdCard c)
        {
            crowdName.text = c.name;
            crowdPortrait.Person = c.name;
            crowdJob.text = Cap(c.jobLine);
            crowdDoing.text = GlassType.Lines(c.doing, 15f, 1.35f);
        }

        void RefreshCrowd()
        {
            var card = CrowdCardFor(CrowdIndex);
            if (card == null) { CloseCrowd(); return; }
            Crowd = card;
            BuildCrowd(card);
        }

        public void Close()
        {
            if (!IsOpen) return;
            UiSound.Play(UiRole.Close); // #96
            OpenId = -1;
            Card = null;
            edges.Clear();
            flownForCard = false;
            if (GameHud.Instance != null) GameHud.Instance.SetOutsideSheet(null);
            if (sheet != null) sheet.Close(() => { if (!AnyOpen) cardRoot.style.display = DisplayStyle.None; });
            ClearLines();
            // Camera mode keeps the view it took over (it follows this person, #70): no easing back.
            if (showMeFromCard && ShowMeDirector.Instance != null) ShowMeDirector.Instance.End(GameHud.Instance == null || !GameHud.Instance.CameraMode);
            showMeFromCard = false;
        }

        /// <summary>The card's Pin button: pins this person (or unpins them if they are the pin).</summary>
        public void TogglePin()
        {
            var sim = Sim;
            if (sim == null || !IsOpen) return;
            bool on = sim.TogglePin(OpenId);
            if (on) GameClock.Tel.PinTapped(OpenId);
            UiSound.Play(UiRole.Toggle); // #96
            if (on && GameHud.Instance != null) GameHud.Instance.Toast($"Following {sim.FindNotable(OpenId)?.name}. Their stories come first.", 3f);
            Refresh(true);
            UpdateChip(sim, true);
        }

        bool FlyTo(PersonRef who)
        {
            var cam = Cam;
            var pv = PeopleView.Find();
            if (cam == null || pv == null || !pv.TryGetDollFeet(who, out var feet)) return false;
            float zoom = Mathf.Min(cam.zoom, BoxCameraMath.FlyPersonZoom);
            var focus = ShowMeDirector.AboveCard(cam, feet, zoom, cam.pitch, CardAnchorY);
            lastFlight = cam.FlyTo(focus, zoom, BoxCameraMath.FlyDefaultSeconds);
            return true;
        }

        // ================================================================== frame
        void Update()
        {
            var sim = Sim;
            if (sim == null) { Close(); CloseCrowd(); CloseBuilding(); SetTag(pinTag, false); return; }
            if (IsOpen && Blocked()) Close();
            if (CrowdOpen && Blocked()) CloseCrowd();
            if (BuildingOpen && (Blocked() || sim.BuildingCardOf(BuildingId) == null)) CloseBuilding();
            if (IsOpen && (Time.unscaledTime >= nextRefresh)) { nextRefresh = Time.unscaledTime + 1f; Refresh(false); }
            if (CrowdOpen && Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 1f; RefreshCrowd(); }
            if ((AnyOpen || BuildingOpen) && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); CloseCrowd(); CloseBuilding(); }
            if (AnyOpen) KeepInView();
            if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y) ApplySafeArea();
            UpdateChip(sim, false);
        }

        /// <summary>Rule 5: the card never covers its person. If they walk under it the camera follows, unless the player has moved it since.</summary>
        void KeepInView()
        {
            if (!flownForCard || Time.unscaledTime < nextKeep || overlayRoot?.panel == null) return;
            var cam = Cam;
            var pv = PeopleView.Find();
            if (cam == null || pv == null || cam.IsFlying) return;
            if ((cam.target - lastFlight).sqrMagnitude > ChipStillThere * ChipStillThere) { flownForCard = false; return; }
            if (!pv.TryGetDollTop(Who, out var top)) return;
            var at = ToPanel(Camera.main, top, out bool ok);
            if (ok && at.y < ShownSheet.worldBound.yMin - KeepClear) return;
            nextKeep = Time.unscaledTime + 2f;
            FlyTo(Who);
        }

        /// <summary>The world isn't the player's to touch now: the card closes and world labels hide.</summary>
        static bool Blocked()
        {
            var hud = GameHud.Instance;
            if (hud != null && (hud.CameraMode || hud.SheetOpen || !hud.IsVisible || hud.CleanMode)) return true;
            var shaping = ShapingView.Instance;
            if (shaping != null && shaping.ShapeMode) return true;
            // (the time-lapse screen's Who opens a card over the past on purpose: #79)
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
            hud.SetPinChip(text, id < 0 ? null : () => ChipTapped(id));
        }

        // D2: a tap flies to the pinned person, a second tap opens the card. "Second" means the camera is still where the first
        // took it (or on its way): move it off them and a tap flies again. Someone with no doll to fly to gets the card at once.
        void ChipTapped(int id)
        {
            var cam = Cam;
            bool there = id == chipFlownId && cam != null && (cam.IsFlying || (cam.target - chipFlownTo).sqrMagnitude < ChipStillThere * ChipStillThere);
            if (!there && FlyTo(PersonRef.Notable(id))) { chipFlownId = id; chipFlownTo = lastFlight; return; }
            chipFlownId = -1;
            Open(id, there);
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
            bool pinOn = pinned != null && pinned.alive && Place(pinTag, pv, cam, PersonRef.Notable(pinned.id));
            if (pinOn) pinTag.Word = pinned.name;
            SetTag(pinTag, pinOn);
            PinTagShown = pinOn;
            // the open card's person (when not the pin): a paper tag
            bool named = CrowdOpen || (IsOpen && Card != null && Card.alive && (pinned == null || pinned.id != OpenId));
            bool nameOn = !hide && named && Place(nameTag, pv, cam, Who);
            if (nameOn) nameTag.Word = CrowdOpen ? Crowd.name : Card.name;
            SetTag(nameTag, nameOn);
            // relationship lines
            drawn.Clear(); drawnTo.Clear(); leaders.Clear();
            int used = 0;
            placed.Clear();
            if (pinOn) Claim(pinTag);
            if (nameOn) Claim(nameTag);
            ClaimDoll(pv, cam);
            if (!hide && IsOpen && Card != null && Card.alive && pv.TryGetDollTop(PersonRef.Notable(OpenId), out var meW))
            {
                var me = ToPanel(cam, meW, out bool meOk);
                if (meOk) used = LayTies(sim, pv, cam, me);
            }
            for (int i = used; i < tieLabels.Count; i++) tieLabels[i].style.display = DisplayStyle.None;
            linesLayer?.MarkDirtyRepaint();
        }

        readonly List<RelMarker> ties = new();
        readonly Dictionary<int, string> tieWords = new();
        readonly Dictionary<int, TieKind> tieKinds = new();
        readonly List<LeaderRoute> leaders = new();
        readonly List<(int label, Vector2 at, Vector2 size, Vector2 anchor)> leadersWanted = new();
        readonly List<LabelBox> otherLabels = new();
        /// <summary>#194: labels drawn at most; the rest are "+N" and every tie is in the card. Labels shown last frame and leader lines (tests).</summary>
        public const int MaxLabels = 4;
        public int LeadersDrawn => leaders.Count;
        public int OverflowShown { get; private set; }
        const float TieTop = 120f, TieH = 66f, TieMaxW = 470f;

        static RelKind RelOf(TieKind k) => k switch
        {
            TieKind.Partner => RelKind.Partner, TieKind.Parent => RelKind.Parent, TieKind.Child => RelKind.Child, TieKind.Sibling => RelKind.Sibling,
            TieKind.Friend or TieKind.MadeUp => RelKind.Friend, TieKind.Rival => RelKind.Rival, TieKind.Mentor or TieKind.Apprentice => RelKind.Mentor, _ => RelKind.Other,
        };

        static string GroupWord(RelKind k) => k switch { RelKind.Child => "kids", RelKind.Sibling => "siblings", _ => "friends" };

        /// <summary>#194: the arcs, then the labels through <see cref="MarkerLayout"/>: people close together share one ("kids ×3"), at most <see cref="MaxLabels"/>
        /// show, none lands on another, and one pushed away from its person gets a thin line back. Returns the labels used.</summary>
        int LayTies(SimCore sim, PeopleView pv, Camera cam, Vector2 me)
        {
            ties.Clear(); tieWords.Clear(); tieKinds.Clear();
            float widest = 200f;
            foreach (var e in edges.OrderBy(x => Rank(x.kind)).ThenBy(x => x.a + x.b))
            {
                int other = e.a == OpenId ? e.b : e.a;
                if (!pv.TryGetDollTop(PersonRef.Notable(other), out var oW)) continue;
                var o = ToPanel(cam, oW, out bool ok);
                if (!ok || (o - me).magnitude < 20f) continue;
                if (drawn.Count < MaxLines)
                {
                    var mid = (me + o) * 0.5f;
                    var ctrl = mid + Vector2.down * Mathf.Min(160f, (o - me).magnitude * 0.3f); // an arc, up over the heads
                    drawn.Add((me, ctrl, o, e.kind));
                    drawnTo.Add(other);
                }
                var all = Card.family.Concat(Card.friends).Concat(Card.rivals).Concat(Card.mentors).Where(t => t.otherId == other).ToList();
                var tie = all.FirstOrDefault(t => t.kind == e.kind || (e.kind == TieKind.Parent && t.kind == TieKind.Child) || (e.kind == TieKind.Mentor && t.kind == TieKind.Apprentice)) ?? all.FirstOrDefault(); // the label says what the line's colour says
                string first = sim.FindNotable(other)?.name ?? "";
                string word = tie != null ? $"{first} · {tie.label}" : $"{first} · {e.label}";
                tieWords[other] = word;
                tieKinds[other] = tie?.kind ?? e.kind;
                widest = Mathf.Max(widest, word.Length * 19f + 100f);
                ties.Add(new RelMarker(other, RelOf(tieKinds[other]), o.x, o.y - TieTop));
            }
            float W = overlayRoot.layout.width, w = Mathf.Min(widest, TieMaxW);
            float bottom = (IsOpen && sheet.worldBound.height > 0f ? sheet.worldBound.yMin : overlayRoot.layout.height) - 16f;
            var res = MarkerLayout.Layout(ties, w, TieH, W, Mathf.Max(TieH, bottom - TieTop), MaxLabels);
            int used = 0;
            foreach (var p in res.placed)
            {
                var label = TieLabel(used++);
                bool group = p.Count > 1;
                label.Word = group ? $"{GroupWord(p.kind)} ×{p.Count}" : tieWords[p.ids[0]];
                label.Mark = MarkOf(tieKinds[p.ids[0]]);
                var size = new Vector2(Sized(label.resolvedStyle.width, label.Word.Length * 19f + 100f), Sized(label.resolvedStyle.height, TieH));
                var spot = new Vector2(p.x, p.y + TieTop);
                var at = Hit(spot, size) >= 0 ? FarEnd(spot, size) : spot; // the name tag and the doll are claimed too
                placed.Add(new Vector4(at.x, at.y, size.x, size.y));
                label.style.left = at.x;
                label.style.top = at.y;
                label.style.display = DisplayStyle.Flex;
                var anchor = new Vector2(p.anchorX, p.anchorY + TieTop);
                if (p.leader || (at - spot).magnitude > TieH * 1.2f || MarkerLayout.GapTo(new LabelBox(at.x, at.y, size.x, size.y), anchor.x, anchor.y) > MarkerLayout.LeaderGap) leadersWanted.Add((placed.Count - 1, at, size, anchor));
            }
            OverflowShown = res.overflow;
            if (res.overflow > 0)
            {
                var more = TieLabel(used++);
                more.Word = "+" + res.overflow;
                more.Mark = "person";
                var size = new Vector2(Sized(more.resolvedStyle.width, more.Word.Length * 19f + 100f), Sized(more.resolvedStyle.height, TieH));
                var at = FarEnd(me + new Vector2(0f, -TieH), size);
                placed.Add(new Vector4(at.x, at.y, size.x, size.y));
                more.style.left = at.x;
                more.style.top = at.y;
                more.style.display = DisplayStyle.Flex;
            }
            // Tester run 8: once every label has its place, each leader goes round the others.
            foreach (var (label, at, size, anchor) in leadersWanted)
            {
                otherLabels.Clear();
                for (int k = 0; k < placed.Count; k++)
                    if (k != label) otherLabels.Add(new LabelBox(placed[k].x, placed[k].y, placed[k].z, placed[k].w));
                leaders.Add(MarkerLayout.RouteLeader(new LabelBox(at.x, at.y, size.x, size.y), anchor.x, anchor.y, otherLabels));
            }
            leadersWanted.Clear();
            return used;
        }

        static int Rank(TieKind k) => k switch
        {
            TieKind.Partner => 0, TieKind.Rival => 1, TieKind.Parent or TieKind.Child => 2, TieKind.Friend => 3,
            TieKind.Sibling => 4, TieKind.Mentor or TieKind.Apprentice => 5, TieKind.MadeUp => 6, _ => 7,
        };

        /// <summary>The mark on a tie's label: the line says it too, so colour never carries it alone (board 3.2).</summary>
        public static string MarkOf(TieKind k) => k switch
        {
            TieKind.Rival => "alert",
            TieKind.Friend or TieKind.MadeUp => "star",
            TieKind.Mentor or TieKind.Apprentice => "book",
            TieKind.Shared => "scroll",
            _ => "person",
        };

        static float Sized(float measured, float estimate) => float.IsNaN(measured) || measured < 1f ? estimate : measured;

        /// <summary>Holds a tag's room so no tie label lands on it. A tag hangs from its point: its bottom edge is at <c>top</c>.</summary>
        void Claim(GlassTag tag)
        {
            float w = Sized(tag.resolvedStyle.width, tag.Word.Length * 22f + 80f), h = Sized(tag.resolvedStyle.height, 72f);
            placed.Add(new Vector4(tag.style.left.value.value, tag.style.top.value.value - h * 0.5f, w, h));
        }

        /// <summary>No tie label on the person the card is about.</summary>
        void ClaimDoll(PeopleView pv, Camera cam)
        {
            if (!IsOpen || pv == null || cam == null || overlayRoot.panel == null) return;
            if (!pv.TryGetDollTop(PersonRef.Notable(OpenId), out var top) || !pv.TryGetDollFeet(PersonRef.Notable(OpenId), out var feet)) return;
            var a = ToPanel(cam, top, out bool okA);
            var b = ToPanel(cam, feet, out bool okB);
            if (okA && okB) placed.Add(new Vector4((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 80f, Mathf.Abs(b.y - a.y) + 8f));
        }

        int Hit(Vector2 at, Vector2 size)
        {
            for (int k = 0; k < placed.Count; k++)
                if (Mathf.Abs(placed[k].x - at.x) < (placed[k].z + size.x) * 0.5f + 4f && Mathf.Abs(placed[k].y - at.y) < (placed[k].w + size.y) * 0.5f + 4f) return k;
            return -1;
        }

        /// <summary>A label's centre: touching the far end of its line (over it first, then around it), on screen, above the card, and clear of
        /// the name tag and of every earlier label. Only in a crowd does it leave the end, downward, then upward once the card is in the way.</summary>
        Vector2 FarEnd(Vector2 end, Vector2 size)
        {
            const float Edge = 16f, Off = 14f, TopMin = 120f;
            float halfW = size.x * 0.5f, halfH = size.y * 0.5f, W = overlayRoot.layout.width;
            float bottom = (IsOpen && sheet.worldBound.height > 0f ? sheet.worldBound.yMin : overlayRoot.layout.height) - halfH - Edge;
            float top = TopMin + halfH;
            Vector2 Keep(Vector2 p) => new(Mathf.Clamp(p.x, halfW + Edge, Mathf.Max(halfW + Edge, W - halfW - Edge)), Mathf.Clamp(p.y, top, Mathf.Max(top, bottom)));
            float dy = halfH + Off, dx = halfW + Off;
            var at = Keep(end + new Vector2(0f, -dy));
            if (Hit(at, size) < 0) return at;
            foreach (var near in new[] { new Vector2(dx, -dy), new Vector2(-dx, -dy), new Vector2(0f, dy), new Vector2(dx, dy), new Vector2(-dx, dy), new Vector2(dx, 0f), new Vector2(-dx, 0f) })
            {
                var p = Keep(end + near);
                if (Hit(p, size) < 0) return p;
            }
            float dir = 1f;
            for (int tries = 0; tries < 2 * MaxLines + 4; tries++)
            {
                int hit = Hit(at, size);
                if (hit < 0) break;
                float step = (placed[hit].w + size.y) * 0.5f + 8f;
                float next = placed[hit].y + dir * step;
                if (dir > 0f && next > bottom) { dir = -1f; next = placed[hit].y - step; }
                if (next < top) break;
                at.y = next;
            }
            return at;
        }

        Vector2 ToPanel(Camera cam, Vector3 world, out bool ok)
        {
            var s = cam.WorldToScreenPoint(world);
            ok = s.z > 0f && s.x >= 0f && s.x <= Screen.width && s.y >= 0f && s.y <= Screen.height;
            return RuntimePanelUtils.ScreenToPanel(overlayRoot.panel, new Vector2(s.x, Screen.height - s.y));
        }

        bool Place(VisualElement tag, PeopleView pv, Camera cam, PersonRef who)
        {
            if (!pv.TryGetDollTop(who, out var w)) return false;
            var p = ToPanel(cam, w, out bool ok);
            if (!ok) return false;
            // keep clear of the card: a person under it has no tag
            if (AnyOpen && ShownSheet != null && ShownSheet.worldBound.height > 0f && p.y > ShownSheet.worldBound.yMin) return false;
            // the tag hangs from its point (Game.uss translates it): keep its half width on screen
            float half = tag.resolvedStyle.width * 0.5f;
            float W = overlayRoot.layout.width;
            if (float.IsNaN(half)) half = 0f;
            tag.style.left = W > 0f ? Mathf.Clamp(p.x, half + 16f, Mathf.Max(half + 16f, W - half - 16f)) : p.x;
            tag.style.top = p.y - 10f;
            if (tag == pinTag) { var s = cam.WorldToScreenPoint(w); PinTagScreen = new Vector2(s.x, s.y); }
            return true;
        }

        static void SetTag(VisualElement tag, bool on) { if (tag != null) tag.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; }

        void ClearLines()
        {
            drawn.Clear(); drawnTo.Clear(); leaders.Clear(); OverflowShown = 0;
            foreach (var l in tieLabels) l.style.display = DisplayStyle.None;
            linesLayer?.MarkDirtyRepaint();
        }

        GlassTag TieLabel(int i)
        {
            while (tieLabels.Count <= i)
            {
                var l = new GlassTag("", TagKind.Tie, "person") { name = "pc-tie-" + tieLabels.Count, InWorld = true };
                l.style.display = DisplayStyle.None;
                overlayRoot.Add(l);
                tieLabels.Add(l);
            }
            return tieLabels[i];
        }

        // Board 3.2: paper, with the line telling the tie. Family is a paper line with a moss heart, friends and teachers a plain paper
        // line, rivals dotted, those who only share stories a thin one. A soft under-stroke, so a line reads on grass and on paths alike.
        const float LeaderWidth = 3.5f, PlainWidth = 8.25f, FamilyWidth = 13.75f, HeartWidth = 5.5f, SharedWidth = 5.5f, DotLength = 8.25f, DotGap = 16.5f;

        void DrawLines(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            foreach (var d in drawn)
            {
                bool family = d.kind is TieKind.Partner or TieKind.Parent or TieKind.Child or TieKind.Sibling;
                float w = family ? FamilyWidth : d.kind == TieKind.Shared ? SharedWidth : PlainWidth;
                if (d.kind == TieKind.Rival)
                {
                    Dotted(p, d.a, d.c, d.b, underCol, w + 5f);
                    Dotted(p, d.a, d.c, d.b, lineCol, w);
                }
                else
                {
                    Solid(p, d.a, d.c, d.b, underCol, w + 5f);
                    Solid(p, d.a, d.c, d.b, lineCol, w);
                    if (family) Solid(p, d.a, d.c, d.b, innerCol, HeartWidth);
                }
                // a dot at the far end, on the other person
                p.fillColor = lineCol;
                p.BeginPath(); p.Arc(d.b, w + 3f, 0f, 360f); p.Fill();
            }
            foreach (var l in leaders) // #194: a label pushed off its person
            {
                void Line()
                {
                    p.BeginPath(); p.MoveTo(new Vector2(l.ax, l.ay));
                    if (l.bends) p.LineTo(new Vector2(l.vx, l.vy));
                    if (l.twice) p.LineTo(new Vector2(l.wx, l.wy));
                    p.LineTo(new Vector2(l.bx, l.by)); p.Stroke();
                }
                p.strokeColor = underCol; p.lineWidth = LeaderWidth + 3f;
                Line();
                p.strokeColor = lineCol; p.lineWidth = LeaderWidth;
                Line();
            }
        }

        static void Solid(Painter2D p, Vector2 a, Vector2 c, Vector2 b, Color col, float w)
        {
            p.strokeColor = col;
            p.lineWidth = w;
            p.BeginPath(); p.MoveTo(a); p.QuadraticCurveTo(c, b); p.Stroke();
        }

        /// <summary>A painter has no dash, so the dots are walked along the curve.</summary>
        static void Dotted(Painter2D p, Vector2 a, Vector2 c, Vector2 b, Color col, float w)
        {
            const int Steps = 64;
            p.strokeColor = col;
            p.lineWidth = w;
            var prev = a;
            float along = 0f, period = DotLength + DotGap;
            p.BeginPath();
            for (int i = 1; i <= Steps; i++)
            {
                float t = i / (float)Steps, u = 1f - t;
                var at = u * u * a + 2f * u * t * c + t * t * b;
                if (along % period < DotLength) { p.MoveTo(prev); p.LineTo(at); }
                along += (at - prev).magnitude;
                prev = at;
            }
            p.Stroke();
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
            linesLayer.AddToClassList("pc-lines");
            linesLayer.generateVisualContent += DrawLines;
            linesLayer.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(LineProp, out var line)) lineCol = line;
                if (e.customStyle.TryGetValue(InnerProp, out var inner)) innerCol = inner;
                if (e.customStyle.TryGetValue(UnderProp, out var under)) underCol = under;
                linesLayer.MarkDirtyRepaint();
            });
            overlayRoot.Add(linesLayer);
            pinTag = Tag("pc-pin-tag", TagKind.Pin);
            nameTag = Tag("pc-name-tag", TagKind.Name);

            cardGo = new GameObject("PersonCard") { hideFlags = HideFlags.DontSave };
            cardGo.transform.SetParent(transform, false);
            var cd = cardGo.AddComponent<UIDocument>();
            cd.panelSettings = GameUiPanel.Settings;
            cd.sortingOrder = 12; // over the HUD's bottom row
            cardRoot = new VisualElement { name = "person-card-root", pickingMode = PickingMode.Ignore };
            cardRoot.AddToClassList("hud-fill");
            cardRoot.style.display = DisplayStyle.None;
            cd.rootVisualElement.Add(cardRoot);
            sheet = new GlassSheet { name = "person-card" };
            sheet.style.display = DisplayStyle.None;
            cardRoot.Add(sheet);

            var head = new VisualElement();
            head.AddToClassList("pc-head");
            portrait = new GlassPortrait("") { name = "person-card-portrait" };
            head.Add(portrait);
            title = new Label { name = "person-card-name", pickingMode = PickingMode.Ignore };
            title.AddToClassList("type-sheet");
            title.AddToClassList("pc-name");
            head.Add(title);
            pinButton = new GlassButton("Pin", TogglePin, ButtonKind.Secondary, UiRole.None) { name = "person-card-pin", Compact = true };
            head.Add(pinButton);
            head.Add(new GlassButton("", Close, ButtonKind.Icon, UiRole.None) { name = "person-card-close", Icon = "close" });
            sheet.Add(head);
            subtitle = new Label { name = "person-card-sub", pickingMode = PickingMode.Ignore };
            subtitle.AddToClassList("type-sub");
            subtitle.AddToClassList("pc-sub");
            sheet.Add(subtitle);

            tabs = new GlassTabs("About", "Recent", "Best") { name = "person-card-tabs" };
            tabs.AddToClassList("pc-tabs");
            tabs.Tab(0).name = "person-card-about";
            tabs.Tab(1).name = "person-card-recent";
            tabs.Tab(2).name = "person-card-best";
            tabs.Changed += i => { Tab = (CardTab)i; if (Card != null) Build(Card); body.scrollOffset = Vector2.zero; };
            sheet.Add(tabs);

            var scroll = new GlassScroll { SnapToRows = true };
            scroll.AddToClassList("pc-scroll");
            body = scroll.View;
            body.name = "person-card-body";
            body.contentContainer.AddToClassList("pc-scroll-pad");
            sheet.Add(scroll);
            BuildCrowdUi();
            BuildBuildingUi();
            ApplySafeArea();
        }

        /// <summary>Board S34Acting: portrait, first name and Close in one row, the job under them, then what they're doing. As tall as
        /// that takes, so the person stands clear of it.</summary>
        void BuildCrowdUi()
        {
            crowdSheet = new GlassSheet(SheetHeight.Content) { name = "crowd-card" };
            crowdSheet.AddToClassList("pc-crowd");
            crowdSheet.style.display = DisplayStyle.None;
            cardRoot.Add(crowdSheet);
            var head = new VisualElement();
            head.AddToClassList("pc-head");
            crowdPortrait = new GlassPortrait("") { name = "crowd-card-portrait" };
            head.Add(crowdPortrait);
            crowdName = new Label { name = "crowd-card-name", pickingMode = PickingMode.Ignore };
            crowdName.AddToClassList("type-sheet");
            crowdName.AddToClassList("pc-name");
            head.Add(crowdName);
            head.Add(new GlassButton("", CloseCrowd, ButtonKind.Icon, UiRole.None) { name = "crowd-card-close", Icon = "close" });
            crowdSheet.Add(head);
            crowdJob = new Label { name = "crowd-card-job", pickingMode = PickingMode.Ignore };
            crowdJob.AddToClassList("type-sub");
            crowdJob.AddToClassList("pc-sub");
            crowdSheet.Add(crowdJob);
            crowdDoing = new Label { name = "crowd-card-doing", pickingMode = PickingMode.Ignore, enableRichText = true };
            crowdDoing.AddToClassList("type-body");
            crowdDoing.AddToClassList("pc-line");
            crowdSheet.Add(crowdDoing);
        }

        GlassTag Tag(string name, TagKind kind)
        {
            var t = new GlassTag("", kind) { name = name, InWorld = true };
            t.style.display = DisplayStyle.None;
            overlayRoot.Add(t);
            return t;
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (sheet == null) return;
            var i = GameUiPanel.SafeInsets(lastSafe, Screen.width, Screen.height);
            bool tight = GlassSheet.IsShort(Screen.width, Screen.height);
            sheet.SetInsets(i, tight ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
            sheet.EnableInClassList("pc--short", tight);
            crowdSheet.SetInsets(i, tight ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
            buildingSheet.SetInsets(i, tight ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
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
            portrait.Person = c.name;
            var sub = new List<string>();
            if (!string.IsNullOrEmpty(c.roleLine)) sub.Add(Cap(c.roleLine));
            sub.Add(c.alive ? $"{c.age} years" : $"{c.bornYear}–{c.diedYear}");
            if (!string.IsNullOrEmpty(c.trait)) sub.Add(c.trait);
            subtitle.text = string.Join(" · ", sub);
            pinButton.text = c.pinned ? "Pinned" : "Pin";
            pinButton.Marked("pin", c.pinned);
            pinButton.style.display = c.alive || c.pinned ? DisplayStyle.Flex : DisplayStyle.None;
            tabs.Select((int)Tab, false);

            body.Clear();
            if (Tab == CardTab.About) BuildAbout(c);
            else BuildStories(Tab == CardTab.Best ? c.bestStories : c.recentStories);
        }

        void BuildAbout(PersonCard c)
        {
            if (c.memorial != null)
            {
                var m = c.memorial;
                var box = Section("person-card-memorial");
                box.Add(Body($"Died in year {m.diedYear}, aged {m.ageAtDeath}{(m.diedInOffice ? ", still in office" : "")}."));
                var note = new List<string>();
                if (m.legendReasons.Count > 0) note.Add("A legend of the box: " + string.Join(", ", m.legendReasons) + ".");
                if (m.rememberedBy.Count > 0) // a line, not chips: the family and friends rows below hold the chips
                {
                    var names = m.rememberedBy.Select(t => t.otherName.Split(' ')[0]).ToList();
                    if (names.Distinct().Count() < names.Count) names = m.rememberedBy.Select(t => t.otherName).ToList(); // two Tildas: full names
                    string who = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
                    note.Add("Remembered by " + who + ".");
                }
                if (note.Count > 0) box.Add(Note(string.Join(" ", note)));
            }
            if (c.quote != null && !string.IsNullOrEmpty(c.quote.text))
            {
                var q = Para($"“{c.quote.text}”", "type-quote", 14f, 1.3f);
                q.name = "person-card-quote";
                body.Add(q);
            }
            if (c.wish != null && !string.IsNullOrEmpty(c.wishText)) Section("person-card-wish").Add(Body(c.wishText));
            // one chip per person, strongest tie first (a partner who was once a friend shows as the partner); rivals always
            // show, even in the family (the drama)
            var ties = new VisualElement();
            ties.AddToClassList("pc-ties");
            body.Add(ties);
            var listed = new HashSet<int>();
            List<PersonTie> Fresh(IEnumerable<PersonTie> ts) => ts.Where(t => listed.Add(t.otherId)).ToList();
            AddTies(ties, "person-card-family", Fresh(c.family));
            AddTies(ties, "person-card-rivals", c.rivals);
            foreach (var t in c.rivals) listed.Add(t.otherId);
            AddTies(ties, "person-card-friends", Fresh(c.friends.OrderBy(t => t.kind == TieKind.Friend ? 0 : 1)));
            AddTies(ties, "person-card-mentors", Fresh(c.mentors));
            if (c.role != null || c.terms.Count > 0)
            {
                var r = Section("person-card-roles");
                if (c.role != null)
                    r.Add(Body(Cap(c.role) + $" since year {c.roleSinceYear}" + (c.termEndsYear > 0 ? $", next vote in year {c.termEndsYear}" : "")));
                foreach (var t in c.terms.Where(x => x.toYear >= 0))
                    r.Add(Note($"{Cap(t.role)}, years {t.fromYear}–{t.toYear}"));
            }
            if (c.relics.Count > 0)
            {
                var rl = Section("person-card-relics");
                foreach (var r in c.relics) rl.Add(Body($"{r.name}. {r.label}"));
            }
            // question 35: the sheet is never half empty, because a story follows
            var story = c.bestStories.FirstOrDefault() ?? c.recentStories.FirstOrDefault();
            if (story != null) StoryRow(Section("person-card-about-stories"), "person-card-about-story-0", story);
            else Section("person-card-about-stories").Add(Note(NoStories));
        }

        const string NoStories = "Nothing told about them yet.";

        void BuildStories(List<PersonStory> list)
        {
            var stories = Section("person-card-stories");
            if (list.Count == 0) stories.Add(Note(NoStories));
            int i = 0;
            foreach (var st in list) StoryRow(stories, "person-card-story-" + i++, st);
        }

        void StoryRow(VisualElement parent, string name, PersonStory st)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("pc-story");
            row.AddToClassList(GlassScroll.RowClass);
            var tx = Para($"Year {st.year}. {st.text}", "type-sub", 14f, 1.3f);
            tx.AddToClassList("pc-story-text");
            row.Add(tx);
            int entryId = st.entryId;
            row.Add(new GlassButton("Show me", () => ShowMe(entryId), ButtonKind.Secondary, UiRole.ShowMe) { name = name + "-show", Compact = true });
            parent.Add(row);
        }

        void AddTies(VisualElement parent, string name, List<PersonTie> ties)
        {
            if (ties.Count == 0) return;
            var wrap = new VisualElement { name = name };
            wrap.AddToClassList("pc-chips");
            foreach (var t in ties)
            {
                int id = t.otherId;
                var chip = new GlassChip($"{t.otherName} · {t.label}", () => Open(id, true)) { name = "person-card-tie-" + id, Gone = !t.otherAlive };
                chip.Face.AddToClassList(GlassScroll.RowClass); // what shows: the slot's clear edges may run past the view
                wrap.Add(chip);
            }
            parent.Add(wrap);
        }

        VisualElement Section(string name)
        {
            var s = new VisualElement { name = name };
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

        static Label Body(string s) => Para(s, "type-body", 15f, 1.35f);
        static Label Note(string s) => Para(s, "type-note", 13f, 1.3f);

        /// <summary>A paragraph at its type's px line height (spec §1): the tag, because USS has no such property.</summary>
        static Label Para(string s, string type, float pt, float ratio)
        {
            var l = new Label(GlassType.Lines(s, pt, ratio)) { pickingMode = PickingMode.Ignore, enableRichText = true };
            l.AddToClassList(type);
            l.AddToClassList("pc-line");
            l.AddToClassList(GlassScroll.RowClass);
            return l;
        }

        // ================================================================== debug (the debug panel, `script eval`)
        public static string DebugOpen(int notableId)
        {
            var v = Instance;
            if (v == null) return "no person card";
            return v.Open(notableId, true) ? "card: " + v.Card.fullName : "no notable #" + notableId;
        }

        /// <summary>Debug: the small card of the first crowd person on view (#148).</summary>
        public static string DebugOpenCrowd()
        {
            var v = Instance;
            var pv = PeopleView.Find();
            if (v == null || pv == null) return "no person card";
            pv.CrowdShown(v.shownCrowd);
            return v.shownCrowd.Count > 0 && v.OpenCrowd(v.shownCrowd[0].crowdIndex, true) ? "crowd card: " + v.Crowd.name : "nobody in the crowd on view";
        }

        /// <summary>Selects a tab by hand, or by the debug panel.</summary>
        public void ShowTab(CardTab tab) { if (IsOpen) tabs.Select((int)tab); }

        /// <summary>Debug: the open card's next tab (About, Recent, Best).</summary>
        public static string DebugNextTab()
        {
            var v = Instance;
            if (v == null || !v.IsOpen) return "open a card first";
            v.ShowTab((CardTab)(((int)v.Tab + 1) % 3));
            return "card tab: " + v.Tab;
        }

        /// <summary>Debug: open the first living notable's card, then camera mode: the camera follows their doll (#70).</summary>
        public static string DebugFollowInCameraMode()
        {
            var v = Instance; var sim = Sim; var hud = GameHud.Instance;
            if (v == null || sim == null || hud == null) return "no person card";
            var n = sim.Pinned != null && sim.Pinned.alive ? sim.Pinned : sim.State.notables.Find(x => x.alive);
            if (n == null || !v.Open(n.id, false)) return "nobody to follow";
            hud.SetCameraMode(true);
            return "camera mode follows " + n.FullName;
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
            return $"card: {(v.IsOpen ? v.Card?.fullName : v.CrowdOpen ? v.Crowd.name + " (crowd)" : "closed")} · lines {v.LinesDrawn} · pin {(sim.Pinned != null ? sim.Pinned.FullName : "none")}{(v.PinTagShown ? " (tag shown)" : "")}";
        }
    }
}
