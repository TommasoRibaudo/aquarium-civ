using System.Collections;
using AquariumCiv.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI.PlayTests
{
    /// <summary>Camera mode (#70): one tap on Photo hides all UI, a drag or a pinch leaves it hidden, a short tap brings it back.
    /// Run alone (one InputTestFixture class per run).</summary>
    [Category("CameraMode")]
    public class CameraModeTests : InputTestFixture
    {
        Touchscreen touch;
        int touchId = 1;
        GameHud hud;

        IEnumerator TapAt(Vector2 p)
        {
            BeginTouch(touchId, p, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        IEnumerator DragFrom(Vector2 a, Vector2 b)
        {
            BeginTouch(touchId, a, queueEventOnly: true);
            yield return null; yield return null;
            MoveTouch(touchId, b, delta: b - a, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, b, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        IEnumerator Start()
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // With the Slice scene open in the Editor the game's own bootstrap already made a HUD (and a feedback sheet): clear them.
            if (GameHud.Instance != null) Object.Destroy(GameHud.Instance.gameObject);
            if (FeedbackUI.Instance != null) Object.Destroy(FeedbackUI.Instance.gameObject);
            yield return null;
            hud = GameHud.Create();
            for (int i = 0; i < 5; i++) yield return null;
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
        }

        void End()
        {
            if (hud != null) Object.Destroy(hud.gameObject);
        }

        // Shown = it and every ancestor is displayed.
        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        static Vector2 Middle => new(Screen.width * 0.5f, Screen.height * 0.5f);

        [UnityTest]
        public IEnumerator PhotoHidesEverythingAndATapBringsItBack()
        {
            yield return Start();
            var root = hud.Root;
            hud.SetCleanMode(true);
            yield return null; yield return null;
            Assert.IsTrue(hud.IsVisible);

            yield return new WaitForSeconds(0.3f);
            yield return TapAt(GameUiPanel.ScreenCenter(root, "hud-photo"));
            Assert.IsTrue(hud.CameraMode, "tapping Photo enters camera mode");
            Assert.IsFalse(hud.CleanMode, "clean mode is closed first");
            Assert.IsNull(hud.OpenSheet);
            yield return null; yield return null;
            foreach (var n in new[] { "hud-title", "hud-history", "hud-shape", "hud-clean", "hud-photo", "hud-feedback", "hud-toast" })
            {
                var e = root.Q(n);
                Assert.IsNotNull(e, n);
                Assert.IsFalse(Shown(e), $"{n} is hidden");
            }
            Assert.IsTrue(Shown(root.Q("hud-photo-exit")), "the way back is shown");
            hud.Toast("hello");
            Assert.IsFalse(Shown(root.Q("hud-toast")), "no toasts in camera mode");
            Assert.IsFalse(UiInput.Over(GameUiPanel.ScreenCenter(root, "hud-shape")), "hidden buttons take no presses");
            yield return new WaitForSecondsRealtime(0.3f); // past the HUD's 0.15 s guard, in seconds: the frame rate varies

            // A drag (orbit) and a pinch do not exit.
            yield return DragFrom(Middle, Middle + new Vector2(300, 100));
            Assert.IsTrue(hud.CameraMode, "a drag does not bring the UI back");
            BeginTouch(touchId, Middle, queueEventOnly: true);
            BeginTouch(touchId + 1, Middle + new Vector2(50, 0), queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, Middle, queueEventOnly: true);
            EndTouch(touchId + 1, Middle + new Vector2(50, 0), queueEventOnly: true);
            yield return null; yield return null;
            touchId += 2;
            Assert.IsTrue(hud.CameraMode, "a two-finger touch does not bring the UI back");

            // A short tap in the world does.
            yield return TapAt(Middle);
            Assert.IsFalse(hud.CameraMode, "a tap brings the UI back");
            yield return null; yield return null;
            Assert.IsTrue(Shown(root.Q("hud-shape")));
            Assert.IsFalse(Shown(root.Q("hud-photo-exit")));
            End();
        }

        [UnityTest]
        public IEnumerator ThePillBringsItBackAndSheetsCloseFirst()
        {
            yield return Start();
            var root = hud.Root;
            hud.Open(GameHud.Shaping);
            hud.SetCameraMode(true);
            Assert.IsNull(hud.OpenSheet, "an open sheet is closed on entering");
            yield return new WaitForSeconds(0.3f);
            yield return null; yield return null;
            yield return TapAt(GameUiPanel.ScreenCenter(root, "hud-photo-exit"));
            Assert.IsFalse(hud.CameraMode, "the pill brings the UI back");
            End();
        }
    }
}
