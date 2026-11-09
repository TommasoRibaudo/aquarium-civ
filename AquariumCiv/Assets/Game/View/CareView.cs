using AquariumCiv.Audio;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Lighting;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Shaping;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.View
{
    /// <summary>
    /// The care layer in the slice (#37; P1 = the glass, P2 = return to mess; Gate 1 in decisions.md: mess dims the sunlight
    /// and slows the work pace). Added to the box at start-up (like the HUD), it:
    /// <list type="bullet">
    /// <item><b>Glass.</b> Gives each of the box's four panes a grime mask (<see cref="GlassMask"/>) shown by the GlassV2
    /// shader's <c>_MaskTex</c>. The sim's grime stays the source over time (it grows the masks unevenly); a wipe paints the
    /// masks and takes the same amount off the sim through <see cref="SimCore.CleanGrime"/> (so shaping charges and relics
    /// count it). A wipe cleans every pane the finger's ray crosses, so the far glass can be cleaned from the front.</item>
    /// <item><b>Clean mode</b> (design/one-handed-controls.md; the HUD's Clean button): only then does one finger wipe.
    /// This is the <see cref="ICameraGate"/> the camera asks at touch-down: a press that starts on the glass or on a piece of
    /// mess is ours, a press anywhere else stays the camera's, and two fingers are always the camera.</item>
    /// <item><b>Mess.</b> <see cref="MessProps"/> draws the weed patches (the trash heaps are gone, #313); a stroke that starts on
    /// one is a toss: a fast flick (release speed and a minimum travel) throws it out through
    /// <see cref="SimCore.Prune"/>, a slow one drops it back with a hint. Mess behind dirty glass is out of reach.</item>
    /// <item><b>Sunlight.</b> The sun's intensity follows the mess (<see cref="CareMath.SunScale"/>, at most -25 %).</item>
    /// <item><b>Cheer.</b> When the glass goes spotless (or all the mess is gone) there is a chime, sparkles and the people
    /// cheer (<see cref="LifePlanner.React"/> with <see cref="KeeperGesture.FullClean"/>).</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class CareView : MonoBehaviour, ICameraGate
    {
        public const int GlassLayer = 6; // an unnamed layer: not the AO layer (2), so the AO bake never sees the panes

        /// <summary>Set false (tests) to keep the bootstrap from adding the care layer to the scene.</summary>
        public static bool AutoCreate = true;
        public static CareView Instance { get; private set; }

        [Header("Wipe")]
        [Tooltip("Wipe radius as a share of screen height (about a fingertip and a half).")] public float wipeRadius = 0.065f;
        [Tooltip("Share of grime removed per stamp at the centre (stamps overlap along a stroke).")] public float wipeStrength = 0.5f;
        public float texelsPerUnit = 32f;
        [Header("Toss")]
        [Tooltip("Release speed (screen heights per second, over the last 0.1 s) that throws a grabbed piece out.")] public float flickSpeed = 0.6f;
        [Tooltip("Minimum travel (share of screen height), so a press-and-lift never counts.")] public float flickMinTravel = 0.03f;
        [Tooltip("Mess within this share of the screen height of the finger counts as touched (about 44 pt).")] public float pickRadius = 0.035f;
        [Tooltip("Grime on the glass in front of a piece above which it cannot be reached: wipe first.")] public float blockingGrime = 0.15f;
        [Header("Mode")]
        [Tooltip("Clean mode switches itself off after this many seconds without a touch.")] public float idleExitSeconds = 30f;
        [Tooltip("... and this long after the last mess is gone.")] public float spotlessExitSeconds = 2.2f;
        [Header("Sun")]
        public float sunFollowRate = 1.5f;

        enum Stroke { None, Ignored, Wipe, Grab, Pending }
        // #518: a wipe's press waiting out KitPress.SecondFingerGrace
        Vector2 pendingAt;
        float pendingT;

        BoxView box;
        MessProps props;
        CareSound sound;
        BoxCamera cam;
        GameHud hud;
        PeopleView people;

        // glass
        Transform boundRoot;
        GlassMask[] masks = new GlassMask[0];
        /// <summary>Each pane's grime left, 0..1 (tests: which pane a wipe can't finish).</summary>
        public string DebugPaneGrime() => string.Join(" ", System.Linq.Enumerable.Select(masks, m => m == null ? "-" : m.Mean.ToString("0.000")));
        Texture2D[] textures = new Texture2D[0];
        Texture2D noise;
        Material[] mats = new Material[0];
        BoxCollider[] cols = new BoxCollider[0];
        bool[] zAxis = new bool[0];
        byte[][] bytes = new byte[0][];
        Vector2[] lastUv = new Vector2[0];
        bool[] hasLast = new bool[0];
        SimCore boundSim;
        float synced;

        // input
        Stroke stroke;
        bool pressSeen;
        Vector2 lastPos, grabStart;
        MessProps.Piece grabbed;
        readonly List<(float t, Vector2 p)> samples = new();
        float strokeStart, lastReact = -10f, lastTouchTime, spotlessSince = -1f, glintBudget;
        float strokeRemoved;
        bool startCue;
        int strokes;

        // sun and cheer
        Light sun;
        float sunBase = 1f, sceneSun = 1f, sunScale = 1f;
        float prevGrime = -1f, prevMess = -1f;
        readonly List<Transform> sparkles = new();
        readonly List<(Vector3 v, float life)> sparkleState = new();
        Mesh sparkleMesh;

        // ---------------- test and debug read-outs ----------------
        public GlassMask[] Masks => masks;
        public MessProps Props => props;
        /// <summary>#268: the dirtiest pane's mean grime, or -1 before the panes are bound.</summary>
        public float WorstPane
        {
            get
            {
                if (masks.Length == 0) return -1f;
                float w = 0f;
                foreach (var m in masks) w = Mathf.Max(w, m.Mean);
                return w;
            }
        }
        /// <summary>Mean grime on the glass as drawn (0 clean .. 1).</summary>
        public float GlassMean => CareMath.WeightedMean(masks);
        public float SunFactor => sunScale;
        public int StrokeCount => strokes;
        public bool IsWiping => stroke == Stroke.Wipe;
        public bool IsGrabbing => stroke == Stroke.Grab;
        public bool CleanMode => hud != null && hud.CleanMode;
        /// <summary>The last cheer: "glass" or "all" and when (unscaled time), for tests.</summary>
        public string LastCheer { get; private set; } = "";
        public int CheerCount { get; private set; }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;
        Camera Cam => cam != null ? cam.GetComponent<Camera>() : Camera.main;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<CareView>() == null) b.gameObject.AddComponent<CareView>();
        }

        // ---------------- lifecycle ----------------
        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            if (!TryGetComponent(out props)) props = gameObject.AddComponent<MessProps>();
            if (!TryGetComponent(out sound)) sound = gameObject.AddComponent<CareSound>();
            Hook();
        }

        void Hook()
        {
            cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();
            cam?.AddGate(this);
            hud = GameHud.Instance;
            if (hud != null) { hud.CleanModeChanged -= OnCleanMode; hud.CleanModeChanged += OnCleanMode; }
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null; else return;
            cam?.RemoveGate(this);
            if (hud != null) hud.CleanModeChanged -= OnCleanMode;
            if (sun != null) sun.intensity = sunBase;
            Release();
        }

        void Release()
        {
            foreach (var t in textures) if (t != null) Destroy(t);
            textures = new Texture2D[0];
            masks = new GlassMask[0];
            boundRoot = null;
            if (noise != null) Destroy(noise);
            foreach (var s in sparkles) if (s != null) Destroy(s.gameObject);
            sparkles.Clear(); sparkleState.Clear();
            if (sparkleMesh != null) Destroy(sparkleMesh);
        }

        const string NothingToClean = "Nothing to clean right now.", WipeHint = "Drag across the glass to wipe it. Flick junk and weeds out.",
            WipeHintLand = "Drag across the glass to wipe it."; // a living land has no junk or weeds to flick (tester run 2)

        void OnCleanMode(bool on)
        {
            lastTouchTime = Time.unscaledTime;
            spotlessSince = -1f;
            hud?.WithdrawToast(AquariumCiv.Sim.CleanHint.Text); // Clean was tapped: the line asking for that is done, and the wipe hint needs the lane
            if (!on)
            {
                hud?.WithdrawToast(NothingToClean); // waiting or on screen: it no longer applies
                hud?.WithdrawToast(WipeHint); hud?.WithdrawToast(WipeHintLand);
                Cancel();
                return;
            }
            var sim = Sim;
            if (sim != null && sim.State.firstRun.active) return; // the ghost finger is saying this, and one message at a time
            hud?.Toast(sim != null && (sim.EcologyOn ? sim.State.grime : sim.MessLevel) <= 0.0005f ? NothingToClean : sim != null && sim.EcologyOn ? WipeHintLand : WipeHint, 5f);
        }

        // ---------------- the camera gate ----------------
        /// <summary>A press is ours in clean mode when it starts on the glass or on mess that is within reach.</summary>
        public bool ClaimsPress(Vector2 screenPos) => Classify(screenPos, out _) != Stroke.Ignored;

        /// <summary>#301 (tester run 2): in the first run's wipe step one finger always wipes. A stroke that starts off the glass (the
        /// natural edge-to-edge swipe) wipes the panes it crosses from where it reaches one, and never turns the camera. Two fingers
        /// still move the view; after the step the normal rule (a press is ours only on glass or mess) is back.</summary>
        public static bool WipeAnywhere => FirstRunView.Instance != null && FirstRunView.Instance.Step == AquariumCiv.Sim.FirstRunStep.Wipe;

        Stroke Classify(Vector2 pos, out MessProps.Piece piece)
        {
            piece = null;
            if (!CleanMode || masks.Length == 0 || UiInput.Over(pos)) return Stroke.Ignored;
            var c = Cam;
            if (c == null) return Stroke.Ignored;
            // #311: on a living land the cloth keeps to the glass; weeds, vines and saplings are the tweezers' (a flick still clears a box without the ecology)
            var sim = Sim;
            if (sim == null || !sim.EcologyOn) piece = props.Pick(c, pos, pickRadius * Screen.height);
            if (piece != null && !BehindDirtyGlass(c, piece)) return Stroke.Grab;
            piece = null;
            return GlassHit(c.ScreenPointToRay(pos), out _, out _, out _) || WipeAnywhere ? Stroke.Wipe : Stroke.Ignored;
        }

        // ---------------- frame ----------------
        void Update()
        {
            var sim = Sim;
            if (sim == null || box == null) return;
            if (hud == null || cam == null) Hook();
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (sim != boundSim) { boundSim = sim; boundRoot = null; prevGrime = prevMess = -1f; Cancel(); }
            BindPanes(sim);
            SyncMasks(sim);
            props.Sync(sim, dt);
            UpdateSun(sim, dt);
            HandleInput(sim, dt);
            UpdateCheer(sim);
            UpdateMode(sim);
            TickSparkles(dt);
            foreach (var m in masks)
                if (m.glint > 0f) m.glint = Mathf.Max(0f, m.glint - dt * 0.8f);
            for (int i = 0; i < masks.Length; i++) if (mats[i] != null) mats[i].SetFloat("_Glint", masks[i].glint);
        }

        void LateUpdate()
        {
            for (int i = 0; i < masks.Length; i++)
            {
                var m = masks[i];
                if (!m.dirty) continue;
                m.dirty = false;
                m.ToBytes(bytes[i]);
                textures[i].SetPixelData(bytes[i], 0);
                textures[i].Apply(false);
            }
        }

        // ---------------- the glass ----------------
        void BindPanes(SimCore sim)
        {
            var root = box.Root;
            if (root == null || root == boundRoot) return;
            boundRoot = root;
            foreach (var t in textures) if (t != null) Destroy(t);
            if (noise == null)
            {
                noise = new Texture2D(GlassNoise.Size, GlassNoise.Size, TextureFormat.RGBA32, false, true)
                    { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "GrimeDetail", hideFlags = HideFlags.DontSave };
                noise.SetPixelData(GlassNoise.Bytes(), 0);
                noise.Apply(false, true);
            }
            var list = new List<Transform>();
            foreach (Transform t in root) if (t.name.StartsWith("Glass") && t.GetComponent<MeshRenderer>() != null) list.Add(t);
            int n = list.Count;
            masks = new GlassMask[n]; textures = new Texture2D[n]; mats = new Material[n]; cols = new BoxCollider[n];
            zAxis = new bool[n]; bytes = new byte[n][]; lastUv = new Vector2[n]; hasLast = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var t = list[i];
                var s = t.localScale;
                zAxis[i] = s.x < s.z;
                var size = new Vector2(zAxis[i] ? s.z : s.x, s.y);
                int paneSeed = sim.State.seed * 31 + i * 7 + 1; // every pane of every box its own grime (#121)
                masks[i] = new GlassMask(size, texelsPerUnit, paneSeed);
                var pos = t.localPosition;
                bool zz = zAxis[i];
                float w = size.x;
                masks[i].NoGrimeBelow(u => zz
                    ? box.GroundHeight(Mathf.Clamp(pos.x, -Land.HX, Land.HX), Mathf.Clamp((u - 0.5f) * w, -Land.HZ, Land.HZ))
                    : box.GroundHeight(Mathf.Clamp((u - 0.5f) * w, -Land.HX, Land.HX), Mathf.Clamp(pos.z, -Land.HZ, Land.HZ)));
                textures[i] = new Texture2D(masks[i].width, masks[i].height, TextureFormat.R8, false, true)
                    { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "GrimeMask_" + t.name, hideFlags = HideFlags.DontSave };
                bytes[i] = new byte[masks[i].Texels];
                mats[i] = t.GetComponent<MeshRenderer>().sharedMaterial;
                mats[i].SetTexture("_MaskTex", textures[i]);
                mats[i].SetTexture("_NoiseTex", noise);
                mats[i].SetVector("_NoiseOffset", GlassPaneNoise.Offset(paneSeed));
                mats[i].SetFloat("_NoiseRot", GlassPaneNoise.Rotation(paneSeed));
                mats[i].SetFloat("_DirtyAlpha", CareMath.PatchAlpha);
                mats[i].SetColor("_GrimeColor", CareMath.GrimeColor);
                t.gameObject.layer = GlassLayer;
                if (!t.TryGetComponent(out cols[i])) cols[i] = t.gameObject.AddComponent<BoxCollider>();
                masks[i].ResetTo(sim.State.grime);
            }
            synced = sim.State.grime;
        }

        /// <summary>The glass box in box space: half width, half depth, and the top of the panes. False before the panes are bound.</summary>
        public bool Walls(out float halfW, out float halfD, out float top)
        {
            halfW = Land.HX; halfD = Land.HZ; top = 1.6f;
            if (cols.Length == 0) return false;
            for (int i = 0; i < cols.Length; i++)
            {
                var t = cols[i].transform;
                if (zAxis[i]) halfW = Mathf.Abs(t.localPosition.x); else halfD = Mathf.Abs(t.localPosition.z);
                top = t.localPosition.y + t.localScale.y * 0.5f;
            }
            return true;
        }

        /// <summary>A pane's transform (tests).</summary>
        public Transform PaneOf(int i) => cols[i].transform;

        /// <summary>#147: grime the sim counts for a crumb's smudge. Small: the mark itself is drawn on the one pane.</summary>
        public const float CrumbGrime = 0.01f;
        /// <summary>The last smudge: its pane (-1 = none yet), its uv and its radius, world units (tests).</summary>
        public int LastSmudgePane { get; private set; } = -1;
        public Vector2 LastSmudgeUv { get; private set; }
        public float LastSmudgeRadius { get; private set; }

        /// <summary>#147: the Sky Crumb hit a wall at <paramref name="boxPoint"/> (box space): a smudge on that pane, real grime the Keeper wipes off like any other.</summary>
        public void Smudge(CrumbWall wall, Vector3 boxPoint, float radiusUv, float strength)
        {
            var sim = Sim;
            for (int i = 0; i < cols.Length; i++)
            {
                var p = cols[i].transform.localPosition;
                bool it = wall switch { CrumbWall.Left => zAxis[i] && p.x < 0f, CrumbWall.Right => zAxis[i] && p.x > 0f, _ => !zAxis[i] && p.z > 0f };
                if (!it) continue;
                var uv = Uv(i, box.transform.TransformPoint(boxPoint));
                uv = new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
                float r = radiusUv * masks[i].size.x;
                masks[i].Smudge(uv, r, strength);
                LastSmudgePane = i; LastSmudgeUv = uv; LastSmudgeRadius = r;
                if (sim != null) { sim.AddMess(CrumbGrime, 0f); synced = sim.State.grime; } // no Grow: this grime is drawn here
                return;
            }
        }

        /// <summary>The sim's grime is the source over time: it grows the masks unevenly, or fades them when something else
        /// (debug, clean all) took grime away. Wipes go the other way (the masks lead, the sim follows).</summary>
        void SyncMasks(SimCore sim)
        {
            if (masks.Length == 0) return;
            float g = sim.State.grime;
            if (g > synced + 0.002f) { foreach (var m in masks) m.Grow(g - synced); synced = g; }
            else if (g < synced - 0.002f || (g <= 0f && synced > 0f))
            {
                float k = g <= 0.001f ? 0f : g / Mathf.Max(synced, 1e-4f);
                foreach (var m in masks) m.FadeTo(m.Mean * k);
                synced = g;
            }
        }

        /// <summary>Can the finger reach this spot of the pane? The panes facing the camera always; the far ones (seen through
        /// the open top and the near glass) only above the ground at their foot: the soil hides the rest.</summary>
        bool Visible(int i, Vector3 world)
        {
            var c = Cam;
            var t = cols[i].transform;
            var outward = zAxis[i] ? new Vector3(Mathf.Sign(t.localPosition.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(t.localPosition.z));
            if (Vector3.Dot(box.transform.TransformDirection(outward), c.transform.position - cols[i].bounds.center) > 0f) return true;
            var l = box.transform.InverseTransformPoint(world);
            return l.y >= box.GroundHeight(Mathf.Clamp(l.x, -Land.HX, Land.HX), Mathf.Clamp(l.z, -Land.HZ, Land.HZ)) - 0.005f;
        }

        // ---------------- #268: what a finger can reach ----------------
        /// <summary>Per pane, the texels a wipe from this camera can reach: a stroke anywhere on the screen where there's glass and no UI,
        /// stamped as <see cref="ContinueWipe"/> stamps (every pane the ray crosses, the far ones only above the ground).</summary>
        bool[][] Reach()
        {
            var c = Cam;
            var reach = new bool[masks.Length][];
            for (int i = 0; i < masks.Length; i++) reach[i] = new bool[masks[i].Texels];
            if (c == null) return reach;
            float W = Screen.width, H = Screen.height;
            float step = Mathf.Max(6f, wipeRadius * H * 0.25f);
            for (float y = step * 0.5f; y < H; y += step)
            for (float x = step * 0.5f; x < W; x += step)
            {
                var p = new Vector2(x, y);
                if (UiInput.Over(p)) continue;
                var ray = c.ScreenPointToRay(p);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] == null || !cols[i].Raycast(ray, out var hit, 200f) || !Visible(i, hit.point)) continue;
                    masks[i].MarkReach(Uv(i, hit.point), StampRadius(ray, i, hit.point), reach[i]);
                }
            }
            return reach;
        }

        /// <summary>#268: per pane, the share of its grime no finger can reach from this camera (tests, debug). "-" before the panes are bound.</summary>
        public float[] UnreachableShare()
        {
            var reach = Reach();
            var r = new float[masks.Length];
            for (int i = 0; i < masks.Length; i++)
            {
                double all = masks[i].Mean * masks[i].LiveTexels;
                r[i] = all <= 1e-6 ? 0f : (float)(masks[i].GrimeOutside(reach[i]) / all);
            }
            return r;
        }

        /// <summary>#268: the first run's wipe asks for the glass to be clear, so only the glass a finger can reach from here stays fogged:
        /// the rest (off the screen, behind the soil's edge, under the UI) is cleared and the sim's grime follows, as a wipe would.
        /// Returns the share of the glass's grime that went.</summary>
        public float ClearUnreachable()
        {
            var sim = Sim;
            if (sim == null || masks.Length == 0) return 0f;
            float before = CareMath.WeightedMean(masks);
            if (before <= 0f) return 0f;
            var reach = Reach();
            for (int i = 0; i < masks.Length; i++) masks[i].ClearOutside(reach[i]);
            float after = CareMath.WeightedMean(masks);
            if (after < before && sim.State.grime > 0f)
            {
                sim.State.grime *= after / before; // not CleanGrime: nobody wiped, so no Keeper's action or shaping charge counts it
                synced = sim.State.grime;
            }
            return 1f - after / before;
        }

        /// <summary>Is there wipeable glass under this screen position (for tests and hints)?</summary>
        public bool GlassAt(Vector2 screenPos) => Cam != null && masks.Length > 0 && GlassHit(Cam.ScreenPointToRay(screenPos), out _, out _, out _);

        /// <summary>#333: the world point on the glass under this screen position (where a knock lands), if there is glass there.</summary>
        public bool GlassPointAt(Vector2 screenPos, out Vector3 world)
        {
            world = default;
            return Cam != null && masks.Length > 0 && GlassHit(Cam.ScreenPointToRay(screenPos), out _, out _, out world);
        }

        bool GlassHit(Ray ray, out int pane, out Vector2 uv, out Vector3 point)
        {
            pane = -1; uv = default; point = default;
            float best = float.MaxValue;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || !cols[i].Raycast(ray, out var hit, 200f) || hit.distance >= best || !Visible(i, hit.point)) continue;
                best = hit.distance; pane = i; point = hit.point; uv = Uv(i, hit.point);
            }
            return pane >= 0;
        }

        Vector2 Uv(int i, Vector3 world)
        {
            var os = cols[i].transform.InverseTransformPoint(world);
            return new Vector2((zAxis[i] ? os.z : os.x) + 0.5f, os.y + 0.5f);
        }

        /// <summary>#268: the most a slanted pane's stamp grows. A pane seen at a slant (a side pane from the default camera, the far
        /// right one in playtest #260) is foreshortened: a fingertip covers a longer stretch of it than of a pane facing the camera, so
        /// a stamp the size of a face-on fingertip left stripes between a player's strokes that no stroke finished.</summary>
        public const float MaxSlantGrowth = 3f;

        /// <summary>The wipe's stamp on pane <paramref name="i"/> where <paramref name="ray"/> meets it, world units: the finger's
        /// footprint on the glass, grown by the pane's slant to the view.</summary>
        float StampRadius(Ray ray, int i, Vector3 point)
        {
            var normal = box.transform.TransformDirection(zAxis[i] ? Vector3.right : Vector3.forward);
            float facing = Mathf.Abs(Vector3.Dot(ray.direction.normalized, normal.normalized));
            return wipeRadius * WorldPerScreen(point) * Mathf.Min(MaxSlantGrowth, 1f / Mathf.Max(facing, 1f / MaxSlantGrowth));
        }

        /// <summary>World units per screen height at a point (what a share of the screen height is in the box).</summary>
        float WorldPerScreen(Vector3 at)
        {
            var c = Cam;
            if (c == null) return 1f;
            float dist = Vector3.Dot(at - c.transform.position, c.transform.forward);
            return 2f * Mathf.Max(dist, 0.01f) * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>Is the glass between the camera and this piece still dirty (a fingertip patch where the sight line crosses it)?</summary>
        public bool BehindDirtyGlass(Camera c, MessProps.Piece piece)
        {
            var from = c.transform.position;
            var to = piece.Center;
            var ray = new Ray(from, to - from);
            float len = (to - from).magnitude;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || !cols[i].Raycast(ray, out var hit, len) || !Visible(i, hit.point)) continue;
                float r = wipeRadius * 0.5f * WorldPerScreen(hit.point);
                if (masks[i].GrimeAt(Uv(i, hit.point), r) > blockingGrime) return true;
            }
            return false;
        }

        // ---------------- input ----------------
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

        void HandleInput(SimCore sim, float dt)
        {
            int n = ReadPointers(out var a);
            if (n == 0)
            {
                if (tweezing && hud != null && hud.Hand == GameHud.KitTool.Tweezers && !tweezeTried) PluckAt(sim, lastPos); // #518: a tap quicker than the grace plucks on its lift
                tweezing = false;
                if (stroke == Stroke.Pending) // #518: a dab quicker than the grace wipes on its lift
                {
                    var end = lastPos;
                    lastPos = pendingAt;
                    if (Begin(pendingAt) == Stroke.Wipe) { ContinueWipe(sim, end, dt); EndWipe(sim); }
                }
                else if (stroke == Stroke.Wipe) EndWipe(sim);
                else if (stroke == Stroke.Grab) EndGrab(sim, lastPos);
                stroke = Stroke.None; pressSeen = false;
                sound.SetRub(0f, 0f);
                return;
            }
            lastTouchTime = Time.unscaledTime;
            if (hud != null && hud.Hand == GameHud.KitTool.Tweezers) { TweezersHand(sim, n, a); lastPos = a; return; }
            if (!pressSeen)
            {
                pressSeen = true;
                // #518: a wipe waits a moment for a second finger (a pan or pinch in clean mode never rubs the glass); a grab starts at once
                // (it only holds the piece, and a second finger drops it back)
                if (n != 1) stroke = Stroke.Ignored;
                else if (Classify(a, out _) == Stroke.Wipe) { stroke = Stroke.Pending; pendingAt = a; pendingT = Time.unscaledTime; }
                else stroke = Begin(a);
                if (n == 1) PointToClean(sim, a);
                if (n == 1 && CleanMode && sim.EcologyOn && Cam != null && UnderFinger(Cam, a) != null) TellClothIsForGlass();
                lastPos = a;
                return;
            }
            if (n >= 2 && stroke != Stroke.Ignored) { Cancel(); stroke = Stroke.Ignored; return; } // two fingers are the camera's
            if (stroke == Stroke.Pending)
            {
                if (Time.unscaledTime - pendingT < KitPress.SecondFingerGrace) { lastPos = a; return; }
                lastPos = pendingAt;
                stroke = Begin(pendingAt); // the stroke starts where the finger landed and catches up to it
            }
            if (stroke == Stroke.Wipe) ContinueWipe(sim, a, dt);
            else if (stroke == Stroke.Grab)
            {
                samples.Add((Time.unscaledTime, a));
                while (samples.Count > 2 && Time.unscaledTime - samples[0].t > 0.1f) samples.RemoveAt(0);
            }
            lastPos = a;
        }

        // ---------------- #311: the tweezers ----------------
        public const string ClothIsForGlass = "The cloth is for the glass. Take the tweezers from your Kit for weeds.";
        public const string TweezersHintOnMess = "Take the tweezers from your Kit to pluck weeds.";
        float clothHintAt = -99f;

        void TellClothIsForGlass()
        {
            if (hud == null || Time.unscaledTime - clothHintAt < 12f) return;
            clothHintAt = Time.unscaledTime;
            hud.Toast(ClothIsForGlass, 3f, through: true);
        }

        /// <summary>The land point under a screen point (box-local), through the misting view's ray (it knows the land).</summary>
        bool LandPoint(Vector2 screen, out Vector3 at)
        {
            at = default;
            var m = EcoMistView.Instance;
            return m != null && m.GroundPoint(screen, out at);
        }

        /// <summary>How far from the finger (box units) a sapling still counts as touched.</summary>
        public const float SaplingReach = 0.16f;
        /// <summary>Plucks that happened this session (tests and the debug panel).</summary>
        public int Plucks { get; private set; }
        /// <summary>What the last pluck took: "weed", "treeweed", "vine", "sapling" or "" (tests).</summary>
        public string LastPluck { get; private set; } = "";
        bool tweezing, tweezeTried;
        float lastPluckAt = -9f, tweezeT;

        void TweezersHand(SimCore sim, int n, Vector2 pos)
        {
            if (!pressSeen)
            {
                pressSeen = true;
                tweezing = n == 1 && !UiInput.Over(pos) && sim.EcologyOn && hud.IsVisible && !hud.SheetOpen && !hud.CameraMode;
                lastPluckAt = -9f; // the throttle paces a drag; a fresh tap always gets its pluck
                tweezeT = Time.unscaledTime; tweezeTried = false;
            }
            if (n >= 2) tweezing = false; // two fingers are the camera's, until every finger is up
            if (!tweezing) return;
            // #518: wait a moment for a second finger (a pan or pinch never plucks); a tap quicker than that plucks on its lift (HandleInput)
            if (Time.unscaledTime - tweezeT < KitPress.SecondFingerGrace) return;
            tweezeTried = true;
            if (Time.unscaledTime - lastPluckAt < 0.06f) return;
            if (PluckAt(sim, pos)) lastPluckAt = Time.unscaledTime;
        }

        /// <summary>Plucks whatever overgrows under a screen point: a weed patch, a tuft round the tree or a cell of vine (what is drawn there), else the wild
        /// sapling nearest to the land point there. Never the Keeper's tree, a grown tree or the heart of a wood (the sim decides). True if something came out.</summary>
        public bool PluckAt(SimCore sim, Vector2 screenPos)
        {
            var c = Cam;
            if (c == null || sim == null || !sim.EcologyOn) return false;
            var piece = props.PickUnder(c, screenPos, TapReachPixels * 1.2f);
            // #375 (playtest #347): the tweezers reach through the glass, grimy or not. BehindDirtyGlass is the cloth's rule (Classify); here it dropped
            // every weed, tuft and vine seen through a fogged patch, so a pluck only worked on clean glass.
            bool onLand = LandPoint(screenPos, out var at);
            if (piece != null && onLand)
            {
                // A wild sapling nearer the finger than the drawn piece wins (a weed patch's reach must not hide the sapling you tapped).
                int near = sim.EcoNearestSapling(at.x, at.z, SaplingReach);
                if (near >= 0)
                {
                    var sw = box.transform.TransformPoint(new Vector3(Sim_X(near), box.GroundHeight(Sim_X(near), Sim_Z(near)), Sim_Z(near)));
                    var ss = c.WorldToScreenPoint(sw);
                    var ps = c.WorldToScreenPoint(piece.Center);
                    if (Vector2.Distance(screenPos, ss) < Vector2.Distance(screenPos, ps)) piece = null;
                }
            }
            if (piece != null) return PluckPiece(sim, piece);
            if (onLand)
            {
                int cell = sim.EcoPluckSapling(at.x, at.z, SaplingReach);
                if (cell >= 0)
                {
                    var local = new Vector3(Sim_X(cell), 0f, Sim_Z(cell));
                    local.y = box.GroundHeight(local.x, local.z);
                    props.PopSapling(local);
                    box.Refresh();
                    Plucked("sapling", local, 0.9f);
                    return true;
                }
            }
            return false;
        }

        static float Sim_X(int cell) => AquariumCiv.Sim.Settle.SiteMap.CXc(cell);
        static float Sim_Z(int cell) => AquariumCiv.Sim.Settle.SiteMap.CZc(cell);

        /// <summary>Plucks one drawn piece of overgrowth (the tweezers, or a test).</summary>
        public bool PluckPiece(SimCore sim, MessProps.Piece piece)
        {
            if (piece == null || piece.leaving) return false;
            var at = piece.t.localPosition;
            string what;
            switch (piece.kind)
            {
                case MessProps.Kind.Vine:
                    if (!sim.EcoPluckVine(piece.key)) return false;
                    what = "vine";
                    break;
                case MessProps.Kind.TreeWeed:
                    if (!sim.EcoPluckTreeWeed()) return false;
                    what = "treeweed";
                    break;
                default:
                {
                    float og = sim.State.overgrowth, cut = og - MessProps.WeedUnit < 0.03f ? og : MessProps.WeedUnit;
                    sim.Prune(cut);
                    what = "weed";
                    break;
                }
            }
            props.Pluck(piece);
            Plucked(what, at, what == "vine" ? 0.6f : 1f);
            return true;
        }

        void Plucked(string what, Vector3 local, float pitchSpread)
        {
            Plucks++; strokes++;
            LastPluck = what;
            GameClock.Tel.Cleaned("prune", true);
            TouchHint.Cleaned();
            Sfx.Play(CueLibrary.Pluck, 0f, 1f + (what == "vine" ? -0.15f : 0f));
            Burst(transform.TransformPoint(local) + Vector3.up * 0.06f, 3, 0.08f, 0.1f);
            people?.Planner?.ReactAt(KeeperGesture.Prune, people.Now, local.x, local.z, 1.4f);
        }

        /// <summary>#160: a touch on mess outside clean mode doesn't clean, and says why: the Clean button pulses and, now and then, a short
        /// hint shows. The drag goes on to move the camera.</summary>
        void PointToClean(SimCore sim, Vector2 pos)
        {
            pressAnswered = false;
            if (hud == null || CleanMode || hud.Hand != GameHud.KitTool.None ||!hud.IsVisible || hud.CameraMode || hud.SheetOpen || sim.State.firstRun.active || UiInput.Over(pos)) return;
            var shaping = ShapingView.Instance;
            if (shaping != null && shaping.ShapeMode) return;
            var c = Cam;
            if (c == null) return;
            var cards = PersonCardView.Instance;
            if (cards != null && (cards.AnyOpen || cards.BuildingOpen || !cards.PickDoll(pos).IsNone)) return; // that tap closes the card, or opens this person's
            var r = TouchHint.Touch(false, UnderFinger(c, pos) != null, Time.unscaledTime);
            Say(r);
            pressAnswered = r.pulse || r.hint;
        }

        void Say(AquariumCiv.Sim.CleanHintResult r)
        {
            if (r.pulse) hud.PulseClean();
            if (r.hint) hud.Toast(Sim != null && Sim.EcologyOn ? TweezersHintOnMess : AquariumCiv.Sim.CleanHint.Text, 3f, through: true); // it answers a touch: a waiting prompt doesn't hold it back
        }

        bool pressAnswered;

        /// <summary>Tester run 8: a tap that <see cref="MessSaysSo"/> kept is answered, whatever the gaps say: the tap would otherwise do nothing at all.</summary>
        public void AnswerMessTap()
        {
            if (hud == null) return;
            Say(TouchHint.Tapped(pressAnswered, Time.unscaledTime));
            pressAnswered = false;
        }

        /// <summary>#160: a tap here is about the mess, so it opens nothing: there's mess under the finger. Tester run 9: that goes on after the first clean, when it
        /// used to let the tap through to whatever was near, mostly nothing: every tap on mess answers, always.</summary>
        public bool MessSaysSo(Vector2 pos)
        {
            var c = Cam;
            return c != null && !CleanMode && UnderFinger(c, pos) != null;
        }

        /// <summary>Tester run 8: how near a tap outside clean mode has to be to a piece of mess to be on it, in pixels. <see cref="pickRadius"/> is for cleaning, where
        /// a generous reach helps: for a tap it was bigger than a house at the default zoom.</summary>
        public float TapReachPixels => TapReach * Screen.width / AquariumCiv.GameUI.GameUiPanel.ReferenceWidth;
        const float TapReach = 30f;

        MessProps.Piece UnderFinger(Camera c, Vector2 pos) => props.PickUnder(c, pos, TapReachPixels);

        /// <summary>Where the mess is on screen now (tests).</summary>
        public void MessOnScreen(System.Collections.Generic.List<Vector2> into)
        {
            var c = Cam;
            if (c != null) props.OnScreen(c, into);
        }

        /// <summary>#160: what's been said about Clean this session.</summary>
        public AquariumCiv.Sim.CleanHint TouchHint { get; } = new();

        /// <summary>Debug: forget the Clean hint, so the next touch on mess shows it.</summary>
        public static string DebugCleanHint()
        {
            if (Instance == null) return "no CareView";
            Instance.TouchHint.Reset();
            return "Clean hint forgotten: touch a weed";
        }

        Stroke Begin(Vector2 pos)
        {
            var kind = Classify(pos, out var piece);
            if (kind == Stroke.Grab)
            {
                grabbed = piece;
                props.SetHeld(piece, true);
                Sfx.Play(CueLibrary.MessGrab);
                grabStart = pos;
                samples.Clear();
                samples.Add((Time.unscaledTime, pos));
            }
            else if (kind == Stroke.Wipe)
            {
                strokeRemoved = 0f;
                strokeStart = Time.unscaledTime;
                startCue = true; // played when the stroke first touches a pane (#301: it may start off the glass)
                System.Array.Clear(hasLast, 0, hasLast.Length);
                ContinueWipe(Sim, pos, 0.016f, true);
            }
            return kind;
        }

        void ContinueWipe(SimCore sim, Vector2 pos, float dt, bool first = false)
        {
            var c = Cam;
            if (c == null) return;
            float H = Mathf.Max(1f, Screen.height);
            float speed = (pos - lastPos).magnitude / H / Mathf.Max(dt, 1f / 120f);
            if (!first && (pos - lastPos).sqrMagnitude < 0.25f) { sound.SetRub(0f, 0f); return; }
            var ray = c.ScreenPointToRay(pos);
            float total = 0f, dirtUnder = 0f;
            int hitPanes = 0;
            Vector3 lastPoint = default;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || !cols[i].Raycast(ray, out var hit, 200f) || !Visible(i, hit.point)) { hasLast[i] = false; continue; }
                var uv = Uv(i, hit.point);
                float radius = StampRadius(ray, i, hit.point);
                dirtUnder = Mathf.Max(dirtUnder, masks[i].GrimeAt(uv, radius));
                double removed = hasLast[i] ? masks[i].BrushLine(lastUv[i], uv, radius, wipeStrength) : masks[i].Brush(uv, radius, wipeStrength);
                total += (float)removed;
                lastUv[i] = uv; hasLast[i] = true; hitPanes++;
                lastPoint = hit.point;
                if (masks[i].CheckCleared()) { Sfx.Play(CueLibrary.PaneClean); Burst(cols[i].bounds.center, 8, 0.5f); }
            }
            if (hitPanes == 0) { sound.SetRub(0f, 0f); return; }
            if (startCue) { startCue = false; Sfx.Play(CueLibrary.WipeStart); }
            long texels = 0;
            foreach (var m in masks) texels += m.LiveTexels;
            float share = total / Mathf.Max(1L, texels);
            strokeRemoved += share;
            if (share > 0f)
            {
                // the sim's grime follows the masks in proportion
                float after = CareMath.WeightedMean(masks);
                sim.CleanGrime(sim.State.grime - sim.State.grime * after / (after + share));
                synced = sim.State.grime;
            }
            if (CareMath.WeightedMean(masks) < CareMath.SpotlessMean && (CareMath.WeightedMean(masks) > 0f || sim.State.grime > 0f))
            {
                foreach (var m in masks) m.Clear();
                if (sim.State.grime > 0f) sim.CleanGrime(sim.State.grime);
                synced = sim.State.grime;
            }
            sound.SetRub(Mathf.Clamp01(speed / 1.2f), dirtUnder);
            glintBudget = Mathf.Min(glintBudget + share * 3000f, 3f);
            int g = Mathf.FloorToInt(glintBudget);
            if (g > 0) { glintBudget -= g; Burst(lastPoint, g, wipeRadius * WorldPerScreen(lastPoint) * 0.5f, 0.12f); }
            if (Time.unscaledTime - lastReact > 2f && share > 0f)
            {
                lastReact = Time.unscaledTime;
                people?.Planner?.React(KeeperGesture.Wipe, people.Now);
            }
        }

        void EndWipe(SimCore sim)
        {
            System.Array.Clear(hasLast, 0, hasLast.Length);
            if (strokeRemoved > 0.0005f) { strokes++; GameClock.Tel.Cleaned("wipe", true); TouchHint.Cleaned(); }
            strokeRemoved = 0f;
        }

        /// <summary>End of a stroke that started on a piece of mess: a fast flick throws it out, a slow drag drops it back.</summary>
        void EndGrab(SimCore sim, Vector2 pos)
        {
            var piece = grabbed;
            grabbed = null;
            if (piece == null || piece.leaving) return;
            props.SetHeld(piece, false);
            samples.Add((Time.unscaledTime, pos));
            var (t0, p0) = samples[0];
            float dt = Mathf.Max(Time.unscaledTime - t0, 1f / 120f);
            float H = Mathf.Max(1f, Screen.height);
            var v = (pos - p0) / H / dt;                       // release velocity, screen heights per second
            float travel = Vector2.Distance(pos, grabStart) / H;
            if (v.magnitude >= flickSpeed && travel >= flickMinTravel) { Toss(sim, piece, v); return; }
            props.Wobble(piece);
            Sfx.Play(CueLibrary.MessDrop);
            hud?.Toast(travel < flickMinTravel ? "Flick it out." : "Flick it faster to throw it out.", 2.5f);
        }

        /// <summary>Throws a piece out (the flick, or a test): the sim loses it, the people react, the piece flies off.</summary>
        public void Toss(SimCore sim, MessProps.Piece piece, Vector2 releaseSpeed)
        {
            GameClock.Tel.Cleaned("prune", true);
            TouchHint.Cleaned();
            var c = Cam;
            var dir = (c.transform.right * releaseSpeed.x + c.transform.up * releaseSpeed.y).normalized;
            float s = WorldPerScreen(piece.Center);
            float strength = Mathf.InverseLerp(flickSpeed, flickSpeed * 4f, releaseSpeed.magnitude);
            var vel = (dir * 0.5f + Vector3.up * 0.4f) * s * Mathf.Lerp(0.6f, 1.2f, strength);
            var at = piece.t.localPosition;
            {
                float og = sim.State.overgrowth, cut = og - MessProps.WeedUnit < 0.03f ? og : MessProps.WeedUnit;
                sim.Prune(cut);
                people?.Planner?.ReactAt(KeeperGesture.Prune, people.Now, at.x, at.z, 1.4f);
            }
            props.Throw(piece, vel);
            // #96: the swish of the throw, a snip for a weed; a harder flick swishes higher
            Sfx.Play(CueLibrary.MessFlick, strength * 3f);
            if (piece.weed) Sfx.Play(CueLibrary.Prune);
            Burst(piece.Center, 4, 0.12f, 0.1f);
            strokes++;
        }

        void Cancel()
        {
            if (grabbed != null) { props.SetHeld(grabbed, false); grabbed = null; }
            if (stroke == Stroke.Wipe) System.Array.Clear(hasLast, 0, hasLast.Length);
            stroke = Stroke.None;
            sound?.SetRub(0f, 0f);
        }

        // ---------------- mode ----------------
        void UpdateMode(SimCore sim)
        {
            if (hud == null || !hud.CleanMode) return;
            float now = Time.unscaledTime;
            if (stroke != Stroke.None && stroke != Stroke.Ignored) { spotlessSince = -1f; return; }
            if ((sim.EcologyOn ? sim.State.grime : sim.MessLevel) <= 0.0005f) // #311: on a living land the cloth only has the glass to do
            {
                if (spotlessSince < 0f) spotlessSince = now;
                if (now - spotlessSince >= spotlessExitSeconds) hud.SetCleanMode(false);
            }
            else spotlessSince = -1f;
            if (now - lastTouchTime >= idleExitSeconds) hud.SetCleanMode(false); // no line: the button changes back (question 11)
        }

        // ---------------- sunlight and cheer ----------------
        void UpdateSun(SimCore sim, float dt)
        {
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
                if (sun == null) return;
                sunBase = sceneSun = sun.intensity;
            }
            float target = CareMath.SunScale(sim.MessLevel);
            sunScale = Mathf.Lerp(sunScale, target, 1f - Mathf.Exp(-sunFollowRate * dt));
            if (Mathf.Abs(sunScale - target) < 0.0005f) sunScale = target;
            // The day sets the sun, the mess takes its share of it; at night the sun is too weak for that to show, so the ambient goes down too.
            var day = DayLightDriver.Instance;
            sunBase = DayLightDriver.Driving ? day.Current.SunIntensity : sceneSun;
            sun.intensity = sunBase * sunScale;
            if (day != null) day.MessAmbient = DayLight.MessAmbientScale(sunScale);
        }

        void UpdateCheer(SimCore sim)
        {
            if (people == null) people = FindAnyObjectByType<PeopleView>();
            float g = sim.State.grime, mess = sim.MessLevel;
            if (prevGrime < 0f) { prevGrime = g; prevMess = mess; return; }
            bool all = prevMess > 0.0005f && mess <= 0.0005f;
            bool glass = prevGrime > 0f && g <= 0f;
            prevGrime = g; prevMess = mess;
            if (!all && !glass) return;
            Cheer(sim, all);
        }

        /// <summary>Chime, sparkles over the box, and the people cheer.</summary>
        public void Cheer(SimCore sim, bool everything)
        {
            LastCheer = everything ? "all" : "glass";
            CheerCount++;
            Sfx.Play(CueLibrary.FullClean, 0f, everything ? 1f : 0.75f);
            Sfx.Play(everything ? CueLibrary.Cheer : CueLibrary.CheerSmall, 0f, 1f, 0.35f); // the people cheer (they also turn and wave)
            int n = everything ? 36 : 22;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(Random.Range(-Land.HX, Land.HX) * 0.85f, Land.GlassTop * Random.Range(0.35f, 0.9f), Random.Range(-Land.HZ, Land.HZ) * 0.85f);
                Burst(transform.TransformPoint(p), 1, 0.05f, 0.25f);
            }
            people?.Planner?.React(KeeperGesture.FullClean, people.Now);
            if (Sim == null || !Sim.State.firstRun.active) // in the first run the next step's hint comes in here (question 27)
                hud?.Toast(everything ? "All tidy. Look at that sky!" : "The glass is sparkling.", 3.5f);
        }

        // ---------------- sparkles ----------------
        /// <summary>A few drifting sparkles (tiny gold diamonds in the box's own material).</summary>
        public void Burst(Vector3 world, int count, float radius, float spread = 0.2f)
        {
            if (box == null) return;
            if (sparkleMesh == null)
            {
                var mb = new MeshBuilder(3) { jitter = 0f };
                mb.Ico(Vector3.zero, 0.012f, new Vector3(1f, 1.5f, 1f), Palette.FireCore, 0f);
                sparkleMesh = mb.ToMesh("CareSparkle");
            }
            for (int i = 0; i < count; i++)
            {
                if (sparkles.Count >= 64) return;
                var go = new GameObject("Sparkle") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = sparkleMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = box.SolidMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.position = world + Random.insideUnitSphere * radius;
                go.transform.localScale = Vector3.one * Random.Range(0.8f, 1.5f);
                sparkles.Add(go.transform);
                sparkleState.Add((Random.insideUnitSphere * spread + Vector3.up * 0.18f, Random.Range(0.6f, 1.2f)));
            }
        }

        void TickSparkles(float dt)
        {
            for (int i = sparkles.Count - 1; i >= 0; i--)
            {
                var (v, life) = sparkleState[i];
                life -= dt;
                if (life <= 0f || sparkles[i] == null)
                {
                    if (sparkles[i] != null) Destroy(sparkles[i].gameObject);
                    sparkles.RemoveAt(i); sparkleState.RemoveAt(i);
                    continue;
                }
                var t = sparkles[i];
                t.position += v * dt;
                t.Rotate(0f, 240f * dt, 0f, Space.World);
                t.localScale = Vector3.one * Mathf.Min(t.localScale.x, Mathf.Clamp01(life * 2f) * 1.4f);
                sparkleState[i] = (v, life);
            }
        }
    }
}
