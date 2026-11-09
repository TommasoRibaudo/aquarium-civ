using System;

namespace AquariumCiv.Audio
{
    /// <summary>
    /// #520: the barren wind as two voices of the same 40 s recording instead of one loop that came round audibly at year 0
    /// (playtest #512). Voice B plays a lot lower (so its loop is longer, ~46 s, and the two seams never line up), and each voice's
    /// gain wanders slowly between <see cref="GainMin"/> and <see cref="GainMax"/> and its pitch drifts a few percent, so the swells
    /// of one never sit under the swells of the other, and no 40 s pattern comes back. Pure C#, its own seeded <see cref="Random"/>.
    /// </summary>
    public sealed class BarrenWind
    {
        public const float GainMin = 0.3f, GainMax = 0.9f;
        public const float PitchA = 1f, PitchB = 0.87f, PitchDrift = 0.035f;
        public const float SegMin = 12f, SegMax = 35f;

        sealed class Wander
        {
            readonly Random rng; readonly float lo, hi;
            float from, to, t, len;
            public float Value;
            public Wander(Random r, float lo, float hi) { rng = r; this.lo = lo; this.hi = hi; from = to = Value = Pick(); Next(); }
            float Pick() => lo + (float)rng.NextDouble() * (hi - lo);
            void Next() { from = to; to = Pick(); t = 0f; len = SegMin + (float)rng.NextDouble() * (SegMax - SegMin); }
            public void Tick(float dt)
            {
                t += dt;
                while (t >= len) { t -= len; Next(); }
                float u = t / len; u = u * u * (3f - 2f * u);
                Value = from + (to - from) * u;
            }
        }

        readonly Wander gainA, gainB, driftA, driftB;

        public BarrenWind(int seed)
        {
            var r = new Random(seed);
            gainA = new Wander(r, GainMin, GainMax); gainB = new Wander(r, GainMin, GainMax);
            driftA = new Wander(r, -PitchDrift, PitchDrift); driftB = new Wander(r, -PitchDrift, PitchDrift);
        }

        public float GainA => gainA.Value;
        public float GainB => gainB.Value;
        public float PitchOfA => PitchA * (1f + driftA.Value);
        public float PitchOfB => PitchB * (1f + driftB.Value);

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            gainA.Tick(dt); gainB.Tick(dt); driftA.Tick(dt); driftB.Tick(dt);
        }

        public override string ToString() => $"wind A {GainA:0.00}@{PitchOfA:0.000} B {GainB:0.00}@{PitchOfB:0.000}";
    }
}
