// S-ritual (B72-B75, animation-specs "Rituals and the teller"): what each ritual beat looks like on a doll: the loop style and the
// hands (Look) and the pose laid on top (Apply). Pure: Mathf, Vector2/3, DollPose and the Life plan, so AnimBench compiles it.
// The plan is Life/MomentStagePlan.Rituals.cs; the things on the ground (the heap, the ladder, the board, the small basket) are drawn
// by View/CampLife.Rituals.cs. Every number is [est] until AnimLab.
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    public static class RitualBeats
    {
        /// <summary>A beat this file poses (the rest are MomentBeats').</summary>
        public static bool Handles(StageBeat b) => b >= StageBeat.HandOut || b == StageBeat.Stand || b == StageBeat.Travel || b == StageBeat.WalkOut || b == StageBeat.Run;

        // ---- small helpers
        static float In(float t, float from, float len) => len <= 0f ? (t >= from ? 1f : 0f) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - from) / len));
        static float Bump(float t, float len, float edge = 0.2f) => In(t, 0f, edge) * (1f - In(t, len - edge, edge));
        static Vector3 Lerp(Vector3 a, Vector3 b, float k) => Vector3.LerpUnclamped(a, b, k);
        static readonly Vector3 BothFrontL = new Vector3(70f, 0f, -10f), BothFrontR = new Vector3(70f, 0f, 10f);

        /// <summary>B72's kids eat every other berry: is this pick a taste (by the pick's own clock)?</summary>
        public static bool Tastes(int eat, float since) => eat == 2 || eat == 1 && Mathf.FloorToInt(Mathf.Max(0f, since) / MomentStagePlan.PickCycle) % 2 == 1;

        /// <summary>B73: the log passer <paramref name="j"/> is handling a log at plan time <paramref name="t"/> (which log, how far into the hop).</summary>
        public static bool LogAt(MomentStagePlan plan, int j, float t, out int k, out float u)
        {
            k = -1; u = 0f;
            float w = t - plan.LogReaches(0, j);
            if (w < 0f) return false;
            k = Mathf.FloorToInt(w / MomentStagePlan.LogEvery);
            if (k >= plan.LogCount) return false;
            u = w - k * MomentStagePlan.LogEvery;
            if (plan.DropLogAt >= 0 && k == plan.DropLogAt && j > plan.DropBy) return false; // the dropped one never comes
            return u < MomentStagePlan.HopSeconds;
        }

        /// <summary>B73: the kids' twigs: kid <paramref name="q"/> (0 = the far end) holds twig k at <paramref name="t"/>.</summary>
        public static bool TwigAt(MomentStagePlan plan, int q, float t, out float u)
        {
            u = 0f;
            float w = t - plan.LineStart - MomentStagePlan.TwigHop * q;
            if (w < 0f) return false;
            int k = Mathf.FloorToInt(w / MomentStagePlan.TwigEvery);
            if (k >= plan.TwigCount) return false;
            u = w - k * MomentStagePlan.TwigEvery;
            return u < MomentStagePlan.TwigHop;
        }

        // ================================================================== the loop style and the hands

        /// <summary>The loop style and what is in the hands for this step at plan time <paramref name="t"/>.</summary>
        public static void Look(MomentStagePlan plan, MomentMember m, int i, float t, bool walking, ref DollStyle style, ref PropKind kind, ref PropSlot slot)
        {
            var st = m.steps[i];
            float since = t - st.t0 - st.cue;
            // what the step holds
            switch (st.hold)
            {
                case StageHold.Basket: case StageHold.SmallBasket: (kind, slot) = (PropKind.Basket, PropSlot.Hand); break;
                case StageHold.FullBasket: (kind, slot) = (PropKind.Basket, PropSlot.Arms); break;
                case StageHold.Offering: (kind, slot) = (PropKind.Basket, PropSlot.Arms); break;
                case StageHold.Log: (kind, slot) = (PropKind.Log, PropSlot.Arms); break;
                case StageHold.Twig: (kind, slot) = (PropKind.Twig, PropSlot.Hand); break;
                case StageHold.Bowl: (kind, slot) = (PropKind.Bowl, PropSlot.Hand); break;
                default: (kind, slot) = (PropKind.None, PropSlot.Hand); break; // the board, the kids' small basket: drawn between the two (CampLife.Rituals)
            }
            if (st.hold == StageHold.SmallBasket && (st.beat == StageBeat.KidCarry || st.beat == StageBeat.KneelSet && st.arg >= 2)) kind = PropKind.None; // B75: the kids' basket is between them
            if (walking) { style = st.beat == StageBeat.Run ? DollStyle.Run : DollStyle.Walk; return; }
            switch (st.beat)
            {
                case StageBeat.Pick:
                    style = Tastes(st.arg, since) ? DollStyle.TasteBerry : DollStyle.PickBerry;
                    (kind, slot) = (PropKind.Basket, PropSlot.Hand);
                    break;
                case StageBeat.PointBush: style = DollStyle.Point; break;
                case StageBeat.Tip:
                    style = since >= 0f ? DollStyle.TipBasket : DollStyle.Stand;
                    if (since >= 0f) (kind, slot) = (PropKind.Basket, PropSlot.Hand);
                    break;
                case StageBeat.TableSet:
                    style = since > 1.2f ? DollStyle.TasteBerry : DollStyle.Stand;
                    if (since > 0.6f) kind = PropKind.None; // set on the table
                    break;
                case StageBeat.DropLog: if (t - st.t0 > 0.55f) kind = PropKind.None; style = DollStyle.Stand; break;
                case StageBeat.PassLog:
                    kind = LogAt(plan, st.arg, t, out _, out float lu) && lu > 0.2f ? PropKind.Log : PropKind.None; slot = PropSlot.Arms;
                    style = DollStyle.Stand;
                    break;
                case StageBeat.PileLog:
                    kind = LogAt(plan, plan.Passers, t, out _, out float pu) ? PropKind.Log : PropKind.None; slot = PropSlot.Arms;
                    style = DollStyle.Stand;
                    break;
                case StageBeat.LastLog: (kind, slot) = t - st.t0 < 0.9f ? (PropKind.Log, PropSlot.Arms) : (PropKind.None, PropSlot.Hand); style = DollStyle.Stand; break;
                case StageBeat.Fumble: kind = t - st.t0 < 0.15f ? PropKind.Log : PropKind.None; slot = PropSlot.Arms; style = t - st.t0 > 1f ? DollStyle.Laugh : DollStyle.Stand; break;
                case StageBeat.HopBack: style = t - st.t0 > 1f ? DollStyle.Laugh : DollStyle.Stand; kind = PropKind.None; break;
                case StageBeat.MendRoof:
                {
                    float u = t - st.t0;
                    style = u > 3.2f && u < 5.6f ? DollStyle.Hammer : DollStyle.Stand;
                    (kind, slot) = u < 2.6f ? (PropKind.Bundle, PropSlot.Hand) : u < 5.6f ? (PropKind.Hammer, PropSlot.Hand) : (PropKind.None, PropSlot.Hand);
                    break;
                }
                case StageBeat.PassTwig: kind = TwigAt(plan, st.arg, t, out float tu) && tu > 0.1f ? PropKind.Twig : PropKind.None; slot = PropSlot.Hand; style = DollStyle.Stand; break;
                case StageBeat.KeepTwig: (kind, slot) = (PropKind.Twig, PropSlot.Arms); style = DollStyle.Stand; break;
                case StageBeat.Laugh: style = DollStyle.Laugh; kind = PropKind.None; break;
                case StageBeat.SitLogs: style = DollStyle.Sit; break;
                case StageBeat.CarryBoard: style = DollStyle.Stand; break;
                case StageBeat.TapBoard: style = st.arg == 1 && t - st.t0 > 0.3f ? DollStyle.Hammer : DollStyle.Stand; (kind, slot) = st.arg == 1 ? (PropKind.Hammer, PropSlot.Hand) : (PropKind.None, PropSlot.Hand); break;
                case StageBeat.TableSit: case StageBeat.Budge: case StageBeat.PassBowl: case StageBeat.BowlsUp: case StageBeat.SongSway:
                    style = DollStyle.Sit; break;
                case StageBeat.TableEat: style = DollStyle.EatMeal; (kind, slot) = (PropKind.Bowl, PropSlot.Hand); break;
                case StageBeat.Ladle: style = DollStyle.Stand; break;
                case StageBeat.UnderTable: style = DollStyle.Stand; kind = PropKind.None; break;
                case StageBeat.BenchNap: style = DollStyle.Lie; kind = PropKind.None; break;
                case StageBeat.KneelSet:
                    style = DollStyle.Stand;
                    if (st.arg <= 1 && t - st.t0 > (st.arg == 1 ? 2.2f : 1.2f)) kind = PropKind.None; // set down (tipped into the basket at the tree)
                    break;
                default: style = DollStyle.Stand; break;
            }
        }

        // ================================================================== the pose on top

        /// <summary>The pose of this step laid over the loop's at plan time <paramref name="t"/>. <paramref name="stride"/>: the walk's phase.</summary>
        public static void Apply(ref DollPose p, MomentStagePlan plan, MomentMember m, int i, float t, float stride, bool walking)
        {
            var st = m.steps[i];
            float u = t - st.t0, len = st.Length, since = u - st.cue;
            if (walking)
            {
                switch (st.beat)
                {
                    case StageBeat.WalkOut:
                        if (st.arg == 1) p.armR = new Vector3(120f, 0f, 20f); // B7.c: the small hand up in the parent's
                        else if (st.arg == 2) p.armL = new Vector3(20f, 0f, -30f); // the parent's down to it
                        else if (st.hold == StageHold.Basket) p.armL = new Vector3(10f * Mathf.Sin(stride), 0f, -12f); // the empty basket swings in step
                        return;
                    case StageBeat.WalkHome:
                        if (st.hold == StageHold.FullBasket) { p.armL = BothFrontL; p.armR = BothFrontR; WaterBeats.Heavy(ref p, stride); }
                        if (st.arg == 1) p.armR = new Vector3(120f, 0f, 20f); else if (st.arg == 2) p.armL = new Vector3(20f, 0f, -30f);
                        return;
                    case StageBeat.CarryCareful:
                        p.armL = new Vector3(90f, 0f, -12f); p.armR = new Vector3(90f, 0f, 12f); p.lean -= 5f;
                        p.offset.y *= 0.15f; p.roll *= 0.2f; // almost no bounce: the basket never swings
                        return;
                    case StageBeat.KidCarry:
                        if (st.arg == 0) p.armR = new Vector3(70f, 0f, 25f); else p.armL = new Vector3(70f, 0f, -25f); // one hand each on the small basket between them
                        return;
                    case StageBeat.CarryBoard: p.armL = new Vector3(80f, 0f, -12f); p.armR = new Vector3(80f, 0f, 12f); return;
                    case StageBeat.UnderTable: CampScenes6.CrawlPose(ref p, u, len); return;
                    case StageBeat.HopBack: p.armL = new Vector3(70f, 0f, -20f); p.armR = new Vector3(70f, 0f, 20f); p.offset.y += 0.02f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u / 0.4f)); return;
                }
                if (st.hold is StageHold.FullBasket or StageHold.Offering) { p.armL = BothFrontL; p.armR = BothFrontR; }
                return;
            }
            switch (st.beat)
            {
                // ---- B72
                case StageBeat.HandOut:
                {
                    // lift one off the stack, turn, hand it over
                    float k = Bump(u, len, 0.25f);
                    p.lean += 12f * (1f - In(u, 0.1f, 0.3f));
                    p.armR = Lerp(new Vector3(20f, 0f, 12f), new Vector3(85f, 0f, 6f), k);
                    break;
                }
                case StageBeat.TakeBasket: p.armR = Lerp(p.armR, new Vector3(80f, 0f, 6f), Bump(u, len, 0.2f)); p.lean += 6f * Bump(u, len, 0.2f); break;
                case StageBeat.HoldBasketUp: p.armR = Lerp(p.armR, new Vector3(140f, 0f, 12f), Bump(u, len, 0.25f)); p.offset.y += 0.01f * Bump(u, len, 0.25f); break;
                case StageBeat.PointBush: p.armR = new Vector3(90f, 0f, 6f); p.lean += 4f; break;
                case StageBeat.TableSet:
                    if (since < 0f) { p.armL = BothFrontL; p.armR = BothFrontR; break; }
                    if (since < 1.2f) { float k = Bump(since, 1.2f, 0.3f); p.lean += 30f * k; p.armL = Lerp(BothFrontL, new Vector3(40f, 0f, -8f), k); p.armR = Lerp(BothFrontR, new Vector3(40f, 0f, 8f), k); }
                    break;
                case StageBeat.Tip: if (since < 0f) { p.armL = BothFrontL; p.armR = BothFrontR; } break;
                case StageBeat.ShowHands:
                {
                    float k = In(u, 0f, 0.35f);
                    p.armL = Lerp(p.armL, new Vector3(160f, 0f, -30f), k); p.armR = Lerp(p.armR, new Vector3(160f, 0f, 30f), k);
                    p.offset.y += 0.006f * k;
                    break;
                }
                case StageBeat.KidHands:
                {
                    // side by side, look, then laugh
                    float k = In(u, 0.2f, 0.3f);
                    p.armL = Lerp(p.armL, new Vector3(140f, 0f, -12f), k); p.armR = Lerp(p.armR, new Vector3(140f, 0f, 12f), k);
                    p.yaw += (st.arg == 0 ? 25f : -25f) * Bump(u - 0.4f, 0.8f, 0.2f);
                    if (u > 1.2f) { float l = Mathf.Abs(Mathf.Sin((u - 1.2f) * 14f)); p.offset.y += 0.006f * l; p.lean -= 8f * In(u, 1.2f, 0.15f); }
                    break;
                }

                // ---- B73
                case StageBeat.DropLog:
                {
                    // lean 30, the log goes down onto the heap
                    float k = Bump(u, 1f, 0.3f);
                    p.lean += 30f * k; p.armL = Lerp(BothFrontL, new Vector3(30f, 0f, -10f), k); p.armR = Lerp(BothFrontR, new Vector3(30f, 0f, 10f), k);
                    break;
                }
                case StageBeat.PassLog:
                {
                    // turn to the giver (yaw -70), take it, swing to the next (yaw +70), hand it on; waiting: turned to the giver, arms ready
                    int j = st.arg;
                    p.armL = new Vector3(45f, 0f, -14f); p.armR = new Vector3(45f, 0f, 14f);
                    if (LogAt(plan, j, t, out _, out float w))
                    {
                        float take = In(w, 0f, 0.25f), swing = In(w, 0.3f, 0.3f);
                        p.yaw += Mathf.Lerp(-70f, 70f, swing) * (j == 0 ? 0.6f : 1f);
                        p.armL = Lerp(p.armL, BothFrontL + new Vector3(10f, 0f, 0f), take); p.armR = Lerp(p.armR, BothFrontR + new Vector3(10f, 0f, 0f), take);
                        if (j == 0) p.lean += 30f * (1f - In(w, 0f, 0.3f)); // the first lifts it off the heap
                        if (w > 0.58f && w < 0.65f) p.hit = 1f;
                    }
                    else p.yaw -= 45f * In(t - st.t0, 0f, 0.3f);
                    break;
                }
                case StageBeat.PileLog:
                {
                    p.armL = new Vector3(40f, 0f, -14f); p.armR = new Vector3(40f, 0f, 14f);
                    if (LogAt(plan, plan.Passers, t, out _, out float w))
                    {
                        float push = Bump(w, 0.65f, 0.2f);
                        p.yaw -= 70f * (1f - In(w, 0.15f, 0.25f));
                        p.armL = Lerp(BothFrontL, new Vector3(60f, 0f, -10f), push); p.armR = Lerp(BothFrontR, new Vector3(60f, 0f, 10f), push);
                        p.lean += 10f * push;
                    }
                    break;
                }
                case StageBeat.Fumble:
                {
                    float k = In(u, 0f, 0.15f);
                    p.armL = Lerp(BothFrontL + new Vector3(10f, 0f, 0f), new Vector3(40f, 0f, -20f), k); p.armR = Lerp(BothFrontR + new Vector3(10f, 0f, 0f), new Vector3(40f, 0f, 20f), k);
                    if (u < 0.6f) p.lean += 10f * Bump(u, 0.6f, 0.15f);
                    break;
                }
                case StageBeat.HopBack: if (u < 1f) { p.armL = new Vector3(70f, 0f, -20f); p.armR = new Vector3(70f, 0f, 20f); } break;
                case StageBeat.MendRoof:
                {
                    // the ladder leant on (0-0.8), up two rungs (0.8-1.6), the bundle laid (1.6-2.6), three taps (3.2-5.6), down (5.6-6.4)
                    float up = In(u, 0.8f, 0.8f) * (1f - In(u, 5.6f, 0.8f));
                    p.offset.y += 0.06f * up;
                    if (u < 0.8f) p.armR = Lerp(p.armR, new Vector3(100f, 0f, 10f), Bump(u, 0.8f, 0.2f));
                    else if (u < 2.6f) p.armR = Lerp(new Vector3(60f, 0f, 10f), new Vector3(150f, 0f, 10f), In(u, 1.6f, 0.5f));
                    p.armL = new Vector3(110f, 0f, -14f) * up + p.armL * (1f - up); // a hand on the ladder
                    break;
                }
                case StageBeat.PassTwig:
                {
                    p.armR = new Vector3(40f, 0f, 14f);
                    if (TwigAt(plan, st.arg, t, out float w)) { p.yaw += Mathf.Lerp(-60f, 60f, In(w, 0.2f, 0.25f)); p.armR = new Vector3(75f, 0f, 10f); }
                    break;
                }
                case StageBeat.KeepTwig:
                    p.armL = new Vector3(60f, 0f, 20f); p.armR = new Vector3(60f, 0f, -20f); // round it
                    p.yaw += 90f * Bump(u, len, 0.25f); p.roll += 4f * Mathf.Sin(u * 9f);
                    break;
                case StageBeat.LastLog:
                {
                    float slide = In(u, 0.3f, 0.6f);
                    p.armL = Lerp(new Vector3(120f, 0f, -10f), new Vector3(90f, 0f, -10f), slide); p.armR = Lerp(new Vector3(120f, 0f, 10f), new Vector3(90f, 0f, 10f), slide);
                    if (u > 1f) { float pat = Mathf.Abs(Mathf.Sin((u - 1f) * Mathf.PI * 2.5f)) * (u < 1.8f ? 1f : 0f); p.armL = new Vector3(14f, 0f, -14f); p.armR = new Vector3(90f - 15f * pat, 0f, 8f); }
                    break;
                }
                case StageBeat.SitLogs: p.lift += PropGeo.SeatLift(PropGeo.LogsTop) * In(u, 0f, 0.3f); break;

                // ---- B74
                case StageBeat.CarryBoard: p.armL = new Vector3(80f, 0f, -12f); p.armR = new Vector3(80f, 0f, 12f); if (u < 0.6f && st.Length < 1f) { p.lean += 20f * Bump(u, 0.7f, 0.2f); } break;
                case StageBeat.TapBoard:
                    if (st.arg == 0) { float k = In(u, 0f, 0.4f); p.armL = Lerp(new Vector3(80f, 0f, -12f), new Vector3(60f, 0f, -12f), k); p.armR = Lerp(new Vector3(80f, 0f, 12f), new Vector3(60f, 0f, 12f), k); p.lean += 15f * k; }
                    break;
                case StageBeat.TableSit: case StageBeat.Budge: case StageBeat.PassBowl: case StageBeat.BowlsUp: case StageBeat.SongSway:
                {
                    p.lift += PropGeo.SeatLift(PropGeo.BenchTop);
                    if (st.beat == StageBeat.Budge && since >= 0f && since < 0.4f) { p.offset.x += 0.015f * Mathf.Sin(Mathf.PI * since / 0.4f); p.offset.y += 0.01f * Mathf.Sin(Mathf.PI * since / 0.4f); }
                    if (st.beat == StageBeat.PassBowl && since >= 0f && since < 0.9f)
                    {
                        float take = Bump(since, 0.5f, 0.15f), give = since > 0.45f ? Bump(since - 0.45f, 0.45f, 0.15f) : 0f;
                        p.yaw += -50f * take + 50f * give;
                        p.armR = Lerp(p.armR, new Vector3(75f, 0f, 8f), Mathf.Max(take, give));
                    }
                    if (st.beat == StageBeat.BowlsUp)
                    {
                        float k = st.arg == 1 ? Bump(u, len, 0.3f) : In(u, 0f, 0.35f) * (1f - In(u, 0.95f, 0.4f));
                        p.armL = Lerp(p.armL, new Vector3(st.arg == 1 ? 120f : 140f, 0f, -14f), k); p.offset.y += 0.006f * k;
                    }
                    if (st.beat == StageBeat.SongSway && since >= 0f) p.roll += 6f * Mathf.Sin(2f * Mathf.PI * 0.5f * since) * In(since, 0f, 0.5f);
                    break;
                }
                case StageBeat.TableEat: p.lift += PropGeo.SeatLift(PropGeo.BenchTop); break;
                case StageBeat.Ladle:
                {
                    float dip = Bump(u, 0.9f, 0.25f);
                    p.lean += 25f * dip; p.armR = Lerp(new Vector3(30f, 0f, 10f), new Vector3(70f, 0f, 4f), dip);
                    break;
                }
                case StageBeat.UnderTable: CampScenes6.CrawlPose(ref p, u, len); break;
                case StageBeat.BenchNap: p.lift += PropGeo.SeatLift(PropGeo.BenchTop); break;

                // ---- B75.a
                case StageBeat.LiftOffering:
                {
                    // squat (0.8) and lift it in both arms against the chest
                    float sq = Bump(u, 0.8f, 0.25f), lift = In(u, 0.45f, 0.5f);
                    p.squash *= Mathf.Lerp(1f, 0.8f, sq); p.offset.y -= 0.02f * sq;
                    p.armL = Lerp(new Vector3(40f, 0f, -12f), new Vector3(90f, 0f, -12f), lift); p.armR = Lerp(new Vector3(40f, 0f, 12f), new Vector3(90f, 0f, 12f), lift);
                    p.lean += Mathf.Lerp(20f, -5f, lift);
                    break;
                }
                case StageBeat.CarryCareful: p.armL = new Vector3(90f, 0f, -12f); p.armR = new Vector3(90f, 0f, 12f); p.lean -= 5f; break;
                case StageBeat.KidCarry:
                {
                    float sq = st.Length > 0.9f && u < 0.9f ? Bump(u, 0.9f, 0.25f) : 0f; // the little squat to lift it
                    p.squash *= Mathf.Lerp(1f, 0.85f, sq);
                    if (st.arg == 0) p.armR = new Vector3(70f, 0f, 25f); else p.armL = new Vector3(70f, 0f, -25f);
                    break;
                }
                case StageBeat.KneelSet:
                {
                    // kneel (B4), lower it (lean 35); arg 1: lift the cloth and tip it into the basket at the tree; 2+: a kid drops a berry in
                    float kn = In(u, 0f, 0.35f) * (1f - In(u, len - 0.4f, 0.4f));
                    CampScenes5.KneelPose(ref p, kn, 1f);
                    if (st.arg == 0) { float d = In(u, 0.3f, 0.6f); p.armL = Lerp(new Vector3(90f, 0f, -12f), new Vector3(40f, 0f, -12f), d); p.armR = Lerp(new Vector3(90f, 0f, 12f), new Vector3(40f, 0f, 12f), d); p.lean += 5f * d; }
                    else if (st.arg == 1)
                    {
                        float cloth = Bump(u - 0.5f, 0.8f, 0.2f), tip = Bump(u - 1.2f, 1f, 0.3f);
                        p.armL = Lerp(new Vector3(90f, 0f, -12f), new Vector3(55f, 0f, -20f), In(u, 0.4f, 0.4f));
                        p.armR = Lerp(new Vector3(90f, 0f, 12f), new Vector3(80f, 0f, 6f), Mathf.Max(cloth, tip));
                        p.roll += 10f * tip;
                    }
                    else { float drop = Bump(u - 0.6f, 0.8f, 0.2f); p.armR = Lerp(new Vector3(40f, 0f, 14f), new Vector3(60f, 0f, 6f), drop); }
                    break;
                }
                case StageBeat.Still: p.lean += 4f; p.armL = new Vector3(8f, 0f, -8f); p.armR = new Vector3(8f, 0f, 8f); break;
                case StageBeat.LookBack: p.yaw += 150f * Bump(u, len, 0.3f); break;
                case StageBeat.TurnHead: { float k = Bump(u - 0.1f, len - 0.1f, 0.25f); p.armR = Lerp(p.armR, new Vector3(110f, 0f, 20f), k); p.lean += 6f * k; break; }
                default:
                    if (st.hold is StageHold.FullBasket or StageHold.Offering) { p.armL = BothFrontL; p.armR = BothFrontR; }
                    break;
            }
        }
    }
}
