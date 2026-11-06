// #365 B9 Meals and the fire (pure: Mathf, Vector2/3, DollPose and the beat kit only, so Tools/AnimBench compiles it).
// Sheets: docs/design/animation-specs.md B9.a eating, B9.b passing bowls, B9.c poking the fire, B9.d the cook stirs.
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// B9: the meal's poses as pure functions of time. <see cref="Eat"/> is the seated doll with a bowl in the left hand and the right
    /// hand going to the mouth now and then (the bite's moment is hashed from the doll's own clock, so neighbours never bite together),
    /// and a lean-back at the end of the meal. <see cref="Poke"/> is the stick into the fire (the fire's sparks answer on the Contact).
    /// <see cref="StirIndex"/> and <see cref="Stir"/> are the cook's stirring with a taste every fourth stir.
    /// </summary>
    public static class MealBeats
    {
        // ------------------------------------------------------------------ eating (B9.a)
        /// <summary>One bite window is <see cref="BiteWindow"/> s; in it the bite starts at a hashed point (so the gaps are 2.7 to 5.2 s) and takes <see cref="BiteSeconds"/>.</summary>
        public const float BiteWindow = 4f, BiteJitter = 1.2f, BiteUp = 0.35f, BiteChew = 0.6f, BiteDown = 0.35f;
        public const float BiteSeconds = BiteUp + BiteChew + BiteDown;
        /// <summary>The meal's last seconds: lean back, arms out, a contented sway.</summary>
        public const float EndSeconds = 1.3f, EndIn = 0.4f;
        /// <summary>The chew bob (spec 0.008 u; raised to read at 30 px) and its rate.</summary>
        public const float ChewBob = 0.013f, ChewHz = 8f;

        /// <summary>When the bite starts inside window <paramref name="n"/> (seconds from the window's start).</summary>
        public static float BiteStart(int n) => BeatHash.U01(n, 365, 1) * BiteJitter;

        /// <summary>The bite at doll clock <paramref name="t"/>: seconds into it, or −1 when not biting.</summary>
        public static float BiteAt(float t)
        {
            int n = Mathf.FloorToInt(t / BiteWindow);
            float x = t - n * BiteWindow - BiteStart(n);
            return x >= 0f && x < BiteSeconds ? x : -1f;
        }

        /// <summary>The seated eater's pose. <paramref name="t"/>: the doll's own clock; <paramref name="left"/>: real seconds to the end of the meal (large: no end yet).</summary>
        public static void Eat(ref DollPose p, float t, float left)
        {
            // seated, as the Sit loop: low, squashed, a little forward
            p.offset.y = -0.03f;
            p.squash = 0.78f;
            p.lean = 6f + Mathf.Sin(t * 0.7f) * 1.5f;
            p.armL = new Vector3(60f, 0f, -10f);   // the bowl in the left hand, held up in front
            p.armR = new Vector3(45f, 0f, 10f);    // the spoon hand, at the bowl
            float x = BiteAt(t);
            float endK = left < EndSeconds + EndIn ? Easing.Apply(Ease.Smooth, (EndSeconds + EndIn - left) / EndIn) : 0f; // 0..1 into the end beat
            if (x >= 0f)
            {
                float bite = 1f - endK;
                float arm;
                if (x < BiteUp) arm = Mathf.Lerp(45f, 135f, Easing.Apply(Ease.Smooth, x / BiteUp));
                else if (x < BiteUp + BiteChew) arm = 135f;
                else arm = Mathf.Lerp(135f, 45f, Easing.Apply(Ease.Smooth, (x - BiteUp - BiteChew) / BiteDown));
                p.armR.x = Mathf.Lerp(45f, arm, bite);
                p.lean = Mathf.Lerp(p.lean, 2f, bite * Mathf.Clamp01(x / BiteUp));
                if (x >= BiteUp && x < BiteUp + BiteChew)
                    p.offset.y += Mathf.Abs(Mathf.Sin((x - BiteUp) * ChewHz * Mathf.PI)) * ChewBob * bite; // the chew bob
            }
            if (endK > 0f)
            {
                // full: lean back, both arms out wide (a short stretch), a slow sway
                float sway = Mathf.Sin((EndSeconds + EndIn - left) * Mathf.PI * 2f / 1.4f);
                p.lean = Mathf.Lerp(p.lean, -10f, endK);
                p.armL = Vector3.Lerp(p.armL, new Vector3(25f, 0f, -75f), endK);
                p.armR = Vector3.Lerp(p.armR, new Vector3(25f, 0f, 75f), endK);
                p.roll = sway * 4f * endK;
                p.offset.y = Mathf.Lerp(p.offset.y, -0.03f, endK) + 0.004f * endK;
                p.squash = Mathf.Lerp(p.squash, 0.86f, endK);
            }
        }

        // ------------------------------------------------------------------ poking the fire (B9.c)
        /// <summary>Stands up out of the sit with the stick raised (x 125), thrusts down and forward at the fire (Snap; the Contact: sparks and a flare), holds, pulls back, sits.</summary>
        public static readonly Beat Poke = new Beat(
            Beat.Step(BeatPhase.Antic, 0.34f, Ease.Back, PoseKey.Hold.Squash(1f).Offset(0f, 0f, -0.03f).Lean(-6f).ArmR(125f, 0f, 8f).ArmL(30f, 0f, -12f).Extend(true)),
            Beat.Step(BeatPhase.Act, 0.15f, Ease.Snap, PoseKey.Hold.Offset(0f, 0f, -0.06f).Lean(20f).ArmR(68f, 0f, 6f)),
            Beat.Step(BeatPhase.Contact, 0.08f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Hold, 0.22f, Ease.Linear, PoseKey.Hold),
            Beat.Step(BeatPhase.Follow, 0.40f, Ease.Smooth, PoseKey.Hold.Offset(0f, 0f, -0.03f).Lean(8f).ArmR(100f, 0f, 8f)),
            Beat.Step(BeatPhase.Settle, 0.45f, Ease.Smooth, PoseKey.Base));

        /// <summary>One poke slot is <see cref="PokeSlot"/> s; the poke starts at a hashed point in its first <see cref="PokeJitter"/> s, so the gap between two pokes is 5 to 8 s.</summary>
        public const float PokeSlot = 6.5f, PokeJitter = 1.5f;

        /// <summary>The poke slot at fire clock <paramref name="t"/>: its number, when its poke starts (clock seconds), and whether it is a big one (every third: the flare).</summary>
        public static int PokeSlotAt(int fireSeed, float t, out float startsAt, out bool big)
        {
            int n = Mathf.FloorToInt(t / PokeSlot);
            startsAt = n * PokeSlot + BeatHash.U01(fireSeed, n, 3651) * PokeJitter;
            big = BeatHash.Pick(3, fireSeed, n, 3652) == 0;
            return n;
        }

        /// <summary>Who of <paramref name="count"/> candidates (sorted by key) pokes in slot <paramref name="n"/>.</summary>
        public static int PokerIndex(int fireSeed, int n, int count) => BeatHash.Pick(count, fireSeed, n, 3653);

        // ------------------------------------------------------------------ the cook stirs (B9.d)
        public const int StirsPerTaste = 4;
        public const float StirSeconds = 1.6f, TasteSeconds = 1.9f;

        public static float StirCycle(float pace) => StirsPerTaste * StirSeconds / Mathf.Clamp(pace <= 0f ? 1f : pace, 0.5f, 1.5f) + TasteSeconds;

        /// <summary>The stir count at doll clock <paramref name="t"/> (steam at each new one), and where in the cycle it is: <paramref name="u"/> is seconds into the stirring,
        /// <paramref name="taste"/> seconds into the taste (−1: stirring).</summary>
        public static int StirIndex(float t, float pace, out float u, out float taste)
        {
            pace = Mathf.Clamp(pace <= 0f ? 1f : pace, 0.5f, 1.5f);
            float per = StirSeconds / pace, stirring = StirsPerTaste * per, cycle = stirring + TasteSeconds;
            int c = Mathf.FloorToInt(t / cycle);
            float within = t - c * cycle;
            if (within < stirring) { u = within; taste = -1f; return c * StirsPerTaste + Mathf.FloorToInt(within / per); }
            u = stirring; taste = within - stirring;
            return c * StirsPerTaste + StirsPerTaste - 1;
        }

        /// <summary>The cook's stirring arm with a taste every fourth stir: the paddle leaves the pot, the hand goes to the mouth (chew), one nod, back to the pot.</summary>
        public static void Stir(ref DollPose p, float t, float pace)
        {
            pace = Mathf.Clamp(pace <= 0f ? 1f : pace, 0.5f, 1.5f);
            StirIndex(t, pace, out float u, out float taste);
            float w = u * Mathf.PI * 2f / (StirSeconds / pace);
            p.armR = new Vector3(40f + Mathf.Sin(w) * 14f, 0f, 10f + Mathf.Cos(w) * 14f);
            p.armL = new Vector3(25f, 0f, -12f);
            p.lean = 16f;
            p.roll = Mathf.Cos(w) * 2f;
            if (taste < 0f) return;
            var stir = p.armR; float stirLean = p.lean;
            float up = Easing.Apply(Ease.Smooth, taste / 0.45f);
            float down = Easing.Apply(Ease.Smooth, (taste - 1.5f) / 0.4f);
            float k = Mathf.Clamp01(up - down);
            p.armR = Vector3.Lerp(stir, new Vector3(130f, 0f, 18f), k);
            p.lean = Mathf.Lerp(stirLean, 4f, k);
            p.roll = Mathf.Lerp(p.roll, 0f, k);
            if (taste >= 0.2f && taste < 1.7f) p.extend = false; // the paddle comes up out of the pot, upright in the fist
            if (taste >= 0.45f && taste < 1.1f) p.offset.y += Mathf.Abs(Mathf.Sin((taste - 0.45f) * ChewHz * Mathf.PI)) * 0.006f; // chewing
            if (taste >= 1.1f && taste < 1.5f) p.lean += Mathf.Sin((taste - 1.1f) / 0.4f * Mathf.PI) * 12f;                          // one nod
        }
    }

    /// <summary>
    /// B9.b: the bowls go round the seated ring. One person (the cook, else a kid) walks from the pot round the ring and hands each a bowl: a
    /// chain of B7's hand-over events (<see cref="PairStage.HandOver"/>), 1.5 s a person, the takers staying seated. Pure; the game's
    /// <c>MealView</c> and AnimBench build the same chain.
    /// </summary>
    public static class MealStage
    {
        /// <summary>Seconds per person (walk 0.2 u + the hand over, squeezed), and the most people served by hand (the rest took theirs from the pile).</summary>
        public const float PerPerson = 1.5f;
        public const int MaxServed = 6;

        /// <summary>The ring in the order the giver walks it: from the pot's side, by angle round the fire, nearest first.</summary>
        public static List<int> Order(Vector2 fire, Vector2 pot, IList<int> keys, IList<Vector2> pos)
        {
            float a0 = Mathf.Atan2(pot.y - fire.y, pot.x - fire.x);
            var idx = new List<int>(keys.Count);
            for (int i = 0; i < keys.Count; i++) idx.Add(i);
            idx.Sort((a, b) => Ang(fire, pos[a], a0).CompareTo(Ang(fire, pos[b], a0)));
            var order = new List<int>(idx.Count);
            foreach (int i in idx) order.Add(i);
            return order;
        }

        static float Ang(Vector2 fire, Vector2 p, float a0) => Mathf.Repeat(Mathf.Atan2(p.y - fire.y, p.x - fire.x) - a0, Mathf.PI * 2f);

        /// <summary>The chain of hand-overs from <paramref name="giverPos"/>, the first at <paramref name="start"/>. <paramref name="servedAt"/> gets the moment each taker has the bowl (the Contact);
        /// the giver is served last. Dolls past <see cref="MaxServed"/> are served at <paramref name="start"/> (they took their own).</summary>
        public static List<PairEvent> Pass(int seed, float start, int giverKey, Vector2 giverPos, Vector2 fire, Vector2 pot, IList<int> takerKeys, IList<Vector2> takerPos, IDictionary<int, float> servedAt)
        {
            var events = new List<PairEvent>();
            var order = Order(fire, pot, takerKeys, takerPos);
            float t = start;
            var from = giverPos;
            int served = 0;
            foreach (int i in order)
            {
                if (takerKeys[i] == giverKey) continue;
                if (served >= MaxServed) { servedAt[takerKeys[i]] = start; continue; }
                var ev = PairStage.HandOver(seed, t, giverKey, takerKeys[i], from, takerPos[i], PropKind.Bowl, PerPerson, 4f);
                if (ev == null) { servedAt[takerKeys[i]] = start; continue; }
                ev.takerSeated = true;
                servedAt[takerKeys[i]] = ev.BeatStart + ev.giverBeat.ContactAt;
                events.Add(ev);
                t = ev.End;
                from = takerPos[i];
                served++;
            }
            servedAt[giverKey] = t + 0.3f;
            return events;
        }
    }
}
