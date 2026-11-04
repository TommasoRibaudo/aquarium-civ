using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Tests
{
    /// <summary>P8 polish #281 phase 1 (sim): new grass since the last look is "fresh", fading over the session.</summary>
    public class EcoSeenTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        // A land with a little green, then the player looks.
        static SimCore Looked(int seed = 7)
        {
            var sim = World(seed);
            sim.Mist(sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ, 3f);
            sim.Advance(0.5);
            sim.MarkLandSeen();
            return sim;
        }

        static void Months(SimCore s, int n) { for (int i = 0; i < n; i++) s.Advance(1.0 / 12); }

        static int Greens(SimCore sim) { int n = 0; for (int c = 0; c < EcoLand.N; c++) if (EcoSeen.IsGreen(sim.State.eco, c)) n++; return n; }

        [Test]
        public void SeenAtLeave_GrassGrownAfterCatchUpIsFresh_AlreadySeenIsNot()
        {
            var sim = Looked();
            int before = Greens(sim);
            Assert.Greater(before, 0);
            Assert.AreEqual(0, sim.LandNewCellCount, "just looked: nothing new");
            var seenCells = new System.Collections.Generic.List<int>();
            for (int c = 0; c < EcoLand.N; c++) if (EcoSeen.IsGreen(sim.State.eco, c)) seenCells.Add(c);
            sim.Mist(sim.EcoGround.campX - 1.2f, sim.EcoGround.campZ + 0.8f, 4f);
            sim.CatchUp(6);
            Assert.Greater(Greens(sim), before, "the land grew while away");
            var fresh = new float[EcoLand.N];
            int n = sim.FillLandFreshness(0f, fresh);
            Assert.Greater(n, 0);
            Assert.AreEqual(Greens(sim) - before, n, "exactly the new green is fresh");
            foreach (int c in seenCells) Assert.AreEqual(0f, fresh[c], $"cell {c} was already seen");
            for (int c = 0; c < EcoLand.N; c++)
            {
                Assert.AreEqual(fresh[c], sim.LandFreshness(c, 0f));
                if (fresh[c] > 0f) { Assert.AreEqual(1f, fresh[c], 1e-5f); Assert.IsTrue(EcoSeen.IsGreen(sim.State.eco, c)); }
            }
        }

        [Test]
        public void Freshness_FadesToZeroOverTheSession_ThenCommits()
        {
            var sim = Looked();
            sim.DebugPretendAwayWithNewGrass(5);
            int cell = -1; var f0 = new float[EcoLand.N];
            sim.FillLandFreshness(0f, f0);
            for (int c = 0; c < EcoLand.N; c++) if (f0[c] > 0f) { cell = c; break; }
            Assert.GreaterOrEqual(cell, 0);
            float prev = 2f;
            for (float t = 0f; t <= EcoSeen.FadeSeconds; t += EcoSeen.FadeSeconds / 9f)
            {
                float v = sim.LandFreshness(cell, t);
                Assert.LessOrEqual(v, prev + 1e-6f, "never rises");
                prev = v;
            }
            Assert.AreEqual(0f, sim.LandFreshness(cell, EcoSeen.FadeSeconds), 1e-6f);
            Assert.AreEqual(0f, sim.LandFreshness(cell, EcoSeen.FadeSeconds * 3f));
            Assert.Greater(sim.LandFreshness(cell, 1f), 0.9f);

            Assert.IsFalse(sim.CommitLandSeenIfFaded(EcoSeen.FadeSeconds * 0.5f), "not faded yet");
            Assert.Greater(sim.LandNewCellCount, 0);
            Assert.IsTrue(sim.CommitLandSeenIfFaded(EcoSeen.FadeSeconds));
            Assert.AreEqual(0, sim.LandNewCellCount, "committed: seen now");
            Assert.AreEqual(0, sim.FillLandFreshness(0f, f0));
        }

        [Test]
        public void OldSaveWithoutMask_ShowsNothingFresh_NoFlash()
        {
            var sim = World();
            sim.Mist(sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ, 3f);
            sim.CatchUp(3);
            Assert.IsNull(sim.State.eco.seen, "never marked");
            Assert.IsFalse(sim.HasLandSeenMask);
            Assert.AreEqual(0, sim.FillLandFreshness(0f, new float[EcoLand.N]));
            Assert.AreEqual(0, sim.LandNewCellCount);
            // the same after a JSON round trip with the field cut out of an older save
            var json = JsonUtility.ToJson(sim.State);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            Assert.IsFalse(loaded.HasLandSeenMask);
            Assert.AreEqual(0, loaded.FillLandFreshness(0f, new float[EcoLand.N]));
            // once the view marks it, later growth is fresh
            loaded.MarkLandSeen();
            Assert.IsTrue(loaded.HasLandSeenMask);
            Assert.AreEqual(0, loaded.LandNewCellCount);
        }

        [Test]
        public void MaskRoundTripsThroughTheSave()
        {
            var sim = Looked();
            sim.DebugPretendAwayWithNewGrass(4);
            int n = sim.LandNewCellCount;
            Assert.Greater(n, 0);
            var json = JsonUtility.ToJson(sim.State);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            Assert.AreEqual(n, loaded.LandNewCellCount);
            CollectionAssert.AreEqual(sim.State.eco.seen, loaded.State.eco.seen);
            Assert.AreEqual(json, JsonUtility.ToJson(loaded.State));
        }

        [Test]
        public void PretendAway_IsDeterministicAndCapped()
        {
            var a = Looked(); var b = Looked();
            int green = Greens(a);
            Assert.AreEqual(a.DebugPretendAwayWithNewGrass(3), b.DebugPretendAwayWithNewGrass(3));
            CollectionAssert.AreEqual(a.State.eco.seen, b.State.eco.seen);
            Assert.AreEqual(green, a.DebugPretendAwayWithNewGrass(10000), "never more than the green there is");
        }

        [Test]
        public void EcologyOff_NothingFresh_AndNothingSaved()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            sim.MarkLandSeen();
            Assert.IsNull(sim.State.eco.seen);
            Assert.IsFalse(sim.HasLandSeenMask);
            Assert.AreEqual(0, sim.DebugPretendAwayWithNewGrass(5));
            Assert.AreEqual(0, sim.LandNewCellCount);
            Assert.AreEqual(0f, sim.LandFreshness(10, 0f));
            Assert.AreEqual(0, sim.FillLandFreshness(0f, new float[EcoLand.N]));
            Assert.IsFalse(sim.CommitLandSeenIfFaded(1000f));
        }

        [Test]
        public void Marking_DoesNotChangeTheEcology()
        {
            var a = World(5); var b = World(5);
            a.Mist(a.EcoGround.campX + 0.9f, a.EcoGround.campZ, 3f); b.Mist(b.EcoGround.campX + 0.9f, b.EcoGround.campZ, 3f);
            a.MarkLandSeen(); a.DebugPretendAwayWithNewGrass(3);
            Months(a, 24); Months(b, 24);
            Assert.AreEqual(a.EcoFingerprint, b.EcoFingerprint, "the mask never feeds back into the model");
        }
    }
}
