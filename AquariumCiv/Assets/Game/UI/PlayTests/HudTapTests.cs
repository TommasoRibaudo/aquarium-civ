using System.Collections;
using AquariumCiv.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI.PlayTests
{
    /// <summary>
    /// The HUD shell (#36) driven the way the tester agent drives it: a fixture touchscreen taps controls found by name
    /// (UI spike rules, docs/design/spikes/ui-automation.md). Every sheet opens and closes by touch, a touch on the HUD
    /// counts as UI for the box camera, the world outside it doesn't. Run alone (one InputTestFixture class per run).
    /// </summary>
    public class HudTapTests : InputTestFixture
    {
        Touchscreen touch;
        Keyboard keyboard;
        int touchId = 1;
        GameHud hud;
        bool soundWas;

        IEnumerator TapAt(Vector2 p)
        {
            BeginTouch(touchId, p, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        IEnumerator Tap(VisualElement root, string name)
        {
            var e = root.Q(name);
            Assert.IsNotNull(e, $"'{name}' exists");
            Assert.AreEqual(DisplayStyle.Flex, e.resolvedStyle.display, $"'{name}' is shown");
            yield return TapAt(GameUiPanel.ScreenCenter(e));
        }

        IEnumerator StartHud()
        {
            LogAssert.ignoreFailingMessages = true; // fixture noise ("Cached unprocessed value..."), see tooling.md
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            soundWas = GameSettings.Sound;
            hud = GameHud.Create();
            for (int i = 0; i < 5; i++) yield return null; // layout
            touch = InputSystem.AddDevice<Touchscreen>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;
        }

        void EndHud()
        {
            GameSettings.Sound = soundWas;
            if (FeedbackUI.Instance != null) Object.Destroy(FeedbackUI.Instance.gameObject);
            if (hud != null) Object.Destroy(hud.gameObject);
        }

        [UnityTest]
        public IEnumerator EverySheetOpensAndClosesByTouch()
        {
            yield return StartHud();
            var root = hud.Root;
            var openers = new[] { ("hud-title", GameHud.Settings), ("hud-history", GameHud.History), ("hud-shape", GameHud.Shaping) };
            foreach (var (opener, id) in openers)
            {
                yield return Tap(root, opener);
                Assert.AreEqual(id, hud.OpenSheet, $"tapping {opener} opens {id}");
                yield return Tap(root, id + "-close");
                Assert.IsNull(hud.OpenSheet, $"Close shuts {id}");

                // A tap on the dimmed world above the sheet closes it too.
                yield return Tap(root, opener);
                Assert.AreEqual(id, hud.OpenSheet);
                yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.97f));
                Assert.IsNull(hud.OpenSheet, $"a tap on the scrim shuts {id}");
            }

            // Back / Esc closes the open sheet.
            yield return Tap(root, "hud-history");
            Assert.AreEqual(GameHud.History, hud.OpenSheet);
            Press(keyboard.escapeKey); yield return null; Release(keyboard.escapeKey); yield return null;
            Assert.IsNull(hud.OpenSheet, "Back closes the sheet");
            EndHud();
        }

        [UnityTest]
        public IEnumerator SettingsSwitchesAndFeedbackWorkByTouch()
        {
            yield return StartHud();
            var root = hud.Root;
            yield return Tap(root, "hud-title");
            bool before = GameSettings.Sound;
            yield return Tap(root, "settings-sound");
            Assert.AreEqual(!before, GameSettings.Sound, "the Sound switch flips the setting");
            Assert.AreEqual(GameSettings.Sound ? 1f : 0f, AudioListener.volume, "and the listener follows");
            Assert.AreEqual(GameSettings.Sound ? "On" : "Off", root.Q<Button>("settings-sound").text);
            yield return Tap(root, "settings-sound");
            Assert.AreEqual(before, GameSettings.Sound);

            // Settings → Send feedback opens the feedback sheet (Editor = test build); its Cancel closes it.
            yield return Tap(root, "settings-feedback");
            Assert.IsNull(hud.OpenSheet, "settings closes when feedback opens");
            for (int i = 0; i < 6 && (FeedbackUI.Instance == null || !FeedbackUI.Instance.IsOpen); i++) yield return null;
            Assert.IsTrue(FeedbackUI.Instance != null && FeedbackUI.Instance.IsOpen, "the feedback sheet is open");
            Assert.IsTrue(hud.IsVisible, "the HUD is back after the screenshot");
            yield return null; yield return null; // the sheet's layout
            Assert.AreEqual(DisplayStyle.None, FeedbackUI.Instance.Root.Q("fb-button").resolvedStyle.display, "the HUD hosts the feedback button");
            yield return Tap(FeedbackUI.Instance.Root, "fb-cancel");
            Assert.IsFalse(FeedbackUI.Instance.IsOpen);

            // The HUD's own Feedback button opens it too.
            yield return Tap(root, "hud-feedback");
            for (int i = 0; i < 6 && !FeedbackUI.Instance.IsOpen; i++) yield return null;
            Assert.IsTrue(FeedbackUI.Instance.IsOpen);
            yield return null; yield return null;
            yield return Tap(FeedbackUI.Instance.Root, "fb-cancel");
            Assert.IsFalse(FeedbackUI.Instance.IsOpen);
            EndHud();
        }

        [UnityTest]
        public IEnumerator HudBlocksTheCameraOnlyWhereItIs()
        {
            yield return StartHud();
            var root = hud.Root;
            Assert.IsTrue(UiInput.Over(GameUiPanel.ScreenCenter(root, "hud-shape")), "a press on Shape is UI");
            Assert.IsTrue(UiInput.Over(GameUiPanel.ScreenCenter(root, "hud-title")), "a press on the title is UI");
            Assert.IsFalse(UiInput.Over(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "the middle of the screen is the world");

            hud.Open(GameHud.Settings);
            yield return null; yield return null;
            Assert.IsTrue(UiInput.Over(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "while a sheet is open its scrim takes the press");
            hud.CloseSheet();

            hud.SetVisible(false);
            yield return null; yield return null;
            Assert.IsFalse(UiInput.Over(GameUiPanel.ScreenCenter(root, "hud-shape")), "a hidden HUD takes no presses");
            hud.SetVisible(true);

            // Every tappable control is at least a 48 dp / 44 pt target.
            yield return null;
            foreach (var name in new[] { "hud-title", "hud-history", "hud-shape", "hud-feedback" })
            {
                var e = root.Q(name);
                Assert.GreaterOrEqual(e.layout.height, GameUiPanel.MinTarget - 0.5f, $"{name} is tall enough");
                Assert.GreaterOrEqual(e.layout.width, GameUiPanel.MinTarget - 0.5f, $"{name} is wide enough");
            }
            EndHud();
        }
    }
}
