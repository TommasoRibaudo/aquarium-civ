using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#603 (answers #576): a new box opens in spring; a box saved before keeps its season (SeasonCalendar).</summary>
    public class SpringStartTests
    {
        int was;
        [SetUp] public void NewCalendar() { was = SeasonCalendar.NewBoxShift; SeasonCalendar.NewBoxShift = SeasonCalendar.DefaultNewBoxShift; }
        [TearDown] public void BackToTheTestsCalendar() { SeasonCalendar.NewBoxShift = was; SeasonCalendar.Shift = 0; }

        static SimCore Fresh(int seed = 5)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        [Test]
        public void AFreshBoxsMonthZeroReadsSpring()
        {
            var sim = Fresh();
            Assert.That(sim.State.month, Is.EqualTo(0L), "years, history and the clock still count from zero");
            Assert.That(LifeClock.SeasonOf(sim.State.month), Is.EqualTo(Season.Spring));
            Assert.That(sim.CalendarMonth, Is.EqualTo(2));
        }

        [Test]
        public void TheSeasonsTurnOnTheCalendarFromThere()
        {
            var sim = Fresh();
            var seen = new System.Collections.Generic.List<Season>();
            for (int m = 0; m < 12; m += 3) seen.Add(LifeClock.SeasonOf(m));
            Assert.That(seen, Is.EqualTo(new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter }));
            sim.Advance(sim.Config.StepHours * 4 + 1e-6);
            Assert.That(sim.State.month, Is.EqualTo(4L));
            Assert.That(sim.CalendarMonth, Is.EqualTo(6), "four months in: the middle of summer's first half");
        }

        [Test]
        public void TheLandsFirstStepIsASpringStep()
        {
            Assert.That(Fresh().State.seasonShift, Is.EqualTo(SeasonCalendar.DefaultNewBoxShift));
            Assert.That(AquariumCiv.Sim.Eco.EcoModel.SeasonOf(1), Is.EqualTo(0));
        }

        [Test]
        public void ANewBoxOnTheShelfAlsoOpensInSpring()
        {
            var first = Fresh();
            var next = SimCore.SeedNextBox(first.Config, first, 6, 1, new BoxCompletion(), out _);
            Assert.That(LifeClock.SeasonOf(next.State.month), Is.EqualTo(Season.Spring));
        }

        [Test]
        public void AnOldSaveKeepsItsSeason()
        {
            // A save from before has no seasonShift in its JSON: it loads as 0, and month 0 is winter as it always was.
            var cfg = new SimConfig { ecology = true };
            var old = SimCore.NewWorld(5, cfg);
            old.seasonShift = 0;
            var json = JsonUtility.ToJson(old);
            var loaded = JsonUtility.FromJson<SimState>(json.Replace("\"seasonShift\":0,", "").Replace(",\"seasonShift\":0", ""));
            var sim = new SimCore(cfg, loaded);
            Assert.That(sim.State.seasonShift, Is.EqualTo(0));
            Assert.That(LifeClock.SeasonOf(0), Is.EqualTo(Season.Winter));
            Assert.That(LifeClock.SeasonOf(2), Is.EqualTo(Season.Spring));
            Fresh(); // (the next box in play sets its own calendar)
        }
    }
}
