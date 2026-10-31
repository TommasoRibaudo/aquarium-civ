using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#67: when a job opens and no notable holds it, a suitable notable takes it (one chronicle line), without
    /// churn and without touching the economy.</summary>
    public class JobFillTests
    {
        static bool Worker(Notable n, SimCore sim) => n.alive && n.age >= 16f && n.age < sim.Config.retireAge;

        [Test]
        public void AnOpenJobGetsSomeoneWithinAYearOverManyYears()
        {
            int worstStreak = 0;
            foreach (int seed in new[] { 3, 11, 29 })
            {
                var sim = NewSim(seed);
                var empty = new System.Collections.Generic.Dictionary<string, int>();
                int fills = 0;
                for (int m = 0; m < 12 * 120; m++)
                {
                    sim.Step();
                    if (sim.State.eraReady) sim.AdvanceEra();
                    var workers = sim.State.notables.Where(n => Worker(n, sim)).ToList();
                    foreach (var job in sim.OpenJobs())
                    {
                        // fillable: no holder, and a crowded job exists to take someone from
                        bool vacant = !workers.Any(n => n.job == job);
                        bool fillable = workers.GroupBy(n => n.job).Any(g => g.Key != JobType.Elder && g.Count() >= 2);
                        empty[job] = vacant && fillable ? (empty.TryGetValue(job, out int c) ? c + 1 : 1) : 0;
                        if (empty[job] > worstStreak) { worstStreak = empty[job]; TestContext.WriteLine($"seed {seed} month {m} {job} streak {worstStreak} era {sim.State.era}"); }
                    }
                }
                fills = sim.State.chronicle.Count(e => e.kind == "job_fill");
                TestContext.WriteLine($"seed {seed}: {fills} job_fill lines, worst vacant streak so far {worstStreak} months");
                Assert.LessOrEqual(fills, 15, $"seed {seed}: job-change spam");
            }
            Assert.LessOrEqual(worstStreak, 12, "an open job stayed empty for more than a year while someone could take it");
        }

        [Test]
        public void TheFarmOpeningPullsOneGathererToTheFields()
        {
            var sim = NewSim(5);
            sim.ForceNextEra();
            sim.State.buildings.RemoveAll(b => b.type == BuildingType.Farm);
            sim.Step();
            var workers = sim.State.notables.Where(x => Worker(x, sim)).ToList();
            Assert.GreaterOrEqual(workers.Count, 3);
            foreach (var n in workers) { sim.DebugSetJob(n, JobType.Fisher); n.jobChangedMonth = -1000000; } // a crowded, valid job, free to move
            Assert.IsNotNull(sim.DebugBuild(BuildingType.Farm));
            sim.Step();
            var farmers = workers.Where(n => n.job == JobType.Farmer).ToList();
            Assert.AreEqual(1, farmers.Count);
            var line = sim.State.chronicle.Single(e => e.kind == "job_fill" && e.text.Contains("farm opened"));
            StringAssert.Contains("farm opened", line.text);
            StringAssert.Contains(farmers[0].FullName, line.text);
            sim.Step();
            Assert.AreEqual(1, workers.Count(n => n.job == JobType.Farmer), "no more than one farmer is drawn");
        }

        [Test]
        public void NobodyLeavesAJobTheyAloneHold()
        {
            var sim = NewSim(5); // camp era: gatherer, hunter, fisher, woodcutter need nothing built; the toolmaker needs the camp
            var workers = sim.State.notables.Where(x => Worker(x, sim)).ToList();
            var jobs = new[] { JobType.Fisher, JobType.Woodcutter, JobType.Gatherer };
            for (int i = 0; i < workers.Count; i++)
            {
                if (i < jobs.Length) sim.DebugSetJob(workers[i], jobs[i]);
                else workers[i].alive = false; // everyone left holds a job alone
            }
            workers = workers.Take(jobs.Length).ToList();
            Assert.AreEqual(jobs.Length, workers.Count, "need three workers for this test");
            var before = workers.Select(w => w.job).ToList();
            int lines = sim.State.chronicle.Count(e => e.kind == "job_fill");
            sim.Step(); // one month: no one is yet of age to make a crowd
            CollectionAssert.AreEqual(before, workers.Select(w => w.job).ToList());
            Assert.AreEqual(lines, sim.State.chronicle.Count(e => e.kind == "job_fill"));
        }

        [Test]
        public void ElderAndChildrenAreNeverPicked()
        {
            var sim = NewSim(5);
            sim.ForceNextEra();
            sim.State.buildings.RemoveAll(b => b.type == BuildingType.Farm);
            sim.Step();
            sim.DebugBuild(BuildingType.Farm);
            sim.Step();
            foreach (var n in sim.State.notables.Where(x => x.alive && (x.age < 16f || x.age >= sim.Config.retireAge)))
                Assert.AreNotEqual(JobType.Farmer, n.job);
        }

        [Test]
        public void FillingDrawsNoDiceAndIsDeterministic()
        {
            var a = NewSim(21); var b = NewSim(21);
            for (int m = 0; m < 12 * 60; m++) { a.Step(); b.Step(); if (a.State.eraReady) a.AdvanceEra(); if (b.State.eraReady) b.AdvanceEra(); }
            Assert.AreEqual(string.Join(",", a.State.notables.Select(n => n.job)), string.Join(",", b.State.notables.Select(n => n.job)));
            Assert.AreEqual(JobTests.Economy(a.State), JobTests.Economy(b.State));
        }
    }
}
