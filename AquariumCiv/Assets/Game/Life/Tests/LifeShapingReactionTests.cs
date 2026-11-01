using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#142: the people react to a gift within seconds: someone awake looks up, points or walks over; sleepers stay
    /// asleep; at night only someone already up reacts, from where they stand, or it waits until people get up. Staging only:
    /// the planner never touches the sim, so the credited answer, its timing and its line stay as they were.</summary>
    public class LifeShapingReactionTests
    {
        static readonly (string kind, bool fish)[] CampTools =
        {
            (SimCore.NudgeKind.Rain, false), (SimCore.NudgeKind.Grove, false), (SimCore.NudgeKind.Herd, false), (SimCore.NudgeKind.Herd, true),
        };

        static readonly int[] Seeds = { 1, 2, 6, 7, 10, 16, 18, 33, 1234 };

        static SimCore Camp(int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        /// <summary>A gift just landed: not answered yet.</summary>
        static Nudge Gift(SimCore sim, string kind, bool fish)
        {
            var spec = sim.Settlement.Map.spec;
            float x = 1.2f, z = 0.6f;
            if (fish) { x = spec.pondX; z = spec.pondZ; }
            else sim.Settlement.OpenSpotNear(ref x, ref z, 0.3f);
            var n = sim.DebugShape(kind, x, z);
            Assert.IsNotNull(n, kind);
            return n;
        }

        static string Tag(string kind, bool fish, int seed, float hour) => $"{kind}{(fish ? " (fish)" : "")} seed {seed} at {hour}h";

        static IEnumerable<float> DayHours() { for (float h = 7f; h < 20f; h += 0.5f) yield return h; }

        /// <summary>Everyone's plan, for comparing with a run that had no reaction.</summary>
        static Dictionary<int, PersonActivity> Plans(LifePlanner planner, ViewTime t)
        {
            var acts = new List<PersonActivity>();
            planner.PlanAll(t, acts);
            return acts.ToDictionary(a => a.who.Key);
        }

        [Test]
        public void EachCampToolGetsAReactionWithinTenSecondsByDay()
        {
            int shown = 0, asleep = 0;
            float slowest = 0f;
            foreach (int seed in Seeds)
                foreach (var (kind, fish) in CampTools)
                {
                    var sim = Camp(seed);
                    var n = Gift(sim, kind, fish);
                    string before = JsonUtility.ToJson(sim.State);
                    var planner = LifePlanner.Wired(sim);
                    // A week of days, so a rest day is in it too.
                    for (long day = sim.State.month; day < sim.State.month + LifeClock.RestDayEvery; day++)
                        foreach (float hour in DayHours())
                        {
                            planner.ClearLive();
                            string tag = Tag(kind, fish, seed, hour) + $" day {day}";
                            var at = new ViewTime(day, hour);
                            bool up = Plans(planner, at).Values.Any(a => !ShapingReactions.Asleep(a.kind) && planner.StageOf(a.who) != LifeStage.Baby);
                            var info = planner.StartShapingReaction(n.id, at);
                            if (!up) { asleep++; Assert.Greater(hour, LifeClock.Evening, $"{tag}: only a winter evening's early bed"); continue; }
                            Assert.IsNotNull(info, $"{tag}: someone reacts");
                            Assert.AreEqual(n.id, info.nudgeId);
                            Assert.AreEqual(Place.Land(PlaceKind.Nudge, n.id), info.spot);
                            float first = -1f;
                            for (float s = 0f; s <= ShapingReactions.WithinSeconds && first < 0f; s += 0.5f)
                                foreach (var a in Plans(planner, LifeClock.Advance(at, s)).Values)
                                    if (a.source == ActivitySource.Scene && info.cast.Any(c => c.who == a.who)) { first = s; break; }
                            Assert.GreaterOrEqual(first, 0f, $"{tag}: a visible reaction within {ShapingReactions.WithinSeconds} s");
                            slowest = Math.Max(slowest, first);
                            shown++;
                            Assert.AreEqual(0, planner.FallbackCount, tag);
                        }
                    Assert.AreEqual(before, JsonUtility.ToJson(sim.State), "the planner never writes the sim");
                }
            TestContext.WriteLine($"{shown} reactions; the slowest was seen after {slowest:0.0} s; {asleep} times the whole box had gone to bed");
            Assert.Greater(shown, 1000);
        }

        /// <summary>What was missing in the first playthrough (#139): somebody goes and has a look, not just the ones who
        /// happened to stand near. Every tool, at the gift, doing what that tool calls for.</summary>
        [Test]
        public void ByDaySomeoneWalksOverForALook()
        {
            int reactions = 0, cameOver = 0;
            foreach (int seed in Seeds)
                foreach (var (kind, fish) in CampTools)
                    foreach (float hour in new[] { 8f, 10f, 14f, 16f })
                    {
                        var sim = Camp(seed);
                        var n = Gift(sim, kind, fish);
                        var planner = LifePlanner.Wired(sim);
                        long day = sim.State.month;
                        var info = planner.StartShapingReaction(n.id, new ViewTime(day, hour));
                        string tag = Tag(kind, fish, seed, hour);
                        Assert.IsNotNull(info, tag);
                        reactions++;
                        var walkers = info.cast.Where(c => c.role != RelicCast.Onlookers).ToList();
                        if (walkers.Count == 0) { TestContext.WriteLine($"{tag}: nobody near enough"); continue; }
                        cameOver++;
                        foreach (var c in walkers)
                        {
                            var last = planner.Plan(c.who, new ViewTime(day, info.end - 0.01f));
                            Assert.AreEqual(info.spot, last.place, $"{tag}: {c.who} at the gift");
                            Assert.IsFalse(last.moving, $"{tag}: {c.who} arrived");
                            var (grown, kids) = ShapingReactions.AtGift(kind);
                            Assert.AreEqual(c.role == RelicCast.Kids ? kids : grown, last.kind, tag);
                        }
                    }
            TestContext.WriteLine($"someone came over in {cameOver} of {reactions} reactions");
            Assert.AreEqual(reactions, cameOver, "by day someone always comes over");
        }

        [Test]
        public void TheFarOnesLookFromWhereTheyStand()
        {
            int onlookers = 0;
            foreach (int seed in Seeds)
            {
                var sim = Camp(seed);
                var n = Gift(sim, SimCore.NudgeKind.Grove, false);
                var planner = LifePlanner.Wired(sim);
                long day = sim.State.month;
                var info = planner.StartShapingReaction(n.id, new ViewTime(day, 10f));
                foreach (var c in info.cast.Where(c => c.role == RelicCast.Onlookers))
                {
                    onlookers++;
                    var a = planner.Plan(c.who, new ViewTime(day, 10.02f));
                    Assert.AreEqual(ActivitySource.Scene, a.source);
                    Assert.AreNotEqual(info.spot, a.place, "they stay where they are");
                }
                Assert.LessOrEqual(info.cast.Count, 1 + 2 + 2 + 3, "a few, not the whole box");
            }
            Assert.Greater(onlookers, 0);
        }

        [Test]
        public void NobodyAsleepIsWoken()
        {
            int sleepers = 0, watched = 0;
            foreach (int seed in Seeds)
                foreach (var (kind, fish) in CampTools)
                    foreach (float hour in new[] { 20.5f, 21.5f, 22.5f, 23.5f, 1f, 3f, 4.5f, 5.5f, 6.5f, 9f, 13f })
                    {
                        var sim = Camp(seed);
                        var n = Gift(sim, kind, fish);
                        var planner = LifePlanner.Wired(sim);
                        var plain = LifePlanner.Wired(sim);
                        var at = new ViewTime(sim.State.month, hour);
                        var asleep = Plans(plain, at).Values.Where(a => ShapingReactions.Asleep(a.kind)).Select(a => a.who).ToList();
                        var info = planner.StartShapingReaction(n.id, at);
                        string tag = Tag(kind, fish, seed, hour);
                        if (info != null)
                            foreach (var c in info.cast)
                                Assert.IsFalse(asleep.Contains(c.who), $"{tag}: {c.who} was asleep");
                        for (float s = 0f; s <= ShapingReactions.Seconds; s += 2f)
                        {
                            var t = LifeClock.Advance(at, s);
                            foreach (var who in asleep)
                            {
                                var a = planner.Plan(who, t);
                                var b = plain.Plan(who, t);
                                Assert.AreEqual(b.kind, a.kind, $"{tag}: {who}");
                                Assert.AreEqual(b.place, a.place, $"{tag}: {who}");
                                watched++;
                            }
                        }
                        sleepers += asleep.Count;
                    }
            TestContext.WriteLine($"{sleepers} sleepers, {watched} samples");
            Assert.Greater(sleepers, 100);
        }

        /// <summary>After dark nobody walks over (as with the answers): whoever is up reacts where they stand.</summary>
        [Test]
        public void AtNightNobodyWalksOver()
        {
            int reactions = 0;
            foreach (int seed in Seeds)
                foreach (float hour in new[] { 20f, 20.5f, 21f, 22f, 23f, 0.5f, 2f, 4f })
                {
                    var sim = Camp(seed);
                    var n = Gift(sim, SimCore.NudgeKind.Herd, false);
                    var planner = LifePlanner.Wired(sim);
                    var info = planner.StartShapingReaction(n.id, new ViewTime(sim.State.month, hour));
                    if (info == null) continue;
                    reactions++;
                    Assert.IsTrue(info.cast.All(c => c.role == RelicCast.Onlookers), $"seed {seed} at {hour}h");
                }
            TestContext.WriteLine($"{reactions} night reactions");
            Assert.Greater(reactions, 0, "someone is still up in the evening");
        }

        /// <summary>The view's loop (phase 2): the gift lands in the small hours, the view keeps asking, and the first one up
        /// reacts, once. With the whole box asleep it has to wait.</summary>
        [Test]
        public void AtNightItWaitsUntilPeopleGetUp()
        {
            int waited = 0;
            float latest = 0f;
            foreach (int seed in Seeds)
                foreach (var (kind, fish) in CampTools)
                {
                    var sim = Camp(seed);
                    var n = Gift(sim, kind, fish);
                    var planner = LifePlanner.Wired(sim);
                    long day = sim.State.month;
                    RelicMomentInfo info = null;
                    float hour = 1f;
                    for (; hour < 12f; hour += 0.05f)
                    {
                        var t = new ViewTime(day, hour);
                        bool up = Plans(planner, t).Values.Any(a => !ShapingReactions.Asleep(a.kind) && planner.StageOf(a.who) != LifeStage.Baby);
                        info = planner.StartShapingReaction(n.id, t);
                        Assert.AreEqual(up, info != null, $"{Tag(kind, fish, seed, hour)}: it starts the moment someone is up, not before");
                        if (info != null) break;
                        waited++;
                    }
                    Assert.IsNotNull(info, Tag(kind, fish, seed, hour));
                    latest = Math.Max(latest, hour);
                    var again = new ViewTime(day, hour + 0.02f);
                    Assert.AreEqual(info.start, planner.StartShapingReaction(n.id, again).start, "asked again, it's the same one");
                    var moments = new List<RelicMomentInfo>();
                    Assert.AreEqual(1, planner.ActiveShapingReactions(again, moments));
                }
            TestContext.WriteLine($"{waited} frames waited; the latest start was {latest:0.00}h");
            Assert.Greater(waited, 0, "the camp does sleep");
            Assert.Less(latest, LifeClock.WorkStart + 1f, "people are up by the morning");
        }

        [Test]
        public void TheLastMinutesOfADayWaitForTheNext()
        {
            foreach (int seed in Seeds)
            {
                var sim = Camp(seed);
                var n = Gift(sim, SimCore.NudgeKind.Rain, false);
                var planner = LifePlanner.Wired(sim);
                Assert.IsNull(planner.StartShapingReaction(n.id, new ViewTime(sim.State.month, 23.99f)), "no blink of a reaction");
            }
        }

        [Test]
        public void NoReactionToAnAnsweredOrUnknownGift()
        {
            var sim = Camp(2);
            var n = Gift(sim, SimCore.NudgeKind.Rain, false);
            var planner = LifePlanner.Wired(sim);
            var at = new ViewTime(sim.State.month, 10f);
            Assert.IsNull(planner.StartShapingReaction(-5, at));
            Assert.IsTrue(sim.DebugSkipToAnswer(n.id));
            Assert.IsNull(planner.StartShapingReaction(n.id, at), "the answer is the reaction now");
        }

        /// <summary>The Keeper can take a gift back while they look at it: its place is gone, so the reaction ends.</summary>
        [Test]
        public void AGiftTakenBackMidReactionEndsIt()
        {
            var sim = Camp(3);
            var n = Gift(sim, SimCore.NudgeKind.Grove, false);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            var info = planner.StartShapingReaction(n.id, new ViewTime(day, 10f));
            Assert.IsNotNull(info);
            var t = new ViewTime(day, 10.2f);
            var moments = new List<RelicMomentInfo>();
            Assert.AreEqual(1, planner.ActiveShapingReactions(t, moments));
            Assert.IsTrue(sim.TakeBack(n.id));
            var acts = new List<PersonActivity>();
            planner.PlanAll(t, acts);
            Assert.AreEqual(0, planner.ActiveShapingReactions(t, moments));
            Assert.IsFalse(acts.Any(a => a.place.kind == PlaceKind.Nudge));
            Assert.AreEqual(0, planner.FallbackCount);
        }

        /// <summary>Those in an answer moment keep playing it: a new gift doesn't pull them away.</summary>
        [Test]
        public void NobodyIsPulledOutOfAMoment()
        {
            int checkedCast = 0;
            foreach (int seed in Seeds)
            {
                var sim = Camp(seed);
                var answered = Gift(sim, SimCore.NudgeKind.Herd, false);
                Assert.IsTrue(sim.DebugSkipToAnswer(answered.id));
                sim.Settlement.PlacePending();
                var n = Gift(sim, SimCore.NudgeKind.Rain, false);
                var planner = LifePlanner.Wired(sim);
                long day = sim.State.month;
                var answer = planner.StartShapingAnswer(answered.id, new ViewTime(day, 10f));
                if (answer == null) continue;
                var info = planner.StartShapingReaction(n.id, new ViewTime(day, 10.1f));
                if (info == null) continue;
                foreach (var c in info.cast)
                {
                    checkedCast++;
                    Assert.IsFalse(answer.cast.Any(x => x.who == c.who), $"seed {seed}: {c.who} is in the answer");
                }
            }
            Assert.Greater(checkedCast, 0);
        }

        [Test]
        public void TwoRunsReactTheSame()
        {
            foreach (int seed in Seeds)
                foreach (var (kind, fish) in CampTools)
                {
                    string Run()
                    {
                        var sim = Camp(seed);
                        var n = Gift(sim, kind, fish);
                        var planner = LifePlanner.Wired(sim);
                        long day = sim.State.month;
                        var info = planner.StartShapingReaction(n.id, new ViewTime(day, 11f));
                        var lines = new List<string> { $"{info.start} {info.end} {info.spot}" };
                        lines.AddRange(info.cast.Select(c => $"{c.who} {c.role}"));
                        var acts = new List<PersonActivity>();
                        for (float h = 11f; h < info.end + 0.5f; h += 0.05f)
                        {
                            planner.PlanAll(new ViewTime(day, h), acts);
                            foreach (var a in acts)
                            {
                                planner.PositionAt(a.who, new ViewTime(day, h), out float x, out float z);
                                lines.Add($"{a.who} {a.kind} {a.place} {x} {z}");
                            }
                        }
                        return string.Join("\n", lines);
                    }
                    Assert.AreEqual(Run(), Run(), Tag(kind, fish, seed, 11f));
                }
        }

        /// <summary>Out of scope and staying that way: the same box, watched and reacting or not, answers the same gift in the
        /// same month with the same line, and every other thing in the sim is the same too.</summary>
        [Test]
        public void TheAnswerItsTimingAndItsLineStayTheSame()
        {
            int answers = 0;
            foreach (int seed in new[] { 1, 7, 1234 })
                foreach (var (kind, fish) in CampTools)
                {
                    var quiet = Camp(seed);
                    var watched = Camp(seed);
                    var nq = Gift(quiet, kind, fish);
                    var nw = Gift(watched, kind, fish);
                    var planner = LifePlanner.Wired(watched);
                    var acts = new List<PersonActivity>();
                    for (int m = 0; m < 60; m++)
                    {
                        if (!nw.credited) planner.StartShapingReaction(nw.id, new ViewTime(watched.State.month, 10f));
                        planner.PlanAll(new ViewTime(watched.State.month, 10.1f), acts);
                        quiet.Step();
                        watched.Step();
                    }
                    Assert.AreEqual(JsonUtility.ToJson(quiet.State), JsonUtility.ToJson(watched.State), Tag(kind, fish, seed, 10f));
                    Assert.AreEqual(nq.credited, nw.credited);
                    Assert.AreEqual(nq.answerMonth, nw.answerMonth);
                    Assert.AreEqual(ShapingAnswers.EntryOf(quiet, nq)?.text, ShapingAnswers.EntryOf(watched, nw)?.text);
                    if (nw.credited) answers++;
                }
            TestContext.WriteLine($"{answers} gifts answered in both boxes");
            Assert.Greater(answers, 0);
        }

        /// <summary>The walk over and the walk back into the day keep the walking pace (≤ 2x, as every moment).</summary>
        [Test]
        public void ReactionWalksKeepThePace()
        {
            float worst = 0f;
            string what = "";
            int reactions = 0, steps = 0;
            foreach (int seed in new[] { 1, 2, 6, 10, 18, 1234 })
                foreach (var (kind, fish) in CampTools)
                {
                    var sim = Camp(seed);
                    var n = Gift(sim, kind, fish);
                    var planner = LifePlanner.Wired(sim);
                    long day = sim.State.month;
                    foreach (float hour in new[] { 5.5f, 7f, 10f, 12.5f, 16f, 18.4f, 19.4f, 19.95f, 20.5f, 21.5f, 23.5f })
                    {
                        planner.ClearLive();
                        var info = planner.StartShapingReaction(n.id, new ViewTime(day, hour));
                        if (info == null) continue;
                        reactions++;
                        var r = MomentTailPace.Worst(planner, day, hour, info.end, Tag(kind, fish, seed, hour));
                        steps += r.steps;
                        if (r.worst > worst) { worst = r.worst; what = r.what; }
                    }
                }
            TestContext.WriteLine($"{reactions} reactions, {steps} steps; fastest {worst:0.00}x ({what})");
            Assert.Greater(reactions, 50);
            Assert.Greater(steps, 100);
            Assert.LessOrEqual(worst, 2f, what);
        }

        [Test]
        public void TheBeatsAreSoundData()
        {
            foreach (var kind in SimCore.SliceTools)
                foreach (int belief in new[] { 1, 2, 3 })
                    for (float s = ShapingReactions.Seconds; s <= ShapingReactions.MaxSeconds; s += ShapingReactions.StretchStep)
                    {
                        var d = ShapingReactions.For(kind, belief, s, true);
                        foreach (var cast in new[] { RelicCast.Owner, RelicCast.Grown, RelicCast.Kids })
                        {
                            var beats = d.For(cast).OrderBy(b => b.from).ToList();
                            Assert.AreEqual(0f, beats[0].from, $"{kind}: starts at once");
                            Assert.AreEqual(1f, beats[^1].to, $"{kind}: tiles the moment");
                            for (int i = 1; i < beats.Count; i++) Assert.AreEqual(beats[i - 1].to, beats[i].from, 1e-6f, "no gaps");
                            Assert.IsFalse(beats.Any(b => ActivityInfo.IsMove(b.kind)), "the staging walks them; beats are poses");
                            Assert.AreEqual(belief == 1 ? ActivityKind.LookUpWary : ActivityKind.LookUp, beats[0].kind, "wary at first");
                        }
                        Assert.IsTrue(d.For(RelicCast.Onlookers).All(b => b.where == RelicWhere.Here));
                    }
            Assert.Less(ShapingReactions.OthersNotice, ShapingReactions.WithinSeconds);
            var far = ShapingReactions.For(SimCore.NudgeKind.Rain, 2, ShapingReactions.FromAfar, false);
            Assert.AreEqual(0, far.For(RelicCast.Owner).Count() + far.For(RelicCast.Grown).Count() + far.For(RelicCast.Kids).Count(), "no walkers at night");
            Assert.AreEqual(1f, far.For(RelicCast.Onlookers).Max(b => b.to));
            Assert.IsFalse(ShapingReactions.CanWalkAt(new ViewTime(0, 4.9f), ShapingReactions.Seconds));
            Assert.IsTrue(ShapingReactions.CanWalkAt(new ViewTime(0, 5f), ShapingReactions.Seconds));
            Assert.IsTrue(ShapingReactions.CanWalkAt(new ViewTime(0, 18f), ShapingReactions.Seconds));
            Assert.IsFalse(ShapingReactions.CanWalkAt(new ViewTime(0, 19.9f), ShapingReactions.Seconds));
            Assert.IsFalse(ShapingReactions.CanWalkAt(new ViewTime(0, 23f), ShapingReactions.Seconds));
        }
    }
}
