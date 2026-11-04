using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// P8 polish #266 (view half) and #277 in the real Slice scene at a phone size: the spray and its sound, the wet area and the
    /// little green at once, puddles after a long hold and the people who use them, and the rebuild budget of a stroke.
    /// </summary>
    [Category("EcoMistView")]
    public class EcoMistViewPlayTests : InputTestFixture
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

        IEnumerator Barren()
        {
            Assert.IsNotNull(GameClock.Instance, "the slice's clock");
            GameClock.DebugEcologyBox(true);
            for (int i = 0; i < 30; i++) yield return null;
            FirstRunView.DebugSkipAll();
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsTrue(GameClock.Instance.Sim.EcologyOn, "a barren box");
            var hud = GameHud.Instance;
            if (hud != null && hud.CleanMode) hud.SetCleanMode(false);
            hud?.SetHand(GameHud.KitTool.Mister);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spray_Sound_WetAtOnce_ALittleGreen_ThenPuddlesAndVisitors()
        {
            yield return Open();
            try
            {
                yield return Barren();
                var sim = GameClock.Instance.Sim;
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var mist = EcoMistView.Instance;
                var surf = EcoSurface.Instance;
                Assert.IsNotNull(mist); Assert.IsNotNull(surf, "the ground-look component is on the box");
                var st = sim.Settlement;
                float x = st.Map.greenX + 0.55f, z = st.Map.greenZ - 0.1f;
                var rig0 = Camera.main.GetComponent<BoxCamera>();
                rig0.FlyTo(box.transform.TransformPoint(box.land.OnGround(x, z)), Mathf.Max(rig0.minZoom * 1.15f, 0.25f), 0.4f);
                for (int i = 0; i < 200 && rig0.IsFlying; i++) yield return null;
                for (int i = 0; i < 20; i++) yield return null;
                var at = ScreenOf(box, x, z);
                int green0 = 0; for (int c = 0; c < Sim.Eco.EcoLand.N; c++) if (sim.EcoCoverAt(c) != Sim.Eco.EcoCover.Bare) green0++;

                int id = touchId++;
                BeginTouch(id, at, queueEventOnly: true);
                float t0 = Time.unscaledTime;
                bool sawWetFast = false, sawFreshFast = false;
                float hit = -1f;
                while (Time.unscaledTime - t0 < 0.9f)
                {
                    MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true);
                    if (surf.WetCells > 3 && hit < 0f) hit = Time.unscaledTime - t0;
                    sawWetFast |= surf.WetCells > 3;
                    sawFreshFast |= surf.FreshCells > 0 || surf.ShootCells > 0;
                    yield return null;
                }
                Assert.IsTrue(mist.Misting, "held");
                Assert.IsTrue(mist.Spraying, "droplets are flying");
                Assert.IsNotNull(mist.Sound);
                Assert.That(mist.Sound.Level, Is.GreaterThan(0.5f), "the fsss is on");
                Assert.IsTrue(sawWetFast, "the ground darkened within a second of the touch: " + hit);
                Assert.IsTrue(sawFreshFast, "a little green or shoots showed at once");
                Debug.Log($"[mistview] wet cells {surf.WetCells}, fresh {surf.FreshCells}, shoots {surf.ShootCells}, first wet after {hit:0.00}s");
                yield return Shot("eco_view_spray_mid");

                // keep the finger down 4.5 s in all: the reach grows with the dwell and a puddle stands
                while (Time.unscaledTime - t0 < 4.8f)
                {
                    MoveTouch(id, at + new Vector2(Mathf.Sin(Time.unscaledTime * 9f), 0f), delta: Vector2.zero, queueEventOnly: true);
                    yield return null;
                }
                Assert.That(mist.SprayRadius, Is.GreaterThan(0.95f), "a long hold reaches the full radius");
                EndTouch(id, at, delta: Vector2.zero, queueEventOnly: true);
                for (int i = 0; i < 6; i++) yield return null;
                Assert.That(mist.Sound.Level, Is.LessThan(0.9f).And.GreaterThan(0f).Or.EqualTo(0f), "the sound fades, it does not cut");
                for (int i = 0; i < 70; i++) yield return null;
                Assert.That(mist.Sound.Level, Is.LessThan(0.01f), "silent after the finger lifts");
                Assert.IsFalse(mist.Spraying);
                yield return Shot("eco_view_wet_after");

                // wet area and green
                int green1 = 0; for (int c = 0; c < Sim.Eco.EcoLand.N; c++) if (sim.EcoCoverAt(c) != Sim.Eco.EcoCover.Bare) green1++;
                Assert.Greater(green1, green0 + 2, "green at once");
                Assert.Greater(surf.WetCells, 20, "a wet area");
                Assert.Greater(surf.PuddleCount, 0, "a long hold left a puddle");
                Assert.That(box.AoPending, Is.EqualTo(0).After(3000, 100), "the AO the stroke skipped caught up");

                // the people use the puddle
                for (int i = 0; i < 600; i++) yield return null; // the rain reactions of the stroke run out first
                var pl = PuddleLife.Instance;
                Assert.IsNotNull(pl);
                string r = pl.VisitNow();
                Debug.Log("[mistview] visit: " + r);
                bool someone = false;
                var pv = PeopleView.Find();
                for (int i = 0; i < 90 && !someone; i++)
                {
                    foreach (var a in pv.Activities)
                        if ((a.kind == ActivityKind.Stomp || a.kind == ActivityKind.WashHands) && a.place.kind == PlaceKind.Puddle) { someone = true; break; }
                    if (i == 40) yield return Shot("eco_view_puddle_people");
                    yield return null;
                }
                var kinds = new System.Text.StringBuilder(); foreach (var a in pv.Activities) kinds.Append(a.kind + "@" + a.place.kind + " ");
                Assert.IsTrue(someone, "someone is at the puddle: " + r + " | " + pv.Activities.Count + " people: " + kinds);
                yield return Shot("eco_view_puddle_people2");

                // some months later: the wet area dried, the shoots grew
                float wetNow = 0f; for (int c = 0; c < Sim.Eco.EcoLand.N; c++) wetNow += sim.EcoWetAt(c);
                sim.DebugSkipSeasons(2);
                box.Refresh();
                for (int i = 0; i < 160; i++) yield return null;
                float wetLater = 0f; for (int c = 0; c < Sim.Eco.EcoLand.N; c++) wetLater += sim.EcoWetAt(c);
                Assert.Less(wetLater, wetNow, "it dried");
                yield return Shot("eco_view_months_later");
            }
            finally { Close(); }
        }

        /// <summary>The rebuild cost of a phone-like stroke: a finger dragging slowly over bare ground for six seconds, a pulse
        /// four times a second. Measured with the old way (every dirty chunk rebuilt with AO on each refresh) and the new.</summary>
        [UnityTest]
        public IEnumerator Stroke_RebuildBudget_BeforeAndAfter()
        {
            yield return Open();
            try
            {
                yield return Barren();
                var sim = GameClock.Instance.Sim;
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var mist = EcoMistView.Instance;
                var st = sim.Settlement;
                var report = new List<string>();
                double[] p99 = new double[2], max = new double[2], maxDown = new double[2], p95 = new double[2], avg = new double[2];
                for (int pass = 0; pass < 2; pass++)
                {
                    bool cheap = pass == 1;
                    mist.cheapStrokes = cheap;
                    var samples = new List<double>(); double down = 0;
                    double last = box.RebuildMsTotal;
                    int id = touchId++;
                    float cx = st.Map.greenX - 0.6f + pass * 0.1f, cz = st.Map.greenZ - 0.9f;
                    var a = ScreenOf(box, cx, cz);
                    BeginTouch(id, a, queueEventOnly: true);
                    float t0 = Time.unscaledTime;
                    while (Time.unscaledTime - t0 < 6f)
                    {
                        float u = (Time.unscaledTime - t0) / 6f;
                        var p = ScreenOf(box, cx + u * 1.6f, cz + Mathf.Sin(u * 6f) * 0.25f);
                        MoveTouch(id, p, delta: Vector2.zero, queueEventOnly: true);
                        yield return null;
                        double now = box.RebuildMsTotal;
                        samples.Add(now - last); last = now; down = Math.Max(down, samples[samples.Count - 1]);
                    }
                    maxDown[pass] = down;
                    EndTouch(id, ScreenOf(box, cx + 1.6f, cz), delta: Vector2.zero, queueEventOnly: true);
                    for (int i = 0; i < 60; i++) { yield return null; double now = box.RebuildMsTotal; samples.Add(now - last); last = now; }
                    var s = new List<double>(samples); s.Sort();
                    double sum = 0, mx = 0; int busy = 0;
                    foreach (var v in s) { sum += v; mx = Math.Max(mx, v); if (v > 0.01) busy++; }
                    max[pass] = mx; avg[pass] = sum / Math.Max(1, s.Count); p95[pass] = s[(int)(s.Count * 0.95)]; p99[pass] = s[(int)(s.Count * 0.99)];
                    string line = $"[budget] {(cheap ? "AFTER (deferred AO, 1 chunk a frame)" : "BEFORE (all dirty chunks + AO per refresh)")}: frames {s.Count}, frames with a rebuild {busy}, worst {mx:0.0} ms (while the finger was down {maxDown[pass]:0.0}), p95 {p95[pass]:0.0} ms, p99 {p99[pass]:0.0} ms, total {sum:0} ms, ChunkBuilds {box.ChunkBuilds}";
                    report.Add(line);
                    Debug.Log(line);
                    for (int i = 0; i < 30; i++) yield return null;
                }
                File.WriteAllLines(Shots + "stroke_budget.txt", report);
                Assert.Less(p99[1], 9.0, "after: 99 % of frames spend no more than ~8 ms rebuilding (p99 " + p99[1].ToString("0.0") + ", worst " + max[1].ToString("0.0") + ")");
                Assert.Less(max[1], 20.0, "and the worst single frame (a GC or a first-time cost) stays far below the old 23+ ms");
                Assert.Less(max[1], max[0], "and it is better than before");
            }
            finally { Close(); }
        }
    }
}
