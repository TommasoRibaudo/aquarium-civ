// #370: the staged moments' view half (pure; runs in Unity's View.Tests and in Tools/AnimBench.Tests).
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class MomentBeatsTests
    {
        static readonly MomentGeometry Geo = new MomentGeometry { fireX = 0f, fireZ = 0.2f, glassX = 0f, glassZ = -1.95f, nx = 0f, nz = -1f, halfX = 3f, halfZ = 2.2f };

        static MomentStagePlan Plan(MomentKind kind)
        {
            var c = new MomentCast { a = MomentPerson.Of(1, false, 0.9f, 0.9f, 3f), hasA = true, b = MomentPerson.Of(2, false, -0.6f, 0.75f, 3f), hasB = true,
                officiant = MomentPerson.Of(3, false, 0.35f, 0.65f, 2f), hasOfficiant = true, babyKey = 99 };
            for (int i = 0; i < 6; i++) c.grown.Add(MomentPerson.Of(10 + i, false, -1.2f + 0.5f * i, 0.5f, 3f));
            for (int i = 0; i < 2; i++) c.kids.Add(MomentPerson.Of(-1 - i, true, 0.6f - 1.2f * i, -0.6f, 3f));
            var plan = MomentStagePlan.Build(kind, c, Geo, 370);
            Assert.IsNotNull(plan, MomentStagePlan.LastWhyNot);
            return plan;
        }

        static (MomentMember m, int i) StepOf(MomentStagePlan plan, StageBeat beat, MomentRole? role = null)
        {
            foreach (var m in plan.members)
                if (role == null || m.role == role)
                    for (int i = 0; i < m.steps.Count; i++) if (m.steps[i].beat == beat) return (m, i);
            Assert.Fail($"no {beat} step");
            return default;
        }

        [Test]
        public void TheRing_IsTheKitsRingGroup_StepsInEvery8Beats_AndHopsInStep()
        {
            var plan = Plan(MomentKind.Festival);
            var rg = MomentBeats.RingOf(plan);
            Assert.IsNotNull(rg);
            Assert.AreEqual(plan.ringKeys.Count, rg.Count);
            var (m, i) = StepOf(plan, StageBeat.Ring);
            float t = plan.ringT0 + 1.1f; // not stepping in
            var at = MomentBeats.Spot(plan, m, i, t, out var face);
            Assert.AreEqual(0f, (at - rg.Cue(rg.IndexOf(m.key), t).target).magnitude, 1e-4f, "the ring is the kit's");
            var centre = new Vector2(plan.centreX, plan.centreZ);
            Assert.AreEqual(plan.ringRadius, (at - centre).magnitude, 1e-3f);
            Assert.AreEqual(0f, (face.Value - centre).magnitude, 1e-5f, "dancers face the middle");
            float inPeak = plan.ringT0 + 8 * MomentStagePlan.RingBeat - MomentStagePlan.RingBeat; // the middle of the step-in
            var inAt = MomentBeats.Spot(plan, m, i, inPeak, out _);
            Assert.AreEqual(plan.ringRadius * (1f - MomentBeats.RingStepIn), (inAt - centre).magnitude, 0.01f, "steps in to 0.6 r");
            // In step on purpose (B2.e): two dancers hop the same at the same time.
            var other = plan.members.First(x => x.key != m.key && x.steps.Any(s => s.beat == StageBeat.Ring));
            int oi = other.steps.FindIndex(s => s.beat == StageBeat.Ring);
            var p1 = DollPose.Rest; var p2 = DollPose.Rest;
            MomentBeats.Apply(ref p1, plan, m, i, t + 0.13f, out _);
            MomentBeats.Apply(ref p2, plan, other, oi, t + 0.13f, out _);
            Assert.AreEqual(p1.offset.y, p2.offset.y, 1e-5f);
            Assert.GreaterOrEqual(Mathf.Abs(p1.armR.z), 90f, "arms out so the hands meet");
            float most = 0f;
            for (float s = 0f; s < 0.5f; s += 0.02f) { var q = DollPose.Rest; MomentBeats.Apply(ref q, plan, m, i, t + s, out _); most = Mathf.Max(most, q.offset.y); }
            Assert.GreaterOrEqual(most, 0.025f, "a hop that reads (≥ 0.025 u)");
        }

        [Test]
        public void TheOneShots_ReturnToTheLoop_AndOnlyTheThrowAndThePopHit()
        {
            var beats = new[] { MomentBeats.Cheer, MomentBeats.Bow, MomentBeats.Give, MomentBeats.Take, MomentBeats.SetDown, MomentBeats.Toss, MomentBeats.GoldPop, MomentBeats.Point, MomentBeats.Hug };
            var under = DollPose.Rest;
            foreach (var b in beats)
            {
                var end = b.Pose(under, b.Duration + 0.01f);
                if (b == MomentBeats.Point) { Assert.AreEqual(1f, end.squash, 1e-4f); continue; }
                Assert.AreEqual(under.lean, end.lean, 1e-4f); Assert.AreEqual(under.squash, end.squash, 1e-4f);
                Assert.AreEqual(0f, (under.armR - end.armR).magnitude, 1e-3f, "back to the loop's arms");
            }
            Assert.Greater(MomentBeats.Toss.ContactAt, 0f, "the throw hits the fire");
            Assert.Greater(MomentBeats.GoldPop.ContactAt, 0f, "the pop: gold sparks");
            foreach (var b in new[] { MomentBeats.Cheer, MomentBeats.Bow, MomentBeats.Give, MomentBeats.Take, MomentBeats.SetDown, MomentBeats.Point, MomentBeats.Join, MomentBeats.Hug, MomentBeats.LiftBaby, MomentBeats.Cradle, MomentBeats.Jump })
                Assert.Less(b.ContactAt, 0f, "no puff for a gesture");
            // Every contact is held (P8).
            foreach (var b in new[] { MomentBeats.Toss, MomentBeats.GoldPop })
            {
                int c = Enumerable.Range(0, b.Count).First(k => b[k].phase == BeatPhase.Contact);
                Assert.AreEqual(BeatPhase.Hold, b[c + 1].phase);
                Assert.GreaterOrEqual(b[c + 1].seconds, 0.1f);
            }
        }

        [Test]
        public void TheToss_HitsOnce_OnTheSecondStep_NotInTheWindUp()
        {
            var plan = Plan(MomentKind.Festival);
            var (lead, wi) = StepOf(plan, StageBeat.TossWind);
            int ti = wi + 1;
            Assert.AreEqual(StageBeat.Toss, lead.steps[ti].beat);
            int hits = 0; bool was = false;
            for (float t = lead.steps[wi].t0; t < lead.steps[ti].t1; t += 1f / 60f)
            {
                int idx = t < lead.steps[ti].t0 ? wi : ti;
                var p = DollPose.Rest;
                MomentBeats.Apply(ref p, plan, lead, idx, t, out _);
                bool h = p.hit > 0.5f;
                if (h && !was) hits++;
                if (idx == wi) Assert.IsFalse(h, "no hit in the wind-up");
                was = h;
            }
            Assert.AreEqual(1, hits);
        }

        [Test]
        public void TheAisleCheer_WaitsForTheCouple()
        {
            var plan = Plan(MomentKind.Wedding);
            var guests = plan.members.Where(m => m.steps.Any(s => s.beat == StageBeat.AisleCheer)).ToList();
            var late = guests.OrderByDescending(m => m.steps.First(s => s.beat == StageBeat.AisleCheer).cue).First();
            int i = late.steps.FindIndex(s => s.beat == StageBeat.AisleCheer);
            var st = late.steps[i];
            var before = DollPose.Rest;
            MomentBeats.Apply(ref before, plan, late, i, st.t0 + st.cue - 0.05f, out _);
            Assert.AreEqual(0f, (before.armR - DollPose.Rest.armR).magnitude, 1e-4f, "still before the couple reaches them");
            var up = DollPose.Rest;
            MomentBeats.Apply(ref up, plan, late, i, st.t0 + st.cue + 0.4f, out _);
            Assert.Greater(up.armR.x, 120f, "arms up as they pass");
        }

        [Test]
        public void TheBaby_GoesUpHigh_AndComesBackToTheArms()
        {
            var plan = Plan(MomentKind.Newborn);
            var (m, i) = StepOf(plan, StageBeat.LiftBaby, MomentRole.Parent);
            var st = m.steps[i];
            float Lift(float s) { var p = DollPose.Rest; MomentBeats.Apply(ref p, plan, m, i, st.t0 + s, out float k); return k; }
            Assert.AreEqual(0f, Lift(0.1f), 1e-4f, "the wind-up lowers it first");
            Assert.Greater(Lift(1.5f), 0.95f, "held high");
            var held = DollPose.Rest; MomentBeats.Apply(ref held, plan, m, i, st.t0 + 1.5f, out _);
            Assert.Greater(held.armL.x, 150f); Assert.Greater(held.armR.x, 150f);
            Assert.Less(Lift(st.Length - 0.01f), 0.02f, "back in the arms by the end");
        }

        [Test]
        public void TheCouple_TurnRoundEachOther_InTheMiddle()
        {
            var plan = Plan(MomentKind.Wedding);
            var couple = plan.members.Where(m => m.role == MomentRole.Couple).ToList();
            float t = plan.ringT0 + 3f;
            var a = MomentBeats.Spot(plan, couple[0], couple[0].steps.FindIndex(s => s.beat == StageBeat.Centre), t, out var fa);
            var b = MomentBeats.Spot(plan, couple[1], couple[1].steps.FindIndex(s => s.beat == StageBeat.Centre), t, out _);
            var centre = new Vector2(plan.centreX, plan.centreZ);
            Assert.AreEqual(2f * MomentBeats.CentreOrbit, (a - b).magnitude, 1e-4f, "opposite each other on the orbit");
            Assert.AreEqual(0f, (fa.Value - b).magnitude, 1e-4f, "facing each other");
            Assert.Less((a - centre).magnitude, plan.ringRadius * 0.5f, "inside the ring");
        }
    }
}
