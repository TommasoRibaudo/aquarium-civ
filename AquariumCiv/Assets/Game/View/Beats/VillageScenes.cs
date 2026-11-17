// #695 part 1 (docs/design/animation-specs.md "#695: the village and the town", B57.a-j and B58.c/f/g/j/k): the village's and the town's
// things get used, as pure builders (the #692 kit: CampScene parts, effects). Every spot comes from the director (View/CampLife.Village.cs),
// worked out from the piece's lot and the way to the camera, so a use plays on the camera's side of its thing. What a use leaves (the
// scarecrow, a home's flower box, the hopscotch grid, a post set straight) is a CampFxKind.Village effect that CampLife.Village carries out.
// Blocked in this part (no such thing in a living-land box, where the Keeper's shaping gifts are gone): B58.a sparks (no smithy or kiln),
// B58.b the new pot (no potter), B58.d the pasture's goats (no pasture; the goat pen's pet is B58.d at the pen), B58.e the bee (no hives),
// B58.h-i the bridge (no dug channels, so no bridges).
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    /// <summary>#695: what a <see cref="CampFxKind.Village"/> effect does (CampLife.Village carries it out).</summary>
    public enum VillageFx { None,
        /// <summary>B57.b: the scarecrow's cross is planted, the straw tied on, the hat on top; a bird lands on it.</summary>
        Cross, Straw, Hat, Bird,
        /// <summary>B57.c: the last sheaf stands up in the middle of the ring.</summary>
        Sheaf,
        /// <summary>B57.d: the home's next thing pops on (a flower box, a painted door, a string of herbs).</summary>
        Extra,
        /// <summary>B57.f: the pebble leaves the hand; the tiny ring deep down.</summary>
        Pebble, PebbleRing,
        /// <summary>B58.f: the hopscotch grid is scratched on the street (it stays a view day).</summary>
        Grid,
        /// <summary>B58.j: the leaning post is pushed straight; a tap drives it in.</summary>
        PostStraight, PostTap,
        /// <summary>B59.l: a kid lands in the hay heap: it dents (0.02 u) and springs back.</summary>
        HayDent,
        /// <summary>B58.c: the crust goes from the baker's hand to a kid's, and a piece from that kid to another.</summary>
        CrustGive, CrustShare }

    /// <summary>#695: sizes of the village's things as drawn (Pieces, box units).</summary>
    public static class VillageGeo
    {
        /// <summary>The village well's stone rim (Pieces.Well): radius and top; the town's fountain basin.</summary>
        public const float WellR = 0.13f, WellTop = 0.12f, FountainR = 0.16f, FountainTop = 0.07f;
        /// <summary>A field fence's top rail and the gap between its posts (Pieces.Fence); crop rows across (BoxView.FieldMesh).</summary>
        public const float FenceTop = 0.11f, RowGap = 0.08f;
        /// <summary>The lumber mill's woodpile: its low front log's top, the lying log's top and length (Pieces.Lumbermill).</summary>
        public const float PileLow = 0.054f, LogTop = 0.058f, LogLen = 0.3f;
        /// <summary>B57.c: the ring's radius per dancer [est] and the skip's pace.</summary>
        public const float RingPer = 0.032f, RingPace = 0.3f;
        /// <summary>B57.h: how far out the kids run round the mill's foot [est].</summary>
        public const float ShadowR = 0.3f;
        /// <summary>B58.f: one square of the hopscotch grid and the hop between squares.</summary>
        public const float Square = 0.05f, GridLen = 6 * Square;
        /// <summary>B57.d: how high the mender stands on the ladder's rungs [est].</summary>
        public const float RungUp = 0.07f;
        /// <summary>B59.k/l: the hay heap by the barn [est]: its radius and its top.</summary>
        public const float HayR = 0.07f, HayTop = 0.06f;
        public static float SeatLift(float top) => PropGeo.SeatLift(top);
    }

    public static class VillageScenes
    {
        /// <summary>No one (a crowd member's key is negative, so -1 is a person: CampLife.Nearest's -1 can't tell them apart).</summary>
        public const int Nobody = int.MinValue;

        static void Do(CampScene s, float t, VillageFx what, Vector2 at, float h = 0f, int member = -1, float value = 0f) =>
            s.Fx(t, CampFxKind.Village, G(at, h), value, (int)what, CampAnchor.Ground, member);

        static CampPoseFn SitOn(float top, float roll = 4f, float phase = 0f) => (ref DollPose p, float u, float l) =>
        {
            Seated(ref p); p.offset.y += VillageGeo.SeatLift(top) + 0.03f; p.roll += roll * Mathf.Sin((u + phase) * Mathf.PI * 2f);
        };

        /// <summary>A hand to the mouth (B9.a): arm x 140, held 0.6 s, <paramref name="n"/> times from <paramref name="at"/>.</summary>
        static void Bite(ref DollPose p, float u, float at, int n)
        {
            float k = 0f;
            for (int i = 0; i < n; i++) k = Mathf.Max(k, Bump(u, at + i * 1.1f, 0.8f));
            p.armR = Lerp(p.armR, new Vector3(140f, 0f, -12f), k);
        }

        // ================================================================== B57.a kids run between the rows
        /// <summary>B57.a: 2–3 kids come in at the field's gate (<paramref name="gateOut"/> → <paramref name="gateIn"/>) and play tag along the
        /// furrows only: each kid has its own furrow (<paramref name="rows"/>: start and end of each, inside the fence), runs it ducking (squash 0.8)
        /// where the crop is tall, crosses to the next furrow at the end and runs back. The farmer (−1: none), working there, straightens up and waves
        /// them out (arm z ∓60, two flaps) at <see cref="CampScene.a"/>, and they run out by the gate. Reads: heads bobbing along the rows.</summary>
        public static CampScene FieldTag(IList<int> kids, IList<Vector2> at, IList<(Vector2 a, Vector2 b)> rows, Vector2 gateOut, Vector2 gateIn, int farmer, Vector2 farmerAt, float t0)
        {
            int n = Mathf.Min(kids.Count, rows.Count);
            var mid = Vector2.zero; foreach (var r in rows) mid += (r.a + r.b) * 0.5f; mid /= Mathf.Max(1, rows.Count);
            var s = new CampScene(CampUse.FieldTag, t0, mid) { followReach = 0.8f };
            CampPoseFn duck = (ref DollPose p, float u, float l) => { p.squash = 0.8f; p.lean += 18f; p.offset.y -= 0.01f; p.offset.y += 0.012f * Mathf.Abs(Mathf.Sin(u * 9f)); };
            var ends = new float[n];
            for (int i = 0; i < n; i++)
            {
                int m = s.Add(kids[i]);
                float t = WalkTime(at[i], gateOut, KidWalk);
                s.Part(m, 0f, t, gateOut, gateIn, DollStyle.Stand, null, PropKind.None, KidWalk);
                // the chaser (kid 0) comes in last
                float wait = i == 0 ? 0.6f : 0f;
                if (wait > 0f) s.Part(m, t, t + wait, gateOut, gateIn, DollStyle.Stand, CrouchPose); t += wait;
                float len = (gateIn - gateOut).magnitude / KidRun + 0.05f;
                Go(s, m, t, len, gateIn, rows[i].a, KidRun, true); t += len;
                len = (rows[i].a - gateIn).magnitude / KidRun + 0.05f;
                Go(s, m, t, len, rows[i].a, rows[i].b, KidRun, true); t += len;
                // along the row, back along the next (or the same) row
                var back = rows[(i + 1) % rows.Count];
                var legs = new[] { (rows[i].b, rows[i].a), (back.b, back.a), (back.a, back.b) };
                var from = rows[i].a;
                foreach (var (to, face) in legs)
                {
                    float l = (to - from).magnitude / (KidRun * (0.9f + 0.05f * i)) + 0.05f;
                    var pt = s.Part(m, t, t + l, to, to + (to - from), DollStyle.Stand, duck, PropKind.None, KidRun * (0.9f + 0.05f * i));
                    pt.run = true; pt.poseWalking = true;
                    t += l; from = to;
                }
                ends[i] = t;
            }
            float wave = 0f; foreach (float e in ends) wave = Mathf.Max(wave, e);
            s.a = wave - 1.6f;
            if (farmer != Nobody)
            {
                int f = s.Add(farmer);
                s.Part(f, s.a - 0.4f, s.a, farmerAt, mid, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean -= 6f * In(u, 0f, l); });
                s.Part(f, s.a, s.a + 1.6f, farmerAt, mid, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float flap = Mathf.Sin(u / 0.8f * Mathf.PI * 2f);
                    p.armR = new Vector3(40f, 0f, 60f + 40f * Mathf.Max(0f, flap)); p.armL = new Vector3(40f, 0f, -60f - 40f * Mathf.Max(0f, flap));
                });
            }
            for (int i = 0; i < n; i++)
            {
                float t = ends[i];
                if (t < wave) { s.Part(i, t, wave, s.parts[i][^1].pos, gateIn, DollStyle.Laugh); t = wave; }
                float l1 = (gateIn - s.parts[i][^1].pos).magnitude / KidRun + 0.05f;
                Go(s, i, t, l1, gateIn, gateOut, KidRun, true); t += l1;
                var away = gateOut + (gateOut - gateIn).normalized * 0.25f + new Vector2(-(gateOut - gateIn).y, (gateOut - gateIn).x).normalized * ((i - 1) * 0.08f);
                float l2 = (away - gateIn).magnitude / KidRun + 0.05f;
                Go(s, i, t, l2, away, away + (away - gateIn), KidRun, true);
            }
            return s;
        }

        // ================================================================== B57.b a scarecrow made together
        /// <summary>B57.b: grown-up <paramref name="g1"/> brings two poles (the carrying pole in the hands) and plants the cross at <paramref name="spot"/>
        /// (pushed in: lean 30, arms down, <c>Dust</c> + a jolt: <see cref="VillageFx.Cross"/>); kid <paramref name="kid"/> hands a bundle of straw up
        /// to <paramref name="g2"/> (B7.b), who ties it on (fiddle 1.5 s: <see cref="VillageFx.Straw"/>) and puts an old hat on top (arm x 170 →
        /// <see cref="VillageFx.Hat"/>, a pop). Everyone steps back and laughs; a songbird lands on its arm (<see cref="VillageFx.Bird"/>) and they laugh
        /// harder. <see cref="CampScene.a"/>: the cross in; <see cref="CampScene.b"/>: the hat on.</summary>
        public static CampScene Scarecrow(int g1, Vector2 at1, int g2, Vector2 at2, int kid, Vector2 kidAt, Vector2 spot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var across = new Vector2(-toCam.y, toCam.x);
            var s = new CampScene(CampUse.Scarecrow, t0, spot) { holdFocus = true };
            int a = s.Add(g1), b = s.Add(g2), k = kid != Nobody ? s.Add(kid) : -1;
            var pa = spot + toCam * 0.02f - across * 0.1f; var pb = spot + toCam * 0.02f + across * 0.1f; var pk = spot + toCam * 0.1f + across * 0.17f;
            float wa = WalkTime(at1, pa, Walk), wb = WalkTime(at2, pb, Walk), wk = k >= 0 ? WalkTime(kidAt, pk, KidWalk) : 0f;
            s.Part(a, 0f, wa, pa, spot, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(b, 0f, wb, pb, spot, DollStyle.Stand, null, PropKind.None, Walk);
            if (k >= 0) s.Part(k, 0f, wk, pk, pb, DollStyle.Stand, null, PropKind.Sheaf, KidWalk);
            float t = Mathf.Max(wa, Mathf.Max(wb, wk));
            if (wa < t) s.Part(a, wa, t, pa, spot, DollStyle.Stand);
            if (wb < t) s.Part(b, wb, t, pb, spot, DollStyle.Stand);
            if (k >= 0 && wk < t) s.Part(k, wk, t, pk, pb, DollStyle.Stand, null, PropKind.Sheaf);
            // plant the cross: two pushes
            s.Part(a, t, t + 1.4f, pa, spot, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float push = Mathf.Max(Bump(u, 0.1f, 0.6f), Bump(u, 0.7f, 0.6f));
                p.lean += 30f * push; p.armL = new Vector3(70f - 30f * push, 0f, -10f); p.armR = new Vector3(70f - 30f * push, 0f, 10f); p.squash = 1f - 0.08f * push;
            });
            s.Part(b, t, t + 1.4f, pb, spot, DollStyle.Look);
            if (k >= 0) s.Part(k, t, t + 1.4f, pk, spot, DollStyle.Stand, null, PropKind.Sheaf);
            s.a = t + 0.45f;
            Do(s, s.a, VillageFx.Cross, spot);
            s.Fx(t + 0.45f, CampFxKind.Dust, G(spot, 0.01f), 0.45f, 3);
            s.Fx(t + 1.05f, CampFxKind.Dust, G(spot, 0.01f), 0.35f, 2);
            t += 1.4f;
            // the kid hands the straw up (B7.b)
            if (k >= 0)
            {
                s.Part(k, t, t + 1f, pk - across * 0.04f, pb, DollStyle.Stand, (ref DollPose p, float u, float l) => { float r = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 8f), r); p.armL = Lerp(p.armL, new Vector3(150f, 0f, -8f), r); p.offset.y += 0.015f * r; }, PropKind.Sheaf);
                s.Part(b, t, t + 1f, pb, pk, DollStyle.Stand, (ref DollPose p, float u, float l) => { float r = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(70f, 0f, 6f), r); p.lean += 10f * r; });
                s.Part(a, t, t + 1f, pa, spot, DollStyle.Stand, NodPose);
                t += 1f;
            }
            // tied on
            s.Part(b, t, t + 1.5f, pb, spot, DollStyle.Stand, (ref DollPose p, float u, float l) => { FiddleHandsPose(ref p, u, l); p.armL.x += 30f; p.armR.x += 30f; }, PropKind.Sheaf);
            s.Part(a, t, t + 1.5f, pa, spot, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(100f, 0f, -14f); p.armR = new Vector3(100f, 0f, 14f); }); // holds the cross
            if (k >= 0) s.Part(k, t, t + 1.5f, pk, spot, DollStyle.Look);
            Do(s, t + 1.4f, VillageFx.Straw, spot);
            t += 1.5f;
            // the hat on top
            s.Part(b, t, t + 1f, pb, spot, DollStyle.Stand, (ref DollPose p, float u, float l) => { float r = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(170f, 0f, 6f), r); p.offset.y += 0.02f * r; });
            s.Part(a, t, t + 1f, pa, spot, DollStyle.Look);
            if (k >= 0) s.Part(k, t, t + 1f, pk, spot, DollStyle.Look);
            s.b = t + 0.55f;
            Do(s, s.b, VillageFx.Hat, spot);
            t += 1f;
            // everyone steps back and laughs; a bird lands on it, they laugh harder
            var ba = pa + toCam * 0.06f - across * 0.06f; var bb = pb + toCam * 0.06f + across * 0.06f; var bk = pk + toCam * 0.04f + across * 0.04f;
            Go(s, a, t, 0.6f, ba, spot, Walk); Go(s, b, t, 0.6f, bb, spot, Walk); if (k >= 0) Go(s, k, t, 0.6f, bk, spot, KidWalk);
            t += 0.6f;
            s.Part(a, t, t + 1.4f, ba, spot, DollStyle.Laugh); s.Part(b, t, t + 1.4f, bb, spot, DollStyle.Laugh); if (k >= 0) s.Part(k, t, t + 1.4f, bk, spot, DollStyle.Laugh);
            t += 1.4f;
            s.c = t; Do(s, t, VillageFx.Bird, spot);
            s.Part(a, t, t + 0.6f, ba, spot, DollStyle.Point);
            s.Part(b, t, t + 2.4f, bb, spot, DollStyle.Laugh, HipsPose);
            if (k >= 0) s.Part(k, t, t + 2.4f, bk, spot, DollStyle.Stand, ClapJumpPose);
            s.Part(a, t + 0.6f, t + 2.4f, ba, spot, DollStyle.Laugh);
            t += 2.4f;
            Go(s, a, t, 1.2f, ba + toCam * 0.3f - across * 0.2f, ba + toCam, Walk);
            Go(s, b, t, 1.2f, bb + toCam * 0.3f + across * 0.2f, bb + toCam, Walk);
            if (k >= 0) Go(s, k, t, 1f, bk + toCam * 0.3f, bk + toCam, KidWalk);
            return s;
        }

        /// <summary>B57.c: the ring's radius for <paramref name="n"/> dancers (room for each, never under 0.16 u round the sheaf) [est].</summary>
        public static float RingR(int n) => Mathf.Max(0.2f, n * 0.07f / (Mathf.PI * 2f) * 1.9f + 0.06f);

        // ================================================================== B57.c the harvest dance
        /// <summary>B57.c: on the last harvest day everyone free comes to the cut field: the last sheaf stands up in the middle (<see cref="VillageFx.Sheaf"/>),
        /// and they make B2.e's ring round it, hands joined (arms z ∓70), skipping (hop 0.02 u at 2.5 Hz, 0.3 u/s) one way and back,
        /// <paramref name="danceFor"/> s (12–16); then they all jump together, arms up (C3.d's end). <see cref="CampScene.a"/>: the jump.</summary>
        public static CampScene HarvestDance(IList<int> who, IList<Vector2> at, Vector2 centre, float danceFor, float t0)
        {
            int n = who.Count;
            float r = RingR(n);
            var s = new CampScene(CampUse.HarvestDance, t0, centre) { holdFocus = true };
            float walk = 0f;
            var start = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                s.Add(who[i]);
                float ang = i * Mathf.PI * 2f / n;
                start[i] = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                walk = Mathf.Max(walk, WalkTime(at[i], start[i], Walk));
            }
            Do(s, 0.2f, VillageFx.Sheaf, centre);
            for (int i = 0; i < n; i++) { s.Part(i, 0f, walk, start[i], centre, DollStyle.Stand, null, PropKind.None, Walk); }
            float t = walk;
            for (int i = 0; i < n; i++) s.Part(i, t, t + 0.6f, start[i], centre, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, l); p.armL = new Vector3(20f, 0f, -70f * k); p.armR = new Vector3(20f, 0f, 70f * k); });
            t += 0.6f;
            danceFor = Mathf.Clamp(danceFor, 12f, 16f);
            float w = VillageGeo.RingPace / r; // rad/s
            const int Steps = 14; // segments per round
            float seg = Mathf.PI * 2f / Steps / w;
            int segs = Mathf.Max(4, Mathf.RoundToInt(danceFor / seg));
            CampPoseFn ring = (ref DollPose p, float u, float l) => { p.armL = new Vector3(20f, 0f, -70f); p.armR = new Vector3(20f, 0f, 70f); p.offset.y += 0.02f * Mathf.Abs(Mathf.Sin(u * 2.5f * Mathf.PI)); };
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, tt = t, ang = a0;
                for (int k = 0; k < segs; k++)
                {
                    float dir = k < segs / 2 ? 1f : -1f; // one way, then back
                    ang += dir * Mathf.PI * 2f / Steps;
                    var p = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    var tangent = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * dir;
                    var part = s.Part(i, tt, tt + seg, p, p + tangent, DollStyle.Stand, ring, PropKind.None, VillageGeo.RingPace * 1.15f);
                    part.poseWalking = true;
                    tt += seg;
                }
                start[i] = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            }
            t += segs * seg;
            s.a = t + 0.3f;
            for (int i = 0; i < n; i++)
            {
                s.Part(i, t, t + 0.3f, start[i], centre, DollStyle.Stand, CrouchPose);
                s.Part(i, t + 0.3f, t + 1f, start[i], centre, DollStyle.Stand, ArmsUpPose);
                s.Part(i, t + 1f, t + 2.4f, start[i], centre, DollStyle.Laugh);
                var away = centre + (start[i] - centre).normalized * (r + 0.3f);
                Go(s, i, t + 2.4f, 1.4f, away, away + (away - centre), Walk);
            }
            return s;
        }

        // ================================================================== B57.d homes mended and dressed
        /// <summary>B57.d: someone of the household sets a short ladder against the front wall (<paramref name="foot"/>, leaning to <paramref name="wall"/>),
        /// climbs onto its rungs (<see cref="VillageGeo.RungUp"/>) and taps three times with the hammer (x 110 → 140, <c>Chips</c> at the wall), climbs
        /// down, then fixes the home's next thing at <paramref name="extraAt"/> (both hands, 1 s → <see cref="VillageFx.Extra"/>: a pop), steps back and
        /// nods. <see cref="CampScene.a"/>: the first tap; <see cref="CampScene.b"/>–<see cref="CampScene.c"/>: the ladder stands (CampLife draws it).</summary>
        public static CampScene HomeMend(int who, Vector2 at, Vector2 foot, Vector2 wall, Vector2 extraAt, Vector2 toCam, float tapH, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.HomeMend, t0, (foot + extraAt) * 0.5f) { holdFocus = true, p0 = foot, p1 = wall };
            int m = s.Add(who);
            var stand = foot + Dir(wall, foot, toCam) * 0.06f;
            float t = WalkTime(at, stand, Walk);
            s.Part(m, 0f, t, stand, wall, DollStyle.Stand, null, PropKind.None, Walk);
            s.b = t;
            s.Part(m, t, t + 0.7f, stand, wall, DollStyle.Stand, FiddleHandsPose); t += 0.7f; // the ladder set against the wall
            var up = Vector2.Lerp(foot, wall, 0.35f);
            CampPoseFn climb = (ref DollPose p, float u, float l) => { float k = In(u, 0f, l); p.offset.y += VillageGeo.RungUp * k; p.armL = new Vector3(120f, 0f, -14f); p.armR = new Vector3(120f, 0f, 14f); };
            var c1 = s.Part(m, t, t + 0.8f, up, wall, DollStyle.Stand, climb, PropKind.None, 0.15f); c1.poseWalking = true; t += 0.8f;
            s.a = t + 0.3f;
            s.Part(m, t, t + 2f, up, wall, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                p.offset.y += VillageGeo.RungUp;
                float ph = Mathf.Repeat(u / 0.6f, 1f);
                p.armR = new Vector3(ph < 0.5f ? Mathf.Lerp(110f, 145f, Smooth(ph * 2f)) : Mathf.Lerp(145f, 110f, Smooth((ph - 0.5f) * 2f)), 0f, 8f);
                p.armL = new Vector3(120f, 0f, -14f);
            }, PropKind.Hammer);
            for (int i = 0; i < 3; i++) s.Fx(t + 0.3f + i * 0.6f, CampFxKind.Chips, G(wall, tapH), 0.45f, 3);
            t += 2f;
            CampPoseFn down = (ref DollPose p, float u, float l) => { float k = 1f - In(u, 0f, l); p.offset.y += VillageGeo.RungUp * k; p.armL = new Vector3(120f * k, 0f, -14f); p.armR = new Vector3(120f * k, 0f, 14f); };
            var c2 = s.Part(m, t, t + 0.7f, stand, wall, DollStyle.Stand, down, PropKind.Hammer, 0.15f); c2.poseWalking = true; t += 0.7f;
            s.Part(m, t, t + 0.6f, stand, wall, DollStyle.Stand, FiddleHandsPose); t += 0.6f; // the ladder taken down
            s.c = t;
            var fix = extraAt + Dir(wall, foot, toCam) * 0.07f;
            float wf = WalkTime(stand, fix, Walk);
            s.Part(m, t, t + wf, fix, extraAt, DollStyle.Stand, null, PropKind.None, Walk); t += wf;
            s.Part(m, t, t + 1.1f, fix, extraAt, DollStyle.Stand, (ref DollPose p, float u, float l) => { FiddleHandsPose(ref p, u, l); p.armL.x += 20f; p.armR.x += 20f; p.lean += 6f; });
            s.d = t + 1f;
            Do(s, s.d, VillageFx.Extra, extraAt);
            t += 1.1f;
            var back = fix + Dir(wall, foot, toCam) * 0.12f;
            Go(s, m, t, 0.6f, back, extraAt, Walk); t += 0.6f;
            s.Part(m, t, t + 1f, back, extraAt, DollStyle.Stand, (ref DollPose p, float u, float l) => { NodPose(ref p, u, l); HipsPose(ref p, u, l); }); t += 1f;
            Go(s, m, t, 1.2f, back + toCam * 0.3f, back + toCam, Walk);
            return s;
        }

        // ================================================================== B57.e gossip at the well
        /// <summary>B57.e: 2–3 grown-ups who came for water set their full buckets down at the well (a squash) and talk (B7.a, the laugh at the end)
        /// <paramref name="chatFor"/> s (6–10), one leaning on the rim (roll 10, lean 12), then pick the buckets up and carry them home.
        /// <see cref="CampScene.a"/>: the buckets down; <see cref="CampScene.b"/>: picked up (CampLife draws them on the ground between).</summary>
        public static CampScene WellGossip(IList<int> who, IList<Vector2> at, Vector2 well, float rimR, Vector2 toCam, float chatFor, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            int n = who.Count;
            var s = new CampScene(CampUse.WellGossip, t0, well) { holdFocus = true };
            var spots = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                s.Add(who[i]);
                // at the well's two sides as the camera sees it (its roof hides the camera's side from above): the leaner (0) at the rim on one
                // side, the others facing her across the well's corner on the other [est]
                float ang = i == 0 ? -80f : i == 1 ? 80f : 45f;
                var d = Rot(toCam, ang);
                spots[i] = well + d * (rimR + (i == 0 ? 0.045f : i == 1 ? 0.075f : 0.13f));
                t = Mathf.Max(t, WalkTime(at[i], spots[i], Walk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, spots[i], well, DollStyle.Stand, null, PropKind.Bucket, Walk);
            s.a = t + 0.3f;
            for (int i = 0; i < n; i++)
                s.Part(i, t, t + 0.5f, spots[i], well, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 25f * k; p.squash = 1f - 0.12f * k; });
            for (int i = 0; i < n; i++) s.Fx(t + 0.3f, CampFxKind.Dust, G(spots[i] + (well - spots[i]).normalized * 0.03f, 0.005f), 0.25f, 2);
            t += 0.5f;
            chatFor = Mathf.Clamp(chatFor, 6f, 10f);
            for (int i = 0; i < n; i++)
            {
                var face = n > 1 ? spots[(i + 1) % n] : well;
                if (i == 0) s.Part(i, t, t + chatFor - 1.2f, spots[i], n > 1 ? spots[1] : well, DollStyle.Talk, (ref DollPose p, float u, float l) => { p.roll += 10f; p.lean += 12f; p.armL = new Vector3(60f, 0f, -20f); });
                else s.Part(i, t, t + chatFor - 1.2f, spots[i], spots[0], DollStyle.Talk);
                s.Part(i, t + chatFor - 1.2f, t + chatFor, spots[i], face, DollStyle.Laugh);
            }
            t += chatFor;
            for (int i = 0; i < n; i++) s.Part(i, t, t + 0.5f, spots[i], well, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 25f * k; p.squash = 1f - 0.12f * k; });
            t += 0.5f;
            s.b = t - 0.2f;
            for (int i = 0; i < n; i++)
            {
                var home = i < at.Count ? at[i] : spots[i] + toCam * 0.4f;
                var go = Vector2.Lerp(spots[i], home, Mathf.Min(1f, 0.5f / Mathf.Max(0.01f, (home - spots[i]).magnitude)));
                s.Part(i, t, t + WalkTime(spots[i], go, Walk), go, go + (go - spots[i]), DollStyle.Stand, null, PropKind.Bucket, Walk);
            }
            return s;
        }

        // ================================================================== B57.f a pebble down the well
        /// <summary>B57.f: a kid climbs the rim on tiptoe (+0.03 u), drops a pebble (arm x 90, the hand opens: <see cref="VillageFx.Pebble"/>), leans
        /// over with a hand at the ear (arm x 120 to the head), waits 1.5 s → the tiny ring deep down (<see cref="VillageFx.PebbleRing"/>) and the kid
        /// jumps back delighted. <see cref="CampScene.a"/>: the drop; <see cref="CampScene.b"/>: the ring. Reads: a kid leaning over the well, hand at the ear.</summary>
        public static CampScene WellPebble(int kid, Vector2 at, Vector2 well, float rimR, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var side = Rot(toCam, 35f);
            var s = new CampScene(CampUse.WellPebble, t0, well) { holdFocus = true };
            int m = s.Add(kid);
            var st = well + side * (rimR + 0.04f);
            float t = WalkTime(at, st, KidWalk);
            s.Part(m, 0f, t, st, well, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.c = 0f; // the pebble is in the hand from the start until the drop
            s.Part(m, t, t + 0.5f, st, well, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, l); p.offset.y += 0.03f * k; p.armL = Lerp(p.armL, new Vector3(70f, 0f, -20f), k); }); t += 0.5f;
            s.Part(m, t, t + 0.8f, st, well, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.3f); p.offset.y += 0.03f; p.lean += 20f * k; p.armR = Lerp(p.armR, new Vector3(90f, 0f, 4f), k); p.armL = new Vector3(70f, 0f, -20f); });
            s.a = t + 0.6f;
            Do(s, s.a, VillageFx.Pebble, well, VillageGeo.WellTop, m);
            t += 0.8f;
            s.Part(m, t, t + 1.9f, st, well, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float k = In(u, 0f, 0.3f); p.offset.y += 0.03f; p.lean += 26f * k; p.armL = Lerp(new Vector3(70f, 0f, -20f), new Vector3(120f, 0f, -48f), k); p.armR = Lerp(new Vector3(90f, 0f, 4f), new Vector3(60f, 0f, 10f), k); p.yaw += 12f * k;
            });
            s.b = t + 1.5f + 0.25f;
            Do(s, s.b, VillageFx.PebbleRing, well, VillageGeo.WellTop - 0.01f);
            t += 1.9f;
            s.Part(m, t, t + 0.4f, st + side * 0.06f, well, DollStyle.HopBack, null, PropKind.None, 0.3f); t += 0.4f;
            s.Part(m, t, t + 1.2f, st + side * 0.06f, well, DollStyle.Stand, ClapJumpPose); t += 1.2f;
            s.Part(m, t, t + 0.6f, st + side * 0.06f, well, DollStyle.Laugh); t += 0.6f;
            Go(s, m, t, 1f, st + side * 0.3f + toCam * 0.1f, st + toCam, KidWalk);
            return s;
        }

        // ================================================================== B57.g watching the sails
        /// <summary>B57.g: 1–2 kids sit facing the windmill and watch the sails go round, the head nodding ±30 deg in step with them (one nod each
        /// time a sail passes: <paramref name="sailDegPerSec"/>), <paramref name="sitFor"/> s. <see cref="CampScene.a"/>: sat down.</summary>
        public static CampScene SailWatch(IList<int> kids, IList<Vector2> at, IList<Vector2> seats, Vector2 mill, float sailDegPerSec, float sitFor, float t0)
        {
            var s = new CampScene(CampUse.SailWatch, t0, Vector2.Lerp(seats[0], mill, 0.5f)) { holdFocus = true };
            float t = 0f; int n = Mathf.Min(kids.Count, seats.Count);
            for (int i = 0; i < n; i++) { s.Add(kids[i]); t = Mathf.Max(t, WalkTime(at[i], seats[i], KidWalk)); }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, seats[i], mill, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.a = t;
            float per = 90f / Mathf.Max(8f, Mathf.Abs(sailDegPerSec)); // seconds per sail
            for (int i = 0; i < n; i++)
            {
                float ph = i * 0.17f;
                s.Part(i, t, t + sitFor, seats[i], mill, DollStyle.Sit, (ref DollPose p, float u, float l) =>
                {
                    Seated(ref p);
                    p.yaw += 30f * Mathf.Sin((u / per + ph) * Mathf.PI * 2f);
                    p.lean -= 12f; // the head up to the sails
                });
                s.Part(i, t + sitFor, t + sitFor + 0.5f, seats[i], mill, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y -= 0.03f * (1f - In(u, 0f, l)); });
                var away = seats[i] + (seats[i] - mill).normalized * 0.3f + new Vector2(0.1f * (i - 0.5f), 0f);
                Go(s, i, t + sitFor + 0.5f, 1.2f, away, away + (away - mill), KidWalk);
            }
            return s;
        }

        // ================================================================== B57.h racing the sails' shadow
        /// <summary>B57.h: two kids run round the mill's foot (<see cref="VillageGeo.ShadowR"/> out) with the sails' shadow, trying to stay in it; the
        /// shadow and the kids don't keep the same pace, so each time a sail's shadow and a kid cross they jump (0.05 u, arms up).
        /// <paramref name="sailDegPerSec"/>: the sails' turn (sign: their way round); <paramref name="runFor"/> s of running (about two laps).
        /// <see cref="CampScene.a"/>: the first jump.</summary>
        public static CampScene SailShadow(IList<int> kids, IList<Vector2> at, Vector2 foot, float r, float sailDegPerSec, float startDeg, float runFor, float t0)
        {
            var s = new CampScene(CampUse.SailShadow, t0, foot) { holdFocus = true };
            int n = kids.Count;
            float sign = sailDegPerSec >= 0f ? 1f : -1f;
            float wSail = Mathf.Max(20f, Mathf.Abs(sailDegPerSec)) * Mathf.Deg2Rad;
            float wKid = Mathf.Min(0.42f / r, KidRun * 0.9f / r); // rad/s: a kid's run [est]
            float rel = Mathf.Abs(wSail - wKid); // how fast the shadow and the kids cross
            float passEvery = Mathf.Clamp((Mathf.PI * 0.5f) / Mathf.Max(0.05f, rel), 1.4f, 4f);
            float t = 0f;
            var starts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                s.Add(kids[i]);
                float a = startDeg * Mathf.Deg2Rad - sign * i * 0.35f;
                starts[i] = foot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                t = Mathf.Max(t, WalkTime(at[i], starts[i], KidWalk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, starts[i], starts[i] + new Vector2(-(starts[i] - foot).y, (starts[i] - foot).x) * sign, DollStyle.Stand, null, PropKind.None, KidWalk);
            for (int i = 0; i < n; i++) s.Part(i, t, t + 0.6f, starts[i], starts[i] + new Vector2(-(starts[i] - foot).y, (starts[i] - foot).x) * sign, DollStyle.Stand, CrouchPose);
            t += 0.6f;
            runFor = Mathf.Clamp(runFor, 6f, 12f);
            const float Seg = 0.22f;
            int segs = Mathf.Max(4, Mathf.RoundToInt(runFor / Seg));
            s.a = t + passEvery * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float a = startDeg * Mathf.Deg2Rad - sign * i * 0.35f, tt = t;
                float first = t + passEvery * (0.5f + 0.12f * i);
                for (int k = 0; k < segs; k++)
                {
                    a += sign * wKid * Seg;
                    var p = foot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    var tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * sign;
                    float ps = tt;
                    var part = s.Part(i, tt, tt + Seg, p, p + tangent, DollStyle.Stand, (ref DollPose q, float u, float l) =>
                    {
                        float at0 = ps + u, j = 0f;
                        for (float jt = first; jt < ps + Seg + 0.5f; jt += passEvery) j = Mathf.Max(j, Bump(at0, jt, 0.4f));
                        q.offset.y += 0.05f * j;
                        q.armL = Lerp(q.armL, new Vector3(160f, 0f, -24f), j); q.armR = Lerp(q.armR, new Vector3(160f, 0f, 24f), j);
                    }, PropKind.None, r * wKid * 1.1f);
                    part.run = true; part.poseWalking = true;
                    tt += Seg;
                }
                starts[i] = foot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            t += segs * Seg;
            for (int i = 0; i < n; i++)
            {
                s.Part(i, t, t + 1.6f, starts[i], foot, DollStyle.Laugh, (ref DollPose p, float u, float l) => { p.lean -= 10f; });
                var away = starts[i] + (starts[i] - foot).normalized * 0.3f;
                Go(s, i, t + 1.6f, 1.2f, away, away + (away - foot), KidWalk);
            }
            return s;
        }

        // ================================================================== B57.i a break on the log pile
        /// <summary>B57.i: two grown-ups on a break sit on the lumber mill's stacked logs (on the low front log, +0.06 u), facing out, chat (B7.a's
        /// hands) and drink from a cup (B9.a's hand to the mouth; CampLife draws the cup), <paramref name="sitFor"/> s. <see cref="CampScene.a"/>: sat.</summary>
        public static CampScene LogPileSit(IList<int> who, IList<Vector2> at, IList<Vector2> seats, Vector2 face, float sitFor, float t0)
        {
            int n = Mathf.Min(who.Count, seats.Count);
            var mid = Vector2.zero; for (int i = 0; i < n; i++) mid += seats[i]; mid /= Mathf.Max(1, n);
            var s = new CampScene(CampUse.LogPileSit, t0, mid) { holdFocus = true };
            float t = 0f;
            var fronts = new Vector2[n];
            var outDir = Unit(face - mid, Vector2.down);
            for (int i = 0; i < n; i++) { s.Add(who[i]); fronts[i] = seats[i] + outDir * 0.06f; t = Mathf.Max(t, WalkTime(at[i], fronts[i], Walk)); }
            for (int i = 0; i < n; i++)
            {
                s.Part(i, 0f, t, fronts[i], seats[i], DollStyle.Stand, null, PropKind.None, Walk);
                var sitDown = s.Part(i, t, t + 0.5f, seats[i], seats[i] + outDir, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += VillageGeo.SeatLift(VillageGeo.PileLow + 0.006f) * In(u, 0f, l); }, PropKind.None, 0.2f);
                sitDown.poseWalking = true;
            }
            t += 0.5f; s.a = t;
            var other = n > 1 ? new[] { seats[1], seats[0] } : new[] { face, face };
            for (int i = 0; i < n; i++)
            {
                int me = i;
                var look = Vector2.Lerp(seats[i] + outDir, other[Mathf.Min(i, 1)], 0.5f);
                s.Part(i, t, t + sitFor, seats[i], look, DollStyle.Sit, (ref DollPose p, float u, float l) =>
                {
                    SitOn(VillageGeo.PileLow + 0.006f, 2f, me * 0.3f)(ref p, u, l);
                    // talk with the hands, the cup to the mouth now and then (B9.a)
                    float talk = me == 0 ? Mathf.Max(0f, Mathf.Sin(u * 1.3f)) : Mathf.Max(0f, Mathf.Sin(u * 1.3f + Mathf.PI));
                    p.armL = Lerp(p.armL, new Vector3(70f, 0f, -26f + 16f * Mathf.Sin(u * 7f)), talk);
                    Bite(ref p, u, me == 0 ? 1.5f : 3.6f, 2);
                });
            }
            s.b = t + sitFor;
            for (int i = 0; i < n; i++)
            {
                var up = s.Part(i, t + sitFor, t + sitFor + 0.5f, fronts[i], fronts[i] + outDir, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += VillageGeo.SeatLift(VillageGeo.PileLow + 0.006f) * (1f - In(u, 0f, l)); }, PropKind.None, 0.2f);
                up.poseWalking = true;
                Go(s, i, t + sitFor + 0.5f, 1.4f, fronts[i] + outDir * 0.35f + new Vector2(-outDir.y, outDir.x) * (i == 0 ? -0.15f : 0.15f), fronts[i] + outDir, Walk);
            }
            return s;
        }

        // ================================================================== B57.j balance on a log
        /// <summary>B57.j: a kid steps up onto the lying log at <paramref name="from"/> (+<see cref="VillageGeo.LogTop"/>) and walks along it to
        /// <paramref name="to"/>, arms out (z ∓90), wobbling (roll ±8 deg at 2 Hz), jumps off the end (a bounce, <c>Dust</c>) and throws the arms up.
        /// <see cref="CampScene.a"/>: the landing.</summary>
        public static CampScene LogBalance(int kid, Vector2 at, Vector2 from, Vector2 to, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var along = Unit(to - from, Vector2.right);
            var s = new CampScene(CampUse.LogBalance, t0, (from + to) * 0.5f) { holdFocus = true };
            int m = s.Add(kid);
            var foot = from - along * 0.06f;
            float t = WalkTime(at, foot, KidWalk);
            s.Part(m, 0f, t, foot, to, DollStyle.Stand, null, PropKind.None, KidWalk);
            var up = s.Part(m, t, t + 0.5f, from, to, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, l); p.offset.y += VillageGeo.LogTop * k; p.armL = new Vector3(20f, 0f, -90f * k); p.armR = new Vector3(20f, 0f, 90f * k); }, PropKind.None, 0.15f);
            up.poseWalking = true; t += 0.5f;
            const float Pace = 0.09f;
            float len = (to - from).magnitude / Pace;
            var walk = s.Part(m, t, t + len, to, to + along, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                p.offset.y += VillageGeo.LogTop;
                float w = Mathf.Sin(u * 2f * Mathf.PI * 2f);
                p.roll += 8f * w;
                p.armL = new Vector3(20f, 0f, -90f + 14f * w); p.armR = new Vector3(20f, 0f, 90f + 14f * w);
            }, PropKind.None, Pace);
            walk.poseWalking = true; t += len;
            var land = to + along * 0.1f;
            var jump = s.Part(m, t, t + 0.45f, land, land + along, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float k = u / l; p.offset.y += Mathf.Lerp(VillageGeo.LogTop, 0f, k) + 0.06f * Mathf.Sin(k * Mathf.PI);
                p.armL = new Vector3(60f, 0f, -60f); p.armR = new Vector3(60f, 0f, 60f);
            }, PropKind.None, 0.25f);
            jump.poseWalking = true; t += 0.45f;
            s.a = t;
            s.Fx(t, CampFxKind.Dust, G(land, 0.005f), 0.4f, 3);
            s.Part(m, t, t + 0.25f, land, land + along, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.squash = 1f - 0.2f * Bump(u, 0f, l); }); t += 0.25f;
            s.Part(m, t, t + 1f, land, land + toCam, DollStyle.Stand, ArmsUpPose); t += 1f;
            s.Part(m, t, t + 0.8f, land, land + toCam, DollStyle.Laugh); t += 0.8f;
            Go(s, m, t, 1f, land + toCam * 0.3f + along * 0.1f, land + toCam, KidWalk);
            return s;
        }

        // ================================================================== B59.k barn chores (work)
        /// <summary>B59.k: someone of the household comes to the hay heap by the barn (<paramref name="heap"/>), pitches three forkfuls into the barn's
        /// door (<paramref name="door"/>): arm x 40 → 140 with an armful of hay, each throw a puff of grass bits at the door; then fetches the bucket
        /// and sets it down at the door for the animals (a squash). <see cref="CampScene.a"/>: the first throw; <see cref="CampScene.b"/>: the bucket down.</summary>
        public static CampScene BarnChores(int who, Vector2 at, Vector2 heap, Vector2 door, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.BarnChores, t0, Vector2.Lerp(heap, door, 0.5f)) { holdFocus = true };
            int m = s.Add(who);
            var toDoor = Dir(heap, door, Vector2.up);
            var st = heap + toDoor * (VillageGeo.HayR + 0.05f);
            float t = WalkTime(at, st, Walk);
            s.Part(m, 0f, t, st, door, DollStyle.Stand, null, PropKind.None, Walk);
            s.a = t + 0.9f;
            for (int i = 0; i < 3; i++)
            {
                // bend to the heap (lean 30, arms x 40), lift and swing to the door (arms x 140), let go
                s.Part(m, t, t + 0.6f, st, heap, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 30f * k; p.armL = Lerp(p.armL, new Vector3(40f, 0f, -12f), k); p.armR = Lerp(p.armR, new Vector3(40f, 0f, 12f), k); }, PropKind.None);
                s.Fx(t + 0.3f, CampFxKind.Grass, G(heap, VillageGeo.HayTop), 0.3f, 2);
                s.Part(m, t + 0.6f, t + 1.2f, st, door, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, l); p.armL = Lerp(new Vector3(40f, 0f, -12f), new Vector3(140f, 0f, -12f), k); p.armR = Lerp(new Vector3(40f, 0f, 12f), new Vector3(140f, 0f, 12f), k); p.lean -= 6f * k; }, PropKind.Sheaf);
                s.Fx(t + 1.15f, CampFxKind.Grass, G(door, 0.06f), 0.45f, 4);
                t += 1.3f;
            }
            var bucketAt = door - toDoor * 0.06f + new Vector2(-toDoor.y, toDoor.x) * 0.07f;
            float w = WalkTime(st, bucketAt - toDoor * 0.05f, Walk);
            s.Part(m, t, t + w, bucketAt - toDoor * 0.05f, bucketAt, DollStyle.Stand, null, PropKind.Bucket, Walk); t += w;
            s.Part(m, t, t + 0.6f, bucketAt - toDoor * 0.05f, bucketAt, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.lean += 28f * k; p.squash = 1f - 0.1f * k; }, PropKind.Bucket);
            s.b = t + 0.35f; s.p0 = bucketAt;
            s.Fx(s.b, CampFxKind.Dust, G(bucketAt, 0.005f), 0.25f, 2);
            t += 0.6f;
            s.Part(m, t, t + 0.6f, bucketAt - toDoor * 0.05f, door, DollStyle.Stand, NodPose); t += 0.6f;
            Go(s, m, t, 1.2f, bucketAt - toDoor * 0.05f + toCam * 0.3f, bucketAt + toCam, Walk);
            return s;
        }

        // ================================================================== B59.l kids jump in the hay
        /// <summary>B59.l: 2 kids climb onto the hay heap (up 0.06 u) and drop back into it, twice each, in turns: squash 0.6 on landing, grass bits fly,
        /// the heap dents 0.02 u and springs back (<see cref="VillageFx.HayDent"/>); they laugh and climb out. <see cref="CampScene.a"/>: the first drop.</summary>
        public static CampScene HayJump(IList<int> kids, IList<Vector2> at, Vector2 heap, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var across = new Vector2(-toCam.y, toCam.x);
            int n = Mathf.Min(2, kids.Count);
            var s = new CampScene(CampUse.HayJump, t0, heap) { holdFocus = true };
            var foot = new Vector2[n]; var top = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                s.Add(kids[i]);
                float side = i == 0 ? -1f : 1f;
                foot[i] = heap + toCam * (VillageGeo.HayR + 0.04f) + across * side * 0.05f;
                top[i] = heap + across * side * 0.025f;
                t = Mathf.Max(t, WalkTime(at[i], foot[i], KidWalk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, foot[i], heap, DollStyle.Stand, null, PropKind.None, KidWalk);
            bool first = true;
            for (int round = 0; round < 2; round++)
                for (int i = 0; i < n; i++)
                {
                    int me = i, other = 1 - i;
                    // climb up (0.6 s), stand on top arms up (0.4 s), drop back (0.35 s): squash 0.6, bits fly, the heap dents
                    var up = s.Part(me, t, t + 0.6f, top[me], top[me] - toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, l); p.offset.y += VillageGeo.HayTop * k; p.lean += 25f * Bump(u, 0f, l); p.armL = new Vector3(110f * (1f - k), 0f, -12f); p.armR = new Vector3(110f * (1f - k), 0f, 12f); }, PropKind.None, 0.15f);
                    up.poseWalking = true;
                    s.Part(me, t + 0.6f, t + 1f, top[me], top[me] + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += VillageGeo.HayTop; p.armL = new Vector3(165f, 0f, -25f); p.armR = new Vector3(165f, 0f, 25f); });
                    s.Part(me, t + 1f, t + 1.6f, top[me], top[me] + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                    {
                        float k = In(u, 0f, 0.35f);
                        p.offset.y += Mathf.Lerp(VillageGeo.HayTop, VillageGeo.HayTop * 0.4f, k) + 0.03f * Bump(u, 0f, 0.35f);
                        p.lean -= 35f * k; p.squash = 1f - 0.4f * Bump(u, 0.3f, 0.3f);
                        p.armL = new Vector3(150f, 0f, -40f); p.armR = new Vector3(150f, 0f, 40f);
                    });
                    if (first) { s.a = t + 1.35f; first = false; }
                    s.Fx(t + 1.35f, CampFxKind.Grass, G(heap, VillageGeo.HayTop), 0.45f, 5);
                    Do(s, t + 1.35f, VillageFx.HayDent, heap);
                    // the other laughs at the foot meanwhile
                    if (n > 1) s.Part(other, t, t + 1.6f, foot[other], heap, DollStyle.Laugh);
                    // slide off the front
                    var down = s.Part(me, t + 1.6f, t + 2.1f, foot[me], heap, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += VillageGeo.HayTop * 0.4f * (1f - In(u, 0f, l)); }, PropKind.None, 0.2f);
                    down.poseWalking = true;
                    if (n > 1) s.Part(other, t + 1.6f, t + 2.1f, foot[other], heap, DollStyle.Stand);
                    t += 2.1f;
                }
            for (int i = 0; i < n; i++)
            {
                s.Part(i, t, t + 1f, foot[i], heap, DollStyle.Laugh);
                Go(s, i, t + 1f, 1f, foot[i] + toCam * 0.3f + across * (i == 0 ? -0.1f : 0.1f), foot[i] + toCam, KidWalk);
            }
            return s;
        }

        // ================================================================== B58.c waiting for a crust
        /// <summary>B58.c: 2–3 kids stand in a row at the bakery door (<paramref name="door"/>, <paramref name="outward"/> away from it), rocking on
        /// the toes (bob 0.015 u at 2 Hz); the baker comes out and hands one a crust (B7.b: <see cref="VillageFx.CrustGive"/>), the others crowd
        /// round; that kid breaks it and shares it (<see cref="VillageFx.CrustShare"/>), and they eat (B9.a). <see cref="CampScene.a"/>: the hand-over.</summary>
        public static CampScene CrustWait(IList<int> kids, IList<Vector2> at, int baker, Vector2 bakerAt, Vector2 door, Vector2 outward, float t0)
        {
            outward = Unit(outward, Vector2.down);
            var across = new Vector2(-outward.y, outward.x);
            int n = kids.Count;
            var s = new CampScene(CampUse.CrustWait, t0, door + outward * 0.12f) { holdFocus = true };
            var row = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                s.Add(kids[i]);
                row[i] = door + outward * 0.15f + across * ((i - (n - 1) * 0.5f) * 0.065f);
                t = Mathf.Max(t, WalkTime(at[i], row[i], KidWalk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, row[i], door, DollStyle.Stand, null, PropKind.None, KidWalk);
            const float Rock = 3f;
            for (int i = 0; i < n; i++) { float ph = i * 0.21f; s.Part(i, t, t + Rock, row[i], door, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += 0.015f * Mathf.Abs(Mathf.Sin((u + ph) * 2f * Mathf.PI)); }); }
            int b = baker != Nobody ? s.Add(baker) : -1;
            s.d = b; // the baker's member (−1: none): CampLife draws the crust in that hand
            var bStand = door + outward * 0.07f;
            if (b >= 0)
            {
                float wb = WalkTime(bakerAt, bStand, Walk);
                float bFrom = Mathf.Max(0f, t + Rock - wb);
                s.Part(b, bFrom, bFrom + wb, bStand, row[0], DollStyle.Stand, null, PropKind.None, Walk);
            }
            t += Rock;
            s.a = t + 0.5f;
            // the hand-over: the baker's arm x 80 out, the kid's x 80 up
            if (b >= 0)
            {
                s.Part(b, t, t + 1f, bStand, row[0], DollStyle.Stand, (ref DollPose p, float u, float l) => { float r = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(80f, 0f, 6f), r); p.lean += 10f * r; });
                s.Part(b, t + 1f, t + 2f, bStand, row[0], DollStyle.Stand, NodPose);
                Go(s, b, t + 2f, WalkTime(bStand, bakerAt, Walk), bakerAt, bakerAt + (bakerAt - door), Walk);
            }
            Do(s, s.a, VillageFx.CrustGive, door, 0f, b >= 0 ? b : 0, 0f);
            s.Part(0, t, t + 1f, row[0], door, DollStyle.Stand, (ref DollPose p, float u, float l) => { float r = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(85f, 0f, 6f), r); p.offset.y += 0.012f * r; });
            // the others crowd in
            var crowd = new Vector2[n];
            for (int i = 1; i < n; i++)
            {
                crowd[i] = row[0] + (row[i] - row[0]).normalized * 0.06f - outward * 0.0f;
                Go(s, i, t, 0.6f, crowd[i], row[0], KidWalk);
                s.Part(i, t + 0.6f, t + 1f, crowd[i], row[0], DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean += 14f; });
            }
            t += 1f;
            // shared: kid 0 breaks it (both hands, 0.6 s) and gives a piece to each
            s.Part(0, t, t + 0.6f, row[0], row[0] + outward, DollStyle.Stand, FiddleHandsPose);
            for (int i = 1; i < n; i++) s.Part(i, t, t + 0.6f, crowd[i], row[0], DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean += 14f; });
            t += 0.6f;
            for (int i = 1; i < n; i++)
            {
                float g0 = t + (i - 1) * 0.8f;
                int me = i;
                Do(s, g0 + 0.4f, VillageFx.CrustShare, door, 0f, me);
                s.Part(0, g0, g0 + 0.8f, row[0], crowd[i], DollStyle.Stand, (ref DollPose p, float u, float l) => { float r = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(80f, 0f, 6f), r); });
                s.Part(i, t, g0 + 0.8f, crowd[i], row[0], DollStyle.Stand, (ref DollPose p, float u, float l) => { float r = Bump(u, Mathf.Max(0f, l - 0.8f), 0.8f); p.armR = Lerp(p.armR, new Vector3(80f, 0f, 6f), r); });
            }
            t += Mathf.Max(0, n - 1) * 0.8f;
            // they eat
            for (int i = 0; i < n; i++)
            {
                var spot = i == 0 ? row[0] : crowd[i];
                s.Part(i, t, t + 2.6f, spot, i == 0 ? spot + outward : row[0], DollStyle.Stand, (ref DollPose p, float u, float l) => { Bite(ref p, u, 0.2f, 2); p.offset.y += 0.006f * Mathf.Abs(Mathf.Sin(u * 6f)); });
                var away = spot + outward * 0.3f + across * ((i - (n - 1) * 0.5f) * 0.12f);
                Go(s, i, t + 2.6f, 1.2f, away, away + outward, KidWalk);
            }
            s.b = t + 2.6f;
            return s;
        }

        // ================================================================== B58.f hopscotch
        /// <summary>B58.f: the first kid kneels and scratches a 6-square grid on the street (<see cref="VillageFx.Grid"/>: it stays a view day), from
        /// <paramref name="start"/> along <paramref name="along"/>; then hops along it (0.03 u at 2.5 Hz), turns at the end and hops back, while the
        /// others wait by the start and clap; then the second kid has a go. <see cref="CampScene.a"/>: the grid drawn.</summary>
        public static CampScene Hopscotch(IList<int> kids, IList<Vector2> at, Vector2 start, Vector2 along, Vector2 toCam, float t0)
        {
            along = Unit(along, Vector2.right); toCam = Unit(toCam, Vector2.down);
            var side = Vector2.Dot(new Vector2(-along.y, along.x), toCam) >= 0f ? new Vector2(-along.y, along.x) : new Vector2(along.y, -along.x);
            int n = kids.Count;
            var end = start + along * VillageGeo.GridLen;
            var s = new CampScene(CampUse.Hopscotch, t0, Vector2.Lerp(start, end, 0.5f)) { holdFocus = true, p0 = start, p1 = along };
            var wait = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                s.Add(kids[i]);
                wait[i] = i == 0 ? start - along * 0.05f : start - along * 0.06f + side * (0.07f + 0.06f * (i - 1));
                t = Mathf.Max(t, WalkTime(at[i], wait[i], KidWalk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, wait[i], end, DollStyle.Stand, null, PropKind.None, KidWalk);
            // the first kid scratches the grid with a stick: kneeling along it
            var kneel = Vector2.Lerp(start, end, 0.5f) + side * 0.06f;
            float wk = WalkTime(wait[0], kneel, KidWalk);
            s.Part(0, t, t + wk, kneel, Vector2.Lerp(start, end, 0.5f), DollStyle.Stand, null, PropKind.Stick, KidWalk);
            for (int i = 1; i < n; i++) s.Part(i, t, t + wk + 2f, wait[i], kneel, DollStyle.Look);
            t += wk;
            s.Part(0, t, t + 2f, kneel, Vector2.Lerp(start, end, 0.5f), DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                KneelPose(ref p, u, l); p.armR = new Vector3(55f, 0f, 10f + 25f * Mathf.Sin(u * 7f)); p.yaw += 25f * Mathf.Sin(u * 1.6f);
            }, PropKind.Stick);
            s.a = t + 1.9f;
            Do(s, s.a, VillageFx.Grid, start, 0f, -1, Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg);
            t += 2f;
            float wb = WalkTime(kneel, wait[0], KidWalk);
            s.Part(0, t, t + wb, wait[0], end, DollStyle.Stand, null, PropKind.None, KidWalk);
            t += wb;
            for (int go = 0; go < Mathf.Min(2, n); go++)
            {
                int h = go;
                var from = wait[h];
                if (go > 0) { s.Part(h, t, t + 0.6f, start - along * 0.05f, end, DollStyle.Stand, null, PropKind.None, KidWalk); from = start - along * 0.05f; t += 0.6f; }
                // hop out and back: 6 squares, 2.5 hops a second
                float hops = 6f, hopT = 1f / 2.5f;
                float leg = hops * hopT;
                CampPoseFn hop = (ref DollPose p, float u, float l) => { p.offset.y += 0.03f * Mathf.Abs(Mathf.Sin(u * 2.5f * Mathf.PI)); p.armL = new Vector3(30f, 0f, -40f); p.armR = new Vector3(30f, 0f, 40f); };
                var outP = s.Part(h, t, t + leg, end - along * (VillageGeo.Square * 0.5f), end + along, DollStyle.Stand, hop, PropKind.None, (VillageGeo.GridLen) / leg * 1.05f); outP.poseWalking = true;
                s.Part(h, t + leg, t + leg + 0.35f, end - along * (VillageGeo.Square * 0.5f), start, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += 0.03f * Bump(u, 0f, l); });
                var backP = s.Part(h, t + leg + 0.35f, t + 2f * leg + 0.35f, start - along * 0.05f, start - along, DollStyle.Stand, hop, PropKind.None, (VillageGeo.GridLen) / leg * 1.05f); backP.poseWalking = true;
                float hopEnd = t + 2f * leg + 0.35f;
                for (int i = 0; i < n; i++) if (i != h) s.Part(i, t, hopEnd, wait[i], Vector2.Lerp(start, end, 0.5f), DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float c = Mathf.Abs(Mathf.Sin(u * 2.2f * Mathf.PI)); p.armL = new Vector3(80f, 0f, Mathf.Lerp(-34f, -2f, c)); p.armR = new Vector3(80f, 0f, Mathf.Lerp(34f, 2f, c));
                });
                s.Part(h, hopEnd, hopEnd + 0.8f, start - along * 0.05f, start - along, DollStyle.Stand, ArmsUpPose);
                for (int i = 0; i < n; i++) if (i != h) s.Part(i, hopEnd, hopEnd + 0.8f, wait[i], start, DollStyle.Laugh);
                if (go > 0) { s.Part(h, hopEnd + 0.8f, hopEnd + 1.4f, wait[h], start, DollStyle.Stand, null, PropKind.None, KidWalk); }
                t = hopEnd + (go > 0 ? 1.4f : 0.8f);
            }
            for (int i = 0; i < n; i++)
            {
                var away = wait[i] + side * 0.3f - along * 0.1f * i;
                Go(s, i, t, 1.2f, away, away + side, KidWalk);
            }
            return s;
        }

        // ================================================================== B58.g stopping to talk (B50.b's meet, in a street)
        /// <summary>B58.g: two walkers who meet in a street stop and chat (B7.a) 4–6 s, then go on: B50.b's scene as the street's use.</summary>
        public static CampScene StreetChat(int a, int b, Vector2 aAt, Vector2 bAt, Vector2 meet, Vector2 along, float chatFor, float t0)
        {
            var s = CampScenes5b.PathChat(a, b, aAt, bAt, meet, along, chatFor, t0);
            s.use = CampUse.StreetChat;
            s.a = s.parts[0].Count > 1 ? s.parts[0][1].from : 0.5f; // they stop and talk
            return s;
        }

        // ================================================================== B58.j fixing a fence
        /// <summary>B58.j: a grown-up kneels by a leaning post (<paramref name="post"/>, leaning toward <paramref name="outward"/>), pushes it straight
        /// (lean 20, a push back 0.4 s: <see cref="VillageFx.PostStraight"/>) and taps it in (the hammer, 3 taps, <c>Chips</c>,
        /// <see cref="VillageFx.PostTap"/>). <see cref="CampScene.a"/>: straight; <see cref="CampScene.b"/>: the last tap.</summary>
        public static CampScene FenceFix(int who, Vector2 at, Vector2 post, Vector2 outward, Vector2 toCam, float t0)
        {
            outward = Unit(outward, Vector2.down);
            var s = new CampScene(CampUse.FenceFix, t0, post) { holdFocus = true, p0 = post, p1 = outward };
            int m = s.Add(who);
            var kneel = post + outward * 0.075f;
            float t = WalkTime(at, kneel, Walk);
            s.Part(m, 0f, t, kneel, post, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.5f, kneel, post, DollStyle.Stand, KneelPose); t += 0.5f;
            // the push: both hands on the post, lean in, then back
            s.Part(m, t, t + 1.2f, kneel, post, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                KneelPose(ref p, 1f, l);
                float push = In(u, 0.1f, 0.6f) * (1f - In(u, 0.8f, 0.4f));
                p.lean += 20f * push; p.armL = new Vector3(85f, 0f, -12f); p.armR = new Vector3(85f, 0f, 12f);
            });
            s.a = t + 0.7f;
            Do(s, s.a, VillageFx.PostStraight, post);
            t += 1.2f;
            s.Part(m, t, t + 0.4f, kneel, post, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, 1f, l); p.lean -= 8f * Bump(u, 0f, l); }, PropKind.Hammer); t += 0.4f;
            s.Part(m, t, t + 1.8f, kneel, post, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                KneelPose(ref p, 1f, l);
                float ph = Mathf.Repeat(u / 0.6f, 1f);
                p.armR = new Vector3(ph < 0.5f ? Mathf.Lerp(70f, 125f, Smooth(ph * 2f)) : Mathf.Lerp(125f, 70f, Smooth((ph - 0.5f) * 2f)), 0f, 8f);
                p.armL = new Vector3(70f, 0f, -12f);
            }, PropKind.Hammer);
            for (int i = 0; i < 3; i++) { s.Fx(t + 0.3f + i * 0.6f, CampFxKind.Chips, G(post, VillageGeo.FenceTop + 0.04f), 0.4f, 3); Do(s, t + 0.3f + i * 0.6f, VillageFx.PostTap, post); }
            s.b = t + 1.5f;
            t += 1.8f;
            s.Part(m, t, t + 0.8f, kneel, post, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, 1f - In(u, 0f, l), l); NodPose(ref p, u, l); }, PropKind.Hammer); t += 0.8f;
            Go(s, m, t, 1.2f, kneel + Unit(toCam, outward) * 0.3f, kneel + toCam, Walk);
            return s;
        }

        // ================================================================== B58.k leaning on and climbing the fences
        /// <summary>B58.k (as B54.w): two kids climb the field's fence at <paramref name="rail"/> and sit on its top rail swinging their legs; a grown-up
        /// (−1: none) leans on the rail beside them, arms on it (x 70, lean 20), watching the field. <see cref="CampScene.a"/>: sat.</summary>
        public static CampScene FenceLean(int kidA, Vector2 atA, int kidB, Vector2 atB, int grown, Vector2 grownAt, Vector2 rail, Vector2 along, Vector2 inward, float sitFor, float t0)
        {
            along = Unit(along, Vector2.right); inward = Unit(inward, Vector2.up);
            var s = new CampScene(CampUse.FenceLean, t0, rail) { holdFocus = true };
            var kids = new List<(int m, Vector2 seat, Vector2 at)>();
            if (kidA != Nobody) kids.Add((s.Add(kidA), rail - along * 0.05f, atA));
            if (kidB != Nobody) kids.Add((s.Add(kidB), rail + along * 0.05f, atB));
            int g = grown != Nobody ? s.Add(grown) : -1;
            var lean = rail + along * 0.17f - inward * 0.055f;
            float t = 0f;
            foreach (var k in kids) t = Mathf.Max(t, WalkTime(k.at, k.seat - inward * 0.06f, KidWalk));
            foreach (var k in kids) s.Part(k.m, 0f, t, k.seat - inward * 0.06f, k.seat, DollStyle.Stand, null, PropKind.None, KidWalk);
            if (g >= 0) s.Part(g, 0f, WalkTime(grownAt, lean, Walk), lean, lean + inward, DollStyle.Stand, null, PropKind.None, Walk);
            float lift = VillageGeo.SeatLift(VillageGeo.FenceTop);
            CampPoseFn climb = (ref DollPose p, float u, float l) => { float k = In(u, 0f, l); p.offset.y += lift * k; p.armL = new Vector3(120f * (1f - k), 0f, -12f); p.armR = new Vector3(120f * (1f - k), 0f, 12f); p.lean += 10f * Bump(u, 0f, l); };
            foreach (var k in kids) { var c = s.Part(k.m, t, t + 0.8f, k.seat, k.seat - inward, DollStyle.Stand, climb, PropKind.None, 0.12f); c.poseWalking = true; }
            t += 0.8f; s.a = t;
            int i = 0;
            foreach (var k in kids) { s.Part(k.m, t, t + sitFor, k.seat, k.seat + inward, DollStyle.Sit, SitOn(VillageGeo.FenceTop, 6f, i * 0.5f)); i++; }
            if (g >= 0)
            {
                float from = WalkTime(grownAt, lean, Walk);
                s.Part(g, from, t + sitFor, lean, lean + inward, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float k = In(u, 0f, 0.5f); p.lean += 20f * k; p.armL = Lerp(p.armL, new Vector3(70f, 0f, -10f), k); p.armR = Lerp(p.armR, new Vector3(70f, 0f, 10f), k);
                });
                Go(s, g, t + sitFor, 1.2f, lean - inward * 0.3f, lean - inward, Walk);
            }
            foreach (var k in kids)
            {
                var down = s.Part(k.m, t + sitFor, t + sitFor + 0.5f, k.seat - inward * 0.06f, k.seat - inward, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.offset.y += lift * (1f - In(u, 0f, l)); }, PropKind.None, 0.2f);
                down.poseWalking = true;
                Go(s, k.m, t + sitFor + 0.5f, 1f, k.seat - inward * 0.35f, k.seat - inward, KidWalk);
            }
            return s;
        }
    }
}
