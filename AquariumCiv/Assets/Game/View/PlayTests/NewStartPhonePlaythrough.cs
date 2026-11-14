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

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #656/#677: the new start played the way the phone plays it (1179x2556, real touches, the first run not skipped): the founders
    /// wait at the glass's edge with the basket and the bundle while the glass is misted, walk in once it is wiped and set them down
    /// (the stock shows from then); no fire before the first bush; the mist and the name by touch; then cheats only to skip time:
    /// the first bush (the grass fire, the gatherers out to the berries), and the first wild wood (the beat, the log fire).
    /// Shots: Temp/agent/night/newstart_*.png.
    /// </summary>
    [Category("NewStartPhone")]
    public class NewStartPhonePlaythrough : PlayTestBase
    {
        const string Dir = Shots + "night/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView Run => FirstRunView.Instance;
        static NewStartView View => NewStartView.Instance;

        static IEnumerator Png(string name) { Directory.CreateDirectory(Dir); yield return Shot("night/" + name); }

        /// <summary>A closer shot: holds the view a moment (the camera glides home after a lean), then takes it.</summary>
        static IEnumerator Close(string name, Vector3 world, float zoom, float pitch = 50f)
        {
            var cam = Camera.main.GetComponent<BoxCamera>();
            for (int i = 0; i < 12; i++) { cam.CancelFlight(); cam.target = world; cam.zoom = zoom; cam.pitch = pitch; cam.Apply(); yield return null; }
            yield return Png(name);
        }

        Vector2 ScreenOf(float x, float z)
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            return Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.land.OnGround(x, z)));
        }

        static List<Vector2[]> PlayerStrokes(CareView care, float spacing)
        {
            var list = new List<Vector2[]>();
            bool right = true;
            for (float y = Screen.height * 0.98f; y > Screen.height * 0.02f; y -= spacing)
            {
                float first = -1f, last = -1f;
                for (float x = 6f; x < Screen.width - 6f; x += 10f)
                {
                    var p = new Vector2(x, y);
                    if (!care.GlassAt(p) || UiInput.Over(p)) continue;
                    if (first < 0f) first = x;
                    last = x;
                }
                if (first < 0f) continue;
                list.Add(right ? new[] { new Vector2(first, y), new Vector2(last, y) } : new[] { new Vector2(last, y), new Vector2(first, y) });
                right = !right;
            }
            return list;
        }

        IEnumerator Hold(Vector2 at, float seconds, System.Func<bool> until = null)
        {
            int id = touchId++;
            BeginTouch(id, at, queueEventOnly: true);
            float t = Time.unscaledTime;
            while (Time.unscaledTime - t < seconds && (until == null || !until())) { MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
            EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(10);
        }

        static int FireVariant()
        {
            var box = Object.FindAnyObjectByType<BoxView>();
            foreach (var p in box.layout.pieces) if (p.kind == PieceKind.Fire && p.era == Era.Camp) return p.variant;
            return -1;
        }

        [UnityTest, Timeout(2700000)]
        public IEnumerator FreshBox_TheNewStart_ByTouch_ToTheCamp()
        {
            UseLivingLand();
            yield return OpenSlice(seed: 6567, skipFirstRun: false);
            Assert.IsTrue(Sim.NewStartOn, "a new living-land box");
            Assert.IsFalse(Sim.StockSetDown, "nothing set down yet");
            var stash = Object.FindAnyObjectByType<StashProps>();
            var care = CareView.Instance;
            yield return Until(() => Run.Step == FirstRunStep.Wipe, 8f);
            yield return Frames(10);
            Assert.IsTrue(View.ArrivalWaiting, "the founders wait at the glass's edge while it is misted");
            Assert.IsFalse(stash.Visible, "no stock shows yet");

            // ---- the wipe, by touch
            float spacing = care.wipeRadius * Screen.height * 1.1f;
            for (int pass = 0; pass < 6 && Run.Step == FirstRunStep.Wipe; pass++)
                foreach (var row in PlayerStrokes(care, spacing))
                {
                    if (Run.Step != FirstRunStep.Wipe) break;
                    yield return Drag(row, 0.03f * Screen.height);
                }
            Assert.AreNotEqual(FirstRunStep.Wipe, Run.Step, "the glass wiped by touch");

            // ---- the arrival: they walk in, carrying the basket and the bundle
            yield return Until(() => View.ArrivalPlaying && !View.ArrivalWaiting, 5f);
            var props = View.HeldPeople.Select(w => View.Holds(w, out var k) ? k : PropKind.None).ToList();
            Assert.Contains(PropKind.FoodBasket, props, "the basket of food is carried in");
            Assert.Contains(PropKind.StickBundle, props, "and the bundle of sticks");
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Png("newstart_01_arrival_default");
            var carrier = View.HeldPeople.First(w => View.Holds(w, out var k) && k == PropKind.FoodBasket);
            if (PeopleView.Find().TryGetDollTransform(carrier, out var ct)) yield return Close("newstart_01b_arrival_basket_bundle", ct.position, 0.25f, 45f);
            yield return Until(() => Sim.StockSetDown, 40f);
            Assert.IsTrue(Sim.StockSetDown, "the walk-in ends with the set-down");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(stash.Visible && stash.CarriedShown, "the stock shows from the set-down: the basket and the bundle beside the pile");
            yield return Png("newstart_02_set_down");
            if (stash.TryGetCarried(0, out var basketAt)) yield return Close("newstart_02b_set_down_close", basketAt, 0.2f, 48f);
            Assert.AreEqual(-1, FireVariant(), "no fire before the first bush: they sit together where the camp will be");

            // ---- the rest of the first run, by touch
            yield return Until(() => Run.Step == FirstRunStep.Rain, 10f);
            var (rx, rz) = Run.Run.SuggestedRainSpot;
            yield return Hold(ScreenOf(rx, rz), 2.5f, () => Run.Step != FirstRunStep.Rain);
            yield return Frames(20);
            yield return Until(() => Run.Step == FirstRunStep.Name, 5f);
            yield return new WaitForSecondsRealtime(4.5f);
            yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-plate"));
            yield return new WaitForSecondsRealtime(0.4f);
            yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-name-keep"));
            yield return Frames(30);
            Assert.IsFalse(Run.Active, "named: the first run is done");
            yield return new WaitForSecondsRealtime(2f);
            yield return Png("newstart_03_no_fire_yet");
            Assert.AreEqual(-1, FireVariant(), "still no fire");
            Assert.IsFalse(Sim.JobOpen(JobType.Gatherer), "no gatherers on bare land");
            AssertNoErrors("the first run and the arrival");

            // ---- skip time to the first bush: the grass fire, and the gatherers open with berries
            EcoDebug.FirstBushNow();
            yield return Frames(20);
            Assert.AreEqual(SimLayout.GrassFireVariant, FireVariant(), "a small fire of grass and twigs");
            EcoDebug.SkipSeasons(1);
            yield return Frames(20);
            Debug.Log("[newstart] jobs: " + EcoDebug.Jobs());
            Assert.IsTrue(Sim.JobOpen(JobType.Gatherer), "the gatherers' job opens with berries on the land");
            var pv = PeopleView.Find();
            PeopleView.DebugSetHour(9f);
            pv.timeScale = 4f;
            bool berries = false; float t0 = Time.unscaledTime;
            while (!berries && Time.unscaledTime - t0 < 120f)
            {
                foreach (var a in pv.Activities) if (a.kind == ActivityKind.Gather && a.place.kind == PlaceKind.BushCell) { berries = true; break; }
                yield return null;
            }
            pv.timeScale = 1f;
            Assert.IsTrue(berries, "someone picks berries");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Png("newstart_03b_gatherers_berries");

            // ---- the first wild wood (cheat for the jump): the beat, then the log fire
            string r = NewStartView.DebugFirstWildWoodNow();
            Debug.Log("[newstart] " + r);
            yield return Until(() => View.FirstWoodPlaying, 6f);
            Assert.IsTrue(View.FirstWoodPlaying, "the first logs come home");
            yield return Until(() => !View.FirstWoodPlaying, 45f);
            Assert.AreEqual(1, View.FirstWoodPlayed);
            yield return Frames(10);
            Assert.AreEqual(0, FireVariant(), "the log fire");
            yield return Png("newstart_12_after_first_wood_default");
            AssertNoErrors("the new start");
        }
    }
}
