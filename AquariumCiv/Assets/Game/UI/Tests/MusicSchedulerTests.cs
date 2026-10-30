using System;
using NUnit.Framework;

namespace AquariumCiv.GameUI.Tests
{
    public class MusicSchedulerTests
    {
        static MusicScheduler Make(int seed, int tracks = 2, float openChance = 1f)
        {
            var cfg = MusicScheduler.Settings.Default;
            cfg.OpenChance = openChance;
            return new MusicScheduler(new Random(seed), tracks, cfg);
        }

        static int RunUntilStart(MusicScheduler s, bool blocked = false, float max = 3000f)
        {
            for (float t = 0; t < max; t += 1f)
            {
                int i = s.Tick(1f, blocked);
                if (i >= 0) return i;
            }
            return -1;
        }

        [Test]
        public void OpeningSessionStartsATrackAfterTheShortDelay()
        {
            var s = Make(1);
            s.OnSessionStart();
            Assert.That(s.SecondsToNext, Is.InRange(5f, 20f));
            Assert.That(RunUntilStart(s, max: 21f), Is.GreaterThanOrEqualTo(0));
            Assert.IsTrue(s.Playing);
        }

        [Test]
        public void NothingStartsWithoutASessionOpen()
        {
            Assert.AreEqual(-1, RunUntilStart(Make(1)));
        }

        [Test]
        public void SessionOpenWithoutTheChanceWaitsALongGap()
        {
            var s = Make(1, openChance: 0f);
            s.OnSessionStart();
            Assert.That(s.SecondsToNext, Is.InRange(180f, 480f));
        }

        [Test]
        public void NothingStartsWhileBlocked_AndTheCountdownDoesNotRun()
        {
            var s = Make(1);
            s.OnSessionStart();
            float before = s.SecondsToNext;
            Assert.AreEqual(-1, RunUntilStart(s, blocked: true, max: 100f));
            Assert.AreEqual(before, s.SecondsToNext);
            Assert.That(RunUntilStart(s), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void AfterATrackThereIsALongGapAndNoSameTrackTwiceInARow()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = Make(seed);
                s.OnSessionStart();
                int prev = RunUntilStart(s);
                for (int n = 0; n < 6; n++)
                {
                    s.OnTrackFinished();
                    Assert.That(s.SecondsToNext, Is.InRange(180f, 480f));
                    int next = RunUntilStart(s);
                    Assert.AreNotEqual(prev, next, $"seed {seed}");
                    prev = next;
                }
            }
        }

        [Test]
        public void OffNeverStartsAndRareDoublesTheGap()
        {
            var s = Make(1);
            s.Frequency = MusicFrequency.Off;
            s.OnSessionStart();
            Assert.AreEqual(-1, RunUntilStart(s));
            s.Frequency = MusicFrequency.Rare;
            s.OnTrackFinished();
            Assert.That(s.SecondsToNext, Is.InRange(360f, 960f));
        }

        [Test]
        public void StartNowPlaysImmediatelyAndNoTracksIsSafe()
        {
            var s = Make(1);
            Assert.GreaterOrEqual(s.StartNow(), 0);
            var none = Make(1, tracks: 0);
            none.OnSessionStart();
            Assert.AreEqual(-1, none.StartNow());
            Assert.AreEqual(-1, RunUntilStart(none));
        }
    }
}
