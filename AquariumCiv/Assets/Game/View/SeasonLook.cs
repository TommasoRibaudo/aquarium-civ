using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #576 seasons you can see. The sim's month is the clock (winter 11, 0, 1; spring 2-4; summer 5-7; autumn 8-10, LifeClock.SeasonOf);
    /// the view turns it into two shader globals the Stylized and EcoGround shaders read:
    /// <c>_AC_Season</c> = (spring, summer, autumn, winter) weights, always summing to 1, each a triangle 3 months wide centred on the
    /// season's middle month, so the land fades from one look to the next over the months and never switches; and <c>_AC_Snow</c>,
    /// 0..1, the snow on the up-facing land (full in deep winter, thin at its edges). No meshes, no per-frame work but two global sets.
    /// The phase is the sim's month plus how far the real clock is into it, so it moves smoothly between the sim's monthly steps.
    /// </summary>
    public static class SeasonLook
    {
        static readonly int SeasonId = Shader.PropertyToID("_AC_Season"), SnowId = Shader.PropertyToID("_AC_Snow");
        /// <summary>Tests and the shot class pin the phase (months, 0..12); null = follow the sim.</summary>
        public static float? HoldPhase;
        /// <summary>The last weights and snow written (tests).</summary>
        public static Vector4 Weights { get; private set; } = new Vector4(0, 1, 0, 0);
        public static float Snow { get; private set; }
        /// <summary>How many times the globals were written (tests: a month, not a frame, is the budget).</summary>
        public static int Writes { get; private set; }

        /// <summary>The middle of each season in months of the year: spring 3.5, summer 6.5, autumn 9.5, winter 0.5 (12.5).</summary>
        static readonly float[] Centre = { 3.5f, 6.5f, 9.5f, 12.5f };

        /// <summary>The season weights at a phase in months (any value; wraps at 12).</summary>
        public static Vector4 WeightsAt(float phase)
        {
            phase = ((phase % 12f) + 12f) % 12f;
            var w = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float d = Mathf.Abs(phase - Centre[i]);
                d = Mathf.Min(d, Mathf.Abs(phase - (Centre[i] - 12f)), Mathf.Abs(phase - (Centre[i] + 12f)));
                w[i] = Mathf.Max(0f, 1f - d / 3f);
            }
            float sum = w[0] + w[1] + w[2] + w[3];
            return new Vector4(w[0] / sum, w[1] / sum, w[2] / sum, w[3] / sum);
        }

        /// <summary>Snow cover 0..1 from the winter weight: it starts as winter takes hold and is full in the deep of it.</summary>
        public static float SnowAt(Vector4 w) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.95f, w.w));

        /// <summary>The sim's phase: months stepped plus the share of the current month the real clock has run.</summary>
        public static float PhaseOf(SimCore sim)
        {
            if (sim == null) return 5.5f;
            var s = sim.State;
            double months = s.simHours / sim.Config.StepHours;
            float frac = (float)(months - System.Math.Floor(months));
            return (float)sim.CalendarMonth + Mathf.Clamp01(frac);
        }

        /// <summary>The season word the title line shows, from the sim's month: "Spring", "Summer", "Autumn", "Winter".</summary>
        public static string Word(long month) => LifeClock.SeasonOf(month).ToString();

        /// <summary>The middle of spring as a phase: the look the first run holds (full spring weight, no snow), so the year it
        /// skips through does not flash summer, autumn and winter at the opening (#603).</summary>
        public const float SpringMiddle = 3.5f;

        /// <summary>#576 cheat: the real clock moves on to the middle of the next season (months from the sim's phase now).</summary>
        public static float MonthsToNextSeasonMiddle(float phase)
        {
            phase = ((phase % 12f) + 12f) % 12f;
            float best = float.MaxValue;
            foreach (var c in Centre)
            {
                float d = c - phase; // winter's middle is 0.5 and 12.5
                if (d > 0.01f) best = Mathf.Min(best, d);
            }
            foreach (var c in Centre) { float d = c - 12f - phase; if (d > 0.01f) best = Mathf.Min(best, d); }
            return best;
        }

        /// <summary>Writes the globals for a phase (only when they changed).</summary>
        public static void Apply(float phase)
        {
            var w = WeightsAt(phase);
            float snow = SnowAt(w);
            if ((w - Weights).sqrMagnitude < 1e-6f && Mathf.Abs(snow - Snow) < 1e-4f && Writes > 0) return;
            Weights = w; Snow = snow; Writes++;
            Shader.SetGlobalVector(SeasonId, w);
            Shader.SetGlobalFloat(SnowId, snow);
        }

        /// <summary>Back to a neutral summer, no snow (a scene without a sim).</summary>
        public static void Reset()
        {
            HoldPhase = null; Weights = new Vector4(0, 1, 0, 0); Snow = 0f; Writes = 0;
            Shader.SetGlobalVector(SeasonId, new Vector4(0, 1, 0, 0));
            Shader.SetGlobalFloat(SnowId, 0f);
        }
    }

    /// <summary>Feeds <see cref="SeasonLook"/> each frame: a few flops, and two global sets only when the phase moved enough to see.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class SeasonDriver : MonoBehaviour
    {
        public static SeasonDriver Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            var b = FindAnyObjectByType<BoxView>();
            if (b != null && b.GetComponent<SeasonDriver>() == null) b.gameObject.AddComponent<SeasonDriver>();
        }

        void OnEnable() { if (Instance != null && Instance != this) { enabled = false; return; } Instance = this; }
        void OnDisable() { if (Instance != this) return; Instance = null; SeasonLook.Reset(); }

        void Update()
        {
            var clock = GameClock.Instance;
            // The first run is the approved opening (mist -> pond). A new box starts in spring (#603), so it holds the spring look for the
            // whole run (its year skip would otherwise cycle through every season); the seasons show from the first visit after it.
            var day = DayLightDriver.Instance;
            if (SeasonLook.HoldPhase == null && day != null && day.HeldForFirstRun) { SeasonLook.Apply(SeasonLook.SpringMiddle); return; }
            float p = SeasonLook.HoldPhase ?? SeasonLook.PhaseOf(clock != null ? clock.Sim : null);
            SeasonLook.Apply(p);
        }
    }
}
