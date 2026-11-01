using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#77: the evening talk on the real scene. Each step, nothing competing with the last line, a touch leaves at any step, the
    /// teaser opens a thread only when it's said, the next return's payoff shows it, and an era that waits replaces it.</summary>
    public class EveningTalkPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static EveningTalkView Talk => EveningTalkView.Instance;
        static VisualElement MomentRoot => MomentPlayer.Instance.GetComponent<UIDocument>().rootVisualElement;

        IEnumerator Quiet(int years = 8)
        {
            yield return OpenSlice(seed: Seed);
            SimLayoutSource.DebugSkipYears(years);
            yield return null;
            Sim.CleanAll();
            Sim.CardsSeenToNow();
            CardStackView.Instance.Model?.HidePeek();
            GameHud.Instance.SetHistoryDot(false);
            yield return Frames(10);
        }

        static bool CaptionUp => MomentRoot.Q("moment-caption").resolvedStyle.display == DisplayStyle.Flex;

        [UnityTest, Timeout(120000)]
        public IEnumerator TheCloseGathersSpeaksAndRestsOnTheShelf()
        {
            yield return Quiet();
            var cam = Camera.main.GetComponent<BoxCamera>();
            Assert.IsFalse(EveningTalkView.Busy(), "the beats are spent");
            int threads = Sim.State.threads.Count;
            string line = Sim.DebugForceTeaser(ThreadSource.Rivalry);
            Assert.IsTrue(Talk.Begin(line));
            Assert.IsFalse(GameHud.Instance.IsVisible, "the HUD steps away");

            yield return Until(() => Talk.Step == CloseStep.Speak, 5f);
            yield return Frames(3);
            Assert.AreEqual(line, MomentRoot.Q("moment-caption").Q<Label>(className: "kg-caption-line").text);
            Assert.IsTrue(CaptionUp);
            Assert.That(Talk.Speaker, Is.Not.Empty, "a role-holder speaks");
            var tag = Talk.GetComponent<UIDocument>().rootVisualElement.Q("close-speaker");
            Assert.AreEqual(DisplayStyle.Flex, tag.resolvedStyle.display, "the speaker's name");
            Assert.Greater(Sim.State.threads.Count, threads);

            yield return Until(() => Talk.Step == CloseStep.Rest, 6f);
            yield return Until(() => !cam.IsFlying, 2f);
            Assert.AreEqual(cam.maxZoom, cam.zoom, 0.01f, "the camera eased out to the shelf");
            Assert.IsTrue(CaptionUp, "the last line stays");
            Assert.AreEqual(DisplayStyle.None, tag.resolvedStyle.display);
            GameHud.Instance.Toast("Nothing competes with the last line.", 1f);
            yield return Frames(4);
            Assert.IsFalse(GameHud.Instance.IsVisible);

            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return Frames(30);
            Assert.IsFalse(Talk.Open, "a touch leaves it");
            Assert.IsTrue(GameHud.Instance.IsVisible, "everything comes back");
            Assert.IsFalse(CaptionUp);
            AssertNoErrors("the close");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ATouchBeforeTheLineLeavesAndPromisesNothing()
        {
            yield return Quiet();
            int threads = Sim.State.threads.Count;
            Assert.IsTrue(Talk.Begin());
            yield return Until(() => Talk.Step == CloseStep.Gather, 3f);
            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return Frames(10);
            Assert.IsFalse(Talk.Open);
            Assert.IsTrue(Talk.LeftEarly);
            Assert.AreEqual(threads, Sim.State.threads.Count, "nothing was said, so nothing was opened");
            Assert.IsTrue(GameHud.Instance.IsVisible);
            AssertNoErrors("leaving early");
        }

        /// <summary>Tester run 6: a touch in the gather flight left the camera at the fire, by tap and by a held finger.</summary>
        [UnityTest, Timeout(120000)]
        [Category("Tester6")]
        public IEnumerator ATouchInTheGatherFlightStillGoesHome()
        {
            yield return Quiet();
            var cam = Camera.main.GetComponent<BoxCamera>();
            float zoom = cam.zoom; var target = cam.target;
            var at = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            foreach (bool held in new[] { false, true })
            {
                Assert.IsTrue(Talk.Begin());
                yield return new WaitForSecondsRealtime(1f);
                Assert.IsTrue(cam.IsFlying, "the gather flight is under way");
                int id = touchId++;
                BeginTouch(id, at, queueEventOnly: true);
                yield return Frames(3);
                if (held) yield return new WaitForSecondsRealtime(0.5f);
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                yield return Frames(4);
                Assert.IsFalse(Talk.Open, "the touch left");
                yield return Until(() => !cam.IsFlying, 4f);
                Assert.IsFalse(cam.IsFlying, "the way home landed");
                Assert.AreEqual(zoom, cam.zoom, 0.02f, "zoom came home, held " + held);
                Assert.Less((cam.target - target).magnitude, 0.03f, "target came home, held " + held);
                yield return Frames(4);
            }
            AssertNoErrors("gather flight touch");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheNextReturnsPayoffCardShowsTheTeaser()
        {
            yield return Quiet();
            var adults = Sim.State.notables.Where(n => n.alive && n.age >= 18f).OrderBy(n => n.id).ToList();
            var a = adults[0]; var b = adults[1];
            a.rivalId = b.id; b.rivalId = a.id;
            string line = Sim.DebugForceTeaser(ThreadSource.Rivalry);
            Assert.IsTrue(Talk.Begin(line));
            yield return Until(() => Talk.Step == CloseStep.Rest, 10f);
            Talk.Leave();
            yield return Frames(6);

            a.rivalId = -1; b.rivalId = -1;
            var made = Sim.Chronicle("reconcile", $"{a.FullName} and {b.FullName} made up.", 0.8f, a.id);
            made.otherNotableId = b.id;
            GameClock.DebugAway(24);
            yield return Frames(6);
            var stack = CardStackView.Instance;
            Assert.AreEqual(Cards.CardStackMode.Peek, stack.Model.Mode);
            stack.Model.TapPeek();
            yield return Frames(6);
            for (int i = 0; i < 8 && string.IsNullOrEmpty(stack.Model.Face.teaserLine); i++) { stack.Model.Next(); yield return Frames(2); }
            StringAssert.Contains(line, stack.Model.Face.teaserLine, "the payoff card links back");
            var seen = stack.Root.Q<Label>("cards-teaser");
            StringAssert.Contains(line, seen.text);
            Assert.AreEqual(DisplayStyle.Flex, seen.resolvedStyle.display);
            AssertNoErrors("the payoff");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AnEraThatWaitsReplacesTheTeaserAndAsksOnce()
        {
            yield return Quiet();
            Sim.ForceEraReady();
            int threads = Sim.State.threads.Count;
            yield return Frames(4);
            Assert.IsTrue(Talk.Begin());
            yield return Until(() => Talk.Step == CloseStep.Speak, 5f);
            yield return Frames(3);
            Assert.AreEqual(EraWaiting.CloseLine, Talk.Line);
            Assert.AreEqual(threads, Sim.State.threads.Count, "no teaser while an era waits");
            yield return Until(() => Talk.Step == CloseStep.Shelf, 5f);
            yield return Frames(3);
            Assert.IsTrue(MomentPlayer.Instance.NotifyAskUp, "the first time, the notification ask");
            Assert.IsFalse(GameHud.Instance.IsVisible, "the HUD stays down");
            yield return Until(() => Talk.Step == CloseStep.Rest, 5f);
            yield return TapAt(GameUiPanel.ScreenCenter(MomentRoot.Q("moment-notify-no")));
            yield return Frames(10);
            Assert.IsFalse(Talk.Open, "the answer ends it");
            Assert.IsTrue(GameHud.Instance.IsVisible);

            Assert.IsTrue(Talk.Begin());
            yield return Until(() => Talk.Step == CloseStep.Rest, 10f);
            Assert.IsFalse(MomentPlayer.Instance.NotifyAskUp, "asked once");
            Talk.Leave();
            AssertNoErrors("the era close");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SteppingBackStartsItOnlyAfterALongSitting()
        {
            yield return Quiet();
            var cam = Camera.main.GetComponent<BoxCamera>();
            Sim.State.storyFlags.Add(CloseRules.FlagFirstClose); // not the first sitting, which counts as long
            EveningTalkView.AutoStart = true;
            cam.zoom = 1.25f;
            yield return Frames(6);
            Assert.IsFalse(Talk.Open, "a short check-in ends quietly");
            EveningTalkView.DebugLongSitting();
            yield return Frames(6);
            Assert.IsTrue(Talk.Open, "stepping back");
            Talk.Leave();
            Talk.SessionBonus = 0.0;
            EveningTalkView.AutoStart = false;
            AssertNoErrors("stepping back");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EachTeaserKindIsForcedFromTheDebugHook()
        {
            yield return Quiet();
            foreach (var kind in new[] { ThreadSource.Era, ThreadSource.Seat, ThreadSource.Couple, ThreadSource.Gift, ThreadSource.Rivalry, ThreadSource.Slice })
            {
                string said = EveningTalkView.DebugTeaser(kind);
                Assert.IsTrue(Talk.Open, kind + ": " + said);
                yield return Until(() => Talk.Step == CloseStep.Speak, 5f);
                Assert.AreEqual(said, Talk.Line);
                Talk.Leave();
                yield return Frames(6);
            }
            AssertNoErrors("teaser kinds");
        }
    }
}
