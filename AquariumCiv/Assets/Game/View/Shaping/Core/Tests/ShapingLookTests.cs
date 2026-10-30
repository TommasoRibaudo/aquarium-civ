using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using T = AquariumCiv.View.Shaping.ShapingTuning;

namespace AquariumCiv.View.Shaping.Tests
{
    /// <summary>#85: the four slice tools' looks over time (rain, grove, channel, herd), their take-back, and the answer's
    /// label, sampled from real shapings in real boxes. Pure: no Editor.</summary>
    public class ShapingLookTests
    {
        // ------------------------------------------------------------------ boxes

        internal static SimCore NewSim(int seed)
        {
            var cfg = new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        internal static SimCore SimIn(Era era, int seed)
        {
            var sim = NewSim(seed);
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assert.AreEqual(era, sim.State.era, $"seed {seed} reaches {era}");
            for (int m = 0; m < 12; m++) sim.Step();
            return sim;
        }

        /// <summary>Open dry ground near a point (what a finger lands on), away from the pond.</summary>
        internal static (float x, float z) OpenLand(SimCore sim, float x, float z)
        {
            var spec = sim.Settlement.Map.spec;
            foreach (var (px, pz) in new[] { (x, z), (-x, z), (x, -z), (-x, -z), (0.3f, 1.2f), (-1.4f, -0.4f) })
            {
                float ax = px, az = pz;
                if (!sim.Settlement.OpenSpotNear(ref ax, ref az, 0.35f)) continue;
                if (Terrain.Dist(ax, az, spec.pondX, spec.pondZ) < 1.1f || sim.HerdVariantAt(ax, az) != "deer") continue;
                return (ax, az);
            }
            Assert.Fail("no open land");
            return (0f, 0f);
        }

        internal static ShapingClock Clock(float secs, float months = 0f, Era era = Era.Camp, float answeredSecs = -1f, float answeredMonths = -1f, float leaving = -1f, long day = 0)
            => new ShapingClock { sinceSeen = secs, ageMonths = months, era = era, sinceAnswered = answeredSecs, monthsSinceAnswer = answeredMonths, sinceLeaving = leaving, day = day };

        static ShapingFootprint Shaped(SimCore sim, string kind, float x, float z)
        {
            var n = sim.DebugShape(kind, x, z);
            Assert.IsNotNull(n);
            return ShapingFootprint.Of(n, sim.Settlement.Map, sim.State.seed);
        }

        // ------------------------------------------------------------------ rain

        [Test]
        public void RainFormsACloudFallsThenTheSoilDarkensAndSprouts()
        {
            var sim = NewSim(3);
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            var f = Shaped(sim, SimCore.NudgeKind.Rain, x, z);
            Assert.IsNotEmpty(f.cells, "it wets some land");
            var map = sim.Settlement.Map;
            Assert.IsTrue(f.cells.All(c => !map.Water(c)), "only land gets wet");

            var r0 = ShapingLooks.Rain(f, Clock(0f));
            Assert.AreEqual(0f, r0.cloud, 1e-4f, "no cloud at the tap's instant");
            Assert.AreEqual(0f, r0.rain, 1e-4f);
            Assert.AreEqual(1f, ShapingLooks.Rain(f, Clock(T.CloudFormSeconds)).cloud, 1e-4f, "the cloud forms first");
            float mid = T.RainDelaySeconds + T.RainSeconds(Era.Camp) * 0.5f;
            var rm = ShapingLooks.Rain(f, Clock(mid));
            Assert.AreEqual(1f, rm.rain, 1e-4f, "then it rains");
            Assert.That(rm.wet, Is.GreaterThan(0.2f).And.LessThan(1f), "the soil darkens while it rains");
            var done = ShapingLooks.Rain(f, Clock(T.RainDelaySeconds + T.RainSeconds(Era.Camp) + T.CloudFadeSeconds + 0.1f));
            Assert.AreEqual(0f, done.cloud, 1e-4f, "the cloud is gone after the shower");
            Assert.AreEqual(0f, done.rain, 1e-4f);
            Assert.AreEqual(1f, done.wet, 1e-4f, "the ground stays dark");
            Assert.AreEqual(0f, done.sprouts, 1e-4f, "no sprouts at once");
            Assert.IsFalse(done.storm);

            // Over the next months: sprouts come up, the soil dries to damp; answered, it dries out.
            float prev = 0f;
            for (float m = 0f; m <= 4f; m += 0.25f)
            {
                var r = ShapingLooks.Rain(f, Clock(1e4f, m));
                Assert.GreaterOrEqual(r.sprouts, prev - 1e-5f, "sprouts only grow");
                prev = r.sprouts;
            }
            Assert.AreEqual(1f, ShapingLooks.Rain(f, Clock(1e4f, T.SproutGrownMonths)).sprouts, 1e-4f, "grown by the third month");
            Assert.AreEqual(T.DampAfter, ShapingLooks.Rain(f, Clock(1e4f, 6f)).wet, 1e-4f, "damp until the people answer");
            Assert.AreEqual(0f, ShapingLooks.Rain(f, Clock(1e4f, 8f, Era.Camp, 1e4f, T.AnsweredDryMonths)).wet, 1e-4f, "dry once answered");

            // Taken back: everything goes within the leave time.
            var gone = ShapingLooks.Rain(f, Clock(mid, 0f, Era.Camp, -1f, -1f, T.LeaveSeconds));
            Assert.AreEqual(0f, gone.cloud + gone.rain + gone.wet + gone.sprouts, 1e-4f, "a take-back clears it");
            Assert.Less(ShapingLooks.Rain(f, Clock(mid, 0f, Era.Camp, -1f, -1f, T.LeaveSeconds * 0.5f)).rain, rm.rain, "and fades, not pops");
        }

        [Test]
        public void ATownRainIsAStormWithLightning_ACampShowerHasNone()
        {
            var town = SimIn(Era.Town, 1234);
            var (x, z) = OpenLand(town, 1.2f, 0.6f);
            var storm = Shaped(town, SimCore.NudgeKind.Rain, x, z);
            Assert.AreEqual(Era.Town, storm.level);
            var camp = Shaped(NewSim(2), SimCore.NudgeKind.Rain, 1.2f, 0.6f);
            float stormFlash = 0f, campFlash = 0f;
            for (float s = 0f; s < 12f; s += 0.02f)
            {
                stormFlash = Math.Max(stormFlash, ShapingLooks.Rain(storm, Clock(s)).flash);
                campFlash = Math.Max(campFlash, ShapingLooks.Rain(camp, Clock(s)).flash);
            }
            Assert.IsTrue(ShapingLooks.Rain(storm, Clock(2f)).storm);
            Assert.Greater(stormFlash, 0.5f, "lightning in the storm");
            Assert.AreEqual(0f, campFlash, "a shower has none");
            Assert.Greater(T.RainSeconds(Era.Town), T.RainSeconds(Era.Camp), "a storm rains longer than a shower");
        }

        // ------------------------------------------------------------------ grove

        [Test]
        public void AGroveGrowsFromSaplingsIntoTreesOverYears()
        {
            var sim = NewSim(5);
            var (x, z) = OpenLand(sim, -1.2f, 0.7f);
            var f = Shaped(sim, SimCore.NudgeKind.Grove, x, z);
            Assert.IsNotEmpty(f.cells, "it plants woods");
            var map = sim.Settlement.Map;
            Assert.IsTrue(f.cells.All(c => map.Forest(c)), "on the cells the sim made woods");

            var g0 = ShapingLooks.Grove(f, Clock(0f), map);
            Assert.IsNotEmpty(g0.trees);
            Assert.IsTrue(g0.trees.All(t => t.growth == 0f), "nothing before the tap lands");
            Assert.IsTrue(g0.trees.All(t => Terrain.Dist(t.x, t.z, x, z) <= T.GroveRadius + 0.1f), "the trees stand round the spot");
            var sap = ShapingLooks.Grove(f, Clock(30f), map);
            Assert.IsTrue(sap.trees.All(t => Math.Abs(t.growth - T.SaplingScale) < 1e-3f), "saplings pop up");
            var prev = sap.trees.Select(t => t.growth).ToArray();
            for (float m = 1f; m <= 48f; m += 1f)
            {
                var g = ShapingLooks.Grove(f, Clock(1e4f, m), map);
                for (int i = 0; i < prev.Length; i++)
                {
                    Assert.GreaterOrEqual(g.trees[i].growth, prev[i] - 1e-5f, "trees only grow");
                    prev[i] = g.trees[i].growth;
                }
            }
            var grown = ShapingLooks.Grove(f, Clock(1e4f, T.TreeDelayMonths + T.TreeGrowMonths), map);
            Assert.IsTrue(grown.trees.All(t => Math.Abs(t.growth - 1f) < 1e-4f), "full trees after three years and a bit");
            var mid = ShapingLooks.Grove(f, Clock(1e4f, 12f), map);
            Assert.IsTrue(mid.trees.Any(t => t.growth > T.SaplingScale + 0.01f && t.growth < 0.99f), "a year in, still growing");
            Assert.IsTrue(ShapingLooks.Grove(f, Clock(1e4f, 20f, Era.Camp, -1f, -1f, T.LeaveSeconds), map).trees.All(t => t.growth == 0f), "a take-back sinks them");
        }

        [Test]
        public void TheGroveLevelsUpWithTheEra_WoodsBlossomOrchard()
        {
            var sim = NewSim(5);
            var (x, z) = OpenLand(sim, -1.2f, 0.7f);
            var f = Shaped(sim, SimCore.NudgeKind.Grove, x, z);
            var map = sim.Settlement.Map;
            const long spring = 3, harvest = 8;
            var woods = ShapingLooks.Grove(f, Clock(1e4f, 48f, Era.Camp, day: spring), map);
            Assert.AreEqual(GroveStyle.Woods, woods.style);
            Assert.IsTrue(woods.trees.All(t => t.blossom == 0f && t.fruit == 0f), "woods don't blossom");
            var village = ShapingLooks.Grove(f, Clock(1e4f, 48f, Era.Village, day: spring), map);
            Assert.AreEqual(GroveStyle.Blossom, village.style);
            Assert.IsTrue(village.trees.All(t => t.blossom > 0.99f), "the village's grove blossoms in spring");
            Assert.IsTrue(ShapingLooks.Grove(f, Clock(1e4f, 48f, Era.Village, day: harvest), map).trees.All(t => t.blossom == 0f), "only in spring");
            var orchard = ShapingLooks.Grove(f, Clock(1e4f, 48f, Era.Town, day: harvest), map);
            Assert.AreEqual(GroveStyle.Orchard, orchard.style);
            Assert.IsTrue(orchard.trees.All(t => t.fruit > 0.99f), "the orchard fruits in late summer");
            // In rows: every orchard tree sits on a cell centre, on a checkerboard.
            foreach (var t in orchard.trees)
            {
                int c = SiteMap.CellAt(t.x, t.z);
                Assert.AreEqual(SiteMap.CXc(c), t.x, 1e-4f);
                Assert.AreEqual(0, (SiteMap.I(c) + SiteMap.J(c)) & 1, "rows");
            }
        }

        [Test]
        public void TreesCutForTimberLeaveTheGrove()
        {
            var sim = NewSim(5);
            var (x, z) = OpenLand(sim, -1.2f, 0.7f);
            var f = Shaped(sim, SimCore.NudgeKind.Grove, x, z);
            var map = sim.Settlement.Map;
            int all = ShapingLooks.Grove(f, Clock(1e4f, 48f), map).trees.Count;
            int c0 = f.cells[0];
            map.Set(c0, SiteMap.FForest, false);
            int after = ShapingLooks.Grove(f, Clock(1e4f, 48f), map).trees.Count;
            Assert.LessOrEqual(after, all);
            Assert.IsFalse(ShapingLooks.Grove(f, Clock(1e4f, 48f), map).trees.Any(t => SiteMap.CellAt(t.x, t.z) == c0 && Terrain.Dist(t.x, t.z, SiteMap.CXc(c0), SiteMap.CZc(c0)) < 0.04f), "no tree on a cut cell");
        }

        // ------------------------------------------------------------------ channel

        static bool Adjacent(int a, int b)
        {
            int di = Math.Abs(SiteMap.I(a) - SiteMap.I(b)), dj = Math.Abs(SiteMap.J(a) - SiteMap.J(b));
            return di + dj == 1;
        }

        [TestCase(1234)]
        [TestCase(7)]
        public void AChannelIsCarvedFromThePondToTheSpotAndTheWaterFollows(int seed)
        {
            var sim = SimIn(Era.Village, seed);
            var map = sim.Settlement.Map;
            var (x, z) = OpenLand(sim, 1.5f, -0.8f);
            var f = Shaped(sim, SimCore.NudgeKind.Channel, x, z);
            Assert.That(f.cells.Count, Is.GreaterThan(2), "a channel of a few cells");
            Assert.IsTrue(f.cells.All(c => map.Canal(c)), "on the sim's canal cells");
            int first = f.cells[0];
            bool byPond = false;
            foreach (int c in new[] { first - 1, first + 1, first - SiteMap.NX, first + SiteMap.NX })
                if (c >= 0 && c < SiteMap.N && map.pond[c]) byPond = true;
            Assert.IsTrue(byPond, "it starts at the pond");
            for (int i = 1; i < f.cells.Count; i++)
                Assert.IsTrue(f.cells.Take(i).Any(c => Adjacent(c, f.cells[i])), $"cell {i} joins the cut so far");
            Assert.Less(Terrain.Dist(SiteMap.CXc(f.cells[^1]), SiteMap.CZc(f.cells[^1]), x, z), 0.75f, "and ends by the spot");

            float carve = ShapingLooks.CarveSeconds(f.cells.Count);
            for (float s = 0f; s <= carve + 1f; s += 0.05f)
            {
                var ch = ShapingLooks.Channel(f, Clock(s, 0f, Era.Village));
                for (int i = 0; i < ch.cells.Count; i++)
                {
                    Assert.LessOrEqual(ch.cells[i].water, ch.cells[i].carved + 1e-5f, "water only where it's dug");
                    if (i > 0) Assert.LessOrEqual(ch.cells[i].carved, ch.cells[i - 1].carved + 1e-5f, "dug from the pond end");
                }
            }
            var full = ShapingLooks.Channel(f, Clock(carve + T.WaterLagSeconds + 0.01f, 0f, Era.Village));
            Assert.IsTrue(full.cells.All(c => c.carved == 1f && c.water == 1f), "carved and full");
            Assert.AreEqual(1f, full.flowHead, 1e-4f, "the water reached the spot");
            Assert.AreEqual(ChannelStyle.Ditch, full.style);
            Assert.AreEqual(-1, full.fallAt, "no fall before the town");
            Assert.AreEqual(ChannelStyle.Irrigation, ShapingLooks.Channel(f, Clock(1e4f, 20f, Era.Village, 1e4f, 2f)).style, "the village lines it with fields once answered");
            var race = ShapingLooks.Channel(f, Clock(1e4f, 20f, Era.Town));
            Assert.AreEqual(ChannelStyle.MillRace, race.style, "a mill race in the town");
            Assert.That(race.fallAt, Is.InRange(1, f.cells.Count - 1), "with its little fall");

            // Taken back: it drains first, then the turf closes.
            var half = ShapingLooks.Channel(f, Clock(1e4f, 1f, Era.Village, -1f, -1f, T.LeaveSeconds * 0.5f));
            Assert.IsTrue(half.cells.All(c => c.water == 0f), "drained");
            Assert.IsTrue(half.cells.Any(c => c.carved > 0f), "still dug");
            var closed = ShapingLooks.Channel(f, Clock(1e4f, 1f, Era.Village, -1f, -1f, T.LeaveSeconds));
            Assert.IsTrue(closed.cells.All(c => c.carved == 0f && c.water == 0f), "closed over");
        }

        // ------------------------------------------------------------------ herd

        static void Walk(HerdLook prev, HerdLook now, float dt, ref float fastest)
        {
            for (int i = 0; i < now.animals.Count; i++)
            {
                if (prev.animals[i].pose == AnimalPose.Hidden || now.animals[i].pose == AnimalPose.Hidden) continue;
                float v = Terrain.Dist(prev.animals[i].x, prev.animals[i].z, now.animals[i].x, now.animals[i].z) / dt;
                fastest = Math.Max(fastest, v);
            }
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(1234)]
        public void DeerRunInFromTheEdgeAndGrazeInsideTheBoxOnLand(int seed)
        {
            var sim = NewSim(seed);
            var map = sim.Settlement.Map;
            var (x, z) = OpenLand(sim, 1.3f, 0.7f);
            var f = Shaped(sim, SimCore.NudgeKind.Herd, x, z);
            Assert.AreEqual(HerdSpecies.Deer, f.Species, "on grass it's deer");
            Assert.AreEqual(T.HerdCount(HerdSpecies.Deer), ShapingLooks.Herd(f, Clock(0f)).animals.Count);
            bool onEdge = Math.Abs(Math.Abs(f.entryX) - ShapingFootprint.MaxX) < 1e-4f || Math.Abs(Math.Abs(f.entryZ) - ShapingFootprint.MaxZ) < 1e-4f;
            Assert.IsTrue(onEdge, "they come in from the box edge");
            var first = ShapingLooks.Herd(f, Clock(0.01f));
            Assert.Less(Terrain.Dist(first.animals[0].x, first.animals[0].z, f.entryX, f.entryZ), 0.15f, "the first starts at the edge");
            Assert.AreEqual(AnimalPose.Hidden, first.animals[first.animals.Count - 1].pose, "the last isn't in yet");

            float fastest = 0f, arrive = T.RunInSeconds + T.RunInStagger * T.HerdCount(HerdSpecies.Deer);
            int grazing = 0, walking = 0;
            var prev = first;
            const float dt = 0.05f;
            for (float s = 0.01f + dt; s < 120f; s += dt)
            {
                var h = ShapingLooks.Herd(f, Clock(s));
                Walk(prev, h, dt, ref fastest);
                foreach (var a in h.animals)
                {
                    Assert.LessOrEqual(Math.Abs(a.x), ShapingFootprint.MaxX + 1e-4f, "inside the box");
                    Assert.LessOrEqual(Math.Abs(a.z), ShapingFootprint.MaxZ + 1e-4f, "inside the box");
                    if (s < arrive) continue;
                    int c = SiteMap.CellAt(a.x, a.z);
                    Assert.IsFalse(map.Water(c), $"seed {seed} t {s:0.00}: a deer in the water at ({a.x:0.00}, {a.z:0.00})");
                    Assert.Less(Terrain.Dist(a.x, a.z, x, z), T.GrazeRadius + T.HerdSpread + 0.05f, "they stay on their grazing ground");
                    if (a.pose == AnimalPose.Graze) grazing++;
                    if (a.pose == AnimalPose.Walk) walking++;
                }
                prev = h;
            }
            TestContext.WriteLine($"seed {seed}: fastest {fastest:0.00} u/s, {grazing} grazing, {walking} walking samples");
            Assert.LessOrEqual(fastest, T.AnimalMaxSpeed, "never a teleport");
            Assert.Greater(grazing, walking, "mostly grazing");
            Assert.Greater(walking, 0, "and moving on now and then");

            // Taken back: they run out to the edge they came from.
            var gone = ShapingLooks.Herd(f, Clock(60f, 1f, Era.Camp, -1f, -1f, T.HerdLeaveSeconds));
            Assert.IsTrue(gone.animals.All(a => a.pose == AnimalPose.Hidden && a.alpha == 0f), "gone");
            Assert.IsTrue(gone.animals.All(a => Terrain.Dist(a.x, a.z, f.entryX, f.entryZ) < 1e-3f), "out at the edge");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void AShoalLandsWithASplashAndSwimsInThePond(int seed)
        {
            var sim = NewSim(seed);
            var map = sim.Settlement.Map;
            float px = map.spec.pondX, pz = map.spec.pondZ;
            Assert.AreEqual("fish", sim.HerdVariantAt(px, pz));
            var f = Shaped(sim, SimCore.NudgeKind.Herd, px, pz);
            Assert.AreEqual(HerdSpecies.Fish, f.Species);
            var pond = Enumerable.Range(0, SiteMap.N).Where(c => map.pond[c]).ToList();
            var h0 = ShapingLooks.Herd(f, Clock(0f));
            Assert.AreEqual(1f, h0.splash, 1e-4f, "a splash where it lands");
            Assert.AreEqual(T.HerdCount(HerdSpecies.Fish), h0.animals.Count);
            Assert.AreEqual(0f, ShapingLooks.Herd(f, Clock(T.SplashSeconds)).splash, 1e-4f);
            float fastest = 0f;
            var prev = h0;
            for (float s = 0.05f; s < 60f; s += 0.05f)
            {
                var h = ShapingLooks.Herd(f, Clock(s));
                Walk(prev, h, 0.05f, ref fastest);
                foreach (var a in h.animals)
                {
                    Assert.AreEqual(AnimalPose.Swim, a.pose);
                    float near = pond.Min(c => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), a.x, a.z));
                    Assert.Less(near, 0.13f, $"t {s:0.00}: a fish out of the water");
                }
                prev = h;
            }
            Assert.LessOrEqual(fastest, T.AnimalMaxSpeed);
            var dive = ShapingLooks.Herd(f, Clock(30f, 1f, Era.Camp, -1f, -1f, T.HerdLeaveSeconds));
            Assert.IsTrue(dive.animals.All(a => a.alpha == 0f), "a take-back: they dive and are gone");
        }

