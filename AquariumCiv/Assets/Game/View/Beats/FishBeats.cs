// #364 B8 Fishing pays off (animation-specs B8). Pure: Mathf, Vector3 and DollPose only, so Tools/AnimBench compiles it.
// The view side (the fish in its arc, the basket, the splash, the frog that moves off) is View/FishingView.cs.
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// A fisher's block as a schedule on the block's own seconds (<c>since</c>, shifted by the cheat): a small tug every 4.5 s, and every
    /// 2 to 4 tugs (by the fisher's hash, never the sim's stream) the tug is a bite instead. A bite is a nibble (the rod dips twice, a tiny ring
    /// at the line's end, a lean in), a held breath, then the yank: the contact frame is when the fish leaves the water. A catch (2 in 3) flips
    /// high into the basket beside the fisher (arms up, a hop); a miss (1 in 3) only clears the water and drops back (the fisher slumps).
    /// The last <see cref="EndSeconds"/> of the block are the end beat: the line comes in and the last fish is held up (a block with
    /// no catch ends in a stretch).
    /// </summary>
    public static class FishBeats
    {
        const int Salt = 364;

        public const float TugCycle = 4.5f, TugSeconds = 0.6f, TugDegrees = 22f;
        /// <summary>The fish's arc from the line's end to the basket (spec: h 0.3 u, T 0.6 s, two flips), and a missed fish's hop.</summary>
        public const float Flight = 0.6f, Apex = 0.3f, Spin = 720f, MissApex = 0.05f, MissFlight = 0.4f;
        /// <summary>Where the line's end is, out from the fisher's feet along the way they face (adult units): the rod's tip, 0.06 u back from the edge, and the line.</summary>
        public const float LineReach = 0.34f, BasketSide = 0.08f;
        /// <summary>The end beat's length (block seconds left), and when in it the fish is in the hand.</summary>
        public const float EndSeconds = 1.6f, FishInHandAt = 0.3f;
        /// <summary>The basket shows this many fish at most (the pile of a few); the sim has no per-fish count.</summary>
        public const int MaxShown = 2;
        /// <summary>The nibble's two tiny rings, seconds into the bite (the bottoms of the two dips).</summary>
        public static readonly float[] NibbleRingAt = { 0.12f, 0.42f };

        public static readonly Beat CatchBeat, MissBeat, EndWithFish, EndEmpty;

        static FishBeats()
        {
            // Both bites share the nibble and the yank (arm x 118 is the rod held out; the dip is 100, the yank 165): 0.15 + 0.15 + 0.15 + 0.35 + 0.15 = contact at 0.95 s.
            CatchBeat = new Beat(
                Beat.Step(BeatPhase.Antic, 0.15f, Ease.Out, PoseKey.Hold.ArmR(100f, 0f, 6f)),
                Beat.Step(BeatPhase.Antic, 0.15f, Ease.Smooth, PoseKey.Hold.ArmR(118f, 0f, 6f)),
                Beat.Step(BeatPhase.Antic, 0.15f, Ease.Out, PoseKey.Hold.ArmR(100f, 0f, 6f).Lean(10f)),
                Beat.Step(BeatPhase.Hold, 0.35f, Ease.Linear, PoseKey.Hold),
                Beat.Step(BeatPhase.Act, 0.15f, Ease.Snap, PoseKey.Hold.ArmR(165f, 0f, 6f).Lean(-15f).Squash(1.03f)),
                Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold),
                // arms up and one hop of 0.03 u, while the fish is in the air
                Beat.Step(BeatPhase.Follow, 0.2f, Ease.Out, PoseKey.Hold.ArmL(70f, 0f, -150f).ArmR(150f, 0f, 30f).Offset(0f, 0.03f, 0f).Lean(-8f)),
                Beat.Step(BeatPhase.Follow, 0.3f, Ease.In, PoseKey.Hold.Offset(0f, 0f, 0f).Lean(0f).Squash(0.96f)),
                Beat.Step(BeatPhase.Hold, 0.15f, Ease.Linear, PoseKey.Hold),
                Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Base));
            MissBeat = new Beat(
                Beat.Step(BeatPhase.Antic, 0.15f, Ease.Out, PoseKey.Hold.ArmR(100f, 0f, 6f)),
                Beat.Step(BeatPhase.Antic, 0.15f, Ease.Smooth, PoseKey.Hold.ArmR(118f, 0f, 6f)),
                Beat.Step(BeatPhase.Antic, 0.15f, Ease.Out, PoseKey.Hold.ArmR(100f, 0f, 6f).Lean(10f)),
                Beat.Step(BeatPhase.Hold, 0.35f, Ease.Linear, PoseKey.Hold),
                Beat.Step(BeatPhase.Act, 0.15f, Ease.Snap, PoseKey.Hold.ArmR(165f, 0f, 6f).Lean(-15f).Squash(1.03f)),
                Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold),
                // the slump: the rod falls, the shoulders drop, the arms hang limp
                Beat.Step(BeatPhase.Follow, 0.3f, Ease.Smooth, PoseKey.Hold.ArmR(60f, 0f, 8f).ArmL(8f, 0f, -8f).Lean(20f).Squash(0.94f)),
                Beat.Step(BeatPhase.Hold, 0.3f, Ease.Linear, PoseKey.Hold),
                Beat.Step(BeatPhase.Settle, 0.45f, Ease.Smooth, PoseKey.Base));
            // The end beat: the line comes in (rod down), then the catch goes up on the stick (arm 150) and is held; with no catch, a stretch.
            EndWithFish = new Beat(
                Beat.Step(BeatPhase.Antic, FishInHandAt, Ease.Out, PoseKey.Hold.ArmR(70f, 0f, 8f).ArmL(40f, 0f, -12f).Lean(4f).Extend(false)),
                Beat.Step(BeatPhase.EndBeat, 0.35f, Ease.Back, PoseKey.Hold.ArmR(150f, 0f, 8f).Lean(-6f).Squash(0.96f)),
                Beat.Step(BeatPhase.Hold, EndSeconds - FishInHandAt - 0.35f, Ease.Linear, PoseKey.Hold));
            EndEmpty = new Beat(
                Beat.Step(BeatPhase.Antic, FishInHandAt, Ease.Out, PoseKey.Hold.ArmR(70f, 0f, 8f).ArmL(40f, 0f, -12f).Lean(4f).Extend(false)),
                Beat.Step(BeatPhase.EndBeat, 0.4f, Ease.Back, PoseKey.Hold.Arms(160f, 22f).Lean(-8f)),
                Beat.Step(BeatPhase.Hold, EndSeconds - FishInHandAt - 0.4f, Ease.Linear, PoseKey.Hold));
        }

        // ---------------------------------------------------------------- the schedule

        /// <summary>The first tug, block seconds (2.5 to 4.5 by the fisher's hash), so neighbours never tug together.</summary>
        public static float FirstTug(int key) => BeatHash.Range(2.5f, 4.5f, key, Salt, 1);

        /// <summary>Tugs in group <paramref name="k"/>: 2, 3 or 4. The last one of the group is the bite.</summary>
        public static int TugsIn(int key, int k) => Mathf.Clamp(2 + (int)(BeatHash.U01(key, Salt, 10 + k) * 3f), 2, 4);

        /// <summary>When bite <paramref name="k"/> begins (block seconds): the tug that ends its group of 2 to 4. Bites are 9 to 18 s apart.</summary>
        public static float BiteAt(int key, int k)
        {
            int tugs = 0;
            for (int g = 0; g <= k; g++) tugs += TugsIn(key, g);
            return FirstTug(key) + TugCycle * (tugs - 1);
        }

        /// <summary>One in three bites is a miss (the fish only clears the water).</summary>
        public static bool Miss(int key, int k) => BeatHash.U01(key, Salt, 100 + k) < 1f / 3f;

        /// <summary>The latest bite that has begun by <paramref name="s"/>, or −1.</summary>
        public static int BiteIndexAt(int key, float s)
        {
            int k = -1;
            while (BiteAt(key, k + 1) <= s) k++;
            return k;
        }

        /// <summary>The first bite after <paramref name="s"/>, and (with <paramref name="wantCatch"/>) the first of those that is not a miss.</summary>
        public static int NextBite(int key, float s, bool wantCatch)
        {
            int k = BiteIndexAt(key, s) + 1;
            while (wantCatch && Miss(key, k)) k++;
            return k;
        }

        /// <summary>Seconds into a bite when the fish leaves the water (the Contact), and when a catch lands in the basket.</summary>
        public static float ContactAt => CatchBeat.ContactAt;
        public static float LandAt => CatchBeat.ContactAt + Flight;
        public static float BiteSeconds => CatchBeat.Duration;

        /// <summary>Fish landed in the basket by block second <paramref name="s"/> (the misses never land).</summary>
        public static int CatchesBy(int key, float s)
        {
            int n = 0;
            for (int k = 0; BiteAt(key, k) + LandAt <= s; k++) if (!Miss(key, k)) n++;
            return n;
        }

        /// <summary>Fish the basket shows: the catches so far (at most <see cref="MaxShown"/>), one less while the last is held up.</summary>
        public static int Shown(int key, float s, float left)
        {
            int n = CatchesBy(key, s) - (HoldsFish(key, s, left) ? 1 : 0);
            return Mathf.Clamp(n, 0, MaxShown);
        }

        /// <summary>Is the last catch in the fisher's hand (the end beat, after the line came in)? The prop swaps from the rod to the stick-and-fish.</summary>
        public static bool HoldsFish(int key, float s, float left) => left <= EndSeconds - FishInHandAt && CatchesBy(key, s) > 0;

        /// <summary>Is the end beat playing (the block's last <see cref="EndSeconds"/>)?</summary>
        public static bool Ending(float left) => left <= EndSeconds;

        // ---------------------------------------------------------------- the pose

        /// <summary>The fishing laid over the standing rod pose (DollMotion's Fish: rod out, a slow bob). <paramref name="s"/> is the block's seconds,
        /// <paramref name="left"/> the seconds to its end.</summary>
        public static void Apply(ref DollPose pose, float s, int key, float left)
        {
            if (Ending(left))
            {
                float rel = EndSeconds - left;
                bool fish = CatchesBy(key, s) > 0;
                (fish ? EndWithFish : EndEmpty).Apply(ref pose, rel);
                if (fish && rel > 0.65f) pose.roll += Mathf.Sin((rel - 0.65f) * Mathf.PI * 2f * 0.9f) * 3f; // a proud little sway
                return;
            }
            int k = BiteIndexAt(key, s);
            if (k >= 0)
            {
                float rel = s - BiteAt(key, k);
                var beat = Miss(key, k) ? MissBeat : CatchBeat;
                if (rel < beat.Duration) { beat.Apply(ref pose, rel); return; }
            }
            pose.armR.x += Tug(key, s);
        }

        /// <summary>The small tug of the rod (arm x, degrees): a bump 0.6 s wide every 4.5 s, except on the tugs that are bites.</summary>
        public static float Tug(int key, float s)
        {
            float first = FirstTug(key);
            int n = Mathf.RoundToInt((s - first) / TugCycle);
            if (n < 0) return 0f;
            float u = (s - (first + TugCycle * n) + TugSeconds * 0.5f) / TugSeconds;
            if (u < 0f || u > 1f || IsBiteTug(key, n)) return 0f;
            return Mathf.Sin(u * Mathf.PI) * TugDegrees;
        }

        static bool IsBiteTug(int key, int n)
        {
            int tugs = 0;
            for (int g = 0; ; g++)
            {
                tugs += TugsIn(key, g);
                if (tugs - 1 == n) return true;
                if (tugs - 1 > n) return false;
            }
        }
    }
}
