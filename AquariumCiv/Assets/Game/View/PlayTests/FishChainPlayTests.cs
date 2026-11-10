using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #543 on the real Slice scene (1179x2556): a living-land camp with a pond and fish placed. A fisher's catch lands in the fish basket (its own look, the
    /// sim's count goes up), a grown-up walks the fish basket to the camp's one fish rack and hangs the fish up (the rack shows them one by one), dry fish
    /// are taken down and carried to the food pile (the pile shows them, beside the berries). Shots in Temp/agent/fb4/543/.
    /// </summary>
    [Category("FishChainPlay")]
    public class FishChainPlayTests : PlayTestBase
    {
        const string Dir = Shots + "fb4/543/";

        static SimCore Sim => GameClock.Instance.Sim;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();

        static void Look(Vector3 world, float zoom, float pitch = 52f)
        {
            var rig = Cam;
            rig.CancelFlight();
            rig.target = world; rig.zoom = zoom; rig.pitch = pitch;
            rig.Apply();
        }

        static IEnumerator Png(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Dir + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        static Vector3 World(BoxView box, float x, float z) => box.transform.TransformPoint(box.land.OnGround(x, z));

        [UnityTest, Timeout(900000)]
        public IEnumerator ACatchGoesBasketRackPile()
        {
            UseLivingLand();
            yield return OpenSlice(1179, 2556, 4242);
            var sim = Sim;
            Assert.IsTrue(sim.EcologyOn, "a living-land box");
            Assert.AreEqual(Era.Camp, sim.State.era);
            var box = Object.FindAnyObjectByType<BoxView>();
            var pv = PeopleView.Find();
            StringAssert.Contains("broke the surface", EcoDebug.WaterBreaksNow());
            EcoDebug.Table(0.3f); // a pond deep enough, then the ducks and the fish (#421): no fishing before them
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(AquariumCiv.Sim.Eco.LifeSpecies.Ducks), 0, "the ducks placed");
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(AquariumCiv.Sim.Eco.LifeSpecies.Fish), 0, "the fish placed");
            Assert.IsTrue(sim.FishingWater, "fishing stands on open water");
            yield return Until(() => LifeArrivalView.Instance == null || !LifeArrivalView.Instance.AnyPlaying, 20f);
            PeopleView.DebugSetHour(10f);
            yield return Frames(20);
            pv.Planner.ClearLive();
            var fv = FishingView.Ensure();
            var rv = FishRackView.Ensure(box);
            var props = GlassVoice.Instance.GetComponent<StashProps>();
            Assert.IsNotNull(fv); Assert.IsNotNull(rv); Assert.IsNotNull(props);
            Assert.AreEqual(0, sim.FishInBasket); Assert.AreEqual(0, sim.FishOnRack); Assert.AreEqual(0, sim.FishInPile);
            Assert.AreEqual(0, props.FishShown, "no fish on the pile yet");

            // 1. a catch lands in the fish basket: the sim's basket count goes up, the basket beside the fisher shows it
            StringAssert.Contains("sent to fish", FishingView.DebugBiteNow());
            yield return Until(() => fv.FishKept > 0, 60f);
            Assert.Greater(fv.FishKept, 0, "a catch was kept");
            Assert.Greater(sim.FishInBasket, 0, "it is in the fish basket");
            yield return Frames(30);
            var fisher = pv.Activities.FirstOrDefault(a => a.kind == ActivityKind.Fish && !a.moving);
            if (fisher.kind == ActivityKind.Fish && pv.TryGetDollPosition(fisher.who, out var at)) Look(World(box, at.x, at.y), 0.18f, 45f);
            yield return Frames(6);
            yield return Png("543_1_fish_basket");

            // the rack stands by the fire once fishing runs
            Assert.IsTrue(pv.Planner.TryGetFishRack(pv.Now, out var rack));
            var rackAt = World(box, rack.x, rack.z) + Vector3.up * 0.06f;
            pv.Planner.FirePoint(out float fx, out float fz);
            var fire = World(box, fx, fz);
            Look((rackAt + fire) * 0.5f, 0.42f);
            yield return Frames(8);
            Assert.IsTrue(rv.Visible, "the fish rack is drawn");
            yield return Png("543_2_rack_empty_wide");

            // 2. someone carries the fish basket to the rack and hangs the fish up, one by one
            pv.Planner.ClearLive();
            StringAssert.Contains("fish in the basket", FishRackView.DebugFillBasket());
            Assert.GreaterOrEqual(sim.FishInBasket, 6, "six fish in the basket");
            int hung = sim.FishInBasket;
            int peak = 0, steps = 0, last = 0;
            Look(rackAt, 0.2f, 40f);
            var hungShot = false;
            for (int i = 0; i < 1500 && sim.FishOnRack < hung; i++)
            {
                yield return null;
                int now = rv.FreshShown + rv.DryShown;
                if (now > last) steps++;
                last = now; peak = Mathf.Max(peak, now);
                if (!hungShot && now >= 3) { hungShot = true; yield return Png("543_3_hanging"); }
            }
            Assert.AreEqual(hung, sim.FishOnRack, "all of them hang on the rack");
            Assert.AreEqual(0, sim.FishInBasket, "the basket is empty");
            Assert.GreaterOrEqual(steps, 3, "the rack fills fish by fish, not all at once");
            yield return Frames(10);
            Assert.AreEqual(hung, rv.FreshShown, "fresh fish: they were hung today");
            yield return Png("543_4_rack_fresh_close");

            // 3. a day on they are dry: someone takes them down and carries them to the pile
            pv.Planner.ClearLive();
            StringAssert.Contains("dry fish on the rack", FishRackView.DebugRack());
            yield return Until(() => rv.DryShown == 6, 5f);
            Assert.AreEqual(6, rv.DryShown, "dry fish hang on the rack");
            Look(rackAt, 0.2f, 40f);
            yield return Frames(6);
            yield return Png("543_5_rack_dry_close");
            yield return Until(() => sim.FishInPile >= 6, 90f);
            Assert.AreEqual(6, sim.FishInPile, "the dry fish reached the pile");
            Assert.AreEqual(0, sim.FishOnRack, "and left the rack");
            yield return Frames(10);
            Assert.AreEqual(0, rv.FreshShown + rv.DryShown, "the rack is bare again");
            Assert.Greater(props.FishShown, 0, "the pile shows the fish");
            Assert.IsTrue(pv.Planner.TryGetStash(out var pile));
            StashProps.DebugStock(sim, true); // berries too: the pile shows both
            yield return Frames(6);
            Look(World(box, pile.x, pile.z) + Vector3.up * 0.03f, 0.2f, 45f);
            yield return Frames(8);
            yield return Png("543_6_pile_with_fish");
            Look((rackAt + fire) * 0.5f, 0.42f);
            yield return Frames(6);
            yield return Png("543_7_camp_wide");
            AssertNoErrors("fish chain");
        }
    }
}
