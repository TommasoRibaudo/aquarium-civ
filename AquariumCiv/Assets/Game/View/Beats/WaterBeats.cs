// #360 Drawing water (animation-specs B4). Pure: Mathf and DollPose only, so Tools/AnimBench compiles it.
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The water run: kneel at the edge, dip the bucket under (Contact: droplets and a ring from HitFx), hold it under while it
    /// fills, lift with effort (the prop swaps empty to full), the weight lands. Then the heavy walk home. The draw is told once on
    /// <c>since</c> (real seconds into the FetchWater block) with its own start delay per person.
    /// </summary>
    public static class WaterBeats
    {
        public const float HeavyRoll = 7f;
        /// <summary>Salt for the start delay's hash (never the sim's stream).</summary>
        const int Salt = 360;

        // Arms are absolute; the bucket hangs upright at HoldArm (-35, 0, 10), so the arm only tips it a little.
        public static readonly Beat Draw = new Beat(
            Beat.Step(BeatPhase.Approach, 0.30f, Ease.Smooth, PoseKey.Hold),
            // kneel: squash 0.76, lean forward, bucket hand forward. Look-fix (AnimLab, batch 1): the feet stay ON the bank (offset y
            // -0.07 with squash 0.6 sank the real doll to the shoulders into the ground); the squash alone reads as the kneel.
            Beat.Step(BeatPhase.Antic, 0.40f, Ease.Smooth, PoseKey.Hold.Squash(0.76f).Lean(18f).Offset(0f, -0.008f, -0.012f).ArmR(10f, 0f, 10f)),
            // dip: further over, the arm reaches out and down so the bucket goes into the water past the edge
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Smooth, PoseKey.Hold.Squash(0.7f).Lean(34f).Offset(0f, -0.012f, -0.04f).ArmR(58f, 0f, 8f)),
            Beat.Step(BeatPhase.Contact, 0.08f, Ease.Linear, PoseKey.Hold),
            // under, filling (the wobble is added in Apply)
            Beat.Step(BeatPhase.Hold, 0.60f, Ease.Linear, PoseKey.Hold),
            // lift anticipation: sink, then up with effort (Back overshoots the lean)
            Beat.Step(BeatPhase.Antic, 0.15f, Ease.Smooth, PoseKey.Hold.Squash(0.66f).Lean(38f)),
            Beat.Step(BeatPhase.Act, 0.40f, Ease.Back, PoseKey.Hold.Lean(-14f).Squash(0.97f).Offset(0f, 0f, 0.01f).ArmR(-20f, 0f, 10f)),
            // the weight lands: the full bucket held a moment
            Beat.Step(BeatPhase.EndBeat, 0.25f, Ease.Smooth, PoseKey.Hold.Squash(0.92f).Lean(4f).Offset(0f, -0.01f, 0f)),
            Beat.Step(BeatPhase.Settle, 0.20f, Ease.Smooth, PoseKey.Base));

        /// <summary>The lift's anticipation starts here (after the hold under): the sum of the first five steps.</summary>
        public static readonly float LiftAt = Draw[0].seconds + Draw[1].seconds + Draw[2].seconds + Draw[3].seconds + Draw[4].seconds;

        /// <summary>Start delay for a person at a bank: 0.33 s per queue place (the place's slot, three places) plus a hashed 0..0.04 s, so two
        /// drawers at one bank never dip within 0.3 s of each other (the spec) and the longest, 0.70 s, still fits the 3.5 s block.</summary>
        public static float Delay(int key, int slot) => (slot < 0 ? 0 : slot % 3) * 0.33f + BeatHash.U01(key, Salt, 1) * 0.04f;

        /// <summary>When the bucket is full: the start of the lift (seconds into the block), plus the person's delay.</summary>
        public static float FullAt(int key, int slot) => Delay(key, slot) + LiftAt;

        /// <summary>Is the bucket drawn full yet (the prop swaps at the start of the lift)?</summary>
        public static bool Full(float since, int key, int slot) => since >= FullAt(key, slot);

        /// <summary>Seconds into the block of the dip's contact, with the person's delay.</summary>
        public static float ContactAt(int key, int slot) => Delay(key, slot) + Draw.ContactAt;

        /// <summary>The draw laid over the standing pose: after the beat it has settled back to the loop.</summary>
        public static void Apply(ref DollPose pose, float since, int key, int slot)
        {
            float s = since - Delay(key, slot);
            var sample = Draw.Sample(s);
            Draw.Apply(ref pose, s);
            if (sample.phase == BeatPhase.Hold) pose.armR.x += Mathf.Sin(sample.clock * 5f * Mathf.PI * 2f) * 6f; // the bucket jostles as it fills
        }

        /// <summary>The walk home under the weight: lower, rolled away from the bucket, the arm straight down, a smaller bounce.</summary>
        public static void Heavy(ref DollPose pose, float stride)
        {
            float w = Mathf.Sin(stride);
            pose.squash = 0.95f;
            pose.offset.y = Mathf.Abs(w) * 0.006f - 0.012f;
            pose.roll = HeavyRoll + w * 2.5f; // the head goes to -x, away from the bucket in the right hand
            pose.lean = 5f;
            pose.armL = new Vector3(-w * 14f, 0f, -22f); // the free arm out for balance
            pose.armR = new Vector3(-30f, 0f, 8f);       // straight down: the bucket hangs upright
        }
    }
}
