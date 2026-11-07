using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>t4-fixes (tester run 3, a04_choice_staged): the two choice tags overlapped for the first ~1.5 s while the cast walked in
    /// ("full packs" over "a place by the fire"), and a tag's mark meant nothing (a pot on "Agnes"). The pure layout rules.</summary>
    public class ChoiceTagLayoutTests
    {
        const float G = ChoiceSceneView.TagSeparation;

        static Rect R(Vector2 at, Vector2 size) => new Rect(at.x - size.x * 0.5f, at.y - size.y, size.x, size.y);

        static float Gap(Rect a, Rect b) => Mathf.Max(Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax), Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));

        [Test]
        public void OverlappingTagsArePushedApartSideBySideKeepingTheGutter()
        {
            var a = new Vector2(500f, 900f); var b = new Vector2(540f, 910f);
            var sa = new Vector2(260f, 88f); var sb = new Vector2(300f, 88f);
            ChoiceSceneView.SeparateTags(ref a, ref b, sa, sb, G, 40f, 1040f, 60f);
            Assert.GreaterOrEqual(Gap(R(a, sa), R(b, sb)), G - 1e-3f, "at least the gutter between them");
            Assert.Less(a.x, b.x, "the left one stays left");
            Assert.AreEqual(900f, a.y, 1e-3f, "side by side: same heights");
            Assert.AreEqual(910f, b.y, 1e-3f);
            Assert.AreEqual((500f + 540f) * 0.5f, (a.x + b.x) * 0.5f, 1e-3f, "apart about their middle");
        }

        [Test]
        public void TheRightOneOfTheTwoMayBeOptionZero()
        {
            var a = new Vector2(560f, 900f); var b = new Vector2(520f, 900f);
            var s = new Vector2(240f, 88f);
            ChoiceSceneView.SeparateTags(ref a, ref b, s, s, G, 40f, 1040f, 60f);
            Assert.Greater(a.x, b.x, "order kept");
            Assert.GreaterOrEqual(Gap(R(a, s), R(b, s)), G - 1e-3f);
        }

        [Test]
        public void PushedApartNearAnEdgeTheyStayInside()
        {
            var a = new Vector2(180f, 900f); var b = new Vector2(200f, 900f);
            var s = new Vector2(280f, 88f);
            ChoiceSceneView.SeparateTags(ref a, ref b, s, s, G, 40f, 1040f, 60f);
            Assert.GreaterOrEqual(R(a, s).xMin, 40f - 1e-3f, "inside the left gutter");
            Assert.LessOrEqual(R(b, s).xMax, 1040f + 1e-3f);
            Assert.GreaterOrEqual(Gap(R(a, s), R(b, s)), G - 1e-3f);
        }

        [Test]
        public void TooWideToSitSideBySideTheyStackWithTheGutter()
        {
            var a = new Vector2(540f, 900f); var b = new Vector2(530f, 920f);
            var s = new Vector2(600f, 88f);
            ChoiceSceneView.SeparateTags(ref a, ref b, s, s, G, 40f, 1040f, 60f);
            Assert.GreaterOrEqual(Gap(R(a, s), R(b, s)), G - 1e-3f, "one above the other");
            Assert.Less(a.y, b.y, "the higher one stays higher");
            Assert.AreEqual(920f, b.y, 1e-3f, "the lower one stays: the higher goes up");
        }

        [Test]
        public void AtTheTopTheLowerOneTakesTheRest()
        {
            var a = new Vector2(540f, 160f); var b = new Vector2(540f, 170f);
            var s = new Vector2(600f, 88f);
            ChoiceSceneView.SeparateTags(ref a, ref b, s, s, G, 40f, 1040f, 60f);
            Assert.GreaterOrEqual(a.y - s.y, 60f - 1e-3f, "not above the top");
            Assert.GreaterOrEqual(Gap(R(a, s), R(b, s)), G - 1e-3f);
        }

        [Test]
        public void TagsAlreadyApartAreLeftAlone()
        {
            var a = new Vector2(250f, 900f); var b = new Vector2(800f, 700f);
            var s = new Vector2(260f, 88f);
            ChoiceSceneView.SeparateTags(ref a, ref b, s, s, G, 40f, 1040f, 60f);
            Assert.AreEqual(new Vector2(250f, 900f), a);
            Assert.AreEqual(new Vector2(800f, 700f), b);
            Assert.GreaterOrEqual(G, 36f, "36 px gutters");
        }

        [Test]
        public void ASideIsSettledOnceItsCastHasNearlyArrived()
        {
            Assert.IsFalse(ChoiceSceneView.SideSettled(0.5f, 0.3f), "still walking in: no tag, no touch on the walker");
            Assert.IsFalse(ChoiceSceneView.SideSettled(0.2f, 1.5f), "1.5 s in and still on the way (tester run 3)");
            Assert.IsTrue(ChoiceSceneView.SideSettled(ChoiceSceneView.SettleDistance, 0.5f), "there");
            Assert.IsTrue(ChoiceSceneView.SideSettled(0f, 0f), "nobody walks (a prop, the travellers)");
            Assert.IsTrue(ChoiceSceneView.SideSettled(1f, ChoiceSceneView.SettleMaxSeconds), "held up on the way: the tag comes anyway");
        }

        [Test]
        public void ATagsMarkMeansSomething()
        {
            Assert.AreEqual("person", ChoiceSceneView.TagMark(new DilemmaOptionView { personId = 4, label = "Agnes" }), "a who-choice: a person");
            Assert.AreEqual("tree", ChoiceSceneView.TagMark(new DilemmaOptionView { personId = 4, icon = "tree" }), "the option's own mark first");
            Assert.IsNull(ChoiceSceneView.TagMark(new DilemmaOptionView { label = "full packs" }), "nothing that means anything: no mark");
            Assert.IsNull(ChoiceSceneView.TagMark(null));
        }
    }
}
