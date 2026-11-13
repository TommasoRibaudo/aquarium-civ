using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#618: winter on the pond. It freezes in the calendar's winter; people go on the ice (holes, sliding) only on a pond of
    /// <see cref="EcoTuning.iceActivityCells"/> cells or more (the user's rule); a small frozen pond is not fished; the wild ducks are away
    /// for the winter and nothing takes them; their count is kept.</summary>
    public class PondIceTests
    {
        [TearDown] public void Reset() => PondIce.Force = null;

        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>A w x h block of water away from the camp; returns its corner cell.</summary>
        static int Pond(SimCore sim, int w, int h)
        {
            var g = sim.EcoGround;
            for (int j = 3; j + h < SiteMap.NZ - 3; j++)
                for (int i = 3; i + w < SiteMap.NX - 3; i++)
                {
                    int c0 = j * SiteMap.NX + i;
                    if (Terrain.Dist(SiteMap.CXc(c0), SiteMap.CZc(c0), g.campX, g.campZ) < 1.2f) continue;
                    for (int jj = j - 1; jj <= j + h; jj++) for (int ii = i - 1; ii <= i + w; ii++) sim.State.eco.cover[jj * SiteMap.NX + ii] = (byte)EcoCover.Bare;
                    for (int jj = j; jj < j + h; jj++) for (int ii = i; ii < i + w; ii++) sim.State.eco.cover[jj * SiteMap.NX + ii] = (byte)EcoCover.Water;
                    return c0;
                }
            Assert.Fail("no room for a pond");
            return -1;
        }

        static long WinterDay() { for (long d = 0; d < 24; d++) if (PondIce.Winter(d) && PondIce.Winter(d + 1)) return d; return -1; }
        static long SummerDay() { for (long d = 0; d < 24; d++) if (!PondIce.Winter(d) && !PondIce.Winter(d + 3) && !PondIce.Winter(d - 3)) return d; return -1; }

        [Test]
        public void ThePondFreezesInWinter_OnlyWithWater_AndTheCheatOverrides()
        {
            var sim = World();
            long w = WinterDay(), s = SummerDay();
            Assert.IsFalse(PondIce.Frozen(sim, w), "no pond: nothing freezes");
            Pond(sim, 6, 6);
            Assert.IsTrue(PondIce.Frozen(sim, w));
            Assert.IsFalse(PondIce.Frozen(sim, s));
            Assert.AreEqual(3, Enumerable.Range(0, 12).Count(m => PondIce.WinterMonth(m)), "three winter months a year");
            PondIce.Force = true; Assert.IsTrue(PondIce.Frozen(sim, s));
            PondIce.Force = false; Assert.IsFalse(PondIce.Frozen(sim, w));
        }

        [Test]
        public void PeopleGoOnTheIce_OnlyOnAPondBigEnough()
        {
            var small = World();
            Pond(small, 4, 4); // 16 cells, under the 20
            long w = WinterDay();
            Assert.Less(small.EcoPondCells, small.Config.eco.iceActivityCells);
            Assert.IsTrue(PondIce.Frozen(small, w), "a small pond still ices over (a look)");
            Assert.IsFalse(PondIce.OnIce(small, w), "but nobody goes on it");
            Assert.IsTrue(PondIce.FishingStopped(small, w), "and nobody fishes it in winter");
            Assert.IsFalse(PondIce.FishingSpot(small, 0, 0, 0, out _, out _, out _, out _) && PondIce.OnIce(small, w));

            var big = World();
            Pond(big, 7, 6);
            Assert.GreaterOrEqual(big.EcoPondCells, big.Config.eco.iceActivityCells);
            Assert.IsTrue(PondIce.OnIce(big, w));
            Assert.IsFalse(PondIce.FishingStopped(big, w), "a big frozen pond is fished through the ice");
            Assert.IsFalse(PondIce.OnIce(big, SummerDay()), "no ice in summer");
        }

        [Test]
        public void IceFishingSpots_AreOffTheShore_AndApart()
        {
            var sim = World();
            int c0 = Pond(sim, 8, 7);
            var holes = new List<(float x, float z)>();
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.IsTrue(PondIce.FishingSpot(sim, 0f, 0f, slot, out float sx, out float sz, out float hx, out float hz), "slot " + slot);
                Assert.IsTrue(PondIce.OnInner(sim, hx, hz), "the hole is off the shore");
                Assert.IsTrue(PondIce.OnInner(sim, sx, sz), "so is the fisher");
                Assert.AreEqual(PondIce.HoleReach, Terrain.Dist(sx, sz, hx, hz), 1e-3f);
                foreach (var h in holes) Assert.GreaterOrEqual(Terrain.Dist(h.x, h.z, hx, hz), PondIce.HoleGap - 1e-4f, "holes apart");
                holes.Add((hx, hz));
            }
        }

        [Test]
        public void TheFishersAndTheSlide_AreTowardTheMiddleOfThePond()
        {
            var sim = World();
            Pond(sim, 15, 11);
            var steps = PondIce.ShoreSteps(sim, out int max);
            Assert.GreaterOrEqual(max, 5);
            int need = (int)System.Math.Ceiling(max * PondIce.MiddleShare);
            var g = sim.EcoGround;
            for (int slot = 0; slot < 2; slot++)
            {
                Assert.IsTrue(PondIce.FishingSpot(sim, g.campX, g.campZ, slot, out _, out _, out float hx, out float hz));
                Assert.GreaterOrEqual(steps[SiteMap.CellAt(hx, hz)], need, "hole " + slot + " is in the middle, not by the bank");
            }
            Assert.IsTrue(PondIce.SlideLine(sim, g.campX, g.campZ, out float ax, out float az, out float bx, out float bz));
            Assert.GreaterOrEqual(steps[SiteMap.CellAt((ax + bx) * 0.5f, (az + bz) * 0.5f)], 2, "the slide is out on the pond, not along its edge");
        }

        [Test]
        public void TheSlide_StaysOnInnerIce_AndNeedsRoom()
        {
            var sim = World();
            Pond(sim, 15, 11);
            Assert.IsTrue(PondIce.SlideLine(sim, 0f, 0f, out float ax, out float az, out float bx, out float bz));
            float len = Terrain.Dist(ax, az, bx, bz);
            Assert.GreaterOrEqual(len, PondIce.SlideMin);
            for (int k = 0; k <= 10; k++) Assert.IsTrue(PondIce.OnInner(sim, ax + (bx - ax) * k / 10f, az + (bz - az) * k / 10f), "point " + k);
            float dx = (bx - ax) / len, dz = (bz - az) / len;
            Assert.IsTrue(PondIce.OnInner(sim, ax - dx * PondIce.RunUpRoom, az - dz * PondIce.RunUpRoom), "room for the queue and the run-up on the ice");
            var g = sim.EcoGround;
            for (int slot = 0; slot < 5; slot++)
                if (PondIce.FishingSpot(sim, g.campX, g.campZ, slot, out _, out _, out float hx, out float hz))
                    for (int k = 0; k <= 10; k++) Assert.GreaterOrEqual(Terrain.Dist(hx, hz, ax + (bx - ax) * k / 10f, az + (bz - az) * k / 10f), PondIce.SlideFromHoles - 1e-3f, "the slide keeps off the holes");

            var tiny = World();
            Pond(tiny, 3, 3);
            Assert.IsFalse(PondIce.SlideLine(tiny, 0f, 0f, out _, out _, out _, out _), "a 3x3 pond has one inner cell: no slide");
        }

        [Test]
        public void TheWildDucksAreAwayInWinter_NothingTakesThem_TheirCountIsKept()
        {
            var sim = World();
            Pond(sim, 7, 6);
            sim.State.eco.ducks = 6; sim.State.eco.duckEggs = 3;
            var m = new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco);
            long w = WinterDay(), s = SummerDay();
            Assert.IsTrue(PondIce.DucksAway(sim, w));
            Assert.IsFalse(PondIce.DucksAway(sim, s));
            Assert.IsFalse(m.MayTake(LifeSpecies.Ducks, LifeTaker.Wolves, w), "the wolves find none");
            Assert.IsFalse(m.MayTake(LifeSpecies.Ducks, LifeTaker.EggGatherers, w), "no eggs taken");
            Assert.IsFalse(m.TryTake(LifeSpecies.Ducks, LifeTaker.Wolves, w, out _));
            Assert.AreEqual(6, sim.State.eco.ducks, "the flock is kept while away");
            Assert.IsTrue(m.MayTake(LifeSpecies.Ducks, LifeTaker.Wolves, s), "back in the other seasons");
        }

        [Test]
        public void ASmallFrozenPond_IsNotFished_ABigOneIs()
        {
            foreach (bool big in new[] { false, true })
            {
                var sim = World(11);
                Pond(sim, big ? 7 : 4, big ? 6 : 4);
                var j = sim.State.eco.jars.First(x => x.species == nameof(LifeSpecies.Fish));
                j.state = (int)JarState.Placed; j.placings = 1; j.placedMonth = sim.State.month;
                sim.State.eco.fish = 6;
                foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge)) sim.DebugSetJob(n, JobType.Fisher);
                var planner = LifePlanner.Wired(sim);
                var day = new List<PersonActivity>();
                int fishW = 0, fishS = 0;
                long w = WinterDay(), s = SummerDay();
                foreach (var who in planner.People)
                {
                    planner.DaySchedule(who, w, day); fishW += day.Count(a => a.kind == ActivityKind.Fish);
                    planner.DaySchedule(who, s, day); fishS += day.Count(a => a.kind == ActivityKind.Fish);
                }
                Assert.Greater(fishS, 0, "fishers fish in summer");
                if (big) Assert.Greater(fishW, 0, "a big frozen pond is fished through the ice");
                else Assert.AreEqual(0, fishW, "nobody fishes a small frozen pond");
            }
        }
    }
}
