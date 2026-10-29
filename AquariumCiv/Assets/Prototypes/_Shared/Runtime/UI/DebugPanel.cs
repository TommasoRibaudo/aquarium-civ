using System.Linq;
using AquariumCiv.Prototypes.Sim;
using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>Dev-only panel: sim stats, time speed, away buttons, variant switch, log export. Three-finger tap / F1 toggles.</summary>
    public class DebugPanel : MonoBehaviour
    {
        public MessView mess;
        public bool visible;
        Vector2 scroll;

        static readonly (string label, float speed)[] Speeds = { ("x1 (1h=1y)", 1f), ("x60", 60f), ("x600", 600f), ("Pause", 0f) };

        void Start()
        {
            if (!Debug.isDebugBuild) { enabled = false; return; }
            if (OrbitCamera.Instance != null) OrbitCamera.Instance.ThreeFingerTap += () => visible = !visible;
        }

        void OnGUI()
        {
            ProtoGui.Begin();
            if (!visible) return;
            var clock = SimClock.Instance;
            var sim = clock.Sim;
            var s = sim.State;
            var cfg = PrototypeConfig.Current;

            var area = new Rect(10, 10, ProtoGui.Width - 20, ProtoGui.Height * 0.62f);
            ProtoGui.Block(area);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 10, area.y + 10, area.width - 20, area.height - 20));
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label($"<b>{cfg.protoId}</b> variant <b>{cfg.Variant}</b> ({cfg.testMode})   speed x{clock.speed:0}   tester {Tester.Nickname}");
            GUILayout.Label($"Year {s.Year:0.0}  pop {s.population}/{sim.Housing}  food {s.food:0}  wood {s.wood:0}  mood {s.mood:0.00}");
            GUILayout.Label($"Mess {sim.MessLevel:0.00} (grime {s.grime:0.00}, vines {s.overgrowth:0.00}, trash {s.trash.Count})  output x{sim.OutputMultiplier:0.00}");
            GUILayout.Label($"Buildings {s.buildings.Count}: {string.Join(", ", s.buildings.GroupBy(b => b.type).Select(g => $"{g.Key} {g.Count()}"))}");

            GUILayout.BeginHorizontal();
            foreach (var (label, speed) in Speeds)
                if (GUILayout.Button(label, GUILayout.Height(54))) clock.speed = speed;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (var h in new[] { 1, 8, 48 })
                if (GUILayout.Button($"Away +{h}h", GUILayout.Height(54))) clock.SimulateAway(h);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < cfg.variants.Length; i++)
                if (GUILayout.Button($"Variant {cfg.variants[i]}", GUILayout.Height(54))) cfg.ForceVariant(i);
            if (GUILayout.Button("Next (lab)", GUILayout.Height(54))) cfg.NextVariant();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wipe glass", GUILayout.Height(54)) && mess != null) mess.WipeGlass(1f, "debug");
            if (GUILayout.Button("Clean all", GUILayout.Height(54)) && mess != null) mess.CleanEverything("debug");
            if (GUILayout.Button("Reset world", GUILayout.Height(54))) clock.ResetWorld();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Messy tank", GUILayout.Height(54)) && mess != null) mess.MakeMessy();
            if (GUILayout.Button("Grime +0.3", GUILayout.Height(54))) sim.AddMess(0.3f, 0, 0);
            if (GUILayout.Button("Trash +3", GUILayout.Height(54))) sim.AddMess(0, 0, 3);
            if (GUILayout.Button("Vines +0.3", GUILayout.Height(54))) sim.AddMess(0, 0.3f, 0);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy last summary", GUILayout.Height(54))) GUIUtility.systemCopyBuffer = EventLog.Instance.LastSummary;
            if (GUILayout.Button("Copy log path", GUILayout.Height(54))) GUIUtility.systemCopyBuffer = EventLog.Instance.LogPath;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("<b>Chronicle</b>");
            foreach (var e in s.chronicle.AsEnumerable().Reverse().Take(8))
                GUILayout.Label($"Y{e.month / SimConfig.MonthsPerYear}: {e.text}", ProtoGui.Wrap);

            GUILayout.Space(8);
            GUILayout.Label("<b>Notables</b>");
            foreach (var n in s.notables.Where(n => n.alive))
                GUILayout.Label($"{n.FullName}, {n.trait}, age {n.age:0}/{n.lifespan:0}", ProtoGui.Wrap);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
