using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.GameUI.Tests
{
    public class SafeInsetsTests
    {
        [Test]
        public void NoNotchMeansNoInsets()
        {
            Assert.AreEqual(Vector4.zero, GameUiPanel.SafeInsets(new Rect(0, 0, 1080, 1920), 1080, 1920));
        }

        [Test]
        public void NotchAndHomeIndicatorBecomePanelUnits()
        {
            // iPhone-like 1170 × 2532: 141 px status/notch at the top, 102 px home indicator at the bottom.
            var i = GameUiPanel.SafeInsets(new Rect(0, 102, 1170, 2532 - 102 - 141), 1170, 2532);
            float s = 1080f / 1170f;
            Assert.AreEqual(0f, i.x, 1e-3f);
            Assert.AreEqual(141f * s, i.y, 1e-3f, "top");
            Assert.AreEqual(0f, i.z, 1e-3f);
            Assert.AreEqual(102f * s, i.w, 1e-3f, "bottom");
        }

        [Test]
        public void LandscapeCutoutsGoToTheSides()
        {
            var i = GameUiPanel.SafeInsets(new Rect(80, 0, 2400 - 160, 1080), 2400, 1080);
            Assert.AreEqual(80f * 1080f / 2400f, i.x, 1e-3f);
            Assert.AreEqual(80f * 1080f / 2400f, i.z, 1e-3f);
        }
    }
}
