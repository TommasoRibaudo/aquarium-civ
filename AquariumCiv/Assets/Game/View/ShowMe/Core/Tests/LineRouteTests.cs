using System;
using NUnit.Framework;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>#327 Option B: every spoken line goes to the one caption, only the first run's wish keeps its bubble; scene lines wait
    /// for the word, ambient talk doesn't; the caption's "who" line. Pure: no Editor.</summary>
    public class LineRouteTests
    {
        [Test]
        public void EverySpokenLineGoesToTheCaption()
        {
            foreach (LineKind k in Enum.GetValues(typeof(LineKind)))
            {
                if (k == LineKind.FirstRunWish) continue;
                Assert.AreEqual(LineChannel.Caption, LineRoute.ChannelFor(k), k.ToString());
            }
        }

        [Test]
        public void OnlyTheFirstRunsWishKeepsTheBubble() => Assert.AreEqual(LineChannel.Bubble, LineRoute.ChannelFor(LineKind.FirstRunWish));

        [Test]
        public void SceneLinesWaitAndAmbientTalkDoesNot()
        {
            foreach (var k in new[] { LineKind.Relic, LineKind.Crumb, LineKind.Wish, LineKind.Echo, LineKind.Death, LineKind.Ask })
                Assert.IsTrue(LineRoute.Waits(k), k.ToString());
            foreach (var k in new[] { LineKind.Ambient, LineKind.Want, LineKind.Tree, LineKind.FirstRunWish })
                Assert.IsFalse(LineRoute.Waits(k), k.ToString());
        }

        [Test]
        public void TheWhoLineIsTheNameOrACrowdLabelFromTheBox()
        {
            Assert.AreEqual("Mira", LineRoute.Who("Mira", false, "camp"));
            Assert.AreEqual("Mira", LineRoute.Who("Mira", true, "camp"), "a name wins");
            Assert.AreEqual("someone in the camp", LineRoute.Who(null, true, "camp"));
            Assert.AreEqual("someone in the village", LineRoute.Who("", true, "village"));
            Assert.IsNull(LineRoute.Who(null, false, "camp"), "nobody to name");
            foreach (var w in new[] { "tank", "glass", "aquarium", "world", "outside" })
                StringAssert.DoesNotContain(w, LineRoute.Who(null, true, "town"), "closed world: only the box's words");
        }

        [Test]
        public void AWaitingLineComesWhenTheWordIsFree()
        {
            var q = new LineQueue<string>();
            q.Add("a", 0);
            q.Add("b", 1);
            Assert.IsFalse(q.TryTake(2, false, out _), "the word is busy");
            Assert.AreEqual(2, q.Count);
            Assert.IsTrue(q.TryTake(3, true, out var first));
            Assert.AreEqual("a", first, "oldest first");
            Assert.IsTrue(q.TryTake(3, true, out var second));
            Assert.AreEqual("b", second);
            Assert.AreEqual(0, q.Dropped);
        }

        [Test]
        public void ALineWaitsOnlySoLong()
        {
            var q = new LineQueue<string>();
            q.Add("a", 0);
            q.Add("b", 5);
            Assert.IsFalse(q.TryTake(LineRoute.WaitSeconds + 1, false, out _));
            Assert.AreEqual(1, q.Count, "the first is let go");
            Assert.AreEqual(1, q.Dropped);
            Assert.IsTrue(q.TryTake(LineRoute.WaitSeconds + 2, true, out var l));
            Assert.AreEqual("b", l, "the second still in time");
        }

        [Test]
        public void TooManyWaitingLetsTheOldestGo()
        {
            var q = new LineQueue<int>();
            for (int i = 0; i < LineRoute.MaxWaiting + 2; i++) q.Add(i, 0);
            Assert.AreEqual(LineRoute.MaxWaiting, q.Count);
            Assert.AreEqual(2, q.Dropped);
            Assert.IsTrue(q.TryTake(0, true, out var l));
            Assert.AreEqual(2, l);
        }
    }
}
