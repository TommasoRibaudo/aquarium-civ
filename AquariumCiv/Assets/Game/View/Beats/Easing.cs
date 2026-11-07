// #355 Beat kit (pure: Mathf only, so Tools/AnimBench compiles it). Names: docs/design/animation-specs.md §0.3.
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>The easing names of the spec sheets (animation-specs §0.3). Each maps 0..1 progress through a phase to 0..1
    /// of the way between its keys; <see cref="Back"/>, <see cref="Wobble"/> and <see cref="Pop"/> overshoot past 1 on the way.</summary>
    public enum Ease
    {
        /// <summary>Even speed (a thrown arc's time).</summary>
        Linear,
        /// <summary>Quad in: speeds up.</summary>
        In,
        /// <summary>Quad out: slows down.</summary>
        Out,
        /// <summary>Smoothstep (today's <c>DollMotion.Smooth</c>).</summary>
        Smooth,
        /// <summary>Cubic in: strikes (≤ 0.15 s).</summary>
        Snap,
        /// <summary>Out-back, s = 1.7: past the key and back (settles, lifts).</summary>
        Back,
        /// <summary>A ball landing: falls in, then two bounces at 35 % and 12 % of the drop.</summary>
        Bounce,
        /// <summary>Reaches the key and quivers about it, damped (a phase's stand-in for <see cref="Easing.Wobble"/>).</summary>
        Wobble,
        /// <summary>0 → 1.2 → 1 (something appearing).</summary>
        Pop,
    }

    /// <summary>The curves behind <see cref="Ease"/>, plus the damped sine the answers shake with (animation-specs §0.3, §0.5).</summary>
    public static class Easing
    {
        /// <summary>The out-back overshoot (§0.3).</summary>
        public const float BackS = 1.70158f;
        /// <summary>The ball's two bounces, as a share of its drop (§0.3).</summary>
        public const float Bounce1 = 0.35f, Bounce2 = 0.12f;
        /// <summary>How far <see cref="Pop"/> overshoots, and where in its time it peaks.</summary>
        public const float PopPeak = 1.2f, PopPeakAt = 0.6f;

        /// <summary>The eased value of <paramref name="u"/> (clamped to 0..1). Exactly 0 at 0 and 1 at 1 for every curve.</summary>
        public static float Apply(Ease e, float u)
        {
            u = Mathf.Clamp01(u);
            switch (e)
            {
                case Ease.In: return u * u;
                case Ease.Out: return 1f - (1f - u) * (1f - u);
                case Ease.Smooth: return u * u * (3f - 2f * u);
                case Ease.Snap: return u * u * u;
                case Ease.Back: { float v = u - 1f; return 1f + (BackS + 1f) * v * v * v + BackS * v * v; }
                case Ease.Bounce: return 1f - BounceHeight(u);
                case Ease.Wobble: return u >= 1f ? 1f : 1f - Mathf.Exp(-5f * u) * Mathf.Cos(u * Mathf.PI * 2f * 2.5f) * (1f - u);
                case Ease.Pop: return Pop(u);
                default: return u;
            }
        }

        /// <summary>Scale 0 → <see cref="PopPeak"/> → 1 over <paramref name="u"/> 0..1 (§0.3 <c>Pop</c>, 0.15 s on the sheets).</summary>
        public static float Pop(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < PopPeakAt) { float k = u / PopPeakAt; return PopPeak * (1f - (1f - k) * (1f - k)); }
            float r = (u - PopPeakAt) / (1f - PopPeakAt);
            return Mathf.Lerp(PopPeak, 1f, r * r * (3f - 2f * r));
        }

        /// <summary>Height (share of the drop, 1 = the top) of a ball that falls over the first part of <paramref name="u"/>, then
        /// bounces twice (<see cref="Bounce1"/>, <see cref="Bounce2"/>) and lies still at 1. Each hop's time goes with √height, as a
        /// real one's does, so it reads as a ball.</summary>
        public static float BounceHeight(float u)
        {
            u = Mathf.Clamp01(u);
            float b1 = Mathf.Sqrt(Bounce1), b2 = Mathf.Sqrt(Bounce2);
            float total = 1f + 2f * b1 + 2f * b2; // the fall takes 1 unit; a hop of height h takes 2√h
            float s = u * total;
            if (s < 1f) return 1f - s * s;                         // the drop
            s -= 1f;
            if (s < 2f * b1) { float x = s - b1; return Bounce1 - x * x; }  // hop 1: a parabola of height Bounce1
            s -= 2f * b1;
            if (s < 2f * b2) { float x = s - b2; return Mathf.Max(0f, Bounce2 - x * x); }
            return 0f;
        }

        /// <summary>The damped shake of an answer (§0.3 <c>Wobble(A, f, τ)</c>): A·e^(−t/τ)·sin 2πft, <paramref name="t"/> in seconds
        /// since the contact (0 before it). Bush: 7°, 6 Hz, 0.18 s; spear in the ground: 6°, 10 Hz, 0.25 s (§0.5, §0.6).</summary>
        public static float Wobble(float t, float amplitude, float frequency, float tau)
        {
            if (t <= 0f || tau <= 0f) return 0f;
            return amplitude * Mathf.Exp(-t / tau) * Mathf.Sin(t * frequency * Mathf.PI * 2f);
        }

        /// <summary>How long a <see cref="Wobble"/> stays visible: until its envelope drops under 6 % of A (−ln 0.06 ≈ 2.81).</summary>
        public static float WobbleSeconds(float tau) => tau * 2.81f;
    }
}
