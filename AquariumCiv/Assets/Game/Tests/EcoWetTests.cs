using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// P8 polish #266 (sim half) and #271: every mist shows a little green at once, the rest of its wet area greens over the
    /// following months, the wet area dries, soaked low ground holds puddles that dry, and misted patches creep on their own.
    /// </summary>
    public class EcoWetTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static SimCore OffWorld() { var cfg = new SimConfig(); return new SimCore(cfg, SimCore.NewWorld(3, cfg)); }

        static void Months(SimCore sim, int n) { for (int i = 0; i < n; i++) sim.Advance(1.0 / 12); }

        static int Greens(SimCore sim) { int n = 0; for (int c = 0; c < EcoLand.N; c++) if (sim.EcoCoverAt(c) != EcoCover.Bare) n++; return n; }

        static float WetSum(SimCore s) { float m = 0; for (int i = 0; i < EcoLand.N; i++) m += s.EcoWetAt(i); return m; }

        static (float x, float z) CampSpot(SimCore sim, float dx = 0.9f, float dz = 0f) => (sim.EcoGround.campX + dx, sim.EcoGround.campZ + dz);

        // ------------------------------------------------------------ a little green at once, every time
        [Test]
        public void EveryMist_ShowsALittleGreenAtOnce_NotJustTheFirst()
        {
            var sim = World();
            var cfg = sim.Config.eco;
            var spots = new[] { (0.9f, 0f), (-1.2f, 0.8f), (1.6f, -0.9f), (-0.4f, -1.3f), (2.2f, 0.6f) };
            int k = 0;
            foreach (var (dx, dz) in spots)
            {
                var (x, z) = CampSpot(sim, dx, dz);
                int c = SiteMap.CellAt(x, z);
                if (c < 0 || sim.EcoCoverAt(c) == EcoCover.Water) continue;
                float seconds = 1f + k++;   // short holds and long ones
                int before = Greens(sim);
                var cells = sim.Mist(x, z, seconds);
                Assert.GreaterOrEqual(cells.Count, cfg.mistMinGreen, $"mist {k}: a few cells green at once");
                Assert.LessOrEqual(cells.Count, 30, $"mist {k}: only a little, not the wet area");
                Assert.AreEqual(before + cells.Count, Greens(sim));
                int wet = 0; for (int i = 0; i < EcoLand.N; i++) if (sim.EcoWetAt(i) > 0.05f) wet++;
                Assert.Greater(wet, cells.Count * 3, $"mist {k}: the wet area is much bigger than the green");
            }
            Assert.Greater(k, 3);
        }

        [Test]
        public void AMistOnGreenGround_NeedsNoMoreGreen()
        {
            var sim = World();
            var (x, z) = CampSpot(sim);
            sim.Mist(x, z, 2f);
            Assert.AreEqual(0, sim.Mist(x, z, 2f).Count, "the green is already there");
        }

        [Test]
        public void TheRadiusIsAboutSevenTenths_AndGrowsWithTheHold()
        {
            var sim = World();
            float s = sim.EcoMistRadius(0.5f), m = sim.EcoMistRadius(2f), l = sim.EcoMistRadius(4f), cap = sim.EcoMistRadius(400f);
            Assert.That(s, Is.InRange(0.68f, 0.8f), "a short mist: about 0.7");
            Assert.Less(s, m); Assert.Less(m, l);
            Assert.AreEqual(l, cap, 1e-5f, "a long press is capped");
            Assert.That(l, Is.InRange(0.9f, 1.1f));
            Assert.AreEqual(0f, OffWorld().EcoMistRadius(2f), "off: nothing");
            int Wet(float seconds) { var w = World(); var (x, z) = CampSpot(w); w.Mist(x, z, seconds); int n = 0; for (int i = 0; i < EcoLand.N; i++) if (w.EcoWetAt(i) > 0.02f) n++; return n; }
            Assert.Less(Wet(0.5f), Wet(4f), "held longer, wetter and wider");
        }

        // ------------------------------------------------------------ the wet area greens over time
        [Test]
        public void TheWetArea_GreensToGrassOverTime()
        {
            var sim = World();
            var (x, z) = CampSpot(sim);
            sim.Mist(x, z, 3f);
            float r = sim.EcoMistRadius(3f);
            var area = new List<int>();
            for (int c = 0; c < EcoLand.N; c++)
                if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) <= r * 0.4f && sim.EcoCoverAt(c) != EcoCover.Water) area.Add(c);
            int G(SimCore s) => area.Count(c => s.EcoCoverAt(c) != EcoCover.Bare);
            int g0 = G(sim);
            Assert.Less(g0, area.Count * 0.5f, "at once: a little, not all");
            Assert.IsTrue(area.Any(c => sim.EcoCoverAt(c) == EcoCover.Bare && sim.EcoSproutAt(c) > 0.05f), "first shoots show on bare wet ground");
            Months(sim, 6);
            int g6 = G(sim);
            Assert.Greater(g6, g0, "six months on, more of it is green");
            Months(sim, 30);
            int g36 = G(sim);
            Assert.GreaterOrEqual(g36, g6);
            TestContext.Out.WriteLine($"wet area {area.Count} cells: at once {g0}, +6 mo {g6}, +3 y {g36}");
            Assert.GreaterOrEqual(g36, area.Count * 0.85f, "in the end the wet area is grass");
        }

        [Test]
        public void ShootsComeBeforeGrass()
        {
            var sim = World();
            var (x, z) = CampSpot(sim);
            sim.Mist(x, z, 2f);
            int shoots = 0; for (int c = 0; c < EcoLand.N; c++) if (sim.EcoCoverAt(c) == EcoCover.Bare && sim.EcoSproutAt(c) > 0.05f) shoots++;
            Assert.Greater(shoots, 2);
            for (int c = 0; c < EcoLand.N; c++) if (sim.EcoCoverAt(c) != EcoCover.Bare) Assert.AreEqual(0f, sim.EcoSproutAt(c), "no shoots on covered ground");
        }

        // ------------------------------------------------------------ wetness dries
        [Test]
        public void Wetness_DriesOverTime()
        {
            var sim = World();
            var (x, z) = CampSpot(sim);
            sim.Mist(x, z, 2f);
            int c0 = SiteMap.CellAt(x, z);
            Assert.Greater(sim.EcoWetAt(c0), 0.8f, "wet at the middle");
            float s0 = WetSum(sim);
            Months(sim, 2);
            Assert.Less(WetSum(sim), s0 * 0.7f, "drying");
            Months(sim, 10);
            Assert.Less(WetSum(sim), s0 * 0.1f, "nearly dry within a game year");
            Assert.Greater(sim.EcoAt(x, z).moisture, 0f);
            Assert.AreEqual(0f, OffWorld().EcoWetAt(0), "off: nothing");
        }

        [Test]
        public void AHumidAirDriesTheWetAreaMoreSlowly()
        {
            SimCore Mk(float humid) { var s = World(); var (x, z) = CampSpot(s); s.Mist(x, z, 2f); s.DebugSetHumidity(humid); return s; }
            var dry = Mk(0.2f); var humid = Mk(0.9f);
            Months(dry, 1); Months(humid, 1);
            Assert.Greater(WetSum(humid), WetSum(dry));
        }

        // ------------------------------------------------------------ puddles
        [Test]
        public void ABigMist_LeavesPuddlesOnLowSoakedGround_ThatDry()
        {
            var sim = World();
            var cells = new List<int>();
            var (x, z) = CampSpot(sim, 0.5f, 0.3f);
            sim.EcoPuddleCells(cells);
            Assert.AreEqual(0, cells.Count, "none to begin with");
            Assert.IsFalse(sim.EcoHasPuddles);
            sim.Mist(x, z, 0.5f);
            sim.EcoPuddleCells(cells);
            Assert.AreEqual(0, cells.Count, "a short mist only wets");
            sim.DebugMistBig(x, z);
            sim.EcoPuddleCells(cells);
            Assert.Greater(cells.Count, 0, "a long mist soaks a spot: puddles");
            foreach (int c in cells) Assert.LessOrEqual(sim.EcoGround.slope[c], sim.Config.eco.puddleMaxSlope, "puddles stand on flat ground");
            Assert.GreaterOrEqual(sim.EcoPuddleAt(cells[0]), sim.EcoPuddleAt(cells[cells.Count - 1]), "strongest first");
            TestContext.Out.WriteLine($"big mist: {cells.Count} puddle cells, strongest {sim.EcoPuddleAt(cells[0]):0.00}, drinkable: {sim.EcoHasPuddles}");
            Assert.IsTrue(sim.EcoHasPuddles, "strong enough to drink from: a water source while there is no pond");
            Assert.GreaterOrEqual(sim.EcoNearestPuddle(x, z, 2f), 0);
            float s0 = cells.Sum(c => sim.EcoPuddleAt(c));
            Months(sim, 1);
            sim.EcoPuddleCells(cells);
            Assert.Less(cells.Sum(c => sim.EcoPuddleAt(c)), s0, "drying");
            Months(sim, 8);
            sim.EcoPuddleCells(cells);
            Assert.AreEqual(0, cells.Count, "gone within a game year");
            Assert.AreEqual(-1, sim.EcoNearestPuddle(x, z, 5f));
        }

        [Test]
        public void PuddlesNow_Debug_MakesPuddles_AndTheyCanBeDrunk()
        {
            var sim = World();
            var (x, z) = CampSpot(sim);
            int made = sim.DebugPuddlesNow(x, z);
            Assert.Greater(made, 3);
            Assert.IsTrue(sim.EcoHasPuddles);
            int c = sim.EcoNearestPuddle(x, z, 3f);
            Assert.GreaterOrEqual(c, 0);
            float before = sim.EcoPuddleAt(c);
            Assert.AreEqual(0.1f, sim.EcoDrawPuddle(c, 0.1f), 1e-5f);
            Assert.AreEqual(before - 0.1f, sim.EcoPuddleAt(c), 1e-5f);
            Assert.AreEqual(0, OffWorld().DebugPuddlesNow(0, 0));
        }

        [Test]
        public void TheNewLayers_RoundTripTheSave_AndAnOldSaveGetsThemEmpty()
        {
            var sim = World();
            var (x, z) = CampSpot(sim);
            sim.DebugMistBig(x, z);
            var back = UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(sim.State));
            var again = new SimCore(sim.Config, back);
            Assert.AreEqual(WetSum(sim), WetSum(again), 1e-4f);
            Assert.AreEqual(sim.EcoFingerprint, again.EcoFingerprint);
            back.eco.wet = null; back.eco.puddle = null; back.eco.sprout = null;   // a save from before
            var old = new SimCore(sim.Config, back);
            Assert.AreEqual(0f, WetSum(old));
            Months(old, 3);                                                         // and it plays on
            Assert.IsTrue(old.EcologyOn);
        }

        // ------------------------------------------------------------ spreading on its own (#271)
        [Test]
        public void AMistedPatch_CreepsOutwardBetweenVisits()
        {
            SimCore Mk(bool creep)
            {
                var s = World();
                if (!creep) { s.Config.eco.creepChance = 0f; s.Config.eco.dewSprout = 0f; }
                var (x, z) = CampSpot(s); s.Mist(x, z, 2f);
                Months(s, 4);           // the wet area has dried
                return s;
            }
            var with = Mk(true); var without = Mk(false);
            int w0 = Greens(with), o0 = Greens(without);
            Months(with, 36); Months(without, 36);
            TestContext.Out.WriteLine($"3 years unvisited: with spreading {w0}->{Greens(with)}, without {o0}->{Greens(without)}");
            Assert.Greater(Greens(with), w0, "the patch crept on, with nobody there");
            Assert.Greater(Greens(with), Greens(without), "the creep is what spreads it");
            Assert.Less(Greens(with), EcoLand.N * 0.5f, "slowly: no meadow from one mist in three years");
        }

        [Test]
        public void BareGround_GetsTheOddSproutAfterDewAndShowers()
        {
            var sim = World(7);
            Months(sim, 12 * 6);
            TestContext.Out.WriteLine($"no mist, 6 years: {Greens(sim)} cells green");
            Assert.Greater(Greens(sim), 0, "no mist ever, and still an odd sprout");
            Assert.That(sim.EcoMilestoneMonth(EcoMilestones.FirstGreen), Is.GreaterThanOrEqualTo(0));
            Assert.Less(Greens(sim), EcoLand.N * 0.1f, "but only odd ones");
        }

        [Test]
        public void TheMister_StaysTheFastWay()
        {
            var misted = World(); var alone = World();
            var k = new[] { (0.9f, 0f), (-1.0f, 0.7f), (0.2f, -1.2f) };
            foreach (var (dx, dz) in k) { var (x, z) = CampSpot(misted, dx, dz); misted.Mist(x, z, 3f); }
            Months(misted, 24); Months(alone, 24);
            Assert.Greater(Greens(misted), Greens(alone) * 3, "two years: the mister is much faster");
        }

        // ------------------------------------------------------------ pacing, with and without the player
        [Test]
        public void TheGate_WithCare_StaysNearTheLabs_AndWithoutThePlayerMuchLater()
        {
            var care = Pace(true, 8 * 4); var none = Pace(false, 34 * 4);
            TestContext.Out.WriteLine($"gate: care Y{care:0.0}, none Y{none:0.0}");
            Assert.That(care, Is.InRange(3f, 5.4f), "with care: the lab's 3-4 years");
            Assert.AreEqual(-1f, none, "without the player: no rabbits are ever placed, so no camp (#419, placed-life spec §2.2)");
        }

        /// <summary>A careful keeper (three mists in each of the first two seasons, then every hour) until the camp gate opens; game years, or -1.</summary>
        static float Pace(bool care, int seasons)
        {
            var sim = World();
            for (int s = 0; s < seasons && !sim.CampGateOpen; s++)
            {
                if (care && (s <= 1 || s % 4 == 0)) EcoTests.PlaceReadyJars(sim); // #417-#419: the thicket, then the rabbits
                if (care && (s <= 1 || s % 4 == 0))
                    for (int k = 0; k < 3; k++)
                    {
                        float a = Terrain.Hash3(91, s * 8 + k, 1) * MathF.PI * 2f, rr = MathF.Sqrt(Terrain.Hash3(92, s * 8 + k, 2)) * MathF.Min(2.8f, 0.7f + (s / 4f + k) * 0.3f);
                        sim.Mist(sim.EcoGround.campX + MathF.Cos(a) * rr, sim.EcoGround.campZ + MathF.Sin(a) * rr, 2f);
                    }
                sim.Advance(0.25);
            }
            return sim.State.eco.campGateMonth < 0 ? -1f : sim.State.eco.campGateMonth / 12f;
        }

        [Test]
        public void TheFirstSession_AndTheFirstDays_ArePacedByWetGroundNotInstantPatches()
        {
            var sim = World();
            var (x, z) = CampSpot(sim);
            int at0 = sim.Mist(x, z, 2f).Count;
            Assert.That(at0, Is.InRange(3, 30), "the first mist: a little green at once");
            Assert.IsTrue(sim.BeforeCamp);
            Months(sim, 6);                                  // half an hour at 1 h = 1 game year
            Assert.IsTrue(sim.BeforeCamp, "no camp in the first session");
            Assert.Greater(Greens(sim), at0, "but the wet ground is greening");
            for (int d = 0; d < 3; d++) { var (mx, mz) = CampSpot(sim, 0.9f - d * 0.8f, 0.4f * d); sim.Mist(mx, mz, 2f); Months(sim, 24); }
            TestContext.Out.WriteLine($"first mist {at0} cells at once; after 3 more mists and 6 years: green {Greens(sim)} of {EcoLand.N}, camp open: {sim.CampGateOpen}");
            Assert.Greater(Greens(sim), EcoLand.N * 0.04f, "a patch of meadow after a week of tending");
        }
    }
}
