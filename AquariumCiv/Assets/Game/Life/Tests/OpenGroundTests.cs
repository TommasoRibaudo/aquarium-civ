using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>Play-lots fix: targets inside a lot are moved to open ground next to it.</summary>
    public class OpenGroundTests
    {
        // Two lots: a square at the origin (half 0.5) and one at (2,0) (half 0.4).
        static bool InLot(float x, float z) => (System.Math.Abs(x) < 0.5f && System.Math.Abs(z) < 0.5f) || (System.Math.Abs(x - 2f) < 0.4f && System.Math.Abs(z) < 0.4f);

        [Test]
        public void OpenPoint_StaysWhereItIs()
        {
            var (x, z) = OpenGround.Nudge(1f, 1f, InLot);
            Assert.AreEqual(1f, x); Assert.AreEqual(1f, z);
        }

        [Test]
        public void PointInALot_MovesJustOutside_AndEverySampledGameTargetEndsOpen()
        {
            for (float gx = -0.6f; gx <= 2.6f; gx += 0.05f)
                for (float gz = -0.6f; gz <= 0.6f; gz += 0.05f)
                {
                    var (x, z) = OpenGround.Nudge(gx, gz, InLot, 1f, 0f);
                    Assert.IsFalse(InLot(x, z), $"({gx:0.00},{gz:0.00}) -> ({x:0.00},{z:0.00})");
                    float d = (float)System.Math.Sqrt((x - gx) * (x - gx) + (z - gz) * (z - gz));
                    Assert.LessOrEqual(d, 0.62f, "it moves to the nearest edge, not across the map");
                }
        }

        [Test]
        public void SlidingAlongALot_IsSteady_NoJumpBetweenNeighbouringPoints()
        {
            float lx = 0, lz = 0; bool first = true;
            for (float gx = 0.3f; gx >= -0.3f; gx -= 0.01f)
            {
                var (x, z) = OpenGround.Nudge(gx, 0.45f, InLot, 0f, 1f);
                if (!first) Assert.Less(System.Math.Abs(x - lx) + System.Math.Abs(z - lz), 0.12f);
                lx = x; lz = z; first = false;
            }
        }
    }
}
