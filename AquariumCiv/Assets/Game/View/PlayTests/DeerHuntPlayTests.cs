using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P9 #426 (animation-specs B13): the deer hunt you watch. Two or three hunters take bows at the fire, walk out in a line, crouch at the
    /// wood's edge, stalk side by side while the deer is held at its spot, the lead shoots; a hit: the arrow lands on the deer (P2: it goes
    /// down at once), the rest of the herd bolts (P3), the count drops by one, the pole is fetched and the deer carried home between two
    /// carriers (the pole's ends stay on their shoulders), set down by the fire; a miss: the arrow sticks in the ground past the deer, the
    /// whole herd bolts, nothing is taken. The first deer's feast that night, the antlers over the chief's door. Shots at phone size:
    /// Temp/agent/p9-deer-*.png.
    /// </summary>
    [Category("DeerHunt")]
    public class DeerHuntPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        static BoxView Box => UnityEngine.Object.FindAnyObjectByType<BoxView>();

        IEnumerator DeerLand()
        {
            UseLivingLand(true);
            GameUiPanel.AcceptInputUnfocused(true);
            yield return OpenSlice(1179, 2556, seed: 4242);
            GameUiPanel.AcceptInputUnfocused(true);
            Assert.IsTrue(Sim.EcologyOn, "a living-land box");
            PeopleView.DebugSetHour(8.2f);
            foreach (var (x, z) in new[] { (-1.6f, 0.6f), (-1.0f, -0.8f), (-2.0f, -0.2f), (1.6f, 0.9f), (0.8f, -0.9f) }) EcoDebug.MistBig(x, z);
            EcoDebug.Table(0.3f);
            Sim.DebugGrowFarWood();
            yield return Frames(20);
            Assert.GreaterOrEqual(Sim.DebugPlaceNearCamp(LifeSpecies.Deer), 0, "deer placed");
            yield return Until(() => !LifeArrivalView.Instance.Playing(LifeSpecies.Deer), 30f);
            Sim.DebugDeerToCap();
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.Find().Planner.ClearLive();
            yield return new WaitForSecondsRealtime(2f);
        }

        static IEnumerator LookAt(float x, float z, float zoom)
        {
            Cam.FlyTo(Box.transform.TransformPoint(Box.land.OnGround(x, z)), zoom, 0f);
            yield return Frames(3);
        }

        static ViewTime Now => PeopleView.Find().Now;

        static IEnumerator UntilHour(DeerHuntPlan p, float hour, float limit = 120f)
        {
            yield return Until(() => Now.day != p.day || Now.hour >= hour, limit);
            Assert.IsTrue(Now.day == p.day && Now.hour >= hour, $"reached {hour:0.00} h (now {Now.hour:0.00})");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheHit_Stalk_Shot_HerdBolts_PoleCarry_SetDown()
        {
            yield return DeerLand();
            var dv = DeerView.Instance; var stage = DeerHuntStage.Instance; var pv = PeopleView.Find();
            Assert.IsNotNull(dv); Assert.IsNotNull(stage, "the hunt's stage");
            Assert.AreSame(dv.Field, pv.Planner.DeerField, "the hunters see the herd the view shows");
            int herd = Sim.EcoDeerCount, shown = dv.Shown;
            string said = DeerHuntStage.DebugHuntNow(true);
            Debug.Log("[p9-deer] " + said);
            var p = pv.Planner.DeerHuntsOn(Now.day).LastOrDefault(x => x.debugStarted);
            Assert.IsNotNull(p, said);
            Assert.IsTrue(p.HasScene && p.playsHit, said);
            float midX = (p.fireX + p.targetX) * 0.5f, midZ = (p.fireZ + p.targetZ) * 0.5f;
            // the line out, bows on their backs
            yield return UntilHour(p, p.walkOutHour + (p.stalkStart - p.walkOutHour) * 0.45f);
            yield return LookAt(midX, midZ, 0.32f);
            yield return Shot("p9-deer-hit-1-line");
            Assert.IsTrue(pv.TryGetDollPose(p.hunters[0], out _), "the lead is drawn");
            // the crouch and the stalk: the deer stands at its spot
            yield return UntilHour(p, p.stalkStart + 0.02f);
            int held0 = dv.TargetHeldFrames;
            yield return LookAt((p.shotX + p.targetX) * 0.5f, (p.shotZ + p.targetZ) * 0.5f, 0.2f);
            yield return Shot("p9-deer-hit-2-stalk");
            Assert.Greater(dv.TargetHeldFrames, held0, "the target is held at its spot through the stalk");
            Assert.IsTrue(dv.TryGetDeerById(p.targetId, out var deerAt), "the target is shown");
            Assert.Less(new Vector2(deerAt.x - p.targetX, deerAt.z - p.targetZ).magnitude, 0.12f, "and stands where the hunters aim");
            Assert.IsTrue(pv.TryGetDollPose(p.hunters[1], out var crouch) && crouch.squash < 0.85f, $"they creep crouched (squash {crouch.squash:0.00})");
            // the aim, held
            float aimAt = p.shootStart + (p.releaseHour - p.shootStart) * 0.85f;
            yield return UntilHour(p, aimAt);
            yield return Shot("p9-deer-hit-3-aim");
            Assert.IsTrue(pv.TryGetDollPose(p.hunters[0], out var aim) && aim.armL.x > 70f, $"the bow arm is up ({aim.armL})");
            // the arrow flies
            yield return UntilHour(p, p.releaseHour + (p.landHour - p.releaseHour) * 0.5f);
            yield return Shot("p9-deer-hit-4-arrow");
            // contact: the deer goes down at once, the herd bolts, one deer fewer
            yield return UntilHour(p, p.landHour);
            yield return Frames(4);
            Assert.GreaterOrEqual(stage.ArrowsLanded, 1, "the arrow landed: " + stage.Status());
            Assert.IsTrue(stage.TryGetCarcass(out _, out bool onPole) && !onPole, "P2: the deer is down within a frame or two");
            Assert.IsTrue(p.resolved && p.caught, "settled at the landing");
            Assert.AreEqual(herd - 1, Sim.EcoDeerCount, "the count drops by one on a hit");
            Assert.Greater(dv.HuntBolts, 0, "P3: the herd bolts");
            Assert.Greater(dv.BoltingNow, 0, "deer are running");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("p9-deer-hit-5-down");
            Assert.AreEqual(Mathf.Min(shown, Sim.EcoDeerShown()), dv.Shown, "the view's herd follows the sim");
            // the pole carry
            stage.ResetPoleGap();
            yield return UntilHour(p, p.takeHour + (p.setDownHour - p.takeHour) * 0.4f, 150f);
            yield return LookAt(p.fireX + p.dirX * 0.7f, p.fireZ + p.dirZ * 0.7f, 0.24f);
            Assert.IsTrue(stage.TryGetCarcass(out _, out onPole) && onPole, "carried on the pole");
            Assert.IsTrue(stage.TryGetPole(out _, out _), "the pole is drawn");
            yield return Shot("p9-deer-hit-6-carry");
            yield return UntilHour(p, p.setDownHour + 0.01f, 150f);
            Assert.LessOrEqual(stage.PoleGapMax, 0.03f, "the pole's ends stay on the carriers' shoulders");
            yield return Frames(4);
            Assert.AreEqual(1, stage.SetDowns, "set down by the fire");
            yield return LookAt(p.fireX, p.fireZ, 0.24f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("p9-deer-hit-7-setdown");
            AssertNoErrors("deer hunt hit");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheMiss_ArrowSticks_WholeHerdBolts_NothingTaken()
        {
            yield return DeerLand();
            var dv = DeerView.Instance; var stage = DeerHuntStage.Instance; var pv = PeopleView.Find();
            int herd = Sim.EcoDeerCount;
            string said = DeerHuntStage.DebugHuntNow(false);
            var p = pv.Planner.DeerHuntsOn(Now.day).LastOrDefault(x => x.debugStarted);
            Assert.IsNotNull(p, said);
            Assert.IsTrue(p.HasScene && !p.playsHit, said);
            yield return UntilHour(p, p.shootStart);
            yield return LookAt((p.shotX + p.targetX) * 0.5f, (p.shotZ + p.targetZ) * 0.5f, 0.2f);
            yield return UntilHour(p, p.landHour);
            yield return Frames(4);
            Assert.IsTrue(stage.TryGetArrow(out var tip, out bool flying) && !flying, "the arrow stands in the ground");
            Assert.Less(new Vector2(tip.x - p.landX, tip.z - p.landZ).magnitude, 0.05f, "past the deer");
            Assert.Greater(dv.HuntBolts, 0, "the whole herd bolts");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("p9-deer-miss-1-arrow");
            Assert.AreEqual(herd, Sim.EcoDeerCount, "a miss takes nothing");
            Assert.IsFalse(p.caught);
            yield return UntilHour(p, p.landHour + (p.arrowTakeHour - p.landHour) * 0.3f);
            yield return Shot("p9-deer-miss-2-shrug");
            yield return UntilHour(p, p.arrowTakeHour + 0.01f);
            yield return Frames(3);
            Assert.IsFalse(stage.TryGetArrow(out _, out _), "the shooter pulled the arrow out");
            // tester 2026-11-08: once the hunt is over it must never be rebuilt (it re-shot its arrow every frame, ArrowsShot 2 -> 81)
            if (Now.hour < p.end + 1.6f) PeopleView.DebugSetHour(Mathf.Min(p.end + 1.6f, 23f));
            yield return Frames(5);
            int shot = stage.ArrowsShot, landed = stage.ArrowsLanded;
            var hunt = GameObject.Find("Deer hunt");
            int kids = hunt != null ? hunt.transform.childCount : 0;
            yield return new WaitForSecondsRealtime(3f);
            Assert.AreEqual(shot, stage.ArrowsShot, "no arrow is shot again after the hunt ended");
            Assert.AreEqual(landed, stage.ArrowsLanded, "no arrow lands again after the hunt ended");
            Assert.LessOrEqual(hunt != null ? hunt.transform.childCount : 0, kids, "the hunt's objects stay flat after it ended");
            Assert.IsFalse(stage.TryGetArrow(out _, out _), "no arrow after the hunt ended");
            AssertNoErrors("deer hunt miss");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator NormalPlay_DeerHuntsArePlanned_TheFeast_TheAntlers()
        {
            yield return DeerLand();
            var pv = PeopleView.Find(); var stage = DeerHuntStage.Instance;
            // a hunt happens in normal play (no cheat): the planner plans scenes over the view's herd on some days
            int scenes = 0;
            for (long d = Now.day + 1; d < Now.day + 40; d++) scenes += pv.Planner.DeerHuntsOn(d).Count(x => x.HasScene && !x.debugStarted);
            Assert.Greater(scenes, 0, "deer hunts with a scene in the next 40 days");
            // the feast
            StringAssert.Contains("feast", stage.DebugFeastNow());
            yield return Until(() => stage.FeastShown, 10f);
            Assert.IsTrue(stage.FeastShown, "the big fire and the spit");
            yield return LookAt(Sim.EcoGround.campX, Sim.EcoGround.campZ, 0.24f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("p9-deer-feast-1-fire");
            yield return Until(() => Now.hour >= DeerHuntStage.RingAt + 0.15f, 90f);
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("p9-deer-feast-2-ring");
            // the antlers over the chief's door
            StringAssert.Contains("antlers", stage.DebugAntlersNow());
            PeopleView.DebugSetHour(10f);
            yield return Until(() => stage.AntlersShown, 10f);
            Assert.IsTrue(stage.AntlersShown, "the antlers hang over the chief's door: " + stage.Status());
            var ant = GameObject.Find("Antlers");
            if (ant != null) { Cam.FlyTo(ant.transform.position, 0.2f, 0f); yield return Frames(3); }
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("p9-deer-antlers");
            AssertNoErrors("feast and antlers");
        }
    }
}
