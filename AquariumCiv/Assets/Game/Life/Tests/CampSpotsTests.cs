using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>fb4 #540: the stew pot, the drying rack, the stash pile, the crumb's stone and the seats never overlap, at any camp size.</summary>
    public class CampSpotsTests
    {
        static readonly (float x, float z)[] Fires = { (0f, 0f), (0.8f, -0.5f), (-1.1f, 0.7f), (0.3f, 1.2f), (-0.4f, -0.9f) };

        [Test]
        public void Rack_StandsClearOfStashStoneAndPot_ForEverySeedAngle()
        {
            var discs = new List<CampSpots.Disc>();
            foreach (var f in Fires)
                for (int i = 0; i < 72; i++)
                {
                    float seed = i / 72f * 6.2831853f;
                    float a = CampSpots.RackAngle(f.x, f.z, seed, LifePlanner.HideFrameDistance);
                    discs.Clear(); CampSpots.FireSide(f.x, f.z, discs);
                    foreach (var d in discs)
                        Assert.GreaterOrEqual(CampSpots.DistanceToRack(f.x, f.z, a, LifePlanner.HideFrameDistance, d.x, d.z), d.r - 1e-3f, $"seed angle {seed} hits a fire-side thing");
                }
        }

        [Test]
        public void RackAngle_KeepsTheSeedsOwn_WhenItIsAlreadyClear()
        {
            // pointing away from the stash (angle 3.55) and the stone (0.28): straight up the z axis from the fire's far side
            float a = CampSpots.RackAngle(0f, 0f, 1.57f, LifePlanner.HideFrameDistance);
            Assert.AreEqual(1.57f, a, 1e-4f);
        }

        [Test]
        public void StewPot_NeverStandsInTheRack_OrOnTheStashOrStone_AtAnyWantedSpot()
        {
            var fixedDiscs = new List<CampSpots.Disc>();
            var tents = new List<CampSpots.Disc>();
            foreach (var f in Fires)
            {
                CampSpots.FireSide(f.x, f.z, fixedDiscs); // (cleared per fire below)
                fixedDiscs.Clear(); CampSpots.FireSide(f.x, f.z, fixedDiscs);
                for (int s = 0; s < 36; s++)
                {
                    float rack = CampSpots.RackAngle(f.x, f.z, s / 36f * 6.2831853f, LifePlanner.HideFrameDistance);
                    for (int w = 0; w < 24; w++)
                    {
                        float wa = w / 24f * 6.2831853f, wr = 0.45f + (w % 5) * 0.1f; // the sim's ring: 0.45 to 0.85
                        float wx = f.x + (float)Math.Cos(wa) * wr, wz = f.z + (float)Math.Sin(wa) * wr;
                        tents.Clear();
                        tents.Add(new CampSpots.Disc(f.x + 0.9f, f.z + 0.5f, 0.22f)); // a tent
                        CampSpots.Spot(f.x, f.z, rack, LifePlanner.HideFrameDistance, wx, wz, tents, out float x, out float z);
                        Assert.IsTrue(CampSpots.Clear(f.x, f.z, rack, LifePlanner.HideFrameDistance, x, z, fixedDiscs, tents),
                            $"fire {f} rack {rack} wanted {wx},{wz} got {x},{z}");
                    }
                }
            }
        }

        [Test]
        public void StewPot_StaysWhereTheSimPutIt_WhenThatSpotIsFree()
        {
            var fixedDiscs = new List<CampSpots.Disc>(); CampSpots.FireSide(0f, 0f, fixedDiscs);
            // rack on the +z side, the pot wanted on -z far side of the ring, clear of the stash (203 deg) and the stone
            Spot(0f, 0f, 1.57f, 0.1f, -0.7f, out float x, out float z);
            Assert.AreEqual(0.1f, x, 1e-4f); Assert.AreEqual(-0.7f, z, 1e-4f);
        }

        static void Spot(float fx, float fz, float rack, float wx, float wz, out float x, out float z) =>
            CampSpots.Spot(fx, fz, rack, LifePlanner.HideFrameDistance, fx + wx, fz + wz, null, out x, out z);
    }
}