        [Test]
        public void TheHerdLevelsUpWithTheEraItWasSentIn_AndMovesToItsPasture()
        {
            Assert.AreEqual(HerdSpecies.Deer, ShapingFootprint.SpeciesOf("deer", Era.Camp));
            Assert.AreEqual(HerdSpecies.Goat, ShapingFootprint.SpeciesOf("deer", Era.Village));
            Assert.AreEqual(HerdSpecies.Sheep, ShapingFootprint.SpeciesOf("deer", Era.Town));
            Assert.AreEqual(HerdSpecies.Fish, ShapingFootprint.SpeciesOf("fish", Era.Town));
            var sim = SimIn(Era.Village, 1234);
            var (x, z) = OpenLand(sim, 1.3f, 0.7f);
            var f = Shaped(sim, SimCore.NudgeKind.Herd, x, z);
            Assert.AreEqual(HerdSpecies.Goat, f.Species);
            Assert.AreEqual(T.HerdCount(HerdSpecies.Goat), ShapingLooks.Herd(f, Clock(30f)).animals.Count);
            // Answered: they drift to the pasture over a few seconds.
            float lx = x - 0.8f, lz = z;
            var start = ShapingLooks.Herd(f, Clock(40f, 14f, Era.Village, 0f, 0f), lx, lz);
            var there = ShapingLooks.Herd(f, Clock(40f + T.HerdMoveToLotSeconds, 14f, Era.Village, T.HerdMoveToLotSeconds, 0.1f), lx, lz);
            float d0 = start.animals.Average(a => Terrain.Dist(a.x, a.z, lx, lz)), d1 = there.animals.Average(a => Terrain.Dist(a.x, a.z, lx, lz));
            Assert.Less(d1, d0, "nearer the pasture");
            Assert.Less(d1, T.GrazeRadius + T.HerdSpread + 0.1f, "grazing round it");
        }

