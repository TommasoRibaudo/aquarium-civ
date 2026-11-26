using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #708 (B70) and #709 (B71) on the real scene at 750x1334: the fish as shadows and at the glass, their dart, the kids' try by hand
    /// (B39.c); the nest built in three trips, its eggs, the gatherer's take and the scold, the kids' look, the chicks, the fly-off and the
    /// empty nest through the year. Phone shots go to Temp/agent/708/ and Temp/agent/709/.
    /// </summary>
    [Category("FishNest")]
    public class FishNestPlayTests : PlayTestBase
    {
        static SimCore Sim => GameClock.Instance.Sim;
        static bool InSeason(Season s) => LifeClock.SeasonOf(Sim.State.month) == s;
        static void ToSeasonSim(Season s) { for (int i = 0; i < 8 && !InSeason(s); i++) EcoDebug.SkipSeasons(1); }

        /// <summary>The sim's season, then the view's day over midnight so the view (the kids' water, the HUD) has it too.</summary>
        static IEnumerator ToSeason(Season s, float hour)
        {
            ToSeasonSim(s);
            var pv = PeopleView.Find();
            long d0 = pv.Now.day;
            PeopleView.DebugSetHour(23.8f);
            pv.timeScale = 20f;
            float t = Time.realtimeSinceStartup;
            while (pv.Now.day == d0 && Time.realtimeSinceStartup - t < 20f) yield return null;
            pv.timeScale = 1f;
            PeopleView.DebugSetHour(hour);
            yield return Frames(20);
            Debug.Log($"[FishNest] season: sim month {Sim.State.month} ({LifeClock.SeasonOf(Sim.State.month)}), view day {pv.Now.day} ({LifeClock.SeasonOf(pv.Now.day)}), was day {d0}");
        }

        /// <summary>Two kids join (a fresh living land has five grown-ups only).</summary>
        static IEnumerator Grown()
        {
            Debug.Log("[FishNest] kids: " + Sim.DebugAddKids(2));
            yield return Frames(30);
        }

        [TearDown]
        public void Close() => CloseSlice();

        static void Budget(FishView fv)
        {
            var sc = fv.School;
            Assert.LessOrEqual(sc.fish.Count, AnimalBudget.Fish, "drawn fish within the budget");
            Assert.LessOrEqual(sc.fish.Count, Sim.EcoFish, "never more than the stock");
            Assert.LessOrEqual(fv.SidesShown, FishSchool.MaxOnWalls, "at most two at the glass");
            foreach (var f in sc.fish)
            {
                if (f.mode == FishSchool.Mode.Shadow) Assert.AreEqual(0f, f.sideScale);
                if (f.mode == FishSchool.Mode.Wall) Assert.AreEqual(0f, f.shadowScale);
                if (f.SideDrawn) Assert.IsTrue(sc.WallFits(f.side, f.along), "a side fish inside a wall 0.06 u deep");
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Fish_Shadows_Glass_Dart_KidsByHand()
        {
            UseLivingLand();
            yield return OpenSlice(750, 1334, seed: 4242);
            Assert.IsTrue(Sim.EcologyOn);
            var fv = FishView.Instance;
            Assert.IsNotNull(fv, "the fish view is on the box");
            yield return Grown();
            if (Sim.EcoFish == 0) Assert.AreEqual(0, fv.Wanted, "no fish before they are placed");
            string r = FishView.DebugShadows(); // the water first (it passes seasons), the fish placed
            yield return ToSeason(Season.Summer, 11f);
            Debug.Log("[FishNest] shadows: " + r);
            Assert.Greater(Sim.EcoFish, 0, r);
            yield return Until(() => fv.ShadowsShown > 0, 20f);
            Assert.Greater(fv.ShadowsShown, 0, "shadows on the water after the arrival");
            for (int i = 0; i < 60; i++) { Budget(fv); yield return null; }
            FishView.DebugShadows();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("708/pond-shadows");

            int darts = fv.School.Darts;
            Debug.Log("[FishNest] startle: " + FishView.DebugStartle());
            Assert.Greater(fv.School.Darts, darts, "they dart from a ring");
            yield return new WaitForSecondsRealtime(0.12f);
            yield return Shot("708/pond-dart");

            string g = FishView.DebugGlass();
            Debug.Log("[FishNest] glass: " + g);
            if (g.Contains("pane"))
            {
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.Greater(fv.SidesShown, 0, "a fish at the glass");
                for (int i = 0; i < 90; i++) { Budget(fv); yield return null; }
                yield return Shot("708/glass-fish");
            }
            else Debug.LogWarning("[FishNest] no wall for side fish on this land: " + g);

            // B39.c: a kid at the edge tries for a fish by hand: the ring scares them, nobody catches one
            int stock = Sim.EcoFish;
            string k = KidPlayDirector.Debug(PlayGame.FishGrab);
            Debug.Log("[FishNest] kids: " + k);
            StringAssert.Contains("FishGrab", k);
            darts = fv.School.Darts;
            FishView.DebugShadows();
            yield return Until(() => fv.School.Darts > darts, 25f);
            Assert.Greater(fv.School.Darts, darts, "the grab's splash makes the fish dart");
            Assert.AreEqual(stock, Sim.EcoFish, "they never catch one");
            yield return Shot("708/kid-edge");

            ToSeasonSim(Season.Winter);
            yield return Frames(20);
            Assert.IsTrue(fv.School.winter, "winter: slower and low");
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Nest_Built_Eggs_Take_Look_Chicks_FlyOff_Year()
        {
            UseLivingLand();
            yield return OpenSlice(750, 1334, seed: 4242);
            var nv = NestView.Instance;
            Assert.IsNotNull(nv, "the nest view is on the box");
            var pv = PeopleView.Find();
            yield return Grown();
            yield return ToSeason(Season.Spring, 8f);
            if (!Sim.NestStands) Assert.IsFalse(nv.NestShown, "no nest before one is built");
            int bush = Sim.DebugNestPlace();
            Assert.GreaterOrEqual(bush, 0, "a nest in a thicket");
            yield return Until(() => nv.BirdShown, 10f);
            Debug.Log("[FishNest] " + NestView.DebugLook());
            yield return Until(() => nv.TripsDone >= 1, 10f);
            Assert.That(nv.NestGrowth, Is.EqualTo(0.6f).Within(0.01f), "the first twig: 0.6");
            yield return Until(() => nv.TripsDone >= NestView.Trips && nv.Bird == NestView.BirdState.Perch, 25f);
            Assert.AreEqual(NestView.Trips, nv.TripsDone, "three trips");
            Assert.That(nv.NestGrowth, Is.EqualTo(1f).Within(0.01f));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("709/nest-built");

            int laid = Sim.DebugNestLay();
            Assert.Greater(laid, 0);
            yield return Until(() => nv.EggsShown == Sim.NestEggs, 10f);
            Assert.AreEqual(Sim.NestEggs, nv.EggsShown, "the eggs come in one by one");
            Assert.AreEqual(NestView.BirdState.Sit, nv.Bird, "the bird sits on them");
            yield return Shot("709/nest-eggs-sitting");

            // B71.d: a gatherer takes one (never the last), the bird scolds, the egg rides to the pile
            int eggs = Sim.NestEggs;
            Assert.IsTrue(pv.Planner.DebugStartNestVisit(pv.Now, true, out var gp), "a gatherer goes");
            pv.timeScale = 3f;
            yield return Until(() => pv.Now.hour >= gp.pickStart, 30f);
            yield return Until(() => nv.Bird == NestView.BirdState.Circle, 10f);
            Assert.AreEqual(NestView.BirdState.Circle, nv.Bird, "the bird is off the nest for the take");
            yield return Until(() => Sim.NestEggsTakenThisSpring > 0, 30f);
            Assert.AreEqual(eggs - 1, Sim.NestEggs, "one egg taken");
            yield return Frames(3);
            Assert.AreEqual(Sim.NestEggs, nv.EggsShown, "the drawn eggs follow");
            yield return Until(() => nv.Scolds > 0, 6f);
            Assert.Greater(nv.Scolds, 0, "the bird scolds her");
            pv.timeScale = 1f;

            // B71.c: the kids look in
            int looks = Sim.NestLooks;
            Assert.IsTrue(pv.Planner.DebugStartNestVisit(pv.Now, false, out var lp), "kids go");
            pv.timeScale = 3f;
            yield return Until(() => Sim.NestLooks > looks, 40f);
            Assert.Greater(Sim.NestLooks, looks, "they looked");
            pv.timeScale = 1f;

            int eggsLeft = Sim.NestEggs;
            Assert.Greater(Sim.DebugNestHatch(), 0);
            yield return Until(() => nv.ChicksShown == Sim.NestChicks, 5f);
            Assert.AreEqual(Mathf.Min(2, eggsLeft), Sim.NestChicks, "two chicks (Q5), or one if one egg was left");
            Assert.AreEqual(Sim.NestChicks, nv.ChicksShown, "the drawn chicks follow");
            Assert.AreEqual(0, nv.EggsShown);
            NestView.DebugLook();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("709/nest-chicks");

            Assert.IsTrue(Sim.DebugNestFlyOff());
            yield return Until(() => nv.Bird == NestView.BirdState.Away && nv.ChicksShown == 0, 10f);
            Assert.IsTrue(nv.NestShown, "the empty nest stays");
            yield return Shot("709/nest-empty-spring");

            ToSeasonSim(Season.Summer); yield return Frames(20);
            Assert.IsTrue(nv.NestShown, "summer: the empty nest");
            NestView.DebugLook(); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("709/nest-summer");
            ToSeasonSim(Season.Autumn); yield return Frames(20);
            NestView.DebugLook(); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("709/nest-autumn");
            ToSeasonSim(Season.Winter); yield return Frames(20);
            Assert.AreEqual(Sim.NestStands, nv.NestShown, "winter: as the sim has it (it keeps the empty nest)");
            NestView.DebugLook(); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("709/nest-winter");
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
