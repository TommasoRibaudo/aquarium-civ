using System;
using AquariumCiv.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.GameUI
{
    /// <summary>The phone's keyboard, in the units the sheets ride it in (spec §2).</summary>
    public static class GlassKeyboard
    {
        /// <summary>A raised keyboard for the debug panel, which an Editor never raises: the board's 291 pt.</summary>
        public static bool Debug { get; set; }
        /// <summary>Tests: a keyboard of exactly this height (panel px), whatever the device says; null when not faked.</summary>
        public static float? Fake { get; set; }
        const float DebugHeight = 291f * GlassType.Pt;
        /// <summary>Under the buttons while a sheet rides it: 12 pt.</summary>
        public const float Pad = 33f;
        /// <summary>A sheet on the keyboard floats, so every corner rounds.</summary>
        public const string RidingClass = "kg-sheet--keyboard";

        /// <summary>Its height in panel px, 0 when it's down.</summary>
        public static float Height()
        {
            if (Fake.HasValue) return Fake.Value;
            if (Debug) return DebugHeight;
            if (!TouchScreenKeyboard.isSupported || !TouchScreenKeyboard.visible) return 0f;
            return TouchScreenKeyboard.area.height * GameUiPanel.ReferenceWidth / Mathf.Max(1f, Screen.width);
        }
    }

    /// <summary>
    /// The naming sheet (board 3.7), and again as Rename (board 3.8): a note, the field, Another and Keep, with the one validation in
    /// <see cref="SettlementNaming"/>. What's refused stays refused until it's edited (question 28). It rides the keyboard by translate,
    /// keeping its layout, with Keep in reach. Names: <c>&lt;id&gt;-sheet</c>, <c>-scrim</c>, <c>-field</c>, <c>-msg</c>, <c>-reroll</c>, <c>-keep</c>, <c>-close</c>.
    /// </summary>
    public class GlassNameSheet
    {
        const string Note = "It goes on the brass plate.";
        readonly VisualElement note, alert;
        readonly Label problem;
        float ridden;

        public GlassSheet Sheet { get; }
        public GlassScrim Scrim { get; }
        public TextField Field { get; }
        public GlassButton Keep { get; }
        public bool IsOpen => Sheet.IsOpen;
        /// <summary>The refusal showing, "" when there's none.</summary>
        public string Problem => problem.text;

        /// <param name="closeWord">The quiet word in the head, or null for the icon Close.</param>
        public GlassNameSheet(string id, string title, string closeWord, Action close, Action another, Action keep)
        {
            // #637 tester (1640x2360): at 30 % of a tablet's shorter panel, Another and Keep rode up over the field; as tall as it needs instead
            Sheet = new GlassSheet(SheetHeight.Content);
            Scrim = new GlassScrim(id, Sheet);
            Scrim.TappedOutside += close;
            var head = GlassSheet.Head(title, closeWord == null ? id + "-close" : null, closeWord == null ? close : null);
            if (closeWord != null) head.Add(new GlassButton(closeWord, close, ButtonKind.Quiet, UiRole.Close) { name = id + "-close" });
            Sheet.Add(head);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("kg-name-note");
            alert = new GlassIcon("alert", "kg-icon--small", "kg-name-alert");
            alert.style.display = DisplayStyle.None;
            row.Add(alert);
            var line = new Label(Note) { pickingMode = PickingMode.Ignore };
            line.AddToClassList("type-note");
            note = line;
            row.Add(note);
            problem = new Label("") { name = id + "-msg", pickingMode = PickingMode.Ignore };
            problem.AddToClassList("type-note");
            problem.style.display = DisplayStyle.None;
            row.Add(problem);
            Sheet.Add(row);

            Field = new TextField { name = id + "-field", maxLength = SettlementNaming.MaxLength + 4 };
            Field.AddToClassList("kg-field");
            Field.RegisterValueChangedCallback(_ => Say(""));
            Sheet.Add(Field);

            var acts = new VisualElement();
            acts.AddToClassList("kg-card-acts");
            acts.AddToClassList("kg-name-acts");
            acts.Add(new GlassButton("Another", another) { name = id + "-reroll" });
            acts.Add(Keep = new GlassButton("Keep", keep, ButtonKind.Primary, UiRole.None) { name = id + "-keep" });
            Sheet.Add(acts);
        }

        /// <summary>The field changed, so what was refused is gone: the note is back and Keep is available.</summary>
        public void Say(string words)
        {
            bool bad = !string.IsNullOrEmpty(words);
            problem.text = words;
            problem.style.display = bad ? DisplayStyle.Flex : DisplayStyle.None;
            alert.style.display = bad ? DisplayStyle.Flex : DisplayStyle.None;
            note.style.display = bad ? DisplayStyle.None : DisplayStyle.Flex;
            Keep.Available = !bad;
        }

        public void Refuse(NameProblem p)
        {
            Say(SettlementNaming.Message(p));
            UiSound.Play(UiRole.Refused);
        }

        /// <summary>Each frame: follows the keyboard up and down. <paramref name="i"/> and <paramref name="pad"/> are the sheet's own, for when it's down.</summary>
        public void Ride(Vector4 i, float pad)
        {
            float h = IsOpen ? GlassKeyboard.Height() : 0f;
            if (Mathf.Abs(h - ridden) < 0.5f) return;
            ridden = h;
            Sheet.EnableInClassList(GlassKeyboard.RidingClass, h > 0f);
            Sheet.SetInsets(h > 0f ? new Vector4(i.x, i.y, i.z, 0f) : i, h > 0f ? GlassKeyboard.Pad : pad);
            Sheet.style.translate = h > 0f ? new Translate(0f, -h) : StyleKeyword.Null;
            Sheet.Surface.Follow(GlassMotion.SlideMs);
        }

        /// <summary>Closing: off the keyboard at once, or it would slide away from up there.</summary>
        public void Land(Vector4 i, float pad)
        {
            Sheet.style.translate = StyleKeyword.Null;
            Sheet.RemoveFromClassList(GlassKeyboard.RidingClass);
            ridden = 0f;
            Sheet.SetInsets(i, pad);
        }

        /// <summary>Riding the keyboard now: its insets aren't the safe area's, so leave them.</summary>
        public bool Riding => ridden > 0f;
    }
}
