using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.View.Shaping.Tests.ShapingLookTests;
using K = AquariumCiv.Sim.SimCore.NudgeKind;

namespace AquariumCiv.View.Shaping.Tests
{
    /// <summary>#84 phase 1: the shaping tray's model: tools per era with names and levels, charges and refill, why a tool is
    /// off, shaping mode (arm, Cancel, one finger = tool, two = camera), placed feedback, the Take back list, and the wish hook.</summary>
    public class ShapingTrayTests
    {
        static TrayTool Tool(ShapingTray t, SimCore sim, string kind) => t.Tools(sim).Single(x => x.kind == kind);

        [Test]
        public void ToolsShowPerEraWithNamesAndLevels()
        {
            var tray = new ShapingTray();
            var camp = NewSim(4);
            Assert.AreEqual(new[] { K.Rain, K.Grove, K.Herd, K.Channel }, tray.Tools(camp).Select(t => t.kind).ToArray(), "tray order");
            Assert.AreEqual("shower", Tool(tray, camp, K.Rain).name);
            Assert.AreEqual(1, Tool(tray, camp, K.Rain).level);
            var ch = Tool(tray, camp, K.Channel);
            Assert.IsFalse(ch.enabled);
            Assert.AreEqual(SimCore.ShapeFail.Locked, ch.fail);
            Assert.AreEqual("Comes with the village", ch.reason);

            var village = SimIn(Era.Village, 7);
            Assert.AreEqual("rain", Tool(tray, village, K.Rain).name);
            Assert.AreEqual("goats", Tool(tray, village, K.Herd).name);
            Assert.AreEqual(2, Tool(tray, village, K.Rain).level);
            Assert.IsTrue(Tool(tray, village, K.Channel).enabled, "the channel arrives with the village");

            var town = SimIn(Era.Town, 7);
            Assert.AreEqual("storm", Tool(tray, town, K.Rain).name);
            Assert.AreEqual("mill race", Tool(tray, town, K.Channel).name);
            Assert.AreEqual(3, Tool(tray, town, K.Channel).level);
        }

        [Test]
        public void UnlockEraAgreesWithTheSim()
        {
            foreach (Era era in new[] { Era.Camp, Era.Village, Era.Town })
            {
                var sim = era == Era.Camp ? NewSim(2) : SimIn(era, 7);
                foreach (var k in SimCore.SliceTools)
                    Assert.AreEqual(sim.State.era >= ShapingTray.UnlockEra(k), sim.ShapingUnlocked(k), $"{k} in {era}");
            }
        }

        [Test]
        public void ChargesShowWholeAndRefillProgress()
        {
            var tray = new ShapingTray();
            var sim = NewSim(4);
            sim.State.shapingCharges = 1.4f;
            var c = tray.Charges(sim);
            Assert.AreEqual(1, c.whole);
            Assert.AreEqual((int)sim.ShapingMaxCharges, c.max);
            Assert.AreEqual(0.4f, c.refill, 1e-3f);
            Assert.IsFalse(c.full);
            Assert.AreEqual($"1 of {c.max}", c.text);
            sim.DebugRefillShaping();
            c = tray.Charges(sim);
            Assert.IsTrue(c.full);
            Assert.AreEqual(0f, c.refill);
            Assert.AreEqual(c.max, c.whole);
        }

        [Test]
        public void ADisabledToolSaysWhyAndIsNotArmed()
        {
            var tray = new ShapingTray();
            var sim = NewSim(4);
            tray.Open();
            Assert.IsFalse(tray.Select(sim, K.Channel));
            Assert.AreEqual(TrayMode.Open, tray.Mode);
            Assert.AreEqual(TrayFeedbackKind.Failed, tray.Feedback.kind);
            Assert.AreEqual(ShapingShow.FailCue, tray.Feedback.cue);
            Assert.AreEqual("Comes with the village", tray.Feedback.text);

            sim.State.shapingCharges = 0.9f;
            var t = Tool(tray, sim, K.Grove);
            Assert.AreEqual(SimCore.ShapeFail.NoCharge, t.fail);
            StringAssert.Contains("Clean the glass", t.reason);
            int seq = tray.Feedback.seq;
            Assert.IsFalse(tray.Select(sim, K.Grove));
            Assert.Greater(tray.Feedback.seq, seq, "every message is new");

            sim.DebugRefillShaping();
            var show = new ShapingShow();
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            Assert.IsTrue(show.Place(sim, K.Rain, x, z).Ok);
            var rain = Tool(tray, sim, K.Rain);
            Assert.AreEqual(SimCore.ShapeFail.AtCap, rain.fail, "one cloud at a time");
            Assert.AreEqual(1, rain.standing);
            Assert.AreEqual(1, rain.cap);
            Assert.AreEqual("Already out there. Wait for the people", rain.reason);
        }

