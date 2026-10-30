using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The chronicle records the cards need (#59, chronicle-and-history.md §2, §5, §6): ranking, grouping and caps,
    /// merged linked stories, teaser threads and their payoffs, the glass stage, the four lines that know too much, and
    /// proof that none of it touches the economy.</summary>
    public class CardsTests
    {
        static readonly string[] KnowTooMuch = { "mirror for signalling the Keeper", "from the glass's shadow", "the Keeper has a cat", "city beyond the glass" };
        static readonly string[] StandIns = { "a stick that throws a shadow", "a better basket", "the Bright One sneezed", "knocked back" };

        static void Watched(SimCore sim, int years, System.Action<SimCore> yearly = null)
        {
            for (int y = 0; y < years; y++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
                yearly?.Invoke(sim);
            }
        }

        /// <summary>A quiet box at month 0 (its own founding lines are at month 0, so a return since 0 doesn't see them).</summary>
        static SimCore Quiet(int seed = 5)
        {
            var sim = NewSim(seed);
            sim.TermFlavourOn = false;
            foreach (var n in sim.State.notables) { n.rivalId = -1; n.friendId = -1; n.partnerId = -1; }
            return sim;
        }

        static ChronicleEntry Add(SimCore sim, long month, string kind, float salience, int a = -1, int b = -1)
        {
            sim.State.month = month;
            var e = sim.Chronicle(kind, $"A {kind} happened.", salience, a);
            e.otherNotableId = b;
            return e;
        }

        static List<Notable> Adults(SimCore sim) => sim.State.notables.Where(n => n.alive && n.age >= 18f).OrderBy(n => n.id).ToList();

        // ---------- ranking, grouping, caps ----------
        [Test]
        public void TheCapScalesWithTimeAwayAndTheRestGoToAlso()
        {
            var sim = Quiet();
            string[] kinds = { "wedding", "friendship", "storm", "keeper", "invention", "naming", "apprentice", "snow", "festival", "role_won" };
            for (int i = 0; i < kinds.Length; i++) Add(sim, 1 + i, kinds[i], 0.5f + i * 0.02f);

            foreach (var (hours, cap) in new[] { (0.5, 3), (1.0, 5), (4.0, 5), (8.0, 5), (24.0, 7) })
            {
                var r = sim.ReturnCards(0, hours);
                Assert.AreEqual(cap, r.cap, $"{hours} h");
                Assert.AreEqual(cap, r.cards.Count, $"{hours} h");
                Assert.AreEqual(kinds.Length - cap, r.also.Count, "the rest wait in Also…");
                Assert.AreEqual(System.Math.Min(3, r.also.Count), r.alsoHeadlines.Count);
                Assert.AreEqual(System.Math.Max(0, r.also.Count - 3), r.alsoMore);
                Assert.IsTrue(r.cards.Any(c => c.lead.kind == "role_won"), "the best makes the stack");
                Assert.IsTrue(r.also.All(a => a.score <= r.cards.Max(c => c.score)));
                Assert.IsTrue(r.alsoHeadlines.All(h => h.Length <= 50));
            }
            Assert.AreEqual(0, sim.ReturnCards(20, 24).cards.Count, "nothing after month 20");
        }

        [Test]
        public void GoodNewsFirstThenTheRestThenDeathsThenTheWish()
        {
            var sim = Quiet();
            var a = Adults(sim);
            Add(sim, 1, "death", 0.85f, a[0].id);
            Add(sim, 2, "storm", 0.5f);
            Add(sim, 3, "wish", 0.6f, a[1].id);
            Add(sim, 4, "rainbow", 0.42f);
            Add(sim, 5, "wedding", 0.7f);
            Add(sim, 6, "era-ready", 0.8f);
            Add(sim, 7, "coming_of_age", 0.95f, a[1].id);
            Add(sim, 8, "rivalry", 0.58f);
            sim.State.eraReady = true;

            var r = sim.ReturnCards(0, 24);
            CollectionAssert.AreEqual(new[] { "era-ready", "wedding", "rivalry", "storm", "rainbow", "death", "wish" }, r.cards.Select(c => c.lead.kind).ToArray());
            Assert.AreEqual(CardKind.EraReady, r.cards[0].kind);
            Assert.AreEqual(CardKind.Legend, r.cards[5].kind);
            Assert.AreEqual(CardTone.Sad, r.cards[5].tone);
            Assert.AreEqual(CardKind.Wish, r.cards[6].kind);
            Assert.IsFalse(r.cards.Concat(r.also).Any(c => c.lead.kind == "coming_of_age"), "never a card");

            // Under an hour: era ready and the wish still come, then the best by score (sad still after good, before the wish).
            r = sim.ReturnCards(0, 0.5);
            CollectionAssert.AreEqual(new[] { "era-ready", "death", "wish" }, r.cards.Select(c => c.lead.kind).ToArray(), "the death outscores the wedding");

            // Once the era is watched, era-ready is plain good news.
            sim.State.eraReady = false;
            Assert.AreEqual(CardKind.Story, sim.ReturnCards(0, 24).cards.First(c => c.lead.kind == "era-ready").kind);
        }

        [Test]
        public void NoThreeWeddingsInARow()
        {
            var sim = Quiet();
            Add(sim, 1, "wedding", 0.7f); Add(sim, 2, "wedding", 0.7f); Add(sim, 3, "wedding", 0.7f);
            Add(sim, 4, "storm", 0.5f); Add(sim, 5, "friendship", 0.5f);
            var r = sim.ReturnCards(0, 0.5);
            Assert.AreEqual(2, r.cards.Count(c => c.lead.kind == "wedding"), "−0.15 per wedding already chosen: 0.7, 0.55, then 0.4 < 0.5");
            Assert.AreEqual(1, r.also.Count(c => c.lead.kind == "wedding"));
        }

        [Test]
        public void ThePinnedPersonAndAViewedCardScoreHigher()
        {
            var sim = Quiet();
            var a = Adults(sim);
            Add(sim, 1, "storm", 0.6f);
            Add(sim, 2, "invention", 0.5f, a[0].id);
            Add(sim, 3, "song", 0.5f, a[1].id);
            sim.State.pinnedNotableId = a[0].id;
            var r = sim.ReturnCards(0, 0.5, new[] { a[1].id });
            CollectionAssert.AreEqual(new[] { "invention", "storm", "song" }, r.cards.Select(c => c.lead.kind).ToArray());
            Assert.AreEqual(0.65f, r.cards[0].score, 1e-5f);
            Assert.AreEqual(0.55f, r.cards[2].score, 1e-5f);
        }

        // ---------- linked stories ----------
        [Test]
        public void AWeddingAndTheFirstChildAreOneCard()
        {
            var sim = Quiet();
            var a = Adults(sim);
            var mum = a[0]; var dad = a[1];
            mum.partnerId = dad.id; dad.partnerId = mum.id;
            var wedding = Add(sim, 10, "wedding", 0.7f, mum.id, dad.id);
            sim.State.month = 20;
            var child = sim.DebugBirth();
            Assert.AreEqual(mum.id, child.parentId);
            sim.State.month = 30;
            sim.DebugBirth(); // a second child: not linked

            var r = sim.ReturnCards(5, 24);
            Assert.AreEqual(2, r.cards.Count, "wedding + first child merged, the second child alone");
            var merged = r.cards.First(c => c.entries.Count == 2);
            CollectionAssert.AreEqual(new[] { "wedding", "birth" }, merged.entries.Select(e => e.kind).ToArray());
            Assert.AreEqual(wedding, merged.lead);
            Assert.AreEqual(sim.FindThread(wedding.threadId).source, ThreadSource.Link);
            CollectionAssert.IsSubsetOf(new[] { mum.id, dad.id, child.id }, merged.people);

            // On a later return the child's card remembers the wedding the player already saw.
            var later = sim.ReturnCards(15, 24).cards.First(c => c.entries[0].notableId == child.id);
            Assert.AreEqual(wedding, later.before);
            StringAssert.Contains(child.name, later.headline);
        }

        [Test]
        public void ARivalryAndItsMakingUpAreOneCard_AndLostLinksToFound()
        {
            var sim = Quiet();
            var a = Adults(sim);
            Add(sim, 1, "rivalry", 0.58f, a[0].id, a[1].id);
            Add(sim, 2, "storm", 0.5f);
            Add(sim, 3, "reconcile", 0.55f, a[1].id, a[0].id);
            var lost = Add(sim, 4, "lost", 0.6f, a[0].id);
            var found = Add(sim, 9, "found", 0.6f, a[0].id);
            var r = sim.ReturnCards(0, 24);
            Assert.AreEqual(3, r.cards.Count);
            Assert.IsTrue(r.cards.Any(c => c.entries.Select(e => e.kind).SequenceEqual(new[] { "rivalry", "reconcile" })));
            Assert.IsTrue(r.cards.Any(c => c.entries.Contains(lost) && c.entries.Contains(found)));

            // The relic hook links any two entries.
            var x = Add(sim, 10, "relic", 0.8f, a[1].id);
            var y = Add(sim, 11, "storm", 0.5f);
            sim.LinkStories(x, y);
            Assert.AreEqual(1, sim.ReturnCards(9, 24).cards.Count(c => c.entries.Contains(x) && c.entries.Contains(y)));
        }

        // ---------- teasers ----------
        static (SimCore sim, Notable a, Notable b) Sweethearts()
        {
            var sim = Quiet();
            var ad = Adults(sim);
            var a = ad[0]; var b = ad[1];
            if (a.family == b.family) b.family = NameData.Families.First(f => f != a.family);
            a.age = 25; b.age = 26;
            a.friendId = b.id; b.friendId = a.id;
            return (sim, a, b);
        }

        [Test]
        public void ATeasedCoupleWhoMarryIsAPayoffCard()
        {
            var (sim, a, b) = Sweethearts();
            var t = sim.OpenTeaserThread();
            Assert.AreEqual(ThreadSource.Couple, t.source);
            StringAssert.Contains(a.FullName, t.teaser);
            StringAssert.Contains("keep sitting together", t.teaser);
            Assert.AreEqual(t.teaser, sim.CurrentTeaser, "the peek shows the teaser");
            Assert.AreNotEqual(t.teaser, sim.OpenTeaser(), "never the same one twice in a row");

            var wedding = Add(sim, 30, "wedding", 0.7f, b.id, a.id);
            var card = sim.ReturnCards(0, 24).cards.Single(c => c.entries.Contains(wedding));
            Assert.AreEqual(ThreadState.PaidOff, t.state);
            Assert.AreEqual(wedding.id, t.payoffEntryId);
            Assert.AreEqual(t.id, wedding.threadId);
            Assert.IsTrue(card.paysOffTeaser);
            Assert.AreEqual(t.teaser, card.teaser, "the old teaser, grey, above the headline");
            Assert.AreEqual(0.7f + SimCore.TeaserPayoffBoost, card.score, 1e-5f);

            // The first child joins the same story.
            a.partnerId = b.id; b.partnerId = a.id;
            sim.State.month = 40;
            var child = sim.DebugBirth();
            Assert.AreEqual(1, sim.ReturnCards(0, 24).cards.Count(c => c.entries.Contains(wedding) && c.entries.Any(e => e.notableId == child.id)));
            Assert.AreEqual(t.id, sim.State.chronicle.Last(e => e.kind == "birth").threadId);
        }

        [Test]
        public void ADeathEndsATeaserGently()
        {
            var (sim, a, b) = Sweethearts();
            var t = sim.OpenTeaserThread();
            a.alive = false;
            var death = Add(sim, 12, "death", 0.85f, a.id);
            var card = sim.ReturnCards(0, 24).cards.Single(c => c.entries.Contains(death));
            Assert.AreEqual(ThreadState.Died, t.state);
            Assert.AreEqual(CardKind.Legend, card.kind);
            Assert.AreEqual(t.teaser, card.teaser);
            Assert.AreEqual($"{b.name} never did ask {a.name}.", card.endLine);
            Assert.IsFalse(card.paysOffTeaser);
            Assert.IsNull(sim.CurrentTeaser);
        }

        [Test]
        public void AnUnansweredGiftIsTeasedAndItsAnswerPaysOff()
        {
            var sim = Quiet(11);
            var nudge = sim.PlaceNudge(SimCore.NudgeKind.Rain, 3);
            var t = sim.OpenTeaserThread();
            Assert.AreEqual(ThreadSource.Gift, t.source);
            Assert.AreEqual("The gatherers keep checking the wet ground.", t.teaser);
            long since = sim.State.month;
            for (int m = 0; m < SimCore.NudgeMonths && !nudge.credited; m++) sim.Step();
            Assert.IsTrue(nudge.credited);
            var card = sim.ReturnCards(since, 24).cards.Single(c => c.paysOffTeaser);
            Assert.AreEqual(CardKind.Keeper, card.kind);
            Assert.AreEqual(nudge.buildingId, card.buildingId);
            Assert.AreEqual(t.teaser, card.teaser);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(77)]
        [TestCase(1234)]
        public void InACenturyEveningTalkTeasersPayOff(int seed)
        {
            var sim = NewSim(seed);
            var said = new List<(string line, Era era)>();
            Watched(sim, 100, s => { var l = s.OpenTeaser(); if (l != null) said.Add((l, s.State.era)); });
            var teasers = sim.State.threads.Where(t => t.source != ThreadSource.Link).ToList();
            var paid = teasers.Where(t => t.state == ThreadState.PaidOff).ToList();
            TestContext.WriteLine($"seed {seed}: {said.Count} said, {teasers.Count} threads, {paid.Count} paid, {teasers.Count(t => t.state == ThreadState.Died)} ended, " +
                                  string.Join(", ", teasers.GroupBy(t => t.source).Select(g => $"{g.Key} {g.Count()}")));
            foreach (var t in paid.Take(6)) TestContext.WriteLine($"  \"{t.teaser}\" -> {sim.State.chronicle.First(e => e.id == t.payoffEntryId).text}");

            Assert.Greater(said.Count, 90);
            Assert.Greater(paid.Count, 0, "something teased came true");
            for (int i = 1; i < said.Count; i++) Assert.AreNotEqual(said[i - 1].line, said[i].line, "never the same one twice in a row");
            Assert.IsFalse(said.Any(x => x.era < Era.Town && x.line.ToLowerInvariant().Contains("glass")), "the people don't say glass before the town");
            foreach (var t in paid)
            {
                var e = sim.State.chronicle.Single(x => x.id == t.payoffEntryId);
                Assert.Greater(e.month, t.month, "the payoff came after the talk");
                Assert.IsTrue(sim.PaysOffATeaser(e));
                if (t.notableId >= 0 && t.source != ThreadSource.Era && t.source != ThreadSource.Gift)
                    Assert.IsTrue(e.notableId == t.notableId || e.otherNotableId == t.notableId || e.notableId == t.otherNotableId, $"{t.teaser} -> {e.text}");
            }
            Assert.AreEqual(sim.State.threads.Count, sim.State.threads.Select(t => t.id).Distinct().Count());
            Assert.IsTrue(sim.State.chronicle.All(e => e.threadId >= 0), "every entry looked at");
        }

        [Test]
        public void NoTeaserWhileAnEraWaits()
        {
            var sim = NewSim(3);
            sim.ForceEraReady();
            Assert.IsNull(sim.OpenTeaser(), "the era-ready state replaces the teaser");
            Assert.IsNotNull(sim.DebugOpenTeaser());
            Assert.IsTrue(sim.State.eraReady);
        }

        // ---------- the glass stage ----------
        [Test]
        public void TheGlassStageMovesWithTheErasAndTheRelics()
        {
            var sim = NewSim(9);
            var changes = new List<int>();
            sim.GlassStageChanged += s => changes.Add(s);
            Assert.AreEqual(1, sim.GlassStage);
            Assert.IsFalse(sim.GlassNudged);

            Assert.AreEqual(1, sim.GlassRelicFound(SimCore.GlassRelic.SkyCrumb), "the camp's own relic nudges stage 1");
            Assert.IsTrue(sim.GlassNudged);
            Assert.AreEqual(1, sim.GlassRelicFound(SimCore.GlassRelic.SkyCrumb));
            Assert.AreEqual(1, sim.GlassRelicFound("left_sandal"), "a relic that says nothing about the glass");

            Assert.AreEqual(2, sim.GlassRelicFound(SimCore.GlassRelic.Lens), "a later relic pulls them on one stage, not two");
            Assert.IsFalse(sim.GlassNudged);
            CollectionAssert.AreEqual(new[] { 2 }, changes);

            sim.ForceNextEra(); // village: already at 2
            Assert.AreEqual(2, sim.GlassStage);
            sim.GlassRelicFound(SimCore.GlassRelic.KeeperDoll);
            Assert.IsTrue(sim.GlassNudged);
            sim.ForceNextEra(); // town
            Assert.AreEqual(3, sim.GlassStage);
            CollectionAssert.AreEqual(new[] { 2, 3 }, changes);
            sim.GlassRelicFound(SimCore.GlassRelic.SkyCrumb);
            Assert.AreEqual(3, sim.GlassStage, "never back");
            Assert.IsTrue(sim.State.storyFlags.Contains("relic:sky_crumb") && sim.State.storyFlags.Contains("relic:lens"));

            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(3, loaded.GlassStage);

            // The era alone moves it too.
            var plain = NewSim(9);
            plain.ForceNextEra();
            Assert.AreEqual(2, plain.GlassStage);
            Assert.AreEqual(2, plain.State.glassStage);
            // An old save without the field still reads the era's stage.
            plain.State.glassStage = 1;
            Assert.AreEqual(2, plain.GlassStage);
        }

        // ---------- the four lines that know too much ----------
        static List<string> ForcedStories(SimCore sim, int rounds)
        {
            var texts = new List<string>();
            string[] traits = { "the clever", "the gossip", "the dreamer" };
            for (int i = 0; i < rounds; i++)
            {
                int k = 0;
                foreach (var n in sim.State.notables.Where(n => n.alive)) n.trait = traits[k++ % traits.Length];
                int before = sim.State.chronicle.Count;
                sim.ForceStory();
                texts.AddRange(sim.State.chronicle.Skip(before).Select(e => e.text));
                sim.State.month += 16 * SimConfig.MonthsPerYear; // past every Fresh window
            }
            return texts;
        }

        [Test]
        public void TheFourLinesWaitForTheTown()
        {
            var sim = NewSim(21);
            var camp = ForcedStories(sim, 120);
            sim.ForceNextEra();
            var village = ForcedStories(sim, 120);
            sim.ForceNextEra();
            var town = ForcedStories(sim, 200);

            foreach (var line in camp.Concat(village))
                foreach (var k in KnowTooMuch) StringAssert.DoesNotContain(k, line);
            var early = camp.Concat(village).ToList();
            foreach (var s in StandIns) Assert.IsTrue(early.Any(l => l.Contains(s)), $"stand-in told before the town: {s}");
            foreach (var k in KnowTooMuch) Assert.IsTrue(town.Any(l => l.Contains(k)), $"told in the town: {k}");
            Assert.IsFalse(early.Any(l => l.Contains("Keeper's lamp")));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(77)]
        [TestCase(1234)]
        [TestCase(4242)]
        public void InACenturyTheFourLinesNeverComeBeforeTheTown(int seed)
        {
            var sim = NewSim(seed);
            int early = 0;
            sim.Chronicled += e =>
            {
                if (sim.State.era == Era.Town) return;
                early++;
                foreach (var k in KnowTooMuch) StringAssert.DoesNotContain(k, e.text, $"year {e.month / 12} ({sim.State.era})");
                StringAssert.DoesNotContain("Keeper's lamp", e.text);
            };
            Watched(sim, 100);
            Assert.Greater(early, 20);
        }

        [Test]
        public void TheStrangeLightIsTheKeepersLampOnceTheLampIsUsed()
        {
            var sim = NewSim(4);
            sim.KeeperLampUsed();
            foreach (var n in sim.State.notables) n.alive = false; // fewer options: the world's events come up more
            for (int i = 0; i < 300 && !sim.State.chronicle.Any(e => e.kind == "strange_light"); i++)
            {
                sim.ForceStory();
                sim.State.month += 21 * SimConfig.MonthsPerYear;
            }
            StringAssert.Contains("Keeper's lamp", sim.State.chronicle.Last(e => e.kind == "strange_light").text);
        }

        // ---------- the economy, and saves ----------
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(77)]
        [TestCase(1234)]
        public void TheEconomyIsTheSameWithAndWithoutTheCards(int seed)
        {
            var off = NewSim(seed); off.CardsOn = false;
            var on = NewSim(seed);
            Watched(off, 120);
            Watched(on, 120, s => { s.OpenTeaser(); s.ReturnCards(s.State.month - 12, 1); });
            Assert.AreEqual(Era.Town, on.State.era);
            Assert.Greater(on.State.threads.Count, 50);

            // Same runtime: the economy, the dice and the notables are identical.
            Assert.AreEqual(JobTests.Economy(off.State), JobTests.Economy(on.State), "economy, dice and notables");
            Assert.AreEqual(off.State.chronicle.Select(e => e.id + e.kind), on.State.chronicle.Select(e => e.id + e.kind), "the same stories, same ids");

            // The texts differ only where a line that knows too much was told before the town (or the early strange light).
            var stripped = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(on.State));
            int swapped = 0;
            for (int i = 0; i < stripped.chronicle.Count; i++)
            {
                var a = off.State.chronicle[i]; var b = stripped.chronicle[i];
                if (a.text != b.text)
                {
                    swapped++;
                    Assert.IsTrue(KnowTooMuch.Any(a.text.Contains) || a.text.Contains("Keeper's lamp"), $"{a.text} / {b.text}");
                    b.text = a.text;
                }
                Assert.AreEqual(-1, a.threadId);
                b.threadId = -1;
            }
            TestContext.WriteLine($"seed {seed}: {swapped} lines swapped, {on.State.threads.Count} threads");
            stripped.threads = off.State.threads;
            stripped.threadNextId = off.State.threadNextId;
            stripped.teaserThreadId = off.State.teaserThreadId;
            Assert.AreEqual(JsonUtility.ToJson(off.State), JsonUtility.ToJson(stripped), "the whole box: notables, links, jobs, roles, ids, dice");
        }

        [Test]
        public void CardRecordsSurviveASave()
        {
            var sim = NewSim(77);
            Watched(sim, 40, s => s.OpenTeaser());
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(loaded.State));
            Assert.Greater(loaded.State.threads.Count, 5);
            Assert.AreEqual(sim.CurrentTeaser, loaded.CurrentTeaser);

            Watched(sim, 30, s => s.OpenTeaser());
            Watched(loaded, 30, s => s.OpenTeaser());
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(loaded.State), "a loaded box goes on the same way");
            var a = sim.ReturnCards(sim.State.month - 240, 24); var b = loaded.ReturnCards(loaded.State.month - 240, 24);
            Assert.AreEqual(a.cards.Select(c => c.headline + c.teaser + c.score), b.cards.Select(c => c.headline + c.teaser + c.score));

            // A save from before the cards: entries not looked at, no threads. The links come back on load.
            var old = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            old.threads = new List<StoryThread>(); old.threadNextId = 0; old.teaserThreadId = -1;
            foreach (var e in old.chronicle) e.threadId = -1;
            var oldSim = new SimCore(sim.Config, old);
            var r = oldSim.ReturnCards(0, 24);
            Assert.AreEqual(7, r.cards.Count);
            Assert.IsTrue(old.chronicle.All(e => e.threadId >= 0));
            Assert.Greater(old.threads.Count, 0, "the story links are rebuilt");
            Assert.IsTrue(old.threads.All(t => t.source == ThreadSource.Link), "teasers aren't invented after the fact");
        }
    }
}
