using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// The ecology layer in the game sim (#243): the Ecology Lab's land model (#228) behind <see cref="SimConfig.ecology"/>,
    /// off by default. Scripted Keepers on the lab's real-hour clock (1 real hour = 1 game year, a season = 15 minutes) play
    /// seed 7 on the first terrain, so the numbers can be set beside the lab's own (care: camp Y4.3, village Y25.3; no
    /// player: camp Y25.3).
    /// </summary>
    public partial class EcoTests
    {
        // ---------------------------------------------------------------- helpers
        static SimCore EcoWorld(int seed = 7, SimConfig cfg = null)
        {
            cfg ??= new SimConfig();
            cfg.ecology = true;
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>The lab's scripted Keeper (Player.cs): (c) full care checks in every waking hour, (a) never comes.
        /// The run starts at 08:00; the first session is half an hour.</summary>
        sealed class Keeper
        {
            readonly bool care;
            int spotsMisted;
            public Keeper(bool care) { this.care = care; }

            static bool Awake(int season) => (8 + season / 4) % 24 >= 8;

            bool VisitsAt(int season)
            {
                if (!care) return false;
                if (season <= 1) return true;
                return season % 4 == 0 && Awake(season);
            }

            public void Play(SimCore sim)
            {
                var e = sim.State.eco;
                if (!VisitsAt(e.season)) return;
                PlaceReadyJars(sim); // #417-#419: the Keeper places every jar that is ready, by the camp
                var land = sim.EcoGround;
                // The lab plans the whole visit from the land as it stands on arrival, then does it.
                (float x, float z)? plant = e.treeCell < 0 && sim.CanPlantTree ? PlantSpot(sim, land) : ((float, float)?)null;
                int spots = 3;
                var mists = new List<(float x, float z)>();
                int tc = e.treeCell >= 0 ? e.treeCell : plant.HasValue ? SiteMap.CellAt(plant.Value.x, plant.Value.z) : -1;
                if (tc >= 0 && e.treeStage < 4) { mists.Add((SiteMap.CXc(tc), SiteMap.CZc(tc))); spots--; }
                for (int k = 0; k < spots; k++) { mists.Add(MistSpot(sim, land, k)); spotsMisted++; }
                if (plant.HasValue) sim.PlantTree(plant.Value.x, plant.Value.z);
                foreach (var m in mists) sim.Mist(m.x, m.z, 2f);
                sim.CleanGrime(1f);
                sim.Prune(1f);
            }

            (float x, float z) MistSpot(SimCore sim, EcoLand land, int k)
            {
                var e = sim.State.eco;
                float reach = MathF.Min(2.8f, 0.7f + spotsMisted * 0.08f);
                float best = float.MaxValue, bx = land.campX, bz = land.campZ;
                for (int tries = 0; tries < 10; tries++)
                {
                    float a = Terrain.Hash3(e.seed * 31 + 101, e.season * 16 + k, tries) * MathF.PI * 2f;
                    float r = MathF.Sqrt(Terrain.Hash3(e.seed * 31 + 102, e.season * 16 + k, tries)) * reach;
                    float x = land.campX + MathF.Cos(a) * r, z = land.campZ + MathF.Sin(a) * r;
                    if (MathF.Abs(x) > Terrain.HX - 0.2f || MathF.Abs(z) > Terrain.HZ - 0.2f) continue;
                    int c = SiteMap.CellAt(x, z);
                    if (c < 0 || (EcoCover)e.cover[c] == EcoCover.Water) continue;
                    float score = e.moisture[c] + ((EcoCover)e.cover[c] == EcoCover.Bare ? 0f : 0.3f);
                    if (score < best) { best = score; bx = x; bz = z; }
                }
                return (bx, bz);
            }

            static (float x, float z) PlantSpot(SimCore sim, EcoLand land)
            {
                var e = sim.State.eco;
                float best = float.MinValue, bx = land.campX, bz = land.campZ;
                for (int k = 0; k < 24; k++)
                {
                    float a = k / 24f * MathF.PI * 2f, r = 0.8f + 0.2f * (k % 2);
                    float x = land.campX + MathF.Cos(a) * r, z = land.campZ + MathF.Sin(a) * r;
                    int c = SiteMap.CellAt(x, z);
                    if (c < 0 || land.InBasin(c) || land.yard[c] || land.slope[c] > 0.3f) continue;
                    float score = e.moisture[c] - 0.1f * Terrain.Dist(x, z, 0f, 0f);
                    if (score > best) { best = score; bx = SiteMap.CXc(c); bz = SiteMap.CZc(c); }
                }
                return (bx, bz);
            }
        }

        /// <summary>#417: every ready jar's group goes on the valid cell nearest the camp (what a scripted Keeper does on a visit).</summary>
        internal static void PlaceReadyJars(SimCore sim)
        {
            var ready = new List<LifeSpecies>();
            sim.ReadyJars(ready);
            foreach (var sp in ready)
            {
                int c = sim.LifePlaceCellNearCamp(sp);
                if (c >= 0) sim.PlaceLife(sp, c);
            }
        }

        /// <summary>A scripted run, with what was watched along the way.</summary>
        sealed class RunResult
        {
            public SimCore sim;
            public bool pondShrank, diedBack;
            public int lastPond;
            public double ms;
            public float Year(string milestone) { long m = sim.EcoMilestoneMonth(milestone); return m < 0 ? -1f : m / 12f; }
            public float CampYear => sim.State.eco.campGateMonth < 0 ? -1f : sim.State.eco.campGateMonth / 12f;
            public float VillageYear => sim.State.eco.villageGateMonth < 0 ? -1f : sim.State.eco.villageGateMonth / 12f;
        }

        static readonly Dictionary<string, RunResult> cache = new Dictionary<string, RunResult>();

        /// <summary>Plays <paramref name="seasons"/> seasons (a quarter real hour each), watching that the pond never dries and
        /// nothing dies back (the lab's own test). Cached: several tests read the same run.</summary>
        static RunResult Play(bool care, int seasons, int seed = 7)
        {
            string key = $"{care}-{seasons}-{seed}";
            lock (cache)
            {
                if (cache.TryGetValue(key, out var hit)) return hit;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var r = new RunResult { sim = EcoWorld(seed) };
                var sim = r.sim; var keeper = new Keeper(care);
                var was = new byte[EcoLand.N];
                for (int s = 0; s < seasons; s++)
                {
                    keeper.Play(sim);
                    Array.Copy(sim.State.eco.cover, was, EcoLand.N);
                    sim.Advance(0.25);
                    var now = sim.State.eco.cover;
                    for (int c = 0; c < EcoLand.N; c++)
                    {
                        var a = (EcoCover)was[c]; var b = (EcoCover)now[c];
                        if (a == EcoCover.Water && b != EcoCover.Water) r.pondShrank = true;
                        else if (b != EcoCover.Water && b != EcoCover.Reeds && a != EcoCover.Reeds && (int)b < (int)a
                                 && !(a == EcoCover.Bush && (b == EcoCover.Grass || b == EcoCover.Tall)) // #385: bushes thin back to meadow
                                 && !(b == EcoCover.Grass && Cleared(sim, c))) r.diedBack = true; // #385: the people clear their lots, paths and green
                    }
                    int pond = sim.EcoPondCells;
                    if (pond < r.lastPond) r.pondShrank = true;
                    r.lastPond = pond;
                }
                r.ms = sw.Elapsed.TotalMilliseconds;
                return cache[key] = r;
            }
        }

        const int Seasons30 = 30 * 4;

        /// <summary>#385: a cell the people keep clear (a lot, a path, the green): what grows there goes back to grass.</summary>
        static bool Cleared(SimCore sim, int c)
        {
            var map = sim.Settlement?.Map;
            return map != null && (map.lot[c] != 0 || map.tier[c] >= PathNet.Trail || map.Green(c));
        }

        // ---------------------------------------------------------------- off = untouched
        [Test]
        public void OffByDefault_NothingChanges()
        {
            Assert.IsFalse(new SimConfig().ecology, "off by default");
            var a = NewSim(21); var b = NewSim(21);
            Assert.IsFalse(a.State.eco.on);
            Assert.IsNull(a.State.eco.cover, "no layers are made");
            a.Advance(30); b.Advance(30);
            Assert.IsFalse(a.EcologyOn);
            Assert.IsFalse(a.BeforeCamp);
            Assert.IsFalse(a.State.eco.on);
            Assert.IsNull(a.State.eco.cover);
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b.State), "same world");
            Assert.Greater(a.State.buildings.Count, 2, "an old box builds on");
            Assert.IsFalse(a.State.chronicle.Any(c => c.kind.StartsWith("eco_")));
            // the Keeper's calls are no-ops
            long rngBefore = a.State.rng.state;
            Assert.AreEqual(0, a.Mist(0f, 0f, 2f).Count);
            Assert.IsFalse(a.CanPlantTree);
            Assert.IsFalse(a.PlantTree(0f, 0f));
            Assert.IsFalse(a.DebugOpenCampGate());
            Assert.AreEqual(rngBefore, a.State.rng.state);
            Assert.AreEqual(0, a.KeeperActionsDone(KeeperAction.Mist));
            Assert.AreEqual(-1, a.EcoAt(0f, 0f).cell);
        }

        [Test]
        public void OldSaveWithoutEco_LoadsWithItOff()
        {
            var sim = NewSim(5);
            sim.Advance(6);
            string json = JsonUtility.ToJson(sim.State);
            string old = Regex.Replace(json, "\"eco\":\\{[^{}]*\\},", "");
            Assert.AreNotEqual(json, old, "the eco block was cut out");
            var loaded = JsonUtility.FromJson<SimState>(old);
            Assert.IsNotNull(loaded.eco);
            Assert.IsFalse(loaded.eco.on);
            var core = new SimCore(new SimConfig(), loaded);
            core.Advance(6);
            Assert.IsFalse(core.EcologyOn);
        }

        [Test]
        public void TheEconomysDice_AreNeverTouched()
        {
            // The economy draws 3 numbers a month from econRng whatever happens; the ecology draws none anywhere.
            var off = NewSim(7); var on = EcoWorld(7);
            off.Advance(10); on.Advance(10);
            Assert.AreEqual(off.State.month, on.State.month);
            Assert.AreEqual(off.State.econRng.state, on.State.econRng.state);
            // The Keeper's hands draw nothing either.
            var rng = (on.State.rng.state, on.State.econRng.state, on.State.storyRng.state);
            on.Mist(0f, 0f, 2f); on.DebugOpenCampGate(); on.DebugTreeStage(2); on.DebugSkipSeasons(0);
            Assert.AreEqual(rng, (on.State.rng.state, on.State.econRng.state, on.State.storyRng.state));
        }

        // ---------------------------------------------------------------- the start
        [Test]
        public void ANewBoxStartsBarren_OneTentAndAFire()
        {
            var sim = EcoWorld();
            var e = sim.State.eco;
            Assert.IsTrue(e.on);
            Assert.IsTrue(sim.BeforeCamp);
            Assert.AreEqual(2, sim.State.buildings.Count, "the fire and one tent");
            Assert.AreEqual(1, sim.Count(BuildingType.Hut));
            Assert.IsTrue(e.cover.All(c => c == (byte)EcoCover.Bare), "all bare");
            Assert.Less(e.table, sim.EcoGround.minHeight, "the water table starts below the lowest ground");
            Assert.AreEqual(0, sim.EcoPondCells);
            Assert.AreEqual(-1, e.treeCell, "no tree yet");
            Assert.IsFalse(sim.CanPlantTree);
            Assert.AreEqual(1f, sim.EcoBareShare, 1e-6f);
            Assert.Greater(sim.EcoFloodLine, sim.EcoGround.main.bottom);
            Assert.Greater(sim.EcoGround.main.cells.Count, 50, "a real hollow");
            // both camp buildings have their lots, none below the flood line
            Assert.IsTrue(sim.State.buildings.All(b => b.lotId != 0), "the camp and the tent are placed");
            AssertNoLotInTheHollow(sim);
        }

        [Test]
        public void NoBuildingBeforeTheCampGate()
        {
            var sim = EcoWorld();
            sim.State.wood = 500; sim.State.population = 30; sim.State.food = 200;
            for (int m = 0; m < 24; m++) { sim.State.wood = 500; sim.State.food = 200; sim.Advance(1.0 / 12); }
            if (sim.State.eco.campGateMonth >= 0) Assert.Inconclusive("the gate opened inside two years");
            Assert.IsTrue(sim.BeforeCamp);
            Assert.AreEqual(2, sim.State.buildings.Count, "nothing is built while the land is barren");
            Assert.IsNull(sim.NextWant().building, "nothing is wanted either");
            // The gate opens: the same box builds on.
            sim.DebugAddHides(SimCore.HidesPerTent); // #284: a tent is made of hides
            Assert.IsTrue(sim.DebugOpenCampGate());
            Assert.IsFalse(sim.BeforeCamp);
            for (int m = 0; m < 12; m++) { sim.State.wood = 500; sim.State.food = 200; sim.Advance(1.0 / 12); }
            Assert.Greater(sim.State.buildings.Count, 2, "the camp grows once the gate is open");
        }

        // ---------------------------------------------------------------- the Keeper's hands
        [Test]
        public void AMist_GreensTheCoreAtOnce()
        {
            var sim = EcoWorld();
            var land = sim.EcoGround;
            float x = land.campX + 0.9f, z = land.campZ;
            int greenedEvents = 0; IReadOnlyList<int> told = null; string firstMilestone = null;
            sim.EcoGreened += cells => { greenedEvents++; told = cells; };
            sim.EcoMilestone += k => firstMilestone ??= k;
            long monthBefore = sim.State.month;
            var cells = sim.Mist(x, z, 2f);
            Assert.AreEqual(monthBefore, sim.State.month, "no season has passed");
            Assert.Greater(cells.Count, 4, "the core of the mist is a handful of cells");
            Assert.LessOrEqual(cells.Count, 20);
            foreach (int c in cells)
            {
                Assert.AreEqual(EcoCover.Grass, sim.EcoCoverAt(c));
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z);
                Assert.LessOrEqual(d, 0.45f * 0.4f + 1e-4f, "only the core greens");
            }
            Assert.AreEqual(1, greenedEvents);
            Assert.AreEqual(cells.Count, told.Count);
            Assert.AreEqual(EcoMilestones.FirstGreen, firstMilestone);
            Assert.Greater(sim.EcoAt(x, z).moisture, 0.3f, "and the ground is wet");
            Assert.AreEqual(1, sim.KeeperActionsDone(KeeperAction.Mist), "a Keeper action");
            Assert.Less(sim.EcoBareShare, 1f);
            // a second mist on the same spot has nothing left to green
            Assert.AreEqual(0, sim.Mist(x, z, 2f).Count);
            // a long press is capped
            var cap = EcoWorld();
            Assert.AreEqual(cap.Mist(x, z, 4f).Count, EcoWorld().Mist(x, z, 400f).Count);
        }

        [Test]
        public void TheSeedWaitsForBushes_ThenOneTreeCanBePlanted()
        {
            var sim = EcoWorld();
            Assert.IsFalse(sim.CanPlantTree);
            Assert.IsFalse(sim.PlantTree(sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ));
            var run = Play(true, Seasons30);
            Assert.IsTrue(run.sim.State.eco.Reached(EcoMilestones.SeedUnlocked));
            Assert.GreaterOrEqual(run.Year(EcoMilestones.TreePlanted), run.Year(EcoMilestones.SeedUnlocked));
            Assert.IsFalse(run.sim.CanPlantTree, "one tree only");
            // never in the hollow
            var fresh = EcoWorld();
            fresh.DebugOpenCampGate();
            fresh.State.eco.milestoneKeys.Add(EcoMilestones.SeedUnlocked); fresh.State.eco.milestoneMonths.Add(0);
            var main = fresh.EcoGround.main;
            int hollow = main.cells[main.cells.Count / 2];
            Assert.IsTrue(fresh.CanPlantTree);
            Assert.IsFalse(fresh.PlantTree(SiteMap.CXc(hollow), SiteMap.CZc(hollow)), "not in the hollow");
            Assert.IsTrue(fresh.PlantTree(fresh.EcoGround.campX + 0.9f, fresh.EcoGround.campZ));
            Assert.IsTrue(fresh.EcoTree.planted);
            Assert.AreEqual(0, fresh.EcoTree.stage);
            Assert.AreEqual(1, fresh.KeeperActionsDone(KeeperAction.Plant));
        }

        // ---------------------------------------------------------------- determinism, save, catch-up
        [Test]
        public void SameSeed_SameLand_AndADifferentSeedIsAnotherBox()
        {
            var a = EcoWorld(7); var b = EcoWorld(7); var c = EcoWorld(8);
            var ka = new Keeper(true); var kb = new Keeper(true); var kc = new Keeper(true);
            for (int s = 0; s < 40; s++)
            {
                ka.Play(a); kb.Play(b); kc.Play(c);
                a.Advance(0.25); b.Advance(0.25); c.Advance(0.25);
            }
            Assert.AreEqual(a.EcoFingerprint, b.EcoFingerprint);
            Assert.AreEqual(JsonUtility.ToJson(a.State.eco), JsonUtility.ToJson(b.State.eco));
            Assert.AreNotEqual(a.EcoFingerprint, c.EcoFingerprint);
        }

        [Test]
        public void SaveRoundTrip_KeepsTheLand_AndPlaysOnTheSame()
        {
            var sim = Fresh(true, 14 * 4); // 14 years of care: grass, bushes, the tree growing
            Assert.IsTrue(sim.State.eco.treeCell >= 0);
            string dir = Path.Combine(Path.GetTempPath(), "aqciv-eco-" + Guid.NewGuid().ToString("N"));
            try
            {
                string path = Path.Combine(dir, "box.json");
                SaveStore.Write(path, sim.State, new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
                Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var env));
                Assert.AreEqual(JsonUtility.ToJson(sim.State.eco), JsonUtility.ToJson(env.state.eco));
                Assert.IsTrue(env.state.eco.on);
                var loaded = new SimCore(sim.Config, env.state);
                Assert.AreEqual(sim.EcoFingerprint, loaded.EcoFingerprint);
                // a mist since the last step is in the save, and the next step sees what it would have seen
                sim.Mist(sim.EcoGround.campX + 1.2f, sim.EcoGround.campZ + 0.3f, 2f);
                var again = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
                var loaded2 = new SimCore(sim.Config, again);
                for (int s = 0; s < 24; s++) { sim.Advance(0.25); loaded2.Advance(0.25); }
                Assert.AreEqual(sim.EcoFingerprint, loaded2.EcoFingerprint, "the loaded box lives on exactly as the live one");
                Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(loaded2.State));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        static SimCore Fresh(bool care, int seasons)
        {
            var sim = EcoWorld(); var k = new Keeper(care);
            for (int s = 0; s < seasons; s++) { k.Play(sim); sim.Advance(0.25); }
            return sim;
        }

        [Test]
        public void CatchUp_EqualsLiveStepping()
        {
            var away = EcoWorld(11); var live = EcoWorld(11);
            away.Advance(2); live.Advance(2);
            away.Mist(away.EcoGround.campX + 0.5f, away.EcoGround.campZ, 2f); live.Mist(live.EcoGround.campX + 0.5f, live.EcoGround.campZ, 2f);
            var r = away.CatchUp(6);
            for (int i = 0; i < 60; i++) live.Advance(0.1);
            Assert.AreEqual(72, r.months, "6 h away = 72 months");
            Assert.AreEqual(away.State.month, live.State.month);
            Assert.AreEqual(away.EcoFingerprint, live.EcoFingerprint);
            Assert.AreEqual(JsonUtility.ToJson(away.State.eco), JsonUtility.ToJson(live.State.eco));
            // and a longer one, through the slowed tail
            var a2 = EcoWorld(3); var l2 = EcoWorld(3);
            var r2 = a2.CatchUp(20);
            Assert.Greater(r2.months, 100);
            l2.Advance(r2.simHours);
            Assert.AreEqual(a2.EcoFingerprint, l2.EcoFingerprint);
        }

        // ---------------------------------------------------------------- the arc
        [Test]
        public void TheArcInOrder_WithCare()
        {
            var r = Play(true, Seasons30);
            var sim = r.sim;
            float green = r.Year(EcoMilestones.FirstGreen), camp = r.CampYear, bush = r.Year(EcoMilestones.FirstBush), seed = r.Year(EcoMilestones.SeedUnlocked),
                planted = r.Year(EcoMilestones.TreePlanted), grown = r.Year(EcoMilestones.TreeGrown), water = r.Year(EcoMilestones.WaterSurfaced), village = r.VillageYear;
            Assert.GreaterOrEqual(green, 0f, "first green");
            Assert.Greater(camp, 0f, "camp gate");
            Assert.Greater(grown, 0f, "the Keeper's tree grew");
            Assert.Greater(water, 0f, "water surfaced");
            Assert.Greater(village, 0f, "village gate");
            Assert.LessOrEqual(green, camp, "green before the camp");
            Assert.LessOrEqual(camp, seed, "the seed comes after the camp");
            Assert.LessOrEqual(seed, planted);
            Assert.Less(planted, grown, "the tree grows after it is planted");
            Assert.Less(grown, water, "the water rises after the tree");
            Assert.LessOrEqual(water, village, "water before the village");
            Assert.LessOrEqual(r.Year(EcoMilestones.FirstRabbit), camp, "rabbits come with the camp");
            // the tree's stages in order
            float prev = planted;
            foreach (var k in EcoMilestones.TreeStages)
            {
                float y = r.Year(k);
                Assert.GreaterOrEqual(y, prev, k);
                prev = y;
            }
            Assert.IsTrue(sim.EcoTree.grown);
            Assert.GreaterOrEqual(sim.EcoGrownTrees, 8);
            Assert.GreaterOrEqual(sim.EcoPondCells, 20);
            // the gates are in the chronicle, in the people's voice
            Assert.AreEqual(1, sim.State.chronicle.Count(c => c.kind == "eco_camp"));
            Assert.AreEqual(1, sim.State.chronicle.Count(c => c.kind == "eco_village"));
            Assert.AreEqual(1, sim.State.chronicle.Count(c => c.kind == "eco_water"));
        }

        [Test]
        public void Timings_AreTheLabs_WithinAQuarter()
        {
            var care = Play(true, Seasons30);
            var none = Play(false, 34 * 4);
            TestContext.Out.WriteLine($"care: camp Y{care.CampYear:0.0} green Y{care.Year(EcoMilestones.FirstGreen):0.0} bush Y{care.Year(EcoMilestones.FirstBush):0.0} " +
                $"planted Y{care.Year(EcoMilestones.TreePlanted):0.0} grown Y{care.Year(EcoMilestones.TreeGrown):0.0} water Y{care.Year(EcoMilestones.WaterSurfaced):0.0} village Y{care.VillageYear:0.0}; " +
                $"none: camp Y{none.CampYear:0.0}; runs {care.ms:0} + {none.ms:0} ms");
            // The lab, seed 7 on the first terrain: care camp Y4.3, village Y25.3; no player camp Y25.3.
            Assert.That(care.CampYear, Is.InRange(4.3f * 0.75f, 4.3f * 1.25f), "care: camp");
            Assert.That(care.VillageYear, Is.InRange(25.3f * 0.75f, 25.3f * 1.25f), "care: village");
            Assert.That(none.CampYear, Is.InRange(25.3f * 0.75f, 25.3f * 1.25f), "no player: camp");
            // the issue's own ranges, too
            Assert.That(care.CampYear, Is.InRange(3f, 5.4f));
            Assert.That(care.VillageYear, Is.InRange(19f, 31.6f));
            Assert.That(none.CampYear, Is.InRange(18f, 31.6f));
            Assert.IsFalse(none.sim.State.eco.Reached(EcoMilestones.TreePlanted), "no player plants nothing");
        }

        [Test]
        public void ThePond_NeverShrinks_AndNothingDiesBack()
        {
            var r = Play(true, Seasons30);
            Assert.IsFalse(r.pondShrank, "an established pond stays");
            Assert.IsFalse(r.diedBack, "cover only moves forward (bushes may thin back to meadow, #385)");
            Assert.Greater(r.lastPond, 0);
        }

        // ---------------------------------------------------------------- the gates in the old economy
        [Test]
        public void TheVillageNeedsItsGate_AsWellAsThePopulationBuildingsAndYears()
        {
            var sim = EcoWorld();
            sim.DebugOpenCampGate();
            for (int i = 0; i < 8; i++) sim.DebugBuild(BuildingType.Farm);
            sim.State.population = 40;
            for (int y = 0; y < 14; y++) { sim.State.food = 300; sim.Advance(1); }
            Assert.GreaterOrEqual(sim.State.buildings.Count, sim.Config.villageAt.buildings);
            Assert.GreaterOrEqual(sim.YearsInEra, sim.Config.villageAt.years);
            Assert.IsFalse(sim.MeetsNextEraThreshold, "the pond and the wood are not there");
            Assert.IsFalse(sim.State.eraReady);
            sim.State.population = Math.Max(sim.State.population, sim.Config.villageAt.population);
            Assert.IsTrue(sim.DebugOpenVillageGate());
            Assert.IsTrue(sim.VillageGateOpen);
            Assert.IsTrue(sim.MeetsNextEraThreshold);
            sim.Advance(1.0 / 12);
            Assert.IsTrue(sim.State.eraReady, "now the village is ready");
        }

        [Test]
        public void TheDebugHooks_Work()
        {
            var sim = EcoWorld();
            var gates = new List<string>(); var keys = new List<string>();
            sim.EcoGateOpened += gates.Add; sim.EcoMilestone += keys.Add;
            Assert.AreEqual(6, sim.DebugSkipSeasons(2), "two seasons are six steps");
            Assert.AreEqual(6, sim.State.month, "two seasons are six months");
            Assert.AreEqual(2, sim.State.eco.season);
            sim.DebugSetHumidity(0.9f);
            Assert.AreEqual(0.9f, sim.EcoHumidity, 1e-6f);
            Assert.IsTrue(sim.EcoTooHumid);
            sim.DebugSetHumidity(0.05f);
            Assert.IsTrue(sim.EcoTooDry);
            Assert.IsTrue(sim.DebugTreeStage(3), "a tree where there was none");
            Assert.AreEqual(3, sim.EcoTree.stage);
            Assert.AreEqual(EcoCover.Young, sim.EcoCoverAt(sim.EcoTree.cell));
            sim.DebugTreeStage(4);
            Assert.IsTrue(sim.EcoTree.grown);
            Assert.AreEqual(EcoCover.Tree, sim.EcoCoverAt(sim.EcoTree.cell));
            Assert.Contains(EcoMilestones.TreeGrown, keys);
            float table = sim.EcoGround.main.bottom + 0.05f;
            sim.DebugSetTable(table);
            sim.DebugSkipSeasons(1);
            Assert.Greater(sim.EcoPondCells, 0, "the water table under the hollow's floor-plus surfaces a pond at the next season");
            Assert.Contains(EcoMilestones.WaterSurfaced, keys);
            Assert.IsTrue(sim.DebugOpenCampGate());
            Assert.IsFalse(sim.DebugOpenCampGate(), "once");
            Assert.IsTrue(sim.DebugOpenVillageGate());
            CollectionAssert.AreEqual(new[] { "camp", "village" }, gates);
            Assert.IsTrue(sim.State.chronicle.Any(c => c.kind == "eco_camp"));
            Assert.IsTrue(sim.State.chronicle.Any(c => c.kind == "eco_village"));
            Assert.IsTrue(sim.State.chronicle.Any(c => c.kind == "eco_water"));
            var at = sim.EcoAt(sim.EcoTree.x, sim.EcoTree.z);
            Assert.AreEqual(sim.EcoTree.cell, at.cell);
            Assert.AreEqual(EcoCover.Tree, at.cover);
            Assert.IsFalse(at.belowFloodLine);
        }

        // ---------------------------------------------------------------- the land the people build on
        [Test]
        public void TheFloodLine_IsNeverBuiltOn()
        {
            var r = Play(true, Seasons30);
            var sim = r.sim;
            Assert.Greater(sim.EcoPondCells, 0, "there is water to keep clear of");
            // fill the box: every kind of lot has to find room somewhere dry
            foreach (var t in new[] { BuildingType.Farm, BuildingType.Farm, BuildingType.Hut, BuildingType.Hut, BuildingType.Hut, BuildingType.Hut,
                BuildingType.Lumbermill, BuildingType.Farm, BuildingType.Hut, BuildingType.Hut })
                sim.DebugBuild(t);
            sim.Advance(2);
            Assert.Greater(sim.State.settle.lots.Count, 8);
            AssertNoLotInTheHollow(sim);
            // the pond is on the land's map too, and the woods follow the cover
            var map = sim.Settlement.Map;
            sim.Settlement.SyncEco(); // (a mill or a path may have cut a tree's wood flag since the season's sync)
            var eco = sim.State.eco;
            int ponds = 0, woods = 0;
            for (int c = 0; c < SiteMap.N; c++)
            {
                bool water = (EcoCover)eco.cover[c] == EcoCover.Water;
                Assert.AreEqual(water, map.pond[c], "pond cell " + c);
                if (water) ponds++;
                if ((EcoCover)eco.cover[c] == EcoCover.Tree && map.lot[c] == 0 && !map.Rock(c)) { woods++; Assert.IsTrue(map.Forest(c), "a grown tree is forest"); }
                if ((EcoCover)eco.cover[c] == EcoCover.Grass && map.lot[c] == 0) Assert.IsTrue(map.Has(c, SiteMap.FMeadow), "grass is meadow");
            }
            Assert.Greater(ponds, 0); Assert.Greater(woods, 0);
        }

        static void AssertNoLotInTheHollow(SimCore sim)
        {
            var land = sim.EcoGround;
            var map = sim.Settlement.Map;
            var cells = new List<int>();
            int checkedLots = 0;
            foreach (var lot in sim.State.settle.lots)
            {
                if (lot.open) continue;
                SiteMap.RectCells(lot.x, lot.z, lot.yaw, lot.w, lot.d, 0f, cells);
                foreach (int c in cells)
                {
                    Assert.IsFalse(land.noBuild[c], $"lot {lot.id} ({lot.kind}) stands on cell {c} below the flood line");
                    Assert.IsFalse(map.pond[c], $"lot {lot.id} stands in the water");
                    if (land.InBasin(c)) Assert.GreaterOrEqual(land.height[c], land.main.spill, "above the main hollow's spill level");
                }
                checkedLots++;
            }
            Assert.Greater(checkedLots, 0);
        }

        [Test]
        public void TheFileStaysQuick()
        {
            var care = Play(true, Seasons30);
            Assert.Less(care.ms, 8000, "30 years of care, whole sim included");
        }

        // ---------------------------------------------------------------- P7 and P8 together (the phone build)

        /// <summary>The phone build ships the dilemmas (P7) and the living land (P8) on together. A barren box with the dilemmas on:
        /// the Keeper mists by the camp each season and answers every question; dilemmas come once the camp stands, the gates open,
        /// and a save round-trips and catch-up equals live stepping.</summary>
        [Test]
        public void DilemmasAndTheLivingLandRunTogether()
        {
            SimCore Make() => EcoWorld(5, new SimConfig { dilemmas = true, dilemmaLab = true });
            var sim = Make();
            Assert.IsTrue(sim.EcologyOn && sim.BeforeCamp);
            var map = sim.Settlement.Map;
            int answered = 0;
            for (int m = 0; m < 30 * 12; m++)
            {
                if (m % 3 == 0) sim.Mist(map.greenX + 0.6f + (m % 7) * 0.08f, map.greenZ - 0.2f, 2f);
                sim.Advance(1.0 / 12);
                var d = sim.OpenDilemma;
                if (d != null && sim.AnswerDilemma(d.id, m % 2, DilemmaPath.Box)) answered++;
            }
            Assert.IsTrue(sim.CampGateOpen, "the camp gate opened under a misting Keeper");
            Assert.Greater(sim.State.dilemmas.Count, 0, "dilemmas come on a living land");
            Assert.Greater(answered, 0);
            var json = JsonUtility.ToJson(sim.State);
            Assert.AreEqual(json, JsonUtility.ToJson(JsonUtility.FromJson<SimState>(json)), "the save round-trips");

            var live = Make(); var away = Make();
            for (int k = 0; k < 2; k++) { live.Mist(map.greenX + 0.6f, map.greenZ - 0.2f, 2f); away.Mist(map.greenX + 0.6f, map.greenZ - 0.2f, 2f); }
            for (int m = 0; m < 6 * 12; m++) live.Advance(1.0 / 12);
            away.Advance(6);
            Assert.AreEqual(Fingerprint(live.State), Fingerprint(away.State), "a week away equals live stepping with both on");
        }

        static bool ClearOfGlass(int c) => SiteMap.I(c) >= 3 && SiteMap.I(c) < SiteMap.NX - 3 && SiteMap.J(c) >= 3 && SiteMap.J(c) < SiteMap.NZ - 3;

        // #378 (playtest #342): a tree planted in the last cells by the glass poked through it.
        [Test]
        public void TheKeepersTree_IsNeverPlantedOnTheBedsEdge_ItSnapsInToTheNearestGoodCell()
        {
            var sim = EcoWorld();
            sim.DebugOpenCampGate();
            sim.State.eco.milestoneKeys.Add(EcoMilestones.SeedUnlocked); sim.State.eco.milestoneMonths.Add(0);
            var land = sim.EcoGround;
            int m = EcoModel.TreeEdgeMargin;
            // find an edge cell on each wall that is dry ground once moved in (not the hollow)
            int tried = 0;
            for (int w = 0; w < 4; w++)
            {
                for (int k = 6; k < 30 && tried <= w; k++)
                {
                    int i = w == 0 ? 0 : w == 1 ? SiteMap.NX - 1 : k, j = w == 2 ? 0 : w == 3 ? SiteMap.NZ - 1 : k;
                    int edge = SiteMap.Index(i, j);
                    int snapped = sim.TreeCellFor(SiteMap.CXc(edge), SiteMap.CZc(edge));
                    if (snapped < 0) continue; // the hollow reaches this stretch: refused, also fine
                    tried++;
                    Assert.IsTrue(ClearOfGlass(snapped), "edge cell " + edge + " snapped to a cell inside the bed");
                    Assert.IsFalse(land.InBasin(snapped));
                    Assert.LessOrEqual(Math.Abs(SiteMap.I(snapped) - i), m + 2);
                }
            }
            Assert.Greater(tried, 0, "at least one edge stretch is dry");
            // an interior dry cell is unchanged
            int inner = -1;
            for (int c = 0; c < EcoLand.N && inner < 0; c++) if (EcoModel.InTreeBed(c) && !land.InBasin(c) && sim.State.eco.cover[c] != (byte)EcoCover.Water) inner = c;
            Assert.AreEqual(inner, sim.TreeCellFor(SiteMap.CXc(inner), SiteMap.CZc(inner)), "an inside cell is never moved");
            // and planting on the very corner lands inside the bed
            int corner = SiteMap.Index(0, 0);
            if (sim.TreeCellFor(SiteMap.CXc(corner), SiteMap.CZc(corner)) >= 0)
            {
                Assert.IsTrue(sim.PlantTree(SiteMap.CXc(corner), SiteMap.CZc(corner)));
                Assert.IsTrue(ClearOfGlass(sim.State.eco.treeCell), "the tree stands clear of the glass");
            }
        }
    }
}
