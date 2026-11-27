// #789 (animation-specs B82.a): the last evening's new clips assert motion: the arm or the head actually moves, and (v2, 2026-11-27: the user's
// clips did not read from the fire's side) each pose reaches its own target angle, on the game's own poses (View/Beats/LastEveningScenes.cs)
// laid over the dolls by the clips' "pose" field, as PeopleView lays a CampPart's pose.
using System.IO;
using AnimBench;
using AquariumCiv.View;
using NUnit.Framework;
using UnityEngine;
using L = AquariumCiv.View.LastEveningScenes;

namespace AnimBenchTests
{
    public class LastEveningClipTests
    {
        static Track Run(string name, out Clip c)
        {
            c = Clip.Load(Path.Combine(ClipPrincipleTests.ClipsDir(), name + ".json"));
            return new Simulator(c).Run();
        }

        /// <summary>How far either arm moved between two poses (the larger, degrees over the three angles): the side is the clip's.</summary>
        static float ArmMoved(DollPose a, DollPose b) => Mathf.Max((a.armL - b.armL).magnitude, (a.armR - b.armR).magnitude);

        /// <summary>How far out sideways the arm on that side is (z away from the body: armR +z, armL -z).</summary>
        static float Out(DollPose p, bool right) => right ? p.armR.z : -p.armL.z;

        static DollFrame At(Track t, Clip c, string id, float sec) => t.Dolls[c.Dolls.FindIndex(d => d.Id == id)].Frames[Mathf.RoundToInt(sec * c.Fps)];

        [Test]
        public void HandOnShoulder_TheArmReachesAcrossAtShoulderHeight_TheEldersHandComesOverIt_ThenTheLookBack()
        {
            var t = Run("789-hand-on-shoulder", out var c);
            var before = At(t, c, "partner", 0.5f).Pose; var held = At(t, c, "partner", 4f).Pose;
            Assert.Greater(ArmMoved(held, before), 60f, "the partner's arm goes out to the shoulder");
            Assert.AreEqual(L.ShoulderArmZ, Mathf.Max(Out(held, true), Out(held, false)), 1f, "out sideways at shoulder height, across to the elder (z 95)");
            Assert.Greater(Mathf.Abs(held.roll), 8f, "leaning in toward the elder");
            Assert.Less(ArmMoved(At(t, c, "partner", 9f).Pose, before), 15f, "and comes back after");
            var e0 = At(t, c, "elder", 0.5f).Pose; var e1 = At(t, c, "elder", 3f).Pose;
            // #800 (the v2 shoot: it was still small): the elder's hand comes up sideways over the partner's arm (z 115), not forward toward the camera
            Assert.Greater(Mathf.Max(Out(e1, true) - Out(e0, true), Out(e1, false) - Out(e0, false)), 60f, "the elder's hand comes up over it (sideways, z 115)");
            Assert.Greater(Mathf.Max(Out(e1, true), Out(e1, false)), L.ShoulderArmZ, "above the partner's arm (z 95)");
            bool hit = false;
            for (float s = 1.8f; s < 2.2f; s += 1f / c.Fps) hit |= At(t, c, "elder", s).Pose.hit > 0f;
            Assert.IsTrue(hit, "a soft hit as the hands meet");
            // the look at the stars (at 7 s, 1 s in, held to 10 s): well back, the hands back on the seat
            var look = At(t, c, "elder", 9f).Pose;
            Assert.AreEqual(L.ElderLookLean, look.lean - e0.lean, 1.5f, "the elder leans well back (lean -32: the head goes with the body)");
            Assert.AreEqual(L.HandsBackX, look.armL.x, 2f, "the hands back on the seat");
            Assert.AreEqual(L.HandsBackX, look.armR.x, 2f);
            Assert.Greater(At(t, c, "elder", 10.4f).Pose.lean, look.lean + 25f, "and up again, the nod after");
        }

        [Test]
        public void LeanOnKnee_TheKidTiltsOntoTheKnee_AndTheEldersHandStrokesOnce()
        {
            var t = Run("789-lean-on-knee", out var c);
            float r0 = At(t, c, "kid", 1.0f).Pose.roll, r1 = At(t, c, "kid", 4f).Pose.roll;
            Assert.AreEqual(25f, Mathf.Abs(r1 - r0), 2f, "the kid's body tilts 25 degrees onto the knee");
            Assert.Greater(At(t, c, "kid", 4f).Pose.lean, At(t, c, "kid", 1.0f).Pose.lean + 5f, "a little forward");
            var h0 = At(t, c, "elder", 3.8f).Pose; var h1 = At(t, c, "elder", 5f).Pose;
            bool l = Mathf.Abs(h0.armL.x) > Mathf.Abs(h0.armR.x) && h0.armL.x < 100f || h0.armR.x > 100f; // the hand not over the shoulder's
            float head = l ? h0.armL.x : h0.armR.x, stroke = l ? h1.armL.x : h1.armR.x;
            Assert.Greater(head, 30f, "the elder's hand rests on the kid's head");
            Assert.Greater(stroke - head, 10f, "and strokes once (x 20)");
        }

