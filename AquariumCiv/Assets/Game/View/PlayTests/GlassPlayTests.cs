using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// The people at the glass and the relic moments (#92) in the real Slice scene at a portrait phone size: each relic's
    /// moment plays without an error and gives its cast back to their day; the kids at the window in stage 2; a tap on the
    /// glass in stage 3 with the Lens brings a scholar over to tap back as often. Screenshots go to Temp/agent. Run alone
    /// (one InputTestFixture class per run); it backs up and restores the slice's save file.
    /// </summary>
    [Category("GlassPlay")]
    public class GlassPlayTests : InputTestFixture
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
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savePath = Path.Combine(Application.persistentDataPath, "game", "box.json");
            backup = savePath + ".glass-test-backup";
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

        static IEnumerator Until(Func<bool> done, float seconds)
        {
            float t = 0f;
            while (!done() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>Grow the fresh box into <paramref name="era"/> (no relic moments: a skip is a catch-up).</summary>
        static IEnumerator GrowInto(Era era)
        {
            var sim = GameClock.Instance.Sim;
            for (int i = 0; i < 20 && sim.State.era < era; i++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            Assume.That(sim.State.era, Is.EqualTo(era), "the box grows into the " + era);
            sim.CleanAll(); // forty years of grime would hide the people behind the glass
            yield return null; yield return null;
        }

        static void EnsureScholar(SimCore sim)
        {
            if (sim.Scholars.Count > 0) return;
            var n = sim.State.notables.Where(x => x.alive && x.age >= SimCore.AdultAge && x.age < 55f).OrderBy(x => x.id).FirstOrDefault();
            if (n != null) n.trait = "the clever";
        }

        /// <summary>Fly the camera to a place the planner knows, close in.</summary>
        static IEnumerator Look(Place place, float zoom = 0.3f)
        {
            var cam = Camera.main.GetComponent<BoxCamera>();
            cam.FlyToPlace(place, zoom, 0.4f);
            yield return new WaitForSecondsRealtime(0.7f);
        }

        [UnityTest]
        public IEnumerator EachRelicMomentPlaysAndGivesItsCastBackToTheirDay()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var pv = PeopleView.Find();
                Assert.IsNotNull(pv); Assert.IsNotNull(GlassVoice.Instance, "the glass layer is on the box");
                yield return GrowInto(Era.Village);
                EnsureScholar(sim);
                foreach (var id in new[] { RelicId.SkyCrumb, RelicId.KeeperDoll, RelicId.LeftSandal, RelicId.FestivalMask, RelicId.Lens })
                {
                    PeopleView.DebugSetHour(id == RelicId.Lens ? 18.4f : 10f);
                    yield return null;
                    string r = GlassVoice.DebugPlayMoment(id);
                    var info = GlassVoice.Instance.LastMoment;
                    Debug.Log($"[GlassPlay] {id}: {r}");
                    Assert.IsNotNull(info, $"{id}: the moment plays ({r})");
                    Assert.AreEqual(id, info.relicId);
                    Assert.Greater(info.cast.Count, 0, $"{id}: someone plays it");
                    // Watch it at 6x; a screenshot of the crowd half way, close in.
                    pv.timeScale = 6f;
                    var planner = pv.Planner;
                    var spot = id == RelicId.FestivalMask ? info.spot : LifePlanner.RelicPlace(id);
                    yield return Look(spot);
                    float mid = (info.start + info.end) * 0.5f;
                    yield return Until(() => pv.Now.hour >= mid || pv.Now.day != info.day, 30f);
                    yield return Shot("relic_" + id);
                    if (id is RelicId.SkyCrumb or RelicId.KeeperDoll or RelicId.Lens)
                        Assert.IsTrue(GlassVoice.Instance.GetComponent<RelicProps>().Made.ContainsKey(id), $"{id}: its prop stands in the box");
                    int inScene = info.cast.Count(c => planner.Plan(c.who, pv.Now).source == ActivitySource.Scene);
                    Assert.Greater(inScene, 0, $"{id}: the cast is in the scene half way");
                    yield return Until(() => pv.Now.hour >= info.end + 0.6f || pv.Now.day != info.day, 40f);
                    pv.timeScale = 1f;
                    // still in this moment: a staged scene (chronicleId -1); a story scene from the chronicle they walk into after it
                    // (a visit at midday) is their day
                    var still = info.cast.Where(c => { var a = planner.Plan(c.who, pv.Now); return a.source == ActivitySource.Scene && a.chronicleId < 0; }).ToList();
                    Assert.IsEmpty(still, $"{id}: the cast is back in their day ({string.Join(", ", still.Select(c => c.who))})");
                    var moments = new List<RelicMomentInfo>();
                    Assert.AreEqual(0, planner.ActiveRelicMoments(pv.Now, moments), "the moment is over");
                    Assert.IsEmpty(errors, $"{id}: no errors: {string.Join(" | ", errors)}");
                }
                StringAssert.DoesNotContain("{0}", MomentPlayer.Instance.LastSaid, "a line was said with the name filled in");
                Assert.IsNotEmpty(MomentPlayer.Instance.LastSaid, "the moments' lines show in the bubble");
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator Stage2KidsComeToTheWindow()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var pv = PeopleView.Find();
                yield return GrowInto(Era.Village);
                sim.DebugSetBelief(2, false);
                PeopleView.DebugSetHour(14.2f);
                yield return null;
                var planner = pv.Planner;
                Assert.AreEqual(2, planner.BeliefStageSeen);
                // Whoever is near enough to the window comes: try a few afternoon hours (each box is a new seed).
                int n = 0;
                for (float h = 14.2f; n == 0 && h < 17f; h += 0.4f)
                {
                    PeopleView.DebugSetHour(h);
                    yield return null;
                    n = planner.GatherAtGlass(pv.Now, true);
                }
                Debug.Log($"[GlassPlay] {n} kids come to the window at {pv.Now}");
                Assert.Greater(n, 0, "kids come to the window");
                pv.timeScale = 4f;
                yield return Until(() => pv.Activities.Count(a => a.kind == ActivityKind.PressHands && !a.moving) >= Math.Min(2, n), 20f);
                pv.timeScale = 1f;
                var at = pv.Activities.Where(a => a.kind == ActivityKind.PressHands && !a.moving).ToList();
                Assert.IsNotEmpty(at, "hands on the glass");
                Assert.IsTrue(at.All(a => a.place.kind == PlaceKind.Glass && a.facesGlass));
                yield return Look(planner.GlassPlace, 0.25f);
                for (int i = 0; i < 20; i++) yield return null;
                yield return Shot("glass_stage2_kids");
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        /// <summary>User 2026-10-30 (#92): relic props read at default zoom, and a relic moment off-screen or too small to see
        /// nudges the camera there; a press on the box, or one already in close-up, doesn't get pulled.</summary>
        [UnityTest]
        public IEnumerator BigRelicsAndARelicMomentNudgesTheCamera()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var pv = PeopleView.Find();
                var glass = GlassVoice.Instance;
                var cam = Camera.main.GetComponent<BoxCamera>();
                yield return GrowInto(Era.Village);
                EnsureScholar(sim);
                PeopleView.DebugSetHour(10f);
                yield return null;
                cam.FlyTo(Vector3.zero, 1f, 0.01f);
                yield return null; yield return null;

                int before = glass.Nudges;
                Debug.Log("[GlassPlay] " + GlassVoice.DebugPlayMoment(RelicId.KeeperDoll));
                Assert.IsNotNull(glass.LastMoment, "the moment plays");
                Assert.Greater(glass.Nudges, before, "from the whole box the camera nudges closer");
                Assert.IsTrue(cam.IsFlying, "gently");
                float t = 0f;
                while (cam.IsFlying && t < 4f) { t += Time.unscaledDeltaTime; yield return null; }
                Assert.LessOrEqual(cam.zoom, BoxCamera.NudgeZoom + 0.02f);
                Assert.IsTrue(pv.Planner.Positions.TryGetPosition(glass.LastMoment.spot, out float x, out float z));
                var world = pv.transform.TransformPoint(new Vector3(x, 0f, z));
                var v = Camera.main.WorldToViewportPoint(world);
                Assert.IsTrue(v.x > 0.1f && v.x < 0.9f && v.y > 0.1f && v.y < 0.9f, $"the moment is on screen ({v})");
                Assert.IsFalse(cam.NudgeToward(world), "framed and close: no nudge");
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("relic_nudged");

                var props = glass.GetComponent<RelicProps>();
                Assert.IsTrue(props.Made.TryGetValue(RelicId.KeeperDoll, out var doll), "the doll stands");
                float h = doll.GetComponent<Renderer>().bounds.size.y;
                Assert.Greater(h, 0.45f, "taller than a person, it reads at default zoom");
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator ATapOnTheGlassInTheTownWithTheLensIsTappedBack()
        {
            yield return Open();
            try
            {
                var sim = GameClock.Instance.Sim;
                var pv = PeopleView.Find();
                var glass = GlassVoice.Instance;
                yield return GrowInto(Era.Town);
                EnsureScholar(sim);
                Assert.IsNotNull(sim.DebugGiveRelic(RelicId.Lens));
                sim.DebugSetBelief(3, true);
                PeopleView.DebugSetHour(10f);
                yield return null; yield return null;
                var cam = Camera.main.GetComponent<BoxCamera>();
                var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (CareView.Instance != null) Assume.That(CareView.Instance.GlassAt(centre), "the middle of the screen is on the glass");
                // Three quick taps: one knock of three.
                for (int i = 0; i < 3; i++) yield return TapAt(centre);
                yield return Until(() => glass.Knocks > 0, 3f);
                Assert.AreEqual(1, glass.Knocks, "the taps are one knock");
                Assert.AreEqual(3, glass.LastTaps, "of three taps");
                Assert.GreaterOrEqual(glass.LastTapBack, 0, "a scholar taps back");
                var who = PersonRef.Notable(glass.LastTapBack);
                // They walk over and knock as often.
                pv.timeScale = 4f;
                PersonActivity knock = default;
                yield return Until(() => (knock = pv.Planner.Plan(who, pv.Now)).kind == ActivityKind.TapBack, 40f);
                pv.timeScale = 1f;
                Assert.AreEqual(ActivityKind.TapBack, knock.kind, "the scholar knocks back");
                Assert.AreEqual(3, knock.count);
                yield return Look(pv.Planner.GlassPlace, 0.25f);
                for (int i = 0; i < 10; i++) yield return null;
                yield return Shot("glass_tapback");
                // Clean mode: a tap is a wipe, never a knock.
                GameHud.Instance.SetCleanMode(true);
                yield return null;
                yield return TapAt(centre);
                yield return new WaitForSecondsRealtime(GlassVoice.TapGap + 0.2f);
                Assert.AreEqual(1, glass.Knocks, "no knock in clean mode");
                GameHud.Instance.SetCleanMode(false);
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }
    }
}
