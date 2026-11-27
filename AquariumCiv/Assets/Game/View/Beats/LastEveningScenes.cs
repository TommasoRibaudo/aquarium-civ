// #789 (docs/design/animation-specs.md B82, spec approved 2026-11-26; the four new clips wait for the user's sign-off before merge): the last
// evening as one camp scene, a pure builder with the beat kit (CampScene parts, a dog's quad parts), as StatueScenes. On the dusk before the
// night an elder dies, they sit at the fire with their people close round them (a hand on the shoulder, a kid against the knee, the dog at
// their feet), look up at the first stars and are walked slowly home by the family. Nothing of the death is shown. New poses:
// HandOnShoulder, LeanOnKnee, HelpUp (the pair) and the pair walk's pacing (PairWalk); the rest is reused. Every number is [est] until the clip.
using System.Collections.Generic;
using UnityEngine;
using static AquariumCiv.View.CampPose;

namespace AquariumCiv.View
{
    /// <summary>#789: who the last evening casts and where things are (CampLife fills it from the box).</summary>
    public sealed class LastEveningCast
    {
        public int elder; public Vector2 elderAt;
        /// <summary>Up to four kin or friends, the partner first (B82.0); each one's spot now and whether they are a kid.</summary>
        public readonly List<int> kin = new(); public readonly List<Vector2> kinAt = new(); public readonly List<bool> kinKid = new();
        /// <summary>The partner's index in <see cref="kin"/> (−1: no partner here; the nearest grown kin takes the hand then).</summary>
        public int partner = -1;
        /// <summary>The elder's own dog, else a grown dog by the fire (−1: none).</summary>
        public int dog = -1; public Vector2 dogAt;
        public Vector2 fire, toCam, door;
        /// <summary>#789 tester (2026-11-27): what stands by the fire that must not come between the family and the camera (the racks' legs),
        /// and the spot at the door the camera sees (the tent's shown side; null: just short of the door, as before).</summary>
        public readonly List<Vector2> clear = new();
        public Vector2? wave;
        /// <summary>The elder's walk as a share of the usual pace (#788's frail walk, about 0.4).</summary>
        public float pace = LastEveningScenes.FrailPace;
    }

    public static class LastEveningScenes
    {
        // ================================================================== numbers (B82.a, all [est])
        /// <summary>The usual walk (CampScenes1.Walk), a kid's, and the elder's frail share of it (#788, "about x0.4 of normal").</summary>
        public const float Walk = 0.22f, KidWalk = 0.3f, FrailPace = 0.4f;
        /// <summary>Up to four kin or friends sit in close (B82.0; LastEveningRules.MaxKin in the sim).</summary>
        public const int MaxKin = 4;
        /// <summary>The elder's seat: this far from the fire's middle. #789 tester (2026-11-27): on the far side of the fire from the camera, so the
        /// elder faces the fire and the player at once (from the camera's side the home view saw their back); turned round by the first of
        /// <see cref="SeatTurns"/> that keeps the racks' legs (<see cref="LastEveningCast.clear"/>) from between the family and the camera.</summary>
        public const float SeatR = 0.28f;
        public static readonly float[] SeatTurns = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f };
        /// <summary>The family round the seat as the camera sees it: this far either side across the view, from this far behind the seat.</summary>
        public const float FamilyHalf = 0.22f, FamilyBehind = 0.2f;
        /// <summary>The kin sit this close round the elder (0.12 to 0.2 u), on the camera's half; the partner right beside them, within an arm's reach
        /// (AnimBench: at 0.16 u the hand fell short of the shoulder). v2 (2026-11-27, the hand did not read): side by side with the elder, both
        /// facing the fire, 0.11 u apart (shoulder to shoulder, not inside each other), so the arm laid across the elder's shoulder shows from the front.</summary>
        public const float RingR = 0.16f, PartnerR = 0.11f;
        /// <summary>The kid at the knee and the dog at the feet: this far in front of the elder (toward the fire); the dog also FeetSide to the elder's other side from the kid.</summary>
        public const float KneeAt = 0.07f, FeetAt = 0.04f, FeetSide = 0.075f;
        /// <summary>Beat 1 lasts at least this long; the kin come 1 to 2 s apart.</summary>
        public const float GatherMin = 8f, ComeEvery = 1.5f;
        /// <summary>Beat 2 (the hand), beat 3 (the stars: look up 3 s, the nod, a hold), beat 4 (rising).</summary>
        public const float HandFor = 8f, LookFor = 3f, NodFor = 1f, StillFor = 3f, KinLookEvery = 0.4f, PatFor = 0.6f, RiseFor = 1.2f;
        /// <summary>The helpers walk this far either side of the elder (v2: 0.1, was 0.07: at 0.07 the three dolls' shoulders were inside each
        /// other and the linked arms vanished); the kid holds the hand a little behind.</summary>
        public const float Beside = 0.1f;
        /// <summary>At the door: the turn, the wave (v2: big and slow, 2.4 s, was a small 1 s one), the step in; the kin stand and look, then go back one by one.</summary>
        public const float TurnFor = 0.8f, WaveFor = 2.4f, InFor = 0.5f, DoorLook = 1.5f;
        /// <summary>v2 targets (2026-11-27, the user: the hand, the helpers' arms, the wave and the look up did not read from the fire's side).
        /// The hand on the shoulder: the arm out sideways at shoulder height, laid across the elder's near shoulder (z 95, x 12 forward).
        /// The help up: the helper's near arm down and out under the elder's arm (z 60, x 15); the elder's arms out over both helpers' shoulders
        /// (z 75). The wave: the arm high over the head (z 150) swung ±25 slowly (0.6 Hz). The stars: the elder leans back 32 (the head
        /// goes back with the body: the doll's head is part of it), the hands back on the seat (x −40); the kin lean back 24.</summary>
        public const float ShoulderArmZ = 95f, ShoulderArmX = 12f, UnderArmZ = 60f, UnderArmX = 15f, OverArmZ = 75f,
            WaveArmZ = 150f, WaveSwing = 25f, WaveHz = 0.6f, ElderLookLean = -32f, KinLookLean = -24f, HandsBackX = -40f;
        /// <summary>A paced walk's step (s): the spot moves on this often (see Track.PacedWalk).</summary>
        public const float WalkStep = 0.3f;

