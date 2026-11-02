using System;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>The slot layout at a place (tester, 2026-10-30: 9 dolls within 0.12 u at the tavern, 3-4 on one spot at homes):
    /// a max-crowding metric, the least distance between any two people there.</summary>
    public class SlotSpotsTests
    {
        static float MinGap((float dx, float dz)[] pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
                for (int j = i + 1; j < pts.Length; j++)
                {
                    float dx = pts[i].dx - pts[j].dx, dz = pts[i].dz - pts[j].dz;
                    best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
                }
            return best;
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(9)]
        [TestCase(20)]
        [TestCase(40)]
        public void NoTwoPeopleAtADoorStandCloserThanTheGap(int people)
        {
            foreach (float outAngle in new[] { 0f, 1.1f, -2.4f, 3.1f })
            {
                var pts = new (float, float)[people];
                for (int s = 0; s < people; s++) pts[s] = SlotSpots.Door(s, outAngle);
                Assert.GreaterOrEqual(MinGap(pts), SlotSpots.Gap - 1e-3f, $"{people} at a door facing {outAngle}");
            }
        }

        [Test]
        public void AHomeHoldsTwoAtTheDoorAndTheAxeAwayFromIt()
        {
            foreach (float outAngle in new[] { 0f, 1.1f, -2.4f, 3.1f })
            {
                for (int rank = 0; rank < 8; rank++)
                {
                    var (dx, dz) = SlotSpots.Home(SlotSpots.HomeSpot.Door, rank, outAngle);
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (rank < SlotSpots.AtDoor) Assert.Less(d, 0.2f, "at the door");
                    else Assert.Greater(d, 0.2f, "past two, out in front");
                    (dx, dz) = SlotSpots.Home(SlotSpots.HomeSpot.Block, rank, outAngle);
                    Assert.Greater((float)Math.Sqrt(dx * dx + dz * dz), 0.25f, "the block isn't at the door");
                    Assert.Greater(dx * Math.Cos(outAngle) + dz * Math.Sin(outAngle), 0.05f, "and it's off the wall");
                }
                var (ax, az) = SlotSpots.Home(SlotSpots.HomeSpot.Block, 0, outAngle);
                var (bx, bz) = SlotSpots.Home(SlotSpots.HomeSpot.Line, 0, outAngle);
                Assert.Greater(Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz)), 0.4f, "the block and the line are on either side");
            }
            Assert.AreEqual(SlotSpots.HomeSpot.Block, SlotSpots.SpotFor(ActivityKind.ChopKindling));
            Assert.AreEqual(SlotSpots.HomeSpot.Block, SlotSpots.SpotFor(ActivityKind.StackWood));
            Assert.AreEqual(SlotSpots.HomeSpot.Line, SlotSpots.SpotFor(ActivityKind.HangWashing));
            Assert.AreEqual(SlotSpots.HomeSpot.Front, SlotSpots.SpotFor(ActivityKind.Play));
            Assert.AreEqual(SlotSpots.HomeSpot.Door, SlotSpots.SpotFor(ActivityKind.SweepStep));
        }

        [TestCase(3)]
        [TestCase(9)]
        [TestCase(30)]
        public void NoTwoPeopleOnOpenGroundStandCloserThanTheGap(int people)
        {
            var pts = new (float, float)[people];
            for (int s = 0; s < people; s++) pts[s] = SlotSpots.Open(s, 0.7f);
            Assert.GreaterOrEqual(MinGap(pts), SlotSpots.Gap - 1e-3f);
        }

        [Test]
        public void TheFirstStandsOnTheSpotAndTheFanOpensAwayFromTheBuilding()
        {
            Assert.AreEqual((0f, 0f), SlotSpots.Door(0, 1f));
            for (int s = 1; s < 12; s++)
            {
                var (dx, dz) = SlotSpots.Door(s, 0f); // out = +x
                Assert.Greater(dx, 0f, $"slot {s} is on the outside of the door");
            }
        }

        [Test]
        public void ANinthPersonIsNoWhereNearTheFirstEight()
        {
            // The tavern at midday: the tester saw nine within 0.12 u of each other. Now the nearest pair is a doll apart,
            // and the whole crowd stays within a short step of the door.
            var pts = new (float dx, float dz)[9];
            for (int s = 0; s < 9; s++) pts[s] = SlotSpots.Door(s, 0f);
            Assert.GreaterOrEqual(MinGap(pts), SlotSpots.Gap - 1e-3f);
            foreach (var p in pts) Assert.LessOrEqual(Math.Sqrt(p.dx * p.dx + p.dz * p.dz), 0.5f);
        }
    }
}
