using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>The activity planner (#29, docs/design/life-in-the-box.md §10): every person, every hour, has a valid
    /// activity and place; the village shows at least 8 kinds of activity; work hours are busy; the plan is deterministic
    /// and never touches the sim's dice; planning the whole box is cheap.</summary>
    public class LifePlannerTests
    {
        static SimCore NewSim(int seed)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>One month, crossing each era as soon as it is ready (a player who always watches).</summary>
        static void Month(SimCore sim)
        {
            sim.Step();
            if (sim.State.eraReady) sim.AdvanceEra();
        }

        static SimCore SimIn(Era era, int seed)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++) Month(sim);
            Assert.AreEqual(era, sim.State.era, $"seed {seed} reaches {era}");
            for (int m = 0; m < 24; m++) sim.Step(); // settle into the era (and stay in it)
            return sim;
        }

        static void AssertValid(LifePlanner planner, List<PersonActivity> acts, ViewTime t, string when)
        {
            foreach (var a in acts)
            {
                Assert.AreNotEqual(ActivityKind.None, a.kind, $"{when}: {a.who} has no activity");
                Assert.IsTrue(planner.IsValidPlace(a.place), $"{when}: {a.who} {a.kind} at a missing place {a.place}");
                Assert.IsTrue(planner.IsValidPlace(a.from), $"{when}: {a.who} {a.kind} from a missing place {a.from}");
                Assert.IsTrue(a.start <= t.hour + 1e-4f && t.hour < a.end + 1e-4f, $"{when}: {a.who} {a.kind} [{a.start},{a.end}) doesn't cover {t.hour}");
                if (a.moving) Assert.AreNotEqual(a.from, a.place, $"{when}: {a.who} walks nowhere");
                if (a.kind == ActivityKind.Walk) Assert.IsTrue(a.moving || a.from == a.place, $"{when}: a walk without a destination");
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        public void EveryPersonEveryHourHasAValidActivityAndPlace(int seed)
        {
            var sim = NewSim(seed);
            var planner = new LifePlanner(sim);
            var acts = new List<PersonActivity>();
            int checks = 0;
            var eras = new HashSet<Era>();
            for (int m = 0; m < 120 * 12; m++)
            {
                Month(sim);
                long day = sim.State.month;
                eras.Add(sim.State.era);
                // Every month a few hours; every year's spring and autumn the whole day, half-hourly.
                bool full = m % 12 == 3 || m % 12 == 9;
                for (float h = full ? 0f : (m * 5) % 24 + 0.3f; h < 24f; h += full ? 0.5f : 7.7f)
                {
                    var t = new ViewTime(day, h);
                    planner.PlanAll(t, acts);
                    int alive = sim.State.notables.Count(n => n.alive);
                    Assert.AreEqual(alive + Math.Max(0, sim.State.population - alive), acts.Count, $"month {day}: everyone is planned");
                    AssertValid(planner, acts, t, $"seed {seed} month {day} {h:0.0}h ({sim.State.era})");
                    checks += acts.Count;
                }
            }
            Assert.AreEqual(0, planner.FallbackCount, "no planned place went missing");
            Assert.IsTrue(eras.Contains(Era.Town), "the run reaches the town");
            TestContext.WriteLine($"seed {seed}: {checks} person-hours checked over 120 years");
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void TheVillageShowsAtLeastEightKindsOfActivity(int seed)
        {
            var sim = SimIn(Era.Village, seed);
            var planner = new LifePlanner(sim);
            var kinds = new HashSet<ActivityKind>();
            var acts = new List<PersonActivity>();
            for (int d = 0; d < 12; d++)
            {
                sim.Step();
                for (float h = 0f; h < 24f; h += 0.5f)
                {
                    planner.PlanAll(new ViewTime(sim.State.month, h), acts);
                    foreach (var a in acts) kinds.Add(a.kind);
                }
            }
            TestContext.WriteLine($"seed {seed}: {kinds.Count} kinds in a village year: {string.Join(", ", kinds.OrderBy(k => k))}");
            Assert.GreaterOrEqual(kinds.Count, 8);
            var work = kinds.Where(k => ActivityInfo.GroupOf(k) == ActivityGroup.Work).ToList();
            Assert.GreaterOrEqual(work.Count, 4, "several different work loops: " + string.Join(", ", work));
        }

        [TestCase(Era.Camp, 1)]
        [TestCase(Era.Village, 7)]
        [TestCase(Era.Town, 7)]
        [TestCase(Era.Town, 1234)]
        public void WorkHoursAreBusy(Era era, int seed)
        {
            var sim = era == Era.Camp ? NewSim(seed) : SimIn(era, seed);
            if (era == Era.Camp) for (int m = 0; m < 36; m++) sim.Step();
            var planner = new LifePlanner(sim);
            var acts = new List<PersonActivity>();
            int adults = 0, busy = 0, walking = 0, days = 0;
            for (int d = 0; d < 36; d++)
            {
                sim.Step();
                long day = sim.State.month;
                if (planner.IsRestDay(day)) continue;
                var w = planner.WeatherOn(day);
                if (w == Weather.Storm || w == Weather.Rainbow) continue;
                days++;
                for (float h = 7.1f; h < 17f; h += 0.4f)
                {
                    if (!LifeClock.IsWorkHour(h)) continue;
                    planner.PlanAll(new ViewTime(day, h), acts);
                    foreach (var a in acts)
                    {
                        if (planner.StageOf(a.who) != LifeStage.Adult || a.source == ActivitySource.Scene) continue;
                        adults++;
                        if (a.IsBusy) busy++;
                        if (a.kind == ActivityKind.Walk) walking++;
                    }
                }
            }
            float share = busy / (float)adults, walk = walking / (float)adults;
            TestContext.WriteLine($"{era} seed {seed}: {days} work days, busy {share:P1}, walking empty-handed {walk:P1} of {adults} adult-samples");
            Assert.Greater(adults, 0);
            Assert.GreaterOrEqual(share, 0.7f, "≥ 70 % of working-age adults busy in work hours (rule 4)");
            Assert.LessOrEqual(walk, 0.12f, "few walk empty-handed in work hours (rule 4: ≤ 10 % [est])");
        }

        static string Dump(LifePlanner planner, IEnumerable<ViewTime> times)
        {
            var sb = new StringBuilder();
            var acts = new List<PersonActivity>();
            foreach (var t in times)
            {
                planner.PlanAll(t, acts);
                foreach (var a in acts)
                    sb.Append(a.who.Key).Append(':').Append(a.kind).Append('@').Append(a.place).Append('<').Append(a.from).Append('/')
                      .Append(a.partner.Key).Append('/').Append(a.prop).Append('/').Append(a.step).Append('/').Append(a.slot).Append(';');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        [Test]
        public void ThePlanIsDeterministic()
        {
            var a = SimIn(Era.Town, 7);
            var b = SimIn(Era.Town, 7);
            long day = a.State.month;
            var times = Enumerable.Range(0, 48).Select(i => new ViewTime(day, i * 0.5f)).ToList();
            string first = Dump(new LifePlanner(a), times);
            Assert.AreEqual(first, Dump(new LifePlanner(b), times), "same seed, same plan");
            // Order doesn't matter: a planner asked backwards, after other days, answers the same.
            var c = new LifePlanner(a);
            Dump(c, new[] { new ViewTime(day + 5, 9f), new ViewTime(day - 3, 18f) });
            var backwards = times.AsEnumerable().Reverse().ToList();
            var lines = Dump(c, backwards).Split('\n').Reverse().Skip(1);
            Assert.AreEqual(first, string.Join("\n", lines) + "\n", "plan doesn't depend on query order");
            // A different seed gives a different day.
            Assert.AreNotEqual(first, Dump(new LifePlanner(SimIn(Era.Town, 1234)), times));
        }

        [Test]
        public void PlanningNeverTouchesTheSimOrItsDice()
        {
            var watched = NewSim(7);
            var alone = NewSim(7);
            var planner = new LifePlanner(watched);
            var acts = new List<PersonActivity>();
            var trips = new List<Trip>();
            for (int m = 0; m < 60 * 12; m++)
            {
                Month(watched);
                Month(alone);
                if (m % 7 != 0) continue;
                string before = JsonUtility.ToJson(watched.State);
                long day = watched.State.month;
                for (float h = 0f; h < 24f; h += 1.5f) planner.PlanAll(new ViewTime(day, h), acts);
                planner.React(KeeperGesture.TapGlass, new ViewTime(day, 10f));
                planner.React(KeeperGesture.HoldFinger, new ViewTime(day, 10f), default, default, 1f);
                planner.StartScene(SceneKind.Wedding, new ViewTime(day, 15f));
                var last = watched.State.chronicle[watched.State.chronicle.Count - 1];
                planner.Echo(last, new ViewTime(day, 11f));
                planner.PlanAll(new ViewTime(day, 10.01f), acts);
                planner.PlanAll(new ViewTime(day, 15.5f), acts);
                foreach (var a in acts) { planner.Describe(a); planner.UpcomingTrips(a.who, new ViewTime(day, 8f), trips); }
                planner.ClearLive();
                Assert.AreEqual(before, JsonUtility.ToJson(watched.State), $"month {day}: planning changed the sim");
            }
            Assert.AreEqual(JsonUtility.ToJson(alone.State), JsonUtility.ToJson(watched.State), "a watched box lives exactly like one nobody planned");
            Assert.AreEqual(alone.State.rng.state, watched.State.rng.state);
            Assert.AreEqual(alone.State.econRng.state, watched.State.econRng.state);
            Assert.AreEqual(alone.State.storyRng.state, watched.State.storyRng.state);
        }

        [Test]
        public void PlanningTheWholeBoxIsCheap()
        {
            var sim = NewSim(7);
            for (int m = 0; m < 100 * 12; m++) Month(sim); // a full box: the town at year 100
            var planner = new LifePlanner(sim);
            var acts = new List<PersonActivity>();
            // Warm up (JIT).
            for (float h = 0; h < 24f; h += 1f) planner.PlanAll(new ViewTime(sim.State.month, h), acts);
            var sw = new Stopwatch();
            var one = new Stopwatch();
            int steps = 0, people = 0;
            double worst = 0;
            for (int d = 0; d < 20; d++)
            {
                sim.Step(); // the sim's month is not part of the planner's cost
                long day = sim.State.month;
                sw.Start();
                for (float h = 0f; h < 24f; h += 0.25f)
                {
                    one.Restart();
                    planner.PlanAll(new ViewTime(day, h), acts);
                    one.Stop();
                    worst = Math.Max(worst, one.Elapsed.TotalMilliseconds);
                    steps++;
                }
                sw.Stop();
                people = acts.Count;
            }
            double ms = sw.Elapsed.TotalMilliseconds / steps;
            TestContext.WriteLine($"Planning the whole town box ({people} people): {ms:0.000} ms per view-time step on average, worst {worst:0.000} ms (a new day's first step); {steps} steps, " +
#if UNITY_2017_1_OR_NEWER
                "Unity Mono)");
            Assert.Less(ms, 2.0, "Mono is slower than .NET; the budget is measured in .NET (Tools/SimTests)");
#else
                ".NET)");
            Assert.Less(ms, 0.5, "≤ 0.5 ms per whole-box plan in .NET");
#endif
        }

        [Test]
        public void InterruptsGoReactionThenSceneThenRoleThenSchedule()
        {
            var sim = SimIn(Era.Town, 7);
            var planner = new LifePlanner(sim);
            var lord = sim.HolderOf(SimCore.LeaderOf(sim.State.era));
            Assume.That(lord, Is.Not.Null, "the town has a lord");
            var who = PersonRef.Notable(lord.id);
            long day = sim.State.month;
            while (planner.IsRestDay(day) || planner.WeatherOn(day) != Weather.Clear || HasScene(planner, day)) { sim.Step(); day = sim.State.month; }

            var t = new ViewTime(day, 17.9f);
            var role = planner.Plan(who, t);
            Assert.AreEqual(ActivitySource.RoleBeat, role.source);
            Assert.AreEqual(ActivityKind.Speech, role.kind, "the leader speaks in the evening");
            var listener = planner.People.First(p => p != who && planner.StageOf(p) == LifeStage.Adult && planner.Plan(p, t).kind == ActivityKind.Listen);
            Assert.AreEqual(who, planner.Plan(listener, t).partner, "they listen to the leader");

            planner.StartScene(SceneKind.EveningTalk, new ViewTime(day, 17.8f));
            Assert.AreEqual(ActivitySource.Scene, planner.Plan(who, t).source, "a scene beats a role beat");
            planner.React(KeeperGesture.TapGlass, new ViewTime(day, 17.88f));
            var r = planner.Plan(who, t);
            Assert.AreEqual(ActivitySource.Reaction, r.source, "a reaction beats everything");
            Assert.AreEqual(ActivityKind.LookUp, r.kind);
            Assert.LessOrEqual((r.end - r.start) * LifeClock.RealSecondsPerViewHour(17.9f), 4.01f, "a reaction lasts ≤ 4 s");
            Assert.AreEqual(ActivitySource.Scene, planner.Plan(who, new ViewTime(day, 18.5f)).source, "after the reaction, back to the scene");
            planner.ClearLive();
            var morning = planner.Plan(who, new ViewTime(day, 10f));
            Assert.AreEqual(ActivityKind.HoldCourt, morning.kind, "the lord holds court in the morning");
        }

        static bool HasScene(LifePlanner planner, long day)
        {
            var acts = new List<PersonActivity>();
            for (float h = 0f; h < 24f; h += 0.5f)
            {
                planner.PlanAll(new ViewTime(day, h), acts);
                if (acts.Any(a => a.source == ActivitySource.Scene)) return true;
            }
            return false;
        }

        [Test]
        public void FriendsMeetEveryEveningAndRivalsArgueSomeDays()
        {
            var sim = SimIn(Era.Village, 7);
            var adults = sim.State.notables.Where(n => n.alive && n.age >= 16f && n.age < 60f).OrderBy(n => n.id).ToList();
            Assume.That(adults.Count, Is.GreaterThanOrEqualTo(4));
            foreach (var n in sim.State.notables) { n.friendId = -1; n.rivalId = -1; }
            adults[0].friendId = adults[1].id; adults[1].friendId = adults[0].id;
            adults[2].rivalId = adults[3].id; adults[3].rivalId = adults[2].id;
            var planner = new LifePlanner(sim);
            int met = 0, argued = 0, days = 0;
            for (long day = sim.State.month; day < sim.State.month + 60; day++)
            {
                if (HasScene(planner, day)) continue;
                days++;
                var f0 = planner.Plan(PersonRef.Notable(adults[0].id), new ViewTime(day, 18.6f));
                var f1 = planner.Plan(PersonRef.Notable(adults[1].id), new ViewTime(day, 18.6f));
                if ((f0.kind == ActivityKind.Chat || f0.kind == ActivityKind.Court) && f0.partner.notableId == adults[1].id &&
                    f1.partner.notableId == adults[0].id && f0.place == f1.place) met++;
                var r0 = planner.Plan(PersonRef.Notable(adults[2].id), new ViewTime(day, 17.4f));
                if (r0.kind == ActivityKind.Argue && r0.partner.notableId == adults[3].id) argued++;
            }
            TestContext.WriteLine($"{days} days: friends met {met}, rivals argued {argued}");
            Assert.AreEqual(days, met, "friends meet every evening both are free");
            Assert.Greater(argued, 0, "rivals argue now and then");
            Assert.Less(argued, days, "but not every day");
        }

        [Test]
        public void TheCrowdIsAQuarterKidsAndTakesItsJobsFromTheBuildings()
        {
            var sim = SimIn(Era.Town, 7);
            var planner = new LifePlanner(sim);
            var crowd = planner.People.Where(p => p.IsCrowd).ToList();
            Assume.That(crowd.Count, Is.GreaterThanOrEqualTo(8));
            float kids = crowd.Count(p => planner.StageOf(p) == LifeStage.Kid || planner.StageOf(p) == LifeStage.Baby) / (float)crowd.Count;
            TestContext.WriteLine($"crowd {crowd.Count}, kids {kids:P0}; jobs: {string.Join(", ", crowd.Select(planner.JobOf))}");
            Assert.That(kids, Is.InRange(0.15f, 0.35f), "about 25 % kids");
            var jobs = crowd.Select(planner.JobOf).ToList();
            Assert.Contains("cook", jobs, "the fire's cook is the first slot");
            Assert.Contains(JobType.Farmer, jobs, "the fields get farmers");
            // Stable by index: the same crowd member keeps their job next month if the buildings don't change.
            int buildings = sim.State.buildings.Count;
            var before = crowd.Select(planner.JobOf).ToList();
            sim.Step();
            if (sim.State.buildings.Count == buildings && sim.State.population >= before.Count)
                Assert.AreEqual(before, planner.People.Where(p => p.IsCrowd).Take(before.Count).Select(planner.JobOf).ToList());
        }

        [Test]
        public void SeasonsSwitchTheFarmersWork()
        {
            var sim = SimIn(Era.Village, 7);
            var planner = new LifePlanner(sim);
            var farmer = planner.People.First(p => planner.JobOf(p) == JobType.Farmer);
            var seen = new Dictionary<Season, HashSet<ActivityKind>>();
            var day = new List<PersonActivity>();
            for (long d = sim.State.month; d < sim.State.month + 24; d++)
            {
                if (planner.IsRestDay(d)) continue;
                var s = planner.SeasonOn(d);
                if (!seen.ContainsKey(s)) seen[s] = new HashSet<ActivityKind>();
                planner.DaySchedule(farmer, d, day);
                foreach (var a in day) seen[s].Add(a.kind);
            }
            Assert.IsTrue(seen[Season.Spring].Contains(ActivityKind.Sow), "sowing in spring");
            Assert.IsTrue(seen[Season.Summer].Contains(ActivityKind.Weed), "weeding in summer");
            Assert.IsTrue(seen[Season.Autumn].Contains(ActivityKind.Harvest), "the harvest in autumn");
            Assert.IsFalse(seen[Season.Winter].Contains(ActivityKind.Harvest), "no harvest in winter");
        }

        [Test]
        public void ADeathBringsAQuietEveningAtTheRememberingPlace()
        {
            var sim = SimIn(Era.Village, 7);
            var parent = sim.State.notables.First(n => n.alive && n.age >= 20f && n.age < 50f);
            var child = sim.DebugBirth();
            child.parentId = parent.id;
            child.family = parent.family;
            child.age = 30f; child.lifespan = 99f;
            sim.Step(); // the child settles in with a job
            var dead = parent;
            dead.alive = false; dead.diedMonth = sim.State.month;
            sim.Chronicle("death", $"{dead.FullName} died of old age.", 0.85f, dead.id);
            var planner = new LifePlanner(sim);
            var mourn = planner.Plan(PersonRef.Notable(child.id), new ViewTime(sim.State.month, 17.9f));
            Assert.AreEqual(ActivityKind.Mourn, mourn.kind);
            Assert.AreEqual(PlaceKind.Remembering, mourn.place.kind);
            Assert.AreEqual(ActivitySource.Scene, mourn.source);
            Assert.AreEqual(ActivityKind.None, planner.Plan(PersonRef.Notable(dead.id), new ViewTime(sim.State.month, 17.9f)).kind, "the dead aren't shown");
            Assert.AreNotEqual(ActivityKind.Mourn, planner.Plan(PersonRef.Notable(child.id), new ViewTime(sim.State.month + 1, 17.9f)).kind, "the next day is normal");
        }

        [Test]
        public void NightStillHasLifeAndKidsPlay()
        {
            var sim = SimIn(Era.Town, 7);
            var planner = new LifePlanner(sim);
            var acts = new List<PersonActivity>();
            int awake = 0, kidsPlaying = 0;
            for (long d = sim.State.month; d < sim.State.month + 6; d++)
            {
                planner.PlanAll(new ViewTime(d, 21f), acts);
                awake += acts.Count(a => a.kind != ActivityKind.Sleep && a.kind != ActivityKind.Walk);
                planner.PlanAll(new ViewTime(d, 15f), acts);
                kidsPlaying += acts.Count(a => planner.StageOf(a.who) == LifeStage.Kid && ActivityInfo.GroupOf(a.kind) == ActivityGroup.Play);
            }
            Assert.Greater(awake, 0, "night owls at 21h");
            Assert.Greater(kidsPlaying, 0, "kids play in the afternoon");
        }

        sealed class LazyEveryone : ILifeHints
        {
            public PersonHints Person(Notable n, string role) => new PersonHints { napChance = 1f, singChance = 1f };
            public BoxHints Office(Notable holder, string role) => new BoxHints { homeEarlyHours = 1f };
        }

        [Test]
        public void TraitHintsColourTheDay()
        {
            var sim = SimIn(Era.Village, 7);
            long day = sim.State.month;
            while (LifeClock.IsRestDay(day)) day++;
            var plain = new LifePlanner(sim);
            var coloured = new LifePlanner(sim, new LazyEveryone());
            var worker = plain.People.First(p => p.IsNotable && plain.StageOf(p) == LifeStage.Adult);
            Assert.AreNotEqual(ActivityKind.Nap, plain.Plan(worker, new ViewTime(day, 13.5f)).kind, "neutral hints: no nap");
            if (!HasScene(coloured, day)) Assert.AreEqual(ActivityKind.Nap, coloured.Plan(worker, new ViewTime(day, 13.5f)).kind, "the lazy nap after lunch");
            Assert.AreEqual(ActivityKind.Sleep, coloured.Plan(worker, new ViewTime(day, 19.9f)).kind, "the grump in office sends people home early");
        }

        [Test]
        public void HoldingAFingerFollowsHowThePeopleUnderstandTheGlass()
        {
            foreach (var (era, seed, kidKind) in new[] { (Era.Camp, 1, ActivityKind.LookUpWary), (Era.Village, 7, ActivityKind.JumpAndPoint), (Era.Town, 7, ActivityKind.JumpAndPoint) })
            {
                var sim = era == Era.Camp ? NewSim(seed) : SimIn(era, seed);
                if (era == Era.Camp) for (int m = 0; m < 24; m++) sim.Step();
                var planner = new LifePlanner(sim);
                long day = sim.State.month;
                planner.React(KeeperGesture.HoldFinger, new ViewTime(day, 10f));
                var acts = new List<PersonActivity>();
                planner.PlanAll(new ViewTime(day, 10.2f), acts);
                var kinds = acts.Where(a => a.source == ActivitySource.Reaction).Select(a => a.kind).ToList();
                TestContext.WriteLine($"{era}: {string.Join(", ", kinds)}");
                Assert.IsNotEmpty(kinds, $"{era}: someone reacts to the finger");
                // #92 (relics-and-glass-voice §3): in the camp the grown-ups stay back, wary; the kids creep in, hop back, then jump.
                if (era == Era.Camp)
                    foreach (var a in acts.Where(a => a.source == ActivitySource.Reaction))
                        if (planner.StageOf(a.who) == LifeStage.Kid) Assert.Contains(a.kind, new[] { ActivityKind.CreepUp, ActivityKind.HopBack, ActivityKind.JumpAndPoint }, "camp kids creep in");
                        else Assert.AreEqual(ActivityKind.LookUpWary, a.kind, "the camp's grown-ups are a bit scared of the glass");
                else if (acts.Any(a => planner.StageOf(a.who) == LifeStage.Kid)) Assert.Contains(kidKind, kinds, $"{era}: kids jump and point");
                planner.Release(new ViewTime(day, 10.3f));
                planner.PlanAll(new ViewTime(day, 10.31f), acts);
                Assert.IsFalse(acts.Any(a => a.source == ActivitySource.Reaction && a.kind != ActivityKind.LookUp), $"{era}: they scatter when the finger lifts");
            }
        }

        [Test]
        public void ShowMeEchoesAStory()
        {
            var sim = SimIn(Era.Village, 7);
            var a = sim.State.notables.Where(n => n.alive && n.age >= 16f).OrderBy(n => n.id).ToList();
            Assume.That(a.Count, Is.GreaterThanOrEqualTo(2));
            var e = sim.Chronicle("friendship", "test", 0.5f, a[0].id);
            e.otherNotableId = a[1].id;
            var planner = new LifePlanner(sim);
            var at = new ViewTime(sim.State.month + 1, 10f);
            Assert.IsTrue(planner.Echo(e, at));
            var x = planner.Plan(PersonRef.Notable(a[0].id), at.AddHours(0.05f));
            Assert.AreEqual(ActivityKind.Chat, x.kind);
            Assert.AreEqual(a[1].id, x.partner.notableId);
            float secs = (x.end - x.start) * LifeClock.RealSecondsPerViewHour(10f);
            Assert.That(secs, Is.InRange(2.9f, 5.1f), "a 3–5 s echo");
            var death = sim.Chronicle("death", "test", 0.85f, a[0].id);
            Assert.IsFalse(planner.Echo(death, at), "no echo for a death");
        }

        [Test]
        public void UpcomingTripsFollowThePathsOfTheDay()
        {
            var sim = SimIn(Era.Village, 7);
            var planner = new LifePlanner(sim);
            long day = sim.State.month;
            while (planner.IsRestDay(day)) day++;
            var trips = new List<Trip>();
            int withTrips = 0;
            foreach (var who in planner.People.Where(p => planner.StageOf(p) == LifeStage.Adult))
            {
                planner.UpcomingTrips(who, new ViewTime(day, 5f), trips, 8);
                if (trips.Count > 0) withTrips++;
                float last = 5f;
                foreach (var tr in trips)
                {
                    Assert.AreNotEqual(tr.from, tr.to, $"{who}: a trip goes somewhere");
                    Assert.GreaterOrEqual(tr.start, last - 1e-4f, $"{who}: trips in order");
                    Assert.Greater(tr.end, tr.start);
                    last = tr.end;
                }
                Assert.IsTrue(trips.Any(t => t.type == TripType.HomeToWork || t.type == TripType.HomeToGreen), $"{who}: the morning walk to work is a wear-pass trip");
            }
            Assert.Greater(withTrips, 0);
        }

        [Test]
        public void TheViewClockRunsAboutFiveMinutesADayWithAShortNight()
        {
            Assert.That(LifeClock.RealSecondsPerDay, Is.InRange(280f, 320f));
            var t = new ViewTime(10, 20f);
            var later = LifeClock.Advance(t, 45f);
            Assert.AreEqual(11, later.day);
            Assert.AreEqual(5f, later.hour, 0.01f, "the night lasts ~45 s");
            var noon = LifeClock.Advance(new ViewTime(3, 7f), 80f);
            Assert.AreEqual(12f, noon.hour, 0.01f);
            Assert.AreEqual(Season.Winter, LifeClock.SeasonOf(12));
            Assert.AreEqual(Season.Spring, LifeClock.SeasonOf(14));
        }
    }
}
