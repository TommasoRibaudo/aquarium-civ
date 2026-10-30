using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.Shaping;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using T = AquariumCiv.View.Shaping.ShapingTuning;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #85 in the real Slice scene at a portrait phone size (1179 × 2556): in the camp, the village and the town each slice
    /// tool is placed by a real one-finger tap in shape mode, its look moves through its stages (the cloud and the rain,
    /// saplings, the channel carved, the herd running in), its take-back chip takes it back and it leaves, and the people's
    /// answer (debug skip) is staged in the world with the world label and the chronicle line. No doll jumps (0.25 u per
    /// 100 ms), no console errors. Screenshots go to Temp/agent (85_*). Run alone; it backs up and restores the save.
    /// </summary>
    [Category("ShapingPlay")]
    public class ShapingPlayTests : InputTestFixture
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
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            backup = savePath + ".shaping-test-backup";
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
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                if (box != null && CareView.Instance == null) box.gameObject.AddComponent<CareView>();
                if (box != null && GlassVoice.Instance == null) box.gameObject.AddComponent<GlassVoice>();
                if (box != null && ShapingView.Instance == null) box.gameObject.AddComponent<ShapingView>();
            }
            yield return null; yield return null;
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
            errors.Clear();
            Application.logMessageReceived += OnLog;
        }

        void Close()
        {
            Application.logMessageReceived -= OnLog;
            ShapingView.Instance?.SetTool(null);
            var pv = PeopleView.Find();
            if (pv != null) { pv.timeScale = 1f; pv.Planner?.ClearLive(); }
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

        // ---------------------------------------------------------------- doll jumps (MomentPlayTests' check)

        readonly Dictionary<PegDollView, Vector3> lastDoll = new();
        readonly List<string> jumpLog = new();
        int jumps, frames;
        float worst;

        void ResetAudit() => lastDoll.Clear();

        void Audit(string tag)
        {
            var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-3f);
            float limit = 0.25f * Mathf.Max(1f, dt / 0.1f);
            var shown = new HashSet<PegDollView>(box.GetComponentsInChildren<PegDollView>(false));
            foreach (var d in shown)
            {
                var p = d.transform.localPosition;
                if (lastDoll.TryGetValue(d, out var q))
                {
                    float m = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));
                    worst = Mathf.Max(worst, m);
                    if (m > limit) { jumps++; if (jumpLog.Count < 12) jumpLog.Add($"{tag} {d.name} {m:0.00}"); }
                }
            }
            lastDoll.Clear();
            foreach (var d in shown) lastDoll[d] = d.transform.localPosition;
            frames++;
        }

        /// <summary>Real seconds of play, auditing the dolls every frame; stops early when <paramref name="until"/> holds.</summary>
        IEnumerator Play(float seconds, string tag, Func<bool> until = null)
        {
            float t = 0f;
            while (t < seconds && (until == null || !until()))
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                Audit(tag);
            }
        }

        static IEnumerator Skip(float seconds) { float t = 0f; while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; } }

        // ---------------------------------------------------------------- the box

        static IEnumerator GrowInto(Era era)
        {
            var sim = GameClock.Instance.Sim;
            for (int i = 0; i < 20 && sim.State.era < era; i++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            Assume.That(sim.State.era, Is.EqualTo(era), "the box grows into the " + era);
            var src = SimLayoutSource.Find();
            float g = 0f;
            while (src != null && src.WaveRunning && g < 30f) { g += Time.unscaledDeltaTime; yield return null; }
            sim.CleanAll();
            yield return null; yield return null;
        }

        /// <summary>Open dry ground near the preferred point: no lot or woods close by, off the pond, away from the spots
        /// already used (a grid over the box, the nearest good one).</summary>
        static (float x, float z) Spot(SimCore sim, float x, float z, List<(float, float)> used)
        {
            var map = sim.Settlement.Map;
            var spec = map.spec;
            (float, float) best = (x, z);
            float bd = float.MaxValue;
            for (float qx = -2.2f; qx <= 2.21f; qx += 0.2f)
                for (float qz = -1.6f; qz <= 1.61f; qz += 0.2f)
                {
                    float ax = qx, az = qz;
                    if (!sim.Settlement.OpenSpotNear(ref ax, ref az, 0.3f)) continue;
                    if (Vector2.Distance(new Vector2(ax, az), new Vector2(qx, qz)) > 0.12f) continue; // the ground right here is open
                    if (Vector2.Distance(new Vector2(ax, az), new Vector2(spec.pondX, spec.pondZ)) < 0.9f) continue;
                    if (sim.HerdVariantAt(ax, az) != "deer") continue;
                    if (used.Any(u => Vector2.Distance(new Vector2(ax, az), new Vector2(u.Item1, u.Item2)) < 0.8f)) continue;
                    bool busy = false;
                    for (int c = 0; c < AquariumCiv.Sim.Settle.SiteMap.N && !busy; c++)
                    {
                        if (!map.Forest(c) && map.lot[c] == 0) continue;
                        if (Vector2.Distance(new Vector2(AquariumCiv.Sim.Settle.SiteMap.CXc(c), AquariumCiv.Sim.Settle.SiteMap.CZc(c)), new Vector2(ax, az)) < 0.45f) busy = true;
                    }
                    if (busy) continue;
                    float d = Vector2.Distance(new Vector2(ax, az), new Vector2(x, z));
                    if (d < bd) { bd = d; best = (ax, az); }
                }
            used.Add(best);
            return best;
        }

        IEnumerator Look(float x, float z, float zoom = 0.5f)
        {
            Camera.main.GetComponent<BoxCamera>().FlyTo(UnityEngine.Object.FindAnyObjectByType<BoxView>().transform.TransformPoint(new Vector3(x, 0f, z)), zoom, 0.3f);
            yield return Play(0.6f, "fly");
        }

        // ---------------------------------------------------------------- the tests

        [UnityTest, Timeout(900000)]
        public IEnumerator Camp_EachToolPlacedByTapGrowsIsAnsweredAndTakenBack() => EraRun(Era.Camp);

        [UnityTest, Timeout(900000)]
        public IEnumerator Village_EachToolPlacedByTapGrowsIsAnsweredAndTakenBack() => EraRun(Era.Village);

        [UnityTest, Timeout(900000)]
        public IEnumerator Town_EachToolPlacedByTapGrowsIsAnsweredAndTakenBack() => EraRun(Era.Town);

        IEnumerator EraRun(Era era)
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var sv = ShapingView.Instance;
                var pv = PeopleView.Find();
                Assert.IsNotNull(sv, "the shaping layer is on the box");
                Assert.IsNotNull(pv);
                if (era == Era.Camp) { SimLayoutSource.DebugSkipYears(3f); yield return Skip(0.5f); sim.CleanAll(); }
                else yield return GrowInto(era);
                PeopleView.DebugSetHour(9.5f);
                yield return Skip(0.5f);
                string tag = era.ToString().ToLowerInvariant();
                var used = new List<(float, float)>();
                var tools = era == Era.Camp
                    ? new[] { SimCore.NudgeKind.Rain, SimCore.NudgeKind.Grove, SimCore.NudgeKind.Herd, "fish" }
                    : new[] { SimCore.NudgeKind.Rain, SimCore.NudgeKind.Grove, SimCore.NudgeKind.Herd, SimCore.NudgeKind.Channel };
                string takeBackTool = era == Era.Camp ? SimCore.NudgeKind.Rain : era == Era.Village ? SimCore.NudgeKind.Channel : SimCore.NudgeKind.Herd;
                ResetAudit();
                foreach (var tool in tools)
                {
                    string kind = tool == "fish" ? SimCore.NudgeKind.Herd : tool;
                    var (x, z) = tool == "fish" ? (sim.Settlement.Map.spec.pondX, sim.Settlement.Map.spec.pondZ) : Spot(sim, 1.3f, 0.6f, used);
                    yield return Look(x, z, kind == SimCore.NudgeKind.Rain ? 0.45f : 0.32f);
                    sv.SetTool(kind);
                    int before = sim.State.nudges.Count;
                    yield return TapAt(sv.ScreenOf(x, z));
                    yield return Play(0.2f, tool);
                    Assert.AreEqual(before + 1, sim.State.nudges.Count, $"{era} {tool}: a tap on the ground placed it ({sv.LastResult})");
                    var n = sim.State.nudges[sim.State.nudges.Count - 1];
                    Assert.AreEqual(kind, n.kind);
                    if (tool == "fish") Assert.AreEqual("fish", n.variant, "flicked into the water: a shoal");
                    var e = sv.Show.Find(n.id);
                    Assert.IsNotNull(e, "the view tracks it");
                    Assert.AreEqual(ShapingStage.Arriving, sv.Show.StageOf(e));
                    Debug.Log($"[ShapingPlay] {era} {tool} #{n.id} at ({n.x:0.00}, {n.z:0.00}): {sv.LastResult}");

                    // The arrival, then the gift waiting.
                    float mid = kind switch { SimCore.NudgeKind.Rain => 3f, SimCore.NudgeKind.Channel => 0.7f, SimCore.NudgeKind.Herd => tool == "fish" ? 0.3f : 1.2f, _ => 0.5f };
                    yield return Play(mid, tool);
                    yield return Shot($"85_{tag}_{tool}_arriving");
                    switch (kind)
                    {
                        case SimCore.NudgeKind.Rain:
                            var r = sv.Show.Rain(e);
                            Assert.Greater(r.cloud, 0.5f, "a cloud over it"); Assert.Greater(r.rain, 0.5f, "raining");
                            Assert.IsTrue(sv.Root.Find($"Shaping_rain_{n.id}/Cloud").gameObject.activeSelf, "the cloud is drawn");
                            break;
                        case SimCore.NudgeKind.Grove:
                            Assert.IsTrue(sv.Show.Grove(e).trees.Any(t => t.growth > 0f), "saplings");
                            Assert.Greater(sv.Root.Find($"Shaping_grove_{n.id}").childCount, 0, "the saplings are drawn");
                            break;
                        case SimCore.NudgeKind.Channel:
                            Assert.Greater(sv.Show.Channel(e).cells.Count, 2, "a channel");
                            break;
                        case SimCore.NudgeKind.Herd:
                            Assert.IsTrue(sv.Show.Herd(e).animals.Any(a => a.pose != AnimalPose.Hidden), "animals coming in");
                            break;
                    }
                    yield return Play(ShapingShow.ArrivalSeconds(e.footprint) - mid + 0.3f, tool);
                    Assert.AreEqual(ShapingStage.Waiting, sv.Show.StageOf(e), $"{era} {tool}: arrived");
                    yield return Shot($"85_{tag}_{tool}_waiting");
                    yield return Play(0.1f, tool);
                    Assert.IsTrue(sv.Chips().Any(c => c.id == n.id), "a take-back chip in shape mode");

                    if (tool == takeBackTool)
                    {
                        // Take it back with the chip, then place it again for the answer.
                        var chip = sv.Chips().First(c => c.id == n.id);
                        yield return TapAt(chip.screen);
                        yield return Play(0.1f, tool);
                        Assert.IsFalse(sim.State.nudges.Any(q => q.id == n.id), $"{era} {tool}: the chip took it back");
                        Assert.AreEqual(ShapingStage.Leaving, sv.Show.StageOf(e));
                        yield return Play(T.LeaveSeconds * 0.4f, tool);
                        yield return Shot($"85_{tag}_{tool}_leaving");
                        yield return Play(T.HerdLeaveSeconds, tool);
                        Assert.IsNull(sv.Show.Find(n.id), "gone");
                        yield return TapAt(sv.ScreenOf(x, z));
                        yield return Play(ShapingShow.ArrivalSeconds(e.footprint) + 0.3f, tool);
                        n = sim.State.nudges[sim.State.nudges.Count - 1];
                        e = sv.Show.Find(n.id);
                        Assert.IsNotNull(e, "placed again");
                    }

                    // The people answer (debug skip): staged in the world, the label and the line over it.
                    sv.SetTool(null);
                    Assert.IsTrue(sv.SkipToAnswer(n.id), sv.LastResult);
                    yield return Play(20f, tool, () => sv.Show.Answer.active && sv.Show.Answer.nudgeId == n.id);
                    var a = sv.Show.Answer;
                    Assert.IsTrue(a.active && a.nudgeId == n.id, $"{era} {tool}: the answer is shown");
                    Assert.IsNotNull(e.moment, $"{era} {tool}: the people's answer is staged in the world");
                    if (n.buildingId >= 0) { var b = sim.State.buildings.First(q => q.id == n.buildingId); yield return Look(b.x, b.z, 0.5f); }
                    else yield return Look(n.x, n.z, 0.5f);
                    yield return Play(2.5f, tool);
                    Assert.AreEqual(a.label, sv.ShownLabel, "the world label");
                    Assert.IsFalse(string.IsNullOrEmpty(sv.ShownBubble), "the chronicle line as a bubble");
                    int inScene = e.moment.cast.Count(c => pv.Planner.Plan(c.who, pv.Now).source == ActivitySource.Scene);
                    Debug.Log($"[ShapingPlay] {era} {tool} answered: \"{a.bubble}\" label \"{a.label}\", cast {e.moment.cast.Count}, {inScene} in the scene now");
                    yield return Shot($"85_{tag}_{tool}_answer");
                    yield return Play(16f, tool, () => !sv.Show.Answer.active); // one answer at a time on screen
                }

                // Years later: the grove grown (and the rain's field).
                ResetAudit();
                SimLayoutSource.DebugSkipYears(4f);
                yield return Skip(1f);
                var grove = sv.Show.Entries.FirstOrDefault(q => q.kind == SimCore.NudgeKind.Grove);
                if (grove != null)
                {
                    var g = sv.Show.Grove(grove);
                    Assert.IsTrue(g.trees.Count == 0 || g.trees.Max(t => t.growth) > 0.9f, "the grove has grown");
                    yield return Look(grove.footprint.x, grove.footprint.z, 0.45f);
                    yield return Shot($"85_{tag}_grove_grown");
                }
                yield return Look(0f, 0f, 1f);
                yield return Shot($"85_{tag}_overview");

                Debug.Log($"[ShapingPlay] {era}: frames {frames} jumps {jumps} worst {worst:0.000}\n" + string.Join("\n", jumpLog));
                Assert.Greater(frames, 100);
                Assert.AreEqual(0, jumps, "no doll jumps: " + string.Join("; ", jumpLog));
                Assert.IsEmpty(errors, "no console errors: " + string.Join(" | ", errors.Take(5)));
            }
            finally { Close(); }
        }
    }
}
