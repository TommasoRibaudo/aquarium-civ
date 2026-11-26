using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #722 batch 2 (docs/design/new-events.md §2.2, §4.1-4.4, §5.1, §5.2): the singer's song, the four small owned choices (Keeper's asks on the
    /// small-choice machinery), and the Keeper's child's callbacks. Each ask is eligible only when gated; the pick changes the ritual's lead, the
    /// song's name on the ritual's caption, the pup's keeper and the person card; a once-only ask isn't asked twice; 5.1 / 5.2 only for a child
    /// the Keeper named. Pure C#.
    /// </summary>
    public class StoryEventBatch2Tests
    {
        static SimCore Camp(int seed)
        {
            var sim = NewSim(seed, new SimConfig { dilemmas = true, moments = true, ecology = true, historyPruneYears = 0 });
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Assume.That(sim.EcologyOn, "the ecology is on");
            return sim;
        }

        static void Months(SimCore sim, int n) { for (int i = 0; i < n; i++) sim.Advance(1.0 / 12); }

        /// <summary>Steps month by month until the calendar month is <paramref name="cal"/> (a year at most).</summary>
        static void ToCalendar(SimCore sim, int cal) { for (int i = 0; i < 13 && sim.CalendarMonth != cal; i++) Months(sim, 1); Assume.That(sim.CalendarMonth, Is.EqualTo(cal)); }

        static void Keep(SimCore sim, string ritual) { if (!sim.State.customs.Contains(RitualKind.CustomOf(ritual))) sim.State.customs.Add(RitualKind.CustomOf(ritual)); }

        static Dilemma Raise(SimCore sim, string kind)
        {
            var d = sim.DebugRaiseStoryAsk(kind, out string why);
            Assert.IsNotNull(d, why);
            Assert.AreEqual(DilemmaState.Open, d.state);
            return d;
        }

        // ---------- 4.1 who carries the offering ----------

        [Test]
        public void TheOfferingAsk_OnlyWithTheRitualTheTreeAndInTheMonthsBefore_ThePickLeadsItAndTheCardSaysSo()
        {
            var sim = Camp(11);
            sim.DebugAddKids(3, 30f); // grown-ups enough
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.Offering), "no offering kept: not asked");
            Keep(sim, RitualKind.Offering);
            Assume.That(!sim.KeeperTreeGrown);
            ToCalendar(sim, RitualKind.MonthOf(RitualKind.Offering) - 1);
            StringAssert.Contains("tree", sim.StoryAskWhy(StoryChoices.Offering), "no Keeper's tree: not asked");
            Assert.IsTrue(sim.DebugTreeStage(4));
            Assume.That(sim.KeeperTreeGrown);
            Assert.IsNull(sim.StoryAskWhy(StoryChoices.Offering), "kept, the tree grown, the month before it: asked");
            ToCalendar(sim, RitualKind.MonthOf(RitualKind.Offering));
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.Offering), "not in its own month");

            var past = sim.DebugPlayRitual(RitualKind.Offering); // someone carried it once
            var d = Raise(sim, StoryChoices.Offering);
            Assert.AreNotEqual(past.notableId, d.personA, "two who have not carried it");
            Assert.AreNotEqual(past.notableId, d.personB);
            var b = sim.FindNotable(d.personB);
            StringAssert.Contains(b.name, sim.QuestionOf(d));
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Assert.AreEqual(b.id, sim.StoryMarks.offeringCarrierId);
            Assert.AreSame(b, sim.RitualLead(RitualKind.Offering), "the pick leads the offering");
            StringAssert.Contains("Carries the offering to the Keeper's tree", sim.KeeperHandLine(b), "their card says so");
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "keeper" && e.text == $"{b.name} carried the offering to the Keeper's tree."), "the done line");
            var line = sim.DebugPlayRitual(RitualKind.Offering);
            Assert.AreEqual(b.id, line.notableId, "and carries it every year");
            // once: not asked again while they live
            ToCalendar(sim, RitualKind.MonthOf(RitualKind.Offering) - 1);
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.Offering), "not asked again while the pick lives");
            b.alive = false; b.diedMonth = sim.State.month;
            Assert.AreNotEqual(b.id, sim.RitualLead(RitualKind.Offering).id, "the usual rule after they die");
        }

        // ---------- 4.2 who sets the first log ----------

        [Test]
        public void TheFirstLogAsk_ThePickLeadsTheWoodpile_ALapsePicksNobody()
        {
            var sim = Camp(12);
            sim.DebugAddKids(3, 30f);
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.FirstLog), "no woodpile ritual: not asked");
            Keep(sim, RitualKind.Woodpile);
            if (!sim.WoodpileStands) sim.DebugFirstWildWoodNow(); // a living land's woodpile comes with its first wild wood
            ToCalendar(sim, RitualKind.MonthOf(RitualKind.Woodpile) - 2);
            Assume.That(sim.WoodpileStands);
            Assert.IsNull(sim.StoryAskWhy(StoryChoices.FirstLog));
            // a lapse (the people decide): nobody is picked, the usual lead stays
            var lapsed = Raise(sim, StoryChoices.FirstLog);
            Assert.IsNotNull(sim.DebugRaiseSmall(SmallChoices.Supper)); // raising another lapses the open one
            Assert.AreEqual(DilemmaState.Lapsed, lapsed.state);
            Assert.AreEqual(-1, sim.StoryMarks.firstLogId, "a lapse picks nobody");
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.FirstLog), "asked this year already");
            var d = Raise(sim, StoryChoices.FirstLog);
            var a = sim.FindNotable(d.personA);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card));
            Assert.AreSame(a, sim.RitualLead(RitualKind.Woodpile));
            StringAssert.Contains("Sets the first log on the woodpile", sim.KeeperHandLine(a));
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "keeper" && e.text == $"{a.name} set the first log on the woodpile."));
            StringAssert.Contains("set the first log", sim.DebugPlayRitual(RitualKind.Woodpile).text);
        }

        // ---------- 2.2 the singer's song, 4.3 its name ----------

        [Test]
        public void TheSingersSong_OnceABox_AtTheFirstRitualWithASinger_ACardNotAMoment()
        {
            var sim = Camp(13);
            sim.DebugAddKids(2, 30f);
            var singer = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).First();
            foreach (var n in sim.State.notables) if (n.trait == "the singer" && n != singer) n.trait = "the kind";
            singer.trait = "the singer";
            Keep(sim, RitualKind.Feast);
            Months(sim, 30);
            var lines = sim.State.chronicle.Where(e => e.kind == StoryEvent.Kind(StoryEvent.SingerSong)).ToList();
            Assert.AreEqual(1, lines.Count, "once, however many feasts");
            StringAssert.Contains(singer.name + " made up a song for the harvest feast.", lines[0].text);
            Assert.AreEqual(singer.id, sim.StoryMarks.songOwnerId);
            Assert.AreEqual(RitualKind.Feast, sim.StoryMarks.songRitual);
            Assert.IsNull(sim.MomentOfEntry(lines[0].id), "no moment of its own: it rides the ritual's (B80.d)");
            Assert.AreEqual(CardTone.Good, SimCore.ToneOf(lines[0].kind));
        }

        [Test]
        public void TheSongsName_OnlyOnceASongIsMade_ThePickIsOnTheRitualsCaption_AskedOnce()
        {
            var sim = Camp(14);
            sim.DebugAddKids(2, 30f);
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.SongName), "no song: not asked");
            Assert.AreEqual(RitualKind.Caption(RitualKind.Feast), sim.RitualCaption(RitualKind.Feast), "no song: the plain caption");
            var song = sim.DebugStory(StoryEvent.SingerSong, out string why);
            Assert.IsNotNull(song, why);
            Assert.IsNull(sim.StoryAskWhy(StoryChoices.SongName), "a song without a name: asked");
            string ritual = sim.StoryMarks.songRitual;
            var names = StoryChoices.SongNames(ritual);
            var d = Raise(sim, StoryChoices.SongName);
            var singer = sim.FindNotable(sim.StoryMarks.songOwnerId);
            Assert.AreEqual($"{singer.name}'s song needs a name. {names.a} or {names.b}?", sim.QuestionOf(d));
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Assert.AreEqual(names.b, sim.StoryMarks.songName);
            Assert.AreEqual($"{RitualKind.Caption(ritual)}: {names.b}", sim.RitualCaption(ritual), "the name on the ritual's caption");
            foreach (var k in RitualKind.All.Where(k => k != ritual)) Assert.AreEqual(RitualKind.Caption(k), sim.RitualCaption(k), "only that ritual's");
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "keeper" && e.text.Contains(names.b)), "and in the book");
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.SongName), "named: never asked again");
        }

        [Test]
        public void TheSongsHint_NamesTheRitualItIsSungAt()
        {
            var sim = Camp(14);
            sim.DebugAddKids(2, 30f);
            Assert.IsNotNull(sim.DebugStory(StoryEvent.SingerSong, out string why), why);
            string ritual = sim.StoryMarks.songRitual;
            var names = StoryChoices.SongNames(ritual);
            var v = sim.ViewOfDilemma(Raise(sim, StoryChoices.SongName));
            Assert.AreEqual($"Everyone sings {names.a} {StoryChoices.RitualAt(ritual)}, every year.", v.options[0].hint);
            Assert.AreEqual($"Everyone sings {names.b} {StoryChoices.RitualAt(ritual)}, every year.", v.options[1].hint);
            StringAssert.DoesNotContain("at it", v.options[0].hint);
            StringAssert.DoesNotContain("{", v.options[1].hint);
        }

        [Test]
        public void TheSupperChoice_AsksOneStewKeptGoingOrAFreshOneEachTime()
        {
            var sim = Camp(14);
            var v = sim.ViewOfDilemma(sim.DebugRaiseSmall(SmallChoices.Supper));
            Assert.AreEqual("Supper: one stew kept going by the fire, or a fresh one each time?", v.question);
            Assert.AreEqual("the stew kept going", v.options[0].label);
            Assert.AreEqual("a fresh one each time", v.options[1].label);
        }

        [Test]
        public void TheSongsNames_TwoPerRitual()
        {
            foreach (var k in RitualKind.All)
            {
                var (a, b) = StoryChoices.SongNames(k);
                Assert.IsNotEmpty(a); Assert.IsNotEmpty(b); Assert.AreNotEqual(a, b);
            }
            Assert.AreEqual(("The Long Table", "One More Bowl"), StoryChoices.SongNames(RitualKind.Feast));
        }

        // ---------- 4.4 which child holds the new pup ----------

        static SimCore WithLitter(int seed, out Dog pup)
        {
            var sim = Camp(seed);
            sim.DebugAddKids(4, 6f); // the first dog's keeper, and two more without a dog
            Assert.IsNotNull(sim.DebugDogNow(1, DogStage.Grown), "a grown dog");
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.PupHolder), "no litter: not asked");
            sim.DebugNextDogGeneration();
            Assume.That(sim.StoryMarks.pupDogId, Is.GreaterThanOrEqualTo(0), "a pup stayed while two kids had no dog");
            pup = sim.FindDog(sim.StoryMarks.pupDogId);
            Assert.AreEqual(sim.StoryMarks.pupKidA, pup.keeperId, "today's rule: the first kid without a dog");
            return sim;
        }

        [Test]
        public void ThePupAsk_TheKeepersPickHoldsIt_AskedOncePerPup()
        {
            var sim = WithLitter(15, out var pup);
            Assert.IsNull(sim.StoryAskWhy(StoryChoices.PupHolder), "a litter, two kids: asked");
            var d = Raise(sim, StoryChoices.PupHolder);
            Assert.AreEqual(sim.StoryMarks.pupKidA, d.personA); Assert.AreEqual(sim.StoryMarks.pupKidB, d.personB);
            Assert.IsTrue(sim.AnswerDilemma(d.id, 1, DilemmaPath.Card));
            Assert.AreEqual(d.personB, pup.keeperId, "the pick holds it: the pup follows them");
            CollectionAssert.Contains(sim.DogsOfKeeper(d.personB), pup);
            var k = sim.FindNotable(d.personB);
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "keeper" && e.text == $"{k.name} held the new pup first. It followed them everywhere after that."));
            Assert.IsNotNull(sim.StoryAskWhy(StoryChoices.PupHolder), "asked once a pup");
        }

        [Test]
        public void ThePupAsk_LetThemDecide_KeepsTodaysRule()
        {
            var sim = WithLitter(16, out var pup);
            int was = pup.keeperId;
            var d = Raise(sim, StoryChoices.PupHolder);
            Assert.IsTrue(sim.LetThemDecide(d.id));
            Assert.AreEqual(was, pup.keeperId, "let them decide: today's rule");
        }

        [Test]
        public void ThePupAsk_IsRaisedAsTheLitterComes_WithoutTheGap()
        {
            var sim = WithLitter(17, out _);
            var open = sim.OpenDilemma;
            if (open != null) sim.AnswerDilemma(open.id, 0, DilemmaPath.Card);
            Months(sim, 1);
            var o = sim.OpenDilemma;
            Assume.That(o, Is.Not.Null, "a choice is open after the month");
            Assert.AreEqual(StoryChoices.PupDef.id, o.defId, "the pup's ask, at once");
        }

        // ---------- 5.1 the Keeper's child comes of age ----------

        [Test]
        public void TheKeepersChild_ComesOfAge_TheirLineAndAStoryMoment_OnlyForTheNamed()
        {
            var sim = NewSim(18, new SimConfig { dilemmas = true, moments = true, historyPruneYears = 0 }); // the slice: work for everyone who grows up
            for (int i = 0; i < 12; i++) sim.Advance(1.0 / 12);
            sim.DebugAddKids(2, 15.1f); // two kids about to come of age (15.1 and 15.8)
            var kids = sim.State.notables.Where(n => n.alive && n.age > 15f && n.age < SimCore.AdultAge).OrderBy(n => n.id).ToList();
            Assume.That(kids.Count, Is.GreaterThanOrEqualTo(2));
            kids[0].namedByKeeper = true;
            Months(sim, 14);
            Assume.That(kids.All(k => k.age >= SimCore.AdultAge && !string.IsNullOrEmpty(k.job)), "both grew up into work");
            var lines = sim.State.chronicle.Where(e => e.kind == StoryEvent.Kind(StoryEvent.KeeperChildGrown)).ToList();
            Assert.AreEqual(1, lines.Count, "only the Keeper's child");
            var e = lines[0];
            Assert.AreEqual(kids[0].id, e.notableId);
            string job = kids[0].job, a = "aeiou".IndexOf(job[0]) >= 0 ? "an " : "a ";
            Assert.AreEqual($"{kids[0].name}, named by the Keeper, came of age and took up work as {a}{job}.", e.text);
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m); Assert.AreEqual(MomentKind.Story, m.kind); Assert.AreEqual(kids[0].id, m.personA);
            Assert.AreEqual(StoryEvent.KeeperChildGrown, sim.StoryOfEntry(e.id));
            Assert.AreEqual(2, sim.State.chronicle.Count(x => x.kind == "coming_of_age" && kids.Any(k => k.id == x.notableId)), "the hidden lines stay");
        }

        // ---------- 5.2 the Keeper's child marries ----------

        [Test]
        public void TheKeepersChild_Marries_TheSpecsLine_OnTheWeddingsCaption()
        {
            var sim = Camp(19);
            sim.DebugAddKids(2, 20f);
            var e = sim.DebugKeeperChildWed(out string why);
            Assert.IsNotNull(e, why);
            var a = sim.FindNotable(e.notableId); var b = sim.FindNotable(e.otherNotableId);
            var child = a.namedByKeeper ? a : b; var other = child == a ? b : a;
            Assert.AreEqual($"{child.name}, named by the Keeper, married {other.name} at the fire.", e.text);
            Assert.AreEqual("wedding", e.kind, "the wedding moment plays");
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m); Assert.AreEqual(MomentKind.Wedding, m.kind);
            Assert.AreEqual(e.text, sim.KeeperWeddingLine(e.id), "its caption says the line");
        }

        [Test]
        public void AWeddingWithNoKeepersChild_KeepsItsOwnLine()
        {
            var sim = Camp(20);
            foreach (var n in sim.State.notables) n.namedByKeeper = false;
            sim.DebugAddKids(6, 17f);
            Months(sim, 10 * SimConfig.MonthsPerYear);
            var weds = sim.State.chronicle.Where(x => x.kind == "wedding").ToList();
            Assume.That(weds.Count, Is.GreaterThan(0), "somebody married");
            foreach (var w in weds)
            {
                bool named = sim.NamedByKeeper(w.notableId) || sim.NamedByKeeper(w.otherNotableId);
                if (named) continue; // a naming answered on its own in the run (none expected: nobody answers)
                StringAssert.DoesNotContain("named by the Keeper", w.text);
                Assert.IsNull(sim.KeeperWeddingLine(w.id));
            }
        }
    }
}
