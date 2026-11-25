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
        /// <summary>2.2 the singer's song (a card: its scene is not built yet) and 5.1 the Keeper's child comes of age (once per person).</summary>
        public const string SingerSong = "singer_song", KeeperChildGrown = "keeper_child_grown";
        /// <summary>Batch 3 (B80.b-h): 1.4 the antlers passed, 2.1 the tinkerer's bucket, 2.3 the gardener's flowers, 2.4 the joker's face,
        /// 3.2 the dry spell, 3.3 the first snow (its line keeps the old kind "snow": the weather reads it).</summary>
        public const string AntlersPassed = "antlers_passed", TinkererBucket = "tinkerer_bucket", GardenerFlowers = "gardener_flowers",
            JokerFace = "joker_face", DrySpell = "dry_spell", FirstSnow = "first_snow";
        public static readonly string[] All = { FirstFruit, TwoMarks, SecondStone, SingerSong, KeeperChildGrown,
            AntlersPassed, TinkererBucket, GardenerFlowers, JokerFace, DrySpell, FirstSnow };
        /// <summary>The events told once a box: the ones the yearly pacing counts (a person's callback, 5.1, is not one).</summary>
        public static readonly string[] OncePerBox = { FirstFruit, TwoMarks, SecondStone, SingerSong,
            AntlersPassed, TinkererBucket, GardenerFlowers, JokerFace, DrySpell, FirstSnow };

        /// <summary>The event has a staged scene of its own (a held <see cref="MomentKind.Story"/> moment). 2.2 rides the ritual's own held
        /// moment (B80.d: the singer's notes first, its line on the ritual's caption), so it is never a Story moment of its own.</summary>
        public static bool Staged(string id) => id != null && id != SingerSong && Array.IndexOf(All, id) >= 0;

        /// <summary>The old first-snow line's kind (SimCore.Story.cs), kept: LifePlanner reads it for the snow day.</summary>
        public const string SnowKind = "snow";

        /// <summary>The chronicle kind of an event's line: "story_first_fruit" (3.3: "snow"). Its moment is <see cref="MomentKind.Story"/>.</summary>
        public static string Kind(string id) => id == FirstSnow ? SnowKind : "story_" + id;

        /// <summary>The event a chronicle kind tells ("story_two_marks" → "two_marks", "snow" → "first_snow"), or null.</summary>
        public static string OfKind(string kind)
        {
            if (kind == SnowKind) return FirstSnow;
            if (kind == null || !kind.StartsWith("story_")) return null;
            string id = kind.Substring(6);
            return id != FirstSnow && Array.IndexOf(All, id) >= 0 ? id : null;
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
        /// <summary>4.1 / 4.2: the one the Keeper picked to carry the offering / set the first log (-1: none). They lead it every year while
        /// they live (<see cref="SimCore.RitualLead"/>).</summary>
        public int offeringCarrierId = -1, firstLogId = -1;
        /// <summary>2.2: the singer's song: the ritual it was made for ("" none), its maker (-1 none) and, from 4.3, its name ("" not yet).</summary>
        public string songRitual = "", songName = "";
        public int songOwnerId = -1;
        /// <summary>4.4: the litter's pup that stayed while two kids had no dog (-1 none), the two kids (the first is today's rule's keeper),
        /// and the pup the ask was raised for (-1 none: asked once a pup).</summary>
        public int pupDogId = -1, pupKidA = -1, pupKidB = -1, pupAskedDog = -1;
        /// <summary>5.2: the wedding lines told as "named by the Keeper" (their chronicle ids): the wedding moment says them on its caption.</summary>
        public List<int> keeperWeddings = new();
        /// <summary>2.2 (B80.d): the song's line and the ritual line it rides on (their chronicle ids; -1 none): the ritual's held moment says it.</summary>
        public int songLineId = -1, songRideId = -1;
        /// <summary>1.4: whose door the antlers hang at (-1: they follow the leader, as before the first pass) and whose post stands empty.</summary>
        public int antlersAtId = -1, antlersFromId = -1;
        /// <summary>2.1: the tinkerer who made the crooked bucket (-1 none). They carry it; after them their eldest child.</summary>
        public int bucketOwnerId = -1;
        /// <summary>2.3: the stone the flower ring is round (a consequence prop's id; -1 none) and the gardener.</summary>
        public int flowersStoneId = -1, gardenerId = -1;
        /// <summary>2.4: the woodpile with the face log (a consequence prop's id; -1 none) and the joker.</summary>
        public int faceLogPropId = -1, jokerId = -1;
        /// <summary>3.2: who carries water to the Keeper's tree in a dry spell (-1 none); after them their eldest child.</summary>
        public int treeWatererId = -1;
        /// <summary>3.3: the month the first snow came (-1 never): its snow figure stands by the fire until the thaw.</summary>
        public long snowFigureMonth = -1;
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
            StoryStep3(); // batch 3: the trait moments (2.1, 2.3, 2.4) and the dry spell (3.2); 1.4 follows the seat, 3.3 the snow
        }

        /// <summary>A first-time story line was written this game year.</summary>
        bool StoryFirstThisYear() => KindThisYear(StoryEvent.OncePerBox.Select(StoryEvent.Kind).ToArray());

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
                case StoryEvent.SingerSong: // #722 (2.2): a singer and a kept ritual, and the ritual played now (the song is its card)
                {
                    if (!DebugSingerReady(out string ritual, out why)) break;
                    int before = State.chronicle.Count;
                    DebugPlayRitual(ritual);
                    e = State.chronicle.Skip(before).LastOrDefault(x => x.kind == StoryEvent.Kind(StoryEvent.SingerSong));
                    if (e == null) why = "the song wasn't made";
                    break;
                }
                case StoryEvent.KeeperChildGrown: // #722 (5.1)
                    e = DebugKeeperChildGrown(out why);
                    break;
                case StoryEvent.AntlersPassed: case StoryEvent.TinkererBucket: case StoryEvent.GardenerFlowers:
                case StoryEvent.JokerFace: case StoryEvent.DrySpell: case StoryEvent.FirstSnow:
                    e = DebugStory3(id, out why); // batch 3 (SimCore.StoryEvents3.cs)
                    break;
                default: why = "unknown event " + id; break;
            }
            return e;
        }
    }
}
