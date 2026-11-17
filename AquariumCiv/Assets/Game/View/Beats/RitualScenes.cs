// S-ritual (animation-specs B75.b, B77): the camp uses that go with the rituals and the boat: the empty offering basket found in spring,
// the fisher sorting the catch on the upside-down boat (work), the kids' house round it (life). Each is a CampScene with the spec's
// beats and timings; every number is [est] until AnimLab. Pure: Mathf, Vector2/3, DollPose and the beat kit.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;

namespace AquariumCiv.View
{
    public static class RitualScenes
    {
        const float Walk = CampScenes1.Walk, KidWalk = CampScenes1.KidWalk, KidRun = CampScenes1.KidRun;
        static float WalkTime(Vector2 a, Vector2 b, float speed) => CampScenes5.WalkTime(a, b, speed);
        static Vector2 Across(Vector2 toCam) => new Vector2(-toCam.y, toCam.x);

        /// <summary>The hull's top over the ground (the boat lies upside down, flat; ConsequenceMeshes.KenneyBoat × the prop scale) [est].</summary>
        public const float HullTop = 0.12f; // measured: the Kenney hull is ~0.1 mesh u high before the prop's 1.5 scale, a sitter on its end sits a little lower than the keel
        /// <summary>B77.a: the sort's length (SortBasket, as built).</summary>
        public const float SortFor = 4f;