        [Test]
        public void HelpUp_TheElderComesUpOffTheSeat_ArmsLinkedWithAHelperEachSide()
        {
            var t = Run("789-help-up", out var c);
            var seated = At(t, c, "elder", 0.9f).Pose; var up = At(t, c, "elder", 2.6f).Pose;
            Assert.Less(seated.squash, 0.85f, "seated");
            Assert.Greater(up.squash, 0.95f, "standing after the rise");
            float minRoll = 0f, maxRoll = 0f;
            for (float s = 1.0f; s < 2.2f; s += 1f / c.Fps) { float r = At(t, c, "elder", s).Pose.roll; minRoll = Mathf.Min(minRoll, r); maxRoll = Mathf.Max(maxRoll, r); }
            Assert.Greater(maxRoll - minRoll, 3f, "a small sway on the way up");
            var risen = At(t, c, "elder", 2.15f).Pose;
            Assert.AreEqual(L.OverArmZ, Out(risen, true), 2f, "the elder's arms go out over both helpers' shoulders (z 75)");
            Assert.AreEqual(L.OverArmZ, Out(risen, false), 2f);
            foreach (var id in new[] { "partner", "son" })
            {
                var a = At(t, c, id, 1.8f).Pose;
                Assert.Greater(ArmMoved(a, At(t, c, id, 0.5f).Pose), 40f, id + "'s near arm moves out");
                Assert.AreEqual(L.UnderArmZ, Mathf.Max(Out(a, true), Out(a, false)), 1f, id + "'s near arm under the elder's (z 60)");
            }
            Assert.AreEqual(2f * (L.Beside + 0.01f), (At(t, c, "partner", 2f).Pos - At(t, c, "son", 2f).Pos).magnitude, 0.01f, "a helper each side, clear of the elder");
        }

        [Test]
        public void PairWalk_TheElderWalksAtTheirOwnSlowPace_HeldUpBetweenTheHelpers()
        {
            var t = Run("789-pair-walk", out var c);
            var e = At(t, c, "elder", 6f);
            Assert.IsTrue(e.Walking, "walking");
            Assert.AreEqual(L.Walk * L.FrailPace, e.Speed, 0.012f, "about 0.4 of the usual pace");
            Assert.Greater((At(t, c, "elder", 11.5f).Pos - At(t, c, "elder", 0.2f).Pos).magnitude, 0.9f, "they really go home");
            Assert.AreEqual(L.OverArmZ, Out(e.Pose, true), 1f, "the elder's arms over the helpers' shoulders the whole way");
            Assert.AreEqual(L.OverArmZ, Out(e.Pose, false), 1f);
            foreach (var id in new[] { "partner", "son" })
            {
                var h = At(t, c, id, 6f);
                Assert.AreEqual(e.Speed, h.Speed, 0.01f, id + " keeps the elder's pace");
                Assert.AreEqual(L.Beside, (h.Pos - e.Pos).magnitude, 0.015f, id + " stays at the elder's side");
                Assert.AreEqual(L.UnderArmZ, Mathf.Max(Out(h.Pose, true), Out(h.Pose, false)), 1f, id + "'s near arm under the elder's");
            }
            Assert.Greater(e.Pose.lean, At(t, c, "elder", 12.5f).Pose.lean + 4f, "the frail stoop while walking");
        }

        [Test]
        public void Wave_BigAndSlow_TheArmHighOverTheHead()
        {
            var t = Run("789-wave", out var c);
            Assert.Less(At(t, c, "elder", 0.5f).Pose.armR.z, 30f, "the arm down before the wave");
            float lo = float.MaxValue, hi = float.MinValue, last = 0f; int turns = 0;
            for (float s = 1.5f; s < 2.9f; s += 1f / c.Fps)
            {
                float z = At(t, c, "elder", s).Pose.armR.z; lo = Mathf.Min(lo, z); hi = Mathf.Max(hi, z);
                float d = z - At(t, c, "elder", s - 1f / c.Fps).Pose.armR.z;
                if (d * last < 0f) turns++;
                if (Mathf.Abs(d) > 1e-3f) last = d;
            }
            Assert.Greater(lo, 120f, "the arm stays high over the head the whole wave (z 150 +-25)");
            Assert.AreEqual(2f * L.WaveSwing, hi - lo, 8f, "swung wide");
            Assert.LessOrEqual(turns, 3, "slowly (0.6 Hz: a turn or two in 1.4 s, not a flutter)");
            Assert.GreaterOrEqual(turns, 1, "it really swings");
            Assert.Less(At(t, c, "elder", 3.6f).Pose.armR.z, 30f, "and comes down before going in");
        }
    }
}
