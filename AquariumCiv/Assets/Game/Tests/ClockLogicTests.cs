using System;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>GameClock's decisions: new box, load + catch-up, broken saves kept aside, newer saves never overwritten.</summary>
    public class ClockLogicTests
    {
        string dir, path;
        static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

        [SetUp] public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "aqciv-clock-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(dir, "game", "box.json");
        }

        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        void SaveWorld(DateTime at, int seed = 42)
        {
            var sim = NewSim(seed);
            sim.Advance(2);
            SaveStore.Write(path, sim.State, at);
        }

        [Test]
        public void NoSaveStartsANewWorldAndSavesIt()
        {
            var s = ClockLogic.Start(path, Now, new SimConfig(), 99);
            Assert.AreEqual(SaveStore.LoadResult.Missing, s.load);
            Assert.IsTrue(s.newWorld);
            Assert.AreEqual(99, s.sim.State.seed);
            Assert.IsTrue(ClockLogic.Save(path, s, Now));
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out _));
            StringAssert.Contains("new world", s.Summary());
        }

        [Test]
        public void ALoadedWorldCatchesUpWithTheSlowTail()
        {
            SaveWorld(Now.AddHours(-48));
            var s = ClockLogic.Start(path, Now, new SimConfig(), 99);
            Assert.AreEqual(SaveStore.LoadResult.Loaded, s.load);
            Assert.IsFalse(s.newWorld);
            Assert.AreEqual(42, s.sim.State.seed);
            Assert.AreEqual(48, s.awayHours, 1e-6);
            Assert.AreEqual(18, s.catchUp.simHours, 1e-6);
            Assert.AreEqual(20, s.sim.State.simHours, 1e-6); // 2 h before the save + 18 lived offline
            Assert.AreEqual(1f, s.sim.State.grime, 1e-5f);
            StringAssert.Contains("slowed", s.Summary());
        }

        [Test]
        public void AClockJumpIsCapped()
        {
            SaveWorld(Now.AddDays(-90));
            var s = ClockLogic.Start(path, Now, new SimConfig(), 99);
            Assert.IsTrue(s.awayCapped);
            Assert.AreEqual(SaveStore.MaxAwayHours, s.awayHours);
        }

        [Test]
        public void AClockMovedBackwardsIsNoTimeAway()
        {
            SaveWorld(Now.AddHours(5));
            var s = ClockLogic.Start(path, Now, new SimConfig(), 99);
            Assert.AreEqual(0, s.awayHours);
            Assert.AreEqual(0, s.catchUp.months);
        }

        [Test]
        public void ACorruptSaveIsKeptAsideAndANewWorldStarts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{ not json");
            File.WriteAllText(path + ".bak", "also broken");
            var s = ClockLogic.Start(path, Now, new SimConfig(), 99);
            Assert.AreEqual(SaveStore.LoadResult.Corrupt, s.load);
            Assert.IsTrue(s.newWorld);
            Assert.AreEqual(path + ".corrupt", s.corruptKeptAs);
            Assert.AreEqual("{ not json", File.ReadAllText(path + ".corrupt"));
            Assert.AreEqual("also broken", File.ReadAllText(path + ".bak.corrupt"));
            Assert.IsTrue(ClockLogic.Save(path, s, Now));
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(99, e.state.seed);
        }

        [Test]
        public void ABrokenMainFileNextToAGoodBackupIsKeptAsideNotRotatedOverIt()
        {
            SaveWorld(Now.AddHours(-1));
            SaveWorld(Now.AddHours(-1)); // second write: the first becomes .bak
            File.WriteAllText(path, "garbage");
            var s = ClockLogic.Start(path, Now, new SimConfig(), 99);
            Assert.AreEqual(SaveStore.LoadResult.LoadedFromBackup, s.load);
            Assert.IsFalse(s.newWorld);
            Assert.AreEqual("garbage", File.ReadAllText(path + ".corrupt"));
            Assert.IsTrue(ClockLogic.Save(path, s, Now));
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out _));
        }

        [Test]
        public void ASaveFromANewerBuildIsNeverOverwritten()
        {
            SaveWorld(Now.AddHours(-3));
            string newer = File.ReadAllText(path).Replace("\"v\":" + SaveStore.CurrentVersion, "\"v\":" + (SaveStore.CurrentVersion + 1));
            File.WriteAllText(path, newer);
            var s = ClockLogic.Start(path, Now, new SimConfig(), 99);
            Assert.AreEqual(SaveStore.LoadResult.TooNew, s.load);
            Assert.IsFalse(s.canSave);
            Assert.IsTrue(s.newWorld);
            Assert.IsFalse(ClockLogic.Save(path, s, Now));
            Assert.AreEqual(newer, File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".corrupt"));
            StringAssert.Contains("NOT SAVING", s.Summary());
        }

        [Test]
        public void ResumeRunsTheTimeAwayAsACatchUp()
        {
            var sim = NewSim();
            var r = ClockLogic.Resume(sim, Now, Now.AddHours(10));
            Assert.AreEqual(10, r.realHours, 1e-6);
            Assert.AreEqual(8.5, r.simHours, 1e-6);
            Assert.AreEqual(0, ClockLogic.Resume(sim, Now, Now.AddHours(-2)).realHours);
        }

        [Test]
        public void SessionSavesAndReloadsTheSameWorld()
        {
            var s = ClockLogic.Start(path, Now, new SimConfig(), 7);
            s.sim.Advance(3);
            ClockLogic.Save(path, s, Now);
            var again = ClockLogic.Start(path, Now, new SimConfig(), 8);
            Assert.AreEqual(JsonUtility.ToJson(s.sim.State), JsonUtility.ToJson(again.sim.State));
        }
    }
}
