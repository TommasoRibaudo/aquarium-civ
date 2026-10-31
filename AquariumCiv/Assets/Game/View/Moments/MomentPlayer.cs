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
    /// Plays the era moment in the Slice scene (era-moment.md §2–§5, #88): the Unity side of <see cref="MomentDirector"/>.
    /// <list type="bullet">
    /// <item>While an era waits: a <b>Watch</b> button in the thumb zone (hidden in camera mode, clean mode, with a sheet
    /// open), and the piles in the box (<see cref="WaitingProps"/>). Very grimy glass first offers a wipe; never blocks.</item>
    /// <item>Watch: sheets and clean mode close, the HUD hides, the sim clock holds (<see cref="GameClock.Held"/>), the
    /// camera follows the rail (BoxCamera is switched off and posed from here; one finger still orbits on top of the rail
    /// and eases back on release). A press that stays put hurries the moment (2×); the "Hold to hurry" hint shows for 3 s.</item>
    /// <item>The cheer beat calls <c>SimCore.AdvanceEra</c>, then SimLayoutSource's rebuild wave refreshes the box.</item>
    /// <item>Leaving (app paused) before the cheer drops the moment: it replays next time.</item>
    /// </list>
    /// Beat staging here is placeholder (bubbles, a caption, puffs on the lots, the view clock for the light); #89/#90/#91
    /// do the real staging through <see cref="BeatStarted"/>. Debug: static <c>Debug*</c> methods (also `script eval`).
    /// </summary>
    public partial class MomentPlayer : MonoBehaviour, IMomentAnchors
    {
        public static MomentPlayer Instance { get; private set; }
        public static bool AutoCreate = true;

        /// <summary>A press that moves less than this fraction of the screen height hurries; more, and it orbits.</summary>
        public const float HurrySlopFraction = 0.02f;
        /// <summary>How long a speech bubble stays, in moment seconds (at most to the end of its beat).</summary>
        public const float BubbleSeconds = 4.5f;

        public MomentDirector Director { get; private set; }
        public readonly GuidedOffset Offset = new();
        public bool Playing => Director != null && Director.State == MomentDirector.Phase.Playing;
        /// <summary>The era waiting state (refreshed twice a second).</summary>
        public WaitingInfo Waiting { get; private set; } = new();
        /// <summary>The glance peek's line while an era waits ("" otherwise), for the glance when it lands.</summary>
        public string Glance => Waiting.waiting ? Waiting.glance : "";

        /// <summary>A beat starts (the staging hooks for #89/#90/#91).</summary>
        public event Action<MomentBeat, int> BeatStarted;
        public event Action<bool> MomentEnded; // true = it played to the end (or past the cheer), false = left before the cheer

        BoxCamera cam;
        bool hudWasVisible = true;
        RebuildWave wave;
        /// <summary>The rebuild wave of the moment playing (null before the wave beat): the AudioDirector adds its knocks.</summary>
        public RebuildWave Wave => wave;
        float lastTime;
        // input
        bool pressDown, pressMoved;
        Vector2 pressStart, lastPos;
        // UI
        UIDocument doc;
        VisualElement root, wipeOffer, notifyAsk;
        Button watchButton;
        Label hint, bubble, caption;
        float bubbleUntil;
        float sayUntil = -1f; // #92: a line said outside an era moment (a relic moment), in unscaled seconds
        float nextRead;
        CameraPose fromPose; // where the player had the camera before the moment (it eases back there after)
        bool hadCamera;
        string toolLine = ""; // the tool beat's hint, said once the moment is over (mid-moment a press hurries)
        /// <summary>The unlock captions ("Drag from the water") ask for a tool the player can't pick up until the shaping tray
        /// (#84) exists: off until then, the lines stay in <see cref="MomentLibrary"/> (tester batch 3).</summary>
        public static bool ToolCaptions = false;
        bool wipingForMoment; // clean mode was opened from the wipe offer
        // puffs
        readonly List<(Transform t, float age, float size)> puffs = new();
        Mesh puffMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static MomentPlayer Create() => new GameObject("MomentPlayer").AddComponent<MomentPlayer>();

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            Director = new MomentDirector(Cheer);
            Director.BeatStarted += OnBeat;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 12; // over the HUD, under the feedback sheet
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Build(doc.rootVisualElement);
            UiInput.Add(IsOverUi);
        }

        void OnDisable()
        {
            if (Instance != this) return;
            UiInput.Remove(IsOverUi);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (Playing) { Director.Leave(); leftByPlayer = true; EndMoment(false); }
            if (puffMesh != null) Destroy(puffMesh);
            Instance = null;
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && Playing) Leave();
        }

        bool IsOverUi(Vector2 p) => GameUiPanel.Over(root, p);

        // ---------------- watch / leave ----------------
        /// <summary>The Watch button. Very grimy glass first offers a wipe (<paramref name="skipWipeOffer"/> skips it).
        /// <paramref name="replay"/> replays the last era's moment without moving the era (debug, the time-lapse later).</summary>
        public bool Watch(bool skipWipeOffer = false, bool replay = false)
        {
            var sim = Sim;
            if (sim == null || Playing) return false;
            if (!replay && (!sim.State.eraReady || sim.IsFinalEra)) return false;
            if (replay && sim.State.era == Era.Camp) return false;
            if (!replay && !skipWipeOffer && EraWaiting.OfferWipeFirst(sim.State.grime))
            {
                wipeOffer.style.display = DisplayStyle.Flex;
                return false;
            }
            wipeOffer.style.display = DisplayStyle.None;
            var def = MomentLibrary.For(replay ? sim.State.era : sim.NextEra);
            momentEra = (int)def.toEra; momentStart = Time.unscaledTime; momentReplay = replay; leftByPlayer = false;
            var ctx = MomentContext.From(sim, replay);

            var hud = GameHud.Instance;
            if (hud != null)
            {
                hud.CloseSheet();
                hud.SetCleanMode(false);
                hud.SetCameraMode(false);
                hudWasVisible = hud.IsVisible;
                hud.SetVisible(false);
            }
            cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();
            var from = CameraPose.Default;
            if (cam != null)
            {
                cam.CancelFlight();
                from = new CameraPose(V(cam.target), cam.yaw, cam.pitch, cam.zoom);
                cam.enabled = false;
            }
            fromPose = from; hadCamera = cam != null;
            toolLine = "";
            sayUntil = -1f; bubble.style.display = DisplayStyle.None; // a relic line still up gives way to the moment
            if (GameClock.Instance != null) GameClock.Instance.Held = true;
            momentOpen = true;
            Offset.Reset();
            pressDown = false;
            wave = null;
            KnocksPlayed = 0;
            StillTaken = null; stillTried = false; MaskDanced = false;
            lastTime = 0f;
            var src = SimLayoutSource.Find();
            if (src != null) src.BeginMomentLook(def.toEra - 1, def.toEra); // the box shows the old look until the wave reaches each lot
            Director.Start(def, ctx, from);
            Debug.Log($"[Moment] {(replay ? "replay" : "watch")} {def.id} · {Director.Beats.Count} beats · {Director.Length:0.#} s");
            return true;
        }

        /// <summary>The player leaves mid-moment: before the cheer nothing moved and it replays next time.</summary>
        public MomentDirector.LeaveResult Leave()
        {
            var r = Director.Leave();
            if (r != MomentDirector.LeaveResult.NotPlaying) leftByPlayer = true;
            if (r != MomentDirector.LeaveResult.NotPlaying) EndMoment(r == MomentDirector.LeaveResult.AlreadyCrossed);
            return r;
        }

        bool Cheer()
        {
            var sim = Sim;
            bool ok = sim != null && sim.AdvanceEra();
            if (ok && GameClock.Instance != null) GameClock.Instance.Save();
            Debug.Log($"[Moment] cheer: era {(ok ? "moved to " + sim.State.era : "did not move")}");
            return ok;
        }

        int momentEra; float momentStart; bool momentReplay, leftByPlayer;
        // The director stays Finished after a moment: without this, anyone holding the clock later (the time-lapse) "ended"
        // it again and let the clock go (tester batch 3).
        bool momentOpen;

        void EndMoment(bool crossed)
        {
            momentOpen = false;
            if (!momentReplay) GameClock.Tel.EraMomentEnded(momentEra, leftByPlayer, Time.unscaledTime - momentStart); // #100: watched live or skipped
            ReleaseCamera();
            if (cam != null && hadCamera) cam.EaseTo(new Vector3(fromPose.target.x, fromPose.target.y, fromPose.target.z), fromPose.yaw, fromPose.pitch, fromPose.zoom, 1.4f);
            hadCamera = false;
            if (GameClock.Instance != null) GameClock.Instance.Held = false;
            var hud = GameHud.Instance;
            if (hud != null) hud.SetVisible(hudWasVisible);
            hint.style.display = DisplayStyle.None;
            bubble.style.display = DisplayStyle.None;
            caption.style.display = DisplayStyle.None;
            wave = null;
            var src = SimLayoutSource.Find();
            if (src != null) src.EndMomentLook();
            Debug.Log($"[Moment] ended ({(crossed ? "crossed" : "left before the cheer: it will replay")})");
            MomentEnded?.Invoke(crossed);
            if (crossed && toolLine != "" && ToolCaptions) Say(toolLine, 6f);
            toolLine = "";
        }

        void ReleaseCamera()
        {
            if (cam != null && !cam.enabled) cam.enabled = true; // BoxCamera re-syncs its targets from the pose on enable
        }

        // ---------------- each frame ----------------
        void Update()
        {
            if (root == null) return;
            if (Time.unscaledTime >= nextRead)
            {
                nextRead = Time.unscaledTime + 0.5f;
                Waiting = EraWaiting.Read(Sim?.State, Sim?.PlaceName);
                var box = FindAnyObjectByType<BoxView>();
                if (box != null)
                {
                    if (!box.TryGetComponent(out WaitingProps props)) props = box.gameObject.AddComponent<WaitingProps>();
                    // the piles stay up through the moment until the cheer moves the era
                    props.Sync(Waiting, Sim);
                    if (!box.TryGetComponent(out FestivalDressing fest)) fest = box.gameObject.AddComponent<FestivalDressing>();
                    fest.Sync(Sim); // the festival year: bunting and tables (#91)
                }
            }
            if (Playing) Play(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            else if (momentOpen && Director.State == MomentDirector.Phase.Finished && (cam != null && !cam.enabled || (GameClock.Instance != null && GameClock.Instance.Held)))
                EndMoment(true);
            UpdatePuffs(Time.unscaledDeltaTime);
            UpdateWatchButton();
            if (sayUntil > 0f && Time.unscaledTime >= sayUntil) { bubble.style.display = DisplayStyle.None; sayUntil = -1f; }
        }

        /// <summary>#92: the moment's speech bubble for a line said outside an era moment (a relic moment, the people at the
        /// glass), for <paramref name="seconds"/> real seconds. Ignored while an era moment plays (its own lines win) and in
        /// camera mode (no UI).</summary>
        public bool Say(string text, float seconds = BubbleSeconds)
        {
            if (bubble == null || Playing || string.IsNullOrEmpty(text)) return false;
            var hud = GameHud.Instance;
            if (hud != null && hud.CameraMode) return false;
            bubble.text = text;
            bubble.style.display = DisplayStyle.Flex;
            sayUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);
            LastSaid = text;
            return true;
        }

        /// <summary>Takes a <see cref="Say"/> line down now (the shelf opens, a museum visit starts or ends).</summary>
        public void HideSaid()
        {
            if (bubble == null || Playing || sayUntil < 0f) return;
            bubble.style.display = DisplayStyle.None;
            sayUntil = -1f;
        }

        /// <summary>The last line <see cref="Say"/> showed (tests).</summary>
        public string LastSaid { get; private set; } = "";

        void Play(float dt)
        {
            // Input: a press that stays put hurries; one that moves orbits on top of the rail.
            ReadPointer(out bool down, out Vector2 pos);
            float slop = Screen.height * HurrySlopFraction;
            if (down && !pressDown && !GameUiPanel.Over(root, pos)) { pressDown = true; pressMoved = false; pressStart = lastPos = pos; }
            if (!down) pressDown = false;
            float dragYaw = 0f, dragPitch = 0f;
            if (pressDown)
            {
                if ((pos - pressStart).sqrMagnitude > slop * slop) pressMoved = true;
                var d = pos - lastPos;
                lastPos = pos;
                if (pressMoved)
                {
                    dragYaw = BoxCameraMath.YawDegrees(d.x, Screen.height);
                    dragPitch = -BoxCameraMath.PitchDegrees(d.y, Screen.height);
                }
            }
            bool hurry = (pressDown && !pressMoved) || DebugHoldHurry;
            Offset.Step(dt, pressDown && pressMoved, dragYaw, dragPitch);

            Director.Tick(dt, hurry);
            if (wave != null)
            {
                foreach (var s in wave.Due(lastTime, Director.Time)) Puff(s);
            }
            lastTime = Director.Time;
            UnrollCobbles();
            TakeStillWhenFramed();

            if (cam != null)
            {
                if (Director.CameraGuided)
                {
                    var p = Director.CameraNow(this, Offset);
                    cam.target = new Vector3(p.target.x, p.target.y, p.target.z);
                    cam.yaw = p.yaw; cam.pitch = p.pitch; cam.zoom = p.zoom;
                    cam.Apply();
                }
                else ReleaseCamera();
            }
            hint.style.display = Director.ShowHurryHint ? DisplayStyle.Flex : DisplayStyle.None;
            if (bubble.resolvedStyle.display == DisplayStyle.Flex && Director.Time >= bubbleUntil) bubble.style.display = DisplayStyle.None;
            if (!Playing) EndMoment(true);
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

        // ---------------- beats (placeholder staging) ----------------
        void OnBeat(MomentBeat b, int index)
        {
            var ctx = Director.Context;
            Debug.Log($"[Moment] beat {b.id} at {Director.Time:0.0} s");
            switch (b.light)
            {
                case MomentLight.Dawn: PeopleView.SetHourGently(6.5f); break;
                case MomentLight.Morning: PeopleView.SetHourGently(8.5f); break;
                case MomentLight.Midday: PeopleView.SetHourGently(12.2f); break;
                case MomentLight.Evening: PeopleView.SetHourGently(18f); break;
                case MomentLight.Dusk: PeopleView.SetHourGently(19.5f); break;
            }
            if (b.Has(BeatAction.AskNotification)) notifyAsk.style.display = DisplayStyle.Flex;
            else if (b.Has(BeatAction.ToolUnlock)) toolLine = ctx.Fill(b.line); // shown after the moment: a press now only hurries
            else if (!string.IsNullOrEmpty(b.line))
            {
                bubble.text = ctx.Fill(b.line);
                bubble.style.display = DisplayStyle.Flex;
                bubbleUntil = Mathf.Min(Director.Time + BubbleSeconds, Director.BeatEnd(index)); // moment time: hurrying shortens it, never spills into the next beat
            }
            if (!string.IsNullOrEmpty(b.caption))
            {
                caption.text = ctx.Fill(b.caption);
                caption.style.display = DisplayStyle.Flex;
            }
            if (b.Has(BeatAction.FestivalStart)) caption.style.display = DisplayStyle.None;
            if (b.Has(BeatAction.RebuildWave)) StartWave(b, index);
            Stage(b, index);
            BeatStarted?.Invoke(b, index);
        }

        void StartWave(MomentBeat b, int index)
        {
            var st = Sim?.Settlement;
            if (st == null) return;
            var lots = st.RebuildOrder().Where(l => !l.retired && l.kind != AquariumCiv.Sim.Settle.LotKind.Camp).Select(l => (l.id, new MVec3(l.x, 0f, l.z)));
            var centre = new MVec3(st.Map.greenX, 0f, st.Map.greenZ);
            wave = new RebuildWave(lots, centre, Director.BeatStart(index), Director.BeatEnd(index) - Director.BeatStart(index));
            lastTime = Mathf.Min(lastTime, Director.BeatStart(index) - 1e-3f);
        }

        // A placeholder puff where a lot rebuilds: a pale ball that swells and fades (#89/#90 do the real dust and swap).
        void Puff(RebuildWave.Step s)
        {
            var lot = Sim?.Settlement?.Lots.FirstOrDefault(l => l.id == s.lotId);
            if (lot == null) return;
            var src = SimLayoutSource.Find();
            if (src != null) src.MomentLot(s.lotId); // the lot rebuilds in place, in the new era's look
            PuffAt(lot.x, lot.z, 1f);
            WaveKnock(s);
        }

        void PuffAt(float x, float z, float size)
        {
            var box = FindAnyObjectByType<BoxView>();
            if (box == null) return;
            if (puffMesh == null)
            {
                var mb = new MeshBuilder(5) { jitter = 0.02f };
                mb.Ico(Vector3.zero, 0.1f, Vector3.one, Palette.Plaster, 0.2f);
                puffMesh = mb.ToMesh("MomentPuff");
            }
            var go = new GameObject("MomentPuff") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(box.transform, false);
            go.transform.localPosition = new Vector3(x, box.GroundHeight(x, z) + 0.08f, z);
            go.AddComponent<MeshFilter>().sharedMesh = puffMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = box.SolidMaterial;
            go.transform.localScale = Vector3.zero;
            puffs.Add((go.transform, 0f, size));
        }

        void UpdatePuffs(float dt)
        {
            const float life = 0.7f;
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var (t, age, size) = puffs[i];
                age += dt;
                if (t == null || age >= life) { if (t != null) Destroy(t.gameObject); puffs.RemoveAt(i); continue; }
                float u = age / life;
                t.localScale = Vector3.one * (Mathf.Sin(u * Mathf.PI) * 2.2f * size);
                puffs[i] = (t, age, size);
            }
        }

        // ---------------- anchors ----------------
        public bool TryGet(MomentAnchor anchor, out MVec3 point)
        {
            point = default;
            var sim = Sim;
            var pv = PeopleView.Find();
            var box = FindAnyObjectByType<BoxView>();
            switch (anchor)
            {
                case MomentAnchor.Box:
                    point = new MVec3(0f, 1f, 0f);
                    return true;
                case MomentAnchor.Hearth:
                case MomentAnchor.Square:
                case MomentAnchor.Steps:
                {
                    var st = sim?.Settlement;
                    if (st == null || box == null) return false;
                    var w = box.transform.TransformPoint(new Vector3(st.Map.greenX, box.GroundHeight(st.Map.greenX, st.Map.greenZ), st.Map.greenZ));
                    point = V(w);
                    return true;
                }
                case MomentAnchor.Leader:
                case MomentAnchor.Elder:
                {
                    if (sim == null || pv == null) return false;
                    var who = anchor == MomentAnchor.Leader ? sim.HolderOf(SimCore.LeaderOf(sim.State.era))
                        : sim.State.notables.Where(n => n.alive).OrderByDescending(n => n.age).FirstOrDefault();
                    if (who == null || !pv.TryGetDollPosition(AquariumCiv.Life.PersonRef.Notable(who.id), out var p)) return false;
                    var w = pv.transform.TransformPoint(new Vector3(p.x, 0f, p.y));
                    point = new MVec3(w.x, 1f, w.z);
                    return true;
                }
                case MomentAnchor.WaveFront:
                {
                    if (wave == null || box == null) return false;
                    int done = wave.DoneBy(Director.Time);
                    if (done == 0) return false;
                    var lot = sim?.Settlement?.Lots.FirstOrDefault(l => l.id == wave.Steps[done - 1].lotId);
                    if (lot == null) return false;
                    point = V(box.transform.TransformPoint(new Vector3(lot.x, 0f, lot.z)));
                    point.y = 1f;
                    return true;
                }
            }
            return false;
        }

        static MVec3 V(Vector3 v) => new(v.x, v.y, v.z);

        // ---------------- UI ----------------
        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "moment-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            parent.Add(root);

            watchButton = new Button(() => Watch()) { name = "moment-watch", text = "Watch" };
            watchButton.AddToClassList("hud-btn");
            watchButton.AddToClassList("hud-btn--primary");
            Pin(watchButton, 330f);
            root.Add(watchButton);

            hint = Pill("moment-hint", MomentDirector.HurryHint, 120f);
            bubble = Pill("moment-bubble", "", 0f);
            bubble.style.bottom = StyleKeyword.Auto;
            bubble.style.top = new Length(30, LengthUnit.Percent);
            caption = Pill("moment-caption", "", 0f);
            caption.style.bottom = StyleKeyword.Auto;
            caption.style.top = 220f;
            caption.style.fontSize = 44;

            wipeOffer = Card("moment-wipe", EraWaiting.WipeOffer,
                ("moment-wipe-yes", "Wipe", () => { wipeOffer.style.display = DisplayStyle.None; wipingForMoment = true; if (GameHud.Instance != null) GameHud.Instance.SetCleanMode(true); }),
                ("moment-wipe-no", EraWaiting.WatchAnyway, () => Watch(true)));
            notifyAsk = Card("moment-notify", "Want a note when the next one is ready?",
                ("moment-notify-yes", "Yes", () => AnswerNotify(true)),
                ("moment-notify-no", "Not now", () => AnswerNotify(false)));
        }

        void AnswerNotify(bool yes)
        {
            notifyAsk.style.display = DisplayStyle.None;
            if (Sim != null) MomentContext.MarkNotifyAsked(Sim.State);
            PlayerPrefs.SetInt("moment.notify", yes ? 1 : 0); // the local notification itself comes with the notification work
            if (GameClock.Instance != null) GameClock.Instance.Save();
        }

        static void Pin(VisualElement e, float bottom)
        {
            e.style.position = Position.Absolute;
            e.style.bottom = bottom;
            e.style.alignSelf = Align.Center;
            e.style.left = new Length(50, LengthUnit.Percent);
            e.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        }

        Label Pill(string name, string text, float bottom)
        {
            var l = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList("hud-toast");
            l.style.bottom = bottom;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.display = DisplayStyle.None;
            root.Add(l);
            return l;
        }

        VisualElement Card(string name, string question, params (string name, string label, Action click)[] buttons)
        {
            var card = new VisualElement { name = name };
            card.AddToClassList("hud-toast");
            card.style.bottom = 330f;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.display = DisplayStyle.None;
            var q = new Label(question) { pickingMode = PickingMode.Ignore };
            q.style.whiteSpace = WhiteSpace.Normal;
            q.style.marginBottom = 20;
            card.Add(q);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            foreach (var (n, label, click) in buttons)
            {
                var b = new Button(click) { name = n, text = label };
                b.AddToClassList("hud-btn");
                b.style.marginLeft = b.style.marginRight = 12;
                row.Add(b);
            }
            card.Add(row);
            root.Add(card);
            return card;
        }

        /// <summary>The player is inside something else (a person card, the time-lapse, Show me, the first run's sheets):
        /// prompts that float over the world wait (tester batch 3).</summary>
        public static bool PromptsBlocked()
        {
            var card = PersonCardView.Instance;
            if (card != null && card.IsOpen) return true;
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return true;
            var sm = ShowMeDirector.Instance;
            if (sm != null && sm.Busy) return true;
            var fr = FirstRunView.Instance;
            return fr != null && (fr.CardOpen || fr.NameSheetOpen);
        }

        void UpdateWatchButton()
        {
            var hud = GameHud.Instance;
            // wiping after the offer: once the glass is under the offer's threshold Watch shows in clean mode too (tester
            // batch 2: junk left in the box kept clean mode on and Watch hidden)
            if (hud == null || !hud.CleanMode) wipingForMoment = false;
            bool wipedEnough = wipingForMoment && Sim != null && !EraWaiting.OfferWipeFirst(Sim.State.grime);
            bool hudFree = hud == null || (hud.IsVisible && !hud.CameraMode && (!hud.CleanMode || wipedEnough) && !hud.SheetOpen);
            bool show = Waiting.waiting && !Playing && hudFree && !PromptsBlocked() && wipeOffer.resolvedStyle.display == DisplayStyle.None;
            watchButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show && watchButton.text != Waiting.watchLabel) watchButton.text = Waiting.watchLabel;
            // the safe area: keep the button above the home indicator and the HUD's bottom row
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            watchButton.style.bottom = GameUiPanel.PromptBottom + i.w;
            if (hud != null && (hud.CameraMode || hud.SheetOpen)) wipeOffer.style.display = DisplayStyle.None;
        }

        // ---------------- debug (debug panel, `script eval`) ----------------
        public static string Status()
        {
            var m = Instance;
            if (m == null) return "no MomentPlayer";
            var d = m.Director;
            var w = m.Waiting;
            string wait = w.waiting ? string.Format(CultureInfo.InvariantCulture, "waiting for the {0} · {1} months · piles {2}", w.next, w.monthsWaited, w.pileLevel) : "no era waiting";
            return d.State == MomentDirector.Phase.Playing
                ? string.Format(CultureInfo.InvariantCulture, "moment {0} · {1:0.0}/{2:0.0} s · beat {3} · ×{4:0.#}{5}", d.Def.id, d.Time, d.Length, d.CurrentBeat?.id, d.Speed, d.Cheered ? " · cheered" : "")
                : wait;
        }

        /// <summary>Debug (#89): the camp lives <paramref name="years"/> without crossing the era (it waits), the glass is
        /// cleaned, so a grown camp's moment can be watched at once.</summary>
        public static string DebugGrow(float years)
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null) return "no sim";
            string r = GameClock.DebugSkipHours(years);
            sim.CleanAll();
            if (Instance != null) Instance.nextRead = 0f;
            var src = SimLayoutSource.Find();
            if (src != null) src.GetComponent<BoxView>()?.Refresh();
            return r + (sim.State.eraReady ? " · era waits" : "");
        }

        /// <summary>Debug (#90): a grown village waiting for the town. From the camp, crosses into the village (no moment),
        /// then lives <paramref name="years"/>, cleans the glass and makes the town ready if the numbers aren't there yet.</summary>
        public static string DebugGrowVillage(float years)
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null) return "no sim";
            if (sim.State.era == Era.Camp) { GameClock.DebugSkipHours(12f); sim.ForceNextEra(); }
            if (sim.State.era != Era.Village) return "not a village";
            string r = DebugGrow(years);
            if (!sim.State.eraReady) sim.ForceEraReady();
            if (Instance != null) Instance.nextRead = 0f;
            return r + " · the town waits";
        }

        /// <summary>Debug (#91): start the era festival now (a festival year: bunting, tables, festival evenings) and, if
        /// the box has the Festival Mask, the mask's dance.</summary>
        public static string DebugFestival()
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null) return "no sim";
            sim.State.festivalUntilMonth = sim.State.month + sim.Config.festivalYears * SimConfig.MonthsPerYear;
            if (Instance != null) { Instance.nextRead = 0f; Instance.StartFestivalScenes(sim, true); }
            return "festival until month " + sim.State.festivalUntilMonth;
        }

        public static string DebugEndFestival()
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null) return "no sim";
            sim.DebugEndFestival();
            if (Instance != null) Instance.nextRead = 0f;
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null) pv.Planner.ClearLive();
            return "festival over";
        }

        public static string DebugEraReady()
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null) return "no sim";
            return sim.ForceEraReady() ? "era ready: " + sim.NextEra : "already ready, or the last era";
        }

        /// <summary>Debug: pretend the era has waited <paramref name="months"/> (sets the pile level).</summary>
        public static string DebugWaited(long months)
        {
            var sim = GameClock.Instance?.Sim;
            if (sim == null || !sim.State.eraReady) return "no era waiting";
            sim.State.eraReadyMonth = sim.State.month - months;
            if (Instance != null) { Instance.nextRead = 0f; }
            return $"waited {months} months · piles {EraWaiting.PileLevel(months)}";
        }

        public static string DebugWatch(bool skipWipeOffer = true) => Instance == null ? "no MomentPlayer" : Instance.Watch(skipWipeOffer) ? Status() : "nothing to watch";
        public static string DebugReplay() => Instance == null ? "no MomentPlayer" : Instance.Watch(true, replay: true) ? Status() : "no past moment";
        public static string DebugLeave() => Instance == null ? "no MomentPlayer" : "left: " + Instance.Leave();

        /// <summary>Debug: the hurry speed (2 = the design, 4 = for testing).</summary>
        public static string DebugHurry(float factor)
        {
            if (Instance == null) return "no MomentPlayer";
            Instance.Director.HurryFactor = factor;
            return $"hurry ×{factor:0.#}";
        }

        /// <summary>Debug / tests: hurry without a finger (sets the director's speed for the next ticks).</summary>
        public static bool DebugHoldHurry;
    }
}