        // ================================================================== B75.b the empty basket in spring
        /// <summary>B75.b: the first kid past the tree lifts the cloth, freezes, lifts the empty basket high and runs to the fire with it held up;
        /// 2-3 grown-ups there look up at the glass and one nods slowly. Then the lead takes it and carries it back to the tree with the careful
        /// walk and sets it down at its foot (the 2026-11-16 note: its home is the tree). <see cref="CampScene.a"/>: the kid lifts it (the basket
        /// leaves the tree), <see cref="CampScene.b"/>: the lead sets it back down; <see cref="CampScene.p0"/>: the basket's spot.</summary>
        public static CampScene OfferingEmpty(int kid, Vector2 kidAt, IList<int> grown, IList<Vector2> grownAt, Vector2 basket, Vector2 fire, Vector2 toCam, float t0)
        {
            toCam = CampScenes5.Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.OfferingEmpty, t0, (basket + fire) * 0.5f) { followReach = 2.5f, p0 = basket }; // the camera keeps on the kid (its run is the beat)
            int k = s.Add(kid);
            var peek = basket + toCam * 0.1f + acr * 0.04f;
            float t = WalkTime(kidAt, peek, KidWalk);
            s.Part(k, 0f, t, peek, basket, DollStyle.Stand, null, PropKind.None, KidWalk);
            // the cloth lifted (arm x 80, lean 20), the freeze, the empty basket lifted high
            s.Part(k, t, t + 0.6f, peek, basket, DollStyle.Stand, (ref DollPose p, float u, float l) => { float q = In(u, 0f, 0.25f); p.armR = Lerp(p.armR, new Vector3(80f, 0f, 6f), q); p.lean += 20f * q; });
            s.Part(k, t + 0.6f, t + 1.1f, peek, basket, DollStyle.Freeze); // nothing in it
            s.a = t + 1.1f;
            s.Fx(s.a, CampFxKind.Prop, Vector3.zero, 5f, (int)PropFx.Look); // the basket leaves the tree (in the kid's hands)
            CampPoseFn high = (ref DollPose p, float u, float l) => { float q = In(u, 0f, 0.25f); p.armL = Lerp(p.armL, new Vector3(150f, 0f, -12f), q); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 12f), q); };
            s.Part(k, s.a, s.a + 1f, peek, basket + toCam, DollStyle.Stand, high).poseWalking = true;
            var atFire = fire + toCam * 0.32f + acr * 0.05f;
            float run = WalkTime(peek, atFire, KidRun);
            var r = s.Part(k, s.a + 1f, s.a + 1f + run, atFire, fire, DollStyle.Stand, high, PropKind.None, KidRun); r.run = true; r.poseWalking = true;
            float there = s.a + 1f + run;
            s.c = there; // the run's end: the grown-ups look up
            s.Part(k, there, there + 2.2f, atFire, atFire + toCam, DollStyle.Stand, high);
            // the grown-ups at the fire: look up at the glass, one nods slowly
            int n = Mathf.Min(3, grown.Count);
            var gm = new int[n]; var spots = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                gm[i] = s.Add(grown[i]);
                spots[i] = fire + CampScenes5.Rot(toCam, -60f + 60f * i) * 0.3f;
                float w = WalkTime(grownAt[i], spots[i], Walk);
                s.Part(gm[i], 0f, Mathf.Max(w, there - 0.3f), spots[i], fire, DollStyle.Sit);
                s.Part(gm[i], Mathf.Max(w, there - 0.3f), there + 1.2f, spots[i], spots[i] + toCam, DollStyle.LookUp);
                if (i == 0) s.Part(gm[i], there + 1.2f, there + 2.4f, spots[i], atFire, DollStyle.Sit, (ref DollPose p, float u, float l) => { Seated(ref p); p.lean += 10f * Bump(u, 0f, 0.6f) + 10f * Bump(u, 0.6f, 0.6f); }); // B7.a's nod, slowly
                else s.Part(gm[i], there + 1.2f, there + 2.4f, spots[i], fire, DollStyle.Sit);
            }
            // the lead (the first grown-up) takes it and carries it back to the tree's foot, the careful walk, and sets it down
            float tk = there + 2.4f;
            if (n > 0)
            {
                var hand = atFire + acr * 0.08f;
                s.Part(gm[0], tk, tk + 0.8f, hand, atFire, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(k, tk, tk + 0.8f, atFire, hand, DollStyle.Stand, (ref DollPose p, float u, float l) => { float q = 1f - In(u, 0.3f, 0.3f); p.armL = new Vector3(150f * q + 70f * (1f - q), 0f, -12f); p.armR = new Vector3(150f * q + 70f * (1f - q), 0f, 12f); });
                s.d = tk + 0.6f; // the hand-over: the basket is in the lead's arms from here
                CampPoseFn careful = (ref DollPose p, float u, float l) => { p.armL = new Vector3(90f, 0f, -12f); p.armR = new Vector3(90f, 0f, 12f); p.offset.y *= 0.15f; p.lean -= 5f; };
                var set = basket + toCam * 0.1f;
                float back = WalkTime(hand, set, Walk * 0.75f);
                s.Part(gm[0], tk + 0.8f, tk + 0.8f + back, set, basket, DollStyle.Stand, careful, PropKind.None, Walk * 0.75f).poseWalking = true;
                float down = tk + 0.8f + back;
                s.Part(gm[0], down, down + 1.4f, set, basket, DollStyle.Stand, (ref DollPose p, float u, float l) => { CampScenes5.KneelPose(ref p, u, l); float q = In(u, 0.4f, 0.5f); p.armL = Lerp(new Vector3(90f, 0f, -12f), new Vector3(40f, 0f, -12f), q); p.armR = Lerp(new Vector3(90f, 0f, 12f), new Vector3(40f, 0f, 12f), q); });
                s.b = down + 1f;
                s.Fx(s.b, CampFxKind.Prop, Vector3.zero, 4f, (int)PropFx.Look); // back at the tree, empty, the cloth flat
                CampScenes5.Go(s, gm[0], down + 1.4f, 1f, set + toCam * 0.3f, set + toCam, Walk);
                CampScenes5.Go(s, k, tk + 0.8f, 1f, atFire + toCam * 0.25f, atFire + toCam, KidWalk);
                for (int i = 1; i < n; i++) s.Part(gm[i], tk, down + 1.4f, spots[i], fire, DollStyle.Sit);
            }
            else
            {
                s.d = -1f;
                s.b = there + 2.2f; // nobody to carry it back: the kid sets it down by the fire's ring, the lead fetches it later [est]
                s.Fx(s.b, CampFxKind.Prop, Vector3.zero, 4f, (int)PropFx.Look);
                CampScenes5.Go(s, k, there + 2.2f, 1f, atFire + toCam * 0.25f, atFire + toCam, KidWalk);
            }
            return s;
        }

        // ================================================================== B77.a sort the catch on the boat (work)
        /// <summary>B77.a: a fisher back from fishing lays the rod along the hull (arm x 120 → 90, the rod rests on the keel), sits on the hull's end,
        /// sorts the catch from the fish basket (SortBasket, 4 s), takes the rod and goes on to the rack. <see cref="CampScene.a"/>: the rod laid,
        /// <see cref="CampScene.b"/>: the rod taken back.</summary>
        public static CampScene BoatSort(int who, Vector2 at, Vector2 boat, Vector2 along, Vector2 toCam, Vector2 rack, float t0)
        {
            toCam = CampScenes5.Unit(toCam, Vector2.down); along = CampScenes5.Unit(along, Vector2.right);
            var s = new CampScene(CampUse.BoatSort, t0, boat) { followReach = 2f, p0 = boat, p1 = along };
            int m = s.Add(who);
            var side = Vector2.Dot(toCam, new Vector2(-along.y, along.x)) >= 0f ? new Vector2(-along.y, along.x) : new Vector2(along.y, -along.x); // the hull's camera side
            var lay = boat + side * 0.09f;
            float t = WalkTime(at, lay, Walk);
            s.Part(m, 0f, t, lay, boat, DollStyle.Stand, null, PropKind.Rod, Walk);
            s.Part(m, t, t + 0.7f, lay, boat, DollStyle.Stand, (ref DollPose p, float u, float l) => { float q = In(u, 0f, 0.3f); p.armR = Lerp(new Vector3(120f, 0f, 8f), new Vector3(90f, 0f, 8f), q); p.lean += 14f * q; }, PropKind.Rod);
            s.a = t + 0.55f;
            s.Fx(s.a, CampFxKind.Prop, Vector3.zero, 1f, (int)PropFx.Jolt);
            t += 0.7f;
            // to the hull's end and sit on it (+0.04 u), the basket in the left hand, the right sorts
            var end = boat + along * 0.13f + side * 0.02f;
            s.Part(m, t, t + 0.7f, end, end + side, DollStyle.Stand, null, PropKind.FishBasket, Walk);
            CampPoseFn sitHull = (ref DollPose p, float u, float l) => { p.lift += HullTop; };
            s.Part(m, t + 0.7f, t + 1.1f, end, end + side, DollStyle.Sit, sitHull, PropKind.FishBasket);
            t += 1.1f;
            s.Part(m, t, t + SortFor, end, end + side, DollStyle.SortBasket, sitHull, PropKind.FishBasket);
            t += SortFor;
            s.Part(m, t, t + 0.5f, end, end + side, DollStyle.Stand, null, PropKind.FishBasket);
            s.Part(m, t + 0.5f, t + 1.3f, lay, boat, DollStyle.Stand, (ref DollPose p, float u, float l) => { float q = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 8f), q); p.lean += 14f * q; }, PropKind.FishBasket, Walk);
            s.b = t + 1.1f;
            t += 1.3f;
            CampScenes5.Go(s, m, t, WalkTime(lay, rack, Walk), rack, rack + toCam, Walk).prop = PropKind.FishBasket;
            return s;
        }

        // ================================================================== B77.b in rain: the kids crouch in the boat's lee (B54.d's huddle)
        /// <summary>B77.b in rain: the kids run to the boat and crouch pressed together against the hull on its camera side (B54.d's rain
        /// huddle: shoulders up, hands in front, looking out now this way, now that), drops running off the keel, 8 s; then they run off.
        /// <see cref="CampScene.kind"/> = 1.</summary>
        public static CampScene BoatLee(IList<int> kids, IList<Vector2> at, Vector2 boat, Vector2 along, Vector2 toCam, float t0)
        {
            toCam = CampScenes5.Unit(toCam, Vector2.down); along = CampScenes5.Unit(along, Vector2.right);
            var s = new CampScene(CampUse.BoatHouse, t0, boat) { holdFocus = true, kind = 1 };
            var near = Vector2.Dot(toCam, new Vector2(-along.y, along.x)) >= 0f ? new Vector2(-along.y, along.x) : new Vector2(along.y, -along.x);
            int n = Mathf.Min(3, kids.Count);
            var ms = new int[n]; var spots = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(kids[i]);
                spots[i] = boat + near * 0.15f + along * ((i - (n - 1) * 0.5f) * 0.06f); // pressed against the hull, 0.06 apart
                float w = WalkTime(at[i], spots[i], KidRun);
                s.Part(ms[i], 0f, w, spots[i], spots[i] + near, DollStyle.Stand, null, PropKind.None, KidRun).run = true;
                t = Mathf.Max(t, w);
            }
            s.a = t;
            for (int i = 0; i < n; i++)
            {
                float ph = i * 0.31f;
                s.Part(ms[i], WalkTime(at[i], spots[i], KidRun), t + CampScenes6.HuddleFor, spots[i], spots[i] + near, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    p.squash = Mathf.Lerp(p.squash, 0.8f, In(u, 0f, 0.3f)); p.offset.y -= 0.01f * In(u, 0f, 0.3f); // crouched in its lee
                    p.armL = new Vector3(30f, 0f, -4f); p.armR = new Vector3(30f, 0f, 4f);
                    p.yaw += 12f * Mathf.Sin((u + ph) * 0.9f);
                });
            }
            for (int k = 0; k < 16; k++) s.Fx(t + 0.3f + k * 0.45f, CampFxKind.Droplets, CampScenes5.G(boat + near * 0.08f + along * ((k % 4 - 1.5f) * 0.07f), HullTop), 0.6f, 3); // drops off the hull
            t += CampScenes6.HuddleFor;
            for (int i = 0; i < n; i++) CampScenes5.Go(s, ms[i], t, 1.2f, spots[i] + near * 0.35f + along * ((i - 1) * 0.15f), spots[i] + near, KidRun, true);
            return s;
        }

        // ================================================================== B77.b the kids' house (life)
        /// <summary>B77.b: one kid crouches behind the far side; another walks up and knocks on the hull (two taps, the boat jolts); the hidden one
        /// pops up over the keel and waves them round; they sit on it side by side, swinging, 6-10 s. A third kid, if there is one, sits with them.</summary>
        public static CampScene BoatHouse(IList<int> kids, IList<Vector2> at, Vector2 boat, Vector2 along, Vector2 toCam, float t0)
        {
            toCam = CampScenes5.Unit(toCam, Vector2.down); along = CampScenes5.Unit(along, Vector2.right);
            var s = new CampScene(CampUse.BoatHouse, t0, boat) { holdFocus = true };
            var near = Vector2.Dot(toCam, new Vector2(-along.y, along.x)) >= 0f ? new Vector2(-along.y, along.x) : new Vector2(along.y, -along.x);
            int hide = s.Add(kids[0]), knock = s.Add(kids[1]);
            var behind = boat - near * 0.1f;
            var front = boat + near * 0.1f;
            float th = WalkTime(at[0], behind, KidWalk), tk = WalkTime(at[1], front, KidWalk);
            float t = Mathf.Max(th + 0.8f, tk);
            s.Part(hide, 0f, th, behind, boat, DollStyle.Stand, null, PropKind.None, KidRun).run = true;
            CampPoseFn crouch = (ref DollPose p, float u, float l) => { p.squash *= 0.7f; p.offset.y -= 0.012f; p.lean += 15f; };
            s.Part(hide, th, t + 1.4f, behind, boat, DollStyle.Stand, crouch);
            s.Part(knock, 0f, tk, front, boat, DollStyle.Stand, null, PropKind.None, KidWalk);
            if (tk < t) s.Part(knock, tk, t, front, boat, DollStyle.Stand);
            // the knock: arm x 90, two taps; the boat jolts on each
            s.Part(knock, t, t + 1.2f, front, boat, DollStyle.Knock);
            s.Fx(t + 0.35f, CampFxKind.Prop, Vector3.zero, 1f, (int)PropFx.Jolt);
            s.Fx(t + 0.75f, CampFxKind.Prop, Vector3.zero, 1f, (int)PropFx.Jolt);
            t += 1.4f;
            // the head pops up over the keel and waves them round
            s.a = t;
            s.Part(hide, t, t + 1.4f, behind, front, DollStyle.Wave, (ref DollPose p, float u, float l) => { p.offset.y += 0.04f * In(u, 0f, 0.15f); });
            s.Part(knock, t, t + 1.4f, front, boat, DollStyle.Laugh);
            t += 1.4f;
            // they sit on it side by side, swinging (roll ±5° at 1 Hz), 8 s
            const float sitFor = 8f;
            var seatA = boat + along * 0.05f; var seatB = boat - along * 0.05f;
            s.Part(hide, t, t + 0.8f, seatA, seatA + near, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(knock, t, t + 0.8f, seatB, seatB + near, DollStyle.Stand, null, PropKind.None, KidWalk);
            CampPoseFn swing(float ph) => (ref DollPose p, float u, float l) => { p.lift += HullTop; p.roll += 5f * Mathf.Sin((u + ph) * Mathf.PI * 2f); };
            s.Part(hide, t + 0.8f, t + 0.8f + sitFor, seatA, seatA + near, DollStyle.Sit, swing(0f));
            s.Part(knock, t + 0.8f, t + 0.8f + sitFor, seatB, seatB + near, DollStyle.Sit, swing(0.35f));
            if (kids.Count > 2)
            {
                int third = s.Add(kids[2]);
                var seatC = boat + along * 0.15f;
                s.Part(third, 0f, t + 0.8f, seatC, seatC + near, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(third, t + 0.8f, t + 0.8f + sitFor, seatC, seatC + near, DollStyle.Sit, swing(0.7f));
                CampScenes5.Go(s, third, t + 0.8f + sitFor, 1f, seatC + near * 0.3f, seatC + near, KidRun, true);
            }
            t += 0.8f + sitFor;
            CampScenes5.Go(s, hide, t, 1f, seatA + near * 0.3f + along * 0.1f, seatA + near, KidRun, true);
            CampScenes5.Go(s, knock, t, 1f, seatB + near * 0.3f - along * 0.1f, seatB + near, KidRun, true);
            return s;
        }
    }
}
