using System;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The shelf (#87): old saves become a shelf of one, the shelf round-trips and survives a kill mid-save,
    /// a finished box is a cheap museum, and box 2 starts with the founders and the relics that crossed.</summary>
    public class ShelfTests
    {
        static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        string dir, path;

        [SetUp] public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "aqciv-shelf-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(dir, "box.json");
        }

        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        /// <summary>A box lived to its town and completed (debug jump), with the default relics found.</summary>
        static (SimCore sim, BoxCompletion done) Finished(int seed = 7)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 200 * SimConfig.MonthsPerYear; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
                if (sim.State.era == Era.Town && m > 60 * SimConfig.MonthsPerYear) break;
            }
            foreach (var id in RelicId.DefaultCarried) sim.DebugGiveRelic(id);
            var done = sim.ForceCompleteBox();
            Assert.NotNull(done);
            return (sim, done);
        }

        static ClockStart Session(SimCore sim) => new() { sim = sim, shelf = ShelfState.Single(sim.State) };

        // ---------- the save ----------
        [Test]
        public void AVersion2SaveMigratesToAShelfOfOne()
        {
            var sim = NewSim(11);
            sim.Advance(10);
            SaveStore.Write(path, sim.State, Now);
            // Make the file read like today's: version 2, no shelf.
            var json = File.ReadAllText(path);
            var env = JsonUtility.FromJson<SaveStore.Envelope>(json);
            env.v = 2; env.shelf = null;
            File.WriteAllText(path, JsonUtility.ToJson(env));
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(SaveStore.CurrentVersion, e.v);
            Assert.AreEqual(1, e.shelf.boxes.Count);
            Assert.AreEqual(0, e.shelf.activeIndex);
            Assert.AreSame(e.state, e.shelf.Active.state);
            Assert.IsFalse(e.shelf.Active.museum);
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(e.state));
        }

        [Test]
        public void TheShelfRoundTripsWithAMuseumAndBox2()
        {
            var (sim, done) = Finished();
            var session = Session(sim);
            var box2 = ClockLogic.StartNextBox(session, new SimConfig(), done, BoxSetting.DesertOasis, 99, Now);
            box2.Advance(5);
            Assert.IsTrue(ClockLogic.Save(path, session, Now));
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            var shelf = e.shelf;
            Assert.AreEqual(2, shelf.boxes.Count);
            Assert.AreEqual(1, shelf.activeIndex);
            Assert.AreEqual(BoxSetting.DesertOasis, shelf.Active.setting);
            Assert.AreEqual(0, shelf.Active.fromBox);
            Assert.AreEqual(JsonUtility.ToJson(box2.State), JsonUtility.ToJson(e.state));
            var museum = shelf.Box(0);
            Assert.IsTrue(museum.museum);
            Assert.IsTrue(museum.state.boxComplete);
            Assert.AreEqual(JsonUtility.ToJson(sim.State.founderIds), JsonUtility.ToJson(museum.state.founderIds));
            Assert.AreEqual(session.shelf.plinths.Count, shelf.plinths.Count);
            Assert.IsNotEmpty(museum.plate);
        }

        [Test]
        public void TheActiveBoxIsNotWrittenTwice()
        {
            var sim = NewSim(3);
            SaveStore.Write(path, sim.State, Now);
            var json = File.ReadAllText(path);
            var size = json.Length;
            Assert.Less(size, JsonUtility.ToJson(sim.State).Length * 1.3, "one state in the file, not two");
        }

        [Test]
        public void AnAppKilledMidShelfSaveKeepsTheLastGoodShelf()
        {
            var (sim, done) = Finished();
            var session = Session(sim);
            ClockLogic.StartNextBox(session, new SimConfig(), done, BoxSetting.MountainVillage, 5, Now);
            ClockLogic.Save(path, session, Now);
            var good = File.ReadAllText(path);
            session.sim.Advance(3);
            // Killed after writing the new temp file, before the swap: a half-written temp is left beside the good save.
            File.WriteAllText(path + ".tmp", good.Substring(0, good.Length / 2));
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(2, e.shelf.boxes.Count);
            // Killed mid-swap with the main file torn: the .bak (the last good shelf) wins.
            ClockLogic.Save(path, session, Now.AddHours(1));
            File.WriteAllText(path, good.Substring(0, good.Length / 3));
            Assert.AreEqual(SaveStore.LoadResult.LoadedFromBackup, SaveStore.Read(path, out e));
            Assert.AreEqual(2, e.shelf.boxes.Count);
            Assert.IsTrue(e.shelf.Box(0).museum);
        }

        [Test]
        public void ASaveFromANewerBuildStillRefusesWithAShelf()
        {
            var sim = NewSim(3);
            SaveStore.Write(path, sim.State, Now);
            var env = JsonUtility.FromJson<SaveStore.Envelope>(File.ReadAllText(path));
            env.v = SaveStore.CurrentVersion + 1;
            File.WriteAllText(path, JsonUtility.ToJson(env));
            Assert.AreEqual(SaveStore.LoadResult.TooNew, SaveStore.Read(path, out _));
        }

        // ---------- museum mode ----------
        [Test]
        public void AFinishedBoxLivesCheaplyAndStaysSmall()
        {
            var (sim, done) = Finished();
            var shelf = ShelfState.Single(sim.State);
            Shelf.FinishActive(shelf, sim, done, Now);
            var museum = shelf.Box(0);
            Assert.AreEqual(0f, museum.state.grime);
            int buildings = museum.state.buildings.Count;
            var era = museum.state.era;
            int relics = museum.state.relics.Count;
            int lines = museum.state.chronicle.Count;
            long monthBefore = museum.state.month;

            // Ten visits of a (capped) forty years each: four centuries.
            var t = Now;
            int maxLate = 0, maxPeople = 0;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                t = t.AddDays(30);
                var m = Shelf.Visit(shelf, 0, new SimConfig(), t, out var r);
                Assert.AreEqual(SimCore.MuseumMaxYears * SimConfig.MonthsPerYear, r.months, "capped visit");
                Assert.IsTrue(r.capped);
                maxLate = Math.Max(maxLate, m.State.chronicle.Count(e => e.month > museum.state.boxCompleteMonth));
                maxPeople = Math.Max(maxPeople, m.State.notables.Count);
            }
            clock.Stop();
            var s = museum.state;
            Assert.AreEqual(monthBefore + 10 * SimCore.MuseumMaxYears * SimConfig.MonthsPerYear, s.month);
            Assert.AreEqual(buildings, s.buildings.Count, "nothing is built");
            Assert.AreEqual(era, s.era, "no eras");
            Assert.AreEqual(0f, s.grime, "the glass doesn't get dirty");
            Assert.AreEqual(0, s.trash.Count);
            Assert.AreEqual(relics, s.relics.Count, "no relic is made");
            Assert.IsTrue(s.boxComplete);
            Assert.LessOrEqual(maxLate, SimCore.MuseumChronicleMax);
            Assert.LessOrEqual(s.notables.Count(n => !n.alive && n.bornMonth > s.boxCompleteMonth), SimCore.MuseumDeadMax);
            Assert.Less(maxPeople, lines + 400, "the people list stays bounded");
            Assert.Less(clock.Elapsed.TotalSeconds, 15, "four centuries of museum is cheap");
            Assert.Greater(s.notables.Count(n => n.alive), 0, "alive but steady: the line goes on");
            Assert.AreEqual(Now.AddDays(300).ToString("o"), DateTime.Parse(museum.lastSeenUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).ToString("o"));
        }

        [Test]
        public void MuseumPeopleAgeWedAndDie()
        {
            var (sim, done) = Finished(11);
            var shelf = ShelfState.Single(sim.State);
            Shelf.FinishActive(shelf, sim, done, Now);
            var ages = sim.State.notables.Where(n => n.alive).ToDictionary(n => n.id, n => n.age);
            var m = Shelf.Visit(shelf, 0, new SimConfig(), Now.AddHours(30), out var r);
            Assert.AreEqual(30 * SimConfig.MonthsPerYear, r.months);
            Assert.IsFalse(r.capped);
            foreach (var n in m.State.notables.Where(n => n.alive && ages.ContainsKey(n.id)))
                Assert.Greater(n.age, ages[n.id]);
            Assert.Greater(r.deaths, 0);
            Assert.Greater(r.births, 0);
            Assert.Greater(m.State.chronicle.Count(e => e.month > m.State.boxCompleteMonth), 0, "their stories wait for the visit");
        }

        [Test]
        public void AVisitOnlyHappensOnAFinishedBoxAndNothingMovesBeforeIt()
        {
            var sim = NewSim(5);
            var shelf = ShelfState.Single(sim.State);
            Assert.Throws<InvalidOperationException>(() => Shelf.Visit(shelf, 0, new SimConfig(), Now, out _));
            Assert.AreEqual(0, sim.MuseumAdvance(100).months, "an unfinished box does not run in museum mode");
        }

        [Test]
        public void AMuseumVisitIsDeterministic()
        {
            string Run()
            {
                var (sim, done) = Finished(21);
                var shelf = ShelfState.Single(sim.State);
                Shelf.FinishActive(shelf, sim, done, Now);
                Shelf.Visit(shelf, 0, new SimConfig(), Now.AddHours(25), out _);
                return JsonUtility.ToJson(shelf.Box(0).state);
            }
            Assert.AreEqual(Run(), Run());
        }

        // ---------- box 2 ----------
        [Test]
        public void Box2StartsWithTheFoundersLinkedBackAndTheRelicsThatCrossed()
        {
            var (sim, done) = Finished();
            var old = sim.State;
            var session = Session(sim);
            var cfg = new SimConfig();
            var box2 = ClockLogic.StartNextBox(session, cfg, done, BoxSetting.DesertOasis, 4242, Now);
            var shelf = session.shelf;
            var s = box2.State;

            Assert.AreEqual(1, s.boxIndex);
            Assert.AreEqual(Era.Camp, s.era);
            Assert.AreEqual(2, shelf.boxes.Count);
            var next = shelf.Active;
            Assert.AreEqual(1, next.index);
            Assert.AreEqual(BoxSetting.DesertOasis, next.setting);
            Assert.AreEqual(done.founders.Count, next.founders.Count);
            foreach (var l in next.founders)
            {
                var from = old.notables.First(n => n.id == l.fromNotableId);
                var copy = box2.FindNotable(l.notableId);
                Assert.AreEqual(0, l.fromBox);
                Assert.AreEqual(from.FullName, copy.FullName);
                Assert.AreEqual(from.trait, copy.trait);
                Assert.AreEqual(from.age, copy.age);
                Assert.IsTrue(copy.alive);
                Assert.AreEqual(-1, copy.parentId);
            }
            Assert.AreEqual(done.leader.FullName, box2.HolderOf(RoleType.Chief).FullName, "the leader of the founders is the first chief");
            Assert.IsTrue(s.chronicle.Any(e => e.kind == "founders"));

            // The three default relics crossed; the old box keeps them on plinths too.
            var carried = s.relics.Where(r => r.carriedFromBox == 0).Select(r => r.id).ToList();
            CollectionAssert.AreEquivalent(RelicId.DefaultCarried, carried);
            Assert.IsTrue(box2.HasRelic(RelicId.SkyCrumb));
            Assert.AreEqual(sim.FoundRelics.Count(), shelf.plinths.Count(p => p.boxIndex == 0));
            Assert.AreEqual(3, shelf.plinths.Count(p => p.boxIndex == 0 && p.carried));
            // The old box is not touched by the new one.
            Assert.IsTrue(shelf.Box(0).museum);
            Assert.AreEqual(0, shelf.Box(0).state.boxIndex);
        }

        [Test]
        public void Box2PlaysOnAndNeverMakesASecondSkyCrumb()
        {
            var (sim, done) = Finished();
            var session = Session(sim);
            var box2 = ClockLogic.StartNextBox(session, new SimConfig(), done, BoxSetting.MountainVillage, 31, Now);
            box2.Advance(30); // 30 game years of the new camp
            Assert.AreEqual(1, box2.State.relics.Count(r => r.id == RelicId.SkyCrumb));
            Assert.AreEqual(0, box2.State.relics.First(r => r.id == RelicId.SkyCrumb).carriedFromBox, "the one it crossed with");
            Assert.Greater(box2.State.month, 300);
        }

        [Test]
        public void Box2WithoutTheSkyCrumbNeverGetsOne()
        {
            var sim = NewSim(9);
            sim.DebugGiveRelic(RelicId.KeeperDoll);
            Assert.IsFalse(sim.HasRelic(RelicId.SkyCrumb));
            var done = sim.ForceCompleteBox();
            var session = Session(sim);
            var box2 = ClockLogic.StartNextBox(session, new SimConfig(), done, BoxSetting.DesertOasis, 8, Now);
            Assert.IsFalse(box2.HasRelic(RelicId.SkyCrumb));
            box2.Advance(5);
            box2.PlaceNudge(SimCore.NudgeKind.Rain, 0.2f, 0.1f);
            box2.PlaceNudge(SimCore.NudgeKind.Grove, -0.3f, 0.4f);
            Assert.IsFalse(box2.HasRelic(RelicId.SkyCrumb));
        }

        [Test]
        public void ASettingThatIsNotForBox2IsRefused()
        {
            var (sim, done) = Finished();
            var session = Session(sim);
            Assert.Throws<ArgumentException>(() => ClockLogic.StartNextBox(session, new SimConfig(), done, BoxSetting.RiverValley, 1, Now));
        }
    }
}
