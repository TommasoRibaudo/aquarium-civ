using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The whole slice in one go, with every bootstrap, by touch where a player would use touch: a fresh box, the first run
    /// (wipe, weeds, name, rain, the year, the card), a tool placed, a person card, Clean, an era moment, the time-lapse
    /// open and shut, the box complete. Any console error fails it. It's the check on every Editor ticket: quick, shallow,
    /// and the one place all the views run together. Target under 4 minutes.
    /// </summary>
    [Category("SmokePlay")]
    public class SlicePlaythroughSmoke : PlayTestBase
    {
        const int Seed = 20261030;
        float t0;

        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView Run => FirstRunView.Instance;

        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        void Step(string name)
        {
            AssertNoErrors(name);
            Debug.Log($"[smoke] {name} done at {Time.realtimeSinceStartup - t0:0} s");
        }

        static List<Vector2[]> RowStrokes(CareView care)
        {
            var list = new List<Vector2[]>();
            float spacing = care.wipeRadius * Screen.height * 1.2f;
            bool right = true;
            for (float y = Screen.height * 0.9f; y > Screen.height * 0.12f; y -= spacing)
            {
                float first = -1f, last = -1f;
                for (float x = 8f; x < Screen.width - 8f; x += 12f)
                {
                    if (!care.GlassAt(new Vector2(x, y))) continue;
                    if (first < 0f) first = x;
                    last = x;
                }
                if (first < 0f) continue;
                list.Add(right ? new[] { new Vector2(first, y), new Vector2(last, y) } : new[] { new Vector2(last, y), new Vector2(first, y) });
                right = !right;
            }
            return list;
        }

        IEnumerator FirstRunByTouch()
        {
            Assert.IsTrue(Run.Active, "a fresh box starts the first run");
            var care = CareView.Instance;
            yield return Until(() => Run.Step == FirstRunStep.Wipe, 6f);
            var rows = RowStrokes(care);
            for (int round = 0; round < 5 && Run.Step == FirstRunStep.Wipe; round++)
                foreach (var row in rows)
                {
                    if (Run.Step != FirstRunStep.Wipe) break;
                    yield return Drag(row, 0.03f * Screen.height);
                }
            yield return Frames(20);
            Assert.AreEqual(FirstRunStep.Patch, Run.Step, "the wiped glass moves on to the patch");
            for (int guard = 0; Run.Step == FirstRunStep.Patch && guard < 14; guard++)
            {
                var piece = care.Props.AllPieces.FirstOrDefault(p => !p.leaving && p.weed);
                if (piece == null) { yield return null; continue; }
                var sp = (Vector2)Camera.main.WorldToScreenPoint(piece.Center);
                yield return Drag(new[] { sp, sp + new Vector2(0.01f, 0.08f) * Screen.height }, 0.045f * Screen.height);
                yield return Frames(4);
            }
            yield return Frames(20);
            Assert.AreEqual(FirstRunStep.Name, Run.Step, "the cleared patch moves on to the name");

            yield return new WaitForSecondsRealtime(1.5f);
            yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-plate"));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(Run.NameSheetOpen, "a tap on the plate opens the naming sheet");
            Run.Root.Q<TextField>("firstrun-name-field").value = "Smokehollow";
            yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-name-keep"));
            yield return Frames(10);
            Assert.AreEqual("Smokehollow", Sim.PlaceName);
            Assert.AreEqual(FirstRunStep.Rain, Run.Step);

            yield return new WaitForSecondsRealtime(2f);
            yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-rain"));
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(ShapingView.Instance.ShapeMode, "the rain button picks the rain");
            var (rx, rz) = Run.Run.SuggestedRainSpot;
            yield return TapAt(ShapingView.Instance.ScreenOf(rx, rz));
            yield return Frames(10);
            Assert.AreEqual(FirstRunStep.Year, Run.Step, "the rain placed by touch moves on to the year");

            yield return Until(() => Run.Step == FirstRunStep.Card, 35f);
            Assert.AreEqual(FirstRunStep.Card, Run.Step, "the year ends on the card");
            yield return Until(() => Run.CardOpen, 3f);
            Assert.IsTrue(Run.CardOpen);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-card-done"));
            yield return Frames(20);
            Assert.IsFalse(Run.Active, "Done ends the run");
            Assert.IsFalse(GameClock.Instance.Held);
        }

        IEnumerator ToolByTouch()
        {
            var sv = ShapingView.Instance;
            Sim.DebugRefillShaping();
            // The real tray: the HUD's Shape button, the rain's button in the sheet, then a tap on the ground (spends a charge).
            yield return TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root.Q("hud-shape")));
            yield return Frames(4);
            Assert.AreEqual(GameHud.Shaping, GameHud.Instance.OpenSheet, "the Shape button opens the tray");
            float charges = Sim.ShapingCharges;
            yield return TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root.Q("shaping-tool-" + SimCore.NudgeKind.Rain)));
            yield return Frames(4);
            Assert.AreEqual(SimCore.NudgeKind.Rain, sv.Tool, "the tray's rain is in hand");
            yield return new WaitForSecondsRealtime(0.5f);
            int before = Sim.State.nudges.Count;
            // Open ground somewhere near the middle of the view: the first spot the box takes.
            var spots = new List<(float x, float z)>();
            for (float x = -1.6f; x <= 1.61f; x += 0.4f)
                for (float z = -1.2f; z <= 1.21f; z += 0.4f) spots.Add((x, z));
            foreach (var (x, z) in spots.OrderBy(s => s.x * s.x + s.z * s.z))
            {
                float ax = x, az = z;
                if (!Sim.Settlement.OpenSpotNear(ref ax, ref az, 0.3f)) continue;
                yield return TapAt(sv.ScreenOf(ax, az));
                yield return Frames(4);
                if (Sim.State.nudges.Count > before) break;
            }
            Assert.Greater(Sim.State.nudges.Count, before, "a tap on the ground places the tool: " + sv.LastResult);
            Assert.Less(Sim.ShapingCharges, charges, "the tray's tool spent a charge");
            sv.SetTool(null);
            yield return Frames(4);
        }

        static IEnumerator Grow(int years)
        {
            for (int i = 0; i < years / 5; i++) { SimLayoutSource.DebugSkipYears(5f); yield return null; }
            Sim.CleanAll();
            yield return Frames(2);
        }

        /// <summary>A notable's doll under the finger, its card open by a tap, closed by its button.</summary>
        IEnumerator PersonCardByTouch()
        {
            yield return Grow(20);
            PeopleView.DebugSetHour(11f);
            yield return Frames(3);
            var pv = PeopleView.Find();
            var cards = PersonCardView.Instance;
            var cam = Camera.main.GetComponent<BoxCamera>();
            var shown = new List<int>();
            pv.NotablesShown(shown);
            Assert.IsNotEmpty(shown, "the box has notables on show");
            pv.timeScale = 0.05f; // the dolls barely move while we aim
            bool opened = false;
            foreach (int id in shown.Take(8))
            {
                if (!pv.TryGetDollTop(PersonRef.Notable(id), out _)) continue; // indoors
                cam.FlyToPerson(PersonRef.Notable(id), 0.6f, 0.3f);
                yield return new WaitForSecondsRealtime(0.6f);
                if (!pv.TryGetDollTop(PersonRef.Notable(id), out var top) || !pv.TryGetDollFeet(PersonRef.Notable(id), out var feet)) continue;
                var s = Camera.main.WorldToScreenPoint((top + feet) * 0.5f);
                if (cards.Pick(s) <= 0) continue;
                yield return TapAt(s);
                yield return Until(() => cards.IsOpen, 2f);
                if (opened = cards.IsOpen) break;
            }
            pv.timeScale = 1f;
            Assert.IsTrue(opened, "a tap on a doll opens their card");
            yield return Shot("smoke_card");
            yield return TapAt(GameUiPanel.ScreenCenter(cards.CardRoot.Q("person-card-close")));
            yield return Frames(6);
            Assert.IsFalse(cards.IsOpen, "Close closes it");
            cam.FlyTo(cam.transform.parent != null ? cam.transform.parent.position : Vector3.up, 1f, 0.3f);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        IEnumerator CleanByTouch()
        {
            var hud = GameHud.Instance;
            var care = CareView.Instance;
            var sim = Sim;
            sim.AddMess(0.6f, 0.3f, 2);
            yield return Frames(20);
            float grime = sim.State.grime;
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            Assert.IsTrue(hud.CleanMode, "the Clean button switches clean mode on");
            yield return Frames(10);
            foreach (var row in RowStrokes(care)) yield return Drag(row, 0.03f * Screen.height);
            yield return Frames(4);
            Assert.Less(sim.State.grime, grime, "a wipe takes the grime off");
            var piece = care.Props.AllPieces.FirstOrDefault(p => !p.leaving);
            int left = care.Props.AllPieces.Count(p => !p.leaving);
            if (piece != null)
            {
                var sp = (Vector2)Camera.main.WorldToScreenPoint(piece.Center);
                yield return Drag(new[] { sp, sp + new Vector2(0.01f, 0.08f) * Screen.height }, 0.045f * Screen.height);
                yield return Frames(6);
                Assert.Less(care.Props.AllPieces.Count(p => !p.leaving), left, "a flick throws a heap out");
            }
            if (hud.CleanMode) yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
            yield return Frames(6);
            Assert.IsFalse(hud.CleanMode);
            sim.CleanAll();
            yield return Frames(4);
        }

        /// <summary>A return: the peek, the stack by touch, Skip all.</summary>
        IEnumerator ReturnCards()
        {
            var stack = CardStackView.Instance;
            Assert.IsNotNull(stack, "the card stack came up with the bootstraps");
            GameClock.DebugAway(8);
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Peek, stack.Model.Mode, "a return shows the peek");
            yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-peek"));
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Open, stack.Model.Mode, "a tap opens the stack");
            yield return Shot("smoke_cards");
            yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-next"));
            yield return Frames(4);
            yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-skip"));
            yield return Frames(6);
            Assert.AreEqual(CardStackMode.Hidden, stack.Model.Mode, "Skip all closes it");
            Assert.IsFalse(GameHud.Instance.ToolsHidden);
            yield return Frames(4);
        }

        IEnumerator EraMoment()
        {
            var mp = MomentPlayer.Instance;
            var sim = Sim;
            var era = sim.State.era;
            sim.State.grime = 0f;
            Assert.IsTrue(sim.ForceEraReady(), "the next era is ready");
            MomentPlayer.DebugWaited(100);
            yield return new WaitForSecondsRealtime(0.7f);
            var watch = mp.GetComponent<UIDocument>().rootVisualElement.Q<Button>("moment-watch");
            Assert.IsTrue(Shown(watch), "Watch shows");
            yield return TapAt(GameUiPanel.ScreenCenter(watch));
            Assert.IsTrue(mp.Playing, "a tap on Watch starts the moment");
            MomentPlayer.DebugHurry(6f);
            MomentPlayer.DebugHoldHurry = true;
            yield return Until(() => !mp.Playing, 60f);
            MomentPlayer.DebugHoldHurry = false;
            Assert.IsFalse(mp.Playing, "the moment ends");
            Assert.AreNotEqual(era, Sim.State.era, "the era moved");
            var ask = mp.GetComponent<UIDocument>().rootVisualElement.Q("moment-notify");
            if (ask != null && Shown(ask)) yield return TapAt(GameUiPanel.ScreenCenter(ask.Q("moment-notify-no")));
            yield return Frames(6);
            yield return Shot("smoke_after_moment");
        }

        /// <summary>History through the real button (no cards waiting): the time-lapse screen, a drag by touch, Close.</summary>
        IEnumerator TimeLapse()
        {
            var tl = TimeLapseView.Ensure();
            var hud = GameHud.Instance;
            // History opens the waiting cards first (the dot stays after "Skip all"); read everything so it is the time-lapse's turn
            Sim.CardsSeenToNow();
            yield return Frames(40);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
            yield return new WaitForSecondsRealtime(0.6f);
            var stack = CardStackView.Instance;
            string branch = "direct";
            if (TimeLapseScreenView.Instance == null || !TimeLapseScreenView.Instance.IsOpen)
            {   branch = "cards";
                // the era's cards wait: History shows them first; Skip all, then History is the time-lapse
                Assert.AreNotEqual(CardStackMode.Hidden, stack.Model.Mode, "History showed the waiting cards");
                if (stack.Model.Mode == CardStackMode.Peek) { yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-peek")); yield return Frames(6); }
                yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-skip"));
                yield return Frames(6);
                Assert.AreEqual(CardStackMode.Hidden, stack.Model.Mode);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
                yield return new WaitForSecondsRealtime(0.6f);
            }
            var screen = TimeLapseScreenView.Instance;
            Assert.IsNotNull(screen, $"the History button made the time-lapse screen (cards {stack.Model.Mode}, dot {stack.Model.HistoryDot}, sheet {GameHud.Instance.OpenSheet}, moment {MomentPlayer.Instance.Playing}, branch {branch})");
            Assert.IsTrue(screen.IsOpen, "the History button opens the time-lapse when no cards wait");
            Assert.IsTrue(tl.Active);
            Assert.IsTrue(GameClock.Instance.Held, "the clock holds while replaying");
            var m = screen.Model;
            yield return Until(() => m.Layout.Width > 0f, 2f);
            yield return Drag(new[] { screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.End) - 2f), screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.End * 0.5f)) }, 20f);
            Assert.Less(m.Position, m.Clock.End - 0.5f, "a drag by touch moves the playhead");
            yield return Shot("smoke_timelapse");
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root, "tl-close"));
            yield return Frames(6);
            Assert.IsFalse(screen.IsOpen);
            Assert.IsFalse(tl.Active, "the replay closes");
            Assert.IsFalse(GameClock.Instance.Held, "and the clock runs again");
        }

        IEnumerator BoxComplete()
        {
            yield return Grow(30);
            var sim = Sim;
            if (sim.State.era != Era.Town) { sim.ForceNextEra(); yield return Frames(3); }
            Assert.AreEqual(Era.Town, sim.State.era);
            sim.ForcePeakReady();
            yield return new WaitForSecondsRealtime(0.8f);
            var c = BoxCompleteController.Instance;
            var prompt = c.GetComponent<UIDocument>().rootVisualElement.Q("complete-prompt");
            Assert.IsTrue(Shown(prompt), "the Finish prompt shows at the peak");
            yield return TapAt(GameUiPanel.ScreenCenter(prompt));
            Assert.IsTrue(c.Playing, "a tap on Finish starts the last moment");
            c.Director.HurryFactor = 4f;
            MomentPlayer.DebugHoldHurry = true;
            yield return Until(() => !c.Playing, 100f);
            MomentPlayer.DebugHoldHurry = false;
            yield return Frames(20);
            Assert.IsFalse(c.Playing, "the last moment ends");
            Assert.IsTrue(c.Committed, "the box completed");
            Assert.AreEqual(2, GameClock.Instance.Shelf.boxes.Count, "the old box is on the shelf");
            Assert.AreEqual(1, GameClock.Instance.Shelf.activeIndex, "box 2 is on screen");
            yield return Shot("smoke_box2");
        }

        [UnityTest, Timeout(420000)]
        public IEnumerator FreshBoxToBoxComplete_NoConsoleErrors()
        {
            yield return OpenSlice(seed: Seed, skipFirstRun: false);
            t0 = Time.realtimeSinceStartup;
            Assert.GreaterOrEqual(bootstrapsRun, 12, "every bootstrap ran");
            AudioDirector.Ensure();
            yield return FirstRunByTouch(); Step("first run");
            yield return ToolByTouch(); Step("tool");
            yield return PersonCardByTouch(); Step("person card");
            yield return CleanByTouch(); Step("clean");
            yield return ReturnCards(); Step("return cards");
            yield return EraMoment(); Step("era moment");
            yield return TimeLapse(); Step("time-lapse");
            yield return BoxComplete(); Step("box complete");
            float total = Time.realtimeSinceStartup - t0;
            Debug.Log($"[smoke] whole slice in {total:0} s");
            Assert.Less(total, 240f, "the smoke test stays under 4 minutes");
            Assert.AreEqual(1, LeakCheck.GeneratedRoots(), "exactly one Generated root");
        }
    }
}
