using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.View
{
    /// <summary>
    /// #575 (playtest note #548): the Keeper can rename a notable from their card. A small "Call them something else" pencil button at the
    /// top of the About tab opens a one-line field with Keep; Keep tidies the text, empty keeps the old name, a refusal is said under the
    /// field. The new first name shows everywhere from then on (the card, the paper tag, History lines written later); chronicle lines
    /// already written keep the old name (they are plain text). While the field is open the card rides the phone keyboard up. Two living
    /// people may not share a first name (the same rule as at birth): that is refused with a kind word. Names: <c>person-card-rename</c>
    /// (the button), <c>person-card-rename-field</c>, <c>-keep</c>, <c>-cancel</c>, <c>-msg</c>.
    /// </summary>
    public partial class PersonCardView
    {
        /// <summary>The rename field is open on the card.</summary>
        public bool RenameOpen { get; private set; }
        string renameText = "";
        TextField renameField;
        Label renameMsg;
        float renamed;

        /// <summary>The field (tests).</summary>
        public TextField RenameField => renameField;
        /// <summary>The refusal showing under the field, "" when none (tests).</summary>
        public string RenameProblem => renameMsg?.text ?? "";

        void RenameReset()
        {
            RenameOpen = false;
            renameText = "";
            renameField = null;
            renameMsg = null;
            if (sheet != null && renamed > 0f) { sheet.style.translate = StyleKeyword.Null; renamed = 0f; }
        }

        /// <summary>The top of the About tab for a living person: the pencil, or the open field.</summary>
        void BuildRenameRow(PersonCard c)
        {
            var wrap = Section("person-card-rename-row");
            wrap.style.flexShrink = 0;
            if (!RenameOpen)
            {
                var open = new GlassButton("Call them something else", () => OpenRename(c), ButtonKind.Secondary, UiRole.Open) { name = "person-card-rename", Compact = true, Icon = "pencil" };
                open.style.alignSelf = Align.FlexStart;
                wrap.Add(open);
                return;
            }
            renameField = new TextField { name = "person-card-rename-field", maxLength = SimCore.RenameMaxLength + 4, value = renameText };
            renameField.AddToClassList("kg-field");
            renameField.RegisterValueChangedCallback(e => { renameText = e.newValue; if (renameMsg != null) renameMsg.text = ""; });
            wrap.Add(renameField);
            renameMsg = new Label("") { name = "person-card-rename-msg", pickingMode = PickingMode.Ignore };
            renameMsg.AddToClassList("type-note");
            wrap.Add(renameMsg);
            var acts = new VisualElement();
            acts.AddToClassList("kg-card-acts");
            acts.Add(new GlassButton("Never mind", CancelRename, ButtonKind.Quiet, UiRole.Close) { name = "person-card-rename-cancel" });
            acts.Add(new GlassButton("Keep", () => DoKeep(), ButtonKind.Primary, UiRole.None) { name = "person-card-rename-keep" });
            wrap.Add(acts);
        }

        void OpenRename(PersonCard c)
        {
            RenameOpen = true;
            renameText = c.name;
            Refresh(true);
            renameField?.Focus();
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
            }
            return r;
        }

        /// <summary>Each frame: while the field is open the card follows the phone keyboard up (as the naming sheets do).</summary>
        void RideKeyboard()
        {
            if (sheet == null) return;
            float h = RenameOpen ? GlassKeyboard.Height() : 0f;
            if (Mathf.Abs(h - renamed) < 0.5f) return;
            renamed = h;
            sheet.style.translate = h > 0f ? new Translate(0f, -h) : StyleKeyword.Null;
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
