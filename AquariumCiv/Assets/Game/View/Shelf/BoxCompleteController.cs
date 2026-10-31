using AquariumCiv.Audio;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// The box completes and box 2 arrives (#93; era-moment.md sections 8-9). When the sim says the peak is ready a prompt
    /// ("Finish {place}") waits in the thumb zone; it is never forced. Tapping it plays the last moment through
    /// <see cref="MomentDirector"/> (<see cref="MomentLibrary.LastMoment"/>): the lord's look back, the legends' lanterns,
    /// the founders with their bundles, the camera out to the shelf, the new box set down, the ruler and the crossing (the
    /// box completes there: <see cref="GameClock.CompleteBox"/> writes both boxes in one atomic save), the last wipe and the lamp,
    /// the plate engraved, the roll call, and the camera on the new box. Left before the crossing nothing moved and it replays;
    /// left after, box 2 is the active box and the rest is skipped.
    /// Afterwards a Shelf button opens a simple shelf view (the boxes side by side, tap one or pick it from the list to visit
    /// it read-only: its people live the time away, the plate and plinths show); "Back" returns to the active box.
    /// Debug: the static Debug* methods (debug panel, `script eval`).
    /// </summary>
    public class BoxCompleteController : MonoBehaviour, IMomentAnchors
    {
        public static BoxCompleteController Instance { get; private set; }
        public static bool AutoCreate = true;

        public MomentDirector Director { get; private set; }
        public bool Playing => Director != null && Director.State == MomentDirector.Phase.Playing;
        public bool ShelfOpen { get; private set; }
        public string SettingForNextBox = BoxSetting.DesertOasis;
        /// <summary>The last moment's progress for tests: the commit happened (box 2 exists).</summary>
        public bool Committed { get; private set; }
        public string LastInfo { get; private set; } = "";

        BoxView box;
        ShelfProps props;
        BoxCamera cam;
        UIDocument doc;
        VisualElement root, sheet, sheetList, banner;
        Button prompt, shelfBtn, backBtn, closeShelf;
        Label hint, bubble, caption, bannerText, roll;
        bool hudWasVisible = true;
        float nextRead, bubbleUntil;
        float lastToast = -1f;
        bool pressDown, pressMoved;
        Vector2 pressStart;
        float pressTime;
        // the moment
        BoxCompletion preview;
        readonly List<string> legends = new(), names = new(), lookback = new();
        readonly List<Transform> lanterns = new();
        Mesh lanternMesh;
        float shelfDrop = -1f;
        string stageSetting = BoxSetting.DesertOasis;
        float beatStartTime;
        int plateFirstLen;
        bool engravedLamp;
        bool momentActive;
        CameraPose shelfPose;

        /// <summary>The banner's top in panel units, under the HUD's top row (the title pill and Photo end near 330).</summary>
        public const float VisitBannerTop = 350f;

        /// <summary>A box's name as the HUD says it: the settlement's name, else <paramref name="unnamed"/> ("Your box").</summary>
        public static string BoxName(SimCore sim, string unnamed) => sim != null && !string.IsNullOrEmpty(sim.State.settlementName) ? sim.State.settlementName : unnamed;

        public static string SettingLabel(string setting) => setting switch
        {
            BoxSetting.DesertOasis => "Desert oasis",
            BoxSetting.MountainVillage => "Mountain village",
            _ => "River valley",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static BoxCompleteController Create() => new GameObject("BoxCompleteController").AddComponent<BoxCompleteController>();

        static GameClock Clock => GameClock.Instance;
        SimCore Sim => Clock != null ? Clock.Sim : null;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            Director = new MomentDirector(Commit);
            Director.BeatStarted += OnBeat;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 13;
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Build(doc.rootVisualElement);
            UiInput.Add(IsOverUi);
            if (Clock != null) Clock.ViewChanged += OnViewChanged;
        }

        void Start()
        {
            // the shelf views pull the camera far back (zoom 2 to 4 times the box fit distance): keep them inside the far plane
            var mc = Camera.main;
            if (mc != null && mc.farClipPlane < 420f) mc.farClipPlane = 420f;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            UiInput.Remove(IsOverUi);
            if (Clock != null) Clock.ViewChanged -= OnViewChanged;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (Playing) Leave();
            if (lanternMesh != null) Destroy(lanternMesh);
            Instance = null;
        }

        void OnApplicationPause(bool pause) { if (pause && Playing) Leave(); }

        bool IsOverUi(Vector2 p) => GameUiPanel.Over(root, p);

        BoxView Box => box != null ? box : box = FindAnyObjectByType<BoxView>();
        ShelfProps Props => props != null ? props : Box != null ? props = ShelfProps.Ensure(Box) : null;

        // ---------------- the view follows the clock ----------------
        void OnViewChanged()
        {
            var src = SimLayoutSource.Find();
            if (src != null) src.WorldChanged();
            SyncShelf();
            if (cam == null) cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();
            if (cam != null && !Playing && !ShelfOpen)
            {
                cam.CancelFlight();
                cam.target = new Vector3(0, 1f, 0); cam.zoom = 1f; cam.yaw = 28f; cam.pitch = 34f;
                cam.enabled = false; cam.enabled = true;
                cam.Apply();
            }
            UpdateBanner();
        }

        void SyncShelf()
        {
            var clock = Clock;
            if (clock == null || clock.Session == null || Props == null) return;
            var shelf = clock.Shelf;
            if (shelf == null) return;
            int shown = clock.Session.visit != null ? clock.Session.visitIndex : shelf.activeIndex;
            Props.HideIndex = Playing && !Committed ? -2 : Playing ? shelf.activeIndex : -1;
            Props.Sync(shelf, shown, !Playing || engravedLamp);
        }

        // ---------------- each frame ----------------
        void Update()
        {
            if (root == null || Clock == null || Clock.Session == null) return;
            if (Time.unscaledTime >= nextRead)
            {
                nextRead = Time.unscaledTime + 0.5f;
                if (!Playing) SyncShelf();
                UpdateBanner();
            }
            if (Playing) Play(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            else if (momentActive && Director.State == MomentDirector.Phase.Finished) Finish();
            else if (ShelfOpen) ShelfInput();
            UpdateButtons();
            if (lastToast > 0f && Time.unscaledTime > lastToast) { bubble.style.display = DisplayStyle.None; lastToast = -1f; }
            // a museum visit: Clean and Shape don't apply there (tester batch 2)
            var hudNow = GameHud.Instance;
            if (hudNow != null) hudNow.HideTools("museum", Clock.Visiting && !Playing);
        }

        bool HudFree()
        {
            var hud = GameHud.Instance;
            return hud == null || (hud.IsVisible && !hud.CameraMode && !hud.CleanMode && !hud.SheetOpen);
        }

        bool PeakWaiting
        {
            get
            {
                var clock = Clock;
                if (clock == null || clock.Session == null || clock.Visiting) return false;
                var s = clock.ActiveSim?.State;
                return s != null && s.peakReady && !s.boxComplete;
            }
        }

        void UpdateButtons()
        {
            bool free = HudFree() && !MomentPlayer.PromptsBlocked() && !Playing && !ShelfOpen && !(MomentPlayer.Instance != null && MomentPlayer.Instance.Playing);
            prompt.style.display = free && PeakWaiting ? DisplayStyle.Flex : DisplayStyle.None;
            if (prompt.resolvedStyle.display == DisplayStyle.Flex || free)
            {
                string t = "Finish " + (Clock.ActiveSim?.PlaceName ?? "the box");
                if (prompt.text != t) prompt.text = t;
            }
            var shelf = Clock.Shelf;
            bool hasShelf = shelf != null && shelf.boxes.Count > 1;
            shelfBtn.style.display = free && hasShelf ? DisplayStyle.Flex : DisplayStyle.None;
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            prompt.style.bottom = GameUiPanel.PromptBottom + i.w;
            shelfBtn.style.bottom = GameUiPanel.PromptBottom + i.w;
            banner.style.top = VisitBannerTop + i.y; // under the title pill and the Feedback / Photo corner
            bool hud = GameHud.Instance != null && GameHud.Instance.CameraMode;
            if (hud) banner.style.display = DisplayStyle.None;
        }

        void UpdateBanner()
        {
            var clock = Clock;
            bool visiting = clock != null && clock.Visiting && !Playing;
            var hud = GameHud.Instance;
            bool hide = hud != null && (hud.CameraMode || !hud.IsVisible);
            banner.style.display = visiting && !ShelfOpen && !hide ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visiting) return;
            var b = clock.Shelf.Box(clock.Session.visitIndex);
            if (b == null) return;
            string t = ShelfProps.PlateLines(b).Replace("\n", " · ");
            t = SettingLabel(b.setting) + " · " + t;
            if (bannerText.text != t) bannerText.text = t;
            string back = "Back to " + BoxName(clock.ActiveSim, "your box");
            if (backBtn.text != back) backBtn.text = back;
        }

        // ---------------- the last moment ----------------
        /// <summary>Starts the last moment. False if the peak is not ready, a moment is playing, or a visit is open.</summary>
        public bool Begin()
        {
            var clock = Clock;
            if (clock == null || Playing || !PeakWaiting || Props == null) return false;
            var sim = clock.ActiveSim;
            if (ShelfOpen) CloseShelf();
            var hud = GameHud.Instance;
            if (hud != null)
            {
                hud.CloseSheet(); hud.SetCleanMode(false); hud.SetCameraMode(false);
                hudWasVisible = hud.IsVisible; hud.SetVisible(false);
            }
            cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();
            var from = CameraPose.Default;
            if (cam != null)
            {
                cam.CancelFlight();
                from = new CameraPose(V(cam.target), cam.yaw, cam.pitch, cam.zoom);
                cam.enabled = false;
            }
            clock.Held = true;
            // who is on stage: a preview on a copy, so nothing of the box moves before the crossing
            var copy = new SimCore(clock.config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            preview = copy.CompleteBox();
            legends.Clear(); names.Clear(); lookback.Clear();
            foreach (var n in sim.State.notables.Where(n => !n.alive).OrderBy(n => n.diedMonth).Take(14)) legends.Add(n.FullName);
            names.AddRange(legends);
            foreach (var n in sim.State.notables.Where(n => n.alive).OrderByDescending(n => n.age).Take(16)) names.Add(n.FullName);
            lookback.Add($"{sim.PlaceName} began with a fire and a hut. We remember.");
            for (int e = 0; e <= (int)Era.Village; e++)
            {
                string title = sim.EraTitle((Era)e);
                if (!string.IsNullOrEmpty(title)) lookback.Add($"We remember {title.Replace("The Years of the ", "the year of the ").Replace("The ", "the ")}.");
            }
            lookback.Add("It was a good glass to live in.");
            stageSetting = SettingForNextBox;
            Committed = false; engravedLamp = false; shelfDrop = -1f;
            ClearLanterns();
            Props.ClearStage();
            Props.HideIndex = -2;
            var ctx = new MomentContext { place = sim.PlaceName, leaderName = preview?.lord?.name ?? "", leaderPresent = preview?.lord != null, year = (int)(sim.State.month / SimConfig.MonthsPerYear) };
            HideLines();
            momentActive = true;
            Director.Start(MomentLibrary.LastMoment(), ctx, from);
            LastInfo = "last moment started";
            Debug.Log($"[LastMoment] start · {Director.Beats.Count} beats · {Director.Length:0.#} s · {legends.Count} legends · {preview?.founders.Count} founders");
            return true;
        }

        /// <summary>The player leaves mid-moment: before the crossing nothing moved and it replays; after it box 2 is the active
        /// box and the rest is skipped.</summary>
        public MomentDirector.LeaveResult Leave()
        {
            var r = Director.Leave();
            if (r != MomentDirector.LeaveResult.NotPlaying) Finish();
            return r;
        }

        /// <summary>The crossing: the box completes. Both boxes are in the file when it returns.</summary>
        bool Commit()
        {
            var clock = Clock;
            var next = clock.CompleteBox(stageSetting, 0, force: false);
            Committed = next != null;
            Debug.Log($"[LastMoment] crossing: {(Committed ? "box completed, box " + clock.Shelf.activeIndex + " is the active box" : "did not complete")}");
            if (Committed)
            {
                var src = SimLayoutSource.Find();
                if (src != null) src.WorldChanged(); // the box on screen is the finished one (visit) until the new box arrives
                int first = ShelfProps.PlateLines(clock.Shelf.Box(clock.Session.visitIndex)).IndexOf('\n');
                plateFirstLen = Mathf.Max(0, first);
                SyncShelf();
                Props.SetLamp(false);
                Props.EngravePlate(0f);
                if (Props.PlateText != null) Props.PlateText.text = Props.PlateFull.Substring(0, plateFirstLen);
            }
            return Committed;
        }

        void Finish()
        {
            if (!momentActive) return;
            momentActive = false;
            var clock = Clock;
            bool crossed = Committed;
            Director.Leave(); // no-op once finished
            ClearLanterns();
            if (Props != null) { Props.ClearStage(); Props.HideIndex = -1; }
            if (clock != null)
            {
                clock.Held = false;
                if (clock.Visiting && Committed) clock.ShowActive(); // box 2 on screen
            }
            engravedLamp = true;
            var hud = GameHud.Instance;
            if (hud != null) hud.SetVisible(hudWasVisible);
            hint.style.display = bubble.style.display = caption.style.display = roll.style.display = DisplayStyle.None;
            if (cam != null) { cam.enabled = true; }
            OnViewChanged();
            if (Committed && clock != null && clock.Session.sim != null)
                Say($"{SettingLabel(clock.Shelf.Active?.setting)} · the founders unpack by a fire.", 5f);
            LastInfo = crossed ? "last moment done: box 2 is on" : "last moment left before the crossing (replays)";
            Debug.Log("[LastMoment] ended: " + LastInfo);
        }

        void Play(float dt)
        {
            ReadPointer(out bool down, out Vector2 pos);
            if (down && !pressDown && !GameUiPanel.Over(root, pos)) { pressDown = true; pressMoved = false; pressStart = pos; }
            if (!down) pressDown = false;
            if (pressDown && (pos - pressStart).sqrMagnitude > Screen.height * 0.02f * Screen.height * 0.02f) pressMoved = true;
            bool hurry = (pressDown && !pressMoved) || MomentPlayer.DebugHoldHurry;
            Director.Tick(dt, hurry);
            if (!Playing) { Finish(); return; }
            Stage(dt);
            if (cam != null && cam.enabled == false && Director.CameraGuided)
            {
                var p = Director.CameraNow(this);
                cam.target = new Vector3(p.target.x, p.target.y, p.target.z);
                cam.yaw = p.yaw; cam.pitch = p.pitch; cam.zoom = p.zoom;
                cam.Apply();
            }
            hint.style.display = Director.ShowHurryHint ? DisplayStyle.Flex : DisplayStyle.None;
            if (bubble.resolvedStyle.display == DisplayStyle.Flex && Director.Time >= bubbleUntil) bubble.style.display = DisplayStyle.None;
        }

        void OnBeat(MomentBeat b, int index)
        {
            var ctx = Director.Context;
            Debug.Log($"[LastMoment] beat {b.id} at {Director.Time:0.0} s");
            beatStartTime = Director.Time;
            if (string.IsNullOrEmpty(b.line)) { bubble.style.display = DisplayStyle.None; bubbleUntil = 0f; }
            switch (b.light)
            {
                case MomentLight.Morning: PeopleView.SetHourGently(8.5f); break;
                case MomentLight.Dusk: PeopleView.SetHourGently(19.5f); break;
            }
            if (!string.IsNullOrEmpty(b.line) && !b.line.Contains("{lookback}"))
            {
                bubble.text = ctx.Fill(b.line);
                bubble.style.display = DisplayStyle.Flex;
                bubbleUntil = Mathf.Min(Director.Time + 6f, Director.BeatEnd(index));
            }
            if (b.Has(BeatAction.LegendLanterns)) MakeLanterns();
            if (b.Has(BeatAction.GatherFounders))
            {
                var who = (preview?.founders ?? new List<Notable>()).Select(n => (n.id, false)).ToList();
                while (who.Count < 7) who.Add((100 + who.Count, who.Count > 4)); // the families: ~7 people
                Props.MakeFounders(who);
            }
            if (b.Has(BeatAction.SetDownBox)) { shelfDrop = 0f; Props.StageNewBox(stageSetting, 0f); }
            if (b.Has(BeatAction.LastWipe) && Committed) { engravedLamp = true; Props.SetLamp(true); }
            if (b.Has(BeatAction.ArriveNewBox))
            {
                Props.ClearStage();
                if (Clock.Visiting && Committed) Clock.ShowActive();
                Props.HideIndex = -1;
                caption.text = SettingLabel(stageSetting);
                caption.style.display = DisplayStyle.Flex;
                roll.style.display = DisplayStyle.None;
            }
            if (b.Has(BeatAction.RollCall)) roll.style.display = DisplayStyle.Flex;
            else roll.style.display = DisplayStyle.None;
            if (b.id != "newbox") caption.style.display = DisplayStyle.None;
        }

        // per-frame staging that depends on where we are in a beat
        void Stage(float dt)
        {
            var b = Director.CurrentBeat;
            if (b == null) return;
            float t = Director.Time - beatStartTime;
            if (b.id == "lookback" && lookback.Count > 0)
            {
                int i = Mathf.Min(lookback.Count - 1, Mathf.FloorToInt(t / 7f));
                if (bubble.text != lookback[i]) { bubble.text = lookback[i]; }
                bubble.style.display = DisplayStyle.Flex; bubbleUntil = float.MaxValue;
            }
            UpdateLanterns(dt);
            if (shelfDrop >= 0f && shelfDrop < 1f && Props != null)
            {
                shelfDrop = Mathf.Min(1f, shelfDrop + dt / 1.2f);
                Props.StageNewBox(stageSetting, shelfDrop);
            }
            if (b.id == "crossing" && Props != null)
            {
                Props.LayRuler(Mathf.Clamp01(t / 4f));
                float u = Mathf.Clamp01((t - 5f) / 13f);
                for (int i = 0; i < Props.FounderDolls.Count; i++) Props.PlaceFounder(i, u);
            }
            if (b.id == "plate" && Props != null && Props.PlateText != null)
            {
                float u = Mathf.Clamp01(t / 7f);
                int full = Props.PlateFull.Length;
                int n = Mathf.Clamp(Mathf.RoundToInt(plateFirstLen + (full - plateFirstLen) * u), 0, full);
                Props.PlateText.text = Props.PlateFull.Substring(0, n);
            }
            if (b.id == "rollcall" && names.Count > 0)
            {
                int start = Mathf.Min(Mathf.FloorToInt(t / 1.2f), Mathf.Max(0, names.Count - 1));
                roll.text = string.Join("\n", names.Skip(start).Take(5));
            }
        }

        // ---------------- the lanterns ----------------
        void MakeLanterns()
        {
            ClearLanterns();
            if (Box == null) return;
            if (lanternMesh == null)
            {
                var mb = new MeshBuilder(55) { jitter = 0.01f };
                mb.Ico(Vector3.zero, 0.07f, new Vector3(1f, 1.3f, 1f), Palette.Lamp, 0f);
                lanternMesh = mb.ToMesh("Lantern");
            }
            int n = Mathf.Max(legends.Count, 6);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Lantern") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(Box.transform, false);
                float a = i * 2.399f;
                go.transform.localPosition = new Vector3(Mathf.Cos(a) * 1.8f, 1.2f, Mathf.Sin(a) * 1.2f);
                go.AddComponent<MeshFilter>().sharedMesh = lanternMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Box.SolidMaterial;
                go.transform.localScale = Vector3.zero;
                lanterns.Add(go.transform);
            }
        }

        void UpdateLanterns(float dt)
        {
            if (lanterns.Count == 0) return;
            var b = Director.CurrentBeat;
            float t = Director.Time - (b != null && b.id == "lanterns" ? beatStartTime : Director.Time - 22f);
            for (int i = 0; i < lanterns.Count; i++)
            {
                if (lanterns[i] == null) continue;
                float k = Mathf.Clamp01((t - i * 1.1f) / 5f);
                float a = i * 2.399f;
                float x = Mathf.Cos(a) * Mathf.Lerp(1.8f, 2.7f, k), z = Mathf.Sin(a) * Mathf.Lerp(1.2f, 1.9f, k);
                lanterns[i].localPosition = new Vector3(x, Mathf.Lerp(1.2f, Land.GlassTop - 0.35f - (i % 3) * 0.2f, k * k * (3f - 2f * k)), z);
                lanterns[i].localScale = Vector3.one * Mathf.Min(1f, k * 4f) * (1.4f + 0.2f * Mathf.Sin(Time.unscaledTime * 2f + i));
            }
        }

        void ClearLanterns()
        {
            foreach (var l in lanterns) if (l != null) Destroy(l.gameObject);
            lanterns.Clear();
        }

        // ---------------- anchors ----------------
        public bool TryGet(MomentAnchor anchor, out MVec3 point)
        {
            switch (anchor)
            {
                case MomentAnchor.Shelf: point = new MVec3(ShelfProps.SlotStep * 0.5f, 0.8f, 0.3f); return true;
                case MomentAnchor.NewBox: point = new MVec3(ShelfProps.SlotStep, 1f, 0f); return true;
                case MomentAnchor.Box: point = new MVec3(0f, 1f, 0f); return true;
            }
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.TryGet(anchor, out point)) return true;
            point = new MVec3(0f, 1f, 0f);
            return true;
        }

        static MVec3 V(Vector3 v) => new(v.x, v.y, v.z);

        // ---------------- the shelf view ----------------
        /// <summary>Opens the shelf view: the camera pulls back to the boxes side by side and a list offers each.</summary>
        public bool OpenShelf()
        {
            var clock = Clock;
            if (clock == null || clock.Shelf == null || ShelfOpen || Playing) return false;
            var hud = GameHud.Instance;
            HideLines();
            if (hud != null) { hud.CloseSheet(); hud.SetCleanMode(false); }
            ShelfOpen = true;
            cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();
            int n = clock.Shelf.boxes.Count;
            float leftX = ShelfProps.SlotX(n - 2);
            float span = -leftX + 7f;
            shelfPose = new CameraPose(new MVec3(leftX * 0.5f, 1f, 0.6f), 8f, 36f, Mathf.Max(1.4f, span / 6.8f));
            if (cam != null)
            {
                cam.CancelFlight();
                cam.enabled = false;
                cam.target = new Vector3(shelfPose.target.x, shelfPose.target.y, shelfPose.target.z);
                cam.yaw = shelfPose.yaw; cam.pitch = shelfPose.pitch; cam.zoom = shelfPose.zoom;
                cam.Apply();
            }
            SyncShelf();
            RebuildSheet();
            sheet.style.display = DisplayStyle.Flex;
            Sfx.Play(CueLibrary.UiOpen); // #96
            return true;
        }

        public void CloseShelf()
        {
            if (!ShelfOpen) return;
            ShelfOpen = false;
            Sfx.Play(CueLibrary.UiClose); // #96
            sheet.style.display = DisplayStyle.None;
            if (cam != null) { cam.enabled = true; OnViewChanged(); }
        }

        void RebuildSheet()
        {
            sheetList.Clear();
            var clock = Clock;
            var shelf = clock.Shelf;
            int shown = clock.Session.visit != null ? clock.Session.visitIndex : shelf.activeIndex;
            foreach (var b in shelf.boxes.OrderBy(b => b.index))
            {
                var bx = b;
                string place = bx.state != null && !string.IsNullOrEmpty(bx.state.settlementName) ? bx.state.settlementName : "Box " + (bx.index + 1);
                string label = place + " · " + SettingLabel(bx.setting) + (bx.museum ? " · museum" : " · playing") + (bx.index == shown ? " · here" : "");
                var btn = new Button(() => PickBox(bx.index)) { name = "shelf-box-" + bx.index, text = label };
                btn.AddToClassList("hud-btn");
                btn.style.marginBottom = 14;
                btn.style.whiteSpace = WhiteSpace.Normal;
                sheetList.Add(btn);
            }
        }

        /// <summary>Choose a box from the shelf: a museum is visited, the active box is gone back to.</summary>
        public bool PickBox(int index)
        {
            var clock = Clock;
            var b = clock.Shelf.Box(index);
            if (b == null) return false;
            CloseShelf();
            if (b.museum)
            {
                if (clock.Visiting && clock.Session.visitIndex == index) return true;
                return Visit(index);
            }
            Back();
            return true;
        }

        public bool Visit(int index)
        {
            var clock = Clock;
            if (clock == null || Playing) return false;
            if (!clock.VisitBox(index, out var r)) return false;
            HideLines();
            string away = r.months > 0
                ? $"While you were away: {r.months / SimConfig.MonthsPerYear} years, {r.births} born, {r.deaths} gone."
                : "";
            if (away != "") Say(away, 6f);
            LastInfo = "visiting box " + index + (away != "" ? " · " + away : "");
            return true;
        }

        public void Back() { if (Clock != null && Clock.Visiting) { HideLines(); Clock.ShowActive(); } }

        /// <summary>Takes any said line down (this one's and the era moment's), e.g. "the founders unpack by a fire".</summary>
        void HideLines()
        {
            if (bubble != null) bubble.style.display = DisplayStyle.None;
            lastToast = -1f;
            if (MomentPlayer.Instance != null) MomentPlayer.Instance.HideSaid();
        }

        void Say(string text, float seconds)
        {
            if (bubble == null || string.IsNullOrEmpty(text)) return;
            bubble.text = text;
            bubble.style.display = DisplayStyle.Flex;
            lastToast = Time.unscaledTime + seconds;
        }

        // taps on a box in the shelf view
        bool tapDown; Vector2 tapStart; float tapTime;
        void ShelfInput()
        {
            ReadPointer(out bool down, out Vector2 pos);
            if (down && !tapDown && !GameUiPanel.Over(root, pos)) { tapDown = true; tapStart = pos; tapTime = Time.unscaledTime; }
            if (!down && tapDown)
            {
                tapDown = false;
                if ((pos - tapStart).magnitude < Screen.height * 0.02f && Time.unscaledTime - tapTime < 0.5f) TapShelf(tapStart);
            }
            if (!down && tapDown) tapDown = false;
        }

        void TapShelf(Vector2 screen)
        {
            var camera = Camera.main;
            if (camera == null || Box == null) return;
            var ray = camera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, 1f, 0));
            if (!plane.Raycast(ray, out float d)) return;
            var p = Box.transform.InverseTransformPoint(ray.GetPoint(d));
            var clock = Clock; var shelf = clock.Shelf;
            int shown = clock.Session.visit != null ? clock.Session.visitIndex : shelf.activeIndex;
            var slots = new List<(int index, float x)> { (shown, 0f) };
            int k = 0;
            foreach (var b in shelf.boxes.Where(b => b.index != shown).OrderBy(b => b.index)) slots.Add((b.index, ShelfProps.SlotX(k++)));
            foreach (var s in slots)
                if (Mathf.Abs(p.x - s.x) < 3.6f && Mathf.Abs(p.z) < 3f) { PickBox(s.index); return; }
        }

        static void ReadPointer(out bool down, out Vector2 pos)
        {
            pos = default; down = false;
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.press.isPressed) { down = true; pos = t.position.ReadValue(); break; }
            if (!down && Mouse.current != null && Mouse.current.leftButton.isPressed) { down = true; pos = Mouse.current.position.ReadValue(); }
        }

        // ---------------- UI ----------------
        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "boxcomplete-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            parent.Add(root);

            prompt = new Button(() => Begin()) { name = "complete-prompt", text = "Finish the box" };
            prompt.AddToClassList("hud-btn"); prompt.AddToClassList("hud-btn--primary");
            CenterPin(prompt, GameUiPanel.PromptBottom);
            root.Add(prompt);

            shelfBtn = new Button(() => OpenShelf()) { name = "shelf-open", text = "Shelf" };
            shelfBtn.AddToClassList("hud-btn"); shelfBtn.AddToClassList("hud-btn--small");
            shelfBtn.style.position = Position.Absolute; shelfBtn.style.left = 24; shelfBtn.style.bottom = GameUiPanel.PromptBottom;
            root.Add(shelfBtn);

            hint = Pill("boxcomplete-hint", MomentDirector.HurryHint, 120f, false);
            bubble = Pill("boxcomplete-bubble", "", 0f, true);
            caption = Pill("boxcomplete-caption", "", 0f, true);
            caption.style.top = 220f; caption.style.fontSize = 44;
            roll = Pill("boxcomplete-roll", "", 0f, true);
            roll.style.top = 360f; roll.style.fontSize = 36;

            banner = new VisualElement { name = "visit-banner" };
            banner.AddToClassList("hud-toast");
            banner.style.top = VisitBannerTop; banner.style.bottom = StyleKeyword.Auto;
            banner.style.flexDirection = FlexDirection.Column; banner.style.alignItems = Align.Center;
            banner.style.display = DisplayStyle.None;
            bannerText = new Label("") { name = "visit-plate", pickingMode = PickingMode.Ignore };
            bannerText.style.whiteSpace = WhiteSpace.Normal; bannerText.style.marginBottom = 14; bannerText.style.fontSize = 30;
            banner.Add(bannerText);
            backBtn = new Button(Back) { name = "shelf-back", text = "Back" };
            backBtn.AddToClassList("hud-btn");
            banner.Add(backBtn);
            root.Add(banner);

            sheet = new VisualElement { name = "shelf-sheet" };
            sheet.AddToClassList("hud-toast");
            sheet.style.bottom = 250f; sheet.style.flexDirection = FlexDirection.Column; sheet.style.alignItems = Align.Stretch;
            sheet.style.display = DisplayStyle.None; sheet.style.width = new Length(88, LengthUnit.Percent);
            var title = new Label("The shelf") { pickingMode = PickingMode.Ignore };
            title.style.marginBottom = 14; title.style.unityTextAlign = TextAnchor.MiddleCenter;
            sheet.Add(title);
            sheetList = new VisualElement { name = "shelf-list" };
            sheet.Add(sheetList);
            closeShelf = new Button(CloseShelf) { name = "shelf-close", text = "Close" };
            closeShelf.AddToClassList("hud-btn");
            sheet.Add(closeShelf);
            root.Add(sheet);
        }

        static void CenterPin(VisualElement e, float bottom)
        {
            e.style.position = Position.Absolute;
            e.style.bottom = bottom;
            e.style.left = new Length(50, LengthUnit.Percent);
            e.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        }

        Label Pill(string name, string text, float bottom, bool top)
        {
            var l = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList("hud-toast");
            l.style.bottom = bottom;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.display = DisplayStyle.None;
            if (top) { l.style.bottom = StyleKeyword.Auto; l.style.top = new Length(30, LengthUnit.Percent); }
            root.Add(l);
            return l;
        }

        // ---------------- debug (debug panel, `script eval`) ----------------
        public static string Status()
        {
            var c = Instance; var clock = Clock;
            if (c == null || clock == null || clock.Session == null) return "no box";
            var s = clock.ActiveSim.State;
            var shelf = clock.Shelf;
            return string.Format(CultureInfo.InvariantCulture, "{0} boxes · active {1} · {2}{3} · peak {4} · complete {5}{6}",
                shelf.boxes.Count, shelf.activeIndex, clock.Visiting ? "visiting " + clock.Session.visitIndex : "on screen: active", c.Playing ? " · moment " + c.Director.CurrentBeat?.id : "",
                s.peakReady, s.boxComplete, c.ShelfOpen ? " · shelf open" : "");
        }

        public static string DebugPeak()
        {
            var sim = Clock?.ActiveSim;
            if (sim == null) return "no sim";
            if (sim.State.boxComplete) return "this box is already complete";
            return sim.ForcePeakReady() ? "peak ready" : sim.State.peakReady ? "peak already ready" : "needs the town era (cross it first)";
        }

        /// <summary>Plays the last moment now (makes the peak ready first).</summary>
        public static string DebugMoment(string setting = BoxSetting.DesertOasis)
        {
            if (Instance == null) return "no controller";
            DebugPeak();
            Instance.SettingForNextBox = setting;
            return Instance.Begin() ? Status() : "cannot start: " + Status();
        }

        /// <summary>Completes the box right now, with no moment: the box becomes a museum, box 2 starts in <paramref name="setting"/>
        /// and is the box on screen.</summary>
        public static string DebugComplete(string setting = BoxSetting.DesertOasis)
        {
            var clock = Clock;
            if (clock == null || clock.ActiveSim == null || Instance == null) return "no clock";
            if (clock.ActiveSim.State.boxComplete || clock.Visiting) return "already complete / visiting";
            if (clock.ActiveSim.State.era != Era.Town) clock.ActiveSim.ForceNextEra();
            var next = clock.CompleteBox(setting, 0, force: true);
            if (next == null) return "could not complete";
            clock.ShowActive();
            return "box completed · box 2 in " + SettingLabel(setting) + " · " + Status();
        }

        public static string DebugVisit(int index = 0) => Instance != null && Instance.Visit(index) ? Status() : "cannot visit box " + index;
        public static string DebugBack() { if (Instance == null) return "no controller"; Instance.Back(); return Status(); }
        public static string DebugShelf() => Instance != null && (Instance.ShelfOpen ? (Instance.CloseShelf2()) : Instance.OpenShelf()) ? Status() : "no shelf yet";
        bool CloseShelf2() { CloseShelf(); return true; }
    }
}
