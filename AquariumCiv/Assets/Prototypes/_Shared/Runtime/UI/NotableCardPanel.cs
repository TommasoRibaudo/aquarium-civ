using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>
    /// P3+: tap a named person in the village → their card: portrait, trait, age, family and friends, their last few
    /// stories, and "Pin" to follow them (a gold marker with their name). Logged as <c>notable</c> tap / pin / unpin.
    /// </summary>
    public class NotableCardPanel : MonoBehaviour
    {
        public static NotableCardPanel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.current != null;
        public static float PanelHeight = 600f;

        Notable current;
        SimCore world; // the world of the open card (null = the live one)
        float openedAt;
        Vector2 scroll;
        GUIStyle nameStyle, small;

        void Awake() => Instance = this;
        void OnEnable() => Wanderer.Tapped += OnTapped;
        void OnDisable() => Wanderer.Tapped -= OnTapped;

        void OnTapped(Wanderer w)
        {
            if (w.NotableId < 0) return;
            var view = w.GetComponentInParent<NotableView>();
            world = view != null && view.source != null ? view.source : null; // a museum box shows its own people
            Open((world ?? SimClock.Instance.Sim).FindNotable(w.NotableId));
        }

        public void Open(Notable n)
        {
            if (n == null) return;
            if (current != null) Close("switch");
            current = n;
            openedAt = Time.unscaledTime;
            scroll = Vector2.zero;
            EventLog.Instance?.Count("notable_cards");
        }

        public void Close(string how = "close")
        {
            if (current == null) return;
            EventLog.Instance?.Log("notable", ("action", "card"), ("id", current.id), ("dwell_s", Time.unscaledTime - openedAt), ("how", how));
            current = null;
        }

        void OnGUI()
        {
            if (current == null || !SurveyPanel.IsPlaying) return;
            GUI.depth = -10; // above the chronicle cards and the HUD
            ProtoGui.Begin();
            var sim = world ?? SimClock.Instance.Sim;
            var s = sim.State;
            var n = current;
            nameStyle ??= new GUIStyle(ProtoGui.Title) { fontSize = 32 };
            small ??= new GUIStyle(ProtoGui.Wrap) { fontSize = 21 };

            float w = ProtoGui.Width, h = ProtoGui.Height;
            var outer = new Rect(16, h - ProtoGui.SafeBottom - 16 - PanelHeight, w - 32, PanelHeight);
            ProtoGui.Block(outer);
            ProtoGui.Panel(outer);
            var inner = new Rect(outer.x + 22, outer.y + 20, outer.width - 44, outer.height - 40);

            PortraitStudio.Draw(new Rect(inner.x, inner.y, 160, 160), n, !n.alive);
            var head = new Rect(inner.x + 176, inner.y, inner.width - 176, 160);
            GUILayout.BeginArea(head);
            GUILayout.Label(n.FullName, nameStyle);
            GUILayout.Label($"<i>{n.trait}</i>", ProtoGui.Wrap);
            GUILayout.Label(n.alive ? $"Age {Mathf.FloorToInt(n.age)}  ·  the {n.family} family" : $"Died at {Mathf.FloorToInt(n.age)}, year {n.diedMonth / SimConfig.MonthsPerYear}", ProtoGui.Hint);
            GUILayout.EndArea();

            var body = new Rect(inner.x, inner.y + 172, inner.width, inner.height - 172 - 96);
            GUILayout.BeginArea(body);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var line in Relations(sim, n)) GUILayout.Label(line, small);
            var stories = sim.StoriesOf(n.id).Take(3).ToList();
            if (stories.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("<b>Their story</b>", small);
                foreach (var e in stories) GUILayout.Label($"<color=#a8cfc9>Y{e.month / SimConfig.MonthsPerYear}</color>  {e.text}", small);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            var row = new Rect(inner.x, inner.yMax - 88, inner.width, 88);
            GUILayout.BeginArea(row);
            GUILayout.BeginHorizontal();
            bool pinned = s.pinnedNotableId == n.id;
            if (n.alive && GUILayout.Button(pinned ? "Unpin" : "Pin to follow", pinned ? GUI.skin.button : ProtoGui.Primary, GUILayout.Height(88)))
            {
                s.pinnedNotableId = pinned ? -1 : n.id;
                EventLog.Instance?.Log("notable", ("action", pinned ? "unpin" : "pin"), ("id", n.id));
                if (!pinned) HudToast.ShowBanner($"Following {n.FullName}. Their stories come first.", 3f);
            }
            if (GUILayout.Button("Close", GUILayout.Height(88))) Close();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static IEnumerable<string> Relations(SimCore sim, Notable n)
        {
            string Name(int id) { var o = sim.FindNotable(id); return o == null ? null : o.alive ? o.FullName : $"{o.FullName} (late)"; }
            if (n.parentId >= 0 && Name(n.parentId) is { } p) yield return $"Child of {p}";
            if (n.partnerId >= 0 && Name(n.partnerId) is { } m) yield return $"Married to {m}";
            var kids = sim.ChildrenOf(n.id).Select(k => k.name).ToList();
            if (kids.Count > 0) yield return $"Children: {string.Join(", ", kids)}";
            if (n.friendId >= 0 && Name(n.friendId) is { } f) yield return $"Best friend: {f}";
            if (n.rivalId >= 0 && Name(n.rivalId) is { } r) yield return $"Rival: {r}";
            if (n.mentorId >= 0 && Name(n.mentorId) is { } t) yield return $"Apprentice of {t}";
        }
    }
}
