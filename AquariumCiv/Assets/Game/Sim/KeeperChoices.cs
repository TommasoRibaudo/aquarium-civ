using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>#373: one ended choice as the sim hands it over (plain data, no sim). The words are already filled in (names, labels).</summary>
    public sealed class KeeperChoiceInput
    {
        public int dilemmaId;
        public Era era;                     // the era it ended in
        public long endMonth;
        public double raisedHours;
        public string question = "";        // the full question, names in
        public string shortAsk = "";        // the def's own short form; empty = cut from the question
        public string chosen = "";          // the label of the option taken
        public DilemmaEnd end;
        public List<string> touched = new List<string>();   // the people it was about, first the one taken
        public string propKind = "";        // ConsequenceKind, "" = it left nothing
        public bool propPlaced;
        public int answerEntryId = -1;      // the Keeper's chronicle line (an answer), the hand pin's entry; -1 for a lapse
        public int personId = -1;           // the person taken (the pin's card button)
    }

    /// <summary>One line of the page, ready to draw: what was asked, what was chosen, who it touched, what it left.</summary>
    public sealed class KeeperChoiceLine
    {
        public int dilemmaId;
        public Era era;
        public int year;                    // as the plate reads it (month / 12 + 1)
        public long endMonth;
        public bool credited;               // the Keeper's (answered or let them decide); a lapse is the people's
        public string ask = "", chosenLine = "", touchedLine = "", propLine = "";
        public string pinText = "";         // the hand pin's one line (credited choices only)
        public int answerEntryId = -1, personId = -1;
    }

    /// <summary>One era's section of the page.</summary>
    public sealed class KeeperChoicesEra
    {
        public Era era;
        public List<KeeperChoiceLine> lines = new List<KeeperChoiceLine>();
    }

    /// <summary>
    /// #373 (visit-loop.md 2.5): shapes the Keeper's choices for the book and the time-lapse. Pure: no sim, no dice.
    /// Wording: "The Keeper chose X." / "The Keeper let the people decide: X." (both credited) / "The people chose X." (a lapse,
    /// not credited). Short lines that read on a phone; closed-world words.
    /// </summary>
    public static class KeeperChoices
    {
        public const int AskMax = 60;

        /// <summary>The page's lines, grouped by era (era order), each era's in the order the choices were made.</summary>
        public static List<KeeperChoicesEra> Build(IEnumerable<KeeperChoiceInput> inputs)
        {
            var res = new List<KeeperChoicesEra>();
            if (inputs == null) return res;
            foreach (var g in inputs.GroupBy(i => i.era).OrderBy(g => (int)g.Key))
            {
                var sec = new KeeperChoicesEra { era = g.Key };
                foreach (var i in g.OrderBy(x => x.endMonth).ThenBy(x => x.raisedHours).ThenBy(x => x.dilemmaId)) sec.lines.Add(LineOf(i));
                res.Add(sec);
            }
            return res;
        }

        public static KeeperChoiceLine LineOf(KeeperChoiceInput i)
        {
            bool credited = i.end == DilemmaEnd.Answered || i.end == DilemmaEnd.LetThemDecide;
            var l = new KeeperChoiceLine
            {
                dilemmaId = i.dilemmaId, era = i.era, endMonth = i.endMonth, year = (int)(i.endMonth / SimConfig.MonthsPerYear) + 1, credited = credited,
                ask = ShortAsk(string.IsNullOrEmpty(i.shortAsk) ? i.question : i.shortAsk),
                chosenLine = ChosenLine(i.end, i.chosen),
                touchedLine = TouchedLine(i.touched),
                propLine = PropLine(i.propKind, i.propPlaced),
                answerEntryId = credited ? i.answerEntryId : -1, personId = i.personId,
            };
            if (credited) l.pinText = (l.ask.Length > 0 ? l.ask + " " : "") + l.chosenLine;
            return l;
        }

        public static string ChosenLine(DilemmaEnd end, string chosen) =>
            end == DilemmaEnd.Answered ? $"The Keeper chose {chosen}."
            : end == DilemmaEnd.LetThemDecide ? $"The Keeper let the people decide: {chosen}."
            : $"The people chose {chosen}.";

        public static string TouchedLine(IList<string> names)
        {
            var n = names == null ? new List<string>() : names.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            if (n.Count == 0) return "";
            return "It touched " + (n.Count == 1 ? n[0] : string.Join(", ", n.Take(n.Count - 1)) + " and " + n[n.Count - 1]) + ".";
        }

        public static string PropLine(string kind, bool placed)
        {
            if (string.IsNullOrEmpty(kind)) return "";
            return placed ? $"It left {PropName(kind)} in the box." : $"It will leave {PropName(kind)} in the box.";
        }

        /// <summary>The thing, in words ("a feather on a post"). An unknown kind reads from its id.</summary>
        public static string PropName(string kind)
        {
            switch (kind)
            {
                case ConsequenceKind.Feather: return "a feather on a post";
                case ConsequenceKind.Baskets: return "a stack of berry baskets";
                case ConsequenceKind.WoodpileRoof: return "a woodpile under a roof";
                case ConsequenceKind.NameStone: return "a stone cut with names";
                case ConsequenceKind.CharmPole: return "a pole hung with charms";
                case ConsequenceKind.StewPot: return "a stew pot on stones";
                case ConsequenceKind.AxeStump: return "an axe in a stump";
                case ConsequenceKind.Staff: return "a staff with a ribbon";
                case ConsequenceKind.FlourSacks: return "sacks of flour";
                case ConsequenceKind.RoofBird: return "a carved bird on a roof";
                case ConsequenceKind.LongTable: return "a long table";
                case ConsequenceKind.OfferingBasket: return "an offering basket";
                case ConsequenceKind.GoatFence: return "a little goat pen";
                case ConsequenceKind.FishCrate: return "a crate of fish";
                case ConsequenceKind.NamePeg: return "a peg with a name tag";
                default: return "a " + (kind ?? "thing").Replace('_', ' ');
            }
        }

        /// <summary>The question in short: the sentence that asks (ends in "?"), else the first one; cut to <see cref="AskMax"/>.</summary>
        public static string ShortAsk(string question)
        {
            if (string.IsNullOrWhiteSpace(question)) return "";
            var parts = new List<string>();
            int start = 0;
            for (int k = 0; k < question.Length; k++)
            {
                char c = question[k];
                if ((c == '.' || c == '?' || c == '!') && (k + 1 >= question.Length || question[k + 1] == ' '))
                {
                    parts.Add(question.Substring(start, k + 1 - start).Trim());
                    start = k + 1;
                }
            }
            if (start < question.Length && question.Substring(start).Trim().Length > 0) parts.Add(question.Substring(start).Trim());
            string pick = parts.LastOrDefault(p => p.EndsWith("?")) ?? parts.FirstOrDefault() ?? question.Trim();
            if (pick.Length <= AskMax) return pick;
            int cut = pick.LastIndexOf(' ', AskMax - 1);
            return pick.Substring(0, cut > 20 ? cut : AskMax - 1).TrimEnd(',', ' ', ':', ';') + "…";
        }
    }
}
