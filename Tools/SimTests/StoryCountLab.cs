// #715 (research 21): how many stories of each kind a box tells, per 10 years, over a 100-year run. Explicit: run by name,
//   dotnet test Tools/SimTests --filter "FullyQualifiedName~StoryCountLab" --logger "console;verbosity=detailed"
// The removed kinds (weather, trait stories, lost in the hills, strange light, sheep, ducks) must count 0 after #715.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    public class StoryCountLab
    {
        static string[] StoryKinds => WritingWeedingTests.StoryKinds;
        static Dictionary<string, int> Count(bool ecology, int seed) => WritingWeedingTests.StoryCounts(ecology, seed);

        [Test, Explicit("#715: the stories per kind per 10 years, a 100-year run")]
        public void StoriesPerTenYears()
        {
            var sb = new StringBuilder();
            foreach (bool eco in new[] { true, false })
            {
                var sum = StoryKinds.ToDictionary(k => k, k => 0);
                int[] seeds = { 7, 21, 42 };
                foreach (int seed in seeds) foreach (var kv in Count(eco, seed)) sum[kv.Key] += kv.Value;
                sb.AppendLine($"ecology {(eco ? "on (living land)" : "off")}: per 10 years, mean of {seeds.Length} seeds");
                float total = 0;
                foreach (var k in StoryKinds) { float per = sum[k] / (float)seeds.Length / 10f; total += per; if (per > 0) sb.AppendLine($"  {k,-14} {per:0.00}"); }
                sb.AppendLine($"  {"all",-14} {total:0.00}");
            }
            TestContext.Out.WriteLine(sb.ToString());
            Console.WriteLine(sb.ToString());
        }
    }
}
