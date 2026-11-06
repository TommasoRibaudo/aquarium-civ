// User 2026-11-06 "Toddling is fine" (B2.d in the normal game): a walking-age baby (LifePlanner.ToddleAge to 4) toddles after its carer
// for the short walks about the camp instead of riding in their arms (LifePlanner.Toddle.cs decides when; #192's cap keeps it cheap).
// Pure (Mathf, Vector2/3 and the beat kit only), so Tools/AnimBench steps exactly this code: PeopleView.Drive and the bench's "toddle"
// block both ask Target / Speed / PlopSince and lay ToddlerPose / CarerPose over the doll's loop pose.
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// A toddler following its carer: 0.15 u behind (a little to one side) while the carer walks, pottering on a short arc in front of
    /// them while they stand or sit. Every 6–10 s it plops down on its bottom (squash, a drop, Back), sits a beat and gets up; a carer
    /// standing near turns and bends to it. Everything is a pure function of (the toddler's key, the clock, the carer's spot): the plop
    /// clock is the kit's hash, never the sim's dice.
    /// </summary>
    public static class ToddleFollow
    {
        /// <summary>Behind the walking carer, and to one side (so the two don't line up in the camera's view).</summary>
        public const float Behind = 0.15f, Side = 0.05f;
        /// <summary>The potter: an arc of this radius at a standing carer's front-left or front-right (<see cref="PotterAside"/> rad off their
        /// front, by hash, so the carer's body hides it less), swinging ±<see cref="PotterSwing"/> rad at about 0.08 u/s.</summary>
        public const float Potter = 0.17f, PotterAside = 0.7f, PotterSwing = 0.9f, PotterRate = 0.5f;
        /// <summary>The fastest a toddler wobbles (a grown-up walks about 0.3 u/s), and how hard it hurries to close a gap (per second).</summary>
        public const float MaxSpeed = 0.36f, Least = 0.06f, Hurry = 2.5f;
        /// <summary>A plop every 6–10 s of toddling, sitting this long (the plop beat and the getting up).</summary>
        public const float WalkLo = 6f, WalkHi = 10f, PlopFor = 1.7f;
        /// <summary>A toddler is drawn this much smaller than a kid (the kid doll × 0.8), so it reads as the little one.</summary>
        public const float ToddlerScale = 0.8f;
        /// <summary>A carer bends to a plopped toddler within this (centre to centre).</summary>
        public const float BendReach = 0.35f;
        const int Salt = 1926;

        /// <summary>The plop: down onto its bottom (squash 0.75, down 0.03 u, Back), a beat sitting, up again.</summary>
        public static readonly Beat Plop = new Beat(
            Beat.Step(BeatPhase.Act, 0.25f, Ease.Back, PoseKey.Hold.Squash(0.72f).Offset(0f, -0.035f, 0f).Lean(-6f).Arms(30f, 45f).Roll(0f)),
            Beat.Step(BeatPhase.Hold, 0.8f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.45f, Ease.Back, PoseKey.Base));

        /// <summary>The carer turns to it and bends (lean 30, hands down to it), holds, straightens.</summary>
        public static readonly Beat Bend = new Beat(
            Beat.Step(BeatPhase.Approach, 0.3f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Smooth, PoseKey.Hold.Lean(30f).Arms(60f, 10f)),
            Beat.Step(BeatPhase.Hold, 0.45f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Settle, 0.4f, Ease.Smooth, PoseKey.Base));

        /// <summary>One toddle-and-plop cycle of this toddler (its own length, so two toddlers never plop in step).</summary>
        public static float Cycle(int key) => BeatHash.Range(WalkLo, WalkHi, key, 1, Salt) + PlopFor;

        /// <summary>Seconds into the plop now (−1 while toddling).</summary>
        public static float PlopSince(int key, float t)
        {
            float L = Cycle(key);
            float c = Mathf.Repeat(t + BeatHash.U01(key, 2, Salt) * L, L);
            return c >= L - PlopFor ? c - (L - PlopFor) : -1f;
        }

        /// <summary>Where the toddler wants to be now. <paramref name="carerFace"/>: the way the carer faces (unit); <paramref name="carerWalking"/>
        /// with <paramref name="carerHeading"/> (unit) the way they go. Plopped: where it sits (<paramref name="current"/>).</summary>
        public static Vector2 Target(int key, float t, Vector2 carer, Vector2 carerFace, bool carerWalking, Vector2 carerHeading, Vector2 current)
        {
            if (PlopSince(key, t) >= 0f) return current;
            float aside = BeatHash.Pick(2, key, 3, Salt) == 0 ? -1f : 1f; // its side of the carer
            if (carerWalking && carerHeading.sqrMagnitude > 1e-6f)
            {
                var h = carerHeading.normalized;
                var right = new Vector2(h.y, -h.x);
                return carer - h * Behind + right * (Side * aside);
            }
            var f = carerFace.sqrMagnitude > 1e-6f ? carerFace.normalized : new Vector2(0f, -1f);
            float a0 = Mathf.Atan2(f.y, f.x) + aside * PotterAside;
            float a = a0 + PotterSwing * Mathf.Sin(t * PotterRate + BeatHash.U01(key, 4, Salt) * 6.283f);
            return carer + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Potter;
        }

        /// <summary>How fast it toddles to a target <paramref name="gap"/> away (u/s).</summary>
        public static float Speed(float gap) => Mathf.Min(MaxSpeed, Least + gap * Hurry);

        /// <summary>The toddler's pose: the toddle loop is the doll's (<c>DollStyle.Toddle</c>); the plop lies over it.</summary>
        public static void ToddlerPose(ref DollPose p, int key, float t)
        {
            float s = PlopSince(key, t);
            if (s >= 0f) Plop.Apply(ref p, s);
        }

        /// <summary>A carer standing near: the bend while the toddler sits (−1: nothing). Returns true while it bends (face the toddler).</summary>
        public static bool CarerPose(ref DollPose p, int toddlerKey, float t)
        {
            float s = PlopSince(toddlerKey, t);
            if (s < 0f || s >= Bend.Duration) return false;
            Bend.Apply(ref p, s);
            return true;
        }
    }
}
