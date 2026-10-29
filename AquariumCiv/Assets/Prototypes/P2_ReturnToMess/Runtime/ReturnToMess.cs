using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.Prototypes.P2
{
    /// <summary>
    /// P2 Return to Mess (prototype-plan.md §5): does coming back to a messy tank pull you in, or feel like guilt?
    /// One variable, the consequence of mess:
    ///   A "cosmetic": mess only looks bad; output and mood ignore it.
    ///   B "output":   mess drags output down linearly (−25 % at the 48 h cap), shown in the ledger, and people grumble.
    ///   Mood ignores mess in both, so B's only economic difference is that stated penalty (tester run 1, finding 1).
    /// Both: P1's wipe gesture, a minimal economy, real offline catch-up, and a welcome-back wave with a one-line summary.
    /// Lab mode adds tester-facing "step away" buttons; diary mode runs on real time only.
    /// Holding a finger on the ledger for 2 s switches this phone between diary and lab (for sit-down lab sessions).
    /// </summary>
    public class ReturnToMess : MonoBehaviour
    {
        public WipeCleaner wipe;
        public CrowdView crowd;
        [Tooltip("Shorter absences than this (real hours) get no wave or summary.")] public double waveMinHours = 0.25;
        [Tooltip("In B, citizens grumble out loud above this mess level.")] public float grumbleAbove = 0.3f;
        public Vector2 grumbleEverySeconds = new(9f, 16f);
        [Tooltip("No grumbling this long after a return: the welcome comes first.")] public float grumbleQuietAfterReturn = 12f;
        [Tooltip("Seconds to hold the ledger to switch diary/lab.")] public float labHoldSeconds = 2f;
        [Tooltip("In B, someone says something happy when cleaning brings mess below this.")] public float cheerBelow = 0.1f;

        // Wistful or comic, never a demand on the player: the people react to their world, they don't scold the Keeper.
        static readonly string[] Grumbles =
        {
            "I miss the sun...", "Found a sock bigger than my house.", "The weeds are taller than me now!",
            "Is it evening already?", "My crops are squinting.", "Someone's crate is in my garden.",
        };
        static readonly string[] Cheers = { "Look at that sky!", "Sunshine!", "I can see the hills again!", "Back to work, the light's back!" };

        public bool OutputVariant => PrototypeConfig.Current.Variant == "B";
        bool Lab => PrototypeConfig.Current.testMode == TestMode.Lab;

        SimClock clock;
        int popBefore;
        Dictionary<string, int> buildingsBefore = new();
        float nextGrumble;
        bool started;
        GUIStyle ledgerStyle;
        Rect ledgerScreen;  // last drawn ledger, in screen pixels (top-left origin)
        float holdTime;
        bool holdDone, holdOnLedger;

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
            wipe.mess.Cleaned += OnCleaned;
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
            cfg.messAffectsMood = false; // mood x mess would stack on the penalty (about −36 % in total) and confound the A/B
        }

        void Update()
        {
            ApplyVariant(); // diary variants change at midnight; cheap and always in sync
            UpdateLabHold();
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

        float messBeforeClean = -1, lastCheer = -99;

        /// <summary>B: when cleaning brings the tank back to (nearly) spotless, a citizen says something happy.</summary>
        void OnCleaned()
        {
            if (!OutputVariant || crowd.Agents.Count == 0) return;
            var sim = clock.Sim;
            if (sim.MessLevel >= cheerBelow || Time.unscaledTime - lastCheer < 20f) return;
            lastCheer = Time.unscaledTime;
            nextGrumble = Time.unscaledTime + grumbleEverySeconds.y;
            var a = crowd.Agents[Random.Range(0, crowd.Agents.Count)];
            HudToast.Show(Cheers[Random.Range(0, Cheers.Length)], a.transform.position + Vector3.up * crowd.characterScale * 3f, 3f);
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
            nextGrumble = Time.unscaledTime + grumbleQuietAfterReturn;

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

        /// <summary>Hold a finger on the ledger to switch diary/lab. Each switch starts a fresh village.</summary>
        void UpdateLabHold()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.isPressed) { holdTime = 0; holdDone = false; holdOnLedger = false; return; }
            var p = pointer.position.ReadValue();
            bool onLedger = ledgerScreen.Contains(new Vector2(p.x, Screen.height - p.y));
            if (pointer.press.wasPressedThisFrame) holdOnLedger = onLedger; // only a press that starts on the ledger counts
            if (holdDone || !holdOnLedger || !onLedger) { holdTime = 0; return; }
            if ((holdTime += Time.unscaledDeltaTime) < labHoldSeconds) return;
            holdDone = true;
            var cfg = PrototypeConfig.Current;
            var mode = cfg.testMode == TestMode.Lab ? TestMode.Diary : TestMode.Lab;
            EventLog.Instance?.Log("debug", ("test_mode", mode.ToString()));
            cfg.SetTestMode(mode); // lab resets the village in StartVariant
            if (mode == TestMode.Diary)
            {
                clock.ResetWorld();
                HudToast.ShowBanner("Diary mode: the tank now runs on real time. Come back whenever you like.", 6f);
            }
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
            string ledger = (Lab ? "<color=#8fe0d0>LAB</color>  ·  " : "") + $"Year {Mathf.FloorToInt(s.Year)}  ·  {s.population} people  ·  {sim.Count(BuildingType.Hut)} {(sim.Count(BuildingType.Hut) == 1 ? "hut" : "huts")}";
            if (OutputVariant)
            {
                // Framed as the world's state, not a loss: grime blocks the people's light, and work follows the light.
                int sun = Mathf.RoundToInt((1f - sim.MessLevel) * 100f);
                int pace = Mathf.RoundToInt((1f - sim.OutputPenalty) * 100f);
                ledger += sun >= 99 ? "\n<color=#ffd98a>Full sunlight  ·  work pace 100%</color>" : $"\n<color=#e6dcc4>Sunlight {sun}%  ·  work pace {pace}%</color>";
            }
            ledgerStyle ??= new GUIStyle(ProtoGui.Box) { richText = true, wordWrap = false };
            var size = ledgerStyle.CalcSize(new GUIContent(ledger));
            var r = new Rect(20, Mathf.Max(20, safeTop + 4), size.x + 8, size.y);
            ProtoGui.Block(r);
            GUI.Box(r, ledger, ledgerStyle);
            ledgerScreen = new Rect(r.x * ProtoGui.Scale, r.y * ProtoGui.Scale, r.width * ProtoGui.Scale, r.height * ProtoGui.Scale);
            if (holdTime > 0.3f && !holdDone) // hold progress, so a switch never comes as a surprise
                GUI.Box(new Rect(r.x + 8, r.yMax + 4, (r.width - 16) * Mathf.Clamp01(holdTime / labHoldSeconds), 8), GUIContent.none, ProtoGui.Primary);

            if (!Lab) return;
            float w = ProtoGui.Width;
            var row = new Rect(20, ProtoGui.Height - 112 - 12 - 108, w - 40, 108); // above Feedback / Done (88 tall at h - 112)
            ProtoGui.Block(row);
            ProtoGui.Panel(row);
            GUILayout.BeginArea(new Rect(row.x + 12, row.y + 8, row.width - 24, row.height - 16));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Step away:", GUILayout.Width(120), GUILayout.Height(88));
            foreach (var h in new[] { 1, 8, 24, 48 })
                if (GUILayout.Button($"+{h} h", GUILayout.Height(88))) clock.SimulateAway(h); // 88 canvas ≈ 44 pt
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
