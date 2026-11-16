// #692 part 2 (docs/design/animation-specs.md "#692 part 2: the Keeper's tree visits", B32.a-h): the visits as pure builders.
// Each takes who and where (box x, z) and returns a CampScene whose timeline follows the spec row's beats and timings. The tree's
// shape (trunk, stone ring, crown height) comes from KeeperTreeKit's stages (TreeGeo). Who visits and when: View/CampLife.Tree.cs
// and TreeVisits below (pure rules, tested in EditMode).
using System.Collections.Generic;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using UnityEngine;
using static AquariumCiv.View.CampPose;

namespace AquariumCiv.View
{
    /// <summary>#692 part 2: the Keeper's tree as the visits see it (KeeperTreeKit's numbers per stage).</summary>
    public static class TreeGeo
    {
        static float K(int stage) => stage == 2 ? 0.45f : stage == 3 ? 0.72f : 1.15f;
        /// <summary>The trunk's radius at its foot (the root flare: 0.034 x the stage's scale x 1.5).</summary>
        public static float TrunkR(int stage) => stage < 2 ? 0.02f : 0.034f * K(stage) * 1.5f;
        /// <summary>The stone ring's radius (KeeperTreeKit.RingParts).</summary>
        public static float RingR(int stage) => stage < 2 ? 0.1f : 0.06f + 0.035f * stage;
        /// <summary>The crown's middle above the ground.</summary>
        public static float CrownY(int stage) => stage < 2 ? 0.1f : 0.04f * K(stage) + (stage == 2 ? 0.17f : stage == 3 ? 0.26f : 0.4f) + (stage == 2 ? 0.04f : stage == 3 ? 0.055f : 0.08f);
        /// <summary>The visit spot: the stone ring's edge plus 0.08 u, along <paramref name="dir"/> (the camera's side).</summary>
        public static Vector2 Visit(Vector2 tree, int stage, Vector2 dir) => tree + dir * (RingR(stage) + 0.08f);
        /// <summary>Sitting with the back to the trunk (B32.a: "back to it 0.06 u off").</summary>
        public static Vector2 Lean(Vector2 tree, int stage, Vector2 dir) => tree + dir * (TrunkR(stage) + SitOff);
        /// <summary>Standing with a hand flat on the bark (an arm is 0.075 u).</summary>
        public static Vector2 Touch(Vector2 tree, int stage, Vector2 dir) => tree + dir * (TrunkR(stage) + 0.07f);
        public const float SitOff = 0.055f;
    }

    /// <summary>#692 part 2: who visits the Keeper's tree, and when (pure; the director asks).</summary>
    public static class TreeVisits
    {
        /// <summary>B32.d: the dusk window a quiet visit starts in (view hours), and the hour from which, on the second dusk, it plays unwatched.</summary>
        public const float DuskFrom = 17.2f, DuskTo = 19.4f, UnseenFrom = 19.0f;
        /// <summary>B32.d: a death is visited this many dusks at most (Q10: "waits up to two dusks for you to be watching").</summary>
        public const int Dusks = 2;

        /// <summary>B32.d: the one closest to the dead: the partner, else a grown kid, else a friend (alive and drawn: <paramref name="shown"/>). −1 none.</summary>
        public static int Mourner(IReadOnlyList<Notable> notables, Notable dead, System.Func<int, bool> shown)
        {
            if (dead == null) return -1;
            Notable Find(int id) { if (id < 0) return null; foreach (var n in notables) if (n.id == id) return n; return null; }
            var p = Find(dead.partnerId);
            if (p != null && p.alive && shown(p.id)) return p.id;
            Notable kid = null;
            foreach (var n in notables) if (n.alive && n.parentId == dead.id && n.age >= 16f && shown(n.id) && (kid == null || n.age > kid.age)) kid = n;
            if (kid != null) return kid.id;
            var f = Find(dead.friendId);
            if (f != null && f.alive && shown(f.id)) return f.id;
            foreach (var n in notables) if (n.alive && n.friendId == dead.id && shown(n.id)) return n.id;
            return -1;
        }

        /// <summary>B32.d [est]: the first view day of the next spring after a death on <paramref name="died"/> (at least two days on), or −1.</summary>
        public static long FirstSpringAfter(long died)
        {
            for (long d = died + 2; d < died + 16; d++)
                if (LifeClock.SeasonOf(d) == Season.Spring && LifeClock.SeasonOf(d - 1) != Season.Spring) return d;
            return -1;
        }

        /// <summary>B32.d: which dusk of a visit today is (0 the first, 1 the second), and whether it is the death's visit (0) or the spring one (1).
        /// False when no visit falls today.</summary>
        public static bool Due(long day, long died, out int which, out int dusk)
        {
            which = 0; dusk = (int)(day - died);
            if (dusk >= 0 && dusk < Dusks) return true;
            long spring = FirstSpringAfter(died);
            which = 1; dusk = (int)(day - spring);
            return spring >= 0 && dusk >= 0 && dusk < Dusks;
        }

        /// <summary>B32.d (Q10): a visit starts at dusk only while the Keeper watches the tree, except late on its last dusk, when it plays unseen.</summary>
        public static bool PlayNow(int dusk, float hour, bool watched)
        {
            if (hour < DuskFrom || hour >= DuskTo) return false;
            return watched || dusk >= Dusks - 1 && hour >= UnseenFrom;
        }

        /// <summary>B32.f: a newborn is brought in a morning in its first view days (at most one a game season; the director keeps that).</summary>
        public const int NewbornDays = 3;
        public const float NewbornFrom = 8f, NewbornTo = 11f;
        public static bool NewbornDue(long day, long born, float hour) => day - born >= 0 && day - born <= NewbornDays && hour >= NewbornFrom && hour < NewbornTo;

        /// <summary>B32.e: the newlyweds go at the dusk of the wedding day, or the next morning (the wedding ended after dark).</summary>
        public static bool WeddingDue(long day, long wedding, float hour) => day == wedding && hour >= 17f && hour < 19.6f || day == wedding + 1 && hour >= 7.5f && hour < 10f;

        /// <summary>B32.g: the old one's afternoon (view hours).</summary>
        public const float OldFrom = 14f, OldTo = 16f;
    }

    public static class CampScenes2
    {
        const float Walk = CampScenes1.Walk, KidWalk = CampScenes1.KidWalk, KidRun = CampScenes1.KidRun;

