using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Moments;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Settings by touch on the real Slice scene (#95) and the era notification's close and ask (#97): every setting stands after a restart, the
    /// reset needs its confirm and says what goes, rename keeps the first run's rules, the short phone's feedback, and the phone is
    /// asked only on a yes. The player's own settings are put back after each test.
    /// </summary>
    [Category("SettingsPlay")]
    public class SettingsPlayTests : PlayTestBase
    {
        sealed class FakePhone : ILocalNotifications
        {
            public int asks, cancels;
            public bool grants = true;
            public readonly List<(TimeSpan after, string title, string body)> scheduled = new();
            public void Prepare() { }
            public IEnumerator Ask(Action<bool> answered) { asks++; answered(grants); yield break; }
            public void Schedule(int id, TimeSpan after, string title, string body) { lock (scheduled) scheduled.Add((after, title, body)); }
            public void CancelAll() => cancels++;
        }

        static readonly string[] Keys = { "set_sound", "set_haptics", "set_music_freq", "set_music_vol", "set_sfx_vol", "set_amb_vol", EraNotifier.EnabledKey };
        readonly Dictionary<string, (bool has, float f, int i)> kept = new();

        [SetUp]
        public void Keep()
        {
            kept.Clear();
            foreach (var k in Keys) kept[k] = (PlayerPrefs.HasKey(k), PlayerPrefs.GetFloat(k, 0f), PlayerPrefs.GetInt(k, 0));
        }

        [TearDown]
        public void PutBack()
        {
            foreach (var k in Keys)
            {
                var (has, f, i) = kept[k];
                PlayerPrefs.DeleteKey(k);
                if (!has) continue;
                if (k.EndsWith("_vol")) PlayerPrefs.SetFloat(k, f); else PlayerPrefs.SetInt(k, i);
            }
            PlayerPrefs.Save();
            GameClock.PlatformOverride = null;
            DeviceSilent.DebugForce = false;
            GlassKeyboard.Debug = false;
        }

        GameHud Hud => GameHud.Instance;
        VisualElement Root => Hud.Root;

        IEnumerator Tap(VisualElement root, string name)
        {
            var e = root.Q(name);
            Assert.IsNotNull(e, name);
            yield return TapAt(GameUiPanel.ScreenCenter(e));
        }

        IEnumerator OpenSettings()
        {
            yield return Tap(Root, "hud-title");
            yield return Until(() => Hud.OpenSheet == GameHud.Settings, 1f);
            yield return new WaitForSecondsRealtime(0.4f); // the slide
        }

        /// <summary>Brings a row of the sheet into view, as a finger would.</summary>
        IEnumerator Reveal(string name)
        {
            var scroll = Hud.SettingsView.Scroll;
            scroll.View.ScrollTo(Root.Q(name));
            yield return Frames(4);
        }

        IEnumerator SlideTo(string name, float value)
        {
            yield return Reveal(name);
            float scrolled = Hud.SettingsView.Scroll.View.scrollOffset.y;
            var b = Root.Q(name).worldBound;
            var from = GameUiPanel.ScreenCenter(Root, name);
            float wide = b.width * Screen.width / GameUiPanel.ReferenceWidth, left = from.x - wide * 0.5f;
            // Sideways with a little wander: a finger is never straight, and the sheet mustn't take it.
            var to = new Vector2(left + wide * value, from.y);
            yield return Drag(new[] { from, from + new Vector2((to.x - from.x) * 0.5f, Screen.height * 0.012f), to }, Screen.width * 0.03f);
            yield return Frames(3);
            Assert.AreEqual(scrolled, Hud.SettingsView.Scroll.View.scrollOffset.y, 1f, "a slider keeps its finger: the sheet stays put");
        }

        [UnityTest]
        public IEnumerator EverySettingStandsAfterARestart()
        {
            yield return OpenSlice(seed: 9501);
            yield return OpenSettings();
            Assert.AreEqual(0.59f, Root.Q("settings-sheet").worldBound.height / Root.worldBound.height, 0.004f, "settings stands at 59 %");
            Assert.IsTrue(Hud.SettingsView.Scroll.View.contentContainer.layout.height > Hud.SettingsView.Scroll.View.layout.height, "it scrolls");
            Assert.AreEqual(DisplayStyle.Flex, Hud.SettingsView.Scroll.Q(className: "kg-scroll-bar").resolvedStyle.display, "and the bar says so");

            yield return Tap(Root, "settings-sound");
            Assert.IsFalse(GameSettings.Sound);
            yield return Tap(Root, "settings-sound");
            Assert.IsTrue(GameSettings.Sound);
            yield return Tap(Root, "settings-sound");

            Sfx.ResetLog();
            yield return SlideTo("settings-effects", 0.25f);
            Assert.AreEqual(0.25f, GameSettings.SfxVolume, 0.05f, "the slider follows the finger");
            CollectionAssert.Contains(Sfx.LastAsked, CueLibrary.UiConfirm, "a live sample on release");
            yield return SlideTo("settings-music", 0.8f);
            Assert.AreEqual(0.8f, GameSettings.MusicVolume, 0.05f);
            yield return SlideTo("settings-ambience", 0.3f);
            Assert.AreEqual(0.3f, GameSettings.AmbienceVolume, 0.05f);
            float sfx = GameSettings.SfxVolume, musicVol = GameSettings.MusicVolume, amb = GameSettings.AmbienceVolume;

            yield return Reveal("settings-music-plays");
            yield return Tap(Root.Q("settings-music-plays"), "tab-rare");
            Assert.AreEqual(MusicFrequency.Rare, GameSettings.MusicFrequency);

            yield return Reveal("settings-haptics");
            yield return Tap(Root, "settings-haptics");
            Assert.IsFalse(GameSettings.Haptics);
            yield return Reveal("settings-notifications");
            yield return Tap(Root, "settings-notifications");
            Assert.IsTrue(GameSettings.Notifications, "the Editor's phone says yes");
            AssertNoErrors("settings");

            // A restart: the scene again, the box from its save, the prefs from the disk's copy.
            PlayerPrefs.Save();
            yield return OpenSlice(seed: 9501, freshSave: false);
            yield return OpenSettings();
            var sheet = Root.Q("settings-sheet");
            Assert.AreEqual("Off", sheet.Q<Button>("settings-sound").text, "sound stands");
            Assert.AreEqual(sfx, sheet.Q<GlassSlider>("settings-effects").Value, 0.001f, "effects stand");
            Assert.AreEqual(musicVol, sheet.Q<GlassSlider>("settings-music").Value, 0.001f, "music stands");
            Assert.AreEqual(amb, sheet.Q<GlassSlider>("settings-ambience").Value, 0.001f, "ambience stands");
            Assert.AreEqual(1, sheet.Q<GlassTabs>("settings-music-plays").Selected, "music plays stands");
            Assert.AreEqual("Off", sheet.Q<Button>("settings-haptics").text, "haptics stand");
            Assert.AreEqual("On", sheet.Q<Button>("settings-notifications").text, "notifications stand");
            AssertNoErrors("settings after a restart");
        }

        [UnityTest]
        public IEnumerator AFingerScrollsTheSheetToItsEndWithoutTappingARow()
        {
            yield return OpenSlice(seed: 9510);
            yield return OpenSettings();
            var view = Hud.SettingsView.Scroll.View;
            var at = GameUiPanel.ScreenCenter(Root, "settings-notifications") - new Vector2(Screen.width * 0.3f, 0f);
            yield return Drag(new[] { at, at + new Vector2(0f, Screen.height * 0.25f) }, Screen.height * 0.01f);
            yield return Frames(6);
            Assert.Greater(view.scrollOffset.y, 10f, "the rows moved");
            Assert.AreEqual(GameHud.Settings, Hud.OpenSheet, "and nothing else opened");
            AssertNoErrors("scroll");
        }

        [UnityTest]
        public IEnumerator HapticsBuzzOnceWhenTurnedOn()
        {
            yield return OpenSlice(seed: 9502);
            GameSettings.Haptics = false;
            yield return OpenSettings();
            yield return Reveal("settings-haptics");
            yield return new WaitForSecondsRealtime(0.1f);
            int before = AudioDirector.Instance.HapticsFired;
            yield return Tap(Root, "settings-haptics");
            yield return Frames(3);
            Assert.AreEqual(before + 1, AudioDirector.Instance.HapticsFired, "one buzz");
        }

        [UnityTest]
        public IEnumerator TheSilentNoticeLivesInTheSoundRow()
        {
            yield return OpenSlice(seed: 9503);
            yield return OpenSettings();
            Assert.AreEqual(DisplayStyle.None, Root.Q("settings-silent").resolvedStyle.display);
            DeviceSilent.DebugForce = true;
            yield return Frames(4);
            Assert.AreEqual(DisplayStyle.Flex, Root.Q("settings-silent").resolvedStyle.display, "the notice comes up while the sheet is open");
            Assert.AreEqual(0.59f, Root.Q("settings-sheet").worldBound.height / Root.worldBound.height, 0.004f, "and the sheet stays at 59 %");
            Assert.IsNotNull(Root.Q("settings-sound").parent.Q("settings-silent"), "inside the Sound row");
        }

        [UnityTest]
        public IEnumerator RenameKeepsTheFirstRunsRules()
        {
            yield return OpenSlice(seed: 9504);
            var sim = GameClock.Instance.ActiveSim;
            string was = sim.State.settlementName;
            yield return OpenSettings();
            yield return Reveal("settings-rename");
            StringAssert.Contains(was, Root.Q<GlassRowButton>("settings-rename").Label, "the row names the box");
            yield return Tap(Root, "settings-rename");
            yield return Until(() => Hud.OpenSheet == SettingsSheets.Rename, 1f);
            yield return new WaitForSecondsRealtime(0.4f);
            var field = Root.Q<TextField>("rename-field");
            Assert.AreEqual(was, field.value);

            yield return Tap(Root, "rename-reroll");
            Assert.AreNotEqual(was, field.value, "Another offers another");
            field.value = "shit hole";
            yield return Frames(2);
            yield return Tap(Root, "rename-keep");
            Assert.AreEqual(was, sim.State.settlementName, "a refused name stays out");
            Assert.IsNotEmpty(Hud.SettingsView.RenameProblem, "and the sheet says why");
            var keep = (GlassButton)Root.Q("rename-keep");
            Assert.IsFalse(keep.Available, "Keep waits for an edit (question 28)");
            field.value = "Lanternfold";
            yield return Frames(2);
            Assert.IsTrue(keep.Available);

            GlassKeyboard.Debug = true;
            yield return Frames(4);
            float kbTop = Root.worldBound.height - GlassKeyboard.Height();
            Assert.LessOrEqual(Root.Q("rename-sheet").worldBound.yMax, kbTop + 1f, "the sheet rides on the keyboard");
            Assert.LessOrEqual(keep.worldBound.yMax, kbTop, "Keep stays in reach");
            GlassKeyboard.Debug = false;
            yield return Frames(4);

            yield return Tap(Root, "rename-keep");
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual("Lanternfold", sim.State.settlementName);
            Assert.IsNull(Hud.OpenSheet);
            Assert.AreEqual("Lanternfold", Root.Q("hud-title-text").Q<Label>().text, "the title follows at once");
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(GameClock.Instance.SavePath, out var saved));
            Assert.AreEqual("Lanternfold", saved.state.settlementName, "and it's saved");
            AssertNoErrors("rename");
        }

        [UnityTest]
        public IEnumerator ResetNeedsItsConfirmAndSaysWhatGoes()
        {
            yield return OpenSlice(seed: 9505);
            var clock = GameClock.Instance;
            clock.ActiveSim.SetSettlementName("Mossbarrow");
            GameClock.DebugSkipHours(3);
            var old = clock.ActiveSim;
            int years = Mathf.FloorToInt(old.State.Year);

            IEnumerator OpenReset()
            {
                yield return OpenSettings();
                yield return Reveal("settings-reset");
                yield return Tap(Root, "settings-reset");
                yield return Until(() => Hud.OpenSheet == SettingsSheets.Reset, 1f);
                yield return new WaitForSecondsRealtime(0.4f);
            }

            yield return OpenReset();
            var sheet = Root.Q<GlassSheet>("reset-sheet");
            Assert.AreEqual("Reset Mossbarrow?", sheet.Q<Label>("kg-sheet-title").text);
            StringAssert.Contains(SettingsSheets.ResetWords("Mossbarrow", years), Root.Q<Label>("reset-body").text, "it names what's lost");
            Assert.IsFalse(sheet.Surface.IsFrosted, "flat, over the only scrim that dims (question 32)");
            Assert.IsTrue(Root.Q("reset-scrim").ClassListContains("kg-scrim--dim"));
            var far = Root.Q("reset-confirm").worldBound;
            var near = Root.Q("reset-keep").worldBound;
            Assert.Greater(near.xMin - far.xMax, 44f, "Reset box stands apart from Keep playing");
            Assert.AreEqual(ButtonKind.Primary, ((GlassButton)Root.Q("reset-keep")).Kind);
            Assert.AreEqual(ButtonKind.Quiet, ((GlassButton)Root.Q("reset-confirm")).Kind);

            yield return Tap(Root, "reset-keep");
            Assert.IsNull(Hud.OpenSheet);
            Assert.AreSame(old, clock.ActiveSim, "Keep playing keeps the box");

            yield return OpenReset();
            yield return TapAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.8f));
            yield return Frames(2);
            Assert.IsNull(Hud.OpenSheet, "a tap on the world backs out");
            Assert.AreSame(old, clock.ActiveSim, "and never resets");

            yield return OpenReset();
            yield return Tap(Root, "reset-confirm");
            yield return Frames(10);
            Assert.AreNotSame(old, clock.ActiveSim, "Reset box does");
            Assert.Less(clock.ActiveSim.State.Year, 1f, "a new box");
            Assert.IsTrue(FirstRunView.Instance != null && FirstRunView.Instance.Active, "with its first run");
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(clock.SavePath, out var saved));
            Assert.AreEqual(clock.ActiveSim.State.seed, saved.state.seed, "and it's what's saved");
            yield return Frames(30);
            AssertNoErrors("reset");
        }

        [UnityTest]
        public IEnumerator FeedbackFillsAShortPhoneAndFoldsOverTheKeyboard()
        {
            yield return OpenSlice(750, 1334, seed: 9506);
            var fb = FeedbackUI.Instance;
            yield return Tap(Root, "hud-feedback");
            yield return Until(() => fb.IsOpen, 3f);
            yield return new WaitForSecondsRealtime(0.5f);
            var sheet = fb.Root.Q("fb-sheet");
            float h = fb.Root.worldBound.height;
            Assert.AreEqual(h, sheet.worldBound.height, 1f, "full height under 700 pt");
            var send = (GlassButton)fb.Root.Q("fb-send");
            Assert.Greater(send.worldBound.yMax, h * 0.9f, "Send on the bottom edge");
            Assert.LessOrEqual(fb.Root.Q("fb-text").worldBound.yMax, fb.Root.Q("fb-shot").worldBound.yMin + 1f, "nothing overlaps");
            Assert.IsFalse(send.Available, "Send waits for text");

            GlassKeyboard.Debug = true;
            fb.Root.Q<TextField>("fb-text").value = "A note.";
            yield return Frames(4);
            Assert.AreEqual(DisplayStyle.None, fb.Root.Q("fb-kinds").resolvedStyle.display, "the kinds fold away");
            Assert.AreEqual(DisplayStyle.None, fb.Root.Q("fb-shot").resolvedStyle.display, "and the screenshot row");
            Assert.LessOrEqual(send.worldBound.yMax, h - GlassKeyboard.Height(), "Send stays in reach");
            Assert.IsTrue(send.Available);
            GlassKeyboard.Debug = false;
            yield return Frames(4);
            Assert.AreEqual(h, sheet.worldBound.height, 1f, "and it comes back");
            yield return Tap(fb.Root, "fb-cancel");
            yield return Frames(3);
            Assert.IsFalse(fb.IsOpen);
            AssertNoErrors("feedback");
        }

        [UnityTest]
        public IEnumerator ACloseSchedulesOneNoteAndAnOpenCancelsItFocusDoesNothing()
        {
            var phone = new FakePhone();
            GameClock.PlatformOverride = phone;
            GameSettings.Notifications = true;
            yield return OpenSlice(seed: 9507);
            var clock = GameClock.Instance;
            Assert.Greater(phone.cancels, 0, "the open cancels");
            int cancels = phone.cancels;

            clock.SendMessage("OnApplicationFocus", false);
            yield return Frames(3);
            Assert.AreEqual(0, phone.scheduled.Count, "losing focus isn't leaving");
            Assert.IsNull(clock.Notifier.Last);

            clock.SendMessage("OnApplicationPause", true);
            Assert.IsTrue(clock.Notifier.WaitForRun(30000), "the worker ends");
            yield return Frames(2);
            var last = clock.Notifier.Last;
            Assert.IsTrue(last.HasValue, "the close reported");
            Assert.IsTrue(last.Value.result.Found, "a young box has an era coming: " + last.Value);
            Assert.AreEqual(1, phone.scheduled.Count, "one notification");
            Assert.AreEqual(last.Value.result.realHours, phone.scheduled[0].after.TotalHours, 2.0 / 60, "at close + its real hours");
            Debug.Log("[notifytest] " + last.Value);

            clock.SendMessage("OnApplicationPause", false);
            Assert.Greater(phone.cancels, cancels, "coming back cancels it");

            GameSettings.Notifications = false;
            clock.SendMessage("OnApplicationPause", true);
            Assert.IsTrue(clock.Notifier.WaitForRun(30000));
            Assert.AreEqual(1, phone.scheduled.Count, "the toggle off means nothing new");
            clock.SendMessage("OnApplicationPause", false);
            AssertNoErrors("close");
        }

        [UnityTest]
        public IEnumerator ThePhoneIsAskedOnlyOnAYes()
        {
            var phone = new FakePhone();
            GameClock.PlatformOverride = phone;
            yield return OpenSlice(seed: 9508);
            var ui = MomentPlayer.Instance.GetComponent<UIDocument>().rootVisualElement;

            MomentPlayer.DebugNotifyAsk();
            yield return Frames(4);
            yield return Tap(ui, "moment-notify-no");
            Assert.AreEqual(0, phone.asks, "Not now asks nothing");
            Assert.IsFalse(GameSettings.Notifications);

            MomentPlayer.DebugNotifyAsk();
            yield return Frames(4);
            phone.grants = false;
            yield return Tap(ui, "moment-notify-yes");
            yield return Frames(2);
            Assert.AreEqual(1, phone.asks, "a yes asks the phone");
            Assert.IsFalse(GameSettings.Notifications, "the phone's no stands");

            MomentPlayer.DebugNotifyAsk();
            yield return Frames(4);
            phone.grants = true;
            yield return Tap(ui, "moment-notify-yes");
            yield return Frames(2);
            Assert.IsTrue(GameSettings.Notifications, "a yes on both sides turns it on");
            Assert.AreEqual(1, PlayerPrefs.GetInt("moment.notify"));

            // The toggle: off asks nothing, on asks, and a phone's no flips it back.
            phone.grants = false;
            yield return OpenSettings();
            yield return Reveal("settings-notifications");
            yield return Tap(Root, "settings-notifications");
            Assert.IsFalse(GameSettings.Notifications);
            Assert.AreEqual(2, phone.asks, "off asks nothing");
            yield return Tap(Root, "settings-notifications");
            yield return Frames(2);
            Assert.AreEqual(3, phone.asks);
            Assert.AreEqual("Off", Root.Q<Button>("settings-notifications").text, "the toggle shows what's true");
            AssertNoErrors("ask");
        }

        // ================================================================== every state of board 3.8, at both phones
        [UnityTest, Explicit, Category("SettingsShots"), Timeout(300000)]
        public IEnumerator Shots([Values(1179, 750)] int w)
        {
            int h = w == 1179 ? 2556 : 1334;
            string at = $"s38-{w}-";
            yield return OpenSlice(w, h, seed: 9509);
            var clock = GameClock.Instance;
            clock.ActiveSim.SetSettlementName("Mossbarrow");
            GameClock.DebugSkipHours(8);
            var boxes = UnityEngine.Object.FindAnyObjectByType<BoxCompleteController>();
            if (boxes != null) boxes.enabled = false; // it follows Stories every frame; the shot wants the row without a finished box
            Hud.SetShelfRow(true);
            GameSettings.Notifications = false;
            yield return Frames(10);
            yield return OpenSettings();
            yield return Shot(at + "settings");
            yield return Reveal("settings-version");
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Shot(at + "settings-scrolled");
            Hud.SettingsView.Scroll.View.scrollOffset = Vector2.zero;
            DeviceSilent.DebugForce = true;
            yield return Frames(6);
            yield return Shot(at + "settings-silent");
            DeviceSilent.DebugForce = false;
            yield return Frames(3);

            Hud.Open(SettingsSheets.Rename);
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Shot(at + "rename");
            Root.Q<TextField>("rename-field").value = "shit hole";
            yield return Tap(Root, "rename-keep");
            yield return Frames(3);
            yield return Shot(at + "rename-refused");
            Root.Q<TextField>("rename-field").value = "Mossbarrow";
            GlassKeyboard.Debug = true;
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Shot(at + "rename-keyboard");
            GlassKeyboard.Debug = false;
            yield return Frames(3);
            Hud.CloseSheet();
            yield return new WaitForSecondsRealtime(0.4f);

            Hud.Open(SettingsSheets.Reset);
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Shot(at + "reset");
            Hud.CloseSheet();
            yield return new WaitForSecondsRealtime(0.4f);
            Hud.Open(SettingsSheets.Licences);
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Shot(at + "licences");
            Hud.CloseSheet();
            yield return new WaitForSecondsRealtime(0.4f);

            var fb = FeedbackUI.Instance;
            yield return Tap(Root, "hud-feedback");
            yield return Until(() => fb.IsOpen, 3f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(at + "feedback-empty");
            yield return Tap(fb.Root, "fb-kind-bug");
            fb.Root.Q<TextField>("fb-text").value = "The peek covers the second line of the toast when I come back.";
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Shot(at + "feedback-filled");
            GlassKeyboard.Debug = true;
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Shot(at + "feedback-keyboard");
            GlassKeyboard.Debug = false;
            yield return Frames(3);
            fb.Close();
            yield return new WaitForSecondsRealtime(0.4f);
            Hud.Toast("Thanks! Sent to the team.", 6f);
            yield return Until(() => Root.Q("hud-toast").resolvedStyle.opacity > 0.9f, 2f);
            yield return Frames(3);
            yield return Shot(at + "feedback-sent");
            MomentPlayer.DebugNotifyAsk();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(at + "notify-ask");
            yield return Tap(MomentPlayer.Instance.GetComponent<UIDocument>().rootVisualElement, "moment-notify-no");
            yield return Frames(3);
            AssertNoErrors("shots");
        }
    }
}
