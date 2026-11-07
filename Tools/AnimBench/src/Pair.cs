// #363 AnimBench: a pair moment in a clip (hand over, walk together, hug). The clip's "pair" block names the kind and the two dolls; the bench
// then drives them from the game's own pure code (View/Beats/PairStage.cs, PairBeats.cs), as PeopleView does: the pair's shared clock says where
// each stands and what it holds, and the beat lies over the loop pose. (Talk needs no block: two dolls with activity Chat and a partner.)
//
//   "pair": { "kind": "HandOver", "members": ["giver", "taker"], "t0": 1, "prop": "Spear", "window": 0 }
//   "pair": { "kind": "WalkTogether", "members": ["lead", "side"], "t0": 0.5, "heading": [1, 0], "seconds": 7, "speed": 0.14 }
//   "pair": { "kind": "Hug", "members": ["a", "b"], "t0": 1, "force": true }
using System.Text.Json.Nodes;
using AquariumCiv.Life;
using AquariumCiv.View;
using UnityEngine;

namespace AnimBench
{
    public sealed class PairSpec
    {
        public PairKind Kind;
        public string A, B;
        public float T0, Window, Seconds = 7f, Speed = 0.14f;
        public PropKind Prop = PropKind.Spear;
        public Vector2 Heading = new Vector2(1f, 0f);
        public bool Force, LiftSecond;
        public int Seed = 363;

        public static PairSpec Parse(JsonObject o)
        {
            var p = new PairSpec
            {
                Kind = Clip.Enum<PairKind>(Clip.Str(o, "kind") ?? "HandOver", "pair kind"),
                T0 = Clip.Num(o, "t0", 0f), Window = Clip.Num(o, "window", 0f), Seconds = Clip.Num(o, "seconds", 7f), Speed = Clip.Num(o, "speed", 0.14f),
                Force = Clip.Bool(o, "force", false), LiftSecond = Clip.Bool(o, "liftSecond", false), Seed = Clip.Int(o, "seed", 363),
            };
            if (Clip.Str(o, "prop") != null) p.Prop = Clip.Enum<PropKind>(Clip.Str(o, "prop"), "pair prop");
            if (o["heading"] != null) p.Heading = Clip.XZ(o["heading"], "pair heading");
            if (o["members"] is JsonArray m && m.Count == 2) { p.A = m[0].GetValue<string>(); p.B = m[1].GetValue<string>(); }
            else throw new ClipException("pair: needs members [first, second] (doll ids)");
            return p;
        }
    }

    public sealed partial class Simulator
    {
        PairEvent pairEv;

        void SetUpPair()
        {
            var ps = clip.Pair;
            if (ps == null) return;
            var a = clip.Doll(ps.A) ?? throw new ClipException($"pair: '{ps.A}' is not a doll");
            var b = clip.Doll(ps.B) ?? throw new ClipException($"pair: '{ps.B}' is not a doll");
            var pa = a.At(ps.T0).From; var pb = b.At(ps.T0).From;
            BeatFlags.Hugs = ps.Force || BeatFlags.Hugs;
            switch (ps.Kind)
            {
                case PairKind.HandOver: pairEv = PairStage.HandOver(ps.Seed, ps.T0, a.Seed, b.Seed, pa, pb, ps.Prop, ps.Window, 3f); break;
                case PairKind.WalkTogether: pairEv = PairStage.WalkTogether(ps.Seed, ps.T0, a.Seed, b.Seed, pa, ps.Heading, ps.Seconds, ps.Speed); break;
                default: pairEv = PairStage.Hug(ps.Seed, ps.T0, a.Seed, b.Seed, pa, pb, ps.Force, ps.LiftSecond); break;
            }
            if (pairEv == null) throw new ClipException("pair: the event could not be staged (too far apart, or hugs are off: set \"force\": true)");
        }

