// #776 (animation-specs B81.b-d): how the people use the founder's statue: kids climb its two-step base, the anniversary flowers, an old one
// sits at its foot (see StatueScenes.cs for the nod in passing and the sweep).
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    public static partial class StatueScenes
    {
        // ================================================================== B81.b kids climb the base
        /// <summary>
        /// B81.b (about 9 s): a kid walks to the plinth's camera side, reaches up, pulls onto the lower step (0 → 0.03 u in 0.5 s, a wiggle), steps onto
        /// the upper (0.06 u in all) and stands against the figure's leg with one arm round it; looks up at the face, waves to a kid below (Nobody: none);
        /// hops down (squash, dust). A grown-up (<paramref name="grown"/>) stands within <see cref="GrownNear"/> watching the whole time. Feet on the steps
        /// only, never on the figure.
        /// </summary>
        public static CampScene Climb(int kid, Vector2 kidAt, int grown, Vector2 grownAt, int below, Vector2 belowAt, Vector2 spot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.StatueClimb, t0, spot) { holdFocus = true };
            var T = new Track(s);
            int mk = s.Add(kid), mg = s.Add(grown);
            var foot = Front(spot, toCam, KneelAt); var onLow = Front(spot, toCam, 0.085f); var onUp = Front(spot, toCam, 0.045f);
            var gStand = spot + Rot(toCam, 65f) * GrownNear; var bStand = spot + Rot(toCam, -40f) * 0.3f;
            // the grown-up comes to stand close, watching
            float wg = T.WalkTo(mg, 0f, gStand, spot, Walk, grownAt);
            float walk = T.WalkTo(mk, 0f, foot, spot, KidWalk, kidAt);
            float t = Mathf.Max(walk, 0.4f);
            // reach up (x 170), pull onto the lower step, step onto the upper, stand against the figure's leg
            T.Part(mk, t, t + 0.5f, foot, spot, DollStyle.Stand, (ref DollPose p, float u, float len) => { float k = In(u, 0f, 0.3f); p.armL = Lerp(p.armL, new Vector3(170f, 0f, -10f), k); p.armR = Lerp(p.armR, new Vector3(170f, 0f, 10f), k); });
            t += 0.5f;
            T.Part(mk, t, t + 0.5f, onLow, spot, DollStyle.Stand, Rise(0f, Lift1, 0.5f, true), PropKind.None, 0.25f);
            t += 0.5f;
            s.Fx(t, CampFxKind.Dust, G(foot, 0.005f), 0.25f, 1);
            T.Part(mk, t, t + 0.4f, onUp, spot, DollStyle.Stand, Rise(Lift1, Lift2, 0.4f, false), PropKind.None, 0.2f);
            t += 0.4f;
            T.Part(mk, t, t + 1.2f, onUp, spot, DollStyle.Stand, Hug(Lift2));
            t += 1.2f;
            T.Part(mk, t, t + 1.0f, onUp, spot, DollStyle.LookUp, Held(Lift2));
            t += 1.0f;
            if (below != Nobody)
            {
                int mb = s.Add(below);
                float wb = T.WalkTo(mb, 0f, bStand, onUp, KidWalk, belowAt);
                if (t + 1.0f > wb + 1e-3f) T.Part(mb, wb, t + 1.0f, bStand, onUp, DollStyle.Look);
                T.Part(mb, t + 1.0f, t + 2.0f, bStand, onUp, DollStyle.Wave);
            }
            T.Part(mk, t, t + 1.0f, onUp, below != Nobody ? bStand : foot, DollStyle.Wave, Held(Lift2));
            t += 1.0f;
            T.Part(mk, t, t + 0.6f, foot, spot, DollStyle.Stand, HopDown(Lift2), PropKind.None, 0.4f);
            s.Fx(t + 0.3f, CampFxKind.Dust, G(foot, 0.005f), 0.4f, 3);
            t += 0.6f;
            if (t + 0.6f > T.EndOf(mg) + 1e-3f) T.Part(mg, wg, t + 0.6f, gStand, spot, DollStyle.Look);
            return s;
        }

        // ================================================================== B81.c flowers on the anniversary
        /// <summary>
        /// B81.c (about 14 s): the bearer (a living family member, else the oldest grown-up) walks to the foot carrying a bunch (winter:
        /// <paramref name="sprig"/>, a dark sprig), kneels, lays it on the lower step (<see cref="CampScene.a"/>), a hand on the figure's foot (1.4 s, head down),
        /// stands; up to two others come and stand a step back with the head bowed (3 s), then each lays a hand on the plinth. The bunch stays until night.
        /// </summary>
        public static CampScene Flowers(int bearer, Vector2 bearerAt, IList<int> others, IList<Vector2> othersAt, bool sprig, Vector2 spot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.StatueFlowers, t0, spot) { holdFocus = true, kind = sprig ? 1 : 0 };
            var T = new Track(s);
            int mb = s.Add(bearer);
            var foot = Front(spot, toCam, KneelAt);
            float t = T.WalkTo(mb, 0f, foot, spot, Walk, bearerAt);
            s.Tip(mb, 0f, t + 0.9f, CampTipKind.Bunch, 1.2f, sprig ? 0.7f : 0f);
            T.Part(mb, t, t + 1.8f, foot, spot, DollStyle.Stand, KneelPose);
            s.a = t + 0.9f;
            s.Fx(s.a, CampFxKind.Dust, G(Front(spot, toCam, 0.07f), StepH), 0.2f, 1);
            t += 1.8f;
            T.Part(mb, t, t + 1.4f, foot, spot, DollStyle.Stand, HandOnFoot);
            t += 1.4f;
            T.Part(mb, t, t + 0.6f, foot, spot, DollStyle.Stand);
            float standEnd = t;
            // the others come and stand a step back, head bowed (they are there as she kneels), then each lays a hand on the plinth
            int n = Mathf.Min(2, others.Count);
            var sides = new[] { spot + Rot(toCam, 50f) * 0.15f, spot + Rot(toCam, -50f) * 0.15f };
            for (int i = 0; i < n; i++)
            {
                int m = s.Add(others[i]);
                var stand = spot + Rot(toCam, (i == 0 ? 1f : -1f) * 28f) * 0.32f;
                float wk = WalkTime(othersAt[i], stand, Walk);
                float start = Mathf.Max(0f, s.a - wk);
                float arrive = T.WalkTo(m, start, stand, spot, Walk, othersAt[i]);
                float bowEnd = Mathf.Max(arrive + 3.0f, standEnd + i * 0.9f);
                T.Part(m, arrive, bowEnd, stand, spot, DollStyle.Stand, BowHead);
                float at = T.WalkTo(m, bowEnd, sides[i], spot, Walk);
                T.Part(m, at, at + 1.0f, sides[i], spot, DollStyle.Stand, HandOn);
            }
            return s;
        }

        // ================================================================== B81.d sit at its foot
        /// <summary>
        /// B81.d (about 14 s): a grown-up (an old one first) walks to the plinth's side facing the green (<paramref name="side"/>, a unit vector from
        /// the statue), sits on the lower step with the back against the plinth for <paramref name="seconds"/>, the head up at the figure now and then (a slow
        /// nod once); a second (Nobody: none) sits on the ground beside them, they talk and the first points up at the figure; then a hand on the knee and up.
        /// </summary>
        public static CampScene Sit(int who, Vector2 whoAt, int second, Vector2 secondAt, Vector2 spot, Vector2 side, float seconds, float t0)
        {
            side = Unit(side, Vector2.up); var acr = Across(side);
            var s = new CampScene(CampUse.StatueSit, t0, spot) { holdFocus = true };
            var T = new Track(s);
            int mw = s.Add(who);
            var onStep = spot + side * 0.085f; var approach = spot + side * 0.17f;
            float t = T.WalkTo(mw, 0f, approach, spot, Walk, whoAt);
            // a turn and a step up onto the lower step
            T.Part(mw, t, t + 0.6f, onStep, onStep + side, DollStyle.Stand, Rise(0f, StepH, 0.5f, false), PropKind.None, 0.25f);
            t += 0.6f;
            float sitEnd = t + Mathf.Max(5f, seconds), seat = t;
            CampPoseFn Sitting(float lean = 0f, float arm = -1f) => (ref DollPose p, float u, float len) =>
            {
                Seated(ref p); p.lift = StepH; p.lean += lean * Bump(u, 0f, len);
                if (arm > 0f) p.armR = Lerp(p.armR, new Vector3(arm, 0f, 6f), Bump(u, 0f, len));
            };
            var faceOut = onStep + side;
            // sits (back to the plinth), the head up at the figure, sits, a slow nod, sits
            T.Part(mw, seat, seat + 2.5f, onStep, faceOut, DollStyle.Sit, Sitting());
            T.Part(mw, seat + 2.5f, seat + 3.7f, onStep, faceOut, DollStyle.Sit, Sitting(-10f));
            float mid = seat + 3.7f;
            if (second != Nobody)
            {
                int ms = s.Add(second);
                var beside = spot + side * 0.17f + acr * 0.12f;
                float ws = WalkTime(secondAt, beside, Walk);
                float startS = Mathf.Max(0f, mid - ws - 0.4f);
                float arriveS = T.WalkTo(ms, startS, beside, onStep, Walk, secondAt);
                T.Part(ms, arriveS, sitEnd + 0.5f, beside, onStep, DollStyle.Sit, (ref DollPose p, float u, float len) => Seated(ref p));
                T.Part(mw, mid, mid + 0.8f, onStep, faceOut, DollStyle.Sit, Sitting(0f, 110f)); // points up at the figure
                T.Part(mw, mid + 0.8f, sitEnd, onStep, faceOut, DollStyle.Sit, Sitting());
            }
            else
            {
                T.Part(mw, mid, mid + 0.9f, onStep, faceOut, DollStyle.Sit, Sitting(8f));
                T.Part(mw, mid + 0.9f, sitEnd, onStep, faceOut, DollStyle.Sit, Sitting());
            }
            // up: a hand on the knee, then down from the step and away
            T.Part(mw, sitEnd, sitEnd + 0.5f, onStep, faceOut, DollStyle.Sit, Sitting(0f, 40f));
            t = sitEnd + 0.5f;
            var away = spot + side * 0.45f + acr * 0.1f;
            T.Part(mw, t, t + 0.5f, approach, away, DollStyle.Stand, (ref DollPose p, float u, float len) => { p.lift = Mathf.Lerp(StepH, 0f, In(u, 0f, 0.3f)); }, PropKind.None, 0.3f);
            T.WalkTo(mw, t + 0.5f, away, away + side, Walk);
            if (second != Nobody)
            {
                int ms = s.IndexOf(second);
                T.WalkTo(ms, sitEnd + 0.5f, spot + side * 0.5f - acr * 0.1f, spot + side, Walk);
            }
            return s;
        }
    }
}
