using AquariumCiv.Audio;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using T = AquariumCiv.View.Shaping.ShapingTuning;

namespace AquariumCiv.View
{
    /// <summary>
    /// The Keeper's shapings in the world (#85; game-definition §6, research 10 "Decision"): the rain, the grove, the
    /// channel and the herd drawn in the style kit from <see cref="ShapingShow"/>'s looks every frame, the people's answer
    /// shown (the planner stages it, a glow ring on the new building, a world label and the chronicle line as a bubble),
    /// and a take-back that plays each in reverse.
    /// <para>Until the shaping tray (#84) the way in is the debug panel: pick a tool and shape mode is on, then one finger
    /// on the ground places it (<see cref="ICameraGate"/>: two fingers stay the camera's; clean mode, the glass knock and
    /// the HUD never see that press). In shape mode each shaping the people haven't used has a "Take back" chip.</para>
    /// Added to the box at start-up like <see cref="CareView"/>.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public class ShapingView : MonoBehaviour, ICameraGate
    {
        public static bool AutoCreate = true;
        public static ShapingView Instance { get; private set; }

        /// <summary>A press that moves less than this share of the screen height and lifts within <see cref="TapMaxSeconds"/> is a tap.</summary>
        public const float TapSlop = 0.025f, TapMaxSeconds = 0.6f;
        public const float CloudHeight = 0.85f;
        /// <summary>Animals a little larger than life next to the dolls (Choice 5's big props): a herd reads at phone size.</summary>
        public const float AnimalScale = 1.35f, FishScale = 1.7f;
        const int MaxChips = 4;

        ShapingShow show = new();
        readonly Dictionary<int, Vis> vis = new();
        readonly List<ShapingEvent> events = new();
        SimCore boundSim;
        BoxView box;
        BoxCamera cam;
        GameHud hud;
        Transform root;
        Material fxMat;
        bool overlaysRefreshed;

        /// <summary>The tracker (tests, the debug panel, #84's tray).</summary>
        public ShapingShow Show => show;
        /// <summary>The tool in the Keeper's hand (a <see cref="SimCore.NudgeKind"/>), or null: shape mode off.</summary>
        public string Tool { get; private set; }
        public bool ShapeMode => Tool != null;
        /// <summary>Debug: place without spending a charge or minding the era lock and caps.</summary>
        public bool Free = true;
        public string LastResult { get; private set; } = "";
        /// <summary>Everything that happened (tests), oldest first, at most 64.</summary>
        public readonly List<ShapingEvent> EventLog = new();
        public Transform Root => root;

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        Camera Cam => cam != null ? cam.GetComponent<Camera>() : Camera.main;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<ShapingView>() == null) b.gameObject.AddComponent<ShapingView>();
        }

        // ================================================================== lifecycle

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            SimLayout.KeeperOverlays = true; // the layout leaves the Keeper's groves and herds to us
            overlaysRefreshed = false;
            Hook();
            BuildUi();
            UiInput.Add(IsOverUi);
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            cam?.RemoveGate(this);
            if (hud != null) hud.CleanModeChanged -= OnCleanMode;
            UiInput.Remove(IsOverUi);
            SimLayout.KeeperOverlays = false;
            if (box != null && Application.isPlaying) box.Refresh();
            ClearVisuals();
            if (root != null) Destroy(root.gameObject);
            if (uiGo != null) Destroy(uiGo);
        }

        void Hook()
        {
            cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();
            cam?.AddGate(this);
            var h = GameHud.Instance;
            if (h != hud)
            {
                if (hud != null) hud.CleanModeChanged -= OnCleanMode;
                hud = h;
                if (hud != null) hud.CleanModeChanged += OnCleanMode;
            }
        }

        void OnCleanMode(bool on) { if (on) SetTool(null); }

        // ================================================================== the calls

        /// <summary>Pick a tool (shape mode on) or null (off). Clean mode goes off; a hint says what to do.</summary>
        public void SetTool(string kind)
        {
            if (kind != null && System.Array.IndexOf(SimCore.SliceTools, kind) < 0) kind = null;
            Tool = kind;
            if (kind == null) return;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            var sim = Sim;
            string name = sim != null ? sim.ShapingToolName(kind) : kind;
            hud?.Toast($"Tap the ground to place the {name}. Two fingers move the camera.", 4f);
        }

        /// <summary>Place the tool in hand (or <paramref name="kind"/>) at a box point. Free in debug; else it spends a charge
        /// and says why when it can't.</summary>
        public string PlaceAt(float x, float z, string kind = null, bool? free = null)
        {
            var sim = Sim;
            kind ??= Tool;
            if (sim == null || kind == null) return LastResult = "no sim or no tool";
            if (free ?? Free)
            {
                var n = show.DebugPlace(sim, kind, x, z);
                LastResult = n == null ? "no such tool" : $"{kind} #{n.id} at ({x:0.00}, {z:0.00})";
            }
            else
            {
                var r = show.Place(sim, kind, x, z);
                if (!r.Ok)
                {
                    LastResult = FailText(sim, kind, r.fail);
                    Sfx.Play(ShapingShow.FailCue); // soft: "not now", the text says why
                    hud?.Toast(LastResult, 3f);
                    return LastResult;
                }
                LastResult = $"{kind} #{r.nudge.id} at ({x:0.00}, {z:0.00})";
            }
            Changed();
            return LastResult;
        }

        static string FailText(SimCore sim, string kind, SimCore.ShapeFail f) => f switch
        {
            SimCore.ShapeFail.Locked => $"The {sim.ShapingToolName(kind)} comes with the village.",
            SimCore.ShapeFail.NoCharge => "No shaping left for now: cleaning the glass brings it back.",
            SimCore.ShapeFail.AtCap => $"That's as many as the box holds ({SimCore.ShapingCap(kind)} at once).",
            _ => "That tool can't be used here.",
        };

        /// <summary>Take a shaping back (the chip). False once the people have used it.</summary>
        public bool TakeBack(int nudgeId)
        {
            var sim = Sim;
            if (sim == null || !show.TakeBack(sim, nudgeId)) return false;
            LastResult = $"took back #{nudgeId}";
            Changed();
            return true;
        }

        /// <summary>Debug: the people answer this shaping now (−1 = the newest one waiting).</summary>
        public bool SkipToAnswer(int nudgeId = -1)
        {
            var sim = Sim;
            if (sim == null) return false;
            if (nudgeId < 0)
            {
                var n = sim.State.nudges.FindLast(x => !x.credited && System.Array.IndexOf(SimCore.SliceTools, x.kind) >= 0);
                if (n == null) { LastResult = "nothing waiting for an answer"; return false; }
                nudgeId = n.id;
            }
            if (!show.SkipToAnswer(sim, nudgeId)) { LastResult = "already answered"; return false; }
            LastResult = $"people answered #{nudgeId}";
            Changed();
            return true;
        }

        void Changed()
        {
            if (box != null) box.Refresh();
            if (GameClock.Instance != null && GameClock.Instance.Sim != null) GameClock.Instance.Save();
        }

        // ---------- `script eval` / tests ----------
        public static string DebugPlace(string kind, float x, float z, bool free = true) => Instance == null ? "no ShapingView" : Instance.PlaceAt(x, z, kind, free);
        public static string DebugTakeBack(int id = -1)
        {
            var v = Instance; if (v == null) return "no ShapingView";
            if (id < 0) { for (int i = v.show.Entries.Count - 1; i >= 0; i--) if (v.Sim != null && v.Sim.CanTakeBack(v.show.Entries[i].nudgeId)) { id = v.show.Entries[i].nudgeId; break; } }
            return v.TakeBack(id) ? v.LastResult : "nothing to take back";
        }
        public static string DebugSkip(int id = -1) { var v = Instance; if (v == null) return "no ShapingView"; v.SkipToAnswer(id); return v.LastResult; }

        // ================================================================== the camera gate and the tap

        bool Usable()
        {
            if (!ShapeMode || box == null || Sim == null) return false;
            if (hud != null && (hud.CleanMode || hud.SheetOpen || hud.ToolsHidden || hud.CameraMode)) return false;
            var mp = MomentPlayer.Instance;
            if (mp != null && mp.Playing) return false;
            var tl = TimeLapseView.Find();
            return tl == null || !tl.Active;
        }

        /// <summary>In shape mode a one-finger press on the ground is the tool's (not on the UI, not in clean mode).</summary>
        public bool ClaimsPress(Vector2 screenPos) => Usable() && !UiInput.Over(screenPos) && GroundPoint(screenPos, out _, out _);

        bool pressDown, pressOk;
        int pressChip;
        Vector2 pressPos;
        float pressTime;

        static int ReadPointers(out Vector2 a)
        {
            a = default;
            int n = 0;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.isInProgress) continue;
                    if (n == 0) a = t.position.ReadValue();
                    n++;
                }
                if (n > 0) return n;
            }
            var m = Mouse.current;
            if (m == null) return 0;
            a = m.position.ReadValue();
            return m.leftButton.isPressed ? 1 : (m.rightButton.isPressed || m.middleButton.isPressed) ? 2 : 0;
        }

        void HandleInput()
        {
            int n = ReadPointers(out var a);
            float now = Time.unscaledTime;
            if (n == 0)
            {
                bool tap = pressDown && now - pressTime <= TapMaxSeconds;
                if (tap && pressChip > 0) TakeBack(pressChip); // the chip's own click may have done it already: then this is a no-op
                else if (tap && pressOk && GroundPoint(pressPos, out float x, out float z)) PlaceAt(x, z);
                pressDown = false;
                return;
            }
            if (!pressDown)
            {
                pressDown = true; pressPos = a; pressTime = now;
                pressChip = n == 1 ? ChipAt(a) : 0;
                pressOk = n == 1 && pressChip == 0 && ClaimsPress(a);
                return;
            }
            if (n >= 2 || (a - pressPos).magnitude > TapSlop * Mathf.Max(1f, Screen.height)) { pressOk = false; pressChip = 0; }
        }

        /// <summary>The ground under a screen point, in box coordinates (a march along the ray over the land's height).</summary>
        public bool GroundPoint(Vector2 screenPos, out float x, out float z)
        {
            x = z = 0f;
            var c = Cam;
            if (c == null || box == null) return false;
            var ray = c.ScreenPointToRay(screenPos);
            var o = box.transform.InverseTransformPoint(ray.origin);
            var d = box.transform.InverseTransformDirection(ray.direction).normalized;
            const float step = 0.04f; // 1500 steps reach 60 units: the camera may be 30+ away at the default framing (#94)
            float prev = 0f;
            bool wasAbove = true;
            for (int i = 1; i < 1500; i++)
            {
                float t = i * step;
                var p = o + d * t;
                bool inside = Mathf.Abs(p.x) <= Land.HX && Mathf.Abs(p.z) <= Land.HZ;
                if (inside && p.y <= box.GroundHeight(p.x, p.z))
                {
                    if (!wasAbove) return false;
                    float lo = prev, hi = t;
                    for (int k = 0; k < 12; k++)
                    {
                        float m = (lo + hi) * 0.5f;
                        var q = o + d * m;
                        if (q.y <= box.GroundHeight(Mathf.Clamp(q.x, -Land.HX, Land.HX), Mathf.Clamp(q.z, -Land.HZ, Land.HZ))) hi = m; else lo = m;
                    }
                    var h = o + d * hi;
                    x = h.x; z = h.z;
                    return true;
                }
                if (p.y < -1f) return false;
                prev = t;
            }
            return false;
        }

        /// <summary>The screen point over a box point at a height above the ground (tests and the tester agent touch here).</summary>
        public Vector2 ScreenOf(float x, float z, float up = 0f)
        {
            var c = Cam;
            if (c == null || box == null) return default;
            var w = box.transform.TransformPoint(new Vector3(x, box.GroundHeight(x, z) + up, z));
            var s = c.WorldToScreenPoint(w);
            return new Vector2(s.x, s.y);
        }

        // ================================================================== the frame

        void Update()
        {
            var sim = Sim;
            if (sim == null || box == null) return;
            if (hud == null || cam == null) Hook();
            if (!overlaysRefreshed && box.Root != null) { overlaysRefreshed = true; box.Refresh(); }
            if (sim != boundSim)
            {
                boundSim = sim;
                ClearVisuals();
                show = new ShapingShow();
            }
            var pv = PeopleView.Find();
            var planner = pv != null && pv.Planner != null && pv.Planner.Sim == sim ? pv.Planner : null;
            var now = pv != null ? pv.Now : new ViewTime(sim.State.month, 10f);
            show.Sync(sim, Time.unscaledTime, now, planner, events);
            foreach (var e in events)
            {
                EventLog.Add(e);
                if (!string.IsNullOrEmpty(e.cue)) Sfx.Play(e.cue); // #96: a tool landing, the people answering, one taken back
                if (e.kind == ShapingEventKind.Gone || e.kind == ShapingEventKind.TakenBack) box.Refresh();
            }
            while (EventLog.Count > 64) EventLog.RemoveAt(0);
            if (Usable()) HandleInput(); else pressDown = false;
        }

        void LateUpdate()
        {
            var sim = Sim;
            if (sim == null || box == null || box.Root == null) return;
            if (root == null)
            {
                root = new GameObject("Shapings") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(transform, false);
            }
            var tl = TimeLapseView.Find();
            var src = SimLayoutSource.Find();
            bool hidden = (tl != null && tl.Active) || (src != null && src.Sim != sim); // the replay or another box on show
            root.gameObject.SetActive(!hidden);
            if (hidden) { HideUi(); return; }
            fxMat ??= LoadFx();
            float t = Time.unscaledTime;
            var seen = new HashSet<int>();
            foreach (var e in show.Entries)
            {
                seen.Add(e.nudgeId);
                if (!vis.TryGetValue(e.nudgeId, out var v)) vis[e.nudgeId] = v = new Vis(this, e);
                switch (e.kind)
                {
                    case SimCore.NudgeKind.Rain: v.Rain(show.Rain(e), t); break;
                    case SimCore.NudgeKind.Grove: v.Grove(show.Grove(e)); break;
                    case SimCore.NudgeKind.Channel: v.Channel(show.Channel(e), t); break;
                    case SimCore.NudgeKind.Herd: v.Herd(show.Herd(e), show.ClockOf(e), PastureFade(sim, e), t); break;
                }
            }
            var gone = new List<int>();
            foreach (var kv in vis) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { vis[id].Destroy(); vis.Remove(id); }
            DrawAnswer(show.Answer, t);
            UpdateUi(sim);
        }

        /// <summary>An answered herd that went to a pasture joins the pasture's own flock: it fades once it gets there.</summary>
        float PastureFade(SimCore sim, ShapingShow.Entry e)
        {
            if (e.buildingId < 0 || sim.Settlement == null) return 1f;
            var b = sim.State.buildings.Find(q => q.id == e.buildingId);
            if (b == null || b.lotId == 0) return 1f;
            var lot = sim.Settlement.Lots.Find(l => l.id == b.lotId);
            if (lot == null || lot.kind != LotKind.Pasture) return 1f;
            var c = show.ClockOf(e);
            return 1f - Mathf.Clamp01((c.sinceAnswered - T.HerdMoveToLotSeconds) / 1.5f);
        }

        static Material LoadFx()
        {
            var m = Resources.Load<Material>("ShapingFx");
            if (m != null) return m;
            var sh = Shader.Find("AquariumCiv/View/ShapingFx");
            return sh != null ? new Material(sh) { hideFlags = HideFlags.DontSave } : null;
        }

        void ClearVisuals()
        {
            foreach (var v in vis.Values) v.Destroy();
            vis.Clear();
            if (answerGo != null) { Destroy(answerGo); answerGo = null; }
        }

        Vector3 Ground(float x, float z) => new(x, box.GroundHeight(Mathf.Clamp(x, -Land.HX, Land.HX), Mathf.Clamp(z, -Land.HZ, Land.HZ)), z);

        // ================================================================== the answer: ring, beam

        GameObject answerGo;
        ShapingMeshes.FxMesh answerFx;
        MeshRenderer answerR;
        MaterialPropertyBlock answerMpb;

        void DrawAnswer(AnswerLook a, float t)
        {
            if (!a.active || a.highlight <= 0.001f || fxMat == null)
            {
                if (answerGo != null) answerGo.SetActive(false);
                return;
            }
            if (answerGo == null)
            {
                answerFx = new ShapingMeshes.FxMesh("Shaping_AnswerGlow");
                answerR = ShapingMeshes.FxRenderer(root, "AnswerGlow", answerFx, fxMat);
                answerGo = answerR.gameObject;
                answerMpb = new MaterialPropertyBlock();
            }
            answerGo.SetActive(true);
            var g = Ground(a.x, a.z) + Vector3.up * 0.02f;
            float pulse = 1f + 0.06f * Mathf.Sin(t * 3f);
            answerFx.Clear();
            var glow = Palette.Glow;
            answerFx.Ring(g, 0.26f * pulse, 0.36f * pulse, 32, new Color(glow.r, glow.g, glow.b, 0.85f), new Color(glow.r, glow.g, glow.b, 0f));
            answerFx.Beam(g, 0.24f, 0.9f, 20, new Color(1f, 0.9f, 0.6f, 0.35f), new Color(1f, 0.9f, 0.6f, 0f));
            answerFx.Apply();
            answerMpb.SetColor("_Color", new Color(1f, 1f, 1f, a.highlight));
            answerR.SetPropertyBlock(answerMpb);
        }

        // ================================================================== the UI: the world label, the bubble, the chips

        GameObject uiGo;
        UIDocument doc;
        VisualElement uiRoot;
        Label labelEl, bubbleEl;
        readonly List<Button> chipEls = new();
        readonly int[] chipIds = new int[MaxChips];

        /// <summary>The world label and bubble showing now (tests).</summary>
        public string ShownLabel => labelEl != null && labelEl.style.display == DisplayStyle.Flex ? labelEl.text : null;
        public string ShownBubble => bubbleEl != null && bubbleEl.style.display == DisplayStyle.Flex ? bubbleEl.text : null;
        /// <summary>The take-back chips showing now: nudge id and the chip's screen centre (tests, the tester agent).</summary>
        public List<(int id, Vector2 screen)> Chips()
        {
            var l = new List<(int, Vector2)>();
            for (int i = 0; i < chipEls.Count; i++)
                if (chipEls[i].style.display == DisplayStyle.Flex && chipEls[i].panel != null) l.Add((chipIds[i], GameUiPanel.ScreenCenter(chipEls[i])));
            return l;
        }

        void BuildUi()
        {
            uiGo = new GameObject("ShapingUI") { hideFlags = HideFlags.DontSave };
            uiGo.transform.SetParent(transform, false);
            doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 11; // over the HUD's world, under the moment player
            uiRoot = doc.rootVisualElement;
            uiRoot.pickingMode = PickingMode.Ignore;
            uiRoot.style.position = Position.Absolute;
            uiRoot.style.left = uiRoot.style.right = uiRoot.style.top = uiRoot.style.bottom = 0;
            labelEl = Pill("shaping-label", 30);
            labelEl.style.backgroundColor = new Color(0.94f, 0.89f, 0.8f, 0.94f); // the HUD's plaster pill: the gift's name
            labelEl.style.color = new Color(0.42f, 0.29f, 0.21f);
            bubbleEl = Pill("shaping-bubble", 34);
            bubbleEl.style.maxWidth = 760;
            bubbleEl.style.whiteSpace = WhiteSpace.Normal;
            for (int i = 0; i < MaxChips; i++)
            {
                int k = i;
                var b = new Button(() => { if (chipIds[k] > 0) TakeBack(chipIds[k]); }) { name = "shaping-takeback-" + i, text = "Take back" };
                b.AddToClassList("hud-btn");
                b.style.position = Position.Absolute;
                b.style.height = GameUiPanel.MinTarget * 0.8f;
                b.style.minWidth = 0;
                b.style.fontSize = 32;
                b.style.display = DisplayStyle.None;
                uiRoot.Add(b);
                chipEls.Add(b);
            }
        }

        Label Pill(string name, int fontSize)
        {
            var l = new Label { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList("hud-toast");
            l.style.position = Position.Absolute;
            l.style.right = StyleKeyword.Auto; // hud-toast stretches across the bottom; this one sits on its anchor
            l.style.bottom = StyleKeyword.Auto;
            l.style.fontSize = fontSize;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.display = DisplayStyle.None;
            uiRoot.Add(l);
            return l;
        }

        bool IsOverUi(Vector2 screenPos) => uiRoot != null && GameUiPanel.Over(uiRoot, screenPos);

        /// <summary>The nudge whose take-back chip is under a screen point (0 = none).</summary>
        int ChipAt(Vector2 screenPos)
        {
            for (int i = 0; i < chipEls.Count; i++)
            {
                var c = chipEls[i];
                if (c.style.display != DisplayStyle.Flex || c.panel == null || chipIds[i] <= 0) continue;
                var p = RuntimePanelUtils.ScreenToPanel(c.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
                if (c.worldBound.Contains(p)) return chipIds[i];
            }
            return 0;
        }

        void HideUi()
        {
            if (labelEl == null) return;
            labelEl.style.display = DisplayStyle.None;
            bubbleEl.style.display = DisplayStyle.None;
            foreach (var c in chipEls) c.style.display = DisplayStyle.None;
        }

        /// <summary>Puts an element centred over a box point (panel units); false when the point is behind the camera.</summary>
        bool Anchor(VisualElement e, float x, float z, float up, float dyPanel = 0f, bool below = false)
        {
            var c = Cam;
            if (c == null || e.panel == null) return false;
            var w = box.transform.TransformPoint(Ground(x, z) + Vector3.up * up);
            var s = c.WorldToScreenPoint(w);
            if (s.z <= 0f) return false;
            var p = RuntimePanelUtils.ScreenToPanel(e.panel, new Vector2(s.x, Screen.height - s.y));
            e.style.left = p.x;
            e.style.top = p.y + dyPanel;
            e.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(below ? 0 : -100, LengthUnit.Percent));
            return true;
        }

        void UpdateUi(SimCore sim)
        {
            if (uiRoot == null) return;
            bool noUi = hud != null && (hud.CameraMode || hud.SheetOpen || !hud.IsVisible);
            var a = show.Answer;
            bool label = !noUi && a.active && a.labelAlpha > 0.01f && Anchor(labelEl, a.x, a.z, 0.35f);
            labelEl.style.display = label ? DisplayStyle.Flex : DisplayStyle.None;
            if (label) { labelEl.text = a.label; labelEl.style.opacity = a.labelAlpha; }
            bool bubble = !noUi && a.active && a.bubbleAlpha > 0.01f && !string.IsNullOrEmpty(a.bubble) && Anchor(bubbleEl, a.x, a.z, 0.35f, -80f);
            bubbleEl.style.display = bubble ? DisplayStyle.Flex : DisplayStyle.None;
            if (bubble) { bubbleEl.text = a.bubble; bubbleEl.style.opacity = a.bubbleAlpha; }

            int k = 0;
            if (!noUi && Usable())
                for (int i = show.Entries.Count - 1; i >= 0 && k < MaxChips; i--)
                {
                    var e = show.Entries[i];
                    var chip = show.ChipFor(sim, e.nudgeId);
                    if (!chip.visible || show.StageOf(e) == ShapingStage.Arriving) continue; // let the arrival be seen first
                    if (!Anchor(chipEls[k], chip.x, chip.z, 0f, 40f, true)) continue; // under the gift, never over it
                    chipIds[k] = e.nudgeId;
                    chipEls[k].style.display = DisplayStyle.Flex;
                    k++;
                }
            for (; k < MaxChips; k++) { chipEls[k].style.display = DisplayStyle.None; chipIds[k] = 0; }
        }

        // ================================================================== one shaping's objects

        sealed class Vis
        {
            readonly ShapingView owner;
            readonly ShapingShow.Entry entry;
            readonly Transform root;
            readonly MaterialPropertyBlock mpb = new();

            public Vis(ShapingView owner, ShapingShow.Entry e)
            {
                this.owner = owner;
                entry = e;
                root = new GameObject($"Shaping_{e.kind}_{e.nudgeId}") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(owner.root, false);
            }

            public void Destroy() { if (root != null) Object.Destroy(root.gameObject); }

            Material Solid => owner.box.SolidMaterial;
            Vector3 Ground(float x, float z) => owner.Ground(x, z);

            void Tint(Renderer r, float alpha)
            {
                mpb.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
                r.SetPropertyBlock(mpb);
            }

            // ---------------------------------------------------------- rain
            Transform cloud;
            bool cloudStorm;
            Light flash;
            ShapingMeshes.FxMesh streaks, wet;
            MeshRenderer streakR, wetR;
            readonly List<Transform> sprouts = new();
            int streakCount;

            public void Rain(RainLook r, float t)
            {
                if (owner.fxMat == null) return;
                var g = Ground(r.x, r.z);
                if (cloud == null || cloudStorm != r.storm)
                {
                    if (cloud != null) Object.Destroy(cloud.gameObject);
                    cloudStorm = r.storm;
                    cloud = ShapingMeshes.Solid(root, "Cloud", ShapingMeshes.Cloud(r.storm), Solid, false);
                }
                float cs = ShapingLooks.Smooth(r.cloud) * (r.storm ? 1.3f : 1f);
                cloud.gameObject.SetActive(cs > 0.01f);
                cloud.localPosition = g + Vector3.up * CloudHeight + new Vector3(Mathf.Sin(t * 0.4f) * 0.03f, Mathf.Sin(t * 0.7f) * 0.01f, 0f);
                cloud.localScale = Vector3.one * Mathf.Max(0.001f, cs);

                // the streaks, falling from the cloud to the ground
                if (streaks == null)
                {
                    streaks = new ShapingMeshes.FxMesh("Shaping_Rain");
                    streakR = ShapingMeshes.FxRenderer(root, "Rain", streaks, owner.fxMat);
                    streakCount = r.storm ? 220 : entry.footprint.level == Era.Village ? 150 : 100;
                }
                streakR.gameObject.SetActive(r.rain > 0.01f);
                if (r.rain > 0.01f)
                {
                    streaks.Clear();
                    var col = new Color(0.82f, 0.9f, 1f, 0.75f);
                    float top = g.y + CloudHeight - 0.08f;
                    for (int i = 0; i < streakCount; i++)
                    {
                        float a = Land.Hash3(entry.footprint.seed, i, 1) * Mathf.PI * 2f;
                        float rad = Mathf.Sqrt(Land.Hash3(entry.footprint.seed, i, 2)) * 0.32f * cs;
                        float x = r.x + Mathf.Cos(a) * rad, z = r.z + Mathf.Sin(a) * rad;
                        float gy = owner.box.GroundHeight(Mathf.Clamp(x, -Land.HX, Land.HX), Mathf.Clamp(z, -Land.HZ, Land.HZ));
                        float u = Mathf.Repeat(t * (r.storm ? 2.2f : 1.6f) + Land.Hash3(entry.footprint.seed, i, 3), 1f);
                        float y = Mathf.Lerp(top, gy, u);
                        float len = r.storm ? 0.09f : 0.065f;
                        var p0 = new Vector3(x, y, z);
                        var p1 = new Vector3(x - (r.storm ? 0.02f : 0.005f), y + len, z);
                        const float w = 0.004f;
                        streaks.Quad(p0 + new Vector3(-w, 0, 0), p1 + new Vector3(-w, 0, 0), p1 + new Vector3(w, 0, 0), p0 + new Vector3(w, 0, 0), col);
                        streaks.Quad(p0 + new Vector3(0, 0, -w), p1 + new Vector3(0, 0, -w), p1 + new Vector3(0, 0, w), p0 + new Vector3(0, 0, w), col);
                    }
                    streaks.Apply();
                    Tint(streakR, r.rain);
                }

                // the darkened soil
                if (wet == null)
                {
                    wet = new ShapingMeshes.FxMesh("Shaping_Wet");
                    wetR = ShapingMeshes.FxRenderer(root, "WetSoil", wet, owner.fxMat);
                    var col = Palette.SoilDark;
                    const float h = SiteMap.Cell * 0.5f;
                    foreach (int c in entry.footprint.cells)
                    {
                        float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                        float fall = 1f - Mathf.Clamp01((Vector2.Distance(new Vector2(x, z), new Vector2(r.x, r.z)) - 0.3f) / 0.25f); // soft edge
                        var cc = new Color(col.r, col.g, col.b, 0.5f * Mathf.Max(0.25f, fall));
                        Vector3 P(float px, float pz) => Ground(px, pz) + Vector3.up * 0.006f;
                        wet.Quad(P(x - h, z - h), P(x - h, z + h), P(x + h, z + h), P(x + h, z - h), cc);
                    }
                    wet.Apply();
                }
                wetR.gameObject.SetActive(r.wet > 0.01f);
                if (r.wet > 0.01f) Tint(wetR, r.wet);

                // sprouts on the wet ground
                if (sprouts.Count == 0)
                {
                    int k = 0;
                    foreach (int c in entry.footprint.cells)
                    {
                        if (Land.Hash3(entry.footprint.seed, c, 9) > 0.45f) continue;
                        float x = SiteMap.CXc(c) + (Land.Hash3(entry.footprint.seed, c, 10) - 0.5f) * 0.06f;
                        float z = SiteMap.CZc(c) + (Land.Hash3(entry.footprint.seed, c, 11) - 0.5f) * 0.06f;
                        var s = ShapingMeshes.Solid(root, "Sprout", ShapingMeshes.Sprouts(k++ % 3), Solid, false);
                        s.localPosition = Ground(x, z);
                        s.localRotation = Quaternion.Euler(0f, Land.Hash3(entry.footprint.seed, c, 12) * 360f, 0f);
                        sprouts.Add(s);
                    }
                }
                float sp = r.sprouts;
                foreach (var s in sprouts)
                {
                    s.gameObject.SetActive(sp > 0.02f);
                    s.localScale = new Vector3(0.6f + 0.4f * sp, Mathf.Max(0.001f, sp), 0.6f + 0.4f * sp);
                }

                // a storm's lightning
                if (r.storm)
                {
                    if (flash == null)
                    {
                        var go = new GameObject("Lightning") { hideFlags = HideFlags.DontSave };
                        go.transform.SetParent(root, false);
                        flash = go.AddComponent<Light>();
                        flash.type = LightType.Point;
                        flash.range = 2.5f;
                        flash.color = new Color(0.85f, 0.9f, 1f);
                        flash.shadows = LightShadows.None;
                    }
                    flash.transform.localPosition = g + Vector3.up * (CloudHeight - 0.15f);
                    flash.intensity = r.flash * 6f;
                    flash.enabled = r.flash > 0.01f;
                }
            }

            // ---------------------------------------------------------- grove
            readonly Dictionary<int, (Transform t, string key)> trees = new();

            public void Grove(GroveLook g)
            {
                var seen = new HashSet<int>();
                foreach (var tr in g.trees)
                {
                    seen.Add(tr.seed);
                    bool blossom = tr.blossom > 0.5f, fruit = tr.fruit > 0.5f;
                    int variant = (tr.seed & 0x7fffffff) % 4;
                    string key = $"{g.style}{blossom}{fruit}{variant}";
                    if (!trees.TryGetValue(tr.seed, out var it) || it.t == null)
                    {
                        var t = ShapingMeshes.Solid(root, "Tree", ShapingMeshes.Tree(g.style, blossom, fruit, variant), Solid);
                        t.localRotation = Quaternion.Euler(0f, (tr.seed & 0xffff) % 360, 0f);
                        trees[tr.seed] = it = (t, key);
                    }
                    else if (it.key != key)
                    {
                        it.t.GetComponent<MeshFilter>().sharedMesh = ShapingMeshes.Tree(g.style, blossom, fruit, variant);
                        trees[tr.seed] = it = (it.t, key);
                    }
                    it.t.gameObject.SetActive(tr.growth > 0.005f);
                    it.t.localPosition = Ground(tr.x, tr.z);
                    it.t.localScale = Vector3.one * Mathf.Max(0.001f, tr.growth);
                }
                var drop = new List<int>();
                foreach (var kv in trees) if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
                foreach (int k in drop) { if (trees[k].t != null) Object.Destroy(trees[k].t.gameObject); trees.Remove(k); }
            }

            // ---------------------------------------------------------- channel
            ShapingMeshes.FxMesh water, trench;
            MeshRenderer waterR, trenchR;
            MeshFilter capsF;
            Mesh capsMesh;
            string capsSig = "";
            Transform wheel;
            readonly List<Transform> sluices = new();

            public void Channel(ChannelLook ch, float t)
            {
                if (owner.fxMat == null || ch.cells.Count == 0) return;
                var land = owner.box.land;
                const float h = SiteMap.Cell * 0.5f;
                bool leaving = entry.leavingAt >= 0f;

                // Turf caps over the cells not yet carved (the layout has already cut the trench): they shrink as the cut runs.
                var sig = new System.Text.StringBuilder();
                foreach (var c in ch.cells) sig.Append(c.carved >= 1f ? '1' : c.carved <= 0f ? '0' : (char)('a' + (int)(c.carved * 20f)));
                string s = leaving ? "" : sig.ToString(); // after a take-back the land is whole again: no caps
                if (s != capsSig)
                {
                    capsSig = s;
                    var mb = new MeshBuilder(entry.footprint.seed) { jitter = 0f };
                    if (!leaving)
                        foreach (var c in ch.cells)
                        {
                            if (c.carved >= 1f) continue;
                            float k = h * (1.05f - c.carved);
                            float y = land.Height(c.x, c.z) + 0.004f;
                            var col = Color.Lerp(Palette.GrassA, Palette.GrassB, Land.Hash3(entry.footprint.seed, c.cell, 21));
                            mb.Quad(new Vector3(c.x - k, y, c.z - k), new Vector3(c.x - k, y, c.z + k), new Vector3(c.x + k, y, c.z + k), new Vector3(c.x + k, y, c.z - k), col);
                        }
                    if (capsF == null)
                    {
                        var go = new GameObject("TurfCaps") { hideFlags = HideFlags.DontSave };
                        go.transform.SetParent(root, false);
                        capsF = go.AddComponent<MeshFilter>();
                        go.AddComponent<MeshRenderer>().sharedMaterial = Solid;
                    }
                    if (capsMesh != null) Object.Destroy(capsMesh);
                    capsMesh = mb.ToMesh("Shaping_TurfCaps");
                    capsMesh.hideFlags = HideFlags.DontSave;
                    capsF.sharedMesh = capsMesh;
                }

                // The water, with a shimmer running down the channel; on a take-back the dark trench closes after it drains.
                if (water == null)
                {
                    water = new ShapingMeshes.FxMesh("Shaping_ChannelWater");
                    waterR = ShapingMeshes.FxRenderer(root, "Water", water, owner.fxMat);
                    trench = new ShapingMeshes.FxMesh("Shaping_Trench");
                    trenchR = ShapingMeshes.FxRenderer(root, "Trench", trench, owner.fxMat);
                }
                water.Clear(); trench.Clear();
                for (int i = 0; i < ch.cells.Count; i++)
                {
                    var c = ch.cells[i];
                    float y = (leaving ? land.Height(c.x, c.z) : owner.box.GroundHeight(c.x, c.z)) + 0.012f;
                    if (c.water > 0.01f)
                    {
                        float shimmer = 0.5f + 0.5f * Mathf.Sin(t * 4f - i * 0.7f);
                        var col = Color.Lerp(Palette.Water, Palette.Plaster, 0.25f * shimmer);
                        col.a = 0.75f * c.water;
                        float k = h * 0.8f;
                        water.Quad(new Vector3(c.x - k, y, c.z - k), new Vector3(c.x - k, y, c.z + k), new Vector3(c.x + k, y, c.z + k), new Vector3(c.x + k, y, c.z - k), col);
                    }
                    if (leaving && c.carved > 0.01f)
                    {
                        var col = Palette.SoilDark; col.a = 0.8f * c.carved;
                        float k = h * 0.75f * c.carved;
                        trench.Quad(new Vector3(c.x - k, y - 0.004f, c.z - k), new Vector3(c.x - k, y - 0.004f, c.z + k), new Vector3(c.x + k, y - 0.004f, c.z + k), new Vector3(c.x + k, y - 0.004f, c.z - k), col);
                    }
                }
                water.Apply(); trench.Apply();
                Tint(waterR, 1f); Tint(trenchR, 1f);

                // the town's mill race: a wheel turning at the little fall
                bool race = ch.style == ChannelStyle.MillRace && ch.fallAt > 0 && ch.fallAt < ch.cells.Count - 1;
                if (race && wheel == null) wheel = ShapingMeshes.Solid(root, "WaterWheel", ShapingMeshes.WaterWheel(), Solid);
                if (wheel != null)
                {
                    var f = ch.cells[Mathf.Clamp(ch.fallAt, 0, ch.cells.Count - 1)];
                    bool on = race && f.carved >= 1f;
                    wheel.gameObject.SetActive(on);
                    if (on)
                    {
                        var a = ch.cells[ch.fallAt - 1]; var b = ch.cells[ch.fallAt + 1];
                        var dir = new Vector3(b.x - a.x, 0f, b.z - a.z);
                        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
                        wheel.localPosition = Ground(f.x, f.z) + Vector3.up * 0.05f;
                        wheel.localRotation = Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(-t * 90f * f.water, 0f, 0f);
                    }
                }
                // the village's irrigation: little sluice boards along it once the farmers line it with fields
                bool irrigate = ch.style == ChannelStyle.Irrigation && !leaving;
                if (irrigate && sluices.Count == 0)
                    for (int i = 2; i < ch.cells.Count - 1; i += 4)
                    {
                        var a = ch.cells[i - 1]; var b = ch.cells[i + 1]; var c = ch.cells[i];
                        var dir = new Vector3(b.x - a.x, 0f, b.z - a.z);
                        var sl = ShapingMeshes.Solid(root, "Sluice", ShapingMeshes.Sluice(), Solid);
                        sl.localPosition = Ground(c.x, c.z);
                        sl.localRotation = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
                        sluices.Add(sl);
                    }
                foreach (var sl in sluices) sl.gameObject.SetActive(irrigate);
            }

            // ---------------------------------------------------------- herd
            readonly List<Transform> animals = new();
            ShapingMeshes.FxMesh splash;
            MeshRenderer splashR;
            HerdSpecies species;

            public void Herd(HerdLook h, ShapingClock c, float fade, float t)
            {
                if (animals.Count != h.animals.Count || species != h.species)
                {
                    foreach (var a in animals) if (a != null) Object.Destroy(a.gameObject);
                    animals.Clear();
                    species = h.species;
                    for (int i = 0; i < h.animals.Count; i++)
                        animals.Add(ShapingMeshes.Solid(root, h.species.ToString(), ShapingMeshes.Animal(h.species), Solid, h.species != HerdSpecies.Fish));
                }
                bool fish = h.species == HerdSpecies.Fish;
                for (int i = 0; i < h.animals.Count; i++)
                {
                    var a = h.animals[i];
                    var tr = animals[i];
                    float alpha = a.alpha * fade;
                    bool show = a.pose != AnimalPose.Hidden && alpha > 0.01f;
                    tr.gameObject.SetActive(show);
                    if (!show) continue;
                    float ph = (a.seed & 0xffff) * 0.001f;
                    float yaw = a.heading * Mathf.Rad2Deg, pitch = 0f, bob = 0f;
                    switch (a.pose)
                    {
                        case AnimalPose.Run: bob = Mathf.Abs(Mathf.Sin(t * 14f + ph)) * 0.014f; break;
                        case AnimalPose.Walk: bob = Mathf.Abs(Mathf.Sin(t * 8f + ph)) * 0.006f; break;
                        case AnimalPose.Graze: pitch = 14f + 4f * Mathf.Sin(t * 2.3f + ph); break;
                        case AnimalPose.Swim: yaw += 10f * Mathf.Sin(t * 9f + ph); break;
                        case AnimalPose.Dive: pitch = 40f; break;
                    }
                    // just at the surface, so the shoal reads through the water at phone size
                    var p = fish ? new Vector3(a.x, Land.WaterY + 0.002f - (a.pose == AnimalPose.Dive ? (1f - alpha) * 0.05f : 0f), a.z) : Ground(a.x, a.z);
                    tr.localPosition = p + Vector3.up * bob;
                    tr.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                    tr.localScale = Vector3.one * (fish ? FishScale * Mathf.Max(0.001f, alpha) : AnimalScale * Mathf.Max(0.001f, fade));
                }
                if (fish && owner.fxMat != null)
                {
                    if (splash == null)
                    {
                        splash = new ShapingMeshes.FxMesh("Shaping_Splash");
                        splashR = ShapingMeshes.FxRenderer(root, "Splash", splash, owner.fxMat);
                    }
                    splashR.gameObject.SetActive(h.splash > 0.01f);
                    if (h.splash > 0.01f)
                    {
                        float grow = 1f - h.splash;
                        splash.Clear();
                        var at = new Vector3(entry.footprint.entryX, Land.WaterY + 0.004f, entry.footprint.entryZ);
                        splash.Ring(at, 0.05f + 0.2f * grow, 0.09f + 0.24f * grow, 24, new Color(1f, 1f, 1f, 0.9f), new Color(1f, 1f, 1f, 0f));
                        splash.Apply();
                        Tint(splashR, h.splash);
                    }
                }
            }
        }
    }
}
