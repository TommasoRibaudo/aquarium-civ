// #705 (B28): hanging out the washing and taking it in. Pure: Mathf and DollPose only, so Tools/AnimBench compiles it.
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// B28.b, one cloth's beat (2.4 s): bend to the basket and lift the wet cloth, shake it out, reach it up to the cord, pin it
    /// (two Contacts: the pin at each top corner), smooth it flat with one hand sweeping down while the other holds the cord, and a side step to the
    /// next. After the last cloth she shakes her hands dry. <see cref="TakeIn"/> is B28.d: from the line's near end, a hand up to each
    /// cloth and a pull, one after another. The line itself (cord, stakes, cloths) is <see cref="LaundryLines"/>.
    /// </summary>
    public static class LaundryBeats
    {
        public static readonly Beat HangBeat = new Beat(
            // lift: bend to the basket, the cloth up wet and heavy
            Beat.Step(BeatPhase.Antic, 0.25f, Ease.Smooth, PoseKey.Hold.Lean(25f).Squash(0.88f).Offset(0f, 0f, -0.012f).Arms(55f, 8f)),
            Beat.Step(BeatPhase.Act, 0.35f, Ease.Back, PoseKey.Hold.Lean(4f).Squash(1f).Offset(0f, 0f, 0f).ArmL(100f, 0f, -10f).ArmR(112f, 0f, 10f)),
            // shake: both arms 130 - 150 - 130, the cloth snaps out flat
            Beat.Step(BeatPhase.Act, 0.12f, Ease.Snap, PoseKey.Hold.Lean(-2f).Arms(150f, 8f)),
            Beat.Step(BeatPhase.Act, 0.12f, Ease.Snap, PoseKey.Hold.Arms(128f, 8f)),
            Beat.Step(BeatPhase.Act, 0.11f, Ease.Snap, PoseKey.Hold.Arms(152f, 8f)),
            // reach: both hands up, the cloth against the cord, the head back
            Beat.Step(BeatPhase.Approach, 0.30f, Ease.Out, PoseKey.Hold.Lean(-8f).Offset(0f, 0.004f, 0f).Arms(168f, 6f)),
            // pin: the right hand down on the first corner (Contact), a beat on the cloth, up to the second (Contact)
            Beat.Step(BeatPhase.Act, 0.08f, Ease.Snap, PoseKey.Hold.ArmR(150f, 0f, 8f)),
            Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.07f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Act, 0.06f, Ease.Snap, PoseKey.Hold.ArmR(172f, 0f, 8f)),
            Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold.ArmR(158f, 0f, 8f)),
            Beat.Step(BeatPhase.Hold, 0.02f, Ease.Linear, PoseKey.Hold),
            // smooth: one hand sweeps the cloth top to bottom while the other holds the cord
            Beat.Step(BeatPhase.Follow, 0.40f, Ease.Smooth, PoseKey.Hold.Lean(-2f).ArmR(100f, 0f, 6f).ArmL(166f, 0f, -6f)),
            // step: a side step and back toward the basket for the next
            Beat.Step(BeatPhase.Settle, 0.40f, Ease.Smooth, PoseKey.Hold.Lean(12f).Offset(0f, 0f, 0f).Arms(58f, 8f)));

        /// <summary>The pull on one cloth when taking in (0.45 s): the hand up to it, a snap down and back with the cloth.</summary>
        public static readonly Beat PluckBeat = new Beat(
            Beat.Step(BeatPhase.Antic, 0.12f, Ease.Smooth, PoseKey.Hold.Lean(-4f).Squash(1f).ArmR(160f, 0f, 8f).ArmL(40f, 0f, -10f)),
            Beat.Step(BeatPhase.Act, 0.18f, Ease.Snap, PoseKey.Hold.Lean(6f).ArmR(60f, 0f, 8f)),
            Beat.Step(BeatPhase.Settle, 0.15f, Ease.Smooth, PoseKey.Hold.Lean(4f).ArmR(48f, 0f, 6f).ArmL(48f, 0f, -6f)));

        /// <summary>The whole chore's pose <paramref name="since"/> real seconds in, for <paramref name="cloths"/> cloths.</summary>
        public static void Hang(ref DollPose p, float since, int cloths)
        {
            cloths = Mathf.Max(1, cloths);
            if (since < 0f) return;
            float done = cloths * Laundry.SecondsPerCloth;
            if (since < done) { HangBeat.Apply(ref p, since % Laundry.SecondsPerCloth); return; }
            float t = since - done;
            if (t < 0.5f)
            {   // both hands shaken dry (arm z 40, 2 Hz)
                float s = Mathf.Sin(t * Mathf.PI * 4f);
                p.armL = new Vector3(40f, 0f, -22f - 18f * s); p.armR = new Vector3(40f, 0f, 22f + 18f * s);
                p.lean = 6f;
            }
            else
            {   // a look at the full line, one nod
                float n = Mathf.Sin(Mathf.Clamp01((t - 0.5f) / 0.7f) * Mathf.PI);
                p.armL = new Vector3(8f, 0f, -14f); p.armR = new Vector3(8f, 0f, 14f);
                p.lean = -3f + n * 8f;
            }
        }

        /// <summary>Which cloth (0-based) a hanging doll is at <paramref name="since"/> real seconds in, and how far she is on the side step to the next (0-1).</summary>
        public static int AtCloth(float since, int cloths, out float stepping)
        {
            stepping = 0f;
            if (since < 0f) return 0;
            int k = Mathf.Min(cloths - 1, (int)(since / Laundry.SecondsPerCloth));
            float local = since - k * Laundry.SecondsPerCloth;
            float stepAt = Laundry.SecondsPerCloth - Laundry.Step;
            if (k < cloths - 1 && local > stepAt) stepping = Mathf.SmoothStep(0f, 1f, (local - stepAt) / Laundry.Step);
            return k;
        }

        /// <summary>Where along the cord (from its middle) the hanging doll stands <paramref name="since"/> real seconds in: at each cloth in turn with the side step
        /// between, and once the last is hung she steps clear of the cord's far stake to look at it, not in front of her own washing.</summary>
        public static float HangAlong(float since, int cloths, float scale)
        {
            cloths = Mathf.Max(1, cloths);
            int k = AtCloth(since, cloths, out float stepping);
            float a = Mathf.Lerp(Laundry.SlotAlong(k, cloths, scale), Laundry.SlotAlong(Mathf.Min(k + 1, cloths - 1), cloths, scale), stepping);
            float done = cloths * Laundry.SecondsPerCloth;
            if (since > done)
                a = Mathf.Lerp(Laundry.SlotAlong(cloths - 1, cloths, scale), (Laundry.Length(cloths) * 0.5f + 0.09f) * scale, Mathf.SmoothStep(0f, 1f, (since - done) / 0.6f));
            return a;
        }

        /// <summary>The take-in: out to the line, then a pull on each cloth in turn, near end to far. The doll carries the basket (Hand slot).</summary>
        public static void TakeIn(ref DollPose p, float since, int cloths)
        {
            float t = since - Laundry.WalkOut;
            if (t < 0f) return;
            int i = (int)(t / Laundry.PluckSeconds);
            if (i < cloths) { PluckBeat.Apply(ref p, t - i * Laundry.PluckSeconds); return; }
            // the basket held up and carried in
            p.armL = new Vector3(48f, 0f, -6f); p.armR = new Vector3(48f, 0f, 6f);
        }

        /// <summary>Which cloth (0-based, near end first) a taker is plucking <paramref name="since"/> real seconds in.</summary>
        public static int PluckingCloth(float since, int cloths) => Mathf.Clamp((int)((since - Laundry.WalkOut) / Laundry.PluckSeconds), 0, Mathf.Max(0, cloths - 1));
    }
}
