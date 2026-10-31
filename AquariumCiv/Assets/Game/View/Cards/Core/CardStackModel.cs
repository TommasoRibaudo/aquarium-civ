using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;

namespace AquariumCiv.View.Cards
{
    public enum CardStackMode { Hidden, Peek, Open }

    /// <summary>The one extra button a card can have (§2.1).</summary>
    public enum CardAction { None, Give, Remember, Watch }

    /// <summary>The peek: one line at the bottom (§2.4). Its look is the view's.</summary>
    public struct CardPeek
    {
        public bool visible;
        public string text;         // the top card's headline, or the teaser when there are no cards
        public bool isTeaser;       // nothing new: the line is only a teaser and tapping it opens nothing
        public int notableId;       // the portrait (-1 = none)
        public string teaser;       // the line under the peek (null = none; the era-ready state replaces it)
    }

    /// <summary>Everything one page of the stack shows, as data (§2.1). <see cref="isAlso"/>: the "Also…" page.</summary>
    public sealed class CardFace
    {
        public ChronicleCard card;      // null on the "Also…" page
        public bool isAlso;
        public List<string> alsoLines = new();  // the three tappable headlines
        public int alsoMore;                    // how many more past those
        public string id = "";
        public CardKind kind;
        public CardTone tone;
        public string teaserLine = "";  // small and grey, above the headline
        public string headline = "";
        public string body = "";
        public string endLine = "";     // a teased thread a death ended
        public string quote = "";
        public int quoteBy = -1;        // -1 = the crowd
        public string footer = "";
        public List<int> portraits = new();     // at most two
        public bool keeperMark;         // the small hand mark
        public string relicId = "";     // the relic's picture
        public bool canShowMe;
        public CardAction action;
        public string actionLabel = "";
        public bool actionEnabled = true;
        public string actionNote = "";  // why the action can't happen now, said on the card (practices §4)
    }

    /// <summary>What Give did: the wish to arm the tool for, or why not (<see cref="line"/> is ready to show).</summary>
    public struct GiveOutcome
    {
        public bool ok;
        public SimCore.ShapeFail fail;
        public string line;
        public Wish wish;
    }

    /// <summary>
    /// The chronicle card stack on return, peek included, with no Unity and no look (#75; chronicle-and-history.md §2, §7,
    /// first-session §3). What's read is kept in the save (<see cref="SimCore.UnreadCards"/>), so a kill loses nothing.
    /// <para>The sheet (phase 2) drives it like this:</para>
    /// <code>
    /// // on every open and resume, after the catch-up. Under 15 minutes or in the first run nothing happens.
    /// model.Prepare(awayHours);
    /// // Peek: draw model.Peek. Tap → TapPeek(); swipe down or the first touch elsewhere → HidePeek().
    /// // Open: draw model.Face, model.Index of model.PageCount, hide the tools.
    /// //   Next/swipe → Next(), back → Back(), Skip all → SkipAll(), back gesture → Close().
    /// //   Show me: var card = ShowMe(); ShowMeDirector.Instance.Show(card); when the flight lands, ShowMeLanded().
    /// //   Portrait: OpenPerson(id), then PersonClosed(). Also…: OpenAlso(i), Back().
    /// //   Give() (then ShapingView.ArmWish), Remember() (then PersonClosed()), Watch().
    /// // on Changed redraw; on ShowMeEnd call ShowMeDirector.Instance.End(true).
    /// // the history button's dot: HistoryDot. Pause: OnPause().
    /// </code>
    /// </summary>
    public sealed class CardStackModel
    {
        /// <summary>Under this away there's no peek and no cards (P2's threshold, §2.4).</summary>
        public const double MinAwayHours = 0.25;
        /// <summary>Skip all or Close on a card that was up this long counts it as read [est]; shorter, it waits.</summary>
        public const double MinReadSeconds = 1.5;
        public const string AlsoId = "also";

        readonly SimCore sim;
        readonly TelemetryLog log;
        readonly Func<double> now;
        readonly ShowMeCore showMe;
        readonly DwellTracker dwell = new DwellTracker();
        readonly HashSet<string> shown = new HashSet<string>();
        ReturnStack stack;
        List<int> viewed = new List<int>();
        int cap = 3;
        bool flying, showMeUsed;

        public CardStackMode Mode { get; private set; }
        public CardPeek Peek { get; private set; }
        /// <summary>A dot, never a number: stories wait to be read.</summary>
        public bool HistoryDot { get; private set; }
        public int Index { get; private set; }
        /// <summary>A card opened from "Also…" (null = none): it covers that page until <see cref="Back"/>.</summary>
        public ChronicleCard Detail { get; private set; }

