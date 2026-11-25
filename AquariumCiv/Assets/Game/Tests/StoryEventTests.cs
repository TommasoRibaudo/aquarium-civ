using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #722 (docs/design/new-events.md §1.1-1.3): the story events' sim side. Each first event is told once a box; at most one new one a game
    /// year from the story step (the marks follow their weddings); the couples' marks stop at eight pairs; the second stone stands beside the
    /// first once the tenth name is due; each line queues a Story moment naming its people. Pure C#.
    /// </summary>
    public class StoryEventTests
    {
        static SimCore Camp(int seed)
        {
            var sim = NewSim(seed, new SimConfig { dilemmas = true, moments = true, ecology = true, historyPruneYears = 0 }); // the whole History: the tests count its lines
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Assume.That(sim.EcologyOn, "the ecology is on");
            return sim;
        }

        static void Months(SimCore sim, int n) { for (int i = 0; i < n; i++) sim.Advance(1.0 / 12); }

        /// <summary>#722 batch 3: no tinkerer, gardener or joker, so their events (the props placed here are their gates) never take the year.</summary>
        static void NoTraitEvents(SimCore sim) { foreach (var n in sim.State.notables) if (n.trait is "the tinkerer" or "the gardener" or "the joker") n.trait = "the kind"; }

        static ChronicleEntry[] Lines(SimCore sim, string id) => sim.State.chronicle.Where(e => e.kind == StoryEvent.Kind(id)).ToArray();

        /// <summary>The name stone stands (placed a month ago) and <paramref name="names"/> people died since.</summary>
        static ConsequenceProp StoneWithNames(SimCore sim, int names)
        {
            sim.DebugPlaceEveryProp();
            var stone = sim.FirstNameStone;
            Assert.IsNotNull(stone, "a name stone stands");
            stone.dueMonth = sim.State.month - 1;
            sim.DebugAddKids(names + 4, 30f); // grown-ups enough to lose some and still have one to roll the stone
            NoTraitEvents(sim);
            for (int i = sim.NamesSince(stone).Count; i < names; i++) Assert.IsNotNull(sim.DebugKillOldest());
            return stone;
        }

        static void AssertMoment(SimCore sim, ChronicleEntry e)
        {
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m, e.kind + " queues a held moment");
            Assert.AreEqual(MomentKind.Story, m.kind);
            Assert.AreEqual(e.notableId, m.personA, "the moment's first person is the line's");
            Assert.AreEqual(e.otherNotableId, m.personB);
            Assert.IsNotNull(sim.StoryOfEntry(e.id), "the view can tell which event it is");
        }

        [Test]
        public void TheFirstFruit_OnceABox_InSummer_NamesAKid_AndQueuesAStoryMoment()
        {
            var sim = Camp(3);
            Assert.IsTrue(sim.DebugTreeStage(4));
            Assume.That(sim.KeeperTreeGrown, "the Keeper's tree is grown");
            sim.DebugAddKids(2);
            Months(sim, 36);
            Assume.That(sim.KeeperTreeGrown, "the tree is still grown");
            var lines = Lines(sim, StoryEvent.FirstFruit);
            Assert.AreEqual(1, lines.Length, "told once, however many summers");
            var e = lines[0];
            int cal = SeasonCalendar.OfMonth(e.month, sim.State.seasonShift);
            Assert.That(cal, Is.InRange(5, 7), "in summer");
            Assert.AreEqual(e.month / SimConfig.MonthsPerYear, sim.StoryMarks.fruitYear);
            var kid = sim.FindNotable(e.notableId);
            Assert.IsNotNull(kid); StringAssert.Contains(kid.name, e.text, "the kid is named");
            Assert.AreEqual(StoryEvent.FirstFruit, sim.StoryOfEntry(e.id));
            AssertMoment(sim, e);
        }

        [Test]
        public void TheTreeFruitsEverySummerAfterTheFirst()
        {
            var sim = Camp(4);
            Assert.IsTrue(sim.DebugTreeStage(4));
            sim.DebugAddKids(2);
            Assert.IsNotNull(sim.DebugStory(StoryEvent.FirstFruit, out string why), why);
            int fruiting = 0, summers = 0;
            for (int i = 0; i < 24; i++)
            {
                Months(sim, 1);
                int cal = sim.CalendarMonth;
                if (cal >= 5 && cal <= 7) { summers++; if (sim.TreeInFruit) fruiting++; }
                else Assert.IsFalse(sim.TreeInFruit, "only in summer");
            }
            Assume.That(sim.KeeperTreeGrown);
            Assert.Greater(summers, 0);
            Assert.AreEqual(summers, fruiting, "every summer");
        }

        [Test]
        public void AtMostOneNewEventAGameYear_FromTheStoryStep()
        {
            var sim = Camp(5);
            Assert.IsTrue(sim.DebugTreeStage(4));
            StoneWithNames(sim, SimCore.StoneNames + 1); // the second stone is due now, the fruit at the next summer
            sim.DebugAddKids(2); // a kid to pick it
            Months(sim, 36);
            var firsts = sim.State.chronicle.Where(e => e.kind == StoryEvent.Kind(StoryEvent.FirstFruit) || e.kind == StoryEvent.Kind(StoryEvent.SecondStone)).ToList();
            Assert.AreEqual(2, firsts.Count, "both told, once each");
            Assert.AreEqual(2, firsts.Select(e => e.month / SimConfig.MonthsPerYear).Distinct().Count(), "in two different years");
        }

        [Test]
        public void TheSecondStone_AfterTheTenthName_BesideTheFirst_Once()
        {
            var sim = Camp(6);
            Assume.That(!sim.KeeperTreeGrown, "no first fruit to take the year");
            var first = StoneWithNames(sim, SimCore.StoneNames);
            int stones = sim.ConsequenceProps.Count(p => p.kind == ConsequenceKind.NameStone);
            Months(sim, 1);
            if (sim.NamesSince(first).Count <= SimCore.StoneNames)
            {
                Assert.AreEqual(0, Lines(sim, StoryEvent.SecondStone).Length, "nine names: the stone holds them");
                Assert.AreEqual(-1, sim.StoryMarks.secondStoneId);
                Assert.IsNotNull(sim.DebugKillOldest()); // the tenth
                Months(sim, 1);
            }
            var lines = Lines(sim, StoryEvent.SecondStone);
            Assert.AreEqual(1, lines.Length, "the tenth name is due: a second stone");
            var second = sim.FindConsequenceProp(sim.StoryMarks.secondStoneId);
            Assert.IsNotNull(second);
            Assert.AreEqual(ConsequenceKind.NameStone, second.kind); Assert.IsTrue(second.placed);
            Assert.AreSame(first, sim.FirstNameStone, "the first is still the first");
            float apart = (float)System.Math.Sqrt((second.x - first.x) * (second.x - first.x) + (second.z - first.z) * (second.z - first.z));
            Assert.That(apart, Is.InRange(0.15f, 0.4f), "beside the first");
            var e = lines[0];
            Assert.AreEqual(sim.StoryMarks.secondStoneFirst, e.otherNotableId, "the one just lost goes on it first");
            Assert.IsFalse(sim.FindNotable(e.otherNotableId).alive);
            var who = sim.FindNotable(e.notableId);
            Assert.IsTrue(who.alive && who.age >= SimCore.AdultAge, "a grown-up rolls it");
            AssertMoment(sim, e);
            // once: more names never roll a third
            for (int i = 0; i < 3; i++) sim.DebugKillOldest();
            Months(sim, 14);
            Assert.AreEqual(1, Lines(sim, StoryEvent.SecondStone).Length);
            Assert.AreEqual(stones + 1, sim.ConsequenceProps.Count(p => p.kind == ConsequenceKind.NameStone), "one second stone, no third");
        }

        [Test]
        public void TheMarks_APairACouple_AtMostEight_TheLineQueuesAStoryMoment()
        {
            var sim = Camp(7);
            Assert.IsTrue(sim.DebugTreeStage(4));
            sim.DebugAddKids(4, 30f);
            var e = sim.DebugStory(StoryEvent.TwoMarks, out string why);
            Assert.IsNotNull(e, why);
            Assert.AreEqual(StoryEvent.Kind(StoryEvent.TwoMarks), e.kind);
            Assert.AreEqual(1, sim.StoryMarks.markA.Count);
            Assert.AreEqual(e.notableId, sim.StoryMarks.markA[0]); Assert.AreEqual(e.otherNotableId, sim.StoryMarks.markB[0]);
            AssertMoment(sim, e);
            for (int i = 0; i < 12; i++) sim.DebugStory(StoryEvent.TwoMarks, out _);
            Assert.AreEqual(SimCore.MaxMarkPairs, sim.StoryMarks.markA.Count, "eight pairs at most");
            Assert.AreEqual(sim.StoryMarks.markA.Count, sim.StoryMarks.markB.Count);
        }

        [Test]
        public void TheMarks_FollowTheWeddings_TheFirstCoupleIsTheEventOnce()
        {
            var sim = Camp(8);
            Assert.IsTrue(sim.DebugTreeStage(4));
            sim.DebugAddKids(6, 17f); // young grown-ups to marry
            long from = sim.State.month;
            Months(sim, 15 * SimConfig.MonthsPerYear);
            Assume.That(sim.KeeperTreeGrown, "the tree stayed grown");
            int weddings = sim.State.chronicle.Count(x => x.kind == "wedding" && x.month >= from);
            Assume.That(weddings, Is.GreaterThan(0), "somebody married");
            Assert.AreEqual(1, Lines(sim, StoryEvent.TwoMarks).Length, "the first couple's marks are the event, once");
            Assert.AreEqual(System.Math.Min(SimCore.MaxMarkPairs, weddings), sim.StoryMarks.markA.Count, "a pair each wedding, at most eight");
            Assert.AreEqual(sim.StoryMarks.markA.Count - 1, sim.State.chronicle.Count(x => x.kind == "story_marks_year"), "later couples' marks are History");
        }
    }
}
