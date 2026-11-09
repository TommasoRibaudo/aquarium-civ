using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#521 (playtest #508): the first camp days showed the whole camp digging roots at one spot. Before bushes grow the
    /// gatherer's work is roots in the dry stream bed; now at most two dig (each at their own patch) and the rest tend the fire,
    /// fetch water or knap. Over the first ten view days, in the working day (07:30-17:00, before the evening fire where the
    /// whole camp gathers by design; the dawn water walks before 07:30 and sitting out at the door, which is rest and at each own door, do not count) no moment has more than half the grown-ups on one activity, and the bed holds at most two.</summary>
    public class CampSpreadTests
    {
        static SimCore Fresh(int seed, bool gateOpen)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            if (gateOpen) sim.DebugOpenCampGate(); // the camp gate open, still no bush: roots are still the gatherer's work
            return sim;
        }

        /// <summary>What a person is doing, as the eye sees it: a carry or a tip belongs to the dig, a walk is nothing.</summary>
        static ActivityKind? Doing(PersonActivity a)
        {
            if (a.kind == ActivityKind.Walk || a.kind == ActivityKind.Sleep || a.kind == ActivityKind.Indoors || a.kind == ActivityKind.SitOut) return null;
            if (a.kind == ActivityKind.TipLoad || (a.kind == ActivityKind.Carry && a.moving && a.prop == Prop.Basket)) return ActivityKind.Gather;
            if (a.kind == ActivityKind.Carry && a.moving && (a.prop == Prop.Buckets || a.prop == Prop.EmptyBucket)) return ActivityKind.FetchWater;
            if (a.moving && a.kind != ActivityKind.FetchWater) return null;
            return a.kind;
        }

        [TestCase(1, false)]
        [TestCase(3, false)]
        [TestCase(7, false)]
        [TestCase(1, true)]
        [TestCase(5, true)]
        public void TheFirstDaysShowSeveralThingsAtOnceAndTheBedHoldsTwo(int seed, bool gateOpen)
        {
            var sim = Fresh(seed, gateOpen);
            var planner = LifePlanner.Wired(sim);
            var grown = planner.People.Where(p => planner.StageOf(p) == LifeStage.Adult).ToList();
            Assert.GreaterOrEqual(grown.Count, 3);
            var days = new List<PersonActivity>[grown.Count];
            var spots = new HashSet<Place>();
            var kinds = new HashSet<ActivityKind>();
            var buf = new List<PersonActivity>();
            for (long day = 0; day < 10; day++)
            {
                for (int i = 0; i < grown.Count; i++) { planner.DaySchedule(grown[i], day, buf); days[i] = new List<PersonActivity>(buf); }
                if (planner.IsRestDay(day)) continue;
                for (float h = 7.5f; h < 17f; h += 0.25f)
                {
                    var count = new Dictionary<ActivityKind, int>();
                    int atBed = 0;
                    for (int i = 0; i < grown.Count; i++)
                    {
                        var a = days[i].FirstOrDefault(x => x.start <= h && h < x.end);
                        if (a.kind == ActivityKind.None) continue;
                        if (a.place.kind == PlaceKind.Stream && !a.moving && a.kind == ActivityKind.Gather) { atBed++; spots.Add(a.place); }
                        var k = Doing(a);
                        if (!k.HasValue) continue;
                        count.TryGetValue(k.Value, out int n); count[k.Value] = n + 1;
                        kinds.Add(k.Value);
                    }
                    Assert.LessOrEqual(atBed, 2, $"day {day} {h:0.00}: at most two at the root bed");
                    foreach (var kv in count)
                        Assert.LessOrEqual(kv.Value * 2, grown.Count, $"day {day} {h:0.00}: {kv.Value} of {grown.Count} grown-ups all on {kv.Key}");
                }
            }
            TestContext.WriteLine($"seed {seed} gate {gateOpen}: {grown.Count} grown-ups, activities {string.Join(",", kinds)}, dig spots {string.Join(",", spots)}");
            Assert.GreaterOrEqual(kinds.Count, 3, "at least three different things going on: " + string.Join(",", kinds));
            Assert.Greater(spots.Count, 1, "more than one dig spot");
        }
    }
}