        /// <summary>Anything on screen changed.</summary>
        public event Action Changed;
        /// <summary>The stack closed after a Show me: the echo ends and the camera goes back (§2.5).</summary>
        public event Action ShowMeEnd;

        /// <param name="clock">Real seconds, for the dwell time.</param>
        public CardStackModel(SimCore sim, Func<double> clock, TelemetryLog log = null)
        {
            this.sim = sim ?? throw new ArgumentNullException(nameof(sim));
            now = clock ?? throw new ArgumentNullException(nameof(clock));
            this.log = log;
            showMe = new ShowMeCore(sim);
            HistoryDot = sim.HasUnreadCards;
        }

        // ------------------------------------------------------------------ the return

        /// <summary>Cards can't come now: the first run tells its own card (first-session §2), a finished box is a museum.</summary>
        bool Quiet => sim.State.firstRun.active || sim.State.boxComplete;

        public int CardCount => stack?.cards.Count ?? 0;
        public int AlsoCount => stack?.also.Count ?? 0;
        /// <summary>The page dots: the cards, then "Also…" if anything overflowed.</summary>
        public int PageCount => CardCount + (AlsoCount > 0 ? 1 : 0);
        public bool OnAlsoPage => Mode == CardStackMode.Open && Detail == null && Index == CardCount;
        public bool CanGoBack => Mode == CardStackMode.Open && (Detail != null || Index > 0);
        public bool OnLastPage => Mode == CardStackMode.Open && Detail == null && Index == PageCount - 1;

        /// <summary>The app opened or came back, <paramref name="awayHours"/> after leaving. True when a stack is due: the peek
        /// is up with the top card. False under <see cref="MinAwayHours"/>, in the first run, or with nothing to tell (then the
        /// peek may still show the teaser).</summary>
        public bool Prepare(double awayHours)
        {
            if (awayHours < MinAwayHours)
            {
                if (Mode == CardStackMode.Open && dwell.Current == null) dwell.Show(CurrentId, now());
                return false;
            }
            if (Mode == CardStackMode.Open) End();
            Mode = CardStackMode.Hidden;
            Peek = default;
            stack = null;
            if (Quiet) { Changed?.Invoke(); return false; }
            viewed = sim.State.cards.viewed.ToList();
            sim.State.cards.viewed.Clear();
            cap = SimCore.CardCap(awayHours);
            return ShowPeek();
        }

        /// <summary>Debug: a return of up to <paramref name="cards"/> cards, whatever the time away.</summary>
        public bool DebugPrepare(int cards)
        {
            if (Mode == CardStackMode.Open) End();
            cap = Math.Max(1, cards);
            return ShowPeek();
        }

        bool ShowPeek()
        {
            Build();
            var teaser = sim.State.eraReady ? null : sim.CurrentTeaser;
            if (CardCount > 0)
            {
                var top = stack.cards[0];
                Peek = new CardPeek { visible = true, text = top.headline, notableId = top.people.Count > 0 ? top.people[0] : -1, teaser = teaser };
            }
            else Peek = new CardPeek { visible = teaser != null, text = teaser ?? "", isTeaser = true, notableId = -1 };
            Mode = Peek.visible ? CardStackMode.Peek : CardStackMode.Hidden;
            Changed?.Invoke();
            return CardCount > 0;
        }

        void Build()
        {
            stack = sim.UnreadCardsCapped(cap, viewed);
            HistoryDot = sim.HasUnreadCards;
        }

        /// <summary>Tap on the peek: the full stack. A teaser peek has nothing behind it.</summary>
        public bool TapPeek()
        {
            if (Mode != CardStackMode.Peek || CardCount == 0) return false;
            log.PeekTap(IdOf(stack.cards[0]));
            OpenStack();
            return true;
        }

        /// <summary>Swipe down, or the first touch elsewhere: the peek goes, the cards wait (the dot says so).</summary>
        public void HidePeek()
        {
            if (Mode != CardStackMode.Peek) return;
            Mode = CardStackMode.Hidden;
            Peek = default;
            HistoryDot = sim.HasUnreadCards;
            Changed?.Invoke();
        }

        /// <summary>Open what's waiting in this session (the dot, or back from the era moment). False with nothing to show.</summary>
        public bool Reopen()
        {
            if (Mode == CardStackMode.Open || Quiet) return false;
            Build();
            if (CardCount == 0) return false;
            OpenStack();
            return true;
        }

