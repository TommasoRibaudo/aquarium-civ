// #364 Fishing pays off: pure tests of the schedule and the poses (UnityEngine maths only), run in Unity EditMode and in Tools/AnimBench.Tests.
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    public class FishBeatsTests
    {
        [Test]
        public void BitesComeEvery2To4TugsSoNineToEighteenSecondsApart()
        {
            for (int key = 0; key < 300; key++)
            {
                Assert.That(FishBeats.FirstTug(key), Is.InRange(2.5f, 4.5f));
                float prev = FishBeats.FirstTug(key) - FishBeats.TugCycle; // a group counts from the tug before the first
                for (int k = 0; k < 8; k++)
                {
                    float at = FishBeats.BiteAt(key, k);
                    int tugs = FishBeats.TugsIn(key, k);
                    Assert.That(tugs, Is.InRange(2, 4));
                    if (k > 0) Assert.AreEqual(tugs * FishBeats.TugCycle, at - FishBeats.BiteAt(key, k - 1), 1e-3f, $"key {key} bite {k}");
                    Assert.Greater(at, prev);
                    prev = at;
                }
            }
        }

        [Test]
        public void TwoFishersAreNeverInStep_AndAboutOneBiteInThreeMisses()
        {
            int same = 0, misses = 0, total = 0;
            for (int a = 0; a < 100; a++)
                for (int b = a + 1; b < 100; b++) if (Mathf.Abs(FishBeats.BiteAt(a, 0) - FishBeats.BiteAt(b, 0)) < 0.2f) same++;
            for (int key = 0; key < 400; key++) for (int k = 0; k < 6; k++) { total++; if (FishBeats.Miss(key, k)) misses++; }
            Assert.Less(same, 4950 / 8, "first bites spread over the 2 s window and the group sizes");
            Assert.That(misses / (float)total, Is.InRange(0.27f, 0.40f));
        }

        [Test]
        public void TheContactIsOneWindowAtThePointNineFiveAndTheFishIsOnItsWay()
        {
            foreach (bool miss in new[] { false, true })
            {
                var beat = miss ? FishBeats.MissBeat : FishBeats.CatchBeat;
                Assert.AreEqual(0.95f, beat.ContactAt, 1e-3f);
                Assert.AreEqual(2.0f, beat.Duration, 0.15f, "a bite beat is about 2 s");
                int hits = 0;
                for (float t = 0f; t < beat.Duration + 0.5f; t += 1f / 60f)
                {
                    var p = DollPose.Rest; p.armR = new Vector3(118f, 0f, 6f);
                    beat.Apply(ref p, t);
                    if (p.hit > 0.5f) hits++;
                }
                Assert.That(hits, Is.InRange(3, 6), "one window of the 0.06 s contact");
            }
            Assert.AreEqual(FishBeats.ContactAt + 0.6f, FishBeats.LandAt, 1e-4f);
        }

        [Test]
        public void TheYankReachesTheTopAtContact_AndTheNibbleDipsTwice()
        {
            var p = DollPose.Rest; p.armR = new Vector3(118f, 0f, 6f);
            FishBeats.CatchBeat.Apply(ref p, FishBeats.ContactAt + 0.01f);
            Assert.AreEqual(165f, p.armR.x, 1f, "the top of the yank");
            float low1 = 999f, low2 = 999f;
            for (float t = 0f; t < 0.6f; t += 0.01f)
            {
                var q = DollPose.Rest; q.armR = new Vector3(118f, 0f, 6f);
                FishBeats.CatchBeat.Apply(ref q, t);
                if (t < 0.3f) low1 = Mathf.Min(low1, q.armR.x); else low2 = Mathf.Min(low2, q.armR.x);
            }
            Assert.AreEqual(100f, low1, 2f); Assert.AreEqual(100f, low2, 2f);
        }

        [Test]
        public void ACatchLandsInTheBasketAfterTheFlight_AMissNeverDoes()
        {
            for (int key = 0; key < 50; key++)
            {
                int k = FishBeats.NextBite(key, 0f, true);
                Assert.IsFalse(FishBeats.Miss(key, k));
                float at = FishBeats.BiteAt(key, k);
                int before = FishBeats.CatchesBy(key, at + FishBeats.LandAt - 0.02f);
                Assert.AreEqual(before + 1, FishBeats.CatchesBy(key, at + FishBeats.LandAt + 0.02f), $"key {key}");
                for (int j = 0; j < 6; j++)
                    if (FishBeats.Miss(key, j)) Assert.AreEqual(FishBeats.CatchesBy(key, FishBeats.BiteAt(key, j) + 1.5f), FishBeats.CatchesBy(key, FishBeats.BiteAt(key, j) - 0.1f), 0);
            }
        }

        [Test]
        public void TheBasketShowsAtMostTwo_AndOneLessWhileTheLastIsHeldUp()
        {
            int key = 7;
            float late = FishBeats.BiteAt(key, 6) + 3f;
            Assert.AreEqual(FishBeats.MaxShown, FishBeats.Shown(key, late, 50f));
            Assert.IsFalse(FishBeats.HoldsFish(key, late, 50f));
            Assert.IsTrue(FishBeats.HoldsFish(key, late, FishBeats.EndSeconds - FishBeats.FishInHandAt - 0.01f), "the fish is in the hand once the line is in");
            Assert.IsFalse(FishBeats.HoldsFish(key, 1f, 0.5f), "no catch yet: nothing to hold up");
            Assert.AreEqual(0, FishBeats.Shown(key, 1f, 0.5f));
        }

        [Test]
        public void TheEndBeatLiftsTheArmAndHolds_WithOrWithoutACatch()
        {
            int key = 7;
            foreach (float s in new[] { 1f, FishBeats.BiteAt(key, 3) + 3f })
            {
                var p = DollPose.Rest; p.armR = new Vector3(118f, 0f, 6f);
                FishBeats.Apply(ref p, s, key, 0.2f);
                Assert.GreaterOrEqual(p.armR.x, 148f, "the arm is up at the end");
                var q = DollPose.Rest; q.armR = new Vector3(118f, 0f, 6f);
                FishBeats.Apply(ref q, s, key, FishBeats.EndSeconds + 1f);
                Assert.Less(Mathf.Abs(q.armR.x - 118f), 30f, "before the end beat it is the loop");
                Assert.AreEqual(0f, q.hit);
            }
        }

        [Test]
        public void TheRegularTugsSkipTheBites()
        {
            int key = 11;
            float first = FishBeats.FirstTug(key);
            Assert.Greater(FishBeats.Tug(key, first), 20f, "the first tug is a plain tug");
            float bite = FishBeats.BiteAt(key, 0);
            Assert.AreEqual(0f, FishBeats.Tug(key, bite), "the bite's tug is the bite, not a tug");
            Assert.AreEqual(0f, FishBeats.Tug(key, first - 1f));
        }
    }
}
