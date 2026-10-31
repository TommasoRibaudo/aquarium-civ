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
    /// gold tag follows the doll and survives a save and reload, and no doll jumps while a card opens. Screenshots go to
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

                yield return TapAt(at);
                yield return Until(() => cards.IsOpen, 2f);
                Assert.IsTrue(cards.IsOpen, "a tap on a doll opens the card");
                Assert.AreEqual(expect, cards.OpenId, "the card is the tapped person's");
                yield return new WaitForSecondsRealtime(GlassVoice.TapGap + 0.2f);
                Assert.AreEqual(knocks, glass.Knocks, "a tap on a person is no knock");
                foreach (int n in shownIds)
                    if (before.TryGetValue(n, out var b0) && pv.TryGetDollFeet(PersonRef.Notable(n), out var b1))
                        Assert.Less(Vector3.Distance(b0, b1), 0.25f, $"doll {n} didn't jump when the card opened");

                var card = cards.CardRoot.Q("person-card");
                Assert.AreEqual(sim.FindNotable(expect).FullName, (cards.CardRoot.Q<Label>("person-card-name")).text);
                Assert.Less(card.worldBound.height, cards.CardRoot.worldBound.height * (PersonCardView.CardMaxShare + 0.01f), "the card leaves the world above it");
                yield return new WaitForSecondsRealtime(1.2f); // the flight lands
                Assert.IsTrue(DollScreen(expect, out var after) && after.y > Screen.height * 0.5f, "the person stands above the card");
                yield return Shot("pc-card");
                // the stories at the bottom of the card, and one "Show me"
                var scroll = cards.CardRoot.Q<ScrollView>("person-card-body");
                scroll.scrollOffset = new Vector2(0f, 99999f);
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
                scroll.scrollOffset = Vector2.zero;

                // lines to the ties: zoom out so their dolls are in view
                cards.Open(id, false);
                cam.FlyTo(Vector3.zero, 1f, 0.2f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Until(() => cards.LinesDrawn > 0, 2f);
                Assert.Greater(cards.LinesDrawn, 0, "lines run to their friend and rival");
                Assert.LessOrEqual(cards.LinesDrawn, PersonCardView.MaxLines);
                var n0 = sim.FindNotable(id);
                if (DollScreen(n0.friendId, out _)) CollectionAssert.Contains(cards.LinesTo, n0.friendId);
                yield return Shot("pc-lines");

                // tap-through to a tie: their card opens
                var chip = cards.CardRoot.Q<Button>("person-card-tie-" + n0.friendId);
                if (chip != null && chip.worldBound.yMax < card.worldBound.yMax)
                {
                    yield return TapAt(GameUiPanel.ScreenCenter(chip));
                    yield return Until(() => cards.OpenId == n0.friendId, 1.5f);
                    Assert.AreEqual(n0.friendId, cards.OpenId, "a tie chip opens that person's card");
                }

                // Close: the card and the lines go
                yield return TapAt(GameUiPanel.ScreenCenter(cards.CardRoot.Q("person-card-close")));
                yield return null; yield return null;
                Assert.IsFalse(cards.IsOpen, "Close closes it");
                Assert.AreEqual(0, cards.LinesDrawn, "the lines clear with the card");
                Assert.IsEmpty(errors, string.Join("\n", errors));
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
                Assert.IsEmpty(errors, string.Join("\n", errors));
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

                // the chip opens their card
                yield return TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root, "hud-pin"));
                yield return Until(() => cards.IsOpen, 1f);
                Assert.AreEqual(who.id, cards.OpenId, "the pin chip opens the pinned person's card");
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
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }

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
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }
    }
}
