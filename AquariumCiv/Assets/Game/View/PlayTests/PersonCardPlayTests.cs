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
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Notables in the view (#39) in the real Slice scene at 1179x2556, by touch: a tap on a notable's doll opens their card
    /// (and is no knock), their ties' lines draw and clear when the card closes, a tap on empty glass still knocks, the pin's
    /// gold tag follows the doll and survives a save and reload, no doll jumps while a card opens, and the Keeper's Glass card (#165)
    /// holds the board at both phone sizes. Screenshots go to
    /// Temp/agent (pc-*.png). Run alone: <c>--filter PersonCardPlay --filter_type category</c>. Backs up and restores the save.
    /// </summary>
    [Category("PersonCardPlay")]
    public class PersonCardPlayTests : InputTestFixture
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

        IEnumerator Open(bool backupSave = true, int w = 1179, int h = 2556)
        {
            LogAssert.ignoreFailingMessages = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            GameUiPanel.AcceptInputUnfocused(true); // UI Toolkit drops taps while the Editor is behind another window
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            if (backupSave)
            {
                savePath = GameClock.DefaultSavePath;
                backup = savePath + ".card-test-backup";
                if (File.Exists(savePath)) { File.Copy(savePath, backup, true); File.Delete(savePath); }
                if (File.Exists(savePath + ".bak")) { File.Copy(savePath + ".bak", backup + ".bak", true); File.Delete(savePath + ".bak"); }
                GameClock.DebugNextSeed = 1234; // a pinned world, so a failure replays (tooling.md)
            }
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
                if (box != null && ShowMeDirector.Instance == null) box.gameObject.AddComponent<ShowMeDirector>();
                if (box != null && PersonCardView.Instance == null) box.gameObject.AddComponent<PersonCardView>();
            }
            ShowMeDirector.BubblesOn = false;
            yield return null; yield return null;
            if (touch == null) touch = InputSystem.AddDevice<Touchscreen>();
            yield return null;
            errors.Clear();
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        void Close()
        {
            Application.logMessageReceived -= OnLog;
            GameClock.DebugNextSeed = 0;
            ShowMeDirector.BubblesOn = true;
            if (PersonCardView.Instance != null) PersonCardView.Instance.Close();
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

        /// <summary>Grow the box a few decades (friends, rivals, families), clean the glass, midday.</summary>
        static IEnumerator Grow(float years = 30f)
        {
            for (int i = 0; i < years / 10f; i++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            GameClock.Instance.Sim.CleanAll();
            PeopleView.DebugSetHour(11f);
            yield return null; yield return null;
        }

        /// <summary>#207: how each shown doll moved, frame by frame, while the card opened: the longest step in one frame, the fastest, and the whole way.</summary>
        sealed class DollStepWatch : MonoBehaviour
        {
            PeopleView pv;
            readonly Dictionary<int, Vector3> last = new(), first = new();
            readonly Dictionary<int, (float step, float speed, float frameDt)> worst = new();
            float started;
            public void Begin(PeopleView view, List<int> ids)
            {
                pv = view; started = Time.realtimeSinceStartup;
                foreach (int n in ids) if (pv.TryGetDollFeet(PersonRef.Notable(n), out var w)) { last[n] = w; first[n] = w; }
            }
            void LateUpdate()
            {
                if (pv == null) return;
                float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
                foreach (int n in new List<int>(last.Keys))
                {
                    if (!pv.TryGetDollFeet(PersonRef.Notable(n), out var w)) continue;
                    float step = Vector3.Distance(w, last[n]);
                    last[n] = w;
                    if (!worst.TryGetValue(n, out var had) || step > had.step) worst[n] = (step, step / dt, dt);
                }
            }
            public float Elapsed => Time.realtimeSinceStartup - started;
            public float WorstStep { get { float m = 0f; foreach (var v in worst.Values) m = Mathf.Max(m, v.step); return m; } }
            public string Report()
            {
                var s = new System.Text.StringBuilder($"over {Elapsed:0.00} s: ");
                foreach (var (n, v) in worst)
                    if (v.step > 0.002f) s.Append($"doll {n} way {Vector3.Distance(first[n], last[n]):0.000} worst step {v.step:0.000} in {v.frameDt:0.000} s; ");
                return s.ToString();
            }
        }

        /// <summary>Screen point (pixels, y up) at the middle of a notable's doll; false if it isn't shown on screen.</summary>
        static bool DollScreen(int id, out Vector2 p)
        {
            p = default;
            var pv = PeopleView.Find();
            var cam = Camera.main;
            if (pv == null || !pv.TryGetDollTop(PersonRef.Notable(id), out var top) || !pv.TryGetDollFeet(PersonRef.Notable(id), out var feet)) return false;
            var s = cam.WorldToScreenPoint((top + feet) * 0.5f);
            p = new Vector2(s.x, s.y);
            return s.z > 0f && s.x > 40f && s.x < Screen.width - 40f && s.y > Screen.height * 0.2f && s.y < Screen.height * 0.85f;
        }

        /// <summary>A notable with a doll on screen after flying to them, with at least one living tie; ties forced if needed.</summary>
        static int PickSubject(SimCore sim)
        {
            var pv = PeopleView.Find();
            var shown = new List<int>();
            pv.NotablesShown(shown);
            var adults = shown.Select(sim.FindNotable).Where(n => n != null && n.alive && n.age >= SimCore.AdultAge).OrderBy(n => n.id).ToList();
            Assume.That(adults.Count, Is.GreaterThanOrEqualTo(3), "three grown notables in view");
            var a = adults[0];
            // a friend and a rival who are out and about, so the lines have somewhere to go
            var f = adults[1]; var r = adults[2];
            if (sim.FindNotable(a.friendId)?.alive != true) { a.friendId = f.id; f.friendId = a.id; }
            if (sim.FindNotable(a.rivalId)?.alive != true && a.friendId != r.id) { a.rivalId = r.id; r.rivalId = a.id; }
            return a.id;
        }

        [UnityTest]
        [Category("Part3")]
        public IEnumerator TapADollOpensTheirCardAndTheirLines()
        {
            yield return Open();
            try
            {
                yield return Grow();
                var sim = GameClock.Instance.Sim;
                var cards = PersonCardView.Instance;
                var glass = GlassVoice.Instance;
                Assert.IsNotNull(cards, "the person card view is on the box");
                int id = PickSubject(sim);
                var cam = Camera.main.GetComponent<BoxCamera>();
                cam.FlyToPerson(PersonRef.Notable(id), 0.6f, 0.3f);
                yield return new WaitForSecondsRealtime(0.6f);
                PeopleView.Find().timeScale = 0.05f; // the dolls barely move while we aim
                yield return null;
                Assume.That(DollScreen(id, out var at), "their doll is on screen");
                // pick the doll nearest that point the way the view does, so a neighbour standing close isn't a false failure
                int expect = cards.Pick(at);
                Assume.That(expect, Is.GreaterThan(0), "a doll is under the point");
                int knocks = glass.Knocks;

                // no doll jumps while the card opens and the camera flies
                var pv = PeopleView.Find();
                var shownIds = new List<int>(); pv.NotablesShown(shownIds);
                var before = new Dictionary<int, Vector3>();
                foreach (int n in shownIds) if (pv.TryGetDollFeet(PersonRef.Notable(n), out var w)) before[n] = w;

                var watch = new GameObject("doll step watch").AddComponent<DollStepWatch>();
                watch.Begin(pv, shownIds);
                yield return TapAt(at);
                yield return Until(() => cards.IsOpen, 2f);
                Assert.IsTrue(cards.IsOpen, "a tap on a doll opens the card");
                Assert.AreEqual(expect, cards.OpenId, "the card is the tapped person's");
                yield return new WaitForSecondsRealtime(GlassVoice.TapGap + 0.2f);
                Assert.AreEqual(knocks, glass.Knocks, "a tap on a person is no knock");
                // #207: a jump is a long step in one frame. The whole way was never the measure: the window is a second, and a doll gliding into its slot at the
                // walk's pace with the catch-up covers 0.3 u in it (doll 21: 0.298 u in 1.02 s, longest step 0.071 u in a 0.16 s frame).
                Assert.Less(watch.WorstStep, 0.12f, "a doll jumped when the card opened: " + watch.Report());
                foreach (int n in shownIds)
                    if (before.TryGetValue(n, out var b0) && pv.TryGetDollFeet(PersonRef.Notable(n), out var b1))
                        Assert.Less(Vector3.Distance(b0, b1), 0.05f + 2f * LifePlanner.RealWalkSpeed * watch.Elapsed, $"doll {n} outran a walk while the card opened; {watch.Report()}");
                UnityEngine.Object.Destroy(watch.gameObject);

                var card = cards.CardRoot.Q("person-card");
                Assert.AreEqual(sim.FindNotable(expect).FullName, (cards.CardRoot.Q<Label>("person-card-name")).text);
                Assert.Less(card.worldBound.height, cards.CardRoot.worldBound.height * (PersonCardView.CardMaxShare + 0.01f), "the card leaves the world above it");
                yield return new WaitForSecondsRealtime(1.2f); // the flight lands
                Assert.IsTrue(DollScreen(expect, out var after) && after.y > Screen.height * 0.5f, "the person stands above the card");
                yield return Shot("pc-card");
                // the stories under their tab, and one "Show me"
                var scroll = cards.CardRoot.Q<ScrollView>("person-card-body");
                yield return TapAt(GameUiPanel.ScreenCenter(cards.CardRoot.Q("person-card-recent")));
                Assert.AreEqual(CardTab.Recent, cards.Tab, "a tap on Recent");
                yield return null; yield return null;
                yield return Shot("pc-card-stories");
                var show = cards.CardRoot.Q<Button>("person-card-story-0-show");
                if (show != null && show.worldBound.yMax < Screen.height * 10f)
                {
                    yield return TapAt(GameUiPanel.ScreenCenter(show));
                    yield return new WaitForSecondsRealtime(1.5f);
                    Assert.IsTrue(cards.IsOpen, "Show me keeps the card open");
                    Debug.Log("[PersonCardPlay] show me: " + ShowMeDirector.Status());
                    yield return Shot("pc-showme");
                }

                // lines to the ties: zoom out so their dolls are in view
                yield return TapAt(GameUiPanel.ScreenCenter(cards.CardRoot.Q("person-card-about")));
                cards.Open(id, false);
                cam.FlyTo(Vector3.zero, 1f, 0.2f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Until(() => cards.LinesDrawn > 0, 2f);
                Assert.Greater(cards.LinesDrawn, 0, "lines run to their friend and rival");
                Assert.LessOrEqual(cards.LinesDrawn, PersonCardView.MaxLines);
                var n0 = sim.FindNotable(id);
                // #118: a tie within 20 panel px is skipped by design and the top MaxLines come first, so the friend's line is only owed when it's far enough
                float apart = 60f * Screen.width / Mathf.Max(1f, cards.CardRoot.panel.visualTree.layout.width);
                if (cards.LinesDrawn < PersonCardView.MaxLines && DollScreen(n0.friendId, out var fp) && DollScreen(id, out var mp) && (fp - mp).magnitude > apart)
                    CollectionAssert.Contains(cards.LinesTo, n0.friendId);
                yield return Shot("pc-lines");

                // tap-through to a tie: their card opens
                var chip = cards.CardRoot.Q<Button>("person-card-tie-" + n0.friendId);
                if (chip != null)
                {
                    scroll.ScrollTo(chip);
                    yield return null; yield return null;
                    yield return TapAt(GameUiPanel.ScreenCenter(chip));
                    yield return Until(() => cards.OpenId == n0.friendId, 1.5f);
                    Assert.AreEqual(n0.friendId, cards.OpenId, "a tie chip opens that person's card");
                }

                // Close: the card and the lines go
                yield return TapAt(GameUiPanel.ScreenCenter(cards.CardRoot.Q("person-card-close")));
                yield return null; yield return null;
                Assert.IsFalse(cards.IsOpen, "Close closes it");
                Assert.AreEqual(0, cards.LinesDrawn, "the lines clear with the card");
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TapACrowdDollOpensTheirSmallCard()
        {
            yield return Open();
            try
            {
                yield return Grow(20f);
                var sim = GameClock.Instance.Sim;
                var cards = PersonCardView.Instance;
                var pv = PeopleView.Find();
                pv.timeScale = 0.05f;
                yield return null;
                var crowd = new List<PersonRef>();
                pv.CrowdShown(crowd);
                PersonRef who = PersonRef.None;
                var cam = Camera.main;
                var at = Vector2.zero;
                foreach (var c in crowd)
                {
                    if (!pv.TryGetDollTop(c, out var top) || !pv.TryGetDollFeet(c, out var feet)) continue;
                    var s = cam.WorldToScreenPoint((top + feet) * 0.5f);
                    var p = new Vector2(s.x, s.y);
                    if (s.z <= 0f || s.x < 60f || s.x > Screen.width - 60f || s.y < Screen.height * 0.25f || s.y > Screen.height * 0.85f) continue;
                    if (!cards.PickPerson(p).IsCrowd) continue; // a notable in front of them is a notable's tap
                    who = cards.PickPerson(p); at = p;
                    break;
                }
                Assume.That(who.IsCrowd, "a crowd doll on screen with nobody in front");
                int knocks = GlassVoice.Instance.Knocks;
                yield return TapAt(at);
                yield return null; yield return null;
                Assert.IsTrue(cards.CrowdOpen, "a tap on a crowd doll opens their card");
                Assert.IsFalse(cards.IsOpen, "and no notable's");
                Assert.AreEqual(who.crowdIndex, cards.CrowdIndex);
                Assert.AreEqual(knocks, GlassVoice.Instance.Knocks, "it is no knock");
                yield return new WaitForSecondsRealtime(0.5f);

                var root = cards.CardRoot;
                Assert.AreEqual(sim.CrowdName(who.crowdIndex), root.Q<Label>("crowd-card-name").text, "their own first name");
                Assert.IsNotEmpty(root.Q<Label>("crowd-card-job").text);
                Assert.That(root.Q<Label>("crowd-card-doing").text, Does.Contain(cards.Crowd.name));
                Assert.AreEqual(DisplayStyle.None, root.Q("person-card").resolvedStyle.display, "no stories, family or pin: the big card stays away");
                var sheet = root.Q("crowd-card");
                Assert.Less(sheet.worldBound.height / root.worldBound.height, 0.3f, "smaller than a person's");
                Assert.LessOrEqual(sheet.worldBound.yMax, root.worldBound.height + 0.5f, "on the bottom edge");
                Assert.AreEqual("crowd " + who.crowdIndex, who.ToString());
                Assert.AreEqual(who.crowdIndex, cards.CrowdIndex);
                Assert.IsTrue(MomentPlayer.PromptsBlocked(), "prompts wait for it");
                yield return Shot("pc-crowd");

                yield return TapAt(GameUiPanel.ScreenCenter(root.Q("crowd-card-close")));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsFalse(cards.CrowdOpen, "Close closes it");
                Assert.AreEqual(DisplayStyle.None, root.Q("crowd-card").resolvedStyle.display);
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        IEnumerator SwipeFrom(Vector2 from, float dy)
        {
            BeginTouch(touchId, from, queueEventOnly: true);
            yield return null; yield return null;
            for (int i = 1; i <= 6; i++) { MoveTouch(touchId, from + new Vector2(0f, dy * i / 6f), queueEventOnly: true); yield return null; }
            EndTouch(touchId, from + new Vector2(0f, dy), delta: Vector2.zero, queueEventOnly: true);
            yield return null; yield return null;
            touchId++;
        }

        /// <summary>#403 (note #384): the handle grows the card to 59 % and the person stays above it; a swipe down on the head brings it
        /// back to 30 %; a closed card opens at 30 % again. Shots: pc-tall, pc-tall-short.</summary>
        [UnityTest]
        public IEnumerator TheirTagSaysWhatTheyAreDoingNow()
        {
            // #653 (playtest): while a notable's card is open, their tag over the doll says what they're doing now
            yield return Open(true, 1179, 2556);
            try
            {
                yield return Grow();
                var sim = GameClock.Instance.Sim;
                var cards = PersonCardView.Instance;
                int id = PickSubject(sim);
                Assert.IsTrue(cards.Open(id, true), "the card opens");
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.IsNotEmpty(cards.DoingNow, "the planner says what they're doing");
                yield return Shot("pc-doing");
                StringAssert.DoesNotContain(" at ", cards.DoingNow, "the verb, not the place");
                var n = sim.FindNotable(id);
                bool shown = false;
                foreach (var doc in UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None))
                    doc.rootVisualElement?.panel?.visualTree.Query<UnityEngine.UIElements.Label>().ForEach(l => { if (l.text == n.name + " · " + cards.DoingNow && l.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex) shown = true; });
                Assert.IsTrue(shown, $"a tag over them reads '{n.name} · {cards.DoingNow}'");
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator TheHandleGrowsTheCardAndThePersonStaysAboveIt()
        {
            foreach (var (w, h, shot) in new[] { (1179, 2556, "pc-tall"), (750, 1334, "pc-tall-short") })
            {
                yield return Open(true, w, h);
                try
                {
                    yield return Grow();
                    var sim = GameClock.Instance.Sim;
                    var cards = PersonCardView.Instance;
                    int id = PickSubject(sim);
                    PeopleView.Find().timeScale = 0.05f;
                    cards.Open(id, true);
                    yield return new WaitForSecondsRealtime(1.2f);
                    var card = cards.CardRoot.Q("person-card");
                    float full = cards.CardRoot.worldBound.height;
                    Assert.IsFalse(cards.Expanded, "a card opens at 30 %");
                    Assert.Less(card.worldBound.height, full * (PersonCardView.CardMaxShare + 0.01f));
                    float shortBody = cards.CardRoot.Q<ScrollView>("person-card-body").worldBound.height;

                    yield return TapAt(GameUiPanel.ScreenCenter(cards.Grab));
                    yield return new WaitForSecondsRealtime(1.4f); // the flight lands
                    Assert.IsTrue(cards.Expanded, "a tap on the handle grows the card");
                    Assert.AreEqual(full * PersonCardView.ExpandedShare, card.worldBound.height, full * 0.01f, "the tall card is 59 %");
                    float tallBody = cards.CardRoot.Q<ScrollView>("person-card-body").worldBound.height;
                    var bc = Camera.main.GetComponent<BoxCamera>();
                    PeopleView.Find().TryGetDollFeet(PersonRef.Notable(id), out var feet);
                    Debug.Log($"[403] cam target {bc.target} zoom {bc.zoom:0.00} pitch {bc.pitch:0.0} yaw {bc.yaw:0} · feet {feet} · want {ShowMeDirector.AboveCard(bc, feet, bc.zoom, bc.pitch, PersonCardView.ExpandedAnchorY)} · doll y {(DollScreen(id, out var dy) ? dy.y / Screen.height : -1f):0.00} · bounds {BoxCameraMath.PanBounds(bc.zoom)}");
                    Assert.Greater(tallBody, shortBody * 2f, $"the scroll area grows ({shortBody:0} → {tallBody:0} px)");
                    Assert.IsTrue(DollScreen(id, out var at) && at.y > Screen.height * PersonCardView.ExpandedShare, $"the person stands above the tall card (y {at.y:0} of {Screen.height})");
                    Assert.IsTrue(cards.IsOpen, "the handle is no world tap");
                    yield return Shot(shot);

                    yield return SwipeFrom(GameUiPanel.ScreenCenter(cards.CardRoot.Q("person-card-name")), -Screen.height * 0.08f); // screen y up: down
                    yield return null;
                    Assert.IsFalse(cards.Expanded, "a swipe down on the head brings it back to 30 %");
                    yield return SwipeFrom(GameUiPanel.ScreenCenter(cards.Grab), Screen.height * 0.08f);
                    Assert.IsTrue(cards.Expanded, "a swipe up on the handle grows it");
                    cards.Close();
                    yield return new WaitForSecondsRealtime(0.5f);
                    cards.Open(id, false);
                    yield return null; yield return null;
                    Assert.IsFalse(cards.Expanded, "a card opens at 30 % again");
                    Assert.IsEmpty(errors, string.Join(" | ", errors));
                }
                finally { Close(); }
            }
        }

        /// <summary>The person's head on the card's panel (y down), or false off screen.</summary>
        static bool HeadOnPanel(PersonCardView cards, int id, out float y)
        {
            y = 0f;
            var pv = PeopleView.Find();
            if (!pv.TryGetDollTop(PersonRef.Notable(id), out var top)) return false;
            var s = Camera.main.WorldToScreenPoint(top);
            if (s.z <= 0f) return false;
            y = RuntimePanelUtils.ScreenToPanel(cards.CardRoot.panel, new Vector2(s.x, Screen.height - s.y)).y;
            return true;
        }

        /// <summary>#484, #485, #486 (the #403 tester's polish): the card grows behind the camera and never over its person; a person at the
        /// back who needs no flight still stands clear of the tall card; the pill takes a tap 44 pt tall.</summary>
        [UnityTest]
        public IEnumerator TheTallCardNeverCoversItsPerson_AndThePillIsEasyToHit()
        {
            yield return Open(true, 750, 1334); // the short phone, where the back person's feet touched the edge
            try
            {
                yield return Grow();
                var sim = GameClock.Instance.Sim;
                var cards = PersonCardView.Instance;
                PeopleView.Find().timeScale = 0.02f;
                var card = cards.CardRoot.Q("person-card");

                // #484: every frame of the grow, the head stays above the card's top
                int id = PickSubject(sim);
                cards.Open(id, true);
                yield return new WaitForSecondsRealtime(1.2f);
                cards.SetExpanded(true);
                int frames = 0, under = 0; float worst = 0f;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < PersonCardView.GrowSeconds + 0.3f)
                {
                    yield return null;
                    if (!HeadOnPanel(cards, id, out float hy)) continue;
                    frames++;
                    float over = hy - card.worldBound.yMin;
                    if (over > 2f) { under++; worst = Mathf.Max(worst, over); }
                }
                Debug.Log($"[484] grow: {frames} frames, head under the card's top in {under} (worst {worst:0} px)");
                Assert.Greater(frames, 10);
                Assert.AreEqual(0, under, $"the head went under the growing card (worst {worst:0} px)");
                Assert.AreEqual(cards.CardRoot.worldBound.height * PersonCardView.ExpandedShare, card.worldBound.height, cards.CardRoot.worldBound.height * 0.01f, "full height in the end");
                cards.Close();
                yield return new WaitForSecondsRealtime(0.4f);

                // #485: a person at the back (just clear of the tall card's edge, so no flight is needed today) still ends clear of it
                int back = -1;
                foreach (var n in sim.State.notables.Where(x => x.alive))
                {
                    cards.Open(n.id, true);
                    yield return new WaitForSecondsRealtime(1.1f);
                    if (DollScreen(n.id, out var p0) && p0.y > Screen.height * PersonCardView.ExpandedShare && p0.y < Screen.height * (PersonCardView.ExpandedShare + PersonCardView.ClearShare)) { back = n.id; break; }
                    cards.Close();
                    yield return null;
                }
                if (back >= 0)
                {
                    cards.SetExpanded(true);
                    yield return new WaitForSecondsRealtime(PersonCardView.GrowSeconds + 0.4f);
                    Assert.IsTrue(DollScreen(back, out var p1));
                    Debug.Log($"[485] back person {back}: middle at {p1.y / Screen.height:0.000} of the screen");
                    Assert.GreaterOrEqual(p1.y / Screen.height, PersonCardView.ExpandedShare + PersonCardView.ClearShare - 0.012f, "their middle clear of the tall card's edge");
                    cards.Close();
                    yield return new WaitForSecondsRealtime(0.4f);
                }
                else Debug.Log("[485] no person stood just over the edge in this box: the case isn't exercised here");

                // #486: the pill's tap area is 44 pt tall; a tap 30 pt above the card's edge grows it
                cards.Open(id, true);
                yield return new WaitForSecondsRealtime(1.2f);
                float pt = cards.CardRoot.worldBound.width / 393f;
                Assert.GreaterOrEqual(cards.Grab.worldBound.height, 120f, "44 pt tall");
                var g = cards.Grab.worldBound;
                var tapPanel = new Vector2(g.center.x, card.worldBound.yMin - 30f * pt);
                Assert.IsTrue(g.Contains(tapPanel), "30 pt above the edge is the pill's");
                float panelToScreen = Screen.width / cards.CardRoot.panel.visualTree.worldBound.width; // as GameUiPanel.ScreenCenter
                yield return TapAt(new Vector2(tapPanel.x * panelToScreen, Screen.height - tapPanel.y * panelToScreen));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsTrue(cards.Expanded, "the tap above the edge grew the card");
                Assert.IsTrue(cards.IsOpen, "and was no world tap");
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator ATapOnEmptyGlassStillKnocks()
        {
            yield return Open();
            try
            {
                yield return Grow(10f);
                var cards = PersonCardView.Instance;
                var glass = GlassVoice.Instance;
                PeopleView.Find().timeScale = 0.05f;
                yield return null;
                // a point on the glass with no doll near it
                Vector2 spot = default; bool found = false;
                for (float y = 0.3f; y <= 0.75f && !found; y += 0.05f)
                    for (float x = 0.15f; x <= 0.85f && !found; x += 0.1f)
                    {
                        var p = new Vector2(Screen.width * x, Screen.height * y);
                        if (CareView.Instance != null && !CareView.Instance.GlassAt(p)) continue;
                        if (UiInput.Over(p) || cards.Pick(p) >= 0) continue;
                        spot = p; found = true;
                    }
                Assume.That(found, "an empty spot on the glass");
                int knocks = glass.Knocks;
                for (int i = 0; i < 2; i++) yield return TapAt(spot);
                yield return Until(() => glass.Knocks > knocks, 3f);
                Assert.AreEqual(knocks + 1, glass.Knocks, "the taps are one knock");
                Assert.IsFalse(cards.IsOpen, "no card from empty glass");

                // with a card open, a tap on the world closes it (no knock)
                var sim = GameClock.Instance.Sim;
                cards.Open(sim.State.notables.First(n => n.alive).id, false);
                yield return null;
                yield return new WaitForSecondsRealtime(GlassVoice.TapGap + 0.1f);
                knocks = glass.Knocks;
                Vector2 up = new(Screen.width * 0.5f, Screen.height * 0.8f);
                if (cards.Pick(up) < 0 && !UiInput.Over(up))
                {
                    yield return TapAt(up);
                    yield return new WaitForSecondsRealtime(GlassVoice.TapGap + 0.2f);
                    Assert.IsFalse(cards.IsOpen, "a tap on the world closes the card");
                    Assert.AreEqual(knocks, glass.Knocks, "closing the card is no knock");
                }
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        [UnityTest]
        public IEnumerator PinShowsTheTagAndSurvivesSaveAndLoad()
        {
            yield return Open();
            try
            {
                yield return Grow(10f);
                var sim = GameClock.Instance.Sim;
                var cards = PersonCardView.Instance;
                var pv = PeopleView.Find();
                var shown = new List<int>(); pv.NotablesShown(shown);
                var who = shown.Select(sim.FindNotable).First(n => n != null && n.alive && n.age >= SimCore.AdultAge);
                cards.Open(who.id, true);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return TapAt(GameUiPanel.ScreenCenter(cards.CardRoot.Q("person-card-pin")));
                yield return null;
                Assert.AreEqual(who.id, sim.State.pinnedNotableId, "the card's Pin pins them");
                Assert.AreEqual("Pinned", cards.CardRoot.Q<Button>("person-card-pin").text);
                yield return Until(() => GameHud.Instance.PinChipText != null, 1f);
                Assert.AreEqual(who.name, GameHud.Instance.PinChipText, "the HUD chip shows the pin");
                yield return Shot("pc-pinned-card");
                cards.Close();
                yield return null; yield return null;
                Assert.IsTrue(cards.PinTagShown, "the gold tag is over the pinned doll");
                // it follows the doll
                Assert.IsTrue(pv.TryGetDollTop(PersonRef.Notable(who.id), out var top));
                var s = Camera.main.WorldToScreenPoint(top);
                Assert.Less(Vector2.Distance(new Vector2(s.x, s.y), cards.PinTagScreen), 4f, "the tag sits on their head");
                yield return Shot("pc-pin-tag");

                // D2: the chip's first tap flies to them, a second opens their card
                var flyCam = Camera.main.GetComponent<BoxCamera>();
                yield return TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root, "hud-pin"));
                yield return null; yield return null;
                Assert.IsFalse(cards.IsOpen, "the first tap only flies");
                Assert.IsTrue(flyCam.IsFlying, "the first tap flies");
                yield return Until(() => !flyCam.IsFlying, 2f);
                yield return TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root, "hud-pin"));
                yield return Until(() => cards.IsOpen, 1f);
                Assert.AreEqual(who.id, cards.OpenId, "the second tap opens the pinned person's card");
                cards.Close();

                // save, reload the scene: still pinned, the tag back
                Assert.IsTrue(GameClock.Instance.Save());
                int pinned = who.id;
                yield return Open(false);
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(pinned, GameClock.Instance.Sim.State.pinnedNotableId, "the pin is saved with the box");
                PeopleView.DebugSetHour(11f);
                Camera.main.GetComponent<BoxCamera>().FlyToPerson(PersonRef.Notable(pinned), 0.6f, 0.2f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Until(() => PersonCardView.Instance.PinTagShown, 2f);
                Assert.IsTrue(PersonCardView.Instance.PinTagShown, "the tag is back after loading");
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        /// <summary>A person with a doll in view, ties forced, for the checks at each phone size. They stay in view at the sheet's 30 %.</summary>
        IEnumerator CardUp(int w, int h, Action<int> done)
        {
            yield return Open(true, w, h);
            yield return Grow();
            var sim = GameClock.Instance.Sim;
            int id = PickSubject(sim);
            PeopleView.Find().timeScale = 0.05f;
            var cards = PersonCardView.Instance;
            Assert.IsTrue(cards.Open(id, true));
            yield return new WaitForSecondsRealtime(1.2f);
            done(id);
        }

        IEnumerator HoldsTheBoard(int w, int h)
        {
            int id = -1;
            yield return CardUp(w, h, i => id = i);
            try
            {
                var cards = PersonCardView.Instance;
                var root = cards.CardRoot;
                var sheet = root.Q<GlassSheet>("person-card");
                float H = root.worldBound.height;
                Assert.AreEqual(H * 0.3f, sheet.worldBound.height, H * 0.005f, "the sheet is 30 % of the screen");
                Assert.AreEqual(H, sheet.worldBound.yMax, 1f, "on the bottom edge");
                Assert.IsTrue(FrostSwitch.Frosted == sheet.Surface, "the card holds the frost");

                // the person stands clear above it, with their tag
                var pv = PeopleView.Find();
                Assert.IsTrue(pv.TryGetDollFeet(PersonRef.Notable(id), out var feet));
                float feetY = Screen.height - Camera.main.WorldToScreenPoint(feet).y;
                float sheetTop = sheet.worldBound.yMin * Screen.height / H;
                Assert.Less(feetY, sheetTop, "the card doesn't cover their feet");
                Assert.AreEqual(DisplayStyle.Flex, root.panel.visualTree.Q("pc-name-tag").resolvedStyle.display, "their name tag shows");

                // the subtitle has its own row: nothing runs under Pin
                var pin = root.Q("person-card-pin");
                var close = root.Q("person-card-close");
                var name = root.Q("person-card-name");
                var sub = root.Q("person-card-sub");
                Assert.GreaterOrEqual(sub.worldBound.yMin, pin.worldBound.yMax - 1f, "the subtitle is under Pin's row");
                Assert.LessOrEqual(name.worldBound.xMax, pin.worldBound.xMin + 1f, "the name stops before Pin");
                Assert.LessOrEqual(pin.worldBound.xMax, close.worldBound.xMin + 1f, "Pin before Close");
                Assert.LessOrEqual(close.worldBound.xMax, root.worldBound.xMax - GlassSheet.SidePad + 1f, "on the sheet's margin");

                // the first view shows a line and the ties, with nothing cut
                var view = root.Q<ScrollView>("person-card-body");
                Assert.GreaterOrEqual(view.worldBound.yMin, root.Q("person-card-tabs").worldBound.yMax - 1f, "the tab line clears the text");
                var face = root.Query<GlassChip>().First()?.Face;
                yield return Shot($"pc-board-{w}x{h}");
                if (face != null) Assert.LessOrEqual(face.worldBound.yMax, view.worldBound.yMax + 1f, "a row of chips shows under the line");

                // every control reaches a thumb
                foreach (var tab in root.Query<Button>(className: "kg-tab").ToList()) Assert.GreaterOrEqual(tab.worldBound.height, GameUiPanel.MinTarget - 1f, tab.name);
                foreach (var chip in root.Query<GlassChip>().ToList()) Assert.GreaterOrEqual(chip.worldBound.height, GameUiPanel.MinTarget - 1f, chip.name);
                Assert.IsTrue(((GlassButton)close).ContainsPoint(new Vector2(close.layout.width * 0.5f, -10f)), "Close reaches past its drawn edge");

                // tie labels: on the screen, above the card, never on the name tag, for as long as they show
                var cam = Camera.main.GetComponent<BoxCamera>();
                cards.Open(id, false);
                cam.FlyTo(Vector3.zero, 1f, 0.2f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Until(() => cards.LinesDrawn > 0, 2f);
                Assert.Greater(cards.LinesDrawn, 0, "lines run to their ties");
                var overlay = root.panel.visualTree.Q("pc-overlay");
                var tag = overlay.Q("pc-name-tag");
                int seen = 0;
                for (int f = 0; f < 30; f++)
                {
                    yield return null;
                    foreach (var l in overlay.Query<GlassTag>(className: "kg-tag--tie").ToList())
                    {
                        if (l.resolvedStyle.display == DisplayStyle.None) continue;
                        seen++;
                        Assert.IsFalse(l.worldBound.Overlaps(tag.worldBound), $"{l.name} sits on the name tag");
                        Assert.LessOrEqual(l.worldBound.yMax, sheet.worldBound.yMin + 1f, $"{l.name} is above the card");
                        Assert.GreaterOrEqual(l.worldBound.xMin, 0f, $"{l.name} is on screen");
                        Assert.LessOrEqual(l.worldBound.xMax, root.worldBound.xMax, $"{l.name} is on screen");
                    }
                }
                Assert.Greater(seen, 0, "labels were on screen");
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HoldsTheBoardOnATallPhone() => HoldsTheBoard(1179, 2556);

        [UnityTest, Timeout(120000)]
        public IEnumerator HoldsTheBoardOnAShortPhone() => HoldsTheBoard(750, 1334);

        [UnityTest, Timeout(120000)]
        public IEnumerator TabsSwitchAndANewPersonStartsOnAbout()
        {
            int id = -1;
            yield return CardUp(1179, 2556, i => id = i);
            try
            {
                var cards = PersonCardView.Instance;
                var root = cards.CardRoot;
                var hud = GameHud.Instance;
                Assert.AreEqual(CardTab.About, cards.Tab, "About first");
                Assert.AreEqual(hud.Root.Q("hud-history").resolvedStyle.visibility, Visibility.Hidden, "the tools step back");
                Assert.IsTrue(root.Q("person-card-portrait").resolvedStyle.width > 0f, "the portrait");
                Assert.IsTrue(root.Q("person-card-about").ClassListContains("kg-tab--on"));
                Assert.IsNotNull(root.Q("person-card-about-stories"), "question 35: About ends in a story");
                Assert.IsNull(root.Q("person-card-stories"), "the story lists wait under their tabs");

                yield return TapAt(GameUiPanel.ScreenCenter(root, "person-card-recent"));
                Assert.AreEqual(CardTab.Recent, cards.Tab);
                Assert.IsTrue(root.Q("person-card-recent").ClassListContains("kg-tab--on"));
                Assert.IsNotNull(root.Q("person-card-stories"));
                yield return TapAt(GameUiPanel.ScreenCenter(root, "person-card-best"));
                Assert.IsTrue(cards.ShowingBest);
                Assert.AreEqual("card tab: About", PersonCardView.DebugNextTab(), "the cheat goes round");

                // the toast rides above the card, the pin mark shows
                cards.ShowTab(CardTab.Recent);
                yield return TapAt(GameUiPanel.ScreenCenter(root, "person-card-pin"));
                yield return null; yield return null;
                Assert.IsTrue(root.Q("person-card-pin").ClassListContains("kg-btn--marked"), "Pinned has its mark");
                var toast = hud.Root.Q("hud-toast");
                yield return Until(() => toast.resolvedStyle.display == DisplayStyle.Flex, 1f);
                yield return null; yield return null;
                Assert.LessOrEqual(toast.worldBound.yMax, root.Q("person-card").worldBound.yMin, "the toast is above the card");
                Assert.AreEqual(CardTab.Recent, cards.Tab, "pinning stays on the tab");

                int other = GameClock.Instance.Sim.State.notables.First(n => n.alive && n.id != id).id;
                cards.Open(other, false);
                yield return null;
                Assert.AreEqual(CardTab.About, cards.Tab, "a new person starts on About");

                cards.Close();
                yield return null; yield return null;
                Assert.AreEqual(Visibility.Visible, hud.Root.Q("hud-history").resolvedStyle.visibility, "the tools come back with the card gone");
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }

        IEnumerator CardShots(int w, int h)
        {
            int id = -1;
            yield return CardUp(w, h, i => id = i);
            try
            {
                string at = $"-{w}";
                var cards = PersonCardView.Instance;
                var sim = GameClock.Instance.Sim;
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot("pc165-about" + at);
                cards.ShowTab(CardTab.Recent);
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Shot("pc165-recent" + at);
                cards.ShowTab(CardTab.Best);
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Shot("pc165-best" + at);
                cards.TogglePin();
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot("pc165-pinned" + at);
                cards.TogglePin();
                yield return new WaitForSecondsRealtime(3.5f);
                cards.ShowTab(CardTab.About);
                var cam = Camera.main.GetComponent<BoxCamera>();
                cards.Open(id, false);
                cam.FlyTo(Vector3.zero, 0.75f, 0.2f);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("pc165-lines" + at);
                FrostSwitch.On = false;
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Shot("pc165-flat" + at);
                FrostSwitch.On = true;
                cards.Close();
                yield return new WaitForSecondsRealtime(0.1f);

                int blank = sim.DebugBirth()?.id ?? -1; // a birth is their one story, so it goes
                if (blank >= 0) sim.State.chronicle.RemoveAt(sim.State.chronicle.Count - 1);
                if (blank >= 0)
                {
                    cards.Open(blank, true);
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Shot("pc165-no-stories-about" + at);
                    cards.ShowTab(CardTab.Recent);
                    yield return new WaitForSecondsRealtime(0.2f);
                    yield return Shot("pc165-no-stories" + at);
                    cards.Close();
                }
                for (int i = 0; i < 8; i++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
                var dead = sim.State.notables.Where(n => !n.alive).OrderByDescending(n => sim.PersonCardOf(n.id).memorial?.legendReasons.Count ?? 0).FirstOrDefault();
                if (dead != null)
                {
                    cards.Open(dead.id, true);
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Shot("pc165-legend" + at);
                    cards.Close();
                }
                yield return new WaitForSecondsRealtime(0.1f);
                Debug.Log("[PersonCardPlay] shots done, blank " + blank + ", dead " + (dead?.id ?? -1));
            }
            finally { FrostSwitch.On = FrostSwitch.Default; Close(); }
        }

        /// <summary>Every state at both phone sizes, for eyes. Temp/agent/pc165-*.png.</summary>
        [UnityTest, Explicit, Timeout(180000)]
        public IEnumerator ShotsTall() => CardShots(1179, 2556);

        [UnityTest, Explicit, Timeout(180000)]
        public IEnumerator ShotsShort() => CardShots(750, 1334);

        [UnityTest]
        public IEnumerator TheDeadGetAMemorialCard()
        {
            yield return Open();
            try
            {
                yield return Grow(70f);
                var sim = GameClock.Instance.Sim;
                var dead = sim.State.notables.LastOrDefault(n => !n.alive);
                Assume.That(dead, Is.Not.Null, "someone has died in 70 years");
                var cards = PersonCardView.Instance;
                Assert.IsTrue(cards.Open(dead.id, true));
                yield return null; yield return null;
                Assert.IsNotNull(cards.CardRoot.Q("person-card-memorial"), "the memorial section");
                Assert.AreEqual(DisplayStyle.None, cards.CardRoot.Q("person-card-pin").resolvedStyle.display, "the dead can't be pinned");
                Assert.AreEqual(0, cards.LinesDrawn);
                yield return Shot("pc-memorial");
                cards.Close();
                Assert.IsEmpty(errors, string.Join(" | ", errors));
            }
            finally { Close(); }
        }
    }
}
