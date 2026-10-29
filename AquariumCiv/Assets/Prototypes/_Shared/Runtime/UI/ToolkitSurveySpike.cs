using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// UI-automation spike (vertical-slice-plan.md §5.3): a survey-like panel in UI Toolkit, built in code, that a test
    /// can tap through the real touch path (IMGUI panels can't be clicked from automation here: tooling.md 2026-10-29).
    /// Not used by any prototype. Every control has a stable name, so tests and the tester agent find it with
    /// <see cref="UiProbe"/> instead of by pixel.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ToolkitSurveySpike : MonoBehaviour
    {
        public int Rating { get; private set; }
        public int Sends { get; private set; }
        public event Action<int> Sent;

        public VisualElement Root => GetComponent<UIDocument>().rootVisualElement;

        /// <summary>Create the panel on a new GameObject (PanelSettings and theme from Resources/UiToolkit).</summary>
        public static ToolkitSurveySpike Create()
        {
            var go = new GameObject("ToolkitSurveySpike");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UiToolkit/SpikePanel");
            return go.AddComponent<ToolkitSurveySpike>();
        }

        void OnEnable()
        {
            var root = Root;
            root.Clear();
            var card = new VisualElement { name = "survey" };
            card.style.position = Position.Absolute;
            card.style.left = 40; card.style.right = 40; card.style.bottom = 160;
            card.style.paddingLeft = card.style.paddingRight = card.style.paddingTop = card.style.paddingBottom = 24;
            card.style.backgroundColor = new Color(0.1f, 0.12f, 0.14f, 0.92f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 24;

            var q = new Label("That session felt... (1 chore - 5 fun)") { name = "q1-label" };
            q.style.fontSize = 40; q.style.color = Color.white; q.style.whiteSpace = WhiteSpace.Normal;
            card.Add(q);

            var row = new VisualElement { name = "q1-row" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 16;
            for (int i = 1; i <= 5; i++)
            {
                int value = i;
                var b = new Button(() => Rate(value)) { name = $"q1-{i}", text = i.ToString() };
                b.style.flexGrow = 1; b.style.height = 96; b.style.fontSize = 40; // 96 px ≈ 48 pt at 1080 wide
                row.Add(b);
            }
            card.Add(row);

            var send = new Button(Send) { name = "send", text = "Send" };
            send.style.height = 96; send.style.marginTop = 16; send.style.fontSize = 40;
            send.SetEnabled(false);
            card.Add(send);
            root.Add(card);
        }

        void Rate(int value)
        {
            Rating = value;
            for (int i = 1; i <= 5; i++) Root.Q<Button>($"q1-{i}").EnableInClassList("selected", i == value);
            Root.Q<Button>("send").SetEnabled(true);
        }

        void Send()
        {
            Sends++;
            Sent?.Invoke(Rating);
        }
    }

    /// <summary>Find UI Toolkit controls by name and turn them into screen points for touch input (y up, pixels).</summary>
    public static class UiProbe
    {
        public static Vector2 ScreenCenter(VisualElement e)
        {
            var c = e.worldBound.center;            // panel space: y down, panel pixels
            float scale = Screen.width / e.panel.visualTree.worldBound.width;
            return new Vector2(c.x * scale, Screen.height - c.y * scale);
        }

        public static Vector2 ScreenCenter(VisualElement root, string name) =>
            ScreenCenter(root.Q(name) ?? throw new ArgumentException($"No element named '{name}'"));
    }
}
