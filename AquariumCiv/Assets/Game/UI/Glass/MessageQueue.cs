using System.Collections.Generic;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// One message at a time (hierarchy rule 3): the toast lane and the speech bubble each run on one of these. Pure, so EditMode
    /// tests cover it: the owner passes unscaled seconds to <see cref="Tick"/> and draws <see cref="Current"/>. A hold (a prompt on
    /// screen, the first run's moment) keeps new messages waiting, and takes down the one showing, which comes back when it lifts.
    /// The same text twice in a row is one message, and at most <see cref="MaxWaiting"/> wait: the oldest goes first, so a
    /// repeated hint can't queue up a minute of stale lines.
    /// </summary>
    public class MessageQueue
    {
        public const int MaxWaiting = 3;
        /// <summary>With something waiting, the one showing gets this long at least and then gives way, so a queue can't lag the game.</summary>
        public const float MinSeconds = 1.5f;

        public readonly struct Message
        {
            public readonly string text;
            public readonly float seconds;
            public readonly bool through;
            public Message(string text, float seconds, bool through = false) { this.text = text; this.seconds = seconds; this.through = through; }
        }

        readonly Queue<Message> waiting = new();
        readonly HashSet<string> holds = new();
        bool showing, cleared;
        float until, shownAt;

        /// <summary>The text on screen now, or null.</summary>
        public string Current { get; private set; }
        public int Waiting => waiting.Count;
        public bool Held => holds.Count > 0;

        public void Post(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text == Current) { until = -1f; return; } // again: it stays up for another round
            foreach (var m in waiting) if (m.text == text) return;
            if (waiting.Count >= MaxWaiting) waiting.Dequeue();
            waiting.Enqueue(new Message(text, seconds));
        }

        /// <summary>Tester run 8: the answer to something the player just did goes ahead of what's waiting and past a hold. A prompt held the Clean hint back until it was
        /// over, by when nobody remembered the tap.</summary>
        public void PostThrough(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text == Current) { until = -1f; return; }
            var rest = waiting.ToArray();
            waiting.Clear();
            waiting.Enqueue(new Message(text, seconds, true));
            foreach (var m in rest) if (m.text != text) waiting.Enqueue(m);
        }

        /// <summary>Takes back a line that no longer applies, waiting or on screen (tester run 6).</summary>
        public void Withdraw(string text)
        {
            if (showing && Current == text) { Current = null; showing = false; cleared = true; }
            var rest = waiting.ToArray();
            waiting.Clear();
            foreach (var m in rest) if (m.text != text) waiting.Enqueue(m);
        }

        /// <summary>Holds or releases the queue for <paramref name="reason"/>; several reasons can hold it at once.</summary>
        public void Hold(string reason, bool on)
        {
            if (on) holds.Add(reason); else holds.Remove(reason);
        }

        public void Clear()
        {
            waiting.Clear();
            cleared = Current != null;
            Current = null;
            showing = false;
        }

        /// <summary>Call every frame. Returns true when <see cref="Current"/> changed.</summary>
        public bool Tick(float now)
        {
            bool wiped = cleared;
            cleared = false;
            if (showing && until < 0f) until = now + current.seconds;
            if (Held && !(showing && current.through) && !(waiting.Count > 0 && waiting.Peek().through))
            {
                if (!showing) return wiped;
                // Down for the prompt; what was left comes back after it.
                var back = new Message(Current, current.seconds);
                var rest = waiting.ToArray();
                waiting.Clear();
                waiting.Enqueue(back);
                foreach (var m in rest) waiting.Enqueue(m);
                Current = null;
                showing = false;
                return true;
            }
            if (showing && now < (waiting.Count > 0 ? System.Math.Min(until, shownAt + MinSeconds) : until)) return false;
            bool had = showing;
            if (waiting.Count == 0 || (Held && !waiting.Peek().through))
            {
                Current = null;
                showing = false;
                return had || wiped;
            }
            current = waiting.Dequeue();
            Current = current.text;
            showing = true;
            shownAt = now;
            until = now + current.seconds;
            return true;
        }

        Message current;
    }
}