        [Test]
        public void ArmingAndCancelAreCleanAndCostNothing()
        {
            var tray = new ShapingTray();
            var sim = NewSim(4);
            float charges = sim.ShapingCharges;
            Assert.AreEqual(TrayMode.Closed, tray.Mode);
            tray.Open();
            Assert.AreEqual(TrayMode.Open, tray.Mode);
            Assert.IsTrue(tray.Select(sim, K.Rain));
            Assert.AreEqual(TrayMode.Armed, tray.Mode);
            Assert.AreEqual(K.Rain, tray.Armed);
            Assert.IsTrue(Tool(tray, sim, K.Rain).armed);
            StringAssert.StartsWith("Shower:", tray.Banner(sim));
            StringAssert.Contains("Rub", tray.Banner(sim));
            Assert.AreEqual(ToolGesture.Rub, Tool(tray, sim, K.Rain).gesture);
            Assert.AreEqual(ToolGesture.Tap, ShapingTray.GestureOf(K.Grove));
            Assert.AreEqual(ToolGesture.Drag, ShapingTray.GestureOf(K.Channel));
            Assert.AreEqual(ToolGesture.FlickFromEdge, ShapingTray.GestureOf(K.Herd));

            tray.Cancel();
            Assert.AreEqual(TrayMode.Open, tray.Mode, "back to the tray");
            Assert.AreEqual("", tray.Armed);
            Assert.AreEqual("", tray.Banner(sim));
            Assert.AreEqual(charges, sim.ShapingCharges);
            Assert.IsEmpty(sim.State.nudges);

            tray.Select(sim, K.Grove);
            tray.Close();
            Assert.AreEqual(TrayMode.Closed, tray.Mode);
            Assert.AreEqual("", tray.Armed);
            var show = new ShapingShow();
            Assert.AreEqual(SimCore.ShapeFail.Unknown, tray.PlaceAt(sim, show, 0f, 0f).fail, "nothing in hand: nothing placed");
            Assert.IsEmpty(sim.State.nudges);
        }

        [Test]
        public void OneFingerIsTheToolTwoAreTheCamera()
        {
            var tray = new ShapingTray();
            var sim = NewSim(4);
            Assert.AreEqual(FingerRoute.None, tray.RouteFingers(1), "no tool: the HUD/ground taps are not the tray's");
            Assert.AreEqual(FingerRoute.Camera, tray.RouteFingers(2), "two fingers always move the camera");
            tray.Open();
            tray.Select(sim, K.Rain);
            Assert.AreEqual(FingerRoute.Tool, tray.RouteFingers(1));
            Assert.AreEqual(FingerRoute.Camera, tray.RouteFingers(2));
            Assert.AreEqual(FingerRoute.None, tray.RouteFingers(0));

            Assert.IsTrue(tray.BeginGesture());
            Assert.IsTrue(tray.GestureActive);
            Assert.AreEqual(FingerRoute.Camera, tray.RouteFingers(2), "a second finger lands mid-rub");
            Assert.IsFalse(tray.GestureActive, "the rub is abandoned");
            Assert.IsFalse(tray.EndGesture(), "lifting the fingers places nothing");
            Assert.AreEqual(K.Rain, tray.Armed, "the tool is still in hand");
            Assert.AreEqual(TrayMode.Armed, tray.Mode);

            Assert.IsTrue(tray.BeginGesture());
            Assert.IsTrue(tray.EndGesture(), "a clean one-finger gesture is the tool's");
        }

