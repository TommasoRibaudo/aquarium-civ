using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AquariumCiv.Audio.Tests
{
    /// <summary>#315: the pond's frogs are single, soft, rare croaks: one at a time, long gaps, quieter and duller when far, more at night.</summary>
    public class FrogCroakerTests
    {
        static List<(float at, FrogCall call)> Run(int seed, float level, float zoom, float seconds, Func<float, bool> busy = null)
        {
            var f = new FrogCroaker(seed);
            var res = new List<(float, FrogCall)>();
            for (float t = 0f; t < seconds; t += 0.1f)
                if (f.Tick(0.1f, level, zoom, busy != null && busy(t), out var c)) res.Add((t, c));
            return res;
        }

        [Test]
        public void GapsAreLongAndShorterAtNight()
        {
            var night = Run(1, 1f, 0.5f, 3600f);
            var day = Run(1, 0.25f, 0.5f, 3600f);
            Assert.That(night.Count, Is.InRange(60, 260), "night: a croak every 10 to 25 s");
            Assert.Greater(night.Count, day.Count * 1.5, "more at night than by day");
            for (int i = 1; i < night.Count; i++)
                Assert.GreaterOrEqual(night[i].at - night[i - 1].at, FrogCroaker.GapMin * FrogCroaker.NightScale - 0.2f, "never in a burst");
            for (int i = 1; i < day.Count; i++)
                Assert.GreaterOrEqual(day[i].at - day[i - 1].at, FrogCroaker.GapMin * FrogCroaker.DayScale - 0.2f);
        }

        [Test]
        public void NeverWhileTheLastCroakStillSoundsAndNeverTheSameClipTwice()
        {
            Assert.IsEmpty(Run(2, 1f, 1f, 600f, t => true), "busy all the time: none");
            var calls = Run(3, 1f, 0.5f, 3600f);
            for (int i = 1; i < calls.Count; i++) Assert.AreNotEqual(calls[i - 1].call.clip, calls[i].call.clip);
            Assert.AreEqual(FrogCroaker.ClipCount, calls.Select(c => c.call.clip).Distinct().Count(), "all the takes get used");
            // a croak that is still sounding when the gap ends is only delayed, not stacked
            var late = Run(4, 1f, 0.5f, 600f, t => t % 30f < 25f);
            foreach (var c in late) Assert.GreaterOrEqual(c.at % 30f, 24.9f, "calls start in the quiet part");
        }

        [Test]
        public void SofterAndDullerTheFartherTheCamera()
        {
            Assert.Less(FrogCroaker.VolumeAt(0f), FrogCroaker.VolumeAt(0.5f));
            Assert.Less(FrogCroaker.VolumeAt(0.5f), FrogCroaker.VolumeAt(1f));
            Assert.Less(FrogCroaker.CutoffAt(0f), FrogCroaker.CutoffAt(1f));
            Assert.LessOrEqual(FrogCroaker.CutoffAt(1f), 4500f, "nothing a phone speaker bites on");
            float far = Run(5, 1f, 0f, 1200f).Max(c => c.call.volume), near = Run(5, 1f, 1f, 1200f).Min(c => c.call.volume);
            Assert.Less(far, near, "even the loudest far croak is under the softest near one");
            Assert.LessOrEqual(Run(5, 1f, 1f, 1200f).Max(c => c.call.volume), FrogCroaker.NearVolume + 1e-4f);
        }

        [Test]
        public void NoFrogsWithoutWaterOrInWinter()
        {
            Assert.IsEmpty(Run(6, 0f, 1f, 1200f));
            var f = new FrogCroaker(7);
            Assert.IsTrue(f.Now(0.5f).volume > 0f, "the debug cheat still croaks");
        }

        [Test]
        public void TheCroaksAreSoftShortAndBounded()
        {
            for (int c = 0; c < FrogCroaker.ClipCount; c++)
            {
                var a = FrogCroaker.Generate(c, 315);
                CollectionAssert.AreEqual(a, FrogCroaker.Generate(c, 315), "deterministic");
                Assert.AreEqual(FrogCroaker.Peak, a.Max(Math.Abs), 1e-4);
                Assert.IsTrue(a.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
                float secs = a.Length / (float)FrogCroaker.SampleRate;
                Assert.That(secs, Is.InRange(0.2f, 0.7f), "a short croak");
                Assert.Less(Math.Abs(a[0]), 0.02f);
                Assert.Less(Math.Abs(a[^1]), 0.02f);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => FrogCroaker.Generate(FrogCroaker.ClipCount, 1));
        }
    }
}
