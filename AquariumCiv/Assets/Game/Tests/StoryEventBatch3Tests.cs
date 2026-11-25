using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #722 batch 3 (docs/design/new-events.md §1.4, §2.1-2.4, §3.2, §3.3; animation-specs B80.b-h): the last story events' sim side. Each is told
    /// once a box, gated on what it talks about, names its person and queues a Story moment; what it leaves is kept (the antlers' door, the
    /// crooked bucket's carrier, the flower ring, the face log, the tree's waterer and their child, the snow figure until the thaw); at most one
    /// new event a game year; the singer's song rides its ritual's moment. Pure C#.
    /// </summary>
    public class StoryEventBatch3Tests
    {
        static readonly string[] EventTraits = { "the tinkerer", "the gardener", "the joker", "the singer" };

        /// <summary>A lived-in camp with the trait events' traits taken away (each test gives the one it needs).</summary>
        static SimCore Camp(int seed, int adults = 4)
        {
            var sim = NewSim(seed, new SimConfig { dilemmas = true, moments = true, ecology = true, historyPruneYears = 0 });
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Assume.That(sim.EcologyOn, "the ecology is on");
            if (adults > 0) sim.DebugAddKids(adults, 30f);
            NoTraits(sim);
            return sim;
        }

        static void NoTraits(SimCore sim) { foreach (var n in sim.State.notables) if (EventTraits.Contains(n.trait)) n.trait = "the kind"; }

        static void Months(SimCore sim, int n) { for (int i = 0; i < n; i++) sim.Advance(1.0 / 12); }

        static ChronicleEntry[] Lines(SimCore sim, string id) => sim.State.chronicle.Where(e => e.kind == StoryEvent.Kind(id)).ToArray();

        static Notable Adult(SimCore sim, int i = 0) => sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge && n.age < 45f).OrderBy(n => n.id).ElementAt(i);

        static void AssertStoryMoment(SimCore sim, ChronicleEntry e, string id)
        {
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m, id + " queues a held moment");
            Assert.AreEqual(MomentKind.Story, m.kind);
            Assert.AreEqual(e.notableId, m.personA);
            Assert.AreEqual(id, sim.StoryOfEntry(e.id), "the view can tell which event it is");
        }

        static void Kill(SimCore sim, Notable n) { n.alive = false; n.diedMonth = sim.State.month; }

        // ---------- 1.4 the antlers passed ----------

        [Test]
        public void TheAntlers_PassWithTheSeat_TheLineAndTheDoors_ThenLaterPassesAreSilent()
        {
            var sim = Camp(31);
            Assert.IsNotNull(sim.DebugStory(StoryEvent.AntlersPassed, out string why), "the cheat: the antlers go up and pass: " + why);
            var line = Lines(sim, StoryEvent.AntlersPassed).Single();
            var nu = sim.FindNotable(line.notableId); var old = sim.FindNotable(line.otherNotableId);
            Assert.AreEqual($"The antlers on the post went to {nu.name}, with the seat.", line.text);
            Assert.AreEqual(nu.id, sim.SeatOf(SimCore.LeaderOf(sim.State.era)).holderId, "the new one holds the seat");
            Assert.AreEqual(nu.id, sim.StoryMarks.antlersAtId, "the antlers at the new door");
            Assert.AreEqual(old.id, sim.StoryMarks.antlersFromId, "the old post stays empty");
            Assert.AreSame(nu, sim.AntlersDoor);
            AssertStoryMoment(sim, line, StoryEvent.AntlersPassed);
            Assert.AreEqual(old.id, sim.MomentOfEntry(line.id).personB, "the old one carries them");
            // a later pass (the new one dies; the seat fills by its rule): the antlers follow, no new line
            Kill(sim, nu);
            for (int i = 0; i < 14 && (sim.SeatOf(SimCore.LeaderOf(sim.State.era)).holderId < 0 || sim.SeatOf(SimCore.LeaderOf(sim.State.era)).holderId == nu.id); i++) Months(sim, 1);
            int next = sim.SeatOf(SimCore.LeaderOf(sim.State.era)).holderId;
            Assume.That(next, Is.GreaterThanOrEqualTo(0), "a new leader took the seat");
            Assert.AreEqual(next, sim.StoryMarks.antlersAtId, "they went to the next door");
            Assert.AreEqual(nu.id, sim.StoryMarks.antlersFromId);
            Assert.AreEqual(1, Lines(sim, StoryEvent.AntlersPassed).Length, "told once");
        }

        [Test]
        public void TheAntlers_InPlay_AfterTheNewLeadersMoment_AndNeverWithoutAntlers()
        {
            foreach (bool antlers in new[] { false, true })
            {
                var sim = Camp(32);
                if (antlers) sim.State.eco.antlersMonth = sim.State.month;
                var seat = sim.SeatOf(SimCore.LeaderOf(sim.State.era));
                Assume.That(seat != null && seat.holderId >= 0, "a leader sits");
                var chief = sim.FindNotable(seat.holderId);
                Kill(sim, chief);
                for (int i = 0; i < 14 && (seat.holderId < 0 || seat.holderId == chief.id); i++) Months(sim, 1);
                Assume.That(seat.holderId, Is.GreaterThanOrEqualTo(0), "the seat filled");
                var lines = Lines(sim, StoryEvent.AntlersPassed);
                if (!antlers) { Assert.IsEmpty(lines, "no antlers: nothing passes"); Assert.AreEqual(-1, sim.StoryMarks.antlersAtId); continue; }
                Assume.That(lines.Length, Is.EqualTo(1), "the year's slot was free");
                Assert.AreEqual(seat.holderId, lines[0].notableId);
                Assert.AreEqual(chief.id, lines[0].otherNotableId, "from the old chief's door (they died: their family carry)");
                var story = sim.MomentOfEntry(lines[0].id);
                var leader = sim.State.moments.Where(m => m.kind == MomentKind.NewLeader && m.personA == seat.holderId).OrderBy(m => m.id).FirstOrDefault();
                if (leader != null) Assert.Less(leader.id, story.id, "queued right after the new leader's moment");
            }
        }

        // ---------- 2.1 the tinkerer's bucket ----------

        [Test]
        public void TheTinkerersBucket_OnceABox_TheFirstGrownTinkerer_TheyCarryIt_ThenTheirChild()
        {
            var sim = Camp(33);
            sim.DebugSetTable(sim.EcoFloodLine + 0.05f); sim.DebugSkipSeasons(1); // the pond fills
            Assume.That(sim.PondFetched, "people fetch water from the pond");
            Months(sim, 14);
            Assert.IsEmpty(Lines(sim, StoryEvent.TinkererBucket), "no tinkerer: no bucket");
            Assert.AreEqual(-1, sim.SpoutBucketUserId);
            var t = Adult(sim, 1); t.trait = "the tinkerer";
            Adult(sim, 2).trait = "the tinkerer";
            Months(sim, 30);
            var line = Lines(sim, StoryEvent.TinkererBucket).Single();
            var first = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge && n.trait == "the tinkerer").OrderBy(n => n.id).First();
            Assert.AreEqual(first.id, line.notableId, "the first grown-up with the trait");
            Assert.AreEqual($"{first.name} made a bucket with a spout. It poured to the left, so everyone stood to the right.", line.text);
            AssertStoryMoment(sim, line, StoryEvent.TinkererBucket);
            Assert.AreEqual(first.id, sim.SpoutBucketUserId, "they carry the crooked bucket");
            // after them, their eldest grown-up child
            int before = sim.State.notables.Count;
            sim.DebugAddKids(2, 25f);
            var kids = sim.State.notables.Skip(before).ToList();
            Assert.AreEqual(2, kids.Count, "two grown children");
            foreach (var n in sim.State.notables) if (n.parentId == first.id) n.parentId = -1;
            kids[0].parentId = first.id; kids[1].parentId = first.id; kids[0].age = 25f; kids[1].age = 27f;
            Kill(sim, first);
            Assert.AreEqual(kids[1].id, sim.SpoutBucketUserId, "the eldest child carries it");
        }

        [Test]
        public void TheTinkerersBucket_NeedsOpenWater()
        {
            var sim = Camp(34);
            Adult(sim).trait = "the tinkerer";
            if (sim.PondFetched) Assert.Pass("this box has its pond: the gate is open (covered by the test above)");
            Months(sim, 14);
            Assert.IsEmpty(Lines(sim, StoryEvent.TinkererBucket));
        }

        // ---------- 2.3 the gardener's flowers ----------

        [Test]
        public void TheGardenersFlowers_OnlyAtTheNameStone_TheRingStays()
        {
            var sim = Camp(35);
            Adult(sim).trait = "the gardener";
            Months(sim, 14);
            Assert.IsEmpty(Lines(sim, StoryEvent.GardenerFlowers), "no name stone: no flowers");
            Assert.IsFalse(sim.FlowersAtStone);
            sim.DebugPlaceEveryProp();
            NoTraits(sim); Adult(sim).trait = "the gardener";
            var stone = sim.FirstNameStone;
            Assert.IsNotNull(stone, "the name stone stands");
            Months(sim, 26);
            var line = Lines(sim, StoryEvent.GardenerFlowers).Single();
            var g = sim.FindNotable(line.notableId);
            Assert.AreEqual("the gardener", g.trait);
            Assert.AreEqual($"{g.name} planted flowers round the name stone, and someone watered them every morning.", line.text);
            AssertStoryMoment(sim, line, StoryEvent.GardenerFlowers);
            Assert.AreEqual(stone.id, sim.StoryMarks.flowersStoneId);
            Assert.IsTrue(sim.FlowersAtStone, "the ring stays at the stone");
        }

        // ---------- 2.4 the joker's face ----------

        [Test]
        public void TheJokersFace_OnlyWithTheWoodpile_TheFaceLogStays()
        {
            var sim = Camp(36);
            Adult(sim).trait = "the joker";
            Months(sim, 14);
            if (sim.WoodpileProp == null) Assert.IsEmpty(Lines(sim, StoryEvent.JokerFace), "no woodpile: no face");
            sim.DebugPlaceEveryProp();
            if (!sim.WoodpileStands) sim.DebugFirstWildWoodNow();
            NoTraits(sim); Adult(sim).trait = "the joker";
            var pile = sim.WoodpileProp;
            Assert.IsNotNull(pile, "the woodpile stands");
            Months(sim, 26);
            var line = Lines(sim, StoryEvent.JokerFace).Single();
            var j = sim.FindNotable(line.notableId);
            Assert.AreEqual($"{j.name} carved a grinning face into a log on the woodpile. Nobody would burn it.", line.text);
            AssertStoryMoment(sim, line, StoryEvent.JokerFace);
            Assert.AreEqual(pile.id, sim.StoryMarks.faceLogPropId);
            Assert.IsTrue(sim.FaceLogOn);
        }

        // ---------- pacing ----------

        [Test]
        public void TheTraitEvents_AtMostOneNewEventAGameYear()
        {
            var sim = Camp(37);
            sim.DebugPlaceEveryProp();
            if (!sim.WoodpileStands) sim.DebugFirstWildWoodNow();
            NoTraits(sim);
            Adult(sim, 0).trait = "the tinkerer"; Adult(sim, 1).trait = "the gardener"; Adult(sim, 2).trait = "the joker";
            Months(sim, 5 * SimConfig.MonthsPerYear);
            var kinds = StoryEvent.OncePerBox.Select(StoryEvent.Kind).ToHashSet();
            var firsts = sim.State.chronicle.Where(e => kinds.Contains(e.kind)).ToList();
            foreach (var g in firsts.GroupBy(e => e.month / SimConfig.MonthsPerYear)) Assert.AreEqual(1, g.Count(), $"year {g.Key}: {string.Join(" | ", g.Select(e => e.kind))}");
            foreach (var id in new[] { StoryEvent.TinkererBucket, StoryEvent.GardenerFlowers, StoryEvent.JokerFace })
                Assert.LessOrEqual(Lines(sim, id).Length, 1, id + " once");
        }

        // ---------- 3.2 the dry spell ----------

        [Test]
        public void TheDrySpell_NamesTheWaterer_TheCardSaysSo_AfterThemTheirChild()
        {
            var sim = Camp(38);
            if (!sim.KeeperTreePlanted) { Assert.IsNull(sim.DebugStory(StoryEvent.DrySpell, out string no), "no tree"); StringAssert.Contains("tree", no); }
            Assert.IsTrue(sim.DebugTreeStage(2));
            var e = sim.DebugStory(StoryEvent.DrySpell, out string why);
            Assert.IsNotNull(e, why);
            Assert.IsTrue(sim.InDrySpell, "the grass is brown (the cheat's dry spell)");
            var who = sim.FindNotable(e.notableId);
            Assert.AreEqual($"The grass went brown in the dry spell. {who.name} carried water to the Keeper's tree every evening.", e.text);
            AssertStoryMoment(sim, e, StoryEvent.DrySpell);
            Assert.AreSame(who, sim.TreeWaterer);
            StringAssert.Contains("Waters the Keeper's tree in a dry spell.", sim.KeeperHandLine(who), "their card says so");
            sim.DebugAddKids(1, 22f);
            var child = sim.State.notables.Where(n => n.alive && n.age >= 22f && n.age < 23f).OrderByDescending(n => n.id).First();
            foreach (var n in sim.State.notables) if (n.parentId == who.id) n.parentId = -1;
            child.parentId = who.id;
            Kill(sim, who);
            Assert.AreSame(child, sim.TreeWaterer, "after them, their child carries the water");
            StringAssert.Contains("Waters the Keeper's tree in a dry spell.", sim.KeeperHandLine(child));
            StringAssert.Contains("Watered the Keeper's tree", sim.KeeperHandLine(who));
        }

        [Test]
        public void TheDrySpell_ChangesNoSimNumbers()
        {
            var a = Camp(39); var b = Camp(39);
            Assert.IsTrue(a.DebugTreeStage(2)); Assert.IsTrue(b.DebugTreeStage(2));
            a.DebugDrySpell(); b.DebugDrySpell();
            Assert.IsNotNull(a.DebugStory(StoryEvent.DrySpell, out string why), why); // the cheat dries the land again: the same as b's
            b.DebugDrySpell();
            CollectionAssert.AreEqual(b.State.eco.moisture, a.State.eco.moisture, "the look only");
            Assert.AreEqual(b.State.eco.treeStage, a.State.eco.treeStage);
        }

        // ---------- 3.3 the first snow ----------

        [Test]
        public void TheFirstSnow_TheSpecsLineWithAKid_AStoryMoment_TheWeatherStillReadsIt_TheFigureUntilTheThaw()
        {
            var sim = Camp(40);
            sim.DebugAddKids(2, 7f);
            for (int i = 0; i < 13 && sim.CalendarMonth != 11; i++) Months(sim, 1);
            Assume.That(sim.CalendarMonth, Is.EqualTo(11), "the start of winter");
            var e = sim.DebugStory(StoryEvent.FirstSnow, out string why);
            Assert.IsNotNull(e, why);
            Assert.AreEqual("snow", e.kind, "the old kind: LifePlanner's snow day reads it");
            Assert.IsTrue(sim.State.storyFlags.Contains("snow"), "the snow flag the weather reads");
            var kid = sim.FindNotable(e.notableId);
            Assert.Less(kid.age, SimCore.AdultAge);
            Assert.AreEqual($"The first snow came. {kid.name} caught a flake on their tongue, and the others copied.", e.text);
            AssertStoryMoment(sim, e, StoryEvent.FirstSnow);
            Assert.AreEqual(StoryEvent.FirstSnow, StoryEvent.OfKind("snow"));
            Assert.IsTrue(sim.SnowFigureStands, "the snow figure stands");
            for (int i = 0; i < 5 && sim.SnowFigureStands; i++) Months(sim, 1);
            Assert.IsFalse(sim.SnowFigureStands, "gone with the thaw");
        }

        [Test]
        public void TheFirstSnow_InPlay_OnceABox_WithTheSpecsLine()
        {
            var sim = Camp(41);
            sim.DebugAddKids(2, 6f);
            Months(sim, 8 * SimConfig.MonthsPerYear);
            var lines = sim.State.chronicle.Where(x => x.kind == "snow").ToList();
            Assume.That(lines.Count, Is.GreaterThan(0), "it snowed in eight years");
            Assert.AreEqual(1, lines.Count, "the first snow is told once");
            StringAssert.StartsWith("The first snow came. ", lines[0].text);
            if (lines[0].notableId >= 0) StringAssert.EndsWith("caught a flake on their tongue, and the others copied.", lines[0].text);
            else { StringAssert.DoesNotContain("tongue", lines[0].text, "no kid: the plain line"); Assert.AreEqual(HeldMomentState.Carded, sim.MomentOfEntry(lines[0].id).state, "a card"); }
        }

        // ---------- 2.2 the song rides its ritual's moment ----------

        [Test]
        public void TheSingersSong_RidesTheRitualsMoment_ItsLineOnTheCaption_NotACardWhileItWaits()
        {
            var sim = Camp(42);
            Assert.IsTrue(sim.DebugSingerReady(out string ritual, out string why), why);
            var ritualLine = sim.DebugPlayRitual(ritual);
            var song = sim.State.chronicle.Last(x => x.kind == StoryEvent.Kind(StoryEvent.SingerSong));
            Assert.AreEqual(song.text, sim.SongLineOn(ritualLine.id), "the ritual's moment says the song's line");
            Assert.IsNull(sim.SongLineOn(song.id));
            var held = sim.MomentOfEntry(ritualLine.id);
            Assume.That(held, Is.Not.Null, "the ritual is a held moment");
            Assert.IsTrue(sim.MomentHidesCard(song), "while the ritual's moment waits, the song is no card");
            Assert.AreEqual(song.notableId, sim.SongSingerOf(ritual)?.id, "its singer leads it every year");
            Assert.IsNull(sim.SongSingerOf(RitualKind.All.First(k => k != ritual)));
            var next = sim.DebugPlayRitual(ritual);
            Assert.IsNull(sim.SongLineOn(next.id), "the line is said once (its first year)");
        }
    }
}