        [Test]
        public void AHerdSentInTheCampBecomesGoatsAndThenSheepAsTheEraMoves()
        {
            var sim = NewSim(1234);
            var (x, z) = OpenLand(sim, 1.3f, 0.7f);
            var f = Shaped(sim, SimCore.NudgeKind.Herd, x, z);
            Assert.AreEqual(HerdSpecies.Deer, ShapingLooks.Herd(f, Clock(30f, 0f, Era.Camp)).species);
            var goats = ShapingLooks.Herd(f, Clock(30f, 0f, Era.Village));
            Assert.AreEqual(HerdSpecies.Goat, goats.species);
            Assert.AreEqual(T.HerdCount(HerdSpecies.Goat), goats.animals.Count);
            var sheep = ShapingLooks.Herd(f, Clock(30f, 0f, Era.Town));
            Assert.AreEqual(HerdSpecies.Sheep, sheep.species);
            Assert.AreEqual(T.HerdCount(HerdSpecies.Sheep), sheep.animals.Count);
            Assert.AreEqual(HerdSpecies.Deer, f.Species, "what was sent stays on the footprint");
            // A shoal stays a shoal; a later herd is never a step back.
            var fish = new ShapingFootprint { variant = "fish", level = Era.Camp };
            Assert.AreEqual(HerdSpecies.Fish, fish.SpeciesAt(Era.Town));
            var late = new ShapingFootprint { variant = "deer", level = Era.Town };
            Assert.AreEqual(HerdSpecies.Sheep, late.SpeciesAt(Era.Camp));
        }

