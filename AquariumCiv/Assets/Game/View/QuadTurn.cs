// #771: the heading of a standing or trotting quad (wolf, and the same helper for deer/dog): an eased turn on the spot,
// a dead-zone for tiny moves and a held look target, so a wolf neither snaps between headings nor twitches. Pure (System only).
using System;

namespace AquariumCiv.View
{
    /// <summary>Per-animal heading state. <see cref="yaw"/> in degrees (the pose's yaw), <see cref="rate"/> the current turn speed (deg/s).</summary>
    public struct QuadTurn
    {
        public float yaw, rate;
        /// <summary>The look target held against flicker (deg); <see cref="held"/> false until first set.</summary>
        public float target; public bool held;
        /// <summary>True once the wolf counted as moving (hysteresis: on above <see cref="MoveOn"/>, off below <see cref="MoveOff"/>).</summary>
        public bool moving;

        public const float MoveOn = 0.02f, MoveOff = 0.008f;   // world units to the goal
        public const float Dead = 7f;                          // deg: a new look target closer than this to the held one is ignored
        public const float MaxRate = 150f;                     // deg/s at the fastest turn on the spot
        public const float Accel = 700f;                       // deg/s^2: the ease in (and out, by braking early)
        public const float TrotMaxRate = 300f;                 // the old trot turn limit, kept

        public static float Delta(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f; else if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>Hysteresis on "is it moving" from its distance to the goal.</summary>
        public bool Moving(float dist)
        {
            moving = moving ? dist > MoveOff : dist > MoveOn;
            return moving;
        }

        public float pending, pendingFor; public bool hasPending;
        public const float Hold = 0.12f;                       // s: a new look target must stay put this long before the wolf follows it

        /// <summary>A candidate look heading (deg), once per frame: taken only if it is outside the dead-zone round the held one and
        /// has stayed (within the dead-zone) for <see cref="Hold"/> seconds, so a flickering target never steers. The first one is taken at once.</summary>
        public void Look(float candidate, float dt)
        {
            if (!held) { target = candidate; held = true; return; }
            if (Math.Abs(Delta(target, candidate)) <= Dead) { hasPending = false; return; }
            if (hasPending && Math.Abs(Delta(pending, candidate)) <= Dead) pendingFor += dt;
            else pendingFor = 0f;
            pending = candidate; hasPending = true;
            if (pendingFor >= Hold) { target = candidate; hasPending = false; }
        }

        /// <summary>Turn toward <see cref="target"/> with an eased rate: speeds up by <see cref="Accel"/>, never above
        /// <paramref name="maxRate"/>, and brakes so it arrives with no rate left (no overshoot, no back-and-forth).</summary>
        public void Step(float dt, float maxRate = MaxRate)
        {
            if (dt <= 0f) return;
            float d = Delta(yaw, target);
            // the fastest rate from which we can still stop within d at Accel (v^2 = 2 a d), so the turn eases out
            float stopRate = (float)Math.Sqrt(2f * Accel * 0.6f * Math.Abs(d)); // brakes at 0.6 of the acceleration: discrete steps never overshoot
            float want = Math.Min(maxRate, stopRate) * Math.Sign(d);
            float dv = Accel * dt;
            rate += Math.Max(-dv, Math.Min(dv, want - rate));
            float move = rate * dt;
            if (Math.Abs(d) <= Math.Abs(move) + 0.05f && Math.Abs(rate) < Accel * dt * 2f) { yaw = target; rate = 0f; }
            else if (Math.Sign(move) == Math.Sign(d) && Math.Abs(move) > Math.Abs(d)) { yaw = target; rate = 0f; }
            else yaw += move;
            yaw = ((yaw % 360f) + 360f) % 360f; if (yaw > 180f) yaw -= 360f;
        }
    }
}
