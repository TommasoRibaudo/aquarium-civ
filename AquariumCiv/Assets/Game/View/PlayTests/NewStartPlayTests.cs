using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #656/#677 (the user, 2026-11-14), the view: the founders walk in with a basket of food and a bundle of sticks and set them down
    /// at the camp spot (the stock shows only from then); no fire before the first bush, a small fire of dry grass and twigs, the log
    /// fire from the first wild wood (a moment played once); tinder runs and the deadfall read as grass, twigs and fallen branches; hunger
    /// is a look at the sky. Shots to Temp/agent/night/newstart_*.png.
    /// </summary>
    [Category("NewStart")]
    public class NewStartPlayTests : PlayTestBase
    {
        const string Dir = Shots + "night/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static NewStartView View => NewStartView.Instance;
        static FirstRunView Run => FirstRunView.Instance;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        static IEnumerator Png(string name)
        {
            Directory.CreateDirectory(Dir);
            // the camera glides home after a lean or a placed-life arrival: hold the shot's view for a moment, then take it
            if (lookSet) for (int i = 0; i < 12; i++) { Look(lookAt, lookZoom, lookPitch); yield return null; }
            yield return Shot("night/" + name);
            lookSet = false;
        }
        static bool lookSet; static Vector3 lookAt; static float lookZoom, lookPitch;

        static void Look(Vector3 world, float zoom, float pitch = 52f)
        {
            lookSet = true; lookAt = world; lookZoom = zoom; lookPitch = pitch;
            var rig = Cam;
            rig.CancelFlight();
            rig.target = world; rig.zoom = zoom; rig.pitch = pitch;
            rig.Apply();
        }

        static Vector3 World(float x, float z)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return box.transform.TransformPoint(box.land.OnGround(x, z));
        }

        static Vector3 FireWorld()
        {
            PeopleView.Find().Planner.FirePoint(out float fx, out float fz);
            return World(fx, fz);
        }

        static Vector3 Doll(PersonRef who)
        {
            var pv = PeopleView.Find();
            return pv.TryGetDollTransform(who, out var t) ? t.position : FireWorld();
        }

        static int FireVariant()
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            foreach (var p in box.layout.pieces) if (p.kind == PieceKind.Fire && p.era == Era.Camp) return p.variant;
            return -1; // no fire drawn
        }

        static StashProps Stash => Object.FindAnyObjectByType<StashProps>();

        IEnumerator OpenNewBox(int seed)
        {
            UseLivingLand();
            yield return OpenSlice(seed: seed);
            Assert.IsTrue(Sim.NewStartOn, "a new living-land box begins with the carried stock");
            Assert.IsNotNull(View, "the new start's view is up");
        }

        // ------------------------------------------------------------------ the set-down
        [UnityTest, Timeout(300000)]
        public IEnumerator TheArrival_SetsTheStockDown_ThePileShowsOnlyThen()
        {
            yield return OpenNewBox(656);
            Assert.IsTrue(Sim.StockSetDown, "a test that skips the first run has its stock set down at once");
            yield return Frames(10);
            Assert.IsTrue(Stash.Visible && Stash.CarriedShown, "set down: the pile with the basket and the bundle beside it");

            string r = NewStartView.DebugArrivalAgain();
            Debug.Log("[newstart] " + r);
            yield return Frames(5);
            Assert.IsFalse(Sim.StockSetDown, "the basket and the bundle are carried again");
            Assert.IsTrue(View.ArrivalPlaying, "the founders walk in");
            yield return Frames(5);
            Assert.IsFalse(Stash.Visible, "no stock shows before the set-down");
            var props = View.HeldPeople.Select(w => View.Holds(w, out var k) ? k : PropKind.None).ToList();
            Assert.Contains(PropKind.FoodBasket, props, "one carries the basket of food");
            Assert.Contains(PropKind.StickBundle, props, "one carries the bundle of sticks");

            yield return Until(() => Sim.StockSetDown, 45f);
            Assert.IsTrue(Sim.StockSetDown, "the walk-in ends with the set-down");
            yield return Frames(5);
            Assert.IsTrue(Stash.Visible && Stash.CarriedShown, "from the set-down: the pile, the basket and the bundle");
            yield return Until(() => !View.ArrivalPlaying, 10f);
            Assert.IsFalse(View.ArrivalPlaying, "then they go about their day");
            StringAssert.DoesNotContain("wood", View.LastLine.ToLowerInvariant(), "the sticks are never called wood");
            AssertNoErrors("the arrival");
        }

        // ------------------------------------------------------------------ the fire
        [UnityTest, Timeout(300000)]
        public IEnumerator TheFire_None_GrassAndTwigs_Logs()
        {
            yield return OpenNewBox(677);
            yield return Frames(20);
            Assert.AreEqual(CampFireStage.None, Sim.FireStage, "a fresh barren box: nothing to burn yet");
            Assert.AreEqual(-1, FireVariant(), "no fire is drawn before the first bush");

            EcoDebug.FirstBushNow();
            yield return Frames(20);
            Assert.AreEqual(CampFireStage.GrassAndTwig, Sim.FireStage, "the first bush: dry grass and twigs to burn");
            Assert.AreEqual(SimLayout.GrassFireVariant, FireVariant(), "the box draws the small grass-and-twig fire");
            PeopleView.DebugSetHour(12f);
            yield return new WaitForSecondsRealtime(2f);
            Look(FireWorld(), 0.22f, 40f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Png("newstart_04_grass_fire_day");
            PeopleView.DebugSetHour(19.4f);
            yield return new WaitForSecondsRealtime(2.5f);
            Look(FireWorld(), 0.22f, 40f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Png("newstart_05_grass_fire_dusk");

            Sim.DebugFireStage(2);
            Object.FindAnyObjectByType<BoxView>().Refresh();
            yield return Frames(20);
            Assert.AreEqual(0, FireVariant(), "the log fire");
            Sim.DebugFireStage(-1);
            yield return Frames(20);
            Assert.AreEqual(SimLayout.GrassFireVariant, FireVariant(), "back to what the land lets burn");
            AssertNoErrors("the fire stages");
        }

        // ------------------------------------------------------------------ the first wild wood
        [UnityTest, Timeout(300000)]
        public IEnumerator TheFirstWildWood_PlaysOnce_ThenTheLogFire()
        {
            yield return OpenNewBox(4677);
            EcoDebug.FirstBushNow();
            PeopleView.DebugSetHour(10.5f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 15f); // the bushes' arrival and its lean are over
            yield return new WaitForSecondsRealtime(6f);
            Assert.AreEqual(SimLayout.GrassFireVariant, FireVariant());
            string r = NewStartView.DebugFirstWildWoodNow();
            Debug.Log("[newstart] " + r);
            Assert.IsTrue(Sim.FirstWildWoodReached, r);
            yield return Until(() => View.FirstWoodPlaying, 5f);
            Assert.IsTrue(View.FirstWoodPlaying, "the first logs are carried home, watched");
            Assert.AreEqual(SimLayout.GrassFireVariant, FireVariant(), "the grass fire burns till the logs are on the pile");
            Assert.IsTrue(StashProps.HoldLogs, "the pile holds its logs back till they are set on it");
            var carriers = View.HeldPeople.ToList();
            Assert.That(carriers.Count, Is.InRange(1, 2), "one or two grown-ups fetch them");
            // the shot of the carry home: once a log is on a shoulder
            yield return Until(() => carriers.Any(w => View.Holds(w, out var k) && k == PropKind.Log), 30f);
            yield return new WaitForSecondsRealtime(1.2f);
            Look(Doll(carriers[0]), 0.3f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Png("newstart_08_first_wood_beat");
            yield return Until(() => !View.FirstWoodPlaying, 45f);
            Assert.IsFalse(View.FirstWoodPlaying, "the beat is short");
            Assert.AreEqual(1, View.FirstWoodPlayed, "once");
            Assert.IsFalse(Sim.FirstWildWoodPending, "seen");
            Assert.IsFalse(StashProps.HoldLogs);
            yield return Frames(10);
            Assert.AreEqual(0, FireVariant(), "the fire is built up as a log fire");
            Assert.That(Stash.LogsShown, Is.GreaterThan(0), "the first logs lie on the pile");
            Assert.IsTrue(Stash.CarriedShown, "the bundle of sticks is kept beside them");
            Look(FireWorld(), 0.24f, 40f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Png("newstart_09_log_fire_after");
            if (Stash.TryGetCarried(1, out var bundle)) Look(bundle, 0.16f, 50f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Png("newstart_10_pile_bundle_logs");
            yield return new WaitForSecondsRealtime(4f);
            Assert.AreEqual(1, View.FirstWoodPlayed, "never again");
            AssertNoErrors("the first wild wood");
        }

        // ------------------------------------------------------------------ the runs and hunger
        [UnityTest, Timeout(600000)]
        public IEnumerator TinderRuns_Deadfall_AndHunger_ReadAsTheyShould()
        {
            yield return OpenNewBox(1656);
            var pv = PeopleView.Find();
            EcoDebug.FirstBushNow();
            PeopleView.DebugSetHour(9.5f);
            yield return Frames(20);
            // a tinder run: an armful of dry grass and twigs, never a log or an axe
            pv.timeScale = 4f;
            PersonRef tinder = default; PropKind seen = PropKind.None;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 120f && tinder.IsNone)
            {
                foreach (var a in pv.Activities)
                    if ((a.kind == ActivityKind.Carry && a.prop == AquariumCiv.Life.Prop.Stick && a.from.kind == PlaceKind.Meadow && a.moving)
                        && pv.TryGetDollTransform(a.who, out var t) && t.gameObject.activeInHierarchy)
                    { tinder = a.who; break; }
                yield return null;
            }
            pv.timeScale = 1f;
            Assert.IsFalse(tinder.IsNone, "someone carries dry grass and twigs to the grass fire");
            yield return Frames(3);
            Assert.IsTrue(pv.TryGetDollTransform(tinder, out var tt));
            var view = tt.GetComponent<PegDollView>();
            seen = view != null ? view.propKind : PropKind.None;
            Assert.AreEqual(PropKind.Tinder, seen, "an armful of dry grass and twigs");
            Look(Doll(tinder), 0.2f);
            yield return Frames(2);
            yield return Png("newstart_06_tinder_run");

            // hunger: the basket empty, now and then someone stands and looks at the sky
            Sim.DebugEmptyBasket();
            Assert.IsTrue(Sim.Hungry);
            PersonRef looker = default;
            t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 40f && looker.IsNone)
            {
                foreach (var a in pv.Activities)
                    if (pv.TryGetDollTransform(a.who, out var t) && t.gameObject.activeInHierarchy && !pv.IsWalking(a.who) && PeopleView.HungryLook(a.who, 0f)
                        && pv.TryGetDollPose(a.who, out var pose) && pose.lean < -10f)
                    { looker = a.who; break; }
                yield return null;
            }
            Assert.IsFalse(looker.IsNone, "hungry: someone looks up at the sky");
            Look(Doll(looker), 0.2f, 68f);
            yield return Frames(2);
            yield return Png("newstart_11_hungry_looks_up");
            Assert.AreEqual(0, Sim.State.food, 0.001f);
            Sim.State.food = 6f;

            // the deadfall: a Keeper's tree with a wild tree beside it: fallen branches picked up, carried home over a shoulder
            EcoDebug.PlantHere();
            EcoDebug.TreeStage(4);
            string w = EcoDebug.WildTrees(true);
            Debug.Log("[newstart] " + w);
            yield return Frames(10);
            Assume.That(Sim.DeadfallTreeCount, Is.GreaterThan(0), "a wild tree stands by the Keeper's: " + w);
            pv.Planner.Invalidate();
            pv.timeScale = 4f;
            PersonRef picker = default; bool carrying = false;
            t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 150f && picker.IsNone)
            {
                foreach (var a in pv.Activities)
                {
                    bool pick = LifePlanner.IsDeadfallGather(a) && !a.moving && !a.enRoute;
                    bool carry = a.kind == ActivityKind.Carry && a.from.kind == PlaceKind.Deadfall && a.moving;
                    if ((pick || carry) && pv.TryGetDollTransform(a.who, out var t) && t.gameObject.activeInHierarchy) { picker = a.who; carrying = carry; break; }
                }
                yield return null;
            }
            pv.timeScale = 1f;
            Assert.IsFalse(picker.IsNone, "someone gathers fallen branches under the wild tree");
            yield return Frames(3);
            pv.TryGetDollTransform(picker, out var pt);
            var pk = pt.GetComponent<PegDollView>().propKind;
            Assert.IsTrue(pk != PropKind.Axe && pk != PropKind.Log, "no axe, no cut log: " + pk);
            if (carrying) Assert.AreEqual(PropKind.Branch, pk, "a fallen branch over the shoulder");
            Look(Doll(picker), 0.2f);
            yield return Frames(2);
            yield return Png("newstart_07_deadfall_" + (carrying ? "carry" : "pickup"));
            AssertNoErrors("the runs and hunger");
        }
            // ------------------------------------------------------------------ tester 2026-11-14
        /// <summary>Hungry on bare land with nothing placed: once a visit someone says what they lack: the land greening while no jar is
        /// ready; something that grows food once the bushes' jar is ready (and the Kit's dot stays on).</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Hungry_OnBareLand_TheWayOutShows()
        {
            yield return OpenNewBox(2656);
            Assert.IsFalse(Sim.JarReady(AquariumCiv.Sim.Eco.LifeSpecies.Thicket), "a bare box: the bushes' jar is not ready");
            Sim.DebugEmptyBasket();
            View.DebugNewVisit();
            yield return Until(() => View.HungerLinesSaid > 0, 20f);
            Assert.AreEqual(1, View.HungerLinesSaid, "a hunger line, once");
            CollectionAssert.Contains(NewStartView.HungryLandLines, View.LastHungerLine, "no jar ready: the line looks to the land greening");
            yield return new WaitForSecondsRealtime(8f);
            Assert.AreEqual(1, View.HungerLinesSaid, "at most once a visit");

            Sim.DebugJarsReady();
            Assert.IsTrue(Sim.JarReady(AquariumCiv.Sim.Eco.LifeSpecies.Thicket));
            View.DebugNewVisit();
            yield return Until(() => View.HungerLinesSaid > 1, 20f);
            Assert.AreEqual(2, View.HungerLinesSaid, "the next visit says it again");
            CollectionAssert.Contains(NewStartView.HungryJarLines, View.LastHungerLine, "the jar is ready: they want something that grows food");
            yield return Frames(10);
            Assert.IsTrue(GameHud.Instance.LifeDotShown, "the Kit's dot stays on while the jar waits");
            Look(FireWorld(), 0.35f);
            yield return Png("newstart_13_hungry_jar_line");
            StringAssert.DoesNotContain("root", View.LastHungerLine.ToLowerInvariant());
            AssertNoErrors("hunger's way out");
        }

        /// <summary>The first logs wait for a wedding (or any moment) to end, and lie on the pile only once they are set down.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator TheFirstWood_WaitsForAMoment_LogsOnlyWhenSetDown()
        {
            yield return OpenNewBox(4677);
            EcoDebug.FirstBushNow();
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 15f);
            string m = StagedMoments.DebugPlayNow(MomentKind.Wedding);
            Debug.Log("[newstart] " + m);
            yield return Frames(30);
            bool moment = StagedMoments.Playing > 0 || StagedMoments.PlayingMoment(Sim) != null;
            NewStartView.DebugFirstWildWoodNow();
            yield return Frames(30);
            Assert.IsTrue(Sim.FirstWildWoodPending);
            Assert.AreEqual(0, Stash.LogsShown, "no logs on the pile before they are carried home");
            if (moment) Assert.IsFalse(View.FirstWoodPlaying, "the first logs wait while the wedding plays");
            yield return Until(() => View.FirstWoodPlaying, 120f);
            Assert.IsTrue(View.FirstWoodPlaying, "then they come");
            Assert.AreEqual(0, Stash.LogsShown, "still none on the pile while they are carried");
            yield return Until(() => !View.FirstWoodPlaying, 45f);
            Assert.That(Stash.LogsShown, Is.GreaterThan(0), "set down: on the pile");
            AssertNoErrors("the queued first wood");
        }
}
}
