// #692 part 5 (docs/design/animation-specs.md "#692 part 5: the rest of the camp", B40-B53): the uses as pure builders, part one (B40-B46:
// tents, the stash, the racks, the thicket, wild trees, the pond, mist puddles). Each takes who and where (box x, z) and returns a CampScene
// whose timeline follows the spec row's beats and timings. What a use leaves (a toy on a tent, a lit lantern, a purple mouth, a leaf boat)
// rides on the scene as CampFxKind.Leave effects; View/CampLife.Camp.cs draws them and picks who uses what and when. B47-B53: CampScenes5b.cs.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;

namespace AquariumCiv.View
{
    /// <summary>#692 part 5: the numbers of the things the uses touch, and the swing's rules (pure).</summary>
    public static class CampGeo5
    {
        // ---- B40/B52: the lantern pole at each tent's door [est]: a short pole beside the door, the lantern at its top
        public const float PoleOff = 0.07f, PoleH = 0.15f, LanternSize = 0.03f;

        /// <summary>#692 p5 retakes: the box camera's home yaw (BoxCamera.yaw, 28 deg). The tents' shown side is fixed to it, not to the live
        /// camera, so the lantern pole stays put while the player turns the box.</summary>
        public const float HomeYaw = 28f;
        /// <summary>The way toward the home camera on the ground (box x, z).</summary>
        public static Vector2 HomeToCam => new Vector2(-Mathf.Sin(HomeYaw * Mathf.Deg2Rad), -Mathf.Cos(HomeYaw * Mathf.Deg2Rad));
        /// <summary>A door is shown when it faces the home camera at least this much (cos of 60 deg) [est].</summary>
        public const float ShownDot = 0.5f;

        /// <summary>The drawn tent's radius on a hut lot <paramref name="w"/> x <paramref name="d"/> (Pieces.Teepee's: 0.42 x the short side,
        /// 0.14 to 0.38).</summary>
        public static float TentRadius(float w, float d) => Mathf.Clamp(Mathf.Min(w, d) * 0.42f, 0.14f, 0.38f);

        /// <summary>#692 p5 retakes (the user's review: the peek, the toy and the lantern hid behind the tent): the side of a tent the uses play on.
        /// The door itself when it faces the camera (<see cref="ShownDot"/>); else the door's way turned round the tent toward the camera just far
        /// enough to face it by <see cref="ShownDot"/>: the hem the kids lift on the side the player sees, on the tent's wall
        /// (<paramref name="tentR"/>). <paramref name="hem"/>: true when it is not the door.</summary>
        public static void ShownSide(Vector2 centre, Vector2 door, Vector2 out1, Vector2 toCam, float tentR, out Vector2 spot, out Vector2 way, out bool hem)
        {
            out1 = out1.sqrMagnitude > 1e-6f ? out1.normalized : Vector2.down;
            toCam = toCam.sqrMagnitude > 1e-6f ? toCam.normalized : Vector2.down;
            if (Vector2.Dot(out1, toCam) >= ShownDot - 1e-4f) { spot = door; way = out1; hem = false; return; }
            float r = Mathf.Max(0.05f, tentR);
            // turn toward the camera the short way round (straight away: to the left as the camera sees it [est])
            float cross = out1.x * toCam.y - out1.y * toCam.x;
            float sign = Mathf.Abs(cross) < 1e-4f ? 1f : Mathf.Sign(cross);
            float want = Mathf.Acos(ShownDot) * Mathf.Rad2Deg; // the angle from the camera way
            float camAng = Mathf.Atan2(toCam.y, toCam.x) * Mathf.Rad2Deg;
            float a = (camAng - sign * want) * Mathf.Deg2Rad;
            way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            spot = centre + way * r;
            hem = true;
        }

        /// <summary>The lantern pole beside the shown side: <see cref="PoleOff"/> to the side nearer the camera, a little out.</summary>
        public static Vector2 PoleBeside(Vector2 spot, Vector2 way, Vector2 toCam)
        {
            var side = new Vector2(-way.y, way.x);
            if (Vector2.Dot(side, toCam) < 0f) side = -side;
            return spot + side * PoleOff + way * 0.035f; // clear of the sloping canvas
        }
        /// <summary>B40.b: at most two things hang on a tent's pole.</summary>
        public const int TentThings = 2;
        // ---- B42: meat strips on the fish rack (0.06 x 0.015 u), at most four [est]; they dry three view days [est]
        public const float StripLong = 0.06f, StripWide = 0.015f;
        public const int MaxStrips = 4, StripDays = 3;
        // ---- B45.a: the skips (u along the water from the throw), 0.25 s apart, the stone 0.02 u over the water
        public static readonly float[] SkipSteps = { 0.15f, 0.12f, 0.08f };
        public const float SkipGap = 0.25f, SkipH = 0.02f, SkipFirst = 0.12f, PlopFar = 0.12f;
        /// <summary>A skip's ring scale (the sink's 1.3 x). Back to the spec's 0.6 (reshoot2): the rings had been hidden under the water, not too small.</summary>
        public const float SkipRing = 0.6f;
        // ---- B45.b: the leaf boat drifts 0.04 u/s, turning 20 deg/s, 0.3-0.5 u out
        public const float BoatSpeed = 0.04f, BoatTurn = 20f, BoatSize = 0.03f;
        // ---- B48.b: the little pool behind the dam
        public const float DamPoolR = 0.06f;
        // ---- B52.b: the moths circle the lantern (r 0.04 at 2 Hz), pale specks 0.016 u [est]
        public const float MothR = 0.04f, MothHz = 2f, MothSize = 0.016f;
        // ---- B53.d the rope swing: two ropes 0.16 u to a plank 0.07 x 0.025 u; the plank's seat 0.05 u off the ground [est]; the arc ±25 deg at 0.5 Hz
        // [est]; the empty swing rocks to a stop (tau 1.5 s); the ropes drawn at least 2 px wide [est]
        public const float RopeLen = 0.16f, RopeW = 0.006f, RopeMinPx = 2f, PlankW = 0.08f, PlankD = 0.03f, PlankT = 0.01f, SeatH = 0.05f, ArcDeg = 25f, ArcHz = 0.5f, ArcTau = 1.5f;
        /// <summary>#692 p5 retakes: the plank's top (where a rider sits) and DollMotion's sit drop (a Sit doll sinks 0.03 doll units), lifted back
        /// so the rider sits on the plank, not through it.</summary>
        public const float SeatTop = SeatH + PlankT * 0.5f, SitDrop = 0.03f;
        /// <summary>#692 p5 retakes: the ropes run up into the crown's underside (a grown tree's crown bottom: 0.23 x its scale − 0.02, the scale
        /// = its spot radius / 0.16), at least <see cref="RopeLen"/> over the seat.</summary>
        public static float RopeTop(float crownR) => Mathf.Max(SeatH + RopeLen, 0.23f * crownR / 0.16f - 0.02f + 0.015f);
        /// <summary>B53.d: the swing's tree stands within this of the fire; a grown-up within <see cref="GrownNear"/> while a kid swings [est].</summary>
        public const float SwingReach = 1.5f, GrownNear = 1f;

        /// <summary>The swing's tree: a grown wild tree (stage 4), never the Keeper's, within <see cref="SwingReach"/> of the fire.</summary>
        public static bool SwingTreeOk(bool keepersTree, bool grown, float distToFire) => !keepersTree && grown && distToFire <= SwingReach;

        /// <summary>How far the plank swings out each way from under the pivot (u).</summary>
        public static float ArcReach => RopeLen * Mathf.Sin(ArcDeg * Mathf.Deg2Rad);

        /// <summary>B53.d: the arc never over water, a path or the fire, and the jump-off lands on open ground: the plank's two ends and the landing
        /// (<see cref="ArcReach"/> + 0.06 u ahead) are checked with <paramref name="bad"/> (true = water, a path, a tent), and the arc keeps 0.3 u from the fire.</summary>
        public static bool SwingSpotOk(Vector2 pivot, Vector2 fwd, Vector2 fire, System.Func<Vector2, bool> bad)
        {
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector2.right;
            var pts = new[] { pivot, pivot + fwd * ArcReach, pivot - fwd * ArcReach, pivot + fwd * (ArcReach + 0.06f) };
            foreach (var p in pts)
            {
                if ((p - fire).magnitude < 0.3f) return false;
                if (bad != null && bad(p)) return false;
            }
            return true;
        }

        /// <summary>A ride lasts a whole number of swings plus a half, so it ends at the top of a front swing (the jump-off): 0.5 + 2k s, k ≥ 1.</summary>
        public static float RideLength(float want) => 0.5f + 2f * Mathf.Max(1, Mathf.RoundToInt((want - 0.5f) / 2f));

        /// <summary>The swing's angle (deg, + toward the front) <paramref name="u"/> s into a ride of <paramref name="ride"/> s: it builds up over the
        /// first 1.5 s; after the ride the empty swing rocks to a stop (tau <see cref="ArcTau"/>).</summary>
        public static float SwingTheta(float u, float ride)
        {
            if (u < 0f) return 0f;
            float w = Mathf.PI * 2f * ArcHz;
            if (u <= ride) return ArcDeg * Mathf.Min(1f, 0.35f + u / 1.5f) * Mathf.Sin(w * u);
            float a = ArcDeg * Mathf.Sin(w * ride);
            float d = u - ride;
            return a * Mathf.Cos(w * d) * Mathf.Exp(-d / ArcTau);
        }

