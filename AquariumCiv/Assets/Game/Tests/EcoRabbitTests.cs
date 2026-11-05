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

        // ---------------------------------------------------------------- #333: a tap startles, they roam and spread

        static RabbitHerd.Field Herd(out SimCore sim, int rabbits = 8, int seed = 7)
        {
            sim = Meadow(seed);
            if (rabbits > 8) sim.DebugAddRabbits(rabbits - 8);
            var f = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(f);
            f.Sync(rabbits);
            return f;
        }

        static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        /// <summary>Mean distance from each rabbit to its nearest neighbour at time t.</summary>
        static float MeanNearest(RabbitHerd.Field f, double t)
        {
            var ids = new List<int>(f.Alive); float sum = 0f;
            foreach (int a in ids)
            {
                var pa = f.Sample(a, t); float best = float.MaxValue;
                foreach (int b in ids) if (b != a) { var pb = f.Sample(b, t); best = Math.Min(best, Dist(pa.x, pa.z, pb.x, pb.z)); }
                sum += best;
            }
            return sum / ids.Count;
        }

        [Test]
        public void Tap_StartlesTheRabbitAndTheNearOnes_ToFarAway_AndTheyDoNotComeBack()
        {
            foreach (int seed in new[] { 7, 11, 23 })
            {
                var f = Herd(out var sim, 10, seed);
                double t0 = 30.0;
                var victim = f.Sample(f.Alive[0], t0);
                float tx = victim.x, tz = victim.z; // right on it (0.08 off, another rabbit can be nearer)
                int near = 0;
                foreach (int id in f.Alive) { var p = f.Sample(id, t0); if (Dist(p.x, p.z, tx, tz) <= RabbitHerd.NearRadius) near++; }
                var started = new List<RabbitHerd.Field.Startled>();
                int n = f.Startle(tx, tz, t0, null, started);
                Assert.AreEqual(near, n, "the rabbit and those near the tap, no others");
                Assert.AreEqual(f.Alive[0], started[0].id, "the one tapped first");
                for (int i = 1; i < n; i++) Assert.GreaterOrEqual(started[i].until, started[0].until, "the near ones go later");
                Assert.AreEqual(10, f.Alive.Count, "a tap removes nobody");
                foreach (var s in started)
                {
                    Assert.GreaterOrEqual(s.until, t0 + RabbitHerd.Scare.FreezeSeconds - 1e-6, "a beat to freeze first");
                    float far = 0f; int back = 0, samples = 0;
                    for (double t = s.until + 5; t < t0 + 150; t += 0.5)
                    {
                        var p = f.Sample(s.id, t); samples++;
                        float d = Dist(p.x, p.z, tx, tz); far += d;
                        if (d < 0.3f) back++;
                        Assert.IsTrue(f.IsGreenAt(p.x, p.z), "settles on green");
                    }
                    Assert.AreEqual(0, back, $"seed {seed} rabbit {s.id} never returns to the tap's spot");
                    Assert.Greater(far / samples, 0.45f, $"seed {seed} rabbit {s.id} lives far from the tap");
                }
            }
        }

        /// <summary>The user (2026-11-05): a knock on the glass startles the rabbits close to the knocked spot; no rabbit has to be at
        /// the spot, and the ones farther off stay.</summary>
        [Test]
        public void Knock_StartlesTheRabbitsNearTheKnockedSpot_NotTheFarOnes_AndTheyDoNotComeBack()
        {
            foreach (int seed in new[] { 7, 11, 23 })
            {
                var f = Herd(out _, 10, seed);
                double t0 = 30.0;
                var p0 = f.Sample(f.Alive[0], t0);
                // the knock lands a little way off the nearest rabbit (as the glass is), never on it
                float kx = p0.x + 0.6f, kz = p0.z;
                var near = new List<int>();
                foreach (int id in f.Alive) { var p = f.Sample(id, t0); if (Dist(p.x, p.z, kx, kz) <= RabbitHerd.KnockRadius) near.Add(id); }
                Assume.That(near.Count, Is.GreaterThan(0));
                var started = new List<RabbitHerd.Field.Startled>();
                int n = f.Knock(kx, kz, t0, null, started);
                Assert.AreEqual(near.Count, n, $"seed {seed}: every rabbit within reach of the knock, and no others");
                CollectionAssert.AreEquivalent(near, started.ConvertAll(s => s.id));
                Assert.AreEqual(10, f.Alive.Count, "a knock removes nobody");
                foreach (var s in started)
                {
                    Assert.GreaterOrEqual(s.until, t0 + RabbitHerd.Scare.FreezeSeconds - 1e-6, "a beat to freeze first");
                    int back = 0;
                    for (double t = s.until + 5; t < t0 + 150; t += 0.5)
                    {
                        var p = f.Sample(s.id, t);
                        if (Dist(p.x, p.z, kx, kz) < 0.35f) back++;
                        Assert.IsTrue(f.IsGreenAt(p.x, p.z), "settles on green");
                    }
                    Assert.AreEqual(0, back, $"seed {seed} rabbit {s.id} doesn't come back to the knocked spot");
                }
            }
        }

        /// <summary>The user (2026-11-05, "the running around sucks"): a scheduled spot that jumps (the green round its home changed)
        /// must not pop the rabbit across the box; it hops over.</summary>
        [Test]
        public void Scare_AJumpInTheSchedule_IsHoppedOver_NotAPop()
        {
            var f = Herd(out _, 3);
            var sc = new RabbitHerd.Scare();
            var none = new float[0];
            var a = new RabbitPose { x = -1.0f, z = 0.0f, act = RabbitAct.Graze };
            var b = new RabbitPose { x = -0.3f, z = 0.0f, act = RabbitAct.Graze };
            var last = sc.Step(0.03f, a, none, none, f);
            float maxStep = 0f;
            for (int i = 0; i < 200; i++)
            {
                var p = sc.Step(0.03f, b, none, none, f);
                maxStep = Math.Max(maxStep, Dist(p.x, p.z, last.x, last.z));
                last = p;
            }
            Assert.Less(maxStep, 0.05f, "no pop: at most a hop's worth a step");
            Assert.AreEqual(b.x, last.x, 0.02f, "and it gets there");
        }

        [Test]
        public void Knock_FarFromEveryRabbit_DoesNothing()
        {
            var f = Herd(out _, 6);
            var list = new List<RabbitHerd.Field.Startled>();
            Assert.AreEqual(0, f.Knock(-9f, -9f, 12.0, null, list));
            Assert.IsEmpty(list);
        }

        [Test]
        public void Tap_FarFromEveryRabbit_DoesNothing_AndSkippedRabbitsStay()
        {
            var f = Herd(out _, 6);
            double t0 = 12.0;
            var p0 = f.Sample(f.Alive[0], t0);
            var list = new List<RabbitHerd.Field.Startled>();
            Assert.AreEqual(0, f.Startle(-9f, -9f, t0, null, list), "no rabbit near the tap");
            var before = f.Sample(f.Alive[0], t0 + 60);
            Assert.AreEqual(0, f.Startle(p0.x, p0.z, t0, id => true, list), "rabbits a hunter is stalking are left alone");
            var after = f.Sample(f.Alive[0], t0 + 60);
            Assert.AreEqual(before.x, after.x, 1e-4f);
            Assert.AreEqual(before.z, after.z, 1e-4f);
        }

        [Test]
        public void Startle_TouchesNoSimRandomStream_AndIsRepeatable()
        {
            var a = Herd(out var sa, 8); var b = Herd(out var sb, 8);
            ulong fpA = sa.EcoFingerprint;
            var la = new List<RabbitHerd.Field.Startled>(); var lb = new List<RabbitHerd.Field.Startled>();
            var p = a.Sample(a.Alive[0], 20);
            a.Startle(p.x, p.z, 20, null, la); b.Startle(p.x, p.z, 20, null, lb);
            Assert.Greater(la.Count, 0);
            Assert.AreEqual(la.Count, lb.Count);
            for (int i = 0; i < la.Count; i++) Assert.AreEqual(la[i].refuge, lb[i].refuge);
            Assert.AreEqual(fpA, sa.EcoFingerprint, "the sim never saw it");
            sa.Advance(1.0 / 12); sb.Advance(1.0 / 12);
            Assert.AreEqual(sa.EcoFingerprint, sb.EcoFingerprint);
        }

        [Test]
        public void ManyTaps_RemoveNoRabbit_AndTheHerdStaysOnGreen()
        {
            var f = Herd(out _, 12);
            var list = new List<RabbitHerd.Field.Startled>();
            double t = 10;
            for (int k = 0; k < 40; k++)
            {
                t += 7;
                var p = f.Sample(f.Alive[k % f.Alive.Count], t);
                f.Startle(p.x, p.z, t, null, list);
                Assert.AreEqual(12, f.Alive.Count);
                foreach (int id in f.Alive) { var q = f.Sample(id, t + 3); Assert.IsTrue(f.IsGreenAt(q.x, q.z), $"k {k} id {id} at {q.x},{q.z} act {q.act} cell {SiteMap.CellAt(q.x, q.z)}"); }
            }
        }

        const float SpreadFloor = 0.25f;

        [Test]
        public void Herd_IsSpreadOut_OverALongRun_AndRoamsWide()
        {
            foreach (int seed in new[] { 7, 11, 23 })
            {
                var f = Herd(out _, 8, seed);
                float sum = 0f; int n = 0, wide = 0;
                var ids = new List<int>(f.Alive);
                var x0 = new float[ids.Count]; var x1 = new float[ids.Count]; var z0 = new float[ids.Count]; var z1 = new float[ids.Count];
                for (int i = 0; i < ids.Count; i++) { x0[i] = z0[i] = float.MaxValue; x1[i] = z1[i] = float.MinValue; }
                for (double t = 0; t < 3600; t += 5)
                {
                    sum += MeanNearest(f, t); n++;
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var p = f.Sample(ids[i], t);
                        x0[i] = Math.Min(x0[i], p.x); x1[i] = Math.Max(x1[i], p.x); z0[i] = Math.Min(z0[i], p.z); z1[i] = Math.Max(z1[i], p.z);
                    }
                }
                for (int i = 0; i < ids.Count; i++) if (Dist(x0[i], z0[i], x1[i], z1[i]) > 0.7f) wide++;
                Assert.Greater(sum / n, SpreadFloor, $"seed {seed}: mean nearest-neighbour distance over an hour");
                Assert.GreaterOrEqual(wide, ids.Count - 1, $"seed {seed}: most rabbits roam over more than 0.7 of the box");
            }
        }

        [Test]
        public void Scare_Hold_FreezesThenRunsToTheNewPlace_OnGreen()
        {
            var f = Herd(out _, 3);
            double t0 = 40;
            var p = f.Sample(f.Alive[0], t0);
            var list = new List<RabbitHerd.Field.Startled>();
            f.Startle(p.x + 0.1f, p.z, t0, null, list);
            var s = list[0];
            var sc = new RabbitHerd.Scare();
            sc.Hold(p.x, p.z, p.x + 0.1f, p.z, s.until, RabbitHerd.Scare.FreezeSeconds, SiteMap.CXc(s.refuge), SiteMap.CZc(s.refuge));
            Assert.IsTrue(sc.Fleeing);
            var none = new float[0];
            double now = t0; bool sat = false, ran = false; RabbitPose last = default;
            for (int i = 0; i < 600; i++)
            {
                now += 0.05;
                var b = f.Sample(s.id, now);
                last = sc.Step(0.05f, b, none, none, f, now);
                if (!sc.Running) Assert.IsTrue(f.IsGreenAt(last.x, last.z), "on green when not running (the run may cross a strip of bare earth: no more jumps back onto the green)");
                if (now < s.until - 0.06) { Assert.AreEqual(RabbitAct.SitUp, last.act); Assert.AreEqual(p.x, last.x, 1e-4f); sat = true; }
                else if (sc.Running) ran = true;
                if (!sc.Fleeing) break;
            }
            Assert.IsTrue(sat && ran, "froze, then ran");
            Assert.IsFalse(sc.Fleeing, "and settled");
            Assert.Greater(Dist(last.x, last.z, p.x + 0.1f, p.z), 0.5f, "it ran far from the tap");
            Assert.AreEqual(SiteMap.CXc(s.refuge), last.x, 0.05f, "to its refuge");
            // then it sits looking back a moment and hops into its new home's day
            for (int i = 0; i < 600; i++) { now += 0.05; last = sc.Step(0.05f, f.Sample(s.id, now), none, none, f, now); }
            var home = f.Sample(s.id, now);
            Assert.AreEqual(home.x, last.x, 0.05f, "back in its day at the new home");
            Assert.AreEqual(home.z, last.z, 0.05f);
        }

        [Test]
        public void Hunt_StillFindsARabbit_AfterTaps()
        {
            var f = Herd(out _, 6);
            var list = new List<RabbitHerd.Field.Startled>();
            var p = f.Sample(f.Alive[0], 20);
            f.Startle(p.x, p.z, 20, null, list);
            int id = f.NearestTo(0f, 0f, 60.0, out float hx, out float hz);
            Assert.GreaterOrEqual(id, 0);
            Assert.IsTrue(f.PositionOf(id, 60.0, out float qx, out float qz));
            Assert.AreEqual(hx, qx); Assert.AreEqual(hz, qz);
            Assert.IsTrue(f.Remove(id), "a catch still takes it out");
        }
    }
}
