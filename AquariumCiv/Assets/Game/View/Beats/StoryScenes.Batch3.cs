// #722 batch 3 (docs/design/new-events.md §1.4, §2.1, §2.3, §2.4, §3.2, §3.3; docs/design/animation-specs.md B80.b-h): the last story events
// as camp scenes, pure builders with the beat kit (CampScene parts, effects, tips), as StoryScenes.cs. B80.b the antlers carried from the old
// post to the new door; B80.c the tinkerer's spout (the mend beat at the fire, a try at the pond, the pour goes left); B80.e the gardener's ring
// of flowers round the name stone (and the morning water, a life use); B80.f the joker's grin on a log's end (the marks' strokes, two kids peek)
// and the kids' pat as they pass; B80.g the dry spell's bucket to the Keeper's tree (the others look up for rain) and its evening repeat; B80.h
// the first snow (a flake on the tongue, the others copy, a snow figure rolled by the fire). B80.d (the song) is the ritual's own moment: no
// builder. Every number is [est] until the clip.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    public static partial class StoryScenes
    {
        // ================================================================== shared: a bucket filled at the pond, carried, poured
        /// <summary>The bucket's kinds, plain or the tinkerer's crooked one (2.1).</summary>
        static PropKind Full(bool spout) => spout ? PropKind.SpoutBucket : PropKind.Bucket;
        static PropKind Empty(bool spout) => spout ? PropKind.SpoutBucketEmpty : PropKind.BucketEmpty;

        /// <summary>Carrying a full bucket (lean −4, the arm out a little, a sway): B34.a's carry.</summary>
        static void CarryBucket(ref DollPose p, float t, float len) { p.lean -= 4f; p.armR = new Vector3(20f, 0f, 22f); p.roll += Mathf.Sin(t * 6f) * 2f; }

        /// <summary>The dip at the pond's edge: crouched, the arm out and down (x 58) into the water, held (the bucket fills), up again.</summary>
        static void DipBucket(ref DollPose p, float t, float len)
        {
            float d = In(t, 0f, 0.25f) * (1f - In(t, len - 0.3f, 0.3f));
            p.squash = Mathf.Lerp(1f, 0.76f, d); p.offset.y -= 0.01f * d; p.lean += 30f * d;
            p.armR = Lerp(p.armR, new Vector3(58f, 0f, 8f), d);
        }
        /// <summary>How long a dip takes; the bucket is full this far in.</summary>
        public const float DipFor = 1.4f, FullAt = 0.8f;

        /// <summary>The crooked pour (2.1): <see cref="CampScenes3.PourPose"/> with the bucket rolled to the left (the spout points off to the side).</summary>
        static void PourLeft(ref DollPose p, float t, float len) { CampScenes3.PourPose(ref p, t, len); p.roll -= 10f * In(t, 0f, 0.4f); p.yaw += 8f * In(t, 0f, 0.4f); }

        /// <summary>Where the water lands: at <paramref name="target"/>, or (the crooked bucket) off to the pourer's left of it.</summary>
        public static Vector2 PourLands(Vector2 stand, Vector2 target, bool spout)
        {
            var face = Dir(stand, target, Vector2.up);
            return spout ? stand + Rot(face, 90f) * SpoutSide + face * 0.03f : target;
        }
        /// <summary>2.1: how far to the left of the pourer the crooked bucket's water lands.</summary>
        public const float SpoutSide = 0.1f;

        /// <summary>Walks to the pond's edge with the empty bucket, dips it (droplets and a ring at the dip), stands with it full. Returns when it
        /// is full; <paramref name="t"/> moves on.</summary>
        static float Fill(CampScene s, int m, ref float t, Vector2 from, Vector2 edge, Vector2 toWater, bool spout)
        {
            toWater = Unit(toWater, Vector2.up);
            float w = WalkTime(from, edge, Walk);
            s.Part(m, t, t + w, edge, edge + toWater, DollStyle.Stand, null, Empty(spout), Walk); t += w;
            s.Part(m, t, t + FullAt, edge, edge + toWater, DollStyle.Stand, DipBucket, Empty(spout));
            float full = t + FullAt;
            s.Fx(full - 0.2f, CampFxKind.Droplets, G(edge + toWater * 0.08f, 0.01f), 0.4f, 2);
            s.Fx(full - 0.2f, CampFxKind.Ring, G(edge + toWater * 0.08f, 0.005f), 0.5f, 1);
            s.Part(m, full, t + DipFor, edge, edge + toWater, DollStyle.Stand, (ref DollPose p, float u, float l) => DipBucket(ref p, u + FullAt, DipFor), Full(spout));
            t += DipFor;
            return full;
        }

        /// <summary>Carries the full bucket from <paramref name="from"/> to <paramref name="stand"/>, crouches and tips it over <paramref name="target"/>
        /// (B34.a's pour, 6 droplets; the crooked bucket's land to the left). A wet patch where it lands. Returns the hit (the water lands).</summary>
        static float Pour(CampScene s, int m, ref float t, Vector2 from, Vector2 stand, Vector2 target, bool spout, float pace)
        {
            float w = WalkTime(from, stand, pace);
            s.Part(m, t, t + w, stand, target, DollStyle.Stand, CarryBucket, Full(spout), pace).poseWalking = true; t += w;
            s.Part(m, t, t + 0.3f, stand, target, DollStyle.Stand, CrouchPose, Full(spout)); t += 0.3f;
            s.Part(m, t, t + CampScenes3.PourSeconds + 0.3f, stand, target, DollStyle.Stand, spout ? PourLeft : CampScenes3.PourPose, Full(spout));
            var lands = PourLands(stand, target, spout);
            for (int i = 0; i < 6; i++) s.Fx(t + 0.2f + i * 0.17f, CampFxKind.Droplets, G(Vector2.Lerp(stand, lands, 0.6f), 0.06f), 0.45f, 1);
            float hit = t + CampScenes3.PourSeconds;
            s.Fx(hit, CampFxKind.Mark, G(lands), 0.05f, (int)CampMark.WetPatch);
            t += CampScenes3.PourSeconds + 0.3f;
            return hit;
        }

        /// <summary>#687's sky look: up at the glass for rain (2.6 s), then the head down and a slow shake (two turns of ±12°, 1.4 s).</summary>
        static void SkyLookAt(CampScene s, int m, float t, Vector2 at, Vector2 toCam)
        {
            s.Part(m, t, t + 2.6f, at, at + toCam, DollStyle.LookUp);
            s.Part(m, t + 2.6f, t + 4f, at, at + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.yaw += 12f * Mathf.Sin(u / l * Mathf.PI * 4f); p.lean += 6f * In(u, 0f, 0.3f); });
        }
        public const float SkyLookFor = 4f;

        // ================================================================== B80.b the antlers passed
        /// <summary>B80.b: how far in front of a door's post the carrier stands, how long the lift and the set take, how high the antlers are carried.</summary>
        public const float PostStand = 0.09f, LiftFor = 1.2f, SetFor = 1.2f;

        /// <summary>Both arms up to the post's top (x 150), the hit at 0.7 s (the antlers come off), then down to the carry (x 110).</summary>
        static void LiftOff(ref DollPose p, float t, float len)
        {
            float up = In(t, 0f, 0.4f), down = In(t, 0.75f, 0.4f);
            float x = Mathf.Lerp(Mathf.Lerp(0f, 150f, up), 110f, down);
            p.armL = new Vector3(x, 0f, -14f); p.armR = new Vector3(x, 0f, 14f); p.offset.y += 0.012f * up * (1f - down);
        }

        /// <summary>The antlers carried in front, high (arms x 110).</summary>
        static void CarryHigh(ref DollPose p, float t, float len) { p.armL = new Vector3(110f, 0f, -14f); p.armR = new Vector3(110f, 0f, 14f); p.lean -= 4f; }

        /// <summary>From the carry up onto the post (x 150) at 0.6 s (the hit: they are on it), the arms down.</summary>
        static void SetOn(ref DollPose p, float t, float len)
        {
            float up = In(t, 0f, 0.4f), down = In(t, 0.7f, 0.4f);
            float x = Mathf.Lerp(Mathf.Lerp(110f, 150f, up), 0f, down);
            p.armL = new Vector3(x, 0f, -14f); p.armR = new Vector3(x, 0f, 14f); p.offset.y += 0.012f * up * (1f - down);
        }

        /// <summary>
        /// B80.b: <paramref name="carrier"/> (the old chief, or one of their family) walks to the old post by <paramref name="oldPost"/>, lifts the
        /// antlers off it (both arms up, the hit <see cref="CampScene.a"/>: they leave the post), carries them high in front to the new chief's
        /// door (<paramref name="newPost"/>) and sets them on the post there (<see cref="CampScene.b"/>: on it). <paramref name="nu"/> (the new
        /// chief; Nobody: none drawn) comes to their door and watches, nods as they go up; the two nod to each other, then go. The old post stays
        /// empty. <see cref="CampScene.p0"/>, <see cref="CampScene.p1"/>: the two posts.
        /// </summary>
        public static CampScene AntlersPass(int carrier, Vector2 carrierAt, int nu, Vector2 nuAt, Vector2 oldPost, Vector2 newPost, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.AntlersPass, t0, (oldPost + newPost) * 0.5f) { followReach = 2.5f, p0 = oldPost, p1 = newPost };
            int c = s.Add(carrier);
            var oldStand = oldPost + toCam * PostStand; var newStand = newPost + toCam * PostStand;
            float t = WalkTime(carrierAt, oldStand, Walk);
            s.Part(c, 0f, t, oldStand, oldPost, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(c, t, t + 0.6f, oldStand, oldPost, DollStyle.Look); t += 0.6f; // a last look at them there
            s.Part(c, t, t + LiftFor, oldStand, oldPost, DollStyle.Stand, LiftOff);
            s.a = t + 0.7f;
            s.Fx(s.a, CampFxKind.Dust, G(oldPost, 0.2f), 0.25f, 2);
            t += LiftFor;
            float w = WalkTime(oldStand, newStand, Walk * 0.85f);
            s.Part(c, t, t + w, newStand, newPost, DollStyle.Stand, CarryHigh, PropKind.None, Walk * 0.85f).poseWalking = true; t += w;
            s.Part(c, t, t + SetFor, newStand, newPost, DollStyle.Stand, SetOn);
            s.b = t + 0.6f;
            s.Fx(s.b, CampFxKind.Dust, G(newPost, 0.2f), 0.3f, 2);
            t += SetFor;
            // the new chief at their door, beside the post
            int n = -1;
            var nuStand = newPost + toCam * 0.13f + acr * 0.14f;
            if (nu != Nobody)
            {
                n = s.Add(nu);
                float wn = Mathf.Min(WalkTime(nuAt, nuStand, Walk), Mathf.Max(0.4f, s.b - 1.5f));
                s.Part(n, 0f, wn, nuStand, newStand, DollStyle.Stand, null, PropKind.None, Walk);
                if (wn < s.b - 0.2f) s.Part(n, wn, s.b - 0.2f, nuStand, newStand, DollStyle.Look);
                s.Part(n, s.b - 0.2f, t, nuStand, newPost, DollStyle.Stand, NodPose);
            }
            // face to face, a nod each, then they go
            s.Part(c, t, t + 1f, newStand, n >= 0 ? nuStand : newPost, DollStyle.Stand, NodPose);
            if (n >= 0) s.Part(n, t, t + 1f, nuStand, newStand, DollStyle.Stand, NodPose);
            t += 1f;
            s.c = t;
            Go(s, c, t, 1.4f, newStand + toCam * 0.3f - acr * 0.25f, newStand + toCam, Walk);
            if (n >= 0) s.Part(n, t, t + 1.4f, nuStand, newPost, DollStyle.Look); // looks at them a while longer
            return s;
        }

        // ================================================================== B80.c the tinkerer's bucket
        /// <summary>B80.c: the mend at the fire (ChoreBeats' Mend, seated), how far the watchers stand to the right.</summary>
        public const float SpoutMendFor = 4f, WatchRight = 0.15f;
        /// <summary>#779: how far apart the watchers stand along the shore (a body width and a little).</summary>
        public const float WatchStep = 0.12f;

        /// <summary>
        /// B80.c: <paramref name="who"/> sits by the fire and bends a spout onto a bucket (ChoreBeats' <see cref="DollStyle.Mend"/>, seated, the
        /// bucket in hand; the hit <see cref="CampScene.a"/>: the spout is on, the crooked bucket from then); holds it up to look; walks to the
        /// pond's edge and dips it full (<see cref="CampScene.b"/>); tips it, and the water lands to the left (<see cref="CampScene.c"/>: a wet patch
        /// <see cref="SpoutSide"/> off to the side). Up to two <paramref name="watchers"/> come and stand to the right; at the pour they step further
        /// right, and everyone laughs. Then the tinkerer walks off with the bucket.
        /// </summary>
        public static CampScene TinkererBucket(int who, Vector2 whoAt, IList<int> watchers, IList<Vector2> watchersAt, Vector2 fire, Vector2 edge, Vector2 toWater,
            Vector2 toCam, float t0, StoryRoom room = null)
        {
            toCam = Unit(toCam, Vector2.down); toWater = Unit(toWater, Vector2.up);
            var s = new CampScene(CampUse.TinkererBucket, t0, (fire + edge) * 0.5f) { followReach = 2f };
            int m = s.Add(who);
            var seat = fire + Rot(toCam, 55f) * 0.3f;
            float t = WalkTime(whoAt, seat, Walk);
            s.Part(m, 0f, t, seat, fire, DollStyle.Sit, null, PropKind.BucketEmpty, Walk);
            s.Part(m, t, t + 0.5f, seat, fire, DollStyle.Sit, null, PropKind.BucketEmpty); t += 0.5f;
            s.Part(m, t, t + SpoutMendFor, seat, fire, DollStyle.Mend, null, PropKind.BucketEmpty); // bends the spout on
            for (int i = 0; i < 3; i++) s.Fx(t + 0.6f + i * 1.2f, CampFxKind.Chips, G(seat + Dir(seat, fire, toCam) * 0.06f, 0.04f), 0.2f, 1);
            t += SpoutMendFor;
            s.a = t;
            s.Part(m, t, t + 1f, seat, fire, DollStyle.Sit, (ref DollPose p, float u, float l) => { Seated(ref p); p.armR = Lerp(p.armR, new Vector3(110f, 0f, 10f), In(u, 0f, 0.3f)); }, PropKind.SpoutBucketEmpty); // a look at it
            t += 1f;
            float full = Fill(s, m, ref t, seat, edge, toWater, true);
            s.b = full;
            // the try: tipped where it stands, the water off to the left
            var face = toWater; var right = Rot(face, -90f);
            s.Part(m, t, t + 0.3f, edge, edge + face, DollStyle.Stand, CrouchPose, PropKind.SpoutBucket); t += 0.3f;
            s.Part(m, t, t + CampScenes3.PourSeconds + 0.3f, edge, edge + face, DollStyle.Stand, PourLeft, PropKind.SpoutBucket);
            var lands = PourLands(edge, edge + face, true);
            for (int i = 0; i < 6; i++) s.Fx(t + 0.2f + i * 0.17f, CampFxKind.Droplets, G(Vector2.Lerp(edge, lands, 0.7f), 0.06f), 0.45f, 1);
            s.c = t + CampScenes3.PourSeconds;
            s.Fx(s.c, CampFxKind.Mark, G(lands), 0.05f, (int)CampMark.WetPatch);
            t += CampScenes3.PourSeconds + 0.3f;
            s.Part(m, t, t + 1f, edge, lands, DollStyle.Look, null, PropKind.SpoutBucketEmpty); t += 1f; // looks where it went
            s.Part(m, t, t + 1.4f, edge, edge - face, DollStyle.Laugh, null, PropKind.SpoutBucketEmpty); t += 1.4f;
            float end = t;
            // the watchers, to the right, a step further right at the pour
            // #779: each watcher has a spot of their own along the shore, a body width from the tinkerer's (the pond's edge and where they walk off
            // to), from each other and from anyone else about, and not in the water; their step right and their walk off are free too
            var tinkerEnd = edge - face * 0.35f + right * 0.1f;
            var atSpot = new List<Vector2> { edge, tinkerEnd }; var atFurther = new List<Vector2> { edge, tinkerEnd }; var atAway = new List<Vector2> { edge, tinkerEnd }; // one list a phase: they stand there together
            int nw = Mathf.Min(2, watchers.Count);
            for (int i = 0; i < nw; i++)
            {
                int k = s.Add(watchers[i]);
                var spot = BodySpots.Free(edge + right * (WatchRight + i * WatchStep) - face * 0.06f, right, atSpot, room);
                var further = BodySpots.Free(spot + right * 0.08f, right, atFurther, room);
                var away = BodySpots.Free(further + right * 0.15f - face * 0.2f, right, atAway, room);
                float w = WalkTime(watchersAt[i], spot, Walk), start = Mathf.Max(0f, s.b - w), there = Mathf.Min(start + w, s.c - 0.6f); // there before the pour
                s.Part(k, start, there, spot, edge, DollStyle.Stand, null, PropKind.None, Walk);
                if (there < s.c - 0.6f) s.Part(k, there, s.c - 0.6f, spot, edge, DollStyle.Look);
                s.Part(k, s.c - 0.6f, s.c, further, edge, DollStyle.Stand, null, PropKind.None, 0.3f); // a quick step right
                s.Part(k, s.c, end, further, edge, DollStyle.Laugh);
                Go(s, k, end, 1.2f, away, further - face, Walk);
            }
            Go(s, m, end, 1.4f, tinkerEnd, edge - face, Walk).prop = PropKind.SpoutBucketEmpty;
            return s;
        }

        // ================================================================== B80.e the gardener's flowers
        /// <summary>B80.e: the ring round the name stone: <see cref="LandGeo.RingStones"/> flowers at this radius (clear of the slab's 0.105 u half-width).</summary>
        public const float FlowerRingR = 0.15f, PressFor = 0.6f;
        public static int Flowers => LandGeo.RingStones;

        /// <summary>Flower <paramref name="i"/> of the ring round the stone (box x, z).</summary>
        public static Vector2 FlowerAt(Vector2 stone, int i) => LandGeo.RingSpot(stone, i, FlowerRingR);

        /// <summary>
        /// B80.e: <paramref name="who"/> (the gardener) walks to the name stone with a small bunch (the PickFlowers bunch in hand), kneels outside
        /// the ring at three spots from the camera's side round, and presses two flowers into the ground at each (a press, the hand down, a
        /// grass puff: one <see cref="CampFxKind.Grass"/> per flower, CampLife shows the flowers by them); the first is <see cref="CampScene.a"/>, the
        /// last <see cref="CampScene.b"/>. Then stands, looks at the ring and nods.
        /// </summary>
        public static CampScene GardenerFlowers(int who, Vector2 whoAt, Vector2 stone, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.GardenerFlowers, t0, stone) { holdFocus = true };
            int m = s.Add(who);
            // the pairs of flowers, the camera's side first
            var order = new List<int>();
            for (int i = 0; i < Flowers; i++) order.Add(i);
            order.Sort((x, y) => Vector2.Dot(FlowerAt(stone, y) - stone, toCam).CompareTo(Vector2.Dot(FlowerAt(stone, x) - stone, toCam)));
            float t = 0f; var from = whoAt; bool first = true;
            for (int k = 0; k + 1 < order.Count; k += 2)
            {
                var f0 = FlowerAt(stone, order[k]); var f1 = FlowerAt(stone, order[k + 1]);
                var mid = (f0 + f1) * 0.5f;
                var kneel = stone + Dir(stone, mid, toCam) * (FlowerRingR + 0.07f);
                float w = WalkTime(from, kneel, Walk);
                s.Part(m, t, t + w, kneel, mid, DollStyle.Stand, null, PropKind.None, Walk); t += w;
                s.Part(m, t, t + 0.4f, kneel, mid, DollStyle.Stand, KneelPose); t += 0.4f;
                foreach (var f in new[] { f0, f1 })
                {
                    s.Part(m, t, t + PressFor, kneel, f, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, 1f, l); p.armR = Lerp(p.armR, new Vector3(60f, 0f, 6f), Bump(u, 0f, l)); p.lean += 6f * Bump(u, 0.2f, 0.3f); });
                    float hit = t + PressFor * 0.55f;
                    s.Fx(hit, CampFxKind.Grass, G(f, 0.01f), 0.3f, 1);
                    if (first) { s.a = hit; first = false; }
                    s.b = hit;
                    t += PressFor;
                }
                from = kneel;
            }
            s.Tip(m, Mathf.Max(0f, s.a - 2f), s.b, CampTipKind.Bunch); // the bunch in the hand from the walk in to the last press
            var back = stone + toCam * (FlowerRingR + 0.2f);
            float wb = WalkTime(from, back, Walk);
            s.Part(m, t, t + wb, back, stone, DollStyle.Stand, null, PropKind.None, Walk); t += wb;
            s.Part(m, t, t + 1.4f, back, stone, DollStyle.Look); t += 1.4f;
            s.Part(m, t, t + 0.6f, back, stone, DollStyle.Stand, NodPose); t += 0.6f;
            Go(s, m, t, 1.2f, back + toCam * 0.25f + Across(toCam) * 0.15f, back + toCam, Walk);
            return s;
        }

        /// <summary>B80.e (2.3 "Stays", a life use): in the morning <paramref name="who"/> carries a bucket to the ring and pours a little over the
        /// flowers on the camera's side (B34.a's pour; the crooked bucket's lands to the left, 2.1); looks, nods, goes. <see cref="CampScene.a"/>:
        /// the water lands.</summary>
        public static CampScene FlowerWater(int who, Vector2 whoAt, Vector2 stone, Vector2 toCam, bool spout, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.FlowerWater, t0, stone) { holdFocus = true };
            int m = s.Add(who);
            var target = stone + toCam * FlowerRingR;
            var stand = target + toCam * 0.08f + Across(toCam) * 0.03f;
            float t = 0f;
            s.a = Pour(s, m, ref t, whoAt, stand, target, spout, Walk * 0.85f);
            s.Part(m, t, t + 1f, stand, target, DollStyle.Look, null, Empty(spout)); t += 1f;
            s.Part(m, t, t + 0.6f, stand, target, DollStyle.Stand, NodPose, Empty(spout)); t += 0.6f;
            Go(s, m, t, 1.2f, stand + toCam * 0.3f, stand + toCam, Walk).prop = Empty(spout);
            return s;
        }

        // ================================================================== B80.f the joker's face
        /// <summary>B80.f: how far out from the woodpile's middle the carver stands (the log ends are 0.2 u out) and the kids' step back from them.</summary>
        public const float CarveStand = 0.26f, KidsPeekBack = 0.16f;

        /// <summary>The face log's end (box x, height, z): the top row's end on the camera's side (the pile's logs run along its z).</summary>
        public static Vector3 FaceEnd(Vector2 pile, Vector2 face) { var p = pile + face * (0.13f * ConsequenceMeshes.Scale); return new Vector3(p.x, 0.124f * ConsequenceMeshes.Scale, p.y); }

        /// <summary>
        /// B80.f: <paramref name="joker"/> walks to the woodpile's front and carves a grin into the end of a log (two rounds of B78.b's cut: the arm
        /// x 70, three strokes each, a chip puff each, the cutting stone in hand); the face shows at <see cref="CampScene.a"/>. Up to two
        /// <paramref name="kids"/> come and lean in to watch a step back (Peek, <see cref="KidsPeekBack"/> u from the carver: the blade stays with
        /// the grown-up), and laugh when it shows; the joker steps back and laughs with them (hands on the hips). <paramref name="face"/>: the
        /// pile's end toward the camera.
        /// </summary>
        public static CampScene JokerFace(int joker, Vector2 jokerAt, IList<int> kids, IList<Vector2> kidsAt, Vector2 pile, Vector2 face, float t0)
        {
            face = Unit(face, Vector2.down); var acr = Across(face);
            var s = new CampScene(CampUse.JokerFace, t0, pile) { holdFocus = true };
            int m = s.Add(joker);
            var end = FaceEnd(pile, face);
            var stand = pile + face * CarveStand + acr * 0.03f;
            float t = WalkTime(jokerAt, stand, Walk);
            s.Part(m, 0f, t, stand, pile, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.6f, stand, pile, DollStyle.Look); t += 0.6f; // picks the log
            float cutFrom = t;
            for (int r = 0; r < 2; r++) { Cut(s, m, t, stand, pile, end); t += CutFor; }
            s.a = t - 0.2f;
            var back = stand + face * 0.1f;
            s.Part(m, t, t + 0.5f, back, pile, DollStyle.Stand, null, PropKind.None, 0.2f); t += 0.5f;
            s.Part(m, t, t + 1.6f, back, pile, DollStyle.Laugh, HipsPose); t += 1.6f;
            float done = t;
            Go(s, m, t, 1.2f, back + face * 0.3f - acr * 0.2f, back + face, Walk);
            // the kids, a step back either side, leaning in
            int nk = Mathf.Min(2, kids.Count);
            for (int i = 0; i < nk; i++)
            {
                int k = s.Add(kids[i]);
                var spot = stand + face * KidsPeekBack + acr * (i == 0 ? 0.1f : -0.1f);
                float w = WalkTime(kidsAt[i], spot, KidWalk), start = Mathf.Max(0f, cutFrom - w);
                s.Part(k, start, start + w, spot, new Vector2(end.x, end.z), DollStyle.Stand, null, PropKind.None, KidWalk);
                float there = start + w;
                if (there < s.a) s.Part(k, there, s.a, spot, stand, DollStyle.Peek);
                s.Part(k, Mathf.Max(there, s.a), done, spot, stand, DollStyle.Laugh);
                Go(s, k, done, 1f, spot + face * 0.3f + acr * (i == 0 ? 0.2f : -0.2f), spot + face, KidWalk, true);
            }
            return s;
        }

        /// <summary>B80.f (2.4 "Read later", a life use): a kid passing the woodpile pats the face log (arm x 90, 0.6 s, the hit
        /// <see cref="CampScene.a"/>), grins (Laugh, 0.8 s) and runs on.</summary>
        public static CampScene FacePat(int kid, Vector2 kidAt, Vector2 pile, Vector2 face, float t0)
        {
            face = Unit(face, Vector2.down); var acr = Across(face);
            var s = new CampScene(CampUse.FacePat, t0, pile);
            int k = s.Add(kid);
            var spot = pile + face * 0.24f;
            float t = WalkTime(kidAt, spot, KidWalk);
            s.Part(k, 0f, t, spot, pile, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(k, t, t + 0.6f, spot, pile, DollStyle.Stand, (ref DollPose p, float u, float l) => { float b = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), b); p.lean += 6f * b; });
            s.a = t + 0.3f;
            s.Fx(s.a, CampFxKind.Prop, Vector3.zero, 0f, (int)PropFx.Jolt);
            t += 0.6f;
            s.Part(k, t, t + 0.8f, spot, spot + face, DollStyle.Laugh); t += 0.8f;
            var on = spot + face * 0.1f + (Vector2.Dot(kidAt - pile, acr) >= 0f ? -acr : acr) * 0.5f; // on the way it was going
            Go(s, k, t, 1.2f, on, on + (on - spot), KidRun, true);
            return s;
        }

        // ================================================================== B80.g the dry spell
        /// <summary>
        /// B80.g: <paramref name="who"/> fills a bucket at the pond's edge (<see cref="Fill"/>), carries it to the Keeper's tree and pours it at its
        /// foot (B34.a's pour; the hit <see cref="CampScene.a"/>: a wet patch, the tree's leaves stir); looks up at the crown, nods. Up to two
        /// <paramref name="lookers"/> look up at the glass for rain where they stand (#687's sky look, one after the other), from the fill.
        /// <paramref name="use"/>: <see cref="CampUse.DrySpell"/> (the event) or <see cref="CampUse.TreeWater"/> (the evening repeat, no lookers).
        /// <paramref name="spout"/>: the tinkerer's crooked bucket (2.1), whose water lands to the left.
        /// </summary>
        public static CampScene TreeWater(CampUse use, int who, Vector2 whoAt, Vector2 edge, Vector2 toWater, Vector2 tree, int stage, IList<int> lookers,
            IList<Vector2> lookersAt, Vector2 toCam, bool spout, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(use, t0, (edge + tree) * 0.5f) { followReach = 2f };
            int m = s.Add(who);
            float t = 0f;
            float full = Fill(s, m, ref t, whoAt, edge, toWater, spout);
            var stand = TreeGeo.Touch(tree, stage, Dir(tree, edge, toCam)) + Dir(tree, edge, toCam) * 0.04f;
            var foot = tree + Dir(tree, stand, toCam) * (TreeGeo.TrunkR(stage) + 0.03f);
            s.a = Pour(s, m, ref t, edge, stand, foot, spout, Walk * 0.8f);
            s.Fx(s.a + 0.2f, CampFxKind.Leaves, G(tree, TreeGeo.CrownY(stage)), 0.3f, 1);
            s.Part(m, t, t + 1.2f, stand, tree, DollStyle.LookUp, null, Empty(spout)); t += 1.2f;
            s.Part(m, t, t + 0.6f, stand, tree, DollStyle.Stand, NodPose, Empty(spout)); t += 0.6f;
            Go(s, m, t, 1.2f, stand + toCam * 0.3f, stand + toCam, Walk).prop = Empty(spout);
            int n = use == CampUse.DrySpell ? Mathf.Min(2, lookers.Count) : 0;
            for (int i = 0; i < n; i++)
            {
                int k = s.Add(lookers[i]);
                float at = full + i * 1.6f;
                s.Part(k, 0f, at, lookersAt[i], lookersAt[i] + toCam, DollStyle.Stand);
                SkyLookAt(s, k, at, lookersAt[i], toCam);
            }
            return s;
        }

        // ================================================================== B80.h the first snow
        /// <summary>B80.h: the snow figure's spot by the fire (on the camera's side, a little round), its two balls (base, head), and the roll's length.</summary>
        public const float FigureOut = 0.42f, BaseR = 0.045f, HeadR = 0.03f, RollLen = 0.22f, SnowRollFor = 3.2f;

        /// <summary>Fixed toward the front glass (where the Keeper looks from), so the figure stays put when the camera turns.</summary>
        public static Vector2 FigureSpot(Vector2 fire) => fire + Rot(Vector2.down, 50f) * FigureOut;

        /// <summary>The head back, mouth to the sky (LookUp), and a little hop and snap forward as the flake lands (the hit at 0.3 s).</summary>
        static void TongueCatch(ref DollPose p, float t, float len) { p.lean -= 12f * (1f - In(t, 0.2f, 0.2f)); p.offset.y += 0.015f * Bump(t, 0.05f, 0.3f); p.lean += 6f * Bump(t, 0.3f, 0.3f); }

        /// <summary>Bent over the ball, both hands on it (arms x 60), pushing it along (a shove at 1 Hz).</summary>
        static void RollPose(ref DollPose p, float t, float len)
        {
            float d = In(t, 0f, 0.25f), shove = Bump(Mathf.Repeat(t, 1f), 0f, 1f);
            p.squash = Mathf.Lerp(1f, 0.84f, d); p.lean += (28f + 6f * shove) * d;
            p.armL = Lerp(p.armL, new Vector3(60f, 0f, -8f), d); p.armR = Lerp(p.armR, new Vector3(60f, 0f, 8f), d);
        }

        /// <summary>The snow base <paramref name="u"/> scene seconds in: where it is and how big (it grows as it rolls), or false before the roll.</summary>
        public static bool BaseNow(CampScene s, float u, out Vector2 at, out float r)
        {
            at = s.p1; r = BaseR;
            if (u < s.d) return false;
            float k = Mathf.Clamp01((u - s.d) / Mathf.Max(0.1f, s.b - s.d));
            at = Vector2.Lerp(s.p0, s.p1, k); r = Mathf.Lerp(0.015f, BaseR, k);
            return true;
        }

        /// <summary>
        /// B80.h: <paramref name="kid"/> stops and tips the head back (LookUp, 1.4 s), catches a flake on the tongue (the hit <see cref="CampScene.a"/>)
        /// and laughs; the <paramref name="others"/> (up to three) copy, one after another. Then two of them (the kid and the next) walk by the fire
        /// and roll a small ball of snow along the ground to the figure's spot (bent over, pushing; it grows from the roll's start
        /// <see cref="CampScene.d"/> to <see cref="CampScene.b"/>: in place); the kid lifts the small head onto it (arms up, <see cref="CampScene.c"/>).
        /// Everyone steps back and laughs, the kids hop. <see cref="CampScene.p0"/>, <see cref="CampScene.p1"/>: the roll's start, the figure's spot.
        /// No climbing; the kids keep to the ground.
        /// </summary>
        public static CampScene FirstSnow(int kid, Vector2 kidAt, IList<int> others, IList<Vector2> othersAt, IList<bool> othersKid, Vector2 fire, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var spot = FigureSpot(fire);
            var rollDir = Unit(Rot(toCam, -40f), Vector2.right);
            var start = spot - rollDir * RollLen;
            var s = new CampScene(CampUse.FirstSnow, t0, (kidAt + spot) * 0.5f) { followReach = 1.5f, p0 = start, p1 = spot };
            int k = s.Add(kid);
            s.Part(k, 0f, 0.5f, kidAt, kidAt + toCam, DollStyle.Stand); // stops
            s.Part(k, 0.5f, 1.9f, kidAt, kidAt + toCam, DollStyle.LookUp);
            s.Part(k, 1.9f, 2.5f, kidAt, kidAt + toCam, DollStyle.LookUp, TongueCatch);
            s.a = 2.2f;
            s.Part(k, 2.5f, 3.5f, kidAt, kidAt + toCam, DollStyle.Laugh);
            float t = 3.5f;
            int n = Mathf.Min(3, others.Count);
            var om = new int[n]; float copied = t;
            for (int i = 0; i < n; i++)
            {
                om[i] = s.Add(others[i]);
                float c0 = s.a + 0.5f + i * 0.5f;
                s.Part(om[i], 0f, c0, othersAt[i], kidAt, DollStyle.Look);
                s.Part(om[i], c0, c0 + 1.4f, othersAt[i], othersAt[i] + toCam, DollStyle.LookUp);
                s.Part(om[i], c0 + 1.4f, c0 + 2f, othersAt[i], othersAt[i] + toCam, DollStyle.LookUp, TongueCatch);
                s.Part(om[i], c0 + 2f, c0 + 2.8f, othersAt[i], othersAt[i] + toCam, DollStyle.Laugh);
                copied = Mathf.Max(copied, c0 + 2.8f);
            }
            if (copied > t) s.Part(k, t, copied, kidAt, kidAt + toCam, DollStyle.Laugh);
            t = copied;
            // the roll: the kid and the next one (a kid if there is one) push the ball in from the side
            int helper = -1;
            for (int i = 0; i < n && helper < 0; i++) if (othersKid[i]) helper = i;
            if (helper < 0 && n > 0) helper = 0;
            Vector2 Behind(float side) => start - rollDir * 0.07f + acr * side;
            float wk = WalkTime(kidAt, Behind(0.035f), KidWalk), wh = helper >= 0 ? WalkTime(othersAt[helper], Behind(-0.035f), othersKid[helper] ? KidWalk : Walk) : 0f;
            float go = t;
            s.Part(k, go, go + wk, Behind(0.035f), start, DollStyle.Stand, null, PropKind.None, KidWalk);
            if (helper >= 0) s.Part(om[helper], go, go + wh, Behind(-0.035f), start, DollStyle.Stand, null, PropKind.None, othersKid[helper] ? KidWalk : Walk);
            t = go + Mathf.Max(wk, wh);
            s.d = t;
            float rollSpeed = RollLen / SnowRollFor;
            var r0 = s.Part(k, t, t + SnowRollFor, spot - rollDir * 0.07f + acr * 0.035f, spot, DollStyle.Stand, RollPose, PropKind.None, rollSpeed); r0.poseWalking = true;
            if (helper >= 0) { var r1 = s.Part(om[helper], t, t + SnowRollFor, spot - rollDir * 0.07f - acr * 0.035f, spot, DollStyle.Stand, RollPose, PropKind.None, rollSpeed); r1.poseWalking = true; }
            for (int q = 1; q <= 3; q++) s.Fx(t + q * SnowRollFor / 3f, CampFxKind.Dust, G(Vector2.Lerp(start, spot, q / 3f), 0.005f), 0.25f, 1); // a little powder
            t += SnowRollFor;
            s.b = t;
            // the head: the kid steps round and lifts it on (arms up from the ground: no climbing)
            var headStand = spot + toCam * 0.09f + acr * 0.02f;
            s.Part(k, t, t + 0.6f, headStand, spot, DollStyle.Stand, null, PropKind.None, 0.25f); t += 0.6f;
            s.Part(k, t, t + 0.9f, headStand, spot, DollStyle.Stand, (ref DollPose p, float u, float l) => { float b = Bump(u, 0f, l); p.armL = Lerp(p.armL, new Vector3(120f, 0f, -10f), b); p.armR = Lerp(p.armR, new Vector3(120f, 0f, 10f), b); });
            s.c = t + 0.5f;
            s.Fx(s.c, CampFxKind.Dust, G(spot, BaseR * 2f), 0.3f, 2);
            t += 0.9f;
            // everyone back a step, laughing; the kids hop
            var kb = headStand + toCam * 0.08f;
            s.Part(k, t, t + 0.5f, kb, spot, DollStyle.Stand, null, PropKind.None, 0.25f);
            s.Part(k, t + 0.5f, t + 1.5f, kb, spot, DollStyle.Stand, ClapJumpPose);
            for (int i = 0; i < n; i++)
            {
                var ring = spot + Rot(toCam, (i - 1) * 45f + (i >= 1 ? 30f : -30f)) * 0.2f;
                float from = i == helper ? s.b : Mathf.Max(copied, s.d);
                var fromAt = i == helper ? spot - rollDir * 0.07f - acr * 0.035f : othersAt[i];
                float w = WalkTime(fromAt, ring, othersKid[i] ? KidWalk : Walk);
                s.Part(om[i], from, from + w, ring, spot, DollStyle.Stand, null, PropKind.None, othersKid[i] ? KidWalk : Walk);
                float there = from + w;
                if (there < t) s.Part(om[i], there, t, ring, spot, DollStyle.Look);
                s.Part(om[i], Mathf.Max(there, t), t + 1.5f, ring, spot, othersKid[i] ? DollStyle.Stand : DollStyle.Laugh, othersKid[i] ? ClapJumpPose : null);
            }
            t += 1.5f;
            Go(s, k, t, 1.2f, kb + toCam * 0.3f + acr * 0.15f, kb + toCam, KidWalk);
            return s;
        }
    }
}
