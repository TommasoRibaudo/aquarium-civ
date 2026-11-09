using System;

namespace AquariumCiv.Life
{
    /// <summary>
    /// #362: the hunt's beats in REAL seconds, shared by the planner (how long each step lasts, where the spear lands) and the view
    /// (the throw's pose, the spear's flight, the rabbit's answer), so the two never disagree. Pure: AnimBench compiles it.
    /// The throw (<see cref="HuntPhase.Strike"/>): rise from the crouch and aim, snap, the spear flies, it lands, the hold.
    /// </summary>
    public static class HuntTiming
    {
        // ---- the throw (Strike), seconds from the start of the step
        public const float RiseSeconds = 0.40f, AimSeconds = 0.35f, ActSeconds = 0.12f;
        /// <summary>The spear leaves the hand a little into the snap (the arm passes about 110 degrees).</summary>
        public const float ReleaseAt = RiseSeconds + AimSeconds + 0.05f;
        /// <summary>Slower than a real throw, so the eye can follow it (animation-specs B6).</summary>
        public const float FlightSeconds = 0.45f;
        public const float LandAt = ReleaseAt + FlightSeconds;
        public const float ContactSeconds = 0.06f, HoldSeconds = 0.35f, SettleSeconds = 0.34f;
        public const float StrikeSeconds = LandAt + ContactSeconds + HoldSeconds + SettleSeconds;
        /// <summary>The spear's flight: its apex above the straight line (box units).</summary>
        public const float FlightApex = 0.15f;

        // ---- after the throw
        /// <summary>The shrug and the stamp after a miss; the pull that frees the spear (bend, then up); the bend that lifts the catch.</summary>
        public const float BoltSeconds = 1.8f, PullSeconds = 0.8f, PickUpSeconds = 0.8f;
        /// <summary>The bend is the first half of a pull or a pick-up: the spear comes out / the rabbit comes up at the end of it.</summary>
        public const float PullTakeAt = 0.45f, PickTakeAt = 0.45f;
        public const float HoldUpSeconds = 1.8f;
        /// <summary>The slow proud walk home with the catch, relative to a normal walk.</summary>
        public const float ProudWalk = 0.8f;
        /// <summary>The rabbit falls over this fast after the hit (it jumps 0.05 u first).</summary>
        public const float FallSeconds = 0.25f;

        // ---- geometry (box units)
        /// <summary>The stalk starts this far from the rabbit and ends at the throw point, <see cref="ThrowRange"/> from it.</summary>
        public const float StalkRange = 0.75f, ThrowRange = 0.40f;
        /// <summary>A miss lands this far past the rabbit (plus a side step), the pull-up spot is this far short of the spear.</summary>
        public const float MissPast = 0.12f, MissPastMore = 0.08f, MissSide = 0.09f, StandOff = 0.10f;
    }

    /// <summary>
    /// P9 #426 (animation-specs B13): the deer hunt's beats in REAL seconds, shared by the planner and the view as <see cref="HuntTiming"/>
    /// is for the rabbits. The shot (<see cref="HuntPhase.DeerShoot"/>): up to one knee, the bow comes up, the draw, hold the aim, the
    /// release, the arrow's flight, it lands (the contact), the hold. Distances in box units. Pure: AnimBench compiles it.
    /// </summary>
    public static class DeerTiming
    {
        // ---- the shot, seconds from the start of the step
        public const float RiseSeconds = 0.45f, DrawSeconds = 0.5f, AimSeconds = 0.5f, ReleaseSnap = 0.1f;
        /// <summary>The string arm snaps forward and the arrow leaves.</summary>
        public const float ReleaseAt = RiseSeconds + DrawSeconds + AimSeconds;
        /// <summary>The arrow's flight (slower than life so it can be followed) and its apex over the straight line.</summary>
        public const float FlightSeconds = 0.4f, FlightApex = 0.06f;
        public const float LandAt = ReleaseAt + FlightSeconds;
        public const float ShootSeconds = LandAt + 0.9f;
        /// <summary>The crouch at the wood's edge with the lead's point (arm x 90, 1 s) and the others' nods.</summary>
        public const float CrouchSeconds = 1.8f;
        /// <summary>A hit: the kneel with a hand on the neck (the thank-you); the tie under the pole; the set-down by the fire.</summary>
        public const float ThankSeconds = 1.8f, TieSeconds = 1.8f, SetDownSeconds = 1.3f, TakePoleSeconds = 0.9f;
        /// <summary>A miss: the shrug together (B6's), the pull that frees the arrow from the ground.</summary>
        public const float ShrugSeconds = HuntTiming.BoltSeconds, PullSeconds = 0.8f, PullTakeAt = 0.36f;

