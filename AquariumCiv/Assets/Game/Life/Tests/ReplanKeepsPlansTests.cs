using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>
    /// #697 (everyone sets off together, then all stop midway and turn back) and #696 (the sleepers come out of the tent before the last one
    /// goes in): both came from a replan, which threw every cached day away and planned it again from the new snapshot. A replan now keeps
    /// what each person is doing: the ones asleep stay asleep, the ones on a walk finish it, the ones standing set off from where they are.
    /// One test per way the planner replans (<see cref="LifePlanner.ReplanTriggers"/>), at night and by day.
    /// </summary>
    public class ReplanKeepsPlansTests
    {
        static readonly string[] Triggers = LifePlanner.ReplanTriggers;

        // The trigger without the DebugTrigger helper, so the same test runs against code that does not have it.
        internal static void Fire(SimCore sim, LifePlanner planner, string trigger)
        {
            var s = sim.State;
            switch (trigger)
            {
                case "month": sim.Step(); break;
                case "population": s.population += 1; break;
                case "chronicle": s.chronicle.Add(new ChronicleEntry { id = s.NextId(), month = s.month, kind = "note", text = "", salience = 1f }); break;
                case "nudge": sim.PlaceNudge(SimCore.NudgeKind.Rain, 0f, 0f); break;
                case "supper": sim.DebugSupper(SupperPick.Pot); break;
                case "building": sim.DebugBuild(BuildingType.Hut); break;
                case "invalidate": planner.Invalidate(); break;
                case "season": planner.ForcedSeason = planner.ForcedSeason; break;
                case "weather": planner.ForcedWeather = planner.ForcedWeather; break;
                case "waterrun": planner.ForceWaterRun(0); break;
            }
        }

        static bool Asleep(in PersonActivity a) => a.kind == ActivityKind.Sleep && !a.moving;

        /// <summary>A walk that was under way and is not any more, on a plan that has not run its course: it turned round or stopped.</summary>
        static bool Reversed(in PersonActivity prev, in PersonActivity now, float hour) =>
            prev.moving && !prev.enRoute && prev.end > hour + 1e-3f && !now.enRoute && !(now.moving && now.place == prev.place); // a chat on the way is a run-in, planned to stop them

        /// <summary>Samples one stretch of a day every 0.05 h, firing the triggers at the hours given; returns the reversals and risers.</summary>
        static (List<string> reversed, List<string> risers) Run(SimCore sim, LifePlanner planner, long day, float from, float to, IList<(float at, string trigger)> fire)
        {
            var reversed = new List<string>(); var risers = new List<string>();
            var prev = new Dictionary<int, PersonActivity>();
            var slept = new HashSet<int>(); var woke = new HashSet<int>();
            var acts = new List<PersonActivity>();
            int next = 0;
            for (float h = from; h < to; h += 0.05f)
            {
                while (next < fire.Count && h >= fire[next].at) Fire(sim, planner, fire[next++].trigger);
                planner.PlanAll(new ViewTime(day, h), acts);
                foreach (var a in acts)
                {
                    int k = a.who.Key;
                    if (prev.TryGetValue(k, out var p) && Reversed(p, a, h)) reversed.Add($"person {k} at {h:0.00}: walk to {p.place.kind} became {a.kind}{(a.moving ? ">" : "=")}{a.place.kind}");
                    if (Asleep(a)) { if (woke.Contains(k)) { risers.Add($"person {k} asleep again at {h:0.00}"); woke.Remove(k); } slept.Add(k); }
                    else if (slept.Contains(k) && !(a.moving && a.place.kind == PlaceKind.Home)) woke.Add(k);
                    prev[k] = a;
                }
            }
            return (reversed, risers);
        }

        static IEnumerable<string> EveryTrigger => LifePlanner.ReplanTriggers;

        // ---------------------------------------------------------------- #696: at night

        [TestCaseSource(nameof(EveryTrigger))]
        public void A_replan_at_night_keeps_the_sleepers_in_the_tent(string trigger)
        {
            foreach (int pop in new[] { 5, 8 })
                for (float at = 19.7f; at < 23.7f; at += 0.45f)
                {
                    var (sim, planner) = CampRhythmTests.Camp(pop);
                    long day = sim.State.month + 1;
                    var (rev, risers) = Run(sim, planner, day, 18.5f, 23.95f, new[] { (at, trigger) });
                    Assert.That(risers, Is.Empty, $"{trigger} at {at:0.00}, pop {pop}: someone came out of bed and went back in");
                    Assert.That(rev, Is.Empty, $"{trigger} at {at:0.00}, pop {pop}: a walk turned round");
                }
        }

        [Test]
        public void Replans_one_after_another_at_night_keep_the_sleepers_in_too()
        {
            var (sim, planner) = CampRhythmTests.Camp(8);
            long day = sim.State.month + 1;
            var fire = new List<(float, string)>();
            float h = 19.9f;
            foreach (var t in Triggers) { fire.Add((h, t)); h += 0.2f; }
            var (rev, risers) = Run(sim, planner, day, 18.5f, 23.95f, fire);
            Assert.That(risers, Is.Empty);
            Assert.That(rev, Is.Empty);
        }

        // ---------------------------------------------------------------- #697: by day

        [TestCaseSource(nameof(EveryTrigger))]
        public void A_replan_in_the_middle_of_a_walk_by_day_does_not_turn_the_walkers_round(string trigger)
        {
            int walkersSeen = 0;
            for (float at = 5.2f; at < 22.5f; at += 0.35f)
            {
                var (sim, planner) = CampRhythmTests.Camp(8);
                long day = sim.State.month + 1;
                var acts = new List<PersonActivity>();
                planner.PlanAll(new ViewTime(day, at - 0.05f), acts);
                walkersSeen += acts.Count(a => a.moving);
                var (rev, _) = Run(sim, planner, day, at - 0.05f, at + 1.0f, new[] { (at, trigger) });
                Assert.That(rev, Is.Empty, $"{trigger} at {at:0.00}: {string.Join("; ", rev)}");
            }
            Assert.Greater(walkersSeen, 20, "the sweep did catch people on walks");
        }

        [Test]
        public void A_run_of_different_replans_through_the_evening_never_turns_a_walk_round()
        {
            foreach (int seed in new[] { 7, 11 })
            {
                var (sim, planner) = CampRhythmTests.Camp(8, seed);
                long day = sim.State.month;
                var fire = new List<(float, string)>();
                float h = 16.6f;
                foreach (var t in new[] { "supper", "month", "population", "chronicle", "building", "nudge", "supper" }) { fire.Add((h, t)); h += 0.3f; }
                var (rev, risers) = Run(sim, planner, day, 15.5f, 23.95f, fire);
                Assert.That(rev, Is.Empty, $"seed {seed}: " + string.Join("; ", rev));
                Assert.That(risers, Is.Empty, $"seed {seed}");
            }
        }

        [Test]
        public void A_long_session_with_the_sims_own_months_and_other_replans_never_turns_a_walk_round()
        {
            foreach (int seed in new[] { 7, 11 })
            {
                var r = ReplanDiagnosisTests.Natural(seed, 25, true);
                Assert.Greater(r.replans, 20, "the session replanned often enough to mean something");
                Assert.That(r.reversals, Is.EqualTo(0), $"seed {seed}: " + string.Join("; ", r.lines));
                Assert.That(r.massReversals, Is.EqualTo(0), $"seed {seed}: " + string.Join("; ", r.lines));
                Assert.That(r.risers, Is.EqualTo(0), $"seed {seed}");
            }
        }
    }
}
