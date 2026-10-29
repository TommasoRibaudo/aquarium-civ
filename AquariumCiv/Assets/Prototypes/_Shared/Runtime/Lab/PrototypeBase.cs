using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// The lab/diary flow shared by P3–P6 (P2 has its own and stays as tested):
    /// - Lab: every variant starts from the same fresh world (same seed, same pre-generated history), so A and B compare
    ///   fairly; the tester then uses "Step away". Diary: real time, real offline catch-up, variant by day.
    /// - A return snapshots the world first, so the prototype can say what changed; people wave on any return ≥ 15 min.
    /// - The care layer is the shared wipe + mess with no output penalty and no mood link (Gate 1's question, not ours).
    /// </summary>
    public abstract class PrototypeBase : MonoBehaviour
    {
        public WipeCleaner wipe;
        public CrowdView crowd;
        public NotableView notables;
        public LabHud hud;
        [Tooltip("Game years simulated before a fresh world is shown, so it already has people, families and stories.")]
        public float prehistoryYears = 20f;
        [Tooltip("Shorter absences than this (real hours) get no wave or welcome.")] public double waveMinHours = 0.25;

        protected SimClock clock;
        protected bool started;
        protected long monthBefore;
        protected int popBefore;
        protected Dictionary<string, int> buildingsBefore = new();
        bool skipLaunchReturn;

        protected PrototypeConfig Cfg => PrototypeConfig.Current;
        protected bool Lab => Cfg.testMode == TestMode.Lab;
        protected int VariantIndex => Cfg.CurrentIndex;

        protected virtual void Awake()
        {
            // Before SimClock.Start runs the launch catch-up, so the rules apply to the time away.
            clock = SimClock.Instance;
            ApplyConfig(clock.config);
            // Lab: a fresh world replaces whatever loaded. Diary: continue its own save (lab sessions never touch it),
            // or start a fresh one with history if this phone has none yet.
            skipLaunchReturn = Lab || !clock.HasSave;
            clock.Returning += Snapshot;
            clock.Returned += HandleReturn;
        }

        protected virtual IEnumerator Start()
        {
            Cfg.VariantChanged += _ => BeginVariant(true);
            // Lab → diary: back to the diary's own world (or a fresh one). Diary → lab starts fresh through VariantChanged.
            if (hud != null) hud.ModeSwitched += m =>
            {
                if (m != TestMode.Diary) return;
                if (clock.HasSave) clock.Reload(); else FreshWorld();
                BeginVariant(false);
            };
            yield return null; // after the launch catch-up
            if (skipLaunchReturn) FreshWorld();
            BeginVariant(false);
            started = true;
        }

        protected virtual void Update() => ApplyConfig(clock.config); // diary variants change at midnight

        /// <summary>Set the sim switches for this prototype (called every frame; keep it cheap).</summary>
        protected virtual void ApplyConfig(SimConfig cfg)
        {
            cfg.outputPenaltyEnabled = false;
            cfg.messAffectsMood = false;
        }

        /// <summary>Same seed, same history, clean glass: the start line for both variants.</summary>
        protected virtual void FreshWorld()
        {
            clock.ResetWorld();
            ApplyConfig(clock.config);
            clock.Sim.Advance(prehistoryYears / clock.config.yearsPerRealHour);
            clock.Sim.CleanAll();
            if (GlassGrime.Instance != null) GlassGrime.Instance.ResetTo(0f);
            clock.Save();
            EventLog.Instance?.Log("lab_setup", ("prehistory_years", prehistoryYears), ("year", clock.Sim.State.Year), ("pop", clock.Sim.State.population));
        }

        void BeginVariant(bool switched)
        {
            ApplyConfig(clock.config);
            if (switched && Lab) FreshWorld();
            OrbitCamera.Instance?.ResetView();
            OnVariantStart(switched);
        }

        /// <summary>A variant begins (launch, switch, or mode change). Show the variant's hint here.</summary>
        protected abstract void OnVariantStart(bool switched);

        /// <summary>The player came back after at least waveMinHours. sinceMonth = the game month before the absence.</summary>
        protected abstract void OnReturn(double hours, float years, long sinceMonth);

        void Snapshot()
        {
            var s = clock.Sim.State;
            monthBefore = s.month;
            popBefore = s.population;
            buildingsBefore = s.buildings.GroupBy(b => b.type).ToDictionary(g => g.Key, g => g.Count());
        }

        void HandleReturn(double hours, float years)
        {
            var sim = clock.Sim;
            EventLog.Instance?.Log("return", ("away_h", hours), ("years", years), ("pop_delta", sim.State.population - popBefore),
                ("new_buildings", sim.State.buildings.Count - buildingsBefore.Values.Sum()), ("mess", sim.MessLevel),
                ("stories", sim.State.chronicle.Count(e => e.month > monthBefore)));
            if (!started && skipLaunchReturn) return;
            if (hours < waveMinHours) return;
            NotableCardPanel.Instance?.Close("return"); // the welcome comes first
            OnReturn(hours, years, monthBefore);
            StartCoroutine(Wave());
        }

        IEnumerator Wave()
        {
            yield return new WaitForSecondsRealtime(0.6f); // new citizens spawn first
            crowd.CheerAll(2.5f);
            if (notables != null) notables.CheerAll(2.5f);
            Juice.Chime();
        }

        /// <summary>"You were away 8 hours: 8 years passed. +4 people, 1 new hut."</summary>
        protected string StatsLine(double hours, float years)
        {
            var s = clock.Sim.State;
            var built = s.buildings.GroupBy(b => b.type)
                .Select(g => (type: g.Key, n: g.Count() - buildingsBefore.GetValueOrDefault(g.Key)))
                .Where(x => x.n > 0).ToList();
            int people = s.population - popBefore;
            var news = new List<string>();
            if (people != 0) news.Add($"{(people > 0 ? "+" : "")}{people} {(Mathf.Abs(people) == 1 ? "person" : "people")}");
            news.AddRange(built.Select(x => $"{x.n} new {Noun(x.type, x.n)}"));
            return $"You were away {Away(hours)}: {years:0.#} years passed. {(news.Count > 0 ? string.Join(", ", news) : "All quiet in the glass")}.";
        }

        protected static string Away(double hours) => hours < 1 ? $"{hours * 60:0} minutes" : $"{hours:0.#} hours";

        protected static string Noun(string type, int n)
        {
            string w = type == BuildingType.Lumbermill ? "lumber mill" : type.ToLowerInvariant();
            return n == 1 ? w : w == "church" ? "churches" : w + "s";
        }

        /// <summary>Common debug rows: quiet time jumps (no return), so states are reachable without waiting.</summary>
        protected void DrawTimeCheats()
        {
            GUILayout.BeginHorizontal();
            foreach (var y in new[] { 1, 10, 50 })
                if (GUILayout.Button($"+{y} years (quiet)", GUILayout.Height(54))) clock.Sim.Advance(y / clock.config.yearsPerRealHour);
            if (GUILayout.Button("Fresh lab world", GUILayout.Height(54))) { FreshWorld(); OnVariantStart(false); }
            GUILayout.EndHorizontal();
        }
    }
}