        // ---- geometry
        /// <summary>The crouch (the lead points) this far from the deer, the stalk ends at the shot point this far.</summary>
        public const float CrouchRange = 1.1f, ShotRange = 0.6f;
        /// <summary>Hunters walk out in a line this far apart and stalk side by side this far apart.</summary>
        public const float LineGap = 0.15f, SideGap = 0.2f;
        /// <summary>A miss lands this far past the deer (plus a side step); the shooter stands this far short of the arrow to pull it.</summary>
        public const float MissPast = 0.2f, MissSide = 0.06f, StandOff = 0.1f;
        /// <summary>The pole (0.35 u): its carriers stand at its two ends; the deer is set down this far from the fire.</summary>
        public const float PoleHalf = 0.175f, SetDownFromFire = 0.42f;
        /// <summary>The carry home is slower than a walk; the runner who fetches the pole runs.</summary>
        public const float CarryPace = 0.7f, RunPace = 1.9f;
        /// <summary>Nobody hunts at the water (B14.a): never a deer within this of the pond.</summary>
        public const float WaterKeep = 0.5f;
        /// <summary>At the release every deer this near the target bolts (P3).</summary>
        public const float BoltReach = 1.5f;
    }

    /// <summary>The stalk's rhythm: seven creeping steps (0.05 u in 0.5 s, then still 0.7 s), and once or twice the rabbit sits up and the
    /// hunter freezes until it grazes again. Planner and view read the same function of the seconds into the stalk.</summary>
    public static class HuntStalk
    {
        public const int Steps = 7;
        public const float StepSeconds = 0.5f, StillSeconds = 0.7f;
        /// <summary>The freezes come after these steps (0-based).</summary>
        public const int FreezeAfterA = 2, FreezeAfterB = 5;

        public enum Move : byte { Step, Still, Freeze }

        public struct Now
        {
            public Move move;
            /// <summary>0..1 through the creeping step (Step only).</summary>
            public float stepU;
            /// <summary>How far along the stalk's path the hunter is, 0..1.</summary>
            public float fraction;
            public int step;
        }

        /// <summary>Total seconds of the stalk; the last still absorbs <paramref name="extra"/> (the wait for the rabbit to stop hopping).</summary>
        public static float Duration(float freezeA, float freezeB, float extra = 0f) => Steps * (StepSeconds + StillSeconds) + freezeA + freezeB + extra;

        /// <summary>Where the stalk is <paramref name="s"/> seconds in.</summary>
        public static Now Sample(float s, float freezeA, float freezeB)
        {
            float t = Math.Max(0f, s);
            for (int k = 0; k < Steps; k++)
            {
                if (t < StepSeconds) return new Now { move = Move.Step, stepU = t / StepSeconds, step = k, fraction = (k + Smooth(t / StepSeconds)) / Steps };
                t -= StepSeconds;
                if (t < StillSeconds) return new Now { move = Move.Still, step = k, fraction = (k + 1f) / Steps };
                t -= StillSeconds;
                float freeze = k == FreezeAfterA ? freezeA : k == FreezeAfterB ? freezeB : 0f;
                if (freeze > 0f)
                {
                    if (t < freeze) return new Now { move = Move.Freeze, step = k, fraction = (k + 1f) / Steps };
                    t -= freeze;
                }
            }
            return new Now { move = Move.Still, step = Steps - 1, fraction = 1f };
        }

        static float Smooth(float u) => u * u * (3f - 2f * u);

        /// <summary>Is the rabbit sitting up (the hunter frozen) at this second of the stalk?</summary>
        public static bool RabbitUp(float s, float freezeA, float freezeB) => Sample(s, freezeA, freezeB).move == Move.Freeze;
    }
}
