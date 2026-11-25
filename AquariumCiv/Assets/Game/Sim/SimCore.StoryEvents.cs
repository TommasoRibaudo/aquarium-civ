using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>#722: the events that replace the flavour layer (docs/design/new-events.md, signed off by the user 2026-11-25; 3.1 and 1.5
    /// cut). Each names someone, leaves something in the box, and is told once.</summary>
    public static class StoryEvent
    {
        public const string FirstFruit = "first_fruit", TwoMarks = "two_marks", SecondStone = "second_stone";
        public static readonly string[] All = { FirstFruit, TwoMarks, SecondStone };

        /// <summary>The chronicle kind of an event's line: "story_first_fruit". Its moment is <see cref="MomentKind.Story"/>.</summary>
        public static string Kind(string id) => "story_" + id;

        /// <summary>The event a chronicle kind tells ("story_two_marks" → "two_marks"), or null.</summary>
        public static string OfKind(string kind)
        {
            if (kind == null || !kind.StartsWith("story_")) return null;
            string id = kind.Substring(6);
            return Array.IndexOf(All, id) >= 0 ? id : null;
        }
    }

    /// <summary>#722: what the events left in the box, saved with it (new fields read as empty from an old save).</summary>
    [Serializable]
    public class StoryMarks
    {
        /// <summary>1.1: the game year the Keeper's tree first fruited (-1: never). From then it fruits every summer.</summary>
        public long fruitYear = -1;
        /// <summary>1.2: the couples' marks on the Keeper's tree, in order (a, b ids), at most <see cref="SimCore.MaxMarkPairs"/>.</summary>
        public List<int> markA = new(), markB = new();
        /// <summary>1.3: the second name stone (a consequence prop's id; -1: none yet) and the first name cut on it.</summary>
        public int secondStoneId = -1, secondStoneFirst = -1;
    }

    /// <summary>
    /// #722: the story events. The sim writes each event's line (kind "story_&lt;id&gt;") the month it happens, once per box (a flag in
    /// <see cref="SimState.storyFlags"/>, so a pruned History never makes it new again), and keeps what stays in <see cref="SimState.storyMarks"/>.
    /// The line queues a held moment (<see cref="MomentKind.Story"/>): watched, the view plays the scene and says the line on the caption;
    /// otherwise it is a card. At most one new event a game year from <see cref="StoryStep"/>; the marks follow their weddings.
    /// No dice: picks come from the year and the ids. All text is [est] until the user signs it off.
    /// </summary>
    public partial class SimCore
    {
        public const int MaxMarkPairs = 8;
        /// <summary>1.3: the names one stone holds (the spec's nine).</summary>
        public const int StoneNames = 9;

        public StoryMarks StoryMarks => State.storyMarks ??= new StoryMarks();

        static string StoryFlag(string id) => "story:" + id;
        public bool StoryHappened(string id) => State.storyFlags.Contains(StoryFlag(id));

        /// <summary>The event a held moment's line tells, or null.</summary>
        public string StoryOfEntry(int entryId)
        {
            for (int i = State.chronicle.Count - 1; i >= 0; i--)
                if (State.chronicle[i].id == entryId) return StoryEvent.OfKind(State.chronicle[i].kind);
            return null;
        }

        /// <summary>Every month (from the dilemma step, as the rituals): at most one new event a game year, in the spec's order.</summary>
        void StoryStep()
        {
            if (State.notables == null) return;
            if (StoryFirstThisYear()) return;
            if (TryFirstFruit(false) != null) return;
            if (TrySecondStone(false) != null) return;
        }

        /// <summary>A first-time story line was written this game year.</summary>
        bool StoryFirstThisYear() => KindThisYear(StoryEvent.All.Select(StoryEvent.Kind).ToArray());

        ChronicleEntry StoryLine(string id, string text, float salience, int a, int b)
        {
            State.storyFlags.Add(StoryFlag(id));
            return Record(StoryEvent.Kind(id), text, salience, a, b);
        }

        bool Summer => CalendarMonth >= 5 && CalendarMonth <= 7;

        // ---------- 1.1 the tree's first fruit ----------

        /// <summary>The kid who picks it: a free kid, by the year (the view casts the nearest free kid; this one is named).</summary>
        Notable FruitKid()
        {
            var kids = State.notables.Where(n => n.alive && n.age >= 4f && n.age < AdultAge).OrderBy(n => n.id).ToList();
            return kids.Count == 0 ? null : kids[(int)(RitualYear % kids.Count)];
        }

        ChronicleEntry TryFirstFruit(bool force)
        {
            if (StoryHappened(StoryEvent.FirstFruit)) return null;
            if (!KeeperTreeGrown || (!force && !Summer)) return null;
            var kid = FruitKid();
            if (kid == null) return null;
            StoryMarks.fruitYear = RitualYear;
            return StoryLine(StoryEvent.FirstFruit, $"The Keeper's tree gave its first fruit. {kid.name} picked it, and everyone had a bite.", 0.7f, kid.id, -1);
        }

        /// <summary>1.1: the tree has fruit on it now (every summer after the first fruit).</summary>
        public bool TreeInFruit => StoryMarks.fruitYear >= 0 && KeeperTreeGrown && Summer;

        // ---------- 1.2 two marks on the tree ----------

        /// <summary>From <see cref="Wedding"/>: with the Keeper's tree grown the couple cut their marks (up to eight pairs). The first couple's
        /// is the event (its scene and line); later couples' marks are History. A couple whose parents' marks are there is told so.</summary>
        void MarksAfterWedding(Notable a, Notable b)
        {
            if (!KeeperTreeGrown || a == null || b == null) return;
            var m = StoryMarks;
            if (m.markA.Count >= MaxMarkPairs) return;
            m.markA.Add(a.id); m.markB.Add(b.id);
            if (!StoryHappened(StoryEvent.TwoMarks))
            {
                StoryLine(StoryEvent.TwoMarks, $"{a.name} and {b.name} cut their marks into the Keeper's tree, side by side.", 0.65f, a.id, b.id);
                return;
            }
            var parents = MarkedParents(a) ?? MarkedParents(b);
            string who = MarkedParents(a) != null ? a.name : b.name;
            Record("story_marks_year", parents != null
                ? $"{who} cut a mark under {parents.Value.pa}'s and {parents.Value.pb}'s."
                : $"{a.name} and {b.name} cut their marks into the Keeper's tree.", 0.3f, a.id, b.id);
        }

        (string pa, string pb)? MarkedParents(Notable n)
        {
            var parent = FindNotable(n.parentId);
            if (parent == null) return null;
            var m = StoryMarks;
            for (int i = 0; i < m.markA.Count; i++)
                if (m.markA[i] == parent.id || m.markB[i] == parent.id)
                    return (FindNotable(m.markA[i])?.name ?? "", FindNotable(m.markB[i])?.name ?? "");
            return null;
        }

        // ---------- 1.3 the second stone ----------

        /// <summary>The first name stone (a choice's thing), or null.</summary>
        public ConsequenceProp FirstNameStone => ConsequenceProps.FirstOrDefault(p => p.kind == ConsequenceKind.NameStone && p.placed && p.id != StoryMarks.secondStoneId);

        /// <summary>The people who died since a stone stood (its names), newest last.</summary>
        public List<Notable> NamesSince(ConsequenceProp stone) =>
            stone == null ? new List<Notable>() : State.notables.Where(n => !n.alive && n.diedMonth > stone.dueMonth).OrderBy(n => n.diedMonth).ThenBy(n => n.id).ToList();

        ChronicleEntry TrySecondStone(bool force)
        {
            if (StoryHappened(StoryEvent.SecondStone) || StoryMarks.secondStoneId >= 0) return null;
            var first = FirstNameStone;
            if (first == null) return null;
            var names = NamesSince(first);
            if (!force && names.Count <= StoneNames) return null; // the tenth name is due
            var dead = names.Count > 0 ? names[^1] : null;
            if (dead == null) return null;
            var who = State.notables.Where(n => n.alive && n.age >= AdultAge && n.age < 50f).OrderBy(n => Math.Abs(n.age - 28f)).ThenBy(n => n.id).FirstOrDefault()
                      ?? State.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.id).FirstOrDefault();
            if (who == null) return null;
            var list = State.consequenceProps ??= new List<ConsequenceProp>();
            // beside the first stone, on the side away from the pond's middle [est]
            float side = first.x >= 0f ? 1f : -1f;
            var p = new ConsequenceProp
            {
                id = list.Count == 0 ? 1 : list.Max(x => x.id) + 1, dilemmaId = 991000, option = 0, kind = ConsequenceKind.NameStone,
                anchor = first.anchor, placed = true, x = first.x + side * 0.24f, z = first.z + 0.04f, yaw = first.yaw + 12f,
                dueMonth = State.month, placedMonth = State.month, personId = dead.id,
            };
            list.Add(p);
            StoryMarks.secondStoneId = p.id; StoryMarks.secondStoneFirst = dead.id;
            return StoryLine(StoryEvent.SecondStone, $"The name stone was full. {who.name} rolled a second one beside it, and {dead.name}'s name went on first.", 0.75f, who.id, dead.id);
        }

        // ---------- debug (SimDebugPanel's "Stories (#722)" block) ----------

        /// <summary>Debug: the event happens now, its gate eased where a cheat must (the season, the stone's count). The line, or null and why.</summary>
        public ChronicleEntry DebugStory(string id, out string why)
        {
            why = "";
            State.storyFlags.Remove(StoryFlag(id));
            ChronicleEntry e = null;
            switch (id)
            {
                case StoryEvent.FirstFruit:
                    e = TryFirstFruit(true);
                    if (e == null) why = !KeeperTreeGrown ? "the Keeper's tree isn't grown ('Grow the Keeper's tree' first)" : "no kid in the box";
                    break;
                case StoryEvent.TwoMarks:
                {
                    if (!KeeperTreeGrown) { why = "the Keeper's tree isn't grown"; break; }
                    var adults = State.notables.Where(n => n.alive && n.age >= AdultAge).OrderBy(n => n.id).ToList();
                    var a = adults.FirstOrDefault(n => Alive(n.partnerId) != null);
                    var b = a != null ? Alive(a.partnerId) : null;
                    if (a == null) { a = adults.ElementAtOrDefault(0); b = adults.ElementAtOrDefault(1); }
                    if (a == null || b == null) { why = "two grown-ups needed"; break; }
                    MarksAfterWedding(a, b);
                    e = State.chronicle.LastOrDefault(x => x.kind == StoryEvent.Kind(StoryEvent.TwoMarks) || x.kind == "story_marks_year");
                    break;
                }
                case StoryEvent.SecondStone:
                    if (StoryMarks.secondStoneId >= 0) { why = "the second stone already stands"; break; }
                    if (FirstNameStone == null) { why = "no name stone stands (answer 'camp_remember' with the stone first)"; break; }
                    if (NamesSince(FirstNameStone).Count == 0) { why = "nobody has died since the stone stood"; break; }
                    e = TrySecondStone(true);
                    if (e == null) why = "no grown-up to roll it";
                    break;
                default: why = "unknown event " + id; break;
            }
            return e;
        }
    }
}
