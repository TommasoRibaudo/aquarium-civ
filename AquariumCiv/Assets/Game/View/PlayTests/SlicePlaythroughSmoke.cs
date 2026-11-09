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

        /// <summary>A flick a slow Editor can't slow down. CareView reads the release speed in real seconds (flickSpeed screen heights a
        /// second over the last 0.1 s), and a drag spans its begin, move and release frames, so on an Editor at a few fps (tooling.md
        /// 2026-11-02 / 2026-11-08) the old 0.08 H drag came out under flickSpeed and dropped the patch back. One move, as long as the
        /// frame time needs: three times the speed over three frames.</summary>
        IEnumerator Flick(CareView care, Vector2 sp)
        {
            float t0 = Time.realtimeSinceStartup;
            yield return Frames(4);
            float frame = (Time.realtimeSinceStartup - t0) / 4f;
            float travel = Mathf.Clamp(care.flickSpeed * 3f * 4f * frame, 0.3f, 2f);
            Debug.Log($"[smoke] flick: frame {frame * 1000f:0} ms, travel {travel:0.00} H");
            var to = sp + new Vector2(0.02f, travel) * Screen.height;
            int id = touchId++;
            BeginTouch(id, sp, queueEventOnly: true);
            yield return null;
            MoveTouch(id, to, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null; // the move gets a frame of its own before the lift
            EndTouch(id, to, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
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
                yield return Flick(care, sp);
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
            sim.AddMess(0.6f, 0.3f);
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
                yield return Flick(care, sp);
                yield return Frames(6);
                Assert.Less(care.Props.AllPieces.Count(p => !p.leaving), left, "a flick throws a weed patch out");
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
            yield return Until(() => mp.Director.Time >= 8f, 20f);
            yield return Shot("smoke_moment_close");
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
            // Hierarchy D1: History is always the time-lapse, whatever stories wait
            Sim.CardsSeenToNow();
            yield return Frames(40);
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
            yield return new WaitForSecondsRealtime(0.6f);
            var stack = CardStackView.Instance;
            var screen = TimeLapseScreenView.Instance;
            Assert.IsNotNull(screen, $"the History button made the time-lapse screen (cards {stack.Model.Mode}, dot {stack.Model.HistoryDot}, sheet {GameHud.Instance.OpenSheet}, moment {MomentPlayer.Instance.Playing})");
            Assert.IsTrue(screen.IsOpen, "the History button opens the time-lapse");
            Assert.IsTrue(tl.Active);
            Assert.IsTrue(GameClock.Instance.Held, "the clock holds while replaying");
            var m = screen.Model;
            yield return Until(() => m.Layout.Width > 0f, 2f);
            yield return Drag(new[] { screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.End) - 2f), screen.ScreenPointAtBarX(m.Layout.XOf(m.Clock.End * 0.5f)) }, 20f);
            Assert.Less(m.Position, m.Clock.End - 0.5f, "a drag by touch moves the playhead");
            yield return Shot("smoke_timelapse");
            // A drag can settle on a pin, and the caption's row then has the play row's place, Close with it.
            if (m.Caption != null) { yield return TapAt(GameUiPanel.ScreenCenter(screen.Root, "tl-cap-close")); yield return Frames(3); }
            // The book, by touch: open, a page, close, and the screen is still there.
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root, "tl-book"));
            yield return Frames(6);
            var book = BookView.Instance;
            Assert.IsTrue(book.IsOpen, "the Book button opens the book");
            yield return TapAt(GameUiPanel.ScreenCenter(book.Root, "book-next"));
            yield return Frames(4);
            Assert.AreEqual(1, book.At);
            yield return Shot("smoke_book");
            yield return TapAt(GameUiPanel.ScreenCenter(book.Root, "book-close"));
            yield return Frames(4);
            Assert.IsFalse(book.IsOpen);
            Assert.IsTrue(screen.IsOpen);
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root, "tl-close"));
            yield return Frames(6);
            Assert.IsFalse(screen.IsOpen);
            Assert.IsFalse(tl.Active, "the replay closes");
            Assert.IsFalse(GameClock.Instance.Held, "and the clock runs again");
        }

        IEnumerator TheClose()
        {
            var talk = EveningTalkView.Instance;
            var hud = GameHud.Instance;
            bool hadHud = hud.IsVisible;
            Assert.IsTrue(talk.Begin(), "the evening talk starts");
            yield return Until(() => talk.Step == AquariumCiv.View.Moments.CloseStep.Rest, 10f);
            Assert.That(talk.Line, Is.Not.Empty, "it ends on a line");
            yield return Shot("smoke_close");
            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return Frames(90); // the camera comes home
            Assert.IsFalse(talk.Open, "a touch leaves it");
            Assert.AreEqual(hadHud, hud.IsVisible);
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
            yield return Until(() => c.PickerOpen, 240f); // the moment runs on capped frame time (0.1 s): a slow Editor stretches it
            Assert.IsTrue(c.PickerOpen, "the moment asks for the next box");
            yield return Shot("smoke_last_moment");
            yield return Frames(4);
            yield return TapAt(GameUiPanel.ScreenCenter(c.GetComponent<UIDocument>().rootVisualElement, "picker-begin"));
            yield return Until(() => !c.Playing, 100f);
            MomentPlayer.DebugHoldHurry = false;
            yield return Frames(20);
            Assert.IsFalse(c.Playing, "the last moment ends");
            Assert.IsTrue(c.Committed, "the box completed");
            Assert.AreEqual(2, GameClock.Instance.Shelf.boxes.Count, "the old box is on the shelf");
            Assert.AreEqual(1, GameClock.Instance.Shelf.activeIndex, "box 2 is on screen");
            yield return Shot("smoke_box2");
        }

        IEnumerator ShelfView()
        {
            var c = BoxCompleteController.Instance;
            Assert.IsTrue(c.OpenShelf(), "the shelf opens with a box on it");
            yield return Frames(20);
            yield return Shot("smoke_shelf");
            c.CloseShelf();
            yield return Frames(20);
        }

        /// <summary>P8 (#248): the living land's first run by touch on a barren box (wipe, hold to mist, name; no year), then
        /// cheats to the camp gate, where the tents may go up. Every bootstrap, no console errors, one Generated root.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator BarrenBoxFirstRunToCampGate_NoConsoleErrors()
        {
            bool prefWas = GameClock.EcologyPref;
            GameClock.EcologyPref = true; // GameClock.Awake reads it: the fresh box starts barren
            try
            {
                yield return OpenSlice(seed: Seed, skipFirstRun: false);
                t0 = Time.realtimeSinceStartup;
                Assert.IsTrue(Sim.EcologyOn, "a barren box");
                Assert.IsTrue(Sim.BeforeCamp);
                Assert.IsNotNull(EcoMistView.Instance, "the mist view bootstrapped");
                Assert.IsTrue(Run.Active, "a fresh box starts the first run");
                var care = CareView.Instance;
                yield return Until(() => Run.Step == FirstRunStep.Wipe, 6f);
                yield return Shot("smoke_eco_misted_glass");
                var rows = RowStrokes(care);
                for (int round = 0; round < 5 && Run.Step == FirstRunStep.Wipe; round++)
                    foreach (var row in rows)
                    {
                        if (Run.Step != FirstRunStep.Wipe) break;
                        yield return Drag(row, 0.03f * Screen.height);
                    }
                yield return Frames(20);
                Assert.AreEqual(FirstRunStep.Rain, Run.Step, "no patch on a barren box: the first mist");
                yield return Shot("smoke_eco_barren");
                Step("eco wipe");

                yield return new WaitForSecondsRealtime(1.5f);
                var (rx, rz) = Run.Run.SuggestedRainSpot;
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var w = box.transform.TransformPoint(box.land.OnGround(rx, rz));
                var at = (Vector2)Camera.main.WorldToScreenPoint(w);
                int id = touchId++;
                BeginTouch(id, at, queueEventOnly: true);
                float h0 = Time.unscaledTime;
                while (Time.unscaledTime - h0 < 2f && Run.Step == FirstRunStep.Rain) { MoveTouch(id, at, delta: Vector2.zero, queueEventOnly: true); yield return null; }
                yield return Shot("smoke_eco_first_mist");
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                yield return Frames(20);
                Assert.AreEqual(FirstRunStep.Name, Run.Step, "the held finger misted and the ground greened");
                Assert.That(Sim.State.month, Is.LessThanOrEqualTo(1), "no time skip (the live clock only)");
                Step("eco first mist");

                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("smoke_eco_after_mist");
                Assert.AreEqual("name", Run.Run.Hint.id, "the Name it hint shows after the misting finger lifts (tester P8)");
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsTrue(Run.NameSheetOpen, "a tap on the plate opens the naming sheet");
                Run.Root.Q<TextField>("firstrun-name-field").value = "Dusthollow";
                yield return TapAt(GameUiPanel.ScreenCenter(Run.Root, "firstrun-name-keep"));
                yield return Frames(20);
                Assert.AreEqual("Dusthollow", Sim.PlaceName);
                Assert.IsFalse(Run.Active, "named: the run is done, with no year skip");
                Step("eco name");

                int before = Sim.State.buildings.Count;
                EcoDebug.SkipSeasons(8);
                Assert.AreEqual(before, Sim.State.buildings.Count, "no new tent before the camp gate");
                EcoDebug.OpenCampGate();
                EcoDebug.SkipSeasons(1);
                Assert.AreEqual(before, Sim.State.buildings.Count, "the gate is open but there are no hides: the first tent waits (#284)");
                Assert.IsTrue(Sim.FirstTentWaiting, "and it is waiting for hides");
                EcoDebug.AddHides(SimCore.HidesPerTent); // the hunt's hides (the real path is EcoPhonePlaythrough beat 9)
                EcoDebug.SkipSeasons(1);
                yield return Frames(30);
                Assert.Greater(Sim.State.buildings.Count, before, "the first tent goes up from the hides after the camp gate");
                Assert.IsTrue(Sim.FirstTentRaised, "it is the camp's first tent");
                Assert.GreaterOrEqual(Sim.TentsFromHidesCount, 1, "built from hides");
                EcoDebug.SkipSeasons(46); // no fields before the village: later tents wait for the people to fill the old one
                yield return Shot("smoke_eco_camp");
                Step("eco camp gate");
                Assert.AreEqual(1, LeakCheck.GeneratedRoots(), "exactly one Generated root");
                Debug.Log($"[smoke] barren box run in {Time.realtimeSinceStartup - t0:0} s");
            }
            finally { GameClock.EcologyPref = prefWas; }
        }

        [UnityTest, Timeout(420000)]
        public IEnumerator FreshBoxToBoxComplete_NoConsoleErrors()
        {
            UseLivingLand(false); // #298: the old slice, whatever switch an earlier run left on
            yield return OpenSlice(seed: Seed, skipFirstRun: false);
            Assert.IsFalse(GameClock.Instance.Sim.EcologyOn, "the old slice: the living land is off");
            t0 = Time.realtimeSinceStartup;
            Assert.GreaterOrEqual(bootstrapsRun, 12, "every bootstrap ran");
            AudioDirector.Ensure();
            yield return FirstRunByTouch(); Step("first run");
            yield return ToolByTouch(); Step("tool");
            yield return PersonCardByTouch(); Step("person card");
            yield return CleanByTouch(); Step("clean");
            yield return ReturnCards(); Step("return cards");
            using var depth = new DepthWatch(); // tester run 10: passing moments rendered blank
            yield return EraMoment(); Step("era moment");
            Assert.Greater(depth.Frames, 100, "the watch saw the moment");
            Assert.AreEqual(0, depth.Faults, "the era moment never cuts the box: " + depth.First);
            Assert.AreEqual(0, depth.OffScreen, "and keeps it on screen at every beat");
            yield return TimeLapse(); Step("time-lapse");
            yield return TheClose(); Step("the close");
            yield return BoxComplete(); Step("box complete");
            yield return ShelfView(); Step("shelf");
            Debug.Log($"[smoke] depth watch: {depth.Frames} frames, closest {depth.Worst}");
            Assert.AreEqual(0, depth.Faults, "the time-lapse, the close, the last moment and the shelf never cut the box: " + depth.First);
            float total = Time.realtimeSinceStartup - t0;
            Debug.Log($"[smoke] whole slice in {total:0} s");
            Assert.Less(total, 240f, "the smoke test stays under 4 minutes");
            Assert.AreEqual(1, LeakCheck.GeneratedRoots(), "exactly one Generated root");
        }
    }
}
