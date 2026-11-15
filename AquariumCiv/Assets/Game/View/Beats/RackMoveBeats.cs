// #700 (B27): a path comes over a rack; two grown-ups take it down and set it up again. Pure: Mathf and DollPose only, so Tools/AnimBench compiles it.
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The two people's poses in the rack move (B27), laid over a standing pose by <c>PeopleView.Drive</c> (and AnimBench): the Lifter
    /// (the ridge pole on her shoulder, the basket in her right hand) and the Carrier (the frames, one under each arm). The beat's clock,
    /// the stands and the rack's parts are <see cref="RackMovePlan"/>'s; this only poses the body. Times are seconds into the beat.
    /// </summary>
    public static class RackMoveBeats
    {
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float BackEase(float x) { x = Mathf.Clamp01(x); const float c = 1.70158f; float u = x - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }
        static float Ramp(float t, float a, float b) => Mathf.Clamp01((t - a) / Mathf.Max(1e-4f, b - a));
        static Vector3 Mix(Vector3 a, Vector3 b, float k) => Vector3.Lerp(a, b, k);

        /// <summary>A body turn by keys (seconds, degrees): the head turns of the antic, "here is the problem, there is the answer".</summary>
        static float Turn(float t, float shift)
        {
            t -= shift;
            if (t < 0.4f) return 0f;
            if (t < 0.55f) return 40f * Smooth(Ramp(t, 0.4f, 0.55f));
            if (t < 0.7f) return 40f;
            if (t < 0.85f) return Mathf.Lerp(40f, -40f, Smooth(Ramp(t, 0.7f, 0.85f)));
            if (t < 1.0f) return -40f;
            if (t < 1.15f) return Mathf.Lerp(-40f, 15f, Smooth(Ramp(t, 1.0f, 1.15f)));
            if (t < 1.3f) return 15f;
            return Mathf.Lerp(15f, 0f, Smooth(Ramp(t, 1.3f, 1.6f)));
        }

        /// <summary>0..1..0 over a reach: the arm goes out to the work and comes back (a fish off the bar, a fish onto it).</summary>
        static float Reach(float t, float at) => Mathf.Sin(Mathf.Clamp01((t - (at - 0.3f)) / 0.45f) * Mathf.PI);

        /// <summary>The pose at <paramref name="t"/> seconds into the beat. <paramref name="stride"/> is the doll's walk phase (radians) for the legs' bob;
        /// <paramref name="holds"/> says the right hand holds the basket (its arm stays as <see cref="DollMotion.HoldArm"/>).</summary>
        public static void Apply(ref DollPose p, RackMovePlan plan, RackRole role, float t, float stride, bool holds)
        {
            bool lifter = role == RackRole.Lifter;
            var ph = plan.PhaseAt(t);
            plan.Stand(role, t, out _, out _, out _, out _, out bool walking);
            if (walking)
            {   // the legs of a walk (a glide along the rack, the carry)
                float w = Mathf.Sin(stride);
                p.offset.y += Mathf.Abs(w) * 0.011f; p.roll = w * 4.5f;
            }
            var hold = holds ? DollMotion.HoldArm : new Vector3(0f, 0f, 14f);
            if (lifter) p.armR = hold;
            float shift = lifter ? 0f : RackMovePlan.CarrierDelay;
            switch (ph)
            {
                case RackPhase.Antic:
                {
                    p.yaw += Turn(t, shift) * (lifter ? 1f : -0.8f);
                    p.lean = 3f;
                    if (lifter)
                    {   // rubs the back of the neck (arm z 80, 0.4 s)
                        float k = Smooth(Ramp(t, 0f, 0.1f)) * (1f - Smooth(Ramp(t, 0.4f, 0.55f)));
                        float rub = Mathf.Sin(t * Mathf.PI * 2f * 5f) * 10f;
                        p.armL = Mix(p.armL, new Vector3(150f, 0f, -80f + rub), k);
                    }
                    else
                    {   // a nod at the end: "yes"
                        float n = Mathf.Sin(Ramp(t, 1.5f, 2.0f) * Mathf.PI);
                        p.lean += 7f * n;
                    }
                    break;
                }
                case RackPhase.TakeCargo:
                    CargoReach(ref p, plan, lifter, t, false);
                    break;
                case RackPhase.LiftRidge:
                {
                    float k = BackEase(Ramp(t, plan.tTake, plan.tLift));
                    if (lifter) { p.armL = Mix(p.armL, new Vector3(165f, 0f, -10f), k); p.lean = -4f * k; p.offset.y += 0.006f * k; }
                    else { p.armL = Mix(p.armL, new Vector3(60f, 0f, -8f), k); p.armR = Mix(p.armR, new Vector3(60f, 0f, 8f), k); p.lean = 3f; }
                    break;
                }
                case RackPhase.PullFrames:
                    if (lifter) { p.armL = new Vector3(150f, 0f, -22f); p.lean = 2f; p.yaw += 12f * Mathf.Sin(Ramp(t, plan.tLift + 1.2f, plan.tLift + 2.4f) * Mathf.PI); }
                    else Pull(ref p, t - plan.tLift);
                    break;
                case RackPhase.Carry:
                    if (lifter) { p.armL = new Vector3(150f, 0f, -24f); p.lean = 3f; }
                    else { p.armL = new Vector3(35f, 0f, -22f); p.armR = new Vector3(35f, 0f, 22f); p.lean = -6f; }
                    break;
                case RackPhase.Plant:
                    if (lifter)
                    {
                        p.armL = new Vector3(150f, 0f, -24f); p.lean = 2f;
                        p.yaw += 15f * Mathf.Sin(Ramp(t, plan.tCarry + 0.4f, plan.tCarry + 1.4f) * Mathf.PI) - 15f * Mathf.Sin(Ramp(t, plan.tCarry + 2.4f, plan.tCarry + 3.4f) * Mathf.PI);
                    }
                    else Plant(ref p, t - plan.tCarry);
                    break;
                case RackPhase.Raise:
                    Raise(ref p, lifter, t - plan.tPlant);
                    break;
                case RackPhase.ReHang:
                    CargoReach(ref p, plan, lifter, t, true);
                    break;
                case RackPhase.End:
                    End(ref p, lifter, t - plan.tHang);
                    break;
            }
        }

        /// <summary>The fish (or hides) off the bar one at a time into the basket, and back on again: each piece goes to one of the two, alternating.</summary>
        static void CargoReach(ref DollPose p, RackMovePlan plan, bool lifter, float t, bool back)
        {
            p.lean = 4f;
            for (int i = 0; i < plan.cargo; i++)
            {
                if ((i % 2 == 0) != lifter) continue;
                float at = back ? plan.HangAt(i) : plan.TakeAt(i);
                float k = Reach(t, at);
                if (k <= 0f) continue;
                var up = back ? new Vector3(150f, 0f, lifter ? -10f : 10f) : new Vector3(130f, 0f, lifter ? -12f : 12f);
                if (lifter) p.armL = Mix(p.armL, up, k); else p.armR = Mix(p.armR, up, k);
                p.lean = 4f + 8f * k * (back ? -0.5f : 1f);
            }
        }

        /// <summary>The Carrier's two pulls: down to a frame (crouch 0.04 u, arm x 120), a 0.15 s Snap up (to 40) and the frame goes under his arm.</summary>
        static void Pull(ref DollPose p, float u)
        {
            // B at u = 1.0 (he stands at its end from 0.5), A at u = 2.4 (at its end from 2.2)
            float PullAt(float at, float stand) => Smooth(Ramp(u, stand, at - 0.15f)) * (1f - Smooth(Ramp(u, at - 0.15f, at))); // down to the frame, then the Snap up
            float down = Mathf.Max(PullAt(1.0f, 0.5f), PullAt(2.4f, 2.2f));
            float snapped = Mathf.Max(Smooth(Ramp(u, 0.85f, 1.0f)) * (1f - Smooth(Ramp(u, 1.0f, 1.45f))), Smooth(Ramp(u, 2.25f, 2.4f)) * (1f - Smooth(Ramp(u, 2.4f, 2.85f))));
            bool aStage = u > 1.4f;
            var held = aStage ? new Vector3(35f, 0f, 22f) : Vector3.zero; // B under his right arm once pulled
            if (aStage) p.armR = held; else p.armR = new Vector3(0f, 0f, 14f);
            float armX = Mathf.Lerp(25f, 120f, down);
            armX = Mathf.Lerp(armX, 40f, snapped);
            // the pulling hand: the left once B is under the right arm, the right for B
            var pull = new Vector3(armX, 0f, aStage ? -10f : 10f);
            if (aStage) p.armL = Mix(new Vector3(0f, 0f, -14f), pull, Mathf.Max(down, snapped)); else p.armR = Mix(new Vector3(0f, 0f, 14f), pull, Mathf.Max(down, snapped));
            p.offset.y += -0.04f * down;
            p.squash = 1f - 0.1f * down;
            p.lean = 14f * down;
        }

        /// <summary>The Carrier plants B (down at 0, pressed 0.5 to 1.8), walks to A's end (1.8 to 2.6) and plants it (down 2.6, pressed 3.1 to 4.0).</summary>
        static void Plant(ref DollPose p, float u)
        {
            p.armL = new Vector3(35f, 0f, -22f); p.armR = new Vector3(35f, 0f, 22f); p.lean = -6f;
            for (int n = 0; n < 2; n++)
            {   // down 0.5 s before `from`, hands pressing from `from` to `to`, then up
                float from = n == 0 ? 0.5f : 3.1f, to = n == 0 ? 1.8f : 4.0f;
                float d = Smooth(Ramp(u, from - 0.5f, from)) * (1f - Smooth(Ramp(u, to, to + 0.3f)));
                if (d <= 0f) continue;
                float since = u - from;
                float jolt = since > 0f ? Mathf.Exp(-since / 0.9f) * Mathf.Sin(since * Mathf.PI * 2f * 4f) * 10f : 0f;
                p.armL = Mix(p.armL, new Vector3(85f + jolt, 0f, -8f), d); p.armR = Mix(p.armR, new Vector3(85f - jolt, 0f, 8f), d);
                p.offset.y += -0.04f * d; p.squash = 1f - 0.12f * d; p.lean = Mathf.Lerp(p.lean, 16f, d);
            }
            if (u > 1.8f && u < 2.6f) { p.armL = new Vector3(35f, 0f, -22f); p.armR = new Vector3(0f, 0f, 14f); p.lean = -4f; }
        }

        /// <summary>Both lift the pole off the shoulder to the forks (arm x 170, Back), drop it in, and the Lifter tugs it twice.</summary>
        static void Raise(ref DollPose p, bool lifter, float u)
        {
            float up = BackEase(Ramp(u, 0f, 1.3f));
            float drop = Smooth(Ramp(u, 1.5f, 1.75f));
            float arm = Mathf.Lerp(150f, 170f, up);
            arm = Mathf.Lerp(arm, 90f, drop);
            if (lifter)
            {
                p.armL = new Vector3(arm, 0f, -10f); p.lean = Mathf.Lerp(3f, -6f, up) + 8f * drop;
                // two tugs, 0.5 s apart
                float tug = Mathf.Max(Tug(u, 2.0f), Tug(u, 2.5f));
                p.armL = Mix(p.armL, new Vector3(110f, 0f, -10f), tug * Smooth(Ramp(u, 1.75f, 1.85f)));
                p.lean += 6f * tug;
            }
            else
            {
                p.armL = new Vector3(Mathf.Lerp(35f, 150f, up), 0f, -12f); p.armR = new Vector3(Mathf.Lerp(0f, 150f, up), 0f, 12f);
                p.armL = Mix(p.armL, new Vector3(60f, 0f, -14f), drop); p.armR = Mix(p.armR, new Vector3(30f, 0f, 14f), drop);
                p.lean = Mathf.Lerp(-6f, -3f, up);
            }
        }

        static float Tug(float u, float at) => Mathf.Sin(Mathf.Clamp01((u - at) / 0.3f) * Mathf.PI);

        /// <summary>The Lifter steps back with a hand on her hip and nods; the Carrier turns to the old spot and waves: "there, out of the way".</summary>
        static void End(ref DollPose p, bool lifter, float u)
        {
            if (lifter)
            {
                p.armL = Mix(p.armL, new Vector3(0f, 0f, -70f), BackEase(Ramp(u, 0.3f, 0.75f)));
                p.lean = 8f * Mathf.Sin(Ramp(u, 0.9f, 1.5f) * Mathf.PI);
            }
            else
            {
                float k = BackEase(Ramp(u, 0.5f, 0.9f));
                float wave = Mathf.Sin((u - 0.9f) * Mathf.PI * 2f * 2f) * 22f;
                p.armR = Mix(p.armR, new Vector3(10f, 0f, 135f + wave), k);
                p.lean = -2f * k;
            }
        }
    }
}
