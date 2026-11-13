using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #635 (playtest #634): "where do we remember the ones we lose" answered "the tree" hangs charms on the Keeper's tree when it is
    /// grown, else the grown tree nearest the fire, else a pole stands in until a tree grows; one charm per death; the words name who
    /// died. Pure C#, so it also runs in Tools/SimTests.
    /// </summary>
    public class CharmTreeTests
    {
        static SimCore Camp(int seed)
        {
            var sim = NewSim(seed, new SimConfig { dilemmas = true, moments = true, ecology = true });
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Assume.That(sim.EcologyOn, "the ecology is on");
            // no wild trees: each test grows the ones it wants
            for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)sim.State.eco.cover[c] == EcoCover.Tree) sim.State.eco.cover[c] = (byte)EcoCover.Grass;
            return sim;
        }

        static ConsequenceProp AnswerTree(SimCore sim)
        {
            var d = sim.DebugRaiseDilemma("camp_remember");
            Assert.IsNotNull(d, "camp_remember raises: " + sim.DilemmaWhyNot("camp_remember"));
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            var p = sim.ConsequencePropFor(d.id, 1);
            Assert.IsNotNull(p);
            Assert.IsTrue(p.placed, "placed at once: it never waits for a tree");
            Assert.AreEqual(PropAnchor.Tree, p.anchor);
            return p;
        }

        static int WildTree(SimCore sim, int skip = -1)
        {
            for (int c = EcoLand.N / 3; c < EcoLand.N; c++)
            {
                var cv = (EcoCover)sim.State.eco.cover[c];
                if (c == sim.State.eco.treeCell || c == skip || cv == EcoCover.Water || cv == EcoCover.Tree) continue;
                sim.State.eco.cover[c] = (byte)EcoCover.Tree;
                return c;
            }
            Assert.Fail("no ground for a wild tree");
            return -1;
        }

        [Test]
        public void TheCharmsGoOnTheKeepersGrownTree()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var sim = Camp(seed);
                WildTree(sim); // a wild tree too: the Keeper's comes first
                Assert.IsTrue(sim.DebugTreeStage(4));
                var p = AnswerTree(sim);
                Assert.AreEqual(sim.State.eco.treeCell, p.treeCell, $"seed {seed}");
                Assert.AreEqual("the Keeper's tree", sim.CharmTreeWords);
            }
        }

        [Test]
        public void WithNoGrownTreeAPoleStandsInAndMovesToTheKeepersTreeWhenItGrows()
        {
            var sim = Camp(5);
            var p = AnswerTree(sim);
            Assert.AreEqual(-1, p.treeCell, "no tree: a pole");
            Assert.AreEqual("a pole till a tree grows", sim.CharmTreeWords);
            Assert.IsTrue(sim.DebugTreeStage(4));
            sim.Advance(1.0 / 12);
            Assert.AreEqual(sim.State.eco.treeCell, p.treeCell, "the next month the charms hang on the Keeper's tree");
        }

        [Test]
        public void AWildTreeServesUntilTheKeepersTreeIsGrown()
        {
            var sim = Camp(6);
            int wild = WildTree(sim);
            var p = AnswerTree(sim);
            Assert.AreEqual(wild, p.treeCell);
            Assert.AreEqual("the old tree", sim.CharmTreeWords);
            Assert.IsTrue(sim.DebugTreeStage(4));
            sim.Advance(1.0 / 12);
            Assert.AreEqual(sim.State.eco.treeCell, p.treeCell);
        }

        [Test]
        public void WhenItsTreeIsGoneTheCharmsMove()
        {
            var sim = Camp(7);
            int a = WildTree(sim);
            var p = AnswerTree(sim);
            Assert.AreEqual(a, p.treeCell);
            int b = WildTree(sim, a);
            sim.State.eco.cover[a] = (byte)EcoCover.Bare; // felled
            sim.Advance(1.0 / 12);
            Assert.AreNotEqual(a, p.treeCell);
            Assert.AreEqual((EcoCover)sim.State.eco.cover[p.treeCell], EcoCover.Tree, $"on a standing tree (b = {b})");
        }

        [Test]
        public void OneCharmPerDeath()
        {
            var sim = Camp(8);
            Assert.IsTrue(sim.DebugTreeStage(4));
            var p = AnswerTree(sim);
            Assert.AreEqual(1, sim.CharmsOn(p), "the first is for the one the choice was about");
            sim.Advance(1.0 / 12);
            sim.DebugKillOldest();
            Assert.AreEqual(2, sim.CharmsOn(p));
        }

        [Test]
        public void TheWordsNameWhoDied()
        {
            var sim = Camp(9);
            var d = sim.DebugRaiseDilemma("camp_remember");
            Assert.IsNotNull(d);
            var dead = sim.FindNotable(d.deadId);
            Assert.IsNotNull(dead, "the latest death is kept with the choice");
            Assert.IsFalse(dead.alive);
            var view = sim.ViewOfDilemma(d).options;
            StringAssert.Contains(dead.name, view[0].hint, "the stone's hint names them");
            StringAssert.Contains(dead.name, view[1].hint, "the tree's hint names them");
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Assert.IsTrue(sim.State.chronicle.Any(e => e.text.Contains("The first charm went up for " + dead.name)), "the answer's line names them");
            Assert.IsFalse(sim.State.chronicle.Any(e => e.text.Contains("{dead}") || e.text.Contains("{tree}")), "no token left in a line");
        }
    }
}
