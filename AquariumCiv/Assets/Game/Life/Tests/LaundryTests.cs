using System;
using System.Collections.Generic;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#705 (B28): the washing line's rules. The pure ones (when it may hang, how long it stays, who may take it in) and the planner's
    /// (never in rain, a storm, snow or winter; never at or past 17:00; the taker is the doer or a housemate at their own door; the chore stays
    /// inside the home's dawn window). The look and the doll's beats are the View's and AnimBench's.</summary>
    public class LaundryTests
    {
        // ---- the pure rules ------------------------------------------------------------------------------------------------

        [TestCase(Weather.Clear, Season.Summer, true)]
        [TestCase(Weather.Fog, Season.Spring, true)]
        [TestCase(Weather.Rainbow, Season.Autumn, true)]
        [TestCase(Weather.KeeperRain, Season.Summer, false)]
        [TestCase(Weather.Storm, Season.Spring, false)]
        [TestCase(Weather.Snow, Season.Autumn, false)]
        [TestCase(Weather.Clear, Season.Winter, false)]
        [TestCase(Weather.Fog, Season.Winter, false)]
        public void WashingHangsOnlyOnADryDayOutsideWinter(Weather w, Season s, bool hangs)
        {
            Assert.AreEqual(hangs, Laundry.CanHang(s, w));
            Assert.AreEqual(hangs, Laundry.ChoreFits(s, w, 6.8f));
        }

        [Test]
        public void ALateChoreIsAnotherChoreSoNothingStandsPast17()
        {
            Assert.IsTrue(Laundry.ChoreFits(Season.Summer, Weather.Clear, 8f));
            Assert.IsTrue(Laundry.ChoreFits(Season.Summer, Weather.Clear, 15.8f));
            Assert.IsFalse(Laundry.ChoreFits(Season.Summer, Weather.Clear, 16.2f));
            Assert.IsFalse(Laundry.ChoreFits(Season.Summer, Weather.Clear, 17f));
            Assert.IsFalse(Laundry.ChoreFits(Season.Summer, Weather.Clear, 17.3f), "the evening chore at the door");
        }

        [Test]
        public void TheStayIsOneViewHourAfterSheLeavesAndNeverPast17()
        {
            Assert.AreEqual(8.1f, Laundry.TakeInAt(7.1f), 1e-4f);
            Assert.AreEqual(Laundry.StayHours, 1f);
            Assert.AreEqual(17f, Laundry.TakeInAt(16.6f), 1e-4f, "capped at 17:00");
            Assert.AreEqual(17f, Laundry.TakeInAt(16.9f), 1e-4f);
            Assert.AreEqual(9.1f, Laundry.FadeAt(7.1f), 1e-4f, "the fade is the 2 h mark");
            Assert.AreEqual(17f, Laundry.FadeAt(16.5f), 1e-4f);
            for (float leave = 5f; leave < 17f; leave += 0.37f)
            {
                Assert.LessOrEqual(Laundry.TakeInAt(leave), Laundry.Latest + 1e-4f, $"leave {leave}");
                Assert.LessOrEqual(Laundry.FadeAt(leave), Laundry.Latest + 1e-4f, $"leave {leave}");
                Assert.GreaterOrEqual(Laundry.FadeAt(leave), Laundry.TakeInAt(leave));
            }
        }

        [Test]
        public void ThreeClothsTakeSevenSecondsAndTheBeatAddsUp()
        {
            Assert.AreEqual(3, Laundry.ClothsFor(7f));
            Assert.AreEqual(1, Laundry.ClothsFor(1f), "never fewer than one");
            Assert.AreEqual(4, Laundry.ClothsFor(30f), "a home's line holds four");
            Assert.AreEqual(5, Laundry.ClothsFor(30f, Laundry.BigCapacity));
            Assert.AreEqual(5, Laundry.CapacityFor(4));
            Assert.AreEqual(4, Laundry.CapacityFor(2));
            Assert.AreEqual(Laundry.SecondsPerCloth, Laundry.Lift + Laundry.Shake + Laundry.Reach + Laundry.Pin + Laundry.Smooth + Laundry.Step, 1e-4f);
            // 0.7 view hours at dawn's 10 s per hour is 7 s: three cloths fit
            Assert.AreEqual(3, Laundry.ClothsFor(LifeClock.RealSecondsBetween(6.0f, 6.0f + Laundry.ChoreHours)));
        }

        [Test]
        public void ClothsAppearOneAtATimeAndComeOffOneAtATime()
        {
            Assert.AreEqual(0, Laundry.PinnedBy(0.5f, 3));
            Assert.AreEqual(1, Laundry.PinnedBy(Laundry.PinnedAt(0) + 0.01f, 3));
            Assert.AreEqual(2, Laundry.PinnedBy(Laundry.PinnedAt(1) + 0.01f, 3));
            Assert.AreEqual(3, Laundry.PinnedBy(99f, 3));
            Assert.AreEqual(3, Laundry.LeftAfter(0.3f, 3), "she is still walking out");
            Assert.AreEqual(2, Laundry.LeftAfter(Laundry.WalkOut + Laundry.PluckSeconds + 0.01f, 3));
            Assert.AreEqual(0, Laundry.LeftAfter(99f, 3));
            Assert.Greater(Laundry.TakeInSeconds(3), Laundry.WalkOut + 3 * Laundry.PluckSeconds, "the stakes come out after the last cloth");
        }

        [Test]
        public void ClothsDryFromSeventyToAHundredPercentOverTheStay()
        {
            Assert.AreEqual(0.7f, Laundry.Dryness(0f), 1e-4f);
            Assert.AreEqual(0.85f, Laundry.Dryness(0.5f), 1e-4f);
            Assert.AreEqual(1f, Laundry.Dryness(1f), 1e-4f);
            Assert.AreEqual(1f, Laundry.Dryness(5f), 1e-4f);
        }

        [Test]
        public void TheLineGoesThroughItsStatesOnTheClock()
        {
            float start = 7f, leave = 7.7f, take = 8.7f, fade = 9.7f;
            Assert.AreEqual(Laundry.Phase.None, Laundry.PhaseAt(6.9f, start, leave, take, true, fade, 0f, 3));
            Assert.AreEqual(Laundry.Phase.Going, Laundry.PhaseAt(7.3f, start, leave, take, true, fade, 0f, 3));
            Assert.AreEqual(Laundry.Phase.Hung, Laundry.PhaseAt(8.2f, start, leave, take, true, fade, 0f, 3));
            Assert.AreEqual(Laundry.Phase.TakingIn, Laundry.PhaseAt(8.72f, start, leave, take, true, fade, 1f, 3));
            Assert.AreEqual(Laundry.Phase.Gone, Laundry.PhaseAt(8.9f, start, leave, take, true, fade, 30f, 3));
            Assert.AreEqual(Laundry.Phase.Hung, Laundry.PhaseAt(9f, start, leave, take, false, fade, 0f, 3), "nobody free: it hangs on to the 2 h mark");
            Assert.AreEqual(Laundry.Phase.Fading, Laundry.PhaseAt(9.71f, start, leave, take, false, fade, 0.3f, 3));
            Assert.AreEqual(Laundry.Phase.Gone, Laundry.PhaseAt(9.8f, start, leave, take, false, fade, 5f, 3));
        }

        // ---- the planner's rules -------------------------------------------------------------------------------------------

        static SimCore VillageSim(int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < Era.Village; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            Assert.AreEqual(Era.Village, sim.State.era);
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        /// <summary>Every 6 minutes of view time from 05:00 to 20:00 over <paramref name="days"/> days: what the people do.</summary>
        static IEnumerable<(long day, float hour, PersonActivity a)> Sweep(LifePlanner planner, long from, int days)
        {
            var acts = new List<PersonActivity>();
            for (long day = from; day < from + days; day++)
                for (float h = 5f; h < 20f; h += 0.1f)
                {
                    planner.PlanAll(new ViewTime(day, h), acts);
                    foreach (var a in acts) yield return (day, h, a);
                }
        }

        [TestCase(Weather.KeeperRain)]
        [TestCase(Weather.Storm)]
        [TestCase(Weather.Snow)]
        public void NothingHangsInRainAStormOrSnow(Weather weather)
        {
            var sim = VillageSim(7);
            var planner = new LifePlanner(sim) { ForcedSeason = Season.Summer, ForcedWeather = weather };
            int seen = 0;
            foreach (var (day, h, a) in Sweep(planner, sim.State.month, 8))
                if (a.kind == ActivityKind.HangWashing || a.kind == ActivityKind.TakeInWashing) seen++;
            Assert.AreEqual(0, seen, $"washing on a {weather} day");
            for (long d = sim.State.month; d < sim.State.month + 8; d++) Assert.AreEqual(0, planner.LaundryOn(d).Count, $"a line up on a {weather} day {d}");
        }

        [Test]
        public void NothingHangsInWinter()
        {
            var sim = VillageSim(7);
            var planner = new LifePlanner(sim) { ForcedSeason = Season.Winter, ForcedWeather = Weather.Clear };
            int seen = 0;
            foreach (var (day, h, a) in Sweep(planner, sim.State.month, 8))
                if (a.kind == ActivityKind.HangWashing || a.kind == ActivityKind.TakeInWashing) seen++;
            Assert.AreEqual(0, seen, "washing in winter");
            for (long d = sim.State.month; d < sim.State.month + 8; d++) Assert.AreEqual(0, planner.LaundryOn(d).Count, $"a line up in winter, day {d}");
        }

        /// <summary>On dry summer days over a month: the chore happens, stays in the dawn window, has no line at 17:00, and its take-in is the doer's or a housemate's.</summary>
        [Test]
        public void ADryDayHangsWashingInsideTheDawnWindowAndTheTakerIsAHousemate()
        {
            var sim = VillageSim(7);
            var planner = new LifePlanner(sim) { ForcedSeason = Season.Summer, ForcedWeather = Weather.Clear };
            int hung = 0, taken = 0, faded = 0, afterChores = 0;
            var acts = new List<PersonActivity>();
            for (long day = sim.State.month; day < sim.State.month + 40; day++)
            {
                var lines = planner.LaundryOn(day);
                foreach (var l in lines)
                {
                    hung++;
                    if (l.start < 10f)
                    {
                        Assert.GreaterOrEqual(l.start, 5f, "the dawn chore starts after waking");
                        Assert.LessOrEqual(l.leave, 8.6f, $"day {day}: the chore ends inside the dawn window");
                    }
                    Assert.LessOrEqual(l.leave, Laundry.Latest, "she leaves by 17:00");
                    Assert.LessOrEqual(l.fadeAt, Laundry.Latest + 1e-4f, "never past 17:00");
                    Assert.LessOrEqual(l.takeAt + Laundry.TakeInHours, Laundry.Latest + 1e-3f, "the take-in is done by 17:00");
                    Assert.GreaterOrEqual(l.takeAt, l.leave, "the stay comes after the chore");
                    Assert.GreaterOrEqual(l.cloths, 1);
                    Assert.LessOrEqual(l.cloths, Laundry.BigCapacity);
                    if (l.Taken)
                    {
                        taken++;
                        planner.PlanAll(new ViewTime(day, l.takeAt + 0.01f), acts);
                        var taker = acts.Find(a => a.who.Key == l.takerKey);
                        Assert.AreEqual(ActivityKind.TakeInWashing, taker.kind, $"day {day}: the taker is at the line at {l.takeAt:0.00}");
                        Assert.AreEqual(l.home, taker.place, "the taker steps out of the home that hung it: the doer or a housemate");
                        planner.PlanAll(new ViewTime(day, l.TakeEnd + 0.02f), acts);
                        Assert.AreNotEqual(ActivityKind.TakeInWashing, acts.Find(a => a.who.Key == l.takerKey).kind, "back to the day after the take-in");
                    }
                    else { faded++; Assert.GreaterOrEqual(l.fadeAt, l.takeAt); }
                }
                // no washing, no take-in, no line at 17:00 or after
                foreach (float h in new[] { 17f, 17.2f, 18.5f })
                {
                    planner.PlanAll(new ViewTime(day, h), acts);
                    foreach (var a in acts) { if (a.kind == ActivityKind.HangWashing || a.kind == ActivityKind.TakeInWashing) afterChores++; }
                }
            }
            TestContext.WriteLine($"LAUNDRY village seed 7, 40 dry summer days: {hung} lines, {taken} taken in, {faded} faded, {afterChores} washing activities at or past 17:00");
            Assert.Greater(hung, 0, "the chore never came up in 40 days");
            Assert.AreEqual(0, afterChores, "washing at or after 17:00");
        }

        [Test]
        public void TheCheatsChoreHangsThenIsTakenInByTheSameHand()
        {
            var sim = VillageSim(7);
            var planner = new LifePlanner(sim) { ForcedSeason = Season.Summer, ForcedWeather = Weather.Clear };
            long day = sim.State.month; float now = 9f;
            int key = planner.DebugLaundryPick(0f, 0f);
            Assert.GreaterOrEqual(key, 0);
            Assert.IsTrue(planner.DebugLaundryNow(new ViewTime(day, now), key));
            var acts = new List<PersonActivity>();
            planner.PlanAll(new ViewTime(day, now + 0.2f), acts);
            Assert.AreEqual(ActivityKind.HangWashing, acts.Find(a => a.who.Key == key).kind);
            var lines = planner.LaundryOn(day);
            Assert.IsTrue(lines.Count >= 1);
            var l = lines[lines.Count - 1];
            Assert.IsTrue(l.debug && l.Taken && l.takerKey == key);
            float stayReal = LifeClock.RealSecondsBetween(l.leave, l.takeAt);
            Assert.AreEqual(16f, stayReal, 1f, "the cheat's stay is about 16 s real");
            planner.PlanAll(new ViewTime(day, l.takeAt + 0.02f), acts);
            Assert.AreEqual(ActivityKind.TakeInWashing, acts.Find(a => a.who.Key == key).kind);
            Assert.IsTrue(planner.DebugLaundryNow(new ViewTime(day, 9f), key));
            Assert.IsTrue(planner.DebugTakeInNow(new ViewTime(day, 9.1f)), "the second cheat ends the stay now");
            Assert.AreEqual(Math.Max(9.7f, 9.1f), planner.LaundryOn(day)[planner.LaundryOn(day).Count - 1].takeAt, 0.02f, "taken in right after the chore");
        }
    }
}
