using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.GameUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Keeper's Glass with its motion ON, by real taps (#108). <see cref="GameUiPanel.AcceptInputUnfocused"/> turns
    /// <see cref="GlassMotion.Instant"/> on for every other play test, so none of them can see what happens while something is on
    /// its way: a sheet reopened as it finishes closing, a toast posted as the last one's fade ends, a tap on what a leaving sheet
    /// still covers.
    /// </summary>
    [Category("GlassMotion")]
    public class GlassMotionPlayTests : PlayTestBase
    {
        const float WindowFrom = 0.28f, WindowTo = 0.30f;

        static float Now => Time.realtimeSinceStartup;

        public override void TearDown()
        {
            GlassMotion.Instant = true;
            base.TearDown();
        }

        IEnumerator OpenWithMotion()
        {
            yield return OpenSlice(seed: 7531);
            GlassMotion.Instant = false;
            yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 8f);
        }

        IEnumerator Tap(string name) => TapAt(GameUiPanel.ScreenCenter(GameHud.Instance.Root, name));

        static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        static string UpTrouble(GameHud hud)
        {
            var root = hud.Root;
            var sheet = root.Q<GlassSheet>("settings-sheet");
            if (hud.OpenSheet != GameHud.Settings) return "the HUD says " + (hud.OpenSheet ?? "closed");
            if (root.Q("settings-scrim").resolvedStyle.display != DisplayStyle.Flex) return "scrim display none";
            if (sheet.resolvedStyle.display != DisplayStyle.Flex) return "sheet display none";
            if (sheet.ClassListContains(GlassMotion.SlideOff)) return "sheet stayed off";
            if (FrostSwitch.On && !ReferenceEquals(FrostSwitch.Frosted, sheet.Surface)) return "no frost";
            if (root.Q("hud-bottom").resolvedStyle.visibility != Visibility.Hidden) return "tools show through the sheet";
            return null;
        }

        static string DownTrouble(GameHud hud)
        {
            var root = hud.Root;
            if (hud.OpenSheet != null) return "the HUD says " + hud.OpenSheet;
            if (root.Q("settings-scrim").resolvedStyle.display != DisplayStyle.None) return "scrim stayed up";
            if (root.Q("settings-sheet").resolvedStyle.display != DisplayStyle.None) return "sheet stayed up";
            if (root.Q("hud-bottom").resolvedStyle.visibility != Visibility.Visible) return "tools gone";
            if (root.Q("hud-pins").resolvedStyle.visibility != Visibility.Visible) return "pin row gone";
            if (FrostSwitch.ShownCount != 0) return "a surface stayed registered";
            return null;
        }

        /// <summary>What each panel would pick at a screen point: a tap that went astray says where.</summary>
        static string UnderTheFinger(Vector2 p) => string.Join(", ", Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .Where(d => d.isActiveAndEnabled && d.rootVisualElement?.panel != null)
            .Select(d => d.gameObject.name + ":" + (d.rootVisualElement.panel.Pick(RuntimePanelUtils.ScreenToPanel(d.rootVisualElement.panel, new Vector2(p.x, Screen.height - p.y)))?.name ?? "nothing")));

        [UnityTest, Timeout(240000)]
        public IEnumerator ASheetReopenedAsItFinishesClosingComesBack()
        {
            yield return OpenWithMotion();
            var hud = GameHud.Instance;
            float closedAt = 0f, openedAt = 0f;
            void Times(string id) { if (id == null) closedAt = Now; else openedAt = Now; }
            hud.SheetChanged += Times;
            var gaps = new List<float>();
            var bad = new List<string>();
            float lead = 0.06f; // press to sheet: measured, so the sweep lands where it means to
            try
            {
                for (int pass = 0; pass < 3 && (pass == 0 || gaps.Count(g => g >= WindowFrom && g <= WindowTo) < 3); pass++)
                    for (float want = 0.24f; want <= 0.3401f; want += 0.005f)
                    {
                        yield return Tap("hud-settings");
                        yield return Until(() => hud.OpenSheet == GameHud.Settings, 1f);
                        yield return Wait(0.4f);
                        yield return Tap("settings-close");
                        yield return Until(() => hud.OpenSheet == null, 1f);
                        while (Now < closedAt + want - lead) yield return null;
                        float pressed = Now;
                        yield return Tap("hud-settings");
                        yield return Until(() => hud.OpenSheet == GameHud.Settings, 1f);
                        lead = Mathf.Lerp(lead, openedAt - pressed, 0.5f);
                        float gap = openedAt - closedAt;
                        gaps.Add(gap);
                        yield return Wait(0.45f);
                        string trouble = UpTrouble(hud);
                        if (trouble != null)
                        {
                            bad.Add($"{gap:0.000}: {trouble}");
                            hud.CloseSheet(); // and on with the sweep, so the report shows the whole window
                            yield return Wait(0.45f);
                            continue;
                        }
                        yield return Tap("settings-close");
                        yield return Wait(0.45f);
                        trouble = DownTrouble(hud);
                        if (trouble != null) bad.Add($"{gap:0.000} closing: {trouble}");
                    }
            }
            finally { hud.SheetChanged -= Times; }
            string list = string.Join(" ", gaps.Select(g => g.ToString("0.000")));
            Debug.Log("[GlassMotion] reopen gaps: " + list);
            Assert.IsEmpty(bad, "reopened after Close, of " + gaps.Count + " gaps: " + list);
            Assert.GreaterOrEqual(gaps.Count(g => g >= WindowFrom && g <= WindowTo), 3, "the sweep has to land in the end of the close: " + list);
            AssertNoErrors("reopen sweep");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AToastPostedAsTheLastOneFadesOutStillShows()
        {
            yield return OpenWithMotion();
            var hud = GameHud.Instance;
            var label = hud.Root.Q<GlassToast>("hud-toast");
            var bad = new List<string>();
            int seen = 0;
            for (float after = 0.44f; after <= 0.5201f; after += 0.005f)
            {
                yield return Until(() => GameUiPanel.Toasts.Current == null && GameUiPanel.Toasts.Waiting == 0, 6f);
                yield return Wait(0.3f);
                string first = $"First {after:0.000}", second = $"Second {after:0.000}";
                float start = Now;
                hud.Toast(first, 0.3f);
                while (Now - start < after) yield return null;
                float posted = Now - start;
                hud.Toast(second, 0.7f);
                yield return Until(() => GameUiPanel.Toasts.Current == second, 0.5f);
                yield return Wait(0.3f);
                if (GameUiPanel.Toasts.Current != second) continue; // the game said something of its own
                seen++;
                if (label.resolvedStyle.display != DisplayStyle.Flex) bad.Add($"{posted:0.000}: display none");
                else if (label.ClassListContains(GlassMotion.FadeOff) || label.resolvedStyle.opacity < 0.9f) bad.Add($"{posted:0.000}: faded");
                else if (label.text != second) bad.Add($"{posted:0.000}: says {label.text}");
            }
            Assert.IsEmpty(bad, "a toast lost");
            Assert.Greater(seen, 10, "enough of the sweep ran");
            AssertNoErrors("toast sweep");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ALeavingSheetTakesNoTaps()
        {
            yield return OpenWithMotion();
            var hud = GameHud.Instance;
            float closedAt = 0f;
            void Times(string id) { if (id == null) closedAt = Now; }
            hud.SheetChanged += Times;
            var bad = new List<string>();
            try
            {
                foreach (float gap in new[] { 0.04f, 0.12f, 0.2f })
                {
                    yield return Tap("hud-settings");
                    yield return Until(() => hud.OpenSheet == GameHud.Settings, 1f);
                    yield return Wait(0.45f);
                    Assert.AreEqual(GameHud.Settings, hud.OpenSheet, "the sheet is up");
                    var closeAt = GameUiPanel.ScreenCenter(hud.Root, "settings-close");
                    yield return TapAt(closeAt);
                    yield return Until(() => hud.OpenSheet == null, 1f);
                    Assert.IsNull(hud.OpenSheet, "Close took the tap; under it: " + UnderTheFinger(closeAt));
                    while (Now < closedAt + gap) yield return null;
                    float at = Now - closedAt;
                    yield return Tap("hud-clean");
                    yield return Frames(2);
                    if (!hud.CleanMode) bad.Add($"{at:0.000}");
                    hud.SetCleanMode(false);
                    yield return Wait(0.5f);
                }
            }
            finally { hud.SheetChanged -= Times; }
            Assert.IsEmpty(bad, "a tool tap dropped after Close");

            // and the sheet takes taps again the next time it's up
            yield return Tap("hud-settings");
            yield return Until(() => hud.OpenSheet == GameHud.Settings, 1f);
            yield return Wait(0.45f);
            bool sound = GameSettings.Sound;
            yield return Tap("settings-sound");
            yield return Frames(2);
            Assert.AreNotEqual(sound, GameSettings.Sound, "the switch takes the tap");
            yield return Tap("settings-close");
            yield return Wait(0.45f);
            Assert.IsNull(DownTrouble(hud));
            AssertNoErrors("leaving sheet");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator QuietAndIconButtonsShowThePress()
        {
            yield return OpenWithMotion();
            var hud = GameHud.Instance;
            var quiet = new GlassButton("Skip all", null, ButtonKind.Quiet) { ShowPressed = true };
            var icon = new GlassButton("", null, ButtonKind.Icon) { Icon = "close", ShowPressed = true };
            hud.Root.Add(quiet);
            hud.Root.Add(icon);
            yield return Wait(0.2f);
            Assert.Greater(quiet.resolvedStyle.backgroundColor.a, 0.9f, "quiet");
            Assert.Greater(icon.resolvedStyle.backgroundColor.a, 0.9f, "icon");
            quiet.ShowPressed = icon.ShowPressed = false;
            yield return Wait(0.2f);
            Assert.AreEqual(0f, quiet.resolvedStyle.backgroundColor.a, 0.01f, "at rest they stay bare");
            Assert.AreEqual(0f, icon.resolvedStyle.backgroundColor.a, 0.01f);
            hud.Root.Remove(quiet);
            hud.Root.Remove(icon);

            // The pictures: the HUD's own quiet and icon buttons, pressed on the world and on a sheet
            var title = hud.Root.Q<GlassButton>("hud-title");
            var photo = hud.Root.Q<GlassButton>("hud-photo");
            title.ShowPressed = photo.ShowPressed = true;
            yield return Wait(0.2f);
            yield return Shot("glass-pressed-world");
            title.ShowPressed = photo.ShowPressed = false;
            hud.Open(GameHud.Settings);
            yield return Wait(0.45f);
            var close = hud.Root.Q<GlassButton>("settings-close");
            close.ShowPressed = true;
            yield return Wait(0.2f);
            yield return Shot("glass-pressed-sheet");
            close.ShowPressed = false;
            hud.CloseSheet();
            yield return Wait(0.45f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheCleanHintGoesWithCleanModeAndTheTrayEndsIt()
        {
            yield return OpenWithMotion();
            var hud = GameHud.Instance;

            // A hint waiting behind a toast doesn't outlive the mode
            hud.Toast("Something else", 3f);
            hud.SetCleanMode(true);
            Assert.AreEqual(1, GameUiPanel.Toasts.Waiting, "the hint waits");
            hud.SetCleanMode(false);
            Assert.AreEqual(0, GameUiPanel.Toasts.Waiting, "and goes with Done");

            // The tray ends clean mode, by touch, and it stays ended
            yield return Tap("hud-clean");
            yield return Frames(2);
            Assert.IsTrue(hud.CleanMode);
            yield return Tap("hud-shape");
            yield return Until(() => hud.OpenSheet == GameHud.Shaping, 1f);
            Assert.IsFalse(hud.CleanMode, "the tray ends clean mode");
            Assert.AreEqual("Clean", hud.Root.Q<GlassButton>("hud-clean").text);
            yield return Wait(0.4f);
            yield return Tap("shaping-close");
            yield return Wait(0.45f);
            Assert.IsFalse(hud.CleanMode, "and it's still off after");
            AssertNoErrors("clean and tray");
        }
    }
}
