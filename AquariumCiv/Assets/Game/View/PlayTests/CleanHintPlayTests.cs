using System.Collections;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>#160 by touch: a press on mess outside clean mode pulses Clean and says so, the words are rate limited, the drag still
    /// moves the camera, and clean mode or a first clean ends it.</summary>
    [Category("Part3")]
    public class CleanHintPlayTests : PlayTestBase
    {
        const int Seed = 7531;
        static SimCore Sim => GameClock.Instance.ActiveSim;
        static GameHud Hud => GameHud.Instance;

        IEnumerator WithMess()
        {
            yield return OpenSlice(seed: Seed);
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
            Sim.AddMess(0f, 0f, 3);
            yield return Frames(8);
            Assert.IsNotEmpty(CareView.Instance.Props.AllPieces, "mess to touch");
        }

        static Vector2 OnMess()
        {
            var piece = CareView.Instance.Props.AllPieces.First(p => !p.leaving);
            return Camera.main.WorldToScreenPoint(piece.Center);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ATouchOnMessPulsesCleanAndTheWordsAreRateLimited()
        {
            yield return WithMess();
            var care = CareView.Instance;
            int pulses = Hud.CleanPulses;
            yield return TapAt(OnMess());
            yield return Frames(2);
            Assert.AreEqual(pulses + 1, Hud.CleanPulses, "the button pulses");
            Assert.AreEqual(CleanHint.Text, GameUiPanel.Toasts.Current, "and says why");
            Assert.AreEqual(1, care.TouchHint.HintsShown);

            yield return TapAt(OnMess());
            yield return Frames(2);
            Assert.AreEqual(pulses + 2, Hud.CleanPulses, "tester run 8: a tap that opened nothing gets its answer, however soon");
            Assert.AreEqual(1, care.TouchHint.HintsShown, "and no second hint");
            AssertNoErrors("clean hint");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ATouchOffTheMessOrInCleanModeSaysNothing()
        {
            yield return WithMess();
            int pulses = Hud.CleanPulses;
            var empty = new Vector2(Screen.width * 0.5f, Screen.height * 0.9f);
            yield return TapAt(empty);
            yield return Frames(2);
            Assert.AreEqual(pulses, Hud.CleanPulses, "not on mess");
            Hud.SetCleanMode(true);
            yield return Frames(2);
            yield return TapAt(OnMess());
            yield return Frames(2);
            Assert.AreEqual(pulses, Hud.CleanPulses, "clean mode needs no pointing");
            AssertNoErrors("no hint");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheDragStillMovesTheCameraAndACleanEndsTheHint()
        {
            yield return WithMess();
            var cam = Camera.main.GetComponent<BoxCamera>();
            var start = OnMess();
            float yaw = cam.yaw;
            int pulses = Hud.CleanPulses;
            var path = Enumerable.Range(0, 12).Select(i => start + new Vector2(i * 22f, 0f)).ToArray();
            yield return Drag(path, 30f);
            yield return Frames(3);
            Assert.AreEqual(pulses + 1, Hud.CleanPulses, "the press pulsed");
            Assert.AreNotEqual(yaw, cam.yaw, "and the drag orbited");
            CareView.Instance.TouchHint.Cleaned();
            CareView.Instance.TouchHint.Touch(false, true, Time.unscaledTime + 100f);
            yield return TapAt(OnMess());
            yield return Frames(2);
            Assert.AreEqual(pulses + 2, Hud.CleanPulses, "tester run 9: a tap on mess answers, cleaned or not");
            AssertNoErrors("drag");
        }
    }
}
