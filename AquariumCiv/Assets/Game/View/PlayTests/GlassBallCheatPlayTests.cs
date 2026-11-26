using System.Collections;
using System.Collections.Generic;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#772: the debug cheat "Ball at the glass" must show a missed ball meeting the pane and coming back, every time.</summary>
    [Category("GlassBallCheat")]
    public class GlassBallCheatPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.ActiveSim;

        [UnityTest, Timeout(300000)]
        public IEnumerator BallAtTheGlassCheat_ABallOfTheCheatsGameMeetsThePaneAndComesBack_NeverLeavesTheBox()
        {
            yield return OpenSlice(seed: 1234);
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            var born = new List<Notable>();
            for (int i = 0; i < 4; i++) { var n = Sim.DebugBirth(); Assert.IsNotNull(n, "a grown-up to be a parent"); born.Add(n); }
            SimLayoutSource.DebugSkipYears(0.1f);
            yield return null;
            foreach (var n in born) n.age = 7f;
            var pv = PeopleView.Find();
            pv.Planner.Invalidate();
            Sim.CleanAll();
            PeopleView.DebugSetHour(11f);
            yield return Frames(4);
            CampLife.OnlyCheats = true;
            try
            {
                pv.Camp.CutForCheat();
                string r = KidPlayDirector.DebugBallAtGlass();
                Assert.IsTrue(r.StartsWith("Ball"), r);
                float maxX = -9f, last = -9f; bool turned = false, seen = false;
                float x0 = GlassBox.HalfX - 0.1f;
                float end = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < end && !turned)
                {
                    var ball = KidPlayDirector.DebugForcedBall();
                    if (ball != null)
                    {
                        seen = true;
                        var l = ball.transform.localPosition;
                        Assert.LessOrEqual(l.x, GlassBox.HalfX - BallGame.BallR + 0.02f, "the ball is past the right pane");
                        Assert.GreaterOrEqual(l.x, -GlassBox.HalfX, "past the left pane");
                        Assert.GreaterOrEqual(l.z, GlassBox.ZMin, "past the back pane"); Assert.LessOrEqual(l.z, GlassBox.ZMax, "past the front pane");
                        if (l.x > maxX) maxX = l.x;
                        if (maxX >= x0 && l.x < last - 0.005f && l.x < maxX - 0.01f) turned = true;
                        last = l.x;
                    }
                    yield return null;
                }
                Assert.IsTrue(seen, "the cheat's ball was drawn");
                Assert.GreaterOrEqual(maxX, x0, $"the ball came within 0.1 of the pane (furthest x {maxX:0.000}, pane {GlassBox.HalfX:0.000})");
                Assert.IsTrue(turned, "the ball turned back off the glass");
            }
            finally { CampLife.OnlyCheats = false; }
        }
    }
}
