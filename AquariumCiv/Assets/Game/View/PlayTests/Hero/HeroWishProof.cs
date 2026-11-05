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
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AquariumCiv.View.PlayTests
{
    /// <summary>
    /// #107 hero piece: the Keeper's Glass wish card built in UI Toolkit over the live tank, to prove the mockup can be
    /// delivered. A proof, not the retrofit (#166): nothing here touches CardStackView, and the fonts, sheet and shadow in this
    /// folder are loaded by path in the Editor only, so no build carries them. Each state saves a PNG to Temp/agent/hero, one
    /// "-bg" twin with the text hidden so contrast can be read from the pixels, and a line per text box in hero.tsv.
    /// Run by hand: <c>--filter HeroWish --filter_type category --include_explicit true --async_tests true</c>.
    /// Result: docs/design/ui/hero/README.md.
    /// </summary>
    [Explicit("Writes proof images; run by hand, #107"), Category("HeroWish")]
    public class HeroWishProof : PlayTestBase
    {
        const string Dir = "Assets/Game/View/PlayTests/Hero/";
        static readonly string Out = Shots + "hero/";
        const float Pt = 2.75f, Bleed = 48f, BottomPad = 30f * Pt;
        const float BodyPitch = 15f * 1.35f * Pt; // the mockup's 1.35 on 15 pt

        const string Headline = "Mabel has a wish", Body = "Mabel the woodcutter would love rain on the west fields.", Year = "Year 70";
        // Longest name in NameData, longest job with a wish, longest form, longest wish.
        const string LongHeadline = "Constance has a wish", LongBody = "Constance the stallholder keeps wishing for a grove on the north side.", LongYear = "Year 118";

        VisualElement root, stack, shadow, sheet, baseImage, inner, image, words;
        Label title, portrait, body, year;
        Button back, next, showMe, give, skip;
        System.Func<Vector2, bool> over;
        GameObject go;
        int gives;

        static SimCore Sim => GameClock.Instance.ActiveSim;

        [OneTimeSetUp]
        public void Wipe()
        {
            if (Directory.Exists(Out)) Directory.Delete(Out, true);
            Directory.CreateDirectory(Out);
        }

        public override void TearDown()
        {
            if (over != null) UiInput.Remove(over);
            over = null;
            FrostBackdrop.Stop();
            if (go != null) Object.Destroy(go);
            DebugDock.ResetLock();
            base.TearDown();
        }

        // ---------- the card ----------

        static Label Text(VisualElement parent, string name, string text, params string[] classes)
        {
            var l = new Label(text) { name = name, pickingMode = PickingMode.Ignore, enableRichText = true };
            foreach (var c in classes) l.AddToClassList(c);
            parent.Add(l);
            return l;
        }

        static VisualElement Box(VisualElement parent, string name, string cls)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            parent.Add(e);
            return e;
        }

        Button Press(VisualElement parent, string name, string text, System.Action clicked, params string[] classes)
        {
            var b = new Button(clicked) { name = name, text = text };
            foreach (var c in classes) b.AddToClassList(c);
            parent.Add(b);
            return b;
        }

        void Build(PanelSettings panel = null)
        {
            go = new GameObject("HeroWish");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel != null ? panel : GameUiPanel.Settings;
            doc.sortingOrder = 90;
#if UNITY_EDITOR
            var uss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(Dir + "HeroWish.uss");
            Assert.IsNotNull(uss, "HeroWish.uss");
            doc.rootVisualElement.styleSheets.Add(uss);
#endif
            root = Box(doc.rootVisualElement, "hero-root", "hero-root");
            stack = Box(root, "hero-stack", "hero-stack");
            shadow = Box(stack, "hero-shadow", "hero-shadow");
            sheet = Box(stack, "hero-sheet", "surface");
            sheet.pickingMode = PickingMode.Position;
            baseImage = Box(sheet, "hero-frost-base", "hero-frost-image");
            inner = Box(sheet, "hero-frost-inner", "hero-frost-inner");
            image = Box(inner, "hero-frost-image", "hero-frost-image");
            Box(sheet, "hero-tint", "hero-tint");
            words = Box(sheet, "hero-words", "hero-words");

            var dots = Box(words, "hero-dots", "hero-dots");
            for (int i = 0; i < 5; i++)
            {
                var d = Box(dots, "hero-dot-" + i, "hero-dot");
                if (i == 2) d.AddToClassList("hero-dot--on");
            }
            var head = Box(words, "hero-head", "hero-head");
            portrait = Text(head, "hero-portrait", "M", "hero-portrait");
            title = Text(head, "hero-title", Headline, "hero-title");
            skip = Press(head, "hero-skip", "Skip all", null, "hero-quiet");
            body = Text(words, "hero-body", Lines(Body), "hero-body");
            year = Text(words, "hero-year", Year, "hero-sub");
            var acts = Box(words, "hero-acts", "hero-acts");
            back = Press(acts, "hero-back", "Back", null, "hero-btn");
            next = Press(acts, "hero-next", "Next", null, "hero-btn");
            showMe = Press(acts, "hero-showme", "Show me", null, "hero-btn");
            give = Press(acts, "hero-give", "Give", () => gives++, "hero-btn", "hero-btn--primary");
            back.style.display = DisplayStyle.None; // the board's card has no Back; the longest-text shot does

            // Faked: an iPhone of this size has 59 pt above and 34 pt below. The Editor reports none.
            var safe = GameUiPanel.SafeInsets(new Rect(0f, 102f, 1179f, 2556f - 102f - 177f), 1179f, 2556f);
            words.style.paddingBottom = Mathf.Max(BottomPad, safe.w);

            sheet.RegisterCallback<GeometryChangedEvent>(_ => Align());
            over = p => GameUiPanel.Over(root, p);
            UiInput.Add(over);
        }

        /// <summary>Same as FrostTestPanel: the whole-screen copy, laid under the sheet so it lines up with the tank.</summary>
        void Align()
        {
            var screen = root.worldBound;
            image.style.left = -sheet.worldBound.x;
            image.style.top = Bleed - sheet.worldBound.y;
            image.style.width = screen.width;
            image.style.height = screen.height;
            baseImage.style.left = -sheet.worldBound.x;
            baseImage.style.top = -sheet.worldBound.y;
            baseImage.style.width = screen.width;
            baseImage.style.height = screen.height;
        }

        /// <summary>A px line height: a percent tag is a percent of the font's own line, not of its size (see the probe).</summary>
        static string Lines(string s) => $"<line-height={BodyPitch:0.##}px>{s}";

        void Frost(bool on)
        {
            image.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            baseImage.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            sheet.EnableInClassList("surface--flat", !on);
            if (on)
            {
                var copy = Background.FromRenderTexture(FrostBackdrop.Start(4));
                foreach (var e in new[] { image, baseImage })
                {
                    e.style.backgroundImage = copy;
                    e.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
                }
            }
            else FrostBackdrop.Stop();
        }

        void Blur(float px)
        {
            var f = new FilterFunction(FilterFunctionType.Blur);
            f.AddParameter(new FilterParameter(px));
            inner.style.filter = new List<FilterFunction> { f };
        }

        void Content(string h, string b, string y, string initial, bool withBack)
        {
            title.text = h;
            body.text = Lines(b);
            year.text = y;
            portrait.text = initial;
            back.style.display = withBack ? DisplayStyle.Flex : DisplayStyle.None;
        }

        IEnumerable<Label> Texts() => root.Query<Label>().ToList();
        IEnumerable<Button> Buttons() => root.Query<Button>().ToList();

        // ---------- saving ----------

        static void SavePng(string name)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Out + name + ".png", tex.EncodeToPNG());
            Debug.Log($"[Hero] {name} {tex.width}x{tex.height}");
            Object.Destroy(tex);
        }

        static string Rect(VisualElement e, float scale)
        {
            var r = e.worldBound;
            return $"{r.x * scale:0}\t{r.y * scale:0}\t{r.width * scale:0}\t{r.height * scale:0}";
        }

        /// <summary>The state, then the same frame's neighbour with every word transparent, then each text box in screen px.</summary>
        IEnumerator Cap(string name)
        {
            yield return Frames(4);
            yield return new WaitForEndOfFrame();
            SavePng(name);
            var labels = Texts().Cast<TextElement>().Concat(Buttons()).Where(e => e.resolvedStyle.display != DisplayStyle.None).ToList();
            var colours = labels.Select(l => l.style.color).ToList();
            foreach (var l in labels) l.style.color = Color.clear;
            yield return Frames(2);
            yield return new WaitForEndOfFrame();
            SavePng(name + "-bg");
            for (int i = 0; i < labels.Count; i++) labels[i].style.color = colours[i];
            float scale = Screen.width / root.worldBound.width;
            foreach (var l in labels)
                File.AppendAllText(Out + "hero.tsv", $"{name}\t{l.name}\t{Rect(l, scale)}\n");
            yield return Frames(2);
        }

        /// <summary>Puts the box behind the sheet, as Show me and following a person do. On the home view only the shelf is there.</summary>
        IEnumerator Close()
        {
            Camera.main.GetComponent<BoxCamera>().FlyTo(new Vector3(0f, 2.2f, 0f), 0.55f, 0.2f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Frames(20);
        }

        /// <summary>Editor only and a desktop GPU: ranks the two blurs, says nothing of a phone.</summary>
        static IEnumerator FrameCost(string label)
        {
            int vs = QualitySettings.vSyncCount, fps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var ms = new List<float>();
            yield return Frames(20);
            for (int i = 0; i < 300; i++) { yield return null; ms.Add(Time.unscaledDeltaTime * 1000f); }
            QualitySettings.vSyncCount = vs;
            Application.targetFrameRate = fps;
            ms.Sort();
            Debug.Log($"[Hero] frame {label}: median {ms[ms.Count / 2]:0.00} ms of {ms.Count}");
        }

        IEnumerator Clean()
        {
            Sim.CleanAll();
            Sim.CardsSeenToNow();
            PeopleView.DebugSetHour(11f);
            NightLamps.DebugSetLevel(0f);
            yield return Frames(40);
        }

        // ---------- the proof ----------

        /// <summary>The same card on a clone of the panel with the serialized forceGammaRendering on: does UI then blend like CSS?</summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator GammaVariant()
        {
            yield return OpenSlice(seed: 7531);
            DebugDock.ForceLocked = true;
            for (int y = 0; y < 7; y++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            yield return Clean();
            GameHud.Instance.Root.style.display = DisplayStyle.None;
            var clone = Object.Instantiate(GameUiPanel.Settings);
            clone.forceGammaRendering = true;
            Build(clone);
            Frost(true);
            yield return Frames(10);
            yield return Cap("gamma");
            AssertNoErrors("gamma");
            Object.Destroy(go);
            go = null;
            yield return null;
            Object.Destroy(clone);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator WishCard()
        {
            yield return OpenSlice(seed: 7531);
            DebugDock.ForceLocked = true;
            for (int y = 0; y < 7; y++) { SimLayoutSource.DebugSkipYears(10f); yield return null; }
            yield return Clean();
            GameHud.Instance.Root.style.display = DisplayStyle.None; // the board's card has no HUD over it
            Sim.CardsSeenToNow();
            yield return Frames(10);

            yield return new WaitForEndOfFrame();
            SavePng("tank"); // the mockup's background, so the two are compared over the same picture

            Build();
            Frost(true);
            yield return Frames(6);
            float panelHeight = root.worldBound.height;

            // Measured rules
            Assert.AreEqual(0.30f, stack.worldBound.height / panelHeight, 0.002f, "sheet height");
            foreach (var b in new VisualElement[] { skip, next, showMe, give })
            {
                Assert.GreaterOrEqual(b.worldBound.height, 121f - 0.5f, b.name + " height");
                Assert.GreaterOrEqual(b.worldBound.width, 121f - 0.5f, b.name + " width");
            }
            Assert.Greater(portrait.resolvedStyle.unityFontDefinition.font != null ? 1 : 0, 0, "Lora did not reach the portrait");
            Debug.Log($"[Hero] panel {root.worldBound.width}x{panelHeight} screen {Screen.width}x{Screen.height} sheet {stack.worldBound.height} padBottom {words.resolvedStyle.paddingBottom}");

            // A real tap reaches the primary, and the game's camera stays out of it.
            yield return TapAt(GameUiPanel.ScreenCenter(give));
            yield return Frames(3);
            Assert.AreEqual(1, gives, "Give took one tap");

            // Line height: what the rich tags really do
            var probeHost = new VisualElement();
            probeHost.AddToClassList("hero-words");
            probeHost.style.visibility = Visibility.Hidden;
            sheet.Add(probeHost);
            string two = "Mabel the woodcutter would love rain on the west fields, and she would love it twice as much on the east ones.";
            var plain = Text(probeHost, "p0", two, "hero-body");
            var pct = Text(probeHost, "p1", "<line-height=135%>" + two, "hero-body");
            var px = Text(probeHost, "p2", Lines(two), "hero-body");
            yield return Frames(3);
            Debug.Log($"[Hero] two-line label height, panel px: plain {plain.worldBound.height:0.0}, 135% {pct.worldBound.height:0.0}, {BodyPitch:0.0}px {px.worldBound.height:0.0}");
            probeHost.RemoveFromHierarchy();

            yield return Cap("frost");
            Debug.Log($"[Hero] title {Rect(title, 1f)} body {Rect(body, 1f)} year {Rect(year, 1f)} next {Rect(next, 1f)} give {Rect(give, 1f)} skip {Rect(skip, 1f)}");

            baseImage.style.display = DisplayStyle.None; // what the sheet looks like without the sharp copy under the blur
            yield return Cap("edge-band");
            baseImage.style.display = StyleKeyword.Null;

            // The shadow alone, solid, to read its shape
            sheet.style.visibility = Visibility.Hidden;
            shadow.style.unityBackgroundImageTintColor = Color.black;
            yield return Frames(3);
            yield return new WaitForEndOfFrame();
            SavePng("shadow-solid");
            shadow.style.unityBackgroundImageTintColor = StyleKeyword.Null;
            sheet.style.visibility = StyleKeyword.Null;

            Blur(38.5f); // the mockup's 14 CSS px, in panel px
            yield return Cap("blur-38");
            inner.style.filter = StyleKeyword.Null;

            Frost(false);
            long copies = FrostBackdrop.CopiesRecorded;
            yield return Cap("flat");
            Assert.IsFalse(FrostBackdrop.Active, "flat still has a pass");
            Assert.AreEqual(copies, FrostBackdrop.CopiesRecorded, "flat still copies the frame");
            Frost(true);

            // Linear blending thins dark text. A hair of outline in the text's own colour gives the weight back.
            foreach (var t in Texts().Cast<TextElement>().Concat(Buttons()))
            {
                t.style.unityTextOutlineWidth = 0.3f;
                t.style.unityTextOutlineColor = t.resolvedStyle.color;
            }
            yield return Cap("text-weight");
            foreach (var t in Texts().Cast<TextElement>().Concat(Buttons()))
            {
                t.style.unityTextOutlineWidth = StyleKeyword.Null;
                t.style.unityTextOutlineColor = StyleKeyword.Null;
            }

            Content(LongHeadline, LongBody, LongYear, "C", true);
            yield return Frames(4);
            yield return Cap("long");
            float bottomGap = sheet.worldBound.yMax - give.worldBound.yMax;
            float textEnd = year.worldBound.yMax;
            Debug.Log($"[Hero] long: gap year-to-buttons {give.worldBound.yMin - textEnd:0.0}, buttons-to-edge {bottomGap:0.0}, row right {give.worldBound.xMax:0.0}, back-left {back.worldBound.xMin:0.0}");
            Assert.Greater(give.worldBound.yMin, textEnd, "long text ran into the buttons");
            Assert.LessOrEqual(give.worldBound.xMax, root.worldBound.width - 44f + 0.5f, "button row overflows");
            Assert.GreaterOrEqual(give.worldBound.xMin, showMe.worldBound.xMax, "buttons overlap");
            Content(Headline, Body, Year, "M", false);

            yield return Close();
            yield return Cap("close");
            root.style.display = DisplayStyle.None;
            yield return Frames(3);
            yield return new WaitForEndOfFrame();
            SavePng("tank-close");
            root.style.display = StyleKeyword.Null;
            Blur(38.5f);
            yield return Cap("close-blur-38");
            yield return FrameCost("blur 38");
            inner.style.filter = StyleKeyword.Null;
            yield return Frames(3);
            yield return FrameCost("blur 14");

            Sim.AddMess(0.7f, 0.5f);
            yield return Frames(60);
            Debug.Log($"[Hero] grime {Sim.State.grime:0.00}");
            yield return Cap("close-dirty");
            Frost(false);
            yield return Cap("close-dirty-flat");
            Frost(true);
            Camera.main.GetComponent<BoxCamera>().FlyTo(Vector3.zero, 1f, 0.2f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Frames(20);
            yield return Cap("dirty");
            Frost(false);
            yield return Cap("dirty-flat");
            Frost(true);

            yield return Clean();
            PeopleView.DebugSetHour(22f);
            NightLamps.DebugSetLevel(1f);
            yield return Frames(40);
            yield return Cap("night");
            NightLamps.DebugSetLevel(0f);

            // The floor: frost over pure black, the worst the tint can face
            image.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            baseImage.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            sheet.style.backgroundColor = Color.black;
            inner.style.display = DisplayStyle.None;
            yield return Cap("black");

            AssertNoErrors("hero");
        }
    }
}
