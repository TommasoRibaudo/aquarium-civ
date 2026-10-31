using System.Collections;
using AquariumCiv.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The always-reachable Feedback button (bottom-left, in the safe area) and its sheet: pick a kind, write a line,
    /// optionally keep the screenshot of the box that was taken just before the sheet opened, Send. UI Toolkit, built in
    /// code, every control has a stable name (<c>fb-button</c>, <c>fb-kind-bug</c>, <c>fb-text</c>, <c>fb-send</c>,
    /// <c>fb-cancel</c>, <c>fb-shot</c>, <c>fb-toast</c>) so tests and the tester agent find them. Until the HUD shell
    /// shares the game panel (<see cref="GameUiPanel"/>). While the HUD shell (<see cref="GameHud"/>) is up it hides its own
    /// button: the HUD has a Feedback button and a Settings row that call <see cref="Open"/>.
    /// Presses on the sheet or button never reach the box camera (<see cref="UiInput"/>).
    /// </summary>
    public class FeedbackUI : MonoBehaviour
    {
        // Palette (View/Palette.cs, the style kit): this assembly cannot see View, so the few colours it needs are copied.
        static readonly Color Plaster = Hex("#EFE3CC"), Timber = Hex("#6A4B36"), Terracotta = Hex("#C66F4E"), Frame = Hex("#2E2A27"),
            Canvas = Hex("#E2D3B3"), Moss = Hex("#5E7F43");

        static readonly FeedbackKind[] Kinds = { FeedbackKind.Bug, FeedbackKind.Confusing, FeedbackKind.Idea, FeedbackKind.Feel, FeedbackKind.Praise };
        static readonly string[] KindText = { "Something broke", "Confusing", "Idea", "Look / feel", "Loved it" };

        public static FeedbackUI Instance { get; private set; }
        /// <summary>Set false (tests) to keep the bootstrap from adding a button to the scene.</summary>
        public static bool AutoCreate = true;
        /// <summary>Feedback is for test builds (the Editor, development builds, and slice test builds with the SLICE_TEST define,
        /// set for iOS and Android by SliceBuild.UseSliceBuildSettings so CI's release build has it) until a relay exists (feedback.md).</summary>
        public static bool InTestBuild =>
#if SLICE_TEST
            true;
#else
            Application.isEditor || Debug.isDebugBuild;
#endif

        static bool hostedByHud;
        /// <summary>The HUD shell is up and shows its own Feedback button: hide this one.</summary>
        public static bool HostedByHud
        {
            get => hostedByHud;
            set { hostedByHud = value; if (Instance != null) Instance.ShowButton(); }
        }

        UIDocument doc;
        VisualElement root, button, sheet, shotRow;
        Label toast;
        TextField text;
        Button send;
        Label shotLabel;
        Toggle shotToggle;
        Image shotImage;
        readonly Button[] kindButtons = new Button[Kinds.Length];
        int kind = -1;
        byte[] shot;
        Texture2D shotPreview;
        bool capturing;
        Coroutine toastRoutine;

        public bool IsOpen => sheet != null && sheet.style.display == DisplayStyle.Flex;
        public VisualElement Root => root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoCreate || Instance != null || FindAnyObjectByType<GameClock>() == null) return;
            Create();
        }

        public static FeedbackUI Create()
        {
            var go = new GameObject("FeedbackUI");
            return go.AddComponent<FeedbackUI>();
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            FeedbackSender.Ensure();
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = GameUiPanel.Settings;
            doc.sortingOrder = 100; // over the HUD shell
        }

        void OnEnable()
        {
            if (Instance != this) return;
            Build(doc.rootVisualElement);
            UiInput.Add(IsOverUi);
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result += OnResult;
        }

        void OnDisable()
        {
            if (Instance != this) return;
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result -= OnResult;
            UiInput.Remove(IsOverUi);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (shotPreview != null) Destroy(shotPreview);
        }

        bool IsOverUi(Vector2 screenPos) => GameUiPanel.Over(root, screenPos);

        // ---------------- building ----------------
        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "fb-root", pickingMode = PickingMode.Ignore };
            Fill(root);
            parent.Add(root);

            float scale = 1080f / Mathf.Max(1, Screen.width);
            var safe = Screen.safeArea;
            float bottomInset = safe.y * scale, topInset = (Screen.height - safe.yMax) * scale;
            float leftInset = safe.x * scale;

            button = Pill("fb-button", "Feedback", Frame, Plaster, 34);
            button.style.position = Position.Absolute;
            button.style.left = 28 + leftInset; button.style.bottom = 28 + bottomInset;
            button.style.width = 240; button.style.height = 112;
            button.style.opacity = 0.82f;
            ((Button)button).clicked += OnButton;
            root.Add(button);

            sheet = new VisualElement { name = "fb-sheet", pickingMode = PickingMode.Position };
            Fill(sheet);
            sheet.style.backgroundColor = new Color(0.12f, 0.1f, 0.09f, 0.55f);
            sheet.style.display = DisplayStyle.None;
            root.Add(sheet);

            var card = new VisualElement { name = "fb-card" };
            card.style.position = Position.Absolute;
            card.style.left = 40; card.style.right = 40; card.style.top = 196 + topInset; // below the HUD title
            Pad(card, 36); Round(card, 36);
            card.style.backgroundColor = Plaster;
            sheet.Add(card);

            card.Add(Text("Tell us something", 52, Timber, FontStyle.Bold));
            var hint = Text("What you saw, felt or expected. We read every one.", 32, Timber, FontStyle.Normal);
            hint.style.marginTop = 6; hint.style.marginBottom = 22;
            card.Add(hint);

            var chips = new VisualElement { name = "fb-kinds" };
            chips.style.flexDirection = FlexDirection.Row; chips.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < Kinds.Length; i++)
            {
                int index = i;
                var b = Pill("fb-kind-" + Kinds[i].ToString().ToLowerInvariant(), KindText[i], Canvas, Timber, 34);
                b.style.height = 100; b.style.paddingLeft = b.style.paddingRight = 30;
                b.style.marginRight = 14; b.style.marginBottom = 14;
                b.clicked += () => SelectKind(index);
                kindButtons[i] = b;
                chips.Add(b);
            }
            card.Add(chips);

            text = new TextField { name = "fb-text", multiline = true, maxLength = FeedbackReport.MaxTextChars };
            text.textEdition.placeholder = "Type here...";
            text.style.marginTop = 8; text.style.height = 300;
            text.style.whiteSpace = WhiteSpace.Normal;
            text.RegisterValueChangedCallback(_ => Refresh());
            var input = text.Q(className: "unity-base-text-field__input");
            if (input != null)
            {
                input.style.fontSize = 36; input.style.color = Timber; input.style.backgroundColor = Color.white;
                input.style.unityTextAlign = TextAnchor.UpperLeft;
                Round(input, 20); Pad(input, 18);
            }
            card.Add(text);

            shotRow = new VisualElement { name = "fb-shot-row" };
            shotRow.style.flexDirection = FlexDirection.Row; shotRow.style.alignItems = Align.Center; shotRow.style.marginTop = 22;
            shotImage = new Image { name = "fb-shot-image", scaleMode = ScaleMode.ScaleToFit };
            shotImage.style.width = 120; shotImage.style.height = 160; shotImage.style.marginRight = 22;
            shotImage.style.backgroundColor = Canvas;
            shotRow.Add(shotImage);
            shotToggle = new Toggle { name = "fb-shot", value = true };
            shotToggle.text = "";
            shotToggle.style.marginRight = 14;
            shotToggle.Q(className: "unity-toggle__checkmark")?.ApplyScale(1.8f);
            shotRow.Add(shotToggle);
            shotLabel = Text("Include a screenshot of the box", 32, Timber, FontStyle.Normal);
            shotLabel.style.flexShrink = 1;
            shotRow.Add(shotLabel);
            card.Add(shotRow);

            var note = Text("Also attached: your device model, the game's log and the box's numbers. No name, no personal data.", 28, Timber, FontStyle.Italic);
            note.style.marginTop = 20; note.style.opacity = 0.75f;
            card.Add(note);

            var buttons = new VisualElement { name = "fb-buttons" };
            buttons.style.flexDirection = FlexDirection.Row; buttons.style.marginTop = 26;
            var cancel = Pill("fb-cancel", "Cancel", Canvas, Timber, 40);
            cancel.style.flexGrow = 1; cancel.style.height = 124; cancel.style.marginRight = 16;
            cancel.clicked += Close;
            send = Pill("fb-send", "Send", Terracotta, Plaster, 40);
            send.style.flexGrow = 1; send.style.height = 124;
            send.clicked += OnSend;
            buttons.Add(cancel); buttons.Add(send);
            card.Add(buttons);

            toast = Text("", 36, Plaster, FontStyle.Bold);
            toast.name = "fb-toast"; toast.pickingMode = PickingMode.Ignore;
            toast.style.position = Position.Absolute;
            toast.style.left = 80; toast.style.right = 80; toast.style.bottom = 230 + bottomInset; // above the HUD's bottom row
            toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            toast.style.backgroundColor = new Color(Frame.r, Frame.g, Frame.b, 0.92f);
            Pad(toast, 24); Round(toast, 28);
            toast.style.display = DisplayStyle.None;
            root.Add(toast);
            Refresh();
            ShowButton();
        }

        void ShowButton()
        {
            if (button == null) return;
            button.style.display = !HostedByHud && !IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------- behaviour ----------------
        void OnButton() => Open();

        /// <summary>Open the sheet (the HUD's Settings page calls this too). The screenshot is taken first, without the HUD on it.</summary>
        public void Open()
        {
            if (capturing || IsOpen) return;
            StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            capturing = true;
            root.style.display = DisplayStyle.None; // out of the picture, with the HUD
            var hud = GameHud.Instance;
            bool hudWasVisible = hud != null && hud.IsVisible;
            if (hudWasVisible) hud.SetVisible(false);
            yield return null;
            yield return new WaitForEndOfFrame();
            shot = ScreenShot.Jpg(ScreenCapture.CaptureScreenshotAsTexture());
            root.style.display = DisplayStyle.Flex;
            if (hudWasVisible) hud.SetVisible(true);
            capturing = false;

            if (shotPreview == null) shotPreview = new Texture2D(2, 2);
            bool ok = shot != null && shotPreview.LoadImage(shot);
            shotImage.image = ok ? shotPreview : null;
            shotRow.style.display = ok ? DisplayStyle.Flex : DisplayStyle.None;
            shotToggle.value = ok;
            kind = -1; text.value = "";
            Refresh();
            sheet.style.display = DisplayStyle.Flex;
            ShowButton();
        }

        public void Close()
        {
            sheet.style.display = DisplayStyle.None;
            ShowButton();
            text.Blur();
        }

        void SelectKind(int i) { kind = i; Refresh(); }

        void Refresh()
        {
            if (kindButtons[0] == null) return;
            for (int i = 0; i < kindButtons.Length; i++)
            {
                bool on = i == kind;
                kindButtons[i].style.backgroundColor = on ? Moss : Canvas;
                kindButtons[i].style.color = on ? Plaster : Timber;
            }
            send.SetEnabled(kind >= 0 && text.value.Trim().Length > 0);
            send.style.opacity = send.enabledSelf ? 1f : 0.45f;
        }

        void OnSend()
        {
            if (kind < 0 || text.value.Trim().Length == 0) return;
            var sender = FeedbackSender.Ensure();
            if (sender.Submit(Kinds[kind], text.value, shotToggle.value ? shot : null)) Close();
            else ShowToast("One moment, then try again.");
        }

        void OnResult(bool sentNow, string message) => ShowToast(message);

        void ShowToast(string message)
        {
            if (toast == null) return;
            toast.text = message;
            toast.style.display = DisplayStyle.Flex;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(HideToast());
        }

        IEnumerator HideToast()
        {
            yield return new WaitForSecondsRealtime(3.5f);
            toast.style.display = DisplayStyle.None;
        }

        // ---------------- element helpers ----------------
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }

        static void Pad(VisualElement e, float p) => e.style.paddingLeft = e.style.paddingRight = e.style.paddingTop = e.style.paddingBottom = p;

        static void Round(VisualElement e, float r) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        static Label Text(string s, float size, Color color, FontStyle style)
        {
            var l = new Label(s);
            l.style.fontSize = size; l.style.color = color; l.style.unityFontStyleAndWeight = style;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        static Button Pill(string name, string label, Color bg, Color fg, float size)
        {
            var b = new Button { name = name, text = label };
            b.style.backgroundColor = bg; b.style.color = fg; b.style.fontSize = size;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            Round(b, 56);
            b.style.marginLeft = b.style.marginRight = b.style.marginTop = b.style.marginBottom = 0;
            return b;
        }
    }

    static class VisualElementScale
    {
        public static void ApplyScale(this VisualElement e, float s) => e.style.scale = new Scale(new Vector2(s, s));
    }
}
