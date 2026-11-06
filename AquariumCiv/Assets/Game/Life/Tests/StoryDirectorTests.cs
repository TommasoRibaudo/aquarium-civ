using System.Collections.Generic;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#367 B11: the background-life director's decision (Life/StoryDirector.cs), pure: a story always in view, as busy as the
    /// population says, never people who are busy, never the same kind twice running, nothing at night, its own seeded stream.</summary>
    public class StoryDirectorTests
    {
        // ---------------------------------------------------------------- a little box

        static StoryWorld World(int pop, float time, int kids = 4, int adults = 4, float free = 60f)
        {
            var w = new StoryWorld { time = time, population = pop, part = StoryDayPart.Day, waterHours = true, focusX = 0f, focusZ = 0f };
            for (int i = 0; i < kids; i++) w.free.Add(new StoryActor { key = -(i + 1), kid = true, x = 0.2f * i, z = 0.1f, freeSeconds = free });
            for (int i = 0; i < adults; i++) w.free.Add(new StoryActor { key = 100 + i, adult = true, x = 0.3f * i, z = -0.2f, freeSeconds = free });
            w.rabbits = true; w.rabbitX = 0.8f; w.rabbitZ = 0.4f;
            w.water = true; w.waterX = -1f; w.waterZ = 0.5f;
            w.splash = true; w.splashX = -0.9f; w.splashZ = 0.4f;
            w.bush = true; w.bushX = 1f; w.bushZ = -0.5f; w.bushCell = 77;
            return w;
        }

        static bool Yes(StoryPlan p) => true;

        sealed class Running { public StoryKind kind; public float until; public List<int> who; }

        /// <summary>Runs the director over <paramref name="seconds"/> of a box where everything is possible; each story it starts is in view
        /// for its own length. Returns the plans in order with their start times, the quiet seconds and the longest quiet stretch.</summary>
        static (List<(float t, StoryKind k)> starts, float quiet, float longest) Run(int pop, float seconds, int seed = 5, StoryDensity density = StoryDensity.Normal, float warm = 30f)
        {
            var d = new StoryDirector(seed) { Density = density };
            var running = new List<Running>();
            var starts = new List<(float, StoryKind)>();
            float quiet = 0f, longest = 0f, stretch = 0f;
            const float dt = 0.5f;
            for (float t = 0f; t < seconds; t += dt)
            {
                running.RemoveAll(r => t >= r.until);
                var w = World(pop, t, kids: 8, adults: 8);
                var busy = new HashSet<int>();
                foreach (var r in running) { foreach (int k in r.who) busy.Add(k); if (!w.kindsInView.Contains(r.kind)) w.kindsInView.Add(r.kind); }
                w.free.RemoveAll(a => busy.Contains(a.key));
                w.storiesInView = running.Count;
                var plan = d.Step(w, Yes);
                if (plan != null)
                {
                    starts.Add((t, plan.kind));
                    running.Add(new Running { kind = plan.kind, until = t + plan.seconds, who = new List<int>(plan.members) });
                }
                if (t < warm) continue;
                if (running.Count == 0) { quiet += dt; stretch += dt; longest = System.Math.Max(longest, stretch); }
                else stretch = 0f;
            }
            return (starts, quiet, longest);
        }

        // ---------------------------------------------------------------- the rule

        [Test]
        public void NothingInView_StartsOne_NearTheFocus()
        {
            var d = new StoryDirector(1);
            var w = World(8, 0f);
            var plan = d.Step(w, Yes);
            Assert.IsNotNull(plan, d.LastWhy);
            Assert.AreEqual(StoryKind.ChaseRabbits, plan.kind, "B11's order: the kids chase the rabbits first");
            Assert.That(plan.members.Count, Is.InRange(1, PlayGroups.MaxChase));
            foreach (int k in plan.members) Assert.Less(k, 0, "kids only for a kids' game");
        }

        [Test]
        public void SomethingInView_Waits_UntilTheIntervalIsUp()
        {
            var d = new StoryDirector(1);
            Assert.IsNotNull(d.Step(World(8, 0f), Yes));
            var w = World(8, 3f); w.storiesInView = 1; w.kindsInView.Add(StoryKind.ChaseRabbits);
            Assert.IsNull(d.Step(w, Yes), "a camp's floor is 1 and one is in view: wait");
            w = World(8, 6f); w.storiesInView = 1;
            Assert.IsNull(d.Step(w, Yes), "still under the interval");
            // A tribe (floor 0): none in view, but only just quiet.
            var tribe = new StoryDirector(2);
            var tw = World(4, 0f); tw.storiesInView = 1;
            Assert.IsNull(tribe.Step(tw, Yes), "a story is on: a tribe is calm");
            tw = World(4, 3f);
            Assert.IsNull(tribe.Step(tw, Yes), "a tribe waits for a quiet stretch before it starts one");
            StoryPlan late = null;
            for (float t = 6f; t < 40f && late == null; t += 3f) late = tribe.Step(World(4, t), Yes);
            Assert.IsNotNull(late, "none for a while: one starts");
        }

        [Test]
        public void Density_ScalesWithPopulation_LikeTheMomentDirector()
        {
            Assert.AreEqual(20f, StoryRules.IntervalSeconds(4, StoryDensity.Normal), 1e-4f);
            Assert.AreEqual(20f, StoryRules.IntervalSeconds(6, StoryDensity.Normal), 1e-4f);
            Assert.AreEqual(15f, StoryRules.IntervalSeconds(10, StoryDensity.Normal), 1e-4f);
            Assert.AreEqual(10f, StoryRules.IntervalSeconds(22, StoryDensity.Normal), 1e-4f);
            Assert.AreEqual(6f, StoryRules.IntervalSeconds(40, StoryDensity.Normal), 1e-4f);
            Assert.AreEqual(12.5f, StoryRules.IntervalSeconds(16, StoryDensity.Normal), 1e-4f, "linear between the rows");
            Assert.AreEqual(3f, StoryRules.IntervalSeconds(40, StoryDensity.High), 1e-4f, "high = twice the rate");
            Assert.AreEqual(new[] { 0, 1, 1, 2 }, new[] { StoryRules.Floor(5, StoryDensity.Normal), StoryRules.Floor(8, StoryDensity.Normal), StoryRules.Floor(15, StoryDensity.Normal), StoryRules.Floor(30, StoryDensity.Normal) });
            Assert.AreEqual(3, StoryRules.Cap(30, StoryDensity.Normal), "a town: 2-3 in view");
            // The same curve as the moments: busier together.
            for (int pop = 1; pop < 40; pop++)
                Assert.LessOrEqual(StoryRules.IntervalSeconds(pop + 1, StoryDensity.Normal), StoryRules.IntervalSeconds(pop, StoryDensity.Normal) + 1e-4f, $"pop {pop}");

            var tribe = Run(5, 600f); var town = Run(30, 600f);
            foreach (int pop in new[] { 5, 8, 15, 30 }) { var r = Run(pop, 600f); TestContext.WriteLine($"pop {pop}: {r.starts.Count} starts in 10 min, quiet {r.quiet / 9.5f:0.0} s/min, longest quiet {r.longest:0.0} s"); }
            Assert.Greater(town.starts.Count, tribe.starts.Count * 2, $"a town is busy ({town.starts.Count} starts in 10 min), a tribe calm ({tribe.starts.Count})");
            Assert.AreEqual(0, Run(30, 120f, density: StoryDensity.Off).starts.Count, "off starts nothing");
            Assert.Greater(Run(30, 600f, density: StoryDensity.High).starts.Count, town.starts.Count, "high starts more");
        }

        [TestCase(5, 20f)]
        [TestCase(8, 15f)]
        [TestCase(15, 10f)]
        [TestCase(30, 6f)]
        public void TheView_IsNeverQuietForLong(int pop, float most)
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var r = Run(pop, 600f, seed);
                Assert.Less(r.longest, most, $"pop {pop} seed {seed}: the longest stretch with no story in view");
                if (pop >= 7) Assert.Less(r.quiet / 9.5f, most, $"pop {pop} seed {seed}: quiet seconds per minute");
            }
        }

        [Test]
        public void NeverStealsBusyPeople()
        {
            PersonActivity A(ActivityKind k, ActivitySource src = ActivitySource.Schedule, bool moving = false) =>
                new PersonActivity { kind = k, source = src, moving = moving, place = Place.Land(PlaceKind.Meadow), chronicleId = -1 };
            Assert.IsTrue(StoryRules.FreeFor(A(ActivityKind.SitOut), LifeStage.Adult));
            Assert.IsTrue(StoryRules.FreeFor(A(ActivityKind.Play), LifeStage.Kid));
            Assert.IsTrue(StoryRules.FreeFor(A(ActivityKind.Chat), LifeStage.Elder));
            foreach (var k in new[] { ActivityKind.Sleep, ActivityKind.Nap, ActivityKind.Eat, ActivityKind.Indoors, ActivityKind.Chop, ActivityKind.Gather,
                                      ActivityKind.Fish, ActivityKind.Carry, ActivityKind.FetchWater, ActivityKind.Cook, ActivityKind.Knap, ActivityKind.Dance, ActivityKind.Speech })
                Assert.IsFalse(StoryRules.FreeFor(A(k), LifeStage.Adult), k.ToString());
            Assert.IsFalse(StoryRules.FreeFor(A(ActivityKind.Play), LifeStage.Adult), "grown-ups at play are a scene");
            Assert.IsFalse(StoryRules.FreeFor(A(ActivityKind.SitOut), LifeStage.Baby));
            Assert.IsFalse(StoryRules.FreeFor(A(ActivityKind.SitOut, ActivitySource.Scene), LifeStage.Adult), "in a scene or a moment");
            Assert.IsFalse(StoryRules.FreeFor(A(ActivityKind.SitOut, ActivitySource.Reaction), LifeStage.Adult), "reacting to the Keeper");
            Assert.IsFalse(StoryRules.FreeFor(A(ActivityKind.Walk, moving: true), LifeStage.Adult), "on the way somewhere");
            var hunter = A(ActivityKind.SitOut); hunter.hunt = HuntPhase.WalkOut;
            Assert.IsFalse(StoryRules.FreeFor(hunter, LifeStage.Adult), "a hunt");
            var tent = A(ActivityKind.SitOut); tent.tent = TentPhase.Raise;
            Assert.IsFalse(StoryRules.FreeFor(tent, LifeStage.Adult), "a tent raising");
            var parent = A(ActivityKind.SitOut); parent.carrying = PersonRef.Crowd(3);
            Assert.IsFalse(StoryRules.FreeFor(parent, LifeStage.Adult), "a baby in the arms");

            // Someone whose idle stretch ends in 5 s (work, a meal) isn't borrowed for a 20 s errand.
            var d = new StoryDirector(3);
            var w = World(8, 0f, kids: 0, adults: 1, free: 5f);
            w.rabbits = false; w.splash = false;
            Assert.IsNull(d.Step(w, Yes), "the only grown-up goes back to work soon");
            var d2 = new StoryDirector(3);
            var w2 = World(8, 0f, kids: 0, adults: 1, free: 30f);
            var p = d2.Step(w2, Yes);
            Assert.IsNotNull(p, d2.LastWhy);
            Assert.AreEqual(StoryKind.WaterRun, p.kind);
            Assert.AreEqual(100, p.members[0]);
        }

        [Test]
        public void NoKindTwiceRunning_NorAgainWithin30s_NorOneAlreadyInView()
        {
            foreach (int pop in new[] { 5, 8, 15, 30 })
                for (int seed = 1; seed <= 4; seed++)
                {
                    var s = Run(pop, 900f, seed, StoryDensity.High).starts;
                    Assert.Greater(s.Count, 5, $"pop {pop}");
                    for (int i = 1; i < s.Count; i++) Assert.AreNotEqual(s[i - 1].k, s[i].k, $"pop {pop} seed {seed}: #{i} at {s[i].t}");
                    var last = new Dictionary<StoryKind, float>();
                    foreach (var (t, k) in s)
                    {
                        if (last.TryGetValue(k, out float was)) Assert.GreaterOrEqual(t - was, StoryRules.NoRepeatSeconds, $"pop {pop}: {k} again after {t - was} s");
                        last[k] = t;
                    }
                }
            var d = new StoryDirector(4);
            var w = World(8, 0f); w.kindsInView.Add(StoryKind.ChaseRabbits); w.kindsInView.Add(StoryKind.Ball);
            var p = d.Step(w, Yes);
            Assert.AreEqual(StoryKind.Tag, p.kind, "the chase and a ball game are in view already");
        }

        [Test]
        public void Night_Nothing_Evening_OnlyAHandOverAtTheFire()
        {
            var d = new StoryDirector(6);
            var w = World(30, 0f); w.part = StoryDayPart.Night;
            Assert.IsNull(d.Step(w, Yes));
            StringAssert.Contains("night", d.LastWhy);

            d = new StoryDirector(6);
            w = World(30, 0f); w.part = StoryDayPart.Evening; w.waterHours = false;
            Assert.IsNull(d.Step(w, Yes), "nobody at the fire: nothing");
            d = new StoryDirector(6);
            w = World(30, 0f); w.part = StoryDayPart.Evening; w.waterHours = false;
            for (int i = 0; i < w.free.Count; i++) { var a = w.free[i]; if (a.key == 101 || a.key == 102) a.atFire = true; w.free[i] = a; }
            var p = d.Step(w, Yes);
            Assert.IsNotNull(p, d.LastWhy);
            Assert.AreEqual(StoryKind.HandOver, p.kind);
            CollectionAssert.AreEquivalent(new[] { 101, 102 }, p.members, "the two at the fire");

            // No water run after the morning, even in daylight.
            d = new StoryDirector(6);
            w = World(8, 0f, kids: 0, adults: 1); w.waterHours = false;
            p = d.Step(w, Yes);
            Assert.AreEqual(StoryKind.Pick, p.kind, "the afternoon: a pick, not a water run");
        }

        [Test]
        public void AViewThatCantStartIt_FallsThroughToTheNextKind()
        {
            var d = new StoryDirector(7);
            var p = d.Step(World(8, 0f), x => x.kind != StoryKind.ChaseRabbits && x.kind != StoryKind.Ball);
            Assert.AreEqual(StoryKind.Tag, p.kind);
            Assert.AreEqual(StoryKind.Tag, d.LastKind);
            Assert.AreEqual(1, d.Started);
        }

        [Test]
        public void Deterministic_OnItsOwnSeededStream()
        {
            var a = Run(15, 600f, seed: 11);
            var b = Run(15, 600f, seed: 11);
            CollectionAssert.AreEqual(a.starts, b.starts, "same seed, same box: the same stories at the same times");
            var members = new List<string>();
            for (int seed = 0; seed < 12; seed++)
            {
                var d = new StoryDirector(seed);
                var w = World(8, 0f, kids: 6); w.rabbits = false;
                var p = d.Step(w, Yes);
                members.Add(string.Join(",", p.members));
            }
            Assert.Greater(new HashSet<string>(members).Count, 1, "the seed changes who of the free kids (and the jitter)");
        }
    }
    /// <summary>#367: the planner half: the grown-ups' errands (a water run, a pick) are short live scenes for idle people only, they play
    /// their beat at the real place and walk back into their day; nobody asleep or at work is taken.</summary>
    public class StoryErrandTests
    {
        static SimCore Box(int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        static bool FindIdle(LifePlanner planner, long day, out PersonRef who, out ViewTime at)
        {
            for (float h = 7f; h < 16.5f; h += 0.25f)
                foreach (var p in planner.People)
                {
                    if (planner.StageOf(p) != LifeStage.Adult) continue;
                    var t = new ViewTime(day, h);
                    var a = planner.Plan(p, t);
                    if (StoryRules.FreeFor(a, LifeStage.Adult) && a.end - h > 0.6f) { who = p; at = t; return true; }
                }
            who = PersonRef.None; at = default;
            return false;
        }

        [TestCase(5)]
        [TestCase(7)]
        [TestCase(23)]
        public void AWaterRun_DrawsAtTheWater_AndWalksBackIntoTheDay(int seed)
        {
            var sim = Box(seed);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            Assume.That(FindIdle(planner, day, out var who, out var at), "an idle grown-up some time in the day");
            Assert.IsTrue(planner.StartStoryErrand(StoryKind.WaterRun, who, at, -1, out float secs, out string why), why);
            Assert.That(secs, Is.InRange(LifePlanner.StoryDrawSeconds, LifePlanner.StoryMaxErrandSeconds));
            Assert.IsTrue(planner.InLifeStory(who, at));
            Assert.IsFalse(planner.StartStoryErrand(StoryKind.WaterRun, who, at, -1, out _, out _), "one errand at a time");
            bool drew = false, carried = false;
            for (float s = 0f; s <= secs; s += 0.25f)
            {
                var a = planner.Plan(who, LifeClock.Advance(at, s));
                if (a.kind == ActivityKind.FetchWater && !a.moving && a.place.kind is PlaceKind.Pond or PlaceKind.Stream or PlaceKind.Puddle) drew = true;
                if (a.kind == ActivityKind.Carry && a.prop == Prop.Buckets) carried = true;
                Assert.AreNotEqual(ActivityKind.Sleep, a.kind);
            }
            Assert.IsTrue(drew, "kneels and draws at the real water");
            Assert.IsTrue(carried, "carries the full bucket home");
            var after = LifeClock.Advance(at, secs + 20f);
            Assert.IsFalse(planner.InLifeStory(who, after), "over");
            Assert.AreNotEqual(ActivitySource.Scene, planner.Plan(who, after).source, "back in the day");
            Assert.AreEqual(1, planner.EndLifeStories(), "it can be ended at once (the cheat turned off)");
            Assert.IsFalse(planner.InLifeStory(who, at));
        }

        [Test]
        public void APick_GoesToTheBushInView_AndPicksThere()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            sim.DebugOpenCampGate();
            sim.DebugAddBushes(6);
            var planner = LifePlanner.Wired(sim);
            bool found = false; PersonRef who = default; ViewTime at = default; int cell = -1;
            for (long day = sim.State.month; day < sim.State.month + 12 && !found; day++)
            {
                if (!planner.StoryBushNear(0f, 0f, day, out cell, out _, out _)) continue;
                for (float h = 7f; h < 16.5f && !found; h += 0.25f)
                    foreach (var p in planner.People)
                    {
                        if (planner.StageOf(p) != LifeStage.Adult) continue;
                        var a = planner.Plan(p, new ViewTime(day, h));
                        if (StoryRules.FreeFor(a, LifeStage.Adult) && a.end - h > 0.6f) { who = p; at = new ViewTime(day, h); found = true; break; }
                    }
            }
            Assume.That(found, "a bush with berries and an idle grown-up");
            Assert.IsTrue(planner.StartStoryErrand(StoryKind.Pick, who, at, cell, out float secs, out string why), why);
            bool picked = false;
            for (float s = 0f; s <= secs; s += 0.25f)
            {
                var a = planner.Plan(who, LifeClock.Advance(at, s));
                if (a.kind == ActivityKind.Gather && a.place == Place.Land(PlaceKind.BushCell, cell) && !a.moving)
                {
                    picked = true;
                    Assert.AreEqual(StoryKind.Pick, StoryRules.StoryOf(a, out _), "it counts as a story in view");
                }
            }
            Assert.IsTrue(picked, "picks at the bush");
        }

        [Test]
        public void NobodyAsleep_AtWork_OrAKid_IsSentOnAnErrand()
        {
            var sim = Box(7);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            int tried = 0;
            foreach (var p in planner.People)
            {
                var night = new ViewTime(day, 2f);
                Assert.IsFalse(planner.StartStoryErrand(StoryKind.WaterRun, p, night, -1, out _, out _), $"{p} at 2:00");
                var t = new ViewTime(day, 9.5f);
                var a = planner.Plan(p, t);
                if (StoryRules.FreeFor(a, planner.StageOf(p)) && planner.StageOf(p) == LifeStage.Adult) continue;
                tried++;
                Assert.IsFalse(planner.StartStoryErrand(StoryKind.WaterRun, p, t, -1, out _, out string why), $"{p} doing {a.kind} ({planner.StageOf(p)})");
            }
            Assert.Greater(tried, 2);
            Assert.IsFalse(planner.StartStoryErrand(StoryKind.Pick, planner.People[0], new ViewTime(day, 9.5f), -1, out _, out _), "no bush: no pick");
        }
    }
}

