namespace AquariumCiv.View.ShowMe
{
    /// <summary>#376 (playtest #337): one finger down and up is a tap; a second finger at any time spoils it. The press only
    /// begins when the screen was empty and exactly one finger lands (two landing in the same frame, or one lifting out of
    /// a pinch, is never a press), and it stays spoiled until every finger is up. Pure: touch count and position in.</summary>
    public sealed class TapGesture
    {
        public enum Event { None, Began, Tap, Cancelled }

        int prev;
        bool active, spoiled;
        public float X { get; private set; }
        public float Y { get; private set; }
        public float StartTime { get; private set; }
        /// <summary>Was the last <see cref="Event.Tap"/> short enough (<paramref name="maxSeconds"/>)?</summary>
        public bool Quick { get; private set; }
        public bool Active => active;

        /// <param name="n">Fingers down this frame.</param>
        public Event Update(int n, float x, float y, float t, float slop, float maxSeconds)
        {
            var ev = Event.None;
            if (n >= 2 && prev < 2) { if (active) ev = Event.Cancelled; active = false; spoiled = true; }
            else if (n == 1 && prev == 0 && !spoiled) { active = true; X = x; Y = y; StartTime = t; ev = Event.Began; }
            else if (n == 1 && active)
            {
                float dx = x - X, dy = y - Y;
                if (dx * dx + dy * dy > slop * slop) { active = false; ev = Event.Cancelled; }
            }
            else if (n == 0 && active)
            {
                active = false;
                Quick = t - StartTime <= maxSeconds;
                ev = Event.Tap;
            }
            if (n == 0) spoiled = false;
            prev = n;
            return ev;
        }
    }
}
