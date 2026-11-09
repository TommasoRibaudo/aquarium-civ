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
    /// (<see cref="LifePlanner.StageEcho"/>), its line shows on the caption with the speaker's name, ring and speech mark, then the
    /// echo ends and the camera eases back to where it was (<see cref="End"/>: the card stack calls it when it closes; the
    /// debug panel's Show me ends by itself). A death flies to the remembering stone and lays a flower; the weather and the
    /// box's big days fly wide. A touch takes the camera back from it: the flight stops and the camera stays where the
    /// player puts it.</item>
    /// <item><b>The people's talk</b> (<see cref="WorldBubbles"/>): now and then a person on screen says a line in their own
    /// words. Quiet while a sheet is open, in camera mode and clean mode, during a moment, the
    /// time-lapse, the last moment, shape mode and Show me.</item>
    /// <item><b>#327 Option B (the user, 2026-11-08)</b>: every spoken line, at any zoom, goes to one caption at the top with the speaker's name
    /// (or a crowd label), the <see cref="SpeakerRing"/> under them and a speech mark over their head (<see cref="ChatterView.SpeechOver"/>).
    /// The over-head bubble is kept only for the first run's wish (<see cref="FirstRunView"/>). Routing: <see cref="LineRoute"/>.</item>
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
        /// <summary>Nothing left to watch: the flight is over and the echo has played, or it was all ended (the card comes back).</summary>
        public bool Settled => !Busy || (State == Phase.Hold && phaseT >= holdFor);
        public ShowMePlan Current { get; private set; }
        public RelicMomentInfo CurrentEcho { get; private set; }
        /// <summary>The echo's line said last (tests).</summary>
        public string LastLine { get; private set; } = "";
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
            EndSceneLine();
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
            EndLine(); // the people's talk and an older scene's line give way to what the player asked to see
            Returned = Interrupted = false;

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
            StepLines();
            StepChatter();
        }

        void LateUpdate()
        {
            var pv = PeopleView.Find();
            var who = Speaking;
            if (who.IsNone && Time.unscaledTime < debugSpeechUntil) who = debugSpeechWho;
            Chatter.SpeechOver = who;
            Chatter.Draw(pv, Camera.main);
            Ring.Draw(pv, Speaking, Time.unscaledDeltaTime);
        }

        void OnDestroy() { Chatter.Dispose(); Ring.Dispose(); }

        /// <summary>#204: the ring under whoever has the word.</summary>
        public SpeakerRing Ring { get; } = new();

        /// <summary>Who has a line up on the caption now (None with no line, or a line nobody in the box says).</summary>
        PersonRef Speaking => CaptionUp ? lineWho : PersonRef.None;

        /// <summary>#144: the wordless talk, driven from here so it shares the lines' idea of quiet. #327: it also draws the speech mark.</summary>
        public ChatterView Chatter { get; } = new();

        /// <summary>The people's chatter stays out of it now. "Someone has the word" isn't quiet: it's a real line, which fades only the talk.</summary>
        bool ChatterQuiet(out bool realLine)
        {
            string q = QuietReason();
            bool word = q == SomeoneSpeaks;
            realLine = word || CaptionUp;
            return q != null && !word;
        }

        void StepChatter()
        {
            bool quiet = ChatterQuiet(out bool line);
            var cam = Camera.main;
            Chatter.Step(PeopleView.Find(), cam != null ? cam.GetComponent<BoxCamera>() : null, Sim, quiet, line);
        }

        /// <summary>#161: see <see cref="ChatterView.Knock"/>.</summary>
        public Dictionary<int, float> KnockMarks(LifePlanner planner, ViewTime at, Vector2 screen) =>
            Chatter.Knock(PeopleView.Find(), Camera.main, Sim, planner, at, screen, ChatterQuiet(out _));

        public void KnockHeard(PersonRef who) => Chatter.KnockHeard(PeopleView.Find(), Sim, who, ChatterQuiet(out _));

        const string SomeoneSpeaks = "someone has the word";

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
                if (q != null) Say(q.text, q.speakerId >= 0 ? PersonRef.Notable(q.speakerId) : PersonRef.None, PeopleVoice.SecondsFor(q.text), LineKind.Death);
            }
            State = Phase.Hold; phaseT = 0f; holdFor = NoEchoHoldSeconds;
        }

        void SayEchoLine(PeopleView pv)
        {
            if (lineSaid) return;
            if (pv.Planner.EchoLinesAt(pv.Now, lines) == 0) return;
            var l = lines[0];
            lineSaid = true;
            Say(l.text, l.speaker, Mathf.Clamp(LifeClock.RealSecondsBetween(pv.Now.hour, l.end) + 1.5f, 3f, MomentPlayer.BubbleSeconds), LineKind.Echo);
        }

        // ================================================================== the one caption (#327 Option B)

        /// <summary>
        /// #327 Option B (the user, 2026-11-08): a line of a scene (an echo, a death's words, the choice scene's ask, a relic moment, the crumb,
        /// a wish's thanks) on the one caption at the top, with the speaker's name (or a crowd label), the ring under them and the speech mark
        /// over their head. It takes the word from the people's ambient talk; if another scene's line has it (or a moment, or the first run's
        /// wish), it waits its turn for up to <see cref="LineRoute.WaitSeconds"/> instead of falling to a second system or vanishing.
        /// <paramref name="replace"/>: a new line of the same scene takes over the one up now (an echo after its own line, a new ask).
        /// True if it shows now, false if it waits.
        /// </summary>
        public bool Say(string text, PersonRef speaker, float seconds, LineKind kind = LineKind.Echo, bool replace = true)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (!CaptionBlocked() && SceneMaySpeak(replace) && ShowLine(text, speaker, seconds, SceneVoice, kind)) return true;
            if (LineRoute.Waits(kind)) waiting.Add(new Waiting { text = text, who = speaker, seconds = seconds, kind = kind }, Time.unscaledTimeAsDouble);
            return false;
        }

        /// <summary>A line that waits its turn behind whatever is said now (relic lines, the crumb, a wish's thanks): it never cuts another
        /// scene's line short. True if it shows now.</summary>
        public bool SayOrWait(string text, PersonRef speaker, float seconds, LineKind kind) => Say(text, speaker, seconds, kind, false);

        /// <summary>#224: a staged line from outside (the choice scene's ask), on the caption with the asker's ring and mark.</summary>
        public void SayLine(string text, PersonRef speaker, float seconds) => Say(text, speaker, seconds, LineKind.Ask);

        /// <summary>#224: take down a staged line (the choice scene was left or answered).</summary>
        public void ClearLine() => EndSceneLine();

        static string NameOf(PersonRef who) => who.IsNotable ? Sim?.FindNotable(who.notableId)?.name : null;

        /// <summary>The caption's "who" line for this speaker: their first name (notable or crowd person), or null so the caption shows none (#522).</summary>
        static string WhoLine(PersonRef who)
        {
            var sim = Sim;
            return LineRoute.Who(NameOf(who), who.IsCrowd && sim != null ? sim.CrowdName(who.crowdIndex) : null);
        }

        sealed class Waiting { public string text; public PersonRef who; public float seconds; public LineKind kind; }
        readonly LineQueue<Waiting> waiting = new();

        /// <summary>Scene lines waiting for the word now, and let go after waiting too long (tests, the debug panel).</summary>
        public int WaitingLines => waiting.Count;
        public int LinesLetGo => waiting.Dropped;

        /// <summary>May a scene's line have the word now? Nobody, or only the ambient talk, has it (or this scene, if it may replace itself).</summary>
        bool SceneMaySpeak(bool replace)
        {
            var h = GameUiPanel.Bubbles.Holder;
            return h == null || h.rank < BubbleHost.Rank.Line || (replace && h == SceneVoice);
        }

        /// <summary>The caption can't be up now: it would come down the same frame.</summary>
        bool CaptionBlocked()
        {
            var hud = GameHud.Instance;
            var mp = MomentPlayer.Instance;
            return (mp != null && mp.Playing) || BoxCompleteController.ShelfIsOpen || (hud != null && (hud.CameraMode || !hud.IsVisible));
        }

        /// <summary>The one way a spoken line shows: ask the word for <paramref name="voice"/>, then the caption with the "who" line, the
        /// ring and the speech mark (<see cref="Speaking"/>). False if the word can't be had.</summary>
        bool ShowLine(string text, PersonRef speaker, float seconds, BubbleHost.Voice voice, LineKind kind)
        {
            if (!GameUiPanel.Bubbles.Ask(voice)) return false; // a higher rank told the old holder to go: its caption is already down
            if (captionVoice != null && captionVoice != voice) GameUiPanel.Bubbles.Done(captionVoice);
            EnsureUi();
            LastLine = text;
            LastWho = WhoLine(speaker);
            LastKind = kind;
            lineWho = speaker;
            captionVoice = voice;
            if (voice == SceneVoice) SceneLines++;
            var hud = GameHud.Instance;
            bool underHud = hud != null && hud.OutsideSheet == null;
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            captionEl.UnderHud = underHud;
            captionEl.style.top = Mathf.Max(underHud ? CaptionUnderHud : CaptionTop, i.y + CaptionUnderNotch);
            captionEl.Set(text, LastWho);
            captionUntil = Time.unscaledTime + seconds;
            GlassMotion.Show(captionEl, GlassMotion.CaptionOff, null, GlassMotion.CaptionInMs);
            Debug.Log($"[ShowMe] line ({kind}) on the caption for {speaker}: {text}");
            return true;
        }

        /// <summary>The caption comes down and its voice lets the word go.</summary>
        void EndLine()
        {
            bubble = null;
            lineWho = PersonRef.None;
            var v = captionVoice;
            captionVoice = null;
            if (v != null) GameUiPanel.Bubbles.Done(v);
            if (captionUntil <= 0f) return;
            captionUntil = 0f;
            if (captionEl != null) GlassMotion.Hide(captionEl, GlassMotion.CaptionOff, GlassMotion.CaptionOutMs);
        }

        /// <summary>A scene's line comes down (the ambient talk's stays).</summary>
        void EndSceneLine() { if (captionVoice == sceneVoice && sceneVoice != null) EndLine(); }

        /// <summary>The caption's box on the panel, empty with none up.</summary>
        public Rect CaptionBound => captionUntil > 0f && captionEl != null ? captionEl.worldBound : default;
        /// <summary>A line in the caption lane now (tests, the debug panel).</summary>
        public bool CaptionUp => captionUntil > 0f;
        /// <summary>A caption's line up now (tests), or null.</summary>
        public string ShownCaption => captionUntil > 0f ? LastLine : null;
        /// <summary>The "who" line of the last line said (a name, a crowd label or null), and its kind (tests).</summary>
        public string LastWho { get; private set; }
        public LineKind LastKind { get; private set; }
        /// <summary>Who says the line up now (None with none up, tests).</summary>
        public PersonRef LineSpeaker => Speaking;
        /// <summary>Scene lines shown on the caption so far (tests).</summary>
        public int SceneLines { get; private set; }
        public bool SpeakerRingUp => Ring.Up;
        /// <summary>The speech mark stood over the speaker last frame (tests).</summary>
        public bool SpeechMarkUp => Chatter.SpeechDrawn;

        /// <summary>The top of someone's head in panel units, false with no doll to look at (tests).</summary>
        public bool TryHeadOnPanel(PersonRef who, out Vector2 head)
        {
            head = default;
            EnsureUi();
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || cam == null || captionEl?.panel == null || !pv.TryGetDollTop(who, out var w)) return false;
            var s = cam.WorldToScreenPoint(w);
            head = RuntimePanelUtils.ScreenToPanel(captionEl.panel, new Vector2(s.x, Screen.height - s.y));
            return true;
        }

        /// <summary>The caption reads its time, then goes; a moment, the shelf, camera mode or a hidden HUD take it down at once, and the
        /// ambient talk's line also goes when the people must keep quiet. True while it's up.</summary>
        bool StepCaption()
        {
            if (captionUntil <= 0f) return false;
            bool world = captionVoice != null && captionVoice == worldVoice;
            if (Time.unscaledTime >= captionUntil || CaptionBlocked() || (world && QuietReason() != null)) { EndLine(); return false; }
            return true;
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

        // ================================================================== the people's ambient talk

        WorldBubble bubble;
        float nextCheck;
        PersonRef lineWho;
        BubbleHost.Voice captionVoice;
        GameObject uiGo;
        VisualElement uiRoot;
        GlassCaption captionEl;
        float captionUntil;
        readonly List<int> visibleNotables = new();
        readonly List<PersonRef> visibleCrowd = new();
        PersonRef debugSpeechWho;
        float debugSpeechUntil;

        /// <summary>The ambient line up now (on the caption), null with none; and the last shown (tests).</summary>
        public WorldBubble ShownBubble => CaptionUp && captionVoice != null && captionVoice == worldVoice ? bubble : null;
        public WorldBubble LastBubble { get; private set; }
        public int BubblesShown { get; private set; }

        /// <summary>Why the people keep quiet now (null = they may speak).</summary>
        public string QuietReason()
        {
            var hud = GameHud.Instance;
            if (!BubblesOn) return "off";
            if (hud != null && hud.CameraMode) return "camera mode";
            if (hud != null && hud.CleanMode) return "clean mode";
            if (hud != null && hud.SheetOpen) return "a sheet is open";
            if (PersonCardView.Instance != null && (PersonCardView.Instance.AnyOpen || PersonCardView.Instance.BuildingOpen)) return "a person card is open"; // its tie tags stand over the people (tester run 5)
            if (hud != null && !hud.IsVisible) return "the HUD is hidden";
            if (Busy) return "show me";
            if (ChoiceSceneView.Instance != null && ChoiceSceneView.Instance.Holding) return "the people ask the Keeper"; // #224
            if (FirstRunView.Instance != null && FirstRunView.Instance.HasTheWord) return "the first run";
            if (!GameUiPanel.Bubbles.Free(BubbleHost.Rank.World) && GameUiPanel.Bubbles.Holder != WorldVoice) return SomeoneSpeaks;
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return "a moment";
            var tl = TimeLapseView.Find();
            if (tl != null && tl.Active) return "the time-lapse";
            var bc = BoxCompleteController.Instance;
            if (bc != null && bc.Playing) return "the last moment";
            if (bc != null && bc.ShelfOpen) return "the shelf";
            var sv = ShapingView.Instance;
            if (sv != null && sv.ShapeMode) return "shape mode";
            var pv = PeopleView.Find();
            if (pv?.Planner != null && pv.Planner.ActiveRelicMoments(pv.Now, echoes) > 0) return "a relic moment";
            return null;
        }

        void StepLines()
        {
            var pv = PeopleView.Find();
            var cam = Camera.main;
            bool up = StepCaption();
            // #327: a scene's line that found the word busy says it now, if it's still in time.
            if (!CaptionBlocked() && SceneMaySpeak(false) && waiting.TryTake(Time.unscaledTimeAsDouble, true, out var w))
            {
                ShowLine(w.text, w.who, w.seconds, SceneVoice, w.kind);
                return;
            }
            waiting.TryTake(Time.unscaledTimeAsDouble, false, out _); // lets go of what waited too long
            if (up || pv == null || cam == null || bubbles == null) return;
            if (QuietReason() != null || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + BubbleCheckSeconds;
            Visible(pv, cam);
            var now = pv.Now;
            string moment = now.hour >= 18f && now.hour < 21f ? PeopleMoment.Evening : null;
            bubbles.HeldNow = StagedMoments.PlayingMoment(Sim); // t1-moments: a staged moment's own words, not the stories
            var b = bubbles.Next(Time.unscaledTime, moment, visibleNotables);
            if (b == null) return;
            ShowBubble(b);
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

        /// <summary>#327 Option B: the people's own line on the caption at any zoom, with the speaker's name (or a crowd label), their ring
        /// and the speech mark. The caption doesn't need a doll big enough to carry a bubble, so the old "too small" gate is gone.</summary>
        void ShowBubble(WorldBubble b, PersonRef? over = null)
        {
            var who = over ?? (b.speakerId >= 0 ? PersonRef.Notable(b.speakerId) : visibleCrowd.Count > 0 ? visibleCrowd[(BubblesShown * 7) % visibleCrowd.Count] : PersonRef.None);
            if (who.IsNone && visibleNotables.Count > 0) who = PersonRef.Notable(visibleNotables[0]);
            if (who.IsNone) return; // nobody on screen to say it (it's counted said: fine, there are more)
            var kind = b.source == BubbleSource.Want ? LineKind.Want : b.source == BubbleSource.Tree ? LineKind.Tree : LineKind.Ambient;
            if (!ShowLine(b.text, who, b.seconds, WorldVoice, kind)) return;
            bubble = b;
            LastBubble = b;
            BubblesShown++;
        }

        /// <summary>The caption lane without and with the HUD's top row, and the least it keeps under a notch (panel units, spec §1).</summary>
        const float CaptionTop = 143f, CaptionUnderHud = 275f, CaptionUnderNotch = 16f;

        BubbleHost.Voice worldVoice, sceneVoice;
        // A higher rank took the word: the caption goes at once (the one who took it shows theirs next).
        BubbleHost.Voice WorldVoice => worldVoice ??= GameUiPanel.Bubbles.Join(BubbleHost.Rank.World, () => { if (captionVoice == worldVoice) { captionVoice = null; EndLine(); } });
        BubbleHost.Voice SceneVoice => sceneVoice ??= GameUiPanel.Bubbles.Join(BubbleHost.Rank.Line, () => { if (captionVoice == sceneVoice) { captionVoice = null; EndLine(); } });

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
            captionEl = new GlassCaption("") { name = "showme-caption" };
            captionEl.style.display = DisplayStyle.None;
            uiRoot.Add(captionEl);
        }

        // ================================================================== debug

        public static string Status()
        {
            var d = Instance;
            if (d == null) return "show me: off";
            return string.Format(CultureInfo.InvariantCulture, "show me: {0}{1} · lines {2} shown, {5} waiting, {6} let go, quiet: {3}{4}", d.State,
                d.Current != null ? " " + d.Current.target : "", d.BubblesShown + d.SceneLines, d.QuietReason() ?? "no",
                d.LastBubble != null ? " · last: " + d.LastBubble.text : "", d.WaitingLines, d.LinesLetGo);
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

        /// <summary>Debug: an ambient line now, whatever the gaps (still never one already said; still quiet when it must be).</summary>
        public static string DebugBubble()
        {
            var d = Instance;
            if (d == null) return "no ShowMeDirector";
            d.Bind();
            var pv = PeopleView.Find(); var cam = Camera.main;
            if (pv == null || cam == null || d.bubbles == null) return "no people";
            string quiet = d.QuietReason();
            if (quiet != null && quiet != "a sheet is open") return "quiet: " + quiet;
            d.EndLine();
            d.Visible(pv, cam);
            var b = d.bubbles.Force(Time.unscaledTime, null, d.visibleNotables);
            if (b == null) return "nothing left to say";
            d.ShowBubble(b);
            return d.ShownBubble != null ? "line: " + b : "nobody on screen to say: " + b.text;
        }

        /// <summary>Debug (#204, #327): this line by this person now, as an ordinary ambient line (on the caption, with their ring and speech mark).</summary>
        public static string DebugSayOver(PersonRef who, string text)
        {
            var d = Instance;
            if (d == null) return "no ShowMeDirector";
            d.Bind();
            var pv = PeopleView.Find(); var cam = Camera.main;
            if (pv == null || cam == null || d.bubbles == null) return "no people";
            string quiet = d.QuietReason();
            if (quiet != null && quiet != "off") return "quiet: " + quiet;
            d.EndLine();
            var b = new WorldBubble { key = "debug:" + Time.frameCount, text = text, speakerId = who.IsNotable ? who.notableId : -1, source = BubbleSource.Ambient, entryId = -1, seconds = 6f, lines = PeopleVoice.Wrap(text) };
            d.ShowBubble(b, who);
            return d.ShownBubble != null ? "line: " + text : "nobody on screen to say: " + text;
        }

        /// <summary>Debug (#204, #327): a line by whoever is walking, else whoever is on view: a ring and a speech mark to watch.</summary>
        public static string DebugSayOverWalker()
        {
            var pv = PeopleView.Find();
            if (pv == null) return "no people";
            var any = PersonRef.None;
            foreach (var a in pv.Activities)
            {
                if (!pv.TryGetDollTop(a.who, out _)) continue;
                if (a.moving) return DebugSayOver(a.who, "Mind the mud on the way, it's deep.");
                if (any.IsNone) any = a.who;
            }
            return any.IsNone ? "nobody on view" : DebugSayOver(any, "Mind the mud on the way, it's deep.");
        }

        /// <summary>Debug (#181): the want line for one case now, through the same caption and word as any other line.</summary>
        public static string DebugWant(WantCase c)
        {
            var d = Instance;
            if (d == null) return "no ShowMeDirector";
            d.Bind();
            var pv = PeopleView.Find(); var cam = Camera.main;
            if (pv == null || cam == null || d.bubbles == null) return "no people";
            string quiet = d.QuietReason();
            if (quiet != null) return "quiet: " + quiet;
            var b = d.bubbles.ForceWant(c, Time.unscaledTime);
            if (b == null) return c + " isn't true now";
            d.EndLine();
            d.Visible(pv, cam);
            d.ShowBubble(b);
            return d.ShownBubble != null ? "want: " + b.text + " (" + d.LastWho + ")" : "nobody on screen to say: " + b.text;
        }

        /// <summary>Debug (#318): a tree line now, through the same bubble and word as any other. A tree that isn't tended yet is
        /// planted at the sprout stage and counted as misted once first (the cheat's own doing, said in the result).</summary>
        public static string DebugTreeLine()
        {
            var d = Instance;
            if (d == null) return "no ShowMeDirector";
            d.Bind();
            var pv = PeopleView.Find(); var cam = Camera.main;
            if (pv == null || cam == null || d.bubbles == null || d.bubblesSim == null) return "no people";
            string quiet = d.QuietReason();
            if (quiet != null) return "quiet: " + quiet;
            var sim = d.bubblesSim;
            string prep = "";
            if (!PeopleVoice.TreeTended(sim))
            {
                if (!sim.EcologyOn) return "no ecology in this box";
                if (!sim.DebugTreeStage(Mathf.Max(1, sim.EcoTree.stage))) return "no tree can be planted";
                sim.State.eco.keeperMistings = Mathf.Max(1, sim.State.eco.keeperMistings);
                prep = "(tree tended by the cheat) ";
            }
            var b = d.bubbles.ForceTree(Time.unscaledTime);
            if (b == null) return prep + "every tree line for this stage is said (Clear said)";
            d.EndLine();
            d.Visible(pv, cam);
            d.ShowBubble(b);
            return prep + (d.ShownBubble != null ? "tree: " + b.text + " (" + d.LastWho + ")" : "nobody on screen to say: " + b.text);
        }

        /// <summary>Debug (#120): forget what's been said in this box.</summary>
        public static string DebugClearSaid()
        {
            var d = Instance;
            if (d == null) return "no ShowMeDirector";
            d.Bind();
            if (d.bubbles == null) return "no box";
            d.bubbles.Reset();
            return "said list cleared";
        }

        // ---------------- debug (#327 Option B)

        /// <summary>The named person nearest the screen's middle (a crowd person if no one named is on view), None with nobody on view.</summary>
        static PersonRef NearestMiddle(bool named)
        {
            var pv = PeopleView.Find(); var cam = Camera.main;
            var best = PersonRef.None;
            float bestD = float.MaxValue;
            if (pv == null || cam == null) return best;
            foreach (var a in pv.Activities)
            {
                if (named && !a.who.IsNotable) continue;
                if (a.kind == ActivityKind.Sleep || !pv.TryGetDollTop(a.who, out var w)) continue;
                var v = cam.WorldToViewportPoint(w);
                if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
                float d = (new Vector2(v.x, v.y) - new Vector2(0.5f, 0.6f)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = a.who; }
            }
            return best.IsNone && named ? NearestMiddle(false) : best;
        }

        /// <summary>Debug (#327): "Line: caption here": a line by the named person nearest the screen's middle, on the caption with their
        /// name, ring and speech mark (the Option B frame at any zoom).</summary>
        public static string DebugLineHere()
        {
            var who = NearestMiddle(true);
            if (who.IsNone) return "nobody on view";
            return DebugSayOver(who, "The beans by the pond are up. Come and see.") + " · " + who + " · ring " + (Instance != null && Instance.SpeakerRingUp);
        }

        /// <summary>Debug (#327): "Line: a scene line, then one that waits": two relic-like lines at once; the second waits for the first.</summary>
        public static string DebugWaitingLines()
        {
            var d = Instance;
            if (d == null) return "no ShowMeDirector";
            var who = NearestMiddle(true);
            if (who.IsNone) return "nobody on view";
            bool first = d.SayOrWait("Did you hear that? Up there.", who, 4f, LineKind.Relic);
            bool second = d.SayOrWait("Hush. Listen again.", who, 4f, LineKind.Relic);
            return $"first {(first ? "shows" : "waits")}, second {(second ? "shows" : "waits")} · waiting {d.WaitingLines}";
        }

        /// <summary>Debug (#327): "Speech mark": the speech mark alone over the person nearest the middle for 4 s (no line), to judge its look.</summary>
        public static string DebugSpeechMark()
        {
            var d = Instance;
            if (d == null) return "no ShowMeDirector";
            var who = NearestMiddle(true);
            if (who.IsNone) return "nobody on view";
            d.debugSpeechWho = who;
            d.debugSpeechUntil = Time.unscaledTime + 4f;
            return "speech mark over " + who + (NameOf(who) is string n ? " (" + n + ")" : "");
        }

        public static string DebugEnd() { var d = Instance; if (d == null) return "no ShowMeDirector"; d.End(); return "show me ended"; }
    }
}
