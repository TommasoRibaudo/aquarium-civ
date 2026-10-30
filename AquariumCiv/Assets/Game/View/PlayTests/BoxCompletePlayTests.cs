using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #93 in the real Slice scene at a portrait phone size: a grown town reaches its peak, the prompt shows, the last moment
    /// plays (hurried) through the crossing where the box completes, box 2 arrives, the shelf view lists both boxes, a museum
    /// visit shows its plate and plinths, back restores the active box exactly, and the file on disk holds both boxes.
    /// Screenshots go to Temp/agent (bc_*.png). Backs up and restores the save file.
    /// </summary>
    [Category("BoxCompletePlay")]
    public class BoxCompletePlayTests
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
        string savePath, backup;
        readonly List<string> errors = new();

        void OnLog(string msg, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error) errors.Add(msg); }

        IEnumerator Open(int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            savePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            backup = savePath + ".boxcomplete-test-backup";
            if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
            if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
            SetSize(w, h);
            float waited = 0f;
            while ((Screen.width != w || Screen.height != h) && waited < 4f) { waited += 0.1f; yield return new WaitForSecondsRealtime(0.1f); }
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            for (int i = 0; i < 90; i++) yield return null;
            Assume.That(GameClock.Instance, Is.Not.Null);
            errors.Clear();
            Application.logMessageReceived += OnLog;
        }

        void Close()
        {
            Application.logMessageReceived -= OnLog;
            MomentPlayer.DebugHoldHurry = false;
            try
            {
                if (File.Exists(backup + ".bak")) { File.Copy(backup + ".bak", savePath + ".bak", true); File.Delete(backup + ".bak"); }
                else if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
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

        static string Stable(SimState st) => System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(st), "\"simHours\":[0-9.eE+-]+", "");

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        static IEnumerator Grow(int years)
        {
            for (int i = 0; i < years; i++)
            {
                SimLayoutSource.DebugSkipYears(1f);
                if (i % 10 == 0) yield return null;
            }
            GameClock.Instance.ActiveSim.CleanAll();
            yield return Frames(2);
        }

        static IEnumerator ToTown()
        {
            yield return Grow(60);
            var sim = GameClock.Instance.ActiveSim;
            if (sim.State.era != Era.Town) { sim.ForceNextEra(); yield return Frames(3); }
            Assert.AreEqual(Era.Town, sim.State.era);
            sim.ForcePeakReady();
            yield return Frames(3);
        }

        static IEnumerator WaitBeat(BoxCompleteController c, string beat, string shot, int extraFrames)
        {
            float t = 0f;
            while (c.Playing && c.Director.CurrentBeat?.id != beat && t < 120f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(beat, c.Director.CurrentBeat?.id, "reached beat " + beat);
            yield return Frames(extraFrames);
            if (shot != null) yield return Shot(shot);
        }

        [UnityTest]
        public IEnumerator TheBoxCompletesBoxTwoArrivesAndAMuseumCanBeVisited()
        {
            yield return Open();
            yield return ToTown();
            var clock = GameClock.Instance;
            var c = BoxCompleteController.Instance ?? BoxCompleteController.Create();
            Assert.IsNotNull(c, "the controller is in the scene");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shot("bc_1_prompt");
            Assert.IsTrue(c.Begin(), "the player can finish the box");
            Assert.IsFalse(clock.ActiveSim.State.boxComplete, "nothing moves before the crossing");
            c.Director.HurryFactor = 3f;
            MomentPlayer.DebugHoldHurry = true;
            yield return WaitBeat(c, "lookback", "bc_2_lookback", 40);
            yield return WaitBeat(c, "lanterns", "bc_3_lanterns", 150);
            yield return WaitBeat(c, "founders", "bc_4_founders", 30);
            yield return WaitBeat(c, "shelf", "bc_5_shelf", 120);
            Assert.IsFalse(c.Committed);
            yield return WaitBeat(c, "crossing", null, 5);
            Assert.IsTrue(c.Committed, "the box completed at the crossing");
            Assert.AreEqual(2, clock.Shelf.boxes.Count);
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(savePath, out var env));
            Assert.AreEqual(2, env.shelf.boxes.Count, "both boxes are in the file the moment it completes");
            yield return Frames(120);
            yield return Shot("bc_6_crossing");
            yield return WaitBeat(c, "wipe", "bc_7_wipe", 60);
            yield return WaitBeat(c, "plate", "bc_8_plate_start", 5);
            yield return WaitBeat(c, "rollcall", "bc_9_plate_done_roll", 40);
            yield return WaitBeat(c, "newbox", null, 5);
            float t = 0f;
            while (c.Playing && t < 60f) { t += Time.unscaledDeltaTime; yield return null; }
            MomentPlayer.DebugHoldHurry = false;
            yield return Frames(30);
            Assert.IsFalse(c.Playing);
            Assert.IsFalse(clock.Visiting, "box 2 is on screen");
            Assert.AreSame(clock.Session.sim, clock.Sim);
            Assert.AreEqual(1, clock.Shelf.activeIndex);
            Assert.IsFalse(clock.Sim.State.boxComplete);
            Assert.IsFalse(clock.Held);
            yield return Frames(20);
            var bv = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            Assert.IsTrue(bv.layout.pieces.Count > 0 && bv.layout.pieces.All(p => p.era == Era.Camp), "box 2 is drawn as a camp, not the old town");
            yield return Shot("bc_10_box2");
            string box2Before = Stable(clock.ActiveSim.State);

            Assert.IsTrue(c.OpenShelf());
            yield return Frames(30);
            yield return Shot("bc_11_shelf");
            Assert.IsTrue(c.PickBox(0));
            Assert.IsTrue(clock.Visiting);
            Assert.IsTrue(clock.Sim.State.boxComplete);
            yield return Frames(40);
            Assert.IsTrue(UnityEngine.Object.FindAnyObjectByType<BoxView>().layout.pieces.Any(p => p.era == Era.Town), "the museum is drawn as the town it was");
            yield return Shot("bc_12_visit");
            var props = UnityEngine.Object.FindAnyObjectByType<BoxView>().GetComponent<ShelfProps>();
            Assert.IsTrue(props.Dressed, "the museum wears its plate, lamp and plinths");
            Assert.IsTrue(props.PlateFull.Length > 5, "the plate reads: " + props.PlateFull);
            Assert.IsTrue(props.LampLit, "the museum lamp is lit");
            Assert.IsTrue(clock.Save());
            SaveStore.Read(savePath, out env);
            Assert.AreEqual(1, env.shelf.activeIndex, "a save while visiting writes the active box");
            c.Back();
            yield return Frames(30);
            Assert.IsFalse(clock.Visiting);
            Assert.AreEqual(box2Before, Stable(clock.ActiveSim.State), "box 2 is exactly as it was (it lives on in the background; only the sub-month clock moves)");
            Assert.IsTrue(UnityEngine.Object.FindAnyObjectByType<BoxView>().layout.pieces.All(p => p.era == Era.Camp), "back on box 2: the camp");
            yield return Shot("bc_13_back_on_box2");
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Close();
        }

        [UnityTest]
        public IEnumerator LeavingBeforeTheCrossingChangesNothingAndItReplays()
        {
            yield return Open();
            yield return ToTown();
            var clock = GameClock.Instance;
            var c = BoxCompleteController.Instance ?? BoxCompleteController.Create();
            Assert.IsTrue(c.Begin());
            c.Director.HurryFactor = 6f;
            MomentPlayer.DebugHoldHurry = true;
            yield return WaitBeat(c, "lanterns", null, 5);
            Assert.AreEqual(MomentDirector.LeaveResult.Replays, c.Leave());
            MomentPlayer.DebugHoldHurry = false;
            yield return Frames(5);
            Assert.IsFalse(c.Committed);
            Assert.AreEqual(1, clock.Shelf.boxes.Count);
            Assert.IsFalse(clock.ActiveSim.State.boxComplete);
            Assert.IsFalse(clock.Held);
            Assert.IsTrue(c.Begin(), "it plays again");
            c.Leave();
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Close();
        }
    }
}
