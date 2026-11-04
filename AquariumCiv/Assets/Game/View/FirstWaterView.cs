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
    /// P8 #279 (keepers-hands 2 beat 6): the pond's first water is a moment. When water first breaks the surface in the hollow
    /// (<see cref="EcoMilestones.WaterSurfaced"/>):
    /// <list type="bullet">
    /// <item>a few small sparkles twinkle on the new water for a while, and a soft drop and bubble sound;</item>
    /// <item>the people near come over to the water's edge and look (<see cref="KeeperGesture.Marvel"/>), and one of them says a
    /// line in the era's words (the glass voice's bubble);</item>
    /// <item>when the camera is free (the player is watching and busy with nothing else) it leans in a little.</item>
    /// </list>
    /// If the water came while the player was away, a short version plays when they return: the sparkle, the sound, one line.
    /// Once per land. The sounds are the CC0 one-shots <c>first_water_drop</c> and <c>first_water_bubble</c>.
    /// </summary>
    public class FirstWaterView : MonoBehaviour
    {
        public static bool AutoCreate = true;
        public static FirstWaterView Instance { get; private set; }

        /// <summary>How long the full moment sparkles (real seconds), and the short one on return.</summary>
        public const float FullSeconds = 12f, AwaySeconds = 5f, FadeSeconds = 2.5f;
        /// <summary>The line comes once the people have had time to walk over.</summary>
        public const float LineAfter = 2.5f;
        const int MaxSparkles = 14;

        public static readonly string[] TribeLines =
        {
            "Water. It came up out of the ground, and it stays.",
            "Look at it. The hollow is wet and the wet is standing still.",
        };
        public static readonly string[] CampLines =
        {
            "The hollow holds water now. We can drink from it.",
            "Water in the low place. It stays, even in the sun.",
        };
        public static readonly string[] VillageLines =
        {
            "The pond is a pond. It was only mud once.",
        };
        public static readonly string[] AwayLines =
        {
            "While nobody watched, the hollow filled.",
            "Come and see. The low place has water in it now.",
        };

        BoxView box;
        SimCore bound;
        Transform holder;
        Transform rootSeen;
        readonly List<Transform> sparkles = new();
        readonly List<float> sparkleT = new(), sparkleLife = new();
        static Mesh sparkMesh;
        bool awayPending;
        float t = -1f, length, reactT, lineAt = -1f, waitPond;
        bool full, lined, leanPending;
        readonly List<PersonRef> whoBuf = new(); readonly List<float> px = new(), pz = new();

        /// <summary>For tests and the panel: how many times each version played, the last line, the camera leans.</summary>
        public int FullPlays { get; private set; }
        public int AwayPlays { get; private set; }
        public int Leans { get; private set; }
        public string LastLine { get; private set; } = "";
        public bool Playing => t >= 0f;
        public int SparklesShown { get; private set; }
        public int PeopleSent { get; private set; }

        SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : null;

        public static FirstWaterView Ensure(BoxView b)
        {
            if (b == null) return null;
            var v = b.GetComponent<FirstWaterView>();
            return v != null ? v : b.gameObject.AddComponent<FirstWaterView>();
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
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            Bind(null);
            if (holder != null) Destroy(holder.gameObject);
            holder = null; sparkles.Clear(); sparkleT.Clear(); sparkleLife.Clear(); t = -1f; awayPending = false;
        }

        void Bind(SimCore sim)
        {
            if (sim == bound) return;
            if (bound != null) bound.EcoMilestone -= OnMilestone;
            bound = sim;
            awayPending = false; t = -1f;
            if (sim != null) sim.EcoMilestone += OnMilestone;
        }

        void OnMilestone(string key)
        {
            if (key != EcoMilestones.WaterSurfaced || bound == null) return;
            if (bound.CatchingUp) awayPending = true; // the player was away: a short version when they are back
            else Begin(true);
        }

        void Update()
        {
            var sim = Sim;
            Bind(sim != null && sim.EcologyOn ? sim : null);
            if (sim == null || !sim.EcologyOn || box == null || box.Root == null) { Hide(); return; }
            if (rootSeen != box.Root) { holder = null; sparkles.Clear(); sparkleT.Clear(); sparkleLife.Clear(); rootSeen = box.Root; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (awayPending && !sim.CatchingUp) { awayPending = false; Begin(false); }
            if (t < 0f) return;
            var eco = box.layout.eco;
            if (eco.pondCells <= 0) // the view has not drawn the water yet; wait a moment for it
            {
                waitPond += dt;
                if (waitPond < 4f) return;
            }
            t += dt;
            if (leanPending) { if (Lean() || t > 3f) leanPending = false; }
            UpdatePeople(dt);
            UpdateSparkles(eco, dt);
            if (t >= length + FadeSeconds) { t = -1f; Hide(); }
        }

        void Hide() { if (holder != null) holder.gameObject.SetActive(false); SparklesShown = 0; }

        // ---------------------------------------------------------------- the moment
        /// <summary>Plays the moment now (the debug panel's replay; also what the milestone calls). <paramref name="fullVersion"/>: false is the short one on return.</summary>
        public void Begin(bool fullVersion)
        {
            var sim = Sim;
            if (sim == null || !sim.EcologyOn || box == null) return;
            full = fullVersion; length = fullVersion ? FullSeconds : AwaySeconds;
            t = 0f; reactT = 0f; lined = false; waitPond = 0f; lineAt = LineAfter;
            if (fullVersion) FullPlays++; else AwayPlays++;
            float vol = fullVersion ? 1f : 0.6f;
            Sfx.Play(CueLibrary.FirstWaterDrop, 0f, vol);
            Sfx.Play(CueLibrary.FirstWaterBubble, 0f, vol, 0.7f);
            leanPending = fullVersion; // once the water is drawn
        }

        /// <summary>The pond's middle: the water cell nearest the water cells' centre, in box units. False when none is drawn.</summary>
        public bool PondSpot(out float x, out float z)
        {
            x = z = 0f;
            var eco = box != null ? box.layout.eco : null;
            if (eco == null || eco.pondCells <= 0) return false;
            float sx = 0f, sz = 0f; int n = 0;
            for (int c = 0; c < SiteMap.N; c++)
                if (eco.WaterCell(c)) { sx += SiteMap.CXc(c); sz += SiteMap.CZc(c); n++; }
            if (n == 0) return false;
            sx /= n; sz /= n;
            float best = float.MaxValue;
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (!eco.WaterCell(c)) continue;
                float dx = SiteMap.CXc(c) - sx, dz = SiteMap.CZc(c) - sz, d = dx * dx + dz * dz;
                if (d < best) { best = d; x = SiteMap.CXc(c); z = SiteMap.CZc(c); }
            }
            return true;
        }

        bool Lean()
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : null;
            if (cam == null || !GlassVoice.CameraFree() || !PondSpot(out float x, out float z)) return false;
            var at = new Vector3(x, box.layout.eco.waterY, z);
            if (cam.NudgeToward(box.Root.TransformPoint(at))) { Leans++; return true; }
            return false;
        }

        void UpdatePeople(float dt)
        {
            var pv = PeopleView.Find();
            if (pv == null || pv.Planner == null) return;
            // the walk over: the reaction lasts a few seconds, so it is renewed twice
            reactT -= dt;
            if (reactT <= 0f && t < length)
            {
                reactT = 3.6f;
                pv.Planner.React(KeeperGesture.Marvel, pv.Now, Place.Land(PlaceKind.Pond), default, full ? 0.95f : 0.5f, 4f, 3.5f);
                PeopleSent++;
            }
            if (!lined && t >= lineAt) { lined = true; Say(pv); }
        }

        void Say(PeopleView pv)
        {
            var sim = Sim; var show = ShowMeDirector.Instance;
            if (sim == null || show == null) return;
            var lines = !full ? AwayLines : sim.BeforeCamp ? TribeLines : sim.State.eco.villageGateMonth >= 0 ? VillageLines : CampLines;
            string line = lines[(sim.State.seed & int.MaxValue) % lines.Length];
            pv.StandingNow(whoBuf, px, pz);
            PondSpot(out float cx, out float cz);
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < whoBuf.Count; i++)
            {
                float d = (px[i] - cx) * (px[i] - cx) + (pz[i] - cz) * (pz[i] - cz);
                if (whoBuf[i].IsNotable) d -= 100f; // a named person speaks if one is about
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return;
            show.SayLine(line, whoBuf[best], 4.5f);
            LastLine = line;
        }

        // ---------------------------------------------------------------- the sparkles
        void UpdateSparkles(EcoLayer eco, float dt)
        {
            if (eco.pondCells <= 0) { Hide(); return; }
            if (holder == null)
            {
                holder = new GameObject("FirstWater") { hideFlags = HideFlags.DontSave }.transform;
                holder.SetParent(box.Root, false);
            }
            holder.gameObject.SetActive(true);
            if (sparkMesh == null)
            {
                var mb = new MeshBuilder(77) { jitter = 0f };
                mb.Ico(Vector3.zero, 0.011f, new Vector3(1f, 0.35f, 1f), new Color(0.96f, 0.99f, 1f), 0.02f);
                sparkMesh = mb.ToMesh("WaterSparkle");
            }
            float env = t < length ? 1f : Mathf.Clamp01(1f - (t - length) / FadeSeconds);
            int want = Mathf.RoundToInt(MaxSparkles * env * (full ? 1f : 0.6f));
            while (sparkles.Count < MaxSparkles)
            {
                var g = new GameObject("Sparkle") { hideFlags = HideFlags.DontSave };
                g.transform.SetParent(holder, false);
                g.AddComponent<MeshFilter>().sharedMesh = sparkMesh;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = box.SolidMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.SetActive(false);
                sparkles.Add(g.transform); sparkleT.Add(-1f); sparkleLife.Add(1f);
            }
            int shown = 0;
            for (int i = 0; i < sparkles.Count; i++)
            {
                var s = sparkles[i];
                if (sparkleT[i] < 0f || sparkleT[i] >= sparkleLife[i])
                {
                    if (i >= want) { s.gameObject.SetActive(false); sparkleT[i] = -1f; continue; }
                    // a new twinkle on a random water cell
                    int c = RandomWaterCell(eco);
                    if (c < 0) { s.gameObject.SetActive(false); continue; }
                    s.localPosition = new Vector3(SiteMap.CXc(c) + Random.Range(-0.04f, 0.04f), eco.waterY + 0.006f, SiteMap.CZc(c) + Random.Range(-0.04f, 0.04f));
                    sparkleT[i] = Random.Range(0f, 0.2f); sparkleLife[i] = Random.Range(0.8f, 1.6f);
                    s.localRotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
                }
                sparkleT[i] += dt;
                float u = Mathf.Clamp01(sparkleT[i] / sparkleLife[i]);
                float k = Mathf.Sin(u * Mathf.PI);
                s.localScale = new Vector3(1f, 0.6f + 0.4f * k, 1f) * (0.2f + 1.3f * k * k);
                s.gameObject.SetActive(true); shown++;
            }
            SparklesShown = shown;
        }

        static int RandomWaterCell(EcoLayer eco)
        {
            for (int tries = 0; tries < 24; tries++)
            {
                int c = Random.Range(0, SiteMap.N);
                if (eco.WaterCell(c)) return c;
            }
            for (int c = 0; c < SiteMap.N; c++) if (eco.WaterCell(c)) return c;
            return -1;
        }
    }
}
