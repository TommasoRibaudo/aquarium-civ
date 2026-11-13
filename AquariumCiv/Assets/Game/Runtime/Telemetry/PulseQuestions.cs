using System;
using System.Collections.Generic;

namespace AquariumCiv.Game
{
    /// <summary>
    /// #636: one quick question per session for the first outside playtest. A question, its tap answers, and when it may be asked.
    /// <see cref="rounds"/> lists the day (since install) each round opens: "know" is asked on day 0 and again from day 3.
    /// </summary>
    [Serializable]
    public class PulseDef
    {
        public string id = "";
        public string question = "";
        public string[] answers = Array.Empty<string>();
        /// <summary>The answer whose tap opens a one-line text box before it is sent (-1 = none).</summary>
        public int textOn = -1;
        /// <summary>Every answer offers the text box (an optional "why").</summary>
        public bool textAlways;
        /// <summary>The answer whose tap opens the Feedback sheet (kind Confusing) after it is sent (-1 = none).</summary>
        public int feedbackOn = -1;
        /// <summary>The day each round opens (day 0 = install day). One answer (or two "Not now"s) closes a round.</summary>
        public int[] rounds = { 0 };
        /// <summary>Only in the first <see cref="PulseQuestions.EarlySeconds"/> of a session (what brought you back).</summary>
        public bool early;
        /// <summary>Only in a session that began after the player was away at least <see cref="PulseQuestions.AwayHours"/>.</summary>
        public bool needsAway;
    }

    /// <summary>What a question has had: rounds closed, "Not now"s in the open round, the session it last showed in.</summary>
    [Serializable]
    public class PulseRecord
    {
        public string id = "";
        public int roundsDone;
        public int dismissed;
        public int lastShownSession = -1;
        public int lastAnsweredDay = -1;
    }

    /// <summary>Saved next to the telemetry files (pulse.json).</summary>
    [Serializable]
    public class PulseState
    {
        public int sessions;                 // sessions seen (1 = the first)
        public string lastSessionId = "";
        public int askedSession = -1;        // the session the last question showed in
        public int askedDay = -1;            // the day of the last question
        public int askedToday;               // questions shown on askedDay
        public List<PulseRecord> records = new List<PulseRecord>();

        public PulseRecord Of(string id)
        {
            foreach (var r in records) if (r.id == id) return r;
            var n = new PulseRecord { id = id };
            records.Add(n);
            return n;
        }

        /// <summary>Counts a session the first time its id is seen. Returns the session number.</summary>
        public int See(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return Math.Max(1, sessions);
            if (sessionId != lastSessionId) { lastSessionId = sessionId; sessions++; }
            return sessions;
        }
    }

    /// <summary>What the game knows right now.</summary>
    public struct PulseContext
    {
        public int dayIndex;            // days since install, phone calendar
        public int session;             // PulseState.sessions after See()
        public double sessionSeconds;   // since this session started
        public double awayHours;        // how long the player was away before this session
        public bool firstRunDone;
        public bool choiceThisVisit;    // a choice (dilemma) card was raised or shown this visit: one ask per visit
        public bool busy;               // something is on screen (RecallCalm says not calm)
    }

    /// <summary>
    /// #636: which question to ask now, if any. Pure: the view (PulseQuestionView) feeds it and shows the sheet. Rules: never before the
    /// first run is done, never in a visit that already had a choice, at most one per session and <see cref="MaxPerDay"/> a day, never
    /// in the first <see cref="MinSessionSeconds"/> (except an <see cref="PulseDef.early"/> one, which is only asked early). The first due
    /// question in <see cref="All"/> order wins.
    /// </summary>
    public static class PulseQuestions
    {
        public const int MaxPerDay = 3, MaxDismissals = 2;
        public const double MinSessionSeconds = 20, EarlySeconds = 90, AwayHours = 0.5;