        // ------------------------------------------------------------------ the answer's label, sameness

        [Test]
        public void TheLabelNamesTheGiftByItsLevel()
        {
            Assert.AreEqual("The Keeper's rain", ShapingLooks.LabelOf(SimCore.NudgeKind.Rain, "", Era.Camp));
            Assert.AreEqual("The Keeper's rain", ShapingLooks.LabelOf(SimCore.NudgeKind.Rain, "", Era.Village));
            Assert.AreEqual("The Keeper's storm", ShapingLooks.LabelOf(SimCore.NudgeKind.Rain, "", Era.Town));
            Assert.AreEqual("The Keeper's grove", ShapingLooks.LabelOf(SimCore.NudgeKind.Grove, "", Era.Camp));
            Assert.AreEqual("The Keeper's shoal", ShapingLooks.LabelOf(SimCore.NudgeKind.Herd, "fish", Era.Camp));
            Assert.AreEqual("The Keeper's herd", ShapingLooks.LabelOf(SimCore.NudgeKind.Herd, "deer", Era.Camp));
            Assert.AreEqual("The Keeper's mill race", ShapingLooks.LabelOf(SimCore.NudgeKind.Channel, "", Era.Town));
        }

        [Test]
        public void TheAnswerShowHighlightsLabelsAndReadsTheLineThenEnds()
        {
            const string line = "After the Keeper's rain, Ada Lund planted a new field.";
            float total = ShapingLooks.AnswerSeconds(line);
            var a0 = ShapingLooks.Answer(1, 5, 9, 3, 0f, 0f, "The Keeper's rain", line, 0f);
            Assert.IsTrue(a0.active);
            Assert.AreEqual(0f, a0.bubbleAlpha, "the line waits a beat");
            var a1 = ShapingLooks.Answer(1, 5, 9, 3, 0f, 0f, "The Keeper's rain", line, 2f);
            Assert.AreEqual(1f, a1.highlight, 1e-4f, "the new field lights up");
            Assert.AreEqual(1f, a1.labelAlpha, 1e-4f);
            Assert.AreEqual(1f, a1.bubbleAlpha, 1e-4f, "the line is up");
            Assert.AreEqual(line, a1.bubble);
            Assert.GreaterOrEqual(ShapingLooks.BubbleSeconds(line), line.Length / T.BubbleCharsPerSecond, "long enough to read");
            Assert.AreEqual(0f, ShapingLooks.Answer(1, -1, 9, 3, 0f, 0f, "x", line, 2f).highlight, "a harvest has no building to light");
            var end = ShapingLooks.Answer(1, 5, 9, 3, 0f, 0f, "The Keeper's rain", line, total);
            Assert.IsFalse(end.active);
            Assert.AreEqual(1f, end.progress);
            Assert.LessOrEqual(total, 15f, "never long");
        }

