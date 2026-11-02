using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#197 on the real scene, in a Village and a Town: a run-in stops on the path, faces, plays Chat or Argue and walks on with no
    /// jump or slot glide; a doll walking over leaves its path, stands the beat and walks back into its day; midday and evening spread.</summary>
    [Category("RunIns")]
    public class RunInPlayTests : PlayTestBase
    {
        const int Seed = 1234;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static PeopleView People => PeopleView.Find();

        static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator ToEra(Era era)
        {
            yield return OpenSlice(1080, 1920, Seed);
            ShowMeDirector.BubblesOn = false;
            StringAssert.Contains("the town waits", MomentPlayer.DebugGrowVillage(30f));
            yield return Frames(3);
            if (era == Era.Town) { Sim.ForceNextEra(); yield return Frames(3); SimLayoutSource.DebugSkipYears(10f); yield return Frames(3); }
            Assert.AreEqual(era, Sim.State.era);
            Sim.CleanAll();
            yield return Frames(4);
        }

        struct Sample { public float t, speed, step, gap, facing; public ActivityKind kind; public bool enRoute, moving; public Vector2 at; }

        /// <summary>Watches one person through <paramref name="seconds"/> real seconds, each frame.</summary>
        IEnumerator Watch(PersonRef who, PersonRef with, float seconds, List<Sample> into)
        {
            var pv = People;
            Vector2 last = default;
            bool has = false;
            float start = Time.realtimeSinceStartup, prevT = start;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
                float now = Time.realtimeSinceStartup, dt = Mathf.Max(1e-4f, now - prevT);
                prevT = now;
                if (!pv.TryGetDollPosition(who, out var p)) continue;
                var a = pv.Activities.FirstOrDefault(x => x.who == who);
                pv.TryGetDollPosition(with, out var q);
                pv.TryGetDollFacing(who, out var f);
                var to = q - p;
                into.Add(new Sample
                {
                    t = now - start, speed = has ? (p - last).magnitude / dt : 0f, step = has ? (p - last).magnitude : 0f, gap = to.magnitude,
                    facing = to.sqrMagnitude > 1e-6f ? Vector2.Dot(f.normalized, to.normalized) : -2f,
                    kind = a.kind, enRoute = a.enRoute, moving = a.moving, at = p,
                });
                last = p; has = true;
            }
        }

        static string Sum(List<Sample> s) => $"max speed {s.Max(x => x.speed):0.00} (walk {LifePlanner.RealWalkSpeed:0.00}), kinds {string.Join(",", s.Select(x => x.kind).Distinct())}, path {s.First().at} to {s.Last().at}";

        /// <summary>The beat as played, with both dolls.</summary>
        IEnumerator AuditBeat(string era, LifePlanner.RunInBeat stop, LifePlanner.RunInBeat? go)
        {
            var pv = People;
            float lead = 2.5f / LifeClock.RealSecondsPerViewHour(stop.start);
            float from = (go.HasValue ? Mathf.Min(stop.start, go.Value.start) : stop.start) - lead;
            PeopleView.DebugSetHour(from);
            yield return Wait(0.6f); // the plan settles after a jump: the first samples aren't the walk
            float real = LifeClock.RealSecondsBetween(pv.Now.hour, stop.end) + 3f;
            var a = new List<Sample>(); var b = new List<Sample>();
            var ra = StartWatch(stop.who, stop.with, real, a);
            var rb = StartWatch(stop.with, stop.who, real, b);
            yield return new WaitForSecondsRealtime(real + 0.1f);
            Debug.Log($"[runin] {era} {stop.kind} {stop.who} stops at {stop.start:0.00}-{stop.end:0.00}: {Sum(a)}");
            Debug.Log($"[runin] {era} {stop.with}{(go.HasValue ? " walks over" : " walks too")}: {Sum(b)}");
            var mid = a.Where(s => s.kind == stop.kind).ToList();
            Assert.IsNotEmpty(mid, $"{stop.who} plays {stop.kind}");
            Assert.IsTrue(mid.All(s => s.enRoute), "stopped on the route, not at a slot");
            Assert.Less(a.Skip(2).Max(s => s.step), 0.1f, "the stopper never jumps");
            Assert.Less(b.Skip(2).Max(s => s.step), 0.1f, "nor does the other");
            Assert.Less(a.Skip(2).Max(s => s.speed), 2.2f, "or runs");
            Assert.Less(b.Skip(2).Max(s => s.speed), 2.2f, "or runs");
            var settled = mid.Skip(mid.Count / 2).ToList();
            float drift = (settled.First().at - settled.Last().at).magnitude;
            Assert.Less(drift, 0.05f, "and stands still for the beat, no slot glide (the walk's crowd offset easing off is a few hundredths)");
            var both = settled.Where(s => s.gap < 0.6f).ToList();
            Assert.IsNotEmpty(both, $"the two meet (gap {settled.Min(s => s.gap):0.00})");
            Assert.Greater(both.Min(s => s.gap), 0.05f, "not on one spot");
            Assert.Greater(both.Average(s => s.facing), 0.8f, "they face each other");
        }

        readonly List<Coroutine> watchers = new();
        Coroutine StartWatch(PersonRef who, PersonRef with, float seconds, List<Sample> into) { var c = TestRunner().StartCoroutine(Watch(who, with, seconds, into)); watchers.Add(c); return c; }
        static MonoBehaviour TestRunner() => People;

        IEnumerator Audit(Era era)
        {
            yield return ToEra(era);
            var pv = People;
            string tag = era.ToString();
            int chats = 0, bickers = 0, over = 0, stands = 0;
            long day = pv.Now.day;
            int walkersWanted = 2, plainWanted = 1, days = 0;
            for (int tries = 0; tries < 60 && (walkersWanted > 0 || plainWanted > 0); tries++)
            {
                PeopleView.DebugSetHour(6f); // a jump lands on the sim's month: the day the list is read for
                yield return Frames(2);
                var beats = pv.Planner.RunInBeatsOn(pv.Now.day).Where(b => b.kind != ActivityKind.Wave && b.start < 19f).ToList();
                days++;
                if (beats.Count > 0) Debug.Log($"[runin] {tag} day {pv.Now.day}: {string.Join(" | ", beats.Select(b => $"{b.kind} {b.who}>{b.with} {b.start:0.00} over {b.walksOver}"))}");
                foreach (var stop in beats.Where(b => !b.walksOver))
                {
                    var go = beats.Where(b => b.who == stop.with && b.with == stop.who && b.walksOver).Select(b => (LifePlanner.RunInBeat?)b).FirstOrDefault();
                    if (go.HasValue ? walkersWanted <= 0 : plainWanted <= 0) continue;
                    yield return AuditBeat(tag, stop, go);
                    if (stop.kind == ActivityKind.Chat) chats++; else bickers++;
                    if (go.HasValue) { over++; walkersWanted--; } else { stands++; plainWanted--; }
                }
                SimLayoutSource.DebugSkipYears(1f / 12f);
                Sim.CleanAll();
                yield return Frames(2);
            }
            Debug.Log($"[runin] {tag}: looked at {days} days");
            Assert.Greater(over + stands, 0, "the days have run-ins");
            Debug.Log($"[runin] {tag}: audited {chats} chats, {bickers} bickers, {over} walked over, {stands} met walking");
            AssertNoErrors(tag + " run-ins");
        }

        [UnityTest, Timeout(300000)] public IEnumerator AVillagesRunInsPlayClean() => Audit(Era.Village);
        [UnityTest, Timeout(300000)] public IEnumerator ATownsRunInsPlayClean() => Audit(Era.Town);

        IEnumerator Force(Era era, bool bicker)
        {
            yield return ToEra(era);
            PeopleView.DebugSetHour(10f);
            yield return Wait(0.6f);
            var pv = People;
            string said = PeopleView.DebugRunIn(bicker);
            Debug.Log("[runin] forced " + said);
            StringAssert.Contains("walks over", said);
            var beats = pv.Planner.RunInBeatsOn(pv.Now.day);
            var go = beats.Where(b => b.walksOver).OrderByDescending(b => b.start).First();
            var want = bicker ? ActivityKind.Argue : ActivityKind.Chat;
            Assert.AreEqual(want, go.kind);
            yield return Wait(0.5f);
            Assert.AreEqual(want, pv.Activities.First(a => a.who == go.with).kind, "the stopper's beat started at once");
            AssertNoErrors("forced beat");
        }

        [UnityTest, Timeout(120000)] public IEnumerator TheDebugPanelForcesABicker() => Force(Era.Town, true);
        [UnityTest, Timeout(120000)] public IEnumerator TheDebugPanelForcesAChat() => Force(Era.Village, false);

        static string Spread(PeopleView pv) => string.Join(", ", pv.Activities.Where(a => a.kind is not (ActivityKind.None or ActivityKind.Sleep))
            .GroupBy(a => a.place.kind).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()} ({string.Join("/", g.Select(x => x.kind).Distinct().Take(4))})"));

        /// <summary>Midday and evening spread, and the tavern at 19:00, across a few days. Asserts the box isn't all in one place.</summary>
        IEnumerator SpreadOf(Era era)
        {
            yield return ToEra(era);
            var pv = People;
            for (int d = 0; d < 3; d++)
            {
                foreach (float hour in new[] { 12.2f, 19f })
                {
                    PeopleView.DebugSetHour(hour);
                    yield return Wait(0.5f);
                    var up = pv.Activities.Where(a => a.kind is not (ActivityKind.None or ActivityKind.Sleep)).ToList();
                    int most = up.Count == 0 ? 0 : up.GroupBy(a => a.place).Max(g => g.Count()); // a family's own door is not a crowd: homes one by one
                    int inn = pv.Activities.Count(a => a.kind == ActivityKind.Drink);
                    Debug.Log($"[spread] {era} day {d} {hour:0.0}: {up.Count} up, {Spread(pv)}, drinking {inn}");
                    if (!up.Any(a => a.kind == ActivityKind.Dance)) // a feast day has the whole box on the green, on purpose
                        Assert.Less(most, Mathf.Max(5, Mathf.CeilToInt(up.Count * 0.85f)), "not everyone in one place");
                    if (d == 0) yield return Shot($"bundle2-197-{era}-{(hour > 15f ? "evening" : "midday")}");
                }
                SimLayoutSource.DebugSkipYears(0.09f);
                yield return Frames(3);
            }
            AssertNoErrors("spread");
        }

        [UnityTest, Timeout(120000)] public IEnumerator AVillageSpreads() => SpreadOf(Era.Village);
        [UnityTest, Timeout(120000)] public IEnumerator ATownSpreads() => SpreadOf(Era.Town);
    }
}
