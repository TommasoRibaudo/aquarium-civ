using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain; // not UnityEngine.Terrain
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// Organic placement in the slice sim (M2, #32): Growth Lab's criteria 2–6 as sim tests, plus lots that never move,
    /// the save, the migration from slots, and an economy placement can't touch. Numbers go to the log with a [Settle]
    /// tag (Unity console / dotnet test output). Timings are asserted under .NET only (Mono is 3–4× slower; tooling.md).
    /// </summary>
    public class SettleTests
    {
        static readonly (int seed, int preset)[] Runs = { (7, 0), (11, 1), (1, 2) };

        static void Log(string s)
        {
#if UNITY_2017_1_OR_NEWER
            UnityEngine.Debug.Log("[Settle] " + s);
#else
            TestContext.Progress.WriteLine("[Settle] " + s);
#endif
        }

        internal static SimCore NewBox(int seed, int preset = 0, bool settle = true, int slots = 20)
        {
            var cfg = new SimConfig { terrainPreset = preset, settle = settle, buildingSlots = slots };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>A player who watches every era moment as soon as it is ready.</summary>
        static void Month(SimCore sim)
        {
            sim.Step();
            sim.State.simHours = sim.State.month * sim.Config.StepHours;
            if (sim.State.eraReady) sim.AdvanceEra();
        }

        static void ToYear(SimCore sim, int year, Action<SimCore> each = null)
        {
            while (sim.State.month < year * 12L) { Month(sim); each?.Invoke(sim); }
        }

        /// <summary>The scripted Keeper of the Growth Lab time-lapse: rain, a grove, a channel, on open ground near spots
        /// set by the terrain (years 14, 33, 58).</summary>
        static void Keeper(SimCore sim)
        {
            var t = TerrainSpec.Preset(sim.TerrainPreset);
            float sx = t.mirror ? -1f : 1f;
            long m = sim.State.month;
            void Shape(string kind, float x, float z)
            {
                sim.Settlement.OpenSpotNear(ref x, ref z, 0.45f);
                sim.PlaceNudge(kind, x, z);
            }
            bool back = sim.TerrainPreset % 3 == 2;
            if (m == 14 * 12 + 4) Shape(SimCore.NudgeKind.Rain, back ? t.centreX + 1.5f : t.centreX + sx * 1.4f, back ? t.centreZ - 0.6f : t.centreZ + 1.0f);
            if (m == 33 * 12 + 2) Shape(SimCore.NudgeKind.Grove, t.centreX - sx * 1.6f, t.centreZ - 1.1f);
            if (m == 58 * 12 + 7) Shape(SimCore.NudgeKind.Channel, back ? t.centreX + 0.8f : t.centreX + sx * 0.9f, back ? t.centreZ - 0.9f : t.centreZ + 1.25f);
        }

        static readonly Dictionary<(int, int), SimCore> cache = new Dictionary<(int, int), SimCore>();

        internal static SimCore Century(int seed, int preset)
        {
            if (cache.TryGetValue((seed, preset), out var sim)) return sim;
            sim = NewBox(seed, preset);
            ToYear(sim, 120, Keeper);
            cache[(seed, preset)] = sim;
            return sim;
        }

        // ---------- geometry ----------
        [Test]
        public void RectGapAndRectCellsAreRight()
        {
            Assert.Less(SiteMap.RectGap(0, 0, 0, 1, 1, 0.5f, 0, 30, 1, 1), 0f);
            Assert.AreEqual(0.5f, SiteMap.RectGap(0, 0, 0, 1, 1, 1.5f, 0, 0, 1, 1), 1e-4f);
            Assert.Greater(SiteMap.RectGap(0, 0, 45, 1, 1, 1.3f, 0, 0, 1, 1), 0f);
            var cells = new List<int>();
            SiteMap.RectCells(0.05f, 0.05f, 0f, 0.5f, 0.3f, 0f, cells);
            Assert.AreEqual(15, cells.Count, "0.5 × 0.3 at 0.1 cells");
        }

        [Test]
        public void ANewBoxHasItsCampAndFirstHutPlaced()
        {
            var sim = NewBox(42);
            var st = sim.State.settle;
            Assert.AreEqual(SettleState.Version, st.version);
            Assert.AreEqual(2, sim.State.buildings.Count);
            Assert.IsTrue(sim.State.buildings.All(b => b.lotId != 0), "camp and hut have lots");
            var camp = sim.Settlement.LotOf(sim.State.buildings[0]);
            Assert.AreEqual(LotKind.Camp, camp.kind);
            Assert.AreEqual(sim.Settlement.GreenX, camp.x, 1e-5f);
            Assert.AreEqual(SettleState.Version, st.version);
            Assert.AreEqual(ClusterPlacer.CommonsCount, st.commons.Count, "three commons kept open");
            Assert.AreEqual(1, st.yards.Count, "the first hut founds a family yard");
        }

        // ---------- criterion 2: the nudge answer lands where the land was shaped ----------
        struct Trial { public bool answered; public float dist; public string type; public LotKind kind; }

        static Era EraFor(string kind) => kind switch
        {
            SimCore.NudgeKind.Channel or SimCore.NudgeKind.Lamp => Era.Village,
            SimCore.NudgeKind.Outcrop or SimCore.NudgeKind.Spring or SimCore.NudgeKind.DyeMeadow => Era.Town,
            _ => Era.Camp,
        };

        static Trial RunTrial(string kind, int i)
        {
            int seed = 1000 + i * 7919 + kind.Length * 31, preset = i % 3;
            var sim = NewBox(seed, preset);
            int year = 8 + (int)(Terrain.Hash3(seed, 1, 0) * 40f);
            ToYear(sim, year);
            while (sim.State.era < EraFor(kind)) sim.ForceNextEra();
            float x = -2.2f + 4.4f * Terrain.Hash3(seed, 2, 0), z = -1.5f + 3.0f * Terrain.Hash3(seed, 3, 0);
            sim.Settlement.OpenSpotNear(ref x, ref z, 0.45f);
            var n = sim.PlaceNudge(kind, x, z);
            for (int m = 0; m < 48 && n.lotId == 0; m++) Month(sim);
            var t = new Trial { dist = 99f };
            if (n.lotId == 0) return t;
            var lot = sim.Settlement.LotById(n.lotId);
            t.answered = true; t.kind = lot.kind;
            t.type = sim.State.buildings.First(b => b.id == n.buildingId).type;
            t.dist = Terrain.Dist(lot.x, lot.z, x, z);
            return t;
        }

        [Test]
        public void NudgeAnswersLandWithinOneUnit()
        {
            foreach (var kind in SimCore.NudgeKind.Gifts)
            {
                bool first = Array.IndexOf(SimCore.NudgeKind.All, kind) >= 0;
                int n = first ? 30 : 12, ok = 0, answered = 0;
                float sum = 0f;
                var kinds = new Dictionary<LotKind, int>();
                for (int i = 0; i < n; i++)
                {
                    var t = RunTrial(kind, i);
                    if (!t.answered) continue;
                    answered++; sum += t.dist;
                    if (t.dist <= 1f) ok++;
                    kinds[t.kind] = kinds.TryGetValue(t.kind, out var c) ? c + 1 : 1;
                }
                Log($"C2 {kind}: {ok}/{n} answers within 1.0 u ({100f * ok / n:0}%), {answered} answered, mean {sum / Math.Max(1, answered):0.00} u; as {string.Join(", ", kinds.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value))}");
                Assert.GreaterOrEqual(ok, (int)Math.Ceiling(0.8 * n), kind);
            }
        }

        [Test]
        public void AnswersSuitTheEraAndTheLand()
        {
            // A town's channel is answered by a watermill on the water; an outcrop's workshop follows the rock.
            var sim = NewBox(5);
            ToYear(sim, 15);
            while (sim.State.era < Era.Town) sim.ForceNextEra();
            var t = TerrainSpec.Preset(0);
            float x = t.centreX + 1.1f, z = t.centreZ + 1.2f;
            sim.Settlement.OpenSpotNear(ref x, ref z, 0.45f);
            var ch = sim.PlaceNudge(SimCore.NudgeKind.Channel, x, z);
            for (int m = 0; m < 40 && ch.lotId == 0; m++) Month(sim);
            Assert.AreNotEqual(0, ch.lotId, "the channel was answered");
            Assert.AreEqual(BuildingType.Workshop, sim.State.buildings.First(b => b.id == ch.buildingId).type);
            Assert.AreEqual(LotKind.Watermill, sim.Settlement.LotById(ch.lotId).kind, "a town answers a channel with a watermill");

            Assert.AreEqual("clay", SimCore.Deposit(t, t.pondX + 0.7f, t.pondZ), "by the water an outcrop is clay");
            Assert.IsTrue(SimCore.WetSpot(t, t.pondX, t.pondZ), "a herd flicked into the pond is fish");
            Assert.IsFalse(SimCore.WetSpot(t, t.centreX, t.centreZ));
        }

        // ---------- criterion 3: live = catch-up ----------
        [Test]
        public void LiveSteppingEqualsCatchUp()
        {
            SimCore Make()
            {
                var s = NewBox(7);
                s.Advance(10);
                s.ForceNextEra();
                float x = 0.6f, z = 0.9f;
                s.Settlement.OpenSpotNear(ref x, ref z, 0.45f);
                s.PlaceNudge(SimCore.NudgeKind.Rain, x, z);
                return s;
            }
            var a = Make(); var b = Make(); var c = Make();
            for (int m = 0; m < 576; m++) a.Advance(a.Config.StepHours);   // live: month by month
            b.Advance(48.0);                                                 // catch-up: one call
            for (int k = 0; k < 48 * 7; k++) c.Advance(1.0 / 7.0);           // live: odd-sized frames
            Assert.AreEqual(a.State.month, b.State.month);
            b.State.simHours = c.State.simHours = a.State.simHours; // summed in different steps: equal to rounding only
            string ja = JsonUtility.ToJson(a.State);
            Assert.AreEqual(ja, JsonUtility.ToJson(b.State), "month by month vs Advance(48 h)");
            Assert.AreEqual(ja, JsonUtility.ToJson(c.State), "small frames vs month by month");
            Log($"C3 live = catch-up over 576 months: identical ({a.State.settle.lots.Count} lots, era {a.State.era})");
        }

        // ---------- criterion 4: sanity ----------
        [Test]
        public void SanityOverThreeSeedsAndACentury()
        {
            foreach (var (seed, preset) in Runs)
            {
                var sim = Century(seed, preset);
                var s = SettleChecks.Check(sim.Settlement, sim.State.buildings);
                var st = sim.State.settle;
                var homes = sim.Settlement.Homes();
                var sizes = st.yards.Select(k => sim.Settlement.HomesOfYard(k.id).Count).ToList();
                Log($"C4 seed {seed}/preset {preset}: year 120, era {sim.State.era}, pop {sim.State.population}; {s}; " +
                    $"{homes.Count} homes in {st.yards.Count} yards (sizes {string.Join(",", sizes)}), {st.yards.Count(k => k.outbuildingLot != 0)} barns; no-lot months {st.noLotMonths}");
                Assert.AreEqual(Era.Town, sim.State.era, "eras advanced");
                Assert.AreEqual(0, s.overlaps, "overlapping lots");
                Assert.AreEqual(0, s.inMargin, "lots in the glass margin");
                Assert.AreEqual(0, s.unconnected, "lots without a path to the green");
                Assert.AreEqual(0, s.unplacedBuildings, "every building the economy built has a lot");
                Assert.GreaterOrEqual(s.lots, sim.State.buildings.Count - 2, "a lot for every building (hearth and well stand on the green)");
                foreach (var b in sim.State.buildings)
                {
                    var lot = sim.Settlement.LotOf(b);
                    Assert.IsNotNull(lot, b.type);
                    Assert.AreEqual(lot.x, b.x); Assert.AreEqual(lot.z, b.z); Assert.AreEqual(lot.doorX, b.doorX);
                }
                // The commons and the yards are never built on.
                foreach (var l in st.lots)
                    if (!l.open)
                    {
                        var cells = new List<int>();
                        SiteMap.RectCells(l.x, l.z, l.yaw, l.w, l.d, 0f, cells);
                        foreach (int c in cells) Assert.IsFalse(sim.Settlement.Map.CommonsAt(c) || sim.Settlement.Map.YardAt(c), $"lot {l.id} on a commons or yard");
                    }
            }
        }

        [Test]
        public void FamiliesGrowRoundTheirYardsAndGetBarns()
        {
            int families = 0, big = 0, barns = 0;
            foreach (var (seed, preset) in Runs)
            {
                var sim = Century(seed, preset);
                foreach (var k in sim.State.settle.yards)
                {
                    families++;
                    if (sim.Settlement.HomesOfYard(k.id).Count + (k.outbuildingLot != 0 ? 1 : 0) >= 3) big++;
                    if (k.outbuildingLot != 0) barns++;
                }
                // Every notable has a home, and partners share it.
                foreach (var n in sim.State.notables.Where(q => q.alive))
                {
                    var home = sim.Settlement.HomeOf(n);
                    Assert.IsNotNull(home, n.FullName);
                    Assert.IsTrue(Encyclopedia.IsHome(home.kind));
                    if (n.partnerId >= 0)
                    {
                        var p = sim.State.notables.First(q => q.id == n.partnerId);
                        if (p.alive) Assert.AreEqual(home.id, sim.Settlement.HomeOf(p).id, "partners live together");
                    }
                }
            }
            Log($"families: {big}/{families} yards are farmsteads of 3+ buildings (homes + barn), {barns} barns");
            Assert.Greater(barns, families / 2, "most family yards have a barn by the town era");
            Assert.Greater(big, families / 2, "most families read as a farmstead of three or more");
        }

        [Test]
        public void AHouseholdKeepsItsHomeWhenNewHomesAreBuilt()
        {
            int checkedPairs = 0, newHomes = 0;
            foreach (var (seed, preset) in new[] { (3, 0), (11, 1), (42, 2) })
            {
                var sim = NewBox(seed, preset);
                var seen = new Dictionary<int, (int head, int lot)>();
                int homesBefore = 0;
                while (sim.State.month < 100 * 12L)
                {
                    Month(sim);
                    int homesNow = sim.Settlement.Homes().Count;
                    if (homesNow > homesBefore) newHomes++;
                    homesBefore = homesNow;
                    foreach (var n in sim.State.notables)
                    {
                        if (!n.alive) continue;
                        var home = sim.Settlement.HomeOf(n);
                        if (home == null) continue;
                        int head = sim.Settlement.HouseholdHead(n).id;
                        if (seen.TryGetValue(n.id, out var prev) && prev.head == head && !sim.Settlement.Lots.First(l => l.id == prev.lot).retired)
                        {
                            Assert.AreEqual(prev.lot, home.id, $"{n.FullName} moved house in month {sim.State.month} (seed {seed})");
                            checkedPairs++;
                        }
                        seen[n.id] = (head, home.id);
                    }
                }
            }
            Assert.Greater(newHomes, 10, "homes were built while households lived");
            Assert.Greater(checkedPairs, 1000);
        }

        [Test]
        public void HomeAssignmentsSurviveASaveAndAnOldSaveGetsThemFromTheHash()
        {
            var sim = NewBox(42, 0);
            ToYear(sim, 60);
            var living = sim.State.notables.Where(q => q.alive).ToList();
            var before = living.ToDictionary(n => n.id, n => sim.Settlement.HomeOf(n).id);
            Assert.IsNotEmpty(sim.State.settle.homes);
            var json = JsonUtility.ToJson(sim.State.settle);
            var loaded = JsonUtility.FromJson<SettleState>(json);
            Assert.AreEqual(sim.State.settle.homes.Count, loaded.homes.Count);
            for (int i = 0; i < loaded.homes.Count; i++)
            {
                Assert.AreEqual(sim.State.settle.homes[i].head, loaded.homes[i].head);
                Assert.AreEqual(sim.State.settle.homes[i].lot, loaded.homes[i].lot);
            }
            // An old save has no assignments: they are derived once, from the hash over today's homes.
            sim.State.settle.homes.Clear();
            var again = new SimCore(sim.Config, sim.State);
            _ = again.Settlement;
            Assert.IsNotEmpty(sim.State.settle.homes);
            foreach (var n in living) Assert.IsNotNull(again.Settlement.HomeOf(n));
        }

        [Test]
        public void AFullBoxRecordsMonthsWithNoGoodLot()
        {
            // The economy asks for far more than fits (debug builds every year in the town era): the layout stays sane,
            // the builds that find nothing wait, and the months are counted for the town's peak (era-moment.md §8).
            var sim = NewBox(3, 0, slots: 200);
            ToYear(sim, 30);
            while (sim.State.era < Era.Town) sim.ForceNextEra();
            var kinds = new[] { BuildingType.Hut, BuildingType.Workshop, BuildingType.Farm, BuildingType.Hut };
            for (int y = 0; y < 40; y++)
            {
                for (int k = 0; k < 3; k++) sim.DebugBuild(kinds[(y + k) % kinds.Length]);
                for (int m = 0; m < 12; m++) Month(sim);
            }
            var s = SettleChecks.Check(sim.Settlement, sim.State.buildings);
            var st = sim.State.settle;
            Log($"full box: {s}; no-lot builds {st.noLotBuilds}, no-lot months {st.noLotMonths}, waiting now for {sim.Settlement.MonthsWithoutLot} months");
            Assert.AreEqual(0, s.overlaps);
            Assert.AreEqual(0, s.inMargin);
            Assert.AreEqual(0, s.unconnected);
            Assert.Greater(st.noLotBuilds, 0, "the box filled up");
            Assert.Greater(st.noLotMonths, 0);
            Assert.GreaterOrEqual(sim.Settlement.MonthsWithoutLot, 36, "the peak criterion's 3 years");
            Assert.Greater(s.waiting, 0);
        }

        // ---------- criterion 5: emergence ----------
        [Test]
        public void PathsEmergeThatNoConnectionLaid()
        {
            foreach (var (seed, preset) in Runs)
            {
                var sim = Century(seed, preset);
                var e = SettleChecks.Emerge(sim.Settlement.Map);
                var curves = sim.Settlement.Paths();
                Log($"C5 seed {seed}/preset {preset}: {e}; {curves.Count} path curves");
                Assert.GreaterOrEqual(e.largestEmergent, 5, "a path at least 0.5 u long that no connection step laid");
                Assert.Greater(e.roadCells, 0, "a main street worn into a road by the town era");
                Assert.Greater(curves.Count, 0);
                foreach (var c in curves)
                {
                    Assert.GreaterOrEqual(c.Count, 2);
                    Assert.Greater(c.cells.Count, 1);
                    Assert.LessOrEqual(c.foundedMonth, sim.State.month);
                }
            }
        }

        [Test]
        public void ThePlannerCanWalkFromHomeToTheGreen()
        {
            var sim = Century(7, 0);
            var route = new List<int>();
            foreach (var h in sim.Settlement.Homes())
            {
                Assert.IsTrue(sim.Settlement.Route(h.doorX, h.doorZ, sim.Settlement.GreenX, sim.Settlement.GreenZ, route), $"home {h.id}");
                Assert.Greater(route.Count, 0);
            }
            sim.Settlement.Fire(out float fx, out float fz, out bool lit);
            Assert.IsFalse(lit, "the camp's hearth gave way to the green");
            sim.Settlement.MarketCentre(out float mx, out float mz);
            Assert.Less(Terrain.Dist(mx, mz, sim.Settlement.GreenX, sim.Settlement.GreenZ), 1.5f, "the market sits by the green");
            var features = sim.Settlement.Features();
            Assert.IsTrue(features.Any(f => f.kind == "pond"));
            Assert.IsTrue(features.Any(f => f.kind == SimCore.NudgeKind.Grove && f.lotId != 0), "the grove was answered");
            Assert.AreEqual(sim.Settlement.Standing().Count(), sim.Settlement.RebuildOrder().Count);
        }

        // ---------- criterion 6: cost ----------
        [Test]
        public void CostPerMonthAndCatchUp()
        {
            NewBox(3).Advance(5); // warm up (JIT)
            var sim = NewBox(7);
            var sw = Stopwatch.StartNew();
            ToYear(sim, 60, Keeper);
            double total = sw.Elapsed.TotalMilliseconds;
            var w = sim.Settlement;
            double f = 1000.0 / Stopwatch.Frequency;
            double place = w.placeTicks * f / w.monthsStepped, wear = w.wearTicks * f / w.monthsStepped;
            sw.Restart();
            sim.Advance(48.0 + (sim.State.month * sim.Config.StepHours - sim.State.simHours)); // 576 months in one call
            double catchUp = sw.Elapsed.TotalMilliseconds;
            Assert.AreEqual(108 * 12, sim.State.month);
            Log($"C6 placement {place:0.000} + wear {wear:0.000} = {place + wear:0.000} ms per month (whole sim {total / 720:0.000} ms per month); " +
                $"576-month catch-up from year 60 (whole sim): {catchUp:0} ms");
#if !UNITY_2017_1_OR_NEWER
            Assert.Less(place + wear, 1.0, "placement + wear per month");
            Assert.Less(catchUp, 300.0, "576-month catch-up");
#endif
        }

        // ---------- lots never move ----------
        [Test]
        public void LotsNeverMoveAcrossEras()
        {
            var sim = NewBox(11, 1);
            var first = new Dictionary<int, (float, float, float, float, float)>();
            int eras = 0;
            Era last = sim.State.era;
            ToYear(sim, 110, s =>
            {
                Keeper(s);
                if (s.State.era != last) { eras++; last = s.State.era; }
                if (s.State.month % 12 != 0 && s.State.era == last) return;
                foreach (var l in s.State.settle.lots)
                {
                    var now = (l.x, l.z, l.yaw, l.w, l.d);
                    if (!first.TryGetValue(l.id, out var was)) { first[l.id] = now; continue; }
                    Assert.AreEqual(was, now, $"lot {l.id} ({l.kind}) moved");
                }
            });
            Assert.AreEqual(2, eras, "camp → village → town");
            Assert.Greater(first.Count, 15);
        }

        // ---------- the save ----------
        [Test]
        public void ASavedBoxLoadsAndGoesOnTheSameWay()
        {
            var sim = NewBox(9);
            ToYear(sim, 40, Keeper);
            var json = JsonUtility.ToJson(sim.State);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            Assert.AreEqual(json, JsonUtility.ToJson(loaded.State), "round trip");
            for (int m = 0; m < 240; m++) { Month(sim); Month(loaded); }
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(loaded.State), "a loaded box places and wears exactly as one that never stopped");
            Log($"save: {json.Length / 1024} KB of JSON at year 40 ({sim.State.settle.lots.Count} lots)");
        }

        [Test]
        public void AnOldSaveIsMigratedFromSlotsOnce()
        {
            // A box saved before placement: buildings with slots only, shapings and trash with slots.
            var old = NewBox(13, settle: false);
            ToYear(old, 25);
            old.PlaceNudge(SimCore.NudgeKind.Grove, 4);
            old.AddMess(0.2f, 0.2f, 5);
            ToYear(old, 45);
            var json = JsonUtility.ToJson(old.State);
            Assert.AreEqual(0, old.State.settle.version);

            SimCore Load() => new SimCore(new SimConfig(), JsonUtility.FromJson<SimState>(json));
            var a = Load();
            var w = a.Settlement; // first use migrates
            var s = SettleChecks.Check(w, a.State.buildings);
            Log($"migration: {s}; {a.State.settle.yards.Count} yards, {SettleChecks.Emerge(w.Map).pathCells} path cells");
            Assert.AreEqual(SettleState.Version, a.State.settle.version);
            Assert.AreEqual(0, s.unplacedBuildings, "every old building has a lot");
            Assert.AreEqual(0, s.overlaps);
            Assert.AreEqual(0, s.unconnected);
            Assert.IsTrue(a.State.nudges.All(n => n.x != 0f || n.z != 0f), "shapings have a spot");
            Assert.IsTrue(a.State.trash.All(t => t.x != 0f || t.z != 0f), "trash has a spot");
            Assert.AreEqual(JsonUtility.ToJson(old.State.buildings.Select(b => b.type + b.slot).ToList()),
                JsonUtility.ToJson(a.State.buildings.Select(b => b.type + b.slot).ToList()), "slots are kept");
            // Deterministic and only once: the same old save migrates the same way, and a migrated box doesn't migrate again.
            var b2 = Load(); _ = b2.Settlement;
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b2.State));
            var again = new SimCore(new SimConfig(), JsonUtility.FromJson<SimState>(JsonUtility.ToJson(a.State)));
            _ = again.Settlement;
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(again.State), "no second migration");
            ToYear(a, 60);
            Assert.AreEqual(0, SettleChecks.Check(a.Settlement, a.State.buildings).overlaps, "goes on placing after the migration");
        }

        // ---------- the economy is placement's to read, never the other way round ----------
        [Test]
        public void TheEconomyIsUnchangedByPlacement()
        {
            foreach (var seed in new[] { 1234, 7 })
            {
                var on = NewBox(seed); var off = NewBox(seed, settle: false);
                void Script(SimCore s)
                {
                    long m = s.State.month;
                    // Every gift tool once, at fixed spots (the economy only reads the terrain at the spot).
                    for (int k = 0; k < SimCore.NudgeKind.Gifts.Length; k++)
                        if (m == (10 + 9 * k) * 12 + 3) s.PlaceNudge(SimCore.NudgeKind.Gifts[k], -1.2f + 0.35f * k, 0.6f - 0.2f * k);
                }
                ToYear(on, 100, Script); ToYear(off, 100, Script);
                Assert.AreEqual(JobTests.Economy(off.State), JobTests.Economy(on.State), $"seed {seed}: economy");
                Assert.AreEqual(Fingerprint(off.State), Fingerprint(on.State), $"seed {seed}: world fingerprint");
                Assert.AreEqual(off.State.nudges.Count(n => n.credited), on.State.nudges.Count(n => n.credited));
                Assert.Greater(on.State.settle.lots.Count, 10);
                Assert.AreEqual(0, off.State.settle.lots.Count);
            }
        }
    }
}
