using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Notables' wishes for a gift (#83): they appear at a sensible rate, Give places the shaping and credits the
    /// wisher, an ignored wish lapses quietly without touching the economy or the mood.</summary>
    public class WishTests
    {
        static SimCore Grown(int seed, double years = 12)
        {
            var sim = NewSim(seed);
            sim.Advance(years);
            sim.State.shapingCharges = sim.ShapingMaxCharges;
            return sim;
        }

        [Test]
        public void WishesAppearAtASensibleRateAndOnlyOneIsOpen()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var sim = NewSim(seed);
                int maxOpen = 0;
                for (int m = 0; m < 20 * 12; m++)
                {
                    sim.Advance(1.0 / 12);
                    maxOpen = System.Math.Max(maxOpen, sim.State.wishes.Count(w => w.state == WishState.Open));
                }
                int n = sim.State.wishes.Count;
                Assert.LessOrEqual(maxOpen, 1, $"seed {seed}");
                Assert.GreaterOrEqual(n, 3, $"seed {seed}: {n} wishes in 20 years");
                Assert.LessOrEqual(n, 25, $"seed {seed}: {n} wishes in 20 years");
                foreach (var w in sim.State.wishes)
                {
                    Assert.IsNotNull(sim.FindNotable(w.notableId));
                    Assert.IsTrue(sim.State.chronicle.Any(e => e.id == w.entryId && e.kind == "wish" && e.notableId == w.notableId));
                }
            }
        }

        [Test]
        public void WishCardComesWithTheReturnStack()
        {
            var sim = NewSim(3);
            sim.Advance(6);
            var w = sim.DebugForceWish();
            Assert.IsNotNull(w);
            var stack = sim.ReturnCardsCapped(sim.State.month - 1, 5);
            Assert.IsTrue(stack.cards.Any(c => c.kind == CardKind.Wish && c.lead.id == w.entryId));
            Assert.AreEqual(1, stack.cards.Count(c => c.kind == CardKind.Wish));
        }

        [Test]
        public void GivePlacesTheShapingAndCreditsTheWisher()
        {
            var sim = Grown(5);
            var w = sim.DebugForceWish(SimCore.NudgeKind.Grove);
            Assert.IsNotNull(w);
            float charges = sim.ShapingCharges;
            Assert.AreEqual(SimCore.ShapeFail.None, sim.CanGiveWish(w.id));
            var r = sim.GiveWish(w.id);
            Assert.IsTrue(r.Ok, r.fail.ToString());
            Assert.AreEqual(WishState.Given, w.state);
            Assert.AreEqual(w.notableId, r.nudge.creditNotableId);
            Assert.AreEqual(charges - 1f, sim.ShapingCharges, 1e-4f);
            sim.Advance(3.05);
            Assert.IsTrue(r.nudge.credited);
            var e = sim.AnswerOf(r.nudge);
            Assert.IsNotNull(e);
            Assert.AreEqual(w.notableId, e.notableId);
            // a given wish is not a card any more, and cannot be given twice
            Assert.IsFalse(sim.ReturnCardsCapped(0, 7).cards.Any(c => c.kind == CardKind.Wish && c.lead.id == w.entryId));
            Assert.IsFalse(sim.GiveWish(w.id).Ok);
        }

        [Test]
        public void GiveWithoutAChargeFailsAndTheWishStaysOpen()
        {
            var sim = Grown(5);
            var w = sim.DebugForceWish(SimCore.NudgeKind.Grove);
            sim.State.shapingCharges = 0f;
            var r = sim.GiveWish(w.id);
            Assert.AreEqual(SimCore.ShapeFail.NoCharge, r.fail);
            Assert.AreEqual(WishState.Open, w.state);
        }

        [Test]
        public void AnIgnoredWishLapsesQuietlyAndTheEconomyIsTheSame()
        {
            var on = NewSim(9);
            var off = NewSim(9);
            off.WishesOn = false;
            on.Advance(1); off.Advance(1);
            var w = on.DebugForceWish(SimCore.NudgeKind.Grove);
            Assert.IsNotNull(w);
            on.WishesOn = false; // no new wishes: follow just this one
            on.Advance(SimCore.WishLapseMonths / 12.0 + 0.1);
            off.Advance(SimCore.WishLapseMonths / 12.0 + 0.1);
            Assert.AreEqual(WishState.Lapsed, w.state);
            Assert.IsFalse(on.ReturnCardsCapped(0, 7).cards.Any(c => c.kind == CardKind.Wish && c.lead.id == w.entryId));
            Assert.AreEqual(off.State.mood, on.State.mood, 1e-6f);
            Assert.AreEqual(off.State.food, on.State.food, 1e-3f);
            Assert.AreEqual(off.State.population, on.State.population);
            Assert.AreEqual(off.State.buildings.Count, on.State.buildings.Count);
        }

        [Test]
        public void WishingNeverChangesTheEconomy()
        {
            var on = NewSim(11);
            var off = NewSim(11);
            off.WishesOn = false;
            on.Advance(15); off.Advance(15);
            Assert.Greater(on.State.wishes.Count, 0);
            Assert.AreEqual(off.State.mood, on.State.mood, 1e-6f);
            Assert.AreEqual(off.State.population, on.State.population);
            Assert.AreEqual(off.State.buildings.Count, on.State.buildings.Count);
        }

        [Test]
        public void ShapingTheGiftYourselfMeetsTheWish()
        {
            var sim = Grown(5);
            var w = sim.DebugForceWish(SimCore.NudgeKind.Grove);
            float x = w.x + 0.3f, z = w.z;
            sim.Settlement?.OpenSpotNear(ref x, ref z, 0.5f);
            Assert.IsTrue(sim.PlaceShaping(SimCore.NudgeKind.Grove, x, z).Ok);
            sim.Advance(1.0 / 12);
            Assert.AreEqual(WishState.Met, w.state);
        }

        [Test]
        public void ADeadWisherLapsTheWish()
        {
            var sim = Grown(5);
            var w = sim.DebugForceWish();
            sim.FindNotable(w.notableId).alive = false;
            sim.Advance(1.0 / 12);
            Assert.AreEqual(WishState.Lapsed, w.state);
        }

        [Test]
        public void WishesSurviveASaveAndLoad()
        {
            var sim = NewSim(4);
            sim.Advance(5);
            var w = sim.DebugForceWish(SimCore.NudgeKind.Rain);
            string json = UnityEngine.JsonUtility.ToJson(sim.State);
            var back = UnityEngine.JsonUtility.FromJson<SimState>(json);
            Assert.AreEqual(sim.State.wishes.Count, back.wishes.Count);
            Assert.AreEqual(w.kind, back.wishes.Last().kind);
            Assert.AreEqual(w.entryId, back.wishes.Last().entryId);
        }
    }
}
