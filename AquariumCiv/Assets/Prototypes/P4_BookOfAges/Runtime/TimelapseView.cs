using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.Prototypes.P4
{
    /// <summary>
    /// Variant A: a time-lapse scrubber. Drag along the timeline and the village in the glass rewinds and regrows
    /// (buildings pop in, the crowd and the named people of that year appear). Pins on the timeline mark the big
    /// stories; scrubbing near one shows it. Play replays the century in about 15 seconds.
    /// </summary>
    public class TimelapseView : MonoBehaviour, IHistoryView
    {
        public DioramaView village;
        public CrowdView crowd;
        public NotableView notables;
        public int pinCount = 18;
        public float panelHeight = 330f;
        [Tooltip("A replay takes about this many seconds, whatever the age of the world.")] public float replaySeconds = 15f;

        public bool IsOpen { get; private set; }
        public event System.Action<string> Snapshot;

        float viewYear, lastYear, scrubYears, openedAt;
        bool playing, dragging;
        int pinTaps;
        List<ChronicleEntry> pins = new();
        ChronicleEntry shown;
        Rect barCanvas;
        GUIStyle yearStyle, pinText, pinMeta;

        SimState S => SimClock.Instance.Sim.State;
        int MaxYear => Mathf.Max(1, (int)(S.month / SimConfig.MonthsPerYear));

        public void Open()
        {
            IsOpen = true;
            openedAt = Time.unscaledTime;
            scrubYears = 0; pinTaps = 0;
            viewYear = lastYear = MaxYear;
            playing = false;
            pins = S.chronicle.Where(e => e.kind != "coming_of_age" && e.kind != "grumble")
                .OrderByDescending(e => e.salience).ThenBy(e => e.month).Take(pinCount).OrderBy(e => e.month).ToList();
            shown = null;
            OrbitCamera.Instance?.ResetView();
            Apply();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            playing = dragging = false;
            village.showUpToMonth = -1;
            crowd.populationOverride = -1;
            notables.viewMonth = -1;
            EventLog.Instance?.Log("history", ("action", "close"), ("view", "timelapse"), ("dwell_s", Time.unscaledTime - openedAt),
                ("scrub_years", scrubYears), ("pin_taps", pinTaps));
            EventLog.Instance?.Count("history_s", Time.unscaledTime - openedAt);
            EventLog.Instance?.Count("scrub_years", scrubYears);
        }

        void Apply()
        {
            int y = Mathf.Clamp(Mathf.RoundToInt(viewYear), 0, MaxYear);
            scrubYears += Mathf.Abs(viewYear - lastYear);
            lastYear = viewYear;
            bool live = y >= MaxYear;
            long month = y * (long)SimConfig.MonthsPerYear + SimConfig.MonthsPerYear - 1; // the whole of that year
            village.showUpToMonth = live ? -1 : month;
            notables.viewMonth = live ? -1 : month;
            var snap = S.years.LastOrDefault(s => s.year <= y);
            crowd.populationOverride = live || snap == null ? -1 : snap.population;
            // The story nearest the handle (within two years) shows above the timeline.
            shown = pins.Where(e => Mathf.Abs(e.month / (float)SimConfig.MonthsPerYear - viewYear) <= 2f)
                .OrderBy(e => Mathf.Abs(e.month / (float)SimConfig.MonthsPerYear - viewYear)).FirstOrDefault();
        }

        void Update()
        {
            if (!IsOpen) return;
            if (playing)
            {
                viewYear += Time.unscaledDeltaTime * MaxYear / replaySeconds;
                if (viewYear >= MaxYear) { viewYear = MaxYear; playing = false; }
                Apply();
            }
            var p = Pointer.current;
            if (p == null) return;
            var pos = p.position.ReadValue();
            var canvas = new Vector2(pos.x / ProtoGui.Scale, (Screen.height - pos.y) / ProtoGui.Scale);
            var hit = new Rect(barCanvas.x - 20, barCanvas.y - 44, barCanvas.width + 40, barCanvas.height + 88); // finger-sized
            if (p.press.wasPressedThisFrame && hit.Contains(canvas)) { dragging = true; playing = false; }
            if (dragging && p.press.isPressed)
            {
                float t = Mathf.InverseLerp(barCanvas.x, barCanvas.xMax, canvas.x);
                viewYear = t * MaxYear;
                Apply();
            }
            if (dragging && p.press.wasReleasedThisFrame)
            {
                dragging = false;
                // Let go near a pin: snap to it, so a tap on a pin reads as "show me that".
                var near = pins.OrderBy(e => Mathf.Abs(PinX(e) - canvas.x)).FirstOrDefault();
                if (near != null && Mathf.Abs(PinX(near) - canvas.x) < 26f)
                {
                    viewYear = near.month / (float)SimConfig.MonthsPerYear;
                    pinTaps++;
                    EventLog.Instance?.Log("history", ("action", "pin"), ("card", near.id), ("kind", near.kind));
                    Apply();
                }
            }
        }

        float PinX(ChronicleEntry e) => Mathf.Lerp(barCanvas.x, barCanvas.xMax, e.month / (float)SimConfig.MonthsPerYear / MaxYear);

        void OnGUI()
        {
            if (!IsOpen || !SurveyPanel.IsPlaying) return;
            ProtoGui.Begin();
            yearStyle ??= new GUIStyle(ProtoGui.Title) { fontSize = 34 };
            pinText ??= new GUIStyle(ProtoGui.Wrap) { fontSize = 22 };
            pinMeta ??= new GUIStyle(ProtoGui.Hint) { fontSize = 19 };
            float w = ProtoGui.Width, h = ProtoGui.Height;
            var outer = new Rect(16, h - ProtoGui.SafeBottom - 112 - 12 - panelHeight, w - 32, panelHeight);
            ProtoGui.Block(outer);
            ProtoGui.Panel(outer);
            var inner = new Rect(outer.x + 22, outer.y + 16, outer.width - 44, outer.height - 32);

            int y = Mathf.Clamp(Mathf.RoundToInt(viewYear), 0, MaxYear);
            var snap = S.years.LastOrDefault(s => s.year <= y);
            var naming = S.chronicle.FirstOrDefault(e => e.kind == "naming");
            bool named = !string.IsNullOrEmpty(S.settlementName) && naming != null && naming.month / SimConfig.MonthsPerYear <= y;
            string place = named ? $"  ·  {S.settlementName}" : "";
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 44), y >= MaxYear ? $"Today, year {y}" : $"Year {y}", yearStyle);
            GUI.Label(new Rect(inner.x, inner.y + 44, inner.width, 28), $"{snap?.population ?? S.population} people  ·  {snap?.buildings ?? S.buildings.Count} buildings{place}", pinMeta);

            // Timeline with pins.
            barCanvas = new Rect(inner.x + 14, inner.y + 120, inner.width - 28, 10);
            var c0 = GUI.color;
            GUI.color = new Color(0.45f, 0.75f, 0.78f, 0.55f);
            GUI.DrawTexture(new Rect(barCanvas.x, barCanvas.y + 2, barCanvas.width, 6), Texture2D.whiteTexture);
            GUI.color = c0;
            foreach (var e in pins)
                ProtoGui.Dot(new Rect(PinX(e) - 7, barCanvas.y - 22, 14, 14), e == shown ? ProtoGui.AccentColor : new Color(0.6f, 0.85f, 0.85f, 0.85f));
            float hx = Mathf.Lerp(barCanvas.x, barCanvas.xMax, viewYear / MaxYear);
            ProtoGui.Dot(new Rect(hx - 18, barCanvas.y - 13, 36, 36), ProtoGui.VariantColor(0));
            GUI.Label(new Rect(barCanvas.x - 8, barCanvas.y + 24, 100, 24), "Year 0", pinMeta);
            GUI.Label(new Rect(barCanvas.xMax - 92, barCanvas.y + 24, 100, 24), "Today", pinMeta);

            // Controls.
            var row = new Rect(inner.x, inner.yMax - 88, inner.width, 88);
            GUILayout.BeginArea(row);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(playing ? "Pause" : viewYear >= MaxYear ? "Replay" : "Play", GUILayout.Height(88)))
            {
                if (!playing && viewYear >= MaxYear) viewYear = 0;
                playing = !playing;
                EventLog.Instance?.Log("history", ("action", playing ? "play" : "pause"));
            }
            if (GUILayout.Button("Snapshot", GUILayout.Height(88))) Snapshot?.Invoke("timelapse");
            if (GUILayout.Button("Close", ProtoGui.Primary, GUILayout.Height(88))) Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // The story at the handle, above the panel.
            if (shown == null) return;
            var who = SimClock.Instance.Sim.FindNotable(shown.notableId);
            float cardH = 132;
            var card = new Rect(16, outer.y - cardH - 10, w - 32, cardH);
            ProtoGui.Block(card);
            ProtoGui.Panel(card);
            float tx = card.x + 18;
            if (who != null) { PortraitStudio.Draw(new Rect(card.x + 14, card.y + 14, 104, 104), who, !who.alive); tx = card.x + 132; }
            GUI.Label(new Rect(tx, card.y + 12, card.xMax - tx - 14, 26), $"Year {shown.month / SimConfig.MonthsPerYear}", pinMeta);
            GUI.Label(new Rect(tx, card.y + 38, card.xMax - tx - 14, cardH - 46), shown.text, pinText);
        }
    }

    /// <summary>What the controller needs from either history view.</summary>
    public interface IHistoryView
    {
        bool IsOpen { get; }
        void Open();
        void Close();
        event System.Action<string> Snapshot;
    }
}
