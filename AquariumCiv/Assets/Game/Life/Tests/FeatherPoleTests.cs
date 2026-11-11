using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Life.Tests
{
    /// <summary>fb4 #566: ONE feather pole however many answers asked for a feather (old saves too), standing a bit further out than the tents and
    /// clear of the camp's pieces and the pond's future water, and the people visit it and pray: once or twice a day, one or two grown-ups, the
    /// pose held a few real seconds, then back to the day.</summary>
    public class FeatherPoleTests
    {
        static SimCore Eco(int seed = 5)
        {
            var cfg = new SimConfig { ecology = true, settle = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static ConsequenceProp AddFeather(SimCore sim, float x, float z)
        {
            var list = sim.State.consequenceProps;
            var p = new ConsequenceProp { id = list.Count + 1, dilemmaId = 700 + list.Count, kind = ConsequenceKind.Feather, anchor = PropAnchor.Home, placed = true, x = x, z = z, dueMonth = 0, placedMonth = 0 };
            list.Add(p);
            return p;
        }

        // ---------------------------------------------------------------- one pole
        [Test]
        public void OnePoleHoweverManyFeathersAreLeft()
        {
            var sim = Eco();
            Assert.IsNull(sim.FeatherPoleProp, "no feather, no pole");
            var a = AddFeather(sim, 0.3f, 0.2f); AddFeather(sim, -0.4f, 0.1f); AddFeather(sim, 0.1f, -0.5f);
            var pole = sim.FeatherPoleProp;
            Assert.AreSame(a, pole, "the first feather is the pole");
            Assert.IsTrue(sim.FeatherPoleSpot(out float x, out float z));
            foreach (var p in sim.State.consequenceProps.Where(q => q.kind == ConsequenceKind.Feather))
                Assert.AreEqual((x, z), (p.x, p.z), "every feather answer is that one pole (an old save's stakes by the tents move onto it)");
            var again = sim.FeatherPoleProp; sim.FeatherPoleSpot(out float x2, out float z2);
            Assert.AreEqual((x, z), (x2, z2), "stable");
            // a feather left later joins it
            var late = AddFeather(sim, 0.9f, 0.9f);
            Assert.AreSame(a, sim.FeatherPoleProp);
            Assert.AreEqual((x, z), (late.x, late.z));
            // saved
            var back = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            back.FeatherPoleSpot(out float bx, out float bz);
            Assert.AreEqual((x, z), (bx, bz));
        }

        [Test]
        public void TheSpotIsFarEnoughOutAndClearOfTheCampsPiecesAndTheFutureWater()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var sim = Eco(seed);
                AddFeather(sim, 0f, 0f);
                Assert.IsTrue(sim.FeatherPoleSpot(out float x, out float z));
                sim.Settlement.Fire(out float fx, out float fz, out _);
                float d = Terrain.Dist(x, z, fx, fz);
                Assert.GreaterOrEqual(d, FeatherPoleSpots.MinFire - 1e-3f, $"seed {seed}: out past the rack, stash, pot and seats");
                Assert.LessOrEqual(d, 2.9f, $"seed {seed}: still in view of the camp");
                foreach (var b in sim.State.buildings.Where(q => q.lotId != 0))
                    Assert.GreaterOrEqual(Terrain.Dist(x, z, b.x, b.z), FeatherPoleSpots.BuildingGap * 0.99f, $"seed {seed}: clear of the tent at {b.x:0.00},{b.z:0.00}");
                foreach (var q in sim.State.consequenceProps.Where(q => q.placed && q.kind != ConsequenceKind.Feather))
                    Assert.GreaterOrEqual(Terrain.Dist(x, z, q.x, q.z), FeatherPoleSpots.PropGap * 0.69f, $"seed {seed}: clear of the {q.kind} prop");
                var map = sim.Settlement.Map;
                int c = SiteMap.CellAt(x, z);
                Assert.IsTrue(ApartSpots.CellFree(map, c), $"seed {seed}: on open ground (no lot, path, field, yard, green, rock, water)");
                Assert.IsFalse(PondShore.InFutureWater(sim, x, z, FeatherPoleSpots.WaterMargin), $"seed {seed}: dry for good");
                Assert.IsFalse(map.Forest(c));
                Assert.LessOrEqual(System.Math.Abs(x), Terrain.HX - ApartSpots.GlassMargin + 1e-3f);
                Assert.LessOrEqual(System.Math.Abs(z), Terrain.HZ - ApartSpots.GlassMargin + 1e-3f);
            }
        }

        [Test]
        public void TheSpotIsTheSameForTheSameBoxAndOutsideTheStakesOfTheSettlement()
        {
            var a = Eco(3); var b = Eco(3);
            AddFeather(a, 0f, 0f); AddFeather(b, 1f, 1f);
            a.FeatherPoleSpot(out float ax, out float az); b.FeatherPoleSpot(out float bx, out float bz);
            Assert.AreEqual((ax, az), (bx, bz), "no dice, no dependence on where the old stake was");
            var (px, pz) = FeatherPoleSpots.Pick(null, 0f, 0f, 3, null, null);
            Assert.GreaterOrEqual((float)System.Math.Sqrt(px * px + pz * pz), 1f, "with no map a point out from the fire");
        }

        [Test]
        public void ARaisedTentOnTheSpotMovesThePole()
        {
            var sim = Eco(2);
            AddFeather(sim, 0f, 0f);
            sim.FeatherPoleSpot(out float x, out float z);
            var b = sim.State.buildings.First(q => q.lotId != 0);
            float ox = b.x, oz = b.z;
            b.x = x + 0.05f; b.z = z;
            sim.FeatherPoleSpot(out float x2, out float z2);
            Assert.GreaterOrEqual(Terrain.Dist(x2, z2, b.x, b.z), FeatherPoleSpots.BuildingGap * 0.99f);
            b.x = ox; b.z = oz;
        }

        // ---------------------------------------------------------------- the visit
        static SimCore PoleCamp(int seed)
        {
            var sim = Eco(seed);
            Assert.IsTrue(sim.DebugEnsureFeatherPole());
            return sim;
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(7)]
        public void SomeoneWalksToThePolePraysAFewSecondsAndGoesBack_EveryDay(int seed)
        {
            var sim = PoleCamp(seed);
            var planner = LifePlanner.Wired(sim);
            planner.ForcedWeather = Weather.Clear;
            var pole = planner.PolePlace;
            Assert.AreEqual(PlaceKind.Prop, pole.kind);
            var visits = new List<LifePlanner.PrayerVisit>();
            int total = 0;
            for (long day = 0; day < 8; day++)
            {
                planner.PrayerVisitsOn(day, visits);
                Assert.GreaterOrEqual(visits.Count, 1, $"day {day}: someone prays");
                var starts = visits.Select(v => v.start).Distinct().Count();
                Assert.LessOrEqual(starts, 2, $"day {day}: once or twice a day");
                foreach (var v in visits)
                {
                    total++;
                    float secs = (v.end - v.arrive) * LifeClock.RealSecondsPerViewHour(v.arrive);
                    Assert.AreEqual(LifePlanner.PraySeconds, secs, 0.6f, "a few real seconds");
                    Assert.Greater(v.until, v.end, "and a walk back");
                    Assert.Less(v.until, 24f);
                }
                Assert.LessOrEqual(visits.Count(v => !v.kid), 4, "one or two grown-ups at each visit");
                // the plan the view reads: walk there, pray (facing the pole's place), walk back, then the day again
                var v0 = visits.First(v => !v.kid);
                var who = planner.People.First(p => p.Key == v0.key);
                var before = planner.Plan(who, new ViewTime(day, v0.start - 0.05f));
                var walking = planner.Plan(who, new ViewTime(day, (v0.start + v0.arrive) * 0.5f));
                var praying = planner.Plan(who, new ViewTime(day, (v0.arrive + v0.end) * 0.5f));
                var back = planner.Plan(who, new ViewTime(day, (v0.end + v0.until) * 0.5f));
                var after = planner.Plan(who, new ViewTime(day, v0.until + 0.02f));
                Assert.AreNotEqual(ActivityKind.Pray, before.kind);
                Assert.AreEqual(ActivityKind.Walk, walking.kind); Assert.AreEqual(pole, walking.place); Assert.IsTrue(walking.moving);
                Assert.AreEqual(ActivityKind.Pray, praying.kind); Assert.AreEqual(pole, praying.place); Assert.IsFalse(praying.moving);
                Assert.AreEqual(ActivityKind.Walk, back.kind); Assert.AreEqual(pole, back.from); Assert.IsTrue(back.moving);
                Assert.AreNotEqual(ActivityKind.Pray, after.kind, "it ends");
            }
            Assert.GreaterOrEqual(total, 8);
        }

        [Test]
        public void NoPoleNoPrayer_AndAStormKeepsThemIn()
        {
            var sim = Eco(5);
            var planner = LifePlanner.Wired(sim);
            var visits = new List<LifePlanner.PrayerVisit>();
            Assert.AreEqual(0, planner.PrayerVisitsOn(3, visits), "no feather, no visit");
            sim.DebugEnsureFeatherPole();
            planner.Invalidate();
            planner.ForcedWeather = Weather.Storm;
            Assert.AreEqual(0, planner.PrayerVisitsOn(3, visits), "not in a storm");
            planner.ForcedWeather = Weather.Clear;
            Assert.Greater(planner.PrayerVisitsOn(3, visits), 0);
        }

        [Test]
        public void ThereAreMoreVisitsAtDuskThanAtMidday()
        {
            var sim = PoleCamp(5);
            var planner = LifePlanner.Wired(sim);
            planner.ForcedWeather = Weather.Clear;
            var visits = new List<LifePlanner.PrayerVisit>();
            int dusk = 0, midday = 0;
            for (long day = 0; day < 40; day++)
            {
                planner.PrayerVisitsOn(day, visits);
                foreach (var s in visits.Where(v => !v.kid).Select(v => v.start).Distinct()) { if (s >= LifePlanner.DuskFrom) dusk++; else midday++; }
            }
            Assert.Greater(dusk, midday);
            Assert.GreaterOrEqual(midday, 5, "and a midday one on some days");
            Assert.GreaterOrEqual(dusk, 30);
        }

        [Test]
        public void ThePrayNowCheatSendsPeopleAtOnceAndTheyComeBack()
        {
            var sim = Eco(5);
            var planner = LifePlanner.Wired(sim);
            planner.ForcedWeather = Weather.Clear;
            var now = new ViewTime(2, 10f);
            int n = planner.DebugPrayNow(now);
            Assert.GreaterOrEqual(n, 1, "someone goes, and a pole was made");
            var pole = planner.PolePlace;
            Assert.AreEqual(PlaceKind.Prop, pole.kind);
            int praying = 0;
            for (float h = 10f; h < 12f; h += 0.02f)
                foreach (var who in planner.People)
                    if (planner.Plan(who, new ViewTime(2, h)).kind == ActivityKind.Pray) { praying++; break; }
            Assert.Greater(praying, 3, "the pose is held");
            foreach (var who in planner.People) Assert.AreNotEqual(ActivityKind.Pray, planner.Plan(who, new ViewTime(2, 12f)).kind, "and it is over by noon");
        }
    }
}
