using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>History records (#73): the box at a year matches the live run, keyframes and deltas survive a save,
    /// pruning keeps what the book needs, role history, era titles and starts, pin time, positions, and the economy
    /// is the same with the records on or off.</summary>
    public class HistoryTests
    {
        const int Y = SimConfig.MonthsPerYear;

        /// <summary>Month by month, crossing each era the moment it is ready; calls <paramref name="atYear"/> right after the
        /// step that starts a game year (before any era crossing of that month).</summary>
        static void Watched(SimCore sim, int years, System.Action<int> atYear = null)
        {
            for (int m = 0; m < years * Y; m++)
            {
                sim.Step();
                if (sim.State.month % Y == 0) atYear?.Invoke((int)(sim.State.month / Y));
                if (sim.State.eraReady) sim.AdvanceEra();
            }
        }

        /// <summary>The box read straight off the live state, the way the records should reproduce it.</summary>
        static BoxAtYear Live(SimCore sim)
        {
            var s = sim.State;
            var r = new BoxAtYear { year = (int)(s.month / Y), month = s.month, era = s.era, population = s.population };
            foreach (var b in s.buildings)
            {
                var lot = b.lotId > 0 ? s.settle.lots.Find(l => l.id == b.lotId) : null;
                if (lot == null || lot.retired) continue;
                r.buildings.Add(new BuildingAt { id = b.id, type = b.type, lotId = b.lotId, x = b.x, z = b.z, yaw = b.yaw, w = b.w, d = b.d, look = s.era, foundedMonth = lot.foundedMonth });
            }
            for (int i = 0; i < s.settle.tier.Length; i++)
            {
                int v = (s.settle.tier[i] & 3) | ((s.settle.flags[i] & SiteMap.FLaid) != 0 ? 4 : 0);
                if (v != 0) r.paths.Add(new PathCellAt { cell = i, tier = v & 3, laid = (v & 4) != 0 });
            }
            r.shapings = s.nudges.Select(n => new ShapeRec { nudgeId = n.id, kind = n.kind, x = n.x, z = n.z, month = n.month, era = n.eraAtPlace }).ToList();
            return r;
        }

        static string Key(BoxAtYear b) => JsonUtility.ToJson(new BoxAtYear
        {
            year = b.year, month = b.month, era = b.era, population = b.population,
            buildings = b.buildings.OrderBy(x => x.id).ToList(), paths = b.paths, shapings = b.shapings.OrderBy(x => x.nudgeId).ToList(),
        });

        static void SameBox(BoxAtYear want, BoxAtYear got, string why)
        {
            Assert.IsNotNull(got, why);
            Assert.AreEqual(Key(want), Key(got), why);
        }

        // ---------- the box at a year ----------
        [TestCase(1)]
        [TestCase(7)]
        public void TheBoxAtAYearMatchesTheLiveRun(int seed)
        {
            var sim = NewSim(seed);
            var live = new Dictionary<int, BoxAtYear>();
            bool placed = false;
            Watched(sim, 75, year =>
            {
                if (year == 6 && !placed)
                {
                    placed = true;
                    float x = 0.5f, z = 0.5f;
                    sim.Settlement.OpenSpotNear(ref x, ref z, 0.5f);
                    sim.State.shapingCharges = 3f;
                    Assert.IsTrue(sim.PlaceShaping(SimCore.NudgeKind.Grove, x, z).Ok);
                }
                if (year % 5 == 0 || year < 4) live[year] = Live(sim);
            });
            Assert.IsTrue(sim.State.era >= Era.Village, "the run crossed an era, so looks change");
            Assert.Greater(live.Count, 14);
            foreach (var kv in live) SameBox(kv.Value, sim.BoxAt(kv.Key), $"seed {seed}, year {kv.Key}");
            Assert.IsNull(sim.BoxAt((int)(sim.State.month / Y) + 1), "a year that has not happened");
            Assert.Greater(sim.BoxAt(60).paths.Count, sim.BoxAt(1).paths.Count, "paths wear in as the town grows");
            Assert.AreEqual(Era.Camp, sim.BoxAt(1).era);
            Assert.AreEqual(sim.State.era, sim.BoxAt((int)(sim.State.month / Y)).era);
        }

        [Test]
        public void LooksFollowTheEraAndTheCampHearthIsClearedAtTheTown()
        {
            var sim = NewSim(3);
            Watched(sim, 90);
            Assert.AreEqual(Era.Town, sim.State.era, "seed 3 reaches the town");
            int before = (int)(sim.EraStartMonth(Era.Village) / Y) - 1, after = (int)(sim.EraStartMonth(Era.Village) / Y) + 1;
            int afterTown = (int)(sim.EraStartMonth(Era.Town) / Y) + 1;
            var camp = sim.State.buildings.First(b => b.type == BuildingType.Camp);
            Assert.IsTrue(sim.BoxAt(2).buildings.Any(b => b.id == camp.id && b.look == Era.Camp));
            Assert.IsTrue(sim.State.settle.lots.Any(l => l.buildingId == camp.id && l.retired), "the village clears the hearth");
            {
                Assert.IsTrue(sim.BoxAt(before).buildings.Any(b => b.id == camp.id), "the hearth stood before the village");
                Assert.IsFalse(sim.BoxAt(after).buildings.Any(b => b.id == camp.id), "and is gone after");
            }
            var hut = sim.State.buildings.First(b => b.type == BuildingType.Hut);
            Assert.AreEqual(Era.Town, sim.BoxAt(afterTown).buildings.First(b => b.id == hut.id).look, "the first hut is a house now");
            Assert.AreEqual(Era.Camp, sim.BoxAt(2).buildings.First(b => b.id == hut.id).look, "and was a hut");
        }

        [Test]
        public void KeyframesAndDeltasRoundTripThroughASave()
        {
            var sim = NewSim(11);
            Watched(sim, 65);
            var steps = sim.State.history.paths;
            CollectionAssert.AreEqual(new[] { 0, 10, 20, 30, 40, 50, 60 }, steps.Where(p => p.keyframe).Select(p => p.year).ToArray(), "a keyframe every ten years");
            Assert.IsTrue(steps.Any(p => !p.keyframe), "years in between are deltas");
            Assert.Less(steps.Where(p => !p.keyframe).Average(p => p.packed.Length), steps.Where(p => p.keyframe).Last().packed.Length, "a delta is smaller than a keyframe");

            var json = JsonUtility.ToJson(sim.State);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            for (int y = 0; y <= 65; y += 3) Assert.AreEqual(Key(sim.BoxAt(y)), Key(loaded.BoxAt(y)), $"year {y} after a save and load");
            Assert.AreEqual(sim.EraTitle(Era.Camp), loaded.EraTitle(Era.Camp));

            // The loaded sim goes on recording where the saved one stopped.
            Watched(loaded, 12);
            Watched(sim, 12);
            Assert.AreEqual(JsonUtility.ToJson(sim.State.history), JsonUtility.ToJson(loaded.State.history), "a loaded sim records the same as one that never stopped");
        }

        // ---------- the economy ----------
        static string Economy(SimState s) =>
            $"{s.month}|{s.population}|{s.food:0.####}|{s.wood:0.####}|{s.mood:0.####}|{s.rng.state}|{s.econRng.state}|{s.storyRng.state}|" +
            string.Join(",", s.buildings.Select(b => b.type + b.slot)) + "|" + string.Join(",", s.notables.Select(n => n.FullName + n.alive));

        [Test]
        public void TheEconomyIsTheSameWithTheRecordsOnOrOff()
        {
            var off = new SimConfig { recordHistory = false };
            var noPrune = new SimConfig { historyPruneYears = 0 };
            foreach (int seed in new[] { 2, 5 })
            {
                var a = NewSim(seed, off); var b = NewSim(seed); var c = NewSim(seed, noPrune);
                Watched(a, 100); Watched(b, 100); Watched(c, 100);
                Assert.AreEqual(Economy(a.State), Economy(b.State), $"seed {seed}: records on");
                Assert.AreEqual(Economy(a.State), Economy(c.State), $"seed {seed}: pruning off");
                Assert.IsEmpty(a.State.history.looks, "off records nothing");
            }
        }

        // ---------- pruning ----------
        [Test]
        public void PruningKeepsEveryBookWorthyEntryAndDropsOldLowOnes()
        {
            var cfg = new SimConfig { historyPruneYears = 0 };
            var sim = NewSim(4, cfg);
            Watched(sim, 100);
            var all = sim.State.chronicle.ToList();
            int low = all.Count(e => e.salience < 0.5f && e.month < sim.State.month - 30 * Y);
            Assert.Greater(low, 10, "the run has old low entries to drop");

            sim.Config.historyPruneYears = 30;
            int dropped = sim.PruneChronicle();
            Assert.Greater(dropped, 0);
            Assert.AreEqual(all.Count - dropped, sim.State.chronicle.Count);
            var left = new HashSet<int>(sim.State.chronicle.Select(e => e.id));
            foreach (var e in all.Where(sim.IsBookWorthy)) Assert.IsTrue(left.Contains(e.id), $"book-worthy {e.kind} '{e.text}' stays");
            foreach (var e in sim.State.chronicle.Where(e => e.month >= sim.State.month - 30 * Y)) Assert.IsTrue(all.Contains(e));
            Assert.AreEqual(all.Count(e => e.month >= sim.State.month - 30 * Y), sim.State.chronicle.Count(e => e.month >= sim.State.month - 30 * Y), "nothing recent goes");
            foreach (var kind in new[] { "founding", "era" }) Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == kind), kind);
            // Anyone's five best stories stay.
            foreach (var n in sim.State.notables)
            {
                var best = all.Where(e => e.notableId == n.id || e.otherNotableId == n.id).OrderByDescending(e => e.salience).ThenBy(e => e.month).ThenBy(e => e.id).Take(5);
                foreach (var e in best) Assert.IsTrue(left.Contains(e.id), $"{n.FullName}'s top-five {e.kind} stays");
            }
            Assert.AreEqual(dropped, sim.State.history.prunedEntries);
            Assert.IsTrue(sim.State.history.positions.All(p => left.Contains(p.id)), "positions go with their entries");
            Assert.AreEqual(0, sim.PruneChronicle(), "pruning twice drops nothing more");
        }

        [Test]
        public void ALongBoxPrunesItselfAndStaysSmall()
        {
            var sim = NewSim(8);
            Watched(sim, 120);
            var s = sim.State;
            int full = JsonUtility.ToJson(s).Length;
            int hist = JsonUtility.ToJson(s.history).Length;
            var cfg = new SimConfig { historyPruneYears = 0, recordHistory = false };
            var bare = NewSim(8, cfg);
            Watched(bare, 120);
            int bareSize = JsonUtility.ToJson(bare.State).Length;
            TestContext.WriteLine($"120-year save (seed 8): {full} chars; history records {hist} ({hist * 100 / full}%); pruned {s.history.prunedEntries} entries; " +
                                  $"without records or pruning {bareSize}; chronicle {s.chronicle.Count} entries; path steps {s.history.paths.Count}, " +
                                  $"looks {s.history.looks.Count}, role terms {s.history.roleTerms.Count}, positions {s.history.positions.Count}");
            Assert.Greater(s.history.prunedEntries, 0);
            Assert.Less(hist, full / 4, "the records stay a small part of the save");
            Assert.Less(full, bareSize * 6 / 5, "records minus pruning cost under a fifth of the save");
        }

        // ---------- roles, eras, pin, positions ----------
        [Test]
        public void RoleHistoryCoversEveryHolderAndHowTheyWonIt()
        {
            var sim = NewSim(6);
            Watched(sim, 90);
            var terms = sim.RoleHistory().ToList();
            Assert.Greater(terms.Count, 3);
            var chief = sim.RoleHistory(RoleType.Chief).First();
            Assert.AreEqual(0, chief.fromMonth);
            Assert.AreEqual("Contest", chief.how);
            foreach (var seat in sim.State.roles.Where(r => r.holderId >= 0))
            {
                var open = terms.Where(t => t.role == seat.role && t.toMonth < 0).ToList();
                Assert.AreEqual(1, open.Count, seat.role);
                Assert.AreEqual(seat.holderId, open[0].holderId);
                Assert.AreEqual(seat.holderId, sim.RoleHolderAt(seat.role, sim.State.month));
            }
            foreach (var t in terms.Where(t => t.toMonth >= 0)) Assert.GreaterOrEqual(t.toMonth, t.fromMonth);
            // One holder at a time per role.
            foreach (var g in terms.GroupBy(t => t.role))
            {
                var ordered = g.OrderBy(t => t.fromMonth).ToList();
                for (int i = 1; i < ordered.Count; i++)
                {
                    Assert.GreaterOrEqual(ordered[i - 1].toMonth, 0, $"{g.Key}: an earlier term is closed");
                    Assert.GreaterOrEqual(ordered[i].fromMonth, ordered[i - 1].toMonth - 0, $"{g.Key} terms don't overlap");
                }
            }
            var who = sim.FindNotable(chief.holderId);
            Assert.IsTrue(sim.TermsOf(who.id).Any());
        }

        [Test]
        public void EraStartsAndTitlesAreStoredAndNeverChange()
        {
            var sim = NewSim(9);
            Watched(sim, 95);
            Assert.GreaterOrEqual((int)sim.State.era, 1);
            var eraEntries = sim.State.chronicle.Where(e => e.kind == "era").OrderBy(e => e.month).ToList();
            Assert.AreEqual(0, sim.EraStartMonth(Era.Camp));
            for (int i = 0; i < eraEntries.Count; i++) Assert.AreEqual(eraEntries[i].month, sim.EraStartMonth((Era)(i + 1)), "era start months");
            string camp = sim.EraTitle(Era.Camp);
            StringAssert.StartsWith("The ", camp);
            Assert.AreEqual("", sim.EraTitle(sim.State.era), "the era that goes on has no title yet");
            var chapters = sim.BookChapters();
            Assert.AreEqual(camp, chapters[0].eraTitle);
            // The title is kept: more years and a pruned chronicle don't rewrite it.
            Watched(sim, 40);
            sim.PruneChronicle();
            Assert.AreEqual(camp, sim.EraTitle(Era.Camp));
            Assert.AreEqual(camp, sim.BookChapters()[0].eraTitle);
            Assert.AreEqual(camp, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)).history.eraTitles[0]);
        }

        [Test]
        public void ARelicTitleWinsAndTheBoxCompletionTitlesTheTown()
        {
            var sim = NewSim(2);
            sim.Advance(6);
            sim.State.chronicle.Add(new ChronicleEntry { id = 9000, month = sim.State.month - 3, kind = "keeper", text = "x", salience = 0.99f, relicId = RelicId.SkyCrumb });
            Assert.IsTrue(sim.ForceNextEra());
            sim.Step();
            Assert.AreEqual("The Years of the Sky Bread", sim.EraTitle(Era.Camp));
            Assert.AreEqual("", sim.EraTitle(Era.Town));
            sim.ForceCompleteBox();
            StringAssert.StartsWith("The ", sim.EraTitle(Era.Town), "the final era is titled when the box completes");
        }

        [Test]
        public void PinTimeAddsUpAndMakesTheLegendRule()
        {
            var sim = NewSim(1);
            var n = sim.State.notables[0];
            sim.State.pinnedNotableId = n.id;
            sim.Advance(sim.Config.StepHours * 30 + 1e-6);
            Assert.AreEqual(30 * sim.Config.StepHours, sim.PinnedHours(n.id), 0.01);
            Assert.IsFalse(sim.PinnedLongEnough(n.id));
            sim.State.pinnedNotableId = -1;
            sim.Advance(sim.Config.StepHours * 12);
            Assert.AreEqual(30 * sim.Config.StepHours, sim.PinnedHours(n.id), 0.01, "unpinned time doesn't count");
            sim.Config.pinLegendHours = 2f;
            Assert.IsTrue(sim.PinnedLongEnough(n.id));
            Assert.AreEqual(0f, sim.PinnedHours(9999));
        }

        [Test]
        public void EntriesCarryTheSpotTheyHappened()
        {
            var sim = NewSim(5);
            Watched(sim, 40);
            var withBuilding = sim.State.chronicle.Where(e => e.buildingId >= 0 && e.salience >= 0.5f).ToList();
            Assert.IsNotEmpty(withBuilding);
            foreach (var e in withBuilding)
            {
                var b = sim.State.buildings.First(x => x.id == e.buildingId);
                if (b.lotId == 0) continue;
                Assert.IsTrue(sim.EntryPosition(e, out float x, out float z), e.kind);
                Assert.AreEqual(b.x, x, 0.006f);
                Assert.AreEqual(b.z, z, 0.006f);
            }
            var personal = sim.State.chronicle.Where(e => e.buildingId < 0 && e.notableId >= 0 && e.salience >= 0.5f).ToList();
            Assert.IsNotEmpty(personal);
            Assert.IsTrue(personal.Any(e => sim.EntryPosition(e, out _, out _)), "a person's story is at their home");
            var era = sim.State.chronicle.First(e => e.kind == "era");
            Assert.IsFalse(sim.EntryPosition(era, out _, out _), "an era belongs to the whole box");
            Assert.Greater(sim.State.history.positions.Count, 5);
        }

        [Test]
        public void ShapingsAreRecordedWithTheirSpotAndEndedByATakeBack()
        {
            var sim = NewSim(12);
            sim.Advance(22);
            sim.State.shapingCharges = 3f;
            float x = 0.3f, z = 0.2f;
            Assert.IsTrue(sim.Settlement.OpenSpotNear(ref x, ref z, 0.5f));
            var res = sim.PlaceShaping(SimCore.NudgeKind.Grove, x, z);
            Assert.IsTrue(res.Ok);
            sim.Step();
            long placed = res.nudge.month;
            var rec = sim.State.history.shapes.Single(r => r.nudgeId == res.nudge.id);
            Assert.AreEqual(SimCore.NudgeKind.Grove, rec.kind);
            Assert.AreEqual(x, rec.x, 0.001f);
            Assert.AreEqual(placed, rec.month);
            Assert.IsTrue(sim.TakeBack(res.nudge.id));
            sim.Step();
            Assert.AreEqual(sim.State.month, rec.endMonth, "taken back");
        }

        [Test]
        public void TheRememberingStoneAppearsWhenTheSandalIsFound()
        {
            var sim = NewSim(2);
            sim.Advance(5);
            sim.State.relics.Add(new Relic { id = RelicId.LeftSandal, foundMonth = sim.State.month, x = 0.7f, z = -0.4f, ownerId = sim.State.notables[0].id });
            sim.Advance(1);
            var stone = sim.State.history.shapes.Single(r => r.kind == "stone");
            Assert.AreEqual(0.7f, stone.x, 0.001f);
            Assert.AreEqual(-0.4f, stone.z, 0.001f);
            sim.Advance(2);
            Assert.AreEqual(1, sim.State.history.shapes.Count(r => r.kind == "stone"), "once");
        }
    }
}
