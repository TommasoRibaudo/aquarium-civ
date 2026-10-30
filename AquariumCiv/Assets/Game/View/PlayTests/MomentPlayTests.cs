using System;
using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The era moment (#88) in the real Slice scene at a portrait phone size: the waiting state (Watch button, piles), a tap
    /// on Watch, the HUD hidden and the clock held, a held finger hurrying, the cheer moving the era, the camera handed
    /// back; and leaving before the cheer, which replays. Run alone (one InputTestFixture class per run); it backs up and
    /// restores the slice's save file.
    /// </summary>
    [Category("MomentPlay")]
    public class MomentPlayTests : InputTestFixture
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
        Touchscreen touch;
        int touchId = 1;
        string savePath, backup;

        IEnumerator Open(int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            backup = savePath + ".moment-test-backup";
            if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
            if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
            SetSize(w, h);
            float waited = 0f;
            while ((Screen.width != w || Screen.height != h) && waited < 4f) { waited += 0.1f; yield return new WaitForSecondsRealtime(0.1f); }
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            for (int i = 0; i < 90; i++) yield return null;
            if (GameClock.Instance != null)
            {
                if (GameHud.Instance == null) GameHud.Create();
                if (MomentPlayer.Instance == null) MomentPlayer.Create();
            }
            yield return null; yield return null;
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
        }

        void Close()
        {
            MomentPlayer.DebugHoldHurry = false;
            if (MomentPlayer.Instance != null && MomentPlayer.Instance.Playing) MomentPlayer.Instance.Leave();
            try
            {
                if (File.Exists(backup + ".bak")) { File.Copy(backup + ".bak", savePath + ".bak", true); File.Delete(backup + ".bak"); }
                if (File.Exists(backup)) { File.Copy(backup, savePath, true); File.Delete(backup); }
                else if (File.Exists(savePath)) File.Delete(savePath);
            }
            catch (Exception e) { Debug.LogWarning("restore save: " + e.Message); }
        }

        static void SetSize(int w, int h)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("AquariumCiv.EditorTools.AgentGameView");
                if (t == null) continue;
                t.GetMethod("SetSize").Invoke(null, new object[] { w, h });
                return;
            }
        }

        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Shots);
            File.WriteAllBytes(Shots + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        IEnumerator TapAt(Vector2 p)
        {
            BeginTouch(touchId, p, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        static bool Shown(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        static IEnumerator Until(Func<bool> done, float seconds)
        {
            float t = 0f;
            while (!done() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator WatchHurryCheerAndTheCameraComesBack()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var mp = MomentPlayer.Instance;
                var hud = GameHud.Instance;
                var cam = Camera.main.GetComponent<BoxCamera>();
                Assert.IsNotNull(mp); Assert.IsNotNull(cam);
                if (sim.State.era != Era.Camp) Assert.Inconclusive("the fresh box should start in the camp");
                sim.State.grime = 0f;
                Assert.IsTrue(sim.ForceEraReady());
                MomentPlayer.DebugWaited(100);
                yield return new WaitForSecondsRealtime(0.7f);

                // the waiting state
                var watch = mp.GetComponent<UIDocument>().rootVisualElement.Q<Button>("moment-watch");
                Assert.IsTrue(Shown(watch), "Watch shows while the era waits");
                Assert.AreEqual("Watch them become a village", watch.text);
                var props = UnityEngine.Object.FindAnyObjectByType<WaitingProps>();
                Assert.IsTrue(props != null && props.Showing, "the piles are in the box");
                Assert.AreEqual(3, props.Level);
                StringAssert.Contains("ready to become a village", mp.Glance);
                hud.SetCleanMode(true);
                yield return null; yield return null;
                Assert.IsFalse(Shown(watch), "hidden in clean mode");
                hud.SetCleanMode(false);
                yield return null; yield return null;
                yield return Shot("moment_waiting");

                // Watch, by touch
                yield return TapAt(GameUiPanel.ScreenCenter(watch));
                Assert.IsTrue(mp.Playing, "a tap on Watch starts the moment");
                Assert.IsFalse(hud.IsVisible, "the HUD slides away");
                Assert.IsTrue(GameClock.Instance.Held, "the sim clock holds");
                Assert.IsFalse(cam.enabled, "the camera is guided");
                long month = sim.State.month;

                // a held finger hurries (it stays put: no orbit)
                var mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
                BeginTouch(touchId, mid, queueEventOnly: true);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.AreEqual(MomentDirector.HurrySpeed, mp.Director.Speed, 1e-4f, "holding hurries");
                EndTouch(touchId, mid, delta: Vector2.zero, queueEventOnly: true);
                touchId++;
                yield return null; yield return null;
                Assert.AreEqual(1f, mp.Director.Speed, 1e-4f);

                // fast forward (debug hurry ×4) to the wave, take a still, then on to the cheer
                MomentPlayer.DebugHurry(4f);
                MomentPlayer.DebugHoldHurry = true;
                yield return Until(() => mp.Director.Time >= 26f, 20f);
                MomentPlayer.DebugHoldHurry = false;
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("moment_beat_wave");
                MomentPlayer.DebugHoldHurry = true;
                yield return Until(() => mp.Director.Cheered, 30f);
                Assert.IsTrue(mp.Director.EraAdvanced);
                Assert.AreEqual(Era.Village, sim.State.era, "the cheer moved the era");
                MomentPlayer.DebugHoldHurry = false;
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("moment_beat_cheer");
                MomentPlayer.DebugHoldHurry = true;
                yield return Until(() => !mp.Playing, 30f);
                MomentPlayer.DebugHoldHurry = false;
                yield return null; yield return null;

                Assert.IsFalse(mp.Playing);
                Assert.AreEqual(month, sim.State.month, "no months passed during the moment");
                Assert.IsTrue(cam.enabled, "the camera is back in the player's hands");
                Assert.IsFalse(GameClock.Instance.Held);
                Assert.IsTrue(hud.IsVisible, "the HUD comes back");
                Assert.IsFalse(Shown(watch), "nothing waits now");
                var ask = mp.GetComponent<UIDocument>().rootVisualElement.Q("moment-notify");
                Assert.IsTrue(Shown(ask), "the first moment ends with the notification question");
                yield return TapAt(GameUiPanel.ScreenCenter(ask.Q("moment-notify-no")));
                Assert.IsFalse(Shown(ask));
                Assert.IsTrue(sim.State.storyFlags.Contains(MomentContext.FlagNotifyAsked));

                // the camera answers a drag again
                float yaw = cam.yaw;
                var a = new Vector2(Screen.width * 0.3f, Screen.height * 0.5f);
                BeginTouch(touchId, a, queueEventOnly: true);
                yield return null;
                for (int i = 1; i <= 10; i++) { MoveTouch(touchId, a + new Vector2(i * 30f, 0f), delta: Vector2.zero, queueEventOnly: true); yield return null; }
                EndTouch(touchId, a + new Vector2(300f, 0f), delta: Vector2.zero, queueEventOnly: true);
                touchId++;
                for (int i = 0; i < 20; i++) yield return null;
                Assert.AreNotEqual(yaw, cam.yaw, "a drag orbits after the moment");
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator LeavingBeforeTheCheerReplays()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var mp = MomentPlayer.Instance;
                sim.State.grime = 0.9f;
                Assert.IsTrue(sim.ForceEraReady());
                Era from = sim.State.era;
                yield return new WaitForSecondsRealtime(0.7f);

                // grimy glass: Watch offers a wipe first, never blocks
                Assert.IsFalse(mp.Watch());
                var offer = mp.GetComponent<UIDocument>().rootVisualElement.Q("moment-wipe");
                yield return null; yield return null;
                Assert.IsTrue(Shown(offer));
                yield return TapAt(GameUiPanel.ScreenCenter(offer.Q("moment-wipe-no")));
                Assert.IsTrue(mp.Playing, "Watch anyway");

                yield return new WaitForSecondsRealtime(1.5f);
                Assert.AreEqual(MomentDirector.LeaveResult.Replays, mp.Leave());
                yield return null;
                Assert.AreEqual(from, sim.State.era);
                Assert.IsTrue(sim.State.eraReady, "the era still waits");
                Assert.IsTrue(Camera.main.GetComponent<BoxCamera>().enabled);
                Assert.IsFalse(GameClock.Instance.Held);
                Assert.IsTrue(GameHud.Instance.IsVisible);

                // again: plays from the first beat
                Assert.IsTrue(mp.Watch(true));
                Assert.AreEqual(0, mp.Director.BeatIndex);
                mp.Leave();
            }
            finally { Close(); }
        }
    }
}
