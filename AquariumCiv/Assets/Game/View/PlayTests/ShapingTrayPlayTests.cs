using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Shaping;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using K = AquariumCiv.Sim.SimCore.NudgeKind;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#84: the shaping tray by touch on the real Slice scene at 1179 x 2556. Open it from the HUD, pick a tool, place it with
    /// one finger (a charge is spent), no charge says why, Cancel costs nothing, two fingers don't place, Take back refunds, and a
    /// wish's Give places at the moved spot. Screenshots go to Temp/agent (84_*).</summary>
    public class ShapingTrayPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static ShapingView Sv => ShapingView.Instance;
        static VisualElement HudRoot => GameHud.Instance.Root;

        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return e == null;
        }

        VisualElement Ui => Sv.GetComponentInChildren<UIDocument>().rootVisualElement;

        IEnumerator TapHud(string name)
        {
            var e = HudRoot.Q(name);
            Assert.IsNotNull(e, name);
            Assert.IsTrue(Shown(e), name + " is on screen");
            yield return TapAt(GameUiPanel.ScreenCenter(e));
            yield return Frames(4);
        }

        IEnumerator Start()
        {
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(4f);
            yield return null;
            Sim.CleanAll();
            Sim.DebugRefillShaping();
            yield return Frames(4);
        }

        (float x, float z) Spot(float x, float z, bool deer = true)
        {
            foreach (var (px, pz) in new[] { (x, z), (-x, z), (x, -z), (-x, -z), (0.3f, 1.2f), (-1.4f, -0.4f) })
            {
                float ax = px, az = pz;
                if (!Sim.Settlement.OpenSpotNear(ref ax, ref az, 0.35f)) continue;
                if (deer && Sim.HerdVariantAt(ax, az) != "deer") continue;
                return (ax, az);
            }
            Assert.Fail("no open land");
            return default;
        }

        IEnumerator OpenTray()
        {
            yield return TapHud("hud-shape");
            Assert.AreEqual(GameHud.Shaping, GameHud.Instance.OpenSheet);
            yield return Frames(3);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PickAToolAndATapPlacesItAndSpendsACharge()
        {
            yield return Start();
            Sim.State.shapingCharges = 3f;
            yield return OpenTray();
            Assert.IsTrue(Shown(HudRoot.Q("shaping-charges")));
            Assert.AreEqual("3 of " + (int)Sim.ShapingMaxCharges, HudRoot.Q<Label>("shaping-charges-text").text);
            foreach (var kind in SimCore.SliceTools) Assert.IsTrue(Shown(HudRoot.Q("shaping-tool-" + kind)), kind);
            Assert.IsTrue(HudRoot.Q("shaping-tool-" + K.Channel).ClassListContains("tool--off"), "the channel is greyed in the camp");
            Assert.That(HudRoot.Q("shaping-tool-" + K.Channel).Q<Label>(className: "tool-reason").text, Does.Contain("village"));
            yield return Shot("84_tray");

            yield return TapAt(GameUiPanel.ScreenCenter(HudRoot.Q("shaping-tool-" + K.Grove)));
            yield return Frames(4);
            Assert.IsNull(GameHud.Instance.OpenSheet, "the sheet closes so the world is free");
            Assert.AreEqual(K.Grove, Sv.Tool);
            Assert.IsTrue(Sv.RealTool);
            var banner = Ui.Q<Label>("shaping-banner");
            Assert.IsTrue(Shown(Ui.Q("shaping-wish-bar")));
            Assert.That(banner.text, Does.StartWith("Grove"));
            yield return Shot("84_armed");

            var (x, z) = Spot(1.3f, 0.6f);
            int before = Sim.State.nudges.Count;
            float charges = Sim.ShapingCharges;
            yield return TapAt(Sv.ScreenOf(x, z));
            yield return Frames(6);
            Assert.AreEqual(before + 1, Sim.State.nudges.Count, Sv.LastResult);
            Assert.AreEqual(charges - 1f, Sim.ShapingCharges, 1e-3f, "a charge is spent");
            Assert.IsFalse(Sv.ShapeMode, "one gift per arming");
            Assert.IsFalse(Shown(Ui.Q("shaping-wish-bar")));
            Assert.That(Sv.LastResult, Does.Contain("placed"));
            yield return Shot("84_placed");
            AssertNoErrors("place");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator NoChargeShowsWhyAndCancelCostsNothing()
        {
            yield return Start();
            Sim.State.shapingCharges = 0f;
            yield return OpenTray();
            var rain = HudRoot.Q("shaping-tool-" + K.Rain);
            Assert.IsTrue(rain.ClassListContains("tool--off"));
            Assert.That(rain.Q<Label>(className: "tool-reason").text, Does.Contain("No charge"));
            yield return Shot("84_nocharge");
            yield return TapAt(GameUiPanel.ScreenCenter(rain));
            yield return Frames(4);
            Assert.IsFalse(Sv.ShapeMode, "an empty tray arms nothing");
            Assert.AreEqual(GameHud.Shaping, GameHud.Instance.OpenSheet, "the sheet stays, with the reason");
            Assert.That(Sv.LastResult, Does.Contain("No charge"));
            Assert.AreEqual(0, Sim.State.nudges.Count);

            Sim.DebugRefillShaping();
            yield return Frames(4);
            float charges = Sim.ShapingCharges;
            yield return TapAt(GameUiPanel.ScreenCenter(HudRoot.Q("shaping-tool-" + K.Rain)));
            yield return Frames(4);
            Assert.IsTrue(Sv.ShapeMode);
            yield return TapAt(GameUiPanel.ScreenCenter(Ui.Q("shaping-cancel")));
            yield return Frames(4);
            Assert.IsFalse(Sv.ShapeMode, "Cancel puts it down");
            Assert.AreEqual(charges, Sim.ShapingCharges, "nothing spent");
            Assert.AreEqual(0, Sim.State.nudges.Count);
            AssertNoErrors("nocharge");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TwoFingersMoveTheCameraAndPlaceNothing()
        {
            yield return Start();
            Assert.IsTrue(Sv.ArmTool(K.Grove));
            yield return Frames(3);
            var (x, z) = Spot(1.3f, 0.6f);
            var p = Sv.ScreenOf(x, z);
            int a = touchId++, b = touchId++;
            BeginTouch(a, p, queueEventOnly: true);
            BeginTouch(b, p + new Vector2(140, 60), queueEventOnly: true);
            yield return null; yield return null;
            MoveTouch(a, p + new Vector2(-30, 0), delta: Vector2.zero, queueEventOnly: true);
            MoveTouch(b, p + new Vector2(200, 60), delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(a, p, queueEventOnly: true);
            EndTouch(b, p, queueEventOnly: true);
            yield return Frames(6);
            Assert.AreEqual(0, Sim.State.nudges.Count, "two fingers place nothing");
            Assert.AreEqual(K.Grove, Sv.Tool, "the tool stays in hand");
            AssertNoErrors("two fingers");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TakeBackRefundsFromTheChipAndTheList()
        {
            yield return Start();
            Sim.State.shapingCharges = 3f;
            Assert.IsTrue(Sv.ArmTool(K.Grove));
            var (x, z) = Spot(1.3f, 0.6f);
            yield return TapAt(Sv.ScreenOf(x, z));
            yield return Frames(4);
            Assert.AreEqual(1, Sim.State.nudges.Count, Sv.LastResult);
            Assert.AreEqual(2f, Sim.ShapingCharges, 1e-3f);
            var chip = Ui.Q("shaping-takeback-0");
            yield return Until(() => Shown(chip), 12f); // the arrival is seen first
            Assert.IsTrue(Shown(chip), "the chip shows outside shape mode");
            yield return Shot("84_chip");
            yield return TapAt(GameUiPanel.ScreenCenter(chip));
            yield return Frames(4);
            Assert.AreEqual(0, Sim.State.nudges.Count, "taken back");
            Assert.AreEqual(3f, Sim.ShapingCharges, 1e-3f, "the charge comes back");

            // Again, from the tray's list.
            Assert.IsTrue(Sv.ArmTool(K.Grove));
            var (x2, z2) = Spot(-1.2f, 0.6f);
            yield return TapAt(Sv.ScreenOf(x2, z2));
            yield return Frames(4);
            Assert.AreEqual(1, Sim.State.nudges.Count);
            yield return OpenTray();
            var item = HudRoot.Query<Button>(className: "tray-takeback").First();
            Assert.IsNotNull(item, "the list has it");
            yield return Shot("84_takeback_list");
            yield return TapAt(GameUiPanel.ScreenCenter(item));
            yield return Frames(4);
            Assert.AreEqual(0, Sim.State.nudges.Count);
            Assert.AreEqual(3f, Sim.ShapingCharges, 1e-3f);
            AssertNoErrors("take back");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ChannelAndHerdPlaceAtTheDragEndAndTheFlickLanding()
        {
            yield return Start();
            SimLayoutSource.DebugSkipYears(30f);
            yield return null;
            Sim.CleanAll();
            Sim.DebugRefillShaping();
            Assume.That(Sim.State.era >= Era.Village, "the box reached the village");
            Assert.IsTrue(Sv.ArmTool(K.Channel));
            var (x, z) = Spot(1.3f, 0.6f, false);
            var from = Sv.ScreenOf(x, z) + new Vector2(-120, -60);
            var to = Sv.ScreenOf(x, z);
            yield return Drag(new[] { from, to }, 25f);
            yield return Frames(4);
            var n = Sim.State.nudges.Find(q => q.kind == K.Channel);
            Assert.IsNotNull(n, Sv.LastResult);
            Assert.IsTrue(Sv.GroundPoint(to, out float gx, out float gz));
            Assert.AreEqual(gx, n.x, 0.6f, "the drag's end is the point");
            Assert.AreEqual(gz, n.z, 0.6f);

            Assert.IsTrue(Sv.ArmTool(K.Herd));
            var (hx, hz) = Spot(-1.3f, 0.5f);
            var land = Sv.ScreenOf(hx, hz);
            yield return Drag(new[] { land + new Vector2(0, -300), land }, 120f);
            yield return Frames(4);
            Assert.IsNotNull(Sim.State.nudges.Find(q => q.kind == K.Herd), Sv.LastResult);
            AssertNoErrors("gestures");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator AWishesGiveMovedFirstPlacesAtTheMovedSpot()
        {
            yield return Start();
            var w = Sim.DebugForceWish(K.Grove);
            Assert.IsNotNull(w);
            Assert.IsTrue(Sv.ArmWish(w.id));
            yield return Frames(4);
            Assert.IsTrue(Shown(Ui.Q("shaping-wish-spot")), "the Here pin");
            var (x, z) = Spot(-1.3f, 0.6f);
            if (Mathf.Abs(x - w.x) < 0.5f && Mathf.Abs(z - w.z) < 0.5f) (x, z) = Spot(1.3f, -0.6f);
            var from = Sv.ScreenOf(w.x, w.z);
            var to = Sv.ScreenOf(x, z);
            yield return Drag(new[] { from, to }, 25f);
            yield return Frames(4);
            Assert.AreEqual(x, Sv.Tray.PendingX, 0.5f, "the spot followed the finger");
            Assert.AreEqual(WishState.Open, w.state, "moving spends nothing");
            yield return Shot("84_wish_moved");
            yield return TapAt(to);
            yield return Frames(6);
            var n = Sim.State.nudges.Find(q => q.kind == K.Grove);
            Assert.IsNotNull(n, Sv.LastResult);
            Assert.AreEqual(x, n.x, 0.6f, "placed at the moved spot");
            Assert.AreEqual(w.notableId, n.creditNotableId, "credited to the wisher");
            AssertNoErrors("wish");
        }
    }
}
