using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AquariumCiv.Audio.Tests
{
    /// <summary>#331: the sand's hiss is occasional gusts, not a constant loop.</summary>
    public class SandGustsTests
    {
        static List<(float at, SandGust g)> Run(int seed, float level, float seconds)
        {
            var s = new SandGusts(seed);
            var res = new List<(float, SandGust)>();
            for (float t = 0f; t < seconds; t += 0.1f)
                if (s.Tick(0.1f, level, out var g)) res.Add((t, g));
            return res;
        }

        [Test]
        public void GapsAreEightToTwentyFiveSecondsAfterTheGustEnds()
        {
            var r = Run(1, 0.5f, 7200f);
            Assert.That(r.Count, Is.GreaterThan(100));
            for (int i = 1; i < r.Count; i++)
            {
                float gap = r[i].at - (r[i - 1].at + r[i - 1].g.duration);
                Assert.That(gap, Is.GreaterThanOrEqualTo(SandGusts.GapMin * SandGusts.BusyScale - 0.2f), "no overlap, never a burst");
                Assert.That(gap, Is.LessThanOrEqualTo(SandGusts.GapMax * SandGusts.SparseScale + 0.2f));
            }
            foreach (var (_, g) in r) Assert.That(g.duration, Is.InRange(SandGusts.DurMin, SandGusts.DurMax));
        }

        [Test]
        public void NoGustBelowTheThreshold()
        {
            Assert.IsEmpty(Run(2, SandGusts.GustsFromLevel - 0.01f, 3600f));
            Assert.IsEmpty(Run(2, 0f, 3600f));
        }

        [Test]
        public void MoreGustsTheBarerTheBox()
        {
            Assert.Greater(Run(3, 0.5f, 7200f).Count, Run(3, 0.08f, 7200f).Count * 1.3);
            Assert.Greater(Run(3, 0.08f, 7200f).Count, 0);
        }

        [Test]
        public void NoTwoGustsAlikeAndNeverTheSameCutTwice()
        {
            var r = Run(4, 0.5f, 7200f);
            for (int i = 1; i < r.Count; i++) Assert.AreNotEqual(r[i - 1].g.clip, r[i].g.clip);
            Assert.AreEqual(r.Count, r.Select(x => (x.g.startFrac, x.g.duration, x.g.pitch, x.g.pan)).Distinct().Count());
            Assert.AreEqual(SandGusts.ClipCount, r.Select(x => x.g.clip).Distinct().Count(), "all the cuts get used");
            foreach (var (_, g) in r)
            {
                Assert.That(g.pitch, Is.InRange(0.9f, 1.1f));
                Assert.That(g.pan, Is.InRange(-1f, 1f));
                Assert.That(g.fadeIn + g.fadeOut, Is.LessThanOrEqualTo(g.duration), "the fades fit inside the gust");
                Assert.That(g.gain, Is.InRange(0.3f, 1f));
            }
        }

        [Test]
        public void SameSeedSameGustsDifferentSeedDifferent()
        {
            var a = Run(5, 0.5f, 600f); var b = Run(5, 0.5f, 600f); var c = Run(6, 0.5f, 600f);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a[i].at, b[i].at); Assert.AreEqual(a[i].g.startFrac, b[i].g.startFrac); }
            Assert.IsFalse(a.Select(x => x.at).SequenceEqual(c.Select(x => x.at)));
        }

        [Test]
        public void NowPlaysAtOnceAndKeepsTheNextAGapAway()
        {
            var s = new SandGusts(7);
            var g = s.Now();
            Assert.That(s.SecondsToNext, Is.GreaterThanOrEqualTo(g.duration + SandGusts.GapMin - 0.01f));
        }

        [Test]
        public void TheHissLevelIsSmallerGoneEarlierAndOnlyFalls()
        {
            AmbienceInput In(float bare) => new AmbienceInput { eco = true, hour = 10f, bare01 = bare };
            Assert.That(Ambience.Targets(In(1f)).sandHiss, Is.InRange(0.4f, 0.6f), "lower than the old constant bed");
            Assert.AreEqual(0f, Ambience.Targets(In(Ambience.SandHissGoneBelow)).sandHiss, 1e-6f);
            Assert.That(Ambience.SandHissGoneBelow, Is.GreaterThan(Ambience.BarrenGoneBelow + 0.15f), "gone well before the wind bed");
            float prev = 2f;
            for (float b = 1f; b >= 0f; b -= 0.05f)
            {
                float v = Ambience.Targets(In(b)).sandHiss;
                Assert.That(v, Is.LessThanOrEqualTo(prev + 1e-5f)); prev = v;
            }
            var half = Ambience.Targets(In(0.5f));
            Assert.That(half.sandHiss, Is.LessThan(half.barren * 0.5f), "the hiss fades faster than the wind");
        }
    }
}
