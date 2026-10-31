using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#66: a bigger medieval name pool; no two living notables share a given name, and no full name ever comes back.</summary>
    public class NameTests
    {
        static string Run(int seed, bool check)
        {
            var sim = NewSim(seed);
            for (int y = 0; y < 120; y++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
                if (!check) continue;
                var alive = sim.State.notables.Where(n => n.alive).ToList();
                Assert.AreEqual(alive.Count, alive.Select(n => n.name).Distinct().Count(), $"seed {seed} year {y}: two living share a given name: {string.Join(", ", alive.Select(n => n.FullName))}");
                var all = sim.State.notables;
                Assert.AreEqual(all.Count, all.Select(n => n.FullName).Distinct().Count(), $"seed {seed} year {y}: a full name repeated");
            }
            return string.Join("|", sim.State.notables.Select(n => n.FullName));
        }

        [Test]
        public void NoDuplicateNamesOver12Seeds120Years()
        {
            int people = 0;
            for (int seed = 1; seed <= 12; seed++) { Run(seed, true); people += NewSimCount(seed); }
            Assert.Greater(people, 12 * 8, "enough people were named to mean something");
        }

        static int NewSimCount(int seed) => Run(seed, false).Split('|').Length;

        [Test]
        public void NamesAreDeterministic()
        {
            for (int seed = 1; seed <= 4; seed++) Assert.AreEqual(Run(seed, false), Run(seed, false));
        }

        [Test]
        public void TablesAreBigAndClean()
        {
            Assert.GreaterOrEqual(NameData.Given.Length, 150);
            Assert.GreaterOrEqual(NameData.Families.Length, 60);
            Assert.AreEqual(NameData.Given.Length, NameData.Given.Distinct().Count());
            Assert.AreEqual(NameData.Families.Length, NameData.Families.Distinct().Count());
        }
    }
}
