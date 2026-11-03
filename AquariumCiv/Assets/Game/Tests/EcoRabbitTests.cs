using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Tests
{
    /// <summary>P8 polish #282 phase 1: rabbit tracks, the rabbits on screen (pure model), the first rabbit sign, the hunt catch, the cheats.</summary>
    public class EcoRabbitTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static SimCore OffWorld() { var cfg = new SimConfig(); return new SimCore(cfg, SimCore.NewWorld(3, cfg)); }

        static readonly (float dx, float dz)[] Spots = { (0.9f, 0f), (-1.2f, 0.8f), (1.6f, -0.9f), (-0.4f, -1.3f), (2.2f, 0.6f), (0.2f, 1.4f), (-2f, -0.3f), (1f, 1.2f) };

        static void MistSpot(SimCore sim, int k) { var s = Spots[k % Spots.Length]; sim.DebugMistBig(sim.EcoGround.campX + s.dx, sim.EcoGround.campZ + s.dz); }

        /// <summary>Mists one more spot every other month until a rabbit stands in the box.</summary>
        static void GrowUntilRabbit(SimCore sim, int maxMonths = 120)
        {
            for (int m = 0; m < maxMonths && !sim.RabbitsSeen; m++)
            {
                if (m % 2 == 0 && m < 80) MistSpot(sim, m / 2);
                sim.Advance(1.0 / 12);
            }
        }

        /// <summary>A meadow with a herd to look at, whatever the meadow says.</summary>
        static SimCore Meadow(int seed = 7)
        {
            var sim = World(seed);
            for (int i = 0; i < 12; i++) { MistSpot(sim, i); sim.Advance(1.0 / 12); }
            sim.DebugAddRabbits(8);
            return sim;
        }

        [Test]
        public void TracksComeAtLeastASeasonBeforeTheFirstRabbit()
        {
            var sim = World();
            GrowUntilRabbit(sim);
            long tracks = sim.EcoMilestoneMonth(EcoMilestones.RabbitTracks), rabbit = sim.EcoMilestoneMonth(EcoMilestones.FirstRabbit);
            Assert.GreaterOrEqual(tracks, 0, "tracks were seen");
            Assert.GreaterOrEqual(rabbit, 0, "a rabbit came");
            Assert.GreaterOrEqual(rabbit - tracks, 3, "at least a season between the tracks and the first rabbit");
            Assert.IsTrue(sim.EcoRabbitTracksSeen);
            Assert.IsTrue(sim.State.chronicle.Exists(c => c.kind == "eco_tracks"), "the chronicle tells of the tracks");
            Assert.IsTrue(sim.State.chronicle.Exists(c => c.kind == "eco_rabbit"));
        }

        [Test]
        public void TrackCells_AreGreenCellsOutsideTheYard_AndGoAway()
        {
            var sim = World();
            for (int m = 0; m < 60 && !sim.EcoRabbitTracksSeen; m++)
            {
                if (m % 2 == 0) MistSpot(sim, m / 2);
                sim.Advance(1.0 / 12);
            }
            Assert.IsTrue(sim.EcoRabbitTracksSeen);
            Assert.AreEqual(0, sim.EcoRabbitCount, "no rabbit yet when the tracks show");
            var cells = sim.EcoRabbitTrackCells;
            Assert.Greater(cells.Count, 0);
            foreach (int c in cells)
            {
                Assert.IsTrue(RabbitHerd.IsGreen(sim.EcoCoverAt(c)), "tracks lie in the grass");
                Assert.IsFalse(sim.EcoGround.yard[c], "not in the yard");
            }
            for (int m = 0; m < 48; m++) { if (m < 20 && m % 2 == 0) MistSpot(sim, m); sim.Advance(1.0 / 12); }
            Assert.AreEqual(0, sim.EcoRabbitTrackCells.Count, "the tracks fade some seasons after the rabbits are about");
        }

        [Test]
        public void ShownCount_FollowsTheSim_AndCaps()
        {
            var sim = World();
            sim.DebugMistBig(sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ);
            Assert.AreEqual(0, sim.EcoRabbitsShown());
            sim.DebugAddRabbits(3);
            Assert.AreEqual(Math.Min(12, sim.EcoRabbitCount), sim.EcoRabbitsShown());
            sim.DebugAddRabbits(50);
            Assert.Greater(sim.EcoRabbitCount, 12);
            Assert.AreEqual(12, sim.EcoRabbitsShown(), "capped for the phone");
            Assert.AreEqual(5, sim.EcoRabbitsShown(5));
            Assert.AreEqual(0, RabbitHerd.Shown(0.2f));
            Assert.AreEqual(3, RabbitHerd.Shown(2.6f));
            var f = new RabbitHerd.Field();
            Assert.IsTrue(f.Sync(sim.EcoRabbitsShown()));
            Assert.AreEqual(12, f.Alive.Count);
            Assert.IsFalse(f.Sync(12));
            f.Sync(4);
            Assert.AreEqual(4, f.Alive.Count);
        }

        [Test]
        public void Rabbits_StayOnGreenCells_AllTheTime()
        {
            var sim = Meadow();
            var f = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(f);
            f.Sync(sim.EcoRabbitsShown());
            Assert.Greater(f.Alive.Count, 0);
            Assert.IsTrue(f.HasGreen);
            int hops = 0, grazes = 0, sits = 0;
            foreach (int id in f.Alive)
                for (double t = 0; t < 240; t += 0.25)
                {
                    var p = f.Sample(id, t);
                    int cell = SiteMap.CellAt(p.x, p.z);
                    Assert.GreaterOrEqual(cell, 0, "inside the box");
                    Assert.IsTrue(RabbitHerd.IsGreen(sim.EcoCoverAt(cell)), "rabbit " + id + " at t=" + t + " stands on green");
                    if (p.act == RabbitAct.Hop) { hops++; Assert.GreaterOrEqual(p.y, 0f); }
                    else if (p.act == RabbitAct.Graze) grazes++;
                    else if (p.act == RabbitAct.SitUp) sits++;
                }
            Assert.Greater(hops, 0); Assert.Greater(grazes, 0); Assert.Greater(sits, 0);
        }

        [Test]
        public void Sample_IsAPureFunctionOfTimeAndSeed()
        {
            var a = Meadow(); var b = Meadow();
            var fa = new RabbitHerd.Field(); var fb = new RabbitHerd.Field();
            a.EcoRabbitsRefresh(fa); b.EcoRabbitsRefresh(fb);
            fa.Sync(5); fb.Sync(5);
            for (double t = 0; t < 60; t += 1.7)
                foreach (int id in fa.Alive)
                {
                    var p = fa.Sample(id, t); var q = fb.Sample(id, t); var again = fa.Sample(id, t);
                    Assert.AreEqual(p.x, q.x); Assert.AreEqual(p.z, q.z); Assert.AreEqual(p.act, q.act);
                    Assert.AreEqual(p.x, again.x); Assert.AreEqual(p.z, again.z);
                }
            var first = fa.Sample(0, 0); bool moved = false;
            for (double t = 1; t < 120 && !moved; t += 1) { var p = fa.Sample(0, t); moved = Math.Abs(p.x - first.x) + Math.Abs(p.z - first.z) > 0.02f; }
            Assert.IsTrue(moved, "they move");
        }

        [Test]
        public void TheWarren_IsAtTheMeadowsEdge_NotInTheYard()
        {
            var sim = World();
            sim.DebugMistBig(sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ);
            sim.DebugAddRabbits(4);
            var f = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(f);
            Assert.GreaterOrEqual(f.WarrenCell, 0);
            Assert.IsFalse(sim.EcoGround.yard[f.WarrenCell], "the warren is not in the yard");
        }

        [Test]
        public void FleeRule_RunsAwayFromThePeople_AndStaysOnGreen()
        {
            var sim = Meadow();
            var f = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(f); f.Sync(1);
            var basePose = f.Sample(0, 3);
            var px = new[] { basePose.x - 0.3f }; var pz = new[] { basePose.z };
            Assert.IsTrue(RabbitHerd.Scare.FleeDirection(basePose.x, basePose.z, px, pz, RabbitHerd.Scare.AlertRadius, out float dx, out float dz));
            Assert.Greater(dx, 0.9f, "away is east");
            Assert.IsFalse(RabbitHerd.Scare.FleeDirection(basePose.x, basePose.z, new[] { basePose.x - 2f }, new[] { basePose.z }, RabbitHerd.Scare.AlertRadius, out _, out _), "far people do not matter");
            var scare = new RabbitHerd.Scare();
            for (int i = 0; i < 20; i++)
            {
                var p = scare.Step(0.05f, basePose, px, pz, f);
                Assert.IsTrue(f.IsGreenAt(p.x, p.z), "bolts, but only over green");
            }
            Assert.IsTrue(scare.Fleeing);
            Assert.GreaterOrEqual(scare.offX, 0f, "moved away from the west, or stayed put at the green's edge");
            var none = new float[0];
            for (int i = 0; i < 400; i++)
            {
                var p = scare.Step(0.05f, basePose, none, none, f);
                Assert.IsTrue(f.IsGreenAt(p.x, p.z));
            }
            Assert.IsFalse(scare.Fleeing);
            Assert.AreEqual(0f, scare.offX, 1e-4f); Assert.AreEqual(0f, scare.offZ, 1e-4f);
        }

        [Test]
        public void FirstRabbitEvent_FiresOnce_WithAnArrivalCell()
        {
            var sim = World();
            int fired = 0, cell = -1;
            sim.RabbitArrived += c => { fired++; cell = c; };
            Assert.IsFalse(sim.EcoRabbitArrivalPending);
            GrowUntilRabbit(sim);
            Assert.AreEqual(1, fired);
            Assert.GreaterOrEqual(cell, 0);
            Assert.AreEqual(cell, sim.EcoRabbitWarren);
            Assert.IsTrue(RabbitHerd.IsGreen(sim.EcoCoverAt(cell)));
            Assert.IsTrue(sim.EcoRabbitArrivalPending);
            Assert.IsTrue(sim.AckRabbitArrival());
            Assert.IsFalse(sim.AckRabbitArrival(), "once");
            for (int m = 0; m < 24; m++) sim.Advance(1.0 / 12);
            Assert.AreEqual(1, fired, "the event does not fire again");
            Assert.IsFalse(sim.EcoRabbitArrivalPending);
        }

        [Test]
        public void Nearest_PicksTheClosestPerson()
        {
            Assert.AreEqual(1, RabbitHerd.Nearest(new[] { 5f, 1f, 3f }, new[] { 0f, 0f, 0f }, 0.8f, 0f));
            Assert.AreEqual(-1, RabbitHerd.Nearest(new float[0], new float[0], 0f, 0f));
        }

        [Test]
        public void Catch_LowersTheCount_AndItRegrows()
        {
            var sim = Meadow();
            for (int i = 0; i < 30; i++) { MistSpot(sim, i); sim.Advance(1.0 / 12); }
            float before = sim.EcoRabbits;
            Assert.GreaterOrEqual(before, 3f);
            Assert.IsTrue(sim.CatchRabbit());
            Assert.AreEqual(before - 1f, sim.EcoRabbits, 1e-4f);
            Assert.AreEqual(1, sim.EcoRabbitsCaught);
            var f = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(f);
            f.Sync(RabbitHerd.Shown(before));
            int hunted = f.NearestTo(0f, 0f, 5.0, out float hx, out float hz);
            Assert.GreaterOrEqual(hunted, 0);
            Assert.IsTrue(f.PositionOf(hunted, 5.0, out float qx, out float qz));
            Assert.AreEqual(hx, qx); Assert.AreEqual(hz, qz);
            Assert.IsTrue(f.Remove(hunted));
            Assert.IsFalse(f.PositionOf(hunted, 5.0, out _, out _), "the caught rabbit is gone from the box");
            f.Sync(sim.EcoRabbitsShown());
            Assert.IsFalse(System.Linq.Enumerable.Contains(f.Alive, hunted), "its id is never reused");
            float low = sim.EcoRabbits;
            for (int m = 0; m < 48; m++) sim.Advance(1.0 / 12);
            Assert.Greater(sim.EcoRabbits, low, "the model regrows it toward the meadow");
        }

        [Test]
        public void Catch_WithNoRabbits_DoesNothing()
        {
            var sim = World();
            Assert.IsFalse(sim.CatchRabbit());
            Assert.AreEqual(0, sim.EcoRabbitsCaught);
        }

        [Test]
        public void Cheats_TracksNow_And_AddRabbits()
        {
            var sim = World();
            sim.DebugMistBig(sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ);
            Assert.IsFalse(sim.EcoRabbitTracksSeen);
            Assert.IsTrue(sim.DebugRabbitTracksNow());
            Assert.IsTrue(sim.EcoRabbitTracksSeen);
            Assert.Greater(sim.EcoRabbitTrackCells.Count, 0);
            Assert.AreEqual(0, sim.EcoRabbitCount, "tracks only");
            Assert.IsFalse(sim.DebugRabbitTracksNow(), "a second time is a no-op");
            int n = sim.DebugAddRabbits(5);
            Assert.GreaterOrEqual(n, 5);
            Assert.IsTrue(sim.RabbitsSeen);
            Assert.IsTrue(sim.EcoRabbitArrivalPending);
            var s2 = World(3);
            s2.DebugMistBig(s2.EcoGround.campX + 0.9f, s2.EcoGround.campZ);
            s2.DebugAddRabbits(3);
            Assert.IsTrue(s2.EcoRabbitTracksSeen, "+N goes through the tracks and the first-rabbit sign");
            Assert.GreaterOrEqual(s2.EcoRabbitWarren, 0);
        }

        [Test]
        public void SurvivesSaveAndLoad()
        {
            var sim = World();
            sim.DebugMistBig(sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ);
            sim.DebugAddRabbits(4);
            var copy = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(sim.EcoRabbitWarren, copy.EcoRabbitWarren);
            Assert.AreEqual(sim.EcoRabbitTrackCells.Count, copy.EcoRabbitTrackCells.Count);
            Assert.AreEqual(sim.EcoRabbitArrivalPending, copy.EcoRabbitArrivalPending);
        }

        [Test]
        public void EcologyOff_NothingChanges()
        {
            var sim = OffWorld();
            Assert.AreEqual(0, sim.EcoRabbitCount);
            Assert.AreEqual(0, sim.EcoRabbitsShown());
            Assert.IsFalse(sim.EcoRabbitTracksSeen);
            Assert.AreEqual(0, sim.EcoRabbitTrackCells.Count);
            Assert.IsFalse(sim.CatchRabbit());
            Assert.IsFalse(sim.DebugRabbitTracksNow());
            Assert.AreEqual(0, sim.DebugAddRabbits(5));
            Assert.IsFalse(sim.AckRabbitArrival());
            Assert.AreEqual(-1, sim.EcoRabbitWarren);
            sim.Advance(2);
            Assert.AreEqual(0UL, sim.EcoFingerprint);
            Assert.AreEqual(0, sim.EcoRabbitCount);
        }
    }
}