        [Test]
        public void PlacingSpendsAChargeSaysSoAndLeavesShapingMode()
        {
            var tray = new ShapingTray();
            var sim = NewSim(4);
            var show = new ShapingShow();
            sim.State.shapingCharges = 2f;
            tray.Open();
            tray.Select(sim, K.Rain);
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            var r = tray.PlaceAt(sim, show, x, z);
            Assert.IsTrue(r.Ok, r.fail.ToString());
            Assert.AreEqual(1f, sim.ShapingCharges, 1e-4f);
            Assert.AreEqual(TrayFeedbackKind.Placed, tray.Feedback.kind);
            Assert.AreEqual("Shower placed. 1 charge left", tray.Feedback.text);
            Assert.AreEqual(r.nudge.id, tray.Feedback.nudgeId);
            Assert.AreEqual(Audio.CueLibrary.Rain, tray.Feedback.cue);
            Assert.AreEqual(TrayMode.Closed, tray.Mode);
            Assert.AreEqual("", tray.Armed);
            Assert.IsNotNull(show.Find(r.nudge.id), "the show tracks it at once");
        }

        [Test]
        public void StayArmedKeepsTheToolWhileChargesAndCapAllow()
        {
            var tray = new ShapingTray { StayArmedAfterPlace = true };
            var sim = NewSim(4);
            var show = new ShapingShow();
            sim.State.shapingCharges = 3f;
            tray.Select(sim, K.Grove);
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            Assert.IsTrue(tray.PlaceAt(sim, show, x, z).Ok);
            Assert.AreEqual(TrayMode.Armed, tray.Mode);
            Assert.AreEqual(K.Grove, tray.Armed);
        }

        [Test]
        public void APlacingThatFailsPutsTheToolDownWithTheReason()
        {
            var tray = new ShapingTray();
            var sim = NewSim(4);
            var show = new ShapingShow();
            tray.Select(sim, K.Grove);
            sim.State.shapingCharges = 0f; // spent elsewhere between arming and the finger lifting
            var r = tray.PlaceAt(sim, show, 1f, 1f);
            Assert.AreEqual(SimCore.ShapeFail.NoCharge, r.fail);
            Assert.AreEqual(TrayFeedbackKind.Failed, tray.Feedback.kind);
            Assert.AreEqual(ShapingShow.FailCue, tray.Feedback.cue);
            StringAssert.Contains("Clean the glass", tray.Feedback.text);
            Assert.AreEqual(TrayMode.Open, tray.Mode);
            Assert.AreEqual("", tray.Armed);
            Assert.IsEmpty(sim.State.nudges);
        }

        [Test]
        public void TheTakeBackListHoldsUnusedShapingsAndRefunds()
        {
            var tray = new ShapingTray();
            var sim = NewSim(4);
            var show = new ShapingShow();
            Assert.IsEmpty(tray.TakeBackList(sim));
            sim.DebugRefillShaping();
            float charges = sim.ShapingCharges;
            var (x, z) = OpenLand(sim, 1.2f, 0.6f);
            tray.Select(sim, K.Rain);
            var a = tray.PlaceAt(sim, show, x, z).nudge;
            tray.Select(sim, K.Grove);
            var (x2, z2) = OpenLand(sim, -1.2f, 0.6f);
            var b = tray.PlaceAt(sim, show, x2, z2).nudge;
            var list = tray.TakeBackList(sim);
            Assert.AreEqual(new[] { b.id, a.id }, list.Select(i => i.nudgeId).ToArray(), "newest first");
            Assert.AreEqual("Take back the shower", list[1].label);
            Assert.AreEqual(a.x, list[1].x, 1e-4f);

            Assert.IsTrue(tray.TakeBack(sim, show, a.id));
            Assert.AreEqual(charges - 1f, sim.ShapingCharges, 1e-4f, "the rain's charge is back, the grove's is spent");
            Assert.AreEqual(TrayFeedbackKind.TakenBack, tray.Feedback.kind);
            Assert.AreEqual(1, tray.TakeBackList(sim).Count);

            sim.DebugSkipToAnswer(b.id);
            Assert.IsEmpty(tray.TakeBackList(sim), "the people used it: no take-back");
            Assert.IsFalse(tray.TakeBack(sim, show, b.id));
            Assert.AreEqual(TrayFeedbackKind.Info, tray.Feedback.kind);
            StringAssert.Contains("already used", tray.Feedback.text);
        }

