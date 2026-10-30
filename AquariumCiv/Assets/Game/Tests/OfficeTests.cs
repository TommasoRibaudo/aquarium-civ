using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The role-holder's trait colours their term (#28, notable-roles.md §4): term lines, codas on a few story
    /// lines, a flavour readout for the planner and a teaser for the evening talk. Story and behaviour only: the economy,
    /// the dice, the notables and the roles are the same with the flavour off.</summary>
    public class OfficeTests
    {
        static IEnumerable<SimCore> Months(SimCore sim, int years)
        {
            for (int m = 0; m < years * SimConfig.MonthsPerYear; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
                yield return sim;
            }
        }

        static bool IsTerm(ChronicleEntry e) => e.kind.StartsWith("term_");

        static SimCore Sim(int seed, bool flavour)
        {
            var sim = NewSim(seed);
            sim.TermFlavourOn = flavour;
            return sim;
        }

        /// <summary>A camp whose chief has this trait and stays in office (young, long-lived).</summary>
        static (SimCore sim, Notable chief) CampWithChief(int seed, string trait)
        {
            var sim = NewSim(seed);
            var chief = sim.HolderOf(RoleType.Chief);
            chief.trait = trait; chief.age = 20f; chief.lifespan = 200f;
            return (sim, chief);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(77)]
        [TestCase(1234)]
        public void TheEconomyIsTheSameWithAndWithoutTheFlavour(int seed)
        {
            var off = Sim(seed, false); var on = Sim(seed, true);
            foreach (var _ in Months(off, 120)) { }
            foreach (var _ in Months(on, 120)) { }
            Assert.AreEqual(Era.Town, on.State.era);

            int lines = on.State.chronicle.Count(IsTerm);
            TestContext.WriteLine($"seed {seed}: {lines} term lines, {on.State.chronicle.Count(e => !IsTerm(e) && e.text != off.State.chronicle.First(o => o.id == e.id).text)} codas");
            Assert.Greater(lines, 10, "the flavour did something");
            Assert.AreEqual(0, off.State.chronicle.Count(IsTerm), "off means off");

            // Same-runtime: everything but the chronicle's term lines and codas is identical.
            Assert.AreEqual(JobTests.Economy(off.State), JobTests.Economy(on.State), "economy, dice and notables");
            var stripped = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(on.State));
            stripped.chronicle.RemoveAll(IsTerm);
            Assert.AreEqual(off.State.chronicle.Select(e => e.id + e.kind), stripped.chronicle.Select(e => e.id + e.kind), "the other lines happen as before");
            for (int i = 0; i < stripped.chronicle.Count; i++)
            {
                StringAssert.StartsWith(off.State.chronicle[i].text, stripped.chronicle[i].text, "a coda only adds a clause");
                stripped.chronicle[i].text = off.State.chronicle[i].text;
            }
            stripped.termLineNextId = off.State.termLineNextId;
            Assert.AreEqual(JsonUtility.ToJson(off.State), JsonUtility.ToJson(stripped), "the whole box: notables, links, jobs, roles, ids, dice");
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void InACenturyTermLinesComeFromTheHoldersTrait(int seed)
        {
            var sim = NewSim(seed);
            var byTrait = new Dictionary<string, int>();
            sim.Chronicled += e =>
            {
                if (!IsTerm(e)) return;
                var h = sim.FindNotable(e.notableId);
                Assert.IsNotNull(sim.RoleOf(h), $"{e.text}: told by a role-holder");
                CollectionAssert.Contains(SimCore.TermKindsOf(h.trait).ToList(), e.kind, $"{h.trait}: {e.text}");
                Assert.AreEqual(sim.SeatBuilding(sim.RoleOf(h))?.id ?? -1, e.buildingId, "fly-to the seat");
                byTrait.TryGetValue(h.trait, out int n);
                byTrait[h.trait] = n + 1;
            };
            foreach (var _ in Months(sim, 100)) { }
            var terms = sim.State.chronicle.Where(IsTerm).ToList();
            foreach (var e in terms.Take(12)) TestContext.WriteLine($"{e.month / 12,4} {e.kind}: {e.text}");
            TestContext.WriteLine("by trait: " + string.Join(", ", byTrait.Select(kv => $"{kv.Key} {kv.Value}")));
            Assert.GreaterOrEqual(terms.Count, 10);
            Assert.GreaterOrEqual(byTrait.Count, 3, "several holders' traits show");
            Assert.IsTrue(terms.All(e => !e.text.Contains("{") && e.salience == 0.5f && e.id >= SimCore.TermLineIdBase));
            Assert.AreEqual(sim.State.chronicle.Count, sim.State.chronicle.Select(e => e.id).Distinct().Count(), "ids are unique");
            Assert.Less(terms.Count, sim.State.chronicle.Count / 3, "term lines don't crowd out the chronicle");
        }

        [Test]
        public void EachTraitTellsItsOwnTerm()
        {
            var counts = new Dictionary<string, Dictionary<string, int>>();
            foreach (var trait in NameData.Traits)
            {
                counts[trait] = new Dictionary<string, int>();
                for (int seed = 1; seed <= 3; seed++)
                {
                    var (sim, chief) = CampWithChief(seed, trait);
                    sim.Advance(45);
                    foreach (var e in sim.State.chronicle.Where(e => IsTerm(e) && e.notableId == chief.id))
                    {
                        counts[trait].TryGetValue(e.kind, out int n);
                        counts[trait][e.kind] = n + 1;
                    }
                }
                TestContext.WriteLine($"{trait}: {string.Join(", ", counts[trait].Select(kv => $"{kv.Key} {kv.Value}"))}");
                Assert.GreaterOrEqual(counts[trait].Values.Sum(), 5, $"{trait}: a chief's term over 3 × 45 years");
                CollectionAssert.IsSubsetOf(counts[trait].Keys.ToList(), SimCore.TermKindsOf(trait).ToList(), trait);
            }
            // The user's examples: the cheerful leader feasts and sings, the stern one keeps it tidy, the quarrelsome rows.
            int Of(string trait, params string[] kinds) => kinds.Sum(k => counts[trait].TryGetValue(k, out int n) ? n : 0);
            Assert.Greater(Of("the singer", "term_feast", "term_song"), 0);
            Assert.AreEqual(0, Of("the singer", "term_tidy", "term_row"));
            Assert.Greater(Of("the grump", "term_tidy"), 0);
            Assert.AreEqual(0, Of("the grump", "term_feast", "term_song"));
            Assert.Greater(Of("the stubborn", "term_row") + Of("the gossip", "term_row") + Of("the grump", "term_row"), 0);
            Assert.AreEqual(0, Of("the kind", "term_row"));
        }

        [Test]
        public void TheFlavourReadout()
        {
            var (grumpy, g) = CampWithChief(3, "the grump");
            var f = grumpy.CurrentTermFlavor();
            Assert.Greater(f.tidiness, 0.5f); Assert.Greater(f.quiet, 0.5f); Assert.Less(f.feasts, 0.5f);
            Assert.AreEqual(f.tidiness, grumpy.TermFlavorOf(g).tidiness, "the camp's only office is the chief's");

            var (merry, _) = CampWithChief(3, "the singer");
            f = merry.CurrentTermFlavor();
            Assert.Greater(f.music, 0.5f); Assert.Greater(f.feasts, 0.5f);
            Assert.IsTrue(new[] { f.feasts, f.music, f.tidiness, f.quiet, f.quarrels, f.kindness, f.invention, f.adventure, f.caution,
                f.idleness, f.gardening, f.mirth, f.gathering, f.wonder }.All(v => v >= 0f && v <= 1f));

            var nobody = grumpy.State.notables.FirstOrDefault(n => n.alive && grumpy.RoleOf(n) == null);
            if (nobody != null) Assert.AreEqual(0.5f, grumpy.TermFlavorOf(nobody).tidiness, "no role, no colour");
            grumpy.TermFlavourOn = false;
            Assert.AreEqual(0.5f, grumpy.CurrentTermFlavor().tidiness, "the switch turns it off");

            // A town: the leader counts in full, every other office at half; still within 0..1.
            var town = NewSim(7);
            foreach (var _ in Months(town, 120)) if (town.State.era == Era.Town) break;
            var tf = town.CurrentTermFlavor();
            Assert.IsTrue(new[] { tf.feasts, tf.tidiness, tf.quarrels, tf.wonder }.All(v => v >= 0f && v <= 1f));
        }

        [Test]
        public void CodasColourExistingLinesOnly()
        {
            var (sim, chief) = CampWithChief(4, "the worrier");
            uint story = sim.State.storyRng.state, rng = sim.State.rng.state, econ = sim.State.econRng.state;
            var e = sim.Chronicle("storm", "A great storm rattled the glass.", 0.5f);
            Assert.AreEqual($"A great storm rattled the glass. {chief.FullName} the chief had them all counted in before the first gust.", e.text);
            Assert.AreEqual((story, rng, econ), (sim.State.storyRng.state, sim.State.rng.state, sim.State.econRng.state), "no dice");
            Assert.AreEqual("Fog filled the glass for a week.", sim.Chronicle("fog", "Fog filled the glass for a week.", 0.4f).text, "no coda for fog");

            chief.trait = "the brave";
            var other = sim.State.notables.First(n => n.alive && n != chief);
            StringAssert.Contains("led the search party", sim.Chronicle("lost", "X got lost.", 0.6f, other.id).text);
            Assert.AreEqual("X got lost.", sim.Chronicle("lost", "X got lost.", 0.6f, chief.id).text, "not about the holder themself");

            sim.TermFlavourOn = false;
            Assert.AreEqual("X got lost.", sim.Chronicle("lost", "X got lost.", 0.6f, other.id).text, "the switch turns it off");
        }

        [Test]
        public void TeaserAndDebugCheat()
        {
            var (sim, chief) = CampWithChief(5, "the kind");
            var t = sim.OfficeTeaser();
            Assert.AreEqual($"{chief.FullName} has been cooking enough stew for the whole camp.", t);

            uint story = sim.State.storyRng.state;
            var e = sim.DebugTermStory(RoleType.Chief);
            Assert.IsNotNull(e);
            Assert.AreEqual(chief.id, e.notableId);
            CollectionAssert.Contains(SimCore.TermKindsOf("the kind").ToList(), e.kind);
            Assert.AreEqual(sim.State.chronicle.Last(), e);
            Assert.AreEqual(story, sim.State.storyRng.state, "no dice");
            Assert.IsNull(sim.DebugTermStory(RoleType.Lord), "no lord in a camp");

            sim.TermFlavourOn = false;
            Assert.IsNull(sim.OfficeTeaser());
            Assert.IsNull(sim.DebugTermStory(RoleType.Chief));
        }

        [Test]
        public void TermFlavourIsDeterministic()
        {
            var a = NewSim(21); var b = NewSim(21);
            foreach (var _ in Months(a, 90)) { }
            foreach (var _ in Months(b, 90)) { }
            Assert.Greater(a.State.chronicle.Count(IsTerm), 5);
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b.State));
            Assert.AreEqual(JsonUtility.ToJson(a.CurrentTermFlavor()), JsonUtility.ToJson(b.CurrentTermFlavor()));
            Assert.AreEqual(a.OfficeTeaser(), b.OfficeTeaser());
        }

        [Test]
        public void TermFlavourSurvivesASave()
        {
            var sim = NewSim(7);
            foreach (var _ in Months(sim, 50)) { }
            Assert.Greater(sim.State.chronicle.Count(IsTerm), 0);
            var loaded = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual(sim.State.termLineNextId, loaded.State.termLineNextId);
            Assert.AreEqual(JsonUtility.ToJson(sim.CurrentTermFlavor()), JsonUtility.ToJson(loaded.CurrentTermFlavor()));
            Assert.AreEqual(sim.OfficeTeaser(), loaded.OfficeTeaser());
            foreach (var _ in Months(sim, 30)) { }
            foreach (var _ in Months(loaded, 30)) { }
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(loaded.State), "a loaded box goes on the same way");

            // A save from before #28 has no counter: term lines still get their own ids, never a shared one.
            var old = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            old.State.termLineNextId = 0;
            int nextShared = old.State.nextId;
            var leader = old.State.roles.First(r => r.holderId >= 0);
            ChronicleEntry e = null;
            for (int i = 0; i < 400 && e == null; i++) { e = old.DebugTermStory(leader.role); if (e == null) old.Step(); } // #61: told kinds rest longer
            Assert.IsNotNull(e);
            Assert.GreaterOrEqual(e.id, SimCore.TermLineIdBase);
            Assert.AreEqual(old.State.chronicle.Count, old.State.chronicle.Select(x => x.id).Distinct().Count(), "ids are unique");
            Assert.LessOrEqual(nextShared, old.State.nextId);
        }
    }
}
