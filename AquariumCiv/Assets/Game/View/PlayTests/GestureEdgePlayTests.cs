using System.Collections;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;


namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Tester run 5's two [real] findings. A double tap on a sheet's closing control: the second tap must stop there, not reach what the
    /// sheet covered, while a new tap elsewhere 0.03 s later still works (so <see cref="GlassMotion.Instant"/> is off here: the
    /// repeat only exists while something is on its way out). And the ease home after a moment: a held finger can't kill it.
    /// </summary>
    [Category("GestureEdges")]
    public class GestureEdgePlayTests : PlayTestBase
    {
        static readonly float[] Gaps = { 0.03f, 0.12f };
        static float Now => Time.realtimeSinceStartup;
        static GameHud Hud => GameHud.Instance;

        public override void TearDown()
        {
            GlassMotion.Instant = true;
            MomentPlayer.DebugHoldHurry = false;
            base.TearDown();
        }

        IEnumerator OpenWithMotion(int seed = 7531, bool skipFirstRun = true)
        {
            yield return OpenSlice(seed: seed, skipFirstRun: skipFirstRun);
            GlassMotion.Instant = false;
        }

        static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        /// <summary>Two real touches at one spot, <paramref name="gap"/> seconds from the first's lift to the second's press.</summary>
        IEnumerator DoubleTap(Vector2 p, float gap)
        {
            int id = touchId++;
            BeginTouch(id, p, queueEventOnly: true);
            yield return Frames(2);
            EndTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null;
            float lifted = Now;
            while (Now < lifted + gap) yield return null;
            id = touchId++;
            BeginTouch(id, p, queueEventOnly: true);
            yield return Frames(2);
            EndTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(2);
        }

        static string Open()
        {
            var tl = TimeLapseScreenView.Instance;
            string s = Hud.OpenSheet ?? (tl != null && tl.IsOpen ? "time-lapse" : null);
            return s;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheSecondTapOnSettingsCloseDoesNotKnockTheGlass()
        {
            yield return OpenWithMotion();
            foreach (float gap in Gaps)
            {
                yield return TapAt(GameUiPanel.ScreenCenter(Hud.Root, "hud-settings"));
                yield return Until(() => Hud.OpenSheet == GameHud.Settings, 1f);
                yield return Wait(0.45f);
                int knocks = GlassVoice.Instance.Knocks;
                yield return DoubleTap(GameUiPanel.ScreenCenter(Hud.Root, "settings-close"), gap);
                yield return Wait(GlassVoice.TapGap + 0.3f);
                Assert.IsNull(Open(), "the sheet closed and nothing opened, gap " + gap);
                Assert.AreEqual(knocks, GlassVoice.Instance.Knocks, "no knock, gap " + gap);
            }
            AssertNoErrors("settings close");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheSecondTapOnKeepPlayingDoesNotOpenTheTray()
        {
            yield return OpenWithMotion();
            foreach (float gap in Gaps)
            {
                Hud.Open(SettingsSheets.Reset);
                yield return Wait(0.45f);
                yield return DoubleTap(GameUiPanel.ScreenCenter(Hud.Root, "reset-keep"), gap);
                yield return Wait(0.5f);
                Assert.IsNull(Open(), "gap " + gap);
            }
            AssertNoErrors("keep playing");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheSecondTapOnFeedbackCancelStopsThere()
        {
            yield return OpenWithMotion();
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            var fb = FeedbackUI.Instance;
            foreach (float gap in Gaps)
            {
                yield return TapAt(GameUiPanel.ScreenCenter(Hud.Root, "hud-feedback"));
                yield return Until(() => fb.IsOpen, 3f);
                Assert.IsTrue(fb.IsOpen);
                yield return Wait(0.5f);
                yield return DoubleTap(GameUiPanel.ScreenCenter(fb.Root, "fb-cancel"), gap);
                yield return Wait(0.5f);
                Assert.IsFalse(fb.IsOpen, "gap " + gap);
                Assert.IsNull(Open(), "gap " + gap);
            }
            AssertNoErrors("feedback cancel");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheSecondTapOnTheTimeLapseCloseStopsThere()
        {
            yield return OpenWithMotion();
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            var screen = TimeLapseScreenView.Ensure();
            foreach (float gap in Gaps)
            {
                Assert.IsTrue(screen.Open(), "a past to show");
                yield return Wait(0.6f);
                yield return DoubleTap(GameUiPanel.ScreenCenter(screen.Root, "tl-close"), gap);
                yield return Wait(0.5f);
                Assert.IsNull(Open(), "gap " + gap);
            }
            AssertNoErrors("time-lapse close");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheSecondTapOnTheLastNextStopsThere()
        {
            yield return OpenWithMotion();
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            GameClock.Instance.ActiveSim.CleanAll();
            var view = CardStackView.Instance;
            foreach (float gap in Gaps)
            {
                GameClock.DebugAway(24);
                yield return Frames(6);
                Assert.AreEqual(CardStackMode.Peek, view.Model.Mode, "a return shows the peek");
                yield return Wait(0.5f);
                yield return TapAt(GameUiPanel.ScreenCenter(view.Root, "cards-peek"));
                yield return Wait(0.5f);
                for (int guard = 0; guard < 8 && view.Model.Mode == CardStackMode.Open && !view.Model.OnLastPage; guard++)
                {
                    yield return TapAt(GameUiPanel.ScreenCenter(view.Root, "cards-next"));
                    yield return Wait(0.2f);
                }
                yield return DoubleTap(GameUiPanel.ScreenCenter(view.Root, "cards-next"), gap);
                yield return Wait(0.5f);
                Assert.AreNotEqual(CardStackMode.Open, view.Model.Mode, "the last Next closed the stack");
                Assert.IsNull(Open(), "gap " + gap);
            }
            AssertNoErrors("last next");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheSecondTapOnTheFirstRunDoneStopsThere()
        {
            yield return OpenWithMotion(skipFirstRun: false);
            var first = FirstRunView.Instance;
            first.Run.DebugSkipTo(FirstRunStep.Card);
            yield return Until(() => first.CardOpen, 6f);
            Assert.IsTrue(first.CardOpen, "the card");
            yield return Wait(0.6f);
            yield return DoubleTap(GameUiPanel.ScreenCenter(first.Root, "firstrun-card-done"), 0.03f);
            yield return Wait(0.5f);
            Assert.IsNull(Open(), "the tray stays shut");
            AssertNoErrors("first run done");
        }

        /// <summary>Tester run 6: book-close sits over hud-shape, and the book hides at once.</summary>
        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator TheSecondTapOnTheBookCloseStopsThere()
        {
            yield return OpenWithMotion();
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            var book = BookView.Instance;
            foreach (float gap in Gaps)
            {
                Assert.IsTrue(book.Open(), "the book opens");
                yield return Wait(0.5f);
                yield return DoubleTap(GameUiPanel.ScreenCenter(book.Root, "book-close"), gap);
                yield return Wait(0.5f);
                Assert.IsFalse(book.IsOpen, "gap " + gap);
                Assert.IsNull(Open(), "the tray stays shut, gap " + gap);
            }
            AssertNoErrors("book close");
        }

        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator TheSecondTapOnThePersonCardCloseStopsThere()
        {
            yield return OpenWithMotion();
            SimLayoutSource.DebugSkipYears(8f);
            yield return null;
            var card = PersonCardView.Instance;
            int id = GameClock.Instance.ActiveSim.State.notables.Find(n => n.alive && n.age >= 16f).id;
            foreach (float gap in Gaps)
            {
                Assert.IsTrue(card.Open(id, fly: false), "the card opens");
                yield return Wait(0.5f);
                int knocks = GlassVoice.Instance.Knocks;
                yield return DoubleTap(GameUiPanel.ScreenCenter(Hud.Root.panel.visualTree, "person-card-close"), gap);
                yield return Wait(GlassVoice.TapGap + 0.3f);
                Assert.IsFalse(card.AnyOpen, "no card came back, gap " + gap);
                Assert.IsNull(Open(), "gap " + gap);
                Assert.AreEqual(knocks, GlassVoice.Instance.Knocks, "no knock, gap " + gap);
            }
            AssertNoErrors("person card close");
        }

        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator TheSecondTapOnTheTrayCloseStopsThere()
        {
            yield return OpenWithMotion();
            foreach (float gap in Gaps)
            {
                Hud.Open(GameHud.Shaping);
                yield return Wait(0.45f);
                int knocks = GlassVoice.Instance.Knocks;
                yield return DoubleTap(GameUiPanel.ScreenCenter(Hud.Root, GameHud.Shaping + "-close"), gap);
                yield return Wait(GlassVoice.TapGap + 0.3f);
                Assert.IsNull(Open(), "gap " + gap);
                Assert.AreEqual(knocks, GlassVoice.Instance.Knocks, "no knock, gap " + gap);
            }
            AssertNoErrors("tray close");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ANewTapElsewhereRightAfterCloseStillWorks()
        {
            yield return OpenWithMotion();
            foreach (float gap in Gaps)
            {
                yield return TapAt(GameUiPanel.ScreenCenter(Hud.Root, "hud-settings"));
                yield return Until(() => Hud.OpenSheet == GameHud.Settings, 1f);
                yield return Wait(0.45f);
                var close = GameUiPanel.ScreenCenter(Hud.Root, "settings-close");
                var clean = GameUiPanel.ScreenCenter(Hud.Root, "hud-clean");
                int id = touchId++;
                BeginTouch(id, close, queueEventOnly: true);
                yield return Frames(2);
                EndTouch(id, close, delta: Vector2.zero, queueEventOnly: true);
                yield return null;
                float lifted = Now;
                while (Now < lifted + gap) yield return null;
                yield return TapAt(clean);
                yield return Frames(2);
                Assert.IsTrue(Hud.CleanMode, "a tap on Clean counts, gap " + gap);
                Hud.SetCleanMode(false);
                yield return Wait(0.5f);
            }
            AssertNoErrors("new tap");
        }

        /// <summary>The guard is short: the same spot takes a tap again once the window is over.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator TheSameSpotWorksAgainAfterTheWindow()
        {
            yield return OpenWithMotion();
            Hud.Open(SettingsSheets.Reset);
            yield return Wait(0.45f);
            var keep = GameUiPanel.ScreenCenter(Hud.Root, "reset-keep");
            yield return TapAt(keep);
            yield return Wait(GlassTapGuard.Seconds + 0.2f);
            Hud.Open(GameHud.Settings);
            yield return Wait(0.45f);
            yield return TapAt(GameUiPanel.ScreenCenter(Hud.Root, "settings-close"));
            yield return Wait(GlassTapGuard.Seconds + 0.2f);
            Assert.IsEmpty(GlassTapGuard.Live, "no guard outlives its window");
            AssertNoErrors("after the window");
        }

        IEnumerator ToMomentEnd(MomentPlayer mp)
        {
            var sim = GameClock.Instance.ActiveSim;
            Assert.IsTrue(sim.ForceEraReady(), "an era waits");
            yield return null;
            MomentPlayer.DebugHurry(8f);
            MomentPlayer.DebugHoldHurry = true;
            Assert.IsTrue(mp.Watch(true), "the moment starts");
            yield return Until(() => !mp.Playing, 60f);
            MomentPlayer.DebugHoldHurry = false;
            Assert.IsFalse(mp.Playing, "the moment ended");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AFingerHeldThroughTheEndOfAMomentStillGoesHome()
        {
            yield return OpenSlice(seed: 7531);
            var cam = Camera.main.GetComponent<BoxCamera>();
            var mp = MomentPlayer.Instance;
            float zoom = cam.zoom; var target = cam.target;
            var world = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            int id = touchId++;
            BeginTouch(id, world, queueEventOnly: true);
            yield return Frames(2);
            yield return ToMomentEnd(mp);
            yield return Wait(2f); // 1.4 s of ease, the finger still down
            EndTouch(id, world, delta: Vector2.zero, queueEventOnly: true);
            yield return Frames(3);
            Assert.AreEqual(zoom, cam.zoom, 0.02f, "zoom came home");
            Assert.Less((cam.target - target).magnitude, 0.03f, "target came home");
            Assert.IsFalse(cam.OffHome);
            AssertNoErrors("held through the end");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ATapDuringTheEaseHomeDoesNotStopIt()
        {
            yield return OpenSlice(seed: 7531);
            var cam = Camera.main.GetComponent<BoxCamera>();
            var mp = MomentPlayer.Instance;
            float zoom = cam.zoom; var target = cam.target;
            yield return ToMomentEnd(mp);
            yield return Wait(0.4f);
            Assert.IsTrue(cam.IsFlying, "the ease home is under way");
            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return Wait(1.6f);
            Assert.AreEqual(zoom, cam.zoom, 0.02f, "zoom came home");
            Assert.Less((cam.target - target).magnitude, 0.03f, "target came home");
            AssertNoErrors("tap in the ease");
        }

        /// <summary>A drag after the ease is an ordinary orbit: the firm flight doesn't keep the camera.</summary>
        [Test]
        public void AnOrdinaryFlightStillGivesWayToATouch()
        {
            var go = new GameObject("cam", typeof(Camera));
            var cam = go.AddComponent<BoxCamera>();
            cam.FlyTo(Vector3.zero, 0.5f, 1f);
            Assert.IsFalse(cam.TickFlight(0.1f, touches: 1), "a touch stops a plain flight");
            cam.EaseTo(Vector3.zero, 0f, 40f, 1f, 1f, firm: true);
            Assert.IsTrue(cam.TickFlight(0.1f, touches: 1), "a finger can't stop a firm one");
            Assert.IsTrue(cam.TickFlight(0.1f, scroll: 3f));
            Assert.IsFalse(cam.TickFlight(2f), "and it arrives");
            Object.DestroyImmediate(go);
        }
        /// <summary>#376 (playtest #337): zooming with two fingers on the glass knocked it. Two fingers landing together, one lifting
        /// and then the other, is a pinch, not a tap; a single tap right after still knocks.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator APinchOnTheGlassIsNeverAKnock()
        {
            yield return OpenSlice(seed: 7531, skipFirstRun: true);
            var glass = GlassVoice.Instance;
            var a = new Vector2(-1f, -1f);
            for (float fy = 0.3f; fy <= 0.7f && a.x < 0f; fy += 0.03f)
                for (float fx = 0.2f; fx <= 0.7f; fx += 0.05f)
                {
                    var p = new Vector2(Screen.width * fx, Screen.height * fy);
                    var q = p + new Vector2(Screen.width * 0.1f, 0f);
                    if (CareView.Instance.GlassAt(p) && CareView.Instance.GlassAt(q) && PersonCardView.Instance.Pick(p) <= 0 && PersonCardView.Instance.Pick(q) <= 0) { a = p; break; }
                }
            Assume.That(a.x, Is.GreaterThanOrEqualTo(0f), "some glass has no doll behind it");
            var b = a + new Vector2(Screen.width * 0.1f, 0f);
            int knocks = glass.Knocks;
            int id1 = 90, id2 = 91;
            BeginTouch(id1, a, queueEventOnly: true);
            BeginTouch(id2, b, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(id1, a, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(id2, b, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            yield return Wait(GlassVoice.TapGap + 0.3f);
            Assert.AreEqual(knocks, glass.Knocks, "a pinch is not a knock");
            // One finger down, a second joins, both lift: still nothing.
            BeginTouch(id1 + 2, a, queueEventOnly: true);
            yield return null; yield return null;
            BeginTouch(id2 + 2, b, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(id2 + 2, b, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(id1 + 2, a, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            yield return Wait(GlassVoice.TapGap + 0.3f);
            Assert.AreEqual(knocks, glass.Knocks, "a second finger joining spoils the tap");
            // And a fresh single tap still knocks, at once.
            yield return TapAt(a);
            yield return Until(() => glass.Knocks > knocks, 3f);
            Assert.AreEqual(knocks + 1, glass.Knocks, "a single tap still knocks");
            AssertNoErrors("pinch knock");
        }
    }
}
