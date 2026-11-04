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
        public void TheNameHintShowsAfterTheMistingFingerLifts()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            var run = FirstRun.Begin(sim);
            run.DebugSkipTo(FirstRunStep.Rain);
            var (x, z) = run.SuggestedRainSpot;
            sim.Mist(x, z, 2f);
            run.Tick(0.1f, true);                       // the finger that misted is still down
            Assert.AreEqual(FirstRunStep.Name, run.Step);
            for (int i = 0; i < 20; i++) run.Tick(0.1f, true);
            for (int i = 0; i < 20; i++) run.Tick(0.1f, false); // lifted
            Assert.AreEqual("name", run.Hint.id, "Name it shows (tester P8: the held finger used it up)");
            Assert.AreEqual(HintGesture.Hold, FirstRun.MistHint.gesture, "the mist hint shows a hold, not a tap");
        }

        [Test]
        public void TheCampRaisesNoFieldsOrMillOnABareLand()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            sim.DebugAddHides(SimCore.HidesPerTent); // #284: a tent is made of hides
            sim.DebugOpenCampGate();
            for (int m = 0; m < 12 * 12; m++) { sim.Step(); if (sim.State.eraReady) break; }
            int farms = 0, mills = 0;
            foreach (var b in sim.State.buildings) { if (b.type == BuildingType.Farm) farms++; if (b.type == BuildingType.Lumbermill) mills++; }
            Assert.That(sim.State.buildings.Count, Is.GreaterThan(2), "tents went up after the gate");
            Assert.AreEqual(0, farms, "no fields before the village gate (tester P8)");
            if (sim.State.eco.cTrees < cfg.eco.villageTrees / 2) Assert.AreEqual(0, mills, "no lumber mill without a wood to cut");
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
