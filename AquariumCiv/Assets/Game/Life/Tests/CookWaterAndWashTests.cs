using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#405: the cook draws the water first and carries it to the fire, then stirs the pot. #406: a grown-up near a
    /// puddle washes their hands once a day, not on every visit (fire, wash, fire, wash).</summary>
    public class CookWaterAndWashTests
    {
        static SimCore Camp(int seed, int months = 60)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < months && !sim.State.eraReady; m++) sim.Step();
            Assert.AreEqual(Era.Camp, sim.State.era);
            return sim;
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TheCookDrawsWaterBeforeTheFirstStir(int seed)
        {
            var planner = LifePlanner.Wired(Camp(seed));
            var cook = planner.People.Where(w => planner.JobOf(w) == "cook").ToList();
            Assume.That(cook, Is.Not.Empty, "the camp has a cook");
            var day = new List<PersonActivity>();
            int checkedDays = 0;
            for (int i = 0; i < 20; i++)
            {
                planner.DaySchedule(cook[0], i * 7, day);
                int stir = day.FindIndex(a => a.kind == ActivityKind.Cook);
                if (stir < 0) continue;
                checkedDays++;
                string trace = string.Join("\n", day.Take(stir + 1).Select(a => $"{a.start:0.00}-{a.end:0.00} {a.kind} {a.prop} {a.from}->{a.place}"));
                int dip = day.FindIndex(a => a.kind == ActivityKind.FetchWater && !a.moving);
                Assert.That(dip, Is.InRange(0, stir), $"day {i * 7}: the cook dips the bucket before the first stir\n{trace}");
                int carry = day.FindIndex(dip, a => a.moving && a.prop == Prop.Buckets);
                Assert.That(carry, Is.InRange(dip, stir), $"day {i * 7}: the full buckets are carried to the fire before the stir\n{trace}");
                Assert.AreEqual(CampStash.Stand, day[carry].place, "set down at the stash by the fire (#150)");
                Assert.IsFalse(day.Take(dip).Any(a => a.prop == Prop.Buckets), "the bucket goes out empty (#152)");
            }
            Assert.Greater(checkedDays, 0, "the cook stirs the pot on some day");
        }

        [Test]
        public void AGrownUpWashesAtThePuddleOnceADay()
        {
            var sim = Camp(1);
            var planner = LifePlanner.Wired(sim);
            var cook = planner.People.First(w => planner.JobOf(w) == "cook");
            long day = sim.State.month;
            var fire = planner.Plan(cook, new ViewTime(day, 10f)).place;
            // The puddle calls every few minutes of the morning; the fire is "near" it (no positions: same place).
            var visits = new List<float>();
            for (float h = 9.6f; h < 12.5f; h += 0.3f) { visits.Add(h); planner.React(KeeperGesture.Puddle, new ViewTime(day, h), fire, default, 0.95f, 4f, 2.2f); }
            var washes = new Dictionary<PersonRef, int>();
            var acts = new List<PersonActivity>();
            foreach (float h in visits)
            {
                planner.PlanAll(new ViewTime(day, h + 0.02f), acts);
                foreach (var a in acts.Where(a => a.source == ActivitySource.Reaction && a.kind == ActivityKind.WashHands))
                    washes[a.who] = washes.TryGetValue(a.who, out int n) ? n + 1 : 1;
            }
            TestContext.WriteLine(string.Join(", ", washes.Select(kv => $"{kv.Key}: {kv.Value}")));
            Assert.IsNotEmpty(washes, "someone by the fire washes");
            Assert.IsTrue(washes.Values.All(n => n == 1), "each grown-up washes once, not on every visit");
        }
    }
}