        /// <summary>The plank's offset from under the pivot at angle <paramref name="deg"/>: forward (u) and up (u).</summary>
        public static Vector2 SeatOffset(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(RopeLen * Mathf.Sin(r), RopeLen * (1f - Mathf.Cos(r))); }

        /// <summary>#692 p5 retakes: how far short of the drawn water's edge a bank spot stands (box u) [est].</summary>
        public const float ShoreBack = 0.035f, ShoreReach = 0.6f;

        /// <summary>The drawn shore from a bank spot: walk along <paramref name="toWater"/> (0.01 u steps, up to <see cref="ShoreReach"/>) to the
        /// first point where the pond's sheet is drawn (<paramref name="wet"/>: BoxView.WaterDrawnAt; on the gentle banks the sheet ends 0.1–0.2 u
        /// past where the ground dips under the level) and come back <see cref="ShoreBack"/>; the spot itself when the water is not that near.</summary>
        public static Vector2 ToShore(Vector2 bank, Vector2 toWater, System.Func<float, float, bool> wet)
        {
            toWater = toWater.sqrMagnitude > 1e-6f ? toWater.normalized : Vector2.up;
            for (float d = 0f; d <= ShoreReach + 1e-4f; d += 0.01f)
            {
                var p = bank + toWater * d;
                if (wet(p.x, p.y)) return bank + toWater * Mathf.Max(0f, d - ShoreBack);
            }
            return bank;
        }

        /// <summary>B45.a: where skip <paramref name="i"/> rings (i = SkipSteps.Length: the sink), from the thrower at the bank.</summary>
        public static Vector2 SkipPoint(Vector2 bank, Vector2 toWater, int i)
        {
            float d = SkipFirst;
            for (int k = 0; k < i && k < SkipSteps.Length; k++) d += SkipSteps[k];
            if (i >= SkipSteps.Length) d += 0.03f;
            return bank + toWater.normalized * d;
        }

