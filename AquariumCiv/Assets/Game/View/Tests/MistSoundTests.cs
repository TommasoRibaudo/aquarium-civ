using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>P8 #266: the mister's "fsss" is made in code. It must be there, soft, band-limited noise and loop without a click;
    /// and the puddle activities are wired into the doll's moves.</summary>
    public class MistSoundTests
    {
        [Test]
        public void TheSpray_IsAudibleBoundedAndLoopsWithoutASeam()
        {
            var d = MistSound.Generate();
            Assert.AreEqual(Mathf.RoundToInt(MistSound.Rate * MistSound.LoopSeconds), d.Length);
            float peak = 0f, rms = 0f, jump = 0f;
            for (int i = 0; i < d.Length; i++) { peak = Mathf.Max(peak, Mathf.Abs(d[i])); rms += d[i] * d[i]; if (i > 0) jump = Mathf.Max(jump, Mathf.Abs(d[i] - d[i - 1])); }
            rms = Mathf.Sqrt(rms / d.Length);
            Assert.That(peak, Is.LessThanOrEqualTo(0.95f).And.GreaterThan(0.5f), "normalised, not clipping");
            Assert.That(rms, Is.GreaterThan(0.08f), "not silence");
            // the step across the wrap is no bigger than the largest step inside the clip
            Assert.That(Mathf.Abs(d[0] - d[d.Length - 1]), Is.LessThanOrEqualTo(jump * 1.5f + 0.02f), "no click where the loop wraps");
        }

        [Test]
        public void TheSpray_IsAirNotATone()
        {
            var d = MistSound.Generate();
            // zero crossings per second of a band of noise at 2-7 kHz sit well above a rumble's
            int zc = 0; for (int i = 1; i < d.Length; i++) if ((d[i - 1] < 0f) != (d[i] < 0f)) zc++;
            float perSecond = zc / MistSound.LoopSeconds;
            Assert.That(perSecond, Is.GreaterThan(3000f), "bright, hissy");
        }

        [Test]
        public void PuddleActivities_HaveMovesAndAGroup()
        {
            Assert.AreEqual(DollStyle.Stomp, DollMotion.StyleOf(ActivityKind.Stomp));
            Assert.AreEqual(DollStyle.Crouch, DollMotion.StyleOf(ActivityKind.WashHands));
            Assert.AreEqual(ActivityGroup.Reaction, ActivityInfo.GroupOf(ActivityKind.Stomp));
            Assert.AreEqual(ActivityGroup.Reaction, ActivityInfo.GroupOf(ActivityKind.WashHands));
        }
    }
}
