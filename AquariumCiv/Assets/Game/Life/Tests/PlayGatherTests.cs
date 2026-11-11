using System.Collections.Generic;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#541: two free kids never play two solo copies of one game: they join one group and play together.</summary>
    public class PlayGatherTests
    {
        static PlayGroups.KidSpot S(int id, float x, float z, int n = 1, int cls = 0) => new PlayGroups.KidSpot(id, x, z, n, cls);

        [Test]
        public void TwoLoneKidsNearEachOther_JoinOneGroup_AndPlayATogetherGame()
        {
            var host = PlayGroups.Gather(new[] { S(10, 0f, 0f), S(20, 1f, 0.5f) });
            Assert.AreEqual(1, new HashSet<int>(host).Count, "one host for both");
            // The merged group is two kids: a game for two, never a solo ball copy.
            var plans = new List<PlayGroupPlan>();
            PlayGroups.Split(new[] { 5, 6 }, 99, 3, 14f, 0f, 0f, (x, z) => PlaySites.None, null, plans);
            Assert.AreEqual(1, plans.Count);
            Assert.AreEqual(2, plans[0].members.Length);
            Assert.IsTrue(PlayGroups.Fits(plans[0].game, 2), "a game that fits a pair: " + plans[0].game);
            Assert.IsTrue(PlayGroups.Fits(PlayGame.Tag, 2), "tag is a together game for two");
        }

        [Test]
        public void OneKid_StaysSolo()
        {
            var host = PlayGroups.Gather(new[] { S(10, 0f, 0f) });
            Assert.AreEqual(0, host[0]);
        }

        [Test]
        public void LoneKidsFarApart_OrOfDifferentClass_DoNotGather()
        {
            Assert.AreEqual(new[] { 0, 1 }, PlayGroups.Gather(new[] { S(1, 0f, 0f), S(2, 20f, 0f) }));
            Assert.AreEqual(new[] { 0, 1 }, PlayGroups.Gather(new[] { S(1, 0f, 0f, 1, 0), S(2, 0.5f, 0f, 1, 1) }));
        }

        [Test]
        public void ALoneKidJoinsTheNearestGroupWithRoom_NeverAFullOne()
        {
            var host = PlayGroups.Gather(new[] { S(1, 0f, 0f, 3), S(2, 0.5f, 0f, 1), S(3, 1f, 0f, 1) });
            Assert.AreEqual(0, host[1]); // fills the group of three to four
            Assert.AreEqual(2, host[2], "the next one has no room there: stays (and the group of four is not exceeded)");
            var full = PlayGroups.Gather(new[] { S(1, 0f, 0f, 4), S(2, 0.5f, 0f, 1) });
            Assert.AreEqual(1, full[1]);
        }

        [Test]
        public void ThreeLoneKids_NeverEndUpAsTwoSoloCopies_OneGroupOfThree()
        {
            var host = PlayGroups.Gather(new[] { S(1, 0f, 0f), S(2, 1f, 0f), S(3, 2f, 0f) });
            Assert.AreEqual(1, new HashSet<int>(host).Count, "all three in one group: " + string.Join(",", host));
        }

        [Test]
        public void TwoGroupsInView_NeverPlayTheSameGame()
        {
            var inView = new List<PlayGame> { PlayGame.Ball };
            // For a pair, tag replaces a repeated ball.
            Assert.AreEqual(PlayGame.Tag, PlayGroups.Differ(PlayGame.Ball, 2, inView));
            Assert.AreEqual(PlayGame.Ball, PlayGroups.Differ(PlayGame.Ball, 2, new List<PlayGame>()));
            Assert.AreEqual(PlayGame.Ball, PlayGroups.Differ(PlayGame.Ball, 2, inView, roomy: false), "a yard has the one game");
        }
    }
}
