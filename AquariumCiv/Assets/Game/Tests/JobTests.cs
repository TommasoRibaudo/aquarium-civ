using System;
using System.Globalization;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Everyday jobs (#25, docs/design/notable-roles.md §1): every adult notable holds one, valid for the era and
    /// the buildings that exist. Jobs are view and story only: the economy never reads them.</summary>
    public class JobTests
    {
        /// <summary>Everything the economy and the dice produce, without chronicle text or ids (job lines add entries,
        /// which shift ids but nothing else).</summary>
        /// <summary>A hash of everything the economy owns. Floats go in as their exact bits: Unity's Mono and .NET format
        /// "R" differently, which made the same economy hash differently in the Editor and in Tools/SimTests.</summary>
        internal static uint Economy(SimState s)
        {
            string fp = s.population + "|" + s.month + "|" + Bits(s.food) + "|" + Bits(s.wood) + "|" + Bits(s.mood) + "|" +
                        Bits(s.grime) + "|" + Bits(s.overgrowth) + "|" +
                        s.rng.state + "|" + s.econRng.state + "|" + s.storyRng.state + "|" + s.era + "|" + s.eraReadyMonth + "|" +
                        string.Join(",", s.buildings.Select(b => b.type + b.slot)) + "|" + string.Join(",", s.trash.Select(t => t.slot)) + "|" +
                        string.Join(",", s.notables.Select(n => n.FullName + n.trait + n.alive + Bits(n.age))) + "|" + s.settlementName;
            return Fnv(fp);
        }

        static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

        static void Watched(SimCore sim, int hours)
        {
            for (int h = 0; h < hours; h++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
            }
        }

        [Test]
        public void TheEconomyIsWhatItWasBeforeJobs()
        {
#if UNITY_2017_1_OR_NEWER
            Assert.Ignore("Pinned hashes are measured under .NET (Tools/SimTests); Unity's Mono rounds some float math differently. The same-runtime comparisons run here.");
#endif
            var camp = NewSim(1234);
            camp.Advance(48);
            var town = NewSim(7, SweeperTests.NoSweepers()); // #60 changed the town on purpose: compared without its sweepers
            Watched(town, 150);
            Assert.AreEqual(Era.Town, town.State.era);
            Assert.AreEqual(2932785620u, Economy(camp.State), "camp"); // #61: weddings of an age and shared family names move the story stream (RoleTests' pins say how)
            // #27: at the town's era moment no one is free to be captain of the watch, so a notable steps out of the crowd
            // (Yara Brook, year 30). One more notable shifts the story dice from then on, as #26's rise does; roles draw no
            // dice themselves (RoleTests.TheEconomyIsWhatItWasBeforeRoles: runs without a crowd pull keep the old hash).
            Assert.AreEqual(428278537u, Economy(town.State), "town"); // re-measured for #61 (weddings of an age, shared family names)
            // #60: town sweepers clear heaps and trim the weeds, so the town has less mess, a quicker work pace (more food
            // and wood, more births), and every heap they clear lets a new one draw its spot from the main stream.
            var swept = NewSim(7);
            Watched(swept, 150);
            Assert.AreEqual(3088876224u, Economy(swept.State), "town with its sweepers (#60)");
        }

        /// <summary>Every adult has an open job of the era, children none, the retired are elders.</summary>
        static void AssertJobs(SimCore sim, string when)
        {
            foreach (var n in sim.State.notables.Where(n => n.alive))
            {
                string who = $"{when}: {n.FullName} ({n.age:0}, {n.job})";
                Assert.IsTrue(sim.JobValid(n), who);
                if (n.age < 16f) Assert.AreEqual(JobType.None, n.job, who);
                else if (n.age >= sim.Config.retireAge) Assert.AreEqual(JobType.Elder, n.job, who);
                else
                {
                    CollectionAssert.Contains(SimCore.JobsOf(sim.State.era).ToList(), n.job, who);
                    Assert.IsTrue(sim.JobOpen(n.job), who);
                }
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void EveryAdultNotableHasAJobValidForItsEra(int seed)
        {
            var sim = NewSim(seed);
            AssertJobs(sim, "founding");
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int h = 0; h < 120; h++)
            {
                sim.Advance(1);
                AssertJobs(sim, $"seed {seed} year {h + 1}");
                if (sim.State.eraReady) { sim.AdvanceEra(); AssertJobs(sim, $"seed {seed} year {h + 1}, {sim.State.era} began"); }
                foreach (var n in sim.State.notables.Where(n => n.alive)) seen.Add(sim.State.era + ":" + n.job);
            }
            TestContext.WriteLine($"seed {seed}: {sim.State.era}; jobs seen: {string.Join(", ", seen.OrderBy(x => x))}");
            Assert.AreEqual(Era.Town, sim.State.era, "the eras were advanced");
            Assert.GreaterOrEqual(seen.Count(x => x.StartsWith("Camp:") && x != "Camp:"), 3, "camp jobs vary");
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "coming_of_age"), "someone came of age");
        }

        [Test]
        public void ComingOfAgeNamesTheJob()
        {
            var sim = NewSim(1234);
            sim.Advance(48);
            var lines = sim.State.chronicle.Where(e => e.kind == "coming_of_age").ToList();
            Assert.IsNotEmpty(lines);
            foreach (var e in lines)
                StringAssert.Contains("came of age and took up work as a", e.text);
        }

        [Test]
        public void TheEraMomentChangesJobsInOneLine()
        {
            int lines = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = NewSim(seed);
                sim.Advance(20);
                bool campOnly = sim.State.notables.Any(n => n.alive && (n.job == JobType.Hunter || n.job == JobType.Gatherer || n.job == JobType.Toolmaker));
                int before = sim.State.chronicle.Count(e => e.kind == "job_era");
                Assert.IsTrue(sim.ForceNextEra());
                AssertJobs(sim, $"seed {seed} village");
                int added = sim.State.chronicle.Count(e => e.kind == "job_era") - before;
                Assert.AreEqual(campOnly ? 1 : 0, added, $"seed {seed}: one line for the whole move");
                if (added == 0) continue;
                lines++;
                var line = sim.State.chronicle.Last(e => e.kind == "job_era");
                StringAssert.StartsWith("With the village came new work: ", line.text);
                Assert.IsFalse(sim.State.chronicle.Any(e => e.kind == "job_change"), "an era move isn't a lost workplace");
            }
            Assert.GreaterOrEqual(lines, 8);
        }

        [Test]
        public void TheNearestJobComesFirst()
        {
            var sim = NewSim(3);
            sim.Advance(20);
            var n = sim.State.notables.First(x => x.alive && x.age >= 16f && x.age < 90f);
            sim.DebugSetJob(n, JobType.Gatherer);
            Assert.IsTrue(sim.State.buildings.Any(b => b.type == BuildingType.Farm));
            sim.ForceNextEra();
            Assert.AreEqual(JobType.Farmer, n.job, "gatherer → farmer (notable-roles.md §1)");
            StringAssert.Contains($"{n.FullName} went from gatherer to farmer", sim.State.chronicle.Last(e => e.kind == "job_era").text);
        }

        [Test]
        public void ALostWorkplaceChangesTheJob()
        {
            var sim = NewSim(3);
            sim.Advance(20);
            sim.ForceNextEra();
            var n = sim.State.notables.First(x => x.alive && x.age >= 16f && x.age < 90f);
            sim.DebugSetJob(n, JobType.Farmer);
            sim.State.buildings.RemoveAll(b => b.type == BuildingType.Farm); // nothing removes buildings yet; a flood or fire may
            Assert.IsFalse(sim.JobValid(n));
            sim.State.wood = 0; // so this month can't build a new farm
            sim.Step();
            Assert.AreNotEqual(JobType.Farmer, n.job);
            Assert.IsTrue(sim.JobValid(n));
            var line = sim.State.chronicle.Last(e => e.kind == "job_change" && e.notableId == n.id); // every farmer gets their own line
            StringAssert.Contains($"{n.FullName} had no farm to work at any more, and became a", line.text);
        }

        [Test]
        public void TheVeryOldRetireToElder()
        {
            var sim = NewSim(5);
            var n = sim.State.notables[0];
            n.age = sim.Config.retireAge - 0.5f;
            n.lifespan = 200f;
            string job = n.job;
            Assert.AreNotEqual(JobType.None, job);
            sim.Advance(1);
            Assert.AreEqual(JobType.Elder, n.job);
            var line = sim.State.chronicle.Last(e => e.kind == "retired");
            Assert.AreEqual(n.id, line.notableId);
            StringAssert.Contains($"a {job}", line.text);
            StringAssert.Contains(((int)sim.Config.retireAge).ToString(), line.text);
            sim.Advance(1);
            Assert.AreEqual(1, sim.State.chronicle.Count(e => e.kind == "retired"), "one line");
        }

        [Test]
        public void ChildrenHaveNoJob()
        {
            var sim = NewSim(9);
            var child = sim.DebugBirth();
            sim.Step();
            Assert.AreEqual(JobType.None, child.job);
            sim.DebugSetJob(child, JobType.Fisher);
            sim.Step();
            Assert.AreEqual(JobType.None, child.job, "a child's job is taken away again");
        }

        [Test]
        public void TraitsLeanToTheirJobs()
        {
            int braveHunters = 0, brave = 0, otherHunters = 0, other = 0;
            for (int seed = 1; seed <= 200; seed++)
                foreach (var n in NewSim(seed).State.notables)
                {
                    if (n.trait == "the brave") { brave++; if (n.job == JobType.Hunter) braveHunters++; }
                    else if (n.trait != "the loud") { other++; if (n.job == JobType.Hunter) otherHunters++; }
                }
            float b = braveHunters / (float)brave, o = otherHunters / (float)other;
            TestContext.WriteLine($"hunters: {b:P0} of the brave, {o:P0} of the rest");
            Assert.Greater(b, o * 2f);
        }

        [Test]
        public void JobsAreSpreadOverTheOpenJobs()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var jobs = NewSim(seed).State.notables.Select(n => n.job).ToList();
                Assert.GreaterOrEqual(jobs.Distinct().Count(), System.Math.Min(3, jobs.Count) - 1, $"seed {seed}: {string.Join(",", jobs)}");
            }
        }

        [Test]
        public void JobsAreDeterministicAndDrawNoDice()
        {
            var a = NewSim(77); var b = NewSim(77);
            a.Advance(60); b.Advance(60);
            Assert.AreEqual(string.Join(",", a.State.notables.Select(n => n.job)), string.Join(",", b.State.notables.Select(n => n.job)));

            // Jobs that keep getting scrambled leave the economy and every dice stream exactly as they were.
            var plain = NewSim(77); var meddled = NewSim(77);
            for (int m = 0; m < 12 * 80; m++)
            {
                foreach (var n in meddled.State.notables) n.job = m % 2 == 0 ? JobType.Smith : JobType.None;
                plain.Step(); meddled.Step();
                if (m == 12 * 30) { plain.ForceNextEra(); meddled.ForceNextEra(); }
            }
            Assert.AreEqual(Economy(plain.State), Economy(meddled.State));
        }

        [Test]
        public void TheJobIsSaved()
        {
            var sim = NewSim(4);
            sim.Advance(10);
            var loaded = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            CollectionAssert.AreEqual(sim.State.notables.Select(n => n.job), loaded.notables.Select(n => n.job));
        }

        [Test]
        public void ASaveFromBeforeJobsGetsJobsNextMonth()
        {
            var sim = NewSim(4);
            sim.Advance(10);
            var old = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            foreach (var n in old.State.notables) n.job = JobType.None; // what a save without the field loads as
            Assert.IsTrue(old.State.notables.Any(n => n.alive && n.age >= 16f && !old.JobValid(n)));
            int before = old.State.chronicle.Count;
            old.Step();
            AssertJobs(old, "after load");
            Assert.IsFalse(old.State.chronicle.Skip(before).Any(e => e.kind.StartsWith("job")), "a first job needs no line");
        }

        [Test]
        public void TheTownHasItsOwnWork()
        {
            var sim = NewSim(7);
            Watched(sim, 150);
            var open = sim.OpenJobs();
            CollectionAssert.Contains(open, JobType.Sweeper);
            CollectionAssert.DoesNotContain(open, JobType.Hunter);
            CollectionAssert.DoesNotContain(open, JobType.Mason, "no quarry or stone in the sim yet");
            if (sim.Count(BuildingType.Market) > 0) CollectionAssert.Contains(open, JobType.Stallholder);
        }

        [Test]
        public void DebugCheats()
        {
            var sim = NewSim(11);
            var n = sim.State.notables[0];
            sim.DebugSetJob(n, JobType.Toolmaker);
            Assert.AreEqual(JobType.Toolmaker, n.job);
            sim.DebugRedrawJobs();
            AssertJobs(sim, "redrawn");
        }
    }
}
