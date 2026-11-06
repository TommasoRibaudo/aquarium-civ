// #355 Beat kit (pure: Mathf only, so Tools/AnimBench compiles it). De-sync: docs/design/animation-specs.md §0.8 (P6).
namespace AquariumCiv.View
{
    /// <summary>The beat kit's own seeded hash. Never the sim's random stream: drawing from that would move every seed-pinned test.</summary>
    public static class BeatHash
    {
        public static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16; h *= 0x7feb352du;
                h ^= h >> 15; h *= 0x846ca68bu;
                h ^= h >> 16;
                return h;
            }
        }

        public static uint Of(int a, int b, int c = 0)
        {
            unchecked { return Mix((uint)a * 0x9E3779B1u ^ Mix((uint)b + 0x632BE5ABu) ^ Mix((uint)c * 0x85EBCA77u + 0x27D4EB2Fu)); }
        }

        /// <summary>0 ≤ x &lt; 1 from three ints.</summary>
        public static float U01(int a, int b, int c = 0) => (Of(a, b, c) >> 8) / 16777216f;

        public static float Range(float lo, float hi, int a, int b, int c = 0) => lo + (hi - lo) * U01(a, b, c);

        /// <summary>0 ≤ i &lt; n.</summary>
        public static int Pick(int n, int a, int b, int c = 0) => n <= 1 ? 0 : (int)(Of(a, b, c) % (uint)n);
    }

    /// <summary>
    /// How one member of a beat stays out of step with its neighbours (animation-specs §0.8, P6): a start delay 0–0.6 s, its period
    /// × 0.9–1.1 and its amplitude × 0.85–1.15, all from a hash of its key. Play a beat for a member as
    /// <c>beat.Pose(under, d.Clock(since), d.amp)</c>. <see cref="InStep"/> is for the dance ring and the festival's last cheer,
    /// which move together on purpose.
    /// </summary>
    public readonly struct Desync
    {
        public const float MaxDelay = 0.6f, PeriodLo = 0.9f, PeriodHi = 1.1f, AmpLo = 0.85f, AmpHi = 1.15f;
        /// <summary>P6: phases closer than this count as "in step".</summary>
        public const float InStepWindow = 0.15f;

        public readonly float delay, period, amp;

        public Desync(float delay, float period, float amp) { this.delay = delay; this.period = period; this.amp = amp; }

        /// <summary>No de-sync: start now, as written.</summary>
        public static Desync InStep => new Desync(0f, 1f, 1f);

        /// <summary>A member on its own, from its key (a person's <c>PersonRef.Key</c>) and a salt per kind of beat.</summary>
        public static Desync Of(int key, int salt)
            => new Desync(BeatHash.Range(0f, MaxDelay, key, salt, 1), BeatHash.Range(PeriodLo, PeriodHi, key, salt, 2), BeatHash.Range(AmpLo, AmpHi, key, salt, 3));

        /// <summary>
        /// Member <paramref name="index"/> of <paramref name="count"/> in a group: the delays are spread in slots (a hashed order, a small
        /// jitter in each), so no three members ever start within <see cref="InStepWindow"/> of each other, as a plain hash could.
        /// The slot is 0.6 s / count, but at least 0.1 s (a big group spreads over more than 0.6 s).
        /// </summary>
        public static Desync InGroup(int seed, int index, int count)
        {
            count = count < 1 ? 1 : count;
            float slot = UnityEngine.Mathf.Max(MaxDelay / count, 0.1f);
            uint mine = BeatHash.Of(seed, index, 7);
            int rank = 0;
            for (int j = 0; j < count; j++)
            {
                if (j == index) continue;
                uint other = BeatHash.Of(seed, j, 7);
                if (other < mine || (other == mine && j < index)) rank++;
            }
            float jitter = (BeatHash.U01(seed, index, 8) - 0.5f) * 0.2f; // ± a tenth of a slot
            float delay = slot * (rank + 0.5f + jitter) - slot * 0.5f;     // the first slot starts at 0
            return new Desync(UnityEngine.Mathf.Max(0f, delay), BeatHash.Range(PeriodLo, PeriodHi, seed, index, 9), BeatHash.Range(AmpLo, AmpHi, seed, index, 10));
        }

        /// <summary>The member's beat clock: seconds into its (stretched) beat, from seconds since the group's start.</summary>
        public float Clock(float since) => (since - delay) / (period <= 0f ? 1f : period);

        /// <summary>A looping move's phase for this member (a dance step, a run's stride): its own start and speed.</summary>
        public float LoopTime(float t) => (t - delay) / (period <= 0f ? 1f : period);
    }
}
