using NUnit.Framework;

namespace AquariumCiv.GameUI.Tests
{
    public class FrostStatsTests
    {
        [Test]
        public void AWindowGivesAverageAndWorst()
        {
            var w = new FrostWindow(10);
            w.Add(10f, 4f, 2f);
            w.Add(30f, 8f, 6f);
            var r = w.Read(0f);
            Assert.AreEqual(2, r.Frames);
            Assert.AreEqual(20f, r.FrameAvg, 1e-4f);
            Assert.AreEqual(30f, r.FrameWorst, 1e-4f);
            Assert.AreEqual(6f, r.CpuAvg, 1e-4f);
            Assert.AreEqual(8f, r.CpuWorst, 1e-4f);
            Assert.AreEqual(4f, r.GpuAvg, 1e-4f);
            Assert.AreEqual(6f, r.GpuWorst, 1e-4f);
            Assert.AreEqual(50f, r.Fps, 1e-3f);
        }

        [Test]
        public void TheWindowRollsOverItsOldestFrames()
        {
            var w = new FrostWindow(2);
            w.Add(100f, 1f, 1f);
            w.Add(10f, 1f, 1f);
            w.Add(20f, 1f, 1f);
            var r = w.Read(0f);
            Assert.AreEqual(2, r.Frames);
            Assert.AreEqual(20f, r.FrameWorst, 1e-4f);
        }

        [Test]
        public void ADeviceWithoutGpuTimeReadsAsNone()
        {
            var w = new FrostWindow(4);
            w.Add(10f, 4f, 0f);
            Assert.Less(w.Read(0f).GpuAvg, 0f);
            Assert.AreEqual("-", FrostStats.Pair(w.Read(0f).GpuAvg, w.Read(0f).GpuWorst));
        }

        [Test]
        public void GpuAverageIgnoresFramesWithoutATime()
        {
            var w = new FrostWindow(4);
            w.Add(10f, 4f, 0f);
            w.Add(10f, 4f, 8f);
            Assert.AreEqual(8f, w.Read(0f).GpuAvg, 1e-4f);
        }

        [Test]
        public void AFrameOverOneAndAHalfBudgetsIsADrop()
        {
            var w = new FrostWindow(4);
            w.Add(33f, 1f, 1f);
            w.Add(49f, 1f, 1f);
            w.Add(51f, 1f, 1f);
            Assert.AreEqual(1, w.Read(33.3f).Drops);
            Assert.AreEqual(0, w.Read(0f).Drops, "no cap, nothing to miss");
        }

        [Test]
        public void CpuWorkIsTheBusierThreadWithoutThePresentWait()
        {
            Assert.AreEqual(5f, FrostStats.CpuWork(30.0, 25.0, 3.0), 1e-4f);
            Assert.AreEqual(9f, FrostStats.CpuWork(30.0, 25.0, 9.0), 1e-4f);
        }

        [Test]
        public void TheWorstCaseSummaryFitsAFeedbackNote()
        {
            var windows = new[] { new FrostWindow(4), new FrostWindow(4), new FrostWindow(4), new FrostWindow(4) };
            foreach (var w in windows) w.Add(1234.5f, 1234.5f, 1234.5f);
            string header = "[debug] Frost test #178 on a-device-model-name-of-a-good-length\n1179x2556 scale 0.8 cap 60 timing on battery 100>100%";
            string text = FrostStats.Summary(header, windows, 16.7f);
            Assert.LessOrEqual(text.Length, FeedbackReport.MaxTextChars, text);
            foreach (var label in new[] { "Off", "Flat", "1/8 + 6", "1/4 + 14" }) StringAssert.Contains(label + ":", text);
        }

        [Test]
        public void ModesMatchTheKeepersGlassValues()
        {
            Assert.AreEqual(0, FrostModes.Divisor(FrostMode.Off));
            Assert.AreEqual(0, FrostModes.Divisor(FrostMode.Flat));
            Assert.AreEqual(8, FrostModes.Divisor(FrostMode.Eighth));
            Assert.AreEqual(4, FrostModes.Divisor(FrostMode.Quarter));
            Assert.AreEqual(6f, FrostModes.Blur(FrostMode.Eighth));
            Assert.AreEqual(14f, FrostModes.Blur(FrostMode.Quarter));
            Assert.AreEqual(0.72f, FrostModes.TintAlpha(FrostMode.Quarter), 1e-4f);
            Assert.AreEqual(0.92f, FrostModes.TintAlpha(FrostMode.Flat), 1e-4f);
        }
    }
}
