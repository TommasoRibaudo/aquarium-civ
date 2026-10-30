using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Camera mode in the real Slice scene (#70, user 2026-10-30): the player zooms and moves freely (one finger orbits, two
    /// pinch and pan, by touch), the camera keeps following the person they were following (a person card, a pin), a real
    /// pan lets go, and the auto-orbit waits 10 s. The HUD side is CameraModeTests (UI). It backs up and restores the save.
    /// </summary>
    [Category("CameraModePlay")]
    public class CameraModePlayTests : InputTestFixture
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
        Touchscreen touch;
        int touchId = 1;
        string savePath, backup;
        readonly List<string> errors = new();

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) errors.Add(msg);
        }

        IEnumerator Open(int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            backup = savePath + ".cameramode-test-backup";
            if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
            if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
            GameClock.DebugNextSeed = 1234;
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
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                if (box != null && CareView.Instance == null) box.gameObject.AddComponent<CareView>();
                if (box != null && GlassVoice.Instance == null) box.gameObject.AddComponent<GlassVoice>();
                if (box != null && ShowMeDirector.Instance == null) box.gameObject.AddComponent<ShowMeDirector>();
                if (box != null && PersonCardView.Instance == null) box.gameObject.AddComponent<PersonCardView>();
            }
            ShowMeDirector.BubblesOn = false;
            yield return null; yield return null;
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
            errors.Clear();
            Application.logMessageReceived += OnLog;
        }

        void Close()
        {
            Application.logMessageReceived -= OnLog;
            GameClock.DebugNextSeed = 0;
            ShowMeDirector.BubblesOn = true;
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

        IEnumerator Drag(Vector2 a, Vector2 b, int steps = 20)
        {
            int id = touchId++;
            BeginTouch(id, a, queueEventOnly: true);
            yield return null; yield return null;
            for (int i = 1; i <= steps; i++)
            {
                MoveTouch(id, Vector2.Lerp(a, b, i / (float)steps), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(id, b, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
        }

        /// <summary>Two fingers from (a0, b0) to (a1, b1).</summary>
        IEnumerator TwoFingers(Vector2 a0, Vector2 b0, Vector2 a1, Vector2 b1, int steps = 20)
        {
            int ia = touchId++, ib = touchId++;
            BeginTouch(ia, a0, queueEventOnly: true);
            BeginTouch(ib, b0, queueEventOnly: true);
            yield return null; yield return null; yield return null;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                MoveTouch(ia, Vector2.Lerp(a0, a1, t), delta: Vector2.zero, queueEventOnly: true);
                MoveTouch(ib, Vector2.Lerp(b0, b1, t), delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(ia, a1, delta: Vector2.zero, queueEventOnly: true);
            EndTouch(ib, b1, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null; yield return null;
        }

        static Vector2 Mid => new(Screen.width * 0.5f, Screen.height * 0.55f);
        static BoxCamera Rig => Camera.main.GetComponent<BoxCamera>();

        static bool DollWorld(PersonRef who, out Vector3 w)
        {
            w = default;
            var pv = PeopleView.Find();
            if (pv == null || !pv.TryGetDollPosition(who, out var p)) return false;
            w = pv.transform.TransformPoint(new Vector3(p.x, 0f, p.y));
            return true;
        }

        /// <summary>A living notable with a doll on the land now.</summary>
        static IEnumerator FindDoll(Action<int> found)
        {
            var sim = GameClock.Instance.Sim;
            PeopleView.SetHourGently(10.5f);
            for (int i = 0; i < 300; i++)
            {
                var n = sim.State.notables.FirstOrDefault(x => x.alive && DollWorld(PersonRef.Notable(x.id), out _));
                if (n != null) { found(n.id); yield break; }
                yield return null;
            }
            Assert.Fail("no notable has a doll");
        }

        [UnityTest]
        public IEnumerator OrbitPinchAndPanAllWorkInCameraMode()
        {
            yield return Open();
            try
            {
                var hud = GameHud.Instance;
                var rig = Rig;
                hud.SetCameraMode(true);
                yield return new WaitForSecondsRealtime(0.3f);

                float yaw0 = rig.yaw, pitch0 = rig.pitch;
                yield return Drag(Mid, Mid + new Vector2(0.2f * Screen.width, 0.06f * Screen.height));
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsTrue(hud.CameraMode, "a drag stays in camera mode");
                Assert.Greater(Mathf.Abs(rig.yaw - yaw0), 2f, "one finger orbits (yaw)");
                Assert.Greater(Mathf.Abs(rig.pitch - pitch0), 0.5f, "and tilts");

                float zoom0 = rig.zoom;
                var side = new Vector2(0.15f * Screen.width, 0f);
                yield return TwoFingers(Mid - side, Mid + side, Mid - side * 2f, Mid + side * 2f);
                Assert.IsTrue(hud.CameraMode, "a pinch stays in camera mode");
                Assert.Less(rig.zoom, zoom0 * 0.8f, "a pinch out zooms in");

                var t0 = rig.target;
                var up = new Vector2(0.14f * Screen.width, 0.08f * Screen.height);
                yield return TwoFingers(Mid - side, Mid + side, Mid - side + up, Mid + side + up);
                Assert.IsTrue(hud.CameraMode, "a pan stays in camera mode");
                Assert.Greater(new Vector2(rig.target.x - t0.x, rig.target.z - t0.z).magnitude, 0.15f, "two fingers pan");
                yield return Shot("camera-mode-free");
                Assert.IsEmpty(errors, string.Join("\n", errors));
                hud.SetCameraMode(false);
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TheCameraKeepsFollowingTheOpenCardsPersonUntilAPan()
        {
            yield return Open();
            try
            {
                var hud = GameHud.Instance;
                var rig = Rig;
                var cards = PersonCardView.Instance;
                int id = -1;
                yield return FindDoll(x => id = x);
                var who = PersonRef.Notable(id);
                Assert.IsTrue(cards.Open(id, true), "the card opens");
                float t = 0f;
                while (rig.IsFlying && t < 4f) { t += Time.unscaledDeltaTime; yield return null; }

                hud.SetCameraMode(true);
                Assert.AreEqual(who, rig.Following, "camera mode takes over the card's person");
                yield return null; yield return null;
                Assert.IsFalse(cards.IsOpen, "the card closes in camera mode");

                // It eases onto the doll and stays on them while they walk.
                t = 0f; float off = 1f;
                while (t < 4f)
                {
                    t += Time.unscaledDeltaTime;
                    Assert.IsTrue(rig.IsFollowing, "still following");
                    if (DollWorld(who, out var d)) off = new Vector2(rig.target.x - d.x, rig.target.z - d.z).magnitude;
                    yield return null;
                }
                var b = BoxCameraMath.PanBounds(rig.zoom);
                if (DollWorld(who, out var w) && Mathf.Abs(w.x) < b.x && Mathf.Abs(w.z) < b.y)
                    Assert.Less(off, 0.3f, "the camera is on the doll");
                yield return Shot("camera-mode-follow");

                // One finger and a pinch on the spot keep them.
                yield return Drag(Mid, Mid + new Vector2(0.15f * Screen.width, 0f));
                Assert.AreEqual(who, rig.Following, "an orbit keeps following");
                var side = new Vector2(0.12f * Screen.width, 0f);
                float z0 = rig.zoom;
                yield return TwoFingers(Mid - side, Mid + side, Mid - side * 0.7f, Mid + side * 0.7f);
                Assert.AreEqual(who, rig.Following, "a pinch keeps following");
                Assert.Greater(rig.zoom, z0, "and zooms");
                Assert.IsTrue(hud.CameraMode);

                // A real pan lets go.
                var move = new Vector2(0f, 0.2f * Screen.height);
                yield return TwoFingers(Mid - side, Mid + side, Mid - side + move, Mid + side + move);
                Assert.IsFalse(rig.IsFollowing, "a two-finger pan lets them go");

                hud.SetCameraMode(false);
                Assert.IsFalse(rig.IsFollowing);
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator ThePinnedPersonIsFollowedWhenInView_AndAnUnpinnedBoxIsNot()
        {
            yield return Open();
            try
            {
                var hud = GameHud.Instance;
                var rig = Rig;
                var sim = GameClock.Instance.Sim;
                hud.SetCameraMode(true);
                Assert.IsFalse(rig.IsFollowing, "nobody to follow");
                hud.SetCameraMode(false);
                yield return null;

                int id = -1;
                yield return FindDoll(x => id = x);
                Assert.IsTrue(sim.Pin(id));
                Assert.IsTrue(DollWorld(PersonRef.Notable(id), out var w));
                rig.FlyTo(w, 0.7f, 0.01f);
                yield return null; yield return null;
                hud.SetCameraMode(true);
                Assert.AreEqual(PersonRef.Notable(id), rig.Following, "the pinned person in view is followed");
                hud.SetCameraMode(false);
                sim.Unpin();
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TheAutoOrbitStartsAfterTenSecondsIdle()
        {
            yield return Open();
            try
            {
                var hud = GameHud.Instance;
                var rig = Rig;
                Assert.AreEqual(10f, BoxCamera.AutoOrbitIdleSeconds, "user 2026-10-30");
                Assert.IsTrue(BoxCamera.AutoOrbitInCameraMode);
                yield return Drag(Mid, Mid + new Vector2(0.03f * Screen.width, 0f)); // leave the yaw limits
                yield return new WaitForSecondsRealtime(1f);
                hud.SetCameraMode(true);
                float yaw0 = rig.yaw;
                yield return new WaitForSecondsRealtime(BoxCamera.AutoOrbitIdleSeconds - 2f);
                Assert.AreEqual(yaw0, rig.yaw, 0.05f, "still at 8 s");
                yield return new WaitForSecondsRealtime(4.5f);
                Assert.Greater(Mathf.Abs(rig.yaw - yaw0), 1f, "drifting by 12.5 s");
                hud.SetCameraMode(false);
            }
            finally { Close(); }
        }
    }
}
