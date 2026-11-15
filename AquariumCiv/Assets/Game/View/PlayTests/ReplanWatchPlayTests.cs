using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#697/#696 watched in play mode: a real camp a good many years on, replans natural (the sim's own months) and forced (supper,
    /// building and the rest), every doll sampled every frame. By day: no frame where three or more walkers turn round at once.
    /// By night: nobody who is asleep gets up, and no sleeper's doll shows, until morning.</summary>
    [Explicit("Watched run: about 3 minutes each, screenshots for the eye"), Category("ReplanWatch")]
    public class ReplanWatchPlayTests : PlayTestBase
    {
        const string Out = "F:/GameDev-wt/pt1b/AquariumCiv/Temp/agent/697/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();

        static IEnumerator Snap(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Out + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        IEnumerator Grown()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 1234);
            SimLayoutSource.DebugSkipYears(16f);
            yield return null;
            Sim.CleanAll();
            People?.Planner?.ClearLive(); foreach (var bd in Sim.State.buildings) People.Planner.ForgetTent(bd.id); // a raising begun in the daytime of the skipped years is carried by the clock jump to night: not what is watched // a staged moment left over from the skipped years would pull a sleeper out at night: not what these tests are about
            yield return Frames(3);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TenGameMinutesByDayWithReplansNeverTurnThreeWalkersRoundAtOnce()
        {
            yield return Grown();
            var pv = People;
            var lines = new List<string>();
            pv.Planner.ReplanLog = l => lines.Add($"{pv.Now} {l}");
            pv.Planner.ClearLive(); foreach (var bd in Sim.State.buildings) pv.Planner.ForgetTent(bd.id);
            PeopleView.DebugSetHour(7f);
            pv.timeScale = 4f; // 10 game minutes = 600 s of the view clock = 150 s here
            yield return Frames(4);
            var prev = new Dictionary<int, PersonActivity>();
            float t0 = Time.realtimeSinceStartup, nextForce = t0 + 30f, nextShot = t0 + 5f;
            int force = 0, worst = 0, shots = 0, frames = 0, totalRev = 0;
            long day0 = pv.Now.day;
            string[] forced = { "supper", "building", "month", "chronicle", "population" };
            while (Time.realtimeSinceStartup - t0 < 150f)
            {
                if (Time.realtimeSinceStartup >= nextForce) { nextForce += 30f; PeopleView.DebugReplan(forced[force++ % forced.Length]); }
                yield return null;
                frames++;
                if (pv.Now.hour > 19.5f || pv.Now.day != day0) { PeopleView.DebugSetHour(7f); day0 = pv.Now.day; prev.Clear(); continue; } // stay in the daylight
                int rev = 0;
                foreach (var a in pv.Activities)
                {
                    if (prev.TryGetValue(a.who.Key, out var b) && b.moving && !b.enRoute && !a.enRoute && b.end > pv.Now.hour + 0.02f && !(a.moving && a.place == b.place)) rev++;
                    prev[a.who.Key] = a;
                }
                totalRev += rev; worst = Mathf.Max(worst, rev);
                Assert.Less(rev, 3, $"{rev} walkers turned round in one frame at {pv.Now}; replans: {string.Join(" / ", lines.TakeLast(3))}");
                if (shots < 3 && Time.realtimeSinceStartup >= nextShot) { nextShot += 50f; yield return Snap($"day-{++shots}"); }
            }
            File.WriteAllLines(Out + "day-replans.txt", lines);
            TestContext.WriteLine($"frames {frames}, replans logged {lines.Count}, forced {force}, worst reversals in one frame {worst}, total {totalRev}, shots {shots}");
            Assert.GreaterOrEqual(lines.Count, force, "the forced replans were logged");
            pv.timeScale = 1f;
            AssertNoErrors("ten game minutes by day");
            CloseSlice();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ANightWithEveryReplanTriggerKeepsEverySleeperInTheTent()
        {
            yield return Grown();
            var pv = People;
            var lines = new List<string>();
            pv.Planner.ReplanLog = l => lines.Add($"{pv.Now} {l}");
            pv.Planner.ClearLive(); foreach (var bd in Sim.State.buildings) pv.Planner.ForgetTent(bd.id);
            ShowMeDirector.BubblesOn = false; // a line said at night brings its speaker out of the tent on purpose (the caption's ring): not what is watched
            PeopleView.DebugSetHour(20f);
            pv.timeScale = 0.5f;
            yield return Frames(4);
            var asleepSince = new HashSet<int>(); var hiddenOnce = new HashSet<int>(); var shown = new Dictionary<int, List<string>>(); // a doll still walking the last steps into the tent is shown while the plan already says asleep; once it was hidden it must stay so
            var trig = LifePlanner.ReplanTriggers.Where(t => t != "month").ToArray(); // a month forced every 2 s runs the sim days ahead of the view, and a view day taken at once is a new plan on purpose (a catch-up): the month has its own cases in ReplanKeepsPlansPlayTests
            int next = 0, shots = 0, frames = 0, visibleFrames = 0;
            float t0 = Time.realtimeSinceStartup, nextForce = t0 + 4f;
            while (pv.Now.hour >= 19.9f && pv.Now.hour < 23.3f && Time.realtimeSinceStartup - t0 < 240f)
            {
                if (Time.realtimeSinceStartup >= nextForce)
                {
                    nextForce += 2.5f;
                    if (shots == 0 && asleepSince.Count > 3) { yield return Snap("night-before-replan"); shots++; }
                    PeopleView.DebugReplan(trig[next++ % trig.Length]);
                    yield return null;
                    if (shots == 1) { yield return Snap("night-after-replan"); shots++; }
                }
                yield return null;
                frames++;
                foreach (var a in pv.Activities)
                {
                    int k = a.who.Key;
                    bool asleep = a.kind == ActivityKind.Sleep && !a.moving;
                    if (asleep) asleepSince.Add(k);
                    else if (asleepSince.Contains(k))
                        Assert.Fail($"person {k} got up ({a.kind}, moving {a.moving}) at {pv.Now} after going to bed; replans: {string.Join(" / ", lines.TakeLast(3))}");
                    if (asleep && !pv.TryGetDollTop(a.who, out _)) hiddenOnce.Add(k);
                    else if (asleep && hiddenOnce.Contains(k) && !(ShowMeDirector.Instance != null && ShowMeDirector.Instance.CaptionUp)) // a caption line brings its speaker out on purpose (ring under them): own question, noted in the report
                    {
                        visibleFrames++;
                        if (visibleFrames == 1) { pv.TryGetDollPose(a.who, out var pz); Debug.Log($"WATCH-FIRST pose hidden {pz.hidden} lying {pz.lying} kind {a.kind} src {a.source} slot {a.slot} start {a.start:0.00} end {a.end:0.00}"); pv.timeScale = 0f; yield return Snap("night-sleeper-shown"); pv.timeScale = 0.5f; }
                        pv.TryGetDollPosition(a.who, out var dp);
                        if (!shown.TryGetValue(k, out var sh)) shown[k] = sh = new List<string>();
                        if (sh.Count < 3) sh.Add($"{pv.Now} doll at {dp} place {a.place.kind} frames-so-far {visibleFrames} last replans: {string.Join(" / ", lines.TakeLast(1))}");
                    }
                }
            }
            File.WriteAllLines(Out + "night-replans.txt", lines);
            TestContext.WriteLine($"frames {frames}, slept {asleepSince.Count}, replans logged {lines.Count}, forced {next}, reached {pv.Now}, screenshots {shots}");
            Debug.Log($"WATCH-NIGHT visible-while-asleep frames {visibleFrames}: " + string.Join(" || ", shown.Select(kv => kv.Key + ": " + string.Join("; ", kv.Value))));
            Assert.AreEqual(0, visibleFrames, "a sleeper was shown again: " + string.Join(" || ", shown.Select(kv => kv.Key + ": " + string.Join("; ", kv.Value))));
            Assert.Greater(asleepSince.Count, 3, "people went to bed");
            Assert.GreaterOrEqual(next, 9,  "every trigger was forced at least once");
            pv.timeScale = 1f;
            AssertNoErrors("a night of replans");
            CloseSlice();
        }
    }
}
