using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #372: the small choices that sit between the big dilemmas (visit-loop.md 2.4). A small choice is a <see cref="DilemmaDef"/>
    /// with <see cref="DilemmaDef.smallKind"/> set: it rides the dilemma machinery (one open at a time, the gap, three visits, Let them
    /// decide, the card and the choice scene) but is raised from a box's own moment, not from the era. To add a kind: add a def to
    /// <see cref="SmallChoices.All"/>, and a case in <see cref="SimCore.SmallWhyNot"/> (does the moment exist?),
    /// <see cref="SimCore.PrepareSmall"/> (fill the card's words) and <see cref="SimCore.ApplySmall"/> (what the pick does).
    /// </summary>
    public static class SmallChoices
    {
        public const string Naming = "naming";

        /// <summary>A newborn can be named by the Keeper until this age (game years).</summary>
        public const float NewbornNameAge = 4f;

        static DilemmaOptionDef Name(int n) => new DilemmaOptionDef
        {
            label = "{n" + n + "}", hint = n == 1 ? "{a}'s name for the baby." : "{b}'s name for the baby.",
            done = "The Keeper named the baby {n" + n + "}.",
            prop = ConsequenceKind.NamePeg, propAt = PropAnchor.Home,
        };

        /// <summary>Naming a notable couple's newborn: each parent offers a name. Option 0 points at the first parent, option 1 at the second.</summary>
        static readonly DilemmaDef NamingDef = new DilemmaDef
        {
            id = "small_naming", kind = DilemmaKind.Who, size = DilemmaSize.Small, smallKind = Naming, cast = DilemmaCast.TwoCandidates,
            minEra = Era.Camp, maxEra = Era.Town,
            question = "{a} and {b} have a baby, and two names. {a} says {n1}. {b} says {n2}.",
            shortAsk = "Naming {a} and {b}'s baby.",
            letDone = "{a} and {b} named the baby {n}.",
            options = new[] { Name(1), Name(2) },
        };

        public static readonly DilemmaDef[] All = { NamingDef };
    }

    public partial class SimCore
    {
        /// <summary>The small choices the sim draws from. Tests can swap it; default <see cref="SmallChoices.All"/>.</summary>
        public IReadOnlyList<DilemmaDef> SmallCatalog { get; set; } = SmallChoices.All;

        /// <summary>The newborn a naming waits on: a living child of a living couple, young, not named yet by the Keeper and not asked about
        /// before. The lowest id wins. Null when there is none (nothing is invented).</summary>
        public Notable NamingNewborn()
        {
            var s = State;
            string defId = SmallDefId(SmallChoices.Naming);
            foreach (var c in s.notables)
            {
                if (!c.alive || c.namedByKeeper || c.age >= SmallChoices.NewbornNameAge) continue;
                var p = Alive(c.parentId);
                var q = p == null ? null : Alive(p.partnerId);
                if (p == null || q == null) continue;
                if (s.dilemmas.Any(d => d.subjectId == c.id && d.defId == defId)) continue;
                return c;
            }
            return null;
        }

        string SmallDefId(string kind) => SmallCatalog.FirstOrDefault(x => x.smallKind == kind)?.id ?? "";

        /// <summary>Why this small def can't be raised now (null = it can); a and b are the people its options point at.</summary>
        string SmallWhyNot(DilemmaDef def, out int a, out int b)
        {
            a = b = -1;
            if (def.smallKind == SmallChoices.Naming)
            {
                var c = NamingNewborn();
                if (c == null) return "no notable newborn is waiting for a name";
                var p = Alive(c.parentId); var q = Alive(p.partnerId);
                a = p.id; b = q.id;
                return null;
            }
            return "unknown small choice";
        }

        List<(DilemmaDef def, int a, int b)> EligibleSmall()
        {
            var list = new List<(DilemmaDef def, int a, int b)>();
            foreach (var def in SmallCatalog)
                if (SmallWhyNot(def, out int a, out int b) == null) list.Add((def, a, b));
            return list;
        }

        bool NameClash(Notable child, string given) =>
            given == child.name || State.notables.Any(o => o != child && (o.alive && o.name == given || o.name == given && o.family == child.family));

        /// <summary>Fill the card's words once the question is raised: the two names, from hashes (no dice), distinct from each other,
        /// from every living person's name and from the baby's own.</summary>
        void PrepareSmall(Dilemma d, DilemmaDef def)
        {
            if (def.smallKind != SmallChoices.Naming) return;
            var c = NamingNewborn();
            if (c == null) return;
            d.subjectId = c.id;
            var pool = NameData.Given;
            string Pick(int salt, string not)
            {
                int start = (int)(DilemmaRoll(salt, d.id, d.month) * pool.Length) % pool.Length;
                for (int i = 0; i < pool.Length; i++)
                {
                    string g = pool[(start + i) % pool.Length];
                    if (g != not && !NameClash(c, g) && (not == null || g[0] != not[0])) return g;
                }
                for (int i = 0; i < pool.Length; i++) // the table is nearly used up: any name that is free and different
                {
                    string g = pool[(start + i) % pool.Length];
                    if (g != not && !NameClash(c, g)) return g;
                }
                return pool[start];
            }
            d.nameA = Pick(120, null);
            d.nameB = Pick(121, d.nameA);
        }

        /// <summary>What the pick does: the baby takes the name. Flagged as the Keeper's only when the Keeper answered (or let them
        /// decide), never when the people took it after a lapse.</summary>
        void ApplySmall(Dilemma d, DilemmaDef def, int option)
        {
            if (def.smallKind != SmallChoices.Naming) return;
            var c = FindNotable(d.subjectId);
            if (c == null || !c.alive) return;
            string name = option == 0 ? d.nameA : d.nameB;
            if (string.IsNullOrEmpty(name)) return;
            string was = c.FullName;
            c.name = name;
            c.namedByKeeper = d.end == DilemmaEnd.Answered || d.end == DilemmaEnd.LetThemDecide;
            foreach (var e in State.chronicle) // the birth line told the old name: it now reads the new one
                if (e.kind == "birth" && e.notableId == c.id && e.text.Contains(was)) e.text = e.text.Replace(was, c.FullName);
        }

        /// <summary>The words of the name placeholders on a small card.</summary>
        string SmallText(string t, Dilemma d) =>
            t.Replace("{n1}", d.nameA ?? "").Replace("{n2}", d.nameB ?? "").Replace("{child}", FindNotable(d.subjectId)?.name ?? "the baby");

        /// <summary>True if the Keeper named this person (the person card shows "chosen by the Keeper", C4).</summary>
        public bool NamedByKeeper(int notableId) => FindNotable(notableId)?.namedByKeeper ?? false;

        /// <summary>Which of the two the parents take when the Keeper says nothing: a hash, so it is the same in every replay. A parent who
        /// has died cannot be taken.</summary>
        int SmallDefaultOption(Dilemma d)
        {
            bool a = Alive(d.personA) != null, b = Alive(d.personB) != null;
            if (a != b) return a ? 0 : 1;
            return (int)(DilemmaRoll(31, d.id, d.month) * 2f) % 2;
        }

        /// <summary>Debug (#372): raise the naming now, making a notable newborn first if none waits. Null if no couple can be found.</summary>
        public Dilemma DebugRaiseNaming()
        {
            var open = OpenDilemma;
            if (open != null) LapseDilemma(open);
            var w = OpenWish;
            if (w != null) CloseWish(w, WishState.Lapsed);
            if (NamingNewborn() == null && DebugMakeNotableNewborn() == null) return null;
            var def = SmallCatalog.FirstOrDefault(x => x.smallKind == SmallChoices.Naming);
            if (def == null || SmallWhyNot(def, out int a, out int b) != null) return null;
            return RaiseDilemma(def, a, b);
        }

        /// <summary>Debug (#372): a baby is born to a living adult couple (two free adults are wed first). Null if there are no two adults.</summary>
        public Notable DebugMakeNotableNewborn()
        {
            var s = State;
            var adults = s.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.id).ToList();
            var p = adults.FirstOrDefault(n => Alive(n.partnerId) != null);
            if (p == null)
            {
                p = adults.FirstOrDefault(n => n.partnerId < 0);
                var q = adults.FirstOrDefault(n => n != p && n.partnerId < 0);
                if (p == null || q == null) return null;
                p.partnerId = q.id; q.partnerId = p.id;
            }
            var child = AddNotable(0f, p.id, HouseOf(p));
            var partner = Alive(p.partnerId);
            Chronicle("birth", $"{child.FullName} was born to {p.FullName} and {partner.FullName}.", 0.6f, child.id);
            return child;
        }
    }
}
