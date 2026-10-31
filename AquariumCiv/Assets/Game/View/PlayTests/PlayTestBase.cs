using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Every start-up bootstrap of the game, for a scene loaded by a test. Unity runs them after the first scene only, so a
    /// scene loaded in play mode gets them by hand; a test that hand-picks a list misses views, and a bug that needs two of
    /// them together (tester batch 3: the first run cleared the shaping tool) never shows. Found by reflection, so a new
    /// bootstrap joins by itself.
    /// </summary>
    public static class Bootstraps
    {
        /// <summary>The [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] methods in the game's assemblies.</summary>
        public static List<MethodInfo> Find()
        {
            var list = new List<MethodInfo>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = a.GetName().Name;
                if (!name.StartsWith("AquariumCiv.") || name.Contains("Tests")) continue;
                foreach (var t in a.GetTypes())
                    foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        var at = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                        if (at != null && at.loadType == RuntimeInitializeLoadType.AfterSceneLoad) list.Add(m);
                    }
            }
            return list;
        }

        /// <summary>Runs them all (each guards against a second copy), plus the time-lapse, which is made on demand.
        /// Returns how many ran.</summary>
        public static int All()
        {
            var list = Find();
            foreach (var m in list) m.Invoke(null, null);
            TimeLapseView.Ensure();
            return list.Count;
        }
    }

    /// <summary>
    /// The shared harness for play tests on the real Slice scene at a portrait phone size: unfocused input, a pinned seed,
    /// the player's save backed up and put back, every bootstrap, touch helpers, a list of console errors, and at teardown
    /// the leak check (<see cref="LeakCheck"/>). One InputTestFixture class per run, as ever.
    /// </summary>
    public abstract class PlayTestBase : InputTestFixture
    {
        protected const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        protected const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
        protected Touchscreen touch;
        protected int touchId = 1;
        protected readonly List<string> errors = new();
        protected string savePath;
        /// <summary>How many bootstraps the last <see cref="OpenSlice"/> ran.</summary>
        protected int bootstrapsRun;
        string backup;
        bool soundWas, opened;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) errors.Add(msg);
        }

        /// <summary>Loads Slice with a new box (or the save on disk, <paramref name="freshSave"/> false) and every bootstrap.
        /// <paramref name="seed"/> pins the world so a failure replays (0 = a random box).</summary>
        protected IEnumerator OpenSlice(int w = 1179, int h = 2556, int seed = 0, bool freshSave = true, bool skipFirstRun = true)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            backup = savePath + ".playtest-backup";
            if (!opened)
            {
                foreach (var ext in new[] { "", ".bak" })
                    if (File.Exists(savePath + ext)) File.Copy(savePath + ext, backup + ext, true);
                soundWas = GameSettings.Sound;
                opened = true;
            }
            if (freshSave)
                foreach (var ext in new[] { "", ".bak" })
                    if (File.Exists(savePath + ext)) File.Delete(savePath + ext);
            if (seed != 0) GameClock.DebugNextSeed = seed;
            SetSize(w, h);
            float waited = 0f;
            while ((Screen.width != w || Screen.height != h) && waited < 4f) { waited += 0.1f; yield return new WaitForSecondsRealtime(0.1f); }
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            for (int i = 0; i < 90; i++) yield return null; // build, layout, the Game view size lands
            if (GameClock.Instance != null) bootstrapsRun = Bootstraps.All();
            yield return null; yield return null;
            if (skipFirstRun && FirstRunView.Instance != null && FirstRunView.Instance.Active) FirstRunView.DebugSkipAll();
            yield return null; yield return null;
            if (touch == null) touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
            errors.Clear();
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        /// <summary>Puts the save and the settings back. Safe to call twice.</summary>
        protected void CloseSlice()
        {
            Application.logMessageReceived -= OnLog;
            var pv = PeopleView.Find();
            if (pv != null) { pv.timeScale = 1f; pv.Planner?.ClearLive(); }
            MomentPlayer.DebugHoldHurry = false;
            GameClock.DebugNextSeed = 0;
            if (!opened) return;
            opened = false;
            GameSettings.Sound = soundWas;
            try
            {
                foreach (var ext in new[] { "", ".bak" })
                {
                    if (File.Exists(backup + ext)) { File.Copy(backup + ext, savePath + ext, true); File.Delete(backup + ext); }
                    else if (File.Exists(savePath + ext)) File.Delete(savePath + ext);
                }
            }
            catch (Exception e) { Debug.LogWarning("restore save: " + e.Message); }
        }

        /// <summary>Whatever a test did, the save goes back and no box may have leaked.</summary>
        public override void TearDown()
        {
            CloseSlice();
            base.TearDown();
            LeakCheck.AssertClean();
        }

        // ---------------- helpers ----------------
        protected static void SetSize(int w, int h)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("AquariumCiv.EditorTools.AgentGameView");
                if (t == null) continue;
                t.GetMethod("SetSize").Invoke(null, new object[] { w, h });
                return;
            }
        }

        protected static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Shots);
            File.WriteAllBytes(Shots + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        protected IEnumerator TapAt(Vector2 p)
        {
            BeginTouch(touchId, p, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        protected IEnumerator Drag(Vector2[] path, float step)
        {
            int id = touchId++;
            var p = path[0];
            BeginTouch(id, p, queueEventOnly: true);
            yield return null;
            for (int i = 1; i < path.Length; i++)
                while (Vector2.Distance(p, path[i]) > 0.01f)
                {
                    p = Vector2.MoveTowards(p, path[i], step);
                    MoveTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
                    yield return null;
                }
            EndTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
        }

        protected static IEnumerator Until(Func<bool> done, float seconds)
        {
            float t = 0f;
            while (!done() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        protected static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Fails on any console error since <see cref="OpenSlice"/>.</summary>
        protected void AssertNoErrors(string when) => Assert.IsEmpty(errors, $"{when}: {string.Join(" | ", errors.Take(5))}");
    }

    /// <summary>
    /// The 81-box leak must fail fast. DontSave objects outlive a scene, parentless and sceneless, and keep rendering: the
    /// panes hazed the glass, and the dolls left props with nobody holding them (bodies die with the scene, the shared
    /// prop meshes don't). The rule: at most one <c>Generated</c> root, no parentless DontSave root outside a scene (the
    /// engine's own hidden objects are NotEditable and the test runner's object doesn't count), no prop on a bodiless doll.
    /// </summary>
    public static class LeakCheck
    {
        static IEnumerable<Transform> SceneObjects() => Resources.FindObjectsOfTypeAll<Transform>().Where(t => t != null
#if UNITY_EDITOR
            && !UnityEditor.EditorUtility.IsPersistent(t)
#endif
        );

        public static List<string> Orphans() => SceneObjects()
            .Where(t => t.parent == null && !t.gameObject.scene.IsValid() && t.name != "Code-based tests runner"
                && (t.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave && (t.gameObject.hideFlags & HideFlags.NotEditable) == 0)
            .Select(t => t.name).ToList();

        public static int GeneratedRoots() => SceneObjects().Count(t => t.name == "Generated");

        /// <summary>Props whose doll lost its body: what the leaked dolls leave standing in the town.</summary>
        public static List<string> BodilessProps() => SceneObjects()
            .Select(t => t.GetComponent<PegDollView>())
            .Where(d => d != null && d.prop != null && (d.body == null || d.body.GetComponent<MeshFilter>().sharedMesh == null))
            .Select(d => d.prop.name).ToList();

        public static void AssertClean()
        {
            Assert.IsEmpty(Orphans(), "leaked parentless DontSave roots");
            Assert.IsEmpty(BodilessProps(), "props with nobody holding them");
            Assert.LessOrEqual(GeneratedRoots(), 1, "more than one Generated root: a box leaked");
        }
    }
}
