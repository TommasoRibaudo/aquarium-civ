using System;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#231 phase 2: the life of the strangers who live apart. Their camp keeps clear of the settlement (and is nudged when a lot
    /// covers it), their day has its beats (asleep at night, at the fire at dusk), and the choice scene stands the travellers towards the
    /// home they would make.</summary>
    public class ApartLifeTests
    {
        static SimCore Grown(int seed, int months)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < months; m++) sim.Step();
            return sim;
        }

        [Test]
        public void TheCampStaysOnItsSpotWhileItIsClearAndMovesOffALotThatCoversIt()
        {
            var sim = Grown(7531, 96);
            var map = sim.Settlement.Map;
            foreach (var kind in new[] { ApartKind.WoodsCamp, ApartKind.ShoreHut })
            {
                var home = sim.ApartSpotPreview(kind);
                var spot = ApartLife.FreeSpot(map, home.x, home.z, out bool moved);
                Assert.IsTrue(ApartLife.FootprintFree(map, spot.x, spot.z), kind + ": the camp stands clear");
                if (!moved) Assert.AreEqual(home, spot, kind + ": on its home spot while that is clear");
            }
            // A lot on every cell round a spot: the camp moves to the nearest clear place.
            int onLot = Array.FindIndex(map.lot, x => x != 0);
            Assert.GreaterOrEqual(onLot, 0, "the box has a lot");
            float bx = SiteMap.CXc(onLot), bz = SiteMap.CZc(onLot);
            var at = ApartLife.FreeSpot(map, bx, bz, out bool nudged);
            Assert.IsTrue(nudged, "a spot on a lot is nudged");
            Assert.IsTrue(ApartLife.FootprintFree(map, at.x, at.z), "to a clear place");
            double movedBy = Math.Sqrt((at.x - bx) * (at.x - bx) + (at.z - bz) * (at.z - bz));
            for (int c = 0; c < SiteMap.N; c++)
            {
                double dc = Math.Sqrt((SiteMap.CXc(c) - bx) * (SiteMap.CXc(c) - bx) + (SiteMap.CZc(c) - bz) * (SiteMap.CZc(c) - bz));
                double fromGreen = Math.Sqrt((SiteMap.CXc(c) - map.greenX) * (SiteMap.CXc(c) - map.greenX) + (SiteMap.CZc(c) - map.greenZ) * (SiteMap.CZc(c) - map.greenZ));
                if (dc < movedBy - 1e-4 && fromGreen >= ApartLife.AwayFromGreen)
                    Assert.IsFalse(ApartLife.FootprintFree(map, SiteMap.CXc(c), SiteMap.CZc(c)), "the nearest clear place away from the green");
            }
        }

        [Test]
        public void NoCellUnderALotARingAPathOrTheWaterIsFree()
        {
            var sim = Grown(7531, 120);
            var map = sim.Settlement.Map;
            int lots = 0, paths = 0, water = 0;
            for (int c = 0; c < SiteMap.N; c++)
            {
                if (map.lot[c] != 0) { lots++; Assert.IsFalse(ApartLife.CellFree(map, c), "a lot"); }
                if (map.wear[c] >= PathNet.TTrail) { paths++; Assert.IsFalse(ApartLife.CellFree(map, c), "a path"); }
                if (map.Water(c)) { water++; Assert.IsFalse(ApartLife.CellFree(map, c), "the water"); }
            }
            Assert.Greater(lots, 0); Assert.Greater(water, 0);
            Debug.Log($"[ApartLife] lots {lots}, path cells {paths}, water {water}");
        }

        [Test]
        public void TheirDayHasItsBeats()
        {
            foreach (var kind in new[] { ApartKind.WoodsCamp, ApartKind.ShoreHut })
                for (int m = 0; m < 3; m++)
                {
                    Assert.AreEqual(ApartDoing.Sleep, ApartLife.Doing(kind, m, 2f), "asleep at night");
                    Assert.AreEqual(ApartDoing.Sleep, ApartLife.Doing(kind, m, 23f));
                    Assert.AreEqual(ApartDoing.SitAtFire, ApartLife.Doing(kind, m, 19f), "at the fire at dusk");
                    Assert.AreEqual(ApartDoing.SitAtFire, ApartLife.Doing(kind, m, 7f), "and in the morning");
                    Assert.AreNotEqual(ApartDoing.Sleep, ApartLife.Doing(kind, m, 10f), "up by day");
                }
            Assert.AreEqual(ApartDoing.Fish, ApartLife.Doing(ApartKind.ShoreHut, 0, 10f), "the shore family fishes");
            Assert.AreEqual(ApartDoing.Chop, ApartLife.Doing(ApartKind.WoodsCamp, 0, 10f), "the woods camp chops");
            Assert.AreEqual(ApartDoing.Wander, ApartLife.Doing(ApartKind.WoodsCamp, 1, 10f));
            // Seats ring the fire, apart from each other; a wanderer stays near the camp.
            var s0 = ApartLife.Seat((0f, 0f), (0f, 1f), 0, 3);
            var s1 = ApartLife.Seat((0f, 0f), (0f, 1f), 1, 3);
            Assert.AreEqual(ApartLife.SeatRadius, Math.Sqrt(s0.x * s0.x + s0.z * s0.z), 1e-4);
            Assert.Greater(Math.Sqrt((s0.x - s1.x) * (s0.x - s1.x) + (s0.z - s1.z) * (s0.z - s1.z)), 0.15);
            for (float h = 8f; h < 17f; h += 0.25f)
            {
                var w = ApartLife.WanderPoint((1f, 1f), 1, h, 42);
                Assert.Less(Math.Sqrt((w.x - 1f) * (w.x - 1f) + (w.z - 1f) * (w.z - 1f)), 0.41);
            }
        }

        [Test]
        public void TheTravellersWaitTowardsTheHomeTheyWouldMake()
        {
            const float hx = 3f, hz = 2.2f;
            var fire = (0f, 0f);
            var far = ChoiceScenes.HomeWard(fire, (2.5f, 1.5f), hx, hz, out bool ok);
            Assert.IsTrue(ok);
            float d = (float)Math.Sqrt(far.x * far.x + far.z * far.z);
            Assert.LessOrEqual(d, ChoiceScenes.EdgeMax + 1e-4f, "both sides in one look");
            Assert.GreaterOrEqual(d, ChoiceScenes.EdgeMin - 1e-4f, "clearly apart");
            Assert.Greater(far.x * 2.5f + far.z * 1.5f, 0f, "towards the home");
            Assert.LessOrEqual(Math.Abs(far.x), hx - ChoiceScenes.EdgeMargin + 1e-4f); Assert.LessOrEqual(Math.Abs(far.z), hz - ChoiceScenes.EdgeMargin + 1e-4f);
            ChoiceScenes.HomeWard(fire, (0.2f, 0.1f), hx, hz, out bool near);
            Assert.IsFalse(near, "a home by the fire doesn't read as apart");
        }

        [Test]
        public void TheStrangersSceneFacesTheirHomeAndNeverWritesTheSim()
        {
            foreach (int seed in new[] { 7531, 1, 2, 6, 7, 10 })
            {
                var sim = Grown(seed, 72);
                Dilemma d = null;
                for (int tries = 0; tries < 12 && d == null; tries++)
                {
                    d = sim.DebugRaiseDilemma("camp_travellers");
                    if (d == null) for (int m = 0; m < 12; m++) sim.Step();
                }
                if (d == null) continue;
                var planner = LifePlanner.Wired(sim);
                var v = sim.ViewOfDilemma(d);
                string before = JsonUtility.ToJson(sim.State);
                var st = planner.StageChoice(v, new ViewTime(sim.State.month, 10f), 1f, 0f, 0f, -1f);
                Assert.IsNotNull(st);
                Assert.AreEqual(before, JsonUtility.ToJson(sim.State), "staging never writes the sim");
                var spot = sim.ApartSpotPreview(ApartKind.WoodsCamp);
                var home = ApartLife.FreeSpot(sim.Settlement.Map, spot.x, spot.z, out _); // the camp as drawn
                if (!st.hasHome) { Debug.Log($"[ApartLife] seed {seed}: home too near the fire, the edge it is"); continue; }
                Assert.AreEqual(1, st.homeOption, "the full-packs option");
                Assert.AreEqual(home.x, st.homeX, 1e-4f);
                var fire = (st.sides[0].x, st.sides[0].z);
                var trav = (st.sides[1].x, st.sides[1].z);
                float toHome = (home.x - fire.Item1) * (trav.Item1 - fire.Item1) + (home.z - fire.Item2) * (trav.Item2 - fire.Item2);
                Assert.Greater(toHome, 0f, $"seed {seed}: the travellers wait on the way to their camp");
                // the answer then makes the group at that very spot
                Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
                var g = sim.ApartOf(d);
                Assert.AreEqual(spot.x, g.x, 1e-4f); Assert.AreEqual(spot.z, g.z, 1e-4f);
                return;
            }
            Assert.Fail("camp_travellers held in no seed");
        }
    }
}
