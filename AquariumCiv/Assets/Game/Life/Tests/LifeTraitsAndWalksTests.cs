using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#64: everyone's own trait shows in their day (a lazy notable without a role naps now and then, more weakly
    /// than a lazy holder), and an interrupted walk never jumps: a role beat redirects it, a Keeper reaction stops the
    /// person where they are, a live scene walks them over from there, and afterwards they re-route from where they stand.</summary>
    public class LifeTraitsAndWalksTests
    {
        static SimCore SimIn(Era era, int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assert.AreEqual(era, sim.State.era, $"seed {seed} reaches {era}");
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        /// <summary>How far one step may move a person: one path cell.</summary>
        const float MaxStep = SiteMap.Cell;
        /// <summary>The sampling step in view hours (about one frame at 30 fps in the slow daytime hours).</summary>
        const float Dt = 0.0005f;

        // ------------------------------------------------------------------ everyone's own trait

        static List<Notable> AdultNonHolders(SimCore sim) =>
            sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge + 1f && n.age < sim.Config.retireAge - 1f && n.job != JobType.Elder && sim.RoleOf(n) == null)
                .OrderBy(n => n.id).Take(6).ToList();

        static int Naps(SimCore sim, IEnumerable<Notable> who, long day0, int days)
        {
            var planner = LifePlanner.Wired(sim);
            var segs = new List<PersonActivity>();
            int naps = 0;
            for (long d = day0; d < day0 + days; d++)
                foreach (var n in who)
                {
                    planner.DaySchedule(PersonRef.Notable(n.id), d, segs);
                    naps += segs.Count(a => a.kind == ActivityKind.Nap);
                }
            return naps;
        }

        [TestCase(Era.Village, 7)]
        [TestCase(Era.Town, 1234)]
        public void ALazyNonHolderNapsMoreThanABusyOne(Era era, int seed)
        {
            var sim = SimIn(era, seed);
            var who = AdultNonHolders(sim);
            Assume.That(who.Count, Is.GreaterThanOrEqualTo(1), "grown notables without a role");
            long day0 = sim.State.month;
            const int Days = 40;
            foreach (var n in who) n.trait = "the lazy";
            int lazy = Naps(sim, who, day0, Days);
            foreach (var n in who) n.trait = "the tinkerer"; // busy at the bench, no idleness
            int busy = Naps(sim, who, day0, Days);
            sim.TermFlavourOn = false;
            foreach (var n in who) n.trait = "the lazy";
            int off = Naps(sim, who, day0, Days);
            sim.TermFlavourOn = true;
            TestContext.WriteLine($"{era} seed {seed}: {who.Count} non-holders over {Days} view days: lazy {lazy} naps, busy {busy}, lazy with the flavour off {off}");
            Assert.Greater(lazy, busy, "a lazy notable without a role naps more than a busy one");
            Assert.Greater(lazy, Days * who.Count / 10, "the lazy nap on a fair share of days");
            Assert.Less(lazy, Days * who.Count * 3 / 4, "but not every day: weaker than a holder");
            Assert.AreEqual(0, off, "the flavour off: no colour");
        }

        [Test]
        public void AHoldersColourIsStrongerThanANonHoldersWithTheSameTrait()
        {
            var sim = SimIn(Era.Village, 7);
            string role = SimCore.LeaderOf(sim.State.era);
            var leader = sim.HolderOf(role);
            Assume.That(leader, Is.Not.Null);
            var other = AdultNonHolders(sim).First();
            var hints = new TermLifeHints(sim);
            foreach (var trait in new[] { "the lazy", "the singer", "the kind", "the worrier", "the dreamer" })
            {
                leader.trait = other.trait = trait;
                var h = hints.Person(leader, role);
                var o = hints.Person(other, null);
                float hs = h.napChance + h.singChance + h.sitWithOldChance + h.hillChance + h.homeEarlyHours;
                float os = o.napChance + o.singChance + o.sitWithOldChance + o.hillChance + o.homeEarlyHours;
                TestContext.WriteLine($"{trait}: holder {hs:0.00}, non-holder {os:0.00}");
                Assert.Greater(os, 0f, $"{trait}: the non-holder shows their trait");
                Assert.Greater(hs, os, $"{trait}: the holder's colour is stronger");
                Assert.AreEqual(hs * TermLifeHints.NonHolderStrength, os, 1e-4f, $"{trait}: at half strength");
                Assert.AreEqual(default(BoxHints), hints.Office(other, null), "no role, no colour for the box");
            }
        }

        // ------------------------------------------------------------------ interrupted walks

        /// <summary>Samples a person's position over [from, to) and returns the largest move in one step.</summary>
        static float MaxJump(LifePlanner planner, PersonRef who, long day, float from, float to, out int samples, out float worstAt)
        {
            float worst = 0f;
            worstAt = from;
            samples = 0;
            bool has = planner.PositionAt(who, new ViewTime(day, from), out float px, out float pz);
            Assert.IsTrue(has, $"{who} has a position at {from:0.000}");
            for (float h = from + Dt; h < to; h += Dt)
            {
                Assert.IsTrue(planner.PositionAt(who, new ViewTime(day, h), out float x, out float z), $"{who} has a position at {h:0.000}");
                float d = (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                if (d > worst) { worst = d; worstAt = h; }
                px = x; pz = z;
                samples++;
            }
            return worst;
        }

        static float Dist(LifePlanner planner, PersonRef who, ViewTime t, float x, float z)
        {
            Assert.IsTrue(planner.PositionAt(who, t, out float px, out float pz));
            return (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
        }

        /// <summary>A grown notable's long walk on a plain day (no scene), and the walk's destination spot.</summary>
        static bool FindWalk(LifePlanner planner, long day, out PersonRef who, out PersonActivity walk)
        {
            var segs = new List<PersonActivity>();
            foreach (var p in planner.People.Where(p => p.IsNotable && planner.StageOf(p) == LifeStage.Adult))
            {
                planner.DaySchedule(p, day, segs);
                if (segs.Any(a => a.source == ActivitySource.Scene)) continue;
                for (int i = 0; i < segs.Count; i++)
                {
                    var a = segs[i];
                    if (a.kind != ActivityKind.Walk || !a.moving || a.end - a.start < 0.15f || a.start < 6f || a.end > 19f) continue;
                    if (!planner.Positions.TryGetPosition(a.from, out float fx, out float fz) || !planner.Positions.TryGetPosition(a.place, out float tx, out float tz)) continue;
                    if ((fx - tx) * (fx - tx) + (fz - tz) * (fz - tz) < 1f) continue; // a walk worth watching
                    who = p; walk = a;
                    return true;
                }
            }
            who = default; walk = default;
            return false;
        }

        [TestCase(Era.Village, 7)]
        [TestCase(Era.Town, 1234)]
        public void AReactionStopsAWalkWhereItIsAndItCarriesOnFromThere(Era era, int seed)
        {
            var sim = SimIn(era, seed);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            PersonRef who = default;
            PersonActivity walk = default;
            for (int i = 0; i < 12 && !FindWalk(planner, day, out who, out walk); i++) day++;
            Assume.That(who.IsNone, Is.False, "a long walk to watch");
            Assert.IsTrue(planner.Positions.TryGetPosition(walk.place, out float tx, out float tz));

            float plainJump = MaxJump(planner, who, day, walk.start - 0.2f, walk.end + 0.8f, out _, out _);
            float mid = walk.start + (walk.end - walk.start) * 0.4f;
            planner.React(KeeperGesture.TapPerson, new ViewTime(day, mid), default, who, 1f, 2f);
            var r = planner.Plan(who, new ViewTime(day, mid + 0.001f));
            Assert.AreEqual(ActivitySource.Reaction, r.source, "the tapped walker reacts");
            Assert.IsTrue(planner.PositionAt(who, new ViewTime(day, mid), out float sx, out float sz));
            float rEnd = r.end;

            float jump = MaxJump(planner, who, day, walk.start - 0.2f, walk.end + 0.8f, out int samples, out float at);
            float still = Dist(planner, who, new ViewTime(day, rEnd - 0.001f), sx, sz);
            float toDest = Dist(planner, who, new ViewTime(day, rEnd + 0.001f), tx, tz);
            float stopToDest = (float)Math.Sqrt((sx - tx) * (sx - tx) + (sz - tz) * (sz - tz));
            var after = planner.Plan(who, new ViewTime(day, rEnd + 0.002f));
            TestContext.WriteLine($"{era} seed {seed}: {who} walks {walk.from} -> {walk.place} {walk.start:0.000}-{walk.end:0.000}; tapped at {mid:0.000} for " +
                                  $"{(rEnd - mid) * 60f:0.0} view minutes; largest step {jump:0.000} (at {at:0.000}; plain day {plainJump:0.000}) over {samples} samples; " +
                                  $"{stopToDest:0.00} from the destination when stopped, {toDest:0.00} just after; then {after.kind} to {after.place}");
            Assert.Less(still, 1e-3f, "they react where they stopped");
            Assert.Greater(toDest, stopToDest - 0.2f, "they don't jump ahead to the walk's destination");
            Assert.AreEqual(ActivityKind.Walk, after.kind, "they walk on afterwards");
            // Towards where they were going, if they still have time to stop there (else on to their next stop).
            var day1 = new List<PersonActivity>();
            planner.DaySchedule(who, day, day1);
            int wi = day1.FindIndex(x => Math.Abs(x.start - walk.start) < 1e-4f && x.kind == walk.kind);
            if (wi >= 0 && wi + 1 < day1.Count && day1[wi + 1].end > rEnd + LifePlanner.MaxWalk + 0.05f)
                Assert.AreEqual(walk.place, after.place, "towards where they were going");
            Assert.LessOrEqual(jump, MaxStep, "never more than one cell per step across the interruption");
        }

        [TestCase(Era.Village, 8)]
        [TestCase(Era.Town, 1234)]
        public void ALiveSceneWalksThemOverFromWhereTheyAreAndBack(Era era, int seed)
        {
            var sim = SimIn(era, seed);
            var planner = LifePlanner.Wired(sim);
            long day = sim.State.month;
            PersonRef who = default;
            PersonActivity walk = default;
            for (int i = 0; i < 12 && !FindWalk(planner, day, out who, out walk); i++) day++;
            Assume.That(who.IsNone, Is.False, "a long walk to watch");
            var friend = planner.People.First(p => p.IsNotable && p != who && planner.StageOf(p) == LifeStage.Adult);
            float mid = walk.start + (walk.end - walk.start) * 0.5f;
            planner.StartScene(SceneKind.Chat, new ViewTime(day, mid), who.notableId, friend.notableId, -1, 0.5f);
            var a = planner.Plan(who, new ViewTime(day, mid + 0.01f));
            Assert.AreEqual(ActivitySource.Scene, a.source, "the scene interrupts the walk");
            float jump = MaxJump(planner, who, day, walk.start - 0.2f, mid + 1.6f, out int samples, out float at);
            TestContext.WriteLine($"{era} seed {seed}: {who} walking {walk.from} -> {walk.place}, called to a chat at {a.place} at {mid:0.000} ({a.kind}); " +
                                  $"largest step {jump:0.000} at {at:0.000} over {samples} samples");
            Assert.LessOrEqual(jump, MaxStep, "never more than one cell per step into the scene and back into the day");
            Assert.AreEqual(ActivityKind.Chat, planner.Plan(who, new ViewTime(day, mid + 0.4f)).kind, "they chat once they get there");
        }

        [TestCase(Era.Village, 7)]
        [TestCase(Era.Town, 1234)]
        public void WalksCutByRoleBeatsAndScenesNeverJump(Era era, int seed)
        {
            var sim = SimIn(era, seed);
            var planner = LifePlanner.Wired(sim);
            long day0 = sim.State.month;
            const int Days = 3;
            float worst = 0f;
            string worstAt = "";
            int samples = 0, doubled = 0;
            var segs = new List<PersonActivity>();
            for (long d = day0; d < day0 + Days; d++)
                foreach (var who in planner.People)
                {
                    float j = MaxJump(planner, who, d, 0f, 24f - Dt, out int n, out float at);
                    samples += n;
                    if (j > worst) { worst = j; worstAt = $"{who} day {d} at {at:0.000}"; }
                    // A cut walk used to run on to its old destination (squeezed into the time before the beat) and only then
                    // walk to the beat; the rest of it walked back to where it started first. Either shows as a walk that
                    // goes straight on into another walk from its end.
                    planner.DaySchedule(who, d, segs);
                    for (int i = 1; i < segs.Count; i++)
                        if (segs[i - 1].kind == ActivityKind.Walk && segs[i - 1].moving && segs[i].kind == ActivityKind.Walk && segs[i].moving &&
                            (segs[i].source != ActivitySource.Schedule || segs[i - 1].source != ActivitySource.Schedule)) doubled++;
                }
            TestContext.WriteLine($"{era} seed {seed}: {planner.InterruptedWalks} walks cut by a beat, scene, the weather or a relationship in {Days} days; " +
                                  $"{doubled} walks straight into another walk; largest step {worst:0.000} ({worstAt}) over {samples} samples of {planner.People.Count} people");
            Assert.Greater(planner.InterruptedWalks, 0, "some walks were interrupted");
            Assert.AreEqual(0, doubled, "a walk cut by a beat or a scene heads straight for it, and on from there afterwards");
            Assert.LessOrEqual(worst, MaxStep, "nobody moves more than one cell per step all day");
        }

        // ------------------------------------------------------------------ cost

        [Test]
        public void PositionsCostLittle()
        {
            var sim = SimIn(Era.Town, 7);
            var planner = LifePlanner.Wired(sim);
            var acts = new List<PersonActivity>();
            long day = sim.State.month;
            for (float h = 0; h < 24f; h += 1f) planner.PlanAll(new ViewTime(day, h), acts); // JIT
            var sw = new Stopwatch();
            int steps = 0;
            for (int d = 0; d < 10; d++)
            {
                sim.Step();
                day = sim.State.month;
                planner.React(KeeperGesture.HoldFinger, new ViewTime(day, 10f));
                planner.React(KeeperGesture.TapGlass, new ViewTime(day, 15f));
                planner.StartScene(SceneKind.EveningTalk, new ViewTime(day, 17.5f));
                sw.Start();
                for (float h = 0f; h < 24f; h += 0.25f)
                {
                    var t = new ViewTime(day, h);
                    planner.PlanAll(t, acts);
                    foreach (var a in acts) planner.PositionAt(a.who, t, out _, out _);
                    steps++;
                }
                sw.Stop();
                planner.ClearLive();
            }
            double ms = sw.Elapsed.TotalMilliseconds / steps;
            TestContext.WriteLine($"[LifeWalks] Planning and placing the whole town box ({acts.Count} people) with live reactions and a scene: " +
                                  $"{ms:0.000} ms per view-time step on average over {steps} steps (" +
#if UNITY_2017_1_OR_NEWER
                "Unity Mono)");
            Assert.Less(ms, 2.0, "Mono is slower than .NET; the budget is measured in .NET (Tools/SimTests)");
#else
                ".NET)");
            Assert.Less(ms, 0.5, "<= 0.5 ms per whole-box step in .NET");
#endif
        }
    }
}
