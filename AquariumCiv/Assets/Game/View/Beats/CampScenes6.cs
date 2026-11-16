// #693 (docs/design/animation-specs.md "#693: the choice props", B54.a-zz): the things the choices leave get used, a work use and a life
// use each, as pure builders (the same kit as #692: CampScene parts, effects, tips). Every spot is worked out from the prop's spot and the
// way to the camera (toCam), so the use plays on the camera's side of the thing. What happens to the prop itself (a basket taken, the axe
// pulled out, a new row of names, the bird set upright) is a CampFxKind.Prop effect that View/CampLife.Props.cs carries out.
// No goat pen here: #714 turned the pond family's goat pen into their boat (B77), so B54.v/w have no thing to play at.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    /// <summary>#693: sizes of the choice props as drawn (ConsequenceMeshes at its 1.5 scale, box units).</summary>
    public static class PropGeo
    {
        public const float S = ConsequenceMeshes.Scale;
        /// <summary>The axe stump's top; the long table's bench and top; the fish crate's top; the woodpile's low logs.</summary>
        public const float StumpTop = 0.1f * S, BenchTop = 0.046f * S, TableTop = 0.077f * S, CrateTop = 0.07f * S, LogsTop = 0.054f * S;
        /// <summary>Half the table's length and the benches' distance out from its middle (local z).</summary>
        public const float TableHalf = 0.2f * S, BenchOut = 0.085f * S;
        /// <summary>The name peg's tag; the roof bird's height; the stew pot's rim.</summary>
        public const float TagH = 0.17f * S, BirdH = 0.3f * S, PotRim = 0.16f * S;
        /// <summary>B54.b: a basket's height (each one of the tower).</summary>
        public const float BasketH = 0.06f * S;
        /// <summary>A sitter's offset over the ground for a seat this high (the Sit loop sits on the ground at −0.03).</summary>
        public static float SeatLift(float top) => Mathf.Max(0f, top - 0.012f);
    }

    public static class CampScenes6
    {
        static Vector2 Across(Vector2 toCam) => new Vector2(-toCam.y, toCam.x);
        static void PropDo(CampScene s, float t, PropFx what, float value = 0f, int member = -1) =>
            s.Fx(t, CampFxKind.Prop, Vector3.zero, value, (int)what, CampAnchor.Ground, member);
        static void Jolt(CampScene s, float t) => PropDo(s, t, PropFx.Jolt);
        static void Look(CampScene s, float t, int variant) => PropDo(s, t, PropFx.Look, variant);

        /// <summary>A reach to the thing (arm x <paramref name="x"/>), held, then back.</summary>
        static CampPoseFn Reach(float x, float lean = 0f) => (ref DollPose p, float u, float l) =>
        {
            float k = In(u, 0f, 0.25f) * (1f - In(u, l - 0.2f, 0.2f));
            p.armR = Lerp(p.armR, new Vector3(x, 0f, 6f), k); p.lean += lean * k;
        };

        /// <summary>Sitting on something <paramref name="top"/> high, swinging (roll ±<paramref name="roll"/> deg at 1 Hz).</summary>
        static CampPoseFn SitOn(float top, float roll = 5f, float phase = 0f) => (ref DollPose p, float u, float l) =>
        {
            Seated(ref p); p.offset.y += PropGeo.SeatLift(top) + 0.03f; p.roll += roll * Mathf.Sin((u + phase) * Mathf.PI * 2f);
        };

        // ================================================================== B54.a the baskets: take one, bring it back full (work)
        public const float PickFor = 3f;

        /// <summary>B54.a: the gatherer lifts the top basket off the stack (arm x 80 → hit: one fewer, the stack jolts) and carries it to the
        /// picking spot <paramref name="bush"/>, picks (PickBerry, <see cref="PickFor"/> s: the pick is cut short for the box [est]), brings it
        /// back and sets it down (a squash, the jolt): the stack is whole again. <see cref="CampScene.a"/>: the lift.</summary>
        public static CampScene BasketTake(int who, Vector2 at, Vector2 stack, Vector2 bush, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.BasketTake, t0, stack) { followReach = 1.2f };
            int m = s.Add(who);
            var st = stack + toCam * 0.13f;
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, stack, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.8f, st, stack, DollStyle.Stand, Reach(80f, 15f)); s.a = t + 0.45f;
            Look(s, t + 0.45f, 1); Jolt(s, t + 0.45f); t += 0.8f;
            float w = WalkTime(st, bush, Walk);
            s.Part(m, t, t + w, bush, bush + toCam, DollStyle.Stand, null, PropKind.Basket, Walk); t += w;
            s.Part(m, t, t + PickFor, bush, bush - toCam, DollStyle.PickBerry, null, PropKind.Basket); t += PickFor;
            s.Part(m, t, t + w, st, stack, DollStyle.Stand, null, PropKind.Basket, Walk); t += w;
            s.Part(m, t, t + 0.7f, st, stack, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 25f * k; p.squash = Mathf.Lerp(1f, 0.9f, k); p.armR = Lerp(p.armR, new Vector3(60f, 0f, 6f), k); });
            Look(s, t + 0.4f, 0); Jolt(s, t + 0.4f); s.Fx(t + 0.4f, CampFxKind.Dust, G(stack, 0.01f), 0.3f, 2);
            t += 0.7f;
            Go(s, m, t, 1.2f, st + toCam * 0.2f, st + toCam, Walk);
            return s;
        }

        // ================================================================== B54.b the basket tower (life)
        /// <summary>B54.b: the drawn tower's beats: when each basket is lifted off the stack and set on the tower (scene s), the wobble and the
        /// fall (<see cref="CampScene.b"/>), and when they go back.</summary>
        public struct Tower { public float[] lift, set; public float wobble, fall, back; public Vector2 at; }

        /// <summary>B54.b: two kids carry the three baskets one by one to a spot beside the stack and set them upside down one on the other
        /// (arms x 120, the third on tiptoe x 150); the tower wobbles (3 deg, 4 Hz) and falls (the baskets roll, dust); the kids laugh and put
        /// them back side by side. <paramref name="tower"/> is the drawn tower's timing. <see cref="CampScene.a"/>: the third basket set.</summary>
        public static CampScene BasketTower(int kidA, int kidB, Vector2 atA, Vector2 atB, Vector2 stack, Vector2 toCam, float t0, out Tower tower)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.BasketTower, t0, stack) { holdFocus = true };
            int a = s.Add(kidA), b = s.Add(kidB);
            var spot = stack + acr * 0.2f + toCam * 0.06f;
            var tw0 = new Tower { lift = new float[3], set = new float[3], at = spot };
            var takeA = stack + toCam * 0.1f + acr * 0.03f; var takeB = stack + toCam * 0.1f - acr * 0.06f;
            var setA = spot + toCam * 0.07f - acr * 0.04f; var setB = spot + toCam * 0.06f + acr * 0.05f;
            float ta = WalkTime(atA, takeA, KidWalk), tb = WalkTime(atB, takeB, KidWalk);
            float t = Mathf.Max(ta, tb);
            s.Part(a, 0f, t, takeA, stack, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(b, 0f, t, takeB, stack, DollStyle.Stand, null, PropKind.None, KidWalk);
            CampPoseFn carry = (ref DollPose p, float u, float l) => { p.armL = new Vector3(70f, 0f, -10f); p.armR = new Vector3(70f, 0f, 10f); };
            // one basket: lift it off the stack at tt (0.5 s), carry it to the tower, set it on (0.7 s; the third on tiptoe, x 150); returns the end
            float One(int i, int m, float tt, Vector2 take, Vector2 set)
            {
                s.Part(m, tt, tt + 0.5f, take, stack, DollStyle.Stand, Reach(70f, 20f));
                tw0.lift[i] = tt + 0.3f; Look(s, tt + 0.3f, i + 1); Jolt(s, tt + 0.3f);
                tt += 0.5f;
                float w = WalkTime(take, set, KidWalk);
                s.Part(m, tt, tt + w, set, spot, DollStyle.Stand, carry, PropKind.None, KidWalk).poseWalking = true;
                tt += w;
                float arm = i == 2 ? 150f : 120f, lift = i == 2 ? 0.025f : 0f;
                s.Part(m, tt, tt + 0.7f, set, spot, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float k = In(u, 0f, 0.3f) * (1f - In(u, l - 0.15f, 0.15f));
                    p.armL = Lerp(new Vector3(70f, 0f, -10f), new Vector3(arm, 0f, -8f), k); p.armR = Lerp(new Vector3(70f, 0f, 10f), new Vector3(arm, 0f, 8f), k); p.offset.y += lift * k;
                });
                tw0.set[i] = tt + 0.45f;
                return tt + 0.7f;
            }
            float endA1 = One(0, a, t, takeA, setA);
            float backA = WalkTime(setA, takeA, KidWalk);
            s.Part(a, endA1, endA1 + backA, takeA, stack, DollStyle.Stand, null, PropKind.None, KidWalk);
            float endB = One(1, b, Mathf.Max(t + 0.6f, endA1 - 0.3f), takeB, setB);
            float endA2 = One(2, a, Mathf.Max(endA1 + backA, endB - 0.6f), takeA, setA);
            s.a = tw0.set[2];
            t = endA2;
            // the wobble (3 deg at 4 Hz, 1.4 s), then the fall
            tw0.wobble = t; tw0.fall = t + 1.4f; s.b = tw0.fall;
            s.Part(a, endA2, tw0.fall, setA, spot, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(40f, 0f, -30f); p.armR = new Vector3(40f, 0f, 30f); });
            s.Part(b, endB, tw0.fall, setB, spot, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(40f, 0f, -30f); p.armR = new Vector3(40f, 0f, 30f); });
            s.Fx(tw0.fall + 0.35f, CampFxKind.Dust, G(spot, 0.01f), 0.5f, 3);
            s.Fx(tw0.fall + 0.6f, CampFxKind.Dust, G(spot + acr * 0.1f, 0.01f), 0.4f, 2);
            t = tw0.fall;
            s.Part(a, t, t + 0.4f, setA + toCam * 0.03f, spot, DollStyle.Flinch);
            s.Part(b, t, t + 0.4f, setB + toCam * 0.03f, spot, DollStyle.Flinch);
            t += 0.4f;
            s.Part(a, t, t + 1.8f, setA + toCam * 0.03f, setB, DollStyle.Laugh);
            s.Part(b, t, t + 1.8f, setB + toCam * 0.03f, setA, DollStyle.Laugh);
            t += 1.8f;
            // they put them back side by side
            s.Part(a, t, t + 0.6f, spot + toCam * 0.06f, spot, DollStyle.Stand, Reach(40f, 35f));
            s.Part(b, t, t + 0.6f, spot + toCam * 0.06f + acr * 0.06f, spot, DollStyle.Stand, Reach(40f, 35f));
            t += 0.6f;
            float wb = WalkTime(spot, takeA, KidWalk);
            s.Part(a, t, t + wb, takeA, stack, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(70f, 0f, -10f); p.armR = new Vector3(70f, 0f, 10f); }, PropKind.None, KidWalk).poseWalking = true;
            s.Part(b, t, t + wb, takeB, stack, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(70f, 0f, -10f); p.armR = new Vector3(70f, 0f, 10f); }, PropKind.None, KidWalk).poseWalking = true;
            t += wb;
            s.Part(a, t, t + 0.6f, takeA, stack, DollStyle.Stand, Reach(50f, 30f));
            s.Part(b, t, t + 0.6f, takeB, stack, DollStyle.Stand, Reach(50f, 30f));
            tw0.back = t + 0.35f; Look(s, t + 0.35f, 0); Jolt(s, t + 0.35f);
            t += 0.6f;
            Go(s, a, t, 1f, takeA + toCam * 0.25f, takeA + toCam, KidWalk);
            Go(s, b, t, 1f, takeB + toCam * 0.25f - acr * 0.1f, takeB + toCam, KidWalk);
            tower = tw0;
            return s;
        }

        /// <summary>B54.b: where tower basket <paramref name="i"/> is drawn at scene second <paramref name="u"/> (box x, height, z; yaw in deg; roll
        /// in deg; false: not drawn: still on the stack or back on it). From the stack to the hands of its kid is drawn by the kid's arms.</summary>
        public static bool TowerBasket(in Tower tw, int i, float u, Vector2 stack, Vector2 across, out Vector3 at, out float roll, out bool carried)
        {
            at = default; roll = 0f; carried = false;
            if (tw.lift == null || u < tw.lift[i] || u >= tw.back) return false;
            if (u < tw.set[i]) { carried = true; return true; } // in the kid's arms (CampLife puts it at the hands)
            float h = 0.004f + i * PropGeo.BasketH * 0.92f;
            var p = tw.at;
            if (u >= tw.wobble && u < tw.fall) roll = 3f * (i + 1) * Mathf.Sin((u - tw.wobble) * 4f * Mathf.PI * 2f);
            if (u >= tw.fall)
            {
                float k = Smooth((u - tw.fall) / 0.6f);
                float side = i == 0 ? -0.5f : i == 1 ? 0.7f : 1.4f;
                p += across.normalized * (0.08f * side * k) + new Vector2(across.y, -across.x).normalized * (0.03f * i * k);
                h = Mathf.Lerp(h, 0.02f, k) + 0.02f * Bump(u, tw.fall + 0.45f, 0.25f); // a bounce on landing
                roll = Mathf.Lerp(0f, 90f + 25f * i, k);
            }
            at = new Vector3(p.x, h, p.y);
            return true;
        }

        // ================================================================== B54.c the woodpile: stack a log, fetch one (work)
        /// <summary>B54.c: <paramref name="fetch"/> false: carries a log in from <paramref name="at"/> (the chopping) and slides it onto the pile
        /// under the roof (arm x 60, a push of 0.04 u → hit: the pile jolts). True (a wet day): takes one off (arm x 60, the jolt) and carries it
        /// to the fire <paramref name="fire"/>, where it goes on (sparks). <see cref="CampScene.a"/>: the push (or the lift).</summary>
        public static CampScene WoodStack(int who, Vector2 at, Vector2 pile, Vector2 toCam, bool fetch, Vector2 fire, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.WoodStack, t0, pile) { followReach = fetch ? 1.5f : 0.6f, kind = fetch ? 1 : 0 };
            int m = s.Add(who);
            var st = pile + toCam * 0.27f; // the logs reach 0.2 out from the middle
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, pile, DollStyle.Stand, fetch ? null : (ref DollPose p, float u, float l) => { p.armL = new Vector3(70f, 0f, -12f); p.armR = new Vector3(70f, 0f, 12f); }, PropKind.None, Walk).poseWalking = true; // #693 retakes: the log in both arms (drawn)
            CampPoseFn push = (ref DollPose p, float u, float l) =>
            {
                float k = In(u, 0f, 0.3f) * (1f - In(u, l - 0.25f, 0.25f)); float push2 = 0.04f * In(u, 0.35f, 0.3f) * (1f - In(u, l - 0.25f, 0.25f));
                p.armL = Lerp(p.armL, new Vector3(60f, 0f, -10f), k); p.armR = Lerp(p.armR, new Vector3(60f, 0f, 10f), k); p.lean += 18f * k; p.offset.z -= push2;
            };
            s.Part(m, t, t + 1.0f, st, pile, DollStyle.Stand, push, PropKind.None);
            s.a = t + 0.6f; Jolt(s, t + 0.6f); s.Fx(t + 0.6f, CampFxKind.Chips, G(pile + toCam * 0.2f, 0.12f), 0.25f, 2);
            t += 1.0f;
            if (!fetch) { Go(s, m, t, 1.2f, st + toCam * 0.25f, st + toCam, Walk); return s; }
            var fs = fire + Unit(st - fire, toCam) * 0.17f;
            float w = WalkTime(st, fs, Walk);
            s.Part(m, t, t + w, fs, fire, DollStyle.Stand, null, PropKind.Log, Walk).slot = PropSlot.Shoulder; t += w;
            s.Part(m, t, t + 0.8f, fs, fire, DollStyle.FeedFire, null, PropKind.None);
            s.Fx(t + 0.45f, CampFxKind.Sparks, G(fire, 0.05f), 0.5f, 4);
            t += 0.8f;
            Go(s, m, t, 1f, fs + toCam * 0.2f, fs + toCam, Walk);
            return s;
        }

        // ================================================================== B54.d the woodpile: shelter from the rain (life)
        public const float HuddleFor = 8f;

        /// <summary>B54.d: <paramref name="rain"/>: 2-3 people hurry under the roof (a run) and stand pressed together (0.06 u apart), shoulders up
        /// (squash 0.95), looking out at the rain, <see cref="HuddleFor"/> s, drops falling off the roof's edge; then they go. Dry: the kids sit
        /// on the low logs, swinging their legs. <see cref="CampScene.a"/>: all under the roof.</summary>
        public static CampScene WoodShelter(IList<int> who, IList<Vector2> at, Vector2 pile, Vector2 toCam, bool rain, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.WoodShelter, t0, pile) { holdFocus = true, kind = rain ? 1 : 0 };
            int n = Mathf.Min(who.Count, 3);
            float t = 0f;
            var ms = new int[n]; var spots = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(who[i]);
                float off = (i - (n - 1) * 0.5f) * (rain ? 0.06f : 0.1f);
                spots[i] = pile + toCam * (rain ? 0.23f : 0.2f) + acr * off; // pressed to the logs' front, under the eave; dry: on the low logs
                float w = WalkTime(at[i], spots[i], rain ? KidRun : KidWalk);
                var go = s.Part(ms[i], 0f, w, spots[i], spots[i] + toCam, DollStyle.Stand, null, PropKind.None, rain ? KidRun : KidWalk); go.run = rain;
                t = Mathf.Max(t, w);
            }
            s.a = t;
            for (int i = 0; i < n; i++)
            {
                float ph = i * 0.31f;
                if (rain) s.Part(ms[i], s.FirstFrom(i) + 0f + WalkTime(at[i], spots[i], KidRun), t + HuddleFor, spots[i], spots[i] + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    p.squash = Mathf.Lerp(p.squash, 0.95f, In(u, 0f, 0.3f)); p.armL = new Vector3(30f, 0f, -4f); p.armR = new Vector3(30f, 0f, 4f);
                    p.yaw += 12f * Mathf.Sin((u + ph) * 0.9f); // looking out, now this way, now that
                });
                else s.Part(ms[i], WalkTime(at[i], spots[i], KidWalk), t + HuddleFor, spots[i], spots[i] + toCam, DollStyle.Sit, SitOn(PropGeo.LogsTop, 5f, ph));
            }
            if (rain) for (int k = 0; k < 16; k++) s.Fx(t + 0.3f + k * 0.45f, CampFxKind.Droplets, G(pile + toCam * 0.3f + acr * ((k % 4 - 1.5f) * 0.1f), 0.32f), 0.7f, 4); // drops off the roof's edge, in front of them
            t += HuddleFor;
            for (int i = 0; i < n; i++) Go(s, ms[i], t, 1.2f, spots[i] + toCam * 0.35f + acr * ((i - 1) * 0.15f), spots[i] + toCam, KidWalk, rain);
            return s;
        }

        // ================================================================== B54.e the name stone: cut a new name (work)
        public const float CutFor = 6f;

        /// <summary>B54.e: the toolmaker kneels at the stone and taps with a stone in the hand (the hammer's stroke, x 70 ↔ 100 at 2 Hz) → each
        /// tap a few grey chips, <see cref="CutFor"/> s; then the new row shows (<see cref="PropFx.NameRow"/>). <see cref="CampScene.a"/>: the first tap.</summary>
        public static CampScene NameCut(int who, Vector2 at, Vector2 stone, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.NameCut, t0, stone) { holdFocus = true };
            int m = s.Add(who);
            var kneel = stone + toCam * 0.13f + acr * 0.09f; // beside the face, so the stone and the tapping hand both show
            float t = WalkTime(at, kneel, Walk);
            s.Part(m, 0f, t, kneel, stone, DollStyle.Stand, null, PropKind.None, Walk);
            s.Tip(m, t, t + 0.6f + CutFor, CampTipKind.Stone, 1.8f); // #693 retakes: the tapping stone drawn 1.8x
            s.Part(m, t, t + 0.6f, kneel, stone, DollStyle.Stand, KneelPose); t += 0.6f;
            s.a = t + 0.25f;
            s.Part(m, t, t + CutFor, kneel, stone, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                KneelPose(ref p, 1f, l); p.lean -= 10f;
                float ph = (u * 2f) % 1f; float x = ph < 0.5f ? Mathf.Lerp(100f, 70f, Smooth(ph * 2f)) : Mathf.Lerp(70f, 100f, Smooth((ph - 0.5f) * 2f));
                p.armR = new Vector3(x, 0f, 8f); p.armL = new Vector3(40f, 0f, -20f);
            });
            for (int i = 0; i < (int)(CutFor * 2f); i++) s.Fx(t + 0.25f + i * 0.5f, CampFxKind.Chips, G(stone + toCam * 0.06f + acr * 0.03f, 0.2f), 0.6f, 4);
            t += CutFor;
            PropDo(s, t - 0.2f, PropFx.NameRow); s.Fx(t - 0.2f, CampFxKind.Dust, G(stone + toCam * 0.06f, 0.22f), 0.8f, 5); // the new row, in a puff of stone dust
            s.Part(m, t, t + 0.8f, kneel, stone, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, 1f, l); NodPose(ref p, u, l); });
            t += 0.8f;
            Go(s, m, t, 1.2f, kneel + toCam * 0.25f, kneel + toCam, Walk);
            return s;
        }

        // ================================================================== B54.f the name stone: kids trace the names (life)
        /// <summary>B54.f: a kid runs a finger along a row (arm x 110 [est: the rows start above a kid's head at x 90], the hand sweeps 0.06 u
        /// sideways over 1 s, twice), then rubs the stone with the sleeve (arm x 100, circles at 2 Hz, 1.5 s); a grown-up (−1: none) stands by
        /// and nods. <see cref="CampScene.a"/>: the first trace.</summary>
        public static CampScene NameTrace(int kid, Vector2 at, int grown, Vector2 grownAt, Vector2 stone, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.NameTrace, t0, stone) { holdFocus = true };
            int m = s.Add(kid);
            var st = stone + toCam * 0.1f + acr * 0.03f;
            float t = WalkTime(at, st, KidWalk);
            s.Part(m, 0f, t, st, stone, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.a = t + 0.3f;
            for (int i = 0; i < 2; i++)
            {
                s.Part(m, t, t + 1.2f, st, stone, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float k = In(u, 0f, 0.2f) * (1f - In(u, l - 0.15f, 0.15f));
                    p.armR = Lerp(p.armR, new Vector3(125f, 0f, Mathf.Lerp(-35f, 40f, Smooth((u - 0.1f) / 1f))), k); // the hand sweeps along the row (#693 retakes: wider)
                    p.yaw += Mathf.Lerp(-22f, 22f, Smooth((u - 0.1f) / 1f)) * k; p.offset.y += 0.025f * k;
                });
                s.Fx(t + 0.6f, CampFxKind.Dust, new Vector3(0f, 0.01f, 0f), 0.35f, 2, CampAnchor.Hand, m); // the finger along the names
                t += 1.2f;
            }
            s.Part(m, t, t + 1.5f, st, stone, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float k = In(u, 0f, 0.2f) * (1f - In(u, l - 0.15f, 0.15f)); float a = u * 2f * Mathf.PI * 2f;
                p.armR = Lerp(p.armR, new Vector3(100f + 12f * Mathf.Sin(a), 0f, 8f + 14f * Mathf.Cos(a)), k); p.lean += 8f * k;
            });
            s.Fx(t + 0.7f, CampFxKind.Dust, G(stone + toCam * 0.05f, 0.12f), 0.2f, 1);
            t += 1.5f;
            if (grown >= 0)
            {
                int g = s.Add(grown);
                var gs = stone + toCam * 0.28f - acr * 0.1f;
                float gw = WalkTime(grownAt, gs, Walk);
                s.Part(g, 0f, gw, gs, st, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(g, Mathf.Max(gw, t - 0.6f), Mathf.Max(gw, t - 0.6f) + 1f, gs, st, DollStyle.Stand, NodPose);
            }
            s.Part(m, t, t + 0.5f, st, st + toCam, DollStyle.Stand);
            Go(s, m, t + 0.5f, 1f, st + toCam * 0.3f + acr * 0.1f, st + toCam, KidWalk);
            return s;
        }

        // ================================================================== B54.g the charms: a kid flicks one (life)
        /// <summary>B54.g: a kid passing under the tree stops below a charm, jumps (a hop of 0.06 u, arm x 170) and flicks it → hit: the charms
        /// jolt (it spins, the others sway); the kid watches it, looking up, 1.5 s. <see cref="CampScene.a"/>: the flick.</summary>
        public static CampScene CharmFlick(int kid, Vector2 at, Vector2 under, Vector2 toCam, int jumps, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.CharmFlick, t0, under) { holdFocus = true };
            int m = s.Add(kid);
            var st = under + toCam * 0.03f;
            float t = WalkTime(at, st, KidWalk);
            s.Part(m, 0f, t, st, under - toCam, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(m, t, t + 0.5f, st, under - toCam, DollStyle.LookUp); t += 0.5f;
            s.a = t + 0.35f;
            for (int i = 0; i < Mathf.Clamp(jumps, 1, 2); i++)
            {
                s.Part(m, t, t + 0.6f, st, under - toCam, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float k = Bump(u, 0.05f, 0.5f); p.offset.y += 0.06f * k; p.armR = Lerp(p.armR, new Vector3(170f, 0f, 6f), In(u, 0f, 0.2f) * (1f - In(u, 0.45f, 0.15f)));
                    p.squash = u < 0.08f ? 0.9f : 1f;
                });
                Jolt(s, t + 0.33f);
                t += 0.6f;
                s.Part(m, t, t + 1.5f, st, under - toCam, DollStyle.LookUp); t += 1.5f;
            }
            Go(s, m, t, 1f, st + toCam * 0.3f + Across(toCam) * 0.15f, st + toCam, KidWalk);
            return s;
        }

        // ================================================================== B54.i the big pot: kids drum on it (life)
        /// <summary>B54.i: 1-2 kids sit at the pot with a twig each and drum (arms x 90 ↔ 60 at 4 Hz in turn) → each beat the pot jolts; after
        /// <paramref name="drumFor"/> s a grown-up (−1: none) comes and puts a hand over it (arm x 80): they stop and laugh.
        /// <see cref="CampScene.a"/>: the first beat.</summary>
        public static CampScene PotDrum(IList<int> kids, IList<Vector2> at, int grown, Vector2 grownAt, Vector2 pot, Vector2 toCam, float drumFor, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.PotDrum, t0, pot) { holdFocus = true };
            int n = Mathf.Min(2, kids.Count);
            float t = 0f; var ms = new int[n]; var seats = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(kids[i]);
                seats[i] = pot + (toCam * 0.15f + acr * (n == 1 ? 0.04f : (i == 0 ? -0.1f : 0.1f))).normalized * 0.17f;
                float w = WalkTime(at[i], seats[i], KidWalk);
                s.Part(ms[i], 0f, w, seats[i], pot, DollStyle.Stand, null, PropKind.Twig, KidWalk);
                t = Mathf.Max(t, w);
            }
            for (int i = 0; i < n; i++) s.Part(ms[i], WalkTime(at[i], seats[i], KidWalk), t + 0.5f, seats[i], pot, DollStyle.Sit, (ref DollPose p, float u, float l) => Seated(ref p), PropKind.Twig);
            t += 0.5f;
            s.a = t + 0.06f;
            for (int i = 0; i < n; i++)
            {
                float ph = i * 0.125f;
                s.Part(ms[i], t, t + drumFor, seats[i], pot, DollStyle.Sit, (ref DollPose p, float u, float l) =>
                {
                    Seated(ref p); p.lean += 10f;
                    float r = Mathf.Sin((u + ph) * 4f * Mathf.PI * 2f), q = Mathf.Sin((u + ph + 0.125f) * 4f * Mathf.PI * 2f);
                    p.armR = new Vector3(85f + 40f * r, 0f, 10f); p.armL = new Vector3(85f + 40f * q, 0f, -10f); p.offset.y += 0.006f * Mathf.Abs(r); // #693 retakes: x 45 <-> 125
                }, PropKind.Twig);
            }
            for (float b = 0.06f; b < drumFor; b += 0.25f) { Jolt(s, t + b); if (((int)(b / 0.25f)) % 2 == 0) s.Fx(t + b, CampFxKind.Chips, G(pot + toCam * 0.06f, PropGeo.PotRim * 0.7f), 0.3f, 2); }
            t += drumFor;
            if (grown >= 0)
            {
                int g = s.Add(grown);
                var gs = pot + toCam * 0.1f - acr * 0.14f;
                float w = WalkTime(grownAt, gs, Walk);
                s.Part(g, Mathf.Max(0f, t - w - 0.2f), t - 0.2f, gs, pot, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(g, t - 0.2f, t + 1.6f, gs, pot, DollStyle.Stand, Reach(80f, 20f));
                s.Part(g, t + 1.6f, t + 2.6f, gs, seats[0], DollStyle.Laugh);
            }
            for (int i = 0; i < n; i++)
            {
                s.Part(ms[i], t, t + 0.4f, seats[i], pot, DollStyle.Sit, (ref DollPose p, float u, float l) => { Seated(ref p); p.armL = new Vector3(40f, 0f, -20f); p.armR = new Vector3(40f, 0f, 20f); }, PropKind.Twig);
                s.Part(ms[i], t + 0.4f, t + 2.6f, seats[i], pot, DollStyle.Laugh, null, PropKind.Twig);
                Go(s, ms[i], t + 2.6f, 1f, seats[i] + toCam * 0.3f, seats[i] + toCam, KidWalk);
            }
            return s;
        }

        // ================================================================== B54.j the axe stump: split wood (work)
        /// <summary>B54.j: the woodcutter sets a short log on the stump (arm x 60), pulls the axe out (lean −10, a 0.3 s heave: the axe leaves
        /// the stump), splits (two chops; the second → hit: two halves fall either side, chips) and sets the axe back in (a jolt). Drawn log
        /// timing in <see cref="CampScene.b"/> (log set) and <see cref="CampScene.a"/> (the split). </summary>
        public static CampScene StumpSplit(int who, Vector2 at, Vector2 stump, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.StumpSplit, t0, stump) { holdFocus = true };
            int m = s.Add(who);
            var st = stump + acr * 0.16f + toCam * 0.12f; // side on and in front, so the stump and the falling halves show
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, stump, DollStyle.Stand, null, PropKind.Log, Walk).slot = PropSlot.Arms;
            s.Part(m, t, t + 0.7f, st, stump, DollStyle.Stand, Reach(60f, 20f), PropKind.None); s.b = t + 0.4f; t += 0.7f;
            s.Part(m, t, t + 0.6f, st, stump, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float k = In(u, 0f, 0.2f); p.armR = Lerp(p.armR, new Vector3(70f, 0f, 6f), k); p.armL = Lerp(p.armL, new Vector3(70f, 0f, -6f), k); p.lean -= 10f * Bump(u, 0.15f, 0.35f);
            });
            Look(s, t + 0.3f, 1); t += 0.6f;
            s.Part(m, t, t + 1.6f, st, stump, DollStyle.Chop, null, PropKind.Axe);
            s.a = t + 1.3f;
            s.Fx(t + 0.5f, CampFxKind.Chips, G(stump, PropGeo.StumpTop + 0.03f), 0.3f, 2);
            s.Fx(t + 1.3f, CampFxKind.Chips, G(stump, PropGeo.StumpTop + 0.03f), 0.45f, 4);
            s.Fx(t + 1.7f, CampFxKind.Dust, G(stump + acr * 0.08f, 0.01f), 0.3f, 2);
            t += 1.6f;
            s.Part(m, t, t + 0.6f, st, stump, DollStyle.Stand, Reach(55f, 15f), PropKind.Axe);
            Look(s, t + 0.4f, 0); Jolt(s, t + 0.4f); t += 0.6f;
            s.Part(m, t, t + 0.7f, st, stump, DollStyle.Stand, Reach(40f, 35f)); // gathers the halves
            t += 0.7f;
            Go(s, m, t, 1.2f, st + toCam * 0.25f, st + toCam, Walk).prop = PropKind.Log;
            return s;
        }

        // ================================================================== B54.k the axe stump: kids sit on it (life)
        /// <summary>B54.k: a kid sits on the stump's edge beside the axe handle (never touched), swinging (roll ±5 deg at 1 Hz), a friend (−1:
        /// none) on the other edge, <paramref name="sitFor"/> s. <see cref="CampScene.a"/>: sat down.</summary>
        public static CampScene StumpSit(int kid, Vector2 at, int friend, Vector2 friendAt, Vector2 stump, Vector2 toCam, float sitFor, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.StumpSit, t0, stump) { holdFocus = true };
            int m = s.Add(kid);
            var seat = stump + toCam * 0.06f + acr * 0.035f;
            float t = WalkTime(at, seat + toCam * 0.06f, KidWalk);
            s.Part(m, 0f, t, seat + toCam * 0.06f, stump, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(m, t, t + 0.4f, seat, seat + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += PropGeo.SeatLift(PropGeo.StumpTop) * In(u, 0f, 0.4f); }, PropKind.None, 0.2f).poseWalking = true;
            t += 0.4f; s.a = t;
            s.Part(m, t, t + sitFor, seat, seat + toCam, DollStyle.Sit, SitOn(PropGeo.StumpTop));
            if (friend >= 0)
            {
                int f = s.Add(friend);
                var fs = stump + toCam * 0.06f - acr * 0.035f;
                float w = WalkTime(friendAt, fs + toCam * 0.06f, KidWalk);
                s.Part(f, 0f, w, fs + toCam * 0.06f, stump, DollStyle.Stand, null, PropKind.None, KidWalk);
                float up = Mathf.Max(w + 0.4f, t + 0.8f);
                s.Part(f, w, up, fs, fs + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += PropGeo.SeatLift(PropGeo.StumpTop) * In(u, l - 0.4f, 0.4f); }, PropKind.None, 0.2f).poseWalking = true;
                s.Part(f, up, Mathf.Max(up + 1f, t + sitFor), fs, fs + toCam, DollStyle.Sit, SitOn(PropGeo.StumpTop, 5f, 0.5f));
                Go(s, f, Mathf.Max(up + 1f, t + sitFor), 1f, fs + toCam * 0.3f - acr * 0.1f, fs + toCam, KidWalk);
            }
            t += sitFor;
            Go(s, m, t, 1f, seat + toCam * 0.3f + acr * 0.1f, seat + toCam, KidWalk);
            return s;
        }

        // ================================================================== B54.l the staff: the headman's walk round (work)
        /// <summary>B54.l: the headman bends and takes the staff from its stone (arm x 60 → 100: the stone is bare), walks round the
        /// <paramref name="round"/> points with it (planted at each step, arm x 30 ↔ 40), leans on it at the last one (the meeting, 3 s), comes
        /// back and puts it back. <see cref="CampScene.a"/>: the staff taken up.</summary>
        public static CampScene StaffWalk(int headman, Vector2 at, Vector2 stone, IList<Vector2> round, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.StaffWalk, t0, stone) { followReach = 1.6f };
            int m = s.Add(headman);
            var st = stone + toCam * 0.1f;
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, stone, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.9f, st, stone, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, 0.6f); p.lean += 30f * k; p.armR = Lerp(p.armR, new Vector3(Mathf.Lerp(60f, 100f, In(u, 0.3f, 0.4f)), 0f, 6f), In(u, 0f, 0.2f)); });
            s.a = s.c = t + 0.4f; Look(s, t + 0.4f, 1); t += 0.9f;
            CampPoseFn planted = (ref DollPose p, float u, float l) => { p.armR = new Vector3(35f + 5f * Mathf.Sin(u * Mathf.PI * 2f * 1.6f), 0f, 12f); p.lean -= 4f; };
            var from = st;
            for (int i = 0; i < round.Count; i++)
            {
                float w = WalkTime(from, round[i], Walk);
                var pt = s.Part(m, t, t + w, round[i], round[i] + Dir(from, round[i], toCam), DollStyle.Stand, planted, PropKind.None, Walk); pt.poseWalking = true;
                t += w; from = round[i];
                bool last = i == round.Count - 1;
                s.Part(m, t, t + (last ? 3f : 0.6f), round[i], round[i] + toCam, last ? DollStyle.Talk : DollStyle.Look, last ? (ref DollPose p, float u, float l) => { p.armR = new Vector3(20f, 0f, 16f); p.roll += 6f; } : planted, PropKind.None);
                t += last ? 3f : 0.6f;
            }
            float wb = WalkTime(from, st, Walk);
            s.Part(m, t, t + wb, st, stone, DollStyle.Stand, planted, PropKind.None, Walk).poseWalking = true; t += wb;
            s.Part(m, t, t + 0.8f, st, stone, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 25f * k; p.armR = Lerp(p.armR, new Vector3(70f, 0f, 6f), k); }, PropKind.None);
            s.d = t + 0.45f; Look(s, t + 0.45f, 0); Jolt(s, t + 0.45f); t += 0.8f;
            Go(s, m, t, 1f, st + toCam * 0.2f, st + toCam, Walk);
            return s;
        }

        // ================================================================== B54.m the staff: kids play headman (life)
        /// <summary>B54.m: one kid picks the staff up and struts (lean −10, chin up, the staff planted at each step), the others bow in a wave as
        /// it passes; the headman (−1: none) comes, stops, hands on hips and laughs; the kid puts it back fast and runs. <see cref="CampScene.a"/>:
        /// the first bow.</summary>
        public static CampScene StaffPlay(int kid, Vector2 at, IList<int> others, IList<Vector2> othersAt, int headman, Vector2 headAt, Vector2 stone, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.StaffPlay, t0, stone + toCam * 0.15f) { holdFocus = true };
            int m = s.Add(kid);
            var st = stone + toCam * 0.09f;
            float t = WalkTime(at, st, KidWalk);
            s.Part(m, 0f, t, st, stone, DollStyle.Stand, null, PropKind.None, KidWalk);
            int no = Mathf.Min(2, others.Count);
            var bowAt = new Vector2[no]; var om = new int[no];
            for (int i = 0; i < no; i++)
            {
                om[i] = s.Add(others[i]);
                bowAt[i] = stone + toCam * 0.32f + acr * (0.08f + 0.1f * i) * (i % 2 == 0 ? 1f : 1f);
                s.Part(om[i], 0f, WalkTime(othersAt[i], bowAt[i], KidWalk), bowAt[i], stone, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(om[i], WalkTime(othersAt[i], bowAt[i], KidWalk), WalkTime(othersAt[i], bowAt[i], KidWalk) + 0.01f, bowAt[i], stone, DollStyle.Stand);
            }
            s.Part(m, t, t + 0.8f, st, stone, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, 0.6f); p.lean += 30f * k; p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), In(u, 0f, 0.2f)); });
            s.c = t + 0.4f; Look(s, t + 0.4f, 1); t += 0.8f;
            // the strut: along the row of bowing kids and back
            CampPoseFn strut = (ref DollPose p, float u, float l) => { p.lean -= 10f; p.armR = new Vector3(35f + 6f * Mathf.Sin(u * 9f), 0f, 12f); p.armL = new Vector3(0f, 0f, -30f); p.offset.y += 0.008f * Mathf.Abs(Mathf.Sin(u * 9f)); };
            var a1 = stone + toCam * 0.2f - acr * 0.1f; var a2 = stone + toCam * 0.2f + acr * 0.35f;
            float w1 = WalkTime(st, a1, 0.14f);
            s.Part(m, t, t + w1, a1, a1 + acr, DollStyle.Stand, strut, PropKind.None, 0.14f).poseWalking = true; t += w1;
            float w2 = WalkTime(a1, a2, 0.14f);
            s.Part(m, t, t + w2, a2, a2 + acr, DollStyle.Stand, strut, PropKind.None, 0.14f).poseWalking = true;
            s.a = t + w2 * 0.35f;
            for (int i = 0; i < no; i++)
            {
                float bt = Mathf.Max(t + w2 * (0.35f + 0.25f * i), s.parts[1 + i][^1].to);
                s.Part(om[i], bt, bt + 1.4f, bowAt[i], bowAt[i] - toCam, DollStyle.Bow);
            }
            t += w2;
            s.Part(m, t, t + 1.2f, a2, a2 + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean -= 12f; p.armR = new Vector3(40f, 0f, 12f); p.armL = new Vector3(20f, 0f, -70f); }, PropKind.None);
            t += 1.2f;
            if (headman >= 0)
            {
                int h = s.Add(headman);
                var hs = a2 + acr * 0.22f + toCam * 0.05f;
                float w = WalkTime(headAt, hs, Walk);
                s.Part(h, Mathf.Max(0f, t - w), t, hs, a2, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(h, t, t + 0.8f, hs, a2, DollStyle.Stand, HipsPose);
                s.Part(h, t + 0.8f, t + 2.2f, hs, a2, DollStyle.Laugh, HipsPose);
            }
            s.Part(m, t, t + 0.6f, a2, a2 - acr, DollStyle.Freeze, null, PropKind.None); t += 0.6f;
            float wr = WalkTime(a2, st, KidRun);
            var r = s.Part(m, t, t + wr, st, stone, DollStyle.Stand, null, PropKind.None, KidRun); r.run = true; t += wr;
            s.Part(m, t, t + 0.4f, st, stone, DollStyle.Stand, Reach(60f, 25f), PropKind.None);
            s.d = t + 0.25f; Look(s, t + 0.25f, 0); Jolt(s, t + 0.25f); t += 0.4f;
            Go(s, m, t, 1f, st + toCam * 0.4f - acr * 0.2f, st + toCam, KidRun, true);
            for (int i = 0; i < no; i++) { float lf = Mathf.Max(t - 1.6f, s.parts[1 + i][^1].to); s.Part(om[i], lf, Mathf.Max(lf, t), bowAt[i], a2, DollStyle.Laugh); Go(s, om[i], Mathf.Max(lf, t), 1f, bowAt[i] + toCam * 0.2f, bowAt[i] + toCam, KidRun, true); }
            return s;
        }

        // ================================================================== B54.n flour: carry the sacks (work)
        /// <summary>B54.n: a villager takes a sack from the heap (on the shoulder), walks heavy to the <paramref name="door"/>, sets it down there (a
        /// squash, a small white puff). <see cref="CampScene.a"/>: set down.</summary>
        public static CampScene FlourCarry(int who, Vector2 at, Vector2 sacks, Vector2 door, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.FlourCarry, t0, sacks) { followReach = 2f };
            int m = s.Add(who);
            var st = sacks + toCam * 0.15f;
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, sacks, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.9f, st, sacks, DollStyle.Lift, null, PropKind.None); t += 0.9f;
            CampPoseFn heavy = (ref DollPose p, float u, float l) => { p.lean += 8f; p.roll += 3f * Mathf.Sin(u * 7f); p.armL = new Vector3(150f, 0f, -20f); p.offset.y -= 0.004f * Mathf.Abs(Mathf.Sin(u * 7f)); };
            float w = WalkTime(st, door, 0.16f);
            var c = s.Part(m, t, t + w, door, door + Dir(st, door, toCam), DollStyle.Stand, heavy, PropKind.Sack, 0.16f); c.slot = PropSlot.Shoulder; c.poseWalking = true;
            t += w;
            s.Part(m, t, t + 0.8f, door, door + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 30f * k; p.squash = Mathf.Lerp(1f, 0.9f, k); }, PropKind.None);
            s.a = t + 0.45f; s.Fx(t + 0.45f, CampFxKind.Dust, G(door + toCam * 0.06f, 0.01f), 0.35f, 3);
            t += 0.8f;
            Go(s, m, t, 1f, door + toCam * 0.25f, door + toCam, Walk);
            return s;
        }

        // ================================================================== B54.o flour: the flour fight (life)
        /// <summary>B54.o: 3-4 kids at the split sack: by turns one scoops a handful (arm x 40 → 120) and throws it at another (an arc, h 0.06:
        /// a white puff on the one it hits, who keeps a white blotch until the evening wash); then they chase about. <see cref="CampScene.a"/>:
        /// the first hit.</summary>
        public static CampScene FlourFight(IList<int> kids, IList<Vector2> at, Vector2 sack, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.FlourFight, t0, sack + toCam * 0.2f) { holdFocus = true };
            int n = Mathf.Clamp(kids.Count, 2, 4);
            var ms = new int[n]; var spot = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(kids[i]);
                float a = (i / (float)n) * Mathf.PI * 2f + 0.4f;
                spot[i] = sack + toCam * 0.25f + (acr * Mathf.Cos(a) + toCam * Mathf.Sin(a) * 0.5f) * 0.16f;
                float w = WalkTime(at[i], spot[i], KidWalk);
                s.Part(ms[i], 0f, w, spot[i], sack, DollStyle.Stand, null, PropKind.None, KidWalk);
                t = Mathf.Max(t, w);
            }
            s.a = -1f;
            int throws = n + 1;
            for (int k = 0; k < throws; k++)
            {
                int thrower = k % n, target = (k + 1 + (k % 2)) % n; if (target == thrower) target = (thrower + 1) % n;
                var dip = sack + Unit(spot[thrower] - sack, toCam) * 0.09f;
                float hit = t + 1.05f;
                for (int i = 0; i < n; i++) if (i != thrower) s.Part(ms[i], t, i == target ? hit : t + 1.4f, spot[i], spot[thrower], i == target ? DollStyle.Stand : DollStyle.Laugh);
                s.Part(ms[thrower], t, t + 0.5f, dip, sack, DollStyle.Stand, (ref DollPose p, float u, float l) => { float q = In(u, 0f, 0.3f); p.lean += 30f * q; p.armR = Lerp(p.armR, new Vector3(40f, 0f, 6f), q); }, PropKind.None, KidRun);
                s.Part(ms[thrower], t + 0.5f, t + 0.9f, dip, spot[target], DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armR = Lerp(new Vector3(40f, 0f, 6f), new Vector3(120f, 0f, 6f), In(u, 0f, 0.2f)); });
                s.arcs.Add(new CampArc { member = thrower, from = t + 0.7f, seconds = 0.35f, h = 0.06f, to = G(spot[target], 0.08f) });
                if (s.a < 0f) s.a = hit;
                s.Fx(hit, CampFxKind.Dust, G(spot[target], 0.07f), 0.95f, 7); // #693 retakes: a big white puff
                PropDo(s, hit, PropFx.Flour, 1f, target);
                s.Part(ms[target], hit, hit + 0.35f, spot[target] - Unit(spot[thrower] - spot[target], toCam) * 0.03f, spot[thrower], DollStyle.Flinch);
                s.Part(ms[thrower], t + 0.9f, t + 1.4f, spot[thrower], spot[target], DollStyle.Laugh);
                t += 1.4f;
            }
            // then the chase: everyone runs round the sack once
            for (int i = 0; i < n; i++)
            {
                var c1 = sack + toCam * 0.25f + Rot(acr, 90f + i * 45f) * 0.32f; var c2 = sack + toCam * 0.25f + Rot(acr, 200f + i * 45f) * 0.3f;
                Go(s, ms[i], t, 1.2f, c1, c1 + acr, KidRun, true);
                Go(s, ms[i], t + 1.2f, 1.2f, c2, c2 - acr, KidRun, true);
                s.Part(ms[i], t + 2.4f, t + 3.4f, c2, sack, DollStyle.Laugh);
            }
            return s;
        }

        // ================================================================== B54.p the roof bird: straighten it after a storm (work)
        public const float LadderH = 0.18f;
        /// <summary>#693 retakes [est]: how high the roofer climbs (the drawn ladder is 2.2x so the hands reach the bird; the spec's 0.06 left them
        /// at the pole's foot).</summary>
        public const float ClimbH = 0.13f, LadderScale = 2.2f;

        /// <summary>B54.p: the roofer leans a short ladder on the ridge pole (drawn from <see cref="CampScene.b"/> to the scene's end), climbs two
        /// rungs (+0.06 u), sets the bird upright (it tips from 20 to 0 deg, a 0.3 s heave) and rubs it with a cloth (arm x 150, circles 1 s), then
        /// climbs down and takes the ladder away. <see cref="CampScene.a"/>: upright.</summary>
        public static CampScene BirdStraighten(int who, Vector2 at, Vector2 pole, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.BirdStraighten, t0, pole) { holdFocus = true };
            int m = s.Add(who);
            var foot = pole + toCam * 0.1f + acr * 0.03f;
            float t = WalkTime(at, foot, Walk);
            s.Part(m, 0f, t, foot, pole, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.7f, foot, pole, DollStyle.Stand, Reach(110f, 5f)); s.b = t + 0.35f; t += 0.7f;
            s.Part(m, t, t + 1.0f, foot - toCam * 0.03f, pole, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float c = In(u, 0f, l); p.offset.y += ClimbH * c; float ph = Mathf.Sin(u * Mathf.PI * 4f);
                p.armL = new Vector3(120f + 20f * ph, 0f, -8f); p.armR = new Vector3(120f - 20f * ph, 0f, 8f);
            }, PropKind.None, 0.05f).poseWalking = true;
            t += 1.0f;
            s.Part(m, t, t + 0.8f, foot - toCam * 0.03f, pole, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                p.offset.y += ClimbH; float k = In(u, 0f, 0.2f); p.armL = Lerp(p.armL, new Vector3(160f, 0f, -8f), k); p.armR = Lerp(p.armR, new Vector3(160f, 0f, 8f), k); p.lean -= 10f * Bump(u, 0.3f, 0.4f);
            });
            s.a = t + 0.45f; PropDo(s, t + 0.45f, PropFx.Upright); t += 0.8f;
            s.Part(m, t, t + 1.2f, foot - toCam * 0.03f, pole, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                p.offset.y += ClimbH; float a = u * Mathf.PI * 2f * 1.5f; p.armR = new Vector3(150f + 10f * Mathf.Sin(a), 0f, 8f + 14f * Mathf.Cos(a)); p.armL = new Vector3(120f, 0f, -8f);
            });
            t += 1.2f;
            s.Part(m, t, t + 0.8f, foot, pole, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += ClimbH * (1f - In(u, 0f, l)); }, PropKind.None, 0.05f).poseWalking = true;
            t += 0.8f;
            s.Part(m, t, t + 0.6f, foot, pole, DollStyle.Stand, Reach(110f, 5f)); t += 0.6f;
            s.d = t - 0.3f; // the ladder taken down
            Go(s, m, t, 1.2f, foot + toCam * 0.3f + acr * 0.1f, foot + toCam, Walk);
            return s;
        }

        // ================================================================== B54.q the roof bird: kids fly under it (life)
        /// <summary>B54.q: 1-2 kids, arms out (z ∓90) flapping (z ∓90 ↔ ∓60 at 3 Hz), run a circle (r 0.17 u) round the pole, looking up at the bird,
        /// <paramref name="laps"/> laps, then stop and look up. <see cref="CampScene.a"/>: off they fly.</summary>
        public static CampScene BirdFly(IList<int> kids, IList<Vector2> at, Vector2 pole, Vector2 toCam, int laps, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.BirdFly, t0, pole) { holdFocus = true };
            int n = Mathf.Min(2, kids.Count);
            float t = 0f; const float R = 0.17f;
            var ms = new int[n];
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(kids[i]);
                var st = pole + Rot(toCam, i * 180f) * R;
                float w = WalkTime(at[i], st, KidWalk);
                s.Part(ms[i], 0f, w, st, pole, DollStyle.Stand, null, PropKind.None, KidWalk);
                t = Mathf.Max(t, w);
            }
            s.a = t;
            CampPoseFn flap = (ref DollPose p, float u, float l) => { float f = Mathf.Sin(u * 3f * Mathf.PI * 2f); p.armL = new Vector3(10f, 0f, -62f - 30f * f); p.armR = new Vector3(10f, 0f, 62f + 30f * f); p.lean -= 8f; p.offset.y += 0.012f * (0.5f + 0.5f * f); }; // #693 retakes: z 32 <-> 92
            const int Steps = 8;
            for (int i = 0; i < n; i++)
            {
                float tt = t;
                for (int k = 1; k <= Steps * Mathf.Clamp(laps, 1, 3); k++)
                {
                    var q = pole + Rot(toCam, i * 180f + k * 360f / Steps) * R;
                    var nx = pole + Rot(toCam, i * 180f + (k + 1) * 360f / Steps) * R;
                    float step = 2f * Mathf.PI * R / Steps / KidRun;
                    var pt = s.Part(ms[i], tt, tt + step, q, nx, DollStyle.Stand, flap, PropKind.None, KidRun); pt.run = true; pt.poseWalking = true;
                    tt += step;
                }
                var end = pole + Rot(toCam, i * 180f + 360f / Steps) * R;
                s.Part(ms[i], tt, tt + 1.8f, end, pole, DollStyle.LookUp);
                Go(s, ms[i], tt + 1.8f, 1f, end + toCam * 0.25f, end + toCam, KidWalk);
            }
            return s;
        }

        // ================================================================== B54.r the long table: the shared meal (work)
        public const float EatFor = 7f;

        /// <summary>B54.r: the eaters sit along both benches (<paramref name="seats"/>, faces across the table), the giver walks down the near side
        /// and back along the far side handing each a bowl (B9.b along a line), and everyone eats (EatMeal) <see cref="EatFor"/> s.
        /// <see cref="CampScene.a"/>: the first bowl handed over.</summary>
        public static CampScene TableMeal(int giver, Vector2 giverAt, IList<int> eaters, IList<Vector2> at, IList<Vector2> seats, IList<Vector2> faces, Vector2 mid, Vector2 pot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.TableMeal, t0, mid) { holdFocus = true };
            int g = s.Add(giver);
            int n = Mathf.Min(eaters.Count, seats.Count);
            var ms = new int[n];
            float sat = 0f;
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(eaters[i]);
                float w = WalkTime(at[i], seats[i], Walk);
                s.Part(ms[i], 0f, w, seats[i], faces[i], DollStyle.Stand, null, PropKind.None, Walk);
                sat = Mathf.Max(sat, w);
            }
            float gw = WalkTime(giverAt, pot, Walk);
            s.Part(g, 0f, gw, pot, mid, DollStyle.Stand, null, PropKind.None, Walk);
            float t = Mathf.Max(sat, gw) + 0.4f;
            var served = new float[n];
            var from = pot;
            s.a = -1f;
            for (int i = 0; i < n; i++)
            {
                var hand = seats[i] + Unit(seats[i] - faces[i], toCam) * 0.09f; // behind the sitter, on the bench's outer side
                float w = WalkTime(from, hand, Walk);
                s.Part(g, t, t + w, hand, seats[i], DollStyle.Stand, null, PropKind.Bowl, Walk); t += w;
                s.Part(g, t, t + 0.6f, hand, seats[i], DollStyle.Stand, Reach(70f, 15f), PropKind.Bowl);
                served[i] = t + 0.35f; if (s.a < 0f) s.a = served[i];
                t += 0.6f; from = hand;
            }
            for (int i = 0; i < n; i++)
            {
                s.Part(ms[i], WalkTime(at[i], seats[i], Walk), served[i], seats[i], faces[i], DollStyle.Sit, SitOn(PropGeo.BenchTop, 0f));
                s.Part(ms[i], served[i], t + EatFor, seats[i], faces[i], DollStyle.EatMeal, (ref DollPose p, float u, float l) => { p.offset.y += PropGeo.SeatLift(PropGeo.BenchTop); }, PropKind.Bowl);
            }
            var gs = from + toCam * 0.1f;
            s.Part(g, t, t + EatFor, gs, mid, DollStyle.EatMeal, null, PropKind.Bowl);
            t += EatFor;
            for (int i = 0; i < n; i++) Go(s, ms[i], t, 1f, seats[i] + Unit(seats[i] - faces[i], toCam) * 0.2f, seats[i] + toCam, Walk);
            return s;
        }

        // ================================================================== B54.s the long table: under it, and the nap (life)
        /// <summary>B54.s: two kids crawl under the table end to end (squash 0.6, lean 30, 0.15 u/s), one behind the other, and pop out at the far
        /// end laughing; then an old one (−1: none) lies on a bench and naps (Lie, 6 s). <see cref="CampScene.a"/>: the pop-out.</summary>
        public static CampScene TableUnder(int kidA, int kidB, Vector2 atA, Vector2 atB, Vector2 endA, Vector2 endB, int old, Vector2 oldAt, Vector2 bench, Vector2 benchAlong, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.TableUnder, t0, (endA + endB) * 0.5f) { holdFocus = true };
            int a = s.Add(kidA), b = s.Add(kidB);
            var dir = Unit(endB - endA, Vector2.right);
            var inA = endA - dir * 0.08f; var inB = inA - dir * 0.08f;
            float t = Mathf.Max(WalkTime(atA, inA, KidWalk), WalkTime(atB, inB, KidWalk));
            s.Part(a, 0f, t, inA, endB, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(b, 0f, t, inB, endB, DollStyle.Stand, null, PropKind.None, KidWalk);
            CampPoseFn crawl = (ref DollPose p, float u, float l) => { p.squash = 0.72f; p.lean += 48f; p.offset.y -= 0.018f; p.yaw += 0f; float c = Mathf.Sin(u * 9f); p.armL = new Vector3(70f + 25f * c, 0f, -12f); p.armR = new Vector3(70f - 25f * c, 0f, 12f); p.roll += 4f * c; }; // #693 retakes: on hands and knees (lean 62, low), the arms stepping
            var outA = endB + dir * 0.08f; var outB = endB + dir * 0.02f + toCam * 0.06f;
            float ca = (outA - inA).magnitude / 0.15f, cb = (outB - inB).magnitude / 0.15f;
            s.Part(a, t, t + ca, outA, outA + dir, DollStyle.Stand, crawl, PropKind.None, 0.15f).poseWalking = true;
            s.Part(b, t + 0.4f, t + 0.4f + cb, outB, outB + dir, DollStyle.Stand, crawl, PropKind.None, 0.15f).poseWalking = true;
            s.a = t + ca;
            float e = t + 0.4f + cb;
            s.Part(a, t + ca, e + 0.2f, outA, outA + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += 0.02f * Bump(u, 0f, 0.35f); });
            s.Part(a, e + 0.2f, e + 2f, outA, outB, DollStyle.Laugh);
            s.Part(b, e, e + 2f, outB, outA, DollStyle.Laugh);
            t = e + 2f;
            Go(s, a, t, 1f, outA + toCam * 0.3f, outA + toCam, KidRun, true);
            Go(s, b, t, 1f, outB + toCam * 0.3f + dir * 0.1f, outB + toCam, KidRun, true);
            if (old >= 0)
            {
                int o = s.Add(old);
                var at = bench;
                float w = WalkTime(oldAt, at + toCam * 0.06f, 0.16f);
                s.Part(o, Mathf.Max(0f, t - w - 0.5f), t - 0.5f, at + toCam * 0.06f, at, DollStyle.Stand, null, PropKind.None, 0.16f);
                s.Part(o, t - 0.5f, t + 0.3f, at, at + Unit(benchAlong, Vector2.right), DollStyle.Stand, (ref DollPose p, float u, float l) => { Seated(ref p); p.offset.y += PropGeo.SeatLift(PropGeo.BenchTop) + 0.03f; }, PropKind.None, 0.1f).poseWalking = true;
                s.Part(o, t + 0.3f, t + 6.3f, at, at + Unit(benchAlong, Vector2.right), DollStyle.Lie, (ref DollPose p, float u, float l) => { p.offset.y += PropGeo.SeatLift(PropGeo.BenchTop); });
                s.Part(o, t + 6.3f, t + 7f, at, at + toCam, DollStyle.Stretch);
                Go(s, o, t + 7f, 1f, at + toCam * 0.25f, at + toCam, 0.16f);
            }
            return s;
        }

        // ================================================================== B54.t the offering basket: filled at a feast (work)
        /// <summary>B54.t: the one sent carries the best basket up, lifts the cloth (arm x 80: the food shows), tips the food in (TipBasket), lays the
        /// cloth back (the heap shows under it, fuller) and bows to the glass. <see cref="CampScene.a"/>: the tip.</summary>
        public static CampScene OfferingFill(int who, Vector2 at, Vector2 offering, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.OfferingFill, t0, offering) { holdFocus = true };
            int m = s.Add(who);
            var st = offering + acr * 0.15f + toCam * 0.04f; // beside it, so the basket and the cloth show
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, offering, DollStyle.Stand, null, PropKind.Basket, Walk);
            s.Part(m, t, t + 0.8f, st, offering, DollStyle.Stand, Reach(80f, 20f), PropKind.Basket);
            Look(s, t + 0.45f, 2); t += 0.8f;
            s.Part(m, t, t + 1.4f, st, offering, DollStyle.TipBasket, null, PropKind.Basket);
            s.a = t + 0.8f; Look(s, t + 0.8f, 3); s.Fx(t + 0.8f, CampFxKind.Grass, G(offering, 0.12f), 0.3f, 3);
            t += 1.4f;
            s.Part(m, t, t + 0.8f, st, offering, DollStyle.Stand, Reach(80f, 20f));
            Look(s, t + 0.45f, 1); PropDo(s, t + 0.45f, PropFx.Offered); Jolt(s, t + 0.45f); t += 0.8f;
            var bow = offering + toCam * 0.18f;
            s.Part(m, t, t + 0.9f, bow, bow + toCam, DollStyle.Stand, null, PropKind.None, Walk); t += 0.9f;
            s.Part(m, t, t + 1.4f, bow, bow + toCam, DollStyle.Bow); t += 1.4f;
            Go(s, m, t, 1f, bow + toCam * 0.2f + acr * 0.15f, bow + toCam, Walk);
            return s;
        }

        // ================================================================== B54.u the offering basket: waiting for the Keeper (life)
        public const float WaitFor = 9f;

        /// <summary>B54.u: 2-3 kids sit in a row beside the basket facing the glass, chins in their hands (arms x 120 to the face), looking up at
        /// it; halfway one gets up, lifts the cloth to check (still full) and shrugs, and sits again. <see cref="CampScene.a"/>: the check.</summary>
        public static CampScene OfferingWait(IList<int> kids, IList<Vector2> at, Vector2 offering, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.OfferingWait, t0, offering + toCam * 0.1f) { holdFocus = true };
            int n = Mathf.Min(3, kids.Count);
            var ms = new int[n]; var seats = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(kids[i]);
                seats[i] = offering + toCam * 0.12f + acr * (0.12f + 0.08f * i);
                float w = WalkTime(at[i], seats[i], KidWalk);
                s.Part(ms[i], 0f, w, seats[i], seats[i] + toCam, DollStyle.Stand, null, PropKind.None, KidWalk);
                t = Mathf.Max(t, w);
            }
            CampPoseFn chin = (ref DollPose p, float u, float l) => { Seated(ref p); p.lean -= 12f; p.armL = new Vector3(120f, 0f, 10f); p.armR = new Vector3(120f, 0f, -10f); };
            float half = t + WaitFor * 0.5f;
            for (int i = 0; i < n; i++) s.Part(ms[i], WalkTime(at[i], seats[i], KidWalk), i == 0 ? half : t + WaitFor, seats[i], seats[i] + toCam, DollStyle.Sit, chin);
            // the check
            var peek = offering + acr * 0.1f + toCam * 0.06f;
            s.Part(ms[0], half, half + 0.6f, peek, offering, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(ms[0], half + 0.6f, half + 1.4f, peek, offering, DollStyle.Stand, Reach(80f, 20f));
            Look(s, half + 0.9f, 2); Look(s, half + 1.3f, 1);
            s.a = half + 0.9f;
            s.Part(ms[0], half + 1.4f, half + 2.2f, peek, peek + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.armL = Lerp(p.armL, new Vector3(30f, 0f, -60f), k); p.armR = Lerp(p.armR, new Vector3(30f, 0f, 60f), k); p.offset.y += 0.01f * k; }); // the shrug
            s.Part(ms[0], half + 2.2f, half + 2.8f, seats[0], seats[0] + toCam, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(ms[0], half + 2.8f, t + WaitFor, seats[0], seats[0] + toCam, DollStyle.Sit, chin);
            t += WaitFor;
            for (int i = 0; i < n; i++) Go(s, ms[i], t, 1f, seats[i] + toCam * 0.3f, seats[i] + toCam, KidWalk);
            return s;
        }

        // ================================================================== B54.x the fish crate: to the rack (work)
        /// <summary>B54.x: the rack keeper lifts the fish one by one into the fish basket (arm x 40 → 100, each fish leaves the crate:
        /// <see cref="PropFx.Look"/> 1..3), carries it to the rack <paramref name="rack"/> and hangs them (HangFish, as the rack's own beat [est:
        /// the Hang move]). <see cref="CampScene.a"/>: the first fish out.</summary>
        public static CampScene CrateFish(int who, Vector2 at, Vector2 crate, Vector2 rack, Vector2 rackFace, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.CrateFish, t0, crate) { followReach = 2f };
            int m = s.Add(who);
            var st = crate + toCam * 0.14f + acr * 0.04f;
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, crate, DollStyle.Stand, null, PropKind.FishBasket, Walk);
            s.a = t + 0.5f;
            for (int i = 0; i < 3; i++)
            {
                s.Part(m, t, t + 0.9f, st, crate, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    p.lean += 25f * Bump(u, 0f, 0.6f); p.armR = Lerp(new Vector3(40f, 0f, 6f), new Vector3(100f, 0f, 6f), In(u, 0.3f, 0.4f)); p.armL = new Vector3(50f, 0f, -10f);
                }, PropKind.FishBasket);
                Look(s, t + 0.5f, i + 1);
                t += 0.9f;
            }
            float w = WalkTime(st, rack, Walk);
            s.Part(m, t, t + w, rack, rackFace, DollStyle.Stand, null, PropKind.FishBasket, Walk); t += w;
            s.Part(m, t, t + 2.4f, rack, rackFace, DollStyle.Hang, null, PropKind.Fish); t += 2.4f;
            Go(s, m, t, 1f, rack + toCam * 0.2f, rack + toCam, Walk);
            return s;
        }

        // ================================================================== B54.y the fish crate: wave across the pond (life)
        /// <summary>B54.y: 1-2 kids sit on the crate facing the pond and the family's shore <paramref name="shore"/>, then stand and wave big (both
        /// arms), twice. <see cref="CampScene.a"/>: the first wave.</summary>
        public static CampScene CrateWave(IList<int> kids, IList<Vector2> at, Vector2 crate, Vector2 shore, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.CrateWave, t0, crate) { holdFocus = true };
            int n = Mathf.Min(2, kids.Count);
            var ms = new int[n]; var seats = new Vector2[n];
            var look = Unit(shore - crate, -toCam);
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(kids[i]);
                seats[i] = crate + acr * (n == 1 ? 0f : (i == 0 ? -0.05f : 0.05f));
                var app = seats[i] - look * 0.08f;
                float w = WalkTime(at[i], app, KidWalk);
                s.Part(ms[i], 0f, w, app, crate, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(ms[i], w, w + 0.4f, seats[i], seats[i] + look, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += PropGeo.SeatLift(PropGeo.CrateTop) * In(u, 0f, 0.4f); }, PropKind.None, 0.2f).poseWalking = true;
                t = Mathf.Max(t, w + 0.4f);
            }
            for (int i = 0; i < n; i++) s.Part(ms[i], s.parts[i][1].to, t + 3f, seats[i], seats[i] + look, DollStyle.Sit, SitOn(PropGeo.CrateTop, 4f, i * 0.4f));
            t += 3f;
            s.a = t + 0.4f;
            CampPoseFn standOn = (ref DollPose p, float u, float l) => { p.offset.y += PropGeo.CrateTop; };
            for (int i = 0; i < n; i++)
            {
                s.Part(ms[i], t, t + 0.4f, seats[i], seats[i] + look, DollStyle.Stand, standOn);
                s.Part(ms[i], t + 0.4f, t + 2.6f, seats[i], seats[i] + look, DollStyle.Wave, (ref DollPose p, float u, float l) =>
                {
                    p.offset.y += PropGeo.CrateTop + 0.02f * Mathf.Abs(Mathf.Sin(u * 3f * Mathf.PI)); float f = Mathf.Sin(u * 3f * Mathf.PI * 2f) * 40f; p.armL = new Vector3(165f, 0f, -25f - f); p.armR = new Vector3(165f, 0f, 25f + f); // #693 retakes: big waves, little hops
                });
                s.Part(ms[i], t + 2.6f, t + 3.4f, seats[i], seats[i] + look, DollStyle.Stand, standOn);
                s.Part(ms[i], t + 3.4f, t + 5f, seats[i], seats[i] + look, DollStyle.Wave, (ref DollPose p, float u, float l) =>
                {
                    p.offset.y += PropGeo.CrateTop; float f = Mathf.Sin(u * 3f * Mathf.PI * 2f) * 40f; p.armL = new Vector3(165f, 0f, -25f - f); p.armR = new Vector3(165f, 0f, 25f + f); p.offset.y += 0.04f * Bump(u, 0.6f, 0.3f);
                });
                var down = seats[i] - look * 0.1f + acr * (i == 0 ? -0.05f : 0.05f);
                s.Part(ms[i], t + 5f, t + 5.4f, down, down - look, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += PropGeo.CrateTop * (1f - In(u, 0f, 0.35f)); }, PropKind.None, 0.3f).poseWalking = true;
                Go(s, ms[i], t + 5.4f, 1f, down + toCam * 0.3f, down + toCam, KidWalk);
            }
            return s;
        }

        // ================================================================== B54.z the name peg: a fresh cord (work)
        /// <summary>B54.z: the parent crouches at the peg, unties the old tag (fiddle 0.8 s), ties it on again (fiddle 1 s → hit: the tag jolts) and
        /// nods. <see cref="CampScene.a"/>: tied.</summary>
        public static CampScene PegRetie(int parent, Vector2 at, Vector2 peg, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.PegRetie, t0, peg) { holdFocus = true };
            int m = s.Add(parent);
            var st = peg + toCam * 0.04f + acr * 0.1f; // #693 retakes: side-on, so the tag shows
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, peg, DollStyle.Stand, null, PropKind.None, Walk);
            CampPoseFn tie = (ref DollPose p, float u, float l) => { CrouchPose(ref p, u, l); float f = Mathf.Sin(u * 4f * Mathf.PI * 2f) * 12f; p.armL = new Vector3(110f + f, 0f, 12f); p.armR = new Vector3(110f - f, 0f, -12f); };
            s.Part(m, t, t + 0.8f, st, peg, DollStyle.Stand, tie); Jolt(s, t + 0.6f); t += 0.8f;
            s.Part(m, t, t + 1.0f, st, peg, DollStyle.Stand, tie);
            s.a = t + 1.0f; Jolt(s, t + 1.0f); Look(s, t + 1.0f, 1); t += 1.0f; // the fresh cord (red) shows
            s.Part(m, t, t + 0.7f, st, peg, DollStyle.Stand, (ref DollPose p, float u, float l) => { CrouchPose(ref p, 1f, l); NodPose(ref p, u, l); });
            t += 0.7f;
            Go(s, m, t, 1f, st + toCam * 0.25f, st + toCam, Walk);
            return s;
        }

        // ================================================================== B54.zz the name peg: the morning tap (life)
        /// <summary>B54.zz: the named child runs past their own peg and taps the top of it without stopping (arm x 130 [est: x 70 reaches only
        /// the post; the tag hangs above a kid's head], a 0.1 s snap → hit: the tag swings), and runs on to <paramref name="onTo"/>.
        /// <see cref="CampScene.a"/>: the tap.</summary>
        public static CampScene PegTap(int kid, Vector2 at, Vector2 peg, Vector2 onTo, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.PegTap, t0, peg) { holdFocus = true };
            int m = s.Add(kid);
            var by = peg - toCam * 0.05f; // behind the peg as the camera sees it, so the tag stays in view
            var run = Unit(onTo - at, Across(toCam));
            var pass = by - run * 0.02f;
            float w = WalkTime(at, pass, KidRun);
            var a = s.Part(m, 0f, w, pass, pass + run, DollStyle.Stand, null, PropKind.None, KidRun); a.run = true;
            s.a = w + 0.12f;
            var b = s.Part(m, w, w + 0.35f, by + run * 0.08f, by + run, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float k = Bump(u, 0f, 0.3f); p.armR = Lerp(p.armR, new Vector3(150f, 0f, -10f), k); p.yaw -= 25f * k;
            }, PropKind.None, KidRun); b.run = true; b.poseWalking = true;
            PropDo(s, w + 0.12f, PropFx.Swing); // #693 retakes: the tapped peg swings (15 deg, 1.2 s)
            float w2 = WalkTime(by, onTo, KidRun);
            var c = s.Part(m, w + 0.35f, w + 0.35f + w2, onTo, onTo + run, DollStyle.Stand, null, PropKind.None, KidRun); c.run = true;
            return s;
        }

        // ================================================================== B54.h the big pot: the together supper (work)
        /// <summary>B54.h: on a "one big pot" night the cook stirs the big pot (Stir with the stick, 3 s, the pot's steam swells), then the bowl
        /// round's giver dips there between the hand-overs (CampLife moves the B30 pot's cooking spot onto the big pot for the night, so the camp's
        /// own bowl round plays at it). This scene is the cook's opening stir. <see cref="CampScene.a"/>: the first stir.</summary>
        public static CampScene BigPotStir(int cook, Vector2 at, Vector2 pot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.BigPot, t0, pot) { holdFocus = true };
            int m = s.Add(cook);
            var st = pot + (acr * 0.8f + toCam * 0.6f).normalized * 0.17f;
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, pot, DollStyle.Stand, null, PropKind.Stick, Walk);
            s.a = t + 0.3f;
            s.Part(m, t, t + 4f, st, pot, DollStyle.Stir, null, PropKind.Stick);
            s.Fx(t + 0.5f, CampFxKind.Steam, G(pot, PropGeo.PotRim), 0.5f, 2);
            s.Fx(t + 2.0f, CampFxKind.Steam, G(pot, PropGeo.PotRim), 0.5f, 2);
            s.Fx(t + 3.4f, CampFxKind.Steam, G(pot, PropGeo.PotRim), 0.5f, 2);
            t += 4f;
            s.Part(m, t, t + 0.8f, st, pot, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean += 20f * Bump(u, 0f, l); }); // a sniff
            return s;
        }

        // ================================================================== 2026-11-16 "Bring a pen back": the goat pen (B54.v, B54.w, B58.d)
        /// <summary>The pen's sizes as drawn: half its width, the top rail's height (box units).</summary>
        public const float PenHalf = ConsequenceMeshes.PenHalf * PropGeo.S, PenTop = 0.085f * PropGeo.S;

        /// <summary>B54.v: a grown-up kneels outside the gnawed corner <paramref name="gap"/> (<paramref name="outward"/>: away from the pen), binds a
        /// new rail on (both hands working, 1.5 s), then three hammer taps (x 70 to 115, each a puff of chips) and the corner is whole
        /// (<see cref="PropFx.Look"/> 1). <see cref="CampScene.a"/>: the first tap.</summary>
        public static CampScene PenMend(int who, Vector2 at, Vector2 gap, Vector2 outward, Vector2 toCam, float t0)
        {
            outward = Unit(outward, Vector2.right); toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.PenMend, t0, gap) { holdFocus = true };
            int m = s.Add(who);
            var kneel = gap + outward * 0.1f + toCam * 0.03f;
            float t = WalkTime(at, kneel, Walk);
            s.Part(m, 0f, t, kneel, gap, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 1.5f, kneel, gap, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, u, l); FiddleHandsPose(ref p, u, l); });
            t += 1.5f;
            s.a = t + 0.3f;
            s.Part(m, t, t + 1.8f, kneel, gap, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                KneelPose(ref p, 1f, l);
                float ph = Mathf.Repeat(u / 0.6f, 1f);
                p.armR = new Vector3(ph < 0.5f ? Mathf.Lerp(70f, 115f, Smooth(ph * 2f)) : Mathf.Lerp(115f, 70f, Smooth((ph - 0.5f) * 2f)), 0f, 8f);
                p.armL = new Vector3(60f, 0f, -14f);
            }, PropKind.Hammer);
            for (int i = 0; i < 3; i++) { s.Fx(t + 0.3f + i * 0.6f, CampFxKind.Chips, G(gap, PenTop * 0.7f), 0.5f, 3); Jolt(s, t + 0.3f + i * 0.6f); }
            Look(s, t + 1.6f, 1);
            t += 1.8f;
            s.Part(m, t, t + 0.7f, kneel, gap, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, 1f, l); NodPose(ref p, u, l); }, PropKind.Hammer);
            t += 0.7f;
            Go(s, m, t, 1f, kneel + toCam * 0.25f, kneel + toCam, Walk);
            return s;
        }

        /// <summary>B54.w: two kids climb onto the pen's near rail <paramref name="rail"/> (the side toward the camera) and sit on it swinging their
        /// legs (roll 6 deg); with <paramref name="gapOpen"/>, a third (-1: none) crawls in through the gnawed gap (on hands and knees, 0.15 u/s),
        /// stands up inside and waves both arms, then crawls back out. <see cref="CampScene.a"/>: sat on the rail.</summary>
        public static CampScene PenRails(int kidA, int kidB, Vector2 atA, Vector2 atB, int crawler, Vector2 crawlAt, Vector2 rail, Vector2 along, Vector2 gap, Vector2 outward, bool gapOpen, Vector2 toCam, float sitFor, float t0)
        {
            toCam = Unit(toCam, Vector2.down); along = Unit(along, Vector2.right); outward = Unit(outward, Vector2.right);
            var s = new CampScene(CampUse.PenRails, t0, (rail + gap) * 0.5f) { holdFocus = true };
            int a = s.Add(kidA), b = s.Add(kidB);
            var seatA = rail - along * 0.06f; var seatB = rail + along * 0.06f;
            float wa = WalkTime(atA, seatA + toCam * 0.06f, KidWalk), wb = WalkTime(atB, seatB + toCam * 0.06f, KidWalk);
            s.Part(a, 0f, wa, seatA + toCam * 0.06f, seatA, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(b, 0f, wb, seatB + toCam * 0.06f, seatB, DollStyle.Stand, null, PropKind.None, KidWalk);
            float t = Mathf.Max(wa, wb);
            CampPoseFn climb = (ref DollPose p, float u, float l) => { float k = In(u, 0f, l); p.offset.y += PropGeo.SeatLift(PenTop) * k; p.armL = new Vector3(110f * (1f - k), 0f, -12f); p.armR = new Vector3(110f * (1f - k), 0f, 12f); };
            if (wa < t) s.Part(a, wa, t, seatA + toCam * 0.06f, seatA, DollStyle.Stand);
            if (wb < t) s.Part(b, wb, t, seatB + toCam * 0.06f, seatB, DollStyle.Stand);
            s.Part(a, t, t + 0.6f, seatA, seatA + toCam, DollStyle.Stand, climb, PropKind.None, 0.2f).poseWalking = true;
            s.Part(b, t, t + 0.6f, seatB, seatB + toCam, DollStyle.Stand, climb, PropKind.None, 0.2f).poseWalking = true;
            t += 0.6f; s.a = t;
            float end = t + sitFor;
            if (crawler >= 0 && gapOpen)
            {
                int c = s.Add(crawler);
                var outside = gap + outward * 0.12f; var inside = gap - outward * 0.12f;
                float w = WalkTime(crawlAt, outside, KidWalk);
                s.Part(c, 0f, w, outside, gap, DollStyle.Stand, null, PropKind.None, KidWalk);
                float c0 = Mathf.Max(w, t + 0.5f);
                if (c0 > w) s.Part(c, w, c0, outside, gap, DollStyle.Stand);
                CampPoseFn crawl = (ref DollPose p, float u, float l) => { p.squash = 0.72f; p.lean += 48f; p.offset.y -= 0.018f; float k = Mathf.Sin(u * 9f); p.armL = new Vector3(70f + 25f * k, 0f, -12f); p.armR = new Vector3(70f - 25f * k, 0f, 12f); };
                float cl = (inside - outside).magnitude / 0.15f;
                s.Part(c, c0, c0 + cl, inside, inside - outward, DollStyle.Stand, crawl, PropKind.None, 0.15f).poseWalking = true;
                float tt = c0 + cl;
                s.Part(c, tt, tt + 2f, inside, inside + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float f = Mathf.Sin(u * 3f * Mathf.PI * 2f) * 35f; p.armL = new Vector3(165f, 0f, -25f - f); p.armR = new Vector3(165f, 0f, 25f + f); p.offset.y += 0.02f * Mathf.Abs(Mathf.Sin(u * 3f * Mathf.PI));
                });
                tt += 2f;
                var outAgain = gap + outward * 0.14f + toCam * 0.04f;
                s.Part(c, tt, tt + cl, outAgain, outAgain + outward, DollStyle.Stand, crawl, PropKind.None, 0.15f).poseWalking = true;
                tt += cl;
                end = Mathf.Max(end, tt + 0.5f);
                Go(s, c, tt, 1f, outAgain + toCam * 0.3f, outAgain + toCam, KidWalk);
            }
            s.Part(a, t, end, seatA, seatA + toCam, DollStyle.Sit, SitOn(PenTop, 6f, 0f));
            s.Part(b, t, end, seatB, seatB + toCam, DollStyle.Sit, SitOn(PenTop, 6f, 0.5f));
            foreach (int m in new[] { a, b })
            {
                var seat = m == a ? seatA : seatB;
                s.Part(m, end, end + 0.5f, seat + toCam * 0.06f, seat + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += PropGeo.SeatLift(PenTop) * (1f - In(u, 0f, l)); }, PropKind.None, 0.2f).poseWalking = true;
                Go(s, m, end + 0.5f, 1f, seat + toCam * 0.3f + along * (m == a ? -0.1f : 0.1f), seat + toCam, KidWalk);
            }
            return s;
        }

        /// <summary>B58.d at the pen: a kid at the near rail <paramref name="rail"/> reaches over (arm x 100, lean 20) and pats the goat's head twice;
        /// the goat butts gently (<see cref="CampScene.c"/>: CampLife steps it in 0.03 u) and the kid stumbles back laughing.
        /// <see cref="CampScene.a"/>: the first pat. The goat stands inside at <see cref="CampScene.p0"/> for the whole scene.</summary>
        public static CampScene PenPet(int kid, Vector2 at, Vector2 rail, Vector2 inward, Vector2 toCam, float t0)
        {
            inward = Unit(inward, Vector2.up); toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.PenPet, t0, rail) { holdFocus = true };
            int m = s.Add(kid);
            var side = new Vector2(-inward.y, inward.x); if (Vector2.Dot(side, at - rail) < 0f) side = -side;
            var st = rail - inward * 0.06f + side * 0.06f; // a little to the side, so the kid doesn't hide the goat
            float w = WalkTime(at, st, KidWalk);
            s.p0 = rail + inward * 0.085f - side * 0.02f; // the goat comes to the rail
            s.Part(m, 0f, w, st, rail, DollStyle.Stand, null, PropKind.None, KidWalk);
            float t = Mathf.Max(w, 2f); // the goat needs a moment to come over
            if (t > w) s.Part(m, w, t, st, rail, DollStyle.Stand);
            s.a = t + 0.5f;
            s.Part(m, t, t + 1.6f, st, rail, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float k = In(u, 0f, 0.3f); p.lean += 20f * k; float pat = Mathf.Max(Bump(u, 0.4f, 0.3f), Bump(u, 0.9f, 0.3f));
                p.armR = Lerp(p.armR, new Vector3(100f - 25f * pat, 0f, 6f), k); p.offset.y += 0.01f * k;
            });
            t += 1.6f;
            s.c = t + 0.1f;
            s.Part(m, t, t + 0.5f, st - inward * 0.05f, rail, DollStyle.HopBack, null, PropKind.None, 0.3f);
            s.Part(m, t + 0.5f, t + 2.3f, st - inward * 0.05f, rail, DollStyle.Laugh);
            t += 2.3f;
            Go(s, m, t, 1f, st - inward * 0.3f + toCam * 0.1f, st + toCam, KidWalk);
            return s;
        }
    }
}