        public static readonly PulseDef[] All =
        {
            new PulseDef { id = "know", question = "Right now, do you know what to do next?", answers = new[] { "Yes", "Sort of", "No" }, rounds = new[] { 0, 3 } },
            new PulseDef { id = "back", question = "What brought you back just now?", answers = new[] { "A notification", "Curious", "Habit", "Had a minute" }, rounds = new[] { 1 }, early = true, needsAway = true },
            new PulseDef { id = "name", question = "Can you name someone who lives in your box?", answers = new[] { "Yes", "Not really" }, textOn = 0, rounds = new[] { 1 } },
            new PulseDef { id = "time", question = "Coming back, did it feel like time had passed in there?", answers = new[] { "Yes", "A bit", "No" }, rounds = new[] { 1 }, needsAway = true },
            new PulseDef { id = "event", question = "Do you remember something that happened in your box lately?", answers = new[] { "Yes", "No" }, textOn = 0, rounds = new[] { 2 } },
            new PulseDef { id = "mine", question = "Is the box growing because of you?", answers = new[] { "Mostly me", "Both", "On its own", "Not sure" }, rounds = new[] { 3, 6 } },
            new PulseDef { id = "tidy", question = "Tidying the box is...", answers = new[] { "Nice", "Fine", "A chore" }, rounds = new[] { 3 } },
            new PulseDef { id = "lost", question = "Anything confusing right now?", answers = new[] { "No", "Yes" }, feedbackOn = 1, rounds = new[] { 2, 5 } },
            new PulseDef { id = "miss", question = "If this box vanished tomorrow, you'd be...", answers = new[] { "Sad", "Shrug", "Relieved" }, rounds = new[] { 5 } },
            new PulseDef { id = "keep", question = "Would you keep playing this?", answers = new[] { "Yes", "Maybe", "No" }, textAlways = true, rounds = new[] { 7 } },
        };

        public static PulseDef Find(string id) => Array.Find(All, d => d.id == id);

        /// <summary>The question to ask now, or null.</summary>
        public static PulseDef Next(PulseState s, PulseContext c, IReadOnlyList<PulseDef> defs = null)
        {
            if (s == null || c.busy || !c.firstRunDone || c.choiceThisVisit) return null;
            if (s.askedSession == c.session) return null;
            if (s.askedDay == c.dayIndex && s.askedToday >= MaxPerDay) return null;
            foreach (var d in defs ?? All)
            {
                if (!Due(s.Of(d.id), d, c)) continue;
                if (d.early ? c.sessionSeconds > EarlySeconds : c.sessionSeconds < MinSessionSeconds) continue;
                if (d.needsAway && c.awayHours < AwayHours) continue;
                return d;
            }
            return null;
        }

        /// <summary>The question's open round has begun, it wasn't answered today, and it didn't show this session.</summary>
        public static bool Due(PulseRecord r, PulseDef d, PulseContext c)
        {
            if (r.roundsDone >= d.rounds.Length) return false;
            if (c.dayIndex < d.rounds[r.roundsDone]) return false;
            if (r.lastAnsweredDay == c.dayIndex) return false; // a repeat waits for another day
            return r.lastShownSession != c.session;
        }

        public static void Shown(PulseState s, PulseDef d, PulseContext c)
        {
            s.Of(d.id).lastShownSession = c.session;
            s.askedSession = c.session;
            if (s.askedDay != c.dayIndex) { s.askedDay = c.dayIndex; s.askedToday = 0; }
            s.askedToday++;
        }

        public static void Answered(PulseState s, PulseDef d, int day)
        {
            var r = s.Of(d.id);
            r.roundsDone++; r.dismissed = 0; r.lastAnsweredDay = day;
        }

        /// <summary>"Not now": asked again in a later session; after <see cref="MaxDismissals"/> the round is given up.</summary>
        public static void Dismissed(PulseState s, PulseDef d)
        {
            var r = s.Of(d.id);
            if (++r.dismissed >= MaxDismissals) { r.roundsDone++; r.dismissed = 0; }
        }

        /// <summary>The issue title: "[Slice] Pulse know (day 3, session 7): Sort of".</summary>
        public static string Title(PulseDef d, string answer, int day, int session, string text)
        {
            var t = $"[Slice] Pulse {d.id} (day {day}, session {session}): {answer}";
            if (!string.IsNullOrWhiteSpace(text))
            {
                var first = text.Trim().Split('\n')[0].Trim();
                if (first.Length > 40) first = first.Substring(0, 37) + "...";
                t += " - " + first;
            }
            return t;
        }

        public static readonly string[] Labels = { "feedback", "fb:pulse", "proto:slice" };
    }
}
