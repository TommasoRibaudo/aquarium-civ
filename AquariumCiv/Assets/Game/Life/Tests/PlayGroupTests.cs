using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#358: the kids at one place split into play groups, each a game its neighbour isn't playing, far enough apart (research 17 §4.6).</summary>
    public class PlayGroupTests
    {
        static List<int> Kids(int n) { var l = new List<int>(); for (int i = 0; i < n; i++) l.Add(1000 + i * 7); return l; }

        [Test]
        public void Split_GroupsOfTwoToFour_EveryKidOnce_GamesThatFit()
        {
            var plans = new List<PlayGroupPlan>();
            for (int n = 1; n <= 13; n++)
            {
                PlayGroups.Split(Kids(n), 77, 5, 14f, 0f, 0f, (x, z) => true, null, plans);
                Assert.AreEqual(PlayGroups.GroupCount(n), plans.Count, $"{n} kids");
                var seen = new HashSet<int>();
                foreach (var p in plans)
                {
                    Assert.LessOrEqual(p.members.Length, PlayGroups.MaxGroup, $"{n} kids: a group of {p.members.Length}");
                    if (n > 1) Assert.GreaterOrEqual(p.members.Length, 2, $"{n} kids: nobody plays alone when there are others");
                    Assert.IsTrue(PlayGroups.Fits(p.game, p.members.Length), $"{p.game} for {p.members.Length}");
                    foreach (int k in p.members) Assert.IsTrue(seen.Add(k), $"kid {k} in two groups");
                }
                Assert.AreEqual(n, seen.Count);
            }
        }

        [Test]
        public void Split_NeighbourGroupsPlayDifferentGames_AndStandAtLeastApart()
        {
            var plans = new List<PlayGroupPlan>();
            for (int n = 5; n <= 12; n++)
            for (long day = 0; day < 20; day++)
            {
                PlayGroups.Split(Kids(n), 31, day, 15f, 0.4f, -0.2f, (x, z) => day % 2 == 0, null, plans);
                for (int a = 0; a < plans.Count; a++)
                for (int b = a + 1; b < plans.Count; b++)
                {
                    // Two groups of three or fewer always have another game; bigger ones may share tag, 1 u apart (§0.8).
                    if (n <= 6) Assert.AreNotEqual(plans[a].game, plans[b].game, $"{n} kids, day {day}: two groups at one place play {plans[a].game}");
                    float d = (float)Math.Sqrt((plans[a].x - plans[b].x) * (plans[a].x - plans[b].x) + (plans[a].z - plans[b].z) * (plans[a].z - plans[b].z));
                    Assert.GreaterOrEqual(d, PlayGroups.Apart - 1e-3f, $"{n} kids, day {day}: groups {d:0.00} u apart");
                }
            }
        }

        [Test]
        public void Pick_ChaseOnlyWithRabbits_NeverATakenGame_AndVariesOverTheDay()
        {
            var games = new HashSet<PlayGame>();
            for (int seed = 0; seed < 200; seed++)
            {
                var g = PlayGroups.Pick(seed, 3, false, null);
                Assert.AreNotEqual(PlayGame.ChaseRabbits, g, "no rabbits, no chase");
                games.Add(g);
                var taken = new List<PlayGame> { PlayGame.Tag };
                Assert.AreNotEqual(PlayGame.Tag, PlayGroups.Pick(seed, 3, true, taken));
                Assert.AreEqual(PlayGroups.Pick(seed, 3, true, null), PlayGroups.Pick(seed, 3, true, null), "a hash: the same every time");
            }
            CollectionAssert.Contains(games, PlayGame.Tag);
            CollectionAssert.Contains(games, PlayGame.Ball);
            Assert.AreEqual(PlayGame.Ball, PlayGroups.Pick(5, 1, false, null), "alone: the ball, tossed ahead and fetched");
            Assert.AreNotEqual(PlayGroups.Seed(1, 3, 13f, 0), PlayGroups.Seed(1, 3, 15f, 0), "a new block, a new pick");
        }
    }
}
