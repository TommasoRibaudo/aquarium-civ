using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Tester-facing flow: nickname on first launch, a "Done for now" button, the 5-question survey
    /// (prototype-plan.md §4), then a thank-you screen with "copy summary".
    /// </summary>
    public class SurveyPanel : MonoBehaviour
    {
        enum Mode { Playing, Nickname, Survey, Thanks }

        Mode mode;
        static SurveyPanel instance;
        /// <summary>No modal of ours is up (the Feedback button only shows then).</summary>
        public static bool IsPlaying => instance == null || instance.mode == Mode.Playing;
        string sendResult = "";
        string nick = "";
        int q1, q2, q4;
        string q3 = "", q5 = "";
        Vector2 scroll;

        void Start()
        {
            instance = this;
            if (FeedbackSender.Instance != null) FeedbackSender.Instance.Result += (_, msg) => sendResult = msg;
            mode = string.IsNullOrEmpty(Tester.Nickname) ? Mode.Nickname : Mode.Playing;
        }

        void OnGUI()
        {
            ProtoGui.Begin();
            float w = ProtoGui.Width, h = ProtoGui.Height;
            switch (mode)
            {
                case Mode.Playing:
                {
                    var r = new Rect(w - 240, h - 112, 220, 88);
                    ProtoGui.Block(r);
                    if (GUI.Button(r, "Done for now")) DoneForNow();
                    break;
                }
                case Mode.Nickname:
                {
                    var r = Modal(w, h, 380);
                    GUILayout.BeginArea(r);
                    GUILayout.Label("Welcome, Keeper!", ProtoGui.Title);
                    GUILayout.Label("Pick a nickname so we can tell your test sessions apart (no real name needed).", ProtoGui.Wrap);
                    nick = GUILayout.TextField(nick, 20, GUILayout.Height(56));
                    GUILayout.Space(10);
                    if (ProtoGui.PrimaryButton("Start", nick.Trim().Length > 0, GUILayout.Height(72))) { Tester.Nickname = nick.Trim(); mode = Mode.Playing; }
                    GUILayout.EndArea();
                    break;
                }
                case Mode.Survey:
                {
                    var r = Modal(w, h, Mathf.Min(h - 160, 1000));
                    GUILayout.BeginArea(r);
                    scroll = GUILayout.BeginScrollView(scroll);
                    GUILayout.Label("Quick survey", ProtoGui.Title);
                    GUILayout.Label("5 questions, under a minute.", ProtoGui.Hint);
                    GUILayout.Space(8);
                    q1 = Likert("1. That session felt...", "a chore", "relaxing / fun", q1);
                    q2 = Likert("2. How much do you want to check on it again later?", "not at all", "a lot", q2);
                    GUILayout.Label("3. Tell me one thing that happened or that you noticed in your tank.", ProtoGui.Wrap);
                    q3 = GUILayout.TextArea(q3, 300, GUILayout.Height(90));
                    GUILayout.Space(10);
                    q4 = Likert($"4. {PrototypeConfig.Current.surveyQ4}", "disagree", "agree", q4);
                    GUILayout.Label("5. Did anything feel like work, confusing, or guilt-inducing?", ProtoGui.Wrap);
                    q5 = GUILayout.TextArea(q5, 300, GUILayout.Height(90));
                    GUILayout.Space(10);
                    GUILayout.Space(10);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Back to the tank", GUILayout.Height(72))) mode = Mode.Playing;
                    if (ProtoGui.PrimaryButton("Send", q1 > 0 && q2 > 0 && q4 > 0, GUILayout.Height(72))) Submit();
                    GUILayout.EndHorizontal();
                    GUILayout.EndScrollView();
                    GUILayout.EndArea();
                    break;
                }
                case Mode.Thanks:
                {
                    var r = Modal(w, h, 420);
                    GUILayout.BeginArea(r);
                    GUILayout.Label(FeedbackSender.Instance != null ? sendResult : "Thanks! Tap \"Copy summary\" and paste it into our chat.", ProtoGui.Wrap);
                    if (GUILayout.Button("Copy summary", GUILayout.Height(64))) GUIUtility.systemCopyBuffer = EventLog.Instance.LastSummary;
                    if (PrototypeConfig.Current.testMode == TestMode.Lab && GUILayout.Button("Next version (lab test)", GUILayout.Height(64)))
                    {
                        PrototypeConfig.Current.NextVariant();
                        Resume();
                    }
                    if (GUILayout.Button("Keep watching", ProtoGui.Primary, GUILayout.Height(72))) Resume();
                    GUILayout.EndArea();
                    break;
                }
            }
        }

        // Diary tests ask the survey at most once per calendar day (prototype-plan.md §4).
        static string SurveyDayKey => $"survey_day_{PrototypeConfig.Current.protoId}";
        static string Today => System.DateTime.Now.ToString("yyyy-MM-dd");

        void DoneForNow()
        {
            if (PrototypeConfig.Current.testMode == TestMode.Diary && PlayerPrefs.GetString(SurveyDayKey, "") == Today)
            {
                EventLog.Instance.EndSession("done");
                sendResult = "Thanks, Keeper! Today's survey is already in. See you next time.";
                mode = Mode.Thanks;
                return;
            }
            q1 = q2 = q4 = 0; q3 = q5 = "";
            mode = Mode.Survey;
        }

        void Submit()
        {
            PlayerPrefs.SetString(SurveyDayKey, Today);
            PlayerPrefs.Save();
            EventLog.Instance.Survey(new List<(string, string)>
            {
                ("q1_feel", q1.ToString()), ("q2_return", q2.ToString()), ("q3_recall", q3),
                ("q4", q4.ToString()), ("q5_work", q5)
            });
            EventLog.Instance.EndSession("done");
            sendResult = "Sending your answers...";
            FeedbackSender.Instance?.SendSurvey(new List<(string, string, string)>
            {
                ("q1_feel", "That session felt... (1 chore - 5 relaxing/fun)", q1.ToString()),
                ("q2_return", "Want to check on it again later? (1-5)", q2.ToString()),
                ("q3_recall", "One thing that happened or you noticed", q3),
                ("q4", $"{PrototypeConfig.Current.surveyQ4} (1-5)", q4.ToString()),
                ("q5_work", "Anything feel like work, confusing, or guilt-inducing?", q5),
            }, EventLog.Instance.LastSummary);
            mode = Mode.Thanks;
        }

        void Resume()
        {
            var sim = SimClock.Instance.Sim;
            EventLog.Instance.StartSession(0, 0, sim.MessLevel);
            mode = Mode.Playing;
        }

        static Rect Modal(float w, float h, float height)
        {
            var outer = new Rect(20, (h - height) / 2, w - 40, height);
            ProtoGui.Block(outer);
            ProtoGui.Panel(outer);
            return new Rect(outer.x + 24, outer.y + 24, outer.width - 48, outer.height - 48);
        }

        static int Likert(string question, string low, string high, int value)
        {
            GUILayout.Label(question, ProtoGui.Wrap);
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 5; i++)
                if (ProtoGui.ChipButton(i.ToString(), value == i, GUILayout.Height(64), GUILayout.MinWidth(60))) value = i;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(low, ProtoGui.Hint);
            GUILayout.FlexibleSpace();
            GUILayout.Label(high, ProtoGui.HintRight);
            GUILayout.EndHorizontal();
            GUILayout.Space(14);
            return value;
        }
    }
}
