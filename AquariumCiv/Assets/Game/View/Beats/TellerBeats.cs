// B76 (animation-specs "The teller's manner at the fire", W717): the Keeper's pick tells tonight's story, slow with all the voices or
// short for a laugh. The teller's gestures (DollStyle.Tell) and the listeners' (the kids copy the wind, scoot in, flinch, laugh; the
// grown-ups nod) as pure functions of the seconds since the telling began. Pure: Mathf, Vector3 and DollPose (AnimBench compiles it).
// The unpicked evening teller keeps DollStyle.Speech (approvals row 229). Every number is [est] until AnimLab.
using UnityEngine;

namespace AquariumCiv.View
{
    public static class TellerBeats
    {
        /// <summary>The picked teller is telling now: the beats say the teller's lines (ShowMeDirector leaves the random ones out).</summary>
        public static bool SpeaksItsOwnLines;

        /// <summary>B76.a's beats (s from the start, on a 23 s telling; a longer or shorter evening scales them): the hush, the two voices, the
        /// wind, the voices again, the scary part (the jump at <see cref="SlowJump"/>), the end beat.</summary>
        public const float SlowLength = 23f, SlowVoices = 2f, SlowWind = 8f, SlowVoices2 = 12f, SlowScary = 15f, SlowJump = 18.6f, SlowEnd = 20f;
        /// <summary>B76.b's beats (s, on a 9 s telling, then the laugh): the look round, the quick telling, the woodpile mime, the end.</summary>
        public const float ShortLength = 9f, ShortQuick = 1f, ShortWood = 5f, ShortEndAt = 7f, LaughLength = 2.5f;
        /// <summary>B76.a: the voices' height (squash 1.08 big, 0.85 small; the spec's fallback if it doesn't read: 1.12 / 0.8).</summary>
        public static float BigSquash = 1.08f, SmallSquash = 0.85f;

