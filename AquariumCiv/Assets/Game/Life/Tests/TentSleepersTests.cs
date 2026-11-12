using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>
    /// fb7 #607 (from #599): in a camp at night, the last one to go to bed walks into the tent and everyone already asleep came
    /// out and walked in again. A replan at night (the sim's signature moved: a chronicle entry, a nudge, the month) must leave
    /// the sleepers asleep where they are.
    /// </summary>
    public class TentSleepersTests
    {
        static bool Asleep(in PersonActivity a) => a.kind == ActivityKind.Sleep && !a.moving;

        /// <summary>Per person, the sample hours at which they were asleep, between 19:00 and 24:00 of one day.</summary>
        static Dictionary<int, List<(float h, ActivityKind k, bool moving)>> Night(LifePlanner planner, long day, Action<float> atHour = null)
        {
            var log = new Dictionary<int, List<(float, ActivityKind, bool)>>();
            var acts = new List<PersonActivity>();
            for (float h = 18.5f; h < 23.99f; h += 0.05f)
            {
                atHour?.Invoke(h);
                planner.PlanAll(new ViewTime(day, h), acts);
                foreach (var a in acts)
                {
                    if (!log.TryGetValue(a.who.Key, out var l)) log[a.who.Key] = l = new List<(float, ActivityKind, bool)>();
                    l.Add((h, a.kind, a.moving));
                }
            }
            return log;
        }

        /// <summary>Anyone asleep, then not asleep, then asleep again: came out of bed and went back in.</summary>
        static List<string> Risers(Dictionary<int, List<(float h, ActivityKind k, bool moving)>> log)
        {
            var bad = new List<string>();
            foreach (var kv in log)
            {
                bool slept = false, woke = false;
                foreach (var s in kv.Value)
                {
                    bool asleep = s.k == ActivityKind.Sleep && !s.moving;
                    if (asleep && woke) { bad.Add($"person {kv.Key} asleep again at {s.h:0.00}"); break; }
                    if (asleep) slept = true;
                    else if (slept) woke = true;
                }
            }
            return bad;
        }

        static string Dump(Dictionary<int, List<(float h, ActivityKind k, bool moving)>> log)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in log)
            {
                sb.Append("p").Append(kv.Key).Append(": ");
                ActivityKind last = ActivityKind.None; bool lastMoving = false; bool first = true;
                foreach (var s in kv.Value)
                {
                    if (first || s.k != last || s.moving != lastMoving) sb.Append(s.h.ToString("0.00")).Append(s.moving ? " walk>" : " ").Append(s.k).Append("; ");
                    first = false; last = s.k; lastMoving = s.moving;
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        [Test]
        public void A_quiet_camp_night_has_nobody_get_up_after_going_to_bed()
        {
            var (sim, planner) = CampRhythmTests.Camp(5);
            long day = sim.State.month + 3;
            var log = Night(planner, day);
            TestContext.WriteLine(Dump(log));
            Assert.That(Risers(log), Is.Empty, Dump(log));
        }

        [Test]
        public void A_replan_in_the_middle_of_the_night_leaves_the_sleepers_asleep()
        {
            foreach (int pop in new[] { 5, 8 })
            {
                var (sim, planner) = CampRhythmTests.Camp(pop);
                long day = sim.State.month + 3;
                // every kind of replan trigger, one per sample hour from 19:00
                float next = 19.0f; int n = 0;
                var log = Night(planner, day, h =>
                {
                    if (h < next) return;
                    next += 0.2f;
                    n++;
                    planner.Invalidate(); // what a moved sim signature does: every cached day is dropped and planned again
                });
                TestContext.WriteLine(Dump(log));
                Assert.That(Risers(log), Is.Empty, $"pop {pop}\n" + Dump(log));
            }
        }

        [Test]
        public void A_month_ticking_over_in_the_middle_of_the_night_leaves_the_sleepers_asleep()
        {
            foreach (int pop in new[] { 5, 8 })
                for (int tickAt = 0; tickAt < 5; tickAt++)
                {
                    var (sim, planner) = CampRhythmTests.Camp(pop);
                    long day = sim.State.month + 2;
                    float when = 20.1f + tickAt * 0.9f;
                    bool done = false;
                    var log = Night(planner, day, h => { if (!done && h >= when) { done = true; sim.Step(); } });
                    Assert.That(Risers(log), Is.Empty, $"pop {pop}, month ticked at {when:0.0}: " + Dump(log));
                }
        }

        [Test]
        public void A_newcomer_or_a_gone_one_in_the_middle_of_the_night_leaves_the_sleepers_asleep()
        {
            foreach (int delta in new[] { 1, 2, 3, -1, -2 })
                for (int tickAt = 0; tickAt < 4; tickAt++)
                {
                    var (sim, planner) = CampRhythmTests.Camp(8);
                    long day = sim.State.month + 2;
                    float when = 20.4f + tickAt * 0.9f;
                    bool done = false;
                    var log = Night(planner, day, h => { if (!done && h >= when) { done = true; sim.State.population = Math.Max(5, sim.State.population + delta); } });
                    Assert.That(Risers(log), Is.Empty, $"population {delta:+0;-0} at {when:0.0}: " + Dump(log));
                }
        }
    }
}
