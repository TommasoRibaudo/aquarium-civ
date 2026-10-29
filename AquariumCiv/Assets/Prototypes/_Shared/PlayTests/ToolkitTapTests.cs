using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.Prototypes.Tests
{
    /// <summary>
    /// UI-automation spike: a real (fixture) touch reaches a UI Toolkit button, found by name, with no pixel guessing.
    /// IMGUI can't do this in this project (tooling.md 2026-10-29). Run it alone (one InputTestFixture test per run).
    /// </summary>
    public class ToolkitTapTests : InputTestFixture
    {
        Touchscreen touch;
        int touchId = 1;

        IEnumerator Tap(VisualElement root, string name)
        {
            var p = UiProbe.ScreenCenter(root, name);
            BeginTouch(touchId, p, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        [UnityTest]
        public IEnumerator ATouchTapsToolkitButtonsByName()
        {
            LogAssert.ignoreFailingMessages = true; // fixture noise ("Cached unprocessed value..."), see tooling.md
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            var spike = ToolkitSurveySpike.Create();
            int sent = -1;
            spike.Sent += r => sent = r;
            for (int i = 0; i < 5; i++) yield return null; // layout
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;

            var root = spike.Root;
            Assert.IsNotNull(root.Q<Button>("send"));
            Assert.IsFalse(root.Q<Button>("send").enabledSelf, "Send waits for an answer");

            yield return Tap(root, "send"); // disabled: nothing happens
            Assert.AreEqual(0, spike.Sends);

            yield return Tap(root, "q1-4");
            Assert.AreEqual(4, spike.Rating, "the tap on '4' reached the button");
            Assert.IsTrue(root.Q<Button>("send").enabledSelf);

            yield return Tap(root, "send");
            Assert.AreEqual(1, spike.Sends);
            Assert.AreEqual(4, sent);

            UnityEngine.Object.Destroy(spike.gameObject);
        }
    }
}
