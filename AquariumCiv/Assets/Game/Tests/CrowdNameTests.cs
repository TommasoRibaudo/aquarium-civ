using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#148: a crowd person's first name, stable across sessions, carried over when they step out of the crowd.</summary>
    public class CrowdNameTests
    {
        static SimCore Reloaded(SimCore sim) => new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));

        static List<string> Names(SimCore sim) => Enumerable.Range(0, sim.CrowdCount).Select(sim.CrowdName).ToList();

        /// <summary>A long box, eras taken as they come, calling <paramref name="beforeMonth"/> before each month.</summary>
        static void Live(SimCore sim, int years, System.Action beforeMonth = null)
        {
            for (int m = 0; m < years * SimConfig.MonthsPerYear; m++)
            {
                beforeMonth?.Invoke();
                sim.Advance(sim.Config.StepHours);
                if (sim.State.eraReady) sim.AdvanceEra();
            }
        }

        [Test]
        public void TheTableIsItsOwnAndBigEnough()
        {
            var t = NameData.CrowdGiven;
            Assert.AreEqual(t.Length, t.Distinct().Count());
            Assert.IsEmpty(t.Intersect(NameData.Given), "a crowd name reaches a notable only by rising");
            Assert.GreaterOrEqual(t.Length, 150, "a full box of 80, and the ones who rise");
        }

        [Test]
        public void EveryoneInTheCrowdHasADifferentName()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var sim = NewSim(seed);
                Live(sim, 60);
                var names = Names(sim);
                Assert.Greater(names.Count, 5, $"seed {seed}");
                Assert.IsTrue(names.All(n => !string.IsNullOrEmpty(n)));
                Assert.AreEqual(names.Count, names.Distinct().Count(), $"seed {seed}: {string.Join(", ", names)}");
                var notables = sim.State.notables.Where(n => n.alive).Select(n => n.name);
                Assert.IsEmpty(names.Intersect(notables), $"seed {seed}: a crowd person shares a name with a living notable");
            }
        }

        [Test]
        public void TheNameIsTheSameAfterASaveAndReload()
        {
            var sim = NewSim(11);
            Live(sim, 30);
            var before = Names(sim);
            Assert.Greater(before.Count, 3);
            var back = Reloaded(sim);
            CollectionAssert.AreEqual(before, Names(back));
        }

        [Test]
        public void ANameStaysWithItsIndexOverTheYears()
        {
            var sim = NewSim(8);
            Live(sim, 10);
            var early = Names(sim);
            int stepped = 0;
            for (int y = 0; y < 40; y++)
            {
                int risen = sim.State.chronicle.Count(e => e.kind == "rise" || e.kind == "role_rise");
                Live(sim, 1);
                stepped += sim.State.chronicle.Count(e => e.kind == "rise" || e.kind == "role_rise") - risen;
            }
            var late = Names(sim);
            // Only a place someone stepped out of gets a new name.
            int differ = Enumerable.Range(0, System.Math.Min(early.Count, late.Count)).Count(i => early[i] != late[i]);
            Assert.LessOrEqual(differ, stepped, $"{differ} names changed, {stepped} people rose");
        }

        [Test]
        public void AnOldSaveLoadsAndFillsTheSameNames()
        {
            // A save from before #148: the two fields aren't in the file at all. No version bump: they read as empty.
            var sim = NewSim(21);
            Live(sim, 20);
            string path = Path.Combine(Path.GetTempPath(), "crowd-old-save-" + System.Guid.NewGuid().ToString("N"), "save.json");
            SaveStore.Write(path, sim.State, System.DateTime.UtcNow);
            string json = File.ReadAllText(path);
            string old = Regex.Replace(json, @",""crowdNames"":\[[^\]]*\]", "");
            old = Regex.Replace(old, @",""crowdNameNext"":\d+", "");
            Assert.AreNotEqual(json, old);
            StringAssert.DoesNotContain("crowdName", old);
            File.WriteAllText(path, old);
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var e));
            Assert.AreEqual(SaveStore.CurrentVersion, e.v);
            var state = e.state;
            Assert.IsEmpty(state.crowdNames);
            Assert.AreEqual(0, state.crowdNameNext);

            var loaded = new SimCore(sim.Config, state);
            var names = Names(loaded);
            Assert.AreEqual(loaded.CrowdCount, names.Count);
            Assert.IsTrue(names.All(n => !string.IsNullOrEmpty(n)));
            CollectionAssert.AreEqual(names, Names(Reloaded(loaded)), "and they stay after the next save");
            Live(loaded, 5);
        }

        [Test]
        public void TheNamesDrawNoDice()
        {
            var a = NewSim(6);
            var b = NewSim(6);
            var read = new List<string>();
            Live(a, 25, () => read.AddRange(Names(a))); // asking for names every month
            Live(b, 25);
            Assert.AreEqual(b.State.rng.state, a.State.rng.state);
            Assert.AreEqual(b.State.econRng.state, a.State.econRng.state);
            Assert.AreEqual(b.State.storyRng.state, a.State.storyRng.state);
            CollectionAssert.AreEqual(Names(b), Names(a), "asking early changes nothing");
        }

        [Test]
        public void SomeoneWhoRisesCarriesTheirName()
        {
            int rises = 0, carried = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = NewSim(seed);
                var before = new List<string>(); int known = 0;
                Live(sim, 110, () =>
                {
                    var s = sim.State;
                    for (int i = known; i < s.notables.Count; i++)
                    {
                        var n = s.notables[i];
                        var line = s.chronicle.FirstOrDefault(e => e.notableId == n.id && (e.kind == "rise" || e.kind == "role_rise"));
                        if (line == null) continue;
                        CollectionAssert.Contains(NameData.CrowdGiven, n.name, $"seed {seed}: {line.text}");
                        StringAssert.Contains(n.name, line.text, "the line tells the name they carried");
                        CollectionAssert.DoesNotContain(Names(sim), n.name, "nobody in the crowd has it now");
                        rises++;
                        if (before.Contains(n.name)) carried++; // else they joined the crowd that same month
                    }
                    known = s.notables.Count;
                    Names(sim);
                    before = new List<string>(s.crowdNames);
                });
            }
            TestContext.WriteLine($"{rises} rose, {carried} with a name the crowd already had that month");
            Assert.GreaterOrEqual(carried * 10, rises * 8, "almost always a name the crowd had the month before");
            Assert.Greater(rises, 0, "someone rose in ten long boxes");
        }

        [Test]
        public void ADebugRiseCarriesTheLastName()
        {
            var sim = NewSim(2);
            sim.State.population = 12;
            int top = sim.CrowdCount - 1;
            string name = sim.CrowdName(top);
            var n = sim.ForceCrowdRise();
            Assert.IsNotNull(n);
            Assert.AreEqual(name, n.name);
            Assert.AreEqual(top, sim.CrowdCount, "their place in the crowd has gone");
            Assert.IsNull(sim.CrowdName(top));

            sim.State.population++; // someone comes back to that index: a new person
            Assert.IsNotNull(sim.CrowdName(top));
            Assert.AreNotEqual(name, sim.CrowdName(top));
        }

        [Test]
        public void ACardShowsNameJobAndWhatTheyreDoing()
        {
            var sim = NewSim(3);
            sim.State.population = 12;
            string name = sim.CrowdName(0);
            var c = sim.CrowdCardOf(0, "cook", "Adult", "One of the people is tending the pot at the fire with a neighbour.");
            Assert.AreEqual(0, c.index);
            Assert.AreEqual(name, c.name);
            Assert.AreEqual("adult", c.stage);
            Assert.AreEqual("cook", c.jobLine);
            Assert.AreEqual($"{name} is tending the pot at the fire with a neighbour.", c.doing);

            Assert.AreEqual("helper", sim.CrowdCardOf(0, "", "Adult", null).jobLine);
            Assert.AreEqual($"{name} is out and about.", sim.CrowdCardOf(0, "", "Adult", null).doing);
            Assert.AreEqual("child", sim.CrowdCardOf(0, "", "Kid", "A child is playing at the green.").jobLine);
            Assert.AreEqual("baby", sim.CrowdCardOf(0, "", "Baby", "A child is asleep at home.").jobLine);
            Assert.AreEqual("elder", sim.CrowdCardOf(0, null, "Elder", "An old one is napping at home.").jobLine);
        }

        [Test]
        public void NoCardForSomeoneWhoIsNotThere()
        {
            var sim = NewSim(3);
            Assert.IsNull(sim.CrowdCardOf(-1, "", "Adult", ""));
            Assert.IsNull(sim.CrowdCardOf(sim.CrowdCount, "", "Adult", ""));
            Assert.IsNull(sim.CrowdName(sim.CrowdCount));
        }

        [Test]
        public void ANewBoxWalksTheTableItsOwnWay()
        {
            var a = NewSim(1); var b = NewSim(2);
            a.State.population = 20; b.State.population = 20;
            CollectionAssert.AreNotEqual(Names(a), Names(b));
        }
    }
}
