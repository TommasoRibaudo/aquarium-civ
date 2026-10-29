using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes.P3
{
    /// <summary>
    /// Variant A's return: 3–7 "while you were away" cards, top story first (salience, plus a boost for the pinned
    /// notable), each with the person's portrait, and "Show me" flies the camera to them or to the place.
    /// Sits in the bottom half (thumb zone), above Feedback / Done. Logs <c>chronicle_view</c> per card.
    /// </summary>
    public class ChronicleCards : MonoBehaviour
    {
        public int minCards = 3, maxCards = 7;
        [Tooltip("Cards beyond the minimum need at least this salience.")] public float keepAbove = 0.5f;
        public float pinnedBoost = 0.25f;
        public float panelHeight = 470f;

        public bool IsOpen => cards.Count > 0;
        public event System.Action Closed;

        readonly List<ChronicleEntry> cards = new();
        int index;
        float shownAt;
        bool flewThisCard;
        GUIStyle titleStyle, textStyle, metaStyle;

        static readonly Dictionary<string, string> Titles = new()
        {
            ["death"] = "Farewell", ["birth"] = "A new arrival", ["wedding"] = "A wedding", ["friendship"] = "New friends",
            ["rivalry"] = "A falling-out", ["reconcile"] = "Made up", ["first_building"] = "A first", ["milestone"] = "Growing",
            ["festival"] = "A festival", ["lean_winter"] = "A lean winter", ["naming"] = "A name", ["anniversary"] = "An anniversary",
            ["invention"] = "An invention", ["song"] = "A new song", ["coming_of_age"] = "Grown up", ["rise"] = "Someone new",
            ["apprentice"] = "An apprentice", ["elder"] = "The eldest", ["storm"] = "A storm", ["rainbow"] = "A rainbow",
            ["fog"] = "Fog", ["drought"] = "A dry summer", ["bumper"] = "A bumper harvest", ["snow"] = "First snow",
            ["strange_light"] = "A strange light", ["ducks"] = "New neighbours", ["sheep"] = "A regular", ["lost"] = "Lost and found",
            ["prank"] = "Mischief", ["giant_turnip"] = "A giant turnip", ["founding"] = "The founding",
        };

        /// <summary>Pick the cards for this return from what happened since <paramref name="sinceMonth"/>.</summary>
        public static List<ChronicleEntry> Rank(SimState s, long sinceMonth, int min, int max, float keepAbove, float pinnedBoost)
        {
            float Score(ChronicleEntry e) => e.salience + (s.pinnedNotableId >= 0 && (e.notableId == s.pinnedNotableId || e.otherNotableId == s.pinnedNotableId) ? pinnedBoost : 0f);
            var ranked = s.chronicle.Where(e => e.month > sinceMonth && e.kind != "grumble")
                .OrderByDescending(Score).ThenByDescending(e => e.month).ToList();
            return ranked.Where((e, i) => i < min || (i < max && Score(e) >= keepAbove)).ToList();
        }

        public void Show(List<ChronicleEntry> entries)
        {
            cards.Clear();
            cards.AddRange(entries);
            index = 0;
            shownAt = Time.unscaledTime;
            flewThisCard = false;
            if (cards.Count > 0) Juice.Chime();
        }

        void Leave(string how)
        {
            var e = cards[index];
            EventLog.Instance?.Log("chronicle_view", ("card", e.id), ("kind", e.kind), ("rank", index + 1), ("of", cards.Count),
                ("dwell_s", Time.unscaledTime - shownAt), ("fly_to", flewThisCard), ("how", how));
            EventLog.Instance?.Count("cards_read");
            EventLog.Instance?.Count("card_dwell_s", Time.unscaledTime - shownAt);
            if (flewThisCard) OrbitCamera.Instance?.ResetView();
        }

        void Next()
        {
            Leave("next");
            index++;
            shownAt = Time.unscaledTime;
            flewThisCard = false;
            if (index >= cards.Count) Close();
        }

        public void Close(string how = null)
        {
            if (how != null && index < cards.Count) Leave(how);
            cards.Clear();
            Closed?.Invoke();
        }

        static DioramaView village;

        /// <summary>Where "Show me" goes: the person, the other person, or the building.</summary>
        static bool Where(ChronicleEntry e, out Vector3 at, out Wanderer who)
        {
            at = default;
            who = null;
            if (village == null) village = FindAnyObjectByType<DioramaView>();
            var b = village != null && e.buildingId >= 0 ? village.Find(e.buildingId) : null;
            if (b != null && e.kind == "first_building") { at = b.transform.position; return true; } // the new building is the news
            var nv = NotableView.Instance;
            if (nv != null) who = nv.Find(e.notableId) ?? nv.Find(e.otherNotableId);
            if (who != null) { at = who.transform.position; return true; }
            if (b != null) { at = b.transform.position; return true; }
            return false;
        }

        void FlyTo(ChronicleEntry e)
        {
            if (!Where(e, out var at, out var who)) return;
            var cam = OrbitCamera.Instance;
            // Pivot a little towards the camera, so the target lands above the card instead of behind it.
            var f = cam.transform.forward; f.y = 0; f.Normalize();
            cam.FocusOn(at - f * 0.9f, 0.35f);
            if (who != null)
            {
                who.Cheer(2.5f);
                HudToast.Show(who.DisplayName, who.transform.position + Vector3.up * 0.7f, 3f);
            }
            flewThisCard = true;
            EventLog.Instance?.Count("fly_to");
        }

        void OnGUI()
        {
            if (cards.Count == 0 || !SurveyPanel.IsPlaying || NotableCardPanel.IsOpen) return;
            ProtoGui.Begin();
            titleStyle ??= new GUIStyle(ProtoGui.Title) { fontSize = 30 };
            textStyle ??= new GUIStyle(ProtoGui.Wrap) { fontSize = 24 };
            metaStyle ??= new GUIStyle(ProtoGui.Hint) { fontSize = 20 };
            var sim = SimClock.Instance.Sim;
            var e = cards[index];
            var who = sim.FindNotable(e.notableId);

            float w = ProtoGui.Width, h = ProtoGui.Height;
            var outer = new Rect(16, h - ProtoGui.SafeBottom - 112 - 12 - panelHeight, w - 32, panelHeight);
            ProtoGui.Block(outer);
            ProtoGui.Panel(outer);
            var inner = new Rect(outer.x + 22, outer.y + 18, outer.width - 44, outer.height - 36);

            // Header: "While you were away · 2 of 5", with page dots.
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 30), $"While you were away  ·  {index + 1} of {cards.Count}", metaStyle);
            for (int i = 0; i < cards.Count; i++)
                ProtoGui.Dot(new Rect(inner.xMax - (cards.Count - i) * 24, inner.y + 8, 14, 14),
                    i == index ? ProtoGui.AccentColor : new Color(0.55f, 0.8f, 0.8f, i < index ? 0.8f : 0.35f));

            float top = inner.y + 40;
            float textX = inner.x;
            if (who != null)
            {
                PortraitStudio.Draw(new Rect(inner.x, top, 150, 150), who, !who.alive);
                textX = inner.x + 166;
            }
            var titleR = new Rect(textX, top, inner.xMax - textX, 40);
            GUI.Label(titleR, Titles.TryGetValue(e.kind, out var t) ? t : "News", titleStyle);
            GUI.Label(new Rect(textX, top + 42, inner.xMax - textX, 28),
                who != null ? $"{who.FullName}, {who.trait}  ·  year {e.month / SimConfig.MonthsPerYear}" : $"Year {e.month / SimConfig.MonthsPerYear}", metaStyle);
            float textTop = who != null ? top + 160 : top + 80;
            GUI.Label(new Rect(inner.x, textTop, inner.width, inner.yMax - 96 - textTop), e.text, textStyle);

            var row = new Rect(inner.x, inner.yMax - 88, inner.width, 88);
            GUILayout.BeginArea(row);
            GUILayout.BeginHorizontal();
            bool last = index == cards.Count - 1;
            if (GUILayout.Button("Skip all", GUILayout.Height(88), GUILayout.Width(150))) { Close("skip"); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
            if (Where(e, out _, out _) && GUILayout.Button("Show me", GUILayout.Height(88))) FlyTo(e);
            if (GUILayout.Button(last ? "Done" : "Next", ProtoGui.Primary, GUILayout.Height(88))) Next();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
