// #692 part 3 (docs/design/animation-specs.md "#692 part 3: grass, saplings, rocks, fallen branches", B33-B36): the uses as pure builders.
// Each takes who and where (box x, z) and returns a CampScene whose timeline follows the spec row's beats and timings. What the uses leave
// on the land (a wet patch, a ring of stones, a twig, a pebble stack, the stick horse leaning, grass bits in the hair, a flower behind the
// ear) rides on the scene as CampFxKind.Mark effects; View/CampLife.Land.cs draws them. Who uses what and when: CampLife.Land.cs.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;

namespace AquariumCiv.View
{
    /// <summary>#692 part 3: the numbers of the things the uses touch (pure).</summary>
    public static class LandGeo
    {
        /// <summary>B35 [est]: the two low flat boulders at the camp's edge (high, wide), and how far out from the fire they lie.</summary>
        public const float RockH = 0.07f, RockW = 0.12f, RockFrom = 0.5f, RockTo = 0.8f, RockApart = 0.35f;
        /// <summary>B34.b: the ring of stones round a sapling (radius, stones), and at most this many rings in the box.</summary>
        public const float RingR = 0.06f;
        public const int RingStones = 6, MaxRings = 4;
        /// <summary>B34.c: at most this many planted twigs in the box (each stays a season).</summary>
        public const int MaxTwigs = 4;
        /// <summary>B35.b: five pebbles a stack, at most three stacks in the box.</summary>
        public const int Pebbles = 5, MaxStacks = 3;
        /// <summary>B34: a use's sapling stands within this of the camp.</summary>
        public const float SaplingReach = 1.5f;

        /// <summary>B35.b: the pebbles get smaller up the stack, 0.02 → 0.012 u across.</summary>
        public static float PebbleSize(int i) => Mathf.Lerp(0.02f, 0.012f, Mathf.Clamp01(i / (float)(Pebbles - 1)));

        /// <summary>The height of a stack of <paramref name="n"/> pebbles (each sits 0.8 of its size on the one under it).</summary>
        public static float StackHeight(int n) { float h = 0f; for (int i = 0; i < n; i++) h += PebbleSize(i) * 0.8f; return h; }

        /// <summary>B34.b: stone <paramref name="i"/>'s spot in the ring round <paramref name="sapling"/>.</summary>
        public static Vector2 RingSpot(Vector2 sapling, int i) => RingSpot(sapling, i, RingR);
        public static Vector2 RingSpot(Vector2 sapling, int i, float r) { float a = i * Mathf.PI * 2f / RingStones + 0.3f; return sapling + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }

        /// <summary>#692 p3 (the user's review: the stones hid under the young tree's crown): the ring lies just outside the drawn crown
        /// (<paramref name="crown"/> u), never smaller than <see cref="RingR"/>, at most 0.18 u.</summary>
        public static float RingFor(float crown) => Mathf.Clamp(crown + 0.03f, RingR, 0.18f);

        /// <summary>B35 [est]: the boulders' spots: two (or fewer) on free ground 0.5–0.8 u from the fire, at least 0.35 u apart, the first toward
        /// the camera's side (so a sitter faces the Keeper), from the box's seed (never shuffled by a rebuild). <paramref name="ok"/>: the box's own room test
        /// (no path, no lot, no water, no prop).</summary>
        public static List<Vector2> Boulders(Vector2 fire, Vector2 toCam, int seed, System.Func<Vector2, bool> ok)
        {
            var got = new List<Vector2>();
            float a0 = Mathf.Atan2(toCam.y, toCam.x) + (BeatHash.U01(seed, 1, 6940) - 0.5f) * 0.8f;
            for (int k = 0; k < 24 && got.Count < 2; k++)
            {
                float a = a0 + (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * 0.55f + (got.Count == 1 ? 1.4f : 0f);
                float r = Mathf.Lerp(RockFrom, RockTo, BeatHash.U01(seed, k, 6941));
                var p = fire + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (got.Count == 1 && (p - got[0]).magnitude < RockApart) continue;
                if (ok != null && !ok(p)) continue;
                got.Add(p);
            }
            return got;
        }
    }

    public static class CampScenes3
    {
        const float Walk = CampScenes1.Walk, KidWalk = CampScenes1.KidWalk, KidRun = CampScenes1.KidRun;
        /// <summary>A part that snaps to a new spot (the offset a pose carried becomes the real spot in one frame).</summary>
        const float Snap = 5f;

        static float WalkTime(Vector2 a, Vector2 b, float speed) => Mathf.Clamp((b - a).magnitude / speed + 0.25f, 0.4f, 9f);
        static Vector2 Dir(Vector2 from, Vector2 to, Vector2 fallback) { var d = to - from; return d.sqrMagnitude > 1e-6f ? d.normalized : fallback; }
        static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        static Vector3 G(Vector2 p, float h = 0f) => new Vector3(p.x, h, p.y);
        static void Mark(CampScene s, float t, CampMark m, Vector2 at, float h = 0f, float scale = 1f) => s.Fx(t, CampFxKind.Mark, G(at, h), scale, (int)m);
        static void PersonMark(CampScene s, float t, CampMark m, int member) => s.Fx(t, CampFxKind.Mark, Vector3.zero, 1f, (int)m, CampAnchor.Head, member);

        // ================================================================== shared poses
        /// <summary>A bend to pick something up from the ground (lean 35, arm x 40 → 60; the grab at 0.4 s).</summary>
        public static void BendPickPose(ref DollPose p, float t, float len)
        {
            float d = In(t, 0f, 0.3f) * (1f - In(t, len - 0.2f, 0.2f));
            p.lean += 35f * d; p.squash = Mathf.Lerp(p.squash, 0.92f, d);
            float g = t < 0.4f ? Mathf.Lerp(40f, 60f, Smooth((t - 0.3f) / 0.1f)) : 60f;
            p.armR = Lerp(p.armR, new Vector3(g, 0f, 8f), d);
        }

        /// <summary>A crouch (squash 0.8, lean 30) with the hand down to the ground (arm x 40): set a stone, a pebble, pat the soil.</summary>
        public static void CrouchSetPose(ref DollPose p, float t, float len)
        {
            float d = In(t, 0f, 0.15f);
            p.squash = Mathf.Lerp(1f, 0.8f, d); p.offset.y -= 0.012f * d; p.lean += 30f * d;
            p.armR = Lerp(p.armR, new Vector3(40f, 0f, 6f), d);
        }

        static void CrouchPose(ref DollPose p, float t, float len) { p.squash = 0.8f; p.offset.y -= 0.012f; p.lean += 22f; }

        static void HugStonesPose(ref DollPose p, float t, float len) { p.armL = new Vector3(60f, 0f, 24f); p.armR = new Vector3(60f, 0f, -24f); p.lean -= 4f; }

