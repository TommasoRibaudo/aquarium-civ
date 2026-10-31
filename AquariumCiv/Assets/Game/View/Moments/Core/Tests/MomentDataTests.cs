using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;

namespace AquariumCiv.View.Moments.Tests
{
    /// <summary>The moment data (#89, #90), the camera rail, the rebuild wave and the era-ready waiting state (#88).</summary>
    public class MomentDataTests
    {
        class Anchors : IMomentAnchors
        {
            public readonly Dictionary<MomentAnchor, MVec3> at = new();
            public bool TryGet(MomentAnchor a, out MVec3 p) => at.TryGetValue(a, out p);
        }

        static IEnumerable<MomentDef> All() => new[] { MomentLibrary.CampToVillage(), MomentLibrary.VillageToTown() };

        // ---------- the data ----------
        [Test]
        public void TheDocsTimingsAndTheCheer()
        {
            var v = MomentLibrary.CampToVillage();
            Assert.AreEqual(46f, v.Beat("cheer").start);
            Assert.AreEqual(20f, v.Beat("wave").start);
            Assert.AreEqual("channel", v.Beat("tool").param);
            var t = MomentLibrary.VillageToTown();
            Assert.AreEqual(75f, t.Beat("cheer").start);
            Assert.AreEqual(35f, t.Beat("wave").start);
            Assert.AreEqual("outcrop", t.Beat("tool").param);
        }

        [Test]
        public void EachMomentHasExactlyOneCheerAndItsBeatsAreInOrder()
        {
            foreach (var m in All())
            {
                Assert.AreEqual(1, m.beats.Count(b => b.Has(BeatAction.Cheer)), m.id);
                for (int i = 1; i < m.beats.Count; i++)
                    Assert.LessOrEqual(m.beats[i - 1].start, m.beats[i].start, $"{m.id}: {m.beats[i].id}");
                Assert.AreEqual(m.beats.Count, m.beats.Select(b => b.id).Distinct().Count(), $"{m.id}: unique beat ids");
                Assert.IsTrue(m.beats.Any(b => b.freeCamera), $"{m.id} frees the camera");
                Assert.IsTrue(m.beats.Where(b => b.Has(BeatAction.Cheer)).All(b => b.when == BeatCondition.Always), "the cheer always plays");
            }
        }

        [Test]
        public void EveryCameraKeyIsInsideBoxCamerasRanges()
        {
            foreach (var m in All())
                foreach (var b in m.beats)
                    foreach (var k in b.camera)
                        Assert.IsTrue(CameraRail.Limits.Inside(k), $"{m.id}/{b.id} key at {k.at}: yaw {k.yaw} pitch {k.pitch} zoom {k.zoom}");
        }

        [TestCase(true, true, true)]
        [TestCase(false, false, false)]
        public void LengthsAreAboutTheDocs(bool first, bool leader, bool cleared)
        {
            var ctx = new MomentContext { firstMoment = first, leaderPresent = leader, somethingCleared = cleared };
            var d = new MomentDirector(() => true);
            d.Start(MomentLibrary.CampToVillage(), ctx, CameraPose.Default);
            Assert.That(d.Length, Is.InRange(60f, 75f), "camp → village ~60 s (+ the question)");
            d = new MomentDirector(() => true);
            d.Start(MomentLibrary.VillageToTown(), ctx, CameraPose.Default);
            Assert.That(d.Length, Is.InRange(90f, 105f), "village → town ~90 s");
        }

        // ---------- the rail ----------
        [Test]
        public void TheRailGlidesFromWhereThePlayerLeftTheCamera()
        {
            var beats = new List<MomentBeat> { new() { id = "a", start = 0f, camera = { new CameraKey(2f, MomentAnchor.Box, 10f, 30f, 0.8f) } } };
            var rail = new CameraRail(beats);
            var from = new CameraPose(new MVec3(0f, 1f, 0f), 50f, 40f, 1.2f);
            var p0 = rail.Evaluate(0f, from, null);
            Assert.AreEqual(50f, p0.yaw, 1e-4f);
            Assert.AreEqual(1.2f, p0.zoom, 1e-4f);
            var mid = rail.Evaluate(1f, from, null);
            Assert.AreEqual(30f, mid.yaw, 1e-3f, "smootherstep is 0.5 half-way");
            var end = rail.Evaluate(2f, from, null);
            Assert.AreEqual(10f, end.yaw, 1e-4f);
            Assert.AreEqual(0.8f, end.zoom, 1e-4f);
            Assert.AreEqual(10f, rail.Evaluate(99f, from, null).yaw, 1e-4f, "holds after the last key");
        }

