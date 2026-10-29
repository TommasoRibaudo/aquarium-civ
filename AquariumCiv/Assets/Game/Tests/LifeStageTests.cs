using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Life stages for 110–160-year notables (decisions.md 2026-10-29 "Life stages": "Only stretch elderly").
    /// The elder stages doubled with the lifespan; adulthood (16), parenthood (20–40) and marriage (≤ 50) did not.</summary>
    public class LifeStageTests
    {
        const float ProtoLifespanMin = 55f, ProtoLifespanMax = 80f, ProtoElderAge = 45f, ProtoEldestStoryAge = 60f;

        /// <summary>A sim whose notables are exactly these ages (long lifespans, so nobody dies mid-test).</summary>
        static SimCore WithPeople(int seed, params float[] ages)
        {
            var sim = NewSim(seed);
            var s = sim.State;
            s.notables.Clear();
            for (int i = 0; i < ages.Length; i++)
                s.notables.Add(new Notable { id = s.NextId(), name = "P" + i, family = "F" + i, trait = "the kind", age = ages[i], lifespan = 500f });
            return sim;
        }

        static List<ChronicleEntry> ForceStories(SimCore sim, int times, string kind)
        {
            var found = new List<ChronicleEntry>();
            for (int i = 0; i < times; i++)
            {
                int before = sim.State.chronicle.Count;
                sim.ForceStory();
                found.AddRange(sim.State.chronicle.Skip(before).Where(e => e.kind == kind));
                foreach (var n in sim.State.notables) { n.mentorId = -1; n.partnerId = -1; } // keep everyone eligible
            }
            return found;
        }

        [Test]
        public void ElderStagesScaleWithTheLifespan()
        {
            var c = new SimConfig();
            float scale = c.lifespanMin / ProtoLifespanMin;
            Assert.AreEqual(scale, c.lifespanMax / ProtoLifespanMax, 1e-4f, "the lifespan scaled evenly");
            Assert.AreEqual(ProtoElderAge * scale, c.elderAge, 1e-3f, "elders start at the same share of life as before");
            Assert.AreEqual(ProtoEldestStoryAge * scale, c.eldestStoryAge, 1e-3f, "the eldest's story too");
            Assert.Less(c.elderAge, c.lifespanMin, "everyone lives long enough to be an elder");
            Assert.Less(c.eldestStoryAge, c.lifespanMax, "some live long enough to tell the founding story");
        }

        [Test]
        public void MastersFromFiftyTakeApprentices()
        {
            // User, 2026-10-29: "apprentices 50". Masters in their prime mentor, not only the elders.
            var sim = WithPeople(7, 40f, 55f, 10f, 12f);
            var apprenticed = ForceStories(sim, 300, "apprentice");
            Assert.IsNotEmpty(apprenticed, "the 55-year-old should take an apprentice sometimes");
            int masterId = sim.State.notables.First(n => n.age == 55f).id;
            Assert.IsTrue(apprenticed.All(e => e.otherNotableId == masterId), "only the master mentors");

            var noMasters = WithPeople(7, 40f, 49f, 10f, 12f);
            Assert.IsEmpty(ForceStories(noMasters, 300, "apprentice"), "nobody under the master age mentors");
        }

        [Test]
        public void TheEldestTellsTheFoundingStoryOnlyWhenOldEnough()
        {
            var young = WithPeople(3, 100f, 40f, 30f);
            young.State.month = 12 * 50; // winter
            Assert.IsEmpty(ForceStories(young, 300, "elder"), "100 is no longer old enough");

            var old = WithPeople(3, 125f, 40f, 30f);
            old.State.month = 12 * 50;
            var told = ForceStories(old, 300, "elder");
            Assert.IsNotEmpty(told);
            Assert.IsTrue(told.All(e => e.notableId == old.State.notables[0].id));
        }

        [Test]
        public void MarriageAgeIsUnchanged()
        {
            Assert.IsNotEmpty(ForceStories(WithPeople(5, 45f, 50f), 300, "wedding"), "people up to 50 still marry");
            Assert.IsEmpty(ForceStories(WithPeople(5, 51f, 60f), 300, "wedding"), "...and not after");
        }

        [Test]
        public void ParenthoodAgeIsUnchanged()
        {
            // Five people (the minimum, so nobody new rises) over ten years.
            var over = WithPeople(11, 41f, 45f, 50f, 55f, 60f);
            over.Advance(10);
            Assert.IsFalse(over.State.chronicle.Any(e => e.kind == "birth"), "no children after 40");

            var parents = WithPeople(11, 20f, 22f, 25f, 28f, 30f);
            parents.Advance(10);
            Assert.IsTrue(parents.State.chronicle.Any(e => e.kind == "birth"), "children from 20 to 40");
        }

        [Test]
        public void AdulthoodIsUnchanged()
        {
            var sim = WithPeople(13, 15.5f, 30f, 30f, 30f, 30f);
            sim.Advance(1);
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "coming_of_age" && e.notableId == sim.State.notables[0].id), "adult at 16");
        }

        /// <summary>The death roll: a notable dies in the year their age reaches the lifespan drawn at birth, and of
        /// nothing else. So every death is of old age, between 110 and 160.</summary>
        [Test]
        public void NotablesDieOfOldAgeBetween110And160()
        {
            var deathAges = new List<float>();
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = NewSim(seed);
                sim.Advance(250); // 250 game years: the founders and their children's generation die
                var c = sim.Config;
                foreach (var n in sim.State.notables)
                {
                    Assert.That(n.lifespan, Is.InRange(c.lifespanMin, c.lifespanMax), n.FullName);
                    if (n.alive) { Assert.Less(n.age, n.lifespan, $"{n.FullName} outlived their lifespan"); continue; }
                    Assert.GreaterOrEqual(n.age, n.lifespan, $"{n.FullName} died young");
                    Assert.Less(n.age - n.lifespan, sim.Config.notableAgeFactor + 1e-3f, $"{n.FullName} died late");
                    deathAges.Add(n.age);
                }
            }
            Assert.Greater(deathAges.Count, 30, "enough deaths to judge");
            TestContext.WriteLine($"{deathAges.Count} deaths, ages {deathAges.Min():0}–{deathAges.Max():0}, mean {deathAges.Average():0.0}");
            Assert.That(deathAges.Min(), Is.GreaterThanOrEqualTo(110f));
            Assert.That(deathAges.Max(), Is.LessThanOrEqualTo(161f));
        }
    }
}
