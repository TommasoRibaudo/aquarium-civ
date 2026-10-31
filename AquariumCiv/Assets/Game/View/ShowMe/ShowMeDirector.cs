using AquariumCiv.Audio;
using System.Collections.Generic;
using System.Globalization;
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
    /// <summary>
    /// "Show me" and the people's voice in the world (#76 phase 2; chronicle-and-history §2.5, §5). Added to the box at
    /// start-up (like <see cref="GlassVoice"/>):
    /// <list type="bullet">
    /// <item><b>Show me</b> (<see cref="Show(ChronicleEntry, bool)"/>, <see cref="Show(ChronicleCard, bool)"/>): the camera flies to
    /// the plan's place (<see cref="ShowMeCore"/>) with the point above the card, the people play the echo
    /// (<see cref="LifePlanner.StageEcho"/>), its line shows over the speaker's head, then the
    /// echo ends and the camera eases back to where it was (<see cref="End"/>: the card stack calls it when it closes; the
    /// debug panel's Show me ends by itself). A death flies to the remembering stone and lays a flower; the weather and the
    /// box's big days fly wide. A touch takes the camera back from it: the flight stops and the camera stays where the
    /// player puts it.</item>
    /// <item><b>World bubbles</b> (<see cref="WorldBubbles"/>): now and then a person on screen says a line in their own
    /// words, in a bubble over their head. Quiet while a sheet is open, in camera mode and clean mode, during a moment, the
    /// time-lapse, the last moment, shape mode and Show me.</item>
    /// </list>
    /// Debug: static <c>Debug*</c> methods (the debug panel).
    /// </summary>
    [DefaultExecutionOrder(25)]
    public class ShowMeDirector : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static ShowMeDirector Instance { get; private set; }
        /// <summary>World bubbles on (tests turn them off to keep a scene quiet).</summary>
        public static bool BubblesOn = true;

        public enum Phase { Idle, Flying, Waiting, Echo, Hold, Returning }

        /// <summary>A beat on the place after an echo (or instead of one) before the camera goes back.</summary>
        public const float HoldSeconds = 0.8f, NoEchoHoldSeconds = 2.5f, ReturnSeconds = 0.9f;
        /// <summary>Show me looks down from at least this pitch (degrees), over the glass's rim.</summary>
        public const float ShowPitch = 52f;
        /// <summary>How often a world bubble is considered (real seconds).</summary>
        public const float BubbleCheckSeconds = 1f;

        public Phase State { get; private set; }
        public bool Busy => State != Phase.Idle && State != Phase.Returning;
        public ShowMePlan Current { get; private set; }
        public RelicMomentInfo CurrentEcho { get; private set; }
        /// <summary>The echo's line said last (tests).</summary>
        public string LastLine { get; private set; } = "";
        /// <summary>The world bubble showing now (null = none) and the last shown (tests).</summary>
        public WorldBubble ShownBubble => bubble != null && bubbleEl != null && bubbleEl.style.display == DisplayStyle.Flex ? bubble : null;
        public WorldBubble LastBubble { get; private set; }
        public int BubblesShown { get; private set; }
        /// <summary>The camera came back after the last Show me (tests).</summary>
        public bool Returned { get; private set; }
        /// <summary>A touch took the camera during the last Show me.</summary>
        public bool Interrupted { get; private set; }

        BoxView box;
        SimCore boundSim;
        LifePlanner boundPlanner;
        ShowMeCore core;
        WorldBubbles bubbles;
        float phaseT, holdFor;
        bool autoReturn, lineSaid, pressDown;
        (Vector3 target, float yaw, float pitch, float zoom) saved;
        GameObject flower;
        readonly List<RelicLine> lines = new();
        readonly List<RelicMomentInfo> echoes = new();

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : SimLayoutSource.Find()?.Sim;
        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<ShowMeDirector>() == null) b.gameObject.AddComponent<ShowMeDirector>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null; else return;
            if (flower != null) Destroy(flower);
            if (uiGo != null) Destroy(uiGo);
        }

        void OnApplicationPause(bool paused) { if (!paused) bubbles?.WarmUp(); } // a new sitting: the people let you settle in

        /// <summary>The pure core for the box shown now (null before the people are up).</summary>
        public ShowMeCore Core { get { Bind(); return core; } }
        public WorldBubbles Bubbles { get { Bind(); return bubbles; } }

        void Bind()
        {
            var sim = Sim;
            var planner = PeopleView.Find()?.Planner;
            if (sim == boundSim && planner == boundPlanner) return;
            boundSim = sim; boundPlanner = planner;
            core = sim != null ? new ShowMeCore(sim, planner?.Positions) : null;
            if (sim == null) bubbles = null;
            else if (!ReferenceEquals(bubblesSim, sim)) { bubbles = new WorldBubbles(sim); bubblesSim = sim; } // a new box: nothing said yet
        }

        SimCore bubblesSim;

        // ================================================================== the API (#75's card stack, the debug panel)

        /// <summary>"Show me" on a card: fly there, the echo, its line. <paramref name="autoReturn"/>: the camera eases back by
        /// itself after (the debug panel); false = it stays until <see cref="End"/> (the card stack, when it closes).</summary>
        public ShowMePlan Show(ChronicleCard card, bool autoReturn = false) { Bind(); return card == null || core == null ? null : Begin(core.Plan(card), autoReturn); }

        /// <summary>"Show me" on one entry (see <see cref="Show(ChronicleCard, bool)"/>).</summary>
        public ShowMePlan Show(ChronicleEntry entry, bool autoReturn = false) { Bind(); return entry == null || core == null ? null : Begin(core.Plan(entry), autoReturn); }

        /// <summary>The card stack closed (or another Show me starts): the echo ends, the flower goes, and the camera eases back
        /// to where it was before the first Show me (unless the player took it, or <paramref name="returnCamera"/> is false).</summary>
        public void End(bool returnCamera = true)
        {
            if (State == Phase.Idle && Current == null) return;
            var planner = PeopleView.Find()?.Planner;
            planner?.EndEchoes();
            ClearEchoBubble();
            CurrentEcho = null;
            if (flower != null) { Destroy(flower); flower = null; }
            var cam = Cam;
            if (returnCamera && !Interrupted && cam != null && State != Phase.Returning)
            {
                cam.EaseTo(saved.target, saved.yaw, saved.pitch, saved.zoom, ReturnSeconds);
                State = Phase.Returning;
                Returned = true;
                phaseT = 0f;
            }
            else State = Phase.Idle;
            Current = null;
        }

        ShowMePlan Begin(ShowMePlan plan, bool auto)
        {
            var cam = Cam;
            var pv = PeopleView.Find();
            if (plan == null || cam == null || pv == null || !plan.target.hasPoint) return null;
            if (State == Phase.Idle || State == Phase.Returning)
                saved = (cam.target, cam.yaw, cam.pitch, cam.zoom); // a Show me after a Show me keeps the first view to go back to
            if (State != Phase.Idle && State != Phase.Returning) { pv.Planner?.EndEchoes(); if (flower != null) { Destroy(flower); flower = null; } }
            Current = plan;
            GameClock.Tel.ShowMe(plan.entry != null ? plan.entry.id.ToString() : "?");
            GameClock.Tel.CardFlownTo(plan.entry != null ? plan.entry.id.ToString() : "?");
            CurrentEcho = null;
            autoReturn = auto;
            lineSaid = false;
            ClearEchoBubble();
            Returned = Interrupted = false;
            HideBubble();

            var t = plan.target;
            Vector2 p = new(t.x, t.z);
            if (t.followId >= 0 && pv.TryGetDollPosition(PersonRef.Notable(t.followId), out var doll)) p = doll;
            var world = pv.transform.TransformPoint(new Vector3(p.x, 0f, p.y));
            float dist = new Vector2(world.x - cam.target.x, world.z - cam.target.z).magnitude;
            float zoom = CameraZoom(cam, t.zoom);
            // From above the rim: at the player's usual pitch a close look goes through the front pane's haze.
            float pitch = Mathf.Max(cam.pitch, ShowPitch);
            var focus = AboveCard(cam, world, zoom, pitch, t.anchorY);
            cam.EaseTo(focus, cam.yaw, pitch, zoom, ShowMeCore.FlySeconds(dist));
            State = Phase.Flying;
            phaseT = 0f;
            Sfx.Play(CueLibrary.ShowMe); // #96: "look at this"
            if (plan.echo != null && plan.echo.flower) { LayFlower(t); Sfx.Play(CueLibrary.Flower, 0f, 1f, 0.6f); }
            Debug.Log($"[ShowMe] {plan.entry?.kind} #{plan.entry?.id}: {t}; echo {(plan.HasEcho ? plan.echo.kind.ToString() : plan.echo?.skip.ToString())}");
            return plan;
        }

        /// <summary>The plan's zoom on this camera: the core's scale (a person 0.45 ... the whole box 1) mapped so a person is
        /// as close as the camera goes (the portrait fit makes 0.45 still most of the box).</summary>
        public static float CameraZoom(BoxCamera cam, float planZoom)
        {
            float k = Mathf.InverseLerp(ShowMeCore.PersonZoom, ShowMeCore.BoxZoom, planZoom);
            return Mathf.Lerp(cam.minZoom, 1f, k * k);
        }

        /// <summary>The camera target that puts <paramref name="world"/> at <paramref name="anchorY"/> of the screen height (the
        /// card covers the bottom): the target slides toward the viewer along the ground.</summary>
        public static Vector3 AboveCard(BoxCamera cam, Vector3 world, float zoom, float pitchDeg, float anchorY)
        {
            var c = cam.GetComponent<Camera>();
            float fov = c != null ? c.fieldOfView : 22f;
            float d = cam.FitDistance() * Mathf.Clamp(zoom, cam.minZoom, cam.maxZoom);
            float pitch = pitchDeg * Mathf.Deg2Rad;
            float a = Mathf.Atan((2f * anchorY - 1f) * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
            if (pitch - a < 0.1f) return world;
            float s = d * Mathf.Sin(a) / Mathf.Sin(pitch - a);
            var fwd = Quaternion.Euler(0f, cam.yaw, 0f) * Vector3.forward;
            // Never past the pan bounds (the camera would clamp it and the place would slide off): as high as fits.
            var b = BoxCameraMath.PanBounds(zoom);
            for (int i = 0; i < 8; i++)
            {
                var f = world - fwd * s;
                if (Mathf.Abs(f.x) <= b.x && Mathf.Abs(f.z) <= b.y) return f;
                s *= 0.6f;
            }
            return world;
        }

        void LayFlower(ShowMeTarget t)
        {
            if (box == null) return;
            flower = new GameObject("ShowMeFlower") { hideFlags = HideFlags.DontSave };
            flower.transform.SetParent(box.transform, false);
            float y = box.GroundHeight(Mathf.Clamp(t.x, -Land.HX, Land.HX), Mathf.Clamp(t.z, -Land.HZ, Land.HZ));
            flower.transform.localPosition = new Vector3(t.x + 0.06f, y, t.z - 0.05f);
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stem.GetComponent<Collider>());
            stem.transform.SetParent(flower.transform, false);
            stem.transform.localScale = new Vector3(0.008f, 0.03f, 0.008f);
            stem.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(flower.transform, false);
            head.transform.localScale = Vector3.one * 0.035f;
            head.transform.localPosition = new Vector3(0f, 0.065f, 0f);
            Tint(stem, new Color(0.36f, 0.55f, 0.27f));
            Tint(head, new Color(0.96f, 0.82f, 0.35f));
        }

        static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ================================================================== the frame

        void Update()
        {
            Bind();
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            StepShowMe(dt);
            StepBubbles();
        }

        void StepShowMe(float dt)
        {
            if (State == Phase.Idle) return;
            var cam = Cam;
            var pv = PeopleView.Find();
            if (cam == null || pv == null) { State = Phase.Idle; return; }
            phaseT += dt;
            // A touch on the box (not the UI) takes the camera: it stays where the player puts it.
            bool press = Pressed(out var pos);
            if (press && !pressDown && !UiInput.Over(pos) && State != Phase.Returning) Interrupted = true;
            pressDown = press;
            switch (State)
            {
                case Phase.Flying:
                    if (cam.IsFlying) return;
                    State = Phase.Waiting; phaseT = 0f;
                    return;
                case Phase.Waiting:
                    if (phaseT < (Current?.echoDelay ?? 0f)) return;
                    StartEcho(pv);
                    return;
                case Phase.Echo:
                {
                    SayEchoLine(pv);
                    var now = pv.Now;
                    if (CurrentEcho == null || now.day != CurrentEcho.day || now.hour >= CurrentEcho.end || pv.Planner.ActiveEchoes(now, echoes) == 0)
                    { State = Phase.Hold; phaseT = 0f; holdFor = HoldSeconds; }
                    return;
                }
                case Phase.Hold:
                    if (phaseT < holdFor) return;
                    if (autoReturn || Interrupted) End(!Interrupted);
                    return;
                case Phase.Returning:
                    if (!cam.IsFlying || phaseT > ReturnSeconds + 0.5f) State = Phase.Idle;
                    return;
            }
        }

        void StartEcho(PeopleView pv)
        {
            var plan = Current;
            CurrentEcho = plan != null && plan.HasEcho ? pv.Planner.StageEcho(plan.echo, pv.Now) : null;
            if (CurrentEcho != null) { State = Phase.Echo; phaseT = 0f; return; }
            // Fly-to only: a death's words from the one left behind, then a look at the place.
            if (plan?.echo != null && plan.echo.skip == EchoSkip.Death && plan.entry != null)
            {
                var q = PeopleVoice.StoryLine(Sim, plan.entry);
                if (q != null) Say(q.text, q.speakerId >= 0 ? PersonRef.Notable(q.speakerId) : PersonRef.None, PeopleVoice.SecondsFor(q.text));
            }
            State = Phase.Hold; phaseT = 0f; holdFor = NoEchoHoldSeconds;
        }

        void SayEchoLine(PeopleView pv)
        {
            if (lineSaid) return;
            if (pv.Planner.EchoLinesAt(pv.Now, lines) == 0) return;
            var l = lines[0];
            lineSaid = true;
            Say(l.text, l.speaker, Mathf.Clamp(LifeClock.RealSecondsBetween(pv.Now.hour, l.end) + 1.5f, 3f, MomentPlayer.BubbleSeconds));
        }

        /// <summary>The echo's line goes over the speaker's head like the world bubbles (user 2026-10-30, #76). With nobody to
        /// put it over (no speaker, no doll) it shows in the moment's bubble.</summary>
        void Say(string text, PersonRef speaker, float seconds)
        {
            LastLine = text;
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (!speaker.IsNone && pv != null && cam != null && pv.TryGetDollPosition(speaker, out _))
            {
                EnsureUi();
                echoWho = speaker;
                echoUntil = Time.unscaledTime + seconds;
                bubbleEl.text = $"“{text}”";
                bubbleEl.style.display = DisplayStyle.Flex;
                bubbleEl.style.fontSize = 34; bubbleEl.style.maxWidth = 640;
                if (!Place(pv, cam, speaker)) bubbleEl.style.display = DisplayStyle.None; // shows as soon as they're on screen
                EchoLinesOverHead++;
                return;
            }
            var mp = MomentPlayer.Instance;
            string who = speaker.IsNotable ? Sim?.FindNotable(speaker.notableId)?.name : null;
            if (mp != null) mp.Say(who != null && !text.Contains(who) ? $"{who}: “{text}”" : $"“{text}”", seconds);
        }

        PersonRef echoWho;
        float echoUntil;
        /// <summary>Echo lines drawn over their speaker's head so far, and whether one shows now (tests).</summary>
        public int EchoLinesOverHead { get; private set; }
        public bool EchoBubbleShown => !echoWho.IsNone && bubbleEl != null && bubbleEl.style.display == DisplayStyle.Flex;
        /// <summary>The screen point the bubble sits over, and who says the echo (tests).</summary>
        public Vector2 BubbleScreen { get; private set; }
        public PersonRef EchoSpeaker => echoWho;

        /// <summary>The echo's bubble follows its speaker while it lasts; world bubbles wait. True while it does.</summary>
        bool StepEchoBubble(PeopleView pv, Camera cam)
        {
            if (echoWho.IsNone) return false;
            if (Time.unscaledTime >= echoUntil) { ClearEchoBubble(); return false; }
            var hud = GameHud.Instance;
            bool show = (hud == null || (!hud.CameraMode && hud.IsVisible)) && Place(pv, cam, echoWho);
            bubbleEl.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            return true;
        }

        void ClearEchoBubble()
        {
            if (echoWho.IsNone) return;
            echoWho = PersonRef.None;
            if (bubbleEl != null) bubbleEl.style.display = DisplayStyle.None;
        }

        static bool Pressed(out Vector2 pos)
        {
            pos = default;
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.isInProgress) { pos = t.position.ReadValue(); return true; }
            var m = Mouse.current;
            if (m == null) return false;
            pos = m.position.ReadValue();
            return m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed;
        }

        // ================================================================== world bubbles

        WorldBubble bubble;
        float bubbleUntil, nextCheck;
        PersonRef bubbleWho;
        GameObject uiGo;
        VisualElement uiRoot;
        Label bubbleEl;
        readonly List<int> visibleNotables = new();
        readonly List<PersonRef> visibleCrowd = new();

        /// <summary>Why the people keep quiet now (null = they may speak).</summary>
        public string QuietReason()
        {
            var hud = GameHud.Instance;
            if (!BubblesOn) return "off";
            if (hud != null && hud.CameraMode) return "camera mode";
            if (hud != null && hud.CleanMode) return "clean mode";
            if (hud != null && hud.SheetOpen) return "a sheet is open";
            if (hud != null && !hud.IsVisible) return "the HUD is hidden";
            if (Busy) return "show me";
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return "a moment";
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return "the time-lapse";
            var bc = BoxCompleteController.Instance;
            if (bc != null && bc.Playing) return "the last moment";
            var sv = ShapingView.Instance;
            if (sv != null && sv.ShapeMode) return "shape mode";
            var pv = PeopleView.Find();
            if (pv?.Planner != null && pv.Planner.ActiveRelicMoments(pv.Now, echoes) > 0) return "a relic moment";
            return null;
        }

        void StepBubbles()
        {
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || cam == null || bubbles == null) { HideBubble(); return; }
            if (StepEchoBubble(pv, cam)) return;
            string quiet = QuietReason();
            if (bubble != null)
            {
                bool gone = quiet != null || Time.unscaledTime >= bubbleUntil || !Place(pv, cam, bubbleWho);
                if (gone) HideBubble();
                return;
            }
            if (quiet != null || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + BubbleCheckSeconds;
            Visible(pv, cam);
            var now = pv.Now;
            string moment = now.hour >= 18f && now.hour < 21f ? PeopleMoment.Evening : null;
            var b = bubbles.Next(Time.unscaledTime, moment, visibleNotables);
            if (b == null) return;
            ShowBubble(pv, cam, b);
        }

        void Visible(PeopleView pv, Camera cam)
        {
            visibleNotables.Clear(); visibleCrowd.Clear();
            var planner = pv.Planner;
            if (planner == null) return;
            foreach (var who in planner.People)
            {
                if (!pv.TryGetDollTop(who, out var w)) continue;
                var v = cam.WorldToViewportPoint(w);
                if (v.z <= 0f || v.x < 0.12f || v.x > 0.88f || v.y < 0.42f || v.y > 0.88f) continue; // over the card's half: not there
                if (who.IsNotable) visibleNotables.Add(who.notableId); else visibleCrowd.Add(who);
            }
        }

        void ShowBubble(PeopleView pv, Camera cam, WorldBubble b)
        {
            var who = b.speakerId >= 0 ? PersonRef.Notable(b.speakerId) : visibleCrowd.Count > 0 ? visibleCrowd[(BubblesShown * 7) % visibleCrowd.Count] : PersonRef.None;
            if (who.IsNone && visibleNotables.Count > 0) who = PersonRef.Notable(visibleNotables[0]);
            if (who.IsNone) return; // nobody on screen to say it (it's counted said: fine, there are more)
            if (DollPixels(pv, cam, who) < TinySpeakerPx) return; // zoomed far out: a bubble nobody can tie to a person
            EnsureUi();
            bubble = b;
            bubbleWho = who;
            bubbleUntil = Time.unscaledTime + b.seconds;
            bubbleEl.text = string.Join("\n", b.lines);
            bubbleEl.style.display = DisplayStyle.Flex;
            FitToSpeaker(pv, cam, who);
            if (!Place(pv, cam, who)) { HideBubble(); return; }
            LastBubble = b;
            BubblesShown++;
            Debug.Log($"[ShowMe] bubble {b} over {who}");
        }

        bool Place(PeopleView pv, Camera cam, PersonRef who)
        {
            // The real top of the doll (a fixed height in box space fell below the land: it sits higher than that).
            if (bubbleEl == null || bubbleEl.panel == null || !pv.TryGetDollTop(who, out var w)) return false;
            var s = cam.WorldToScreenPoint(w);
            if (s.z <= 0f || s.x < 0f || s.x > Screen.width || s.y < 0f || s.y > Screen.height) return false;
            BubbleScreen = new Vector2(s.x, s.y);
            var q = RuntimePanelUtils.ScreenToPanel(bubbleEl.panel, new Vector2(s.x, Screen.height - s.y));
            float bw = bubbleEl.resolvedStyle.width, bh = bubbleEl.resolvedStyle.height;
            if (float.IsNaN(bw)) bw = 0f;
            if (float.IsNaN(bh)) bh = 0f;
            // The echo sits right over its speaker. An ambient line goes to the side with more room, so it covers the
            // speaker's neighbours less: at usual zooms a bubble over the head hid the place (tester batch 3).
            bool ambient = echoWho.IsNone;
            float panelW = bubbleEl.panel.visualTree.layout.width;
            var at = !ambient ? new Vector2(q.x - bw * 0.5f, q.y - 16f - bh)
                : q.x < panelW * 0.5f ? new Vector2(q.x + 30f, q.y - 26f - bh)
                : new Vector2(q.x - 30f - bw, q.y - 26f - bh);
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            float topMin = TopHudBottom + i.y;
            at = GameUiPanel.KeepOnScreen(bubbleEl, at, topMin);
            // never on the answer's own label and line
            var sv = ShapingView.Instance;
            if (sv != null)
                foreach (var r in sv.ShownLabelRects())
                {
                    var me = new Rect(at.x, at.y, bw, bh);
                    if (!me.Overlaps(r)) continue;
                    at.y = r.yMin - bh - 12f >= topMin ? r.yMin - bh - 12f : r.yMax + 12f;
                }
            bubbleEl.style.left = at.x;
            bubbleEl.style.top = at.y;
            return true;
        }

        /// <summary>Under the title pill (panel units).</summary>
        const float TopHudBottom = 190f;
        /// <summary>A doll shorter than this on screen (pixels of a 2556 tall frame) is too small to say whose line it is.</summary>
        public const float TinySpeakerPx = 12f;

        /// <summary>The doll's height on screen, in pixels of a 2556 tall frame.</summary>
        float DollPixels(PeopleView pv, Camera cam, PersonRef who)
        {
            if (!pv.TryGetDollTop(who, out var top) || !pv.TryGetDollFeet(who, out var feet)) return 0f;
            return (cam.WorldToScreenPoint(top) - cam.WorldToScreenPoint(feet)).magnitude * 2556f / Mathf.Max(1f, Screen.height);
        }

        /// <summary>An ambient bubble scales with the speaker: small at the default view, full size close up.</summary>
        void FitToSpeaker(PeopleView pv, Camera cam, PersonRef who)
        {
            float k = Mathf.Clamp01((DollPixels(pv, cam, who) - 30f) / 70f);
            bubbleEl.style.fontSize = Mathf.Lerp(30f, 34f, k);
            bubbleEl.style.maxWidth = Mathf.Lerp(470f, 640f, k);
        }

        void HideBubble()
        {
            bubble = null;
            if (bubbleEl != null) bubbleEl.style.display = DisplayStyle.None;
        }

        void EnsureUi()
        {
            if (uiGo != null) return;
            uiGo = new GameObject("ShowMeUI") { hideFlags = HideFlags.DontSave };
            uiGo.transform.SetParent(transform, false);
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 11; // over the HUD's world, under the moment player
            uiRoot = doc.rootVisualElement;
            uiRoot.pickingMode = PickingMode.Ignore;
            uiRoot.style.position = Position.Absolute;
            uiRoot.style.left = uiRoot.style.right = uiRoot.style.top = uiRoot.style.bottom = 0;
            bubbleEl = new Label { name = "showme-bubble", pickingMode = PickingMode.Ignore };
            bubbleEl.AddToClassList("hud-toast");
            bubbleEl.style.position = Position.Absolute;
            bubbleEl.style.right = StyleKeyword.Auto;
            bubbleEl.style.bottom = StyleKeyword.Auto;
            bubbleEl.style.fontSize = 34;
            bubbleEl.style.maxWidth = 640;
            bubbleEl.style.backgroundColor = new Color(0.97f, 0.94f, 0.88f, 0.95f); // the people's words: paper, not the HUD's dark pill
            bubbleEl.style.color = new Color(0.3f, 0.22f, 0.16f);
            bubbleEl.style.unityTextAlign = TextAnchor.MiddleCenter;
            bubbleEl.style.display = DisplayStyle.None;
            uiRoot.Add(bubbleEl);
        }

        // ================================================================== debug

        public static string Status()
        {
            var d = Instance;
            if (d == null) return "show me: off";
            return string.Format(CultureInfo.InvariantCulture, "show me: {0}{1} · bubbles {2} shown, quiet: {3}{4}", d.State,
                d.Current != null ? " " + d.Current.target : "", d.BubblesShown, d.QuietReason() ?? "no",
                d.LastBubble != null ? " · last: " + d.LastBubble.text : "");
        }

        /// <summary>Debug: Show me on a chronicle entry by id (the camera comes back by itself).</summary>
        public static string DebugShow(int entryId)
        {
            var d = Instance; var sim = Sim;
            if (d == null || sim == null) return "no ShowMeDirector";
            var e = sim.State.chronicle.Find(x => x.id == entryId);
            if (e == null) return "no entry #" + entryId;
            var plan = d.Show(e, true);
            return plan == null ? "no place for #" + entryId : $"show me {e.kind}: {plan.target} · echo {(plan.HasEcho ? plan.echo.kind.ToString() : plan.echo.skip.ToString())}";
        }

        /// <summary>Debug: the latest chronicle entries that make cards (newest first), for the panel's list.</summary>
        public static List<ChronicleEntry> Latest(int count = 8)
        {
            var l = new List<ChronicleEntry>();
            var sim = Sim;
            if (sim == null) return l;
            for (int i = sim.State.chronicle.Count - 1; i >= 0 && l.Count < count; i--)
                if (SimCore.ShownAsCard(sim.State.chronicle[i])) l.Add(sim.State.chronicle[i]);
            return l;
        }

        /// <summary>Debug: a bubble now, whatever the gaps (still never one already said; still quiet when it must be).</summary>
        public static string DebugBubble()
        {
            var d = Instance;
            if (d == null) return "no ShowMeDirector";
            d.Bind();
            var pv = PeopleView.Find(); var cam = Camera.main;
            if (pv == null || cam == null || d.bubbles == null) return "no people";
            string quiet = d.QuietReason();
            if (quiet != null && quiet != "a sheet is open") return "quiet: " + quiet;
            d.HideBubble();
            d.Visible(pv, cam);
            var b = d.bubbles.Force(Time.unscaledTime, null, d.visibleNotables);
            if (b == null) return "nothing left to say";
            d.ShowBubble(pv, cam, b);
            return d.bubble != null ? "bubble: " + b : "nobody on screen to say: " + b.text;
        }

        public static string DebugEnd() { var d = Instance; if (d == null) return "no ShowMeDirector"; d.End(); return "show me ended"; }
    }
}
