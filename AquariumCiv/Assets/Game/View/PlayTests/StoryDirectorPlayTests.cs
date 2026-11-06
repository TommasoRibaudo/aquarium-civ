using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#367 B11 on the real scene (every bootstrap, the default camera): with the background-life director on, a story is in
    /// progress inside the view rectangle within about 20 s of daylight, and what the director starts really plays (the kids in a
    /// game, the pair in a hand-over, the grown-up on their errand). Off starts nothing.</summary>
    public class StoryDirectorPlayTests : PlayTestBase
    {
        const int Seed = 3671;
        static SimCore Sim => GameClock.Instance.ActiveSim;

        IEnumerator Box(StoryDensity density)
        {
            StoryDirectorView.Density = density;
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(3);
            yield return null;
            PeopleView.DebugSetHour(9f);
            yield return Frames(10);
            Assert.IsNotNull(StoryDirectorView.Instance, "the director is in the scene (bootstrap)");
        }

        public override void TearDown()
        {
            StoryDirectorView.Density = StoryDensity.Normal;
            base.TearDown();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WithTheDirectorOn_AStoryIsInView_Within20s()
        {
            yield return Box(StoryDensity.Normal);
            var d = StoryDirectorView.Instance;
            float t0 = Time.realtimeSinceStartup;
            yield return Until(() => d.Director != null && d.InViewCount > 0, 22f);
            Assert.Greater(d.InViewCount, 0, $"no story in view after {Time.realtimeSinceStartup - t0:0} s: {d.Status()}");
            AssertNoErrors("the director's first looks");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WhatTheDirectorStarts_ReallyPlays_AndOffStartsNothing()
        {
            yield return Box(StoryDensity.High);
            var d = StoryDirectorView.Instance;
            var pv = PeopleView.Find();
            yield return Until(() => d.Director != null && d.Director.Started > 0, 30f);
            Assert.Greater(d.Director?.Started ?? 0, 0, "the director started a story (pop " + Sim.State.population + "): " + d.Status());
            var plan = d.LastPlan;
            yield return new WaitForSecondsRealtime(KidPlayDirector.DirectedLead + 1.5f);
            int playing = 0;
            foreach (int key in plan.members)
            {
                var who = key > 0 ? PersonRef.Notable(key) : PersonRef.Crowd(-key - 1);
                bool on = plan.kind switch
                {
                    StoryKind.ChaseRabbits or StoryKind.Ball or StoryKind.Tag or StoryKind.Splash => pv.KidPlay.GameOf(key, out _) != null,
                    StoryKind.HandOver => PairStage.TryCue(key, pv.PoseClock, out _, out _) || plan.seconds < 4f,
                    _ => pv.Planner.InLifeStory(who, pv.Now),
                };
                if (on) playing++;
            }
            Assert.Greater(playing, 0, $"{plan}: its people play it");

            StoryDirectorView.CycleDensity(); // high -> off
            Assert.AreEqual(StoryDensity.Off, StoryDirectorView.Density);
            int started = d.Director.Started;
            yield return new WaitForSecondsRealtime(StoryRules.CheckSeconds * 3f);
            Assert.AreEqual(started, d.Director.Started, "off starts nothing");
            StringAssert.Contains("Off", d.Status());
            AssertNoErrors("the director's stories");
        }
    }
}
