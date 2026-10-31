using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Life;
using AquariumCiv.Sim;
using AquariumCiv.View.ShowMe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #76 phase 2 in the real Slice scene at a portrait phone size: "Show me" on a person's story, a building's, one whose
    /// building is gone and a death flies to the right place, plays the echo with its line, ends and brings the camera back;
    /// the people's bubbles show over their heads in normal play and keep quiet in camera mode. No doll jumps (0.25 u per
    /// 100 ms), no console errors. Screenshots go to Temp/agent. It backs up and restores the slice's save file.
    /// </summary>
    [Category("ShowMePlay")]
    public class ShowMePlayTests : InputTestFixture
    {
        const string ScenePath = "Assets/Game/Scenes/Slice.unity";
        const string Shots = "F:/GameDev/AquariumCiv/Temp/agent/";
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
            savePath = GameClock.DefaultSavePath;
            backup = savePath + ".showme-test-backup";
            if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
            if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
            GameClock.DebugNextSeed = 1234; // a pinned world, so a failure replays (tooling.md)
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
                if (box != null && ShowMeDirector.Instance == null) box.gameObject.AddComponent<ShowMeDirector>();
            }
            yield return null; yield return null;
            errors.Clear();
            Application.logMessageReceived += OnLog;
        }

        void Close()
        {
            Application.logMessageReceived -= OnLog;
            GameClock.DebugNextSeed = 0;
            ShowMeDirector.BubblesOn = true;
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

        static IEnumerator GrowInto(Era era)
        {
            var sim = GameClock.Instance.Sim;
            for (int i = 0; i < 20 && sim.State.era < era; i++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            Assume.That(sim.State.era, Is.EqualTo(era), "the box grows into the " + era);
            sim.CleanAll();
            var src = SimLayoutSource.Find();
            float t = 0f;
            while (src != null && src.WaveRunning && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            PeopleView.SetHourGently(10.5f);
            for (int i = 0; i < 120; i++) yield return null; // everyone walks to where 10:30 has them
        }

        /// <summary>Dolls on screen never move more than 0.25 u per 100 ms.</summary>
        sealed class JumpAudit
        {
            readonly BoxView box;
            readonly Dictionary<PegDollView, Vector3> last = new();
            public int jumps, frames;
            public float worst;
            public readonly List<string> log = new();
            public JumpAudit(BoxView box) { this.box = box; }
            public void Step()
            {
                float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-3f);
                float limit = 0.25f * Mathf.Max(1f, dt / 0.1f);
                var shown = box.GetComponentsInChildren<PegDollView>(false);
                foreach (var d in shown)
                {
                    var p = d.transform.localPosition;
                    if (last.TryGetValue(d, out var q))
                    {
                        float m = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));
                        worst = Mathf.Max(worst, m);
                        if (m > limit) { jumps++; if (log.Count < 12) log.Add($"{d.name} {m:0.00}"); }
                    }
                }
                last.Clear();
                foreach (var d in shown) last[d] = d.transform.localPosition;
                frames++;
            }
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        [UnityTest]
        public IEnumerator ShowMe_FliesEchoesAndComesBack()
        {
            yield return Open();
            try
            {
                Assert.IsNotNull(GameClock.Instance, "the slice runs");
                yield return GrowInto(Era.Village);
                ShowMeDirector.BubblesOn = false; // the echo's own line only
                var sim = GameClock.Instance.Sim;
                var d = ShowMeDirector.Instance;
                var cam = Camera.main.GetComponent<BoxCamera>();
                var box = UnityEngine.Object.FindAnyObjectByType<BoxView>();
                var pv = PeopleView.Find();
                var grown = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).ToList();
                Assume.That(grown.Count, Is.GreaterThanOrEqualTo(3));
                var standing = sim.State.buildings.First(b => b.lotId > 0 && sim.State.settle.lots.Find(l => l.id == b.lotId)?.retired == false && b.type != "hut");
                var retired = sim.State.buildings.FirstOrDefault(b => b.lotId > 0 && sim.State.settle.lots.Find(l => l.id == b.lotId)?.retired == true);
                var dead = sim.State.notables.FirstOrDefault(n => !n.alive) ?? grown.Last(); // a death card's place is the stone either way

                var person = sim.Chronicle("friendship", $"{grown[0].FullName} and {grown[1].FullName} became friends.", 0.8f, grown[0].id);
                person.otherNotableId = grown[1].id;
                var building = sim.Chronicle("keeper", $"{grown[2].FullName} answered the Keeper.", 0.9f, grown[2].id);
                building.buildingId = standing.id;
                var gone = sim.Chronicle("invention", $"{grown[1].FullName} invented a five-wheeled barrow.", 0.7f, grown[1].id);
                gone.buildingId = retired != null ? retired.id : 999_999;
                var cases = new List<(string name, ChronicleEntry e, ShowMeSubject subject, bool echo)>
                {
                    ("person", person, ShowMeSubject.Person, true),
                    ("building", building, ShowMeSubject.Building, true),
                    ("gone-building", gone, ShowMeSubject.Person, true),
                };
                cases.Add(("death", sim.Chronicle("death", $"{dead.FullName} died.", 0.9f, dead.id), ShowMeSubject.Remembering, false));

                var audit = new JumpAudit(box);
                foreach (var c in cases)
                {
                    var before = (cam.target, cam.yaw, cam.pitch, cam.zoom);
                    var plan = d.Show(c.e, true);
                    Assert.IsNotNull(plan, $"{c.name}: a plan");
                    Assert.AreEqual(c.subject, plan.target.subject, $"{c.name}: {plan.target}");
                    if (c.name == "gone-building") Assert.AreEqual(ShowMeFallback.BuildingGone, plan.target.fallback);
                    Assert.AreEqual(c.echo, plan.HasEcho, $"{c.name}: echo {plan.echo.skip}");
                    bool echoed = false, flewThere = false, flower = false, shotLine = false, overHead = false;
                    int linesOver = d.EchoLinesOverHead;
                    float t = 0f;
                    while (d.State != ShowMeDirector.Phase.Idle && t < 25f)
                    {
                        audit.Step();
                        if (d.State == ShowMeDirector.Phase.Waiting) flewThere = true;
                        if (d.CurrentEcho != null && !echoed) echoed = true;
                        if (d.EchoBubbleShown && !overHead && pv.TryGetDollFeet(d.EchoSpeaker, out var feet))
                        {
                            var fs = Camera.main.WorldToScreenPoint(feet);
                            overHead = true;
                            Assert.Greater(d.BubbleScreen.y, fs.y, $"{c.name}: the line sits above the speaker's feet");
                            Assert.Less(d.BubbleScreen.y - fs.y, 0.2f * Screen.height, $"{c.name}: and right over their head");
                            Assert.Less(Mathf.Abs(d.BubbleScreen.x - fs.x), 0.05f * Screen.width, $"{c.name}: not off to the side");
                        }
                        if (echoed && !shotLine && d.EchoBubbleShown)
                        {
                            shotLine = true;
                            for (int f = 0; f < 3; f++) { yield return null; audit.Step(); }
                            var line = d.GetComponentInChildren<UIDocument>().rootVisualElement.Q("showme-bubble");
                            var lb = line.worldBound;
                            if (d.EchoBubbleShown) Assert.IsTrue(lb.xMin >= 0f && lb.xMax <= line.panel.visualTree.layout.width && lb.yMin >= 0f, $"{c.name}: the line stays on screen: {lb}");
                            yield return Shot("showme-" + c.name);
                        }
                        if (!c.echo && d.State == ShowMeDirector.Phase.Hold && !flower)
                        {
                            flower = GameObject.Find("ShowMeFlower") != null;
                            yield return Shot("showme-" + c.name);
                        }
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Debug.Log($"[ShowMePlay] {c.name}: {plan.target}; echo {(echoed ? d.CurrentEcho?.spot.ToString() ?? "played" : "none")}; line \"{d.LastLine}\"; {t:0.0}s");
                    Assert.AreEqual(ShowMeDirector.Phase.Idle, d.State, $"{c.name}: it ends");
                    Assert.IsTrue(flewThere, $"{c.name}: the camera flew there");
                    Assert.AreEqual(c.echo, echoed, $"{c.name}: the people echo it");
                    if (c.echo && !string.IsNullOrEmpty(plan.echo.line))
                    {
                        StringAssert.Contains(plan.echo.line, d.LastLine, $"{c.name}: the echo's line is said");
                        Assert.Greater(d.EchoLinesOverHead, linesOver, $"{c.name}: over the speaker's head, not at the fixed spot");
                        Assert.IsTrue(overHead, $"{c.name}: and it showed");
                    }
                    if (!c.echo) Assert.IsTrue(flower, "a flower at the stone");
                    Assert.IsTrue(d.Returned, $"{c.name}: the camera comes back");
                    Assert.Less(Flat(cam.target, before.target), 0.05f, $"{c.name}: back where it was");
                    Assert.AreEqual(before.zoom, cam.zoom, 0.02f, $"{c.name}: and as close");
                    Assert.AreEqual(0, pv.Planner.ActiveEchoes(pv.Now, new List<RelicMomentInfo>()), "the echo is over");
                    Assert.IsNull(GameObject.Find("ShowMeFlower"), "the flower goes with it");
                    for (int i = 0; i < 20; i++) { audit.Step(); yield return null; }
                }
                Debug.Log($"[ShowMePlay] jump audit: frames {audit.frames} jumps {audit.jumps} worst {audit.worst:0.000} {string.Join("; ", audit.log)}");
                Assert.Greater(audit.frames, 100);
                Assert.AreEqual(0, audit.jumps, "no doll jumps: " + string.Join("; ", audit.log));
                Assert.IsEmpty(errors, "no console errors: " + string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator ShowMe_ATouchKeepsTheCameraWhereThePlayerPutsIt()
        {
            yield return Open();
            try
            {
                yield return GrowInto(Era.Village);
                ShowMeDirector.BubblesOn = false;
                var sim = GameClock.Instance.Sim;
                var d = ShowMeDirector.Instance;
                var touch = InputSystem.AddDevice<Touchscreen>();
                var a = sim.State.notables.Where(n => n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).First();
                var e = sim.Chronicle("song", $"{a.FullName} made up a song.", 0.7f, a.id);
                Assert.IsNotNull(d.Show(e, true));
                yield return null; yield return null;
                var p = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
                BeginTouch(1, p, queueEventOnly: true);
                for (int i = 0; i < 6; i++) yield return null;
                EndTouch(1, p, delta: Vector2.zero, queueEventOnly: true);
                float t = 0f;
                while (d.State != ShowMeDirector.Phase.Idle && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
                Assert.IsTrue(d.Interrupted, "the touch took the camera");
                Assert.IsFalse(d.Returned, "the camera stays where the player has it");
                Assert.AreEqual(ShowMeDirector.Phase.Idle, d.State);
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator Bubbles_ShowInPlayAndKeepQuietInCameraMode()
        {
            yield return Open();
            try
            {
                yield return GrowInto(Era.Village);
                var d = ShowMeDirector.Instance;
                var hud = GameHud.Instance;
                var cam = Camera.main.GetComponent<BoxCamera>();
                cam.FlyTo(Vector3.zero, 0.75f, 0.2f);
                yield return new WaitForSecondsRealtime(0.5f);
                Assume.That(d.Bubbles, Is.Not.Null);
                d.Bubbles.rules.minGapSeconds = 3f; d.Bubbles.rules.speakerGapSeconds = 6f; // a quicker test, the same rules
                Assert.IsNull(d.QuietReason(), "normal play: they may speak");
                float t = 0f;
                while (d.ShownBubble == null && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
                Assert.IsNotNull(d.ShownBubble, "someone says something over their head");
                Debug.Log($"[ShowMePlay] bubble after {t:0.0}s: {d.ShownBubble}");
                Assert.IsTrue(PeopleVoice.Allowed(d.ShownBubble.text, GameClock.Instance.Sim));
                yield return null; yield return null; yield return null;
                var el = d.GetComponentInChildren<UIDocument>().rootVisualElement.Q("showme-bubble");
                var wb = el.worldBound;
                float pw = el.panel.visualTree.layout.width;
                Assert.IsTrue(wb.xMin >= 0f && wb.xMax <= pw && wb.yMin >= 0f, $"the bubble stays on screen: {wb}, panel width {pw}");
                yield return Shot("showme-bubble");

                hud.SetCameraMode(true);
                yield return null; yield return null;
                Assert.IsNull(d.ShownBubble, "camera mode: the bubble goes");
                int shown = d.BubblesShown;
                yield return new WaitForSecondsRealtime(8f);
                Assert.AreEqual(shown, d.BubblesShown, "and nobody speaks in camera mode");
                Assert.AreEqual("camera mode", d.QuietReason());
                yield return Shot("showme-cameramode-quiet");
                hud.SetCameraMode(false);
                yield return null;
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }
    }
}
