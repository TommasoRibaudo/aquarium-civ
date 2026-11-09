using NUnit.Framework;

namespace AquariumCiv.Audio.Tests
{
    /// <summary>#520: the barren wind's two voices never settle into a pattern.</summary>
    public class BarrenWindTests
    {
        [Test]
        public void GainsAndPitchesStayInRange()
        {
            var w = new BarrenWind(1);
            for (int i = 0; i < 100000; i++)
            {
                w.Tick(0.1f);
                Assert.That(w.GainA, Is.InRange(BarrenWind.GainMin - 1e-4f, BarrenWind.GainMax + 1e-4f));
                Assert.That(w.GainB, Is.InRange(BarrenWind.GainMin - 1e-4f, BarrenWind.GainMax + 1e-4f));
                Assert.That(w.PitchOfA, Is.InRange(0.96f, 1.04f));
                Assert.That(w.PitchOfB, Is.InRange(0.83f, 0.91f));
            }
        }

        [Test]
        public void VoicesHaveDifferentLoopLengths()
        {
            var w = new BarrenWind(2);
            Assert.That(40f / w.PitchOfB - 40f / w.PitchOfA, Is.GreaterThan(3f), "seams drift apart by seconds each pass");
        }

        [Test]
        public void GainsWanderAndDoNotRepeatOnTheLoopLength()
        {
            var w = new BarrenWind(3);
            float min = 9f, max = -9f; var at0 = new System.Collections.Generic.List<float>();
            for (float t = 0f; t < 1200f; t += 0.5f)
            {
                w.Tick(0.5f);
                min = System.Math.Min(min, w.GainA); max = System.Math.Max(max, w.GainA);
                if (System.Math.Abs(t % 40f) < 0.25f) at0.Add(w.GainA);
            }
            Assert.That(max - min, Is.GreaterThan(0.3f), "the swells really move");
            Assert.That(at0.Count, Is.GreaterThan(10));
            float spread = System.Linq.Enumerable.Max(at0) - System.Linq.Enumerable.Min(at0);
            Assert.That(spread, Is.GreaterThan(0.1f), "the same moment of the loop gets different levels each pass");
        }

        [Test]
        public void SameSeedSameWindDifferentSeedNot()
        {
            var a = new BarrenWind(5); var b = new BarrenWind(5); var c = new BarrenWind(6);
            for (int i = 0; i < 500; i++) { a.Tick(0.3f); b.Tick(0.3f); c.Tick(0.3f); }
            Assert.AreEqual(a.GainA, b.GainA);
            Assert.AreNotEqual(a.GainA, c.GainA);
        }

        [Test]
        public void WindFadesFasterThanBeforeAsTheLandGreens()
        {
            var a = new AmbienceInput { eco = true, bare01 = 0.5f, hour = 12f };
            float u = (0.5f - Ambience.BarrenGoneBelow) / (1f - Ambience.BarrenGoneBelow);
            float old = u * u * (3f - 2f * u) * (0.75f + 0.25f * 0.5f);
            Assert.That(Ambience.Targets(a).barren, Is.LessThan(old * 0.8f));
        }
    }
}
