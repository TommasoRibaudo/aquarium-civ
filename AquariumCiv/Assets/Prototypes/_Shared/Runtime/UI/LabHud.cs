using System;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// P3+ HUD basics, as P2 has them: a one-line ledger top-left (below the lab strip), tester-facing
    /// "Step away +1/+8/+24/+48 h" in lab (the same path as a real resume), and a 2 s hold on the ledger that switches
    /// the phone between diary and lab (each switch starts a fresh world via ModeSwitched).
    /// </summary>
    public class LabHud : MonoBehaviour
    {
        public int[] stepAwayHours = { 1, 8, 24, 48 };
        public bool showStepAway = true;
        [Tooltip("Seconds to hold the ledger to switch diary/lab.")] public float holdSeconds = 2f;

        /// <summary>Set by the prototype while its own panel uses the bottom of the screen.</summary>
        public bool hidden;
        /// <summary>Optional extra ledger text (appended after a dot).</summary>
        public Func<string> extra;
        /// <summary>The ledger hold switched the mode (the prototype starts a fresh world for it).</summary>
        public event Action<TestMode> ModeSwitched;

        public static LabHud Instance { get; private set; }
        /// <summary>Canvas y just below the ledger, for banners and panels that sit under it.</summary>
        public float LedgerBottom { get; private set; } = 160f;
        /// <summary>Canvas y of the top of the step-away row (or of the Feedback/Done row when it's hidden).</summary>
        public float BottomTop => ProtoGui.Height - ProtoGui.SafeBottom - 112 - (StepAwayShowing ? 12 + 108 : 0);

        bool Lab => PrototypeConfig.Current.testMode == TestMode.Lab;
        bool StepAwayShowing => Lab && showStepAway && !hidden && SurveyPanel.IsPlaying;

        GUIStyle ledgerStyle;
        Rect ledgerScreen;
        float holdTime;
        bool holdDone, holdOnLedger;

        void Awake() => Instance = this;

        void Update() => UpdateHold();

        void UpdateHold()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.isPressed) { holdTime = 0; holdDone = false; holdOnLedger = false; return; }
            var p = pointer.position.ReadValue();
            bool onLedger = ledgerScreen.Contains(new Vector2(p.x, Screen.height - p.y));
            if (pointer.press.wasPressedThisFrame) holdOnLedger = onLedger; // only a press that starts on the ledger counts
            if (holdDone || !holdOnLedger || !onLedger) { holdTime = 0; return; }
            if ((holdTime += Time.unscaledDeltaTime) < holdSeconds) return;
            holdDone = true;
            var cfg = PrototypeConfig.Current;
            var mode = cfg.testMode == TestMode.Lab ? TestMode.Diary : TestMode.Lab;
            EventLog.Instance?.Log("debug", ("test_mode", mode.ToString()));
            SimClock.Instance.Save(); // into this mode's own save, before the switch changes which file is current
            cfg.SetTestMode(mode);
            ModeSwitched?.Invoke(mode);
            HudToast.ShowBanner(mode == TestMode.Diary
                ? "Diary mode: the tank now runs on real time. Come back whenever you like."
                : "Lab mode: use \"Step away\" below to leave the tank for a while.", 6f);
        }

        void OnGUI()
        {
            if (!SurveyPanel.IsPlaying) return;
            ProtoGui.Begin();
            var clock = SimClock.Instance;
            var s = clock.Sim.State;
            float safeTop = (Screen.height - Screen.safeArea.yMax) / ProtoGui.Scale;
            string ledger = (Lab ? "<color=#8fe0d0>LAB</color>  ·  " : "") + $"Year {Mathf.FloorToInt(s.Year)}  ·  {s.population} people";
            if (!string.IsNullOrEmpty(s.settlementName)) ledger += $"  ·  {s.settlementName}";
            var more = extra?.Invoke();
            if (!string.IsNullOrEmpty(more)) ledger += $"  ·  {more}";
            ledgerStyle ??= new GUIStyle(ProtoGui.Box) { richText = true, wordWrap = false };
            var size = ledgerStyle.CalcSize(new GUIContent(ledger));
            var r = new Rect(20, Mathf.Max(20, safeTop + 4) + LabStrip.Reserved, Mathf.Min(size.x + 8, ProtoGui.Width - 40), size.y);
            ProtoGui.Block(r);
            GUI.Box(r, ledger, ledgerStyle);
            LedgerBottom = r.yMax;
            ledgerScreen = new Rect(r.x * ProtoGui.Scale, r.y * ProtoGui.Scale, r.width * ProtoGui.Scale, r.height * ProtoGui.Scale);
            if (holdTime > 0.3f && !holdDone)
                GUI.Box(new Rect(r.x + 8, r.yMax + 4, (r.width - 16) * Mathf.Clamp01(holdTime / holdSeconds), 8), GUIContent.none, ProtoGui.Primary);

            if (!StepAwayShowing) return;
            float w = ProtoGui.Width;
            var row = new Rect(20, ProtoGui.Height - ProtoGui.SafeBottom - 112 - 12 - 108, w - 40, 108);
            ProtoGui.Block(row);
            ProtoGui.Panel(row);
            GUILayout.BeginArea(new Rect(row.x + 12, row.y + 8, row.width - 24, row.height - 16));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Step away:", GUILayout.Width(120), GUILayout.Height(88));
            foreach (var h in stepAwayHours)
                if (GUILayout.Button($"+{h} h", GUILayout.Height(88))) clock.SimulateAway(h);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
