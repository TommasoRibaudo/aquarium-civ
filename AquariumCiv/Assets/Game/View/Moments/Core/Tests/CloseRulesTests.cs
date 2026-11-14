using NUnit.Framework;

namespace AquariumCiv.View.Moments.Tests
{
    /// <summary>When the evening talk starts (#77, first-session-and-check-in.md §4 Choice 6, decisions.md "Session close").</summary>
    public class CloseRulesTests
    {
        static CloseAsk Long(float idle = 0f, float zoom = 1f) => new()
        {
            sessionSeconds = CloseRules.LongSittingSeconds + 1, sinceLastClose = double.MaxValue, idleSeconds = idle, zoom = zoom,
        };

        [Test]
        public void AShortCheckInEndsQuietly()
        {
            var a = Long(idle: 600f, zoom: 1.3f);
            a.sessionSeconds = CloseRules.LongSittingSeconds - 1;
            Assert.IsFalse(CloseRules.ShouldStart(a));
        }

        [Test]
        public void AfterALongSittingTwentySecondsOfQuietStartsIt()
        {
            Assert.IsFalse(CloseRules.ShouldStart(Long(idle: 19f)));
            Assert.IsTrue(CloseRules.ShouldStart(Long(idle: 20f)));
        }

        [Test]
        public void ZoomedInAndQuietIsWatchingNotLeaving()
        {
            // #672 (playtest): the player zoomed in and set the phone down to watch; the close never flew them out
            Assert.IsFalse(CloseRules.ShouldStart(Long(idle: 600f, zoom: 0.4f)));
            Assert.IsTrue(CloseRules.ShouldStart(Long(idle: 20f, zoom: CloseRules.WatchingZoom)));
        }

        [Test]
        public void AfterALongSittingSteppingBackStartsIt()
        {
            Assert.IsFalse(CloseRules.ShouldStart(Long(zoom: 1.1f)));
            Assert.IsTrue(CloseRules.ShouldStart(Long(zoom: CloseRules.StepBackZoom)));
        }

        [Test]
        public void NotWhileAFingerIsDownOrABeatIsUnspent()
        {
            var a = Long(idle: 30f);
            a.touching = true;
            Assert.IsFalse(CloseRules.ShouldStart(a));
            a = Long(idle: 30f);
            a.busy = true;
            Assert.IsFalse(CloseRules.ShouldStart(a));
        }

        [Test]
        public void TheFirstSittingCountsAsLong()
        {
            var a = Long(idle: 30f);
            a.sessionSeconds = 200.0;
            a.firstSitting = true;
            Assert.IsTrue(CloseRules.ShouldStart(a));
        }

        [Test]
        public void ItNeverNags()
        {
            var a = Long(idle: 30f);
            a.sinceLastClose = 60.0;
            Assert.IsFalse(CloseRules.ShouldStart(a));
            a.sinceLastClose = CloseRules.LongSittingSeconds;
            Assert.IsTrue(CloseRules.ShouldStart(a));
        }

        [Test]
        public void ItIsShort()
        {
            Assert.LessOrEqual(CloseRules.TotalSeconds, 8f);
            Assert.AreEqual(CloseStep.Leaving, CloseRules.StepAt(0f));
            Assert.AreEqual(CloseStep.Gather, CloseRules.StepAt(CloseRules.LeavingSeconds));
            Assert.AreEqual(CloseStep.Speak, CloseRules.StepAt(CloseRules.LeavingSeconds + CloseRules.GatherSeconds));
            Assert.AreEqual(CloseStep.Shelf, CloseRules.StepAt(CloseRules.TotalSeconds - 0.1f));
            Assert.AreEqual(CloseStep.Rest, CloseRules.StepAt(CloseRules.TotalSeconds));
        }

        [Test]
        public void AWaitingEraReplacesTheTeaser()
        {
            Assert.AreEqual("They'll wait for you", CloseRules.LastLine(true, EraWaiting.CloseLine, "Mira and Tob"));
            Assert.AreEqual("Mira and Tob", CloseRules.LastLine(false, EraWaiting.CloseLine, "Mira and Tob"));
        }
    }
}
