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
        public void TheInkIsNeverAMidTone_EveryDaylightPicksDarkOrLight()
        {
            for (float d = 1f; d >= 0f; d -= 0.01f)
            {
                float t = HudInk.Target(d);
                Assert.That(t == 0f || t == 1f, $"daylight {d}: target {t}");
                Assert.That(HudInk.Target(d, 0f) == 0f || HudInk.Target(d, 0f) == 1f);
                Assert.That(HudInk.Target(d, 1f) == 0f || HudInk.Target(d, 1f) == 1f);
            }
            Assert.That(HudInk.Target(0.67f), Is.EqualTo(1f), "a dusk sky (0.67) already has the light ink");
        }

        [Test]
        public void ARunningDuskInsideTheBandKeepsItsSide_NoFlicker()
        {
            // tester fb7: a running dusk sat at 0.75, 0.74, 0.72, 0.71 for a long stretch
            float ink = 0f;
            foreach (var d in new[] { 0.80f, 0.75f, 0.74f, 0.72f, 0.71f, 0.72f, 0.71f })
            {
                ink = HudInk.Target(d, ink);
                Assert.That(ink, Is.EqualTo(0f), $"still dark ink at {d}: no mid-tone, no flip");
            }
            ink = HudInk.Target(0.69f, ink);
            Assert.That(ink, Is.EqualTo(1f), "below the band it turns light");
            foreach (var d in new[] { 0.71f, 0.73f, 0.72f })
            {
                ink = HudInk.Target(d, ink);
                Assert.That(ink, Is.EqualTo(1f), $"back up in the band ({d}) it stays light");
            }
            Assert.That(HudInk.Target(0.75f, ink), Is.EqualTo(0f), "above the band it turns dark again");
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
