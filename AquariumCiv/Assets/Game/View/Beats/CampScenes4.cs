// #692 part 4 (docs/design/animation-specs.md "#692 part 4: animals", B37-B39, and B15.d feeding the ducks for B39.b): the animal uses as
// pure builders. Each takes who and where (box x, z) and returns a CampScene whose timeline follows the spec row's beats and timings. The
// animals themselves stay their views' (PondLife's frog and ducks, DeerView's herd, a rabbit kit drawn by CampLife.Animals); the scene
// carries the moments the director hands them (the grab, the release, the doe's look, the bolt, the kit's hops, the crumbs' flights).
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;

namespace AquariumCiv.View
{
    /// <summary>#692 part 4: the numbers the animal uses share (pure).</summary>
    public static class AnimalGeo
    {
        /// <summary>B38: the watchers stand this far from the nearest deer (outside DeerView's alert radius 0.6).</summary>
        public const float WatchFrom = 0.8f, WatchTo = 1.2f;
        /// <summary>B38.b: the kids creep from 1.2 u to this (the giggle or the twig there).</summary>
        public const float CreepFrom = 1.2f, CreepTo = 0.62f;
        /// <summary>B38: a herd counts as grazing at the camp's edge when its middle is within this of the fire [est: the camp's edge ~1 u out,
        /// plus the spec's 1.5 u].</summary>
        public const float HerdReach = 2.5f;
        /// <summary>B39.a: the kit is the rabbit doll at 0.6 scale.</summary>
        public const float KitScale = 0.6f;
        /// <summary>B15.d: a crumb's flight (height, seconds) onto the water.</summary>
        public const float CrumbH = 0.08f, CrumbT = 0.35f;
        /// <summary>B39.b [est]: the afternoon crumbs' chance (with the morning's "often" and the egg run, two duck uses on most days).</summary>
        public const float AfternoonCrumbs = 0.5f;

        /// <summary>B39.b [est]: the duck uses planned on a view day with ducks in the box: the egg run when one is planned that day (B15.a), the
        /// morning crumbs often, the afternoon crumbs sometimes; a day with none of those still gets the morning crumbs, so a duck use is
        /// never missing. Pure: the director and the rate test both use it.</summary>
        public static int DuckUsesPlanned(long day, int seed, bool eggRun, out bool morning, out bool afternoon)
        {
            morning = BeatHash.U01((int)(day & 0x7fffffff), 1, 6950 + (seed & 0xffff)) < CampLife.Often;
            afternoon = BeatHash.U01((int)(day & 0x7fffffff), 2, 6950 + (seed & 0xffff)) < AfternoonCrumbs;
            if (!eggRun && !morning && !afternoon) morning = true;
            return (eggRun ? 1 : 0) + (morning ? 1 : 0) + (afternoon ? 1 : 0);
        }
    }

    public static class CampScenes4
    {
        /// <summary>#695: no one in an optional part (a crowd member's key is negative, so −1 is a person).</summary>
        const int Nobody = VillageScenes.Nobody;
        const float Walk = CampScenes1.Walk, KidWalk = CampScenes1.KidWalk, KidRun = CampScenes1.KidRun;
        const float Snap = 5f;

        static float WalkTime(Vector2 a, Vector2 b, float speed) => Mathf.Clamp((b - a).magnitude / speed + 0.25f, 0.4f, 9f);
        static Vector2 Dir(Vector2 from, Vector2 to, Vector2 fallback) { var d = to - from; return d.sqrMagnitude > 1e-6f ? d.normalized : fallback; }
        static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        static Vector3 G(Vector2 p, float h = 0f) => new Vector3(p.x, h, p.y);

        // ================================================================== shared poses
        /// <summary>B6's creep at kid scale: low (squash 0.86, lean 14), arms a little forward; laid over the slow walk.</summary>
        public static void CreepPose(ref DollPose p, float t, float len)
        {
            p.squash = 0.86f; p.lean += 14f; p.offset.y -= 0.006f;
            p.armL = new Vector3(25f, 0f, -16f); p.armR = new Vector3(25f, 0f, 16f);
        }

        /// <summary>B13's freeze: stock-still mid-creep (the low pose held, the arms tucked).</summary>
        public static void FreezeLowPose(ref DollPose p, float t, float len)
        {
            p.squash = 0.84f; p.lean += 18f; p.offset.y -= 0.008f;
            p.armL = new Vector3(15f, 0f, -10f); p.armR = new Vector3(15f, 0f, 10f);
        }

        /// <summary>A crouch at the ground (squash 0.8), still.</summary>
        static void CrouchStillPose(ref DollPose p, float t, float len) { p.squash = 0.8f; p.offset.y -= 0.012f; p.lean += 10f; p.armL = new Vector3(30f, 0f, -10f); p.armR = new Vector3(30f, 0f, 10f); }