        bool StepPairDoll(DollState d, float t, float dt)
        {
            var spec = d.Spec;
            var pairEv = this.pairEv != null && t >= this.pairEv.start && !this.pairEv.IsOver(t) && this.pairEv.group.IndexOf(spec.Seed) >= 0 ? this.pairEv : MealEventFor(spec.Seed, t); // #365: the meal's bowl chain too
            if (pairEv == null) return false;
            int i = pairEv.group.IndexOf(spec.Seed);
            var cue = PairStage.CueOf(pairEv, i, t);
            int si = spec.IndexAt(Mathf.Max(0f, t));
            var seg = spec.Segments[si];
            var prev = d.Pos;
            bool jump = !d.Placed || dt <= 0f;
            // PeopleView: the pair's pace, catching up as it goes
            d.Pos = jump ? cue.pos : Vector2.MoveTowards(d.Pos, cue.pos, Mathf.Min(0.75f, cue.speed * 1.1f + (cue.pos - d.Pos).magnitude * 1.5f) * dt);
            float inst = !jump ? (d.Pos - prev).magnitude / dt : 0f;
            d.Speed = Mathf.Lerp(d.Speed, inst, dt > 0f ? 1f - Mathf.Exp(-dt * 10f) : 1f);
            d.Placed = true;
            if (d.Speed > Bench.WalkStarts) d.Walking = true; else if (d.Speed < Bench.WalkStops) d.Walking = false;
            bool walking = d.Walking;
            var style = walking ? DollStyle.Walk : pairEv.takerSeated && i == 1 ? DollStyle.Sit : DollStyle.Stand; // #365: a bowl handed to someone who stays seated
            if (jump) d.Velocity = Vector2.zero;
            else d.Velocity = Vector2.Lerp(d.Velocity, (d.Pos - prev) / dt, 1f - Mathf.Exp(-dt * Bench.VelocityRate));
            float want = d.Yaw;
            if (walking) { if (d.Velocity.sqrMagnitude > 0.05f * 0.05f) want = Bench.Yaw(Vector2.zero, d.Velocity); }
            else if ((cue.face - d.Pos).sqrMagnitude > 1e-5f) want = Bench.Yaw(d.Pos, cue.face);
            float most = walking ? Mathf.Lerp(Bench.SlowTurn, Bench.MostTurn, d.Speed / Bench.FaceFrom) : Bench.MostTurn;
            d.Yaw = dt > 0f ? d.Yaw + Mathf.Clamp(Mathf.DeltaAngle(d.Yaw, want) * (1f - Mathf.Exp(-dt * Bench.TurnRate)), -most * dt, most * dt) : want;

            var propKind = cue.propMode == 1 ? cue.prop : seg.Prop;
            var propSlot = cue.propMode == 1 ? cue.slot : seg.Slot;
            bool holds = propKind != PropKind.None && propKind != PropKind.Mask;
            d.Stride = Mathf.Repeat(d.Stride + DollMotion.Cadence(style, d.Speed) * Mathf.PI * dt, Mathf.PI * 2f);
            float phase = clip.Clock + t + spec.Phase;
            var pose = DollMotion.Evaluate(style, phase, 1f, 0, 1, propSlot, holds, d.Speed, 0f, Mathf.Max(0f, t - pairEv.start), 0, d.Stride);
            if (style != d.Drew)
            {
                d.BlendFrom = d.Pose;
                d.BlendT = pose.hidden || pose.lying || d.Pose.hidden || d.Pose.lying || jump ? Bench.BlendSeconds : 0f;
                d.Drew = style;
            }
            if (d.BlendT < Bench.BlendSeconds)
            {
                d.BlendT += dt;
                pose = DollMotion.Blend(d.BlendFrom, pose, Mathf.SmoothStep(0f, 1f, d.BlendT / Bench.BlendSeconds));
            }
            if (pairEv.kind == PairKind.WalkTogether && i == 1) d.Stride = dolls.Find(x => x.Spec.Seed == pairEv.group.KeyOf(0)).Stride; // PeopleView: strides in step
            PairStage.Apply(pairEv, i, t, ref pose, d.Stride);
            d.Pose = pose;
            string other = dolls.Find(x => x.Spec.Seed == pairEv.group.KeyOf(1 - i)).Spec.Id;
            var f = new DollFrame
            {
                Pose = pose, Style = style, Pos = d.Pos, BodyYaw = d.Yaw, Yaw = d.Yaw + pose.yaw, Walking = walking, Speed = d.Speed,
                Progress = 0f, Since = Mathf.Max(0f, t - pairEv.start), Segment = si, Step = 0, Hunt = HuntPhase.None, Prop = propKind, Slot = propSlot,
                HitTarget = pose.hit > 0.5f ? other : null, InStep = pairEv.kind == PairKind.WalkTogether, // a walking pair strides in step (exempt from P6)
            };
            Bench.Place(ref f, spec.Scale);
            d.Last = f;
            return true;
        }
    }
}
