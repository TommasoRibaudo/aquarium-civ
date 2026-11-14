using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim.Replay;

namespace AquariumCiv.Sim
{
    /// <summary>#373: the Keeper's choices for the book ("The Keeper's choices", one section per era) and the time-lapse (hand pins).
    /// Reads only; the shaping is the pure <see cref="KeeperChoices"/>.</summary>
    public partial class SimCore
    {
        /// <summary>The kind of a hand pin on the time-lapse (a <see cref="StoryPin.kind"/>).</summary>
        public const string KeeperPinKind = "keeper_choice";

        /// <summary>The era a month fell in (by the stored era starts).</summary>
        public Era EraAtMonth(long month)
        {
            HistoryEras(Hist);
            var era = Era.Camp;
            for (int i = 0; i < Hist.eraStarts.Count && i <= (int)Era.Town; i++)
                if (Hist.eraStarts[i] <= month) era = (Era)i;
            return era;
        }

        /// <summary>Every choice that has ended, as plain data (open ones are not listed).</summary>
        public List<KeeperChoiceInput> KeeperChoiceInputs()
        {
            var res = new List<KeeperChoiceInput>();
            foreach (var d in State.dilemmas)
            {
                if (d.state == DilemmaState.Open || d.option < 0 || d.endMonth < 0) continue;
                var def = DilemmaDefOf(d);
                if (def == null || d.option >= def.options.Length) continue;
                var input = new KeeperChoiceInput
                {
                    dilemmaId = d.id, era = EraAtMonth(d.endMonth), endMonth = d.endMonth, raisedHours = d.raisedHours,
                    question = QuestionOf(d), shortAsk = string.IsNullOrEmpty(def.shortAsk) ? "" : DilemmaCap(DilemmaText(def.shortAsk, d)),
                    chosen = DilemmaText(def.options[d.option].label, d), end = d.end, answerEntryId = d.answerEntryId, personId = d.PersonOf(d.option),
                };
                // Who it touched: a small choice is about the baby and its parents; a big one about the person taken, then the other.
                var ids = new List<int>();
                if (d.subjectId >= 0) ids.Add(d.subjectId);
                ids.Add(d.PersonOf(d.option));
                ids.Add(d.PersonOf(1 - d.option));
                foreach (var id in ids.Where(x => x >= 0).Distinct())
                {
                    var n = FindNotable(id);
                    if (n != null) input.touched.Add(n.FullName);
                }
                var prop = ConsequencePropFor(d.id, d.option);
                if (prop != null)
                {
                    input.propKind = prop.kind; input.propPlaced = prop.placed;
                    if (prop.anchor == PropAnchor.Tree && prop.placed && prop.treeCell >= 0)
                        input.propWords = prop.treeCell == State.eco.treeCell ? "charms on the Keeper's tree" : "charms on a tree"; // #635
                }
                res.Add(input);
            }
            return res;
        }

        /// <summary>Debug (#373): three sample choices in every era the box has reached (answered, let them decide, the people's after a
        /// lapse), each with the thing it leaves, at a quarter, a half and three quarters of the era. Makes the page and the hand pins
        /// show at once. Returns how many it added. Calling it again adds no more.</summary>
        public int DebugFillKeeperChoices()
        {
            var s = State;
            if (s.dilemmas.Any(d => d.id >= DebugChoiceIdBase)) return 0;
            var people = s.notables.Where(n => n.alive).OrderBy(n => n.id).Select(n => n.id).ToList();
            int Person(int k) => people.Count == 0 ? -1 : people[k % people.Count];
            var outcomes = new[] { DilemmaEnd.Answered, DilemmaEnd.LetThemDecide, DilemmaEnd.Visits };
            int added = 0, fake = DebugChoiceIdBase;
            for (int e = 0; e <= (int)s.era; e++)
            {
                long from = EraStartMonth((Era)e), to = e < (int)s.era ? EraStartMonth((Era)(e + 1)) : s.month;
                if (from < 0 || to <= from) continue;
                var defs = DilemmaCatalog.Where(x => x.minEra <= (Era)e && (Era)e <= x.maxEra && x.smallKind == null).Concat(SmallCatalog).ToList();
                if (defs.Count == 0) continue;
                for (int k = 0; k < 3; k++)
                {
                    var def = defs[(e * 3 + k) % defs.Count];
                    int option = k % 2;
                    long month = from + (to - from) * (k + 1) / 4;
                    var d = new Dilemma
                    {
                        id = fake++, defId = def.id, month = month, endMonth = month, state = outcomes[k] == DilemmaEnd.Visits ? DilemmaState.Lapsed : DilemmaState.Answered,
                        end = outcomes[k], option = option, size = def.size, raisedHours = month, personA = Person(e * 3 + k), personB = Person(e * 3 + k + 1),
                        subjectId = def.smallKind != null ? Person(e * 3 + k + 2) : -1, nameA = def.smallKind != null ? "Mira" : "", nameB = def.smallKind != null ? "Tove" : "",
                    };
                    if (outcomes[k] != DilemmaEnd.Visits)
                    {
                        string label = DilemmaText(def.options[option].label, d);
                        var entry = new ChronicleEntry
                        {
                            id = s.NextId(), month = month, kind = "keeper", keeper = true, salience = 0.8f, notableId = d.PersonOf(option), otherNotableId = d.PersonOf(1 - option),
                            text = outcomes[k] == DilemmaEnd.Answered ? $"The Keeper chose {label}." : $"The Keeper let the people decide, and they chose {label}.",
                        };
                        entry.text = LandWords(entry.text); // #656/#677
                        s.chronicle.Add(entry);
                        d.answerEntryId = entry.id;
                    }
                    s.dilemmas.Add(d);
                    LeaveConsequence(d, def, option);
                    added++;
                }
            }
            foreach (var p in State.consequenceProps) if (!p.placed) TryPlaceProp(p, true);
            return added;
        }

        /// <summary>Sample choices made by <see cref="DebugFillKeeperChoices"/> take ids from here, clear of real ones.</summary>
        public const int DebugChoiceIdBase = 91000;

        /// <summary>The page's sections: one per era that has a choice.</summary>
        public List<KeeperChoicesEra> KeeperChoicesByEra() => KeeperChoices.Build(KeeperChoiceInputs());

        /// <summary>The hand pins: one per choice the Keeper made (answered or let the people decide), at the month it was made.</summary>
        public List<StoryPin> KeeperChoicePins()
        {
            var res = new List<StoryPin>();
            foreach (var sec in KeeperChoicesByEra())
                foreach (var l in sec.lines)
                {
                    if (!l.credited || l.answerEntryId < 0) continue;
                    res.Add(new StoryPin
                    {
                        entryId = l.answerEntryId, month = l.endMonth, year = l.endMonth / (float)SimConfig.MonthsPerYear, kind = KeeperPinKind,
                        text = l.pinText, salience = 0.8f, notableId = l.personId, buildingId = -1,
                    });
                }
            return res;
        }
    }
}
