using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>P8 (#248, keepers-hands §2.1): the first run on a barren box. Wipe the misted glass, no weed patch, the first
    /// mist instead of the rain tray (done when the ground greens), then the name; no year skip and no time passes.</summary>
    public class EcoFirstRunTests
    {
        [Test]
        public void WipeThenMistThenName_NoYearSkip()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            var run = FirstRun.Begin(sim);
            long month0 = sim.State.month;
            run.Tick(3f, false);
            Assert.AreEqual(FirstRunStep.Wipe, run.Step, "arrived: the misted glass");
            sim.CleanGrime(1f);
            run.Tick(0.1f, true);
            Assert.AreEqual(FirstRunStep.Rain, run.Step, "no patch on a barren box: straight to the first mist");
            Assert.IsNull(run.RainWish, "no rain wish: the land asks");
            run.Tick(2f, false);
            Assert.AreEqual(FirstRun.MistHint.id, run.Hint.id, "the hint says to hold and mist");
            var (x, z) = run.SuggestedRainSpot;
            var st = sim.Settlement;
            Assert.That(System.Math.Sqrt((x - st.Map.greenX) * (x - st.Map.greenX) + (z - st.Map.greenZ) * (z - st.Map.greenZ)), Is.LessThan(1.2), "next to the camp");
            Assert.That(sim.Mist(x, z, 2f).Count, Is.GreaterThan(0));
            run.Tick(0.1f, true);
            Assert.AreEqual(FirstRunStep.Name, run.Step, "the first green: now name it");
            run.KeepName(null);
            run.Tick(0.1f, false);
            Assert.AreEqual(FirstRunStep.Done, run.Step, "no year, no card");
            Assert.IsFalse(run.Active);
            Assert.AreEqual(month0, sim.State.month, "no time skip: not a month passed");
            Assert.That(sim.EcoMilestoneMonth(EcoMilestones.FirstGreen), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void SkippingTheMistStepMistsForThePlayer()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(11, cfg));
            var run = FirstRun.Begin(sim);
            run.DebugSkipTo(FirstRunStep.Rain);
            Assert.AreEqual(FirstRunStep.Rain, run.Step);
            run.SkipStep();
            Assert.AreEqual(FirstRunStep.Name, run.Step);
            Assert.That(sim.EcoBareShare, Is.LessThan(1f), "the skip misted");
        }
    }
}
