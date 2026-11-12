using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.GameUI.Tests
{
    public class HudInkTests
    {
        [Test]
        public void BrightDayIsDarkInkAndNightIsLight()
        {
            Assert.That(HudInk.Target(1f), Is.EqualTo(0f));
            Assert.That(HudInk.Target(0.8f), Is.EqualTo(0f));
            Assert.That(HudInk.Target(0.45f), Is.EqualTo(1f));
            Assert.That(HudInk.Target(0f), Is.EqualTo(1f));
        }

        [Test]
        public void DuskIsBetweenAndFallsMonotonically()
        {
            float prev = -1f;
            for (float d = 1f; d >= 0f; d -= 0.05f)
            {
                float t = HudInk.Target(d);
                Assert.That(t, Is.GreaterThan(prev - 1e-5f));
                prev = t;
            }
            Assert.That(HudInk.Target(0.70f), Is.GreaterThan(0.1f));
            Assert.That(HudInk.Target(0.70f), Is.LessThan(0.9f));
            Assert.That(HudInk.Target(0.67f), Is.GreaterThan(0.8f), "a dusk sky (0.67) already has the light ink");
        }

        [Test]
        public void TheEaseTakesAboutASecondAndNeverSnaps()
        {
            float v = 0f; int frames = 0;
            while (v < 1f && frames < 1000) { v = HudInk.Step(v, 1f, 1f / 60f); frames++; }
            Assert.That(frames, Is.GreaterThan(55));
            Assert.That(frames, Is.LessThan(66));
            Assert.That(HudInk.Step(0f, 1f, 0.016f), Is.LessThan(0.05f));
            Assert.That(HudInk.Step(1f, 0f, 5f), Is.EqualTo(0f)); // a long frame stops at the target
        }

        [Test]
        public void InkAndShadowFollowTheLightness()
        {
            Assert.That(HudInk.Ink(0f), Is.EqualTo(HudInk.Dark));
            Assert.That(HudInk.Ink(1f), Is.EqualTo(HudInk.Light));
            Assert.That(HudInk.Shadow(0f).a, Is.EqualTo(0f));
            Assert.That(HudInk.Shadow(1f).a, Is.EqualTo(HudInk.ShadowAlpha));
        }

        [Test]
        public void OffHomeDarkInkGetsAPaleHaloAndLightInkKeepsItsShadow()
        {
            var pale = HudInk.Shadow(0f, 1f);
            Assert.That(pale.r, Is.GreaterThan(0.8f), "a pale halo under dark ink off home");
            Assert.That(pale.a, Is.GreaterThan(0.5f));
            Assert.That(HudInk.Shadow(0f, 0f), Is.EqualTo(HudInk.Shadow(0f)), "at home: no halo");
            Assert.That(HudInk.Shadow(1f, 1f), Is.EqualTo(HudInk.Shadow(1f)), "light ink keeps its dark shadow");
        }
    }
}