        [Test]
        public void TheRailIsSmoothAndMonotonicBetweenKeys()
        {
            var beats = new List<MomentBeat>
            {
                new() { id = "a", start = 0f, camera = { new CameraKey(0f, MomentAnchor.Box, 50f, 30f, 1f) } },
                new() { id = "b", start = 1f, camera = { new CameraKey(9f, MomentAnchor.Box, -10f, 30f, 1f) } },
            };
            var rail = new CameraRail(beats);
            float last = float.MaxValue, lastStep = 0f;
            for (float t = 0f; t <= 10f; t += 0.05f)
            {
                float y = rail.Evaluate(t, CameraPose.Default, null).yaw;
                Assert.LessOrEqual(y, last + 1e-4f);
                float step = last == float.MaxValue ? 0f : last - y;
                Assert.Less(step, 1f, "no jumps at 20 fps");
                lastStep = step; last = y;
            }
            Assert.AreEqual(-10f, last, 1e-3f);
        }

        [Test]
        public void AnchorsAreFollowedAndAMissingOneFallsBackToTheBox()
        {
            var a = new Anchors();
            a.at[MomentAnchor.Leader] = new MVec3(1f, 1f, -0.5f);
            var k = new CameraKey(0f, MomentAnchor.Leader, 20f, 20f, 0.4f);
            var p = CameraRail.PoseOf(k, a);
            Assert.AreEqual(1f, p.target.x, 1e-4f);
            Assert.AreEqual(-0.5f, p.target.z, 1e-4f);
            var missing = CameraRail.PoseOf(new CameraKey(0f, MomentAnchor.Steps, 20f, 20f, 0.4f), a);
            Assert.AreEqual(CameraPose.Default.target, missing.target);
        }

        [Test]
        public void PosesAreClampedToBoxCamerasRanges()
        {
            var far = new CameraKey(0f, MomentAnchor.Box, 120f, 5f, 2f) { offset = new MVec3(9f, 0f, 9f) };
            var p = CameraRail.PoseOf(far, null);
            Assert.AreEqual(CameraRail.Limits.MaxYaw, p.yaw);
            Assert.AreEqual(CameraRail.Limits.MaxZoom, p.zoom);
            Assert.AreEqual(CameraRail.Limits.PitchFloorFull, p.pitch);
            Assert.AreEqual(0f, p.target.x, 1e-5f, "no pan at zoom 1 and out");
            var close = CameraRail.Limits.Clamp(new CameraPose(new MVec3(9f, 1f, -9f), 0f, 0f, 0.3f));
            Assert.AreEqual(CameraRail.Limits.HX - CameraRail.Limits.PanMargin, close.target.x, 1e-4f);
            Assert.AreEqual(-(CameraRail.Limits.HZ - CameraRail.Limits.PanMargin), close.target.z, 1e-4f);
            Assert.AreEqual(CameraRail.Limits.PitchFloorClose, close.pitch, 1e-4f);
        }

        [Test]
        public void TheDirectorsCameraFollowsItsRail()
        {
            var d = new MomentDirector(() => true);
            var from = new CameraPose(new MVec3(0f, 1f, 0f), 40f, 40f, 1.1f);
            d.Start(MomentLibrary.CampToVillage(), new MomentContext(), from);
            Assert.AreEqual(40f, d.CameraNow(null).yaw, 1e-3f, "starts where the player was");
            for (int i = 0; i < 27; i++) d.Tick(0.1f, false); // 2.7 s: past the key at 2.5 s
            Assert.AreEqual(15f, d.CameraNow(null).yaw, 1e-3f, "at the dawn key");
        }

        [Test]
        public void TheFingerPullsOffTheRailAndEasesBack()
        {
            var o = new GuidedOffset();
            o.Step(0.016f, true, 25f, 5f);
            Assert.AreEqual(25f, o.Yaw);
            o.Step(0.016f, true, 100f, 0f);
            Assert.AreEqual(GuidedOffset.MaxYaw, o.Yaw, "capped");
            var rail = new CameraPose(new MVec3(0f, 1f, 0f), 0f, 34f, 1f);
            Assert.AreEqual(40f, o.Apply(rail).yaw, 1e-4f);
            for (int i = 0; i < 300; i++) o.Step(1f / 60f, false);
            Assert.AreEqual(0f, o.Yaw, "back on the rail after 5 s");
            Assert.AreEqual(0f, o.Pitch);
            Assert.AreEqual(0f, o.Apply(rail).yaw);
        }

        // ---------- the wave ----------
        [Test]
        public void TheWaveRunsFromTheCentreOutwardOnARisingScale()
        {
            var lots = new List<(int, MVec3)>
            {
                (5, new MVec3(2f, 1f, 0f)), (3, new MVec3(0.5f, 1f, 0f)), (9, new MVec3(-1f, 1f, 0f)), (1, new MVec3(0f, 1f, -2.5f)),
            };
            var w = new RebuildWave(lots, new MVec3(0f, 1f, 0f), 20f, 20f);
            CollectionAssert.AreEqual(new[] { 3, 9, 5, 1 }, w.Steps.Select(s => s.lotId).ToArray());
            Assert.AreEqual(20f, w.Steps[0].time, 1e-4f);
            Assert.AreEqual(20f + 20f * RebuildWave.Spread, w.Steps[3].time, 1e-4f);
            for (int i = 1; i < w.Steps.Count; i++)
            {
                Assert.Greater(w.Steps[i].time, w.Steps[i - 1].time);
                Assert.Greater(w.Steps[i].semitone, w.Steps[i - 1].semitone);
            }
            Assert.AreEqual(0, w.DoneBy(19.9f));
            Assert.AreEqual(4, w.DoneBy(40f));
            Assert.AreEqual(1, w.Due(19f, 20f).Count());
            Assert.AreEqual(0, w.Due(20f, 20.1f).Count(), "a step fires once");
        }

