using System;
using System.Collections.Generic;

namespace AquariumCiv.Game
{
    /// <summary>
    /// The typed calls the UI makes (#75 cards, #79 time-lapse, #80 book, #88 era moment, the clean gestures). They only build
    /// events; the UI never formats a log line. All safe to call with a null log (logging off).
    /// </summary>
    public static class TelemetryHooks
    {
        // play
        public static void Cleaned(this TelemetryLog l, string kind, bool voluntary) => l?.Log(Ev.Clean, "kind", kind, "voluntary", voluntary);
        public static void CaughtUp(this TelemetryLog l, double awayHours, double livedHours, int months, double ms) =>
            l?.Log(Ev.CatchUp, "away_h", awayHours, "lived_h", livedHours, "months", months, "ms", ms);
        /// <summary>The era moment ended: watched live to the end, or hurried/skipped (#88).</summary>
        public static void EraMomentEnded(this TelemetryLog l, int era, bool skipped, double seconds) =>
            l?.Log(Ev.EraMoment, "era", era, "mode", skipped ? "skipped" : "live", "secs", seconds);

        // cards
        public static void CardShown(this TelemetryLog l, string card, string kind, int index, int total) =>
            l?.Log(Ev.CardShown, "card", card, "kind", kind, "i", index, "n", total);
        /// <summary>Dwell comes from <see cref="DwellTracker"/>.</summary>
        public static void CardRead(this TelemetryLog l, string card, double dwellSeconds) => l?.Log(Ev.CardRead, "card", card, "dwell_ms", Math.Round(dwellSeconds * 1000));
        public static void CardOpened(this TelemetryLog l, string card, int personId) => l?.Log(Ev.CardOpened, "card", card, "person", personId);
        public static void CardFlownTo(this TelemetryLog l, string card) => l?.Log(Ev.CardFlownTo, "card", card);
        public static void ShowMe(this TelemetryLog l, string card) => l?.Log(Ev.ShowMe, "card", card);
        public static void SkipAll(this TelemetryLog l, int cardsLeft) => l?.Log(Ev.SkipAll, "left", cardsLeft);
        public static void PeekTap(this TelemetryLog l, string card) => l?.Log(Ev.PeekTap, "card", card);

        // history
        public static void HistoryOpened(this TelemetryLog l, string via, bool prompted) => l?.Log(Ev.HistoryOpen, "via", via, "prompted", prompted);
        public static void TimelapseOpened(this TelemetryLog l, bool prompted) => l?.Log(Ev.TimelapseOpen, "prompted", prompted);
        public static void TimelapseScrubbed(this TelemetryLog l, double fromYear, double toYear) => l?.Log(Ev.TimelapseScrub, "from_year", fromYear, "to_year", toYear);
        public static void TimelapsePlayed(this TelemetryLog l) => l?.Log(Ev.TimelapsePlay);
        public static void PinTapped(this TelemetryLog l, int personId) => l?.Log(Ev.PinTap, "person", personId);
        public static void SnapshotTaken(this TelemetryLog l) => l?.Log(Ev.Snapshot);
        public static void BookPageTurned(this TelemetryLog l, int page) => l?.Log(Ev.BookPage, "page", page);
        public static void RememberTapped(this TelemetryLog l, int personId) => l?.Log(Ev.RememberTap, "person", personId);

        // recall question (#100)
        public static void RecallShown(this TelemetryLog l, int dayIndex) => l?.Log(Ev.RecallShown, "day", dayIndex);
        public static void RecallAnswered(this TelemetryLog l, int storiesRecalled) => l?.Log(Ev.RecallAnswered, "count", storiesRecalled);
        public static void RecallDismissed(this TelemetryLog l) => l?.Log(Ev.RecallDismissed);
    }

    /// <summary>
    /// Time a card is actually in front of the player (P3 polish): the clock stops while a person card or a fly-to covers it.
    /// Time is passed in (real seconds), so it is testable and ignores frame-rate.
    /// </summary>
    public class DwellTracker
    {
        string card;
        double since, total;
        int covers;

        public string Current => card;
        public bool Covered => covers > 0;

        /// <summary>A card appeared. Ends the previous one first (returns its read, or null).</summary>
        public (string card, double seconds)? Show(string cardId, double now)
        {
            var prev = End(now);
            card = cardId; total = 0; covers = 0; since = now;
            return prev;
        }

        /// <summary>A person card or fly-to now covers the card (calls nest).</summary>
        public void Cover(double now)
        {
            if (card == null) return;
            if (covers++ == 0) total += now - since;
        }

        public void Uncover(double now)
        {
            if (card == null || covers == 0) return;
            if (--covers == 0) since = now;
        }

        /// <summary>The card was dismissed or the app paused: returns its dwell, or null when none was showing.</summary>
        public (string card, double seconds)? End(double now)
        {
            if (card == null) return null;
            double secs = total + (covers == 0 ? now - since : 0);
            var r = (card, Math.Max(0, secs));
            card = null; total = 0; covers = 0;
            return r;
        }

        /// <summary>Convenience: end and log the read.</summary>
        public void EndAndLog(TelemetryLog log, double now)
        {
            var r = End(now);
            if (r.HasValue) log.CardRead(r.Value.card, r.Value.seconds);
        }
    }
}
