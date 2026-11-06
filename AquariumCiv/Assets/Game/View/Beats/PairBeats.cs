// #363 B7 Talk and pairs (pure: Mathf, Vector2/3, DollPose and the beat kit only, so Tools/AnimBench compiles it).
// Sheets: docs/design/animation-specs.md B7.a talk, B7.b hand over, B7.c walk together, B7.d hug.
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>Where a chat is for one of its two people at a moment (<see cref="PairBeats.TalkAt"/>).</summary>
    public struct TalkState
    {
        public bool talker;
        /// <summary>Which beat of the talker's turn (0, 1): the open hand comes on the second.</summary>
        public int beat;
        /// <summary>Seconds into this beat, and the beat's length (the pair's own, so neighbouring chats never bob in step).</summary>
        public float x, period;
        /// <summary>Seconds into the closing laugh (negative: not laughing yet).</summary>
        public float laugh;
    }

    /// <summary>
    /// B7: the pair beats as pure functions of time. Talk is a function of (the two keys, the shared clock, the seconds left in the
    /// chat), so both people agree without talking to each other: the listener nods on the talker's beats, the talker turns an open hand,
    /// and the last <see cref="LaughSeconds"/> are a shared laugh. Hand over, walk together and hug are <see cref="Beat"/>s on a
    /// pair's shared clock (<see cref="PairStage"/> owns the clock and the staging).
    /// </summary>
    public static class PairBeats
    {
        // ------------------------------------------------------------------ talk (B7.a)
        /// <summary>A talker beat (today's period).</summary>
        public const float TalkBeat = 1.3f;
        /// <summary>The talker's bob peaks this far through a beat; the listener's nod starts <see cref="NodDelay"/> later.</summary>
        public const float PeakShare = 0.25f, NodDelay = 0.15f, NodSeconds = 0.35f;
        public const float LaughSeconds = 1.2f, LaughLag = 0.12f;
        /// <summary>The talker's bob (spec 0.012; AnimBench: 0.012 did not read, so 0.025) and the nod's depth (lean 4 to 20, squash 0.96).</summary>
        public const float BobHeight = 0.025f, NodLean = 20f, TalkLean = 4f;

        public static int PairSeed(int a, int b) => unchecked((int)BeatHash.Of(Mathf.Min(a, b), Mathf.Max(a, b), 363));

        /// <summary>The pair's chat at the shared clock <paramref name="t"/> (real seconds), for the person with key <paramref name="me"/>.
        /// <paramref name="remaining"/> = real seconds until the chat ends (a big number if unknown).</summary>
        public static TalkState TalkAt(int me, int other, float t, float remaining)
        {
            int seed = PairSeed(me, other);
            float period = TalkBeat * BeatHash.Range(0.92f, 1.08f, seed, 1, 363);
            float u = Mathf.Max(0f, t) + BeatHash.U01(seed, 2, 363) * 20f;
            int bi = Mathf.FloorToInt(u / period);
            int turn = bi / 2;
            bool lower = me < other;
            int speaker = (turn + BeatHash.Pick(2, seed, 3, 363)) & 1;
            return new TalkState
            {
                talker = speaker == (lower ? 0 : 1), beat = bi & 1, x = u - bi * period, period = period,
                laugh = remaining <= LaughSeconds ? LaughSeconds - remaining - (lower ? 0f : LaughLag) : -1f,
            };
        }

        /// <summary>Lays the chat over <paramref name="p"/> (the Talk loop pose): bob + open hand, or nod, or the shared laugh. A turn change
        /// eases over <see cref="RoleBlend"/> s (the arm never snaps). <paramref name="handBusy"/>: the right hand holds something (a flower): no open hand.</summary>
        public static void Talk(ref DollPose p, in TalkState s, float amp, bool handBusy)
        {
            float peakAt = PeakShare * s.period;
            // The talker's side of the pose.
            float w = Mathf.Sin(s.x / s.period * Mathf.PI * 2f);
            float up = Mathf.Max(0f, w);
            float tOff = up * BobHeight * amp, tLean = TalkLean + up * 3f, tRoll = 0f;
            var tArm = new Vector3(22f + up * 26f, 0f, 14f);
            if (s.beat == 1)
            {
                // Every second beat the open hand turns out to the listener (0.5 s): right arm (x 78, z 36), a little roll.
                float e = Ease01(s.x, 0.18f, 0.38f, 0.6f);
                tArm = Vector3.Lerp(tArm, new Vector3(78f, 0f, 36f), e);
                tRoll = 4f * e;
            }
            // The listener's side: nods on the talker's beats (twice on the open-hand beat).
            float nod = Nod(s.x - peakAt - NodDelay);
            if (s.beat == 1) nod = Mathf.Max(nod, Nod(s.x - peakAt - NodDelay - NodSeconds));
            float lLean = Mathf.Lerp(TalkLean, NodLean, nod), lSquash = Mathf.Lerp(1f, 0.96f, nod), lOff = -0.015f * nod;
            var lArm = new Vector3(0f, 0f, 14f);

            float k = RoleAmount(s); // 1: all talker, 0: all listener
            p.offset.y = Mathf.Lerp(lOff, tOff, k);
            p.lean = Mathf.Lerp(lLean, tLean, k);
            p.squash = Mathf.Lerp(lSquash, 1f, k);
            p.roll = tRoll * k;
            if (!handBusy) p.armR = Vector3.Lerp(lArm, tArm, k);

            if (s.laugh >= 0f)
            {
                float l = Easing.Apply(Ease.Smooth, s.laugh / 0.15f);
                float f = Mathf.Sin(s.laugh * Mathf.PI * 2f * 4.5f);
                p.offset.y = Mathf.Lerp(p.offset.y, Mathf.Abs(f) * 0.02f * amp, l);
                p.roll = Mathf.Lerp(p.roll, f * 5f, l);
                p.lean = Mathf.Lerp(p.lean, -8f, l);
                p.squash = Mathf.Lerp(p.squash, 1f, l);
                p.armL = Vector3.Lerp(p.armL, new Vector3(40f, 0f, -30f), l);
                if (!handBusy) p.armR = Vector3.Lerp(p.armR, new Vector3(40f, 0f, 30f), l);
            }
        }

        public const float RoleBlend = 0.3f;

        /// <summary>1 while the person talks, 0 while they listen, easing over <see cref="RoleBlend"/> s after the turn changes.</summary>
        public static float RoleAmount(in TalkState s)
        {
            float into = (s.beat == 1 ? s.period : 0f) + s.x; // seconds into this turn
            float e = Easing.Apply(Ease.Smooth, into / RoleBlend);
            return s.talker ? e : 1f - e;
        }

        /// <summary>One dip of a nod over <see cref="NodSeconds"/>: 0 to 1 to 0 (x is seconds since its start).</summary>
        static float Nod(float x) => x <= 0f || x >= NodSeconds ? 0f : Mathf.Sin(x / NodSeconds * Mathf.PI);

        /// <summary>Up over [0, rise], flat to <paramref name="hold"/>, down by <paramref name="end"/>.</summary>
        static float Ease01(float x, float rise, float hold, float end)
            => Easing.Apply(Ease.Smooth, x / rise) * (1f - Easing.Apply(Ease.Smooth, (x - hold) / Mathf.Max(0.01f, end - hold)));

        // ------------------------------------------------------------------ hand over (B7.b)
        /// <summary>The giver holds the thing out, pushes it a little, the hands meet, a beat's hold, the arms drop with one nod.</summary>
        public static readonly Beat Giver = new Beat(
            Beat.Step(BeatPhase.Antic, 0.35f, Ease.Back, PoseKey.Hold.Lean(10f).Arms(80f, 8f)),
            Beat.Step(BeatPhase.Act, 0.25f, Ease.Out, PoseKey.Hold.Lean(14f).Arms(92f, 6f)),
            Beat.Step(BeatPhase.Contact, 0.08f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.30f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.25f, Ease.Smooth, PoseKey.Hold.Lean(12f).Arms(20f, 12f)),
            Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Base));

        /// <summary>The taker reaches a breath after the giver offers (the beat is played 0.1 s late), meets the hand, lifts the thing to look at it.</summary>
        public static readonly Beat Taker = new Beat(
            Beat.Step(BeatPhase.Approach, 0.10f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Antic, 0.20f, Ease.Out, PoseKey.Hold.Lean(6f).Arms(45f, 10f)),
            Beat.Step(BeatPhase.Act, 0.20f, Ease.Out, PoseKey.Hold.Lean(10f).Arms(82f, 8f)),
            Beat.Step(BeatPhase.Contact, 0.08f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.30f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.25f, Ease.Back, PoseKey.Hold.Lean(6f).Arms(100f, 10f)),
            Beat.Step(BeatPhase.Follow, 0.25f, Ease.Back, PoseKey.Hold.Lean(12f).Arms(60f, 10f)),
            Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Base));

        /// <summary>The taker plays its beat this long after the giver (its own Approach step makes the contacts coincide).</summary>
        public const float TakerLag = 0.10f;
        /// <summary>The contact in the giver's beat (seconds): the thing changes hands here.</summary>
        public static float HandOverContactAt => Giver.ContactAt;
        public static float HandOverSeconds => Giver.Duration;

        // ------------------------------------------------------------------ walk together (B7.c)
        /// <summary>Gap between the two walkers' centres: tunics are 0.11 across, the inner hands meet with the arms near rest.</summary>
        public const float WalkGap = 0.12f;
        public const float TurnCycle = 4f, TurnSeconds = 0.8f, TurnDegrees = 22f, LeanDegrees = 6f; // spec 15 and 4: exaggerated for ~30 px dolls

        /// <summary>Lays "walking together" over a walk pose: the inner hands joined and swinging as one, a lean toward each other, and a turn
        /// to each other in turn (the lead on the first half of a 4 s cycle, the side on the second). <paramref name="lead"/>: the one whose right
        /// side faces the other (the other walks at its right hand).</summary>
        public static void WalkTogether(ref DollPose p, bool lead, float t, float stride, int seed)
        {
            float sw = Mathf.Sin(stride);
            var inner = new Vector3(28f + 8f * sw, 0f, lead ? 11f : -11f);
            if (lead) p.armR = inner; else p.armL = inner;
            p.roll = lead ? -LeanDegrees : LeanDegrees;
            float c = Mathf.Repeat(t + BeatHash.U01(seed, 4, 363) * TurnCycle, TurnCycle) - (lead ? 0f : TurnCycle * 0.5f);
            float k = c >= 0f && c < TurnSeconds ? Mathf.Sin(c / TurnSeconds * Mathf.PI) : 0f;
            p.yaw += (lead ? -1f : 1f) * TurnDegrees * k;
        }

        // ------------------------------------------------------------------ hug (B7.d, behind BeatFlags.Hugs)
        public const float HugArrive = 0.6f, HugGapClose = 0.09f, HugStepBack = 0.05f;
        /// <summary>Visit loop: the hold was 1.2 s and read as "two dolls standing close"; now 1.8 s, a visible sway and a heart (<see cref="HeartDelay"/> into the beat).</summary>
        public const float HugHold = 1.8f, HugSwayDegrees = 10f, HugSwayHz = 0.8f;
        /// <summary>When the heart mark pops over the carer: as the arms close.</summary>
        public static float HeartDelay => Hug.StartOf(BeatPhase.Contact);
        /// <summary>Arms wide while they step in, wrap on arrival with a squeeze, hold (the sway is laid on by <see cref="HugSway"/>), let go.</summary>
        public static readonly Beat Hug = new Beat(
            Beat.Step(BeatPhase.Antic, 0.30f, Ease.Out, PoseKey.Hold.Lean(4f).ArmL(40f, 0f, -140f).ArmR(40f, 0f, 140f)), // visit loop: arms wider on the way in (was 100)
            Beat.Step(BeatPhase.Act, 0.30f, Ease.Smooth, PoseKey.Hold.Lean(10f).Squash(0.92f).ArmL(85f, 0f, 10f).ArmR(85f, 0f, -10f)),
            Beat.Step(BeatPhase.Contact, 0.08f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, HugHold, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.30f, Ease.Smooth, PoseKey.Hold.Lean(2f).Squash(1f).Arms(20f, 14f)),
            Beat.Step(BeatPhase.Settle, 0.20f, Ease.Smooth, PoseKey.Base));

        /// <summary>The sway while they hold (roll ±6° at 1 Hz, opposite signs: they face each other), the lifted partner and the little hop as they let go.</summary>
        public static void HugSway(ref DollPose p, float since, bool second, bool lifted)
        {
            float hold0 = Hug.StartOf(BeatPhase.Contact), hold1 = Hug.StartOf(BeatPhase.Follow);
            if (since > hold0 && since < hold1)
            {
                float k = Easing.Apply(Ease.Smooth, (since - hold0) / 0.2f);
                p.roll = (second ? -1f : 1f) * HugSwayDegrees * Mathf.Sin((since - hold0) * Mathf.PI * 2f * HugSwayHz) * k;
                if (lifted) p.offset.y = 0.02f * k;
            }
            else if (since >= hold1 && since < hold1 + 0.3f)
            {
                float u = (since - hold1) / 0.3f;
                p.offset.y = Mathf.Max(p.offset.y, 0.02f * 4f * u * (1f - u) + (lifted ? 0.02f * (1f - u) : 0f)); // a tiny hop
            }
        }
    }
}
