using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Offline slow-down (game definition v0.2): 8 h at full speed, then a quarter; mess still runs on real hours.</summary>
    public class OfflineCatchUpTests
    {
        [TestCase(0, 0)]
        [TestCase(-3, 0)]
        [TestCase(5, 5)]
        [TestCase(8, 8)]
        [TestCase(48, 18)]
        [TestCase(24 * 7, 48)]
        public void RealHoursMapToSimHours(double real, double sim)
        {
            Assert.AreEqual(sim, new SimConfig().OfflineSimHours(real), 1e-9);
        }

        [Test]
        public void ShortAbsenceIsTheSameAsAdvance()
        {
            var a = NewSim(); var b = NewSim();
            var r = a.CatchUp(6);
            b.Advance(6);
            Assert.IsFalse(r.Slowed);
            Assert.AreEqual(72, r.months);
            Assert.AreEqual(JsonUtility.ToJson(b.State), JsonUtility.ToJson(a.State));
        }

        [Test]
        public void TwoDaysAwayLivesEighteenYears()
        {
            var sim = NewSim();
            var r = sim.CatchUp(48);
            Assert.IsTrue(r.Slowed);
            Assert.AreEqual(18, r.simHours, 1e-9);
            Assert.AreEqual(18 * 12, r.months);
            Assert.AreEqual(18 * 12, sim.State.month);
            Assert.AreEqual(18, sim.State.simHours, 1e-9);
        }

        [Test]
        public void MessAfterTwoDaysAwayReachesTheCapAnyway()
        {
            var slowed = NewSim(); var full = NewSim();
            slowed.CatchUp(48);
            full.Advance(48);
            Assert.AreEqual(1f, full.State.grime, 1e-5f);
            Assert.AreEqual(full.State.grime, slowed.State.grime, 1e-5f);
            Assert.AreEqual(full.State.overgrowth, slowed.State.overgrowth, 1e-5f);
            Assert.Greater(slowed.State.trash.Count, 0);
            Assert.LessOrEqual(slowed.State.trash.Count, slowed.Config.trashMax);
        }

        [Test]
        public void MessIsCappedAfterAMonthAway()
        {
            var sim = NewSim();
            sim.CatchUp(30 * 24);
            Assert.AreEqual(1f, sim.State.grime);
            Assert.AreEqual(1f, sim.State.overgrowth);
            Assert.LessOrEqual(sim.State.trash.Count, sim.Config.trashMax);
        }

        [Test]
        public void HalfADayAwayTopsUpTheUnlivedHoursAsMess()
        {
            // 12 h away = 9 sim hours; mess must show all 12 real hours.
            var slowed = NewSim(); var full = NewSim();
            slowed.CatchUp(12);
            full.Advance(12);
            Assert.AreEqual(12f / 48f, slowed.State.grime, 1e-4f);
            Assert.AreEqual(full.State.grime, slowed.State.grime, 1e-4f);
        }

        [Test]
        public void CatchUpIsDeterministic()
        {
            var a = NewSim(7); var b = NewSim(7);
            a.Advance(3); b.Advance(3);
            a.CatchUp(55.5); b.CatchUp(55.5);
            Assert.AreEqual(JsonUtility.ToJson(b.State), JsonUtility.ToJson(a.State));
        }

        [Test]
        public void AWeekAwayStaysFast()
        {
            var sim = NewSim();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = sim.CatchUp(24 * 7);
            sw.Stop();
            TestContext.WriteLine($"a week away: {r.months} months in {sw.ElapsedMilliseconds} ms");
            Assert.AreEqual(48 * 12, r.months);
            Assert.Less(sw.ElapsedMilliseconds, 1000);
        }
    }
}