        /// <summary>Shaking off like a wet pup (B3.a's end beat): roll ±12° at 8 Hz, the arms flung out, a little bounce.</summary>
        public static void ShakeOffPose(ref DollPose p, float t, float len)
        {
            float fade = 1f - In(t, len - 0.2f, 0.2f), sw = Mathf.Sin(t * 8f * Mathf.PI * 2f);
            p.roll += sw * 12f * fade; p.yaw += sw * 10f * fade;
            p.armL = new Vector3(10f, 0f, -70f + sw * 25f); p.armR = new Vector3(10f, 0f, 70f + sw * 25f);
            p.offset.y += Mathf.Abs(sw) * 0.015f * fade;
        }

        static void ShrugPose(ref DollPose p, float t, float len)
        {
            float u = Bump(t, 0f, len);
            p.armL = Lerp(new Vector3(60f, 0f, 15f), new Vector3(30f, 0f, -50f), u); p.armR = Lerp(new Vector3(60f, 0f, -15f), new Vector3(30f, 0f, 50f), u);
            p.offset.y += 0.006f * u;
        }

        /// <summary>A grown-up shakes the head at them (yaw ±20° at 4 Hz, 0.5 s), hands on hips.</summary>
        static void HeadShakePose(ref DollPose p, float t, float len)
        {
            p.yaw += 20f * Mathf.Sin(t * 4f * Mathf.PI * 2f) * Bump(t, 0f, Mathf.Min(len, 0.5f) + 0.0001f);
            p.armL = new Vector3(0f, 0f, -55f); p.armR = new Vector3(0f, 0f, 55f);
        }