        [Test]
        public void AWishesGivePreArmsTheToolAtTheWishedSpot()
        {
            var tray = new ShapingTray();
            var sim = NewSim(5);
            for (int m = 0; m < 24; m++) sim.Step();
            sim.DebugRefillShaping();
            var show = new ShapingShow();
            var w = sim.DebugForceWish(K.Grove);
            Assert.IsNotNull(w);
            float charges = sim.ShapingCharges;
            Assert.IsTrue(tray.ArmForWish(sim, w.id));
            Assert.AreEqual(TrayMode.Armed, tray.Mode);
            Assert.AreEqual(K.Grove, tray.Armed);
            Assert.IsTrue(tray.HasPending);
            Assert.AreEqual(w.x, tray.PendingX);
            Assert.AreEqual(w.z, tray.PendingZ);
            Assert.AreEqual(w.notableId, tray.PendingNotableId);
            Assert.AreEqual(WishState.Open, w.state, "armed is not given");
            Assert.AreEqual(charges, sim.ShapingCharges);

            tray.Cancel();
            Assert.IsFalse(tray.HasPending);
            Assert.AreEqual(WishState.Open, w.state, "cancelled: the wish stays open, no penalty");

            Assert.IsTrue(tray.ArmForWish(sim, w.id));
            var r = tray.Confirm(sim, show);
            Assert.IsTrue(r.Ok, r.fail.ToString());
            Assert.AreEqual(WishState.Given, w.state);
            Assert.AreEqual(w.notableId, r.nudge.creditNotableId);
            Assert.AreEqual(w.x, r.nudge.x, 1e-3f);
            Assert.AreEqual(charges - 1f, sim.ShapingCharges, 1e-4f);
            Assert.AreEqual(TrayMode.Closed, tray.Mode);
            Assert.IsFalse(tray.HasPending);
            show.Sync(sim, 1f, new Life.ViewTime(sim.State.month, 10f));
            Assert.IsNotNull(show.Find(r.nudge.id), "the show picks it up");
        }

        [Test]
        public void AMovedWishGiftIsCreditedToTheWisherAndMeetsTheWish()
        {
            var tray = new ShapingTray();
            var sim = NewSim(5);
            for (int m = 0; m < 24; m++) sim.Step();
            sim.DebugRefillShaping();
            var show = new ShapingShow();
            var w = sim.DebugForceWish(K.Grove);
            Assert.IsTrue(tray.ArmForWish(sim, w.id));
            var (x, z) = OpenLand(sim, -1.2f, -0.6f);
            tray.MovePending(x, z);
            var r = tray.Confirm(sim, show);
            Assert.IsTrue(r.Ok, r.fail.ToString());
            Assert.AreEqual(w.notableId, r.nudge.creditNotableId);
            sim.Step(); // WishStep: a gift of its tool now stands
            Assert.AreEqual(WishState.Met, w.state);
        }

        [Test]
        public void AWishThatCantBeGivenSaysWhyAndStaysOpen()
        {
            var tray = new ShapingTray();
            var sim = NewSim(5);
            for (int m = 0; m < 24; m++) sim.Step();
            var w = sim.DebugForceWish(K.Grove);
            sim.State.shapingCharges = 0f;
            Assert.IsFalse(tray.ArmForWish(sim, w.id));
            Assert.AreEqual(TrayMode.Closed, tray.Mode);
            StringAssert.Contains("Clean the glass", tray.Feedback.text);
            Assert.AreEqual(WishState.Open, w.state);
            Assert.IsFalse(tray.ArmForWish(sim, 12345), "no such wish");
        }

        [Test]
        public void DescribeNamesTheStateForTheDebugPanel()
        {
            var tray = new ShapingTray();
            var sim = NewSim(4);
            tray.Select(sim, K.Rain);
            var s = tray.Describe(sim);
            StringAssert.Contains("Armed", s);
            StringAssert.Contains("armed=rain", s);
            StringAssert.Contains("channel:Locked", s);
        }
    }
}
