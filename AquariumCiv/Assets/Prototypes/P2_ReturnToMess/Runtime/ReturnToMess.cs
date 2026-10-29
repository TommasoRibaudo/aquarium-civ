using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes.P2
{
    /// <summary>
    /// P2 Return to Mess (prototype-plan.md §5): does coming back to a messy tank pull you in, or feel like guilt?
    /// One variable, the consequence of mess:
    ///   A "cosmetic": mess only looks bad; output and mood ignore it.
    ///   B "output":   mess drags output down linearly (−25 % at the 48 h cap), shown in the ledger, and people grumble.
    /// Both: P1's wipe gesture, a minimal economy, real offline catch-up, and a welcome-back wave with a one-line summary.
    /// Lab mode adds tester-facing "step away" buttons; diary mode runs on real time only.
    /// </summary>
    public class ReturnToMess : MonoBehaviour
    {
        public WipeCleaner wipe;
        public CrowdView crowd;
        [Tooltip("Shorter absences than this (real hours) get no wave or summary.")] public double waveMinHours = 0.25;
        [Tooltip("In B, citizens grumble out loud above this mess level.")] public float grumbleAbove = 0.3f;
        public Vector2 grumbleEverySeconds = new(9f, 16f);

        static readonly string[] Grumbles =
        {
            "Ugh, I can't see the sky.", "Who left this junk here?", "These weeds are everywhere.",
            "Hard to work in this gloom.", "Is anyone going to clean up?", "My crops need light!",
        };

        public bool OutputVariant => PrototypeConfig.Current.Variant == "B";
        bool Lab => PrototypeConfig.Current.testMode == TestMode.Lab;

        SimClock clock;
        int popBefore;
        Dictionary<string, int> buildingsBefore = new();
        float nextGrumble;
        bool started;
        GUIStyle ledgerStyle;

        void Awake()
        {
            // Before SimClock.Start runs the launch catch-up, so the variant's rules apply to the time away.
            clock = SimClock.Instance;
            ApplyVariant();
            clock.Returning += Snapshot;
            clock.Returned += OnReturned;
        }

        IEnumerator Start()
        {
            PrototypeConfig.Current.VariantChanged += _ => StartVariant(true);
            yield return null; // after the launch welcome-back
            StartVariant(false);
            started = true;
        }

        void StartVariant(bool switched)
        {
            ApplyVariant();
            // Lab: every variant starts from the same fresh village and a clean tank, then the tester steps away.
            if (Lab && switched) clock.ResetWorld();
            if (Lab) HudToast.ShowBanner("Lab session: use \"Step away\" below to leave the tank for a while, then come back to it.", 8f);
            else if (!PlayerPrefs.HasKey("p2_hinted")) { PlayerPrefs.SetInt("p2_hinted", 1); wipe.ShowHint(); }
        }

        void ApplyVariant()
        {
            var cfg = clock.config;
            cfg.outputPenaltyEnabled = OutputVariant;
            cfg.messAffectsMood = OutputVariant;
        }

        void Update()
        {
            ApplyVariant(); // diary variants change at midnight; cheap and always in sync
            var sim = clock.Sim;
            float grump = OutputVariant ? Mathf.Clamp01(sim.MessLevel * 1.4f) : 0f;
            crowd.grumpiness = Mathf.MoveTowards(crowd.grumpiness, grump, Time.unscaledDeltaTime * 0.5f);
            if (OutputVariant && sim.MessLevel > grumbleAbove && Time.unscaledTime > nextGrumble && crowd.Agents.Count > 0)
            {
                nextGrumble = Time.unscaledTime + Random.Range(grumbleEverySeconds.x, grumbleEverySeconds.y);
                var a = crowd.Agents[Random.Range(0, crowd.Agents.Count)];
                HudToast.Show(Grumbles[Random.Range(0, Grumbles.Length)], a.transform.position + Vector3.up * crowd.characterScale * 3f, 3f);
                EventLog.Instance?.Count("grumbles");
            }
        }

        // ---------- welcome back ----------

        void Snapshot()
        {
            var s = clock.Sim.State;
            popBefore = s.population;
            buildingsBefore = s.buildings.GroupBy(b => b.type).ToDictionary(g => g.Key, g => g.Count());
        }

        void OnReturned(double hours, float years)
        {
            var sim = clock.Sim;
            var s = sim.State;
            var built = s.buildings.GroupBy(b => b.type)
                .Select(g => (type: g.Key, n: g.Count() - buildingsBefore.GetValueOrDefault(g.Key)))
                .Where(x => x.n > 0).ToList();
            int people = s.population - popBefore;
            EventLog.Instance?.Log("return", ("away_h", hours), ("years", years), ("pop_delta", people),
                ("new_buildings", built.Sum(x => x.n)), ("mess", sim.MessLevel), ("output_penalty", sim.OutputPenalty));
            if (hours < waveMinHours) return;

            string away = hours < 1 ? $"{hours * 60:0} minutes" : $"{hours:0.#} hours";
            var news = new List<string>();
            if (people != 0) news.Add($"{(people > 0 ? "+" : "")}{people} {(Mathf.Abs(people) == 1 ? "person" : "people")}");
            news.AddRange(built.Select(x => $"{x.n} new {Noun(x.type, x.n)}"));
            HudToast.ShowBanner($"Welcome back, Keeper! You were away {away}. {(news.Count > 0 ? string.Join(", ", news) : "All quiet in the glass")}.", 6f);
            StartCoroutine(Wave());
        }

        IEnumerator Wave()
        {
            yield return new WaitForSecondsRealtime(0.6f); // new citizens spawn first
            crowd.CheerAll(2.5f);
            Juice.Chime();
        }

        static string Noun(string type, int n)
        {
            string w = type == BuildingType.Lumbermill ? "lumber mill" : type.ToLowerInvariant();
            return n == 1 ? w : w == "church" ? "churches" : w + "s";
        }

        // ---------- HUD ----------

        void OnGUI()
        {
            if (!started || !SurveyPanel.IsPlaying) return;
            ProtoGui.Begin();
            var sim = clock.Sim;
            var s = sim.State;
            float safeTop = (Screen.height - Screen.safeArea.yMax) / ProtoGui.Scale;
            string ledger = $"Year {Mathf.FloorToInt(s.Year)}  ·  {s.population} people  ·  {sim.Count(BuildingType.Hut)} {(sim.Count(BuildingType.Hut) == 1 ? "hut" : "huts")}";
            if (OutputVariant)
            {
                int pct = Mathf.RoundToInt(sim.OutputPenalty * 100f);
                ledger += pct >= 1 ? $"\n<color=#ffb070>Mess is slowing work: −{pct}%</color>" : "\nWork is going well";
            }
            ledgerStyle ??= new GUIStyle(ProtoGui.Box) { richText = true, wordWrap = false };
            var size = ledgerStyle.CalcSize(new GUIContent(ledger));
            var r = new Rect(20, Mathf.Max(20, safeTop + 4), size.x + 8, size.y);
            ProtoGui.Block(r);
            GUI.Box(r, ledger, ledgerStyle);

            if (!Lab) return;
            float w = ProtoGui.Width, y = ProtoGui.Height - 190;
            var row = new Rect(20, y - 10, w - 40, 88);
            ProtoGui.Block(row);
            ProtoGui.Panel(row);
            GUILayout.BeginArea(new Rect(row.x + 12, row.y + 8, row.width - 24, row.height - 16));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Step away:", GUILayout.Width(120), GUILayout.Height(64));
            foreach (var h in new[] { 1, 8, 24, 48 })
                if (GUILayout.Button($"+{h} h", GUILayout.Height(64))) clock.SimulateAway(h);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
