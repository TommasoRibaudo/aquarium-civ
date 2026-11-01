using System.Collections.Generic;
using NUnit.Framework;

namespace AquariumCiv.Audio.Tests
{
    /// <summary>#195 phase 1: the day's birds stay fresh (bouts and gaps, random calls, no repeats back to back).</summary>
    public class BirdSongTests
    {
        [Test]
        public void TenMinutesHasGapsBoutsAndManyCallsNeverTheSameClipTwice()
        {
            var b = new BirdSong(11, 5);
            int calls = 0, gapsSeen = 0, last = -1; bool wasBout = true;
            var gaps = new List<float>(); float gapStart = 0f;
            var clips = new HashSet<int>();
            float minSwell = 1f;
            for (float t = 0f; t < 600f; t += 0.02f)
            {
                if (b.Tick(0.02f, 1f, out var c))
                {
                    calls++; clips.Add(c.clip);
                    Assert.AreNotEqual(last, c.clip); last = c.clip;
                    Assert.That(c.volume, Is.InRange(0.4f, 1f)); Assert.That(c.pitch, Is.InRange(0.9f, 1.12f)); Assert.That(c.pan, Is.InRange(-1f, 1f));
                }
                minSwell = System.Math.Min(minSwell, b.Swell);
                if (wasBout && !b.InBout) gapStart = t;
                if (!wasBout && b.InBout) { gapsSeen++; gaps.Add(t - gapStart); }
                wasBout = b.InBout;
            }
            Assert.GreaterOrEqual(calls, 600 / (int)BirdSong.CallMax, "calls come all the time");
            Assert.AreEqual(5, clips.Count, "every clip gets used");
            Assert.GreaterOrEqual(gapsSeen, 3, "the song breaks into bouts");
            Assert.LessOrEqual(minSwell, BirdSong.GapLevel + 0.01f, "a gap is nearly quiet");
            Assert.Greater(new HashSet<float>(gaps.ConvertAll(g => (float)System.Math.Round(g, 1))).Count, 1, "no two gaps the same length");
        }

        [Test]
        public void NoCallsAtNightAndTheSameSeedIsTheSameDay()
        {
            var a = new BirdSong(3, 4);
            for (int i = 0; i < 20000; i++) Assert.IsFalse(a.Tick(0.05f, 0f, out _), "birds level 0: no call");
            var c = new BirdSong(3, 4); var d = new BirdSong(3, 4);
            for (int i = 0; i < 5000; i++)
            {
                bool x = c.Tick(0.1f, 1f, out var cc); bool y = d.Tick(0.1f, 1f, out var dd);
                Assert.AreEqual(x, y); if (x) Assert.AreEqual(cc.clip, dd.clip);
            }
        }

        [Test]
        public void OneClipOrNoneIsSafe()
        {
            var one = new BirdSong(1, 1); var none = new BirdSong(1, 0);
            int n = 0;
            for (int i = 0; i < 6000; i++) { if (one.Tick(0.1f, 1f, out var c)) { n++; Assert.AreEqual(0, c.clip); } Assert.IsFalse(none.Tick(0.1f, 1f, out _)); }
            Assert.Greater(n, 5);
        }
    }
}
