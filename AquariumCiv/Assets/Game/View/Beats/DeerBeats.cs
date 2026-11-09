// P9 #426 (animation-specs B13; pure: Mathf, Vector3, DollPose and the Life types only, so Tools/AnimBench compiles it). The deer hunt's
// poses on the people: the crouch at the wood's edge where the lead points and the others nod, the stalk (B6's creep), the shot with the
// bow (up to one knee, the bow up, the draw, the aim held, the snap), the kneel with a hand on the deer's neck, the tie, the pole on the
// shoulder in step, the set-down. Seconds are real seconds into the hunt step (DeerTiming); the planner and the view read the same clock.
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    public static class DeerBeats
    {
        /// <summary>The shot (B13.a): to one knee and the bow comes up (left arm forward holding it), the right arm draws back to the face,
        /// <b>hold the aim</b> 0.5 s, the release snaps the string arm forward (the arrow leaves at <see cref="DeerTiming.ReleaseAt"/>),
        /// still while it flies, the hold, back to the crouch.</summary>
        public static readonly Beat Shoot = new Beat(
            Beat.Step(BeatPhase.Antic, DeerTiming.RiseSeconds, Ease.Smooth, PoseKey.Hold.Squash(0.8f).Lean(-4f).ArmL(62f, 0f, -10f).ArmR(48f, 0f, 6f).Extend(false)),
            Beat.Step(BeatPhase.Antic, DeerTiming.DrawSeconds, Ease.Smooth, PoseKey.Hold.Lean(-7f).ArmL(90f, 0f, -6f).ArmR(84f, 0f, -38f)),
            Beat.Step(BeatPhase.Hold, DeerTiming.AimSeconds, Ease.Linear, PoseKey.Hold.Lean(-8f).ArmR(86f, 0f, -40f)),
            Beat.Step(BeatPhase.Act, DeerTiming.ReleaseSnap, Ease.Snap, PoseKey.Hold.Lean(-3f).ArmR(70f, 0f, 24f)),
            Beat.Step(BeatPhase.Follow, DeerTiming.FlightSeconds - DeerTiming.ReleaseSnap, Ease.Out, PoseKey.Hold.ArmR(64f, 0f, 28f)),
            Beat.Step(BeatPhase.Hold, 0.5f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.4f, Ease.Smooth, PoseKey.Hold.Squash(0.74f).Lean(10f).ArmL(30f, 0f, -12f).ArmR(22f, 0f, 10f)));

        /// <summary>The kneel at the deer (B13.a follow): down to the knee, the right hand out onto its neck and held there a moment (a
        /// thank-you), then up.</summary>
        public static readonly Beat Thank = new Beat(
            Beat.Step(BeatPhase.Antic, 0.4f, Ease.Smooth, PoseKey.Hold.Squash(0.7f).Lean(26f).ArmR(40f, 0f, 8f).ArmL(18f, 0f, -12f).Extend(false)),
            Beat.Step(BeatPhase.Act, 0.3f, Ease.Out, PoseKey.Hold.Lean(30f).ArmR(62f, 0f, 6f)),
            Beat.Step(BeatPhase.Hold, 0.7f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.4f, Ease.Smooth, PoseKey.Hold.Squash(1f).Lean(0f).ArmR(18f, 0f, 10f)));

        /// <summary>The set-down by the fire (B13.b end beat): bend with the pole, both arms down, back up.</summary>
        public static readonly Beat SetDown = new Beat(
            Beat.Step(BeatPhase.Antic, 0.5f, Ease.Smooth, PoseKey.Hold.Squash(0.82f).Lean(34f).ArmL(70f, 0f, -10f).ArmR(70f, 0f, 10f).Extend(false)),
            Beat.Step(BeatPhase.Contact, 0.3f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.5f, Ease.Back, PoseKey.Hold.Squash(1f).Lean(-4f).ArmL(18f, 0f, -10f).ArmR(18f, 0f, 10f)));

        /// <summary>The beats that keep the crouched or kneeling style (no walk pose over them).</summary>
        public static bool Crouched(HuntPhase p) => p is HuntPhase.DeerCrouch or HuntPhase.DeerStalk or HuntPhase.DeerShoot or HuntPhase.DeerWatchShot
            or HuntPhase.DeerShrug or HuntPhase.DeerPullArrow or HuntPhase.DeerThank or HuntPhase.DeerTie or HuntPhase.DeerSetDown;

        /// <summary>What the hunter holds: the bow on the back walking out (the planner's slot), in the left hand from the crouch on;
        /// nothing in the carry (the pole is drawn on their shoulders by <see cref="DeerHuntStage"/>).</summary>
        public static (PropKind kind, PropSlot slot) PropOf(HuntPhase phase, PropKind kind, PropSlot slot)
        {
            if (phase is HuntPhase.DeerCarry or HuntPhase.DeerTie or HuntPhase.DeerSetDown or HuntPhase.DeerThank) return (PropKind.None, PropSlot.Hand);
            return (kind, slot);
        }

        /// <summary>The overlay for a deer-hunt beat on top of the style's pose. <paramref name="role"/>: 0 the shooter (the lead), 1 the
        /// pole runner, 2 the third. <paramref name="stride"/> and <paramref name="walking"/> come from the doll (the carry's step).</summary>
        public static void Apply(ref DollPose pose, HuntPhase phase, float since, in HuntStalk.Now stalk, int role, float stride, bool walking)
        {
            switch (phase)
            {
                case HuntPhase.DeerCrouch: Crouch(ref pose, since, role); break;
                case HuntPhase.DeerStalk: HuntBeats.Stalk(ref pose, stalk, since); break;
                case HuntPhase.DeerShoot: Shoot.Apply(ref pose, since); break;
                case HuntPhase.DeerWatchShot: Watch(ref pose, since); break;
                case HuntPhase.DeerShrug: HuntBeats.Shrug.Apply(ref pose, since); break;
                case HuntPhase.DeerPullArrow: HuntBeats.Pull.Apply(ref pose, since); break;
                case HuntPhase.DeerThank: Thank.Apply(ref pose, since); break;
                case HuntPhase.DeerTie: Tie(ref pose, since); break;
                case HuntPhase.DeerCarry: Carry(ref pose, stride, walking); break;
                case HuntPhase.DeerSetDown: SetDown.Apply(ref pose, since); break;
            }
        }

        /// <summary>Crouched at the wood's edge (squash 0.72): the lead points at the deer (arm x 90, held 1 s), the others nod twice.</summary>
        public static void Crouch(ref DollPose pose, float since, int role)
        {
            float breath = Mathf.Sin(since * 2.2f) * 0.01f;
            pose.squash = 0.72f + breath; pose.lean = 18f; pose.roll = 0f; pose.extend = false;
            pose.armL = new Vector3(28f, 0f, -12f); pose.armR = new Vector3(22f, 0f, 10f);
            if (role == 0)
            {
                float up = Mathf.Clamp01((since - 0.25f) / 0.25f) * Mathf.Clamp01((1.45f - since) / 0.25f);
                pose.armR = Vector3.Lerp(pose.armR, new Vector3(90f, 0f, 4f), Easing.Apply(Ease.Out, up));
                pose.lean = 18f - 8f * up;
            }
            else
            {
                float u = since - 0.5f;
                if (u > 0f && u < 0.8f) pose.lean += Mathf.Abs(Mathf.Sin(u / 0.4f * Mathf.PI)) * 9f; // two nods
            }
        }

        /// <summary>The others while the lead shoots: crouched and still, leaning in a little to watch the arrow.</summary>
        public static void Watch(ref DollPose pose, float since)
        {
            pose.squash = 0.7f + Mathf.Sin(since * 2f) * 0.006f; pose.lean = 12f; pose.roll = 0f; pose.extend = false;
            pose.armL = new Vector3(30f, 0f, -12f); pose.armR = new Vector3(20f, 0f, 10f);
        }

        /// <summary>Kneeling at the deer, both arms out at the cord (x 60), a fiddle while they tie, then up together (Back).</summary>
        public static void Tie(ref DollPose pose, float since)
        {
            float rise = Mathf.Clamp01((since - (DeerTiming.TieSeconds - 0.5f)) / 0.5f);
            float fiddle = Mathf.Sin(since * 9f) * 8f * (1f - rise);
            pose.squash = Mathf.Lerp(0.74f, 1f, Easing.Apply(Ease.Back, rise)); pose.lean = Mathf.Lerp(28f, 0f, rise); pose.extend = false;
            pose.armL = Vector3.Lerp(new Vector3(60f + fiddle, 0f, -8f), new Vector3(30f, 0f, -12f), rise);
            pose.armR = Vector3.Lerp(new Vector3(60f - fiddle, 0f, 8f), new Vector3(165f, 0f, 14f), rise);
        }

        /// <summary>The pole on the right shoulder (the hand up on it), the left arm swinging, a carrying bob of 0.015 u in step.</summary>
        public static void Carry(ref DollPose pose, float stride, bool walking)
        {
            pose.armR = new Vector3(165f, 0f, 14f);
            if (walking) pose.offset.y += Mathf.Abs(Mathf.Sin(stride)) * 0.015f * 0.5f;
            pose.lean = Mathf.Max(pose.lean, 4f);
        }
    }
}
