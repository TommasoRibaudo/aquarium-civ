using System.Collections.Generic;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#359: the B3 games are picked by what a group's spot offers (the water, a grown tree, cover, snow), and neighbours still differ.</summary>
    public class PlayGroup2Tests
    {
        static List<int> Kids(int n) { var l = new List<int>(); for (int i = 0; i < n; i++) l.Add(2000 + i * 13); return l; }

        [Test]
        public void Pick_AtTheWater_TheySplash_TwoToFour()
        {
            for (int seed = 0; seed < 200; seed++)
                for (int size = 2; size <= PlayGroups.MaxGroup; size++)
                    Assert.AreEqual(PlayGame.Splash, PlayGroups.Pick(seed, size, PlaySites.Water | PlaySites.Rabbits, new List<PlayGame> { PlayGame.Splash }), $"seed {seed}, {size} kids");
            Assert.AreNotEqual(PlayGame.Splash, PlayGroups.Pick(3, 1, PlaySites.Water, null), "one kid alone doesn't splash");
        }

        [Test]
        public void Pick_Snow_MakesASnowballFight_UnlessTheNeighbourHasOne()
        {
            Assert.AreEqual(PlayGame.Snowball, PlayGroups.Pick(5, 3, PlaySites.Snow, null));
            Assert.AreNotEqual(PlayGame.Snowball, PlayGroups.Pick(5, 3, PlaySites.Snow, new List<PlayGame> { PlayGame.Snowball }));
            Assert.AreEqual(PlayGame.Ball, PlayGroups.Pick(5, 3, PlaySites.Snow, null, roomy: false), "by a home: the ball, as before");
        }

        [Test]
        public void Pick_TheWoodsAndHideAndSeek_TurnUpWhereTheyCan_AboutHalfTheTime()
        {
            int woods = 0, hide = 0, n = 400;
            for (int seed = 0; seed < n; seed++)
            {
                var g2 = PlayGroups.Pick(seed, 2, PlaySites.Tree | PlaySites.Cover, null);
                var g3 = PlayGroups.Pick(seed, 3, PlaySites.Tree | PlaySites.Cover, null);
                if (g2 == PlayGame.Woods) woods++;
                if (g3 == PlayGame.HideSeek) hide++;
                Assert.AreNotEqual(PlayGame.HideSeek, g2, "hide and seek needs three");
                Assert.AreNotEqual(PlayGame.Woods, g3, "the woods games are for two");
                Assert.AreNotEqual(PlayGame.Woods, PlayGroups.Pick(seed, 2, PlaySites.None, null), "no tree, no woods");
                Assert.AreNotEqual(PlayGame.HideSeek, PlayGroups.Pick(seed, 3, PlaySites.None, null), "no cover, no hiding");
                Assert.AreNotEqual(PlayGame.Woods, PlayGroups.Pick(seed, 2, PlaySites.Tree, new List<PlayGame> { PlayGame.Woods }), "the neighbour's game");
            }
            Assert.That(woods, Is.InRange(n / 3, n * 2 / 3), "woods for a pair by a tree");
            Assert.That(hide, Is.InRange(n / 3, n * 2 / 3), "hide and seek for three with cover");
        }

        [Test]
        public void Pick_WithNothingNew_IsB2sPick()
        {
            for (int seed = 0; seed < 300; seed++)
                for (int size = 1; size <= 4; size++)
                {
                    var taken = new List<PlayGame> { (PlayGame)(1 + seed % 3) };
                    Assert.AreEqual(PlayGroups.Pick(seed, size, true, taken), PlayGroups.Pick(seed, size, PlaySites.Rabbits, taken), $"seed {seed} size {size} rabbits");
                    Assert.AreEqual(PlayGroups.Pick(seed, size, false, taken), PlayGroups.Pick(seed, size, PlaySites.None, taken), $"seed {seed} size {size}");
                }
        }

        [Test]
        public void Split_NeighboursNeverShareAGame_ExceptTheSplashAlongTheBank()
        {
            var plans = new List<PlayGroupPlan>();
            for (long day = 0; day < 60; day++)
                for (int n = 2; n <= 12; n++)
                {
                    PlayGroups.Split(Kids(n), 59, day, 14f, 0f, 0f, (x, z) => PlaySites.Tree | PlaySites.Cover | (day % 3 == 0 ? PlaySites.Rabbits : PlaySites.None), null, plans);
                    var seen = new HashSet<PlayGame>();
                    foreach (var p in plans)
                    {
                        Assert.IsTrue(PlayGroups.Fits(p.game, p.members.Length), $"{p.game} for {p.members.Length}");
                        if (plans.Count <= 2) Assert.IsTrue(seen.Add(p.game), $"day {day}, {n} kids: two groups play {p.game}");
                    }
                    PlayGroups.Split(Kids(n), 59, day, 14f, 0f, 0f, (x, z) => PlaySites.Water, null, plans);
                    foreach (var p in plans) Assert.AreEqual(p.members.Length >= 2 ? PlayGame.Splash : p.game, p.game, "at the water they splash");
                }
        }
    }
}
