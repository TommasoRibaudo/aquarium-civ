// #692 part 1 (docs/design/animation-specs.md "#692 part 1: the fire, the stew pot, the feather pole"): the scenes as pure builders.
// Each takes who and where (box x, z) and returns a CampScene whose timeline follows the spec row's beats and timings.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;

namespace AquariumCiv.View
{
    public static class CampScenes1
    {
        /// <summary>A grown-up's and a kid's walking pace in the scenes (box units a second).</summary>
        public const float Walk = 0.22f, KidWalk = 0.3f, KidRun = 0.5f;
        /// <summary>The pot's rim above the ground (MealDirector.PotTop; kept here so AnimBench compiles the scenes).</summary>
        public const float PotTop = 0.12f;

        static float WalkTime(Vector2 a, Vector2 b, float speed) => Mathf.Clamp((b - a).magnitude / speed + 0.25f, 0.4f, 7f);
        static Vector2 Dir(Vector2 from, Vector2 to, Vector2 fallback) { var d = to - from; return d.sqrMagnitude > 1e-6f ? d.normalized : fallback; }
        static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        static Vector3 G(Vector2 p, float h = 0f) => new Vector3(p.x, h, p.y);

        // ================================================================== B29.a warm hands in winter
        public const float WarmSeconds = 3.6f;

        /// <summary>B29.a over a sitter's Sit: lean in 10°, both arms out to the flames (x 75, z ∓10) held 1.2 s, the rub (x 75 ↔ 85 at 4 Hz,
        /// 0.8 s), a shiver (roll ±4° at 10 Hz, 0.3 s), arms in to a self-hug (0.8 s); a breath of steam at the head at the end.</summary>
        public static void WarmPose(ref DollPose p, float t, float len)
        {
            float lean = 10f * In(t, 0f, 0.4f) * (1f - In(t, 2.7f, 0.3f));
            p.lean += lean;
            var outL = new Vector3(75f, 0f, -10f); var outR = new Vector3(75f, 0f, 10f);
            var hugL = new Vector3(40f, 0f, 28f); var hugR = new Vector3(40f, 0f, -28f);
            if (t < 0.4f) { float u = In(t, 0f, 0.4f); p.armL = Lerp(p.armL, outL, u); p.armR = Lerp(p.armR, outR, u); }
            else if (t < 1.6f) { p.armL = outL; p.armR = outR; }
            else if (t < 2.4f)
            {
                float w = Mathf.Sin((t - 1.6f) * 4f * Mathf.PI * 2f);
                p.armL = new Vector3(80f + 5f * w, 0f, -6f); p.armR = new Vector3(80f - 5f * w, 0f, 6f);
            }
            else if (t < 2.7f) { p.armL = outL; p.armR = outR; p.roll += Mathf.Sin((t - 2.4f) * 10f * Mathf.PI * 2f) * 4f; }
            else
            {
                float u = In(t, 2.7f, 0.25f), back = In(t, len - 0.35f, 0.35f);
                p.armL = Lerp(Lerp(outL, hugL, u), p.armL, back); p.armR = Lerp(Lerp(outR, hugR, u), p.armR, back);
                p.roll += Mathf.Sin(t * 9f) * 1.5f * (1f - back);
            }
        }

        public static CampScene WarmHands(int key, float t0)
        {
            var s = new CampScene(CampUse.WarmHands, t0, Vector2.zero);
            int m = s.Add(key);
            s.Stay(m, 0f, WarmSeconds, DollStyle.Sit, WarmPose);
            s.Fx(WarmSeconds - 0.2f, CampFxKind.Steam, new Vector3(0f, 0.01f, 0f), 0.3f, 1, CampAnchor.Head, m);
            return s;
        }

        // ================================================================== B29.b kids poke sticks in the fire
        public const float PokeEach = 1.25f, CircleFor = 1.5f, KidFromFire = 0.2f;

        /// <summary>One kid's poke at kid scale (B9.c): raise x 120 (0.3 s), thrust x 60 (0.15 s Snap; the contact at 0.45 s), a hold, then the hop
        /// back 0.04 u with both arms up (x 70), delighted. Over a squat (squash 0.8).</summary>
        static void KidPokePose(ref DollPose p, float t, float len)
        {
            p.squash = 0.8f; p.offset.y = -0.01f; p.lean = 8f;
            p.armL = new Vector3(20f, 0f, -16f);
            if (t < 0.3f) { float u = In(t, 0f, 0.3f); p.armR = new Vector3(Mathf.Lerp(40f, 120f, u), 0f, 8f); p.lean = Mathf.Lerp(8f, -4f, u); }
            else if (t < 0.45f) { float u = Mathf.Clamp01((t - 0.3f) / 0.15f); u = 1f - (1f - u) * (1f - u) * (1f - u); p.armR = new Vector3(Mathf.Lerp(120f, 60f, u), 0f, 6f); p.lean = Mathf.Lerp(-4f, 18f, u); p.offset.z = -0.02f * u; }
            else if (t < 0.62f) { p.armR = new Vector3(60f, 0f, 6f); p.lean = 18f; p.offset.z = -0.02f; p.hit = 0f; }
            else
            {
                float u = In(t, 0.62f, 0.3f), back = In(t, len - 0.25f, 0.25f);
                p.offset.z = Mathf.Lerp(-0.02f, 0.04f, u); p.offset.y = -0.01f + Bump(t, 0.62f, 0.3f) * 0.025f;
                p.armL = Lerp(new Vector3(20f, 0f, -16f), new Vector3(70f, 0f, -30f), u * (1f - back));
                p.armR = Lerp(new Vector3(60f, 0f, 6f), new Vector3(70f, 0f, 30f), u * (1f - back));
                p.lean = Mathf.Lerp(18f, -8f, u);
                p.squash = Mathf.Lerp(0.8f, 0.95f, u * (1f - back));
            }
        }

        /// <summary>The ember tip drawn in the air: arm x 100, a circle r 0.03 u at 1.5 Hz.</summary>
        static void CirclePose(ref DollPose p, float t, float len)
        {
            float w = t * 1.5f * Mathf.PI * 2f, k = In(t, 0f, 0.25f);
            p.squash = 0.86f; p.offset.y = -0.005f; p.lean = 2f;
            p.armR = Lerp(new Vector3(60f, 0f, 6f), new Vector3(100f + Mathf.Sin(w) * 14f, 0f, 12f + Mathf.Cos(w) * 14f), k);
            p.armL = new Vector3(15f, 0f, -18f);
        }

