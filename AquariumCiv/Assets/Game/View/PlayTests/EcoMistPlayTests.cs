using System;
using System.Collections;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P8 misting by touch (#244) in the real Slice scene at a phone size: a barren box from the debug switch; a finger held
    /// still on the land near the camp mists it (the sim gets the mist, the ground greens at once, the people react) while
    /// the camera stays put; a quick drag still orbits and never mists. Backs up and restores the save and the switch.
    /// </summary>
    [Category("EcoPlay")]
    public class EcoMistPlayTests : InputTestFixture
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
        Touchscreen touch;
        int touchId = 1;
        string savePath, backup;
        bool prefWas;

        IEnumerator Open(int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true);
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = GameClock.DefaultSavePath;
            backup = savePath + ".eco-test-backup";
            if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
            if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
            prefWas = GameClock.EcologyPref;
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
                var b = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                if (CareView.Instance == null && b != null) b.gameObject.AddComponent<CareView>();
                if (EcoMistView.Instance == null && b != null) b.gameObject.AddComponent<EcoMistView>();
            }
            yield return null; yield return null;
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
        }

        void Close()
        {
            GameClock.EcologyPref = prefWas;
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
            File.WriteAllBytes(Shots + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        static Vector2 ScreenOf(BoxView box, float x, float z)
        {
            var w = box.transform.TransformPoint(box.land.OnGround(x, z));
            var s = Camera.main.WorldToScreenPoint(w);
            return new Vector2(s.x, s.y);
        }

        [UnityTest]
        public IEnumerator HoldMists_GreensAtOnce_PeopleReact_CameraStill_DragDoesNot()
        {
            yield return Open();
            try
            {
                Assert.IsNotNull(GameClock.Instance, "the slice's clock");
                string r = GameClock.DebugEcologyBox(true);
                Debug.Log("[eco] " + r);
                for (int i = 0; i < 30; i++) yield return null;
                Debug.Log("[eco] first run: " + FirstRunView.DebugSkipAll()); // #248 reworks the first run; here the mist alone
                for (int i = 0; i < 30; i++) yield return null;
                var sim = GameClock.Instance.Sim;
                Assert.IsTrue(sim.EcologyOn, "a barren box");
                Assert.IsTrue(sim.BeforeCamp, "before the camp");
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var mist = EcoMistView.Instance;
                Assert.IsNotNull(mist, "the mist view is on the box");
                var hud = GameHud.Instance;
                if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
                yield return Shot("eco_play_barren");

                var rig = Camera.main.GetComponent<BoxCamera>();
                var st = sim.Settlement;
                float x = st.Map.greenX + 0.55f, z = st.Map.greenZ - 0.1f;
                var at = ScreenOf(box, x, z);
                Debug.Log($"[eco] hold at {at} for ({x:0.00}, {z:0.00})");
                float bare0 = sim.EcoBareShare, yaw0 = rig.yaw, pitch0 = rig.pitch;

                int id = touchId++;
                BeginTouch(id, at, queueEventOnly: true);
                yield return null;
                float t0 = Time.unscaledTime;
                bool sawMisting = false;
                while (Time.unscaledTime - t0 < 2.2f)
                {
                    // a finger never stays perfectly still: a pixel of wobble
                    MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true);
                    sawMisting |= mist.Misting;
                    yield return null;
                }
                yield return Shot("eco_play_misting");
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                for (int i = 0; i < 20; i++) yield return null;

                Assert.IsTrue(sawMisting, "a still finger turned into a mist");
                Assert.That(mist.MistPulses, Is.GreaterThan(2), "the sim was told while it ran");
                Assert.That(Vector2.Distance(new Vector2(mist.LastMist.x, mist.LastMist.z), new Vector2(x, z)), Is.LessThan(0.25f), "where the finger was");
                Assert.That(sim.EcoBareShare, Is.LessThan(bare0), "the ground greened at once");
                Assert.That(sim.EcoMilestoneMonth(AquariumCiv.Sim.Eco.EcoMilestones.FirstGreen), Is.GreaterThanOrEqualTo(0), "the first green");
                Assert.That(mist.Reactions, Is.GreaterThan(0), "the people were asked to react");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(rig.yaw, yaw0)), Is.LessThan(0.5f), "the camera held still");
                Assert.That(Mathf.Abs(rig.pitch - pitch0), Is.LessThan(0.5f));
                for (int i = 0; i < 60; i++) yield return null;
                yield return Shot("eco_play_greened");

                // A drag is the camera's: it orbits and never mists.
                int pulses = mist.MistPulses;
                int id2 = touchId++;
                var p = at;
                BeginTouch(id2, p, queueEventOnly: true);
                yield return null;
                for (int i = 0; i < 25; i++) { p += new Vector2(12f, 0f); MoveTouch(id2, p, delta: Vector2.zero, queueEventOnly: true); yield return null; }
                EndTouch(id2, p, delta: Vector2.zero, queueEventOnly: true);
                for (int i = 0; i < 30; i++) yield return null;
                Assert.AreEqual(pulses, mist.MistPulses, "a drag never mists");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(rig.yaw, yaw0)), Is.GreaterThan(1f), "the drag orbited");
            }
            finally { Close(); }
        }
    }
}
