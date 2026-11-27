using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#774: the family tree (tree A: the whole family of a founding couple, top-down, the dead kept for everyone, branches that
    /// fold to a chip) and the person card's data for card B (one story timeline, a line per relation). All pure reads.</summary>
    public class FamilyTreeTests
    {
        const int Y = SimConfig.MonthsPerYear;
        // ids: the founders A and B; their children C, D (died young), E; X married C; F is C and X's child, G is E's
        const int A = 1, B = 2, C = 3, D = 4, E = 5, X = 10, F = 6, G = 7, Lone = 20;

        /// <summary>A hand-made family in an empty box: no chronicle, so the other parent comes from the partner at the birth.</summary>
        static SimCore Family()
        {
            var sim = NewSim(3);
            var s = sim.State;
            s.notables.Clear();
            s.chronicle.Clear();
            s.pinnedNotableId = -1;
            s.month = 120 * Y;
            Notable P(int id, string name, int parent, int bornYear, int partner = -1, bool alive = true, int diedYear = -1) =>
                new Notable { id = id, name = name, family = "Hollin", trait = "the kind", parentId = parent, bornMonth = bornYear * Y, partnerId = partner,
                              alive = alive, diedMonth = alive ? -1 : diedYear * Y, age = alive ? 120 - bornYear : diedYear - bornYear, lifespan = 500f };
            s.notables.Add(P(A, "Bram", -1, 0, B));
            s.notables.Add(P(B, "Edda", -1, 0, A));
            s.notables.Add(P(C, "Tilda", A, 30, X));
            s.notables.Add(P(D, "Moss", A, 32, alive: false, diedYear: 40));
            s.notables.Add(P(E, "Pim", A, 35));
            var x = P(X, "Ulf", -1, 25, C); x.family = "Fenn"; s.notables.Add(x);
            s.notables.Add(P(F, "Nell", C, 60));
            s.notables.Add(P(G, "Odo", E, 66));
            s.notables.Add(P(Lone, "Cora", -1, 50)); // nobody to draw: no tree
            s.nextId = 100;
            return sim;
        }

        [Test]
        public void GenerationsGoTopDownFromTheFoundingCouple()
        {
            var t = Family().FamilyTreeOf(A);
            Assert.NotNull(t);
            Assert.AreEqual(8, t.people, "everyone but the lone stranger");
            Assert.AreEqual(3, t.rows);
            foreach (var (id, gen) in new[] { (A, 1), (B, 1), (C, 2), (D, 2), (E, 2), (X, 2), (F, 3), (G, 3) })
                Assert.AreEqual(gen, t.Node(id).gen, "generation of " + id);
            Assert.AreEqual(new[] { 1, 2, 3 }, t.gens.Select(g => g.gen).ToArray());
            Assert.AreEqual(new[] { 1, 31, 61 }, t.gens.Select(g => g.year).ToArray(), "the gutter: the year the generation's first was born");
            Assert.AreEqual("Hollin", t.familyName);
            Assert.IsNull(t.Node(Lone));
        }

        [Test]
        public void PartnersStandBesideTheirOwnAndChildrenHangOldestFirst()
        {
            var t = Family().FamilyTreeOf(A);
            Assert.IsTrue(t.Node(B).x > t.Node(A).x && !t.Node(B).blood && t.Node(B).mateOf == A, "Edda beside Bram");
            Assert.AreEqual(t.Node(C).x + 1f, t.Node(X).x, 1e-4, "Ulf beside Tilda");
            Assert.AreEqual(C, t.Node(X).mateOf);
            Assert.IsTrue(t.couples.Any(c => c.a == C && c.b == X));
            Assert.Less(t.Node(C).x, t.Node(D).x);
            Assert.Less(t.Node(D).x, t.Node(E).x);
            var under = t.branches.First(b => b.ownerId == A);
            Assert.AreEqual(3, under.childX.Count);
            Assert.AreEqual((t.Node(A).x + t.Node(B).x) / 2f, under.fromX, 1e-4, "the drop leaves from between the couple");
            foreach (var row in t.nodes.GroupBy(n => n.row))
            {
                var xs = row.Select(n => n.x).OrderBy(v => v).ToList();
                for (int i = 1; i < xs.Count; i++) Assert.GreaterOrEqual(xs[i] - xs[i - 1], 1f - 1e-4, "no two faces on one spot");
            }
            Assert.That(t.nodes.Max(n => n.x), Is.LessThanOrEqualTo(t.width));
        }

        [Test]
        public void TheDeadStayInTheTreeForEveryone()
        {
            var sim = Family();
            var moss = sim.FamilyTreeOf(A).Node(D);
            Assert.NotNull(moss, "Moss died young and is no legend, and stays");
            Assert.IsFalse(moss.alive);
            Assert.AreEqual(41, moss.diedYear);
            sim.FindNotable(A).alive = false;
            sim.FindNotable(A).diedMonth = 90 * Y;
            Assert.NotNull(sim.FamilyTreeOf(A).Node(A), "a dead founder still heads the tree");
        }

        [Test]
        public void ABranchFoldsToAChipAndAnEmptyGenerationDropsOut()
        {
            var sim = Family();
            var t = sim.FamilyTreeOf(A, new HashSet<int> { C });
            Assert.IsNull(t.Node(F), "Tilda's child is in the chip");
            var b = t.branches.First(x => x.ownerId == C);
            Assert.IsTrue(b.folded);
            Assert.AreEqual(1, b.hidden);
            Assert.AreEqual(3, t.rows, "Pim's child still fills the third row");
            Assert.AreEqual(8, t.people, "folding hides, it doesn't forget");
            var all = sim.FamilyTreeOf(A, new HashSet<int> { A });
            Assert.AreEqual(2, all.rows, "the founders and their chip");
            Assert.AreEqual(6, all.branches.Single().hidden);
            Assert.AreEqual(2, all.nodes.Count);
            var none = sim.FamilyTreeOf(A, new HashSet<int> { F }); // a leaf has nothing to fold
            Assert.NotNull(none.Node(F));
        }

        [Test]
        public void EachFamilyHasOneTreeAndThePartnersWhoMarriedInHaveNone()
        {
            var sim = Family();
            Assert.AreEqual(new[] { A }, sim.FamilyTreeRoots().ToArray());
            Assert.AreEqual(A, sim.FamilyTreeRootOf(F));
            Assert.AreEqual(A, sim.FamilyTreeRootOf(X), "Ulf is found in the tree he married into");
            Assert.AreEqual(-1, sim.FamilyTreeRootOf(Lone));
            Assert.AreEqual(new[] { X }, sim.MatesOf(C).ToArray());
        }

        [Test]
        public void TheBookFamilyPagesAreTheTrees()
        {
            var sim = Family();
            var pages = sim.BookPages().Where(p => p.kind == BookPageKind.Family).ToList();
            Assert.AreEqual(1, pages.Count);
            Assert.AreEqual(SimCore.BookTreeKey(A), pages[0].key);
            Assert.AreEqual("The Hollin family", pages[0].title);
            Assert.AreEqual(8, pages[0].tree.people);
        }

        [Test]
        public void ReadingTheTreeChangesNothing()
        {
            var sim = NewSim(4);
            sim.Advance(60);
            string before = $"{sim.State.month}|{sim.State.rng.state}|{sim.State.storyRng.state}|{sim.State.notables.Count}|{sim.State.chronicle.Count}";
            foreach (int r in sim.FamilyTreeRoots())
            {
                var t = sim.FamilyTreeOf(r);
                Assert.AreEqual(t.people, t.nodes.Count, "unfolded, everyone has a face");
                Assert.AreEqual(t.nodes.Count, t.nodes.Select(n => n.id).Distinct().Count(), "one face each");
                foreach (var n in t.nodes.Where(n => !n.blood)) Assert.NotNull(t.Node(n.mateOf));
            }
            Assert.AreEqual(before, $"{sim.State.month}|{sim.State.rng.state}|{sim.State.storyRng.state}|{sim.State.notables.Count}|{sim.State.chronicle.Count}");
        }

        [Test]
        public void TheStoryTimelineFoldsRecentAndBestAndMarksTheBest()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var sim = NewSim(seed);
                sim.Advance(50);
                foreach (var n in sim.State.notables)
                {
                    var c = sim.PersonCardOf(n.id);
                    Assert.LessOrEqual(c.timeline.Count, SimCore.CardTimeline);
                    for (int i = 1; i < c.timeline.Count; i++) Assert.GreaterOrEqual(c.timeline[i - 1].month, c.timeline[i].month, "newest first");
                    CollectionAssert.IsSubsetOf(c.bestStories.Select(s => s.entryId).ToList(), c.timeline.Select(s => s.entryId).ToList(), "the best are in it");
                    Assert.AreEqual(c.bestStories.Count, c.timeline.Count(s => s.toldMost));
                    foreach (var st in c.recentStories) Assert.IsTrue(c.timeline.Any(s => s.entryId == st.entryId), "the recent are in it");
                    foreach (var t in c.family.Concat(c.friends).Concat(c.rivals).Concat(c.mentors))
                    {
                        if (t.story == null) continue;
                        var e = sim.State.chronicle.First(x => x.id == t.story.entryId);
                        Assert.IsTrue((e.notableId == n.id && e.otherNotableId == t.otherId) || (e.notableId == t.otherId && e.otherNotableId == n.id), "the line names both");
                    }
                }
            }
        }

        [Test]
        public void ARelationsLineIsTheirMostToldEntryTogether()
        {
            var sim = Family();
            var s = sim.State;
            s.chronicle.Add(new ChronicleEntry { id = 500, month = 70 * Y, kind = "friendship", text = "Tilda and Pim mended the racks.", salience = 0.4f, notableId = C, otherNotableId = E });
            s.chronicle.Add(new ChronicleEntry { id = 501, month = 80 * Y, kind = "friendship", text = "Pim carried Tilda home.", salience = 0.8f, notableId = E, otherNotableId = C });
            s.chronicle.Add(new ChronicleEntry { id = 502, month = 90 * Y, kind = "friendship", text = "Tilda and Pim sat by the fire.", salience = 0.5f, notableId = C, otherNotableId = E });
            var c = sim.PersonCardOf(C);
            var pim = c.family.First(t => t.otherId == E);
            Assert.NotNull(pim.story);
            Assert.AreEqual(501, pim.story.entryId);
        }
    }
}
