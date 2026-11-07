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

        IEnumerator Box(StoryDensity density, float hour = 9f)
        {
            StoryDirectorView.Density = density;
            yield return OpenSlice(seed: Seed);
            // Batch 3: the skip would queue staged moments (#370: a wedding, a new chief) whose live blocks take the adults, and the
            // moment director would play them one after another. This test watches the director's background stories: no moments.
            momentsWere = GameClock.Instance.config.moments;
            GameClock.Instance.config.moments = false;
            SimLayoutSource.DebugSkipYears(3);
            yield return null;
            PeopleView.DebugSetHour(hour);
            yield return Frames(10);
            // Anything already playing stops, until none starts for 2 s (as HuntViewPlayTests does).
            for (int calm = 0, f = 0; calm < 120 && f < 1200; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
            PeopleView.DebugSetHour(hour);
            yield return Frames(10);
            Assert.IsNotNull(StoryDirectorView.Instance, "the director is in the scene (bootstrap)");
            StoryDirectorView.Instance.Director?.Reset(); // the look starts from here, not from before the moments were stopped
        }

        bool momentsWere = true;

        public override void TearDown()
        {
            StoryDirectorView.Density = StoryDensity.Normal;
            if (GameClock.Instance != null) GameClock.Instance.config.moments = momentsWere;
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
            // Batch 3: at 9 h every one of this tribe's six works now (fishing #363, knapping, the hunt #357, the pot #365, gathering):
            // nobody is free to borrow, rightly. The fire at 18.5 h has them sitting together: a hand-over there (B11's evening).
            yield return Box(StoryDensity.High, 18.5f);
            var d = StoryDirectorView.Instance;
            var pv = PeopleView.Find();
            yield return Until(() => d.Director != null && d.Director.Started > 0, 30f);
            Assert.Greater(d.Director?.Started ?? 0, 0, "the director started a story (pop " + Sim.State.population + "): " + d.Status()
                + $"; staged moments playing {StagedMoments.Playing}; people: " + string.Join(", ", System.Linq.Enumerable.Select(pv.Activities, a => $"{a.who}:{a.kind}/{a.source}{(a.moving ? "/moving" : "")}")));
            var plan = d.LastPlan;
            // A kids' game walks in for its lead first; a hand-over is a few seconds in all (approach + the beat): look at it at once.
            yield return new WaitForSecondsRealtime(StoryRules.IsKidGame(plan.kind) ? KidPlayDirector.DirectedLead + 1.5f : 0.3f);
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
