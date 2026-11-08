using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #327 Option B on the real scene (docs/design/people-talk.md): every spoken line, at any zoom, is on the one caption at the top with the
    /// speaker's name, the ring under them and the speech mark over their head; no over-head bubble but the first run's wish; a scene's line
    /// that finds the word busy waits its turn; and a first-run line never stacks with a caption.
    /// </summary>
    [Category("People327")]
    public class CaptionEveryLinePlayTests : PlayTestBase
    {
        const int Seed = 1234;
        const string Look = "F:/GameDev/AquariumCiv/Temp/agent/327/";
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static ShowMeDirector Director => ShowMeDirector.Instance;
        static BoxCamera Cam => Camera.main.GetComponent<BoxCamera>();
        static PeopleView People => PeopleView.Find();

        static IEnumerator LookShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Look);
            File.WriteAllBytes(Look + name + ".png", tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        IEnumerator Village()
        {
            yield return OpenSlice(1179, 2556, Seed);
            ShowMeDirector.BubblesOn = false;
            MomentPlayer.DebugGrowVillage(30f);
            yield return Frames(3);
            Sim.CleanAll();
            PeopleView.DebugSetHour(10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            for (int calm = 0, f = 0; calm < 60 && f < 900; f++) { calm = StagedMoments.StopAll() == 0 && StagedMoments.Playing == 0 ? calm + 1 : 0; yield return null; }
        }

        /// <summary>A named grown-up awake and on view (the middle of the screen first).</summary>
        static PersonRef Named()
        {
            var pv = People;
            var best = PersonRef.None;
            float bestD = float.MaxValue;
            foreach (var a in pv.Activities)
            {
                if (!a.who.IsNotable || a.kind == ActivityKind.Sleep || !pv.TryGetDollTop(a.who, out var w)) continue;
                var v = Camera.main.WorldToViewportPoint(w);
                if (v.z <= 0f || v.x < 0.05f || v.x > 0.95f || v.y < 0.3f || v.y > 0.9f) continue;
                float d = (new Vector2(v.x, v.y) - new Vector2(0.5f, 0.6f)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = a.who; }
            }
            return best;
        }

        static VisualElement ShowMeRoot => Director.GetComponentInChildren<UIDocument>().rootVisualElement;

        /// <summary>The routing: at the default view (zoom 1) and close up (0.45), a line is the caption with the name, the ring and the
        /// speech mark, and never a bubble.</summary>
        [UnityTest, Timeout(150000)]
        public IEnumerator EveryLineIsTheCaptionWithNameRingAndMark([Values(1f, 0.45f)] float zoom)
        {
            yield return Village();
            Cam.EaseTo(Cam.target, Cam.yaw, Cam.pitch, zoom, 0.05f);
            yield return new WaitForSecondsRealtime(0.6f);
            var who = Named();
            Assume.That(who.IsNone, Is.False, "a named person on view");
            ShowMeDirector.BubblesOn = true;
            string said = ShowMeDirector.DebugSayOver(who, "The beans by the pond are up. Come and see.");
            StringAssert.StartsWith("line", said);
            yield return Frames(4);
            Assert.IsTrue(Director.CaptionUp, "on the caption");
            Assert.AreEqual(LineChannel.Caption, LineRoute.ChannelFor(Director.LastKind));
            Assert.AreEqual(Sim.FindNotable(who.notableId).name, Director.LastWho, "the speaker's name under the line");
            Assert.AreEqual(who, Director.LineSpeaker);
            Assert.IsTrue(Director.SpeakerRingUp, "the ring under them");
            Assert.IsTrue(Director.SpeechMarkUp, "the speech mark over them");
            Assert.AreEqual(who, Director.Chatter.SpeechDrawnOver);
            Assert.IsNull(ShowMeRoot.Q("showme-bubble"), "no over-head bubble for a spoken line");
            var cap = ShowMeRoot.Q("showme-caption");
            Assert.AreEqual(DisplayStyle.Flex, cap.resolvedStyle.display);
            Assert.Less(cap.worldBound.yMax, ShowMeRoot.worldBound.height * 0.4f, "in the top lane");
            Assert.AreEqual(0, Director.Chatter.Plan.Live.Count(s => !s.reaction), "the chatter fades under a real line");
            Debug.Log($"[#327] zoom {zoom}: {said}; ring at {Director.Ring.At}, mark at {Director.Chatter.SpeechAt}, caption {cap.worldBound}");
            yield return LookShot($"caption-zoom-{zoom:0.00}");
            AssertNoErrors("routing " + zoom);
        }

        /// <summary>A scene's line (a relic's, here) has the ring and the mark while it shows, and they go with it; the word is free after.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator ASceneLinesRingAndMarkGoWithIt()
        {
            yield return Village();
            var who = Named();
            Assume.That(who.IsNone, Is.False, "a named person on view");
            Assert.IsTrue(Director.SayOrWait("Did you hear that? Up there.", who, 2f, LineKind.Relic), "the word is free: it shows now");
            yield return Frames(3);
            Assert.IsTrue(Director.CaptionUp);
            Assert.IsTrue(Director.SpeakerRingUp, "ring");
            Assert.IsTrue(Director.SpeechMarkUp, "mark");
            yield return Until(() => !Director.CaptionUp, 4f);
            Assert.IsFalse(Director.CaptionUp, "the line reads its time and goes");
            yield return new WaitForSecondsRealtime(0.4f); // the fades
            Assert.IsFalse(Director.SpeakerRingUp, "the ring goes with it");
            Assert.IsFalse(Director.Chatter.SpeechDrawn, "and the mark");
            Assert.IsTrue(GameUiPanel.Bubbles.Free(BubbleHost.Rank.World), "the word is free again");
            AssertNoErrors("scene line");
        }

        /// <summary>A second scene line while one is up waits its turn (no second system, not dropped), then shows.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator ASceneLineWaitsForTheOneUpThenShows()
        {
            yield return Village();
            var who = Named();
            Assume.That(who.IsNone, Is.False, "a named person on view");
            Assert.IsTrue(Director.SayOrWait("Did you hear that? Up there.", who, 2f, LineKind.Relic));
            Assert.IsFalse(Director.SayOrWait("Hush. Listen again.", who, 2f, LineKind.Crumb), "the second waits");
            Assert.AreEqual(1, Director.WaitingLines);
            yield return Frames(3);
            Assert.AreEqual("Did you hear that? Up there.", Director.ShownCaption, "the first isn't cut");
            yield return Until(() => Director.ShownCaption == "Hush. Listen again.", 5f);
            Assert.AreEqual("Hush. Listen again.", Director.ShownCaption, "then the second");
            Assert.AreEqual(0, Director.WaitingLines);
            Assert.AreEqual(0, Director.LinesLetGo);
            AssertNoErrors("waiting line");
        }

        /// <summary>Stacking, the way the first run asks for the word (a Line-rank voice asked every frame): it waits while a caption is up,
        /// and a scene's line waits while it holds the word. One message on screen at a time.</summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator AFirstRunLineAndACaptionNeverStack()
        {
            yield return Village();
            var who = Named();
            Assume.That(who.IsNone, Is.False, "a named person on view");
            var firstRun = GameUiPanel.Bubbles.Join(BubbleHost.Rank.Line);
            Assert.IsTrue(Director.Say("Rain on the east field.", who, 1.5f, LineKind.Echo));
            yield return Frames(2);
            Assert.IsFalse(GameUiPanel.Bubbles.Ask(firstRun), "a first-run line waits while the caption is up");
            yield return Until(() => !Director.CaptionUp, 4f);
            Assert.IsTrue(GameUiPanel.Bubbles.Ask(firstRun), "and gets the word when it's gone");
            Assert.IsFalse(Director.SayOrWait("Did you hear that?", who, 1.5f, LineKind.Wish), "a wish's thanks waits while the first run speaks");
            yield return Frames(3);
            Assert.IsFalse(Director.CaptionUp, "no caption over the first run's line");
            Assert.AreEqual(1, Director.WaitingLines);
            GameUiPanel.Bubbles.Done(firstRun);
            yield return Until(() => Director.CaptionUp, 2f);
            Assert.AreEqual("Did you hear that?", Director.ShownCaption, "then it's said");
            AssertNoErrors("stacking");
        }

        /// <summary>The real first run: its wish is still an over-head bubble (it points at a person to tap), and it waits for a caption
        /// that is up when the step comes.</summary>
        [UnityTest, Timeout(150000)]
        public IEnumerator TheFirstRunsWishIsStillABubbleAndWaitsForTheCaption()
        {
            yield return OpenSlice(1179, 2556, Seed, skipFirstRun: false);
            var fr = FirstRunView.Instance;
            Assert.IsTrue(fr.Active, "the first run");
            yield return Until(() => fr.Step == FirstRunStep.Wipe, 6f);
            fr.Run.DebugSkipTo(FirstRunStep.Name);
            yield return Frames(6);
            var who = PersonRef.Notable(Sim.State.notables.First(n => n.alive).id);
            Assert.IsTrue(Director.Say("We found a dry spot for the tents.", who, 3f, LineKind.Echo), "a caption in the first run");
            fr.Run.DebugSkipTo(FirstRunStep.Rain);
            var bubble = fr.Root.Q("firstrun-bubble");
            bool stacked = false;
            for (int f = 0; f < 30; f++)
            {
                yield return null;
                if (Director.CaptionUp && bubble.resolvedStyle.display == DisplayStyle.Flex) stacked = true;
            }
            Assert.IsFalse(stacked, "the wish never shows under a caption");
            yield return Until(() => !Director.CaptionUp, 5f);
            yield return Until(() => bubble.resolvedStyle.display == DisplayStyle.Flex, 6f);
            Assume.That(bubble.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the rain wish shows in this box");
            Assert.IsInstanceOf<GlassBubble>(bubble, "the first run's wish is a bubble with a tail");
            Assert.IsFalse(Director.CaptionUp, "and nothing on the caption with it");
            yield return LookShot("firstrun-wish-bubble");
            AssertNoErrors("first run wish");
        }
    }
}
