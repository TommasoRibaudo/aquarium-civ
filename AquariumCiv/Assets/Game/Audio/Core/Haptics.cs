using System;

namespace AquariumCiv.Audio
{
    /// <summary>
    /// What one buzz is on each platform (#96). Android: a waveform of (milliseconds, amplitude 0..255) pairs for
    /// <c>VibrationEffect.createWaveform(timings, amplitudes, -1)</c> (amplitude 0 = a gap); on phones without amplitude
    /// control phase 2 plays only the longest pulse. iOS: a <c>UIImpactFeedbackGenerator</c> style or a
    /// <c>UINotificationFeedbackGenerator</c> type (the system's own feel, no custom patterns).
    /// </summary>
    public readonly struct HapticPattern
    {
        public enum IosKind { None, Selection, ImpactLight, ImpactMedium, ImpactHeavy, ImpactSoft, NotifySuccess, NotifyWarning }

        public readonly long[] androidTimings;
        public readonly int[] androidAmplitudes;
        public readonly IosKind ios;

        public HapticPattern(IosKind ios, long[] timings, int[] amplitudes)
        { this.ios = ios; androidTimings = timings; androidAmplitudes = amplitudes; }

        public bool IsNone => ios == IosKind.None && (androidTimings == null || androidTimings.Length == 0);
        public long AndroidTotalMs { get { long t = 0; if (androidTimings != null) foreach (var x in androidTimings) t += x; return t; } }
    }

    /// <summary>The kinds as patterns. Soft and short throughout: a calm game, never an alarm [est, tune on a phone].</summary>
    public static class HapticMap
    {
        public static HapticPattern For(HapticKind k) => k switch
        {
            HapticKind.Selection => new(HapticPattern.IosKind.Selection, new long[] { 8 }, new[] { 60 }),
            HapticKind.Light => new(HapticPattern.IosKind.ImpactLight, new long[] { 12 }, new[] { 90 }),
            HapticKind.Medium => new(HapticPattern.IosKind.ImpactMedium, new long[] { 18 }, new[] { 150 }),
            HapticKind.Heavy => new(HapticPattern.IosKind.ImpactHeavy, new long[] { 25 }, new[] { 220 }),
            // two soft taps rising: "done"
            HapticKind.Success => new(HapticPattern.IosKind.NotifySuccess, new long[] { 14, 70, 22 }, new[] { 100, 0, 170 }),
            // one soft, slightly long pulse: "not now", not "wrong"
            HapticKind.Warning => new(HapticPattern.IosKind.ImpactSoft, new long[] { 30 }, new[] { 70 }),
            _ => new(HapticPattern.IosKind.None, Array.Empty<long>(), Array.Empty<int>()),
        };
    }

    /// <summary>
    /// Whether a buzz goes out now (#96): off with the setting; at most one every <see cref="MinGap"/> (a wave of 30
    /// lots at hurry speed still ticks, but never a continuous drone), and a stronger buzz may cut in on a lighter one's
    /// gap. Pure: the view passes unscaled seconds and fires what comes back.
    /// </summary>
    public class HapticGate
    {
        /// <summary>Shortest time between two buzzes (seconds) [est].</summary>
        public const float MinGap = 0.06f;
        /// <summary>Light ticks at most this many per second (the wave, a fast channel stroke) [est].</summary>
        public const int MaxTicksPerSecond = 8;

        float lastAt = float.NegativeInfinity;
        HapticKind lastKind;
        float windowStart = float.NegativeInfinity;
        int ticksInWindow;

        public bool TryBuzz(HapticKind k, float now, bool enabled, out HapticPattern pattern)
        {
            pattern = default;
            if (!enabled || k == HapticKind.None) return false;
            bool light = k == HapticKind.Selection || k == HapticKind.Light;
            if (now - lastAt < MinGap && Strength(k) <= Strength(lastKind)) return false;
            if (light)
            {
                if (now - windowStart >= 1f) { windowStart = now; ticksInWindow = 0; }
                if (ticksInWindow >= MaxTicksPerSecond) return false;
                ticksInWindow++;
            }
            lastAt = now; lastKind = k;
            pattern = HapticMap.For(k);
            return true;
        }

        static int Strength(HapticKind k) => k switch
        {
            HapticKind.Selection => 1, HapticKind.Light => 2, HapticKind.Warning => 2, HapticKind.Medium => 3,
            HapticKind.Success => 4, HapticKind.Heavy => 5, _ => 0,
        };
    }
}
