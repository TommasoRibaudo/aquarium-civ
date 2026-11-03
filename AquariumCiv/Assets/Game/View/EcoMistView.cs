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
        ParticleSystem mist;
        bool pressed, misting;
        float pressT, pulseT, refreshT;
        Vector2 pressPos;
        bool dirty;
        readonly List<Bloom> blooms = new();

        /// <summary>The live green: a disc on the ground that spreads out from where the mist fell, until the box's own
        /// chunk (rebuilt with the new cover) takes over.</summary>
        sealed class Bloom
        {
            public Vector3 at;
            public float t, radius;
            public GameObject go;
            public Mesh mesh;
        }

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
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            cam?.RemoveGate(this);
            foreach (var b in blooms) Drop(b);
            blooms.Clear();
            if (mist != null) Destroy(mist.gameObject);
        }

        bool Usable(SimCore sim)
        {
            if (sim == null || !sim.EcologyOn || box == null) return false;
            hud ??= GameHud.Instance;
            // The first run hides the HUD until it's done, and its first mist is this gesture (#248): hidden only blocks outside it.
            if (hud != null && ((!hud.IsVisible && !sim.State.firstRun.active) || hud.CleanMode || hud.CameraMode || hud.SheetOpen)) return false;
            // In the first run the mist is its own step's: not while wiping or naming (build 15: misting on the name step hid "Name it").
            if (sim.State.firstRun.active && sim.State.firstRun.step != (int)FirstRunStep.Rain) return false;
            var shaping = ShapingView.Instance;
            if (shaping != null && shaping.ShapeMode) return false;
            var cards = PersonCardView.Instance;
            if (cards != null && (cards.AnyOpen || cards.BuildingOpen)) return false;
            return true;
        }

        void Update()
        {
            var sim = Sim;
            _ = Cam; // registers the camera gate
            float dt = Time.unscaledDeltaTime;
            Grow(dt);
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
                misting = true; pulseT = pulseSeconds; refreshT = 0f; // on a person too: they get wet (tester: a hold there did nothing)
            }
            var bc = Cam; if (bc != null) bc.HoldOrbitUntil = now + 0.25f;
            if (!Ground(pos, out var at)) return;
            Puff(at);
            pulseT += dt; refreshT += dt;
            if (pulseT >= pulseSeconds) { Pulse(sim, at, pulseT); pulseT = 0f; }
            if (refreshT >= refreshSeconds && dirty) { refreshT = 0f; dirty = false; box.Refresh(); }

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
            if (!sim.PlantTree(at.x, at.z))
            {
                LastSeed = "refused";
                hud?.Toast("Not there: the seed needs dry ground above the hollow.", 3f);
                return;
            }
            LastSeed = $"planted at ({at.x:0.00}, {at.z:0.00})";
            AddBloom(at, 0.2f);
            box.Refresh();
            GameClock.Instance?.Save();
            hud?.SetHand(GameHud.KitTool.None);
            hud?.Toast("The seed is in the ground. Mist it, and keep its weeds back.", 4f);
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null) pv.Planner.ReactAt(AquariumCiv.Life.KeeperGesture.Rain, pv.Now, at.x, at.z, 1.4f);
        }

        void EndMist()
        {
            misting = false;
            if (mist != null) mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (dirty) { dirty = false; box.Refresh(); }
        }

        /// <summary>Mists the ground at a box-local point for this long (also the debug panel's "mist here" and tests).</summary>
        public void Pulse(SimCore sim, Vector3 at, float seconds)
        {
            if (sim == null || !sim.EcologyOn) return;
            int greened = sim.Mist(at.x, at.z, seconds).Count;
            MistPulses++;
            LastMist = at;
            dirty = true;
            if (greened > 0) AddBloom(at, Mathf.Max(0.32f, Mathf.Sqrt(greened) * AquariumCiv.Sim.Settle.SiteMap.Cell * 0.7f + 0.12f));
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

        // ---------------- the mist itself ----------------
        void Puff(Vector3 at)
        {
            if (mist == null) mist = MakeMist();
            var w = box.transform.TransformPoint(at + Vector3.up * 0.55f);
            mist.transform.position = w;
            if (!mist.isEmitting) mist.Play();
        }

        ParticleSystem MakeMist()
        {
            var go = new GameObject("Mist") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(0.92f, 0.96f, 1f, 0.55f);
            main.gravityModifier = 0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 220f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 22f; sh.radius = 0.12f;
            sh.rotation = new Vector3(90f, 0f, 0f); // pointing down
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = box.smoke != null ? box.smoke : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // ---------------- the live green ----------------
        void AddBloom(Vector3 at, float radius)
        {
            foreach (var b in blooms) if ((b.at - at).sqrMagnitude < 0.0025f) { b.radius = Mathf.Max(b.radius, radius); b.t = Mathf.Min(b.t, 0.3f); return; }
            var go = new GameObject("Bloom") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(box.Root != null ? box.Root : transform, false);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = box.SolidMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blooms.Add(new Bloom { at = at, radius = radius, go = go, mesh = new Mesh { name = "Bloom" } });
        }

        const float BloomGrow = 1.2f, BloomLife = 4.5f;

        void Grow(float dt)
        {
            for (int i = blooms.Count - 1; i >= 0; i--)
            {
                var b = blooms[i];
                b.t += dt;
                if (b.t > BloomLife || b.go == null) { Drop(b); blooms.RemoveAt(i); continue; }
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b.t / BloomGrow));
                Disc(b, b.radius * k);
            }
        }

        /// <summary>A ground-hugging disc with a ragged edge, in the box's grass colours; rebuilt as it spreads (tiny).</summary>
        void Disc(Bloom b, float r)
        {
            const int Seg = 20, Rings = 3;
            var land = box.land;
            var co = land.Colors;
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            verts.Add(land.OnGround(b.at.x, b.at.z) + Vector3.up * 0.006f); cols.Add(co.grassC);
            for (int ring = 1; ring <= Rings; ring++)
                for (int s = 0; s < Seg; s++)
                {
                    float a = s / (float)Seg * Mathf.PI * 2f;
                    float rr = r * ring / Rings * (ring == Rings ? 0.75f + 0.5f * Land.Hash(s, (int)(b.at.x * 100)) : 1f);
                    float x = b.at.x + Mathf.Cos(a) * rr, z = b.at.z + Mathf.Sin(a) * rr;
                    verts.Add(land.OnGround(x, z) + Vector3.up * 0.006f);
                    cols.Add(Color.Lerp(co.grassC, co.grassA, ring / (float)Rings));
                }
            for (int s = 0; s < Seg; s++) { tris.Add(0); tris.Add(1 + (s + 1) % Seg); tris.Add(1 + s); }
            for (int ring = 1; ring < Rings; ring++)
                for (int s = 0; s < Seg; s++)
                {
                    int a0 = 1 + (ring - 1) * Seg + s, a1 = 1 + (ring - 1) * Seg + (s + 1) % Seg;
                    int b0 = a0 + Seg, b1 = a1 + Seg;
                    tris.Add(a0); tris.Add(a1); tris.Add(b1); tris.Add(a0); tris.Add(b1); tris.Add(b0);
                }
            b.mesh.Clear();
            b.mesh.SetVertices(verts); b.mesh.SetColors(cols); b.mesh.SetTriangles(tris, 0); b.mesh.RecalculateNormals();
            b.go.GetComponent<MeshFilter>().sharedMesh = b.mesh;
        }

        static void Drop(Bloom b)
        {
            if (b.go != null) Destroy(b.go);
            if (b.mesh != null) Destroy(b.mesh);
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
