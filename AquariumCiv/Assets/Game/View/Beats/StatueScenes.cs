// #776 (docs/design/animation-specs.md B81, approved 2026-11-26): the founder's statue as camp scenes, pure builders with the beat kit (CampScene
// parts, effects, tips), as StoryScenes. B81.a the raising (the one held Story scene): two roll a block onto the plinth, one carves it in three
// rounds (the block, the shoulders, the head, the carved figure), the family stand in a half ring on the camera's side and the youngest lays the first
// flowers. B81.b-f the people's uses: a kid climbs the base, the anniversary flowers, an old one sits at its foot, a nod in passing, the sweep.
// Every number is [est] until the clip (AnimLab review).
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;
using static AquariumCiv.View.CampScenes5;

namespace AquariumCiv.View
{
    public static partial class StatueScenes
    {
        const int Nobody = VillageScenes.Nobody;
        static Vector2 Across(Vector2 toCam) => new Vector2(-toCam.y, toCam.x);

        // ================================================================== shared geometry
        /// <summary>B81.1: the plinth's steps (box units): each step's height, the lower step's half width, the figure's feet's height.</summary>
        public const float StepH = PegDoll.StatueStepH, LowHalf = PegDoll.StatueLowW * 0.5f, FeetY = PegDoll.StatueFeetY;
        /// <summary>The carver's work spot and the kneelers' spot: this far from the plinth's centre toward the camera (just off the lower step).</summary>
        public const float WorkAt = 0.15f, KneelAt = 0.13f;
        /// <summary>B81.a: the roll's pace, how far behind the block the rollers push, how long they hurry to reach it.</summary>
        public const float RollSpeed = 0.3f, Behind = 0.09f, ApproachFor = 3.5f;
        /// <summary>B81.a: three rounds of three strokes; a stroke's length and the gap between rounds.</summary>
        public const int Rounds = 3, Strokes = 3;
        public const float StrokeEvery = 0.55f, RoundGap = 0.5f, RoundFor = Strokes * StrokeEvery + 0.2f;
        /// <summary>The family's half ring on the camera's side (the carver's work spot is 0.15 u out, so 0.3 u back is 0.45), the watchers' beyond.</summary>
        public const float FamilyR = 0.45f, KidR = 0.38f, WatchR = 0.7f;
        /// <summary>B81.b: how high each step lifts a kid (the lower step, then both), and how close the watching grown-up stands (within 0.6 u).</summary>
        public const float Lift1 = StepH, Lift2 = 2f * StepH, GrownNear = 0.4f;

        public static Vector2 Front(Vector2 spot, Vector2 toCam, float d) => spot + Unit(toCam, Vector2.down) * d;

        /// <summary>Where the block is while it is rolled in (B81.a): from <see cref="CampScene.p0"/> to the spot over the roll
        /// (<see cref="CampScene.p1"/>.x to <see cref="CampScene.a"/>), at the ground; at the seat it hops up onto the plinth (0.25 s) and stays.
        /// False before the roll starts (the block is not there yet) or after the hop.</summary>
        public static bool BlockNow(CampScene s, float u, Vector2 spot, out Vector2 at, out float height)
        {
            at = spot; height = FeetY;
            float r0 = s.p1.x, r1 = s.a;
            if (u < 0f || u >= r1 + 0.25f) return false;
            if (u < r0) { at = s.p0; height = 0f; return true; }
            if (u < r1) { at = Vector2.Lerp(s.p0, spot, Smooth((u - r0) / Mathf.Max(1e-3f, r1 - r0))); height = 0f; return true; }
            height = Mathf.Lerp(0f, FeetY, Smooth((u - r1) / 0.25f)) + 0.012f * Bump(u, r1, 0.25f);
            return true;
        }

        // ================================================================== poses
        static void Push(ref DollPose p, float t, float len)
        {
            float shove = Bump(Mathf.Repeat(t, 0.8f), 0f, 0.8f);
            p.lean += 25f + 5f * shove; p.squash = 0.9f; p.armL = new Vector3(90f - 10f * shove, 0f, -6f); p.armR = new Vector3(90f - 10f * shove, 0f, 6f);
        }

        static void Brace(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.3f);
            p.lean += 15f * u; p.armL = Lerp(p.armL, new Vector3(90f, 0f, -6f), u); p.armR = Lerp(p.armR, new Vector3(90f, 0f, 6f), u);
        }

