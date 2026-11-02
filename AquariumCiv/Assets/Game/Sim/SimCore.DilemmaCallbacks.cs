using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    public enum CallbackState { Pending, Fired, Dropped }

    /// <summary>Who a callback credits: the Keeper (they answered) or the people (the dilemma lapsed).</summary>
    public enum CallbackCredit { Keeper, People }

    /// <summary>One scheduled callback (P7, #221), made when a dilemma ends. Saved with the box.</summary>
    [Serializable]
    public class DilemmaCallback
    {
        public int id;
        public int dilemmaId;
        public int option;                  // the option taken
        public int index;                   // which of the option's callbacks
        public long dueMonth;
        public CallbackState state;
        public long firedMonth = -1;
        public int entryId = -1;            // the chronicle entry it wrote (kind "dilemma_callback"); -1 if dropped or pending
        public bool usedFallback;
    }

    /// <summary>What the view or telemetry reads from a callback chronicle entry.</summary>
    public sealed class CallbackInfo
    {
        public DilemmaCallback callback;
        public Dilemma dilemma;
        public int option;
        public CallbackCredit credit;
        public float yearsSinceAnswer;      // from the end of the dilemma to the entry
    }

    /// <summary>
    /// Dilemma callbacks (P7 rule 4, #221): every option comes back years later, credited "Because the Keeper chose ..." (answered) or
    /// "Because they chose ..." (lapsed). Scheduled at the dilemma's end from a hash of the seed and the dilemma id (no dice), fired
    /// by <see cref="CallbackStep"/> in the monthly step (one per month at most). The entry is a story card whose thread is the old
    /// question, so it gets the teaser payoff boost and shows the question as the grey line above.
    /// </summary>
    public partial class SimCore
    {
        public const string CallbackKind = "dilemma_callback";

        void ScheduleCallbacks(Dilemma d, DilemmaDef def, int option)
        {
            var cbs = def.options[option].callbacks;
            if (cbs == null) return;
            var s = State;
            for (int i = 0; i < cbs.Length; i++)
            {
                var c = cbs[i];
                int lo = Math.Max(0, c.minYears) * SimConfig.MonthsPerYear, hi = Math.Max(lo, c.maxYears * SimConfig.MonthsPerYear);
                long due = d.endMonth + lo + (int)(DilemmaRoll(90 + i, d.id * 4 + option, d.endMonth) * (hi - lo + 1));
                s.dilemmaCallbacks.Add(new DilemmaCallback { id = s.dilemmaCallbacks.Count + 1, dilemmaId = d.id, option = option, index = i, dueMonth = due });
            }
        }

        /// <summary>The callbacks still to come, soonest first.</summary>
        public List<DilemmaCallback> PendingCallbacks() =>
            State.dilemmaCallbacks.Where(c => c.state == CallbackState.Pending).OrderBy(c => c.dueMonth).ThenBy(c => c.id).ToList();

        DilemmaCallbackDef CallbackDefOf(DilemmaCallback c)
        {
            var d = FindDilemma(c.dilemmaId);
            var def = DilemmaDefOf(d);
            var cbs = def == null || c.option < 0 || c.option >= def.options.Length ? null : def.options[c.option].callbacks;
            return cbs == null || c.index < 0 || c.index >= cbs.Length ? null : cbs[c.index];
        }

        /// <summary>Fires the soonest callback that is due (one per month); the others wait for the next month.</summary>
        void CallbackStep()
        {
            var list = State.dilemmaCallbacks;
            if (list.Count == 0) return;
            DilemmaCallback next = null;
            foreach (var c in list)
                if (c.state == CallbackState.Pending && c.dueMonth <= State.month && (next == null || c.dueMonth < next.dueMonth)) next = c;
            if (next != null) FireCallback(next);
        }

        bool CallbackHolds(DilemmaCallbackDef def, Dilemma d)
        {
            if (def.aliveA && Alive(d.personA) == null) return false;
            if (def.aliveB && Alive(d.personB) == null) return false;
            if (!string.IsNullOrEmpty(def.needsBuilding) && !Has(def.needsBuilding)) return false;
            if (!string.IsNullOrEmpty(def.needsCustom) && !HasCustom(def.needsCustom)) return false;
            return true;
        }

        ChronicleEntry FireCallback(DilemmaCallback c)
        {
            var d = FindDilemma(c.dilemmaId);
            var ddef = DilemmaDefOf(d);
            var def = CallbackDefOf(c);
            c.state = CallbackState.Dropped;
            if (d == null || ddef == null || def == null) return null;
            bool ok = CallbackHolds(def, d);
            string raw = ok ? def.text : def.fallback;
            if (string.IsNullOrEmpty(raw)) return null; // no fallback: dropped quietly
            string label = DilemmaText(ddef.options[c.option].label, d);
            string credit = (d.state == DilemmaState.Answered ? "Because the Keeper chose " : "Because they chose ") + label;
            string text = DilemmaText(raw, d).Replace("{chosen}", label);
            if (raw.Contains("{credit}")) text = text.Replace("{credit}", credit);
            else text = credit + ", " + (raw.StartsWith("{") || text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text.Substring(1));
            int person = Alive(d.PersonOf(c.option)) != null ? d.PersonOf(c.option) : Alive(d.personA) != null ? d.personA : Alive(d.personB) != null ? d.personB : -1;
            var e = Record(CallbackKind, text, Math.Min(0.95f, def.salience), person, -1);
            e.buildingId = d.BuildingOf(c.option);
            c.state = CallbackState.Fired;
            c.firedMonth = State.month;
            c.entryId = e.id;
            c.usedFallback = !ok;
            return e;
        }

        /// <summary>Annotation: the callback's thread is the old question, already paid off by this entry.</summary>
        int CallbackThread(ChronicleEntry e)
        {
            var c = State.dilemmaCallbacks.FirstOrDefault(x => x.entryId == e.id);
            var d = c == null ? null : FindDilemma(c.dilemmaId);
            if (d == null) return 0;
            var t = NewThread(ThreadSource.Dilemma, DilemmaText(DilemmaDefOf(d)?.question, d), e.notableId, e.otherNotableId);
            t.month = d.month;
            t.state = ThreadState.PaidOff;
            t.payoffEntryId = e.id;
            return t.id;
        }

        /// <summary>For the View and telemetry: the dilemma, option, credit and years since the answer, from a callback entry (null if it is not one).</summary>
        public CallbackInfo CallbackInfoOf(ChronicleEntry e)
        {
            if (e == null || e.kind != CallbackKind) return null;
            var c = State.dilemmaCallbacks.FirstOrDefault(x => x.entryId == e.id);
            var d = c == null ? null : FindDilemma(c.dilemmaId);
            if (d == null) return null;
            return new CallbackInfo
            {
                callback = c, dilemma = d, option = c.option,
                credit = d.state == DilemmaState.Answered ? CallbackCredit.Keeper : CallbackCredit.People,
                yearsSinceAnswer = (e.month - d.endMonth) / (float)SimConfig.MonthsPerYear,
            };
        }

        /// <summary>Debug: fires the next pending callback now, ignoring its date and the monthly limit. Ones with nothing to tell
        /// (condition failed, no fallback) are dropped on the way. Returns the entry, or null if none was left to tell.</summary>
        public ChronicleEntry DebugFireCallbackNow()
        {
            foreach (var c in PendingCallbacks())
            {
                var e = FireCallback(c);
                if (e != null) return e;
            }
            return null;
        }
    }
}
