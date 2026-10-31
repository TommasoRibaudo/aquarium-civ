using System;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#93: completing the box through the clock logic. Complete gives a museum and box 2 with the founders and the
    /// relics, visit and back leave the active box exactly as it was, and a kill around the completion never loses a box.</summary>
    public class CompleteBoxFlowTests
    {
        static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        string dir, path;
        SimConfig cfg;

        [SetUp] public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "aqciv-complete-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(dir, "box.json");
            cfg = new SimConfig();
        }

        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        ClockStart Town(int seed = 7)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 200 * SimConfig.MonthsPerYear; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
                if (sim.State.era == Era.Town && m > 60 * SimConfig.MonthsPerYear) break;
            }
            foreach (var id in RelicId.DefaultCarried) sim.DebugGiveRelic(id);
            sim.ForcePeakReady();
            return new ClockStart { sim = sim, shelf = ShelfState.Single(sim.State) };
        }

        static string Json(SimState s) => UnityEngine.JsonUtility.ToJson(s);

        [Test]
        public void CompleteMakesAMuseumAndABoxTwoWithFoundersAndRelics()
        {
            var s = Town();
            var done = s.sim;
            var completion = done.CompleteBox();
            Assert.NotNull(completion);
            var box2 = ClockLogic.StartNextBox(s, cfg, completion, BoxSetting.DesertOasis, 99, Now);
            Assert.AreNotSame(done, box2);
            Assert.AreEqual(2, s.shelf.boxes.Count);
            Assert.IsTrue(s.shelf.Box(0).museum);
            Assert.IsFalse(s.shelf.Box(1).museum);
            Assert.AreEqual(1, s.shelf.activeIndex);
            Assert.AreEqual(BoxSetting.DesertOasis, s.shelf.Active.setting);
            Assert.GreaterOrEqual(s.shelf.Active.founders.Count, 1);
            Assert.IsNotEmpty(box2.State.relics.Where(r => r.carriedFromBox == 0).ToList(), "relics crossed");
            Assert.IsNotEmpty(s.shelf.plinths.Where(p => p.boxIndex == 0).ToList(), "plinths on the old box");
            Assert.IsFalse(string.IsNullOrEmpty(s.shelf.Box(0).plate));
            Assert.AreEqual(0f, done.State.grime, "museum glass is clean");
        }

        [Test]
        public void VisitAndBackLeaveTheActiveBoxExactly()
        {
            var s = Town();
            ClockLogic.StartNextBox(s, cfg, s.sim.CompleteBox(), BoxSetting.MountainVillage, 5, Now);
            string before = Json(s.sim.State);
            string museumBefore = Json(s.shelf.Box(0).state);
            Assert.IsTrue(ClockLogic.Visit(s, 0, cfg, Now.AddHours(3), out var r));
            Assert.AreEqual(0, s.visitIndex);
            Assert.AreSame(s.shelf.Box(0).state, s.ViewSim.State);
            Assert.Greater(r.months, 0, "the museum lived the time away");
            Assert.AreNotEqual(museumBefore, Json(s.shelf.Box(0).state), "the museum moved on");
            ClockLogic.EndVisit(s);
            Assert.AreSame(s.sim, s.ViewSim);
            Assert.AreEqual(before, Json(s.sim.State), "the active box is exactly as it was");
            Assert.IsFalse(ClockLogic.Visit(s, 1, cfg, Now, out _), "the active box is not a museum");
        }

        [Test]
        public void SavingWhileVisitingSavesTheActiveBoxNotTheMuseum()
        {
            var s = Town();
            ClockLogic.StartNextBox(s, cfg, s.sim.CompleteBox(), BoxSetting.DesertOasis, 5, Now);
            ClockLogic.Visit(s, 0, cfg, Now, out _);
            Assert.IsTrue(ClockLogic.Save(path, s, Now));
            var back = ClockLogic.Start(path, Now, cfg, 1);
            Assert.AreEqual(1, back.shelf.activeIndex);
            Assert.IsFalse(back.sim.State.boxComplete, "the active box is the playable one");
            Assert.IsTrue(back.shelf.Box(0).museum);
            Assert.AreEqual(Json(s.shelf.Box(0).state), Json(back.shelf.Box(0).state));
        }

        [Test]
        public void AKillBeforeTheCrossingKeepsTheOldBoxAndAfterItBothBoxes()
        {
            var s = Town();
            Assert.IsTrue(ClockLogic.Save(path, s, Now)); // the autosave before the moment
            // killed during the moment, before the crossing: nothing moved
            var a = ClockLogic.Start(path, Now, cfg, 1);
            Assert.AreEqual(1, a.shelf.boxes.Count);
            Assert.IsTrue(a.sim.State.peakReady);
            Assert.IsFalse(a.sim.State.boxComplete);
            // the crossing: complete and write at once
            ClockLogic.StartNextBox(a, cfg, a.sim.CompleteBox(), BoxSetting.DesertOasis, 3, Now);
            Assert.IsTrue(ClockLogic.Save(path, a, Now));
            // killed right after (the rest of the moment never played)
            var b = ClockLogic.Start(path, Now, cfg, 1);
            Assert.AreEqual(2, b.shelf.boxes.Count);
            Assert.IsTrue(b.shelf.Box(0).museum);
            Assert.AreEqual(1, b.shelf.activeIndex);
            Assert.AreEqual(Json(a.shelf.Box(0).state), Json(b.shelf.Box(0).state), "the finished box was not lost");
            Assert.AreEqual(a.sim.State.seed, b.sim.State.seed, "box 2 was not lost");
            // a half-written main file (a kill during the write) falls back to the .bak: still both boxes or the old one
            File.WriteAllText(path, "{ broken");
            var c = ClockLogic.Start(path, Now, cfg, 1);
            Assert.AreEqual(ClockLogic.Start(path + ".bak", Now, cfg, 1).shelf.boxes.Count, c.shelf.boxes.Count);
            Assert.IsFalse(c.newWorld);
        }
    }
}