        static float WalkTime(Vector2 a, Vector2 b, float speed) => Mathf.Clamp((b - a).magnitude / speed + 0.25f, 0.4f, 9f);
        static Vector2 Dir(Vector2 from, Vector2 to, Vector2 fallback) { var d = to - from; return d.sqrMagnitude > 1e-6f ? d.normalized : fallback; }
        static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        static Vector3 G(Vector2 p, float h = 0f) => new Vector3(p.x, h, p.y);

        // ================================================================== shared poses
        /// <summary>The slide down against the trunk (B32.a): squash 1 → 0.7 and down, 0.6 s Smooth, leaning back −10 on the bark.</summary>
        public static void SlideSitPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.6f);
            p.squash = Mathf.Lerp(1f, 0.7f, k); p.offset.y = Mathf.Lerp(0f, -0.035f, k); p.lean = Mathf.Lerp(0f, -10f, k);
        }

        /// <summary>Sitting against the trunk, dozing (B32.a): leaning back −10; every 6 s the head droops twice (forward 20 over 1 s, snaps up
        /// in 0.15 s), at 1 s and 3.6 s; arms resting.</summary>
        public static void DozePose(ref DollPose p, float t, float len)
        {
            p.squash = 0.7f; p.offset.y = -0.035f; p.lean = -10f;
            p.armL = new Vector3(18f, 0f, -12f); p.armR = new Vector3(18f, 0f, 12f);
            float c = Mathf.Repeat(t, 6f);
            p.lean += Droop(c, 1f) + Droop(c, 3.6f);
        }

        static float Droop(float t, float at) => t < at ? 0f : t < at + 1f ? 20f * Smooth(t - at) : t < at + 1.15f ? 20f * (1f - (t - at - 1f) / 0.15f) : 0f;

        /// <summary>A leaf on them, brushed off (B32.a): the arm x 120, z ∓40, 0.3 s, over the doze.</summary>
        static CampPoseFn DozeBrush(float brushAt) => (ref DollPose p, float t, float len) =>
        {
            DozePose(ref p, t, len);
            float u = Bump(t, brushAt, 0.3f);
            if (u > 0f) p.armR = Lerp(p.armR, new Vector3(120f, 0f, -40f), u);
        };

        static void StandUpPose(ref DollPose p, float t, float len) { float k = In(t, 0f, len); p.squash = Mathf.Lerp(0.7f, 1f, k); p.offset.y = Mathf.Lerp(-0.035f, 0f, k); p.lean = Mathf.Lerp(-10f, 4f, Bump(t, 0f, len)); }

        /// <summary>One pat on the trunk: the arm to x 90 (the contact at 0.3 s), back.</summary>
        static void PatPose(ref DollPose p, float t, float len) { float u = Bump(t, 0f, 0.6f); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), u); p.lean += 6f * u; }

        /// <summary>A hand flat on the bark (arm x 90, lean 10), held.</summary>
        static void HandOnBarkPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f)); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 4f), u); p.lean += 10f * u; }

        /// <summary>B32.d's walk: 0.7 x, head down (forward 15), arms limp.</summary>
        static void HeadDownPose(ref DollPose p, float t, float len) { p.lean += 15f; p.armL = new Vector3(0f, 0f, -6f); p.armR = new Vector3(0f, 0f, 6f); }

        /// <summary>B32.d's sit: against the trunk, arms in (x 40), head down.</summary>
        static void MournSitPose(ref DollPose p, float t, float len)
        {
            SlideSitPose(ref p, t, len);
            float k = In(t, 0.3f, 0.6f);
            p.lean = Mathf.Lerp(p.lean, 14f, k);
            p.armL = Lerp(p.armL, new Vector3(40f, 0f, 22f), k); p.armR = Lerp(p.armR, new Vector3(40f, 0f, -22f), k);
        }

        /// <summary>B32.d: reach up on tiptoe (x 170, +0.02 u) and tie (fiddle ±10° at 4 Hz, 1.2 s).</summary>
        static void TiptoeTiePose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.3f), f = Mathf.Sin(t * 4f * Mathf.PI * 2f) * 10f * In(t, 0.3f, 0.1f);
            p.armL = Lerp(p.armL, new Vector3(170f + f, 0f, 8f), k); p.armR = Lerp(p.armR, new Vector3(170f - f, 0f, -8f), k);
            p.offset.y += 0.02f * k; p.squash = Mathf.Lerp(1f, 1.05f, k);
        }

        /// <summary>B32.d: the forehead rests on the bark (lean 25, 2 s).</summary>
        static void ForeheadPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.4f) * (1f - In(t, len - 0.4f, 0.4f)); p.lean += 25f * u; p.armR = Lerp(p.armR, new Vector3(70f, 0f, 4f), u); p.armL = Lerp(p.armL, new Vector3(70f, 0f, -4f), u); }

        /// <summary>Hand in hand (B32.e): the inner arm out and down toward the other (<paramref name="left"/>: this one walks on the left).</summary>
        static CampPoseFn HandHold(bool left) => (ref DollPose p, float t, float len) => { if (left) p.armL = new Vector3(22f, 0f, -38f); else p.armR = new Vector3(22f, 0f, 38f); }; // armR is the counter-clockwise side (B32.b's check)

        /// <summary>Both hands on the bark at once (B32.e: arms x 90, 1.5 s).</summary>
        static void BothHandsPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f)); p.armL = Lerp(p.armL, new Vector3(90f, 0f, -4f), u); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 4f), u); p.lean += 8f * u; }

        /// <summary>The wedding's join: a hop together, 0.03 u.</summary>
        static void HopTogetherPose(ref DollPose p, float t, float len) { float u = Bump(t, 0.1f, 0.4f); p.offset.y += 0.03f * u; p.armL = new Vector3(60f, 0f, -20f); p.armR = new Vector3(60f, 0f, 20f); }

        /// <summary>The newborn cradled (arms x 60).</summary>
        static void CradlePose(ref DollPose p, float t, float len) { p.armL = new Vector3(60f, 0f, 22f); p.armR = new Vector3(60f, 0f, -22f); }

        /// <summary>B32.f: the bundle lifted toward the crown (both arms x 150, Back 1.5 s), a slow sway ±4°, then cradled again.</summary>
        static void LiftBabyPose(ref DollPose p, float t, float len)
        {
            float up = Back(Mathf.Clamp01(t / 1.5f)), down = In(t, len - 0.6f, 0.6f);
            float x = Mathf.LerpUnclamped(60f, 150f, up * (1f - down));
            p.armL = new Vector3(x, 0f, 14f); p.armR = new Vector3(x, 0f, -14f);
            p.lean -= 8f * up * (1f - down);
            p.roll += Mathf.Sin(t * 1.6f) * 4f * (1f - down) * Mathf.Clamp01(t / 1.5f);
        }

        static float Back(float u) { const float s = 1.4f; u -= 1f; return u * u * ((s + 1f) * u + s) + 1f; }

        /// <summary>B32.g: the old one's slow bent walk (lean 12).</summary>
        static void BentPose(ref DollPose p, float t, float len) { p.lean += 12f; p.armL = new Vector3(10f, 0f, -8f); p.armR = new Vector3(14f, 0f, 8f); }

        /// <summary>A hand held out with something (x 85).</summary>
        static void OfferPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.3f); p.armR = Lerp(p.armR, new Vector3(85f, 0f, 4f), u); p.lean += 6f * u; }

        /// <summary>Taking it, seated (x 70, then back).</summary>
        static void TakeSeatedPose(ref DollPose p, float t, float len) { DozeFree(ref p); float u = Bump(t, 0f, len); p.armR = Lerp(p.armR, new Vector3(70f, 0f, 6f), u); }

        static void DozeFree(ref DollPose p) { p.squash = 0.7f; p.offset.y = -0.035f; p.lean = -6f; }

        /// <summary>A seated pat on the kid's head beside them (arm x 110, across).</summary>
        static void SeatedHeadPatPose(ref DollPose p, float t, float len) { DozeFree(ref p); float u = Bump(t, 0f, len); p.armL = Lerp(new Vector3(18f, 0f, -12f), new Vector3(110f, 0f, 30f), u); }

        /// <summary>B32.g's end: the helper takes both hands (arms x 80) and leans back to pull them up (Back 0.5 s).</summary>
        static void PullUpPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.3f); p.armL = Lerp(p.armL, new Vector3(80f, 0f, 6f), u); p.armR = Lerp(p.armR, new Vector3(80f, 0f, -6f), u); p.lean -= 12f * Bump(t, 0.3f, 0.6f); }

        /// <summary>Being pulled up: both hands out (x 80), the squash back to 1 (Back 0.5 s).</summary>
        static void PulledUpPose(ref DollPose p, float t, float len)
        {
            float k = Back(Mathf.Clamp01((t - 0.3f) / 0.5f));
            p.squash = Mathf.LerpUnclamped(0.7f, 1f, k); p.offset.y = Mathf.LerpUnclamped(-0.035f, 0f, k); p.lean = Mathf.Lerp(-6f, 10f, Mathf.Clamp01(k));
            p.armL = new Vector3(80f, 0f, -6f); p.armR = new Vector3(80f, 0f, 6f);
        }

        /// <summary>The ring round the trunk (B32.c): hands joined (arms out z ∓70), a skip (hop 0.02 u at 2.5 Hz).</summary>
        static void SkipRingPose(ref DollPose p, float t, float len)
        {
            p.armL = new Vector3(10f, 0f, -70f); p.armR = new Vector3(10f, 0f, 70f);
            p.offset.y += 0.02f * Mathf.Abs(Mathf.Sin(t * 2.5f * Mathf.PI));
        }

        /// <summary>Kids touch the trunk together (arms x 90), then fall back onto their bottoms (squash 0.7, 0.3 s), laughing.</summary>
        static void TouchFallPose(ref DollPose p, float t, float len)
        {
            if (t < 0.5f) { float u = In(t, 0f, 0.25f); p.armL = Lerp(p.armL, new Vector3(90f, 0f, -6f), u); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), u); p.lean += 10f * u; return; }
            float k = In(t, 0.5f, 0.3f);
            p.squash = Mathf.Lerp(1f, 0.7f, k); p.offset.y -= 0.03f * k; p.offset.z += 0.04f * k; p.lean -= 14f * k;
            p.armL = new Vector3(30f, 0f, -40f); p.armR = new Vector3(30f, 0f, 40f);
            if (t > 0.8f) { float b = Mathf.Abs(Mathf.Sin((t - 0.8f) * 9f)); p.offset.y += 0.008f * b; p.lean -= 6f * b; }
        }

        /// <summary>B32.h: the point up at the glass (arm x 150), held.</summary>
        static void PointGlassPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f)); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 8f), u); p.lean -= 6f * u; }

        /// <summary>B32.h: the planting mime: crouch, poke a finger into the ground (x 40, lean 35, 0.5 s), then rise with both arms slowly
        /// up (x 20 → 170 over 2 s, Smooth): it grew.</summary>
        static void PlantMimePose(ref DollPose p, float t, float len)
        {
            if (t < 0.9f)
            {
                float d = In(t, 0f, 0.3f);
                p.squash = Mathf.Lerp(1f, 0.78f, d); p.offset.y -= 0.02f * d; p.lean += 35f * d;
                p.armR = Lerp(p.armR, new Vector3(40f, 0f, 6f), d);
                if (t > 0.35f) p.offset.y -= 0.008f * Bump(t, 0.35f, 0.3f);
                return;
            }
            float r = Smooth((t - 0.9f) / 2f), up = In(t, 0.9f, 0.4f);
            p.squash = Mathf.Lerp(0.78f, 1.04f, up); p.offset.y += Mathf.Lerp(-0.02f, 0.01f, up); p.lean += Mathf.Lerp(35f, -4f, up);
            float x = Mathf.Lerp(20f, 170f, r);
            p.armL = new Vector3(x, 0f, -10f); p.armR = new Vector3(x, 0f, 10f);
        }

        /// <summary>The kids copy: arms slowly up (x 20 → 170 over 2 s).</summary>
        static void ArmsUpPose(ref DollPose p, float t, float len) { float x = Mathf.Lerp(20f, 170f, Smooth(t / 2f)); p.armL = new Vector3(x, 0f, -12f); p.armR = new Vector3(x, 0f, 12f); }

        // ================================================================== B32.a sit under it
        /// <summary>B32.a: walk to the trunk, turn, back to it, slide down to sit, lean back on the bark; dozing (two droops each 6 s); one leaf
        /// falls on them and is brushed off; stand, stretch, one pat on the trunk (the crown shakes). A second sitter (−1: none) does the
        /// same 50° round, a second later. <paramref name="sitFor"/>: seconds sitting (at least 6).</summary>
        /// <remarks>#692 p5 retakes (B44.b, a wild tree): <paramref name="doze"/> (null: B32.a's doze) takes the leaf's brush time and gives the
        /// sitting pose; <paramref name="trunkR"/> (≤ 0: the Keeper's tree at <paramref name="stage"/>) puts the back against a thinner trunk.</remarks>
        public static CampScene TreeSit(int key, Vector2 at, Vector2 tree, int stage, Vector2 dir, float sitFor, float t0, int second = -1, Vector2 secondAt = default,
            System.Func<float, CampPoseFn> doze = null, float trunkR = -1f)
        {
            var s = new CampScene(CampUse.TreeSit, t0, TreeGeo.Visit(tree, stage, dir));
            Sitter(s, key, at, tree, stage, dir, Mathf.Max(6f, sitFor), 0f, doze, trunkR);
            if (second >= 0) Sitter(s, second, secondAt, tree, stage, Rot(dir, 55f), Mathf.Max(6f, sitFor) - 1f, 1f, doze, trunkR);
            return s;
        }

        static void Sitter(CampScene s, int key, Vector2 at, Vector2 tree, int stage, Vector2 dir, float sitFor, float delay, System.Func<float, CampPoseFn> doze = null, float trunkR = -1f)
        {
            int m = s.Add(key);
            var spot = trunkR > 0f ? tree + dir * (trunkR + TreeGeo.SitOff) : TreeGeo.Lean(tree, stage, dir);
            var outward = spot + dir;
            float t = 0f, w = WalkTime(at, spot, Walk) + delay;
            s.Part(m, t, t + w, spot, outward, DollStyle.Stand, null, PropKind.None, Walk); t += w;
            s.Part(m, t, t + 0.4f, spot, outward, DollStyle.Stand); t += 0.4f;
            s.Part(m, t, t + 0.6f, spot, outward, DollStyle.Sit, SlideSitPose); t += 0.6f;
            float leafAt = Mathf.Min(4.6f, sitFor - 1.4f);
            s.Part(m, t, t + sitFor, spot, outward, DollStyle.Sit, doze != null ? doze(leafAt + 0.8f) : DozeBrush(leafAt + 0.8f));
            s.Fx(t + leafAt, CampFxKind.Leaves, new Vector3(0f, 0.1f, 0f), 0.6f, 1, CampAnchor.Head, m);
            t += sitFor;
            s.Part(m, t, t + 0.4f, spot, outward, DollStyle.Stand, StandUpPose); t += 0.4f;
            s.Part(m, t, t + 1f, spot, outward, DollStyle.Stretch); t += 1f;
            var touch = TreeGeo.Touch(tree, stage, dir);
            s.Part(m, t, t + 0.7f, touch, tree, DollStyle.Stand, PatPose, PropKind.None, 0.2f);
            s.Fx(t + 0.3f, CampFxKind.TreeShake, G(tree), 3f);
            t += 0.7f;
            s.Part(m, t, t + WalkTime(touch, at, Walk) * 0.5f, Vector2.Lerp(touch, at, 0.5f), at, DollStyle.Stand, null, PropKind.None, Walk);
        }

        // ================================================================== B32.b touch the bark in passing
        /// <summary>B32.b: a walker passing the trunk (heading <paramref name="heading"/>) goes on past it: a grown-up slows to 0.6 x, the near arm
        /// out (z 60 → x 80, 0.3 s), the hand trailing on the bark 0.6 s, a small nod (lean 8); a kid slaps it (x 100, Snap 0.1 s: the leaves
        /// shiver) and runs on. <see cref="CampScene.a"/>: the touch's time.</summary>
        public static CampScene TreeTouch(int key, bool kid, Vector2 at, Vector2 tree, int stage, Vector2 heading, float t0)
        {
            heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : Vector2.right;
            var rel = at - tree;
            var side = rel - heading * Vector2.Dot(rel, heading);
            side = side.sqrMagnitude > 1e-6f ? side.normalized : new Vector2(-heading.y, heading.x);
            var pass = tree + side * (TreeGeo.TrunkR(stage) + (kid ? 0.06f : 0.065f));
            // which arm is nearer the trunk: the doll's armR swings out on the side counter-clockwise of its heading seen from above (checked in the
            // AnimLab clip: the first guess put the hand out away from the trunk)
            bool trunkOnRight = heading.x * (tree - pass).y - heading.y * (tree - pass).x > 0f;
            var before = pass - heading * 0.12f; var after = pass + heading * 0.12f; var on = pass + heading * 0.45f;
            var s = new CampScene(CampUse.TreeTouch, t0, tree);
            int m = s.Add(key);
            float pace = kid ? KidWalk : Walk;
            float t = 0f, w = WalkTime(at, before, pace);
            s.Part(m, t, t + w, before, after, DollStyle.Stand, null, PropKind.None, pace); t += w;
            if (!kid)
            {
                float slow = 0.24f / (Walk * 0.6f);
                s.Part(m, t, t + slow, after, on, DollStyle.Stand, TrailHand(trunkOnRight, slow), PropKind.None, Walk * 0.6f).poseWalking = true;
                s.a = t + slow * 0.5f; t += slow;
                s.Part(m, t, t + WalkTime(after, on, Walk), on, on + heading, DollStyle.Stand, null, PropKind.None, Walk);
            }
            else
            {
                float w2 = WalkTime(before, pass, KidWalk) * 0.6f;
                s.Part(m, t, t + w2, pass, pass + heading, DollStyle.Stand, null, PropKind.None, KidWalk); t += w2;
                s.Part(m, t, t + 0.35f, pass, tree, DollStyle.Stand, Slap(trunkOnRight), PropKind.None, 0.2f);
                s.a = t + 0.12f;
                s.Fx(t + 0.12f, CampFxKind.TreeShake, G(tree), 2f);
                t += 0.35f;
                s.Part(m, t, t + WalkTime(pass, on, KidRun), on, on + heading, DollStyle.Laugh, null, PropKind.None, KidRun).run = true;
            }
            return s;
        }

        static CampPoseFn TrailHand(bool right, float len) => (ref DollPose p, float t, float l) =>
        {
            float u = In(t, 0f, 0.3f) * (1f - In(t, l - 0.25f, 0.25f));
            var arm = Lerp(new Vector3(0f, 0f, 60f), new Vector3(80f, 0f, 70f), In(t, 0f, 0.3f)); // out to the side (z 60), then forward along the bark
            if (right) p.armR = Lerp(p.armR, arm, u); else p.armL = Lerp(p.armL, new Vector3(arm.x, 0f, -arm.z), u);
            p.lean += 8f * Bump(t, l * 0.45f, 0.3f);
        };

        static CampPoseFn Slap(bool right) => (ref DollPose p, float t, float len) =>
        {
            float u = t < 0.12f ? Smooth(t / 0.12f) : 1f - In(t, 0.15f, 0.2f);
            var arm = new Vector3(100f, 0f, 40f);
            if (right) p.armR = Lerp(p.armR, arm, u); else p.armL = Lerp(p.armL, new Vector3(arm.x, 0f, -arm.z), u);
        };

        // ================================================================== B32.c kids circle it
        public const float RingSpeed = 0.3f;

        /// <summary>B32.c: 3–5 kids join hands in a ring round the trunk (r = the stone ring + 0.1 u), skipping round one way and back; then all
        /// run in, touch the trunk together (the crown shakes), and fall back on their bottoms laughing. <see cref="CampScene.a"/>: the touch.</summary>
        public static CampScene TreeCircle(IList<int> kids, IList<Vector2> at, Vector2 tree, int stage, Vector2 dir, float t0)
        {
            var s = new CampScene(CampUse.TreeCircle, t0, tree) { holdFocus = true }; // the camera stays on the tree (it swung with the lead kid)
            int n = kids.Count;
            float r = TreeGeo.RingR(stage) + 0.1f, a0 = Mathf.Atan2(dir.y, dir.x);
            float t = 0f;
            for (int i = 0; i < n; i++) { s.Add(kids[i]); t = Mathf.Max(t, WalkTime(at[i], P(tree, r, a0 + i * Mathf.PI * 2f / n), KidWalk)); }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, P(tree, r, a0 + i * Mathf.PI * 2f / n), tree, DollStyle.Stand, null, PropKind.None, KidWalk);
            for (int i = 0; i < n; i++) s.Part(i, t, t + 0.5f, P(tree, r, a0 + i * Mathf.PI * 2f / n), tree, DollStyle.Stand, SkipRingPose);
            t += 0.5f;
            const int segs = 12;
            float step = 2f * Mathf.PI * r / segs / RingSpeed;
            for (int way = 0; way < 2; way++)
                for (int k = 0; k < segs; k++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        float ang = a0 + i * Mathf.PI * 2f / n + (way == 0 ? (k + 1) : (segs - k - 1)) * Mathf.PI * 2f / segs;
                        s.Part(i, t, t + step, P(tree, r, ang), tree, DollStyle.Stand, SkipRingPose, PropKind.None, RingSpeed).poseWalking = true;
                    }
                    t += step;
                }
            float runIn = WalkTime(P(tree, r, a0), P(tree, TreeGeo.TrunkR(stage) + 0.06f, a0), KidRun);
            for (int i = 0; i < n; i++)
                s.Part(i, t, t + runIn, P(tree, TreeGeo.TrunkR(stage) + 0.06f, a0 + i * Mathf.PI * 2f / n), tree, DollStyle.Stand, null, PropKind.None, KidRun).run = true;
            t += runIn;
            s.a = t + 0.25f;
            s.Fx(t + 0.25f, CampFxKind.TreeShake, G(tree), 3f);
            for (int i = 0; i < n; i++) s.Part(i, t, t + 2f, P(tree, TreeGeo.TrunkR(stage) + 0.06f, a0 + i * Mathf.PI * 2f / n), tree, DollStyle.Stand, TouchFallPose);
            return s;
        }

        static Vector2 P(Vector2 c, float r, float a) => c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

        // ================================================================== B32.d the quiet visit after a death
        /// <summary>The hug at the end is this long (PairBeats.Hug).</summary>
        public static float HugSeconds => PairBeats.Hug.Duration;

        /// <summary>
        /// B32.d (~25 s): 1 the walk from the fire, alone, 0.7 x, head down; the people at the fire turn to watch (1.5 s). 2 At the ring's edge, a
        /// look up at the crown (2 s). 3 A step in, a hand flat on the bark, held 3 s; the tree's glow comes up a little (<see cref="CampFxKind.Glow"/>).
        /// 4 The slide down against the trunk, arms in, head down, 7 s; a leaf drifts down and stays on the shoulder. 5 <paramref name="tie"/>:
        /// stand, reach up on tiptoe and tie this death's charm (the contact: <see cref="CampScene.b"/>); else the forehead on the bark (2 s).
        /// 6 A long look up (2 s), the walk back at a normal pace; <paramref name="meeter"/> (−1: none) stands and walks out 0.3 u to meet them:
        /// a hug (B7.d, <see cref="CampScene.pairs"/>). <see cref="CampScene.a"/>: the glow's start.
        /// </summary>
        public static CampScene TreeQuiet(int mourner, Vector2 at, Vector2 tree, int stage, Vector2 dir, bool tie, IList<int> watchers, IList<Vector2> watchAt, int meeter, Vector2 meeterAt, float t0)
        {
            var s = new CampScene(CampUse.TreeQuiet, t0, tree) { followReach = 2.5f }; // the camera goes with them back to the fire and the hug
            int m = s.Add(mourner);
            var visit = TreeGeo.Visit(tree, stage, dir);
            var touch = TreeGeo.Touch(tree, stage, dir);
            var sit = TreeGeo.Lean(tree, stage, dir);
            float t = 0f, w = Mathf.Clamp((visit - at).magnitude / (Walk * 0.7f), 4f, 12f);
            s.Part(m, t, t + w, visit, tree, DollStyle.Stand, HeadDownPose, PropKind.None, Walk * 0.7f).poseWalking = true;
            for (int i = 0; i < watchers.Count; i++)
            {
                int k = s.Add(watchers[i]);
                s.Part(k, 0.3f + 0.25f * i, 0.3f + 0.25f * i + Mathf.Max(1.5f, w), watchAt[i], visit, DollStyle.Sit).keepSpot = true;
            }
            t += w;
            s.Part(m, t, t + 2f, visit, tree, DollStyle.LookUp); t += 2f;
            s.Part(m, t, t + 0.6f, touch, tree, DollStyle.Stand, null, PropKind.None, 0.15f); t += 0.6f;
            s.Part(m, t, t + 3f, touch, tree, DollStyle.Stand, HandOnBarkPose);
            s.a = t + 0.4f;
            s.Fx(t + 0.4f, CampFxKind.Glow, G(tree, TreeGeo.CrownY(stage)), 1f);
            t += 3f;
            s.Part(m, t, t + 0.5f, sit, sit + dir, DollStyle.Stand, null, PropKind.None, 0.15f); t += 0.5f;
            float sitFor = 7f;
            s.Part(m, t, t + sitFor, sit, sit + dir, DollStyle.Sit, MournSitPose);
            s.Tip(m, t + 1.5f, 999f, CampTipKind.Leaf); // drifts down for 2 s and stays on the shoulder (drawn while they are in the scene)
            t += sitFor;
            s.Part(m, t, t + 0.4f, sit, sit + dir, DollStyle.Stand, StandUpPose); t += 0.4f;
            if (tie)
            {
                s.Part(m, t, t + 0.4f, touch, tree, DollStyle.Stand, null, PropKind.None, 0.15f); t += 0.4f;
                s.Part(m, t, t + 1.4f, touch, tree, DollStyle.Stand, TiptoeTiePose);
                s.Tip(m, t, t + 1.2f, CampTipKind.Charm);
                s.b = t + 1.2f;
                s.Fx(t + 1.2f, CampFxKind.TreeShake, G(tree), 2f);
                s.Fx(t + 1.2f, CampFxKind.Sparks, G(touch, 0.32f), 0.35f);
                t += 1.4f;
            }
            else
            {
                s.Part(m, t, t + 0.4f, touch, tree, DollStyle.Stand, null, PropKind.None, 0.15f); t += 0.4f;
                s.Part(m, t, t + 2f, touch, tree, DollStyle.Stand, ForeheadPose); t += 2f;
                s.b = -1f;
            }
            s.Part(m, t, t + 2f, touch + dir * 0.04f, tree, DollStyle.LookUp, null, PropKind.None, 0.1f); t += 2f;
            // 6: back toward the fire at a normal pace; someone at the fire meets them 0.3 u out: a hug
            var home = meeter >= 0 ? meeterAt : at;
            var toTree = Dir(home, visit, dir);
            // [est] the spec's 0.3 u out from the fire; when the tree stands far off (over 1 u), they meet halfway, so the hug is seen with the tree
            var meet = home + toTree * Mathf.Max(0.3f, ((visit - home).magnitude - 1f) * 0.5f + 0.3f);
            var stop = meet + toTree * PairStage.HugFrom;
            float wb = WalkTime(visit, stop, Walk);
            s.Part(m, t, t + wb, stop, meet, DollStyle.Stand, null, PropKind.None, Walk);
            if (meeter >= 0)
            {
                int k = s.IndexOf(meeter); if (k < 0) k = s.Add(meeter);
                float stand = t + wb - 1.6f;
                s.Part(k, stand, stand + 0.4f, meeterAt, visit, DollStyle.Stand).keepSpot = true;
                s.Part(k, stand + 0.4f, t + wb, meet, visit, DollStyle.Stand, null, PropKind.None, Walk);
                t += wb;
                var hug = PairStage.Hug(9321 + mourner, t0 + t, mourner, meeter, stop, meet, force: true);
                if (hug != null) s.pairs.Add(hug);
                t += HugSeconds + 0.2f;
                s.Part(m, t, t + 0.3f, stop, meet, DollStyle.Stand).keepSpot = true; // the scene's end (they go back to their plans)
            }
            else t += wb;
            return s;
        }

        // ================================================================== B32.e the newlyweds walk to the tree
        /// <summary>B32.e: the couple meet and walk to the tree hand in hand, stand side by side facing it (2 s), both look up, each lays a hand on
        /// the bark at the same moment (1.5 s), turn to each other and hop together (the wedding's join), and walk back as they came.</summary>
        public static CampScene TreeWed(int a, int b, Vector2 aAt, Vector2 bAt, Vector2 tree, int stage, Vector2 dir, float t0)
        {
            var s = new CampScene(CampUse.TreeWed, t0, TreeGeo.Visit(tree, stage, dir));
            int ma = s.Add(a), mb = s.Add(b);
            var toTree = Dir(aAt, tree, dir);
            var side = new Vector2(toTree.y, -toTree.x); // a's right
            var meetB = aAt + side * 0.08f;
            float t = 0f, w = WalkTime(bAt, meetB, Walk);
            s.Part(ma, 0f, w, aAt, meetB, DollStyle.Stand).keepSpot = true;
            s.Part(mb, 0f, w, meetB, aAt, DollStyle.Stand, null, PropKind.None, Walk);
            t += w;
            var across = Rot(dir, 90f);
            var va = TreeGeo.Visit(tree, stage, dir) + across * 0.045f; var vb = TreeGeo.Visit(tree, stage, dir) - across * 0.045f;
            bool aLeft = Vector2.Dot(va - vb, Rot(Dir(aAt, tree, dir), 90f)) > 0f;
            float pace = Walk * 0.85f, wt = WalkTime(aAt, va, pace);
            s.Part(ma, t, t + wt, va, tree, DollStyle.Stand, HandHold(aLeft), PropKind.None, pace).poseWalking = true;
            s.Part(mb, t, t + wt, vb, tree, DollStyle.Stand, HandHold(!aLeft), PropKind.None, pace).poseWalking = true;
            t += wt;
            s.Part(ma, t, t + 2f, va, tree, DollStyle.Stand, HandHold(aLeft)); s.Part(mb, t, t + 2f, vb, tree, DollStyle.Stand, HandHold(!aLeft)); t += 2f;
            s.Part(ma, t, t + 1.2f, va, tree, DollStyle.LookUp); s.Part(mb, t, t + 1.2f, vb, tree, DollStyle.LookUp); t += 1.2f;
            var ta = TreeGeo.Touch(tree, stage, Rot(dir, 22f)); var tb = TreeGeo.Touch(tree, stage, Rot(dir, -22f));
            if (Vector2.Dot(ta - tb, va - vb) < 0f) { var x = ta; ta = tb; tb = x; }
            s.Part(ma, t, t + 0.6f, ta, tree, DollStyle.Stand, null, PropKind.None, 0.15f); s.Part(mb, t, t + 0.6f, tb, tree, DollStyle.Stand, null, PropKind.None, 0.15f); t += 0.6f;
            s.Part(ma, t, t + 1.5f, ta, tree, DollStyle.Stand, HandOnBarkPose); s.Part(mb, t, t + 1.5f, tb, tree, DollStyle.Stand, HandOnBarkPose);
            s.a = t + 0.3f; t += 1.5f;
            s.Part(ma, t, t + 0.4f, ta, tb, DollStyle.Stand); s.Part(mb, t, t + 0.4f, tb, ta, DollStyle.Stand); t += 0.4f;
            s.Part(ma, t, t + 0.7f, ta, tb, DollStyle.Stand, HopTogetherPose); s.Part(mb, t, t + 0.7f, tb, ta, DollStyle.Stand, HopTogetherPose); t += 0.7f;
            float wb = WalkTime(va, aAt, pace);
            s.Part(ma, t, t + wb, aAt, aAt - toTree, DollStyle.Stand, HandHold(!aLeft), PropKind.None, pace).poseWalking = true;
            s.Part(mb, t, t + wb, meetB, meetB - toTree, DollStyle.Stand, HandHold(aLeft), PropKind.None, pace).poseWalking = true;
            return s;
        }

        // ================================================================== B32.f a parent brings the newborn
        /// <summary>B32.f: the parent walks with the bundle in the arms to the ring, lifts it toward the crown (x 150, Back 1.5 s) with a slow sway;
        /// a leaf drifts down past it (the tree's answer); cradles it again; a small bow; walks back. <see cref="CampScene.a"/>/<see cref="CampScene.b"/>:
        /// the lift's start and end (the bundle is lifted between, CampLife draws it).</summary>
        public static CampScene TreeNewborn(int parent, Vector2 at, Vector2 tree, int stage, Vector2 dir, float t0)
        {
            var s = new CampScene(CampUse.TreeNewborn, t0, TreeGeo.Visit(tree, stage, dir));
            int m = s.Add(parent);
            var visit = TreeGeo.Visit(tree, stage, dir) - dir * 0.04f;
            float t = 0f, w = WalkTime(at, visit, Walk * 0.85f);
            s.Part(m, t, t + w, visit, tree, DollStyle.Stand, CradlePose, PropKind.None, Walk * 0.85f).poseWalking = true; t += w;
            s.Part(m, t, t + 0.4f, visit, tree, DollStyle.Stand, CradlePose); t += 0.4f;
            float lift = 4.2f;
            s.Part(m, t, t + lift, visit, tree, DollStyle.Stand, LiftBabyPose);
            s.a = t; s.b = t + lift - 0.6f;
            s.Fx(t + 1.6f, CampFxKind.Leaves, G(Vector2.Lerp(tree, visit, 0.7f), TreeGeo.CrownY(stage) - 0.05f), 0.7f, 2);
            t += lift;
            s.Part(m, t, t + 0.4f, visit, tree, DollStyle.Stand, CradlePose); t += 0.4f;
            s.Part(m, t, t + 1f, visit, tree, DollStyle.Bow, CradlePose); t += 1f;
            s.Part(m, t, t + WalkTime(visit, at, Walk * 0.85f), at, at - dir, DollStyle.Stand, CradlePose, PropKind.None, Walk * 0.85f).poseWalking = true;
            s.kind = 1; // the bundle shows the whole scene
            return s;
        }

        // ================================================================== B32.g the old one's afternoons there
        /// <summary>B32.g: the old one's slow bent walk (0.6 x, lean 12) to the trunk, B32.a's sit, dozing on and off for <paramref name="sitFor"/> s.
        /// Now and then (<paramref name="kid"/> ≥ 0) a kid brings a flower, hands it over and sits beside them 4 s; the old one pats the kid's head.
        /// At the end <paramref name="helper"/> (−1: none, they get up alone) takes both hands, pulls them up, and walks them home slowly.</summary>
        public static CampScene TreeOld(int old, Vector2 at, Vector2 tree, int stage, Vector2 dir, float sitFor, int kid, Vector2 kidAt, int helper, Vector2 helperAt, float t0)
        {
            var s = new CampScene(CampUse.TreeOld, t0, TreeGeo.Lean(tree, stage, dir));
            int m = s.Add(old);
            var spot = TreeGeo.Lean(tree, stage, dir); var outward = spot + dir;
            float slow = Walk * 0.6f;
            float t = 0f, w = WalkTime(at, spot, slow);
            s.Part(m, t, t + w, spot, outward, DollStyle.Stand, BentPose, PropKind.None, slow).poseWalking = true; t += w;
            s.Part(m, t, t + 0.5f, spot, outward, DollStyle.Stand, BentPose); t += 0.5f;
            s.Part(m, t, t + 0.6f, spot, outward, DollStyle.Sit, SlideSitPose); t += 0.6f;
            float sitFrom = t;
            sitFor = Mathf.Max(8f, sitFor);
            if (kid >= 0)
            {
                int k = s.Add(kid);
                float come = sitFrom + sitFor * 0.3f;
                var by = spot + Rot(dir, 70f) * 0.07f; var front = spot + dir * 0.09f;
                float wk = WalkTime(kidAt, front, KidWalk);
                s.Part(k, come - wk, come, front, spot, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Tip(k, come - wk, come + 0.5f, CampTipKind.Flower);
                s.Part(k, come, come + 0.8f, front, spot, DollStyle.Stand, OfferPose);
                s.Part(m, sitFrom, come + 0.3f, spot, outward, DollStyle.Sit, DozePose);
                s.Part(m, come + 0.3f, come + 1.0f, spot, front, DollStyle.Sit, TakeSeatedPose);
                s.Tip(m, come + 0.5f, 999f, CampTipKind.Flower);
                s.Part(k, come + 0.8f, come + 1.4f, by, outward, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(k, come + 1.4f, come + 5.4f, by, outward + Rot(dir, 70f) * 0.07f, DollStyle.Sit, (ref DollPose p, float u, float l) => Seated(ref p));
                s.Part(m, come + 1.0f, come + 2.2f, spot, outward, DollStyle.Sit, (ref DollPose p, float u, float l) => DozeFree(ref p));
                s.Part(m, come + 2.2f, come + 3.0f, spot, by, DollStyle.Sit, SeatedHeadPatPose);
                s.Part(m, come + 3.0f, sitFrom + sitFor, spot, outward, DollStyle.Sit, DozePose);
                s.Part(k, come + 5.4f, come + 5.4f + WalkTime(by, kidAt, KidWalk) * 0.5f, Vector2.Lerp(by, kidAt, 0.5f), kidAt, DollStyle.Stand, null, PropKind.None, KidWalk);
            }
            else s.Part(m, sitFrom, sitFrom + sitFor, spot, outward, DollStyle.Sit, DozePose);
            t = sitFrom + sitFor;
            var home = helper >= 0 ? helperAt : at;
            if (helper >= 0)
            {
                int h = s.Add(helper);
                var front = spot + dir * 0.1f;
                float wh = WalkTime(helperAt, front, Walk);
                s.Part(h, t - wh, t, front, spot, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(h, t, t + 0.9f, front, spot, DollStyle.Stand, PullUpPose);
                s.Part(m, t, t + 0.9f, spot, front, DollStyle.Stand, PulledUpPose);
                t += 0.9f;
                var side = Rot(Dir(spot, home, -dir), 90f) * 0.08f;
                float wb = WalkTime(spot, home, slow);
                s.Part(m, t, t + wb, home, home + Dir(spot, home, -dir), DollStyle.Stand, BentPose, PropKind.None, slow).poseWalking = true;
                s.Part(h, t, t + wb, home + side, home + side + Dir(spot, home, -dir), DollStyle.Stand, HandHold(false), PropKind.None, slow).poseWalking = true;
            }
            else
            {
                s.Part(m, t, t + 0.6f, spot, outward, DollStyle.Stand, StandUpPose); t += 0.6f;
                s.Part(m, t, t + WalkTime(spot, home, slow), home, home + Dir(spot, home, -dir), DollStyle.Stand, BentPose, PropKind.None, slow).poseWalking = true;
            }
            return s;
        }

        // ================================================================== B32.h the eldest tells the kids who planted it
        /// <summary>B32.h: the kids sit in a half ring facing the eldest under the tree; the eldest pats the trunk (the crown shakes), then points up
        /// at the glass; the kids turn their heads up to it one after another (0.12 s apart), one waves at it; the eldest mimes the planting
        /// (a crouch, a finger in the ground, then both arms slowly up: it grew); the kids copy, arms up, and laugh; one kid pats the trunk the
        /// way the eldest did; they scatter. <paramref name="toCam"/>: the camera's side (the glass the Keeper looks through).</summary>
        public static CampScene TreeTell(int eldest, Vector2 at, IList<int> kids, IList<Vector2> kidsAt, Vector2 tree, int stage, Vector2 toCam, float t0)
        {
            var s = new CampScene(CampUse.TreeTell, t0, TreeGeo.Visit(tree, stage, toCam)) { holdFocus = true };
            int e = s.Add(eldest);
            var es = TreeGeo.Touch(tree, stage, toCam) + toCam * 0.02f;
            var circle = es + toCam * 0.2f;
            int n = kids.Count;
            var spots = new Vector2[n];
            float t = WalkTime(at, es, Walk);
            for (int i = 0; i < n; i++)
            {
                s.Add(kids[i]);
                spots[i] = es + Rot(toCam, -55f + 110f * (n == 1 ? 0.5f : i / (float)(n - 1))) * 0.2f;
                t = Mathf.Max(t, WalkTime(kidsAt[i], spots[i], KidWalk));
            }
            s.Part(e, 0f, t, es, es + toCam, DollStyle.Stand, null, PropKind.None, Walk);
            for (int i = 0; i < n; i++) s.Part(1 + i, 0f, t, spots[i], es, DollStyle.Stand, null, PropKind.None, KidWalk);
            t += 0.6f;
            // the pat on the trunk
            s.Part(e, t - 0.6f, t, es, tree, DollStyle.Stand);
            s.Part(e, t, t + 0.7f, es, tree, DollStyle.Stand, PatPose);
            s.Fx(t + 0.3f, CampFxKind.TreeShake, G(tree), 2f);
            float kidsFrom = t - 0.6f;
            t += 0.7f;
            // the point up at the glass; the kids' heads turn up to it, one after another; one waves
            s.Part(e, t, t + 0.3f, es, es + toCam, DollStyle.Stand); t += 0.3f;
            s.Part(e, t, t + 2.2f, es, es + toCam, DollStyle.Stand, PointGlassPose);
            for (int i = 0; i < n; i++)
            {
                s.Part(1 + i, kidsFrom, t + 0.3f + 0.12f * i, spots[i], es, DollStyle.Sit);
                s.Part(1 + i, t + 0.3f + 0.12f * i, t + 2.2f, spots[i], spots[i] + toCam, i == 0 ? DollStyle.WaveSmall : DollStyle.LookUp, (ref DollPose p, float u, float l) => Seated(ref p));
            }
            t += 2.2f;
            // the planting mime; the kids copy, arms slowly up, and laugh
            s.Part(e, t, t + 3f, es, es + toCam, DollStyle.Stand, PlantMimePose);
            for (int i = 0; i < n; i++) s.Part(1 + i, t, t + 0.9f, spots[i], es, DollStyle.Sit);
            for (int i = 0; i < n; i++) s.Part(1 + i, t + 0.9f, t + 1.2f, spots[i], es, DollStyle.Stand);
            for (int i = 0; i < n; i++) s.Part(1 + i, t + 1.2f, t + 3.4f, spots[i], es, DollStyle.Stand, ArmsUpPose);
            t += 3.4f;
            s.Part(e, t - 0.4f, t + 1f, es, es + toCam, DollStyle.Laugh);
            for (int i = 0; i < n; i++) s.Part(1 + i, t, t + 1f, spots[i], es, DollStyle.Laugh);
            t += 1f;
            // one kid pats the trunk the way the eldest did; they scatter
            var patAt = TreeGeo.Touch(tree, stage, Rot(toCam, 40f));
            float wp = WalkTime(spots[0], patAt, KidWalk);
            s.Part(1, t, t + wp, patAt, tree, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(1, t + wp, t + wp + 0.7f, patAt, tree, DollStyle.Stand, PatPose);
            s.Fx(t + wp + 0.3f, CampFxKind.TreeShake, G(tree), 2f);
            for (int i = 1; i < n; i++) s.Part(1 + i, t, t + wp + 0.7f, spots[i], patAt, DollStyle.Stand);
            s.Part(e, t, t + wp + 0.7f, es, patAt, DollStyle.Stand);
            t += wp + 0.7f;
            for (int i = 0; i < n; i++)
            {
                var from = i == 0 ? patAt : spots[i];
                var away = Rot(toCam, -70f + 140f * (n == 1 ? 0.5f : i / (float)(n - 1)));
                s.Part(1 + i, t, t + 1.4f, from + away * 0.5f, from + away, DollStyle.Laugh, null, PropKind.None, KidRun).run = true;
            }
            return s;
        }
    }
}