        [Test]
        public void LooksAreAPureFunctionOfTheFootprintAndTheClocks()
        {
            var a = NewSim(9);
            var b = NewSim(9);
            foreach (var kind in new[] { SimCore.NudgeKind.Rain, SimCore.NudgeKind.Grove, SimCore.NudgeKind.Herd })
            {
                var (x, z) = OpenLand(a, 1.1f, -0.5f);
                var fa = Shaped(a, kind, x, z);
                var fb = Shaped(b, kind, x, z);
                CollectionAssert.AreEqual(fa.cells, fb.cells);
                for (float s = 0f; s < 20f; s += 0.37f)
                {
                    var c = Clock(s, s / 4f, Era.Camp, day: (long)s);
                    Assert.AreEqual(Dump(fa, c, a.Settlement.Map), Dump(fb, c, b.Settlement.Map), $"{kind} at {s}");
                    Assert.AreEqual(Dump(fa, c, a.Settlement.Map), Dump(fa, c, a.Settlement.Map), "the same frame twice");
                }
            }
        }

        static string Dump(ShapingFootprint f, ShapingClock c, SiteMap map)
        {
            var r = ShapingLooks.Rain(f, c);
            var g = ShapingLooks.Grove(f, c, map);
            var h = ShapingLooks.Herd(f, c);
            return $"{r.cloud:R} {r.rain:R} {r.wet:R} {r.sprouts:R} | {string.Join(",", g.trees.Select(t => $"{t.x:R}/{t.z:R}/{t.growth:R}"))} | "
                + string.Join(",", h.animals.Select(an => $"{an.x:R}/{an.z:R}/{an.pose}"));
        }
    }
}
