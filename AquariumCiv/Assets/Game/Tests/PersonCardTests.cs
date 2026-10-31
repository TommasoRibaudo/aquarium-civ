using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Notables in the view (#39): the person card's view-model, the relationships graph, the pin rules and the
    /// memorial. All pure: building cards never changes the world or the dice.</summary>
    public class PersonCardTests
    {
        static SimCore Grown(int seed, double years = 40)
        {
            var sim = NewSim(seed);
            sim.Advance(years);
            return sim;
        }

        static string Fingerprint(SimCore sim) =>
            $"{sim.State.month}|{sim.State.population}|{sim.State.food:F4}|{sim.State.wood:F4}|{sim.State.mood:F4}|{sim.State.buildings.Count}|" +
            $"{sim.State.notables.Count}|{sim.State.chronicle.Count}|{sim.State.rng.state}|{sim.State.econRng.state}|{sim.State.storyRng.state}|{sim.State.nextId}";

        [Test]
        public void CardShowsNameAgeTraitJobRoleAndStories()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var sim = Grown(seed);
                foreach (var n in sim.State.notables.Where(x => x.alive))
                {
                    var c = sim.PersonCardOf(n.id);
                    Assert.AreEqual(n.FullName, c.fullName);
                    Assert.AreEqual(n.trait, c.trait);
                    Assert.AreEqual((int)n.age, c.age);
                    Assert.IsTrue(c.alive);
                    Assert.IsNull(c.memorial);
                    Assert.AreEqual(sim.RoleOf(n), c.role);
                    if (n.age >= SimCore.AdultAge && !string.IsNullOrEmpty(n.job)) StringAssert.StartsWith(n.job, c.roleLine);
                    if (c.role != null)
                    {
                        StringAssert.Contains(c.role, c.roleLine);
                        Assert.Greater(c.roleSinceYear, 0);
                        Assert.IsTrue(c.terms.Any(t => t.role == c.role && t.toYear < 0), $"seed {seed}: the current term is listed");
                    }
                    Assert.LessOrEqual(c.recentStories.Count, SimCore.CardStories);
                    Assert.LessOrEqual(c.bestStories.Count, SimCore.CardStories);
                    for (int i = 1; i < c.recentStories.Count; i++) Assert.GreaterOrEqual(c.recentStories[i - 1].month, c.recentStories[i].month);
                    for (int i = 1; i < c.bestStories.Count; i++) Assert.GreaterOrEqual(c.bestStories[i - 1].salience, c.bestStories[i].salience);
                    foreach (var st in c.recentStories.Concat(c.bestStories))
                    {
                        var e = sim.State.chronicle.First(x => x.id == st.entryId);
                        Assert.IsTrue(e.notableId == n.id || e.otherNotableId == n.id);
                        Assert.IsFalse(string.IsNullOrEmpty(st.headline));
                    }
                }
                Assert.IsNull(sim.PersonCardOf(987654));
            }
        }

        [Test]
        public void ChildRoleLineAndLeaderLine()
        {
            var sim = NewSim(2);
            var parent = sim.State.notables[0];
            var kid = sim.DebugBirth();
            Assert.AreEqual("baby", sim.PersonCardOf(kid.id).roleLine);
            kid.age = 8f;
            Assert.AreEqual("child", sim.PersonCardOf(kid.id).roleLine);
            parent.job = JobType.Gatherer;
            sim.DebugSetRoleHolder(RoleType.Chief, parent);
            Assert.AreEqual("gatherer, chief of the camp", sim.CardRoleLine(parent, RoleType.Chief));
        }

        [Test]
        public void FamilyFriendsAndRivalsComeFromTheLinks()
        {
            var sim = Grown(3, 30);
            var adults = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).ToList();
            Assume.That(adults.Count >= 3);
            var a = adults[0]; var b = adults[1]; var c = adults[2];
            a.friendId = b.id; b.friendId = a.id;
            a.rivalId = c.id; c.rivalId = a.id;
            var card = sim.PersonCardOf(a.id);
            Assert.IsTrue(card.friends.Any(t => t.otherId == b.id && t.kind == TieKind.Friend && t.label == "friend"));
            Assert.IsTrue(card.rivals.Any(t => t.otherId == c.id && t.kind == TieKind.Rival));
            Assert.IsTrue(sim.PersonCardOf(c.id).rivals.Any(t => t.otherId == a.id));

            // children and the other parent, siblings
            var kid = sim.DebugBirth();
            var p = sim.FindNotable(kid.parentId);
            var kc = sim.PersonCardOf(kid.id);
            Assert.IsTrue(kc.family.Any(t => t.otherId == p.id && t.kind == TieKind.Parent));
            Assert.IsTrue(sim.PersonCardOf(p.id).family.Any(t => t.otherId == kid.id && t.kind == TieKind.Child));
            var kid2 = sim.DebugBirth();
            if (kid2.parentId == p.id)
                Assert.IsTrue(sim.PersonCardOf(kid2.id).family.Any(t => t.otherId == kid.id && t.kind == TieKind.Sibling));
        }

        [Test]
        public void OtherParentIsTheParentsPartnerAtTheBirth()
        {
            // over many seeds, every child born to a married parent knows both parents, and both list the child
            int checkedKids = 0;
            for (int seed = 1; seed <= 8; seed++)
            {
                var sim = Grown(seed, 60);
                foreach (var kid in sim.State.notables.Where(n => n.parentId >= 0))
                {
                    var birth = sim.State.chronicle.FirstOrDefault(e => e.kind == "birth" && e.notableId == kid.id);
                    if (birth == null || !birth.text.Contains(" and ")) continue;
                    int other = sim.OtherParentOf(kid);
                    Assert.GreaterOrEqual(other, 0, $"seed {seed}: {birth.text}");
                    StringAssert.Contains(sim.FindNotable(other).FullName, birth.text, $"seed {seed}");
                    Assert.IsTrue(sim.AllChildrenOf(other).Any(x => x.id == kid.id));
                    checkedKids++;
                }
            }
            Assert.Greater(checkedKids, 0, "some couples had children");
        }

        [Test]
        public void MadeUpAndSharedStoriesAreDerivedWithoutDice()
        {
            var sim = Grown(4, 20);
            var adults = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).ToList();
            Assume.That(adults.Count >= 3);
            var a = adults[0]; var b = adults[1]; var c = adults[2];
            a.rivalId = b.rivalId = -1; a.friendId = b.friendId = c.friendId = -1;
            a.partnerId = b.partnerId = c.partnerId = -1;
            sim.State.chronicle.Add(new ChronicleEntry { id = 900001, month = sim.State.month, kind = "rivalry", text = "x", salience = 0.58f, notableId = a.id, otherNotableId = b.id });
            sim.State.chronicle.Add(new ChronicleEntry { id = 900002, month = sim.State.month, kind = "reconcile", text = "y", salience = 0.55f, notableId = b.id, otherNotableId = a.id });
            Assert.IsTrue(sim.MadeUp(a, b));
            Assert.IsTrue(sim.TiesOf(a.id).Any(t => t.otherId == b.id && t.kind == TieKind.MadeUp));
            a.rivalId = b.id; b.rivalId = a.id;
            Assert.IsFalse(sim.MadeUp(a, b), "rivals again");

            int before = sim.SharedStories(a.id, c.id);
            for (int i = before; i < SimCore.SharedStoriesMin; i++)
                sim.State.chronicle.Add(new ChronicleEntry { id = 900010 + i, month = sim.State.month, kind = "term_feast", text = "z", salience = 0.5f, notableId = c.id, otherNotableId = a.id });
            bool otherTie = sim.TiesOf(a.id).Any(t => t.otherId == c.id && t.kind != TieKind.Shared);
            if (!otherTie) Assert.IsTrue(sim.TiesOf(a.id).Any(t => t.otherId == c.id && t.kind == TieKind.Shared && t.sharedStories >= SimCore.SharedStoriesMin));
        }

        [Test]
        public void GraphHasOneEdgePerPairWithTheStrongestTie()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var sim = Grown(seed, 50);
                var g = sim.RelationshipGraph();
                Assert.AreEqual(g.Count, g.Select(e => (System.Math.Min(e.a, e.b), System.Math.Max(e.a, e.b))).Distinct().Count(), $"seed {seed}");
                foreach (var e in g)
                {
                    Assert.IsTrue(sim.FindNotable(e.a).alive && sim.FindNotable(e.b).alive, "living only by default");
                    Assert.AreEqual(e.kind, e.kinds[0]);
                    Assert.IsFalse(string.IsNullOrEmpty(e.label));
                    Assert.That(e.strength, Is.InRange(0f, 1f));
                    if (e.kind == TieKind.Parent) Assert.IsTrue(sim.AllChildrenOf(e.a).Any(c => c.id == e.b), "parent → child");
                    if (e.kind == TieKind.Mentor) Assert.AreEqual(e.a, sim.FindNotable(e.b).mentorId, "mentor → apprentice");
                }
                foreach (var n in sim.State.notables.Where(x => x.alive))
                {
                    var r = sim.FindNotable(n.rivalId);
                    if (r != null && r.alive && n.partnerId != r.id)
                        Assert.IsTrue(g.Any(e => e.Involves(n.id) && e.Involves(r.id) && e.kind == TieKind.Rival), $"seed {seed}: rivals drawn as rivals");
                }
                // a focus graph is a subset of the full one (dead included)
                var any = sim.State.notables.First(x => x.alive);
                Assert.IsTrue(sim.RelationshipGraph(false, any.id).All(e => e.Involves(any.id)));
                Assert.AreEqual(g.Count, sim.RelationshipGraph().Count, "same answer twice");
            }
        }

        [Test]
        public void PinRules()
        {
            var sim = Grown(1, 10);
            var living = sim.State.notables.Where(n => n.alive).OrderBy(n => n.id).ToList();
            var a = living[0]; var b = living[1];
            Assert.IsTrue(sim.Pin(a.id));
            Assert.AreEqual(a, sim.Pinned);
            Assert.IsTrue(sim.PersonCardOf(a.id).pinned);
            Assert.IsTrue(sim.Pin(b.id), "pinning another moves the pin");
            Assert.AreEqual(b.id, sim.State.pinnedNotableId);
            Assert.IsFalse(sim.PersonCardOf(a.id).pinned);
            Assert.IsFalse(sim.TogglePin(b.id), "toggle unpins");
            Assert.AreEqual(-1, sim.State.pinnedNotableId);
            Assert.IsTrue(sim.TogglePin(b.id));
            Assert.IsFalse(sim.Pin(4242), "no such person");
            Assert.AreEqual(b.id, sim.State.pinnedNotableId, "a failed pin leaves the pin");

            // the pin stays on someone who dies, their card turns to a memorial, and pin time stops counting
            sim.Advance(sim.Config.StepHours * 3 + 1e-6);
            float hours = sim.PinnedHours(b.id);
            Assert.Greater(hours, 0f);
            b.lifespan = b.age + 0.01f;
            sim.Advance(1.0);
            Assert.IsFalse(b.alive);
            Assert.AreEqual(b, sim.Pinned);
            float atDeath = sim.PinnedHours(b.id);
            sim.Advance(2.0);
            Assert.AreEqual(atDeath, sim.PinnedHours(b.id), 1e-4, "the dead don't add pin time");
            Assert.IsFalse(sim.CanPin(b.id));
            Assert.IsFalse(sim.Pin(b.id), "the dead can't be pinned again");
            var card = sim.PersonCardOf(b.id);
            Assert.IsTrue(card.pinned);
            Assert.IsFalse(card.canPin);
            Assert.IsNotNull(card.memorial);
        }

        [Test]
        public void PinnedStoriesRankHigherOnReturn()
        {
            var sim = Grown(6, 20);
            long since = sim.State.month;
            sim.Advance(10);
            var people = sim.State.chronicle.Where(e => e.month > since && e.notableId >= 0 && SimCore.ShownAsCard(e)).Select(e => e.notableId).Distinct().ToList();
            Assume.That(people.Count > 0);
            int who = people[0];
            float Score() => sim.ReturnCardsCapped(since, 7).cards.Concat(sim.ReturnCardsCapped(since, 7).also)
                .Where(c => c.people.Contains(who)).Select(c => c.score).DefaultIfEmpty(0f).Max();
            float plain = Score();
            sim.State.pinnedNotableId = who;
            Assert.Greater(Score(), plain);
        }

        [Test]
        public void TheDeadGetAMemorial()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var sim = Grown(seed, 90);
                var dead = sim.State.notables.Where(n => !n.alive).ToList();
                Assert.Greater(dead.Count, 0, $"seed {seed}");
                foreach (var n in dead)
                {
                    var c = sim.PersonCardOf(n.id);
                    Assert.IsFalse(c.alive);
                    Assert.IsNotNull(c.memorial);
                    Assert.AreEqual(SimCore.YearNo(n.diedMonth), c.memorial.diedYear);
                    Assert.AreEqual((int)n.age, c.memorial.ageAtDeath);
                    Assert.IsNull(c.wish);
                    Assert.IsNull(c.role, "no role held now");
                    Assert.IsFalse(c.bestStories.Any(s => s.kind == "death" && s.entryId == sim.State.chronicle.LastOrDefault(e => e.kind == "death" && e.notableId == n.id)?.id));
                    Assert.IsTrue(c.memorial.rememberedBy.All(t => t.otherAlive));
                    Assert.IsTrue(c.family.Concat(c.friends).Where(t => !t.otherAlive).All(t => t.label.StartsWith("late ") || t.kind == TieKind.MadeUp || t.kind == TieKind.Shared));
                }
                // founders are legends
                foreach (var f in sim.State.notables.Where(n => n.parentId < 0 && n.bornMonth == 0))
                    CollectionAssert.Contains(sim.LegendReasons(f.id), "founder");
            }
        }

        [Test]
        public void WishRelicAndQuoteOnTheCard()
        {
            var sim = Grown(3, 8);
            var w = sim.DebugForceWish();
            Assume.That(w != null);
            var c = sim.PersonCardOf(w.notableId);
            Assert.AreEqual(w, c.wish);
            Assert.IsFalse(string.IsNullOrEmpty(c.wishText));
            foreach (var other in sim.State.notables.Where(n => n.id != w.notableId)) Assert.IsNull(sim.PersonCardOf(other.id).wish);

            var r = sim.DebugGiveRelic(RelicId.KeeperDoll);
            Assume.That(r != null && r.ownerId >= 0);
            Assert.IsTrue(sim.PersonCardOf(r.ownerId).relics.Any(x => x.id == RelicId.KeeperDoll && x.owner && !string.IsNullOrEmpty(x.label)));

            // a quote in their voice for the stage now; never a later stage's line; steady through the year
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 4f))
            {
                var q = sim.QuoteOf(n);
                Assert.IsNotNull(q, n.FullName);
                Assert.AreEqual(n.id, q.speakerId);
                if (q.lineNo > 0)
                {
                    var line = SimCore.LineBank.First(l => l.no == q.lineNo);
                    Assert.IsTrue(line.stage == 0 || line.stage == sim.BeliefStage);
                }
                Assert.AreEqual(q.text, sim.QuoteOf(n).text);
            }
            var chief = sim.HolderOf(RoleType.Chief) ?? sim.HolderOf(sim.State.era == Era.Camp ? RoleType.Chief : SimCore.LeaderOf(sim.State.era));
            if (chief != null && sim.State.era == Era.Camp)
            {
                var q = sim.QuoteOf(chief);
                var line = SimCore.LineBank.First(l => l.no == q.lineNo);
                Assert.That(line.speaker, Is.EqualTo("chief").Or.EqualTo("leader").Or.EqualTo(chief.trait).Or.EqualTo("elder"));
            }
            sim.RelicsOn = false;
            Assert.IsNull(sim.QuoteOf(sim.State.notables.First(x => x.alive)));
        }

        [Test]
        public void BuildingCardsChangesNothing()
        {
            var a = Grown(7, 30);
            var b = Grown(7, 30);
            foreach (var n in a.State.notables) { a.PersonCardOf(n.id); a.LegendReasons(n.id); a.QuoteOf(n); }
            a.RelationshipGraph(); a.RelationshipGraph(false);
            Assert.AreEqual(Fingerprint(b), Fingerprint(a));
            a.Advance(20); b.Advance(20);
            Assert.AreEqual(Fingerprint(b), Fingerprint(a), "the world goes on the same after cards were read");
        }
    }
}