        [Test]
        public void TheScaleClimbsThenStaysOnTop()
        {
            Assert.AreEqual(0, RebuildWave.Semitone(0));
            Assert.AreEqual(12, RebuildWave.Semitone(5));
            int top = RebuildWave.Semitone(RebuildWave.MaxNotes - 1);
            Assert.AreEqual(top, RebuildWave.Semitone(50));
        }

        // ---------- the waiting state ----------
        static SimCore NewSim(int seed = 42)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        [Test]
        public void NotWaitingUntilTheEraIsReady()
        {
            var sim = NewSim();
            var w = EraWaiting.Read(sim.State, sim.PlaceName);
            Assert.IsFalse(w.waiting);
            Assert.AreEqual(0, w.pileLevel);
            Assert.IsEmpty(w.props);
        }

        [Test]
        public void WaitingForTheVillage()
        {
            var sim = NewSim();
            sim.ForceEraReady();
            var w = EraWaiting.Read(sim.State, "Glasswick");
            Assert.IsTrue(w.waiting);
            Assert.AreEqual(Era.Village, w.next);
            Assert.AreEqual(1, w.pileLevel);
            CollectionAssert.AreEquivalent(new[] { WaitingProp.SeedBaskets, WaitingProp.Stakes, WaitingProp.ClearedStrip }, w.props);
            Assert.AreEqual(WaitingLeader.AtFireLookingUp, w.leader);
            Assert.IsTrue(w.eldersGather);
            Assert.AreEqual("Watch them become a village", w.watchLabel);
            Assert.AreEqual("Glasswick is ready to become a village · Watch", w.glance);
            Assert.AreEqual("They'll wait for you", w.closeLine);
        }

        [Test]
        public void WaitingForTheTown()
        {
            var sim = NewSim();
            sim.ForceNextEra();
            sim.ForceEraReady();
            var w = EraWaiting.Read(sim.State, "Glasswick");
            Assert.AreEqual(Era.Town, w.next);
            CollectionAssert.Contains(w.props, WaitingProp.TimberPile);
            CollectionAssert.Contains(w.props, WaitingProp.MarketBell);
            Assert.AreEqual(WaitingLeader.PacesTheGreen, w.leader);
            Assert.AreEqual("Watch them become a town", w.watchLabel);
        }

        [Test]
        public void ThePilesGrowALittleWithTheWaitAndNeverShrink()
        {
            var sim = NewSim();
            sim.ForceEraReady();
            int last = 0;
            for (int m = 0; m < 400; m++)
            {
                int level = EraWaiting.Read(sim.State, "").pileLevel;
                Assert.GreaterOrEqual(level, last);
                Assert.LessOrEqual(level, EraWaiting.MaxPile);
                last = level;
                sim.State.month++; // time passes; the era still waits (the sim never advances it)
            }
            Assert.AreEqual(EraWaiting.MaxPile, last);
            Assert.AreEqual(1, EraWaiting.PileLevel(0));
            Assert.AreEqual(2, EraWaiting.PileLevel(12));
            Assert.AreEqual(4, EraWaiting.PileLevel(10_000));
            Assert.Less(EraWaiting.PileCount(1), EraWaiting.PileCount(4));
            Assert.IsTrue(EraWaiting.Grows(WaitingProp.SeedBaskets));
            Assert.IsFalse(EraWaiting.Grows(WaitingProp.Stakes));
        }

        [Test]
        public void TheWaitSurvivesSimStepsUntilWatched()
        {
            var sim = NewSim();
            sim.ForceEraReady();
            long readyAt = sim.State.eraReadyMonth;
            for (int m = 0; m < 24; m++) sim.Step();
            var w = EraWaiting.Read(sim.State, sim.PlaceName);
            Assert.IsTrue(w.waiting);
            Assert.AreEqual(sim.State.month - readyAt, w.monthsWaited);
            Assert.AreEqual(2, w.pileLevel);
        }

        [Test]
        public void NoWaitingInTheTownAndAWipeOfferOnlyOnGrimyGlass()
        {
            var sim = NewSim();
            sim.ForceNextEra(); sim.ForceNextEra();
            Assert.IsFalse(EraWaiting.Read(sim.State, "").waiting);
            Assert.IsFalse(EraWaiting.OfferWipeFirst(0.2f));
            Assert.IsTrue(EraWaiting.OfferWipeFirst(0.8f));
        }
    }
}
