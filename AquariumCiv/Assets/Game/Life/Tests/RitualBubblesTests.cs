using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#276 (pure part): who shows which wordless symbol and when during a ritual or festival.</summary>
    public class RitualBubblesTests
    {
        static SimCore TownBox(int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < Era.Town; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        static void AssertSchedule(List<RitualBubble> bubbles, float from, float to)
        {
            foreach (var b in bubbles)
            {
                Assert.GreaterOrEqual(b.start, from - 1e-4f);
                Assert.LessOrEqual(b.end, to + 1e-4f);
                Assert.Less(b.start, b.end);
            }
            foreach (var b in bubbles)
            {
                int alive = bubbles.Count(k => k.start <= b.start + 1e-5f && k.end > b.start + 1e-5f);
                Assert.LessOrEqual(alive, RitualBubbles.MaxAlive, "too many bubbles at once");
                Assert.AreEqual(1, bubbles.Count(k => k.who.Equals(b.who) && k.start <= b.start + 1e-5f && k.end > b.start + 1e-5f), "one bubble per person at a time");
            }
        }

        [TestCase(5)]
        [TestCase(7)]
        [TestCase(23)]
        public void TheDollRitual_ShowsDollsThenEyes_OnASchedule(int seed)
        {
            var sim = TownBox(seed);
            var planner = LifePlanner.Wired(sim);
            var now = RitualBubbles.StartNow(sim, planner, new ViewTime(sim.State.month, 10f), RelicId.KeeperDoll);
            Assert.IsNotNull(now, "the cheat starts the ritual");
            Assert.IsNotEmpty(now.bubbles);
            AssertSchedule(now.bubbles, now.moment.start, now.moment.end);
            var owner = now.moment.cast.First(c => c.role == RelicCast.Owner).who;
            Assert.IsTrue(now.bubbles.Any(b => b.who.Equals(owner) && b.symbol == RitualSymbol.Doll), "the carrier shows the doll");
            Assert.IsTrue(now.bubbles.Any(b => b.symbol == RitualSymbol.Eye), "the onlookers show an eye");
            Assert.IsTrue(now.bubbles.All(b => now.moment.cast.Any(c => c.who.Equals(b.who))), "only the moment's cast show symbols");
        }

        [Test]
        public void TheMaskFestival_ShowsSunAndNotes()
        {
            var sim = TownBox(7);
            var planner = LifePlanner.Wired(sim);
            var now = RitualBubbles.StartNow(sim, planner, new ViewTime(sim.State.month, 13f), RelicId.FestivalMask);
            Assert.IsNotNull(now);
            AssertSchedule(now.bubbles, now.moment.start, now.moment.end);
            Assert.IsTrue(now.bubbles.Any(b => b.symbol == RitualSymbol.Sun) && now.bubbles.Any(b => b.symbol == RitualSymbol.Notes));
        }

        [Test]
        public void TheFestivalEvening_RotatesBubblesThroughEveryone_WithinTheCap()
        {
            var people = Enumerable.Range(0, 12).Select(PersonRef.Crowd).ToList();
            var b = RitualBubbles.ForFestival(17.5f, 21.5f, people);
            AssertSchedule(b, 17.5f, 21.5f);
            Assert.AreEqual(RitualSymbol.Sun, b.OrderBy(x => x.start).First().symbol, "it opens with a sun");
            Assert.GreaterOrEqual(b.Select(x => x.who).Distinct().Count(), 10, "nearly everyone gets a turn");
            Assert.IsEmpty(RitualBubbles.ForFestival(17.5f, 21.5f, new List<PersonRef>()));
        }

        [Test]
        public void SameInputs_SameSchedule()
        {
            var people = Enumerable.Range(0, 8).Select(PersonRef.Crowd).ToList();
            var a = RitualBubbles.ForFestival(17.5f, 21.5f, people);
            var b = RitualBubbles.ForFestival(17.5f, 21.5f, people);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual((a[i].who, a[i].symbol, a[i].start), (b[i].who, b[i].symbol, b[i].start));
        }
    }
}
