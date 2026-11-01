using System;
using System.Collections.Generic;
using System.Globalization;
using AquariumCiv.Game;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// Debug-only test of the frosted sheet on a real phone (#178): a 30 % Keeper's Glass sheet over the live tank, five modes,
    /// CPU and GPU time per mode, an automatic run, and a way to send the numbers. Exists only while open: nothing of it is
    /// in the scene or the renderer until the debug panel makes it, and closing destroys it. Styled in code so that it needs
    /// no sheet of its own in the build.
    /// </summary>
    public class FrostTestPanel : MonoBehaviour
    {
        const int WindowFrames = 1800; // 20 s at 90 fps, 30 s at 60: an automatic step never rolls over
        const float SettleSeconds = 2f, RefreshSeconds = 0.5f, Bleed = 48f;
        static readonly Color Ink = Hex(0x3D2A1E), InkSoft = Hex(0x4A3324), Brass = Hex(0xD9B873), Primary = Hex(0x9E5033), OnPrimary = Hex(0xEFE3CC);
        static readonly Color Frost = Hex(0xF3E9D6);

        public static FrostTestPanel Instance { get; private set; }
        /// <summary>Tests: takes the note's text instead of the real sender, so a test never files an issue.</summary>
        public static Func<string, bool> SendOverride;

        /// <summary>Seconds per mode in the automatic run. About 80 s in all; tests shorten it.</summary>
        public float autoSeconds = 20f;
        public float settleSeconds = SettleSeconds;

        public FrostMode Mode { get; private set; } = FrostMode.Off;
        public bool AutoRunning => autoStep >= 0;
        public VisualElement Root => root;
        public FrostWindow Window(FrostMode m) => windows[(int)m];

        readonly FrostWindow[] windows = { new(WindowFrames), new(WindowFrames), new(WindowFrames), new(WindowFrames), new(WindowFrames) };
        readonly Label[,] cells = new Label[FrostModes.Count, 4];
        readonly Button[] modeButtons = new Button[FrostModes.Count];
        readonly FrameTiming[] timing = new FrameTiming[1];
        UIDocument doc;
        VisualElement root, sheet, sharp, inner, image, tint;
        Label status;
        Button capButton;
        float settleUntil, nextRefresh, stepEnd, batteryAtStart;
        int autoStep = -1, frameRateWas;
        bool timingSeen;
        string note = "";
        Func<Vector2, bool> over;

        /// <summary>Debug panel: opens the test, or closes it when open.</summary>
        public static string DebugToggle()
        {
            if (Instance != null) { Instance.Close(); return "frost test closed"; }
            new GameObject("FrostTestPanel").AddComponent<FrostTestPanel>();
            return "frost test open";
        }

        static float Budget(int cap) => cap > 0 ? 1000f / cap : 0f;

        /// <summary>The frame cap in force. A phone left at -1 runs at 30.</summary>
        static int Cap => Application.targetFrameRate > 0 ? Application.targetFrameRate : Application.isMobilePlatform ? 30 : 0;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            over = p => GameUiPanel.Over(root, p);
            frameRateWas = Application.targetFrameRate;
            batteryAtStart = SystemInfo.batteryLevel;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 90; // over the HUD, under the feedback sheet
        }

        void OnEnable()
        {
            if (Instance != this) return;
            FrostSwitch.Suspended = true; // this test owns the copy while it runs
            Build(doc.rootVisualElement);
            UiInput.Add(over);
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result += OnSent;
            Apply(FrostMode.Off);
        }

        void OnDisable()
        {
            if (Instance != this) return;
            UiInput.Remove(over);
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result -= OnSent;
            image.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            sharp.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            FrostBackdrop.Stop();
            FrostSwitch.Suspended = false;
            Application.targetFrameRate = frameRateWas;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Close()
        {
            enabled = false;
            Destroy(gameObject);
        }

        void OnSent(bool sent, string message) { note = message; Refresh(); }

        // ---------- modes ----------

        /// <summary>Picks a mode by hand, which ends an automatic run.</summary>
        public void SetMode(FrostMode m)
        {
            autoStep = -1;
            Apply(m);
        }

        void Apply(FrostMode m)
        {
            Mode = m;
            settleUntil = Time.realtimeSinceStartup + settleSeconds; // the switch itself isn't what's being measured
            int divisor = FrostModes.Divisor(m);
            image.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            sharp.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            if (divisor > 0)
            {
                var copy = Background.FromRenderTexture(FrostBackdrop.Start(divisor));
                image.style.backgroundImage = copy;
                sharp.style.backgroundImage = copy; // the production recipe: without it a light band runs down the sheet's sides
                var blur = new FilterFunction(FilterFunctionType.Blur);
                blur.AddParameter(new FilterParameter(FrostModes.Blur(m)));
                inner.style.filter = new List<FilterFunction> { blur };
            }
            else
            {
                FrostBackdrop.Stop();
                inner.style.filter = new List<FilterFunction>();
            }
            inner.style.display = divisor > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            sharp.style.display = divisor > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            sheet.style.display = m == FrostMode.Off ? DisplayStyle.None : DisplayStyle.Flex;
            tint.style.backgroundColor = WithAlpha(Frost, FrostModes.TintAlpha(m));
            // The blur fades to nothing at the copy's own edge, the screen's edge: an opaque sheet keeps the HUD from showing through there.
            sheet.style.backgroundColor = divisor > 0 ? Frost : Color.clear;
            for (int i = 0; i < modeButtons.Length; i++)
            {
                bool on = i == (int)m;
                modeButtons[i].style.backgroundColor = on ? Primary : WithAlpha(Frost, FrostModes.FlatAlpha);
                modeButtons[i].style.color = on ? OnPrimary : InkSoft;
            }
            Refresh();
        }

        // ---------- automatic run ----------

        /// <summary>Every mode in turn, then back to off with the numbers on screen.</summary>
        public void StartAuto()
        {
            foreach (var w in windows) w.Clear();
            note = "";
            autoStep = 0;
            stepEnd = Time.realtimeSinceStartup + autoSeconds;
            Apply((FrostMode)0);
        }

        void StepAuto()
        {
            autoStep++;
            if (autoStep >= FrostModes.Count)
            {
                autoStep = -1;
                Apply(FrostMode.Off);
                note = "Run done. Send to file these numbers.";
                Debug.Log("[Frost] " + SummaryText());
                Refresh();
                return;
            }
            stepEnd = Time.realtimeSinceStartup + autoSeconds;
            Apply((FrostMode)autoStep);
        }

        void ToggleCap()
        {
            Application.targetFrameRate = Cap == 60 ? 30 : 60;
            foreach (var w in windows) w.Clear(); // numbers at another cap don't belong in the same row
            Refresh();
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now >= settleUntil) Sample();
            if (AutoRunning && now >= stepEnd) StepAuto();
            if (now >= nextRefresh) Refresh();
        }

        void Sample()
        {
            FrameTimingManager.CaptureFrameTimings();
            var window = windows[(int)Mode];
            if (FrameTimingManager.GetLatestTimings(1, timing) == 0)
            {
                float ms = Time.unscaledDeltaTime * 1000f;
                window.Add(ms, ms, 0f); // no stats on this device: frame time only, and the summary says so
                return;
            }
            timingSeen = true;
            var t = timing[0];
            window.Add((float)t.cpuFrameTime, FrostStats.CpuWork(t.cpuMainThreadFrameTime, t.cpuMainThreadPresentWaitTime, t.cpuRenderThreadFrameTime), (float)t.gpuFrameTime);
        }

        // ---------- the numbers ----------

        void Refresh()
        {
            nextRefresh = Time.realtimeSinceStartup + RefreshSeconds;
            float budget = Budget(Cap);
            for (int i = 0; i < FrostModes.Count; i++)
            {
                var r = windows[i].Read(budget);
                bool has = r.Frames > 0;
                SetCell(i, 0, has ? FrostStats.Pair(r.FrameAvg, r.FrameWorst) : "-");
                SetCell(i, 1, has ? FrostStats.Pair(r.CpuAvg, r.CpuWorst) : "-");
                SetCell(i, 2, has ? FrostStats.Pair(r.GpuAvg, r.GpuWorst) : "-");
                SetCell(i, 3, has ? r.Drops.ToString(CultureInfo.InvariantCulture) : "-");
            }
            string run = AutoRunning
                ? $"Run {autoStep + 1}/{FrostModes.Count}, {Mathf.CeilToInt(stepEnd - Time.realtimeSinceStartup)} s left"
                : note;
            status.text = $"{(timingSeen ? "CPU and GPU from frame timing" : "No frame timing yet")}. ms avg/worst. {run}";
            capButton.text = Cap == 60 ? "Cap 60" : "Cap 30";
        }

        void SetCell(int row, int column, string text)
        {
            if (cells[row, column].text != text) cells[row, column].text = text; // an unchanged label costs nothing
        }

        /// <summary>The note's text: one line for the phone, then the table.</summary>
        public string SummaryText()
        {
            float scale = UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.renderScale : 1f;
            string header = string.Format(CultureInfo.InvariantCulture, "[debug] Frost test #178 on {0}\n{1}x{2} scale {3:0.##} cap {4} {5} battery {6:0}>{7:0}%",
                SystemInfo.deviceModel, Screen.width, Screen.height, scale, Cap, timingSeen ? "timing on" : "timing off", batteryAtStart * 100f, SystemInfo.batteryLevel * 100f);
            return FrostStats.Summary(header, windows, Budget(Cap));
        }

        /// <summary>Sends the numbers as a feedback note. The whole text also goes in the log, which the note carries.</summary>
        public void Send()
        {
            string text = SummaryText();
            Debug.Log("[Frost] " + text);
            bool taken = SendOverride != null ? SendOverride(text) : FeedbackSender.Ensure().Submit(FeedbackKind.Idea, text, null);
            note = taken ? "Sending..." : "Refused: wait a few seconds and send again.";
            Refresh();
        }

        // ---------- the panel ----------

        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "frost-root", pickingMode = PickingMode.Ignore };
            Fill(root);
            parent.Add(root);
            var safe = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);

            BuildSheet(safe.w);
            BuildControls(safe.y);
        }

        void BuildSheet(float bottomInset)
        {
            sheet = new VisualElement { name = "frost-sheet", pickingMode = PickingMode.Position };
            sheet.style.position = Position.Absolute;
            sheet.style.left = 0; sheet.style.right = 0; sheet.style.bottom = 0;
            sheet.style.height = Length.Percent(30f);
            sheet.style.borderTopLeftRadius = 48; sheet.style.borderTopRightRadius = 48;
            sheet.style.overflow = Overflow.Hidden;
            root.Add(sheet);

            // Reaches above the sheet so the blur has real pixels at its top edge, then the sheet clips it. Not past the screen:
            // a filter region running off-screen left a light band down one side.
            sharp = new VisualElement { name = "frost-sharp", pickingMode = PickingMode.Ignore };
            sharp.style.position = Position.Absolute;
            sharp.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            sheet.Add(sharp);

            inner = new VisualElement { name = "frost-inner", pickingMode = PickingMode.Ignore };
            inner.style.position = Position.Absolute;
            inner.style.left = 0; inner.style.right = 0; inner.style.top = -Bleed; inner.style.bottom = 0;
            inner.style.overflow = Overflow.Hidden;
            sheet.Add(inner);

            image = new VisualElement { name = "frost-image", pickingMode = PickingMode.Ignore };
            image.style.position = Position.Absolute;
            image.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            inner.Add(image);

            tint = new VisualElement { name = "frost-tint", pickingMode = PickingMode.Ignore };
            Fill(tint);
            tint.style.borderTopLeftRadius = 48; tint.style.borderTopRightRadius = 48;
            Border(tint, 2f);
            tint.style.borderBottomWidth = 0;
            sheet.Add(tint);

            var words = new VisualElement { name = "frost-words", pickingMode = PickingMode.Ignore };
            Fill(words);
            words.style.paddingLeft = 56; words.style.paddingRight = 56; words.style.paddingTop = 44; words.style.paddingBottom = 28 + bottomInset;
            words.Add(Text("The Keeper's Glass", 54, Ink, true));
            var body = Text("Nine huts now, and smoke from the kiln by the shore. Ada has taken the old path up the hill and the children follow her in the mornings. The rain came early this year, and the barley will be late.", 38, Ink, false);
            body.style.marginTop = 16;
            words.Add(body);
            sheet.Add(words);

            sheet.RegisterCallback<GeometryChangedEvent>(_ => Align());
        }

        /// <summary>Lays the whole screen copy under the sheet, so what's behind the glass lines up with the tank around it.</summary>
        void Align()
        {
            var screen = root.worldBound;
            sharp.style.left = -sheet.worldBound.x;
            sharp.style.top = -sheet.worldBound.y;
            sharp.style.width = screen.width;
            sharp.style.height = screen.height;
            image.style.left = -sheet.worldBound.x; // from the sheet: the backdrop is hidden in flat mode and has no bounds
            image.style.top = Bleed - sheet.worldBound.y;
            image.style.width = screen.width;
            image.style.height = screen.height;
        }

        void BuildControls(float topInset)
        {
            var card = new VisualElement { name = "frost-controls", pickingMode = PickingMode.Position };
            card.style.position = Position.Absolute;
            card.style.left = 32; card.style.right = 32; card.style.top = 196 + topInset; // below the HUD title and the dbg button
            card.style.backgroundColor = WithAlpha(Frost, FrostModes.FlatAlpha);
            card.style.borderTopLeftRadius = 28; card.style.borderTopRightRadius = 28; card.style.borderBottomLeftRadius = 28; card.style.borderBottomRightRadius = 28;
            Border(card, 2f);
            card.style.paddingLeft = 20; card.style.paddingRight = 20; card.style.paddingTop = 16; card.style.paddingBottom = 16;
            root.Add(card);

            string[] heads = { "frame", "cpu", "gpu", "drops" };
            var head = Row();
            head.Add(Cell("mode", 240, true));
            for (int c = 0; c < heads.Length; c++) head.Add(Cell(heads[c], c == 3 ? 110 : 200, true));
            card.Add(head);
            for (int m = 0; m < FrostModes.Count; m++)
            {
                var row = Row();
                row.Add(Cell(FrostModes.Label((FrostMode)m), 240, false));
                for (int c = 0; c < heads.Length; c++) row.Add(cells[m, c] = Cell("-", c == 3 ? 110 : 200, false));
                card.Add(row);
            }
            status = Text("", 28, Ink, false);
            status.name = "frost-status";
            status.style.marginTop = 8; status.style.marginBottom = 8;
            card.Add(status);

            var modes = Row();
            for (int m = 0; m < FrostModes.Count; m++)
            {
                var mode = (FrostMode)m;
                modes.Add(modeButtons[m] = Press($"frost-mode-{m}", FrostModes.Label(mode), () => SetMode(mode)));
            }
            card.Add(modes);
            var actions = Row();
            actions.Add(Press("frost-auto", "Auto run", StartAuto));
            actions.Add(Press("frost-send", "Send", Send));
            actions.Add(capButton = Press("frost-cap", "Cap 30", ToggleCap));
            actions.Add(Press("frost-close", "Close", Close));
            actions.style.marginTop = 8;
            card.Add(actions);
        }

        static VisualElement Row()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            return r;
        }

        static Label Cell(string text, float width, bool head)
        {
            var l = Text(text, 30, Ink, head);
            l.style.width = width;
            l.style.flexShrink = 1;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            return l;
        }

        static Label Text(string text, float size, Color color, bool bold)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            return l;
        }

        static Button Press(string name, string text, Action clicked)
        {
            var b = new Button(clicked) { name = name, text = text };
            b.style.flexGrow = 1; b.style.flexBasis = 0;
            b.style.height = 121; // 44 pt
            b.style.fontSize = 32;
            b.style.color = InkSoft;
            b.style.backgroundColor = WithAlpha(Frost, FrostModes.FlatAlpha);
            b.style.marginLeft = 4; b.style.marginRight = 4; b.style.marginTop = 0; b.style.marginBottom = 0;
            b.style.borderTopLeftRadius = 20; b.style.borderTopRightRadius = 20; b.style.borderBottomLeftRadius = 20; b.style.borderBottomRightRadius = 20;
            Border(b, 2f);
            return b;
        }

        static void Border(VisualElement e, float width)
        {
            e.style.borderTopWidth = width; e.style.borderLeftWidth = width; e.style.borderRightWidth = width; e.style.borderBottomWidth = width;
            e.style.borderTopColor = Brass; e.style.borderLeftColor = Brass; e.style.borderRightColor = Brass; e.style.borderBottomColor = Brass;
        }

        static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static Color Hex(int rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
