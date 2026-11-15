using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>The four yearly rituals (#721, research 21 §7.1): what each tradition choice turns into.</summary>
    public static class RitualKind
    {
        public const string Berrying = "berrying", Woodpile = "woodpile", Feast = "feast", Offering = "offering";
        public static readonly string[] All = { Berrying, Woodpile, Feast, Offering };

        /// <summary>The custom (set by the Keeper's answer, or the people's after a lapse) that starts each ritual.</summary>
        public static string CustomOf(string kind) => kind switch
        {
            Berrying => "winter:baskets", Woodpile => "winter:wood", Feast => "harvest:feast", Offering => "harvest:offering", _ => "",
        };

        /// <summary>The calendar month it plays in (SimCore.CalendarMonth: autumn is 8 to 10). The offering's empty basket is found in
        /// spring (<see cref="OfferingEmptyMonth"/>).</summary>
        public static int MonthOf(string kind) => kind switch { Berrying => 8, Feast => 9, Offering => 9, Woodpile => 10, _ => -1 };
        public const int OfferingEmptyMonth = 3;

        /// <summary>The caption the view shows over the held moment.</summary>
        public static string Caption(string kind) => kind switch
        {
            Berrying => "The berrying walk", Woodpile => "Stacking the woodpile", Feast => "The harvest feast", Offering => "The offering", _ => "",
        };
    }

    /// <summary>
    /// #721 (user 2026-11-14 on village_harvest and camp_winter: "rework to be ritualistic in nature and it replays throughout"): the
    /// Keeper's answer starts a ritual, and <see cref="SimState.customs"/> finally has a reader. Every year in its season the ritual plays,
    /// led by a named person (the leader, else the eldest). The first time it is a card (kind "ritual"); every later year it is one line
    /// in History (kind "ritual_year", never a card). It grows with the box: in the village the berrying walk ends at the long table when
    /// the feast is kept too; in the town the tavern sets the feast table and the priest carries the offering. A lapsed choice starts it
    /// too (the people chose). No dice: rotations and picks come from the year and the ids. The view stages it as a held moment when the
    /// Keeper watches (<see cref="RitualPlayed"/>; Editor work, specs in docs/design/animation-specs.md first). All text is [est].
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The chronicle kind of a ritual's first time ("ritual_feast": a card) and of every later year ("ritual_feast_year":
        /// History only, never a card; SimCore.Cards.cs GroupOf).</summary>
        public static string RitualFirstKind(string kind) => "ritual_" + kind;
        public static string RitualYearKind(string kind) => "ritual_" + kind + "_year";

        /// <summary>The ritual a chronicle line plays ("ritual_feast" or "ritual_feast_year" → "feast"), or null (the empty basket too).</summary>
        public static string RitualOfLine(string entryKind)
        {
            if (entryKind == null || !entryKind.StartsWith("ritual_")) return null;
            foreach (var k in RitualKind.All)
                if (entryKind == RitualFirstKind(k) || entryKind == RitualYearKind(k)) return k;
            return null;
        }

        /// <summary>#721 (Editor pass): the ritual a held moment's line plays, or null.</summary>
        public string RitualOfEntry(int entryId)
        {
            for (int i = State.chronicle.Count - 1; i >= 0; i--)
                if (State.chronicle[i].id == entryId) return RitualOfLine(State.chronicle[i].kind);
            return null;
        }

        /// <summary>A ritual played this month: (kind, the one who led it, its chronicle entry). Its line queues a held moment
        /// (<see cref="MomentKind.Ritual"/>) while the Keeper watches; the view stages it with <see cref="RitualKind.Caption"/>.</summary>
        public event Action<string, Notable, ChronicleEntry> RitualPlayed;

        /// <summary>The rituals this box keeps (a custom answered or lapsed into), in the order of <see cref="RitualKind.All"/>.</summary>
        public IEnumerable<string> Rituals => RitualKind.All.Where(k => State.customs != null && HasCustom(RitualKind.CustomOf(k)));

        /// <summary>Every month: a kept ritual whose month it is, and that has not played this year, plays.</summary>
        void RitualStep()
        {
            if (State.customs == null || State.customs.Count == 0) return;
            int cal = CalendarMonth;
            foreach (var k in Rituals)
                if (cal == RitualKind.MonthOf(k) && !RitualPlayedThisYear(k)) PlayRitual(k);
            if (cal == RitualKind.OfferingEmptyMonth && HasCustom(RitualKind.CustomOf(RitualKind.Offering)) && !KindThisYear("ritual_offering_empty")
                && State.storyFlags.Contains(RitualFlag(RitualKind.Offering)))
                Chronicle("ritual_offering_empty", "The offering basket was empty in the morning. Nobody saw who emptied it.", 0.55f);
        }

        long RitualYear => State.month / SimConfig.MonthsPerYear;

        bool RitualPlayedThisYear(string kind) => KindThisYear(RitualFirstKind(kind), RitualYearKind(kind));

        /// <summary>An entry of one of these kinds was written this game year.</summary>
        bool KindThisYear(params string[] kinds)
        {
            long from = RitualYear * SimConfig.MonthsPerYear;
            for (int i = State.chronicle.Count - 1; i >= 0; i--)
            {
                var e = State.chronicle[i];
                if (e.month < from) break;
                if (Array.IndexOf(kinds, e.kind) >= 0) return true;
            }
            return false;
        }

        /// <summary>The flag set the first time a ritual plays (so a pruned History never makes it "first" again).</summary>
        static string RitualFlag(string kind) => "ritual:" + kind;

        /// <summary>The ritual has played at least once in this box.</summary>
        public bool RitualStarted(string kind) => State.storyFlags.Contains(RitualFlag(kind));

        /// <summary>How many years this ritual has played (its entries in the chronicle, pruned ones counted).</summary>
        public int RitualTimes(string kind) =>
            Told(RitualFirstKind(kind)) + Told(RitualYearKind(kind));

        /// <summary>Who leads it this year: the priest for the town's offering, else the era's leader, else the eldest adult.</summary>
        public Notable RitualLead(string kind)
        {
            if (kind == RitualKind.Offering && State.era == Era.Town && HolderOf(RoleType.Priest) is Notable priest && priest.alive) return priest;
            var leader = Alive(SeatOf(LeaderOf(State.era))?.holderId ?? -1);
            return leader ?? State.notables.Where(n => n.alive && n.age >= AdultAge).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
        }

        /// <summary>A child for a line that names one, picked from the year (no dice); null when there is none.</summary>
        Notable RitualKid()
        {
            var kids = State.notables.Where(n => n.alive && n.age >= 4f && n.age < AdultAge).OrderBy(n => n.id).ToList();
            return kids.Count == 0 ? null : kids[(int)(RitualYear % kids.Count)];
        }

        /// <summary>This year's line for a ritual (rotating by year, grown by era); {lead} filled.</summary>
        string RitualLine(string kind, Notable lead, bool first)
        {
            string who = lead?.name ?? "Everyone";
            long y = RitualYear;
            var kid = RitualKid();
            switch (kind)
            {
                case RitualKind.Berrying:
                    if (State.era >= Era.Village && HasCustom(RitualKind.CustomOf(RitualKind.Feast)) && !first && y % 3 == 2)
                        return "The berrying walk ended at the long table this year.";
                    return kid != null && y % 2 == 1 && !first
                        ? $"{who} led the berrying walk, and {kid.name} ate more than they carried."
                        : $"{who} led the berrying walk. Everyone came home with purple fingers.";
                case RitualKind.Woodpile:
                    return y % 2 == 1 && !first
                        ? $"{who} mended the woodpile's roof, and everyone passed a log along the line."
                        : $"{who} set the last log on the woodpile before the snow.";
                case RitualKind.Feast:
                    if (State.era == Era.Town && Has(BuildingType.Tavern) && y % 2 == 0) return "The tavern set out the long table for the harvest feast.";
                    return first || y % 2 == 0
                        ? $"{Math.Max(2, State.population)} people sat down at the harvest table, and {who} served the first bowl."
                        : $"The harvest table was one seat longer than last year. {who} sat at the end.";
                case RitualKind.Offering:
                    if (State.era == Era.Town && lead != null && lead.id == HolderOf(RoleType.Priest)?.id)
                        return $"The priest carried the offering to {OfferingPlace}, and the town walked behind.";
                    return $"{who} carried the best basket to {OfferingPlace} and set it down.";
            }
            return "";
        }

        /// <summary>#723: where the offering goes: the Keeper's tree once it stands, else the tallest tree [est] (the slice has none).</summary>
        string OfferingPlace => KeeperTreeGrown ? "the Keeper's tree" : "the tallest tree";

        /// <summary>Plays the ritual now: its line (a card the first time, History after), the event for the view.</summary>
        ChronicleEntry PlayRitual(string kind)
        {
            var lead = RitualLead(kind);
            bool first = !RitualStarted(kind);
            if (first) State.storyFlags.Add(RitualFlag(kind));
            var e = Record(first ? RitualFirstKind(kind) : RitualYearKind(kind), RitualLine(kind, lead, first), first ? 0.7f : 0.3f, lead?.id ?? -1, -1);
            RitualPlayed?.Invoke(kind, lead, e);
            return e;
        }

        // ---------- debug (SimDebugPanel's "Rituals" block) ----------
        /// <summary>Debug: keep this ritual (its custom set if the choice was never asked) and play it now, whatever the month. Null for an unknown kind.</summary>
        public ChronicleEntry DebugPlayRitual(string kind)
        {
            string custom = RitualKind.CustomOf(kind);
            if (string.IsNullOrEmpty(custom)) return null;
            EnsureDilemmaState();
            if (!State.customs.Contains(custom)) State.customs.Add(custom);
            return PlayRitual(kind);
        }
    }
}