        void OpenStack()
        {
            Mode = CardStackMode.Open;
            Peek = default;
            Index = 0;
            Detail = null;
            shown.Clear();
            ShowCurrent();
        }

        // ------------------------------------------------------------------ paging

        /// <summary>The card on screen (null on the "Also…" page and when closed).</summary>
        public ChronicleCard Shown => Mode != CardStackMode.Open ? null : Detail ?? (Index < CardCount ? stack.cards[Index] : null);

        string CurrentId => Shown != null ? IdOf(Shown) : AlsoId;

        /// <summary>The entry's id, as ShowMeDirector logs it, so a card's events join up.</summary>
        static string IdOf(ChronicleCard c) => c.lead.id.ToString();

        void ShowCurrent()
        {
            flying = false;
            dwell.EndAndLog(log, now());
            dwell.Show(CurrentId, now());
            if (shown.Add(CurrentId)) log.CardShown(CurrentId, Shown?.lead.kind ?? AlsoId, Index, PageCount);
            Changed?.Invoke();
        }

        /// <summary>Marks the page read. Passing "Also…" retires everything in it, or the overflow would pile up forever.</summary>
        void MarkPage()
        {
            if (Shown != null) sim.MarkCardRead(Shown);
            else if (stack != null) foreach (var c in stack.also) sim.MarkCardRead(c);
        }

        /// <summary>Next or a swipe: this page is read; past the last one the stack closes. False if it closed.</summary>
        public bool Next()
        {
            if (Mode != CardStackMode.Open) return false;
            MarkPage();
            if (Detail != null) { Detail = null; ShowCurrent(); return true; }
            if (Index >= PageCount - 1) { End(); return false; }
            Index++;
            ShowCurrent();
            return true;
        }

        /// <summary>Back a page, or out of a card opened from "Also…". Going back unreads nothing.</summary>
        public bool Back()
        {
            if (!CanGoBack) return false;
            if (Detail != null) Detail = null;
            else Index--;
            ShowCurrent();
            return true;
        }

        /// <summary>Tap a line of the "Also…" card: its full card.</summary>
        public bool OpenAlso(int line)
        {
            if (!OnAlsoPage || line < 0 || line >= Math.Min(3, AlsoCount)) return false;
            Detail = stack.also[line];
            sim.MarkCardRead(Detail);
            ShowCurrent();
            return true;
        }

        /// <summary>Skip all: what's unread waits for the next open (the dot says so).</summary>
        public void SkipAll()
        {
            if (Mode != CardStackMode.Open) return;
            LeaveEarly();
            log.SkipAll(stack.cards.Concat(stack.also).Count(c => !sim.IsCardRead(c)));
            End();
        }

        /// <summary>The back gesture: as Skip all, but it isn't one.</summary>
        public void Close()
        {
            if (Mode != CardStackMode.Open) return;
            LeaveEarly();
            End();
        }

        void LeaveEarly()
        {
            var r = dwell.End(now());
            if (!r.HasValue) return;
            log.CardRead(r.Value.card, r.Value.seconds);
            if (r.Value.seconds >= MinReadSeconds) MarkPage();
        }

        void End()
        {
            dwell.EndAndLog(log, now());
            Mode = CardStackMode.Hidden;
            Detail = null;
            flying = false;
            sim.CompactCardLedger();
            HistoryDot = sim.HasUnreadCards;
            if (showMeUsed) { showMeUsed = false; ShowMeEnd?.Invoke(); }
            Changed?.Invoke();
        }

        /// <summary>The app is leaving: the read time stops. <see cref="Prepare"/> picks it up again on a short absence.</summary>
        public void OnPause()
        {
            if (Mode == CardStackMode.Open) dwell.EndAndLog(log, now());
        }

        // ------------------------------------------------------------------ the buttons

        /// <summary>Show me: returns the card to pass to ShowMeDirector.Show, or null. The stack stays open behind it and the
        /// read time stops during the flight; call <see cref="ShowMeLanded"/> when it lands.</summary>
        public ChronicleCard ShowMe()
        {
            var card = Shown;
            if (card == null || !CanShowMe(card)) return null;
            sim.MarkCardRead(card);
            showMeUsed = true; // ShowMeDirector logs show_me and card_flown_to, for every Show me
            if (!flying) { flying = true; dwell.Cover(now()); }
            return card;
        }

        public void ShowMeLanded()
        {
            if (!flying) return;
            flying = false;
            dwell.Uncover(now());
        }

        bool CanShowMe(ChronicleCard c) => showMe.Target(c.lead).hasPoint;

