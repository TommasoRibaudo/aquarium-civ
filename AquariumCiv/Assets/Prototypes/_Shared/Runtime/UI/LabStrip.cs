using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// Lab prototypes only: a slim strip under the notch that says what you're testing and what it's compared with
    /// ("P3 · A: Chronicle cards", "vs B: Plain return"), with "Switch to B" and "Hub". A is teal, B is sand, here and on
    /// the hub. Switching starts the other variant's lab setup fresh (the prototype listens to VariantChanged).
    /// </summary>
    public class LabStrip : MonoBehaviour
    {
        const float StripHeight = 104f, Gap = 8f;

        static LabStrip instance;
        /// <summary>Canvas height the strip takes below the safe top (0 when no strip is showing), for HUD layout.</summary>
        public static float Reserved => instance != null && instance.Showing ? StripHeight + Gap : 0f;

        GUIStyle line1, line2;
        PrototypeConfig cfg;

        bool Showing => cfg != null && cfg.testMode == TestMode.Lab && SurveyPanel.IsPlaying;

        void Awake() => instance = this;

        void Start()
        {
            cfg = PrototypeConfig.Current;
            LabSession.MarkPlayed(cfg.protoId, cfg.CurrentIndex, cfg.testMode);
            cfg.VariantChanged += _ => LabSession.MarkPlayed(cfg.protoId, cfg.CurrentIndex, cfg.testMode);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void OnGUI()
        {
            if (!Showing) return;
            ProtoGui.Begin();
            var entry = TestCatalog.Find(cfg.protoId);
            if (entry == null) return;
            int v = cfg.CurrentIndex, other = 1 - v;
            line1 ??= new GUIStyle(ProtoGui.Wrap) { fontSize = 23, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip };
            line2 ??= new GUIStyle(ProtoGui.Hint) { fontSize = 20, wordWrap = false, clipping = TextClipping.Clip };

            float safeTop = (Screen.height - Screen.safeArea.yMax) / ProtoGui.Scale;
            float w = ProtoGui.Width;
            var r = new Rect(12, Mathf.Max(8, safeTop + 4), w - 24, StripHeight);
            ProtoGui.Block(r);
            ProtoGui.Panel(r);

            bool hub = HubLink.Available;
            float hubW = hub ? 104 : 0, switchW = 176;
            float textX = r.x + 16, textW = r.width - 32 - switchW - hubW - 8;
            // Line 1: "P3 · [A] Chronicle cards", line 2: "vs B: Plain return"
            var pill = new Rect(textX + 60, r.y + 12, 34, 34);
            GUI.Label(new Rect(textX, r.y + 12, 50, 38), $"<b>{entry.id}</b> ·", line1);
            GUI.Box(pill, TestCatalog.Letter(v), ProtoGui.VariantPill(v));
            GUI.Label(new Rect(pill.xMax + 8, r.y + 12, textW - (pill.xMax + 8 - textX), 38), entry.names[v], line1);
            GUI.Label(new Rect(textX, r.y + 56, textW, 34),
                $"vs <color={ProtoGui.VariantHex(other)}><b>{TestCatalog.Letter(other)}</b></color>: {entry.names[other]}", line2);

            var sw = new Rect(r.xMax - 8 - hubW - switchW, r.y + 8, switchW, 88);
            if (GUI.Button(sw, $"Switch to {TestCatalog.Letter(other)}", ProtoGui.VariantButton(other)))
            {
                EventLog.Instance?.Log("lab_switch", ("to", TestCatalog.Letter(other)));
                cfg.ForceVariant(other);
            }
            if (hub && GUI.Button(new Rect(r.xMax - 8 - hubW, r.y + 8, hubW - 4, 88), "Hub")) HubLink.GoToHub();
        }
    }

    /// <summary>
    /// After the second variant of a pair has been surveyed (lab, via the hub): "Which did you prefer, A or B, and why?"
    /// Drawn by SurveyPanel's thank-you screen, then back to the hub. Logged as <c>pair_pref</c> and sent like a survey.
    /// </summary>
    public static class PairQuestion
    {
        static int pick = -1;
        static string why = "";

        public static bool Due
        {
            get
            {
                var cfg = PrototypeConfig.Current;
                return cfg != null && LabLaunch.ViaHub && cfg.testMode == TestMode.Lab &&
                       LabSession.BothSurveyed(cfg.protoId) && !LabSession.Paired(cfg.protoId);
            }
        }

        /// <summary>Draw inside a GUILayout area (the survey modal). Returns true when done.</summary>
        public static bool Draw()
        {
            var cfg = PrototypeConfig.Current;
            var entry = TestCatalog.Find(cfg.protoId);
            GUILayout.Label("You've played both!", ProtoGui.Title);
            GUILayout.Label("Which did you prefer, A or B, and why?", ProtoGui.Wrap);
            GUILayout.Space(8);
            for (int i = 0; i < 2; i++)
            {
                string label = $"{TestCatalog.Letter(i)} · {entry?.names[i]}{(pick == i ? "  (your pick)" : "")}";
                if (GUILayout.Button(label, ProtoGui.VariantButton(i), GUILayout.Height(88))) pick = i;
                if (entry != null) GUILayout.Label(entry.lines[i], ProtoGui.Hint);
            }
            GUILayout.Space(6);
            GUILayout.Label("Why? (a few words is plenty)", ProtoGui.Wrap);
            why = GUILayout.TextArea(why, 300, GUILayout.Height(120));
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Skip", GUILayout.Height(88), GUILayout.Width(150)))
            {
                EventLog.Instance?.Log("pair_pref", ("pair", cfg.protoId), ("pick", "skip"), ("why", why), ("last_played", cfg.Variant));
                LabSession.MarkPaired(cfg.protoId);
                pick = -1; why = "";
                GUILayout.EndHorizontal();
                if (HubLink.Available) HubLink.GoToHub();
                return true;
            }
            bool send = ProtoGui.PrimaryButton(HubLink.Available ? "Send and back to the hub" : "Send", pick >= 0, GUILayout.Height(88));
            GUILayout.EndHorizontal();
            if (!send) return false;

            string letter = TestCatalog.Letter(pick);
            EventLog.Instance?.Log("pair_pref", ("pair", cfg.protoId), ("pick", letter), ("why", why), ("last_played", cfg.Variant));
            FeedbackSender.Instance?.SendPairPreference(cfg.protoId, letter, why);
            LabSession.MarkPaired(cfg.protoId);
            pick = -1; why = "";
            if (HubLink.Available) HubLink.GoToHub();
            return true;
        }
    }
}
