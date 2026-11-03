using System.Collections.Generic;
using System.Globalization;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using AquariumCiv.View.ShowMe;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// The choice scene in the box (#224; research 14 §2.3): a moment, not a menu. Show me on a dilemma card
    /// (<see cref="CardStackView.DilemmaShowMeHook"/>) stages the people's ask in the world (<see cref="LifePlanner.StageChoice"/>,
    /// <see cref="ChoiceScenes"/>): the two candidates either side of the fire, two stakes at two sites, two offerings on the stone, or the
    /// chief at the fire and the travellers at the edge. The camera frames both; each choice gets a soft halo and a small tag with its
    /// words; a touch on one answers (<see cref="DilemmaPath.Box"/>). Then the chosen one cheers, the other shrugs or sulks, people
    /// near enough gather, and everyone goes back to their day. Left without a touch it stays open: they wait
    /// <see cref="ChoiceScenes.DriftSeconds"/>, drift back to work, and stage again on the next Show me.
    /// Everything it makes hangs under the box and goes with the scene. Debug: <see cref="DebugStatus"/>, <see cref="DebugStage"/>,
    /// <see cref="DebugAnswer"/>, <see cref="DebugLeave"/>.
    /// </summary>
    public class ChoiceSceneView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static ChoiceSceneView Instance { get; private set; }

        public enum Phase { Idle, Waiting, Drifting, Reacting }

        /// <summary>Panel units (1080 wide) round each choice's figure that a touch still takes: a 44 pt target and a little more.</summary>
        public const float PickRadius = 72f;
        /// <summary>The halo (box units): clear inside, the soft bright line, clear outside; how high over the ground; its peak alpha.</summary>
        const float HaloInner = 0.085f, HaloMid = 0.15f, HaloOuter = 0.23f, HaloLift = 0.02f, HaloPeak = 0.7f, FadeSeconds = 0.35f;
        /// <summary>The framing: the two sides span about this share of the screen's width, the view is never narrower than this (box
        /// units), and the middle sits this high on the screen (the bar is at the bottom).</summary>
        const float SpanShare = 0.62f, MinWidth = 1.7f, AnchorY = 0.5f;
        /// <summary>The halo grows with the view so it reads when the camera is out (zoom over this, up to that many times).</summary>
        const float HaloZoomFrom = 0.3f, HaloGrowMost = 2.4f;
        /// <summary>Real seconds before the staging runs out that it is staged again while the Keeper still looks.</summary>
        const float ExtendBefore = 12f;
        /// <summary>A traveller's walk (box units a second) and height for the tag and the touch.</summary>
        const float TravellerWalk = 0.22f, FigureHeight = 0.24f;

        public Phase State { get; private set; }
        /// <summary>The scene staged now (null when idle).</summary>
        public ChoiceStage Stage { get; private set; }
        /// <summary>The option the Keeper touched in the last scene (-1 = none yet).</summary>
        public int Chosen { get; private set; } = -1;
        /// <summary>The bar stays up over it: waiting on a touch, or the reaction to one.</summary>
        public bool Holding => State == Phase.Waiting || State == Phase.Reacting;
        /// <summary>The halos are up and a touch answers.</summary>
        public bool Answerable => State == Phase.Waiting && Watched();
        /// <summary>How bright each halo is now (0..1; tests and the tester).</summary>
        public float HaloAlpha(int side) => side >= 0 && side < 2 ? haloA[side] : 0f;
        /// <summary>Tests and the debug panel: watched without the card (the card's bar normally says so).</summary>
        public static bool DebugWatch;

        BoxView box;
        Transform root;
        readonly GameObject[] halos = new GameObject[2];
        readonly Transform[] props = new Transform[2];
        readonly float[] haloA = new float[2];
        readonly List<Object> made = new();
        readonly List<Traveller> travellers = new();
        Material haloMat;
        Mesh haloMesh;
        MaterialPropertyBlock block;
        GameObject uiGo;
        VisualElement uiRoot;
        readonly GlassTag[] tags = new GlassTag[2];
        float phaseT, clock;
        bool watchedSeen, said;
        string question = "";
        // The camera: where it was before the first look, where the flight landed, and whether the player has taken it since.
        (Vector3 target, float yaw, float pitch, float zoom) saved;
        bool ownCamera, landed, taken;
        Vector3 landedTarget;
        float landedZoom;

        sealed class Traveller
        {
            public PegDollView view;
            public Vector2 pos, home;
            public float phase, stride, yaw;
        }

        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.ActiveSim : null;
        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<ChoiceSceneView>() == null) b.gameObject.AddComponent<ChoiceSceneView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            CardStackView.DilemmaShowMeHook = Offer;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            if (CardStackView.DilemmaShowMeHook == (System.Func<DilemmaView, bool>)Offer) CardStackView.DilemmaShowMeHook = null;
            End();
            Kill(haloMesh); Kill(haloMat);
            haloMesh = null; haloMat = null;
            if (uiGo != null) Kill(uiGo);
            uiGo = null; uiRoot = null;
            tags[0] = tags[1] = null;
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        // ================================================================== the hook: Show me on a dilemma card

        /// <summary>Show me on an open dilemma card: stage its choice scene and fly to it. False (the card falls back to the normal
        /// Show me) when it can't be staged here.</summary>
        public bool Offer(DilemmaView v)
        {
            if (v?.dilemma == null || v.dilemma.state != DilemmaState.Open || box == null) return false;
            var pv = PeopleView.Find();
            var planner = pv != null ? pv.Planner : null;
            var cam = Cam;
            var c = cam != null ? cam.GetComponent<Camera>() : null;
            if (planner == null || c == null) return false;
            var fwd = box.transform.InverseTransformDirection(c.transform.forward);
            var right = box.transform.InverseTransformDirection(c.transform.right);
            var st = planner.StageChoice(v, pv.Now, right.x, right.z, -fwd.x, -fwd.z, ChoiceScenes.WaitSeconds, Land.HX, Land.HZ);
            if (st == null) return false;
            ClearWorld();
            pv.ClearPins();
            Stage = st;
            Chosen = -1;
            question = v.question ?? "";
            said = false;
            Build(st, v);
            if (!ownCamera) { saved = (cam.target, cam.yaw, cam.pitch, cam.zoom); ownCamera = true; }
            landed = taken = false;
            Frame(cam, st);
            State = Phase.Waiting;
            phaseT = 0f;
            watchedSeen = false;
            Sfx.Play(CueLibrary.ShowMe);
            GameClock.DilemmaShown(v.dilemma); // #225: the box scene counts as shown (once per dilemma; the card usually came first)
            Debug.Log($"[Choice] {st.defId} ({st.kind}) staged: {Describe(st)}");
            return true;
        }

        // ================================================================== the frame

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            clock += dt;
            StepCamera();
            if (State == Phase.Idle) return;
            phaseT += dt;
            var pv = PeopleView.Find();
            var planner = pv != null ? pv.Planner : null;
            var sim = Sim;
            if (planner == null || sim == null || Stage == null) { End(); return; }
            var d = sim.FindDilemma(Stage.dilemmaId);
            bool open = d != null && d.state == DilemmaState.Open;
            bool watched = Watched();
            switch (State)
            {
                case Phase.Waiting:
                    if (!open) { End(); return; } // answered on the card, or the people decided
                    if (watched) { watchedSeen = true; if (!said && phaseT > 0.6f) SayAsk(); }
                    else if (watchedSeen || phaseT > 1.5f) { State = Phase.Drifting; phaseT = 0f; Unsay(); break; } // left without a touch
                    if (!planner.ChoiceRunning(pv.Now) || LifeClock.RealSecondsBetween(pv.Now.hour, Stage.end) < ExtendBefore) Restage(pv, planner, d);
                    break;
                case Phase.Drifting:
                    if (!open || phaseT >= ChoiceScenes.DriftSeconds || !planner.ChoiceRunning(pv.Now)) { End(); return; } // back to work
                    break;
                case Phase.Reacting:
                    if (phaseT >= ChoiceScenes.ReactSeconds || planner.Choice == null) { End(); return; }
                    break;
            }
            foreach (var (who, x, z) in Stage.Pins()) pv.Pin(who, new Vector2(x, z));
        }

        void LateUpdate()
        {
            if (State == Phase.Idle || Stage == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || cam == null) return;
            DrawTravellers(dt);
            DrawHalos(pv, dt);
            DrawTags(pv, cam);
        }

        bool Watched()
        {
            if (DebugWatch) return true;
            var cards = CardStackView.Instance;
            return cards != null && cards.ShowingChoice;
        }

        void Restage(PeopleView pv, LifePlanner planner, Dilemma d)
        {
            var v = Sim?.ViewOfDilemma(d);
            if (v == null) return;
            var st = planner.StageChoice(v, pv.Now, Stage.rx, Stage.rz, Stage.tx, Stage.tz, ChoiceScenes.WaitSeconds, Land.HX, Land.HZ);
            if (st == null) return;
            pv.ClearPins();
            Stage = st; // the same places and props: only the time runs on (and maybe a fresh worker by a stake)
        }

        /// <summary>The scene is over (answered and reacted, left long enough, the dilemma closed elsewhere): everyone walks back into
        /// their day and every prop, halo and traveller goes. The camera stays with the card, as for any Show me.</summary>
        public void End()
        {
            var pv = PeopleView.Find();
            if (State != Phase.Idle) Unsay();
            if (State != Phase.Idle && pv != null && pv.Planner != null) pv.Planner.EndChoice(pv.Now); // cut here: they walk back from where they stand
            if (pv != null) pv.ClearPins();
            ClearWorld();
            State = Phase.Idle;
            Stage = null;
        }

        // ================================================================== a touch

        /// <summary>A short tap reached the world (<see cref="GlassVoice"/>, before the person card): true if it answered the people.</summary>
        public bool WorldTap(Vector2 screenPos)
        {
            if (!Answerable || Stage == null) return false;
            int side = SideAt(screenPos, out _);
            return side >= 0 && Answer(Stage.sides[side].option);
        }

        /// <summary>The side whose figure is under a screen point (pixels, y up): the nearest within <see cref="PickRadius"/>, or -1.</summary>
        public int SideAt(Vector2 screenPos, out float distance)
        {
            distance = float.MaxValue;
            var cam = Camera.main;
            if (Stage == null || cam == null) return -1;
            float r = PickRadius * Screen.width / GameUiPanel.ReferenceWidth;
            int best = -1;
            for (int i = 0; i < 2; i++)
                foreach (var (feet, top) in Figures(i))
                {
                    var a = cam.WorldToScreenPoint(feet);
                    var b = cam.WorldToScreenPoint(top);
                    if (a.z <= 0f || b.z <= 0f) continue;
                    float dd = ToSegment(screenPos, a, b);
                    if (dd < distance) { distance = dd; best = dd <= r ? i : best; }
                }
            return distance <= r ? best : -1;
        }

        /// <summary>Where on screen to touch a side (pixels, y up): the middle of its first figure. False when it's off screen.</summary>
        public bool TryScreenOf(int side, out Vector2 screen)
        {
            screen = default;
            var cam = Camera.main;
            if (Stage == null || cam == null || side < 0 || side > 1) return false;
            foreach (var (feet, top) in Figures(side))
            {
                var s = cam.WorldToScreenPoint((feet + top) * 0.5f);
                if (s.z <= 0f) continue;
                screen = new Vector2(s.x, s.y);
                return s.x >= 0f && s.x <= Screen.width && s.y >= 0f && s.y <= Screen.height;
            }
            return false;
        }

        static float ToSegment(Vector2 p, Vector3 a3, Vector3 b3)
        {
            Vector2 a = a3, b = b3;
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>The Keeper touched <paramref name="option"/>: the answer goes in (final, the Box path), the people react, the bar says it.</summary>
        public bool Answer(int option)
        {
            var sim = Sim;
            var pv = PeopleView.Find();
            if (Stage == null || sim == null || State == Phase.Reacting) return false;
            var cards = CardStackView.Instance;
            bool ok = cards != null && cards.Model != null && cards.Model.AnswerDilemmaInBox(Stage.dilemmaId, option);
            if (!ok && sim.AnswerDilemma(Stage.dilemmaId, option, DilemmaPath.Box)) { ok = true; GameClock.DilemmaAnswered(sim.FindDilemma(Stage.dilemmaId)); }
            if (!ok) return false;
            Chosen = option;
            Unsay();
            State = Phase.Reacting;
            phaseT = 0f;
            if (pv != null && pv.Planner != null) pv.Planner.ReactChoice(option, pv.Now);
            foreach (var t in travellers) t.home = t.pos;
            Sfx.Play(CueLibrary.Cheer);
            if (cards != null) cards.ChoiceAnswered();
            Debug.Log($"[Choice] {Stage.defId}: the Keeper chose option {option} in the box");
            return true;
        }

        // ================================================================== building the scene

        void Build(ChoiceStage st, DilemmaView v)
        {
            root = new GameObject("ChoiceScene") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(transform, false);
            var land = box.land;
            var right = new Vector2(st.rx, st.rz);
            var mid = Middle(st);
            for (int i = 0; i < 2; i++)
            {
                var s = st.sides[i];
                // A prop or a stranger never stands in the water: walked back towards the scene's middle until dry.
                var at = Dry(land, new Vector2(s.x, s.z), Middle(st));
                s.x = at.x; s.z = at.y;
                if (s.Prop != ChoiceProp.None)
                {
                    var mesh = s.Prop == ChoiceProp.Stake ? ChoiceProps.Stake(ChoiceProps.ClothFor(s.option))
                        : ChoiceProps.OfferingMesh(ChoiceProps.OfferingFor(st.defId, s.option));
                    made.Add(mesh);
                    var go = new GameObject(s.Prop == ChoiceProp.Stake ? "ChoiceStake" + i : "ChoiceOffering" + i) { hideFlags = HideFlags.DontSave };
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = box.SolidMaterial;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    // The prop's +x (the stake's cloth) points outward, away from the other side, along the screen.
                    float outward = Vector2.Dot(at - mid, right) >= 0f ? 1f : -1f;
                    var o = right * outward;
                    float yaw = Mathf.Atan2(-o.y, o.x) * Mathf.Rad2Deg;
                    go.transform.SetLocalPositionAndRotation(land.OnGround(at), Quaternion.Euler(0f, yaw, 0f));
                    props[i] = go.transform;
                }
                for (int k = 0; k < s.travellers.Count; k++)
                {
                    var p = Dry(land, new Vector2(s.travellers[k].x, s.travellers[k].z), Middle(st));
                    var look = PegDoll.PickLook(9001 + st.dilemmaId * 7 + k, Sim != null && Sim.State.era == Era.Camp, false, 0);
                    look.accent = Palette.Accents[(k + 4) % Palette.Accents.Length];
                    var view = PegDollView.Create(root, box.SolidMaterial, look, false, 4000 + st.dilemmaId * 5 + k);
                    view.name = "ChoiceTraveller" + k;
                    view.SetProp(PropKind.Bundle, PropSlot.Back); // full packs
                    travellers.Add(new Traveller { view = view, pos = p, home = p, phase = k * 0.37f, yaw = Mathf.Atan2(st.tx, st.tz) * Mathf.Rad2Deg + 180f });
                }
                halos[i] = Halo(i);
            }
            if (uiRoot == null) BuildUi();
            for (int i = 0; i < 2; i++)
            {
                var o = v.options[i];
                tags[i].Word = o.label;
                if (o.personId < 0) tags[i].Mark = st.kind switch { DilemmaKind.Tradition => "scroll", DilemmaKind.Strangers => i == 0 ? "person" : "herd", _ => "pin" };
                tags[i].style.display = DisplayStyle.None;
                haloA[i] = 0f;
            }
        }

        /// <summary>The ask, said in the box by the one who asks (over their head, or on the caption plate when it's long or they're
        /// out of sight): the people ask, the bar only says what to do. It stays up while they wait.</summary>
        void SayAsk()
        {
            said = true;
            var sm = ShowMeDirector.Instance;
            if (sm == null || string.IsNullOrEmpty(question) || Stage == null) return;
            var who = Stage.asker;
            if (who.IsNone && Stage.sides[0].rule.stand == ChoiceStand.Asker && Stage.sides[0].people.Count > 0) who = Stage.sides[0].people[0].who;
            if (who.IsNone)
            {
                var d = Sim?.FindDilemma(Stage.dilemmaId);
                if (d != null && d.askerId >= 0) who = PersonRef.Notable(d.askerId);
            }
            sm.SayLine(question, who, ChoiceScenes.WaitSeconds);
        }

        void Unsay()
        {
            if (!said) return;
            said = false;
            ShowMeDirector.Instance?.ClearLine();
        }

        static Vector2 Middle(ChoiceStage st) => new((st.sides[0].x + st.sides[1].x) * 0.5f, (st.sides[0].z + st.sides[1].z) * 0.5f);

        static Vector2 Dry(Land land, Vector2 p, Vector2 toward)
        {
            for (int i = 0; i < 12 && land.IsWater(p.x, p.y); i++) p = Vector2.MoveTowards(p, toward, 0.08f);
            return p;
        }

        GameObject Halo(int i)
        {
            if (haloMesh == null) haloMesh = SpeakerRing.RingMesh("ChoiceHalo", HaloInner, HaloMid, HaloOuter, new Color(1f, 0.93f, 0.72f), HaloPeak);
            if (haloMat == null)
            {
                // Like the speaker's ring: nothing cuts it, so a choice behind a tree still shows where to touch.
                haloMat = new Material(ShapingView.LoadFx()) { name = "ChoiceHalo", hideFlags = HideFlags.DontSave, renderQueue = 3004 };
                haloMat.SetFloat("_ZTest", (float)CompareFunction.Always);
            }
            block ??= new MaterialPropertyBlock();
            var go = new GameObject("ChoiceHalo" + i) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = haloMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = haloMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go;
        }

        void BuildUi()
        {
            uiGo = new GameObject("ChoiceTags") { hideFlags = HideFlags.DontSave };
            uiGo.transform.SetParent(transform, false);
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 11; // under the cards' bar
            uiRoot = new VisualElement { name = "choice-root", pickingMode = PickingMode.Ignore };
            uiRoot.AddToClassList("hud-fill");
            doc.rootVisualElement.Add(uiRoot);
            for (int i = 0; i < 2; i++)
            {
                tags[i] = new GlassTag("") { name = "choice-tag-" + i, InWorld = true };
                tags[i].style.display = DisplayStyle.None;
                uiRoot.Add(tags[i]);
            }
        }

        void ClearWorld()
        {
            if (root != null) Kill(root.gameObject);
            root = null;
            foreach (var o in made) Kill(o);
            made.Clear();
            travellers.Clear();
            for (int i = 0; i < 2; i++) { halos[i] = null; props[i] = null; haloA[i] = 0f; if (tags[i] != null) tags[i].style.display = DisplayStyle.None; }
        }

        // ================================================================== drawing

        /// <summary>The figures standing for a side (world feet, top): its people, its prop, its travellers.</summary>
        IEnumerable<(Vector3 feet, Vector3 top)> Figures(int side)
        {
            var s = Stage.sides[side];
            var pv = PeopleView.Find();
            if (pv != null)
                foreach (var (who, x, z) in s.people)
                {
                    if (pv.TryGetDollFeet(who, out var f) && pv.TryGetDollTop(who, out var t)) yield return (f, t);
                    else { var g = World(x, z); yield return (g, g + Vector3.up * FigureHeight); }
                }
            if (props[side] != null)
            {
                var b = props[side].position;
                yield return (b, b + Vector3.up * ChoiceProps.TopOf(s.Prop));
            }
            if (s.travellers.Count > 0)
                foreach (var t in travellers)
                {
                    if (t.view == null) continue;
                    var f = t.view.transform.position;
                    yield return (f, f + Vector3.up * FigureHeight);
                }
        }

        Vector3 World(float x, float z) => box.transform.TransformPoint(box.land.OnGround(x, z));

        void DrawHalos(PeopleView pv, float dt)
        {
            bool up = State == Phase.Waiting && Watched();
            for (int i = 0; i < 2; i++)
            {
                var go = halos[i];
                if (go == null) continue;
                float want = up ? 1f : State == Phase.Reacting && i == Chosen && phaseT < 2.2f ? 1f : 0f;
                haloA[i] = Mathf.MoveTowards(haloA[i], want, dt / FadeSeconds);
                if (haloA[i] <= 0f) { if (go.activeSelf) go.SetActive(false); continue; }
                if (!go.activeSelf) go.SetActive(true);
                // Under the middle of the side's figures, a little wider for a group.
                Vector3 sum = Vector3.zero;
                int n = 0;
                foreach (var (feet, _) in Figures(i)) { sum += feet; n++; }
                if (n == 0) { go.SetActive(false); continue; }
                var at = sum / n;
                float group = Stage.sides[i].travellers.Count > 1 ? 1.5f : Stage.sides[i].people.Count > 1 ? 1.3f : 1f;
                var bc = Cam;
                if (bc != null) group *= Mathf.Clamp(bc.zoom / HaloZoomFrom, 1f, HaloGrowMost);
                float pulse = State == Phase.Reacting ? 1f + 0.35f * Mathf.Clamp01(phaseT / 1.2f) : 1f + 0.05f * Mathf.Sin(clock * 2.4f + i * 1.7f);
                go.transform.position = at + Vector3.up * HaloLift;
                go.transform.localScale = Vector3.one * group * pulse;
                float breathe = State == Phase.Reacting ? 1f : 0.82f + 0.18f * Mathf.Sin(clock * 2.4f + i * 1.7f);
                block.SetColor("_Color", new Color(1f, 1f, 1f, haloA[i] * breathe));
                go.GetComponent<MeshRenderer>().SetPropertyBlock(block);
            }
        }

        void DrawTags(PeopleView pv, Camera cam)
        {
            bool on = State == Phase.Waiting && Watched();
            for (int i = 0; i < 2; i++)
            {
                var tag = tags[i];
                if (tag == null) continue;
                bool shown = on && haloA[i] > 0.3f && Place(tag, cam, i);
                tag.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                tag.style.opacity = haloA[i];
            }
        }

        /// <summary>Hangs a tag over the side's highest figure, kept on screen.</summary>
        bool Place(GlassTag tag, Camera cam, int side)
        {
            if (uiRoot?.panel == null) return false;
            Vector3? best = null;
            float bestY = float.MinValue;
            foreach (var (_, top) in Figures(side))
            {
                var s = cam.WorldToScreenPoint(top);
                if (s.z > 0f && s.y > bestY) { bestY = s.y; best = s; }
            }
            if (!best.HasValue) return false;
            var sp = best.Value;
            if (sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) return false;
            var p = RuntimePanelUtils.ScreenToPanel(uiRoot.panel, new Vector2(sp.x, Screen.height - sp.y));
            float half = tag.resolvedStyle.width * 0.5f, W = uiRoot.layout.width;
            if (float.IsNaN(half)) half = 0f;
            tag.style.left = W > 0f ? Mathf.Clamp(p.x, half + 16f, Mathf.Max(half + 16f, W - half - 16f)) : p.x;
            tag.style.top = p.y - 10f;
            return true;
        }

        void DrawTravellers(float dt)
        {
            if (travellers.Count == 0) return;
            var land = box.land;
            var fire = new Vector2(Stage.sides[0].x, Stage.sides[0].z);
            float k = AquariumCiv.Lighting.CameraReach.DollScale(Cam != null ? Cam.zoom : 1f, PeopleView.DollScaleAmount);
            float faceYaw = Mathf.Atan2(Stage.tx, Stage.tz) * Mathf.Rad2Deg + 180f; // looking out at the glass (a doll's front is its -z)
            foreach (var t in travellers)
            {
                var style = DollStyle.LookUp;
                Vector2 goal = t.pos;
                if (State == Phase.Reacting)
                {
                    if (Chosen == 0)
                    {
                        // A place by the fire: they walk in to it, and cheer once there.
                        var to = fire + (t.home - fire).normalized * 0.3f;
                        goal = to;
                        style = (t.pos - to).sqrMagnitude > 0.0004f ? DollStyle.Walk : DollStyle.Cheer;
                    }
                    else
                    {
                        // Full packs: a wave, then on their way: to the home they will make in the box (#231: the far woods, the far
                        // shore, where ApartGroupsView draws them after), else out to the wall.
                        style = phaseT < 1.8f ? DollStyle.Wave : DollStyle.Walk;
                        if (phaseT >= 1.8f)
                        {
                            var away = (t.home - fire).normalized;
                            goal = Stage.hasHome && Chosen == Stage.homeOption ? new Vector2(Stage.homeX, Stage.homeZ) + (t.home - new Vector2(Stage.sides[1].x, Stage.sides[1].z)) : t.home + away * 2f;
                        }
                    }
                }
                else if (Mathf.Repeat(clock + t.phase * 9f, 6f) > 4.6f) style = DollStyle.WaveSmall; // the idle loop: now and then a small wave
                var before = t.pos;
                t.pos = Vector2.MoveTowards(t.pos, goal, TravellerWalk * dt);
                t.pos.x = Mathf.Clamp(t.pos.x, -Land.HX + 0.1f, Land.HX - 0.1f);
                t.pos.y = Mathf.Clamp(t.pos.y, -Land.HZ + 0.1f, Land.HZ - 0.1f);
                if (land.IsWater(t.pos.x, t.pos.y)) t.pos = before;
                var vel = (t.pos - before) / Mathf.Max(dt, 1e-4f);
                float speed = vel.magnitude;
                if (style == DollStyle.Walk && speed < 0.01f) style = DollStyle.LookUp;
                float want = style == DollStyle.Walk ? Mathf.Atan2(vel.x, vel.y) * Mathf.Rad2Deg + 180f : faceYaw;
                t.yaw = Mathf.MoveTowardsAngle(t.yaw, want, 360f * dt);
                t.stride = Mathf.Repeat(t.stride + DollMotion.Cadence(style, speed) * Mathf.PI * dt, Mathf.PI * 2f);
                var pose = DollMotion.Evaluate(style, clock + t.phase, 1f, 0, 1, PropSlot.Back, true, speed, 0f, 0f, 0, t.stride);
                var v = t.view;
                float s = v.scale;
                var yawQ = Quaternion.Euler(0f, t.yaw + pose.yaw, 0f);
                var tilt = Quaternion.Euler(-pose.lean, 0f, pose.roll);
                v.transform.SetLocalPositionAndRotation(land.OnGround(t.pos) + yawQ * (pose.offset * s), yawQ * tilt);
                v.transform.localScale = new Vector3(k, pose.squash * k, k);
                v.ApplyArms(pose);
            }
        }

        // ================================================================== the camera

        /// <summary>Flies to frame both choices: from above the rim (a close look at the usual pitch goes through the front pane's haze),
        /// close enough that the two sides span about half the screen, the middle a little above the bar.</summary>
        void Frame(BoxCamera cam, ChoiceStage st)
        {
            var c = cam.GetComponent<Camera>();
            var r = new Vector2(st.rx, st.rz);
            float lo = float.MaxValue, hi = float.MinValue;
            Vector2 sum = Vector2.zero;
            int n = 0;
            void Add(float x, float z)
            {
                float a = x * r.x + z * r.y;
                lo = Mathf.Min(lo, a); hi = Mathf.Max(hi, a);
                sum += new Vector2(x, z); n++;
            }
            foreach (var s in st.sides) { Add(s.x, s.z); foreach (var (_, x, z) in s.people) Add(x, z); foreach (var (x, z) in s.travellers) Add(x, z); }
            if (!st.asker.IsNone) Add(st.askerX, st.askerZ);
            var mid = sum / Mathf.Max(1, n);
            float span = hi - lo;
            float aspect = c != null ? c.aspect : 9f / 16f, vfov = c != null ? c.fieldOfView : 22f;
            float tanH = Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad) * aspect;
            float width = Mathf.Max(MinWidth, span / SpanShare);
            float zoom = Mathf.Clamp(width / (2f * cam.FitDistance() * tanH), cam.minZoom, 1f);
            float pitch = Mathf.Max(cam.pitch, ShowMeDirector.ShowPitch);
            var world = World(mid.x, mid.y);
            var focus = ShowMeDirector.AboveCard(cam, world, zoom, pitch, AnchorY);
            float dist = new Vector2(world.x - cam.target.x, world.z - cam.target.z).magnitude;
            cam.EaseTo(focus, cam.yaw, pitch, zoom, ShowMeCore.FlySeconds(dist));
        }

        /// <summary>The card closed: the camera eases back to where the first look took it from, unless the player has moved it since.</summary>
        void StepCamera()
        {
            if (!ownCamera) return;
            var cam = Cam;
            if (cam == null) { ownCamera = false; return; }
            if (!landed && !cam.IsFlying) { landed = true; landedTarget = cam.target; landedZoom = cam.zoom; }
            if (landed && !taken && !cam.IsFlying && ((cam.target - landedTarget).magnitude > 0.05f || Mathf.Abs(cam.zoom - landedZoom) > 0.02f)) taken = true;
            var cards = CardStackView.Instance;
            bool cardOpen = cards != null && cards.Model != null && cards.Model.Mode == CardStackMode.Open;
            if (cardOpen || DebugWatch) return;
            ownCamera = false;
            if (!taken) cam.EaseTo(saved.target, saved.yaw, saved.pitch, saved.zoom, ShowMeDirector.ReturnSeconds);
        }

        // ================================================================== debug (the debug panel, #225; script eval)

        static string Describe(ChoiceStage st)
        {
            var ci = CultureInfo.InvariantCulture;
            string Side(ChoiceSide s) => string.Format(ci, "#{0} {1} at ({2:0.00},{3:0.00}) people {4} travellers {5}", s.option, s.Prop, s.x, s.z, s.people.Count, s.travellers.Count);
            return Side(st.sides[0]) + " | " + Side(st.sides[1]) + (st.asker.IsNone ? "" : " | asker " + st.asker);
        }

        /// <summary>Debug: what the choice scene is doing.</summary>
        public static string DebugStatus()
        {
            var v = Instance;
            if (v == null) return "no ChoiceSceneView";
            if (v.Stage == null) return $"choice: {v.State}, last answer {v.Chosen}";
            return string.Format(CultureInfo.InvariantCulture, "choice: {0} {1:0.0}s, {2}: {3}; halos {4:0.00}/{5:0.00}",
                v.State, v.phaseT, v.Stage.defId, Describe(v.Stage), v.haloA[0], v.haloA[1]);
        }

        /// <summary>Debug: stage the open dilemma's choice scene without the card (watched until <see cref="DebugLeave"/>).</summary>
        public static string DebugStage()
        {
            var v = Instance;
            var sim = Sim;
            if (v == null || sim == null) return "no ChoiceSceneView or sim";
            var d = sim.OpenDilemmaView;
            if (d == null) return "no open dilemma (raise one first)";
            DebugWatch = true;
            return v.Offer(d) ? DebugStatus() : "could not stage it";
        }

        /// <summary>Debug: touch option <paramref name="option"/> in the box.</summary>
        public static string DebugAnswer(int option)
        {
            var v = Instance;
            return v == null ? "no ChoiceSceneView" : v.Answer(option) ? DebugStatus() : "no answer (nothing waiting, or closed)";
        }

        /// <summary>Debug: look away (the people drift back after <see cref="ChoiceScenes.DriftSeconds"/>).</summary>
        public static string DebugLeave()
        {
            DebugWatch = false;
            return DebugStatus();
        }
    }
}
