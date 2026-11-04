using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>P8 polish #284 phase 1: tents are made of hides. No new tent before the camp gate, a tent takes SimCore.HidesPerTent hides,
    /// the first tent after the gate is the camp's moment and fires once (it waits for hides), the cheat plays it, the planner's
    /// scene and the hide frame are sane, and with the ecology off tents still cost wood.</summary>
    public class EcoTentTests
    {
        static SimCore Eco(int seed = 5)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static int Huts(SimCore sim) => sim.State.buildings.Count(b => b.type == BuildingType.Hut);

        [Test]
        public void NoNewTentBeforeTheGate()
        {
            var sim = Eco();
            sim.DebugAddHides(10);
            sim.State.wood = 1000f;
            int before = sim.State.buildings.Count;
            for (int m = 0; m < 24 && sim.BeforeCamp; m++) sim.Step();
            Assert.IsTrue(sim.BeforeCamp);
            Assert.AreEqual(before, sim.State.buildings.Count, "the tribe is one tent and a fire");
            Assert.AreEqual(10, sim.HidesDrying, "no hides spent");
            Assert.IsFalse(sim.FirstTentWaiting, "the moment is not armed before the gate");
        }

        [Test]
        public void TheFirstTentWaitsForHidesThenUsesThemAndFiresOnce()
        {
            var sim = Eco();
            var began = new List<Building>(); var raised = new List<(Building b, bool first)>();
            sim.CampBegan += b => began.Add(b);
            sim.TentRaised += (b, f) => raised.Add((b, f));
            sim.State.wood = 1000f; // wood alone builds no tent
            sim.DebugOpenCampGate();
            Assert.IsTrue(sim.FirstTentWaiting, "the gate is open and the first tent waits");
            int huts = Huts(sim);
            for (int m = 0; m < 6; m++) { sim.State.wood = 1000f; sim.Step(); }
            Assert.AreEqual(huts, Huts(sim), "no hides, no tent");
            Assert.IsEmpty(began);

            sim.DebugAddHides(SimCore.HidesPerTent + 1);
            sim.Step();
            Assert.AreEqual(huts + 1, Huts(sim), "the tent went up");
            Assert.AreEqual(1, sim.HidesDrying, "it took exactly " + SimCore.HidesPerTent);
            Assert.IsTrue(sim.FirstTentRaised);
            Assert.AreEqual(1, began.Count);
            Assert.IsTrue(raised[0].first);

            sim.DebugAddHides(50);
            for (int m = 0; m < 80; m++) { sim.State.food = 5000f; sim.State.wood = 5000f; sim.Step(); }
            Assert.AreEqual(1, began.Count, "the moment fires once");
            Assert.IsTrue(raised.Skip(1).All(r => !r.first), "later tents go up without the moment");
            Assert.AreEqual(sim.HidesTotal - sim.HidesDrying, SimCore.HidesPerTent * sim.TentsFromHidesCount, "every tent used its hides, nothing else did");
            Assert.AreEqual(raised.Count, sim.TentsFromHidesCount);
        }

        [Test]
        public void HidesAlreadyDryingMeanNoWaitAtTheGate()
        {
            var sim = Eco();
            int began = 0; sim.CampBegan += _ => began++;
            sim.DebugAddHides(SimCore.HidesPerTent);
            int huts = Huts(sim);
            sim.DebugOpenCampGate();
            Assert.AreEqual(1, began);
            Assert.AreEqual(huts + 1, Huts(sim));
            Assert.AreEqual(0, sim.HidesDrying);
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "eco_camp"), "the chronicle card stays");
        }

        [Test]
        public void ALaterTentNeedsAHomeWantAndHides()
        {
            var sim = Eco();
            sim.DebugCampMoment();
            int huts = Huts(sim);
            sim.State.population = 2; sim.State.wood = 5000f; sim.State.food = 5000f;
            sim.DebugAddHides(40);
            sim.Step();
            Assert.AreEqual(huts, Huts(sim), "nobody is short of a roof: no second tent");
            Assert.AreEqual(40, sim.HidesDrying);
        }

        [Test]
        public void TheCheatPlaysTheMomentAndGivesHidesIfShort()
        {
            var sim = Eco();
            int began = 0; sim.CampBegan += _ => began++;
            var b = sim.DebugCampMoment();
            Assert.NotNull(b);
            Assert.IsTrue(sim.CampGateOpen);
            Assert.AreEqual(1, began);
            Assert.AreEqual(0, sim.HidesDrying);
            Assert.AreNotEqual(0, b.lotId, "the tent has its lot at once");
            Assert.NotNull(sim.DebugCampMoment(), "it can be played again");
            Assert.AreEqual(2, began);
        }

        [Test]
        public void AnOldSaveWithTheGateOpenGetsNoMoment()
        {
            var sim = Eco();
            sim.DebugOpenCampGate();
            sim.State.eco.campTentState = 0; // a save from before #284
            int began = 0; sim.CampBegan += _ => began++;
            sim.DebugAddHides(10);
            for (int m = 0; m < 24; m++) sim.Step();
            Assert.AreEqual(0, began);
            Assert.IsFalse(sim.FirstTentWaiting);
        }

        [Test]
        public void WithTheEcologyOffTentsCostWoodAsBefore()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(5, cfg));
            Assert.IsFalse(sim.TentsFromHides);
            int fired = 0; sim.CampBegan += _ => fired++; sim.TentRaised += (_, __) => fired++;
            Assert.IsNull(sim.DebugCampMoment());
            for (int m = 0; m < 120; m++) { sim.State.wood = 5000f; sim.State.food = 5000f; sim.Step(); }
            Assert.Greater(Huts(sim), 1, "wood builds tents");
            Assert.AreEqual(0, fired);
            Assert.AreEqual(0, sim.HidesDrying);
        }

        // ---------------------------------------------------------------- the planner's scene

        [Test]
        public void TheSceneIsSane()
        {
            var sim = Eco();
            var planner = LifePlanner.Wired(sim);
            var tent = sim.DebugCampMoment();
            planner.Invalidate();
            var now = new ViewTime(3, 10f);
            Assert.IsTrue(planner.BeginTentRaising(tent.id, now, true, out var plan));
            Assert.AreSame(plan, planner.TentPlanOf(tent.id));
            Assert.IsTrue(planner.BeginTentRaising(tent.id, new ViewTime(3, 12f), true, out var again) && ReferenceEquals(plan, again), "planned once");
            Assert.GreaterOrEqual(plan.workers.Count, 1);
            Assert.Less(0f, plan.takeAt);
            Assert.Less(plan.takeAt, plan.raiseStart);
            Assert.Less(plan.raiseStart, plan.raiseEnd);
            Assert.LessOrEqual(plan.raiseEnd, plan.end);
            Assert.AreEqual(SimCore.HidesPerTent, plan.hides);
            Assert.Greater(System.Math.Abs(plan.frameX - plan.lotX) + System.Math.Abs(plan.frameZ - plan.lotZ), 0.01f, "the lot is not on the frame");
            foreach (var w in plan.workers)
            {
                Assert.IsNotEmpty(w.steps);
                float t = 0f;
                foreach (var s in w.steps)
                {
                    Assert.AreEqual(t, s.start, 1e-4f, "steps follow each other");
                    Assert.Greater(s.end, s.start);
                    foreach (float v in new[] { s.x0, s.z0, s.x1, s.z1 }) Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
                    t = s.end;
                }
                if (w.isKid) continue;
                Assert.AreEqual(TentPhase.ToFrame, w.steps[0].phase);
                Assert.IsTrue(w.steps.Any(s => s.phase == TentPhase.CarryOut && s.prop == w.load && (w.load == Prop.Log || w.load == Prop.Hide)));
                Assert.IsTrue(w.steps.Any(s => s.phase == TentPhase.Raise));
                Assert.IsTrue(w.steps.Any(s => s.phase == TentPhase.Stand), "the first tent ends with a moment to stand back");
            }
            Assert.AreEqual(plan.workers.Count, plan.workers.Select(w => w.who).Distinct().Count(), "nobody twice");

            // what the view reads while it plays
            var mid = new ViewTime(3, 10f + (plan.raiseStart + plan.raiseEnd) * 0.5f);
            var builder = plan.workers.First(w => !w.isKid);
            Assert.IsTrue(planner.TentStepAt(builder.who, mid, out var step, out _));
            Assert.AreEqual(TentPhase.Raise, step.phase);
            Assert.AreEqual(TentPhase.Raise, planner.Plan(builder.who, mid).tent);
            Assert.AreEqual(0f, planner.TentRaised(tent.id, now), 1e-4f);
            Assert.That(planner.TentRaised(tent.id, mid), Is.InRange(0.3f, 0.7f));
            var after = new ViewTime(3, 10f + plan.end + 0.1f);
            Assert.AreEqual(1f, planner.TentRaised(tent.id, after), 1e-4f);
            Assert.AreEqual(TentPhase.None, planner.Plan(builder.who, after).tent, "back to their day");
            Assert.AreEqual(1f, planner.TentRaised(-77, now), "an unplanned tent just stands");
        }

        [Test]
        public void TheFrameStandsByTheFireAndCountsTheHides()
        {
            var sim = Eco();
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.HideFrame(out float x, out float z, out float dx, out float dz));
            Assert.IsTrue(planner.FirePoint(out float fx, out float fz));
            Assert.AreEqual(LifePlanner.HideFrameDistance, System.Math.Sqrt((x - fx) * (x - fx) + (z - fz) * (z - fz)), 1e-3);
            Assert.AreEqual(1.0, dx * dx + dz * dz, 1e-3);
            var pts = new List<(float x, float z)>();
            var now = new ViewTime(1, 9f);
            Assert.AreEqual(0, planner.HideFramePoints(now, pts));
            Assert.IsEmpty(pts);
            sim.DebugAddHides(3);
            Assert.AreEqual(3, planner.HideFramePoints(now, pts));
            Assert.AreEqual(3, pts.Count);
            sim.DebugAddHides(20);
            planner.HideFramePoints(now, pts);
            Assert.AreEqual(LifePlanner.HideFrameMax, pts.Count);
            Assert.AreEqual(pts.Count, pts.Distinct().Count());

            // the hides a tent is about to take still hang there until they are taken
            sim.TakeHides(sim.HidesDrying);
            var tent = sim.DebugCampMoment();
            Assert.IsTrue(planner.BeginTentRaising(tent.id, now, true, out var plan));
            Assert.AreEqual(SimCore.HidesPerTent, planner.HidesOnFrame(now));
            Assert.AreEqual(0, planner.HidesOnFrame(new ViewTime(1, 9f + plan.takeAt + 0.01f)));

            var offCfg = new SimConfig();
            var off = new SimCore(offCfg, SimCore.NewWorld(5, offCfg));
            Assert.IsFalse(LifePlanner.Wired(off).HideFrame(out _, out _, out _, out _), "no frame without the living land");
        }
    }
}
