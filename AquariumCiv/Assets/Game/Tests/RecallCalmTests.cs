using AquariumCiv.Game;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#328: the recall question waits for a calm moment. Pure C#, no Editor.</summary>
    public class RecallCalmTests
    {
        static RecallCalmState Calm() => new RecallCalmState { secondsSinceTouch = 10, secondsSinceBusy = 10 };

        [Test] public void AnEmptyQuietScreenIsCalm() => Assert.IsTrue(RecallCalm.CanAskNow(Calm()));

        [Test]
        public void EachBusyThingBlocksIt()
        {
            var s = Calm(); s.sheetOpen = true; Assert.IsFalse(RecallCalm.CanAskNow(s), "sheet (the Kit)");
            s = Calm(); s.toolInHand = true; Assert.IsFalse(RecallCalm.CanAskNow(s), "tool");
            s = Calm(); s.firstRunStep = true; Assert.IsFalse(RecallCalm.CanAskNow(s), "first-run step");
            s = Calm(); s.momentPlaying = true; Assert.IsFalse(RecallCalm.CanAskNow(s), "moment / Show me");
            s = Calm(); s.captionOrToast = true; Assert.IsFalse(RecallCalm.CanAskNow(s), "caption or toast");
            s = Calm(); s.anotherPromptUp = true; Assert.IsFalse(RecallCalm.CanAskNow(s), "another prompt");
        }

        [Test]
        public void ARecentTouchOrARecentBusyMomentBlocksIt()
        {
            var s = Calm(); s.secondsSinceTouch = RecallCalm.CalmSeconds - 0.1; Assert.IsFalse(RecallCalm.CanAskNow(s));
            s = Calm(); s.secondsSinceBusy = RecallCalm.CalmSeconds - 0.1; Assert.IsFalse(RecallCalm.CanAskNow(s));
            s = Calm(); s.secondsSinceTouch = RecallCalm.CalmSeconds; s.secondsSinceBusy = RecallCalm.CalmSeconds; Assert.IsTrue(RecallCalm.CanAskNow(s));
        }

        [Test]
        public void DueButBusyStaysDueUntilItShows()
        {
            // the day stamp is only written by Shown(), so a wait costs nothing: the next calm check (or session) asks again
            var st = new RecallState();
            var c = new RecallContext { dayIndex = 3, sessionSeconds = 60, cardsEverShown = 2 };
            Assert.IsTrue(RecallPrompt.ShouldAsk(st, c));
            var busy = Calm(); busy.sheetOpen = true;
            Assert.IsFalse(RecallCalm.CanAskNow(busy));
            Assert.IsTrue(RecallPrompt.ShouldAsk(st, c), "still due while waiting");
            Assert.IsTrue(RecallCalm.CanAskNow(Calm()));
            RecallPrompt.Shown(st, 3);
            Assert.IsFalse(RecallPrompt.ShouldAsk(st, c), "once shown, not again that day");
        }
    }
}
