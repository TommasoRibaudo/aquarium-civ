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
        string nick = "";
        int q1, q2, q4;
        string q3 = "", q5 = "";
        Vector2 scroll;

        void Start()
        {
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
                    var r = new Rect(w - 230, h - 100, 210, 76);
                    ProtoGui.Block(r);
                    if (GUI.Button(r, "Done for now")) { q1 = q2 = q4 = 0; q3 = q5 = ""; mode = Mode.Survey; }
                    break;
                }
                case Mode.Nickname:
                {
                    var r = Modal(w, h, 340);
                    GUILayout.BeginArea(r);
                    GUILayout.Label("Welcome, Keeper! Pick a nickname so we can tell your test sessions apart (no real name needed).", ProtoGui.Wrap);
                    nick = GUILayout.TextField(nick, 20, GUILayout.Height(56));
                    GUILayout.Space(10);
                    GUI.enabled = nick.Trim().Length > 0;
                    if (GUILayout.Button("Start", GUILayout.Height(64))) { Tester.Nickname = nick.Trim(); mode = Mode.Playing; }
                    GUI.enabled = true;
                    GUILayout.EndArea();
                    break;
                }
                case Mode.Survey:
                {
                    var r = Modal(w, h, h - 160);
                    GUILayout.BeginArea(r);
                    scroll = GUILayout.BeginScrollView(scroll);
                    GUILayout.Label("<b>Quick survey (5 questions)</b>");
                    q1 = Likert("1. That session felt...", "a chore", "relaxing / fun", q1);
                    q2 = Likert("2. How much do you want to check on it again later?", "not at all", "a lot", q2);
                    GUILayout.Label("3. Tell me one thing that happened or that you noticed in your tank.", ProtoGui.Wrap);
                    q3 = GUILayout.TextArea(q3, 300, GUILayout.Height(90));
                    q4 = Likert($"4. {PrototypeConfig.Current.surveyQ4}", "disagree", "agree", q4);
                    GUILayout.Label("5. Did anything feel like work, confusing, or guilt-inducing?", ProtoGui.Wrap);
                    q5 = GUILayout.TextArea(q5, 300, GUILayout.Height(90));
                    GUILayout.Space(10);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Back to the tank", GUILayout.Height(64))) mode = Mode.Playing;
                    GUI.enabled = q1 > 0 && q2 > 0 && q4 > 0;
                    if (GUILayout.Button("Send", GUILayout.Height(64))) Submit();
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    GUILayout.EndScrollView();
                    GUILayout.EndArea();
                    break;
                }
                case Mode.Thanks:
                {
                    var r = Modal(w, h, 420);
                    GUILayout.BeginArea(r);
                    GUILayout.Label("Thanks! Tap \"Copy summary\" and paste it into our chat.", ProtoGui.Wrap);
                    if (GUILayout.Button("Copy summary", GUILayout.Height(64))) GUIUtility.systemCopyBuffer = EventLog.Instance.LastSummary;
                    if (PrototypeConfig.Current.testMode == TestMode.Lab && GUILayout.Button("Next version (lab test)", GUILayout.Height(64)))
                    {
                        PrototypeConfig.Current.NextVariant();
                        Resume();
                    }
                    if (GUILayout.Button("Keep watching", GUILayout.Height(64))) Resume();
                    GUILayout.EndArea();
                    break;
                }
            }
        }

        void Submit()
        {
            EventLog.Instance.Survey(new List<(string, string)>
            {
                ("q1_feel", q1.ToString()), ("q2_return", q2.ToString()), ("q3_recall", q3),
                ("q4", q4.ToString()), ("q5_work", q5)
            });
            EventLog.Instance.EndSession("done");
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
            GUI.Box(outer, GUIContent.none);
            GUI.Box(outer, GUIContent.none); // double box = more opaque
            return new Rect(outer.x + 16, outer.y + 16, outer.width - 32, outer.height - 32);
        }

        static int Likert(string question, string low, string high, int value)
        {
            GUILayout.Label(question, ProtoGui.Wrap);
            GUILayout.BeginHorizontal();
            GUILayout.Label(low, GUILayout.Width(130));
            for (int i = 1; i <= 5; i++)
            {
                var label = value == i ? $"[{i}]" : i.ToString();
                if (GUILayout.Button(label, GUILayout.Height(60), GUILayout.MinWidth(60))) value = i;
            }
            GUILayout.Label(high, GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            return value;
        }
    }
}