        /// <summary>B45.b: the leaf boat <paramref name="t"/> s after the push: drifting out at <see cref="BoatSpeed"/>, its heading turning
        /// <see cref="BoatTurn"/> deg/s, until <paramref name="len"/> u out (then it stays).</summary>
        public static Vector2 BoatAt(Vector2 start, Vector2 dir, float t, float len, out float yaw)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
            float tEnd = len / BoatSpeed;
            float tt = Mathf.Clamp(t, 0f, tEnd);
            // the drift curves a little (a quarter of the spin bends the way) [est]
            float bend = 0.25f * BoatTurn * Mathf.Deg2Rad;
            var p = start;
            const int steps = 12;
            for (int i = 0; i < steps; i++)
            {
                float a = bend * tt * (i + 0.5f) / steps;
                var d = new Vector2(dir.x * Mathf.Cos(a) - dir.y * Mathf.Sin(a), dir.x * Mathf.Sin(a) + dir.y * Mathf.Cos(a));
                p += d * BoatSpeed * tt / steps;
            }
            yaw = BoatTurn * Mathf.Max(0f, t) % 360f;
            return p;
        }
    }

    public static class CampScenes5
    {
        internal const float Walk = CampScenes1.Walk, KidWalk = CampScenes1.KidWalk, KidRun = CampScenes1.KidRun, Snap = 5f;

        internal static float WalkTime(Vector2 a, Vector2 b, float speed) => Mathf.Clamp((b - a).magnitude / speed + 0.25f, 0.4f, 9f);
        internal static Vector2 Dir(Vector2 from, Vector2 to, Vector2 fallback) { var d = to - from; return d.sqrMagnitude > 1e-6f ? d.normalized : fallback; }
        internal static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        internal static Vector2 Unit(Vector2 v, Vector2 fallback) => v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
        internal static Vector3 G(Vector2 p, float h = 0f) => new Vector3(p.x, h, p.y);
        /// <summary>A part 5 mark at a spot (CampLife.Camp draws it).</summary>
        internal static void Leave(CampScene s, float t, CampMark m, Vector2 at, float h = 0f, float scale = 1f) => s.Fx(t, CampFxKind.Leave, G(at, h), scale, (int)m);
        /// <summary>A part 5 mark worn by a member (a crown, a purple mouth).</summary>
        internal static void Worn(CampScene s, float t, CampMark m, int member) => s.Fx(t, CampFxKind.Leave, Vector3.zero, 1f, (int)m, CampAnchor.Head, member);
        internal static CampPart Go(CampScene s, int m, float t, float len, Vector2 to, Vector2 face, float speed, bool run = false)
        {
            var p = s.Part(m, t, t + len, to, face, DollStyle.Stand, null, PropKind.None, speed); p.run = run; return p;
        }

        // ================================================================== shared poses
        /// <summary>A crouch (squash 0.8, lean 20).</summary>
        internal static void CrouchPose(ref DollPose p, float t, float len) { float d = In(t, 0f, 0.2f); p.squash = Mathf.Lerp(1f, 0.8f, d); p.offset.y -= 0.012f * d; p.lean += 20f * d; }
        /// <summary>B4's kneel (squash 0.72, low, lean 30).</summary>
        internal static void KneelPose(ref DollPose p, float t, float len) { float d = In(t, 0f, 0.3f); p.squash = Mathf.Lerp(1f, 0.72f, d); p.offset.y -= 0.02f * d; p.lean += 30f * d; }
        /// <summary>Both arms up (x 150): so many!</summary>
        internal static void ArmsUpPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.2f); p.armL = Lerp(p.armL, new Vector3(150f, 0f, -20f), u); p.armR = Lerp(p.armR, new Vector3(150f, 0f, 20f), u); p.offset.y += 0.02f * Bump(t, 0.1f, 0.4f); }
        /// <summary>On tiptoe, reaching up (arms x 160, +0.02 u), the hands fiddling (±10 deg at 4 Hz).</summary>
        internal static void TiptoeTiePose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.25f), f = Mathf.Sin(t * 4f * Mathf.PI * 2f) * 10f * u;
            p.offset.y += 0.02f * u; p.armL = Lerp(p.armL, new Vector3(160f + f, 0f, -8f), u); p.armR = Lerp(p.armR, new Vector3(160f - f, 0f, 8f), u);
        }
        /// <summary>Both hands at the chest working something (x 90, a fiddle ±10 deg at 4 Hz).</summary>
        internal static void FiddleHandsPose(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f), f = Mathf.Sin(t * 4f * Mathf.PI * 2f) * 10f * u;
            p.armL = Lerp(p.armL, new Vector3(90f + f, 0f, 14f), u); p.armR = Lerp(p.armR, new Vector3(90f - f, 0f, -14f), u);
        }
        /// <summary>A small nod (lean 8, 0.3 s).</summary>
        internal static void NodPose(ref DollPose p, float t, float len) { p.lean += 8f * Bump(t, 0f, Mathf.Min(0.3f, len)); }
        /// <summary>Hands on the hips (arms z 70 out to the sides, bent in), a laugh bob.</summary>
        internal static void HipsPose(ref DollPose p, float t, float len) { p.armL = new Vector3(20f, 0f, -70f); p.armR = new Vector3(20f, 0f, 70f); p.offset.y += 0.006f * Mathf.Abs(Mathf.Sin(t * 8f)); }
        /// <summary>A rub on a kid's head (arm x 110, 0.5 s).</summary>
        internal static void HeadRubPose(ref DollPose p, float t, float len) { float u = Bump(t, 0f, len); p.armR = Lerp(p.armR, new Vector3(110f, 0f, 6f + 8f * Mathf.Sin(t * 20f)), u); }
        /// <summary>A point at something (arm x 90).</summary>
        internal static void PointPose(ref DollPose p, float t, float len) { float u = In(t, 0f, 0.2f); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 4f), u); }
        /// <summary>The head shaking at them (yaw ±20 deg at 4 Hz, 0.5 s).</summary>
        internal static void HeadShakePose(ref DollPose p, float t, float len) { p.yaw += 20f * Mathf.Sin(t * 4f * Mathf.PI * 2f) * (1f - In(t, len - 0.15f, 0.15f)); }
        /// <summary>A slow look left and right (yaw ±<paramref name="deg"/> over <paramref name="per"/> s each way).</summary>
        internal static CampPoseFn LookRound(float deg, float per, float lean = 0f) => (ref DollPose p, float t, float len) =>
        {
            p.yaw += deg * Mathf.Sin(t / per * Mathf.PI); p.lean += lean;
        };
        /// <summary>A hop up (0.05 u) with both arms up (x 170) and a clap at the top (both arms meet, the hit at 0.22 s).</summary>
        internal static void ClapJumpPose(ref DollPose p, float t, float len)
        {
            float h = Bump(t, 0f, 0.45f);
            p.offset.y += 0.05f * h;
            float meet = In(t, 0.12f, 0.1f) * (1f - In(t, 0.3f, 0.12f));
            p.armL = new Vector3(170f, 0f, Mathf.Lerp(-30f, 2f, meet)); p.armR = new Vector3(170f, 0f, Mathf.Lerp(30f, -2f, meet));
        }

        /// <summary>A poke each <paramref name="every"/> s (arm x 70, a head bob with each), <paramref name="n"/> pokes, crouched.</summary>
        internal static CampPoseFn PokeCountPose(int n, float every) => (ref DollPose p, float t, float len) =>
        {
            CrouchPose(ref p, t, len);
            int k = Mathf.FloorToInt(t / every);
            if (k >= n) return;
            float w = t - k * every;
            float u = Bump(w, 0f, every * 0.8f);
            p.armR = Lerp(p.armR, new Vector3(70f, 0f, 6f), Mathf.Max(0.5f, u));
            p.lean += 6f * u;
            p.yaw += (k % 2 == 0 ? 8f : -8f) * u;
        };

        // ================================================================== B40.a kids peek out of the tents
        public const float PeekLook = 1f;

        /// <summary>B40.a: a kid inside the door (0.04 u in) pops the head out (Peek, a lean out), looks left and right (yaw ±30, 1 s), ducks back in
        /// (0.2 s), pops out again and runs out. <paramref name="out1"/>: from the tent out of its door. <see cref="CampScene.a"/>: the first pop.</summary>
        public static CampScene TentPeek(int kid, Vector2 door, Vector2 out1, float t0)
        {
            out1 = Unit(out1, Vector2.down);
            var s = new CampScene(CampUse.TentPeek, t0, door + out1 * 0.05f) { holdFocus = true };
            int m = s.Add(kid);
            var inside = door - out1 * 0.04f;
            var face = door + out1;
            float t = 0f;
            s.Part(m, t, t + 0.6f, inside, face, DollStyle.Hidden, null, PropKind.None, Snap); t += 0.6f; // in the tent: the doll waits unseen at the door
            s.a = t;
            s.Part(m, t, t + 0.3f, inside, face, DollStyle.Peek, (ref DollPose p, float u, float l) => { p.lean += 22f * In(u, 0f, 0.2f); p.squash = 0.9f; }); t += 0.3f;
            s.Part(m, t, t + PeekLook, inside, face, DollStyle.Peek, (ref DollPose p, float u, float l) => { p.lean += 22f; p.squash = 0.9f; p.yaw += 30f * Mathf.Sin(u / l * Mathf.PI * 2f); }); t += PeekLook;
            s.Part(m, t, t + 0.2f, inside, face, DollStyle.Peek, (ref DollPose p, float u, float l) => { p.lean += 22f * (1f - In(u, 0f, 0.2f)); }); t += 0.2f;
            s.Part(m, t, t + 0.5f, inside, face, DollStyle.Hidden); t += 0.5f; // ducked back in
            s.Part(m, t, t + 0.3f, inside, face, DollStyle.Peek, (ref DollPose p, float u, float l) => { p.lean += 22f * In(u, 0f, 0.15f); }); t += 0.3f;
            var outAt = door + Rot(out1, 25f) * 0.4f;
            Go(s, m, t, WalkTime(inside, outAt, KidRun), outAt, outAt + out1, KidRun, true);
            return s;
        }

        // ================================================================== B40.b a toy or a charm on the tent
        /// <summary>B40.b: reaches up to the tent's pole on tiptoe (arms x 160, +0.02 u), ties the thing (fiddle 1 s) → hit: it pops on and dangles
        /// (a <see cref="CampMark.Toy"/> or <see cref="CampMark.Beads"/> at the pole); the door flap jolts; a flick (arm x 140) spins it; they watch.
        /// <see cref="CampScene.a"/>: the tie.</summary>
        public static CampScene TentToy(int who, Vector2 at, Vector2 pole, Vector2 door, bool beads, float t0, Vector2 toCam = default)
        {
            var s = new CampScene(CampUse.TentToy, t0, pole) { holdFocus = true };
            int m = s.Add(who);
            var away = Unit(pole - door, Vector2.right);
            var stand = pole + Rot(away, 70f) * 0.06f;
            if (toCam.sqrMagnitude > 1e-6f)
            {   // #692 p5 retakes (the tier stood in front of the pole): beside it as the camera sees it, on the side away from the tent
                toCam = toCam.normalized;
                var across = new Vector2(-toCam.y, toCam.x);
                if (Vector2.Dot(across, away) < 0f) across = -across;
                stand = pole + across * 0.065f - toCam * 0.01f;
            }
            float t = 0f, w = WalkTime(at, stand, KidWalk);
            s.Part(m, t, t + w, stand, pole, DollStyle.Stand, null, PropKind.None, KidWalk); t += w;
            s.Tip(m, 0f, t + 1.25f, beads ? CampTipKind.Beads : CampTipKind.Toy);
            s.Part(m, t, t + 1.3f, stand, pole, DollStyle.Stand, TiptoeTiePose); t += 1.25f;
            s.a = t;
            Leave(s, t, beads ? CampMark.Beads : CampMark.Toy, pole, CampGeo5.PoleH - 0.03f);
            s.Fx(t, CampFxKind.Jolt, G(door));
            t += 0.05f;
            s.Part(m, t, t + 0.5f, stand, pole, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(140f, 0f, 10f), k); });
            Leave(s, t + 0.3f, CampMark.ToySpin, pole, CampGeo5.PoleH - 0.03f);
            t += 0.5f;
            s.Part(m, t, t + 1.6f, stand, pole, DollStyle.LookUp); t += 1.6f;
            Go(s, m, t, WalkTime(stand, at, KidWalk) * 0.5f, Vector2.Lerp(stand, at, 0.5f), at, KidWalk);
            return s;
        }

        // ================================================================== B40.c sitting in the doorway at dusk
        /// <summary>B40.c: a grown-up sits at the door, half in (0.03 u in), leaning back −5, watching the fire; a kid (−1: none) comes and sits
        /// against them, an arm round the kid (z ∓70). After <paramref name="sitFor"/> they go in. <see cref="CampScene.a"/>: the arm round.</summary>
        public static CampScene TentDoorSit(int grown, int kid, Vector2 gAt, Vector2 kAt, Vector2 door, Vector2 out1, Vector2 fire, float sitFor, float t0, Vector2 toCam = default)
        {
            out1 = Unit(out1, Vector2.down);
            var side = new Vector2(-out1.y, out1.x);
            if (toCam.sqrMagnitude > 1e-6f)
            {   // #692 p5 retakes (the kid sat in front of the grown-up): beside them across the camera's view, on the hugging arm's side
                var across = new Vector2(-toCam.normalized.y, toCam.normalized.x);
                side = Vector2.Dot(across, side) >= 0f ? across : -across;
            }
            var s = new CampScene(CampUse.TentDoorSit, t0, door + out1 * 0.05f) { holdFocus = true };
            int g = s.Add(grown);
            var seat = door - out1 * 0.03f;
            float t = 0f, w = WalkTime(gAt, seat, Walk);
            s.Part(g, t, t + w, seat, fire, DollStyle.Stand, null, PropKind.None, Walk); t += w;
            float sat = t;
            sitFor = Mathf.Max(6f, sitFor);
            bool withKid = kid >= 0;
            float hugAt = sat + 2.2f;
            s.Part(g, t, t + sitFor, seat, fire, DollStyle.Sit, (ref DollPose p, float u, float l) =>
            {
                p.lean -= 5f;
                if (withKid) { float k = In(u, hugAt - sat, 0.4f); p.armL = Lerp(p.armL, new Vector3(40f, 0f, -70f), k); }
            });
            s.a = hugAt;
            t += sitFor;
            if (withKid)
            {
                int k = s.Add(kid);
                var kseat = seat + side * 0.07f + out1 * 0.01f;
                float wk = WalkTime(kAt, kseat, KidWalk);
                float from = Mathf.Max(0f, hugAt - 0.5f - wk);
                s.Part(k, from, from + wk, kseat, fire, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(k, from + wk, t, kseat, fire, DollStyle.Sit, (ref DollPose p, float u, float l) => { p.roll += 8f * In(u, 0.3f, 0.4f); });
                s.Part(k, t, t + 0.7f, door - out1 * 0.06f, door - out1, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(k, t + 0.7f, t + 1.4f, door - out1 * 0.06f, door - out1, DollStyle.Hidden);
            }
            s.Part(g, t, t + 0.8f, door - out1 * 0.06f, door - out1, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(g, t + 0.8f, t + 1.4f, door - out1 * 0.06f, door - out1, DollStyle.Hidden);
            return s;
        }

        // ================================================================== B41.a kids count the berries (and B42.c the fish)
        public const int CountPokes = 5;
        public const float PokeEvery = 0.5f;

        /// <summary>B41.a / B42.c: two kids crouch at the things (<paramref name="things"/>: the baskets, or the fish along the rack); one pokes each in
        /// turn (arm x 70, one poke every 0.5 s, a head bob each, 5 pokes) moving along them; then both throw the arms up (x 150) and cheer.
        /// <see cref="CampScene.a"/>: the arms up. A <see cref="CampMark.StripTouch"/> at each poked thing (the rack's strips swing).</summary>
        public static CampScene CountThings(CampUse use, int k1, int k2, Vector2 at1, Vector2 at2, IList<Vector2> things, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var mid = Vector2.zero; foreach (var p in things) mid += p; mid /= Mathf.Max(1, things.Count);
            var s = new CampScene(use, t0, mid) { holdFocus = true };
            int a = s.Add(k1), b = k2 >= 0 ? s.Add(k2) : -1;
            var line = things.Count > 1 ? Unit(things[things.Count - 1] - things[0], new Vector2(-toCam.y, toCam.x)) : new Vector2(-toCam.y, toCam.x);
            var front = toCam; // they crouch on the camera's side, facing the things
            Vector2 Stand(int i) => things[Mathf.Clamp(i, 0, things.Count - 1)] + front * 0.08f;
            var sa = Stand(0); var sb = mid + front * 0.14f - line * 0.03f;
            float t = Mathf.Max(WalkTime(at1, sa, KidWalk), b >= 0 ? WalkTime(at2, sb, KidWalk) : 0f);
            s.Part(a, 0f, t, sa, things[0], DollStyle.Stand, null, PropKind.None, KidWalk);
            if (b >= 0) s.Part(b, 0f, t, sb, mid, DollStyle.Stand, null, PropKind.None, KidWalk);
            float start = t;
            for (int i = 0; i < CountPokes; i++)
            {
                int ti = things.Count <= 1 ? 0 : Mathf.RoundToInt(i * (things.Count - 1) / (float)(CountPokes - 1));
                var st = Stand(ti);
                s.Part(a, t, t + PokeEvery, st, things[ti], DollStyle.Stand, PokeCountPose(1, PokeEvery), PropKind.None, 0.4f);
                Leave(s, t + PokeEvery * 0.4f, CampMark.StripTouch, things[ti]);
                t += PokeEvery;
            }
            if (b >= 0) s.Part(b, start, t, sb, mid, DollStyle.Stand, (ref DollPose p, float u, float l) => { CrouchPose(ref p, u, l); p.yaw += 10f * Mathf.Sin(u * 2f * Mathf.PI); });
            s.a = t;
            s.Part(a, t, t + 0.5f, Stand(things.Count - 1), mid, DollStyle.Stand, ArmsUpPose);
            if (b >= 0) s.Part(b, t, t + 0.5f, sb, mid, DollStyle.Stand, ArmsUpPose);
            t += 0.5f;
            s.Part(a, t, t + 1.2f, Stand(things.Count - 1), mid, DollStyle.Cheer);
            if (b >= 0) s.Part(b, t, t + 1.2f, sb, mid, DollStyle.Cheer);
            t += 1.2f;
            var away = mid + Rot(front, 40f) * 0.45f;
            Go(s, a, t, WalkTime(Stand(things.Count - 1), away, KidRun), away, away + front, KidRun, true);
            if (b >= 0) Go(s, b, t, WalkTime(sb, away, KidRun), away + line * 0.1f, away + front, KidRun, true);
            return s;
        }

        // ================================================================== B41.b a snack sneaked and caught
        /// <summary>B41.b: the sneak sidles up looking left and right (yaw ±40, twice), reaches into a basket (arm x 60, lean 25), a berry pops into
        /// the hand, up to the mouth → hit: the grown-up at the stash turns (0.2 s) and points; the sneak freezes (0.5 s, the hand at the mouth),
        /// eats it fast and runs; is caught after 0.3 u by the arm (both stop); the grown-up laughs, then a rub on the head (0.5 s).
        /// <see cref="CampScene.a"/>: the hand at the mouth.</summary>
        public static CampScene SnackSneak(int sneak, Vector2 sAt, int grown, Vector2 gAt, Vector2 pile, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var across = new Vector2(-toCam.y, toCam.x);
            var s = new CampScene(CampUse.SnackSneak, t0, pile + toCam * 0.1f) { holdFocus = true };
            int k = s.Add(sneak), g = s.Add(grown);
            var reach = pile + Rot(toCam, 55f) * 0.11f;
            var gStand = pile + Rot(toCam, -60f) * 0.13f;
            float t = 0f;
            float wg = WalkTime(gAt, gStand, Walk);
            s.Part(g, 0f, wg, gStand, pile, DollStyle.Stand, null, PropKind.None, Walk);
            float ws = Mathf.Max(WalkTime(sAt, reach, KidWalk * 0.6f), wg);
            var sidle = s.Part(k, t, t + ws, reach, pile, DollStyle.Stand, LookRound(40f, ws / 4f, 6f), PropKind.None, KidWalk * 0.6f); sidle.poseWalking = true;
            s.Part(g, wg, ws + 0.9f, gStand, pile, DollStyle.SortBasket);
            t += ws;
            s.Part(k, t, t + 0.5f, reach, pile, DollStyle.Stand, (ref DollPose p, float u, float l) => { float d = Bump(u, 0f, l); p.lean += 25f * d; p.armR = Lerp(p.armR, new Vector3(60f, 0f, 6f), d); });
            s.Tip(k, t + 0.35f, t + 2.1f, CampTipKind.Berry);
            t += 0.5f;
            s.Part(k, t, t + 0.4f, reach, pile + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armR = Lerp(p.armR, new Vector3(135f, 0f, -10f), In(u, 0f, 0.3f)); });
            t += 0.4f;
            s.a = t;
            // caught: the grown-up turns and points; the sneak freezes with the hand at the mouth
            s.Part(g, ws + 0.9f, t + 0.2f, gStand, pile, DollStyle.SortBasket);
            s.Part(g, t + 0.2f, t + 0.9f, gStand, reach, DollStyle.Stand, PointPose);
            s.Part(k, t, t + 0.5f, reach, gStand, DollStyle.Freeze, (ref DollPose p, float u, float l) => { p.armR = new Vector3(135f, 0f, -10f); });
            t += 0.5f;
            s.Part(k, t, t + 0.6f, reach, gStand, DollStyle.Taste); t += 0.6f;
            var run = reach + Unit(reach - gStand, across) * 0.3f;
            float wr = WalkTime(reach, run, KidRun);
            Go(s, k, t, wr, run, run + (run - reach), KidRun, true);
            var catchAt = run - Unit(run - reach, across) * 0.06f;
            s.Part(g, s.a + 0.9f, t + wr + 0.1f, catchAt, run, DollStyle.Stand, null, PropKind.None, 0.45f).run = true;
            t += wr + 0.1f;
            s.Part(k, t - 0.1f, t + 1.6f, run, catchAt, DollStyle.Laugh);
            s.Part(g, t, t + 1f, catchAt, run, DollStyle.Laugh); t += 1f;
            s.Part(g, t, t + 0.6f, catchAt, run, DollStyle.Stand, HeadRubPose); t += 0.6f;
            return s;
        }

        // ================================================================== B42.a tending the racks (work)
        public const float TouchEach = 0.3f, FanFor = 2f, ScrapeFor = 3f;

        /// <summary>B42.a: the keeper walks the fish rack's bar touching each hanging thing (arm x 150, 0.3 s each; each swings: a
        /// <see cref="CampMark.StripTouch"/>), turns one over (a longer touch, the arm twisting), fans the smoke (2 s); then at the hide rack (when
        /// <paramref name="hasHide"/>) scrapes (ScrapeHide, the scraper stone) and tugs a corner taut (arm x 90, back 0.04 u) → hit: a jolt.
        /// <see cref="CampScene.a"/>: the tug (or the fan's end without a hide).</summary>
        public static CampScene TendRacks(int keeper, Vector2 at, IList<Vector2> pieces, Vector2 rackMid, Vector2 toCam, bool hasHide, Vector2 hideMid, Vector2 hideAlong, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.TendRacks, t0, rackMid) { followReach = 0.9f };
            int m = s.Add(keeper);
            float t = 0f;
            var from = at;
            for (int i = 0; i < pieces.Count; i++)
            {
                var st = pieces[i] + toCam * 0.07f;
                float w = i == 0 ? WalkTime(from, st, Walk) : 0.35f;
                s.Part(m, t, t + w, st, pieces[i], DollStyle.Stand, null, PropKind.None, i == 0 ? Walk : 0.3f); t += w;
                bool turn = i == pieces.Count / 2;
                float len = turn ? 0.6f : TouchEach;
                s.Part(m, t, t + len, st, pieces[i], DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float k = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(150f, turn ? 40f * Mathf.Sin(u / l * Mathf.PI) : 0f, 6f), k);
                });
                Leave(s, t + len * 0.5f, CampMark.StripTouch, pieces[i]);
                t += len;
                from = st;
            }
            // fan the smoke (B16.c): the arm waves low at the side
            s.Part(m, t, t + FanFor, from, rackMid, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armR = new Vector3(50f, 0f, 30f + 35f * Mathf.Sin(u * 3f * Mathf.PI * 2f)); p.lean += 10f; });
            s.Fx(t + 0.6f, CampFxKind.Steam, G(rackMid, 0.05f), 0.4f, 1);
            t += FanFor;
            s.a = t;
            if (hasHide)
            {
                hideAlong = Unit(hideAlong, Vector2.right);
                var hs = hideMid + toCam * 0.1f;
                float w = WalkTime(from, hs, Walk);
                s.Part(m, t, t + w, hs, hideMid, DollStyle.Stand, null, PropKind.None, Walk); t += w;
                s.Part(m, t, t + ScrapeFor, hs, hideMid, DollStyle.ScrapeHide, null, PropKind.Scraper); t += ScrapeFor;
                var corner = hideMid + hideAlong * 0.08f + toCam * 0.1f;
                s.Part(m, t, t + 0.4f, corner, hideMid + hideAlong * 0.08f, DollStyle.Stand, null, PropKind.None, 0.3f); t += 0.4f;
                s.Part(m, t, t + 0.6f, corner, hideMid + hideAlong * 0.08f, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float k = In(u, 0f, 0.2f) * (1f - In(u, 0.45f, 0.15f)); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), k); p.armL = Lerp(p.armL, new Vector3(90f, 0f, -6f), k);
                    p.offset.z += 0.04f * In(u, 0.15f, 0.15f) * (1f - In(u, 0.45f, 0.15f)); p.lean -= 8f * k;
                });
                s.a = t + 0.3f;
                s.Fx(t + 0.3f, CampFxKind.Jolt, G(hideMid));
                s.Fx(t + 0.3f, CampFxKind.Dust, G(hideMid, 0.08f), 0.3f, 2);
                t += 0.6f;
            }
            s.Part(m, t, t + 0.5f, s.parts[m][s.parts[m].Count - 1].pos, rackMid, DollStyle.Stand, NodPose);
            return s;
        }

        // ================================================================== B42.b kids duck under the hide rack
        /// <summary>B42.b: in a chase that passes the rack, the runner ducks (squash 0.7, lean 25, 0.3 s) under the hides, out the other side with a
        /// hop; the chaser goes round the end. The scraper (−1: none) shoos them (B17.b: arm z ∓90, two flaps). <see cref="CampScene.a"/>: the duck.</summary>
        public static CampScene DuckUnderHides(int runner, int chaser, Vector2 rAt, Vector2 cAt, Vector2 hideMid, Vector2 along, int scraper, Vector2 scAt, float t0)
        {
            along = Unit(along, Vector2.right);
            var cross = new Vector2(-along.y, along.x);
            var s = new CampScene(CampUse.DuckUnderHides, t0, hideMid) { holdFocus = true };
            int r = s.Add(runner), c = s.Add(chaser);
            var startR = hideMid + cross * 0.35f; var under = hideMid; var outR = hideMid - cross * 0.25f;
            var startC = startR + cross * 0.12f + along * 0.04f; var roundC = hideMid + along * 0.2f; var endC = outR + along * 0.08f;
            float t = Mathf.Max(WalkTime(rAt, startR, KidRun), WalkTime(cAt, startC, KidRun));
            Go(s, r, 0f, t, startR, under, KidRun, true);
            Go(s, c, 0f, t, startC, under, KidRun, true);
            float w1 = (under - startR).magnitude / KidRun;
            Go(s, r, t, w1 - 0.15f, under + cross * 0.05f, under, KidRun, true); float duck = t + w1 - 0.15f;
            s.Part(r, duck, duck + 0.3f, under - cross * 0.05f, outR, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Bump(u, 0f, l); p.squash = Mathf.Lerp(1f, 0.7f, k); p.lean += 25f * k; }, PropKind.None, KidRun);
            s.a = duck + 0.15f;
            s.Fx(duck + 0.15f, CampFxKind.Jolt, G(hideMid));
            float w2 = (outR - under).magnitude / KidRun;
            s.Part(r, duck + 0.3f, duck + 0.3f + w2, outR, outR - cross, DollStyle.Hop, null, PropKind.None, KidRun);
            var cWay = WalkTime(startC, roundC, KidRun) + WalkTime(roundC, endC, KidRun);
            Go(s, c, t, cWay * 0.5f, roundC, roundC - cross, KidRun, true);
            Go(s, c, t + cWay * 0.5f, cWay * 0.5f, endC, outR, KidRun, true);
            float end = Mathf.Max(duck + 0.3f + w2, t + cWay);
            s.Part(r, duck + 0.3f + w2, end + 1.2f, outR, endC, DollStyle.Laugh);
            s.Part(c, t + cWay, end + 1.2f, endC, outR, DollStyle.Laugh);
            if (scraper >= 0)
            {
                int sc = s.Add(scraper);
                s.Part(sc, 0f, s.a, scAt, hideMid, DollStyle.ScrapeHide, null, PropKind.Scraper).keepSpot = true;
                s.Part(sc, s.a, s.a + 0.7f, scAt, outR, DollStyle.Stand, (ref DollPose p, float u, float l) => { float f = Mathf.Abs(Mathf.Sin(u / l * Mathf.PI * 2f)); p.armL = new Vector3(10f, 0f, -90f * f); p.armR = new Vector3(10f, 0f, 90f * f); }).keepSpot = true;
            }
            return s;
        }

        // ================================================================== B42.d a kid sneaks a fish
        /// <summary>B42.d: on tiptoe (+0.03 u) a kid pulls a fish off the bar (a <see cref="CampMark.FishTaken"/>: the rack's count drops by one) and runs;
        /// the keeper chases 0.4 u, gives up, hands on hips, and laughs; behind a tent the kid shares it with another kid (both taste).
        /// <see cref="CampScene.a"/>: the pull.</summary>
        public static CampScene SneakFish(int kid, Vector2 kAt, int keeper, Vector2 keepAt, Vector2 fish, Vector2 toCam, int friend, Vector2 fAt, Vector2 behind, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.SneakFish, t0, fish) { followReach = 1.2f };
            int k = s.Add(kid);
            var st = fish + toCam * 0.06f;
            float t = WalkTime(kAt, st, KidWalk * 0.7f);
            s.Part(k, 0f, t, st, fish, DollStyle.Stand, LookRound(30f, 0.6f, 4f), PropKind.None, KidWalk * 0.7f).poseWalking = true;
            s.Part(k, t, t + 0.7f, st, fish, DollStyle.Stand, (ref DollPose p, float u, float l) => { float d = In(u, 0f, 0.3f); p.offset.y += 0.03f * d; p.armR = Lerp(p.armR, new Vector3(155f, 0f, 6f), d); p.armR.x -= 40f * In(u, 0.5f, 0.15f); });
            t += 0.55f;
            s.a = t;
            Leave(s, t, CampMark.FishTaken, fish);
            t += 0.15f;
            var run = Vector2.Lerp(st, behind, 0.5f);
            float wr = WalkTime(st, behind, KidRun);
            s.Part(k, t, t + wr, behind, behind + (behind - st), DollStyle.Stand, null, PropKind.Fish, KidRun).run = true;
            int g = s.Add(keeper);
            var chaseTo = st + Unit(behind - st, Vector2.right) * 0.4f;
            s.Part(g, 0f, s.a, keepAt, fish, DollStyle.Stand).keepSpot = true;
            s.Part(g, s.a, s.a + 0.25f, keepAt, st, DollStyle.Stand, PointPose).keepSpot = true;
            s.Part(g, s.a + 0.25f, s.a + 0.25f + WalkTime(keepAt, chaseTo, 0.42f), chaseTo, behind, DollStyle.Stand, null, PropKind.None, 0.42f).run = true;
            float gs = s.a + 0.25f + WalkTime(keepAt, chaseTo, 0.42f);
            s.Part(g, gs, gs + 1.6f, chaseTo, behind, DollStyle.Stand, HipsPose);
            s.Part(g, gs + 1.6f, gs + 2.6f, chaseTo, behind, DollStyle.Laugh);
            t += wr;
            if (friend >= 0)
            {
                int f = s.Add(friend);
                var fs = behind + Unit(behind - st, Vector2.right) * 0.06f + toCam * 0.03f;
                float wf = WalkTime(fAt, fs, KidWalk);
                s.Part(f, Mathf.Max(0f, t - wf), t, fs, behind, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(f, t, t + 2f, fs, behind, DollStyle.Taste);
            }
            s.Part(k, t, t + 2f, behind, behind + toCam, DollStyle.Taste, null, PropKind.Fish);
            return s;
        }

        // ================================================================== B43.b peek at rabbits from the thicket
        /// <summary>B43.b: 1–2 kids lie on the belly at the thicket's edge facing the meadow (<paramref name="toMeadow"/>), part a bush with both arms
        /// (x 90, z ∓40; the bush leans: a <see cref="CampFxKind.BushSway"/>, held) and watch 4–6 s; a rabbit sits up: the kids freeze; then they slide
        /// back out. <see cref="CampScene.a"/>: the freeze.</summary>
        public static CampScene ThicketPeek(IList<int> kids, IList<Vector2> at, Vector2 edge, Vector2 bush, Vector2 toMeadow, float watch, float t0)
        {
            toMeadow = Unit(toMeadow, Vector2.up);
            var side = new Vector2(-toMeadow.y, toMeadow.x);
            var s = new CampScene(CampUse.ThicketPeek, t0, edge + toMeadow * 0.1f) { holdFocus = true };
            watch = Mathf.Clamp(watch, 4f, 6f);
            float t = 0f;
            var spots = new Vector2[kids.Count];
            for (int i = 0; i < kids.Count; i++)
            {
                s.Add(kids[i]);
                spots[i] = edge + side * ((i - (kids.Count - 1) * 0.5f) * 0.07f);
                t = Mathf.Max(t, WalkTime(at[i], spots[i] - toMeadow * 0.1f, KidWalk));
            }
            for (int i = 0; i < kids.Count; i++) s.Part(i, 0f, t, spots[i] - toMeadow * 0.1f, spots[i] + toMeadow, DollStyle.Stand, null, PropKind.None, KidWalk);
            // down onto the belly (lean +90: face down, the head toward the meadow), crawl in to the edge
            for (int i = 0; i < kids.Count; i++)
                s.Part(i, t, t + 0.8f, spots[i], spots[i] + toMeadow, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.4f); p.lean = 90f * k; p.offset.y = 0.02f * k; p.armL = new Vector3(150f, 0f, -20f); p.armR = new Vector3(150f, 0f, 20f); }, PropKind.None, 0.12f).poseWalking = true;
            t += 0.8f;
            s.Fx(t + 0.2f, CampFxKind.BushSway, G(bush), 0.6f);
            float freeze = t + watch * 0.55f;
            s.a = freeze;
            for (int i = 0; i < kids.Count; i++)
            {
                int ii = i;
                s.Part(i, t, t + watch, spots[i], spots[i] + toMeadow, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    p.lean = 90f; p.offset.y = 0.02f;
                    float k = In(u, 0f, 0.3f);
                    p.armL = Lerp(new Vector3(150f, 0f, -20f), new Vector3(90f, 0f, -40f), k); p.armR = Lerp(new Vector3(150f, 0f, 20f), new Vector3(90f, 0f, 40f), k);
                    float fz = In(u, watch * 0.55f, 0.1f) * (1f - In(u, watch * 0.55f + 1.4f, 0.2f));
                    p.roll += (ii == 0 ? 6f : -6f) * Mathf.Sin(u * 1.3f) * (1f - fz); // the heads move a little, then still
                });
            }
            t += watch;
            for (int i = 0; i < kids.Count; i++)
                s.Part(i, t, t + 1f, spots[i] - toMeadow * 0.1f, spots[i] + toMeadow, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = 1f - In(u, 0.6f, 0.4f); p.lean = 90f * k; p.offset.y = 0.02f * k; p.armL = new Vector3(150f * k, 0f, -20f); p.armR = new Vector3(150f * k, 0f, 20f); }, PropKind.None, 0.1f).poseWalking = true;
            return s;
        }

        // ================================================================== B43.c berry-stained faces
        /// <summary>B43.c (B17.a's purple mouth): a kid at the stash eats three berries (arm x 135, a chew bob), and has a purple mouth (worn until
        /// the next meal); a parent comes and wipes it off with a thumb (arm x 110, two rubs in 0.5 s): it fades. <see cref="CampScene.a"/>: the
        /// purple mouth; <see cref="CampScene.b"/>: the wipe.</summary>
        public static CampScene BerryWipe(int kid, Vector2 kAt, int parent, Vector2 pAt, Vector2 pile, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var s = new CampScene(CampUse.BerryWipe, t0, pile + toCam * 0.12f) { holdFocus = true };
            int k = s.Add(kid);
            var st = pile + Rot(toCam, 40f) * 0.11f;
            float t = WalkTime(kAt, st, KidWalk);
            s.Part(k, 0f, t, st, pile, DollStyle.Stand, null, PropKind.None, KidWalk);
            for (int i = 0; i < 3; i++)
            {
                s.Part(k, t, t + 0.4f, st, pile, DollStyle.Stand, (ref DollPose p, float u, float l) => { float d = Bump(u, 0f, l); p.lean += 22f * d; p.armR = Lerp(p.armR, new Vector3(60f, 0f, 6f), d); });
                s.Tip(k, t + 0.3f, t + 1.0f, CampTipKind.Berry);
                t += 0.4f;
                s.Part(k, t, t + 0.6f, st, st + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { float d = Bump(u, 0f, 0.4f); p.armR = Lerp(p.armR, new Vector3(135f, 0f, -10f), d); p.offset.y += 0.01f * Mathf.Abs(Mathf.Sin(u * 8f * Mathf.PI)) * In(u, 0.3f, 0.1f); });
                t += 0.6f;
            }
            s.a = t;
            Worn(s, t, CampMark.PurpleMouth, k);
            s.Part(k, t, t + 1.2f, st, st + toCam, DollStyle.Laugh); t += 1.2f;
            if (parent >= 0)
            {
                int g = s.Add(parent);
                var gs = st + Rot(toCam, 80f) * 0.09f; // #692 p5 (the Editor pass): beside the kid as the camera sees it, never in front
                float wg = WalkTime(pAt, gs, Walk);
                s.Part(g, Mathf.Max(0f, t - wg), t, gs, st, DollStyle.Stand, null, PropKind.None, Walk);
                s.Part(k, t, t + 1.2f, st, gs, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.lean -= 6f; });
                s.Part(g, t, t + 0.9f, gs, st, DollStyle.Stand, (ref DollPose p, float u, float l) =>
                {
                    float d = In(u, 0f, 0.2f) * (1f - In(u, 0.7f, 0.2f));
                    p.armR = Lerp(p.armR, new Vector3(110f, 0f, 4f + 10f * Mathf.Sin((u - 0.2f) * 4f * Mathf.PI * 2f)), d); p.lean += 10f * d; p.squash = Mathf.Lerp(1f, 0.92f, d);
                });
                s.b = t + 0.6f;
                Worn(s, t + 0.6f, CampMark.WipeMouth, k);
                t += 1.2f;
                s.Part(g, t - 0.3f, t + 0.6f, gs, st, DollStyle.Stand, NodPose);
            }
            return s;
        }

        // ================================================================== B44.a climb a low branch
        /// <summary>#692 p5 retakes: the seat up the trunk (box u) when the tree is not known; <see cref="ClimbSeatFor"/> from its crown [est].</summary>
        public const float ClimbSeat = 0.12f;

        /// <summary>The seat for a wild tree whose spot radius is <paramref name="crownR"/> (scale = r / 0.16): 0.1 u under the crown's underside
        /// (0.23 x scale − 0.02), between 0.08 and 0.16 u: the kid's head just under the leaves.</summary>
        public static float ClimbSeatFor(float crownR) => Mathf.Clamp(0.23f * crownR / 0.16f - 0.02f - 0.1f, 0.08f, 0.16f);

        /// <summary>B44.a: reaches up (x 170), pulls up (0 → +0.09 u over 0.8 s, a wiggle roll ±10 at 6 Hz), sits on the low branch on the camera's
        /// side of the trunk (+0.09 u, 0.05 u out), swinging (roll ±5 at 1 Hz) <paramref name="sitFor"/> (6–10 s), waves to a kid below (−1: none);
        /// jumps down (a hop, a dust puff). The crown sways at the climb and the jump. <see cref="CampScene.a"/>: up; <see cref="CampScene.b"/>: down.</summary>
        public static CampScene ClimbTree(int kid, Vector2 at, Vector2 trunk, Vector2 toCam, float sitFor, int friend, Vector2 fAt, float t0, float seatH = ClimbSeat, float sc = 1f)
        {
            toCam = Unit(toCam, Vector2.down);
            sc = Mathf.Max(0.3f, sc);
            float up = seatH / sc; // #692 p5 retakes: the seat in box units (a kid's offsets shrink with the doll: +0.09 read as barely off the ground)
            sitFor = Mathf.Clamp(sitFor, 6f, 10f);
            var s = new CampScene(CampUse.ClimbTree, t0, trunk + toCam * 0.05f) { holdFocus = true };
            int m = s.Add(kid);
            var seat = trunk + toCam * 0.05f;
            var foot = trunk + toCam * 0.07f;
            float t = WalkTime(at, foot, KidWalk);
            s.Part(m, 0f, t, foot, trunk, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(m, t, t + 0.4f, foot, trunk, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.3f); p.armL = Lerp(p.armL, new Vector3(170f, 0f, -8f), k); p.armR = Lerp(p.armR, new Vector3(170f, 0f, 8f), k); p.offset.y += 0.01f * k; }); t += 0.4f;
            s.Part(m, t, t + 0.8f, seat, trunk, DollStyle.Stand, (ref DollPose p, float u, float l) =>
            {
                float k = In(u, 0f, 0.8f);
                p.offset.y += up * k + 0.01f; p.roll += 10f * Mathf.Sin(u * 6f * Mathf.PI * 2f) * (1f - k * 0.7f);
                p.armL = new Vector3(Mathf.Lerp(170f, 90f, k), 0f, -8f); p.armR = new Vector3(Mathf.Lerp(170f, 90f, k), 0f, 8f);
            }, PropKind.None, 0.1f);
            s.Fx(t + 0.4f, CampFxKind.TreeShake, G(trunk), 6f);
            t += 0.8f;
            s.a = t;
            float waveAt = sitFor * 0.5f;
            bool waves = friend >= 0;
            s.Part(m, t, t + sitFor, seat, seat + toCam, DollStyle.Sit, (ref DollPose p, float u, float l) =>
            {
                p.offset.y += up + CampGeo5.SitDrop; p.roll += 5f * Mathf.Sin(u * Mathf.PI * 2f);
                if (waves) { float w = Bump(u, waveAt, 1.2f); p.armR = Lerp(p.armR, new Vector3(160f, 0f, 20f + 25f * Mathf.Sin(u * 12f)), Mathf.Min(1f, w * 2f)); }
            });
            if (friend >= 0)
            {
                int f = s.Add(friend);
                var fs = seat + toCam * 0.1f + new Vector2(-toCam.y, toCam.x) * 0.15f; // #692 p5 retakes: beside the tree, not in front of the climber
                float wf = WalkTime(fAt, fs, KidWalk);
                s.Part(f, Mathf.Max(0f, t + waveAt - 0.5f - wf), t + waveAt - 0.5f, fs, seat, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(f, t + waveAt - 0.5f, t + sitFor + 1.2f, fs, seat, DollStyle.Wave);
            }
            t += sitFor;
            var land = foot + toCam * 0.06f;
            s.Part(m, t, t + 0.5f, land, land + toCam, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.4f); p.offset.y += Mathf.Lerp(up, 0f, k) + 0.03f * Mathf.Sin(k * Mathf.PI); p.squash = u > 0.4f ? 0.8f : 1f; }, PropKind.None, 0.35f);
            s.Fx(t + 0.1f, CampFxKind.TreeShake, G(trunk), 6f);
            s.Fx(t + 0.42f, CampFxKind.Dust, G(land, 0.01f), 0.5f, 3);
            s.b = t + 0.42f;
            t += 0.5f;
            s.Part(m, t, t + 1f, land, land + toCam, DollStyle.Laugh);
            return s;
        }

        // ================================================================== B44.b nap in the shade
        /// <summary>#692 p5 retakes (B44.b did not read as a nap): asleep against the bark. Leaning back −18 (the back on the trunk), every 7 s the
        /// sleeper slumps over 2 s (lean +16, a slump to the side, roll 14, so it shows from the front) and holds it, then a little start (0.15 s)
        /// and slumps again; a slow breath (squash 0.7 ± 0.025 at 0.3 Hz); the arms folded in the lap (x 40, z ∓30); the leaf brushed off at
        /// <paramref name="brushAt"/> as B32.a [est].</summary>
        public static CampPoseFn NapPose(float brushAt) => (ref DollPose p, float t, float len) =>
        {
            p.squash = 0.7f + 0.025f * Mathf.Sin(t * 0.3f * Mathf.PI * 2f); p.offset.y = -0.035f;
            float c = Mathf.Repeat(t, 7f);
            float slump = In(c, 0.4f, 2f) * (1f - In(c, 6.7f, 0.15f));
            p.lean = -18f + 16f * slump; p.roll += 14f * slump;
            p.armL = new Vector3(40f, 0f, -30f); p.armR = new Vector3(40f, 0f, 30f);
            float u = Bump(t, brushAt, 0.3f);
            if (u > 0f) p.armR = Lerp(p.armR, new Vector3(120f, 0f, -40f), u);
        };

        // ================================================================== B44.c lean on the trunk
        /// <summary>B44.c: a chat (B7.a) by a wild tree: one leans a shoulder on the trunk (roll 12, 0.05 u off it), the other faces them; they
        /// talk <paramref name="chatFor"/> s (4–6). <see cref="CampScene.a"/>: the lean.</summary>
        /// <summary>#692 p5 retakes: the shoulder lean (spec 12 deg; 16 so it reads beside the bark) [est].</summary>
        public const float LeanRoll = 16f;

        public static CampScene TrunkLean(int a, int b, Vector2 aAt, Vector2 bAt, Vector2 trunk, float trunkR, Vector2 toCam, float chatFor, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var across = new Vector2(-toCam.y, toCam.x);
            var s = new CampScene(CampUse.TrunkLean, t0, trunk + toCam * 0.08f) { holdFocus = true };
            int ma = s.Add(a), mb = s.Add(b);
            // #692 p5 retakes (the pair stood in front of the trunk): the leaner beside the trunk as the camera sees it, the other out to the side and
            // a little toward the camera, both clear of the trunk
            var la = trunk + across * (trunkR + 0.045f) + toCam * 0.01f;
            var lb = la + Unit(toCam * 0.5f + across, across) * 0.15f;
            float t = Mathf.Max(WalkTime(aAt, la, Walk), WalkTime(bAt, lb, Walk));
            s.Part(ma, 0f, t, la, lb, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(mb, 0f, t, lb, la, DollStyle.Stand, null, PropKind.None, Walk);
            s.a = t + 0.4f;
            chatFor = Mathf.Clamp(chatFor, 4f, 6f);
            // the shoulder toward the trunk: roll 12 deg toward it, the far arm loose
            s.Part(ma, t, t + chatFor + 0.4f, la, lb, DollStyle.Talk, (ref DollPose p, float u, float l) => { p.roll -= LeanRoll * In(u, 0f, 0.4f); });
            s.Part(mb, t, t + chatFor + 0.4f, lb, la, DollStyle.Talk);
            t += chatFor + 0.4f;
            s.Part(ma, t, t + 0.5f, la, lb, DollStyle.Stand, NodPose);
            s.Part(mb, t, t + 0.5f, lb, la, DollStyle.Stand, NodPose);
            return s;
        }

        // ================================================================== B45.a skip stones
        /// <summary>B45.a: picks a flat stone (a bend), stands side-on to the water (squash 0.85), arm back low (x −30, z 60, 0.3 s), whips it forward
        /// (Snap 0.12 s) → hit: the stone skims low and skips <paramref name="skips"/> times (a ring each, 0.25 s apart, 0.15 / 0.12 / 0.08 u), then
        /// sinks with a last ring; three skips: a cheer. <paramref name="skips"/> 0 = a kid's plop (one ring, h 0.1): a laugh. Frogs and ducks near
        /// flee (a <see cref="CampMark.SkipSplash"/> at the first ring). <see cref="CampScene.a"/>: the release; <see cref="CampScene.kind"/>: the skips.</summary>
        public static CampScene SkipStones(int who, Vector2 at, Vector2 bank, Vector2 toWater, Vector2 across, int skips, float t0)
        {
            toWater = Unit(toWater, Vector2.up);
            var s = new CampScene(CampUse.SkipStones, t0, bank + toWater * 0.2f) { holdFocus = true, kind = skips };
            int m = s.Add(who);
            var pick = bank - toWater * 0.06f + across * 0.05f;
            float t = WalkTime(at, pick, Walk);
            s.Part(m, 0f, t, pick, pick - toWater * 0.1f, DollStyle.Stand, null, PropKind.None, Walk);
            s.Part(m, t, t + 0.6f, pick, pick - toWater * 0.1f, DollStyle.Stand, CampScenes3.BendPickPose);
            s.Tip(m, t + 0.4f, t + 0.6f + 0.4f + 0.3f + 0.12f + 0.6f, CampTipKind.FlatStone);
            t += 0.6f;
            // side-on: facing along the bank, the throwing arm toward the water
            var sideFace = bank + Rot(toWater, -90f);
            s.Part(m, t, t + 0.4f, bank, sideFace, DollStyle.Stand, null, PropKind.None, 0.3f); t += 0.4f;
            s.Part(m, t, t + 0.3f, bank, sideFace, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = In(u, 0f, 0.3f); p.squash = Mathf.Lerp(1f, 0.85f, k); p.armR = Lerp(p.armR, new Vector3(-30f, 0f, 60f), k); p.lean += 8f * k; });
            t += 0.3f;
            s.Part(m, t, t + 0.12f, bank, sideFace, DollStyle.Stand, (ref DollPose p, float u, float l) => { float k = Smooth(u / 0.12f); p.squash = 0.85f; p.armR = Lerp(new Vector3(-30f, 0f, 60f), new Vector3(70f, 0f, 75f), k); p.yaw += 25f * k; });
            t += 0.12f;
            s.a = t;
            s.Part(m, t, t + 0.5f, bank, sideFace, DollStyle.Stand, (ref DollPose p, float u, float l) => { p.armR = Lerp(new Vector3(70f, 0f, 75f), p.armR, In(u, 0.1f, 0.3f)); p.yaw += 25f * (1f - In(u, 0f, 0.4f)); });
            t += 0.5f;
            float last = s.a;
            if (skips <= 0)
            {
                var plop = bank + toWater * CampGeo5.PlopFar;
                float hit = s.a + 0.35f;
                s.Fx(hit, CampFxKind.WaterRing, G(plop), 0.9f);
                s.Fx(hit, CampFxKind.WaterDrops, G(plop, 0.01f), 0.5f, 4);
                Leave(s, hit, CampMark.SkipSplash, plop);
                last = hit;
            }
            else
            {
                for (int i = 0; i <= skips; i++)
                {
                    float hit = s.a + 0.2f + i * CampGeo5.SkipGap;
                    var pt = CampGeo5.SkipPoint(bank, toWater, i);
                    s.Fx(hit, CampFxKind.WaterRing, G(pt), i < skips ? CampGeo5.SkipRing : CampGeo5.SkipRing * 1.3f); // #692 p5 retakes: 0.6 read as nothing on the pale water
                    if (i == 0) Leave(s, hit, CampMark.SkipSplash, pt);
                    last = hit;
                }
            }
            t = Mathf.Max(t, last + 0.1f);
            bool cheer = skips >= 3;
            s.Part(m, t, t + 1.3f, bank, bank + toWater, cheer ? DollStyle.Cheer : DollStyle.Laugh);
            return s;
        }

        // ================================================================== B45.b a leaf boat
        /// <summary>B45.b: folds a big leaf (both hands x 90, fiddle 1 s), crouches, sets it on the water (arm x 40, lean 35 → hit, a ring) and
        /// gives it a push; it drifts out (a <see cref="CampMark.Boat"/>: CampLife draws it drifting 0.3–0.5 u, turning); the kid walks along the bank
        /// after it. <see cref="CampScene.a"/>: the push.</summary>
        public static CampScene LeafBoat(int kid, Vector2 at, Vector2 bank, Vector2 toWater, float t0)
        {
            toWater = Unit(toWater, Vector2.up);
            var along = new Vector2(-toWater.y, toWater.x);
            var s = new CampScene(CampUse.LeafBoat, t0, bank + toWater * 0.15f) { followReach = 0.4f };
            int m = s.Add(kid);
            float t = WalkTime(at, bank, KidWalk);
            s.Part(m, 0f, t, bank, bank + toWater, DollStyle.Stand, null, PropKind.None, KidWalk);
            s.Part(m, t, t + 1f, bank, bank + toWater, DollStyle.Stand, FiddleHandsPose);
            s.Tip(m, t, t + 1f + 0.6f, CampTipKind.LeafFold);
            t += 1f;
            s.Part(m, t, t + 0.6f, bank, bank + toWater, DollStyle.Stand, (ref DollPose p, float u, float l) => { CrouchPose(ref p, u, l); float k = In(u, 0.1f, 0.3f); p.lean += 15f * k; p.armR = Lerp(p.armR, new Vector3(40f, 0f, 6f), k); });
            t += 0.55f;
            s.a = t;
            var on = bank + toWater * 0.07f; // #692 p5 retakes: on the drawn water (the bank stands 0.035 short of it)
            s.Fx(t, CampFxKind.WaterRing, G(on), 0.5f);
            Leave(s, t, CampMark.Boat, on);
            t += 0.05f;
            s.Part(m, t, t + 0.4f, bank, bank + toWater, DollStyle.Stand, (ref DollPose p, float u, float l) => { CrouchPose(ref p, 1f, l); float k = Bump(u, 0f, l); p.armR = Lerp(p.armR, new Vector3(70f, 0f, 6f), k); });
            t += 0.4f;
            // follow along the bank (slowly, watching it)
            var follow = bank + along * 0.2f - toWater * 0.02f;
            s.Part(m, t, t + 5f, follow, follow + toWater, DollStyle.Stand, null, PropKind.None, 0.04f);
            t += 5f;
            s.Part(m, t, t + 1.2f, follow, follow + toWater, DollStyle.Point);
            return s;
        }

        // ================================================================== B45.c feet in the water
        /// <summary>B45.c: 1–2 grown-ups sit on the edge facing the water, sunk 0.02 u at the front, leaning back −10, a slow bob (0.008 u at 1.5 Hz)
        /// and a ring at the front every 2 s (dolls have no legs: the rings are the feet [est]).</summary>
        public static CampScene FeetInWater(int a, int b, Vector2 aAt, Vector2 bAt, Vector2 bank, Vector2 toWater, float sitFor, float t0)
        {
            toWater = Unit(toWater, Vector2.up);
            var along = new Vector2(-toWater.y, toWater.x);
            var s = new CampScene(CampUse.FeetInWater, t0, bank + toWater * 0.08f) { holdFocus = true };
            int n = b >= 0 ? 2 : 1;
            sitFor = Mathf.Max(6f, sitFor);
            float t = 0f;
            var spots = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                s.Add(i == 0 ? a : b);
                spots[i] = bank + along * ((i - (n - 1) * 0.5f) * 0.09f);
                t = Mathf.Max(t, WalkTime(i == 0 ? aAt : bAt, spots[i], Walk));
            }
            for (int i = 0; i < n; i++) s.Part(i, 0f, t, spots[i], spots[i] + toWater, DollStyle.Stand, null, PropKind.None, Walk);
            s.a = t; // #692 p5 retakes: the sit (AnimLab's lead)
            for (int i = 0; i < n; i++)
            {
                float ph = i * 0.6f;
                s.Part(i, t, t + sitFor, spots[i], spots[i] + toWater, DollStyle.Sit, (ref DollPose p, float u, float l) => { p.offset.y -= 0.02f * In(u, 0f, 0.5f); p.lean -= 10f; p.offset.y += 0.008f * Mathf.Sin((u + ph) * 1.5f * Mathf.PI * 2f); });
                for (float r = 1.2f + ph; r < sitFor; r += 2f) s.Fx(t + r, CampFxKind.WaterRing, G(spots[i] + toWater * 0.07f), 0.5f); // reshoot2: the rings had been under the water (CampLife.FireCamp)
            }
            return s;
        }

        // ================================================================== B46.a look at their reflection
        /// <summary>B46.a: a kid kneels over a puddle (B4's kneel), head down −40, still 1.5 s; pulls a face (head roll ±15, twice) and hops back
        /// laughing; a second kid (−1: none) joins, both lean over, heads 0.05 u apart. <see cref="CampScene.a"/>: the face.</summary>
        public static CampScene PuddleLook(int k1, int k2, Vector2 at1, Vector2 at2, Vector2 puddle, float puddleR, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down);
            var across = new Vector2(-toCam.y, toCam.x);
            var s = new CampScene(CampUse.PuddleLook, t0, puddle) { holdFocus = true };
            int a = s.Add(k1);
            var edge = puddle + toCam * (puddleR + 0.03f);
            float t = WalkTime(at1, edge, KidWalk);
            s.Part(a, 0f, t, edge, puddle, DollStyle.Stand, null, PropKind.None, KidWalk);
            CampPoseFn over = (ref DollPose p, float u, float l) => { KneelPose(ref p, u, l); p.lean += 40f * In(u, 0.2f, 0.3f); };
            s.Part(a, t, t + 1.8f, edge, puddle, DollStyle.Stand, over); t += 1.8f;
            s.a = t;
            s.Part(a, t, t + 1f, edge, puddle, DollStyle.Stand, (ref DollPose p, float u, float l) => { KneelPose(ref p, 1f, l); p.lean += 40f; p.roll += 15f * Mathf.Sin(u / l * Mathf.PI * 4f); }); t += 1f;
            var back = edge + toCam * 0.06f;
            s.Part(a, t, t + 0.5f, back, puddle, DollStyle.HopBack, null, PropKind.None, 0.2f); t += 0.5f;
            s.Part(a, t, t + 1f, back, puddle, DollStyle.Laugh); t += 1f;
            if (k2 >= 0)
            {
                int b = s.Add(k2);
                var e1 = edge + across * 0.025f; var e2 = edge - across * 0.025f + toCam * 0.005f;
                float w = WalkTime(at2, e2, KidWalk);
                s.Part(b, Mathf.Max(0f, t - w), t, e2, puddle, DollStyle.Stand, null, PropKind.None, KidWalk);
                s.Part(a, t, t + 0.4f, e1, puddle, DollStyle.Stand, null, PropKind.None, 0.2f); t += 0.4f;
                s.Part(a, t, t + 2.2f, e1, puddle, DollStyle.Stand, over);
                s.Part(b, t - 0.4f, t + 2.2f, e2, puddle, DollStyle.Stand, over);
                t += 2.2f;
                s.Part(a, t, t + 1f, e1 + toCam * 0.05f, puddle, DollStyle.Laugh, null, PropKind.None, 0.2f);
                s.Part(b, t, t + 1f, e2 + toCam * 0.05f, puddle, DollStyle.Laugh, null, PropKind.None, 0.2f);
            }
            return s;
        }

        // ================================================================== B46.b race to the next one
        public const float RaceSpeed = 0.5f;
        /// <summary>#692 p5 retakes: the gap between the racers' lanes (box u) [est].</summary>
        public const float RaceLane = 0.065f;

        /// <summary>B46.b: 2–4 kids run puddle to puddle (0.5 u/s); the first there stomps (droplets and a ring), the others pile in and stomp;
        /// three puddles in a row. <see cref="CampScene.a"/>: the first stomp.</summary>
        public static CampScene PuddleRace(IList<int> kids, IList<Vector2> at, IList<Vector2> puddles, float t0)
        {
            var s = new CampScene(CampUse.PuddleRace, t0, puddles.Count > 0 ? puddles[0] : (at.Count > 0 ? at[0] : Vector2.zero)) { followReach = 1f };
            int n = kids.Count;
            var pos = new Vector2[n];
            float t = 0f;
            for (int i = 0; i < n; i++) { s.Add(kids[i]); pos[i] = at[i]; }
            s.a = -1f;
            var prev = Vector2.zero; for (int i = 0; i < n; i++) prev += at[i]; prev /= Mathf.Max(1, n);
            for (int q = 0; q < puddles.Count && q < 3; q++)
            {
                var pd = puddles[q];
                float first = float.MaxValue;
                var arrive = new float[n];
                // #692 p5 retakes (the kids clumped into one blob): each kid keeps a lane, side by side across the run, so all of them read
                var runDir = Unit(pd - prev, Vector2.up);
                var lane = new Vector2(-runDir.y, runDir.x);
                prev = pd;
                for (int i = 0; i < n; i++)
                {
                    var spot = pd + lane * ((i - (n - 1) * 0.5f) * RaceLane);
                    float speed = RaceSpeed * (1f - 0.06f * ((i + q) % n));
                    float w = WalkTime(pos[i], spot, speed);
                    Go(s, i, t, w, spot, pd, speed, true);
                    arrive[i] = t + w;
                    first = Mathf.Min(first, arrive[i]);
                    pos[i] = spot;
                }
                float end = 0f;
                for (int i = 0; i < n; i++)
                {
                    s.Part(i, arrive[i], arrive[i] + 0.7f, pos[i], pd, DollStyle.Stomp);
                    s.Fx(arrive[i] + 0.25f, CampFxKind.Droplets, G(pos[i], 0.01f), 0.6f, 5);
                    s.Fx(arrive[i] + 0.25f, CampFxKind.Ring, G(pos[i], 0.005f), 0.6f);
                    if (s.a < 0f) s.a = arrive[i] + 0.25f;
                    end = Mathf.Max(end, arrive[i] + 0.7f);
                }
                for (int i = 0; i < n; i++) if (arrive[i] + 0.7f < end) s.Part(i, arrive[i] + 0.7f, end, pos[i], pd, DollStyle.Laugh);
                t = end;
            }
            for (int i = 0; i < n; i++) s.Part(i, t, t + 1.2f, pos[i], pos[i] + Vector2.down, i % 2 == 0 ? DollStyle.Laugh : DollStyle.Cheer);
            return s;
        }
    }
}
