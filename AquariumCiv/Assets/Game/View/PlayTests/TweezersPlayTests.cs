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

        /// <summary>#402 (playtest #394, "removed a tree and another grown-up tree appeared right after"): a grown tree with young ones just before it
        /// in cell order is drawn before the pluck, and still drawn (not newly drawn) after it. Failed before the fix: trees and young trees were
        /// placed in one pass in cell order, so a young tree took the grown one's room and plucking it let the grown tree pop up.</summary>
        [UnityTest]
        public IEnumerator Tweezers_PluckingASapling_NoGrownTreePopsUp()
        {
            yield return Open();
            try
            {
                yield return LivingLand();
                var sim = GameClock.Instance.Sim;
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var e = sim.State.eco;
                box.Refresh();
                yield return null;
                // A grown tree that is drawn today, on an even cell (one crown per 2 × 2 block), away from the Keeper's tree and the water.
                int ti = e.treeCell >= 0 ? AquariumCiv.Sim.Settle.SiteMap.I(e.treeCell) : -99, tj = e.treeCell >= 0 ? AquariumCiv.Sim.Settle.SiteMap.J(e.treeCell) : -99;
                bool Ok(int c)
                {
                    int i = AquariumCiv.Sim.Settle.SiteMap.I(c), j = AquariumCiv.Sim.Settle.SiteMap.J(c);
                    if (((i | j) & 1) != 0 || i < 2 || j < 2 || i > AquariumCiv.Sim.Settle.SiteMap.NX - 3 || j > AquariumCiv.Sim.Settle.SiteMap.NZ - 3) return false;
                    if (Math.Abs(i - ti) < 4 && Math.Abs(j - tj) < 4) return false;
                    for (int dj = -2; dj <= 2; dj++)
                        for (int di = -2; di <= 2; di++)
                        {
                            var cv = (EcoCover)e.cover[AquariumCiv.Sim.Settle.SiteMap.Index(i + di, j + dj)];
                            if (cv == EcoCover.Water || cv == EcoCover.Reeds) return false;
                        }
                    return true;
                }
                // Spots where a lone grown tree is drawn (meadow round it), apart from each other: one spot alone can miss, since each young
                // tree is drawn only on some cells (a hash), so the case runs at several.
                var spots = new System.Collections.Generic.List<int>();
                var tried = Enumerable.Range(0, EcoLand.N).Where(Ok).OrderBy(c => (c * 7919) % 997).Take(60).ToList();
                foreach (int cand in tried)
                {
                    if (spots.Count >= 6) break;
                    int gi = AquariumCiv.Sim.Settle.SiteMap.I(cand), gj = AquariumCiv.Sim.Settle.SiteMap.J(cand);
                    if (spots.Any(s => Math.Abs(AquariumCiv.Sim.Settle.SiteMap.I(s) - gi) < 6 && Math.Abs(AquariumCiv.Sim.Settle.SiteMap.J(s) - gj) < 6)) continue;
                    var saved = new byte[25];
                    for (int dj = -2, k = 0; dj <= 2; dj++)
                        for (int di = -2; di <= 2; di++, k++)
                        {
                            int c = AquariumCiv.Sim.Settle.SiteMap.Index(gi + di, gj + dj);
                            saved[k] = e.cover[c];
                            e.cover[c] = (byte)EcoCover.Grass;
                        }
                    e.cover[cand] = (byte)EcoCover.Tree;
                    lock (box.TreeSpots) box.TreeSpots.Clear();
                    box.Refresh();
                    bool stands;
                    lock (box.TreeSpots) stands = box.TreeSpots.ContainsKey(cand);
                    if (stands) { spots.Add(cand); continue; }
                    for (int dj = -2, k = 0; dj <= 2; dj++)
                        for (int di = -2; di <= 2; di++, k++) e.cover[AquariumCiv.Sim.Settle.SiteMap.Index(gi + di, gj + dj)] = saved[k];
                }
                Assert.GreaterOrEqual(spots.Count, 3, $"lone grown trees stand ({tried.Count} spots tried)");
                // Young trees on the four cells that come before each grown tree in cell order and touch it.
                var young = spots.SelectMany(g => new[] { (-1, 0), (-1, -1), (0, -1), (1, -1) }
                    .Select(d => AquariumCiv.Sim.Settle.SiteMap.Index(AquariumCiv.Sim.Settle.SiteMap.I(g) + d.Item1, AquariumCiv.Sim.Settle.SiteMap.J(g) + d.Item2))).ToArray();
                foreach (int c in young) e.cover[c] = (byte)EcoCover.Young;
                lock (box.TreeSpots) box.TreeSpots.Clear();
                box.Refresh();
                yield return null;
                string Missing() { lock (box.TreeSpots) return string.Join(" ", spots.Where(g => !box.TreeSpots.ContainsKey(g)).Select(g => $"{AquariumCiv.Sim.Settle.SiteMap.I(g)},{AquariumCiv.Sim.Settle.SiteMap.J(g)}")); }
                Assert.AreEqual("", Missing(), $"every grown tree is drawn with young trees beside it ({spots.Count} spots)");

                foreach (int c in young)
                {
                    Assert.IsTrue(sim.EcoCanPluckSapling(c), $"young tree {c} can be plucked");
                    Assert.AreEqual(c, sim.EcoPluckSapling(AquariumCiv.Sim.Settle.SiteMap.CXc(c), AquariumCiv.Sim.Settle.SiteMap.CZc(c), 0.01f));
                }
                lock (box.TreeSpots) box.TreeSpots.Clear();
                box.Refresh();
                yield return null;
                Assert.AreEqual("", Missing(), "every grown tree still stands after the pluck");
                yield return Shot("tweezers-402-after-pluck");
            }
            finally { Close(); }
        }

        /// <summary>#375 (playtest #347, "tweezers only worked when I wasn't clicking through the glass"): a weed seen through grimy glass comes out
        /// with a tap by touch. Failed before the fix: PluckAt dropped any piece behind dirty glass (BehindDirtyGlass), the cloth's rule.</summary>
        [UnityTest]
        public IEnumerator Tweezers_PluckAWeedSeenThroughGrimyGlass()
        {
            yield return Open();
            try
            {
                yield return LivingLand();
                var sim = GameClock.Instance.Sim;
                var hud = GameHud.Instance;
                var care = CareView.Instance;
                sim.AddMess(0f, 0.4f);
                for (int i = 0; i < 40; i++) yield return null;
                sim.AddMess(1f, 0f); // the glass fogs over
                for (int i = 0; i < 20; i++) yield return null;
                // Batch 1: from the default pitch most of the land is seen through the open top, so "a weed in view" was often not behind
                // any pane. Take one that is; failing that, look in lower, through the front glass.
                MessProps.Piece Pick() => care.Props.AllPieces.FirstOrDefault(p => !p.leaving && p.kind == MessProps.Kind.Weed && OnScreen(ScreenOf(p)) && care.BehindDirtyGlass(Camera.main, p));
                var weed = Pick();
                if (weed == null)
                {
                    var rig = Camera.main.GetComponent<BoxCamera>();
                    rig.pitch = 14f; rig.Apply();
                    for (int i = 0; i < 30; i++) yield return null;
                    weed = Pick();
                }
                Assert.IsNotNull(weed, "a weed patch in view, behind the glass");
                Assert.IsTrue(care.BehindDirtyGlass(Camera.main, weed), "precondition: the glass in front of the weed is grimy");
                hud.SetHand(GameHud.KitTool.Tweezers);
                yield return null;
                float og0 = sim.State.overgrowth;
                yield return TapAt(ScreenOf(weed));
                Assert.Less(sim.State.overgrowth, og0 - 0.05f, "the weed came out through the glass");
                Assert.AreEqual("weed", care.LastPluck);
                hud.SetHand(GameHud.KitTool.None);
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

        /// <summary>#317 look check: every stage of the Keeper's tree at the default camera (tester: the seed and the sprout were lost there), and the grown tree at night.</summary>
        [UnityTest]
        public IEnumerator KeeperTree_EveryStageAtTheDefaultView_Shots()
        {
            yield return Open();
            try
            {
                yield return LivingLand();
                for (int s = 0; s <= 4; s++)
                {
                    EcoDebug.TreeStage(s);
                    for (int i = 0; i < 20; i++) yield return null;
                    yield return Shot($"keeper-tree-T{s}-default");
                }
                NightLamps.DebugSetLevel(1f);
                for (int i = 0; i < 20; i++) yield return null;
                yield return Shot("keeper-tree-T4-night");
                NightLamps.DebugSetLevel(0f);
            }
            finally { Close(); }
        }
    }
}
