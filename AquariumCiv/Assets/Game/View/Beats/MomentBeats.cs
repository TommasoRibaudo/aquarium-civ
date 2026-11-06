// #370 Staged moments: where each member stands and the one-shot beats laid over their pose (pure: Mathf, Vector2, DollPose,
// the beat kit and the Life plan, so Tools/AnimBench compiles it). Plan: Life/MomentStagePlan.cs; specs: animation-specs C3, B2.e.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #370: the view half of a staged moment (<see cref="MomentStagePlan"/>): the spot and facing of a member at a plan time
    /// (<see cref="Spot"/>), and the pose on top of the loop the planner's activity draws (<see cref="Apply"/>). The ring is the
    /// kit's <see cref="RingGroup"/> (B2.e): in step on purpose, hops on each 0.5 s beat, reverses every 4, steps in with arms up
    /// every 8. Every one-shot is a kit <see cref="Beat"/>; cheers and bows start on the plan's cue, so a wave runs through
    /// the crowd (P6), and only the dance and the festival's last jump are in step.
    /// </summary>
    public static class MomentBeats
    {
        // ---------------------------------------------------------------- numbers [est] (tuned on AnimBench, clips 370-*.json)
        /// <summary>The ring's hop on each beat, the cheer bounce when it steps in, how far in (share of the radius).</summary>
        public const float RingHop = 0.03f, RingCheerHop = 0.04f, RingStepIn = 0.4f;
        /// <summary>Arms out to the side so the hands meet (z ∓95), up when the ring steps in (z ∓160).</summary>
        public const float RingArmsOut = 95f, RingArmsUp = 160f;
        /// <summary>The couple in the middle: orbit radius and speed.</summary>
        public const float CentreOrbit = 0.06f, CentreDegPerSecond = 90f;
        /// <summary>The newborn lift: the sway (degrees, Hz) while held high.</summary>
        public const float LiftSway = 4f, LiftSwayHz = 0.6f;
        /// <summary>The hug sway (B7.d): ±6° at 1 Hz; how far they close in.</summary>
        public const float HugSway = 6f, HugClose = 0.55f;

        // ---------------------------------------------------------------- the beats

        /// <summary>A cheer as the couple passes (1.1 s): dip, spring up with both arms high, hold, land.</summary>
        public static readonly Beat Cheer = new Beat(
            Beat.Step(BeatPhase.Antic, 0.14f, Ease.Smooth, PoseKey.Hold.Squash(0.88f).Lean(-4f).Arms(20f, 20f)),
            Beat.Step(BeatPhase.Act, 0.16f, Ease.Back, PoseKey.Hold.Squash(1.06f).Offset(0f, 0.045f, 0f).Arms(165f, 35f)),
            Beat.Step(BeatPhase.Hold, 0.36f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.16f, Ease.In, PoseKey.Hold.Offset(0f, 0f, 0f).Squash(0.94f)),
            Beat.Step(BeatPhase.Settle, 0.28f, Ease.Smooth, PoseKey.Base));

        /// <summary>The festival's last jump (1.0 s, all at once): a deep dip, a big jump, land.</summary>
        public static readonly Beat Jump = new Beat(
            Beat.Step(BeatPhase.Antic, 0.2f, Ease.Smooth, PoseKey.Hold.Squash(0.84f).Lean(6f).Arms(-20f, 25f)),
            Beat.Step(BeatPhase.Act, 0.18f, Ease.Out, PoseKey.Hold.Squash(1.08f).Offset(0f, 0.07f, 0f).Lean(-4f).Arms(170f, 30f)),
            Beat.Step(BeatPhase.Hold, 0.14f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.16f, Ease.In, PoseKey.Hold.Offset(0f, 0f, 0f).Squash(0.9f)),
            Beat.Step(BeatPhase.EndBeat, 0.32f, Ease.Back, PoseKey.Hold.Squash(1f).Arms(150f, 30f)));

        /// <summary>A bow (1.15 s): a breath back, a deep bow with the hands low, hold, up again.</summary>
        public static readonly Beat Bow = new Beat(
            Beat.Step(BeatPhase.Antic, 0.15f, Ease.Smooth, PoseKey.Hold.Lean(-6f)),
            Beat.Step(BeatPhase.Act, 0.28f, Ease.Out, PoseKey.Hold.Lean(38f).Arms(25f, 8f)),
            Beat.Step(BeatPhase.Hold, 0.4f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.32f, Ease.Smooth, PoseKey.Base));

        /// <summary>The spotter (1.1 s): two little jumps, the right arm pointing up at the Keeper.</summary>
        public static readonly Beat Point = new Beat(
            Beat.Step(BeatPhase.Antic, 0.1f, Ease.Smooth, PoseKey.Hold.Squash(0.9f).ArmR(60f, 0f, 10f)),
            Beat.Step(BeatPhase.Act, 0.15f, Ease.Back, PoseKey.Hold.Squash(1.05f).Offset(0f, 0.04f, 0f).ArmR(155f, 0f, 12f).Lean(-10f)),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.13f, Ease.In, PoseKey.Hold.Offset(0f, 0f, 0f).Squash(0.92f)),
            Beat.Step(BeatPhase.Act, 0.14f, Ease.Back, PoseKey.Hold.Squash(1.04f).Offset(0f, 0.035f, 0f)),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.EndBeat, 0.18f, Ease.In, PoseKey.Hold.Offset(0f, 0f, 0f).Squash(1f)));

        /// <summary>The couple join hands (2 s): reach (right hands, forward), hold, a hop together, hands stay joined.</summary>
        public static readonly Beat Join = new Beat(
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Smooth, PoseKey.Hold.ArmR(75f, 0f, -8f).Lean(6f)),
            Beat.Step(BeatPhase.Hold, 0.45f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Antic, 0.12f, Ease.Smooth, PoseKey.Hold.Squash(0.88f)),
            Beat.Step(BeatPhase.Act, 0.15f, Ease.Back, PoseKey.Hold.Squash(1.06f).Offset(0f, 0.04f, 0f).ArmL(150f, 0f, -25f)),
            Beat.Step(BeatPhase.Hold, 0.25f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.2f, Ease.In, PoseKey.Hold.Offset(0f, 0f, 0f).Squash(1f)),
            Beat.Step(BeatPhase.EndBeat, 0.48f, Ease.Smooth, PoseKey.Hold.ArmL(0f, 0f, -14f).Lean(0f)));

        /// <summary>The hug (B7.d, only with <see cref="BeatFlags.Hugs"/>): arms wide, wrap, sway, step back.</summary>
        public static readonly Beat Hug = new Beat(
            Beat.Step(BeatPhase.Antic, 0.3f, Ease.Smooth, PoseKey.Hold.Arms(30f, 100f)),
            Beat.Step(BeatPhase.Act, 0.3f, Ease.Out, PoseKey.Hold.ArmL(85f, 0f, 10f).ArmR(85f, 0f, -10f).Lean(10f).Squash(0.92f)),
            Beat.Step(BeatPhase.Hold, 1.0f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.4f, Ease.Smooth, PoseKey.Base));

        /// <summary>The newborn (3.8 s): lower to the chest (wind-up), lift high with both arms (x 165), hold and sway, back to a cradle.</summary>
        public static readonly Beat LiftBaby = new Beat(
            Beat.Step(BeatPhase.Antic, 0.4f, Ease.Smooth, PoseKey.Hold.Arms(40f, 6f).Squash(0.9f).Lean(8f)),
            Beat.Step(BeatPhase.Act, 0.45f, Ease.Back, PoseKey.Hold.Arms(165f, 8f).Squash(1.05f).Offset(0f, 0.025f, 0f).Lean(-6f)),
            Beat.Step(BeatPhase.Hold, 2.35f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.6f, Ease.Smooth, PoseKey.Hold.Arms(60f, 6f).Squash(1f).Offset(0f, 0f, 0f).Lean(0f)));
        /// <summary>The arms cradling the baby at the chest (x 60).</summary>
        static readonly Vector3 HoldL = new Vector3(60f, 0f, -6f), HoldR = new Vector3(60f, 0f, 6f);
        /// <summary>When the baby is high (the lift's act, its hold).</summary>
        public const float LiftUpFrom = 0.4f, LiftUpAt = 0.85f, LiftDownFrom = 3.2f, LiftDownAt = 3.8f;

        /// <summary>The end beat (1.2 s): cradled in the arms, a bow to the Keeper.</summary>
        public static readonly Beat Cradle = new Beat(
            Beat.Step(BeatPhase.Settle, 0.25f, Ease.Smooth, PoseKey.Hold.Arms(60f, 6f)),
            Beat.Step(BeatPhase.EndBeat, 0.4f, Ease.Out, PoseKey.Hold.Lean(26f)),
            Beat.Step(BeatPhase.Hold, 0.25f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.3f, Ease.Smooth, PoseKey.Hold.Lean(0f)));

        /// <summary>The old chief hands the band over (1.6 s).</summary>
        public static readonly Beat Give = new Beat(
            Beat.Step(BeatPhase.Antic, 0.2f, Ease.Smooth, PoseKey.Hold.ArmR(-15f, 0f, 10f)),
            Beat.Step(BeatPhase.Act, 0.3f, Ease.Out, PoseKey.Hold.ArmR(85f, 0f, 4f).Lean(8f)),
            Beat.Step(BeatPhase.Hold, 0.5f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.6f, Ease.Smooth, PoseKey.Base));

        /// <summary>The new chief takes it a breath later and puts it on (1.6 s).</summary>
        public static readonly Beat Take = new Beat(
            Beat.Step(BeatPhase.Approach, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Act, 0.3f, Ease.Out, PoseKey.Hold.ArmR(75f, 0f, 6f).Lean(6f)),
            Beat.Step(BeatPhase.Hold, 0.3f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Smooth, PoseKey.Hold.ArmR(175f, 0f, 20f).Lean(-4f)),
            Beat.Step(BeatPhase.Hold, 0.25f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.2f, Ease.Smooth, PoseKey.Base));

        /// <summary>The gold mark pops on (1.3 s): a squeeze, the pop (the hit: gold sparks), arms high, settle with a cheer.</summary>
        public static readonly Beat GoldPop = new Beat(
            Beat.Step(BeatPhase.Antic, 0.14f, Ease.Smooth, PoseKey.Hold.Squash(0.85f).Arms(10f, 20f)),
            Beat.Step(BeatPhase.Contact, 0.08f, Ease.Out, PoseKey.Hold.Squash(1.3f).Offset(0f, 0.03f, 0f).Arms(170f, 25f)),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.35f, Ease.Back, PoseKey.Hold.Squash(1f).Offset(0f, 0f, 0f)),
            Beat.Step(BeatPhase.EndBeat, 0.33f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.2f, Ease.Smooth, PoseKey.Base));

        /// <summary>Setting a log or a basket down (1.0 s): bend, lower it, straighten.</summary>
        public static readonly Beat SetDown = new Beat(
            Beat.Step(BeatPhase.Antic, 0.15f, Ease.Smooth, PoseKey.Hold.Lean(-4f)),
            Beat.Step(BeatPhase.Act, 0.3f, Ease.Out, PoseKey.Hold.Lean(32f).Arms(35f, 8f).Squash(0.94f)),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Base));

        /// <summary>The log onto the fire (1.6 s over two plan steps): arm back, throw (the hit: the fire flares), follow through.</summary>
        public static readonly Beat Toss = new Beat(
            Beat.Step(BeatPhase.Antic, 0.35f, Ease.Smooth, PoseKey.Hold.ArmR(-45f, 0f, 10f).Lean(-10f).Squash(0.95f)),
            Beat.Step(BeatPhase.Act, 0.2f, Ease.Snap, PoseKey.Hold.ArmR(100f, 0f, 6f).Lean(16f).Squash(1.03f)),
            Beat.Step(BeatPhase.Contact, 0.08f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.2f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.35f, Ease.Out, PoseKey.Hold.Lean(6f).Squash(1f)),
            Beat.Step(BeatPhase.Settle, 0.42f, Ease.Smooth, PoseKey.Base));

        // ---------------------------------------------------------------- the ring

        static readonly ConditionalWeakTable<MomentStagePlan, RingGroup> rings = new ConditionalWeakTable<MomentStagePlan, RingGroup>();

        /// <summary>The plan's ring as the kit's <see cref="RingGroup"/>, its slots turned so everyone walks the least to reach theirs.</summary>
        public static RingGroup RingOf(MomentStagePlan plan)
        {
            if (plan == null || plan.ringKeys.Count == 0) return null;
            if (rings.TryGetValue(plan, out var rg)) return rg;
            int n = plan.ringKeys.Count;
            var centre = new Vector2(plan.centreX, plan.centreZ);
            var probe = new RingGroup(plan.seed, plan.ringT0, plan.ringKeys, centre, plan.ringRadius, MomentStagePlan.RingDegPerSecond, MomentStagePlan.RingBeat, MomentStagePlan.RingReverseEvery);
            int best = 0; float bestCost = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                float cost = 0f;
                for (int i = 0; i < n; i++)
                {
                    var m = plan.Member(plan.ringKeys[(i + k) % n]);
                    var from = BeforeRing(plan, m);
                    cost += (probe.Cue(i, plan.ringT0).target - from).sqrMagnitude;
                }
                if (cost < bestCost - 1e-6f) { bestCost = cost; best = k; }
            }
            var keys = new List<int>(n);
            for (int i = 0; i < n; i++) keys.Add(plan.ringKeys[(i + best) % n]);
            rg = new RingGroup(plan.seed, plan.ringT0, keys, centre, plan.ringRadius, MomentStagePlan.RingDegPerSecond, MomentStagePlan.RingBeat, MomentStagePlan.RingReverseEvery);
            rings.Add(plan, rg);
            return rg;
        }

        static Vector2 BeforeRing(MomentStagePlan plan, MomentMember m)
        {
            if (m == null) return new Vector2(plan.centreX, plan.centreZ);
            for (int i = m.steps.Count - 1; i >= 0; i--)
                if (m.steps[i].t0 < plan.ringT0 && m.steps[i].beat != StageBeat.Ring) return new Vector2(m.steps[i].x1, m.steps[i].z1);
            return new Vector2(m.x, m.z);
        }

        /// <summary>How far in the ring has stepped (0..1): every 8 beats it steps in for 1 s (B2.e).</summary>
        public static float StepIn(float sinceRing)
        {
            if (sinceRing <= 0f) return 0f;
            float cycle = 8f * MomentStagePlan.RingBeat, inFor = 2f * MomentStagePlan.RingBeat;
            float c = sinceRing % cycle;
            return c < cycle - inFor ? 0f : Mathf.Sin(Mathf.PI * (c - (cycle - inFor)) / inFor);
        }

        static Vector2 RingSpot(MomentStagePlan plan, RingGroup rg, int key, float t, float scale)
        {
            int i = rg.IndexOf(key);
            var centre = new Vector2(plan.centreX, plan.centreZ);
            if (i < 0) return centre;
            var p = rg.Cue(i, t).target;
            return centre + (p - centre) * scale;
        }

        // ---------------------------------------------------------------- where

        /// <summary>
        /// Where member <paramref name="m"/> stands in step <paramref name="index"/> at plan time <paramref name="t"/> (box x, z), and
        /// what they face (null: the way they walk). The ring and the orbit come from the kit; the rest is the plan's straight line.
        /// </summary>
        public static Vector2 Spot(MomentStagePlan plan, MomentMember m, int index, float t, out Vector2? face)
        {
            face = null;
            var st = m.steps[index];
            float since = t - st.t0, u = Mathf.Clamp01(since / st.Length);
            var at = new Vector2(Mathf.Lerp(st.x0, st.x1, u), Mathf.Lerp(st.z0, st.z1, u));
            var centre = new Vector2(plan.centreX, plan.centreZ);
            var glass = new Vector2(plan.geo.nx, plan.geo.nz);
            switch (st.beat)
            {
                case StageBeat.Ring:
                {
                    var rg = RingOf(plan);
                    if (rg != null) at = RingSpot(plan, rg, m.key, t, 1f - RingStepIn * StepIn(t - plan.ringT0));
                    face = centre;
                    return at;
                }
                case StageBeat.Centre:
                {
                    // The couple turn round each other in the middle, hands joined.
                    bool first = plan.members.Count > 0 && plan.members[0].key == m.key;
                    float a = (since * CentreDegPerSecond) * Mathf.Deg2Rad + BeatHash.U01(plan.seed, 3, 370) * Mathf.PI * 2f + (first ? 0f : Mathf.PI);
                    at = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * CentreOrbit;
                    face = centre - (at - centre); // the other one
                    return at;
                }
            }
            if (st.ring >= 0)
            {
                var rg = RingOf(plan);
                if (rg != null) at = RingSpot(plan, rg, m.key, plan.ringT1, st.ringScale); // where the ring stopped
            }
            if (st.beat == StageBeat.Join && BeatFlags.Hugs)
            {
                float close = Easing.Apply(Ease.Smooth, (since - 0.3f) / 0.4f) * (1f - Easing.Apply(Ease.Smooth, (since - 1.6f) / 0.4f));
                at = Vector2.Lerp(at, centre, HugClose * close);
            }
            if (st.faceGlass) face = at + glass;
            else if (st.hasFace) face = new Vector2(st.faceX, st.faceZ);
            if (st.Moves && st.beat != StageBeat.Vow && st.beat != StageBeat.Speech) face = null; // walking: they face the way they go
            return at;
        }

        // ---------------------------------------------------------------- how

        /// <summary>The one-shot of this step, and how far into it the member is (seconds; negative: not yet).</summary>
        public static Beat OneShot(MomentStagePlan plan, MomentMember m, int index, float t, out float since)
        {
            var st = m.steps[index];
            since = t - st.t0 - st.cue;
            switch (st.beat)
            {
                case StageBeat.AisleCheer: return Cheer;
                case StageBeat.Cheer: return st.kind == ActivityKind.Cheer ? null : Cheer; // a Cheer activity already loops its own
                case StageBeat.Bow: return Bow;
                case StageBeat.Jump: return Jump;
                case StageBeat.Point: return Point;
                case StageBeat.Join: return BeatFlags.Hugs ? Hug : Join;
                case StageBeat.LiftBaby: return LiftBaby;
                case StageBeat.Cradle: return Cradle;
                case StageBeat.Give: return Give;
                case StageBeat.Take: return Take;
                case StageBeat.GoldPop: return GoldPop;
                case StageBeat.SetDown: return SetDown;
                case StageBeat.TossWind: case StageBeat.Toss: return Toss;
                default: return null;
            }
        }

        /// <summary>
        /// Lays the step's beat over the loop pose: the one-shot (a cheer, a bow, the lift, the toss), the ring dance (arms out, a
        /// hop each beat, arms up when it steps in), the couple's joined hands. <paramref name="babyLift"/>: how high the newborn is
        /// (0 in the arms, 1 held up), for the doll's bundle.
        /// </summary>
        public static void Apply(ref DollPose pose, MomentStagePlan plan, MomentMember m, int index, float t, out float babyLift)
        {
            babyLift = 0f;
            var st = m.steps[index];
            float since = t - st.t0;
            switch (st.beat)
            {
                case StageBeat.Ring:
                {
                    float s = t - plan.ringT0, beat = MomentStagePlan.RingBeat;
                    float hop = Mathf.Abs(Mathf.Sin(Mathf.PI * Mathf.Repeat(s, beat) / beat));
                    float up = StepIn(s);
                    float side = Mathf.Lerp(RingArmsOut, RingArmsUp, up);
                    pose.armL = new Vector3(0f, 0f, -side);
                    pose.armR = new Vector3(0f, 0f, side);
                    pose.offset = new Vector3(0f, RingHop * hop + RingCheerHop * up * hop, 0f);
                    pose.squash = 1f + 0.05f * (hop - 0.5f);
                    pose.lean = -6f * up; pose.roll = 0f; pose.yaw = 0f;
                    return;
                }
                case StageBeat.Centre:
                {
                    float s = t - plan.ringT0, beat = MomentStagePlan.RingBeat;
                    float hop = Mathf.Abs(Mathf.Sin(Mathf.PI * Mathf.Repeat(s, beat) / beat));
                    pose.armR = new Vector3(70f, 0f, -8f); // right hands joined across the middle
                    pose.armL = new Vector3(0f, 0f, -60f);
                    pose.offset = new Vector3(0f, 0.025f * hop, 0f);
                    pose.squash = 1f + 0.04f * (hop - 0.5f);
                    pose.lean = 4f; pose.roll = 0f; pose.yaw = 0f;
                    return;
                }
                case StageBeat.WalkIn:
                {
                    // Hand in hand: the inner arm reaches out to the other (the first of the couple walks on the other's right).
                    bool first = plan.members.Count > 0 && plan.members[0].key == m.key;
                    if (first) pose.armL = new Vector3(8f, 0f, -30f); else pose.armR = new Vector3(8f, 0f, 30f);
                    return;
                }
                case StageBeat.Settle:
                {
                    // The baby comes round to the front of the chest, ready to be lifted.
                    float k = Easing.Apply(Ease.Smooth, since / 0.3f);
                    pose.armL = Vector3.Lerp(pose.armL, HoldL, k); pose.armR = Vector3.Lerp(pose.armR, HoldR, k);
                    return;
                }
            }
            var b = OneShot(plan, m, index, t, out float bs);
            if (b == null) return;
            if (st.beat == StageBeat.Toss && bs < b.ContactAt) bs = b.ContactAt; // the second half starts on the throw, never early
            bool cued = st.beat is StageBeat.AisleCheer or StageBeat.Cheer or StageBeat.Bow or StageBeat.Jump or StageBeat.SetDown;
            if (cued && bs < 0f) return;
            var under = pose;
            if (st.beat is StageBeat.LiftBaby or StageBeat.Cradle) { under.armL = HoldL; under.armR = HoldR; } // from the cradle, not from the loop's arms
            pose = b.Pose(under, bs);
            if (st.beat == StageBeat.LiftBaby)
            {
                float up = Easing.Apply(Ease.Back, Mathf.InverseLerp(LiftUpFrom, LiftUpAt, bs)) * (1f - Easing.Apply(Ease.Smooth, Mathf.InverseLerp(LiftDownFrom, LiftDownAt, bs)));
                babyLift = Mathf.Clamp(up, 0f, 1.1f);
                if (bs > LiftUpAt && bs < LiftDownFrom) pose.roll += LiftSway * Mathf.Sin(2f * Mathf.PI * LiftSwayHz * (bs - LiftUpAt)); // a slow sway up there
            }
            else if (st.beat == StageBeat.Join && BeatFlags.Hugs && bs > 0.6f && bs < 1.6f)
                pose.roll += HugSway * Mathf.Sin(2f * Mathf.PI * (bs - 0.6f));
            if (st.beat == StageBeat.TossWind && pose.hit > 0f) pose.hit = 0f; // the contact is the second step's
        }

        /// <summary>Did step <paramref name="index"/> start in (<paramref name="from"/>, <paramref name="to"/>]? The view's one-time cues (the spotter's "!").</summary>
        public static bool StepStarted(MomentMember m, int index, float from, float to)
        {
            if (m == null || index < 0 || index >= m.steps.Count) return false;
            float at = m.steps[index].t0;
            return from < at && at <= to;
        }
    }
}
