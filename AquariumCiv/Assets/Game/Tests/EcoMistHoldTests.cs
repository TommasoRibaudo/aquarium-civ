using System.Collections.Generic;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// P8 polish #266 (view half): the mist view calls the sim a quarter second at a time while the finger stays down, and tells it
    /// how long the finger has lingered (<c>hold</c>). The reach and the puddles go by the hold, the wetting by the pulse, so a long
    /// hold in pulses is the same mist as one call with the whole time.
    /// </summary>
    public class EcoMistHoldTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static int WetCells(SimCore s) { int n = 0; for (int c = 0; c < EcoLand.N; c++) if (s.EcoWetAt(c) > 0.05f) n++; return n; }

        static (float x, float z) Spot(SimCore sim) => (sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ);

        [Test]
        public void TheHoldGrowsTheReach_NotTheAmount()
        {
            var a = World(); var b = World();
            var (x, z) = Spot(a);
            a.Mist(x, z, 0.25f);                 // a short pulse, no hold given: the small reach
            b.Mist(x, z, 0.25f, 4f);             // the same pulse from a finger that has stayed four seconds: the full reach
            Assert.Greater(WetCells(b), WetCells(a) + 10, "a lingering finger wets a wider area");
            Assert.Greater(b.EcoMistRadius(4f), a.EcoMistRadius(0.25f));
        }

        [Test]
        public void PulsesWithAHold_MakePuddles_OnlyAfterALongEnoughDwell()
        {
            var shortHold = World(); var longHold = World();
            var (x, z) = Spot(shortHold);
            var cells = new List<int>();
            for (int i = 0; i < 6; i++) { shortHold.Mist(x, z, 0.25f, 0.25f * (i + 1)); }          // 1.5 s of dwell
            shortHold.EcoPuddleCells(cells, 0.05f);
            Assert.AreEqual(0, cells.Count, "a short dwell leaves no puddle");
            for (int i = 0; i < 18; i++) { longHold.Mist(x, z, 0.25f, 0.25f * (i + 1)); }          // 4.5 s of dwell
            longHold.EcoPuddleCells(cells, 0.05f);
            Assert.Greater(cells.Count, 0, "a long dwell soaks flat ground to puddles");
        }

        [Test]
        public void NoHoldGiven_BehavesAsBefore()
        {
            var a = World(); var b = World();
            var (x, z) = Spot(a);
            a.Mist(x, z, 1.5f); b.Mist(x, z, 1.5f, 1.5f);
            for (int c = 0; c < EcoLand.N; c++)
            {
                Assert.AreEqual(a.EcoWetAt(c), b.EcoWetAt(c), 1e-6f);
                Assert.AreEqual(a.EcoPuddleAt(c), b.EcoPuddleAt(c), 1e-6f);
            }
        }
    }
}
