using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>What the player has read of the chronicle cards (#75), saved with the box so it survives a kill.</summary>
    [Serializable]
    public class CardLedger
    {
        /// <summary>A save from before the ledger (the field keeps this default): see SimCore.StartCardLedger.</summary>
        public const long NotStarted = -2;
        public long since = NotStarted;     // every entry up to this month is done with (exclusive, like ReturnCards)
        public List<int> read = new();      // entries after since that were read (cards are not read in month order)
        public List<int> viewed = new();    // people whose card was opened since the last return (§2.2: +0.05)
    }

    /// <summary>
    /// The unread side of the cards (#75; chronicle-and-history.md §2.2: "unread cards wait for the next open; nothing is lost").
    /// Two things leave the stack only when they are resolved, not when read: an open wish ("it just stops once given") and a
    /// waiting era ("the era waits"). Story only: no dice, nothing in the economy reads this.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>A new world starts with nothing read; a save from before the ledger starts now, or the whole history
        /// would come back as "unread" (the save's own time away is still told: this runs before the catch-up).</summary>
        void StartCardLedger()
        {
            var c = State.cards ??= new CardLedger();
            if (c.since == CardLedger.NotStarted) c.since = State.month > 0 ? State.month : -1;
        }

        /// <summary>"Ready for a new age" is stale once the age came, or for an older age.</summary>
        bool EraReadyLive(ChronicleEntry e) => e.kind != "era-ready" || (State.eraReady && e.month >= State.eraReadyMonth);

        /// <summary>Stays in the stack, read or not, until it's resolved.</summary>
        bool CardWaits(ChronicleEntry e) =>
            (e.kind == "wish" && WishOfEntry(e.id)?.state == WishState.Open) || (e.kind == "era-ready" && EraReadyLive(e));

        bool CardUnread(ChronicleEntry e, HashSet<int> read) =>
            e.month > State.cards.since && !read.Contains(e.id) && ShownAsCard(e) && WishCardLive(e) && EraReadyLive(e);

        /// <summary>The cards for a return from what's unread, whatever month it dates from.</summary>
        public ReturnStack UnreadCards(double realHoursAway, IEnumerable<int> viewedPeople = null) =>
            UnreadCardsCapped(CardCap(realHoursAway), viewedPeople);

        /// <summary><see cref="UnreadCards"/> with an explicit cap (debug).</summary>
        public ReturnStack UnreadCardsCapped(int cap, IEnumerable<int> viewedPeople = null)
        {
            AnnotateNew();
            var read = new HashSet<int>(State.cards.read);
            return BuildStack(State.chronicle.Where(e => CardUnread(e, read) || (ShownAsCard(e) && CardWaits(e))).ToList(), cap, viewedPeople);
        }

        /// <summary>Anything still unread (the dot on the history button). A wish or an era already read doesn't count.</summary>
        public bool HasUnreadCards
        {
            get
            {
                AnnotateNew();
                var read = new HashSet<int>(State.cards.read);
                return State.chronicle.Any(e => CardUnread(e, read));
            }
        }

        public bool IsCardRead(ChronicleCard c) => c != null && c.entries.All(e => e.month <= State.cards.since || State.cards.read.Contains(e.id));

        public void MarkCardRead(ChronicleCard c)
        {
            if (c == null) return;
            foreach (var e in c.entries)
                if (e.month > State.cards.since && !State.cards.read.Contains(e.id)) State.cards.read.Add(e.id);
        }

        /// <summary>The stack closed: move <see cref="CardLedger.since"/> up to the first unread entry and forget the read ids
        /// behind it, so the list stays as short as what's waiting. Never past last month: this month can still get stories.</summary>
        public void CompactCardLedger()
        {
            var c = State.cards;
            var read = new HashSet<int>(c.read);
            long first = State.month;
            foreach (var e in State.chronicle)
                if (e.month < first && CardUnread(e, read)) first = e.month;
            c.since = Math.Max(c.since, first - 1);
            var after = new HashSet<int>(State.chronicle.Where(e => e.month > c.since).Select(e => e.id));
            c.read.RemoveAll(id => !after.Contains(id));
        }

        /// <summary>Everything so far has been seen (the first run told it in its own card).</summary>
        public void CardsSeenToNow()
        {
            State.cards.since = Math.Max(State.cards.since, State.month);
            State.cards.read.Clear();
        }

        /// <summary>A person's card was opened: their stories score +0.05 on the next return (§2.2).</summary>
        public void NotePersonViewed(int notableId)
        {
            if (notableId >= 0 && !State.cards.viewed.Contains(notableId)) State.cards.viewed.Add(notableId);
        }

        /// <summary>The card's footer: "Year 34 · by the well" (§2.1).</summary>
        public string CardFooter(ChronicleCard c)
        {
            if (c == null) return "";
            string year = $"Year {c.month / SimConfig.MonthsPerYear}";
            var b = c.buildingId < 0 ? null : State.buildings.FirstOrDefault(x => x.id == c.buildingId);
            return b == null ? year : $"{year} · by the {Label(b)}";
        }

        /// <summary>Debug: every story is unread again.</summary>
        public void DebugClearCardsRead()
        {
            State.cards.since = -1;
            State.cards.read.Clear();
        }
    }
}
