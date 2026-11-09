using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>
    /// P9 #426 / #427 on the planner side (animation-specs B13, B14): the deer hunt's scene over the plan. Two or three hunters take bows at the
    /// fire, walk out in a line, crouch, stalk side by side to the shot point 0.6 u from the deer, the shooter shoots; a hit fetches the pole
    /// and carries the deer home (the two carriers a pole's length apart all the way), a miss shrugs and pulls the arrow. Never a deer at the
    /// water. The white stag's hunt always hits and changes nothing in the herd.
    /// </summary>
    public class DeerHuntSceneTests
    {
        static (SimCore sim, LifePlanner planner, DeerHerd.Field field) Wired(int seed = 7)
        {
            var sim = PlacedLifePlanTests.Deer(seed);
            var planner = new LifePlanner(sim);
            var field = new DeerHerd.Field();
            sim.EcoDeerRefresh(field);
            field.Sync(sim.EcoDeerShown());
            planner.DeerField = field;
            return (sim, planner, field);
        }

        static HuntStep At(List<HuntStep> steps, float hour) => steps.First(s => hour < s.end);

        static (float x, float z) Where(HuntStep s, float hour)
        {
            float f = s.end > s.start ? System.Math.Clamp((hour - s.start) / (s.end - s.start), 0f, 1f) : 1f;
            return (s.x0 + (s.x1 - s.x0) * f, s.z0 + (s.z1 - s.z0) * f);
        }

        static float D(float ax, float az, float bx, float bz) => (float)System.Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        [Test]
        public void TheHit_Line_Stalk_Shot_PoleCarry_Home()
        {
            var (sim, planner, field) = Wired();
            Assert.IsTrue(planner.DebugStartDeerHunt(new ViewTime(5, 8f), true, out var plan));
            Assert.IsTrue(plan.HasScene, "a scene over the plan");
            Assert.IsTrue(plan.playsHit);
            Assert.GreaterOrEqual(plan.targetId, 0);
            Assert.AreEqual(plan.hunters.Count, plan.steps.Count, "beats for each hunter");
            // every hunter's beats run in order without gaps
            foreach (var steps in plan.steps)
                for (int i = 1; i < steps.Count; i++) Assert.AreEqual(steps[i - 1].end, steps[i].start, 1e-3f, $"no gap at {steps[i].phase}");
            Assert.IsTrue(plan.steps.All(s => s.Any(x => x.phase == HuntPhase.DeerTakeBow)), "everyone takes a bow");
            Assert.IsTrue(plan.steps.All(s => s.Any(x => x.phase == HuntPhase.DeerWalkOut && x.prop == Prop.Bow)), "bows on the way out");
            Assert.IsTrue(plan.steps.All(s => s.Any(x => x.phase == HuntPhase.DeerStalk)), "they all stalk");
            Assert.AreEqual(1, plan.steps.Count(s => s.Any(x => x.phase == HuntPhase.DeerShoot)), "one shooter");
            Assert.IsTrue(plan.steps[0].Any(x => x.phase == HuntPhase.DeerShoot), "the lead shoots");
            // the shot point is 0.6 u from the deer (or less, close to the camp)
            var shoot = plan.steps[0].First(x => x.phase == HuntPhase.DeerShoot);
            Assert.AreEqual(DeerTiming.ShotRange, D(shoot.x0, shoot.z0, plan.targetX, plan.targetZ), 0.02f, "the shot from 0.6 u");
            Assert.AreEqual(plan.targetX, plan.landX, 1e-4f, "a hit lands on the deer");
            // the times
            Assert.Less(plan.stalkStart, plan.shootStart); Assert.Less(plan.shootStart, plan.releaseHour); Assert.Less(plan.releaseHour, plan.landHour);
            Assert.AreEqual(plan.landHour, plan.resolveHour, 1e-5f, "settled when the arrow lands");
            float aim = LifeClock.RealSecondsBetween(plan.shootStart, plan.releaseHour) - DeerTiming.RiseSeconds - DeerTiming.DrawSeconds;
            Assert.GreaterOrEqual(aim, 0.3f, "P8: the aim holds 0.3 s at least");
            Assert.Less(plan.landHour, plan.takeHour); Assert.Less(plan.takeHour, plan.setDownHour); Assert.Less(plan.setDownHour, plan.end);
            // the pole: fetched, and the carriers stay a pole's length apart all through the carry
            int run = plan.poleRunner;
            Assert.IsTrue(plan.steps[run].Any(x => x.phase == HuntPhase.DeerPoleRun) && plan.steps[run].Any(x => x.phase == HuntPhase.DeerPoleBack && x.prop == Prop.Pole), "the runner fetches the pole");
            var carryA = plan.steps[0].First(x => x.phase == HuntPhase.DeerCarry); var carryB = plan.steps[run].First(x => x.phase == HuntPhase.DeerCarry);
            Assert.AreEqual(carryA.start, carryB.start, 1e-4f); Assert.AreEqual(carryA.end, carryB.end, 1e-4f, "in step");
            for (float u = 0f; u <= 1f; u += 0.1f)
            {
                float h = carryA.start + (carryA.end - carryA.start) * u;
                var a = Where(carryA, h); var b = Where(carryB, h);
                Assert.AreEqual(DeerTiming.PoleHalf * 2f, D(a.x, a.z, b.x, b.z), 0.02f, $"the pole's ends stay in the hands ({u:0.0})");
            }
            Assert.AreEqual(0f, D(plan.setX, plan.setZ, (Where(carryA, carryA.end).x + Where(carryB, carryB.end).x) / 2f, (Where(carryA, carryA.end).z + Where(carryB, carryB.end).z) / 2f), 0.02f, "set down by the fire");
            Assert.AreEqual(DeerTiming.SetDownFromFire, D(plan.setX, plan.setZ, plan.fireX, plan.fireZ), 0.01f);
            // never at the water
            Assert.IsFalse(planner.DeerSpotAtWater(plan.targetX, plan.targetZ), "nobody hunts at the water");
            // settled at the landing: one deer
            int herd = sim.EcoDeerCount;
            var settled = new List<DeerHuntPlan>();
            Assert.AreEqual(0, planner.ResolveDeerHunts(new ViewTime(5, plan.landHour - 0.01f)));
            Assert.AreEqual(1, planner.ResolveDeerHunts(new ViewTime(5, plan.landHour + 0.001f), settled));
            Assert.IsTrue(plan.caught); Assert.AreEqual(herd - 1, sim.EcoDeerCount);
            // the hunters are hunting through the planner (their day yields to the hunt)
            var mid = new ViewTime(5, (plan.stalkStart + plan.shootStart) / 2f);
            Assert.IsTrue(planner.DeerHuntStepAt(plan.hunters[1], mid, out var st, out var p2) && ReferenceEquals(p2, plan));
            Assert.AreEqual(HuntPhase.DeerStalk, st.phase);
        }

        [Test]
        public void TheMiss_Shrug_PullTheArrow_NothingTaken()
        {
            var (sim, planner, _) = Wired();
            Assert.IsTrue(planner.DebugStartDeerHunt(new ViewTime(6, 8.5f), false, out var plan));
            Assert.IsTrue(plan.HasScene); Assert.IsFalse(plan.playsHit);
            Assert.Greater(D(plan.landX, plan.landZ, plan.targetX, plan.targetZ), 0.15f, "the arrow lands past the deer");
            Assert.IsTrue(plan.steps.All(s => s.Any(x => x.phase == HuntPhase.DeerShrug)), "they shrug together");
            Assert.IsTrue(plan.steps[0].Any(x => x.phase == HuntPhase.DeerPullArrow), "the shooter pulls the arrow out");
            Assert.IsFalse(plan.steps.Any(s => s.Any(x => x.phase == HuntPhase.DeerCarry)), "no carry");
            Assert.Greater(plan.arrowTakeHour, plan.landHour);
            planner.ResolveDeerHunts(new ViewTime(6, 23f));
            Assert.IsFalse(plan.caught); Assert.AreEqual(8, sim.EcoDeerCount);
        }

        [Test]
        public void EveryPlannedHunt_HasAScene_NeverAtTheWater_TwoOrThree()
        {
            var (sim, planner, _) = Wired();
            int scenes = 0;
            for (long d = 0; d < 120; d++)
                foreach (var p in planner.DeerHuntsOn(d))
                {
                    Assert.IsTrue(p.HasScene, $"day {d}: a scene");
                    Assert.IsFalse(planner.DeerSpotAtWater(p.targetX, p.targetZ), $"day {d}: not at the water");
                    Assert.That(p.hunters.Count, Is.InRange(2, 3));
                    Assert.Less(p.end, 24f);
                    scenes++;
                }
            Assert.Greater(scenes, 4, "deer hunts happen in normal play");
        }

        [Test]
        public void TheWhiteStag_Hunt_AlwaysHits_TheHerdUntouched()
        {
            var (sim, planner, _) = Wired();
            int herd = sim.EcoDeerCount;
            float wx = sim.EcoGround.campX + 1.4f, wz = sim.EcoGround.campZ;
            Assert.IsTrue(planner.StartWhiteStagHunt(new ViewTime(9, 9f), wx, wz, out var plan));
            Assert.IsTrue(plan.white && plan.playsHit && plan.HasScene);
            Assert.AreEqual(wx, plan.targetX, 1e-4f);
            var scenes = new List<DeerHuntPlan>();
            planner.DeerScenesOn(9, scenes);
            Assert.Contains(plan, scenes);
            planner.ResolveDeerHunts(new ViewTime(9, 23f));
            Assert.AreEqual(1, planner.ResolveWhiteHunts(new ViewTime(9, 23f)));
            Assert.IsTrue(plan.caught);
            Assert.AreEqual(herd, sim.EcoDeerCount, "the white stag was never one of the herd");
        }
    }
}
