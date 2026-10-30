using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Shaping by placement (#82): rain, grove, channel and herd are answered in 1–3 years and credited; shared
    /// charges refilled by cleaning and over time while happy; take back until used; natural caps; tools by era.</summary>
    public class ShapingTests
    {
        static SimCore Grown(int seed)
        {
            var sim = NewSim(seed);
            sim.Advance(20);
            sim.State.shapingCharges = sim.Config.shapingStartCharges; // twenty happy years have filled the charges to the cap
            return sim;
        }

        static SimCore Village(int seed)
        {
            var sim = Grown(seed);
            Assert.IsTrue(sim.ForceNextEra());
            return sim;
        }

        /// <summary>A dry open spot near (x, z).</summary>
        static void Dry(SimCore sim, ref float x, ref float z) => Assert.IsTrue(sim.Settlement.OpenSpotNear(ref x, ref z, 0.5f));

        /// <summary>A spot in the pond.</summary>
        static (float x, float z) Wet(SimCore sim)
        {
            var t = TerrainSpec.Preset(sim.TerrainPreset);
            for (float x = -Terrain.HX; x < Terrain.HX; x += 0.2f)
                for (float z = -Terrain.HZ; z < Terrain.HZ; z += 0.2f)
                    if (SimCore.WetSpot(t, x, z)) return (x, z);
            Assert.Fail("no water in the box");
            return (0, 0);
        }

        [TestCase(SimCore.NudgeKind.Rain)]
        [TestCase(SimCore.NudgeKind.Grove)]
        [TestCase(SimCore.NudgeKind.Herd)]
        [TestCase(SimCore.NudgeKind.Channel)]
        public void EachToolIsAnsweredWithinThreeYearsAndCredited(string kind)
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = kind == SimCore.NudgeKind.Channel ? Village(seed) : Grown(seed);
                float x = 0.6f, z = -0.4f;
                Dry(sim, ref x, ref z);
                var r = sim.PlaceShaping(kind, x, z);
                Assert.IsTrue(r.Ok, $"{kind} seed {seed}: {r.fail}");
                var n = r.nudge;
                sim.Advance(3.05);
                Assert.IsTrue(n.credited, $"{kind} seed {seed}: not answered in 3 years");
                long wait = n.answerMonth - n.month;
                Assert.GreaterOrEqual(wait, 12, $"{kind} seed {seed}: answered after {wait} months");
                Assert.LessOrEqual(wait, 36, $"{kind} seed {seed}: answered after {wait} months");
                var line = sim.AnswerOf(n);
                Assert.IsNotNull(line, $"{kind} seed {seed}: no credit line");
                StringAssert.Contains("Keeper", line.text);
                Assert.GreaterOrEqual(line.notableId, 0, "credited to a named person");
            }
        }

        [Test]
        public void TheAnswerIsCreditedToTheNotableTheKeeperNamed()
        {
            var sim = Grown(5);
            var ada = sim.State.notables.First(n => n.alive && n.age >= 16f);
            float x = 0.5f, z = 0.5f; Dry(sim, ref x, ref z);
            var n = sim.PlaceShaping(SimCore.NudgeKind.Rain, x, z, ada.id).nudge;
            sim.Advance(3.05);
            Assert.IsTrue(n.credited);
            var line = sim.AnswerOf(n);
            Assert.AreEqual(ada.id, line.notableId);
            StringAssert.Contains(ada.FullName, line.text);
        }

        [Test]
        public void ShapingSpendsASharedCharge()
        {
            var sim = Grown(3);
            Assert.AreEqual(2, sim.ShapingChargesWhole);
            float x = -0.6f, z = 0.2f; Dry(sim, ref x, ref z);
            Assert.IsTrue(sim.PlaceShaping(SimCore.NudgeKind.Grove, x, z).Ok);
            Assert.AreEqual(1, sim.ShapingChargesWhole);
            x = 0.9f; z = 0.2f; Dry(sim, ref x, ref z);
            Assert.IsTrue(sim.PlaceShaping(SimCore.NudgeKind.Herd, x, z).Ok);
            Assert.AreEqual(0, sim.ShapingChargesWhole);
            var r = sim.PlaceShaping(SimCore.NudgeKind.Rain, 0f, 0f);
            Assert.AreEqual(SimCore.ShapeFail.NoCharge, r.fail);
            Assert.IsNull(r.nudge);
        }

        [Test]
        public void CleaningRefillsChargesByWhatWasReallyCleaned()
        {
            var sim = Grown(3);
            sim.State.shapingCharges = 0f;
            sim.CleanAll(); sim.CleanGrime(1f); sim.Prune(1f); // nothing to clean: nothing to farm
            float baseline = sim.ShapingCharges;
            sim.State.grime = 1f; sim.State.overgrowth = 0f; sim.State.trash.Clear();
            sim.CleanGrime(1f);
            Assert.AreEqual(baseline + sim.Config.shapingPerGrime, sim.ShapingCharges, 1e-3f);
            sim.CleanGrime(1f);
            Assert.AreEqual(baseline + sim.Config.shapingPerGrime, sim.ShapingCharges, 1e-3f, "wiping a clean glass gives nothing");
            sim.State.overgrowth = 1f;
            sim.Prune(1f);
            Assert.AreEqual(baseline + sim.Config.shapingPerGrime + sim.Config.shapingPerOvergrowth, sim.ShapingCharges, 1e-3f);
            sim.AddMess(0f, 0f, 2);
            int before = sim.State.trash.Count;
            float c = sim.ShapingCharges;
            Assert.IsTrue(sim.RemoveTrash(sim.State.trash[0].id));
            Assert.AreEqual(c + sim.Config.shapingPerTrash, sim.ShapingCharges, 1e-3f);
            Assert.AreEqual(before - 1, sim.State.trash.Count);
        }

        [Test]
        public void ChargesRefillOverTimeWhileThePeopleAreHappyAndStopAtTheCap()
        {
            var sim = Grown(3);
            sim.State.shapingCharges = 0f;
            sim.State.mood = 1f;
            float start = sim.ShapingCharges;
            sim.State.mood = 1f;
            sim.Advance(2);
            Assert.Greater(sim.ShapingCharges, start, "two happy years give charges back");
            sim.Advance(60);
            Assert.AreEqual(sim.ShapingMaxCharges, sim.ShapingCharges, 1e-3f, "capped");
        }

        [Test]
        public void NoRefillWhileTheyAreUnhappy()
        {
            var sim = Grown(3);
            sim.State.shapingCharges = 0f;
            for (int i = 0; i < 24; i++)
            {
                sim.State.mood = 0.1f; // keep them sad each month
                sim.Advance(1.0 / 12);
            }
            Assert.AreEqual(0f, sim.ShapingCharges, 1e-3f);
        }

        [Test]
        public void TheCapGrowsWithTheEra()
        {
            var sim = Grown(4);
            float camp = sim.ShapingMaxCharges;
            sim.ForceNextEra();
            Assert.AreEqual(camp + 1, sim.ShapingMaxCharges);
        }

        [Test]
        public void TakeBackWorksUntilThePeopleHaveUsedItAndNotAfter()
        {
            var sim = Grown(6);
            float x = 0.7f, z = -0.7f; Dry(sim, ref x, ref z);
            var flags = (byte[])sim.Settlement.Map.flags.Clone();
            float food = sim.State.food;
            var n = sim.PlaceShaping(SimCore.NudgeKind.Grove, x, z).nudge;
            Assert.AreEqual(1, sim.ShapingChargesWhole);
            CollectionAssert.AreNotEqual(flags, sim.Settlement.Map.flags, "the grove changed the land");
            Assert.IsTrue(sim.CanTakeBack(n.id));
            Assert.IsTrue(sim.TakeBack(n.id));
            Assert.AreEqual(2, sim.ShapingChargesWhole, "the charge came back");
            CollectionAssert.AreEqual(flags, sim.Settlement.Map.flags, "the land is as it was");
            Assert.IsFalse(sim.State.nudges.Contains(n));
            Assert.IsFalse(sim.TakeBack(n.id), "gone already");

            // Rain: its food comes with it, so a take-back and a new rain can't farm food.
            sim.State.food = 5f; food = 5f;
            var rain = sim.PlaceShaping(SimCore.NudgeKind.Rain, x, z).nudge;
            Assert.Greater(rain.foodGiven, 0f);
            float afterRain = sim.State.food;
            Assert.IsTrue(sim.TakeBack(rain.id));
            Assert.AreEqual(afterRain - rain.foodGiven, sim.State.food, 1e-3f);
            Assert.GreaterOrEqual(sim.State.food, food - 0.001f - 0f);

            // Used: once the people have answered, no take-back.
            var used = sim.PlaceShaping(SimCore.NudgeKind.Grove, x, z).nudge;
            sim.Advance(3.05);
            Assert.IsTrue(used.credited);
            Assert.IsFalse(sim.CanTakeBack(used.id));
            Assert.IsFalse(sim.TakeBack(used.id));
            Assert.IsTrue(sim.State.nudges.Contains(used));
        }

        [Test]
        public void TakingBackAChannelDriesItUp()
        {
            var sim = Village(8);
            float x = 0.9f, z = 0.9f; Dry(sim, ref x, ref z);
            var flags = (byte[])sim.Settlement.Map.flags.Clone();
            var n = sim.PlaceShaping(SimCore.NudgeKind.Channel, x, z).nudge;
            Assert.IsTrue(sim.TakeBack(n.id));
            CollectionAssert.AreEqual(flags, sim.Settlement.Map.flags);
        }

        [Test]
        public void NaturalCapsAndEraLocks()
        {
            var sim = Grown(7);
            sim.DebugRefillShaping();
            Assert.AreEqual(SimCore.ShapeFail.Locked, sim.CanShape(SimCore.NudgeKind.Channel), "the channel waits for the village");
            Assert.AreEqual(SimCore.ShapeFail.Unknown, sim.CanShape(SimCore.NudgeKind.Lamp));
            float x = 0.5f, z = 0.5f; Dry(sim, ref x, ref z);
            Assert.IsTrue(sim.PlaceShaping(SimCore.NudgeKind.Rain, x, z).Ok);
            Assert.AreEqual(SimCore.ShapeFail.AtCap, sim.PlaceShaping(SimCore.NudgeKind.Rain, x, z).fail, "one cloud at a time");
            sim.DebugRefillShaping();
            // Two herds on grass, a third is too many.
            float hx = -0.8f, hz = 0.5f; Dry(sim, ref hx, ref hz);
            Assert.AreEqual("deer", sim.HerdVariantAt(hx, hz));
            sim.State.shapingCharges = 10f;
            Assert.IsTrue(sim.PlaceShaping(SimCore.NudgeKind.Herd, hx, hz).Ok);
            Assert.IsTrue(sim.PlaceShaping(SimCore.NudgeKind.Herd, hx, hz).Ok);
            Assert.AreEqual(SimCore.ShapeFail.AtCap, sim.PlaceShaping(SimCore.NudgeKind.Herd, hx, hz).fail);
        }

        [Test]
        public void ChannelsCapAtThree()
        {
            var sim = Village(9);
            sim.State.shapingCharges = 10f;
            float x = 0.9f, z = 0.9f; Dry(sim, ref x, ref z);
            for (int i = 0; i < 3; i++) Assert.IsTrue(sim.PlaceShaping(SimCore.NudgeKind.Channel, x, z).Ok);
            Assert.AreEqual(SimCore.ShapeFail.AtCap, sim.PlaceShaping(SimCore.NudgeKind.Channel, x, z).fail);
        }

        [Test]
        public void AHerdFlickedIntoWaterIsOneShoalOfFish()
        {
            var sim = Grown(11);
            sim.State.shapingCharges = 10f;
            var (wx, wz) = Wet(sim);
            Assert.AreEqual("fish", sim.HerdVariantAt(wx, wz));
            var n = sim.PlaceShaping(SimCore.NudgeKind.Herd, wx, wz).nudge;
            Assert.AreEqual("fish", n.variant);
            Assert.AreEqual(SimCore.ShapeFail.AtCap, sim.PlaceShaping(SimCore.NudgeKind.Herd, wx, wz).fail, "one shoal per water body");
            float x = 0.6f, z = 0.6f; Dry(sim, ref x, ref z);
            Assert.AreEqual("deer", sim.PlaceShaping(SimCore.NudgeKind.Herd, x, z).nudge.variant, "deer are separate");
        }

        [Test]
        public void ToolsLevelUpByEra()
        {
            Assert.AreEqual("shower", SimCore.ShapingToolName(SimCore.NudgeKind.Rain, Era.Camp));
            Assert.AreEqual("storm", SimCore.ShapingToolName(SimCore.NudgeKind.Rain, Era.Town));
            Assert.AreEqual("grove", SimCore.ShapingToolName(SimCore.NudgeKind.Grove, Era.Camp));
            Assert.AreEqual("orchard", SimCore.ShapingToolName(SimCore.NudgeKind.Grove, Era.Town));
            Assert.AreEqual("mill race", SimCore.ShapingToolName(SimCore.NudgeKind.Channel, Era.Town));
            var sim = Grown(2);
            float x = 0.5f, z = 0.5f; Dry(sim, ref x, ref z);
            var n = sim.PlaceShaping(SimCore.NudgeKind.Grove, x, z).nudge;
            Assert.AreEqual((int)Era.Camp, n.eraAtPlace);
        }

        [Test]
        public void DebugHooksPlaceFreelyRefillAndSkipToTheAnswer()
        {
            var sim = Grown(13);
            sim.State.shapingCharges = 0f;
            float x = 0.4f, z = 0.4f; Dry(sim, ref x, ref z);
            var n = sim.DebugShape(SimCore.NudgeKind.Channel, x, z); // locked in the camp, free for the panel
            Assert.IsNotNull(n);
            Assert.AreEqual(0f, sim.ShapingCharges, 1e-4f, "free");
            Assert.IsNull(sim.DebugShape(SimCore.NudgeKind.Lamp, x, z), "not a slice tool");
            Assert.IsTrue(sim.DebugSkipToAnswer(n.id));
            Assert.IsTrue(n.credited);
            Assert.IsNotNull(sim.AnswerOf(n));
            Assert.IsFalse(sim.DebugSkipToAnswer(n.id));
            sim.DebugRefillShaping();
            Assert.AreEqual(sim.ShapingMaxCharges, sim.ShapingCharges);
        }

        [Test]
        public void PlacingShapingKeepsLiveAndCatchUpTheSame()
        {
            var a = Grown(21); var b = Grown(21);
            float x = 0.5f, z = 0.5f; Dry(a, ref x, ref z);
            a.PlaceShaping(SimCore.NudgeKind.Grove, x, z);
            b.PlaceShaping(SimCore.NudgeKind.Grove, x, z);
            a.Advance(5);
            for (int i = 0; i < 5 * 12; i++) b.Advance(1.0 / 12);
            Assert.AreEqual(string.Join("\n", a.State.chronicle.Select(e => e.text)), string.Join("\n", b.State.chronicle.Select(e => e.text)));
            Assert.AreEqual(a.ShapingCharges, b.ShapingCharges, 1e-3f);
        }
    }
}
