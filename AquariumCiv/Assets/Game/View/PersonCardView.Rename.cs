using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// #575 (playtest note #548): the Keeper can rename a notable from their card. A small "Call them something else" pencil button on the
    /// About tab, under the family and friend chips (#674) opens a one-line field with Save; Save tidies the text, empty keeps the old name, a refusal is said under the
    /// field. The new first name shows everywhere from then on (the card, the paper tag, History lines written later); chronicle lines
    /// already written keep the old name (they are plain text). While the field is open the card sits on the phone keyboard and fits the room between the top safe area and the keyboard (#584): shorter, tabs folded away, the field and its buttons scrolled into view. Two living
    /// people may not share a first name (the same rule as at birth): that is refused with a kind word. Names: <c>person-card-rename</c>
    /// (the button), <c>person-card-rename-field</c>, <c>-save</c>, <c>-cancel</c>, <c>-msg</c>.
    /// </summary>
    public partial class PersonCardView
    {
        /// <summary>The rename field is open on the card.</summary>
        public bool RenameOpen { get; private set; }
        string renameText = "";
        TextField renameField;
        Label renameMsg;
        VisualElement renameActs;
        VisualElement renameRow;
        float renamed;
        /// <summary>Room kept clear under the safe area's top (the HUD's strip), panel px: 44 pt.</summary>
        public const float TopClear = 121f;
        /// <summary>The card never gets shorter than this while renaming: head, field and the buttons (panel px).</summary>
        const float RenameMinHeight = 560f;

        /// <summary>The field (tests).</summary>
        public TextField RenameField => renameField;
        /// <summary>The refusal showing under the field, "" when none (tests).</summary>
        public string RenameProblem => renameMsg?.text ?? "";

        void RenameReset()
        {
            RenameOpen = false;
            renameText = "";
            GlassKeyboard.Down(renameField); // #645: before the redraw drops it, or the phone's keyboard stays up
            renameField = null;
            renameMsg = null;
            renameActs = null;
            renameRow = null;
            if (sheet != null && renamed > 0f) LandKeyboard();
            sheet?.RemoveFromClassList("pc--renaming");
        }

        /// <summary>On the About tab of a living person, under the chips (#674): the pencil, or the open field.</summary>
        void BuildRenameRow(PersonCard c)
        {
            var wrap = Section("person-card-rename-row");
            wrap.style.flexShrink = 0;
            if (RenameOpen) renameRow = wrap;
            if (!RenameOpen)
            {
                var open = new GlassButton("Call them something else", () => OpenRename(c), ButtonKind.Secondary, UiRole.Open) { name = "person-card-rename", Compact = true };
                open.Marked("pencil", true);
                open.style.alignSelf = Align.FlexStart;
                wrap.Add(open);
                return;
            }
            renameField = GlassKeyboard.Tame(new TextField { name = "person-card-rename-field", maxLength = SimCore.RenameMaxLength + 4, value = renameText });
            renameField.AddToClassList("kg-field");
            renameField.RegisterValueChangedCallback(e => { renameText = e.newValue; if (renameMsg != null) renameMsg.text = ""; });
            wrap.Add(renameField);
            renameMsg = new Label("") { name = "person-card-rename-msg", pickingMode = PickingMode.Ignore };
            renameMsg.AddToClassList("type-note");
            renameMsg.AddToClassList("pc-rename-msg");
            wrap.Add(renameMsg);
            var acts = new VisualElement();
            acts.AddToClassList("kg-card-acts");
            acts.AddToClassList("pc-rename-acts");
            acts.Add(new GlassButton("Never mind", CancelRename, ButtonKind.Ghost, UiRole.Close) { name = "person-card-rename-cancel" });
            acts.Add(new GlassButton("Save", () => DoKeep(), ButtonKind.Primary, UiRole.None) { name = "person-card-rename-save" });
            wrap.Add(acts);
            renameActs = acts;
        }

        void OpenRename(PersonCard c)
        {
            RenameOpen = true;
            renameText = c.name;
            Refresh(true);
            renameField?.Focus();
            ShowRenameActs();
        }

        /// <summary>The card is a short scroll: bring the field, Never mind and Save fully into view (the pass found them half cut off, and a tap
        /// on the cut half missed). The field first, then the buttons, so a short card ends on the buttons with the field still showing.</summary>
        void ShowRenameActs(long delayMs = 30)
        {
            var acts = renameActs;
            if (acts == null || body == null) return;
            // the field open: the subtitle and the tabs fold away with or without the keyboard (fb7 pass: without it the short card
            // scrolled to the buttons with the field itself off the top)
            sheet?.EnableInClassList("pc--renaming", true);
            body.schedule.Execute(() =>
            {
                if (renameActs != acts || body == null) return;
                if (renameRow != null) body.ScrollTo(renameRow);
                body.ScrollTo(acts);
            }).ExecuteLater(delayMs);
        }

        void CancelRename()
        {
            RenameReset();
            if (Card != null) Refresh(true);
        }

        /// <summary>Keep: rename through the sim, save, redraw. Returns the sim's answer (tests).</summary>
        public SimCore.RenameResult DoKeep()
        {
            var sim = Sim;
            if (sim == null || !IsOpen) return SimCore.RenameResult.NoSuchPerson;
            var r = sim.RenameNotable(OpenId, renameText, out string clean);
            if (r == SimCore.RenameResult.Renamed || r == SimCore.RenameResult.Kept)
            {
                RenameReset();
                if (r == SimCore.RenameResult.Renamed)
                {
                    if (GameClock.Instance != null) GameClock.Instance.Save();
                    UiSound.Play(UiRole.Confirm);
                    GameHud.Instance?.Toast($"{clean} it is.", 2.5f);
                }
                Refresh(true);
            }
            else if (renameMsg != null)
            {
                renameMsg.text = SimCore.RenameMessage(r);
                UiSound.Play(UiRole.Refused);
                ShowRenameActs();
            }
            return r;
        }

        /// <summary>The card's top edge now (panel px, y down; tests).</summary>
        public float SheetTop => sheet == null ? 0f : sheet.worldBound.yMin;
        /// <summary>The lowest the card's top may go while renaming: under the safe area and the HUD's strip (panel px; tests).</summary>
        public float RenameTopLimit => (sheet != null ? sheet.Insets.y : 0f) + TopClear;

        /// <summary>
        /// Each frame: while the field is open and the phone keyboard is up, the card sits on the keyboard and is made to fit the room
        /// above it (#584): translated up by the keyboard's height as the naming sheets are, but no taller than the space under the top
        /// safe area, with the tabs folded away so the field and its buttons are what is shown.
        /// </summary>
        void RideKeyboard()
        {
            if (sheet == null) return;
            float h = RenameOpen ? GlassKeyboard.Height() : 0f;
            if (Mathf.Abs(h - renamed) < 0.5f) return;
            bool wasDown = renamed <= 0f;
            renamed = h;
            if (h <= 0f) { LandKeyboard(); if (RenameOpen) ShowRenameActs(); return; }
            var i = GameUiPanel.SafeInsets(Screen.safeArea, Screen.width, Screen.height);
            float panelH = cardRoot != null && cardRoot.layout.height > 0f ? cardRoot.layout.height : GameUiPanel.ReferenceWidth * Screen.height / Mathf.Max(1f, Screen.width);
            float room = panelH - h - (i.y + TopClear);
            float fit = Mathf.Min(room, Mathf.Max(panelH * CardMaxShare, RenameMinHeight));
            sheet.EnableInClassList(GlassKeyboard.RidingClass, true);
            sheet.EnableInClassList("pc--renaming", true);
            sheet.SetInsets(new Vector4(i.x, i.y, i.z, 0f), GlassKeyboard.Pad);
            sheet.style.height = Mathf.Max(fit, 200f);
            sheet.style.translate = new Translate(0f, -h);
            sheet.Surface.Follow(GlassMotion.SlideMs);
            ShowRenameActs(wasDown ? 120 : 30); // the layout changes under it: scroll once it has settled
        }

        /// <summary>Off the keyboard at once (closing, or the keyboard went down): the card's own height and safe-area insets again.</summary>
        void LandKeyboard()
        {
            renamed = 0f;
            sheet.style.translate = StyleKeyword.Null;
            sheet.style.height = StyleKeyword.Null;
            sheet.RemoveFromClassList(GlassKeyboard.RidingClass);
            sheet.RemoveFromClassList("pc--renaming");
            ApplySafeArea();
        }

        /// <summary>Tests: open the field with a text and press Keep.</summary>
        public SimCore.RenameResult DebugRename(string text)
        {
            if (!IsOpen || Card == null) return SimCore.RenameResult.NoSuchPerson;
            if (!RenameOpen) OpenRename(Card);
            renameText = text;
            if (renameField != null) renameField.SetValueWithoutNotify(text);
            return DoKeep();
        }

        /// <summary>Debug panel (#575): open the card of the pinned person (or the first living notable) with the rename field open.</summary>
        public static string DebugRenameField()
        {
            var v = Instance;
            var sim = Sim;
            if (sim == null) return "no person card";
            var n = sim.Pinned ?? sim.State.notables.Find(x => x.alive);
            if (n == null || !v.Open(n.id, true)) return "no living notable";
            v.Tab = CardTab.About;
            if (!v.RenameOpen) v.OpenRename(v.Card);
            return "rename field open for " + n.FullName;
        }
    }
}
