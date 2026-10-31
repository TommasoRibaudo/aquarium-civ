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
    /// The first run (#94) driven by touch in the real Slice scene at a portrait phone size on a brand-new box: wipe, flick
    /// the weeds, keep a name, place the rain, the year plays, the card, Done, normal play. Also: killed after the name and
    /// resumed at the right step; never blocks (an untouched plate keeps its name, Skip all). Backs up and restores the
    /// slice's save. Run one test per Editor run (one InputTestFixture class per run).
    /// </summary>
    public class FirstRunPlayTests : InputTestFixture
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
        Touchscreen touch;
        int touchId = 1;
        string savePath, backup;
        bool soundWas;

        // ---------------- harness ----------------
        IEnumerator Open(bool deleteSave = true, int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            backup = savePath + ".firstrun-test-backup";
            if (backup != null && !File.Exists(backup + ".marker"))
            {
                if (File.Exists(savePath)) File.Copy(savePath, backup, true);
                if (File.Exists(savePath + ".bak")) File.Copy(savePath + ".bak", backup + ".bak", true);
                File.WriteAllText(backup + ".marker", "x");
            }
            if (deleteSave)
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
            }
            soundWas = GameSettings.Sound;
            SetSize(w, h);
            float waited = 0f;
            while ((Screen.width != w || Screen.height != h) && waited < 4f) { waited += 0.1f; yield return new WaitForSecondsRealtime(0.1f); }
            yield return LoadScene();
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
        }

        IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            for (int i = 0; i < 90; i++) yield return null;
            // The start-up bootstraps run after the first scene only, so a scene loaded in play mode gets them by hand.
            var b = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            if (GameHud.Instance == null) GameHud.Create();
            if (b != null && CareView.Instance == null) b.gameObject.AddComponent<CareView>();
            if (b != null && ShapingView.Instance == null) b.gameObject.AddComponent<ShapingView>();
            if (b != null && ShowMeDirector.Instance == null) b.gameObject.AddComponent<ShowMeDirector>();
            if (FirstRunView.Instance == null) FirstRunView.Create();
            for (int i = 0; i < 6; i++) yield return null;
        }

        void Close()
        {
            GameSettings.Sound = soundWas;
            try
            {
                if (File.Exists(backup + ".marker"))
                {
                    if (File.Exists(backup + ".bak")) { File.Copy(backup + ".bak", savePath + ".bak", true); File.Delete(backup + ".bak"); }
                    if (File.Exists(backup)) { File.Copy(backup, savePath, true); File.Delete(backup); }
                    else if (File.Exists(savePath)) File.Delete(savePath);
                    File.Delete(backup + ".marker");
                }
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
            File.WriteAllBytes(Shots + "firstrun_" + name + ".png", tex.EncodeToPNG());
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

        IEnumerator Drag(Vector2[] path, float step)
        {
            int id = touchId++;
            var p = path[0];
            BeginTouch(id, p, queueEventOnly: true);
            yield return null;
            for (int i = 1; i < path.Length; i++)
            {
                while (Vector2.Distance(p, path[i]) > 0.01f)
                {
                    p = Vector2.MoveTowards(p, path[i], step);
                    MoveTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
                    yield return null;
                }
            }
            EndTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
        }

        IEnumerator WaitFor(Func<bool> cond, float seconds)
        {
            float t = 0f;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static SimCore Sim => GameClock.Instance.ActiveSim;
        static FirstRunView View => FirstRunView.Instance;
        static Camera Cam => Camera.main;

        static System.Collections.Generic.List<Vector2[]> RowStrokes(CareView care)
        {
            var list = new System.Collections.Generic.List<Vector2[]>();
            float spacing = care.wipeRadius * Screen.height * 1.2f;
            bool right = true;
            for (float y = Screen.height * 0.9f; y > Screen.height * 0.12f; y -= spacing)
            {
                float first = -1f, last = -1f;
                for (float x = 8f; x < Screen.width - 8f; x += 12f)
                {
                    if (!care.GlassAt(new Vector2(x, y))) continue;
                    if (first < 0f) first = x;
                    last = x;
                }
                if (first < 0f) continue;
                list.Add(right ? new[] { new Vector2(first, y), new Vector2(last, y) } : new[] { new Vector2(last, y), new Vector2(first, y) });
                right = !right;
            }
            return list;
        }

        IEnumerator WipeTheGlass()
        {
            var care = CareView.Instance;
            var rows = RowStrokes(care);
            int rounds = 0;
            while (View.Step == FirstRunStep.Wipe && rounds < 5)
            {
                foreach (var row in rows)
                {
                    if (View.Step != FirstRunStep.Wipe) break;
                    yield return Drag(row, 0.02f * Screen.height);
                }
                rounds++;
            }
        }

        IEnumerator FlickTheWeeds()
        {
            var care = CareView.Instance;
            int guard = 0;
            while (View.Step == FirstRunStep.Patch && guard++ < 14)
            {
                var piece = care.Props.AllPieces.FirstOrDefault(p => !p.leaving && p.weed);
                if (piece == null) { yield return null; continue; }
                var sp = (Vector2)Cam.WorldToScreenPoint(piece.Center);
                yield return Drag(new[] { sp, sp + new Vector2(0.01f, 0.08f) * Screen.height }, 0.045f * Screen.height);
                for (int i = 0; i < 4; i++) yield return null;
            }
        }

        // ---------------- tests ----------------
        [UnityTest]
        public IEnumerator FreshBox_FullRunByTouch_ReachesNormalPlay()
        {
            yield return Open();
            try
            {
                Assert.IsNotNull(View, "the first-run view exists");
                Assert.IsTrue(View.Active, "a brand-new box starts the first run");
                var sim = Sim;
                var hud = GameHud.Instance;
                Assert.GreaterOrEqual(sim.State.grime, FirstRun.StartGrime);
                Assert.GreaterOrEqual(sim.State.overgrowth, FirstRun.StartOvergrowth);
                float t0 = Time.realtimeSinceStartup;

                // Arrive, then Wipe with the finger ghost.
                yield return WaitFor(() => View.Step == FirstRunStep.Wipe, 6f);
                Assert.AreEqual(FirstRunStep.Wipe, View.Step);
                yield return new WaitForSecondsRealtime(2f);
                Assert.AreEqual("wipe", View.ShownHintId, "the wipe hint shows");
                Assert.LessOrEqual(View.ShownHintText.Split(' ').Length, 3);
                Assert.IsTrue(hud.CleanMode, "one finger wipes: clean mode is on for the player");
                yield return Shot("1_wipe_hint");
                yield return WipeTheGlass();
                for (int i = 0; i < 20; i++) yield return null;
                Assert.AreEqual(FirstRunStep.Patch, View.Step, "the clear glass moves the run to the patch");
                Assert.IsTrue(CareView.Instance.Props.WeedCount >= 3, "the overgrown patch is drawn");
                yield return new WaitForSecondsRealtime(1.8f);
                yield return Shot("2_patch_hint");
                yield return FlickTheWeeds();
                for (int i = 0; i < 20; i++) yield return null;
                Assert.AreEqual(FirstRunStep.Name, View.Step, "the cleared patch moves the run to the plate");
                Assert.LessOrEqual(sim.State.overgrowth, FirstRun.ClearedOvergrowth);

                // Name: the plate, the sheet, a refused name, a kept one.
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("3_plate");
                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsTrue(View.NameSheetOpen, "tapping the plate opens the naming sheet");
                var field = View.Root.Q<TextField>("firstrun-name-field");
                Assert.IsFalse(string.IsNullOrEmpty(field.value), "a suggested name is already in the field");
                field.value = "shit hole";
                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-name-keep"));
                Assert.AreEqual(FirstRunStep.Name, View.Step, "a refused name keeps the run at the plate");
                Assert.IsFalse(string.IsNullOrEmpty(View.NameMessage), "and says why");
                yield return Shot("4_name_refused");
                field.value = "Mossbarrow";
                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-name-keep"));
                for (int i = 0; i < 10; i++) yield return null;
                Assert.AreEqual("Mossbarrow", sim.PlaceName);
                Assert.AreEqual(FirstRunStep.Rain, View.Step);
                Assert.IsFalse(View.NameSheetOpen);

                // Rain: the wish bubble, the button, the ground.
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot("5_rain_hint");
                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-rain"));
                yield return new WaitForSecondsRealtime(1f);
                Assert.IsTrue(ShapingView.Instance.ShapeMode, "the rain button picks the rain");
                yield return Shot("6_rain_armed");
                var toastEl = hud.Root.Q("hud-toast");
                Assert.IsFalse(toastEl.resolvedStyle.display == DisplayStyle.Flex && toastEl.worldBound.Overlaps(View.Root.Q("firstrun-rain").worldBound), "the button and the toast keep to their lanes");
                var (rx, rz) = View.Run.SuggestedRainSpot;
                var rainPt = ShapingView.Instance.ScreenOf(rx, rz);
                Debug.Log($"[firstrun] tapping the ground at {rainPt}, over UI: {UiInput.Over(rainPt)}, ground: {ShapingView.Instance.GroundPoint(rainPt, out var gx, out var gz)} ({gx:0.00},{gz:0.00}) vs ({rx:0.00},{rz:0.00}), charges {Sim.ShapingCharges}, cam dist {Vector3.Distance(Camera.main.transform.position, ShapingView.Instance.transform.position):0.0}");
                yield return TapAt(ShapingView.Instance.ScreenOf(rx, rz));
                Debug.Log($"[firstrun] after the tap: {ShapingView.Instance.LastResult} step {View.Step}");
                for (int i = 0; i < 10; i++) yield return null;
                Assert.AreEqual(FirstRunStep.Year, View.Step, "a rain placed by touch moves the run to the year");
                Assert.IsTrue(sim.State.nudges.Any(n => n.kind == SimCore.NudgeKind.Rain));
                Assert.IsTrue(GameClock.Instance.Held, "the live clock is held while the year plays");
                // Tester batch 3: the year's fast clock made the dolls jump at every skip.
                var boxView = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var lastPos = new System.Collections.Generic.Dictionary<PegDollView, Vector3>();
                float yearT = 0f, worstStep = 0f;
                while (yearT < 8f)
                {
                    yield return null;
                    yearT += Time.unscaledDeltaTime;
                    float allow = Mathf.Max(1f, Time.unscaledDeltaTime / 0.1f);
                    foreach (var d in boxView.GetComponentsInChildren<PegDollView>(false))
                    {
                        var p = d.transform.localPosition;
                        if (lastPos.TryGetValue(d, out var q)) worstStep = Mathf.Max(worstStep, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)) / allow);
                        lastPos[d] = p;
                    }
                }
                Debug.Log($"[firstrun] worst doll step in the year {worstStep:0.00} u");
                Assert.Less(worstStep, 0.25f, "the dolls walk through the first year");
                yield return Shot("7_year");

                // The year plays; the card.
                yield return WaitFor(() => View.Step == FirstRunStep.Card, 25f);
                Assert.AreEqual(FirstRunStep.Card, View.Step, "the year ends on the card");
                yield return null; yield return null;
                Assert.IsFalse(GameClock.Instance.Held);
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.IsTrue(View.CardOpen);
                Assert.IsTrue(View.Summary.rainAnswered, "the rain was answered inside the year");
                yield return Shot("8_card");
                float run = Time.realtimeSinceStartup - t0;

                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-card-showme"));
                yield return new WaitForSecondsRealtime(3f);
                yield return Shot("9_showme");
                yield return WaitFor(() => View.CardOpen, 12f);
                Assert.IsTrue(View.CardOpen, "the card comes back after Show me");
                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-card-done"));
                for (int i = 0; i < 20; i++) yield return null;
                Assert.IsFalse(View.Active, "Done ends the run");
                Assert.IsFalse(View.CardOpen);
                Assert.IsFalse(hud.ToolsHidden, "the tools are back");
                Assert.IsFalse(GameClock.Instance.Held);
                Assert.AreEqual("", View.ShownHintId);

                // Normal play again: the Clean button works, the HUD sheet opens, the name is on the HUD.
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                Assert.IsTrue(hud.CleanMode);
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-clean"));
                Assert.IsFalse(hud.CleanMode);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("10_normal_play");
                Assert.AreEqual(FirstRunStep.Done, (FirstRunStep)sim.State.firstRun.step);
                Debug.Log($"[firstrun] whole run by touch, {run:0} s of play here (hints and waits included)");
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator KilledAfterTheName_ResumesAtTheRainStep()
        {
            yield return Open();
            try
            {
                yield return WaitFor(() => View.Step == FirstRunStep.Wipe, 6f);
                View.Run.DebugSkipTo(FirstRunStep.Name);
                for (int i = 0; i < 6; i++) yield return null;
                Assert.AreEqual(FirstRunStep.Name, View.Step);
                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-plate"));
                yield return new WaitForSecondsRealtime(0.3f);
                View.Root.Q<TextField>("firstrun-name-field").value = "Lanternfold";
                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-name-keep"));
                for (int i = 0; i < 10; i++) yield return null;
                Assert.AreEqual(FirstRunStep.Rain, View.Step);
                Assert.IsTrue(GameClock.Instance.Save(), "saved");
                Assert.IsTrue(File.Exists(savePath));

                // "Kill": drop the scene and every view, then open the box again from the save.
                var wishId = View.Run.RainWish?.id ?? -1;
                yield return LoadScene();
                Assert.IsFalse(GameClock.Instance.Session.newWorld, "the second open loads the save");
                Assert.IsNotNull(View);
                Assert.IsTrue(View.Active, "the run resumes");
                Assert.AreEqual(FirstRunStep.Rain, View.Step, "at the rain step, not the start");
                Assert.AreEqual("Lanternfold", Sim.PlaceName);
                Assert.AreEqual(wishId, View.Run.RainWish?.id ?? -1, "with the same wish");
                yield return new WaitForSecondsRealtime(2.5f);
                Assert.IsTrue(View.Root.Q("firstrun-rain").resolvedStyle.display == DisplayStyle.Flex, "the rain button is up");
                Assert.AreEqual("rain-tap", View.ShownHintId, "and the finger points at it");
                yield return Shot("11_resumed_rain");
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator NeverBlocks_UntouchedPlateKeepsItsName_AndSkipAllEndsTheRun()
        {
            yield return Open();
            try
            {
                var sim = Sim;
                yield return WaitFor(() => View.Step == FirstRunStep.Wipe, 6f);
                // Do something else instead: the HUD works, sheets open, nothing is locked.
                var hud = GameHud.Instance;
                yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
                Assert.AreEqual(GameHud.History, hud.OpenSheet, "the HUD still opens its sheets mid-run");
                hud.CloseSheet();
                View.Run.DebugSkipTo(FirstRunStep.Name);
                for (int i = 0; i < 6; i++) yield return null;
                Assert.AreEqual(FirstRunStep.Name, View.Step);
                var suggestion = View.Run.SuggestedName;
                // Leave the plate alone: the director keeps the suggestion after its idle wait (fast-forwarded here).
                sim.State.firstRun.untouchedSeconds = FirstRun.NameAutoKeepSeconds - 1.5;
                yield return new WaitForSecondsRealtime(3f);
                Assert.AreEqual(FirstRunStep.Rain, View.Step, "an untouched plate does not hold the run up");
                Assert.AreEqual(suggestion, sim.PlaceName);
                yield return Shot("12_auto_kept");
                // Skip all.
                yield return TapAt(GameUiPanel.ScreenCenter(View.Root, "firstrun-skip-all"));
                for (int i = 0; i < 10; i++) yield return null;
                Assert.IsFalse(View.Active, "Skip all ends the run at once");
                Assert.IsFalse(hud.ToolsHidden);
                Assert.IsFalse(GameClock.Instance.Held);
                yield return Shot("13_skipped");
            }
            finally { Close(); }
        }
    }
}
