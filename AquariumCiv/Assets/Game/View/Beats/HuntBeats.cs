// #362 B6 (pure: Mathf, Vector3, DollPose and the Life types only, so Tools/AnimBench compiles it). The hunt's poses: the creeping stalk,
// the throw, the shrug and the stamp, the bend that pulls a spear out of the ground or lifts a catch. Seconds are real seconds into the
// hunt step (HuntTiming); the planner and the view read the same clock.
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    public static class HuntBeats
    {
        /// <summary>The throw (animation-specs B6): rise out of the crouch and aim (hold the aim 0.35 s), snap, follow through while the
        /// spear flies, <b>Contact = the spear lands</b> (the hit the puff and the rabbit answer), hold, settle upright.</summary>
        public static readonly Beat Throw = new Beat(
            Beat.Step(BeatPhase.Antic, HuntTiming.RiseSeconds, Ease.Smooth, PoseKey.Hold.Squash(1f).Lean(-12f).ArmR(165f, 0f, 8f).ArmL(90f, 0f, -12f).Extend(false)),
            Beat.Step(BeatPhase.Antic, HuntTiming.AimSeconds, Ease.Smooth, PoseKey.Hold.Lean(-15f).ArmL(86f, 0f, -12f)),
            Beat.Step(BeatPhase.Act, HuntTiming.ActSeconds, Ease.Snap, PoseKey.Hold.Lean(25f).ArmR(60f, 0f, 8f).ArmL(30f, 0f, -12f).Offset(0f, 0f, -0.04f).Extend(true)),
            Beat.Step(BeatPhase.Follow, HuntTiming.LandAt - HuntTiming.RiseSeconds - HuntTiming.AimSeconds - HuntTiming.ActSeconds,
                Ease.Out, PoseKey.Hold.Lean(28f).ArmR(48f, 0f, 8f)),
            Beat.Step(BeatPhase.Contact, HuntTiming.ContactSeconds, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, HuntTiming.HoldSeconds, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, HuntTiming.SettleSeconds, Ease.Smooth, PoseKey.Hold.Squash(1f).Lean(0f).ArmR(18f, 0f, 10f).ArmL(18f, 0f, -10f).Offset(0f, 0f, 0f).Extend(false)));

        /// <summary>After a miss: arms out in a shrug, a stamp, the arms stay out a moment, then drop.</summary>
        public static readonly Beat Shrug = new Beat(
            // look-fix (AnimLab, batch 1): arms straight out at 70 degrees read as a stiff T-pose on the real doll (it has no elbows);
            // a shrug is the hands out low and forward, palms to the sky, and a little shake of the head (the whole doll turns -10/+10)
            Beat.Step(BeatPhase.Antic, 0.25f, Ease.Out, PoseKey.Hold.Squash(1f).Lean(-6f).ArmL(72f, 0f, -22f).ArmR(72f, 0f, 22f).Extend(false)),
            Beat.Step(BeatPhase.Hold, 0.15f, Ease.Smooth, PoseKey.Hold.Yaw(-10f)),
            Beat.Step(BeatPhase.Hold, 0.15f, Ease.Smooth, PoseKey.Hold.Yaw(10f)),
            Beat.Step(BeatPhase.Hold, 0.15f, Ease.Smooth, PoseKey.Hold.Yaw(0f)),
            Beat.Step(BeatPhase.Act, 0.10f, Ease.Snap, PoseKey.Hold.Squash(0.86f).Lean(4f)),
            Beat.Step(BeatPhase.Follow, 0.20f, Ease.Back, PoseKey.Hold.Squash(1f).Lean(-6f)),
            Beat.Step(BeatPhase.Hold, 0.40f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.40f, Ease.Smooth, PoseKey.Hold.Lean(0f).ArmL(18f, 0f, -10f).ArmR(18f, 0f, 10f)));

        /// <summary>Bend to the spear in the ground and pull it out (lean 32 to -10, Back), ending upright.</summary>
        public static readonly Beat Pull = new Beat(
            Beat.Step(BeatPhase.Antic, 0.30f, Ease.Out, PoseKey.Hold.Squash(0.9f).Lean(32f).ArmR(85f, 0f, 8f).ArmL(30f, 0f, -12f).Extend(false)),
            Beat.Step(BeatPhase.Hold, 0.15f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Back, PoseKey.Hold.Squash(1f).Lean(-10f).ArmR(60f, 0f, 8f).ArmL(18f, 0f, -10f)));

        /// <summary>The same bend, but it ends with the catch lifted (the arm already on its way up for the hold).</summary>
        public static readonly Beat Lift = new Beat(
            Beat.Step(BeatPhase.Antic, 0.30f, Ease.Out, PoseKey.Hold.Squash(0.9f).Lean(32f).ArmR(85f, 0f, 8f).ArmL(30f, 0f, -12f).Extend(false)),
            Beat.Step(BeatPhase.Hold, 0.15f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Back, PoseKey.Hold.Squash(1f).Lean(-10f).ArmR(150f, 0f, 10f).ArmL(0f, 0f, -30f)));

        /// <summary>The crouched creep: still on the spot with the spear low and back; in a step the body dips and rocks (side to side,
        /// the arms swinging); frozen when the rabbit sits up, lower and leaning back.</summary>
        public static void Stalk(ref DollPose pose, in HuntStalk.Now now, float since)
        {
            float breath = Mathf.Sin(since * 2.2f) * 0.012f;
            switch (now.move)
            {
                case HuntStalk.Move.Step:
                {
                    float s = Mathf.Sin(Mathf.PI * now.stepU);
                    float side = (now.step & 1) == 0 ? 1f : -1f;
                    pose.squash = 0.72f - 0.07f * s;
                    pose.lean = 20f + 9f * s;
                    pose.roll = side * 5f * s;
                    pose.offset.y = 0f;
                    pose.armL = new Vector3(25f + 38f * s * side, 0f, -12f);
                    pose.armR = new Vector3(40f - 18f * s * side, 0f, 10f);
                    break;
                }
                case HuntStalk.Move.Freeze:
                    pose.squash = 0.64f + breath; pose.lean = 6f; pose.roll = 0f;
                    pose.armL = new Vector3(62f, 0f, -22f); pose.armR = new Vector3(40f, 0f, 10f);
                    break;
                default:
                    pose.squash = 0.72f + breath; pose.lean = 20f; pose.roll = 0f;
                    pose.armL = new Vector3(25f, 0f, -12f); pose.armR = new Vector3(40f, 0f, 10f);
                    break;
            }
            pose.extend = false;
        }

        /// <summary>The overlay for a hunt phase on top of the style's pose (what <c>PeopleView.Drive</c> does after the blend).
        /// <paramref name="since"/> is real seconds into the step.</summary>
        public static void Apply(ref DollPose pose, HuntPhase phase, float since, in HuntStalk.Now stalk)
        {
            switch (phase)
            {
                case HuntPhase.Stalk: Stalk(ref pose, stalk, since); break;
                case HuntPhase.Strike: Throw.Apply(ref pose, since); break;
                case HuntPhase.Bolt: Shrug.Apply(ref pose, since); break;
                case HuntPhase.PullSpear: Pull.Apply(ref pose, since); break;
                case HuntPhase.PickUp: Lift.Apply(ref pose, since); break;
                case HuntPhase.HoldUp: ShowCatch(ref pose, since); break;
            }
        }

        /// <summary>Look-fix (visit loop): the catch is shown off, not hidden over the head. The fist swings up and OUT to the side
        /// (0.35 s, out-back past the key), so the dangling rabbit (PegDollView.HangRabbit) hangs clear of the head and reads against
        /// the grass from the high default camera; then two proud pumps and a hold.</summary>
        /// <summary>t4-fixes: the hold-up's arm at the top (degrees): pitched up past level, out to the side (was 112 / 72: the fist sat at
        /// shoulder height out to the side).</summary>
        public const float HoldUpArmPitch = 128f, HoldUpArmOut = 58f;

        public static void ShowCatch(ref DollPose pose, float since)
        {
            float u = Mathf.Clamp01(since / 0.35f);
            float up = Easing.Apply(Ease.Back, u);
            float pump = since > 0.35f && since < 1.15f ? Mathf.Abs(Mathf.Sin((since - 0.35f) * Mathf.PI / 0.4f)) : 0f;
            // t4-fixes (tester run 3): higher (the fist well over the head, still out to the side so the rabbit hangs clear of it) and a
            // bigger proud hop on each pump; the hunter faces the camera (PeopleView), so the catch is seen from the front.
            pose.armR = new Vector3(Mathf.LerpUnclamped(60f, HoldUpArmPitch, up) + 12f * pump, 0f, Mathf.LerpUnclamped(10f, HoldUpArmOut, up));
            pose.armL = new Vector3(0f, 0f, -24f);
            pose.lean = -8f; pose.roll = -4f * up;
            pose.offset.y = 0.012f * pump;
        }

        /// <summary>Has the spear left the hand? (The prop is drawn until then.)</summary>
        public static bool SpearInHand(HuntPhase phase, float since) => phase != HuntPhase.Strike || since < HuntTiming.ReleaseAt;
    }
}
