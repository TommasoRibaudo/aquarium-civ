using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#700 (B27): the take-down and set-up beat's rules (when, who) and its plan (timing, stands, the rack's parts), all pure.</summary>
    public class RackMoveTests
    {
        static RackMovePlan Fish(int cargo = 6, int fresh = 2, float oldA = 1.449f, float newA = 2.1f)
            => new RackMovePlan(RackKind.Fish, 0.3f, -0.2f, oldA, newA, FishRackSpot.Distance, FishRackSpot.HalfLength, 0.19f, cargo, fresh);

        // ------------------------------------------------------------ when
        [TestCase(DayPhase.Evening, 6, true)]
        [TestCase(DayPhase.Night, 6, true)]
        [TestCase(DayPhase.Morning, 6, false)]
        [TestCase(DayPhase.Midday, 6, false)]
        [TestCase(DayPhase.Afternoon, 6, false)]
        [TestCase(DayPhase.Dawn, 6, false)]
        [TestCase(DayPhase.Morning, 0, true)]   // by day an empty rack may move
        [TestCase(DayPhase.Afternoon, 0, true)]
        public void Quiet_IsDuskOrNight_OrAnEmptyRack(DayPhase phase, int cargo, bool expected)
            => Assert.AreEqual(expected, RackMoveRules.Quiet(phase, cargo, false));

        [Test]
        public void TheCheatForcesAnyHour()
            => Assert.IsTrue(RackMoveRules.Quiet(DayPhase.Midday, 9, true));

        [Test]
        public void NeverWhileSomeoneHangsOrTakesDown_NorWhileAnotherRackMoves_NorWithoutAFreeSpot()
        {
            Assert.IsTrue(RackMoveRules.CanStart(DayPhase.Evening, 5, false, false, false, true));
            Assert.IsFalse(RackMoveRules.CanStart(DayPhase.Evening, 5, false, true, false, true), "someone hangs or takes down");
            Assert.IsFalse(RackMoveRules.CanStart(DayPhase.Night, 5, false, false, true, true), "one rack beat at a time");
            Assert.IsFalse(RackMoveRules.CanStart(DayPhase.Night, 5, false, false, false, false), "no free spot: the rack keeps its place and there is no beat");
            Assert.IsFalse(RackMoveRules.CanStart(DayPhase.Midday, 5, false, false, false, true), "by day only when empty");
            Assert.IsFalse(RackMoveRules.CanStart(DayPhase.Midday, 5, true, true, false, true), "the cheat does not override someone mid-hang");
        }

        // ------------------------------------------------------------ who
        static List<RackMoveRules.Candidate> Crowd(params (int key, bool adult, bool free, float d)[] c)
        {
            var l = new List<RackMoveRules.Candidate>();
            foreach (var x in c) l.Add(new RackMoveRules.Candidate(x.key, x.adult, x.free, x.d));
            return l;
        }

        [Test]
        public void TheKeeperLifts_TheNearestOtherGrownUpCarries()
        {
            var c = Crowd((1, true, true, 0.9f), (2, true, true, 0.1f), (3, true, true, 0.4f), (4, false, true, 0.0f));
            Assert.IsTrue(RackMoveRules.PickCrew(c, 1, out int lifter, out int carrier));
            Assert.AreEqual(1, lifter, "the rack-keeper lifts");
            Assert.AreEqual(2, carrier, "the nearest idle grown-up carries; a kid never does");
        }

        [Test]
        public void ABusyKeeper_IsReplacedByTheNearestFreeGrownUp_AndNobodyIsPickedTwice()
        {
            var c = Crowd((1, true, false, 0.0f), (2, true, true, 0.3f), (3, true, true, 0.2f));
            Assert.IsTrue(RackMoveRules.PickCrew(c, 1, out int lifter, out int carrier));
            Assert.AreEqual(3, lifter);
            Assert.AreEqual(2, carrier);
            Assert.AreNotEqual(lifter, carrier);
        }

        [Test]
        public void FewerThanTwoFreeGrownUps_NoBeat()
        {
            Assert.IsFalse(RackMoveRules.PickCrew(Crowd((1, true, true, 0f), (2, true, false, 0f), (3, false, true, 0f)), 1, out _, out _));
            Assert.IsFalse(RackMoveRules.PickCrew(Crowd(), 1, out _, out _));
        }

        [Test]
        public void NegativeCrowdKeys_AreRealPeople()
        {   // crowd members have negative keys: -1 is somebody, not "nobody"
            var c = Crowd((-1, true, true, 0.2f), (-2, true, true, 0.1f));
            Assert.IsTrue(RackMoveRules.PickCrew(c, int.MinValue, out int lifter, out int carrier));
            Assert.AreEqual(-2, lifter); Assert.AreEqual(-1, carrier);
        }

        [Test]
        public void TiesGoToTheLowerKey()
        {
            var c = Crowd((5, true, true, 0.3f), (4, true, true, 0.3f), (6, true, true, 0.3f));
            Assert.IsTrue(RackMoveRules.PickCrew(c, int.MinValue, out int lifter, out int carrier));
            Assert.AreEqual(4, lifter); Assert.AreEqual(5, carrier);
        }

        // ------------------------------------------------------------ the plan's timing
        [Test]
        public void ALoadedRack_TakesAboutTwentyTwoSeconds_WithTheSpecsPhases()
        {
            var p = Fish();
            Assert.That(p.total, Is.InRange(19f, 26f), "about 20 s real (B27)");
            Assert.That(p.tPull - 0f - RackMovePlan.AnticSeconds, Is.InRange(5f, 6.5f), "take-down 5-6 s (cargo, the ridge, the frames)");
            Assert.That(p.carrySeconds, Is.InRange(3f, 8f), "carry 3-8 s by distance");
            Assert.AreEqual(4f, p.tPlant - p.tCarry, 1e-3f, "plant 4 s");
            Assert.AreEqual(3f, p.tRaise - p.tPlant, 1e-3f, "raise 3 s");
        }

        [Test]
        public void AnEmptyRack_SkipsTheCargoAndTheHang()
        {
            var p = Fish(cargo: 0, fresh: 0);
            Assert.AreEqual(0f, p.cargoSeconds); Assert.AreEqual(0f, p.hangSeconds);
            Assert.AreEqual(p.tRaise, p.tHang, 1e-4f);
            var look = p.LookAt(0.5f);
            Assert.AreEqual(0, look.oldWhole, "an empty rack stands whole with nothing on it");
            Assert.AreEqual(0, p.LookAt(p.total - 0.5f).newWhole);
        }

        [Test]
        public void TheCarryIsLongerFurther_AndTheNewAngleIsCommittedWhenItEnds()
        {
            var near = Fish(newA: 1.6f); var far = Fish(newA: 3.4f);
            Assert.Greater(far.carrySeconds, near.carrySeconds - 1e-4f);
            Assert.AreEqual(near.tCarry, near.CommitAt, 1e-5f);
            Assert.AreEqual(RackPhase.Carry, near.PhaseAt(near.tPull + 0.1f));
            Assert.AreEqual(RackPhase.Done, near.PhaseAt(near.total + 0.1f));
        }

        // ------------------------------------------------------------ the people
        [Test]
        public void TheCrewStandWithinReachOfTheRack_AtTheLiftAndAtThePlant()
        {
            var p = Fish();
            foreach (var role in new[] { RackRole.Lifter, RackRole.Carrier })
            {
                p.Stand(role, p.tTake + 0.3f, out float x, out float z, out _, out _, out _);
                p.Centre(false, out float cx, out float cz);
                Assert.Less(Dist(x, z, cx, cz), 0.3f, role + " at the old rack for the lift");
                p.Stand(role, p.tRaise - 0.5f, out x, out z, out _, out _, out _);
                p.Centre(true, out cx, out cz);
                Assert.Less(Dist(x, z, cx, cz), 0.35f, role + " at the new rack for the raise");
            }
            // the Carrier is at each frame's end as it is pulled and as it is set down
            p.Stand(RackRole.Carrier, p.tLift + 1.0f, out float bx, out float bz, out _, out _, out _);
            p.FrameAt(false, true, out float fbx, out float fbz);
            Assert.Less(Dist(bx, bz, fbx, fbz), 0.14f, "a hand within reach of frame B at its pull");
            p.Stand(RackRole.Carrier, p.tLift + 2.4f, out bx, out bz, out _, out _, out _);
            p.FrameAt(false, false, out fbx, out fbz);
            Assert.Less(Dist(bx, bz, fbx, fbz), 0.14f, "and of frame A");
            p.Stand(RackRole.Carrier, p.tCarry + 0.5f, out bx, out bz, out _, out _, out _);
            p.FrameAt(true, true, out fbx, out fbz);
            Assert.Less(Dist(bx, bz, fbx, fbz), 0.14f, "frame B at its plant");
            p.Stand(RackRole.Carrier, p.tCarry + 3.0f, out bx, out bz, out _, out _, out _);
            p.FrameAt(true, false, out fbx, out fbz);
            Assert.Less(Dist(bx, bz, fbx, fbz), 0.14f, "frame A at its plant");
        }

        [Test]
        public void TheCarryGoesRoundTheFire_AndNobodyMovesFasterThanAWalk()
        {
            foreach (float newA in new[] { 1.7f, 2.6f, 4.0f })
            {
                var p = Fish(newA: newA);
                foreach (var role in new[] { RackRole.Lifter, RackRole.Carrier })
                {
                    float px = float.NaN, pz = 0f, worst = 0f, nearest = 9f, worstT = 0f;
                    for (float t = 0f; t < p.total; t += 1f / 30f)
                    {
                        p.Stand(role, t, out float x, out float z, out _, out _, out _);
                        if (!float.IsNaN(px) && Dist(x, z, px, pz) * 30f > worst) { worst = Dist(x, z, px, pz) * 30f; worstT = t; }
                        px = x; pz = z;
                        if (t > p.tPull && t < p.tCarry) nearest = Math.Min(nearest, Dist(x, z, p.fx, p.fz));
                    }
                    Assert.Less(worst, 0.7f, $"{role} to {newA}: no jump, 0.7 u/s at most (the shuffle between the frames peaks at 0.64) (B27 carry is 0.25 u/s); worst at t={worstT:0.00}, phase {p.PhaseAt(worstT)}");
                    Assert.Greater(nearest, 0.3f, $"{role} to {newA}: the carry does not cut through the fire");
                }
            }
        }

        // ------------------------------------------------------------ the rack's parts
        [Test]
        public void TheRackIsWholeBeforeAndAfter_AndThreePartsInBetween()
        {
            var p = Fish();
            Assert.AreEqual(6, p.LookAt(0.1f).oldWhole, "the rack stands whole with its fish to start");
            Assert.AreEqual(-1, p.LookAt(0.1f).newWhole);
            Assert.AreEqual(0, p.LookAt(p.tTake + 0.05f).oldWhole >= 0 ? -1 : 0, "once the fish are off the parts are drawn");
            var mid = p.LookAt(p.tCarry - 0.5f);
            Assert.IsTrue(mid.ridge.shown && mid.frameA.shown && mid.frameB.shown);
            Assert.AreEqual(-1, mid.oldWhole); Assert.AreEqual(-1, mid.newWhole);
            var end = p.LookAt(p.total - 0.1f);
            Assert.AreEqual(6, end.newWhole, "the fish are back on");
            Assert.IsTrue(p.LookAt(p.tLift + 2f).holes, "the old spot has its holes");
        }

        [Test]
        public void TheFishComeOffOneAtATime_AndGoBackOnOneAtATime()
        {
            var p = Fish();
            int prev = 6; bool sawSix = false, sawZero = false;
            for (float t = RackMovePlan.AnticSeconds; t < p.tTake; t += 0.02f)
            {
                int n = p.LookAt(t).oldWhole;
                if (n < 0) { sawZero = true; break; }
                Assert.LessOrEqual(n, prev); Assert.GreaterOrEqual(prev - n, 0); Assert.LessOrEqual(prev - n, 1, "one fish at a time");
                prev = n; if (n == 6) sawSix = true;
            }
            Assert.IsTrue(sawSix);
            prev = 0;
            for (float t = p.tRaise; t < p.tHang; t += 0.02f)
            {
                int n = p.HungNow(t);
                Assert.GreaterOrEqual(n, prev); Assert.LessOrEqual(n - prev, 1, "one fish at a time");
                prev = n;
            }
            Assert.AreEqual(6, prev);
            Assert.IsTrue(sawZero || prev == 6);
        }

        [Test]
        public void NoPartJumps_AcrossTheWholeBeat_AndEachContactHoldsAtLeastATenthOfASecond()
        {
            var p = Fish();
            var last = new float[6]; bool first = true;
            float worst = 0f, worstT = 0f; int worstPart = 0;
            for (float t = 0f; t < p.total; t += 1f / 30f)
            {
                var l = p.LookAt(t);
                if (l.oldWhole >= 0 || l.newWhole >= 0) { first = true; continue; } // whole racks are the camp's own pieces
                var parts = new[] { l.ridge, l.frameA, l.frameB };
                var now = new float[6];
                for (int i = 0; i < 3; i++) { now[i * 2] = parts[i].x; now[i * 2 + 1] = parts[i].z; }
                if (!first) for (int i = 0; i < 3; i++) { float dd = Dist(now[i * 2], now[i * 2 + 1], last[i * 2], last[i * 2 + 1]) * 30f; if (dd > worst) { worst = dd; worstT = t; worstPart = i; } }
                last = now; first = false;
            }
            Assert.Less(worst, 1.2f, $"no part teleports (u/s): part {worstPart} at t={worstT:0.00}, phase {p.PhaseAt(worstT)}");
            var ev = p.Events();
            Assert.GreaterOrEqual(ev.Count, 5);
            for (int i = 1; i < ev.Count; i++) Assert.GreaterOrEqual(ev[i].t - ev[i - 1].t, 0.1f - 1e-4f, "puffs and contacts do not stack");
            Assert.IsTrue(ev.Exists(e => e.grass), "the last frame leaving the ground throws up grass");
        }

        [Test]
        public void ThePlantedFramesStandWhereTheNewRackIs_AndTheHolesAreTheOldFeet()
        {
            var p = Fish();
            var l = p.LookAt(p.tRaise - 0.05f);
            p.FrameAt(true, false, out float ax, out float az); p.FrameAt(true, true, out float bx, out float bz);
            Assert.AreEqual(0f, Dist(l.frameA.x, l.frameA.z, ax, az), 1e-3f);
            Assert.AreEqual(0f, Dist(l.frameB.x, l.frameB.z, bx, bz), 1e-3f);
            Assert.AreEqual(0f, l.frameA.y, 1e-3f);
            var holes = new List<(float x, float z)>();
            p.Holes(holes);
            Assert.AreEqual(4, holes.Count);
            p.FrameAt(false, false, out float oax, out float oaz);
            Assert.Less(Dist(holes[0].x, holes[0].z, oax, oaz), 0.08f);
        }

        static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }
}
