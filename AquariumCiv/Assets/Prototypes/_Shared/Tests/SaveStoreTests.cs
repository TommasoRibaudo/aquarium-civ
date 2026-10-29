using System;
using System.IO;
using AquariumCiv.Prototypes.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Prototypes.Tests
{
    /// <summary>Save-versioning spike (SaveStore): old saves load, broken saves fall back, newer saves are never overwritten.</summary>
    public class SaveStoreTests
    {
        string dir, path;
        static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

        [SetUp] public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "aqciv-save-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(dir, "slice.json");
        }

        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        static SimCore World(int seed = 42)
        {
            var cfg = new SimConfig { storyEvents = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            sim.Advance(10);
            return sim;
        }

        [Test]
        public void RoundTripKeepsTheWorldAndTime()
        {
            var sim = World();
            SaveStore.Write(path, sim.State, Now);
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(SaveStore.CurrentVersion, e.v);
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(e.state));
            Assert.AreEqual(3.0, SaveStore.AwayHours(e, Now.AddHours(3), out var capped), 1e-6);
            Assert.IsFalse(capped);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void APrototypeSaveLoadsAsVersionZeroAndMigrates()
        {
            // SimClock's format: {lastSeenUtc, state}, no version.
            var sim = World();
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{\"lastSeenUtc\":\"2026-10-29T10:00:00.0000000Z\",\"state\":" + JsonUtility.ToJson(sim.State) + "}");
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(SaveStore.CurrentVersion, e.v);
            Assert.AreEqual(sim.State.month, e.state.month);
            Assert.AreEqual(2.0, SaveStore.AwayHours(e, Now, out _), 1e-6);
        }

        [Test]
        public void ABrokenSaveFallsBackToTheLastGoodOne()
        {
            var sim = World();
            SaveStore.Write(path, sim.State, Now);
            sim.Advance(5);
            SaveStore.Write(path, sim.State, Now.AddHours(5)); // the first save becomes .bak
            File.WriteAllText(path, "{\"v\":1,\"lastSeen"); // a crash mid-write
            Assert.AreEqual(SaveStore.LoadResult.LoadedFromBackup, SaveStore.Read(path, out var e));
            Assert.AreEqual(10 * 12, e.state.month);
        }

        [Test]
        public void ASaveFromANewerBuildIsRefused()
        {
            var sim = World();
            SaveStore.Write(path, sim.State, Now);
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"v\":1", "\"v\":99"));
            Assert.AreEqual(SaveStore.LoadResult.TooNew, SaveStore.Read(path, out var e));
            Assert.IsNull(e);
        }

        [Test]
        public void MissingAndGarbageSavesAreReported()
        {
            Assert.AreEqual(SaveStore.LoadResult.Missing, SaveStore.Read(path, out _));
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "not json");
            Assert.AreEqual(SaveStore.LoadResult.Corrupt, SaveStore.Read(path, out _));
        }

        [Test]
        public void ClockJumpsAreClamped()
        {
            var e = new SaveStore.Envelope { lastSeenUtc = Now.ToString("o") };
            Assert.AreEqual(0, SaveStore.AwayHours(e, Now.AddHours(-5), out var back));
            Assert.IsFalse(back);
            Assert.AreEqual(SaveStore.MaxAwayHours, SaveStore.AwayHours(e, Now.AddDays(400), out var capped));
            Assert.IsTrue(capped);
        }
    }
}
