using AquariumCiv.Audio;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
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
    /// <item><b>Mess.</b> <see cref="MessProps"/> draws the heaps and weed patches; a stroke that starts on one is a toss: a fast
    /// flick (release speed and a minimum travel) throws it out through <see cref="SimCore.RemoveTrash"/> or
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

        enum Stroke { None, Ignored, Wipe, Grab }

        BoxView box;
        MessProps props;
        CareSound sound;
        BoxCamera cam;
        GameHud hud;
        PeopleView people;

        // glass
        Transform boundRoot;
        GlassMask[] masks = new GlassMask[0];
        Texture2D[] textures = new Texture2D[0];
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
        int strokes;

        // sun and cheer
        Light sun;
        float sunBase = 1f, sunScale = 1f;
        float prevGrime = -1f, prevMess = -1f;
        readonly List<Transform> sparkles = new();
        readonly List<(Vector3 v, float life)> sparkleState = new();
        Mesh sparkleMesh;

        // ---------------- test and debug read-outs ----------------
        public GlassMask[] Masks => masks;
        public MessProps Props => props;
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
            foreach (var s in sparkles) if (s != null) Destroy(s.gameObject);
            sparkles.Clear(); sparkleState.Clear();
            if (sparkleMesh != null) Destroy(sparkleMesh);
        }

        void OnCleanMode(bool on)
        {
            lastTouchTime = Time.unscaledTime;
            spotlessSince = -1f;
            if (!on) { Cancel(); return; }
            var sim = Sim;
            hud?.Toast(sim != null && sim.MessLevel <= 0.0005f
                ? "Nothing to clean right now."
                : "Drag across the glass to wipe it. Flick junk and weeds out.", 5f);
        }

        // ---------------- the camera gate ----------------
        /// <summary>A press is ours in clean mode when it starts on the glass or on mess that is within reach.</summary>
        public bool ClaimsPress(Vector2 screenPos) => Classify(screenPos, out _) != Stroke.Ignored;

        Stroke Classify(Vector2 pos, out MessProps.Piece piece)
        {
            piece = null;
            if (!CleanMode || masks.Length == 0 || UiInput.Over(pos)) return Stroke.Ignored;
            var c = Cam;
            if (c == null) return Stroke.Ignored;
            piece = props.Pick(c, pos, pickRadius * Screen.height);
            if (piece != null && !BehindDirtyGlass(c, piece)) return Stroke.Grab;
            piece = null;
            return GlassHit(c.ScreenPointToRay(pos), out _, out _, out _) ? Stroke.Wipe : Stroke.Ignored;
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
                masks[i] = new GlassMask(size, texelsPerUnit, i);
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
                t.gameObject.layer = GlassLayer;
                if (!t.TryGetComponent(out cols[i])) cols[i] = t.gameObject.AddComponent<BoxCollider>();
                masks[i].ResetTo(sim.State.grime);
            }
            synced = sim.State.grime;
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

        /// <summary>Is there wipeable glass under this screen position (for tests and hints)?</summary>
        public bool GlassAt(Vector2 screenPos) => Cam != null && masks.Length > 0 && GlassHit(Cam.ScreenPointToRay(screenPos), out _, out _, out _);

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

        /// <summary>World units per screen height at a point (what a share of the screen height is in the box).</summary>
        float WorldPerScreen(Vector3 at)
        {
            var c = Cam;
            if (c == null) return 1f;
            float dist = Vector3.Dot(at - c.transform.position, c.transform.forward);
            return 2f * Mathf.Max(dist, 0.01f) * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>Is the glass between the camera and this piece still dirty (a fingertip patch where the sight line crosses it)?</summary>
        bool BehindDirtyGlass(Camera c, MessProps.Piece piece)
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
                if (stroke == Stroke.Wipe) EndWipe(sim);
                else if (stroke == Stroke.Grab) EndGrab(sim, lastPos);
                stroke = Stroke.None; pressSeen = false;
                sound.SetRub(0f, 0f);
                return;
            }
            lastTouchTime = Time.unscaledTime;
            if (!pressSeen)
            {
                pressSeen = true;
                stroke = n == 1 ? Begin(a) : Stroke.Ignored;
                lastPos = a;
                return;
            }
            if (n >= 2 && stroke != Stroke.Ignored) { Cancel(); stroke = Stroke.Ignored; return; } // two fingers are the camera's
            if (stroke == Stroke.Wipe) ContinueWipe(sim, a, dt);
            else if (stroke == Stroke.Grab)
            {
                samples.Add((Time.unscaledTime, a));
                while (samples.Count > 2 && Time.unscaledTime - samples[0].t > 0.1f) samples.RemoveAt(0);
            }
            lastPos = a;
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
                Sfx.Play(CueLibrary.WipeStart);
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
                float radius = wipeRadius * WorldPerScreen(hit.point);
                dirtUnder = Mathf.Max(dirtUnder, masks[i].GrimeAt(uv, radius));
                double removed = hasLast[i] ? masks[i].BrushLine(lastUv[i], uv, radius, wipeStrength) : masks[i].Brush(uv, radius, wipeStrength);
                total += (float)removed;
                lastUv[i] = uv; hasLast[i] = true; hitPanes++;
                lastPoint = hit.point;
                if (masks[i].CheckCleared()) { Sfx.Play(CueLibrary.PaneClean); Burst(cols[i].bounds.center, 8, 0.5f); }
            }
            if (hitPanes == 0) { sound.SetRub(0f, 0f); return; }
            long texels = 0;
            foreach (var m in masks) texels += m.Texels;
            float share = total / Mathf.Max(1L, texels);
            strokeRemoved += share;
            if (share > 0f)
            {
                // the sim's grime follows the masks in proportion (the masks have no grime under the soil, so their mean runs lower)
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
            if (strokeRemoved > 0.0005f) { strokes++; GameClock.Tel.Cleaned("wipe", true); }
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
            GameClock.Tel.Cleaned(piece.weed ? "prune" : "flick", true);
            var c = Cam;
            var dir = (c.transform.right * releaseSpeed.x + c.transform.up * releaseSpeed.y).normalized;
            float s = WorldPerScreen(piece.Center);
            float strength = Mathf.InverseLerp(flickSpeed, flickSpeed * 4f, releaseSpeed.magnitude);
            var vel = (dir * 0.5f + Vector3.up * 0.4f) * s * Mathf.Lerp(0.6f, 1.2f, strength);
            var at = piece.t.localPosition;
            if (piece.weed)
            {
                float og = sim.State.overgrowth, cut = og - MessProps.WeedUnit < 0.03f ? og : MessProps.WeedUnit;
                sim.Prune(cut);
                people?.Planner?.ReactAt(KeeperGesture.Prune, people.Now, at.x, at.z, 1.4f);
            }
            else
            {
                sim.RemoveTrash(piece.key);
                people?.Planner?.React(KeeperGesture.FlickTrash, people.Now, Place.Land(PlaceKind.TrashHeap, piece.key));
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
            if (sim.MessLevel <= 0.0005f)
            {
                if (spotlessSince < 0f) spotlessSince = now;
                if (now - spotlessSince >= spotlessExitSeconds) hud.SetCleanMode(false);
            }
            else spotlessSince = -1f;
            if (now - lastTouchTime >= idleExitSeconds) { hud.SetCleanMode(false); hud.Toast("Clean mode off.", 2f); }
        }

        // ---------------- sunlight and cheer ----------------
        void UpdateSun(SimCore sim, float dt)
        {
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
                if (sun == null) return;
                sunBase = sun.intensity;
            }
            float target = CareMath.SunScale(sim.MessLevel);
            sunScale = Mathf.Lerp(sunScale, target, 1f - Mathf.Exp(-sunFollowRate * dt));
            if (Mathf.Abs(sunScale - target) < 0.0005f) sunScale = target;
            sun.intensity = sunBase * sunScale;
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
