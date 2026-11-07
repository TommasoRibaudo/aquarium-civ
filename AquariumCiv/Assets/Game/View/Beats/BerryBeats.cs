// #361 B5 berries: the poses of picking, digging roots and a kid's tasting, as beats (pure: Mathf and DollPose, so AnimBench compiles it).
// Spec: docs/design/animation-specs.md B5.a-c. The view side (arcs, basket dots, the bush answering) is View/BerryPickView.cs.
using System.Collections.Generic;
using AquariumCiv.Life;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>One loop of berry-and-root beats. Each style loops a "group" beat on the doll's own pose clock (so neighbours are never in step).</summary>
    public static class BerryMotion
    {
        public const int PicksPerGroup = 6, BasketMax = 5;
        public const float PickCycle = 2.5f, RootCycle = 4f, TasteCycle = 2.2f;
        /// <summary>The block's last moment: the basket is held up (§B5.a end beat) before the walk to the stash.</summary>
        public const float HoldUpSeconds = 0.9f;
        /// <summary>From the pull's Contact to the root leaving the hand (the held-up root's Hold).</summary>
        public const float RootHeldSeconds = 0.96f;
        /// <summary>From the pull's Contact to the root coming up out of the ground into the hand.</summary>
        public const float RootUpAfterSeconds = 0.16f;
        /// <summary>Look-fix: a tasted berry rides in the kid's hand from the contact until the hand is at the mouth (the Follow step).</summary>
        public const float TasteToMouthSeconds = 0.5f;

        static readonly Vector3 BasketArm = new Vector3(28f, 0f, -30f); // look-fix: out from the hip (z -12 hid the basket behind the body)
        /// <summary>Look-fix (AnimLab, batch 1): the picker's head went into the bush at the spec's 32-40 degree lean from 0.17 u off its
        /// centre. The lean is scaled by this and the reaching arm pitched up by the difference, so the hand still reaches the bush.</summary>
        public const float PickLeanScale = 0.55f;
        /// <summary>The left arm pose the basket is built upright for (PegDollView.HoldInLeft).</summary>
        public static Vector3 BasketArmL => BasketArm;

        // ---- the end beat at the stash (B5.a, #361 follow-up): tip the basket onto the pile. Times are seconds since the doll stood still at the pile.
        /// <summary>The whole tip: lift, tilt, pour, settle (the planner's TipLoad block is a little longer).</summary>
        public const float TipSeconds = 1.4f;
        /// <summary>The berries leave the rim at <see cref="TipPourAt"/> + i * <see cref="TipGap"/>, and fly <see cref="TipFlight"/>: the first lands at <see cref="TipContactAt"/> (the pose's hit).</summary>
        public const float TipPourAt = 0.5f, TipGap = 0.06f, TipFlight = 0.25f, TipContactAt = TipPourAt + TipFlight;
        public const int TipMinBerries = 3, TipMaxBerries = 5;
        /// <summary>The basket rolled forward this far (degrees) at the pour; the view turns the prop (it is on the chest, not on an arm).</summary>
        public const float TipRollDegrees = 100f, TipLiftHeight = 0.03f;

        static readonly Beat pickGroup, rootGroup, tasteGroup, holdUp, tipBeat;
        static readonly DollPose tipUnder;
        static readonly float[] rootContacts, pickContacts, tasteContacts;
        static readonly DollPose pickUnder, rootUnder, tasteUnder;

        static BerryMotion()
        {
            pickUnder = DollPose.Rest; pickUnder.squash = 0.76f; pickUnder.lean = 32f;
            pickUnder.armL = BasketArm; pickUnder.armR = new Vector3(40f, 0f, 8f);
            rootUnder = pickUnder; rootUnder.squash = 0.7f; rootUnder.lean = 35f;
            tasteUnder = DollPose.Rest; tasteUnder.armR = new Vector3(30f, 0f, 18f); tasteUnder.lean = -3f;

            var s = new List<BeatStep>();
            var times = new List<float>();
            for (int i = 0; i < PicksPerGroup; i++) AddPickCycle(s, i == PicksPerGroup - 1);
            pickGroup = Make(s, times); pickContacts = times.ToArray();

            s.Clear(); times.Clear();
            Paw(s, true); Paw(s, false); Paw(s, true);
            // the pull: grip the root in the ground (contact: the dig spot answers with a big puff of dust), strain, then heave it up and hold it high
            s.Add(Beat.Step(BeatPhase.Antic, 0.2f, Ease.Out, PoseKey.Hold.ArmR(85, 0, 8).Lean(40).Squash(0.66f)));
            s.Add(Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold));
            s.Add(Beat.Step(BeatPhase.Hold, 0.1f, Ease.Linear, PoseKey.Hold));
            s.Add(Beat.Step(BeatPhase.Act, 0.3f, Ease.Back, PoseKey.Hold.ArmR(112, 0, 14).Lean(-12).Squash(0.95f))); // look-fix: held up at the face, not hoisted overhead like a rock
            s.Add(Beat.Step(BeatPhase.Hold, 0.5f, Ease.Linear, PoseKey.Hold));
            s.Add(Beat.Step(BeatPhase.Follow, 0.25f, Ease.Snap, PoseKey.Hold.ArmR(75, 0, 8).Lean(30).Squash(0.7f)));
            s.Add(Beat.Step(BeatPhase.Settle, 0.5f, Ease.Smooth, PoseKey.Base));
            float used = 0f; foreach (var st in s) used += st.seconds;
            s.Add(Beat.Step(BeatPhase.Settle, RootCycle - used, Ease.Linear, PoseKey.Hold));
            rootGroup = Make(s, times); rootContacts = times.ToArray();

            s.Clear(); times.Clear();
            for (int i = 0; i < 3; i++)
            {
                s.Add(Beat.Step(BeatPhase.Antic, 0.1f, Ease.Out, PoseKey.Hold.ArmR(60, 0, 18)));
                s.Add(Beat.Step(BeatPhase.Act, 0.4f, Ease.Smooth, PoseKey.Hold.ArmR(100, 0, 18).Lean(8)));
                s.Add(Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold));
                // look-fix: a big arc up to the mouth and the head tipped back (the whole doll leans back): it reads from the default camera
                s.Add(Beat.Step(BeatPhase.Follow, 0.5f, Ease.Back, PoseKey.Hold.ArmR(158, 0, 6).Lean(-16)));
                s.Add(Beat.Step(BeatPhase.Hold, 0.6f, Ease.Linear, PoseKey.Hold)); // chewing (a bob rides on it)
                if (i < 2) s.Add(Beat.Step(BeatPhase.Settle, 0.54f, Ease.Smooth, PoseKey.Base));
                else
                {
                    s.Add(Beat.Step(BeatPhase.Settle, 0.2f, Ease.Smooth, PoseKey.Base));
                    s.Add(Beat.Step(BeatPhase.EndBeat, 0.15f, Ease.Out, PoseKey.Hold.Arms(0, 90).Offset(0, 0.03f, 0)));
                    s.Add(Beat.Step(BeatPhase.EndBeat, 0.15f, Ease.In, PoseKey.Hold.Offset(0, 0, 0)));
                    s.Add(Beat.Step(BeatPhase.Settle, 0.34f, Ease.Smooth, PoseKey.Base));
                }
            }
            tasteGroup = Make(s, times); tasteContacts = times.ToArray();

            // the tip: the basket comes up (arms 95), tips forward (lean 35, arms 80), shakes out, the first berry lands (contact), the rest follow, then back to the carry
            tipUnder = DollPose.Rest; tipUnder.lean = 3f; tipUnder.armL = new Vector3(62f, 0f, -4f); tipUnder.armR = new Vector3(62f, 0f, 4f);
            tipBeat = new Beat(
                Beat.Step(BeatPhase.Antic, 0.25f, Ease.Out, PoseKey.Hold.Arms(95, 4).Lean(-4)),
                Beat.Step(BeatPhase.Act, 0.25f, Ease.Smooth, PoseKey.Hold.Arms(80, 4).Lean(35)),
                Beat.Step(BeatPhase.Act, TipContactAt - 0.5f, Ease.Linear, PoseKey.Hold.Lean(38)),
                Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold),
                Beat.Step(BeatPhase.Hold, 0.25f, Ease.Linear, PoseKey.Hold),
                Beat.Step(BeatPhase.Settle, TipSeconds - TipContactAt - 0.31f, Ease.Smooth, PoseKey.Base));

            holdUp = new Beat(
                Beat.Step(BeatPhase.EndBeat, 0.3f, Ease.Back, PoseKey.Hold.ArmL(120, 0, -12).Lean(10).Squash(0.9f).ArmR(40, 0, 8)),
                Beat.Step(BeatPhase.EndBeat, 0.6f, Ease.Linear, PoseKey.Hold));
        }

        static Beat Make(List<BeatStep> steps, List<float> contactTimes)
        {
            float t = 0f;
            foreach (var st in steps)
            {
                if (st.phase == BeatPhase.Contact) contactTimes.Add(t);
                t += st.seconds;
            }
            return new Beat(steps.ToArray());
        }

        static void AddPickCycle(List<BeatStep> s, bool last)
        {
            s.Add(Beat.Step(BeatPhase.Antic, 0.15f, Ease.Out, PoseKey.Hold.ArmR(18, 0, 8).Lean(28)));
            s.Add(Beat.Step(BeatPhase.Act, 0.35f, Ease.Smooth, PoseKey.Hold.ArmR(110, 0, 8).Lean(38)));
            s.Add(Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold));
            s.Add(Beat.Step(BeatPhase.Follow, 0.12f, Ease.Snap, PoseKey.Hold.ArmR(70, 0, 8).Lean(34)));
            s.Add(Beat.Step(BeatPhase.Hold, 0.25f, Ease.Linear, PoseKey.Hold));
            s.Add(Beat.Step(BeatPhase.Settle, 0.4f, Ease.Smooth, PoseKey.Hold.ArmR(55, 0, 8).Lean(32).Yaw(-7)));
            if (!last)
            {
                s.Add(Beat.Step(BeatPhase.Settle, 0.5f, Ease.Smooth, PoseKey.Hold.ArmR(80, 0, 8).Yaw(6)));
                s.Add(Beat.Step(BeatPhase.Settle, 0.67f, Ease.Smooth, PoseKey.Hold.ArmR(40, 0, 8).Lean(32).Yaw(0)));
            }
            else
            {
                // the basket is full: held up a moment (the end beat), then back down to the next round
                s.Add(Beat.Step(BeatPhase.EndBeat, 0.35f, Ease.Back, PoseKey.Hold.ArmL(120, 0, -12).Lean(10).Squash(0.9f).Yaw(0).ArmR(40, 0, 8)));
                s.Add(Beat.Step(BeatPhase.EndBeat, 0.6f, Ease.Linear, PoseKey.Hold));
                s.Add(Beat.Step(BeatPhase.Settle, 0.35f, Ease.Smooth, PoseKey.Hold.ArmL(BasketArm.x, 0, BasketArm.z).Lean(32).Squash(0.76f)));
            }
        }

        static void Paw(List<BeatStep> s, bool hits)
        {
            s.Add(Beat.Step(BeatPhase.Act, 0.14f, Ease.Snap, PoseKey.Hold.ArmR(90, 0, 8).Lean(38)));
            if (hits) s.Add(Beat.Step(BeatPhase.Contact, 0.06f, Ease.Linear, PoseKey.Hold));
            s.Add(Beat.Step(BeatPhase.Hold, 0.1f, Ease.Linear, PoseKey.Hold));
            s.Add(Beat.Step(BeatPhase.Follow, 0.2f, Ease.Smooth, PoseKey.Hold.ArmR(40, 0, 8).Lean(35)));
        }

        // ---------------------------------------------------------------- what the styles are

        public static bool IsBerryStyle(DollStyle s) => s is DollStyle.PickBerry or DollStyle.DigRoot or DollStyle.TasteBerry or DollStyle.TipBasket;

        /// <summary>The basket rides in the left hand while the right hand picks or digs (PegDollView.HoldInLeft).</summary>
        public static bool BasketInLeft(DollStyle s) => s is DollStyle.PickBerry or DollStyle.DigRoot or DollStyle.TipBasket;

        /// <summary>The move for an activity at a place: a gatherer at a real bush picks berries, at the dry stream bed digs roots, a kid
        /// eating at a bush stands tasting. Anything else keeps <paramref name="style"/>.</summary>
        public static DollStyle Retarget(DollStyle style, ActivityKind kind, PlaceKind place)
        {
            if (kind == ActivityKind.Gather && style == DollStyle.Pick)
            {
                if (place == PlaceKind.BushCell) return DollStyle.PickBerry;
                if (place == PlaceKind.Stream) return DollStyle.DigRoot;
            }
            else if (kind == ActivityKind.Eat && place == PlaceKind.BushCell) return DollStyle.TasteBerry;
            return style;
        }

        // ---------------------------------------------------------------- the poses

        // ---------------------------------------------------------------- the tip at the stash

        /// <summary>The pose of the tip <paramref name="since"/> seconds after the doll stood still at the pile (held on its last frame after the end).</summary>
        public static DollPose TipPose(float since) => tipBeat.Pose(tipUnder, Mathf.Max(0f, since));

        /// <summary>The seconds into the tip of the beat's contact (equal to <see cref="TipContactAt"/>).</summary>
        public static float TipContact => tipBeat.ContactAt;

        /// <summary>How far the basket is rolled forward (0..<see cref="TipRollDegrees"/>): up with the lift, over at the pour, back at the end.</summary>
        public static float TipRoll(float since)
        {
            float on = Easing.Apply(Ease.Smooth, (since - 0.25f) / 0.25f);
            float off = Easing.Apply(Ease.Smooth, (since - (TipSeconds - 0.4f)) / 0.4f);
            return TipRollDegrees * Mathf.Clamp01(on - off);
        }

        /// <summary>How far the basket is lifted off the chest (0..1 of <see cref="TipLiftHeight"/>).</summary>
        public static float TipLift(float since)
        {
            float up = Easing.Apply(Ease.Out, since / 0.25f);
            float down = Easing.Apply(Ease.Smooth, (since - (TipSeconds - 0.4f)) / 0.4f);
            return Mathf.Clamp01(up - down);
        }

        /// <summary>How many berries pour (3..5): the basket's count, but never a tip of nothing.</summary>
        public static int TipBerries(int inBasket) => Mathf.Clamp(inBasket, TipMinBerries, TipMaxBerries);

        /// <summary>When berry <paramref name="i"/> leaves the rim, seconds into the tip.</summary>
        public static float TipLaunchAt(int i) => TipPourAt + i * TipGap;

        /// <summary>The pose of a berry style at the doll's pose clock <paramref name="t"/>. <paramref name="progress"/> (0..1) and
        /// <paramref name="since"/> (real seconds) tell how long the block still has: the last <see cref="HoldUpSeconds"/> of a pick hold the basket up.</summary>
        public static void Evaluate(DollStyle style, ref DollPose p, float t, float progress, float since)
        {
            switch (style)
            {
                case DollStyle.PickBerry:
                {
                    p = pickGroup.Pose(pickUnder, Mathf.Repeat(t, pickGroup.Duration));
                    EndOfBlock(ref p, pickUnder, progress, since);
                    if (p.lean > 0f) { float cut = p.lean * (1f - PickLeanScale); p.lean -= cut; p.armR.x += cut; } // look-fix: the hand reaches, the head stays out
                    break;
                }
                case DollStyle.DigRoot:
                {
                    p = rootGroup.Pose(rootUnder, Mathf.Repeat(t, rootGroup.Duration));
                    EndOfBlock(ref p, rootUnder, progress, since);
                    break;
                }
                case DollStyle.TipBasket:
                    p = TipPose(since);
                    break;
                case DollStyle.TasteBerry:
                {
                    float c = Mathf.Repeat(t, tasteGroup.Duration);
                    p = tasteGroup.Pose(tasteUnder, c);
                    if (tasteGroup.Sample(c).phase == BeatPhase.Hold) p.offset.y += Mathf.Abs(Mathf.Sin(c * Mathf.PI * 8f)) * 0.022f;
                    break;
                }
            }
        }

        /// <summary>The block's last <see cref="HoldUpSeconds"/>: the basket is lifted and held (the end beat) over whatever the loop was doing.</summary>
        static void EndOfBlock(ref DollPose p, in DollPose under, float progress, float since)
        {
            float remaining = progress > 0.02f ? since * (1f - progress) / progress : 99f;
            float k = HoldUpSeconds - remaining;
            if (k <= 0f) return;
            var end = holdUp.Pose(under, k);
            float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.2f));
            float hit = w > 0.5f ? 0f : p.hit;
            p = Mix(p, end, w); p.hit = hit;
        }

        static DollPose Mix(in DollPose a, in DollPose b, float k)
        {
            var p = b;
            p.offset = Vector3.LerpUnclamped(a.offset, b.offset, k);
            p.yaw = Mathf.LerpAngle(a.yaw, b.yaw, k);
            p.lean = Mathf.Lerp(a.lean, b.lean, k);
            p.roll = Mathf.Lerp(a.roll, b.roll, k);
            p.squash = Mathf.Lerp(a.squash, b.squash, k);
            p.armL = Vector3.Lerp(a.armL, b.armL, k);
            p.armR = Vector3.Lerp(a.armR, b.armR, k);
            return p;
        }

        // ---------------------------------------------------------------- for the view: where in the loop a hit is

        public static float GroupSeconds(DollStyle style) => style switch
        {
            DollStyle.PickBerry => pickGroup.Duration, DollStyle.DigRoot => rootGroup.Duration, DollStyle.TasteBerry => tasteGroup.Duration, _ => 1f,
        };

        /// <summary>Which contact of the loop a hit at pose clock <paramref name="t"/> is (0-based; the hit frame is just after the contact starts).</summary>
        public static int ContactIndex(DollStyle style, float t)
        {
            var times = style == DollStyle.PickBerry ? pickContacts : style == DollStyle.DigRoot ? rootContacts : tasteContacts;
            float c = Mathf.Repeat(t, GroupSeconds(style));
            int idx = times.Length - 1;
            for (int i = 0; i < times.Length; i++) if (times[i] <= c + 0.001f) idx = i;
            return idx;
        }

        /// <summary>The pull is the last contact of the digging loop (the big dust, the root held up).</summary>
        public static bool IsRootPull(float t) => ContactIndex(DollStyle.DigRoot, t) == rootContacts.Length - 1;
    }
}
