using System;
using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// The event log (#81) and the week metrics and day-3 question (#100) in the real Slice scene: a scripted session (open, wipe,
    /// an era moment watched live, one left, pause and back) writes the expected JSONL lines; the recall question shows on a
    /// faked day 3 and not over a moment. Backs up and restores the slice's save AND its telemetry folder. Run alone.
    /// </summary>
    [Category("TelemetryPlay")]
    public class TelemetryPlayTests : InputTestFixture
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        Touchscreen touch;
        int touchId = 1;
        string gameDir, backupDir;

        IEnumerator Open(int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            GameClock.DebugDayOffset = 0;
            gameDir = Path.Combine(Application.persistentDataPath, "game");
            backupDir = gameDir + "-telemetry-test-backup";
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
            Directory.CreateDirectory(backupDir);
            Directory.CreateDirectory(gameDir);
            foreach (var f in new[] { "box.json", "box.json.bak" })
                if (File.Exists(Path.Combine(gameDir, f))) { File.Copy(Path.Combine(gameDir, f), Path.Combine(backupDir, f), true); File.Delete(Path.Combine(gameDir, f)); }
            var tel = Path.Combine(gameDir, "telemetry");
            if (Directory.Exists(tel)) Directory.Move(tel, Path.Combine(backupDir, "telemetry"));
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
                if (RecallPromptView.Instance == null) RecallPromptView.Create();
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                if (CareView.Instance == null && box != null) box.gameObject.AddComponent<CareView>(); // bootstraps run after the first scene only
            }
            yield return null; yield return null;
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
        }

        void Close()
        {
            GameClock.DebugDayOffset = 0;
            MomentPlayer.DebugHoldHurry = false;
            if (MomentPlayer.Instance != null && MomentPlayer.Instance.Playing) MomentPlayer.Instance.Leave();
            try
            {
                var tel = Path.Combine(gameDir, "telemetry");
                if (Directory.Exists(tel)) Directory.Delete(tel, true);
                foreach (var f in new[] { "box.json", "box.json.bak" })
                {
                    var b = Path.Combine(backupDir, f);
                    if (File.Exists(b)) File.Copy(b, Path.Combine(gameDir, f), true);
                    else if (File.Exists(Path.Combine(gameDir, f))) File.Delete(Path.Combine(gameDir, f));
                }
                var tb = Path.Combine(backupDir, "telemetry");
                if (Directory.Exists(tb)) Directory.Move(tb, tel);
                Directory.Delete(backupDir, true);
            }
            catch (Exception e) { Debug.LogWarning("restore: " + e.Message); }
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

        IEnumerator Drag(Vector2 a, Vector2 b, float step)
        {
            int id = touchId++;
            var p = a;
            BeginTouch(id, p, queueEventOnly: true);
            yield return null;
            while (Vector2.Distance(p, b) > 0.01f)
            {
                p = Vector2.MoveTowards(p, b, step);
                MoveTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
                yield return null;
            }
            EndTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
        }

        static IEnumerator Until(Func<bool> done, float seconds)
        {
            float t = 0f;
            while (!done() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static string[] Names()
        {
            var log = GameClock.Tel;
            log.Flush();
            return log.ReadAllLines().Select(l => TelemetryEvent.Parse(l)?.name).ToArray();
        }

        static TelemetryEvent Last(string name) => GameClock.Tel.ReadAllLines().Select(TelemetryEvent.Parse).Last(e => e != null && e.name == name);

        static void SkipFirstRun()
        {
            if (UnityEngine.Object.FindAnyObjectByType<FirstRunView>() != null) FirstRunView.DebugSkipAll();
        }

        [UnityTest]
        public IEnumerator AScriptedSessionWritesTheExpectedLines()
        {
            yield return Open();
            try
            {
                Assert.IsNotNull(GameClock.Tel, "the clock starts the event log");
                SkipFirstRun();
                Assert.AreEqual(Ev.SessionStart, Names()[0]);
                var start = TelemetryEvent.Parse(GameClock.Tel.ReadAllLines()[0]);
                Assert.AreEqual("open", start.Str("reason"));
                Assert.IsNotNull(start.Str("install_utc"));

                // a wipe by finger in clean mode
                var sim = GameClock.Instance.Sim;
                sim.AddMess(1f, 1f, sim.Config.trashMax);
                var hud = GameHud.Instance;
                hud.SetCleanMode(true);
                for (int i = 0; i < 20; i++) yield return null;
                float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
                for (int k = -2; k <= 2; k++)
                    yield return Drag(new Vector2(cx - 0.3f * Screen.width, cy + k * 0.05f * Screen.height), new Vector2(cx + 0.3f * Screen.width, cy + k * 0.05f * Screen.height), 0.02f * Screen.height);
                hud.SetCleanMode(false);
                Assert.Greater(CareView.Instance.StrokeCount, 0, "the finger wiped");
                CollectionAssert.Contains(Names(), Ev.Clean);

                // an era moment watched to the end (hurried)
                Assert.IsTrue(sim.ForceEraReady());
                yield return new WaitForSecondsRealtime(0.7f);
                var mp = MomentPlayer.Instance;
                Assert.IsTrue(mp.Watch(true));
                MomentPlayer.DebugHurry(4f);
                MomentPlayer.DebugHoldHurry = true;
                yield return Until(() => !mp.Playing, 90f);
                MomentPlayer.DebugHoldHurry = false;
                Assert.IsFalse(mp.Playing, "the moment ended");
                Assert.AreEqual("live", Last(Ev.EraMoment).Str("mode"));

                // and the next one left halfway
                sim.ForceEraReady();
                yield return new WaitForSecondsRealtime(0.7f);
                if (mp.Watch(true))
                {
                    yield return new WaitForSecondsRealtime(1f);
                    mp.Leave();
                    Assert.AreEqual("skipped", Last(Ev.EraMoment).Str("mode"));
                }

                // pause and back
                GameClock.Instance.SendMessage("OnApplicationPause", true);
                yield return null;
                GameClock.Instance.SendMessage("OnApplicationPause", false);
                yield return null;
                var all = Names();
                CollectionAssert.Contains(all, Ev.SessionEnd);
                CollectionAssert.Contains(all, Ev.CatchUp);
                Assert.AreEqual("pause", Last(Ev.SessionEnd).Str("reason"));

                var report = TelemetryAnalyzer.Analyze(GameClock.Tel.ReadAllLines(), GameClock.TelemetryNow);
                Assert.AreEqual(1, report.opens, "a short pause is not a new open");
                Assert.GreaterOrEqual(report.erasLive, 1);
                Assert.IsNotNull(report.secondsToFirstClean);
                Debug.Log("[TelemetryPlay] " + report.ToText());
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TheRecallQuestionShowsOnDayThree_NotOverAMoment()
        {
            yield return Open();
            try
            {
                SkipFirstRun();
                var view = RecallPromptView.Instance;
                view.checkSeconds = 0.2f;
                var sim = GameClock.Instance.Sim;
                GameClock.Tel.ShowMe("1"); GameClock.Tel.CardOpened("person", 1); // something to recall
                RecallPromptView.DebugReset();

                RecallPromptView.DebugSetDay(1);
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsFalse(view.IsOpen, "not before day 3");

                // day 3, but a moment plays: no
                RecallPromptView.DebugSetDay(3);
                Assert.IsTrue(sim.ForceEraReady());
                yield return new WaitForSecondsRealtime(0.7f);
                var mp = MomentPlayer.Instance;
                Assert.IsTrue(mp.Watch(true));
                yield return Until(() => GameClock.Instance.SessionSeconds > RecallPrompt.MinSessionSeconds + 1, 40f);
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsTrue(mp.Playing, "the moment is still on");
                Assert.IsFalse(view.IsOpen, "never over an era moment");
                mp.Leave();

                // calm: yes
                yield return Until(() => view.IsOpen, 8f);
                Assert.IsTrue(view.IsOpen, "day 3 and calm: the question shows");
                CollectionAssert.Contains(Names(), Ev.RecallShown);

                // answer by touch
                yield return null;
                var c = GameUiPanel.ScreenCenter(view.Root.Q("recall-2"));
                BeginTouch(touchId, c, queueEventOnly: true); yield return null; yield return null;
                EndTouch(touchId, c, delta: Vector2.zero, queueEventOnly: true); yield return null; yield return null;
                Assert.IsFalse(view.IsOpen, "answering closes it");
                Assert.AreEqual(2, Last(Ev.RecallAnswered).Num("count"));
                Assert.IsTrue(view.State.answered);
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.IsFalse(view.IsOpen, "never asked again once answered");
            }
            finally { Close(); }
        }
    }
}
