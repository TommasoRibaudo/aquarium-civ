using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>#589: the supper choice as the life planner reads it.</summary>
    public static class SupperPick
    {
        public const int None = 0, Pot = 1, Baskets = 2;
    }

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
        /// <summary>t4-fixes: who tells the story at the fire tonight (two adults). Almost always there in a camp.</summary>
        public const string Story = "story";
        /// <summary>t4-fixes: supper tonight, one big pot or a basket each. Always there in a camp.</summary>
        public const string Supper = "supper";
        /// <summary>t4-fixes: a story or supper choice is asked at most this many times in a box (each leaves its thing).</summary>
        public const int RepeatMax = 2;

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

        /// <summary>t4-fixes: who tells the story at the fire tonight. Option 0 is the first adult, option 1 the second. The teller
        /// keeps a feather by their home.</summary>
        static readonly DilemmaDef StoryDef = new DilemmaDef
        {
            id = "small_story", kind = DilemmaKind.Who, size = DilemmaSize.Small, smallKind = Story, cast = DilemmaCast.TwoCandidates,
            minEra = Era.Camp, maxEra = Era.Village,
            question = "Who tells the story at the fire tonight: {a} or {b}?",
            shortAsk = "Who tells tonight's story.",
            letDone = "{n} told the story at the fire.",
            options = new[]
            {
                new DilemmaOptionDef { label = "{a}", hint = "{a} tells it slow, with all the voices.", done = "The Keeper picked {a} to tell the story.",
                    prop = ConsequenceKind.Feather, propAt = PropAnchor.Home },
                new DilemmaOptionDef { label = "{b}", hint = "{b} tells it short, and gets a laugh.", done = "The Keeper picked {b} to tell the story.",
                    prop = ConsequenceKind.Feather, propAt = PropAnchor.Home },
            },
        };

        /// <summary>t4-fixes: supper tonight. One big pot stays by the fire, or the baskets stack up there.</summary>
        static readonly DilemmaDef SupperDef = new DilemmaDef
        {
            id = "small_supper", kind = DilemmaKind.Tradition, size = DilemmaSize.Small, smallKind = Supper, cast = DilemmaCast.None,
            minEra = Era.Camp, maxEra = Era.Village,
            question = "Supper tonight: one big pot for everyone, or a basket for each family?",
            shortAsk = "What supper is tonight.",
            letDone = "They had {n} for supper.",
            options = new[]
            {
                new DilemmaOptionDef { label = "one big pot", hint = "Everyone eats together at the fire.", done = "The Keeper chose one big pot for supper.",
                    prop = ConsequenceKind.StewPot, propAt = PropAnchor.Fire },
                new DilemmaOptionDef { label = "a basket each", hint = "Each family eats at home, quietly.", done = "The Keeper chose a basket each for supper.",
                    prop = ConsequenceKind.Baskets, propAt = PropAnchor.Fire },
            },
        };

        public static readonly DilemmaDef[] All = { NamingDef, StoryDef, SupperDef };
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

        /// <summary>#589: how supper goes on a view day (<see cref="SupperPick"/>): the evening the choice ended and the one after.
        /// <see cref="SupperPick.None"/> on every other day.</summary>
        public int SupperOn(long day) => State.supperPick != SupperPick.None && (day == State.supperMonth || day == State.supperMonth + 1) ? State.supperPick : SupperPick.None;

        /// <summary>Debug (#589): settle supper now, as if the Keeper had picked.</summary>
        public void DebugSupper(int pick) { State.supperPick = pick; State.supperMonth = State.month; }

        string SmallDefId(string kind) => SmallCatalog.FirstOrDefault(x => x.smallKind == kind)?.id ?? "";

        /// <summary>Why this small def can't be raised now (null = it can); a and b are the people its options point at.</summary>
        string SmallWhyNot(DilemmaDef def, out int a, out int b)
        {
            a = b = -1;
            if (LifeChoices.Is(def)) return LifeWhyNot(def); // #437/#438
            if (def.smallKind == SmallChoices.Naming)
            {
                var c = NamingNewborn();
                if (c == null) return "no notable newborn is waiting for a name";
                var p = Alive(c.parentId); var q = Alive(p.partnerId);
                a = p.id; b = q.id;
                return null;
            }
            if (def.smallKind != SmallChoices.Story && def.smallKind != SmallChoices.Supper) return "unknown small choice";
            // t4-fixes: the everyday kinds. Never the same kind twice in a row among the small ones, and at most RepeatMax in a box.
            if (State.era < def.minEra || State.era > def.maxEra) return "not in this era";
            var lastSmall = State.dilemmas.LastOrDefault(x => x.size == DilemmaSize.Small);
            if (lastSmall != null && lastSmall.defId == def.id) return "asked last time";
            if (State.dilemmas.Count(x => x.defId == def.id) >= SmallChoices.RepeatMax) return "asked enough in this box";
            if (def.smallKind == SmallChoices.Supper) return null;
            return StoryPair(def, out a, out b) ? null : "fewer than two adults";
        }

        /// <summary>t4-fixes: the story's two adults, by a hash of the next id, a pair not asked about before while there are others.</summary>
        bool StoryPair(DilemmaDef def, out int a, out int b)
        {
            a = b = -1;
            var told = new HashSet<int>(State.dilemmas.Where(x => x.defId == def.id).SelectMany(x => new[] { x.personA, x.personB }));
            var adults = State.notables.Where(n => n.alive && n.age >= AdultAge).ToList();
            var fresh = adults.Where(n => !told.Contains(n.id)).ToList();
            var pool = fresh.Count >= 2 ? fresh : adults;
            if (pool.Count < 2) return false;
            var ranked = pool.OrderByDescending(n => DilemmaRoll(123, n.id, State.dilemmaNextId)).ThenBy(n => n.id).ToList();
            a = ranked[0].id; b = ranked[1].id;
            return true;
        }

        /// <summary>Debug (t4-fixes): raise the story or the supper small choice now (the open choice lapses, the open wish closes; the
        /// repeat rules are lifted). Null if it can't be cast (fewer than two adults for the story).</summary>
        public Dilemma DebugRaiseSmall(string kind)
        {
            var def = SmallCatalog.FirstOrDefault(x => x.smallKind == kind) ?? SmallChoices.All.FirstOrDefault(x => x.smallKind == kind);
            if (def == null) return null;
            int a = -1, b = -1;
            if (kind == SmallChoices.Story && !StoryPair(def, out a, out b)) return null;
            var open = OpenDilemma;
            if (open != null) LapseDilemma(open);
            var w = OpenWish;
            if (w != null) CloseWish(w, WishState.Lapsed);
            return RaiseDilemma(def, a, b);
        }

        List<(DilemmaDef def, int a, int b)> EligibleSmall()
        {
            var list = new List<(DilemmaDef def, int a, int b)>();
            foreach (var def in SmallCatalog)
                if (SmallWhyNot(def, out int a, out int b) == null) list.Add((def, a, b));
            // t4-fixes: a newborn waiting for a name goes first (the baby grows out of it); the everyday kinds fill the other slots.
            int naming = list.FindIndex(x => x.def.smallKind == SmallChoices.Naming);
            if (naming >= 0) return new List<(DilemmaDef def, int a, int b)> { list[naming] };
            // #437/#438: a wolves' or dogs' moment that waits comes next (the first in the catalog's order: thief, pup, kill, pen)
            var life = EligibleLife();
            if (life.Count > 0) return new List<(DilemmaDef def, int a, int b)> { life[0] };
            return list;
        }

        bool NameClash(Notable child, string given) =>
            given == child.name || State.notables.Any(o => o != child && (o.alive && o.name == given || o.name == given && o.family == child.family));

        /// <summary>Fill the card's words once the question is raised: the two names, from hashes (no dice), distinct from each other,
        /// from every living person's name and from the baby's own.</summary>
        void PrepareSmall(Dilemma d, DilemmaDef def)
        {
            if (LifeChoices.Is(def)) { PrepareLife(d, def); return; } // #437/#438
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
            if (LifeChoices.Is(def)) { ApplyLife(d, def, option); return; } // #437/#438
            if (def.smallKind == SmallChoices.Supper)
            {
                // #589: the pick is played out that evening (and the next): one pot at the fire, or a basket each at home.
                State.supperPick = option == 0 ? SupperPick.Pot : SupperPick.Baskets;
                State.supperMonth = State.month;
                return;
            }
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
            var def = DilemmaDefOf(d);
            if (LifeChoices.Is(def)) return LifeDefaultOption(d, def); // #437/#438: the chief's trait
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
