using System.Collections;
using AquariumCiv.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>
    /// The feedback sheet (#55, on the Keeper's Glass in #95, board 3.8): pick a kind, write a line, optionally keep the screenshot of the box taken
    /// just before the sheet opened, Send. It stands at 59 %; a phone too short for that gets it at full height, under the safe area, with Cancel and
    /// Send on the bottom edge (spec §2). With the keyboard up it folds to the head, the text and the buttons and rides on top. What Send says
    /// comes back as an ordinary toast. Every control has a stable name (<c>fb-sheet</c>, <c>fb-scrim</c>, <c>fb-close</c>, <c>fb-kind-bug</c>, <c>fb-text</c>,
    /// <c>fb-shot</c>, <c>fb-shot-image</c>, <c>fb-send</c>, <c>fb-cancel</c>). The HUD's Feedback icon and the Settings row call <see cref="Open"/>.
    /// Presses on the sheet never reach the box camera (<see cref="UiInput"/>).
    /// </summary>
    public class FeedbackUI : MonoBehaviour
    {
        static readonly FeedbackKind[] Kinds = { FeedbackKind.Bug, FeedbackKind.Confusing, FeedbackKind.Idea, FeedbackKind.Feel, FeedbackKind.Praise };
        static readonly string[] KindText = { "Something broke", "Confusing", "Idea", "Look / feel", "Loved it" };
        /// <summary>Send asks only for text (spec §4), so a note with no kind chosen goes as an idea.</summary>
        const FeedbackKind NoKind = FeedbackKind.Idea;
        /// <summary>The sheet's board height, 470 pt, doesn't fit 59 % of a screen shaped wider than this, on a 375 pt phone.</summary>
        public const float FullAspect = 470f / 0.59f / 375f;
        const string FoldClass = "fb-fold", RidingClass = "fb-sheet--keyboard", FullClass = "kg-sheet--full";

        public static FeedbackUI Instance { get; private set; }
        /// <summary>Set false (tests) to keep the bootstrap from adding the sheet to the scene.</summary>
        public static bool AutoCreate = true;
        /// <summary>Feedback is for test builds (the Editor, development builds, and slice test builds with the SLICE_TEST define,
        /// set for iOS and Android by SliceBuild.UseSliceBuildSettings so CI's release build has it) until a relay exists (feedback.md).</summary>
        public static bool InTestBuild =>
#if SLICE_TEST
            true;
#else
            Application.isEditor || Debug.isDebugBuild;
#endif

        UIDocument doc;
        VisualElement root;
        GlassSheet sheet;
        GlassScrim scrim;
        TextField text;
        Label count;
        GlassButton send;
        VisualElement shotMark;
        Image shotImage;
        readonly GlassChip[] kindChips = new GlassChip[Kinds.Length];
        int kind = -1;
        bool shotOn;
        byte[] shot;
        Texture2D shotPreview;
        bool capturing, full;
        float ridden;
        VisualElement hostSheet;
        bool hostKeepsTop;

        public bool IsOpen => sheet != null && sheet.IsOpen;
        public VisualElement Root => root;
        /// <summary>The box's screenshot goes with the note.</summary>
        public bool ShotOn => shotOn;
        /// <summary>The last capture (tests).</summary>
        public byte[] LastShot => shot;

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

        const string ShotClass = "hud-icon--shot";

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

        void Update()
        {
            if (sheet == null) return;
            Ride();
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        // ---------------- building ----------------
        void Build(VisualElement parent)
        {
            parent.Clear();
            root = new VisualElement { name = "fb-root", pickingMode = PickingMode.Ignore };
            root.AddToClassList("hud-fill");
            parent.Add(root);

            sheet = new GlassSheet(SheetHeight.Tall);
            scrim = new GlassScrim("fb", sheet);
            scrim.TappedOutside += Close;
            root.Add(scrim);

            sheet.Add(GlassSheet.Head("Tell us something", "fb-close", Close));
            sheet.Add(Text("What you saw, felt or expected. We read every one.", "type-sub", "fb-line", FoldClass));

            var chips = new VisualElement { name = "fb-kinds", pickingMode = PickingMode.Ignore };
            chips.AddToClassList("fb-kinds");
            chips.AddToClassList(FoldClass);
            for (int i = 0; i < Kinds.Length; i++)
            {
                int index = i;
                var c = new GlassChip(KindText[i], () => SelectKind(index), true) { name = "fb-kind-" + Kinds[i].ToString().ToLowerInvariant() };
                kindChips[i] = c;
                chips.Add(c);
            }
            sheet.Add(chips);

            text = GlassKeyboard.Tame(new TextField { name = "fb-text", multiline = true, maxLength = FeedbackReport.MaxTextChars }); // #645
            text.textEdition.placeholder = "Type here...";
            text.AddToClassList("kg-field");
            text.AddToClassList("kg-field--area");
            text.RegisterValueChangedCallback(_ => Refresh());
            sheet.Add(text);
            count = Text("", "type-note", "fb-count"); // #619: "N left" near the cap, so a long dictation never loses words unseen
            sheet.Add(count);

            var shotRow = new Button(() => { UiSound.Play(UiRole.Toggle); SetShot(!shotOn); }) { name = "fb-shot" };
            shotRow.RemoveFromClassList(Button.ussClassName);
            shotRow.AddToClassList("kg-row");
            shotRow.AddToClassList("kg-row--clear");
            shotRow.AddToClassList(FoldClass);
            shotImage = new Image { name = "fb-shot-image", scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            shotImage.AddToClassList("kg-thumb");
            shotRow.Add(shotImage);
            shotRow.Add(Text("Include a screenshot of the box", "type-body", "kg-row-label"));
            shotRow.Add(shotMark = new GlassIcon("check"));
            sheet.Add(shotRow);

            sheet.Add(Text("Also attached: your device model, the game's log and the box's numbers. No name, no personal data.", "type-note", "fb-note", FoldClass));

            var acts = new VisualElement { name = "fb-buttons" };
            acts.AddToClassList("kg-card-acts");
            acts.AddToClassList("fb-acts");
            acts.Add(new GlassButton("Cancel", Close, ButtonKind.Secondary, UiRole.Close) { name = "fb-cancel" });
            send = new GlassButton("Send", OnSend, ButtonKind.Primary, UiRole.Confirm) { name = "fb-send" };
            acts.Add(send);
            sheet.Add(acts);
            Refresh();
        }

        static Label Text(string s, params string[] classes)
        {
            var l = new Label(s) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        // ---------------- behaviour ----------------
        /// <summary>Open the sheet (the HUD's Settings page calls this too). The screenshot is taken first, without the HUD on it.</summary>
        public void Open()
        {
            if (capturing || IsOpen) return;
            preset = -1;
            StartCoroutine(OpenRoutine());
        }

        /// <summary>#636: open with a kind already picked (the quick question "Anything confusing?" answered Yes).</summary>
        public void OpenAs(FeedbackKind k)
        {
            if (capturing || IsOpen) return;
            preset = System.Array.IndexOf(Kinds, k);
            StartCoroutine(OpenRoutine());
        }

        int preset = -1;

        IEnumerator OpenRoutine()
        {
            capturing = true;
            root.style.display = DisplayStyle.None; // only the sheet leaves the picture: the HUD and the bubbles are what a UI note is about (#153)
            var hud = GameHud.Instance;
            hud?.Root.Q("hud-feedback")?.AddToClassList(ShotClass); // the finger just left it: the shot shows it at rest
            yield return null;
            yield return null; // a style change lands the frame after
            yield return new WaitForEndOfFrame();
            shot = ScreenShot.Jpg(ScreenCapture.CaptureScreenshotAsTexture());
            hud?.Root.Q("hud-feedback")?.RemoveFromClassList(ShotClass);
            root.style.display = DisplayStyle.Flex;
            capturing = false;

            if (shotPreview == null) shotPreview = new Texture2D(2, 2);
            bool ok = shot != null && shotPreview.LoadImage(shot);
            shotImage.image = ok ? shotPreview : null;
            sheet.Q("fb-shot").EnableInClassList("fb-no-shot", !ok); // a class, so the keyboard's fold can still hide it
            SetShot(ok);
            kind = preset;
            text.value = "";
            Refresh();
            ridden = 0f;
            Lay();
            scrim.Open();
            if (hud != null)
            {
                hostSheet = hud.OutsideSheet;
                hostKeepsTop = hud.OutsideKeepsTop;
                hud.SetOutsideSheet(sheet, !full);
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            sheet.style.translate = StyleKeyword.Null;
            ridden = 0f;
            scrim.Close();
            text.Blur();
            var hud = GameHud.Instance;
            if (hud != null && hud.OutsideSheet == sheet) hud.SetOutsideSheet(hostSheet, hostKeepsTop);
            hostSheet = null;
        }

        /// <summary>The sheet's shape for this screen and this keyboard.</summary>
        void Lay()
        {
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            full = Screen.height / Mathf.Max(1f, (float)Screen.width) < FullAspect;
            bool riding = ridden > 0f;
            sheet.EnableInClassList(RidingClass, riding);
            sheet.EnableInClassList(GlassKeyboard.RidingClass, riding);
            sheet.SetFull(full && !riding);
            sheet.SetInsets(riding ? new Vector4(i.x, i.y, i.z, 0f) : i, riding ? GlassKeyboard.Pad : GlassSheet.IsShort(Screen.width, Screen.height) ? GlassSheet.ShortBottomPad : GlassSheet.BottomPad);
            sheet.style.translate = riding ? new Translate(0f, -ridden) : StyleKeyword.Null;
        }

        void Ride()
        {
            float h = IsOpen ? GlassKeyboard.Height() : 0f;
            if (Mathf.Abs(h - ridden) < 0.5f) return;
            ridden = h;
            Lay();
            sheet.Surface.Follow(GlassMotion.SlideMs);
        }

        void SetShot(bool on)
        {
            shotOn = on;
            shotMark.style.visibility = on ? Visibility.Visible : Visibility.Hidden;
        }

        void SelectKind(int i) { kind = i; Refresh(); }

        void Refresh()
        {
            for (int i = 0; i < kindChips.Length; i++) kindChips[i].On = i == kind;
            bool ready = text.value.Trim().Length > 0;
            if (count != null)
            {
                count.text = FeedbackReport.CounterText(text.value.Length);
                count.style.display = count.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            send.Available = ready;
            send.Kind = ready ? ButtonKind.Primary : ButtonKind.Secondary;
        }

        void OnSend()
        {
            if (text.value.Trim().Length == 0) return;
            var sender = FeedbackSender.Ensure();
            if (sender.Submit(kind >= 0 ? Kinds[kind] : NoKind, text.value, shotOn ? shot : null)) Close();
            else ShowToast("One moment, then try again.");
        }

        void OnResult(bool sentNow, string message) => ShowToast(message);

        static void ShowToast(string message)
        {
            if (GameHud.Instance != null) GameHud.Instance.Toast(message, 3.5f);
        }
    }
}
