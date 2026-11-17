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

        /// <summary>B54.a: the gatherer lifts the right-hand basket off the pile (arm x 80 → hit: one fewer, the pile jolts) and carries it in both
        /// hands (CampLife draws the very same basket, from <see cref="CampScene.a"/> to <see cref="CampScene.b"/>) to the picking spot, picks
        /// (PickBerry, <see cref="PickFor"/> s [est: cut short for the box]), brings it back and sets it down: the pile is whole again.</summary>
        public static CampScene BasketTake(int who, Vector2 at, Vector2 stack, Vector2 bush, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.BasketTake, t0, stack) { followReach = 1.2f };
            int m = s.Add(who);
            var st = stack + toCam * 0.15f;
            CampPoseFn carry = (ref DollPose p, float u, float l) => { p.armL = new Vector3(55f, 0f, -16f); p.armR = new Vector3(55f, 0f, 16f); };
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, stack, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.8f, st, stack, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.3f); p.lean += 15f * Bump(u, 0f, 0.6f); p.armL = Lerp(p.armL, new Vector3(70f, 0f, -16f), k); p.armR = Lerp(p.armR, new Vector3(70f, 0f, 16f), k); });
            s.a = t + 0.45f; Look(s, t + 0.45f, 1); Jolt(s, t + 0.45f); t += 0.8f;
            float w = WalkTime(st, bush, Walk);
            s.Part(m, t, t + w, bush, bush + toCam, DollStyle.Stand, carry, PropKind.None, Walk).poseWalking = true; t += w;
            s.Part(m, t, t + PickFor, bush, bush - toCam, DollStyle.PickBerry, (ref DollPose p, float u, float l) => { p.armL = new Vector3(55f, 0f, -16f); }); t += PickFor;
            s.Part(m, t, t + w, st, stack, DollStyle.Stand, carry, PropKind.None, Walk).poseWalking = true; t += w;
            s.Part(m, t, t + 0.7f, st, stack, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 25f * k; p.squash = Mathf.Lerp(1f, 0.9f, k); p.armL = new Vector3(55f + 15f * k, 0f, -16f); p.armR = new Vector3(55f + 15f * k, 0f, 16f); });
            s.b = t + 0.4f; Look(s, t + 0.4f, 0); Jolt(s, t + 0.4f); s.Fx(t + 0.4f, CampFxKind.Dust, G(stack, 0.01f), 0.3f, 2);
            t += 0.7f;
            Go(s, m, t, 1.2f, st + toCam * 0.2f, st + toCam, Walk);
            return s;
        }

        // ================================================================== B54.c the woodpile: stack a log, fetch one (work)
        /// <summary>B54.c: <paramref name="fetch"/> false: carries a log in both arms (CampLife draws the pile's own log piece) to the pile's front,
        /// lifts it over the front logs (arms up to x 150) and lays it in a free slot on top (<see cref="PropFx.Stack"/> at <see cref="CampScene.a"/>:
        /// it is part of the pile from then on). True (a wet day): takes one off (arm x 60, the jolt) and carries it to the fire.</summary>
        public static CampScene WoodStack(int who, Vector2 at, Vector2 pile, Vector2 toCam, bool fetch, Vector2 fire, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.WoodStack, t0, pile) { followReach = fetch ? 1.5f : 0.6f, kind = fetch ? 1 : 0 };
            int m = s.Add(who);
            var st = pile + toCam * 0.33f; // the logs reach 0.2 out from the middle: the crosswise log in the hands stays clear of them
            CampPoseFn carry = (ref DollPose p, float u, float l) => { p.armL = new Vector3(70f, 0f, -12f); p.armR = new Vector3(70f, 0f, 12f); };
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, pile, DollStyle.Stand, fetch ? null : carry, PropKind.None, Walk).poseWalking = !fetch;
            if (!fetch)
            {
                // the lay: arms from x 70 up to x 150 (the log over the front logs) and forward, onto the top (1.4 s)
                s.Part(m, t, t + 1.4f, st, pile, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float up = In(u, 0f, 0.6f) * (1f - In(u, 1.0f, 0.3f));
                    p.armL = new Vector3(70f + 80f * up, 0f, -12f); p.armR = new Vector3(70f + 80f * up, 0f, 12f); p.offset.y += 0.015f * up; p.lean += 8f * In(u, 0.6f, 0.4f);
                });
                s.c = t; s.a = t + 1.0f; PropDo(s, t + 1.0f, PropFx.Stack); Jolt(s, t + 1.0f);
                t += 1.4f;
                Go(s, m, t, 1.2f, st + toCam * 0.25f, st + toCam, Walk);
                return s;
            }
            s.Part(m, t, t + 1.0f, st, pile, DollStyle.Stand, Reach(60f, 15f));
            s.a = t + 0.6f; PropDo(s, t + 0.6f, PropFx.Unstack); Jolt(s, t + 0.6f);
            t += 1.0f;
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
        /// <summary>#693 notes: how far from the runestone's middle, out along its face, the cutter kneels and the tracing kid stands (its face is
        /// <see cref="ConsequenceMeshes.StoneHalfZ"/> x 1.5 = 0.053 out; a doll's half-width 0.03 and arm 0.075 stay outside it).</summary>
        public const float NameKneel = 0.17f, NameStand = 0.16f;
        public const float CutFor = 6f;

        /// <summary>B54.e: the toolmaker kneels at the stone and taps with a stone in the hand (the hammer's stroke, x 70 ↔ 100 at 2 Hz) → each
        /// tap a few grey chips, <see cref="CutFor"/> s; then the new row shows (<see cref="PropFx.NameRow"/>). <see cref="CampScene.a"/>: the first tap.</summary>
        public static CampScene NameCut(int who, Vector2 at, Vector2 stone, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.NameCut, t0, stone) { holdFocus = true };
            int m = s.Add(who);
            var kneel = stone + toCam * NameKneel + acr * 0.09f; // #693 notes: off the runestone's face by its real depth, so no hand or tool goes into it
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
            var st = stone + toCam * NameStand + acr * 0.03f; // #693 notes: out of the stone (its face 0.05 out, the arm 0.075)
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
        /// <summary>B54.n: a villager lifts a sack from the heap onto the shoulder (CampLife draws the heap's own sack there from
        /// <see cref="CampScene.c"/> to <see cref="CampScene.a"/>, then at the door, where it stays), walks heavy to the <paramref name="door"/> and
        /// sets it down (a squash, a small white puff). <see cref="CampScene.a"/>: set down.</summary>
        public static CampScene FlourCarry(int who, Vector2 at, Vector2 sacks, Vector2 door, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.FlourCarry, t0, sacks) { followReach = 2f };
            int m = s.Add(who);
            var st = sacks + toCam * 0.17f;
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, sacks, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.9f, st, sacks, DollStyle.Lift, null, PropKind.None);
            s.c = t + 0.6f; t += 0.9f;
            CampPoseFn heavy = (ref DollPose p, float u, float l) => { p.lean += 8f; p.roll += 3f * Mathf.Sin(u * 7f); p.armL = new Vector3(150f, 0f, -20f); p.offset.y -= 0.004f * Mathf.Abs(Mathf.Sin(u * 7f)); };
            float w = WalkTime(st, door, 0.16f);
            var c = s.Part(m, t, t + w, door, door + Dir(st, door, toCam), DollStyle.Stand, heavy, PropKind.None, 0.16f); c.poseWalking = true;
            t += w;
            s.Part(m, t, t + 0.8f, door, door + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 30f * k; p.squash = Mathf.Lerp(1f, 0.9f, k); p.armL = new Vector3(150f * (1f - k), 0f, -20f); });
            s.a = t + 0.45f; s.p0 = door + toCam * 0.07f; s.Fx(t + 0.45f, CampFxKind.Dust, G(s.p0, 0.01f), 0.35f, 3);
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
        /// <summary>B54.s: two kids crawl in at one end of the table (between its legs, under the top: on hands and knees, 0.15 u/s), stop under the
        /// middle and giggle, then crawl out at the far end laughing; then an old one (-1: none) crouches at the end and lies down under the table,
        /// calmly, 6 s. <paramref name="endA"/>/<paramref name="endB"/>: just outside the two ends, on the table's long line.
        /// <see cref="CampScene.a"/>: both under the table.</summary>
        public static CampScene TableUnder(int kidA, int kidB, Vector2 atA, Vector2 atB, Vector2 endA, Vector2 endB, int old, Vector2 oldAt, Vector2 bench, Vector2 benchAlong, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var mid = (endA + endB) * 0.5f;
            var s = new CampScene(CampUse.TableUnder, t0, mid) { holdFocus = true };
            int a = s.Add(kidA), b = s.Add(kidB);
            var dir = Unit(endB - endA, Vector2.right);
            var inA = endA; var inB = endA - dir * 0.09f;
            float t = Mathf.Max(WalkTime(atA, inA, KidWalk), WalkTime(atB, inB, KidWalk));
            s.Part(a, 0f, t, inA, endB, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(b, 0f, t, inB, endB, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(a, t, t + 0.4f, inA, endB, DollStyle.Stand, CrawlPose);
            s.Part(b, t, t + 0.4f, inB, endB, DollStyle.Stand, CrawlPose);
            t += 0.4f;
            var midA = mid + dir * 0.05f; var midB = mid - dir * 0.05f;
            float ca = (midA - inA).magnitude / CrawlSpeed, cb = (midB - inB).magnitude / CrawlSpeed;
            s.Part(a, t, t + ca, midA, midA + dir, DollStyle.Stand, CrawlPose, PropKind.None, CrawlSpeed).poseWalking = true;
            s.Part(b, t, t + cb, midB, midB + dir, DollStyle.Stand, CrawlPose, PropKind.None, CrawlSpeed).poseWalking = true;
            float under = t + Mathf.Max(ca, cb);
            if (ca < under - t) s.Part(a, t + ca, under, midA, midA + dir, DollStyle.Stand, CrawlPose);
            if (cb < under - t) s.Part(b, t + cb, under, midB, midB + dir, DollStyle.Stand, CrawlPose);
            s.a = under;
            CampPoseFn giggle = (ref DollPose p, float u, float l) => { CrawlPose(ref p, u, l); p.offset.y += 0.004f * Mathf.Abs(Mathf.Sin(u * 14f)); p.yaw += 25f * Mathf.Sin(u * 2.5f); };
            s.Part(a, under, under + 2.2f, midA, midA - toCam, DollStyle.Stand, giggle);
            s.Part(b, under, under + 2.2f, midB, midB - toCam, DollStyle.Stand, giggle);
            t = under + 2.2f;
            var outA = endB + dir * 0.02f; var outB = endB + dir * 0.02f - toCam * 0.0f;
            float oa = (outA - midA).magnitude / CrawlSpeed, ob = (outB - midB).magnitude / CrawlSpeed;
            s.Part(a, t, t + oa, outA, outA + dir, DollStyle.Stand, CrawlPose, PropKind.None, CrawlSpeed).poseWalking = true;
            s.Part(b, t + oa, t + oa + ob, outB, outB + dir, DollStyle.Stand, CrawlPose, PropKind.None, CrawlSpeed).poseWalking = true; // one behind the other
            float outT = t + oa;
            s.Part(a, outT, outT + 0.4f, outA + dir * 0.08f, outA + toCam, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(a, outT + 0.4f, t + oa + ob + 1.4f, outA + dir * 0.08f, outA + toCam, DollStyle.Laugh);
            float endBt = t + oa + ob;
            s.Part(b, endBt, endBt + 1.4f, outB + toCam * 0.06f, outA, DollStyle.Laugh, null, PropKind.None, KidWalk);
            t = endBt + 1.4f;
            Go(s, a, t, 1f, outA + toCam * 0.3f + dir * 0.1f, outA + toCam, KidRun, true);
            Go(s, b, t, 1f, outB + toCam * 0.35f, outB + toCam, KidRun, true);
            if (old >= 0)
            {
                int o = s.Add(old);
                var endIn = endA + dir * 0.02f;
                var lie = mid; // the body lies along the table under its middle (the doll lies its length out along where it faces)
                float w = WalkTime(oldAt, endA - dir * 0.1f, 0.16f);
                float t1 = Mathf.Max(w, t - 0.5f);
                s.Part(o, 0f, w, endA - dir * 0.1f, endA, DollStyle.Stand, null, PropKind.None, 0.16f);
                if (t1 > w) s.Part(o, w, t1, endA - dir * 0.1f, endA, DollStyle.Stand);
                s.Part(o, t1, t1 + 0.8f, endA - dir * 0.1f, endA, DollStyle.Stand, CrouchPose);
                s.Part(o, t1 + 0.8f, t1 + 0.8f + 6f, lie - dir * 0.08f, lie + dir, DollStyle.Lie);
                s.Part(o, t1 + 6.8f, t1 + 7.6f, endA - dir * 0.1f, endA - dir, DollStyle.Stand, CrouchPose, PropKind.None, 0.16f);
                Go(s, o, t1 + 7.6f, 1f, endA - dir * 0.1f + toCam * 0.25f, endA + toCam, 0.16f);
            }
            return s;
        }

        /// <summary>#693 notes: on hands and knees, low enough to pass under the long table's top (0.105 u) between its legs.</summary>
        public const float CrawlSpeed = 0.15f;
        internal static void CrawlPose(ref DollPose p, float u, float l)
        {
            p.squash = 0.62f; p.lean += 58f; p.offset.y -= 0.022f;
            float k = Mathf.Sin(u * 9f); p.armL = new Vector3(75f + 22f * k, 0f, -12f); p.armR = new Vector3(75f - 22f * k, 0f, 12f);
        }

        // ================================================================== B54.t the offering basket: filled at a feast (work)
        /// <summary>B54.t: the one sent carries the best basket up, lifts the cloth (arm x 80: the food shows), tips the food in (TipBasket), lays the
        /// cloth back (the heap shows under it, fuller) and bows to the glass. <see cref="CampScene.a"/>: the tip.</summary>
        public static CampScene OfferingFill(int who, Vector2 at, Vector2 offering, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.OfferingFill, t0, offering) { holdFocus = true };
            int m = s.Add(who);
            var st = offering + acr * 0.17f + toCam * 0.04f; // beside it, so the basket and the cloth show
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
                seats[i] = offering + toCam * 0.14f + acr * (0.16f + 0.08f * i);
                float w = WalkTime(at[i], seats[i], KidWalk);
                s.Part(ms[i], 0f, w, seats[i], seats[i] + toCam, DollStyle.Stand, null, PropKind.None, KidWalk);
                t = Mathf.Max(t, w);
            }
            CampPoseFn chin = (ref DollPose p, float u, float l) => { Seated(ref p); p.lean -= 12f; p.armL = new Vector3(120f, 0f, 10f); p.armR = new Vector3(120f, 0f, -10f); };
            float half = t + WaitFor * 0.5f;
            for (int i = 0; i < n; i++) s.Part(ms[i], WalkTime(at[i], seats[i], KidWalk), i == 0 ? half : t + WaitFor, seats[i], seats[i] + toCam, DollStyle.Sit, chin);
            // the check
            var peek = offering + acr * 0.17f + toCam * 0.06f; // #693 notes: outside the bigger basket
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

        // ================================================================== B54.y the fish basket: peek in and wave a fish (life)
        /// <summary>B54.y (#693 notes: the crate is a woven fish basket now): 1-2 kids come up to the basket and lean over to peek in (lean 35, 1 s);
        /// one lifts a fish out (CampLife draws it in the hand from <see cref="CampScene.c"/> to <see cref="CampScene.d"/>) and waves it high toward
        /// the pond and the family's shore, the other waves both arms; then the fish goes back in. <see cref="CampScene.a"/>: the first wave.</summary>
        public static CampScene CrateWave(IList<int> kids, IList<Vector2> at, Vector2 crate, Vector2 shore, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.CrateWave, t0, crate) { holdFocus = true };
            int n = Mathf.Min(2, kids.Count);
            var ms = new int[n]; var spots = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(kids[i]);
                spots[i] = crate + (toCam * 0.13f + acr * (n == 1 ? 0.04f : (i == 0 ? -0.08f : 0.08f)));
                float w = WalkTime(at[i], spots[i], KidWalk);
                s.Part(ms[i], 0f, w, spots[i], crate, DollStyle.Stand, null, PropKind.None, KidWalk);
                t = Mathf.Max(t, w);
            }
            for (int i = 0; i < n; i++) { float w = WalkTime(at[i], spots[i], KidWalk); if (w < t) s.Part(ms[i], w, t, spots[i], crate, DollStyle.Stand); }
            for (int i = 0; i < n; i++) s.Part(ms[i], t, t + 1.2f, spots[i], crate, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 35f * k; p.offset.y += 0.01f * k; });
            t += 1.2f;
            var look = Unit(shore - crate, -toCam);
            // kid 0 reaches in and lifts a fish (one fewer in the basket), turns to the pond and waves it high
            s.Part(ms[0], t, t + 0.6f, spots[0], crate, DollStyle.Stand, Reach(50f, 30f));
            s.c = t + 0.4f; Look(s, t + 0.4f, 1); t += 0.6f;
            s.a = t + 0.3f;
            CampPoseFn waveFish = (ref DollPose p, float u, float l) => { float f = Mathf.Sin(u * 3f * Mathf.PI * 2f) * 25f; p.armR = new Vector3(165f, 0f, 15f + f); p.offset.y += 0.02f * Mathf.Abs(Mathf.Sin(u * 3f * Mathf.PI)); };
            s.Part(ms[0], t, t + 2.6f, spots[0], spots[0] + look, DollStyle.Stand, waveFish);
            if (n > 1) s.Part(ms[1], t, t + 2.6f, spots[1], spots[1] + look, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float f = Mathf.Sin(u * 3f * Mathf.PI * 2f) * 40f; p.armL = new Vector3(165f, 0f, -25f - f); p.armR = new Vector3(165f, 0f, 25f + f); p.offset.y += 0.03f * Mathf.Abs(Mathf.Sin(u * 3f * Mathf.PI));
            });
            t += 2.6f;
            s.Part(ms[0], t, t + 0.7f, spots[0], crate, DollStyle.Stand, Reach(55f, 30f));
            s.d = t + 0.4f; Look(s, t + 0.4f, 0); t += 0.7f;
            if (n > 1) s.Part(ms[1], t - 0.7f, t, spots[1], crate, DollStyle.Laugh);
            for (int i = 0; i < n; i++) Go(s, ms[i], t, 1f, spots[i] + toCam * 0.3f, spots[i] + toCam, KidWalk);
            return s;
        }

        // ================================================================== B54.z the name post: the name re-carved (work)
        /// <summary>B54.z (#693 notes: the peg is a carved name post now): a grown-up stands at the post's face and re-cuts the carved mark with a
        /// knife: short downward scrapes (arm x 95 to 80 at 3 Hz, 2.5 s), a few chips each third of a second; the mark shows freshly cut
        /// (<see cref="PropFx.Recut"/>); a step back and a nod. <see cref="CampScene.a"/>: the first scrape.</summary>
        public static CampScene PostCarve(int parent, Vector2 at, Vector2 peg, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.PostCarve, t0, peg) { holdFocus = true };
            int m = s.Add(parent);
            var st = peg + toCam * 0.06f + acr * 0.1f; // side on: the mark and the knife show
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, peg, DollStyle.Stand, null, PropKind.None, Walk);
            s.a = t + 0.3f;
            s.Part(m, t, t + 2.5f, st, peg, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float ph = Mathf.Repeat(u * 3f, 1f); p.armR = new Vector3(Mathf.Lerp(98f, 80f, ph), 0f, 4f); p.armL = new Vector3(80f, 0f, -10f); p.lean += 8f;
            }, PropKind.Stick);
            for (int i = 0; i < 7; i++) s.Fx(t + 0.3f + i * 0.33f, CampFxKind.Chips, G(peg + toCam * 0.035f, PropGeo.S * ConsequenceMeshes.PostH * 0.68f), 0.3f, 2);
            PropDo(s, t + 2.3f, PropFx.Recut);
            t += 2.5f;
            s.Part(m, t, t + 0.4f, st + toCam * 0.05f, peg, DollStyle.Stand, null, PropKind.None, 0.2f);
            s.Part(m, t + 0.4f, t + 1.1f, st + toCam * 0.05f, peg, DollStyle.Stand, NodPose);
            t += 1.1f;
            Go(s, m, t, 1f, st + toCam * 0.3f, st + toCam, Walk);
            return s;
        }

        /// <summary>#693 notes: where people stand off the name post's face (its half-width 0.033, a doll's 0.03, the reach).</summary>
        public const float PostStand = 0.13f;

        // ================================================================== B54.zz the name post: the kids' heights (life)
        /// <summary>B54.zz (#693 notes): a kid stands with the back to the name post, straight and on tiptoe a little (facing the camera); a grown-up
        /// (or an older kid) beside it lays a hand flat on the kid's head and scratches a line on the post there (arm x 150, three short strokes →
        /// <see cref="PropFx.Measure"/>: the scratch shows); the kid steps away, turns, looks and cheers. <see cref="CampScene.a"/>: the scratch.</summary>
        public static CampScene PostMeasure(int kid, Vector2 at, int marker, Vector2 markerAt, Vector2 peg, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.PostMeasure, t0, peg) { holdFocus = true };
            int k = s.Add(kid);
            var back = peg + toCam * 0.065f; // the back against the post's face
            float wk = WalkTime(at, back + toCam * 0.1f, KidWalk);
            s.Part(k, 0f, wk, back + toCam * 0.1f, peg, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(k, wk, wk + 0.6f, back, back + toCam, DollStyle.Stand, null, PropKind.None, 0.2f);
            float t = wk + 0.6f;
            int g = -1; var gs = peg + toCam * 0.02f + acr * 0.13f; // beside the post, not in front of the kid
            if (marker >= 0)
            {
                g = s.Add(marker);
                float wg = WalkTime(markerAt, gs, Walk);
                s.Part(g, 0f, wg, gs, back, DollStyle.Stand, null, PropKind.None, Walk);
                t = Mathf.Max(t, wg);
                if (wg < t) s.Part(g, wg, t, gs, back, DollStyle.Stand);
            }
            if (wk + 0.6f < t) s.Part(k, wk + 0.6f, t, back, back + toCam, DollStyle.Stand);
            CampPoseFn straight = (ref DollPose p, float u, float l) => { p.lean -= 4f; p.offset.y += 0.006f; p.armL = new Vector3(0f, 0f, -6f); p.armR = new Vector3(0f, 0f, 6f); };
            s.Part(k, t, t + 2.2f, back, back + toCam, DollStyle.Stand, straight);
            s.a = t + 1.2f;
            if (g >= 0)
            {
                s.Part(g, t, t + 2.2f, gs, back, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float r = In(u, 0f, 0.4f) * (1f - In(u, 1.9f, 0.3f)); float st = u > 0.8f && u < 1.4f ? Mathf.Sin(u * 30f) * 8f : 0f;
                    p.armR = Lerp(p.armR, new Vector3(150f, 0f, -18f + st), r); p.lean += 6f * r;
                });
            }
            PropDo(s, t + 1.2f, PropFx.Measure);
            s.Fx(t + 1.2f, CampFxKind.Chips, G(peg + toCam * 0.035f, 0.148f), 0.25f, 2);
            t += 2.2f;
            var look = back + toCam * 0.16f + acr * 0.05f;
            s.Part(k, t, t + 0.6f, look, peg, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(k, t + 0.6f, t + 1.4f, look, peg, DollStyle.Look);
            s.Part(k, t + 1.4f, t + 2.6f, look, peg, DollStyle.Cheer);
            if (g >= 0) s.Part(g, t, t + 2.6f, gs, look, DollStyle.Laugh);
            t += 2.6f;
            Go(s, k, t, 1f, look + toCam * 0.25f, look + toCam, KidRun, true);
            if (g >= 0) Go(s, g, t, 1f, gs + toCam * 0.25f, gs + toCam, Walk);
            return s;
        }

        // ================================================================== B54.h the big pot: the together supper (work)
        /// <summary>#693 notes: the block of wood the stirrer stands on beside the big pot (its top, box units), and how far from the pot it stands.</summary>
        public const float StirBlock = 0.07f, StirFrom = 0.27f; // the pot and its tripod reach 0.2 out: he stands clear of them

        /// <summary>B54.h: on a "one big pot" night the cook steps up onto a block of wood beside the big pot (CampLife draws the block at
        /// <see cref="CampScene.p0"/> and the ladle from the hands down into the pot, going round) and stirs: leaning over the rim, both arms on
        /// the ladle, 4 s, the steam swells; a sniff. Then the bowl round dips there. <see cref="CampScene.a"/>: the first stir.</summary>
        public static CampScene BigPotStir(int cook, Vector2 at, Vector2 pot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.BigPot, t0, pot) { holdFocus = true };
            int m = s.Add(cook);
            var st = pot + (acr * 0.8f + toCam * 0.6f).normalized * StirFrom;
            s.p0 = st;
            var foot = st + (st - pot).normalized * 0.09f;
            float t = WalkTime(at, foot, Walk);
            s.Part(m, 0f, t, foot, pot, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.6f, st, pot, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += StirBlock * In(u, 0f, l); }, PropKind.None, 0.15f).poseWalking = true; // up onto the block
            t += 0.6f;
            s.a = t + 0.3f; s.c = t; s.d = t + 4.4f; // the ladle in the hands
            s.Part(m, t, t + 4f, st, pot, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                p.offset.y += StirBlock; p.lean += 24f; float c = Mathf.Sin(u * Mathf.PI * 2f * 0.9f) * 12f;
                p.armL = new Vector3(78f + c, 0f, -8f); p.armR = new Vector3(78f - c, 0f, 8f);
            });
            s.Fx(t + 0.5f, CampFxKind.Steam, G(pot, PropGeo.PotRim), 0.5f, 2);
            s.Fx(t + 2.0f, CampFxKind.Steam, G(pot, PropGeo.PotRim), 0.5f, 2);
            s.Fx(t + 3.4f, CampFxKind.Steam, G(pot, PropGeo.PotRim), 0.5f, 2);
            t += 4f;
            s.Part(m, t, t + 0.8f, st, pot, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += StirBlock; p.lean += 20f * Bump(u, 0f, l); }); // a sniff
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
