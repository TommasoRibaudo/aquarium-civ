using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #700 (B27) on the real Slice scene (1179x2556): a path is laid over the fish rack (the cheat), "Rack moves now" sends two people, who take the
    /// rack down, carry it and set it up again (the parts are drawn by RackMoveView, the people stand and pose by RackMovePlan/RackMoveBeats); when the
    /// beat ends the rack stands clear of the path with its fish back on, and four holes mark the old spot. Shots in Temp/agent/700/.
    /// </summary>
    [Category("RackMovePlay")]
    public class RackMovePlayTests : PlayTestBase
    {
        static string Dir => Shots + "700/";
        static SimCore Sim => GameClock.Instance.Sim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        static void Look(Vector3 world, float zoom, float pitch = 45f)
        {
            var rig = Cam;
            rig.CancelFlight();
            rig.target = world; rig.zoom = zoom; rig.pitch = pitch;
            rig.Apply();
        }

        static IEnumerator Png(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Dir + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static Vector3 World(BoxView box, float x, float z) => box.transform.TransformPoint(box.land.OnGround(x, z));

        [UnityTest, Timeout(900000)]
        public IEnumerator APathOverTheFishRack_TwoPeopleTakeItDownAndSetItUpOffThePath()
        {
            UseLivingLand();
            yield return OpenSlice(1179, 2556, 4242);
            var sim = Sim;
            Assert.IsTrue(sim.EcologyOn, "a living-land box");
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            var planner = pv.Planner;
            var rv = RackMoveView.Ensure(box);
            var fr = FishRackView.Ensure(box);
            Assert.IsNotNull(rv); Assert.IsNotNull(fr);
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            PeopleView.DebugSetHour(10f);
            yield return Frames(20);
            planner.ClearLive();
            Assert.IsTrue(planner.FirePoint(out float fx, out float fz));
            Assert.AreEqual(6, sim.DebugFishOnRack(6, pv.Now.day), "six dry fish hang on the rack");
            yield return Frames(10);
            Assert.IsTrue(rv.isActiveAndEnabled);
            float oldAngle = FishRackSpot.AngleFor(sim, fx, fz);

            // the path comes over it: by day, loaded, nothing moves yet (the rack waits for a quiet hour or the cheat)
            string laid = SimDebugPanel.DoPathOverRack(false);
            Debug.Log("[700] " + laid);
            StringAssert.Contains("path laid", laid);
            yield return Frames(30);
            Assert.IsTrue(RackSites.Pending(sim, RackKind.Fish, out float from, out float to), "the memo notes the rack on the path");
            Assert.AreEqual(oldAngle, from, 1e-4f);
            Assert.AreEqual(oldAngle, FishRackSpot.AngleFor(sim, fx, fz), 1e-4f, "by day and loaded the rack stays");
            var onPath = RackSites.PathTest(sim);
            Assert.IsTrue(CampSpots.RackOnPath(fx, fz, oldAngle, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength, onPath), "it stands on the path");
            Assert.IsFalse(rv.Drawing, "and nobody has started");

            // the cheat: two people, now
            StringAssert.Contains("take the rack down", SimDebugPanel.DoRackMoveNow());
            yield return Until(() => planner.RackMoveRunning, 10f);
            Assert.IsTrue(planner.RackMoveRunning, "a rack beat is planned: " + planner.RackMoveWhy);
            planner.FirePoint(out fx, out fz);
            var oldAt = World(box, fx + Mathf.Cos(oldAngle) * FishRackSpot.Distance, fz + Mathf.Sin(oldAngle) * FishRackSpot.Distance);
            Look(oldAt + Vector3.up * 0.05f, 0.26f, 42f);
            yield return Until(() => rv.Drawing, 60f);
            Assert.IsTrue(rv.Drawing, "the beat plays");
            Assert.IsFalse(fr.Visible, "the regular rack view stays away while it plays");
            yield return Png("700_1_antic");
            bool sawTake = false, sawCarry = false, sawPlant = false, sawRaise = false;
            for (int i = 0; i < 9000 && planner.RackMoveRunning; i++)
            {
                yield return null;
                if (!rv.Drawing) continue;
                if (!sawTake && rv.Phase == RackPhase.PullFrames) { sawTake = true; yield return Png("700_2_frames_pulled"); }
                if (!sawCarry && rv.Phase == RackPhase.Carry) { sawCarry = true; yield return Png("700_3_carry"); }
                if (!sawPlant && rv.Phase == RackPhase.Plant) { sawPlant = true; yield return Png("700_4_plant"); }
                if (!sawRaise && rv.Phase == RackPhase.Raise) { sawRaise = true; yield return Png("700_5_raise"); }
            }
            Assert.IsTrue(sawTake && sawCarry && sawPlant && sawRaise, $"every phase played (take {sawTake}, carry {sawCarry}, plant {sawPlant}, raise {sawRaise})");
            Assert.IsFalse(planner.RackMoveRunning, "the crew are let go");

            // after: off the path, the fish back on, the holes show
            yield return Frames(20);
            float newAngle = FishRackSpot.AngleFor(sim, fx, fz);
            Assert.Greater(Mathf.Abs(newAngle - oldAngle), 1e-3f, "the rack moved");
            Assert.IsFalse(CampSpots.RackOnPath(fx, fz, newAngle, FishRackSpot.Distance, FishRackSpot.HalfDepth, FishRackSpot.HalfLength, onPath), "the rack ends off the path");
            Assert.IsFalse(RackSites.Pending(sim, RackKind.Fish, out _, out _), "nothing waits to move");
            Assert.AreEqual(6, sim.FishOnRack, "its fish are back on");
            Assert.IsTrue(fr.Visible, "the regular rack is back");
            Assert.AreEqual(6, fr.FreshShown + fr.DryShown);
            Assert.AreEqual(4, rv.HolesShown, "the four holes of the old spot");
            var newAt = World(box, fx + Mathf.Cos(newAngle) * FishRackSpot.Distance, fz + Mathf.Sin(newAngle) * FishRackSpot.Distance);
            Look((oldAt + newAt) * 0.5f + Vector3.up * 0.03f, 0.3f, 48f);
            yield return Frames(6);
            yield return Png("700_6_after");
            Assert.AreEqual(1, planner.RackMovesStarted, "one beat, once");
        }
    }
}