        /// <summary>The kid holds the twig out to the grown-up (x 80).</summary>
        static void OfferPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.3f); p.armR = Lerp(p.armR, new Vector3(85f, 0f, 4f), u); p.lean += 6f * u; }

        /// <summary>The grown-up: a hand out (x 70) for the twig, then the toss into the fire (x 110 → 60), then a hand on the kid's head (x 110).</summary>
        static void TakeTossPose(ref DollPose p, float t, float len)
        {
            if (t < 0.6f) { float u = In(t, 0f, 0.3f); p.armR = Lerp(p.armR, new Vector3(70f, 0f, 8f), u); p.lean += 6f * u; }
            else if (t < 0.9f) { float u = In(t, 0.6f, 0.15f); p.armR = new Vector3(Mathf.Lerp(70f, 115f, u), 0f, 8f); p.lean += Mathf.Lerp(6f, -4f, u); }
            else if (t < 1.2f) { float u = Mathf.Clamp01((t - 0.9f) / 0.15f); p.armR = new Vector3(Mathf.Lerp(115f, 55f, u), 0f, 8f); p.lean += 10f * u; }
            else if (t < 1.6f) { p.armR = Lerp(new Vector3(55f, 0f, 8f), p.armR, In(t, 1.2f, 0.4f)); }
        }

        static void HeadPatPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f));
            p.armL = Lerp(p.armL, new Vector3(100f, 0f, 30f), u); // across, onto the kid sitting at the left
        }

        /// <summary>B29.b: the kid walks in, squats 0.2 u from the fire with a twig, 2–3 pokes (sparks and a small flare at each), the hop back;
        /// the glowing tip drawn in circles (1.5 s); to the grown-up, who takes the twig and tosses it in (sparks); the kid sits by them, a hand
        /// on its head. <paramref name="pokes"/>: 2 or 3.</summary>
        public static CampScene KidPoke(int kid, int adult, Vector2 kidAt, Vector2 adultAt, Vector2 fire, int pokes, float t0)
        {
            var toAdult = Dir(fire, adultAt, Vector2.down);
            var squat = fire + Rot(toAdult, 30f) * KidFromFire;
            var sitBy = adultAt + Rot(Dir(adultAt, fire, Vector2.up), 75f) * 0.11f;
            var s = new CampScene(CampUse.KidPoke, t0, (fire + adultAt) * 0.5f);
            int k = s.Add(kid), a = s.Add(adult);
            float t = 0f, walk = WalkTime(kidAt, squat, KidWalk);
            s.Part(k, t, t + walk, squat, fire, DollStyle.Crouch, null, PropKind.Twig, KidWalk); t += walk;
            s.Part(k, t, t + 0.4f, squat, fire, DollStyle.Crouch, (ref DollPose p, float u, float l) => { p.squash = Mathf.Lerp(1f, 0.8f, In(u, 0f, 0.4f)); }, PropKind.Twig); t += 0.4f;
            float firstContact = t + 0.45f;
            for (int i = 0; i < pokes; i++)
            {
                s.Part(k, t, t + PokeEach, squat, fire, DollStyle.Crouch, KidPokePose, PropKind.Twig);
                s.Fx(t + 0.45f, CampFxKind.Sparks, G(fire, 0.03f), 0.75f);
                s.Fx(t + 0.45f, CampFxKind.Flare, G(fire), 1.15f);
                t += PokeEach;
            }
            s.Part(k, t, t + CircleFor, squat, fire, DollStyle.Stand, CirclePose, PropKind.Twig); t += CircleFor;
            s.Tip(k, firstContact, t + 1.6f, CampTipKind.Ember);
            float w2 = WalkTime(squat, sitBy, KidWalk);
            s.Part(k, t, t + w2, sitBy, adultAt, DollStyle.Stand, null, PropKind.Twig, KidWalk); t += w2;
            s.Part(k, t, t + 0.45f, sitBy, adultAt, DollStyle.Stand, OfferPose, PropKind.Twig);
            s.Part(a, t, t + 0.45f, adultAt, sitBy, DollStyle.Sit, TakeTossPose).keepSpot = true;
            t += 0.45f;
            s.Part(k, t, t + 0.6f, sitBy, adultAt, DollStyle.Stand, OfferPose);
            var toss = s.Part(a, t, t + 1.15f, adultAt, fire, DollStyle.Sit, (ref DollPose p, float u, float l) => TakeTossPose(ref p, u + 0.45f, l + 0.45f), PropKind.Twig);
            toss.keepSpot = true;
            s.Fx(t + 1.1f, CampFxKind.Sparks, G(fire, 0.04f), 0.9f);
            s.Fx(t + 1.1f, CampFxKind.Flare, G(fire), 1.1f);
            t += 0.6f;
            s.Part(k, t, t + 0.5f, sitBy, fire, DollStyle.Sit); t += 0.5f;
            s.Part(k, t, t + 2.2f, sitBy, fire, DollStyle.Sit);
            s.Part(a, t + 0.05f, t + 1.25f, adultAt, fire, DollStyle.Sit, HeadPatPose).keepSpot = true;
            return s;
        }

        // ================================================================== B29.c a berry on a stick
        static void BendPose(ref DollPose p, float t, float len)
        {
            float u = Bump(t, 0f, len);
            p.lean += 35f * u; p.armL = Lerp(p.armL, new Vector3(40f, 0f, -12f), u); p.squash = Mathf.Lerp(1f, 0.92f, u);
        }

        static void PushBerryPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.25f);
            p.armL = Lerp(p.armL, new Vector3(70f, 0f, 14f), u); p.armR = Lerp(p.armR, new Vector3(70f, 0f, -6f), u); p.lean += 6f * u;
        }

        static void HoldOverPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.4f);
            p.armR = Lerp(p.armR, new Vector3(95f, 0f, 6f), u); p.lean += 10f * u; p.armL = new Vector3(10f, 0f, -16f);
            p.armR.x += Mathf.Sin(t * 2.3f) * 2f; // a small unsteady hand
        }

        /// <summary>Pull it back, blow twice (head forward 0.015 u), hand to the mouth (x 135) and chew (8 Hz), then fan the mouth (x 130, z ±30 at 5 Hz).</summary>
        static void EatHotPose(ref DollPose p, float t, float len)
        {
            p.armL = new Vector3(10f, 0f, -16f);
            if (t < 0.9f)
            {
                p.armR = Lerp(new Vector3(95f, 0f, 6f), new Vector3(105f, 0f, 2f), In(t, 0f, 0.3f));
                p.offset.z -= 0.015f * (Bump(t, 0.3f, 0.25f) + Bump(t, 0.6f, 0.25f));
            }
            else if (t < 1.8f)
            {
                p.armR = Lerp(new Vector3(105f, 0f, 2f), new Vector3(135f, 0f, -4f), In(t, 0.9f, 0.3f));
                if (t > 1.2f) p.offset.y += Mathf.Abs(Mathf.Sin((t - 1.2f) * 8f * Mathf.PI)) * 0.01f;
            }
            else
            {
                p.armR = Lerp(new Vector3(135f, 0f, -4f), new Vector3(40f, 0f, 10f), In(t, 1.8f, 0.3f));
                p.armL = new Vector3(130f, 0f, -10f + Mathf.Sin((t - 1.8f) * 5f * Mathf.PI * 2f) * 30f);
                p.offset.y += Mathf.Abs(Mathf.Sin((t - 1.8f) * 10f)) * 0.008f;
            }
        }

        /// <summary>B29.c: to a stash basket (bend, a berry in the hand), to the fire, the berry pushed onto the twig, held over the flames 2.5 s
        /// while it darkens and swells (1 → 1.3), a spark pops off it; pulled back, blown on (two puffs of steam), eaten (chew), the mouth fanned:
        /// hot! A grin to end.</summary>
        public static CampScene BerryStick(int key, Vector2 at, Vector2 stash, Vector2 fire, float t0, bool kid = true)
        {
            float pace = kid ? KidWalk : Walk;
            var basket = stash + Dir(stash, fire, Vector2.right) * 0.12f;
            var spot = fire + Dir(fire, basket, Vector2.down) * 0.22f;
            var s = new CampScene(CampUse.BerryStick, t0, fire);
            int m = s.Add(key);
            float t = 0f, w = WalkTime(at, basket, pace);
            s.Part(m, t, t + w, basket, stash, DollStyle.Stand, null, PropKind.Twig, pace); t += w;
            s.Part(m, t, t + 0.8f, basket, stash, DollStyle.Stand, BendPose, PropKind.Twig); t += 0.8f;
            float w2 = WalkTime(basket, spot, pace);
            s.Part(m, t, t + w2, spot, fire, DollStyle.Stand, null, PropKind.Twig, pace); t += w2;
            s.Part(m, t, t + 0.45f, spot, fire, DollStyle.Stand, PushBerryPose, PropKind.Twig);
            float tipOn = t + 0.4f; t += 0.45f;
            s.Part(m, t, t + 2.5f, spot, fire, DollStyle.Stand, HoldOverPose, PropKind.Twig);
            s.Tip(m, tipOn, t, CampTipKind.Berry, 1f, 0f);
            s.Tip(m, t, t + 2.5f + 1.3f, CampTipKind.Berry, 1.3f, 0.6f);
            s.Fx(t + 2.3f, CampFxKind.Sparks, new Vector3(0f, 0f, 0f), 0.4f, 0, CampAnchor.TwigTip, m);
            t += 2.5f;
            s.Part(m, t, t + 2.4f, spot, fire, DollStyle.Stand, EatHotPose, PropKind.Twig);
            s.Fx(t + 0.42f, CampFxKind.Steam, Vector3.zero, 0.35f, 1, CampAnchor.TwigTip, m);
            s.Fx(t + 0.72f, CampFxKind.Steam, Vector3.zero, 0.35f, 1, CampAnchor.TwigTip, m);
            t += 2.4f;
            s.Part(m, t, t + 0.7f, spot, fire, DollStyle.Laugh, null, PropKind.Twig);
            return s;
        }

        // ================================================================== B29.d meat on a spit after a hunt
        /// <summary>The spit's sticks stand this far either side of the fire's middle; the bar is 0.22 u.</summary>
        public const float SpitHalf = 0.11f, TurnSeconds = 16.7f, MeatTurn = 4f, DripEvery = 2f;

        static void PushStickPose(ref DollPose p, float t, float len)
        {
            float d = In(t, 0f, 0.3f);
            p.squash = Mathf.Lerp(1f, 0.82f, d); p.lean += 25f * d;
            p.armL = Lerp(p.armL, new Vector3(60f, 0f, 12f), d); p.armR = Lerp(p.armR, new Vector3(60f, 0f, -12f), d);
            if (t > 0.3f && t < 0.5f) p.offset.y -= 0.02f * Smooth((t - 0.3f) / 0.1f); // push down (Snap)
            else if (t >= 0.5f) p.offset.y -= 0.02f * (1f - In(t, 0.5f, 0.3f));
        }

        static void LayBarPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.3f), down = In(t, 0.3f, 0.5f);
            var x = Mathf.Lerp(Mathf.Lerp(p.armR.x, 150f, u), 100f, down);
            p.armL = new Vector3(x, 0f, -8f); p.armR = new Vector3(x, 0f, 8f); p.lean += 8f * down;
        }

        /// <summary>The turner, seated at the bar's end: arm x 80, a small circle (z ±20 at 0.5 Hz).</summary>
        static void TurnPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.4f) * (1f - In(t, len - 0.4f, 0.4f)), w = t * 0.5f * Mathf.PI * 2f;
            p.armR = Lerp(p.armR, new Vector3(80f + Mathf.Sin(w) * 8f, 0f, 10f + Mathf.Cos(w) * 20f), k); p.lean += 8f * k;
        }

        static void LiftBarPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.4f);
            p.armL = Lerp(p.armL, new Vector3(100f, 0f, -10f), u); p.armR = Lerp(p.armR, new Vector3(100f, 0f, 10f), u);
            p.lean += 10f * Bump(t, 0f, 0.6f);
        }

        static void TearPose(ref DollPose p, float t, float len)
        {
            p.lean += 20f; p.armL = new Vector3(55f, 0f, -12f);
            float u = t < 0.3f ? 0f : Mathf.Clamp01((t - 0.3f) / 0.15f);
            p.armR = new Vector3(Mathf.Lerp(60f, 100f, u), 0f, 10f);
        }

        static void HoldUpPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.25f) * (1f - In(t, len - 0.25f, 0.25f));
            p.armR = Lerp(p.armR, new Vector3(140f, 0f, 10f), u); p.lean -= 6f * u;
        }

        static void SniffPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f));
            p.lean += 10f * u - 15f * u * Mathf.Abs(Mathf.Sin(t * 5f));
            p.offset.y += 0.006f * Mathf.Abs(Mathf.Sin(t * 10f)) * u;
        }

        /// <summary>B29.d's set-up and turning (the round of pieces is the director's, at <see cref="SpitRoundAt"/>): the hunter pushes the
        /// two forked sticks in either side of the fire (each a dust puff), lays the bar with the meat across; a sitter at the bar's end
        /// turns it for <see cref="TurnSeconds"/> (a drip every 2 s: a spark and a flare 1.1); the cook lifts the bar off onto a flat stone and
        /// tears a piece. <see cref="CampScene.a"/> is the time the bar is laid (scene s), <see cref="CampScene.b"/> the time it is lifted
        /// off; <see cref="CampScene.kind"/> 0 rabbit, 1 deer.</summary>
        public static CampScene Spit(int hunter, int turner, int cook, Vector2 hunterAt, Vector2 turnerAt, Vector2 cookAt, Vector2 fire, Vector2 across, bool deer, float t0, out float roundAt, out Vector2 stone)
        {
            across = across.sqrMagnitude > 1e-6f ? across.normalized : Vector2.right;
            var side = new Vector2(-across.y, across.x);
            Vector2 s1 = fire + across * SpitHalf, s2 = fire - across * SpitHalf;
            Vector2 at1 = s1 + across * 0.07f + side * 0.02f, at2 = s2 - across * 0.07f + side * 0.02f;
            stone = fire + side * 0.3f + across * 0.08f;
            var s = new CampScene(CampUse.Spit, t0, fire) { kind = deer ? 1 : 0 };
            int h = s.Add(hunter), tr = turner != hunter ? s.Add(turner) : h, c = cook != hunter && cook != turner ? s.Add(cook) : -1;
            float t = 0f, w = WalkTime(hunterAt, at1, Walk);
            s.Part(h, t, t + w, at1, s1, DollStyle.Stand, null, PropKind.Stick, Walk); t += w;
            s.Part(h, t, t + 0.9f, at1, s1, DollStyle.Stand, PushStickPose, PropKind.Stick);
            s.Fx(t + 0.4f, CampFxKind.Dust, G(s1), 0.6f, 2); s.Fx(t + 0.4f, CampFxKind.Jolt, G(s1)); s.a = t + 0.4f; t += 0.9f;
            float w2 = WalkTime(at1, at2, Walk);
            s.Part(h, t, t + w2, at2, s2, DollStyle.Stand, null, PropKind.Stick, Walk); t += w2;
            s.Part(h, t, t + 0.9f, at2, s2, DollStyle.Stand, PushStickPose, PropKind.Stick);
            s.Fx(t + 0.4f, CampFxKind.Dust, G(s2), 0.6f, 2); s.Fx(t + 0.4f, CampFxKind.Jolt, G(s2)); t += 0.9f;
            s.Part(h, t, t + 0.85f, at2, fire, DollStyle.Stand, LayBarPose); s.a = t + 0.55f; t += 0.85f;
            float layAt = t;
            // the turner walks to the bar's end and sits; the hunter goes back to the ring and watches
            var end1 = s1 + across * 0.09f;
            float wt = WalkTime(turnerAt, end1, Walk);
            float turnFrom = layAt;
            if (tr != h) s.Part(tr, layAt - wt, layAt, end1, fire, DollStyle.Sit, null, PropKind.None, Walk);
            else { s.Part(h, t, t + WalkTime(at2, end1, Walk), end1, fire, DollStyle.Sit, null, PropKind.None, Walk); turnFrom = t + WalkTime(at2, end1, Walk); }
            s.Part(tr, turnFrom, turnFrom + TurnSeconds, end1, fire, DollStyle.Sit, TurnPose);
            for (float d = turnFrom + 1f; d < turnFrom + TurnSeconds; d += DripEvery)
            {
                s.Fx(d, CampFxKind.Sparks, G(fire, 0.04f), 0.45f);
                s.Fx(d, CampFxKind.Flare, G(fire), 1.1f);
            }
            var ring = fire - side * 0.3f + across * 0.1f; // the hunter sits in the ring, facing his roast
            if (tr != h) s.Part(h, layAt, turnFrom + TurnSeconds + 3f, ring, fire, DollStyle.Sit, null, PropKind.None, Walk);
            t = turnFrom + TurnSeconds;
            // the cook lifts the bar off, onto the flat stone, and tears the first piece
            int ck = c >= 0 ? c : h;
            var lift = fire + side * 0.12f;
            float wc = WalkTime(cookAt, lift, Walk);
            s.Part(ck, t - wc, t, lift, fire, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(ck, t, t + 0.7f, lift, fire, DollStyle.Stand, LiftBarPose); s.b = t + 0.4f; t += 0.7f;
            float ws = WalkTime(lift, stone + side * 0.08f, Walk);
            s.Part(ck, t, t + ws, stone + side * 0.09f, stone, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(95f, 0f, -10f); p.armR = new Vector3(95f, 0f, 10f); }, PropKind.None, Walk).poseWalking = true;
            t += ws;
            s.Part(ck, t, t + 0.6f, stone + side * 0.09f, stone, DollStyle.Stand, TearPose);
            s.Fx(t + 0.42f, CampFxKind.Steam, G(stone, 0.06f), 0.4f, 1);
            t += 0.6f;
            roundAt = t;
            s.a = layAt; // when the meat is on
            return s;
        }

        /// <summary>The spit's round: the hunter gets the first piece and holds it up (x 140, 0.6 s) with a small cheer.</summary>
        public static void SpitHolder(CampScene s, int hunter, float from, Vector2 at, Vector2 fire)
        {
            int m = s.IndexOf(hunter);
            if (m < 0) m = s.Add(hunter);
            s.Part(m, from, from + 0.8f, at, fire, DollStyle.Sit, HoldUpPose, PropKind.MeatPiece).keepSpot = true;
            s.Part(m, from + 0.8f, from + 1.6f, at, fire, DollStyle.Cheer, null, PropKind.MeatPiece).keepSpot = true;
        }

        /// <summary>Kids crowd in and sniff (head up +15°, lean 10, 0.6 s), near the stone.</summary>
        public static void SpitKids(CampScene s, IList<int> kids, IList<Vector2> at, Vector2 stone, float from)
        {
            for (int i = 0; i < kids.Count; i++)
            {
                int m = s.Add(kids[i]);
                var spot = stone + Rot(Dir(stone, at[i], Vector2.down), 0f) * (0.16f + 0.05f * i);
                float w = WalkTime(at[i], spot, KidWalk);
                s.Part(m, from, from + w, spot, stone, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(m, from + w, from + w + 1.4f, spot, stone, DollStyle.Stand, SniffPose);
            }
        }

        // ================================================================== B30 the stew pot
        /// <summary>The cold pot's spot: on three stones 0.15 u further out than its cooking spot beside the fire ring.</summary>
        public static Vector2 ColdPot(Vector2 fire, Vector2 potOffset) => fire + potOffset + potOffset.normalized * 0.15f;

        static void TipBucketPose(ref DollPose p, float t, float len)
        {
            float lift = In(t, 0f, 0.4f), tip = In(t, 0.4f, 0.6f), back = In(t, len - 0.3f, 0.3f);
            float x = Mathf.Lerp(Mathf.Lerp(60f, 100f, lift), 125f, tip);
            x = Mathf.Lerp(x, 30f, back);
            p.armL = new Vector3(x, 0f, -8f); p.armR = new Vector3(x, 0f, 8f);
            p.lean += Mathf.Lerp(-5f * lift, 18f, tip) * (1f - back);
        }

        static void TipBasketPose(ref DollPose p, float t, float len)
        {
            float tip = Bump(t, 0.1f, len - 0.2f);
            p.armL = new Vector3(60f + 50f * tip, 0f, -8f); p.armR = new Vector3(60f + 50f * tip, 0f, 8f); p.lean += 20f * tip;
        }

        /// <summary>Crouched either side of the pot, arms in (x 60), the lift together (the pot rises 0.06 u), carried, set down.</summary>
        static void PotCarryPose(ref DollPose p, float t, float len)
        {
            float crouch = Mathf.Max(Bump(t, 0f, 0.8f), Bump(t, len - 0.8f, 0.8f));
            p.squash = Mathf.Lerp(1f, 0.84f, crouch); p.lean += 16f * crouch;
            p.armL = new Vector3(60f, 0f, -4f); p.armR = new Vector3(60f, 0f, 4f);
        }

        /// <summary>B30.a: the helper brings a full bucket (the heavy walk), lifts it and tips it in (4 droplets at the rim; the water shows);
        /// the cook brings the stash basket and tips it in (the stew shows); the two lift the pot together, side-step it 0.15 u onto the fire
        /// stones and set it down (a jolt, sparks from under). <see cref="CampScene.a"/>: water in; <see cref="CampScene.b"/>: food in;
        /// the carry runs from <c>CarryFrom</c> to <c>CarryTo</c> (returned).</summary>
        public static CampScene PotFill(int cook, int helper, Vector2 cookAt, Vector2 helperAt, Vector2 stash, Vector2 cold, Vector2 hot, bool fish, float t0, out float carryFrom, out float carryTo)
        {
            var move = Dir(cold, hot, Vector2.left);
            var side = new Vector2(-move.y, move.x);
            var s = new CampScene(CampUse.PotFill, t0, (cold + hot) * 0.5f);
            int c = s.Add(cook), h = s.Add(helper);
            var fromStash = stash + Dir(stash, cold, Vector2.right) * 0.1f;
            // the helper: to the pile for the bucket, the heavy walk to the pot, the tip
            float t = 0f, w = WalkTime(helperAt, fromStash, Walk);
            s.Part(h, t, t + w, fromStash, stash, DollStyle.Stand, null, PropKind.BucketEmpty, Walk); t += w;
            s.Part(h, t, t + 0.5f, fromStash, stash, DollStyle.Stand, BendPose, PropKind.Bucket); t += 0.5f;
            var hs = cold - side * 0.11f;
            float w2 = WalkTime(fromStash, hs, Walk * 0.8f);
            s.Part(h, t, t + w2, hs, cold, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean -= 4f; p.armR = new Vector3(20f, 0f, 22f); p.roll += Mathf.Sin(u * 6f) * 2f; }, PropKind.Bucket, Walk * 0.8f).poseWalking = true;
            t += w2;
            s.Part(h, t, t + 1.3f, hs, cold, DollStyle.Stand, TipBucketPose, PropKind.Bucket);
            s.Fx(t + 0.85f, CampFxKind.Droplets, G(cold, PotTop), 0.6f, 4);
            s.a = t + 0.85f; float helperFree = t + 1.3f; t += 1.3f;
            // the cook: to the pile, a basket, to the pot (the other side), the tip
            float cw = WalkTime(cookAt, fromStash + side * 0.08f, Walk);
            float cs = Mathf.Max(0f, t - cw - 1.6f);
            s.Part(c, cs, cs + cw, fromStash + side * 0.08f, stash, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(c, cs + cw, cs + cw + 0.5f, fromStash + side * 0.08f, stash, DollStyle.Stand, BendPose, fish ? PropKind.FishBasket : PropKind.Basket);
            var cside = cold + side * 0.11f;
            float cw2 = WalkTime(fromStash + side * 0.08f, cside, Walk);
            float ct = cs + cw + 0.5f;
            s.Part(c, ct, Mathf.Max(ct + cw2, t), cside, cold, DollStyle.Stand, null, fish ? PropKind.FishBasket : PropKind.Basket, Walk);
            t = Mathf.Max(ct + cw2, t);
            s.Part(h, helperFree, t + 1.1f, hs, cold, DollStyle.Stand, null, PropKind.None);
            s.Part(c, t, t + 1.1f, cside, cold, DollStyle.Stand, TipBasketPose, fish ? PropKind.FishBasket : PropKind.Basket);
            for (int i = 0; i < 4; i++) s.Fx(t + 0.35f + 0.06f * i, CampFxKind.Droplets, G(cold, PotTop), 0.3f, 1);
            s.b = t + 0.5f; t += 1.1f;
            // the lift: crouch either side, up together, the side-step onto the fire stones, down (jolt and sparks)
            carryFrom = t + 0.7f; carryTo = carryFrom + 1.2f;
            float len = 2.9f;
            s.Part(c, t, t + 0.7f, cold + side * 0.1f, cold, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(h, t, t + 0.7f, cold - side * 0.1f, cold, DollStyle.Stand, null, PropKind.None, Walk);
            t += 0.7f;
            s.Part(c, t - 0.0f, t + len - 0.7f, hot + side * 0.1f, hot, DollStyle.Stand, PotCarryPose, PropKind.None, 0.14f).poseWalking = true;
            s.Part(h, t - 0.0f, t + len - 0.7f, hot - side * 0.1f, hot, DollStyle.Stand, PotCarryPose, PropKind.None, 0.14f).poseWalking = true;
            s.Fx(carryTo + 0.2f, CampFxKind.Jolt, G(hot));
            s.Fx(carryTo + 0.2f, CampFxKind.Sparks, G(hot, 0.02f), 0.7f);
            s.Fx(carryTo + 0.2f, CampFxKind.Dust, G(hot), 0.5f, 2);
            return s;
        }

        static void ScrapePose(ref DollPose p, float t, float len)
        {
            float w = t / len * 3f * Mathf.PI * 2f, k = In(t, 0f, 0.2f) * (1f - In(t, len - 0.2f, 0.2f));
            p.lean += 22f * k;
            p.armR = Lerp(p.armR, new Vector3(70f + Mathf.Sin(w) * 10f, 0f, 8f + Mathf.Cos(w) * 12f), k);
        }

        static void StackBowlsPose(ref DollPose p, float t, float len)
        {
            float u = Bump(t, 0f, len);
            p.lean += 30f * u; p.squash = Mathf.Lerp(1f, 0.85f, u); p.armL = Lerp(p.armL, new Vector3(50f, 0f, -6f), u);
        }

        /// <summary>B30.e: the cook scrapes the pot out (three circles in 1.5 s), the cook and a helper lift it off the fire and set it on its
        /// stones out from the ring; a kid carries the bowls over and stacks them beside it. The carry runs from <paramref name="carryFrom"/> to
        /// <paramref name="carryTo"/>; the stack shows from <see cref="CampScene.b"/>.</summary>
        public static CampScene PotOff(int cook, int helper, int kid, Vector2 cookAt, Vector2 helperAt, Vector2 kidAt, Vector2 hot, Vector2 cold, float t0, out float carryFrom, out float carryTo)
        {
            var move = Dir(hot, cold, Vector2.right);
            var side = new Vector2(-move.y, move.x);
            var s = new CampScene(CampUse.PotOff, t0, (cold + hot) * 0.5f);
            int c = s.Add(cook), h = helper >= 0 ? s.Add(helper) : -1, k = kid >= 0 ? s.Add(kid) : -1;
            float t = 0f, w = WalkTime(cookAt, hot + side * 0.13f, Walk);
            s.Part(c, t, t + w, hot + side * 0.13f, hot, DollStyle.Stand, null, PropKind.Stick, Walk); t += w;
            s.Part(c, t, t + 1.5f, hot + side * 0.13f, hot, DollStyle.Stand, ScrapePose, PropKind.Stick); t += 1.5f;
            if (h >= 0)
            {
                float wh = WalkTime(helperAt, hot - side * 0.1f, Walk);
                s.Part(h, Mathf.Max(0f, t - wh), t + 0.5f, hot - side * 0.1f, hot, DollStyle.Stand, null, PropKind.None, Walk);
            }
            s.Part(c, t, t + 0.5f, hot + side * 0.1f, hot, DollStyle.Stand, null, PropKind.None, Walk);
            t += 0.5f;
            carryFrom = t + 0.7f; carryTo = carryFrom + 1.2f;
            s.Part(c, t, t + 2.2f, cold + side * 0.1f, cold, DollStyle.Stand, PotCarryPose, PropKind.None, 0.14f).poseWalking = true;
            if (h >= 0) s.Part(h, t, t + 2.2f, cold - side * 0.1f, cold, DollStyle.Stand, PotCarryPose, PropKind.None, 0.14f).poseWalking = true;
            s.Fx(carryTo + 0.2f, CampFxKind.Jolt, G(cold));
            s.Fx(carryTo + 0.2f, CampFxKind.Dust, G(cold), 0.5f, 2);
            t += 2.2f;
            if (k >= 0)
            {
                var stack = cold + move * 0.1f + side * 0.06f;
                float wk = WalkTime(kidAt, stack + move * 0.06f, KidWalk);
                float ks = Mathf.Max(0f, t - wk);
                s.Part(k, ks, t, stack + move * 0.06f, stack, DollStyle.Stand, null, PropKind.Bowl, KidWalk);
                s.Part(k, t, t + 0.9f, stack + move * 0.06f, stack, DollStyle.Stand, StackBowlsPose, PropKind.Bowl);
                s.Fx(t + 0.45f, CampFxKind.Dust, G(stack), 0.3f, 1);
                s.b = t + 0.45f;
                s.Part(k, t + 0.9f, t + 1.4f, stack + move * 0.06f, stack, DollStyle.Hop);
                t += 1.4f;
            }
            else s.b = t;
            return s;
        }

        /// <summary>B30.b: the dip at the pot before a hand-over (arm x 60 → 30, lean 25, 0.3 s; the contact puts stew in the bowl).</summary>
        public static void DipPose(ref DollPose p, float t, float len)
        {
            float d = Bump(t, 0.05f, 0.55f);
            p.lean += 25f * d; p.armR = Lerp(p.armR, new Vector3(Mathf.Lerp(60f, 30f, In(t, 0.1f, 0.3f)), 0f, 8f), d);
            p.armL = new Vector3(55f, 0f, -10f); // the bowl held up, waiting
        }

        public const float DipSeconds = 0.7f;

        static void TiptoePeekPose(ref DollPose p, float t, float len)
        {
            float up = In(t, 0f, 0.3f);
            p.offset.y += 0.03f * up; p.squash = Mathf.Lerp(1f, 1.08f, up);
            p.lean += 20f * In(t, 0.3f, 0.3f);
            p.armL = new Vector3(30f, 0f, -20f); p.armR = new Vector3(30f, 0f, 20f);
        }

        static void SteamFacePose(ref DollPose p, float t, float len)
        {
            // flinch back 0.04 u (0.2 s), wipe the face (arms x 140 in turn, 0.5 s), sniff (two bobs), a grin
            float back = In(t, 0f, 0.2f);
            p.offset.z += 0.04f * back; p.lean -= 10f * back * (1f - In(t, 0.5f, 0.3f));
            if (t > 0.2f && t < 0.7f)
            {
                float a = (t - 0.2f) / 0.5f;
                p.armL = new Vector3(a < 0.5f ? 140f : 40f, 0f, -12f); p.armR = new Vector3(a >= 0.5f ? 140f : 40f, 0f, 12f);
            }
            if (t > 0.75f && t < 1.25f) { p.lean -= 12f * Mathf.Abs(Mathf.Sin((t - 0.75f) * 2f * Mathf.PI)); p.offset.y += 0.01f * Mathf.Abs(Mathf.Sin((t - 0.75f) * 2f * Mathf.PI)); }
        }

        static void ShooPose(ref DollPose p, float t, float len)
        {
            float k = Bump(t, 0f, len);
            p.armR = Lerp(p.armR, new Vector3(70f, 0f, 10f - 60f * Mathf.Abs(Mathf.Sin(t / len * 2f * Mathf.PI))), k);
        }

        /// <summary>B30.c: the kid comes up beside the cook at the pot, on tiptoe, leans over the rim (0.8 s); a puff of steam in the face: the
        /// flinch, the wipe, the sniff, a grin; the cook shoos with the stick (two flaps) and the kid runs off laughing, looks back from 0.4 u
        /// and hops. <paramref name="cook"/> −1: nobody shoos; the kid just runs off.</summary>
        public static CampScene PotPeek(int kid, int cook, Vector2 kidAt, Vector2 cookAt, Vector2 pot, float t0)
        {
            var fromCook = Dir(pot, cookAt, Vector2.down);
            var spot = pot + Rot(fromCook, 55f) * 0.12f;
            var away = spot + Dir(pot, spot, Vector2.down) * 0.4f;
            var s = new CampScene(CampUse.PotPeek, t0, pot);
            int k = s.Add(kid);
            float t = 0f, w = WalkTime(kidAt, spot, KidWalk);
            s.Part(k, t, t + w, spot, pot, DollStyle.Stand, null, PropKind.None, KidWalk); t += w;
            s.Part(k, t, t + 1.1f, spot, pot, DollStyle.Stand, TiptoePeekPose); t += 1.1f;
            s.Fx(t - 0.05f, CampFxKind.Steam, G(Vector2.Lerp(pot, spot, 0.5f), PotTop + 0.03f), 0.35f, 1);
            s.Part(k, t, t + 1.5f, spot, pot, DollStyle.Stand, SteamFacePose); t += 1.5f;
            if (cook >= 0) s.Part(s.Add(cook), t - 0.4f, t + 0.3f, cookAt, pot, DollStyle.Stir, ShooPose, PropKind.Stick).keepSpot = true;
            s.Part(k, t, t + 0.55f, spot, pot, DollStyle.Laugh); t += 0.55f;
            float wr = WalkTime(spot, away, KidRun);
            s.Part(k, t, t + wr, away, away + (away - spot), DollStyle.Laugh, null, PropKind.None, KidRun).run = true; t += wr;
            s.Part(k, t, t + 0.4f, away, pot, DollStyle.Stand); t += 0.4f;
            s.Part(k, t, t + 0.6f, away, pot, DollStyle.Hop); t += 0.6f;
            return s;
        }

        /// <summary>B30.d's beat: slow, turn to the pot, lean 15, sniff (two short head-ups with a bob), a slow nod (lean 4 → 14 → 4), two pats on the belly.</summary>
        static void SniffNodPose(ref DollPose p, float t, float len)
        {
            float lean = 15f * In(t, 0.4f, 0.25f) * (1f - In(t, 1.0f, 0.2f));
            float sniff = Bump(t, 0.6f, 0.15f) + Bump(t, 0.8f, 0.15f);
            p.lean += lean - 10f * sniff; p.offset.y += 0.01f * sniff;
            if (t > 1.1f && t < 1.5f) p.lean += Mathf.Lerp(4f, 14f, Bump(t, 1.1f, 0.4f));
            if (t > 1.5f && t < 1.95f) { float pat = Mathf.Abs(Mathf.Sin((t - 1.5f) / 0.45f * Mathf.PI * 2f)); p.armR = new Vector3(40f, 0f, 8f - 20f * pat); p.armL = new Vector3(40f, 0f, -8f + 20f * pat); }
        }

        public const float SniffSeconds = 2.1f;

        /// <summary>B30.d: a grown-up passing the pot stops where they are, turns to it, sniffs, nods, pats the belly and walks on; the cook
        /// nods back (<paramref name="cook"/> −1: none).</summary>
        public static CampScene PotSniff(int walker, int cook, Vector2 at, Vector2 pot, Vector2 cookAt, float t0)
        {
            var s = new CampScene(CampUse.PotSniff, t0, pot);
            int m = s.Add(walker);
            s.Part(m, 0f, SniffSeconds, at, pot, DollStyle.Stand, SniffNodPose, PropKind.None, 0.05f);
            if (cook >= 0) s.Part(s.Add(cook), 1.1f, 1.6f, cookAt, pot, DollStyle.Stir, (ref DollPose p, float u, float l) => { p.lean += 10f * Bump(u, 0f, l); }, PropKind.Stick).keepSpot = true;
            return s;
        }

        // ================================================================== B31 the feather pole
        static void FiddlePose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.25f), f = Mathf.Sin(t * 4f * Mathf.PI * 2f) * 10f;
            p.armL = Lerp(p.armL, new Vector3(95f + f, 0f, 6f), k); p.armR = Lerp(p.armR, new Vector3(95f - f, 0f, -6f), k); p.lean += 6f * k;
        }

        static void TouchPose(ref DollPose p, float t, float len) { float u = Bump(t, 0f, len); p.armR = Lerp(p.armR, new Vector3(110f, 0f, 6f), u); p.lean += 8f * u; }

        /// <summary>B31.a: the chief (else the eldest) and the people it is for walk to the pole; the chief holds the new feather up (x 160, 1.2 s;
        /// a flash at the hand) and ties it on (both arms x 95, a fiddle ±10° at 4 Hz, 1.5 s; the contact: the feather on, the pole jolts,
        /// a chime); all pray 3 s; the one it is for touches it (x 110, 0.8 s); they step back and look up (1 s); a kid points.
        /// <see cref="CampScene.a"/>: the tie's contact.</summary>
        public static CampScene FeatherTie(int chief, IList<int> forKeys, int kid, Vector2 chiefAt, IList<Vector2> forAt, Vector2 kidAt, Vector2 pole, Vector2 towardCamp, float t0)
        {
            var out1 = Dir(pole, towardCamp, Vector2.down);
            var s = new CampScene(CampUse.FeatherTie, t0, pole);
            int c = s.Add(chief);
            var cs = pole + out1 * 0.1f;
            float w = WalkTime(chiefAt, cs, Walk);
            for (int i = 0; i < forKeys.Count; i++) w = Mathf.Max(w, WalkTime(forAt[i], pole + Rot(out1, i == 0 ? 45f : -45f) * 0.16f, Walk));
            float t = w;
            s.Part(c, 0f, t, cs, pole, DollStyle.Stand, null, PropKind.Feather, Walk);
            s.Part(c, t, t + 1.2f, cs, pole, DollStyle.HoldUp, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.3f); p.armR = Lerp(p.armR, new Vector3(160f, 0f, 6f), k); }, PropKind.Feather);
            s.Fx(t + 0.5f, CampFxKind.Sparks, new Vector3(0f, 0.04f, 0f), 0.45f, 0, CampAnchor.Hand, c);
            t += 1.2f;
            s.Part(c, t, t + 1.5f, cs, pole, DollStyle.Stand, FiddlePose, PropKind.Feather);
            s.a = t + 1.5f; t += 1.5f;
            s.Fx(s.a, CampFxKind.Jolt, G(pole)); s.Fx(s.a, CampFxKind.Chime, G(pole));
            var who = new List<int> { c };
            var spots = new List<Vector2> { cs };
            for (int i = 0; i < forKeys.Count; i++)
            {
                int m = s.Add(forKeys[i]);
                var sp = pole + Rot(out1, i == 0 ? 45f : -45f) * 0.16f;
                s.Part(m, 0f, s.a, sp, pole, DollStyle.Stand, null, PropKind.None, Walk);
                who.Add(m); spots.Add(sp);
            }
            int kk = -1; Vector2 ks = pole + out1 * 0.32f + Rot(out1, 90f) * 0.08f;
            if (kid >= 0) { kk = s.Add(kid); s.Part(kk, 0f, s.a, ks, pole, DollStyle.Stand, null, PropKind.None, KidWalk); }
            for (int i = 0; i < who.Count; i++) s.Part(who[i], t, t + 3f, spots[i], pole, DollStyle.Pray);
            if (kk >= 0) s.Part(kk, s.a, t + 3f, ks, pole, DollStyle.Stand);
            t += 3f;
            int touch = who.Count > 1 ? who[1] : who[0];
            for (int i = 0; i < who.Count; i++)
                s.Part(who[i], t, t + 0.8f, spots[i], pole, DollStyle.Stand, who[i] == touch ? TouchPose : null);
            if (kk >= 0) s.Part(kk, t, t + 0.8f, ks, pole, DollStyle.Stand);
            t += 0.8f;
            for (int i = 0; i < who.Count; i++) s.Part(who[i], t, t + 1.2f, spots[i] + out1 * 0.05f, pole, DollStyle.LookUp, null, PropKind.None, 0.1f);
            if (kk >= 0) s.Part(kk, t, t + 1.4f, ks, pole, DollStyle.Point);
            return s;
        }

        static void ShovePose(ref DollPose p, float t, float len) { float u = Bump(t, 0f, len); p.armL = Lerp(p.armL, new Vector3(80f, 0f, -6f), u); p.armR = Lerp(p.armR, new Vector3(80f, 0f, 6f), u); p.lean += 10f * u; }

        /// <summary>The creep in (B6 at kid scale): 4 steps of 0.04 u in 0.4 s, 0.5 s still between, two looks back (yaw 150°), then the reach
        /// (x 100, lean 20), a pull back, the touch (the contact at <c>len − 0.9</c>), the squeal (hop 0.05 u, arms up x 160).</summary>
        static void CreepTouchPose(ref DollPose p, float t, float len)
        {
            p.squash = 0.86f; p.lean += 14f; p.armL = new Vector3(25f, 0f, -16f); p.armR = new Vector3(25f, 0f, 16f);
            float creep = 4f * 0.9f;
            if (t < creep)
            {
                int step = Mathf.FloorToInt(t / 0.9f);
                float w = t - step * 0.9f;
                if ((step == 1 || step == 3) && w > 0.45f && w < 0.85f) p.yaw += 150f * Bump(w, 0.45f, 0.4f);
                return;
            }
            float r = t - creep;
            if (r < 0.4f) { float u = In(r, 0f, 0.3f); p.armR = new Vector3(Mathf.Lerp(25f, 100f, u), 0f, 6f); p.lean += 6f * u; }
            else if (r < 0.7f) { p.armR = new Vector3(70f, 0f, 8f); p.lean += 2f; } // pulls back once
            else if (r < 1.0f) { p.armR = new Vector3(100f, 0f, 6f); p.lean += 6f; } // the touch
            else
            {
                float u = Bump(r, 1.0f, 0.5f);
                p.squash = 1f; p.lean = -6f; p.offset.y += 0.05f * u;
                p.armL = new Vector3(160f, 0f, -16f); p.armR = new Vector3(160f, 0f, 16f);
            }
        }

        public static float DareCreepSeconds => 4f * 0.9f + 1.5f;

        /// <summary>B31.b: the kids bunch up 0.4 u from the pole; one is pushed forward (the shove, a stumble 0.05 u), creeps in looking back
        /// twice, reaches, pulls back once, touches (the feathers flutter), squeals and runs back; the others hop and laugh; the next one
        /// goes. At the end a grown-up near the pole turns to look and they scatter. <see cref="CampScene.a"/>/<see cref="CampScene.b"/>:
        /// the two touches (scene s).</summary>
        public static CampScene PoleDare(IList<int> kids, IList<Vector2> kidAt, int adult, Vector2 adultAt, Vector2 pole, Vector2 toCamp, float t0)
        {
            var out1 = Dir(pole, toCamp, Vector2.down);
            var side = new Vector2(-out1.y, out1.x);
            var bunch = pole + out1 * 0.4f;
            var s = new CampScene(CampUse.PoleDare, t0, (pole + bunch) * 0.5f);
            int n = kids.Count;
            var spots = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                s.Add(kids[i]);
                spots[i] = bunch + side * ((i - (n - 1) * 0.5f) * 0.08f) + out1 * (i % 2) * 0.03f;
                t = Mathf.Max(t, WalkTime(kidAt[i], spots[i], KidWalk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, spots[i], pole, DollStyle.Stand, null, PropKind.None, KidWalk);
            int rounds = Mathf.Min(2, n);
            for (int r = 0; r < rounds; r++)
            {
                int darer = r, pusher = (r + 1) % n;
                s.Part(pusher, t, t + 0.5f, spots[pusher], spots[darer], DollStyle.Stand, ShovePose);
                for (int i = 0; i < n; i++) if (i != darer && i != pusher) s.Part(i, t, t + 0.5f, spots[i], pole, DollStyle.Stand);
                var stumble = spots[darer] - out1 * 0.05f;
                s.Part(darer, t, t + 0.5f, stumble, pole, DollStyle.Stand, null, PropKind.None, 0.2f);
                t += 0.5f;
                var near = pole + out1 * 0.09f;
                float len = DareCreepSeconds;
                s.Part(darer, t, t + len, near, pole, DollStyle.Stand, CreepTouchPose, PropKind.None, 0.04f / 0.9f + 0.03f).poseWalking = true;
                float touchAt = t + 4f * 0.9f + 0.85f;
                if (r == 0) s.a = touchAt; else s.b = touchAt;
                s.Fx(touchAt, CampFxKind.Jolt, G(pole));
                for (int i = 0; i < n; i++) if (i != darer) s.Part(i, t, touchAt + 0.2f, spots[i], pole, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean += 6f; p.squash = 0.95f; });
                t += len;
                float back = WalkTime(near, spots[darer], KidRun);
                s.Part(darer, t, t + back, spots[darer], pole, DollStyle.Laugh, null, PropKind.None, KidRun).run = true;
                for (int i = 0; i < n; i++) if (i != darer) s.Part(i, touchAt + 0.2f, t + back, spots[i], pole, i % 2 == 0 ? DollStyle.Hop : DollStyle.Laugh);
                t += back;
            }
            if (adult >= 0)
            {
                int a = s.Add(adult);
                s.Part(a, t, t + 1.2f, adultAt, bunch, DollStyle.Look).keepSpot = true;
            }
            t += 0.6f;
            for (int i = 0; i < n; i++)
            {
                var dir = Rot(out1, -60f + 120f * (n == 1 ? 0.5f : i / (float)(n - 1)));
                s.Part(i, t, t + 1.3f, spots[i] + dir * 0.45f, spots[i] + dir, DollStyle.Laugh, null, PropKind.None, KidRun).run = true;
            }
            return s;
        }
    }
}
