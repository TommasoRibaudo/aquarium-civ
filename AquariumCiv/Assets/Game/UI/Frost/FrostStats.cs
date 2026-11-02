using System;
using System.Globalization;
using System.Text;

namespace AquariumCiv.GameUI
{
    public enum FrostMode { Off, Flat, Eighth, Quarter, QuarterLarge }

    /// <summary>What each mode of the frost test draws: Keeper's Glass values (docs/design/ui/directions/README.md).</summary>
    public static class FrostModes
    {
        public const int Count = 5;
        public const float FrostAlpha = 0.72f, FlatAlpha = 0.92f;

        public static string Label(FrostMode m) => m switch
        {
            FrostMode.Off => "Off",
            FrostMode.Flat => "Flat",
            FrostMode.Eighth => "1/8 + 6",
            FrostMode.Quarter => "1/4 + 14",
            _ => "1/4 + 38",
        };

        /// <summary>Size of the frame copy as 1/n; 0 = no copy at all.</summary>
        public static int Divisor(FrostMode m) => m == FrostMode.Eighth ? 8 : m >= FrostMode.Quarter ? 4 : 0;
        public static float Blur(FrostMode m) => m == FrostMode.Eighth ? 6f : m == FrostMode.Quarter ? 14f : m == FrostMode.QuarterLarge ? 38.5f : 0f;
        public static float TintAlpha(FrostMode m) => m == FrostMode.Flat ? FlatAlpha : FrostAlpha;
    }

    /// <summary>Average and worst of one window, in ms. A negative GPU figure means the device gave none.</summary>
    public readonly struct FrostReading
    {
        public readonly int Frames, Drops;
        public readonly float FrameAvg, FrameWorst, CpuAvg, CpuWorst, GpuAvg, GpuWorst;

        public FrostReading(int frames, int drops, float frameAvg, float frameWorst, float cpuAvg, float cpuWorst, float gpuAvg, float gpuWorst)
        {
            Frames = frames; Drops = drops;
            FrameAvg = frameAvg; FrameWorst = frameWorst;
            CpuAvg = cpuAvg; CpuWorst = cpuWorst;
            GpuAvg = gpuAvg; GpuWorst = gpuWorst;
        }

        public float Fps => FrameAvg > 0f ? 1000f / FrameAvg : 0f;
    }

    /// <summary>A rolling window of frame timings for one mode. Pure, so it is unit-tested.</summary>
    public sealed class FrostWindow
    {
        readonly float[] frame, cpu, gpu;
        int next, count;

        public FrostWindow(int capacity)
        {
            frame = new float[capacity];
            cpu = new float[capacity];
            gpu = new float[capacity];
        }

        public int Count => count;

        public void Clear() { next = 0; count = 0; }

        /// <param name="gpuMs">Zero or less: this device gave no GPU time for the frame.</param>
        public void Add(float frameMs, float cpuMs, float gpuMs)
        {
            frame[next] = frameMs;
            cpu[next] = cpuMs;
            gpu[next] = gpuMs;
            next = (next + 1) % frame.Length;
            if (count < frame.Length) count++;
        }

        /// <param name="budgetMs">One frame at the cap. A frame over 1.5 budgets missed its slot, and counts as a drop. Zero: no cap, no drops counted.</param>
        public FrostReading Read(float budgetMs)
        {
            float fSum = 0f, fMax = 0f, cSum = 0f, cMax = 0f, gSum = 0f, gMax = 0f;
            int gCount = 0, drops = 0;
            for (int i = 0; i < count; i++)
            {
                fSum += frame[i]; fMax = Math.Max(fMax, frame[i]);
                cSum += cpu[i]; cMax = Math.Max(cMax, cpu[i]);
                if (gpu[i] > 0f) { gSum += gpu[i]; gMax = Math.Max(gMax, gpu[i]); gCount++; }
                if (budgetMs > 0f && frame[i] > budgetMs * 1.5f) drops++;
            }
            if (count == 0) return new FrostReading(0, 0, 0f, 0f, 0f, 0f, -1f, -1f);
            return new FrostReading(count, drops, fSum / count, fMax, cSum / count, cMax,
                gCount > 0 ? gSum / gCount : -1f, gCount > 0 ? gMax : -1f);
        }
    }

    /// <summary>The numbers as text, short enough for a feedback note (<see cref="FeedbackReport.MaxTextChars"/>).</summary>
    public static class FrostStats
    {
        /// <summary>What the CPU spent on a frame: the busier thread, without the wait for Present.</summary>
        public static float CpuWork(double mainMs, double presentWaitMs, double renderMs) =>
            (float)Math.Max(mainMs - presentWaitMs, renderMs);

        public static string Pair(float avg, float worst) => avg < 0f
            ? "-"
            : string.Format(CultureInfo.InvariantCulture, "{0:0.0}/{1:0.0}", avg, worst);

        /// <summary>A header line, a legend, then one line per mode that has data.</summary>
        public static string Summary(string header, FrostWindow[] windows, float budgetMs)
        {
            var b = new StringBuilder(header).Append("\nmode: frames fps | frame | cpu | gpu avg/worst ms | drops");
            for (int i = 0; i < windows.Length; i++)
            {
                var r = windows[i].Read(budgetMs);
                if (r.Frames == 0) continue;
                b.Append(string.Format(CultureInfo.InvariantCulture, "\n{0}: {1} {2:0.0} | {3} | {4} | {5} | {6}",
                    FrostModes.Label((FrostMode)i), r.Frames, r.Fps, Pair(r.FrameAvg, r.FrameWorst), Pair(r.CpuAvg, r.CpuWorst), Pair(r.GpuAvg, r.GpuWorst), r.Drops));
            }
            return b.ToString();
        }
    }
}