        static Vector2 Unit(Vector2 v, Vector2 fallback) => v.sqrMagnitude > 1e-8f ? v.normalized : fallback;
        static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
        static Vector2 Across(Vector2 d) => new Vector2(-d.y, d.x);
        /// <summary>A walk's length at a pace (no clamp: the elder's slow walk home is long on purpose).</summary>
        public static float WalkTime(Vector2 a, Vector2 b, float speed) => Mathf.Min(30f, (b - a).magnitude / Mathf.Max(0.01f, speed) + 0.25f);

        /// <summary>A scene's members with no gaps: a part that starts after the member's last one ended is preceded by a hold where they were
        /// (a gap would send them back to their plan mid-scene), as StatueScenes' track.</summary>
        sealed class Track
        {
            readonly CampScene s;
            readonly Dictionary<int, float> end = new();
            readonly Dictionary<int, Vector2> pos = new(), face = new();
            readonly Dictionary<int, DollStyle> held = new();
            public Track(CampScene scene) { s = scene; }

            public CampPart Part(int m, float from, float to, Vector2 at, Vector2 faceTo, DollStyle style, CampPoseFn pose = null, float speed = Walk)
            {
                if (end.TryGetValue(m, out float e) && from > e + 1e-3f) s.Part(m, e, from, pos[m], face[m], held[m]);
                var p = s.Part(m, from, to, at, faceTo, style, pose, PropKind.None, speed);
                end[m] = to; pos[m] = at; face[m] = faceTo; held[m] = style == DollStyle.Hidden ? DollStyle.Hidden : style is DollStyle.Sit ? DollStyle.Sit : DollStyle.Stand;
                return p;
            }