        /// <summary>The chisel's round: the right arm at x 70 swinging down to x 52 with each of three strokes, the left steadying.</summary>
        static void Chisel(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f) * (1f - In(t, len - 0.2f, 0.2f));
            float w = Mathf.Max(0f, t - 0.15f); int n = Mathf.FloorToInt(w / StrokeEvery);
            float stroke = n < Strokes ? Bump(w - n * StrokeEvery, 0f, StrokeEvery) : 0f;
            p.armR = Lerp(p.armR, new Vector3(70f - 18f * stroke, 0f, 6f), u); p.armL = Lerp(p.armL, new Vector3(50f, 0f, -14f), u); p.lean += 8f * u;
        }

        /// <summary>Wiping the hands on each other (both arms x 80, the rub ±8 deg at 3 Hz).</summary>
        static void Wipe(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.2f) * (1f - In(t, len - 0.2f, 0.2f)), r = Mathf.Sin(t * 3f * Mathf.PI * 2f) * 8f;
            p.armL = Lerp(p.armL, new Vector3(80f + r, 0f, 14f), u); p.armR = Lerp(p.armR, new Vector3(80f - r, 0f, -14f), u);
        }

        /// <summary>A slow nod (lean 10 over the part).</summary>
        static void SlowNod(ref DollPose p, float t, float len) { p.lean += 10f * Bump(t, 0f, len); }

        /// <summary>The head bowed (lean 12 in over 0.4 s, held).</summary>
        static void BowHead(ref DollPose p, float t, float len) { p.lean += 12f * In(t, 0f, 0.4f); }

        /// <summary>A hand laid on the plinth (the right arm out to x 70, in and out over 0.25 s).</summary>
        static void HandOn(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.25f) * (1f - In(t, len - 0.25f, 0.25f));
            p.armR = Lerp(p.armR, new Vector3(70f, 0f, 4f), u); p.lean += 6f * u;
        }

        /// <summary>The hand on the figure's foot, the head down (kneeling: the right arm x 50).</summary>
        static void HandOnFoot(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.3f) * (1f - In(t, len - 0.3f, 0.3f));
            KneelPose(ref p, t, len); p.armR = Lerp(p.armR, new Vector3(50f, 0f, 4f), u); p.lean += 6f * u;
        }

        /// <summary>A rise to <paramref name="to"/> (box units, not scaled) over <paramref name="over"/> s from <paramref name="from"/>, with a wiggle
        /// roll of ±10 deg at 6 Hz while pulling (B81.b's scramble).</summary>
        static CampPoseFn Rise(float from, float to, float over, bool wiggle) => (ref DollPose p, float t, float len) =>
        {
            float k = In(t, 0f, over);
            p.lift = Mathf.Lerp(from, to, k);
            if (wiggle) p.roll += 10f * Mathf.Sin(t * 6f * Mathf.PI * 2f) * Bump(t, 0f, over);
            if (wiggle) { p.armL = Lerp(p.armL, new Vector3(165f, 0f, -12f), 1f - k); p.armR = Lerp(p.armR, new Vector3(165f, 0f, 12f), 1f - k); }
        };

        /// <summary>Held at a height with a pose over it.</summary>
        static CampPoseFn Held(float lift, CampPoseFn over = null) => (ref DollPose p, float t, float len) => { over?.Invoke(ref p, t, len); p.lift = lift; };

        /// <summary>One arm round the figure's leg (the arm x 90 toward it) at a height.</summary>
        static CampPoseFn Hug(float lift) => (ref DollPose p, float t, float len) => { p.lift = lift; p.armR = Lerp(p.armR, new Vector3(90f, 0f, 8f), In(t, 0f, 0.25f)); p.lean += 4f; };

        /// <summary>The hop down: from <paramref name="from"/> the height falls over 0.3 s, the landing squashes (0.8 for 0.15 s).</summary>
        static CampPoseFn HopDown(float from) => (ref DollPose p, float t, float len) =>
        {
            p.lift = Mathf.Lerp(from, 0f, In(t, 0f, 0.3f));
            p.squash = Mathf.Lerp(1f, 0.8f, Bump(t, 0.3f, 0.3f));
        };

        // ================================================================== B81.e a nod in passing
        /// <summary>
        /// B81.e (about 1.5 s, never longer): a grown-up whose walk crosses the green within 0.5 u slows to a stop (0.6 s), turns the head to the statue,
        /// a small nod; one who <paramref name="knew"/> them also touches the plinth in passing (the hand out, x 70) and goes on. Kids who never knew
        /// them just run past: they have the climb.
        /// </summary>
        public static CampScene Nod(int who, Vector2 whoAt, bool knew, Vector2 spot, Vector2 toCam, float t0)
        {
            var s = new CampScene(CampUse.StatueNod, t0, spot) { holdFocus = false };
            int m = s.Add(who);
            s.Part(m, 0f, 0.6f, whoAt, spot, DollStyle.Stand);
            s.Part(m, 0.6f, 1.1f, whoAt, spot, DollStyle.Stand, (ref DollPose p, float u, float len) => { p.lean += 15f * Bump(u, 0f, len); });
            if (knew)
            {
                var toward = whoAt + Unit(spot - whoAt, Vector2.up) * 0.03f;
                s.Part(m, 1.1f, 1.5f, toward, spot, DollStyle.Stand, HandOn, PropKind.None, 0.2f);
            }
            else s.Part(m, 1.1f, 1.4f, whoAt, spot, DollStyle.Stand);
            return s;
        }

        // ================================================================== B81.f the green's sweep (work)
        /// <summary>
        /// B81.f (about 9 s, work): the weekly clean of the green: the sweeper walks with a broom to the plinth's feet and brushes them last (two strokes,
        /// 3 s), leaving it clear.
        /// </summary>
        public static CampScene Sweep(int who, Vector2 whoAt, Vector2 spot, Vector2 toCam, float t0)
        {
            toCam = Unit(toCam, Vector2.down); var acr = Across(toCam);
            var s = new CampScene(CampUse.StatueSweep, t0, spot) { holdFocus = true };
            int m = s.Add(who);
            var at = Front(spot, toCam, KneelAt + 0.03f) + acr * 0.04f;
            float w = WalkTime(whoAt, at, Walk);
            s.Part(m, 0f, w, at, spot, DollStyle.Stand, null, PropKind.Broom, Walk);
            s.Part(m, w, w + 3.0f, at, spot, DollStyle.Sweep, null, PropKind.Broom);
            for (int i = 0; i < 2; i++) s.Fx(w + 0.75f + i * 1.5f, CampFxKind.Dust, G(at - toCam * 0.04f, 0.005f), 0.2f, 1);
            var away = at + toCam * 0.3f;
            s.Part(m, w + 3.0f, w + 3.0f + 1.0f, away, away + toCam, DollStyle.Stand, null, PropKind.Broom, Walk);
            return s;
        }
    }
}
