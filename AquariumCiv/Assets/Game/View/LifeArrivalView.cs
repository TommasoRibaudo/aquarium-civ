using System.Collections.Generic;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// P9 #424 (animation-specs B12, placed-life §2.4 and §6): the Keeper placed life, and it arrives at once while they watch.
    /// <list type="bullet">
    /// <item><b>The arrival</b>, an effect with no hand (B12.a–f): the thicket's seeds shower down and three bushes push up green; the
    /// rabbits drop lightly from the rim and shake off (RabbitView plays the pair itself); the deer step out of the trees and walk to the
    /// spot; the ducks glide down onto the pond with a skid; a bag floats on the pond, opens, and fish shadows dart out; two grey wolves
    /// trot out of the far wood at the den and lie down, and the first dusk after brings their howl. Deer and wolves are P0's
    /// <see cref="QuadDoll"/>; at the end beat they are handed to <see cref="DeerView"/> / <see cref="WolfView"/> where they stand (#425).</item>
    /// <item><b>The people react</b> (B12.0) within half a second: everyone awake within 2.5 u turns to it, the nearest grown-up points,
    /// one kid runs over and cheers, two or three walk over to watch, the chief looks up at the glass; for the wolves they only turn and
    /// stand still. At night the reaction waits for dawn (the arrival doesn't).</item>
    /// <item><b>A line</b> in the people's words after the arrival's end beat; the sim already wrote the history line (<c>life_*</c>).</item>
    /// <item><b>The waiting hint</b> (B12.w): while a jar is ready the background-life director may start a hint scene, once per visit per
    /// jar, never for the wolves (<see cref="HintWanted"/>; StoryDirectorView casts it, LifePlanner.StartStoryHint plays it).</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(7)]
    public class LifeArrivalView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static LifeArrivalView Instance { get; private set; }

        /// <summary>B12.0: people this near the spot react (box units); the reaction lasts this long (real seconds).</summary>
        public const float ReactRadius = 2.5f, ReactSeconds = 4f;

        /// <summary>The proposed lines, one per species, in the people's words (closed world: the gift comes from the sky). The user signs these off.</summary>
        public static readonly string[] Lines =
        {
            "Seeds fell out of the sky, and bushes stood up out of the grass.",   // thicket
            "The sky sent game.",                                                  // rabbits
            "Deer, out of the trees. Walk softly.",                                // deer
            "Two birds came down onto our water, and they stay.",                  // ducks
            "Something moves under the water. Fish!",                              // fish
            "Grey ones, at the far trees. Keep the little ones close.",            // wolves
        };

        /// <summary>How long each arrival plays to its end beat (real seconds): then the line.</summary>
        public static float Length(LifeSpecies s) => s switch
        {
            LifeSpecies.Thicket => 3.2f, LifeSpecies.Rabbits => RabbitView.SignSeconds, LifeSpecies.Deer => 6.5f,
            LifeSpecies.Ducks => 3.6f, LifeSpecies.Fish => 6.2f, _ => 6f,
        };

        sealed class Arrival
        {
            public LifeSpecies s; public int cell; public Vector3 at; public float t, length; public bool replay;
            public bool reacted, lined; public float reactAt = -1f;
            public Transform root;
            public readonly List<int> bushes = new();
            public readonly List<Transform> parts = new();
            public readonly List<QuadDoll> quads = new();
            public readonly List<Vector3> from = new(), to = new();
            public bool unhid, landed, opened;
        }

        BoxView box;
        SimCore bound;
        readonly List<Arrival> playing = new(), dawn = new();
        readonly HashSet<LifeSpecies> hinted = new();
        readonly List<PersonRef> whoBuf = new(); readonly List<float> px = new(), pz = new();
        readonly List<(PersonRef who, float until)> looking = new();
        Material glass, shadow;
        static Mesh seedMesh, bagMesh, fishMesh, wingMesh;
        float howlT = -1f; Arrival howlShow;

        // ---------------------------------------------------------------- for tests and the panel
        public int Played { get; private set; }
        public int PlayedOf(LifeSpecies s) => playedOf[(int)s];
        readonly int[] playedOf = new int[6];
        public bool Playing(LifeSpecies s) { foreach (var a in playing) if (a.s == s && a.t < a.length) return true; return false; }
        public bool AnyPlaying => playing.Count > 0;
        public LifeSpecies LastSpecies { get; private set; }
        /// <summary>Unscaled time the last arrival started, and when its people reacted (-1 until they do).</summary>
        public float LastStartAt { get; private set; } = -1f;
        public float LastReactAt { get; private set; } = -1f;
        /// <summary>The people sent by the last reaction: who turned, who points, who walks over.</summary>
        public int LastTurned { get; private set; }
        public int LastWalkers { get; private set; }
        public PersonRef LastPointer { get; private set; }
        public PersonRef LastRunner { get; private set; }
        public string LastLine { get; private set; } = "";
        public int Howls { get; private set; }
        /// <summary>First-dusk howls played by themselves (B12.f end beat).</summary>
        public int FirstHowls { get; private set; }
        public int HintsPlayed { get; private set; }
        /// <summary>Hint scenes the camera leaned in on.</summary>
        public int HintLeans { get; private set; }
        public string LastHint { get; private set; } = "";
        /// <summary>The arrival's pieces drawn now (bushes, dolls, the bag, shadows): the shots and tests look for them.</summary>
        public int PiecesShown { get { int n = 0; foreach (var a in playing) if (a.root != null && a.root.gameObject.activeInHierarchy) n += a.root.childCount; return n; } }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { PondLife.HoldDucksUntil = -1f; }

        public static LifeArrivalView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<LifeArrivalView>();
            return v != null ? v : b.gameObject.AddComponent<LifeArrivalView>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Ensure(FindAnyObjectByType<BoxView>());
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            box = GetComponent<BoxView>();
            if (GameClock.Instance != null) GameClock.Instance.CaughtUp += OnCaughtUp;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (GameClock.Instance != null) GameClock.Instance.CaughtUp -= OnCaughtUp;
            Bind(null);
            for (int i = playing.Count - 1; i >= 0; i--) End(playing[i], false);
            playing.Clear();
            if (howlShow != null) { End(howlShow, false); howlShow = null; }
            if (glass != null) Destroy(glass);
            if (shadow != null) Destroy(shadow);
        }

        /// <summary>A new visit (the box opened after time away): each ready jar may hint once more.</summary>
        void OnCaughtUp(CatchUpResult r) => hinted.Clear();

        void Bind(SimCore sim)
        {
            if (sim == bound) return;
            if (bound != null) bound.LifePlaced -= OnPlaced;
            bound = sim;
            if (sim != null) sim.LifePlaced += OnPlaced;
        }

        /// <summary>The Keeper's tap: the arrival starts on the same frame (B12.0 Contact), before the view's refresh draws the group.</summary>
        void OnPlaced(LifePlacedInfo info)
        {
            if (bound == null || bound.CatchingUp) return; // a placing while away can't happen (the jar waits), but keep the pending path
            Begin(info.species, info.cell, false);
        }

        void Update()
        {
            var sim = Sim;
            Bind(sim != null && sim.EcologyOn ? sim : null);
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null) return;
            // an arrival that hasn't played (placed by a cheat, or before this view was up)
            foreach (var s in LifeJar.All)
                if (sim.LifeArrivalPending(s) && !Has(s)) Begin(s, sim.LifePlacedCell(s), false);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            TryLean();
            if (Cam != null) WatchCancels(Cam);
            var pv = PeopleView.Find();
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                var a = playing[i];
                a.t += dt;
                Step(a, dt);
                if (!a.reacted) TryReact(a, pv);
                if (!a.lined && a.reacted && a.t >= a.length && Time.unscaledTime >= a.reactAt + 1.5f) { a.lined = true; Say(a, pv); }
                if (Done(a)) { if (!a.reacted) dawn.Add(a); End(a); playing.RemoveAt(i); }
            }
            // an arrival at night: the people see it at dawn (B12.0), then say the line
            if (dawn.Count > 0 && pv != null && pv.Planner != null && LifeClock.PhaseOf(pv.ViewHour) != DayPhase.Night)
            {
                foreach (var a in dawn) { TryReact(a, pv); a.lined = true; Say(a, pv); }
                dawn.Clear();
            }
            for (int i = looking.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= looking[i].until) { pv?.ClearLookAt(looking[i].who); looking.RemoveAt(i); }
            Howl(sim, pv, dt);
        }

        // ---------------------------------------------------------------- the lean (user 2026-11-08: "the camera leans in")
        Vector3 leanAt; float leanUntil = -1f, hintLeanUntil = -1f; LifeSpecies leanFor; bool leanReplay, leanRefused; int cancelsSeen;
        /// <summary>Camera leans made for the ghost, arrivals and hints (tests).</summary>
        public int Nudges { get; private set; }
        /// <summary>The player cancelled a lean (two fingers): no more leans until the life tool is put down and the arrivals are over.</summary>
        public bool LeanRefused => leanRefused;

        /// <summary>How close the camera leans for each group (BoxCamera zoom; 1 = the whole box): close enough that the group reads at phone
        /// size (rabbits and ducks are ~0.05 u), never closer than the camera's floor. Animals stay true to scale.</summary>
        public static float LeanZoom(LifeSpecies s) => s switch
        {
            LifeSpecies.Thicket => 0.36f, LifeSpecies.Rabbits => 0.2f, LifeSpecies.Deer => 0.32f,
            LifeSpecies.Ducks => 0.2f, LifeSpecies.Fish => 0.22f, _ => 0.32f,
        };
        /// <summary>A waiting hint's lean: the people are the scene, so a little wider; held this long at most (real seconds).</summary>
        public const float HintZoom = 0.42f, HintHoldMax = 10f;

        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;

        /// <summary>What keeps a lean: the ghost on the land, an arrival playing (and its line), a hint scene.</summary>
        bool HoldLean()
        {
            var lp = LifePlaceView.Instance;
            return (lp != null && lp.GhostShown) || playing.Count > 0 || Time.unscaledTime < hintLeanUntil;
        }

        /// <summary>The ghost was shown or moved (LifePlaceView): lean in on it, box-local <paramref name="at"/>.</summary>
        public bool LeanOnGhost(LifeSpecies s, Vector3 at)
        {
            if (box == null) return false;
            leanAt = box.transform.TransformPoint(at); leanFor = s; leanReplay = false; leanUntil = Time.unscaledTime + 1.2f;
            return TryLeanNow();
        }

        void TryLean()
        {
            if (Time.unscaledTime > leanUntil) return;
            var cam = Cam;
            if (cam == null) return;
            WatchCancels(cam);
            if (leanRefused || (leanReplay && !GlassVoice.CameraFree())) { leanUntil = -1f; return; }
            if (cam.LeanIn(leanAt, LeanZoom(leanFor), HoldLean)) { Nudges++; leanUntil = -1f; }
        }

        bool TryLeanNow() { TryLean(); return leanUntil < 0f; }

        void WatchCancels(BoxCamera cam)
        {
            if (cam.LeanCancels != cancelsSeen) { cancelsSeen = cam.LeanCancels; leanRefused = true; }
            var hud = GameUI.GameHud.Instance;
            bool inHand = hud != null && hud.Hand == GameUI.GameHud.KitTool.Life;
            if (leanRefused && !inHand && playing.Count == 0 && !cam.Leaning) leanRefused = false; // the next placing may lean again
        }

        bool Has(LifeSpecies s) { foreach (var a in playing) if (a.s == s) return true; return false; }

        bool Done(Arrival a)
        {
            const float stay = 0.5f; // deer and wolves are handed to their everyday views at the end beat (#425)
            return a.t >= a.length + stay && (a.lined || !a.reacted || a.t > a.length + 30f);
        }

        // ---------------------------------------------------------------- starting one

        /// <summary>The debug panel's "replay arrival X": plays the placed species' arrival again at its spot (no sim change). What happened.</summary>
        public string Replay(LifeSpecies s)
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn) return "no living box";
            if (!sim.LifeIsPlaced(s)) return $"{s}: not placed yet (place it first)";
            int cell = s == LifeSpecies.Wolves && sim.WolfDen >= 0 ? sim.WolfDen : sim.LifePlacedCell(s);
            if (cell < 0) return $"{s}: no spot";
            for (int i = playing.Count - 1; i >= 0; i--) if (playing[i].s == s) { End(playing[i]); playing.RemoveAt(i); }
            Begin(s, cell, true);
            return $"{s}: arrival replayed at cell {cell}";
        }

        void Begin(LifeSpecies s, int cell, bool replay)
        {
            var sim = Sim;
            if (sim == null || box == null || box.Root == null || cell < 0) return;
            sim.AckLifeArrival(s);
            if (s == LifeSpecies.Wolves && sim.WolfDen >= 0) cell = sim.WolfDen;
            var a = new Arrival { s = s, cell = cell, length = Length(s), replay = replay };
            a.at = Ground(SiteMap.CXc(cell), SiteMap.CZc(cell), s is LifeSpecies.Ducks or LifeSpecies.Fish);
            a.root = new GameObject("Arrival_" + s) { hideFlags = HideFlags.DontSave }.transform;
            a.root.SetParent(box.transform, false); // not the Generated root: a rebuild (the thicket's reserved ground) must not take it
            playing.Add(a);
            Played++; playedOf[(int)s]++; LastSpecies = s; LastStartAt = Time.unscaledTime; LastReactAt = -1f;
            switch (s)
            {
                case LifeSpecies.Thicket: SetupThicket(a, sim); break;
                case LifeSpecies.Rabbits:
                    var rv = RabbitView.Instance;
                    if (rv != null && (replay || !sim.EcoRabbitArrivalPending)) rv.ReplayDrop(); // else RabbitView starts the pair itself this frame
                    break;
                case LifeSpecies.Deer: SetupDeer(a); break;
                case LifeSpecies.Ducks: SetupDucks(a); break;
                case LifeSpecies.Fish: SetupFish(a); break;
                case LifeSpecies.Wolves:
                    SetupWolves(a); // the first dusk's howl is the sim's (WolfFirstHowlDue, saved)
                    break;
            }
            // B12.0 + user 2026-11-08: the camera leans in close and stays through the arrival; tried for a second (the finger that placed
            // it may still hold the camera)
            leanAt = box.transform.TransformPoint(a.at); leanFor = s; leanUntil = Time.unscaledTime + 1.2f; leanReplay = replay;
            TryLean();
            Sfx.Play(s is LifeSpecies.Ducks or LifeSpecies.Fish ? CueLibrary.FirstWaterDrop : CueLibrary.Pluck, 0f, 0.7f);
        }

        Vector3 Ground(float x, float z, bool water)
        {
            var g = box.land.OnGround(x, z);
            if (water) g.y = Mathf.Max(g.y, WaterY);
            return g;
        }

        float WaterY => box.layout != null && box.layout.eco != null ? box.layout.eco.waterY : Land.WaterY;

        void End(Arrival a, bool refresh = true)
        {
            if (a.bushes.Count > 0 && !a.unhid) { box.HideBushes(a.bushes, false); if (refresh) box.Refresh(); a.unhid = true; } // not while the scene goes (a refresh then builds a new root)
            if (a.s == LifeSpecies.Ducks) PondLife.HoldDucksUntil = -1f;
            if (a.root != null) Destroy(a.root.gameObject);
            a.root = null;
        }

        Transform Piece(Arrival a, string name, Mesh m, Material mat)
        {
            var g = new GameObject(name) { hideFlags = HideFlags.DontSave };
            g.transform.SetParent(a.root, false);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            a.parts.Add(g.transform);
            return g.transform;
        }

        Material See(Color c)
        {
            var baseMat = Resources.Load<Material>("LifeDim");
            var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("AquariumCiv/View/LifeDim"));
            m.hideFlags = HideFlags.DontSave;
            m.SetColor("_Color", c);
            m.renderQueue = 3001;
            return m;
        }

        Material Glass => glass != null ? glass : glass = See(new Color(0.80f, 0.93f, 1f, 0.45f));
        Material Shadow => shadow != null ? shadow : shadow = See(new Color(0.10f, 0.14f, 0.16f, 0.55f));

        // ---------------------------------------------------------------- B12.a the thicket
        void SetupThicket(Arrival a, SimCore sim)
        {
            // the founders: the bush cells nearest the spot (the sim put them there, #418)
            var near = new List<(float d, int c)>();
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (sim.EcoCoverAt(c) != EcoCover.Bush) continue;
                float dx = SiteMap.CXc(c) - SiteMap.CXc(a.cell), dz = SiteMap.CZc(c) - SiteMap.CZc(a.cell);
                float d = dx * dx + dz * dz;
                if (d < 0.6f * 0.6f) near.Add((d, c));
            }
            near.Sort((p, q) => p.d.CompareTo(q.d));
            for (int i = 0; i < near.Count && i < 3; i++) a.bushes.Add(near[i].c);
            if (a.bushes.Count > 0) box.HideBushes(a.bushes, true); // the chunk leaves them out while they push up
            if (seedMesh == null)
            {
                var mb = new MeshBuilder(4241) { jitter = 0f };
                mb.Ico(Vector3.zero, 0.009f, new Vector3(1f, 0.7f, 1f), new Color(0.55f, 0.40f, 0.22f), 0.1f);
                seedMesh = mb.ToMesh("Seed");
            }
            var rnd = new System.Random(a.cell * 31 + 7);
            for (int i = 0; i < 16; i++)
            {
                var p = Piece(a, "Seed", seedMesh, box.SolidMaterial);
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.04f + 0.2f * (float)rnd.NextDouble();
                var land = a.at + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                land.y = box.land.OnGround(land.x, land.z).y;
                a.to.Add(land);
                a.from.Add(land + new Vector3(0f, 0.55f + 0.25f * (float)rnd.NextDouble(), 0f));
                p.localPosition = a.from[i];
            }
        }

        const float SeedFall = 0.7f, BushGap = 0.3f, BushGrow = 0.5f;

        void StepThicket(Arrival a)
        {
            // the seeds shower down and sink into the soil
            int seeds = a.from.Count;
            for (int i = 0; i < seeds; i++)
            {
                var p = a.parts[i];
                float delay = (i % 5) * 0.06f, u = Mathf.Clamp01((a.t - delay) / SeedFall);
                p.localPosition = Vector3.Lerp(a.from[i], a.to[i], u * u);
                p.localScale = Vector3.one * (u >= 1f ? Mathf.Clamp01(1f - (a.t - delay - SeedFall) / 0.4f) : 1f);
                p.gameObject.SetActive(p.localScale.x > 0.01f);
            }
            // the bushes push up one after another: Dust at the base, 0 -> 1.15 -> 1 (Back), Leaves on top, one sway
            for (int i = 0; i < a.bushes.Count; i++)
            {
                int c = a.bushes[i];
                float t0 = SeedFall + i * BushGap;
                if (a.parts.Count <= seeds + i)
                {
                    if (a.t < t0) continue;
                    if (!box.BushSpots.TryGetValue(c, out var spot)) spot = (box.land.OnGround(SiteMap.CXc(c), SiteMap.CZc(c)), 0.075f);
                    var mb = new MeshBuilder(c * 7 + 1) { jitter = 0.03f };
                    Pieces.Bush(mb, Vector3.zero, spot.r, false);
                    var p = Piece(a, "Bush", mb.ToMesh("ArrivalBush"), box.SolidMaterial);
                    p.localPosition = spot.pos;
                    p.localScale = Vector3.zero;
                    if (HitFx.Enabled) HitFx.Puff(PuffKind.Dust, box.Root.TransformPoint(spot.pos), Vector3.up, 0.8f, true);
                    Sfx.Play(CueLibrary.Pluck, 2f * i, 0.5f);
                }
                var b = a.parts[seeds + i];
                float u = Mathf.Clamp01((a.t - t0) / BushGrow);
                float k = BackOut(u, 1.15f);
                float sway = u >= 1f ? Mathf.Sin((a.t - t0 - BushGrow) * 9f) * 6f * Mathf.Exp(-(a.t - t0 - BushGrow) * 3f) : 0f;
                b.localScale = new Vector3(Mathf.Max(0f, k), Mathf.Max(0f, k), Mathf.Max(0f, k));
                b.localRotation = Quaternion.Euler(0f, 0f, sway);
                if (u >= 1f && b.name == "Bush")
                {
                    b.name = "Bush grown";
                    if (HitFx.Enabled) HitFx.Puff(PuffKind.Leaves, box.Root.TransformPoint(b.localPosition + Vector3.up * 0.08f), Vector3.up, 0.6f, true);
                }
            }
            // the end beat: the real bushes come back with their berries (BushView pops the dots on)
            float grown = SeedFall + Mathf.Max(0, a.bushes.Count - 1) * BushGap + BushGrow + 0.4f;
            if (!a.unhid && a.t >= grown && a.bushes.Count > 0)
            {
                a.unhid = true;
                box.HideBushes(a.bushes, false);
                box.Refresh();
                for (int i = seeds; i < a.parts.Count; i++) a.parts[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Back-out ease to a peak of <paramref name="peak"/> then settling at 1.</summary>
        static float BackOut(float u, float peak)
        {
            if (u <= 0f) return 0f;
            if (u < 0.7f) { float v = u / 0.7f; return peak * (1f - (1f - v) * (1f - v)); }
            return Mathf.Lerp(peak, 1f, Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f));
        }

        // ---------------------------------------------------------------- B12.c the deer, B12.f the wolves (placeholder dolls)
        Vector3 AwayFromCamera(Vector3 at, float dist)
        {
            var cam = Camera.main;
            var dir = cam != null ? box.Root.InverseTransformDirection(cam.transform.forward) : Vector3.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            var p = at + dir * dist;
            p.x = Mathf.Clamp(p.x, -Land.HX + 0.15f, Land.HX - 0.15f);
            p.z = Mathf.Clamp(p.z, -Land.HZ + 0.15f, Land.HZ - 0.15f);
            return box.land.OnGround(p.x, p.z);
        }

        Vector3 Fire()
        {
            var st = bound != null ? bound.Settlement : null;
            if (st == null) return Vector3.zero;
            st.Fire(out float fx, out float fz, out _);
            return new Vector3(fx, 0f, fz);
        }

        float YawToCamp(Vector3 from)
        {
            var f = Fire();
            return Mathf.Atan2(f.x - from.x, f.z - from.z) * Mathf.Rad2Deg;
        }

        void SetupDeer(Arrival a)
        {
            // out of the trees nearest the spot, or the box's back edge if no tree is within 1 u
            Vector3 edge = AwayFromCamera(a.at, 0.9f);
            float best = 1f;
            lock (box.BushSpots)
                foreach (var kv in box.TreeSpots)
                {
                    float d = (new Vector2(kv.Value.pos.x, kv.Value.pos.z) - new Vector2(a.at.x, a.at.z)).magnitude;
                    if (d < best && d > 0.25f) { best = d; edge = kv.Value.pos; }
                }
            var g = LifeLooks.Group(LifeSpecies.Deer);
            for (int i = 0; i < g.Length; i++)
            {
                var q = new QuadDoll(g[i].quad, a.root, box.SolidMaterial);
                a.quads.Add(q);
                float tx = a.at.x + g[i].at.x, tz = a.at.z + g[i].at.y;
                TreeCrowns.Push(box, bound, ref tx, ref tz, 0.07f); // #425: never under a crown
                var to = Ground(tx, tz, false);
                a.to.Add(to);
                a.from.Add(Ground(edge.x + g[i].at.x * 0.5f, edge.z + g[i].at.y * 0.5f, false));
            }
        }

        void StepQuads(Arrival a, bool wolves)
        {
            if (a.landed) return;
            if (a.t >= a.length)
            {   // #425: the end beat hands them to their everyday view where they stand (DeerView / WolfView draw them from now)
                a.landed = true;
                var hand = wolves ? WolfView.HandOff : DeerView.HandOff;
                hand.Clear();
                foreach (var q in a.quads) { hand.Add((q.go.transform.localPosition, q.go.transform.localEulerAngles.y)); q.SetActive(false); }
                return;
            }
            for (int i = 0; i < a.quads.Count; i++)
            {
                var q = a.quads[i];
                Vector3 from = a.from[i], to = a.to[i];
                float yawIn = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
                float yawCamp = YawToCamp(to);
                float delay = wolves ? i * 0.35f : i == 0 ? 0f : i == 1 ? 1.0f : 1.6f;
                float walk = wolves ? 1.3f : 2.6f;
                float t = a.t - delay;
                float alertAt = walk, settle = walk + (wolves ? 2.6f : 1.5f);
                if (t < 0f) { q.Pose(from, yawIn, float.NaN, 0f, 0f, 0f, 0f, 0f, 0f, 0f); continue; } // not out yet
                if (t < walk)
                {
                    float u = t / walk;
                    var p = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, u));
                    float scale = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.4f)); // out of the trees' shade
                    q.Pose(p, yawIn, t * (wolves ? 16f : 10f), wolves ? 1f : 0f, wolves ? 8f : -5f, 0f, 0f, 0f, wolves ? 0f : 10f, scale);
                    continue;
                }
                if (t < settle)
                {
                    float u = t - alertAt;
                    if (wolves)
                    {   // sniff the air (muzzle up, two nods), then look toward the camp
                        float sniff = u < 1f ? -25f + Mathf.Sin(u * Mathf.PI * 4f) * 8f : 0f;
                        float look = u < 1f ? 0f : Mathf.Clamp(Mathf.DeltaAngle(yawIn, yawCamp), -60f, 60f);
                        q.Pose(to, yawIn, float.NaN, 0f, sniff, look, 0f, 0f, 0f);
                    }
                    else
                    {   // Alert: head up and turned (ears turning), tail flag up
                        float look = Mathf.Clamp(Mathf.DeltaAngle(yawIn, yawCamp), -70f, 70f) * Mathf.Sin(Mathf.Clamp01(u / 0.25f) * Mathf.PI * 0.5f);
                        q.Pose(to, yawIn, float.NaN, 0f, -18f, look + Mathf.Sin(u * 5f) * 6f, 0f, 0f, 40f);
                    }
                    continue;
                }
                float v = t - settle;
                if (wolves) q.Pose(to, yawIn, float.NaN, 0f, 10f, 0f, Mathf.Clamp01(v / 0.6f), 0f, 0f);         // lie down at the den mouth
                else
                {
                    bool watcher = i == 1 && v < 2f;                                                              // one doe keeps watching the camp
                    float graze = watcher ? -18f : Mathf.Lerp(-18f, 55f, Mathf.SmoothStep(0f, 1f, v / 0.6f)) + Mathf.Sin(v * 3f) * 4f;
                    q.Pose(to, yawIn, float.NaN, 0f, graze, watcher ? Mathf.Clamp(Mathf.DeltaAngle(yawIn, yawCamp), -70f, 70f) : 0f, 0f, 0f, 10f);
                }
            }
        }

        void SetupWolves(Arrival a)
        {
            // at the den, two grey shapes trot 0.3 u out of the trees (away from the camp is "in the trees")
            var f = Fire();
            var outDir = new Vector3(a.at.x - f.x, 0f, a.at.z - f.z);
            if (outDir.sqrMagnitude < 1e-4f) outDir = Vector3.forward;
            outDir.Normalize();
            var g = LifeLooks.Group(LifeSpecies.Wolves);
            for (int i = 0; i < g.Length; i++)
            {
                var q = new QuadDoll(QuadLook.Of(QuadKind.Wolf, i), a.root, box.SolidMaterial);
                a.quads.Add(q);
                var side = new Vector3(-outDir.z, 0f, outDir.x) * g[i].at.x;
                float tx = a.at.x + side.x, tz = a.at.z + side.z;
                TreeCrowns.Push(box, bound, ref tx, ref tz, 0.06f); // #425: at the den mouth, not under the crowns
                var to = Ground(tx, tz, false);
                a.to.Add(to);
                var from = to + outDir * 0.35f;
                a.from.Add(Ground(from.x, from.z, false));
            }
        }

        /// <summary>B12.f end beat: the first dusk after the wolves were placed, they howl (B21.a plays it in full later, #441). The sound is a
        /// CC0 recording (Freesound 519592, assets-log).</summary>
        void Howl(SimCore sim, PeopleView pv, float dt)
        {
            if (howlShow != null)
            {
                howlT += dt;
                for (int i = 0; i < howlShow.quads.Count; i++)
                {
                    float u = howlT - i * 0.6f;
                    float sit = Mathf.Clamp01(u / 0.4f) * Mathf.Clamp01((4.5f - howlT) / 0.5f);
                    howlShow.quads[i].Pose(howlShow.to[i], YawToCamp(howlShow.to[i]) + 150f, float.NaN, 0f, -70f * sit, 0f, 0f, sit, 0f);
                }
                if (howlT > 5f) { End(howlShow); howlShow = null; }
                return;
            }
            if (pv == null || !sim.WolfFirstHowlDue) return;
            float hour = pv.ViewHour;
            bool dusk = hour >= 18.5f && hour < 21.5f;
            if (!dusk || Has(LifeSpecies.Wolves)) return; // the arrival is still playing: the howl comes after
            if (sim.AckFirstHowl()) { DebugHowl(); FirstHowls++; }
        }

        /// <summary>The debug panel's "howl now" (and what the first dusk calls): two wolves sit at the den and howl.</summary>
        public string DebugHowl()
        {
            var sim = Sim;
            if (sim == null || box == null || box.Root == null) return "no living box";
            int den = sim.WolfDen >= 0 ? sim.WolfDen : sim.LifePlacedCell(LifeSpecies.Wolves);
            if (den < 0) return "no wolves' den";
            if (howlShow != null) { End(howlShow); howlShow = null; }
            var wv = WolfView.Instance;
            var denAt = Ground(SiteMap.CXc(den), SiteMap.CZc(den), false);
            if (wv != null && sim.WolvesPlaced) wv.ForceHowl(5f); // #425: the pack itself sits and howls
            else
            {
                howlShow = new Arrival { s = LifeSpecies.Wolves, cell = den, at = denAt };
                howlShow.root = new GameObject("Howl") { hideFlags = HideFlags.DontSave }.transform;
                howlShow.root.SetParent(box.transform, false);
                SetupWolves(howlShow);
                howlT = 0f;
            }
            Howls++;
            Sfx.Play(CueLibrary.WolfHowl, 0f, 1f);
            Sfx.Play(CueLibrary.WolfHowl, -2f, 0.8f, 0.7f);
            // the people look toward the far wood a beat
            var pv = PeopleView.Find();
            if (pv != null && pv.Planner != null)
            {
                pv.StandingNow(whoBuf, px, pz);
                for (int i = 0; i < whoBuf.Count; i++) { pv.LookAt(whoBuf[i], new Vector2(denAt.x, denAt.z)); looking.Add((whoBuf[i], Time.unscaledTime + 3f)); }
            }
            return $"the wolves howl at the den (cell {den})";
        }

        // ---------------------------------------------------------------- B12.d the ducks
        void SetupDucks(Arrival a)
        {
            PondLife.HoldDucksUntil = Time.unscaledTime + Length(LifeSpecies.Ducks) + 0.2f;
            PondLife.DuckMeshes(out var body, out var head, out var tail);
            if (wingMesh == null)
            {
                var mb = new MeshBuilder(4242) { jitter = 0.02f };
                mb.Ico(new Vector3(0.022f, 0f, 0f), 0.022f, new Vector3(1f, 0.15f, 0.55f), new Color(0.42f, 0.32f, 0.21f), 0.05f);
                wingMesh = mb.ToMesh("DuckWing");
            }
            var g = LifeLooks.Group(LifeSpecies.Ducks);
            var away = AwayFromCamera(a.at, 0.7f) - a.at; away.y = 0f;
            for (int i = 0; i < g.Length; i++)
            {
                var d = new GameObject("Duck") { hideFlags = HideFlags.DontSave }.transform;
                d.SetParent(a.root, false);
                a.parts.Add(d);
                Sub(d, body, Vector3.zero); Sub(d, head, PondLife.DuckHeadAt); Sub(d, tail, PondLife.DuckTailAt);
                Sub(d, wingMesh, new Vector3(0.012f, 0.026f, 0f)).name = "WingR";
                var wl = Sub(d, wingMesh, new Vector3(-0.012f, 0.026f, 0f)); wl.name = "WingL"; wl.localScale = new Vector3(-1f, 1f, 1f);
                var to = Ground(a.at.x + g[i].at.x, a.at.z + g[i].at.y, true) + Vector3.up * 0.004f;
                a.to.Add(to);
                a.from.Add(to + away + Vector3.up * 0.5f + new Vector3(0f, i * 0.05f, 0f));
            }
        }

        Transform Sub(Transform parent, Mesh m, Vector3 at)
        {
            var g = new GameObject("Part") { hideFlags = HideFlags.DontSave };
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = box.SolidMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        const float Glide = 1.2f, Skid = 0.35f;

        void StepDucks(Arrival a)
        {
            for (int i = 0; i < a.parts.Count; i++)
            {
                var d = a.parts[i];
                float t = a.t - i * 0.25f;
                Vector3 from = a.from[i], to = a.to[i];
                var flat = to - from; flat.y = 0f;
                var dir = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
                var skidEnd = to + dir * 0.15f;
                Transform wr = d.Find("WingR"), wl = d.Find("WingL");
                float flap = 0f;
                if (t < Glide)
                {   // a shallow glide down (Out), feet forward, a flap or two
                    float u = Mathf.Clamp01(t / Glide), e = 1f - (1f - u) * (1f - u);
                    d.localPosition = Vector3.Lerp(from, to, e);
                    d.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-12f * (1f - u), 0f, 0f);
                    flap = 50f * Mathf.Sin(t * 14f) * (u < 0.3f || u > 0.8f ? 1f : 0.3f);
                    d.gameObject.SetActive(true);
                }
                else if (t < Glide + Skid)
                {   // lands with a skid along the water: rings and droplets
                    float u = (t - Glide) / Skid;
                    d.localPosition = Vector3.Lerp(to, skidEnd, 1f - (1f - u) * (1f - u));
                    d.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-10f * (1f - u), 0f, 0f);
                    flap = 70f * (1f - u);
                    if (d.name == "Duck")
                    {
                        d.name = "Duck landed";
                        if (HitFx.Enabled)
                        {
                            var w = box.Root.TransformPoint(to);
                            HitFx.Puff(PuffKind.Ring, w, Vector3.up, 1f, true);
                            HitFx.Puff(PuffKind.Droplets, w, Vector3.up, 0.9f, true);
                        }
                    }
                }
                else
                {   // a tail shake, then a slow paddle round
                    float u = t - Glide - Skid;
                    float ang = u * 0.9f * (i == 0 ? 1f : -1f);
                    var c = skidEnd + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang) - 1f) * 0.05f;
                    d.localPosition = c + Vector3.up * Mathf.Sin(u * 2f) * 0.0015f;
                    d.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
                    var tail = d.childCount > 2 ? d.GetChild(2) : null;
                    if (tail != null) tail.localRotation = Quaternion.Euler(0f, u < 0.5f ? Mathf.Sin(u * 40f) * 25f : 0f, 0f);
                }
                if (wr != null) wr.localRotation = Quaternion.Euler(0f, 0f, flap);
                if (wl != null) wl.localRotation = Quaternion.Euler(0f, 0f, -flap);
                bool winged = t < Glide + Skid;
                if (wr != null) wr.gameObject.SetActive(winged);
                if (wl != null) wl.gameObject.SetActive(winged);
            }
            // the hand-over: the pond's own ducks take over where these paddle
            if (a.t >= a.length - 0.1f && !a.landed)
            {
                a.landed = true;
                PondLife.DuckStarts.Clear();
                foreach (var d in a.parts) PondLife.DuckStarts.Add(new Vector2(d.localPosition.x, d.localPosition.z));
                PondLife.HoldDucksUntil = -1f;
                foreach (var d in a.parts) d.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- B12.e the fish
        void SetupFish(Arrival a)
        {
            if (bagMesh == null)
            {
                var mb = new MeshBuilder(4243) { jitter = 0f };
                mb.Ico(new Vector3(0f, 0.035f, 0f), 0.06f, new Vector3(1f, 0.62f, 1f), Color.white, 0.08f);
                mb.Prism(new Vector3(0f, 0.07f, 0f), 0.012f, 0.02f, 0.02f, 6, Color.white); // the tied top
                bagMesh = mb.ToMesh("FishBag");
                var fb = new MeshBuilder(4244) { jitter = 0f };
                LifeLooks.FishShape(fb, 1f);
                fishMesh = fb.ToMesh("FishShadow");
            }
            var bag = Piece(a, "Bag", bagMesh, Glass);
            bag.localPosition = a.at + Vector3.up * 0.4f;
            for (int i = 0; i < 6; i++)
            {
                var f = Piece(a, "FishInBag", fishMesh, Shadow);
                f.localScale = Vector3.one * 0.55f;
            }
            for (int i = 0; i < 6; i++) { var f = Piece(a, "Shadow", fishMesh, Shadow); f.gameObject.SetActive(false); }
        }

        const float BagDrop = 0.45f, BagFloat = 3f, BagOpen = 0.2f, BagSink = 0.6f, FanOut = 1.5f;

        void StepFish(Arrival a)
        {
            var bag = a.parts[0];
            float t = a.t, wy = WaterY;
            Vector3 c = new Vector3(a.at.x, wy, a.at.z);
            float bob = 0f, sink = 0f;
            if (t < BagDrop) { float u = t / BagDrop; bag.localPosition = c + Vector3.up * (0.4f * (1f - u * u)); }
            else
            {
                if (!a.landed) { a.landed = true; if (HitFx.Enabled) HitFx.Puff(PuffKind.Ring, box.Root.TransformPoint(c), Vector3.up, 1f, true); }
                float u = t - BagDrop;
                bob = Mathf.Sin(u * Mathf.PI * 2f * 1.5f) * 0.01f + (u < 0.3f ? Mathf.Sin(u / 0.3f * Mathf.PI) * 0.03f : 0f); // Bounce, then the Wobble
                float sinkT = t - BagDrop - BagFloat - BagOpen;
                sink = Mathf.Clamp01(sinkT / BagSink);
                bag.localPosition = c + Vector3.up * (bob - sink * 0.09f - 0.02f);
                bag.localRotation = Quaternion.Euler(sink * 50f, 0f, 0f);
                float open = Mathf.Clamp01((t - BagDrop - BagFloat) / BagOpen);
                bag.localScale = new Vector3(1f + open * 0.15f, 1f - open * 0.2f, 1f + open * 0.15f) * (1f - sink * 0.6f);
                if (open > 0f && !a.opened) { a.opened = true; if (HitFx.Enabled) HitFx.Puff(PuffKind.Droplets, box.Root.TransformPoint(c), Vector3.up, 0.6f, true); }
            }
            bag.gameObject.SetActive(sink < 1f);
            // six small fish circling inside the bag
            for (int i = 0; i < 6; i++)
            {
                var f = a.parts[1 + i];
                float ang = t * 2.4f + i * Mathf.PI / 3f;
                f.localPosition = bag.localPosition + new Vector3(Mathf.Cos(ang) * 0.028f, 0.03f + (i % 2) * 0.012f, Mathf.Sin(ang) * 0.028f);
                f.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                f.gameObject.SetActive(t < BagDrop + BagFloat + BagOpen);
            }
            // then the shadows fan out under the water from the spot, with two rings, and fade
            float ft = t - (BagDrop + BagFloat + BagOpen);
            for (int i = 0; i < 6; i++)
            {
                var f = a.parts[7 + i];
                if (ft < 0f) { f.gameObject.SetActive(false); continue; }
                float ang = i * Mathf.PI / 3f + 0.3f, u = Mathf.Clamp01(ft / FanOut), e = 1f - (1f - u) * (1f - u);
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                f.localPosition = c + dir * (0.03f + 0.35f * e) + Vector3.up * 0.002f;
                f.localRotation = Quaternion.LookRotation(dir);
                f.localScale = Vector3.one * (0.9f + 0.2f * (i % 3)) * Mathf.Clamp01(1f - (ft - FanOut) / 0.8f);
                f.gameObject.SetActive(f.localScale.x > 0.02f);
                if (i < 2 && ft >= i * 0.4f && f.name == "Shadow")
                {
                    f.name = "Shadow rung";
                    if (HitFx.Enabled) HitFx.Puff(PuffKind.Ring, box.Root.TransformPoint(c + dir * 0.12f), Vector3.up, 0.8f, true);
                }
            }
        }

        void Step(Arrival a, float dt)
        {
            if (a.root == null) return;
            switch (a.s)
            {
                case LifeSpecies.Thicket: StepThicket(a); break;
                case LifeSpecies.Deer: StepQuads(a, false); break;
                case LifeSpecies.Wolves: StepQuads(a, true); break;
                case LifeSpecies.Ducks: StepDucks(a); break;
                case LifeSpecies.Fish: StepFish(a); break;
            }
        }

        // ---------------------------------------------------------------- the people (B12.0)
        void TryReact(Arrival a, PeopleView pv)
        {
            if (pv == null || pv.Planner == null) return;
            if (LifeClock.PhaseOf(pv.ViewHour) == DayPhase.Night) return; // waits for dawn (the arrival itself doesn't)
            a.reacted = true; a.reactAt = Time.unscaledTime; LastReactAt = a.reactAt;
            var spot = new Vector2(a.at.x, a.at.z);
            pv.StandingNow(whoBuf, px, pz);
            var order = new List<(float d, int i)>();
            for (int i = 0; i < whoBuf.Count; i++)
            {
                float d = (new Vector2(px[i], pz[i]) - spot).magnitude;
                if (d <= ReactRadius) order.Add((d, i));
            }
            order.Sort((p, q) => p.d.CompareTo(q.d));
            var planner = pv.Planner;
            var now = pv.Now;
            int turned = 0, walkers = 0;
            LastPointer = PersonRef.None; LastRunner = PersonRef.None;
            bool still = a.s == LifeSpecies.Wolves;
            int slot = 1;
            foreach (var (d, i) in order)
            {
                var who = whoBuf[i];
                var stage = planner.StageOf(who);
                if (stage == LifeStage.Baby || stage == LifeStage.None) continue;
                bool kid = stage == LifeStage.Kid;
                pv.LookAt(who, spot);
                looking.Add((who, Time.unscaledTime + 6f));
                turned++;
                if (still) { planner.React(KeeperGesture.ArrivalStill, now, default, who, 1f, ReactSeconds); continue; }
                if (!kid && LastPointer.IsNone) { LastPointer = who; planner.React(KeeperGesture.Sighting, now, default, who, 1f, 1.5f); continue; }
                if (kid && LastRunner.IsNone) { LastRunner = who; planner.React(KeeperGesture.Arrival, now, Place.Land(PlaceKind.Arrival, a.cell * 8), who, 1f, ReactSeconds); walkers++; continue; }
                if (walkers < 4 && slot <= 3) { planner.React(KeeperGesture.Arrival, now, Place.Land(PlaceKind.Arrival, a.cell * 8 + slot++), who, 1f, ReactSeconds); walkers++; }
            }
            // the chief looks up at the glass
            var sim = Sim;
            var chief = sim != null ? sim.HolderOf(SimCore.LeaderOf(sim.State.era)) : null;
            if (chief != null && !still)
            {
                var c = PersonRef.Notable(chief.id);
                if (c != LastPointer && c != LastRunner) planner.React(KeeperGesture.Glance, now, default, c, 1f, 2.5f);
            }
            LastTurned = turned; LastWalkers = walkers;
        }

        void Say(Arrival a, PeopleView pv)
        {
            var show = ShowMeDirector.Instance;
            string line = Lines[(int)a.s];
            LastLine = line;
            if (show == null || pv == null) return;
            pv.StandingNow(whoBuf, px, pz);
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < whoBuf.Count; i++)
            {
                float d = (px[i] - a.at.x) * (px[i] - a.at.x) + (pz[i] - a.at.z) * (pz[i] - a.at.z);
                if (pv.Planner != null && pv.Planner.StageOf(whoBuf[i]) is LifeStage.Baby) continue;
                if (whoBuf[i].IsNotable) d -= 100f; // a named person speaks if one is about
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0) show.SayLine(line, whoBuf[best], 4.5f);
        }

        // ---------------------------------------------------------------- the waiting hint (B12.w)

        /// <summary>The hint the director may start now: the first ready jar (not the wolves) that hasn't hinted this visit, its stage by
        /// how long it has waited, and its spot. False when there is none.</summary>
        public bool HintWanted(SimCore sim, out int species, out int stage, out int cell, out float x, out float z)
        {
            species = stage = 0; cell = -1; x = z = 0f;
            if (sim == null || !sim.EcologyOn || AnyPlaying) return false;
            foreach (var s in LifeJar.All)
            {
                if (s == LifeSpecies.Wolves || hinted.Contains(s) || !sim.JarReady(s)) continue;
                if (!HintSpot(sim, s, out cell, out x, out z)) continue;
                species = (int)s;
                long ready = sim.JarReadyMonth(s);
                stage = StoryRules.HintStage(ready >= 0 ? sim.State.month - ready : 0);
                return true;
            }
            return false;
        }

        /// <summary>Where a species' hint plays: the valid ground for it nearest the camp (the empty meadow, the wood's edge, the bank).</summary>
        public bool HintSpot(SimCore sim, LifeSpecies s, out int cell, out float x, out float z)
        {
            cell = sim != null ? sim.LifePlaceCellNearCamp(s) : -1;
            x = z = 0f;
            if (cell < 0) return false;
            x = SiteMap.CXc(cell); z = SiteMap.CZc(cell);
            return true;
        }

        /// <summary>The director started a hint: that jar won't hint again this visit.</summary>
        public void HintStarted(LifeSpecies s, int stage, int people, float seconds = 0f, int cell = -1)
        {
            // user 2026-11-08: the hint scenes lean in too (only when the camera is free: nobody holding it, no card, no moment)
            if (cell >= 0 && seconds > 0f && box != null && GlassVoice.CameraFree() && Cam != null && !Cam.Touching && !Cam.Leaning)
            {
                hintLeanUntil = Time.unscaledTime + Mathf.Min(seconds, HintHoldMax);
                var at = box.land.OnGround(SiteMap.CXc(cell), SiteMap.CZc(cell));
                WatchCancels(Cam);
                if (!leanRefused && Cam.LeanIn(box.transform.TransformPoint(at), HintZoom, HoldLean)) { Nudges++; HintLeans++; }
            }
            hinted.Add(s);
            HintsPlayed++;
            LastHint = $"{s} stage {stage} ({people})";
        }

        public bool HintedThisVisit(LifeSpecies s) => hinted.Contains(s);

        /// <summary>The panel's status line.</summary>
        public string Status()
        {
            string p = playing.Count == 0 ? "none playing" : string.Join(", ", playing.ConvertAll(a => $"{a.s} {a.t:0.0}/{a.length:0.0}s"));
            return $"arrivals: {p}; played {Played}, last {LastSpecies} (turned {LastTurned}, walkers {LastWalkers}); hints {HintsPlayed} ({LastHint}); howls {Howls}" +
                   (Sim != null && Sim.WolfFirstHowlDue ? ", first howl due at dusk" : "");
        }
    }
}
