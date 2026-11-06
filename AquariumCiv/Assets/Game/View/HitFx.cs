using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Lighting;
using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>What a stroke does where it lands: a puff (and a water ring), a leaf or two, and a shake on the target.</summary>
    public struct HitAnswer
    {
        public bool hasPuff;
        public PuffKind puff;
        /// <summary>Add a flat ring on the water (droplets over water).</summary>
        public bool ring;
        public ShakeTarget shake;
        /// <summary>Leaves to drop from the target (0 = none; the winter takes them away anyway).</summary>
        public int leaves;
        /// <summary>The sparks are the fire's (float up), not a knapped stone's.</summary>
        public bool fire;
        /// <summary>#370: gold sparks (the new chief's pop); <see cref="big"/>: a bigger burst (a log thrown on the fire flares).</summary>
        public bool gold;
        public float big;
        public bool IsNone => !hasPuff && shake == ShakeTarget.None && leaves == 0;
    }

    /// <summary>
    /// #357 (animation-specs §0.4, §0.5, B1): every stroke that hits something makes a puff where it hits and the thing it hit moves.
    /// <list type="bullet">
    /// <item><b>Puffs</b>: one pooled system (<see cref="PuffPool"/>: at most 24 puffs and 160 bits), ONE mesh, ONE material, ONE draw call, rebuilt in
    /// world space each frame like <see cref="ChatterView"/> from outlines made once (<see cref="HitFxShapes"/>). Solid palette colours, no alpha: the
    /// bits shrink to nothing over the last 40 % of their life. Only puffs inside the camera view are made.</item>
    /// <item><b>Shakes</b>: bushes and trees are baked into the land's chunk meshes, so their sway is in the vertex shader: <see cref="ShakeSet"/> keeps up
    /// to 16 shake centres and uploads them to the Stylized shader's globals (<c>_AC_Shake*</c>); a vertex with a sway weight moves about its base,
    /// falling off with the distance from the centre. Separate objects (a frame, a stone) get <see cref="Jolt"/>, a transform drop.</item>
    /// <item><b>The call</b>: <see cref="Puff"/>(kind, worldPos, dir, scale) and <see cref="Shake"/>(target, worldPos, away) for any later animation (water,
    /// berries, hunt, fishing, meals, felling). <see cref="PeopleView.WorkHit"/> is mapped by <see cref="Resolve"/> (style + place → kind and target).</item>
    /// </list>
    /// The pure maths and the table of numbers are in HitFxMath.cs (AnimBench can draw puffs from it).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HitFx : MonoBehaviour
    {
        public static bool AutoCreate = true;
        /// <summary>The debug panel's "Puffs on/off": off = no puffs and no shakes from <see cref="PeopleView.WorkHit"/> (direct calls too).</summary>
        public static bool Enabled = true;
        // #370 (batch 2): the festival's log toss lands on the frame its staged pose set the hit; only that stroke flares the fire
        // (a kid pointing at the fire during play, or any other Point, makes nothing: #359).
        static PersonRef stagedToss;
        static int stagedTossFrame = -1;
        /// <summary>#370: <see cref="StagedMoments"/> marks the doll whose Toss contact is on this frame (called from its pose hook, just before the hit edge).</summary>
        public static void MarkStagedToss(PersonRef who) { stagedToss = who; stagedTossFrame = Time.frameCount; }
        /// <summary>The smallest a bit draws, in pixels across (§0.1: below ~3 px nothing reads at the full-box view).</summary>
        public static float MinPixels = 6f; // look-fix: 4 px bits vanished at the default zoom
        /// <summary>Look-fix (visit loop): a whole puff keeps at least this many pixels across (its spread and its bits grow with the camera's distance),
        /// so at the full-box view it still reads (batch 1: almost invisible at ~4 px). Its nominal span is <see cref="NominalSpan"/>.</summary>
        public static float MinPuffPixels = 14f;
        const float NominalSpan = 0.06f, MaxViewBoost = 3f;
        /// <summary>Look-fix: every bit is drawn over a darker silhouette this much bigger (a cartoon outline), so pale puffs (ring, droplets,
        /// grass, snow, steam) read on sand and on grass, and sparks read against the flames.</summary>
        public static float RimScale = 1.38f;

        public static HitFx Instance { get; private set; }

        /// <summary>After the glass, like the chatter marks' neighbours: the transparent queue's +2 (ShapingFx).</summary>
        const int Queue = 3002;

        readonly PuffPool pool = new();
        readonly ShakeSet shakes = new();
        readonly List<Vector3> verts = new();
        readonly List<Color> colors = new();
        readonly List<int> tris = new();
        readonly Color[] palette = new Color[System.Enum.GetValues(typeof(PuffColour)).Length];
        int paletteSeason = -2;
        GameObject go;
        Mesh mesh;
        Material material;
        PeopleView people;
        BoxView box;
        BoxCamera rig;
        float findT;
        int seedCounter;
        bool shakesUploaded;

        static readonly int ShakeAId = Shader.PropertyToID("_AC_ShakeA"), ShakeBId = Shader.PropertyToID("_AC_ShakeB"),
            ShakeCId = Shader.PropertyToID("_AC_ShakeC"), ShakeCountId = Shader.PropertyToID("_AC_ShakeCount");

        // ---- counters for tests and the debug panel
        public int LivePuffs => pool.Live;
        public int LiveBits => pool.LiveBits;
        public int PuffsSpawned => pool.Spawned;
        public int PuffsDropped => pool.Dropped;
        public int ShakesStarted { get; private set; }
        public int ShakesLive => shakes.Live;
        public int HitsSeen { get; private set; }
        public PuffKind LastKind { get; private set; }
        public int LastHitPuffs { get; private set; }
        /// <summary>The renderer, for the draw-call count (tests): one.</summary>
        public MeshRenderer Renderer { get; private set; }
        /// <summary>The last frame's vertex count (tests).</summary>
        public int VertsDrawn { get; private set; }

        public static HitFx Ensure()
        {
            if (Instance != null) return Instance;
            // In play mode it lives in the scene and dies with it: a parentless DontSave root outlives the scene and play mode
            // (PlayTestBase.LeakCheck failed every GestureEdge test on one). Out of play mode (AnimLab, edit tests) nothing may be saved.
            var g = new GameObject("HitFx");
            if (!Application.isPlaying) g.hideFlags = HideFlags.DontSave;
            return g.AddComponent<HitFx>();
        }

        /// <summary>Look-fix: the instance a stroke may use. Out of play mode a stroke never makes one: edit-mode tests that step PeopleView
        /// (PeopleViewTests' day, BerryPickView, RabbitView) made a parentless DontSave "HitFx" on every puff (OnEnable, which sets
        /// <see cref="Instance"/>, doesn't run in edit mode), and every later PlayMode test failed LeakCheck on them.</summary>
        static HitFx Live() => Application.isPlaying ? Ensure() : Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<PeopleView>() == null) return;
            Ensure();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            Instance = null;
            Unhook();
            ResetShaderShakes();
            if (go != null) Destroy(go);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            go = null; mesh = null; material = null; Renderer = null;
            pool.Clear(); shakes.Clear(); joltList.Clear();
        }

        void Hook()
        {
            var pv = PeopleView.Find();
            if (pv == people) return;
            Unhook();
            people = pv;
            if (people != null) people.WorkHit += OnWorkHit;
        }

        void Unhook()
        {
            if (people != null) people.WorkHit -= OnWorkHit;
            people = null;
        }

        static double Now => Time.unscaledTimeAsDouble;

        // ================================================================== the calls

        /// <summary>
        /// Makes one puff. <paramref name="worldPos"/> is where it starts; <paramref name="dir"/> is "away from the cut face" (chips) or the lean of a fall
        /// (leaves), the others ignore it; <paramref name="scale"/> 1 is the spec's size (the doll zoom factor is applied for you).
        /// False if puffs are off or the spot is outside the camera view (the cost guard; pass <paramref name="force"/> to skip that).
        /// </summary>
        public static bool Puff(PuffKind kind, Vector3 worldPos, Vector3 dir, float scale = 1f, bool force = false) =>
            Puff(kind, worldPos, dir, scale, PuffOptions.Default, force);

        /// <summary><see cref="Puff(PuffKind, Vector3, Vector3, float, bool)"/> with options (wet dust, muddy water, fire sparks, aimed droplets, ground height).</summary>
        public static bool Puff(PuffKind kind, Vector3 worldPos, Vector3 dir, float scale, in PuffOptions opts, bool force = false)
        {
            if (!Enabled) return false;
            var fx = Live();
            return fx != null && fx.Spawn(kind, worldPos, dir, scale, opts, force);
        }

        /// <summary>A shake on a baked bush or tree at <paramref name="worldPos"/>, leaning away along <paramref name="away"/>, after <paramref name="delay"/> seconds
        /// (the shader's sway; <see cref="ShakeTarget.Frame"/> and <see cref="ShakeTarget.Stone"/> are for <see cref="Jolt"/>). False if off or out of view.</summary>
        public static bool Shake(ShakeTarget target, Vector3 worldPos, Vector3 away, float delay = 0f, bool force = false)
        {
            if (!Enabled || target is ShakeTarget.None or ShakeTarget.Frame or ShakeTarget.Stone) return false;
            var fx = Live();
            return fx != null && fx.StartShake(target, worldPos, away, delay, force);
        }

        /// <summary>A drop-and-back jolt of a separate object (a tent frame, a stone, a stash pile): 0.012 u down, 8 Hz, τ 0.12 s (§0.5), on its local position.</summary>
        public static bool Jolt(Transform t, float delay = 0f)
        {
            if (!Enabled || t == null) return false;
            var fx = Live();
            return fx != null && fx.StartJolt(t, delay);
        }

        // ================================================================== the answer to a WorkHit

        static readonly HitAnswer None = default;

        /// <summary>
        /// Maps a stroke (the doll's move and where it works) to its answer (§0.4, B1.a–f). Water first (droplets and a ring), then a bush (leaves and a sway),
        /// then the move: Chop at the woods = chips and a tree sway (at an outcrop sparks, else chips); Hammer = dust and a frame jolt (sparks at a smithy);
        /// Hoe = dust; SitStrike (knap) = sparks; Stomp = dust (water at a puddle). Anything else that sets <c>hit</c> (the glass knock) = nothing.
        /// </summary>
        public static HitAnswer Resolve(DollStyle style, PlaceKind place, bool stagedToss = false)
        {
            bool water = place is PlaceKind.Pond or PlaceKind.Stream or PlaceKind.Puddle;
            bool strokeStyle = style is DollStyle.Chop or DollStyle.Hammer or DollStyle.Hoe or DollStyle.SitStrike or DollStyle.Stomp or DollStyle.Fish or DollStyle.Stir or DollStyle.DrawWater /* #360 */;
            if (water && (strokeStyle || style is DollStyle.Pick or DollStyle.Crouch or DollStyle.Reach))
                return new HitAnswer { hasPuff = true, puff = PuffKind.Droplets, ring = true };
            bool bush = place is PlaceKind.BushCell or PlaceKind.Bushes;
            if (bush && (style is DollStyle.Pick or DollStyle.Reach or DollStyle.Chop or DollStyle.Hoe or DollStyle.Crouch))
                return new HitAnswer { shake = ShakeTarget.Bush, leaves = 2 };
            // #362: the hunter's throw lands (Contact of the Strike beat; the point is where the spear comes down): a puff of grass, on the meadow
            if (style == DollStyle.Crouch && place == PlaceKind.Meadow) return new HitAnswer { hasPuff = true, puff = PuffKind.Grass };
            // ---- #370: the staged moments (MomentBeats): a log thrown on the fire flares (a big burst of the fire's sparks); the new chief's pop: gold sparks ----
            if (stagedToss && place == PlaceKind.Fire) return new HitAnswer { hasPuff = true, puff = PuffKind.Sparks, fire = true, big = 1.6f };
            if (style == DollStyle.Cheer && place is PlaceKind.Fire or PlaceKind.Green) return new HitAnswer { hasPuff = true, puff = PuffKind.Sparks, fire = true, gold = true };
            // ---- end #370 ----
            // #365 B9.c: the stick into the fire: its sparks float up; every third poke is bigger (the flare stand-in, as the thrown log's)
            if (style is DollStyle.PokeFire or DollStyle.PokeFireBig && place == PlaceKind.Fire) return new HitAnswer { hasPuff = true, puff = PuffKind.Sparks, fire = true, big = style == DollStyle.PokeFireBig ? 1.7f : 1f };
            switch (style)
            {
                case DollStyle.Chop:
                    if (place is PlaceKind.Woods or PlaceKind.ForestEdge) return new HitAnswer { hasPuff = true, puff = PuffKind.Chips, shake = ShakeTarget.Tree, leaves = 1 };
                    if (place is PlaceKind.Outcrop or PlaceKind.ClayBank or PlaceKind.Smithy) return new HitAnswer { hasPuff = true, puff = PuffKind.Sparks, shake = ShakeTarget.Stone };
                    return new HitAnswer { hasPuff = true, puff = PuffKind.Chips };
                case DollStyle.Hammer:
                    if (place is PlaceKind.Smithy or PlaceKind.Workshop) return new HitAnswer { hasPuff = true, puff = PuffKind.Sparks };
                    return new HitAnswer { hasPuff = true, puff = PuffKind.Dust, shake = ShakeTarget.Frame };
                case DollStyle.Hoe:
                    return new HitAnswer { hasPuff = true, puff = PuffKind.Dust };
                case DollStyle.SitStrike:
                    return new HitAnswer { hasPuff = true, puff = PuffKind.Sparks, shake = ShakeTarget.Stone, fire = false };
                case DollStyle.Stomp:
                    return new HitAnswer { hasPuff = true, puff = PuffKind.Dust };
                default:
                    return None;
            }
        }

        void OnWorkHit(PersonRef who, Place place, DollStyle style, Vector3 at)
        {
            HitsSeen++;
            LastHitPuffs = 0;
            if (!Enabled) return;
            var ans = Resolve(style, place.kind, stagedTossFrame == Time.frameCount && stagedToss.Equals(who)); // #370: only the staged toss flares
            if (ans.IsNone) return;
            var away = AwayFrom(who, at);
            if (ans.hasPuff)
            {
                var o = PuffOptions.Default;
                bool water = ans.puff == PuffKind.Droplets;
                if (water) { o.groundY = WaterY(at); at.y = Mathf.Max(at.y, o.groundY); o.mud = style == DollStyle.DrawWater && place.kind is PlaceKind.Puddle or PlaceKind.Stream; /* #360: the wet low ground is scooped muddy */ }
                else o.groundY = GroundY(at);
                o.fire = ans.fire;
                o.gold = ans.gold; // #370
                if (Spawn(ans.puff, at, away, ans.big > 0f ? ans.big : 1f, o, false)) LastHitPuffs++;
                if (ans.ring && Spawn(PuffKind.Ring, at, away, 1f, o, false)) LastHitPuffs++;
            }
            if (ans.shake == ShakeTarget.Bush)
            {
                var bv = BushView.Instance;
                if (place.kind == PlaceKind.BushCell && bv != null) bv.Shake(place.index, away);
                else StartShake(ShakeTarget.Bush, at, away, 0f, false);
            }
            else if (ans.shake == ShakeTarget.Tree) StartShake(ShakeTarget.Tree, at, away, 0f, false);
            // Frames and stones have no object of their own yet (the tent frame is in a baked mesh): the dust and the sparks carry their answer.
            if (ans.leaves > 0 && (ans.shake == ShakeTarget.Bush || ans.shake == ShakeTarget.Tree) && !Winter())
            {
                var o = PuffOptions.Default;
                o.count = ans.leaves;
                o.groundY = GroundY(at);
                Spawn(PuffKind.Leaves, at + Vector3.up * (ans.shake == ShakeTarget.Tree ? 0.18f : 0.05f), away, 1f, o, false);
            }
        }

        Vector3 AwayFrom(PersonRef who, Vector3 at)
        {
            if (people != null && people.TryGetDollFeet(who, out var feet))
            {
                var d = new Vector3(at.x - feet.x, 0f, at.z - feet.z);
                if (d.sqrMagnitude > 1e-6f) return d.normalized;
            }
            return Vector3.forward;
        }

        static bool Winter()
        {
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            return sim != null && LifeClock.SeasonOf(sim.State.month) == Season.Winter;
        }

        // ================================================================== world helpers

        BoxView Box => box != null ? box : box = FindAnyObjectByType<BoxView>();

        float GroundY(Vector3 world)
        {
            var b = Box;
            if (b == null || b.Root == null) return world.y - 0.06f;
            var local = b.Root.InverseTransformPoint(world);
            return b.Root.TransformPoint(new Vector3(local.x, b.SurfaceHeight(local.x, local.z), local.z)).y;
        }

        float WaterY(Vector3 world)
        {
            var b = Box;
            if (b == null || b.Root == null) return world.y;
            var local = b.Root.InverseTransformPoint(world);
            float surface = b.SurfaceHeight(local.x, local.z);
            // Standing on the bank, the water is a little below; over the pond itself the surface height is the water's.
            return b.Root.TransformPoint(new Vector3(local.x, Mathf.Min(surface, Land.WaterY), local.z)).y;
        }

        /// <summary>Look-fix: the puff scale that keeps <see cref="NominalSpan"/> at least <see cref="MinPuffPixels"/> across from the main camera
        /// (1 when close, up to <see cref="MaxViewBoost"/>). The camera's own pixel height, so a render texture (AnimLab) counts right.</summary>
        static float ViewBoost(Vector3 at)
        {
            var cam = Camera.main;
            if (cam == null || cam.pixelHeight <= 0) return 1f;
            float perPx = PerPixel(cam, at);
            return Mathf.Clamp(MinPuffPixels * perPx / NominalSpan, 1f, MaxViewBoost);
        }

        static float PerPixel(Camera cam, Vector3 at)
        {
            float depth = Mathf.Max(0.01f, Vector3.Dot(at - cam.transform.position, cam.transform.forward));
            return cam.orthographic ? 2f * cam.orthographicSize / cam.pixelHeight : 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;
        }

        float ZoomScale
        {
            get
            {
                if (rig == null && Camera.main != null) rig = Camera.main.GetComponent<BoxCamera>();
                return rig != null ? CameraReach.DollScale(rig.zoom) : 1f;
            }
        }

        /// <summary>The cost guard: inside the camera's view with a margin (no camera = yes, for tests).</summary>
        public static bool InView(Vector3 world, float margin = 0.1f)
        {
            var cam = Camera.main;
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x > -margin && v.x < 1f + margin && v.y > -margin && v.y < 1f + margin;
        }

        // ================================================================== spawning

        bool Spawn(PuffKind kind, Vector3 at, Vector3 dir, float scale, in PuffOptions o, bool force)
        {
            if (!force && !InView(at)) return false;
            if (kind == PuffKind.Sparks && o.fire) at += Vector3.up * 0.07f * ZoomScale; // look-fix: the fire's sparks start over the flames, not inside them
            pool.Spawn(kind, at, dir, scale * Mathf.Max(ZoomScale, ViewBoost(at)), Now, unchecked(++seedCounter * 7919 + (int)kind * 104729), o);
            LastKind = kind;
            return true;
        }

        bool StartShake(ShakeTarget target, Vector3 at, Vector3 away, float delay, bool force)
        {
            if (!force && !InView(at, 0.2f)) return false;
            shakes.Add(ShakeSpec.Of(target), at, GroundY(at), away, Now, delay);
            ShakesStarted++;
            return true;
        }

        // ---- jolts (separate objects)
        struct JoltEntry { public Transform t; public Vector3 basePos; public double start; }
        readonly List<JoltEntry> joltList = new();

        bool StartJolt(Transform t, float delay)
        {
            for (int i = 0; i < joltList.Count; i++)
                if (joltList[i].t == t) { var e = joltList[i]; e.start = Now + delay; joltList[i] = e; return true; }
            joltList.Add(new JoltEntry { t = t, basePos = t.localPosition, start = Now + delay });
            ShakesStarted++;
            return true;
        }

        void StepJolts(double now)
        {
            var spec = ShakeSpec.Jolt;
            for (int i = joltList.Count - 1; i >= 0; i--)
            {
                var e = joltList[i];
                if (e.t == null) { joltList.RemoveAt(i); continue; }
                float age = (float)(now - e.start);
                if (age > spec.Seconds) { e.t.localPosition = e.basePos; joltList.RemoveAt(i); continue; }
                e.t.localPosition = e.basePos + Vector3.up * ShakeMath.JoltOffset(spec, age);
            }
        }

        // ================================================================== a frame

        void Update()
        {
            findT -= Time.unscaledDeltaTime;
            if (people == null || findT <= 0f) { findT = 0.5f; Hook(); }
        }

        void LateUpdate()
        {
            double now = Now;
            pool.Step(now);
            StepJolts(now);
            StepShakes(now);
            Draw(now);
        }

        void StepShakes(double now)
        {
            int n = shakes.Step(now);
            if (n == 0 && !shakesUploaded) return;
            Shader.SetGlobalVectorArray(ShakeAId, shakes.A);
            Shader.SetGlobalVectorArray(ShakeBId, shakes.B);
            Shader.SetGlobalVectorArray(ShakeCId, shakes.C);
            Shader.SetGlobalFloat(ShakeCountId, n);
            shakesUploaded = n > 0;
        }

        static void ResetShaderShakes() => Shader.SetGlobalFloat(ShakeCountId, 0f);

        // ================================================================== drawing

        void BuildPalette()
        {
            int season = -1;
            var sim = GameClock.Instance != null ? GameClock.Instance.Sim : null;
            if (sim != null) season = (int)LifeClock.SeasonOf(sim.State.month);
            if (season == paletteSeason) return;
            paletteSeason = season;
            Color L(Color c) => MeshBuilder.Linear(c);
            Color grass = season switch { (int)Season.Spring => Palette.Sprout, (int)Season.Autumn => Palette.Straw, (int)Season.Winter => Palette.Sand, _ => Palette.GrassA };
            Color leaf = season switch { (int)Season.Spring => Palette.LeafC, (int)Season.Autumn => Palette.Terracotta, (int)Season.Winter => Palette.Sand, _ => Palette.LeafA };
            palette[(int)PuffColour.WoodPale] = L(Color.Lerp(Palette.WoodLight, Color.white, 0.35f));
            palette[(int)PuffColour.Bark] = L(Palette.Bark);
            palette[(int)PuffColour.DrySoil] = L(Palette.Dirt);
            palette[(int)PuffColour.WetSoil] = L(Palette.Mud);
            palette[(int)PuffColour.Ember] = L(Palette.Ember);
            palette[(int)PuffColour.SparkYellow] = L(Palette.FireCore);
            palette[(int)PuffColour.Gold] = L(Palette.Accents[1]);
            palette[(int)PuffColour.Water] = L(Color.Lerp(Palette.Water, Color.white, 0.5f));
            palette[(int)PuffColour.Mud] = L(Palette.Mud);
            palette[(int)PuffColour.RingWater] = L(Color.Lerp(Palette.Water, Color.white, 0.75f));
            palette[(int)PuffColour.RingMud] = L(Color.Lerp(Palette.Mud, Color.white, 0.2f));
            palette[(int)PuffColour.Grass] = L(grass);
            palette[(int)PuffColour.Leaf] = L(leaf);
            palette[(int)PuffColour.Steam] = L(Palette.FireSmoke);
            palette[(int)PuffColour.Snow] = L(new Color(0.96f, 0.97f, 1f));
        }

        /// <summary>The colour a palette entry draws in now (linear, the season's grass and leaf included).</summary>
        public Color ColourOf(PuffColour c) { BuildPalette(); return palette[(int)c]; }

        void Draw(double now)
        {
            var cam = Camera.main;
            if (pool.Live == 0 || cam == null) { Hide(); VertsDrawn = 0; return; }
            BuildPalette();
            verts.Clear(); colors.Clear(); tris.Clear();
            var right = cam.transform.right;
            var up = cam.transform.up;
            float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            for (int s = 0; s < pool.SlotCount; s++)
            {
                var p = pool[s];
                if (!p.live) continue;
                float since = (float)(now - p.start);
                for (int i = 0; i < p.bitCount; i++)
                {
                    ref var b = ref p.bits[i];
                    float age = HitFxMath.Age(b, since);
                    float size = HitFxMath.Size(b, age);
                    if (size <= 0f) continue;
                    var pos = HitFxMath.Position(b, age);
                    float depth = Vector3.Dot(pos - cam.transform.position, cam.transform.forward);
                    if (depth <= 0f) continue;
                    float perPx = cam.orthographic ? 2f * cam.orthographicSize / cam.pixelHeight : 2f * depth * tan / cam.pixelHeight;
                    var col = palette[(int)b.colour];
                    var rim = RimOf(b.colour, col);
                    var back = cam.transform.forward * Mathf.Max(0.002f, perPx * 0.5f); // the outline sits just behind the fill
                    if (b.shape == PuffShape.Ring)
                    {
                        float line = Mathf.Max(HitFxMath.Line(b, age), perPx * 2f * Shrink(age, b.life));
                        HitFxShapes.RingTris(size, line * 2.4f, pos + back, verts, tris); // a dark band under the pale ring
                        for (int k = 0; k < HitFxShapes.RingSides * 2; k++) colors.Add(rim);
                        HitFxShapes.RingTris(size, line, pos, verts, tris);
                        for (int k = 0; k < HitFxShapes.RingSides * 2; k++) colors.Add(col);
                        continue;
                    }
                    float floor = perPx * MinPixels * (b.shape == PuffShape.Blob ? 0.5f : 1f);
                    float shrink = HitFxMath.Shrink(age, b.life);
                    size = Mathf.Max(size, floor * shrink);
                    float roll = HitFxMath.Roll(b, age);
                    if (b.shape == PuffShape.Drop)
                    {
                        var v = HitFxMath.Velocity(b, age);
                        roll = Mathf.Atan2(Vector3.Dot(v, up), Vector3.Dot(v, right)) * Mathf.Rad2Deg;
                    }
                    float cr = Mathf.Cos(roll * Mathf.Deg2Rad), sr = Mathf.Sin(roll * Mathf.Deg2Rad);
                    float stretch = b.stretch > 0f ? b.stretch : 1f;
                    var o = HitFxShapes.Unit(b.shape);
                    for (int pass = 0; pass < 2; pass++) // look-fix: the outline first (bigger, darker, just behind), then the fill
                    {
                        float sz = pass == 0 ? size * RimScale + perPx * 1.5f : size;
                        var at = pass == 0 ? pos + back : pos;
                        var c = pass == 0 ? rim : col;
                        int start = verts.Count;
                        for (int k = 0; k < o.v.Length; k++)
                        {
                            float x = o.v[k].x * sz * stretch, y = o.v[k].y * sz;
                            float rx = x * cr - y * sr, ry = x * sr + y * cr;
                            verts.Add(at + right * rx + up * ry);
                            colors.Add(c);
                        }
                        for (int k = 0; k < o.t.Length; k++) tris.Add(start + o.t[k]);
                    }
                }
            }
            Upload();
        }

        static float Shrink(float age, float life) => HitFxMath.Shrink(age, life);

        /// <summary>Look-fix: the outline colour of a puff bit: a dark version of its own colour (sparks: a deep ember, so they stand out of the flames).</summary>
        Color RimOf(PuffColour pc, Color col)
        {
            if (pc is PuffColour.SparkYellow or PuffColour.Ember or PuffColour.Gold) return MeshBuilder.Linear(new Color(0.35f, 0.08f, 0.03f));
            float k = pc is PuffColour.Water or PuffColour.RingWater or PuffColour.Snow or PuffColour.Steam ? 0.5f : 0.42f;
            return new Color(col.r * k, col.g * k, col.b * k, 1f);
        }

        void Upload()
        {
            VertsDrawn = verts.Count;
            if (verts.Count == 0) { Hide(); return; }
            if (go == null) Make();
            if (go == null) return;
            if (!go.activeSelf) go.SetActive(true);
            mesh.Clear(true);
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f); // world space, always near the box
        }

        void Hide() { if (go != null && go.activeSelf) go.SetActive(false); }

        void Make()
        {
            var src = ShapingView.LoadFx();
            if (src == null) return;
            go = new GameObject("HitFxPuffs") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            mesh = new Mesh { name = "HitFxPuffs", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = go.AddComponent<MeshRenderer>();
            material = new Material(src) { name = "HitFxPuffs", hideFlags = HideFlags.DontSave, renderQueue = Queue };
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
        }

        // ================================================================== for tests and cheats

        /// <summary>Drops every live puff and shake (tests, "Puffs off").</summary>
        public void ClearAll()
        {
            pool.Clear(); shakes.Clear();
            foreach (var e in joltList) if (e.t != null) e.t.localPosition = e.basePos;
            joltList.Clear();
            ResetShaderShakes();
        }
    }
}
