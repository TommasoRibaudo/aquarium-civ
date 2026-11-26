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

        /// <summary>4.3 over 2.2 (the shot test's failure: the song's line overrode the name): once the Keeper names the song, the ritual's
        /// caption has the name, the first year's moment too.</summary>
        [Test]
        public void OnceTheSongIsNamed_TheNameIsOnTheCaption_TheFirstYearsMomentToo()
        {
            var sim = Camp(42);
            Assert.IsTrue(sim.DebugSingerReady(out string ritual, out string why), why);
            var ritualLine = sim.DebugPlayRitual(ritual);
            Assert.IsNotNull(sim.SongLineOn(ritualLine.id), "unnamed: the first year's caption is the song's line");
            var d = sim.DebugRaiseStoryAsk(StoryChoices.SongName, out why);
            Assert.IsNotNull(d, why);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card));
            string name = StoryChoices.SongNames(ritual).a;
            Assert.AreEqual(name, sim.StoryMarks.songName);
            Assert.IsNull(sim.SongLineOn(ritualLine.id), "named: the name wins over the song's line");
            Assert.AreEqual($"{RitualKind.Caption(ritual)}: {name}", sim.RitualCaption(ritual));
            Assert.IsNull(sim.SongLineOn(sim.DebugPlayRitual(ritual).id));
        }

        // ---------- the cheats: a scene that can play now ----------

        /// <summary>The view's cast filter (<see cref="SimCore.DebugCastable"/>): each cheat's pick is someone the view can cast now.</summary>
        [Test]
        public void TheCheats_PickPeopleTheViewCanCastNow()
        {
            var sim = Camp(43);
            sim.DebugAddKids(3, 8f);
            var kids = sim.State.notables.Where(n => n.alive && n.age >= 4f && n.age < SimCore.AdultAge).OrderBy(n => n.id).ToList();
            var grown = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge && n.age < 50f).OrderBy(n => n.id).ToList();
            var kid = kids[^1]; var adult = grown[^1];
            sim.DebugCastable = n => n == kid || n == adult;
            Assert.AreEqual(kid.id, sim.DebugStory(StoryEvent.FirstSnow, out string why)?.notableId, why);
            Assert.AreEqual(kid.id, sim.DebugStory(StoryEvent.FirstFruit, out why)?.notableId ?? kid.id, why); // (the tree may not be grown: no line)
            foreach (var id in new[] { StoryEvent.DrySpell, StoryEvent.JokerFace })
            {
                var e = sim.DebugStory(id, out why);
                if (e == null) continue; // its thing isn't in this box
                Assert.AreEqual(adult.id, e.notableId, id + ": the one the view can cast");
            }
            sim.DebugCastable = null;
        }

        /// <summary>#784: the cheats that need a thing from an earlier choice stand it, from a box where that choice was never made: "Stone: fill
        /// it" stands the name stone with a death after it; "Trait: the joker's face" builds the woodpile's roof; the gardener's cheat the stone.</summary>
        [Test]
        public void TheCheats_StandTheNameStoneWithADeath_AndTheWoodpilesRoof_InABoxWithNeither()
        {
            var sim = Camp(61, 4);
            Assert.IsNull(sim.FirstNameStone, "no name stone in this box");
            Assert.IsNull(sim.WoodpileProp, "no woodpile roof in this box");
            var second = sim.DebugStory(StoryEvent.SecondStone, out string why);
            Assert.IsNotNull(second, "the second stone: " + why);
            Assert.IsNotNull(sim.FirstNameStone, "the first stone stands now");
            Assert.IsTrue(sim.FirstNameStone.placed);
            Assert.GreaterOrEqual(sim.NamesSince(sim.FirstNameStone).Count, 1, "with a death after it");
            Assert.GreaterOrEqual(sim.StoryMarks.secondStoneId, 0, "and the second beside it");
            var face = sim.DebugStory(StoryEvent.JokerFace, out why);
            Assert.IsNotNull(face, "the joker's face: " + why);
            Assert.IsNotNull(sim.WoodpileProp, "the woodpile's roof stands now");
            Assert.IsTrue(sim.FaceLogOn, "with the face log on it");
        }

        [Test]
        public void TheGardenersCheat_StandsTheNameStone_InABoxWithoutOne()
        {
            var sim = Camp(62, 4);
            Assert.IsNull(sim.FirstNameStone);
            Assert.IsNotNull(sim.DebugStory(StoryEvent.GardenerFlowers, out string why), why);
            Assert.IsTrue(sim.FlowersAtStone, "the flowers ring the stone it stood");
        }

        [Test]
        public void TheStandingCheats_KeepTheThingsThatStandAlready_AndPlayAgain()
        {
            var sim = Camp(63, 4);
            Assert.IsNotNull(sim.DebugStory(StoryEvent.GardenerFlowers, out string why), why);
            int stones = sim.ConsequenceProps.Count(p => p.kind == ConsequenceKind.NameStone);
            Assert.IsNotNull(sim.DebugStory(StoryEvent.SecondStone, out why), why);
            Assert.AreEqual(stones + 1, sim.ConsequenceProps.Count(p => p.kind == ConsequenceKind.NameStone), "the stone that stood is the first; one more is the second");
            Assert.IsNotNull(sim.DebugStory(StoryEvent.JokerFace, out why), why);
            int roofs = sim.ConsequenceProps.Count(p => p.kind == ConsequenceKind.WoodpileRoof);
            Assert.IsNotNull(sim.DebugStory(StoryEvent.JokerFace, out why), why);
            Assert.AreEqual(roofs, sim.ConsequenceProps.Count(p => p.kind == ConsequenceKind.WoodpileRoof), "the roof is not built twice");
        }

        /// <summary>5.1's cheat on a land that gives no work yet: the child still comes of age with work (they help make the tools), so the
        /// line and its scene happen.</summary>
        [Test]
        public void TheKeepersChildCheat_AlwaysComesOfAgeWithWork()
        {
            var sim = Camp(44);
            sim.DebugAddKids(1, 12f);
            var e = sim.DebugKeeperChildGrown(out string why);
            Assert.IsNotNull(e, why);
            var child = sim.FindNotable(e.notableId);
            Assert.IsFalse(string.IsNullOrEmpty(child.job), "work");
            StringAssert.Contains("came of age and took up work as", e.text);
            Assert.AreEqual(MomentKind.Story, sim.MomentOfEntry(e.id)?.kind);
        }

        /// <summary>1.4's cheat makes its gate true: the antlers go from one door to another, so the old and the new chief live in two homes (a
        /// tent goes up for one if they shared one: "one home: the antlers stay").</summary>
        [Test]
        public void TheAntlersCheat_TheTwoChiefsHaveTwoHomes()
        {
            var sim = Camp(46);
            Assume.That(sim.Settlement, Is.Not.Null, "a settlement");
            sim.DebugCastable = n => true;
            var e = sim.DebugStory(StoryEvent.AntlersPassed, out string why);
            sim.DebugCastable = null;
            Assert.IsNotNull(e, why);
            var nu = sim.FindNotable(e.notableId); var old = sim.FindNotable(e.otherNotableId);
            var a = sim.Settlement.HomeOf(old); var b = sim.Settlement.HomeOf(nu);
            Assert.IsNotNull(a, "the old chief has a home"); Assert.IsNotNull(b, "the new chief has a home");
            Assert.AreNotSame(a, b, "two homes, two doors");
        }

        /// <summary>The cheats' people are counted: the box's header says as many people as are drawn.</summary>
        [Test]
        public void PeopleACheatAdds_AreCounted()
        {
            var sim = Camp(47, 0);
            int before = sim.State.population;
            Assert.AreEqual(3, sim.DebugAddKids(3, 8f));
            Assert.AreEqual(before + 3, sim.State.population);
        }

        /// <summary>5.2's cheat makes its gate true: with fewer than two unwed grown-ups a grown-up joins, and the wedding is told.</summary>
        [Test]
        public void TheWeddingCheat_WithEveryoneWed_StillMarries()
        {
            var sim = Camp(45, 0);
            for (int i = 0; i < 8 && sim.State.notables.Count(n => n.alive && n.age >= SimCore.AdultAge && !(sim.FindNotable(n.partnerId)?.alive ?? false)) >= 2; i++)
                Assert.IsNotNull(sim.DebugKeeperChildWed(out _));
            var e = sim.DebugKeeperChildWed(out string why);
            Assert.IsNotNull(e, why);
            StringAssert.Contains("named by the Keeper, married", e.text);
            Assert.AreEqual(MomentKind.Wedding, sim.MomentOfEntry(e.id)?.kind);
        }
    }
}