            /// <summary>A walk at its own pace: in steps of <see cref="WalkStep"/> s along the line, so the view's catch-up (a part's pace plus 1.5 x
            /// the gap to its spot) never runs the whole way at once (the first clips: the elder's slow walk home was over in a second, then they
            /// stood at the door). The pose keeps one clock over the whole walk.</summary>
            public void PacedWalk(int m, float from, float to, Vector2 a, Vector2 b, Vector2 faceTo, DollStyle style, CampPoseFn pose, float speed)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt((to - from) / WalkStep));
                float len = to - from;
                for (int i = 0; i < n; i++)
                {
                    float t0 = from + len * i / n, t1 = from + len * (i + 1) / n, off = t0 - from;
                    CampPoseFn sub = pose == null ? null : (ref DollPose p, float t, float l) => pose(ref p, t + off, len);
                    var part = Part(m, t0, t1, Vector2.Lerp(a, b, (i + 1f) / n), faceTo, style, sub, speed);
                    part.poseWalking = true;
                }
            }

            public float EndOf(int m) => end.TryGetValue(m, out float e) ? e : 0f;
            public Vector2 PosOf(int m, Vector2 fallback) => pos.TryGetValue(m, out var p) ? p : fallback;
        }

        // ================================================================== the new poses (B82.a "N")
        /// <summary>B82.a beat 2: a hand on the elder's shoulder: the near arm toward them (x 60, z 20 out to their side), eased in 0.5 s and held.
        /// <paramref name="left"/>: the elder sits on this doll's left (the arm on that side reaches; the doll faces its local −z, so that is
        /// <c>armR</c>, as AnimBench's sheets showed).</summary>
        /// <remarks>v2 (2026-11-27): side by side, both facing the fire; the near arm out sideways at shoulder height and laid across the elder's near
        /// shoulder (z <see cref="ShoulderArmZ"/>, x <see cref="ShoulderArmX"/>: a level bar from one doll to the other, seen from the front),
        /// eased in 0.8 s; the body leans in toward the elder (roll 10).</remarks>
        public static CampPoseFn HandOnShoulder(bool left) => (ref DollPose p, float t, float len) =>
        {
            float u = In(t, 0f, 0.8f) * (1f - In(t, len - 0.5f, 0.5f));
            if (left) p.armR = Lerp(p.armR, new Vector3(ShoulderArmX, 0f, ShoulderArmZ), u); else p.armL = Lerp(p.armL, new Vector3(ShoulderArmX, 0f, -ShoulderArmZ), u);
            p.lean += 3f * u; p.roll += (left ? -10f : 10f) * u; // a roll's top goes to local -x: toward armR's side is negative
        };

        /// <summary>B82.a beat 2: a kid sits and leans the head onto the elder's knee: the body tilted 25 degrees toward them (eased in 0.8 s),
        /// a little forward, the near arm across. <paramref name="left"/>: the knee is on the kid's left.</summary>
        public static CampPoseFn LeanOnKnee(bool left) => (ref DollPose p, float t, float len) =>
        {
            float u = In(t, 0f, 0.8f) * (1f - In(t, len - 0.5f, 0.5f));
            p.roll += (left ? -25f : 25f) * u; p.lean += 10f * u;
            if (left) p.armR = Lerp(p.armR, new Vector3(40f, 0f, 30f), u); else p.armL = Lerp(p.armL, new Vector3(40f, 0f, -30f), u);
        };

        /// <summary>B82.a beat 4, the helper's half of HelpUp: standing at the elder's side, facing the same way, the near arm out sideways under their
        /// elbow (x 25 forward, z 55 out toward them: AnimBench showed x 70 alone reached for the fire, not the elder), eased in 0.3 s;
        /// a small lift with the knees as the elder comes up (squash 0.94 to 1).</summary>
        /// <remarks>v2 (2026-11-27, "a helper each side" was lost): the near arm down and out under the elder's arm (z <see cref="UnderArmZ"/>,
        /// x <see cref="UnderArmX"/>), the elder's arm over it (<see cref="HelpedUp"/>), linked, seen from the front.</remarks>
        public static CampPoseFn HelpUp(bool left) => (ref DollPose p, float t, float len) =>
        {
            float u = In(t, 0f, 0.3f);
            if (left) p.armR = Lerp(p.armR, new Vector3(UnderArmX, 0f, UnderArmZ), u); else p.armL = Lerp(p.armL, new Vector3(UnderArmX, 0f, -UnderArmZ), u);
            p.roll += (left ? -5f : 5f) * u; // leaning in to take the weight
            p.lean += 6f * u * (1f - In(t, 0.4f, len - 0.4f)) + 2f; // v2: less (a deep lean showed the top of the head from the front)
            p.squash *= Mathf.Lerp(0.94f, 1f, In(t, 0.3f, len - 0.3f));
        };

        /// <summary>B82.a beat 4, the elder's half of HelpUp: from the seat up to standing over <see cref="RiseFor"/> (the seat's drop and squash ease
        /// out), pushing off the stool with both hands, a small sway (roll ±5) on the way up.</summary>
        // v2 (2026-11-27): as they come up, the arm on each helper's side goes out over that helper's shoulders (z <see cref="OverArmZ"/>,
        // eased in over the rise), linked with the helper's arm under it. <paramref name="overL"/>/<paramref name="overR"/>: a helper on
        // the side of <c>armL</c>/<c>armR</c> (the other arm just eases down from the push).
        public static void HelpedUp(ref DollPose p, float t, float len) => HelpedUpBy(true, true)(ref p, t, len);

        public static CampPoseFn HelpedUpBy(bool overL, bool overR) => (ref DollPose p, float t, float len) =>
        {
            float k = In(t, 0f, len);
            p.offset.y = Mathf.Lerp(-0.03f, 0f, k); p.squash = Mathf.Lerp(0.78f, 1f, k);
            p.lean += Mathf.Lerp(18f, 4f, k);
            p.roll += 5f * Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.3f, len)) * (1f - k);
            var push = Mathf.Max(0f, 1f - 2f * k); float over = In(t, len * 0.25f, len * 0.6f);
            p.armL = Lerp(p.armL, new Vector3(30f, 0f, -24f), push); p.armR = Lerp(p.armR, new Vector3(30f, 0f, 24f), push);
            if (overL) p.armL = Lerp(p.armL, new Vector3(8f, 0f, -OverArmZ), over);
            if (overR) p.armR = Lerp(p.armR, new Vector3(8f, 0f, OverArmZ), over);
        };

        /// <summary>v2: the elder's walk home held up between the helpers: the frail walk with the arm on each helper's side kept over their shoulders.</summary>
        public static CampPoseFn HeldUpWalk(bool overL, bool overR) => (ref DollPose p, float t, float len) =>
        {
            FrailWalk(ref p, t, len);
            if (overL) p.armL = new Vector3(8f, 0f, -OverArmZ);
            if (overR) p.armR = new Vector3(8f, 0f, OverArmZ);
        };

        /// <summary>B82.a beat 5 (the pair walk's pacing): a helper walking at the elder's side at the elder's own slow pace, the near arm under
        /// their elbow (x 30 forward, z 40 out toward them) the whole way and the body turned in a little toward them.</summary>
        /// <remarks>v2 (2026-11-27): the near arm under the elder's (z <see cref="UnderArmZ"/>, x <see cref="UnderArmX"/>), not turned in (the turn hid the
        /// linked arms from the front), leaning in a little.</remarks>
        public static CampPoseFn PairWalk(bool left) => (ref DollPose p, float t, float len) =>
        {
            float u = In(t, 0f, 0.4f);
            if (left) p.armR = Lerp(p.armR, new Vector3(UnderArmX, 0f, UnderArmZ), u); else p.armL = Lerp(p.armL, new Vector3(UnderArmX, 0f, -UnderArmZ), u);
            p.lean += 4f * u; p.roll += (left ? -4f : 4f) * u;
        };

        /// <summary>v2 (2026-11-27, the wave was too small): at the door, facing the fire, the arm goes up high over the head (z
        /// <see cref="WaveArmZ"/>, 0.5 s) and swings slowly ±<see cref="WaveSwing"/> at <see cref="WaveHz"/>, then comes down (0.4 s); a little
        /// lean back and the body swaying with it.</summary>
        public static void BigWave(ref DollPose p, float t, float len)
        {
            float u = In(t, 0f, 0.5f) * (1f - In(t, len - 0.4f, 0.4f));
            float sw = Mathf.Sin((t - 0.5f) * WaveHz * Mathf.PI * 2f) * In(t, 0.4f, 0.3f);
            p.armR = Lerp(p.armR, new Vector3(-6f, 0f, WaveArmZ + WaveSwing * sw), u);
            p.lean += -5f * u; p.roll += -4f * sw * u;
        }

        /// <summary>The elder's frail walk: a little stoop (lean 8) and a short, slow sway.</summary>
        public static void FrailWalk(ref DollPose p, float t, float len) { p.lean += 8f; p.roll += 2f * Mathf.Sin(t * 2.4f); }

        /// <summary>A kid holding the elder's hand on the walk (the near arm out and down toward them: x 20, z 35).</summary>
        public static CampPoseFn HoldHand(bool left) => (ref DollPose p, float t, float len) =>
        {
            float u = In(t, 0f, 0.3f);
            if (left) p.armR = Lerp(p.armR, new Vector3(20f, 0f, 35f), u); else p.armL = Lerp(p.armL, new Vector3(20f, 0f, -35f), u);
        };

        /// <summary>The elder through beats 2 and 3, seated: at <paramref name="handAt"/> the hand on the shoulder's side comes up over it (1 s, a soft
        /// hit when it touches); at <paramref name="kidAt"/> the other hand rests on the kid's head and at <paramref name="strokeAt"/> strokes once
        /// (a slow x 20 wag, 1 s); at <paramref name="lookAt"/> the look up (v2: lean <see cref="ElderLookLean"/>, the hands back; 1 s in, 3 s), the slow nod (x 10, 1 s) after it.</summary>
        public static CampPoseFn ElderAtTheFire(float handAt, bool handLeft, float kidAt, float strokeAt, float lookAt) => (ref DollPose p, float t, float len) =>
        {
            float hand = handAt >= 0f ? In(t, handAt, 1f) * (1f - In(t, len - 0.4f, 0.4f)) : 0f;
            var over = new Vector3(110f, 0f, handLeft ? 34f : -34f);
            if (handLeft) p.armR = Lerp(p.armR, over, hand); else p.armL = Lerp(p.armL, over, hand); // the doll faces its local -z: armR is on its left (AnimBench)
            if (handAt >= 0f && t >= handAt + 0.9f && t < handAt + 1.05f) p.hit = 0.4f; // softly, as the hands meet
            float kid = kidAt >= 0f ? In(t, kidAt, 0.6f) * (1f - In(t, len - 0.4f, 0.4f)) : 0f;
            float stroke = kidAt >= 0f ? 20f * Bump(t, strokeAt, 1f) : 0f;
            var onHead = new Vector3(45f + stroke, 0f, -20f);
            if (handLeft) p.armL = Lerp(p.armL, onHead, kid); else p.armR = Lerp(p.armR, new Vector3(onHead.x, 0f, 20f), kid);
            if (lookAt >= 0f)
            {
                // v2 (2026-11-27, only a slight lean read): the whole doll leans well back (ElderLookLean; the head goes with it), the hands back on the seat
                float look = In(t, lookAt, 1f) * (1f - In(t, lookAt + LookFor, 0.6f));
                p.lean += ElderLookLean * look + 10f * Bump(t, lookAt + LookFor, NodFor);
                p.armL = Lerp(p.armL, new Vector3(HandsBackX, 0f, -18f), look); p.armR = Lerp(p.armR, new Vector3(HandsBackX, 0f, 18f), look);
            }
        };

        /// <summary>A kin's look up at the stars, seated (v2: lean <see cref="KinLookLean"/>, was −12; 1 s in, held for <see cref="LookFor"/>), over another pose;
        /// the free hands go back on the ground (a hand on the elder's shoulder stays).</summary>
        public static CampPoseFn LookUpAt(float at, CampPoseFn over = null) => (ref DollPose p, float t, float len) =>
        {
            var arms0L = p.armL; var arms0R = p.armR;
            over?.Invoke(ref p, t, len);
            float look = In(t, at, 1f) * (1f - In(t, at + LookFor, 0.6f));
            p.lean += KinLookLean * look;
            if (p.armL == arms0L) p.armL = Lerp(p.armL, new Vector3(HandsBackX * 0.75f, 0f, -18f), look);
            if (p.armR == arms0R) p.armR = Lerp(p.armR, new Vector3(HandsBackX * 0.75f, 0f, 18f), look);
        };

        /// <summary>The elder's pat for the dog before rising: the near hand down to it (x 35, 0.6 s bump).</summary>
        public static void PatDog(ref DollPose p, float t, float len) { float u = Bump(t, 0f, len); p.armR = Lerp(p.armR, new Vector3(35f, 0f, 10f), u); p.lean += 14f * u; }

        /// <summary>AnimBench and AnimLab: a pose by name (the clips' <c>"pose"</c>), null when unknown.</summary>
        public static CampPoseFn PoseOf(string name) => name switch
        {
            "HandOnShoulderL" => HandOnShoulder(true), "HandOnShoulderR" => HandOnShoulder(false),
            "LeanOnKneeL" => LeanOnKnee(true), "LeanOnKneeR" => LeanOnKnee(false),
            "HelpUpL" => HelpUp(true), "HelpUpR" => HelpUp(false), "HelpedUp" => HelpedUp,
            "PairWalkL" => PairWalk(true), "PairWalkR" => PairWalk(false), "FrailWalk" => FrailWalk,
            "HoldHandL" => HoldHand(true), "HoldHandR" => HoldHand(false), "PatDog" => PatDog,
            "BigWave" => BigWave, "HeldUpWalk" => HeldUpWalk(true, true),
            "ElderAtTheFire" => ElderAtTheFire(1f, true, 2.5f, 4.5f, 7f), // the clips: the partner on the elder's left
            _ => null,
        };

        // ================================================================== B82.a the scene
        /// <summary>The elder's seat at the fire (box x, z): on the far side from the camera (see <see cref="SeatR"/>).</summary>
        public static Vector2 SeatOf(Vector2 fire, Vector2 toCam, IReadOnlyList<Vector2> clear = null)
        {
            var tc = Unit(toCam, Vector2.down);
            Vector2 best = fire - tc * SeatR; int bestHid = int.MaxValue;
            foreach (float a in SeatTurns)
            {
                var s = fire - Rot(tc, a) * SeatR;
                int hid = Hidden(s, tc, clear);
                if (hid < bestHid) { best = s; bestHid = hid; }
                if (hid == 0) break;
            }
            return best;
        }

        /// <summary>How many of <paramref name="clear"/> stand among the family round <paramref name="seat"/> or between them and the camera.</summary>
        public static int Hidden(Vector2 seat, Vector2 toCam, IReadOnlyList<Vector2> clear)
        {
            if (clear == null) return 0;
            var tc = Unit(toCam, Vector2.down); var ac = Across(tc); int n = 0;
            foreach (var p in clear) { var d = p - seat; if (Vector2.Dot(d, tc) > -FamilyBehind && Mathf.Abs(Vector2.Dot(d, ac)) < FamilyHalf) n++; }
            return n;
        }

        /// <summary>
        /// B82.a (about 30 s and the walk home): the elder sits at the fire (walking there slowly if they aren't), the kin come one by one and sit in
        /// close round them on the far side of the fire, facing the camera (the partner beside them; the tester 2026-11-27), the dog lies across their feet; the partner's hand on the shoulder,
        /// the elder's hand over it, a kid's head on the knee and one stroke; all look up at the stars (the caption at <see cref="CampScene.b"/>),
        /// a hold; the elder pats the dog, the partner and one grown kin help them up, and they walk them home at the elder's own slow pace, the kid
        /// holding a hand, the dog a step behind; at the door the elder turns, a small wave, and goes in (<see cref="CampScene.c"/>); the kin stand
        /// and look, then walk back to the fire one by one and sit. <see cref="CampScene.a"/>: beat 2's start; <see cref="CampScene.d"/>: the walk home's start.
        /// </summary>
        public static CampScene Evening(LastEveningCast c, float t0)
        {
            var toCam = Unit(c.toCam, Vector2.down);
            var seat = SeatOf(c.fire, toCam, c.clear);
            // the tester 2026-11-27: they face between the fire and the camera (the fire itself when the seat is straight beyond it), so a seat turned
            // round the fire away from a rack still shows their fronts; everything "toward the fire" below is this way
            var toFire = Unit(Unit(c.fire - seat, toCam) + toCam, toCam);
            var faceAt = seat + toFire;
            var side = Across(toFire); // the elder's left, as they face the fire
            var s = new CampScene(CampUse.LastEvening, t0, seat) { holdFocus = false, followReach = 0.9f, dog = c.dog };
            var T = new Track(s);
            int me = s.Add(c.elder);
            float frail = Walk * Mathf.Clamp(c.pace, 0.2f, 1f);
            int n = Mathf.Min(c.kin.Count, MaxKin);

            // who does what: the hand on the shoulder (the partner, else the first grown kin), the kid at the knee (only with a grown kin there),
            // the second helper up (another grown kin)
            int helper = c.partner >= 0 && c.partner < n ? c.partner : -1;
            if (helper < 0) for (int i = 0; i < n; i++) if (!c.kinKid[i]) { helper = i; break; }
            int kneeKid = -1;
            if (helper >= 0) for (int i = 0; i < n; i++) if (c.kinKid[i]) { kneeKid = i; break; }
            int second = -1;
            for (int i = 0; i < n; i++) if (i != helper && !c.kinKid[i]) { second = i; break; }

            // 1 gather: the elder to the seat (slowly), the kin one by one into the half ring on the camera's side, the dog to the feet
            float wE = (c.elderAt - seat).magnitude < 0.03f ? 0.2f : WalkTime(c.elderAt, seat, frail);
            var walkIn = T.Part(me, 0f, wE, seat, c.fire, DollStyle.Stand, FrailWalk, frail); walkIn.poseWalking = true;
            var ms = new int[n]; var spots = new Vector2[n];
            var ring = new[] { -90f, -50f, 50f }; int slot = 0; // round from the way they face (the fire, the camera): the elder's other side from the partner first, then the two in front at the sides (the tester 2026-11-27: none behind them, hidden by the elder)
            float gather = Mathf.Max(GatherMin, wE + 0.5f);
            for (int i = 0; i < n; i++)
            {
                ms[i] = s.Add(c.kin[i]);
                Vector2 at = i == kneeKid ? seat + toFire * KneeAt + side * 0.025f
                    : i == helper ? seat + side * PartnerR
                    : seat + Rot(toFire, ring[Mathf.Min(slot++, ring.Length - 1)]) * RingR;
                spots[i] = at;
                float start = 0.8f + i * ComeEvery;
                float spd = c.kinKid[i] ? KidWalk : Walk;
                float w = WalkTime(c.kinAt[i], at, spd);
                T.Part(ms[i], 0f, start, c.kinAt[i], seat, DollStyle.Stand);
                T.Part(ms[i], start, start + w, at, seat, DollStyle.Stand, null, spd);
                gather = Mathf.Max(gather, start + w + 0.5f);
            }
            float tHand = gather, tStars = tHand + HandFor, tRise = tStars + LookFor + NodFor + StillFor;
            s.a = tHand; s.b = tStars;
            bool dog = c.dog >= 0;
            float tUp = tRise + (dog ? PatFor : 0f);

            // the elder, seated through beats 1 to 3: the hand over the shoulder's, the kid's head, the look up and the nod
            bool handLeft = helper >= 0 && Vector2.Dot(spots[helper] - seat, side) > 0f;
            T.Part(me, wE, tRise, seat, faceAt, DollStyle.Sit,
                ElderAtTheFire(helper >= 0 ? tHand + 1f - wE : -1f, handLeft, kneeKid >= 0 ? tHand + 1.5f - wE : -1f, tHand + 4f - wE, tStars - wE));
            if (dog) T.Part(me, tRise, tUp, seat, faceAt, DollStyle.Sit, PatDog);

            // the kin: sit in close facing the elder; the hand, the knee, the look up one after another
            for (int i = 0; i < n; i++)
            {
                float arrive = T.EndOf(ms[i]);
                float look = tStars + KinLookEvery * (i + 1);
                // the partner sits turned to the elder, the hand reaching straight to the shoulder; the kid at the knee faces the fire, the knee beside them
                var face = i == kneeKid || i == helper ? spots[i] + toFire : Vector2.Lerp(seat, c.fire, 0.5f); // v2: the partner side by side with the elder, both facing the fire; the tester 2026-11-27: the others turned in toward the elder from facing the fire, so their fronts show too
                CampPoseFn over = i == helper ? Window(HandOnShoulder(LeftOf(spots[i], face, seat)), tHand - arrive, tStars + LookFor - arrive)
                    : i == kneeKid ? Window(LeanOnKnee(LeftOf(spots[i], face, seat)), tHand + 1.5f - arrive, tRise - arrive) : null;
                T.Part(ms[i], arrive, tRise, spots[i], face, DollStyle.Sit, LookUpAt(look - arrive, over));
            }

            // the dog: to the feet, lies across them, two thumps of the tail (1.5 Hz) in beat 2
            // v2 (the first in-box clip: the dog lay across the kid at the knee, the two inside each other): at the feet on the elder's other
            // side from the kid, lying with its head toward the fire
            var feet = seat + toFire * FeetAt - side * FeetSide;
            var lieFace = feet + toFire;
            if (dog)
            {
                float dw = WalkTime(c.dogAt, feet, Walk);
                float d0 = Mathf.Clamp(gather - dw - 0.2f, 0f, 2f);
                s.Quad(0, 0f, d0, c.dogAt, seat, QuadAct.Lie);
                s.Quad(0, d0, d0 + dw, feet, lieFace, QuadAct.Stand, Walk);
                s.Quad(0, d0 + dw, tHand + 3f, feet, lieFace, QuadAct.Lie);
                s.Quad(0, tHand + 3f, tHand + 3f + 1.33f, feet, lieFace, QuadAct.Wag);
                s.Quad(0, tHand + 3f + 1.33f, tUp, feet, lieFace, QuadAct.Lie);
            }

            // 4 rising: the two helpers stand, step to the elder's sides (facing the fire, as the elder) and help them up
            var helpers = new List<int>();
            if (helper >= 0) helpers.Add(helper);
            if (second >= 0) helpers.Add(second);
            var stand = new Vector2[helpers.Count];
            float tStood = tUp + 0.6f + RiseFor;
            for (int h = 0; h < helpers.Count; h++)
            {
                int i = helpers[h];
                var at = seat + (h == 0 ? side : -side) * (Beside + 0.01f);
                stand[h] = at;
                T.Part(ms[i], tRise, tUp, spots[i], seat, DollStyle.Stand);
                T.Part(ms[i], tUp, tUp + 0.6f, at, at + toFire, DollStyle.Stand, null, Walk);
                T.Part(ms[i], tUp + 0.6f, tStood, at, at + toFire, DollStyle.Stand, HelpUp(LeftOf(at, at + toFire, seat)));
            }
            T.Part(me, tUp, tUp + 0.6f, seat, faceAt, DollStyle.Sit);
            // v2: the elder's arm on each helper's side goes out over that helper's shoulders (armR is the doll's left: LeftOf)
            bool upR = false, upL = false;
            foreach (var at in stand) { if (LeftOf(seat, faceAt, at)) upR = true; else upL = true; }
            T.Part(me, tUp + 0.6f, tStood, seat, faceAt, DollStyle.Stand, HelpedUpBy(upL, upR));

            // 5 home: at the elder's own slow pace, a helper each side, the kid at a hand a little behind, the dog a step behind
            // the tester 2026-11-27: the wave on the side of the tent the camera sees (c.wave), not behind the tent; else just short of the door
            var dir = Unit((c.wave ?? c.door) - seat, toCam);
            var across = Across(dir);
            var atDoor = c.wave ?? c.door - dir * 0.05f;
            var waveFace = atDoor + Unit(Unit(c.fire - atDoor, toCam) + toCam * 1.3f, toCam); // to the fire, turned well round to the camera (a fire behind the tent: about 40 degrees off it)
            float walk = WalkTime(seat, atDoor, frail);
            s.d = tStood;
            float tDoor = tStood + walk;
            bool walkR = false, walkL = false;
            for (int h = 0; h < helpers.Count; h++) { var end = atDoor + (Vector2.Dot(stand[h] - seat, across) >= 0f ? across : -across) * Beside; if (LeftOf(atDoor - dir, atDoor, end)) walkR = true; else walkL = true; }
            T.PacedWalk(me, tStood, tDoor, seat, atDoor, c.door, DollStyle.Stand, helpers.Count > 0 ? HeldUpWalk(walkL, walkR) : FrailWalk, frail);
            for (int h = 0; h < helpers.Count; h++)
            {
                int i = helpers[h];
                var end = atDoor + (Vector2.Dot(stand[h] - seat, across) >= 0f ? across : -across) * Beside;
                var st0 = seat + (end - atDoor); // v2: abreast of the elder from the first step (from the help-up spot it was a diagonal the whole walk)
                T.PacedWalk(ms[i], tStood, tDoor, st0, end, end + dir, DollStyle.Stand, PairWalk(LeftOf(end, end + dir, atDoor)), (end - st0).magnitude / Mathf.Max(0.1f, walk - 0.25f));
            }
            if (kneeKid >= 0)
            {
                int i = kneeKid;
                var kidSide = helpers.Count > 1 ? -dir * 0.09f - across * (Beside + 0.04f) : -across * Beside; // v2: clear of the wider helpers
                var near = helpers.Count > 1 ? seat - toFire * 0.1f - side * 0.05f : seat - side * (Beside + 0.04f) - toFire * 0.03f; // v2: behind, clear of the helper there
                T.Part(ms[i], tRise, tStood, near, seat, DollStyle.Stand, null, KidWalk);
                var end = atDoor + kidSide;
                var k0 = seat + kidSide; // v2: a step behind from the start (from the rise spot the kid walked ahead of them)
                T.PacedWalk(ms[i], tStood, tDoor, k0, end, end + dir, DollStyle.Stand, HoldHand(LeftOf(end, end + dir, atDoor)), (end - k0).magnitude / Mathf.Max(0.1f, walk - 0.25f));
            }
            if (dog)
            {
                var behind = atDoor - dir * 0.1f - across * 0.03f;
                s.Quad(0, tUp, tStood, feet, seat, QuadAct.Stand);
                s.Quad(0, tStood, tDoor, behind, c.door, QuadAct.Stand, (behind - feet).magnitude / Mathf.Max(0.1f, walk - 0.25f));
            }
            // at the door: the turn back to the fire, a small wave, in
            float tTurn = tDoor + TurnFor, tWave = tTurn + WaveFor, tIn = tWave + InFor;
            T.Part(me, tDoor, tTurn, atDoor, waveFace, DollStyle.Stand);
            T.Part(me, tTurn, tWave, atDoor, waveFace, DollStyle.Stand, BigWave); // v2: big and slow (was WaveSmall)
            T.Part(me, tWave, tIn, c.door, c.door + Unit(c.door - atDoor, dir), DollStyle.Stand, null, Walk * 0.5f);
            T.Part(me, tIn, tIn + 0.3f, c.door, c.door + dir, DollStyle.Hidden);
            s.c = tIn;
            // the others stay by the fire, standing to see them off; those at the door stand and look (1.5 s), then come back one by one and sit
            float back = tIn + DoorLook;
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                bool walked = helpers.Contains(i) || i == kneeKid;
                var to = spots[i] + Unit(spots[i] - seat, -toFire) * 0.04f;
                if (!walked)
                {
                    T.Part(ms[i], tRise, tIn, spots[i], c.door, DollStyle.Stand);
                    float sitAt = back + 0.6f * k++;
                    T.Part(ms[i], sitAt, sitAt + 1.2f, to, c.fire, DollStyle.Sit, null, Walk);
                    continue;
                }
                // v2: at the door they step out to the sides (0.1 u, a little back), so the elder turns and waves alone, not inside their huddle
                var end0 = T.PosOf(ms[i], spots[i]);
                var from = end0 + Unit(end0 - atDoor, -across) * 0.1f - dir * 0.03f;
                T.Part(ms[i], T.EndOf(ms[i]), back, from, c.door, DollStyle.Look);
                float go = back + 0.6f * k++;
                float spd = c.kinKid[i] ? KidWalk : Walk;
                float w = WalkTime(from, to, spd);
                T.Part(ms[i], go, go + w, to, c.fire, DollStyle.Stand, null, spd);
                T.Part(ms[i], go + w, go + w + 1.2f, to, c.fire, DollStyle.Sit);
            }
            if (dog) s.Quad(0, tDoor, Mathf.Max(tIn + 0.3f, s.Length), c.door - dir * 0.08f + across * 0.05f, c.door, QuadAct.Lie);
            return s;
        }

        /// <summary>The point <paramref name="target"/> is on the left of a doll at <paramref name="at"/> facing <paramref name="face"/> (box x, z; Unity's
        /// forward +z has its right at +x).</summary>
        public static bool LeftOf(Vector2 at, Vector2 face, Vector2 target)
        {
            var f = Unit(face - at, Vector2.up);
            var right = new Vector2(f.y, -f.x);
            return Vector2.Dot(target - at, right) < 0f;
        }


        /// <summary>A pose that plays only from <paramref name="a"/> to <paramref name="b"/> seconds into the part (its own clock from <paramref name="a"/>).</summary>
        static CampPoseFn Window(CampPoseFn f, float a, float b) => (ref DollPose p, float t, float len) =>
        {
            if (t < a || t >= b) return;
            f(ref p, t - a, b - a);
        };
    }
}