        static float In(float t, float from, float len) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - from) / Mathf.Max(1e-4f, len)));
        static float Bump(float t, float from, float len) => t < from || t > from + len ? 0f : Mathf.Sin((t - from) / len * Mathf.PI);
        static Vector3 L(Vector3 a, Vector3 b, float k) => Vector3.LerpUnclamped(a, b, k);

        /// <summary>The line the teller says between <paramref name="from"/> and <paramref name="to"/> seconds (its index in
        /// <c>PeopleVoice.TellerLines</c>), else -1. Slow: the wind (0), the scary part (1). Short: the woodpile (0, only where it stands), the
        /// mock-serious end (the last).</summary>
        public static int LineBetween(bool slow, bool woodpile, float from, float to)
        {
            bool Hit(float at) => from < at && at <= to;
            if (slow) return Hit(SlowWind) ? 0 : Hit(SlowScary) ? 1 : -1;
            if (woodpile && Hit(ShortWood + 0.6f)) return 0;
            return Hit(ShortLength + 0.6f) ? (woodpile ? 1 : 0) : -1;
        }

        // ================================================================== B76.a slow, with all the voices

        /// <summary>The teller at <paramref name="s"/> seconds into the slow telling (the beats' clock, already scaled to 23 s).</summary>
        public static void Slow(ref DollPose p, float s)
        {
            p.armL = new Vector3(20f, 0f, -16f); p.armR = new Vector3(20f, 0f, 16f);
            if (s < SlowVoices)
            {   // 1 the hush: both hands up, palms out, and wait
                float k = In(s, 0f, 0.6f);
                p.armL = L(p.armL, new Vector3(120f, 0f, -30f), k); p.armR = L(p.armR, new Vector3(120f, 0f, 30f), k);
                return;
            }
            if (s < SlowWind) { Voices(ref p, s - SlowVoices); return; }
            if (s < SlowVoices2)
            {   // 3 the wind: stop, wag a finger, then sweep both arms over the head with a sway
                float w = s - SlowWind;
                if (w < 0.4f) { p.armR = new Vector3(60f, 0f, 10f); return; } // the freeze
                if (w < 1f) { p.armR = new Vector3(110f, 0f, 15f * Mathf.Sin((w - 0.4f) * 4f * Mathf.PI * 2f)); return; } // no, wait
                float sw = Mathf.Sin((w - 1f) * 0.8f * Mathf.PI * 2f), k = In(w, 1f, 0.3f) * (1f - In(w, 3.6f, 0.4f));
                p.armL = L(p.armL, new Vector3(0f, 0f, -130f - 20f * sw), k); p.armR = L(p.armR, new Vector3(0f, 0f, 130f + 20f * sw), k);
                p.roll += 8f * sw * k;
                return;
            }
            if (s < SlowScary) { Voices(ref p, (s - SlowVoices2) % 6f); return; }
            if (s < SlowEnd)
            {   // 5 the scary part: crouch and beckon twice, still, then the jump (the hit)
                float c = s - SlowScary, j = s - SlowJump;
                float crouch = In(c, 0f, 0.4f) * (1f - In(j, 0f, 0.12f));
                p.squash *= Mathf.Lerp(1f, 0.8f, crouch); p.lean += 25f * crouch; p.offset.y -= 0.015f * crouch;
                if (c < 1f) p.armR = new Vector3(Mathf.Lerp(80f, 110f, Mathf.PingPong(c * 4f, 1f)), 0f, 10f); // come closer (twice)
                else if (j < 0f) { p.armL = new Vector3(60f, 0f, -14f); p.armR = new Vector3(60f, 0f, 14f); }
                else
                {
                    float up = In(j, 0f, 0.12f) * (1f - In(j, 0.9f, 0.4f));
                    p.armL = L(p.armL, new Vector3(0f, 0f, -160f), up); p.armR = L(p.armR, new Vector3(0f, 0f, 160f), up);
                    p.offset.y += 0.04f * Bump(j, 0f, 0.35f);
                    if (j < 0.06f) p.hit = 1f;
                }
                return;
            }
            {   // 6 the end beat: the hands come down slowly, palms down, then a bow
                float e = s - SlowEnd;
                float down = In(e, 0f, 1f);
                p.armL = L(new Vector3(60f, 0f, -24f), new Vector3(30f, 0f, -16f), down); p.armR = L(new Vector3(60f, 0f, 24f), new Vector3(30f, 0f, 16f), down);
                p.lean += 30f * Bump(e, 1.1f, 0.6f);
            }
        }

        /// <summary>B76.a beat 2: the big voice then the small one, 3 s each, a 0.3 s snap turn between.</summary>
        static void Voices(ref DollPose p, float v)
        {
            bool big = v < 3f;
            float u = big ? v : v - 3f, turn = In(u, 0f, 0.3f);
            if (big)
            {
                p.squash *= Mathf.Lerp(1f, BigSquash, turn); p.lean -= 6f * turn; // chin up, tall
                p.armL = L(p.armL, new Vector3(30f, 0f, -70f), turn); p.armR = L(p.armR, new Vector3(30f, 0f, 70f), turn);
                p.offset.y += 0.015f * Mathf.Abs(Mathf.Sin(u * Mathf.PI)); p.yaw += 25f * turn; // a slow heavy bob at 1 Hz
            }
            else
            {
                p.squash *= Mathf.Lerp(1f, SmallSquash, turn); p.lean += 15f * turn; p.roll += 12f * turn;
                p.armL = L(p.armL, new Vector3(90f, 0f, 20f), turn); p.armR = L(p.armR, new Vector3(90f, 0f, -20f), turn); // hands together at the chest
                p.offset.y += 0.008f * Mathf.Abs(Mathf.Sin(u * 3f * Mathf.PI)); p.yaw -= 25f * turn;
            }
        }

        /// <summary>A listener at <paramref name="s"/> s into the slow telling: how far a kid has scooted in (u), the pose on top of the sit.
        /// <paramref name="first"/>: the kid nearest the middle (the one a grown-up's arm goes round, the one who throws an arm up at the end);
        /// <paramref name="hugs"/>: the grown-up who puts an arm round it.</summary>
        public static float SlowListener(ref DollPose p, float s, bool kid, bool first, bool hugs)
        {
            float scoot = 0f;
            if (kid)
            {
                scoot += 0.03f * In(s, 0.6f, 0.6f); // the hush: they shuffle in
                if (s >= SlowWind + 1.4f && s < SlowVoices2)
                {   // the kids copy the wind a beat late, arms up, swaying
                    float w = s - SlowWind - 1.4f, sw = Mathf.Sin(w * 0.8f * Mathf.PI * 2f), k = In(w, 0f, 0.3f) * (1f - In(w, 2.4f, 0.4f));
                    p.armL = L(p.armL, new Vector3(0f, 0f, -150f), k); p.armR = L(p.armR, new Vector3(0f, 0f, 150f), k); p.roll += 5f * sw * k;
                }
                if (s >= SlowScary)
                {
                    scoot += 0.04f * In(s, SlowScary + 0.6f, 0.6f) * (1f - In(s, SlowEnd + 1f, 1f)); // come closer
                    float j = s - SlowJump;
                    if (j >= 0f && j < 1.6f)
                    {
                        scoot -= 0.03f * Bump(j, 0f, 0.7f); p.lean -= 15f * Bump(j, 0f, 0.5f); // the flinch
                        if (j > 0.6f) { p.offset.y += 0.006f * Mathf.Abs(Mathf.Sin((j - 0.6f) * 14f)); p.lean -= 6f; } // then the laugh
                    }
                }
                if (first && s > SlowEnd + 2f) { float h = s - SlowEnd - 2f; p.armR = new Vector3(170f, 0f, 10f); p.offset.y += 0.03f * Bump(h, 0f, 0.4f); } // again!
            }
            else
            {
                if (hugs && s >= SlowScary + 1f && s < SlowEnd + 1f) p.armL = new Vector3(70f, 0f, 60f); // an arm round the nearest kid (B55.b)
                if (s >= SlowEnd + 1.6f) p.lean += 9f * (Bump(s, SlowEnd + 1.6f, 0.5f) + Bump(s, SlowEnd + 2.2f, 0.5f)); // the nods, twice
                float j = s - SlowJump; if (j >= 0f && j < 0.5f) p.lean -= 6f * Bump(j, 0f, 0.5f);
            }
            return scoot;
        }

        // ================================================================== B76.b short, and gets a laugh

        /// <summary>The teller at <paramref name="s"/> s into the short telling (the beats' clock), through the laugh. <paramref name="pileYaw"/>:
        /// the woodpile's bearing from the way they face (degrees), NaN without a woodpile.</summary>
        public static void Short(ref DollPose p, float s, float pileYaw)
        {
            bool wood = !float.IsNaN(pileYaw);
            p.armL = new Vector3(20f, 0f, -16f); p.armR = new Vector3(20f, 0f, 16f);
            if (s < ShortQuick)
            {   // 1 listen: a finger up, a quick look round
                p.armR = L(p.armR, new Vector3(140f, 0f, 10f), In(s, 0f, 0.4f));
                p.yaw += 30f * Mathf.Sin(Mathf.Clamp01((s - 0.4f) / 0.6f) * Mathf.PI * 2f);
                return;
            }
            if (s < ShortWood || !wood && s < ShortEndAt) { Talker(ref p, s - ShortQuick); return; }
            if (s < ShortEndAt)
            {   // 3 the woodpile: mime sitting down, jumping up, then point at it
                float w = s - ShortWood;
                if (w < 0.5f) { p.squash *= Mathf.Lerp(1f, 0.75f, In(w, 0f, 0.3f)); p.offset.y -= 0.02f * In(w, 0f, 0.3f); }
                else if (w < 1f) { float up = In(w, 0.5f, 0.12f); p.offset.y += 0.06f * Bump(w, 0.5f, 0.4f); p.armL = L(p.armL, new Vector3(160f, 0f, -14f), up); p.armR = L(p.armR, new Vector3(160f, 0f, 14f), up); if (w < 0.56f) p.hit = 1f; }
                else { p.yaw += pileYaw * In(w, 1f, 0.3f); p.armR = new Vector3(90f, 0f, 6f); }
                return;
            }
            if (s < ShortLength)
            {   // 4 the end: arms out wide, palms up, a quick bow (the hit: the laugh)
                float e = s - ShortEndAt, k = In(e, 0f, 0.3f);
                p.armL = L(p.armL, new Vector3(60f, 0f, -80f), k); p.armR = L(p.armR, new Vector3(60f, 0f, 80f), k);
                p.lean += 25f * Bump(e, 0.6f, 0.4f);
                return;
            }
            {   // 5 end beat: a hand on the chest, two slow nods, mock-serious; then laughs too
                float e = s - ShortLength;
                p.armR = new Vector3(100f, 0f, -20f);
                p.lean += 10f * (Bump(e, 0.3f, 0.6f) + Bump(e, 1f, 0.6f));
                if (e > 1.7f) p.offset.y += 0.006f * Mathf.Abs(Mathf.Sin((e - 1.7f) * 14f));
            }
        }

        /// <summary>B7.a's talker at speed: a beat every 0.8 s, an open hand to the left, then the right, a bob.</summary>
        static void Talker(ref DollPose p, float t)
        {
            int beat = Mathf.FloorToInt(t / 0.8f); float u = t - beat * 0.8f, k = Bump(u, 0f, 0.8f);
            if ((beat & 1) == 0) p.armL = L(p.armL, new Vector3(70f, 0f, -55f), k); else p.armR = L(p.armR, new Vector3(70f, 0f, 55f), k);
            p.offset.y += 0.015f * k;
        }

        /// <summary>A listener in the short telling: the heads turn to the woodpile, 0.05 s apart (<paramref name="order"/>); the kids rock back on the laugh.</summary>
        public static void ShortListener(ref DollPose p, float s, bool kid, int order, float pileYaw)
        {
            if (!float.IsNaN(pileYaw) && s >= ShortWood + 1f && s < ShortEndAt + 0.4f) p.yaw += pileYaw * In(s, ShortWood + 1f + 0.05f * order, 0.3f) * (1f - In(s, ShortEndAt, 0.4f));
            if (kid && s >= ShortLength && s < ShortLength + 0.8f) p.lean -= 20f * Bump(s, ShortLength, 0.8f);
            if (s >= ShortLength + 1.7f) p.offset.y += 0.02f * Mathf.Abs(Mathf.Sin((s - ShortLength - 1.7f) * 12f)) * (1f - In(s, ShortLength + LaughLength, 0.3f)); // the second, bigger laugh
        }
    }
}
