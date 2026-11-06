// #360 Drawing water: pure tests of the draw beat (UnityEngine maths only), run in Unity EditMode and in Tools/AnimBench.Tests.
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class WaterBeatsTests
    {
        [Test]
        public void TheDrawFitsTheDawnBlockWithItsLongestDelay()
        {
            const float block = 0.35f * 10f; // 0.35 h at dawn's 10 s per view hour
            for (int slot = 0; slot < 3; slot++)
                for (int key = 0; key < 200; key++)
                    Assert.LessOrEqual(WaterBeats.Delay(key, slot) + WaterBeats.Draw.Duration, block, $"slot {slot} key {key}");
        }

        [Test]
        public void TwoDrawersAtOneBankNeverDipWithin0Point3Seconds()
        {
            for (int a = 0; a < 60; a++)
                for (int b = 0; b < 60; b++)
                    for (int s = 0; s < 2; s++)
                        Assert.GreaterOrEqual(Mathf.Abs(WaterBeats.ContactAt(a, s) - WaterBeats.ContactAt(b, s + 1)), 0.25f);
        }

        [Test]
        public void TheContactIsOneWindow_AndTheBucketIsFullOnlyFromTheLift()
        {
            int hits = 0;
            var under = DollPose.Rest;
            bool was = false;
            for (float t = 0f; t < 4f; t += 1f / 30f)
            {
                var p = under;
                WaterBeats.Apply(ref p, t, 7, 0);
                bool now = p.hit > 0.5f;
                if (now && !was) hits++;
                was = now;
                if (t < WaterBeats.ContactAt(7, 0) + 0.1f) Assert.IsFalse(WaterBeats.Full(t, 7, 0), "empty until it's been under");
            }
            Assert.AreEqual(1, hits);
            Assert.IsTrue(WaterBeats.Full(WaterBeats.FullAt(7, 0) + 0.01f, 7, 0));
            Assert.Greater(WaterBeats.FullAt(7, 0), WaterBeats.ContactAt(7, 0) + 0.5f, "filling takes a while");
        }

        [Test]
        public void TheDipKneelsLowAndTheLiftLeansBack()
        {
            var under = DollPose.Rest;
            var p = under; WaterBeats.Apply(ref p, WaterBeats.ContactAt(3, 0) + 0.3f, 3, 0);
            Assert.Less(p.squash, 0.75f); Assert.Greater(p.lean, 20f); Assert.LessOrEqual(p.lean, 45f, "a kneel, not a dive"); Assert.Less(p.offset.y, -0.05f);
            var q = under; WaterBeats.Apply(ref q, WaterBeats.FullAt(3, 0) + 0.3f, 3, 0);
            Assert.Less(q.lean, 0f, "leaning back on the lift");
            var done = under; WaterBeats.Apply(ref done, 10f, 3, 0);
            Assert.AreEqual(1f, done.squash, 1e-3f, "settled back");
        }

        [Test]
        public void TheHeavyWalkIsLowerAndRolledAwayFromTheBucket()
        {
            var p = DollPose.Rest; WaterBeats.Heavy(ref p, 0f);
            Assert.Less(p.squash, 1f); Assert.Greater(p.roll, 5f);
            Assert.Less(p.offset.y, 0f);
        }
    }
}
