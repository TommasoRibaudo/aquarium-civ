using NUnit.Framework;

namespace AquariumCiv.View.Tests
{
    /// <summary>t3-choice (tester run 2 A, F): the rule a choice staged in the box by itself holds its cast by, and the choice tags'
    /// gutter from the screen's sides.</summary>
    public class ChoiceBoxHoldTests
    {
        [Test]
        public void ABoxStagedChoiceHoldsWhileThePlayerLooksAtTheBox()
        {
            Assert.IsTrue(ChoiceSceneView.BoxHolds(0f, true, false, true), "just staged");
            Assert.IsTrue(ChoiceSceneView.BoxHolds(10f, true, false, true), "10 s later the cast is still there (it went after 1.5 s)");
            Assert.IsTrue(ChoiceSceneView.BoxHolds(59f, true, false, true), "a minute is generous");
            Assert.GreaterOrEqual(ChoiceSceneView.BoxHoldSeconds, 60f);
            Assert.LessOrEqual(ChoiceSceneView.BoxHoldSeconds, 90f);
        }

        [Test]
        public void ItLetsGoWhenThePlayerLooksElsewhereOrAfterTheHold()
        {
            Assert.IsFalse(ChoiceSceneView.BoxHolds(5f, false, false, true), "the app went to the back");
            Assert.IsFalse(ChoiceSceneView.BoxHolds(5f, true, true, true), "the player opened something else");
            Assert.IsFalse(ChoiceSceneView.BoxHolds(5f, true, false, false), "the camera left the camp");
            Assert.IsFalse(ChoiceSceneView.BoxHolds(ChoiceSceneView.BoxHoldSeconds, true, false, true), "held long enough: it relaxes");
        }

        [Test]
        public void ATagKeepsTheGutterFromBothSides()
        {
            const float W = 1080f, half = 120f, g = ChoiceSceneView.TagGutter;
            Assert.GreaterOrEqual(g, 32f * W / 1179f, "at least ~32 px on a 1179 px wide phone");
            Assert.AreEqual(half + g, ChoiceSceneView.ClampTagX(5f, half, W, g), 1e-3f, "pushed in from the left edge");
            Assert.AreEqual(W - half - g, ChoiceSceneView.ClampTagX(W, half, W, g), 1e-3f, "and from the right");
            Assert.AreEqual(500f, ChoiceSceneView.ClampTagX(500f, half, W, g), 1e-3f, "in the middle it stays where it hangs");
            Assert.AreEqual(40f + half + g, ChoiceSceneView.ClampTagX(0f, half, W, g, 40f, 0f), 1e-3f, "plus the safe area");
            Assert.AreEqual(W - 30f - half - g, ChoiceSceneView.ClampTagX(W, half, W, g, 0f, 30f), 1e-3f);
            Assert.AreEqual(W * 0.5f, ChoiceSceneView.ClampTagX(10f, W, W, g), 1e-3f, "wider than the room: centred");
        }
    }
}
