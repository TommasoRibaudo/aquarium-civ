// #789 (animation-specs B82.a): the last evening's four new clips assert motion: the arm or the head actually moves, on the game's own poses
// (View/Beats/LastEveningScenes.cs) laid over the dolls by the clips' "pose" field, as PeopleView lays a CampPart's pose.
using System.IO;
using AnimBench;
using AquariumCiv.View;
using NUnit.Framework;
using UnityEngine;

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

        static DollFrame At(Track t, Clip c, string id, float sec) => t.Dolls[c.Dolls.FindIndex(d => d.Id == id)].Frames[Mathf.RoundToInt(sec * c.Fps)];

        [Test]
        public void HandOnShoulder_TheArmGoesOut_AndTheEldersHandComesOverIt()
        {
            var t = Run("789-hand-on-shoulder", out var c);
            var before = At(t, c, "partner", 0.5f).Pose; var held = At(t, c, "partner", 4f).Pose;
            Assert.Greater(ArmMoved(held, before), 20f, "the partner's arm goes out to the shoulder (x 60; the sit already holds the hands a little forward)");
            Assert.Greater(Mathf.Max(held.armL.x, held.armR.x), 55f, "forward, toward the elder");
            Assert.Less(ArmMoved(At(t, c, "partner", 9f).Pose, before), 15f, "and comes back after");
            var e0 = At(t, c, "elder", 0.5f).Pose; var e1 = At(t, c, "elder", 3f).Pose;
            Assert.Greater(Mathf.Max(e1.armL.x - e0.armL.x, e1.armR.x - e0.armR.x), 60f, "the elder's hand comes up over it (x 110)");
            Assert.Less(At(t, c, "elder", 8.2f).Pose.lean, e0.lean - 8f, "the look up at the stars (lean back)");
            bool hit = false;
            for (float s = 1.8f; s < 2.2f; s += 1f / c.Fps) hit |= At(t, c, "elder", s).Pose.hit > 0f;
            Assert.IsTrue(hit, "a soft hit as the hands meet");
        }

        [Test]
        public void LeanOnKnee_TheKidTiltsOntoTheKnee_AndTheEldersHandStrokesOnce()
        {
            var t = Run("789-lean-on-knee", out var c);
            float r0 = At(t, c, "kid", 1.0f).Pose.roll, r1 = At(t, c, "kid", 4f).Pose.roll;
            Assert.Greater(Mathf.Abs(r1 - r0), 20f, "the kid's body tilts about 25 degrees onto the knee");
            Assert.Greater(At(t, c, "kid", 4f).Pose.lean, At(t, c, "kid", 1.0f).Pose.lean + 5f, "a little forward");
            var h0 = At(t, c, "elder", 3.8f).Pose; var h1 = At(t, c, "elder", 5f).Pose;
            bool l = Mathf.Abs(h0.armL.x) > Mathf.Abs(h0.armR.x) && h0.armL.x < 100f || h0.armR.x > 100f; // the hand not over the shoulder's
            float head = l ? h0.armL.x : h0.armR.x, stroke = l ? h1.armL.x : h1.armR.x;
            Assert.Greater(head, 30f, "the elder's hand rests on the kid's head");
            Assert.Greater(stroke - head, 10f, "and strokes once (x 20)");
        }

        [Test]
        public void HelpUp_TheElderComesUpOffTheSeat_BothHelpersArmsUnderTheElbows()
        {
            var t = Run("789-help-up", out var c);
            var seated = At(t, c, "elder", 0.9f).Pose; var up = At(t, c, "elder", 2.6f).Pose;
            Assert.Less(seated.squash, 0.85f, "seated");
            Assert.Greater(up.squash, 0.95f, "standing after the rise");
            float minRoll = 0f, maxRoll = 0f;
            for (float s = 1.0f; s < 2.2f; s += 1f / c.Fps) { float r = At(t, c, "elder", s).Pose.roll; minRoll = Mathf.Min(minRoll, r); maxRoll = Mathf.Max(maxRoll, r); }
            Assert.Greater(maxRoll - minRoll, 3f, "a small sway on the way up");
            Assert.Greater(ArmMoved(At(t, c, "partner", 1.8f).Pose, At(t, c, "partner", 0.5f).Pose), 30f, "the partner's near arm out under the elbow");
            Assert.Greater(ArmMoved(At(t, c, "son", 1.8f).Pose, At(t, c, "son", 0.5f).Pose), 30f, "the son's near arm out under the other elbow");
        }

        [Test]
        public void PairWalk_TheElderWalksAtTheirOwnSlowPace_TheHelpersKeepAtTheirSidesWithAnArmOut()
        {
            var t = Run("789-pair-walk", out var c);
            var e = At(t, c, "elder", 6f);
            Assert.IsTrue(e.Walking, "walking");
            Assert.AreEqual(LastEveningScenes.Walk * LastEveningScenes.FrailPace, e.Speed, 0.012f, "about 0.4 of the usual pace");
            Assert.Greater((At(t, c, "elder", 11.5f).Pos - At(t, c, "elder", 0.2f).Pos).magnitude, 0.9f, "they really go home");
            foreach (var id in new[] { "partner", "son" })
            {
                var h = At(t, c, id, 6f);
                Assert.AreEqual(e.Speed, h.Speed, 0.01f, id + " keeps the elder's pace");
                Assert.AreEqual(LastEveningScenes.Beside, (h.Pos - e.Pos).magnitude, 0.015f, id + " stays at the elder's side");
                Assert.Greater(Mathf.Max(Mathf.Abs(h.Pose.armL.z), Mathf.Abs(h.Pose.armR.z)), 35f, id + "'s near arm out under the elbow");
            }
            Assert.Greater(e.Pose.lean, At(t, c, "elder", 12.5f).Pose.lean + 4f, "the frail stoop while walking");
        }
    }
}
