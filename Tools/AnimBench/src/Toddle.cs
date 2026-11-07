// User 2026-11-06 "Toddling is fine" (B2.d in the normal game): a toddler following its real carer in a clip. The clip's "toddle" block
// names the two dolls; the carer runs its own timeline (walks, stands, sits: the planner's day), the toddler is driven from the game's own
// pure code (View/Beats/ToddleFollow.cs) as PeopleView.Drive does: behind the walking carer, pottering in front of a standing one, the
// plop every 6-10 s; a carer standing near turns and bends to it. The toddler is drawn ToddleFollow.ToddlerScale smaller than a kid.
//
//   "toddle": { "carer": "ma", "toddler": "baby" }      (list the carer before the toddler in "dolls")
using System.Text.Json.Nodes;
using AquariumCiv.Life;
using AquariumCiv.View;
using UnityEngine;

namespace AnimBench
{
    public sealed class ToddleSpec
    {
        public string Carer, Toddler;

        public static ToddleSpec Parse(JsonObject o)
        {
            var s = new ToddleSpec { Carer = Clip.Str(o, "carer"), Toddler = Clip.Str(o, "toddler") };
            if (s.Carer == null || s.Toddler == null) throw new ClipException("toddle: needs \"carer\" and \"toddler\" (doll ids)");
            return s;
        }
    }

    public sealed partial class Simulator
    {
        /// <summary>The carer of a "toddle" block bends to its plopped toddler when it stands near (PeopleView's bendTo): what to face, or null.</summary>
        Vector2? ToddleBendTo(DollState d, bool walking, DollStyle style, float time)
        {
            var ts = clip.Toddle;
            if (ts == null || d.Spec.Id != ts.Carer || walking || style != DollStyle.Stand) return null;
            var tod = DollById(ts.Toddler);
            if (tod == null || !tod.Placed || ToddleFollow.PlopSince(tod.Spec.Seed, time) < 0f) return null;
            if ((tod.Pos - d.Pos).sqrMagnitude >= ToddleFollow.BendReach * ToddleFollow.BendReach) return null;
            return tod.Pos;
        }

        bool StepToddleDoll(DollState d, float t, float dt)
        {
            var ts = clip.Toddle;
            if (ts == null || d.Spec.Id != ts.Toddler) return false;
            var spec = d.Spec;
            var carer = DollById(ts.Carer) ?? throw new ClipException($"toddle: '{ts.Carer}' is not a doll");
            float time = clip.Clock + t;
            var prev = d.Pos;
            bool jump = !d.Placed || dt <= 0f;
            var cf = Quaternion.Euler(0f, carer.Yaw, 0f) * Vector3.back;
            var camF = Quaternion.Euler(clip.Camera.Pitch, clip.Camera.Yaw, 0f) * Vector3.forward; // batch 4: as PeopleView, the camera's ground forward
            var target = ToddleFollow.Target(spec.Seed, time, carer.Pos, new Vector2(cf.x, cf.z), carer.Walking, carer.Velocity, d.Placed ? d.Pos : carer.Pos, new Vector2(camF.x, camF.z));
            d.Pos = jump ? target : Vector2.MoveTowards(d.Pos, target, ToddleFollow.Speed((target - d.Pos).magnitude) * dt);
            float inst = !jump ? (d.Pos - prev).magnitude / dt : 0f;
            d.Speed = Mathf.Lerp(d.Speed, inst, dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 1f);
            d.Placed = true;
            if (d.Speed > Bench.WalkStarts) d.Walking = true; else if (d.Speed < Bench.WalkStops) d.Walking = false;
            if (jump) d.Velocity = Vector2.zero;
            else d.Velocity = Vector2.Lerp(d.Velocity, (d.Pos - prev) / dt, 1f - Mathf.Exp(-dt * Bench.VelocityRate));
            float want = d.Yaw;
            if (d.Walking) { if (d.Velocity.sqrMagnitude > 0.05f * 0.05f) want = Bench.Yaw(Vector2.zero, d.Velocity); }
            else if ((carer.Pos - d.Pos).sqrMagnitude > 1e-5f) want = Bench.Yaw(d.Pos, carer.Pos);
            float most = d.Walking ? Mathf.Lerp(Bench.SlowTurn, Bench.MostTurn, d.Speed / Bench.FaceFrom) : Bench.MostTurn;
            d.Yaw = dt > 0f ? d.Yaw + Mathf.Clamp(Mathf.DeltaAngle(d.Yaw, want) * (1f - Mathf.Exp(-dt * Bench.TurnRate)), -most * dt, most * dt) : want;

            var style = DollStyle.Toddle;
            d.Stride = Mathf.Repeat(d.Stride + DollMotion.Cadence(style, d.Speed) * Mathf.PI * dt, Mathf.PI * 2f);
            float phase = time + spec.Phase;
            var pose = DollMotion.Evaluate(style, phase, 1f, 0, 1, AquariumCiv.View.PropSlot.Hand, false, d.Speed, 0f, 0f, 0, d.Stride);
            if (style != d.Drew) { d.BlendFrom = d.Pose; d.BlendT = Bench.BlendSeconds; d.Drew = style; }
            ToddleFollow.ToddlerPose(ref pose, spec.Seed, time);
            d.Pose = pose;
            float plop = ToddleFollow.PlopSince(spec.Seed, time);
            var f = new DollFrame
            {
                Pose = pose, Style = style, Pos = d.Pos, BodyYaw = d.Yaw, Yaw = d.Yaw + pose.yaw, Walking = d.Walking, Speed = d.Speed,
                Progress = 0f, Since = Mathf.Max(0f, plop), Segment = 0, Step = 0, Hunt = HuntPhase.None, Prop = PropKind.None, Slot = AquariumCiv.View.PropSlot.Hand,
                EndBeat = plop >= 0f, Playing = true,
            };
            Bench.Place(ref f, spec.Scale * ToddleFollow.ToddlerScale);
            d.Last = f;
            return true;
        }
    }
}
