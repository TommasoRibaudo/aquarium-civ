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

        bool open;
        int kind = -1;
        string text = "";

        void Start()
        {
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result += (_, msg) => HudToast.ShowBanner(msg, 3f);
        }

        void OnGUI()
        {
            ProtoGui.Begin();
            float w = ProtoGui.Width, h = ProtoGui.Height;
            if (!open)
            {
                if (!SurveyPanel.IsPlaying) return;
                var r = new Rect(20, h - 100, 190, 76);
                ProtoGui.Block(r);
                if (GUI.Button(r, "Feedback")) { open = true; kind = -1; text = ""; }
                return;
            }

            var outer = new Rect(20, h * 0.18f, w - 40, 470);
            ProtoGui.Block(outer);
            GUI.Box(outer, GUIContent.none);
            GUI.Box(outer, GUIContent.none);
            GUILayout.BeginArea(new Rect(outer.x + 16, outer.y + 16, outer.width - 32, outer.height - 32));
            GUILayout.Label("<b>Tell us something</b>  (what you saw, felt or expected)", ProtoGui.Wrap);
            GUILayout.Space(6);
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int i = row * 3; i < Mathf.Min(Kinds.Length, row * 3 + 3); i++)
                {
                    var label = FeedbackSender.KindLabel(Kinds[i]);
                    if (GUILayout.Button(kind == i ? $"[{label}]" : label, GUILayout.Height(60))) kind = i;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            text = GUILayout.TextArea(text, 600, GUILayout.Height(220));
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(64))) open = false;
            GUI.enabled = kind >= 0 && text.Trim().Length > 0;
            if (GUILayout.Button("Send", GUILayout.Height(64)))
            {
                FeedbackSender.Instance?.SendFeedback(Kinds[kind], text);
                open = false;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
