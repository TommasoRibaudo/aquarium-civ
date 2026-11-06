// #363 B7 talk and pairs: pure tests (NUnit 3 classic asserts; UnityEngine maths only), so Tools/AnimBench runs them too.
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class PairBeatsTests
    {
        static DollPose Talked(in TalkState s, float amp = 1f, bool busy = false)
        {
            var p = DollPose.Rest;
            PairBeats.Talk(ref p, s, amp, busy);
            return p;
        }

        static TalkState State(bool talker, int beat, float x, float laugh = -1f) => new TalkState { talker = talker, beat = beat, x = x, period = PairBeats.TalkBeat, laugh = laugh };

        [Test]
        public void Listener_NodsOnTheTalkersPeak_ByNodDelay()
        {
            float peak = PairBeats.PeakShare * PairBeats.TalkBeat;
            Assert.AreEqual(PairBeats.TalkLean, Talked(State(false, 0, peak + PairBeats.NodDelay - 0.02f)).lean, 0.2f, "flat just before the nod");
            Assert.AreEqual(PairBeats.NodLean, Talked(State(false, 0, peak + PairBeats.NodDelay + PairBeats.NodSeconds * 0.5f)).lean, 0.2f, "deepest mid-nod");
            Assert.AreEqual(PairBeats.TalkLean, Talked(State(false, 0, peak + PairBeats.NodDelay + PairBeats.NodSeconds + 0.02f)).lean, 0.2f, "back up");
            Assert.That(PairBeats.NodDelay, Is.InRange(0.1f, 0.25f));
            Assert.That(PairBeats.NodLean - PairBeats.TalkLean, Is.GreaterThanOrEqualTo(12f), "a nod you can see at 30 px");
        }

        [Test]
        public void Talker_BobsAtThePeak_AndTurnsAnOpenHandEverySecondBeat()
        {
            float peak = PairBeats.PeakShare * PairBeats.TalkBeat;
            Assert.AreEqual(PairBeats.BobHeight, Talked(State(true, 0, peak)).offset.y, 1e-4f);
            Assert.That(PairBeats.BobHeight, Is.GreaterThanOrEqualTo(0.025f));
            float calm = Talked(State(true, 0, 0f)).armR.x, open = Talked(State(true, 1, 0.35f)).armR.x;
            Assert.That(open, Is.GreaterThanOrEqualTo(70f), "the open hand");
            Assert.That(open - calm, Is.GreaterThanOrEqualTo(40f), "an arm swing of 40 degrees or more");
            Assert.That(Talked(State(true, 1, 0.35f)).armR.z, Is.GreaterThanOrEqualTo(30f), "turned out");
            Assert.AreEqual(14f, Talked(State(true, 1, 1.2f)).armR.z, 1f, "the hand is back by the end of the beat");
            // a hand that holds a flower stays on it
            var busy = DollPose.Rest; busy.armR = DollMotion.HoldArm;
            PairBeats.Talk(ref busy, State(true, 1, 0.35f), 1f, true);
            Assert.AreEqual(DollMotion.HoldArm, busy.armR);
        }

        [Test]
        public void TheLastSecondAndAHalfIsASharedLaugh_BothBob()
        {
            var a = PairBeats.TalkAt(1, 2, 50f, 1.0f); var b = PairBeats.TalkAt(2, 1, 50f, 1.0f);
            Assert.That(a.laugh, Is.GreaterThanOrEqualTo(0f)); Assert.That(b.laugh, Is.GreaterThanOrEqualTo(0f));
            Assert.Less(PairBeats.TalkAt(1, 2, 50f, PairBeats.LaughSeconds + 0.5f).laugh, 0f, "not before the last 1.2 s");
            float bobA = 0f, bobB = 0f;
            for (float l = 0.3f; l < 1.2f; l += 0.01f)
            {
                var sa = a; sa.laugh = l; var sb = b; sb.laugh = l;
                bobA = Mathf.Max(bobA, Talked(sa).offset.y); bobB = Mathf.Max(bobB, Talked(sb).offset.y);
                Assert.AreEqual(-8f, Talked(sa).lean, 0.5f);
                Assert.AreEqual(40f, Talked(sb).armL.x, 0.5f, "arms to the belly");
            }
            Assert.That(bobA, Is.GreaterThanOrEqualTo(0.018f)); Assert.That(bobB, Is.GreaterThanOrEqualTo(0.018f));
        }

        [Test]
        public void ARoleChange_NeverSnapsAnArm()
        {
            // across a whole chat the right arm never jumps more than a few degrees between two frames 1/30 s apart (the open hand, 56 degrees in 0.18 s, is the fastest move; a snap from the talking arm to the resting one would be 48 in one frame)
            float worst = 0f; Vector3 last = Vector3.zero; bool first = true;
            for (float t = 0f; t < 40f; t += 1f / 30f)
            {
                var p = Talked(PairBeats.TalkAt(7, 9, t, 99f));
                if (!first) worst = Mathf.Max(worst, Mathf.Abs(p.armR.x - last.x));
                last = p.armR; first = false;
            }
            Assert.That(worst, Is.LessThan(20f), "worst one-frame arm jump " + worst);
        }

        [Test]
        public void TalkTurns_AreOpposite_AndNeighbouringChatsNeverBobInStep()
        {
            for (float t = 0f; t < 20f; t += 0.37f)
            {
                var a = PairBeats.TalkAt(3, 8, t, 99f); var b = PairBeats.TalkAt(8, 3, t, 99f);
                Assert.AreNotEqual(a.talker, b.talker, "one talks, the other listens at t=" + t);
                Assert.AreEqual(a.beat, b.beat); Assert.AreEqual(a.x, b.x, 1e-4f, "one shared clock");
            }
            // two unrelated chats are within 0.15 s of each other 0.3/1.3 = 23 % of the time by chance; each pair has its own period (x0.92 to 1.08) and phase, so they drift
            int inStep = 0, total = 0;
            for (int n = 0; n < 60; n++)
                for (float t = 2f; t < 30f; t += 4.1f)
                {
                    var p = PairBeats.TalkAt(100 + n * 2, 101 + n * 2, t, 99f);
                    var q = PairBeats.TalkAt(300 + n * 2, 301 + n * 2, t, 99f);
                    float pa = p.x / p.period, qa = q.x / q.period; // where each chat is in its beat
                    float d = Mathf.Abs(Mathf.DeltaAngle(pa * 360f, qa * 360f)) / 360f * PairBeats.TalkBeat;
                    total++; if (d < Desync.InStepWindow) inStep++;
                }
            Assert.That(inStep / (float)total, Is.LessThan(0.3f), $"{inStep} of {total} neighbouring chats within 0.15 s of one another");
        }

        [Test]
        public void HandOver_TheThingIsInExactlyOnePairOfHands_AndTheHandsMeetAtTheContact()
        {
            var ev = PairStage.HandOver(5, 10f, 1, 2, new Vector2(0f, 0f), new Vector2(0.4f, 0f), PropKind.Spear);
            Assert.NotNull(ev);
            Assert.AreEqual(PairBeats.Giver.ContactAt, PairBeats.Taker.ContactAt + PairBeats.TakerLag, 1e-4f, "one contact on the shared clock");
            for (float t = ev.start; t < ev.End; t += 1f / 30f)
            {
                var g = PairStage.CueOf(ev, 0, t); var k = PairStage.CueOf(ev, 1, t);
                int holders = (g.propMode == 1 && g.prop == PropKind.Spear ? 1 : 0) + (k.propMode == 1 && k.prop == PropKind.Spear ? 1 : 0);
                Assert.AreEqual(1, holders, "at t=" + t);
            }
            Assert.AreEqual(PropKind.Spear, PairStage.CueOf(ev, 0, ev.BeatStart + 0.1f).prop);
            Assert.AreEqual(PropKind.Spear, PairStage.CueOf(ev, 1, ev.BeatStart + PairBeats.Giver.ContactAt + 0.01f).prop);
            // the giver ends 0.14 u from the taker, who stays where it stands
            Assert.AreEqual(PairStage.HandGap, (PairStage.CueOf(ev, 0, ev.BeatStart).pos - PairStage.CueOf(ev, 1, ev.BeatStart).pos).magnitude, 1e-3f);
            Assert.AreEqual(0.4f, PairStage.CueOf(ev, 1, ev.BeatStart).pos.x, 1e-4f);
            // one hit, the giver's
            var gp = DollPose.Rest; var kp = DollPose.Rest;
            PairStage.Apply(ev, 0, ev.BeatStart + PairBeats.Giver.ContactAt + 0.02f, ref gp, 0f);
            PairStage.Apply(ev, 1, ev.BeatStart + PairBeats.Giver.ContactAt + 0.02f, ref kp, 0f);
            Assert.AreEqual(1f, gp.hit); Assert.AreEqual(0f, kp.hit);
        }

        [Test]
        public void HandOver_TooFar_IsNotStaged_AndAShortWindowSqueezesTheBeat()
        {
            Assert.IsNull(PairStage.HandOver(1, 0f, 1, 2, Vector2.zero, new Vector2(2f, 0f), PropKind.Spear));
            var tight = PairStage.HandOver(1, 0f, 1, 2, Vector2.zero, new Vector2(0.3f, 0f), PropKind.Spear, 1.2f);
            Assert.That(tight.k, Is.InRange(0.55f, 0.99f));
            Assert.That(tight.End, Is.LessThan(2.0f));
        }

        [Test]
        public void WalkTogether_SideBySide_AtOneSpeed()
        {
            var ev = PairStage.WalkTogether(3, 0f, 1, 2, new Vector2(0f, 0f), new Vector2(0f, 1f), 6f, 0.14f);
            for (float t = 0f; t < 6f; t += 0.5f)
            {
                var a = PairStage.CueOf(ev, 0, t); var b = PairStage.CueOf(ev, 1, t);
                Assert.AreEqual(PairBeats.WalkGap, (a.pos - b.pos).magnitude, 1e-4f);
                Assert.AreEqual(0.14f * t, a.pos.y, 1e-4f);
                Assert.AreEqual(a.pos.y, b.pos.y, 1e-4f, "abreast");
            }
            // the lead has the side at its right hand (heading +z: right is +x)
            Assert.Greater(PairStage.CueOf(ev, 1, 1f).pos.x, PairStage.CueOf(ev, 0, 1f).pos.x);
            // inner hands joined, a lean toward each other, a turn in turn
            var lp = DollPose.Rest; var sp = DollPose.Rest;
            PairBeats.WalkTogether(ref lp, true, 0f, 0f, 3); PairBeats.WalkTogether(ref sp, false, 0f, 0f, 3);
            Assert.Less(lp.roll, 0f); Assert.Greater(sp.roll, 0f);
            Assert.Greater(lp.armR.x, 20f); Assert.Greater(sp.armL.x, 20f);
            float maxLead = 0f, maxSide = 0f;
            for (float t = 0f; t < 8f; t += 0.05f)
            {
                var l = DollPose.Rest; var s = DollPose.Rest;
                PairBeats.WalkTogether(ref l, true, t, 0f, 3); PairBeats.WalkTogether(ref s, false, t, 0f, 3);
                maxLead = Mathf.Max(maxLead, -l.yaw); maxSide = Mathf.Max(maxSide, s.yaw);
                Assert.IsFalse(Mathf.Abs(l.yaw) > 0.5f && Mathf.Abs(s.yaw) > 0.5f, "they turn in turn, not together (t=" + t + ")");
            }
            Assert.That(maxLead, Is.GreaterThanOrEqualTo(15f)); Assert.That(maxSide, Is.GreaterThanOrEqualTo(15f));
        }

        [Test]
        public void Hug_IsBehindTheFlag_AndClosesToHugGapThenStepsBack()
        {
            bool was = BeatFlags.Hugs;
            try
            {
                BeatFlags.Hugs = false;
                Assert.IsNull(PairStage.Hug(1, 0f, 1, 2, Vector2.zero, new Vector2(0.5f, 0f)), "off by default");
                var ev = PairStage.Hug(1, 0f, 1, 2, Vector2.zero, new Vector2(0.5f, 0f), force: true);
                Assert.NotNull(ev);
                float at = ev.BeatStart;
                Assert.AreEqual(PairBeats.HugGapClose, (PairStage.CueOf(ev, 0, at + 1.0f).pos - PairStage.CueOf(ev, 1, at + 1.0f).pos).magnitude, 1e-3f, "arms around each other");
                float end = (PairStage.CueOf(ev, 0, at + 2.3f).pos - PairStage.CueOf(ev, 1, at + 2.3f).pos).magnitude;
                Assert.AreEqual(PairBeats.HugGapClose + PairBeats.HugStepBack, end, 2e-3f, "a step back");
                Assert.AreEqual(2.4f, PairBeats.Hug.Duration, 0.05f);
                BeatFlags.Hugs = true;
                Assert.NotNull(PairStage.Hug(1, 0f, 1, 2, Vector2.zero, new Vector2(0.5f, 0f)), "on with the flag");
            }
            finally { BeatFlags.Hugs = was; }
        }
    }
}
