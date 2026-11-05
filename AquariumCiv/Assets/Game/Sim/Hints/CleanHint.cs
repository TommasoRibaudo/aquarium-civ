namespace AquariumCiv.Sim
{
    /// <summary>What to do for a touch that started on mess (#160).</summary>
    public struct CleanHintResult
    {
        public bool pulse;   // pulse the Clean button
        public bool hint;    // and show the short hint (a pulse alone is the repeat)
    }

    /// <summary>
    /// #160: a touch that starts on a weed while the Keeper is not in clean mode points to the Clean button,
    /// without nagging. Pure C#: the view calls <see cref="Touch"/> at touch start (the drag still moves the camera) and
    /// <see cref="Cleaned"/> when the Keeper has cleaned anything. The pulse repeats after <see cref="PulseGap"/> seconds,
    /// the words after <see cref="HintGap"/>, at most <see cref="MaxHints"/> times a session, and never again once the
    /// Keeper has cleaned: they know. A tap is another matter: it always answers, see <see cref="Tapped"/>. Times are real seconds on any steady clock.
    /// </summary>
    public sealed class CleanHint
    {
        public const string Text = "Tap Clean to wipe the mess.";
        public const float PulseGap = 6f, HintGap = 45f;
        public const int MaxHints = 3;

        float lastPulse = float.NegativeInfinity, lastHint = float.NegativeInfinity;
        int hints;
        bool learned;

        public int HintsShown => hints;
        public bool Learned => learned;

        public CleanHintResult Touch(bool inCleanMode, bool onMess, float now)
        {
            var r = new CleanHintResult();
            if (inCleanMode || !onMess || learned) return r;
            if (now - lastPulse >= PulseGap) { r.pulse = true; lastPulse = now; }
            if (r.pulse && hints < MaxHints && now - lastHint >= HintGap) { r.hint = true; lastHint = now; hints++; }
            return r;
        }

        /// <summary>Tester run 8: a tap that stopped on mess and opened nothing never goes unanswered, however soon after the last: the pulse at least, the words
        /// as they'd come anyway. Tester run 9: that holds after the first clean too; only the words stop. The press may have answered already (<paramref name="pressAnswered"/>): one tap, one answer.</summary>
        public CleanHintResult Tapped(bool pressAnswered, float now)
        {
            var r = new CleanHintResult();
            if (pressAnswered) return r;
            r.pulse = true;
            lastPulse = now;
            if (!learned && hints < MaxHints && now - lastHint >= HintGap) { r.hint = true; lastHint = now; hints++; }
            return r;
        }

        /// <summary>The Keeper cleaned something in clean mode: no more hints this session.</summary>
        public void Cleaned() => learned = true;

        /// <summary>Debug panel: forget everything, so the hint shows again.</summary>
        public void Reset() { lastPulse = lastHint = float.NegativeInfinity; hints = 0; learned = false; }
    }
}
