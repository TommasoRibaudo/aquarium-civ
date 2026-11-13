// #618: the kids' ice slide, pure (UnityEngine maths only), as the other KidPlay tests.
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class IceSlideTests
    {
        static readonly int[] Two = { 11, 22 }, Three = { 11, 22, 33 }, Four = { 11, 22, 33, 44 };
        static readonly Vector2 A = new(0f, 0f), B = new(0.5f, 0f);

        [Test]
        public void ThePickPutsKidsOnTheIce_WhenTheIceIsThere()
        {
            Assert.AreEqual(PlayGame.IceSlide, PlayGroups.Pick(5, 3, PlaySites.Ice | PlaySites.Snow, null));
            Assert.AreEqual(PlayGame.IceSlide, PlayGroups.Pick(5, 2, PlaySites.Ice, null));
            Assert.IsFalse(PlayGroups.Fits(PlayGame.IceSlide, 1), "a slide needs someone to watch");
            Assert.IsNull(KidGame.Create(PlayGame.IceSlide, 1, 0f, Three, A, null), "no ice site: no slide");
            Assert.IsNotNull(KidGame.Create(PlayGame.IceSlide, 1, 0f, Three, A, new PlaySite { ice = true, slideA = A, slideB = B }));
        }

        [Test]
        public void EveryoneSlidesInTurn_FromAToB_AndIsBackBeforeTheirNextTurn()
        {
            foreach (var members in new[] { Two, Three, Four })
            {
                var g = new IceSlideGame(618, 0f, members, A, B);
                Assert.GreaterOrEqual(g.Turn * g.Count, IceSlideGame.AwaySeconds, "back in the queue before the next turn");
                for (int k = 0; k < 2 * g.Count; k++)
                {
                    int i = k % g.Count;
                    float start = k * g.Turn;
                    var run = g.Cue(i, start + 0.1f);
                    Assert.AreEqual(GroupRole.Runner, run.role, $"turn {k}: member {i} runs up");
                    Assert.AreEqual(DollStyle.Run, run.style);
                    var glide = g.Cue(i, start + IceSlideGame.RunUp + 0.05f);
                    Assert.Less(Vector2.Distance(glide.pos, A), 0.06f, "the glide starts at A");
                    var end = g.Cue(i, start + IceSlideGame.RunUp + IceSlideGame.Glide + 0.05f);
                    Assert.Less(Vector2.Distance(end.pos, B), 1e-3f, "and stops at B");
                    Assert.IsTrue(end.style == DollStyle.Sit || end.style == DollStyle.Cheer, "a fall or a whoop");
                    // nobody else is sliding at the same moment
                    for (int j = 0; j < g.Count; j++) if (j != i) Assert.AreNotEqual(DollStyle.Run, g.Cue(j, start + 0.1f).style);
                }
            }
        }

        [Test]
        public void TheGlideSlowsDown_AndItsArmsGoOutWide()
        {
            Assert.AreEqual(0f, IceSlideGame.GlideAt(0f), 1e-5f);
            Assert.AreEqual(1f, IceSlideGame.GlideAt(1f), 1e-5f);
            Assert.Greater(IceSlideGame.GlideAt(0.25f) - IceSlideGame.GlideAt(0f), IceSlideGame.GlideAt(1f) - IceSlideGame.GlideAt(0.75f), "fast off the run-up, slow at the end");
            var g = new IceSlideGame(618, 0f, Three, A, B);
            var p = g.PoseOf(0, IceSlideGame.RunUp + 0.3f, 0f, 1f, 0f, out var c);
            Assert.AreEqual(GroupRole.Runner, c.role);
            Assert.Less(p.armL.z, -60f); Assert.Greater(p.armR.z, 60f);
        }

        [Test]
        public void SomeSlidesEndInAFall_NotAll()
        {
            var g = new IceSlideGame(7, 0f, Three, A, B);
            int falls = 0;
            for (int k = 0; k < 30; k++) if (g.Falls(k)) falls++;
            Assert.AreEqual(10, falls, "one in three");
        }
    }
}
