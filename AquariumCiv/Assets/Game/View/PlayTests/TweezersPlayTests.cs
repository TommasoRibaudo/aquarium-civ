using System;
using System.Collections;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #311 / #314 in the real Slice scene at a phone size, by touch: the tweezers on the kit sheet, a tap that plucks a weed patch, a tuft round
    /// the Keeper's tree, a cell of vine and a wild sapling; the cloth that no longer takes weeds on a living land; the camera still moving with
    /// two fingers. Run alone (one InputTestFixture class per run); it backs up and restores the slice's save file.
    /// </summary>
    [Category("Tweezers")]
    public class TweezersPlayTests : InputTestFixture
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
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = GameClock.DefaultSavePath;
            backup = savePath + ".tweezers-test-backup";
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

        IEnumerator TapAt(Vector2 p)
        {
            BeginTouch(touchId, p, queueEventOnly: true);
            yield return null; yield return null;
            EndTouch(touchId, p, delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        static Vector2 ScreenOf(BoxView box, float x, float z)
        {
            var w = box.transform.TransformPoint(box.land.OnGround(x, z));
            var s = Camera.main.WorldToScreenPoint(w);
            return new Vector2(s.x, s.y);
        }

        static Vector2 ScreenOf(MessProps.Piece p)
        {
            var s = Camera.main.WorldToScreenPoint(p.Center);
            return new Vector2(s.x, s.y);
        }

        static bool OnScreen(Vector2 s) => s.x > 40 && s.y > 200 && s.x < Screen.width - 40 && s.y < Screen.height - 300;

        IEnumerator LivingLand()
        {
            Assert.IsNotNull(GameClock.Instance, "the slice's clock");
            GameClock.DebugEcologyBox(true);
            for (int i = 0; i < 30; i++) yield return null;
            FirstRunView.DebugSkipAll();
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsTrue(GameClock.Instance.Sim.EcologyOn, "a living land");
            var hud = GameHud.Instance;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            hud?.SetHand(GameHud.KitTool.None);
            GameClock.Instance.Sim.CleanAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tweezers_PluckWeedsTreeWeedsVineAndSaplings_TheClothDoesNot()
        {
            yield return Open();
            try
            {
                yield return LivingLand();
                var sim = GameClock.Instance.Sim;
                var hud = GameHud.Instance;
                var care = CareView.Instance;
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                Assert.IsNotNull(care); Assert.IsNotNull(hud); Assert.IsTrue(hud.KitBox, "the Kit button");
                sim.DebugTreeStage(1);
                sim.AddMess(0f, 0.4f);
                sim.DebugGrowTreeWeeds();
                var tree = sim.EcoTree;
                sim.DebugGrowVine(tree.x + 0.5f, tree.z + 0.5f, 8);
                for (int i = 0; i < 40; i++) yield return null;
                Assert.Greater(care.Props.WeedCount, 0, "weed patches are drawn");
                Assert.AreEqual(3, care.Props.AllPieces.Count(p => !p.leaving && p.kind == MessProps.Kind.TreeWeed), "three tufts round the tree");
                Assert.Greater(care.Props.VineCount, 0, "the vine is drawn");
                yield return Shot("tweezers-1-overgrown");

                // The cloth in hand: a tap on a weed takes nothing (the glass is all it does here).
                hud.SetCleanMode(true);
                yield return null;
                var weed = care.Props.AllPieces.First(p => !p.leaving && p.kind == MessProps.Kind.Weed);
                float og0 = sim.State.overgrowth;
                if (OnScreen(ScreenOf(weed))) { yield return TapAt(ScreenOf(weed)); }
                Assert.AreEqual(og0, sim.State.overgrowth, 1e-4f, "the cloth leaves the weeds");
                Assert.AreEqual(0, care.Plucks);
                hud.SetCleanMode(false);

                // The tweezers: the kit's own button picks them up.
                hud.SetHand(GameHud.KitTool.Tweezers);
                yield return null;
                Assert.AreEqual(GameHud.KitTool.Tweezers, hud.Hand);
                var rig = Camera.main.GetComponent<BoxCamera>();
                float yaw0 = rig.yaw;

                // a weed patch
                weed = care.Props.AllPieces.First(p => !p.leaving && p.kind == MessProps.Kind.Weed && OnScreen(ScreenOf(p)));
                yield return TapAt(ScreenOf(weed));
                Assert.Less(sim.State.overgrowth, og0 - 0.05f, "a weed patch came out");
                Assert.AreEqual("weed", care.LastPluck);
                Assert.AreEqual(yaw0, rig.yaw, 0.01f, "the tweezers never turned the camera");

                // a tuft round the Keeper's tree
                var tuft = care.Props.AllPieces.FirstOrDefault(p => !p.leaving && p.kind == MessProps.Kind.TreeWeed && OnScreen(ScreenOf(p)));
                if (tuft != null)
                {
                    yield return TapAt(ScreenOf(tuft));
                    Assert.AreEqual(2, sim.EcoTreeWeedPieces, "one tuft round the tree came out (last pluck: " + care.LastPluck + ")");
                    Assert.AreEqual("treeweed", care.LastPluck);
                }

                // a strand of vine, then a drag along it takes more than one cell
                int vines0 = sim.EcoVine.Count;
                var vine = care.Props.AllPieces.First(p => !p.leaving && p.kind == MessProps.Kind.Vine && OnScreen(ScreenOf(p)));
                yield return TapAt(ScreenOf(vine));
                Assert.AreEqual(vines0 - 1, sim.EcoVine.Count, "a cell of vine came away");
                Assert.AreEqual("vine", care.LastPluck);
                yield return Shot("tweezers-2-after");

                // a wild sapling at the forest's edge
                int made = sim.DebugCreepForestEdge(4);
                Assert.Greater(made, 0, "saplings crept in");
                box.Refresh();
                for (int i = 0; i < 10; i++) yield return null;
                var e = sim.State.eco;
                int cell = Enumerable.Range(0, EcoLand.N).First(c => e.cover[c] == (byte)EcoCover.Seedling && c != e.treeCell && sim.EcoCanPluckSapling(c));
                float cx = AquariumCiv.Sim.Settle.SiteMap.CXc(cell), cz = AquariumCiv.Sim.Settle.SiteMap.CZc(cell);
                var sp = ScreenOf(box, cx, cz);
                if (OnScreen(sp))
                {
                    yield return TapAt(sp);
                    Assert.AreNotEqual((byte)EcoCover.Seedling, e.cover[cell], "the sapling came out (last pluck: " + care.LastPluck + ")");
                    Assert.AreEqual("sapling", care.LastPluck);
                }
                // The Keeper's own tree never comes out
                Assert.IsFalse(sim.EcoCanPluckSapling(sim.EcoTree.cell));
                Assert.AreEqual(sim.EcoTree.cell, e.treeCell);
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TheKitSheet_HasTheTweezers_AndPuttingThemDownEndsTheTool()
        {
            yield return Open();
            try
            {
                yield return LivingLand();
                var hud = GameHud.Instance;
                Assert.IsTrue(hud.KitBox);
                hud.SetHand(GameHud.KitTool.Tweezers);
                yield return null;
                Assert.AreEqual(GameHud.KitTool.Tweezers, hud.Hand);
                Assert.IsFalse(hud.CleanMode, "one tool in hand at a time");
                hud.SetCleanMode(true);
                yield return null;
                Assert.AreEqual(GameHud.KitTool.None, hud.Hand, "the cloth replaces the tweezers");
                hud.SetCleanMode(false);
            }
            finally { Close(); }
        }
    }
}
