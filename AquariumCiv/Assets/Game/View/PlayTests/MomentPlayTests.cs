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

        /// <summary>#89: a grown camp (+14 years) watched to the end. The wave rebuilds every lot in place in the village
        /// look before the cheer; at the end no lot shows the camp look, the green has its well, no exceptions. Stills of
        /// the chief, the green, the wave and the cheer at a portrait phone size.</summary>
        [UnityTest]
        public IEnumerator GrownCampBecomesAVillageLotByLot()
        {
            yield return Open();
            try
            {
                LogAssert.ignoreFailingMessages = false;
                var sim = GameClock.Instance.Sim;
                var mp = MomentPlayer.Instance;
                var src = SimLayoutSource.Find();
                var cam = Camera.main.GetComponent<BoxCamera>();
                MomentPlayer.DebugGrow(14f);
                if (!sim.State.eraReady) sim.ForceEraReady();
                Assert.AreEqual(Era.Camp, sim.State.era);
                var lots = sim.Settlement.Lots.FindAll(l => !l.retired && l.kind != AquariumCiv.Sim.Settle.LotKind.Camp);
                Assert.Greater(lots.Count, 2, "a grown camp has lots to rebuild");
                yield return new WaitForSecondsRealtime(0.7f);
                var props = UnityEngine.Object.FindAnyObjectByType<WaitingProps>();
                Assert.IsTrue(props != null && props.Showing);
                cam.FlyTo(Vector3.zero, 1f, 0.01f);
                yield return null; yield return null;
                yield return Shot("m89_0_waiting");

                Assert.IsTrue(mp.Watch(true));
                Assert.IsTrue(src.MomentLook);
                MomentPlayer.DebugHurry(4f);

                IEnumerator To(float t)
                {
                    MomentPlayer.DebugHoldHurry = true;
                    yield return Until(() => mp.Director.Time >= t - 0.3f, 40f);
                    MomentPlayer.DebugHoldHurry = false;
                    yield return Until(() => mp.Director.Time >= t, 5f);
                }

                yield return To(11f);
                yield return Shot("m89_1_chief");
                foreach (var l in lots) Assert.AreEqual(Era.Camp, src.ShownEra(l), "nothing rebuilt before the wave");

                yield return To(18f);
                yield return Shot("m89_2_green");
                Assert.AreEqual(Era.Village, src.ShownEra(null), "the fire ring has become the green");
                Assert.AreEqual(Era.Camp, sim.State.era, "the sim has not moved yet");

                yield return To(29f);
                yield return Shot("m89_3_wave");
                int mid = mp.KnocksPlayed;
                Assert.Greater(mid, 0, "the wave has reached some lots");

                yield return To(40f);
                Assert.AreEqual(lots.Count, mp.KnocksPlayed, "every lot rebuilt by the end of the wave");
                foreach (var l in lots) Assert.AreEqual(Era.Village, src.ShownEra(l), $"lot {l.id} ({l.kind}) shows the village look");
                Assert.AreEqual(Era.Camp, sim.State.era, "still before the cheer");

                yield return To(48f);
                Assert.AreEqual(Era.Village, sim.State.era, "the cheer moved the era");
                yield return Shot("m89_4_cheer");

                MomentPlayer.DebugHoldHurry = true;
                yield return Until(() => !mp.Playing, 40f);
                MomentPlayer.DebugHoldHurry = false;
                for (int i = 0; i < 10; i++) yield return null;
                Assert.IsFalse(src.MomentLook);
                Assert.IsFalse(src.WaveRunning, "no second wave after the cheer");
                foreach (var l in sim.Settlement.Lots)
                    if (!l.retired) Assert.AreEqual(Era.Village, src.ShownEra(l), $"lot {l.id} ({l.kind})");
                Assert.IsTrue(sim.Settlement.Lots.Exists(l => l.kind == AquariumCiv.Sim.Settle.LotKind.Camp && l.retired), "the camp hearth is retired");
                var well = UnityEngine.Object.FindAnyObjectByType<BoxView>().layout.pieces.Exists(p => p.kind == PieceKind.Well);
                Assert.IsTrue(well, "the green has its well");
                Assert.IsTrue(cam.enabled);
                yield return Shot("m89_5_after");
            }
            finally { Close(); }
        }

        /// <summary>#90: a grown village (+40 years) watched to the town. Every lot rebuilds in the town look on the wave
        /// before the cheer; the green becomes a cobbled square with stalls; the cobbles unroll from the square and reach
        /// the main paths; no error logs. Stills of the bell, the square, the wave, the lord and the cheer.</summary>
        [UnityTest]
        public IEnumerator GrownVillageBecomesATown()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var mp = MomentPlayer.Instance;
                var src = SimLayoutSource.Find();
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var cam = Camera.main.GetComponent<BoxCamera>();
                Debug.Log("[town test] " + MomentPlayer.DebugGrowVillage(40f));
                Assert.AreEqual(Era.Village, sim.State.era);
                Assert.IsTrue(sim.State.eraReady);
                yield return Until(() => !src.WaveRunning, 30f); // the debug crossing's own wave
                LogAssert.ignoreFailingMessages = false;
                var st = sim.Settlement;
                var lots = st.Lots.FindAll(l => !l.retired && l.kind != AquariumCiv.Sim.Settle.LotKind.Camp);
                Assert.Greater(lots.Count, 6, "a grown village");
                var pathCells = new System.Collections.Generic.List<int>();
                for (int c = 0; c < AquariumCiv.Sim.Settle.SiteMap.N; c++) if (st.Map.tier[c] >= AquariumCiv.Sim.Settle.PathNet.PathTier) pathCells.Add(c);
                Assert.IsNotEmpty(pathCells, "the village has main paths");
                yield return new WaitForSecondsRealtime(0.7f);
                cam.FlyTo(Vector3.zero, 1f, 0.01f);
                yield return null; yield return null;
                yield return Shot("m90_0_waiting");

                Assert.IsTrue(mp.Watch(true));
                MomentPlayer.DebugHurry(4f);
                IEnumerator To(float t)
                {
                    MomentPlayer.DebugHoldHurry = true;
                    yield return Until(() => mp.Director.Time >= t - 0.3f, 60f);
                    MomentPlayer.DebugHoldHurry = false;
                    yield return Until(() => mp.Director.Time >= t, 5f);
                }
                int Cobbled() { int n = 0; foreach (int c in pathCells) if (src.CellCobbled(c)) n++; return n; }

                yield return To(4f);
                yield return Shot("m90_1_bell");
                Assert.AreEqual(0, Cobbled(), "no cobbles before the square");

                yield return To(21f);
                yield return Shot("m90_2_square");
                Assert.AreEqual(Era.Town, src.ShownEra(null), "the green has become the square");
                Assert.IsTrue(box.layout.clearings.Exists(c => c.kind == ClearingKind.Cobble) || !st.Has(AquariumCiv.Sim.Settle.LotKind.Market),
                    "a cobbled square (once there is a market)");
                Assert.IsTrue(box.layout.pieces.Exists(p => p.kind == PieceKind.Stall) || !st.Has(AquariumCiv.Sim.Settle.LotKind.Market), "stalls round the rim");
                int atSquare = Cobbled();

                yield return To(50f);
                yield return Shot("m90_3_wave");
                int mid = Cobbled();
                Assert.Greater(mp.KnocksPlayed, 0);
                Assert.Greater(mid, atSquare, "the cobbles unroll along the main paths with the wave");

                yield return To(66f);
                yield return Shot("m90_4_lord");
                Assert.AreEqual(lots.Count, mp.KnocksPlayed, "every lot rebuilt by the end of the wave");
                foreach (var l in lots) Assert.AreEqual(Era.Town, src.ShownEra(l), $"lot {l.id} ({l.kind}) shows the town look");
                Assert.AreEqual(pathCells.Count, Cobbled(), "the cobbles reached every main path");
                Assert.AreEqual(Era.Village, sim.State.era, "still before the cheer");

                yield return To(77f);
                Assert.AreEqual(Era.Town, sim.State.era, "the cheer moved the era");
                yield return Shot("m90_5_cheer");

                MomentPlayer.DebugHoldHurry = true;
                yield return Until(() => !mp.Playing, 40f);
                MomentPlayer.DebugHoldHurry = false;
                for (int i = 0; i < 10; i++) yield return null;
                Assert.IsFalse(src.MomentLook);
                Assert.IsFalse(src.WaveRunning, "no second wave after the cheer");
                foreach (var l in sim.Settlement.Lots)
                    if (!l.retired) Assert.AreEqual(Era.Town, src.ShownEra(l), $"lot {l.id} ({l.kind})");
                Assert.AreEqual(pathCells.Count, Cobbled(), "the town keeps its cobbles after the moment");
                Assert.IsTrue(box.layout.paths.Exists(p => p.cobbled), "cobbled paths in the box");
                Assert.IsTrue(cam.enabled);
                yield return Shot("m90_6_after");
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
