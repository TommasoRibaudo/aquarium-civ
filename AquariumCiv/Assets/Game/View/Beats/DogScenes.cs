// #694 (docs/design/animation-specs.md B24.b, B25, "#694: dogs and wolves in the people's day" B55-B56): the dogs' scenes and the people's
// parts with the dogs and the wolves, as pure builders (the same kit as #692/#693: CampScene parts, effects, tips), plus the dog's and the
// visiting wolf's own tracks (CampScene.quads: actor 0 the dog, 1 the wolf). View/CampLife.Dogs.cs casts and draws them.
// The rules (decisions.md 2026-11-07): no wolf is ever killed or touched; a wolf keeps 0.3 u from every person and 0.6 u from the people
// driving it off; wolves never come in among the tents. WolfDriveOff plans the wolf's points so they hold, and the tests check every point.
using System.Collections.Generic;
using AquariumCiv.Sim.Eco;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    /// <summary>#694: the dogs' and the visiting wolf's numbers (box units, seconds).</summary>
    public static class DogGeo
    {
        /// <summary>The pup follows its keeper this far behind (B7.c-style).</summary>
        public const float PupBehind = 0.1f;
        /// <summary>A dog's walk, trot and run; the wolf's trot.</summary>
        public const float DogWalk = 0.12f, DogTrot = 0.3f, DogRun = 0.6f, WolfTrot = 0.22f;
        /// <summary>B56.e: the dog sleeps at the fire ring's edge, this far from the fire (never in it); the pup by the embers a little nearer.</summary>
        public const float FireSleep = 0.25f, EmberSleep = 0.22f;
        /// <summary>B25: the grown dog lies in the sun this far out from its keeper's tent's shown side.</summary>
        public const float SunOut = 0.14f;
        /// <summary>B56.d: the kid's seat on a big dog's back (+0.1 u) and the ride (0.4 u, slowly).</summary>
        public const float RideSeat = 0.1f, RideLen = 0.4f, RideSpeed = 0.07f;
        /// <summary>B56.c: the stick's arc (h 0.12, 0.5 s) and how far it flies.</summary>
        public const float FetchH = 0.12f, FetchT = 0.5f, FetchFar = 0.45f;
        /// <summary>B25.b: the bark's hop forward (each of three) and the bark's beat.</summary>
        public const float BarkHop = 0.03f, BarkEvery = 0.6f;
        /// <summary>B24.b: the grown-up chases the fish thief this far and gives up; the thief runs this far.</summary>
        public const float ChaseFor = 0.5f, ThiefRun = 0.65f;
        /// <summary>B55.b: the kid presses in this close to the grown-up.</summary>
        public const float PressIn = 0.04f;
        /// <summary>The visiting wolf comes in from this far beyond its stop (out of the dark).</summary>
        public const float WolfFrom = 0.9f;
    }

    /// <summary>
    /// #694 B55.a / B25.b: where a wolf that comes near the camp stands and goes, and where the people who drive it off stop. The wolf comes
    /// along a ray from the fire (out of the dark), stops within <see cref="NearEdge"/> of the camp's edge (outside the tent ring and the
    /// firelight's edge), backs off <see cref="BackOff"/>, then trots away along the ray. The drivers walk <see cref="Step"/> toward it but
    /// never nearer than <see cref="MinGap"/>. A ray that crosses the tent ring is turned (15 degree steps) until every point is clear.
    /// Pure: no Unity objects (Vector2 maths only), so EditMode tests check every point.
    /// </summary>
    public static class WolfDriveOff
    {
        /// <summary>The wolf comes within this of the camp's edge; the people walk this far toward it; never nearer than this to them; it
        /// backs off this far; it trots away to this far from the fire (or the land's end).</summary>
        public const float NearEdge = 1.2f, Step = 0.3f, MinGap = 0.6f, BackOff = 0.2f, AwayTo = 2.6f;
        /// <summary>The check's sample step along the wolf's path.</summary>
        public const float Sample = 0.04f;

        public struct Plan
        {
            public bool ok;
            /// <summary>The unit way from the fire out to the wolf.</summary>
            public Vector2 dir;
            public Vector2 wolfStart, wolfStop, wolfBack, wolfAway;
            public Vector2[] driverStop;
        }

        /// <summary>The camp's edge: the farthest tent ring circle's far side from the fire (0.4 u with no tents).</summary>
        public static float CampEdge(WolfGround g)
        {
            float e = 0.4f;
            for (int k = 0; k + 2 < g.ring.Count; k += 3)
                e = Mathf.Max(e, new Vector2(g.ring[k] - g.fireX, g.ring[k + 1] - g.fireZ).magnitude + g.ring[k + 2]);
            return e;
        }

        static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }

        static bool Clear(WolfGround g, Vector2 a, Vector2 b)
        {
            float len = (b - a).magnitude; int n = Mathf.Max(1, Mathf.CeilToInt(len / Sample));
            for (int i = 0; i <= n; i++) { var p = Vector2.Lerp(a, b, i / (float)n); if (!WolfRules.OnLand(p.x, p.y) || WolfRules.InTentRing(p.x, p.y, g.ring)) return false; }
            return true;
        }

        /// <summary>The plan for a wolf coming from <paramref name="toward"/> (its den or where it stands), with the people who drive it off
        /// starting at <paramref name="drivers"/> (none: a guard, or a wolf only watched). <paramref name="others"/> are everyone else drawn
        /// (x, z pairs): the wolf keeps <see cref="WolfRules.PersonGap"/> from them all.</summary>
        public static Plan Make(WolfGround g, Vector2 toward, IList<Vector2> drivers, IList<float> others = null)
        {
            var fire = new Vector2(g.fireX, g.fireZ);
            var d0 = toward - fire; d0 = d0.sqrMagnitude > 1e-6f ? d0.normalized : Vector2.up;
            float edge = CampEdge(g);
            float stopR = Mathf.Max(WolfRules.FirelightEdge + 0.05f, edge + NearEdge - 0.05f);
            for (int k = 0; k < 24; k++)
            {
                float deg = (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 15f;
                var dir = Rot(d0, deg);
                // #694 clips: it stops at the first clear point past the firelight's edge on its way in (outside the tent ring), so the fire,
                // the people and the wolf share one frame; never past the camp's edge + 1.2
                float r = WolfRules.FirelightEdge + 0.1f;
                while (r < stopR && WolfRules.InTentRing(fire.x + dir.x * r, fire.y + dir.y * r, g.ring)) r += 0.05f;
                var stop = fire + dir * Mathf.Min(r, stopR);
                // the land may end before the stop: pull it in, never nearer than the firelight's edge
                while (!WolfRules.OnLand(stop.x, stop.y) && (stop - fire).magnitude > WolfRules.FirelightEdge + 0.05f) stop -= dir * 0.05f;
                if (!WolfRules.OnLand(stop.x, stop.y) || WolfRules.InTentRing(stop.x, stop.y, g.ring)) continue;
                var start = stop + dir * DogGeo.WolfFrom;
                while (!WolfRules.OnLand(start.x, start.y) && (start - stop).magnitude > 0.1f) start -= dir * 0.05f;
                var back = stop + dir * BackOff;
                if (!WolfRules.OnLand(back.x, back.y)) back = stop;
                var away = fire + dir * Mathf.Max(AwayTo, stopR + 0.6f);
                while (!WolfRules.OnLand(away.x, away.y) && (away - back).magnitude > 0.05f) away -= dir * 0.05f;
                if (!Clear(g, start, stop) || !Clear(g, stop, away)) continue;
                var plan = new Plan { ok = true, dir = dir, wolfStart = start, wolfStop = stop, wolfBack = back, wolfAway = away, driverStop = new Vector2[drivers?.Count ?? 0] };
                bool fine = true;
                for (int i = 0; i < plan.driverStop.Length; i++)
                {
                    var at = drivers[i];
                    var to = stop - at; float dist = to.magnitude;
                    float walk = Mathf.Clamp(dist - MinGap - 0.06f, 0f, Step);
                    var st = dist > 1e-4f ? at + to / dist * walk : at;
                    if ((st - stop).magnitude < MinGap + 0.02f) st = stop - (to.sqrMagnitude > 1e-6f ? to.normalized : dir) * (MinGap + 0.06f);
                    plan.driverStop[i] = st;
                    if (MinTo(plan, at) < MinGap || MinTo(plan, st) < MinGap) fine = false;
                }
                if (others != null)
                    for (int i = 0; i + 1 < others.Count; i += 2)
                        if (MinTo(plan, new Vector2(others[i], others[i + 1])) < WolfRules.PersonGap + 0.02f) fine = false;
                if (fine) return plan;
            }
            return new Plan { ok = false, dir = d0, driverStop = new Vector2[drivers?.Count ?? 0] };
        }

        /// <summary>The wolf's nearest approach to a point on its whole path (start, stop, back, away).</summary>
        public static float MinTo(in Plan p, Vector2 q) => Mathf.Min(SegDist(p.wolfStart, p.wolfStop, q), Mathf.Min(SegDist(p.wolfStop, p.wolfBack, q), SegDist(p.wolfBack, p.wolfAway, q)));

        public static float SegDist(Vector2 a, Vector2 b, Vector2 q)
        {
            var ab = b - a; float l2 = ab.sqrMagnitude;
            float t = l2 < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / l2);
            return (a + ab * t - q).magnitude;
        }

        /// <summary>Points along the wolf's whole path every <see cref="Sample"/> (the tests walk them).</summary>
        public static List<Vector2> Points(in Plan p)
        {
            var r = new List<Vector2>();
            void Seg(Vector2 a, Vector2 b) { int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / Sample)); for (int i = 0; i <= n; i++) r.Add(Vector2.Lerp(a, b, i / (float)n)); }
            Seg(p.wolfStart, p.wolfStop); Seg(p.wolfStop, p.wolfBack); Seg(p.wolfBack, p.wolfAway);
            return r;
        }

        /// <summary>What a camp does about a wolf near at night: a grown dog guards (B25.b); else the grown-ups drive it off (B55.a); else
        /// nobody goes out, the kids are only held close (B55.b).</summary>
        public enum Answer { Guard, DriveOff, HoldKids }
        public static Answer AnswerFor(bool grownDog, int grownUpsNear) => grownDog ? Answer.Guard : grownUpsNear >= 2 ? Answer.DriveOff : Answer.HoldKids;
    }

    public static class DogScenes
    {
        const int Dog = 0, Wolf = 1;
        static Vector2 Across(Vector2 toCam) => new Vector2(-toCam.y, toCam.x);
        static float QTime(Vector2 a, Vector2 b, float speed) => Mathf.Clamp((b - a).magnitude / Mathf.Max(0.01f, speed) + 0.15f, 0.3f, 9f);

        // ================================================================== shared people poses
        /// <summary>B56.a: the whistle: a hand to the mouth (x 130), two head jabs.</summary>
        static void WhistlePose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f) * (1f - In(t, len - 0.2f, 0.2f));
            p.armR = Lerp(p.armR, new Vector3(130f, 0f, -18f), u);
            p.lean -= 10f * (Bump(t, 0.3f, 0.25f) + Bump(t, 0.7f, 0.25f));
        }
        /// <summary>A shout: two head jabs (the head pushed forward and back).</summary>
        static void JabPose(ref DollPose p, float t, float len) { p.lean += 10f * (Bump(Mathf.Repeat(t, 0.9f), 0f, 0.22f) + Bump(Mathf.Repeat(t, 0.9f), 0.35f, 0.22f)); }
        /// <summary>B55.a: one stands and points (arm x 90) with two head jabs.</summary>
        static void PointShoutPose(ref DollPose p, float t, float len) { PointPose(ref p, t, len); JabPose(ref p, t, len); }
        /// <summary>B55.a: waving both arms and the torch (arm x 150 to 170 at 3 Hz), shouting.</summary>
        static void WaveTorchPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f), w = Mathf.Sin(t * 3f * Mathf.PI * 2f);
            p.armL = Lerp(p.armL, new Vector3(160f + 10f * w, 0f, -22f - 8f * w), u);
            p.armR = Lerp(p.armR, new Vector3(160f - 10f * w, 0f, 22f + 8f * w), u);
            JabPose(ref p, t, len);
        }
        /// <summary>A crouch and two pats on the dog (arm x 60).</summary>
        static void PatPose(ref DollPose p, float t, float len)
        {
            CrouchPose(ref p, t, len);
            float pat = Mathf.Max(Bump(t, 0.3f, 0.35f), Bump(t, 0.8f, 0.35f));
            p.armR = Lerp(p.armR, new Vector3(60f - 20f * pat, 0f, 8f), In(t, 0f, 0.2f));
        }
        /// <summary>B56.b: a scratch on the dog's head (arm x 50, 1 s, the hand rubbing).</summary>
        static void ScratchPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f) * (1f - In(t, len - 0.2f, 0.2f));
            p.lean += 18f * u; p.armR = Lerp(p.armR, new Vector3(50f, 0f, 6f + 10f * Mathf.Sin(t * 22f)), u);
        }
        /// <summary>B55.b: an arm round the kid (arm z across the back [est: x 20, z -70]).</summary>
        static void ArmRoundPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f));
            p.armR = Lerp(p.armR, new Vector3(20f, 0f, -70f), u);
        }
        /// <summary>B24.b: a kid bows back to the pup (lean 30, arms down and forward).</summary>
        static void BowBackPose(ref DollPose p, float t, float len)
        {
            float u = Bump(Mathf.Repeat(t, 2.4f), 0.1f, 1.4f);
            p.lean += 32f * u; p.armL = Lerp(p.armL, new Vector3(-10f, 0f, -20f), u); p.armR = Lerp(p.armR, new Vector3(-10f, 0f, 20f), u);
            p.offset.y -= 0.01f * u;
        }
        /// <summary>B56.d: sitting on the dog's back (+0.1 u), the hands forward (x 60) on its neck, bobbing with its steps.</summary>
        static CampPoseFn RidePose(float slideAt) => (ref DollPose p, float t, float len) =>
        {
            float off = slideAt >= 0f ? In(t, slideAt, 0.5f) : 0f;
            float up = In(t, 0f, 0.4f) * (1f - off);
            Seated(ref p); p.offset.y = Mathf.Lerp(-0.03f, DogGeo.RideSeat - 0.01f, up) + 0.006f * Mathf.Abs(Mathf.Sin(t * 5f)) * up;
            p.squash = Mathf.Lerp(1f, 0.8f, up);
            p.armL = Lerp(p.armL, new Vector3(60f, 0f, -10f), up); p.armR = Lerp(p.armR, new Vector3(60f, 0f, 10f), up);
            p.roll += 12f * off;
        };

        // ================================================================== B24.b the pup at the fire
        /// <summary>B24.b: the pup trots to <paramref name="spot"/> (by its keeper), lies down and chews a hide (head shakes ±25 deg at 4 Hz);
        /// the keeper (−1 none) sits by it watching. <see cref="CampScene.a"/>: the first shake.</summary>
        public static CampScene PupChew(int dog, Vector2 dogAt, int keeper, Vector2 keeperAt, Vector2 spot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.PupChew, t0, spot) { holdFocus = true, dog = dog };
            float w = QTime(dogAt, spot, DogGeo.DogTrot);
            s.Quad(Dog, 0f, w, spot, spot + toCam, QuadAct.Stand, DogGeo.DogTrot, QuadHeld.Hide);
            s.Quad(Dog, w, w + 9f, spot, spot + Across(toCam), QuadAct.Chew, DogGeo.DogTrot, QuadHeld.Hide);
            s.a = w + 0.6f;
            if (keeper >= 0)
            {
                int m = s.Add(keeper);
                var seat = spot + Across(toCam) * 0.12f + toCam * 0.04f;
                float kw = WalkTime(keeperAt, seat, KidWalk);
                s.Part(m, 0f, kw, seat, spot, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(m, kw, w + 9f, seat, spot, DollStyle.Sit);
                Go(s, m, w + 9f, 1f, seat + toCam * 0.25f, seat + toCam, KidWalk);
            }
            s.Quad(Dog, w + 9f, w + 10f, spot, spot + toCam, QuadAct.Stand);
            return s;
        }

        /// <summary>B24.b: the pup plays with the kids: it play-bows (<c>PlayBow</c>, the hop sideways), a kid bows back (arms down); twice;
        /// then it bounds a little circle and the kids run after it, laughing. <see cref="CampScene.a"/>: the first bow.</summary>
        public static CampScene PupPlay(int dog, Vector2 dogAt, IList<int> kids, IList<Vector2> at, Vector2 spot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var across = Across(toCam);
            var s = new CampScene(CampUse.PupPlay, t0, spot) { holdFocus = true, dog = dog };
            float w = QTime(dogAt, spot, DogGeo.DogTrot);
            float t = w;
            var ms = new List<int>(); float kidIn = 0f;
            for (int i = 0; i < kids.Count; i++)
            {
                int m = s.Add(kids[i]); ms.Add(m);
                var st = spot - toCam * 0.02f + across * (i == 0 ? -0.16f : i == 1 ? 0.16f : 0f) + toCam * (i == 2 ? 0.16f : 0.06f);
                float kw = WalkTime(at[i], st, KidWalk);
                s.Part(m, 0f, kw, st, spot, DollStyle.Stand, null, PropKind.None, KidWalk);
                kidIn = Mathf.Max(kidIn, kw);
            }
            t = Mathf.Max(t, kidIn);
            s.Quad(Dog, 0f, t, spot, spot - across, QuadAct.Wag, DogGeo.DogTrot);
            s.a = t;
            // two bows: the pup faces the first kid, then the second
            for (int b = 0; b < 2; b++)
            {
                int m = ms[Mathf.Min(b, ms.Count - 1)];
                var faceTo = spot + across * (b == 0 ? -1f : 1f);
                s.Quad(Dog, t, t + 2.4f, spot, faceTo, QuadAct.PlayBow);
                foreach (int k in ms)
                {
                    var st = s.parts[k][0].pos;
                    if (k == m) s.Part(k, t, t + 2.4f, st, spot, DollStyle.Stand, BowBackPose);
                    else s.Part(k, t, t + 2.4f, st, spot, DollStyle.Laugh);
                }
                t += 2.4f;
            }
            // the bound round, the kids after it
            var ring = new[] { spot + toCam * 0.22f, spot + across * 0.26f, spot - toCam * 0.18f, spot - across * 0.26f, spot + toCam * 0.1f };
            float r0 = t;
            foreach (var p in ring) { float d = QTime(s.quads[^1].pos, p, DogGeo.DogRun); s.Quad(Dog, t, t + d, p, p + (p - spot), QuadAct.Wag, DogGeo.DogRun); t += d; }
            for (int i = 0; i < ms.Count; i++)
            {
                float tt = r0 + 0.3f;
                for (int j = 0; j < ring.Length; j++)
                {
                    var p = ring[j] + across * (0.05f * (i - 0.5f)) - toCam * 0.06f;
                    var prev = s.parts[ms[i]][^1].pos;
                    float d = Mathf.Clamp((p - prev).magnitude / KidRun + 0.05f, 0.2f, 3f);
                    var part = Go(s, ms[i], tt, d, p, p + (p - spot), KidRun, true); tt += d;
                }
                s.Part(ms[i], tt, Mathf.Max(tt + 1.2f, t + 1.2f), s.parts[ms[i]][^1].pos, spot, DollStyle.Laugh);
            }
            s.Quad(Dog, t, t + 2.4f, s.quads[^1].pos, spot + toCam, QuadAct.PlayBow);
            return s;
        }

        /// <summary>B24.b: the pup goes to the embers, sniffs, and sleeps curled there (<c>SleepCurl</c>) by the fire ring.
        /// <see cref="CampScene.a"/>: asleep.</summary>
        public static CampScene PupSleep(int dog, Vector2 dogAt, Vector2 fire, Vector2 toCam, float sleepFor, float t0, Vector2? at = null)
        {
            toCam = Unit(toCam, Vector2.down);
            var spot = at ?? fire + Rot(toCam, 55f) * DogGeo.EmberSleep;
            var s = new CampScene(CampUse.PupSleep, t0, (spot + fire) * 0.5f) { holdFocus = true, dog = dog };
            float w = QTime(dogAt, spot, DogGeo.DogWalk);
            s.Quad(Dog, 0f, w, spot, fire, QuadAct.Stand, DogGeo.DogWalk);
            s.Quad(Dog, w, w + 1.2f, spot, fire, QuadAct.Sniff);
            s.Quad(Dog, w + 1.2f, w + 1.8f, spot, spot + Rot(fire - spot, 90f), QuadAct.Lie);
            s.a = w + 1.8f;
            s.Quad(Dog, w + 1.8f, w + 1.8f + sleepFor, spot, spot + Rot(fire - spot, 90f), QuadAct.Sleep);
            return s;
        }

        /// <summary>B24.b: the pup sneaks to the stash (head low), takes a fish and runs off with it in its mouth; the grown-up nearest
        /// (−1 none) sees, runs after it <see cref="DogGeo.ChaseFor"/> and gives up (hands on the hips, the head shaking); the pup lies down
        /// out of reach and chews the fish. <see cref="CampScene.a"/>: the fish taken.</summary>
        public static CampScene PupSteal(int dog, Vector2 dogAt, Vector2 stash, int grown, Vector2 grownAt, Vector2 away, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); away = Unit(away, Across(toCam));
            var s = new CampScene(CampUse.PupSteal, t0, stash + away * (DogGeo.ThiefRun * 0.45f)) { dog = dog, holdFocus = true }; // the stash and the getaway in one frame
            var at = stash + toCam * 0.07f;
            float w = QTime(dogAt, at, DogGeo.DogWalk);
            s.Quad(Dog, 0f, w, at, stash, QuadAct.HeadDown, DogGeo.DogWalk);
            s.Quad(Dog, w, w + 1f, at, stash, QuadAct.Sniff);
            float take = w + 1f; s.a = take;
            s.Quad(Dog, take, take + 0.3f, at, stash, QuadAct.HeadDown, DogGeo.DogWalk, QuadHeld.Fish);
            var run = at + away * DogGeo.ThiefRun;
            float r = QTime(at, run, DogGeo.DogRun);
            s.Quad(Dog, take + 0.3f, take + 0.3f + r, run, run + away, QuadAct.Stand, DogGeo.DogRun, QuadHeld.Fish);
            float t = take + 0.3f + r;
            s.Quad(Dog, t, t + 0.6f, run, at, QuadAct.Lie, DogGeo.DogRun, QuadHeld.Fish);
            s.Quad(Dog, t + 0.6f, t + 5f, run, at, QuadAct.Chew, DogGeo.DogRun, QuadHeld.Fish);
            if (grown >= 0)
            {
                int m = s.Add(grown);
                var watch = stash - away * 0.16f + toCam * 0.1f;
                float gw = WalkTime(grownAt, watch, Walk);
                s.Part(m, 0f, Mathf.Max(gw, take), watch, stash, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(m, Mathf.Max(gw, take), Mathf.Max(gw, take) + 0.4f, watch, run, DollStyle.Stand, PointPose);
                float c0 = Mathf.Max(gw, take) + 0.4f;
                var stop = watch + Unit(run - watch, away) * DogGeo.ChaseFor;
                float cl = Mathf.Clamp(DogGeo.ChaseFor / 0.45f, 0.4f, 2f);
                Go(s, m, c0, cl, stop, run, 0.45f, true);
                s.Part(m, c0 + cl, c0 + cl + 1.6f, stop, run, DollStyle.Stand, (ref DollPose p, float u, float l) => { HipsPose(ref p, u, l); HeadShakePose(ref p, u, Mathf.Min(l, 0.8f)); });
                Go(s, m, c0 + cl + 1.6f, 1.2f, stop - away * 0.25f + toCam * 0.1f, stash, Walk);
            }
            return s;
        }

        /// <summary>B24.b ("a bit wild"): the pup noses at the meat; the grown-up near it shoos it off (two flaps of the arm, z ∓60); the pup
        /// growls (head low, 0.5 s) and slinks off a little and lies down. <see cref="CampScene.a"/>: the growl.</summary>
        public static CampScene PupShoo(int dog, Vector2 dogAt, Vector2 meat, int grown, Vector2 grownAt, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var across = Across(toCam);
            var s = new CampScene(CampUse.PupShoo, t0, meat) { holdFocus = true, dog = dog };
            var at = meat + toCam * 0.08f - across * 0.03f;
            float w = QTime(dogAt, at, DogGeo.DogWalk);
            s.Quad(Dog, 0f, w, at, meat, QuadAct.HeadDown, DogGeo.DogWalk);
            s.Quad(Dog, w, w + 1.2f, at, meat, QuadAct.Sniff);
            float t = w + 1.2f;
            var st = meat - toCam * 0.07f + across * 0.13f; // behind the meat from the camera, so the pup stays in view
            if (grown >= 0)
            {
                int m = s.Add(grown);
                float gw = WalkTime(grownAt, st, Walk);
                float g0 = Mathf.Max(0f, t - gw);
                s.Part(m, 0f, g0 + gw, st, at, DollStyle.Stand, null, PropKind.None, Walk);
                t = Mathf.Max(t, g0 + gw);
                s.Part(m, t, t + 1.1f, st, at, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float k = In(u, 0f, 0.15f) * (1f - In(u, l - 0.15f, 0.15f));
                    p.armR = Lerp(p.armR, new Vector3(40f, 0f, 10f - 60f * Mathf.Abs(Mathf.Sin(u / l * 2f * Mathf.PI))), k); p.lean += 8f * k;
                });
                s.Part(m, t + 1.1f, t + 3.4f, st, at, DollStyle.Stand, HipsPose);
                Go(s, m, t + 3.4f, 1.2f, st + across * 0.25f + toCam * 0.1f, st + toCam, Walk);
            }
            if (w + 1.2f < t) s.Quad(Dog, w + 1.2f, t, at, meat, QuadAct.Sniff);
            s.Quad(Dog, t, t + 0.6f, at, st, QuadAct.Growl);
            s.a = t + 0.1f;
            var off = at - across * 0.3f + toCam * 0.12f;
            float o = QTime(at, off, DogGeo.DogWalk);
            s.Quad(Dog, t + 0.6f, t + 0.6f + o, off, off - across, QuadAct.HeadDown, DogGeo.DogWalk);
            s.Quad(Dog, t + 0.6f + o, t + 3.6f + o, off, meat, QuadAct.Lie);
            return s;
        }

        /// <summary>B24.b: two kids race along a lane and back; the pup bounds along beside them, a nose ahead, and play-bows at the end; the
        /// kids cheer. <see cref="CampScene.a"/>: the turn.</summary>
        public static CampScene PupRunAlong(int dog, Vector2 dogAt, IList<int> kids, IList<Vector2> at, Vector2 start, Vector2 along, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); along = Unit(along, Across(toCam));
            var end = start + along * 0.9f;
            var s = new CampScene(CampUse.PupRunAlong, t0, (start + end) * 0.5f) { dog = dog, holdFocus = true };
            float t = 0f; var ms = new List<int>();
            for (int i = 0; i < kids.Count && i < 2; i++)
            {
                int m = s.Add(kids[i]); ms.Add(m);
                var lane = start + toCam * (i == 0 ? 0.05f : -0.05f);
                float kw = WalkTime(at[i], lane, KidWalk);
                s.Part(m, 0f, kw, lane, end, DollStyle.Stand, null, PropKind.None, KidWalk);
                t = Mathf.Max(t, kw);
            }
            var dlane = start + toCam * 0.13f;
            float dw = QTime(dogAt, dlane, DogGeo.DogTrot);
            s.Quad(Dog, 0f, Mathf.Max(t, dw), dlane, end, QuadAct.Wag, DogGeo.DogTrot);
            t = Mathf.Max(t, dw) + 0.4f;
            float leg = 0.9f / KidRun;
            for (int i = 0; i < ms.Count; i++)
            {
                var lane = start + toCam * (i == 0 ? 0.05f : -0.05f);
                float sp = KidRun * (i == 0 ? 1f : 0.92f);
                s.Part(ms[i], Mathf.Max(t - 0.4f, s.parts[ms[i]][0].to), t, lane, end, DollStyle.Stand);
                Go(s, ms[i], t, leg / (sp / KidRun), lane + along * 0.9f, end, sp, true);
                Go(s, ms[i], t + leg / (sp / KidRun), leg / (sp / KidRun), lane, start, sp, true);
                s.Part(ms[i], t + 2f * leg / (sp / KidRun), t + 2f * leg + 2.4f, lane, dlane, DollStyle.Cheer);
            }
            s.Quad(Dog, t, t + leg * 0.9f, end + toCam * 0.13f + along * 0.06f, end + along, QuadAct.Stand, DogGeo.DogRun);
            s.a = t + leg;
            s.Quad(Dog, t + leg * 0.9f, t + 2f * leg * 0.92f, dlane - along * 0.06f, start - along, QuadAct.Bark, DogGeo.DogRun);
            s.Quad(Dog, t + 2f * leg * 0.92f, t + 2f * leg + 2.4f, dlane - along * 0.06f, start + along, QuadAct.PlayBow);
            return s;
        }

        // ================================================================== B25 / B56.a the hunt
        /// <summary>B56.a + B25.a: the hunter at the fire whistles (hand to the mouth, two jabs); the dog runs to heel; they walk out toward the
        /// rabbits; the hunter crouches and points; the dog runs round to the rabbits' far side and in (the flush: <see cref="CampScene.a"/>,
        /// CampLife knocks the rabbits there and they bolt toward the hunter); the hunter throws (<see cref="CampScene.b"/>), picks up the
        /// catch, then crouches and pats the dog (two pats) while its tail wags; home with the rabbit on the shoulder, the dog at heel.</summary>
        public static CampScene DogHunt(int dog, Vector2 dogAt, int hunter, Vector2 hunterAt, Vector2 rabbits, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var way = Unit(rabbits - hunterAt, Across(toCam));
            var side = Across(way); if (Vector2.Dot(side, toCam) < 0f) side = -side; // the dog on the camera's side
            var s = new CampScene(CampUse.DogHunt, t0, Vector2.Lerp(hunterAt, rabbits, 0.55f)) { dog = dog, holdFocus = true };
            int m = s.Add(hunter);
            s.Part(m, 0f, 1.3f, hunterAt, rabbits, DollStyle.Stand, WhistlePose, PropKind.Spear);
            var heel = hunterAt + side * 0.08f;
            float hw = QTime(dogAt, heel, DogGeo.DogRun);
            s.Quad(Dog, 0f, 0.5f, dogAt, hunterAt, QuadAct.Alert);
            s.Quad(Dog, 0.5f, Mathf.Max(1.6f, 0.5f + hw), heel, rabbits, QuadAct.Wag, DogGeo.DogRun);
            float t = Mathf.Max(1.6f, 0.5f + hw);
            s.Part(m, 1.3f, t, hunterAt, rabbits, DollStyle.Stand, null, PropKind.Spear);
            var crouch = rabbits - way * 0.45f;
            float ww = WalkTime(hunterAt, crouch, Walk);
            s.Part(m, t, t + ww, crouch, rabbits, DollStyle.Stand, null, PropKind.Spear, Walk);
            s.Quad(Dog, t, t + ww, crouch + side * 0.08f, rabbits, QuadAct.Stand, Walk);
            t += ww;
            s.Part(m, t, t + 0.8f, crouch, rabbits, DollStyle.Stand, (ref DollPose p, float u, float l) => { CrouchPose(ref p, u, l); PointPose(ref p, u, l); }, PropKind.Spear);
            // the dog runs round to the far side
            var wide = rabbits + side * 0.22f;
            var far = rabbits + way * 0.25f;
            float d1 = QTime(crouch + side * 0.08f, wide, DogGeo.DogRun), d2 = QTime(wide, far, DogGeo.DogRun);
            s.Quad(Dog, t, t + d1, wide, far, QuadAct.Stand, DogGeo.DogRun);
            s.Quad(Dog, t + d1, t + d1 + d2, far, rabbits, QuadAct.Stand, DogGeo.DogRun);
            float flush = t + d1 + d2; s.a = flush;
            s.Quad(Dog, flush, flush + 0.9f, rabbits + way * 0.06f, crouch, QuadAct.Bark, DogGeo.DogRun);
            s.Part(m, t + 0.8f, flush + 0.3f, crouch, rabbits, DollStyle.Stand, CrouchPose, PropKind.Spear);
            s.b = flush + 0.3f + CampScenes4.ThrowRelease;
            s.Part(m, flush + 0.3f, flush + 0.3f + CampScenes4.ThrowSeconds, crouch, rabbits, DollStyle.Stand, CampScenes4.ThrowPose, PropKind.Spear);
            t = flush + 0.3f + CampScenes4.ThrowSeconds;
            var catchAt = crouch + way * 0.2f;
            s.Part(m, t, t + 1f, catchAt, catchAt + way, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t + 1f, t + 1.6f, catchAt, catchAt + way, DollStyle.Stand, CrouchPose);
            s.Part(m, t + 1.6f, t + 2.6f, catchAt, catchAt + toCam, DollStyle.HoldUp, null, PropKind.Rabbit);
            s.Quad(Dog, flush + 0.9f, t + 2.6f, catchAt + way * 0.1f + side * 0.04f, catchAt, QuadAct.Wag, DogGeo.DogTrot);
            t += 2.6f;
            // the pat
            var dogPat = catchAt + side * 0.1f;
            s.Quad(Dog, t, t + 1.8f, dogPat, catchAt, QuadAct.Wag, DogGeo.DogTrot);
            s.Part(m, t, t + 1.8f, catchAt, dogPat, DollStyle.Stand, PatPose);
            s.c = t + 0.3f;
            t += 1.8f;
            s.Part(m, t, t + 0.2f, catchAt, hunterAt, DollStyle.Stand);
            var home = catchAt + (hunterAt - catchAt) * 0.5f;
            float hb = WalkTime(catchAt, home, Walk);
            var back = s.Part(m, t, t + hb, home, hunterAt, DollStyle.Stand, null, PropKind.Rabbit, Walk); back.slot = PropSlot.Shoulder;
            s.Quad(Dog, t, t + hb, home + side * 0.08f, hunterAt, QuadAct.Wag, Walk);
            return s;
        }

        // ================================================================== B25.b + B56.b on guard
        /// <summary>B25.b + B56.b: a wolf comes out of the dark (the plan's start to stop); the dog at the fire stands, trots between the fire and
        /// the wolf and barks (three head jabs, a 0.03 u hop forward each: <see cref="CampScene.a"/> the first); the wolf <c>Alert</c>s, backs
        /// off and trots away into the dark. A grown-up at the fire (−1 none) stands, looks into the dark (2 s), comes and scratches the
        /// dog's head (1 s), and sits again; the dog curls up by the fire.</summary>
        public static CampScene DogGuard(int dog, Vector2 dogAt, in WolfDriveOff.Plan plan, Vector2 fire, int grown, Vector2 grownAt, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.DogGuard, t0, Vector2.Lerp(fire, plan.wolfStop, 0.4f)) { holdFocus = true, dog = dog };
            float wIn = QTime(plan.wolfStart, plan.wolfStop, DogGeo.WolfTrot);
            s.Quad(Wolf, 0f, wIn, plan.wolfStop, fire, QuadAct.Stand, DogGeo.WolfTrot);
            var guard = fire + plan.dir * Mathf.Max(0.35f, (plan.wolfStop - fire).magnitude * 0.45f);
            s.Quad(Dog, 0f, wIn * 0.6f, dogAt, plan.wolfStop, QuadAct.Lie);
            s.Quad(Dog, wIn * 0.6f, wIn * 0.6f + 0.5f, dogAt, plan.wolfStop, QuadAct.Alert);
            float gt = QTime(dogAt, guard, DogGeo.DogTrot);
            float t = wIn * 0.6f + 0.5f;
            s.Quad(Dog, t, t + gt, guard, plan.wolfStop, QuadAct.Alert, DogGeo.DogTrot);
            t = Mathf.Max(t + gt, wIn + 0.3f);
            s.Quad(Wolf, wIn, t, plan.wolfStop, fire, QuadAct.Stand);
            s.a = t;
            s.Quad(Dog, t, t + 3f * DogGeo.BarkEvery, guard, plan.wolfStop, QuadAct.Bark);
            s.Quad(Wolf, t, t + 0.8f, plan.wolfStop, fire, QuadAct.Alert);
            float back = QTime(plan.wolfStop, plan.wolfBack, 0.12f);
            s.Quad(Wolf, t + 0.8f, t + 0.8f + back, plan.wolfBack, fire, QuadAct.Alert, 0.12f);
            float away = QTime(plan.wolfBack, plan.wolfAway, DogGeo.WolfTrot * 1.3f);
            s.Quad(Wolf, t + 0.8f + back, t + 0.8f + back + away, plan.wolfAway, plan.wolfAway + plan.dir, QuadAct.Stand, DogGeo.WolfTrot * 1.3f);
            float tb = t + 3f * DogGeo.BarkEvery;
            var guardHop = guard + plan.dir * DogGeo.BarkHop * 3f;
            s.Quad(Dog, tb, t + 0.8f + back + away, guardHop, plan.wolfAway, QuadAct.Alert);
            float te = t + 0.8f + back + away;
            if (grown >= 0)
            {
                int m = s.Add(grown);
                s.Part(m, 0f, t, grownAt, fire, DollStyle.Sit, null, PropKind.None, Walk); // at the fire (walks there first when the cheat brings them)
                var look = grownAt + Unit(plan.wolfStop - grownAt, plan.dir) * 0.05f;
                s.Part(m, t, t + 2f, look, plan.wolfStop, DollStyle.Stand, LookRound(14f, 1f));
                var by = guardHop - plan.dir * 0.09f + Across(plan.dir) * 0.04f;
                float bw = WalkTime(look, by, Walk);
                float sc = Mathf.Max(t + 2f + bw, te);
                s.Part(m, t + 2f, sc, by, guardHop, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(m, sc, sc + 1f, by, guardHop, DollStyle.Stand, ScratchPose);
                s.Quad(Dog, te, sc + 1f, guardHop, by, QuadAct.Wag);
                s.c = sc;
                float rb = WalkTime(by, grownAt, Walk);
                s.Part(m, sc + 1f, sc + 1f + rb, grownAt, fire, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(m, sc + 1f + rb, sc + 3f + rb, grownAt, fire, DollStyle.Sit);
                te = sc + 1f;
            }
            var sleep = fire + Rot(Unit(guard - fire, plan.dir), 40f) * DogGeo.FireSleep;
            float sw = QTime(guardHop, sleep, DogGeo.DogWalk);
            s.Quad(Dog, te, te + sw, sleep, fire, QuadAct.Stand, DogGeo.DogWalk);
            s.Quad(Dog, te + sw, te + sw + 3f, sleep, sleep + Rot(fire - sleep, 90f), QuadAct.Sleep);
            return s;
        }

        // ================================================================== B55.a driving the wolves off
        /// <summary>B55.a: a wolf comes out of the dark and stands; the first driver stands and points with two head jabs (a shout); the second
        /// takes a burning stick from the fire (a torch: the ember tip, bigger, sparks every 0.3 s); together (2-3) they walk 0.3 u toward it
        /// (never nearer than 0.6), waving both arms and the torch, shouting; the wolf <c>Alert</c>s (<see cref="CampScene.a"/>), backs off
        /// 0.2 u, turns and trots into the dark. They stand 2 s looking after it; the torch goes back in the fire; they sit again.</summary>
        /// <param name="startAt">Where they are now when they first walk to their seats <paramref name="at"/> by the fire (the cheat; null: they
        /// sit there already).</param>
        public static CampScene DriveOff(IList<int> who, IList<Vector2> at, in WolfDriveOff.Plan plan, Vector2 fire, Vector2 toCam, float t0, IList<Vector2> startAt = null)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.WolfDriveOff, t0, Vector2.Lerp(fire, plan.wolfStop, 0.45f)) { holdFocus = true };
            float wIn = QTime(plan.wolfStart, plan.wolfStop, DogGeo.WolfTrot);
            var ms = new List<int>(); foreach (int k in who) ms.Add(s.Add(k));
            float seatBy = 0f;
            if (startAt != null) for (int i = 0; i < ms.Count; i++) if ((startAt[i] - at[i]).magnitude > 0.05f) seatBy = Mathf.Max(seatBy, WalkTime(startAt[i], at[i], Walk) + 0.5f);
            float t = Mathf.Max(wIn, seatBy);
            // the wolf comes out of the dark once they sit by the fire
            s.Quad(Wolf, 0f, t - wIn, plan.wolfStart, fire, QuadAct.Stand);
            s.Quad(Wolf, t - wIn, t, plan.wolfStop, fire, QuadAct.Stand, DogGeo.WolfTrot);
            wIn = t;
            void Stay(int m, float until)
            {
                int i = ms.IndexOf(m);
                if (startAt == null || (startAt[i] - at[i]).magnitude <= 0.05f) { s.Stay(m, 0f, until, DollStyle.Sit); return; }
                float w = WalkTime(startAt[i], at[i], Walk);
                s.Part(m, 0f, w, at[i], fire, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(m, w, until, at[i], fire, DollStyle.Sit);
            }
            // the shout
            Stay(ms[0], t);
            s.Part(ms[0], t, t + 1.4f, at[0], plan.wolfStop, DollStyle.Stand, PointShoutPose);
            // the torch
            int torch = ms.Count > 1 ? ms[1] : ms[0];
            var take = fire + Unit(at[torch] - fire, toCam) * 0.12f;
            float tt = t;
            if (torch != ms[0])
            {
                Stay(torch, t);
                float tw = WalkTime(at[torch], take, Walk);
                s.Part(torch, t, t + tw, take, fire, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(torch, t + tw, t + tw + 0.6f, take, fire, DollStyle.Stand, CrouchPose);
                tt = t + tw + 0.6f;
            }
            for (int i = 2; i < ms.Count; i++) { Stay(ms[i], t + 0.6f); s.Part(ms[i], t + 0.6f, t + 1.4f, at[i], plan.wolfStop, DollStyle.Stand, JabPose); }
            float go = Mathf.Max(t + 1.4f, tt);
            if (torch == ms[0]) s.Part(ms[0], t + 1.4f, go, at[0], plan.wolfStop, DollStyle.Stand, JabPose);
            if (torch != ms[0] && go > tt) s.Part(torch, tt, go, take, plan.wolfStop, DollStyle.Stand, null, PropKind.Stick);
            for (int i = 2; i < ms.Count; i++) if (go > t + 1.4f) s.Part(ms[i], t + 1.4f, go, at[i], plan.wolfStop, DollStyle.Stand, JabPose);
            if (ms[0] != torch && go > t + 1.4f) s.Part(ms[0], t + 1.4f, go, at[0], plan.wolfStop, DollStyle.Stand, JabPose);
            // the walk out, waving
            float walk = 0f;
            for (int i = 0; i < ms.Count; i++)
            {
                var from = ms[i] == torch && torch != ms[0] ? take : at[i];
                walk = Mathf.Max(walk, (plan.driverStop[i] - from).magnitude / 0.18f + 0.2f);
            }
            walk = Mathf.Clamp(walk, 1.2f, 3f);
            float wave = 2.6f;
            for (int i = 0; i < ms.Count; i++)
                s.Part(ms[i], go, go + walk + wave, plan.driverStop[i], plan.wolfStop, DollStyle.Stand, WaveTorchPose, ms[i] == torch ? PropKind.Stick : PropKind.None, 0.18f).poseWalking = true;
            s.Quad(Wolf, wIn, go + walk * 0.7f, plan.wolfStop, fire, QuadAct.Stand);
            float a = go + walk * 0.7f; s.a = a;
            s.Quad(Wolf, a, a + 0.6f, plan.wolfStop, fire, QuadAct.Alert);
            float back = QTime(plan.wolfStop, plan.wolfBack, 0.12f);
            s.Quad(Wolf, a + 0.6f, a + 0.6f + back, plan.wolfBack, fire, QuadAct.Alert, 0.12f);
            float away = QTime(plan.wolfBack, plan.wolfAway, DogGeo.WolfTrot * 1.3f);
            s.Quad(Wolf, a + 0.6f + back, a + 0.6f + back + away, plan.wolfAway, plan.wolfAway + plan.dir, QuadAct.Stand, DogGeo.WolfTrot * 1.3f);
            float e = Mathf.Max(go + walk + wave, a + 0.6f + back + away * 0.5f);
            if (e > go + walk + wave) for (int i = 0; i < ms.Count; i++) s.Part(ms[i], go + walk + wave, e, plan.driverStop[i], plan.wolfStop, DollStyle.Stand, JabPose, ms[i] == torch ? PropKind.Stick : PropKind.None);
            // looking after it, 2 s
            for (int i = 0; i < ms.Count; i++) s.Part(ms[i], e, e + 2f, plan.driverStop[i], plan.wolfAway, DollStyle.Stand, null, ms[i] == torch ? PropKind.Stick : PropKind.None);
            s.Tip(torch, tt, e + 2f + 2.2f, CampTipKind.Ember, 2.2f);
            for (float f = tt + 0.3f; f < e + 2f; f += 0.3f) s.Fx(f, CampFxKind.Sparks, new Vector3(0f, 0.01f, 0f), 0.35f, 2, CampAnchor.TwigTip, torch);
            e += 2f;
            // the torch back in the fire, then everyone sits again
            int ti = ms.IndexOf(torch);
            float bt = WalkTime(plan.driverStop[ti], take, Walk);
            s.Part(torch, e, e + bt, take, fire, DollStyle.Stand, null, PropKind.Stick, Walk);
            s.Part(torch, e + bt, e + bt + 0.6f, take, fire, DollStyle.Stand, CrouchPose, PropKind.Stick);
            s.Fx(e + bt + 0.5f, CampFxKind.Sparks, new Vector3(fire.x, 0.03f, fire.y), 0.6f, 6);
            float end = e + bt + 0.6f;
            for (int i = 0; i < ms.Count; i++)
            {
                var from = ms[i] == torch ? take : plan.driverStop[i];
                float bw = WalkTime(from, at[i], Walk);
                float st = ms[i] == torch ? end : e;
                s.Part(ms[i], st, st + bw, at[i], fire, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(ms[i], st + bw, Mathf.Max(st + bw + 1.5f, end + 1f), at[i], fire, DollStyle.Sit);
            }
            return s;
        }

        // ================================================================== B55.b holding the kids close
        /// <summary>B55.b: each grown-up walks to their kid and puts an arm round them; the kid presses in (0.04 u); both face the wolf
        /// (<paramref name="wolf"/>); held for <paramref name="holdFor"/> (until the wolves are gone, plus 2 s); then the arm drops and the kid
        /// runs off. <paramref name="plan"/> (when ok) brings a wolf to watch from the firelight's edge and leave (the cheat's "in view").
        /// <see cref="CampScene.a"/>: all held.</summary>
        public static CampScene KidsClose(IList<int> grown, IList<Vector2> grownAt, IList<int> kids, IList<Vector2> kidsAt, Vector2 wolf, in WolfDriveOff.Plan plan, float holdFor, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var mid = Vector2.zero; foreach (var k in kidsAt) mid += k; mid /= Mathf.Max(1, kidsAt.Count);
            var s = new CampScene(CampUse.KidsClose, t0, plan.ok ? Vector2.Lerp(mid, plan.wolfStop, 0.35f) : mid) { holdFocus = true };
            float t = 0f;
            var gm = new List<int>(); var km = new List<int>(); var spot = new List<Vector2>(); var kspot = new List<Vector2>();
            for (int i = 0; i < grown.Count && i < kids.Count; i++)
            {
                gm.Add(s.Add(grown[i])); km.Add(s.Add(kids[i]));
                var toW = Unit(wolf - kidsAt[i], toCam);
                var side = Across(toW);
                var gs = kidsAt[i] + side * 0.06f - toW * 0.02f; spot.Add(gs);
                kspot.Add(gs - side * DogGeo.PressIn);
                t = Mathf.Max(t, WalkTime(grownAt[i], gs, Walk));
            }
            float wIn = plan.ok ? QTime(plan.wolfStart, plan.wolfStop, DogGeo.WolfTrot) : 0f;
            for (int i = 0; i < gm.Count; i++)
            {
                s.Part(gm[i], 0f, t, spot[i], wolf, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(km[i], 0f, t * 0.6f, kidsAt[i], wolf, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean -= 6f * In(u, 0f, 0.3f); });
                s.Part(km[i], t * 0.6f, t, kspot[i], wolf, DollStyle.Stand, null, PropKind.None, 0.12f);
            }
            s.a = t;
            float hold = Mathf.Max(holdFor, plan.ok ? wIn + 6f : 0f);
            for (int i = 0; i < gm.Count; i++)
            {
                s.Part(gm[i], t, t + hold, spot[i], wolf, DollStyle.Stand, ArmRoundPose);
                s.Part(km[i], t, t + hold, kspot[i], wolf, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean -= 6f; p.roll += 6f; });
                var off = kspot[i] - Unit(wolf - kspot[i], toCam) * 0.3f + Across(toCam) * (i % 2 == 0 ? 0.12f : -0.12f);
                Go(s, km[i], t + hold, 1.2f, off, off - (wolf - kspot[i]), KidRun, true);
                s.Part(gm[i], t + hold, t + hold + 1f, spot[i], kspot[i], DollStyle.Stand);
            }
            if (plan.ok)
            {
                s.Quad(Wolf, 0f, wIn, plan.wolfStop, plan.wolfStop - plan.dir, QuadAct.Stand, DogGeo.WolfTrot);
                s.Quad(Wolf, wIn, t + hold - 2.6f, plan.wolfStop, plan.wolfStop - plan.dir, QuadAct.Alert);
                float away = QTime(plan.wolfStop, plan.wolfAway, DogGeo.WolfTrot);
                s.Quad(Wolf, t + hold - 2.6f, t + hold - 2.6f + away, plan.wolfAway, plan.wolfAway + plan.dir, QuadAct.Stand, DogGeo.WolfTrot);
            }
            return s;
        }

        // ================================================================== B56.c fetch
        /// <summary>B56.c: a kid throws a stick (B2.b's throw, an arc h 0.12 over 0.5 s) <paramref name="throws"/> times; the grown dog runs after
        /// it, head down 0.2 s, the stick in its mouth, runs back and drops it at the kid's feet, then play-bows; the kid cheers.
        /// <see cref="CampScene.a"/>: the first release. The throws are <see cref="CampScene.arcs"/> (member 0).</summary>
        public static CampScene Fetch(int kid, Vector2 kidAt, int dog, Vector2 dogAt, Vector2 spot, Vector2 toCam, int throws, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var across = Across(toCam);
            var s = new CampScene(CampUse.DogFetch, t0, spot + across * 0.2f) { holdFocus = true, dog = dog };
            int m = s.Add(kid);
            float w = WalkTime(kidAt, spot, KidWalk);
            s.Part(m, 0f, w, spot, spot + across, DollStyle.Stand, null, PropKind.None, KidWalk);
            var feet = spot + across * 0.07f + toCam * 0.02f;
            float dw = QTime(dogAt, feet, DogGeo.DogTrot);
            s.Quad(Dog, 0f, Mathf.Max(w, dw), feet, spot, QuadAct.Wag, DogGeo.DogTrot);
            float t = Mathf.Max(w, dw) + 0.2f;
            s.Part(m, w, t, spot, spot + across, DollStyle.Stand);
            s.Quad(Dog, Mathf.Max(w, dw), t, feet, spot + across, QuadAct.Wag);
            for (int i = 0; i < throws; i++)
            {
                var land = spot + Rot(across, (i - 1) * 25f) * DogGeo.FetchFar;
                s.Part(m, t, t + CampScenes4.ThrowSeconds, spot, land, DollStyle.Stand, CampScenes4.ThrowPose);
                float rel = t + CampScenes4.ThrowRelease;
                if (i == 0) s.a = rel;
                s.arcs.Add(new CampArc { member = m, from = rel, seconds = DogGeo.FetchT, h = DogGeo.FetchH, to = new Vector3(land.x, 0.004f, land.y) });
                s.Quad(Dog, t, rel, feet, land, QuadAct.Alert);
                float run = QTime(feet, land, DogGeo.DogRun);
                s.Quad(Dog, rel, rel + Mathf.Max(run, DogGeo.FetchT), land, land + (land - spot), QuadAct.Stand, DogGeo.DogRun);
                float got = rel + Mathf.Max(run, DogGeo.FetchT);
                s.Quad(Dog, got, got + 0.2f, land, land + (land - spot), QuadAct.HeadDown);
                s.Quad(Dog, got + 0.2f, got + 0.2f + run, feet, spot, QuadAct.Stand, DogGeo.DogRun, QuadHeld.Stick);
                float back = got + 0.2f + run;
                s.Quad(Dog, back, back + 0.3f, feet, spot, QuadAct.HeadDown);
                s.Quad(Dog, back + 0.3f, back + 1.5f, feet, spot, QuadAct.PlayBow);
                s.Part(m, t + CampScenes4.ThrowSeconds, back + 0.3f, spot, land, DollStyle.Stand);
                s.Part(m, back + 0.3f, back + 1.3f, spot, feet, DollStyle.Cheer);
                // the stick lies at the kid's feet until it is picked up again
                s.Part(m, back + 1.3f, back + 1.7f, spot, feet, DollStyle.Stand, CrouchPose);
                t = back + 1.7f;
            }
            Go(s, m, t, 1f, spot + toCam * 0.25f, spot + toCam, KidWalk);
            s.Quad(Dog, t, t + 1f, feet + toCam * 0.2f, spot + toCam, QuadAct.Wag, DogGeo.DogTrot);
            return s;
        }

        // ================================================================== B56.d a kid rides a big dog
        /// <summary>B56.d: the big dog stands; a small kid climbs on its back (+0.1 u, hands forward x 60); the dog walks 0.4 u slowly and the
        /// kid bobs; an older kid (−1 none) walks beside, a hand out to them (arm x 70); the dog lies down (<c>LieDown</c>) and the kid slides
        /// off laughing. <see cref="CampScene.a"/>: on its back; <see cref="CampScene.b"/>: the slide.</summary>
        public static CampScene Ride(int small, Vector2 smallAt, int older, Vector2 olderAt, int dog, Vector2 dogAt, Vector2 spot, Vector2 along, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); along = Unit(along, Across(toCam));
            var end = spot + along * DogGeo.RideLen;
            var s = new CampScene(CampUse.DogRide, t0, (spot + end) * 0.5f) { holdFocus = true, dog = dog };
            float dw = QTime(dogAt, spot, DogGeo.DogWalk);
            s.Quad(Dog, 0f, dw, spot, end, QuadAct.Stand, DogGeo.DogWalk);
            int m = s.Add(small);
            var climb = spot + toCam * 0.06f;
            float kw = WalkTime(smallAt, climb, KidWalk);
            s.Part(m, 0f, kw, climb, spot, DollStyle.Stand, null, PropKind.None, KidWalk);
            float t = Mathf.Max(dw, kw);
            s.Quad(Dog, dw, t + 0.8f, spot, end, QuadAct.Wag);
            if (t > kw) s.Part(m, kw, t, climb, spot, DollStyle.Stand);
            var up = s.Part(m, t, t + 0.8f, spot, end, DollStyle.Sit, RidePose(-1f), PropKind.None, 0.12f); up.poseWalking = true; up.carried = true;
            t += 0.8f; s.a = t;
            float ride = DogGeo.RideLen / DogGeo.RideSpeed;
            s.Quad(Dog, t, t + ride, end, end + along, QuadAct.Stand, DogGeo.RideSpeed);
            var rp = s.Part(m, t, t + ride, end, end + along, DollStyle.Sit, (ref DollPose p, float u, float l) => { RidePose(-1f)(ref p, u + 0.4f, l); }, PropKind.None, DogGeo.RideSpeed);
            rp.poseWalking = true; rp.carried = true;
            int o = -1;
            if (older >= 0)
            {
                o = s.Add(older);
                var beside0 = spot + toCam * 0.09f; var beside1 = end + toCam * 0.09f;
                float ow = WalkTime(olderAt, beside0, KidWalk);
                s.Part(o, 0f, Mathf.Max(ow, t), beside0, spot, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(o, Mathf.Max(ow, t), t + ride, beside1, end, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armL = Lerp(p.armL, new Vector3(70f, 0f, -20f), In(u, 0f, 0.3f)); }, PropKind.None, DogGeo.RideSpeed).poseWalking = true;
            }
            t += ride;
            s.Quad(Dog, t, t + 3f, end, end + along, QuadAct.Lie);
            s.b = t + 0.5f;
            var off = end + toCam * 0.07f;
            var down = s.Part(m, t, t + 1f, off, end + toCam, DollStyle.Sit, (ref DollPose p, float u, float l) => { RidePose(0.4f)(ref p, u + 0.4f, l); }, PropKind.None, 0.1f); down.poseWalking = true; down.carried = true;
            s.Part(m, t + 1f, t + 2.8f, off, end, DollStyle.Laugh);
            if (o >= 0) s.Part(o, t, t + 2.8f, end + toCam * 0.09f, off, DollStyle.Laugh);
            Go(s, m, t + 2.8f, 1f, off + toCam * 0.25f, off + toCam, KidWalk);
            return s;
        }

        // ================================================================== B56.e / B25 the dog's rest
        /// <summary>B56.e: the grown dog curls up asleep at the fire ring's edge (<see cref="DogGeo.FireSleep"/> from the fire, never in it);
        /// a kid (−1 none) comes and lies down with the head on it for 4 s before bedtime. <see cref="CampScene.a"/>: asleep.</summary>
        public static CampScene DogFire(int dog, Vector2 dogAt, Vector2 fire, int kid, Vector2 kidAt, Vector2 toCam, float t0, Vector2? at = null)
        {
            toCam = Unit(toCam, Vector2.down);
            var spot = at ?? fire + Rot(toCam, -50f) * DogGeo.FireSleep;
            var s = new CampScene(CampUse.DogFire, t0, (spot * 2f + fire) / 3f) { holdFocus = true, dog = dog };
            float w = QTime(dogAt, spot, DogGeo.DogWalk);
            var curl = spot + Rot(fire - spot, -90f);
            s.Quad(Dog, 0f, w, spot, fire, QuadAct.Stand, DogGeo.DogWalk);
            s.Quad(Dog, w, w + 0.8f, spot, fire, QuadAct.Sniff);
            s.Quad(Dog, w + 0.8f, w + 1.4f, spot, curl, QuadAct.Lie);
            s.a = w + 1.4f;
            float end = w + 12f;
            if (kid >= 0)
            {
                int m = s.Add(kid);
                var away = Unit(spot - fire, toCam);
                var lie = spot + away * 0.075f;
                float kw = WalkTime(kidAt, lie + away * 0.05f, KidWalk);
                float k0 = Mathf.Max(kw, w + 2.5f);
                s.Part(m, 0f, k0, lie + away * 0.05f, spot, DollStyle.Stand, null, PropKind.None, KidWalk);
                // lies on the back, the head toward the dog (lean −90 lies the head back, away from the facing: face away from the dog)
                s.Part(m, k0, k0 + 0.8f, lie, lie + away, DollStyle.Stand, CampScenes3.LieBackPose);
                s.Part(m, k0 + 0.8f, k0 + 4.8f, lie, lie + away, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean = -90f; p.offset.y = 0.03f; p.armL = new Vector3(170f, 0f, -40f); p.armR = new Vector3(170f, 0f, 40f); p.roll += 4f * Mathf.Sin(u * 1.2f); });
                s.Part(m, k0 + 4.8f, k0 + 5.4f, lie, lie + away, DollStyle.Stand, (ref DollPose p, float u, float l) => { CampScenes3.LieBackPose(ref p, l - u, l); });
                Go(s, m, k0 + 5.4f, 1f, lie + toCam * 0.3f, lie + toCam, KidWalk);
                end = Mathf.Max(end, k0 + 6.4f);
            }
            s.Quad(Dog, w + 1.4f, end, spot, curl, QuadAct.Sleep);
            return s;
        }

        /// <summary>B25: the grown dog walks to its keeper's tent's sunny side and lies there (head down on the paws, now and then up).
        /// <see cref="CampScene.a"/>: lying.</summary>
        public static CampScene DogSun(int dog, Vector2 dogAt, Vector2 spot, Vector2 toCam, float lieFor, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.DogSun, t0, spot) { holdFocus = true, dog = dog };
            float w = QTime(dogAt, spot, DogGeo.DogWalk);
            s.Quad(Dog, 0f, w, spot, spot + Across(toCam), QuadAct.Stand, DogGeo.DogWalk);
            s.a = w;
            s.Quad(Dog, w, w + lieFor, spot, spot + Across(toCam), QuadAct.Lie);
            return s;
        }
    }
}
