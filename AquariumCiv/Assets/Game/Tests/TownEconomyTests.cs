using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The town's economy builds a workshop, a guildhall and shops by itself (#114), at sensible years, without
    /// crowding out the shaping answers or the peak.</summary>
    public class TownEconomyTests
    {
        static SimCore Run(int seed, int years)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < years * SimConfig.MonthsPerYear; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            return sim;
        }

        [Test]
        public void ATownBuildsAWorkshopAGuildhallAndShopsWithoutAnyShaping()
        {
            int withAll = 0, towns = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = Run(seed, 120);
                var s = sim.State;
                if (s.era != Era.Town) continue;
                towns++;
                Assert.AreEqual(0, s.nudges.Count, "no shaping was made");
                Assert.LessOrEqual(sim.Count(BuildingType.Guildhall), 1, $"seed {seed}: one guildhall");
                Assert.LessOrEqual(sim.Count(BuildingType.Workshop), 3, $"seed {seed}: workshops capped");
                Assert.LessOrEqual(sim.Count(BuildingType.Shop), 3, $"seed {seed}: shops capped");
                Assert.LessOrEqual(s.buildings.Count, sim.Config.buildingSlots);
                long townStart = s.eraStartMonth;
                foreach (var b in s.buildings.Where(b => BuildingType.EraOf(b.type) == Era.Town))
                    Assert.GreaterOrEqual(b.foundedMonth, townStart, $"seed {seed}: {b.type} only in the town era");
                if (sim.Count(BuildingType.Guildhall) == 1 && sim.Count(BuildingType.Workshop) >= 1 && sim.Count(BuildingType.Shop) >= 1) withAll++;
                TestContext.WriteLine($"seed {seed}: guildhall {sim.Count(BuildingType.Guildhall)}, workshop {sim.Count(BuildingType.Workshop)}, shop {sim.Count(BuildingType.Shop)}, " +
                                      $"houses {sim.Count(BuildingType.Hut)}, pop {s.population}, market {sim.Count(BuildingType.Market)}, total {s.buildings.Count}, " +
                                      string.Join(" ", s.buildings.Where(b => BuildingType.EraOf(b.type) == Era.Town).Select(b => $"{b.type}@y{SimCore.YearNo(b.foundedMonth)}")));
            }
            Assert.GreaterOrEqual(towns, 6);
            Assert.GreaterOrEqual(withAll, towns / 2, "most towns have all three");
        }

        [Test]
        public void MostTownsBuildABakeryAndASimBakerWorksIt()
        {
            int towns = 0, withBakery = 0, withBaker = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = Run(seed, 120);
                if (sim.State.era != Era.Town) continue;
                towns++;
                Assert.LessOrEqual(sim.Count(BuildingType.Bakery), 1, $"seed {seed}: one bakery");
                if (sim.Count(BuildingType.Bakery) == 0) continue;
                withBakery++;
                if (sim.State.notables.Any(n => n.alive && n.job == JobType.Baker)) withBaker++;
                var lot = sim.Settlement.Lots.FirstOrDefault(l => l.buildingId == sim.State.buildings.First(b => b.type == BuildingType.Bakery).id);
                if (lot != null) Assert.AreEqual(LotKind.Bakery, lot.kind, $"seed {seed}");
            }
            Assert.GreaterOrEqual(towns, 6);
            Assert.GreaterOrEqual(withBakery, towns * 3 / 4, "most towns have a bakery");
            Assert.GreaterOrEqual(withBaker, withBakery / 2, "and a baker in it");
        }

        [Test]
        public void TheBakeryCarriesItsOwnFirstBuildingLine()
        {
            StringAssert.Contains("oven", NameData.FirstBuildingLine(BuildingType.Bakery, null, Era.Town));
            Assert.AreEqual(Era.Town, BuildingType.EraOf(BuildingType.Bakery));
        }

        [Test]
        public void TheTownBuildingsGetLotsOfTheRightKind()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var sim = Run(seed, 120);
                var lots = sim.Settlement.Lots;
                foreach (var b in sim.State.buildings.Where(b => b.type == BuildingType.Shop || b.type == BuildingType.Guildhall || b.type == BuildingType.Workshop))
                {
                    var lot = lots.FirstOrDefault(l => l.buildingId == b.id);
                    if (lot == null) continue; // waiting for a lot (a crowded green) is allowed; the rest must be the right kind
                    Assert.AreEqual(Encyclopedia.FromType(b.type), lot.kind, $"seed {seed}: {b.type}");
                }
            }
        }

        [Test]
        public void EveryBuildingTypeHasAKindOfItsOwn()
        {
            var types = typeof(BuildingType).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()).ToList();
            Assert.GreaterOrEqual(types.Count, 12);
            var seen = new HashSet<LotKind>();
            foreach (var t in types)
            {
                var k = Encyclopedia.FromType(t);
                Assert.IsTrue(seen.Add(k), $"{t} shares its lot kind {k} with another type");
            }
        }

        [Test]
        public void TheGuildmasterCanBeElectedAtTheGuildhall()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = Run(seed, 120);
                if (sim.Count(BuildingType.Guildhall) == 0) continue;
                var seat = sim.State.roles.FirstOrDefault(r => r.role == RoleType.Guildmaster);
                Assert.IsNotNull(seat, $"seed {seed}");
                Assert.GreaterOrEqual(seat.holderId, 0, $"seed {seed}: the guildmaster seat is filled");
                Assert.AreEqual(BuildingType.Guildhall, sim.SeatBuilding(RoleType.Guildmaster)?.type, $"seed {seed}: it sits in the guildhall");
                return;
            }
            Assert.Fail("no seed built a guildhall");
        }
    }
}
