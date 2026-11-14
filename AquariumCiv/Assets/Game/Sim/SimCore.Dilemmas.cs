using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    public enum DilemmaState { Open, Answered, Lapsed }

    /// <summary>How a dilemma ended (#368). Answered and LetThemDecide are the Keeper's (credited); Visits and Gone are unanswered closes
    /// (the people took their default); Debug is a cheat.</summary>
    public enum DilemmaEnd { None, Answered, LetThemDecide, Visits, Gone, Debug }

    /// <summary>#368: a big choice (the dilemmas of DilemmaDefs, every ~6-10 h) or a small one between (C5 adds them: naming a newborn).
    /// The scheduler asks <see cref="SimCore.NextDilemmaSize"/>; today only Big is ever picked.</summary>
    public enum DilemmaSize { Big, Small }

    /// <summary>Where the player answered: by touching one of the two people or places in the box, or on the card's two buttons.</summary>
    public enum DilemmaPath { Box, Card }

    /// <summary>One dilemma raised in a box (P7, #220): which def, when, who it points at, how it ended. Saved with the box.</summary>
    [Serializable]
    public class Dilemma
    {
        public int id;
        public string defId = "";
        public long month;                  // raised
        public long endMonth = -1;          // answered or lapsed (-1 = open)
        public DilemmaState state;
        public int option = -1;             // the option chosen (answered) or picked by the people (lapsed); -1 while open
        public DilemmaPath path;            // how it was answered (meaningful when state is Answered)
        public int personA = -1, personB = -1;       // the two people the options point at (-1 = none)
        public int askerId = -1;            // who put the question (the era's leader when raised, or the eldest other adult; -1 = the people at large): voices it by trait
        public int buildingA = -1, buildingB = -1;   // the place each option points at (-1 = none)
        public int entryId = -1;            // its chronicle entry (kind "dilemma"): the card
        public int answerEntryId = -1;      // the Keeper line an answer wrote (kind "keeper"); -1 for a lapse
        public int resultEntryId = -1;      // the line a lapse wrote (kind "dilemma_result": a story card, not the question)
        // #368: visits and the end. An old save has these at their defaults (an old open dilemma counts as shown once).
        public DilemmaSize size;            // big or small
        public DilemmaEnd end;              // how it ended (None while open)
        public int visits = 1;              // visits it has been shown on (the raise counts as the first)
        public int visitNo;                 // the SimState.visitCount of the last visit that counted
        public double raisedHours;          // the SimState.clockHours it was raised at (real hours)
        public int subjectId = -1;          // #372: a small choice's subject (the newborn being named); -1 = none
        public string land = "";            // #578: where the words put it ("woods", "meadow" or "pond"; "" = the woods), fixed when it is raised
        public string nameA = "", nameB = ""; // #372: a small choice's two offered words ({n1}, {n2}); empty = none
        public int deadId = -1;             // #635: a choice about the lost names the latest death when it was raised ({dead}); -1 = none

        /// <summary>The person option <paramref name="i"/> points at (-1 = none).</summary>
        public int PersonOf(int i) => i == 0 ? personA : i == 1 ? personB : -1;
        public int BuildingOf(int i) => i == 0 ? buildingA : i == 1 ? buildingB : -1;
    }

    /// <summary>A lean an answer left on the box, like a nudge's (SimCore.Nudges.cs) but without a shaping behind it.</summary>
    [Serializable]
    public class DilemmaLean
    {
        public int lean;                    // (int)SimCore.Lean
        public long fromMonth, untilMonth;
        public int dilemmaId;
    }

    /// <summary>What the view reads for one option: the words and the person or place it points at.</summary>
    public sealed class DilemmaOptionView
    {
        public int index;
        public string label, hint;
        public int personId = -1;           // the notable it points at (-1 = none)
        public int buildingId = -1;         // the building it points at (-1 = none)
        public string buildingType = "";
        public string icon = "";            // t4-fixes: the option's own mark for its tag ("" = none)
    }

    /// <summary>What the view reads for a dilemma: kind, the question, and its two options (#223 card, #224 choice scene).</summary>
    public sealed class DilemmaView
    {
        public Dilemma dilemma;
        public DilemmaDef def;
        public DilemmaKind kind;
        public string question;
        public DilemmaOptionView[] options;
    }

    /// <summary>
    /// The Keeper's dilemmas (P7, #220; docs/design/results/P7.md, research 14 section 2). The people put two proposals to the Keeper:
    /// a card (chronicle kind "dilemma", <see cref="CardKind.Dilemma"/>) raised from a <see cref="DilemmaDef"/>, answered with
    /// <see cref="AnswerDilemma"/> or left to lapse after <see cref="DilemmaLapseMonths"/>, when the people decide. It copies the
    /// wish: open, then answered or lapsed; one open at a time, sharing the wish slot, a gap after each. Everything is opt-in on
    /// <see cref="SimConfig.dilemmas"/> (off: <see cref="DilemmaStep"/> returns at once, no state changes, no dice). With it on,
    /// no dice stream is drawn either (hashes of the seed, ids and month), so the economy's dice never shift.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The gap after a wish ends before a dilemma is raised (game months). Wishes only: a choice's own gap is in hours.</summary>
        public const int DilemmaGapMonths = 8;
        /// <summary>#368 (visit-loop.md 2.4): a choice is never closed by the clock. It closes on an answer, "Let them decide", being shown
        /// on this many visits, or its people being gone.</summary>
        public const int DilemmaMaxVisits = 3;
        /// <summary>#368: a new choice needs this many real hours (watched or away) since the last one was raised [est].</summary>
        public const double DilemmaGapHours = 3.0;
        /// <summary>#368: an open counts as a new visit only after this long away (real hours, 15 minutes), so flicking back to the app
        /// from another one does not burn the question's three visits.</summary>
        public const double DilemmaVisitAwayHours = 0.25;
        /// <summary>The clock is a sum of floats: hour comparisons forgive this much (well under a game month).</summary>
        const double ClockEps = 1e-7;
        /// <summary>The gap in this box's clock: the lab (x60) keeps the old 8 game months.</summary>
        public double DilemmaGapNow => Config.dilemmaLab ? DilemmaGapMonths * Config.StepHours : DilemmaGapHours;
        /// <summary>Dilemma entries take their own ids (above the wishes'), so the shared ids never shift.</summary>
        public const int DilemmaIdBase = 1_200_000_000;

        /// <summary>The defs the sim draws from. Tests (and later the content) can swap it; default <see cref="DilemmaDefs.All"/>.</summary>
        public IReadOnlyList<DilemmaDef> DilemmaCatalog { get; set; } = DilemmaDefs.All;

        public Dilemma FindDilemma(int id) => State.dilemmas.FirstOrDefault(d => d.id == id);
        public Dilemma DilemmaOfEntry(int entryId) => State.dilemmas.FirstOrDefault(d => d.entryId == entryId);
        public DilemmaDef DilemmaDefOf(Dilemma d) => d == null ? null : DilemmaCatalog.FirstOrDefault(x => x.id == d.defId) ?? SmallCatalog.FirstOrDefault(x => x.id == d.defId)
            ?? LifeChoiceCatalog?.FirstOrDefault(x => x.id == d.defId); // #437/#438: the wolves' and the dogs' choices

        /// <summary>The dilemma that is open now (at most one), or null.</summary>
        public Dilemma OpenDilemma => State.dilemmas.FirstOrDefault(d => d.state == DilemmaState.Open);

        /// <summary>A dilemma entry is a card only while its dilemma is open.</summary>
        bool DilemmaCardLive(ChronicleEntry e)
        {
            var d = DilemmaOfEntry(e.id);
            return d == null || d.state == DilemmaState.Open;
        }

        /// <summary>A save from before dilemmas has none of these fields: they start empty (the same as a new world).</summary>
        void EnsureDilemmaState()
        {
            var s = State;
            s.dilemmas ??= new List<Dilemma>();
            s.dilemmaLeans ??= new List<DilemmaLean>();
            s.dilemmaCallbacks ??= new List<DilemmaCallback>();
            s.customs ??= new List<string>();
            s.apartGroups ??= new List<ApartGroup>();
            s.consequenceProps ??= new List<ConsequenceProp>();
            if (s.dilemmaNextId < 1) s.dilemmaNextId = 1;
        }

        /// <summary>True when a custom set by an answer is on the box ("burial:stone").</summary>
        public bool HasCustom(string key) => State.customs.Contains(key);

        bool DilemmaLeaning(Lean l)
        {
            var ls = State.dilemmaLeans;
            if (ls.Count == 0) return false;
            foreach (var x in ls) if (x.lean == (int)l && State.month < x.untilMonth) return true;
            return false;
        }

        // ---------- hashes (no dice) ----------
        float DilemmaRoll(int salt, int id, long month)
        {
            uint h = (uint)State.seed * 0x9E3779B1u ^ (uint)id * 0x85EBCA77u ^ (uint)month * 0xC2B2AE3Du ^ (uint)salt * 0x27D4EB2Fu ^ 0x51ED270Bu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        // ---------- pacing ----------
        long DilemmaLastEnd => State.dilemmas.Count == 0 ? long.MinValue / 2 : State.dilemmas.Max(d => d.endMonth >= 0 ? d.endMonth : d.month);

        /// <summary>The next big choice is due from this clock hour (see <see cref="DilemmaScheduleFrom"/>); -1 = not scheduled.</summary>
        bool DilemmaDue => Config.dilemmas && !State.firstRun.active && (State.dilemmaNextHours >= 0 && State.clockHours + ClockEps >= State.dilemmaNextHours && EligibleDefs().Count > 0 || SmallDue || SeasonalLifeWaiting);

        /// <summary>#372: a small choice is next, its moment exists and the gap has passed: like a due big one it holds the slot (no new wish starts).
        /// t4-fixes: with no small kind eligible, the next eligible big fills the small slot (see <see cref="TryRaiseDilemma"/>).</summary>
        bool SmallDue => Config.smallChoices && (State.dilemmas.Count > 0 && NextDilemmaSize() == DilemmaSize.Small || LifeChoiceWaiting) // #437: a waiting life choice takes the small slot
            && HoursSinceDilemmaRaised + ClockEps >= DilemmaGapNow && (EligibleSmall().Count > 0 || EligibleDefs(DilemmaSize.Big).Count > 0);

        /// <summary>While a dilemma is open or due, no new wish starts (the slot is shared).</summary>
        bool DilemmaHoldsSlot => Config.dilemmas && (OpenDilemma != null || DilemmaDue);

        /// <summary>#368: real hours since the last choice was raised (a very large number when none ever was).</summary>
        public double HoursSinceDilemmaRaised => State.dilemmaLastRaiseHours < -1e8 ? 1e9 : State.clockHours - State.dilemmaLastRaiseHours;

        /// <summary>#368/#372: the next choice's size. A big choice that is due always comes first; otherwise a small one follows a big
        /// one, and a big follows a small one (they alternate). Small ones share the gap and the one-at-a-time rule; with none eligible
        /// (nothing is invented) the next eligible big takes the small slot after the gap (t4-fixes, in <see cref="TryRaiseDilemma"/>).</summary>
        public DilemmaSize NextDilemmaSize()
        {
            var s = State;
            if (!Config.smallChoices || SmallCatalog.Count == 0) return DilemmaSize.Big;
            if (s.dilemmaNextHours >= 0 && s.clockHours + ClockEps >= s.dilemmaNextHours) return DilemmaSize.Big;
            var last = s.dilemmas.Count == 0 ? null : s.dilemmas[s.dilemmas.Count - 1];
            return last != null && last.size == DilemmaSize.Big ? DilemmaSize.Small : DilemmaSize.Big;
        }

        /// <summary>The clock hour the next big choice is due, counted from <paramref name="from"/> (real hours). Normal play: 6 to 10 game
        /// years on (hours at the box's pace); the lab: the first at year 2, then about every 3 years (plus or minus 6 months).</summary>
        double DilemmaScheduleFrom(double from, bool first)
        {
            var c = Config;
            double month = c.StepHours;
            if (c.dilemmaLab)
            {
                if (first) return Math.Max(from, c.dilemmaLabFirstYear * SimConfig.MonthsPerYear * month);
                int jitter = (int)(DilemmaRoll(21, State.dilemmaNextId, (long)Math.Round(from / month)) * 13f) - 6;
                return from + (c.dilemmaLabEveryYears * SimConfig.MonthsPerYear + jitter) * month;
            }
            int lo = c.dilemmaMinYears * SimConfig.MonthsPerYear, hi = Math.Max(lo, c.dilemmaMaxYears * SimConfig.MonthsPerYear);
            return from + (lo + (int)(DilemmaRoll(20, State.dilemmaNextId, (long)Math.Round(from / month)) * (hi - lo + 1))) * month;
        }

        /// <summary>Every month: close a choice whose people are gone, and raise the next when it is due and the player is watching.
        /// Nothing lapses by the clock (#368). No dice. Nothing at all when off.</summary>
        void DilemmaStep()
        {
            ConsequencePropStep(); // #371: a choice's thing waiting for its spot is placed (within a game year)
            ApartStep(); // the people who live apart age even if the switch is turned off later (nothing when there are none)
            if (!Config.dilemmas) return;
            CallbackStep(); // no dice: one due callback per month
            var open = OpenDilemma;
            if (open != null)
            {
                if (CastGone(open)) LapseDilemma(open, DilemmaEnd.Gone);
                return;
            }
            TryRaiseDilemma();
        }

        /// <summary>#368: raise the next choice if it is due, the gap has passed and the player is watching. During an offline catch-up
        /// it never raises: the choice is held (still due) and <see cref="BeginVisit"/> raises it on the next open. True if one was raised.</summary>
        bool TryRaiseDilemma()
        {
            var s = State;
            if (!Config.dilemmas || OpenDilemma != null) return false;
            if (s.firstRun.active) { s.dilemmaNextHours = -1; return false; } // none during the first run; the clock restarts after it
            if (s.dilemmaNextHours < 0) s.dilemmaNextHours = DilemmaScheduleFrom(s.clockHours, s.dilemmas.Count == 0);
            var size = NextDilemmaSize();
            if (CatchingUp) return false;
            if (TryRaiseSeasonalLife()) return true; // user 2026-11-08: the thief and the pup don't wait for the gap
            if (HoursSinceDilemmaRaised + ClockEps < DilemmaGapNow) return false;
            // t4-fixes (tester run 3): the small slot is open but no small kind is there (no newborn, the everyday ones asked): the next
            // eligible big comes after the 3 h gap instead of waiting out its 6-10 h window, so a return still finds a choice.
            if (size == DilemmaSize.Small && EligibleSmall().Count == 0) size = DilemmaSize.Big;
            else if (size == DilemmaSize.Big && s.clockHours + ClockEps < s.dilemmaNextHours)
            {
                if (!LifeChoiceWaiting) return false;
                size = DilemmaSize.Small; // #437/#438: the wolves' and the dogs' moments don't wait out a big choice's 6-10 h window
            }
            // A due dilemma never ends a wish: it waits for the open one to end (new wishes already wait for it), then the 8-month gap
            // runs from that end.
            if (OpenWish != null) return false;
            long lastWish = s.wishes.Count == 0 ? long.MinValue / 2 : s.wishes.Max(w => w.endMonth >= 0 ? w.endMonth : w.month);
            if (s.month - lastWish < DilemmaGapMonths) return false;
            var pick = PickDilemmaDef(size, out int a, out int b);
            if (pick == null && size == DilemmaSize.Big && LifeChoiceWaiting) pick = PickDilemmaDef(DilemmaSize.Small, out a, out b); // #437: no big one holds
            if (pick == null) return false;
            var raised = RaiseDilemma(pick, a, b);
            choiceStageFor = raised.id; // t3-choice (tester run 2 D): a choice raised while the player watches is staged in the box too
            return true;
        }

        /// <summary>User 2026-11-08: a seasonal life choice (the meat thief, the lost pup) is raised as soon as it happens, without the
        /// small-choice gap, the next small slot or the wishes' gap; still never with another choice or a wish open (it waits its turn),
        /// and only from <see cref="TryRaiseDilemma"/> (the Keeper watches, or an open). True if one was raised.</summary>
        bool TryRaiseSeasonalLife()
        {
            if (!Config.smallChoices || OpenDilemma != null || OpenWish != null) return false;
            var def = SeasonalLifeDef();
            if (def == null) return false;
            var raised = RaiseDilemma(def, -1, -1);
            choiceStageFor = raised.id;
            return true;
        }

        /// <summary>
        /// #368: the app came to the front (a session start, or an open after being away). Counts a visit (an open after at least
        /// <see cref="DilemmaVisitAwayHours"/> away, or the first), counts it against the open choice (shown on <see cref="DilemmaMaxVisits"/>
        /// visits, then the people decide it, recorded as unanswered), and raises a held choice if it is due and the gap has passed.
        /// Call it after <see cref="CatchUp"/>. No dice. True if a choice is open afterwards.
        /// </summary>
        public bool BeginVisit()
        {
            var s = State;
            if (!Config.dilemmas) return false; // off: no state changes at all
            EnsureDilemmaState();
            bool newVisit = s.visitCount == 0 || awayHours >= DilemmaVisitAwayHours - 1e-9;
            if (newVisit) s.visitCount++;
            var open = OpenDilemma;
            if (open != null && open.visitNo != s.visitCount)
            {
                if (open.visits >= DilemmaMaxVisits) LapseDilemma(open, DilemmaEnd.Visits); // shown on three visits, never answered
                else { open.visits++; open.visitNo = s.visitCount; }
            }
            if (OpenDilemma == null) TryRaiseDilemma();
            awayHours = 0;
            choiceStageFor = OpenDilemma?.id ?? -1; // visit-loop 2.4: a choice waiting on the open is staged in the box first
            return OpenDilemma != null;
        }

        // Not saved: a load is an open, and BeginVisit sets it again.
        int choiceStageFor = -1;

        /// <summary>Visit-loop 2.4: this open found a choice waiting (held while away, or still open from an earlier visit), or one was
        /// just raised while the player watches (t3-choice), and the view has not yet staged it in the box. The view stages it once the welcome moment is over, then calls <see cref="ChoiceStaged"/>.
        /// False once staged, answered, lapsed or when none was open on the open.</summary>
        public bool ChoiceStageDue => choiceStageFor >= 0 && OpenDilemma?.id == choiceStageFor;

        /// <summary>The view staged the waiting choice (or gave up): it is not due again until the next open.</summary>
        public void ChoiceStaged() => choiceStageFor = -1;

        /// <summary>Debug: the open choice counts as held for this open (it is staged again).</summary>
        public void DebugHoldChoiceForOpen() => choiceStageFor = OpenDilemma?.id ?? -1;

        /// <summary>Visit-loop 2.4: a moment (the welcome) is playing or still due on this open, so the held choice waits for it:
        /// the welcome moment first, then the staged choice. Starts the visit's bookkeeping like the director does.</summary>
        public bool MomentBeforeChoice()
        {
            if (!Config.moments || CatchingUp || State.firstRun.active) return false;
            if (Moments.Any(m => m.state == HeldMomentState.Playing)) return true;
            EnsureMomentVisit();
            return MomentPacing.welcomeDue && Moments.Any(m => m.state == HeldMomentState.Waiting);
        }

        /// <summary>A person the options point at died: the people decide at once (the same as a wish whose wisher dies).</summary>
        bool CastGone(Dilemma d)
        {
            var def = DilemmaDefOf(d);
            if (def == null) return false;
            if (def.smallKind != null && d.subjectId >= 0 && Alive(d.subjectId) == null) return true; // the baby is gone
            if (def.cast != DilemmaCast.TwoCandidates) return false;
            return Alive(d.personA) == null || Alive(d.personB) == null;
        }

        // ---------- conditions and the cast ----------
        string RoleOfDef(DilemmaDef def) => def.role == DilemmaDef.Leader ? LeaderOf(State.era) : def.role;

        /// <summary>Does the def hold now? On success the two people (or -1).</summary>
        bool DefHolds(DilemmaDef def, out int a, out int b) => DefWhyNot(def, out a, out b) == null;

        /// <summary>Debug (#225): why this def can't be raised in this box now, in a few words; null when it can. Read-only.</summary>
        public string DilemmaWhyNot(string defId)
        {
            var def = DilemmaCatalog.FirstOrDefault(x => x.id == defId);
            return def == null ? "no such dilemma" : DefWhyNot(def, out _, out _);
        }

        /// <summary><see cref="DefHolds"/> with the reason it fails (null = it holds). Constant strings: it runs monthly, so no garbage.</summary>
        string DefWhyNot(DilemmaDef def, out int a, out int b)
        {
            a = b = -1;
            if (State.dilemmas.Any(d => d.defId == def.id)) return "asked already in this box";  // never repeats in a box
            if (State.era < def.minEra || State.era > def.maxEra) return "not in this era";
            if (!string.IsNullOrEmpty(def.needs) && !Has(def.needs)) return "a building it needs is missing";
            if (def.needsDeath && !AnyoneHasDied) return NobodyDiedWhy;
            if (def.leaderElder || def.termEndingMonths > 0)
            {
                var lseat = SeatOf(LeaderOf(State.era));
                var leader = Alive(lseat?.holderId ?? -1);
                if (leader == null) return "no leader";
                if (def.leaderElder && leader.age < Config.elderAge) return "the leader isn't old yet";
                if (def.termEndingMonths > 0 && (lseat.termEndsMonth < State.month || lseat.termEndsMonth - State.month > def.termEndingMonths)) return "the leader's term isn't ending";
            }
            string role = RoleOfDef(def);
            if (!string.IsNullOrEmpty(role))
            {
                var seat = SeatOf(role);
                if (seat == null) return "no seat for its role";
                if (seat.holderId >= 0 && !def.replacesHolder) return "its role's seat is filled";
            }
            if (def.cast == DilemmaCast.TwoCandidates)
            {
                var pool = RoleCandidates(role);
                if (pool.Count < 2) return "fewer than two candidates";
                var ranked = string.IsNullOrEmpty(role)
                    ? pool.OrderByDescending(n => DilemmaRoll(5, n.id, 0)).ThenBy(n => n.id).ToList()
                    : pool.OrderByDescending(n => Standing(n, role)).ThenBy(n => n.id).ToList();
                a = ranked[0].id; b = ranked[1].id;
            }
            return null;
        }

        public const string NobodyDiedWhy = "nobody has died yet";

        /// <summary>#346: true once any person of this box has died (a notable, in the camp or apart). Words about the lost wait for this.</summary>
        public bool AnyoneHasDied
        {
            get { foreach (var n in State.notables) if (!n.alive) return true; return false; }
        }

        List<(DilemmaDef def, int a, int b)> EligibleDefs(DilemmaSize? size = null)
        {
            var list = new List<(DilemmaDef def, int a, int b)>();
            foreach (var def in DilemmaCatalog)
                if ((size == null || def.size == size) && DefHolds(def, out int a, out int b)) list.Add((def, a, b));
            return list;
        }

        DilemmaDef PickDilemmaDef(DilemmaSize size, out int a, out int b)
        {
            a = b = -1;
            var ok = size == DilemmaSize.Small ? EligibleSmall() : EligibleDefs(size);
            if (ok.Count == 0) return null;
            var pick = ok[(int)(DilemmaRoll(22, State.dilemmaNextId, State.month) * ok.Count) % ok.Count];
            a = pick.a; b = pick.b;
            return pick.def;
        }

        // ---------- words ----------
        string DilemmaText(string t, Dilemma d)
        {
            if (string.IsNullOrEmpty(t)) return "";
            if (t.IndexOf('{') < 0) return t;
            if (d.subjectId >= 0 || !string.IsNullOrEmpty(d.nameA)) t = SmallText(t, d);
            return t.Replace("{a}", FindNotable(d.personA)?.name ?? "someone").Replace("{b}", FindNotable(d.personB)?.name ?? "someone")
                    .Replace("{place}", DilemmaPlace).Replace("{land}", LandWords(d)).Replace("{camp}", CampWords(d))
                    .Replace("{dead}", FindNotable(d.deadId)?.name ?? "the one we lost").Replace("{tree}", t.Contains("{tree}") ? CharmTreeWords : "");
        }

        /// <summary>{place}: the settlement's name, or "the camp" / "the village" before it has one (it is named at 12 people), so a
        /// line reads the same mid-sentence. A line that starts with it is capitalised by <see cref="DilemmaCap"/>.</summary>
        string DilemmaPlace => string.IsNullOrEmpty(State.settlementName) ? "the " + EraNoun(State.era) : State.settlementName;

        static string DilemmaCap(string t) => string.IsNullOrEmpty(t) || !char.IsLower(t[0]) ? t : char.ToUpperInvariant(t[0]) + t.Substring(1);

        /// <summary>The question as the asker puts it: the def's line for the asker's trait (<see cref="DilemmaDef.askedBy"/>), else
        /// the plain question. Raw (placeholders left in).</summary>
        string RawQuestion(DilemmaDef def, Dilemma d)
        {
            if (def == null) return "";
            var asker = d == null ? null : FindNotable(d.askerId);
            return asker != null && def.askedBy != null && def.askedBy.TryGetValue(asker.trait ?? "", out var q) && !string.IsNullOrEmpty(q) ? q : def.question;
        }

        /// <summary>The question with names in, as the card and the old-question thread show it.</summary>
        public string QuestionOf(Dilemma d) => DilemmaCap(DilemmaText(RawQuestion(DilemmaDefOf(d), d), d));

        /// <summary>Option <paramref name="i"/>'s hint: for a person, the def's line for that person's trait (<see cref="DilemmaDef.traitHints"/>,
        /// {p} = them, {q} = the other one), else the option's own hint.</summary>
        string HintOf(DilemmaDef def, Dilemma d, int i)
        {
            var o = def.options[i];
            var p = FindNotable(d.PersonOf(i));
            if (p != null && def.traitHints != null && def.traitHints.TryGetValue(p.trait ?? "", out var h) && !string.IsNullOrEmpty(h))
                return DilemmaCap(DilemmaText(h.Replace("{p}", i == 0 ? "{a}" : "{b}").Replace("{q}", i == 0 ? "{b}" : "{a}"), d));
            return DilemmaCap(DilemmaText(o.hint, d));
        }

        /// <summary>The words and the people and places of a dilemma, for the card and the choice scene.</summary>
        public DilemmaView ViewOfDilemma(Dilemma d)
        {
            var def = DilemmaDefOf(d);
            if (d == null || def == null) return null;
            var v = new DilemmaView { dilemma = d, def = def, kind = def.kind, question = QuestionOf(d), options = new DilemmaOptionView[def.options.Length] };
            for (int i = 0; i < v.options.Length; i++)
            {
                var o = def.options[i];
                v.options[i] = new DilemmaOptionView
                {
                    index = i, label = DilemmaText(o.label, d), hint = HintOf(def, d, i),
                    personId = d.PersonOf(i), buildingId = d.BuildingOf(i), buildingType = o.buildingType ?? "", icon = o.icon ?? "",
                };
            }
            return v;
        }

        /// <summary>The open dilemma as the view reads it, or null.</summary>
        public DilemmaView OpenDilemmaView => ViewOfDilemma(OpenDilemma);

        string DilemmaHeadline(ChronicleEntry e)
        {
            var d = DilemmaOfEntry(e.id);
            return d == null ? "The people ask the Keeper" : Clip(QuestionOf(d));
        }

        string DilemmaAnswerHeadline(ChronicleEntry e)
        {
            var d = State.dilemmas.FirstOrDefault(x => x.answerEntryId == e.id);
            var def = DilemmaDefOf(d);
            return d == null || def == null || d.option < 0 ? null
                : d.end == DilemmaEnd.LetThemDecide ? $"The Keeper let the people decide: {DilemmaText(def.options[d.option].label, d)}"
                : $"The Keeper chose {DilemmaText(def.options[d.option].label, d)}";
        }

        // ---------- raise ----------
        Dilemma RaiseDilemma(DilemmaDef def, int a, int b)
        {
            var s = State;
            int id = s.dilemmaNextId++;
            if (s.visitCount < 1) s.visitCount = 1; // a session in progress is a visit; the raise is the first time it is shown
            var d = new Dilemma { id = id, defId = def.id, month = s.month, personA = a, personB = b, size = def.size, visits = 1, visitNo = s.visitCount, raisedHours = Math.Round(s.clockHours, 6) };
            if (def.landWords) d.land = PickLand(id);
            if (def.needsDeath) d.deadId = s.notables.Where(n => !n.alive).OrderByDescending(n => n.diedMonth).ThenByDescending(n => n.id).FirstOrDefault()?.id ?? -1;
            PrepareSmall(d, def);
            var leader = Alive(SeatOf(LeaderOf(s.era))?.holderId ?? -1);
            if (def.askerNotLeader)
                d.askerId = s.notables.Where(n => n.alive && n.age >= AdultAge && n.id != (leader?.id ?? -1) && n.id != a && n.id != b)
                    .OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault()?.id ?? -1;
            else if (leader != null && leader.id != a && leader.id != b) d.askerId = leader.id;
            for (int i = 0; i < 2 && i < def.options.Length; i++)
            {
                var bt = def.options[i].buildingType;
                var bld = string.IsNullOrEmpty(bt) ? null : s.buildings.FirstOrDefault(x => x.type == bt);
                if (i == 0) d.buildingA = bld?.id ?? -1; else d.buildingB = bld?.id ?? -1;
            }
            var e = new ChronicleEntry { id = DilemmaIdBase + id, month = s.month, kind = "dilemma", text = QuestionOf(d), salience = 0.6f, notableId = a, otherNotableId = b };
            e.text = LandWords(e.text); // #656/#677
            s.chronicle.Add(e);
            Chronicled?.Invoke(e);
            d.entryId = e.id;
            s.dilemmas.Add(d);
            s.dilemmaLastRaiseHours = Math.Round(s.clockHours, 6);
            if (def.size == DilemmaSize.Big) s.dilemmaNextHours = DilemmaScheduleFrom(s.clockHours, false);
            return d;
        }

        // ---------- answer ----------
        /// <summary>
        /// The Keeper's answer (a recorded input, like a shaping: it takes effect at the sim's current month and is final; there is
        /// no undo). Writes a "keeper" chronicle entry and applies the option's effects. False if the dilemma is not open or
        /// the option is not 0 or 1.
        /// </summary>
        public bool AnswerDilemma(int dilemmaId, int option, DilemmaPath path)
        {
            var d = FindDilemma(dilemmaId);
            var def = DilemmaDefOf(d);
            if (d == null || def == null || d.state != DilemmaState.Open || option < 0 || option >= def.options.Length) return false;
            d.end = DilemmaEnd.Answered;
            var o = def.options[option];
            string text = string.IsNullOrEmpty(o.done) ? $"The people asked, and the Keeper chose {DilemmaText(o.label, d)}." : DilemmaCap(DilemmaText(o.done, d));
            CloseAnswered(d, def, option, path, text);
            return true;
        }

        /// <summary>
        /// #368: "Let them decide": the Keeper hands the choice back. The people take their default (<see cref="DilemmaDefaultOption"/>);
        /// it is credited as the Keeper's choice (state Answered, a "keeper" line, callbacks and the book treat it as the Keeper's pick).
        /// False if the dilemma is not open.
        /// </summary>
        public bool LetThemDecide(int dilemmaId, DilemmaPath path = DilemmaPath.Card)
        {
            var d = FindDilemma(dilemmaId);
            var def = DilemmaDefOf(d);
            if (d == null || def == null || d.state != DilemmaState.Open) return false;
            int option = DilemmaDefaultOption(d);
            d.end = DilemmaEnd.LetThemDecide;
            string label = DilemmaText(def.options[option].label, d);
            CloseAnswered(d, def, option, path, !string.IsNullOrEmpty(def.letDone) ? DilemmaCap(DilemmaText(def.letDone.Replace("{n}", label), d)) : $"The Keeper let the people decide, and they chose {label}.");
            return true;
        }

        void CloseAnswered(Dilemma d, DilemmaDef def, int option, DilemmaPath path, string text)
        {
            d.state = DilemmaState.Answered;
            d.option = option;
            d.path = path;
            d.endMonth = State.month;
            var e = Record("keeper", text, 0.8f, d.PersonOf(option), d.PersonOf(1 - option));
            e.keeper = true;
            d.answerEntryId = e.id;
            // t4-fixes (tester run 3): the Keeper gave this answer and saw it said: it is read, so a later return never leads its peek
            // with the Keeper's own old answer. It stays in History and on the Keeper's choices page.
            if (State.cards != null && e.month > State.cards.since && !State.cards.read.Contains(e.id)) State.cards.read.Add(e.id);
            ApplyDilemmaEffects(d, def, option);
            ScheduleCallbacks(d, def, option);
            LeaveConsequence(d, def, option);
        }

        // ---------- lapse ----------
        /// <summary>Which option the people take when the Keeper says nothing: the role-holder's trait (and who they favour; for a dilemma
        /// with no role, the asker's), else a seeded hash.</summary>
        public int DilemmaDefaultOption(Dilemma d)
        {
            var def = DilemmaDefOf(d);
            if (def == null) return 0;
            if (def.smallKind != null) return SmallDefaultOption(d);
            string role = RoleOfDef(def);
            // The role-holder decides; a dilemma about no role is decided by the one who asked (the leader when it was raised).
            var holder = string.IsNullOrEmpty(role) ? Alive(d.askerId) : Alive(SeatOf(role)?.holderId ?? -1);
            float[] score = new float[def.options.Length];
            for (int i = 0; i < score.Length; i++)
            {
                var o = def.options[i];
                var person = Alive(d.PersonOf(i));
                if (def.cast == DilemmaCast.TwoCandidates && person == null) score[i] -= 100f; // the one who died cannot be chosen
                if (holder == null) continue;
                if (o.favouredBy != null && Array.IndexOf(o.favouredBy, holder.trait) >= 0) score[i] += 2f;
                if (person != null) score[i] += Affinity(holder, person);
            }
            int best = 0; bool tie = true;
            for (int i = 1; i < score.Length; i++) { if (score[i] != score[0]) tie = false; if (score[i] > score[best]) best = i; }
            if (!tie) return best;
            return (int)(DilemmaRoll(30, d.id, d.month) * score.Length) % score.Length;
        }

        /// <summary>The people decide (the choice was shown on three visits without an answer, or a person in it died; a cheat): the default
        /// option's effects apply and the chronicle says so neutrally. No blame, nothing credited to the Keeper. Recorded as unanswered.</summary>
        void LapseDilemma(Dilemma d, DilemmaEnd end = DilemmaEnd.Debug)
        {
            if (d == null || d.state != DilemmaState.Open) return;
            var def = DilemmaDefOf(d);
            d.end = end;
            d.state = DilemmaState.Lapsed;
            d.endMonth = State.month;
            if (def == null) return;
            d.option = DilemmaDefaultOption(d);
            string label = DilemmaText(def.options[d.option].label, d);
            var e = Record("dilemma_result", !string.IsNullOrEmpty(def.letDone) ? DilemmaCap(DilemmaText(def.letDone.Replace("{n}", label), d)) : $"The people chose {label} themselves.", 0.55f, d.PersonOf(d.option), d.PersonOf(1 - d.option));
            d.resultEntryId = e.id;
            ApplyDilemmaEffects(d, def, d.option);
            ScheduleCallbacks(d, def, d.option);
            LeaveConsequence(d, def, d.option);
        }

        // ---------- effects ----------
        static readonly string[] ProudTraits = { "the stubborn", "the grump", "the loud", "the brave", "the clever", "the tinkerer", "the worrier" };

        void ApplyDilemmaEffects(Dilemma d, DilemmaDef def, int option)
        {
            var s = State;
            var o = def.options[option];
            if (def.smallKind != null) { ApplySmall(d, def, option); return; } // a small choice has no role, lean or ties
            var chosen = Alive(d.PersonOf(option));
            var other = Alive(d.PersonOf(1 - option));

            if (o.takesRole && chosen != null)
            {
                string role = RoleOfDef(def);
                var seat = string.IsNullOrEmpty(role) ? null : SeatOf(role);
                if (seat != null)
                {
                    if (seat.holderId >= 0 && seat.holderId != chosen.id) Vacate(seat);   // the holder steps aside; bypasses the succession roll
                    Assign(seat, chosen);
                    if (d.state == DilemmaState.Answered) chosen.chosenByKeeper = true; // #371: the card says "Chosen by the Keeper"
                    RoleLine("role_won", $"{chosen.FullName} became {RoleIn(role)}.", 0.65f, chosen, other, seat);
                }
            }

            if (o.lean.HasValue)
                s.dilemmaLeans.Add(new DilemmaLean { lean = (int)o.lean.Value, fromMonth = s.month, untilMonth = s.month + NudgeMonths, dilemmaId = d.id });

            if (o.ties == DilemmaTies.RivalOrGrace && chosen != null && other != null) UnchosenTies(d, chosen, other);

            if (!string.IsNullOrEmpty(o.custom) && !s.customs.Contains(o.custom)) s.customs.Add(o.custom);

            if (o.strangers > 0 && !string.IsNullOrEmpty(o.apart)) StrangersApart(d, o.strangers, ApartKindFor(d, o.apart), o.joins);
            else if (o.strangers > 0) StrangersJoin(d, o.strangers);
        }

        /// <summary>The unchosen: by trait either a grudge (a rivalry, sometimes) or grace (a friendship, or just good will).</summary>
        void UnchosenTies(Dilemma d, Notable chosen, Notable other)
        {
            bool proud = Array.IndexOf(ProudTraits, other.trait) >= 0;
            bool free = other.rivalId < 0 && chosen.rivalId < 0 && other.partnerId != chosen.id && other.friendId != chosen.id;
            if (proud && free && DilemmaRoll(40, d.id, d.month) < 0.6f)
            {
                other.rivalId = chosen.id; chosen.rivalId = other.id;
                Chronicle("rivalry", $"{other.FullName} could not forgive being passed over for {chosen.FullName}. They have been rivals since.", 0.7f, other.id, chosen.id);
                return;
            }
            if (other.friendId < 0 && chosen.friendId < 0 && other.rivalId != chosen.id && other.partnerId != chosen.id)
            {
                other.friendId = chosen.id; chosen.friendId = other.id;
                Chronicle("friendship", $"{other.FullName} took it well, and wished {chosen.FullName} luck. They became fast friends over it.", 0.6f, other.id, chosen.id);
                return;
            }
            Chronicle("dilemma_grace", proud ? $"{other.name} sulked for a season, then let it go." : $"{other.name} took it gracefully and wished {chosen.name} well.", 0.5f, other.id, chosen.id);
        }

        /// <summary>A family of strangers joins: new notables with one family name, drawn from hashes (no dice).</summary>
        void StrangersJoin(Dilemma d, int count)
        {
            var s = State;
            var made = MakeStrangers(d, count, out string family);
            if (d.state == DilemmaState.Answered) foreach (var n in made) n.chosenByKeeper = true; // #371
            s.population += made.Count;
            string names = string.Join(" and ", made.Select(n => n.name));
            Chronicle("newcomer", $"The {family} family, {names}, wandered in from a far-away tank and joined the {EraNoun(s.era)}.", 0.7f, made[0].id);
        }

        /// <summary>The strangers' people, named and traited from hashes (no dice); a couple when there are two or more. Added to the
        /// notables as they are named (so no two share a name); <see cref="StrangersApart"/> takes them out again.</summary>
        List<Notable> MakeStrangers(Dilemma d, int count, out string family)
        {
            var s = State;
            family = NameData.Families[(int)(DilemmaRoll(50, d.id, d.month) * NameData.Families.Length) % NameData.Families.Length];
            var local = new SimRandom((int)(DilemmaRoll(51, d.id, d.month) * 1_000_000f) + 1); // only for the name retries
            var made = new List<Notable>();
            for (int i = 0; i < count; i++)
            {
                var n = new Notable
                {
                    id = s.NextId(),
                    name = NameData.Given[(int)(DilemmaRoll(52 + i, d.id, d.month) * NameData.Given.Length) % NameData.Given.Length],
                    family = family,
                    trait = NameData.Traits[(int)(DilemmaRoll(60 + i, d.id, d.month) * NameData.Traits.Length) % NameData.Traits.Length],
                    age = 18f + DilemmaRoll(70 + i, d.id, d.month) * 17f,
                    lifespan = Config.lifespanMin + DilemmaRoll(80 + i, d.id, d.month) * (Config.lifespanMax - Config.lifespanMin),
                    bornMonth = s.month,
                };
                MakeNameUnique(n, local);
                s.notables.Add(n);
                made.Add(n);
            }
            if (made.Count > 1) { made[0].partnerId = made[1].id; made[1].partnerId = made[0].id; }
            return made;
        }

        // ---------- debug ----------
        /// <summary>Debug: raise a dilemma now (by def id, else the first that holds), ignoring pacing, the gap and the first run. Lapses
        /// the open dilemma and closes the open wish first (the slot is shared). Null if no def holds.</summary>
        public Dilemma DebugRaiseDilemma(string defId = null)
        {
            var open = OpenDilemma;
            if (open != null) LapseDilemma(open);
            var w = OpenWish;
            if (w != null) CloseWish(w, WishState.Lapsed);
            foreach (var def in DilemmaCatalog)
            {
                if (defId != null && def.id != defId) continue;
                if (defId != null && def.needsDeath && !AnyoneHasDied) DebugKillOldest(); // #346: a cheat asked for it by name: the death it waits for comes first
                if (DefHolds(def, out int a, out int b)) return RaiseDilemma(def, a, b);
            }
            return null;
        }

        /// <summary>Debug (#368): forget the gap and the schedule: the next watched month raises a choice (a wish or the first run may still hold it).</summary>
        public void DebugSkipDilemmaGap()
        {
            State.dilemmaLastRaiseHours = -1e9;
            State.dilemmaNextHours = State.clockHours;
        }

        /// <summary>Debug (#368): the visit counter, the open choice's visits and the hours since the last raise, in a line.</summary>
        public string DilemmaVisitStatus()
        {
            var o = OpenDilemma;
            double since = HoursSinceDilemmaRaised;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "visits counted {0}; open choice: {1}; since last raise {2}; next big due {3}",
                State.visitCount, o == null ? "none" : $"{o.defId} shown on {o.visits}/{DilemmaMaxVisits} visits",
                since > 1e8 ? "never raised" : since.ToString("0.0") + " h",
                State.dilemmaNextHours < 0 ? "not scheduled" : (State.dilemmaNextHours - State.clockHours).ToString("0.0") + " h from now");
        }

        /// <summary>Debug (#368): the player was away <paramref name="hours"/> real hours and opens the app (catch-up, then a visit).</summary>
        public bool DebugOpenAfter(double hours)
        {
            CatchUp(Math.Max(0, hours));
            return BeginVisit();
        }

        /// <summary>Debug: the people decide the open dilemma now. False if none is open.</summary>
        public bool DebugLapseDilemma()
        {
            var open = OpenDilemma;
            if (open == null) return false;
            LapseDilemma(open);
            return true;
        }
    }
}
