using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// "Feedback" button (bottom-left, thumb zone) → pick a kind, write a line or two, send. Goes to GitHub via
    /// FeedbackSender with the session context attached, so testers never need to copy-paste anything.
    /// </summary>
    public class FeedbackPanel : MonoBehaviour
    {
        static readonly FeedbackSender.Kind[] Kinds =
        {
            FeedbackSender.Kind.Bug, FeedbackSender.Kind.Feel, FeedbackSender.Kind.Confusing,
            FeedbackSender.Kind.Idea, FeedbackSender.Kind.Praise,
        };

        static FeedbackPanel instance;
        /// <summary>The feedback form is up (banners hide behind it).</summary>
        public static bool IsOpen => instance != null && instance.open;
        bool open;
        int kind = -1;
        string text = "";

        void Start()
        {
            instance = this;
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result += (_, msg) => HudToast.ShowBanner(msg, 3f);
        }

        void OnGUI()
        {
            ProtoGui.Begin();
            float w = ProtoGui.Width, h = ProtoGui.Height;
            if (!open)
            {
                if (!SurveyPanel.IsPlaying) return;
                var r = new Rect(20, h - 112 - ProtoGui.SafeBottom, 200, 88);
                ProtoGui.Block(r);
                if (GUI.Button(r, "Feedback")) { open = true; kind = -1; text = ""; }
                return;
            }

            // Sized to its content (title, 2 chip rows, text box, buttons) so nothing gets clipped.
            const float pad = 24, height = 580;
            var outer = new Rect(20, Mathf.Max(200, h * 0.2f), w - 40, height); // below the top banner, above the keyboard
            ProtoGui.Block(outer);
            ProtoGui.Panel(outer);
            float inner = outer.width - pad * 2;
            GUILayout.BeginArea(new Rect(outer.x + pad, outer.y + pad, inner, outer.height - pad * 2));
            GUILayout.Label("Tell us something", ProtoGui.Title);
            GUILayout.Label("What you saw, felt or expected. Pick one:", ProtoGui.Hint);
            GUILayout.Space(10);
            float chipW = (inner - 30) / 3; // 3 per row, margins 5 each side
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int i = row * 3; i < Mathf.Min(Kinds.Length, row * 3 + 3); i++)
                    if (ProtoGui.ChipButton(FeedbackSender.KindLabel(Kinds[i]), kind == i, GUILayout.Width(chipW), GUILayout.Height(60)))
                        kind = i;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(12);
            text = GUILayout.TextArea(text, 600, GUILayout.Height(210));
            if (text.Length == 0)
            {
                var ta = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(ta.x + 18, ta.y + 12, ta.width - 36, 60), "Type here...", ProtoGui.Hint);
            }
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(72))) open = false;
            if (ProtoGui.PrimaryButton("Send", kind >= 0 && text.Trim().Length > 0, GUILayout.Height(72)))
            {
                FeedbackSender.Instance?.SendFeedback(Kinds[kind], text);
                open = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