        /// <summary>The point (arm x 90, held), from a crouch beside a kid (squash 0.8) when <paramref name="crouch"/>.</summary>
        static CampPoseFn PointPose(bool crouch) => (ref DollPose p, float t, float len) =>
        {
            float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f));
            if (crouch) { p.squash = Mathf.Lerp(1f, 0.8f, In(t, 0f, 0.3f)); p.offset.y -= 0.012f * In(t, 0f, 0.3f); }
            p.armR = Lerp(p.armR, new Vector3(90f, 0f, 4f), u);
        };

        /// <summary>Watching still, the head following (a slow sway of the yaw), and with <paramref name="crouch"/> low beside the kid.</summary>
        static CampPoseFn WatchPose(bool crouch, float phase) => (ref DollPose p, float t, float len) =>
        {
            p.yaw += 8f * Mathf.Sin(t * 0.6f + phase);
            if (crouch) { p.squash = 0.8f; p.offset.y -= 0.012f; }
            p.armL = new Vector3(8f, 0f, -12f); p.armR = new Vector3(8f, 0f, 12f);
        };

        /// <summary>B37: the pounce: lean 40 and both arms down x 60, a 0.15 s snap to the ground (the hit at 0.15 s), then held low.</summary>
        public static void PouncePose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.15f);
            p.lean += 40f * k; p.squash = Mathf.Lerp(0.86f, 0.8f, k); p.offset.y -= 0.01f * k; p.offset.z -= 0.03f * k;
            p.armL = Lerp(new Vector3(100f, 0f, -12f), new Vector3(60f, 0f, -6f), k); p.armR = Lerp(new Vector3(100f, 0f, 12f), new Vector3(60f, 0f, 6f), k);
        }
        public const float PounceHit = 0.15f;

        /// <summary>B37: the cupped hands held up to the face (both arms x 130, the hands together), a peek in (lean 20, 0.6 s) at
        /// <paramref name="peekAt"/>; the frog's wriggle is the director's.</summary>
        static CampPoseFn CuppedPose(float peekAt) => (ref DollPose p, float t, float len) =>
        {
            float k = In(t, 0f, 0.35f);
            p.armL = Lerp(new Vector3(60f, 0f, -6f), new Vector3(130f, 0f, 14f), k); p.armR = Lerp(new Vector3(60f, 0f, 6f), new Vector3(130f, 0f, -14f), k);
            p.lean += 20f * Bump(t, peekAt, 0.6f);
        };

        /// <summary>B37: the kid crouches at the water and opens the hands (arms apart z ∓50, 0.3 s at <paramref name="openAt"/>).</summary>
        static CampPoseFn ReleasePose(float openAt) => (ref DollPose p, float t, float len) =>
        {
            float c = In(t, 0f, 0.3f), o = In(t, openAt, 0.3f);
            p.squash = Mathf.Lerp(1f, 0.8f, c); p.offset.y -= 0.012f * c; p.lean += 25f * c;
            p.armL = Lerp(new Vector3(70f, 0f, 14f), new Vector3(60f, 0f, -50f), o); p.armR = Lerp(new Vector3(70f, 0f, -14f), new Vector3(60f, 0f, 50f), o);
        };

        /// <summary>B37's miss: the flop in the mud (squash 0.6, lying low, 0.3 s), then still a moment.</summary>
        static void FlopPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.3f);
            p.squash = Mathf.Lerp(0.8f, 0.6f, k); p.lean += Mathf.Lerp(40f, 70f, k); p.offset.y -= 0.01f; p.offset.z -= 0.04f * k;
            p.armL = new Vector3(150f, 0f, -20f); p.armR = new Vector3(150f, 0f, 20f);
        }

        /// <summary>Sitting up from the flop (0.5 s), dripping.</summary>
        static void SitUpPose(ref DollPose p, float t, float len) { float k = In(t, 0f, 0.5f); p.lean += 70f * (1f - k); p.offset.z -= 0.04f * (1f - k); p.squash = Mathf.Lerp(0.6f, 0.78f, k); p.offset.y -= 0.03f * k; }

        /// <summary>The friend leaning in to see (lean 25, the hands on the knees).</summary>
        static void LeanInPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f)); p.lean += 25f * u; p.armL = Lerp(p.armL, new Vector3(30f, 0f, -8f), u); p.armR = Lerp(p.armR, new Vector3(30f, 0f, 8f), u); }

        // ================================================================== B37 frogs
        /// <summary>B37: <paramref name="kid"/> creeps to the frog on the bank (3 creep steps), pounces (a muddy splash of 3 drops). A catch: the
        /// frog is in the cupped hands (<see cref="CampScene.a"/>: the grab, the director moves it there), held up to the face, peeked at, it
        /// wriggles; the friend leans in; the kid crouches at the water and opens the hands: the frog hops in (<see cref="CampScene.b"/>: the
        /// release) with a ring; the kid waves. A <paramref name="miss"/>: the frog hops in first (<see cref="CampScene.b"/> at the pounce,
        /// <see cref="CampScene.a"/> −1), the kid flops (6 drops), sits up and shakes off; the friend laughs.</summary>
        public static CampScene FrogCatch(int kid, int friend, Vector2 kidAt, Vector2 friendAt, Vector2 frog, Vector2 toWater, bool miss, float t0)
        {
            toWater = toWater.sqrMagnitude > 1e-6f ? toWater.normalized : Vector2.up;
            var s = new CampScene(CampUse.FrogCatch, t0, frog) { holdFocus = true };
            int m = s.Add(kid);
            // they come at it from the land side
            var start = frog - toWater * 0.2f;
            var near = frog - toWater * 0.07f;
            float t = WalkTime(kidAt, start, KidWalk);
            s.Part(m, 0f, t, start, frog, DollStyle.Stand, null, PropKind.None, KidWalk);
            int f = -1; Vector2 fs = default;
            if (friend != Nobody)
            {
                f = s.Add(friend);
                fs = start + Rot(-toWater, 60f) * 0.13f;
                s.Part(f, 0f, t + 0.2f, fs, frog, DollStyle.Stand, null, PropKind.None, KidWalk);
            }
            // three creep steps (0.9 s each, B6's rhythm), the last ends at the pounce
            float creep = 3f * 0.9f;
            s.Part(m, t, t + creep, near, frog, DollStyle.Stand, CreepPose, PropKind.None, (start - near).magnitude / creep + 0.005f).poseWalking = true;
            if (f >= 0) s.Part(f, t + 0.2f, t + creep, fs, frog, DollStyle.Stand, CreepPose);
            t += creep;
            float pounce = t;
            if (miss) s.b = pounce; // the frog is quicker
            s.Part(m, t, t + (miss ? 0.25f : 0.5f), near, frog, DollStyle.Stand, PouncePose);
            s.Fx(t + PounceHit, CampFxKind.Droplets, G(frog, 0.01f), 0.35f, 3);
            if (!miss)
            {
                if (f >= 0) s.Part(f, t, t + 0.5f, fs, frog, DollStyle.Stand);
                s.a = t + PounceHit;
                t += 0.5f;
                // stand up holding it up to the face, the peek, the wriggle (the director's)
                s.Part(m, t, t + 2.4f, near, Rot(toWater, 180f) + near, DollStyle.Stand, CuppedPose(0.9f));
                if (f >= 0) s.Part(f, t, t + 2.4f, near + Rot(-toWater, 60f) * 0.075f, near, DollStyle.Stand, LeanInPose, PropKind.None, KidWalk);
                s.c = t + 0.4f; // the wriggle from here
                t += 2.4f;
                // crouch at the water and open the hands
                var edge = frog - toWater * 0.03f;
                s.Part(m, t, t + 0.4f, edge, edge + toWater, DollStyle.Stand, null, PropKind.None, KidWalk);
                if (f >= 0) s.Part(f, t, t + 1.4f, fs, edge, DollStyle.Stand);
                t += 0.4f;
                s.Part(m, t, t + 1.0f, edge, edge + toWater, DollStyle.Stand, ReleasePose(0.4f));
                s.b = t + 0.55f; // the hands open: it hops
                s.Fx(s.b + 0.5f, CampFxKind.Ring, G(frog + toWater * 0.1f, 0.005f), 0.5f);
                t += 1.0f;
                s.Part(m, t, t + 1.4f, edge, edge + toWater, DollStyle.WaveSmall);
                if (f >= 0) s.Part(f, t, t + 1.4f, fs, edge + toWater, DollStyle.WaveSmall);
                t += 1.4f;
            }
            else
            {
                s.a = -1f;
                s.Fx(pounce + 0.5f, CampFxKind.Ring, G(frog + toWater * 0.1f, 0.005f), 0.5f);
                t += 0.25f;
                s.Part(m, t, t + 1.0f, near + toWater * 0.03f, frog, DollStyle.Stand, FlopPose, PropKind.None, Snap);
                s.Fx(t + 0.25f, CampFxKind.Droplets, G(near + toWater * 0.03f, 0.01f), 0.45f, 6);
                t += 1.0f;
                s.Part(m, t, t + 0.5f, near + toWater * 0.03f, frog, DollStyle.Sit, SitUpPose); t += 0.5f;
                s.Part(m, t, t + 0.6f, near + toWater * 0.03f, frog, DollStyle.Stand); t += 0.6f;
                s.Part(m, t, t + 0.9f, near + toWater * 0.03f, frog, DollStyle.Stand, ShakeOffPose);
                s.Fx(t + 0.3f, CampFxKind.Droplets, new Vector3(0f, 0.01f, 0f), 0.3f, 3, CampAnchor.Head, m);
                t += 0.9f;
                s.Part(m, t, t + 1.2f, near + toWater * 0.03f, fs, DollStyle.Laugh); t += 1.2f;
                if (f >= 0) s.Part(f, pounce, t, fs, near, DollStyle.Laugh); // the friend laughs through it
            }
            // they go
            var away = start - toWater * 0.35f + Rot(toWater, 90f) * 0.1f;
            s.Part(m, t, t + WalkTime(near, away, KidWalk), away, away - toWater, DollStyle.Stand, null, PropKind.None, KidWalk);
            if (f >= 0) s.Part(f, t, t + WalkTime(fs, away, KidWalk), away + Rot(toWater, 90f) * 0.08f, away - toWater, DollStyle.Stand, null, PropKind.None, KidWalk);
            return s;
        }

        // ================================================================== B38.a watch the deer
        /// <summary>B38.a: a grown-up and 1–2 kids stop at the wood's edge <see cref="AnimalGeo.WatchFrom"/>–<see cref="AnimalGeo.WatchTo"/> from
        /// the nearest deer, side by side; the grown-up crouches beside a kid and points (1.5 s); they watch <paramref name="watch"/> (6–10 s),
        /// the heads following; a doe looks up (<see cref="CampScene.a"/>: the director's alert) and everyone freezes until she grazes
        /// (<see cref="DeerView.AlertSeconds"/> + 1 s); two slow steps back; they turn and go.</summary>
        public static CampScene DeerWatch(IList<int> who, IList<Vector2> at, int grown, Vector2 deer, Vector2 camp, float watch, float t0)
        {
            watch = Mathf.Clamp(watch, 6f, 10f);
            var fromDeer = Dir(deer, camp, Vector2.down);
            var across = new Vector2(-fromDeer.y, fromDeer.x);
            var mid = deer + fromDeer * 0.85f;
            var s = new CampScene(CampUse.DeerWatch, t0, Vector2.Lerp(mid, deer, 0.3f)) { holdFocus = true };
            int n = who.Count;
            var spots = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                s.Add(who[i]);
                spots[i] = mid + across * ((i - (n - 1) * 0.5f) * 0.09f);
                t = Mathf.Max(t, WalkTime(at[i], spots[i], who[i] == grown ? Walk : KidWalk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, spots[i], deer, DollStyle.Stand, null, PropKind.None, who[i] == grown ? Walk : KidWalk);
            int g = who.IndexOf(grown);
            // the point
            for (int i = 0; i < n; i++) s.Part(i, t, t + 1.5f, spots[i], deer, DollStyle.Stand, i == g ? PointPose(true) : WatchPose(false, i));
            t += 1.5f;
            float look = t + watch * 0.45f, freeze = DeerView.AlertSeconds + 1f;
            s.a = look;
            for (int i = 0; i < n; i++)
            {
                s.Part(i, t, look, spots[i], deer, DollStyle.Stand, WatchPose(i == g, i));
                s.Part(i, look + 0.15f, look + 0.15f + freeze, spots[i], deer, DollStyle.Freeze);
                s.Part(i, look, look + 0.15f, spots[i], deer, DollStyle.Freeze);
                s.Part(i, look + 0.15f + freeze, t + watch, spots[i], deer, DollStyle.Stand, WatchPose(i == g, i + 2f));
            }
            t += Mathf.Max(watch, look - t + 0.15f + freeze + 1f);
            // two slow steps back, then they turn and go
            for (int i = 0; i < n; i++) s.Part(i, t, t + 1.2f, spots[i] + fromDeer * 0.06f, deer, DollStyle.Stand, null, PropKind.None, 0.05f);
            t += 1.2f;
            for (int i = 0; i < n; i++)
            {
                var away = spots[i] + fromDeer * 0.5f + across * ((i - (n - 1) * 0.5f) * 0.05f);
                s.Part(i, t, t + WalkTime(spots[i], away, Walk), away, away + fromDeer, DollStyle.Stand, null, PropKind.None, Walk);
            }
            return s;
        }

        // ================================================================== B38.b kids creep up on the deer
        /// <summary>B38.b: 2–3 kids crouch-creep in a line from <see cref="AnimalGeo.CreepFrom"/> toward the deer; they freeze once when a deer lifts
        /// its head (<see cref="CampScene.c"/>: the director's look-up); at <see cref="AnimalGeo.CreepTo"/> one giggles (or steps on a twig, a
        /// chip at the foot) → <see cref="CampScene.a"/>: the herd's alert and bolt (the director's knock); the kids jump up and run after them
        /// 0.3 u, arms out, stop, the arms drop, they look at each other, one shrugs and they all laugh; they walk back and a grown-up shakes
        /// the head at them.</summary>
        public static CampScene DeerCreep(IList<int> kids, IList<Vector2> at, int grown, Vector2 grownAt, Vector2 deer, Vector2 camp, bool twig, float t0)
        {
            var fromDeer = Dir(deer, camp, Vector2.down);
            var across = new Vector2(-fromDeer.y, fromDeer.x);
            var s = new CampScene(CampUse.DeerCreep, t0, deer + fromDeer * 0.6f) { holdFocus = true };
            int n = kids.Count;
            var line = new Vector2[n]; var close = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                s.Add(kids[i]);
                var off = across * ((i - (n - 1) * 0.5f) * 0.08f);
                line[i] = deer + fromDeer * AnimalGeo.CreepFrom + off;
                close[i] = deer + fromDeer * AnimalGeo.CreepTo + off;
                t = Mathf.Max(t, WalkTime(at[i], line[i], KidWalk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, line[i], deer, DollStyle.Stand, null, PropKind.None, KidWalk);
            // the creep in two halves with a freeze between (a deer lifts its head)
            float half = 3f * 0.9f, gap = (AnimalGeo.CreepFrom - AnimalGeo.CreepTo) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var mid = Vector2.Lerp(line[i], close[i], 0.5f);
                s.Part(i, t, t + half, mid, deer, DollStyle.Stand, CreepPose, PropKind.None, gap / half + 0.005f).poseWalking = true;
                s.Part(i, t + half, t + half + 1.6f, mid, deer, DollStyle.Stand, FreezeLowPose);
                s.Part(i, t + half + 1.6f, t + half * 2f + 1.6f, close[i], deer, DollStyle.Stand, CreepPose, PropKind.None, gap / half + 0.005f).poseWalking = true;
            }
            s.c = t + half + 0.1f;
            t += half * 2f + 1.6f;
            // the giggle or the twig → the alert, then the bolt
            int who = 0;
            s.Part(who, t, t + 0.4f, close[who], deer, DollStyle.Stand, twig ? (CampPoseFn)FreezeLowPose : (ref DollPose p, float u, float l) => { FreezeLowPose(ref p, u, l); p.offset.y += 0.006f * Mathf.Abs(Mathf.Sin(u * 9f * Mathf.PI)); });
            for (int i = 1; i < n; i++) s.Part(i, t, t + 0.4f, close[i], deer, DollStyle.Stand, FreezeLowPose);
            s.a = t + 0.2f;
            if (twig) s.Fx(s.a, CampFxKind.Chips, G(close[who], 0.01f), 0.35f, 1);
            t += 0.4f + DeerView.KnockLook;
            // up and after them 0.3 u, arms out; stop; the arms drop
            for (int i = 0; i < n; i++)
            {
                var run = close[i] - fromDeer * 0.3f;
                s.Part(i, t, t + 0.8f, run, deer, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = new Vector3(70f, 0f, -40f); p.armR = new Vector3(70f, 0f, 40f); }, PropKind.None, KidRun).run = true;
            }
            t += 0.8f;
            for (int i = 0; i < n; i++)
            {
                var stop = close[i] - fromDeer * 0.3f;
                s.Part(i, t, t + 0.5f, stop, deer, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.4f); p.armL = Lerp(new Vector3(70f, 0f, -40f), p.armL, k); p.armR = Lerp(new Vector3(70f, 0f, 40f), p.armR, k); });
                int other = (i + 1) % n;
                s.Part(i, t + 0.5f, t + 1.1f, stop, close[other] - fromDeer * 0.3f, DollStyle.Stand); // look at each other
                if (i == n - 1) s.Part(i, t + 1.1f, t + 1.8f, stop, camp, DollStyle.Stand, ShrugPose);
                else s.Part(i, t + 1.1f, t + 1.8f, stop, close[n - 1], DollStyle.Stand);
                s.Part(i, t + 1.8f, t + 3.0f, stop, close[other], DollStyle.Laugh);
            }
            t += 3.0f;
            // walk back; a grown-up shakes the head at them
            var back = deer + fromDeer * (AnimalGeo.CreepFrom + 0.3f);
            for (int i = 0; i < n; i++)
            {
                var b = back + across * ((i - (n - 1) * 0.5f) * 0.08f);
                s.Part(i, t, t + WalkTime(close[i], b, KidWalk), b, b + fromDeer, DollStyle.Stand, null, PropKind.None, KidWalk);
            }
            if (grown != Nobody)
            {
                int g = s.Add(grown);
                var gs = back + fromDeer * 0.12f;
                float arrive = Mathf.Max(0.5f, t - WalkTime(grownAt, gs, Walk));
                s.Part(g, arrive, t, gs, back, DollStyle.Stand, null, PropKind.None, Walk);
                float wb = WalkTime(close[0], back, KidWalk);
                s.Part(g, t, t + wb, gs, back, DollStyle.Stand);
                s.Part(g, t + wb, t + wb + 0.9f, gs, back, DollStyle.Stand, HeadShakePose);
            }
            return s;
        }

        // ================================================================== B39.a a kid holds a kit
        /// <summary>The scoop (both arms x 50 → 70 over 0.8 s, smooth) from a crouch, to the hold at the chest (x 60).</summary>
        static void ScoopPose(ref DollPose p, float t, float len)
        {
            float k = In(t, 0f, 0.8f), up = In(t, 0.8f, Mathf.Max(0.01f, len - 0.8f));
            p.squash = Mathf.Lerp(0.8f, 1f, up); p.offset.y -= 0.012f * (1f - up); p.lean += 20f * (1f - up);
            float x = Mathf.Lerp(Mathf.Lerp(50f, 70f, k), 60f, up);
            p.armL = new Vector3(x, 0f, 16f); p.armR = new Vector3(x, 0f, -16f);
        }

        /// <summary>Holding the kit at the chest (x 60); one arm strokes it (x 70 ↔ 90, 3 strokes in 1.5 s) from <paramref name="strokeAt"/>.</summary>
        static CampPoseFn HoldKitPose(float strokeAt) => (ref DollPose p, float t, float len) =>
        {
            p.armL = new Vector3(60f, 0f, 16f); p.armR = new Vector3(60f, 0f, -16f);
            float u = t - strokeAt;
            if (u >= 0f && u < 1.5f) p.armR = new Vector3(80f - 10f * Mathf.Cos(u / 0.5f * Mathf.PI * 2f), 0f, -10f);
        };

        /// <summary>Showing the kit to the other kid (arms out x 80).</summary>
        static void ShowKitPose(ref DollPose p, float t, float len) { float k = In(t, 0f, 0.4f); p.armL = Lerp(new Vector3(60f, 0f, 16f), new Vector3(80f, 0f, 10f), k); p.armR = Lerp(new Vector3(60f, 0f, -16f), new Vector3(80f, 0f, -10f), k); p.lean += 6f * k; }

        /// <summary>Setting it down (crouch, arms x 40).</summary>
        static void SetDownPose(ref DollPose p, float t, float len) { float k = In(t, 0f, 0.5f); p.squash = Mathf.Lerp(1f, 0.8f, k); p.offset.y -= 0.012f * k; p.lean += 25f * k; p.armL = Lerp(new Vector3(80f, 0f, 10f), new Vector3(40f, 0f, 10f), k); p.armR = Lerp(new Vector3(80f, 0f, -10f), new Vector3(40f, 0f, -10f), k); }

        /// <summary>B39.a: <paramref name="kid"/> crouches very still at the meadow's edge (3 s); a kit hops up from its mother (<see cref="CampScene.p0"/>)
        /// to the kid's hands (<see cref="CampScene.c"/> → <see cref="CampScene.a"/>: the hops in); the kid scoops it slowly and holds it at the
        /// chest, the ears twitching; strokes it three times; turns and shows it to <paramref name="other"/>; sets it down
        /// (<see cref="CampScene.b"/>): it hops back to its mother in two hops (<see cref="CampScene.d"/>: back); the kid waves after it.</summary>
        public static CampScene KitHold(int kid, int other, Vector2 kidAt, Vector2 otherAt, Vector2 spot, Vector2 mother, float t0)
        {
            var toMeadow = Dir(spot, mother, Vector2.up);
            var s = new CampScene(CampUse.KitHold, t0, Vector2.Lerp(spot, mother, 0.3f)) { holdFocus = true };
            int m = s.Add(kid);
            float t = WalkTime(kidAt, spot, KidWalk);
            s.Part(m, 0f, t, spot, mother, DollStyle.Stand, null, PropKind.None, KidWalk);
            int o = -1; Vector2 os = spot - toMeadow * 0.25f + Rot(toMeadow, 90f) * 0.12f;
            if (other != Nobody) { o = s.Add(other); s.Part(o, 0f, t + 0.5f, os, spot, DollStyle.Stand, null, PropKind.None, KidWalk); }
            s.Part(m, t, t + 3f, spot, mother, DollStyle.Stand, CrouchStillPose);
            if (o >= 0) s.Part(o, t + 0.5f, t + 3f, os, spot, DollStyle.Stand, CrouchStillPose);
            s.p0 = mother; s.p1 = spot + toMeadow * 0.045f;
            s.c = t + 1.2f; // the kit sets off
            s.a = t + 3f;   // in the hands
            t += 3f;
            s.Part(m, t, t + 1.4f, spot, mother, DollStyle.Stand, ScoopPose);
            if (o >= 0) s.Part(o, t, t + 1.4f, os, spot, DollStyle.Stand, CrouchStillPose);
            t += 1.4f;
            s.Part(m, t, t + 3.2f, spot, mother, DollStyle.Stand, HoldKitPose(1.2f));
            if (o >= 0) s.Part(o, t, t + 3.2f, os, spot, DollStyle.Stand);
            t += 3.2f;
            // turn and show it
            var face = o >= 0 ? os : spot - toMeadow;
            s.Part(m, t, t + 1.6f, spot, face, DollStyle.Stand, ShowKitPose);
            if (o >= 0) s.Part(o, t, t + 1.6f, os + Dir(os, spot, toMeadow) * 0.04f, spot, DollStyle.Stand, LeanInPose, PropKind.None, KidWalk);
            t += 1.6f;
            s.Part(m, t, t + 0.9f, spot, mother, DollStyle.Stand, SetDownPose);
            if (o >= 0) s.Part(o, t, t + 0.9f, os + Dir(os, spot, toMeadow) * 0.04f, spot, DollStyle.Stand);
            s.b = t + 0.6f;
            t += 0.9f;
            s.d = s.b + 1.2f; // two hops back
            s.Part(m, t, t + 1.8f, spot, mother, DollStyle.WaveSmall);
            if (o >= 0) s.Part(o, t, t + 1.8f, os, mother, DollStyle.WaveSmall);
            t += 1.8f;
            var away = spot - toMeadow * 0.4f;
            s.Part(m, t, t + WalkTime(spot, away, KidWalk), away, away - toMeadow, DollStyle.Stand, null, PropKind.None, KidWalk);
            return s;
        }

        /// <summary>B39.a: where the kit is at scene second <paramref name="u"/>, and its act (null: not drawn): hops from the mother to the hands,
        /// held, set down, two hops back (it sits at the mother's side 0.6 s, then is gone among the rabbits [est]).</summary>
        public static bool KitAt(CampScene s, float u, out Vector2 at, out bool held, out bool hopping, out float hopPhase)
        {
            at = default; held = false; hopping = false; hopPhase = 0f;
            if (u < s.c || u > s.d + 0.6f) return false;
            var mother = s.p0; var hands = s.p1;
            if (u < s.a)
            {   // hops in: 4 hops, a pause between
                float k = (u - s.c) / Mathf.Max(0.01f, s.a - s.c);
                int hop = Mathf.Min(3, Mathf.FloorToInt(k * 4f)); float w = k * 4f - hop;
                float step = Mathf.Clamp01(w / 0.6f);
                at = Vector2.Lerp(mother + (hands - mother).normalized * 0.06f, hands, (hop + step) / 4f);
                hopping = w < 0.6f; hopPhase = step;
                return true;
            }
            if (u < s.b) { held = true; at = hands; return true; }
            if (u < s.d)
            {
                float k = (u - s.b) / Mathf.Max(0.01f, s.d - s.b);
                int hop = Mathf.Min(1, Mathf.FloorToInt(k * 2f)); float w = k * 2f - hop;
                float step = Mathf.Clamp01(w / 0.7f);
                at = Vector2.Lerp(hands, mother + (hands - mother).normalized * 0.06f, (hop + step) / 2f);
                hopping = w < 0.7f; hopPhase = step;
                return true;
            }
            at = mother + (hands - mother).normalized * 0.06f;
            return true;
        }

        // ================================================================== B15.d feed the ducks
        /// <summary>A throw underarm (arm back x −30 → forward x 110, the release at 0.35 s).</summary>
        public static void ThrowPose(ref DollPose p, float t, float len)
        {
            float back = In(t, 0f, 0.25f), fwd = In(t, 0.25f, 0.12f), down = In(t, 0.5f, Mathf.Max(0.01f, len - 0.5f));
            float x = Mathf.Lerp(Mathf.Lerp(20f, -30f, back), 110f, fwd);
            p.armR = Lerp(new Vector3(x, 0f, 10f), new Vector3(20f, 0f, 12f), down);
            p.lean += 8f * fwd * (1f - down);
        }
        public const float ThrowRelease = 0.37f, ThrowSeconds = 0.9f;

        /// <summary>B15.d: 2–3 kids at the bank throw crumbs (berry-sized bits, <see cref="AnimalGeo.CrumbH"/>, <see cref="AnimalGeo.CrumbT"/> onto
        /// the water: a ring each); the ducks come fast (the director's lure from t 0); one climbs out (<see cref="CampScene.a"/>) and a kid runs
        /// from it (B2's runner), the others laugh; the ducks peck up the last crumbs (until <see cref="CampScene.b"/>); the kids walk off.</summary>
        public static CampScene FeedDucks(IList<int> kids, IList<Vector2> at, Vector2 bank, Vector2 toWater, float t0)
        {
            toWater = toWater.sqrMagnitude > 1e-6f ? toWater.normalized : Vector2.up;
            var across = new Vector2(-toWater.y, toWater.x);
            var s = new CampScene(CampUse.FeedDucks, t0, bank + toWater * 0.15f) { holdFocus = true };
            int n = kids.Count;
            var spots = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++)
            {
                s.Add(kids[i]);
                spots[i] = bank - toWater * 0.04f + across * ((i - (n - 1) * 0.5f) * 0.12f);
                t = Mathf.Max(t, WalkTime(at[i], spots[i], KidWalk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, spots[i], spots[i] + toWater, DollStyle.Stand, null, PropKind.None, KidWalk);
            // three rounds of throws, each kid a beat after the last
            for (int r = 0; r < 3; r++)
            {
                float roundEnd = t + ThrowSeconds + n * 0.3f + 0.6f;
                for (int i = 0; i < n; i++)
                {
                    float a = t + i * 0.3f;
                    if (i > 0) s.Part(i, t, a, spots[i], spots[i] + toWater, DollStyle.Stand);
                    s.Part(i, a, a + ThrowSeconds, spots[i], spots[i] + toWater, DollStyle.Stand, ThrowPose);
                    s.Part(i, a + ThrowSeconds, roundEnd, spots[i], spots[i] + toWater, DollStyle.Stand);
                    s.Tip(i, a, a + ThrowRelease, CampTipKind.Berry, 0.6f);
                    var land = spots[i] + toWater * (0.16f + 0.05f * ((r + i) % 3)) + across * (0.03f * (r - 1));
                    s.arcs.Add(new CampArc { member = i, from = a + ThrowRelease, seconds = AnimalGeo.CrumbT, to = G(land), h = AnimalGeo.CrumbH });
                    s.Fx(a + ThrowRelease + AnimalGeo.CrumbT, CampFxKind.Ring, G(land, 0.004f), 0.3f);
                }
                t = roundEnd;
            }
            // a duck climbs out; the nearest kid runs from it, the others laugh
            s.a = t;
            int runner = 0;
            var runTo = spots[runner] - toWater * 0.45f + across * 0.15f;
            s.Part(runner, t + 0.8f, t + 0.8f + WalkTime(spots[runner], runTo, KidRun), runTo, runTo - toWater, DollStyle.Laugh, null, PropKind.None, KidRun).run = true;
            s.Part(runner, t, t + 0.8f, spots[runner], spots[runner] + toWater, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean -= 8f * In(u, 0.3f, 0.3f); p.armL = new Vector3(40f, 0f, -30f); p.armR = new Vector3(40f, 0f, 30f); });
            for (int i = 1; i < n; i++) s.Part(i, t, t + 3.2f, spots[i], spots[runner], DollStyle.Laugh);
            t += 3.2f;
            s.b = t + 2f; // the ducks peck the last crumbs while the kids go
            for (int i = 1; i < n; i++)
            {
                var away = spots[i] - toWater * 0.45f + across * (0.1f * i);
                s.Part(i, t, t + WalkTime(spots[i], away, KidWalk), away, away - toWater, DollStyle.Stand, null, PropKind.None, KidWalk);
            }
            return s;
        }
    }
}
