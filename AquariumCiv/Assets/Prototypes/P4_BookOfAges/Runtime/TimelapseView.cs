using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.Prototypes.P4
{
    /// <summary>
    /// Variant A: a time-lapse scrubber. Drag along the timeline and the village in the glass rewinds and regrows
    /// (buildings pop in, the crowd and the named people of that year appear). Pins mark the years with big stories;
    /// near a pin, all of that year's stories show above the timeline. Play replays the century in about 15 seconds.
    /// </summary>
    public class TimelapseView : MonoBehaviour, IHistoryView
    {
        public DioramaView village;
        public CrowdView crowd;
        public NotableView notables;
        [Tooltip("The biggest stories become pins; stories in the same year share one pin.")] public int storyCount = 26;
        public int storiesPerPin = 3;
        public float panelHeight = 330f;
        [Tooltip("A replay takes about this many seconds, whatever the age of the world.")] public float replaySeconds = 15f;
        [Tooltip("While open, the world renders into the screen above this share (from the bottom), so the panels never cover the village.")]
        public float worldBottom = 0.42f;

        public bool IsOpen { get; private set; }
        public event System.Action<string> Snapshot;

        class Pin { public int year; public List<ChronicleEntry> stories; }

        float viewYear, scrubYears, openedAt;
        bool playing, dragging;
        int pinTaps;
        List<Pin> pins = new();
        Pin shown;
        Rect barCanvas;
        GUIStyle yearStyle, pinText, pinMeta;

        SimState S => SimClock.Instance.Sim.State;
        int MaxYear => Mathf.Max(1, (int)(S.month / SimConfig.MonthsPerYear));
        int ViewYear => Mathf.Clamp(Mathf.FloorToInt(viewYear + 0.001f), 0, MaxYear);

        public void Open()
        {
            IsOpen = true;
            openedAt = Time.unscaledTime;
            scrubYears = 0; pinTaps = 0;
            viewYear = MaxYear;
            playing = false;
            pins = S.chronicle.Where(e => e.kind != "coming_of_age" && e.kind != "grumble")
                .OrderByDescending(e => e.salience).ThenBy(e => e.month).Take(storyCount)
                .GroupBy(e => (int)(e.month / SimConfig.MonthsPerYear)).OrderBy(g => g.Key)
                .Select(g => new Pin { year = g.Key, stories = g.OrderByDescending(e => e.salience).Take(storiesPerPin).OrderBy(e => e.month).ToList() })
                .ToList();
            shown = null;
            var cam = OrbitCamera.Instance;
            if (cam != null)
            {
                cam.ResetView();
                // The world moves into the top of the screen, above the story card and the timeline.
                if (backdrop == null) backdrop = MakeBackdrop(cam.Cam);
                backdrop.enabled = true;
                cam.Cam.rect = new Rect(0, worldBottom, 1, 1 - worldBottom);
                cam.FocusOn(cam.BasePivot, 0.7f); // a little closer: the village fills the smaller view
            }
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
            var cam = OrbitCamera.Instance;
            if (cam != null) { cam.Cam.rect = new Rect(0, 0, 1, 1); cam.ResetView(); }
            if (backdrop != null) backdrop.enabled = false;
            EventLog.Instance?.Log("history", ("action", "close"), ("view", "timelapse"), ("dwell_s", Time.unscaledTime - openedAt),
                ("scrub_years", scrubYears), ("pin_taps", pinTaps));
            EventLog.Instance?.Count("history_s", Time.unscaledTime - openedAt);
            EventLog.Instance?.Count("scrub_years", scrubYears);
        }

        Camera backdrop;

        /// <summary>Clears the whole screen behind the shrunken world view (nothing else draws below it).</summary>
        static Camera MakeBackdrop(Camera main)
        {
            var go = new GameObject("TimelapseBackdrop");
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = main.backgroundColor;
            c.cullingMask = 0;
            c.depth = main.depth - 1;
            c.enabled = false;
            return c;
        }

        void OnDestroy()
        {
            if (backdrop != null) Destroy(backdrop.gameObject);
        }

        long ViewMonth => ViewYear * (long)SimConfig.MonthsPerYear + SimConfig.MonthsPerYear - 1; // the whole of that year

        void Apply()
        {
            int y = ViewYear;
            bool live = y >= MaxYear;
            village.showUpToMonth = live ? -1 : ViewMonth;
            notables.viewMonth = live ? -1 : ViewMonth;
            var snap = S.years.LastOrDefault(s => s.year <= y + 1); // the end of that year
            crowd.populationOverride = live || snap == null ? -1 : snap.population;
            // The pin at (or right next to) the handle shows its stories.
            shown = pins.Where(p => Mathf.Abs(p.year - viewYear) <= 1.2f).OrderBy(p => Mathf.Abs(p.year + 0.5f - viewYear)).FirstOrDefault();
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
                float to = t * MaxYear;
                scrubYears += Mathf.Abs(to - viewYear); // dragged distance only (Play is logged on its own)
                viewYear = to;
                Apply();
            }
            if (dragging && p.press.wasReleasedThisFrame)
            {
                dragging = false;
                // Let go near a pin: snap to it, so a tap on a pin reads as "show me that year".
                var near = pins.OrderBy(e => Mathf.Abs(PinX(e) - canvas.x)).FirstOrDefault();
                if (near != null && Mathf.Abs(PinX(near) - canvas.x) < 26f)
                {
                    viewYear = near.year + 0.5f;
                    pinTaps++;
                    EventLog.Instance?.Log("history", ("action", "pin"), ("year", near.year), ("stories", near.stories.Count), ("kind", near.stories[0].kind));
                    Apply();
                }
            }
        }

        float PinX(Pin p) => Mathf.Lerp(barCanvas.x, barCanvas.xMax, (p.year + 0.5f) / MaxYear);

        void OnGUI()
        {
            if (!IsOpen || !SurveyPanel.IsPlaying) return;
            GUI.depth = -5; // above the world markers
            ProtoGui.Begin();
            yearStyle ??= new GUIStyle(ProtoGui.Title) { fontSize = 34 };
            pinText ??= new GUIStyle(ProtoGui.Wrap) { fontSize = 21 };
            pinMeta ??= new GUIStyle(ProtoGui.Hint) { fontSize = 19 };
            float w = ProtoGui.Width, h = ProtoGui.Height;
            var outer = new Rect(16, h - ProtoGui.SafeBottom - 112 - 12 - panelHeight, w - 32, panelHeight);
            ProtoGui.Block(outer);
            ProtoGui.Panel(outer);
            var inner = new Rect(outer.x + 22, outer.y + 16, outer.width - 44, outer.height - 32);

            int y = ViewYear;
            int people = y >= MaxYear ? S.population : S.years.LastOrDefault(s => s.year <= y + 1)?.population ?? S.population;
            int buildings = S.buildings.Count(b => b.foundedMonth <= ViewMonth); // what the tank shows
            var naming = S.chronicle.FirstOrDefault(e => e.kind == "naming");
            bool named = !string.IsNullOrEmpty(S.settlementName) && naming != null && naming.month / SimConfig.MonthsPerYear <= y;
            string place = named ? $"  ·  {S.settlementName}" : "";
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 44), y >= MaxYear ? $"Today, year {y}" : $"Year {y}", yearStyle);
            GUI.Label(new Rect(inner.x, inner.y + 44, inner.width, 28), $"{people} people  ·  {buildings} buildings{place}", pinMeta);
            if (!dragging && scrubYears < 1f && !playing)
                GUI.Label(new Rect(inner.x + inner.width * 0.45f, inner.y + 10, inner.width * 0.55f, 28), "Drag the timeline to go back in time", new GUIStyle(pinMeta) { alignment = TextAnchor.UpperRight });

            // Timeline with pins.
            barCanvas = new Rect(inner.x + 14, inner.y + 120, inner.width - 28, 10);
            var c0 = GUI.color;
            GUI.color = new Color(0.45f, 0.75f, 0.78f, 0.55f);
            GUI.DrawTexture(new Rect(barCanvas.x, barCanvas.y + 2, barCanvas.width, 6), Texture2D.whiteTexture);
            GUI.color = c0;
            foreach (var p in pins)
            {
                float d = p.stories.Count > 1 ? 18 : 14;
                ProtoGui.Dot(new Rect(PinX(p) - d / 2, barCanvas.y - 18 - d / 2, d, d), p == shown ? ProtoGui.AccentColor : new Color(0.6f, 0.85f, 0.85f, 0.85f));
            }
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

            // That year's stories, above the panel.
            if (shown == null) return;
            var first = shown.stories[0];
            var who = SimClock.Instance.Sim.FindNotable(first.notableId);
            float tx = 16 + 18 + (who != null ? 114 : 0), tw = w - 32 - (tx - 16) - 14;
            string text = string.Join("\n", shown.stories.Select(e => e.text));
            float textH = pinText.CalcHeight(new GUIContent(text), tw);
            float cardH = Mathf.Max(who != null ? 132 : 0, textH + 52);
            var card = new Rect(16, outer.y - cardH - 10, w - 32, cardH);
            ProtoGui.Block(card);
            ProtoGui.Panel(card);
            if (who != null) PortraitStudio.Draw(new Rect(card.x + 14, card.y + 14, 104, 104), who, !who.alive);
            GUI.Label(new Rect(tx, card.y + 12, tw, 26), $"Year {shown.year}", pinMeta);
            GUI.Label(new Rect(tx, card.y + 38, tw, textH + 4), text, pinText);
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
