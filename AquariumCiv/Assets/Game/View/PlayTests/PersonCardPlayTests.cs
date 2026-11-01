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
                if (DollScreen(n0.friendId, out _)) CollectionAssert.Contains(cards.LinesTo, n0.friendId);
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
                Assert.IsEmpty(errors, string.Join("\n", errors));
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
                Assert.IsEmpty(errors, string.Join("\n", errors));
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
                Assert.IsEmpty(errors, string.Join("\n", errors));
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
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
            finally { Close(); }
        }
    }
}
