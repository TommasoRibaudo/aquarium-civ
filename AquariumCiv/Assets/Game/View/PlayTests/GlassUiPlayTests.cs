using System.Collections;
using System.Linq;
using AquariumCiv.Audio;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using AquariumCiv.View.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// Keeper's Glass in the real scene (#108): the gallery holds every state, frost costs nothing unless a surface is up, the HUD keeps
    /// to hierarchy D1 to D6, the lanes hold, and the small controls take real taps. Pixels are in <see cref="GlassShots"/>.
    /// </summary>
    [Category("GlassUi")]
    public class GlassUiPlayTests : PlayTestBase
    {
        static bool Displayed(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        public override void TearDown()
        {
            FrostSwitch.On = FrostSwitch.Default;
            FrostSwitch.Blur = FrostSwitch.BlurLarge;
            GlassMotion.Instant = true;
            if (GlassGallery.Instance != null) Object.Destroy(GlassGallery.Instance.gameObject);
            base.TearDown();
        }

        IEnumerator Copying(bool expected, string when)
        {
            long before = FrostBackdrop.CopiesRecorded;
            yield return Frames(6);
            if (expected) Assert.Greater(FrostBackdrop.CopiesRecorded, before, when + ": the copy runs");
            else Assert.AreEqual(before, FrostBackdrop.CopiesRecorded, when + ": no pass");
            Assert.AreEqual(expected, FrostBackdrop.Active, when);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FrostRunsOnlyWhileASurfaceIsUpAndFlatHasNoPass()
        {
            yield return OpenSlice(seed: 7531);
            var hud = GameHud.Instance;
            yield return Copying(false, "no sheet up");

            hud.Open(GameHud.Settings);
            yield return Frames(4);
            var surface = hud.Root.Q<GlassSheet>("settings-sheet").Surface;
            Assert.IsTrue(surface.ClassListContains(FrostSurface.FrostClass));
            Assert.IsFalse(surface.ClassListContains(FrostSurface.FlatClass));
            yield return Copying(true, "sheet up, frost");

            hud.SetVisible(false);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Copying(false, "HUD hidden with the sheet open");
            hud.SetVisible(true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Copying(true, "HUD back");

            // The one switch, with the sheet up: the same layout, no copy, no filter
            float h = surface.worldBound.height;
            FrostSwitch.On = false;
            yield return Frames(3);
            Assert.IsTrue(surface.ClassListContains(FrostSurface.FlatClass), "the class is swapped");
            Assert.IsFalse(surface.ClassListContains(FrostSurface.FrostClass));
            Assert.AreEqual(h, surface.worldBound.height, 0.5f, "layout doesn't change");
            yield return Copying(false, "flat");
            hud.CloseSheet();
            hud.Open(GameHud.History);
            yield return Copying(false, "flat, another sheet");
            FrostSwitch.On = true;
            yield return Copying(true, "back to frost");
            hud.CloseSheet();
            yield return Copying(false, "closed");
            Assert.AreEqual(0, FrostSwitch.ShownCount);
            AssertNoErrors("frost");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator OneFrostedSurfaceAtATime()
        {
            yield return OpenSlice(seed: 7531);
            var hud = GameHud.Instance;
            hud.Open(GameHud.History);
            yield return Frames(3);
            var first = hud.Root.Q<GlassSheet>("history-sheet").Surface;
            Assert.AreSame(first, FrostSwitch.Frosted);
            GlassGallery.DebugToggle();
            yield return Frames(3);
            GlassGallery.Instance.OpenLive();
            yield return Frames(3);
            var second = GlassGallery.Instance.LiveSheet.Surface;
            Assert.AreSame(second, FrostSwitch.Frosted, "the newest surface takes the frost");
            Assert.IsFalse(first.IsFrosted, "the older one waits on flat");
            GlassGallery.Instance.CloseLive();
            yield return Frames(3);
            Assert.AreSame(first, FrostSwitch.Frosted, "and gets it back");
            hud.CloseSheet();
            yield return Frames(3);
            Assert.IsFalse(FrostBackdrop.Active);
            AssertNoErrors("one surface");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheFrostPhoneTestOwnsTheCopyWhileOpen()
        {
            yield return OpenSlice(seed: 7531);
            var hud = GameHud.Instance;
            hud.Open(GameHud.History);
            yield return Frames(3);
            FrostTestPanel.DebugToggle();
            yield return Frames(3);
            Assert.IsNull(FrostSwitch.Frosted, "surfaces stay flat under the test");
            FrostTestPanel.Instance.SetMode(FrostMode.Quarter);
            yield return Frames(3);
            Assert.IsTrue(FrostBackdrop.Active);
            FrostTestPanel.DebugToggle();
            yield return Frames(4);
            Assert.IsNotNull(FrostSwitch.Frosted, "the sheet takes it back");
            hud.CloseSheet();
            yield return Frames(3);
            Assert.IsFalse(FrostBackdrop.Active);
            AssertNoErrors("frost test");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheGalleryHoldsEveryComponentInEveryState()
        {
            yield return OpenSlice(seed: 7531);
            Assert.AreEqual("gallery open", GlassGallery.DebugToggle());
            yield return Frames(6);
            var g = GlassGallery.Instance;
            string[] required =
            {
                "button-primary", "button-primary-pressed", "button-primary-off", "button-secondary", "button-secondary-pressed", "button-secondary-off",
                "button-quiet", "button-quiet-pressed", "button-quiet-off", "button-icon", "button-icon-pressed",
                "chip-label", "chip-label-pressed", "chip-choice-off", "chip-choice-on",
                "pin-pinned", "pin-long", "pin-hidden", "stories-shown", "stories-hidden",
                "sheet-open", "sheet-scrolled", "card-wish", "card-story",
                "tab-tabs", "tab-segment", "toggle-on", "toggle-off", "slider-value", "slider-zero",
                "bubble-line", "bubble-chatter", "label-name", "label-pin", "label-tie", "label-plate",
                "badge-dot", "badge-page-dots", "badge-pips", "badge-new", "badge-ribbon",
                "toast-short", "toast-long", "caption-line", "caption-who", "caption-panel",
                "hint-word", "hint-finger", "hint-ghost", "banner-tool", "banner-visiting",
                "prompt-ready", "prompt-pressed", "prompt-hidden",
            };
            foreach (var id in required)
                Assert.Contains("gal-" + id, g.States, id);
            Assert.AreEqual(GlassIcon.All.Length, g.States.Count(s => s.StartsWith("gal-icon-")), "every icon");
            foreach (var id in g.States)
            {
                var e = g.Root.Q(id);
                Assert.IsNotNull(e, id);
                if (id.EndsWith("-hidden")) { Assert.AreEqual(DisplayStyle.None, e.resolvedStyle.display, id); continue; }
                Assert.Greater(e.layout.width, 0f, id);
                Assert.Greater(e.layout.height, 0f, id);
            }
            foreach (var c in GlassIcon.All)
            {
                var icon = g.Root.Q("gal-icon-" + c).Q<GlassIcon>();
                Assert.IsNotNull(icon.resolvedStyle.backgroundImage.vectorImage, c + " has no vector image");
            }
            foreach (var cls in new[] { "type-title", "type-sheet", "type-box", "type-big", "type-head", "type-small-head", "type-btn", "type-body", "type-sub", "type-quote", "type-note" })
            {
                var l = g.Root.Q("gal-type-" + cls);
                Assert.IsNotNull(l.resolvedStyle.unityFontDefinition.fontAsset, cls + " has no font asset");
                Assert.GreaterOrEqual(l.resolvedStyle.fontSize, 13f * GlassType.Pt - 0.01f, "nothing under 13 pt");
            }

            // Spec's USS routes
            var slot = g.Root.Q<GlassChip>("gal-chip-label");
            Assert.AreEqual(GameUiPanel.MinTarget, slot.layout.height, 0.6f, "the slot is a whole target");
            Assert.AreEqual(82.5f, slot.Face.layout.height, 0.6f, "the chip shows 30");
            Assert.AreEqual(605f, g.Root.Q("gal-pin-long").layout.width, 1f, "the long pin stops at 220 pt");
            var off = g.Root.Q<GlassButton>("gal-button-secondary-off");
            Assert.AreEqual(1f, off.resolvedStyle.opacity, "unavailable is never faded");
            Assert.AreEqual(ColorOf("#3D2A1E"), off.resolvedStyle.color, "and keeps full-strength text");
            Assert.AreEqual(GameUiPanel.MinTarget, g.Root.Q("gal-slider-value").layout.height, 0.6f, "slider hit strip");

            Assert.AreEqual("gallery closed", GlassGallery.DebugToggle());
            yield return Frames(3);
            Assert.IsNull(GlassGallery.Instance, "gone when closed");
            AssertNoErrors("gallery");
        }

        /// <summary>Practices §5, spec §1: a square of MinTarget around the centre takes a tap, or the piece's own box does. Both of the
        /// tests the picker makes, in the piece's own space so a pressed button's scale doesn't count.</summary>
        static void AssertReaches(VisualElement e, string who)
        {
            Assert.AreNotEqual(PickingMode.Ignore, e.pickingMode, who);
            var r = e.layout;
            float half = GameUiPanel.MinTarget * 0.5f - 0.5f;
            float x0 = Mathf.Min(0.5f, r.width * 0.5f - half), x1 = Mathf.Max(r.width - 0.5f, r.width * 0.5f + half);
            float y0 = Mathf.Min(0.5f, r.height * 0.5f - half), y1 = Mathf.Max(r.height - 0.5f, r.height * 0.5f + half);
            Assert.GreaterOrEqual(x1 - x0, GameUiPanel.MinTarget - 2f, who + " width");
            Assert.GreaterOrEqual(y1 - y0, GameUiPanel.MinTarget - 2f, who + " height");
            // Smaller than a target? The picker looks only inside the bounding box, and the clear child is what widens that.
            bool small = r.width < GameUiPanel.MinTarget - 1f || r.height < GameUiPanel.MinTarget - 1f;
            var reach = e.Q(className: GlassReach.Class);
            if (small) Assert.IsNotNull(reach, $"{who} is drawn {r.width:0}x{r.height:0} with no reach");
            foreach (var p in new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x0, y1), new Vector2(x1, y1) })
            {
                Assert.IsTrue(e.ContainsPoint(p), $"{who} takes a tap at {p}, drawn {r.width:0}x{r.height:0}");
                if (small) Assert.IsTrue(reach.worldBound.Contains(e.LocalToWorld(p)), $"{who}: the reach covers {p}, drawn {r.width:0}x{r.height:0}");
            }
        }

        static VisualElement[] Targets(VisualElement root) =>
            root.Query<Button>().ToList().Cast<VisualElement>().Concat(root.Query<GlassSlider>().ToList()).Where(Displayed).ToArray();

        [UnityTest, Timeout(120000)]
        public IEnumerator EveryTargetReachesMinTarget()
        {
            yield return OpenSlice(seed: 7531);
            var hud = GameHud.Instance;
            hud.SetPinChip("Piers", null);
            hud.ForceStories = true;
            yield return Frames(4);
            var onHud = Targets(hud.Root);
            Assert.GreaterOrEqual(onHud.Length, 7, "the tools, the top row, the pin and Stories");
            foreach (var e in onHud) AssertReaches(e, "HUD " + e.name);
            hud.ForceStories = false;
            hud.SetPinChip(null, null);

            GlassGallery.DebugToggle();
            yield return Frames(6);
            var g = GlassGallery.Instance;
            var all = Targets(g.Root);
            Assert.GreaterOrEqual(all.Length, 30, "the gallery's buttons, chips, tabs, toggles, sliders and prompts");
            foreach (var e in all) AssertReaches(e, $"{e.name} {e.GetType().Name}");

            // A real tap past the drawn edge, on each route: the reach, and the clear box
            g.ScrollTo("Button");
            yield return Frames(4);
            var b = g.Root.Q<GlassButton>("gal-button-secondary");
            int taps = 0;
            b.Tapped += () => taps++;
            float k = Screen.width / g.Root.worldBound.width;
            yield return TapAt(GameUiPanel.ScreenCenter(b) + new Vector2(0f, (GameUiPanel.MinTarget * 0.5f - 2f) * k));
            Assert.AreEqual(1, taps, "a button takes a tap just past its drawn edge");
            AssertNoErrors("targets");
        }

        static Color ColorOf(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        [UnityTest, Timeout(120000)]
        public IEnumerator SmallControlsTakeRealTaps()
        {
            yield return OpenSlice(seed: 7531);
            GlassGallery.DebugToggle();
            yield return Frames(6);
            var g = GlassGallery.Instance;
            int taps = 0;

            var chip = g.Root.Q<GlassChip>("gal-chip-choice-off");
            chip.Tapped += () => taps++;
            g.ScrollTo("Chip");
            yield return Frames(4);
            // The chip is 30 tall; a tap near the slot's edge, off the visible face, still lands
            var edge = GameUiPanel.ScreenCenter(chip) + new Vector2(0f, 48f * Screen.width / 1080f);
            yield return TapAt(edge);
            Assert.AreEqual(1, taps, "the slot takes the tap");
            Assert.IsTrue(chip.On, "a choice turns on");

            g.ScrollTo("Button");
            yield return Frames(4);
            Sfx.ResetLog();
            taps = 0;
            var off = g.Root.Q<GlassButton>("gal-button-secondary-off");
            off.Tapped += () => taps++;
            yield return TapAt(GameUiPanel.ScreenCenter(off));
            Assert.AreEqual(0, taps, "unavailable does nothing");
            CollectionAssert.Contains(Sfx.LastAsked, CueLibrary.ShapeFail, "and says so");

            g.ScrollTo("Tab and toggle");
            yield return Frames(4);
            var toggle = g.Root.Q<GlassToggle>("gal-toggle-off");
            yield return TapAt(GameUiPanel.ScreenCenter(toggle));
            Assert.IsTrue(toggle.On);
            Assert.AreEqual("On", toggle.text);
            var slider = g.Root.Q<GlassSlider>("gal-slider-zero");
            var r = slider.worldBound;
            float k = Screen.width / g.Root.worldBound.width;
            yield return TapAt(new Vector2((r.x + r.width * 0.5f) * k, Screen.height - r.center.y * k));
            Assert.AreEqual(0.5f, slider.Value, 0.03f, "a touch on the strip sets the value");
            AssertNoErrors("taps");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheHudKeepsToTheHierarchy()
        {
            yield return OpenSlice(seed: 7531);
            var hud = GameHud.Instance;
            var root = hud.Root;
            yield return Frames(4);

            // D6: one calm look, no permanent primary
            var tools = new[] { "hud-history", "hud-clean", "hud-shape" }.Select(n => root.Q<GlassButton>(n)).ToArray();
            foreach (var t in tools)
            {
                Assert.AreEqual(ButtonKind.Secondary, t.Kind, t.name);
                Assert.AreEqual(tools[0].layout.width, t.layout.width, 0.5f, "equal weight");
                Assert.AreEqual(154f, t.layout.height, 0.6f, "56 pt");
            }
            float H = root.worldBound.height;
            Assert.AreEqual(GameUiPanel.ToolsBottom, H - tools[0].worldBound.yMax, 1f, "tools lane");

            // D1: stories never hijack History
            Assert.IsFalse(Displayed(root.Q("hud-history-dot")), "no stories, no chip");
            Assert.IsNull(tools[0].Q<GlassDot>(), "History itself has no dot");
            hud.ForceStories = true;
            yield return null; yield return null;
            var stories = root.Q("hud-history-dot");
            Assert.AreEqual(1, stories.Query<GlassDot>().ToList().Count, "a dot");
            Assert.AreEqual("Stories", stories.Q<Label>().text, "and a word, never a number");
            hud.ForceStories = false;

            // D2: a portrait with the name, apart from the tools
            hud.SetPinChip("Piers", null);
            yield return null; yield return null;
            var pin = root.Q<PinChip>("hud-pin");
            Assert.AreEqual("P", pin.Q<GlassPortrait>().text);
            Assert.AreEqual("Piers", hud.PinChipText);
            Assert.AreEqual(GameUiPanel.PinBottom, H - pin.worldBound.yMax, 1f, "pin lane");
            Assert.IsFalse(root.Q("hud-bottom").Contains(pin), "apart from the tools");
            hud.SetPinChip(null, null);
            yield return null;

            // D3, D4: quiet name, settings mark, small icons
            var title = root.Q<GlassButton>("hud-title");
            Assert.AreEqual(ColorOf("#00000000"), title.resolvedStyle.backgroundColor, "no pill");
            Assert.IsNotNull(title.Q<GlassIcon>(), "a settings mark");
            Assert.Less(root.Q("hud-feedback").resolvedStyle.opacity, 1f, "Feedback is quieter");

            // Rule 7: Clean's state shows, and clean mode ends with the word back
            hud.SetCleanMode(true);
            yield return null; yield return null;
            Assert.AreEqual("Done", root.Q<GlassButton>("hud-clean").text);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(className: "kg-tool-mark").resolvedStyle.display, "a check mark");
            hud.SetCleanMode(false);

            // Sheets: on the bottom edge, 30 %, settings 59 %
            hud.Open(GameHud.History);
            yield return Frames(3);
            var sheet = root.Q("history-sheet");
            Assert.AreEqual(0.30f, sheet.worldBound.height / H, 0.003f);
            Assert.AreEqual(H, sheet.worldBound.yMax, 1f, "on the bottom edge");
            Assert.AreEqual(ColorOf("#00000000"), root.Q("history-scrim").resolvedStyle.backgroundColor, "nothing dims");
            hud.Toast("Above the sheet.", 2f);
            yield return Frames(3);
            Assert.AreEqual(GameUiPanel.ToastAboveSheet, sheet.worldBound.yMin - root.Q("hud-toast").worldBound.yMax, 1.5f, "a toast sits 8 pt above a sheet");
            hud.CloseSheet();
            hud.Open(GameHud.Settings);
            yield return Frames(3);
            Assert.AreEqual(0.59f, root.Q("settings-sheet").worldBound.height / H, 0.003f);
            hud.CloseSheet();
            yield return Frames(3);
            GameUiPanel.Toasts.Clear();
            AssertNoErrors("hud");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HistoryOpensTheTimeLapseEvenWithStoriesWaiting()
        {
            yield return OpenSlice(seed: 7531);
            for (int y = 0; y < 3; y++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            GameClock.DebugAway(6f);
            yield return Frames(6);
            var stack = CardStackView.Instance;
            yield return Until(() => stack.Model.Mode == CardStackMode.Peek, 2f);
            Assume.That(stack.Model.Mode, Is.EqualTo(CardStackMode.Peek), "needs a return with stories");
            yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-peek"));
            yield return Frames(4);
            yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-skip"));
            yield return Frames(6);
            var hud = GameHud.Instance;
            Assert.IsTrue(stack.Model.HistoryDot, "stories wait");
            Assert.IsTrue(Displayed(hud.Root.Q("hud-history-dot")), "the chip says so");
            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(CardStackMode.Hidden, stack.Model.Mode, "the stories stay put");
            Assert.IsTrue(TimeLapseScreenView.Instance != null && TimeLapseScreenView.Instance.IsOpen, "History is the time-lapse");
            AssertNoErrors("history");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator APromptOutranksToasts()
        {
            yield return OpenSlice(seed: 7531);
            var hud = GameHud.Instance;
            var host = new GlassBigPrompt("Watch them become a village") { name = "test-prompt" };
            hud.Root.Add(host);
            host.Ready = true;
            hud.Toast("Waits for the prompt.", 2f);
            yield return Frames(3);
            Assert.IsNull(GameUiPanel.Toasts.Current, "the toast waits");
            Assert.AreEqual(GameUiPanel.PromptBottom, hud.Root.worldBound.height - host.worldBound.yMax, 1f, "prompt lane");
            host.Ready = false;
            yield return Frames(3);
            Assert.AreEqual("Waits for the prompt.", GameUiPanel.Toasts.Current);
            Assert.AreEqual(DisplayStyle.Flex, hud.Root.Q("hud-toast").resolvedStyle.display);
            host.RemoveFromHierarchy();
            GameUiPanel.Toasts.Clear();
            AssertNoErrors("prompt");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SheetsReallySlide()
        {
            yield return OpenSlice(seed: 7531);
            GlassMotion.Instant = false;
            var hud = GameHud.Instance;
            hud.Open(GameHud.History);
            var sheet = hud.Root.Q("history-sheet");
            float H = hud.Root.worldBound.height;
            yield return Frames(2);
            yield return new WaitForSecondsRealtime(0.1f);
            float mid = sheet.worldBound.yMin;
            Assert.Greater(mid, H * 0.70f + 1f, "still on its way up");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(H * 0.70f, sheet.worldBound.yMin, 1.5f, "landed");
            hud.CloseSheet();
            Assert.IsTrue(FrostBackdrop.Active, "the copy stays while it leaves");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(DisplayStyle.None, hud.Root.Q("history-scrim").resolvedStyle.display, "and it's gone");
            Assert.IsFalse(FrostBackdrop.Active);
            GlassMotion.Instant = true;
            AssertNoErrors("slide");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheNameGetsABackingOffTheHomeView()
        {
            yield return OpenSlice(seed: 7531);
            var hud = GameHud.Instance;
            var cam = Camera.main.GetComponent<BoxCamera>();
            yield return Frames(4);
            Assert.IsFalse(hud.OffHome, "home view: quiet text with nothing behind it");
            cam.FlyTo(new Vector3(0f, 2.2f, 0f), 0.55f, 0.2f);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(hud.OffHome);
            Assert.AreNotEqual(0f, hud.Root.Q("hud-title-text").resolvedStyle.backgroundColor.a, "the backing is on");
            AssertNoErrors("backing");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SafeAreaPaddingTakesTheLargerOf30AndTheInset()
        {
            yield return OpenSlice(seed: 7531);
            var sheet = GameHud.Instance.Root.Q<GlassSheet>("history-sheet");
            GameHud.Instance.Open(GameHud.History);
            yield return Frames(3);
            float H = 34f * GlassType.Pt;
            sheet.SetInsets(new Vector4(0f, 0f, 0f, H));
            yield return Frames(2);
            Assert.AreEqual(H, sheet.contentContainer.resolvedStyle.paddingBottom, 0.5f, "34 pt home bar");
            sheet.SetInsets(new Vector4(30f, 0f, 20f, 10f));
            yield return Frames(2);
            Assert.AreEqual(GlassSheet.BottomPad, sheet.contentContainer.resolvedStyle.paddingBottom, 0.5f, "30 pt floor");
            Assert.AreEqual(GlassSheet.SidePad + 30f, sheet.contentContainer.resolvedStyle.paddingLeft, 0.5f, "a side notch");
            GameHud.Instance.CloseSheet();
            AssertNoErrors("safe area");
        }
    }

    /// <summary>The pictures for #108: each gallery section and each HUD state, to Temp/agent/glass. Run by hand:
    /// <c>--filter GlassShots --filter_type category --include_explicit true --async_tests true</c>.</summary>
    [Explicit("Writes images; run by hand"), Category("GlassShots")]
    public class GlassShots : PlayTestBase
    {
        const string Dir = "glass/";

        public override void TearDown()
        {
            FrostSwitch.On = FrostSwitch.Default;
            if (GlassGallery.Instance != null) Object.Destroy(GlassGallery.Instance.gameObject);
            DebugDock.ResetLock();
            base.TearDown();
        }

        static IEnumerator Pic(string name)
        {
            yield return Frames(6);
            yield return Shot(Dir + name);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator HudStates()
        {
            System.IO.Directory.CreateDirectory(Shots + Dir);
            yield return OpenSlice(seed: 7531);
            DebugDock.ForceLocked = true;
            for (int y = 0; y < 7; y++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            Sim().CleanAll();
            Sim().CardsSeenToNow();
            PeopleView.DebugSetHour(11f);
            NightLamps.DebugSetLevel(0f);
            yield return Frames(40);
            var hud = GameHud.Instance;
            yield return Pic("hud-default");
            hud.SetPinChip("Piers", null);
            hud.ForceStories = true;
            hud.Toast("All tidy. Look at that sky!", 6f);
            yield return Pic("hud-pin-stories-toast");
            hud.SetPinChip("Bartholomew the Younger (remembered)", null);
            hud.SetCleanMode(true);
            yield return Pic("hud-long-pin-clean-on");
            hud.SetCleanMode(false);
            hud.SetPinChip(null, null);
            hud.ForceStories = false;
            GameUiPanel.Toasts.Clear();
            hud.SetShapeNew(true);
            yield return new WaitForSecondsRealtime(2f);
            yield return Pic("hud-new-tool");
            hud.SetShapeNew(false);
            hud.Open(GameHud.History);
            yield return Pic("sheet-history-frost");
            FrostSwitch.On = false;
            yield return Pic("sheet-history-flat");
            FrostSwitch.On = true;
            hud.CloseSheet();
            hud.Open(GameHud.Settings);
            yield return Pic("sheet-settings-frost");
            hud.CloseSheet();
            hud.Open(GameHud.Shaping);
            yield return Pic("sheet-shaping-content-height");
            hud.CloseSheet();
            Camera.main.GetComponent<BoxCamera>().FlyTo(new Vector3(0f, 2.2f, 0f), 0.55f, 0.2f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Frames(20);
            yield return Pic("hud-close-camera-backed");
            hud.Open(GameHud.History);
            yield return Pic("sheet-close-camera-frost");
            hud.CloseSheet();
            yield return Frames(4);
            hud.SetCameraMode(true);
            yield return Pic("hud-camera-mode");
            hud.SetCameraMode(false);
            AssertNoErrors("hud shots");
        }

        static SimCore Sim() => GameClock.Instance.ActiveSim;

        /// <summary>The new HUD and lanes in the game scene with the screens not retrofitted yet over it, at a tall and a short phone:
        /// tray, card stack, time-lapse, Watch, each with a toast where a toast can land.</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator OldScreensOverTheHud([Values(1179, 750)] int w)
        {
            System.IO.Directory.CreateDirectory(Shots + Dir);
            int h = w == 1179 ? 2556 : 1334;
            string at = $"old-{w}-";
            yield return OpenSlice(w, h, seed: 7531);
            DebugDock.ForceLocked = true;
            for (int y = 0; y < 7; y++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            Sim().CleanAll();
            Sim().CardsSeenToNow();
            PeopleView.DebugSetHour(11f);
            NightLamps.DebugSetLevel(0f);
            yield return Frames(40);
            var hud = GameHud.Instance;
            yield return Pic(at + "hud");
            hud.SetPinChip("Piers", null);
            hud.ForceStories = true;
            hud.Toast("All tidy. Look at that sky!", 6f);
            yield return Pic(at + "hud-pin-stories-toast");
            hud.SetPinChip(null, null);
            hud.ForceStories = false;
            GameUiPanel.Toasts.Clear();

            hud.Open(GameHud.Shaping);
            yield return Pic(at + "tray");
            hud.Toast("Above the sheet.", 2f);
            yield return Pic(at + "tray-toast");
            hud.CloseSheet();
            GameUiPanel.Toasts.Clear();
            yield return Frames(6);

            var stack = CardStackView.Instance;
            CardStackView.DebugClearRead();
            CardStackView.DebugReturn(3);
            yield return Until(() => stack.Model.Mode == CardStackMode.Peek, 2f);
            yield return Pic(at + "cards-peek");
            yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-peek"));
            yield return Frames(6);
            yield return Pic(at + "cards-stack");
            yield return TapAt(GameUiPanel.ScreenCenter(stack.Root, "cards-skip"));
            yield return Frames(6);
            Sim().CardsSeenToNow();
            yield return Frames(6);

            yield return TapAt(GameUiPanel.ScreenCenter(hud.Root, "hud-history"));
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Pic(at + "timelapse");
            var screen = TimeLapseScreenView.Instance;
            yield return TapAt(GameUiPanel.ScreenCenter(screen.Root.Q("tl-close")));
            yield return new WaitForSecondsRealtime(0.8f);

            Sim().State.grime = 0f;
            Assert.IsTrue(Sim().ForceEraReady(), "an era is ready");
            MomentPlayer.DebugWaited(100);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Frames(20);
            yield return Pic(at + "watch");
            hud.Toast("Tap the ground to place the Grove. Two fingers move the camera.", 2f);
            yield return Pic(at + "watch-toast");
            GameUiPanel.Toasts.Clear();
            AssertNoErrors("old screens");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator GalleryPages()
        {
            System.IO.Directory.CreateDirectory(Shots + Dir);
            yield return OpenSlice(seed: 7531);
            DebugDock.ForceLocked = true;
            GlassGallery.DebugToggle();
            yield return Frames(8);
            var g = GlassGallery.Instance;
            int n = 0;
            foreach (var s in g.Sections.ToList())
            {
                g.ScrollTo(s);
                yield return Pic($"gallery-{n++:00}-{s.ToLowerInvariant().Replace(' ', '-')}");
            }
            // Long sections: the second screen of each
            foreach (var s in new[] { "Tokens", "Toast caption hint banner prompt", "Sheet", "Card" })
            {
                g.ScrollTo(s);
                yield return Frames(2);
                g.Scroll.scrollOffset += new Vector2(0f, 1600f);
                yield return Pic($"gallery-more-{s.Split(' ')[0].ToLowerInvariant()}");
            }
            g.ScrollTo("Tokens");
            g.OpenLive();
            yield return Pic("gallery-live-frost");
            g.CloseLive();
            AssertNoErrors("gallery shots");
        }
    }
}
