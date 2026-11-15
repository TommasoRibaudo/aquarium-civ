using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #696 (the tent at night: people come out before the last sleeper goes in) and #697 (everyone sets off together and turns back), on
    /// the real scene: each replan trigger is forced at night and by day (<see cref="PeopleView.DebugReplan"/>, the same cheats as the
    /// debug panel's "Replans" section), and across the frames after it the sleepers' dolls stay hidden in their tents and nobody's walk
    /// turns round.
    /// </summary>
    [Category("ReplanKeepsPlans")]
    public class ReplanKeepsPlansPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();

        IEnumerator Grown()
        {
            UseLivingLand(); // the camp of tents round the fire, as in the playtest
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            Sim.CleanAll();
            yield return Frames(2);
        }

        static bool Asleep(PersonActivity a) => a.kind == ActivityKind.Sleep && !a.moving;

        public static IEnumerable<string> Triggers => LifePlanner.ReplanTriggers;

        [UnityTest, Timeout(180000)]
        public IEnumerator EveryReplanTriggerAtNightLeavesTheSleepersHiddenInTheirTents([ValueSource(nameof(Triggers))] string trigger)
        {
            yield return Grown();
            foreach (float hour in new[] { 20.4f, 21.6f, 23.2f })
            {
                PeopleView.DebugSetHour(hour);
                yield return Frames(4);
                var pv = People;
                // Time stands still for the check: the question is what the replan does, not what the clock does.
                pv.timeScale = 0.05f;
                yield return Frames(3);
                var asleep = pv.Activities.Where(Asleep).Select(a => a.who).ToList();
                if (hour > 21f) Assert.IsNotEmpty(asleep, $"somebody is asleep at {hour:0.0}");
                foreach (var who in asleep) Assert.IsFalse(pv.TryGetDollTop(who, out _), $"{who} is asleep, so their doll is hidden, before the replan");
                StringAssert.Contains(trigger, PeopleView.DebugReplan(trigger));
                for (int f = 0; f < 40; f++)
                {
                    yield return null;
                    foreach (var who in asleep)
                    {
                        var a = pv.Activities.First(x => x.who.Equals(who));
                        Assert.IsTrue(Asleep(a) || (a.moving && a.place.kind == PlaceKind.Home), $"{trigger} at {hour:0.0}, frame {f}: {who} got up (now {a.kind}, moving {a.moving})");
                        Assert.IsFalse(pv.TryGetDollTop(who, out _), $"{trigger} at {hour:0.0}, frame {f}: {who} came out of the tent");
                    }
                }
                AssertNoErrors($"replan {trigger} at night");
            }
            CloseSlice();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator EveryReplanTriggerByDayLeavesTheWalkersWalking([ValueSource(nameof(Triggers))] string trigger)
        {
            // 17:20 and 6:30 are when the camp is on its way to supper / to work: the most walkers at once
            yield return Grown();
            foreach (float hour in new[] { 6.5f, 17.3f })
            {
                PeopleView.DebugSetHour(hour);
                var pv = People;
                pv.timeScale = 0.05f;
                yield return Frames(4);
                var walking = pv.Activities.Where(a => a.moving && a.end > pv.Now.hour + 0.05f).ToDictionary(a => a.who.Key, a => (place: a.place, end: a.end));
                StringAssert.Contains(trigger, PeopleView.DebugReplan(trigger));
                for (int f = 0; f < 30; f++)
                {
                    yield return null;
                    foreach (var a in pv.Activities)
                        if (walking.TryGetValue(a.who.Key, out var w) && w.end > pv.Now.hour + 0.02f)
                            Assert.IsTrue(a.moving && a.place == w.place, $"{trigger} at {hour:0.0}, frame {f}: person {a.who.Key} was walking to {w.place.kind} and is now {a.kind}{(a.moving ? '>' : '=')}{a.place.kind}");
                }
                AssertNoErrors($"replan {trigger} by day");
            }
            CloseSlice();
        }
    }
}
