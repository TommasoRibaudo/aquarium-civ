using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>The care layer's pure parts (#37): the grime mask's wipe maths, the sunlight curve, the camera gate's claim.</summary>
    public class CareTests
    {
        static GlassMask Pane(float level = 1f)
        {
            var m = new GlassMask(new Vector2(6f, 3f), 32f, 0);
            m.ResetTo(level);
            return m;
        }

        [Test]
        public void Mask_SizeFollowsPaneAndTexelDensity()
        {
            var m = new GlassMask(new Vector2(6f, 3f), 32f, 0);
            Assert.AreEqual(192, m.width);
            Assert.AreEqual(96, m.height);
            Assert.AreEqual(192 * 96, m.Texels);
        }

        [Test]
        public void ResetTo_UnevenButAboutTheLevel()
        {
            var m = Pane(0.5f);
            Assert.AreEqual(0.5f, m.Mean, 0.08f, "the weights average out to about one");
            float min = 1f, max = 0f;
            foreach (float g in m.grime) { min = Mathf.Min(min, g); max = Mathf.Max(max, g); }
            Assert.Greater(max - min, 0.2f, "grime is uneven, not a flat tint");
        }

        [Test]
        public void Brush_RemovesGrimeUnderTheFingerAndReportsIt()
        {
            var m = Pane(1f);
            float before = m.Mean;
            double removed = m.Brush(new Vector2(0.5f, 0.5f), 0.3f, 0.5f);
            Assert.Greater(removed, 0.0);
            Assert.AreEqual(before - (float)(removed / m.Texels), m.Mean, 1e-4f, "the mean drops by exactly what was reported");
            Assert.Less(m.GrimeAt(new Vector2(0.5f, 0.5f), 0.1f), m.GrimeAt(new Vector2(0.05f, 0.1f), 0.1f), "the centre is cleaner than the far corner");
            Assert.IsTrue(m.dirty, "the texture needs an upload");
        }

        [Test]
        public void Brush_OutsideThePane_RemovesNothing()
        {
            var m = Pane(1f);
            Assert.AreEqual(0.0, m.Brush(new Vector2(3f, 3f), 0.3f, 0.5f));
        }

        [Test]
        public void BrushLine_StampsAlongTheSegment_NoGaps()
        {
            var m = Pane(1f);
            m.BrushLine(new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f), 0.3f, 0.5f);
            for (float u = 0.12f; u <= 0.88f; u += 0.04f)
                Assert.Less(m.GrimeAt(new Vector2(u, 0.5f), 0.05f), 0.3f, $"the line is clean at u={u}");
        }

        [Test]
        public void Zigzag_CleansAWholePaneInAFewPasses_AndSnapsSpotless()
        {
            var m = Pane(1f);
            float radius = 0.065f * 5f; // a fingertip and a half at the camera's usual distance (about 5 units per screen height)
            int passes = 0;
            for (float v = radius * 0.5f; v < 3f + radius * 0.5f; v += radius * 1.2f)
            {
                float vv = Mathf.Clamp01(v / 3f);
                m.BrushLine(new Vector2(0f, vv), new Vector2(1f, vv), radius, 0.5f);
                passes++;
            }
            Assert.LessOrEqual(passes, 8, "a few sweeps across the glass");
            Assert.Less(m.Mean, 0.15f, "the pane is nearly clean after one zigzag");
            m.Clear();
            Assert.AreEqual(0f, m.Mean);
            Assert.IsTrue(CareMath.WeightedMean(new[] { m }) < CareMath.SpotlessMean);
        }

        [Test]
        public void NoGrimeBelow_KeepsTheSoilStripClean_AndTheMeanStillFollowsTheLevel()
        {
            var m = new GlassMask(new Vector2(6f, 3f), 32f, 1);
            m.NoGrimeBelow(u => 1f); // ground one unit up the pane
            m.ResetTo(0.5f);
            Assert.AreEqual(0f, m.GrimeAt(new Vector2(0.5f, 0.1f), 0.2f), 1e-4f, "nothing under the ground");
            Assert.Greater(m.GrimeAt(new Vector2(0.5f, 0.8f), 0.2f), 0.2f, "grime above it");
            Assert.AreEqual(0.5f, m.Mean, 0.1f, "and the mean follows the level it was given");
            m.Grow(0.2f);
            Assert.AreEqual(0.7f, m.Mean, 0.1f);
        }

        [Test]
        public void Grow_RaisesTheMean_ByAboutTheDelta_AndRearmsTheClearedCheck()
        {
            var m = Pane(0f);
            m.Grow(0.4f);
            Assert.AreEqual(0.4f, m.Mean, 0.08f);
            Assert.IsFalse(m.CheckCleared(), "dirty is not cleared");
            m.Clear();
            Assert.IsTrue(m.CheckCleared(), "dirty to clean fires once");
            Assert.IsFalse(m.CheckCleared(), "and not again until it grows dirty again");
            m.Grow(0.5f);
            m.Clear();
            Assert.IsTrue(m.CheckCleared());
        }

        [Test]
        public void FadeTo_ScalesTheMask_ToTheTarget()
        {
            var m = Pane(0.8f);
            m.FadeTo(m.Mean * 0.5f);
            Assert.AreEqual(0.4f, m.Mean, 0.06f);
            m.FadeTo(0f);
            Assert.AreEqual(0f, m.Mean);
        }

        [Test]
        public void SunScale_DimsBy25PercentAtMost()
        {
            Assert.AreEqual(1f, CareMath.SunScale(0f), 1e-6f);
            Assert.AreEqual(0.875f, CareMath.SunScale(0.5f), 1e-6f);
            Assert.AreEqual(0.75f, CareMath.SunScale(1f), 1e-6f, "Gate 1: at most -25 % at 48 h of mess");
            Assert.AreEqual(0.75f, CareMath.SunScale(3f), 1e-6f, "never below");
            Assert.AreEqual(1f, CareMath.SunScale(-1f), 1e-6f);
            for (float m = 0f; m < 1f; m += 0.05f) Assert.GreaterOrEqual(CareMath.SunScale(m), CareMath.SunScale(m + 0.05f), "a monotone curve");
        }

        [Test]
        public void Gate_IgnoresPresses_WhenCleanModeIsOff()
        {
            CareView.AutoCreate = false;
            var go = new GameObject("CareGateTest");
            try
            {
                var care = go.AddComponent<CareView>();
                Assert.IsFalse(care.CleanMode);
                Assert.IsFalse(care.ClaimsPress(new Vector2(500f, 1000f)), "one finger is the camera's unless clean mode is on");
                Assert.IsTrue(care is ICameraGate);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
