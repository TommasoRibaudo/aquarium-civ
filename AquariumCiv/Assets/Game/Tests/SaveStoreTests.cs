using System;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The slice's save: old saves load, a killed app never loses the world, newer saves are never overwritten.</summary>
    public class SaveStoreTests
    {
        string dir, path;
        static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

        [SetUp] public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "aqciv-save-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(dir, "box.json");
        }

        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        static SimCore World(int seed = 42)
        {
            var sim = NewSim(seed);
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
        public void ALoadedWorldCarriesOnExactlyAsIfItNeverStopped()
        {
            var a = World(); var b = World();
            SaveStore.Write(path, b.State, Now);
            SaveStore.Read(path, out var e);
            var loaded = new SimCore(new SimConfig(), e.state);
            a.Advance(30); loaded.Advance(30);
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(loaded.State));
        }

        [Test]
        public void AnAppKilledMidSaveKeepsTheLastGoodWorld()
        {
            // Killed after writing the temp file but before the swap: the main save is intact, a stale .tmp is left over.
            var sim = World();
            SaveStore.Write(path, sim.State, Now);
            File.WriteAllText(path + ".tmp", "{\"v\":1,\"lastSe");
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(sim.State.month, e.state.month);
            // The next save must not trip over the leftover.
            sim.Advance(2);
            Assert.DoesNotThrow(() => SaveStore.Write(path, sim.State, Now.AddHours(2)));
            SaveStore.Read(path, out e);
            Assert.AreEqual(sim.State.month, e.state.month);
        }

        [Test]
        public void ABrokenSaveFallsBackToTheLastGoodOne()
        {
            var sim = World();
            SaveStore.Write(path, sim.State, Now);
            sim.Advance(5);
            SaveStore.Write(path, sim.State, Now.AddHours(5)); // the first save becomes .bak
            File.WriteAllText(path, "{\"v\":1,\"lastSeen"); // a torn main file
            Assert.AreEqual(SaveStore.LoadResult.LoadedFromBackup, SaveStore.Read(path, out var e));
            Assert.AreEqual(10 * 12, e.state.month);
        }

        [Test]
        public void AnUnversionedSaveLoadsAsVersionZeroAndMigrates()
        {
            var sim = World();
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{\"lastSeenUtc\":\"2026-10-29T10:00:00.0000000Z\",\"state\":" + JsonUtility.ToJson(sim.State) + "}");
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(SaveStore.CurrentVersion, e.v);
            Assert.AreEqual(sim.State.month, e.state.month);
            Assert.AreEqual(2.0, SaveStore.AwayHours(e, Now, out _), 1e-6);
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
