using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.View.ShowMe.Tests
{
    /// <summary>P8 #318: the people talk about the Keeper's tree once it is tended, by its stage, in the voice of their trait,
    /// at a pace that doesn't flood, and never in words the stage may not say.</summary>
    public class TreeTalkTests
    {
        static SimCore EcoSim(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        static SimCore Tended(int stage, int mistings = 1)
        {
            var sim = EcoSim();
            Assert.IsTrue(sim.DebugTreeStage(stage));
            sim.State.eco.keeperMistings = mistings;
            return sim;
        }

        /// <summary>One grown adult per trait, so every trait line has a speaker.</summary>
        static void GiveEveryTrait(SimCore sim)
        {
            var adults = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).ToList();
            var traits = new[] { "the worrier", "the gardener" }.Concat(NameData.Traits.Where(t => t != "the worrier" && t != "the gardener")).ToArray();
            for (int i = 0; i < traits.Length && i < adults.Count; i++) adults[i].trait = traits[i];
        }

        [Test]
        public void NothingIsSaidBeforeTheTreeIsTended()
        {
            var sim = EcoSim();
            Assert.AreEqual(0, PeopleVoice.TreeLinesNow(sim).Count(), "no tree");
            sim.DebugTreeStage(2);
            Assert.AreEqual(0, sim.State.eco.keeperMistings);
            Assert.AreEqual(0, PeopleVoice.TreeLinesNow(sim).Count(), "a tree nobody has tended");
            var wb = new WorldBubbles(sim, new BubbleRules { warmUpSeconds = 0f });
            Assert.IsNull(wb.ForceTree(0), "the cheat says nothing either");
            for (double t = 0; t < 3000; t += 25)
            {
                var b = wb.Next(t);
                if (b != null) Assert.AreNotEqual(BubbleSource.Tree, b.source, b.ToString());
            }
        }

        [Test]
        public void ThePeopleTalkOnceTheTreeIsTended()
        {
            var sim = Tended(1);
            GiveEveryTrait(sim);
            Assert.IsTrue(PeopleVoice.TreeLinesNow(sim).Any());
            var wb = new WorldBubbles(sim, new BubbleRules { warmUpSeconds = 0f });
            int tree = 0;
            for (double t = 0; t < 6000; t += 25)
            {
                var b = wb.Next(t);
                if (b != null && b.source == BubbleSource.Tree) tree++;
            }
            Assert.Greater(tree, 0, "tree lines come up at the fire and on walks, not only while tending");
        }

        [Test]
        public void TheTreeIsOneTopicNotTheOnlyOne()
        {
            var sim = Tended(2);
            GiveEveryTrait(sim);
            var wb = new WorldBubbles(sim, new BubbleRules { warmUpSeconds = 0f });
            Assert.IsNotNull(wb.ForceTree(0), "a tree line is said");
            for (int k = 0; k < 2; k++)
            {
                // right after one, anything else there is to say comes before the next (the tree waits two bubbles)
                var first = wb.Candidates().First();
                if (wb.Candidates().Any(c => c.source != BubbleSource.Tree)) Assert.AreNotEqual(BubbleSource.Tree, first.source, "bubble " + k);
                Assert.IsNotNull(wb.Force(k + 1));
            }
            Assert.AreEqual(2, wb.rules.bubblesBetweenTree);
        }

        [Test]
        public void StageLinesRespectTheStage()
        {
            for (int stage = 1; stage <= 4; stage++)
            {
                var sim = Tended(stage, 3);
                GiveEveryTrait(sim);
                var now = PeopleVoice.TreeLinesNow(sim).ToList();
                Assert.IsNotEmpty(now, "stage " + stage);
                foreach (var (l, _, _) in now) Assert.That(stage, Is.InRange(l.minTree, l.maxTree), l.id + " at stage " + stage);
                if (stage == 1) CollectionAssert.DoesNotContain(now.Select(x => x.line.id).ToList(), "any-grown1");
                if (stage == 4) CollectionAssert.DoesNotContain(now.Select(x => x.line.id).ToList(), "any-sprout1");
            }
        }

        [Test]
        public void WateringLinesWaitForThePeopleToLearn()
        {
            var few = Tended(2, 1);
            var many = Tended(2, few.Config.eco.learnWater);
            GiveEveryTrait(few); GiveEveryTrait(many);
            CollectionAssert.DoesNotContain(PeopleVoice.TreeLinesNow(few).Select(x => x.line.id).ToList(), "child1");
            CollectionAssert.Contains(PeopleVoice.TreeLinesNow(many).Select(x => x.line.id).ToList(), "child1");
        }

        [Test]
        public void TraitsColourTheLinesAndSpeakersFitThem()
        {
            var sim = Tended(3);
            GiveEveryTrait(sim);
            var worrier = sim.State.notables.First(n => n.trait == "the worrier");
            var now = PeopleVoice.TreeLinesNow(sim).ToList();
            foreach (var (l, who, _) in now.Where(x => x.line.trait != null && x.line.trait != PeopleVoice.ChildTrait))
                Assert.AreEqual(l.trait, sim.FindNotable(who).trait, l.id);
            Assert.AreEqual(worrier.id, now.First(x => x.line.id == "worrier2").speakerId);
            Assert.IsTrue(now.Where(x => x.line.trait == null).All(x => x.speakerId == -1), "general lines are the crowd's");
            foreach (var n in sim.State.notables) if (n.trait == "the gardener") n.trait = "the lazy";
            Assert.IsFalse(PeopleVoice.TreeLinesNow(sim).Any(x => x.line.trait == "the gardener"), "no gardener, no gardener's line");
        }

        [Test]
        public void EveryLineKeepsTheClosedWorldAndFitsABubble()
        {
            string[] outside = { "tank", "outside", "hill", "far away", "town", "village", "hut", "bread", "market", "glass", "window", "box", "game", "screen" };
            Assert.GreaterOrEqual(PeopleVoice.TreeLines.Count, 25);
            Assert.LessOrEqual(PeopleVoice.TreeLines.Count, 45);
            Assert.AreEqual(PeopleVoice.TreeLines.Count, PeopleVoice.TreeLines.Select(l => l.id).Distinct().Count(), "ids are the said-keys");
            foreach (var l in PeopleVoice.TreeLines)
            {
                string t = l.text.ToLowerInvariant();
                foreach (var w in outside) Assert.IsFalse(t.Contains(w), l.id + ": " + w);
                Assert.LessOrEqual(PeopleVoice.Wrap(l.text.Replace("{keeper}", "the Bright One").Replace("{Keeper}", "The Bright One")).Count, 3, l.id);
                Assert.That(l.minTree, Is.InRange(1, 4));
                Assert.GreaterOrEqual(l.maxTree, l.minTree);
            }
            for (int s = 1; s <= 4; s++) Assert.IsTrue(PeopleVoice.TreeLines.Any(l => l.trait == null && s >= l.minTree && s <= l.maxTree), "stage " + s);
            foreach (var l in PeopleVoice.TreeLines.Where(l => l.trait != null && l.trait != PeopleVoice.ChildTrait))
                CollectionAssert.Contains(NameData.Traits, l.trait, l.id);
        }

        [Test]
        public void TheKeeperIsNamedAsTheStageNamesIt()
        {
            var sim = Tended(1);
            Assert.AreEqual(1, sim.BeliefStage);
            var all = PeopleVoice.TreeLinesNow(sim).Select(x => x.text).ToList();
            Assert.IsTrue(all.Any(t => t.Contains("Bright One")));
            Assert.IsFalse(all.Any(t => t.Contains("Keeper")), "the camp says the Bright One");
        }

        [Test]
        public void TheCheatForcesALineAndNeverRepeatsIt()
        {
            var sim = Tended(1);
            var wb = new WorldBubbles(sim);
            var a = wb.ForceTree(0);
            Assert.IsNotNull(a);
            Assert.AreEqual(BubbleSource.Tree, a.source);
            var b = wb.ForceTree(1);
            Assert.AreNotEqual(a.key, b.key);
        }
    }
}