        /// <summary>Holding something up to show it (arm x 150).</summary>
        static void HoldUpPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.25f) * (1f - In(t, len - 0.2f, 0.2f)); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 8f), u); }

        static void OfferPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.3f); p.armR = Lerp(p.armR, new Vector3(85f, 0f, 4f), u); p.lean += 6f * u; }

        static void TakePose(ref DollPose p, float t, float len) { float u = Bump(t, 0f, len); p.armR = Lerp(p.armR, new Vector3(75f, 0f, 6f), u); }

        /// <summary>B33.c: the flower tucked behind the ear (the arm up to the side of the head, x 150, z 30).</summary>
        static void TuckPose(ref DollPose p, float t, float len) { float u = Bump(t, 0f, len); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 30f), u); p.roll -= 6f * u; }

        static void NodPose(ref DollPose p, float t, float len) { p.lean += 14f * Bump(t, 0f, Mathf.Min(len, 0.5f)); }

        static void PatPose(ref DollPose p, float t, float len) { float u = Bump(t, 0f, len); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), u); p.lean += 4f * u; }

        // ================================================================== B33.a roll in the grass
        /// <summary>The flop into the roll (0.25 s): squash 0.7, onto the side (roll 90) and over (lean 90): the start of a log roll.</summary>
        public static void FlopPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, len);
            p.squash = Mathf.Lerp(1f, 0.7f, Bump(t, 0f, len * 1.6f)); p.roll = 90f * k; p.lean = 90f * k; p.offset.y = 0.03f * k;
            p.armL = new Vector3(0f, 0f, -10f); p.armR = new Vector3(0f, 0f, 10f);
        }

        /// <summary>Roll <paramref name="r"/> of three: lying on the side (roll 90) the body turns over once about its own length (lean 90 → −270,
        /// 0.4 s), moving 0.05 u sideways (toward the way they face).</summary>
        public static CampPoseFn RollPose(int r) => (ref DollPose p, float t, float len) =>
        {
            float u = Mathf.Clamp01(t / len);
            // #692 p3 (the user's review: it read as lying on the side): the arms stick out in front, so they sweep round with each turn, and
            // the body lifts a little twice a turn (it is not round), so the turning shows from the default camera too
            p.roll = 90f; p.lean = 90f - 360f * u; p.offset.y = 0.03f + 0.008f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)); p.offset.z = RollStep * (r + u); p.squash = 1f;
            p.armL = new Vector3(80f, 0f, -10f); p.armR = new Vector3(80f, 0f, 10f);
        };
        /// <summary>#692 p3: 0.08 u a roll [est] (the spec's 0.05 turned the doll in place: it read as lying still).</summary>
        public const float RollStep = 0.08f, RollSeconds = 0.4f;
        public const int Rolls = 3;

        /// <summary>On the back after the rolls, arms out (z ∓90), giggling (a bob 0.01 u at 9 Hz).</summary>
        public static void GigglePose(ref DollPose p, float t, float len)
        {
            p.roll = 90f; p.lean = 90f; p.offset.y = 0.03f + 0.01f * Mathf.Abs(Mathf.Sin(t * 9f * Mathf.PI)); p.offset.z = RollStep * Rolls;
            p.armL = new Vector3(0f, 0f, -90f); p.armR = new Vector3(0f, 0f, 90f);
        }

        /// <summary>Sitting up from the back (0.5 s) onto the bottom.</summary>
        /// <summary>B33.b: sitting up from lying on the back (0.5 s).</summary>
        static void SitUpBackPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.5f);
            p.lean = -90f * (1f - k); p.offset.y = Mathf.Lerp(0.03f, -0.03f, k); p.squash = Mathf.Lerp(1f, 0.78f, k);
        }

        static void SitUpPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.5f);
            p.roll = 90f * (1f - k); p.lean = 90f * (1f - k); p.offset.y = Mathf.Lerp(0.03f, -0.03f, k); p.squash = Mathf.Lerp(1f, 0.78f, k);
        }

        /// <summary>B33.a: 1–3 kids run to the grass, flop down, roll sideways three times (0.4 s a roll, 0.05 u each, 4 grass blades at each turn),
        /// stop on the back, arms out, giggle 0.8 s, sit up with two green bits in the hair (a mark, until the next meal). Tall grass flattens where
        /// they rolled for 3 s (a <see cref="CampMark.Flatten"/>). <see cref="CampScene.a"/>: the first flop.</summary>
        public static CampScene GrassRoll(IList<int> kids, IList<Vector2> at, Vector2 spot, Vector2 across, float t0)
        {
            across = across.sqrMagnitude > 1e-6f ? across.normalized : Vector2.right;
            var side = new Vector2(-across.y, across.x);
            // #692 p3: the log roll's drawn offset (roll 90, then lean about the body's length) carries the doll toward −across for a doll facing
            // +across; the grass, the flattening, the sit-up and the camera follow where it is drawn (the sit-up jumped back 0.24 u before)
            var rollTo = -across;
            var s = new CampScene(CampUse.GrassRoll, t0, spot + rollTo * RollStep * Rolls * 0.5f) { holdFocus = true };
            int n = kids.Count;
            float first = 99f;
            for (int i = 0; i < n; i++)
            {
                int m = s.Add(kids[i]);
                var start = spot + side * ((i - (n - 1) * 0.5f) * 0.11f);
                var face = start + across;
                float t = 0f, w = WalkTime(at[i], start, KidRun) + 0.5f * i;
                s.Part(m, t, t + w, start, face, DollStyle.Stand, null, PropKind.None, KidRun).run = true; t += w;
                s.Part(m, t, t + 0.25f, start, face, DollStyle.Stand, FlopPose); first = Mathf.Min(first, t); t += 0.25f;
                for (int r = 0; r < Rolls; r++)
                {
                    s.Part(m, t, t + RollSeconds, start, face, DollStyle.Stand, RollPose(r));
                    s.Fx(t + RollSeconds * 0.5f, CampFxKind.Grass, G(start + rollTo * RollStep * (r + 0.5f), 0.02f), 0.6f, 4);
                    t += RollSeconds;
                }
                s.Part(m, t, t + 0.8f, start, face, DollStyle.Stand, GigglePose); t += 0.8f;
                var end = start + rollTo * RollStep * Rolls;
                if (i == 0) Mark(s, t, CampMark.Flatten, Vector2.Lerp(start, end, 0.5f));
                s.Part(m, t, t + 0.5f, end, face, DollStyle.Sit, SitUpPose, PropKind.None, Snap); t += 0.5f;
                PersonMark(s, t, CampMark.GrassBits, m);
                s.Part(m, t, t + 1.2f, end, face, DollStyle.Laugh, (ref DollPose p, float u, float l) => Seated(ref p)); t += 1.2f;
                var away = end + Rot(across, 40f + 30f * i) * 0.4f;
                s.Part(m, t, t + WalkTime(end, away, KidRun), away, away + across, DollStyle.Stand, null, PropKind.None, KidRun).run = true;
            }
            s.a = first;
            return s;
        }

        // ================================================================== B33.b lie back and look up
        /// <summary>Lying on the back (lean −90 from the sit, 0.6 s: tipped backwards, the face up), the hands behind the head (arms x 170, z ∓40).</summary>
        public static void LieBackPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.6f);
            p.lean = -90f * k; p.offset.y = Mathf.Lerp(-0.03f, 0.03f, k); p.squash = Mathf.Lerp(0.78f, 1f, k);
            p.armL = Lerp(p.armL, new Vector3(170f, 0f, -40f), k); p.armR = Lerp(p.armR, new Vector3(170f, 0f, 40f), k);
        }

        /// <summary>Still on the back: now and then pointing straight up (<paramref name="pointAt"/>, 1 s: the arm x 90 from the chest), a turn of the
        /// head (<paramref name="lookAt"/>, 0.3 s), a soft laugh together (<paramref name="laughAt"/>, a bob).</summary>
        static CampPoseFn StillPose(float pointAt, float lookAt, float laughAt) => (ref DollPose p, float t, float len) =>
        {
            p.lean = -90f; p.offset.y = 0.03f;
            p.armL = new Vector3(170f, 0f, -40f); p.armR = new Vector3(170f, 0f, 40f);
            float pt = Bump(t, pointAt, 1f);
            if (pointAt >= 0f && pt > 0f) p.armR = Lerp(p.armR, new Vector3(90f, 0f, 4f), Mathf.Min(1f, pt * 2f));
            if (lookAt >= 0f) p.roll += 18f * In(t, lookAt, 0.3f) * (1f - In(t, lookAt + 1.4f, 0.3f));
            p.offset.y += 0.008f * Mathf.Abs(Mathf.Sin((t - laughAt) * 8f)) * Bump(t, laughAt, 0.8f);
        };

        /// <summary>B33.b: two people (a kid with a parent, or a couple) sit side by side in the grass and lie back, hands behind the head, still for
        /// <paramref name="lieFor"/> (6–10 s); one points straight up, the other turns the head to look; a soft laugh together. The first sits up,
        /// stretches and holds out a hand; they go. <see cref="CampScene.a"/>: the point.</summary>
        public static CampScene LieBack(int a, int b, Vector2 aAt, Vector2 bAt, bool bKid, Vector2 spot, Vector2 toCam, float lieFor, float t0)
        {
            toCam = toCam.sqrMagnitude > 1e-6f ? toCam.normalized : Vector2.down;
            lieFor = Mathf.Clamp(lieFor, 6f, 10f);
            var across = new Vector2(-toCam.y, toCam.x);
            var s = new CampScene(CampUse.LieBack, t0, spot) { holdFocus = true };
            int ma = s.Add(a), mb = s.Add(b);
            Vector2 pa = spot + across * 0.05f, pb = spot - across * 0.05f;
            // they lie with the feet toward the camera, the heads away: the faces read
            float t = Mathf.Max(WalkTime(aAt, pa, Walk), WalkTime(bAt, pb, bKid ? KidWalk : Walk));
            s.Part(ma, 0f, t, pa, pa + toCam, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(mb, 0f, t, pb, pb + toCam, DollStyle.Stand, null, PropKind.None, bKid ? KidWalk : Walk);
            s.Part(ma, t, t + 0.4f, pa, pa + toCam, DollStyle.Sit); s.Part(mb, t, t + 0.4f, pb, pb + toCam, DollStyle.Sit); t += 0.4f;
            s.Part(ma, t, t + 0.6f, pa, pa + toCam, DollStyle.Stand, LieBackPose); s.Part(mb, t, t + 0.6f, pb, pb + toCam, DollStyle.Stand, LieBackPose); t += 0.6f;
            float point = lieFor * 0.35f, laugh = lieFor * 0.6f;
            s.Part(ma, t, t + lieFor, pa, pa + toCam, DollStyle.Stand, StillPose(point, -1f, laugh));
            s.Part(mb, t, t + lieFor, pb, pb + toCam, DollStyle.Stand, StillPose(-1f, point + 0.3f, laugh));
            s.a = t + point;
            t += lieFor;
            // a sits up, stands, stretches, holds out a hand; b sits up and is helped up
            s.Part(ma, t, t + 0.5f, pa, pa + toCam, DollStyle.Sit, SitUpBackPose); t += 0.5f;
            s.Part(ma, t, t + 0.4f, pa, pa + toCam, DollStyle.Stand);
            s.Part(mb, t - 0.5f, t + 1.4f, pb, pb + toCam, DollStyle.Stand, StillPose(-1f, -1f, -9f));
            t += 0.4f;
            s.Part(ma, t, t + 1f, pa, pa + toCam, DollStyle.Stretch); t += 1f;
            s.Part(ma, t, t + 0.9f, pa, pb, DollStyle.Stand, OfferPose);
            s.Part(mb, t, t + 0.5f, pb, pa, DollStyle.Sit, SitUpBackPose);
            s.Part(mb, t + 0.5f, t + 0.9f, pb, pa, DollStyle.Stand, TakePose);
            t += 0.9f;
            float wb = WalkTime(pa, aAt, Walk);
            s.Part(ma, t, t + wb * 0.5f, Vector2.Lerp(pa, aAt, 0.5f), aAt, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(mb, t, t + wb * 0.5f, Vector2.Lerp(pb, aAt, 0.5f) - across * 0.08f, aAt, DollStyle.Stand, null, PropKind.None, bKid ? KidWalk : Walk);
            return s;
        }

        // ================================================================== B33.c pick flowers
        /// <summary>B33.c: the picker walks slowly through the tall grass to each flower (2–3), bends, picks it (the grab at 0.4 s: 2 grass blades) and
        /// holds it up; the flowers make a little bunch; then gives it to <paramref name="taker"/> (−1: lays it on the remembering stone at
        /// <paramref name="takerAt"/>), who sniffs it and tucks it behind the ear (it stays there until night). <see cref="CampScene.a"/>: the
        /// hand-over.</summary>
        public static CampScene PickFlowers(int picker, Vector2 at, IList<Vector2> flowers, int taker, Vector2 takerAt, float t0)
        {
            var s = new CampScene(CampUse.PickFlowers, t0, flowers.Count > 0 ? flowers[0] : at);
            int m = s.Add(picker);
            float pace = Walk * 0.75f;
            float t = 0f; var from = at;
            int n = Mathf.Clamp(flowers.Count, 1, 3);
            float firstPick = -1f;
            for (int i = 0; i < n; i++)
            {
                var f = flowers[i];
                var stand = f - Dir(from, f, Vector2.down) * 0.05f;
                float w = WalkTime(from, stand, pace);
                s.Part(m, t, t + w, stand, f, DollStyle.Stand, null, PropKind.None, pace); t += w;
                s.Part(m, t, t + 0.6f, stand, f, DollStyle.Stand, BendPickPose);
                s.Fx(t + 0.4f, CampFxKind.Grass, G(f, 0.03f), 0.4f, 2);
                if (firstPick < 0f) firstPick = t + 0.4f;
                t += 0.6f;
                s.Part(m, t, t + 0.7f, stand, f, DollStyle.Stand, HoldUpPose); t += 0.7f;
                from = stand;
            }
            // #692 p3 (the user's review: the camera drifted to the fire): the taker comes to the picker in the grass, so the hand-over stays
            // where the flowers were; laid on the stone, the picker still walks there
            var meet = from + Dir(from, takerAt, Vector2.down) * 0.1f;
            var give = taker >= 0 ? from : takerAt + Dir(takerAt, from, Vector2.down) * 0.07f;
            var takeAt = taker >= 0 ? meet : takerAt;
            float wg = taker >= 0 ? 0.4f : WalkTime(from, give, Walk);
            s.Part(m, t, t + wg, give, takeAt, DollStyle.Stand, null, PropKind.None, Walk); t += wg;
            s.Tip(m, firstPick, t + 0.5f, n > 1 ? CampTipKind.Bunch : CampTipKind.Flower);
            if (taker >= 0)
            {
                int k = s.Add(taker);
                float wk = WalkTime(takerAt, meet, Walk);
                s.Part(k, Mathf.Max(0f, t - wk - 0.6f), t, meet, give, DollStyle.Stand, null, PropKind.None, Walk); // sees them picking and comes over
                s.Part(m, t, t + 0.9f, give, takeAt, DollStyle.Stand, OfferPose);
                s.a = t + 0.5f;
                s.Part(k, t, t + 0.9f, meet, give, DollStyle.Stand, TakePose);
                s.Tip(k, t + 0.5f, t + 2.6f, n > 1 ? CampTipKind.Bunch : CampTipKind.Flower);
                t += 0.9f;
                s.Part(k, t, t + 1.4f, meet, give, DollStyle.Stand, CampScenes1.SniffNodPose);
                s.Part(m, t, t + 2.3f, give, takeAt, DollStyle.Laugh);
                t += 1.4f;
                s.Part(k, t, t + 0.6f, meet, give, DollStyle.Stand, TuckPose);
                PersonMark(s, t + 0.3f, CampMark.EarFlower, k);
                t += 0.9f;
            }
            else
            {
                // laid on the remembering stone (B32-style care): a bend, the flowers stay as a mark
                s.Part(m, t, t + 0.7f, give, takerAt, DollStyle.Stand, BendPickPose);
                s.a = t + 0.4f;
                Mark(s, t + 0.4f, CampMark.EarFlower, takerAt, 0.03f);
                t += 0.7f;
                s.Part(m, t, t + 1f, give, takerAt, DollStyle.Mourn); t += 1f;
            }
            s.Part(m, t, t + 0.3f, give, give + (give - takerAt), DollStyle.Stand);
            return s;
        }

        // ================================================================== B33.d blow a grass whistle
        /// <summary>#692 p3: how far each side of the whistler the other kids come to play (u).</summary>
        public const float OthersApart = 0.17f;

        /// <summary>The blade held at the mouth between both hands (arms x 125, z ∓15).</summary>
        public static void WhistleHoldPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.25f);
            p.armL = Lerp(p.armL, new Vector3(125f, 0f, 15f), k); p.armR = Lerp(p.armR, new Vector3(125f, 0f, -15f), k);
        }

        /// <summary>A blow (0.4 s): lean −5, the head bobs forward 0.015 u, stretch 1.05; the hit at the end.</summary>
        public static void BlowPose(ref DollPose p, float t, float len)
        {
            WhistleHoldPose(ref p, 1f, len);
            float u = Bump(t, 0f, 0.4f);
            p.lean += -5f * u + 10f * Bump(t, 0.25f, 0.15f); p.offset.z -= 0.015f * Bump(t, 0.25f, 0.15f); p.squash = Mathf.Lerp(1f, 1.05f, u);
        }

        /// <summary>A dud: nothing comes out; a shrug (arms out z ∓50, the shoulders up 0.006).</summary>
        static void ShrugPose(ref DollPose p, float t, float len)
        {
            float u = Bump(t, 0f, len);
            p.armL = Lerp(new Vector3(60f, 0f, 15f), new Vector3(30f, 0f, -50f), u); p.armR = Lerp(new Vector3(60f, 0f, -15f), new Vector3(30f, 0f, 50f), u);
            p.offset.y += 0.006f * u;
        }

        /// <summary>Hands over the ears (arms x 150, z ∓60, 0.8 s).</summary>
        public static void EarsPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.15f) * (1f - In(t, len - 0.15f, 0.15f));
            p.armL = Lerp(p.armL, new Vector3(150f, 0f, -60f), u); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 60f), u); p.lean -= 6f * u;
        }

        /// <summary>B33.d: a kid pulls a blade of grass (bend, the snap: 1 blade), holds it between the hands at the mouth and blows: two duds
        /// (nothing, a shrug), then it works (two short white lines burst at the hands); the kids near turn, cover their ears (0.8 s), laugh and
        /// all try. <see cref="CampScene.a"/>: the blow that works.</summary>
        public static CampScene GrassWhistle(int kid, Vector2 at, Vector2 spot, IList<int> others, IList<Vector2> othersAt, float t0, Vector2 across = default)
        {
            var s = new CampScene(CampUse.GrassWhistle, t0, spot) { holdFocus = true };
            int m = s.Add(kid);
            var face = spot + Dir(at, spot, Vector2.down);
            float t = 0f, w = WalkTime(at, spot, KidWalk);
            s.Part(m, t, t + w, spot, face, DollStyle.Stand, null, PropKind.None, KidWalk); t += w;
            s.Part(m, t, t + 0.6f, spot, face, DollStyle.Stand, BendPickPose);
            s.Fx(t + 0.4f, CampFxKind.Grass, G(spot + Dir(at, spot, Vector2.down) * 0.04f, 0.02f), 0.35f, 1);
            float bladeFrom = t + 0.4f;
            t += 0.6f;
            s.Part(m, t, t + 0.4f, spot, face, DollStyle.Stand, WhistleHoldPose); t += 0.4f;
            for (int d = 0; d < 2; d++)
            {
                s.Part(m, t, t + 0.4f, spot, face, DollStyle.Stand, BlowPose); t += 0.4f;
                s.Part(m, t, t + 0.7f, spot, face, DollStyle.Stand, ShrugPose); t += 0.7f;
                s.Part(m, t, t + 0.3f, spot, face, DollStyle.Stand, WhistleHoldPose); t += 0.3f;
            }
            s.Part(m, t, t + 0.4f, spot, face, DollStyle.Stand, BlowPose);
            s.a = t + 0.4f;
            s.Fx(t + 0.4f, CampFxKind.Whistle, new Vector3(0f, 0.01f, 0f), 0.12f, 2, CampAnchor.Hand, m);
            t += 0.4f;
            s.Part(m, t, t + 1.2f, spot, face, DollStyle.Stand, WhistleHoldPose);
            float heard = t;
            // #692 p3 (the user's review: the others were off camera): they come and play near, one each side of the whistler as the camera
            // sees it, a little toward the camera, back turned at first, so the turn and the ears read in the same frame
            var ac = across.sqrMagnitude > 1e-6f ? across.normalized : Rot(Dir(spot, face, Vector2.down), 90f);
            var toCam = new Vector2(ac.y, -ac.x);
            for (int i = 0; i < others.Count; i++)
            {
                int k = s.Add(others[i]);
                float sideK = i % 2 == 0 ? 1f : -1f;
                var near = spot + ac * sideK * (OthersApart + 0.05f * (i / 2)) + toCam * 0.04f;
                var away = near + ac * sideK;
                float wk = Mathf.Min(WalkTime(othersAt[i], near, KidWalk), Mathf.Max(0.5f, heard - 0.5f));
                s.Part(k, 0f, wk, near, away, DollStyle.Stand, null, PropKind.None, KidWalk);
                float h = heard + 0.15f + 0.1f * i;
                s.Part(k, wk, h, near, away, DollStyle.Look);
                s.Part(k, h, h + 0.3f, near, spot, DollStyle.Stand); // the turn
                s.Part(k, h + 0.3f, h + 1.1f, near, spot, DollStyle.Stand, EarsPose);
                s.Part(k, h + 1.1f, h + 2.1f, near, spot, DollStyle.Laugh);
                // they all try: a blade, the hands at the mouth, a blow (no sound: theirs are duds)
                s.Part(k, h + 2.1f, h + 2.6f, near, spot, DollStyle.Stand, BendPickPose);
                s.Part(k, h + 2.6f, h + 3.6f, near, spot, DollStyle.Stand, BlowPose);
                s.Tip(k, h + 2.5f, h + 3.6f, CampTipKind.Blade);
            }
            t += 1.2f;
            s.Part(m, t, t + 1f, spot, face, DollStyle.Laugh); t += 1f;
            s.Part(m, t, t + 0.4f, spot, face, DollStyle.Stand, BlowPose);
            s.Fx(t + 0.4f, CampFxKind.Whistle, new Vector3(0f, 0.01f, 0f), 0.1f, 2, CampAnchor.Hand, m);
            t += 0.4f;
            s.Part(m, t, Mathf.Max(t + 0.8f, heard + 3.8f), spot, face, DollStyle.Laugh);
            s.Tip(m, bladeFrom, t, CampTipKind.Blade);
            return s;
        }

        // ================================================================== B34.a water a sapling
        /// <summary>Carrying a full bucket (lean −4, the arm out a little).</summary>
        static void BucketWalkPose(ref DollPose p, float t, float len) { p.lean -= 4f; p.armR = new Vector3(20f, 0f, 22f); p.roll += Mathf.Sin(t * 6f) * 2f; }

        /// <summary>B34.a: the tip over the little tree: crouched, the bucket tipped slowly (the arm forward x 50 → 100, lean 20, over 1.2 s).</summary>
        public static void PourPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 1.2f);
            p.squash = 0.84f; p.offset.y -= 0.01f; p.lean += 20f;
            p.armR = new Vector3(Mathf.Lerp(50f, 100f, k), 0f, 10f);
        }
        public const float PourSeconds = 1.2f;

        /// <summary>B34.a: the water carrier walks over with the bucket, stops 0.08 u from the little tree, crouches and tips the bucket slowly over it
        /// (6 droplets in 1 s at the foot); a darker wet patch (r 0.05 u, gone in a view hour) and the tree perks up; stands, looks at it, nods.
        /// <see cref="CampScene.a"/>: the wet patch (the hit).</summary>
        public static CampScene WaterSapling(int carrier, Vector2 at, Vector2 sapling, float t0, Vector2 standSide = default, float standOff = 0.08f)
        {
            var s = new CampScene(CampUse.WaterSapling, t0, sapling) { holdFocus = true };
            int m = s.Add(carrier);
            var stand = sapling + (standSide.sqrMagnitude > 1e-6f ? standSide.normalized : Dir(sapling, at, Vector2.down)) * standOff; // #692 p3: outside a young tree's crown
            float t = 0f, w = WalkTime(at, stand, Walk * 0.85f);
            s.Part(m, t, t + w, stand, sapling, DollStyle.Stand, BucketWalkPose, PropKind.Bucket, Walk * 0.85f).poseWalking = true; t += w;
            s.Part(m, t, t + 0.3f, stand, sapling, DollStyle.Stand, CrouchPose, PropKind.Bucket); t += 0.3f;
            s.Part(m, t, t + PourSeconds + 0.3f, stand, sapling, DollStyle.Stand, PourPose, PropKind.Bucket);
            for (int i = 0; i < 6; i++) s.Fx(t + 0.2f + i * 0.17f, CampFxKind.Droplets, G(Vector2.Lerp(stand, sapling, 0.6f), 0.06f), 0.45f, 1);
            s.a = t + PourSeconds;
            Mark(s, t + PourSeconds, CampMark.WetPatch, sapling, 0f, 0.05f);
            s.Fx(t + PourSeconds, CampFxKind.Perk, G(sapling), 1f);
            t += PourSeconds + 0.3f;
            s.Part(m, t, t + 0.4f, stand, sapling, DollStyle.Stand, null, PropKind.BucketEmpty); t += 0.4f;
            s.Part(m, t, t + 1f, stand, sapling, DollStyle.Look, null, PropKind.BucketEmpty); t += 1f;
            s.Part(m, t, t + 0.6f, stand, sapling, DollStyle.Stand, NodPose, PropKind.BucketEmpty); t += 0.6f;
            s.Part(m, t, t + WalkTime(stand, at, Walk) * 0.5f, Vector2.Lerp(stand, at, 0.5f), at, DollStyle.Stand, null, PropKind.BucketEmpty, Walk);
            return s;
        }

        // ================================================================== B34.b ring a sapling with stones
        /// <summary>B34.b: a grown-up and a kid fetch stones from <paramref name="pile"/> (the grown-up 3 in the arms, the kid one in the hand) and set
        /// them into a ring r 0.06 round the sapling (each: a crouch, the hand down, 0.4 s, a dust puff, the stone stays); six stones in two trips. Both
        /// stand back; the kid hops. <see cref="CampScene.a"/>: the first stone; <see cref="CampScene.b"/>: the last.</summary>
        public static CampScene RingSapling(int grown, int kid, Vector2 gAt, Vector2 kAt, Vector2 sapling, Vector2 pile, float t0, float ringR = LandGeo.RingR)
        {
            var s = new CampScene(CampUse.RingSapling, t0, sapling) { holdFocus = true };
            int g = s.Add(grown);
            int k = kid >= 0 ? s.Add(kid) : -1;
            var out1 = Dir(sapling, pile, Vector2.down);
            Vector2 gFrom = gAt, kFrom = kAt;
            float t = 0f, first = -1f, last = 0f;
            int stone = 0;
            for (int trip = 0; trip < 2; trip++)
            {
                var gp = pile + Rot(out1, 90f) * 0.04f; var kp = pile - Rot(out1, 90f) * 0.04f;
                float wg = WalkTime(gFrom, gp, Walk), wk = k >= 0 ? WalkTime(kFrom, kp, KidWalk) : 0f;
                float w = Mathf.Max(wg, wk);
                s.Part(g, t, t + w, gp, pile, DollStyle.Stand, null, PropKind.None, Walk);
                if (k >= 0) s.Part(k, t, t + w, kp, pile, DollStyle.Stand, null, PropKind.None, KidWalk);
                t += w;
                s.Part(g, t, t + 0.6f, gp, pile, DollStyle.Stand, BendPickPose);
                if (k >= 0) s.Part(k, t, t + 0.6f, kp, pile, DollStyle.Stand, BendPickPose);
                s.Tip(g, t + 0.4f, 0f, CampTipKind.Stones); // the end is set below
                int gTip = s.tips.Count - 1;
                int kTip = -1;
                if (k >= 0) { s.Tip(k, t + 0.4f, 0f, CampTipKind.Stone); kTip = s.tips.Count - 1; }
                t += 0.6f;
                // to the ring: the grown-up sets two, the kid one
                int gi0 = stone, gi1 = stone + 1, ki = stone + 2;
                stone += k >= 0 ? 3 : 2;
                Vector2 Crouch(int i) { var r = LandGeo.RingSpot(sapling, i, ringR); return r + (r - sapling).normalized * 0.06f; }
                var gc0 = Crouch(gi0); var gc1 = Crouch(gi1); var kc = Crouch(ki);
                float wg2 = WalkTime(gp, gc0, Walk * 0.85f), wk2 = k >= 0 ? WalkTime(kp, kc, KidWalk) : 0f;
                s.Part(g, t, t + wg2, gc0, sapling, DollStyle.Stand, HugStonesPose, PropKind.None, Walk * 0.85f).poseWalking = true;
                if (k >= 0) s.Part(k, t, t + wk2, kc, sapling, DollStyle.Stand, null, PropKind.None, KidWalk);
                float ts = t + wg2;
                s.Part(g, ts, ts + 0.6f, gc0, sapling, DollStyle.Stand, CrouchSetPose); SetStone(s, ts + 0.4f, sapling, gi0, ringR); if (first < 0f) first = ts + 0.4f;
                s.Part(g, ts + 0.6f, ts + 0.9f, gc1, sapling, DollStyle.Stand, CrouchPose, PropKind.None, 0.25f);
                s.Part(g, ts + 0.9f, ts + 1.5f, gc1, sapling, DollStyle.Stand, CrouchSetPose); SetStone(s, ts + 1.3f, sapling, gi1, ringR);
                var tg = s.tips[gTip]; tg.to = ts + 1.3f; tg.kind = CampTipKind.Stones; s.tips[gTip] = tg;
                float te = ts + 1.5f;
                if (k >= 0)
                {
                    float tk = Mathf.Max(t + wk2, ts + 0.5f); // the kid waits for the grown-up's first, then sets theirs
                    s.Part(k, t + wk2, tk, kc, sapling, DollStyle.Stand);
                    s.Part(k, tk, tk + 0.6f, kc, sapling, DollStyle.Stand, CrouchSetPose); SetStone(s, tk + 0.4f, sapling, ki, ringR);
                    var tkk = s.tips[kTip]; tkk.to = tk + 0.4f; s.tips[kTip] = tkk;
                    te = Mathf.Max(te, tk + 0.6f);
                    last = Mathf.Max(last, tk + 0.4f);
                    kFrom = kc;
                }
                last = Mathf.Max(last, ts + 1.3f);
                gFrom = gc1;
                if (k >= 0 && te > ts + 1.5f) s.Part(g, ts + 1.5f, te, gc1, sapling, DollStyle.Stand);
                else if (k >= 0) s.Part(k, s.parts[k][s.parts[k].Count - 1].to, te, kc, sapling, DollStyle.Stand);
                t = te;
            }
            // both stand back and look; the kid hops
            var gb = sapling + (gFrom - sapling).normalized * (ringR + 0.14f);
            s.Part(g, t, t + 0.8f, gb, sapling, DollStyle.Stand, null, PropKind.None, 0.25f);
            s.Part(g, t + 0.8f, t + 2.2f, gb, sapling, DollStyle.Look);
            if (k >= 0)
            {
                var kb = sapling + (kFrom - sapling).normalized * (ringR + 0.12f);
                s.Part(k, t, t + 0.7f, kb, sapling, DollStyle.Stand, null, PropKind.None, 0.3f);
                s.Part(k, t + 0.7f, t + 1.5f, kb, sapling, DollStyle.Hop);
                s.Part(k, t + 1.5f, t + 2.2f, kb, sapling, DollStyle.Laugh);
            }
            s.a = first; s.b = last;
            return s;
        }

        static void SetStone(CampScene s, float t, Vector2 sapling, int i, float ringR)
        {
            var r = LandGeo.RingSpot(sapling, i, ringR);
            Mark(s, t, CampMark.RingStone, r);
            s.Fx(t, CampFxKind.Dust, G(r, 0.01f), 0.3f, 1);
        }

        // ================================================================== B34.c a kid plants a twig beside it
        /// <summary>Pushing the twig into the ground: both arms x 30, lean 35, down 0.02 u; the snap at 0.45 s.</summary>
        public static void PushTwigPose(ref DollPose p, float t, float len)
        {
            float d = In(t, 0f, 0.25f);
            p.squash = Mathf.Lerp(1f, 0.8f, d); p.offset.y -= 0.012f * d + 0.02f * In(t, 0.3f, 0.15f) * (1f - In(t, 0.6f, 0.2f)); p.lean += 35f * d;
            p.armL = Lerp(p.armL, new Vector3(30f, 0f, 8f), d); p.armR = Lerp(p.armR, new Vector3(30f, 0f, -8f), d);
        }

        /// <summary>Three pats on the soil round it, a hand in turn (arm x 40).</summary>
        static void PatSoilPose(ref DollPose p, float t, float len)
        {
            CrouchPose(ref p, t, len);
            float c = Mathf.Repeat(t, 0.7f);
            float u = Bump(c, 0f, 0.35f), v = Bump(c, 0.35f, 0.35f);
            p.armR = Lerp(new Vector3(20f, 0f, 10f), new Vector3(40f, 0f, 6f), u);
            p.armL = Lerp(new Vector3(20f, 0f, -10f), new Vector3(40f, 0f, -6f), v);
        }

        /// <summary>B34.c: a kid picks up a twig at <paramref name="twigAt"/> (bend, lift), crouches 0.06 u from the sapling, pushes it into the ground
        /// (the twig stands: a mark, for a season), pats the soil round it three times, "waters" it (2 droplets) and nods at it.
        /// <see cref="CampScene.a"/>: the twig stands.</summary>
        public static CampScene PlantTwig(int kid, Vector2 at, Vector2 sapling, Vector2 twigAt, float t0, Vector2 toCam = default, float plantOff = 0.06f)
        {
            var s = new CampScene(CampUse.PlantTwig, t0, sapling) { holdFocus = true };
            int m = s.Add(kid);
            // #692 p3 (the user's review: the kid was behind the tree): with the camera's way known, the twig goes in on the camera's side of
            // the little tree, a little round, and the kid crouches in front of it, between the twig and the camera
            bool cam = toCam.sqrMagnitude > 1e-6f;
            var front = cam ? toCam.normalized : Dir(sapling, at, Vector2.down);
            var side = cam ? Rot(front, 35f) : Rot(front, 70f);
            var plant = sapling + side * plantOff;
            var crouch = plant + (cam ? Rot(front, -20f) : front) * 0.06f;
            float t = 0f, w = WalkTime(at, twigAt, KidWalk);
            s.Part(m, t, t + w, twigAt, twigAt + Dir(at, twigAt, Vector2.down), DollStyle.Stand, null, PropKind.None, KidWalk); t += w;
            s.Part(m, t, t + 0.6f, twigAt, twigAt + Dir(at, twigAt, Vector2.down), DollStyle.Stand, BendPickPose); t += 0.6f;
            float w2 = WalkTime(twigAt, crouch, KidWalk);
            s.Part(m, t, t + w2, crouch, plant, DollStyle.Stand, null, PropKind.Twig, KidWalk); t += w2;
            s.Part(m, t, t + 0.45f, crouch, plant, DollStyle.Stand, PushTwigPose, PropKind.Twig);
            s.Part(m, t + 0.45f, t + 0.8f, crouch, plant, DollStyle.Stand, PushTwigPose);
            s.a = t + 0.45f;
            Mark(s, t + 0.45f, CampMark.Twig, plant);
            s.Fx(t + 0.45f, CampFxKind.Dust, G(plant, 0.01f), 0.3f, 1);
            t += 0.8f;
            s.Part(m, t, t + 2.1f, crouch, plant, DollStyle.Stand, PatSoilPose); t += 2.1f;
            s.Part(m, t, t + 0.6f, crouch, plant, DollStyle.Stand, CrouchPose);
            s.Fx(t + 0.2f, CampFxKind.Droplets, G(plant, 0.05f), 0.35f, 1);
            s.Fx(t + 0.4f, CampFxKind.Droplets, G(plant, 0.05f), 0.35f, 1);
            t += 0.6f;
            s.Part(m, t, t + 0.4f, crouch, plant, DollStyle.Stand); t += 0.4f;
            s.Part(m, t, t + 0.7f, crouch, plant, DollStyle.Stand, NodPose); t += 0.7f;
            s.Part(m, t, t + 1f, crouch, plant, DollStyle.Laugh);
            return s;
        }

        // ================================================================== B35.a sit on a rock; the kid on the rock
        /// <summary>Seated on top of a boulder (the sit offset +0.06 u).</summary>
        public static void RockSitPose(ref DollPose p, float t, float len) { Seated(ref p); p.offset.y += RockSit; p.lean -= 6f; p.armL = new Vector3(35f, 0f, -18f); p.armR = new Vector3(35f, 0f, 18f); } // #692 p3: leaning back a little, hands on the knees
        /// <summary>#692 p3: how far to the side of the boulder's middle the grown-up sits, and the kid stands on a shared boulder.</summary>
        public const float SeatSide = 0.05f, KidSide = 0.045f;
        public const float RockSit = 0.075f; // #692 p3 (the user's review: the sitter looked as if standing): the seat on the boulder's top, not in it

        /// <summary>The climb on (0.4 s): a hop 0.07 u up and 0.08 u in, landing on top.</summary>
        static void ClimbPose(ref DollPose p, float t, float len)
        {
            float u = Mathf.Clamp01(t / len);
            p.offset.y += LandGeo.RockH * Smooth(u) + 0.03f * Mathf.Sin(u * Mathf.PI); p.offset.z += 0.08f * Smooth(u);
            p.squash = Mathf.Lerp(0.85f, 1f, Smooth(u * 2f));
            p.armL = new Vector3(60f, 0f, -20f); p.armR = new Vector3(60f, 0f, 20f);
        }

        /// <summary>King of the rock: standing on top, arms up wide (over PoseBig).</summary>
        static void OnTopPose(ref DollPose p, float t, float len) { p.offset.y += LandGeo.RockH; }

        /// <summary>The jump off (0.5 s): forward 0.12 u, down 0.07 u in an arc, the bounce at the landing.</summary>
        static void JumpOffPose(ref DollPose p, float t, float len)
        {
            float u = Mathf.Clamp01(t / 0.4f);
            p.offset.y += LandGeo.RockH * (1f - u) + 0.04f * Mathf.Sin(u * Mathf.PI); p.offset.z += 0.12f * u;
            if (t > 0.4f) { float b = (t - 0.4f) / Mathf.Max(0.01f, len - 0.4f); p.squash = 1f - 0.2f * Mathf.Sin(Mathf.Clamp01(b) * Mathf.PI); p.offset.z += 0f; }
            p.armL = new Vector3(110f, 0f, -40f); p.armR = new Vector3(110f, 0f, 40f);
        }

        /// <summary>B35.a: a grown-up walks up to a boulder, turns and sits on top (<paramref name="sitFor"/> s); a kid (−1: none) climbs the other
        /// boulder (or the same one, round its far side), stands on top with the arms up wide (king of the rock, 1 s) and jumps off with a bounce and
        /// a dust puff. <see cref="CampScene.a"/>: the kid's jump lands.</summary>
        public static CampScene SitRock(int grown, Vector2 gAt, Vector2 rock, Vector2 toCam, float sitFor, int kid, Vector2 kAt, Vector2 kidRock, float t0)
        {
            toCam = toCam.sqrMagnitude > 1e-6f ? toCam.normalized : Vector2.down;
            var across = new Vector2(toCam.y, -toCam.x);
            // #692 p3 (the user's review): one boulder shared (the cheat picks the other only when it is near): the grown-up sits on its top to one
            // side as the camera sees it, the kid climbs the other side; the camera holds both
            bool shared = (kidRock - rock).sqrMagnitude < 1e-4f && grown >= 0;
            if (shared) kidRock = rock - across * KidSide;
            var s = new CampScene(CampUse.SitRock, t0, grown >= 0 && kid >= 0 ? Vector2.Lerp(rock, kidRock, 0.5f) : grown >= 0 ? rock : kidRock) { holdFocus = true };
            float t = 0f;
            if (grown >= 0)
            {
                int g = s.Add(grown);
                var front = rock + toCam * 0.1f;
                float w = WalkTime(gAt, front, Walk);
                s.Part(g, 0f, w, front, rock, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(g, w, w + 0.4f, front, front + toCam, DollStyle.Stand); // turns
                var seat = rock + across * SeatSide + toCam * 0.02f; // #692 p3: on the top's side: the boulder shows beside and under the sitter
                s.Part(g, w + 0.4f, w + 0.9f, seat, seat + toCam, DollStyle.Sit, RockSitPose, PropKind.None, 0.25f);
                s.Part(g, w + 0.9f, w + 0.9f + sitFor, seat, seat + toCam, DollStyle.Sit, RockSitPose);
                t = w + 0.9f + sitFor;
                s.Part(g, t, t + 0.4f, front, front + toCam, DollStyle.Stand, null, PropKind.None, 0.3f);
                s.Part(g, t + 0.4f, t + 0.4f + WalkTime(front, gAt, Walk) * 0.5f, Vector2.Lerp(front, gAt, 0.5f), gAt, DollStyle.Stand, null, PropKind.None, Walk);
            }
            if (kid >= 0)
            {
                int k = s.Add(kid);
                var into = Dir(kAt, kidRock, -toCam);
                var edge = kidRock - into * 0.08f;
                float tk = grown >= 0 ? 2f : 0f;
                float w = WalkTime(kAt, edge, KidWalk);
                s.Part(k, tk, tk + w, edge, kidRock, DollStyle.Stand, null, PropKind.None, KidWalk); tk += w;
                s.Part(k, tk, tk + 0.2f, edge, kidRock, DollStyle.Stand, CrouchPose); tk += 0.2f;
                s.Part(k, tk, tk + 0.4f, edge, kidRock, DollStyle.Stand, ClimbPose); tk += 0.4f;
                s.Part(k, tk, tk + 0.3f, kidRock, kidRock + toCam, DollStyle.Stand, OnTopPose, PropKind.None, Snap); tk += 0.3f;
                s.Part(k, tk, tk + 1f, kidRock, kidRock + toCam, DollStyle.PoseBig, OnTopPose); tk += 1f;
                s.Part(k, tk, tk + 0.3f, kidRock, kidRock + toCam, DollStyle.Stand, OnTopPose); tk += 0.3f;
                s.Part(k, tk, tk + 0.65f, kidRock, kidRock + toCam, DollStyle.Stand, JumpOffPose);
                var land = kidRock + toCam * 0.12f;
                s.a = tk + 0.4f;
                s.Fx(tk + 0.4f, CampFxKind.Dust, G(land, 0.01f), 0.45f, 2);
                tk += 0.65f;
                s.Part(k, tk, tk + 1f, land, land + toCam, DollStyle.Laugh, null, PropKind.None, Snap); tk += 1f;
                var away = land + Rot(toCam, 60f) * 0.4f;
                s.Part(k, tk, tk + WalkTime(land, away, KidRun), away, away + toCam, DollStyle.Stand, null, PropKind.None, KidRun).run = true;
            }
            return s;
        }

        // ================================================================== B35.b stack pebbles
        /// <summary>Setting a pebble on the stack: the arm x 60, slow (0.5 s), then a 0.3 s hold, balancing.</summary>
        public static void SetPebblePose(ref DollPose p, float t, float len)
        {
            CrouchPose(ref p, t, len);
            float k = In(t, 0f, 0.5f);
            p.armR = Lerp(new Vector3(40f, 0f, 8f), new Vector3(60f, 0f, 4f), k);
            if (t > 0.5f) p.roll += Mathf.Sin((t - 0.5f) * 25f) * 1.5f;
        }

        /// <summary>After the fifth: both hands away slowly (arms out z ∓40, 0.6 s).</summary>
        static void HandsAwayPose(ref DollPose p, float t, float len)
        {
            CrouchPose(ref p, t, len);
            float k = In(t, 0f, 0.6f);
            p.armL = Lerp(new Vector3(55f, 0f, -4f), new Vector3(40f, 0f, -40f), k); p.armR = Lerp(new Vector3(55f, 0f, 4f), new Vector3(40f, 0f, 40f), k);
        }

        /// <summary>B35.b: a kid (or an old one) crouches by <paramref name="spot"/> (on a boulder, <paramref name="baseH"/> up, or the bank) and stacks five
        /// pebbles, each smaller: pick one (bend, the grab: it pops into the hand), set it on (slow, a hold: balancing; the stack wobbles); on the fifth
        /// a pause, the hands away slowly, a cheer. <paramref name="topple"/> (1 time in 3): it topples, the pebbles bounce and roll off, they laugh.
        /// The stack stays for the view day. <see cref="CampScene.a"/>: the fifth set; <see cref="CampScene.b"/>: the topple (−1: none).</summary>
        public static CampScene StackPebbles(int who, bool kid, Vector2 at, Vector2 spot, float baseH, bool topple, float t0, Vector2 crouchSide = default)
        {
            var s = new CampScene(CampUse.StackPebbles, t0, spot) { holdFocus = true };
            int m = s.Add(who);
            var crouch = spot + (crouchSide.sqrMagnitude > 1e-6f ? crouchSide.normalized : Dir(spot, at, Vector2.down)) * 0.07f; // the user can't see a stack behind the stacker: the director passes the far side
            float pace = kid ? KidWalk : Walk * 0.7f;
            float t = 0f, w = WalkTime(at, crouch, pace);
            s.Part(m, t, t + w, crouch, spot, DollStyle.Stand, null, PropKind.None, pace); t += w;
            s.Part(m, t, t + 0.3f, crouch, spot, DollStyle.Stand, CrouchPose); t += 0.3f;
            for (int i = 0; i < LandGeo.Pebbles; i++)
            {
                s.Part(m, t, t + 0.6f, crouch, spot, DollStyle.Stand, BendPickPose);
                s.Tip(m, t + 0.4f, t + 1.1f, CampTipKind.Pebble, LandGeo.PebbleSize(i) / LandGeo.PebbleSize(0));
                t += 0.6f;
                s.Part(m, t, t + 0.8f, crouch, spot, DollStyle.Stand, SetPebblePose);
                Mark(s, t + 0.5f, CampMark.Pebble, spot, baseH);
                if (i == LandGeo.Pebbles - 1) s.a = t + 0.5f;
                t += 0.8f;
            }
            s.Part(m, t, t + 0.4f, crouch, spot, DollStyle.Stand, CrouchPose); t += 0.4f;
            s.Part(m, t, t + 0.6f, crouch, spot, DollStyle.Stand, HandsAwayPose); t += 0.6f;
            if (topple)
            {
                s.b = t + 0.3f;
                Mark(s, t + 0.3f, CampMark.Topple, spot, baseH);
                s.Fx(t + 0.9f, CampFxKind.Dust, G(spot, 0.01f), 0.35f, 2);
                s.Part(m, t, t + 0.4f, crouch, spot, DollStyle.Stand, CrouchPose); t += 0.4f;
                s.Part(m, t, t + 1.4f, crouch, spot, DollStyle.Laugh); t += 1.4f;
            }
            else
            {
                s.b = -1f;
                s.Part(m, t, t + 1.2f, crouch, spot, DollStyle.Cheer); t += 1.2f;
            }
            s.Part(m, t, t + 0.4f, crouch, crouch + (crouch - spot), DollStyle.Stand);
            return s;
        }

        // ================================================================== B36.a stick sword fight
        /// <summary>Snapping a branch to size over the knee: both hands x 70, a knee up, the snap at 0.3 s.</summary>
        public static void KneeSnapPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f) * (1f - In(t, len - 0.15f, 0.15f));
            p.armL = Lerp(p.armL, new Vector3(70f, 0f, 20f), u); p.armR = Lerp(p.armR, new Vector3(70f, 0f, -20f), u);
            p.lean += 12f * u; p.offset.y += 0.006f * u;
            if (t > 0.3f) { float k = Bump(t, 0.3f, 0.15f); p.armL.z += 25f * k; p.armR.z -= 25f * k; }
        }

        /// <summary>En garde: the stick pointing at the other (arm x 110).</summary>
        static void EnGardePose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.2f); p.armR = Lerp(p.armR, new Vector3(110f, 0f, 6f), u); p.lean += 6f * u; p.squash = Mathf.Lerp(1f, 0.95f, u); }

        /// <summary>The end beat: both sticks up high (x 170), a hop.</summary>
        static void SticksUpPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.25f);
            p.armR = Lerp(p.armR, new Vector3(170f, 0f, 10f), u); p.armL = new Vector3(20f, 0f, -50f);
            p.offset.y += 0.025f * Mathf.Abs(Mathf.Sin(t * 2.5f * Mathf.PI)) * u; p.lean -= 6f * u;
        }

        public const int Strikes = 4;
        public const float StrikeEvery = 1.2f, DuelGap = 0.24f;

        /// <summary>B36.a: two kids at the deadfall each pick up a fallen branch (bend, lift) and snap it to size over the knee (2 chips; half drops),
        /// stand en garde (0.5 s), then B3.b's duel as built (<see cref="WoodsGame.Strike"/> / <see cref="WoodsGame.Block"/>, four strikes, a small white
        /// flash and a light wood knock at each cross); end: both sticks up high, then dropped back on the deadfall. <see cref="CampScene.a"/>: the
        /// first cross.</summary>
        public static CampScene StickFight(int k1, int k2, Vector2 at1, Vector2 at2, Vector2 deadfall, Vector2 toCam, float t0)
        {
            toCam = toCam.sqrMagnitude > 1e-6f ? toCam.normalized : Vector2.down;
            var axis = new Vector2(-toCam.y, toCam.x);
            var mid = deadfall + toCam * 0.28f;
            var s = new CampScene(CampUse.StickFight, t0, mid) { holdFocus = true };
            int a = s.Add(k1), b = s.Add(k2);
            Vector2 pa = deadfall + axis * 0.07f + toCam * 0.06f, pb = deadfall - axis * 0.07f + toCam * 0.06f;
            float t = Mathf.Max(WalkTime(at1, pa, KidRun), WalkTime(at2, pb, KidRun));
            s.Part(a, 0f, t, pa, deadfall, DollStyle.Stand, null, PropKind.None, KidRun).run = true;
            s.Part(b, 0f, t, pb, deadfall, DollStyle.Stand, null, PropKind.None, KidRun).run = true;
            s.Part(a, t, t + 0.6f, pa, deadfall, DollStyle.Stand, BendPickPose); s.Part(b, t + 0.15f, t + 0.75f, pb, deadfall, DollStyle.Stand, BendPickPose);
            s.Part(b, t, t + 0.15f, pb, deadfall, DollStyle.Stand);
            t += 0.75f;
            // the snap over the knee: the branch, then a short stick
            s.Part(a, t, t + 0.6f, pa, pa + toCam, DollStyle.Stand, KneeSnapPose, PropKind.Branch);
            s.Part(b, t, t + 0.6f, pb, pb + toCam, DollStyle.Stand, KneeSnapPose, PropKind.Branch);
            s.Fx(t + 0.3f, CampFxKind.Chips, new Vector3(0f, 0f, 0f), 0.3f, 2, CampAnchor.Hand, a); // #692 p3: smaller chips
            s.Fx(t + 0.35f, CampFxKind.Chips, new Vector3(0f, 0f, 0f), 0.3f, 2, CampAnchor.Hand, b);
            t += 0.6f;
            Vector2 da = mid + axis * DuelGap * 0.5f, db = mid - axis * DuelGap * 0.5f;
            float w = Mathf.Max(WalkTime(pa, da, KidWalk), WalkTime(pb, db, KidWalk));
            s.Part(a, t, t + w, da, db, DollStyle.Stand, null, PropKind.Twig, KidWalk);
            s.Part(b, t, t + w, db, da, DollStyle.Stand, null, PropKind.Twig, KidWalk);
            t += w;
            s.Part(a, t, t + 0.5f, da, db, DollStyle.Stand, EnGardePose, PropKind.Twig); s.Part(b, t, t + 0.5f, db, da, DollStyle.Stand, EnGardePose, PropKind.Twig);
            t += 0.5f;
            float first = -1f;
            for (int k = 0; k < Strikes; k++)
            {
                int att = k % 2 == 0 ? a : b, def = att == a ? b : a;
                var pAtt = att == a ? da : db; var pDef = att == a ? db : da;
                s.Part(att, t, t + StrikeEvery, pAtt, pDef, DollStyle.Stand, (ref DollPose p, float u, float l) => WoodsGame.Strike.Apply(ref p, u), PropKind.Twig);
                s.Part(def, t, t + StrikeEvery, pDef, pAtt, DollStyle.Stand, (ref DollPose p, float u, float l) => WoodsGame.Block.Apply(ref p, u), PropKind.Twig);
                float hit = t + WoodsGame.Strike.ContactAt;
                s.Fx(hit, CampFxKind.Clash, G(mid, 0.065f), 0.35f);
                if (first < 0f) first = hit;
                t += StrikeEvery;
            }
            s.a = first;
            s.Part(a, t, t + 1f, da, db, DollStyle.Stand, SticksUpPose, PropKind.Twig); s.Part(b, t, t + 1f, db, da, DollStyle.Stand, SticksUpPose, PropKind.Twig);
            t += 1f;
            // back on the deadfall
            float wb = Mathf.Max(WalkTime(da, pa, KidWalk), WalkTime(db, pb, KidWalk));
            s.Part(a, t, t + wb, pa, deadfall, DollStyle.Stand, null, PropKind.Twig, KidWalk); s.Part(b, t, t + wb, pb, deadfall, DollStyle.Stand, null, PropKind.Twig, KidWalk);
            t += wb;
            s.Part(a, t, t + 0.4f, pa, deadfall, DollStyle.Stand, BendPickPose, PropKind.Twig); s.Part(b, t, t + 0.4f, pb, deadfall, DollStyle.Stand, BendPickPose, PropKind.Twig);
            t += 0.4f;
            s.Part(a, t, t + 0.5f, pa, deadfall, DollStyle.Stand, BendPickPose); s.Part(b, t, t + 0.5f, pb, deadfall, DollStyle.Stand, BendPickPose);
            t += 0.5f;
            s.Part(a, t, t + 0.8f, pa, pa + toCam, DollStyle.Laugh); s.Part(b, t, t + 0.8f, pb, pb + toCam, DollStyle.Laugh);
            return s;
        }

        // ================================================================== B36.b a stick horse
        /// <summary>The gallop on the stick horse: a hop 0.03 u at 3 Hz, lean 10 forward, the pitch rocking ±6° in step, the hand at the chest
        /// holding the horse's front (arm x 60).</summary>
        public static void GallopPose(ref DollPose p, float t, float len)
        {
            float ph = t * 3f * Mathf.PI * 2f;
            p.offset.y += 0.03f * Mathf.Abs(Mathf.Sin(ph * 0.5f));
            p.lean += 10f + 6f * Mathf.Sin(ph * 0.5f + 0.6f);
            p.armR = new Vector3(60f, 0f, -12f); p.armL = new Vector3(40f + 20f * Mathf.Sin(ph * 0.5f), 0f, -30f);
        }

        /// <summary>The horse rears: stop, lean −15, its front up (the arm x 110), 0.4 s.</summary>
        public static void RearPose(ref DollPose p, float t, float len) { float u = Bump(t, 0f, len); p.lean -= 15f * u; p.armR = Lerp(new Vector3(60f, 0f, -12f), new Vector3(110f, 0f, -12f), u); p.armL = new Vector3(60f, 0f, -40f); }

        public const float HorseLoopR = 0.14f, HorseSpeed = 0.3f; // #692 p3: a small loop the camera holds (was 0.22)

        /// <summary>B36.b: a kid picks up a long fallen branch and rides it: the gallop round a loop in the camp (r 0.22 u, one and a half times round),
        /// rearing once halfway (stop, lean back, its front up, 0.4 s); then leans the "horse" against <paramref name="leanAt"/> (it stays there a view
        /// hour) and pats it twice. <see cref="CampScene.a"/>: the rear; <see cref="CampScene.b"/>: the horse leant.</summary>
        public static CampScene StickHorse(int kid, Vector2 at, Vector2 branchAt, Vector2 centre, Vector2 leanAt, float t0)
        {
            var s = new CampScene(CampUse.StickHorse, t0, centre) { holdFocus = true };
            int m = s.Add(kid);
            float t = 0f, w = WalkTime(at, branchAt, KidWalk);
            s.Part(m, t, t + w, branchAt, branchAt + Dir(at, branchAt, Vector2.down), DollStyle.Stand, null, PropKind.None, KidWalk); t += w;
            s.Part(m, t, t + 0.6f, branchAt, branchAt + Dir(at, branchAt, Vector2.down), DollStyle.Stand, BendPickPose);
            float horseFrom = t + 0.4f;
            t += 0.6f;
            float a0 = Mathf.Atan2(branchAt.y - centre.y, branchAt.x - centre.x);
            var onLoop = centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * HorseLoopR;
            float w2 = WalkTime(branchAt, onLoop, HorseSpeed);
            s.Part(m, t, t + w2, onLoop, onLoop + (onLoop - branchAt), DollStyle.Stand, GallopPose, PropKind.None, HorseSpeed).poseWalking = true; t += w2;
            const int segs = 18; // 1.5 times round
            float step = 2f * Mathf.PI * HorseLoopR / 12f / HorseSpeed;
            for (int k = 1; k <= segs; k++)
            {
                float ang = a0 + k * Mathf.PI * 2f / 12f;
                var p = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * HorseLoopR;
                var tang = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang));
                s.Part(m, t, t + step, p, p + tang, DollStyle.Stand, GallopPose, PropKind.None, HorseSpeed).poseWalking = true;
                t += step;
                if (k == segs / 2)
                {
                    s.Part(m, t, t + 0.6f, p, p + tang, DollStyle.Stand, RearPose);
                    s.a = t + 0.3f;
                    t += 0.6f;
                }
            }
            var lastP = centre + new Vector2(Mathf.Cos(a0 + segs * Mathf.PI * 2f / 12f), Mathf.Sin(a0 + segs * Mathf.PI * 2f / 12f)) * HorseLoopR;
            var stand = leanAt + Dir(leanAt, lastP, Vector2.down) * 0.08f;
            float w3 = WalkTime(lastP, stand, HorseSpeed);
            s.Part(m, t, t + w3, stand, leanAt, DollStyle.Stand, GallopPose, PropKind.None, HorseSpeed).poseWalking = true; t += w3;
            s.Part(m, t, t + 0.6f, stand, leanAt, DollStyle.Stand, BendPickPose);
            s.b = t + 0.4f;
            Mark(s, t + 0.4f, CampMark.Horse, leanAt);
            s.Tip(m, horseFrom, t + 0.4f, CampTipKind.Horse);
            t += 0.6f;
            s.Part(m, t, t + 0.7f, stand, leanAt, DollStyle.Stand, PatPose);
            s.Part(m, t + 0.7f, t + 1.4f, stand, leanAt, DollStyle.Stand, PatPose);
            t += 1.4f;
            s.Part(m, t, t + 1f, stand, leanAt, DollStyle.Laugh);
            return s;
        }
    }
}
