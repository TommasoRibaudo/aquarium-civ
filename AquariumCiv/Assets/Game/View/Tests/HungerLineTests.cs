using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.View.Tests
{
    /// <summary>#782: the "nothing here grows anything to eat" bubble reads the box: it stops once the Keeper's tree has given its first fruit.</summary>
    public class HungerLineTests
    {
        static SimCore HungryBox(int seed)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            sim.DebugEmptyBasket();
            return sim;
        }

        [Test]
        public void BeforeAnyFood_TheLineCanStillBePicked()
        {
            int said = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var sim = HungryBox(seed);
                Assert.IsTrue(sim.Hungry && sim.NewStartOn && !sim.LandGivesFood && !sim.StoryHappened(StoryEvent.FirstFruit), "seed " + seed);
                if (NewStartView.HungerLineFor(sim) != null) said++;
            }
            Assert.AreEqual(60, said, "a hungry bare box says its line");
        }

        [Test]
        public void AfterTheFirstFruit_TheLineIsNeverPicked()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                var sim = HungryBox(seed);
                sim.State.storyFlags.Add("story:" + StoryEvent.FirstFruit);
                Assert.IsTrue(sim.StoryHappened(StoryEvent.FirstFruit));
                Assert.IsNull(NewStartView.HungerLineFor(sim), "seed " + seed);
            }
        }
    
        /// <summary>#776 tester (polish 2): a village with fields said "The ground is bare..." during the statue raising. The bare-land lines are the
        /// first camp's; past the camp none is said, hungry or not.</summary>
        [Test]
        public void PastTheCamp_TheBareLandLineIsNeverPicked()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = HungryBox(seed);
                Assert.IsNotNull(NewStartView.HungerLineFor(sim), "seed " + seed + ": the camp says it");
                sim.State.era = Era.Village;
                Assert.IsNull(NewStartView.HungerLineFor(sim), "seed " + seed + ": a village does not");
            }
        }
}
}
