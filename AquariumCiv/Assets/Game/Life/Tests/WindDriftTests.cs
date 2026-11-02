using System;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#202: the wind holds spells of still, gentle and brisk, eases between them, and is the same for the same box.</summary>
    public class WindDriftTests
    {
        [TestCase(7), TestCase(1234), TestCase(-99)]
        public void ItBlowsStillGentleAndBrisk(int seed)
        {
            int still = 0, gentle = 0, brisk = 0, n = 0;
            for (double t = 0; t < 3600; t += 0.5, n++)
            {
                float w = WindDrift.At(seed, t);
                Assert.That(w, Is.InRange(0f, 1f));
                if (w <= 0.001f) still++;
                else if (w < 0.5f) gentle++;
                else brisk++;
            }
            Assert.Greater(still / (float)n, 0.1f, "some spells the sails stand");
            Assert.Greater(gentle / (float)n, 0.2f, "some they drift");
            Assert.Greater(brisk / (float)n, 0.1f, "some they go");
        }

        [Test]
        public void ItNeverChangesInAStep()
        {
            float last = WindDrift.At(5, 0);
            for (double t = 1.0 / 30.0; t < 900; t += 1.0 / 30.0)
            {
                float w = WindDrift.At(5, t);
                Assert.Less(Math.Abs(w - last), 0.02f, $"at {t}");
                last = w;
            }
        }

        [Test]
        public void TheSameBoxBlowsTheSameAndAnotherDoesNot()
        {
            int differ = 0;
            for (double t = 0; t < 600; t += 3)
            {
                Assert.AreEqual(WindDrift.At(11, t), WindDrift.At(11, t));
                if (Math.Abs(WindDrift.At(11, t) - WindDrift.At(12, t)) > 0.05f) differ++;
            }
            Assert.Greater(differ, 40);
        }
    }
}
