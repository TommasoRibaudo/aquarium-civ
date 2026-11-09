using AquariumCiv.Audio;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.View
{
    /// <summary>
    /// P8 misting (keepers-hands.md §3.1, #244): hold a finger still on the land and a fine mist drifts down there; the
    /// sim wets the ground at once and its core greens live (the user, 2026-11-02: an answer to every early mist). No hand
    /// or bottle is shown. <b>A temporary input</b> until the kit button (#249) is signed off: it never claims a press,
    /// it only takes over a press that stays still for <see cref="holdDelay"/>, and while it mists the camera holds still
    /// (<see cref="BoxCamera.HoldOrbitUntil"/>). Sliding the finger then mists along the way. Only while the living
    /// land is on, outside the other tools' modes and sheets.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public class EcoMistView : MonoBehaviour, ICameraGate
    {
        public static bool AutoCreate = true;
        public static EcoMistView Instance { get; private set; }

        [Tooltip("A press that stays within the slop this long starts the mist (seconds).")] public float holdDelay = 0.4f; // longer than a tap (GlassVoice.TapMaxSeconds 0.35): a hold never also opens a card
        [Tooltip("How far a finger may wander before the hold is a drag (share of screen height).")] public float slop = 0.02f;
        [Tooltip("The sim is told about the mist this often while it runs (seconds).")] public float pulseSeconds = 0.25f;
        [Tooltip("The box refreshes (rebuilds the greened chunk) this often while misting, and when it ends (seconds).")] public float refreshSeconds = 1.2f;

        BoxView box;
        BoxCamera cam;
        GameHud hud;
        ParticleSystem mist, splash, haze;
        Material dropMat;
        MistSound sound;
        bool pressed, misting;
        float pressT, pulseT, refreshT;
        Vector2 pressPos;
        bool dirty;
        // How long the finger has stayed about one spot (seconds): the mist reaches further the longer it lingers and only a long
        // dwell soaks the ground to puddles. Sliding on resets it.
        float dwell;
        Vector3 dwellAt;
        bool dwellOn;
        [Tooltip("While the mister is held, chunk rebuilds skip the AO bake and go one a frame; AO catches up when the finger lifts (#277).")] public bool cheapStrokes = true;
        /// <summary>The nozzle's height over the ground (box units).</summary>
        public const float NozzleHeight = 0.5f;

        /// <summary>How long the finger has lingered about one spot (seconds), for tests and the debug panel.</summary>
        public float Dwell => dwell;
        /// <summary>How far the spray reaches now (box units), from the sim's radius for this dwell.</summary>
        public float SprayRadius { get; private set; }
        public MistSound Sound => sound;
        /// <summary>The droplets are flying (tests).</summary>
        public bool Spraying => mist != null && mist.isEmitting;
        public bool Misting => misting;
        public int MistPulses { get; private set; }
        /// <summary>The last point misted on the ground (box-local), for tests and the debug panel.</summary>
        public Vector3 LastMist { get; private set; }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<EcoMistView>() == null) b.gameObject.AddComponent<EcoMistView>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            EcoSurface.Ensure(box); // the wet ground, shoots, green patch and puddles this mist leaves
            PuddleLife.Ensure(box); // and the people and rabbit who use its puddles
            sound = GetComponent<MistSound>() ?? gameObject.AddComponent<MistSound>();
            WarmUp();
        }

        /// <summary>#296: the spray's particle systems are made at load and each draws one invisible droplet once, so the first
        /// spray of a session makes nothing and compiles nothing (the tester measured a 120-135 ms frame there).</summary>
        void WarmUp()
        {
            if (mist != null) return;
            mist = MakeSpray(false); splash = MakeSpray(true); haze = MakeHaze();
            var at = box != null && box.land != null ? box.transform.TransformPoint(box.land.OnGround(0f, 0f) + Vector3.up * 0.2f) : transform.position;
            foreach (var ps in new[] { mist, splash, haze })
            {
                ps.transform.position = at;
                ps.Emit(new ParticleSystem.EmitParams { position = at, applyShapeToPosition = false, startColor = new Color32(255, 255, 255, 0), startSize = 0.001f, startLifetime = 0.05f, velocity = Vector3.zero }, 1);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            WarmedUp = true;
        }

        /// <summary>The spray was made and drawn once at load (tests).</summary>
        public bool WarmedUp { get; private set; }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            cam?.RemoveGate(this);
            if (box != null && box.deferAO) { box.deferAO = false; box.FlushAo(); }
            sound?.SetSpray(0f);
            if (mist != null) Destroy(mist.gameObject);
            if (splash != null) Destroy(splash.gameObject);
            if (haze != null) Destroy(haze.gameObject);
            mist = splash = haze = null;
        }

        bool Usable(SimCore sim)
        {
            if (sim == null || !sim.EcologyOn || box == null) return false;
            hud ??= GameHud.Instance;
            // The first run hides the HUD until it's done, and its first mist is this gesture (#248): hidden only blocks outside it.
            if (hud != null && ((!hud.IsVisible && !sim.State.firstRun.active) || hud.CleanMode || hud.CameraMode || hud.SheetOpen)) return false;
            // In the first run the mist is its own step's: not while wiping or naming (build 15: misting on the name step hid "Name it").
            // A hold that began on the mist step keeps spraying while the finger stays down after the step moves on (#295: it
            // cut out ~1 s in, as the first green came); a new press on the name step still does nothing.
            if (sim.State.firstRun.active && sim.State.firstRun.step != (int)FirstRunStep.Rain && !misting) return false;
            var shaping = ShapingView.Instance;
            if (shaping != null && shaping.ShapeMode) return false;
            var cards = PersonCardView.Instance;
            if (cards != null && (cards.AnyOpen || cards.BuildingOpen)) return false;
            return true;
        }

        /// <summary>How long this frame's Update took (ms), for the frame-time tests (#296).</summary>
        public double LastUpdateMs { get; private set; }
        readonly System.Diagnostics.Stopwatch updateWatch = new();

        void Update()
        {
            updateWatch.Restart();
            Step();
            LastUpdateMs = updateWatch.Elapsed.TotalMilliseconds;
        }

        void Step()
        {
            var sim = Sim;
            _ = Cam; // registers the camera gate
            float dt = Time.unscaledDeltaTime;
            // The glass shows the air's humidity (keepers-hands §5.1): eased, so a wipe clears it over a moment, not a frame.
            if (box != null)
            {
                float want = sim != null && sim.EcologyOn ? sim.EcoDroplets : 0f;
                if (Mathf.Abs(want - box.Droplets) > 0.001f) box.SetDroplets(Mathf.MoveTowards(box.Droplets, want, dt * 0.8f));
            }
            int n = ReadPointer(out var pos);
            hud ??= GameHud.Instance;
            var hand = hud != null ? hud.Hand : GameHud.KitTool.None;
            if (hand == GameHud.KitTool.Seed) { SeedHand(sim, n, pos); return; }
            if (hand == GameHud.KitTool.Tweezers || hand == GameHud.KitTool.Life) { if (misting) EndMist(); pressed = false; return; } // the tweezers are CareView's (#311), the life tool LifePlaceView's (#423): no hold-to-mist under them
            if (n != 1 || !Usable(sim))
            {
                if (misting) EndMist();
                pressed = false;
                return;
            }
            float now = Time.unscaledTime;
            bool inHand = hand == GameHud.KitTool.Mister; // the mister in hand: one finger mists at once and a drag mists along (keepers-hands 3.1)
            if (!pressed)
            {
                pressed = true; pressT = now; pressPos = pos;
                if (UiInput.Over(pos)) pressT = float.PositiveInfinity; // a press on the UI is the UI's
                return;
            }
            if (!misting)
            {
                if (float.IsPositiveInfinity(pressT)) return;
                if (!inHand && (pos - pressPos).magnitude > slop * Mathf.Max(1, Screen.height)) { pressT = float.PositiveInfinity; return; } // a drag: the camera's
                if (!inHand && now - pressT < holdDelay) return;
                BeginMist();
            }
            var bc = Cam; if (bc != null) bc.HoldOrbitUntil = now + 0.25f;
            if (!Ground(pos, out var at)) return;
            // Lingering: the same spot (within a quarter unit) keeps adding to the dwell; moving on starts it again.
            if (!dwellOn || (at - dwellAt).sqrMagnitude > DwellSlop * DwellSlop) { dwellAt = at; dwell = 0f; dwellOn = true; }
            dwell += dt;
            SprayRadius = sim.EcoMistRadius(dwell);
            Spray(at);
            pulseT += dt; refreshT += dt;
            if (pulseT >= pulseSeconds) { Pulse(sim, at, pulseT, dwell); pulseT = 0f; }
            if (refreshT >= (cheapStrokes ? StrokeRefreshSeconds : refreshSeconds) && dirty) { refreshT = 0f; dirty = false; box.Refresh(); }
        }

        const float DwellSlop = 0.25f, StrokeRefreshSeconds = 0.35f;

        void BeginMist()
        {
            misting = true; pulseT = pulseSeconds; refreshT = 0f; // on a person too: they get wet (tester: a hold there did nothing)
            dwell = 0f; dwellOn = false;
            // #277: while the finger is down the chunks rebuild without the AO bake, one a frame; the tint under the finger comes
            // first (EcoSurface) and the mesh follows. The AO catches up when the finger lifts.
            if (cheapStrokes && box != null) box.deferAO = true;
            Sfx.Haptic(HapticKind.Light); // one soft tick as the spray starts
        }

        BoxCamera Cam
        {
            get
            {
                if (cam == null && Camera.main != null) { cam = Camera.main.GetComponent<BoxCamera>(); cam?.AddGate(this); }
                return cam;
            }
        }

        /// <summary>The camera leaves one finger to the kit's mister and seed while one is in hand (two fingers still move the view).</summary>
        public bool ClaimsPress(Vector2 screenPos)
        {
            hud ??= GameHud.Instance;
            if (hud == null || hud.Hand == GameHud.KitTool.None || UiInput.Over(screenPos)) return false;
            var sim = Sim;
            return sim != null && sim.EcologyOn && Ground(screenPos, out _);
        }

        // ---------- the seed in hand: a tap on the land plants the one tree ----------
        bool seedDown;
        float seedT;
        Vector2 seedAt, seedLast;
        /// <summary>The last seed tap's answer (tests and the debug panel).</summary>
        public string LastSeed { get; private set; } = "";

        void SeedHand(SimCore sim, int n, Vector2 pos)
        {
            if (misting) EndMist();
            pressed = false;
            if (sim == null || !sim.EcologyOn) return;
            if (n == 1)
            {
                if (!seedDown) { seedDown = !UiInput.Over(pos); seedT = Time.unscaledTime; seedAt = pos; }
                seedLast = pos;
                return;
            }
            if (!seedDown) return;
            seedDown = false;
            if (n > 1) return; // two fingers: the camera's
            if ((seedLast - seedAt).magnitude > slop * 2f * Mathf.Max(1, Screen.height)) return; // a drag isn't a tap
            if (!Ground(seedLast, out var at)) return;
            if (!sim.CanPlantTree) { LastSeed = "no seed"; hud?.SetHand(GameHud.KitTool.None); return; }
            int tcell = sim.TreeCellFor(at.x, at.z);
            bool moved = tcell >= 0 && tcell != AquariumCiv.Sim.Settle.SiteMap.CellAt(at.x, at.z);
            if (!sim.PlantTree(at.x, at.z))
            {
                LastSeed = "refused";
                hud?.Toast("Not there: the seed needs dry ground above the hollow.", 3f);
                return;
            }
            LastSeed = $"planted at ({at.x:0.00}, {at.z:0.00})";
            box.Refresh();
            GameClock.Instance?.Save();
            hud?.SetHand(GameHud.KitTool.None);
            // #378: a press on the glass rim lands a little in from it; say so, the seed never jumps silently.
            hud?.Toast(moved ? "The seed went in a little way from the glass, where a tree has room. Mist it, and keep its weeds back." : "The seed is in the ground. Mist it, and keep its weeds back.", 4f);
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null) pv.Planner.ReactAt(AquariumCiv.Life.KeeperGesture.Rain, pv.Now, at.x, at.z, 1.4f);
        }

        void EndMist()
        {
            misting = false;
            dwellOn = false; dwell = 0f;
            if (mist != null) mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (splash != null) splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (haze != null) haze.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            sound?.SetSpray(0f);
            // The finger is up: the AO the stroke skipped is baked now, a chunk a frame (BoxView.CatchUp).
            if (box != null && box.deferAO) { box.deferAO = false; box.FlushAo(); box.CatchUp(); dirty = true; }
            if (dirty) { dirty = false; box.Refresh(); }
        }

        /// <summary>Mists the ground at a box-local point for this long (also the debug panel's "mist here" and tests).
        /// <paramref name="hold"/> is how long the finger has lingered there: it sets the reach and what soaks to puddles
        /// (default: the same as <paramref name="seconds"/>, a whole hold in one go).</summary>
        public void Pulse(SimCore sim, Vector3 at, float seconds, float hold = -1f)
        {
            if (sim == null || !sim.EcologyOn) return;
            int greened = sim.Mist(at.x, at.z, seconds, hold < 0f ? seconds : hold).Count;
            MistPulses++;
            LastMist = at;
            if (hud != null && hud.Hand == GameHud.KitTool.Mister) GameHud.NoteMisterUsed(); // its hint has done its job
            dirty = true;
            // The first mist of a box is its first answer (tester: too small at the default zoom): the camera leans in on it.
            if (greened > 0 && !leaned && sim.State.firstRun.active)
            {
                leaned = true;
                var bc = Cam;
                if (bc != null && !bc.IsFlying) bc.FlyTo(box.transform.TransformPoint(at), 0.62f, 1.4f);
            }
            // keepers-hands §2.1: every early mist gets an answer. Those near it stand in it, hands out and faces up; the
            // kids splash about (the rain's reaction, at the mist's spot). Once every couple of seconds while it runs.
            if (Time.unscaledTime - lastReact > 2f)
            {
                var pv = PeopleView.Find();
                if (pv != null && pv.Planner != null) { pv.Planner.ReactAt(AquariumCiv.Life.KeeperGesture.Rain, pv.Now, at.x, at.z, 1.4f); lastReact = Time.unscaledTime; Reactions++; }
            }
        }

        float lastReact = -99f;
        bool leaned;
        /// <summary>Times the people were asked to react to a mist (tests).</summary>
        public int Reactions { get; private set; }

        // ---------------- the spray itself: droplets from a nozzle above the finger down to the ground ----------------
        void Spray(Vector3 at)
        {
            if (mist == null) WarmUp();
            var nozzle = box.transform.TransformPoint(at + Vector3.up * NozzleHeight);
            mist.transform.position = nozzle;
            splash.transform.position = box.transform.TransformPoint(at + Vector3.up * 0.01f);
            haze.transform.position = box.transform.TransformPoint(at + Vector3.up * 0.10f);
            // The droplets land across most of the reach: the cone opens as the mist widens with the dwell.
            float spread = Mathf.Max(0.2f, SprayRadius * 0.62f);
            var sh = mist.shape; sh.angle = Mathf.Clamp(Mathf.Atan(spread / NozzleHeight) * Mathf.Rad2Deg, 8f, 55f);
            var sp = splash.shape; sp.radius = spread * 0.9f;
            var hz = haze.shape; hz.radius = spread * 0.75f;
            if (!mist.isEmitting) { mist.Play(); splash.Play(); haze.Play(); }
            sound?.SetSpray(Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(0.7f, 1f, SprayRadius)));
        }

        /// <summary>The droplets: small, quick, stretched into streaks along their fall, pale blue-white and soft (not smoke), or
        /// the ground-level flecks where they land.</summary>
        ParticleSystem MakeSpray(bool ground)
        {
            var go = new GameObject(ground ? "MistSplash" : "MistSpray") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            var sh = ps.shape;
            var r = go.GetComponent<ParticleSystemRenderer>();
            dropMat ??= EcoSurface.MakeMaterial("EcoDrop", 4, 5, 10);
            r.sharedMaterial = dropMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (!ground)
            {
                // fine droplets: many, small and see-through, so the spray reads as a falling mist, not a white tuft (tester run 1)
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.42f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);   // down: a half-unit fall in a third of a second
                main.startSize = new ParticleSystem.MinMaxCurve(0.0055f, 0.0105f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.70f, 0.86f, 1f, 0.42f), new Color(0.88f, 0.96f, 1f, 0.62f));
                main.gravityModifier = 0.25f;
                main.maxParticles = 1200;
                em.rateOverTime = 640f;
                sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 30f; sh.radius = 0.03f;
                sh.rotation = new Vector3(90f, 0f, 0f); // pointing down
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.035f; r.lengthScale = 1.6f;
            }
            else
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.007f, 0.013f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.9f, 1f, 0.38f), new Color(1f, 1f, 1f, 0.5f));
                main.gravityModifier = 0.4f;
                main.maxParticles = 300;
                em.rateOverTime = 110f;
                sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.4f; sh.radiusThickness = 1f;
                sh.rotation = new Vector3(-90f, 0f, 0f); // the circle lies flat; the fleck hops up
                var vel = ps.velocityOverLifetime; vel.space = ParticleSystemSimulationSpace.World;
                // #299: every axis in the same mode (two constants), set before the module turns on, or Unity logs "Particle
                // Velocity curves must all be in the same mode" when y alone is a range and x/z keep their default single constant.
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.enabled = true;
                r.renderMode = ParticleSystemRenderMode.Billboard;
            }
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(ground ? 0f : 0.85f, 1f) });
            col.color = g;
            return ps;
        }

        /// <summary>A faint haze where the mist hangs over the ground: a few big, soft, nearly clear puffs that swell and fade.</summary>
        ParticleSystem MakeHaze()
        {
            var go = new GameObject("MistHaze") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.86f, 0.93f, 1f, 0.10f), new Color(0.95f, 0.98f, 1f, 0.16f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 24f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.4f; sh.radiusThickness = 1f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            dropMat ??= EcoSurface.MakeMaterial("EcoDrop", 4, 5, 10);
            r.sharedMaterial = dropMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 10f; // behind the droplets
            return ps;
        }

        // ---------------- input ----------------
        static int ReadPointer(out Vector2 a)
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

        /// <summary>The ground under a screen point, box-local: marches the ray down to the land.</summary>
        /// <summary>The land point (box-local) under a screen point, for the other kit tools (the tweezers, #311).</summary>
        public bool GroundPoint(Vector2 screen, out Vector3 at) => Ground(screen, out at);

        bool Ground(Vector2 screen, out Vector3 at)
        {
            at = default;
            var c = Camera.main;
            if (c == null) return false;
            var ray = c.ScreenPointToRay(screen);
            var o = box.transform.InverseTransformPoint(ray.origin);
            var d = box.transform.InverseTransformDirection(ray.direction).normalized;
            float prev = 0f;
            for (float t = 0f; t < 30f; t += 0.02f)
            {
                var p = o + d * t;
                if (Mathf.Abs(p.x) > Land.HX + 1f && t > 5f && p.y < 0f) break;
                if (Mathf.Abs(p.x) <= Land.HX && Mathf.Abs(p.z) <= Land.HZ && p.y <= box.land.Height(p.x, p.z))
                {
                    // one bisection pass between the last two samples
                    float lo = prev, hi = t;
                    for (int k = 0; k < 8; k++) { float m = (lo + hi) * 0.5f; var q = o + d * m; if (q.y <= box.land.Height(q.x, q.z)) hi = m; else lo = m; }
                    at = o + d * hi;
                    at.x = Mathf.Clamp(at.x, -Land.HX + 0.05f, Land.HX - 0.05f); at.z = Mathf.Clamp(at.z, -Land.HZ + 0.05f, Land.HZ - 0.05f);
                    return true;
                }
                prev = t;
            }
            return false;
        }
    }
}