        /// <summary>A portrait was tapped: the view opens the person card over this card.</summary>
        public void OpenPerson(int notableId)
        {
            if (Mode != CardStackMode.Open || Shown == null) return;
            log.CardOpened(IdOf(Shown), notableId);
            sim.NotePersonViewed(notableId);
            dwell.Cover(now());
        }

        public void PersonClosed() { if (Mode == CardStackMode.Open) dwell.Uncover(now()); }

        /// <summary>The wish card's Give: the stack closes and the view arms the tool at the wish's spot (ShapingTray.ArmForWish);
        /// the wish stays open until the player places it. If it can't be given the stack stays and <see cref="GiveOutcome.line"/>
        /// says why.</summary>
        public GiveOutcome Give()
        {
            var card = Shown;
            var wish = card?.kind == CardKind.Wish ? sim.WishOfEntry(card.lead.id) : null;
            if (wish == null) return new GiveOutcome { fail = SimCore.ShapeFail.Unknown, line = "" };
            var fail = sim.CanGiveWish(wish.id);
            var o = new GiveOutcome { ok = fail == SimCore.ShapeFail.None, fail = fail, wish = wish, line = FailLine(wish, fail) };
            if (o.ok) { sim.MarkCardRead(card); End(); }
            else Changed?.Invoke();
            return o;
        }

        string FailLine(Wish w, SimCore.ShapeFail f) =>
            f == SimCore.ShapeFail.None ? "" : w.state != WishState.Open ? "That wish has passed." : sim.ShapeFailLine(w.kind, f);

        /// <summary>Remember on a legend's card: returns the person (their book page, or their card until the book exists; -1 =
        /// not a legend's card). Call <see cref="PersonClosed"/> afterwards.</summary>
        public int Remember()
        {
            var card = Shown;
            if (card?.kind != CardKind.Legend || card.people.Count == 0) return -1;
            sim.MarkCardRead(card);
            log.RememberTapped(card.people[0]);
            dwell.Cover(now());
            return card.people[0];
        }

        /// <summary>Watch on the era card: the stack closes for the moment; <see cref="Reopen"/> brings the rest back after it.</summary>
        public bool Watch()
        {
            if (Shown?.kind != CardKind.EraReady) return false;
            sim.MarkCardRead(Shown);
            End();
            return true;
        }

        // ------------------------------------------------------------------ what to draw

        /// <summary>The page on screen as data (null when closed).</summary>
        public CardFace Face
        {
            get
            {
                if (Mode != CardStackMode.Open) return null;
                var c = Shown;
                if (c == null) return new CardFace { isAlso = true, id = AlsoId, headline = "Also…", alsoLines = stack.alsoHeadlines.ToList(), alsoMore = stack.alsoMore };
                var f = new CardFace
                {
                    card = c, id = IdOf(c), kind = c.kind, tone = c.tone, headline = c.headline, body = BodyOf(c),
                    teaserLine = string.IsNullOrEmpty(c.teaser) ? "" : "At the fire: " + c.teaser, endLine = c.endLine ?? "",
                    quote = c.lead.quote ?? "", quoteBy = c.lead.speakerId, footer = sim.CardFooter(c),
                    portraits = c.people.Take(2).ToList(), keeperMark = c.kind == CardKind.Keeper, relicId = c.lead.relicId ?? "",
                    canShowMe = CanShowMe(c),
                };
                switch (c.kind)
                {
                    case CardKind.Legend: f.action = CardAction.Remember; f.actionLabel = "Remember"; break;
                    case CardKind.EraReady: f.action = CardAction.Watch; f.actionLabel = "Watch"; break;
                    case CardKind.Wish: FillGive(f, sim.WishOfEntry(c.lead.id)); break;
                }
                return f;
            }
        }

        /// <summary>The lead's text, without saying the headline twice: most stories' headline is their first sentence.</summary>
        static string BodyOf(ChronicleCard c)
        {
            string text = c.lead.text ?? "", head = c.headline ?? "";
            if (head.Length == 0 || !text.StartsWith(head, StringComparison.Ordinal)) return text;
            return text.Substring(head.Length).TrimStart('.', ':', ';', '!', ' ');
        }

        void FillGive(CardFace f, Wish w)
        {
            if (w == null || w.state == WishState.Given) return; // given: the button is gone
            f.action = CardAction.Give;
            f.actionLabel = "Give";
            var fail = sim.CanGiveWish(w.id);
            f.actionEnabled = fail == SimCore.ShapeFail.None;
            f.actionNote = FailLine(w, fail);
        }
    }
}
