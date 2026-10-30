using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AquariumCiv.Game;
using AquariumCiv.GameUI;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// TEMPORARY sim cheats panel (#40): every sim debug hook in collapsible sections, so a tester or the user can reach any
    /// state without waiting. A "sim" button (top right, inside the safe area) opens it; <see cref="SliceDebugPanel"/> keeps
    /// the camp/era/box/music tools. IMGUI, drawn on the safe area, scrollable by drag. Every action also goes through the
    /// static <c>Do*</c> methods, so `script eval` can call them (SimDebugPanel.DoRelic("lens")).
    /// Sections: Time, Era and box, Notables (selected one), Roles, Buildings, Shaping, Wishes, Relics and belief, Care.
    /// </summary>
    [AddComponentMenu("AquariumCiv/TEMPORARY Sim Debug Panel")]
    public class SimDebugPanel : MonoBehaviour
    {
        public bool open { get => DebugDock.Active == DebugDock.Sim; set => DebugDock.Active = value ? DebugDock.Sim : DebugDock.None; }
        float contentH = 600f;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        readonly Dictionary<string, bool> fold = new();
        Vector2 scroll;
        string last = "";
        int sel;                 // selected notable id (-1 = none)
        float shapeX, shapeZ = 0.8f;
        int shapeKind;
        static readonly string[] Types = { BuildingType.Hut, BuildingType.Farm, BuildingType.Lumbermill, BuildingType.Well,
            BuildingType.Windmill, BuildingType.Market, BuildingType.Tavern, BuildingType.Church, BuildingType.Workshop, BuildingType.Guildhall };
        static readonly string[] Jobs = { JobType.Gatherer, JobType.Hunter, JobType.Fisher, JobType.Woodcutter, JobType.Toolmaker,
            JobType.Farmer, JobType.Shepherd, JobType.Beekeeper, JobType.Potter, JobType.Brewer, JobType.Mason, JobType.Smith,
            JobType.Weaver, JobType.Baker, JobType.Stallholder, JobType.Sweeper, JobType.Elder };
        static readonly string[] Roles = { RoleType.Chief, RoleType.Headman, RoleType.Lord, RoleType.Miller, RoleType.Priest,
            RoleType.Innkeeper, RoleType.Guildmaster, RoleType.MasterBuilder, RoleType.Merchant, RoleType.Captain };

        // ---------- shared helpers (also for `script eval`) ----------
        static SimCore Sim => GameClock.Instance != null ? GameClock.Instance.Sim : SimLayoutSource.Find()?.Sim;

        static string Done(string text)
        {
            if (GameClock.Instance != null && GameClock.Instance.Sim != null) GameClock.Instance.Save();
            var box = SimLayoutSource.Find();
            if (box != null) box.GetComponent<BoxView>()?.Refresh();
            return text;
        }

        static string Need(out SimCore sim)
        {
            sim = Sim;
            return sim == null ? "no sim" : null;
        }

        public static string DoRelic(string id) { var e = Need(out var s); if (e != null) return e; var r = s.DebugGiveRelic(id); return Done(r == null ? "no " + id : "relic " + id); }
        public static string DoLoseSandal() { var e = Need(out var s); if (e != null) return e; return Done(s.DebugLoseSandal() != null ? "sandal lost" : "nobody to lose it"); }
        public static string DoSandalYears() { var e = Need(out var s); if (e != null) return e; return Done(s.DebugSkipSandalYears(20) ? "sandal lost 20 years longer" : "no lost sandal"); }
        public static string DoBelief(int stage, bool later) { var e = Need(out var s); if (e != null) return e; s.DebugSetBelief(stage, later); return Done($"belief stage {stage} {(later ? "later" : "early")}"); }
        public static string DoTapBack() { var e = Need(out var s); if (e != null) return e; int id = s.DebugTapBack(3); return Done(id < 0 ? "nobody tapped back" : "tapped back: " + Name(s, id)); }
        public static string DoShape(string kind, float x, float z)
        {
            var e = Need(out var s); if (e != null) return e;
            var n = s.DebugShape(kind, x, z);
            return Done(n == null ? "no such tool" : $"{kind} #{n.id} at ({x:0.0}, {z:0.0})");
        }
        public static string DoSkipToAnswer()
        {
            var e = Need(out var s); if (e != null) return e;
            var n = s.State.nudges.LastOrDefault(x => !x.credited);
            return Done(n != null && s.DebugSkipToAnswer(n.id) ? $"people answered {n.kind} #{n.id}" : "nothing waiting for an answer");
        }
        public static string DoWish(string kind, int notableId)
        {
            var e = Need(out var s); if (e != null) return e;
            var w = s.DebugForceWish(kind, notableId);
            return Done(w == null ? "nobody can wish" : $"{Name(s, w.notableId)} wishes for {w.kind}");
        }

        static BoxCamera Cam => Camera.main != null ? Camera.main.GetComponent<BoxCamera>() : FindAnyObjectByType<BoxCamera>();

        /// <summary>Camera fly-to cheats (#38): "person" (notable id, -1 = a random one alive), "fire", "building" (sim id, -1 = the first), "point" (x, z).</summary>
        public static string DoFly(string what, int id = -1, float x = 0f, float z = 0f)
        {
            var cam = Cam; if (cam == null) return "no BoxCamera";
            var sim = Sim;
            switch (what)
            {
                case "person":
                {
                    if (sim == null) return "no sim";
                    var alive = sim.State.notables.Where(n => n.alive).ToList();
                    if (alive.Count == 0) return "nobody alive";
                    var n = id >= 0 ? sim.FindNotable(id) : alive[Random.Range(0, alive.Count)];
                    return n != null && cam.FlyToPerson(AquariumCiv.Life.PersonRef.Notable(n.id)) ? "flying to " + n.FullName : "no doll for " + (n?.FullName ?? "#" + id);
                }
                case "fire":
                    return cam.FlyToPlace(new AquariumCiv.Life.Place(AquariumCiv.Life.PlaceKind.Fire)) ? "flying to the fire" : "no fire spot";
                case "building":
                {
                    if (sim == null) return "no sim";
                    int bid = id >= 0 ? id : (sim.State.buildings.FirstOrDefault()?.id ?? -1);
                    return cam.FlyToBuilding(bid) ? "flying to building #" + bid : "no spot for building #" + bid;
                }
                case "point": cam.FlyTo(new Vector3(x, 0f, z), 0.6f); return $"flying to ({x:0.0}, {z:0.0})";
                case "overview": cam.FlyTo(Vector3.zero, 1f); return "flying to the overview";
            }
            return "unknown " + what;
        }

        static string Name(SimCore s, int id) => s.FindNotable(id)?.FullName ?? ("#" + id);

        // ---------- IMGUI ----------
        void OnGUI()
        {
            if (DebugDock.Active != DebugDock.Sim || DebugDock.Hidden) return;
            var safe = DebugDock.Begin(out float sc);
            var area = DebugDock.PanelArea(safe, 340f, contentH + 60f);
            DebugDock.SetPanel(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            var sim = Sim;
            if (sim == null)
            {
                GUILayout.Label("no sim yet");
                if (GUILayout.Button("close")) open = false;
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(Status(sim));
            if (GUILayout.Button("close", GUILayout.Width(52))) open = false;
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(last)) GUILayout.Label(last);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginVertical();

            if (Section("Time"))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+1 yr")) last = SimLayoutSource.DebugSkipYears(1);
                if (GUILayout.Button("+10 yr")) last = SimLayoutSource.DebugSkipYears(10);
                if (GUILayout.Button("+40 yr")) last = SimLayoutSource.DebugSkipYears(40);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Live +", GUILayout.Width(44));
                if (GUILayout.Button("1h")) last = GameClock.DebugSkipHours(1);
                if (GUILayout.Button("8h")) last = GameClock.DebugSkipHours(8);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Away", GUILayout.Width(44));
                if (GUILayout.Button("+1h")) last = GameClock.DebugAway(1);
                if (GUILayout.Button("+8h")) last = GameClock.DebugAway(8);
                if (GUILayout.Button("+48h")) last = GameClock.DebugAway(48);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("One story event")) { sim.ForceStory(); last = Done("story forced"); }
            }

            if (Section("Era and box"))
            {
                var st = sim.State;
                GUILayout.Label($"era {st.era} · ready {st.eraReady} · peak {st.peakReady} · complete {st.boxComplete} · glass {sim.GlassStage}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Era ready")) last = Done(sim.ForceEraReady() ? "era ready" : "already ready / last era");
                if (GUILayout.Button("Next era")) last = SimLayoutSource.DebugNextEra(true);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Peak ready")) last = Done(sim.ForcePeakReady() ? "peak ready" : "not in the town / already");
                if (GUILayout.Button("Complete box")) last = Done(sim.ForceCompleteBox() != null ? "box complete" : "already complete");
                GUILayout.EndHorizontal();
                if (GUILayout.Button("End festival")) { sim.DebugEndFestival(); last = Done("festival ended"); }
                if (GUILayout.Button("Open teaser")) last = Done(sim.DebugOpenTeaser() ?? "no teaser");
            }

            if (Section("Notables")) NotablesGui(sim);
            if (Section("Roles")) RolesGui(sim);

            if (Section("Camera"))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Fly: random person")) last = DoFly("person");
                if (GUILayout.Button("Fly: selected")) last = DoFly("person", sel);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Fly: fire")) last = DoFly("fire");
                if (GUILayout.Button("Fly: building")) last = DoFly("building");
                if (GUILayout.Button("Fly: overview")) last = DoFly("overview");
                GUILayout.EndHorizontal();
            }

            if (Section("Buildings"))
            {
                GUILayout.Label(string.Join(" ", sim.State.buildings.GroupBy(b => b.type).Select(g => g.Key + "×" + g.Count())));
                for (int i = 0; i < Types.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < i + 2 && j < Types.Length; j++)
                    {
                        if (GUILayout.Button("+" + Types[j])) last = SimLayoutSource.DebugBuild(Types[j]);
                        if (GUILayout.Button("-", GUILayout.Width(28))) last = SimLayoutSource.DebugRemoveBuilding(Types[j]);
                    }
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button(SimLayoutSource.CobbleAllPaths ? "Cobbles on every path: on (tap = off)" : "Cobbles on every path: off (tap = on)"))
                    last = SimLayoutSource.DebugCobblePaths(!SimLayoutSource.CobbleAllPaths);
            }

            if (Section("Shaping"))
            {
                GUILayout.Label($"charges {sim.ShapingCharges:0.0}/{sim.ShapingMaxCharges:0}");
                var tools = SimCore.SliceTools;
                shapeKind = Mathf.Clamp(shapeKind, 0, tools.Length - 1);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < tools.Length; i++) if (GUILayout.Toggle(shapeKind == i, tools[i], GUI.skin.button)) shapeKind = i;
                GUILayout.EndHorizontal();
                shapeX = Slider("x", shapeX, -2.6f, 2.6f);
                shapeZ = Slider("z", shapeZ, -1.9f, 1.9f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Shape here (free)")) last = DoShape(tools[shapeKind], shapeX, shapeZ);
                if (GUILayout.Button("Refill")) { sim.DebugRefillShaping(); last = Done("charges full"); }
                GUILayout.EndHorizontal();
                var waiting = sim.State.nudges.Count(n => !n.credited);
                if (GUILayout.Button($"People answer the last one ({waiting} waiting)")) last = DoSkipToAnswer();
            }

            if (Section("Wishes"))
            {
                var ow = sim.OpenWish;
                GUILayout.Label(ow == null ? "no open wish" : $"{Name(sim, ow.notableId)} wishes for {ow.kind}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Wish (selected)")) last = DoWish(null, sel);
                if (GUILayout.Button("Wish (anyone)")) last = DoWish(null, -1);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                foreach (var k in SimCore.SliceTools) if (GUILayout.Button(k)) last = DoWish(k, sel);
                GUILayout.EndHorizontal();
            }

            if (Section("Relics and belief"))
            {
                GUILayout.Label("relics: " + (sim.State.relics.Count == 0 ? "none" : string.Join(", ", sim.State.relics.Select(r => r.id + (r.Found ? "" : " (lost)")))));
                for (int i = 0; i < RelicId.All.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < i + 2 && j < RelicId.All.Length; j++)
                        if (GUILayout.Button("Give " + RelicId.All[j])) last = DoRelic(RelicId.All[j]);
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Lose sandal")) last = DoLoseSandal();
                if (GUILayout.Button("Sandal +20 yr")) last = DoSandalYears();
                GUILayout.EndHorizontal();
                for (int stage = 1; stage <= 3; stage++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Belief " + stage, GUILayout.Width(60));
                    if (GUILayout.Button("early")) last = DoBelief(stage, false);
                    if (GUILayout.Button("later")) last = DoBelief(stage, true);
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Glass: tap back x3")) last = DoTapBack();
            }

            if (Section("Care"))
            {
                GUILayout.Label($"mess {sim.MessLevel:0.00} · grime {sim.State.grime:0.00} · overgrowth {sim.State.overgrowth:0.00} · trash {sim.State.trash.Count}");
                if (GUILayout.Button("Sweepers clear a heap")) last = Done(sim.DebugSweep() ? "swept" : "no sweepers (town)");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+grime .3")) { sim.AddMess(0.3f, 0f, 0); last = Done("grime +0.3"); }
                if (GUILayout.Button("+trash 3")) { sim.AddMess(0f, 0f, 3); last = Done("trash +3"); }
                if (GUILayout.Button("+weeds .3")) { sim.AddMess(0f, 0.3f, 0); last = Done("overgrowth +0.3"); }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Full mess")) { sim.AddMess(1f, 1f, sim.Config.trashMax); last = Done("full mess"); }
                if (GUILayout.Button("Clean all")) { sim.CleanAll(); last = Done("cleaned all"); }
                GUILayout.EndHorizontal();
                var hud = GameHud.Instance;
                if (hud != null && GUILayout.Button(hud.CleanMode ? "Clean mode: on (tap = off)" : "Clean mode: off (tap = on)")) { hud.SetCleanMode(!hud.CleanMode); last = Done("clean mode " + (hud.CleanMode ? "on" : "off")); }
                var care = CareView.Instance;
                if (care != null) GUILayout.Label($"sun x{care.SunFactor:0.00} · glass drawn {care.GlassMean:0.00} · heaps {care.Props.HeapCount} · weeds {care.Props.WeedCount} · strokes {care.StrokeCount}");
            }

            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) contentH = GUILayoutUtility.GetLastRect().height;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        bool Section(string title)
        {
            fold.TryGetValue(title, out bool on);
            bool now = GUILayout.Toggle(on, (on ? "v " : "> ") + title, GUI.skin.button);
            fold[title] = now;
            return now;
        }

        static float Slider(string label, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + " " + v.ToString("0.0", Ci), GUILayout.Width(52));
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.EndHorizontal();
            return v;
        }

        static string Status(SimCore s)
        {
            var st = s.State;
            return string.Format(Ci, "year {0:0.0} · {1} · pop {2} · {3} notables", st.Year, st.era, st.population, st.notables.Count(n => n.alive));
        }

        void NotablesGui(SimCore sim)
        {
            var alive = sim.State.notables.Where(n => n.alive).OrderBy(n => n.id).ToList();
            if (alive.Count == 0) { GUILayout.Label("nobody alive"); return; }
            int idx = alive.FindIndex(n => n.id == sel);
            if (idx < 0) { idx = 0; sel = alive[0].id; }
            var cur = alive[idx];
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(32))) sel = alive[(idx + alive.Count - 1) % alive.Count].id;
            GUILayout.Label($"{cur.FullName} · {cur.trait} · age {cur.age:0} · job {(string.IsNullOrEmpty(cur.job) ? "-" : cur.job)} · {sim.RoleOf(cur) ?? "no role"}");
            if (GUILayout.Button(">", GUILayout.Width(32))) sel = alive[(idx + 1) % alive.Count].id;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Set job: " + (string.IsNullOrEmpty(cur.job) ? "-" : cur.job) + " (next)"))
            {
                int j = System.Array.IndexOf(Jobs, cur.job);
                sim.DebugSetJob(cur, Jobs[(j + 1) % Jobs.Length]);
                last = Done($"{cur.FullName} → {cur.job}");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Redraw all jobs")) { sim.DebugRedrawJobs(); last = Done("jobs redrawn"); }
            GUILayout.Label("open: " + string.Join(",", sim.OpenJobs()));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Trait: " + cur.trait + " (next)"))
            {
                int t = System.Array.IndexOf(NameData.Traits, cur.trait);
                cur.trait = NameData.Traits[(t + 1) % NameData.Traits.Length];
                last = Done($"{cur.FullName} is now {cur.trait}");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Birth")) last = Done(sim.DebugBirth() is Notable c ? "born: " + c.FullName : "no adult to bear");
            if (GUILayout.Button("Oldest dies")) last = Done(sim.DebugKillOldest() is Notable k ? "died: " + k.FullName : "nobody");
            GUILayout.EndHorizontal();
            // readout of everyone: name, trait, age, job, role
            var sb = new StringBuilder();
            foreach (var n in alive)
                sb.AppendLine($"{(n.id == sel ? "> " : "  ")}{n.FullName}, {n.trait}, {n.age:0}, {(string.IsNullOrEmpty(n.job) ? "-" : n.job)}{(sim.RoleOf(n) is string r ? ", " + r : "")}");
            GUILayout.Label(sb.ToString());
        }

        void RolesGui(SimCore sim)
        {
            sim.TermFlavourOn = GUILayout.Toggle(sim.TermFlavourOn, "Term flavour (trait stories) on");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill / hold elections")) { sim.DebugFillRoles(); last = Done("contests held"); }
            GUILayout.EndHorizontal();
            foreach (var role in Roles.Where(sim.RoleOpen))
            {
                var seat = sim.SeatOf(role);
                string holder = seat == null || seat.holderId < 0 ? "vacant" : Name(sim, seat.holderId);
                GUILayout.Label($"{role}: {holder}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Vacate")) last = Done(sim.DebugVacateRole(role) ? role + " vacated" : "not held");
                if (GUILayout.Button("Set = selected")) last = Done(sim.DebugSetRoleHolder(role, sim.FindNotable(sel)) ? role + " → " + Name(sim, sel) : "can't");
                if (GUILayout.Button("End term")) last = Done(sim.DebugEndTerm(role) ? "term ended" : "no term");
                if (GUILayout.Button("Story")) last = Done(sim.DebugTermStory(role)?.text ?? "no fresh term line");
                GUILayout.EndHorizontal();
            }
        }
    }
}
